using Spectre.Console;

namespace BookOfEternityClient.Core;

internal sealed class StandardTextComposerConsole : ITextComposerConsole
{
    public static StandardTextComposerConsole Instance { get; } = new();

    private StandardTextComposerConsole()
    {
    }

    public void Markup(string markup) => AnsiConsole.Markup(markup);

    public void MarkupLine(string markup) => AnsiConsole.MarkupLine(markup);

    public void WriteLine() => AnsiConsole.WriteLine();

    public string? ReadLine() => Console.ReadLine();

    public bool KeyAvailable => Console.KeyAvailable;

    public ConsoleKeyInfo ReadKey() => Console.ReadKey(true);
}
