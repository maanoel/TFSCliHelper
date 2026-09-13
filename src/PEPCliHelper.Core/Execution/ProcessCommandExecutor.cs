using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using PEPCliHelper.Core.Common;

namespace PEPCliHelper.Core.Execution;

/// <summary>
/// Executa processos diretamente, sem cmd.exe, com ArgumentList e leitura na code page ANSI do Windows
/// (ver <see cref="ConsoleEncoding"/>: com saída redirecionada o tf.exe escreve em 1252 em pt-BR, não na OEM 850).
/// </summary>
public sealed class ProcessCommandExecutor : ICommandExecutor
{
  static ProcessCommandExecutor()
  {
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
  }

  public async Task<CommandResult> ExecuteAsync(Command command, Action<string>? onOutputLine = null, CancellationToken cancellationToken = default)
  {
    var encoding = ConsoleEncoding();
    var startInfo = new ProcessStartInfo(command.FileName)
    {
      UseShellExecute = false,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      RedirectStandardInput = true,
      CreateNoWindow = true,
      StandardOutputEncoding = encoding,
      StandardErrorEncoding = encoding,
    };

    foreach (var argument in command.Arguments)
      startInfo.ArgumentList.Add(argument);

    if (!string.IsNullOrEmpty(command.WorkingDirectory))
      startInfo.WorkingDirectory = command.WorkingDirectory;

    using var process = new Process { StartInfo = startInfo };
    var stopwatch = Stopwatch.StartNew();

    try
    {
      process.Start();
    }
    catch (Win32Exception ex)
    {
      throw new ToolNotFoundException(command.FileName, ex.Message, "Verifique o caminho da ferramenta em 'pep config show' e execute 'pep doctor'.");
    }

    // Sem entrada: nenhuma ferramenta pode ficar aguardando resposta oculta.
    process.StandardInput.Close();

    var stdout = new StringBuilder();
    var stderr = new StringBuilder();
    var gate = new object();
    var outputPump = PumpAsync(process.StandardOutput, stdout, onOutputLine, gate);
    var errorPump = PumpAsync(process.StandardError, stderr, onOutputLine, gate);

    try
    {
      await process.WaitForExitAsync(cancellationToken);
    }
    catch (OperationCanceledException)
    {
      TryKill(process);
      await WaitAfterKillAsync(process);
      await DrainAsync(outputPump, errorPump, TimeSpan.FromSeconds(5));
      return new CommandResult(-1, Snapshot(stdout, gate), Snapshot(stderr, gate), stopwatch.Elapsed, Cancelled: true);
    }

    // Processos filhos (ex.: nós do MSBuild) podem herdar os pipes: não aguardar indefinidamente.
    await DrainAsync(outputPump, errorPump, TimeSpan.FromSeconds(30));
    return new CommandResult(process.ExitCode, Snapshot(stdout, gate), Snapshot(stderr, gate), stopwatch.Elapsed);
  }

  private static async Task PumpAsync(StreamReader reader, StringBuilder buffer, Action<string>? onLine, object gate)
  {
    string? line;
    while ((line = await reader.ReadLineAsync()) is not null)
    {
      lock (gate)
      {
        buffer.AppendLine(line);
        onLine?.Invoke(line);
      }
    }
  }

  private static async Task DrainAsync(Task outputPump, Task errorPump, TimeSpan timeout)
  {
    try
    {
      await Task.WhenAll(outputPump, errorPump).WaitAsync(timeout);
    }
    catch (TimeoutException)
    {
      // A saída coletada até aqui é usada; o exit code do processo já é conhecido.
    }
  }

  private static string Snapshot(StringBuilder buffer, object gate)
  {
    lock (gate)
      return buffer.ToString();
  }

  private static void TryKill(Process process)
  {
    try
    {
      if (!process.HasExited)
        process.Kill(entireProcessTree: true);
    }
    catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
    {
      // Processo já encerrado.
    }
  }

  private static async Task WaitAfterKillAsync(Process process)
  {
    try
    {
      await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
    }
    catch (TimeoutException)
    {
      // O resultado é marcado como cancelado; o estado deve ser inspecionado.
    }
  }

  /// <summary>
  /// Code page ANSI do Windows (GetACP). Verificado na máquina da equipe (2026-09-13): com saída redirecionada e
  /// CreateNoWindow, o tf.exe (.NET Framework) escreve em ANSI 1252, não na OEM 850 — "não está disponível" só
  /// decodifica corretamente como 1252.
  /// </summary>
  internal static Encoding ConsoleEncoding()
  {
    try
    {
      var codePage = OperatingSystem.IsWindows() ? GetACP() : CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
      return Encoding.GetEncoding(codePage);
    }
    catch (Exception ex) when (ex is ArgumentException or NotSupportedException or EntryPointNotFoundException or DllNotFoundException)
    {
      return Encoding.UTF8;
    }
  }

  [System.Runtime.InteropServices.DllImport("kernel32.dll")]
  private static extern int GetACP();
}
