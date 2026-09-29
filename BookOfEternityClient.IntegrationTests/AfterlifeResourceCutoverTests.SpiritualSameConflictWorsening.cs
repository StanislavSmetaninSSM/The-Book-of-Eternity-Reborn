using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Offers only decline when the existing conflict wound has reached an optional source's maximum.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2_CappedSameSideSourceAllowsOnlyNone()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync,
            signedDice: [15, 5, 15, 5]);
        await CommitInitialC2PairAsync(context, conflict =>
        {
            var active = conflict["activeConflict"]!.AsObject();
            var first = active["exchangeLog"]![0]!.AsObject();
            var second = first.DeepClone().AsObject();
            second["exchangeId"] = "exchange_capped_same_side";
            second["before"] = first["after"]!.DeepClone();
            second["after"] = second["before"]!.DeepClone();
            second["after"]!["oppositionSideStrain"] = "fractured";
            second["diceAudit"]!["diceUsed"]![0]!["sourceIndex"] = 2;
            second["diceAudit"]!["diceUsed"]![1]!["sourceIndex"] = 3;
            foreach (var side in new[] { "player", "opposition" })
            {
                second["actionCostAudit"]![side]!["before"] = 3;
                second["actionCostAudit"]![side]!["after"] = 0;
            }
            active["exchangeLog"]!.AsArray().Add(second);
            active["oppositionSideStrain"] = "fractured";
        });
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var validator = new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance);
        var opened = await validator.OpenC2PrivateSessionAsync(lease);
        Assert.Equal("offer", opened.Disposition);
        using var firstOffer = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
        var afterCreate = await firstOffer.SubmitDecisionAsync(lease,
            OriginalSpiritualWoundDecision(firstOffer.Offer!.OpportunityRef,
                "spiritual_action_cost_burden", "guard"),
            "Чужое давление надломило волю хранителя.");
        Assert.True(afterCreate.Disposition == "offer", afterCreate.Disposition + ": " +
            string.Join(Environment.NewLine,
                afterCreate.Issues.Select(issue => $"{issue.Code}: {issue}")));
        using var capped = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(afterCreate.Session);
        Assert.Equal(1, capped.Offer!.MaximumSeverityRank);
        Assert.Equal(2, capped.Offer.MinimumSeverityRank);
        Assert.Equal(new[] { "none" }, capped.Offer.AllowedDecisions);
        var forbidden = await capped.SubmitDecisionAsync(lease,
            OriginalSpiritualWoundDecision(capped.Offer.OpportunityRef,
                "spiritual_action_cost_burden", "guard"),
            "Чужое давление надломило волю хранителя.");
        Assert.Equal("blocked", forbidden.Disposition);
        Assert.Contains(forbidden.Issues, issue =>
            issue.Code is "wound_worsening_severity_not_higher" or
                "wound_severity_above_opportunity");
        var completed = await capped.SubmitDecisionAsync(lease,
            JsonSerializer.SerializeToElement(new
            {
                opportunityRef = capped.Offer.OpportunityRef,
                decision = "none"
            }), null);
        Assert.Equal("completed_unpublished", completed.Disposition);
        using var terminal = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(completed.Session);
        var reduced = await terminal.ReduceCompletedDecisionsAsync(lease);
        Assert.True(reduced.Success, string.Join(Environment.NewLine,
            reduced.Issues.Select(issue => $"{issue.Code}: {issue}")));
        Assert.Equal("none", reduced.ReceiptAfterImage!["decisions"]![1]!["decision"]!.GetValue<string>());
        Assert.Null(reduced.ReceiptAfterImage["decisions"]![1]!["selectedSeverityRank"]);
        Assert.Null(reduced.ReceiptAfterImage["decisions"]![1]!["woundId"]);
        Assert.Null(reduced.ReceiptAfterImage["decisions"]![1]!["transitionId"]);
        var ordinary = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(
            reduced.Reduction);
        var proof = Assert.IsType<SpiritualLiveWoundCompletion>(ordinary.LiveWoundCompletion);
        var firstInsertion = Assert.Single(proof.Insertions);
        Assert.Equal(firstInsertion.Wound.WoundId,
            reduced.ReceiptAfterImage["decisions"]![0]!["woundId"]!.GetValue<string>());
        var planned = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
            AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(
                ordinary, ordinary.Resources));
        Assert.Empty(planned.Issues);
        var plan = Assert.IsType<AcceptedMechanicsPlan>(planned.Plan);
        var finalCarriers = WoundCarrierCatalog.Build(new WoundCarrierCatalogInput(
            null, null, null, null,
            plan.WoundCarrierAfterImages[AfterlifeEntityProfileState.StatePath]));
        Assert.Empty(finalCarriers.Issues);
        Assert.True(finalCarriers.TryResolveOne(firstInsertion.Wound.WoundId,
            out var finalOccurrence));
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(firstInsertion.Wound),
            WoundMaterializationContract.SerializeCanonical(finalOccurrence.Wound));
    }

    /// <summary>
    /// Offers the actual first conflict wound as the later source's worsening target.
    /// </summary>
    /// <param name="guaranteed">
    /// Whether the second source requires an exact rank-II worsening despite allowing rank III.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalSpiritualC2_NextSameSideOfferTargetsRegisteredConflictWound(bool guaranteed)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync,
            signedDice: [15, 5, 12, 8]);
        if (guaranteed)
        {
            var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
                AfterlifeEntityProfileState.StatePath));
            profiles["profiles"]![0]!["standardArts"]!["pressure"] = 1;
            var art = SourceOwnerSpecialArt("player_soul", "player_soul");
            art["tier"] = 1;
            art["spiritualWoundEnvelope"] = new JsonObject
            {
                ["schemaVersion"] = 1, ["maximumSeverityRank"] = 3,
                ["guaranteedSeverityRank"] = 2
            };
            profiles["profiles"]![0]!["specialArts"]!.AsArray().Add(art);
            await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath,
                profiles.ToJsonString());
            const string soulPath = "game_state/meta/soul_state.json";
            var soul = Assert.IsType<JsonObject>(await context.ReadJsonAsync(soulPath));
            soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]!["pressure"] = 1;
            await context.WriteExactJsonAsync(soulPath, soul.ToJsonString());
            await context.CaptureValidatedPendingSnapshotAsync(turn: 42,
                currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 12, 8]);
        }
        await CommitInitialC2PairAsync(context, conflict =>
        {
            var active = conflict["activeConflict"]!.AsObject();
            var first = active["exchangeLog"]![0]!.AsObject();
            if (guaranteed)
            {
                var firstPlayerCost = first["actionCostAudit"]!["player"]!;
                firstPlayerCost["artTier"] = 1;
                firstPlayerCost["effectiveCost"] = 2;
                firstPlayerCost["after"] = 4;
            }
            var second = first.DeepClone().AsObject();
            second["exchangeId"] = "exchange_same_side_worsen";
            second["before"] = first["after"]!.DeepClone();
            second["after"] = second["before"]!.DeepClone();
            second["after"]!["oppositionSideStrain"] = "broken";
            second["diceAudit"]!["diceUsed"]![0]!["sourceIndex"] = 2;
            second["diceAudit"]!["diceUsed"]![0]!["value"] = 12;
            second["diceAudit"]!["diceUsed"]![1]!["sourceIndex"] = 3;
            second["diceAudit"]!["diceUsed"]![1]!["value"] = 8;
            second["diceAudit"]!["playerTotal"] = 12;
            second["diceAudit"]!["oppositionTotal"] = 8;
            second["diceAudit"]!["margin"] = 4;
            second["diceAudit"]!["outcomeBand"] = "player_success";
            foreach (var side in new[] { "player", "opposition" })
            {
                second["actionCostAudit"]![side]!["before"] = guaranteed && side == "player" ? 4 : 3;
                second["actionCostAudit"]![side]!["after"] = 0;
            }
            if (guaranteed)
            {
                second["specialArtAudit"] = new JsonObject
                {
                    ["artId"] = "art_source_owner", ["ownerActorType"] = "player_soul",
                    ["ownerActorId"] = "player_soul", ["baseOperation"] = "pressure",
                    ["costMultiplierPercent"] = 200,
                    ["effectNote"] = "Искусство усиливает прежний надлом."
                };
                var secondPlayerCost = second["actionCostAudit"]!["player"]!;
                secondPlayerCost["effectiveCost"] = 4;
                secondPlayerCost["specialArtId"] = "art_source_owner";
                secondPlayerCost["specialCostMultiplierPercent"] = 200;
                secondPlayerCost["standardEffectiveCost"] = 2;
            }
            active["exchangeLog"]!.AsArray().Add(second);
            active["oppositionSideStrain"] = "broken";
        });
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var validator = new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance);
        var opened = await validator.OpenC2PrivateSessionAsync(lease);
        Assert.True(opened.Disposition == "offer", opened.Disposition + ": " +
            string.Join(Environment.NewLine,
                opened.Issues.Select(issue => $"{issue.Code}: {issue}")));
        using var firstOffer = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
        var advanced = await firstOffer.SubmitDecisionAsync(lease,
            OriginalSpiritualWoundDecision(firstOffer.Offer!.OpportunityRef,
                "spiritual_action_cost_burden", "guard"),
            "Чужое давление надломило волю хранителя.",
            async writeLease => await context.FileSystem.WriteFileAtomicBytesAsync(writeLease,
                ProjectionNarrativePath,
                Encoding.UTF8.GetBytes("""
                    {"response":"Первый удар: Чужое давление надломило волю хранителя.","timestamp":"2026-01-01T00:00:00Z"}
                    """)));
        Assert.True(advanced.Disposition == "offer", advanced.Disposition + ": " +
            string.Join(Environment.NewLine,
                advanced.Issues.Select(issue => $"{issue.Code}: {issue}")));
        using var secondOffer = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(advanced.Session);
        Assert.Equal(guaranteed ? 3 : 2, secondOffer.Offer!.MaximumSeverityRank);
        Assert.Equal(2, secondOffer.Offer.MinimumSeverityRank);
        Assert.Equal(guaranteed ? 2 : null, secondOffer.Offer.RequiredSeverityRank);
        Assert.Equal(guaranteed ? new[] { "materialize" } : new[] { "none", "materialize" },
            secondOffer.Offer.AllowedDecisions);
        var worsening = JsonNode.Parse(OriginalSpiritualWoundDecision(
            secondOffer.Offer.OpportunityRef, "spiritual_action_cost_burden", "guard").GetRawText())!;
        worsening["proposal"]!["severity"] = "II";
        var definitions = worsening["proposal"]!["consequenceDefinitions"]!.AsArray();
        var extra = definitions[0]!.DeepClone();
        extra["definitionRef"] = "maneuver_burden";
        extra["definition"]!["definitionKey"] = "maneuver_burden";
        extra["definition"]!["stacking"]!["stackKey"] = "stack_maneuver_burden";
        extra["definition"]!["components"]![0]!["payload"]!["operation"] = "maneuver";
        definitions.Add(extra);
        if (guaranteed)
        {
            var priorCommand = await context.FileSystem.ReadFileBytesAsync(lease,
                AcceptedMechanicsPlan.WoundCommandPath);
            var declined = await secondOffer.SubmitDecisionAsync(lease,
                JsonSerializer.SerializeToElement(new
                {
                    opportunityRef = secondOffer.Offer.OpportunityRef,
                    decision = "none"
                }), null);
            Assert.Equal("blocked", declined.Disposition);
            Assert.Contains(declined.Issues, issue => issue.Code == "wound_guaranteed_result_required");
            foreach (var invalidRank in new[] { "I", "III" })
            {
                var invalid = worsening.DeepClone().AsObject();
                invalid["proposal"]!["severity"] = invalidRank;
                var rejected = await secondOffer.SubmitDecisionAsync(lease,
                    JsonSerializer.SerializeToElement(invalid), null);
                Assert.Equal("blocked", rejected.Disposition);
                Assert.Contains(rejected.Issues, issue =>
                    issue.Code == "wound_guaranteed_severity_mismatch");
            }
            var currentCommand = await context.FileSystem.ReadFileBytesAsync(lease,
                AcceptedMechanicsPlan.WoundCommandPath);
            Assert.Equal(priorCommand, currentCommand);
        }
        var completed = await secondOffer.SubmitDecisionAsync(lease,
            JsonSerializer.SerializeToElement(worsening),
            "Чужое давление надломило волю хранителя.",
            async writeLease => await context.FileSystem.WriteFileAtomicBytesAsync(writeLease,
                ProjectionNarrativePath,
                Encoding.UTF8.GetBytes("""
                    {"response":"Первый и второй удары: Чужое давление надломило волю хранителя.","timestamp":"2026-01-01T00:00:00Z"}
                    """)));
        Assert.True(completed.Disposition == "completed_unpublished", completed.Disposition + ": " +
            string.Join(Environment.NewLine,
                completed.Issues.Select(issue => $"{issue.Code}: {issue}")));
        using var terminal = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(completed.Session);
        var priorCarriers = await context.FileSystem.ReadFileBytesAsync(lease,
            AfterlifeEntityProfileState.StatePath);
        var priorIdentity = await context.FileSystem.ReadFileBytesAsync(lease,
            WoundIdentityState.StatePath);
        var priorHistory = await context.FileSystem.ReadFileBytesAsync(lease,
            WoundHistoryState.HistoryPath);
        var priorReceipt = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundOpportunityReceiptState.StatePath);
        var reduced = await terminal.ReduceCompletedDecisionsAsync(lease);
        Assert.True(reduced.Success, string.Join(Environment.NewLine,
            reduced.Issues.Select(issue => $"{issue.Code}: {issue}")));
        var ordinary = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(
            reduced.Reduction);
        var proof = Assert.IsType<SpiritualLiveWoundCompletion>(ordinary.LiveWoundCompletion);
        var insertions = proof.Insertions;
        Assert.Equal(2, insertions.Count);
        Assert.Equal("create", Assert.Single(insertions[0].Input.Transitions).Kind);
        Assert.Equal("worsen", Assert.Single(insertions[1].Input.Transitions).Kind);
        Assert.Equal(insertions[0].Wound.WoundId, insertions[1].Wound.WoundId);
        Assert.Equal(2, insertions[1].Wound.Severity.Rank);
        var rows = reduced.ReceiptAfterImage!["decisions"]!.AsArray();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal(insertions[0].Wound.WoundId,
            row!["woundId"]!.GetValue<string>()));
        if (guaranteed)
        {
            Assert.Equal(2, rows[1]!["sourceWitness"]!["guaranteedSeverityRank"]!.GetValue<int>());
            Assert.Equal(3, rows[1]!["sourceWitness"]!["maximumSeverityRank"]!.GetValue<int>());
        }
        Assert.Equal(insertions[0].Wound.LastTransition.TransitionId,
            rows[0]!["transitionId"]!.GetValue<string>());
        Assert.Equal(insertions[1].Wound.LastTransition.TransitionId,
            rows[1]!["transitionId"]!.GetValue<string>());
        Assert.NotEqual(rows[0]!["transitionId"]!.GetValue<string>(),
            rows[1]!["transitionId"]!.GetValue<string>());
        var planned = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
            AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(
                ordinary, ordinary.Resources));
        Assert.Empty(planned.Issues);
        var plan = Assert.IsType<AcceptedMechanicsPlan>(planned.Plan);
        var finalCarriers = WoundCarrierCatalog.Build(new WoundCarrierCatalogInput(
            null, null, null, null,
            plan.WoundCarrierAfterImages[AfterlifeEntityProfileState.StatePath]));
        Assert.Empty(finalCarriers.Issues);
        Assert.True(finalCarriers.TryResolveOne(insertions[0].Wound.WoundId,
            out var finalOccurrence));
        Assert.Equal(2, finalOccurrence.Wound.Severity.Rank);
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(insertions[1].Wound),
            WoundMaterializationContract.SerializeCanonical(finalOccurrence.Wound));
        Assert.True(JsonNode.DeepEquals(insertions[1].ReducedState.Identity,
            plan.WoundIdentityAfterImage));
        Assert.True(JsonNode.DeepEquals(insertions[1].ReducedState.History,
            plan.WoundHistoryAfterImage));
        Assert.Equal(priorCarriers, await context.FileSystem.ReadFileBytesAsync(lease,
            AfterlifeEntityProfileState.StatePath));
        Assert.Equal(priorIdentity, await context.FileSystem.ReadFileBytesAsync(lease,
            WoundIdentityState.StatePath));
        Assert.Equal(priorHistory, await context.FileSystem.ReadFileBytesAsync(lease,
            WoundHistoryState.HistoryPath));
        Assert.Equal(priorReceipt, await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundOpportunityReceiptState.StatePath));
    }
}
