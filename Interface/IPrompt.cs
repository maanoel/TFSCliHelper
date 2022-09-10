namespace TFSCliHelper
{
  public interface IPrompt
  {
    void Write(string command);
    string Read();
    void Finish();

    void Wait();
  }
}
