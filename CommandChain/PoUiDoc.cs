namespace PEPCliHelper
{
  public class PoUiDoc : ICommandChain
  {
    public ICommandChain AnotherCommand { get; set; }

    public void Execute(string arguments)
    {
      if (arguments.Contains("open podoc"))
      {
        new PoUiDocBuilder().Build(arguments);
        return;
      }

      AnotherCommand.Execute(arguments);
    }
  }
}
