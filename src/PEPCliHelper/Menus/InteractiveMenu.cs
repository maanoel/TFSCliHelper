using PEPCliHelper.Core.Catalog;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Infrastructure;
using PEPCliHelper.Presentation;

namespace PEPCliHelper.Menus;

/// <summary>
/// Menu interativo (spec 002). Cada opção monta a mesma linha de comando que o modo por argumentos
/// e executa o mesmo builder: nenhuma regra é duplicada aqui.
/// </summary>
public sealed class InteractiveMenu
{
  private const string Back = "← Voltar";

  public const string PromptLabel = "pep>";

  private static readonly string[] PromptExitWords = ["sair", "exit", "voltar", "quit"];

  private readonly AppServices _services;
  private readonly ConsoleCancellation _cancellation;
  private readonly Func<GlobalOptions, AppServices> _createServices;

  /// <param name="createServices">Cria serviços com as opções globais digitadas no prompt (ex.: --yes, --json).</param>
  public InteractiveMenu(AppServices services, ConsoleCancellation cancellation, Func<GlobalOptions, AppServices>? createServices = null)
  {
    _services = services;
    _cancellation = cancellation;
    _createServices = createServices ?? AppServices.CreateDefault;
  }

  private Ui Ui => _services.Ui;

  public async Task<int> RunAsync()
  {
    Ui.Banner();
    EnsureConfigured();
    Ui.Muted("  Use as setas para navegar e Enter para escolher. Ctrl+C cancela a operação em andamento.");

    var lastExit = ExitCodes.Success;
    while (true)
    {
      _cancellation.Reset();
      string choice;
      try
      {
        choice = await Ui.SelectAsync("O que você quer fazer?", MainOptions.Select(o => o.Label), label => label, _cancellation.Token);
      }
      catch (OperationCanceledException)
      {
        return lastExit;
      }

      var option = MainOptions.First(o => o.Label == choice);
      if (option.Key == "exit")
      {
        Ui.Muted("  Até logo.");
        return lastExit;
      }

      if (option.Key == "prompt")
      {
        lastExit = await RunPromptAsync(lastExit);
        Ui.Blank();
        continue;
      }

      try
      {
        var tokens = await BuildTokensAsync(option.Key, _cancellation.Token);
        if (tokens is null)
          continue;

        Ui.Muted($"  {Ui.Theme.Arrow} pep {string.Join(' ', tokens.Select(t => t.Contains(' ') ? $"\"{t}\"" : t))}");
        lastExit = await PepApp.DispatchAsync(_services, tokens, _cancellation.Token);
      }
      catch (OperationCanceledException)
      {
        Ui.Warn("Cancelado. Voltando ao menu.");
      }
      catch (PepCliException ex)
      {
        Ui.RenderError(ex);
        lastExit = ex.ExitCode;
      }

      Ui.Blank();
    }
  }

  /// <summary>Sem configuração (ou sem versão atual) aplica a configuração automática sem perguntas; falhas aqui não impedem o menu.</summary>
  private void EnsureConfigured()
  {
    try
    {
      new FirstRunSetup(_services).EnsureConfigured();
    }
    catch (PepCliException ex)
    {
      Ui.RenderError(ex);
    }
  }

  /// <summary>
  /// Prompt 'pep&gt;' dentro do CLI: cada linha é um comando do PEP CLI (com ou sem o prefixo 'pep'), executado pelo
  /// mesmo despacho do modo por argumentos. Não executa comandos do shell. 'sair' ou linha vazia com Ctrl+C volta ao menu.
  /// </summary>
  private async Task<int> RunPromptAsync(int lastExit)
  {
    Ui.Title("Prompt de comandos");
    Ui.Muted("  Digite comandos do PEP CLI, com ou sem 'pep' (ex.: merge --changeset 669997, pep build --all, help).");
    Ui.Muted("  'sair' volta ao menu. Apenas comandos do PEP CLI são aceitos; comandos do Windows não são executados.");

    while (true)
    {
      _cancellation.Reset();
      string line;
      try
      {
        line = await Ui.ReadLineAsync(PromptLabel, _cancellation.Token);
      }
      catch (OperationCanceledException)
      {
        return lastExit;
      }

      try
      {
        var tokens = CommandLineSplitter.Split(line);
        if (tokens.Count > 0 && tokens[0].Equals("pep", StringComparison.OrdinalIgnoreCase))
          tokens.RemoveAt(0);
        if (tokens.Count == 0)
          continue;
        if (tokens.Count == 1 && PromptExitWords.Contains(tokens[0], StringComparer.OrdinalIgnoreCase))
          return lastExit;

        lastExit = await ExecutePromptLineAsync(tokens);
      }
      catch (PepCliException ex)
      {
        Ui.RenderError(ex);
        lastExit = ex.ExitCode;
      }

      _services.InvalidateConfig();
      Ui.Blank();
    }
  }

  private async Task<int> ExecutePromptLineAsync(IReadOnlyList<string> line)
  {
    var (typed, tokens) = GlobalOptions.Parse(line, Environment.GetEnvironmentVariable);
    var baseOptions = _services.Options;
    var options = new GlobalOptions
    {
      NoColor = baseOptions.NoColor || typed.NoColor,
      Ascii = baseOptions.Ascii || typed.Ascii,
      Json = typed.Json,
      NonInteractive = typed.NonInteractive,
      Yes = typed.Yes,
      Verbose = baseOptions.Verbose || typed.Verbose,
      Help = typed.Help,
      ShowVersion = typed.ShowVersion,
      ConfigPath = typed.ConfigPath ?? baseOptions.ConfigPath,
    };

    if (options.ShowVersion && tokens.Count == 0)
    {
      Ui.Info(AppInfo.Version);
      return ExitCodes.Success;
    }

    var services = _createServices(options);
    if (tokens.Count == 0)
    {
      HelpRenderer.RenderGeneral(services.Ui, services.Chains);
      return ExitCodes.Success;
    }

    return await PepApp.DispatchAsync(services, tokens, _cancellation.Token);
  }

  private static readonly (string Key, string Label)[] MainOptions =
  [
    ("merge", "Merge de changeset entre versões"),
    ("get", "Get (atualizar versões)"),
    ("build", "Build (MSBuild)"),
    ("env", "Ambientes e versões"),
    ("doctor", "Diagnóstico (doctor)"),
    ("pending", "Pending changes"),
    ("tools", "Ferramentas locais (broker, host, RM)"),
    ("history", "Histórico de execuções"),
    ("prompt", "Prompt de comandos (digitar comandos pep)"),
    ("help", "Ajuda"),
    ("exit", "Sair"),
  ];

  private async Task<IReadOnlyList<string>?> BuildTokensAsync(string key, CancellationToken cancellationToken)
  {
    switch (key)
    {
      case "merge":
        // O merge sempre mostra o plano antes da confirmação.
        return ["merge"];

      case "get":
      {
        var scope = await Ui.SelectAsync("Get em quais versões?",
          ["Todas as versões ativas", "Uma versão", Back], s => s, cancellationToken);
        return scope switch
        {
          "Todas as versões ativas" => [key, "all"],
          "Uma versão" => await VersionAsync(cancellationToken) is { } version ? [key, "version", version] : null,
          _ => null,
        };
      }

      case "build":
        // Versões e projetos são escolhidos dentro de 'pep build' (PEP sempre primeiro).
        return ["build"];

      case "env":
      {
        // Sem opções de configuração na tela: a configuração é automática na primeira execução.
        // Os comandos 'pep config auto' e 'pep config show' continuam disponíveis por argumento.
        var action = await Ui.SelectAsync("Ambientes e versões",
          ["Listar catálogo", "Descobrir versões (somente leitura)", "Validar mapeamentos", Back],
          s => s, cancellationToken);
        return action switch
        {
          "Listar catálogo" => ["env", "list"],
          "Descobrir versões (somente leitura)" => ["env", "discover"],
          "Validar mapeamentos" => ["env", "validate"],
          _ => null,
        };
      }

      case "doctor":
        return ["doctor"];

      case "pending":
      {
        var action = await Ui.SelectAsync("Pending changes", ["Todas as versões ativas", "Uma versão", Back], s => s, cancellationToken);
        return action switch
        {
          "Todas as versões ativas" => ["pending", "list"],
          "Uma versão" => await VersionAsync(cancellationToken) is { } version ? ["pending", "list", version] : null,
          _ => null,
        };
      }

      case "tools":
      {
        var action = await Ui.SelectAsync("Ferramentas locais",
          ["Abrir RM.Host", "Abrir RM", "Encerrar RM.Host (kill host)", "Remover broker", "Abrir Alias.dat", "Abrir RM.Host.exe.config", Back],
          s => s, cancellationToken);
        if (action == Back)
          return null;
        if (action.StartsWith("Encerrar", StringComparison.Ordinal))
          return ["kill", "host"];

        var version = await VersionAsync(cancellationToken);
        if (version is null)
          return null;

        return action switch
        {
          "Abrir RM.Host" => ["open", "host", version],
          "Abrir RM" => ["open", "rm", version],
          "Remover broker" => ["delete", "broker", version],
          "Abrir Alias.dat" => ["open", "alias", version],
          _ => ["open", "hostconfig", version],
        };
      }

      case "history":
      {
        var records = _services.Journal.List(15);
        if (records.Count == 0)
          return ["history", "list"];

        const string list = "Listar execuções";
        var selected = await Ui.SelectAsync("Histórico",
          new[] { list }.Concat(records.Select(r => $"{r.Id}  {r.Command}  {r.Status}")).Append(Back), s => s, cancellationToken);
        return selected == Back ? null : selected == list ? ["history", "list"] : ["history", "show", selected.Split(' ')[0]];
      }

      default:
        return ["help"];
    }
  }

  private async Task<string?> VersionAsync(CancellationToken cancellationToken)
  {
    VersionCatalog catalog;
    try
    {
      catalog = _services.RequireCatalog();
    }
    catch (PepCliException ex)
    {
      Ui.RenderError(ex);
      return null;
    }

    const string back = "__back__";
    var selected = await Ui.SelectAsync("Versão", catalog.Active.Select(v => v.Id).Append(back),
      id => id == back ? Back : catalog.Active.First(v => v.Id == id).Label, cancellationToken);
    return selected == back ? null : selected;
  }
}
