using System.Collections.Generic;

namespace TFSCliHelper
{
  public class BuildSingleVersionBuilder : ICommandBuilder
  {
    public ICommandExecutor Executor { get; private set; }
    private readonly List<string> _pathSlnLib;
    private readonly List<string> _pathSlnSau;
    private readonly List<string> _pathSlnPep;
    private string _msBuildDirectory = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin";
    private string _pathBack;
    private string _pathSau;
    private string _pathLib;

    public BuildSingleVersionBuilder()
    {
      _pathSlnLib = new List<string> {
        StructLibSlnPath._32,
        StructLibSlnPath._33,
        StructLibSlnPath._34,
        StructLibSlnPath._2205,
        StructLibSlnPath._2209,
      };

      _pathSlnSau = new List<string> {
        StructSauSlnPath._32,
        StructSauSlnPath._33,
        StructSauSlnPath._34,
        StructSauSlnPath._2205,
        StructSauSlnPath._2209,
      };

      _pathSlnPep = new List<string> {
        StructPepSlnPath._32,
        StructPepSlnPath._33,
        StructPepSlnPath._34,
        StructPepSlnPath._2205,
        StructPepSlnPath._2209,
      };

      Executor = new TFSCommandExecutor();
    }

    public void Build(string argument)
    {
      PrepareCommand(argument);
      GoToDirectory();
      BuildLib();
      BuildSau();
      BuildPep();
      ExecuteCommands();
    }

    private void PrepareCommand(string argument)
    {
      switch (argument)
      {
        case string a when argument.EndsWith("32"):
          _pathLib = StructLibSlnPath._32;
          _pathBack = StructPepSlnPath._32;
          _pathSau = StructSauSlnPath._32;
          break;
        case string a when argument.EndsWith("33"):
          _pathLib = StructLibSlnPath._33;
          _pathBack = StructPepSlnPath._33;
          _pathSau = StructSauSlnPath._33;
          break;
        case string a when argument.EndsWith("34"):
          _pathLib = StructLibSlnPath._34;
          _pathBack = StructPepSlnPath._34;
          _pathSau = StructSauSlnPath._34;
          break;
        case string a when argument.EndsWith("2205"):
          _pathLib = StructLibSlnPath._2205;
          _pathBack = StructPepSlnPath._2205;
          _pathSau = StructSauSlnPath._2205;
          break;
        case string a when argument.EndsWith("2209"):
          _pathLib = StructLibSlnPath._2209;
          _pathBack = StructPepSlnPath._2209;
          _pathSau = StructSauSlnPath._2209;
          break;
        default:
          throw new InvalidCommandException("Command not implemented.");
      }
    }

    private void GoToDirectory()
    {
      Executor.AddCommand(new Command($"cd ", _msBuildDirectory));
    }

    private void BuildLib()
    {
      foreach (var path in _pathSlnLib)
      {
        Executor.AddCommand(new Command($"msbuild ", path));
      }
    }

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
