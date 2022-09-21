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
      var openHostConfig = new OpenHostConfig();
      var killHost = new KillHost();
      var merge = new Merge();
      var deleteBroker = new DeleteBroker();
      var help = new Help();
      var openRm = new OpenRm();
      var killAll = new KillAll();
      var cmd = new Cmd();
      var clear = new Clear();
      var openAlias = new OpenAlias();
      var openHost = new OpenHost();
      var openFront = new OpenFront();
      var notImplemented = new NotImplemented();

      getAll.AnotherCommand = getVersion;
      getVersion.AnotherCommand = buildAll;
      buildAll.AnotherCommand = buildVersion;
      buildVersion.AnotherCommand = openHostConfig;
      openHostConfig.AnotherCommand = killHost;
      killHost.AnotherCommand = merge;
      merge.AnotherCommand = deleteBroker;
      deleteBroker.AnotherCommand = help;
      help.AnotherCommand = openRm;
      openRm.AnotherCommand = killAll;
      killAll.AnotherCommand = cmd;
      cmd.AnotherCommand = clear;
      clear.AnotherCommand = openAlias;
      openAlias.AnotherCommand = openHost;
      openHost.AnotherCommand = openFront;
      openFront.AnotherCommand = notImplemented;

      getAll.Execute(arguments);
    }
  }
}
