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
      var commands = ObterCommands();
      try
      {
        new ChainOfCommands(commands).SendCommand(_arg.ToLower());
      }
      catch (InvalidCommandException ex)
      {
        System.Console.WriteLine(ex.Message);
      }

      return this;
    }

    public ICommandChain[] ObterCommands()
    {
      return new ICommandChain[] {
          new GetAll(),
          new GetVersion(),
          new BuildAll(),
          new BuildVersion(),
          new OpenHostConfig(),
          new KillHost(),
          new Merge(),
          new DeleteBroker(),
          new Help(),
          new OpenRm(),
          new KillAll(),
          new Cmd(),
          new Clear(),
          new OpenAlias(),
          new OpenHost(),
          new OpenFront(),
          new Exit(),
          new PoUiDoc(),
          new NotImplemented()
      };
    }
  }
}
