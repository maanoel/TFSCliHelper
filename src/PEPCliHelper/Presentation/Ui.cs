using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Infrastructure;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace PEPCliHelper.Presentation;

public interface IStatusSink
{
  /// <summary>Etapa relevante: atualiza o spinner ou imprime uma linha em modo simples.</summary>
  void Step(string text);

  /// <summary>Detalhe efêmero (ex.: última linha do MSBuild): só atualiza o spinner.</summary>
  void Detail(string text);
}

/// <summary>
/// Toda a apresentação passa por aqui: cores, Unicode/ASCII, prompts e JSON.
/// Em --json nada humano é escrito no stdout; prompts só existem quando CanPrompt.
/// </summary>
public sealed class Ui
{
  public const string CancelLabel = "Cancelar";

  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    Converters = { new JsonStringEnumConverter() },
  };

  private readonly TextWriter _jsonOut;

  public Ui(IAnsiConsole console, GlobalOptions options, bool canPrompt, TextWriter jsonOut)
  {
    Console = console;
    Options = options;
    CanPrompt = canPrompt && !options.Json && !options.NonInteractive;
    Theme = new Theme(options.Ascii || !console.Profile.Capabilities.Unicode);
    _jsonOut = jsonOut;
  }

  public IAnsiConsole Console { get; }

  public GlobalOptions Options { get; }

  public Theme Theme { get; }

  public bool Json => Options.Json;

  public bool CanPrompt { get; }

  public bool CanAnimate => CanPrompt && Console.Profile.Capabilities.Interactive;

  public static string Escape(string? text) => Spectre.Console.Markup.Escape(text ?? string.Empty);

  public void Banner() => Human(() => Presentation.Banner.Write(Console, Theme, AppInfo.Version));

  public void Title(string title) => Human(() =>
  {
    Console.WriteLine();
    Console.Write(new Rule($"[bold {Theme.Primary}]{Escape(title)}[/]") { Justification = Justify.Left, Style = Style.Parse(Theme.Muted) });
  });

  public void Rule() => Human(() => Console.Write(new Rule { Style = Style.Parse(Theme.Muted) }));

  public void Blank() => Human(() => Console.WriteLine());

  public void Markup(string markup) => Human(() => Console.MarkupLine(markup));

  public void Text(string text) => Human(() => Console.MarkupLine(Escape(text)));

  public void Muted(string text) => Human(() => Console.MarkupLine($"[{Theme.Muted}]{Escape(text)}[/]"));

  public void Success(string text) => Human(() => Console.MarkupLine(Theme.State(StateKind.Ok, text)));

  public void Info(string text) => Human(() => Console.MarkupLine(Theme.State(StateKind.Info, text)));

  public void Warn(string text) => Human(() => Console.MarkupLine(Theme.State(StateKind.Warn, text)));

  public void Fail(string text) => Human(() => Console.MarkupLine(Theme.State(StateKind.Fail, text)));

  public void Hint(string text) => Human(() => Console.MarkupLine($"  [{Theme.Accent}]{Escape(Theme.Arrow)}[/] {Escape(text)}"));

  public void Bullet(string text, string? color = null) =>
    Human(() => Console.MarkupLine($"  [{color ?? Theme.Muted}]{Escape(Theme.Bullet)}[/] {Escape(text)}"));

  public void Write(IRenderable renderable) => Human(() => Console.Write(renderable));

  /// <summary>Destaque obrigatório da spec: nenhum check-in automático.</summary>
  public void PendingChangesNotice(string text) => Human(() =>
  {
    var panel = new Panel(new Markup($"[bold {Theme.Accent}]{Escape(text)}[/]"))
      .Border(Theme.BoxBorder)
      .BorderColor(Color.MediumPurple1)
      .Padding(1, 0, 1, 0);
    Console.Write(panel);
  });

  public Table NewTable(params string[] columns)
  {
    var table = new Table().Border(Theme.TableBorder).BorderColor(Color.Grey);
    foreach (var column in columns)
      table.AddColumn(new TableColumn($"[bold {Theme.Primary}]{Escape(column)}[/]"));
    return table;
  }

  public Grid NewKeyValueGrid()
  {
    var grid = new Grid();
    grid.AddColumn(new GridColumn().NoWrap().PadRight(2));
    grid.AddColumn();
    return grid;
  }

  public void AddKeyValue(Grid grid, string key, string valueMarkup) =>
    grid.AddRow($"[{Theme.Muted}]{Escape(key)}[/]", valueMarkup);

  public void WriteJson(object value)
  {
    _jsonOut.WriteLine(JsonSerializer.Serialize(value, JsonOptions));
    _jsonOut.Flush();
  }

  public async Task<T> WithStatusAsync<T>(string initial, Func<IStatusSink, Task<T>> work)
  {
    if (Json)
      return await work(NullSink.Instance);

    if (!CanAnimate)
    {
      Muted($"{Theme.Arrow} {initial}");
      return await work(new PlainSink(this));
    }

    return await Console.Status()
      .Spinner(Theme.Ascii ? Spinner.Known.Line : Spinner.Known.Dots)
      .SpinnerStyle(Style.Parse(Theme.Primary))
      .StartAsync(Escape(initial), async context => await work(new SpectreSink(context)));
  }

  public async Task<bool> ConfirmAsync(string question, bool defaultValue, CancellationToken cancellationToken)
  {
    EnsureCanPrompt(question);
    var prompt = new ConfirmationPrompt($"[bold]{Escape(question)}[/]") { DefaultValue = defaultValue };
    return await prompt.ShowAsync(Console, cancellationToken);
  }

  public async Task<T> SelectAsync<T>(string title, IEnumerable<T> items, Func<T, string> label, CancellationToken cancellationToken)
    where T : notnull
  {
    EnsureCanPrompt(title);
    var prompt = new SelectionPrompt<T>()
      .Title($"[bold {Theme.Primary}]?[/] [bold]{Escape(title)}[/]")
      .PageSize(12)
      .MoreChoicesText($"[{Theme.Muted}](use as setas para ver mais opções)[/]")
      .HighlightStyle(Style.Parse($"{Theme.Primary} bold"))
      .UseConverter(item => Escape(label(item)))
      .AddChoices(items);
    return await prompt.ShowAsync(Console, cancellationToken);
  }

  /// <summary>Seleção com a opção final "Cancelar", que lança OperationCanceledException (exit 130 no dispatcher).</summary>
  public async Task<T> SelectOrCancelAsync<T>(string title, IEnumerable<T> items, Func<T, string> label, CancellationToken cancellationToken)
    where T : notnull
  {
    var choices = items.Select(item => new CancelableChoice<T>(item)).Append(CancelableChoice<T>.Cancel);
    var selected = await SelectAsync(title, choices, choice => choice.IsCancel ? CancelLabel : label(choice.Value!), cancellationToken);
    return selected.IsCancel
      ? throw new OperationCanceledException($"Seleção cancelada pelo usuário: \"{title}\".")
      : selected.Value!;
  }

  /// <param name="preselected">Itens que já aparecem marcados.</param>
  public async Task<List<T>> MultiSelectAsync<T>(string title, IEnumerable<T> items, Func<T, string> label, CancellationToken cancellationToken, IEnumerable<T>? preselected = null)
    where T : notnull
  {
    EnsureCanPrompt(title);
    var prompt = new MultiSelectionPrompt<T>()
      .Title($"[bold {Theme.Primary}]?[/] [bold]{Escape(title)}[/]")
      .NotRequired()
      .PageSize(12)
      .HighlightStyle(Style.Parse($"{Theme.Primary} bold"))
      .InstructionsText($"[{Theme.Muted}](Espaço marca/desmarca, Enter confirma, nada marcado cancela)[/]")
      .UseConverter(item => Escape(label(item)))
      .AddChoices(items);
    foreach (var item in preselected ?? [])
      prompt.Select(item);
    return await prompt.ShowAsync(Console, cancellationToken);
  }

  public async Task<string> AskAsync(string question, string? defaultValue, Func<string, string?> validate, CancellationToken cancellationToken)
  {
    EnsureCanPrompt(question);
    var prompt = new TextPrompt<string>($"[bold {Theme.Primary}]?[/] [bold]{Escape(question)}[/]")
      .Validate(value =>
      {
        var error = validate(value);
        return error is null ? ValidationResult.Success() : ValidationResult.Error($"[{Theme.FailColor}]{Escape(error)}[/]");
      });
    if (defaultValue is not null)
      prompt.DefaultValue(defaultValue).ShowDefaultValue();
    return (await prompt.ShowAsync(Console, cancellationToken)).Trim();
  }

  public void RenderError(PepCliException error)
  {
    if (Json)
    {
      WriteJson(new
      {
        erro = new
        {
          mensagem = error.Message,
          onde = error.Where,
          impacto = error.Impact,
          permaneceAlterado = error.Remains,
          proximoPasso = error.NextStep,
          codigoSaida = error.ExitCode,
        },
      });
      return;
    }

    var grid = NewKeyValueGrid();
    AddKeyValue(grid, "O que falhou", $"[bold]{Escape(error.Message)}[/]");
    if (error.Where is not null)
      AddKeyValue(grid, "Onde", Escape(error.Where));
    if (error.Impact is not null)
      AddKeyValue(grid, "Impacto", Escape(error.Impact));
    if (error.Remains is not null)
      AddKeyValue(grid, "Permanece alterado", Escape(error.Remains));
    if (error.NextStep is not null)
      AddKeyValue(grid, "Próximo passo", $"[{Theme.Accent}]{Escape(error.NextStep)}[/]");

    Console.WriteLine();
    Console.Write(new Panel(grid)
      .Header($"[{Theme.FailColor}] {Escape(Theme.Icon(StateKind.Fail))} {Escape(ExitCodes.Describe(error.ExitCode))} (código {error.ExitCode}) [/]")
      .Border(Theme.BoxBorder)
      .BorderColor(Color.Red1));
  }

  public void RenderUnexpected(Exception error, string? historyHint)
  {
    var wrapped = new PepCliException(
      $"Erro inesperado: {error.Message}",
      ExitCodes.Unexpected,
      error.GetType().Name,
      "A operação foi interrompida.",
      "Etapas concluídas antes do erro permanecem. Verifique 'pep pending list' se havia operação TFVC em andamento.",
      historyHint ?? "Execute novamente com --verbose e reporte o erro à equipe com o id do histórico.");
    RenderError(wrapped);
    if (Options.Verbose && !Json)
      Console.WriteException(error, ExceptionFormats.ShortenEverything);
  }

  private void EnsureCanPrompt(string question)
  {
    if (!CanPrompt)
      throw new UsageException(
        $"Entrada necessária em modo não interativo: \"{question}\".",
        "Informe o valor por argumento (veja '--help') ou execute em um terminal interativo.");
  }

  private void Human(Action action)
  {
    if (!Json)
      action();
  }

  private sealed record CancelableChoice<T>(T? Value, bool IsCancel = false)
  {
    public static readonly CancelableChoice<T> Cancel = new(default, IsCancel: true);
  }

  private sealed class NullSink : IStatusSink
  {
    public static readonly NullSink Instance = new();

    public void Step(string text) { }

    public void Detail(string text) { }
  }

  private sealed class PlainSink(Ui ui) : IStatusSink
  {
    public void Step(string text) => ui.Muted($"  {ui.Theme.Arrow} {text}");

    public void Detail(string text)
    {
      if (ui.Options.Verbose)
        ui.Muted($"    {text}");
    }
  }

  private sealed class SpectreSink(StatusContext context) : IStatusSink
  {
    public void Step(string text) => context.Status(Escape(Truncate(text)));

    public void Detail(string text) => context.Status(Escape(Truncate(text)));

    private static string Truncate(string text) => text.Length > 110 ? text[..107] + "..." : text;
  }
}
