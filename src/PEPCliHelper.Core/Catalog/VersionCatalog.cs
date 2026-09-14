using System.Text.RegularExpressions;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.Configuration;

namespace PEPCliHelper.Core.Catalog;

/// <summary><paramref name="Principal"/>: compilado primeiro em cada versão (spec 009).</summary>
public sealed record ProjectDefinition(string Alias, string Name, string LocalFolder, string ServerFolder, string? Solution, bool Principal = false);

public sealed record VersionEntry(
  string Id,
  bool IsCurrent,
  bool Active,
  string LocalRoot,
  string ServerRoot,
  IReadOnlyList<string> Aliases,
  string? Workspace)
{
  public string Label => IsCurrent ? $"{Id} (atual)" : Id;
}

/// <summary>Um projeto dentro de uma versão: onde fica localmente e no servidor.</summary>
public sealed record ProjectLocation(VersionEntry Version, ProjectDefinition Project, string LocalPath, string ServerPath)
{
  public string Label => $"{Version.Id} · {Project.Alias} ({Project.Name})";

  public string? SolutionPath => Project.Solution is null ? null : Path.Combine(LocalPath, Project.Solution);
}

public enum VersionResolutionStatus
{
  Found,
  NotFound,
  Ambiguous,
  Inactive,
}

public sealed record VersionResolution(VersionResolutionStatus Status, VersionEntry? Version, IReadOnlyList<string> Candidates);

/// <summary>Catálogo de versões: uma atual e no máximo quatro legadas ativas (spec 004).</summary>
public sealed partial class VersionCatalog
{
  public const string CurrentToken = "atual";

  /// <summary>Projeto principal quando nenhum está marcado (configurações anteriores ao campo "principal"): o PEP.</summary>
  public const string DefaultPrincipalAlias = "back";

  private readonly List<VersionEntry> _versions;
  private readonly List<ProjectDefinition> _projects;

  public VersionCatalog(IEnumerable<VersionEntry> versions, IEnumerable<ProjectDefinition> projects)
  {
    _versions = versions.ToList();
    _projects = projects.ToList();
  }

  public IReadOnlyList<VersionEntry> All => _versions;

  public IReadOnlyList<ProjectDefinition> Projects => _projects;

  public VersionEntry? Current => _versions.FirstOrDefault(v => v.IsCurrent && v.Active);

  public IReadOnlyList<VersionEntry> ActiveLegacy => _versions.Where(v => v.Active && !v.IsCurrent).ToList();

  /// <summary>Atual primeiro, depois legadas na ordem da configuração.</summary>
  public IReadOnlyList<VersionEntry> Active =>
    (Current is null ? [] : new List<VersionEntry> { Current }).Concat(ActiveLegacy).ToList();

  public bool IsConfigured => Current is not null;

  public static VersionCatalog FromConfig(PepConfig config)
  {
    var versions = config.Versions.Select(v => new VersionEntry(
      v.Id.Trim(),
      v.IsCurrent,
      v.Active,
      LocalPath.Normalize(Path.IsPathFullyQualified(v.LocalPath) ? v.LocalPath : Path.Combine(config.LocalRoot, v.LocalPath)),
      TfvcPath.Normalize(v.ServerPath),
      v.Aliases.Select(a => a.Trim()).Where(a => a.Length > 0).ToList(),
      string.IsNullOrWhiteSpace(v.Workspace) ? null : v.Workspace));

    var principalAlias = config.Projects.FirstOrDefault(p => p.Principal)?.Alias ?? DefaultPrincipalAlias;
    var projects = config.Projects.Select(p => new ProjectDefinition(p.Alias, p.Name, p.LocalFolder, p.ServerFolder, p.Solution,
      p.Alias.Equals(principalAlias, StringComparison.OrdinalIgnoreCase)));
    return new VersionCatalog(versions, projects);
  }

  public ProjectDefinition? Principal => _projects.FirstOrDefault(p => p.Principal);

  /// <summary>
  /// Resolve por id exato, alias exato ou último segmento numérico exato ("2606" → "12.1.2606").
  /// Nunca aproxima: ambiguidade e versões inativas são reportadas.
  /// </summary>
  public VersionResolution ResolveVersion(string? token)
  {
    if (string.IsNullOrWhiteSpace(token))
      return new(VersionResolutionStatus.NotFound, null, []);

    var value = token.Trim();

    var exactId = _versions.FirstOrDefault(v => v.Id.Equals(value, StringComparison.OrdinalIgnoreCase));
    if (exactId is not null)
      return Found(exactId);

    var matches = _versions
      .Where(v => v.Aliases.Any(a => a.Equals(value, StringComparison.OrdinalIgnoreCase))
        || (DigitsRegex().IsMatch(value) && LastSegment(v.Id).Equals(value, StringComparison.Ordinal)))
      .Distinct()
      .ToList();

    if (matches.Count == 0 && value.Equals(CurrentToken, StringComparison.OrdinalIgnoreCase) && Current is not null)
      return Found(Current);

    return matches.Count switch
    {
      0 => new(VersionResolutionStatus.NotFound, null, []),
      1 => Found(matches[0]),
      _ => new(VersionResolutionStatus.Ambiguous, null, matches.Select(m => m.Id).ToList()),
    };

    static VersionResolution Found(VersionEntry version) => version.Active
      ? new(VersionResolutionStatus.Found, version, [])
      : new(VersionResolutionStatus.Inactive, version, []);
  }

  public ProjectDefinition? FindProject(string? alias) =>
    string.IsNullOrWhiteSpace(alias)
      ? null
      : _projects.FirstOrDefault(p => p.Alias.Equals(alias.Trim(), StringComparison.OrdinalIgnoreCase));

  public ProjectLocation Locate(VersionEntry version, ProjectDefinition project) => new(
    version,
    project,
    LocalPath.Normalize(Path.Combine(version.LocalRoot, project.LocalFolder)),
    TfvcPath.Combine(version.ServerRoot, project.ServerFolder));

  public IReadOnlyList<ProjectLocation> LocateAll(IEnumerable<VersionEntry> versions, ProjectDefinition? onlyProject = null) =>
    versions
      .SelectMany(v => (onlyProject is null ? _projects : [onlyProject]).Select(p => Locate(v, p)))
      .ToList();

  private static string LastSegment(string id)
  {
    var index = id.LastIndexOf('.');
    return index >= 0 ? id[(index + 1)..] : id;
  }

  [GeneratedRegex(@"^\d+$")]
  private static partial Regex DigitsRegex();
}
