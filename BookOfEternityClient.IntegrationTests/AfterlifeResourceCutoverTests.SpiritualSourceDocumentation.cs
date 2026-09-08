using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    [Fact]
    public async Task SpiritualSourceAction_DocumentedTerminalWitnessIsSourceLocalPendingOnly()
    {
        var examples = ExampleDocumentationValidationTests.SpiritualSourceActionExamples();
        Assert.Equal(2, examples.Count);
        var resolution = examples[1].DeepClone().AsObject();
        var terminalExchange = Assert.IsType<JsonObject>(resolution["terminalExchange"]);
        Assert.True(JsonNode.DeepEquals(
            terminalExchange["diceAudit"], resolution["diceAudit"]));
        Assert.Equal(new[]
        {
            "actionCostAudit", "after", "before", "diceAudit", "exchangeId",
            "matchupAudit", "operationType", "outcome", "turnNumber"
        }, terminalExchange.Select(static property => property.Key)
            .OrderBy(static property => property, StringComparer.Ordinal));

        await using var context = await CreateCompleteConflictFrameContextAsync();
        var candidate = Assert.IsType<JsonObject>(
            await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        candidate["activeConflict"] = null;
        candidate["recentConflicts"] = new JsonArray(resolution);
        await context.WriteExactJsonAsync(
            AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var before = await context.FileSystem.ReadFileBytesAsync(
            lease, AfterlifeSpiritualConflictState.StatePath);

        var prepared = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);

        AssertNoConflictFrameErrors(prepared.Issues);
        var session = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(prepared.Session);
        var source = Assert.Single(session.Sources);
        Assert.True(session.Owns(source));
        Assert.Equal("player_soul:player_soul", source.ActingActor);
        Assert.Equal("guardian:guardian_frame", source.AffectedActor);
        Assert.Equal("opposition", source.AffectedSide);
        Assert.Equal(new[] { 0, 1 }, session.ClaimedDice);
        var pending = Assert.Single(session.PendingRequirements);
        Assert.Equal(ValidationService.SpiritualSourceRequirement.TerminalClosure, pending.Kind);
        Assert.NotEmpty(pending.CandidateJson);
        Assert.True(session.HasWork);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        Assert.Equal(before, await context.FileSystem.ReadFileBytesAsync(
            lease, AfterlifeSpiritualConflictState.StatePath));
    }
}
