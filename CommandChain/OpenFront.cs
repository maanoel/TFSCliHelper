namespace PEPCliHelper
{
  public class OpenFront : ICommandChain
  {
    public ICommandChain AnotherCommand { get; set; }

    public void Execute(string arguments)
    {
      if (arguments.Contains("open front"))
      {
        new OpenFrontBuilder().Build(arguments);
        return;
      }

      AnotherCommand.Execute(arguments);
    }
  }

}
