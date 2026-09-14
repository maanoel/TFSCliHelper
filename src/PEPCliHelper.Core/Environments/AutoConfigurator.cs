using System.Text.RegularExpressions;
using PEPCliHelper.Core.Catalog;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.Configuration;
using PEPCliHelper.Core.FileSystem;

namespace PEPCliHelper.Core.Environments;

/// <summary>Uma versão proposta pela configuração automática.</summary>
public sealed record AutoConfigVersion(
  string Id,
  bool IsCurrent,
  bool Active,
  string Folder,
  string LocalPath,
  string ServerPath,
  IReadOnlyList<string> ProjectsFound)
{
  public bool HasProjects => ProjectsFound.Count > 0;
}

/// <summary>Pasta em &lt;raiz&gt;\Legado que não foi considerada versão.</summary>
public sealed record IgnoredFolder(string Folder, string Reason);

public sealed record AutoConfigProposal(
  PepConfig Config,
  AutoConfigVersion? Current,
  IReadOnlyList<AutoConfigVersion> ActiveLegacy,
  IReadOnlyList<AutoConfigVersion> InactiveLegacy,
  IReadOnlyList<IgnoredFolder> Ignored,
  IReadOnlyList<string> Errors,
  IReadOnlyList<string> Warnings)
{
  public bool CanApply => Errors.Count == 0;

  /// <summary>Atual, legadas ativas e desativadas, nessa ordem.</summary>
  public IReadOnlyList<AutoConfigVersion> Versions =>
    (Current is null ? [] : new[] { Current }).Concat(ActiveLegacy).Concat(InactiveLegacy).ToList();
}

/// <summary>
/// Configuração automática pela convenção de pastas (decisão de 2026-09-14, spec 004):
/// &lt;raiz&gt;\Atual\Release é a atual; pastas numeradas em &lt;raiz&gt;\Legado são legadas, as 4 mais novas
/// (ordem numérica) ativas e as demais desativadas. Somente leitura do disco: não consulta o TFVC,
/// não cria, move ou exclui pastas.
/// </summary>
public sealed partial class AutoConfigurator
{
  public const string CurrentFolder = "Atual";
  public const string CurrentRelease = "Release";
  public const string LegacyFolder = "Legado";

  private readonly IFileSystem _fileSystem;

  public AutoConfigurator(IFileSystem fileSystem)
  {
    _fileSystem = fileSystem;
  }

  public AutoConfigProposal Propose(PepConfig baseConfig)
  {
    var errors = new List<string>();
    var warnings = new List<string>();
    var config = ConfigStore.Deserialize(ConfigStore.Serialize(baseConfig))!;

    var current = ProposeCurrent(config, errors);
    var (legacy, ignored) = FindLegacy(config);
    var active = legacy.Take(ConfigValidator.MaxActiveLegacy).Select(l => Describe(config, l.Name, l.Folder, isCurrent: false, active: true)).ToList();
    var inactive = legacy.Skip(ConfigValidator.MaxActiveLegacy).Select(l => Describe(config, l.Name, l.Folder, isCurrent: false, active: false)).ToList();

    if (legacy.Count < ConfigValidator.MaxActiveLegacy)
      warnings.Add($"Menos de {ConfigValidator.MaxActiveLegacy} legadas encontradas em {Path.Combine(config.LocalRoot, LegacyFolder)} ({legacy.Count}).");
    if (inactive.Count > 0)
      warnings.Add($"{inactive.Count} legada(s) mais antiga(s) ficaram desativadas: {string.Join(", ", inactive.Select(v => v.Id))}. Ative com 'pep env configure' se precisar.");

    var proposed = (current is null ? [] : new[] { current }).Concat(active).Concat(inactive).ToList();
    foreach (var version in proposed.Where(v => !v.HasProjects))
      warnings.Add($"{version.Id}: sem projetos ({string.Join(", ", config.Projects.Select(p => p.LocalFolder))}) em {version.Folder}.");

    if (current is not null)
    {
      config.Versions = MergeVersions(baseConfig, proposed, warnings);
      errors.AddRange(ConfigValidator.Validate(config).Select(e => "Configuração resultante inválida: " + e));
    }

    return new AutoConfigProposal(config, current, active, inactive, ignored, errors, warnings);
  }

  private AutoConfigVersion? ProposeCurrent(PepConfig config, List<string> errors)
  {
    var folder = Path.Combine(config.LocalRoot, CurrentFolder, CurrentRelease);
    if (_fileSystem.DirectoryExists(folder))
      return Describe(config, VersionCatalog.CurrentToken, folder, isCurrent: true, active: true);

    errors.Add($"Pasta da versão atual não encontrada: {folder}. Verifique a raiz local ('raizLocal') ou use 'pep env configure'.");
    return null;
  }

  private (List<(string Name, string Folder)> Legacy, List<IgnoredFolder> Ignored) FindLegacy(PepConfig config)
  {
    var ignored = new List<IgnoredFolder>();
    var legacy = new List<(string Name, string Folder, Version Number)>();

    foreach (var folder in _fileSystem.GetDirectories(Path.Combine(config.LocalRoot, LegacyFolder)))
    {
      var name = Path.GetFileName(folder.TrimEnd('\\', '/'));
      if (VersionNameRegex().IsMatch(name) && Version.TryParse(name, out var number))
        legacy.Add((name, LocalPath.Normalize(folder), number));
      else
        ignored.Add(new IgnoredFolder(LocalPath.Normalize(folder), "nome não é uma versão numérica (ex.: 12.1.2606)"));
    }

    var ordered = legacy
      .OrderByDescending(l => l.Number)
      .ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
      .Select(l => (l.Name, l.Folder))
      .ToList();
    return (ordered, ignored.OrderBy(i => i.Folder, StringComparer.OrdinalIgnoreCase).ToList());
  }

  private AutoConfigVersion Describe(PepConfig config, string id, string folder, bool isCurrent, bool active) => new(
    id,
    isCurrent,
    active,
    LocalPath.Normalize(folder),
    CatalogEditor.ToConfigLocalPath(config.LocalRoot, folder),
    DiscoveryService.ConventionServerPath(config, folder) ?? string.Empty,
    config.Projects.Where(p => _fileSystem.DirectoryExists(Path.Combine(folder, p.LocalFolder))).Select(p => p.Alias).ToList());

  /// <summary>
  /// Monta a lista de versões: as propostas substituem entradas com o mesmo id ou a mesma pasta (mantendo o workspace);
  /// entradas antigas sem correspondência são mantidas desativadas, nunca descartadas em silêncio.
  /// </summary>
  private static List<VersionConfig> MergeVersions(PepConfig baseConfig,IReadOnlyList<AutoConfigVersion> proposed, List<string> warnings)
  {
    var baseCatalog = VersionCatalog.FromConfig(baseConfig);
    var result = new List<VersionConfig>();
    var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var version in proposed)
    {
      var existing = baseCatalog.All.FirstOrDefault(v => v.Id.Equals(version.Id, StringComparison.OrdinalIgnoreCase))
        ?? baseCatalog.All.FirstOrDefault(v => LocalPath.AreEqual(v.LocalRoot, version.Folder));
      if (existing is not null)
        matched.Add(existing.Id);

      result.Add(new VersionConfig
      {
        Id = version.Id,
        IsCurrent = version.IsCurrent,
        Active = version.Active,
        LocalPath = version.LocalPath,
        ServerPath = version.ServerPath,
        Aliases = version.IsCurrent ? [] : CatalogEditor.SuggestAliases(version.Id).ToList(),
        Workspace = existing?.Workspace,
      });
    }

    var names = new HashSet<string>(result.SelectMany(v => v.Aliases.Prepend(v.Id)), StringComparer.OrdinalIgnoreCase);
    var kept = new List<string>();
    var replaced = new List<string>();
    foreach (var old in baseConfig.Versions.Where(v => !matched.Contains(v.Id.Trim())))
    {
      if (names.Contains(old.Id.Trim()))
      {
        replaced.Add(old.Id);
        continue;
      }

      var aliases = old.Aliases.Where(a => !names.Contains(a.Trim())).ToList();
      result.Add(new VersionConfig
      {
        Id = old.Id,
        IsCurrent = false,
        Active = false,
        LocalPath = old.LocalPath,
        ServerPath = old.ServerPath,
        Aliases = aliases,
        Workspace = old.Workspace,
      });
      foreach (var name in aliases.Prepend(old.Id))
        names.Add(name);
      kept.Add(old.Id);
    }

    if (kept.Count > 0)
      warnings.Add($"Versão(ões) do catálogo anterior sem pasta em Atual\\Release ou Legado mantida(s) desativada(s): {string.Join(", ", kept)}.");
    if (replaced.Count > 0)
      warnings.Add($"Versão(ões) do catálogo anterior substituída(s) por conflito de nome: {string.Join(", ", replaced)}.");

    return result;
  }

  [GeneratedRegex(@"^\d+(\.\d+){1,3}$")]
  private static partial Regex VersionNameRegex();
}
