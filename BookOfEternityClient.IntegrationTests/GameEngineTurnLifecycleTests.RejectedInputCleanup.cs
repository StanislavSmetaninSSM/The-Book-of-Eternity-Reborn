using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Deletes only the input correlated to a genuinely validated signed original and leaves every other physical image intact.
    /// </summary>
    /// <returns>
    /// A task completing after exact matching deletion and an idempotent second cleanup are verified.
    /// </returns>
    [Fact]
    public async Task RejectedTurnRequestCleanup_RemovesOnlyMatchingSignedInput()
    {
        var fixture = await CreateSpiritualEntryGuardOriginalAsync(absentConflict: false);
        await using var context = fixture.Context;
        var expected = await ReadSpiritualEntryGuardFilesAsync(context);
        Assert.True(expected.Remove(Path.Combine("input", "turn_request.json")));

        await InvokePrivateTaskAsync(fixture.Engine, "CleanupCorrelatedRejectedTurnRequestAsync", fixture.Snapshot);
        await InvokePrivateTaskAsync(fixture.Engine, "CleanupCorrelatedRejectedTurnRequestAsync", fixture.Snapshot);

        Assert.False(context.FileSystem.FileExists("input/turn_request.json"));
        await AssertSpiritualEntryGuardFilesAsync(context, expected);
    }

    /// <summary>
    /// Preserves foreign, newer, malformed or ambiguous input and refuses deletion without authenticated snapshot context.
    /// </summary>
    /// <param name="variant">
    /// One independently changed identity field, malformed packet, duplicate identity key, missing input, or absent context.
    /// </param>
    /// <returns>
    /// A task completing after the full physical inventory and bytes remain unchanged.
    /// </returns>
    [Theory]
    [InlineData("session")]
    [InlineData("request")]
    [InlineData("turn")]
    [InlineData("invalid_json")]
    [InlineData("wrong_turn_type")]
    [InlineData("duplicate_session")]
    [InlineData("duplicate_request")]
    [InlineData("duplicate_turn")]
    [InlineData("null_context")]
    [InlineData("missing_input")]
    public async Task RejectedTurnRequestCleanup_PreservesUncorrelatedOrUnusableInput(string variant)
    {
        var fixture = await CreateSpiritualEntryGuardOriginalAsync(absentConflict: false);
        await using var context = fixture.Context;
        const string path = "input/turn_request.json";
        var originalBytes = Assert.IsType<byte[]>(await context.FileSystem.ReadFileBytesAsync(path));
        var original = Encoding.UTF8.GetString(originalBytes).TrimStart('\uFEFF');
        var packet = Assert.IsType<JsonObject>(JsonNode.Parse(original));
        string? replacement = null;
        switch (variant)
        {
            case "session":
                packet["sessionId"] = "foreign_session";
                replacement = packet.ToJsonString();
                break;
            case "request":
                packet["requestId"] = "newer_request";
                replacement = packet.ToJsonString();
                break;
            case "turn":
                packet["turnNumber"] = 43;
                replacement = packet.ToJsonString();
                break;
            case "invalid_json":
                replacement = "{\"sessionId\":";
                break;
            case "wrong_turn_type":
                packet["turnNumber"] = "42";
                replacement = packet.ToJsonString();
                break;
            // The final duplicate retains the matching value: a last-property-wins parser would incorrectly authorize deletion.
            case "duplicate_session":
                replacement = "{\"sessionId\":\"foreign_session\"," + original.TrimStart()[1..];
                break;
            case "duplicate_request":
                replacement = "{\"requestId\":\"foreign_request\"," + original.TrimStart()[1..];
                break;
            case "duplicate_turn":
                replacement = "{\"turnNumber\":43," + original.TrimStart()[1..];
                break;
            case "missing_input":
                context.FileSystem.DeleteFile(path);
                break;
            case "null_context":
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(variant), variant, "Unknown cleanup control.");
        }
        if (replacement is not null)
            await context.WriteExactJsonAsync(path, replacement);
        var expected = await ReadSpiritualEntryGuardFilesAsync(context);

        await InvokePrivateTaskAsync(fixture.Engine, "CleanupCorrelatedRejectedTurnRequestAsync",
            new object?[] { variant == "null_context" ? null : fixture.Snapshot });

        await AssertSpiritualEntryGuardFilesAsync(context, expected);
    }
}
