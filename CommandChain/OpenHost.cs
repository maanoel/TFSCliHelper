namespace PEPCliHelper
{
  public class OpenHost : ICommandChain
  {
    public ICommandChain AnotherCommand { get; set; }

    public void Execute(string arguments)
    {
      if (arguments.Contains("open host"))
      {
        new OpenHostBuilder().Build(arguments);
        return;
      }

      AnotherCommand.Execute(arguments);
    }
  }

}
