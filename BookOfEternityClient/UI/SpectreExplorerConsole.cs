using Spectre.Console;
using Spectre.Console.Rendering;

namespace BookOfEternityClient.UI;

public sealed class SpectreExplorerConsole : IExplorerConsole
{
    public void Clear() => AnsiConsole.Clear();

    public void Write(IRenderable content) => AnsiConsole.Write(content);

    public void WriteLine() => AnsiConsole.WriteLine();

    public void MarkupLine(string markup) => AnsiConsole.MarkupLine(markup);

    public string Ask(string prompt, string defaultValue = "") => AnsiConsole.Ask(prompt, defaultValue);

    public bool Confirm(string prompt, bool defaultValue = false) => AnsiConsole.Confirm(prompt, defaultValue);

    public T Prompt<T>(IPrompt<T> prompt) => AnsiConsole.Prompt(prompt);

    public ConsoleKeyInfo ReadKey() => Console.ReadKey(true);
}
