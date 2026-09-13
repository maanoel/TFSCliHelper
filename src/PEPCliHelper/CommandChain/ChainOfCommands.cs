using PEPCliHelper.Interface;

namespace PEPCliHelper.CommandChain;

public sealed record ChainMatch(ICommandChain Chain, IReadOnlyList<string> Rest);

/// <summary>
/// Encontra o comando pelo caminho mais longo que casa com os tokens e para no primeiro resultado
/// (corrige o legado, que executava todos os elos e sempre imprimia "comando não existe").
/// </summary>
public sealed class ChainOfCommands
{
  public ChainOfCommands(IEnumerable<ICommandChain> chains)
  {
    Chains = chains.ToList();

    var duplicated = Chains.GroupBy(c => c.Help.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
    if (duplicated is not null)
      throw new InvalidOperationException($"Comando registrado mais de uma vez: {duplicated.Key}");
  }

  public IReadOnlyList<ICommandChain> Chains { get; }

  public ChainMatch? Match(IReadOnlyList<string> tokens)
  {
    ICommandChain? best = null;
    foreach (var chain in Chains)
    {
      var path = chain.Help.Path;
      if (path.Count > tokens.Count)
        continue;

      var matches = path.Select((segment, i) => segment.Equals(tokens[i], StringComparison.OrdinalIgnoreCase)).All(x => x);
      if (matches && (best is null || path.Count > best.Help.Path.Count))
        best = chain;
    }

    return best is null ? null : new ChainMatch(best, tokens.Skip(best.Help.Path.Count).ToList());
  }

  /// <summary>Comandos que começam com o primeiro token (para sugerir em erro de digitação).</summary>
  public IReadOnlyList<string> Suggest(IReadOnlyList<string> tokens) =>
    tokens.Count == 0
      ? []
      : Chains
        .Where(c => !c.Help.Hidden && c.Help.Path[0].StartsWith(tokens[0], StringComparison.OrdinalIgnoreCase))
        .Select(c => $"pep {c.Help.Name}")
        .Take(6)
        .ToList();
}
