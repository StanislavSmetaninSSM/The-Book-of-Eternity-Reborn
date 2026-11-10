using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Spectre.Console;
using Spectre.Console.Rendering;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Real Spectre confirmation parsing at the wound UI boundary, without game or filesystem setup.</summary>
public sealed class WoundTreatmentConfirmationTextTests
{
    /// <summary>Legal authored terms are literal text, including text that resembles valid or invalid markup.</summary>
    [Theory]
    [InlineData("balanced", false)] [InlineData("balanced", true)]
    [InlineData("unknown-style", false)] [InlineData("unknown-style", true)]
    [InlineData("open-bracket", false)] [InlineData("open-bracket", true)]
    [InlineData("doubled-brackets", false)] [InlineData("doubled-brackets", true)]
    [InlineData("html-unicode", false)] [InlineData("html-unicode", true)]
    [InlineData("ordinary", false)] [InlineData("ordinary", true)]
    public void NegotiatedTermsRemainLiteralThroughActualConfirmation(string sample, bool recording)
    {
        var terms = sample switch
        {
            "balanced" => "Перенести [red]архив[/]",
            "unknown-style" => "Перенести [несуществующий-тег] архив",
            "open-bracket" => "Перенести [архив",
            "doubled-brackets" => "Перенести [[архив]]",
            "html-unicode" => "Перенести <b>архив</b> — ёж 🦔",
            _ => "Перенести архив и вернуть книгу"
        };
        var selection = Selection(terms);
        var before = selection.ToJson().ToJsonString();
        Assert.Equal(terms, selection.Aid!.Compensation!.ReadableTerms);
        var plain = WoundApplicationService.ConfirmationFor(selection);
        Assert.Contains(terms, plain);
        using var console = new ConfirmationConsole("default");
        var recorded = recording ? new AgentConsoleRecordingExplorerConsole(console) : null;
        IExplorerConsole target = recorded is null ? console : recorded;
        var answer = true;
        var error = Record.Exception(() => answer = ExplorerMode.ConfirmWoundTreatment(target, selection));
        Assert.Equal(before, selection.ToJson().ToJsonString());
        Assert.False(selection.Aid.PlayerConfirmed);
        Assert.Equal(1, console.ConfirmationCalls);
        Assert.Null(error);
        Assert.False(answer);
        Assert.Equal(plain, Markup.Remove(console.LastPrompt!));
        Assert.Contains(plain, console.Output.ToString());
        if (recorded is not null) Assert.Equal(plain, recorded.ReadCapturedText());
        Assert.Equal(0, console.RemainingKeys);
    }

    /// <summary>Escaping presentation does not change explicit yes/no or the original default cancellation.</summary>
    [Theory]
    [InlineData("yes", true)]
    [InlineData("no", false)]
    [InlineData("default", false)]
    public void ConfirmationReturnsOnlyThePlayersAnswer(string input, bool expected)
    {
        var selection = Selection("Вернуть книгу");
        var before = selection.ToJson().ToJsonString();
        using var console = new ConfirmationConsole(input);
        Assert.Equal(expected, ExplorerMode.ConfirmWoundTreatment(console, selection));
        Assert.Equal(1, console.ConfirmationCalls);
        Assert.Equal(0, console.RemainingKeys);
        Assert.Equal(before, selection.ToJson().ToJsonString());
        Assert.False(selection.Aid!.PlayerConfirmed);
    }

    /// <summary>The existing non-negotiated confirmation keeps its plain text and default answer.</summary>
    [Fact]
    public void SelfTreatmentConfirmationRemainsUnchanged()
    {
        var selection = new SpiritualSelfTreatmentSelection("wound_selected", "sha256:" + new string('a', 64));
        using var console = new ConfirmationConsole("default");
        Assert.False(ExplorerMode.ConfirmWoundTreatment(console, selection));
        Assert.Contains(WoundApplicationService.ConfirmationFor(selection), console.Output.ToString());
        Assert.Null(selection.Aid);
        Assert.Equal(0, console.RemainingKeys);
    }

    private static SpiritualSelfTreatmentSelection Selection(string terms)
    {
        var parsedTerms = SpiritualTreatmentParticipation.ReadCompensation(new JsonObject
        {
            ["compensation"] = new JsonObject
            {
                ["kind"] = "favor", ["agreementRef"] = "agreement_selected", ["readableTerms"] = terms
            }
        });
        var selection = new SpiritualSelfTreatmentSelection("wound_selected", "sha256:" + new string('a', 64))
        {
            Aid = new("guardian_selected", "player_soul", "sha256:" + new string('b', 64))
            { ReceiveAid = true, Compensation = parsedTerms }
        };
        var restored = SpiritualSelfTreatmentSelection.Parse(selection.ToJson());
        Assert.Equal(selection, restored);
        return restored;
    }

    private sealed class ConfirmationConsole : IExplorerConsole, IDisposable
    {
        private readonly PromptConsole _renderer;
        private readonly KeyInput _input;
        internal StringWriter Output { get; } = new();
        internal int ConfirmationCalls { get; private set; }
        internal string? LastPrompt { get; private set; }
        internal int RemainingKeys => _input.Remaining;
        internal ConfirmationConsole(string answer)
        {
            _input = new(answer);
            var console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors,
                Interactive = InteractionSupport.Yes, Out = new AnsiConsoleOutput(Output)
            });
            console.Profile.Width = 4096;
            _renderer = new(console, _input);
        }
        public bool Confirm(string prompt, bool defaultValue = false)
        {
            ConfirmationCalls++;
            LastPrompt = prompt;
            return _renderer.Confirm(prompt, defaultValue);
        }
        public void Dispose() => Output.Dispose();
        public void Clear() => throw new NotSupportedException();
        public void Write(IRenderable content) => throw new NotSupportedException();
        public string Ask(string prompt, string defaultValue = "") => throw new NotSupportedException();
        public T Prompt<T>(IPrompt<T> prompt) => throw new NotSupportedException();
        public void Markup(string markup) => throw new NotSupportedException();
        public void MarkupLine(string markup) => throw new NotSupportedException();
        public void WriteLine() => throw new NotSupportedException();
        public string? ReadLine() => throw new NotSupportedException();
        public bool KeyAvailable => throw new NotSupportedException();
        public ConsoleKeyInfo ReadKey() => throw new NotSupportedException();
    }

    private sealed class PromptConsole(IAnsiConsole inner, IAnsiConsoleInput input) : IAnsiConsole
    {
        public Profile Profile => inner.Profile;
        public IAnsiConsoleCursor Cursor => inner.Cursor;
        public IAnsiConsoleInput Input => input;
        public IExclusivityMode ExclusivityMode => inner.ExclusivityMode;
        public RenderPipeline Pipeline => inner.Pipeline;
        public void Clear(bool home) => inner.Clear(home);
        public void Write(IRenderable renderable) => inner.Write(renderable);
    }

    private sealed class KeyInput : IAnsiConsoleInput
    {
        private readonly Queue<ConsoleKeyInfo> _keys = new();
        internal int Remaining => _keys.Count;
        internal KeyInput(string answer)
        {
            if (answer == "yes") _keys.Enqueue(new('y', ConsoleKey.Y, false, false, false));
            if (answer == "no") _keys.Enqueue(new('n', ConsoleKey.N, false, false, false));
            _keys.Enqueue(new('\r', ConsoleKey.Enter, false, false, false));
        }
        public bool IsKeyAvailable() => _keys.Count != 0;
        public ConsoleKeyInfo? ReadKey(bool intercept) => _keys.Dequeue();
        public Task<ConsoleKeyInfo?> ReadKeyAsync(bool intercept, CancellationToken cancellationToken) => Task.FromResult(ReadKey(intercept));
    }
}
