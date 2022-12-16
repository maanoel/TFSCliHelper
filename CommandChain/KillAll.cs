namespace PEPCliHelper
{
  public class Cmd : ICommandChain
  {
    public void Execute(string arguments)
    {
      if (arguments.Contains("cmd"))
        new CmdBuilder().Build(arguments);
    }
  }
}
