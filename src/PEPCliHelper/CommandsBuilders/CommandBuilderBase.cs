using PEPCliHelper.Core.Catalog;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Core.History;
using PEPCliHelper.Infrastructure;
using PEPCliHelper.Interface;
using PEPCliHelper.Presentation;

namespace PEPCliHelper.CommandsBuilders;

/// <summary>Validações e utilidades comuns aos builders. Nenhuma regra de negócio aqui.</summary>
public abstract class CommandBuilderBase : ICommandBuilder
{
  protected CommandBuilderBase(AppServices services)
  {
    Services = services;
  }

  protected AppServices Services { get; }

  protected Ui Ui => Services.Ui;

  public abstract Task<int> BuildAsync(CommandLine line, CancellationToken cancellationToken);

  protected static VersionEntry ResolveVersion(VersionCatalog catalog, string token, string role)
  {
    var resolution = catalog.ResolveVersion(token);
    role = role.Length == 0 ? string.Empty : " " + role.Trim();
    return resolution.Status switch
    {
      VersionResolutionStatus.Found => resolution.Version!,
      VersionResolutionStatus.Ambiguous => throw new UsageException(
        $"Versão{role} ambígua: '{token}' corresponde a {string.Join(", ", resolution.Candidates)}.",
        "Informe a versão completa (ex.: 12.1.2606)."),
      VersionResolutionStatus.Inactive => throw new UsageException(
        $"A versão{role} '{resolution.Version!.Id}' está desativada no catálogo.",
        "A configuração automática ativa as 4 legadas mais novas; escolha uma versão ativa (veja 'pep env list')."),
      _ => throw new UsageException(
        $"Versão{role} desconhecida: '{token}'.",
        $"Versões ativas: {string.Join(", ", catalog.Active.Select(v => v.Id))}. Veja 'pep env list'."),
    };
  }

  protected static ProjectDefinition ResolveProject(VersionCatalog catalog, string alias)
  {
    if (alias.Equals("front", StringComparison.OrdinalIgnoreCase))
    {
      throw new UsageException(
        "O projeto 'front' não é suportado: o front do PEP não é mais gerenciado pelo PEP CLI (migrou para o Git).",
        $"Projetos disponíveis: {string.Join(", ", catalog.Projects.Select(p => p.Alias))}.");
    }

    return catalog.FindProject(alias) ?? throw new UsageException(
      $"Projeto desconhecido: '{alias}'.",
      $"Projetos disponíveis: {string.Join(", ", catalog.Projects.Select(p => $"{p.Alias} ({p.Name})"))}.");
  }

  protected static ProjectDefinition? OptionalProject(VersionCatalog catalog, string? alias) =>
    alias is null ? null : ResolveProject(catalog, alias);

  protected string RequireValue(string? value, string description, CommandLine line) =>
    !string.IsNullOrWhiteSpace(value)
      ? value
      : throw new UsageException($"Informe {description}.", $"Uso: {line.Help.Usage}. Veja 'pep {line.Help.Name} --help'.");

  /// <summary>--yes confirma; em terminal interativo pergunta; em modo não interativo exige --yes.</summary>
  protected async Task<bool> ConfirmAsync(string question, CancellationToken cancellationToken, bool defaultValue = false)
  {
    if (Services.Options.Yes)
      return true;

    if (!Ui.CanPrompt)
    {
      throw new UsageException(
        "Confirmação necessária em modo não interativo.",
        "Revise o plano (use --dry-run) e repita com --yes para confirmar.");
    }

    return await Ui.ConfirmAsync(question, defaultValue, cancellationToken);
  }

  protected ExecutionSession BeginHistory(string command, CommandLine line)
  {
    var arguments = line.Help.Path.Concat(line.Positionals)
      .Concat(line.Help.Options.SelectMany(o => line.OptionValues(o.Name).Select(v => $"--{o.Name}={v}")))
      .Concat(line.Help.Options.Where(o => !o.TakesValue && line.Flag(o.Name)).Select(o => $"--{o.Name}"))
      .ToList();

    try
    {
      return Services.Journal.Begin(command, arguments);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
      Ui.Warn($"Histórico indisponível: {ex.Message}");
      return ExecutionSession.Detached(command, arguments, Services.Time);
    }
  }

  protected void ReportHistory(ExecutionSession session)
  {
    if (session.PersistenceWarning is not null)
      Ui.Warn(session.PersistenceWarning);
    else if (session.Id != "sem-historico")
      Ui.Muted($"  Histórico: pep history show {session.Id}");
  }

  protected static StateKind Kind(bool ok) => ok ? StateKind.Ok : StateKind.Fail;
}
