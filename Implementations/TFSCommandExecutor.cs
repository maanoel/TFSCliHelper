using System;
using System.Collections.Generic;

namespace PEPCliHelper
{
  public class TfsCommandExecutor : ICommandExecutor
  {

    public TfsCommandExecutor()
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
