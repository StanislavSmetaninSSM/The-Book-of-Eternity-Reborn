using Spectre.Console;

namespace BookOfEternityClient.Core;

internal sealed class StandardTextComposerConsole : ITextComposerConsole
{
    private readonly IConsoleInputSource _inputSource;

    public static StandardTextComposerConsole Instance { get; } = new(SystemConsoleInputSource.Instance);

    public StandardTextComposerConsole(IConsoleInputSource inputSource)
    {
        _inputSource = inputSource;
    }

    public void Markup(string markup) => AnsiConsole.Markup(markup);

    public void MarkupLine(string markup) => AnsiConsole.MarkupLine(markup);

    public void WriteLine() => AnsiConsole.WriteLine();

    public string? ReadLine() => _inputSource.ReadLine();

    public bool KeyAvailable => _inputSource.KeyAvailable;

    public ConsoleKeyInfo ReadKey() => _inputSource.ReadKey(intercept: true);
}
