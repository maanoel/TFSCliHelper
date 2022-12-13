namespace PEPCliHelper
{
  public partial class Program
  {
    static void Main(string[] args)
    {
      AskCommand();
    }

    public static void AskCommand()
    {
      new ComandLineInterface()
      .WaitCommand()
      .Run();

      AskCommand();
    }
  }
}
