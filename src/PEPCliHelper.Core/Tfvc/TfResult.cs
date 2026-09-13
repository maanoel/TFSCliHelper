using PEPCliHelper.Core.Execution;

namespace PEPCliHelper.Core.Tfvc;

public enum TfStatus
{
  Success,
  PartialSuccess,
  Failed,
  NotRecognized,
  NetworkError,
  AuthError,
  Cancelled,
}

public sealed record TfResult(TfStatus Status, int ExitCode, string Output, TimeSpan Duration, string CommandLine)
{
  /// <summary>Rede ou autenticação: nenhuma operação seguinte deve continuar.</summary>
  public bool IsEnvironmentError => Status is TfStatus.NetworkError or TfStatus.AuthError;

  public bool IsSuccess => Status == TfStatus.Success;

  /// <summary>Primeiras linhas relevantes da saída, para mensagens.</summary>
  public string Summary
  {
    get
    {
      var lines = Output
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(l => !l.StartsWith("===", StringComparison.Ordinal))
        .Take(3)
        .ToList();
      return lines.Count == 0 ? $"tf.exe retornou código {ExitCode} sem mensagem." : string.Join(" ", lines);
    }
  }

  public string StatusText => Status switch
  {
    TfStatus.Success => "sucesso",
    TfStatus.PartialSuccess => "sucesso parcial",
    TfStatus.Failed => "falhou",
    TfStatus.NotRecognized => "comando não reconhecido",
    TfStatus.NetworkError => "servidor indisponível",
    TfStatus.AuthError => "sem autenticação/permissão",
    TfStatus.Cancelled => "cancelado",
    _ => Status.ToString(),
  };
}

/// <summary>
/// Classifica o resultado do tf.exe. Exit codes documentados: 0 sucesso, 1 sucesso parcial,
/// 2 comando não reconhecido, 100 nada executado. Códigos TF só são usados quando confirmados.
/// </summary>
public static class TfErrorClassifier
{
  /// <summary>Observado na máquina da equipe: servidor Azure DevOps indisponível (inclui falha SSL/TLS).</summary>
  public const string ServiceUnavailableCode = "TF400324";

  /// <summary>Usuário não autorizado a acessar o servidor/coleção.</summary>
  public const string NotAuthorizedCode = "TF30063";

  public static TfResult Classify(CommandResult result, string commandLine)
  {
    var output = result.Output;
    var status = result switch
    {
      { Cancelled: true } => TfStatus.Cancelled,
      { ExitCode: 0 } => TfStatus.Success,
      _ when output.Contains(ServiceUnavailableCode, StringComparison.OrdinalIgnoreCase) => TfStatus.NetworkError,
      _ when output.Contains(NotAuthorizedCode, StringComparison.OrdinalIgnoreCase) => TfStatus.AuthError,
      { ExitCode: 1 } => TfStatus.PartialSuccess,
      { ExitCode: 2 } => TfStatus.NotRecognized,
      _ => TfStatus.Failed,
    };

    return new TfResult(status, result.ExitCode, output, result.Duration, commandLine);
  }

  public static string Guidance(TfResult result) => result.Status switch
  {
    TfStatus.NetworkError => "Verifique VPN/proxy e o acesso à coleção no navegador; depois execute 'pep doctor'.",
    TfStatus.AuthError =>
      "O tf.exe não tem credencial em cache (TF30063). Execute 'pep login' em um terminal e entre com a conta da coleção; depois 'pep doctor'.",
    TfStatus.NotRecognized => "A versão do tf.exe não reconheceu o comando. Verifique a versão em 'pep version' e registre em docs/HOMOLOGACAO.md.",
    TfStatus.Cancelled => "A operação foi interrompida. Inspecione o estado com 'pep pending list'.",
    _ => "Leia a mensagem do tf.exe acima e consulte 'pep history show <id>' para a saída completa.",
  };
}
