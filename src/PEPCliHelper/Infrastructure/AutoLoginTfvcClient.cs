using PEPCliHelper.Core.Tfvc;

namespace PEPCliHelper.Infrastructure;

/// <summary>
/// Decorador de <see cref="ITfvcClient"/>: quando o tf.exe responde sem autenticação (TF30063), pede o login automático
/// ao <see cref="TfvcAuthenticator"/> e repete a mesma chamada uma única vez.
/// Merge e get só são repetidos se a saída não citar nenhum item — ou seja, se nada foi processado.
/// </summary>
public sealed class AutoLoginTfvcClient : ITfvcClient
{
  private readonly ITfvcClient _inner;
  private readonly TfvcAuthenticator _authenticator;

  public AutoLoginTfvcClient(ITfvcClient inner, TfvcAuthenticator authenticator)
  {
    _inner = inner;
    _authenticator = authenticator;
  }

  public Task<WorkfoldQuery> GetWorkfoldAsync(string localPath, CancellationToken cancellationToken) =>
    WithLoginAsync(() => _inner.GetWorkfoldAsync(localPath, cancellationToken), q => q.Result, cancellationToken);

  public Task<TfResult> ListWorkspacesAsync(string collection, CancellationToken cancellationToken) =>
    WithLoginAsync(() => _inner.ListWorkspacesAsync(collection, cancellationToken), r => r, cancellationToken);

  public Task<ChangesetQuery> GetChangesetAsync(int changeset, string collection, CancellationToken cancellationToken) =>
    WithLoginAsync(() => _inner.GetChangesetAsync(changeset, collection, cancellationToken), q => q.Result, cancellationToken);

  public Task<ItemListQuery> GetPendingChangesAsync(string localPath, CancellationToken cancellationToken) =>
    WithLoginAsync(() => _inner.GetPendingChangesAsync(localPath, cancellationToken), q => q.Result, cancellationToken);

  public Task<ItemListQuery> GetConflictsAsync(string localPath, CancellationToken cancellationToken) =>
    WithLoginAsync(() => _inner.GetConflictsAsync(localPath, cancellationToken), q => q.Result, cancellationToken);

  public Task<CandidateQuery> GetMergeCandidatesAsync(string sourceServerPath, string targetServerPath, int changeset, string workingDirectory, CancellationToken cancellationToken) =>
    WithLoginAsync(() => _inner.GetMergeCandidatesAsync(sourceServerPath, targetServerPath, changeset, workingDirectory, cancellationToken), q => q.Result, cancellationToken);

  public Task<ItemListQuery> PreviewMergeAsync(string sourceServerPath, string targetServerPath, int changeset, string workingDirectory, CancellationToken cancellationToken) =>
    WithLoginAsync(() => _inner.PreviewMergeAsync(sourceServerPath, targetServerPath, changeset, workingDirectory, cancellationToken), q => q.Result, cancellationToken);

  public Task<TfResult> MergeChangesetAsync(string sourceServerPath, string targetServerPath, int changeset, string workingDirectory, Action<string>? onOutputLine, CancellationToken cancellationToken) =>
    WithLoginAsync(() => _inner.MergeChangesetAsync(sourceServerPath, targetServerPath, changeset, workingDirectory, onOutputLine, cancellationToken), r => r, cancellationToken,
      retryOnlyIfNothingProcessed: true);

  public Task<ItemListQuery> PreviewGetAsync(string localPath, CancellationToken cancellationToken) =>
    WithLoginAsync(() => _inner.PreviewGetAsync(localPath, cancellationToken), q => q.Result, cancellationToken);

  public Task<TfResult> GetLatestAsync(string localPath, Action<string>? onOutputLine, CancellationToken cancellationToken) =>
    WithLoginAsync(() => _inner.GetLatestAsync(localPath, onOutputLine, cancellationToken), r => r, cancellationToken,
      retryOnlyIfNothingProcessed: true);

  /// <summary>Linha que cita item do servidor ($/...) ou caminho local absoluto: o tf.exe já processou algo.</summary>
  internal static bool HasItemLines(string output) =>
    output
      .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
      .Any(line => line.Contains("$/", StringComparison.Ordinal) || IsRootedLocalPath(line));

  private static bool IsRootedLocalPath(string line) =>
    (line.Length >= 3 && char.IsAsciiLetter(line[0]) && line[1] == ':' && line[2] is '\\' or '/')
    || line.StartsWith(@"\\", StringComparison.Ordinal);

  private async Task<T> WithLoginAsync<T>(Func<Task<T>> call, Func<T, TfResult> resultOf, CancellationToken cancellationToken, bool retryOnlyIfNothingProcessed = false)
  {
    var first = await call();
    var result = resultOf(first);
    if (result.Status != TfStatus.AuthError)
      return first;
    if (retryOnlyIfNothingProcessed && HasItemLines(result.Output))
      return first;
    if (!await _authenticator.EnsureLoggedInAsync(cancellationToken))
      return first;

    return await call();
  }
}
