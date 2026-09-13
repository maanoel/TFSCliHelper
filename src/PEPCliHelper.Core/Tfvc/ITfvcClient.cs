namespace PEPCliHelper.Core.Tfvc;

public sealed record WorkfoldQuery(TfResult Result, WorkfoldInfo? Info);

public sealed record ChangesetInfo(int Id, IReadOnlyList<string> Header, IReadOnlyList<string> Items);

public sealed record ChangesetQuery(TfResult Result, ChangesetInfo? Changeset);

/// <summary>
/// Linhas da saída que referenciam itens (pending changes, conflitos, preview).
/// <see cref="HasUnmatchedItems"/>: a saída cita itens do servidor, mas nenhum com o caminho local esperado
/// (subst, junction ou encoding) — o estado local deve ser tratado como desconhecido.
/// </summary>
public sealed record ItemListQuery(TfResult Result, IReadOnlyList<string> Items, bool HasUnmatchedItems = false)
{
  public bool IsReliable => Result.IsSuccess && !HasUnmatchedItems;
}

public sealed record CandidateQuery(TfResult Result, IReadOnlySet<int> Changesets);

/// <summary>
/// Operações TFVC permitidas ao CLI (spec 005).
/// Por projeto, esta interface NÃO possui check-in, undo, shelve, baseless, /force, /overwrite,
/// resolução automática de conflitos nem criação/alteração de workspaces e mapeamentos.
/// </summary>
public interface ITfvcClient
{
  Task<WorkfoldQuery> GetWorkfoldAsync(string localPath, CancellationToken cancellationToken);

  Task<TfResult> ListWorkspacesAsync(string collection, CancellationToken cancellationToken);

  Task<ChangesetQuery> GetChangesetAsync(int changeset, string collection, CancellationToken cancellationToken);

  /// <summary>Linhas de pending changes que contêm a pasta consultada.</summary>
  Task<ItemListQuery> GetPendingChangesAsync(string localPath, CancellationToken cancellationToken);

  /// <summary>Conflitos existentes, sem resolver (tf resolve /preview).</summary>
  Task<ItemListQuery> GetConflictsAsync(string localPath, CancellationToken cancellationToken);

  /// <summary>Changesets ainda não integrados de origem para destino, restritos a um changeset.</summary>
  Task<CandidateQuery> GetMergeCandidatesAsync(string sourceServerPath, string targetServerPath, int changeset, string workingDirectory, CancellationToken cancellationToken);

  /// <summary>Preview real do merge de um único changeset; não altera nada.</summary>
  Task<ItemListQuery> PreviewMergeAsync(string sourceServerPath, string targetServerPath, int changeset, string workingDirectory, CancellationToken cancellationToken);

  /// <summary>Merge de um único changeset (C&lt;id&gt;~C&lt;id&gt;), sem baseless. Resultado fica em Pending Changes.</summary>
  Task<TfResult> MergeChangesetAsync(string sourceServerPath, string targetServerPath, int changeset, string workingDirectory, Action<string>? onOutputLine, CancellationToken cancellationToken);

  /// <summary>Preview de get; não altera nada.</summary>
  Task<ItemListQuery> PreviewGetAsync(string localPath, CancellationToken cancellationToken);

  /// <summary>Get da versão mais recente, sem /force e sem /overwrite.</summary>
  Task<TfResult> GetLatestAsync(string localPath, Action<string>? onOutputLine, CancellationToken cancellationToken);
}
