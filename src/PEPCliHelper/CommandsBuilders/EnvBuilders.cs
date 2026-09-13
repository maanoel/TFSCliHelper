using PEPCliHelper.Core.Catalog;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.Configuration;
using PEPCliHelper.Core.Environments;
using PEPCliHelper.Core.Tfvc;
using PEPCliHelper.Infrastructure;
using PEPCliHelper.Presentation;
using Spectre.Console;

namespace PEPCliHelper.CommandsBuilders;

/// <summary>pep env list (spec 004).</summary>
public sealed class EnvListBuilder : CommandBuilderBase
{
  public EnvListBuilder(AppServices services)
    : base(services)
  {
  }

  public override Task<int> BuildAsync(CommandLine line, CancellationToken cancellationToken)
  {
    var config = Services.RequireConfig();
    var catalog = VersionCatalog.FromConfig(config);

    if (Ui.Json)
    {
      Ui.WriteJson(new
      {
        atual = catalog.Current?.Id,
        legadasAtivas = catalog.ActiveLegacy.Select(v => v.Id),
        limiteLegadas = ConfigValidator.MaxActiveLegacy,
        versoes = catalog.All.Select(v => new
        {
          id = v.Id,
          atual = v.IsCurrent,
          ativa = v.Active,
          aliases = v.Aliases,
          caminhoLocal = v.LocalRoot,
          caminhoServidor = v.ServerRoot,
          pastaExiste = Services.FileSystem.DirectoryExists(v.LocalRoot),
        }),
      });
      return Task.FromResult(ExitCodes.Success);
    }

    Ui.Title("Ambientes e versões");
    if (catalog.All.Count == 0)
    {
      Ui.Warn("Nenhuma versão cadastrada.");
      Ui.Hint("pep env discover  →  pep env configure");
      return Task.FromResult(ExitCodes.Success);
    }

    var table = Ui.NewTable("Versão", "Tipo", "Situação", "Aliases", "Pasta local", "Caminho TFVC");
    foreach (var v in catalog.Active.Concat(catalog.All.Where(x => !x.Active)))
    {
      var exists = Services.FileSystem.DirectoryExists(v.LocalRoot);
      table.AddRow(
        new Markup($"[bold]{Ui.Escape(v.Id)}[/]"),
        new Markup(v.IsCurrent ? $"[{Ui.Theme.Accent}]atual[/]" : "legada"),
        new Markup(v.Active ? Ui.Theme.State(StateKind.Ok, "ativa") : Ui.Theme.State(StateKind.Neutral, "desativada")),
        new Markup(Ui.Escape(string.Join(", ", v.Aliases))),
        new Markup(exists ? Ui.Escape(v.LocalRoot) : Ui.Theme.State(StateKind.Fail, $"{v.LocalRoot} (inexistente)")),
        new Markup(Ui.Escape(v.ServerRoot)));
    }

    Ui.Write(table);
    Ui.Muted($"  Legadas ativas: {catalog.ActiveLegacy.Count} de {ConfigValidator.MaxActiveLegacy}. Operações 'all' usam somente versões ativas.");
    return Task.FromResult(ExitCodes.Success);
  }
}

/// <summary>pep env discover (spec 004): somente leitura.</summary>
public sealed class EnvDiscoverBuilder : CommandBuilderBase
{
  public EnvDiscoverBuilder(AppServices services)
    : base(services)
  {
  }

  public override async Task<int> BuildAsync(CommandLine line, CancellationToken cancellationToken)
  {
    var config = Services.RequireConfig();
    var discovered = await DiscoverAsync(Services, config, line.Flag("offline"), cancellationToken);

    if (Ui.Json)
    {
      Ui.WriteJson(new
      {
        somenteLeitura = true,
        candidatos = discovered.Select(d => new
        {
          pasta = d.Folder,
          idSugerido = d.SuggestedId,
          pareceAtual = d.LooksCurrent,
          projetos = d.ProjectsFound,
          caminhoServidor = d.ServerRoot,
          mapeamento = d.Mapping?.ToString(),
          noCatalogo = d.CatalogId,
          ativaNoCatalogo = d.CatalogActive,
          observacoes = d.Notes,
        }),
      });
      return ExitCodes.Success;
    }

    Render(Ui, discovered);
    var legacy = discovered.Count(d => !d.LooksCurrent);
    if (legacy > ConfigValidator.MaxActiveLegacy)
      Ui.Warn($"{legacy} pastas legadas encontradas; o catálogo ativo aceita no máximo {ConfigValidator.MaxActiveLegacy}. Escolha as ativas em 'pep env configure'.");
    Ui.Muted("  Descoberta somente leitura: nenhum get, merge, exclusão, encerramento de processo ou alteração de workspace foi executado.");
    Ui.Hint("Para gravar o catálogo: pep env configure");
    return ExitCodes.Success;
  }

  internal static Task<IReadOnlyList<DiscoveredVersion>> DiscoverAsync(AppServices services, PepConfig config, bool offline, CancellationToken cancellationToken)
  {
    var history = services.Journal.Begin("env discover", []);
    return services.Ui.WithStatusAsync("Procurando versões em " + config.LocalRoot, async sink =>
    {
      var result = await services.DiscoveryService.DiscoverAsync(config, !offline, new ConsoleOperationLog(sink, history, services.Ui.Theme), cancellationToken);
      history.Complete(ExitCodes.Success, "concluido");
      return result;
    });
  }

  internal static void Render(Ui ui, IReadOnlyList<DiscoveredVersion> discovered)
  {
    ui.Title("Versões encontradas (candidatas)");
    if (discovered.Count == 0)
    {
      ui.Warn("Nenhuma pasta com projetos configurados foi encontrada em <raiz>\\Atual e <raiz>\\Legado.");
      return;
    }

    var table = ui.NewTable("Pasta", "Id sugerido", "Projetos", "Mapeamento", "Caminho TFVC", "Catálogo", "Observações");
    foreach (var d in discovered)
    {
      table.AddRow(
        new Markup(Ui.Escape(d.Folder)),
        new Markup($"[bold]{Ui.Escape(d.SuggestedId)}[/]{(d.LooksCurrent ? $" [{ui.Theme.Accent}](atual?)[/]" : string.Empty)}"),
        new Markup(Ui.Escape(string.Join(", ", d.ProjectsFound))),
        new Markup(d.Mapping is null ? "—" : ui.Theme.State(PlanRenderer.Kind(d.Mapping.Value), MappingCheck.Describe(d.Mapping.Value))),
        new Markup(Ui.Escape(d.ServerRoot ?? "—")),
        new Markup(d.CatalogId is null ? "—" : ui.Theme.State(d.CatalogActive ? StateKind.Ok : StateKind.Neutral, d.CatalogActive ? "ativa" : "inativa")),
        new Markup(Ui.Escape(string.Join(" ", d.Notes))));
    }

    ui.Write(table);
  }
}

/// <summary>pep env configure (spec 004): rotação do catálogo com revisão e confirmação.</summary>
public sealed class EnvConfigureBuilder : CommandBuilderBase
{
  public EnvConfigureBuilder(AppServices services)
    : base(services)
  {
  }

  public override async Task<int> BuildAsync(CommandLine line, CancellationToken cancellationToken)
  {
    var config = Services.RequireConfig();
    var discovered = await EnvDiscoverBuilder.DiscoverAsync(Services, config, line.Flag("offline"), cancellationToken);
    var options = BuildOptions(config, discovered);

    CatalogChoice current;
    IReadOnlyList<CatalogChoice> legacy;

    if (line.Option("atual") is { } currentToken)
    {
      current = FindCurrentOption(options, discovered, currentToken);
      legacy = line.OptionValues("legado").Select(t => FindOption(options, t)).ToList();
    }
    else
    {
      if (!Ui.CanPrompt)
        throw new UsageException("Informe a versão atual com --atual e as legadas com --legado (repetível).", "Ex.: pep env configure --atual 12.1.2610 --legado 12.1.2606 --legado 12.1.2602 --yes");

      EnvDiscoverBuilder.Render(Ui, discovered);
      var currentOption = await Ui.SelectOrCancelAsync("Qual pasta é a versão ATUAL?", options, o => $"{o.Label}", cancellationToken);
      var currentId = await Ui.AskAsync("Identificador da versão atual (ex.: 12.1.2610)", currentOption.Choice.Id, ValidateId, cancellationToken);
      current = await CompleteServerRootAsync(currentOption.Choice with { Id = currentId }, cancellationToken);

      var legacyOptions = options.Where(o => !LocalPath.AreEqual(o.Choice.LocalFolder, current.LocalFolder)).ToList();
      var selected = await Ui.MultiSelectAsync($"Quais legadas ficam ATIVAS? (máximo {ConfigValidator.MaxActiveLegacy})", legacyOptions, o => o.Label, cancellationToken);
      if (selected.Count > ConfigValidator.MaxActiveLegacy)
        throw new UsageException($"Foram selecionadas {selected.Count} legadas; o máximo é {ConfigValidator.MaxActiveLegacy}. Nada foi gravado.", "Execute novamente e selecione até 4.");

      var completed = new List<CatalogChoice>();
      foreach (var option in selected)
        completed.Add(await CompleteServerRootAsync(option.Choice, cancellationToken));
      legacy = completed;
    }

    var missingServer = new[] { current }.Concat(legacy).Where(c => !TfvcPath.IsServerPath(c.ServerRoot)).Select(c => c.Id).ToList();
    if (missingServer.Count > 0)
      throw new PreconditionException($"Caminho TFVC desconhecido para: {string.Join(", ", missingServer)}.", "Mapeie a pasta no TFVC e rode 'pep env discover', ou use o modo interativo para informar o caminho.");

    var (updated, errors) = CatalogEditor.Apply(config, current, legacy);
    if (errors.Count > 0)
      throw new UsageException("O catálogo resultante é inválido: " + string.Join(" | ", errors), "Nada foi gravado. Ajuste a seleção e tente novamente.");

    Render(updated, config);
    Ui.Muted("  Somente o catálogo é alterado: nenhuma pasta é movida ou excluída, nenhum branch ou workspace é modificado.");

    if (!await ConfirmAsync($"Gravar o catálogo em {Services.ConfigStore.Path}?", cancellationToken))
    {
      Ui.Warn("Nada foi gravado.");
      return ExitCodes.Cancelled;
    }

    var backup = Services.ConfigStore.Save(updated);
    Services.InvalidateConfig();

    if (Ui.Json)
      Ui.WriteJson(new { gravado = true, arquivo = Services.ConfigStore.Path, backup, atual = current.Id, legadas = legacy.Select(l => l.Id) });
    else
    {
      Ui.Success("Catálogo gravado.");
      if (backup is not null)
        Ui.Muted($"  Backup: {backup}");
      Ui.Hint("Valide os mapeamentos: pep env validate");
    }

    return ExitCodes.Success;
  }

  private void Render(PepConfig updated, PepConfig previous)
  {
    Ui.Title("Catálogo revisado");
    var table = Ui.NewTable("Versão", "Tipo", "Situação", "Antes", "Local", "Servidor");
    foreach (var v in updated.Versions.OrderByDescending(v => v.IsCurrent).ThenByDescending(v => v.Active))
    {
      var before = previous.Versions.FirstOrDefault(p => p.Id.Equals(v.Id, StringComparison.OrdinalIgnoreCase));
      var beforeText = before is null ? "nova" : before.IsCurrent ? "atual" : before.Active ? "legada ativa" : "desativada";
      table.AddRow(
        new Markup($"[bold]{Ui.Escape(v.Id)}[/]"),
        new Markup(v.IsCurrent ? $"[{Ui.Theme.Accent}]atual[/]" : "legada"),
        new Markup(v.Active ? Ui.Theme.State(StateKind.Ok, "ativa") : Ui.Theme.State(StateKind.Neutral, "desativada")),
        new Markup(Ui.Escape(beforeText)),
        new Markup(Ui.Escape(v.LocalPath)),
        new Markup(Ui.Escape(v.ServerPath)));
    }

    Ui.Write(table);
  }

  private async Task<CatalogChoice> CompleteServerRootAsync(CatalogChoice choice, CancellationToken cancellationToken)
  {
    if (TfvcPath.IsServerPath(choice.ServerRoot) || !Ui.CanPrompt)
      return choice;

    Ui.Warn($"O caminho TFVC de '{choice.LocalFolder}' não foi confirmado pelo mapeamento.");
    var server = await Ui.AskAsync($"Caminho TFVC da versão {choice.Id} (ex.: $/Linha-RM/Legado/{choice.Id})", null,
      v => TfvcPath.IsServerPath(v) ? null : "Informe um caminho iniciando com $/", cancellationToken);
    return choice with { ServerRoot = server };
  }

  /// <summary>Id novo para a atual (rotação): usa a única pasta candidata em &lt;raiz&gt;\Atual, sem adivinhar entre várias.</summary>
  private static CatalogChoice FindCurrentOption(IReadOnlyList<ConfigureOption> options, IReadOnlyList<DiscoveredVersion> discovered, string token)
  {
    if (options.Any(o => o.Choice.Id.Equals(token, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(o.Choice.LocalFolder).Equals(token, StringComparison.OrdinalIgnoreCase)))
      return FindOption(options, token);

    var currentFolders = discovered.Where(d => d.LooksCurrent).ToList();
    if (currentFolders.Count != 1)
    {
      throw new UsageException(
        $"'{token}' não corresponde a uma candidata e há {currentFolders.Count} pasta(s) em <raiz>\\Atual.",
        "Use o modo interativo 'pep env configure' para escolher a pasta da atual.");
    }

    var option = options.First(o => LocalPath.AreEqual(o.Choice.LocalFolder, currentFolders[0].Folder));
    return option.Choice with { Id = token };
  }

  private static CatalogChoice FindOption(IReadOnlyList<ConfigureOption> options, string token)
  {
    var match = options.Where(o => o.Choice.Id.Equals(token, StringComparison.OrdinalIgnoreCase)
      || Path.GetFileName(o.Choice.LocalFolder).Equals(token, StringComparison.OrdinalIgnoreCase)).ToList();
    return match.Count switch
    {
      1 => match[0].Choice,
      0 => throw new UsageException($"Versão/pasta não encontrada entre as candidatas: '{token}'.", "Veja os ids com 'pep env discover'."),
      _ => throw new UsageException($"'{token}' é ambíguo entre as candidatas.", "Use o id completo."),
    };
  }

  private static IReadOnlyList<ConfigureOption> BuildOptions(PepConfig config, IReadOnlyList<DiscoveredVersion> discovered)
  {
    var catalog = VersionCatalog.FromConfig(config);
    var options = discovered.Select(d =>
    {
      var existing = catalog.All.FirstOrDefault(v => LocalPath.AreEqual(v.LocalRoot, d.Folder));
      var choice = new CatalogChoice(existing?.Id ?? d.SuggestedId, d.Folder, existing?.ServerRoot ?? d.ServerRoot ?? string.Empty);
      var mapping = d.Mapping is null ? "não consultado" : MappingCheck.Describe(d.Mapping.Value);
      return new ConfigureOption(choice, $"{choice.Id}  ·  {d.Folder}  ·  {mapping}{(existing is null ? string.Empty : " · no catálogo")}");
    }).ToList();

    foreach (var version in catalog.All.Where(v => options.All(o => !LocalPath.AreEqual(o.Choice.LocalFolder, v.LocalRoot))))
      options.Add(new ConfigureOption(new CatalogChoice(version.Id, version.LocalRoot, version.ServerRoot), $"{version.Id}  ·  {version.LocalRoot}  ·  no catálogo"));

    return options;
  }

  private static string? ValidateId(string value) =>
    string.IsNullOrWhiteSpace(value) || value.Contains(' ') ? "Informe um identificador sem espaços." : null;

  private sealed record ConfigureOption(CatalogChoice Choice, string Label);
}

/// <summary>pep env validate (spec 004): pastas e mapeamentos do catálogo ativo.</summary>
public sealed class EnvValidateBuilder : CommandBuilderBase
{
  public EnvValidateBuilder(AppServices services)
    : base(services)
  {
  }

  public override async Task<int> BuildAsync(CommandLine line, CancellationToken cancellationToken)
  {
    var catalog = Services.RequireCatalog();
    var mapping = new MappingService(Services.Tfvc, Services.FileSystem);
    var history = BeginHistory("env validate", line);

    var checks = await Ui.WithStatusAsync("Validando mapeamentos", async sink =>
    {
      var log = new ConsoleOperationLog(sink, history, Ui.Theme);
      var list = new List<(ProjectLocation Location, MappingCheck Check)>();
      foreach (var location in catalog.LocateAll(catalog.Active))
      {
        var (check, query) = await mapping.CheckAsync(location.LocalPath, location.ServerPath, location.Label, log, cancellationToken);
        list.Add((location, check));
        if (query is { IsEnvironmentError: true })
          break;
      }

      return list;
    });

    var exit = checks.All(c => c.Check.IsValid) && checks.Count == catalog.LocateAll(catalog.Active).Count ? ExitCodes.Success : ExitCodes.Precondition;
    history.Complete(exit, ExitCodes.Describe(exit));

    if (Ui.Json)
    {
      Ui.WriteJson(new { valido = exit == 0, alvos = checks.Select(c => new { versao = c.Location.Version.Id, projeto = c.Location.Project.Alias, mapeamento = JsonViews.Mapping(c.Check) }) });
      return exit;
    }

    Ui.Title("Validação de mapeamentos");
    var table = Ui.NewTable("Alvo", "Estado", "Workspace", "Servidor efetivo", "Detalhe");
    foreach (var (location, check) in checks)
    {
      table.AddRow(
        new Markup($"[bold]{Ui.Escape(location.Label)}[/]"),
        new Markup(Ui.Theme.State(PlanRenderer.Kind(check.State), check.StateText)),
        new Markup(Ui.Escape(check.WorkspaceName ?? "—")),
        new Markup(Ui.Escape(check.EffectiveServerPath ?? "—")),
        new Markup(Ui.Escape(check.IsValid ? check.Message : $"{check.Message} {check.Guidance}")));
    }

    Ui.Write(table);
    if (exit != 0)
      Ui.Hint("Após corrigir o mapeamento no Visual Studio, execute 'pep env validate' novamente.");
    return exit;
  }
}
