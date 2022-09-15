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
      _versionsFront = new List<string> {
        StructVersionsFront._32,
        StructVersionsFront._33,
        StructVersionsFront._34,
        StructVersionsFront._2205,
        StructVersionsFront._2209,
        StructVersionsFront.atual
      };

      _pathFront = new List<string> {
        StructTFSServerPathFront._32,
        StructTFSServerPathFront._33,
        StructTFSServerPathFront._34,
        StructTFSServerPathFront._2205,
        StructTFSServerPathFront._2209,
        StructTFSServerPathFront.atual,

      };

      _versionsBack = new List<string> {
        StructVersionsBack._32,
        StructVersionsBack._33,
        StructVersionsBack._34,
        StructVersionsBack._2205,
        StructVersionsBack._2209,
        StructVersionsBack.atual,
      };

      _pathBack = new List<string> {
        StructTFSServerPathBack._32,
        StructTFSServerPathBack._33,
        StructTFSServerPathBack._34,
        StructTFSServerPathBack._2205,
        StructTFSServerPathBack._2209,
        StructTFSServerPathBack.atual,
      };

      _versionsSau = new List<string> {
        StructVersionsSau._32,
        StructVersionsSau._33,
        StructVersionsSau._34,
        StructVersionsSau._2205,
        StructVersionsSau._2209,
        StructVersionsSau.atual,
      };

      _pathSau = new List<string> {
        StructTFSServerPathSau._32,
        StructTFSServerPathSau._33,
        StructTFSServerPathSau._34,
        StructTFSServerPathSau._2205,
        StructTFSServerPathSau._2209,
        StructTFSServerPathSau.atual,
      };

      Executor = new TFSCommandExecutor();
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
