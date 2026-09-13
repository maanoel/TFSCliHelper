namespace PEPCliHelper.Installer.Tests;

/// <summary>Ambiente falso: pastas temporárias isoladas, PATH/registro/atalho em memória.</summary>
internal sealed class FakeInstallEnvironment : IInstallEnvironment, IDisposable
{
  public FakeInstallEnvironment()
  {
    Root = Path.Combine(Path.GetTempPath(), "pepcli-installer-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(Root);
  }

  public string Root { get; }
  public string LocalAppData => Path.Combine(Root, "Local");
  public string RoamingAppData => Path.Combine(Root, "Roaming");
  public string StartMenuPrograms => Path.Combine(Root, "StartMenu", "Programs");
  public string SystemDirectory => Path.Combine(Root, "System32");
  public string UserProfile => Root;

  public byte[]? Payload { get; set; } = [0x4D, 0x5A, 1, 2, 3];
  public bool PepRunning { get; set; }
  public UserPathValue UserPath { get; set; } = new(@"C:\Tools;%USERPROFILE%\bin", true);
  public int BroadcastCount { get; private set; }
  public List<ShortcutSpec> Shortcuts { get; } = [];
  public Dictionary<string, IReadOnlyDictionary<string, object>> RegistryKeys { get; } = [];

  public Stream? OpenPayload() => Payload is null ? null : new MemoryStream(Payload);
  public bool IsProcessRunningFrom(string executablePath) => PepRunning;
  public UserPathValue ReadUserPath() => UserPath;
  public void WriteUserPath(UserPathValue value) => UserPath = value;
  public void BroadcastEnvironmentChange() => BroadcastCount++;
  public void CreateShortcut(ShortcutSpec shortcut) => Shortcuts.Add(shortcut);
  public void WriteUninstallEntry(string keyPath, IReadOnlyDictionary<string, object> values) => RegistryKeys[keyPath] = values;

  public void Dispose()
  {
    if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
  }
}
