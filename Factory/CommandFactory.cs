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
          new GetSingleVersionBuilder()
          .Build(arg.ToLower());
          break;
        case "open host":
        case "kill host":
          new TaskHostbuilder().Build();
          break;
        default:
          throw new InvalidCommandException("Command not implemented.");
      }
    }
  }
}
