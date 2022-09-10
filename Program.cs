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

    //static void Main(string[] args)
    //{
    //  string myChoice;
    //  Console.WriteLine("Would you Like to Start CMD?");
    //  Console.WriteLine("\nPress Y(Yes) to start theCMD...");
    //  //Storing the User Choice into a string type

    //  myChoice = Console.ReadLine();
    //  //Starting a Switch-Case Condition Cheking
    //  switch (myChoice)
    //  {
    //    //If user Enter Y then Call the
    //    // StartProcess() Method
    //    case "Y":
    //      StartProcess();
    //      break;
    //    default:
    //      //Otherwise just Print The Error Message
    //      Console.WriteLine("You have Enetered the Wrong Key.");
    //      Console.ReadKey();
    //      break;
    //  }
    //}

    //private static void StartProcess()
    //{
    
    //  ProcessStartInfo pro = new ProcessStartInfo();
    //  pro.FileName = "cmd.exe";
    //  pro.RedirectStandardInput = true;
    //  pro.Verb = "runas";

    //  Process proStart = new Process();
    //  proStart.StartInfo = pro;
    //  proStart.Start();
    //  proStart.StandardInput.WriteLine(@"cd C:\RM\legado\12.1.32\Sau-PEP");
    //  proStart.StandardInput.WriteLine(@"""C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\TF.exe"" get $/RM/Legado/12.1.32/FrameHTML/web_src/app/Sau/PEP");
    //}
  }
}
