namespace PEPCliHelper.Installer.Tests;

public class UserPathEntriesTests
{
  private const string Dir = @"C:\Users\x\AppData\Local\Programs\PepCli";

  [Fact]
  public void Add_EmptyPath_ReturnsOnlyDirectory()
  {
    Assert.Equal(Dir, UserPathEntries.Add("", Dir));
    Assert.Equal(Dir, UserPathEntries.Add(null, Dir));
  }

  [Fact]
  public void Add_NewDirectory_AppendsPreservingEntriesAndOrder()
  {
    Assert.Equal(@"C:\B;%USERPROFILE%\a;" + Dir, UserPathEntries.Add(@"C:\B;%USERPROFILE%\a", Dir));
  }

  [Fact]
  public void Add_PathWithTrailingSeparator_DoesNotDoubleSeparator()
  {
    Assert.Equal(@"C:\B;" + Dir, UserPathEntries.Add(@"C:\B;", Dir));
  }

  [Theory]
  [InlineData(@"C:\B;C:\USERS\X\APPDATA\LOCAL\PROGRAMS\PEPCLI")]
  [InlineData(@"C:\Users\x\AppData\Local\Programs\PepCli\;C:\B")]
  [InlineData(@"C:\B; C:\Users\x\AppData\Local\Programs\PepCli ")]
  public void Add_AlreadyPresentIgnoringCaseAndTrailingSlash_ReturnsUnchanged(string current)
  {
    Assert.True(UserPathEntries.Contains(current, Dir));
    Assert.Equal(current, UserPathEntries.Add(current, Dir));
  }

  [Fact]
  public void Remove_MatchingEntries_RemovesOnlyThemAndKeepsOrder()
  {
    var current = @"C:\A;C:\users\x\appdata\local\programs\pepcli\;C:\B;C:\Users\x\AppData\Local\Programs\PepCli;C:\C";
    Assert.Equal(@"C:\A;C:\B;C:\C", UserPathEntries.Remove(current, Dir));
  }

  [Fact]
  public void Remove_SimilarPrefix_IsNotRemoved()
  {
    var current = @"C:\A;C:\Users\x\AppData\Local\Programs\PepCli2";
    Assert.Equal(current, UserPathEntries.Remove(current, Dir));
  }

  [Fact]
  public void Remove_NotPresentOrEmpty_ReturnsUnchanged()
  {
    Assert.Equal(@"C:\A;;C:\B;", UserPathEntries.Remove(@"C:\A;;C:\B;", Dir));
    Assert.Equal("", UserPathEntries.Remove("", Dir));
    Assert.Equal("", UserPathEntries.Remove(null, Dir));
  }
}
