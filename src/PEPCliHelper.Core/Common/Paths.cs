namespace PEPCliHelper.Core.Common;

/// <summary>Operações sobre caminhos de servidor TFVC ($/...).</summary>
public static class TfvcPath
{
  public static bool IsServerPath(string? path) =>
    !string.IsNullOrWhiteSpace(path) && path.StartsWith("$/", StringComparison.Ordinal);

  public static string Normalize(string path) => path.Trim().Replace('\\', '/').TrimEnd('/');

  public static string Combine(string root, string relative)
  {
    var cleanRoot = Normalize(root);
    var cleanRelative = relative.Replace('\\', '/').Trim('/');
    return cleanRelative.Length == 0 ? cleanRoot : $"{cleanRoot}/{cleanRelative}";
  }

  public static bool AreEqual(string a, string b) =>
    string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);

  /// <summary>True se <paramref name="path"/> é igual ou está dentro de <paramref name="parent"/>.</summary>
  public static bool IsUnderOrEqual(string path, string parent)
  {
    var p = Normalize(path);
    var root = Normalize(parent);
    return p.Equals(root, StringComparison.OrdinalIgnoreCase)
      || p.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
  }

  public static string Relative(string path, string parent)
  {
    if (!IsUnderOrEqual(path, parent))
      throw new ArgumentException($"'{path}' não está dentro de '{parent}'.");
    return Normalize(path)[Normalize(parent).Length..].TrimStart('/');
  }
}

/// <summary>Operações sobre caminhos locais Windows.</summary>
public static class LocalPath
{
  public static string Normalize(string path)
  {
    var full = Path.GetFullPath(path.Trim());
    var root = Path.GetPathRoot(full) ?? string.Empty;
    return full.Length > root.Length ? full.TrimEnd('\\', '/') : full;
  }

  public static bool AreEqual(string a, string b) =>
    string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);

  public static bool IsUnderOrEqual(string path, string parent)
  {
    var p = Normalize(path);
    var root = Normalize(parent);
    return p.Equals(root, StringComparison.OrdinalIgnoreCase)
      || p.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
  }

  public static string Relative(string path, string parent)
  {
    if (!IsUnderOrEqual(path, parent))
      throw new ArgumentException($"'{path}' não está dentro de '{parent}'.");
    return Normalize(path)[Normalize(parent).Length..].TrimStart('\\');
  }

  /// <summary>Converte um relativo de servidor ("a/b.cs") para relativo local ("a\b.cs").</summary>
  public static string FromServerRelative(string serverRelative) => serverRelative.Replace('/', '\\');
}
