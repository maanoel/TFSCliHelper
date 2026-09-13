using PEPCliHelper.Core.Execution;

namespace PEPCliHelper.Core.Tfvc;

/// <summary>Implementação de <see cref="ITfvcClient"/> via tf.exe com argumentos estruturados.</summary>
public sealed class TfExeClient : ITfvcClient
{
  private readonly ICommandExecutor _executor;
  private readonly Func<CancellationToken, Task<string>> _resolveTfPath;
  private readonly Func<string, bool> _directoryExists;

  public TfExeClient(ICommandExecutor executor, Func<CancellationToken, Task<string>> resolveTfPath, Func<string, bool>? directoryExists = null)
  {
    _executor = executor;
    _resolveTfPath = resolveTfPath;
    _directoryExists = directoryExists ?? Directory.Exists;
  }

  public async Task<WorkfoldQuery> GetWorkfoldAsync(string localPath, CancellationToken cancellationToken)
  {
    var result = await RunAsync(["workfold", localPath], null, null, cancellationToken);
    return new WorkfoldQuery(result, result.IsSuccess ? WorkfoldParser.Parse(result.Output) : null);
  }

  public Task<TfResult> ListWorkspacesAsync(string collection, CancellationToken cancellationToken) =>
    RunAsync(["workspaces", $"/collection:{collection}"], null, null, cancellationToken);

  public async Task<ChangesetQuery> GetChangesetAsync(int changeset, string collection, CancellationToken cancellationToken)
  {
    var result = await RunAsync(
      ["changeset", changeset.ToString(System.Globalization.CultureInfo.InvariantCulture), $"/collection:{collection}", "/noprompt"],
      null, null, cancellationToken);

    if (!result.IsSuccess)
      return new ChangesetQuery(result, null);

    var lines = TfOutputParser.Lines(result.Output);
    var header = lines
      .TakeWhile(l => !IsItemLine(l))
      .Select(l => l.Trim())
      .Where(l => l.Length > 0)
      .Take(12)
      .ToList();

    // Itens: linhas cujo conteúdo, após o tipo de alteração, começa com $/ (comentário com $/ no meio do texto é ignorado).
    var itemLines = string.Join('\n', lines.Where(IsItemLine));
    return new ChangesetQuery(result, new ChangesetInfo(changeset, header, TfOutputParser.ExtractServerPaths(itemLines)));

    static bool IsItemLine(string line) =>
      System.Text.RegularExpressions.Regex.IsMatch(line, @"^\s*(\p{L}+(,\s*\p{L}+)*\s+)?\$/");
  }

  public async Task<ItemListQuery> GetPendingChangesAsync(string localPath, CancellationToken cancellationToken)
  {
    var result = await RunAsync(["status", localPath, "/recursive", "/format:detailed"], localPath, null, cancellationToken);
    var items = TfOutputParser.LinesContaining(result.Output, localPath);
    var serverItems = TfOutputParser.ExtractServerPaths(result.Output);
    return new ItemListQuery(result, items, HasUnmatchedItems: result.IsSuccess && items.Count == 0 && serverItems.Count > 0);
  }

  public async Task<ItemListQuery> GetConflictsAsync(string localPath, CancellationToken cancellationToken)
  {
    var result = await RunAsync(["resolve", localPath, "/recursive", "/preview", "/noprompt"], localPath, null, cancellationToken);
    return new ItemListQuery(result, ParseConflicts(result, localPath));
  }

  /// <summary>
  /// Evidência real (tf.exe 17.14 pt-BR, 2026-09-13):
  /// sem conflitos ⇒ exit 0 "Não há conflitos para resolver.";
  /// com conflitos ⇒ exit 1, uma linha por item com caminho RELATIVO à pasta consultada:
  /// "RM.Pep.ExamRequest.Server\Infra\SolicitacaoExameRepositorio.cs: A origem e o destino têm alterações."
  /// Cada linha é devolvida com o caminho absoluto no início, para casar com o escopo do merge.
  /// </summary>
  internal static IReadOnlyList<string> ParseConflicts(TfResult result, string localPath)
  {
    if (result.Status != TfStatus.PartialSuccess)
      return [];

    return TfOutputParser.Lines(result.Output)
      .Select(l => l.Trim())
      .Where(l => l.Length > 0)
      .Select(line =>
      {
        var separator = line.IndexOf(": ", StringComparison.Ordinal);
        if (separator <= 0 || line.StartsWith("$/", StringComparison.Ordinal))
          return line;

        var item = line[..separator];
        return Path.IsPathRooted(item) ? line : $"{Path.Combine(localPath, item)}{line[separator..]}";
      })
      .ToList();
  }

  public async Task<CandidateQuery> GetMergeCandidatesAsync(string sourceServerPath, string targetServerPath, int changeset, string workingDirectory, CancellationToken cancellationToken)
  {
    // Evidência real (tf.exe 17.14, 2026-09-13): "Opção /version não pode ser combinada com opção /candidate".
    // Lista todos os candidatos origem → destino e o chamador verifica se o changeset está entre eles.
    var result = await RunAsync(
      ["merge", "/candidate", "/recursive", sourceServerPath, targetServerPath],
      workingDirectory, null, cancellationToken);
    return new CandidateQuery(result, result.IsSuccess ? TfOutputParser.ParseLeadingNumbers(result.Output) : new HashSet<int>());
  }

  public async Task<ItemListQuery> PreviewMergeAsync(string sourceServerPath, string targetServerPath, int changeset, string workingDirectory, CancellationToken cancellationToken)
  {
    var result = await RunAsync(MergeArguments(sourceServerPath, targetServerPath, changeset, preview: true), workingDirectory, null, cancellationToken);
    return new ItemListQuery(result, TfOutputParser.LinesContaining(result.Output, "$/"));
  }

  public Task<TfResult> MergeChangesetAsync(string sourceServerPath, string targetServerPath, int changeset, string workingDirectory, Action<string>? onOutputLine, CancellationToken cancellationToken) =>
    RunAsync(MergeArguments(sourceServerPath, targetServerPath, changeset, preview: false), workingDirectory, onOutputLine, cancellationToken);

  public async Task<ItemListQuery> PreviewGetAsync(string localPath, CancellationToken cancellationToken)
  {
    var result = await RunAsync(["get", localPath, "/recursive", "/preview", "/noprompt"], localPath, null, cancellationToken);
    return new ItemListQuery(result, TfOutputParser.LinesContaining(result.Output, localPath));
  }

  public Task<TfResult> GetLatestAsync(string localPath, Action<string>? onOutputLine, CancellationToken cancellationToken) =>
    RunAsync(["get", localPath, "/recursive", "/noprompt"], localPath, onOutputLine, cancellationToken);

  /// <summary>Um único changeset: C&lt;id&gt;~C&lt;id&gt;. Nunca intervalo acumulado.</summary>
  internal static string VersionSpec(int changeset) => $"/version:C{changeset}~C{changeset}";

  internal static IReadOnlyList<string> MergeArguments(string source, string target, int changeset, bool preview)
  {
    var arguments = new List<string> { "merge" };
    if (preview)
      arguments.Add("/preview");
    arguments.AddRange(["/recursive", "/noimplicitbaseless", VersionSpec(changeset), source, target, "/noprompt"]);
    return arguments;
  }

  private async Task<TfResult> RunAsync(IReadOnlyList<string> arguments, string? workingDirectory, Action<string>? onOutputLine, CancellationToken cancellationToken)
  {
    var tfPath = await _resolveTfPath(cancellationToken);
    if (workingDirectory is not null && !_directoryExists(workingDirectory))
    {
      // Sem a pasta, o tf.exe resolveria o workspace a partir de outro diretório: nunca executar assim.
      throw new Common.PreconditionException(
        $"A pasta de trabalho '{workingDirectory}' não existe; o comando TFVC não foi executado.",
        "Confirme o caminho local da versão com 'pep env validate'.", workingDirectory);
    }

    var command = new Command(tfPath, arguments, workingDirectory);
    var result = await _executor.ExecuteAsync(command, onOutputLine, cancellationToken);
    return TfErrorClassifier.Classify(result, command.Display);
  }
}
