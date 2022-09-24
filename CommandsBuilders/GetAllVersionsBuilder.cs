using System.Collections.Generic;

namespace PEPCliHelper
{
  public class GetAllVersionsBuilder : ICommandBuilder
  {
    private string TFEXEPATH = @"""C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\TF.exe""";
    private readonly string recursive = " /recursive";

    public ICommandExecutor Executor { get; private set; }
    private readonly List<string> _versionsFront;
    private readonly List<string> _versionsBack;
    private readonly List<string> _pathFront;
    private readonly List<string> _pathBack;
    private readonly List<string> _versionsSau;
    private readonly List<string> _pathSau;

    public GetAllVersionsBuilder()
    {
      _versionsFront = StructVersionsFront.GetAllVersions();
      _pathFront = StructTfsServerPathFront.GetAllVersions();
      _versionsBack = StructVersionsBack.GetAllVersions();
      _pathBack = StructTfsServerPathBack.GetAllVersions();
      _versionsSau = StructVersionsSau.GetAllVersions();
      _pathSau = StructTfsServerPathSau.GetAllVersions();

      Executor = new TfsCommandExecutor();
    }

    public void Build(string arguments)
    {
      GetFrontFiles();
      GetBackFiles();
      GetSauFiles();
      ExecuteCommands();
    }

    private void GetFrontFiles()
    {
      for (int i = 0; i < _versionsFront.Count; i++)
      {
        Executor.AddCommand(new Command("cd", _versionsFront[i]));
        Executor.AddCommand(new Command($"{TFEXEPATH} get", _pathFront[i] + recursive));
      }
    }

    private void GetBackFiles()
    {
      for (int i = 0; i < _versionsBack.Count; i++)
      {
        Executor.AddCommand(new Command("cd", _versionsBack[i]));
        Executor.AddCommand(new Command($"{TFEXEPATH} get", _pathBack[i] + recursive));
      }
    }

    private void GetSauFiles()
    {
      for (int i = 0; i < _versionsSau.Count; i++)
      {
        Executor.AddCommand(new Command("cd", _versionsSau[i]));
        Executor.AddCommand(new Command($"{TFEXEPATH} get", _pathSau[i] + recursive));
      }
    }

    private void ExecuteCommands()
    {
      Executor.Execute();
    }
  }
}
