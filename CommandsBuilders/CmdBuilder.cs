namespace PEPCliHelper
{
  public class CmdBuilder : ICommandBuilder
  {
    public ICommandExecutor Executor { get; private set; }

    public CmdBuilder()
    {
      Executor = new TFSCommandExecutor();
    }

    public void Build(string arguments)
    {
      arguments = arguments.Replace("cmd", "").Trim();

      Executor.AddCommand(new Command(arguments));
      Executor.Execute();
    }
  }
}