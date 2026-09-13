namespace PEPCliHelper.Installer.Tests;

public class UninstallScriptTests
{
  private const string Destination = @"C:\Users\João Silva\AppData\Local\Programs\PepCli";
  private const string Shortcut = @"C:\Users\João Silva\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\PEP CLI.lnk";

  private static string[] Lines() =>
    UninstallScript.Build(Destination, Shortcut).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

  [Fact]
  public void Build_DeletesOnlyTheQuotedDestinationFolder()
  {
    var deletions = Lines().Where(line => line.Contains("rd ", StringComparison.OrdinalIgnoreCase) || line.Contains("rmdir", StringComparison.OrdinalIgnoreCase)).ToList();

    var deletion = Assert.Single(deletions);
    Assert.EndsWith($"rd /s /q \"{Destination}\"", deletion);
  }

  [Fact]
  public void Build_DeletesOnlyTheQuotedShortcutFile()
  {
    var deletes = Lines().Where(line => line.Contains("del ", StringComparison.OrdinalIgnoreCase)).ToList();

    var delete = Assert.Single(deletes);
    Assert.Equal($"if exist \"{Shortcut}\" del /f /q \"{Shortcut}\"", delete);
  }

  [Fact]
  public void Build_NeverDeletesConfigurationFolder()
  {
    var destructive = Lines().Where(line => line.Contains("rd ") || line.Contains("del ") || line.Contains("Remove-Item"));

    Assert.All(destructive, line => Assert.DoesNotContain("PepCli\\", line.Replace(Destination, "")));
    Assert.DoesNotContain(Lines(), line => line.Contains("APPDATA") && !line.StartsWith("echo "));
  }

  [Fact]
  public void Build_RemovesPathEntryAndUninstallKey()
  {
    var lines = Lines();

    Assert.Contains($"set \"PEPCLI_DIR={Destination}\"", lines);
    Assert.Contains(lines, line => line.StartsWith("powershell.exe ") && line.Contains("'Environment'") && line.Contains("$env:PEPCLI_DIR"));
    Assert.Contains(@"reg delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\PepCli"" /f >nul 2>nul", lines);
  }
}
