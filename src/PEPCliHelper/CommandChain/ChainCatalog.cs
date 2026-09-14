using PEPCliHelper.Interface;

namespace PEPCliHelper.CommandChain;

/// <summary>Registro de todos os comandos. Para adicionar um comando, crie o chain e inclua aqui.</summary>
public static class ChainCatalog
{
  public static ChainOfCommands Create() => new(
  [
    new MergeChain(),
    new GetAllChain(),
    new GetVersionChain(),
    new BuildChain(),
    new BuildAllChain(),
    new BuildVersionChain(),
    new EnvListChain(),
    new EnvDiscoverChain(),
    new EnvValidateChain(),
    new ConfigAutoChain(),
    new ConfigShowChain(),
    new ConfigValidateChain(),
    new DoctorChain(),
    new WorkspaceListChain(),
    new WorkspaceInspectChain(),
    new PendingListChain(),
    new ChangesetShowChain(),
    new DeleteBrokerChain(),
    new OpenHostChain(),
    new OpenRmChain(),
    new OpenAliasChain(),
    new OpenHostConfigChain(),
    new KillHostChain(),
    new HistoryListChain(),
    new HistoryShowChain(),
    new HelpChain(),
    new VersionChain(),
    .. RemovedChains.All,
  ]);
}
