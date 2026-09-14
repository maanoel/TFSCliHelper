using PEPCliHelper.Core.Build;
using PEPCliHelper.Core.Catalog;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.History;
using PEPCliHelper.Core.LocalTools;
using PEPCliHelper.Infrastructure;
using PEPCliHelper.Presentation;

namespace PEPCliHelper.CommandsBuilders;

public enum BuildScope
{
  /// <summary>pep build: versões por --version/--all ou seleção interativa.</summary>
  Selection,

  /// <summary>pep build all</summary>
  All,

  /// <summary>pep build version &lt;versao&gt;</summary>
  Version,
}

/// <summary>pep build | pep build all | pep build version &lt;versao&gt; (spec 009). Um único fluxo: seleção → plano → execução (sem confirmação).</summary>
public sealed class BuildBuilder : CommandBuilderBase
{
  private readonly BuildScope _scope;

  public BuildBuilder(AppServices services, BuildScope scope)
    : base(services)
  {
    _scope = scope;
  }

  public override async Task<int> BuildAsync(CommandLine line, CancellationToken cancellationToken)
  {
    var catalog = Services.RequireCatalog();
    var projects = line.OptionValues("project").Select(alias => ResolveProject(catalog, alias)).ToList();

    var (versions, prompted) = await ResolveVersionsAsync(catalog, line, cancellationToken);
    if (prompted && projects.Count == 0)
      projects = await SelectProjectsAsync(catalog, cancellationToken);

    var locations = BuildOrder.Arrange(catalog, versions, projects);
    var history = BeginHistory("build", line);
    history.Describe(projects.Count == 0 ? null : string.Join(",", projects.Select(p => p.Alias)), versions: versions.Select(v => v.Id));

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

    // Sempre antes do build: encerra o RM.Host das versões (mesma rotina de 'pep kill host', graciosa).
    IReadOnlyList<TerminationResult> stoppedHosts = plan.CanExecute && plan.Targets.Any(t => t.HostsToStop.Count > 0)
      ? await Ui.WithStatusAsync("Encerrando RM.Host", _ => Task.Run(() => service.StopHosts(plan), cancellationToken))
      : [];
    foreach (var stop in stoppedHosts)
    {
      history.Step($"kill host PID {stop.Candidate.Process.Pid}", stop.Message);
      if (!Ui.Json)
        Ui.Markup($"  {Ui.Theme.State(stop.Terminated ? StateKind.Ok : StateKind.Warn, $"RM.Host PID {stop.Candidate.Process.Pid}")} {Ui.Escape(stop.Candidate.VersionLabel)} · {Ui.Escape(stop.Message)}");
    }

    if (!plan.CanExecute)
    {
      if (Ui.Json)
        Ui.WriteJson(new { plano = JsonViews.BuildPlan(plan), hostsEncerrados = JsonViews.StoppedHosts(stoppedHosts), resultado = (object?)null });
      else
        Ui.Fail("Nenhuma solução pronta para build. Nada foi executado.");
      history.Complete(ExitCodes.Precondition, "bloqueado");
      return ExitCodes.Precondition;
    }

    // Sem confirmação: build não altera o TFVC nem descarta nada; Ctrl+C interrompe.
    var result = await Ui.WithStatusAsync("Compilando", sink =>
      service.ExecuteAsync(plan, line.Option("configuration"), line.Flag("continue-on-failure"),
        new ConsoleOperationLog(sink, history, Ui.Theme), cancellationToken));

    if (Ui.Json)
      Ui.WriteJson(new { plano = JsonViews.BuildPlan(plan), hostsEncerrados = JsonViews.StoppedHosts(stoppedHosts), resultado = JsonViews.BuildResult(result) });
    else
      PlanRenderer.BuildResult(Ui, result);

    history.Complete(result.ExitCode, ExitCodes.Describe(result.ExitCode),
      result.Targets.Select(t => new TargetOutcome { Target = t.Location.Label, State = t.State.ToString(), Message = t.Message }));
    ReportHistory(history);
    return result.ExitCode;
  }

  private async Task<(IReadOnlyList<VersionEntry> Versions, bool Prompted)> ResolveVersionsAsync(VersionCatalog catalog, CommandLine line, CancellationToken cancellationToken)
  {
    switch (_scope)
    {
      case BuildScope.All:
        return (catalog.Active, false);

      case BuildScope.Version:
        return ([ResolveVersion(catalog, RequireValue(line.Positional(0), "a versão (ex.: pep build version 2606)", line), "")], false);
    }

    var tokens = line.OptionValues("version");
    var all = line.Flag("all");
    if (all && tokens.Count > 0)
      throw new UsageException("Use --all ou --version, não os dois.", "Ex.: pep build --all  ou  pep build --version 2606 --version 2602");

    if (all)
      return (catalog.Active, false);
    if (tokens.Count > 0)
      return (tokens.Select(token => ResolveVersion(catalog, token, "")).ToList(), false);

    if (!Ui.CanPrompt)
    {
      throw new UsageException(
        "Informe as versões do build (--version <versao>, repetível) ou --all.",
        "Ex.: pep build --version 2606 --project back --dry-run  ou  pep build --all");
    }

    var selected = await Ui.MultiSelectAsync("Build de quais versões?", catalog.Active, v => v.Label, cancellationToken,
      catalog.Current is null ? null : [catalog.Current]);
    if (selected.Count == 0)
      throw new OperationCanceledException("Nenhuma versão selecionada.");
    return (selected, true);
  }

  private async Task<List<ProjectDefinition>> SelectProjectsAsync(VersionCatalog catalog, CancellationToken cancellationToken)
  {
    var ordered = catalog.Projects; // PEP (principal) sempre primeiro.
    // Sau-Saúde vem desmarcado: compilá-lo é exceção e o usuário marca quando precisar.
    var preselected = ordered.Where(p => !IsSaude(p)).ToList();
    var selected = await Ui.MultiSelectAsync("Quais projetos compilar?", ordered, ProjectLabel, cancellationToken, preselected);
    if (selected.Count == 0)
      throw new OperationCanceledException("Nenhum projeto selecionado.");
    return selected;
  }

  private static bool IsSaude(ProjectDefinition project) =>
    project.Alias.Equals("sau", StringComparison.OrdinalIgnoreCase)
    || project.Name.Equals("Sau-Saude", StringComparison.OrdinalIgnoreCase);

  private static string ProjectLabel(ProjectDefinition project) =>
    $"{project.Alias} · {project.Name} ({project.Solution ?? "sem solução"})" + (project.Principal ? " — principal, compila primeiro" : string.Empty);
}
