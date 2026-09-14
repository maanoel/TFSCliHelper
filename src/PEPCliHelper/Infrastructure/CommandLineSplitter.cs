using System.Text;
using PEPCliHelper.Core.Common;

namespace PEPCliHelper.Infrastructure;

/// <summary>Divide uma linha digitada no prompt 'pep&gt;' em argumentos, respeitando aspas duplas (ex.: caminhos com espaço).</summary>
public static class CommandLineSplitter
{
  public static List<string> Split(string line)
  {
    var tokens = new List<string>();
    var current = new StringBuilder();
    var inQuotes = false;
    var hasToken = false;

    foreach (var c in line)
    {
      if (c == '"')
      {
        inQuotes = !inQuotes;
        hasToken = true;
      }
      else if (char.IsWhiteSpace(c) && !inQuotes)
      {
        if (hasToken)
          tokens.Add(current.ToString());
        current.Clear();
        hasToken = false;
      }
      else
      {
        current.Append(c);
        hasToken = true;
      }
    }

    if (inQuotes)
      throw new UsageException("Aspas não fechadas na linha de comando.", "Feche as aspas, ex.: pep open host \"12.1.2606\".");
    if (hasToken)
      tokens.Add(current.ToString());
    return tokens;
  }
}
