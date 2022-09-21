namespace PEPCliHelper
{
  public class OpenAliasBuilder : ICommandBuilder
  {
    private string _path;
    private readonly string _aliasName = "alias.dat";

    public ICommandExecutor Executor { get; private set; }

    public OpenAliasBuilder()
    {
      Executor = new TfsCommandExecutor();
    }

    public void Build(string arguments)
    {
      PrepareCommand(arguments);
      Executor.AddCommand(new Command("start", $"notepad++ {_path}/{_aliasName}"));
      Executor.Execute();
    }

    private void PrepareCommand(string argument)
    {
      _path = StructVersionsBin.GetPath(argument);
    }
  }
}