using Spectre.Console;

namespace PEPCliHelper.Presentation;

/// <summary>Identidade visual "PEP CLI" no estilo de banner do Angular CLI.</summary>
public static class Banner
{
  private static readonly string[] Gradient = ["#00e5ff", "#00b8ff", "#3d8bff", "#6a5cff", "#9b4dff", "#c850c0"];

  private static readonly Dictionary<char, string[]> Unicode = new()
  {
    ['P'] = ["██████╗ ", "██╔══██╗", "██████╔╝", "██╔═══╝ ", "██║     ", "╚═╝     "],
    ['E'] = ["███████╗", "██╔════╝", "█████╗  ", "██╔══╝  ", "███████╗", "╚══════╝"],
    ['C'] = [" ██████╗", "██╔════╝", "██║     ", "██║     ", "╚██████╗", " ╚═════╝"],
    ['L'] = ["██╗     ", "██║     ", "██║     ", "██║     ", "███████╗", "╚══════╝"],
    ['I'] = ["██╗", "██║", "██║", "██║", "██║", "╚═╝"],
    [' '] = ["  ", "  ", "  ", "  ", "  ", "  "],
  };

  private static readonly Dictionary<char, string[]> AsciiFont = new()
  {
    ['P'] = [" ____  ", "|  _ \\ ", "| |_) |", "|  __/ ", "|_|    "],
    ['E'] = [" _____ ", "| ____|", "|  _|  ", "| |___ ", "|_____|"],
    ['C'] = ["  ____ ", " / ___|", "| |    ", "| |___ ", " \\____|"],
    ['L'] = [" _     ", "| |    ", "| |    ", "| |___ ", "|_____|"],
    ['I'] = [" ___ ", "|_ _|", " | | ", " | | ", "|___|"],
    [' '] = ["   ", "   ", "   ", "   ", "   "],
  };

  public static IReadOnlyList<string> Lines(bool ascii, string text = "PEP CLI")
  {
    var font = ascii ? AsciiFont : Unicode;
    var height = font['P'].Length;
    return Enumerable.Range(0, height)
      .Select(row => string.Concat(text.Select(c => font[c][row] + (ascii || c == ' ' ? string.Empty : " "))).TrimEnd())
      .ToList();
  }

  public static void Write(IAnsiConsole console, Theme theme, string version)
  {
    console.WriteLine();
    var lines = Lines(theme.Ascii);
    for (var i = 0; i < lines.Count; i++)
      console.MarkupLine($"  [{Gradient[i % Gradient.Length]}]{Markup.Escape(lines[i])}[/]");

    console.WriteLine();
    var separator = theme.Ascii ? "|" : "·";
    console.MarkupLine(
      $"  [bold {theme.Primary}]PEP CLI[/] [{theme.Muted}]v{Markup.Escape(version)} {separator} Linha RM Saúde {separator} merge seguro entre versões TFVC[/]");
    console.MarkupLine($"  [{theme.Muted}]O merge termina em Pending Changes. Nunca em check-in automático.[/]");
    console.WriteLine();
  }
}
