namespace TFSCliHelper
{
  public interface ICommandLineInterface
  {
    void WelcomeMessage();
    ICommandLineInterface WaitCommand();
    ICommandLineInterface Run();
  }
}
