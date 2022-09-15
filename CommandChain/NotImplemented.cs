namespace PEPCliHelper
{
  public class NotImplemented : ICommandChain
  {
    public ICommandChain AnotherCommand { get; set; }

    public void Execute(string arguments)
    {
      System.Console.WriteLine("O comando não existe ou está incorreto.");
    }
  }
}
