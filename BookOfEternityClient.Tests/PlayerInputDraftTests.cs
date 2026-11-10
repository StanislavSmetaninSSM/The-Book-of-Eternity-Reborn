using BookOfEternityClient.Core;
using System.Text;
using System.Globalization;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PlayerInputDraftTests
{
    [Theory]
    [InlineData("a\tb")]
    [InlineData("界e\u0301abcdefgh")]
    public void CooperativeCursorRedrawPreservesRenderedTextAcrossTabsWideCombiningAndWrap(string typed)
    {
        var source = new KeysOnlySource(typed);
        foreach (var key in new[] { ConsoleKey.Home, ConsoleKey.Delete, ConsoleKey.End, ConsoleKey.Enter })
            source.Keys.Enqueue(new ConsoleKeyInfo('\0', key, false, false, false));
        var actual = new LayoutTerminal(12); actual.Write(" > ");
        var expected = new LayoutTerminal(12); expected.Write(" > ");
        var firstLength = char.IsHighSurrogate(typed[0]) ? 2 : 1;
        var remaining = typed[firstLength..];
        expected.Write(remaining + Environment.NewLine);
        Assert.Equal(remaining, new CooperativePlayerInputSource(source, () => false, actual.Write).ReadLine());
        Assert.Equal(expected.Cells.OrderBy(pair => pair.Key), actual.Cells.OrderBy(pair => pair.Key));
        Assert.Equal(expected.Position, actual.Position);
    }

    // Independent terminal layout oracle: columns, not UTF-16/scalar counts.
    private sealed class LayoutTerminal(int width)
    {
        public Dictionary<int, string> Cells { get; } = new();
        public int Position { get; private set; }
        private int _saved;
        public void Write(string text)
        {
            for (var offset = 0; offset < text.Length;)
            {
                if (text[offset] == '\u001b')
                {
                    var command = text[offset..(offset + 3)]; offset += 3;
                    if (command == "\u001b[s") _saved = Position;
                    else if (command == "\u001b[u") Position = _saved;
                    else if (command == "\u001b[J") foreach (var cell in Cells.Keys.Where(cell => cell >= Position).ToArray()) Cells.Remove(cell);
                    else throw new InvalidOperationException("Unsupported terminal command: " + command);
                    continue;
                }
                var rune = Rune.GetRuneAt(text, offset); offset += rune.Utf16SequenceLength;
                if (rune.Value == '\r') { Position -= Position % width; continue; }
                if (rune.Value == '\n') { Position += width - Position % width; continue; }
                if (rune.Value == '\b') { if (Position % width > 0) Position--; continue; }
                if (rune.Value == '\t') { Position += 8 - Position % width % 8; continue; }
                if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark)
                { Cells[Position - 1] = Cells.GetValueOrDefault(Position - 1, "") + rune; continue; }
                var columns = rune.Value is >= 0x4e00 and <= 0x9fff ? 2 : 1;
                if (Position % width + columns > width) Position += width - Position % width;
                Cells[Position] = rune.ToString();
                for (var column = 1; column < columns; column++) Cells[Position + column] = "";
                Position += columns;
            }
        }
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("Исходный текст", "Исходный текст")]
    public void CooperativeNativeEndOfInputKeepsOrdinaryCtrlDSemantics(string typed, string? expected)
    {
        var source = new KeysOnlySource(typed);
        source.Keys.Enqueue(new ConsoleKeyInfo('\u0004', ConsoleKey.D, false, false, true));
        var input = new CooperativePlayerInputSource(source, () => source.Keys.Count == 0, _ => { });
        Assert.Equal(expected, input.ReadLine());
        Assert.Equal(0, source.LineReads);
    }

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
