namespace PEPCliHelper.Core.Common;

/// <summary>
/// Erro conhecido do CLI. Carrega as respostas exigidas pela spec 012:
/// o que falhou, onde, impacto, o que permanece alterado e próximo passo seguro.
/// </summary>
public class PepCliException : Exception
{
  public PepCliException(
    string message,
    int exitCode,
    string? where = null,
    string? impact = null,
    string? remains = null,
    string? nextStep = null)
    : base(message)
  {
    ExitCode = exitCode;
    Where = where;
    Impact = impact;
    Remains = remains;
    NextStep = nextStep;
  }

  public int ExitCode { get; }
  public string? Where { get; }
  public string? Impact { get; }
  public string? Remains { get; }
  public string? NextStep { get; }
}

public sealed class UsageException : PepCliException
{
  public UsageException(string message, string? nextStep = null, string? where = null)
    : base(message, ExitCodes.Usage, where, "Nenhuma operação foi executada.", "Nada foi alterado.", nextStep)
  {
  }
}

public sealed class PreconditionException : PepCliException
{
  public PreconditionException(string message, string? nextStep = null, string? where = null, string? remains = null)
    : base(message, ExitCodes.Precondition, where, "A operação não foi iniciada.", remains ?? "Nada foi alterado.", nextStep)
  {
  }
}

public sealed class ToolNotFoundException : PepCliException
{
  public ToolNotFoundException(string tool, string detail, string nextStep)
    : base($"Ferramenta ausente: {tool}. {detail}", ExitCodes.Precondition, tool,
      "Operações que dependem desta ferramenta não podem ser executadas.", "Nada foi alterado.", nextStep)
  {
    Tool = tool;
  }

  public string Tool { get; }
}
