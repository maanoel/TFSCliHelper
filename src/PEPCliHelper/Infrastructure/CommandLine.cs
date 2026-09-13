using System.Globalization;
using PEPCliHelper.Core.Common;

namespace PEPCliHelper.Infrastructure;

public sealed record OptionSpec(string Name, string? ValueName, string Description, bool Repeatable = false, string? ShortName = null)
{
  public bool TakesValue => ValueName is not null;

  public string Display => (ShortName is null ? $"--{Name}" : $"--{Name}, -{ShortName}") + (ValueName is null ? string.Empty : $" {ValueName}");
}

public sealed record ArgumentSpec(string Name, string Description, bool Required = true)
{
  public string Display => Required ? $"<{Name}>" : $"[{Name}]";
}

public sealed record CommandExample(string Command, string Description);

/// <summary>Ajuda e contrato de parsing de um comando.</summary>
public sealed class CommandHelp
{
  public required IReadOnlyList<string> Path { get; init; }

  public required string Summary { get; init; }

  public required string Category { get; init; }

  public string? Details { get; init; }

  public IReadOnlyList<ArgumentSpec> Arguments { get; init; } = [];

  public IReadOnlyList<OptionSpec> Options { get; init; } = [];

  public IReadOnlyList<CommandExample> Examples { get; init; } = [];

  /// <summary>Aceita posicionais extras (sintaxe antiga do merge).</summary>
  public int MaxPositionals { get; init; } = -1;

  public bool Hidden { get; init; }

  public string Name => string.Join(' ', Path);

  public string Usage =>
    $"pep {Name}"
    + string.Concat(Arguments.Select(a => " " + a.Display))
    + (Options.Count > 0 ? " [opções]" : string.Empty);
}

/// <summary>Argumentos já separados do caminho do comando e validados contra a ajuda.</summary>
public sealed class CommandLine
{
  private readonly Dictionary<string, List<string>> _options;
  private readonly HashSet<string> _flags;

  private CommandLine(CommandHelp help, GlobalOptions global, IReadOnlyList<string> positionals, Dictionary<string, List<string>> options, HashSet<string> flags)
  {
    Help = help;
    Global = global;
    Positionals = positionals;
    _options = options;
    _flags = flags;
  }

  public CommandHelp Help { get; }

  public GlobalOptions Global { get; }

  public IReadOnlyList<string> Positionals { get; }

  public string? Positional(int index) => index < Positionals.Count ? Positionals[index] : null;

  public string? Option(string name) => _options.TryGetValue(name, out var values) ? values[^1] : null;

  public IReadOnlyList<string> OptionValues(string name) => _options.TryGetValue(name, out var values) ? values : [];

  public bool Flag(string name) => _flags.Contains(name);

  public int? IntOption(string name)
  {
    var value = Option(name);
    if (value is null)
      return null;
    return ParsePositiveInt(value, $"--{name}");
  }

  public static int ParsePositiveInt(string value, string what)
  {
    var clean = value.Trim().TrimStart('C', 'c');
    if (!int.TryParse(clean, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number <= 0)
      throw new UsageException($"Valor inválido para {what}: '{value}'. Informe um número inteiro positivo.");
    return number;
  }

  public static CommandLine Parse(IReadOnlyList<string> tokens, CommandHelp help, GlobalOptions global)
  {
    var positionals = new List<string>();
    var options = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    for (var i = 0; i < tokens.Count; i++)
    {
      var token = tokens[i];
      if (!token.StartsWith('-') || token.Length == 1 || IsNegativeNumber(token))
      {
        positionals.Add(token);
        continue;
      }

      string name;
      string? inlineValue = null;
      var isLong = token.StartsWith("--", StringComparison.Ordinal);
      if (isLong)
      {
        name = token[2..];
        var equals = name.IndexOf('=');
        if (equals > 0)
        {
          inlineValue = name[(equals + 1)..];
          name = name[..equals];
        }
      }
      else
      {
        name = token[1..];
      }

      // Forma longa só com "--nome"; forma curta só com "-x".
      var spec = help.Options.FirstOrDefault(o => isLong
        ? o.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
        : o.ShortName is not null && o.ShortName.Equals(name, StringComparison.Ordinal));

      if (spec is null)
      {
        throw new UsageException($"Opção desconhecida '{token}' para 'pep {help.Name}'.", $"Veja as opções com 'pep {help.Name} --help'.");
      }

      if (!spec.TakesValue)
      {
        if (inlineValue is not null)
          throw new UsageException($"A opção --{spec.Name} não recebe valor.", $"Veja 'pep {help.Name} --help'.");
        flags.Add(spec.Name);
        continue;
      }

      var value = inlineValue;
      if (value is null)
      {
        if (i + 1 >= tokens.Count || (tokens[i + 1].StartsWith('-') && !IsNegativeNumber(tokens[i + 1])))
          throw new UsageException($"A opção --{spec.Name} exige um valor {spec.ValueName}.", $"Veja 'pep {help.Name} --help'.");
        value = tokens[++i];
      }

      if (!options.TryGetValue(spec.Name, out var list))
        options[spec.Name] = list = [];
      else if (!spec.Repeatable)
        throw new UsageException($"A opção --{spec.Name} foi informada mais de uma vez.", $"Veja 'pep {help.Name} --help'.");

      list.Add(value);
    }

    var max = help.MaxPositionals >= 0 ? help.MaxPositionals : help.Arguments.Count;
    if (positionals.Count > max)
    {
      throw new UsageException(
        $"Argumentos inesperados para 'pep {help.Name}': {string.Join(' ', positionals.Skip(max))}.",
        $"Uso: {help.Usage}. Caminhos com espaços devem estar entre aspas.");
    }

    return new CommandLine(help, global, positionals, options, flags);
  }

  private static bool IsNegativeNumber(string token) => token.Length > 1 && token[0] == '-' && token.Skip(1).All(char.IsDigit);
}
