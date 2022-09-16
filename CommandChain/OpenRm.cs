namespace PEPCliHelper
{
  public class OpenRm : ICommandChain
  {
    public ICommandChain AnotherCommand { get; set; }

    public void Execute(string arguments)
    {
      if (arguments.Contains("open rm"))
      {
        new OpenRmBuilder().Build(arguments);
        return;
      }

      AnotherCommand.Execute(arguments);
    }
  }

}
