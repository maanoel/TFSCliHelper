namespace TFSCliHelper
{
  public static class CommandFactory
  {
    public static void Create(string arg)
    {
      switch (arg.ToLower())
      {
        case "get all":
          new GetAllVersionsBuilder().Build();
          break;
        case string a when arg.ToLower().Contains("get version"):
          new GetSingleVersionBuilder()
          .Build(arg.ToLower());
          break;
        case "build all":
          new BuildAllVersionsBuilder().Build();
          break;
        case string a when arg.ToLower().Contains("build version"):
          new BuildSingleVersionBuilder()
          .Build(arg.ToLower());
          break;
        case string a when arg.ToLower().Contains("open host"):
          new OpenHostBuilder().Build(arg.ToLower());
          break;
        case "kill host":
          new TaskHostBuilder().Build();
          break;
        case string a when arg.ToLower().Contains("merge"):
          new MergeBuilder().Build(arg.ToLower());
          break;
        default:
          throw new InvalidCommandException("Command not implemented.");
      }
    }
  }
}
