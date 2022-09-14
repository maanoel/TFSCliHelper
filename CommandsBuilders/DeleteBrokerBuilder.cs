namespace PEPCliHelper
{
  public class DeleteBrokerBuilder : ICommandBuilder
  {
    private string _pathBin;
    private readonly string _broker = "_Broker.dat";

    public ICommandExecutor Executor { get; private set; }

    public DeleteBrokerBuilder()
    {
      Executor = new TFSCommandExecutor();
    }
    public void Build(string arguments)
    {
      PrepareCommand(arguments);
      Executor.AddCommand(new Command("del", $"{_pathBin}{_broker}" ));
      Executor.Execute();
    }

    private void PrepareCommand(string argument)
    {
      switch (argument)
      {
        case string a when argument.EndsWith("32"):
          _pathBin = StructVersionsBin._32;
          break;
        case string a when argument.EndsWith("33"):
          _pathBin = StructVersionsBin._33;
          break;
        case string a when argument.EndsWith("34"):
          _pathBin = StructVersionsBin._34;
          break;
        case string a when argument.EndsWith("2205"):
          _pathBin = StructVersionsBin._2205;
          break;
        case string a when argument.EndsWith("2209"):
          _pathBin = StructVersionsBin._2209;
          break;
        case string a when argument.EndsWith("atual"):
          _pathBin = StructVersionsBin.atual;
          break;
        default:
          throw new InvalidCommandException("Command not implemented.");
      }
    }
  }
}
