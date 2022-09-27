namespace PEPCliHelper
{

  public class BuildAll : ICommandChain
  {
    public ICommandChain AnotherCommand { get; set; }

    public void Execute(string arguments)
    {
      if (arguments.Equals("build all"))
      {
        new BuildAllVersionsBuilder().Build(arguments);
        return;
      }

      AnotherCommand.Execute(arguments);
    }
  }
}
