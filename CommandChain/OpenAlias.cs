namespace PEPCliHelper
{
  public class OpenAlias : ICommandChain
  {
    public void Execute(string arguments)
    {
      if (arguments.Contains("open alias"))
        new OpenAliasBuilder().Build(arguments);
    }
  }

}
