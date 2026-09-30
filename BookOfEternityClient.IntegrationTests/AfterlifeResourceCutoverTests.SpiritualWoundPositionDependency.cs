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
    /// Replays an actual saved position wound through fresh filesystem owners and corrects only its later dice arithmetic.
    /// This exercises durable C2 recovery, not a GameEngine process interruption or final publication.
    /// </summary>
    /// <param name="playerWound">
    /// Whether the first exchange wounds the player, requiring the later modifier on the opposition side.
    /// </param>
    /// <returns>
    /// A task completing after exact correction, immutable-input rejection and once-only completed reduction are proved.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalSpiritualC2_PositionDependencyReplaysSavedChoiceAndCorrectsOnlyLaterArithmetic(
        bool playerWound)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync,
            signedDice: playerWound ? [5, 15, 13, 8] : [15, 5, 13, 8]);
        await WritePositionBurdenExchangesAsync(context, playerWound, modifier: "missing");
        var original = await ReadProjectedSourceContinuationCandidateAsync(context);
        var corrected = original.DeepClone().AsObject();
        var secondDice = corrected["activeConflict"]!["exchangeLog"]![1]!["diceAudit"]!;
        var affectedSide = playerWound ? "opposition" : "player";
        secondDice["modifierBreakdown"]![affectedSide]!.AsArray().Add(new JsonObject
        {
            ["modifierType"] = "conflict_position", ["source"] = "conflictPosition",
            ["position"] = playerWound ? "opposition_advantaged" : "player_advantaged", ["value"] = 2
        });
        secondDice[affectedSide + "Total"] = playerWound ? 10 : 15;
        secondDice["margin"] = playerWound ? 3 : 7;
        // Both margins preserve player_success and every authored result, canonical delta and action cost.
        Assert.Equal("player_success", secondDice["outcomeBand"]!.GetValue<string>());
        var scene = playerWound ? "Чужое давление надломило волю души." : "Чужое давление надломило волю хранителя.";
        await context.WriteExactJsonAsync(ProjectionNarrativePath,
            new JsonObject { ["response"] = scene }.ToJsonString());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());

        var immutableImages = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        byte[] selectedCheckpoint;
        byte[] selectedPending;
        byte[] selectedCommand;
        JsonObject submitted;
        string opportunityRef;
        string continuationId;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            foreach (var path in new[]
            {
                "input/turn_request.json", "game_state/control/pending_turn_snapshot.json",
                "game_state/control/pending_turn_snapshot.authority.json", ProjectionNarrativePath,
                AfterlifeEntityProfileState.StatePath, ResourceMaterializationContract.StatePath,
                ResourceMaterializationContract.HistoryPath, WoundIdentityState.StatePath,
                WoundHistoryState.HistoryPath, EffectIdentityState.StatePath,
                SpiritualWoundOpportunityReceiptState.StatePath
            })
                immutableImages.Add(path, await context.FileSystem.ReadFileBytesAsync(lease, path));
            var captured = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
            AssertNoConflictFrameErrors(captured.Issues);
            using (var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(captured.Capture))
            {
                AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
                var first = await capture.AdvanceNextResourceExchangeAsync(lease);
                AssertNoConflictFrameErrors(first.Issues);
                Assert.Equal(0, first.Step!.Interval!.Ordinal);
                var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(
                    OriginalCaptureField(capture, "_source"));
                Assert.Equal(playerWound ? "player_soul:player_soul" : "guardian:guardian_frame",
                    Assert.Single(source.Sources).AffectedActor);
                Assert.Equal(new[] { 0, 1 }, source.ClaimedDice);
                var committed = await capture.CommitC2FirstTransportAsync(lease, first.Step.Interval);
                AssertNoConflictFrameErrors(committed.Issues);
                Assert.Equal("committed", committed.Disposition);
            }
            var opened = await context.Validator.OpenC2PrivateSessionAsync(lease);
            AssertNoConflictFrameErrors(opened.Issues);
            Assert.Equal("offer", opened.Disposition);
            using var offered = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
            opportunityRef = offered.Offer!.OpportunityRef;
            var decision = JsonNode.Parse(OriginalSpiritualWoundDecision(opportunityRef,
                "spiritual_position_burden", "pressure", playerWound ? "player" : "guardian").GetRawText())!;
            decision["proposal"]!["consequenceDefinitions"]![0]!["definition"]!["components"]![0]!["payload"]!["magnitude"] = 1;
            var selected = await offered.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(decision), scene);
            using var selectedSession = selected.Session;
            // Intended first RED: the existing cost-only classifier does not retain a position correction frontier.
            Assert.True(selected.Disposition == "dependent_continuation", FormatC2SubmissionIssues(selected));
            selectedCheckpoint = (await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundCaptureCheckpointState.StatePath))!;
            selectedPending = (await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundDecisionPendingState.StatePath))!;
            selectedCommand = (await context.FileSystem.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!;
            submitted = JsonNode.Parse(selectedCheckpoint)!["checkpoint"]!.AsObject();
            var pendingSubmission = Assert.IsType<JsonObject>(submitted["pendingSubmission"]);
            Assert.Equal(0, submitted["committedAdvance"]!.GetValue<int>());
            Assert.Equal("materialize", pendingSubmission["stagedDecision"]!["decision"]!.GetValue<string>());
            Assert.Equal(opportunityRef, pendingSubmission["stagedDecision"]!["opportunityRef"]!.GetValue<string>());
            Assert.Equal(selectedCommand, Convert.FromBase64String(
                pendingSubmission["command"]!["contentBase64"]!.GetValue<string>()));
            Assert.NotEmpty(pendingSubmission["allocations"]!.AsArray());
            Assert.True(JsonNode.DeepEquals(original, JsonNode.Parse(await context.FileSystem.ReadFileBytesAsync(
                lease, AfterlifeSpiritualConflictState.StatePath) ?? [])));
        }

        // All warm owners and the lease have ended. Reopen the actual physical pair without registry reset or new choice.
        var coldFs = new FileSystemManager(context.RootPath, NullLogger<FileSystemManager>.Instance);
        var coldValidator = new ValidationService(coldFs, NullLogger<ValidationService>.Instance);
        await using var coldLease = await coldFs.AcquireCanonicalWriteLeaseAsync();
        var coldOpened = await coldValidator.OpenC2PrivateSessionAsync(coldLease);
        Assert.Equal("dependent_continuation", coldOpened.Disposition);
        var correctionIssue = Assert.Single(coldOpened.Issues);
        Assert.Equal("afterlife_conflict_dice_missing_position_modifier", correctionIssue.Code);
        Assert.Equal("activeConflict.exchangeLog[1].diceAudit.modifierBreakdown." + affectedSide,
            correctionIssue.FilePath);
        using (var cold = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(coldOpened.Session))
        {
            Assert.Null(cold.Offer);
            var descriptor = Assert.IsType<ValidationService.SpiritualC2DependentContext>(
                await cold.ReadDependentContextAsync(coldLease));
            continuationId = descriptor.ContinuationId;
            Assert.Contains(descriptor.BaselineIssues,
                issue => issue.Code == "afterlife_conflict_dice_missing_position_modifier");
            Assert.NotEmpty(descriptor.CurrentIssues);
            Assert.All(descriptor.DependentDraftFields,
                field => Assert.Equal(AfterlifeSpiritualConflictState.StatePath, field.Path));
            const string dicePointer = "/activeConflict/exchangeLog/1/diceAudit/";
            Assert.Contains(descriptor.DependentDraftFields,
                field => field.JsonPointer.StartsWith(dicePointer + "modifierBreakdown", StringComparison.Ordinal));
            Assert.Contains(descriptor.DependentDraftFields, field => field.JsonPointer == dicePointer + affectedSide + "Total");
            Assert.Contains(descriptor.DependentDraftFields, field => field.JsonPointer == dicePointer + "margin");
            Assert.DoesNotContain(descriptor.DependentDraftFields,
                field => field.JsonPointer.Contains("actionCostAudit", StringComparison.Ordinal) ||
                    field.JsonPointer.StartsWith("/activeConflict/exchangeLog/0/", StringComparison.Ordinal));
            var replacement = await cold.SubmitDecisionAsync(coldLease, JsonSerializer.SerializeToElement(new
            {
                opportunityRef, decision = "none"
            }), null);
            using var rejectedReplacement = replacement.Session;
            Assert.Equal("blocked", replacement.Disposition);
        }

        foreach (var mutation in new[] { "independent_modifier", "signed_die", "closed_exchange" })
        {
            var damaged = corrected.DeepClone().AsObject();
            var log = damaged["activeConflict"]!["exchangeLog"]!;
            if (mutation == "independent_modifier")
                log[1]!["diceAudit"]!["modifierBreakdown"]!["player"]!.AsArray().Add(new JsonObject
                {
                    ["modifierType"] = "situational", ["source"] = "unapproved correction", ["value"] = 0
                });
            else if (mutation == "signed_die") log[1]!["diceAudit"]!["diceUsed"]![0]!["value"] = 14;
            else log[0]!["after"]!["conflictPosition"] = "player_advantaged";
            await coldFs.WriteFileAtomicBytesAsync(coldLease, AfterlifeSpiritualConflictState.StatePath,
                Encoding.UTF8.GetBytes(damaged.ToJsonString()));
            var invalid = await coldValidator.OpenC2PrivateSessionAsync(coldLease);
            using (var invalidSession = invalid.Session)
            {
                if (invalidSession is not null)
                {
                    // Deliberately bypass descriptor reads: execution itself must enforce the frozen comparison.
                    var forbiddenResume = await invalidSession.ResumeDependentContinuationAsync(coldLease);
                    using var forbiddenOwner = forbiddenResume.Session;
                    Assert.True(forbiddenResume.Disposition == "blocked",
                        $"Direct Resume accepted {mutation}: {FormatC2SubmissionIssues(forbiddenResume)}");
                }
            }
            Assert.Equal(selectedCheckpoint, await coldFs.ReadFileBytesAsync(coldLease, SpiritualWoundCaptureCheckpointState.StatePath));
            Assert.Equal(selectedPending, await coldFs.ReadFileBytesAsync(coldLease, SpiritualWoundDecisionPendingState.StatePath));
            Assert.Equal(selectedCommand, await coldFs.ReadFileBytesAsync(coldLease, AcceptedMechanicsPlan.WoundCommandPath));
            await coldFs.WriteFileAtomicBytesAsync(coldLease, AfterlifeSpiritualConflictState.StatePath,
                Encoding.UTF8.GetBytes(corrected.ToJsonString()));
        }

        var resumedOpen = await coldValidator.OpenC2PrivateSessionAsync(coldLease);
        AssertNoConflictFrameErrors(resumedOpen.Issues);
        Assert.Equal("dependent_continuation", resumedOpen.Disposition);
        using var resumed = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(resumedOpen.Session);
        Assert.Null(resumed.Offer);
        var correctedDescriptor = Assert.IsType<ValidationService.SpiritualC2DependentContext>(
            await resumed.ReadDependentContextAsync(coldLease));
        Assert.Equal(continuationId, correctedDescriptor.ContinuationId);
        Assert.Empty(correctedDescriptor.CurrentIssues);
        var advanced = await resumed.ResumeDependentContinuationAsync(coldLease);
        Assert.True(advanced.Disposition == (playerWound ? "completed_unpublished" : "offer"), FormatC2SubmissionIssues(advanced));
        using var next = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(advanced.Session);
        if (playerWound) Assert.Null(next.Offer);
        else Assert.NotEqual(opportunityRef, next.Offer!.OpportunityRef);
        Assert.Equal(selectedCommand, await coldFs.ReadFileBytesAsync(coldLease, AcceptedMechanicsPlan.WoundCommandPath));
        var advancedCheckpoint = JsonNode.Parse(await coldFs.ReadFileBytesAsync(coldLease,
            SpiritualWoundCaptureCheckpointState.StatePath) ?? [])!["checkpoint"]!;
        Assert.Null(advancedCheckpoint["pendingSubmission"]);
        var advancedPending = JsonNode.Parse(await coldFs.ReadFileBytesAsync(coldLease,
            SpiritualWoundDecisionPendingState.StatePath) ?? [])!["pending"]!;
        Assert.True(JsonNode.DeepEquals(submitted["pendingSubmission"]!["stagedDecision"],
            Assert.Single(advancedPending["stagedDecisions"]!.AsArray())));
        var firstAdvance = Assert.Single(advancedCheckpoint["advances"]!.AsArray())!;
        Assert.Equal(submitted["pendingSubmission"]!["stagedDecision"]!["decisionFingerprint"]!.GetValue<string>(),
            Assert.Single(firstAdvance["newDecisionFingerprints"]!.AsArray())!.GetValue<string>());
        var expectedPrefix = submitted["allocations"]!.AsArray()
            .Concat(submitted["pendingSubmission"]!["allocations"]!.AsArray()).ToArray();
        var actualAllocations = advancedCheckpoint["allocations"]!.AsArray();
        Assert.True(actualAllocations.Count >= expectedPrefix.Length);
        for (var index = 0; index < expectedPrefix.Length; index++)
            Assert.True(JsonNode.DeepEquals(expectedPrefix[index], actualAllocations[index]), $"Allocation {index} changed during replay.");

        var completed = playerWound ? advanced : await next.SubmitDecisionAsync(coldLease, JsonSerializer.SerializeToElement(new
        {
            opportunityRef = next.Offer!.OpportunityRef, decision = "none"
        }), null);
        Assert.True(completed.Disposition == "completed_unpublished", FormatC2SubmissionIssues(completed));
        using var terminal = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(completed.Session);
        var reduction = await terminal.ReduceCompletedDecisionsAsync(coldLease);
        AssertNoConflictFrameErrors(reduction.Issues);
        Assert.True(reduction.Success);
        var ordinary = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(reduction.Reduction);
        var wound = Assert.Single(Assert.IsType<SpiritualLiveWoundCompletion>(ordinary.LiveWoundCompletion).Insertions).Wound;
        Assert.Equal(playerWound ? "player_soul" : "guardian_frame", wound.Owner.OwnerId);
        Assert.Equal(1, wound.Severity.Rank);
        var decisions = reduction.ReceiptAfterImage!["decisions"]!.AsArray();
        Assert.Equal(playerWound ? 1 : 2, decisions.Count);
        Assert.Equal(wound.WoundId, Assert.Single(decisions,
            decision => decision!["decision"]!.GetValue<string>() == "materialize")!["woundId"]!.GetValue<string>());
        Assert.Equal(playerWound ? 0 : 1, decisions.Count(decision => decision!["decision"]!.GetValue<string>() == "none"));
        var spends = ordinary.Resources.AppliedTransitions.Where(transition =>
            transition.Operation == ResourceTransitionOperation.Spend).ToArray();
        Assert.Equal(4, spends.Length);
        Assert.All(spends, transition => Assert.Equal(3m, transition.AppliedAmount));
        Assert.Equal(2, spends.Count(transition => transition.AfterState!.Current == 3m));
        Assert.Equal(2, spends.Count(transition => transition.AfterState!.Current == 0m));
        var current = JsonNode.Parse(await coldFs.ReadFileBytesAsync(coldLease,
            AfterlifeSpiritualConflictState.StatePath) ?? [])!;
        Assert.True(JsonNode.DeepEquals(corrected, current));
        Assert.True(JsonNode.DeepEquals(original["activeConflict"]!["exchangeLog"]![0], current["activeConflict"]!["exchangeLog"]![0]));
        foreach (var exchange in current["activeConflict"]!["exchangeLog"]!.AsArray())
        {
            Assert.Equal("contested", exchange!["before"]!["conflictPosition"]!.GetValue<string>());
            Assert.Equal("contested", exchange["after"]!["conflictPosition"]!.GetValue<string>());
        }
        Assert.True(JsonNode.DeepEquals(original["activeConflict"]!["exchangeLog"]![1]!["diceAudit"]!["diceUsed"],
            current["activeConflict"]!["exchangeLog"]![1]!["diceAudit"]!["diceUsed"]));
        foreach (var pair in immutableImages)
            Assert.Equal(pair.Value, await coldFs.ReadFileBytesAsync(coldLease, pair.Key));
    }
}
