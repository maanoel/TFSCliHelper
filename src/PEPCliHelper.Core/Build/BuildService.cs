using PEPCliHelper.Core.Catalog;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.Execution;
using PEPCliHelper.Core.FileSystem;
using PEPCliHelper.Core.LocalTools;
using PEPCliHelper.Core.Processes;

namespace PEPCliHelper.Core.Build;

public enum BuildTargetState
{
  Succeeded,
  Failed,
  Blocked,
  NotStarted,
  Cancelled,
}

public sealed class BuildTargetPlan
{
  public required ProjectLocation Location { get; init; }

  public string? Solution => Location.SolutionPath;

  public List<string> Blockers { get; } = [];

  public List<string> Warnings { get; } = [];

  /// <summary>RM.Host em execução a partir da versão; encerrados por <see cref="BuildService.StopHosts"/> antes do build.</summary>
  public List<ProcessInfo> HostsToStop { get; } = [];

  public bool IsReady => Blockers.Count == 0;
}

public sealed class BuildPlan
{
  public ToolInfo? MsBuild { get; set; }

  public List<string> GlobalBlockers { get; } = [];

  public List<BuildTargetPlan> Targets { get; } = [];

  public bool CanExecute => GlobalBlockers.Count == 0 && Targets.Any(t => t.IsReady);

  public int DryRunExitCode => GlobalBlockers.Count > 0 || Targets.Any(t => !t.IsReady) ? ExitCodes.Precondition : ExitCodes.Success;
}

public sealed record BuildTargetResult(ProjectLocation Location, BuildTargetState State, string Message, TimeSpan? Duration)
{
  public static string Describe(BuildTargetState state) => state switch
  {
    BuildTargetState.Succeeded => "Sucesso",
    BuildTargetState.Failed => "Falhou",
    BuildTargetState.Blocked => "Bloqueado",
    BuildTargetState.NotStarted => "Não iniciado",
    BuildTargetState.Cancelled => "Cancelado",
    _ => state.ToString(),
  };
}

public sealed record BuildExecutionResult(IReadOnlyList<BuildTargetResult> Targets, bool CancellationRequested = false)
{
  public int ExitCode
  {
    get
    {
      if (CancellationRequested || Targets.Any(t => t.State == BuildTargetState.Cancelled))
        return ExitCodes.Cancelled;
      if (Targets.Count > 0 && Targets.All(t => t.State == BuildTargetState.Succeeded))
        return ExitCodes.Success;
      var anySuccess = Targets.Any(t => t.State == BuildTargetState.Succeeded);
      if (anySuccess)
        return ExitCodes.ConflictOrPartial;
      if (Targets.Any(t => t.State == BuildTargetState.Failed))
        return ExitCodes.OperationFailed;
      return ExitCodes.Precondition;
    }
  }
}

/// <summary>Build com MSBuild (spec 009). Sequencial, sem get; encerra o RM.Host da versão antes (como pep kill host).</summary>
public sealed class BuildService
{
  private readonly ICommandExecutor _executor;
  private readonly IFileSystem _fileSystem;
  private readonly LocalToolsService _localTools;
  private readonly ToolLocator _tools;

  public BuildService(ICommandExecutor executor, IFileSystem fileSystem, LocalToolsService localTools, ToolLocator tools)
  {
    _executor = executor;
    _fileSystem = fileSystem;
    _localTools = localTools;
    _tools = tools;
  }

  public async Task<BuildPlan> PlanAsync(IReadOnlyList<ProjectLocation> locations, CancellationToken cancellationToken)
  {
    var plan = new BuildPlan { MsBuild = await _tools.LocateMsBuildAsync(cancellationToken) };
    if (!plan.MsBuild.Found)
      plan.GlobalBlockers.Add($"MSBuild não localizado: {plan.MsBuild.Problem} Instale o Visual Studio ou configure 'ferramentas.msBuild'.");

    foreach (var location in locations)
    {
      var target = new BuildTargetPlan { Location = location };
      plan.Targets.Add(target);

      if (target.Solution is null)
      {
        target.Blockers.Add($"O projeto '{location.Project.Alias}' não tem 'solucao' configurada.");
        continue;
      }

      if (!_fileSystem.FileExists(target.Solution))
        target.Blockers.Add($"Solução não encontrada: {target.Solution}. Faça o get da versão (não é executado automaticamente pelo build).");

      foreach (var host in _localTools.RunningFrom(location.Version, LocalToolsService.HostProcessName))
      {
        target.HostsToStop.Add(host);
        target.Warnings.Add($"RM.Host.exe (PID {host.Pid}) em execução nesta versão: será encerrado antes do build (como 'pep kill host').");
      }

      if (_localTools.HasInaccessibleProcesses(LocalToolsService.HostProcessName))
        target.Warnings.Add("Há processos RM.Host sem caminho acessível; não foi possível confirmar se usam esta versão.");
    }

    return plan;
  }

  /// <summary>
  /// Encerra de forma graciosa (mesma rotina de 'pep kill host') os RM.Host das versões prontas.
  /// Se algum não encerrar, os alvos daquela versão ficam bloqueados; nunca força sozinho.
  /// </summary>
  public IReadOnlyList<TerminationResult> StopHosts(BuildPlan plan)
  {
    var results = new List<TerminationResult>();
    foreach (var group in plan.Targets.Where(t => t.IsReady && t.HostsToStop.Count > 0).GroupBy(t => t.Location.Version.Id, StringComparer.OrdinalIgnoreCase))
    {
      var version = group.First().Location.Version;
      var failures = new List<TerminationResult>();
      foreach (var host in group.SelectMany(t => t.HostsToStop).DistinctBy(p => p.Pid))
      {
        var result = _localTools.Terminate(new HostCandidate(host, version, HostConfidence.Alta), force: false);
        results.Add(result);
        if (!result.Terminated)
          failures.Add(result);
      }

      foreach (var target in group)
      {
        foreach (var failure in failures)
        {
          var pid = failure.Candidate.Process.Pid;
          target.Blockers.Add($"RM.Host.exe (PID {pid}) não foi encerrado: {failure.Message} Encerre com 'pep kill host --pid {pid} --force' e tente novamente.");
        }
      }
    }

    return results;
  }

  public async Task<BuildExecutionResult> ExecuteAsync(BuildPlan plan, string? configuration, bool continueOnFailure, IOperationLog log, CancellationToken cancellationToken)
  {
    if (!plan.CanExecute || plan.MsBuild?.Path is null)
      throw new InvalidOperationException("Plano de build sem destinos prontos.");

    var results = new List<BuildTargetResult>();
    var stop = false;

    foreach (var target in plan.Targets)
    {
      if (!target.IsReady)
      {
        results.Add(new(target.Location, BuildTargetState.Blocked, string.Join(" ", target.Blockers), null));
        continue;
      }

      if (stop || cancellationToken.IsCancellationRequested)
      {
        results.Add(new(target.Location, BuildTargetState.NotStarted, "Não iniciado: execução interrompida.", null));
        stop = true;
        continue;
      }

      var arguments = new List<string> { target.Solution!, "/nologo", "/verbosity:minimal", "/nodeReuse:false" };
      if (!string.IsNullOrWhiteSpace(configuration))
        arguments.Add($"/p:Configuration={configuration}");

      log.Step(target.Location.Label, $"MSBuild {Path.GetFileName(target.Solution)}");
      var command = new Command(plan.MsBuild.Path, arguments, Path.GetDirectoryName(target.Solution));
      var result = await _executor.ExecuteAsync(command, line => log.Output(target.Location.Label, line), cancellationToken);
      log.ToolResult(target.Location.Label, "msbuild", result.ExitCode, result.Cancelled ? "cancelado" : result.ExitCode == 0 ? "sucesso" : "falhou", result.Duration, result.Output);

      if (result.Cancelled)
      {
        results.Add(new(target.Location, BuildTargetState.Cancelled, "Build cancelado. Saídas podem estar incompletas; recompile a solução.", result.Duration));
        stop = true;
      }
      else if (result.ExitCode == 0)
      {
        results.Add(new(target.Location, BuildTargetState.Succeeded, "Build concluído.", result.Duration));
      }
      else
      {
        var errors = result.StdOut.Split('\n')
          .Select(l => l.Trim())
          .Where(l => l.Contains(" error ", StringComparison.OrdinalIgnoreCase) || l.Contains(": erro ", StringComparison.OrdinalIgnoreCase))
          .Take(3)
          .ToList();
        var detail = errors.Count > 0 ? string.Join(" | ", errors) : $"MSBuild retornou código {result.ExitCode}.";
        results.Add(new(target.Location, BuildTargetState.Failed, detail, result.Duration));
        stop = !continueOnFailure;
      }
    }

    return new BuildExecutionResult(results, cancellationToken.IsCancellationRequested);
  }
}
