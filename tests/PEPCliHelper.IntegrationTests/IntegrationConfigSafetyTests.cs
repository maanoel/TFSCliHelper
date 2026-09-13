namespace PEPCliHelper.IntegrationTests;

/// <summary>Travas de IntegrationConfig. Fatos comuns: não usam PEPCLI_IT_CONFIG nem tf.exe e sempre rodam.</summary>
public class IntegrationConfigSafetyTests
{
  private static IntegrationConfig ValidConfig() => new()
  {
    IsTestCollection = true,
    Collection = "https://servidor/ColecaoTeste",
    SourceServer = "$/Teste-PEP/atual/release",
    SourceLocal = @"C:\TestePEP\atual\release",
    Targets =
    [
      new IntegrationTarget { ServerPath = "$/Teste-PEP/Legado/A", LocalPath = @"C:\TestePEP\Legado\A" },
      new IntegrationTarget { ServerPath = "$/Teste-PEP/Legado/B", LocalPath = @"C:\TestePEP\Legado\B" },
    ],
    NoRelationTarget = new IntegrationTarget { ServerPath = "$/Teste-PEP/Legado/SemRelacao", LocalPath = @"C:\TestePEP\Legado\SemRelacao" },
    UnmappedTarget = new IntegrationTarget { ServerPath = "$/Teste-PEP/Legado/A", LocalPath = @"C:\TestePEP\SemMapeamento" },
  };

  [Fact]
  public void Validate_ConfiguracaoDeTeste_SemProblemas()
  {
    Assert.Empty(IntegrationConfig.Validate(ValidConfig()));
  }

  [Fact]
  public void Validate_ColecaoDeTesteFalse_Recusa()
  {
    var config = ValidConfig();
    config.IsTestCollection = false;

    Assert.Contains(IntegrationConfig.Validate(config), p => p.Contains("colecaoDeTeste"));
  }

  [Fact]
  public void Validate_ColecaoVaziaOuMenosDeDoisDestinos_Recusa()
  {
    var config = ValidConfig();
    config.Collection = " ";
    config.Targets.RemoveAt(1);

    var problems = IntegrationConfig.Validate(config);

    Assert.Contains(problems, p => p.Contains("'collection'"));
    Assert.Contains(problems, p => p.Contains("'targets'"));
  }

  [Theory]
  [InlineData("source", "$/Linha-RM/atual/release")]
  [InlineData("source", "$/linha-rm/Legado/12.1.2606")]
  [InlineData("target", "$/Linha-RM/Legado/12.1.2606/Sau-PEP")]
  [InlineData("noRelation", "$/LINHA-RM/Legado/X")]
  [InlineData("unmapped", "$/Linha-RM")]
  [InlineData("target", "$/Teste-PEP/../Linha-RM/atual")]
  [InlineData("target", "Teste-PEP/Legado/A")]
  [InlineData("target", "")]
  public void Validate_CaminhoDeServidorDaEquipeOuInvalido_Recusa(string where, string serverPath)
  {
    var config = ValidConfig();
    switch (where)
    {
      case "source": config.SourceServer = serverPath; break;
      case "target": config.Targets[1].ServerPath = serverPath; break;
      case "noRelation": config.NoRelationTarget!.ServerPath = serverPath; break;
      default: config.UnmappedTarget!.ServerPath = serverPath; break;
    }

    Assert.NotEmpty(IntegrationConfig.Validate(config));
  }

  [Theory]
  [InlineData("source", @"C:\Linha-RM\Atual\Release")]
  [InlineData("source", @"c:\linha-rm")]
  [InlineData("target", @"C:/Linha-RM/Legado/12.1.2606")]
  [InlineData("target", @"C:\TestePEP\..\Linha-RM\Legado")]
  [InlineData("noRelation", @"\\?\C:\Linha-RM\Legado")]
  [InlineData("noRelation", @"\\localhost\C$\Linha-RM")]
  [InlineData("unmapped", @"C:\LINHA-~1\Legado")]
  [InlineData("unmapped", @"TestePEP\SemMapeamento")]
  [InlineData("unmapped", @"C:TestePEP")]
  [InlineData("target", "")]
  public void Validate_CaminhoLocalDaEquipeOuInvalido_Recusa(string where, string localPath)
  {
    var config = ValidConfig();
    switch (where)
    {
      case "source": config.SourceLocal = localPath; break;
      case "target": config.Targets[0].LocalPath = localPath; break;
      case "noRelation": config.NoRelationTarget!.LocalPath = localPath; break;
      default: config.UnmappedTarget!.LocalPath = localPath; break;
    }

    Assert.NotEmpty(IntegrationConfig.Validate(config));
  }

  [Theory]
  [InlineData("checkin", "C:\\TestePEP")]
  [InlineData("undo", "C:\\TestePEP", "/recursive")]
  [InlineData("merge", "/baseless", "$/a", "$/b")]
  [InlineData("merge", "/FORCE", "$/a", "$/b")]
  [InlineData("resolve", "C:\\TestePEP", "/auto:AcceptTheirs")]
  [InlineData("resolve", "C:\\TestePEP", "/recursive", "/noprompt")]
  [InlineData("workfold", "/map", "$/a", "C:\\TestePEP")]
  public void RecordingExecutor_ComandoProibido_RecusaSemChamarExecutorReal(params string[] arguments)
  {
    var inner = new ThrowingExecutor();
    var recorder = new RecordingCommandExecutor(inner);

    Assert.Throws<InvalidOperationException>(() => { _ = recorder.ExecuteAsync(new PEPCliHelper.Core.Execution.Command("tf.exe", arguments)); });
    Assert.Empty(recorder.Commands);
    Assert.False(inner.Called);
  }

  [Theory]
  [InlineData("resolve", "C:\\TestePEP", "/recursive", "/preview", "/noprompt")]
  [InlineData("merge", "/preview", "/recursive", "/version:C1~C1", "$/a", "$/b", "/noprompt")]
  [InlineData("status", "C:\\TestePEP", "/recursive", "/format:detailed")]
  public void RecordingExecutor_ComandoDeLeitura_Permitido(params string[] arguments)
  {
    Assert.False(RecordingCommandExecutor.IsForbidden(new PEPCliHelper.Core.Execution.Command("tf.exe", arguments)));
  }

  private sealed class ThrowingExecutor : PEPCliHelper.Core.Execution.ICommandExecutor
  {
    public bool Called { get; private set; }

    public Task<PEPCliHelper.Core.Execution.CommandResult> ExecuteAsync(PEPCliHelper.Core.Execution.Command command, Action<string>? onOutputLine = null, CancellationToken cancellationToken = default)
    {
      Called = true;
      throw new InvalidOperationException("Executor real não deveria ser chamado.");
    }
  }

  [Fact]
  public void Validate_ColecaoDeTesteAusenteNoJson_RecusaPorPadrao()
  {
    var config = System.Text.Json.JsonSerializer.Deserialize<IntegrationConfig>("""{ "collection": "https://servidor/Teste" }""")!;

    Assert.Contains(IntegrationConfig.Validate(config), p => p.Contains("colecaoDeTeste"));
  }
}
