namespace PEPCliHelper
{
  public class Clear : ICommandChain
  {
    public void Execute(string arguments)
    {
      if (arguments.Equals("clear") || 
          arguments.Equals("cls"))
      {
        new ClearBuilder().Build(arguments);
        return;
      }
    }
  }
}
