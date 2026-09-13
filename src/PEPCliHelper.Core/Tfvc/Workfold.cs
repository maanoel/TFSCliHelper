using System.Text.RegularExpressions;
using PEPCliHelper.Core.Common;

namespace PEPCliHelper.Core.Tfvc;

public sealed record WorkspaceMapping(string ServerPath, string? LocalPath, bool IsCloaked);

public sealed record WorkfoldInfo(string? WorkspaceName, string? Owner, string? Collection, IReadOnlyList<WorkspaceMapping> Mappings);

/// <summary>
/// Lê a saída de "tf workfold &lt;pasta&gt;". Formato esperado (rótulos podem estar traduzidos):
/// <code>
/// Workspace : NOME (DOMINIO\usuario)
/// Collection: https://servidor/colecao
///  $/Projeto/pasta: C:\local\pasta
///  (cloaked) $/Projeto/pasta/sub:
/// </code>
/// </summary>
public static partial class WorkfoldParser
{
  public static WorkfoldInfo Parse(string output)
  {
    string? name = null;
    string? owner = null;
    string? collection = null;
    var mappings = new List<WorkspaceMapping>();

    foreach (var raw in TfOutputParser.Lines(output))
    {
      var line = raw.Trim();
      if (line.Length == 0 || line.StartsWith("===", StringComparison.Ordinal))
        continue;

      var mapping = MappingRegex().Match(line);
      if (mapping.Success)
      {
        var local = mapping.Groups["local"].Value.Trim();
        var cloaked = mapping.Groups["cloak"].Success || local.Length == 0;
        mappings.Add(new WorkspaceMapping(TfvcPath.Normalize(mapping.Groups["server"].Value), cloaked ? null : local, cloaked));
        continue;
      }

      var url = UrlRegex().Match(line);
      if (url.Success && collection is null)
      {
        collection = url.Value.TrimEnd('/');
        continue;
      }

      var workspace = WorkspaceRegex().Match(line);
      if (workspace.Success && name is null)
      {
        name = workspace.Groups["name"].Value.Trim();
        owner = workspace.Groups["owner"].Value.Trim();
      }
    }

    return new WorkfoldInfo(name, owner, collection, mappings);
  }

  [GeneratedRegex(@"^(?<cloak>\([^)]*\)\s*)?(?<server>\$/[^:]*?)\s*:\s*(?<local>.*)$")]
  private static partial Regex MappingRegex();

  [GeneratedRegex(@"https?://\S+", RegexOptions.IgnoreCase)]
  private static partial Regex UrlRegex();

  [GeneratedRegex(@"^[^:$]+:\s*(?<name>[^()]+?)\s*\((?<owner>[^)]+)\)\s*$")]
  private static partial Regex WorkspaceRegex();
}
