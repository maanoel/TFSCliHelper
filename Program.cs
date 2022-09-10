namespace TFSCliHelper
{
  class Program
  {
    static void Main(string[] args)
    {
      System.Console.WriteLine("Welcome");
      System.Console.WriteLine("Please type the command you want to execute...");
      var arg = System.Console.ReadLine();

      CommandFactory.Create()
    }
  }
}
