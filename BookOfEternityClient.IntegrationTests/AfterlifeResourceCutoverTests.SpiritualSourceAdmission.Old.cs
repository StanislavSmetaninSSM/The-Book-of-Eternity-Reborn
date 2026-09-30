using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    // This compiles against the pre-B source. The OLD ordinary control is the
    // existing ConflictFrame_SignedPressureExchangePublishesWithNoOwningPhaseErrors.
    [Fact]
    public async Task SourceOwner_RedRawRejectsOriginalOutOfRangeResilience()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
        var guardian = profiles["profiles"]!.AsArray().OfType<JsonObject>()
            .Single(profile => profile["actorId"]!.GetValue<string>() == "guardian_frame");
        guardian["standardArts"]!["spiritual_resilience"] = 6;
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 42, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 12, 8]);
        await WriteCompleteConflictFrameExchangeAsync(context);

        var issues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(issues, issue => issue.Code == "spiritual_source_input_invalid" &&
            issue.Severity == IssueSeverity.Error);
    }
}
