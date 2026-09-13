namespace PEPCliHelper.Installer.Tests;

public sealed class InstallerTests : IDisposable
{
  private readonly FakeInstallEnvironment _env = new();
  private readonly Installer _installer;

  public InstallerTests() => _installer = new Installer(_env);

  public void Dispose() => _env.Dispose();

  private string Destination => Path.Combine(_env.Root, "Programs", "PepCli");

  private InstallOptions Options(bool path = true, bool shortcut = true, bool registry = true) =>
    new(Destination, path, shortcut, registry, "2.0.0");

  [Fact]
  public void DefaultDestination_IsUnderLocalAppDataPrograms()
  {
    Assert.Equal(Path.Combine(_env.LocalAppData, "Programs", "PepCli"), _installer.DefaultDestination);
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData(@"relativo\pep")]
  [InlineData(@"C:\")]
  [InlineData(@"C:\pep;C:\outro")]
  [InlineData(@"C:\%TEMP%\pep")]
  public void ValidateDestination_InvalidInput_ReturnsError(string destination)
  {
    Assert.Null(_installer.ValidateDestination(destination, out var error));
    Assert.False(string.IsNullOrWhiteSpace(error));
  }

  [Fact]
  public void ValidateDestination_ConfigFolder_IsRefused()
  {
    Assert.Null(_installer.ValidateDestination(Path.Combine(_env.RoamingAppData, "PepCli"), out var error));
    Assert.Contains("configuração", error);
    Assert.Null(_installer.ValidateDestination(Path.Combine(_env.LocalAppData, "PepCli", "bin"), out _));
  }

  [Fact]
  public void ValidateDestination_NonEmptyFolderWithoutPep_IsRefused()
  {
    Directory.CreateDirectory(Destination);
    File.WriteAllText(Path.Combine(Destination, "documento.txt"), "x");

    Assert.Null(_installer.ValidateDestination(Destination, out var error));
    Assert.Contains("não é uma instalação do PEP CLI", error);
  }

  [Fact]
  public void ValidateDestination_NewOrExistingInstall_ReturnsNormalizedPath()
  {
    Assert.Equal(Destination, _installer.ValidateDestination(Destination + "\\", out _));

    Directory.CreateDirectory(Destination);
    File.WriteAllText(Path.Combine(Destination, "pep.exe"), "old");
    File.WriteAllText(Path.Combine(Destination, "outro.txt"), "x");
    Assert.Equal(Destination, _installer.ValidateDestination($"\"{Destination}\"", out var error));
    Assert.Null(error);
  }

  [Fact]
  public void Install_PepRunningFromDestination_RefusesWithoutChanges()
  {
    _env.PepRunning = true;

    var result = _installer.Install(Options());

    Assert.False(result.Success);
    Assert.Contains("Feche", result.Message);
    Assert.False(Directory.Exists(Destination));
    Assert.Empty(_env.RegistryKeys);
  }

  [Fact]
  public void Install_MissingPayload_FailsWithClearMessage()
  {
    _env.Payload = null;

    var result = _installer.Install(Options());

    Assert.False(result.Success);
    Assert.Contains("build-installer.ps1", result.Message);
    Assert.False(Directory.Exists(Destination));
  }

  [Fact]
  public void Install_AllOptions_CopiesExeAndConfiguresPathShortcutRegistry()
  {
    var result = _installer.Install(Options());

    Assert.True(result.Success, result.Message);
    Assert.False(result.Updated);
    var exe = Path.Combine(Destination, "pep.exe");
    Assert.Equal(_env.Payload, File.ReadAllBytes(exe));
    Assert.False(File.Exists(exe + ".new"));
    Assert.True(File.Exists(Path.Combine(Destination, "uninstall.cmd")));

    Assert.Equal(@"C:\Tools;%USERPROFILE%\bin;" + Destination, _env.UserPath.Value);
    Assert.True(_env.UserPath.IsExpandString);
    Assert.Equal(1, _env.BroadcastCount);

    var shortcut = Assert.Single(_env.Shortcuts);
    Assert.Equal(Path.Combine(_env.StartMenuPrograms, "PEP CLI.lnk"), shortcut.ShortcutPath);
    Assert.EndsWith("powershell.exe", shortcut.TargetPath);
    Assert.Equal($"-NoExit -Command \"& '{exe}'\"", shortcut.Arguments);

    var entry = _env.RegistryKeys[Installer.UninstallKeyPath];
    Assert.Equal("PEP CLI", entry["DisplayName"]);
    Assert.Equal("2.0.0", entry["DisplayVersion"]);
    Assert.Equal("Equipe PEP RM", entry["Publisher"]);
    Assert.Equal(Destination, entry["InstallLocation"]);
    Assert.Equal(exe, entry["DisplayIcon"]);
    Assert.Equal($"cmd.exe /c \"\"{Path.Combine(Destination, "uninstall.cmd")}\"\"", entry["UninstallString"]);
    Assert.Equal(1, entry["NoModify"]);
    Assert.Equal(1, entry["NoRepair"]);
  }

  [Fact]
  public void Install_SecondRun_UpdatesWithoutDuplicatingPath()
  {
    Assert.True(_installer.Install(Options()).Success);
    var pathAfterFirst = _env.UserPath.Value;
    _env.Payload = [9, 9, 9];

    var result = _installer.Install(Options());

    Assert.True(result.Success, result.Message);
    Assert.True(result.Updated);
    Assert.Equal(pathAfterFirst, _env.UserPath.Value);
    Assert.Equal(1, _env.BroadcastCount);
    Assert.Equal([9, 9, 9], File.ReadAllBytes(Path.Combine(Destination, "pep.exe")));
  }

  [Fact]
  public void Install_OptionsDisabled_TouchesOnlyDestination()
  {
    var originalPath = _env.UserPath;

    var result = _installer.Install(Options(path: false, shortcut: false, registry: false));

    Assert.True(result.Success, result.Message);
    Assert.Same(originalPath, _env.UserPath);
    Assert.Equal(0, _env.BroadcastCount);
    Assert.Empty(_env.Shortcuts);
    Assert.Empty(_env.RegistryKeys);
    Assert.False(Directory.Exists(_env.RoamingAppData));
  }

  [Fact]
  public void PowerShellArguments_PathWithApostrophe_IsEscaped()
  {
    Assert.Equal("-NoExit -Command \"& 'C:\\O''Brien\\pep.exe' version\"", PowerShellArguments.ForPep(@"C:\O'Brien\pep.exe", "version"));
  }
}
