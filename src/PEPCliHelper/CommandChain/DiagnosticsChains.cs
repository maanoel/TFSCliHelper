using PEPCliHelper.CommandsBuilders;
using PEPCliHelper.Infrastructure;
using PEPCliHelper.Interface;

namespace PEPCliHelper.CommandChain;

public sealed class DoctorChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["doctor"],
    Category = "Diagnóstico e TFVC",
    Summary = "Diagnóstico completo: configuração, ferramentas, conexão, catálogo, mapeamentos, build e disco.",
    Details = "Somente leitura. Não corrige nada; cada falha traz o próximo passo.\nSem credencial do tf.exe (TF30063), em terminal interativo abre o login do TFS automaticamente e continua.",
    Examples = [new("pep doctor", "Primeira coisa a rodar quando algo falhar.")],
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new DoctorBuilder(services);
}

public sealed class WorkspaceListChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["workspace", "list"],
    Category = "Diagnóstico e TFVC",
    Summary = "Lista os workspaces TFVC da coleção.",
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new WorkspaceListBuilder(services);
}

public sealed class WorkspaceInspectChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["workspace", "inspect"],
    Category = "Diagnóstico e TFVC",
    Summary = "Mostra mapeamento efetivo, workspace, cloaking e divergências de uma versão.",
    Arguments = [new ArgumentSpec("versao", "Versão (ex.: atual, 2606).")],
    Options = [new OptionSpec("project", "<alias>", "Somente um projeto.", ShortName: "p")],
    Examples = [new("pep workspace inspect 2606 --project back", "Diagnostica o Sau-PEP da 12.1.2606.")],
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new WorkspaceInspectBuilder(services);
}

public sealed class PendingListChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["pending", "list"],
    Category = "Diagnóstico e TFVC",
    Summary = "Lista pending changes por versão e projeto (todas as ativas se a versão for omitida).",
    Arguments = [new ArgumentSpec("versao", "Versão (opcional).", Required: false)],
    Options = [new OptionSpec("project", "<alias>", "Somente um projeto.", ShortName: "p")],
    Examples = [new("pep pending list", "Todas as versões ativas."), new("pep pending list 2606 --project back", "Revisão após merge.")],
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new PendingListBuilder(services);
}

public sealed class ChangesetShowChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["changeset", "show"],
    Category = "Diagnóstico e TFVC",
    Summary = "Mostra os itens de um changeset; com --project e --source separa incluídos e excluídos.",
    Arguments = [new ArgumentSpec("id", "Número do changeset.")],
    Options =
    [
      new OptionSpec("project", "<alias>", "Projeto para classificar o escopo.", ShortName: "p"),
      new OptionSpec("source", "<versao>", "Versão de origem para classificar o escopo.", ShortName: "s"),
    ],
    Examples = [new("pep changeset show 861799 --project back --source atual", "Escopo do merge.")],
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new ChangesetShowBuilder(services);
}

public sealed class HistoryListChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["history", "list"],
    Category = "Histórico",
    Summary = "Lista as últimas execuções registradas pelo PEP CLI.",
    Options = [new OptionSpec("limit", "<n>", "Quantidade (padrão 20).")],
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new HistoryListBuilder(services);
}

public sealed class HistoryShowChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["history", "show"],
    Category = "Histórico",
    Summary = "Detalha uma execução: etapas, resultado por alvo e orientações.",
    Arguments = [new ArgumentSpec("id", "Id da execução (ou prefixo único).")],
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new HistoryShowBuilder(services);
}

public sealed class HelpChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["help"],
    Category = "Ajuda",
    Summary = "Ajuda geral ou de um comando.",
    MaxPositionals = 3,
    Arguments = [new ArgumentSpec("comando", "Comando (opcional).", Required: false)],
    Examples = [new("pep help merge", "Ajuda do merge.")],
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new HelpBuilder(services);
}

public sealed class VersionChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["version"],
    Category = "Ajuda",
    Summary = "Versão do PEP CLI e do ambiente (ferramentas, configuração, catálogo).",
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new VersionBuilder(services);
}
