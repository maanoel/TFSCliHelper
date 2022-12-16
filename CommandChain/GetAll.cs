namespace PEPCliHelper
{

  public class GetAll : ICommandChain
  {
    public void Execute(string arguments)
    {
      if (arguments.Equals("get all"))
        new GetAllVersionsBuilder().Build(arguments);
    }
  }
}
