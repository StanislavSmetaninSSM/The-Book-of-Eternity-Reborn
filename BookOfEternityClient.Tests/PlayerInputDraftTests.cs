using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PlayerInputDraftTests
{
    [Fact]
    public void CooperativeWakePreservesNativePartialAndNeverStartsReadLine()
    {
        var source = new KeysOnlySource("черновик");
        var input = new CooperativePlayerInputSource(source, () => source.Keys.Count == 0, _ => { });
        var interrupted = Assert.Throws<TextComposerInputClosedException>(() => input.ReadLine());
        Assert.True(interrupted.Interrupted);
        Assert.Equal("черновик", interrupted.Draft);
        Assert.Equal(0, source.LineReads);
    }

    [Fact]
    public void CooperativeConsoleWinnerKeepsUnicodeAndReadsOnTheOwningThread()
    {
        var source = new KeysOnlySource("Дверь Ω\t");
        source.Keys.Enqueue(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));
        var input = new CooperativePlayerInputSource(source, () => false, _ => { });
        Assert.Equal("Дверь Ω\t", input.ReadLine());
        Assert.Equal(0, source.LineReads);
        Assert.All(source.ReaderThreads, thread => Assert.Equal(Environment.CurrentManagedThreadId, thread));
    }

    [Fact]
    public void CooperativeBrowserWinnerDoesNotConsumeTheNextConsoleKey()
    {
        var source = new KeysOnlySource("x");
        var input = new CooperativePlayerInputSource(source, () => true, _ => { });
        Assert.Throws<TextComposerInputClosedException>(() => input.ReadLine());
        Assert.Single(source.Keys);
        Assert.Empty(source.ReaderThreads);
    }

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

    private sealed class KeysOnlySource(string text) : IConsoleInputSource
    {
        internal Queue<ConsoleKeyInfo> Keys { get; } = new(text.Select(c => new ConsoleKeyInfo(c, 0, false, false, false)));
        internal List<int> ReaderThreads { get; } = [];
        internal int LineReads { get; private set; }
        public bool IsScripted => false;
        public bool KeyAvailable => Keys.Count > 0;
        public ConsoleKeyInfo ReadKey(bool intercept = true)
        {
            ReaderThreads.Add(Environment.CurrentManagedThreadId);
            return Keys.Dequeue();
        }
        public string? ReadLine() { LineReads++; throw new InvalidOperationException("A blocking reader must never be started."); }
        public void AssertCompleted() => Assert.Empty(Keys);
    }
}
