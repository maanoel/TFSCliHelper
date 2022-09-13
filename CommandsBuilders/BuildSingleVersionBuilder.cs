using System.Collections.Generic;

namespace TFSCliHelper
{
  public class BuildSingleVersionBuilder : ICommandBuilder
  {
    public ICommandExecutor Executor { get; private set; }
    private string _msBuildDirectory = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin";
    private string _pathBack;
    private string _pathSau;

    public BuildSingleVersionBuilder()
    {
      Executor = new TFSCommandExecutor();
    }

    public void Build(string argument)
    {
      PrepareCommand(argument);
      GoToDirectory();
      BuildSau();
      BuildPep();
      ExecuteCommands();
    }

    private void PrepareCommand(string argument)
    {
      switch (argument)
      {
        case string a when argument.EndsWith("32"):
          _pathBack = StructPepSlnPath._32;
          _pathSau = StructSauSlnPath._32;
          break;
        case string a when argument.EndsWith("33"):
          _pathBack = StructPepSlnPath._33;
          _pathSau = StructSauSlnPath._33;
          break;
        case string a when argument.EndsWith("34"):
          _pathBack = StructPepSlnPath._34;
          _pathSau = StructSauSlnPath._34;
          break;
        case string a when argument.EndsWith("2205"):
          _pathBack = StructPepSlnPath._2205;
          _pathSau = StructSauSlnPath._2205;
          break;
        case string a when argument.EndsWith("2209"):
          _pathBack = StructPepSlnPath._2209;
          _pathSau = StructSauSlnPath._2209;
          break;
        case string a when argument.EndsWith("atual"):
          _pathBack = StructPepSlnPath.atual;
          _pathSau = StructSauSlnPath.atual;
          break;
        default:
          throw new InvalidCommandException("Command not implemented.");
      }
    }

    private void GoToDirectory()
    {
      Executor.AddCommand(new Command($"cd ", _msBuildDirectory));
    }

    private void BuildSau()
    {
      Executor.AddCommand(new Command($"msbuild ", _pathSau));
    }

    private void BuildPep()
    {
      Executor.AddCommand(new Command($"msbuild ", _pathBack));
    }

    private void ExecuteCommands()
    {
      Executor.Execute();
    }
  }
}
