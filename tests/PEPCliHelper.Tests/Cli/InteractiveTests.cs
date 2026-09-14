using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.Processes;
using PEPCliHelper.Core.Tfvc;
using PEPCliHelper.Infrastructure;
using PEPCliHelper.Menus;
using PEPCliHelper.Tests.Fakes;

namespace PEPCliHelper.Tests.Cli;

/// <summary>Fluxos guiados por prompts (spec 002): seleção, multisseleção, confirmação, cancelamento e menu.</summary>
public class InteractiveTests
{
  // Ordem dos prompts do merge: projeto (back, sau), origem (atual, 2606, 2602), destinos (sem a origem), changeset, confirmação.
  private const int ProjectBack = 0;
  private const int SourceCurrent = 0;

  [Fact]
  public async Task Merge_GuiadoPorPromptsComConfirmacao_AplicaDoisMerges()
  {
    using var cli = new InteractiveHarness();
    cli.Select(ProjectBack).Select(SourceCurrent).MultiSelect(0, 1).Type("861799").Type("y");

    var exit = await cli.RunAsync("merge");

    Assert.Equal(ExitCodes.Success, exit);
    Assert.Equal(2, cli.Tfvc.MergeCount);
    Assert.Contains("Nenhum check-in foi realizado", cli.Text);
  }

  [Fact]
  public async Task Merge_ConfirmacaoNegada_Exit130SemMerge()
  {
    using var cli = new InteractiveHarness();
    cli.Select(ProjectBack).Select(SourceCurrent).MultiSelect(0, 1).Type("861799").Type("n");

    var exit = await cli.RunAsync("merge");

    Assert.Equal(ExitCodes.Cancelled, exit);
    Assert.Equal(0, cli.Tfvc.MergeCount);
    Assert.Contains("Merge cancelado", cli.Text);
  }

  [Fact]
  public async Task Merge_MultisselecaoVazia_CanceladoSemMerge()
  {
    using var cli = new InteractiveHarness();
    cli.Select(ProjectBack).Select(SourceCurrent).MultiSelect();

    var exit = await cli.RunAsync("merge");

    Assert.Equal(ExitCodes.Cancelled, exit);
    Assert.Equal(0, cli.Tfvc.MergeCount);
    Assert.Contains("Operação cancelada", cli.Text);
  }

  [Fact]
  public async Task Merge_CancelarNoProjeto_Exit130SemChamadasTfvc()
  {
    using var cli = new InteractiveHarness();
    cli.Select(2);

    var exit = await cli.RunAsync("merge");

    Assert.Equal(ExitCodes.Cancelled, exit);
    Assert.Empty(cli.Tfvc.Calls);
    Assert.Contains("Operação cancelada", cli.Text);
  }

  [Fact]
  public async Task Merge_CancelarNaOrigem_Exit130SemChamadasTfvc()
  {
    using var cli = new InteractiveHarness();
    cli.Select(ProjectBack).Select(3);

    var exit = await cli.RunAsync("merge");

    Assert.Equal(ExitCodes.Cancelled, exit);
    Assert.Empty(cli.Tfvc.Calls);
    Assert.Contains("Operação cancelada", cli.Text);
  }

  [Fact]
  public async Task KillHost_MultisselecaoDeUmPid_EncerraSomenteEsseGraciosamente()
  {
    using var cli = new InteractiveHarness();
    cli.Processes.Processes.Add(new ProcessInfo(10, "RM.Host", @"C:\LR\Atual\Release\Bin\RM.Host.exe", null, true));
    cli.Processes.Processes.Add(new ProcessInfo(11, "RM.Host", @"C:\LR\Legado\12.1.2606\Bin\RM.Host.exe", null, true));
    // Só a seleção: se o CLI pedisse confirmação, o TestConsole falharia por falta de tecla.
    cli.MultiSelect(1);

    var exit = await cli.RunAsync("kill", "host");

    Assert.Equal(ExitCodes.Success, exit);
    Assert.Equal([11], cli.Processes.GracefulRequests);
    Assert.Empty(cli.Processes.Killed);
  }

  private static readonly string[] DryRunMerge =
    ["merge", "--project", "back", "--source", "atual", "--target", "2606", "--target", "2602", "--changeset", "861799", "--dry-run"];

  [Fact]
  public async Task MergeDryRun_SemCredencial_AbreLoginUmaVezEContinua()
  {
    using var cli = new InteractiveHarness();
    cli.Tfvc.RequiresLogin = true;

    var exit = await cli.RunAsync(DryRunMerge);

    Assert.True(exit == ExitCodes.Success, cli.Text);
    var command = Assert.Single(cli.Attached.Executed);
    Assert.Equal(InteractiveHarness.TfExePath, command.FileName);
    Assert.Equal("workspaces", command.Arguments[0]);
    Assert.Contains(command.Arguments, a => a.StartsWith("/collection:", StringComparison.Ordinal));
    Assert.DoesNotContain("/noprompt", command.Arguments);
    Assert.Contains("Conectado ao TFS", cli.Text);
    Assert.Equal(0, cli.Tfvc.MergeCount);
  }

  [Fact]
  public async Task Login_ConsultasParalelasSemCredencial_UmUnicoLogin()
  {
    using var cli = new InteractiveHarness();
    cli.Tfvc.RequiresLogin = true;
    cli.Attached.Delay = TimeSpan.FromMilliseconds(30);
    var tfvc = cli.CreateServices(new GlobalOptions()).Tfvc;

    var results = await Task.WhenAll(Enumerable.Range(0, 4)
      .Select(i => Task.Run(() => tfvc.GetWorkfoldAsync($@"C:\LR\Legado\{i}", CancellationToken.None))));

    Assert.Single(cli.Attached.Executed);
    Assert.All(results, r => Assert.NotEqual(TfStatus.AuthError, r.Result.Status));
  }

  [Fact]
  public async Task MergeDryRun_LoginNaoResolve_FalhaComOrientacaoSemRepetirLogin()
  {
    using var cli = new InteractiveHarness();
    cli.Tfvc.RequiresLogin = true;
    cli.Attached.OnRun = null;

    var exit = await cli.RunAsync(DryRunMerge);

    Assert.NotEqual(ExitCodes.Success, exit);
    Assert.Single(cli.Attached.Executed);
    Assert.Contains("TF30063", cli.Text);
    Assert.Contains("login do TFS", cli.Text);
    Assert.DoesNotContain("pep login", cli.Text);
  }

  [Fact]
  public async Task Login_FalhouNoProcesso_NaoAbreSegundaVez()
  {
    using var cli = new InteractiveHarness();
    cli.Tfvc.RequiresLogin = true;
    cli.Attached.OnRun = null;
    var tfvc = cli.CreateServices(new GlobalOptions()).Tfvc;

    var first = await tfvc.GetWorkfoldAsync(@"C:\LR\Atual\Release", CancellationToken.None);
    var second = await tfvc.GetChangesetAsync(861799, "https://tfs", CancellationToken.None);

    Assert.Single(cli.Attached.Executed);
    Assert.Equal(TfStatus.AuthError, first.Result.Status);
    Assert.Equal(TfStatus.AuthError, second.Result.Status);
  }

  [Fact]
  public async Task Merge_AuthErrorComItensProcessados_NaoRepeteMerge()
  {
    using var cli = new InteractiveHarness();
    const string folder = @"C:\LR\Legado\12.1.2606";
    cli.Tfvc.MergeStatus[folder] = TfStatus.AuthError;
    cli.Tfvc.MergeOutput[folder] = "merge, edit: $/Linha-RM/Atual/Release/a.cs\n" + FakeTfvcClient.AuthErrorOutput;
    var tfvc = cli.CreateServices(new GlobalOptions()).Tfvc;

    var result = await tfvc.MergeChangesetAsync("$/Linha-RM/Atual/Release", "$/Linha-RM/Legado/12.1.2606", 861799, folder, null, CancellationToken.None);

    Assert.Equal(TfStatus.AuthError, result.Status);
    Assert.Equal(1, cli.Tfvc.MergeCount);
    Assert.Empty(cli.Attached.Executed);
  }

  [Fact]
  public async Task Merge_AuthErrorSemItens_AbreLoginERepeteUmaVez()
  {
    using var cli = new InteractiveHarness();
    cli.Tfvc.RequiresLogin = true;
    var tfvc = cli.CreateServices(new GlobalOptions()).Tfvc;

    var result = await tfvc.MergeChangesetAsync("$/Linha-RM/Atual/Release", "$/Linha-RM/Legado/12.1.2606", 861799, @"C:\LR\Legado\12.1.2606", null, CancellationToken.None);

    Assert.Equal(TfStatus.Success, result.Status);
    Assert.Equal(2, cli.Tfvc.MergeCount);
    Assert.Single(cli.Attached.Executed);
  }

  [Fact]
  public async Task GetLatest_AuthErrorComCaminhoLocalProcessado_NaoRepeteGet()
  {
    using var cli = new InteractiveHarness();
    const string folder = @"C:\LR\Legado\12.1.2606";
    cli.Tfvc.GetStatus[folder] = TfStatus.AuthError;
    var tfvc = new AutoLoginTfvcClient(new GetOutputTfvc(cli.Tfvc, $"{folder}\\Bin:\nReplacing a.dll\n{FakeTfvcClient.AuthErrorOutput}"),
      cli.CreateServices(new GlobalOptions()).Authenticator);

    var result = await tfvc.GetLatestAsync(folder, null, CancellationToken.None);

    Assert.Equal(TfStatus.AuthError, result.Status);
    Assert.Equal(1, cli.Tfvc.GetCount);
    Assert.Empty(cli.Attached.Executed);
  }

  [Theory]
  [InlineData("merge, edit: $/Linha-RM/a.cs -> $/Linha-RM/b.cs", true)]
  [InlineData(@"C:\LR\Legado\12.1.2606\Bin:", true)]
  [InlineData(@"\\servidor\pasta\a.cs", true)]
  [InlineData(@"TF30063: você não está autorizado a acessar totvstfs.visualstudio.com\totvstfs.", false)]
  [InlineData("", false)]
  public void HasItemLines_SaidaDoTf_DetectaItensProcessados(string output, bool expected) =>
    Assert.Equal(expected, AutoLoginTfvcClient.HasItemLines(output));

  /// <summary>tf get com saída configurada (o fake não gera linhas de item).</summary>
  private sealed class GetOutputTfvc(FakeTfvcClient inner, string output) : DecoratedTfvc(inner)
  {
    public override async Task<TfResult> GetLatestAsync(string localPath, Action<string>? onOutputLine, CancellationToken cancellationToken) =>
      (await Inner.GetLatestAsync(localPath, onOutputLine, cancellationToken)) with { Output = output };
  }

  private static InteractiveHarness BuildReady()
  {
    var cli = new InteractiveHarness();
    cli.FileSystem.AddFile(@"C:\LR\Atual\Release\Sau-PEP\RM.Pep.sln").AddFile(@"C:\LR\Atual\Release\Sau-Saude\Sau-Saude.sln");
    return cli;
  }

  [Fact]
  public async Task Build_SelecaoPadrao_CompilaSomentePepComSaudeDesmarcado()
  {
    using var cli = BuildReady();
    cli.MultiSelect().MultiSelect();

    var exit = await cli.RunAsync("build");

    Assert.Equal(ExitCodes.Success, exit);
    Assert.All(cli.Executor.Executed, c => Assert.Equal(InteractiveHarness.MsBuildPath, c.FileName));
    Assert.Equal(["RM.Pep.sln"], cli.Executor.Executed.Select(c => Path.GetFileName(c.Arguments[0])));
  }

  [Fact]
  public async Task Build_HostDaVersaoAberto_EncerraAntesDeCompilarSemPerguntar()
  {
    using var cli = BuildReady();
    cli.Processes.Processes.Add(new ProcessInfo(10, "RM.Host", @"C:\LR\Atual\Release\Bin\RM.Host.exe", null, true));
    cli.MultiSelect().MultiSelect();

    var exit = await cli.RunAsync("build");

    Assert.True(exit == ExitCodes.Success, cli.Text);
    Assert.Equal([10], cli.Processes.GracefulRequests);
    Assert.Empty(cli.Processes.Killed);
    Assert.Single(cli.Executor.Executed);
  }

  [Fact]
  public async Task Build_MarcaSaude_CompilaPepAntesDoSaude()
  {
    using var cli = BuildReady();
    cli.MultiSelect().MultiSelect(1);

    var exit = await cli.RunAsync("build");

    Assert.Equal(ExitCodes.Success, exit);
    Assert.Equal(["RM.Pep.sln", "Sau-Saude.sln"], cli.Executor.Executed.Select(c => Path.GetFileName(c.Arguments[0])));
  }

  [Fact]
  public async Task Build_TrocaPepPorSaude_CompilaSomenteSaude()
  {
    using var cli = BuildReady();
    cli.MultiSelect().MultiSelect(0, 1);

    var exit = await cli.RunAsync("build");

    Assert.Equal(ExitCodes.Success, exit);
    Assert.Equal(["Sau-Saude.sln"], cli.Executor.Executed.Select(c => Path.GetFileName(c.Arguments[0])));
  }

  [Fact]
  public async Task Build_DesmarcaVersaoAtual_CanceladoSemMsBuild()
  {
    using var cli = BuildReady();
    cli.MultiSelect(0);

    var exit = await cli.RunAsync("build");

    Assert.Equal(ExitCodes.Cancelled, exit);
    Assert.Empty(cli.Executor.Executed);
  }

  [Fact]
  public async Task Menu_Build_VaiDiretoParaSelecaoECompilaSemOpcaoDeSimular()
  {
    using var cli = BuildReady();
    cli.Select(2).MultiSelect().MultiSelect().SelectLast();

    var exit = await cli.RunMenuAsync();

    Assert.Equal(ExitCodes.Success, exit);
    Assert.Single(cli.Executor.Executed);
    Assert.DoesNotContain("dry-run", cli.Text);
  }

  // Menu: merge, get, build, env, doctor, pending, tools, history, prompt (8), help, sair.
  private const int MenuPrompt = 8;

  [Fact]
  public async Task Menu_PromptDeComandos_ExecutaComandoComESemPrefixoPepEVoltaAoMenu()
  {
    using var cli = new InteractiveHarness();
    cli.Select(MenuPrompt).Type("pep env list").Type("history list").Type("sair").SelectLast();

    var exit = await cli.RunMenuAsync();

    Assert.Equal(ExitCodes.Success, exit);
    Assert.Contains(InteractiveMenu.PromptLabel, cli.Text);
    Assert.Contains("12.1.2606", cli.Text);
    Assert.Contains("Até logo", cli.Text);
  }

  [Fact]
  public async Task Menu_PromptDeComandos_MergeComYesAplicaSemCheckIn()
  {
    using var cli = new InteractiveHarness();
    cli.Select(MenuPrompt)
      .Type("merge --project back --source atual --target 2606 --changeset 861799 --yes")
      .Type("sair")
      .SelectLast();

    var exit = await cli.RunMenuAsync();

    Assert.Equal(ExitCodes.Success, exit);
    Assert.Equal(1, cli.Tfvc.MergeCount);
    Assert.Contains("Nenhum check-in foi realizado", cli.Text);
  }

  [Fact]
  public async Task Menu_PromptDeComandos_ComandoDoWindowsNaoEExecutado()
  {
    using var cli = new InteractiveHarness();
    cli.Select(MenuPrompt).Type("del /q C:\\temp").Type("sair").SelectLast();

    var exit = await cli.RunMenuAsync();

    Assert.Equal(ExitCodes.Usage, exit);
    Assert.Contains("Comando desconhecido", cli.Text);
    Assert.Empty(cli.Executor.Executed);
    Assert.Empty(cli.Processes.Started);
  }

  [Theory]
  [InlineData("merge --changeset 1", new[] { "merge", "--changeset", "1" })]
  [InlineData("  open host   \"12.1 2606\" ", new[] { "open", "host", "12.1 2606" })]
  [InlineData("", new string[0])]
  public void CommandLineSplitter_RespeitaAspasEEspacos(string line, string[] expected)
  {
    Assert.Equal(expected, CommandLineSplitter.Split(line));
  }

  [Fact]
  public void CommandLineSplitter_AspasAbertas_ErroDeUso()
  {
    Assert.Throws<UsageException>(() => CommandLineSplitter.Split("open host \"2606"));
  }

  [Fact]
  public async Task Menu_EscolheSair_RetornaSucesso()
  {
    using var cli = new InteractiveHarness();
    cli.SelectLast();

    var exit = await cli.RunMenuAsync();

    Assert.Equal(ExitCodes.Success, exit);
    Assert.Contains("Até logo", cli.Text);
  }

  [Fact]
  public async Task Menu_SemOpcaoDeLogin_SextaOpcaoEPendingChanges()
  {
    using var cli = new InteractiveHarness();
    // Menu: merge, get, build, env, doctor, pending (5), ... Pending → "Todas as versões ativas"; depois Sair.
    cli.Select(5).Select(0).SelectLast();

    var exit = await cli.RunMenuAsync();

    Assert.Contains(cli.Tfvc.Calls, c => c.StartsWith("status:", StringComparison.Ordinal));
    Assert.DoesNotContain("Login do tf.exe", cli.Text);
    Assert.Empty(cli.Attached.Executed);
    Assert.Contains("Até logo", cli.Text);
    Assert.Equal(ExitCodes.Success, exit);
  }

  [Fact]
  public async Task Menu_MergeCancelarNoProjetoDepoisSair_VoltaAoMenuSemChamadasTfvc()
  {
    using var cli = new InteractiveHarness();
    // Merge abre direto o prompt de projeto (back, sau, cancelar); após cancelar, o menu aceita "Sair".
    cli.Select(0).Select(2).SelectLast();

    var exit = await cli.RunMenuAsync();

    // O menu devolve o código do último comando: o merge cancelado.
    Assert.Equal(ExitCodes.Cancelled, exit);
    Assert.Empty(cli.Tfvc.Calls);
    Assert.DoesNotContain("Simular", cli.Text);
    Assert.Contains("Até logo", cli.Text);
  }

  [Fact]
  public async Task Menu_Get_SemOpcaoDeSimular()
  {
    using var cli = new InteractiveHarness();
    cli.Select(1).Select(2).SelectLast();

    var exit = await cli.RunMenuAsync();

    Assert.Equal(ExitCodes.Success, exit);
    Assert.Empty(cli.Tfvc.Calls);
    Assert.DoesNotContain("Simular", cli.Text);
  }
}
