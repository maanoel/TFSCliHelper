namespace PEPCliHelper
{
  public class OpenHostConfig : ICommandChain
  {
    public ICommandChain AnotherCommand { get; set; }

    public void Execute(string arguments)
    {
      if (arguments.StartsWith("open hostconfig"))
      {
        new OpenHostConfigBuilder().Build(arguments);
        return;
      }

      AnotherCommand.Execute(arguments);
    }
  }
}
