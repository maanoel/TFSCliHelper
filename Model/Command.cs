using System;

namespace PEPCliHelper
{
  public class Command: ICommand
  {
    public Command(string name, string value)
    {
      if (string.IsNullOrEmpty(name)) {
        throw new ArgumentException("Nome do comando não definido.");
      }

      if (string.IsNullOrEmpty(value))
      {
        throw new ArgumentException("Valor do comando não definido.");
      }

      Name = name;
      Value = value;
    }

    public Command(string value)
    {
      if (string.IsNullOrEmpty(value))
      {
        throw new ArgumentException("Valor do comando não definido.");
      }

      Value = value;
    }

    public string Name { get;  private set; }
    public string Value { get; private set; }

    public string Generate()
    {
      return $"{Name} {Value}";
    }
  }
}
