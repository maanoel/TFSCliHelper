using PEPCliHelper.Core.Execution;

namespace PEPCliHelper.IntegrationTests;

/// <summary>Decorador que registra cada comando antes de repassá-lo ao executor real.</summary>
public sealed class RecordingCommandExecutor : ICommandExecutor
{
  private static readonly string[] ForbiddenArguments =
    ["checkin", "undo", "shelve", "unshelve", "rollback", "/baseless", "/force", "/overwrite", "workspace", "/map", "/unmap", "/cloak", "/auto"];

  private readonly ICommandExecutor _inner;
  private readonly List<Command> _commands = [];
  private readonly object _gate = new();

  public RecordingCommandExecutor(ICommandExecutor inner)
  {
    _inner = inner;
  }

  public IReadOnlyList<Command> Commands
  {
    get
    {
      lock (_gate)
        return _commands.ToList();
    }
  }

  public Task<CommandResult> ExecuteAsync(Command command, Action<string>? onOutputLine = null, CancellationToken cancellationToken = default)
  {
    // A trava vem antes da execução: um comando proibido nunca chega ao tf.exe.
    if (IsForbidden(command))
      throw new InvalidOperationException($"Comando proibido nos testes de integração: {command.Display}");

    lock (_gate)
      _commands.Add(command);
    return _inner.ExecuteAsync(command, onOutputLine, cancellationToken);
  }

  /// <summary>Argumento proibido (exato ou com valor, ex.: /auto:AcceptTheirs) ou resolve sem /preview.</summary>
  public static bool IsForbidden(Command command)
  {
    var verb = command.Arguments.FirstOrDefault() ?? string.Empty;
    var hasForbidden = command.Arguments.Any(a => ForbiddenArguments.Any(f =>
      a.Equals(f, StringComparison.OrdinalIgnoreCase) || (f.StartsWith('/') && a.StartsWith(f + ":", StringComparison.OrdinalIgnoreCase))));
    var unpreviewedResolve = verb.Equals("resolve", StringComparison.OrdinalIgnoreCase)
      && !command.Arguments.Any(a => a.Equals("/preview", StringComparison.OrdinalIgnoreCase));
    return hasForbidden || unpreviewedResolve;
  }

  /// <summary>Comandos que alteram o workspace: merge/get sem /preview (candidatos são leitura) e resolve sem /preview.</summary>
  public static bool IsMutating(Command command)
  {
    var verb = command.Arguments.FirstOrDefault() ?? string.Empty;
    bool Has(string flag) => command.Arguments.Any(a => a.Equals(flag, StringComparison.OrdinalIgnoreCase));

    return verb.ToLowerInvariant() switch
    {
      "merge" => !Has("/preview") && !Has("/candidate"),
      "get" or "resolve" => !Has("/preview"),
      "status" or "workfold" or "workspaces" or "changeset" => false,
      _ => true,
    };
  }
}
