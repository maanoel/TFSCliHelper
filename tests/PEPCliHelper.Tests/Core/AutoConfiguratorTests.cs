using PEPCliHelper.Core.Catalog;
using PEPCliHelper.Core.Configuration;
using PEPCliHelper.Core.Environments;
using PEPCliHelper.Tests.Fakes;

namespace PEPCliHelper.Tests.Core;

public class AutoConfiguratorTests
{
  private const string Root = @"C:\AUTO";

  private static PepConfig BaseConfig()
  {
    var config = PepConfig.CreateDefault();
    config.LocalRoot = Root;
    return config;
  }

  private static InMemoryFileSystem Disk(params string[] legacy)
  {
    var fileSystem = new InMemoryFileSystem();
    fileSystem.AddDirectory($@"{Root}\Atual\Release\Sau-PEP");
    foreach (var name in legacy)
      fileSystem.AddDirectory($@"{Root}\Legado\{name}\Sau-PEP");
    return fileSystem;
  }

  [Fact]
  public void Propose_LegadasForaDeOrdem_OrdenaNumericamenteEAtivaAsQuatroMaisNovas()
  {
    var fileSystem = Disk("12.1.2510", "12.1.34", "12.1.2606", "12.1.2306", "12.1.2602", "12.1.2310");

    var proposal = new AutoConfigurator(fileSystem).Propose(BaseConfig());

    Assert.True(proposal.CanApply);
    Assert.Equal(["12.1.2606", "12.1.2602", "12.1.2510", "12.1.2310"], proposal.ActiveLegacy.Select(v => v.Id));
    // System.Version compara segmento a segmento: 12.1.34 < 12.1.2306.
    Assert.Equal(["12.1.2306", "12.1.34"], proposal.InactiveLegacy.Select(v => v.Id));
    Assert.All(proposal.InactiveLegacy, v => Assert.False(v.Active));
    Assert.Contains(proposal.Warnings, w => w.Contains("desativadas", StringComparison.Ordinal));
  }

  [Fact]
  public void Propose_CenarioPadrao_GeraAtualComCaminhosRelativosEConvencaoTfvc()
  {
    var proposal = new AutoConfigurator(Disk("12.1.2606")).Propose(BaseConfig());

    var current = proposal.Config.Versions.Single(v => v.IsCurrent);
    Assert.Equal("atual", current.Id);
    Assert.True(current.Active);
    Assert.Empty(current.Aliases);
    Assert.Equal(@"Atual\Release", current.LocalPath);
    Assert.Equal("$/Linha-RM/Atual/Release", current.ServerPath);

    var legacy = proposal.Config.Versions.Single(v => v.Id == "12.1.2606");
    Assert.Equal(@"Legado\12.1.2606", legacy.LocalPath);
    Assert.Equal("$/Linha-RM/Legado/12.1.2606", legacy.ServerPath);
    Assert.Equal(["2606"], legacy.Aliases);
    Assert.Empty(ConfigValidator.Validate(proposal.Config));
    Assert.Equal("12.1.2606", VersionCatalog.FromConfig(proposal.Config).ResolveVersion("2606").Version!.Id);
  }

  [Fact]
  public void Propose_PastasSemNomeDeVersao_SaoIgnoradasComMotivo()
  {
    var fileSystem = Disk("12.1.2606");
    fileSystem.AddDirectory($@"{Root}\Legado\backup");
    fileSystem.AddDirectory($@"{Root}\Legado\12.1.2606-old");

    var proposal = new AutoConfigurator(fileSystem).Propose(BaseConfig());

    Assert.Equal(["12.1.2606"], proposal.ActiveLegacy.Select(v => v.Id));
    Assert.Equal(2, proposal.Ignored.Count);
    Assert.Contains(proposal.Ignored, i => i.Folder.EndsWith("backup", StringComparison.Ordinal) && i.Reason.Length > 0);
    Assert.Contains(proposal.Ignored, i => i.Folder.EndsWith("12.1.2606-old", StringComparison.Ordinal));
  }

  [Fact]
  public void Propose_MenosDeQuatroLegadas_AvisaENaoFalha()
  {
    var proposal = new AutoConfigurator(Disk("12.1.2606", "12.1.2602")).Propose(BaseConfig());

    Assert.True(proposal.CanApply);
    Assert.Equal(2, proposal.ActiveLegacy.Count);
    Assert.Empty(proposal.InactiveLegacy);
    Assert.Contains(proposal.Warnings, w => w.StartsWith("Menos de 4 legadas", StringComparison.Ordinal));
  }

  [Fact]
  public void Propose_SemAtualRelease_RetornaErroBloqueante()
  {
    var fileSystem = new InMemoryFileSystem().AddDirectory($@"{Root}\Legado\12.1.2606");

    var proposal = new AutoConfigurator(fileSystem).Propose(BaseConfig());

    Assert.False(proposal.CanApply);
    Assert.Null(proposal.Current);
    Assert.Contains(proposal.Errors, e => e.StartsWith("Pasta da versão atual não encontrada", StringComparison.Ordinal));
  }

  [Fact]
  public void Propose_LegadaSemProjetos_IncluiComAviso()
  {
    var fileSystem = Disk();
    fileSystem.AddDirectory($@"{Root}\Legado\12.1.2602");

    var proposal = new AutoConfigurator(fileSystem).Propose(BaseConfig());

    var legacy = Assert.Single(proposal.ActiveLegacy);
    Assert.False(legacy.HasProjects);
    Assert.Equal(["back"], proposal.Current!.ProjectsFound);
    Assert.Contains(proposal.Warnings, w => w.StartsWith("12.1.2602: sem projetos", StringComparison.Ordinal));
  }

  [Fact]
  public void Propose_ConfigExistente_PreservaColecaoFerramentasEWorkspace()
  {
    var config = BaseConfig();
    config.Collection = "https://tfs.exemplo/col";
    config.Tools.TfExe = @"C:\tf\TF.exe";
    config.Projects.RemoveAll(p => p.Alias == "sau");
    config.Versions.Add(new VersionConfig { Id = "12.1.2610", IsCurrent = true, LocalPath = @"Atual\Release", ServerPath = "$/Linha-RM/Atual/Release", Workspace = "WS-ATUAL" });
    config.Versions.Add(new VersionConfig { Id = "12.1.2606", LocalPath = @"Legado\12.1.2606", ServerPath = "$/X/Legado/12.1.2606", Aliases = ["2606"], Workspace = "WS-2606" });
    config.Versions.Add(new VersionConfig { Id = "antiga", Active = true, LocalPath = @"D:\antiga", ServerPath = "$/Linha-RM/antiga" });

    var proposal = new AutoConfigurator(Disk("12.1.2606")).Propose(config);

    var result = proposal.Config;
    Assert.Equal("https://tfs.exemplo/col", result.Collection);
    Assert.Equal(@"C:\tf\TF.exe", result.Tools.TfExe);
    Assert.Equal(["back"], result.Projects.Select(p => p.Alias));
    Assert.Equal("WS-ATUAL", result.Versions.Single(v => v.Id == "atual").Workspace);
    Assert.Equal("WS-2606", result.Versions.Single(v => v.Id == "12.1.2606").Workspace);
    Assert.DoesNotContain(result.Versions, v => v.Id == "12.1.2610");
    var kept = result.Versions.Single(v => v.Id == "antiga");
    Assert.False(kept.Active);
    Assert.Empty(ConfigValidator.Validate(result));
    // A configuração original não é alterada.
    Assert.Equal(3, config.Versions.Count);
  }
}
