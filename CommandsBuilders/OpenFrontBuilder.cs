namespace PEPCliHelper
{
  public class OpenFrontBuilder : ICommandBuilder
  {
    private string _path;

    public ICommandExecutor Executor { get; private set; }

    public OpenFrontBuilder()
    {
      Executor = new TfsCommandExecutor();
    }

    public void Build(string arguments)
    {
      PrepareCommand(arguments);
      Executor.AddCommand(new Command("code", $"{_path}"));
      Executor.Execute();
    }

    private void PrepareCommand(string argument)
    {
      switch (argument)
      {
        case string a when argument.EndsWith("32"):
          _path = StructVersionsFront._32;
          break;
        case string a when argument.EndsWith("33"):
          _path = StructVersionsFront._33;
          break;
        case string a when argument.EndsWith("34"):
          _path = StructVersionsFront._34;
          break;
        case string a when argument.EndsWith("2205"):
          _path = StructVersionsFront._2205;
          break;
        case string a when argument.EndsWith("2209"):
          _path = StructVersionsFront._2209;
          break;
        case string a when argument.EndsWith("atual"):
          _path = StructVersionsFront.atual;
          break;
        default:
          throw new InvalidCommandException("Command not implemented.");
      }
    }
  }
}