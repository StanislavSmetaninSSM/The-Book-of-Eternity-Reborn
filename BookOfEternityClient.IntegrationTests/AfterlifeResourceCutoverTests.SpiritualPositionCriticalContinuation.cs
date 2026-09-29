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
    /// Retains the current position correction when its newly required critical narration prevents speculative advancement.
    /// </summary>
    /// <returns>
    /// A task completing after cold replay retains A until authenticated progress commits its critical text and activates B.
    /// </returns>
    [Fact]
    public async Task OriginalSpiritualC2_PositionCriticalNarrationRetainsCurrentFrontierAndResumesCold()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeTierTwoBaselinesAsync,
            signedDice: [5, 15, 20, 18, 9, 8]);
        await WritePositionBurdenExchangesAsync(context, playerWound: true, modifier: "missing");
        var original = await WriteCriticalPositionContinuationDraftAsync(context);
        const string scene = "Чужое давление надломило волю души.";
        await context.WriteExactJsonAsync(ProjectionNarrativePath, new JsonObject
        {
            ["response"] = scene, ["timestamp"] = "2026-08-15T00:01:00Z"
        }.ToJsonString());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        var immutable = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        byte[] checkpoint;
        byte[] pending;
        byte[] command;
        string opportunityRef;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            foreach (var path in new[] { "input/turn_request.json", "game_state/control/pending_turn_snapshot.json",
                "game_state/control/pending_turn_snapshot.authority.json", ProjectionNarrativePath,
                AfterlifeEntityProfileState.StatePath, ResourceMaterializationContract.StatePath,
                ResourceMaterializationContract.HistoryPath, WoundIdentityState.StatePath,
                WoundHistoryState.HistoryPath, EffectIdentityState.StatePath, SpiritualWoundOpportunityReceiptState.StatePath })
                immutable[path] = await context.FileSystem.ReadFileBytesAsync(lease, path);
            var captured = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
            AssertNoConflictFrameErrors(captured.Issues);
            using (var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(captured.Capture))
            {
                AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
                var first = await capture.AdvanceNextResourceExchangeAsync(lease);
                AssertNoConflictFrameErrors(first.Issues);
                Assert.Equal(0, first.Step!.Interval!.Ordinal);
                var committed = await capture.CommitC2FirstTransportAsync(lease, first.Step.Interval);
                AssertNoConflictFrameErrors(committed.Issues);
                Assert.Equal("committed", committed.Disposition);
            }
            var opened = await context.Validator.OpenC2PrivateSessionAsync(lease);
            Assert.Equal("offer", opened.Disposition);
            using (var offered = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session))
            {
                opportunityRef = offered.Offer!.OpportunityRef;
                var decision = JsonNode.Parse(OriginalSpiritualWoundDecision(opportunityRef,
                    "spiritual_position_burden", "pressure", "player").GetRawText())!;
                decision["proposal"]!["consequenceDefinitions"]![0]!["definition"]!["components"]![0]!["payload"]!["magnitude"] = 1;
                var submitted = await offered.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(decision), scene);
                using var submittedOwner = submitted.Session;
                Assert.True(submitted.Disposition == "dependent_continuation", FormatC2SubmissionIssues(submitted));
            }
            checkpoint = (await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath))!;
            pending = (await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundDecisionPendingState.StatePath))!;
            command = (await context.FileSystem.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!;
        }
        var savedDecision = JsonNode.Parse(checkpoint)!["checkpoint"]!["pendingSubmission"]!["stagedDecision"]!;
        Assert.Equal(opportunityRef, savedDecision["opportunityRef"]!.GetValue<string>());
        Assert.Equal("materialize", savedDecision["decision"]!.GetValue<string>());
        var coldFs = new FileSystemManager(context.RootPath, NullLogger<FileSystemManager>.Instance);
        var cold = new ValidationService(coldFs, NullLogger<ValidationService>.Instance);
        await using var coldLease = await coldFs.AcquireCanonicalWriteLeaseAsync();
        string firstContinuationId;
        var coldOpened = await cold.OpenC2PrivateSessionAsync(coldLease);
        Assert.Equal("dependent_continuation", coldOpened.Disposition);
        using (var owner = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(coldOpened.Session))
        {
            Assert.Null(owner.Offer);
            // Intended RED: the existing sequential baseline discards this valid policy because it cannot invent GM text.
            var current = Assert.IsType<ValidationService.SpiritualC2DependentContext>(await owner.ReadDependentContextAsync(coldLease));
            firstContinuationId = current.ContinuationId;
            Assert.Equal(new[] { "/activeConflict/exchangeLog/1/diceAudit/criticalResult",
                "/activeConflict/exchangeLog/1/diceAudit/margin", "/activeConflict/exchangeLog/1/diceAudit/modifierBreakdown",
                "/activeConflict/exchangeLog/1/diceAudit/oppositionTotal" },
                current.DependentDraftFields.Select(field => field.JsonPointer));
            Assert.All(current.DependentDraftFields, field => Assert.Equal(AfterlifeSpiritualConflictState.StatePath, field.Path));
            Assert.NotEmpty(current.CurrentIssues);
            Assert.DoesNotContain(current.DependentDraftFields, field => field.JsonPointer.Contains("/exchangeLog/2/", StringComparison.Ordinal));
        }

        var issuedA = Assert.IsType<SpiritualWoundContinuationRequest>((await cold.ReadSpiritualWoundContinuationAsync(coldLease)).Request);
        var responseA = new SpiritualWoundContinuationResponse { ContinuationId = issuedA.ContinuationId, WoundDecisions = [] };
        var middleOnly = original.DeepClone().AsObject();
        var middleDice = middleOnly["activeConflict"]!["exchangeLog"]![1]!["diceAudit"]!;
        middleDice["modifierBreakdown"]!["opposition"]!.AsArray().Add(CriticalPositionOppositionModifier());
        middleDice["oppositionTotal"] = 20;
        middleDice["margin"] = 1;
        middleDice["criticalResult"] = new JsonObject
        {
            ["playerNaturalRoll"] = 20, ["oppositionNaturalRoll"] = 18,
            ["marginOutcomeBand"] = "mixed_or_no_effect", ["normalizedOutcomeBand"] = "player_success",
            ["scaleLimit"] = "Удачный бросок ограничен текущим духовным обменом и не создаёт нового вреда.",
            ["narrativeConstraint"] = "Обе стороны сохраняют напряжение, позицию и исходный no_effect."
        };
        foreach (var mutation in new[] { "critical_scalar", "independent_modifier" })
        {
            var damaged = middleOnly.DeepClone().AsObject();
            var damagedDice = damaged["activeConflict"]!["exchangeLog"]![1]!["diceAudit"]!;
            if (mutation == "critical_scalar")
                damagedDice["criticalResult"]!["playerNaturalRoll"] = 19;
            else
            {
                damagedDice["modifierBreakdown"]!["player"]![0]!["value"] = 2;
                damagedDice["playerTotal"] = 22;
                damagedDice["margin"] = 2;
            }
            await coldFs.WriteFileAtomicBytesAsync(coldLease, AfterlifeSpiritualConflictState.StatePath,
                Encoding.UTF8.GetBytes(damaged.ToJsonString()));
            var invalidOpened = await cold.OpenC2PrivateSessionAsync(coldLease);
            using (var invalid = invalidOpened.Session)
            {
                if (invalid is not null)
                {
                    var denied = await invalid.ResumeDependentContinuationAsync(coldLease);
                    using var deniedOwner = denied.Session;
                    Assert.True(denied.Disposition == "blocked", mutation + ": " + FormatC2SubmissionIssues(denied));
                    Assert.DoesNotContain(denied.Issues, issue => issue.FilePath.StartsWith("activeConflict.exchangeLog[2].", StringComparison.Ordinal));
                }
                else Assert.Equal("blocked", invalidOpened.Disposition);
            }
            Assert.Equal(checkpoint, await coldFs.ReadFileBytesAsync(coldLease, SpiritualWoundCaptureCheckpointState.StatePath));
            Assert.Equal(pending, await coldFs.ReadFileBytesAsync(coldLease, SpiritualWoundDecisionPendingState.StatePath));
            Assert.Equal(command, await coldFs.ReadFileBytesAsync(coldLease, AcceptedMechanicsPlan.WoundCommandPath));
        }
        await coldFs.WriteFileAtomicBytesAsync(coldLease, AfterlifeSpiritualConflictState.StatePath,
            Encoding.UTF8.GetBytes(middleOnly.ToJsonString()));
        var middleOpened = await cold.OpenC2PrivateSessionAsync(coldLease);
        Assert.Equal("dependent_continuation", middleOpened.Disposition);
        using (var owner = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(middleOpened.Session))
        {
            // Direct Resume cannot turn valid physical A into accepted history without authenticated progress.
            var resumed = await owner.ResumeDependentContinuationAsync(coldLease);
            using var resumedOwner = resumed.Session;
            Assert.True(resumed.Disposition == "dependent_continuation", FormatC2SubmissionIssues(resumed));
        }
        Assert.Equal(checkpoint, await coldFs.ReadFileBytesAsync(coldLease, SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.Equal(pending, await coldFs.ReadFileBytesAsync(coldLease, SpiritualWoundDecisionPendingState.StatePath));
        Assert.Equal(command, await coldFs.ReadFileBytesAsync(coldLease, AcceptedMechanicsPlan.WoundCommandPath));
        Assert.Equal(JsonSerializer.Serialize(issuedA), JsonSerializer.Serialize((await cold.ReadSpiritualWoundContinuationAsync(coldLease)).Request));
        var progress = await cold.CommitSpiritualWoundDependentProgressAsync(coldLease, issuedA, responseA);
        Assert.True(progress.Disposition == "committed", string.Join("\n", progress.Issues));
        var issuedB = Assert.IsType<SpiritualWoundContinuationRequest>(progress.NextRequest);
        Assert.NotEqual(issuedA.ContinuationId, issuedB.ContinuationId);
        var progressedCheckpoint = JsonNode.Parse((await coldFs.ReadFileBytesAsync(coldLease, SpiritualWoundCaptureCheckpointState.StatePath))!)!;
        Assert.Single(progressedCheckpoint["checkpoint"]!["pendingSubmission"]!["dependentDraftProgress"]!.AsArray());
        Assert.True(JsonNode.DeepEquals(savedDecision, progressedCheckpoint["checkpoint"]!["pendingSubmission"]!["stagedDecision"]));
        Assert.Equal(pending, await coldFs.ReadFileBytesAsync(coldLease, SpiritualWoundDecisionPendingState.StatePath));
        Assert.Equal(command, await coldFs.ReadFileBytesAsync(coldLease, AcceptedMechanicsPlan.WoundCommandPath));
        var nextOpened = await cold.OpenC2PrivateSessionAsync(coldLease);
        Assert.Equal("dependent_continuation", nextOpened.Disposition);
        using (var owner = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(nextOpened.Session))
        {
            var next = Assert.IsType<ValidationService.SpiritualC2DependentContext>(await owner.ReadDependentContextAsync(coldLease));
            Assert.NotEqual(firstContinuationId, next.ContinuationId);
            foreach (var suffix in new[] { "margin", "modifierBreakdown", "oppositionTotal" })
                Assert.Contains(next.DependentDraftFields, field => field.JsonPointer == "/activeConflict/exchangeLog/2/diceAudit/" + suffix);
            Assert.DoesNotContain(next.DependentDraftFields, field => field.JsonPointer.Contains("/exchangeLog/0/", StringComparison.Ordinal));
            Assert.All(next.CurrentIssues, issue => Assert.StartsWith("activeConflict.exchangeLog[2].", issue.FilePath));
            Assert.NotEmpty(next.CurrentIssues);
        }
        var corrected = middleOnly.DeepClone().AsObject();
        var thirdDice = corrected["activeConflict"]!["exchangeLog"]![2]!["diceAudit"]!;
        thirdDice["modifierBreakdown"]!["opposition"]!.AsArray().Add(CriticalPositionOppositionModifier());
        thirdDice["oppositionTotal"] = 10;
        thirdDice["margin"] = -1;
        await coldFs.WriteFileAtomicBytesAsync(coldLease, AfterlifeSpiritualConflictState.StatePath,
            Encoding.UTF8.GetBytes(corrected.ToJsonString()));
        var finalOpened = await cold.OpenC2PrivateSessionAsync(coldLease);
        Assert.Equal("dependent_continuation", finalOpened.Disposition);
        using var finalOwner = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(finalOpened.Session);
        var completed = await finalOwner.ResumeDependentContinuationAsync(coldLease);
        Assert.True(completed.Disposition == "completed_unpublished", FormatC2SubmissionIssues(completed));
        using var final = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(completed.Session);
        Assert.Null(final.Offer);
        var reduction = await final.ReduceCompletedDecisionsAsync(coldLease);
        AssertNoConflictFrameErrors(reduction.Issues);
        Assert.True(reduction.Success);
        var ordinary = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(reduction.Reduction);
        Assert.Single(Assert.IsType<SpiritualLiveWoundCompletion>(ordinary.LiveWoundCompletion).Insertions);
        Assert.Equal("exchange_conflict_frame_42", Assert.Single(reduction.ReceiptAfterImage!["sources"]!.AsArray())!["witness"]!["exchangeId"]!.GetValue<string>());
        var receipt = Assert.Single(reduction.ReceiptAfterImage["decisions"]!.AsArray())!;
        Assert.Equal(opportunityRef, receipt["opportunityRef"]!.GetValue<string>());
        Assert.Equal("spiritual_decision_" + savedDecision["decisionFingerprint"]!.GetValue<string>()[7..], receipt["decisionId"]!.GetValue<string>());
        Assert.Equal(command, await coldFs.ReadFileBytesAsync(coldLease, AcceptedMechanicsPlan.WoundCommandPath));
        var spends = ordinary.Resources.AppliedTransitions.Where(row => row.Operation == ResourceTransitionOperation.Spend).ToArray();
        Assert.Equal(6, spends.Length);
        Assert.All(spends, row => Assert.Equal(1m, row.AppliedAmount));
        foreach (var after in new[] { 5m, 4m, 3m })
            Assert.Equal(2, spends.Count(row => row.BeforeState!.Current == after + 1 && row.AfterState!.Current == after));
        Assert.True(JsonNode.DeepEquals(original["activeConflict"]!["exchangeLog"]![0], corrected["activeConflict"]!["exchangeLog"]![0]));
        for (var index = 1; index < 3; index++)
        {
            var before = original["activeConflict"]!["exchangeLog"]![index]!;
            var after = corrected["activeConflict"]!["exchangeLog"]![index]!;
            Assert.True(JsonNode.DeepEquals(before["before"], after["before"]));
            Assert.True(JsonNode.DeepEquals(before["after"], after["after"]));
            Assert.True(JsonNode.DeepEquals(before["diceAudit"]!["diceUsed"], after["diceAudit"]!["diceUsed"]));
            Assert.True(JsonNode.DeepEquals(before["diceAudit"]!["modifierBreakdown"]!["player"], after["diceAudit"]!["modifierBreakdown"]!["player"]));
        }
        Assert.True(JsonNode.DeepEquals(corrected, JsonNode.Parse(await coldFs.ReadFileBytesAsync(coldLease, AfterlifeSpiritualConflictState.StatePath) ?? [])));
        foreach (var pair in immutable) Assert.Equal(pair.Value, await coldFs.ReadFileBytesAsync(coldLease, pair.Key));
    }

    /// <summary>
    /// Builds the three-exchange signed draft with two unchanged no-effect results and an independent middle modifier.
    /// </summary>
    /// <param name="context">
    /// Signed player-harmed fixture with tier-two pressure authority and original dice 5, 15, 20, 18, 9, 8.
    /// </param>
    /// <returns>
    /// The exact written original carrier retained for immutable-prefix and input comparisons.
    /// </returns>
    private static async Task<JsonObject> WriteCriticalPositionContinuationDraftAsync(ResourceMaterializationTestContext context)
    {
        var root = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = root["activeConflict"]!;
        var log = active["exchangeLog"]!.AsArray();
        var middle = log[1]!;
        middle["outcome"] = "no_effect";
        middle["after"] = middle["before"]!.DeepClone();
        var dice = middle["diceAudit"]!;
        dice["diceUsed"]![0]!["value"] = 20;
        dice["diceUsed"]![1]!["value"] = 18;
        dice["modifierBreakdown"]!["player"]!.AsArray().Add(new JsonObject
        { ["modifierType"] = "situational", ["source"] = "original concentration", ["value"] = 1 });
        dice["playerTotal"] = 21;
        dice["oppositionTotal"] = 18;
        dice["margin"] = 3;
        dice["outcomeBand"] = "player_success";
        Assert.Null(dice["criticalResult"]);
        var third = middle.DeepClone();
        third["exchangeId"] = "exchange_position_critical_third";
        var thirdDice = third["diceAudit"]!;
        thirdDice["diceUsed"]![0]!["sourceIndex"] = 4;
        thirdDice["diceUsed"]![0]!["value"] = 9;
        thirdDice["diceUsed"]![1]!["sourceIndex"] = 5;
        thirdDice["diceUsed"]![1]!["value"] = 8;
        thirdDice["modifierBreakdown"]!["player"]!.AsArray().Clear();
        thirdDice["playerTotal"] = 9;
        thirdDice["oppositionTotal"] = 8;
        thirdDice["margin"] = 1;
        thirdDice["outcomeBand"] = "mixed_or_no_effect";
        log.Add(third);
        for (var index = 0; index < log.Count; index++)
            foreach (var side in new[] { "player", "opposition" })
            {
                var cost = log[index]!["actionCostAudit"]![side]!;
                cost["artTier"] = 2;
                cost["effectiveCost"] = 1;
                cost["before"] = 6 - index;
                cost["after"] = 5 - index;
                active[side + "SideStrain"] = third["after"]![side + "SideStrain"]!.DeepClone();
            }
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        return root;
    }

    /// <summary>
    /// Supplies the literal opposition advantage row prescribed by the saved player's magnitude-one pressure burden.
    /// </summary>
    /// <returns>
    /// A fresh modifier object for one later pressure exchange.
    /// </returns>
    private static JsonObject CriticalPositionOppositionModifier() => new()
    {
        ["modifierType"] = "conflict_position", ["source"] = "conflictPosition",
        ["position"] = "opposition_advantaged", ["value"] = 2
    };
}
