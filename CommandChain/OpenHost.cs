namespace PEPCliHelper
{
  public class OpenHost : ICommandChain
  {
    public void Execute(string arguments)
    {
      if (arguments.Contains("open host"))
        new OpenHostBuilder().Build(arguments);
    }
  }
}
