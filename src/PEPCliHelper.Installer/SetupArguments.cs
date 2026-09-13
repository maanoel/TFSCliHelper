namespace PEPCliHelper.Installer;

/// <summary>Argumentos de linha de comando: <c>/silent [/dir:&lt;pasta&gt;] [/nopath] [/noshortcut] [/noregistry]</c>.</summary>
internal sealed record SetupArguments(
  bool Silent,
  string? Destination,
  bool AddToPath,
  bool CreateShortcut,
  bool RegisterUninstall,
  string? Error)
{
  public const string Usage = "Uso: PEPCLI-Setup.exe [/silent] [/dir:<pasta>] [/nopath] [/noshortcut] [/noregistry]";

  public static SetupArguments Parse(IEnumerable<string> args)
  {
    var silent = false;
    string? destination = null;
    var addToPath = true;
    var createShortcut = true;
    var registerUninstall = true;

    foreach (var raw in args)
    {
      var arg = raw.Trim();
      if (arg.Length == 0) continue;
      var normalized = arg.Length > 1 && arg[0] == '-' ? "/" + arg.TrimStart('-') : arg;

      if (normalized.StartsWith("/dir:", StringComparison.OrdinalIgnoreCase))
      {
        destination = normalized["/dir:".Length..].Trim().Trim('"');
        if (destination.Length == 0) return Invalid("Informe a pasta em /dir:<pasta>.");
        continue;
      }

      switch (normalized.ToLowerInvariant())
      {
        case "/silent": silent = true; break;
        case "/nopath": addToPath = false; break;
        case "/noshortcut": createShortcut = false; break;
        case "/noregistry": registerUninstall = false; break;
        default: return Invalid($"Argumento desconhecido: {raw}");
      }
    }

    return new SetupArguments(silent, destination, addToPath, createShortcut, registerUninstall, null);
  }

  private static SetupArguments Invalid(string message) =>
    new(true, null, false, false, false, message + Environment.NewLine + Usage);
}
