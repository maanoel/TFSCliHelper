﻿namespace TFSCliHelper
{
  public static class CommandFactory
  {
    public static ICommandBuilder Create(string arg)
    {
      switch (arg.ToLower())
      {
        case "get all":
          return new GetAllVersionsBuilder();
        case string a when arg.ToLower().Contains("get version"):
          return  new GetSingleVersionBuilder();
        case "build all":
          return new BuildAllVersionsBuilder();
        case string a when arg.ToLower().Contains("build version"):
          return new BuildSingleVersionBuilder();
        case string a when arg.ToLower().Contains("open host"):
          return new OpenHostBuilder();
        case "kill host":
          return new TaskHostBuilder();
        case string a when arg.ToLower().Contains("merge"):
          return new MergeBuilder();
        case "help":
          return new HelpBuilder();
        default:
          throw new InvalidCommandException("Command not implemented.");
      }
    }
  }
}
