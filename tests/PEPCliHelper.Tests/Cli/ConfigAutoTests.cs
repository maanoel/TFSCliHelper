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
    Assert.Contains("config auto --force", cli.Text);
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
  public async Task PrimeiraExecucao_Menu_ConfiguraAutomaticamenteSemPerguntarEAvisaQueEstaPronto()
  {
    using var cli = new InteractiveHarness(withConfig: false);
    AddDefaultDisk(cli.FileSystem, "12.1.2606", "12.1.2602");
    // Única tecla: "Sair" no menu. Qualquer pergunta na configuração inicial consumiria a tecla e o teste falharia.
    cli.SelectLast();

    var exit = await cli.RunAsync();

    Assert.Equal(ExitCodes.Success, exit);
    var config = Saved(cli.FileSystem, InteractiveHarness.ConfigPath);
    Assert.Equal(["atual", "12.1.2606", "12.1.2602"], config.Versions.Select(v => v.Id));
    Assert.Contains("Primeira configuração", cli.Text);
    Assert.Contains(FirstRunSetup.ReadyMessage, cli.Text);
    Assert.Contains("Até logo", cli.Text);
  }

  [Fact]
  public async Task PrimeiraExecucao_ComandoDireto_ConfiguraAutomaticamenteEExecuta()
  {
    var cli = new CliHarness(withConfig: false);
    AddDefaultDisk(cli.FileSystem, "12.1.2606");

    var exit = await cli.RunAsync("env", "list");

    Assert.Equal(ExitCodes.Success, exit);
    Assert.True(cli.FileSystem.FileExists(CliHarness.ConfigPath));
    Assert.Contains(FirstRunSetup.ReadyMessage, cli.Text);
    Assert.Contains("12.1.2606", cli.Text);
  }

  [Fact]
  public async Task PrimeiraExecucao_SemAtualRelease_AvisaSemGravarEAbreMenu()
  {
    using var cli = new InteractiveHarness(withConfig: false);
    cli.FileSystem.AddDirectory($@"{DefaultRoot}\Legado\12.1.2606");
    cli.SelectLast();

    var exit = await cli.RunAsync();

    Assert.Equal(ExitCodes.Success, exit);
    Assert.False(cli.FileSystem.FileExists(InteractiveHarness.ConfigPath));
    Assert.Contains("Pasta da versão atual não encontrada", cli.Text);
    Assert.Contains("pep config auto", cli.Text);
    Assert.DoesNotContain("manual", cli.Text);
  }

  [Theory]
  [InlineData("env", "configure")]
  [InlineData("config", "init")]
  public async Task ConfiguracaoManual_ComandoRemovido_Exit2OrientaConfigAuto(string group, string command)
  {
    var cli = new CliHarness();
    var before = cli.FileSystem.ReadAllText(CliHarness.ConfigPath);

    var exit = await cli.RunAsync(group, command, "--non-interactive");

    Assert.Equal(ExitCodes.Usage, exit);
    Assert.Contains("pep config auto", cli.Text);
    Assert.Equal(before, cli.FileSystem.ReadAllText(CliHarness.ConfigPath));
  }

  [Fact]
  public async Task ConfigAuto_ForceComConfigInvalida_RecriaComBackup()
  {
    var cli = new CliHarness(withConfig: false);
    AddDefaultDisk(cli.FileSystem, "12.1.2606");
    cli.FileSystem.AddFile(CliHarness.ConfigPath, "{ inválido", readOnly: false);

    var exit = await cli.RunAsync("config", "auto", "--force", "--yes");

    Assert.Equal(ExitCodes.Success, exit);
    Assert.Equal("atual", Saved(cli.FileSystem, CliHarness.ConfigPath).Versions.Single(v => v.IsCurrent).Id);
    Assert.Contains("Backup", cli.Text);
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
  public async Task Menu_Ambientes_SemOpcoesDeConfiguracao()
  {
    using var cli = new InteractiveHarness();
    var before = cli.FileSystem.ReadAllText(InteractiveHarness.ConfigPath);
    // Ambientes → Voltar (última opção) → Sair.
    cli.Select(3).SelectLast().SelectLast();

    var exit = await cli.RunMenuAsync();

    Assert.Equal(ExitCodes.Success, exit);
    Assert.Contains("Listar catálogo", cli.Text);
    Assert.DoesNotContain("Configuração automática", cli.Text);
    Assert.DoesNotContain("Mostrar configuração", cli.Text);
    Assert.DoesNotContain("Configurar atual", cli.Text);
    Assert.DoesNotContain("Criar configuração", cli.Text);
    Assert.Equal(before, cli.FileSystem.ReadAllText(InteractiveHarness.ConfigPath));
  }
}
