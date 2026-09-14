using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.Execution;
using PEPCliHelper.Core.Tfvc;

namespace PEPCliHelper.Infrastructure;

/// <summary>
/// Login automático do tf.exe (substitui o antigo comando de login): quando o tf.exe responde TF30063, executa no terminal
/// do usuário o comando somente leitura "tf workspaces /collection:&lt;url&gt;" sem /noprompt, para o tf.exe exibir o login
/// e guardar a credencial no seu próprio cache. O PEP CLI não recebe, não grava e não registra senha ou token.
/// No máximo UMA tentativa interativa por processo, mesmo com consultas em paralelo.
/// </summary>
public sealed class TfvcAuthenticator
{
  private readonly AppServices _services;
  private readonly SemaphoreSlim _gate = new(1, 1);
  private bool _attempted;
  private bool _loggedIn;

  public TfvcAuthenticator(AppServices services)
  {
    _services = services;
  }

  /// <summary>True se o login foi concluído neste processo (o chamador deve repetir a consulta); false sem login possível.</summary>
  public async Task<bool> EnsureLoggedInAsync(CancellationToken cancellationToken)
  {
    await _gate.WaitAsync(cancellationToken);
    try
    {
      if (_attempted)
        return _loggedIn;
      if (!_services.Ui.CanPrompt)
        return false;

      _attempted = true;
      _loggedIn = await LoginAsync(cancellationToken);
      return _loggedIn;
    }
    finally
    {
      _gate.Release();
    }
  }

  private async Task<bool> LoginAsync(CancellationToken cancellationToken)
  {
    var ui = _services.Ui;
    string collection;
    try
    {
      if (_services.RequireConfig().Collection is not { } configured)
        return false;
      collection = configured;
      var tf = await _services.Tools.RequireTfAsync(cancellationToken);

      ui.Blank();
      ui.Warn("Sem credencial do tf.exe (TF30063). Abrindo o login do TFS; entre com a conta da coleção...");
      ui.Muted($"  Coleção: {collection}. O PEP CLI não recebe nem grava sua senha ou token; a credencial fica no cache do tf.exe.");
      await _services.AttachedRunner.RunAsync(new Command(tf.Path!, ["workspaces", $"/collection:{collection}"]), cancellationToken);
    }
    catch (PepCliException ex)
    {
      ui.Warn($"Login automático do TFS indisponível: {ex.Message}");
      return false;
    }

    var check = await _services.InnerTfvc.ListWorkspacesAsync(collection, cancellationToken);
    if (check.IsSuccess)
    {
      ui.Success("Conectado ao TFS. Continuando...");
      return true;
    }

    ui.Warn($"O login do TFS não liberou o acesso à coleção ({check.StatusText}). O login automático não será repetido nesta execução.");
    return false;
  }
}
