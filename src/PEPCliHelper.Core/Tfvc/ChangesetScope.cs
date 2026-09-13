using PEPCliHelper.Core.Common;

namespace PEPCliHelper.Core.Tfvc;

/// <summary>Classifica os itens de um changeset em relação ao projeto e à versão de origem.</summary>
public sealed record ChangesetScope(
  int Changeset,
  IReadOnlyList<string> Header,
  IReadOnlyList<string> Included,
  IReadOnlyList<string> OtherProjects,
  IReadOnlyList<string> OutsideSource)
{
  public IReadOnlyList<string> Excluded => OtherProjects.Concat(OutsideSource).ToList();

  public static ChangesetScope Analyze(ChangesetInfo changeset, string sourceProjectServerPath, string sourceVersionServerPath)
  {
    var included = new List<string>();
    var otherProjects = new List<string>();
    var outside = new List<string>();

    foreach (var item in changeset.Items)
    {
      if (TfvcPath.IsUnderOrEqual(item, sourceProjectServerPath))
        included.Add(item);
      else if (TfvcPath.IsUnderOrEqual(item, sourceVersionServerPath))
        otherProjects.Add(item);
      else
        outside.Add(item);
    }

    return new ChangesetScope(changeset.Id, changeset.Header, included, otherProjects, outside);
  }

  /// <summary>Caminhos locais no destino correspondentes aos itens incluídos.</summary>
  public IReadOnlyList<string> TargetLocalFiles(string sourceProjectServerPath, string targetProjectLocalPath) =>
    Included
      .Select(item => TfvcPath.Relative(item, sourceProjectServerPath))
      .Where(relative => relative.Length > 0)
      .Select(relative => Path.Combine(targetProjectLocalPath, LocalPath.FromServerRelative(relative)))
      .ToList();

  /// <summary>Caminhos de servidor no destino correspondentes aos itens incluídos.</summary>
  public IReadOnlyList<string> TargetServerItems(string sourceProjectServerPath, string targetProjectServerPath) =>
    Included
      .Select(item => TfvcPath.Relative(item, sourceProjectServerPath))
      .Where(relative => relative.Length > 0)
      .Select(relative => TfvcPath.Combine(targetProjectServerPath, relative))
      .ToList();
}
