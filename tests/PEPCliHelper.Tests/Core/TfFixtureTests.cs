using System.Globalization;
using System.Text.RegularExpressions;
using PEPCliHelper.Core.Execution;
using PEPCliHelper.Core.Tfvc;
using PEPCliHelper.Tests.Fakes;
using PEPCliHelper.Tests.Fixtures;

namespace PEPCliHelper.Tests.Core;

/// <summary>
/// Parsers contra saídas reais do tf.exe (tests/fixtures/tf-ptbr). Asserts são invariantes independentes
/// de formato; cada teste é ignorado se a fixture não existir ou tiver exit code diferente de 0.
/// </summary>
public class TfFixtureTests
{
  private const string LocalPlaceholder = @"C:\fixture-sem-caminho";

  private static (TfExeClient Client, FakeCommandExecutor Executor) ClientReturning(TfFixture fixture)
  {
    var executor = new FakeCommandExecutor
    {
      Respond = _ => new CommandResult(0, fixture.StdOut, fixture.StdErr, TimeSpan.Zero),
    };
    return (new TfExeClient(executor, _ => Task.FromResult("tf.exe"), _ => true), executor);
  }

  /// <summary>Caminho local entre "tf &lt;verbo&gt; " e " /recursive" no cabeçalho (aceita espaços no caminho).</summary>
  private static string LocalPathFromHeader(TfFixture fixture, string verb)
  {
    var match = Regex.Match(fixture.CommandLine, $@"^tf\s+{verb}\s+(?<path>.+?)\s+/recursive", RegexOptions.IgnoreCase);
    return match.Success ? match.Groups["path"].Value.Trim() : LocalPlaceholder;
  }

  [FixtureFact("workfold.txt")]
  public void WorkfoldParser_FixtureReal_ExtraiMapeamentoDeServidorParaCaminhoAbsoluto()
  {
    var fixture = FixtureLoader.Load("workfold.txt");

    var info = WorkfoldParser.Parse(fixture.Body);

    Assert.NotEmpty(info.Mappings);
    Assert.All(info.Mappings, m => Assert.StartsWith("$/", m.ServerPath));
    Assert.Contains(info.Mappings, m => !m.IsCloaked);
    Assert.All(info.Mappings.Where(m => !m.IsCloaked), m => Assert.True(Path.IsPathRooted(m.LocalPath), $"Caminho local não absoluto: '{m.LocalPath}'."));
  }

  [FixtureFact("changeset.txt")]
  public async Task GetChangesetAsync_FixtureReal_ExtraiItensSomenteDeLinhasDeItem()
  {
    var fixture = FixtureLoader.Load("changeset.txt");
    var number = Regex.Match(fixture.CommandLine, @"^tf\s+changeset\s+(?<n>\d+)", RegexOptions.IgnoreCase);
    var changeset = number.Success ? int.Parse(number.Groups["n"].Value, CultureInfo.InvariantCulture) : 1;
    var (client, _) = ClientReturning(fixture);

    var query = await client.GetChangesetAsync(changeset, "https://colecao", CancellationToken.None);

    Assert.True(query.Result.IsSuccess);
    Assert.NotNull(query.Changeset);
    Assert.NotEmpty(query.Changeset.Items);
    var itemLines = TfOutputParser.Lines(fixture.Body)
      .Where(l => Regex.IsMatch(l, @"^\s*(\p{L}+(,\s*\p{L}+)*\s+)?\$/"))
      .ToList();
    Assert.All(query.Changeset.Items, item =>
    {
      Assert.StartsWith("$/", item);
      Assert.Contains(itemLines, line => line.Contains(item, StringComparison.OrdinalIgnoreCase));
    });
  }

  [FixtureFact("status-detailed.txt")]
  public async Task GetPendingChangesAsync_FixtureReal_ConfiavelQuandoHaItens()
  {
    var fixture = FixtureLoader.Load("status-detailed.txt");
    var localPath = LocalPathFromHeader(fixture, "status");
    var (client, executor) = ClientReturning(fixture);

    var query = await client.GetPendingChangesAsync(localPath, CancellationToken.None);

    Assert.True(query.Result.IsSuccess);
    Assert.Contains("status", executor.Executed.Single().Arguments);
    if (TfOutputParser.ExtractServerPaths(fixture.Body).Count > 0)
    {
      Assert.True(query.IsReliable, $"tf status citou itens de servidor, mas nenhuma linha contém '{localPath}'.");
      Assert.NotEmpty(query.Items);
    }
  }

  [FixtureFact("merge-candidate.txt")]
  public void ParseLeadingNumbers_FixtureReal_RetornaSomenteInteirosPositivos()
  {
    var fixture = FixtureLoader.Load("merge-candidate.txt");

    var changesets = TfOutputParser.ParseLeadingNumbers(fixture.Body);

    Assert.All(changesets, n => Assert.True(n > 0, $"Changeset inválido: {n}."));
  }

  [FixtureFact("merge-preview.txt")]
  public async Task PreviewMergeAsync_FixtureReal_SucessoComItensDeServidor()
  {
    var fixture = FixtureLoader.Load("merge-preview.txt");
    var (client, _) = ClientReturning(fixture);

    var query = await client.PreviewMergeAsync("$/origem", "$/destino", 1, LocalPlaceholder, CancellationToken.None);

    Assert.Equal(TfStatus.Success, query.Result.Status);
    Assert.All(query.Items, item => Assert.Contains("$/", item));
  }

  [FixtureFact("get-preview.txt")]
  public async Task PreviewGetAsync_FixtureReal_SucessoSemExcecao()
  {
    var fixture = FixtureLoader.Load("get-preview.txt");
    var localPath = LocalPathFromHeader(fixture, "get");
    var (client, _) = ClientReturning(fixture);

    var query = await client.PreviewGetAsync(localPath, CancellationToken.None);

    Assert.Equal(TfStatus.Success, query.Result.Status);
    Assert.All(query.Items, item => Assert.Contains(localPath, item, StringComparison.OrdinalIgnoreCase));
  }

  [FixtureFact("resolve-preview.txt")]
  public async Task GetConflictsAsync_FixtureReal_SucessoSemExcecao()
  {
    var fixture = FixtureLoader.Load("resolve-preview.txt");
    var localPath = LocalPathFromHeader(fixture, "resolve");
    var (client, _) = ClientReturning(fixture);

    var query = await client.GetConflictsAsync(localPath, CancellationToken.None);

    Assert.Equal(TfStatus.Success, query.Result.Status);
  }
}

public class FixtureLoaderTests
{
  [Fact]
  public void FixtureFact_ArquivoAusente_DefineSkip()
  {
    var attribute = new FixtureFactAttribute("nao-existe-" + Guid.NewGuid().ToString("N") + ".txt");

    Assert.False(string.IsNullOrEmpty(attribute.Skip));
  }

  [Fact]
  public void FixtureLoader_RaizDoRepositorio_LocalizaPastaDeFixtures()
  {
    Assert.NotNull(FixtureLoader.FixturesPath);
    Assert.True(Directory.Exists(FixtureLoader.FixturesPath), $"Pasta não encontrada: {FixtureLoader.FixturesPath}.");
  }

  [Fact]
  public void Parse_CabecalhoEStderr_SeparaExitCodeComandoESaidas()
  {
    const string content = "# tf status C:\\A B /recursive /format:detailed\r\n# exitCode: 100\r\n# codePage: 1252\r\n# capturado: 2026-09-13T10:00:00\r\nlinha 1\r\n\n--- stderr ---\nTF400324: erro\r\n";

    var fixture = FixtureLoader.Parse(content);

    Assert.Equal(@"tf status C:\A B /recursive /format:detailed", fixture.CommandLine);
    Assert.Equal(100, fixture.ExitCode);
    Assert.Contains("linha 1", fixture.StdOut);
    Assert.DoesNotContain("stderr", fixture.StdOut);
    Assert.StartsWith("TF400324", fixture.StdErr);
  }
}
