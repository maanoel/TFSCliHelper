using System.Collections.Generic;

namespace PEPCliHelper
{
  public interface ICommandBuilder
  {
    ICommandExecutor Executor { get; }
    void Build(string arguments);
  }
}
