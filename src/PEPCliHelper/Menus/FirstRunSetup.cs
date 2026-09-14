using PEPCliHelper.CommandsBuilders;
using PEPCliHelper.Core.Catalog;
using PEPCliHelper.Core.Configuration;
using PEPCliHelper.Core.Environments;
using PEPCliHelper.Presentation;

namespace PEPCliHelper.Menus;

/// <summary>
/// Primeira execução (decisão de 2026-09-14): sem configuração ou sem versão atual, aplica a configuração automática
/// sem nenhuma pergunta e avisa o usuário. Não existe configuração manual. Configuração inválida nunca é sobrescrita.
/// </summary>
public sealed class FirstRunSetup
{
  public const string ReadyMessage = "Configuração automática concluída: o PEP CLI está pronto para uso.";

  private readonly AppServices _services;

  public FirstRunSetup(AppServices services)
  {
    _services = services;
  }

  private Ui Ui => _services.Ui;

  public bool IsNeeded()
  {
    var load = _services.LoadConfig();
    return load.Status == ConfigLoadStatus.Missing
      || (load.Status == ConfigLoadStatus.Loaded && !VersionCatalog.FromConfig(load.Config!).IsConfigured);
  }

  /// <summary>Aplica a configuração automática se necessário. Retorna true se o PEP CLI ficou configurado.</summary>
  public bool EnsureConfigured()
  {
    if (!IsNeeded())
      return true;

    var (baseConfig, status) = ConfigAutoBuilder.LoadBase(_services, force: false);
    var quiet = Ui.Json;
    if (!quiet)
    {
      Ui.Title("Primeira configuração");
      Ui.Info(status == ConfigLoadStatus.Missing
        ? $"Nenhuma configuração encontrada. Configurando o PEP CLI automaticamente a partir de {baseConfig.LocalRoot}..."
        : $"A configuração não tem versão atual. Configurando automaticamente a partir de {baseConfig.LocalRoot}...");
    }

    var proposal = new AutoConfigurator(_services.FileSystem).Propose(baseConfig);
    if (!proposal.CanApply)
    {
      if (!quiet)
      {
        ConfigAutoBuilder.Render(Ui, proposal);
        Ui.Fail("Não foi possível configurar automaticamente. Nada foi gravado.");
        Ui.Hint($"Confira se existe {Path.Combine(baseConfig.LocalRoot, AutoConfigurator.CurrentFolder, AutoConfigurator.CurrentRelease)} e execute 'pep config auto'.");
        Ui.Blank();
      }

      return false;
    }

    var backup = ConfigAutoBuilder.Apply(_services, proposal);
    if (!quiet)
    {
      ConfigAutoBuilder.Render(Ui, proposal);
      ConfigAutoBuilder.RenderApplied(_services, proposal, backup);
      Ui.Success(ReadyMessage);
      Ui.Blank();
    }

    return true;
  }
}
