using System.ComponentModel;
using System.Diagnostics;
using PEPCliHelper.Core.Common;

namespace PEPCliHelper.Core.Execution;

/// <summary>
/// Executa um processo no próprio terminal do usuário (sem redirecionar entrada/saída), para ferramentas
/// que precisam exibir interação própria — ex.: login do tf.exe. Uso restrito a comandos somente leitura.
/// </summary>
public interface IAttachedProcessRunner
{
  Task<int> RunAsync(Command command, CancellationToken cancellationToken);
}

public sealed class AttachedProcessRunner : IAttachedProcessRunner
{
  public async Task<int> RunAsync(Command command, CancellationToken cancellationToken)
  {
    var startInfo = new ProcessStartInfo(command.FileName) { UseShellExecute = false };
    foreach (var argument in command.Arguments)
      startInfo.ArgumentList.Add(argument);
    if (!string.IsNullOrEmpty(command.WorkingDirectory))
      startInfo.WorkingDirectory = command.WorkingDirectory;

    using var process = new Process { StartInfo = startInfo };
    try
    {
      process.Start();
    }
    catch (Win32Exception ex)
    {
      throw new ToolNotFoundException(command.FileName, ex.Message, "Verifique o caminho da ferramenta com 'pep doctor'.");
    }

    try
    {
      await process.WaitForExitAsync(cancellationToken);
    }
    catch (OperationCanceledException)
    {
      try
      {
        if (!process.HasExited)
          process.Kill(entireProcessTree: true);
      }
      catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
      {
      }

      throw;
    }

    return process.ExitCode;
  }
}
