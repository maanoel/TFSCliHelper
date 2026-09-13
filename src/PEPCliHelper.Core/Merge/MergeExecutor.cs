using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.Tfvc;

namespace PEPCliHelper.Core.Merge;

/// <summary>
/// Aplica o merge nos destinos prontos, em sequência (spec 007). Nunca faz check-in, nunca resolve
/// conflitos e nunca faz rollback. Cancelamento não desfaz destinos concluídos.
/// </summary>
public sealed class MergeExecutor
{
  /// <summary>Plano mais novo que isto usa revalidação leve (pending changes + graváveis sem checkout).</summary>
  public static readonly TimeSpan LightRevalidationWindow = TimeSpan.FromMinutes(10);

  private readonly ITfvcClient _tfvc;
  private readonly MergePlanner _planner;
  private readonly TimeProvider _time;

  public MergeExecutor(ITfvcClient tfvc, MergePlanner planner, TimeProvider? time = null)
  {
    _tfvc = tfvc;
    _planner = planner;
    _time = time ?? TimeProvider.System;
  }

  public async Task<MergeExecutionResult> ExecuteAsync(MergePlan plan, IOperationLog log, CancellationToken cancellationToken)
  {
    if (!plan.CanExecute)
      throw new InvalidOperationException("O plano possui impedimentos globais ou nenhum destino pronto.");

    var request = plan.Request;
    var results = new List<MergeTargetResult>();
    var stop = false;
    var environmentFailure = false;

    foreach (var target in plan.Targets)
    {
      var name = target.Target.Id;

      if (target.Readiness == TargetReadiness.AlreadyIntegrated)
      {
        results.Add(Result(target, MergeTargetState.AlreadyIntegrated, target.Notes.FirstOrDefault() ?? "Já integrado."));
        continue;
      }

      if (target.Readiness == TargetReadiness.Blocked)
      {
        results.Add(Result(target, MergeTargetState.Blocked, string.Join(" ", target.Blockers)));
        continue;
      }

      if (stop)
      {
        results.Add(Result(target, MergeTargetState.NotStarted, "Não iniciado: execução interrompida após destino anterior."));
        continue;
      }

      if (cancellationToken.IsCancellationRequested)
      {
        results.Add(Result(target, MergeTargetState.NotStarted, "Não iniciado: cancelamento solicitado."));
        stop = true;
        continue;
      }

      var result = await ExecuteTargetAsync(plan, target, log, cancellationToken);
      results.Add(result.Result);
      environmentFailure |= result.EnvironmentFailure;
      stop = MergeOutcome.ShouldStop(result.Result.State, request.StopOnFailure, result.EnvironmentFailure);
    }

    return new MergeExecutionResult(results, environmentFailure, cancellationToken.IsCancellationRequested);
  }

  private async Task<(MergeTargetResult Result, bool EnvironmentFailure)> ExecuteTargetAsync(
    MergePlan plan, MergeTargetPlan target, IOperationLog log, CancellationToken cancellationToken)
  {
    var request = plan.Request;
    var name = target.Target.Id;
    var started = DateTimeOffset.UtcNow;

    // 1: revalidação (leve para plano recente; completa para plano antigo)
    var light = _time.GetUtcNow() - plan.CreatedAt < LightRevalidationWindow;
    log.Step(name, light ? "Revalidando pending changes e arquivos graváveis" : "Revalidando condições críticas (plano com mais de 10 minutos)");
    MergeTargetPlan revalidated;
    try
    {
      revalidated = light
        ? await _planner.RevalidateLocalStateAsync(plan, target, log, cancellationToken)
        : await _planner.RevalidateAsync(plan, target, log, cancellationToken);
    }
    catch (OperationCanceledException)
    {
      return (Result(target, MergeTargetState.NotStarted, "Não iniciado: cancelado durante a revalidação."), false);
    }

    if (cancellationToken.IsCancellationRequested)
      return (Result(target, MergeTargetState.NotStarted, "Não iniciado: cancelado durante a revalidação."), false);

    if (revalidated.Readiness == TargetReadiness.AlreadyIntegrated)
      return (Result(target, MergeTargetState.AlreadyIntegrated, "Na revalidação o changeset não é mais candidato: já integrado. Nada foi reaplicado."), false);

    if (revalidated.Readiness == TargetReadiness.Blocked)
      return (Result(target, MergeTargetState.Blocked, "Bloqueado na revalidação: " + string.Join(" ", revalidated.Blockers)), plan.EnvironmentFailure);

    // 2: estado anterior = o mesmo tf status da revalidação (sem segunda consulta)
    var before = revalidated.PendingQuery;
    if (before is not { IsReliable: true })
    {
      return (Result(target, MergeTargetState.Blocked,
          $"Não foi possível registrar o estado anterior com confiança (tf: {before?.Result.Summary ?? "status não consultado"}). Merge não aplicado."),
        before?.Result.IsEnvironmentError ?? false);
    }

    // 3: merge
    log.Step(name, $"Aplicando merge do changeset {request.Changeset}");
    var merge = await _tfvc.MergeChangesetAsync(
      plan.SourceLocation.ServerPath, target.Location.ServerPath, request.Changeset, target.Location.LocalPath,
      line => log.Output(name, line), cancellationToken);
    log.ToolResult(name, "tf merge", merge.ExitCode, merge.StatusText, merge.Duration, merge.Output);

    // 4–6: inspeção (consultas pós-merge não usam o token: o estado sempre é inspecionado; em paralelo, só leitura)
    log.Step(name, "Inspecionando conflitos e pending changes");
    var conflictsTask = _tfvc.GetConflictsAsync(target.Location.LocalPath, CancellationToken.None);
    var afterTask = _tfvc.GetPendingChangesAsync(target.Location.LocalPath, CancellationToken.None);
    await Task.WhenAll(conflictsTask, afterTask);
    var conflicts = await conflictsTask;
    var after = await afterTask;
    log.ToolResult(name, "tf resolve /preview", conflicts.Result.ExitCode, conflicts.Result.StatusText, conflicts.Result.Duration, conflicts.Result.Output);
    log.ToolResult(name, "tf status (depois)", after.Result.ExitCode, after.Result.StatusText, after.Result.Duration, after.Result.Output);

    var newPending = after.IsReliable ? MergeOutcome.NewItems(before.Items, after.Items) : [];
    var conflictsReadable = conflicts.Result.Status is TfStatus.Success or TfStatus.PartialSuccess;
    var newConflicts = conflictsReadable
      ? MergeOutcome.NewItems(target.ConflictsOutOfScope.Concat(target.ConflictsInScope).ToList(), conflicts.Items)
      : [];

    var state = MergeOutcome.Classify(merge, newPending.Count, newConflicts.Count, previewHadItems: target.PreviewItemCount > 0);
    if (!after.IsReliable && state is MergeTargetState.AppliedWithPendingChanges or MergeTargetState.NoApplicableChanges)
      state = MergeTargetState.Indeterminate;
    // AutoMerge (exit 1): só é aplicado se a ausência de conflitos foi confirmada.
    if (merge.Status == TfStatus.PartialSuccess && !conflictsReadable && state == MergeTargetState.AppliedWithPendingChanges)
      state = MergeTargetState.Indeterminate;
    var conflictNote = merge.IsSuccess && !conflictsReadable && state == MergeTargetState.AppliedWithPendingChanges
      ? " Não foi possível confirmar a ausência de conflitos; verifique no Visual Studio."
      : string.Empty;

    var environmentFailure = merge.IsEnvironmentError || after.Result.IsEnvironmentError;
    var message = state switch
    {
      MergeTargetState.AppliedWithPendingChanges => $"{newPending.Count} nova(s) pending change(s). Revise antes do check-in.{conflictNote}",
      MergeTargetState.NoApplicableChanges => "O TFVC não gerou novas pending changes para este changeset.",
      MergeTargetState.AppliedWithConflicts => $"{newConflicts.Count} conflito(s). Resolva no Visual Studio; nenhuma resolução foi escolhida pelo CLI.",
      MergeTargetState.Cancelled => "Cancelado durante o merge. O destino pode ter alterações parciais: inspecione com 'pep pending list'. Cancelamento não é rollback.",
      MergeTargetState.Indeterminate => $"Resultado não pôde ser confirmado (tf: {merge.StatusText}). Inspecione pending changes e conflitos antes de continuar.",
      _ => $"tf merge falhou: {merge.Summary} {TfErrorClassifier.Guidance(merge)}",
    };

    return (new MergeTargetResult(target.Target, state, message, newPending, newConflicts, DateTimeOffset.UtcNow - started), environmentFailure);
  }

  private static MergeTargetResult Result(MergeTargetPlan target, MergeTargetState state, string message) =>
    new(target.Target, state, message, [], [], null);
}
