using Spectre.Console;
using Spectre.Console.Rendering;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.UI;

public sealed class SpectreExplorerConsole : IExplorerConsole
{
    private readonly Services.IClipboardService? _clipboardService;
    private readonly IConsoleInputSource _inputSource;

    public SpectreExplorerConsole(
        Services.IClipboardService? clipboardService = null,
        IConsoleInputSource? inputSource = null)
    {
        _clipboardService = clipboardService;
        _inputSource = inputSource ?? SystemConsoleInputSource.Instance;
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

    public string? ReadLine() => _inputSource.ReadLine();

    public bool KeyAvailable => _inputSource.KeyAvailable;

    public ConsoleKeyInfo ReadKey() => _inputSource.ReadKey(intercept: true);
}
