namespace PEPCliHelper
{
  public class OpenHostConfigBuilder : ICommandBuilder
  {
    private string _path;
    private readonly string _aliasName = "rm.host.exe.config";

    public ICommandExecutor Executor { get; private set; }

    public OpenHostConfigBuilder()
    {
      Executor = new TFSCommandExecutor();
    }

    public void Build(string arguments)
    {
      PrepareCommand(arguments);
      Executor.AddCommand(new Command("start", $"notepad++ {_path}/{_aliasName}"));
      Executor.Execute();
    }

    private void PrepareCommand(string argument)
    {
      switch (argument)
      {
        case string a when argument.EndsWith("32"):
          _path = StructVersionsBin._32;
          break;
        case string a when argument.EndsWith("33"):
          _path = StructVersionsBin._33;
          break;
        case string a when argument.EndsWith("34"):
          _path = StructVersionsBin._34;
          break;
        case string a when argument.EndsWith("2205"):
          _path = StructVersionsBin._2205;
          break;
        case string a when argument.EndsWith("2209"):
          _path = StructVersionsBin._2209;
          break;
        case string a when argument.EndsWith("atual"):
          _path = StructVersionsBin.atual;
          break;
        default:
          throw new InvalidCommandException("Command not implemented.");
      }
    }
  }
}