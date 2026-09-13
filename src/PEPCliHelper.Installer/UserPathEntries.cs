namespace PEPCliHelper.Installer;

/// <summary>Operações puras sobre o valor do PATH do usuário (entradas separadas por ';').</summary>
internal static class UserPathEntries
{
  public static bool Contains(string? pathValue, string directory)
  {
    var target = Normalize(directory);
    return Split(pathValue).Any(entry => Matches(entry, target));
  }

  public static string Add(string? pathValue, string directory)
  {
    var entry = directory.Trim();
    if (string.IsNullOrWhiteSpace(pathValue)) return entry;
    if (Contains(pathValue, directory)) return pathValue;
    return pathValue.EndsWith(';') ? pathValue + entry : pathValue + ";" + entry;
  }

  public static string Remove(string? pathValue, string directory)
  {
    if (string.IsNullOrEmpty(pathValue)) return string.Empty;
    var target = Normalize(directory);
    var entries = Split(pathValue);
    var kept = entries.Where(entry => !Matches(entry, target)).ToList();
    return kept.Count == entries.Length ? pathValue : string.Join(';', kept);
  }

  private static string[] Split(string? pathValue) =>
    string.IsNullOrEmpty(pathValue) ? [] : pathValue.Split(';');

  private static bool Matches(string entry, string normalizedTarget)
  {
    if (string.IsNullOrWhiteSpace(entry) || normalizedTarget.Length == 0) return false;
    return string.Equals(Normalize(entry), normalizedTarget, StringComparison.OrdinalIgnoreCase)
      || string.Equals(Normalize(Environment.ExpandEnvironmentVariables(entry)), normalizedTarget, StringComparison.OrdinalIgnoreCase);
  }

  private static string Normalize(string value) => value.Trim().Trim('"').TrimEnd('\\', '/');
}
