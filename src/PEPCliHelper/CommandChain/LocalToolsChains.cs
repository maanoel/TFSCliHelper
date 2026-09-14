using PEPCliHelper.CommandsBuilders;
using PEPCliHelper.Core.LocalTools;
using PEPCliHelper.Infrastructure;
using PEPCliHelper.Interface;

namespace PEPCliHelper.CommandChain;

public sealed class DeleteBrokerChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["delete", "broker"],
    Category = "Ferramentas locais",
    Summary = "Remove somente o _Broker.dat da versão, com confirmação e backup.",
    Arguments = [new ArgumentSpec("versao", "Versão (ex.: atual, 2606).")],
    Options = [new OptionSpec("no-backup", null, "Não cria backup antes de remover.")],
    Examples = [new("pep delete broker 2606", "Remove Bin\\_Broker.dat da 12.1.2606.")],
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new DeleteBrokerBuilder(services);
}

public sealed class OpenHostChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["open", "host"],
    Category = "Ferramentas locais",
    Summary = "Abre o RM.Host.exe da versão (avisa se já estiver aberto).",
    Arguments = [new ArgumentSpec("versao", "Versão.")],
    Options = [new OptionSpec("new-instance", null, "Abre outra instância mesmo se já houver uma em execução.")],
    Examples = [new("pep open host atual", "Abre o host da versão atual.")],
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new OpenBuilder(services, LocalFileKind.Host);
}

public sealed class OpenRmChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["open", "rm"],
    Category = "Ferramentas locais",
    Summary = "Abre o RM.exe da versão.",
    Arguments = [new ArgumentSpec("versao", "Versão.")],
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new OpenBuilder(services, LocalFileKind.Rm);
}

public sealed class OpenAliasChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["open", "alias"],
    Category = "Ferramentas locais",
    Summary = "Abre o Alias.dat da versão no editor configurado.",
    Arguments = [new ArgumentSpec("versao", "Versão.")],
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new OpenBuilder(services, LocalFileKind.Alias);
}

public sealed class OpenHostConfigChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["open", "hostconfig"],
    Category = "Ferramentas locais",
    Summary = "Abre o RM.Host.exe.config da versão no editor configurado.",
    Arguments = [new ArgumentSpec("versao", "Versão.")],
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new OpenBuilder(services, LocalFileKind.HostConfig);
}

public sealed class KillHostChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["kill", "host"],
    Category = "Ferramentas locais",
    Summary = "Lista processos RM.Host e encerra somente os escolhidos (gracioso por padrão).",
    Details = "Mostra PID, caminho, versão inferida e confiança. Nunca encerra todos pelo nome.",
    Options =
    [
      new OptionSpec("pid", "<n>", "PID a encerrar.", Repeatable: true),
      new OptionSpec("version", "<versao>", "Encerra os RM.Host desta versão (confiança alta)."),
      new OptionSpec("force", null, "Encerramento forçado (aviso e confirmação próprios)."),
    ],
    Examples = [new("pep kill host", "Seleção guiada."), new("pep kill host --pid 12345", "Encerra um PID."), new("pep kill host --version 2606 --force --yes --non-interactive", "Forçado por script.")],
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new KillHostBuilder(services);
}

/// <summary>Comandos antigos removidos (spec 013).</summary>
public sealed class RemovedChain : ICommandChain
{
  private readonly string _reason;
  private readonly string _alternative;

  public RemovedChain(string[] path, string reason, string alternative)
  {
    _reason = reason;
    _alternative = alternative;
    Help = new CommandHelp
    {
      Path = path,
      Category = "Removidos",
      Summary = $"Removido: {reason}",
      Hidden = true,
      MaxPositionals = int.MaxValue,
    };
  }

  public CommandHelp Help { get; }

  public ICommandBuilder CreateBuilder(AppServices services) => new RemovedCommandBuilder(services, _reason, _alternative);
}

public static class RemovedChains
{
  private const string FrontReason = "o front do PEP não é mais gerenciado pelo PEP CLI (migrou para o Git).";
  private const string FrontAlternative = "Use o repositório Git do front diretamente (git/VS Code).";

  private const string ManualConfigReason =
    "a configuração manual não existe mais: na primeira execução o PEP CLI se configura sozinho (Atual\\Release e as 4 legadas mais novas).";

  private const string ManualConfigAlternative = "Para detectar as versões novamente, execute 'pep config auto'.";

  public static IReadOnlyList<ICommandChain> All { get; } =
  [
    new RemovedChain(["open", "front"], FrontReason, FrontAlternative),
    new RemovedChain(["open", "podoc"], FrontReason, "Acesse a documentação do PO UI no navegador."),
    new RemovedChain(["kill", "all"], "encerrava todos os processos RM.* apenas pelo nome.", "Use 'pep kill host' com seleção explícita."),
    new RemovedChain(["cmd"], "executava comandos de shell arbitrários.", "Execute o comando diretamente no terminal."),
    new RemovedChain(["clear"], "o modo de prompt digitado foi substituído pelo menu interativo.", "Execute 'pep' sem argumentos para o menu."),
    new RemovedChain(["cls"], "o modo de prompt digitado foi substituído pelo menu interativo.", "Execute 'pep' sem argumentos para o menu."),
    new RemovedChain(["exit"], "o modo de prompt digitado foi substituído pelo menu interativo.", "No menu, escolha 'Sair'."),
    new RemovedChain(["env", "configure"], ManualConfigReason, ManualConfigAlternative),
    new RemovedChain(["config", "init"], ManualConfigReason, ManualConfigAlternative),
    new RemovedChain(["login"],
      "o login do TFS agora é automático: quando o tf.exe pedir autenticação (TF30063), o PEP CLI abre o login e continua a operação.",
      "Execute normalmente o comando desejado (ex.: 'pep doctor' ou 'pep merge') em um terminal interativo."),
  ];
}
