namespace TFSCliHelper
{
  public class GetSingleVersionBuilder : ICommandBuilder
  {
    private string TFEXEPATH = @"""C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\TF.exe""";
    private string _versionBack;
    private string _versionFront;
    private readonly string recursive = " /recursive";

    public ICommandExecutor Executor { get; private set; }

    public GetSingleVersionBuilder()
    {
      Executor = new TFSCommandExecutor();
    }

    public void Build(string command)
    {
      PrepareCommand(command);
      GetVersionFiles();
      ExecuteCommands();
    }

    private void PrepareCommand(string command)
    {
      switch (command)
      {
        case string a when command.EndsWith("32"):
          _versionFront = StructVersionsFront._32;
          _versionBack = StructVersionsBack._32;
          break;
        case string a when command.EndsWith("33"):
          _versionFront = StructVersionsFront._33;
          _versionBack = StructVersionsBack._33;
          break;
        case string a when command.EndsWith("34"):
          _versionFront = StructVersionsFront._34;
          _versionBack = StructVersionsBack._34;
          break;
        case string a when command.EndsWith("2205"):
          _versionFront = StructVersionsFront._2205;
          _versionBack = StructVersionsBack._2205;
          break;
        case string a when command.EndsWith("2209"):
          _versionFront = StructVersionsFront._2209;
          _versionBack = StructVersionsBack._2209;
          break;
        default:
          throw new InvalidCommandException("Command not implemented.");
      }
    }

    private void GetVersionFiles()
    {
      Executor.AddCommand(new Command("cd", _versionFront));
      Executor.AddCommand(new Command($"{TFEXEPATH} get", _versionFront + recursive));
      Executor.AddCommand(new Command("cd", _versionBack));
      Executor.AddCommand(new Command($"{_versionFront} get", _versionBack + recursive));
    }

    private void ExecuteCommands()
    {
      Executor.Execute();
    }
  }
}