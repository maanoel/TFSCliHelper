namespace PEPCliHelper
{
  public class OpenHostConfigBuilder : ICommandBuilder
  {
    private string _path;
    private readonly string _aliasName = "rm.host.exe.config";

    public ICommandExecutor Executor { get; private set; }

    public OpenHostConfigBuilder()
    {
      Executor = new TfsCommandExecutor();
    }

    public void Build(string arguments)
    {
      PrepareCommand(arguments);
      Executor.AddCommand(new Command("start", $"notepad++ {_path}/{_aliasName}"));
      Executor.Execute();
    }

    private void PrepareCommand(string argument)
    {
      _path = (new StructVersionsBin() as IStructPath).GetVersion(argument);
    }
  }
}