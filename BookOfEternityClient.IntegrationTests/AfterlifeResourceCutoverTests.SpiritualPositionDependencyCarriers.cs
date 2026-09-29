using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Corrects the actual update-wrapper row while preserving the explicit closed exchange and its ignored duplicate.
    /// </summary>
    /// <returns>
    /// A task completing after genuine saved-selection replay, raw-pointer checks and completed reduction.
    /// </returns>
    [Fact]
    public Task OriginalSpiritualC2_PositionDependencyUsesExactUpdateWrapperCarrier() =>
        ExecutePositionDependencyCarrierAsync(wrapper: true, multipleFrontiers: false);

    /// <summary>
    /// Derives two causal position groups across a real intermediate zero-opportunity exchange without changing its result.
    /// </summary>
    /// <returns>
    /// A task completing after both same-band corrections execute under the original saved wound selection.
    /// </returns>
    [Fact]
    public Task OriginalSpiritualC2_PositionDependencyWalksTwoSameBandFrontiers() =>
        ExecutePositionDependencyCarrierAsync(wrapper: false, multipleFrontiers: true);

    /// <summary>
    /// Authors and resumes genuine C2 position dependencies in one raw wrapper or across two sequential frontiers.
    /// </summary>
    /// <param name="wrapper">
    /// Whether the raw input uses the existing exchange-update carrier and duplicate closed-prefix row.
    /// </param>
    /// <param name="multipleFrontiers">
    /// Whether tier-two pressure pays one point per actor across three exchanges, with a zero-opportunity middle result.
    /// </param>
    /// <returns>
    /// A task completing after the real completed reduction proves one wound, unchanged choice and once-only costs.
    /// </returns>
    private static async Task ExecutePositionDependencyCarrierAsync(bool wrapper, bool multipleFrontiers)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: async original =>
            {
                await SeedOriginalIntakeBaselinesAsync(original);
                if (multipleFrontiers) await SeedSpiritualWorseningTierAuthorityAsync(original);
            }, signedDice: multipleFrontiers ? [15, 5, 8, 9, 13, 8] : [15, 5, 13, 8]);
        await WritePositionBurdenExchangesAsync(context, playerWound: false, modifier: "missing");
        var projected = await ReadProjectedSourceContinuationCandidateAsync(context);
        var log = projected["activeConflict"]!["exchangeLog"]!.AsArray();
        if (multipleFrontiers)
        {
            var middle = log[1]!.AsObject();
            var last = middle.DeepClone().AsObject();
            middle["outcome"] = "no_effect";
            middle["after"] = middle["before"]!.DeepClone();
            middle["diceAudit"]!["diceUsed"]![0]!["value"] = 8;
            middle["diceAudit"]!["diceUsed"]![1]!["value"] = 9;
            middle["diceAudit"]!["playerTotal"] = 8;
            middle["diceAudit"]!["oppositionTotal"] = 9;
            middle["diceAudit"]!["margin"] = -1;
            middle["diceAudit"]!["outcomeBand"] = "mixed_or_no_effect";
            last["exchangeId"] = "exchange_position_dependency_third";
            last["before"] = middle["after"]!.DeepClone();
            last["diceAudit"]!["diceUsed"]![0]!["sourceIndex"] = 4;
            last["diceAudit"]!["diceUsed"]![1]!["sourceIndex"] = 5;
            log.Add(last);
            for (var index = 0; index < log.Count; index++)
                foreach (var side in new[] { "player", "opposition" })
                {
                    var cost = log[index]!["actionCostAudit"]![side]!;
                    cost["artTier"] = 2;
                    cost["effectiveCost"] = 1;
                    cost["before"] = 6 - index;
                    cost["after"] = 5 - index;
                }
        }
        var original = wrapper ? WrapC2DependentConflict(projected) : projected;
        var corrected = original.DeepClone().AsObject();
        var rawLog = PositionDependencyRawLog(corrected, wrapper);
        for (var index = 1; index < rawLog.Count; index++)
        {
            var dice = rawLog[index]!["diceAudit"]!;
            dice["modifierBreakdown"]!["player"]!.AsArray().Add(new JsonObject
            {
                ["modifierType"] = "conflict_position", ["source"] = "conflictPosition",
                ["position"] = "player_advantaged", ["value"] = 2
            });
            dice["playerTotal"] = dice["playerTotal"]!.GetValue<int>() + 2;
            dice["margin"] = dice["margin"]!.GetValue<int>() + 2;
            Assert.Equal(multipleFrontiers && index == 1 ? "mixed_or_no_effect" : "player_success",
                dice["outcomeBand"]!.GetValue<string>());
        }
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, original.ToJsonString());
        const string scene = "Чужое давление надломило волю хранителя.";
        await context.WriteExactJsonAsync(ProjectionNarrativePath, new JsonObject { ["response"] = scene }.ToJsonString());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var unchanged = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in new[]
        {
            "input/turn_request.json", "game_state/control/pending_turn_snapshot.json",
            "game_state/control/pending_turn_snapshot.authority.json", ProjectionNarrativePath,
            AfterlifeEntityProfileState.StatePath, ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath, WoundIdentityState.StatePath,
            WoundHistoryState.HistoryPath, EffectIdentityState.StatePath, SpiritualWoundOpportunityReceiptState.StatePath
        })
            unchanged[path] = await context.FileSystem.ReadFileBytesAsync(lease, path);
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using (var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(captured.Capture))
        {
            AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
            var step = await capture.AdvanceNextResourceExchangeAsync(lease);
            AssertNoConflictFrameErrors(step.Issues);
            Assert.Equal(0, step.Step!.Interval!.Ordinal);
            var committed = await capture.CommitC2FirstTransportAsync(lease, step.Step.Interval);
            AssertNoConflictFrameErrors(committed.Issues);
            Assert.Equal("committed", committed.Disposition);
        }
        var opened = await context.Validator.OpenC2PrivateSessionAsync(lease);
        AssertNoConflictFrameErrors(opened.Issues);
        Assert.Equal("offer", opened.Disposition);
        string originalOpportunity;
        using (var offered = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session))
        {
            originalOpportunity = offered.Offer!.OpportunityRef;
            var decision = JsonNode.Parse(OriginalSpiritualWoundDecision(originalOpportunity,
                "spiritual_position_burden", "pressure").GetRawText())!;
            decision["proposal"]!["consequenceDefinitions"]![0]!["definition"]!["components"]![0]!["payload"]!["magnitude"] = 1;
            var selected = await offered.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(decision), scene);
            using var selectedOwner = selected.Session;
            Assert.True(selected.Disposition == "dependent_continuation", FormatC2SubmissionIssues(selected));
        }
        var savedCheckpoint = (await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath))!;
        var savedPending = (await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundDecisionPendingState.StatePath))!;
        var savedCommand = (await context.FileSystem.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!;
        var saved = JsonNode.Parse(savedCheckpoint)!["checkpoint"]!;
        var savedDecision = saved["pendingSubmission"]!["stagedDecision"]!;
        Assert.Equal(originalOpportunity, savedDecision["opportunityRef"]!.GetValue<string>());
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var dependentOpen = await fresh.OpenC2PrivateSessionAsync(lease);
        Assert.Equal("dependent_continuation", dependentOpen.Disposition);
        string continuationId;
        string[] expectedPointers;
        using (var dependent = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(dependentOpen.Session))
        {
            Assert.Null(dependent.Offer);
            var descriptor = Assert.IsType<ValidationService.SpiritualC2DependentContext>(await dependent.ReadDependentContextAsync(lease));
            continuationId = descriptor.ContinuationId;
            var prefix = wrapper ? "/afterlifeSpiritualConflictUpdate/activeConflictAfter/exchangeLog/" : "/activeConflict/exchangeLog/";
            expectedPointers = Enumerable.Range(1, multipleFrontiers ? 2 : 1).SelectMany(index =>
                new[] { "margin", "modifierBreakdown", "playerTotal" }.Select(field => prefix + index + "/diceAudit/" + field))
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            Assert.Equal(expectedPointers, descriptor.DependentDraftFields.Select(field => field.JsonPointer));
            Assert.All(descriptor.DependentDraftFields, field => Assert.Equal(AfterlifeSpiritualConflictState.StatePath, field.Path));
            Assert.Equal(multipleFrontiers ? 2 : 1, descriptor.BaselineIssues.Count);
            Assert.All(descriptor.BaselineIssues, issue => Assert.Equal("afterlife_conflict_dice_missing_position_modifier", issue.Code));
        }
        Assert.Equal(savedCheckpoint, await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.Equal(savedPending, await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundDecisionPendingState.StatePath));

        if (multipleFrontiers)
        {
            var firstOnly = original.DeepClone().AsObject();
            PositionDependencyRawLog(firstOnly, wrapper)[1]!["diceAudit"] = rawLog[1]!["diceAudit"]!.DeepClone();
            await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath,
                Encoding.UTF8.GetBytes(firstOnly.ToJsonString()));
            var partialOpen = await fresh.OpenC2PrivateSessionAsync(lease);
            Assert.Equal("dependent_continuation", partialOpen.Disposition);
            using var partial = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(partialOpen.Session);
            var descriptor = Assert.IsType<ValidationService.SpiritualC2DependentContext>(await partial.ReadDependentContextAsync(lease));
            Assert.Equal(continuationId, descriptor.ContinuationId);
            Assert.Equal(expectedPointers, descriptor.DependentDraftFields.Select(field => field.JsonPointer));
            Assert.Equal("activeConflict.exchangeLog[2].diceAudit.modifierBreakdown.player",
                Assert.Single(descriptor.CurrentIssues).FilePath);
            Assert.Equal(savedCheckpoint, await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath));
            Assert.Equal(savedPending, await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundDecisionPendingState.StatePath));
        }
        else
        {
            var ignoredDrift = corrected.DeepClone().AsObject();
            PositionDependencyRawLog(ignoredDrift, wrapper)[0]!["ignoredWitness"] = "not authorized";
            await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath,
                Encoding.UTF8.GetBytes(ignoredDrift.ToJsonString()));
            var invalidOpen = await fresh.OpenC2PrivateSessionAsync(lease);
            using var invalid = invalidOpen.Session;
            if (invalid is not null)
            {
                var rejected = await invalid.ResumeDependentContinuationAsync(lease);
                using var rejectedOwner = rejected.Session;
                Assert.True(rejected.Disposition == "blocked", FormatC2SubmissionIssues(rejected));
            }
            Assert.Equal(savedCheckpoint, await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath));
            Assert.Equal(savedPending, await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundDecisionPendingState.StatePath));
        }

        await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath,
            Encoding.UTF8.GetBytes(corrected.ToJsonString()));
        var correctedOpen = await fresh.OpenC2PrivateSessionAsync(lease);
        Assert.Equal("dependent_continuation", correctedOpen.Disposition);
        using var correctedOwner = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(correctedOpen.Session);
        var correctedContext = Assert.IsType<ValidationService.SpiritualC2DependentContext>(await correctedOwner.ReadDependentContextAsync(lease));
        Assert.Equal(continuationId, correctedContext.ContinuationId);
        Assert.Empty(correctedContext.CurrentIssues);
        var advanced = await correctedOwner.ResumeDependentContinuationAsync(lease);
        Assert.True(advanced.Disposition == "offer", FormatC2SubmissionIssues(advanced));
        using var next = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(advanced.Session);
        Assert.NotEqual(originalOpportunity, next.Offer!.OpportunityRef);
        Assert.Equal(savedCommand, await context.FileSystem.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
        var pending = JsonNode.Parse(await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundDecisionPendingState.StatePath) ?? [])!["pending"]!;
        Assert.True(JsonNode.DeepEquals(savedDecision, Assert.Single(pending["stagedDecisions"]!.AsArray())));
        Assert.Equal(multipleFrontiers ? 3 : 2, pending["cursor"]!["exchangeOrdinal"]!.GetValue<int>());
        var nextSource = pending["sources"]![pending["cursor"]!["nextSourceOrdinal"]!.GetValue<int>()]!;
        Assert.Equal(multipleFrontiers ? "exchange_position_dependency_third" : "exchange_source_second",
            nextSource["exchangeId"]!.GetValue<string>());
        if (multipleFrontiers)
        {
            // The source classifier emits harm only for an increasing strain, so this closed no-effect interval has no source row.
            Assert.DoesNotContain(pending["sources"]!.AsArray(), source =>
                source!["exchangeId"]!.GetValue<string>() == "exchange_source_second");
        }
        var completed = await next.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(new
        {
            opportunityRef = next.Offer.OpportunityRef, decision = "none"
        }), null);
        Assert.True(completed.Disposition == "completed_unpublished", FormatC2SubmissionIssues(completed));
        using var final = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(completed.Session);
        var reduction = await final.ReduceCompletedDecisionsAsync(lease);
        AssertNoConflictFrameErrors(reduction.Issues);
        Assert.True(reduction.Success);
        var ordinary = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(reduction.Reduction);
        Assert.Single(Assert.IsType<SpiritualLiveWoundCompletion>(ordinary.LiveWoundCompletion).Insertions);
        Assert.Equal(2, reduction.ReceiptAfterImage!["decisions"]!.AsArray().Count);
        var spends = ordinary.Resources.AppliedTransitions.Where(transition => transition.Operation == ResourceTransitionOperation.Spend).ToArray();
        Assert.Equal(multipleFrontiers ? 6 : 4, spends.Length);
        Assert.All(spends, transition => Assert.Equal(multipleFrontiers ? 1m : 3m, transition.AppliedAmount));
        if (multipleFrontiers)
            Assert.Equal(2, spends.Count(transition => transition.OriginId == "exchange_source_second"));
        foreach (var after in multipleFrontiers ? new[] { 5m, 4m, 3m } : new[] { 3m, 0m })
            Assert.Equal(2, spends.Count(transition => transition.AfterState!.Current == after));
        Assert.True(JsonNode.DeepEquals(corrected, JsonNode.Parse(await context.FileSystem.ReadFileBytesAsync(lease,
            AfterlifeSpiritualConflictState.StatePath) ?? [])));
        Assert.True(JsonNode.DeepEquals(PositionDependencyRawLog(original, wrapper)[0], PositionDependencyRawLog(corrected, wrapper)[0]));
        if (wrapper) Assert.True(JsonNode.DeepEquals(original[AfterlifeSpiritualConflictState.ResponseField]!["exchange"],
            corrected[AfterlifeSpiritualConflictState.ResponseField]!["exchange"]));
        foreach (var pair in unchanged) Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
    }

    /// <summary>
    /// Reads the actual replacement log from a known direct or update-wrapper test carrier.
    /// </summary>
    /// <param name="root">
    /// Authored raw conflict root.
    /// </param>
    /// <param name="wrapper">
    /// Whether the existing update carrier supplies activeConflictAfter.
    /// </param>
    /// <returns>
    /// The mutable log at the exact carrier used by this fixture.
    /// </returns>
    private static JsonArray PositionDependencyRawLog(JsonObject root, bool wrapper) =>
        (wrapper ? root[AfterlifeSpiritualConflictState.ResponseField]!["activeConflictAfter"] : root["activeConflict"])!["exchangeLog"]!.AsArray();
}
