namespace TFSCliHelper
{
  public class OpenHostBuilder : ICommandBuilder
  {
    private string _path;
    public ICommandExecutor Executor { get; private set; }

    public OpenHostBuilder()
    {
      Executor = new TFSCommandExecutor();
    }

    public void Build(string argument)
    {
      PrepareCommand(argument);
      Executor.AddCommand(new Command(_path));
      Executor.Execute();
    }

    private void PrepareCommand(string argument)
    {
      switch (argument)
      {
        case string a when argument.EndsWith("32"):
          _path = StructHostPath._32;
          break;
        case string a when argument.EndsWith("33"):
          _path = StructHostPath._33;
          break;
        case string a when argument.EndsWith("34"):
          _path = StructHostPath._34;
          break;
        case string a when argument.EndsWith("2205"):
          _path = StructHostPath._2205;
          break;
        case string a when argument.EndsWith("2209"):
          _path = StructHostPath._2209;
          break;
        case string a when argument.EndsWith("atual"):
          _path = StructHostPath.atual;
          break;
        default:
          throw new InvalidCommandException("Command not implemented.");
      }
    }
  }
}