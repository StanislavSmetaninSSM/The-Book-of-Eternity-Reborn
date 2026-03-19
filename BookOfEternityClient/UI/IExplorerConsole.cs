using Spectre.Console;
using Spectre.Console.Rendering;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.UI;

public interface IExplorerConsole : ITextComposerConsole
{
    void Clear();
    void Write(IRenderable content);
    string Ask(string prompt, string defaultValue = "");
    bool Confirm(string prompt, bool defaultValue = false);
    T Prompt<T>(IPrompt<T> prompt);
}
