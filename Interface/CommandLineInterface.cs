namespace TFSCliHelper
{
  public interface ICommandLineInterface
  {
    void WelcomeMessage();
    ICommandLineInterface WaitCommand();
    void Run();
  }
}
