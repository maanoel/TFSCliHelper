using PEPCliHelper.CommandsBuilders;
using PEPCliHelper.Core.Catalog;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.Configuration;
using PEPCliHelper.Core.Environments;
using PEPCliHelper.Presentation;

namespace PEPCliHelper.Menus;

/// <summary>
/// Primeira execução (decisão de 2026-09-14): sem configuração ou sem versão atual, oferece a configuração automática
/// antes do menu. A seleção da primeira opção é a confirmação. Configuração inválida nunca é sobrescrita.
/// </summary>
public sealed class FirstRunSetup
{
  public const string AutoOption = "Aplicar configuração automática (recomendado)";
  public const string ManualOption = "Configurar manualmente (pep env configure)";
  public const string SkipOption = "Agora não";

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

  public async Task RunAsync(CancellationToken cancellationToken)
  {
    var (baseConfig, status) = ConfigAutoBuilder.LoadBase(_services);
    var proposal = new AutoConfigurator(_services.FileSystem).Propose(baseConfig);

    Ui.Title("Primeira configuração");
    Ui.Muted(status == ConfigLoadStatus.Missing
      ? $"  Nenhuma configuração encontrada em {_services.ConfigStore.Path}. Proposta detectada em {baseConfig.LocalRoot}:"
      : $"  A configuração não tem versão atual. Proposta detectada em {baseConfig.LocalRoot}:");
    ConfigAutoBuilder.Render(Ui, proposal);

    var options = proposal.CanApply ? new[] { AutoOption, ManualOption, SkipOption } : [ManualOption, SkipOption];
    var choice = await Ui.SelectAsync("Como deseja configurar?", options, o => o, cancellationToken);

    switch (choice)
    {
      case AutoOption:
        var backup = ConfigAutoBuilder.Apply(_services, proposal);
        ConfigAutoBuilder.RenderApplied(_services, proposal, backup);
        break;

      case ManualOption:
        if (status == ConfigLoadStatus.Missing && await PepApp.DispatchAsync(_services, ["config", "init"], cancellationToken) != ExitCodes.Success)
          return;
        await PepApp.DispatchAsync(_services, ["env", "configure"], cancellationToken);
        break;

      default:
        Ui.Muted("  Sem problemas: configure depois em \"Ambientes e versões\" ou com 'pep config auto'.");
        break;
    }

    Ui.Blank();
  }
}
