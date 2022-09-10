namespace TFSCliHelper
{
  public partial class Program
  {
    static void Main(string[] args)
    {
      new ComandLineInterface()
        .WaitCommand()
        .Run();
    }
  }
}
