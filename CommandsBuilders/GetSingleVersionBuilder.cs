namespace TFSCliHelper
{
  public class GetSingleVersionBuilder : ICommandBuilder
  {
    private string TFEXEPATH = @"""C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\TF.exe""";
    private string _pathFrontTfs;
    private string _pathFrontFolder;
    private string _pathBackTfs;
    private string _pathBackFolder;
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
          _pathFrontFolder = StructVersionsFront._32;
          _pathFrontTfs = TFSServerPathFront._32;
          _pathBackFolder = StructVersionsBack._32;
          _pathBackTfs = StructTFSServerPathBack._32;
          break;
        case string a when command.EndsWith("33"):
          _pathFrontFolder = StructVersionsFront._33;
          _pathFrontTfs = TFSServerPathFront._33;
          _pathBackFolder = StructVersionsBack._33;
          _pathBackTfs = StructTFSServerPathBack._33;
          break;
        case string a when command.EndsWith("34"):
          _pathFrontFolder = StructVersionsFront._34;
          _pathFrontTfs = TFSServerPathFront._34;
          _pathBackFolder = StructVersionsBack._34;
          _pathBackTfs = StructTFSServerPathBack._34;
          break;
        case string a when command.EndsWith("2205"):
          _pathFrontFolder = StructVersionsFront._2205;
          _pathFrontTfs = TFSServerPathFront._2205;
          _pathBackFolder = StructVersionsBack._2205;
          _pathBackTfs = StructTFSServerPathBack._2205;
          break;
        case string a when command.EndsWith("2209"):
          _pathFrontFolder = StructVersionsFront._2209;
          _pathFrontTfs = TFSServerPathFront._2209;
          _pathBackFolder = StructVersionsBack._2209;
          _pathBackTfs = StructTFSServerPathBack._2209;
          break;
        default:
          throw new InvalidCommandException("Command not implemented.");
      }
    }

    private void GetVersionFiles()
    {
      Executor.AddCommand(new Command("cd", _pathFrontFolder));
      Executor.AddCommand(new Command($"{TFEXEPATH} get", _pathFrontFolder + recursive));
      Executor.AddCommand(new Command("cd", _pathBackFolder));
      Executor.AddCommand(new Command($"{TFEXEPATH} get", _pathBackTfs + recursive));
    }

    private void ExecuteCommands()
    {
      Executor.Execute();
    }
  }
}