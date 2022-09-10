namespace TFSCliHelper
{
  public interface IPrompt
  {
    string Write(string command);
    void Finish();
    void Wait();
  }
}
