using System.Text.Json;
using System.Text.Json.Serialization;

namespace PEPCliHelper.IntegrationTests;

public sealed class IntegrationTarget
{
  [JsonPropertyName("serverPath")]
  public string ServerPath { get; set; } = string.Empty;

  [JsonPropertyName("localPath")]
  public string LocalPath { get; set; } = string.Empty;
}

public sealed class IntegrationChangesets
{
  [JsonPropertyName("edit")]
  public int? Edit { get; set; }

  [JsonPropertyName("add")]
  public int? Add { get; set; }

  [JsonPropertyName("delete")]
  public int? Delete { get; set; }

  [JsonPropertyName("rename")]
  public int? Rename { get; set; }

  [JsonPropertyName("singleChangeset")]
  public int? SingleChangeset { get; set; }

  [JsonPropertyName("alreadyIntegrated")]
  public int? AlreadyIntegrated { get; set; }

  [JsonPropertyName("conflict")]
  public int? Conflict { get; set; }

  [JsonPropertyName("pendingInScope")]
  public int? PendingInScope { get; set; }
}

/// <summary>Configuração dos testes de integração TFVC (arquivo apontado por PEPCLI_IT_CONFIG). Ver README.md.</summary>
public sealed class IntegrationConfig
{
  public const string EnvironmentVariable = "PEPCLI_IT_CONFIG";

  private const string ForbiddenServerRoot = "$/Linha-RM";
  private const string ForbiddenLocalRoot = @"C:\Linha-RM";

  [JsonPropertyName("colecaoDeTeste")]
  public bool IsTestCollection { get; set; }

  [JsonPropertyName("collection")]
  public string Collection { get; set; } = string.Empty;

  [JsonPropertyName("tfExe")]
  public string? TfExe { get; set; }

  [JsonPropertyName("sourceServer")]
  public string SourceServer { get; set; } = string.Empty;

  [JsonPropertyName("sourceLocal")]
  public string SourceLocal { get; set; } = string.Empty;

  [JsonPropertyName("targets")]
  public List<IntegrationTarget> Targets { get; set; } = [];

  [JsonPropertyName("noRelationTarget")]
  public IntegrationTarget? NoRelationTarget { get; set; }

  [JsonPropertyName("unmappedTarget")]
  public IntegrationTarget? UnmappedTarget { get; set; }

  [JsonPropertyName("changesets")]
  public IntegrationChangesets Changesets { get; set; } = new();

  public static string? ConfiguredPath
  {
    get
    {
      var path = Environment.GetEnvironmentVariable(EnvironmentVariable);
      return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : null;
    }
  }

  /// <summary>Lê sem validar (usado na descoberta para decidir o skip).</summary>
  public static IntegrationConfig? TryRead()
  {
    var path = ConfiguredPath;
    if (path is null)
      return null;

    try
    {
      return Read(path);
    }
    catch (JsonException)
    {
      // Sem skip: o teste falha ao carregar com a mensagem do JSON inválido.
      return null;
    }
  }

  /// <summary>Lê e aplica as travas de segurança; falha com mensagem clara se o alvo não for de teste.</summary>
  public static IntegrationConfig Load()
  {
    var path = ConfiguredPath
      ?? throw new InvalidOperationException($"Variável {EnvironmentVariable} não aponta para um arquivo existente.");
    var config = Read(path);
    var problems = config.SafetyProblems();
    if (problems.Count > 0)
      throw new InvalidOperationException(
        $"Testes de integração recusados ({path}): {string.Join(" ", problems)} Nada foi executado no TFVC.");
    return config;
  }

  public IReadOnlyList<string> SafetyProblems() => Validate(this);

  /// <summary>Travas de segurança, sem depender da variável de ambiente (testável isoladamente).</summary>
  public static IReadOnlyList<string> Validate(IntegrationConfig config)
  {
    var problems = new List<string>();
    if (!config.IsTestCollection)
      problems.Add("'colecaoDeTeste' deve ser true (confirmação de que a coleção/branches são exclusivos de teste).");
    if (string.IsNullOrWhiteSpace(config.Collection))
      problems.Add("'collection' é obrigatório.");
    if (config.Targets is null || config.Targets.Count < 2)
      problems.Add("'targets' deve ter ao menos dois destinos.");

    var targets = (config.Targets ?? []).Append(config.NoRelationTarget).Append(config.UnmappedTarget).OfType<IntegrationTarget>().ToList();
    foreach (var server in targets.Select(t => t.ServerPath ?? string.Empty).Prepend(config.SourceServer ?? string.Empty))
    {
      if (!server.StartsWith("$/", StringComparison.Ordinal))
        problems.Add($"Caminho de servidor inválido: '{server}'.");
      else if (server.Split('/').Any(segment => segment.Trim() is "." or ".."))
        problems.Add($"Caminho de servidor não pode conter '.' ou '..': '{server}'.");
      if (server.StartsWith(ForbiddenServerRoot, StringComparison.OrdinalIgnoreCase))
        problems.Add($"Caminho de servidor da equipe proibido: '{server}'.");
    }

    foreach (var local in targets.Select(t => t.LocalPath ?? string.Empty).Prepend(config.SourceLocal ?? string.Empty))
    {
      // Somente "X:\..." : caminhos UNC ou de dispositivo (\\?\, \\.\) poderiam contornar a comparação com C:\Linha-RM.
      if (!IsDriveRooted(local))
        problems.Add($"Caminho local deve ser absoluto com letra de unidade (ex.: C:\\TestePEP): '{local}'.");
      else if (local.Contains('~'))
        problems.Add($"Caminho local não pode usar nome curto 8.3 ('~'): '{local}'.");
      else if (IsUnder(Path.GetFullPath(local), ForbiddenLocalRoot))
        problems.Add($"Caminho local da equipe proibido: '{local}'.");
    }

    return problems;
  }

  private static bool IsDriveRooted(string path) =>
    path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && (path[2] is '\\' or '/') && Path.IsPathFullyQualified(path);

  // Prefixo textual (sem exigir separador): também recusa irmãos como C:\Linha-RM2, o que é o lado seguro.
  private static bool IsUnder(string fullPath, string root) =>
    fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);

  /// <summary>Entrada opcional preenchida? Chaves: "changesets.edit", "noRelationTarget" etc.</summary>
  public bool Has(string key) => key switch
  {
    "changesets.edit" => Changesets.Edit is not null,
    "changesets.add" => Changesets.Add is not null,
    "changesets.delete" => Changesets.Delete is not null,
    "changesets.rename" => Changesets.Rename is not null,
    "changesets.singleChangeset" => Changesets.SingleChangeset is not null,
    "changesets.alreadyIntegrated" => Changesets.AlreadyIntegrated is not null,
    "changesets.conflict" => Changesets.Conflict is not null,
    "changesets.pendingInScope" => Changesets.PendingInScope is not null,
    "noRelationTarget" => NoRelationTarget is not null,
    "unmappedTarget" => UnmappedTarget is not null,
    _ => throw new ArgumentException($"Chave de configuração desconhecida: '{key}'.", nameof(key)),
  };

  private static IntegrationConfig Read(string path) =>
    JsonSerializer.Deserialize<IntegrationConfig>(File.ReadAllText(path), new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })
      ?? throw new InvalidOperationException($"Arquivo de configuração vazio: {path}.");
}

/// <summary>
/// Fact de integração: ignorado se PEPCLI_IT_CONFIG não aponta para um arquivo existente ou se alguma
/// entrada exigida estiver nula. Nunca executa por padrão.
/// </summary>
public sealed class IntegrationFactAttribute : FactAttribute
{
  public IntegrationFactAttribute(params string[] requiredKeys)
  {
    if (IntegrationConfig.ConfiguredPath is null)
    {
      Skip = $"Integração TFVC desativada: defina {IntegrationConfig.EnvironmentVariable} com o JSON da coleção de teste.";
      return;
    }

    var config = IntegrationConfig.TryRead();
    var missing = config is null ? [] : requiredKeys.Where(k => !config.Has(k)).ToList();
    if (missing.Count > 0)
      Skip = $"Cenário não preparado: {string.Join(", ", missing)} nulo(s) na configuração.";
  }
}
