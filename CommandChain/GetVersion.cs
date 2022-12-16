namespace PEPCliHelper
{
  public class GetVersion : ICommandChain
  {
    public void Execute(string arguments)
    {
      if (arguments.Contains("get version"))
        new GetSingleVersionBuilder().Build(arguments);
    }
  }
}
