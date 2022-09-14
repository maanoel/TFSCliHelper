namespace PEPCliHelper
{
  public interface IPrompt
  {
    void Write(string command);
    void Finish();
    void Wait();
  }
}
