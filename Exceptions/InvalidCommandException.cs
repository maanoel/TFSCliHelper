using System;

namespace PEPCliHelper
{
  public class InvalidCommandException : Exception
  {
    public InvalidCommandException(string message) : base(message)
    {

    }
  }
}
