namespace PEPCliHelper
{
  public class KillHost : ICommandChain
  {
    public void Execute(string arguments)
    {
      if (arguments.Equals("kill host"))
        new TaskHostBuilder().Build(arguments);
    }
  }
}
