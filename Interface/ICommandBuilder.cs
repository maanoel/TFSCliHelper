using System.Collections.Generic;

namespace TFSCliHelper
{
  public interface ICommandBuilder
  {
    public ICommandExecutor Executor { get; }
  }
}
