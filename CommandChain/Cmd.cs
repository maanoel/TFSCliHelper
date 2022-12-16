namespace PEPCliHelper
{
  public class KillAll : ICommandChain
  {
    public void Execute(string arguments)
    {
      if (arguments.Equals("kill all"))
        new TaskAllBuilder().Build(arguments);
    }
  }
}
