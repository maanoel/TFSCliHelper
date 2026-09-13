namespace PEPCliHelper.Installer;

/// <summary>Valor do PATH do usuário e se é REG_EXPAND_SZ (preservado na escrita).</summary>
internal sealed record UserPathValue(string Value, bool IsExpandString);

internal sealed record ShortcutSpec(
  string ShortcutPath,
  string TargetPath,
  string Arguments,
  string WorkingDirectory,
  string IconLocation,
  string Description);

/// <summary>Tudo o que o instalador toca fora da pasta de destino (registro, PATH, Menu Iniciar, processos, payload).</summary>
internal interface IInstallEnvironment
{
  string LocalAppData { get; }
  string RoamingAppData { get; }
  string StartMenuPrograms { get; }
  string SystemDirectory { get; }
  string UserProfile { get; }

  Stream? OpenPayload();
  bool IsProcessRunningFrom(string executablePath);
  UserPathValue ReadUserPath();
  void WriteUserPath(UserPathValue value);
  void BroadcastEnvironmentChange();
  void CreateShortcut(ShortcutSpec shortcut);
  void WriteUninstallEntry(string keyPath, IReadOnlyDictionary<string, object> values);
}
