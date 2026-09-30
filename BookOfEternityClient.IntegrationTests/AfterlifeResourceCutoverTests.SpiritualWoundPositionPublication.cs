using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Publishes two genuinely signed exchanges through C2 and common C4, inserting an actual position wound between them.
    /// </summary>
    /// <param name="context">
    /// Full original fixture signed with dice fifteen, five, thirteen and eight before either exchange was authored.
    /// </param>
    /// <returns>
    /// The real successful common-publication result, including its comparison-only causal position packet.
    /// This fixture does not simulate warm GameEngine finalization.
    /// </returns>
    internal static async Task<AcceptedTurnCanonicalStateRefresh.Result> PublishSpiritualPositionOriginalAsync(
        ResourceMaterializationTestContext context)
    {
        const string scene = "Чужое давление надломило волю хранителя.";
        await WritePositionBurdenExchangesAsync(context, playerWound: false, modifier: "exact");
        await context.WriteExactJsonAsync(ProjectionNarrativePath, new JsonObject { ["response"] = scene }.ToJsonString());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
            AssertNoConflictFrameErrors(recorded.Issues);
            using (var warm = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture))
            {
                AssertNoConflictFrameErrors(await warm.BeginResourceExecutionAsync(lease));
                var step = await warm.AdvanceNextResourceExchangeAsync(lease);
                AssertNoConflictFrameErrors(step.Issues);
                var committed = await warm.CommitC2FirstTransportAsync(lease, step.Step!.Interval!);
                Assert.Equal("committed", committed.Disposition);
                AssertNoConflictFrameErrors(committed.Issues);
            }
            var opened = await context.Validator.OpenC2PrivateSessionAsync(lease);
            AssertNoConflictFrameErrors(opened.Issues);
            Assert.Equal("offer", opened.Disposition);
            using var first = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
            var decision = JsonNode.Parse(OriginalSpiritualWoundDecision(first.Offer!.OpportunityRef,
                "spiritual_position_burden", "pressure").GetRawText())!;
            decision["proposal"]!["consequenceDefinitions"]![0]!["definition"]!["components"]![0]!["payload"]!["magnitude"] = 1;
            var advanced = await first.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(decision), scene);
            Assert.True(advanced.Disposition == "offer", FormatC2SubmissionIssues(advanced));
            using var second = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(advanced.Session);
            Assert.NotEqual(first.Offer!.OpportunityRef, second.Offer!.OpportunityRef);
            var completed = await second.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(new
            {
                opportunityRef = second.Offer.OpportunityRef, decision = "none"
            }), null);
            Assert.True(completed.Disposition == "completed_unpublished", FormatC2SubmissionIssues(completed));
            using var terminal = completed.Session;
        }
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, await ReadSpiritualC4BackupsAsync(context));
        AssertNoConflictFrameErrors(published.Issues);
        Assert.NotNull(published.MechanicsPlan);
        Assert.NotNull(published.SpiritualConflictValidation);
        Assert.Single(Assert.IsType<SpiritualWoundPublishedOutput>(published.SpiritualWoundOutput).Notifications);
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var log = root["activeConflict"]!["exchangeLog"]!.AsArray();
        Assert.Equal(2, log.Count);
        Assert.Empty(log[0]!["diceAudit"]!["modifierBreakdown"]!["player"]!.AsArray());
        Assert.Equal("player_advantaged", log[1]!["diceAudit"]!["modifierBreakdown"]!["player"]![0]!["position"]!.GetValue<string>());
        foreach (var exchange in log)
        {
            Assert.Equal("contested", exchange!["before"]!["conflictPosition"]!.GetValue<string>());
            Assert.Equal("contested", exchange["after"]!["conflictPosition"]!.GetValue<string>());
        }
        var receipts = Assert.IsType<JsonObject>(await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath));
        Assert.Equal(2, receipts["decisions"]!.AsArray().Count);
        Assert.Single(receipts["decisions"]!.AsArray(), value => value!["decision"]!.GetValue<string>() == "materialize");
        return published;
    }
}
