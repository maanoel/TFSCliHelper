namespace PEPCliHelper
{
  public class Help : ICommandChain
  {
    public void Execute(string arguments)
    {
      if (arguments.Equals("help"))
        new HelpBuilder().Build(arguments);
    }
  }
}
