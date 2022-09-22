namespace PEPCliHelper
{
  public struct StructVersionsBin
  {
    private static string _32 = @"C:\RM\legado\12.1.32\bin\";
    private static string _33 = @"C:\RM\legado\12.1.33\bin\";
    private static string _34 = @"C:\RM\legado\12.1.34\bin\";
    private static string _2205 = @"C:\RM\legado\12.1.2205\bin\";
    private static string _2209 = @"C:\RM\legado\12.1.2209\bin\";
    private static string atual = @"C:\RM\atual\release\bin\";

    public static string GetPath(string param)
    {
      switch (param)
      {
        case string a when param.EndsWith("32"):
          return _32;
        case string a when param.EndsWith("33"):
          return _33;
        case string a when param.EndsWith("34"):
          return _34;
        case string a when param.EndsWith("2205"):
          return _2205;
        case string a when param.EndsWith("2209"):
          return _2209;
        case string a when param.EndsWith("atual"):
          return atual;
        default:
          throw new InvalidCommandException("Command not implemented.");
      }
    }
  }
}

