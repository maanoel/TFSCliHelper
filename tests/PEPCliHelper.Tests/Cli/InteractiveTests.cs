using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.Processes;

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
  public async Task EnvConfigure_CancelarNaVersaoAtual_Exit130SemGravarCatalogo()
  {
    using var cli = new InteractiveHarness();
    var before = cli.FileSystem.ReadAllText(InteractiveHarness.ConfigPath);
    cli.SelectLast();

    var exit = await cli.RunAsync("env", "configure", "--offline");

    Assert.Equal(ExitCodes.Cancelled, exit);
    Assert.Equal(before, cli.FileSystem.ReadAllText(InteractiveHarness.ConfigPath));
    Assert.Equal(0, cli.Tfvc.MergeCount + cli.Tfvc.GetCount);
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

  [Fact]
  public async Task Login_Interativo_ExecutaTfWorkspacesNoTerminalSemNoprompt()
  {
    using var cli = new InteractiveHarness();

    var exit = await cli.RunAsync("login");

    Assert.Equal(ExitCodes.Success, exit);
    var command = Assert.Single(cli.Attached.Executed);
    Assert.Equal(InteractiveHarness.TfExePath, command.FileName);
    Assert.Equal("workspaces", command.Arguments[0]);
    Assert.Contains(command.Arguments, a => a.StartsWith("/collection:", StringComparison.Ordinal));
    Assert.DoesNotContain("/noprompt", command.Arguments);
    Assert.Contains("tf.exe autenticado", cli.Text);
  }

  [Fact]
  public async Task Login_AcessoAindaNegado_FalhaComOrientacao()
  {
    using var cli = new InteractiveHarness();
    cli.Tfvc.WorkfoldStatus = PEPCliHelper.Core.Tfvc.TfStatus.AuthError;

    var exit = await cli.RunAsync("login");

    Assert.Equal(ExitCodes.Precondition, exit);
    Assert.Contains("ainda não acessa", cli.Text);
  }

  private static InteractiveHarness BuildReady()
  {
    var cli = new InteractiveHarness();
    cli.FileSystem.AddFile(@"C:\LR\Atual\Release\Sau-PEP\RM.Pep.sln").AddFile(@"C:\LR\Atual\Release\Sau-Saude\Sau-Saude.sln");
    return cli;
  }

  [Fact]
  public async Task Build_AtualETodosProjetosPreSelecionados_CompilaPepAntesDoSaude()
  {
    using var cli = BuildReady();
    cli.MultiSelect().MultiSelect().Type("y");

    var exit = await cli.RunAsync("build");

    Assert.Equal(ExitCodes.Success, exit);
    Assert.All(cli.Executor.Executed, c => Assert.Equal(InteractiveHarness.MsBuildPath, c.FileName));
    Assert.Equal(["RM.Pep.sln", "Sau-Saude.sln"], cli.Executor.Executed.Select(c => Path.GetFileName(c.Arguments[0])));
  }

  [Fact]
  public async Task Build_DesmarcaPep_CompilaSomenteSaude()
  {
    using var cli = BuildReady();
    cli.MultiSelect().MultiSelect(0).Type("y");

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
  public async Task Menu_BuildSimularComSelecao_MostraPlanoSemMsBuild()
  {
    using var cli = BuildReady();
    cli.Select(2).Select(1).MultiSelect().MultiSelect().SelectLast();

    var exit = await cli.RunMenuAsync();

    Assert.Equal(ExitCodes.Success, exit);
    Assert.Empty(cli.Executor.Executed);
    Assert.Contains("Plano de build (dry-run)", cli.Text);
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
  public async Task Menu_EscolheLogin_ExecutaPepLoginEVoltaAoMenu()
  {
    using var cli = new InteractiveHarness();
    cli.Select(5).SelectLast();

    var exit = await cli.RunMenuAsync();

    Assert.Equal(ExitCodes.Success, exit);
    Assert.Single(cli.Attached.Executed);
    Assert.Contains("tf.exe autenticado", cli.Text);
  }

  [Fact]
  public async Task Menu_MergeVoltarDepoisSair_VoltaAoMenuSemChamadasTfvc()
  {
    using var cli = new InteractiveHarness();
    cli.Select(0).Select(2).SelectLast();

    var exit = await cli.RunMenuAsync();

    Assert.Equal(ExitCodes.Success, exit);
    Assert.Empty(cli.Tfvc.Calls);
    // O submenu foi exibido e, após Voltar, o menu principal aceitou "Sair" (prompt sem teclas disponíveis lançaria erro).
    Assert.Contains("Simular merge (dry-run)", cli.Text);
    Assert.Contains("Até logo", cli.Text);
  }
}
