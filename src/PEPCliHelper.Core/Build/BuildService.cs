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

/// <summary>Build com MSBuild (spec 009). Sequencial, sem get, sem encerrar processos.</summary>
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
        target.Blockers.Add(
          $"RM.Host.exe (PID {host.Pid}) está em execução a partir desta versão e pode bloquear a saída do build. " +
          $"Encerre com 'pep kill host --pid {host.Pid}' e tente novamente.");
      }

      if (_localTools.HasInaccessibleProcesses(LocalToolsService.HostProcessName))
        target.Warnings.Add("Há processos RM.Host sem caminho acessível; não foi possível confirmar se usam esta versão.");
    }

    return plan;
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
