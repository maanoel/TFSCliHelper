using PEPCliHelper.Infrastructure;

namespace PEPCliHelper.Interface;

/// <summary>
/// Elo da cadeia de comandos: declara o caminho (ex.: ["delete", "broker"]) e a ajuda,
/// e cria o builder. Não contém lógica de negócio.
/// </summary>
public interface ICommandChain
{
  CommandHelp Help { get; }

  ICommandBuilder CreateBuilder(AppServices services);
}

/// <summary>Valida argumentos, chama o caso de uso do Core e apresenta o resultado. Retorna o código de saída.</summary>
public interface ICommandBuilder
{
  Task<int> BuildAsync(CommandLine line, CancellationToken cancellationToken);
}
