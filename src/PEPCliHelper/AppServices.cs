using System.Text;
using PEPCliHelper.CommandChain;
using PEPCliHelper.Core.Build;
using PEPCliHelper.Core.Catalog;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.Configuration;
using PEPCliHelper.Core.Diagnostics;
using PEPCliHelper.Core.Environments;
using PEPCliHelper.Core.Execution;
using PEPCliHelper.Core.FileSystem;
using PEPCliHelper.Core.Get;
using PEPCliHelper.Core.History;
using PEPCliHelper.Core.LocalTools;
using PEPCliHelper.Core.Merge;
using PEPCliHelper.Core.Processes;
using PEPCliHelper.Core.Tfvc;
using PEPCliHelper.Infrastructure;
using PEPCliHelper.Presentation;
using Spectre.Console;

namespace PEPCliHelper;

/// <summary>
/// Composição manual das dependências (sem container). Serviços que dependem da configuração
/// são criados sob demanda, para que comandos como 'config init' funcionem sem arquivo.
/// </summary>
public sealed class AppServices
{
  private readonly Func<AppServices, ITfvcClient>? _tfvcFactory;
  private readonly Func<AppServices, ToolLocator>? _toolsFactory;
  private ConfigLoadResult? _load;
  private ToolLocator? _tools;
  private ITfvcClient? _tfvc;

  public AppServices(
    GlobalOptions options,
    Ui ui,
    IFileSystem fileSystem,
    ICommandExecutor executor,
    IProcessInspector processes,
    TimeProvider time,
    ConfigStore configStore,
    IExecutionJournal journal,
    Func<AppServices, ITfvcClient>? tfvcFactory = null,
    Func<AppServices, ToolLocator>? toolsFactory = null,
    IAttachedProcessRunner? attachedRunner = null)
  {
    AttachedRunner = attachedRunner ?? new AttachedProcessRunner();
    Options = options;
    Ui = ui;
    FileSystem = fileSystem;
    Executor = executor;
    Processes = processes;
    Time = time;
    ConfigStore = configStore;
    Journal = journal;
    _tfvcFactory = tfvcFactory;
    _toolsFactory = toolsFactory;
    Chains = ChainCatalog.Create();
  }

  public GlobalOptions Options { get; }

  public Ui Ui { get; }

  public IFileSystem FileSystem { get; }

  public ICommandExecutor Executor { get; }

  /// <summary>Processos que precisam do terminal do usuário (login do tf.exe).</summary>
  public IAttachedProcessRunner AttachedRunner { get; }

  public IProcessInspector Processes { get; }

  public TimeProvider Time { get; }

  public ConfigStore ConfigStore { get; }

  public IExecutionJournal Journal { get; }

  public ChainOfCommands Chains { get; }

  public ToolLocator Tools => _tools ??= _toolsFactory?.Invoke(this) ?? new ToolLocator(FileSystem, Executor, LoadConfig().Config?.Tools ?? new ToolsConfig());

  public ITfvcClient Tfvc => _tfvc ??= _tfvcFactory?.Invoke(this)
    ?? new TfExeClient(Executor, async ct => (await Tools.RequireTfAsync(ct)).Path!);

  public LocalToolsService LocalTools => new(FileSystem, Processes, Time);

  public MergePlanner MergePlanner => new(Tfvc, FileSystem, Journal);

  public MergeExecutor MergeExecutor(MergePlanner planner) => new(Tfvc, planner);

  public GetService GetService => new(Tfvc, FileSystem);

  public BuildService BuildService => new(Executor, FileSystem, LocalTools, Tools);

  public DiscoveryService DiscoveryService => new(FileSystem, Tfvc);

  public DoctorService DoctorService => new(ConfigStore, FileSystem, Tools, Tfvc, Journal.Directory);

  public ConfigLoadResult LoadConfig() => _load ??= ConfigStore.Load();

  /// <summary>Após gravar a configuração, recarrega serviços dependentes.</summary>
  public void InvalidateConfig()
  {
    _load = null;
    _tools = null;
    _tfvc = null;
  }

  public PepConfig RequireConfig()
  {
    var load = LoadConfig();
    return load.Status switch
    {
      ConfigLoadStatus.Loaded => load.Config!,
      ConfigLoadStatus.Missing => throw new UsageException(
        $"Configuração não encontrada: {load.Path}",
        "Execute 'pep config init' e depois 'pep env discover' e 'pep env configure'.",
        load.Path),
      _ => throw new UsageException(
        "Configuração inválida: " + string.Join(" | ", load.Errors),
        "Corrija o arquivo e execute 'pep config validate'.",
        load.Path),
    };
  }

  public VersionCatalog RequireCatalog()
  {
    var catalog = VersionCatalog.FromConfig(RequireConfig());
    if (!catalog.IsConfigured)
    {
      throw new PreconditionException(
        "O catálogo de versões está vazio: nenhuma versão atual configurada.",
        "Execute 'pep env discover' para ver as pastas candidatas e 'pep env configure' para definir a atual e as legadas.",
        ConfigStore.Path);
    }

    return catalog;
  }

  public static AppServices CreateDefault(GlobalOptions options)
  {
    var fileSystem = new PhysicalFileSystem();
    var store = new ConfigStore(fileSystem, AppPaths.ResolveConfigFile(options.ConfigPath, Environment.GetEnvironmentVariable));
    var display = store.Exists ? store.Load().Config?.Display : null;

    var redirected = System.Console.IsOutputRedirected;
    var canPrompt = !System.Console.IsInputRedirected && !redirected;
    if (!redirected)
    {
      try
      {
        System.Console.OutputEncoding = Encoding.UTF8;
      }
      catch (IOException)
      {
        // Console sem suporte: o fallback ASCII é aplicado abaixo.
      }
    }

    var noColor = options.NoColor || options.Json || redirected || display?.NoColor == true;
    var ascii = options.Ascii || redirected || display?.Ascii == true;
    var effective = new GlobalOptions
    {
      NoColor = noColor,
      Ascii = ascii,
      Json = options.Json,
      NonInteractive = options.NonInteractive,
      Yes = options.Yes,
      Verbose = options.Verbose,
      Help = options.Help,
      ShowVersion = options.ShowVersion,
      ConfigPath = options.ConfigPath,
    };

    var console = AnsiConsole.Create(new AnsiConsoleSettings
    {
      Ansi = noColor ? AnsiSupport.No : AnsiSupport.Detect,
      ColorSystem = noColor ? ColorSystemSupport.NoColors : ColorSystemSupport.Detect,
      Interactive = canPrompt && !options.NonInteractive ? InteractionSupport.Yes : InteractionSupport.No,
      Out = new AnsiConsoleOutput(System.Console.Out),
    });
    if (ascii)
      console.Profile.Capabilities.Unicode = false;

    var ui = new Ui(console, effective, canPrompt, System.Console.Out);
    return new AppServices(
      effective,
      ui,
      fileSystem,
      new ProcessCommandExecutor(),
      new ProcessInspector(),
      TimeProvider.System,
      store,
      new JsonExecutionJournal(fileSystem, AppPaths.HistoryDirectory, TimeProvider.System));
  }
}
