namespace TFSCliHelper
{
  public class TaskHostBuilder : ICommandBuilder
  {
    public ICommandExecutor Executor { get; private set; }

    public TaskHostBuilder()
    {
      Executor = new TFSCommandExecutor();
    }

    public void Build()
    {
      Executor.AddCommand(new Command("taskkill", "/f /im rm.host.exe"));
      Executor.Execute();
    }
  }
}