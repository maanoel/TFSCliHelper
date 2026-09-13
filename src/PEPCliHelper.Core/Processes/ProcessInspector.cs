using System.ComponentModel;
using System.Diagnostics;
using PEPCliHelper.Core.Common;

namespace PEPCliHelper.Core.Processes;

public sealed record ProcessInfo(int Pid, string Name, string? Path, DateTime? StartTime, bool HasMainWindow);

public enum TerminationOutcome
{
  Terminated,
  StillRunning,
  NotRunning,

  /// <summary>O PID agora pertence a outro processo (reutilizado pelo Windows): nada foi feito.</summary>
  ProcessChanged,
}

public interface IProcessInspector
{
  IReadOnlyList<ProcessInfo> ListByName(string processName);

  /// <summary>Solicita fechamento da janela principal do mesmo processo listado e aguarda.</summary>
  TerminationOutcome TryCloseGracefully(ProcessInfo expected, TimeSpan wait);

  /// <summary>Encerramento forçado de um único processo, somente se ainda for o mesmo listado.</summary>
  TerminationOutcome Kill(ProcessInfo expected, TimeSpan wait);

  /// <summary>Inicia um executável ou abre um arquivo em um programa, sem shell de comandos.</summary>
  void Start(string fileName, string? arguments, string? workingDirectory);
}

public sealed class ProcessInspector : IProcessInspector
{
  public IReadOnlyList<ProcessInfo> ListByName(string processName) =>
    Process.GetProcessesByName(processName)
      .Select(Describe)
      .OrderBy(p => p.Pid)
      .ToList();

  public TerminationOutcome TryCloseGracefully(ProcessInfo expected, TimeSpan wait) =>
    WithSameProcess(expected, process =>
    {
      if (!process.CloseMainWindow())
        return TerminationOutcome.StillRunning;
      return process.WaitForExit(wait) ? TerminationOutcome.Terminated : TerminationOutcome.StillRunning;
    });

  public TerminationOutcome Kill(ProcessInfo expected, TimeSpan wait) =>
    WithSameProcess(expected, process =>
    {
      process.Kill(entireProcessTree: false);
      return process.WaitForExit(wait) ? TerminationOutcome.Terminated : TerminationOutcome.StillRunning;
    });

  public void Start(string fileName, string? arguments, string? workingDirectory)
  {
    var startInfo = new ProcessStartInfo(fileName) { UseShellExecute = true };
    if (!string.IsNullOrEmpty(arguments))
      startInfo.Arguments = arguments;
    if (!string.IsNullOrEmpty(workingDirectory))
      startInfo.WorkingDirectory = workingDirectory;

    try
    {
      using var _ = Process.Start(startInfo);
    }
    catch (Win32Exception ex)
    {
      throw new ToolNotFoundException(fileName, ex.Message, "Confirme o caminho do programa. Para o editor, ajuste 'ferramentas.editor' na configuração.");
    }
  }

  /// <summary>Abre o PID e confere nome, início e caminho no mesmo handle antes de agir.</summary>
  private static TerminationOutcome WithSameProcess(ProcessInfo expected, Func<Process, TerminationOutcome> action)
  {
    Process process;
    try
    {
      process = Process.GetProcessById(expected.Pid);
    }
    catch (ArgumentException)
    {
      return TerminationOutcome.NotRunning;
    }

    using (process)
    {
      try
      {
        if (process.HasExited)
          return TerminationOutcome.NotRunning;

        var current = Describe(process, dispose: false);
        if (!current.Name.Equals(expected.Name, StringComparison.OrdinalIgnoreCase)
          || current.StartTime != expected.StartTime
          || (expected.Path is not null && !string.Equals(current.Path, expected.Path, StringComparison.OrdinalIgnoreCase)))
          return TerminationOutcome.ProcessChanged;

        return action(process);
      }
      catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
      {
        return process.HasExited ? TerminationOutcome.Terminated : TerminationOutcome.StillRunning;
      }
    }
  }

  private static ProcessInfo Describe(Process process) => Describe(process, dispose: true);

  private static ProcessInfo Describe(Process process, bool dispose)
  {
    try
    {
      string? path = null;
      DateTime? started = null;
      var hasWindow = false;
      try
      {
        path = process.MainModule?.FileName;
      }
      catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
      {
        // Sem acesso ao caminho (outro usuário/elevação): confiança desconhecida.
      }

      try
      {
        started = process.StartTime;
        hasWindow = process.MainWindowHandle != IntPtr.Zero;
      }
      catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
      {
      }

      return new ProcessInfo(process.Id, process.ProcessName, path, started, hasWindow);
    }
    finally
    {
      if (dispose)
        process.Dispose();
    }
  }
}
