using System.Collections.Generic;

namespace PEPCliHelper
{
  public class BuildAllVersionsBuilder : ICommandBuilder
  {
    public ICommandExecutor Executor { get; private set; }
    private readonly List<string> _pathSlnSau;
    private readonly List<string> _pathSlnPep;
    private readonly string _msBuildDirectory = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin";
    private readonly ICommandBuilder _taskHostBuilder;

    public BuildAllVersionsBuilder()
    {
      Executor = new TfsCommandExecutor();
      _pathSlnSau = (new StructSauSlnPath() as IStructPath).GetAllVersions();
      _pathSlnPep = (new StructPepSlnPath() as IStructPath).GetAllVersions();
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
