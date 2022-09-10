namespace TFSCliHelper
{
  public partial class Program
  {
    static void Main(string[] args)
    {
      while (true) {

        new ComandLineInterface()
        .WaitCommand()
        .Run();
      }
    }
  }
}
