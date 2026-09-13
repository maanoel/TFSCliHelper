using PEPCliHelper.CommandChain;
using PEPCliHelper.Core.Common;
using PEPCliHelper.Infrastructure;
using Spectre.Console;

namespace PEPCliHelper.Presentation;

public static class HelpRenderer
{
  public const string PendingChangesNotice =
    "O PEP CLI realiza o merge e mantém as alterações em Pending Changes. A revisão, resolução de conflitos e o check-in são responsabilidades do usuário.";

  public static readonly string[] CategoryOrder =
    ["Merge", "Get e build", "Ambientes e configuração", "Diagnóstico e TFVC", "Ferramentas locais", "Histórico", "Ajuda"];

  public static void RenderGeneral(Ui ui, ChainOfCommands chains)
  {
    if (ui.Json)
    {
      ui.WriteJson(new
      {
        uso = "pep <comando> [opções]",
        comandos = chains.Chains.Where(c => !c.Help.Hidden).Select(c => new { comando = c.Help.Name, categoria = c.Help.Category, resumo = c.Help.Summary }),
        codigosSaida = ExitCodeTable(),
      });
      return;
    }

    ui.Banner();
    ui.Markup($"  [bold]Uso:[/] pep [{ui.Theme.Primary}]<comando>[/] [[opções]]      [{ui.Theme.Muted}]sem argumentos abre o menu interativo[/]");

    foreach (var group in chains.Chains.Where(c => !c.Help.Hidden).GroupBy(c => c.Help.Category)
      .OrderBy(g => CategoryIndex(g.Key)))
    {
      ui.Title(group.Key);
      var grid = new Grid();
      grid.AddColumn(new GridColumn().NoWrap().PadLeft(2).PadRight(3));
      grid.AddColumn();
      foreach (var chain in group)
        grid.AddRow($"[{ui.Theme.Primary}]{Ui.Escape(chain.Help.Usage)}[/]", Ui.Escape(chain.Help.Summary));
      ui.Write(grid);
    }

    ui.Title("Opções globais");
    var options = new Grid();
    options.AddColumn(new GridColumn().NoWrap().PadLeft(2).PadRight(3));
    options.AddColumn();
    foreach (var (name, value, description) in GlobalOptions.Descriptions)
      options.AddRow($"[{ui.Theme.Primary}]{Ui.Escape(name + (value is null ? string.Empty : " " + value))}[/]", Ui.Escape(description));
    ui.Write(options);

    ui.Title("Códigos de saída");
    var codes = new Grid();
    codes.AddColumn(new GridColumn().NoWrap().PadLeft(2).PadRight(3));
    codes.AddColumn();
    foreach (var (code, description) in ExitCodeTable())
      codes.AddRow($"[{ui.Theme.Primary}]{code}[/]", Ui.Escape(description));
    ui.Write(codes);

    ui.Blank();
    ui.PendingChangesNotice(PendingChangesNotice);
    ui.Muted("  Detalhes de um comando: pep <comando> --help");
  }

  public static void RenderCommand(Ui ui, CommandHelp help)
  {
    if (ui.Json)
    {
      ui.WriteJson(new
      {
        comando = help.Name,
        resumo = help.Summary,
        detalhes = help.Details,
        uso = help.Usage,
        argumentos = help.Arguments.Select(a => new { nome = a.Name, obrigatorio = a.Required, descricao = a.Description }),
        opcoes = help.Options.Select(o => new { opcao = o.Display, repetivel = o.Repeatable, descricao = o.Description }),
        exemplos = help.Examples.Select(e => new { comando = e.Command, descricao = e.Description }),
      });
      return;
    }

    ui.Blank();
    ui.Markup($"  [bold {ui.Theme.Primary}]pep {Ui.Escape(help.Name)}[/]  [{ui.Theme.Muted}]{Ui.Escape(help.Category)}[/]");
    ui.Markup($"  {Ui.Escape(help.Summary)}");
    if (help.Details is not null)
    {
      ui.Blank();
      foreach (var paragraph in help.Details.Split('\n'))
        ui.Muted("  " + paragraph);
    }

    ui.Title("Uso");
    ui.Markup($"  {Ui.Escape(help.Usage)}");

    if (help.Arguments.Count > 0)
    {
      ui.Title("Argumentos");
      var grid = new Grid();
      grid.AddColumn(new GridColumn().NoWrap().PadLeft(2).PadRight(3));
      grid.AddColumn();
      foreach (var argument in help.Arguments)
        grid.AddRow($"[{ui.Theme.Primary}]{Ui.Escape(argument.Display)}[/]", Ui.Escape(argument.Description));
      ui.Write(grid);
    }

    if (help.Options.Count > 0)
    {
      ui.Title("Opções");
      var grid = new Grid();
      grid.AddColumn(new GridColumn().NoWrap().PadLeft(2).PadRight(3));
      grid.AddColumn();
      foreach (var option in help.Options)
        grid.AddRow($"[{ui.Theme.Primary}]{Ui.Escape(option.Display)}[/]", Ui.Escape(option.Description + (option.Repeatable ? " (pode repetir)" : string.Empty)));
      ui.Write(grid);
    }

    if (help.Examples.Count > 0)
    {
      ui.Title("Exemplos");
      foreach (var example in help.Examples)
      {
        ui.Markup($"  [{ui.Theme.Accent}]{Ui.Escape(example.Command)}[/]");
        ui.Muted($"    {example.Description}");
      }
    }

    ui.Blank();
    ui.Muted("  Opções globais (--json, --non-interactive, --yes, --no-color, --ascii, --config): pep help");
  }

  private static int CategoryIndex(string category)
  {
    var index = Array.IndexOf(CategoryOrder, category);
    return index < 0 ? int.MaxValue : index;
  }

  public static IReadOnlyList<(int Code, string Description)> ExitCodeTable() =>
  [
    (ExitCodes.Success, ExitCodes.Describe(ExitCodes.Success)),
    (ExitCodes.Unexpected, ExitCodes.Describe(ExitCodes.Unexpected)),
    (ExitCodes.Usage, ExitCodes.Describe(ExitCodes.Usage)),
    (ExitCodes.Precondition, ExitCodes.Describe(ExitCodes.Precondition)),
    (ExitCodes.OperationFailed, ExitCodes.Describe(ExitCodes.OperationFailed)),
    (ExitCodes.ConflictOrPartial, ExitCodes.Describe(ExitCodes.ConflictOrPartial)),
    (ExitCodes.Cancelled, ExitCodes.Describe(ExitCodes.Cancelled)),
  ];
}
