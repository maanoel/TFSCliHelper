using PEPCliHelper.Core.Catalog;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.Configuration;
using PEPCliHelper.Infrastructure;
using PEPCliHelper.Presentation;
using Spectre.Console;

namespace PEPCliHelper.CommandsBuilders;

/// <summary>pep history list (spec 012).</summary>
public sealed class HistoryListBuilder : CommandBuilderBase
{
  public HistoryListBuilder(AppServices services)
    : base(services)
  {
  }

  public override Task<int> BuildAsync(CommandLine line, CancellationToken cancellationToken)
  {
    var limit = line.IntOption("limit") ?? 20;
    var records = Services.Journal.List(limit);

    if (Ui.Json)
    {
      Ui.WriteJson(new
      {
        diretorio = Services.Journal.Directory,
        execucoes = records.Select(r => new { id = r.Id, comando = r.Command, inicio = r.StartedAt, situacao = r.Status, codigoSaida = r.ExitCode, duracaoSegundos = r.DurationSeconds, versoes = r.Versions }),
      });
      return Task.FromResult(ExitCodes.Success);
    }

    Ui.Title("Histórico local do PEP CLI");
    if (records.Count == 0)
    {
      Ui.Info("Nenhuma execução registrada.");
      return Task.FromResult(ExitCodes.Success);
    }

    var table = Ui.NewTable("Id", "Início", "Comando", "Versões", "Situação", "Código", "Duração");
    foreach (var record in records)
    {
      var kind = record.ExitCode switch
      {
        0 => StateKind.Ok,
        null => StateKind.Pending,
        ExitCodes.ConflictOrPartial or ExitCodes.Precondition => StateKind.Warn,
        ExitCodes.Cancelled => StateKind.Neutral,
        _ => StateKind.Fail,
      };
      table.AddRow(
        new Markup(Ui.Escape(record.Id)),
        new Markup(record.StartedAt.ToString("dd/MM HH:mm:ss")),
        new Markup($"[bold]{Ui.Escape(record.Command)}[/]"),
        new Markup(Ui.Escape(string.Join(", ", record.Versions))),
        new Markup(Ui.Theme.State(kind, record.Status)),
        new Markup(record.ExitCode?.ToString() ?? "—"),
        new Markup(record.DurationSeconds is { } d ? $"{d:0.0}s" : "—"));
    }

    Ui.Write(table);
    Ui.Muted($"  Diretório: {Services.Journal.Directory}. O histórico local não substitui o histórico TFVC.");
    return Task.FromResult(ExitCodes.Success);
  }
}

/// <summary>pep history show &lt;id&gt; (spec 012).</summary>
public sealed class HistoryShowBuilder : CommandBuilderBase
{
  public HistoryShowBuilder(AppServices services)
    : base(services)
  {
  }

  public override Task<int> BuildAsync(CommandLine line, CancellationToken cancellationToken)
  {
    var id = RequireValue(line.Positional(0), "o id da execução (veja 'pep history list')", line);
    var record = Services.Journal.Find(id)
      ?? throw new UsageException($"Execução '{id}' não encontrada (ou prefixo ambíguo).", "Liste os ids com 'pep history list'.");

    if (Ui.Json)
    {
      Ui.WriteJson(record);
      return Task.FromResult(ExitCodes.Success);
    }

    Ui.Title($"Execução {record.Id}");
    var grid = Ui.NewKeyValueGrid();
    Ui.AddKeyValue(grid, "Comando", $"[bold]{Ui.Escape(string.Join(' ', record.Arguments))}[/]");
    Ui.AddKeyValue(grid, "Projeto / origem", Ui.Escape($"{record.Project ?? "—"} / {record.Source ?? "—"}"));
    Ui.AddKeyValue(grid, "Changeset", record.Changeset?.ToString() ?? "—");
    Ui.AddKeyValue(grid, "Versões", Ui.Escape(string.Join(", ", record.Versions)));
    Ui.AddKeyValue(grid, "Início / fim", Ui.Escape($"{record.StartedAt:dd/MM/yyyy HH:mm:ss} / {record.FinishedAt?.ToString("HH:mm:ss") ?? "em andamento ou interrompida"}"));
    Ui.AddKeyValue(grid, "Situação", Ui.Escape($"{record.Status} (código {record.ExitCode?.ToString() ?? "—"})"));
    Ui.Write(grid);

    if (record.Targets.Count > 0)
    {
      var targets = Ui.NewTable("Alvo", "Estado", "Mensagem");
      foreach (var target in record.Targets)
        targets.AddRow(Ui.Escape(target.Target), Ui.Escape(target.State), Ui.Escape(target.Message));
      Ui.Blank();
      Ui.Write(targets);
    }

    if (record.Steps.Count > 0)
    {
      var steps = Ui.NewTable("Hora", "Alvo", "Etapa", "Resultado");
      foreach (var step in record.Steps)
      {
        steps.AddRow(
          Ui.Escape(step.At.ToString("HH:mm:ss")),
          Ui.Escape(step.Target),
          Ui.Escape(step.Step),
          Ui.Escape(step.Result is null ? string.Empty : $"{step.Result} (tf {step.ToolExitCode}, {step.DurationSeconds:0.0}s)"));
      }

      Ui.Blank();
      Ui.Write(steps);
    }

    foreach (var hint in record.Recovery)
      Ui.Hint(hint);

    Ui.Muted($"  Saída completa das ferramentas (truncada): {Path.Combine(Services.Journal.Directory, record.Id + ".json")}");
    Ui.Muted("  O histórico local registra operações do CLI e não substitui o histórico TFVC.");
    return Task.FromResult(ExitCodes.Success);
  }
}

/// <summary>pep help [comando].</summary>
public sealed class HelpBuilder : CommandBuilderBase
{
  public HelpBuilder(AppServices services)
    : base(services)
  {
  }

  public override Task<int> BuildAsync(CommandLine line, CancellationToken cancellationToken)
  {
    if (line.Positionals.Count == 0)
    {
      HelpRenderer.RenderGeneral(Ui, Services.Chains);
      return Task.FromResult(ExitCodes.Success);
    }

    var match = Services.Chains.Match(line.Positionals);
    if (match is null || match.Chain.Help.Name == "help")
      throw new UsageException($"Comando desconhecido: '{string.Join(' ', line.Positionals)}'.", "Veja 'pep help'.");

    HelpRenderer.RenderCommand(Ui, match.Chain.Help);
    return Task.FromResult(ExitCodes.Success);
  }
}

/// <summary>pep version: banner e ambiente, no estilo "ng version".</summary>
public sealed class VersionBuilder : CommandBuilderBase
{
  public VersionBuilder(AppServices services)
    : base(services)
  {
  }

  public override async Task<int> BuildAsync(CommandLine line, CancellationToken cancellationToken)
  {
    var load = Services.LoadConfig();
    var tf = await Services.Tools.LocateTfAsync(cancellationToken);
    var msbuild = await Services.Tools.LocateMsBuildAsync(cancellationToken);
    var catalog = load.Config is null ? null : VersionCatalog.FromConfig(load.Config);

    var catalogText = catalog?.Current is null
      ? "não configurado"
      : $"atual {catalog.Current.Id} · legadas {(catalog.ActiveLegacy.Count == 0 ? "nenhuma" : string.Join(", ", catalog.ActiveLegacy.Select(v => v.Id)))}";

    if (Ui.Json)
    {
      Ui.WriteJson(new
      {
        pepCli = AppInfo.Version,
        dotnet = Environment.Version.ToString(),
        so = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
        arquitetura = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
        configuracao = new { arquivo = load.Path, situacao = load.Status.ToString() },
        catalogo = catalogText,
        tfExe = new { caminho = tf.Path, versao = tf.Version, problema = tf.Problem },
        msbuild = new { caminho = msbuild.Path, versao = msbuild.Version, problema = msbuild.Problem },
        historico = Services.Journal.Directory,
      });
      return ExitCodes.Success;
    }

    Ui.Banner();
    var grid = Ui.NewKeyValueGrid();
    Ui.AddKeyValue(grid, "PEP CLI", $"[bold]{Ui.Escape(AppInfo.Version)}[/]");
    Ui.AddKeyValue(grid, ".NET runtime", Ui.Escape(Environment.Version.ToString()));
    Ui.AddKeyValue(grid, "Sistema", Ui.Escape($"{System.Runtime.InteropServices.RuntimeInformation.OSDescription} {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}"));
    Ui.AddKeyValue(grid, "Configuração", Ui.Escape($"{load.Path} ({load.Status switch { ConfigLoadStatus.Loaded => "ok", ConfigLoadStatus.Missing => "ausente", _ => "inválida" }})"));
    Ui.AddKeyValue(grid, "Catálogo", Ui.Escape(catalogText));
    Ui.AddKeyValue(grid, "tf.exe", tf.Found ? Ui.Escape($"{tf.Version} · {tf.Path}") : Ui.Theme.State(StateKind.Fail, tf.Problem ?? "não localizado"));
    Ui.AddKeyValue(grid, "MSBuild", msbuild.Found ? Ui.Escape($"{msbuild.Version} · {msbuild.Path}") : Ui.Theme.State(StateKind.Fail, msbuild.Problem ?? "não localizado"));
    Ui.AddKeyValue(grid, "Histórico", Ui.Escape(Services.Journal.Directory));
    Ui.Write(grid);
    Ui.Blank();
    return ExitCodes.Success;
  }
}

/// <summary>Comandos antigos removidos por segurança ou escopo (spec 013).</summary>
public sealed class RemovedCommandBuilder : CommandBuilderBase
{
  private readonly string _reason;
  private readonly string _alternative;

  public RemovedCommandBuilder(AppServices services, string reason, string alternative)
    : base(services)
  {
    _reason = reason;
    _alternative = alternative;
  }

  public override Task<int> BuildAsync(CommandLine line, CancellationToken cancellationToken) =>
    throw new UsageException($"O comando 'pep {line.Help.Name}' foi removido: {_reason}", _alternative);
}
