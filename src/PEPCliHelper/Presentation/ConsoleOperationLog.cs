using PEPCliHelper.Core.Common;

namespace PEPCliHelper.Presentation;

/// <summary>Encaminha etapas de um caso de uso para o spinner/linhas da tela e para o histórico.</summary>
public sealed class ConsoleOperationLog : IOperationLog
{
  private readonly IStatusSink _sink;
  private readonly IOperationLog _journal;
  private readonly string _arrow;

  public ConsoleOperationLog(IStatusSink sink, IOperationLog journal, Theme theme)
  {
    _sink = sink;
    _journal = journal;
    _arrow = theme.Arrow;
  }

  public void Step(string target, string step)
  {
    _sink.Step($"{target} {_arrow} {step}");
    _journal.Step(target, step);
  }

  public void ToolResult(string target, string step, int exitCode, string result, TimeSpan duration, string output) =>
    _journal.ToolResult(target, step, exitCode, result, duration, output);

  public void Output(string target, string line)
  {
    if (!string.IsNullOrWhiteSpace(line))
      _sink.Detail($"{target} {_arrow} {line.Trim()}");
    _journal.Output(target, line);
  }
}
