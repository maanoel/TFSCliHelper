using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.History;
using PEPCliHelper.Core.LocalTools;
using PEPCliHelper.Infrastructure;
using PEPCliHelper.Presentation;
using Spectre.Console;

namespace PEPCliHelper.CommandsBuilders;

/// <summary>pep delete broker &lt;versao&gt; (spec 010).</summary>
public sealed class DeleteBrokerBuilder : CommandBuilderBase
{
  public DeleteBrokerBuilder(AppServices services)
    : base(services)
  {
  }

  public override async Task<int> BuildAsync(CommandLine line, CancellationToken cancellationToken)
  {
    var config = Services.RequireConfig();
    var catalog = Services.RequireCatalog();
    var version = ResolveVersion(catalog, RequireValue(line.Positional(0), "a versão (ex.: pep delete broker 2606)", line), "");
    var tools = Services.LocalTools;
    var plan = tools.PlanBrokerDelete(version, config.LocalFiles);
    var backup = !line.Flag("no-backup");

    Ui.Title($"Remover broker · {version.Label}");
    Ui.Muted($"  Arquivo: {plan.Path}");

    if (!plan.Exists && plan.Blockers.Count == 0)
    {
      if (Ui.Json)
        Ui.WriteJson(new { arquivo = plan.Path, existe = false, acao = "nenhuma" });
      else
        Ui.Info("O arquivo não existe. Nenhuma ação necessária.");
      return ExitCodes.Success;
    }

    if (plan.Blockers.Count > 0)
    {
      throw new PreconditionException(string.Join(" ", plan.Blockers), "Resolva o impedimento e execute novamente.", plan.Path);
    }

    Ui.Warn("Impacto: somente este arquivo de broker é removido; nenhuma outra pasta ou arquivo da versão é alterado.");
    Ui.Muted(backup ? "  Um backup será criado antes da remoção." : "  --no-backup: nenhum backup será criado.");

    if (!await ConfirmAsync($"Remover '{plan.Path}'?", cancellationToken))
    {
      Ui.Warn("Remoção cancelada. Nada foi alterado.");
      return ExitCodes.Cancelled;
    }

    // A situação pode ter mudado enquanto a confirmação estava aberta.
    plan = tools.PlanBrokerDelete(version, config.LocalFiles);
    if (!plan.IsReady)
    {
      throw new PreconditionException(
        plan.Exists ? "A situação mudou após a confirmação: " + string.Join(" ", plan.Blockers) : "O arquivo não existe mais. Nenhuma ação executada.",
        "Verifique e execute novamente.", plan.Path);
    }

    var history = BeginHistory("delete broker", line);
    history.Describe(versions: [version.Id]);
    try
    {
      var backupPath = tools.DeleteBroker(plan, backup);
      history.Step(version.Id, backupPath is null ? "Broker removido sem backup" : $"Backup em {backupPath}; broker removido");
      history.Complete(ExitCodes.Success, "concluido");

      if (Ui.Json)
        Ui.WriteJson(new { arquivo = plan.Path, removido = true, backup = backupPath });
      else
      {
        Ui.Success($"Removido: {plan.Path}");
        if (backupPath is not null)
          Ui.Muted($"  Backup: {backupPath}");
      }

      return ExitCodes.Success;
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
      history.Complete(ExitCodes.OperationFailed, "falhou");
      throw new PepCliException($"Falha ao remover o broker: {ex.Message}", ExitCodes.OperationFailed, plan.Path,
        "O broker pode não ter sido removido.", "Verifique se o arquivo ainda existe e se o backup foi criado.",
        "Feche o RM/Host da versão, confirme permissões e tente novamente.");
    }
  }
}

/// <summary>pep open host|rm|alias|hostconfig &lt;versao&gt; (spec 010).</summary>
public sealed class OpenBuilder : CommandBuilderBase
{
  private readonly LocalFileKind _kind;

  public OpenBuilder(AppServices services, LocalFileKind kind)
    : base(services)
  {
    _kind = kind;
  }

  public override async Task<int> BuildAsync(CommandLine line, CancellationToken cancellationToken)
  {
    var config = Services.RequireConfig();
    var catalog = Services.RequireCatalog();
    var version = ResolveVersion(catalog, RequireValue(line.Positional(0), "a versão", line), "");
    var path = LocalToolsService.ResolveVersionFile(version, config.LocalFiles, _kind);

    if (!Services.FileSystem.FileExists(path))
    {
      throw new PreconditionException($"Arquivo não encontrado: {path}",
        "Confirme 'arquivosLocais' na configuração e se a versão foi compilada/atualizada.", path);
    }

    var directory = Path.GetDirectoryName(path);
    switch (_kind)
    {
      case LocalFileKind.Host:
        var running = Services.LocalTools.RunningFrom(version, LocalToolsService.HostProcessName)
          .Where(p => p.Path is not null && LocalPath.AreEqual(p.Path, path))
          .ToList();
        if (running.Count > 0 && !line.Flag("new-instance"))
        {
          var pids = string.Join(", ", running.Select(p => p.Pid));
          if (!Ui.CanPrompt)
            throw new PreconditionException($"RM.Host.exe desta versão já está em execução (PID {pids}).", "Use --new-instance para abrir outra instância ou 'pep kill host' para encerrar.");
          if (!await Ui.ConfirmAsync($"RM.Host.exe já está em execução (PID {pids}). Abrir outra instância?", false, cancellationToken))
          {
            Ui.Warn("Nada foi aberto.");
            return ExitCodes.Cancelled;
          }
        }

        Services.Processes.Start(path, null, directory);
        break;

      case LocalFileKind.Rm:
        Services.Processes.Start(path, null, directory);
        break;

      default:
        Services.Processes.Start(config.Tools.Editor, $"\"{path}\"", directory);
        break;
    }

    if (Ui.Json)
      Ui.WriteJson(new { aberto = path, versao = version.Id });
    else
      Ui.Success($"Aberto: {path}");
    return ExitCodes.Success;
  }
}

/// <summary>pep kill host (spec 010): exige alvo explícito, gracioso por padrão.</summary>
public sealed class KillHostBuilder : CommandBuilderBase
{
  public KillHostBuilder(AppServices services)
    : base(services)
  {
  }

  public override async Task<int> BuildAsync(CommandLine line, CancellationToken cancellationToken)
  {
    var config = Services.RequireConfig();
    var catalog = Services.RequireCatalog();
    var tools = Services.LocalTools;
    var candidates = tools.ListHosts(catalog, config.LocalRoot);
    var force = line.Flag("force");

    if (candidates.Count == 0)
    {
      if (Ui.Json)
        Ui.WriteJson(new { candidatos = Array.Empty<object>(), encerrados = Array.Empty<object>() });
      else
        Ui.Info("Nenhum processo RM.Host em execução.");
      return ExitCodes.Success;
    }

    if (!Ui.Json)
    {
      Ui.Title("Processos RM.Host");
      var table = Ui.NewTable("PID", "Versão inferida", "Confiança", "Início", "Caminho");
      foreach (var c in candidates)
      {
        table.AddRow(
          c.Process.Pid.ToString(),
          Ui.Escape(c.VersionLabel),
          Ui.Escape(c.Confidence.ToString().ToLowerInvariant()),
          c.Process.StartTime?.ToString("dd/MM HH:mm") ?? "—",
          Ui.Escape(c.Process.Path ?? "(inacessível)"));
      }

      Ui.Write(table);
    }

    var (selected, chosenInList) = await SelectAsync(line, catalog, candidates, cancellationToken);
    if (selected.Count == 0)
    {
      Ui.Warn("Nenhum processo selecionado. Nada foi encerrado.");
      return ExitCodes.Cancelled;
    }

    if (force)
    {
      Ui.Warn("Encerramento FORÇADO: o processo termina sem salvar estado e operações em andamento são perdidas.");
      if (!Services.Options.Yes || Ui.CanPrompt)
      {
        if (!Ui.CanPrompt)
          throw new UsageException("Encerramento forçado exige confirmação.", "Em modo não interativo use --force --yes.");
        if (!await Ui.ConfirmAsync($"Confirmar encerramento FORÇADO de {selected.Count} processo(s)?", false, cancellationToken))
          return ExitCodes.Cancelled;
      }
    }
    else if (!chosenInList && !await ConfirmAsync($"Encerrar de forma graciosa {selected.Count} processo(s)?", cancellationToken))
    {
      // Seleção na lista (Espaço + Enter) já é a confirmação; --pid/--version ainda confirmam.
      return ExitCodes.Cancelled;
    }

    var history = BeginHistory("kill host", line);
    var results = selected.Select(c => tools.Terminate(c, force)).ToList();
    foreach (var result in results)
      history.Step($"PID {result.Candidate.Process.Pid}", result.Message);

    var exit = results.All(r => r.Terminated) ? ExitCodes.Success
      : results.Any(r => r.Terminated) ? ExitCodes.ConflictOrPartial
      : ExitCodes.OperationFailed;
    history.Complete(exit, ExitCodes.Describe(exit),
      results.Select(r => new TargetOutcome { Target = $"PID {r.Candidate.Process.Pid}", State = r.Terminated ? "encerrado" : "ativo", Message = r.Message }));

    if (Ui.Json)
    {
      Ui.WriteJson(new
      {
        forcado = force,
        resultados = results.Select(r => new { pid = r.Candidate.Process.Pid, versao = r.Candidate.Version?.Id, encerrado = r.Terminated, mensagem = r.Message }),
        codigoSaida = exit,
      });
    }
    else
    {
      foreach (var result in results)
        Ui.Markup($"  {Ui.Theme.State(result.Terminated ? StateKind.Ok : StateKind.Warn, $"PID {result.Candidate.Process.Pid}")} {Ui.Escape(result.Message)}");
    }

    return exit;
  }

  /// <returns>Processos escolhidos e se vieram da lista interativa (a própria seleção confirma).</returns>
  private async Task<(IReadOnlyList<HostCandidate> Selected, bool ChosenInList)> SelectAsync(CommandLine line, Core.Catalog.VersionCatalog catalog, IReadOnlyList<HostCandidate> candidates, CancellationToken cancellationToken)
  {
    var pids = line.OptionValues("pid").Select(p => CommandLine.ParsePositiveInt(p, "--pid")).ToHashSet();
    var versionToken = line.Option("version");

    if (pids.Count > 0 || versionToken is not null)
    {
      var version = versionToken is null ? null : ResolveVersion(catalog, versionToken, "");
      var chosen = candidates
        .Where(c => (pids.Count == 0 || pids.Contains(c.Process.Pid))
          && (version is null || (c.Version?.Id.Equals(version.Id, StringComparison.OrdinalIgnoreCase) == true && c.Confidence == HostConfidence.Alta)))
        .ToList();

      var missing = pids.Where(p => candidates.All(c => c.Process.Pid != p)).ToList();
      if (missing.Count > 0)
        throw new PreconditionException($"Processo não localizado entre os RM.Host: PID {string.Join(", ", missing)}.", "Execute 'pep kill host' para listar os candidatos.");
      if (chosen.Count == 0)
        throw new PreconditionException("Nenhum RM.Host corresponde ao alvo informado com confiança alta.", "Use --pid com um dos PIDs listados.");
      return (chosen, false);
    }

    if (!Ui.CanPrompt)
    {
      throw new UsageException(
        $"{candidates.Count} processo(s) RM.Host encontrado(s): informe o alvo explicitamente.",
        "Use --pid <n> (repetível) ou --version <versao>. O PEP CLI não encerra processos apenas pelo nome.");
    }

    var picked = await Ui.MultiSelectAsync("Quais processos encerrar? (Espaço marca, Enter encerra)", candidates,
      c => $"PID {c.Process.Pid} · {c.VersionLabel} ({c.Confidence.ToString().ToLowerInvariant()}) · {c.Process.Path ?? "caminho inacessível"}", cancellationToken);
    return (picked, true);
  }
}
