namespace PEPCliHelper
{
  public interface ICommandChain
  {
    ICommandChain AnotherCommand { get; set; }

    void Execute(string arguments);
  }
}
