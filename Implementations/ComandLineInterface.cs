namespace PEPCliHelper
{
  public class ComandLineInterface : ICommandLineInterface
  {
    private string _arg = "";

    public void WelcomeMessage()
    {
      System.Console.WriteLine("Welcome");
    }

    public ICommandLineInterface WaitCommand()
    {
      System.Console.WriteLine("Digite help para ver a lista de comandos disponíveis");
      System.Console.WriteLine("Digite o comando que você deseja executar...");

      _arg = System.Console.ReadLine();
      return this;
    }

    public ICommandLineInterface Run()
    {
      try
      {
        new ChainOfCommands().SendCommand(_arg.ToLower());
      }
      catch (InvalidCommandException ex)
      {
        System.Console.WriteLine(ex.Message);
      }

      return this;
    }
  }
}
