using System.Collections.Generic;

namespace PEPCliHelper
{
  public interface IStructPath
  {
    string _32 { get; }
    string _33 { get; }
    string _34 { get; }
    string _2205 { get; }
    string _2209 { get; }
    string atual { get; }

    List<string> GetAllVersions()
    {
      return new List<string> {
        _32,
        _33,
        _34,
        _2205,
        _2209,
        atual,
      };
    }

    string GetVersion(string argument)
    {

      switch (argument)
      {
        case string a when argument.EndsWith("32"):
          return _32;
        case string a when argument.EndsWith("33"):
          return _33;
        case string a when argument.EndsWith("34"):
          return _34;
        case string a when argument.EndsWith("2205"):
          return _2205;
        case string a when argument.EndsWith("2209"):
          return _2209;
        case string a when argument.EndsWith("atual"):
          return atual;
        default:
          throw new InvalidCommandException("Command not implemented.");
      }
    }
  }
}
