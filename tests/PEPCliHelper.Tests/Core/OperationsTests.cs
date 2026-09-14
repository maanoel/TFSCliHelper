using PEPCliHelper.Core.Build;
using PEPCliHelper.Core.Catalog;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.Configuration;
using PEPCliHelper.Core.Execution;
using PEPCliHelper.Core.Get;
using PEPCliHelper.Core.LocalTools;
using PEPCliHelper.Core.Processes;
using PEPCliHelper.Core.Tfvc;
using PEPCliHelper.Tests.Fakes;

namespace PEPCliHelper.Tests.Core;

public class GetServiceTests
{
  private readonly InMemoryFileSystem _fileSystem = new();

  [Fact]
  public async Task Plan_GetAll_UmAlvoPorVersaoEProjetoDoCatalogoAtivo()
  {
    var catalog = TestData.Catalog();
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606", "12.1.2602");

    var plan = await new GetService(tfvc, _fileSystem).PlanAsync(catalog.LocateAll(catalog.Active), NullOperationLog.Instance, CancellationToken.None);

    Assert.Equal(6, plan.Targets.Count);
    Assert.All(plan.Targets, t => Assert.True(t.IsReady));
  }

  [Fact]
  public async Task Execute_AlvoSemMapeamento_BloqueadoEDemaisSeguem()
  {
    var catalog = TestData.Catalog();
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606", "12.1.2602");
    tfvc.Workfolds.Remove(@"C:\LR\Legado\12.1.2606");
    var service = new GetService(tfvc, _fileSystem);
    var plan = await service.PlanAsync(catalog.LocateAll(catalog.Active), NullOperationLog.Instance, CancellationToken.None);

    var result = await service.ExecuteAsync(plan, NullOperationLog.Instance, CancellationToken.None);

    Assert.Equal(2, result.Targets.Count(t => t.State == GetTargetState.Blocked));
    Assert.Equal(4, tfvc.GetCount);
    Assert.Equal(ExitCodes.ConflictOrPartial, result.ExitCode);
  }

  [Fact]
  public async Task Execute_TfSucessoParcial_EstadoParcial()
  {
    var catalog = TestData.Catalog();
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606");
    tfvc.GetStatus[TestData.Local("12.1.2610")] = TfStatus.PartialSuccess;
    var service = new GetService(tfvc, _fileSystem);
    var plan = await service.PlanAsync([catalog.Locate(catalog.Current!, catalog.FindProject("back")!)], NullOperationLog.Instance, CancellationToken.None);

    var result = await service.ExecuteAsync(plan, NullOperationLog.Instance, CancellationToken.None);

    Assert.Equal(GetTargetState.Partial, result.Targets.Single().State);
    Assert.Equal(ExitCodes.ConflictOrPartial, result.ExitCode);
  }

  [Fact]
  public async Task Execute_ErroDeRede_InterrompeAlvosSeguintes()
  {
    var catalog = TestData.Catalog();
    var tfvc = TestData.HealthyTfvc(_fileSystem, "12.1.2606", "12.1.2602");
    tfvc.GetStatus[TestData.Local("12.1.2610", "Sau-PEP")] = TfStatus.NetworkError;
    var service = new GetService(tfvc, _fileSystem);
    var plan = await service.PlanAsync(catalog.LocateAll(catalog.Active), NullOperationLog.Instance, CancellationToken.None);

    var result = await service.ExecuteAsync(plan, NullOperationLog.Instance, CancellationToken.None);

    Assert.Equal(GetTargetState.Failed, result.Targets[0].State);
    Assert.All(result.Targets.Skip(1), t => Assert.Equal(GetTargetState.NotStarted, t.State));
  }
}

public class BuildServiceTests
{
  private readonly InMemoryFileSystem _fileSystem = new();
  private readonly FakeProcessInspector _processes = new();
  private readonly FakeCommandExecutor _executor = new();

  private BuildService Service()
  {
    _fileSystem.AddFile(@"C:\vs\MSBuild.exe");
    var tools = new ToolLocator(_fileSystem, _executor, new ToolsConfig { MsBuild = @"C:\vs\MSBuild.exe" });
    return new BuildService(_executor, _fileSystem, new LocalToolsService(_fileSystem, _processes, TimeProvider.System), tools);
  }

  [Fact]
  public async Task Plan_HostDaVersaoEmExecucao_AvisaQueSeraEncerradoSemEncerrarNoPlano()
  {
    var catalog = TestData.Catalog();
    _fileSystem.AddFile(@"C:\LR\Atual\Release\Sau-PEP\RM.Pep.sln");
    _processes.Processes.Add(new ProcessInfo(42, "RM.Host", @"C:\LR\Atual\Release\Bin\RM.Host.exe", null, false));

    var plan = await Service().PlanAsync([catalog.Locate(catalog.Current!, catalog.FindProject("back")!)], CancellationToken.None);

    Assert.True(plan.CanExecute);
    Assert.Contains(plan.Targets.Single().Warnings, w => w.Contains("PID 42") && w.Contains("será encerrado"));
    Assert.Empty(_processes.GracefulRequests);
    Assert.Empty(_processes.Killed);
  }

  [Fact]
  public async Task StopHosts_HostDaVersaoCompilada_EncerraGraciosamenteSomenteEle()
  {
    var catalog = TestData.Catalog();
    _fileSystem.AddFile(@"C:\LR\Atual\Release\Sau-PEP\RM.Pep.sln");
    _processes.Processes.Add(new ProcessInfo(42, "RM.Host", @"C:\LR\Atual\Release\Bin\RM.Host.exe", null, false));
    _processes.Processes.Add(new ProcessInfo(43, "RM.Host", @"C:\LR\Legado\12.1.2606\Bin\RM.Host.exe", null, false));
    var service = Service();
    var plan = await service.PlanAsync([catalog.Locate(catalog.Current!, catalog.FindProject("back")!)], CancellationToken.None);

    var results = service.StopHosts(plan);

    Assert.True(Assert.Single(results).Terminated);
    Assert.Equal([42], _processes.GracefulRequests);
    Assert.Empty(_processes.Killed);
    Assert.True(plan.CanExecute);
  }

  [Fact]
  public async Task StopHosts_HostNaoEncerraGraciosamente_BloqueiaVersaoSemForcar()
  {
    var catalog = TestData.Catalog();
    _fileSystem.AddFile(@"C:\LR\Atual\Release\Sau-PEP\RM.Pep.sln");
    _processes.Processes.Add(new ProcessInfo(42, "RM.Host", @"C:\LR\Atual\Release\Bin\RM.Host.exe", null, false));
    _processes.IgnoresGracefulClose.Add(42);
    var service = Service();
    var plan = await service.PlanAsync([catalog.Locate(catalog.Current!, catalog.FindProject("back")!)], CancellationToken.None);

    var results = service.StopHosts(plan);

    Assert.False(Assert.Single(results).Terminated);
    Assert.Empty(_processes.Killed);
    Assert.False(plan.CanExecute);
    Assert.Contains(plan.Targets.Single().Blockers, b => b.Contains("kill host --pid 42 --force"));
  }

  [Fact]
  public async Task Execute_PrimeiraSolucaoFalha_DemaisNaoIniciadas()
  {
    var catalog = TestData.Catalog();
    _fileSystem.AddFile(@"C:\LR\Atual\Release\Sau-Saude\Sau-Saude.sln").AddFile(@"C:\LR\Atual\Release\Sau-PEP\RM.Pep.sln");
    _executor.Respond = _ => new CommandResult(1, "a.cs(1): error CS1002: ; expected", "", TimeSpan.Zero);
    var service = Service();
    var plan = await service.PlanAsync(BuildOrder.Arrange(catalog, [catalog.Current!]), CancellationToken.None);

    var result = await service.ExecuteAsync(plan, null, false, NullOperationLog.Instance, CancellationToken.None);

    Assert.Equal(BuildTargetState.Failed, result.Targets[0].State);
    Assert.Equal(BuildTargetState.NotStarted, result.Targets[1].State);
    Assert.Equal(ExitCodes.OperationFailed, result.ExitCode);
    Assert.Single(_executor.Executed);
    Assert.Equal("RM.Pep.sln", Path.GetFileName(_executor.Executed[0].Arguments[0]));
  }

  [Fact]
  public async Task Execute_Configuracao_RepassaParaMsBuildSemGet()
  {
    var catalog = TestData.Catalog();
    _fileSystem.AddFile(@"C:\LR\Atual\Release\Sau-PEP\RM.Pep.sln");
    var service = Service();
    var plan = await service.PlanAsync([catalog.Locate(catalog.Current!, catalog.FindProject("back")!)], CancellationToken.None);

    await service.ExecuteAsync(plan, "Release", false, NullOperationLog.Instance, CancellationToken.None);

    var command = _executor.Executed.Single();
    Assert.Contains("/p:Configuration=Release", command.Arguments);
    Assert.DoesNotContain(command.Arguments, a => a.Contains("get", StringComparison.OrdinalIgnoreCase));
  }
}

public class BuildOrderTests
{
  private static PepConfig LegacyConfigSauFirst()
  {
    var config = TestData.Config();
    config.Projects.Reverse();
    foreach (var project in config.Projects)
      project.Principal = false;
    return config;
  }

  private static List<string> Order(VersionCatalog catalog, IEnumerable<VersionEntry> versions, params string[] aliases) =>
    BuildOrder.Arrange(catalog, versions, aliases.Select(a => catalog.FindProject(a)!))
      .Select(l => $"{l.Version.Id}:{l.Project.Alias}")
      .ToList();

  [Fact]
  public void Catalogo_ConfigComSauPrimeiro_PepPrimeiroNaListaDeProjetos()
  {
    var config = LegacyConfigSauFirst();

    var catalog = VersionCatalog.FromConfig(config);

    Assert.Equal(["back", "sau"], catalog.Projects.Select(p => p.Alias));
    Assert.Equal(["back", "sau"], VersionCatalog.PrincipalFirst(config.Projects).Select(p => p.Alias));
    Assert.Equal("sau", config.Projects[0].Alias);
  }

  [Fact]
  public void Arrange_ConfigLegadaComSauPrimeiroSemPrincipal_BackPrimeiroEmCadaVersao()
  {
    var catalog = VersionCatalog.FromConfig(LegacyConfigSauFirst());

    var order = Order(catalog, catalog.Active);

    Assert.Equal(["12.1.2610:back", "12.1.2610:sau", "12.1.2606:back", "12.1.2606:sau", "12.1.2602:back", "12.1.2602:sau"], order);
  }

  [Fact]
  public void Arrange_PrincipalExplicitoEmOutroAlias_Respeitado()
  {
    var config = LegacyConfigSauFirst();
    config.Projects.Single(p => p.Alias == "sau").Principal = true;
    config.Projects.Reverse();
    var catalog = VersionCatalog.FromConfig(config);

    var order = Order(catalog, [catalog.Current!]);

    Assert.Equal(["12.1.2610:sau", "12.1.2610:back"], order);
  }

  [Fact]
  public void Arrange_SomenteSau_CompilaApenasSaude()
  {
    var catalog = TestData.Catalog();

    var order = Order(catalog, [catalog.Current!], "sau");

    Assert.Equal(["12.1.2610:sau"], order);
  }

  [Fact]
  public void Arrange_VersoesForaDeOrdem_AtualPrimeiroDepoisLegadasDoCatalogo()
  {
    var catalog = TestData.Catalog();
    var legacy = catalog.ResolveVersion("2602").Version!;

    var order = Order(catalog, [legacy, catalog.Current!, legacy], "back");

    Assert.Equal(["12.1.2610:back", "12.1.2602:back"], order);
  }
}

public class LocalToolsTests
{
  private readonly InMemoryFileSystem _fileSystem = new();
  private readonly FakeProcessInspector _processes = new();

  private LocalToolsService Service() => new(_fileSystem, _processes, TimeProvider.System);

  [Fact]
  public void PlanBroker_ArquivoAusente_NenhumaAcao()
  {
    var catalog = TestData.Catalog();

    var plan = Service().PlanBrokerDelete(catalog.Current!, new LocalFilesConfig());

    Assert.False(plan.Exists);
    Assert.Empty(plan.Blockers);
    Assert.False(plan.IsReady);
  }

  [Fact]
  public void DeleteBroker_ComBackup_RemoveSomenteOArquivo()
  {
    var catalog = TestData.Catalog();
    _fileSystem.AddFile(@"C:\LR\Atual\Release\Bin\_Broker.dat", "broker", readOnly: false);
    var service = Service();
    var plan = service.PlanBrokerDelete(catalog.Current!, new LocalFilesConfig());

    var backup = service.DeleteBroker(plan, backup: true);

    Assert.True(_fileSystem.FileExists(backup!));
    Assert.False(_fileSystem.FileExists(@"C:\LR\Atual\Release\Bin\_Broker.dat"));
    Assert.Single(_fileSystem.Deleted);
  }

  [Fact]
  public void PlanBroker_HostDaVersaoRodando_Bloqueia()
  {
    var catalog = TestData.Catalog();
    _fileSystem.AddFile(@"C:\LR\Atual\Release\Bin\_Broker.dat");
    _processes.Processes.Add(new ProcessInfo(7, "RM.Host", @"C:\LR\Atual\Release\Bin\RM.Host.exe", null, false));

    Assert.NotEmpty(Service().PlanBrokerDelete(catalog.Current!, new LocalFilesConfig()).Blockers);
  }

  [Fact]
  public void ResolveVersionFile_CaminhoForaDaVersao_Recusa()
  {
    var catalog = TestData.Catalog();

    Assert.Throws<PreconditionException>(() =>
      LocalToolsService.ResolveVersionFile(catalog.Current!, new LocalFilesConfig { Broker = @"..\..\x.dat" }, LocalFileKind.Broker));
  }

  [Fact]
  public void Infer_ProcessoDentroDaVersao_ConfiancaAlta_ForaBaixa_SemCaminhoDesconhecida()
  {
    var catalog = TestData.Catalog();

    Assert.Equal(HostConfidence.Alta, LocalToolsService.Infer(new ProcessInfo(1, "RM.Host", @"C:\LR\Legado\12.1.2606\Bin\RM.Host.exe", null, false), catalog, TestData.Root).Confidence);
    Assert.Equal(HostConfidence.Baixa, LocalToolsService.Infer(new ProcessInfo(2, "RM.Host", @"C:\LR\Legado\12.1.2306\Bin\RM.Host.exe", null, false), catalog, TestData.Root).Confidence);
    Assert.Equal(HostConfidence.Desconhecida, LocalToolsService.Infer(new ProcessInfo(3, "RM.Host", null, null, false), catalog, TestData.Root).Confidence);
  }

  [Fact]
  public void Terminate_PidReutilizadoPorOutroProcesso_NaoEncerra()
  {
    var listed = new ProcessInfo(9, "RM.Host", @"C:\LR\Atual\Release\Bin\RM.Host.exe", new DateTime(2026, 9, 12, 8, 0, 0), true);
    _processes.Processes.Add(listed with { Name = "chrome", Path = @"C:\chrome.exe", StartTime = new DateTime(2026, 9, 12, 9, 0, 0) });
    var candidate = LocalToolsService.Infer(listed, TestData.Catalog(), TestData.Root);

    var result = Service().Terminate(candidate, force: true);

    Assert.False(result.Terminated);
    Assert.Contains("outro processo", result.Message);
    Assert.Empty(_processes.Killed);
  }

  [Fact]
  public void Terminate_SemForce_ApenasGracioso()
  {
    _processes.Processes.Add(new ProcessInfo(9, "RM.Host", @"C:\LR\Atual\Release\Bin\RM.Host.exe", null, true));
    _processes.IgnoresGracefulClose.Add(9);
    var candidate = LocalToolsService.Infer(_processes.Processes[0], TestData.Catalog(), TestData.Root);

    var result = Service().Terminate(candidate, force: false);

    Assert.False(result.Terminated);
    Assert.Empty(_processes.Killed);
  }
}

public class ToolLocatorTests
{
  [Fact]
  public async Task LocateTf_CaminhoConfiguradoInexistente_ProblemaSemFallbackSilencioso()
  {
    var executor = new FakeCommandExecutor();
    var locator = new ToolLocator(new InMemoryFileSystem(), executor, new ToolsConfig { TfExe = @"C:\nao\existe\TF.exe" });

    var tool = await locator.LocateTfAsync();

    Assert.False(tool.Found);
    Assert.Contains("ferramentas.tfExe", tool.Problem);
    Assert.Empty(executor.Executed);
  }

  [Fact]
  public async Task RequireTf_SemVswhereESemConfig_ToolNotFound()
  {
    var locator = new ToolLocator(new InMemoryFileSystem(), new FakeCommandExecutor(), new ToolsConfig(), @"C:\nao\vswhere.exe");

    await Assert.ThrowsAsync<ToolNotFoundException>(() => locator.RequireTfAsync());
  }

  [Fact]
  public async Task LocateTf_ViaVswhere_UsaArgumentosEstruturados()
  {
    var fileSystem = new InMemoryFileSystem().AddFile(@"C:\vswhere.exe").AddFile(@"C:\VS\TF.exe");
    var executor = new FakeCommandExecutor { Respond = _ => new CommandResult(0, "C:\\VS\\TF.exe\r\n", "", TimeSpan.Zero) };

    var tool = await new ToolLocator(fileSystem, executor, new ToolsConfig(), @"C:\vswhere.exe").LocateTfAsync();

    Assert.Equal(@"C:\VS\TF.exe", tool.Path);
    Assert.Contains("-find", executor.Executed.Single().Arguments);
  }
}
