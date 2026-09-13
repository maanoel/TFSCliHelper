namespace PEPCliHelper.Core.Common;

/// <summary>Locais de arquivos do CLI na máquina do usuário.</summary>
public static class AppPaths
{
  public const string ConfigEnvironmentVariable = "PEPCLI_CONFIG";

  public static string ConfigDirectory =>
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PepCli");

  public static string DefaultConfigFile => Path.Combine(ConfigDirectory, "config.json");

  public static string DataDirectory =>
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PepCli");

  public static string HistoryDirectory => Path.Combine(DataDirectory, "history");

  /// <summary>Precedência: argumento explícito → variável PEPCLI_CONFIG → arquivo do usuário.</summary>
  public static string ResolveConfigFile(string? explicitPath, Func<string, string?> readEnvironment)
  {
    if (!string.IsNullOrWhiteSpace(explicitPath))
      return Path.GetFullPath(explicitPath);

    var fromEnvironment = readEnvironment(ConfigEnvironmentVariable);
    if (!string.IsNullOrWhiteSpace(fromEnvironment))
      return Path.GetFullPath(fromEnvironment);

    return DefaultConfigFile;
  }
}
