using PEPCliHelper.Core.Catalog;

namespace PEPCliHelper.Core.Build;

/// <summary>
/// Ordem do build (spec 009): versões na ordem do catálogo (atual primeiro, depois legadas);
/// em cada versão, o projeto principal primeiro e os demais selecionados na ordem da configuração.
/// </summary>
public static class BuildOrder
{
  /// <param name="projects">Projetos selecionados; nulo ou vazio = todos os projetos do catálogo.</param>
  public static IReadOnlyList<ProjectLocation> Arrange(VersionCatalog catalog, IEnumerable<VersionEntry> versions, IEnumerable<ProjectDefinition>? projects = null)
  {
    var selectedAliases = projects?.Select(p => p.Alias).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var orderedProjects = catalog.Projects
      .Where(p => selectedAliases is null || selectedAliases.Count == 0 || selectedAliases.Contains(p.Alias))
      .OrderByDescending(p => p.Principal)
      .ToList();

    var selectedIds = versions.Select(v => v.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var orderedVersions = catalog.Active.Where(v => selectedIds.Contains(v.Id))
      .Concat(catalog.All.Where(v => selectedIds.Contains(v.Id) && !v.Active));

    return orderedVersions
      .SelectMany(v => orderedProjects.Select(p => catalog.Locate(v, p)))
      .ToList();
  }
}
