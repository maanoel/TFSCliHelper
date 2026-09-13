using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.History;
using PEPCliHelper.Infrastructure;
using PEPCliHelper.Presentation;

namespace PEPCliHelper.CommandsBuilders;

/// <summary>pep get all | pep get version &lt;versao&gt; (spec 008).</summary>
public sealed class GetBuilder : CommandBuilderBase
{
  private readonly bool _all;

  public GetBuilder(AppServices services, bool all)
    : base(services)
  {
    _all = all;
  }

  public override async Task<int> BuildAsync(CommandLine line, CancellationToken cancellationToken)
  {
    var catalog = Services.RequireCatalog();
    var project = OptionalProject(catalog, line.Option("project"));
    var versions = _all
      ? catalog.Active
      : [ResolveVersion(catalog, RequireValue(line.Positional(0), "a versão (ex.: pep get version 2606)", line), "")];

    var locations = catalog.LocateAll(versions, project);
    var dryRun = line.Flag("dry-run");
    var history = BeginHistory("get", line);
    history.Describe(project?.Alias, versions: versions.Select(v => v.Id));

    var service = Services.GetService;
    var plan = await Ui.WithStatusAsync("Preparando get", sink =>
      service.PlanAsync(locations, new ConsoleOperationLog(sink, history, Ui.Theme), cancellationToken));

    if (dryRun)
    {
      if (Ui.Json)
        Ui.WriteJson(new { dryRun = true, plano = JsonViews.GetPlan(plan) });
      else
        PlanRenderer.GetPlan(Ui, plan, dryRun: true);
      history.Complete(plan.DryRunExitCode, "dry-run");
      ReportHistory(history);
      return plan.DryRunExitCode;
    }

    if (!Ui.Json)
      PlanRenderer.GetPlan(Ui, plan, dryRun: false);

    if (!plan.CanExecute)
    {
      if (Ui.Json)
        Ui.WriteJson(new { plano = JsonViews.GetPlan(plan), resultado = (object?)null });
      else
        Ui.Fail("Nenhum alvo pronto para get. Nada foi executado.");
      history.Complete(ExitCodes.Precondition, "bloqueado");
      return ExitCodes.Precondition;
    }

    if (!await ConfirmAsync($"Executar get em {plan.Targets.Count(t => t.IsReady)} alvo(s)?", cancellationToken))
    {
      Ui.Warn("Get cancelado. Nada foi alterado.");
      history.Complete(ExitCodes.Cancelled, "cancelado");
      return ExitCodes.Cancelled;
    }

    var result = await Ui.WithStatusAsync("Executando get", sink =>
      service.ExecuteAsync(plan, new ConsoleOperationLog(sink, history, Ui.Theme), cancellationToken));

    if (Ui.Json)
      Ui.WriteJson(new { plano = JsonViews.GetPlan(plan), resultado = JsonViews.GetResult(result) });
    else
      PlanRenderer.GetResult(Ui, result);

    history.Complete(result.ExitCode, ExitCodes.Describe(result.ExitCode),
      result.Targets.Select(t => new TargetOutcome { Target = t.Location.Label, State = t.State.ToString(), Message = t.Message }));
    ReportHistory(history);
    return result.ExitCode;
  }
}
