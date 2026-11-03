using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Retains actual zero/positive choices and rejects a coherent replacement on the existing owner.</summary>
    /// <param name="margin">GM's actual explicit choice.</param>
    /// <param name="maximum">Expected existing formula ceiling.</param>
    [Theory]
    [InlineData(0L, 0)]
    [InlineData(10L, 1)]
    public Task NoRollSource_StandardChoiceKeepsOwnedOriginal(long margin, int maximum) =>
        CheckNoRollSourceOwnerAsync(margin, maximum, special: false);

    /// <summary>Preserves the original acquired-art identity and actual six-point payment without dice.</summary>
    [Fact]
    public Task NoRollSource_SpecialChoiceKeepsOriginalArtAndCosts() =>
        CheckNoRollSourceOwnerAsync(10, 1, special: true);

    private static async Task CheckNoRollSourceOwnerAsync(long margin, int maximum, bool special)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteVoluntaryIncomingHarmDraftAsync(context, special, margin);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
        AssertNoConflictFrameErrors(result.Issues);
        var owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(result.Session);
        var source = Assert.Single(owner.Sources);
        Assert.True(owner.Owns(source));
        Assert.False(owner.Owns(source with { }));
        Assert.Equal(margin, source.Calculation.Input.HarmfulMargin);
        Assert.Equal(maximum, source.Calculation.MaximumSeverityRank);
        Assert.Equal("guardian:guardian_frame", source.ActingActor);
        Assert.Equal("player_soul:player_soul", source.AffectedActor);
        Assert.Empty(owner.PendingRequirements);
        Assert.Empty(owner.ClaimedDice);
        var exchange = JsonNode.Parse(Assert.Single(owner.CheckedExchanges))!.AsObject();
        Assert.False(exchange.ContainsKey("diceAudit"));
        Assert.Equal(margin, exchange[SpiritualWoundNoRollMargin.Property]!["player"]!.GetValue<long>());
        Assert.Equal(special ? 6 : 3, exchange["actionCostAudit"]!["opposition"]!["effectiveCost"]!.GetValue<int>());
        Assert.Equal(special, source.OriginalSpecialArtJson is not null);
        var original = (await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath))!;
        var binding = owner.BuildInputBinding().ToJsonString();
        var changed = JsonNode.Parse(original)!.AsObject();
        var update = changed[AfterlifeSpiritualConflictState.ResponseField]!;
        update["exchange"]![SpiritualWoundNoRollMargin.Property]!["player"] = margin + 1;
        update["activeConflictAfter"]!["exchangeLog"]![0] = update["exchange"]!.DeepClone();
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, changed.ToJsonString());
        var rejected = await owner.ContinueAsync(lease);
        Assert.Null(rejected.Session);
        Assert.Contains(rejected.Issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.Same(source, Assert.Single(owner.Sources));
        Assert.Equal(margin, source.Calculation.Input.HarmfulMargin);
        Assert.Equal(binding, owner.BuildInputBinding().ToJsonString());
        await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath, original);
        var restored = await owner.ContinueAsync(lease);
        AssertNoConflictFrameErrors(restored.Issues);
        Assert.Same(owner, restored.Session);
        Assert.Same(source, Assert.Single(owner.Sources));
        Assert.Empty(owner.ClaimedDice);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    /// <summary>Publishes a real I wound or explicit zero source through existing owners and common C4 exactly once.</summary>
    /// <param name="margin">Actual explicit GM choice, not an inferred margin.</param>
    [Theory]
    [InlineData(10L)]
    [InlineData(0L)]
    public async Task NoRollSource_ColdChoiceUsesCommonPublicationAndSignedHistory(long margin)
    {
        const string scene = "Чужое давление надломило волю души.";
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteVoluntaryIncomingHarmDraftAsync(context, special: false, margin);
        await WriteOriginalIntakeDraftAsync(context);
        await context.WriteExactJsonAsync(ProjectionNarrativePath,
            new JsonObject { ["response"] = scene, ["timestamp"] = "2026-09-30T00:00:00Z" }.ToJsonString());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        var historyBefore = await context.FileSystem.ReadFileBytesAsync(WoundHistoryState.HistoryPath);
        var resourcesBefore = await context.FileSystem.ReadFileBytesAsync(ResourceMaterializationContract.StatePath);
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            if (margin > 0)
            {
                await CommitSpiritualContinuationDraftFirstPairAsync(context, lease);
                var cold = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
                var opened = await cold.OpenC2PrivateSessionAsync(lease);
                AssertNoConflictFrameErrors(opened.Issues);
                Assert.Equal("offer", opened.Disposition);
                using var offered = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
                Assert.Equal(1, offered.Offer!.MaximumSeverityRank);
                var decision = OriginalSpiritualWoundDecision(offered.Offer.OpportunityRef, targetKind: "player");
                var accepted = await offered.SubmitDecisionAsync(lease, decision, scene);
                AssertNoConflictFrameErrors(accepted.Issues);
                Assert.Equal("completed_unpublished", accepted.Disposition);
                using var completed = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(accepted.Session);
                var repeated = await offered.SubmitDecisionAsync(lease, decision, scene);
                Assert.Equal("blocked", repeated.Disposition);
            }
            else
            {
                using var capture = await CompleteSourceOnlyPublicationCaptureAsync(context, lease);
                var prepared = await capture.PrepareC4PublicationAsync(lease);
                AssertNoConflictFrameErrors(prepared.Issues);
                var authority = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority>(prepared.Authority);
                Assert.Null(authority.LiveWoundCompletion);
                Assert.True(AcceptedMechanicsPlanAuthority.RegisterSpiritualPublication(context.FileSystem, lease, authority));
            }
            Assert.Equal(resourcesBefore, await context.FileSystem.ReadFileBytesAsync(lease, ResourceMaterializationContract.StatePath));
            Assert.Equal(historyBefore, await context.FileSystem.ReadFileBytesAsync(lease, WoundHistoryState.HistoryPath));
        }
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        var expected = await PeekPlanAsync(context);
        var spend = Assert.Single(expected.ResourceEvents.Where(row =>
            row.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeConflictSide));
        Assert.Equal(6m, spend.Before);
        Assert.Equal(3m, spend.After);
        Assert.Equal(3m, spend.AppliedAmount);
        ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority retained;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out retained));
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, await ReadSpiritualC4BackupsAsync(context));
        AssertNoConflictFrameErrors(published.Issues);
        Assert.Same(expected, published.MechanicsPlan);
        Assert.True(JsonNode.DeepEquals(expected.StateAfterImage, await context.ReadJsonAsync(ResourceMaterializationContract.StatePath)));
        Assert.True(JsonNode.DeepEquals(expected.HistoryAfterImage, await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath)));
        foreach (var pair in expected.WoundCarrierAfterImages.Concat(expected.EffectCarrierAfterImages)
                     .Concat(expected.OwnerCompanionAfterImages))
            Assert.True(JsonNode.DeepEquals(pair.Value, await context.ReadJsonAsync(pair.Key)), pair.Key);
        var conflict = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!;
        var exchange = conflict["activeConflict"]!["exchangeLog"]![0]!;
        Assert.Equal(margin, exchange[SpiritualWoundNoRollMargin.Property]!["player"]!.GetValue<long>());
        Assert.Null(exchange["diceAudit"]);
        var receipt = (await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!;
        if (margin > 0)
        {
            var witness = Assert.Single(receipt["sources"]!.AsArray())!["witness"]!;
            Assert.Equal(margin, witness["harmfulMargin"]!.GetValue<long>());
            Assert.Empty(witness["selectedDiceIndices"]!.AsArray());
            Assert.Empty(witness["turnEvidence"]!["diceClaims"]!.AsArray());
            Assert.Single(receipt["decisions"]!.AsArray());
            Assert.Equal(1, (await ReadCombatHealingWoundAsync(context)).Severity.Rank);
            var history = WoundHistoryState.Parse((await context.ReadJsonAsync(WoundHistoryState.HistoryPath))!.ToJsonString(), WoundHistoryState.HistoryPath);
            Assert.True(history.IsValid, string.Join("\n", history.Issues));
            Assert.Equal("create", Assert.Single(history.State!.Transitions).Kind);
        }
        else
        {
            Assert.Empty(receipt["sources"]!.AsArray());
            Assert.Empty(receipt["decisions"]!.AsArray());
            Assert.Equal(historyBefore, await context.FileSystem.ReadFileBytesAsync(WoundHistoryState.HistoryPath));
            var profiles = (await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath))!;
            Assert.Empty(profiles["profiles"]!.AsArray().Single(row => row!["actorId"]!.GetValue<string>() == "player_soul")!["activeWounds"]!.AsArray());
        }
        foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                     SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
            Assert.Null(await context.FileSystem.ReadFileBytesAsync(path));
        var finalResources = await context.FileSystem.ReadFileBytesAsync(ResourceMaterializationContract.StatePath);
        var finalHistory = await context.FileSystem.ReadFileBytesAsync(WoundHistoryState.HistoryPath);
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, retained, out _));
        Assert.Equal(finalResources, await context.FileSystem.ReadFileBytesAsync(ResourceMaterializationContract.StatePath));
        Assert.Equal(finalHistory, await context.FileSystem.ReadFileBytesAsync(WoundHistoryState.HistoryPath));
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43, currentRealm: "Chaos Sea",
            preGeneratedDices1d20: [15, 5, 12, 8], requestId: "request_no_roll_history_43");
        AssertNoConflictFrameErrors(await ValidatePublishedConflictPhaseAsync(context));
    }
}
