using System.Text.Json;
using PEPCliHelper.CommandChain;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.Configuration;
using PEPCliHelper.Core.Execution;
using PEPCliHelper.Core.History;
using PEPCliHelper.Core.Processes;
using PEPCliHelper.Infrastructure;
using PEPCliHelper.Presentation;
using PEPCliHelper.Tests.Fakes;
using Spectre.Console;

namespace PEPCliHelper.Tests.Cli;

/// <summary>Terminal em modo não interativo, sem cores, com saída capturada (spec 002).</summary>
internal sealed class CliHarness
{
  public const string ConfigPath = @"C:\cfg\config.json";

  public InMemoryFileSystem FileSystem { get; } = new();

  public FakeTfvcClient Tfvc { get; }

  public FakeProcessInspector Processes { get; } = new();

  public StringWriter Output { get; } = new();

  public CliHarness(bool withConfig = true, params string[] targets)
  {
    Tfvc = TestData.HealthyTfvc(FileSystem, targets.Length == 0 ? ["12.1.2606", "12.1.2602"] : targets);
    if (withConfig)
      new ConfigStore(FileSystem, ConfigPath).Save(TestData.Config());
  }

  public async Task<int> RunAsync(params string[] args)
  {
    return await PepApp.RunAsync(args, options =>
    {
      var console = AnsiConsole.Create(new AnsiConsoleSettings
      {
        Ansi = AnsiSupport.No,
        ColorSystem = ColorSystemSupport.NoColors,
        Interactive = InteractionSupport.No,
        Out = new AnsiConsoleOutput(Output),
      });
      console.Profile.Width = 200;
      var effective = new GlobalOptions
      {
        NoColor = true, Ascii = options.Ascii, Json = options.Json, NonInteractive = true, Yes = options.Yes,
        Verbose = options.Verbose, Help = options.Help, ShowVersion = options.ShowVersion, ConfigPath = options.ConfigPath,
      };
      var ui = new Ui(console, effective, canPrompt: false, Output);
      var executor = new FakeCommandExecutor();
      return new AppServices(effective, ui, FileSystem, executor, Processes, TimeProvider.System,
        new ConfigStore(FileSystem, ConfigPath), new JsonExecutionJournal(FileSystem, @"C:\hist", TimeProvider.System),
        _ => Tfvc,
        _ => new ToolLocator(FileSystem, executor, new ToolsConfig(), @"C:\sem\vswhere.exe"));
    });
  }

  public string Text => Output.ToString();
}

public class CommandParsingTests
{
  private static readonly CommandHelp MergeHelp = new MergeChain().Help;

  [Fact]
  public void Match_CaminhoMaisLongoVence_EParaNoPrimeiro()
  {
    var chains = ChainCatalog.Create();

    var match = chains.Match(["open", "hostconfig", "2606"]);

    Assert.Equal("open hostconfig", match!.Chain.Help.Name);
    Assert.Equal(["2606"], match.Rest);
  }

  [Fact]
  public void Match_TokenQueSoContemNomeDeComando_NaoCasa()
  {
    Assert.Null(ChainCatalog.Create().Match(["remerge", "back"]));
  }

  [Fact]
  public void Parse_TargetRepetido_AcumulaValores()
  {
    var line = CommandLine.Parse(["--target", "2606", "-t", "2602", "--changeset=861799"], MergeHelp, new GlobalOptions());

    Assert.Equal(["2606", "2602"], line.OptionValues("target"));
    Assert.Equal(861799, line.IntOption("changeset"));
  }

  [Fact]
  public void Parse_OpcaoDesconhecida_ErroDeUso()
  {
    var error = Assert.Throws<UsageException>(() => CommandLine.Parse(["--baseless"], MergeHelp, new GlobalOptions()));

    Assert.Contains("desconhecida", error.Message);
  }

  [Fact]
  public void Parse_OpcaoSemValor_ErroDeUso()
  {
    Assert.Throws<UsageException>(() => CommandLine.Parse(["--changeset"], MergeHelp, new GlobalOptions()));
  }

  [Fact]
  public void Parse_ChangesetNaoNumerico_ErroDeUso()
  {
    var line = CommandLine.Parse(["--changeset", "abc"], MergeHelp, new GlobalOptions());

    Assert.Throws<UsageException>(() => line.IntOption("changeset"));
  }

  [Fact]
  public void GlobalOptions_Json_ImplicaNaoInterativoEConfigComEspacos()
  {
    var (options, tokens) = GlobalOptions.Parse(["merge", "--json", "--config", @"C:\Pasta Com Espaço\cfg.json"], _ => null);

    Assert.True(options.Json);
    Assert.True(options.NonInteractive);
    Assert.Equal(@"C:\Pasta Com Espaço\cfg.json", options.ConfigPath);
    Assert.Equal(["merge"], tokens);
  }

  [Fact]
  public void GlobalOptions_VariavelNoColor_DesativaCores()
  {
    Assert.True(GlobalOptions.Parse([], name => name == "NO_COLOR" ? "1" : null).Options.NoColor);
  }
}

public class CliBehaviorTests
{
  [Fact]
  public async Task Build_VersaoEProjetoBackDryRun_PlanoSomenteComRmPepSln()
  {
    var cli = new CliHarness();

    await cli.RunAsync("build", "--version", "2606", "--project", "back", "--dry-run", "--json");

    using var json = JsonDocument.Parse(cli.Text);
    var targets = json.RootElement.GetProperty("plano").GetProperty("alvos").EnumerateArray().ToList();
    var target = Assert.Single(targets);
    Assert.Equal("12.1.2606", target.GetProperty("versao").GetString());
    Assert.Equal("RM.Pep.sln", Path.GetFileName(target.GetProperty("solucao").GetString()));
  }

  [Fact]
  public async Task Build_TodasDryRun_PepAntesDoSaudeEmCadaVersao()
  {
    var cli = new CliHarness();

    await cli.RunAsync("build", "--all", "--dry-run", "--json");

    using var json = JsonDocument.Parse(cli.Text);
    var order = json.RootElement.GetProperty("plano").GetProperty("alvos").EnumerateArray()
      .Select(t => $"{t.GetProperty("versao").GetString()}:{t.GetProperty("projeto").GetString()}");
    Assert.Equal(["12.1.2610:back", "12.1.2610:sau", "12.1.2606:back", "12.1.2606:sau", "12.1.2602:back", "12.1.2602:sau"], order);
  }

  [Fact]
  public async Task Build_NaoInterativoSemVersoes_ErroDeUso()
  {
    var cli = new CliHarness();

    var exit = await cli.RunAsync("build", "--non-interactive");

    Assert.Equal(ExitCodes.Usage, exit);
    Assert.Contains("--all", cli.Text);
  }

  [Fact]
  public async Task Build_AllComVersion_ErroDeUso()
  {
    var cli = new CliHarness();

    var exit = await cli.RunAsync("build", "--all", "--version", "2606", "--dry-run");

    Assert.Equal(ExitCodes.Usage, exit);
  }

  [Fact]
  public void GlobalOptions_VersionDepoisDoComando_FicaParaOComando()
  {
    var (options, tokens) = GlobalOptions.Parse(["build", "--version", "2606"], _ => null);

    Assert.False(options.ShowVersion);
    Assert.Equal(["build", "--version", "2606"], tokens);
    Assert.True(GlobalOptions.Parse(["--version"], _ => null).Options.ShowVersion);
  }

  [Fact]
  public async Task Merge_NaoInterativoSemChangeset_ErroDeUsoSemAguardarEntrada()
  {
    var cli = new CliHarness();

    var exit = await cli.RunAsync("merge", "--project", "back", "--source", "atual", "--target", "2606", "--non-interactive");

    Assert.Equal(ExitCodes.Usage, exit);
    Assert.Contains("changeset", cli.Text);
    Assert.Equal(0, cli.Tfvc.MergeCount);
  }

  [Fact]
  public async Task Merge_SintaxeAntigaNaoInterativa_ExigeDestinosENaoExecuta()
  {
    var cli = new CliHarness();

    var exit = await cli.RunAsync("merge", "back", "atual", "861799", "--non-interactive");

    Assert.Equal(ExitCodes.Usage, exit);
    Assert.Contains("Sintaxe antiga", cli.Text);
    Assert.Empty(cli.Tfvc.Calls);
  }

  [Fact]
  public async Task Merge_ProjetoFront_Removido()
  {
    var cli = new CliHarness();

    var exit = await cli.RunAsync("merge", "--project", "front", "--source", "atual", "--target", "2606", "--changeset", "1");

    Assert.Equal(ExitCodes.Usage, exit);
    Assert.Contains("front", cli.Text);
  }

  [Fact]
  public async Task Merge_DryRunJson_JsonValidoSemAnsiENenhumMerge()
  {
    var cli = new CliHarness();

    var exit = await cli.RunAsync("merge", "--project", "back", "--source", "atual", "--all-legacy", "--changeset", "861799", "--dry-run", "--json");

    Assert.Equal(ExitCodes.Success, exit);
    Assert.DoesNotContain('\u001b', cli.Text);
    using var json = JsonDocument.Parse(cli.Text);
    Assert.True(json.RootElement.GetProperty("plano").GetProperty("dryRun").GetBoolean());
    Assert.False(json.RootElement.GetProperty("checkInRealizado").GetBoolean());
    Assert.Equal(0, cli.Tfvc.MergeCount);
  }

  [Fact]
  public async Task Merge_ArgumentosCompletosComYes_AplicaEDestacaSemCheckIn()
  {
    var cli = new CliHarness();

    var exit = await cli.RunAsync("merge", "--project", "back", "--source", "atual", "--target", "2606", "--target", "2602", "--changeset", "861799", "--yes");

    Assert.Equal(ExitCodes.Success, exit);
    Assert.Equal(2, cli.Tfvc.MergeCount);
    Assert.Contains("Nenhum check-in foi realizado", cli.Text);
    Assert.Contains("Aplicado com pending changes", cli.Text);
    Assert.DoesNotContain('\u001b', cli.Text);
  }

  [Fact]
  public async Task Merge_SemYesEmModoNaoInterativo_NaoExecuta()
  {
    var cli = new CliHarness();

    var exit = await cli.RunAsync("merge", "--project", "back", "--source", "atual", "--target", "2606", "--changeset", "861799", "--non-interactive");

    Assert.Equal(ExitCodes.Usage, exit);
    Assert.Equal(0, cli.Tfvc.MergeCount);
  }

  [Fact]
  public async Task Merge_DestinoBloqueadoComStopOnFailure_NaoExecutaNenhum()
  {
    var cli = new CliHarness();
    cli.Tfvc.Workfolds.Remove(@"C:\LR\Legado\12.1.2602");

    var exit = await cli.RunAsync("merge", "--project", "back", "--source", "atual", "--all-legacy", "--changeset", "861799", "--yes", "--stop-on-failure");

    Assert.Equal(ExitCodes.Precondition, exit);
    Assert.Equal(0, cli.Tfvc.MergeCount);
  }

  [Fact]
  public async Task Merge_DestinoBloqueadoPorPadrao_ExecutaSomenteProntosEInformaBloqueados()
  {
    var cli = new CliHarness();
    cli.Tfvc.Workfolds.Remove(@"C:\LR\Legado\12.1.2602");

    var exit = await cli.RunAsync("merge", "--project", "back", "--source", "atual", "--all-legacy", "--changeset", "861799", "--yes");

    Assert.Equal(ExitCodes.ConflictOrPartial, exit);
    Assert.Equal(1, cli.Tfvc.MergeCount);
    Assert.Contains("Merge aplicado em 1 de 2 destinos selecionados", cli.Text);
    Assert.Contains("12.1.2602 — Bloqueado", cli.Text);
  }

  [Fact]
  public async Task Merge_StopEContinueOnFailureJuntos_ErroDeUsoSemChamadasTfvc()
  {
    var cli = new CliHarness();

    var exit = await cli.RunAsync("merge", "--project", "back", "--source", "atual", "--target", "2606", "--changeset", "861799", "--yes",
      "--stop-on-failure", "--continue-on-failure");

    Assert.Equal(ExitCodes.Usage, exit);
    Assert.Empty(cli.Tfvc.Calls);
  }

  [Fact]
  public async Task Merge_ContinueOnFailureCompatibilidade_AceitoEExecuta()
  {
    var cli = new CliHarness();

    var exit = await cli.RunAsync("merge", "--project", "back", "--source", "atual", "--target", "2606", "--changeset", "861799", "--yes", "--continue-on-failure");

    Assert.Equal(ExitCodes.Success, exit);
    Assert.Equal(1, cli.Tfvc.MergeCount);
  }

  [Fact]
  public async Task Merge_ResultadoJson_ContagemDeAplicadosESelecionados()
  {
    var cli = new CliHarness();

    var exit = await cli.RunAsync("merge", "--project", "back", "--source", "atual", "--all-legacy", "--changeset", "861799", "--yes", "--json");

    Assert.Equal(ExitCodes.Success, exit);
    using var json = JsonDocument.Parse(cli.Text);
    var result = json.RootElement.GetProperty("resultado");
    Assert.Equal(2, result.GetProperty("aplicados").GetInt32());
    Assert.Equal(2, result.GetProperty("selecionados").GetInt32());
  }

  [Fact]
  public async Task Merge_VersaoAmbiguaOuDesconhecida_NaoSelecionaParecida()
  {
    var cli = new CliHarness();

    var exit = await cli.RunAsync("merge", "--project", "back", "--source", "atual", "--target", "06", "--changeset", "1");

    Assert.Equal(ExitCodes.Usage, exit);
    Assert.Contains("desconhecida", cli.Text);
  }

  [Fact]
  public async Task KillHost_VariosCandidatosSemAlvoNaoInterativo_NaoEncerra()
  {
    var cli = new CliHarness();
    cli.Processes.Processes.Add(new ProcessInfo(10, "RM.Host", @"C:\LR\Atual\Release\Bin\RM.Host.exe", null, true));
    cli.Processes.Processes.Add(new ProcessInfo(11, "RM.Host", @"C:\LR\Legado\12.1.2606\Bin\RM.Host.exe", null, true));

    var exit = await cli.RunAsync("kill", "host", "--non-interactive");

    Assert.Equal(ExitCodes.Usage, exit);
    Assert.Empty(cli.Processes.Killed);
    Assert.Empty(cli.Processes.GracefulRequests);
  }

  [Fact]
  public async Task KillHost_PidExplicitoComYes_EncerraSomenteOAlvoGraciosamente()
  {
    var cli = new CliHarness();
    cli.Processes.Processes.Add(new ProcessInfo(10, "RM.Host", @"C:\LR\Atual\Release\Bin\RM.Host.exe", null, true));
    cli.Processes.Processes.Add(new ProcessInfo(11, "RM.Host", @"C:\LR\Legado\12.1.2606\Bin\RM.Host.exe", null, true));

    var exit = await cli.RunAsync("kill", "host", "--pid", "11", "--yes");

    Assert.Equal(ExitCodes.Success, exit);
    Assert.Equal([11], cli.Processes.GracefulRequests);
    Assert.Empty(cli.Processes.Killed);
  }

  [Theory]
  [InlineData("kill", "all")]
  [InlineData("cmd", "dir")]
  [InlineData("open", "front", "atual")]
  public async Task ComandoRemovido_ErroDeUsoComAlternativa(params string[] args)
  {
    var cli = new CliHarness();

    Assert.Equal(ExitCodes.Usage, await cli.RunAsync(args));
    Assert.Contains("foi removido", cli.Text);
  }

  [Fact]
  public async Task GetVersion_SemYesNaoInterativo_NaoExecutaGet()
  {
    var cli = new CliHarness();

    var exit = await cli.RunAsync("get", "version", "2606", "--non-interactive");

    Assert.Equal(ExitCodes.Usage, exit);
    Assert.Equal(0, cli.Tfvc.GetCount);
    Assert.Contains("Confirmação necessária", cli.Text);
  }

  [Fact]
  public async Task DeleteBroker_ArquivoAusente_NenhumaAcaoExit0()
  {
    var cli = new CliHarness();

    var exit = await cli.RunAsync("delete", "broker", "2606");

    Assert.Equal(ExitCodes.Success, exit);
    Assert.Contains("Nenhuma ação necessária", cli.Text);
  }

  [Fact]
  public async Task ComandoSemConfiguracao_OrientaConfigInit()
  {
    var cli = new CliHarness(withConfig: false);

    var exit = await cli.RunAsync("env", "list");

    Assert.Equal(ExitCodes.Usage, exit);
    Assert.Contains("pep config init", cli.Text);
  }

  [Fact]
  public async Task MergeHelp_MostraUsoOpcoesEExemplos()
  {
    var cli = new CliHarness();

    var exit = await cli.RunAsync("merge", "--help");

    Assert.Equal(ExitCodes.Success, exit);
    Assert.Contains("--all-legacy", cli.Text);
    Assert.Contains("pep merge --project back --source atual --target 2606 --changeset 861799", cli.Text);
  }

  [Fact]
  public async Task SemArgumentosNaoInterativo_MostraAjudaEExit2()
  {
    var cli = new CliHarness();

    var exit = await cli.RunAsync();

    Assert.Equal(ExitCodes.Usage, exit);
    Assert.Contains("PEP CLI", cli.Text);
    Assert.Contains("Pending Changes", cli.Text);
  }

  [Fact]
  public async Task HelpAscii_SemCaracteresEspeciaisNoBanner()
  {
    var cli = new CliHarness();

    await cli.RunAsync("help", "--ascii");

    var bannerLines = cli.Text.Split('\n').Take(8);
    Assert.All(bannerLines, line => Assert.True(line.All(c => c < 128 || char.IsLetter(c)), line));
  }

  [Fact]
  public async Task ComandoDesconhecido_SugereParecidos()
  {
    var cli = new CliHarness();

    var exit = await cli.RunAsync("env", "listar");

    Assert.Equal(ExitCodes.Usage, exit);
    Assert.Contains("pep env list", cli.Text);
  }

  [Fact]
  public async Task EnvConfigure_RotacaoNaoInterativa_GravaCatalogoSemExcluirPastas()
  {
    var cli = new CliHarness();

    var exit = await cli.RunAsync("env", "configure", "--atual", "12.1.2614", "--legado", "12.1.2606", "--yes");

    Assert.Equal(ExitCodes.Success, exit);
    var saved = new ConfigStore(cli.FileSystem, CliHarness.ConfigPath).Load();
    Assert.Equal(ConfigLoadStatus.Loaded, saved.Status);
    Assert.Contains(saved.Config!.Versions, v => v.Id == "12.1.2614" && v.IsCurrent && v.Active);
    Assert.Contains(saved.Config.Versions, v => v.Id == "12.1.2602" && !v.Active);
    Assert.Empty(cli.FileSystem.Deleted);
    Assert.Equal(0, cli.Tfvc.MergeCount + cli.Tfvc.GetCount);
  }

  [Fact]
  public async Task EnvConfigure_SemMapeamento_UsaConvencaoDeCaminhoTfvcSemPerguntar()
  {
    var cli = new CliHarness();
    cli.Tfvc.Workfolds.Clear();
    var empty = PepConfig.CreateDefault();
    empty.LocalRoot = TestData.Root;
    new ConfigStore(cli.FileSystem, CliHarness.ConfigPath).Save(empty);

    var exit = await cli.RunAsync("env", "configure", "--atual", "12.1.2614", "--legado", "12.1.2606", "--yes");

    Assert.True(exit == ExitCodes.Success, cli.Text);
    var saved = new ConfigStore(cli.FileSystem, CliHarness.ConfigPath).Load().Config!;
    Assert.Equal("$/Linha-RM/Atual/Release", saved.Versions.Single(v => v.Id == "12.1.2614").ServerPath);
    Assert.Equal("$/Linha-RM/Legado/12.1.2606", saved.Versions.Single(v => v.Id == "12.1.2606").ServerPath);
  }

  [Fact]
  public async Task Login_NaoInterativo_ErroDeUsoSemExecutarTf()
  {
    var cli = new CliHarness();

    var exit = await cli.RunAsync("login", "--non-interactive");

    Assert.Equal(ExitCodes.Usage, exit);
    Assert.Contains("terminal interativo", cli.Text);
  }

  [Fact]
  public async Task Historico_MergeRegistradoAparecenaLista()
  {
    var cli = new CliHarness();
    await cli.RunAsync("merge", "--project", "back", "--source", "atual", "--target", "2606", "--changeset", "861799", "--yes");

    var exit = await cli.RunAsync("history", "list", "--json");

    Assert.Equal(ExitCodes.Success, exit);
    Assert.Contains("\"comando\": \"merge\"", cli.Text);
  }
}
