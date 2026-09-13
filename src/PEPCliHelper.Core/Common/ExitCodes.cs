namespace PEPCliHelper.Core.Common;

/// <summary>Códigos de saída estáveis do PEP CLI (spec 012).</summary>
public static class ExitCodes
{
  public const int Success = 0;
  public const int Unexpected = 1;
  public const int Usage = 2;
  public const int Precondition = 3;
  public const int OperationFailed = 4;
  public const int ConflictOrPartial = 5;
  public const int Cancelled = 130;

  public static string Describe(int code) => code switch
  {
    Success => "Sucesso",
    Unexpected => "Erro inesperado",
    Usage => "Uso ou configuração inválida",
    Precondition => "Pré-condição não atendida",
    OperationFailed => "Falha operacional",
    ConflictOrPartial => "Conflito ou resultado parcial",
    Cancelled => "Cancelado",
    _ => "Desconhecido",
  };
}
