using System.Collections.Generic;

namespace TFSCliHelper
{
  public interface ICommandBuilder
  {
    ICommandExecutor Executor { get; }
    void Build(string arguments);
  }
}
