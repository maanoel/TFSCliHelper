using System.Collections.Generic;

namespace TFSCliHelper
{
  public class GetAllVersionsBuilder : ICommandBuilder
  {
    private string TFEXEPATH = @"""C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\TF.exe""";
    private readonly string recursive = " /recursive";


    public ICommandExecutor Executor { get; private set; }
    private readonly List<string> _versionsFront;
    private readonly List<string> _versionsBack;

    public GetAllVersionsBuilder()
    {
      _versionsFront = new List<string> {
        StructVersionsFront._32,
        StructVersionsFront._33,
        StructVersionsFront._34,
        StructVersionsFront._2205,
        StructVersionsFront._2209,
      };

      _versionsBack = new List<string> {
        StructVersionsBack._32,
        StructVersionsBack._33,
        StructVersionsBack._34,
        StructVersionsBack._2205,
        StructVersionsBack._2209,
      };

      Executor = new TFSCommandExecutor();
    }

    public void Build()
    {
      GetFrontFiles();
      GetBackFiles();
      ExecuteCommands();
    }

    private void GetFrontFiles()
    {
      foreach (var version in _versionsFront)
      {
        Executor.AddCommand(new Command("cd", version));
        Executor.AddCommand(new Command($"{TFEXEPATH} get", version + recursive));
      }
    }

    private void GetBackFiles()
    {
      foreach (var version in _versionsBack)
      {
        Executor.AddCommand(new Command("cd", version));
        Executor.AddCommand(new Command($"{TFEXEPATH} get", version + recursive));
      }
    }
    private void ExecuteCommands()
    {
      Executor.Execute();
    }
  }
}
