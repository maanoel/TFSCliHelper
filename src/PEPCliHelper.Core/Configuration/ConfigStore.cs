using System.Text.Json;
using System.Text.Json.Serialization;
using PEPCliHelper.Core.FileSystem;

namespace PEPCliHelper.Core.Configuration;

public enum ConfigLoadStatus
{
  Loaded,
  Missing,
  Invalid,
}

public sealed record ConfigLoadResult(ConfigLoadStatus Status, string Path, PepConfig? Config, IReadOnlyList<string> Errors);

public sealed class ConfigStore
{
  private static readonly string[] SecretMarkers = ["senha", "password", "token", "secret", "segredo", "pat", "credencial", "credential"];

  private static readonly JsonSerializerOptions Options = new()
  {
    WriteIndented = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
  };

  private readonly IFileSystem _fileSystem;

  public ConfigStore(IFileSystem fileSystem, string path)
  {
    _fileSystem = fileSystem;
    Path = path;
  }

  public string Path { get; }

  public bool Exists => _fileSystem.FileExists(Path);

  public ConfigLoadResult Load()
  {
    if (!_fileSystem.FileExists(Path))
      return new(ConfigLoadStatus.Missing, Path, null, [$"Arquivo de configuração não encontrado: {Path}"]);

    string content;
    try
    {
      content = _fileSystem.ReadAllText(Path);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
      return new(ConfigLoadStatus.Invalid, Path, null, [$"Não foi possível ler {Path}: {ex.Message}"]);
    }

    var secretErrors = FindSecretKeys(content);
    if (secretErrors.Count > 0)
      return new(ConfigLoadStatus.Invalid, Path, null, secretErrors);

    PepConfig? config;
    try
    {
      config = Deserialize(content);
    }
    catch (JsonException ex)
    {
      return new(ConfigLoadStatus.Invalid, Path, null, [$"JSON inválido: {ex.Message}"]);
    }

    if (config is null)
      return new(ConfigLoadStatus.Invalid, Path, null, ["Arquivo de configuração vazio."]);

    var errors = ConfigValidator.Validate(config);
    return errors.Count == 0
      ? new(ConfigLoadStatus.Loaded, Path, config, [])
      : new(ConfigLoadStatus.Invalid, Path, config, errors);
  }

  /// <summary>Valida, faz backup do arquivo anterior e grava de forma atômica. Retorna o caminho do backup, se houver.</summary>
  public string? Save(PepConfig config)
  {
    var errors = ConfigValidator.Validate(config);
    if (errors.Count > 0)
      throw new InvalidOperationException("Configuração inválida: " + string.Join(" | ", errors));

    string? backup = null;
    if (_fileSystem.FileExists(Path))
    {
      // Nome único mesmo com gravações no mesmo segundo (ex.: duas execuções de config auto).
      var stamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
      backup = $"{Path}.{stamp}.bak";
      for (var attempt = 1; _fileSystem.FileExists(backup); attempt++)
        backup = $"{Path}.{stamp}-{attempt}.bak";
      _fileSystem.CopyFile(Path, backup);
    }

    _fileSystem.WriteAllTextAtomic(Path, Serialize(config));
    return backup;
  }

  public static string Serialize(PepConfig config) => JsonSerializer.Serialize(config, Options);

  public static PepConfig? Deserialize(string json) => JsonSerializer.Deserialize<PepConfig>(json, Options);

  internal static IReadOnlyList<string> FindSecretKeys(string json)
  {
    var errors = new List<string>();
    try
    {
      using var document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
      Walk(document.RootElement, "$", errors);
    }
    catch (JsonException)
    {
      // Erros de sintaxe são reportados na desserialização.
    }

    return errors;
  }

  private static void Walk(JsonElement element, string path, List<string> errors)
  {
    if (element.ValueKind == JsonValueKind.Object)
    {
      foreach (var property in element.EnumerateObject())
      {
        var name = property.Name.ToLowerInvariant();
        if (SecretMarkers.Any(marker => name == marker || name.Contains(marker) && marker.Length > 3))
          errors.Add($"{path}.{property.Name}: a configuração não aceita senhas ou tokens. A autenticação é a do tf.exe/Visual Studio; remova o campo.");
        Walk(property.Value, $"{path}.{property.Name}", errors);
      }
    }
    else if (element.ValueKind == JsonValueKind.Array)
    {
      var index = 0;
      foreach (var item in element.EnumerateArray())
        Walk(item, $"{path}[{index++}]", errors);
    }
  }
}
