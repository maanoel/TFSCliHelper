namespace PEPCliHelper
{
  public class BuildSingleVersionBuilder : ICommandBuilder
  {
    public ICommandExecutor Executor { get; private set; }
    private string _msBuildDirectory = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin";
    private string _pathBack;
    private string _pathSau;
    private ICommandBuilder _taskHostBuilder;

    public BuildSingleVersionBuilder()
    {
      Executor = new TfsCommandExecutor();
      _taskHostBuilder = new TaskHostBuilder();
    }

    public void Build(string argument)
    {
      KillHost();
      PrepareCommand(argument);
      GoToDirectory();
      BuildSau();
      BuildPep();
      ExecuteCommands();
    }

    private void KillHost()
    {
      _taskHostBuilder.Build("");
    }

    private void PrepareCommand(string argument)
    {
      _pathBack = StructPepSlnPath.GetVersion(argument);
      _pathSau = StructSauSlnPath.GetVersion(argument);
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
