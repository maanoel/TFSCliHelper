using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.Configuration;
using PEPCliHelper.Menus;
using PEPCliHelper.Tests.Fakes;

namespace PEPCliHelper.Tests.Cli;

/// <summary>pep config auto e primeira execução (decisão de 2026-09-14). Disco em memória: nunca C:\Linha-RM real.</summary>
public class ConfigAutoTests
{
  private const string DefaultRoot = @"C:\Linha-RM";

  private static void AddDefaultDisk(InMemoryFileSystem fileSystem, params string[] legacy)
  {
    fileSystem.AddDirectory($@"{DefaultRoot}\Atual\Release\Sau-PEP");
    foreach (var name in legacy)
      fileSystem.AddDirectory($@"{DefaultRoot}\Legado\{name}\Sau-PEP");
  }

  private static PepConfig Saved(InMemoryFileSystem fileSystem, string path) =>
    ConfigStore.Deserialize(fileSystem.ReadAllText(path))!;

  [Fact]
  public async Task ConfigAuto_ComYesSemConfig_GravaAtualELegadas()
  {
    var cli = new CliHarness(withConfig: false);
    AddDefaultDisk(cli.FileSystem, "12.1.2602", "12.1.2606", "12.1.2510", "12.1.2506", "12.1.2502");

    var exit = await cli.RunAsync("config", "auto", "--yes");

    Assert.Equal(ExitCodes.Success, exit);
    var config = Saved(cli.FileSystem, CliHarness.ConfigPath);
    Assert.Equal(PepConfig.DefaultCollection, config.Collection);
    Assert.Equal("atual", config.Versions.Single(v => v.IsCurrent).Id);
    Assert.Equal(["12.1.2606", "12.1.2602", "12.1.2510", "12.1.2506"], config.Versions.Where(v => v.Active && !v.IsCurrent).Select(v => v.Id));
    Assert.False(config.Versions.Single(v => v.Id == "12.1.2502").Active);
    Assert.Contains("pep env validate", cli.Text);
  }

  [Fact]
  public async Task ConfigAuto_Json_InformaVersoesGravadas()
  {
    var cli = new CliHarness(withConfig: false);
    AddDefaultDisk(cli.FileSystem, "12.1.2606");

    var exit = await cli.RunAsync("config", "auto", "--yes", "--json");

    Assert.Equal(ExitCodes.Success, exit);
    Assert.Contains("\"gravado\": true", cli.Text);
    Assert.Contains("\"legadasAtivas\"", cli.Text);
  }

  [Fact]
  public async Task ConfigAuto_NaoInterativoSemYes_Exit2SemGravar()
  {
    var cli = new CliHarness(withConfig: false);
    AddDefaultDisk(cli.FileSystem, "12.1.2606");

    var exit = await cli.RunAsync("config", "auto");

    Assert.Equal(ExitCodes.Usage, exit);
    Assert.False(cli.FileSystem.FileExists(CliHarness.ConfigPath));
  }

  [Fact]
  public async Task ConfigAuto_ConfigInvalida_Exit2SemSobrescrever()
  {
    var cli = new CliHarness(withConfig: false);
    AddDefaultDisk(cli.FileSystem, "12.1.2606");
    cli.FileSystem.AddFile(CliHarness.ConfigPath, "{ inválido", readOnly: false);

    var exit = await cli.RunAsync("config", "auto", "--yes");

    Assert.Equal(ExitCodes.Usage, exit);
    Assert.Equal("{ inválido", cli.FileSystem.ReadAllText(CliHarness.ConfigPath));
    Assert.Contains("config init --force", cli.Text);
  }

  [Fact]
  public async Task ConfigAuto_SemAtualRelease_Exit3SemGravar()
  {
    var cli = new CliHarness(withConfig: false);
    cli.FileSystem.AddDirectory($@"{DefaultRoot}\Legado\12.1.2606");

    var exit = await cli.RunAsync("config", "auto", "--yes");

    Assert.Equal(ExitCodes.Precondition, exit);
    Assert.False(cli.FileSystem.FileExists(CliHarness.ConfigPath));
  }

  [Fact]
  public async Task PrimeiraExecucao_EnterNaConfiguracaoAutomatica_GravaEAbreMenu()
  {
    using var cli = new InteractiveHarness(withConfig: false);
    AddDefaultDisk(cli.FileSystem, "12.1.2606", "12.1.2602");
    cli.Select(0).SelectLast();

    var exit = await cli.RunAsync();

    Assert.Equal(ExitCodes.Success, exit);
    var config = Saved(cli.FileSystem, InteractiveHarness.ConfigPath);
    Assert.Equal(["atual", "12.1.2606", "12.1.2602"], config.Versions.Select(v => v.Id));
    Assert.Contains("Primeira configuração", cli.Text);
    Assert.Contains("Configuração gravada", cli.Text);
    Assert.Contains("Até logo", cli.Text);
  }

  [Fact]
  public async Task PrimeiraExecucao_AgoraNao_NaoGravaEAbreMenu()
  {
    using var cli = new InteractiveHarness(withConfig: false);
    AddDefaultDisk(cli.FileSystem, "12.1.2606");
    cli.Select(2).SelectLast();

    var exit = await cli.RunAsync();

    Assert.Equal(ExitCodes.Success, exit);
    Assert.False(cli.FileSystem.FileExists(InteractiveHarness.ConfigPath));
    Assert.Contains("Até logo", cli.Text);
  }

  [Fact]
  public async Task PrimeiraExecucao_SemAtualRelease_OfereceSomenteManualOuAgoraNao()
  {
    using var cli = new InteractiveHarness(withConfig: false);
    cli.FileSystem.AddDirectory($@"{DefaultRoot}\Legado\12.1.2606");
    cli.SelectLast().SelectLast();

    var exit = await cli.RunAsync();

    Assert.Equal(ExitCodes.Success, exit);
    Assert.False(cli.FileSystem.FileExists(InteractiveHarness.ConfigPath));
    Assert.Contains("Pasta da versão atual não encontrada", cli.Text);
    Assert.Contains(FirstRunSetup.ManualOption, cli.Text);
    Assert.DoesNotContain(FirstRunSetup.AutoOption, cli.Text);
  }

  [Fact]
  public async Task PrimeiraExecucao_ConfigComAtual_NaoOfereceConfiguracao()
  {
    using var cli = new InteractiveHarness();
    cli.SelectLast();

    var exit = await cli.RunAsync();

    Assert.Equal(ExitCodes.Success, exit);
    Assert.DoesNotContain("Primeira configuração", cli.Text);
  }

  [Fact]
  public async Task Menu_AmbientesConfiguracaoAutomatica_ConfirmaComEnterEGrava()
  {
    using var cli = new InteractiveHarness();
    cli.Select(3).Select(0).Type(string.Empty).SelectLast();

    var exit = await cli.RunMenuAsync();

    Assert.Equal(ExitCodes.Success, exit);
    Assert.Contains("pep config auto", cli.Text);
    var config = Saved(cli.FileSystem, InteractiveHarness.ConfigPath);
    Assert.Equal("atual", config.Versions.Single(v => v.IsCurrent).Id);
    Assert.Equal(["12.1.2606", "12.1.2602"], config.Versions.Where(v => v.Active && !v.IsCurrent).Select(v => v.Id));
  }
}
