using PEPCliHelper.Core.Tfvc;

namespace PEPCliHelper.Tests.Fakes;

/// <summary>TFVC falso configurável por pasta/caminho. Registra cada chamada para verificar que nada foi alterado.</summary>
public sealed class FakeTfvcClient : ITfvcClient
{
  public List<string> Calls { get; } = [];

  public Dictionary<string, WorkfoldInfo> Workfolds { get; } = new(StringComparer.OrdinalIgnoreCase);

  public TfStatus WorkfoldStatus { get; set; } = TfStatus.Success;

  public ChangesetInfo? Changeset { get; set; }

  public TfStatus ChangesetStatus { get; set; } = TfStatus.Success;

  /// <summary>Changesets candidatos por caminho de servidor de destino.</summary>
  public Dictionary<string, HashSet<int>> Candidates { get; } = new(StringComparer.OrdinalIgnoreCase);

  public Dictionary<string, TfStatus> CandidateStatus { get; } = new(StringComparer.OrdinalIgnoreCase);

  /// <summary>Pending changes (antes do merge) por pasta local.</summary>
  public Dictionary<string, List<string>> Pending { get; } = new(StringComparer.OrdinalIgnoreCase);

  /// <summary>Pendências adicionadas quando o merge roda na pasta local.</summary>
  public Dictionary<string, List<string>> PendingAddedByMerge { get; } = new(StringComparer.OrdinalIgnoreCase);

  public Dictionary<string, List<string>> Conflicts { get; } = new(StringComparer.OrdinalIgnoreCase);

  public TfStatus ConflictStatus { get; set; } = TfStatus.Success;

  public Dictionary<string, List<string>> ConflictsAddedByMerge { get; } = new(StringComparer.OrdinalIgnoreCase);

  public Dictionary<string, TfStatus> MergeStatus { get; } = new(StringComparer.OrdinalIgnoreCase);

  /// <summary>Status do tf merge /preview por caminho de servidor de destino.</summary>
  public Dictionary<string, TfStatus> PreviewStatus { get; } = new(StringComparer.OrdinalIgnoreCase);

  public Dictionary<string, TfStatus> GetStatus { get; } = new(StringComparer.OrdinalIgnoreCase);

  public HashSet<string> Outdated { get; } = new(StringComparer.OrdinalIgnoreCase);

  private volatile bool _requiresLogin;

  /// <summary>Enquanto verdadeiro, toda operação responde TF30063 (sem credencial em cache), até <see cref="Login"/>.</summary>
  public bool RequiresLogin
  {
    get => _requiresLogin;
    set => _requiresLogin = value;
  }

  /// <summary>Simula o login concluído no tf.exe (credencial em cache).</summary>
  public void Login() => RequiresLogin = false;

  public const string AuthErrorOutput = @"TF30063: você não está autorizado a acessar totvstfs.visualstudio.com\totvstfs.";

  public int MergeCount => Calls.Count(c => c.StartsWith("merge:", StringComparison.Ordinal));

  public int GetCount => Calls.Count(c => c.StartsWith("get:", StringComparison.Ordinal));

  public Task<WorkfoldQuery> GetWorkfoldAsync(string localPath, CancellationToken cancellationToken)
  {
    Record($"workfold:{localPath}");
    if (RequiresLogin)
      return Task.FromResult(new WorkfoldQuery(Unauthorized(), null));
    if (WorkfoldStatus != TfStatus.Success)
      return Task.FromResult(new WorkfoldQuery(Result(WorkfoldStatus), null));

    var info = Workfolds
      .Where(w => localPath.StartsWith(w.Key, StringComparison.OrdinalIgnoreCase))
      .OrderByDescending(w => w.Key.Length)
      .Select(w => w.Value)
      .FirstOrDefault();

    return Task.FromResult(info is null
      ? new WorkfoldQuery(Result(TfStatus.Failed, 100, "Unable to determine the workspace."), null)
      : new WorkfoldQuery(Result(TfStatus.Success), info));
  }

  public Task<TfResult> ListWorkspacesAsync(string collection, CancellationToken cancellationToken)
  {
    Record("workspaces");
    if (RequiresLogin)
      return Task.FromResult(Unauthorized());
    return Task.FromResult(Result(WorkfoldStatus));
  }

  public Task<ChangesetQuery> GetChangesetAsync(int changeset, string collection, CancellationToken cancellationToken)
  {
    Record($"changeset:{changeset}");
    if (RequiresLogin)
      return Task.FromResult(new ChangesetQuery(Unauthorized(), null));
    return Task.FromResult(ChangesetStatus == TfStatus.Success && Changeset is not null
      ? new ChangesetQuery(Result(TfStatus.Success), Changeset)
      : new ChangesetQuery(Result(ChangesetStatus == TfStatus.Success ? TfStatus.Failed : ChangesetStatus), null));
  }

  public Task<ItemListQuery> GetPendingChangesAsync(string localPath, CancellationToken cancellationToken)
  {
    Record($"status:{localPath}");
    if (RequiresLogin)
      return Task.FromResult(new ItemListQuery(Unauthorized(), []));
    return Task.FromResult(new ItemListQuery(Result(TfStatus.Success), Pending.TryGetValue(localPath, out var list) ? list.ToList() : []));
  }

  public Task<ItemListQuery> GetConflictsAsync(string localPath, CancellationToken cancellationToken)
  {
    Record($"resolve-preview:{localPath}");
    if (RequiresLogin)
      return Task.FromResult(new ItemListQuery(Unauthorized(), []));
    if (ConflictStatus != TfStatus.Success)
      return Task.FromResult(new ItemListQuery(Result(ConflictStatus), []));
    return Task.FromResult(new ItemListQuery(Result(TfStatus.Success), Conflicts.TryGetValue(localPath, out var list) ? list.ToList() : []));
  }

  public Task<CandidateQuery> GetMergeCandidatesAsync(string sourceServerPath, string targetServerPath, int changeset, string workingDirectory, CancellationToken cancellationToken)
  {
    Record($"candidate:{targetServerPath}");
    if (RequiresLogin)
      return Task.FromResult(new CandidateQuery(Unauthorized(), new HashSet<int>()));
    if (CandidateStatus.TryGetValue(targetServerPath, out var status) && status != TfStatus.Success)
      return Task.FromResult(new CandidateQuery(Result(status, 100, "TF14087: sem relação de branch."), new HashSet<int>()));

    return Task.FromResult(new CandidateQuery(Result(TfStatus.Success),
      Candidates.TryGetValue(targetServerPath, out var set) ? set : new HashSet<int>()));
  }

  public Task<ItemListQuery> PreviewMergeAsync(string sourceServerPath, string targetServerPath, int changeset, string workingDirectory, CancellationToken cancellationToken)
  {
    Record($"merge-preview:{targetServerPath}");
    if (RequiresLogin)
      return Task.FromResult(new ItemListQuery(Unauthorized(), []));
    var status = PreviewStatus.TryGetValue(targetServerPath, out var configured) ? configured : TfStatus.Success;
    var line = status == TfStatus.PartialSuccess
      ? $"Conflito (mesclar, editar): {sourceServerPath}/a.cs;C1~C1 -> {targetServerPath}/a.cs;C0"
      : $"merge, edit: {sourceServerPath}/a.cs -> {targetServerPath}/a.cs";
    return Task.FromResult(new ItemListQuery(Result(status, status == TfStatus.PartialSuccess ? 1 : 100), [line]));
  }

  public Task<TfResult> MergeChangesetAsync(string sourceServerPath, string targetServerPath, int changeset, string workingDirectory, Action<string>? onOutputLine, CancellationToken cancellationToken)
  {
    Record($"merge:{workingDirectory}");
    if (RequiresLogin)
      return Task.FromResult(Unauthorized());
    if (cancellationToken.IsCancellationRequested)
      return Task.FromResult(Result(TfStatus.Cancelled, -1));

    var status = MergeStatus.TryGetValue(workingDirectory, out var configured) ? configured : TfStatus.Success;
    if (status is TfStatus.Success or TfStatus.PartialSuccess)
    {
      if (PendingAddedByMerge.TryGetValue(workingDirectory, out var added))
        Pending[workingDirectory] = (Pending.TryGetValue(workingDirectory, out var existing) ? existing : []).Concat(added).ToList();
      if (ConflictsAddedByMerge.TryGetValue(workingDirectory, out var conflicts))
        Conflicts[workingDirectory] = (Conflicts.TryGetValue(workingDirectory, out var existing) ? existing : []).Concat(conflicts).ToList();
    }

    return Task.FromResult(Result(status, status switch { TfStatus.Success => 0, TfStatus.PartialSuccess => 1, _ => 100 },
      MergeOutput.TryGetValue(workingDirectory, out var output) ? output : ""));
  }

  public Task<ItemListQuery> PreviewGetAsync(string localPath, CancellationToken cancellationToken)
  {
    Record($"get-preview:{localPath}");
    if (RequiresLogin)
      return Task.FromResult(new ItemListQuery(Unauthorized(), []));
    return Task.FromResult(new ItemListQuery(Result(TfStatus.Success), Outdated.Contains(localPath) ? [$"{localPath}\\pasta:"] : []));
  }

  public Task<TfResult> GetLatestAsync(string localPath, Action<string>? onOutputLine, CancellationToken cancellationToken)
  {
    Record($"get:{localPath}");
    if (RequiresLogin)
      return Task.FromResult(Unauthorized());
    var status = GetStatus.TryGetValue(localPath, out var configured) ? configured : TfStatus.Success;
    return Task.FromResult(Result(status, status switch { TfStatus.Success => 0, TfStatus.PartialSuccess => 1, _ => 100 }));
  }

  /// <summary>Saída do tf merge por pasta local (ex.: linhas de AutoMerge ou códigos TF).</summary>
  public Dictionary<string, string> MergeOutput { get; } = new(StringComparer.OrdinalIgnoreCase);

  private readonly object _callsGate = new();

  private void Record(string call)
  {
    lock (_callsGate)
      Calls.Add(call);
  }

  private static TfResult Unauthorized() => Result(TfStatus.AuthError, 100, AuthErrorOutput);

  private static TfResult Result(TfStatus status, int exitCode = 0, string output = "") =>
    new(status, status == TfStatus.Success ? 0 : exitCode == 0 ? 100 : exitCode, output, TimeSpan.FromMilliseconds(5), "tf fake");
}
