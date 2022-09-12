using System.Collections.Generic;

namespace TFSCliHelper
{
  public class BuildAllVersionsBuilder : ICommandBuilder
  {
    public ICommandExecutor Executor { get; private set; }
    private readonly List<string> _pathSlnLib;
    private readonly List<string> _pathSlnSau;
    private readonly List<string> _pathSlnPep;
    private string _msBuildDirectory = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin";

    public BuildAllVersionsBuilder()
    {
      _pathSlnLib = new List<string> {
        StructLibSlnPath._32,
        StructLibSlnPath._33,
        StructLibSlnPath._34,
        StructLibSlnPath._2205,
        StructLibSlnPath._2209,
        StructLibSlnPath.atual,
      };

      _pathSlnSau = new List<string> {
        StructSauSlnPath._32,
        StructSauSlnPath._33,
        StructSauSlnPath._34,
        StructSauSlnPath._2205,
        StructSauSlnPath._2209,
        StructSauSlnPath.atual,
      };

      _pathSlnPep = new List<string> {
        StructPepSlnPath._32,
        StructPepSlnPath._33,
        StructPepSlnPath._34,
        StructPepSlnPath._2205,
        StructPepSlnPath._2209,
        StructPepSlnPath.atual,
      };

      Executor = new TFSCommandExecutor();
    }

    public void Build()
    {
      GoToDirectory();
      BuildSau();
      BuildPep();
      ExecuteCommands();
    }

    private void GoToDirectory()
    {
      Executor.AddCommand(new Command($"cd ", _msBuildDirectory));
    }

    //private void BuildLib()
    //{
    //  foreach (var path in _pathSlnLib)
    //  {
    //    Executor.AddCommand(new Command($"msbuild ", path));
    //  }
    //}

    private void BuildSau()
    {
      foreach (var path in _pathSlnSau)
      {
        Executor.AddCommand(new Command($"msbuild ", path));
      }
    }

    private void BuildPep()
    {
      foreach (var path in _pathSlnSau)
      {
        Executor.AddCommand(new Command($"msbuild ", path));
      }
    }

    private void ExecuteCommands()
    {
      Executor.Execute();
    }
  }
}
