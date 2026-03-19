namespace BookOfEternityClient.Core;

public interface ITextComposerConsole
{
    void Markup(string markup);
    void MarkupLine(string markup);
    void WriteLine();
    string? ReadLine();
    bool KeyAvailable { get; }
    ConsoleKeyInfo ReadKey();
}
