using System.Collections.Generic;

namespace PEPCliHelper
{
  public interface ICommandExecutor
  {
    public IPrompt Prompt { get; }
    public List<ICommand> Commands { get; }
    void AddCommand(ICommand command);
    void Execute();
  }
}
