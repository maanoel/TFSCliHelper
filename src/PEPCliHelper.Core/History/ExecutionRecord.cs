using System.Text.Json.Serialization;

namespace PEPCliHelper.Core.History;

/// <summary>Registro local de uma execução do CLI (spec 012). Não substitui o histórico TFVC.</summary>
public sealed class ExecutionRecord
{
  [JsonPropertyName("id")]
  public string Id { get; set; } = string.Empty;

  [JsonPropertyName("comando")]
  public string Command { get; set; } = string.Empty;

  [JsonPropertyName("argumentos")]
  public List<string> Arguments { get; set; } = [];

  [JsonPropertyName("projeto")]
  public string? Project { get; set; }

  [JsonPropertyName("origem")]
  public string? Source { get; set; }

  [JsonPropertyName("changeset")]
  public int? Changeset { get; set; }

  [JsonPropertyName("versoes")]
  public List<string> Versions { get; set; } = [];

  [JsonPropertyName("inicio")]
  public DateTimeOffset StartedAt { get; set; }

  [JsonPropertyName("fim")]
  public DateTimeOffset? FinishedAt { get; set; }

  [JsonPropertyName("duracaoSegundos")]
  public double? DurationSeconds { get; set; }

  [JsonPropertyName("situacao")]
  public string Status { get; set; } = "em andamento";

  [JsonPropertyName("codigoSaida")]
  public int? ExitCode { get; set; }

  [JsonPropertyName("etapas")]
  public List<ExecutionStep> Steps { get; set; } = [];

  [JsonPropertyName("alvos")]
  public List<TargetOutcome> Targets { get; set; } = [];

  [JsonPropertyName("orientacoes")]
  public List<string> Recovery { get; set; } = [];
}

public sealed class ExecutionStep
{
  [JsonPropertyName("em")]
  public DateTimeOffset At { get; set; }

  [JsonPropertyName("alvo")]
  public string Target { get; set; } = string.Empty;

  [JsonPropertyName("etapa")]
  public string Step { get; set; } = string.Empty;

  [JsonPropertyName("resultado")]
  public string? Result { get; set; }

  [JsonPropertyName("codigoFerramenta")]
  public int? ToolExitCode { get; set; }

  [JsonPropertyName("duracaoSegundos")]
  public double? DurationSeconds { get; set; }

  [JsonPropertyName("saida")]
  public string? Output { get; set; }
}

public sealed class TargetOutcome
{
  [JsonPropertyName("alvo")]
  public string Target { get; set; } = string.Empty;

  [JsonPropertyName("estado")]
  public string State { get; set; } = string.Empty;

  [JsonPropertyName("mensagem")]
  public string Message { get; set; } = string.Empty;
}
