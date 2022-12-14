namespace PEPCliHelper
{
  public class TaskHostBuilder : ICommandBuilder
  {
    public ICommandExecutor Executor { get; private set; }

    public TaskHostBuilder()
    {
      Executor = new TfsCommandExecutor();
    }

    public void Build(string arguments)
    {
      Executor.AddCommand(new Command("taskkill", "/f /im rm.host.exe"));
      Executor.Execute();
    }
  }
}