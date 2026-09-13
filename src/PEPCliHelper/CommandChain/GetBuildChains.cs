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

public sealed class BuildAllChain : ICommandChain
{
  private static readonly OptionSpec[] Options =
  [
    GetBuildOptions.Project,
    new OptionSpec("configuration", "<cfg>", "Configuração do MSBuild (ex.: Debug, Release)."),
    new OptionSpec("continue-on-failure", null, "Continua nas próximas soluções após falha."),
    GetBuildOptions.DryRun,
  ];

  public CommandHelp Help { get; } = new()
  {
    Path = ["build", "all"],
    Category = "Get e build",
    Summary = "Compila Sau-Saude e Sau-PEP em todas as versões ativas (MSBuild, sequencial).",
    Details = "Não executa get e não encerra o RM.Host: se o host da versão estiver aberto, o build é bloqueado com orientação.",
    Options = Options,
    Examples = [new("pep build all", "Todas as versões ativas."), new("pep build all --project back --configuration Release", "Somente RM.Pep.sln em Release.")],
  };

  internal static IReadOnlyList<OptionSpec> SharedOptions => Options;

  public ICommandBuilder CreateBuilder(AppServices services) => new BuildBuilder(services, all: true);
}

public sealed class BuildVersionChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["build", "version"],
    Category = "Get e build",
    Summary = "Compila Sau-Saude e Sau-PEP de uma versão.",
    Arguments = [new ArgumentSpec("versao", "Versão (ex.: atual, 2606).")],
    Options = BuildAllChain.SharedOptions,
    Examples = [new("pep build version 2606", "Compila a 12.1.2606.")],
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new BuildBuilder(services, all: false);
}
