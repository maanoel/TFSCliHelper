using System.Collections.Generic;

namespace PEPCliHelper
{
  public class BuildAllVersionsBuilder : ICommandBuilder
  {
    public ICommandExecutor Executor { get; private set; }
    private readonly List<string> _pathSlnSau;
    private readonly List<string> _pathSlnPep;
    private string _msBuildDirectory = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin";
    private ICommandBuilder _taskHostBuilder;

    public BuildAllVersionsBuilder()
    {
      _pathSlnSau = StructSauSlnPath.GetAllVersions();
      _pathSlnPep = StructPepSlnPath.GetAllVersions();
      Executor = new TfsCommandExecutor();
      _taskHostBuilder = new TaskHostBuilder();
    }

    public void Build(string arguments)
    {
      KillHost();
      GoToDirectory();
      BuildSau();
      BuildPep();
      ExecuteCommands();
    }

    private void KillHost()
    {
      _taskHostBuilder.Build("");
    }

    private void GoToDirectory()
    {
      Executor.AddCommand(new Command($"cd ", _msBuildDirectory));
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
      foreach (var path in _pathSlnPep)
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
