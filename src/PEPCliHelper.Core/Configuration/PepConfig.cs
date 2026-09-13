using System.Text.Json.Serialization;

namespace PEPCliHelper.Core.Configuration;

/// <summary>Modelo persistido em config.json (spec 003). Nomes JSON em português.</summary>
public sealed class PepConfig
{
  public const string DefaultCollection = "https://totvstfs.visualstudio.com/DefaultCollection";

  [JsonPropertyName("versaoEsquema")]
  public int SchemaVersion { get; set; } = 1;

  [JsonPropertyName("raizLocal")]
  public string LocalRoot { get; set; } = @"C:\Linha-RM";

  [JsonPropertyName("colecao")]
  public string? Collection { get; set; }

  /// <summary>
  /// Raiz TFVC que espelha a raiz local. Convenção da equipe: &lt;raizLocal&gt;\Legado\12.1.2506 ⇄ &lt;raizServidor&gt;/Legado/12.1.2506.
  /// Usada quando o mapeamento não informa o caminho de servidor.
  /// </summary>
  [JsonPropertyName("raizServidor")]
  public string ServerRoot { get; set; } = "$/Linha-RM";

  [JsonPropertyName("ferramentas")]
  public ToolsConfig Tools { get; set; } = new();

  [JsonPropertyName("versoes")]
  public List<VersionConfig> Versions { get; set; } = [];

  [JsonPropertyName("projetos")]
  public List<ProjectConfig> Projects { get; set; } = [];

  [JsonPropertyName("arquivosLocais")]
  public LocalFilesConfig LocalFiles { get; set; } = new();

  [JsonPropertyName("apresentacao")]
  public DisplayConfig Display { get; set; } = new();

  /// <summary>Defaults seguros: raiz, coleção da equipe e projetos backend. Nenhuma versão.</summary>
  public static PepConfig CreateDefault() => new()
  {
    Collection = DefaultCollection,
    Projects =
    [
      new ProjectConfig { Alias = "sau", Name = "Sau-Saude", LocalFolder = "Sau-Saude", ServerFolder = "Sau-Saude", Solution = "Sau-Saude.sln" },
      new ProjectConfig { Alias = "back", Name = "Sau-PEP", LocalFolder = "Sau-PEP", ServerFolder = "Sau-PEP", Solution = "RM.Pep.sln" },
    ],
  };
}

public sealed class ToolsConfig
{
  [JsonPropertyName("tfExe")]
  public string? TfExe { get; set; }

  [JsonPropertyName("msBuild")]
  public string? MsBuild { get; set; }

  [JsonPropertyName("editor")]
  public string Editor { get; set; } = "notepad++";
}

public sealed class VersionConfig
{
  [JsonPropertyName("id")]
  public string Id { get; set; } = string.Empty;

  [JsonPropertyName("atual")]
  public bool IsCurrent { get; set; }

  [JsonPropertyName("ativa")]
  public bool Active { get; set; } = true;

  /// <summary>Relativo à raiz local ou absoluto.</summary>
  [JsonPropertyName("caminhoLocal")]
  public string LocalPath { get; set; } = string.Empty;

  [JsonPropertyName("caminhoServidor")]
  public string ServerPath { get; set; } = string.Empty;

  [JsonPropertyName("aliases")]
  public List<string> Aliases { get; set; } = [];

  [JsonPropertyName("workspace")]
  public string? Workspace { get; set; }
}

public sealed class ProjectConfig
{
  [JsonPropertyName("alias")]
  public string Alias { get; set; } = string.Empty;

  [JsonPropertyName("nome")]
  public string Name { get; set; } = string.Empty;

  [JsonPropertyName("pastaLocal")]
  public string LocalFolder { get; set; } = string.Empty;

  [JsonPropertyName("pastaServidor")]
  public string ServerFolder { get; set; } = string.Empty;

  /// <summary>Solução relativa à pasta do projeto.</summary>
  [JsonPropertyName("solucao")]
  public string? Solution { get; set; }
}

/// <summary>Arquivos do RM relativos à pasta da versão.</summary>
public sealed class LocalFilesConfig
{
  [JsonPropertyName("broker")]
  public string Broker { get; set; } = @"Bin\_Broker.dat";

  [JsonPropertyName("host")]
  public string Host { get; set; } = @"Bin\RM.Host.exe";

  [JsonPropertyName("rm")]
  public string Rm { get; set; } = @"Bin\RM.exe";

  [JsonPropertyName("alias")]
  public string Alias { get; set; } = @"Bin\Alias.dat";

  [JsonPropertyName("hostConfig")]
  public string HostConfig { get; set; } = @"Bin\RM.Host.exe.config";
}

public sealed class DisplayConfig
{
  [JsonPropertyName("semCor")]
  public bool NoColor { get; set; }

  [JsonPropertyName("ascii")]
  public bool Ascii { get; set; }
}
