using Spectre.Console;
using Spectre.Console.Rendering;

namespace BookOfEternityClient.UI;

public interface IExplorerConsole
{
    void Clear();
    void Write(IRenderable content);
    void WriteLine();
    void MarkupLine(string markup);
    string Ask(string prompt, string defaultValue = "");
    bool Confirm(string prompt, bool defaultValue = false);
    T Prompt<T>(IPrompt<T> prompt);
    ConsoleKeyInfo ReadKey();
}
