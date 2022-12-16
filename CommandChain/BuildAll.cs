namespace PEPCliHelper
{

  public class BuildAll : ICommandChain
  {
    public void Execute(string arguments)
    {
      if (arguments.Equals("build all"))
        new BuildAllVersionsBuilder().Build(arguments);
    }
  }
}
