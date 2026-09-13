using PEPCliHelper.Core.Build;
using PEPCliHelper.Core.Get;
using PEPCliHelper.Core.Merge;
using PEPCliHelper.Core.Tfvc;
using Spectre.Console;

namespace PEPCliHelper.Presentation;

/// <summary>Tabelas de plano e resultado de merge, get e build.</summary>
public static class PlanRenderer
{
  public const string MergeConfirmation =
    "O merge será aplicado aos destinos selecionados e permanecerá em Pending Changes. Nenhum check-in será realizado.";

  public static void MergePlan(Ui ui, MergePlan plan, bool dryRun)
  {
    var request = plan.Request;
    ui.Title(dryRun ? "Plano de merge (dry-run: nada será alterado)" : "Plano de merge");

    var grid = ui.NewKeyValueGrid();
    ui.AddKeyValue(grid, "Projeto", $"[bold]{Ui.Escape(request.Project.Alias)}[/] ({Ui.Escape(request.Project.Name)})");
    ui.AddKeyValue(grid, "Origem", $"[bold]{Ui.Escape(request.Source.Label)}[/]  [{ui.Theme.Muted}]{Ui.Escape(plan.SourceLocation.ServerPath)}[/]");
    ui.AddKeyValue(grid, "Changeset", $"[bold {ui.Theme.Accent}]C{request.Changeset}[/]");
    ui.AddKeyValue(grid, "Destinos", Ui.Escape(string.Join(", ", request.Targets.Select(t => t.Id))));
    ui.AddKeyValue(grid, "Coleção", Ui.Escape(plan.Collection ?? "não configurada"));
    ui.AddKeyValue(grid, "Em falha", request.StopOnFailure ? "interromper (--stop-on-failure)" : "continuar nos demais destinos (padrão)");
    ui.Write(grid);

    if (plan.Scope is { } scope)
    {
      ui.Blank();
      ui.Markup($"  [bold]Escopo do changeset[/]  {ui.Theme.State(StateKind.Ok, $"{scope.Included.Count} incluído(s)")}   " +
        ui.Theme.State(scope.Excluded.Count > 0 ? StateKind.Warn : StateKind.Neutral, $"{scope.Excluded.Count} excluído(s)"));
      foreach (var item in scope.Included.Take(8))
        ui.Bullet(item, ui.Theme.OkColor);
      if (scope.Included.Count > 8)
        ui.Muted($"    … e mais {scope.Included.Count - 8} item(ns) incluído(s)");
      foreach (var item in scope.Excluded.Take(8))
        ui.Markup($"  [{ui.Theme.WarnColor}]{Ui.Escape(ui.Theme.Icon(StateKind.Warn))} fora do merge:[/] {Ui.Escape(item)}");
      if (scope.Excluded.Count > 8)
        ui.Muted($"    … e mais {scope.Excluded.Count - 8} item(ns) excluído(s)");
    }

    foreach (var blocker in plan.GlobalBlockers)
      ui.Fail(blocker);
    foreach (var warning in plan.GlobalWarnings)
      ui.Warn(warning);

    if (plan.Targets.Count == 0)
      return;

    ui.Blank();
    var table = ui.NewTable("Destino", "Situação", "Workspace", "Caminhos", "Verificações");
    foreach (var target in plan.Targets)
    {
      var status = target.Readiness switch
      {
        TargetReadiness.Ready => ui.Theme.State(StateKind.Ok, "Pronto"),
        TargetReadiness.AlreadyIntegrated => ui.Theme.State(StateKind.Info, "Já integrado"),
        _ => ui.Theme.State(StateKind.Blocked, "Bloqueado"),
      };

      var workspace = target.Mapping?.WorkspaceName is { } name
        ? $"{Ui.Escape(name)}{(target.IsLocalWorkspace ? " (local)" : string.Empty)}"
        : $"[{ui.Theme.Muted}]{Ui.Escape(target.Mapping?.StateText ?? "—")}[/]";

      var paths = $"{Ui.Escape(target.Location.LocalPath)}\n[{ui.Theme.Muted}]{Ui.Escape(target.Location.ServerPath)}[/]";

      var checks = new List<string>();
      if (target.Mapping is not null)
        checks.Add(ui.Theme.State(target.Mapping.IsValid ? StateKind.Ok : StateKind.Fail, $"mapeamento: {target.Mapping.StateText}"));
      if (target.Readiness != TargetReadiness.AlreadyIntegrated && target.Mapping?.IsValid == true)
      {
        checks.Add(ui.Theme.State(target.PendingInScope.Count > 0 ? StateKind.Fail : StateKind.Ok,
          $"pendências no escopo: {target.PendingInScope.Count} (fora: {target.PendingOutOfScope.Count})"));
        checks.Add(ui.Theme.State(target.ConflictsInScope.Count > 0 ? StateKind.Fail : StateKind.Ok,
          $"conflitos no escopo: {target.ConflictsInScope.Count}"));
        if (target.UnreconciledFiles.Count > 0)
          checks.Add(ui.Theme.State(StateKind.Fail, $"graváveis sem checkout: {target.UnreconciledFiles.Count}"));
        checks.Add(target.Outdated switch
        {
          true => ui.Theme.State(StateKind.Warn, "atualização: desatualizado"),
          false => ui.Theme.State(StateKind.Ok, "atualização: em dia"),
          _ => ui.Theme.State(StateKind.Neutral, "atualização: não verificada"),
        });
        if (target.PredictedConflicts.Count > 0)
          checks.Add(ui.Theme.State(StateKind.Warn, $"preview: {target.PredictedConflicts.Count} conflito(s) previsto(s)"));
        else if (target.PreviewItemCount is { } previewCount)
          checks.Add(ui.Theme.State(StateKind.Info, $"preview: {previewCount} item(ns)"));
      }

      table.AddRow(new Markup($"[bold]{Ui.Escape(target.Target.Id)}[/]"), new Markup(status), new Markup(workspace), new Markup(paths), new Markup(string.Join("\n", checks)));
    }

    ui.Write(table);

    foreach (var target in plan.Targets)
    {
      foreach (var blocker in target.Blockers)
        ui.Markup($"  {ui.Theme.State(StateKind.Blocked, target.Target.Id)}: {Ui.Escape(blocker)}");
      foreach (var warning in target.Warnings)
        ui.Markup($"  {ui.Theme.State(StateKind.Warn, target.Target.Id)}: {Ui.Escape(warning)}");
      foreach (var note in target.Notes)
        ui.Markup($"  {ui.Theme.State(StateKind.Info, target.Target.Id)}: {Ui.Escape(note)}");
      foreach (var line in target.PendingInScope.Take(5))
        ui.Muted($"      pendente no escopo: {line}");
      foreach (var line in target.UnreconciledFiles.Take(5))
        ui.Muted($"      gravável sem checkout: {line}");
      foreach (var line in target.ConflictsInScope.Take(5))
        ui.Muted($"      conflito: {line}");
    }

    if (dryRun)
    {
      ui.Blank();
      ui.Muted("  Dry-run: somente consultas foram executadas (sem merge, sem get, sem alteração de arquivos ou pending changes).");
      ui.Muted("  Conflitos só são conhecidos com certeza na execução real; o preview do TFVC indica o que seria aplicado.");
    }
  }

  public static void MergeResult(Ui ui, MergePlan plan, MergeExecutionResult result)
  {
    ui.Title("Resultado do merge");
    var table = ui.NewTable("Versão", "Estado", "Novas pendências", "Conflitos", "Duração", "Detalhe");
    foreach (var target in result.Targets)
    {
      table.AddRow(
        new Markup($"[bold]{Ui.Escape(target.Target.Id)}[/]"),
        new Markup(ui.Theme.State(Kind(target.State), MergeTargetResult.Describe(target.State))),
        new Markup(target.NewPendingChanges.Count.ToString()),
        new Markup(target.Conflicts.Count > 0 ? $"[{ui.Theme.FailColor}]{target.Conflicts.Count}[/]" : "0"),
        new Markup(target.Duration is { } d ? $"{d.TotalSeconds:0.0}s" : "—"),
        new Markup(Ui.Escape(target.Message)));
    }

    ui.Write(table);

    var conflicts = result.Targets.Where(t => t.Conflicts.Count > 0).ToList();
    if (conflicts.Count > 0)
    {
      ui.Title("Conflitos para resolver manualmente");
      foreach (var target in conflicts)
      {
        foreach (var conflict in target.Conflicts.Take(20))
          ui.Markup($"  {ui.Theme.State(StateKind.Fail, target.Target.Id)} {Ui.Escape(conflict)}");
      }
    }

    ui.Blank();
    var applied = $"Merge aplicado em {result.AppliedCount} de {result.SelectedCount} destinos selecionados";
    var others = new List<string>();
    if (result.Count(MergeTargetState.AlreadyIntegrated) is > 0 and var integrated)
      others.Add($"{integrated} já integrado(s)");
    if (result.Count(MergeTargetState.NoApplicableChanges) is > 0 and var noChanges)
      others.Add($"{noChanges} sem alterações aplicáveis");
    var summary = others.Count > 0 ? $"{applied} ({string.Join(", ", others)})." : $"{applied}.";
    ui.Markup($"  {ui.Theme.State(result.AppliedCount > 0 ? StateKind.Ok : StateKind.Warn, summary)}");

    var attention = result.Targets
      .Where(t => t.State is MergeTargetState.Blocked or MergeTargetState.NotStarted or MergeTargetState.Failed
        or MergeTargetState.Indeterminate or MergeTargetState.Cancelled)
      .ToList();
    foreach (var target in attention)
      ui.Markup($"  {ui.Theme.State(Kind(target.State), $"{target.Target.Id} — {MergeTargetResult.Describe(target.State)}")}: {Ui.Escape(target.Message)}");

    ui.Title("Próximos passos");
    ui.Hint("Revise as pending changes: pep pending list <versao> --project " + plan.Request.Project.Alias + "  (ou Source Control Explorer)");
    if (conflicts.Count > 0)
      ui.Hint("Resolva os conflitos no Visual Studio (Resolve Conflicts) antes do check-in.");
    if (result.ExitCode != 0)
      ui.Hint("Antes de repetir, execute com --dry-run: destinos já integrados não são reaplicados.");
    ui.Blank();
    ui.PendingChangesNotice("Nenhum check-in foi realizado. " + HelpRenderer.PendingChangesNotice);
  }

  public static void GetPlan(Ui ui, GetPlan plan, bool dryRun)
  {
    ui.Title(dryRun ? "Plano de get (dry-run)" : "Plano de get");
    var table = ui.NewTable("Alvo", "Situação", "Mapeamento", "Pendências", "Atualização");
    foreach (var target in plan.Targets)
    {
      table.AddRow(
        new Markup($"[bold]{Ui.Escape(target.Location.Label)}[/]\n[{ui.Theme.Muted}]{Ui.Escape(target.Location.LocalPath)}[/]"),
        new Markup(target.IsReady ? ui.Theme.State(StateKind.Ok, "Pronto") : ui.Theme.State(StateKind.Blocked, "Bloqueado")),
        new Markup(target.Mapping is null ? "—" : ui.Theme.State(target.Mapping.IsValid ? StateKind.Ok : StateKind.Fail, target.Mapping.StateText)),
        new Markup(target.PendingCount is { } pending ? (pending > 0 ? ui.Theme.State(StateKind.Warn, pending.ToString()) : "0") : "—"),
        new Markup(target.Outdated switch
        {
          true => ui.Theme.State(StateKind.Pending, "há itens a baixar"),
          false => ui.Theme.State(StateKind.Ok, "em dia"),
          _ => "—",
        }));
    }

    ui.Write(table);
    foreach (var target in plan.Targets)
    {
      foreach (var blocker in target.Blockers)
        ui.Markup($"  {ui.Theme.State(StateKind.Blocked, target.Location.Label)}: {Ui.Escape(blocker)}");
      foreach (var warning in target.Warnings)
        ui.Markup($"  {ui.Theme.State(StateKind.Warn, target.Location.Label)}: {Ui.Escape(warning)}");
    }

    ui.Muted("  O get usa 'tf get /recursive' sem /force e sem /overwrite: alterações locais não são sobrescritas.");
  }

  public static void GetResult(Ui ui, GetExecutionResult result)
  {
    ui.Title("Resultado do get");
    var table = ui.NewTable("Alvo", "Estado", "Duração", "Detalhe");
    foreach (var target in result.Targets)
    {
      var kind = target.State switch
      {
        GetTargetState.Updated or GetTargetState.UpToDate => StateKind.Ok,
        GetTargetState.Partial => StateKind.Warn,
        GetTargetState.Blocked => StateKind.Blocked,
        GetTargetState.NotStarted or GetTargetState.Cancelled => StateKind.Neutral,
        _ => StateKind.Fail,
      };
      table.AddRow(
        new Markup($"[bold]{Ui.Escape(target.Location.Label)}[/]"),
        new Markup(ui.Theme.State(kind, GetTargetResult.Describe(target.State))),
        new Markup(target.Duration is { } d ? $"{d.TotalSeconds:0.0}s" : "—"),
        new Markup(Ui.Escape(target.Message)));
    }

    ui.Write(table);
  }

  public static void BuildPlan(Ui ui, BuildPlan plan, bool dryRun)
  {
    ui.Title(dryRun ? "Plano de build (dry-run)" : "Plano de build");
    if (plan.MsBuild?.Found == true)
      ui.Muted($"  MSBuild: {plan.MsBuild.Path} (v{plan.MsBuild.Version})");
    foreach (var blocker in plan.GlobalBlockers)
      ui.Fail(blocker);

    var table = ui.NewTable("Ordem", "Alvo", "Solução", "Situação");
    var order = 1;
    foreach (var target in plan.Targets)
    {
      table.AddRow(
        new Markup((order++).ToString()),
        new Markup($"[bold]{Ui.Escape(target.Location.Label)}[/]"),
        new Markup(Ui.Escape(target.Solution ?? "—")),
        new Markup(target.IsReady ? ui.Theme.State(StateKind.Ok, "Pronto") : ui.Theme.State(StateKind.Blocked, "Bloqueado")));
    }

    ui.Write(table);
    foreach (var target in plan.Targets)
    {
      foreach (var blocker in target.Blockers)
        ui.Markup($"  {ui.Theme.State(StateKind.Blocked, target.Location.Label)}: {Ui.Escape(blocker)}");
      foreach (var warning in target.Warnings)
        ui.Markup($"  {ui.Theme.State(StateKind.Warn, target.Location.Label)}: {Ui.Escape(warning)}");
    }

    ui.Muted("  Build sequencial com MSBuild. Não executa get e não encerra processos.");
  }

  public static void BuildResult(Ui ui, BuildExecutionResult result)
  {
    ui.Title("Resultado do build");
    var table = ui.NewTable("Alvo", "Estado", "Duração", "Detalhe");
    foreach (var target in result.Targets)
    {
      var kind = target.State switch
      {
        BuildTargetState.Succeeded => StateKind.Ok,
        BuildTargetState.Blocked => StateKind.Blocked,
        BuildTargetState.NotStarted or BuildTargetState.Cancelled => StateKind.Neutral,
        _ => StateKind.Fail,
      };
      table.AddRow(
        new Markup($"[bold]{Ui.Escape(target.Location.Label)}[/]"),
        new Markup(ui.Theme.State(kind, BuildTargetResult.Describe(target.State))),
        new Markup(target.Duration is { } d ? $"{d.TotalSeconds:0.0}s" : "—"),
        new Markup(Ui.Escape(target.Message)));
    }

    ui.Write(table);
  }

  public static StateKind Kind(MergeTargetState state) => state switch
  {
    MergeTargetState.AppliedWithPendingChanges => StateKind.Ok,
    MergeTargetState.NoApplicableChanges or MergeTargetState.AlreadyIntegrated => StateKind.Info,
    MergeTargetState.AppliedWithConflicts or MergeTargetState.Indeterminate => StateKind.Warn,
    MergeTargetState.Blocked => StateKind.Blocked,
    MergeTargetState.NotStarted or MergeTargetState.Cancelled => StateKind.Neutral,
    _ => StateKind.Fail,
  };

  public static StateKind Kind(MappingState state) => state == MappingState.Mapped ? StateKind.Ok : StateKind.Fail;
}
