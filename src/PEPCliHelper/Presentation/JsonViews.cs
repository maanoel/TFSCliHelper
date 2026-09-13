using PEPCliHelper.Core.Build;
using PEPCliHelper.Core.Get;
using PEPCliHelper.Core.Merge;
using PEPCliHelper.Core.Tfvc;

namespace PEPCliHelper.Presentation;

/// <summary>Formas estáveis da saída --json.</summary>
public static class JsonViews
{
  public static object Mapping(MappingCheck? check) => check is null
    ? new { estado = "nao-verificado" }
    : new
    {
      estado = check.State.ToString(),
      descricao = check.StateText,
      caminhoLocal = check.LocalPath,
      caminhoServidorEsperado = check.ExpectedServerPath,
      caminhoServidorEfetivo = check.EffectiveServerPath,
      workspace = check.WorkspaceName,
      proprietario = check.Owner,
      colecao = check.Collection,
      subpastasCloaked = check.CloakedChildren,
      mensagem = check.Message,
      orientacao = check.Guidance,
    };

  public static object MergePlan(MergePlan plan, bool dryRun) => new
  {
    dryRun,
    projeto = plan.Request.Project.Alias,
    origem = plan.Request.Source.Id,
    caminhoOrigem = plan.SourceLocation.ServerPath,
    changeset = plan.Request.Changeset,
    colecao = plan.Collection,
    podeExecutar = plan.CanExecute,
    impedimentosGlobais = plan.GlobalBlockers,
    avisosGlobais = plan.GlobalWarnings,
    escopo = plan.Scope is null ? null : new
    {
      incluidos = plan.Scope.Included,
      outrosProjetos = plan.Scope.OtherProjects,
      foraDaOrigem = plan.Scope.OutsideSource,
    },
    destinos = plan.Targets.Select(t => new
    {
      versao = t.Target.Id,
      situacao = t.Readiness.ToString(),
      caminhoLocal = t.Location.LocalPath,
      caminhoServidor = t.Location.ServerPath,
      mapeamento = Mapping(t.Mapping),
      workspaceLocal = t.IsLocalWorkspace,
      pendenciasNoEscopo = t.PendingInScope,
      pendenciasForaDoEscopo = t.PendingOutOfScope.Count,
      gravaveisSemCheckout = t.UnreconciledFiles,
      conflitosNoEscopo = t.ConflictsInScope,
      conflitosForaDoEscopo = t.ConflictsOutOfScope.Count,
      desatualizado = t.Outdated,
      itensPreview = t.PreviewItemCount,
      conflitosPrevistos = t.PredictedConflicts,
      impedimentos = t.Blockers,
      avisos = t.Warnings,
      observacoes = t.Notes,
    }),
  };

  public static object MergeResult(MergeExecutionResult result) => new
  {
    codigoSaida = result.ExitCode,
    situacao = result.Status,
    checkInRealizado = false,
    destinos = result.Targets.Select(t => new
    {
      versao = t.Target.Id,
      estado = t.State.ToString(),
      descricao = MergeTargetResult.Describe(t.State),
      mensagem = t.Message,
      novasPendencias = t.NewPendingChanges,
      conflitos = t.Conflicts,
      duracaoSegundos = t.Duration?.TotalSeconds,
    }),
  };

  public static object GetPlan(GetPlan plan) => new
  {
    podeExecutar = plan.CanExecute,
    alvos = plan.Targets.Select(t => new
    {
      versao = t.Location.Version.Id,
      projeto = t.Location.Project.Alias,
      caminhoLocal = t.Location.LocalPath,
      caminhoServidor = t.Location.ServerPath,
      pronto = t.IsReady,
      mapeamento = Mapping(t.Mapping),
      pendencias = t.PendingCount,
      desatualizado = t.Outdated,
      impedimentos = t.Blockers,
      avisos = t.Warnings,
    }),
  };

  public static object GetResult(GetExecutionResult result) => new
  {
    codigoSaida = result.ExitCode,
    alvos = result.Targets.Select(t => new
    {
      versao = t.Location.Version.Id,
      projeto = t.Location.Project.Alias,
      estado = t.State.ToString(),
      mensagem = t.Message,
      duracaoSegundos = t.Duration?.TotalSeconds,
    }),
  };

  public static object BuildPlan(BuildPlan plan) => new
  {
    msbuild = plan.MsBuild?.Path,
    podeExecutar = plan.CanExecute,
    impedimentosGlobais = plan.GlobalBlockers,
    alvos = plan.Targets.Select(t => new
    {
      versao = t.Location.Version.Id,
      projeto = t.Location.Project.Alias,
      solucao = t.Solution,
      pronto = t.IsReady,
      impedimentos = t.Blockers,
      avisos = t.Warnings,
    }),
  };

  public static object BuildResult(BuildExecutionResult result) => new
  {
    codigoSaida = result.ExitCode,
    alvos = result.Targets.Select(t => new
    {
      versao = t.Location.Version.Id,
      projeto = t.Location.Project.Alias,
      estado = t.State.ToString(),
      mensagem = t.Message,
      duracaoSegundos = t.Duration?.TotalSeconds,
    }),
  };
}
