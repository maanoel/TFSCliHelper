using System.Collections.Generic;

namespace TFSCliHelper
{
  public interface ICommandExecutor
  {
    public IPrompt Prompt { get; }
    public List<ICommand> Commands { get; }
    void AddCommand(ICommand command);
    void Execute();
  }
}
