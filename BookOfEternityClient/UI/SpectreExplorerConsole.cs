using Spectre.Console;
using Spectre.Console.Rendering;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.UI;

public sealed class SpectreExplorerConsole : IExplorerConsole
{
    private readonly Services.IClipboardService? _clipboardService;

    public SpectreExplorerConsole(Services.IClipboardService? clipboardService = null)
    {
        _clipboardService = clipboardService;
    }

    public void Clear() => AnsiConsole.Clear();

    public void Write(IRenderable content) => AnsiConsole.Write(content);

    public void WriteLine() => AnsiConsole.WriteLine();

    public void Markup(string markup) => AnsiConsole.Markup(markup);

    public void MarkupLine(string markup) => AnsiConsole.MarkupLine(markup);

    public string Ask(string prompt, string defaultValue = "")
    {
        return TextComposer.Read(
            this,
            _clipboardService,
            new TextComposerOptions
            {
                PromptMarkup = prompt,
                DefaultValue = defaultValue,
                PreserveNewlines = false
            });
    }

    public bool Confirm(string prompt, bool defaultValue = false) => AnsiConsole.Confirm(prompt, defaultValue);

    public T Prompt<T>(IPrompt<T> prompt) => AnsiConsole.Prompt(prompt);

    public string? ReadLine() => Console.ReadLine();

    public bool KeyAvailable => Console.KeyAvailable;

    public ConsoleKeyInfo ReadKey() => Console.ReadKey(true);
}
