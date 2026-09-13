namespace PEPCliHelper.Installer.Tests;

public class SetupArgumentsTests
{
  [Fact]
  public void Parse_NoArguments_GuiModeWithAllOptions()
  {
    var args = SetupArguments.Parse([]);

    Assert.False(args.Silent);
    Assert.Null(args.Destination);
    Assert.True(args.AddToPath && args.CreateShortcut && args.RegisterUninstall);
    Assert.Null(args.Error);
  }

  [Fact]
  public void Parse_SilentWithAllFlags_DisablesOptionsAndReadsDir()
  {
    var args = SetupArguments.Parse(["/silent", @"/dir:C:\Temp\pep test", "/nopath", "/NoShortcut", "/noregistry"]);

    Assert.True(args.Silent);
    Assert.Equal(@"C:\Temp\pep test", args.Destination);
    Assert.False(args.AddToPath);
    Assert.False(args.CreateShortcut);
    Assert.False(args.RegisterUninstall);
    Assert.Null(args.Error);
  }

  [Fact]
  public void Parse_QuotedDirAndDashSyntax_Accepted()
  {
    var args = SetupArguments.Parse(["-silent", "/DIR:\"D:\\pep\""]);

    Assert.True(args.Silent);
    Assert.Equal(@"D:\pep", args.Destination);
  }

  [Theory]
  [InlineData("/unknown")]
  [InlineData("/dir:")]
  public void Parse_InvalidArgument_ReturnsErrorWithUsage(string arg)
  {
    var args = SetupArguments.Parse(["/silent", arg]);

    Assert.NotNull(args.Error);
    Assert.Contains("PEPCLI-Setup.exe", args.Error);
  }
}
