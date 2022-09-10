using System;

namespace TFSCliHelper
{
  public class InvalidCommandException : Exception
  {
    public InvalidCommandException(string message) : base(message)
    {

    }
  }
}
