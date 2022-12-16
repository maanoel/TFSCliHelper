namespace PEPCliHelper
{
  public class OpenHostConfig : ICommandChain
  {
    public void Execute(string arguments)
    {
      if (arguments.StartsWith("open hostconfig"))
        new OpenHostConfigBuilder().Build(arguments);
    }
  }
}
