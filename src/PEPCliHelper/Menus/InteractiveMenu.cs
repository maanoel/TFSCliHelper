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

  private readonly AppServices _services;
  private readonly ConsoleCancellation _cancellation;

  public InteractiveMenu(AppServices services, ConsoleCancellation cancellation)
  {
    _services = services;
    _cancellation = cancellation;
  }

  private Ui Ui => _services.Ui;

  public async Task<int> RunAsync()
  {
    Ui.Banner();
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

  private static readonly (string Key, string Label)[] MainOptions =
  [
    ("merge", "Merge de changeset entre versões"),
    ("get", "Get (atualizar versões)"),
    ("build", "Build (MSBuild)"),
    ("env", "Ambientes e versões"),
    ("doctor", "Diagnóstico (doctor)"),
    ("login", "Login do tf.exe (resolver TF30063 / sem autenticação)"),
    ("pending", "Pending changes"),
    ("tools", "Ferramentas locais (broker, host, RM)"),
    ("history", "Histórico de execuções"),
    ("help", "Ajuda"),
    ("exit", "Sair"),
  ];

  private async Task<IReadOnlyList<string>?> BuildTokensAsync(string key, CancellationToken cancellationToken)
  {
    switch (key)
    {
      case "merge":
        return await MergeAsync(cancellationToken);

      case "get":
      case "build":
      {
        var scope = await Ui.SelectAsync(key == "get" ? "Get em quais versões?" : "Build de quais versões?",
          ["Todas as versões ativas", "Uma versão", "Simular todas (dry-run)", Back], s => s, cancellationToken);
        return scope switch
        {
          "Todas as versões ativas" => [key, "all"],
          "Simular todas (dry-run)" => [key, "all", "--dry-run"],
          "Uma versão" => await VersionAsync(cancellationToken) is { } version ? [key, "version", version] : null,
          _ => null,
        };
      }

      case "env":
      {
        var action = await Ui.SelectAsync("Ambientes e versões",
          ["Listar catálogo", "Descobrir versões (somente leitura)", "Configurar atual e legadas (rotação)", "Validar mapeamentos", "Criar configuração inicial", "Mostrar configuração", Back],
          s => s, cancellationToken);
        return action switch
        {
          "Listar catálogo" => ["env", "list"],
          "Descobrir versões (somente leitura)" => ["env", "discover"],
          "Configurar atual e legadas (rotação)" => ["env", "configure"],
          "Validar mapeamentos" => ["env", "validate"],
          "Criar configuração inicial" => ["config", "init"],
          "Mostrar configuração" => ["config", "show"],
          _ => null,
        };
      }

      case "login":
        return ["login"];

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

  private async Task<IReadOnlyList<string>?> MergeAsync(CancellationToken cancellationToken)
  {
    var mode = await Ui.SelectAsync("Merge", ["Executar merge (com plano e confirmação)", "Simular merge (dry-run)", Back], s => s, cancellationToken);
    return mode switch
    {
      "Executar merge (com plano e confirmação)" => ["merge"],
      "Simular merge (dry-run)" => ["merge", "--dry-run"],
      _ => null,
    };
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
