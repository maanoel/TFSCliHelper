using PEPCliHelper.CommandChain;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Infrastructure;
using PEPCliHelper.Menus;
using PEPCliHelper.Presentation;

namespace PEPCliHelper;

/// <summary>Ponto de entrada testável: interpreta argumentos, despacha comandos ou abre o menu.</summary>
public static class PepApp
{
  public static async Task<int> RunAsync(string[] args, Func<GlobalOptions, AppServices>? createServices = null)
  {
    GlobalOptions options;
    List<string> tokens;
    try
    {
      (options, tokens) = GlobalOptions.Parse(args, Environment.GetEnvironmentVariable);
    }
    catch (UsageException ex)
    {
      await Console.Error.WriteLineAsync($"{ex.Message} {ex.NextStep}");
      return ExitCodes.Usage;
    }

    var services = (createServices ?? AppServices.CreateDefault)(options);
    using var cancellation = new ConsoleCancellation(listen: createServices is null);

    if (options.ShowVersion && tokens.Count == 0)
    {
      if (services.Options.Json)
        services.Ui.WriteJson(new { versao = AppInfo.Version });
      else
        Console.Out.WriteLine(AppInfo.Version);
      return ExitCodes.Success;
    }

    if (tokens.Count == 0)
    {
      if (!options.Help && services.Ui.CanPrompt)
        return await new InteractiveMenu(services, cancellation).RunAsync();

      HelpRenderer.RenderGeneral(services.Ui, services.Chains);
      return options.Help ? ExitCodes.Success : ExitCodes.Usage;
    }

    return await DispatchAsync(services, tokens, cancellation.Token);
  }

  public static async Task<int> DispatchAsync(AppServices services, IReadOnlyList<string> tokens, CancellationToken cancellationToken)
  {
    var ui = services.Ui;
    try
    {
      var match = services.Chains.Match(tokens);
      if (match is null)
      {
        var suggestions = services.Chains.Suggest(tokens);
        throw new UsageException(
          $"Comando desconhecido: '{string.Join(' ', tokens)}'.",
          suggestions.Count > 0 ? $"Você quis dizer: {string.Join(" | ", suggestions)}? Veja 'pep help'." : "Veja os comandos disponíveis com 'pep help'.");
      }

      if (services.Options.Help)
      {
        HelpRenderer.RenderCommand(ui, match.Chain.Help);
        return ExitCodes.Success;
      }

      var line = CommandLine.Parse(match.Rest, match.Chain.Help, services.Options);
      return await match.Chain.CreateBuilder(services).BuildAsync(line, cancellationToken);
    }
    catch (PepCliException ex)
    {
      ui.RenderError(ex);
      return ex.ExitCode;
    }
    catch (OperationCanceledException)
    {
      if (ui.Json)
        ui.WriteJson(new { cancelado = true, codigoSaida = ExitCodes.Cancelled });
      else
        ui.Warn("Operação cancelada. Cancelamento não desfaz etapas já concluídas; inspecione o estado antes de repetir.");
      return ExitCodes.Cancelled;
    }
    catch (Exception ex)
    {
      ui.RenderUnexpected(ex, $"Consulte o histórico em '{services.Journal.Directory}'.");
      return ExitCodes.Unexpected;
    }
  }
}
