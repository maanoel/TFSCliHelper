using System.Text.RegularExpressions;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.Configuration;

namespace PEPCliHelper.Core.Environments;

/// <summary>Uma versão escolhida na rotação do catálogo.</summary>
public sealed record CatalogChoice(string Id, string LocalFolder, string ServerRoot);

/// <summary>
/// Aplica a rotação de versões apenas no catálogo (spec 004): define a atual, as legadas ativas e desativa as demais.
/// Não move, não exclui pastas e não altera branches ou workspaces.
/// </summary>
public static partial class CatalogEditor
{
  public static (PepConfig Config, IReadOnlyList<string> Errors) Apply(PepConfig original, CatalogChoice current, IReadOnlyList<CatalogChoice> legacy)
  {
    var errors = new List<string>();
    if (legacy.Count > ConfigValidator.MaxActiveLegacy)
      errors.Add($"Foram selecionadas {legacy.Count} legadas; o máximo é {ConfigValidator.MaxActiveLegacy}.");

    if (legacy.Any(l => l.Id.Equals(current.Id, StringComparison.OrdinalIgnoreCase)))
      errors.Add($"A versão '{current.Id}' não pode ser atual e legada ao mesmo tempo.");

    var config = ConfigStore.Deserialize(ConfigStore.Serialize(original))!;
    foreach (var version in config.Versions)
    {
      version.IsCurrent = false;
      version.Active = false;
    }

    Upsert(config, current, isCurrent: true);
    foreach (var choice in legacy)
      Upsert(config, choice, isCurrent: false);

    errors.AddRange(ConfigValidator.Validate(config));
    return (config, errors);
  }

  public static IReadOnlyList<string> SuggestAliases(string id)
  {
    var match = LastNumberRegex().Match(id);
    return match.Success && !match.Value.Equals(id, StringComparison.Ordinal) ? [match.Value] : [];
  }

  /// <summary>Caminho local relativo à raiz quando possível.</summary>
  public static string ToConfigLocalPath(string localRoot, string folder) =>
    LocalPath.IsUnderOrEqual(folder, localRoot) && !LocalPath.AreEqual(folder, localRoot)
      ? LocalPath.Relative(folder, localRoot)
      : LocalPath.Normalize(folder);

  private static void Upsert(PepConfig config, CatalogChoice choice, bool isCurrent)
  {
    var existing = config.Versions.FirstOrDefault(v => v.Id.Equals(choice.Id, StringComparison.OrdinalIgnoreCase));
    if (existing is null)
    {
      existing = new VersionConfig
      {
        Id = choice.Id,
        Aliases = SuggestAliases(choice.Id).ToList(),
      };
      config.Versions.Add(existing);
    }

    existing.LocalPath = ToConfigLocalPath(config.LocalRoot, choice.LocalFolder);
    existing.ServerPath = TfvcPath.Normalize(choice.ServerRoot);
    existing.IsCurrent = isCurrent;
    existing.Active = true;
  }

  [GeneratedRegex(@"\d+$")]
  private static partial Regex LastNumberRegex();
}
