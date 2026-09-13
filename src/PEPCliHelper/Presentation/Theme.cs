using Spectre.Console;

namespace PEPCliHelper.Presentation;

public enum StateKind
{
  Ok,
  Fail,
  Warn,
  Info,
  Blocked,
  Pending,
  Neutral,
}

/// <summary>Cores e ícones. Estado nunca depende só de cor: todo ícone vem com texto.</summary>
public sealed class Theme
{
  public Theme(bool ascii)
  {
    Ascii = ascii;
  }

  public bool Ascii { get; }

  public string Primary => "deepskyblue1";

  public string Accent => "mediumpurple1";

  public string Muted => "grey";

  public string OkColor => "green3";

  public string FailColor => "red1";

  public string WarnColor => "gold1";

  public string Arrow => Ascii ? ">" : "›";

  public string Bullet => Ascii ? "-" : "•";

  public TableBorder TableBorder => Ascii ? TableBorder.Ascii : TableBorder.Rounded;

  public BoxBorder BoxBorder => Ascii ? BoxBorder.Ascii : BoxBorder.Rounded;

  public string Icon(StateKind kind) => (kind, Ascii) switch
  {
    (StateKind.Ok, false) => "✔",
    (StateKind.Fail, false) => "✖",
    (StateKind.Warn, false) => "▲",
    (StateKind.Info, false) => "●",
    (StateKind.Blocked, false) => "⊘",
    (StateKind.Pending, false) => "◷",
    (StateKind.Neutral, false) => "–",
    (StateKind.Ok, true) => "[OK]",
    (StateKind.Fail, true) => "[FALHA]",
    (StateKind.Warn, true) => "[AVISO]",
    (StateKind.Info, true) => "[INFO]",
    (StateKind.Blocked, true) => "[BLOQ]",
    (StateKind.Pending, true) => "[PEND]",
    _ => "[-]",
  };

  public string Color(StateKind kind) => kind switch
  {
    StateKind.Ok => OkColor,
    StateKind.Fail => FailColor,
    StateKind.Warn or StateKind.Pending => WarnColor,
    StateKind.Blocked => "orange1",
    StateKind.Info => Primary,
    _ => Muted,
  };

  /// <summary>Markup de estado: ícone + texto, colorido quando o terminal suporta.</summary>
  public string State(StateKind kind, string text) =>
    $"[{Color(kind)}]{Markup.Escape(Icon(kind))} {Markup.Escape(text)}[/]";
}
