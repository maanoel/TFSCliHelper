namespace PEPCliHelper
{
  public class Cmd : ICommandChain
  {
    public ICommandChain AnotherCommand { get; set; }

    public void Execute(string arguments)
    {
      if (arguments.Contains("cmd"))
      {
        new CmdBuilder().Build(arguments);
        return;
      }

      AnotherCommand.Execute(arguments);
    }
  }
}
