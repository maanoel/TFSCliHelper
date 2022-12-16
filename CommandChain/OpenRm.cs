namespace PEPCliHelper
{
  public class OpenRm : ICommandChain
  {
    public void Execute(string arguments)
    {
      if (arguments.Contains("open rm"))
        new OpenRmBuilder().Build(arguments);
    }
  }
}
