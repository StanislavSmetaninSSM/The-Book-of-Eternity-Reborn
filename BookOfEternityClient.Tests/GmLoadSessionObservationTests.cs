using System.Text.Json;
using BookOfEternityClient.Services.GmRuntime;
using Xunit;

namespace BookOfEternityClient.Tests;

#if DEBUG
/// <summary>Diagnostic DATA controls only; synthetic records do not qualify a native Load owner transition.</summary>
public sealed class GmLoadSessionObservationTests
{
    private const string Nonce = "1234567890abcdef1234567890abcdef";

    [Theory]
    [InlineData(GmLoadMainState.Running)]
    [InlineData(GmLoadMainState.StartedNotReady)]
    public void CompletedPairPreservesEveryFieldAndRestartWarning(GmLoadMainState state)
    {
        var value = Sample(state);
        using var output = new StringWriter();
        GmLoadSessionObservationWriter.Write(Nonce, output, value);
        Assert.False(string.IsNullOrWhiteSpace(output.ToString()), "Completed Load observation is missing.");
        using var json = JsonDocument.Parse(output.ToString());
        var root = json.RootElement;
        Assert.Equal("boe-load-session-completed", root.GetProperty("kind").GetString());
        Assert.Equal(Nonce, root.GetProperty("nonce").GetString());
        var roundTrip = JsonSerializer.Deserialize<GmLoadSessionObservation>(root.GetProperty("observation").GetRawText());
        Assert.Equal(value, roundTrip);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-nonce")]
    [InlineData("1234567890ABCDEF1234567890ABCDEF")]
    public void MissingOrInvalidNonceProducesNoWitness(string? nonce)
    {
        using var output = new StringWriter();
        GmLoadSessionObservationWriter.Write(nonce, output, Sample());
        Assert.Equal("", output.ToString());
    }

    [Theory]
    [InlineData("missing-stop")]
    [InlineData("missing-finish")]
    [InlineData("uncertain")]
    [InlineData("not-committed")]
    [InlineData("not-refreshed")]
    public void IncompleteOutcomeProducesNoWitness(string failure)
    {
        var value = Sample();
        value = failure switch
        {
            "missing-stop" => value with { OriginalStop = null },
            "missing-finish" => value with { ReplacementFinish = null },
            "uncertain" => value with { ReplacementFinish = value.ReplacementFinish! with { State = GmLoadMainState.Uncertain } },
            "not-committed" => value with { Committed = false },
            _ => value with { RefreshConfirmed = false }
        };
        using var output = new StringWriter();
        GmLoadSessionObservationWriter.Write(Nonce, output, value);
        Assert.Equal("", output.ToString());
    }

    [Fact]
    public void ThrowingSinkCannotReplaceLoadOutcome()
        => Assert.Null(Record.Exception(() => GmLoadSessionObservationWriter.Write(Nonce, new ThrowingWriter(), Sample())));

    /// <summary>Builds inert formatting inputs, not original process authority or a fabricated operation success.</summary>
    private static GmLoadSessionObservation Sample(GmLoadMainState state = GmLoadMainState.Running)
    {
        var original = new GmSessionRunIdentity("/owned-root", "original-run", "generation-a", 3,
            GmSessionRunBackend.LinuxSupervisor, "host-a", "boot-a");
        var replacement = original with { RunId = "replacement-run", GenerationId = "generation-b", Epoch = 4 };
        return new("operation", "save-selection", original.RootKey, original.GenerationId,
            new(true, "operation", GmLoadMainState.Stopped, original, new(original.RunId, "LinuxSupervisor", "owned", 101)),
            new(true, "operation", state, replacement, new(replacement.RunId, "LinuxSupervisor", "owned", 102),
                state == GmLoadMainState.StartedNotReady ? "restart warning" : null), true, true, replacement.GenerationId);
    }

    private sealed class ThrowingWriter : StringWriter
    {
        public override void WriteLine(string? value) => throw new IOException("diagnostic sink refused");
    }
}
#endif
