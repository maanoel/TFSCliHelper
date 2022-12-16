namespace PEPCliHelper
{
  public class BuildVersion : ICommandChain
  {
    public void Execute(string arguments)
    {
      if (arguments.Contains("build version"))
        new BuildSingleVersionBuilder().Build(arguments);
    }
  }
}
