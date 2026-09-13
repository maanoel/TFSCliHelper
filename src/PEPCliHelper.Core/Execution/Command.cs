namespace PEPCliHelper.Core.Execution;

/// <summary>Um processo a executar, com argumentos estruturados (nunca uma linha de shell).</summary>
public sealed record Command(string FileName, IReadOnlyList<string> Arguments, string? WorkingDirectory = null)
{
  /// <summary>Representação apenas para exibição e log.</summary>
  public string Display =>
    string.Join(' ', new[] { Quote(Path.GetFileName(FileName)) }.Concat(Arguments.Select(Quote)));

  private static string Quote(string value) =>
    value.Length == 0 || value.Any(char.IsWhiteSpace) ? $"\"{value}\"" : value;
}

public sealed record CommandResult(int ExitCode, string StdOut, string StdErr, TimeSpan Duration, bool Cancelled = false)
{
  public string Output => string.IsNullOrEmpty(StdErr) ? StdOut : $"{StdOut}{Environment.NewLine}{StdErr}";
}

/// <summary>Porta de execução de processos (padrão Executor do CLI).</summary>
public interface ICommandExecutor
{
  /// <exception cref="Common.ToolNotFoundException">O executável não pôde ser iniciado.</exception>
  Task<CommandResult> ExecuteAsync(Command command, Action<string>? onOutputLine = null, CancellationToken cancellationToken = default);
}
