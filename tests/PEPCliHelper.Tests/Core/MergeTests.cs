using PEPCliHelper.Core.Catalog;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.History;
using PEPCliHelper.Core.Merge;
using PEPCliHelper.Core.Tfvc;
using PEPCliHelper.Tests.Fakes;

namespace PEPCliHelper.Tests.Core;

/// <summary>Cenários de aceite do merge (specs 006 e 007) com TFVC falso.</summary>
public class MergeTests
{
  private readonly InMemoryFileSystem _fileSystem = new();
  private readonly VersionCatalog _catalog = TestData.Catalog();

  private MergeRequest Request(bool continueOnFailure = false, params string[] targets) => new(
    _catalog.FindProject("back")!,
    _catalog.Current!,
    targets.Select(t => _catalog.ResolveVersion(t).Version!).ToList(),
    TestData.Changeset,
    continueOnFailure);

  private async Task<(MergePlan Plan, MergePlanner Planner)> PlanAsync(FakeTfvcClient tfvc, MergeRequest request)
  {
    var planner = new MergePlanner(tfvc, _fileSystem, null);
    var plan = await planner.PlanAsync(request, _catalog, "https://col", NullOperationLog.Instance, CancellationToken.None);
    return (plan, planner);
  }

  private static Task<MergeExecutionResult> ExecuteAsync(FakeTfvcClient tfvc, MergePlanner planner, MergePlan plan, CancellationToken cancellationToken = default) =>
    new MergeExecutor(tfvc, planner).ExecuteAsync(plan, NullOperationLog.Instance, cancellationToken);

  [Fact]
  public async Task Merge_DuasVersoesValidas_AplicaEmCadaDestinoSemCheckIn()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606", "12.1.2602");
    var (plan, planner) = await PlanAsync(tfvc, Request(false, "2606", "2602"));

    var result = await ExecuteAsync(tfvc, planner, plan);

    Assert.All(result.Targets, t => Assert.Equal(MergeTargetState.AppliedWithPendingChanges, t.State));
    Assert.Equal(ExitCodes.Success, result.ExitCode);
    Assert.Equal(2, tfvc.MergeCount);
    Assert.DoesNotContain(tfvc.Calls, c => c.Contains("checkin", StringComparison.OrdinalIgnoreCase));
  }

  [Fact]
  public async Task Plan_DestinoSemMapeamento_BloqueiaEOrientaSemAlterar()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606");
    tfvc.Workfolds.Remove(@"C:\LR\Legado\12.1.2606");

    var (plan, _) = await PlanAsync(tfvc, Request(false, "2606"));

    var target = plan.Targets.Single();
    Assert.Equal(TargetReadiness.Blocked, target.Readiness);
    Assert.Equal(MappingState.NotMapped, target.Mapping!.State);
    Assert.Contains(target.Blockers, b => b.Contains("tf workfold"));
    Assert.False(plan.CanExecute);
    Assert.Equal(0, tfvc.MergeCount);
  }

  [Fact]
  public async Task Plan_PendingChangeNoArquivoDoChangeset_BloqueiaEPreserva()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606");
    tfvc.Pending[TestData.Local("12.1.2606")] = [$@"Local item : [PC] {TestData.Local("12.1.2606")}\RM.Pep.Api\Controller.cs"];

    var (plan, _) = await PlanAsync(tfvc, Request(false, "2606"));

    Assert.Equal(TargetReadiness.Blocked, plan.Targets.Single().Readiness);
    Assert.Single(plan.Targets.Single().PendingInScope);
    Assert.Equal(0, tfvc.MergeCount);
  }

  [Fact]
  public async Task Plan_PendingChangeForaDoEscopo_InformaSemBloquear()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606");
    tfvc.Pending[TestData.Local("12.1.2606")] = [$@"Local item : [PC] {TestData.Local("12.1.2606")}\Outro\Arquivo.cs"];

    var (plan, _) = await PlanAsync(tfvc, Request(false, "2606"));

    Assert.Equal(TargetReadiness.Ready, plan.Targets.Single().Readiness);
    Assert.Single(plan.Targets.Single().PendingOutOfScope);
  }

  [Fact]
  public async Task Plan_ArquivoGravavelSemCheckout_BloqueiaComoAlteracaoNaoReconciliada()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606");
    _fileSystem.AddFile($@"{TestData.Local("12.1.2606")}\RM.Pep.Api\Controller.cs", "editado", readOnly: false);

    var (plan, _) = await PlanAsync(tfvc, Request(false, "2606"));

    Assert.Equal(TargetReadiness.Blocked, plan.Targets.Single().Readiness);
    Assert.Single(plan.Targets.Single().UnreconciledFiles);
  }

  [Fact]
  public async Task Plan_ChangesetComItensDeOutroProjeto_ExplicitaIncluidosEExcluidos()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606");
    tfvc.Changeset = new ChangesetInfo(TestData.Changeset, [],
      [$"{TestData.Server("12.1.2610")}/RM.Pep.Api/Controller.cs", $"{TestData.Server("12.1.2610", "Sau-Saude")}/RM.Sau/Servico.cs"]);

    var (plan, _) = await PlanAsync(tfvc, Request(false, "2606"));

    Assert.Single(plan.Scope!.Included);
    Assert.Single(plan.Scope.OtherProjects);
    Assert.Contains(plan.GlobalWarnings, w => w.Contains("FORA do merge"));
  }

  [Fact]
  public async Task Plan_ChangesetSemItensDoProjeto_BloqueioGlobal()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606");
    tfvc.Changeset = new ChangesetInfo(TestData.Changeset, [], [$"{TestData.Server("12.1.2610", "Sau-Saude")}/RM.Sau/Servico.cs"]);

    var (plan, _) = await PlanAsync(tfvc, Request(false, "2606"));

    Assert.NotEmpty(plan.GlobalBlockers);
    Assert.False(plan.CanExecute);
  }

  [Fact]
  public async Task Plan_DryRun_SomenteConsultasNenhumMergeOuGet()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606", "12.1.2602");

    var (plan, _) = await PlanAsync(tfvc, Request(false, "2606", "2602"));

    Assert.Equal(ExitCodes.Success, plan.DryRunExitCode);
    Assert.DoesNotContain(tfvc.Calls, c => c.StartsWith("merge:", StringComparison.Ordinal) || c.StartsWith("get:", StringComparison.Ordinal));
    Assert.Contains(tfvc.Calls, c => c.StartsWith("merge-preview:", StringComparison.Ordinal));
  }

  [Fact]
  public async Task Plan_ChangesetJaIntegrado_NaoReaplica()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606");
    tfvc.Candidates[TestData.Server("12.1.2606")] = [];

    var (plan, planner) = await PlanAsync(tfvc, Request(false, "2606"));

    Assert.Equal(TargetReadiness.AlreadyIntegrated, plan.Targets.Single().Readiness);
    Assert.False(plan.CanExecute);
    Assert.Equal(0, tfvc.MergeCount);
  }

  [Fact]
  public async Task Plan_SemRelacaoDeMerge_BloqueiaSemBaseless()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606");
    tfvc.CandidateStatus[TestData.Server("12.1.2606")] = TfStatus.Failed;

    var (plan, _) = await PlanAsync(tfvc, Request(false, "2606"));

    var target = plan.Targets.Single();
    Assert.Equal(TargetReadiness.Blocked, target.Readiness);
    Assert.Contains(target.Blockers, b => b.Contains("baseless"));
  }

  [Fact]
  public async Task Plan_ServidorIndisponivel_FalhaDeAmbienteInterrompeTudo()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606");
    tfvc.ChangesetStatus = TfStatus.NetworkError;

    var (plan, _) = await PlanAsync(tfvc, Request(false, "2606"));

    Assert.True(plan.EnvironmentFailure);
    Assert.Equal(ExitCodes.Precondition, plan.DryRunExitCode);
  }

  [Fact]
  public async Task Execute_Conflito_InformaMantemEstadoEInterrompeProximos()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606", "12.1.2602");
    tfvc.MergeStatus[TestData.Local("12.1.2606")] = TfStatus.PartialSuccess;
    tfvc.ConflictsAddedByMerge[TestData.Local("12.1.2606")] = [$@"{TestData.Local("12.1.2606")}\RM.Pep.Api\Controller.cs: conflito de conteúdo"];
    var (plan, planner) = await PlanAsync(tfvc, Request(false, "2606", "2602"));

    var result = await ExecuteAsync(tfvc, planner, plan);

    Assert.Equal(MergeTargetState.AppliedWithConflicts, result.Targets[0].State);
    Assert.Single(result.Targets[0].Conflicts);
    Assert.Equal(MergeTargetState.NotStarted, result.Targets[1].State);
    Assert.Equal(ExitCodes.ConflictOrPartial, result.ExitCode);
    Assert.DoesNotContain(tfvc.Calls, c => c.Contains("resolve:", StringComparison.Ordinal));
  }

  [Fact]
  public async Task Execute_FalhaAposPrimeiroConcluido_ParcialSemRollbackENaoIniciados()
  {
    var catalog = TestData.Catalog(3);
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606", "12.1.2602", "12.1.2510");
    tfvc.MergeStatus[TestData.Local("12.1.2602")] = TfStatus.Failed;
    var request = new MergeRequest(catalog.FindProject("back")!, catalog.Current!, catalog.ActiveLegacy, TestData.Changeset);
    var planner = new MergePlanner(tfvc, _fileSystem, null);
    var plan = await planner.PlanAsync(request, catalog, "https://col", NullOperationLog.Instance, CancellationToken.None);

    var result = await ExecuteAsync(tfvc, planner, plan);

    Assert.Equal(MergeTargetState.AppliedWithPendingChanges, result.Targets[0].State);
    Assert.Equal(MergeTargetState.Failed, result.Targets[1].State);
    Assert.Equal(MergeTargetState.NotStarted, result.Targets[2].State);
    Assert.Equal(ExitCodes.ConflictOrPartial, result.ExitCode);
    Assert.NotEmpty(tfvc.Pending[TestData.Local("12.1.2606")]);
  }

  [Fact]
  public async Task Execute_ContinueOnFailure_SegueParaDestinoIndependente()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606", "12.1.2602");
    tfvc.MergeStatus[TestData.Local("12.1.2606")] = TfStatus.Failed;
    var (plan, planner) = await PlanAsync(tfvc, Request(true, "2606", "2602"));

    var result = await ExecuteAsync(tfvc, planner, plan);

    Assert.Equal(MergeTargetState.Failed, result.Targets[0].State);
    Assert.Equal(MergeTargetState.AppliedWithPendingChanges, result.Targets[1].State);
  }

  [Fact]
  public async Task Execute_PendenciaPreexistenteForaDoEscopo_NaoContadaComoNova()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606");
    var old = $@"Local item : [PC] {TestData.Local("12.1.2606")}\Outro\Antigo.cs";
    tfvc.Pending[TestData.Local("12.1.2606")] = [old];
    var (plan, planner) = await PlanAsync(tfvc, Request(false, "2606"));

    var result = await ExecuteAsync(tfvc, planner, plan);

    Assert.Single(result.Targets[0].NewPendingChanges);
    Assert.DoesNotContain(old, result.Targets[0].NewPendingChanges);
  }

  [Fact]
  public async Task Execute_Cancelado_NaoIniciaDestinos()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606", "12.1.2602");
    var (plan, planner) = await PlanAsync(tfvc, Request(false, "2606", "2602"));
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    var result = await ExecuteAsync(tfvc, planner, plan, cancellation.Token);

    Assert.All(result.Targets, t => Assert.Equal(MergeTargetState.NotStarted, t.State));
    Assert.Equal(0, tfvc.MergeCount);
  }

  [Fact]
  public async Task Plan_PendingChangesComCaminhoNaoReconhecido_BloqueiaPorEstadoIndeterminado()
  {
    var tfvc = new UnmatchedPendingTfvc(TestData.HealthyTfvc(_fileSystem, "12.1.2606"));

    var planner = new MergePlanner(tfvc, _fileSystem, null);
    var plan = await planner.PlanAsync(Request(false, "2606"), _catalog, "https://col", NullOperationLog.Instance, CancellationToken.None);

    Assert.Equal(TargetReadiness.Blocked, plan.Targets.Single().Readiness);
    Assert.Contains(plan.Targets.Single().Blockers, b => b.Contains("indeterminado"));
  }

  [Fact]
  public async Task Execute_MergeSemNovasPendenciasComPreviewComItens_Indeterminado()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606");
    tfvc.PendingAddedByMerge.Clear();
    var (plan, planner) = await PlanAsync(tfvc, Request(false, "2606"));

    var result = await ExecuteAsync(tfvc, planner, plan);

    Assert.Equal(MergeTargetState.Indeterminate, result.Targets.Single().State);
    Assert.Equal(ExitCodes.ConflictOrPartial, result.ExitCode);
  }

  [Fact]
  public async Task Execute_CancelamentoEntreDestinos_Exit130()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606", "12.1.2602");
    var (plan, planner) = await PlanAsync(tfvc, Request(false, "2606", "2602"));
    using var cancellation = new CancellationTokenSource();
    var cancelling = new CancelAfterFirstMerge(tfvc, cancellation);

    var result = await new MergeExecutor(cancelling, planner).ExecuteAsync(plan, NullOperationLog.Instance, cancellation.Token);

    Assert.Equal(MergeTargetState.AppliedWithPendingChanges, result.Targets[0].State);
    Assert.Equal(MergeTargetState.NotStarted, result.Targets[1].State);
    Assert.Equal(ExitCodes.Cancelled, result.ExitCode);
  }

  [Fact]
  public async Task Plan_PreviewComConflitoPrevisto_ProntoComAvisoSemBloquear()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606");
    tfvc.PreviewStatus[TestData.Server("12.1.2606")] = TfStatus.PartialSuccess;

    var (plan, _) = await PlanAsync(tfvc, Request(false, "2606"));

    var target = plan.Targets.Single();
    Assert.Equal(TargetReadiness.Ready, target.Readiness);
    Assert.Single(target.PredictedConflicts);
    Assert.Contains(target.Warnings, w => w.Contains("conflito"));
  }

  [Fact]
  public async Task Plan_PreviewFalhaReal_Bloqueia()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606");
    tfvc.PreviewStatus[TestData.Server("12.1.2606")] = TfStatus.Failed;

    var (plan, _) = await PlanAsync(tfvc, Request(false, "2606"));

    Assert.Equal(TargetReadiness.Blocked, plan.Targets.Single().Readiness);
  }

  [Fact]
  public async Task Plan_HistoricoAnterior_ExibidoComoObservacao()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606");
    var journal = new JsonExecutionJournal(_fileSystem, @"C:\hist", TimeProvider.System);
    var session = journal.Begin("merge", ["merge"]);
    session.Describe("back", "12.1.2610", TestData.Changeset, ["12.1.2606"]);
    session.Complete(0, "concluido", [new TargetOutcome { Target = "12.1.2606", State = "AppliedWithPendingChanges", Message = "ok" }]);
    var planner = new MergePlanner(tfvc, _fileSystem, journal);

    var plan = await planner.PlanAsync(Request(false, "2606"), _catalog, "https://col", NullOperationLog.Instance, CancellationToken.None);

    Assert.Contains(plan.Targets.Single().Notes, n => n.Contains(session.Id));
  }
}

public class MergeOutcomeTests
{
  private static TfResult Tf(TfStatus status) => new(status, 0, "", TimeSpan.Zero, "tf");

  [Theory]
  [InlineData(TfStatus.Success, 2, 0, MergeTargetState.AppliedWithPendingChanges)]
  [InlineData(TfStatus.Success, 0, 0, MergeTargetState.NoApplicableChanges)]
  [InlineData(TfStatus.PartialSuccess, 1, 1, MergeTargetState.AppliedWithConflicts)]
  [InlineData(TfStatus.PartialSuccess, 1, 0, MergeTargetState.Indeterminate)]
  [InlineData(TfStatus.Failed, 0, 0, MergeTargetState.Failed)]
  [InlineData(TfStatus.Failed, 3, 0, MergeTargetState.Indeterminate)]
  [InlineData(TfStatus.Cancelled, 3, 0, MergeTargetState.Cancelled)]
  public void Classify_ResultadoDoTf_EstadoDistinguivel(TfStatus status, int newPending, int conflicts, MergeTargetState expected)
  {
    Assert.Equal(expected, MergeOutcome.Classify(Tf(status), newPending, conflicts));
  }

  [Theory]
  [InlineData(MergeTargetState.Failed, false, true)]
  [InlineData(MergeTargetState.Failed, true, false)]
  [InlineData(MergeTargetState.AppliedWithConflicts, true, false)]
  [InlineData(MergeTargetState.Indeterminate, true, true)]
  [InlineData(MergeTargetState.Cancelled, true, true)]
  [InlineData(MergeTargetState.AppliedWithPendingChanges, false, false)]
  public void ShouldStop_PoliticaDeInterrupcao(MergeTargetState state, bool continueOnFailure, bool expected)
  {
    Assert.Equal(expected, MergeOutcome.ShouldStop(state, continueOnFailure, environmentFailure: false));
  }

  [Fact]
  public void ShouldStop_FalhaDeAmbiente_SempreInterrompe()
  {
    Assert.True(MergeOutcome.ShouldStop(MergeTargetState.AppliedWithPendingChanges, true, environmentFailure: true));
  }

  [Theory]
  [InlineData(new[] { MergeTargetState.AppliedWithPendingChanges, MergeTargetState.AlreadyIntegrated }, ExitCodes.Success)]
  [InlineData(new[] { MergeTargetState.AppliedWithPendingChanges, MergeTargetState.Failed }, ExitCodes.ConflictOrPartial)]
  [InlineData(new[] { MergeTargetState.Failed, MergeTargetState.NotStarted }, ExitCodes.OperationFailed)]
  [InlineData(new[] { MergeTargetState.Blocked, MergeTargetState.AlreadyIntegrated }, ExitCodes.Precondition)]
  [InlineData(new[] { MergeTargetState.AppliedWithPendingChanges, MergeTargetState.Cancelled }, ExitCodes.Cancelled)]
  [InlineData(new[] { MergeTargetState.AppliedWithConflicts }, ExitCodes.ConflictOrPartial)]
  public void ComputeExitCode_EstadosPorDestino_CodigoEstavel(MergeTargetState[] states, int expected)
  {
    var version = new VersionEntry("v", false, true, @"C:\v", "$/v", [], null);
    var results = states.Select(s => new MergeTargetResult(version, s, "", [], [], null)).ToList();

    Assert.Equal(expected, MergeOutcome.ComputeExitCode(results));
  }

  [Fact]
  public void MergeRequestValidate_OrigemIgualDestino_Erro()
  {
    var catalog = TestData.Catalog();

    var errors = MergeRequest.Validate(catalog.FindProject("back"), catalog.Current, [catalog.Current!], 1);

    Assert.Contains(errors, e => e.Contains("não pode ser também destino"));
  }
}
