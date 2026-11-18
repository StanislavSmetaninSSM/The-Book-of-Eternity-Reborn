using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

#if DEBUG
public sealed class GameLoopRejectionDiagnosticTests
{
    private const string Nonce = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void ProtectedRefusalEmitsOriginalIssuesAndAllComputedFlagsWithoutTruncation()
    {
        var message = string.Concat(Enumerable.Repeat("Причина 世界🧭", 5000));
        var issue = new ValidationIssue("game_state/example.json", IssueSeverity.Error, message,
            code: "diagnostic_example", actor: "actor-original", section: "section-original",
            expected: "expected-original", actual: "actual-original", repairHint: "hint-original");
        using var sink = new StringWriter();
        GameEngine.EmitProtectedMaterializationFixtureRejection(new[] { issue },
            true, false, true, false, true, "original-source", 3, true, Nonce, sink);
        using var parsed = JsonDocument.Parse(sink.ToString());
        var root = parsed.RootElement;
        AssertEnvelope(root, "protected-materialization");
        Assert.Equal("original-source", root.GetProperty("source").GetString());
        Assert.Equal(3, root.GetProperty("attempt").GetInt32());
        Assert.True(root.GetProperty("rollbackAvailable").GetBoolean());
        var flags = root.GetProperty("flags");
        Assert.True(flags.GetProperty("mortalItem").GetBoolean());
        Assert.False(flags.GetProperty("mortalLocation").GetBoolean());
        Assert.True(flags.GetProperty("effect").GetBoolean());
        Assert.False(flags.GetProperty("resource").GetBoolean());
        Assert.True(flags.GetProperty("wound").GetBoolean());
        var error = Assert.Single(root.GetProperty("errors").EnumerateArray());
        Assert.Equal(message, error.GetProperty("message").GetString());
        Assert.Equal(issue.Code, error.GetProperty("code").GetString());
        Assert.Equal(issue.FilePath, error.GetProperty("filePath").GetString());
        Assert.Equal(issue.Actor, error.GetProperty("actor").GetString());
        Assert.Equal(issue.Section, error.GetProperty("section").GetString());
        Assert.Equal(issue.Expected, error.GetProperty("expected").GetString());
        Assert.Equal(issue.Actual, error.GetProperty("actual").GetString());
        Assert.Equal(issue.RepairHint, error.GetProperty("repairHint").GetString());
        Assert.Equal(issue.Severity.ToString(), error.GetProperty("severity").GetString());
        Assert.Equal(issue.Category.ToString(), error.GetProperty("category").GetString());
    }

    [Fact]
    public void CanonicalRefreshEmitsActualExceptionWithoutTruncation()
    {
        var failure = new InvalidDataException(string.Concat(Enumerable.Repeat("Оригинал 世界🧭", 5000)),
            new IOException("inner-original"));
        using var sink = new StringWriter();
        GameEngine.EmitCanonicalRefreshFixtureRejection(failure, "refresh-source", false, Nonce, sink);
        using var parsed = JsonDocument.Parse(sink.ToString());
        var root = parsed.RootElement;
        AssertEnvelope(root, "canonical-refresh-failed");
        Assert.Equal("refresh-source", root.GetProperty("source").GetString());
        Assert.False(root.GetProperty("rollbackAvailable").GetBoolean());
        Assert.Equal(failure.GetType().FullName, root.GetProperty("exceptionType").GetString());
        Assert.Equal(failure.ToString(), root.GetProperty("exception").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-nonce")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF")]
    public void MissingOrInvalidNonceEmitsNothing(string? nonce)
    {
        using var sink = new StringWriter();
        GameEngine.EmitProtectedMaterializationFixtureRejection([], true, false, false, false, false,
            "source", 1, true, nonce, sink);
        GameEngine.EmitCanonicalRefreshFixtureRejection(new HostileException(), "source", true, nonce, sink);
        Assert.Equal(string.Empty, sink.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiagnosticSinkFailureCannotReplaceOriginalOutcome(bool canonical)
    {
        using var sink = new ThrowingWriter();
        var original = new InvalidDataException("original");
        var failure = Record.Exception(() =>
        {
            if (canonical) GameEngine.EmitCanonicalRefreshFixtureRejection(original, "source", true, Nonce, sink);
            else GameEngine.EmitProtectedMaterializationFixtureRejection([], true, false, false, false, false,
                "source", 1, true, Nonce, sink);
        });
        Assert.Null(failure);
        Assert.Equal("original", original.Message);
    }

    [Fact]
    public void HostileExceptionDiagnosticGetterCannotEscape()
    {
        using var sink = new StringWriter();
        Assert.Null(Record.Exception(() => GameEngine.EmitCanonicalRefreshFixtureRejection(
            new HostileException(), "source", true, Nonce, sink)));
    }

    [Fact]
    public void DebugCallsitesObserveExistingRefusalBeforeBookkeeping()
    {
        var directory = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine");
        var materialization = File.ReadAllText(Path.Combine(directory, "GameEngine.ValidationAndRepair.Part06.cs"));
        var canonical = File.ReadAllText(Path.Combine(directory, "GameEngine.ValidationAndRepair.Part04.cs"));
        AssertOrdered(materialization, "if (requiresMortalItemFailClosed ||", "EmitProtectedMaterializationFixtureRejection(",
            "Materialization repair after {Source}");
        Assert.Contains("EmitProtectedMaterializationFixtureRejection(errors, requiresMortalItemFailClosed, requiresMortalLocationFailClosed, requiresEffectFailClosed, requiresResourceFailClosed, requiresWoundFailClosed, source, attempt, rollbackAvailable, Environment.GetEnvironmentVariable(\"BOE_TEST_GAME_LOOP_FAILURE_NONCE\"), Console.Error);", NormalizeWhitespace(materialization));
        Assert.Contains("EmitCanonicalRefreshFixtureRejection(exception, source, HasRollbackCapability(rollbackSnapshot), Environment.GetEnvironmentVariable(\"BOE_TEST_GAME_LOOP_FAILURE_NONCE\"), Console.Error);", NormalizeWhitespace(canonical));
        AssertOrdered(canonical, "private async Task FailClosedAcceptedTurnCanonicalRefreshAsync(",
            "EmitCanonicalRefreshFixtureRejection(", "var sessionGeneration = await CaptureCurrentSessionGenerationAsync();");
        foreach (var pair in new[] { (materialization, "EmitProtectedMaterializationFixtureRejection("),
                     (canonical, "EmitCanonicalRefreshFixtureRejection(") })
        {
            var at = pair.Item1.IndexOf(pair.Item2, StringComparison.Ordinal);
            var start = pair.Item1.LastIndexOf("#if DEBUG", at, StringComparison.Ordinal);
            Assert.True(start >= 0);
            var end = pair.Item1.IndexOf("#endif", start, StringComparison.Ordinal);
            Assert.True(end > at, "The invocation must precede the FIRST #endif of its DEBUG block.");
            var guarded = pair.Item1[start..end];
            Assert.Contains("BOE_TEST_GAME_LOOP_FAILURE_NONCE", guarded);
            Assert.Contains("catch", guarded);
        }
        // Source placement is not execution proof of an actual rejected turn.
    }

    private static string NormalizeWhitespace(string text) =>
        string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static void AssertEnvelope(JsonElement root, string stage)
    {
        Assert.Equal("boe-game-loop-fixture-rejection", root.GetProperty("kind").GetString());
        Assert.Equal(stage, root.GetProperty("stage").GetString());
        Assert.Equal(Nonce, root.GetProperty("nonce").GetString());
        Assert.Equal(Environment.ProcessId, root.GetProperty("pid").GetInt32());
    }

    private static void AssertOrdered(string text, string first, string second, string third)
    {
        var a = text.IndexOf(first, StringComparison.Ordinal);
        var b = a < 0 ? -1 : text.IndexOf(second, a, StringComparison.Ordinal);
        var c = b < 0 ? -1 : text.IndexOf(third, b, StringComparison.Ordinal);
        Assert.True(a >= 0 && b > a && c > b);
    }

    private sealed class HostileException : Exception
    {
        public override string Message => throw new InvalidOperationException("hostile message getter");
        public override string ToString() => throw new InvalidOperationException("hostile exception rendering");
    }

    private sealed class ThrowingWriter : StringWriter
    {
        public override void WriteLine(string? value) => throw new IOException("diagnostic sink unavailable");
    }
}

#endif
