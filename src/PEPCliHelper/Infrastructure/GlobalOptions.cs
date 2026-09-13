using System.Reflection;
using PEPCliHelper.Core.Common;

namespace PEPCliHelper.Infrastructure;

public static class AppInfo
{
  public static string Version =>
    typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
    ?? typeof(AppInfo).Assembly.GetName().Version?.ToString(3)
    ?? "0.0.0";
}

/// <summary>Opções válidas em qualquer comando (spec 002).</summary>
public sealed class GlobalOptions
{
  public bool NoColor { get; init; }

  public bool Ascii { get; init; }

  public bool Json { get; init; }

  public bool NonInteractive { get; init; }

  public bool Yes { get; init; }

  public bool Verbose { get; init; }

  public bool Help { get; init; }

  public bool ShowVersion { get; init; }

  public string? ConfigPath { get; init; }

  public static readonly IReadOnlyList<(string Name, string? Value, string Description)> Descriptions =
  [
    ("--help, -h", null, "Ajuda do comando."),
    ("--version", null, "Mostra a versão do PEP CLI."),
    ("--json", null, "Saída JSON sem cores, animações ou prompts (implica --non-interactive)."),
    ("--non-interactive", null, "Nunca pergunta; entrada ausente gera erro de uso."),
    ("--yes, -y", null, "Confirma um plano completamente especificado (não ignora validações)."),
    ("--no-color", null, "Desativa cores (também via variável NO_COLOR)."),
    ("--ascii", null, "Usa apenas caracteres ASCII."),
    ("--config", "<arquivo>", "Usa este arquivo de configuração (precede PEPCLI_CONFIG e o arquivo do usuário)."),
    ("--verbose", null, "Mostra detalhes das ferramentas."),
  ];

  /// <summary>Separa opções globais dos tokens do comando.</summary>
  public static (GlobalOptions Options, List<string> Tokens) Parse(IReadOnlyList<string> args, Func<string, string?> readEnvironment)
  {
    var tokens = new List<string>();
    bool noColor = false, ascii = false, json = false, nonInteractive = false, yes = false, verbose = false, help = false, version = false;
    string? config = null;

    for (var i = 0; i < args.Count; i++)
    {
      var arg = args[i];
      switch (arg.ToLowerInvariant())
      {
        case "--no-color": noColor = true; break;
        case "--ascii": ascii = true; break;
        case "--json": json = true; break;
        case "--non-interactive": nonInteractive = true; break;
        case "--yes" or "-y": yes = true; break;
        case "--verbose": verbose = true; break;
        case "--help" or "-h" or "/?": help = true; break;
        case "--version": version = true; break;
        case "--config":
          if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            throw new UsageException("A opção --config exige o caminho do arquivo.", "Ex.: pep doctor --config \"D:\\configs\\pep.json\"");
          config = args[++i];
          break;
        default:
          if (arg.StartsWith("--config=", StringComparison.OrdinalIgnoreCase))
            config = arg["--config=".Length..];
          else
            tokens.Add(arg);
          break;
      }
    }

    var options = new GlobalOptions
    {
      NoColor = noColor || !string.IsNullOrEmpty(readEnvironment("NO_COLOR")),
      Ascii = ascii,
      Json = json,
      NonInteractive = nonInteractive || json,
      Yes = yes,
      Verbose = verbose,
      Help = help,
      ShowVersion = version,
      ConfigPath = config,
    };

    return (options, tokens);
  }
}
