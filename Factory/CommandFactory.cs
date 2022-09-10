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
        default:
          throw new InvalidCommandException("Command not implemented.");
      }
    }
  }
}
