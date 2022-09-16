namespace PEPCliHelper
{
  public class TaskAllBuilder : ICommandBuilder
  {
    public ICommandExecutor Executor { get; private set; }

    public TaskAllBuilder()
    {
      Executor = new TFSCommandExecutor();
    }

    public void Build(string arguments)
    {
      Executor.AddCommand(new Command("taskkill", "/f /im rm.*"));
      Executor.Execute();
    }
  }
}