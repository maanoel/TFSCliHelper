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

  private MergeRequest Request(bool stopOnFailure = false, params string[] targets) => new(
    _catalog.FindProject("back")!,
    _catalog.Current!,
    targets.Select(t => _catalog.ResolveVersion(t).Version!).ToList(),
    TestData.Changeset,
    stopOnFailure);

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
  public async Task Execute_ConflitoComStopOnFailure_InformaMantemEstadoEInterrompeProximos()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606", "12.1.2602");
    AddConflictOnMerge(tfvc, "12.1.2606");
    var (plan, planner) = await PlanAsync(tfvc, Request(true, "2606", "2602"));

    var result = await ExecuteAsync(tfvc, planner, plan);

    Assert.Equal(MergeTargetState.AppliedWithConflicts, result.Targets[0].State);
    Assert.Single(result.Targets[0].Conflicts);
    Assert.Equal(MergeTargetState.NotStarted, result.Targets[1].State);
    Assert.Equal(1, tfvc.MergeCount);
    Assert.Equal(ExitCodes.ConflictOrPartial, result.ExitCode);
    Assert.DoesNotContain(tfvc.Calls, c => c.Contains("resolve:", StringComparison.Ordinal));
  }

  [Fact]
  public async Task Execute_ConflitoNoPrimeiroPorPadrao_ExecutaTodosOsDestinos()
  {
    var catalog = TestData.Catalog(3);
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606", "12.1.2602", "12.1.2510");
    AddConflictOnMerge(tfvc, "12.1.2606");
    var request = new MergeRequest(catalog.FindProject("back")!, catalog.Current!, catalog.ActiveLegacy, TestData.Changeset);
    var planner = new MergePlanner(tfvc, _fileSystem, null);
    var plan = await planner.PlanAsync(request, catalog, "https://col", NullOperationLog.Instance, CancellationToken.None);

    var result = await ExecuteAsync(tfvc, planner, plan);

    Assert.Equal(3, tfvc.MergeCount);
    Assert.Equal(MergeTargetState.AppliedWithConflicts, result.Targets[0].State);
    Assert.Equal(MergeTargetState.AppliedWithPendingChanges, result.Targets[1].State);
    Assert.Equal(MergeTargetState.AppliedWithPendingChanges, result.Targets[2].State);
    Assert.Equal(3, result.AppliedCount);
    Assert.Equal(ExitCodes.ConflictOrPartial, result.ExitCode);
    Assert.DoesNotContain(tfvc.Calls, c => c.Contains("resolve:", StringComparison.Ordinal));
  }

  [Fact]
  public async Task Execute_FalhaAposPrimeiroConcluidoComStopOnFailure_ParcialSemRollbackENaoIniciados()
  {
    var catalog = TestData.Catalog(3);
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606", "12.1.2602", "12.1.2510");
    tfvc.MergeStatus[TestData.Local("12.1.2602")] = TfStatus.Failed;
    var request = new MergeRequest(catalog.FindProject("back")!, catalog.Current!, catalog.ActiveLegacy, TestData.Changeset, StopOnFailure: true);
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
  public async Task Execute_FalhaPorPadrao_SegueParaDemaisDestinos()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606", "12.1.2602");
    tfvc.MergeStatus[TestData.Local("12.1.2606")] = TfStatus.Failed;
    var (plan, planner) = await PlanAsync(tfvc, Request(false, "2606", "2602"));

    var result = await ExecuteAsync(tfvc, planner, plan);

    Assert.Equal(MergeTargetState.Failed, result.Targets[0].State);
    Assert.Equal(MergeTargetState.AppliedWithPendingChanges, result.Targets[1].State);
  }

  [Fact]
  public async Task Execute_IndeterminadoPorPadrao_InterrompeProximos()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606", "12.1.2602");
    tfvc.PendingAddedByMerge.Remove(TestData.Local("12.1.2606"));
    var (plan, planner) = await PlanAsync(tfvc, Request(false, "2606", "2602"));

    var result = await ExecuteAsync(tfvc, planner, plan);

    Assert.Equal(MergeTargetState.Indeterminate, result.Targets[0].State);
    Assert.Equal(MergeTargetState.NotStarted, result.Targets[1].State);
    Assert.Equal(1, tfvc.MergeCount);
  }

  [Fact]
  public async Task Execute_FalhaDeRedeNoMergePorPadrao_InterrompeProximos()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606", "12.1.2602");
    tfvc.MergeStatus[TestData.Local("12.1.2606")] = TfStatus.NetworkError;
    var (plan, planner) = await PlanAsync(tfvc, Request(false, "2606", "2602"));

    var result = await ExecuteAsync(tfvc, planner, plan);

    Assert.True(result.EnvironmentFailure);
    Assert.Equal(MergeTargetState.NotStarted, result.Targets[1].State);
    Assert.Equal(1, tfvc.MergeCount);
  }

  [Fact]
  public async Task Execute_Exit1ComAutoMergeSemConflitos_AplicadoComPendingChanges()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606");
    var local = TestData.Local("12.1.2606");
    tfvc.MergeStatus[local] = TfStatus.PartialSuccess;
    tfvc.MergeOutput[local] =
      $"Conflito resolvido automaticamente: mesclar, editar: {TestData.Server("12.1.2610")}/RM.Pep.Api/Controller.cs;C861799~C861799 -> {TestData.Server("12.1.2606")}/RM.Pep.Api/Controller.cs;C504089 como AutoMerge";
    var (plan, planner) = await PlanAsync(tfvc, Request(false, "2606"));

    var result = await ExecuteAsync(tfvc, planner, plan);

    Assert.Equal(MergeTargetState.AppliedWithPendingChanges, result.Targets.Single().State);
    Assert.Equal(ExitCodes.Success, result.ExitCode);
  }

  [Fact]
  public async Task Execute_Exit1ComCodigoTf_Indeterminado()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606");
    tfvc.MergeStatus[TestData.Local("12.1.2606")] = TfStatus.PartialSuccess;
    tfvc.MergeOutput[TestData.Local("12.1.2606")] = "TF10141: Nenhum arquivo pôde ser verificado.";
    var (plan, planner) = await PlanAsync(tfvc, Request(false, "2606"));

    var result = await ExecuteAsync(tfvc, planner, plan);

    Assert.Equal(MergeTargetState.Indeterminate, result.Targets.Single().State);
  }

  [Fact]
  public async Task Execute_Exit1SemConseguirListarConflitos_Indeterminado()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606");
    tfvc.MergeStatus[TestData.Local("12.1.2606")] = TfStatus.PartialSuccess;
    var (plan, planner) = await PlanAsync(tfvc, Request(false, "2606"));
    tfvc.ConflictStatus = TfStatus.Failed;

    var result = await ExecuteAsync(tfvc, planner, plan);

    Assert.Equal(MergeTargetState.Indeterminate, result.Targets.Single().State);
  }

  [Fact]
  public async Task Execute_PlanoRecente_RevalidacaoLeveComUmStatusAntesDoMerge()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606", "12.1.2602");
    var (plan, planner) = await PlanAsync(tfvc, Request(false, "2606", "2602"));
    var start = tfvc.Calls.Count;

    var result = await ExecuteAsync(tfvc, planner, plan);

    Assert.All(result.Targets, t => Assert.Equal(MergeTargetState.AppliedWithPendingChanges, t.State));
    foreach (var id in new[] { "12.1.2606", "12.1.2602" })
      Assert.Equal([$"status:{TestData.Local(id)}"], CallsBeforeMerge(tfvc, start, id));

    var execution = tfvc.Calls.Skip(start).ToList();
    Assert.DoesNotContain(execution, c => c.StartsWith("workfold:", StringComparison.Ordinal) || c.StartsWith("candidate:", StringComparison.Ordinal));
    Assert.Equal(4, execution.Count(c => c.StartsWith("status:", StringComparison.Ordinal)));
  }

  [Fact]
  public async Task Execute_PlanoComMaisDeDezMinutos_RevalidacaoCompleta()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606");
    var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 13, 9, 0, 0, TimeSpan.Zero));
    var planner = new MergePlanner(tfvc, _fileSystem, null, clock);
    var plan = await planner.PlanAsync(Request(false, "2606"), _catalog, "https://col", NullOperationLog.Instance, CancellationToken.None);
    var start = tfvc.Calls.Count;
    clock.Advance(TimeSpan.FromMinutes(11));

    var result = await new MergeExecutor(tfvc, planner, clock).ExecuteAsync(plan, NullOperationLog.Instance, CancellationToken.None);

    var beforeMerge = CallsBeforeMerge(tfvc, start, "12.1.2606");
    Assert.Equal(MergeTargetState.AppliedWithPendingChanges, result.Targets.Single().State);
    Assert.Contains(beforeMerge, c => c.StartsWith("workfold:", StringComparison.Ordinal));
    Assert.Contains(beforeMerge, c => c.StartsWith("candidate:", StringComparison.Ordinal));
    Assert.Contains(beforeMerge, c => c.StartsWith("resolve-preview:", StringComparison.Ordinal));
    Assert.Single(beforeMerge, c => c.StartsWith("status:", StringComparison.Ordinal));
  }

  [Fact]
  public async Task Execute_PendingNoEscopoAposPlano_BloqueadoNaRevalidacaoLeve()
  {
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606");
    var (plan, planner) = await PlanAsync(tfvc, Request(false, "2606"));
    tfvc.Pending[TestData.Local("12.1.2606")] = [$@"Local item : [PC] {TestData.Local("12.1.2606")}\RM.Pep.Api\Controller.cs"];

    var result = await ExecuteAsync(tfvc, planner, plan);

    Assert.Equal(MergeTargetState.Blocked, result.Targets.Single().State);
    Assert.Equal(0, tfvc.MergeCount);
  }

  [Fact]
  public async Task Plan_DestinosAvaliadosEmParalelo_MantemOrdemELimiteDeConcorrencia()
  {
    var catalog = TestData.Catalog(5);
    string[] ids = ["12.1.2606", "12.1.2602", "12.1.2510", "12.1.2506", "12.1.2502"];
    var inner = TestData.HealthyTfvc(_fileSystem, ids);
    inner.Candidates[TestData.Server("12.1.2510")] = [];
    var tfvc = new SlowWorkfoldTfvc(inner);
    var request = new MergeRequest(catalog.FindProject("back")!, catalog.Current!, catalog.ActiveLegacy, TestData.Changeset);
    var planner = new MergePlanner(tfvc, _fileSystem, null);

    var plan = await planner.PlanAsync(request, catalog, "https://col", NullOperationLog.Instance, CancellationToken.None);

    Assert.Equal(ids, plan.Targets.Select(t => t.Target.Id));
    Assert.Equal(TargetReadiness.AlreadyIntegrated, plan.Targets[2].Readiness);
    Assert.All(plan.Targets.Where((_, i) => i != 2), t => Assert.Equal(TargetReadiness.Ready, t.Readiness));
    Assert.Equal(MergePlanner.MaxParallelTargets, tfvc.MaxInFlight);
  }

  /// <summary>Chamadas do destino na execução, antes do seu tf merge (consultas pós-merge de outros destinos são ignoradas).</summary>
  private static List<string> CallsBeforeMerge(FakeTfvcClient tfvc, int start, string id)
  {
    var local = TestData.Local(id);
    var calls = tfvc.Calls.Skip(start).ToList();
    var mergeIndex = calls.IndexOf($"merge:{local}");
    Assert.True(mergeIndex >= 0, $"merge não executado em {local}");
    return calls.Take(mergeIndex)
      .Where(c => c.EndsWith(local, StringComparison.OrdinalIgnoreCase) || c.EndsWith(TestData.Server(id), StringComparison.OrdinalIgnoreCase))
      .ToList();
  }

  private static void AddConflictOnMerge(FakeTfvcClient tfvc, string id)
  {
    tfvc.MergeStatus[TestData.Local(id)] = TfStatus.PartialSuccess;
    tfvc.ConflictsAddedByMerge[TestData.Local(id)] = [$@"{TestData.Local(id)}\RM.Pep.Api\Controller.cs: conflito de conteúdo"];
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
  private static TfResult Tf(TfStatus status, string output = "") => new(status, 0, output, TimeSpan.Zero, "tf");

  private const string AutoMergeLine = "Conflito resolvido automaticamente: mesclar, editar: $/a/x.cs;C667267~C667267 -> $/b/x.cs;C504089 como AutoMerge";

  [Theory]
  [InlineData(TfStatus.Success, 2, 0, "", MergeTargetState.AppliedWithPendingChanges)]
  [InlineData(TfStatus.Success, 0, 0, "", MergeTargetState.NoApplicableChanges)]
  [InlineData(TfStatus.PartialSuccess, 1, 1, "", MergeTargetState.AppliedWithConflicts)]
  [InlineData(TfStatus.PartialSuccess, 2, 0, AutoMergeLine, MergeTargetState.AppliedWithPendingChanges)]
  [InlineData(TfStatus.PartialSuccess, 1, 0, "TF10141: nenhum arquivo", MergeTargetState.Indeterminate)]
  [InlineData(TfStatus.PartialSuccess, 0, 0, AutoMergeLine, MergeTargetState.Indeterminate)]
  [InlineData(TfStatus.Failed, 0, 0, "", MergeTargetState.Failed)]
  [InlineData(TfStatus.Failed, 3, 0, "", MergeTargetState.Indeterminate)]
  [InlineData(TfStatus.Cancelled, 3, 0, "", MergeTargetState.Cancelled)]
  public void Classify_ResultadoDoTf_EstadoDistinguivel(TfStatus status, int newPending, int conflicts, string output, MergeTargetState expected)
  {
    Assert.Equal(expected, MergeOutcome.Classify(Tf(status, output), newPending, conflicts));
  }

  [Theory]
  [InlineData(MergeTargetState.Failed, true, true)]
  [InlineData(MergeTargetState.Failed, false, false)]
  [InlineData(MergeTargetState.AppliedWithConflicts, false, false)]
  [InlineData(MergeTargetState.AppliedWithConflicts, true, true)]
  [InlineData(MergeTargetState.Blocked, false, false)]
  [InlineData(MergeTargetState.Blocked, true, true)]
  [InlineData(MergeTargetState.Indeterminate, false, true)]
  [InlineData(MergeTargetState.Cancelled, false, true)]
  [InlineData(MergeTargetState.AppliedWithPendingChanges, true, false)]
  public void ShouldStop_PoliticaDeInterrupcao(MergeTargetState state, bool stopOnFailure, bool expected)
  {
    Assert.Equal(expected, MergeOutcome.ShouldStop(state, stopOnFailure, environmentFailure: false));
  }

  [Fact]
  public void ShouldStop_FalhaDeAmbiente_SempreInterrompe()
  {
    Assert.True(MergeOutcome.ShouldStop(MergeTargetState.AppliedWithPendingChanges, stopOnFailure: false, environmentFailure: true));
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
