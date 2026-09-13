using System.Reflection;
using System.Runtime.InteropServices;

namespace PEPCliHelper.Installer;

internal static class Program
{
  public const int ExitSuccess = 0;
  public const int ExitFailure = 1;
  public const int ExitUsage = 2;

  private static bool _consoleAttached;

  public static string Version
  {
    get
    {
      var version = Assembly.GetExecutingAssembly().GetName().Version;
      return version is null ? "0.0.0" : version.ToString(3);
    }
  }

  [STAThread]
  private static int Main(string[] args)
  {
    var arguments = SetupArguments.Parse(args);
    if (arguments.Error is not null)
    {
      WriteToParentConsole(arguments.Error);
      return ExitUsage;
    }

    if (arguments.Silent) return RunSilent(arguments);

    ApplicationConfiguration.Initialize();
    Application.Run(new SetupForm(new Installer(new WindowsInstallEnvironment())));
    return ExitSuccess;
  }

  private static int RunSilent(SetupArguments arguments)
  {
    var installer = new Installer(new WindowsInstallEnvironment());
    var options = new InstallOptions(
      arguments.Destination ?? installer.DefaultDestination,
      arguments.AddToPath,
      arguments.CreateShortcut,
      arguments.RegisterUninstall,
      Version);

    var result = installer.Install(options);
    WriteLog(options.Destination, result);
    WriteToParentConsole(result.Message);
    return result.Success ? ExitSuccess : ExitFailure;
  }

  private static void WriteLog(string destination, InstallResult result)
  {
    var logPath = result.ExecutablePath is not null
      ? Path.Combine(Path.GetDirectoryName(result.ExecutablePath)!, "install.log")
      : Path.Combine(Path.GetTempPath(), "PEPCLI-Setup.log");
    try
    {
      File.WriteAllLines(logPath, result.Log);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
      WriteToParentConsole($"Não foi possível gravar o log em {logPath} (destino {destination}): {ex.Message}");
    }
  }

  /// <summary>WinExe não tem console; em modo silencioso anexa ao terminal que chamou, se houver.</summary>
  private static void WriteToParentConsole(string message)
  {
    _consoleAttached = _consoleAttached || AttachConsole(-1);
    if (!_consoleAttached) return;
    Console.Error.WriteLine();
    Console.Error.WriteLine(message);
  }

  [DllImport("kernel32.dll", SetLastError = true)]
  private static extern bool AttachConsole(int processId);
}
