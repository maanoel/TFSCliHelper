namespace PEPCliHelper
{
  public class OpenRmBuilder : ICommandBuilder
  {
    private string _path;
    private readonly string _rmName = "rm.exe";

    public ICommandExecutor Executor { get; private set; }

    public OpenRmBuilder()
    {
      Executor = new TfsCommandExecutor();
    }

    public void Build(string arguments)
    {
      PrepareCommand(arguments);
      Executor.AddCommand(new Command($"{_path}/{_rmName}"));
      Executor.Execute();
    }

    private void PrepareCommand(string argument)
    {
      _path = StructVersionsBin.GetPath(argument);
    }
  }
}