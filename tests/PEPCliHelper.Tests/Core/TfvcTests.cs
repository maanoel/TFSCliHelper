using System.Reflection;
using PEPCliHelper.Core.Execution;
using PEPCliHelper.Core.Tfvc;
using PEPCliHelper.Tests.Fakes;

namespace PEPCliHelper.Tests.Core;

public class WorkfoldParserTests
{
  private const string EnglishOutput = """
    ===============================================================================
    Workspace : DEV-PC (DOMAIN\dev)
    Collection: https://totvstfs.visualstudio.com/defaultcollection
     $/Linha-RM/atual/release: C:\Linha-RM\Atual\Release
     (cloaked) $/Linha-RM/atual/release/Bin:
    """;

  private const string PortugueseOutput = """
    ===============================================================================
    Espaço de trabalho: DEV-PC (DOMINIO\dev)
    Coleção: https://totvstfs.visualstudio.com/defaultcollection
     $/Linha-RM/Legado/12.1.2606/Sau-PEP: C:\Linha-RM\Legado\12.1.2606\Sau-PEP
     (oculto) $/Linha-RM/Legado/12.1.2606/Sau-PEP/Objetos Gerenciais:
    """;

  [Fact]
  public void Parse_SaidaEmIngles_ExtraiWorkspaceMapeamentoECloak()
  {
    var info = WorkfoldParser.Parse(EnglishOutput);

    Assert.Equal("DEV-PC", info.WorkspaceName);
    Assert.Equal(@"DOMAIN\dev", info.Owner);
    Assert.StartsWith("https://totvstfs", info.Collection);
    Assert.Contains(info.Mappings, m => m.ServerPath == "$/Linha-RM/atual/release" && m.LocalPath == @"C:\Linha-RM\Atual\Release" && !m.IsCloaked);
    Assert.Contains(info.Mappings, m => m.ServerPath == "$/Linha-RM/atual/release/Bin" && m.IsCloaked);
  }

  [Fact]
  public void Parse_SaidaEmPortugues_IndependeDosRotulos()
  {
    var info = WorkfoldParser.Parse(PortugueseOutput);

    Assert.Equal("DEV-PC", info.WorkspaceName);
    Assert.Contains(info.Mappings, m => m.ServerPath.EndsWith("Objetos Gerenciais") && m.IsCloaked);
  }
}

public class MappingResolverTests
{
  private static readonly TfResult Ok = new(TfStatus.Success, 0, "", TimeSpan.Zero, "tf");

  private static WorkfoldInfo Info(params WorkspaceMapping[] mappings) => new("WS", "dev", "https://col", mappings);

  [Fact]
  public void Evaluate_MapeamentoEmAncestral_ValidoComCaminhoEfetivo()
  {
    var check = MappingResolver.Evaluate(@"C:\LR\Atual\Release\Sau-PEP", "$/Linha-RM/atual/release/Sau-PEP", true, Ok,
      Info(new WorkspaceMapping("$/Linha-RM/atual/release", @"C:\LR\Atual\Release", false)));

    Assert.Equal(MappingState.Mapped, check.State);
    Assert.Equal("$/Linha-RM/atual/release/Sau-PEP", check.EffectiveServerPath);
  }

  [Fact]
  public void Evaluate_PastaInexistente_FolderMissingSemConsultarTf()
  {
    var check = MappingResolver.Evaluate(@"C:\LR\x", "$/x", folderExists: false, null, null);

    Assert.Equal(MappingState.FolderMissing, check.State);
  }

  [Fact]
  public void Evaluate_PastaExistenteSemMapeamento_NotMappedComOrientacao()
  {
    var check = MappingResolver.Evaluate(@"C:\LR\Legado\12.1.2606\Sau-PEP", "$/L/Sau-PEP", true, Ok,
      Info(new WorkspaceMapping("$/Linha-RM/atual/release", @"C:\LR\Atual\Release", false)));

    Assert.Equal(MappingState.NotMapped, check.State);
    Assert.Contains("tf workfold", check.Guidance);
  }

  [Fact]
  public void Evaluate_CaminhoCloaked_Cloaked()
  {
    var check = MappingResolver.Evaluate(@"C:\LR\Atual\Release\Sau-PEP", "$/R/Sau-PEP", true, Ok,
      Info(new WorkspaceMapping("$/R", @"C:\LR\Atual\Release", false), new WorkspaceMapping("$/R/Sau-PEP", null, true)));

    Assert.Equal(MappingState.Cloaked, check.State);
  }

  [Fact]
  public void Evaluate_MapeadoParaOutroCaminho_Divergent()
  {
    var check = MappingResolver.Evaluate(@"C:\LR\Legado\12.1.2606\Sau-PEP", "$/Linha-RM/Legado/12.1.2606/Sau-PEP", true, Ok,
      Info(new WorkspaceMapping("$/Linha-RM/Legado/12.1.2602", @"C:\LR\Legado\12.1.2606", false)));

    Assert.Equal(MappingState.Divergent, check.State);
  }

  [Fact]
  public void Evaluate_ServidorIndisponivel_QueryFailed()
  {
    var network = new TfResult(TfStatus.NetworkError, 100, "TF400324", TimeSpan.Zero, "tf");

    Assert.Equal(MappingState.QueryFailed, MappingResolver.Evaluate(@"C:\LR\x", "$/x", true, network, null).State);
  }
}

public class TfErrorClassifierTests
{
  [Theory]
  [InlineData(0, "", TfStatus.Success)]
  [InlineData(1, "conflito", TfStatus.PartialSuccess)]
  [InlineData(2, "", TfStatus.NotRecognized)]
  [InlineData(100, "falhou", TfStatus.Failed)]
  [InlineData(100, "TF400324: o Azure DevOps services não está disponível", TfStatus.NetworkError)]
  [InlineData(100, "TF30063: You are not authorized", TfStatus.AuthError)]
  public void Classify_ExitCodeESaida_StatusEsperado(int exitCode, string output, TfStatus expected)
  {
    var result = TfErrorClassifier.Classify(new CommandResult(exitCode, output, "", TimeSpan.Zero), "tf");

    Assert.Equal(expected, result.Status);
  }

  [Fact]
  public void Classify_Cancelado_NuncaSucesso()
  {
    Assert.Equal(TfStatus.Cancelled, TfErrorClassifier.Classify(new CommandResult(0, "", "", TimeSpan.Zero, Cancelled: true), "tf").Status);
  }
}

public class TfOutputParserTests
{
  [Fact]
  public void ExtractServerPaths_RemoveVersaoESeparaSetaDoMerge()
  {
    var paths = TfOutputParser.ExtractServerPaths("merge, edit: $/A/src/Arquivo Com Espaço.cs;C861799~C861799 -> $/B/src/Arquivo Com Espaço.cs;C861000");

    Assert.Equal(["$/A/src/Arquivo Com Espaço.cs", "$/B/src/Arquivo Com Espaço.cs"], paths);
  }

  [Fact]
  public void ExtractServerPaths_ItensDoChangeset()
  {
    const string output = """
      Changeset: 861799
      Items:
        edit $/Linha-RM/atual/release/Sau-PEP/a.cs
        add $/Linha-RM/atual/release/Sau-Saude/b.cs
      """;

    Assert.Equal(2, TfOutputParser.ExtractServerPaths(output).Count);
  }

  [Fact]
  public void ParseLeadingNumbers_ListaDeCandidatos()
  {
    const string output = """
      Changeset Author        Date
      --------- ------------- ----------
      861799    dev           12/09/2026
      861800*   dev           12/09/2026
      """;

    var numbers = TfOutputParser.ParseLeadingNumbers(output);

    Assert.Contains(861799, numbers);
    Assert.Contains(861800, numbers);
    Assert.DoesNotContain(0, numbers);
  }
}

public class ChangesetScopeTests
{
  [Fact]
  public void Analyze_ItensDeVariosProjetos_SeparaIncluidosEExcluidos()
  {
    var info = new ChangesetInfo(1, [],
    [
      "$/Linha-RM/atual/release/Sau-PEP/a.cs",
      "$/Linha-RM/atual/release/Sau-Saude/b.cs",
      "$/Outro/c.cs",
    ]);

    var scope = ChangesetScope.Analyze(info, "$/Linha-RM/atual/release/Sau-PEP", "$/Linha-RM/atual/release");

    Assert.Single(scope.Included);
    Assert.Single(scope.OtherProjects);
    Assert.Single(scope.OutsideSource);
    Assert.Equal(@"C:\LR\Legado\12.1.2606\Sau-PEP\a.cs", scope.TargetLocalFiles("$/Linha-RM/atual/release/Sau-PEP", @"C:\LR\Legado\12.1.2606\Sau-PEP").Single());
  }

  [Fact]
  public void Analyze_PastaComNomeParecido_NaoIncluiSauPepX()
  {
    var info = new ChangesetInfo(1, [], ["$/R/Sau-PEP-Smart-X/a.cs"]);

    Assert.Empty(ChangesetScope.Analyze(info, "$/R/Sau-PEP", "$/R").Included);
  }
}

public class TfExeClientTests
{
  [Fact]
  public async Task MergeChangeset_ArgumentosDeUmUnicoChangesetSemBaseless()
  {
    var executor = new FakeCommandExecutor();
    var client = new TfExeClient(executor, _ => Task.FromResult(@"C:\tf\TF.exe"), _ => true);

    await client.MergeChangesetAsync("$/R/Sau-PEP", "$/L/Sau-PEP", 861799, @"C:\Pasta Com Espaço", null, CancellationToken.None);

    var arguments = executor.Executed.Single().Arguments;
    Assert.Equal("merge", arguments[0]);
    Assert.Contains("/version:C861799~C861799", arguments);
    Assert.Contains("/noimplicitbaseless", arguments);
    Assert.DoesNotContain(arguments, a => a.Contains("baseless", StringComparison.OrdinalIgnoreCase) && a != "/noimplicitbaseless");
    Assert.DoesNotContain(arguments, a => a.Equals("/force", StringComparison.OrdinalIgnoreCase) || a.StartsWith("/auto", StringComparison.OrdinalIgnoreCase));
  }

  [Fact]
  public async Task GetChangeset_CaminhoNoComentario_NaoViraItem()
  {
    const string output = """
      Changeset: 861799
      User: dev
      Comment:
        Ajuste conforme $/Outro/Projeto/doc.md
      Items:
        edit $/Linha-RM/atual/release/Sau-PEP/a.cs
        merge, edit $/Linha-RM/atual/release/Sau-PEP/b.cs
      """;
    var executor = new FakeCommandExecutor { Respond = _ => new CommandResult(0, output, "", TimeSpan.Zero) };
    var client = new TfExeClient(executor, _ => Task.FromResult(@"C:\tf\TF.exe"), _ => true);

    var query = await client.GetChangesetAsync(861799, "https://col", CancellationToken.None);

    Assert.Equal(["$/Linha-RM/atual/release/Sau-PEP/a.cs", "$/Linha-RM/atual/release/Sau-PEP/b.cs"], query.Changeset!.Items);
  }

  [Fact]
  public async Task PendingChanges_ItensSemCaminhoLocalEsperado_MarcaNaoConfiavel()
  {
    const string output = "$/Linha-RM/Legado/12.1.2606/Sau-PEP/a.cs;C100\n  Local item : [PC] S:\\subst\\Sau-PEP\\a.cs";
    var executor = new FakeCommandExecutor { Respond = _ => new CommandResult(0, output, "", TimeSpan.Zero) };
    var client = new TfExeClient(executor, _ => Task.FromResult(@"C:\tf\TF.exe"), _ => true);

    var query = await client.GetPendingChangesAsync(@"C:\LR\Legado\12.1.2606\Sau-PEP", CancellationToken.None);

    Assert.True(query.HasUnmatchedItems);
    Assert.False(query.IsReliable);
  }

  [Fact]
  public async Task Merge_PastaDeTrabalhoInexistente_NaoExecutaTf()
  {
    var executor = new FakeCommandExecutor();
    var client = new TfExeClient(executor, _ => Task.FromResult(@"C:\tf\TF.exe"), _ => false);

    await Assert.ThrowsAsync<PEPCliHelper.Core.Common.PreconditionException>(() =>
      client.MergeChangesetAsync("$/R/Sau-PEP", "$/L/Sau-PEP", 1, @"C:\nao\existe", null, CancellationToken.None));
    Assert.Empty(executor.Executed);
  }

  [Fact]
  public async Task MergeCandidates_SemVersionPoisTfNaoCombinaComCandidate()
  {
    const string output = """
      Conjunto de alterações Usuário                Data
      ---------------------- ---------------------- ----------
      669990                 Outra Pessoa           10/09/2026
      669997                 Gustavo Amorim Queiroz 11/09/2026
      """;
    var executor = new FakeCommandExecutor { Respond = _ => new CommandResult(0, output, "", TimeSpan.Zero) };
    var client = new TfExeClient(executor, _ => Task.FromResult(@"C:\tf\TF.exe"), _ => true);

    var query = await client.GetMergeCandidatesAsync("$/Linha-RM/Atual/Release/Sau-PEP", "$/Linha-RM/Legado/12.1.2606/Sau-PEP", 669997, @"C:\x", CancellationToken.None);

    var arguments = executor.Executed.Single().Arguments;
    Assert.Contains("/candidate", arguments);
    Assert.DoesNotContain(arguments, a => a.StartsWith("/version", StringComparison.OrdinalIgnoreCase));
    Assert.Contains(669997, query.Changesets);
  }

  [Fact]
  public async Task Changeset_SaidaRealPtBr_ExtraiItemDoProjeto()
  {
    const string output = """
      Conjunto de alterações: 669997
      Usuário: Gustavo Amorim Queiroz
      Aprovado por: Linha-RM Build Service (totvstfs)
      Data: sexta-feira, 11 de setembro de 2026 16:00:55

      Comentário:
        FIX DSAUPEPCONV-24699 Correção lentidão ao solicitar exame - remoção comentário

      Itens:
        editar $/Linha-RM/Atual/Release/Sau-PEP/RM.Pep.ExamRequest.Server/Infra/SolicitacaoExameRepositorio.cs

      Observações de Check-in:
        Descrição para o cliente:
          FIX DSAUPEPCONV-24699 Correção lentidão ao solicitar exame
      """;
    var executor = new FakeCommandExecutor { Respond = _ => new CommandResult(0, output, "", TimeSpan.Zero) };
    var client = new TfExeClient(executor, _ => Task.FromResult(@"C:\tf\TF.exe"), _ => true);

    var query = await client.GetChangesetAsync(669997, "https://col", CancellationToken.None);

    Assert.Equal(["$/Linha-RM/Atual/Release/Sau-PEP/RM.Pep.ExamRequest.Server/Infra/SolicitacaoExameRepositorio.cs"], query.Changeset!.Items);
  }

  [Fact]
  public void Workfold_SaidaRealPtBr_ExtraiWorkspaceEMapeamento()
  {
    const string output = """
      ===============================================================================
      Workspace: SPON010126430 (Manoel Vitor Brito Moura)
      Coleção  : https://totvstfs.visualstudio.com/DefaultCollection
       $/Linha-RM/Legado/12.1.2506/Sau-PEP: C:\Linha-RM\Legado\12.1.2506\Sau-PEP
      """;

    var info = WorkfoldParser.Parse(output);

    Assert.Equal("SPON010126430", info.WorkspaceName);
    Assert.Equal("https://totvstfs.visualstudio.com/DefaultCollection", info.Collection);
    Assert.Contains(info.Mappings, m => m.ServerPath == "$/Linha-RM/Legado/12.1.2506/Sau-PEP" && m.LocalPath == @"C:\Linha-RM\Legado\12.1.2506\Sau-PEP");
  }

  [Fact]
  public async Task Conflitos_SaidaRealPtBrComCaminhoRelativo_DetectaComCaminhoAbsoluto()
  {
    const string output = "\r\nRM.Pep.ExamRequest.Server\\Infra\\SolicitacaoExameRepositorio.cs: A origem e o destino têm alterações.\r\n";
    var executor = new FakeCommandExecutor { Respond = _ => new CommandResult(1, output, "", TimeSpan.Zero) };
    var client = new TfExeClient(executor, _ => Task.FromResult(@"C:\tf\TF.exe"), _ => true);

    var query = await client.GetConflictsAsync(@"C:\Linha-RM\Legado\12.1.2506\Sau-PEP", CancellationToken.None);

    var conflict = Assert.Single(query.Items);
    Assert.StartsWith(@"C:\Linha-RM\Legado\12.1.2506\Sau-PEP\RM.Pep.ExamRequest.Server\Infra\SolicitacaoExameRepositorio.cs:", conflict);
  }

  [Fact]
  public async Task Conflitos_SaidaRealSemConflitos_ListaVazia()
  {
    var executor = new FakeCommandExecutor { Respond = _ => new CommandResult(0, "Não há conflitos para resolver.\r\n", "", TimeSpan.Zero) };
    var client = new TfExeClient(executor, _ => Task.FromResult(@"C:\tf\TF.exe"), _ => true);

    var query = await client.GetConflictsAsync(@"C:\Linha-RM\Legado\12.1.2606\Sau-PEP", CancellationToken.None);

    Assert.Empty(query.Items);
  }

  [Fact]
  public async Task GetLatest_NuncaForceOuOverwrite()
  {
    var executor = new FakeCommandExecutor();
    var client = new TfExeClient(executor, _ => Task.FromResult(@"C:\tf\TF.exe"), _ => true);

    await client.GetLatestAsync(@"C:\LR\Atual\Release\Sau-PEP", null, CancellationToken.None);

    Assert.DoesNotContain(executor.Executed.Single().Arguments, a => a is "/force" or "/overwrite");
  }

  [Fact]
  public async Task Resolve_ApenasPreview()
  {
    var executor = new FakeCommandExecutor();
    var client = new TfExeClient(executor, _ => Task.FromResult(@"C:\tf\TF.exe"), _ => true);

    await client.GetConflictsAsync(@"C:\LR\x", CancellationToken.None);

    Assert.Contains("/preview", executor.Executed.Single().Arguments);
  }

  [Fact]
  public void InterfaceTfvc_NaoExpoeCheckInUndoOuWorkspace()
  {
    var forbidden = new[] { "checkin", "undo", "shelve", "unshelve", "delete", "destroy", "workfoldmap", "createworkspace", "baseless", "resolveauto", "rollback" };

    var methods = typeof(ITfvcClient).GetMethods(BindingFlags.Public | BindingFlags.Instance).Select(m => m.Name.ToLowerInvariant()).ToList();

    Assert.DoesNotContain(methods, name => forbidden.Any(name.Contains));
  }
}
