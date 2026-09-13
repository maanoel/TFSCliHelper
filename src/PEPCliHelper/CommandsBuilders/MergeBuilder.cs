using PEPCliHelper.Core.Catalog;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.Configuration;
using PEPCliHelper.Core.Get;
using PEPCliHelper.Core.History;
using PEPCliHelper.Core.Merge;
using PEPCliHelper.Infrastructure;
using PEPCliHelper.Presentation;

namespace PEPCliHelper.CommandsBuilders;

/// <summary>
/// pep merge (specs 006, 007 e 013). Coleta a entrada (argumentos ou prompts), monta o plano,
/// confirma e executa. Toda regra de segurança está no Core (MergePlanner/MergeExecutor).
/// </summary>
public sealed class MergeBuilder : CommandBuilderBase
{
  public MergeBuilder(AppServices services)
    : base(services)
  {
  }

  public override async Task<int> BuildAsync(CommandLine line, CancellationToken cancellationToken)
  {
    var config = Services.RequireConfig();
    var catalog = Services.RequireCatalog();
    var legacySyntax = line.Positionals.Count > 0;
    var dryRun = line.Flag("dry-run");
    var stopOnFailure = line.Flag("stop-on-failure");

    if (stopOnFailure && line.Flag("continue-on-failure"))
    {
      throw new UsageException(
        "Use --stop-on-failure ou --continue-on-failure, não os dois.",
        "Continuar nos demais destinos já é o padrão; informe --stop-on-failure apenas para interromper após falha, conflito ou bloqueio.");
    }

    if (legacySyntax && line.Positionals.Count != 3)
    {
      throw new UsageException(
        $"Sintaxe antiga incompleta: 'merge {string.Join(' ', line.Positionals)}'. Formato antigo: merge <projeto> <versao> <changeset>.",
        "Use: pep merge --project back --source atual --target 2606 --changeset 861799");
    }

    if (legacySyntax && (line.Option("project") is not null || line.Option("source") is not null || line.Option("changeset") is not null))
      throw new UsageException("Não misture a sintaxe antiga (posicional) com --project, --source ou --changeset.", "Use apenas a sintaxe nova com opções.");

    var projectToken = legacySyntax ? line.Positional(0) : line.Option("project");
    var sourceToken = legacySyntax ? line.Positional(1) : line.Option("source");
    var changeset = legacySyntax ? CommandLine.ParsePositiveInt(line.Positional(2)!, "changeset") : line.IntOption("changeset");

    if (legacySyntax)
    {
      Ui.Warn("Sintaxe antiga detectada: a versão informada agora é a ORIGEM e os destinos precisam ser escolhidos explicitamente.");
      Ui.Muted($"  Sintaxe nova: pep merge --project {projectToken} --source {sourceToken} --target <versao> --changeset {changeset}");
    }

    if (!Ui.Json && !legacySyntax)
      Ui.Title("Merge de changeset");

    // Projeto
    var project = projectToken is not null
      ? ResolveProject(catalog, projectToken)
      : Ui.CanPrompt
        ? await Ui.SelectOrCancelAsync("Projeto", catalog.Projects, p => $"{p.Alias}  ({p.Name})", cancellationToken)
        : throw MissingInput("o projeto (--project)");

    // Origem
    var source = sourceToken is not null
      ? ResolveVersion(catalog, sourceToken, "de origem")
      : Ui.CanPrompt
        ? await Ui.SelectOrCancelAsync("Versão de origem", catalog.Active, v => v.Label, cancellationToken)
        : throw MissingInput("a versão de origem (--source)");

    // Destinos
    var targets = await ResolveTargetsAsync(line, catalog, source, legacySyntax, cancellationToken);

    // Changeset
    changeset ??= Ui.CanPrompt
      ? CommandLine.ParsePositiveInt(await Ui.AskAsync("Changeset de origem (número)", null, ValidateChangeset, cancellationToken), "changeset")
      : throw MissingInput("o changeset (--changeset)");

    var errors = MergeRequest.Validate(project, source, targets, changeset);
    if (errors.Count > 0)
      throw new UsageException(string.Join(" ", errors), $"Uso: {line.Help.Usage}");

    var request = new MergeRequest(project, source, targets, changeset.Value, stopOnFailure);
    var history = BeginHistory("merge", line);
    history.Describe(project.Alias, source.Id, changeset, targets.Select(t => t.Id));

    try
    {
      var exitCode = await RunAsync(request, config, catalog, dryRun, history, cancellationToken);
      ReportHistory(history);
      return exitCode;
    }
    catch (OperationCanceledException)
    {
      history.Complete(ExitCodes.Cancelled, "cancelado", recovery: ["Cancelamento não é rollback. Inspecione 'pep pending list' antes de repetir."]);
      throw;
    }
  }

  private async Task<int> RunAsync(MergeRequest request, PepConfig config, VersionCatalog catalog, bool dryRun, ExecutionSession history, CancellationToken cancellationToken)
  {
    var planner = Services.MergePlanner;
    var plan = await PlanAsync(planner, request, config, catalog, history, cancellationToken);

    if (dryRun)
    {
      Output(plan, dryRun: true, result: null);
      history.Complete(plan.DryRunExitCode, plan.DryRunExitCode == 0 ? "dry-run ok" : "dry-run com bloqueios",
        plan.Targets.Select(t => new TargetOutcome { Target = t.Target.Id, State = t.Readiness.ToString(), Message = string.Join(" ", t.Blockers.Concat(t.Notes)) }));
      return plan.DryRunExitCode;
    }

    if (!Ui.Json)
      PlanRenderer.MergePlan(Ui, plan, dryRun: false);

    // Atualização local: oferece get com confirmação, nunca silencioso.
    var outdated = plan.Targets.Where(t => t.Readiness == TargetReadiness.Ready && t.Outdated == true).ToList();
    if (outdated.Count > 0 && Ui.CanPrompt && !Services.Options.Yes)
    {
      var choice = await Ui.SelectAsync(
        $"{outdated.Count} destino(s) desatualizado(s). O que deseja fazer?",
        ["Executar get nesses destinos e revalidar", "Continuar sem get", "Cancelar"],
        s => s,
        cancellationToken);

      if (choice == "Cancelar")
        return Cancelled(history);

      if (choice.StartsWith("Executar", StringComparison.Ordinal))
      {
        var getExit = await RunGetAsync(outdated.Select(t => t.Location).ToList(), history, cancellationToken);
        if (getExit != ExitCodes.Success)
        {
          Ui.Fail("O get não terminou limpo. O merge não continuará automaticamente; revise o destino e execute o merge novamente.");
          history.Complete(ExitCodes.Precondition, "bloqueado", recovery: ["Revise o resultado do get e repita o merge com --dry-run."]);
          return ExitCodes.Precondition;
        }

        plan = await PlanAsync(planner, request, config, catalog, history, cancellationToken);
        PlanRenderer.MergePlan(Ui, plan, dryRun: false);
      }
    }

    if (!plan.CanExecute)
    {
      var allIntegrated = plan.GlobalBlockers.Count == 0 && plan.Targets.All(t => t.Readiness == TargetReadiness.AlreadyIntegrated);
      var code = allIntegrated ? ExitCodes.Success : ExitCodes.Precondition;
      if (Ui.Json)
        Output(plan, dryRun: false, result: null);
      else if (allIntegrated)
        Ui.Info("Todos os destinos já possuem o changeset integrado. Nada foi executado.");
      else
        Ui.Fail("Nenhum destino está pronto. Nada foi executado; corrija os impedimentos acima e tente novamente.");

      history.Complete(code, allIntegrated ? "ja integrado" : "bloqueado",
        plan.Targets.Select(t => new TargetOutcome { Target = t.Target.Id, State = t.Readiness.ToString(), Message = string.Join(" ", t.Blockers.Concat(t.Notes)) }));
      return code;
    }

    if (plan.HasBlockedTargets)
    {
      var blocked = string.Join(", ", plan.Targets.Where(t => t.Readiness == TargetReadiness.Blocked).Select(t => t.Target.Id));
      if (Ui.CanPrompt && !Services.Options.Yes)
      {
        if (!await Ui.ConfirmAsync($"Destinos bloqueados ({blocked}) serão ignorados. Continuar apenas com os prontos?", false, cancellationToken))
          return Cancelled(history);
      }
      else if (request.StopOnFailure)
      {
        var error = new PreconditionException(
          $"Destinos bloqueados na pré-verificação: {blocked}. Nada foi executado (--stop-on-failure).",
          "Corrija os impedimentos ou repita sem --stop-on-failure para executar somente os destinos prontos.");
        if (!Ui.Json)
          throw error;
        Output(plan, dryRun: false, result: null);
        history.Complete(ExitCodes.Precondition, "bloqueado");
        return ExitCodes.Precondition;
      }
      else if (!Ui.Json)
      {
        Ui.Warn($"Destinos bloqueados ({blocked}) serão ignorados; o merge segue somente nos prontos: {string.Join(", ", plan.ReadyTargets.Select(t => t.Target.Id))}.");
      }
    }

    if (!Ui.Json)
    {
      Ui.Blank();
      Ui.PendingChangesNotice(PlanRenderer.MergeConfirmation);
    }

    if (!await ConfirmAsync($"Aplicar o merge do C{request.Changeset} em {string.Join(", ", plan.ReadyTargets.Select(t => t.Target.Id))}?", cancellationToken))
      return Cancelled(history);

    var executor = Services.MergeExecutor(planner);
    var result = await Ui.WithStatusAsync("Aplicando merge", sink =>
      executor.ExecuteAsync(plan, new ConsoleOperationLog(sink, history, Ui.Theme), cancellationToken));

    Output(plan, dryRun: false, result);
    history.Complete(result.ExitCode, result.Status,
      result.Targets.Select(t => new TargetOutcome { Target = t.Target.Id, State = t.State.ToString(), Message = t.Message }),
      RecoveryHints(result));
    return result.ExitCode;
  }

  private Task<MergePlan> PlanAsync(MergePlanner planner, MergeRequest request, PepConfig config, VersionCatalog catalog, ExecutionSession history, CancellationToken cancellationToken) =>
    Ui.WithStatusAsync("Pré-verificando merge", sink =>
      planner.PlanAsync(request, catalog, config.Collection, new ConsoleOperationLog(sink, history, Ui.Theme), cancellationToken));

  private async Task<int> RunGetAsync(IReadOnlyList<ProjectLocation> locations, ExecutionSession history, CancellationToken cancellationToken)
  {
    var service = Services.GetService;
    var getPlan = await Ui.WithStatusAsync("Preparando get", sink => service.PlanAsync(locations, new ConsoleOperationLog(sink, history, Ui.Theme), cancellationToken));
    PlanRenderer.GetPlan(Ui, getPlan, dryRun: false);
    if (!getPlan.CanExecute)
      return ExitCodes.Precondition;

    var result = await Ui.WithStatusAsync("Executando get", sink => service.ExecuteAsync(getPlan, new ConsoleOperationLog(sink, history, Ui.Theme), cancellationToken));
    PlanRenderer.GetResult(Ui, result);
    return result.ExitCode;
  }

  private async Task<IReadOnlyList<VersionEntry>> ResolveTargetsAsync(CommandLine line, VersionCatalog catalog, VersionEntry source, bool legacySyntax, CancellationToken cancellationToken)
  {
    var tokens = line.OptionValues("target");
    var allLegacy = line.Flag("all-legacy");

    if (allLegacy && tokens.Count > 0)
      throw new UsageException("Use --target ou --all-legacy, não os dois.", "Ex.: pep merge --project back --source atual --all-legacy --changeset 861799");

    if (allLegacy)
    {
      var legacy = catalog.ActiveLegacy.Where(v => !v.Id.Equals(source.Id, StringComparison.OrdinalIgnoreCase)).ToList();
      if (legacy.Count == 0)
        throw new PreconditionException("Não há versões legadas ativas no catálogo além da origem.", "Configure legadas com 'pep env configure'.");
      return legacy;
    }

    if (tokens.Count > 0)
    {
      return tokens
        .Select(t => ResolveVersion(catalog, t, "de destino"))
        .DistinctBy(v => v.Id, StringComparer.OrdinalIgnoreCase)
        .ToList();
    }

    if (!Ui.CanPrompt)
    {
      throw new UsageException(
        legacySyntax
          ? "A sintaxe antiga não assume mais 'todas as outras versões': em modo não interativo os destinos são obrigatórios."
          : "Informe os destinos (--target <versao>, repetível) ou --all-legacy.",
        $"Ex.: pep merge --project {line.Positional(0) ?? line.Option("project") ?? "back"} --source {source.Id} --all-legacy --changeset <id>");
    }

    var options = catalog.Active.Where(v => !v.Id.Equals(source.Id, StringComparison.OrdinalIgnoreCase)).ToList();
    if (options.Count == 0)
      throw new PreconditionException("Não há outras versões ativas para usar como destino.", "Configure legadas com 'pep env configure'.");

    var selected = await Ui.MultiSelectAsync("Destinos do merge", options, v => v.Label, cancellationToken);
    if (selected.Count == 0)
      throw new OperationCanceledException("Nenhum destino selecionado.");
    return selected;
  }

  private void Output(MergePlan plan, bool dryRun, MergeExecutionResult? result)
  {
    if (Ui.Json)
    {
      Ui.WriteJson(new
      {
        plano = JsonViews.MergePlan(plan, dryRun),
        resultado = result is null ? null : JsonViews.MergeResult(result),
        checkInRealizado = false,
      });
      return;
    }

    if (dryRun)
      PlanRenderer.MergePlan(Ui, plan, dryRun: true);
    else if (result is not null)
      PlanRenderer.MergeResult(Ui, plan, result);
  }

  private int Cancelled(ExecutionSession history)
  {
    Ui.Warn("Merge cancelado antes da execução. Nada foi alterado.");
    history.Complete(ExitCodes.Cancelled, "cancelado");
    return ExitCodes.Cancelled;
  }

  private static IEnumerable<string> RecoveryHints(MergeExecutionResult result)
  {
    yield return "Nenhum check-in foi realizado.";
    if (result.Targets.Any(t => t.State == MergeTargetState.AppliedWithConflicts))
      yield return "Resolva os conflitos no Visual Studio antes do check-in.";
    if (result.Targets.Any(t => t.State is MergeTargetState.Indeterminate or MergeTargetState.Cancelled))
      yield return "Inspecione pending changes e conflitos dos destinos indeterminados/cancelados antes de repetir.";
    if (result.Targets.Any(t => t.State is MergeTargetState.NotStarted or MergeTargetState.Blocked))
      yield return "Para retomar, execute o merge com --dry-run: destinos já integrados não são reaplicados.";
  }

  private static string? ValidateChangeset(string value)
  {
    try
    {
      CommandLine.ParsePositiveInt(value, "changeset");
      return null;
    }
    catch (UsageException ex)
    {
      return ex.Message;
    }
  }

  private static UsageException MissingInput(string what) =>
    new($"Informe {what}.",
      "Em modo não interativo todos os parâmetros são obrigatórios. Ex.: pep merge --project back --source atual --target 2606 --changeset 861799");
}
