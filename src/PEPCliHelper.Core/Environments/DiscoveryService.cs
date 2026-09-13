using PEPCliHelper.Core.Catalog;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.Configuration;
using PEPCliHelper.Core.FileSystem;
using PEPCliHelper.Core.Tfvc;

namespace PEPCliHelper.Core.Environments;

public sealed record DiscoveredVersion(
  string Folder,
  string SuggestedId,
  bool LooksCurrent,
  IReadOnlyList<string> ProjectsFound,
  string? ServerRoot,
  MappingState? Mapping,
  IReadOnlyList<string> Notes,
  string? CatalogId,
  bool CatalogActive);

/// <summary>
/// Descoberta somente leitura (spec 004): lista pastas candidatas e consulta mapeamentos.
/// Nunca executa get, merge, exclusão, encerramento de processo ou alteração de workspace.
/// O nome da pasta é apenas uma pista.
/// </summary>
public sealed class DiscoveryService
{
  private readonly IFileSystem _fileSystem;
  private readonly MappingService _mapping;

  public DiscoveryService(IFileSystem fileSystem, ITfvcClient tfvc)
  {
    _fileSystem = fileSystem;
    _mapping = new MappingService(tfvc, fileSystem);
  }

  public async Task<IReadOnlyList<DiscoveredVersion>> DiscoverAsync(PepConfig config, bool queryTfvc, IOperationLog log, CancellationToken cancellationToken)
  {
    var catalog = VersionCatalog.FromConfig(config);
    var results = new List<DiscoveredVersion>();
    var tfvcAvailable = queryTfvc;

    foreach (var (folder, looksCurrent) in CandidateFolders(config))
    {
      cancellationToken.ThrowIfCancellationRequested();
      var projects = config.Projects.Where(p => _fileSystem.DirectoryExists(Path.Combine(folder, p.LocalFolder))).ToList();
      if (projects.Count == 0)
        continue;

      var notes = new List<string>();
      var inCatalog = catalog.All.FirstOrDefault(v => LocalPath.AreEqual(v.LocalRoot, folder));
      string? serverRoot = null;
      MappingState? state = null;

      if (tfvcAvailable)
      {
        var (versionCheck, query) = await _mapping.CheckAsync(folder, null, folder, log, cancellationToken);
        state = versionCheck.State;
        if (query is { IsEnvironmentError: true })
        {
          tfvcAvailable = false;
          notes.Add("TFVC indisponível; mapeamentos não consultados nas pastas seguintes.");
        }
        else if (versionCheck.IsValid)
        {
          serverRoot = versionCheck.EffectiveServerPath;
        }
        else
        {
          (serverRoot, state) = await FromProjectFolderAsync(folder, projects, log, notes, cancellationToken);
        }
      }
      else
      {
        notes.Add("Mapeamento não consultado.");
      }

      if (inCatalog is not null && serverRoot is not null && !TfvcPath.AreEqual(inCatalog.ServerRoot, serverRoot))
        notes.Add($"Catálogo aponta '{inCatalog.ServerRoot}', mas o mapeamento efetivo é '{serverRoot}'.");

      if (serverRoot is null && ConventionServerPath(config, folder) is { } conventional)
      {
        serverRoot = conventional;
        notes.Add($"Caminho TFVC pela convenção '{config.ServerRoot}/<pasta>'. Confirme com 'pep env validate'.");
      }

      var missing = config.Projects.Except(projects).Select(p => p.Name).ToList();
      if (missing.Count > 0)
        notes.Add($"Sem pasta: {string.Join(", ", missing)}.");

      results.Add(new DiscoveredVersion(
        folder,
        inCatalog?.Id ?? SuggestId(folder, looksCurrent),
        looksCurrent,
        projects.Select(p => p.Alias).ToList(),
        serverRoot,
        state,
        notes,
        inCatalog?.Id,
        inCatalog?.Active ?? false));
    }

    return results;
  }

  private IEnumerable<(string Folder, bool LooksCurrent)> CandidateFolders(PepConfig config)
  {
    foreach (var (parent, looksCurrent) in new[] { ("Atual", true), ("Legado", false) })
    {
      var directory = Path.Combine(config.LocalRoot, parent);
      foreach (var folder in _fileSystem.GetDirectories(directory).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        yield return (LocalPath.Normalize(folder), looksCurrent);
    }
  }

  private async Task<(string? ServerRoot, MappingState? State)> FromProjectFolderAsync(
    string folder, IReadOnlyList<ProjectConfig> projects, IOperationLog log, List<string> notes, CancellationToken cancellationToken)
  {
    foreach (var project in projects)
    {
      var (check, query) = await _mapping.CheckAsync(Path.Combine(folder, project.LocalFolder), null, folder, log, cancellationToken);
      if (query is { IsEnvironmentError: true })
        return (null, MappingState.QueryFailed);

      if (check.IsValid && check.EffectiveServerPath is not null)
      {
        var suffix = "/" + project.ServerFolder.Trim('/');
        if (check.EffectiveServerPath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
          notes.Add($"Raiz da versão não mapeada; caminho inferido pelo projeto {project.Name}. Confirme antes de salvar.");
          return (check.EffectiveServerPath[..^suffix.Length], MappingState.Mapped);
        }

        notes.Add($"{project.Name} mapeado para '{check.EffectiveServerPath}', que não termina em '{suffix}'. Informe o caminho manualmente.");
        return (null, MappingState.Divergent);
      }
    }

    notes.Add("Nenhuma pasta desta versão está mapeada no TFVC.");
    return (null, MappingState.NotMapped);
  }

  /// <summary>
  /// Convenção local ⇄ servidor: C:\Linha-RM\Legado\12.1.2506 → $/Linha-RM/Legado/12.1.2506;
  /// C:\Linha-RM\Atual\Release → $/Linha-RM/Atual/Release (TFVC não diferencia maiúsculas).
  /// Null quando a pasta está fora da raiz local.
  /// </summary>
  public static string? ConventionServerPath(PepConfig config, string folder)
  {
    if (!TfvcPath.IsServerPath(config.ServerRoot) || !LocalPath.IsUnderOrEqual(folder, config.LocalRoot) || LocalPath.AreEqual(folder, config.LocalRoot))
      return null;

    return TfvcPath.Combine(config.ServerRoot, LocalPath.Relative(folder, config.LocalRoot).Replace('\\', '/'));
  }

  private static string SuggestId(string folder, bool looksCurrent) =>
    looksCurrent ? VersionCatalog.CurrentToken : Path.GetFileName(folder);
}
