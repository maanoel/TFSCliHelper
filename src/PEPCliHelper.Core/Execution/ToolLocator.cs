using System.Diagnostics;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.Configuration;
using PEPCliHelper.Core.FileSystem;

namespace PEPCliHelper.Core.Execution;

public sealed record ToolInfo(string Name, string? Path, string? Version, string Source, string? Problem)
{
  public bool Found => Path is not null && Problem is null;
}

/// <summary>Localiza tf.exe e MSBuild: configuração → vswhere → ausente. Nunca presume PATH (spec 005).</summary>
public sealed class ToolLocator
{
  public const string TfName = "tf.exe";
  public const string MsBuildName = "MSBuild";

  internal const string TfRelativePath = @"Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\TF.exe";

  private readonly IFileSystem _fileSystem;
  private readonly ICommandExecutor _executor;
  private readonly ToolsConfig _tools;
  private readonly string _vswherePath;
  private readonly Dictionary<string, ToolInfo> _cache = new(StringComparer.OrdinalIgnoreCase);

  public ToolLocator(IFileSystem fileSystem, ICommandExecutor executor, ToolsConfig tools, string? vswherePath = null)
  {
    _fileSystem = fileSystem;
    _executor = executor;
    _tools = tools;
    _vswherePath = vswherePath ?? System.IO.Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
      @"Microsoft Visual Studio\Installer\vswhere.exe");
  }

  public Task<ToolInfo> LocateTfAsync(CancellationToken cancellationToken = default) =>
    LocateAsync(TfName, _tools.TfExe, "ferramentas.tfExe",
      ["-latest", "-products", "*", "-find", TfRelativePath], cancellationToken);

  public Task<ToolInfo> LocateMsBuildAsync(CancellationToken cancellationToken = default) =>
    LocateAsync(MsBuildName, _tools.MsBuild, "ferramentas.msBuild",
      ["-latest", "-products", "*", "-requires", "Microsoft.Component.MSBuild", "-find", @"MSBuild\**\Bin\MSBuild.exe"], cancellationToken);

  public async Task<ToolInfo> RequireTfAsync(CancellationToken cancellationToken = default) =>
    Require(await LocateTfAsync(cancellationToken), "ferramentas.tfExe", "Instale o Visual Studio com Team Explorer");

  public async Task<ToolInfo> RequireMsBuildAsync(CancellationToken cancellationToken = default) =>
    Require(await LocateMsBuildAsync(cancellationToken), "ferramentas.msBuild", "Instale o Visual Studio com a carga de trabalho de desenvolvimento .NET");

  private static ToolInfo Require(ToolInfo tool, string configKey, string install)
  {
    if (tool.Found)
      return tool;

    throw new ToolNotFoundException(tool.Name, tool.Problem ?? "Não localizada.",
      $"{install} ou informe o caminho em '{configKey}' no arquivo de configuração. Depois execute 'pep doctor'.");
  }

  private async Task<ToolInfo> LocateAsync(string name, string? configured, string configKey, string[] vswhereArguments, CancellationToken cancellationToken)
  {
    if (_cache.TryGetValue(name, out var cached))
      return cached;

    ToolInfo result;
    if (!string.IsNullOrWhiteSpace(configured))
    {
      result = _fileSystem.FileExists(configured)
        ? new ToolInfo(name, configured, ReadVersion(configured), "configuração", null)
        : new ToolInfo(name, null, null, "configuração", $"'{configKey}' aponta para '{configured}', que não existe.");
    }
    else if (!_fileSystem.FileExists(_vswherePath))
    {
      result = new ToolInfo(name, null, null, "vswhere", $"vswhere.exe não encontrado em '{_vswherePath}' e '{configKey}' não está configurado.");
    }
    else
    {
      result = await FindWithVswhereAsync(name, configKey, vswhereArguments, cancellationToken);
    }

    _cache[name] = result;
    return result;
  }

  private async Task<ToolInfo> FindWithVswhereAsync(string name, string configKey, string[] arguments, CancellationToken cancellationToken)
  {
    var output = await _executor.ExecuteAsync(new Command(_vswherePath, arguments), null, cancellationToken);
    var path = output.StdOut
      .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
      .FirstOrDefault(line => line.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && _fileSystem.FileExists(line));

    return path is null
      ? new ToolInfo(name, null, null, "vswhere", $"Nenhuma instalação do Visual Studio com {name} foi encontrada e '{configKey}' não está configurado.")
      : new ToolInfo(name, path, ReadVersion(path), "vswhere", null);
  }

  private static string? ReadVersion(string path)
  {
    try
    {
      return FileVersionInfo.GetVersionInfo(path).FileVersion?.Split(' ', 2)[0];
    }
    catch (FileNotFoundException)
    {
      return null;
    }
  }
}
