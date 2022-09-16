namespace PEPCliHelper
{
  public class KillAll : ICommandChain
  {
    public ICommandChain AnotherCommand { get; set; }

    public void Execute(string arguments)
    {
      if (arguments.Equals("kill all"))
      {
        new TaskAllBuilder().Build(arguments);
        return;
      }

      AnotherCommand.Execute(arguments);
    }
  }
}
