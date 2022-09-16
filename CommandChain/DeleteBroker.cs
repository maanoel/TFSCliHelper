namespace PEPCliHelper
{
  public class DeleteBroker : ICommandChain
  {
    public ICommandChain AnotherCommand { get; set; }

    public void Execute(string arguments)
    {
      if (arguments.Contains("delete broker"))
      {
        new DeleteBrokerBuilder().Build(arguments);
        return;
      }

      AnotherCommand.Execute(arguments);
    }
  }
}
