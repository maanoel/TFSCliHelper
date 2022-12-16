namespace PEPCliHelper
{
  public class Merge : ICommandChain
  {
    public void Execute(string arguments)
    {
      if (arguments.Contains("merge"))
        new MergeBuilder().Build(arguments);
    }
  }
}
