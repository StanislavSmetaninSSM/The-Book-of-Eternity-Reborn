using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Applies a real one-use position wound to one later action, then preserves that closed rank after the root expires.
    /// </summary>
    /// <returns>
    /// A task completing after three genuine chronological admissions retain ranks zero, minus one and zero without canonical erosion.
    /// </returns>
    [Fact]
    public async Task OriginalSpiritualGeneration_PositionExpiryPreservesClosedRankWithoutLaterErosion()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            signedDice: [5, 15, 13, 8, 12, 8], seedOriginalInputs: async original =>
            {
                var profiles = Assert.IsType<JsonObject>(await original.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
                foreach (var profile in profiles["profiles"]!.AsArray())
                    profile!["standardArts"]!["pressure"] = 1;
                await original.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
                var soul = Assert.IsType<JsonObject>(await original.ReadJsonAsync("game_state/meta/soul_state.json"));
                soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]!["pressure"] = 1;
                await original.WriteExactJsonAsync("game_state/meta/soul_state.json", soul.ToJsonString());
                var root = Assert.IsType<JsonObject>(await original.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
                root["activeConflict"]!["oppositionSide"]!["leadContestant"]!["actorArtTierSnapshot"]!["pressure"] = 1;
                await original.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
            });
        await WritePositionBurdenExchangesAsync(context, playerWound: true, modifier: "exact");
        var candidate = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = candidate["activeConflict"]!.AsObject();
        var log = active["exchangeLog"]!.AsArray();
        var third = log[1]!.DeepClone();
        third["exchangeId"] = "position_after_expiry";
        third["before"] = log[1]!["after"]!.DeepClone();
        third["after"]!["oppositionSideStrain"] = "fractured";
        var dice = third["diceAudit"]!;
        dice["diceUsed"]![0]!["sourceIndex"] = 4;
        dice["diceUsed"]![1]!["sourceIndex"] = 5;
        dice["diceUsed"]![0]!["value"] = 12;
        dice["modifierBreakdown"]!["opposition"] = new JsonArray();
        dice["playerTotal"] = 12;
        dice["oppositionTotal"] = 8;
        dice["margin"] = 4;
        log.Add(third);
        active["oppositionSideStrain"] = "fractured";
        for (var ordinal = 0; ordinal < log.Count; ordinal++)
            foreach (var side in new[] { "player", "opposition" })
            {
                var cost = log[ordinal]!["actionCostAudit"]![side]!;
                cost["artTier"] = 1;
                cost["effectiveCost"] = 2;
                cost["before"] = 6 - ordinal * 2;
                cost["after"] = 4 - ordinal * 2;
            }
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var canonical = await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath);
        var resourceBytes = await context.FileSystem.ReadFileBytesAsync(lease, ResourceMaterializationContract.StatePath);
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(captured.Capture);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var first = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(first.Issues);
        var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
        var firstSource = Assert.Single(source.Sources);
        var admitted = await capture.AdmitWoundSourceAsync(lease, first.Step!.Interval!, firstSource);
        AssertNoConflictFrameErrors(admitted.Issues);
        var offered = await capture.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(offered.Issues);
        var decision = JsonNode.Parse(OriginalSpiritualWoundDecision(offered.Opportunity!.PublicRef,
            "spiritual_position_burden", "pressure", "player").GetRawText())!;
        var definition = decision["proposal"]!["consequenceDefinitions"]![0]!["definition"]!;
        definition["components"]![0]!["payload"]!["magnitude"] = 1;
        definition["triggers"] = new JsonArray(new JsonObject
        {
            ["triggerId"] = "position_last_payment", ["eventType"] = "resource_spent", ["priority"] = 100,
            ["componentIds"] = new JsonArray("component_001"), ["consumeUses"] = true,
            ["resolutionMode"] = "deterministic"
        });
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "uses", ["initialUses"] = 1,
            ["consumingEventTypes"] = new JsonArray("resource_spent")
        };
        var inserted = await capture.MaterializeWoundAsync(lease, admitted.Admission!,
            JsonSerializer.SerializeToElement(decision), "Чужое давление надломило волю души.");
        AssertNoConflictFrameErrors(inserted.Issues);
        var mechanicsIssues = new List<ValidationIssue>();
        var beforeSecond = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext>(
            capture.PrepareSourceMechanics(source, mechanicsIssues));
        AssertNoConflictFrameErrors(mechanicsIssues);
        Assert.Single(beforeSecond.Contributions, row => row.Profile == "spiritual_position_burden" &&
            row.Actor.TargetId == "player_soul" && row.Operation == "pressure");
        Assert.Equal(-1, ValidationService.SpiritualWoundSourceSession.ReadCurrentEffectivePosition(
            beforeSecond, active, log[1]!.AsObject()));
        var second = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(second.Issues);
        Assert.Equal(1, second.Step!.Interval!.Ordinal);
        var closed = source.ReadAdmittedExchangeMechanics(capture);
        Assert.Equal(new int?[] { 0, -1 }, closed.Select(row => row.EffectivePositionRank));
        var beforeThird = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext>(
            capture.PrepareSourceMechanics(source, mechanicsIssues));
        AssertNoConflictFrameErrors(mechanicsIssues);
        Assert.DoesNotContain(beforeThird.Contributions, row => row.Profile == "spiritual_position_burden");
        Assert.Equal(0, ValidationService.SpiritualWoundSourceSession.ReadCurrentEffectivePosition(
            beforeThird, active, log[2]!.AsObject()));
        var last = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(last.Issues);
        Assert.Equal(2, last.Step!.Interval!.Ordinal);
        var final = source.ReadAdmittedExchangeMechanics(capture);
        Assert.Equal(new int?[] { 0, -1, 0 }, final.Select(row => row.EffectivePositionRank));
        Assert.Equal(closed[1], final[1]);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, source.ClaimedDice);
        Assert.Same(firstSource, source.Sources[0]);
        Assert.Equal(canonical, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath));
        Assert.Equal(resourceBytes, await context.FileSystem.ReadFileBytesAsync(lease, ResourceMaterializationContract.StatePath));
    }
}
