using PEPCliHelper.CommandsBuilders;
using PEPCliHelper.Infrastructure;
using PEPCliHelper.Interface;

namespace PEPCliHelper.CommandChain;

public sealed class MergeChain : ICommandChain
{
  public CommandHelp Help { get; } = new()
  {
    Path = ["merge"],
    Category = "Merge",
    Summary = "Propaga um changeset para uma ou mais versões. Resultado em Pending Changes, sem check-in.",
    Details =
      "Sem opções, pergunta projeto, origem, destinos e changeset.\n" +
      "Antes de executar valida changeset, escopo do projeto, mapeamentos, relação de merge, pending changes,\n" +
      "conflitos, atualização local e preview. Nunca usa baseless, nunca resolve conflitos e nunca faz check-in.\n" +
      "Por padrão o merge roda em todos os destinos prontos, mesmo após falha ou conflito em um deles (--stop-on-failure interrompe).\n" +
      "Sintaxe antiga 'merge <projeto> <versao> <changeset>': a versão é a ORIGEM e os destinos são obrigatórios.",
    MaxPositionals = 3,
    Options =
    [
      new OptionSpec("project", "<alias>", "Projeto: back (Sau-PEP) ou sau (Sau-Saude).", ShortName: "p"),
      new OptionSpec("source", "<versao>", "Versão de origem (ex.: atual, 12.1.2606, 2606).", ShortName: "s"),
      new OptionSpec("target", "<versao>", "Versão de destino.", Repeatable: true, ShortName: "t"),
      new OptionSpec("all-legacy", null, "Todas as legadas ativas do catálogo como destino."),
      new OptionSpec("changeset", "<id>", "Changeset de origem (um único changeset).", ShortName: "c"),
      new OptionSpec("dry-run", null, "Somente valida e mostra o plano. Não altera nada."),
      new OptionSpec("stop-on-failure", null, "Interrompe os destinos seguintes após falha, conflito ou bloqueio (rede, autenticação, indeterminado e cancelamento sempre interrompem)."),
      new OptionSpec("continue-on-failure", null, "Padrão; mantido por compatibilidade."),
    ],
    Examples =
    [
      new("pep merge", "Modo guiado."),
      new("pep merge --project back --source atual --target 2606 --changeset 861799", "Um destino."),
      new("pep merge --project back --source atual --target 2606 --target 2602 --changeset 861799", "Dois destinos."),
      new("pep merge --project sau --source atual --all-legacy --changeset 861799", "Todas as legadas ativas."),
      new("pep merge --project back --source atual --all-legacy --changeset 861799 --dry-run", "Simulação."),
      new("pep merge --project back --source atual --target 2606 --changeset 861799 --yes --non-interactive", "Automação."),
    ],
  };

  public ICommandBuilder CreateBuilder(AppServices services) => new MergeBuilder(services);
}
