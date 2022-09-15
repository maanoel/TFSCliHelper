namespace PEPCliHelper
{
  public class ChainOfCommands
  {
    public void SendCommand(string arguments)
    {
      var getAll = new GetAll();
      var getVersion = new GetVersion();
      var buildAll = new BuildAll();
      var buildVersion = new BuildVersion();
      var openHost = new OpenHost();
      var killHost = new KillHost();
      var merge = new Merge();
      var deleteBroker = new DeleteBroker();
      var help = new Help();
      var notImplemented = new NotImplemented();

      getAll.AnotherCommand = getVersion;
      getVersion.AnotherCommand = buildAll;
      buildAll.AnotherCommand = buildVersion;
      buildVersion.AnotherCommand = openHost;
      openHost.AnotherCommand = killHost;
      killHost.AnotherCommand = merge;
      merge.AnotherCommand = deleteBroker;
      merge.AnotherCommand = help;
      help.AnotherCommand = notImplemented;

      getAll.Execute(arguments);
    }
  }
}
