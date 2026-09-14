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
      Ui.Hint("pep config auto     detecta a atual e as legadas pelas pastas");
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
      Ui.Warn($"{legacy} pastas legadas encontradas; o catálogo ativo aceita no máximo {ConfigValidator.MaxActiveLegacy}. 'pep config auto' ativa as 4 mais novas.");
    Ui.Muted("  Descoberta somente leitura: nenhum get, merge, exclusão, encerramento de processo ou alteração de workspace foi executado.");
    Ui.Hint("Para gravar o catálogo: pep config auto");
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
