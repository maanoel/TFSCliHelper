using System;
using System.Collections.Generic;

namespace PEPCliHelper
{
  public struct StructSauSlnPath
  {
    private static string _32 = @"C:\RM\legado\12.1.32\Sau-Saude\Sau-Saude.sln";
    private static string _33 = @"C:\RM\legado\12.1.33\Sau-Saude\Sau-Saude.sln";
    private static string _34 = @"C:\RM\legado\12.1.34\Sau-Saude\Sau-Saude.sln";
    private static string _2205 = @"C:\RM\legado\12.1.2205\Sau-Saude\Sau-Saude.sln";
    private static string _2209 = @"C:\RM\legado\12.1.2209\Sau-Saude\Sau-Saude.sln";
    private static string atual = @"C:\RM\atual\release\Sau-Saude\Sau-Saude.sln";

    public static List<string> GetAllVersions()
    {
      return new List<string>{
        _32,
        _33,
        _34,
        _2205,
        _2209,
        atual,
      };
    }

    public static string GetVersion(string argument)
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

