using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.History;
using PEPCliHelper.Infrastructure;
using PEPCliHelper.Presentation;

namespace PEPCliHelper.CommandsBuilders;

/// <summary>pep build all | pep build version &lt;versao&gt; (spec 009).</summary>
public sealed class BuildBuilder : CommandBuilderBase
{
  private readonly bool _all;

  public BuildBuilder(AppServices services, bool all)
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
      : [ResolveVersion(catalog, RequireValue(line.Positional(0), "a versão (ex.: pep build version 2606)", line), "")];

    var locations = catalog.LocateAll(versions, project);
    var history = BeginHistory("build", line);
    history.Describe(project?.Alias, versions: versions.Select(v => v.Id));

    var service = Services.BuildService;
    var plan = await Ui.WithStatusAsync("Preparando build", _ => service.PlanAsync(locations, cancellationToken));

    if (line.Flag("dry-run"))
    {
      if (Ui.Json)
        Ui.WriteJson(new { dryRun = true, plano = JsonViews.BuildPlan(plan) });
      else
        PlanRenderer.BuildPlan(Ui, plan, dryRun: true);
      history.Complete(plan.DryRunExitCode, "dry-run");
      return plan.DryRunExitCode;
    }

    if (!Ui.Json)
      PlanRenderer.BuildPlan(Ui, plan, dryRun: false);

    if (!plan.CanExecute)
    {
      if (Ui.Json)
        Ui.WriteJson(new { plano = JsonViews.BuildPlan(plan), resultado = (object?)null });
      else
        Ui.Fail("Nenhuma solução pronta para build. Nada foi executado.");
      history.Complete(ExitCodes.Precondition, "bloqueado");
      return ExitCodes.Precondition;
    }

    if (!await ConfirmAsync($"Compilar {plan.Targets.Count(t => t.IsReady)} solução(ões) em sequência?", cancellationToken))
    {
      Ui.Warn("Build cancelado. Nada foi executado.");
      history.Complete(ExitCodes.Cancelled, "cancelado");
      return ExitCodes.Cancelled;
    }

    var result = await Ui.WithStatusAsync("Compilando", sink =>
      service.ExecuteAsync(plan, line.Option("configuration"), line.Flag("continue-on-failure"),
        new ConsoleOperationLog(sink, history, Ui.Theme), cancellationToken));

    if (Ui.Json)
      Ui.WriteJson(new { plano = JsonViews.BuildPlan(plan), resultado = JsonViews.BuildResult(result) });
    else
      PlanRenderer.BuildResult(Ui, result);

    history.Complete(result.ExitCode, ExitCodes.Describe(result.ExitCode),
      result.Targets.Select(t => new TargetOutcome { Target = t.Location.Label, State = t.State.ToString(), Message = t.Message }));
    ReportHistory(history);
    return result.ExitCode;
  }
}
