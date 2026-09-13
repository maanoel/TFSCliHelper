using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PEPCliHelper.Installer;

/// <summary>Implementação real: registro HKCU, WScript.Shell, processos e recurso embutido.</summary>
internal sealed class WindowsInstallEnvironment : IInstallEnvironment
{
  public const string PayloadResourceName = "payload.pep.exe";

  public string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
  public string RoamingAppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
  public string StartMenuPrograms => Environment.GetFolderPath(Environment.SpecialFolder.Programs);
  public string SystemDirectory => Environment.SystemDirectory;
  public string UserProfile => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

  public Stream? OpenPayload() =>
    Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResourceName);

  public bool IsProcessRunningFrom(string executablePath)
  {
    var name = Path.GetFileNameWithoutExtension(executablePath);
    foreach (var process in Process.GetProcessesByName(name))
    {
      using (process)
      {
        if (string.Equals(TryGetPath(process), executablePath, StringComparison.OrdinalIgnoreCase)) return true;
      }
    }
    return false;
  }

  public UserPathValue ReadUserPath()
  {
    using var key = Registry.CurrentUser.OpenSubKey("Environment");
    if (key?.GetValue("Path", null, RegistryValueOptions.DoNotExpandEnvironmentNames) is not string value)
      return new UserPathValue(string.Empty, true);
    return new UserPathValue(value, key.GetValueKind("Path") != RegistryValueKind.String);
  }

  public void WriteUserPath(UserPathValue value)
  {
    using var key = Registry.CurrentUser.CreateSubKey("Environment", writable: true);
    key.SetValue("Path", value.Value, value.IsExpandString ? RegistryValueKind.ExpandString : RegistryValueKind.String);
  }

  public void BroadcastEnvironmentChange() =>
    SendMessageTimeout(new IntPtr(0xFFFF), 0x001A, IntPtr.Zero, "Environment", 0x0002, 5000, out _);

  public void CreateShortcut(ShortcutSpec shortcut)
  {
    var shellType = Type.GetTypeFromProgID("WScript.Shell")
      ?? throw new InvalidOperationException("WScript.Shell não está disponível nesta máquina.");
    dynamic shell = Activator.CreateInstance(shellType)!;
    try
    {
      Directory.CreateDirectory(Path.GetDirectoryName(shortcut.ShortcutPath)!);
      dynamic link = shell.CreateShortcut(shortcut.ShortcutPath);
      try
      {
        link.TargetPath = shortcut.TargetPath;
        link.Arguments = shortcut.Arguments;
        link.WorkingDirectory = shortcut.WorkingDirectory;
        link.IconLocation = shortcut.IconLocation;
        link.Description = shortcut.Description;
        link.Save();
      }
      finally
      {
        Marshal.FinalReleaseComObject(link);
      }
    }
    finally
    {
      Marshal.FinalReleaseComObject(shell);
    }
  }

  public void WriteUninstallEntry(string keyPath, IReadOnlyDictionary<string, object> values)
  {
    using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
    foreach (var (name, value) in values)
    {
      if (value is int number) key.SetValue(name, number, RegistryValueKind.DWord);
      else key.SetValue(name, value.ToString()!, RegistryValueKind.String);
    }
  }

  private static string? TryGetPath(Process process)
  {
    try
    {
      return process.MainModule?.FileName;
    }
    catch (Win32Exception)
    {
      return null; // Processo de outro usuário/elevado: não é o pep deste usuário.
    }
    catch (InvalidOperationException)
    {
      return null; // Processo encerrou durante a verificação.
    }
  }

  [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
  private static extern IntPtr SendMessageTimeout(
    IntPtr hWnd, uint msg, IntPtr wParam, string lParam, uint flags, uint timeout, out IntPtr result);
}
