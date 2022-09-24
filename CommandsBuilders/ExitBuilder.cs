namespace PEPCliHelper
{
  public class ExitBuilder : ICommandBuilder
  {
    public ICommandExecutor Executor { get; private set; }

    public ExitBuilder()
    {
      Executor = new TfsCommandExecutor();
    }

    public void Build(string arguments)
    {
      Executor.AddCommand(new Command("exit"));
      Executor.Execute();
    }
  }
}