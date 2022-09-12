namespace TFSCliHelper
{
  public class GetSingleVersionBuilder : ICommandBuilder
  {
    private string TFEXEPATH = @"""C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\TF.exe""";
    private string _pathFrontTfs;
    private string _pathFrontFolder;
    private string _pathSauTfs;
    private string _pathSauFolder;
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
          _pathFrontTfs = StructTFSServerPathFront._32;
          _pathBackFolder = StructVersionsBack._32;
          _pathBackTfs = StructTFSServerPathBack._32;
          _pathSauFolder = StructVersionsSau._32;
          _pathSauTfs = StructTFSServerPathSau._32;
          break;
        case string a when command.EndsWith("33"):
          _pathFrontFolder = StructVersionsFront._33;
          _pathFrontTfs = StructTFSServerPathFront._33;
          _pathBackFolder = StructVersionsBack._33;
          _pathBackTfs = StructTFSServerPathBack._33;
          _pathSauFolder = StructVersionsSau._33;
          _pathSauTfs = StructTFSServerPathSau._33;
          break;
        case string a when command.EndsWith("34"):
          _pathFrontFolder = StructVersionsFront._34;
          _pathFrontTfs = StructTFSServerPathFront._34;
          _pathBackFolder = StructVersionsBack._34;
          _pathBackTfs = StructTFSServerPathBack._34;
          _pathSauFolder = StructVersionsSau._34;
          _pathSauTfs = StructTFSServerPathSau._34;
          break;
        case string a when command.EndsWith("2205"):
          _pathFrontFolder = StructVersionsFront._2205;
          _pathFrontTfs = StructTFSServerPathFront._2205;
          _pathBackFolder = StructVersionsBack._2205;
          _pathBackTfs = StructTFSServerPathBack._2205;
          _pathSauFolder = StructVersionsSau._2205;
          _pathSauTfs = StructTFSServerPathSau._2205;
          break;
        case string a when command.EndsWith("2209"):
          _pathFrontFolder = StructVersionsFront._2209;
          _pathFrontTfs = StructTFSServerPathFront._2209;
          _pathBackFolder = StructVersionsBack._2209;
          _pathBackTfs = StructTFSServerPathBack._2209;
          _pathSauFolder = StructVersionsSau._2209;
          _pathSauTfs = StructTFSServerPathSau._2209;
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
      Executor.AddCommand(new Command("cd", _pathSauFolder));
      Executor.AddCommand(new Command($"{TFEXEPATH} get", _pathSauTfs + recursive));
    }

    private void ExecuteCommands()
    {
      Executor.Execute();
    }
  }
}