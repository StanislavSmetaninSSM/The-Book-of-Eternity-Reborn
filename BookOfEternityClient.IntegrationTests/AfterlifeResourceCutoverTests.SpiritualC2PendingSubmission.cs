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
    /// Rejects a lost selection record instead of reopening its old offer from an unconfirmed command.
    /// </summary>
    /// <param name="withPriorAdvance">
    /// Whether one previously committed decision must also remain intact.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalSpiritualC2PendingSubmission_RejectsLostSubmission(bool withPriorAdvance)
    {
        await using var context = await CreateC2PendingSubmissionContextAsync(withPriorAdvance);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var oldCheckpoint = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath);
        await InterruptC2SelectedAdvanceAsync(context, lease);
        var selectedCommand = await context.FileSystem.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath, oldCheckpoint!);

        var reopened = await new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance).OpenC2PrivateSessionAsync(lease);

        using var unexpectedSession = reopened.Session;
        Assert.Equal("blocked", reopened.Disposition);
        Assert.Null(reopened.Session);
        Assert.NotEmpty(reopened.Issues);
        Assert.Equal(oldCheckpoint, await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.Equal(selectedCommand, await context.FileSystem.ReadFileBytesAsync(lease,
            AcceptedMechanicsPlan.WoundCommandPath));
    }

    /// <summary>
    /// Rejects command drift during final advancement and recovers only from the exact committed command.
    /// </summary>
    /// <param name="deleteCommand">
    /// Removes the command when true; otherwise replaces its scene text.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalSpiritualC2PendingSubmission_FinalReadbackRejectsCommandDrift(bool deleteCommand)
    {
        await using var context = await CreateC2PendingSubmissionContextAsync(withPriorAdvance: false);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        using var owner = await PrepareC2SubmissionOwnerAsync(context, lease);
        var selectedCommand = await context.FileSystem.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
        var checkpointWrites = 0;
        var transported = await owner.CommitC2SavedTransportAsync(lease, async (held, _, path, bytes) =>
        {
            await context.FileSystem.WriteFileAtomicBytesAsync(held, path, bytes);
            if (path != SpiritualWoundCaptureCheckpointState.StatePath || ++checkpointWrites != 2) return;
            if (deleteCommand)
                context.FileSystem.DeleteFile(held, AcceptedMechanicsPlan.WoundCommandPath);
            else
            {
                var changed = JsonNode.Parse(selectedCommand!)!;
                changed["commands"]![0]!["finalSceneText"] = "Подмена во время итогового продвижения.";
                await context.FileSystem.WriteFileAtomicBytesAsync(held, AcceptedMechanicsPlan.WoundCommandPath,
                    Encoding.UTF8.GetBytes(changed.ToJsonString()));
            }
        });
        Assert.Equal(2, checkpointWrites);
        Assert.Equal("blocked", transported.Disposition);
        var denied = await new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance).OpenC2PrivateSessionAsync(lease);
        using var unexpectedSession = denied.Session;
        Assert.Equal("blocked", denied.Disposition);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath,
            selectedCommand!);
        var recovered = await new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance).OpenC2PrivateSessionAsync(lease);
        using var recoveredSession = recovered.Session;
        Assert.True(recovered.Disposition == "completed_unpublished", FormatC2SubmissionIssues(recovered));
        Assert.Equal(1, JsonNode.Parse((await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath))!)!["checkpoint"]!["committedAdvance"]!.GetValue<int>());
    }

    /// <summary>
    /// Recovers the exact selected wound after final checkpoint replacement fails, preserving older decisions.
    /// </summary>
    /// <param name="withPriorAdvance">
    /// Whether an earlier side's committed decision precedes the interrupted materialization.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalSpiritualC2PendingSubmission_ResumesExactSelectionAfterInterruptedAdvance(
        bool withPriorAdvance)
    {
        await using var context = await CreateC2PendingSubmissionContextAsync(withPriorAdvance);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var before = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath);
        var pendingBefore = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath);
        var resourcesBefore = await context.FileSystem.ReadFileBytesAsync(lease, ResourceMaterializationContract.StatePath);
        var saved = await InterruptC2SelectedAdvanceAsync(context, lease);
        var body = saved["checkpoint"]!.AsObject();
        var original = JsonNode.Parse(before!)!["checkpoint"]!;
        foreach (var field in new[] { "allocations", "advances", "committedAdvance", "expectedPendingPacketFingerprint" })
            Assert.True(JsonNode.DeepEquals(original[field], body[field]), field);
        Assert.Equal(pendingBefore, await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath));
        var submission = body["pendingSubmission"]!.AsObject();
        Assert.Equal(withPriorAdvance ? 1 : 0, submission["priorCommittedAdvance"]!.GetValue<int>());
        Assert.NotEmpty(submission["allocations"]!.AsArray());
        var retainedAllocations = body["allocations"]!.AsArray().Concat(submission["allocations"]!.AsArray())
            .Select(row => row!.DeepClone()).ToArray();
        var selectedFingerprint = submission["stagedDecision"]!["decisionFingerprint"]!.GetValue<string>();
        var opened = await new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance).OpenC2PrivateSessionAsync(lease);
        Assert.True(opened.Disposition == "dependent_continuation", FormatC2SubmissionIssues(opened));
        using var session = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
        Assert.Null(session.Offer);

        var resumed = await session.ResumeDependentContinuationAsync(lease);

        Assert.True(resumed.Disposition == "completed_unpublished", FormatC2SubmissionIssues(resumed));
        using var completed = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(resumed.Session);
        var final = JsonNode.Parse((await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath))!)!["checkpoint"]!;
        Assert.Null(final["pendingSubmission"]);
        Assert.Equal(withPriorAdvance ? 2 : 1, final["committedAdvance"]!.GetValue<int>());
        Assert.Equal(selectedFingerprint,
            final["advances"]!.AsArray().Last()!["newDecisionFingerprints"]![0]!.GetValue<string>());
        Assert.True(final["allocations"]!.AsArray().Count >= retainedAllocations.Length);
        for (var index = 0; index < retainedAllocations.Length; index++)
            Assert.True(JsonNode.DeepEquals(retainedAllocations[index], final["allocations"]![index]),
                $"Retained selection allocation {index} changed.");
        var reduced = await completed.ReduceCompletedDecisionsAsync(lease);
        AssertNoConflictFrameErrors(reduced.Issues);
        Assert.Single(reduced.ReceiptAfterImage!["decisions"]!.AsArray(),
            row => row!["decision"]!.GetValue<string>() == "materialize");
        Assert.Equal(resourcesBefore, await context.FileSystem.ReadFileBytesAsync(lease,
            ResourceMaterializationContract.StatePath));
        var reused = await session.ResumeDependentContinuationAsync(lease);
        Assert.Equal("blocked", reused.Disposition);
    }

    /// <summary>
    /// Rejects altered choice bytes or a rehashed allocation/source suffix before reoffering or advancing.
    /// </summary>
    /// <param name="damage">
    /// The physical command or saved selection evidence changed after its genuine transport.
    /// </param>
    [Theory]
    [InlineData("command_text")]
    [InlineData("allocation_removed")]
    [InlineData("allocation_reordered")]
    [InlineData("source")]
    public async Task OriginalSpiritualC2PendingSubmission_RejectsChangedSavedSelection(string damage)
    {
        await using var context = await CreateC2PendingSubmissionContextAsync(withPriorAdvance: false);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var saved = await InterruptC2SelectedAdvanceAsync(context, lease);
        var body = saved["checkpoint"]!.AsObject();
        var submission = body["pendingSubmission"]!.AsObject();
        if (damage == "command_text")
        {
            var command = JsonNode.Parse((await context.FileSystem.ReadFileBytesAsync(lease,
                AcceptedMechanicsPlan.WoundCommandPath))!)!;
            command["commands"]![0]!["finalSceneText"] = "Другой исходный текст.";
            await context.FileSystem.WriteFileAtomicBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath,
                Encoding.UTF8.GetBytes(command.ToJsonString()));
        }
        else
        {
            var allocations = submission["allocations"]!.AsArray();
            if (damage == "allocation_removed")
                allocations.RemoveAt(allocations.Count - 1);
            else if (damage == "allocation_reordered")
            {
                Assert.True(allocations.Count >= 2);
                var first = allocations[0]!.DeepClone();
                allocations[0] = allocations[1]!.DeepClone();
                allocations[1] = first;
                allocations[0]!["ordinal"] = body["allocations"]!.AsArray().Count;
                allocations[1]!["ordinal"] = body["allocations"]!.AsArray().Count + 1;
            }
            else
            {
                var decision = submission["stagedDecision"]!.AsObject();
                decision["sourceId"] = "different_saved_source";
                decision["decisionFingerprint"] = SpiritualWoundStateJson.Hash(decision,
                    "staged_decision", "decisionFingerprint");
            }
            body["checkpointFingerprint"] = SpiritualWoundStateJson.Hash(body,
                "spiritual_capture_checkpoint_v1", "checkpointFingerprint");
            await context.FileSystem.WriteFileAtomicBytesAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath,
                Encoding.UTF8.GetBytes(saved.ToJsonString()));
        }
        var checkpointBefore = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath);
        var pendingBefore = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath);

        var reopened = await new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance).OpenC2PrivateSessionAsync(lease);

        Assert.Equal("blocked", reopened.Disposition);
        Assert.Null(reopened.Session);
        Assert.NotEmpty(reopened.Issues);
        Assert.Equal(checkpointBefore, await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.Equal(pendingBefore, await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath));
    }

    /// <summary>
    /// Keeps the owner-selected command exact when physical command bytes change during checkpoint transport.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2PendingSubmission_StagingReadbackCannotBlessReplacementCommand()
    {
        await using var context = await CreateC2PendingSubmissionContextAsync(withPriorAdvance: false);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        using var owner = await PrepareC2SubmissionOwnerAsync(context, lease);
        var originalCommand = await context.FileSystem.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
        var checkpointWrites = 0;
        var transported = await owner.CommitC2SavedTransportAsync(lease, async (held, _, path, bytes) =>
        {
            await context.FileSystem.WriteFileAtomicBytesAsync(held, path, bytes);
            if (path != SpiritualWoundCaptureCheckpointState.StatePath) return;
            checkpointWrites++;
            var changed = JsonNode.Parse(originalCommand!)!;
            changed["commands"]![0]!["finalSceneText"] = "Подменённый текст при сохранении.";
            await context.FileSystem.WriteFileAtomicBytesAsync(held, AcceptedMechanicsPlan.WoundCommandPath,
                Encoding.UTF8.GetBytes(changed.ToJsonString()));
        });
        Assert.Equal("blocked", transported.Disposition);
        Assert.Equal(1, checkpointWrites);
        var checkpoint = JsonNode.Parse((await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath))!)!;
        Assert.Equal(originalCommand, Convert.FromBase64String(checkpoint["checkpoint"]!["pendingSubmission"]!
            ["command"]!["contentBase64"]!.GetValue<string>()));
        var blocked = await new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance).OpenC2PrivateSessionAsync(lease);
        Assert.Equal("blocked", blocked.Disposition);
        Assert.Null(blocked.Session);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath,
            originalCommand!);
        var restored = await new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance).OpenC2PrivateSessionAsync(lease);
        Assert.True(restored.Disposition == "dependent_continuation", FormatC2SubmissionIssues(restored));
        using var retained = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(restored.Session);
        Assert.Null(retained.Offer);
    }

    /// <summary>
    /// Recovers a genuine satisfied guarantee without fabricating or replacing a GM command.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2PendingSubmission_RetainsAutomaticGuaranteeWithoutCommand()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync, signedDice: [15, 5, 15, 5]);
        var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
        var art = SourceOwnerSpecialArt("player_soul", "player_soul");
        art["tier"] = 5;
        art["spiritualWoundEnvelope"] = new JsonObject
        {
            ["schemaVersion"] = 1, ["maximumSeverityRank"] = 1, ["guaranteedSeverityRank"] = 1
        };
        profiles["profiles"]![0]!["specialArts"]!.AsArray().Add(art);
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42,
            currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 15, 5]);
        await CommitInitialC2PairAsync(context, conflict =>
        {
            var active = conflict["activeConflict"]!.AsObject();
            var first = active["exchangeLog"]![0]!.AsObject();
            var second = first.DeepClone().AsObject();
            second["exchangeId"] = "exchange_guarantee_satisfied";
            second["before"] = first["after"]!.DeepClone();
            second["after"] = second["before"]!.DeepClone();
            second["after"]!["oppositionSideStrain"] = "fractured";
            second["diceAudit"]!["diceUsed"]![0]!["sourceIndex"] = 2;
            second["diceAudit"]!["diceUsed"]![1]!["sourceIndex"] = 3;
            second["specialArtAudit"] = new JsonObject
            {
                ["artId"] = "art_source_owner", ["ownerActorType"] = "player_soul",
                ["ownerActorId"] = "player_soul", ["baseOperation"] = "pressure",
                ["costMultiplierPercent"] = 200, ["effectNote"] = "Искусство оставляет след в духовном узоре."
            };
            var playerCost = second["actionCostAudit"]!["player"]!.AsObject();
            playerCost["before"] = 3;
            playerCost["after"] = 1;
            playerCost["artTier"] = 5;
            playerCost["effectiveCost"] = 2;
            playerCost["specialArtId"] = "art_source_owner";
            playerCost["specialCostMultiplierPercent"] = 200;
            playerCost["standardEffectiveCost"] = 1;
            second["actionCostAudit"]!["opposition"]!["before"] = 3;
            second["actionCostAudit"]!["opposition"]!["after"] = 0;
            active["exchangeLog"]!.AsArray().Add(second);
            active["oppositionSideStrain"] = "fractured";
        });
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        using (var first = await PrepareC2SubmissionOwnerAsync(context, lease))
        {
            var committed = await first.CommitC2SavedTransportAsync(lease);
            AssertNoConflictFrameErrors(committed.Issues);
            Assert.Equal("committed", committed.Disposition);
        }
        var commandBefore = await context.FileSystem.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
        var beforeTransportRead = await ReadSpiritualContinuationTransportFilesAsync(context);
        var transportRead = await new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance)
            .ReadSpiritualWoundContinuationAsync(lease);
        Assert.Equal("automatic_continuation", transportRead.Disposition);
        Assert.Null(transportRead.Request);
        Assert.Empty(transportRead.Issues);
        await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, beforeTransportRead);
        var saved = await InterruptC2SelectedAdvanceAsync(context, lease);
        var submission = saved["checkpoint"]!["pendingSubmission"]!;
        Assert.Equal(1, submission["priorCommittedAdvance"]!.GetValue<int>());
        Assert.Equal("guarantee_satisfied", submission["stagedDecision"]!["decision"]!.GetValue<string>());
        Assert.Null(submission["command"]);
        Assert.Equal(commandBefore, await context.FileSystem.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
        var opened = await new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance).OpenC2PrivateSessionAsync(lease);
        Assert.True(opened.Disposition == "dependent_continuation", FormatC2SubmissionIssues(opened));
        using var session = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
        Assert.Null(session.Offer);

        var resumed = await session.ResumeDependentContinuationAsync(lease);

        Assert.True(resumed.Disposition == "completed_unpublished", FormatC2SubmissionIssues(resumed));
        using var completed = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(resumed.Session);
        var reduced = await completed.ReduceCompletedDecisionsAsync(lease);
        AssertNoConflictFrameErrors(reduced.Issues);
        var receipts = reduced.ReceiptAfterImage!["decisions"]!.AsArray();
        Assert.Equal(2, receipts.Count);
        Assert.Equal("guarantee_satisfied", receipts[1]!["decision"]!.GetValue<string>());
        Assert.Equal(submission["stagedDecision"]!["satisfiedWoundId"]!.GetValue<string>(),
            receipts[1]!["woundId"]!.GetValue<string>());
        Assert.Null(receipts[1]!["transitionId"]);
        Assert.Equal(commandBefore, await context.FileSystem.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
    }

    /// <summary>
    /// Creates a genuine signed one-source or already advanced two-source decision frontier.
    /// </summary>
    /// <param name="withPriorAdvance">
    /// Commits the first side's decline when <see langword="true"/>.
    /// </param>
    /// <returns>
    /// File-backed fixture whose next source remains offered by the real C2 owner.
    /// </returns>
    private static async Task<ResourceMaterializationTestContext> CreateC2PendingSubmissionContextAsync(
        bool withPriorAdvance)
    {
        var context = withPriorAdvance ? await CreateTwoSourceC2ContextAsync() :
            await CreateCompleteConflictFrameContextAsync(seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        if (!withPriorAdvance)
            await CommitInitialC2PairAsync(context);
        else
        {
            await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            var opened = await new ValidationService(context.FileSystem,
                NullLogger<ValidationService>.Instance).OpenC2PrivateSessionAsync(lease);
            using var first = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
            var next = await first.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(new
            {
                opportunityRef = first.Offer!.OpportunityRef,
                decision = "none"
            }), null);
            Assert.True(next.Disposition == "offer", FormatC2SubmissionIssues(next));
            next.Session!.Dispose();
        }
        return context;
    }

    /// <summary>
    /// Composes a real materialization or retains automatic satisfaction at the current source.
    /// </summary>
    /// <param name="context">
    /// Signed fixture with a committed C2 frontier.
    /// </param>
    /// <param name="lease">
    /// Active lease retained by the calling test.
    /// </param>
    /// <returns>
    /// Genuine current capture ready for saved transport; the caller must dispose it.
    /// </returns>
    private static async Task<ValidationService.SpiritualOriginalTurnCapture> PrepareC2SubmissionOwnerAsync(
        ResourceMaterializationTestContext context, FileSystemManager.CanonicalWriteLease lease)
    {
        var classified = await new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance).ClassifySpiritualPendingAsync(lease);
        Assert.Equal("match", classified.Disposition);
        var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(classified.Capture);
        var next = await capture.ReadC2NextSourceAsync(lease);
        AssertNoConflictFrameErrors(next.Issues);
        var admitted = await capture.AdmitWoundSourceAsync(lease, next.Interval!, next.Source!);
        AssertNoConflictFrameErrors(admitted.Issues);
        var offered = await capture.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(offered.Issues);
        if (offered.Satisfaction is null)
        {
            var command = WoundResponseInputComposer.Compose(offered.Binding!, [offered.Opportunity!],
                [OriginalSpiritualWoundDecision(offered.Opportunity!.PublicRef,
                    "spiritual_action_cost_burden", "guard")],
                "Чужое давление надломило волю хранителя.", []);
            Assert.True(command.Success);
            await context.FileSystem.WriteFileAtomicBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath,
                Encoding.UTF8.GetBytes(command.CommandRoot!.ToJsonString()));
        }
        return capture;
    }

    /// <summary>
    /// Saves a real selected decision, then injects failure only into its final advancement replacement.
    /// </summary>
    /// <param name="context">
    /// Signed fixture with its current offered or automatically satisfied source.
    /// </param>
    /// <param name="lease">
    /// Active canonical lease for actual transport and read-back.
    /// </param>
    /// <returns>
    /// Exact durable checkpoint containing the interrupted selection.
    /// </returns>
    private static async Task<JsonObject> InterruptC2SelectedAdvanceAsync(
        ResourceMaterializationTestContext context, FileSystemManager.CanonicalWriteLease lease)
    {
        using var owner = await PrepareC2SubmissionOwnerAsync(context, lease);
        var checkpoints = 0;
        var result = await owner.CommitC2SavedTransportAsync(lease, async (held, _, path, bytes) =>
        {
            if (path == SpiritualWoundCaptureCheckpointState.StatePath && ++checkpoints == 2)
                throw new IOException("Interrupt after durable selection and before accepted advancement.");
            await context.FileSystem.WriteFileAtomicBytesAsync(held, path, bytes);
        });
        Assert.True(result.Disposition == "not_committed",
            result.Disposition + ": " + string.Join(Environment.NewLine, result.Issues.Select(issue => $"{issue.Code}: {issue}")));
        Assert.Equal(2, checkpoints);
        var checkpoint = JsonNode.Parse((await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath))!)!.AsObject();
        Assert.IsType<JsonObject>(checkpoint["checkpoint"]!["pendingSubmission"]);
        return checkpoint;
    }

    /// <summary>
    /// Formats actual owner diagnostics for a failed integration assertion.
    /// </summary>
    /// <param name="result">
    /// Adapter result whose disposition and issues must remain visible on failure.
    /// </param>
    /// <returns>
    /// Compact disposition and coded issue text.
    /// </returns>
    private static string FormatC2SubmissionIssues(ValidationService.SpiritualC2PrivateOpenResult result) =>
        result.Disposition + ": " + string.Join(Environment.NewLine,
            result.Issues.Select(issue => $"{issue.Code}: {issue}"));
}
