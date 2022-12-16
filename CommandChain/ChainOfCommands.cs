namespace PEPCliHelper
{
  public class ChainOfCommands
  {
    private readonly ICommandChain[] _commands;

    public ChainOfCommands(ICommandChain[] commands)
    {
      _commands = commands;
    }

    public void SendCommand(string arguments)
    {
      foreach (var command in _commands)
      {
        command.Execute(arguments);
      }
    }
  }
}
