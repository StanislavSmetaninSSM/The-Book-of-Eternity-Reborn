using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PlayerInputDraftTests
{
    [Theory]
    [InlineData("left-insert", "abXc")]
    [InlineData("home-delete", "bc")]
    [InlineData("left-backspace", "ac")]
    [InlineData("home-right-end", "XabcY")]
    public void CooperativeLineEditingPreservesOrdinaryCursorAndDeleteBehavior(string edit, string expected)
    {
        var source = new KeysOnlySource("abc");
        void Key(ConsoleKey key) => source.Keys.Enqueue(new ConsoleKeyInfo('\0', key, false, false, false));
        void Character(char value) => source.Keys.Enqueue(new ConsoleKeyInfo(value, ConsoleKey.NoName, false, false, false));
        switch (edit)
        {
            case "left-insert": Key(ConsoleKey.LeftArrow); Character('X'); break;
            case "home-delete": Key(ConsoleKey.Home); Key(ConsoleKey.Delete); break;
            case "left-backspace": Key(ConsoleKey.LeftArrow); Key(ConsoleKey.Backspace); break;
            case "home-right-end": Key(ConsoleKey.Home); Character('X'); Key(ConsoleKey.RightArrow); Key(ConsoleKey.End); Character('Y'); break;
        }
        Key(ConsoleKey.Enter);
        Assert.Equal(expected, new CooperativePlayerInputSource(source, () => false, _ => { }).ReadLine());
        Assert.Equal(0, source.LineReads);
    }

    [Fact]
    public void EndOfInputDoesNotDuplicateCompletedMultilineDraft()
    {
        var console = new EndOfInputConsole();
        var interruption = Assert.Throws<TextComposerInputClosedException>(() => TextComposer.Read(console, null,
            new TextComposerOptions { PromptMarkup = ">", PreserveNewlines = true, Mode = TextComposerMode.MultilineEditor }));
        Assert.Equal("Завершённая строка", interruption.Draft);
    }

    private sealed class EndOfInputConsole : ITextComposerConsole
    {
        private bool _read;
        public string? ReadLine() { if (_read) return null; _read = true; return "Завершённая строка"; }
        public bool KeyAvailable => false;
        public ConsoleKeyInfo ReadKey() => throw new InvalidOperationException();
        public void Markup(string markup) { }
        public void MarkupLine(string markup) { }
        public void WriteLine() { }
    }

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
