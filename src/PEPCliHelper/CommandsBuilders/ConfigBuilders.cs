using System.Text.Json;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.Configuration;
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
