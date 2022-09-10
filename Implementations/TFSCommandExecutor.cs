using System;
using System.Collections.Generic;

namespace TFSCliHelper
{
  public class TFSCommandExecutor : ICommandExecutor
  {

    public TFSCommandExecutor()
    {
      Commands = new List<ICommand>();
      Prompt = new CMDWindows();
    }

    public List<ICommand> Commands { get; private set; }

    public IPrompt Prompt { get; private set; }

    public void AddCommand(ICommand command)
    {
      Commands.Add(command);
    }

    public void Execute()
    {
      foreach (var command in Commands)
      {
        var generatedCommand = command.Generate();

        Prompt.Write(generatedCommand);
      }
    }
  }
}
