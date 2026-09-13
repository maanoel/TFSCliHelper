using System.Globalization;

namespace PEPCliHelper.Tests.Fixtures;

/// <summary>Saída capturada do tf.exe por scripts/capture-tf-fixtures.ps1.</summary>
public sealed record TfFixture(string CommandLine, int? ExitCode, string StdOut, string StdErr)
{
  public string Body => string.IsNullOrEmpty(StdErr) ? StdOut : $"{StdOut}\n{StdErr}";
}

/// <summary>Localiza e lê as fixtures em tests/fixtures/tf-ptbr a partir da raiz do repositório.</summary>
public static class FixtureLoader
{
  public const string StdErrSeparator = "--- stderr ---";

  private static readonly Lazy<string?> FixturesDirectory = new(FindFixturesDirectory);

  public static string? FixturesPath => FixturesDirectory.Value;

  public static string? PathOf(string fileName) =>
    FixturesPath is null ? null : Path.Combine(FixturesPath, fileName);

  public static TfFixture? TryLoad(string fileName)
  {
    var path = PathOf(fileName);
    return path is not null && File.Exists(path) ? Parse(File.ReadAllText(path)) : null;
  }

  public static TfFixture Load(string fileName) =>
    TryLoad(fileName) ?? throw new FileNotFoundException($"Fixture '{fileName}' não encontrada.", PathOf(fileName));

  /// <summary>Cabeçalho: linhas iniciais "# chave: valor"; o restante é stdout, e stderr após o separador.</summary>
  public static TfFixture Parse(string content)
  {
    var lines = content.Replace("\r\n", "\n").Split('\n');
    var commandLine = string.Empty;
    int? exitCode = null;
    var index = 0;

    for (; index < lines.Length && lines[index].StartsWith('#'); index++)
    {
      var header = lines[index][1..].Trim();
      if (header.StartsWith("tf ", StringComparison.Ordinal) || header == "tf")
        commandLine = header;
      else if (header.StartsWith("exitCode:", StringComparison.OrdinalIgnoreCase)
        && int.TryParse(header["exitCode:".Length..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
        exitCode = code;
    }

    var body = string.Join('\n', lines[index..]);
    var separator = body.IndexOf("\n" + StdErrSeparator + "\n", StringComparison.Ordinal);
    return separator < 0
      ? new TfFixture(commandLine, exitCode, body, string.Empty)
      : new TfFixture(commandLine, exitCode, body[..separator], body[(separator + StdErrSeparator.Length + 2)..]);
  }

  private static string? FindFixturesDirectory()
  {
    for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
    {
      if (File.Exists(Path.Combine(current.FullName, "PEPCliHelper.sln")))
        return Path.Combine(current.FullName, "tests", "fixtures", "tf-ptbr");
    }

    return null;
  }
}

/// <summary>Fact executado apenas quando a fixture existe e foi capturada com exit code 0.</summary>
public sealed class FixtureFactAttribute : FactAttribute
{
  public FixtureFactAttribute(string fileName)
  {
    FileName = fileName;
    var fixture = FixtureLoader.TryLoad(fileName);
    if (fixture is null)
      Skip = $"Fixture '{fileName}' ausente em tests/fixtures/tf-ptbr (capture com scripts/capture-tf-fixtures.ps1).";
    else if (fixture.ExitCode != 0)
      Skip = $"Fixture '{fileName}' capturada com exit code {fixture.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "desconhecido"}.";
  }

  public string FileName { get; }
}
