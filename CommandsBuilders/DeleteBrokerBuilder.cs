namespace PEPCliHelper
{
  public class DeleteBrokerBuilder : ICommandBuilder
  {
    private string _pathBin;
    private readonly string _broker = "_Broker.dat";

    public ICommandExecutor Executor { get; private set; }

    public DeleteBrokerBuilder()
    {
      Executor = new TfsCommandExecutor();
    }
    public void Build(string arguments)
    {
      PrepareCommand(arguments);
      Executor.AddCommand(new Command("del", $"{_pathBin}{_broker}" ));
      Executor.Execute();
    }

    private void PrepareCommand(string argument)
    {
      _pathBin = StructVersionsBin.GetPath(argument);
    }
  }
}
