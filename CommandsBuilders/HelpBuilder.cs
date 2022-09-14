using System;

namespace PEPCliHelper
{
  public class HelpBuilder : ICommandBuilder
  {

    public ICommandExecutor Executor { get; private set; }

    public HelpBuilder()
    {
    }

    public void Build(string commands)
    {
      BreakLine();
      Console.Write(@"Examples of avalible commands: ");
      BreakLine();
      WriteBlue("   get all ");
      Console.Write("build Sau-PEP, Sau-Saude and FrameHTML on all versions");
      BreakLine();
      WriteBlue("   get version X ");
      Console.Write("get Sau-PEP, Sau-Saude and FrameHTML on specified version e.g. get version 2302");
      BreakLine();
      WriteBlue("   build all ");
      Console.Write("build Sau-PEP and Sau-Saude on all versions.");
      BreakLine();
      WriteBlue("   build version X ");
      Console.Write("build Sau-PEP and Sau-Saude on specified version e.g. build version 2209");
      BreakLine();
      WriteBlue("   kill host ");
      Console.Write("kill rm.host.exe that is running.");
      BreakLine();
      WriteBlue("   open host X ");
      Console.Write("open rm.host.exe e.g open host 2209");
      BreakLine();
      WriteBlue("   merge version X ");
      Console.Write("merge a changeset to all versions. Form this command with four parameters 'merge project version changeset' e.g merge back 32 81021 ");
      Console.Write("Check in first in a version to use this command, the version used in version parameter is the versions of check in");
      BreakLine();
      BreakLine();
    }

    private void BreakLine()
    {
      Console.WriteLine("");
    }

    private void WriteBlue(string text) {

      Console.ForegroundColor = ConsoleColor.Blue;
      Console.Write(text);
      Console.ResetColor();
    }

  }
}