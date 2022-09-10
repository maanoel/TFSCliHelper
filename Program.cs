namespace TFSCliHelper
{
  class Program
  {
    static void Main(string[] args)
    {
      System.Console.WriteLine("Welcome");
      System.Console.WriteLine("Please type the command you want to execute...");
      var arg = System.Console.ReadLine();

      try
      {
        CommandFactory.Create(arg);
      }
      catch (InvalidCommandException ex)
      {
        System.Console.WriteLine(ex.Message);
      }
    }
  }
}
