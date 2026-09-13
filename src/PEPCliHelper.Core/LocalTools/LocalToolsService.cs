using PEPCliHelper.Core.Catalog;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.Configuration;
using PEPCliHelper.Core.FileSystem;
using PEPCliHelper.Core.Processes;

namespace PEPCliHelper.Core.LocalTools;

public enum HostConfidence
{
  Alta,
  Baixa,
  Desconhecida,
}

public sealed record HostCandidate(ProcessInfo Process, VersionEntry? Version, HostConfidence Confidence)
{
  public string VersionLabel => Version?.Id ?? (Confidence == HostConfidence.Baixa ? "fora do catálogo" : "desconhecida");
}

public enum LocalFileKind
{
  Broker,
  Host,
  Rm,
  Alias,
  HostConfig,
}

public sealed class BrokerPlan
{
  public required VersionEntry Version { get; init; }

  public required string Path { get; init; }

  public bool Exists { get; init; }

  public List<string> Blockers { get; } = [];

  public bool IsReady => Exists && Blockers.Count == 0;
}

public sealed record TerminationResult(HostCandidate Candidate, bool Terminated, bool Forced, string Message);

/// <summary>Broker, abertura de executáveis e encerramento de host (spec 010).</summary>
public sealed class LocalToolsService
{
  public const string HostProcessName = "RM.Host";
  public const string RmProcessName = "RM";

  private static readonly TimeSpan GracefulWait = TimeSpan.FromSeconds(10);

  private readonly IFileSystem _fileSystem;
  private readonly IProcessInspector _processes;
  private readonly TimeProvider _time;

  public LocalToolsService(IFileSystem fileSystem, IProcessInspector processes, TimeProvider time)
  {
    _fileSystem = fileSystem;
    _processes = processes;
    _time = time;
  }

  /// <summary>Resolve um arquivo da versão e garante que não sai da pasta da versão.</summary>
  public static string ResolveVersionFile(VersionEntry version, LocalFilesConfig files, LocalFileKind kind)
  {
    var relative = kind switch
    {
      LocalFileKind.Broker => files.Broker,
      LocalFileKind.Host => files.Host,
      LocalFileKind.Rm => files.Rm,
      LocalFileKind.Alias => files.Alias,
      LocalFileKind.HostConfig => files.HostConfig,
      _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    var full = LocalPath.Normalize(System.IO.Path.Combine(version.LocalRoot, relative));
    if (!LocalPath.IsUnderOrEqual(full, version.LocalRoot) || LocalPath.AreEqual(full, version.LocalRoot))
    {
      throw new PreconditionException(
        $"O caminho resolvido '{full}' está fora da pasta da versão '{version.LocalRoot}'.",
        "Corrija 'arquivosLocais' na configuração.");
    }

    return full;
  }

  public BrokerPlan PlanBrokerDelete(VersionEntry version, LocalFilesConfig files)
  {
    var path = ResolveVersionFile(version, files, LocalFileKind.Broker);
    var plan = new BrokerPlan { Version = version, Path = path, Exists = _fileSystem.FileExists(path) };

    if (_fileSystem.DirectoryExists(path))
    {
      plan.Blockers.Add($"'{path}' é um diretório. O PEP CLI remove somente o arquivo de broker.");
      return plan;
    }

    if (!plan.Exists)
      return plan;

    foreach (var process in RunningFrom(version, HostProcessName).Concat(RunningFrom(version, RmProcessName)))
      plan.Blockers.Add($"{process.Name}.exe (PID {process.Pid}) está em execução a partir desta versão. Encerre antes de remover o broker (ex.: 'pep kill host --pid {process.Pid}').");

    if (plan.Blockers.Count == 0 && _fileSystem.IsLocked(path))
      plan.Blockers.Add($"O arquivo '{path}' está em uso por outro processo. Feche o RM/Host dessa versão e tente novamente.");

    return plan;
  }

  /// <summary>Remove somente o arquivo do plano. Retorna o caminho do backup, se criado.</summary>
  public string? DeleteBroker(BrokerPlan plan, bool backup)
  {
    if (!plan.IsReady)
      throw new InvalidOperationException("Plano de remoção do broker não está pronto.");

    string? backupPath = null;
    if (backup)
    {
      backupPath = $"{plan.Path}.bak-{_time.GetLocalNow():yyyyMMddHHmmss}";
      _fileSystem.CopyFile(plan.Path, backupPath);
    }

    _fileSystem.DeleteFile(plan.Path);
    return backupPath;
  }

  public IReadOnlyList<ProcessInfo> RunningFrom(VersionEntry version, string processName) =>
    _processes.ListByName(processName)
      .Where(p => p.Path is not null && LocalPath.IsUnderOrEqual(p.Path, version.LocalRoot))
      .ToList();

  public bool HasInaccessibleProcesses(string processName) =>
    _processes.ListByName(processName).Any(p => p.Path is null);

  public IReadOnlyList<HostCandidate> ListHosts(VersionCatalog catalog, string localRoot) =>
    _processes.ListByName(HostProcessName).Select(p => Infer(p, catalog, localRoot)).ToList();

  public static HostCandidate Infer(ProcessInfo process, VersionCatalog catalog, string localRoot)
  {
    if (process.Path is null)
      return new HostCandidate(process, null, HostConfidence.Desconhecida);

    var version = catalog.All
      .Where(v => LocalPath.IsUnderOrEqual(process.Path, v.LocalRoot))
      .OrderByDescending(v => v.LocalRoot.Length)
      .FirstOrDefault();

    if (version is not null)
      return new HostCandidate(process, version, HostConfidence.Alta);

    return new HostCandidate(process, null, LocalPath.IsUnderOrEqual(process.Path, localRoot) ? HostConfidence.Baixa : HostConfidence.Desconhecida);
  }

  /// <summary>Gracioso por padrão; forçado apenas quando <paramref name="force"/> foi confirmado pelo usuário.</summary>
  public TerminationResult Terminate(HostCandidate candidate, bool force)
  {
    var outcome = force
      ? _processes.Kill(candidate.Process, GracefulWait)
      : _processes.TryCloseGracefully(candidate.Process, GracefulWait);

    return outcome switch
    {
      TerminationOutcome.Terminated => new(candidate, true, force, force ? "Encerrado de forma forçada." : "Encerrado de forma graciosa."),
      TerminationOutcome.NotRunning => new(candidate, true, false, "Processo já não estava em execução."),
      TerminationOutcome.ProcessChanged => new(candidate, false, false, "O PID agora pertence a outro processo; nada foi encerrado. Liste novamente com 'pep kill host'."),
      _ when force => new(candidate, false, true, "Não foi possível encerrar (permissão ou processo protegido). Tente como administrador ou pelo Gerenciador de Tarefas."),
      _ => new(candidate, false, false, "Não encerrou de forma graciosa (sem janela principal ou não respondeu). Use --force para encerramento forçado, após salvar o trabalho."),
    };
  }
}
