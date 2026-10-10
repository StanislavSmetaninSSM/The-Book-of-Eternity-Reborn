using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PlayerInputDraftTests
{
    [Fact]
    public void InterruptedMultilinePreservesCompletedLinesAndPartialCurrentLine()
    {
        var console = new InterruptedConsole("Первая строка", "незавершённая вторая");
        var interruption = Assert.Throws<TextComposerInputClosedException>(() => TextComposer.Read(
            console, null, new TextComposerOptions
            {
                PromptMarkup = ">", PreserveNewlines = true, Mode = TextComposerMode.MultilineEditor
            }));
        Assert.Equal("Первая строка\nнезавершённая вторая", interruption.Draft);
        Assert.Equal(2, console.Reads);
    }

    [Fact]
    public void InterruptedConfirmationPreservesExistingClipboardDraft()
    {
        var console = new InterruptedConsole(null, "");
        var interruption = Assert.Throws<TextComposerInputClosedException>(() => TextComposer.Read(
            console, null, new TextComposerOptions
            {
                PromptMarkup = ">", PreserveNewlines = true, DefaultValue = "Черновик\nиз буфера"
            }));
        Assert.Equal("Черновик\nиз буфера", interruption.Draft);
        Assert.Equal(1, console.Reads);
    }

    private sealed class InterruptedConsole(string? completed, string partial) : ITextComposerConsole
    {
        public int Reads { get; private set; }
        public string? ReadLine()
        {
            Reads++;
            if (Reads == 1 && completed != null) return completed;
            throw new TextComposerInputClosedException(partial);
        }
        public bool KeyAvailable => false;
        public ConsoleKeyInfo ReadKey() => throw new InvalidOperationException("No background reader is permitted.");
        public void Markup(string markup) { }
        public void MarkupLine(string markup) { }
        public void WriteLine() { }
    }
}
