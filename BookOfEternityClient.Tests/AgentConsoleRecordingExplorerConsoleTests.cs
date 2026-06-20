using BookOfEternityClient.Core;
using BookOfEternityClient.UI;
using Spectre.Console;
using Spectre.Console.Rendering;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AgentConsoleRecordingExplorerConsoleTests
{
    [Fact]
    public void Prompt_SelectionFallbackCapturesChoicesWithoutAgentMetaText()
    {
        var console = new AgentConsoleRecordingExplorerConsole(new PromptlessExplorerConsole());
        var prompt = new SelectionPrompt<string>()
            .Title("Выберите действие")
            .AddChoices("Открыть письмо", "← Назад");

        var selected = console.Prompt(prompt);
        var captured = console.ReadCapturedText();

        Assert.Equal("← Назад", selected);
        Assert.Contains("Выберите действие", captured, StringComparison.Ordinal);
        Assert.Contains("Открыть письмо", captured, StringComparison.Ordinal);
        Assert.Contains("← Назад", captured, StringComparison.Ordinal);
        Assert.DoesNotContain("Agent Console", captured, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("выбран безопасный пункт", captured, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class PromptlessExplorerConsole : IExplorerConsole
    {
        public bool KeyAvailable => false;

        public void Clear()
        {
        }

        public void Write(IRenderable content)
        {
        }

        public void WriteLine()
        {
        }

        public void Markup(string markup)
        {
        }

        public void MarkupLine(string markup)
        {
        }

        public string Ask(string prompt, string defaultValue = "") => defaultValue;

        public bool Confirm(string prompt, bool defaultValue = false) => defaultValue;

        public T Prompt<T>(IPrompt<T> prompt) =>
            throw new NotSupportedException("Interactive prompts are unavailable in Agent Console recording mode.");

        public string? ReadLine() => string.Empty;

        public ConsoleKeyInfo ReadKey() => new('\r', ConsoleKey.Enter, false, false, false);
    }
}
