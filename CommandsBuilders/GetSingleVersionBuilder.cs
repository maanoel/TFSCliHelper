namespace PEPCliHelper
{
  public class GetSingleVersionBuilder : ICommandBuilder
  {
    private string _pathFrontTfs;
    private string _pathFrontFolder;
    private string _pathSauTfs;
    private string _pathSauFolder;
    private string _pathBackTfs;
    private string _pathBackFolder;
    private readonly string recursive = " /recursive";
    private readonly string TFEXEPATH = @"""C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\TF.exe""";

    public ICommandExecutor Executor { get; private set; }

    public GetSingleVersionBuilder()
    {
      Executor = new TfsCommandExecutor();
    }

    public void Build(string arguments)
    {
      PrepareCommand(arguments);
      GetVersionFiles();
      ExecuteCommands();
    }

    private void PrepareCommand(string command)
    {
      _pathFrontFolder = StructVersionsFront.GetVersion(command);
      _pathFrontTfs = StructTfsServerPathFront.GetVersion(command);
      _pathBackFolder = StructVersionsBack.GetVersion(command);
      _pathBackTfs = StructTfsServerPathBack.GetVersion(command);
      _pathSauFolder = StructVersionsSau.GetVersion(command);
      _pathSauTfs = StructTfsServerPathSau.GetVersion(command);
    }

    private void GetVersionFiles()
    {
      Executor.AddCommand(new Command("cd", _pathFrontFolder));
      Executor.AddCommand(new Command($"{TFEXEPATH} get", _pathFrontTfs + recursive));
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