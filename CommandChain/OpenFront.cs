namespace PEPCliHelper
{
  public class OpenFront : ICommandChain
  {
    public void Execute(string arguments)
    {
      if (arguments.Contains("open front"))
        new OpenFrontBuilder().Build(arguments);
    }
  }
}
