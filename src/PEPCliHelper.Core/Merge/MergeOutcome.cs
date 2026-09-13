using System.Text.RegularExpressions;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.Tfvc;

namespace PEPCliHelper.Core.Merge;

/// <summary>Regras puras de classificação, interrupção e código de saída do merge (spec 007).</summary>
public static partial class MergeOutcome
{
  [GeneratedRegex(@"\bTF\d{5,6}\b")]
  private static partial Regex TfErrorCodeRegex();

  /// <summary>Classifica um destino após o tf merge, comparando pendências antes/depois.</summary>
  /// <param name="previewHadItems">O preview listou itens: exit 0 sem novas pendências não é confirmável.</param>
  /// <remarks>
  /// Evidência real (tf.exe 17.14 pt-BR, 2026-09-13): merge de vários arquivos retorna exit 1 com
  /// "Conflito resolvido automaticamente: ... como AutoMerge" sem conflito pendente. Exit 1 sem conflitos,
  /// com novas pendências e sem código de erro TF é aplicado; qualquer código TF mantém indeterminado.
  /// </remarks>
  public static MergeTargetState Classify(TfResult merge, int newPendingCount, int conflictCount, bool previewHadItems = false)
  {
    if (merge.Status == TfStatus.Cancelled)
      return MergeTargetState.Cancelled;

    if (conflictCount > 0)
      return MergeTargetState.AppliedWithConflicts;

    return merge.Status switch
    {
      TfStatus.Success when newPendingCount > 0 => MergeTargetState.AppliedWithPendingChanges,
      TfStatus.Success => previewHadItems ? MergeTargetState.Indeterminate : MergeTargetState.NoApplicableChanges,
      TfStatus.PartialSuccess when newPendingCount > 0 && !HasTfErrorCode(merge.Output) => MergeTargetState.AppliedWithPendingChanges,
      TfStatus.PartialSuccess => MergeTargetState.Indeterminate,
      _ => newPendingCount > 0 ? MergeTargetState.Indeterminate : MergeTargetState.Failed,
    };
  }

  /// <summary>Saída contém código de erro do TFVC (ex.: TF14087).</summary>
  public static bool HasTfErrorCode(string output) => TfErrorCodeRegex().IsMatch(output);

  /// <summary>
  /// Após este estado, os próximos destinos devem ficar "não iniciados"? Padrão (decisão 2026-09-13): continuar
  /// nos demais destinos. Rede/autenticação, cancelamento e indeterminado sempre interrompem.
  /// </summary>
  public static bool ShouldStop(MergeTargetState state, bool stopOnFailure, bool environmentFailure)
  {
    if (environmentFailure)
      return true;

    return state switch
    {
      MergeTargetState.Indeterminate or MergeTargetState.Cancelled => true,
      MergeTargetState.Failed or MergeTargetState.AppliedWithConflicts or MergeTargetState.Blocked => stopOnFailure,
      _ => false,
    };
  }

  public static int ComputeExitCode(IReadOnlyList<MergeTargetResult> results)
  {
    if (results.Count == 0)
      return ExitCodes.Precondition;

    if (results.Any(r => r.State == MergeTargetState.Cancelled))
      return ExitCodes.Cancelled;

    static bool IsOk(MergeTargetState s) =>
      s is MergeTargetState.AppliedWithPendingChanges or MergeTargetState.NoApplicableChanges or MergeTargetState.AlreadyIntegrated;

    if (results.All(r => IsOk(r.State)))
      return ExitCodes.Success;

    var changedSomething = results.Any(r => r.State is MergeTargetState.AppliedWithPendingChanges
      or MergeTargetState.AppliedWithConflicts or MergeTargetState.Indeterminate);

    if (changedSomething || results.Any(r => r.State is MergeTargetState.AppliedWithConflicts or MergeTargetState.Indeterminate))
      return ExitCodes.ConflictOrPartial;

    if (results.Any(r => r.State == MergeTargetState.Failed))
      return ExitCodes.OperationFailed;

    return ExitCodes.Precondition;
  }

  public static string StatusText(int exitCode) => exitCode switch
  {
    ExitCodes.Success => "concluido",
    ExitCodes.ConflictOrPartial => "parcial",
    ExitCodes.OperationFailed => "falhou",
    ExitCodes.Precondition => "bloqueado",
    ExitCodes.Cancelled => "cancelado",
    _ => "erro",
  };

  /// <summary>Pendências novas: presentes depois e ausentes antes (não atribui pré-existentes ao CLI).</summary>
  public static IReadOnlyList<string> NewItems(IReadOnlyList<string> before, IReadOnlyList<string> after)
  {
    var previous = new HashSet<string>(before.Select(Normalize), StringComparer.OrdinalIgnoreCase);
    return after.Where(item => !previous.Contains(Normalize(item))).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
  }

  /// <summary>Linha pertence ao escopo se contém algum caminho alvo (local ou servidor).</summary>
  public static bool IsInScope(string line, IReadOnlyCollection<string> scopePaths) =>
    scopePaths.Any(path => line.Contains(path, StringComparison.OrdinalIgnoreCase));

  private static string Normalize(string line) => string.Join(' ', line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
