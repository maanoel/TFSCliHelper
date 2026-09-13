using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.FileSystem;

namespace PEPCliHelper.Core.Tfvc;

/// <summary>Consulta e avalia o mapeamento de uma pasta local. Somente leitura.</summary>
public sealed class MappingService
{
  private readonly ITfvcClient _tfvc;
  private readonly IFileSystem _fileSystem;

  public MappingService(ITfvcClient tfvc, IFileSystem fileSystem)
  {
    _tfvc = tfvc;
    _fileSystem = fileSystem;
  }

  public async Task<(MappingCheck Check, TfResult? Query)> CheckAsync(
    string localPath, string? expectedServerPath, string target, IOperationLog log, CancellationToken cancellationToken)
  {
    if (!_fileSystem.DirectoryExists(localPath))
      return (MappingResolver.Evaluate(localPath, expectedServerPath, folderExists: false, null, null), null);

    log.Step(target, "Consultando mapeamento TFVC");
    var query = await _tfvc.GetWorkfoldAsync(localPath, cancellationToken);
    log.ToolResult(target, "tf workfold", query.Result.ExitCode, query.Result.StatusText, query.Result.Duration, query.Result.Output);
    return (MappingResolver.Evaluate(localPath, expectedServerPath, folderExists: true, query.Result, query.Info), query.Result);
  }

  /// <summary>
  /// Workspace local possui a pasta oculta "$tf" na raiz do mapeamento. Em workspace de servidor,
  /// arquivos sem checkout ficam somente leitura — base da detecção de alterações não reconciliadas.
  /// </summary>
  public bool IsLocalWorkspace(MappingCheck check)
  {
    var stop = check.MappingLocalRoot ?? check.LocalPath;
    var current = new DirectoryInfo(check.LocalPath);
    while (current is not null)
    {
      if (_fileSystem.DirectoryExists(Path.Combine(current.FullName, "$tf")))
        return true;
      if (LocalPath.AreEqual(current.FullName, stop) || current.Parent is null)
        break;
      current = current.Parent;
    }

    return false;
  }
}
