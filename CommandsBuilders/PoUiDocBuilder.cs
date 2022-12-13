namespace PEPCliHelper
{
  public class PoUiDocBuilder : ICommandBuilder
  {
    public ICommandExecutor Executor { get; private set; }

    public PoUiDocBuilder()
    {
      Executor = new TfsCommandExecutor();
    }
    public void Build(string arguments)
    {
      Executor.AddCommand(new Command("start chrome", "https://po-ui.io/documentation"));
      Executor.Execute();
    }
  }
}
