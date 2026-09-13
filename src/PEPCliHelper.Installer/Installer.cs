namespace PEPCliHelper.Installer;

internal sealed record InstallOptions(
  string Destination,
  bool AddToPath,
  bool CreateShortcut,
  bool RegisterUninstall,
  string Version);

internal sealed record InstallResult(bool Success, string Message, bool Updated, string? ExecutablePath, IReadOnlyList<string> Log);

/// <summary>Instala o pep.exe por usuário. Não depende de UI; efeitos externos passam por <see cref="IInstallEnvironment"/>.</summary>
internal sealed class Installer(IInstallEnvironment environment)
{
  public const string ExecutableName = "pep.exe";
  public const string ShortcutName = "PEP CLI.lnk";
  public const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\PepCli";
  public const string Publisher = "Equipe PEP RM";

  private readonly List<string> _log = [];

  public string DefaultDestination => Path.Combine(environment.LocalAppData, "Programs", "PepCli");

  public string ShortcutPath => Path.Combine(environment.StartMenuPrograms, ShortcutName);

  public static bool IsExistingInstallation(string destination)
  {
    try
    {
      return Path.IsPathFullyQualified(destination) && File.Exists(Path.Combine(destination, ExecutableName));
    }
    catch (ArgumentException)
    {
      return false; // Caminho digitado ainda incompleto/inválido.
    }
  }

  /// <summary>Retorna a pasta normalizada ou uma mensagem de erro (em <paramref name="error"/>).</summary>
  public string? ValidateDestination(string? destination, out string? error)
  {
    error = null;
    var raw = destination?.Trim().Trim('"') ?? string.Empty;
    if (raw.Length == 0) return Fail("Informe a pasta de destino.", out error);
    if (raw.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || raw.IndexOfAny(['%', ';', '"', '*', '?', '<', '>', '|']) >= 0)
      return Fail($"A pasta \"{raw}\" contém caracteres não permitidos (% ; \" * ? < > |). Escolha outra pasta.", out error);
    if (!Path.IsPathFullyQualified(raw))
      return Fail($"Use um caminho completo, por exemplo {DefaultDestination}.", out error);

    string full;
    try
    {
      full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(raw));
    }
    catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
    {
      return Fail($"Caminho inválido: {raw} ({ex.Message}).", out error);
    }

    if (string.Equals(full, Path.TrimEndingDirectorySeparator(Path.GetPathRoot(full) ?? string.Empty), StringComparison.OrdinalIgnoreCase))
      return Fail("Não instale na raiz de uma unidade. Use uma pasta própria, por exemplo " + DefaultDestination + ".", out error);

    foreach (var protectedDir in ProtectedDirectories())
    {
      if (IsSameOrInside(full, protectedDir))
        return Fail($"A pasta \"{full}\" guarda configuração/histórico do PEP CLI e não pode ser usada como destino.", out error);
    }

    if (File.Exists(full)) return Fail($"\"{full}\" é um arquivo, não uma pasta.", out error);

    if (Directory.Exists(full) && !File.Exists(Path.Combine(full, ExecutableName)) && Directory.EnumerateFileSystemEntries(full).Any())
      return Fail($"A pasta \"{full}\" já tem arquivos e não é uma instalação do PEP CLI. Escolha uma pasta nova ou vazia (a desinstalação remove a pasta inteira).", out error);

    return full;
  }

  public InstallResult Install(InstallOptions options)
  {
    _log.Clear();
    Log($"PEP CLI {options.Version} - instalação iniciada em {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

    var destination = ValidateDestination(options.Destination, out var error);
    if (destination is null) return Failure(error!, updated: false, executable: null);

    var executable = Path.Combine(destination, ExecutableName);
    var updated = File.Exists(executable);
    Log($"Destino: {destination} ({(updated ? "atualização" : "nova instalação")})");

    if (environment.IsProcessRunningFrom(executable))
      return Failure($"O pep está em execução a partir de \"{destination}\". Feche os terminais que estão usando o 'pep' e tente novamente. Nada foi alterado.", updated, null);

    using (var payload = environment.OpenPayload())
    {
      if (payload is null)
        return Failure("Este instalador não contém o pep.exe. Gere-o novamente com scripts\\build-installer.ps1. Nada foi alterado.", updated, null);

      var copyError = CopyExecutable(payload, destination, executable);
      if (copyError is not null) return Failure(copyError, updated, null);
    }

    var stepError = RunOptionalSteps(options, destination, executable);
    if (stepError is not null) return Failure(stepError, updated, executable);

    var message = updated ? "Atualizado. Abra um novo terminal e digite: pep" : "Instalado. Abra um novo terminal e digite: pep";
    Log(message);
    return new InstallResult(true, message, updated, executable, _log.ToList());
  }

  private string? CopyExecutable(Stream payload, string destination, string executable)
  {
    var staging = executable + ".new";
    try
    {
      Directory.CreateDirectory(destination);
      using (var output = new FileStream(staging, FileMode.Create, FileAccess.Write, FileShare.None))
        payload.CopyTo(output);
      File.Move(staging, executable, overwrite: true);
      File.WriteAllText(Path.Combine(destination, UninstallScript.FileName), UninstallScript.Build(destination, ShortcutPath));
      Log($"Copiado: {executable}");
      Log($"Gerado: {Path.Combine(destination, UninstallScript.FileName)}");
      return null;
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
      TryDelete(staging);
      return $"Não foi possível gravar o pep.exe em \"{destination}\": {ex.Message} Verifique se o 'pep' não está aberto e se você tem permissão na pasta, e tente de novo.";
    }
  }

  private string? RunOptionalSteps(InstallOptions options, string destination, string executable)
  {
    var step = string.Empty;
    try
    {
      if (options.AddToPath)
      {
        step = "adicionar a pasta ao PATH do usuário";
        AddToUserPath(destination);
      }
      if (options.CreateShortcut)
      {
        step = "criar o atalho no Menu Iniciar";
        CreateStartMenuShortcut(destination, executable);
      }
      if (options.RegisterUninstall)
      {
        step = "registrar a desinstalação em Aplicativos instalados";
        RegisterUninstallEntry(destination, executable, options.Version);
      }
      return null;
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException
      or InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException)
    {
      return $"O pep.exe foi instalado em \"{destination}\", mas falhou ao {step}: {ex.Message} " +
        "Execute o instalador novamente ou conclua esse passo manualmente.";
    }
  }

  private void AddToUserPath(string destination)
  {
    var current = environment.ReadUserPath();
    if (UserPathEntries.Contains(current.Value, destination))
    {
      Log("PATH do usuário já contém o destino.");
      return;
    }
    environment.WriteUserPath(current with { Value = UserPathEntries.Add(current.Value, destination) });
    environment.BroadcastEnvironmentChange();
    Log("PATH do usuário atualizado.");
  }

  private void CreateStartMenuShortcut(string destination, string executable)
  {
    var powershell = Path.Combine(environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
    environment.CreateShortcut(new ShortcutSpec(
      ShortcutPath,
      powershell,
      PowerShellArguments.ForPep(executable),
      environment.UserProfile,
      executable + ",0",
      "PEP CLI"));
    Log($"Atalho criado: {ShortcutPath} (pasta {destination})");
  }

  private void RegisterUninstallEntry(string destination, string executable, string version)
  {
    var values = new Dictionary<string, object>
    {
      ["DisplayName"] = "PEP CLI",
      ["DisplayVersion"] = version,
      ["Publisher"] = Publisher,
      ["InstallLocation"] = destination,
      ["DisplayIcon"] = executable,
      ["UninstallString"] = UninstallCommand(destination),
      ["NoModify"] = 1,
      ["NoRepair"] = 1,
    };
    environment.WriteUninstallEntry(UninstallKeyPath, values);
    Log($"Desinstalação registrada: HKCU\\{UninstallKeyPath}");
  }

  // Aspas duplas externas: cmd /c remove o par externo e preserva o caminho entre aspas mesmo com espaços ou parênteses.
  public static string UninstallCommand(string destination) =>
    $"cmd.exe /c \"\"{Path.Combine(destination, UninstallScript.FileName)}\"\"";

  private IEnumerable<string> ProtectedDirectories()
  {
    yield return Path.Combine(environment.RoamingAppData, "PepCli");
    yield return Path.Combine(environment.LocalAppData, "PepCli");
  }

  private static bool IsSameOrInside(string path, string directory)
  {
    var dir = Path.TrimEndingDirectorySeparator(directory);
    return string.Equals(path, dir, StringComparison.OrdinalIgnoreCase)
      || path.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
  }

  private InstallResult Failure(string message, bool updated, string? executable)
  {
    Log("ERRO: " + message);
    return new InstallResult(false, message, updated, executable, _log.ToList());
  }

  private void Log(string line) => _log.Add(line);

  private static string? Fail(string message, out string? error)
  {
    error = message;
    return null;
  }

  private static void TryDelete(string path)
  {
    try
    {
      if (File.Exists(path)) File.Delete(path);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
      // Arquivo temporário .new pode ficar para trás; é sobrescrito na próxima instalação.
    }
  }
}

internal static class PowerShellArguments
{
  /// <summary>Argumentos para abrir o PowerShell executando o pep instalado pelo caminho completo.</summary>
  public static string ForPep(string executable, string? pepArguments = null)
  {
    var quoted = "'" + executable.Replace("'", "''") + "'";
    var command = pepArguments is null ? $"& {quoted}" : $"& {quoted} {pepArguments}";
    return $"-NoExit -Command \"{command}\"";
  }
}
