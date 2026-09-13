using PEPCliHelper.Core.Execution;
using PEPCliHelper.Core.Processes;

namespace PEPCliHelper.Tests.Fakes;

public sealed class FakeCommandExecutor : ICommandExecutor
{
  public List<Command> Executed { get; } = [];

  public Func<Command, CommandResult> Respond { get; set; } = _ => new CommandResult(0, string.Empty, string.Empty, TimeSpan.Zero);

  public Task<CommandResult> ExecuteAsync(Command command, Action<string>? onOutputLine = null, CancellationToken cancellationToken = default)
  {
    Executed.Add(command);
    return Task.FromResult(Respond(command));
  }
}

public sealed class FakeAttachedRunner : IAttachedProcessRunner
{
  public List<Command> Executed { get; } = [];

  public int ExitCode { get; set; }

  public Task<int> RunAsync(Command command, CancellationToken cancellationToken)
  {
    Executed.Add(command);
    return Task.FromResult(ExitCode);
  }
}

public sealed class FakeProcessInspector : IProcessInspector
{
  public List<ProcessInfo> Processes { get; } = [];

  public HashSet<int> IgnoresGracefulClose { get; } = [];

  public List<int> GracefulRequests { get; } = [];

  public List<int> Killed { get; } = [];

  public List<(string File, string? Arguments)> Started { get; } = [];

  public IReadOnlyList<ProcessInfo> ListByName(string processName) =>
    Processes.Where(p => p.Name.Equals(processName, StringComparison.OrdinalIgnoreCase)).ToList();

  public TerminationOutcome TryCloseGracefully(ProcessInfo expected, TimeSpan wait)
  {
    var current = Processes.FirstOrDefault(p => p.Pid == expected.Pid);
    if (current is null)
      return TerminationOutcome.NotRunning;
    if (current != expected)
      return TerminationOutcome.ProcessChanged;

    GracefulRequests.Add(expected.Pid);
    if (IgnoresGracefulClose.Contains(expected.Pid))
      return TerminationOutcome.StillRunning;
    Processes.Remove(current);
    return TerminationOutcome.Terminated;
  }

  public TerminationOutcome Kill(ProcessInfo expected, TimeSpan wait)
  {
    var current = Processes.FirstOrDefault(p => p.Pid == expected.Pid);
    if (current is null)
      return TerminationOutcome.NotRunning;
    if (current != expected)
      return TerminationOutcome.ProcessChanged;

    Killed.Add(expected.Pid);
    Processes.Remove(current);
    return TerminationOutcome.Terminated;
  }

  public void Start(string fileName, string? arguments, string? workingDirectory) => Started.Add((fileName, arguments));
}
