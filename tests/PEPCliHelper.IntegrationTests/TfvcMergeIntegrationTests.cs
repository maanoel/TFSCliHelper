using System.Reflection;
using PEPCliHelper.Core.Catalog;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.Configuration;
using PEPCliHelper.Core.Execution;
using PEPCliHelper.Core.FileSystem;
using PEPCliHelper.Core.Merge;
using PEPCliHelper.Core.Tfvc;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace PEPCliHelper.IntegrationTests;

/// <summary>
/// Roteiro de homologação (docs/HOMOLOGACAO.md) contra uma coleção TFVC de TESTE com tf.exe, executor e
/// casos de uso reais. Nunca faz check-in ou undo; pode deixar pending changes no workspace de teste.
/// </summary>
public class TfvcMergeIntegrationTests
{
  private const string ProjectAlias = "it";

  private sealed class TestEnvironment
  {
    public required IntegrationConfig Config { get; init; }
    public required RecordingCommandExecutor Executor { get; init; }
    public required TfExeClient Tfvc { get; init; }
    public required MergePlanner Planner { get; init; }
    public required VersionCatalog Catalog { get; init; }
    public required VersionEntry Source { get; init; }
    public required ProjectDefinition Project { get; init; }

    public VersionEntry Target(int index) => Catalog.All.Single(v => v.Id == $"destino{index + 1}");

    public VersionEntry Version(string id) => Catalog.All.Single(v => v.Id == id);

    public Task<MergePlan> PlanAsync(int changeset, params VersionEntry[] targets) =>
      Planner.PlanAsync(new MergeRequest(Project, Source, targets, changeset), Catalog, Config.Collection, NullOperationLog.Instance, CancellationToken.None);

    public Task<MergeExecutionResult> ExecuteAsync(MergePlan plan) =>
      new MergeExecutor(Tfvc, Planner).ExecuteAsync(plan, NullOperationLog.Instance, CancellationToken.None);

    public IReadOnlyList<Command> MutatingCommands(int from = 0) =>
      Executor.Commands.Skip(from).Where(RecordingCommandExecutor.IsMutating).ToList();
  }

  private static TestEnvironment CreateEnvironment()
  {
    var config = IntegrationConfig.Load();
    var fileSystem = new PhysicalFileSystem();
    var process = new ProcessCommandExecutor();
    var recorder = new RecordingCommandExecutor(process);
    var locator = new ToolLocator(fileSystem, process, new ToolsConfig { TfExe = config.TfExe });
    var tfvc = new TfExeClient(recorder, async ct => (await locator.RequireTfAsync(ct)).Path!);

    var versions = new List<VersionEntry> { new("origem", true, true, config.SourceLocal, config.SourceServer, [], null) };
    versions.AddRange(config.Targets.Select((t, i) => new VersionEntry($"destino{i + 1}", false, true, t.LocalPath, t.ServerPath, [], null)));
    if (config.NoRelationTarget is { } noRelation)
      versions.Add(new VersionEntry("semRelacao", false, true, noRelation.LocalPath, noRelation.ServerPath, [], null));
    if (config.UnmappedTarget is { } unmapped)
      versions.Add(new VersionEntry("semMapeamento", false, true, unmapped.LocalPath, unmapped.ServerPath, [], null));

    var project = new ProjectDefinition(ProjectAlias, "Integração", string.Empty, string.Empty, null);
    var catalog = new VersionCatalog(versions, [project]);
    return new TestEnvironment
    {
      Config = config,
      Executor = recorder,
      Tfvc = tfvc,
      Planner = new MergePlanner(tfvc, fileSystem, null),
      Catalog = catalog,
      Source = versions[0],
      Project = project,
    };
  }

  private static void AssertReady(MergePlan plan)
  {
    var blockers = plan.GlobalBlockers.Concat(plan.Targets.SelectMany(t => t.Blockers)).ToList();
    Assert.True(plan.CanExecute && plan.Targets.All(t => t.Readiness == TargetReadiness.Ready),
      "Plano não está pronto (limpe as pending changes do workspace de teste e confira os changesets): " + string.Join(" | ", blockers));
  }

  private static void AssertNoForbiddenCommand(TestEnvironment env) =>
    Assert.DoesNotContain(env.Executor.Commands, c => c.Arguments.Any(a =>
      a.Equals("checkin", StringComparison.OrdinalIgnoreCase) || a.Equals("undo", StringComparison.OrdinalIgnoreCase)
      || a.Equals("/baseless", StringComparison.OrdinalIgnoreCase)));

  private async Task AssertAppliedOnFirstTargetAsync(int changeset)
  {
    var env = CreateEnvironment();
    var plan = await env.PlanAsync(changeset, env.Target(0));
    AssertReady(plan);

    var result = await env.ExecuteAsync(plan);

    Assert.Equal(MergeTargetState.AppliedWithPendingChanges, result.Targets.Single().State);
    AssertNoForbiddenCommand(env);
  }

  [IntegrationFact("changesets.edit")]
  public async Task H3_MergeDoisDestinos_AplicadoComPendingChangesSemCheckIn()
  {
    var env = CreateEnvironment();
    var changeset = env.Config.Changesets.Edit!.Value;
    var plan = await env.PlanAsync(changeset, env.Target(0), env.Target(1));
    AssertReady(plan);

    var result = await env.ExecuteAsync(plan);

    Assert.All(result.Targets, t => Assert.Equal(MergeTargetState.AppliedWithPendingChanges, t.State));
    Assert.Equal(ExitCodes.Success, result.ExitCode);
    Assert.Equal(2, env.MutatingCommands().Count(c => c.Arguments[0].Equals("merge", StringComparison.OrdinalIgnoreCase)));
    Assert.All(env.MutatingCommands(), c => Assert.Equal("merge", c.Arguments[0], ignoreCase: true));
    Assert.DoesNotContain(typeof(ITfvcClient).GetMethods(BindingFlags.Public | BindingFlags.Instance),
      m => m.Name.Contains("checkin", StringComparison.OrdinalIgnoreCase) || m.Name.Contains("undo", StringComparison.OrdinalIgnoreCase));
    AssertNoForbiddenCommand(env);
  }

  [IntegrationFact("changesets.add")]
  public Task H4_ChangesetDeAdicao_AplicadoComPendingChanges() =>
    AssertAppliedOnFirstTargetAsync(IntegrationConfig.Load().Changesets.Add!.Value);

  [IntegrationFact("changesets.delete")]
  public Task H4_ChangesetDeExclusao_AplicadoComPendingChanges() =>
    AssertAppliedOnFirstTargetAsync(IntegrationConfig.Load().Changesets.Delete!.Value);

  [IntegrationFact("changesets.rename")]
  public Task H4_ChangesetDeRenomeacao_AplicadoComPendingChanges() =>
    AssertAppliedOnFirstTargetAsync(IntegrationConfig.Load().Changesets.Rename!.Value);

  [IntegrationFact("changesets.singleChangeset")]
  public async Task H5_UmChangesetEspecifico_UsaVersaoUnicaEPendenciasSoDoEscopo()
  {
    var env = CreateEnvironment();
    var changeset = env.Config.Changesets.SingleChangeset!.Value;
    var plan = await env.PlanAsync(changeset, env.Target(0));
    AssertReady(plan);

    var result = await env.ExecuteAsync(plan);

    var target = result.Targets.Single();
    Assert.Equal(MergeTargetState.AppliedWithPendingChanges, target.State);
    var versionSpecs = env.Executor.Commands
      .Where(c => c.Arguments[0].Equals("merge", StringComparison.OrdinalIgnoreCase))
      .SelectMany(c => c.Arguments.Where(a => a.StartsWith("/version:", StringComparison.OrdinalIgnoreCase)))
      .Distinct()
      .ToList();
    Assert.Equal([$"/version:C{changeset}~C{changeset}"], versionSpecs);

    var location = plan.Targets.Single().Location;
    var scopePaths = plan.Scope!.TargetLocalFiles(plan.SourceLocation.ServerPath, location.LocalPath)
      .Concat(plan.Scope.TargetServerItems(plan.SourceLocation.ServerPath, location.ServerPath))
      .ToList();
    Assert.All(target.NewPendingChanges, line => Assert.True(MergeOutcome.IsInScope(line, scopePaths), $"Pending change fora do changeset {changeset}: {line}"));
  }

  [IntegrationFact("changesets.alreadyIntegrated")]
  public async Task H6_JaIntegrado_NadaReaplicado()
  {
    var env = CreateEnvironment();

    var plan = await env.PlanAsync(env.Config.Changesets.AlreadyIntegrated!.Value, env.Target(0));

    Assert.Empty(plan.GlobalBlockers);
    Assert.Equal(TargetReadiness.AlreadyIntegrated, plan.Targets.Single().Readiness);
    Assert.False(plan.CanExecute);
    Assert.Empty(env.MutatingCommands());
  }

  [IntegrationFact("changesets.conflict")]
  public async Task H7_Conflito_AplicadoComConflitosSemResolver()
  {
    var env = CreateEnvironment();
    var plan = await env.PlanAsync(env.Config.Changesets.Conflict!.Value, env.Target(0));
    AssertReady(plan);

    var result = await env.ExecuteAsync(plan);

    var target = result.Targets.Single();
    Assert.Equal(MergeTargetState.AppliedWithConflicts, target.State);
    Assert.NotEmpty(target.Conflicts);
    Assert.Equal(ExitCodes.ConflictOrPartial, result.ExitCode);
    Assert.All(env.MutatingCommands(), c => Assert.Equal("merge", c.Arguments[0], ignoreCase: true));
    AssertNoForbiddenCommand(env);
  }

  [IntegrationFact("changesets.edit", "noRelationTarget")]
  public async Task H8_SemRelacaoDeMerge_BloqueadoSemBaseless()
  {
    var env = CreateEnvironment();

    var plan = await env.PlanAsync(env.Config.Changesets.Edit!.Value, env.Version("semRelacao"));

    Assert.Equal(TargetReadiness.Blocked, plan.Targets.Single().Readiness);
    Assert.False(plan.CanExecute);
    Assert.Empty(env.MutatingCommands());
    AssertNoForbiddenCommand(env);
  }

  [IntegrationFact("changesets.edit", "unmappedTarget")]
  public async Task H9_PastaSemMapeamento_Bloqueado()
  {
    var env = CreateEnvironment();

    var plan = await env.PlanAsync(env.Config.Changesets.Edit!.Value, env.Version("semMapeamento"));

    var target = plan.Targets.Single();
    Assert.Equal(TargetReadiness.Blocked, target.Readiness);
    Assert.False(target.Mapping?.IsValid ?? false);
    Assert.Empty(env.MutatingCommands());
  }

  [IntegrationFact("changesets.pendingInScope")]
  public async Task H10_PendingNoEscopo_BloqueadoEAlteracaoPreservada()
  {
    var env = CreateEnvironment();
    var localPath = env.Target(0).LocalRoot;
    var before = await env.Tfvc.GetPendingChangesAsync(localPath, CancellationToken.None);

    var plan = await env.PlanAsync(env.Config.Changesets.PendingInScope!.Value, env.Target(0));

    var target = plan.Targets.Single();
    Assert.Equal(TargetReadiness.Blocked, target.Readiness);
    Assert.NotEmpty(target.PendingInScope);
    Assert.Empty(env.MutatingCommands());
    var after = await env.Tfvc.GetPendingChangesAsync(localPath, CancellationToken.None);
    Assert.Equal(before.Items, after.Items);
  }

  [IntegrationFact("changesets.edit")]
  public async Task H14_DryRun_TfStatusIgualAntesEDepois()
  {
    var env = CreateEnvironment();
    var targets = new[] { env.Target(0), env.Target(1) };
    var before = await StatusAsync(env, targets);
    var start = env.Executor.Commands.Count;

    await env.PlanAsync(env.Config.Changesets.Edit!.Value, targets);

    Assert.Empty(env.MutatingCommands(start));
    Assert.Equal(before, await StatusAsync(env, targets));
  }

  private static async Task<List<string>> StatusAsync(TestEnvironment env, IEnumerable<VersionEntry> targets)
  {
    var lines = new List<string>();
    foreach (var target in targets)
    {
      var query = await env.Tfvc.GetPendingChangesAsync(target.LocalRoot, CancellationToken.None);
      Assert.True(query.IsReliable, $"tf status não confiável em {target.LocalRoot}: {query.Result.Summary}");
      lines.AddRange(query.Items.Order(StringComparer.OrdinalIgnoreCase));
    }

    return lines;
  }
}
