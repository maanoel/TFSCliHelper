using PEPCliHelper.Core.Configuration;
using PEPCliHelper.Core.Execution;
using PEPCliHelper.Core.History;
using PEPCliHelper.Infrastructure;
using PEPCliHelper.Menus;
using PEPCliHelper.Presentation;
using PEPCliHelper.Tests.Fakes;
using Spectre.Console.Testing;

namespace PEPCliHelper.Tests.Cli;

/// <summary>
/// Terminal interativo simulado (spec 002): TestConsole do Spectre com teclas enfileiradas.
/// Se um prompt pedir mais teclas do que as enfileiradas, o TestConsole lança InvalidOperationException.
/// </summary>
internal sealed class InteractiveHarness : IDisposable
{
  public const string ConfigPath = @"C:\cfg\config.json";

  public InteractiveHarness(params string[] targets)
  {
    Tfvc = TestData.HealthyTfvc(FileSystem, targets.Length == 0 ? ["12.1.2606", "12.1.2602"] : targets);
    new ConfigStore(FileSystem, ConfigPath).Save(TestData.Config());
    FileSystem.AddFile(TfExePath);
    Console = new TestConsole().Interactive().Width(200);
    Console.EmitAnsiSequences = false;
  }

  public InMemoryFileSystem FileSystem { get; } = new();

  public FakeTfvcClient Tfvc { get; }

  public FakeProcessInspector Processes { get; } = new();

  public const string TfExePath = @"C:\tf\TF.exe";

  public FakeAttachedRunner Attached { get; } = new();

  public TestConsole Console { get; }

  public TestConsoleInput Keys => Console.Input;

  public string Text => Console.Output;

  /// <summary>Seleciona o item na posição informada (0 = primeiro) de um SelectionPrompt.</summary>
  public InteractiveHarness Select(int index)
  {
    for (var i = 0; i < index; i++)
      Keys.PushKey(ConsoleKey.DownArrow);
    Keys.PushKey(ConsoleKey.Enter);
    return this;
  }

  /// <summary>Seleciona o último item de um SelectionPrompt (ex.: "Cancelar"); o cursor para no fim da lista.</summary>
  public InteractiveHarness SelectLast()
  {
    for (var i = 0; i < 50; i++)
      Keys.PushKey(ConsoleKey.DownArrow);
    Keys.PushKey(ConsoleKey.Enter);
    return this;
  }

  /// <summary>Marca as posições informadas de um MultiSelectionPrompt e confirma com Enter.</summary>
  public InteractiveHarness MultiSelect(params int[] indexes)
  {
    var cursor = 0;
    foreach (var index in indexes.Order())
    {
      for (; cursor < index; cursor++)
        Keys.PushKey(ConsoleKey.DownArrow);
      Keys.PushKey(ConsoleKey.Spacebar);
    }

    Keys.PushKey(ConsoleKey.Enter);
    return this;
  }

  public InteractiveHarness Type(string text)
  {
    Keys.PushTextWithEnter(text);
    return this;
  }

  public Task<int> RunAsync(params string[] args) => PepApp.RunAsync(args, CreateServices);

  public async Task<int> RunMenuAsync()
  {
    using var cancellation = new ConsoleCancellation(listen: false);
    return await new InteractiveMenu(CreateServices(new GlobalOptions()), cancellation).RunAsync();
  }

  public void Dispose() => Console.Dispose();

  private AppServices CreateServices(GlobalOptions options)
  {
    var effective = new GlobalOptions
    {
      NoColor = true, Ascii = options.Ascii, Json = options.Json, NonInteractive = false, Yes = options.Yes,
      Verbose = options.Verbose, Help = options.Help, ShowVersion = options.ShowVersion, ConfigPath = options.ConfigPath,
    };
    var output = new StringWriter();
    var ui = new Ui(Console, effective, canPrompt: true, output);
    var executor = new FakeCommandExecutor();
    return new AppServices(effective, ui, FileSystem, executor, Processes, TimeProvider.System,
      new ConfigStore(FileSystem, ConfigPath), new JsonExecutionJournal(FileSystem, @"C:\hist", TimeProvider.System),
      _ => Tfvc,
      _ => new ToolLocator(FileSystem, executor, new ToolsConfig { TfExe = TfExePath }, @"C:\sem\vswhere.exe"),
      Attached);
  }
}
