using PEPCliHelper.Core.Catalog;
using PEPCliHelper.Core.Configuration;
using PEPCliHelper.Core.Tfvc;

namespace PEPCliHelper.Tests.Fakes;

/// <summary>Cenário padrão: atual + 2606 + 2602, projetos back e sau, em C:\LR (nunca a pasta real da equipe).</summary>
public static class TestData
{
  public const string Root = @"C:\LR";
  public const int Changeset = 861799;

  public static PepConfig Config(int legacyCount = 2)
  {
    var config = PepConfig.CreateDefault();
    config.LocalRoot = Root;
    config.Versions.Add(new VersionConfig { Id = "12.1.2610", IsCurrent = true, LocalPath = @"Atual\Release", ServerPath = "$/Linha-RM/atual/release", Aliases = ["2610"] });
    string[] legacy = ["12.1.2606", "12.1.2602", "12.1.2510", "12.1.2506", "12.1.2502"];
    foreach (var id in legacy.Take(legacyCount))
      config.Versions.Add(new VersionConfig { Id = id, LocalPath = $@"Legado\{id}", ServerPath = $"$/Linha-RM/Legado/{id}", Aliases = [id[5..]] });
    return config;
  }

  public static VersionCatalog Catalog(int legacyCount = 2) => VersionCatalog.FromConfig(Config(legacyCount));

  public static string Local(string id, string project = "Sau-PEP") =>
    id == "12.1.2610" ? $@"{Root}\Atual\Release\{project}" : $@"{Root}\Legado\{id}\{project}";

  public static string Server(string id, string project = "Sau-PEP") =>
    id == "12.1.2610" ? $"$/Linha-RM/atual/release/{project}" : $"$/Linha-RM/Legado/{id}/{project}";

  /// <summary>TFVC falso com mapeamentos válidos, changeset com um arquivo do Sau-PEP e candidato em todos os destinos.</summary>
  public static FakeTfvcClient HealthyTfvc(InMemoryFileSystem fileSystem, params string[] targets)
  {
    var tfvc = new FakeTfvcClient
    {
      Changeset = new ChangesetInfo(Changeset, ["Changeset: 861799"], [$"{Server("12.1.2610")}/RM.Pep.Api/Controller.cs"]),
    };

    foreach (var id in targets.Prepend("12.1.2610"))
    {
      var versionLocal = id == "12.1.2610" ? $@"{Root}\Atual\Release" : $@"{Root}\Legado\{id}";
      var versionServer = id == "12.1.2610" ? "$/Linha-RM/atual/release" : $"$/Linha-RM/Legado/{id}";
      tfvc.Workfolds[versionLocal] = new WorkfoldInfo($"WS-{id}", @"DOM\dev", "https://tfs/col", [new WorkspaceMapping(versionServer, versionLocal, false)]);
      fileSystem.AddDirectory(Local(id));
      fileSystem.AddDirectory(Local(id, "Sau-Saude"));
      fileSystem.AddFile($@"{Local(id)}\RM.Pep.Api\Controller.cs");
      tfvc.Candidates[Server(id)] = [Changeset];
      tfvc.PendingAddedByMerge[Local(id)] = [$@"Local item : [PC] {Local(id)}\RM.Pep.Api\Controller.cs"];
    }

    return tfvc;
  }
}
