using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.Execution;
using PEPCliHelper.Core.Tfvc;
using PEPCliHelper.Infrastructure;

namespace PEPCliHelper.CommandsBuilders;

/// <summary>
/// pep login: autentica o tf.exe na coleção executando, no terminal do usuário, o comando somente leitura
/// "tf workspaces /collection:&lt;url&gt;" sem /noprompt. O tf.exe exibe o login e guarda a credencial no seu próprio cache.
/// O PEP CLI não recebe, não grava e não registra senha ou token.
/// </summary>
public sealed class LoginBuilder : CommandBuilderBase
{
  public LoginBuilder(AppServices services)
    : base(services)
  {
  }

  public override async Task<int> BuildAsync(CommandLine line, CancellationToken cancellationToken)
  {
    if (!Ui.CanPrompt)
    {
      throw new UsageException(
        "'pep login' precisa de um terminal interativo: o tf.exe exibe a janela/solicitação de login.",
        "Execute 'pep login' diretamente em um terminal (sem --json e sem --non-interactive).");
    }

    var config = Services.RequireConfig();
    var collection = config.Collection ?? throw new UsageException("Coleção TFVC não configurada.", "Informe 'colecao' na configuração.");
    var tf = await Services.Tools.RequireTfAsync(cancellationToken);

    Ui.Title("Login do tf.exe");
    Ui.Muted($"  Coleção: {collection}");
    Ui.Muted("  Comando somente leitura: tf workspaces. Se abrir a janela de login da Microsoft, entre com a conta da organização.");
    Ui.Muted("  O PEP CLI não recebe nem grava sua senha ou token; a credencial fica no cache do tf.exe.");
    Ui.Blank();

    var exitCode = await Services.AttachedRunner.RunAsync(
      new Command(tf.Path!, ["workspaces", $"/collection:{collection}"]), cancellationToken);

    Ui.Blank();
    var check = await Ui.WithStatusAsync("Confirmando acesso sem interação", _ => Services.Tfvc.ListWorkspacesAsync(collection, cancellationToken));
    if (check.IsSuccess)
    {
      Ui.Success("tf.exe autenticado: o PEP CLI já consegue acessar a coleção.");
      Ui.Hint("Próximo passo: pep doctor  →  pep merge ... --dry-run");
      return ExitCodes.Success;
    }

    throw new PepCliException(
      $"O tf.exe ainda não acessa a coleção ({check.StatusText}; login retornou código {exitCode}). {check.Summary}",
      check.IsEnvironmentError ? ExitCodes.Precondition : ExitCodes.OperationFailed,
      collection,
      "Merge, get e consultas TFVC continuam bloqueados.",
      "Nada foi alterado.",
      "Confira no Visual Studio (Team Explorer → Manage Connections) se a conta tem acesso à coleção; " +
      "se a janela de login não abriu, rode o mesmo comando no 'Developer PowerShell for VS 2022'.");
  }
}
