using PEPCliHelper.Core.Catalog;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.FileSystem;
using PEPCliHelper.Core.Tfvc;

namespace PEPCliHelper.Core.Get;

public enum GetTargetState
{
  Updated,
  UpToDate,
  Partial,
  Failed,
  Blocked,
  NotStarted,
  Cancelled,
}

public sealed class GetTargetPlan
{
  public required ProjectLocation Location { get; init; }

  public MappingCheck? Mapping { get; set; }

  public List<string> Blockers { get; } = [];

  public List<string> Warnings { get; } = [];

  public int? PendingCount { get; set; }

  public bool? Outdated { get; set; }

  public bool IsReady => Blockers.Count == 0;
}

public sealed class GetPlan
{
  public List<GetTargetPlan> Targets { get; } = [];

  public bool EnvironmentFailure { get; set; }

  public bool CanExecute => !EnvironmentFailure && Targets.Any(t => t.IsReady);

  public int DryRunExitCode => EnvironmentFailure || Targets.Any(t => !t.IsReady) ? ExitCodes.Precondition : ExitCodes.Success;
}

public sealed record GetTargetResult(ProjectLocation Location, GetTargetState State, string Message, TimeSpan? Duration)
{
  public static string Describe(GetTargetState state) => state switch
  {
    GetTargetState.Updated => "Atualizado",
    GetTargetState.UpToDate => "Já atualizado",
    GetTargetState.Partial => "Parcial — revisar",
    GetTargetState.Failed => "Falhou",
    GetTargetState.Blocked => "Bloqueado",
    GetTargetState.NotStarted => "Não iniciado",
    GetTargetState.Cancelled => "Cancelado",
    _ => state.ToString(),
  };
}

public sealed record GetExecutionResult(IReadOnlyList<GetTargetResult> Targets, bool CancellationRequested = false)
{
  public int ExitCode => CancellationRequested ? ExitCodes.Cancelled : ComputeExitCode(Targets);

  public static int ComputeExitCode(IReadOnlyList<GetTargetResult> targets)
  {
    if (targets.Any(t => t.State == GetTargetState.Cancelled))
      return ExitCodes.Cancelled;

    static bool IsOk(GetTargetState s) => s is GetTargetState.Updated or GetTargetState.UpToDate;

    if (targets.Count > 0 && targets.All(t => IsOk(t.State)))
      return ExitCodes.Success;
    if (targets.Any(t => t.State == GetTargetState.Partial) || targets.Any(t => IsOk(t.State)))
      return ExitCodes.ConflictOrPartial;
    if (targets.Any(t => t.State == GetTargetState.Failed))
      return ExitCodes.OperationFailed;
    return ExitCodes.Precondition;
  }
}

/// <summary>Get seguro (spec 008): valida, mostra plano, nunca usa /force ou /overwrite.</summary>
public sealed class GetService
{
  private readonly ITfvcClient _tfvc;
  private readonly MappingService _mapping;

  public GetService(ITfvcClient tfvc, IFileSystem fileSystem)
  {
    _tfvc = tfvc;
    _mapping = new MappingService(tfvc, fileSystem);
  }

  public async Task<GetPlan> PlanAsync(IReadOnlyList<ProjectLocation> locations, IOperationLog log, CancellationToken cancellationToken)
  {
    var plan = new GetPlan();
    foreach (var location in locations)
    {
      cancellationToken.ThrowIfCancellationRequested();
      var target = new GetTargetPlan { Location = location };
      plan.Targets.Add(target);

      if (plan.EnvironmentFailure)
      {
        target.Blockers.Add("Não avaliado: falha de rede/autenticação anterior.");
        continue;
      }

      var (mapping, query) = await _mapping.CheckAsync(location.LocalPath, location.ServerPath, location.Label, log, cancellationToken);
      target.Mapping = mapping;
      if (query is { IsEnvironmentError: true })
        plan.EnvironmentFailure = true;

      if (!mapping.IsValid)
      {
        target.Blockers.Add($"{mapping.Message} {mapping.Guidance}".Trim());
        continue;
      }

      log.Step(location.Label, "Verificando pending changes");
      var pending = await _tfvc.GetPendingChangesAsync(location.LocalPath, cancellationToken);
      log.ToolResult(location.Label, "tf status", pending.Result.ExitCode, pending.Result.StatusText, pending.Result.Duration, pending.Result.Output);
      if (pending.Result.IsSuccess)
      {
        target.PendingCount = pending.Items.Count;
        if (pending.Items.Count > 0)
          target.Warnings.Add($"{pending.Items.Count} pending change(s) locais. O get não as sobrescreve e pode gerar conflitos para revisão.");
      }
      else if (pending.Result.IsEnvironmentError)
      {
        plan.EnvironmentFailure = true;
        target.Blockers.Add($"Falha de acesso ao TFVC: {pending.Result.StatusText}. {TfErrorClassifier.Guidance(pending.Result)}");
        continue;
      }
      else
      {
        target.Warnings.Add($"Não foi possível listar pending changes (tf: {pending.Result.Summary}).");
      }

      log.Step(location.Label, "Get /preview");
      var preview = await _tfvc.PreviewGetAsync(location.LocalPath, cancellationToken);
      log.ToolResult(location.Label, "tf get /preview", preview.Result.ExitCode, preview.Result.StatusText, preview.Result.Duration, preview.Result.Output);
      if (preview.Result.IsSuccess)
      {
        target.Outdated = preview.Items.Count > 0;
      }
      else if (preview.Result.IsEnvironmentError)
      {
        plan.EnvironmentFailure = true;
        target.Blockers.Add($"Falha de acesso ao TFVC: {preview.Result.StatusText}. {TfErrorClassifier.Guidance(preview.Result)}");
      }
      else
      {
        target.Warnings.Add($"Preview do get não confirmou o escopo (tf: {preview.Result.Summary}).");
      }
    }

    return plan;
  }

  public async Task<GetExecutionResult> ExecuteAsync(GetPlan plan, IOperationLog log, CancellationToken cancellationToken)
  {
    var results = new List<GetTargetResult>();
    var stop = false;

    foreach (var target in plan.Targets)
    {
      if (!target.IsReady)
      {
        results.Add(new(target.Location, GetTargetState.Blocked, string.Join(" ", target.Blockers), null));
        continue;
      }

      if (stop || cancellationToken.IsCancellationRequested)
      {
        results.Add(new(target.Location, GetTargetState.NotStarted, "Não iniciado: execução interrompida.", null));
        stop = true;
        continue;
      }

      log.Step(target.Location.Label, "Executando get");
      var result = await _tfvc.GetLatestAsync(target.Location.LocalPath, line => log.Output(target.Location.Label, line), cancellationToken);
      log.ToolResult(target.Location.Label, "tf get", result.ExitCode, result.StatusText, result.Duration, result.Output);

      var (state, message) = result.Status switch
      {
        TfStatus.Success when target.Outdated == false => (GetTargetState.UpToDate, "Nenhum arquivo a atualizar."),
        TfStatus.Success => (GetTargetState.Updated, "Atualizado."),
        TfStatus.PartialSuccess => (GetTargetState.Partial, "Get concluído parcialmente (conflitos ou arquivos graváveis não substituídos). Revise com 'pep pending list' e no Visual Studio."),
        TfStatus.Cancelled => (GetTargetState.Cancelled, "Cancelado. Arquivos já baixados permanecem; execute o get novamente para completar."),
        _ => (GetTargetState.Failed, $"{result.Summary} {TfErrorClassifier.Guidance(result)}"),
      };

      results.Add(new(target.Location, state, message, result.Duration));
      stop = state == GetTargetState.Cancelled || result.IsEnvironmentError;
    }

    return new GetExecutionResult(results, cancellationToken.IsCancellationRequested);
  }
}
