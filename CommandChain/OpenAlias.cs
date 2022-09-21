namespace PEPCliHelper
{
  public class OpenAlias : ICommandChain
  {
    public ICommandChain AnotherCommand { get; set; }

    public void Execute(string arguments)
    {
      if (arguments.Contains("open alias"))
      {
        new OpenAliasBuilder().Build(arguments);
        return;
      }

      AnotherCommand.Execute(arguments);
    }
  }

}
