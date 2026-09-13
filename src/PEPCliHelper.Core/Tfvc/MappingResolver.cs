using PEPCliHelper.Core.Common;

namespace PEPCliHelper.Core.Tfvc;

public enum MappingState
{
  Mapped,
  FolderMissing,
  NotMapped,
  Cloaked,
  Divergent,
  QueryFailed,
}

public sealed record MappingCheck(
  MappingState State,
  string LocalPath,
  string? ExpectedServerPath,
  string? EffectiveServerPath,
  string? WorkspaceName,
  string? Owner,
  string? Collection,
  string? MappingLocalRoot,
  IReadOnlyList<string> CloakedChildren,
  string Message,
  string? Guidance)
{
  public bool IsValid => State == MappingState.Mapped;

  public string StateText => Describe(State);

  public static string Describe(MappingState state) => state switch
  {
    MappingState.Mapped => "mapeado",
    MappingState.FolderMissing => "pasta inexistente",
    MappingState.NotMapped => "não mapeado",
    MappingState.Cloaked => "cloaked",
    MappingState.Divergent => "mapeamento divergente",
    MappingState.QueryFailed => "consulta falhou",
    _ => state.ToString(),
  };
}

/// <summary>
/// Decide se uma pasta local está coberta por um mapeamento válido (inclusive em diretório ancestral),
/// se está cloaked e se aponta para o caminho de servidor esperado (spec 005 / 009 do documento).
/// </summary>
public static class MappingResolver
{
  public static MappingCheck Evaluate(string localPath, string? expectedServerPath, bool folderExists, TfResult? query, WorkfoldInfo? info)
  {
    if (!folderExists)
    {
      return Result(MappingState.FolderMissing, null, null,
        $"A pasta '{localPath}' não existe.",
        "Confirme 'caminhoLocal' da versão em 'pep config show' ou faça o get inicial pelo Visual Studio. Depois execute 'pep env validate'.");
    }

    if (query is null || query.IsEnvironmentError || query.Status == TfStatus.Cancelled)
    {
      return Result(MappingState.QueryFailed, null, null,
        query is null ? "Mapeamento não consultado." : $"Não foi possível consultar o mapeamento: {query.StatusText}. {query.Summary}",
        query is null ? null : TfErrorClassifier.Guidance(query));
    }

    var guidanceNotMapped =
      $"Verifique com: tf workfold \"{localPath}\". Mapeie a pasta pelo Source Control Explorer do Visual Studio " +
      "(o PEP CLI não cria nem altera mapeamentos). Depois execute 'pep env validate'.";

    if (!query.IsSuccess || info is null)
    {
      return Result(MappingState.NotMapped, null, null,
        $"A pasta '{localPath}' não pertence a nenhum workspace conhecido nesta máquina (pasta não mapeada, workspace de outra máquina ou coleção diferente). tf: {query.Summary}",
        guidanceNotMapped);
    }

    var covering = info.Mappings
      .Where(m => !m.IsCloaked && m.LocalPath is not null && LocalPath.IsUnderOrEqual(localPath, m.LocalPath))
      .OrderByDescending(m => LocalPath.Normalize(m.LocalPath!).Length)
      .FirstOrDefault();

    if (covering is null)
    {
      return Result(MappingState.NotMapped, null, null,
        $"O workspace '{info.WorkspaceName}' não possui mapeamento que cubra '{localPath}'.",
        guidanceNotMapped, info);
    }

    var relative = LocalPath.Relative(localPath, covering.LocalPath!).Replace('\\', '/');
    var effective = TfvcPath.Combine(covering.ServerPath, relative);

    var cloak = info.Mappings
      .Where(m => m.IsCloaked
        && TfvcPath.IsUnderOrEqual(effective, m.ServerPath)
        && TfvcPath.Normalize(m.ServerPath).Length > TfvcPath.Normalize(covering.ServerPath).Length)
      .OrderByDescending(m => m.ServerPath.Length)
      .FirstOrDefault();

    var cloakedChildren = info.Mappings
      .Where(m => m.IsCloaked && TfvcPath.IsUnderOrEqual(m.ServerPath, effective) && !TfvcPath.AreEqual(m.ServerPath, effective))
      .Select(m => m.ServerPath)
      .ToList();

    if (cloak is not null)
    {
      return Result(MappingState.Cloaked, effective, covering.LocalPath,
        $"O caminho '{effective}' está cloaked ('{cloak.ServerPath}') no workspace '{info.WorkspaceName}'.",
        $"Remova o cloak pelo Source Control Explorer ou manualmente com: tf workfold /decloak \"{cloak.ServerPath}\". Depois execute 'pep env validate'.",
        info, cloakedChildren);
    }

    if (expectedServerPath is not null && !TfvcPath.AreEqual(effective, expectedServerPath))
    {
      return Result(MappingState.Divergent, effective, covering.LocalPath,
        $"A pasta '{localPath}' está mapeada para '{effective}', mas a configuração espera '{expectedServerPath}'.",
        "Corrija 'caminhoServidor'/'caminhoLocal' da versão na configuração ou o mapeamento do workspace. O PEP CLI não opera em caminho divergente.",
        info, cloakedChildren);
    }

    return Result(MappingState.Mapped, effective, covering.LocalPath,
      $"Mapeado no workspace '{info.WorkspaceName}' para '{effective}'.", null, info, cloakedChildren);

    MappingCheck Result(MappingState state, string? effectivePath, string? root, string message, string? guidance,
      WorkfoldInfo? workfold = null, IReadOnlyList<string>? children = null) =>
      new(state, localPath, expectedServerPath, effectivePath, workfold?.WorkspaceName, workfold?.Owner, workfold?.Collection,
        root, children ?? [], message, guidance);
  }
}
