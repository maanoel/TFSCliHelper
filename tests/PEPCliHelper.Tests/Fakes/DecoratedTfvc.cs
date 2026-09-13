using PEPCliHelper.Core.Tfvc;

namespace PEPCliHelper.Tests.Fakes;

/// <summary>Base que delega ao FakeTfvcClient, permitindo alterar uma operação por cenário.</summary>
public abstract class DecoratedTfvc(FakeTfvcClient inner) : ITfvcClient
{
  protected FakeTfvcClient Inner { get; } = inner;

  public virtual Task<WorkfoldQuery> GetWorkfoldAsync(string localPath, CancellationToken cancellationToken) => Inner.GetWorkfoldAsync(localPath, cancellationToken);

  public virtual Task<TfResult> ListWorkspacesAsync(string collection, CancellationToken cancellationToken) => Inner.ListWorkspacesAsync(collection, cancellationToken);

  public virtual Task<ChangesetQuery> GetChangesetAsync(int changeset, string collection, CancellationToken cancellationToken) => Inner.GetChangesetAsync(changeset, collection, cancellationToken);

  public virtual Task<ItemListQuery> GetPendingChangesAsync(string localPath, CancellationToken cancellationToken) => Inner.GetPendingChangesAsync(localPath, cancellationToken);

  public virtual Task<ItemListQuery> GetConflictsAsync(string localPath, CancellationToken cancellationToken) => Inner.GetConflictsAsync(localPath, cancellationToken);

  public virtual Task<CandidateQuery> GetMergeCandidatesAsync(string sourceServerPath, string targetServerPath, int changeset, string workingDirectory, CancellationToken cancellationToken) =>
    Inner.GetMergeCandidatesAsync(sourceServerPath, targetServerPath, changeset, workingDirectory, cancellationToken);

  public virtual Task<ItemListQuery> PreviewMergeAsync(string sourceServerPath, string targetServerPath, int changeset, string workingDirectory, CancellationToken cancellationToken) =>
    Inner.PreviewMergeAsync(sourceServerPath, targetServerPath, changeset, workingDirectory, cancellationToken);

  public virtual Task<TfResult> MergeChangesetAsync(string sourceServerPath, string targetServerPath, int changeset, string workingDirectory, Action<string>? onOutputLine, CancellationToken cancellationToken) =>
    Inner.MergeChangesetAsync(sourceServerPath, targetServerPath, changeset, workingDirectory, onOutputLine, cancellationToken);

  public virtual Task<ItemListQuery> PreviewGetAsync(string localPath, CancellationToken cancellationToken) => Inner.PreviewGetAsync(localPath, cancellationToken);

  public virtual Task<TfResult> GetLatestAsync(string localPath, Action<string>? onOutputLine, CancellationToken cancellationToken) => Inner.GetLatestAsync(localPath, onOutputLine, cancellationToken);
}

/// <summary>tf status cita itens do servidor, mas nenhum com o caminho local esperado.</summary>
public sealed class UnmatchedPendingTfvc(FakeTfvcClient inner) : DecoratedTfvc(inner)
{
  public override async Task<ItemListQuery> GetPendingChangesAsync(string localPath, CancellationToken cancellationToken)
  {
    var query = await Inner.GetPendingChangesAsync(localPath, cancellationToken);
    return query with { Items = [], HasUnmatchedItems = true };
  }
}

/// <summary>tf workfold assíncrono e lento: força concorrência real e mede o máximo de destinos simultâneos.</summary>
public sealed class SlowWorkfoldTfvc(FakeTfvcClient inner) : DecoratedTfvc(inner)
{
  private int _inFlight;
  private int _maxInFlight;

  public int MaxInFlight => Volatile.Read(ref _maxInFlight);

  public override async Task<WorkfoldQuery> GetWorkfoldAsync(string localPath, CancellationToken cancellationToken)
  {
    var current = Interlocked.Increment(ref _inFlight);
    int observed;
    while (current > (observed = Volatile.Read(ref _maxInFlight)) && Interlocked.CompareExchange(ref _maxInFlight, current, observed) != observed)
    {
    }

    try
    {
      await Task.Delay(20, cancellationToken);
      return await Inner.GetWorkfoldAsync(localPath, cancellationToken);
    }
    finally
    {
      Interlocked.Decrement(ref _inFlight);
    }
  }
}

/// <summary>Relógio controlado pelo teste.</summary>
public sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
{
  private DateTimeOffset _now = now;

  public override DateTimeOffset GetUtcNow() => _now;

  public void Advance(TimeSpan delta) => _now += delta;
}

/// <summary>Simula Ctrl+C logo após o primeiro merge concluir.</summary>
public sealed class CancelAfterFirstMerge(FakeTfvcClient inner, CancellationTokenSource cancellation) : DecoratedTfvc(inner)
{
  public override async Task<TfResult> MergeChangesetAsync(string sourceServerPath, string targetServerPath, int changeset, string workingDirectory, Action<string>? onOutputLine, CancellationToken cancellationToken)
  {
    var result = await Inner.MergeChangesetAsync(sourceServerPath, targetServerPath, changeset, workingDirectory, onOutputLine, cancellationToken);
    await cancellation.CancelAsync();
    return result;
  }
}
