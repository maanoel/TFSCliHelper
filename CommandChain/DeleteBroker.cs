namespace PEPCliHelper
{
  public class DeleteBroker : ICommandChain
  {
    public void Execute(string arguments)
    {
      if (arguments.Contains("delete broker"))
        new DeleteBrokerBuilder().Build(arguments);
    }
  }
}
