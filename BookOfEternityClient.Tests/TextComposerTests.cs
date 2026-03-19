using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class TextComposerTests
{
    [Fact]
    public void ImmediateMode_PreservesNewlines_WhenConfiguredForMultilineContent()
    {
        var console = new FakeComposerConsole
        {
            ReadLines = new Queue<string?>(new[] { "Альфа" }),
            ReadKeys = new Queue<ConsoleKeyInfo>(new[]
            {
                new ConsoleKeyInfo('Б', ConsoleKey.B, false, false, false),
                new ConsoleKeyInfo('е', ConsoleKey.E, false, false, false),
                new ConsoleKeyInfo('т', ConsoleKey.T, false, false, false),
                new ConsoleKeyInfo('а', ConsoleKey.A, false, false, false),
                new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false),
                new ConsoleKeyInfo('Г', ConsoleKey.G, false, false, false),
                new ConsoleKeyInfo('а', ConsoleKey.A, false, false, false),
                new ConsoleKeyInfo('м', ConsoleKey.M, false, false, false),
                new ConsoleKeyInfo('м', ConsoleKey.M, false, false, false),
                new ConsoleKeyInfo('а', ConsoleKey.A, false, false, false)
            })
        };

        var value = TextComposer.Read(console, clipboardService: null, new TextComposerOptions
        {
            PromptMarkup = "[cyan]>[/]",
            PreserveNewlines = true
        });

        Assert.Equal("Альфа\nБета\nГамма", value);
    }

    [Fact]
    public void ImmediateMode_CollapsesNewlines_ForSingleLinePrompts()
    {
        var console = new FakeComposerConsole
        {
            ReadLines = new Queue<string?>(new[] { "Альфа" }),
            ReadKeys = new Queue<ConsoleKeyInfo>(new[]
            {
                new ConsoleKeyInfo('Б', ConsoleKey.B, false, false, false),
                new ConsoleKeyInfo('е', ConsoleKey.E, false, false, false),
                new ConsoleKeyInfo('т', ConsoleKey.T, false, false, false),
                new ConsoleKeyInfo('а', ConsoleKey.A, false, false, false)
            })
        };

        var value = TextComposer.Read(console, clipboardService: null, new TextComposerOptions
        {
            PromptMarkup = "[cyan]>[/]"
        });

        Assert.Equal("Альфа Бета", value);
    }

    [Fact]
    public void ImmediateMode_UsesClipboardShortcut_WhenClipboardServiceProvided()
    {
        var console = new FakeComposerConsole
        {
            ReadLines = new Queue<string?>(new[] { "\\p" })
        };
        var clipboard = new TestClipboardService
        {
            Text = "Первая строка\nВторая строка"
        };

        var value = TextComposer.Read(console, clipboard, new TextComposerOptions
        {
            PromptMarkup = "[cyan]>[/]",
            PreserveNewlines = true
        });

        Assert.Equal("Первая строка\nВторая строка", value);
    }

    [Fact]
    public void MultilineMode_UsesDirectPasteBlockWithoutSpecialClipboardAction()
    {
        var console = new FakeComposerConsole
        {
            ReadLines = new Queue<string?>(new[] { "Первый абзац" }),
            ReadKeys = new Queue<ConsoleKeyInfo>(new[]
            {
                new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false),
                new ConsoleKeyInfo('В', ConsoleKey.V, false, false, false),
                new ConsoleKeyInfo('т', ConsoleKey.T, false, false, false),
                new ConsoleKeyInfo('о', ConsoleKey.O, false, false, false),
                new ConsoleKeyInfo('р', ConsoleKey.R, false, false, false),
                new ConsoleKeyInfo('о', ConsoleKey.O, false, false, false),
                new ConsoleKeyInfo('й', ConsoleKey.Q, false, false, false),
                new ConsoleKeyInfo(' ', ConsoleKey.Spacebar, false, false, false),
                new ConsoleKeyInfo('а', ConsoleKey.A, false, false, false),
                new ConsoleKeyInfo('б', ConsoleKey.B, false, false, false),
                new ConsoleKeyInfo('з', ConsoleKey.Z, false, false, false),
                new ConsoleKeyInfo('а', ConsoleKey.A, false, false, false),
                new ConsoleKeyInfo('ц', ConsoleKey.C, false, false, false)
            })
        };

        var value = TextComposer.Read(console, clipboardService: null, new TextComposerOptions
        {
            PromptMarkup = "[cyan]Текст:[/]",
            PreserveNewlines = true,
            Mode = TextComposerMode.MultilineEditor
        });

        Assert.Equal("Первый абзац\n\nВторой абзац", value);
    }

    [Fact]
    public void MultilineMode_UsesDoubleBlankLineToSubmitManualText()
    {
        var console = new FakeComposerConsole
        {
            ReadLines = new Queue<string?>(new[]
            {
                "Первый абзац",
                string.Empty,
                "Второй абзац",
                string.Empty,
                string.Empty
            })
        };

        var value = TextComposer.Read(console, clipboardService: null, new TextComposerOptions
        {
            PromptMarkup = "[cyan]Текст:[/]",
            PreserveNewlines = true,
            Mode = TextComposerMode.MultilineEditor
        });

        Assert.Equal("Первый абзац\n\nВторой абзац", value);
    }

    [Fact]
    public void MultilineMode_EmptyFirstSubmit_KeepsDefaultValue()
    {
        var console = new FakeComposerConsole
        {
            ReadLines = new Queue<string?>(new[] { string.Empty })
        };

        var value = TextComposer.Read(console, clipboardService: null, new TextComposerOptions
        {
            PromptMarkup = "[cyan]Текст:[/]",
            DefaultValue = "Текущее значение",
            PreserveNewlines = true,
            Mode = TextComposerMode.MultilineEditor
        });

        Assert.Equal("Текущее значение", value);
    }

    [Fact]
    public void MultilineMode_ClearCommand_ReturnsEmptyString()
    {
        var console = new FakeComposerConsole
        {
            ReadLines = new Queue<string?>(new[] { "/clear" })
        };

        var value = TextComposer.Read(console, clipboardService: null, new TextComposerOptions
        {
            PromptMarkup = "[cyan]Текст:[/]",
            DefaultValue = "Старый текст",
            PreserveNewlines = true,
            Mode = TextComposerMode.MultilineEditor,
            AllowClearCommand = true
        });

        Assert.Equal(string.Empty, value);
    }

    private sealed class FakeComposerConsole : ITextComposerConsole
    {
        public Queue<string?> ReadLines { get; init; } = new();
        public Queue<ConsoleKeyInfo> ReadKeys { get; init; } = new();

        public void Markup(string markup)
        {
        }

        public void MarkupLine(string markup)
        {
        }

        public void WriteLine()
        {
        }

        public string? ReadLine()
        {
            if (ReadLines.Count > 0)
                return ReadLines.Dequeue();

            return string.Empty;
        }

        public bool KeyAvailable => ReadKeys.Count > 0;

        public ConsoleKeyInfo ReadKey()
        {
            if (ReadKeys.Count > 0)
                return ReadKeys.Dequeue();

            return new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);
        }
    }
}
