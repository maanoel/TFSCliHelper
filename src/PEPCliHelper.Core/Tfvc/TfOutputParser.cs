using System.Text.RegularExpressions;

namespace PEPCliHelper.Core.Tfvc;

/// <summary>
/// Leitura da saída do tf.exe independente de idioma: ancorada em caminhos ($/..., C:\...) e números,
/// nunca em rótulos traduzidos.
/// </summary>
public static partial class TfOutputParser
{
  public static IReadOnlyList<string> Lines(string output) =>
    output.Split('\n').Select(l => l.TrimEnd('\r')).ToList();

  /// <summary>Linhas não vazias que contêm algum dos trechos (sem diferenciar maiúsculas).</summary>
  public static IReadOnlyList<string> LinesContaining(string output, params string[] needles) =>
    Lines(output)
      .Select(l => l.Trim())
      .Where(l => l.Length > 0 && needles.Any(n => n.Length > 0 && l.Contains(n, StringComparison.OrdinalIgnoreCase)))
      .ToList();

  /// <summary>Caminhos de servidor, sem o sufixo de versão (;C123 / ;X123).</summary>
  public static IReadOnlyList<string> ExtractServerPaths(string output) =>
    ServerPathRegex().Matches(output)
      .Select(m => m.Value.Trim())
      .Where(p => p.Length > 2)
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .ToList();

  /// <summary>Números no início das linhas (listas de changesets do tf merge /candidate).</summary>
  public static IReadOnlySet<int> ParseLeadingNumbers(string output) =>
    Lines(output)
      .Select(l => LeadingNumberRegex().Match(l))
      .Select(m => m.Success && int.TryParse(m.Groups["n"].Value, out var n) ? n : (int?)null)
      .OfType<int>()
      .ToHashSet();

  [GeneratedRegex(@"\$/[^\r\n;]*?(?=;[CX]?\d+|\s+->\s+|\s*\r?$|\s*:\s|\s{2,})", RegexOptions.Multiline)]
  private static partial Regex ServerPathRegex();

  [GeneratedRegex(@"^\s*C?(?<n>\d+)\*?\s")]
  private static partial Regex LeadingNumberRegex();
}
