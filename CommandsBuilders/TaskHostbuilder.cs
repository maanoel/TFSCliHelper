namespace TFSCliHelper
{
  public class TaskHostbuilder : ICommandBuilder
  {
    public ICommandExecutor Executor { get; private set; }

    public TaskHostbuilder()
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