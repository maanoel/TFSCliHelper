using PEPCliHelper.Core.Catalog;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.Tfvc;

namespace PEPCliHelper.Core.Merge;

public sealed record MergeRequest(
  ProjectDefinition Project,
  VersionEntry Source,
  IReadOnlyList<VersionEntry> Targets,
  int Changeset,
  bool ContinueOnFailure = false)
{
  /// <summary>Valida a entrada. Nunca completa valores ausentes por conta própria.</summary>
  public static IReadOnlyList<string> Validate(ProjectDefinition? project, VersionEntry? source, IReadOnlyList<VersionEntry> targets, int? changeset)
  {
    var errors = new List<string>();
    if (project is null)
      errors.Add("Informe o projeto (--project).");
    if (source is null)
      errors.Add("Informe a versão de origem (--source).");
    if (targets.Count == 0)
      errors.Add("Informe ao menos um destino (--target ou --all-legacy).");
    if (changeset is null or <= 0)
      errors.Add("Informe um changeset numérico maior que zero (--changeset).");

    if (source is not null && targets.Any(t => t.Id.Equals(source.Id, StringComparison.OrdinalIgnoreCase)))
      errors.Add($"A origem '{source.Id}' não pode ser também destino.");

    var inactive = targets.Where(t => !t.Active).Select(t => t.Id).ToList();
    if (inactive.Count > 0)
      errors.Add($"Destinos desativados no catálogo: {string.Join(", ", inactive)}.");

    if (source is { Active: false })
      errors.Add($"A origem '{source.Id}' está desativada no catálogo.");

    return errors;
  }
}

public enum TargetReadiness
{
  Ready,
  AlreadyIntegrated,
  Blocked,
}

public sealed class MergeTargetPlan
{
  public required ProjectLocation Location { get; init; }

  public VersionEntry Target => Location.Version;

  public TargetReadiness Readiness { get; set; } = TargetReadiness.Ready;

  public MappingCheck? Mapping { get; set; }

  public bool IsLocalWorkspace { get; set; }

  public List<string> Blockers { get; } = [];

  public List<string> Warnings { get; } = [];

  public List<string> Notes { get; } = [];

  public List<string> PendingInScope { get; } = [];

  public List<string> PendingOutOfScope { get; } = [];

  public List<string> UnreconciledFiles { get; } = [];

  public List<string> ConflictsInScope { get; } = [];

  public List<string> ConflictsOutOfScope { get; } = [];

  /// <summary>Null quando não verificado.</summary>
  public bool? Outdated { get; set; }

  public int? PreviewItemCount { get; set; }

  /// <summary>Linhas de conflito informadas pelo tf merge /preview (não bloqueiam).</summary>
  public List<string> PredictedConflicts { get; } = [];

  public void Block(string reason)
  {
    Readiness = TargetReadiness.Blocked;
    Blockers.Add(reason);
  }
}

public sealed class MergePlan
{
  public required MergeRequest Request { get; init; }

  public required ProjectLocation SourceLocation { get; init; }

  public string? Collection { get; init; }

  public ChangesetScope? Scope { get; set; }

  public List<string> GlobalBlockers { get; } = [];

  public List<string> GlobalWarnings { get; } = [];

  public List<MergeTargetPlan> Targets { get; } = [];

  /// <summary>Rede/autenticação: nada mais deve ser executado.</summary>
  public bool EnvironmentFailure { get; set; }

  public bool HasBlockedTargets => Targets.Any(t => t.Readiness == TargetReadiness.Blocked);

  public IReadOnlyList<MergeTargetPlan> ReadyTargets => Targets.Where(t => t.Readiness == TargetReadiness.Ready).ToList();

  public bool CanExecute => GlobalBlockers.Count == 0 && !EnvironmentFailure && ReadyTargets.Count > 0;

  /// <summary>Código de saída de um dry-run: 0 tudo pronto/integrado, 3 se houver bloqueio.</summary>
  public int DryRunExitCode =>
    GlobalBlockers.Count > 0 || EnvironmentFailure || HasBlockedTargets ? ExitCodes.Precondition : ExitCodes.Success;
}

public enum MergeTargetState
{
  AppliedWithPendingChanges,
  NoApplicableChanges,
  AlreadyIntegrated,
  AppliedWithConflicts,
  Blocked,
  Failed,
  Cancelled,
  NotStarted,
  Indeterminate,
}

public sealed record MergeTargetResult(
  VersionEntry Target,
  MergeTargetState State,
  string Message,
  IReadOnlyList<string> NewPendingChanges,
  IReadOnlyList<string> Conflicts,
  TimeSpan? Duration)
{
  public static string Describe(MergeTargetState state) => state switch
  {
    MergeTargetState.AppliedWithPendingChanges => "Aplicado com pending changes",
    MergeTargetState.NoApplicableChanges => "Sem alterações aplicáveis",
    MergeTargetState.AlreadyIntegrated => "Já integrado",
    MergeTargetState.AppliedWithConflicts => "Aplicado com conflitos",
    MergeTargetState.Blocked => "Bloqueado",
    MergeTargetState.Failed => "Falhou",
    MergeTargetState.Cancelled => "Cancelado",
    MergeTargetState.NotStarted => "Não iniciado",
    MergeTargetState.Indeterminate => "Indeterminado — inspecionar",
    _ => state.ToString(),
  };
}

public sealed record MergeExecutionResult(IReadOnlyList<MergeTargetResult> Targets, bool EnvironmentFailure, bool CancellationRequested = false)
{
  /// <summary>Ctrl+C em qualquer momento da execução resulta em 130, mesmo entre destinos.</summary>
  public int ExitCode => CancellationRequested ? ExitCodes.Cancelled : MergeOutcome.ComputeExitCode(Targets);

  public string Status => MergeOutcome.StatusText(ExitCode);
}
