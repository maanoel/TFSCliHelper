using PEPCliHelper.CommandsBuilders;
using PEPCliHelper.Infrastructure;
using PEPCliHelper.Interface;

namespace PEPCliHelper.CommandChain;

internal static class GetBuildOptions
{
  public static readonly OptionSpec Project = new("project", "<alias>", "Somente um projeto: back ou sau.", ShortName: "p");
  public static readonly OptionSpec DryRun = new("dry-run", null, "Somente valida e mostra o plano.");
}

public sealed class GetAllChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["get", "all"],
    Category = "Get e build",
    Summary = "Get de Sau-PEP e Sau-Saude em todas as versões ativas.",
    Details = "Valida mapeamentos e pending changes, mostra o plano e executa 'tf get /recursive' sem /force.",
    Options = [GetBuildOptions.Project, GetBuildOptions.DryRun],
    Examples = [new("pep get all", "Todas as versões ativas."), new("pep get all --project back --dry-run", "Plano apenas do Sau-PEP.")],
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new GetBuilder(services, all: true);
}

public sealed class GetVersionChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["get", "version"],
    Category = "Get e build",
    Summary = "Get de Sau-PEP e Sau-Saude em uma versão.",
    Arguments = [new ArgumentSpec("versao", "Versão (ex.: atual, 2606, 12.1.2606).")],
    Options = [GetBuildOptions.Project, GetBuildOptions.DryRun],
    Examples = [new("pep get version 2606", "Get da 12.1.2606."), new("pep get version atual --project sau", "Somente Sau-Saude da atual.")],
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new GetBuilder(services, all: false);
}

internal static class BuildOptions
{
  public const string OrderDetails =
    "Ordem: versões atual → legadas; em cada versão o projeto principal (PEP, 'principal' na configuração) compila primeiro, " +
    "depois os demais selecionados. Compila sem pedir confirmação (Ctrl+C interrompe). Não executa get. Antes de compilar encerra o RM.Host das versões selecionadas (como 'pep kill host', gracioso); se não encerrar, a versão é bloqueada com orientação.";

  public static readonly IReadOnlyList<OptionSpec> Shared =
  [
    new OptionSpec("project", "<alias>", "Projeto a compilar (repetível): back (PEP) ou sau (Saúde). Padrão: todos.", Repeatable: true, ShortName: "p"),
    new OptionSpec("configuration", "<cfg>", "Configuração do MSBuild (ex.: Debug, Release)."),
    new OptionSpec("continue-on-failure", null, "Continua nas próximas soluções após falha."),
    GetBuildOptions.DryRun,
  ];
}

public sealed class BuildChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["build"],
    Category = "Get e build",
    Summary = "Compila as versões e projetos selecionados (MSBuild, sequencial; PEP sempre primeiro).",
    Details = "Sem --version/--all, em terminal interativo pergunta as versões (atual marcada) e os projetos (todos marcados, exceto Sau-Saúde)." + BuildOptions.OrderDetails,
    Options =
    [
      new OptionSpec("version", "<versao>", "Versão a compilar (repetível; ex.: atual, 2606).", Repeatable: true),
      new OptionSpec("all", null, "Todas as versões ativas."),
      .. BuildOptions.Shared,
    ],
    Examples =
    [
      new("pep build", "Seleciona versões e projetos interativamente."),
      new("pep build --version 2606 --project back --dry-run", "Plano apenas do PEP (RM.Pep.sln) na 12.1.2606."),
      new("pep build --all --project sau", "Somente o Saúde em todas as versões ativas."),
    ],
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new BuildBuilder(services, BuildScope.Selection);
}

public sealed class BuildAllChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["build", "all"],
    Category = "Get e build",
    Summary = "Compila Sau-PEP e Sau-Saude em todas as versões ativas (MSBuild, sequencial; PEP primeiro).",
    Details = BuildOptions.OrderDetails,
    Options = BuildOptions.Shared,
    Examples = [new("pep build all", "Todas as versões ativas."), new("pep build all --project back --configuration Release", "Somente RM.Pep.sln em Release.")],
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new BuildBuilder(services, BuildScope.All);
}

public sealed class BuildVersionChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["build", "version"],
    Category = "Get e build",
    Summary = "Compila Sau-PEP e Sau-Saude de uma versão (PEP primeiro).",
    Details = BuildOptions.OrderDetails,
    Arguments = [new ArgumentSpec("versao", "Versão (ex.: atual, 2606).")],
    Options = BuildOptions.Shared,
    Examples = [new("pep build version 2606", "Compila a 12.1.2606.")],
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new BuildBuilder(services, BuildScope.Version);
}
