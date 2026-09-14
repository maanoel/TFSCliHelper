using System.Text.Json;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.Configuration;
using PEPCliHelper.Core.Environments;
using PEPCliHelper.Infrastructure;
using PEPCliHelper.Presentation;
using Spectre.Console;

namespace PEPCliHelper.CommandsBuilders;

/// <summary>pep config init (spec 003).</summary>
public sealed class ConfigInitBuilder : CommandBuilderBase
{
  public ConfigInitBuilder(AppServices services)
    : base(services)
  {
  }

  public override Task<int> BuildAsync(CommandLine line, CancellationToken cancellationToken)
  {
    var store = Services.ConfigStore;
    if (store.Exists && !line.Flag("force"))
    {
      throw new UsageException(
        $"Já existe configuração em {store.Path}. Nada foi alterado.",
        "Revise com 'pep config show'. Para recriar com os defaults use 'pep config init --force' (um backup será criado).",
        store.Path);
    }

    var config = PepConfig.CreateDefault();
    var backup = store.Save(config);
    Services.InvalidateConfig();

    if (Ui.Json)
    {
      Ui.WriteJson(new { arquivo = store.Path, backup, criado = true });
    }
    else
    {
      Ui.Success($"Configuração criada: {store.Path}");
      if (backup is not null)
        Ui.Muted($"  Backup da anterior: {backup}");
      Ui.Muted($"  Raiz local: {config.LocalRoot} · Coleção: {config.Collection}");
      Ui.Muted("  Projetos: " + string.Join(", ", config.Projects.Select(p => $"{p.Alias} ({p.Name})")));
      Ui.Title("Próximos passos");
      Ui.Hint("pep env discover     lista as pastas de versões e seus mapeamentos (somente leitura)");
      Ui.Hint("pep env configure    escolhe a versão atual e até 4 legadas");
      Ui.Hint("pep doctor           valida ferramentas, conexão e mapeamentos");
    }

    return Task.FromResult(ExitCodes.Success);
  }
}

/// <summary>pep config show (spec 003). A configuração não guarda segredos; campos suspeitos seriam rejeitados na carga.</summary>
public sealed class ConfigShowBuilder : CommandBuilderBase
{
  public ConfigShowBuilder(AppServices services)
    : base(services)
  {
  }

  public override Task<int> BuildAsync(CommandLine line, CancellationToken cancellationToken)
  {
    var load = Services.LoadConfig();
    if (load.Config is null)
    {
      throw new UsageException(string.Join(" | ", load.Errors),
        load.Status == ConfigLoadStatus.Missing ? "Execute 'pep config init'." : "Corrija o arquivo e execute 'pep config validate'.", load.Path);
    }

    if (Ui.Json)
    {
      Ui.WriteJson(new
      {
        arquivo = load.Path,
        valida = load.Status == ConfigLoadStatus.Loaded,
        erros = load.Errors,
        configuracao = JsonDocument.Parse(ConfigStore.Serialize(load.Config)).RootElement,
      });
      return Task.FromResult(load.Status == ConfigLoadStatus.Loaded ? ExitCodes.Success : ExitCodes.Usage);
    }

    var config = load.Config;
    Ui.Title("Configuração");
    var grid = Ui.NewKeyValueGrid();
    Ui.AddKeyValue(grid, "Arquivo", Ui.Escape(load.Path));
    Ui.AddKeyValue(grid, "Precedência", "argumentos (--config) > PEPCLI_CONFIG / arquivo do usuário > defaults");
    Ui.AddKeyValue(grid, "Raiz local", Ui.Escape(config.LocalRoot));
    Ui.AddKeyValue(grid, "Coleção", Ui.Escape(config.Collection ?? "(não configurada)"));
    Ui.AddKeyValue(grid, "tf.exe", Ui.Escape(config.Tools.TfExe ?? "(automático via vswhere)"));
    Ui.AddKeyValue(grid, "MSBuild", Ui.Escape(config.Tools.MsBuild ?? "(automático via vswhere)"));
    Ui.AddKeyValue(grid, "Editor", Ui.Escape(config.Tools.Editor));
    Ui.AddKeyValue(grid, "Apresentação", $"sem cor: {config.Display.NoColor} · ascii: {config.Display.Ascii}");
    Ui.AddKeyValue(grid, "Segredos", "não armazenados (autenticação do tf.exe/Visual Studio)");
    Ui.Write(grid);

    Ui.Title("Projetos");
    var projects = Ui.NewTable("Alias", "Nome", "Pasta local", "Pasta servidor", "Solução");
    foreach (var p in config.Projects)
      projects.AddRow(Ui.Escape(p.Alias), Ui.Escape(p.Name), Ui.Escape(p.LocalFolder), Ui.Escape(p.ServerFolder), Ui.Escape(p.Solution ?? "—"));
    Ui.Write(projects);

    Ui.Title("Versões");
    var versions = Ui.NewTable("Id", "Tipo", "Ativa", "Aliases", "Local", "Servidor");
    foreach (var v in config.Versions)
    {
      versions.AddRow(
        new Markup($"[bold]{Ui.Escape(v.Id)}[/]"),
        new Markup(v.IsCurrent ? "atual" : "legada"),
        new Markup(Ui.Theme.State(v.Active ? StateKind.Ok : StateKind.Neutral, v.Active ? "sim" : "não")),
        new Markup(Ui.Escape(string.Join(", ", v.Aliases))),
        new Markup(Ui.Escape(v.LocalPath)),
        new Markup(Ui.Escape(v.ServerPath)));
    }

    Ui.Write(versions);

    Ui.Title("Arquivos locais (relativos à versão)");
    var files = Ui.NewKeyValueGrid();
    Ui.AddKeyValue(files, "broker", Ui.Escape(config.LocalFiles.Broker));
    Ui.AddKeyValue(files, "host", Ui.Escape(config.LocalFiles.Host));
    Ui.AddKeyValue(files, "rm", Ui.Escape(config.LocalFiles.Rm));
    Ui.AddKeyValue(files, "alias", Ui.Escape(config.LocalFiles.Alias));
    Ui.AddKeyValue(files, "hostConfig", Ui.Escape(config.LocalFiles.HostConfig));
    Ui.Write(files);

    foreach (var error in load.Errors)
      Ui.Fail(error);

    return Task.FromResult(load.Status == ConfigLoadStatus.Loaded ? ExitCodes.Success : ExitCodes.Usage);
  }
}

/// <summary>
/// pep config auto (spec 004, decisão de 2026-09-14): detecta Atual\Release e as legadas em Legado pela convenção de pastas,
/// mostra a proposta e grava com confirmação (Enter aceita). Não consulta o TFVC.
/// </summary>
public sealed class ConfigAutoBuilder : CommandBuilderBase
{
  public ConfigAutoBuilder(AppServices services)
    : base(services)
  {
  }

  public override async Task<int> BuildAsync(CommandLine line, CancellationToken cancellationToken)
  {
    var (baseConfig, _) = LoadBase(Services);
    var proposal = new AutoConfigurator(Services.FileSystem).Propose(baseConfig);

    if (!proposal.CanApply)
    {
      throw new PreconditionException(
        "Configuração automática indisponível: " + string.Join(" | ", proposal.Errors),
        "Confira a raiz local em 'pep config show' ou configure manualmente com 'pep env configure'.",
        Path.Combine(baseConfig.LocalRoot, AutoConfigurator.CurrentFolder, AutoConfigurator.CurrentRelease));
    }

    Render(Ui, proposal);
    if (!await ConfirmAsync("Aplicar esta configuração?", cancellationToken, defaultValue: true))
    {
      Ui.Warn("Nada foi gravado.");
      return ExitCodes.Cancelled;
    }

    var backup = Apply(Services, proposal);
    if (Ui.Json)
      Ui.WriteJson(ToJson(Services.ConfigStore.Path, backup, proposal));
    else
      RenderApplied(Services, proposal, backup);

    return ExitCodes.Success;
  }

  /// <summary>Configuração existente e válida, ou os defaults quando não há arquivo. Arquivo inválido nunca é sobrescrito.</summary>
  internal static (PepConfig Config, ConfigLoadStatus Status) LoadBase(AppServices services)
  {
    var load = services.LoadConfig();
    return load.Status switch
    {
      ConfigLoadStatus.Loaded => (load.Config!, load.Status),
      ConfigLoadStatus.Missing => (PepConfig.CreateDefault(), load.Status),
      _ => throw new UsageException(
        "A configuração existente é inválida e não será sobrescrita automaticamente: " + string.Join(" | ", load.Errors),
        "Corrija o arquivo (veja 'pep config validate') ou recrie com 'pep config init --force' (um backup será criado) e execute 'pep config auto' novamente.",
        load.Path),
    };
  }

  internal static string? Apply(AppServices services, AutoConfigProposal proposal)
  {
    var backup = services.ConfigStore.Save(proposal.Config);
    services.InvalidateConfig();
    return backup;
  }

  internal static void Render(Ui ui, AutoConfigProposal proposal)
  {
    var table = ui.NewTable("Versão", "Tipo", "Situação", "Pasta local", "Caminho TFVC", "Projetos");
    foreach (var v in proposal.Versions)
    {
      table.AddRow(
        new Markup($"[bold]{Ui.Escape(v.Id)}[/]"),
        new Markup(v.IsCurrent ? $"[{ui.Theme.Accent}]atual[/]" : "legada"),
        new Markup(v.Active ? ui.Theme.State(StateKind.Ok, "ativa") : ui.Theme.State(StateKind.Neutral, "desativada")),
        new Markup(Ui.Escape(v.Folder)),
        new Markup(Ui.Escape(v.ServerPath)),
        new Markup(v.HasProjects ? Ui.Escape(string.Join(", ", v.ProjectsFound)) : ui.Theme.State(StateKind.Warn, "sem projetos")));
    }

    if (proposal.Versions.Count > 0)
      ui.Write(table);

    foreach (var ignored in proposal.Ignored)
      ui.Muted($"  Ignorada: {ignored.Folder} ({ignored.Reason})");
    foreach (var warning in proposal.Warnings)
      ui.Warn(warning);
    foreach (var error in proposal.Errors)
      ui.Fail(error);

    ui.Muted("  Caminhos TFVC pela convenção da pasta, sem consultar o servidor. Nenhuma pasta é criada, movida ou excluída.");
  }

  internal static void RenderApplied(AppServices services, AutoConfigProposal proposal, string? backup)
  {
    var ui = services.Ui;
    ui.Success($"Configuração gravada: {services.ConfigStore.Path}");
    if (backup is not null)
      ui.Muted($"  Backup da anterior: {backup}");
    ui.Muted($"  Atual + {proposal.ActiveLegacy.Count} legada(s) ativa(s)" + (proposal.InactiveLegacy.Count > 0 ? $", {proposal.InactiveLegacy.Count} desativada(s)." : "."));
    ui.Title("Próximos passos");
    ui.Hint("pep login           autentica o tf.exe (se ainda não fez)");
    ui.Hint("pep doctor          valida ferramentas e conexão");
    ui.Hint("pep env validate    confirma os mapeamentos TFVC das versões ativas");
  }

  private static object ToJson(string path, string? backup, AutoConfigProposal proposal) => new
  {
    gravado = true,
    arquivo = path,
    backup,
    atual = proposal.Current?.Id,
    legadasAtivas = proposal.ActiveLegacy.Select(v => v.Id),
    legadasDesativadas = proposal.InactiveLegacy.Select(v => v.Id),
    versoes = proposal.Versions.Select(v => new
    {
      id = v.Id,
      atual = v.IsCurrent,
      ativa = v.Active,
      pasta = v.Folder,
      caminhoLocal = v.LocalPath,
      caminhoServidor = v.ServerPath,
      projetos = v.ProjectsFound,
    }),
    ignoradas = proposal.Ignored.Select(i => new { pasta = i.Folder, motivo = i.Reason }),
    avisos = proposal.Warnings,
  };
}

/// <summary>pep config validate (spec 003).</summary>
public sealed class ConfigValidateBuilder : CommandBuilderBase
{
  public ConfigValidateBuilder(AppServices services)
    : base(services)
  {
  }

  public override Task<int> BuildAsync(CommandLine line, CancellationToken cancellationToken)
  {
    var load = Services.LoadConfig();
    var valid = load.Status == ConfigLoadStatus.Loaded;

    if (Ui.Json)
    {
      Ui.WriteJson(new { arquivo = load.Path, situacao = load.Status.ToString(), valida = valid, erros = load.Errors });
    }
    else if (valid)
    {
      Ui.Success($"Configuração válida: {load.Path}");
    }
    else
    {
      Ui.Fail($"Configuração {(load.Status == ConfigLoadStatus.Missing ? "ausente" : "inválida")}: {load.Path}");
      foreach (var error in load.Errors)
        Ui.Bullet(error, Ui.Theme.FailColor);
      Ui.Hint(load.Status == ConfigLoadStatus.Missing ? "Execute 'pep config init'." : "Corrija os campos acima e execute novamente.");
    }

    return Task.FromResult(valid ? ExitCodes.Success : ExitCodes.Usage);
  }
}
