using PEPCliHelper.Core.Catalog;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.Configuration;
using PEPCliHelper.Core.Execution;
using PEPCliHelper.Core.FileSystem;
using PEPCliHelper.Core.Tfvc;

namespace PEPCliHelper.Core.Diagnostics;

public enum CheckStatus
{
  Ok,
  Warning,
  Fail,
  NotApplicable,
}

public sealed record DoctorCheck(string Area, string Name, CheckStatus Status, string Detail, string? Guidance = null);

/// <summary>Diagnóstico somente leitura (spec 011). Não corrige nada.</summary>
public sealed class DoctorService
{
  private const long LowDiskBytes = 5L * 1024 * 1024 * 1024;

  private readonly ConfigStore _store;
  private readonly IFileSystem _fileSystem;
  private readonly ToolLocator _tools;
  private readonly ITfvcClient _tfvc;
  private readonly string _historyDirectory;

  public DoctorService(ConfigStore store, IFileSystem fileSystem, ToolLocator tools, ITfvcClient tfvc, string historyDirectory)
  {
    _store = store;
    _fileSystem = fileSystem;
    _tools = tools;
    _tfvc = tfvc;
    _historyDirectory = historyDirectory;
  }

  public async Task<IReadOnlyList<DoctorCheck>> RunAsync(IOperationLog log, CancellationToken cancellationToken)
  {
    var checks = new List<DoctorCheck>();

    // Configuração
    var load = _store.Load();
    switch (load.Status)
    {
      case ConfigLoadStatus.Loaded:
        checks.Add(new("Configuração", "Arquivo", CheckStatus.Ok, load.Path));
        break;
      case ConfigLoadStatus.Missing:
        checks.Add(new("Configuração", "Arquivo", CheckStatus.Fail, $"Não encontrado: {load.Path}", "Execute 'pep config init'."));
        break;
      default:
        checks.Add(new("Configuração", "Arquivo", CheckStatus.Fail, string.Join(" | ", load.Errors), "Corrija o arquivo e execute 'pep config validate'."));
        break;
    }

    // Ferramentas
    log.Step("doctor", "Localizando ferramentas");
    var tf = await _tools.LocateTfAsync(cancellationToken);
    checks.Add(ToolCheck(tf, "ferramentas.tfExe"));
    var msbuild = await _tools.LocateMsBuildAsync(cancellationToken);
    checks.Add(ToolCheck(msbuild, "ferramentas.msBuild"));

    var config = load.Status == ConfigLoadStatus.Loaded ? load.Config : null;
    if (config is null)
    {
      checks.Add(new("TFVC", "Coleção", CheckStatus.NotApplicable, "Sem configuração válida."));
      checks.Add(new("Catálogo", "Versões", CheckStatus.NotApplicable, "Sem configuração válida."));
      AddLocalChecks(checks, @"C:\");
      return checks;
    }

    // Coleção, conexão e autenticação
    var environmentOk = false;
    if (string.IsNullOrWhiteSpace(config.Collection))
    {
      checks.Add(new("TFVC", "Coleção", CheckStatus.Fail, "Não configurada.", "Informe 'colecao' na configuração."));
    }
    else if (!tf.Found)
    {
      checks.Add(new("TFVC", "Conexão e autenticação", CheckStatus.NotApplicable, "tf.exe ausente."));
    }
    else
    {
      checks.Add(new("TFVC", "Coleção", CheckStatus.Ok, config.Collection));
      log.Step("doctor", "Consultando workspaces da coleção");
      var workspaces = await _tfvc.ListWorkspacesAsync(config.Collection, cancellationToken);
      log.ToolResult("doctor", "tf workspaces", workspaces.ExitCode, workspaces.StatusText, workspaces.Duration, workspaces.Output);
      environmentOk = workspaces.IsSuccess;
      checks.Add(workspaces.IsSuccess
        ? new("TFVC", "Conexão e autenticação", CheckStatus.Ok, "Coleção acessível; workspaces listados.")
        : new("TFVC", "Conexão e autenticação", CheckStatus.Fail, $"{workspaces.StatusText}: {workspaces.Summary}", TfErrorClassifier.Guidance(workspaces)));
    }

    // Catálogo
    var catalog = VersionCatalog.FromConfig(config);
    if (!catalog.IsConfigured)
    {
      checks.Add(new("Catálogo", "Versões", CheckStatus.Fail, "Nenhuma versão atual configurada.", "Execute 'pep env discover' e 'pep env configure'."));
    }
    else
    {
      var legacy = catalog.ActiveLegacy.Select(v => v.Id).ToList();
      checks.Add(new("Catálogo", "Versões", CheckStatus.Ok,
        $"Atual: {catalog.Current!.Id} · Legadas ativas ({legacy.Count}/{ConfigValidator.MaxActiveLegacy}): {(legacy.Count == 0 ? "nenhuma" : string.Join(", ", legacy))}"));

      var mapping = new MappingService(_tfvc, _fileSystem);
      foreach (var location in catalog.LocateAll(catalog.Active))
      {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_fileSystem.DirectoryExists(location.LocalPath))
        {
          checks.Add(new("Mapeamentos", location.Label, CheckStatus.Fail, $"Pasta inexistente: {location.LocalPath}", "Confirme 'caminhoLocal' ou faça o get inicial pelo Visual Studio."));
          continue;
        }

        if (tf.Found && environmentOk)
        {
          var (check, _) = await mapping.CheckAsync(location.LocalPath, location.ServerPath, location.Label, log, cancellationToken);
          checks.Add(new("Mapeamentos", location.Label,
            check.IsValid ? (check.CloakedChildren.Count > 0 ? CheckStatus.Warning : CheckStatus.Ok) : CheckStatus.Fail,
            check.IsValid && check.CloakedChildren.Count > 0 ? $"{check.Message} Subpastas cloaked: {string.Join(", ", check.CloakedChildren)}" : check.Message,
            check.Guidance));
        }
        else
        {
          checks.Add(new("Mapeamentos", location.Label, CheckStatus.NotApplicable, "TFVC indisponível; mapeamento não verificado."));
        }

        if (location.SolutionPath is not null)
        {
          checks.Add(_fileSystem.FileExists(location.SolutionPath)
            ? new("Build", location.Label, CheckStatus.Ok, location.SolutionPath)
            : new("Build", location.Label, CheckStatus.Warning, $"Solução não encontrada: {location.SolutionPath}", "Faça o get da versão antes do build."));
        }
      }
    }

    AddLocalChecks(checks, config.LocalRoot);
    return checks;
  }

  private void AddLocalChecks(List<DoctorCheck> checks, string localRoot)
  {
    var free = _fileSystem.GetAvailableFreeSpace(localRoot);
    checks.Add(free switch
    {
      null => new("Máquina", "Espaço em disco", CheckStatus.Warning, $"Não foi possível verificar '{localRoot}'."),
      < LowDiskBytes => new("Máquina", "Espaço em disco", CheckStatus.Warning, $"{free / 1024 / 1024 / 1024} GB livres em {Path.GetPathRoot(localRoot)}", "Libere espaço antes de get/build."),
      _ => new("Máquina", "Espaço em disco", CheckStatus.Ok, $"{free / 1024 / 1024 / 1024} GB livres em {Path.GetPathRoot(localRoot)}"),
    });

    try
    {
      _fileSystem.EnsureDirectory(_historyDirectory);
      checks.Add(new("Máquina", "Histórico", CheckStatus.Ok, _historyDirectory));
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
      checks.Add(new("Máquina", "Histórico", CheckStatus.Warning, $"Sem permissão de escrita: {ex.Message}", "Operações continuam, mas sem registro local."));
    }
  }

  private static DoctorCheck ToolCheck(ToolInfo tool, string configKey) => tool.Found
    ? new("Ferramentas", tool.Name, CheckStatus.Ok, $"{tool.Path} (v{tool.Version ?? "?"}, via {tool.Source})")
    : new("Ferramentas", tool.Name, CheckStatus.Fail, tool.Problem ?? "Não localizada.", $"Instale o Visual Studio 2022 ou configure '{configKey}'.");

  public static int ExitCode(IReadOnlyList<DoctorCheck> checks) =>
    checks.Any(c => c.Status == CheckStatus.Fail) ? ExitCodes.Precondition : ExitCodes.Success;
}
