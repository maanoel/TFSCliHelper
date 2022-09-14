namespace PEPCliHelper
{
  public interface ICommand
  {
    string Name { get; }
    string Value { get; }
    string Generate();
  }
}
