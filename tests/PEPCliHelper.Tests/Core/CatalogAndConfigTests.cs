using PEPCliHelper.Core.Catalog;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.Configuration;
using PEPCliHelper.Core.Environments;
using PEPCliHelper.Tests.Fakes;

namespace PEPCliHelper.Tests.Core;

public class VersionCatalogTests
{
  [Theory]
  [InlineData("12.1.2606", "12.1.2606")]
  [InlineData("2606", "12.1.2606")]
  [InlineData("atual", "12.1.2610")]
  [InlineData("ATUAL", "12.1.2610")]
  public void ResolveVersion_TokenInequivoco_RetornaVersao(string token, string expected)
  {
    var resolution = TestData.Catalog().ResolveVersion(token);

    Assert.Equal(VersionResolutionStatus.Found, resolution.Status);
    Assert.Equal(expected, resolution.Version!.Id);
  }

  [Theory]
  [InlineData("06")]
  [InlineData("260")]
  [InlineData("12.1")]
  [InlineData("")]
  public void ResolveVersion_TokenParecido_NaoSelecionaSilenciosamente(string token)
  {
    Assert.Equal(VersionResolutionStatus.NotFound, TestData.Catalog().ResolveVersion(token).Status);
  }

  [Fact]
  public void ResolveVersion_AliasEmDuasVersoes_Ambiguo()
  {
    var catalog = new VersionCatalog(
      [
        new VersionEntry("12.1.2606", true, true, @"C:\a", "$/a", ["x"], null),
        new VersionEntry("11.1.2606", false, true, @"C:\b", "$/b", ["x"], null),
      ],
      []);

    var resolution = catalog.ResolveVersion("2606");

    Assert.Equal(VersionResolutionStatus.Ambiguous, resolution.Status);
    Assert.Equal(2, resolution.Candidates.Count);
  }

  [Fact]
  public void ResolveVersion_VersaoDesativada_InformaInativa()
  {
    var config = TestData.Config();
    config.Versions[2].Active = false;

    Assert.Equal(VersionResolutionStatus.Inactive, VersionCatalog.FromConfig(config).ResolveVersion("2602").Status);
  }

  [Fact]
  public void Active_AtualPrimeiroDepoisLegadasAtivas_DesativadasFora()
  {
    var config = TestData.Config(3);
    config.Versions[3].Active = false;
    var catalog = VersionCatalog.FromConfig(config);

    Assert.Equal(["12.1.2610", "12.1.2606", "12.1.2602"], catalog.Active.Select(v => v.Id));
    Assert.DoesNotContain(catalog.ActiveLegacy, v => v.Id == "12.1.2510");
  }

  [Fact]
  public void Locate_ProjetoNaVersao_SeparaCaminhoLocalDeServidor()
  {
    var catalog = TestData.Catalog();
    var location = catalog.Locate(catalog.ResolveVersion("2606").Version!, catalog.FindProject("back")!);

    Assert.Equal(@"C:\LR\Legado\12.1.2606\Sau-PEP", location.LocalPath);
    Assert.Equal("$/Linha-RM/Legado/12.1.2606/Sau-PEP", location.ServerPath);
  }
}

public class ConfigValidatorTests
{
  [Fact]
  public void Validate_ConfiguracaoPadraoComCatalogo_SemErros()
  {
    Assert.Empty(ConfigValidator.Validate(TestData.Config(4)));
  }

  [Fact]
  public void Validate_CincoLegadasAtivas_ErroDeLimite()
  {
    var errors = ConfigValidator.Validate(TestData.Config(5));

    Assert.Contains(errors, e => e.Contains("máximo é 4"));
  }

  [Fact]
  public void Validate_DuasAtuais_Erro()
  {
    var config = TestData.Config();
    config.Versions[1].IsCurrent = true;

    Assert.Contains(ConfigValidator.Validate(config), e => e.Contains("apenas uma"));
  }

  [Fact]
  public void Validate_DoisProjetosPrincipais_Erro()
  {
    var config = TestData.Config();
    config.Projects.ForEach(p => p.Principal = true);

    Assert.Contains(ConfigValidator.Validate(config), e => e.Contains("principal") && e.Contains("apenas um"));
  }

  [Fact]
  public void Validate_CaminhoDeServidorNoCaminhoLocal_Erro()
  {
    var config = TestData.Config();
    config.Versions[1].LocalPath = "$/Linha-RM/Legado/12.1.2606";

    Assert.Contains(ConfigValidator.Validate(config), e => e.Contains("caminhoLocal"));
  }

  [Fact]
  public void Validate_CaminhoLocalNoCaminhoDeServidor_Erro()
  {
    var config = TestData.Config();
    config.Versions[1].ServerPath = @"C:\Linha-RM\Legado\12.1.2606";

    Assert.Contains(ConfigValidator.Validate(config), e => e.Contains("caminhoServidor"));
  }

  [Fact]
  public void Validate_AliasRepetidoEntreVersoes_Erro()
  {
    var config = TestData.Config();
    config.Versions[2].Aliases = ["2606"];

    Assert.Contains(ConfigValidator.Validate(config), e => e.Contains("já identifica"));
  }

  [Fact]
  public void Validate_ArquivoLocalForaDaVersao_Erro()
  {
    var config = TestData.Config();
    config.LocalFiles.Broker = @"..\..\Windows\system.dat";

    Assert.Contains(ConfigValidator.Validate(config), e => e.Contains("arquivosLocais.broker"));
  }
}

public class ConfigStoreTests
{
  private const string ConfigPath = @"C:\cfg\config.json";

  [Fact]
  public void Load_ArquivoAusente_Missing()
  {
    var store = new ConfigStore(new InMemoryFileSystem(), ConfigPath);

    Assert.Equal(ConfigLoadStatus.Missing, store.Load().Status);
  }

  [Fact]
  public void Save_ArquivoExistente_CriaBackupEGrava()
  {
    var fileSystem = new InMemoryFileSystem();
    var store = new ConfigStore(fileSystem, ConfigPath);
    store.Save(TestData.Config());

    var backup = store.Save(TestData.Config(3));

    Assert.NotNull(backup);
    Assert.True(fileSystem.FileExists(backup));
    Assert.Equal(4, store.Load().Config!.Versions.Count);
  }

  [Fact]
  public void Save_VariasGravacoesSeguidas_BackupsComNomesDistintos()
  {
    var fileSystem = new InMemoryFileSystem();
    var store = new ConfigStore(fileSystem, ConfigPath);
    store.Save(TestData.Config());

    var first = store.Save(TestData.Config());
    var second = store.Save(TestData.Config());

    Assert.NotEqual(first, second);
    Assert.True(fileSystem.FileExists(first!));
    Assert.True(fileSystem.FileExists(second!));
  }

  [Fact]
  public void Load_CampoDeSenha_RejeitaSemAceitarSegredo()
  {
    var fileSystem = new InMemoryFileSystem().AddFile(ConfigPath, "{ \"raizLocal\": \"C:\\\\LR\", \"senha\": \"123\" }");

    var result = new ConfigStore(fileSystem, ConfigPath).Load();

    Assert.Equal(ConfigLoadStatus.Invalid, result.Status);
    Assert.Contains(result.Errors, e => e.Contains("senhas ou tokens"));
  }

  [Fact]
  public void Load_JsonInvalido_InvalidComMensagem()
  {
    var fileSystem = new InMemoryFileSystem().AddFile(ConfigPath, "{ raizLocal: ");

    var result = new ConfigStore(fileSystem, ConfigPath).Load();

    Assert.Equal(ConfigLoadStatus.Invalid, result.Status);
    Assert.Contains(result.Errors, e => e.StartsWith("JSON inválido"));
  }

  [Fact]
  public void ResolveConfigFile_ArgumentoExplicito_PrecedeVariavelDeAmbiente()
  {
    var path = AppPaths.ResolveConfigFile(@"D:\explicito.json", _ => @"D:\ambiente.json");

    Assert.Equal(@"D:\explicito.json", path);
  }

  [Fact]
  public void ResolveConfigFile_SemArgumento_UsaVariavelDeAmbiente()
  {
    Assert.Equal(@"D:\ambiente.json", AppPaths.ResolveConfigFile(null, _ => @"D:\ambiente.json"));
  }
}

public class ConventionServerPathTests
{
  [Theory]
  [InlineData(@"C:\Linha-RM\Legado\12.1.2506", "$/Linha-RM/Legado/12.1.2506")]
  [InlineData(@"C:\Linha-RM\Legado\12.1.2602", "$/Linha-RM/Legado/12.1.2602")]
  [InlineData(@"C:\Linha-RM\Atual\Release", "$/Linha-RM/Atual/Release")]
  public void ConventionServerPath_PastaDentroDaRaiz_EspelhaCaminhoTfvc(string folder, string expected)
  {
    var config = PepConfig.CreateDefault();

    Assert.Equal(expected, DiscoveryService.ConventionServerPath(config, folder));
  }

  [Fact]
  public void ConventionServerPath_PastaForaDaRaiz_Null()
  {
    Assert.Null(DiscoveryService.ConventionServerPath(PepConfig.CreateDefault(), @"D:\Outro\12.1.2506"));
  }

  [Fact]
  public void ConventionServerPath_RaizServidorConfigurada_UsaAConfigurada()
  {
    var config = PepConfig.CreateDefault();
    config.ServerRoot = "$/Outra-Raiz";

    Assert.Equal("$/Outra-Raiz/Legado/12.1.2506", DiscoveryService.ConventionServerPath(config, @"C:\Linha-RM\Legado\12.1.2506"));
  }

  [Fact]
  public void Validate_RaizServidorInvalida_Erro()
  {
    var config = TestData.Config();
    config.ServerRoot = @"C:\Linha-RM";

    Assert.Contains(ConfigValidator.Validate(config), e => e.Contains("raizServidor"));
  }
}

public class CatalogEditorTests
{
  [Fact]
  public void Apply_NovaAtual_AntigaViraLegadaEDesativadasPermanecem()
  {
    var original = TestData.Config(4);
    var current = new CatalogChoice("12.1.2614", @"C:\LR\Atual\Release", "$/Linha-RM/atual/release");
    var legacy = new[]
    {
      new CatalogChoice("12.1.2610", @"C:\LR\Legado\12.1.2610", "$/Linha-RM/Legado/12.1.2610"),
      new CatalogChoice("12.1.2606", @"C:\LR\Legado\12.1.2606", "$/Linha-RM/Legado/12.1.2606"),
    };

    var (config, errors) = CatalogEditor.Apply(original, current, legacy);

    Assert.Empty(errors);
    var catalog = VersionCatalog.FromConfig(config);
    Assert.Equal("12.1.2614", catalog.Current!.Id);
    Assert.Equal(["12.1.2610", "12.1.2606"], catalog.ActiveLegacy.Select(v => v.Id));
    Assert.Contains(config.Versions, v => v.Id == "12.1.2506" && !v.Active);
    Assert.Equal(original.Versions.Count + 1, config.Versions.Count);
    Assert.Equal(["2614"], config.Versions.Single(v => v.Id == "12.1.2614").Aliases);
  }

  [Fact]
  public void Apply_CincoLegadas_Recusa()
  {
    var legacy = Enumerable.Range(1, 5).Select(i => new CatalogChoice($"12.1.25{i:00}", $@"C:\LR\Legado\{i}", $"$/L/{i}")).ToList();

    var (_, errors) = CatalogEditor.Apply(TestData.Config(), new CatalogChoice("12.1.2610", @"C:\LR\Atual\Release", "$/a"), legacy);

    Assert.Contains(errors, e => e.Contains("máximo é 4"));
  }
}
