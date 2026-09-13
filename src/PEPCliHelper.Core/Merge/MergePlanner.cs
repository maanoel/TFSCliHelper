using PEPCliHelper.Core.Catalog;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.FileSystem;
using PEPCliHelper.Core.History;
using PEPCliHelper.Core.Tfvc;

namespace PEPCliHelper.Core.Merge;

/// <summary>
/// Monta o plano de merge com todas as pré-verificações (spec 006). Somente consultas:
/// nunca executa merge, get ou qualquer alteração. É o que o --dry-run executa.
/// </summary>
public sealed class MergePlanner
{
  private readonly ITfvcClient _tfvc;
  private readonly IFileSystem _fileSystem;
  private readonly MappingService _mapping;
  private readonly IExecutionJournal? _journal;

  public MergePlanner(ITfvcClient tfvc, IFileSystem fileSystem, IExecutionJournal? journal)
  {
    _tfvc = tfvc;
    _fileSystem = fileSystem;
    _mapping = new MappingService(tfvc, fileSystem);
    _journal = journal;
  }

  public async Task<MergePlan> PlanAsync(MergeRequest request, VersionCatalog catalog, string? collection, IOperationLog log, CancellationToken cancellationToken)
  {
    var source = catalog.Locate(request.Source, request.Project);
    var plan = new MergePlan { Request = request, SourceLocation = source, Collection = collection };
    foreach (var target in request.Targets)
      plan.Targets.Add(new MergeTargetPlan { Location = catalog.Locate(target, request.Project) });

    if (!request.Source.IsCurrent)
      plan.GlobalWarnings.Add($"A origem '{request.Source.Id}' não é a versão atual. O merge só será permitido para destinos com relação de merge confirmada pelo TFVC.");

    if (string.IsNullOrWhiteSpace(collection))
    {
      plan.GlobalBlockers.Add("Coleção TFVC não configurada ('colecao'). Execute 'pep config show' e ajuste o arquivo de configuração.");
      BlockAll(plan, "Não avaliado: impedimento global.");
      return plan;
    }

    await AnalyzeChangesetAsync(plan, log, cancellationToken);
    if (plan.GlobalBlockers.Count > 0)
    {
      BlockAll(plan, "Não avaliado: impedimento global.");
      return plan;
    }

    foreach (var target in plan.Targets)
    {
      cancellationToken.ThrowIfCancellationRequested();
      if (plan.EnvironmentFailure)
      {
        target.Block("Não avaliado: falha de rede/autenticação em destino anterior.");
        continue;
      }

      await EvaluateTargetAsync(plan, target, log, cancellationToken);
    }

    return plan;
  }

  /// <summary>Revalida as condições críticas imediatamente antes de aplicar o merge (spec 007).</summary>
  public async Task<MergeTargetPlan> RevalidateAsync(MergePlan plan, MergeTargetPlan target, IOperationLog log, CancellationToken cancellationToken)
  {
    var fresh = new MergeTargetPlan { Location = target.Location };
    await EvaluateTargetAsync(plan, fresh, log, cancellationToken, revalidation: true);
    return fresh;
  }

  private async Task AnalyzeChangesetAsync(MergePlan plan, IOperationLog log, CancellationToken cancellationToken)
  {
    var request = plan.Request;
    var label = $"C{request.Changeset}";
    log.Step(label, "Consultando changeset");
    var query = await _tfvc.GetChangesetAsync(request.Changeset, plan.Collection!, cancellationToken);
    log.ToolResult(label, "tf changeset", query.Result.ExitCode, query.Result.StatusText, query.Result.Duration, query.Result.Output);

    if (query.Result.IsEnvironmentError)
    {
      plan.EnvironmentFailure = true;
      plan.GlobalBlockers.Add($"Não foi possível acessar a coleção: {query.Result.StatusText}. {TfErrorClassifier.Guidance(query.Result)}");
      return;
    }

    if (query.Changeset is null)
    {
      plan.GlobalBlockers.Add($"Changeset {request.Changeset} não encontrado ou inacessível na coleção. tf: {query.Result.Summary}");
      return;
    }

    if (query.Changeset.Items.Count == 0)
    {
      plan.GlobalBlockers.Add($"Não foi possível identificar os itens do changeset {request.Changeset}. O resultado é indeterminado; confira com 'pep changeset show {request.Changeset}'.");
      return;
    }

    var scope = ChangesetScope.Analyze(query.Changeset, plan.SourceLocation.ServerPath, request.Source.ServerRoot);
    plan.Scope = scope;

    if (scope.Included.Count == 0)
    {
      plan.GlobalBlockers.Add(
        $"O changeset {request.Changeset} não contém itens de {request.Project.Name} em '{plan.SourceLocation.ServerPath}'. " +
        "Verifique a origem e o projeto escolhidos.");
      return;
    }

    if (scope.OtherProjects.Count > 0)
      plan.GlobalWarnings.Add($"{scope.OtherProjects.Count} item(ns) de outros projetos da origem ficarão FORA do merge.");
    if (scope.OutsideSource.Count > 0)
      plan.GlobalWarnings.Add($"{scope.OutsideSource.Count} item(ns) fora da versão de origem ficarão FORA do merge.");

    plan.GlobalWarnings.Add("Dependências de changesets anteriores não são incluídas automaticamente; conflitos só são conhecidos na execução real.");
  }

  private async Task EvaluateTargetAsync(MergePlan plan, MergeTargetPlan target, IOperationLog log, CancellationToken cancellationToken, bool revalidation = false)
  {
    var request = plan.Request;
    var location = target.Location;
    var name = location.Version.Id;
    var scope = plan.Scope!;

    // 1–2: pasta e mapeamento
    var (mapping, mappingQuery) = await _mapping.CheckAsync(location.LocalPath, location.ServerPath, name, log, cancellationToken);
    target.Mapping = mapping;
    if (mappingQuery is { IsEnvironmentError: true })
      plan.EnvironmentFailure = true;

    if (!mapping.IsValid)
    {
      target.Block($"{mapping.Message} {mapping.Guidance}".Trim());
      return;
    }

    if (mapping.CloakedChildren.Count > 0)
      target.Warnings.Add($"Subpastas cloaked no destino: {string.Join(", ", mapping.CloakedChildren)}.");

    target.IsLocalWorkspace = _mapping.IsLocalWorkspace(mapping);

    // 3: relação de merge e elegibilidade
    log.Step(name, "Verificando elegibilidade (candidatos)");
    var candidates = await _tfvc.GetMergeCandidatesAsync(plan.SourceLocation.ServerPath, location.ServerPath, request.Changeset, location.LocalPath, cancellationToken);
    log.ToolResult(name, "tf merge /candidate", candidates.Result.ExitCode, candidates.Result.StatusText, candidates.Result.Duration, candidates.Result.Output);

    if (candidates.Result.IsEnvironmentError)
    {
      plan.EnvironmentFailure = true;
      target.Block($"Falha de acesso ao TFVC: {candidates.Result.StatusText}. {TfErrorClassifier.Guidance(candidates.Result)}");
      return;
    }

    if (!candidates.Result.IsSuccess)
    {
      target.Block(
        $"Não foi possível confirmar relação de merge entre '{plan.SourceLocation.ServerPath}' e '{location.ServerPath}'. tf: {candidates.Result.Summary} " +
        "O PEP CLI não aplica merge baseless nem cópia de arquivos; verifique a hierarquia de branches no Source Control Explorer.");
      return;
    }

    if (!candidates.Changesets.Contains(request.Changeset))
    {
      target.Readiness = TargetReadiness.AlreadyIntegrated;
      target.Notes.Add($"O changeset {request.Changeset} não é candidato para este destino: já integrado ou sem alterações aplicáveis. Nada será reaplicado.");
      return;
    }

    var targetFiles = scope.TargetLocalFiles(plan.SourceLocation.ServerPath, location.LocalPath);
    var scopePaths = targetFiles
      .Concat(scope.TargetServerItems(plan.SourceLocation.ServerPath, location.ServerPath))
      .ToList();

    // 4: pending changes preexistentes
    log.Step(name, "Verificando pending changes");
    var pending = await _tfvc.GetPendingChangesAsync(location.LocalPath, cancellationToken);
    log.ToolResult(name, "tf status", pending.Result.ExitCode, pending.Result.StatusText, pending.Result.Duration, pending.Result.Output);
    if (!pending.Result.IsSuccess)
    {
      if (pending.Result.IsEnvironmentError)
        plan.EnvironmentFailure = true;
      target.Block($"Não foi possível listar pending changes do destino; estado local desconhecido. tf: {pending.Result.Summary}");
      return;
    }

    if (pending.HasUnmatchedItems)
    {
      target.Block(
        $"O tf status listou pending changes, mas nenhuma com o caminho '{location.LocalPath}' (subst, junction ou encoding). " +
        "Estado local indeterminado: revise 'pep pending list' e o mapeamento antes do merge.");
      return;
    }

    foreach (var line in pending.Items)
      (MergeOutcome.IsInScope(line, scopePaths) ? target.PendingInScope : target.PendingOutOfScope).Add(line);

    if (target.PendingInScope.Count > 0)
      target.Block($"{target.PendingInScope.Count} pending change(s) atingem arquivos do changeset. Revise, faça check-in ou desfaça manualmente antes do merge; nada foi alterado.");
    if (target.PendingOutOfScope.Count > 0)
      target.Notes.Add($"{target.PendingOutOfScope.Count} pending change(s) fora do escopo do merge serão preservadas e não contadas como resultado.");

    // 5: alterações não reconciliadas
    if (target.IsLocalWorkspace)
    {
      target.Warnings.Add("Workspace local: adições/exclusões não detectadas pelo TFVC podem não aparecer. Revise o destino no Visual Studio se houver dúvida.");
    }
    else
    {
      foreach (var file in targetFiles)
      {
        if (_fileSystem.FileExists(file) && !_fileSystem.IsReadOnly(file)
          && !pending.Items.Any(line => line.Contains(file, StringComparison.OrdinalIgnoreCase)))
          target.UnreconciledFiles.Add(file);
      }

      if (target.UnreconciledFiles.Count > 0)
        target.Block($"{target.UnreconciledFiles.Count} arquivo(s) do changeset estão graváveis sem checkout (possível alteração local não reconciliada). Nada será sobrescrito.");
    }

    // 6: conflitos existentes
    log.Step(name, "Verificando conflitos existentes");
    var conflicts = await _tfvc.GetConflictsAsync(location.LocalPath, cancellationToken);
    log.ToolResult(name, "tf resolve /preview", conflicts.Result.ExitCode, conflicts.Result.StatusText, conflicts.Result.Duration, conflicts.Result.Output);
    if (conflicts.Result.IsEnvironmentError)
    {
      plan.EnvironmentFailure = true;
      target.Block($"Falha de acesso ao TFVC ao consultar conflitos: {conflicts.Result.StatusText}.");
      return;
    }

    if (conflicts.Result.Status is TfStatus.Success or TfStatus.PartialSuccess)
    {
      foreach (var line in conflicts.Items)
        (MergeOutcome.IsInScope(line, scopePaths) ? target.ConflictsInScope : target.ConflictsOutOfScope).Add(line);
    }
    else
    {
      target.Warnings.Add($"Não foi possível listar conflitos existentes (tf: {conflicts.Result.Summary}).");
    }

    if (target.ConflictsInScope.Count > 0)
      target.Block($"{target.ConflictsInScope.Count} conflito(s) não resolvido(s) no escopo. Resolva no Visual Studio antes do merge.");
    if (target.ConflictsOutOfScope.Count > 0)
      target.Notes.Add($"{target.ConflictsOutOfScope.Count} conflito(s) fora do escopo existem no destino.");

    if (revalidation)
      return;

    // 7: atualização local
    log.Step(name, "Verificando atualização local (get /preview)");
    var getPreview = await _tfvc.PreviewGetAsync(location.LocalPath, cancellationToken);
    log.ToolResult(name, "tf get /preview", getPreview.Result.ExitCode, getPreview.Result.StatusText, getPreview.Result.Duration, getPreview.Result.Output);
    if (getPreview.Result.IsSuccess)
    {
      target.Outdated = getPreview.Items.Count > 0;
      if (target.Outdated == true)
        target.Warnings.Add("Destino desatualizado em relação ao servidor (critério: tf get /preview da pasta do projeto). Recomenda-se get antes do merge.");
    }
    else
    {
      target.Warnings.Add($"Não foi possível verificar a atualização local (tf: {getPreview.Result.Summary}).");
    }

    // 8: preview real do merge
    if (target.Readiness == TargetReadiness.Ready)
    {
      log.Step(name, "Executando preview do merge");
      var preview = await _tfvc.PreviewMergeAsync(plan.SourceLocation.ServerPath, location.ServerPath, request.Changeset, location.LocalPath, cancellationToken);
      log.ToolResult(name, "tf merge /preview", preview.Result.ExitCode, preview.Result.StatusText, preview.Result.Duration, preview.Result.Output);
      if (preview.Result.IsEnvironmentError)
      {
        plan.EnvironmentFailure = true;
        target.Block($"Falha de acesso ao TFVC no preview: {preview.Result.StatusText}.");
      }
      else if (preview.Result.Status == TfStatus.PartialSuccess)
      {
        // Evidência real (2026-09-13): exit 1 com "Conflito (mesclar, editar)" quando o arquivo também mudou no destino.
        // Conflito previsto não bloqueia: o merge segue e termina em "Aplicado com conflitos" para resolução manual.
        target.PreviewItemCount = preview.Items.Count;
        target.PredictedConflicts.AddRange(preview.Items);
        target.Warnings.Add(
          $"O preview prevê {Math.Max(preview.Items.Count, 1)} conflito(s). O merge ficará em Pending Changes com conflitos para você resolver " +
          "no Visual Studio (Resolve Conflicts); o PEP CLI não escolhe resolução.");
      }
      else if (!preview.Result.IsSuccess)
      {
        target.Block($"O preview do merge falhou. tf: {preview.Result.Summary}");
      }
      else
      {
        target.PreviewItemCount = preview.Items.Count;
      }
    }

    // 9: histórico local
    foreach (var previous in PreviousRuns(request, name))
      target.Notes.Add(previous);
  }

  private IEnumerable<string> PreviousRuns(MergeRequest request, string target)
  {
    if (_journal is null)
      yield break;

    IReadOnlyList<ExecutionRecord> records;
    try
    {
      records = _journal.List(200);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
      yield break;
    }

    foreach (var record in records.Where(r => r.Command == "merge" && r.Changeset == request.Changeset
      && string.Equals(r.Project, request.Project.Alias, StringComparison.OrdinalIgnoreCase)))
    {
      var outcome = record.Targets.FirstOrDefault(t => t.Target.Equals(target, StringComparison.OrdinalIgnoreCase));
      if (outcome is not null)
        yield return $"Execução anterior {record.Id} ({record.StartedAt:dd/MM HH:mm}): {outcome.State}.";
    }
  }

  private static void BlockAll(MergePlan plan, string reason)
  {
    foreach (var target in plan.Targets)
      target.Block(reason);
  }
}
