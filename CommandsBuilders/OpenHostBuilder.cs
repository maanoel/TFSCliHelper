namespace PEPCliHelper
{
  public class OpenHostBuilder : ICommandBuilder
  {
    private string _path;
    private readonly string _hostName = "rm.host.exe";

    public ICommandExecutor Executor { get; private set; }

    public OpenHostBuilder()
    {
      Executor = new TfsCommandExecutor();
    }

    public void Build(string arguments)
    {
      PrepareCommand(arguments);
      Executor.AddCommand(new Command($"{_path}/{_hostName}"));
      Executor.Execute();
    }

    private void PrepareCommand(string argument)
    {
      _path = StructVersionsBin.GetPath(argument);
    }
  }
}