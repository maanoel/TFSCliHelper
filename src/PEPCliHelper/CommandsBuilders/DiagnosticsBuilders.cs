using PEPCliHelper.Core.Catalog;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.Diagnostics;
using PEPCliHelper.Core.Tfvc;
using PEPCliHelper.Infrastructure;
using PEPCliHelper.Presentation;
using Spectre.Console;

namespace PEPCliHelper.CommandsBuilders;

/// <summary>pep doctor (spec 011).</summary>
public sealed class DoctorBuilder : CommandBuilderBase
{
  public DoctorBuilder(AppServices services)
    : base(services)
  {
  }

  public override async Task<int> BuildAsync(CommandLine line, CancellationToken cancellationToken)
  {
    var history = BeginHistory("doctor", line);
    var checks = await Ui.WithStatusAsync("Diagnosticando ambiente", sink =>
      Services.DoctorService.RunAsync(new ConsoleOperationLog(sink, history, Ui.Theme), cancellationToken));
    var exit = DoctorService.ExitCode(checks);
    history.Complete(exit, ExitCodes.Describe(exit));

    if (Ui.Json)
    {
      Ui.WriteJson(new
      {
        codigoSaida = exit,
        verificacoes = checks.Select(c => new { area = c.Area, nome = c.Name, estado = c.Status.ToString(), detalhe = c.Detail, orientacao = c.Guidance }),
      });
      return exit;
    }

    Ui.Title("Diagnóstico (doctor)");
    foreach (var area in checks.GroupBy(c => c.Area))
    {
      Ui.Blank();
      Ui.Markup($"  [bold {Ui.Theme.Primary}]{Ui.Escape(area.Key)}[/]");
      var table = Ui.NewTable("Verificação", "Estado", "Detalhe");
      foreach (var check in area)
      {
        var kind = check.Status switch
        {
          CheckStatus.Ok => StateKind.Ok,
          CheckStatus.Warning => StateKind.Warn,
          CheckStatus.Fail => StateKind.Fail,
          _ => StateKind.Neutral,
        };
        var text = check.Status switch
        {
          CheckStatus.Ok => "OK",
          CheckStatus.Warning => "AVISO",
          CheckStatus.Fail => "FALHA",
          _ => "N/A",
        };
        table.AddRow(
          new Markup(Ui.Escape(check.Name)),
          new Markup(Ui.Theme.State(kind, text)),
          new Markup(Ui.Escape(check.Detail) + (check.Guidance is null ? string.Empty : $"\n[{Ui.Theme.Accent}]{Ui.Escape(Ui.Theme.Arrow)} {Ui.Escape(check.Guidance)}[/]")));
      }

      Ui.Write(table);
    }

    var fails = checks.Count(c => c.Status == CheckStatus.Fail);
    var warnings = checks.Count(c => c.Status == CheckStatus.Warning);
    Ui.Blank();
    if (fails == 0)
      Ui.Success($"Ambiente pronto ({warnings} aviso(s)).");
    else
      Ui.Fail($"{fails} falha(s) e {warnings} aviso(s). O doctor não corrige nada automaticamente; siga as orientações acima.");
    return exit;
  }
}

/// <summary>pep workspace list (spec 011).</summary>
public sealed class WorkspaceListBuilder : CommandBuilderBase
{
  public WorkspaceListBuilder(AppServices services)
    : base(services)
  {
  }

  public override async Task<int> BuildAsync(CommandLine line, CancellationToken cancellationToken)
  {
    var config = Services.RequireConfig();
    var collection = config.Collection ?? throw new UsageException("Coleção TFVC não configurada.", "Informe 'colecao' na configuração.");
    var result = await Ui.WithStatusAsync("Consultando workspaces", _ => Services.Tfvc.ListWorkspacesAsync(collection, cancellationToken));

    if (!result.IsSuccess)
    {
      throw new PepCliException($"Não foi possível listar workspaces: {result.StatusText}. {result.Summary}",
        result.IsEnvironmentError ? ExitCodes.Precondition : ExitCodes.OperationFailed, collection, "Nenhuma alteração.", "Nada foi alterado.",
        TfErrorClassifier.Guidance(result));
    }

    var lines = TfOutputParser.Lines(result.Output).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
    if (Ui.Json)
    {
      Ui.WriteJson(new { colecao = collection, linhas = lines });
      return ExitCodes.Success;
    }

    Ui.Title($"Workspaces · {collection}");
    Ui.Write(new Panel(new Text(string.Join(Environment.NewLine, lines))).Border(Ui.Theme.BoxBorder).BorderColor(Color.Grey));
    Ui.Muted("  Saída do tf.exe apresentada sem interpretação. Para diagnóstico de uma versão: pep workspace inspect <versao>");
    return ExitCodes.Success;
  }
}

/// <summary>pep workspace inspect &lt;versao&gt; (spec 011).</summary>
public sealed class WorkspaceInspectBuilder : CommandBuilderBase
{
  public WorkspaceInspectBuilder(AppServices services)
    : base(services)
  {
  }

  public override async Task<int> BuildAsync(CommandLine line, CancellationToken cancellationToken)
  {
    var catalog = Services.RequireCatalog();
    var version = ResolveVersion(catalog, RequireValue(line.Positional(0), "a versão", line), "");
    var project = OptionalProject(catalog, line.Option("project"));
    var mapping = new MappingService(Services.Tfvc, Services.FileSystem);

    var targets = new List<(string Label, string Local, string Server)> { ($"{version.Id} (raiz)", version.LocalRoot, version.ServerRoot) };
    targets.AddRange(catalog.LocateAll([version], project).Select(l => (l.Label, l.LocalPath, l.ServerPath)));

    var checks = await Ui.WithStatusAsync("Inspecionando mapeamentos", async sink =>
    {
      var list = new List<(string Label, MappingCheck Check, bool Local)>();
      foreach (var (label, local, server) in targets)
      {
        var (check, _) = await mapping.CheckAsync(local, server, label, NullOperationLog.Instance, cancellationToken);
        list.Add((label, check, check.IsValid && mapping.IsLocalWorkspace(check)));
        sink.Step(label);
      }

      return list;
    });

    var exit = checks.Skip(1).All(c => c.Check.IsValid) ? ExitCodes.Success : ExitCodes.Precondition;

    if (Ui.Json)
    {
      Ui.WriteJson(new { versao = version.Id, alvos = checks.Select(c => new { alvo = c.Label, workspaceLocal = c.Local, mapeamento = JsonViews.Mapping(c.Check) }) });
      return exit;
    }

    Ui.Title($"Workspace · {version.Label}");
    foreach (var (label, check, local) in checks)
    {
      var grid = Ui.NewKeyValueGrid();
      Ui.AddKeyValue(grid, "Estado", Ui.Theme.State(PlanRenderer.Kind(check.State), check.StateText));
      Ui.AddKeyValue(grid, "Pasta local", Ui.Escape(check.LocalPath));
      Ui.AddKeyValue(grid, "Servidor esperado", Ui.Escape(check.ExpectedServerPath ?? "—"));
      Ui.AddKeyValue(grid, "Servidor efetivo", Ui.Escape(check.EffectiveServerPath ?? "—"));
      Ui.AddKeyValue(grid, "Workspace", Ui.Escape(check.WorkspaceName is null ? "—" : $"{check.WorkspaceName} ({check.Owner}){(local ? " · local" : string.Empty)}"));
      Ui.AddKeyValue(grid, "Mapeado em", Ui.Escape(check.MappingLocalRoot ?? "—"));
      if (check.CloakedChildren.Count > 0)
        Ui.AddKeyValue(grid, "Subpastas cloaked", Ui.Escape(string.Join(", ", check.CloakedChildren)));
      Ui.AddKeyValue(grid, "Diagnóstico", Ui.Escape(check.Message));
      if (check.Guidance is not null)
        Ui.AddKeyValue(grid, "Como corrigir", $"[{Ui.Theme.Accent}]{Ui.Escape(check.Guidance)}[/]");
      Ui.Write(new Panel(grid).Header($" {Ui.Escape(label)} ").Border(Ui.Theme.BoxBorder).BorderColor(Color.Grey));
    }

    return exit;
  }
}

/// <summary>pep pending list [versao] (spec 011).</summary>
public sealed class PendingListBuilder : CommandBuilderBase
{
  public PendingListBuilder(AppServices services)
    : base(services)
  {
  }

  public override async Task<int> BuildAsync(CommandLine line, CancellationToken cancellationToken)
  {
    var catalog = Services.RequireCatalog();
    var project = OptionalProject(catalog, line.Option("project"));
    var versions = line.Positional(0) is { } token ? [ResolveVersion(catalog, token, "")] : catalog.Active;
    var locations = catalog.LocateAll(versions, project).Where(l => Services.FileSystem.DirectoryExists(l.LocalPath)).ToList();

    var results = await Ui.WithStatusAsync("Consultando pending changes", async sink =>
    {
      var list = new List<(ProjectLocation Location, ItemListQuery Query)>();
      foreach (var location in locations)
      {
        sink.Step(location.Label);
        var query = await Services.Tfvc.GetPendingChangesAsync(location.LocalPath, cancellationToken);
        list.Add((location, query));
        if (query.Result.IsEnvironmentError)
          break;
      }

      return list;
    });

    var failed = results.Any(r => !r.Query.Result.IsSuccess);
    var exit = failed ? ExitCodes.OperationFailed : ExitCodes.Success;

    if (Ui.Json)
    {
      Ui.WriteJson(new
      {
        alvos = results.Select(r => new
        {
          versao = r.Location.Version.Id,
          projeto = r.Location.Project.Alias,
          situacao = r.Query.Result.StatusText,
          pendencias = r.Query.Items,
        }),
      });
      return exit;
    }

    Ui.Title("Pending changes");
    foreach (var (location, query) in results)
    {
      if (!query.Result.IsSuccess)
      {
        Ui.Markup($"  {Ui.Theme.State(StateKind.Fail, location.Label)} {Ui.Escape(query.Result.Summary)}");
        continue;
      }

      Ui.Markup($"  {Ui.Theme.State(query.Items.Count > 0 ? StateKind.Pending : StateKind.Ok, location.Label)} {query.Items.Count} item(ns)");
      foreach (var item in query.Items.Take(50))
        Ui.Muted($"      {item}");
      if (query.Items.Count > 50)
        Ui.Muted($"      … e mais {query.Items.Count - 50}");
    }

    Ui.Muted("  Revise e faça o check-in pelo Visual Studio. O PEP CLI não executa check-in.");
    return exit;
  }
}

/// <summary>pep changeset show &lt;id&gt; (spec 011).</summary>
public sealed class ChangesetShowBuilder : CommandBuilderBase
{
  public ChangesetShowBuilder(AppServices services)
    : base(services)
  {
  }

  public override async Task<int> BuildAsync(CommandLine line, CancellationToken cancellationToken)
  {
    var config = Services.RequireConfig();
    var id = CommandLine.ParsePositiveInt(RequireValue(line.Positional(0), "o número do changeset", line), "changeset");
    var collection = config.Collection ?? throw new UsageException("Coleção TFVC não configurada.", "Informe 'colecao' na configuração.");

    ChangesetScope? scope = null;
    var query = await Ui.WithStatusAsync($"Consultando changeset {id}", _ => Services.Tfvc.GetChangesetAsync(id, collection, cancellationToken));
    if (query.Changeset is null)
    {
      throw new PepCliException($"Changeset {id} não encontrado ou inacessível: {query.Result.StatusText}. {query.Result.Summary}",
        query.Result.IsEnvironmentError ? ExitCodes.Precondition : ExitCodes.OperationFailed, collection, "Nenhuma alteração.", "Nada foi alterado.",
        TfErrorClassifier.Guidance(query.Result));
    }

    if (line.Option("project") is not null || line.Option("source") is not null)
    {
      var catalog = Services.RequireCatalog();
      var project = ResolveProject(catalog, RequireValue(line.Option("project"), "--project junto de --source", line));
      var source = ResolveVersion(catalog, RequireValue(line.Option("source"), "--source junto de --project", line), "de origem");
      var location = catalog.Locate(source, project);
      scope = ChangesetScope.Analyze(query.Changeset, location.ServerPath, source.ServerRoot);
    }

    if (Ui.Json)
    {
      Ui.WriteJson(new
      {
        changeset = id,
        cabecalho = query.Changeset.Header,
        itens = query.Changeset.Items,
        escopo = scope is null ? null : new { incluidos = scope.Included, outrosProjetos = scope.OtherProjects, foraDaOrigem = scope.OutsideSource },
      });
      return ExitCodes.Success;
    }

    Ui.Title($"Changeset C{id}");
    foreach (var header in query.Changeset.Header)
      Ui.Muted("  " + header);

    Ui.Blank();
    if (scope is null)
    {
      foreach (var item in query.Changeset.Items)
        Ui.Bullet(item);
    }
    else
    {
      foreach (var item in scope.Included)
        Ui.Markup($"  {Ui.Theme.State(StateKind.Ok, "incluído")} {Ui.Escape(item)}");
      foreach (var item in scope.Excluded)
        Ui.Markup($"  {Ui.Theme.State(StateKind.Warn, "excluído")} {Ui.Escape(item)}");
    }

    Ui.Muted($"  {query.Changeset.Items.Count} item(ns).");
    return ExitCodes.Success;
  }
}
