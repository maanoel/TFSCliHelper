using PEPCliHelper.CommandsBuilders;
using PEPCliHelper.Infrastructure;
using PEPCliHelper.Interface;

namespace PEPCliHelper.CommandChain;

public sealed class EnvListChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["env", "list"],
    Category = "Ambientes e configuração",
    Summary = "Lista o catálogo: versão atual, legadas ativas e desativadas.",
    Examples = [new("pep env list", "Tabela do catálogo."), new("pep env list --json", "Para scripts.")],
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new EnvListBuilder(services);
}

public sealed class EnvDiscoverChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["env", "discover"],
    Category = "Ambientes e configuração",
    Summary = "Procura versões em <raiz>\\Atual e <raiz>\\Legado e consulta mapeamentos (somente leitura).",
    Options = [new OptionSpec("offline", null, "Não consulta o TFVC; lista apenas as pastas.")],
    Examples = [new("pep env discover", "Propõe candidatos sem alterar nada.")],
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new EnvDiscoverBuilder(services);
}

public sealed class EnvConfigureChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["env", "configure"],
    Category = "Ambientes e configuração",
    Summary = "Define a versão atual e até 4 legadas ativas (rotação). Grava com revisão e confirmação.",
    Details = "Altera somente o catálogo: não move nem exclui pastas, não altera branches nem workspaces.",
    Options =
    [
      new OptionSpec("atual", "<id>", "Versão atual (modo não interativo)."),
      new OptionSpec("legado", "<id>", "Legada ativa (modo não interativo).", Repeatable: true),
      new OptionSpec("offline", null, "Não consulta o TFVC durante a descoberta."),
    ],
    Examples =
    [
      new("pep env configure", "Seleção guiada."),
      new("pep env configure --atual 12.1.2610 --legado 12.1.2606 --legado 12.1.2602 --yes", "Rotação por script."),
    ],
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new EnvConfigureBuilder(services);
}

public sealed class EnvValidateChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["env", "validate"],
    Category = "Ambientes e configuração",
    Summary = "Valida pastas e mapeamentos TFVC de todas as versões ativas.",
    Examples = [new("pep env validate", "Após corrigir um mapeamento.")],
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new EnvValidateBuilder(services);
}

public sealed class ConfigInitChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["config", "init"],
    Category = "Ambientes e configuração",
    Summary = "Cria o arquivo de configuração com defaults seguros.",
    Options = [new OptionSpec("force", null, "Recria mesmo se existir (faz backup).")],
    Examples = [new("pep config init", "Primeiro uso.")],
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new ConfigInitBuilder(services);
}

public sealed class ConfigAutoChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["config", "auto"],
    Category = "Ambientes e configuração",
    Summary = "Configuração automática: <raiz>\\Atual\\Release como atual e as 4 legadas mais novas de <raiz>\\Legado ativas.",
    Details = "Legadas ordenadas pelo número da versão; as mais antigas ficam desativadas. Caminhos TFVC pela convenção, sem consultar o servidor "
      + "(confirme com 'pep env validate'). Mantém coleção, ferramentas e projetos; faz backup do arquivo anterior. Não cria, move nem exclui pastas.",
    Examples =
    [
      new("pep config auto", "Mostra a proposta e grava após Enter."),
      new("pep config auto --yes", "Grava sem perguntar."),
    ],
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new ConfigAutoBuilder(services);
}

public sealed class ConfigShowChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["config", "show"],
    Category = "Ambientes e configuração",
    Summary = "Mostra a configuração efetiva e o arquivo usado.",
    Examples = [new("pep config show", "Tabelas."), new("pep config show --json", "JSON.")],
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new ConfigShowBuilder(services);
}

public sealed class ConfigValidateChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["config", "validate"],
    Category = "Ambientes e configuração",
    Summary = "Valida estrutura e regras da configuração.",
    Examples = [new("pep config validate", "Retorna 0 se válida, 2 se inválida.")],
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new ConfigValidateBuilder(services);
}
