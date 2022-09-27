namespace PEPCliHelper
{
  public class BuildSingleVersionBuilder : ICommandBuilder
  {
    public ICommandExecutor Executor { get; private set; }
    private readonly string _msBuildDirectory = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin";
    private readonly ICommandBuilder _taskHostBuilder;
    private string _pathBack;
    private string _pathSau;

    public BuildSingleVersionBuilder()
    {
      Executor = new TfsCommandExecutor();
      _taskHostBuilder = new TaskHostBuilder();
    }

    public void Build(string arguments)
    {
      KillHost();
      PrepareCommand(arguments);
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
      _pathBack = (new StructPepSlnPath() as IStructPath).GetVersion(argument);
      _pathSau = (new StructSauSlnPath() as IStructPath).GetVersion(argument);
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
