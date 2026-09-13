namespace PEPCliHelper.Core.Common;

/// <summary>
/// Recebe as etapas de uma operação. A apresentação usa para mostrar progresso
/// e o histórico usa para registrar. Casos de uso não conhecem nenhum dos dois.
/// </summary>
public interface IOperationLog
{
  void Step(string target, string step);

  void ToolResult(string target, string step, int exitCode, string result, TimeSpan duration, string output);

  void Output(string target, string line);
}

public sealed class NullOperationLog : IOperationLog
{
  public static readonly NullOperationLog Instance = new();

  public void Step(string target, string step) { }

  public void ToolResult(string target, string step, int exitCode, string result, TimeSpan duration, string output) { }

  public void Output(string target, string line) { }
}
