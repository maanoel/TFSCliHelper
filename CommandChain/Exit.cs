namespace PEPCliHelper
{
  public class Exit : ICommandChain
  {
    public void Execute(string arguments)
    {
      if (arguments.Equals("exit"))
        new ExitBuilder().Build(arguments);
    }
  }
}
