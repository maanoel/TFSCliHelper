namespace PEPCliHelper
{
  public class ClearBuilder : ICommandBuilder
  {
    public ICommandExecutor Executor { get; private set; }

    public ClearBuilder()
    {
      Executor = new TFSCommandExecutor();
    }

    public void Build(string arguments)
    {
      Executor.AddCommand(new Command("cls"));
      Executor.Execute();
    }
  }
}