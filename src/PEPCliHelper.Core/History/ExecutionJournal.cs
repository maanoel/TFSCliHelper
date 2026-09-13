using System.Text.Json;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.FileSystem;

namespace PEPCliHelper.Core.History;

public interface IExecutionJournal
{
  string Directory { get; }

  ExecutionSession Begin(string command, IReadOnlyList<string> arguments);

  IReadOnlyList<ExecutionRecord> List(int limit);

  /// <summary>Busca por id exato ou prefixo único.</summary>
  ExecutionRecord? Find(string id);
}

/// <summary>Histórico em arquivos JSON, um por execução, em %LOCALAPPDATA%\PepCli\history.</summary>
public sealed class JsonExecutionJournal : IExecutionJournal
{
  internal const int MaxOutputLength = 4000;

  internal static readonly JsonSerializerOptions Options = new()
  {
    WriteIndented = true,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
  };

  private readonly IFileSystem _fileSystem;
  private readonly TimeProvider _time;

  public JsonExecutionJournal(IFileSystem fileSystem, string directory, TimeProvider time)
  {
    _fileSystem = fileSystem;
    Directory = directory;
    _time = time;
  }

  public string Directory { get; }

  public ExecutionSession Begin(string command, IReadOnlyList<string> arguments)
  {
    var now = _time.GetLocalNow();
    var record = new ExecutionRecord
    {
      Id = $"{now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..4]}",
      Command = command,
      Arguments = arguments.ToList(),
      StartedAt = now,
    };
    return new ExecutionSession(record, this, _time);
  }

  public IReadOnlyList<ExecutionRecord> List(int limit) =>
    _fileSystem.GetFiles(Directory, "*.json")
      .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
      .Take(limit)
      .Select(TryRead)
      .OfType<ExecutionRecord>()
      .ToList();

  public ExecutionRecord? Find(string id)
  {
    if (string.IsNullOrWhiteSpace(id))
      return null;

    var matches = _fileSystem.GetFiles(Directory, "*.json")
      .Where(f => Path.GetFileNameWithoutExtension(f).StartsWith(id.Trim(), StringComparison.OrdinalIgnoreCase))
      .ToList();

    var exact = matches.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(id.Trim(), StringComparison.OrdinalIgnoreCase));
    return exact is not null ? TryRead(exact) : matches.Count == 1 ? TryRead(matches[0]) : null;
  }

  internal void Save(ExecutionRecord record)
  {
    _fileSystem.EnsureDirectory(Directory);
    _fileSystem.WriteAllTextAtomic(Path.Combine(Directory, record.Id + ".json"), JsonSerializer.Serialize(record, Options));
  }

  private ExecutionRecord? TryRead(string file)
  {
    try
    {
      return JsonSerializer.Deserialize<ExecutionRecord>(_fileSystem.ReadAllText(file), Options);
    }
    catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
    {
      return null;
    }
  }
}

/// <summary>Uma execução em andamento. Grava a cada etapa para não perder registro em falhas.</summary>
public sealed class ExecutionSession : IOperationLog
{
  private readonly JsonExecutionJournal? _journal;
  private readonly TimeProvider _time;
  private readonly object _gate = new();

  internal ExecutionSession(ExecutionRecord record, JsonExecutionJournal? journal, TimeProvider time)
  {
    Record = record;
    _journal = journal;
    _time = time;
  }

  public ExecutionRecord Record { get; }

  public string Id => Record.Id;

  /// <summary>Erro ao gravar o histórico; operação continua e o aviso é exibido.</summary>
  public string? PersistenceWarning { get; private set; }

  public static ExecutionSession Detached(string command, IReadOnlyList<string> arguments, TimeProvider time) =>
    new(new ExecutionRecord { Id = "sem-historico", Command = command, Arguments = arguments.ToList(), StartedAt = time.GetLocalNow() }, null, time);

  public void Describe(string? project = null, string? source = null, int? changeset = null, IEnumerable<string>? versions = null)
  {
    lock (_gate)
    {
      Record.Project = project ?? Record.Project;
      Record.Source = source ?? Record.Source;
      Record.Changeset = changeset ?? Record.Changeset;
      if (versions is not null)
        Record.Versions = versions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    Persist();
  }

  public void Step(string target, string step)
  {
    lock (_gate)
      Record.Steps.Add(new ExecutionStep { At = _time.GetLocalNow(), Target = target, Step = step });
    Persist();
  }

  public void ToolResult(string target, string step, int exitCode, string result, TimeSpan duration, string output)
  {
    lock (_gate)
    {
      Record.Steps.Add(new ExecutionStep
      {
        At = _time.GetLocalNow(),
        Target = target,
        Step = step,
        Result = result,
        ToolExitCode = exitCode,
        DurationSeconds = Math.Round(duration.TotalSeconds, 2),
        Output = output.Length > JsonExecutionJournal.MaxOutputLength ? output[..JsonExecutionJournal.MaxOutputLength] + " …(truncado)" : output,
      });
    }

    Persist();
  }

  public void Output(string target, string line)
  {
    // Linhas individuais não são gravadas; a saída completa (truncada) vai em ToolResult.
  }

  public void Complete(int exitCode, string status, IEnumerable<TargetOutcome>? targets = null, IEnumerable<string>? recovery = null)
  {
    lock (_gate)
    {
      var now = _time.GetLocalNow();
      Record.FinishedAt = now;
      Record.DurationSeconds = Math.Round((now - Record.StartedAt).TotalSeconds, 2);
      Record.ExitCode = exitCode;
      Record.Status = status;
      if (targets is not null)
        Record.Targets = targets.ToList();
      if (recovery is not null)
        Record.Recovery = recovery.ToList();
    }

    Persist();
  }

  private void Persist()
  {
    if (_journal is null)
      return;

    try
    {
      lock (_gate)
        _journal.Save(Record);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
      PersistenceWarning = $"Não foi possível gravar o histórico em '{_journal.Directory}': {ex.Message}";
    }
  }
}
