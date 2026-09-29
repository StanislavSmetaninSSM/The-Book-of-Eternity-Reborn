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
    /// Resolves the next offered source from the verified cold owner rather than a caller-supplied source.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2DecisionFrontier_ResolvesOwnedFirstPositiveSource()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var classified = await fresh.ClassifyInitialSpiritualPendingAsync(lease);
        Assert.Equal("match", classified.Disposition);
        using var cold = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(classified.Capture);

        var next = await cold.ReadC2NextSourceAsync(lease);

        AssertNoConflictFrameErrors(next.Issues);
        var admitted = await cold.AdmitWoundSourceAsync(lease, next.Interval!, next.Source!);
        AssertNoConflictFrameErrors(admitted.Issues);
        Assert.NotNull(admitted.Admission);
    }

    /// <summary>
    /// Invalidates a saved frontier when the physical checkpoint/pending pair changes after classification.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2DecisionFrontier_RejectsChangedPendingPair()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var classified = await fresh.ClassifyInitialSpiritualPendingAsync(lease);
        Assert.Equal("match", classified.Disposition);
        using var cold = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(classified.Capture);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath, Encoding.UTF8.GetBytes("{}"));

        var next = await cold.ReadC2NextSourceAsync(lease);

        Assert.Null(next.Source);
        Assert.NotEmpty(next.Issues);
    }

    /// <summary>
    /// Stages an actual declined source from the registered GM command without writing either private root.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2SavedDecision_StagesOwnedDeclineWithoutTransport()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var oldCheckpoint = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath);
        var oldPending = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath);
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var classified = await fresh.ClassifyInitialSpiritualPendingAsync(lease);
        Assert.Equal("match", classified.Disposition);
        using var cold = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(classified.Capture);
        var next = await cold.ReadC2NextSourceAsync(lease);
        AssertNoConflictFrameErrors(next.Issues);
        var admitted = await cold.AdmitWoundSourceAsync(lease, next.Interval!, next.Source!);
        AssertNoConflictFrameErrors(admitted.Issues);
        var offered = await cold.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(offered.Issues);
        var decline = JsonSerializer.SerializeToElement(new
        {
            opportunityRef = offered.Opportunity!.PublicRef,
            decision = "none"
        });
        var command = WoundResponseInputComposer.Compose(offered.Binding!,
            [offered.Opportunity], [decline], null, []);
        Assert.True(command.Success);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            AcceptedMechanicsPlan.WoundCommandPath,
            Encoding.UTF8.GetBytes(command.CommandRoot!.ToJsonString()));

        var staged = await cold.AdvanceC2NextDecisionDraftAsync(lease);

        AssertNoConflictFrameErrors(staged.Issues);
        Assert.Equal(1, staged.Checkpoint!.CommittedAdvance);
        var packet = JsonNode.Parse(SpiritualWoundDecisionPendingState.SerializeCanonical(staged.Pending!))!
            ["pending"]!.AsObject();
        Assert.Single(packet["stagedDecisions"]!.AsArray());
        Assert.Equal("none", packet["stagedDecisions"]![0]!["decision"]!.GetValue<string>());
        Assert.Equal(oldCheckpoint, await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.Equal(oldPending, await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath));
    }

    /// <summary>
    /// Executes a source-bound materialization into an unpublished successor with its actual allocation suffix.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2SavedDecision_StagesOwnedWoundAndAllocationSuffix()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var checkpointBefore = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath);
        var pendingBefore = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath);
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var classified = await fresh.ClassifyInitialSpiritualPendingAsync(lease);
        Assert.Equal("match", classified.Disposition);
        using var cold = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(classified.Capture);
        var next = await cold.ReadC2NextSourceAsync(lease);
        AssertNoConflictFrameErrors(next.Issues);
        var admitted = await cold.AdmitWoundSourceAsync(lease, next.Interval!, next.Source!);
        AssertNoConflictFrameErrors(admitted.Issues);
        var offered = await cold.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(offered.Issues);
        var decision = OriginalSpiritualWoundDecision(offered.Opportunity!.PublicRef);
        var command = WoundResponseInputComposer.Compose(offered.Binding!,
            [offered.Opportunity], [decision], "Чужое давление надломило волю хранителя.", []);
        Assert.True(command.Success);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            AcceptedMechanicsPlan.WoundCommandPath,
            Encoding.UTF8.GetBytes(command.CommandRoot!.ToJsonString()));

        var staged = await cold.AdvanceC2NextDecisionDraftAsync(lease);

        AssertNoConflictFrameErrors(staged.Issues);
        Assert.Equal(1, staged.Checkpoint!.CommittedAdvance);
        var originalCheckpoint = JsonNode.Parse(Encoding.UTF8.GetString(checkpointBefore!))!
            ["checkpoint"]!.AsObject();
        var savedCheckpoint = JsonNode.Parse(
            SpiritualWoundCaptureCheckpointState.SerializeCanonical(staged.Checkpoint))!
            ["checkpoint"]!.AsObject();
        Assert.True(savedCheckpoint["allocations"]!.AsArray().Count >
            originalCheckpoint["allocations"]!.AsArray().Count);
        Assert.Equal(savedCheckpoint["allocations"]!.AsArray().Count,
            savedCheckpoint["advances"]![0]!["allocationCount"]!.GetValue<int>());
        var packet = JsonNode.Parse(SpiritualWoundDecisionPendingState.SerializeCanonical(staged.Pending!))!
            ["pending"]!.AsObject();
        Assert.Equal("materialize", packet["stagedDecisions"]![0]!["decision"]!.GetValue<string>());
        Assert.NotNull(packet["stagedDecisions"]![0]!["woundDraftBase64"]);
        Assert.Equal(checkpointBefore, await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.Equal(pendingBefore, await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath));
    }

    /// <summary>
    /// Replays a saved decision through fresh original owners without reading its stale pending projection.
    /// </summary>
    /// <param name="materialize">
    /// Whether the saved command creates a wound or explicitly declines it.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalSpiritualC2SavedDecision_ColdReplayReproducesOwnerPacket(bool materialize)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var classified = await fresh.ClassifyInitialSpiritualPendingAsync(lease);
        Assert.Equal("match", classified.Disposition);
        using var cold = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(classified.Capture);
        var next = await cold.ReadC2NextSourceAsync(lease);
        AssertNoConflictFrameErrors(next.Issues);
        var admitted = await cold.AdmitWoundSourceAsync(lease, next.Interval!, next.Source!);
        AssertNoConflictFrameErrors(admitted.Issues);
        var offered = await cold.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(offered.Issues);
        var decision = materialize
            ? OriginalSpiritualWoundDecision(offered.Opportunity!.PublicRef)
            : JsonSerializer.SerializeToElement(new
            {
                opportunityRef = offered.Opportunity!.PublicRef,
                decision = "none"
            });
        var scene = materialize ? "Чужое давление надломило волю хранителя." : null;
        var command = WoundResponseInputComposer.Compose(offered.Binding!,
            [offered.Opportunity], [decision], scene, []);
        Assert.True(command.Success);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            AcceptedMechanicsPlan.WoundCommandPath,
            Encoding.UTF8.GetBytes(command.CommandRoot!.ToJsonString()));
        var staged = await cold.AdvanceC2NextDecisionDraftAsync(lease);
        AssertNoConflictFrameErrors(staged.Issues);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath,
            Encoding.UTF8.GetBytes(SpiritualWoundCaptureCheckpointState.SerializeCanonical(staged.Checkpoint!)));
        context.FileSystem.DeleteFile(lease, SpiritualWoundDecisionPendingState.StatePath);
        cold.Dispose();
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            AcceptedMechanicsPlan.WoundCommandPath, Encoding.UTF8.GetBytes("{}"));
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            ProjectionNarrativePath,
            Encoding.UTF8.GetBytes("{\"response\":\"Поздняя несохранённая правка.\"}"));
        var reopened = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

        var replayed = await reopened.ReplaySavedSpiritualCheckpointAsync(lease);

        AssertNoConflictFrameErrors(replayed.Issues);
        using var replayCapture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(replayed.Capture);
        Assert.Equal(staged.Pending!.PacketFingerprint, replayed.Pending!.PacketFingerprint);
        var savedBody = JsonNode.Parse(
            SpiritualWoundCaptureCheckpointState.SerializeCanonical(staged.Checkpoint!))!
            ["checkpoint"]!.AsObject();
        Assert.Equal(savedBody["allocations"]!.AsArray().Count,
            replayCapture.ReadAllocationCursor(lease));
    }

    /// <summary>
    /// Rejects a structurally rehashed checkpoint whose saved inputs, decision or allocation value was altered.
    /// </summary>
    /// <param name="mutation">
    /// Saved evidence changed without executing a new owner transition.
    /// </param>
    [Theory]
    [InlineData("command")]
    [InlineData("decision")]
    [InlineData("allocation")]
    public async Task OriginalSpiritualC2SavedDecision_RejectsRehashedEvidence(string mutation)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var staged = await StageC2SavedDecisionAsync(context, lease, materialize: true);
        var root = JsonNode.Parse(
            SpiritualWoundCaptureCheckpointState.SerializeCanonical(staged.Checkpoint!))!.AsObject();
        var body = root["checkpoint"]!.AsObject();
        var advance = body["advances"]![0]!.AsObject();
        if (mutation == "command")
        {
            var command = advance["inputChanges"]!.AsArray().Single(row =>
                row!["path"]!.GetValue<string>() == AcceptedMechanicsPlan.WoundCommandPath)!
                .AsObject();
            var altered = Encoding.UTF8.GetBytes("{}");
            command["contentBase64"] = Convert.ToBase64String(altered);
            command["contentFingerprint"] = new CanonicalBeforeImage(true, altered).Fingerprint;
        }
        else if (mutation == "decision")
            advance["newDecisionFingerprints"]![0] = "sha256:" + new string('0', 64);
        else
        {
            var allocation = body["allocations"]!.AsArray().Last(row =>
                row!["kind"]!.GetValue<string>() != "utc_time")!.AsObject();
            var value = allocation["value"]!.GetValue<string>();
            allocation["value"] = value[..^1] + (value[^1] == '0' ? '1' : '0');
        }
        body["checkpointFingerprint"] = SpiritualWoundStateJson.Hash(body,
            "spiritual_capture_checkpoint_v1", "checkpointFingerprint");
        var structurallyParsed = SpiritualWoundCaptureCheckpointState.Parse(root.ToJsonString(),
            SpiritualWoundCaptureCheckpointState.StatePath,
            staged.Checkpoint!.ReadOriginalDraftInputs().PathInventory);
        Assert.True(structurallyParsed.IsValid);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath,
            Encoding.UTF8.GetBytes(root.ToJsonString()));
        var reopened = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

        var replayed = await reopened.ReplaySavedSpiritualCheckpointAsync(lease);

        Assert.Null(replayed.Capture);
        Assert.NotEmpty(replayed.Issues);
    }

    /// <summary>
    /// Rejects a dependent narrative replacement that loses the accepted response shape.
    /// </summary>
    /// <param name="narrative">
    /// Invalid replacement for the registered narrative response.
    /// </param>
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"response\":42}")]
    [InlineData("{\"response\":\"Новый текст\",\"unrelated\":true}")]
    public async Task OriginalSpiritualC2SavedDecision_RejectsInvalidDependentNarrative(string narrative)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var classified = await fresh.ClassifyInitialSpiritualPendingAsync(lease);
        Assert.Equal("match", classified.Disposition);
        using var cold = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(classified.Capture);
        var next = await cold.ReadC2NextSourceAsync(lease);
        AssertNoConflictFrameErrors(next.Issues);
        var admitted = await cold.AdmitWoundSourceAsync(lease, next.Interval!, next.Source!);
        AssertNoConflictFrameErrors(admitted.Issues);
        var offered = await cold.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(offered.Issues);
        var decline = JsonSerializer.SerializeToElement(new
        {
            opportunityRef = offered.Opportunity!.PublicRef,
            decision = "none"
        });
        var command = WoundResponseInputComposer.Compose(offered.Binding!,
            [offered.Opportunity], [decline], null, []);
        Assert.True(command.Success);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            AcceptedMechanicsPlan.WoundCommandPath,
            Encoding.UTF8.GetBytes(command.CommandRoot!.ToJsonString()));
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            ProjectionNarrativePath, Encoding.UTF8.GetBytes(narrative));

        var staged = await cold.AdvanceC2NextDecisionDraftAsync(lease);

        Assert.Null(staged.Checkpoint);
        Assert.NotEmpty(staged.Issues);
        Assert.False(cold.IsCurrentOwner);
    }

    /// <summary>
    /// Reports invalid physical UTF-8 as a rejected decision instead of throwing to recovery.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2SavedDecision_RejectsInvalidCommandEncoding()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var classified = await fresh.ClassifyInitialSpiritualPendingAsync(lease);
        Assert.Equal("match", classified.Disposition);
        using var cold = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(classified.Capture);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            AcceptedMechanicsPlan.WoundCommandPath, [0xff]);

        var staged = await cold.AdvanceC2NextDecisionDraftAsync(lease);

        Assert.Null(staged.Checkpoint);
        Assert.NotEmpty(staged.Issues);
    }

    /// <summary>
    /// Reports invalid checkpoint UTF-8 as a recovery issue without retaining a capture.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2SavedReplay_RejectsInvalidCheckpointEncoding()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath, [0xff]);
        var reopened = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

        var replayed = await reopened.ReplaySavedSpiritualCheckpointAsync(lease);

        Assert.Null(replayed.Capture);
        Assert.NotEmpty(replayed.Issues);
    }

    /// <summary>
    /// Rejects attempts to move an original action, actor, die or unrelated draft sibling through C2.
    /// </summary>
    /// <param name="mutation">
    /// Independent original input to alter after committing the first pair.
    /// </param>
    [Theory]
    [InlineData("action")]
    [InlineData("actor")]
    [InlineData("die")]
    [InlineData("sibling")]
    public async Task OriginalSpiritualC2SavedDecision_RejectsIndependentOriginalMutation(string mutation)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        var conflict = await ReadProjectedSourceContinuationCandidateAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var classified = await fresh.ClassifyInitialSpiritualPendingAsync(lease);
        Assert.Equal("match", classified.Disposition);
        using var cold = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(classified.Capture);
        var next = await cold.ReadC2NextSourceAsync(lease);
        AssertNoConflictFrameErrors(next.Issues);
        var admitted = await cold.AdmitWoundSourceAsync(lease, next.Interval!, next.Source!);
        AssertNoConflictFrameErrors(admitted.Issues);
        var offered = await cold.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(offered.Issues);
        var decline = JsonSerializer.SerializeToElement(new
        {
            opportunityRef = offered.Opportunity!.PublicRef,
            decision = "none"
        });
        var command = WoundResponseInputComposer.Compose(offered.Binding!,
            [offered.Opportunity], [decline], null, []);
        Assert.True(command.Success);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            AcceptedMechanicsPlan.WoundCommandPath,
            Encoding.UTF8.GetBytes(command.CommandRoot!.ToJsonString()));
        if (mutation == "sibling")
            await context.FileSystem.WriteFileAtomicBytesAsync(lease,
                ResourceMaterializationContract.CommandPath, Encoding.UTF8.GetBytes("{\"altered\":true}"));
        else
        {
            if (mutation == "action")
                conflict["activeConflict"]!["exchangeLog"]![0]!["operationType"] = "guard";
            else if (mutation == "actor")
                conflict["activeConflict"]!["playerSide"]!["leadContestant"]!["actorId"] =
                    "changed_original_actor";
            else
                conflict["activeConflict"]!["exchangeLog"]![0]!["diceAudit"]!["diceUsed"]![0]!["sourceIndex"] = 2;
            await context.FileSystem.WriteFileAtomicBytesAsync(lease,
                AfterlifeSpiritualConflictState.StatePath,
                Encoding.UTF8.GetBytes(conflict.ToJsonString()));
        }

        var staged = await cold.AdvanceC2NextDecisionDraftAsync(lease);

        Assert.Null(staged.Checkpoint);
        Assert.NotEmpty(staged.Issues);
        Assert.Equal(mutation == "sibling", cold.IsCurrentOwner);
    }

    /// <summary>
    /// Leaves the same closed exchange active when its second harmful source still needs a decision.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2SavedDecision_KeepsSecondSourceInSameExchange()
    {
        await using var context = await CreateTwoSourceC2ContextAsync();
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var initial = JsonNode.Parse(Encoding.UTF8.GetString((await context.FileSystem.ReadFileBytesAsync(
            lease, SpiritualWoundDecisionPendingState.StatePath))!))!["pending"]!.AsObject();
        Assert.Equal(2, initial["sources"]!.AsArray().Count(source =>
            source!["maximumSeverityRank"]!.GetValue<int>() > 0));

        var staged = await StageC2SavedDecisionAsync(context, lease, materialize: false);

        var packet = JsonNode.Parse(SpiritualWoundDecisionPendingState.SerializeCanonical(staged.Pending!))!
            ["pending"]!.AsObject();
        Assert.Equal(1, packet["cursor"]!["exchangeOrdinal"]!.GetValue<int>());
        Assert.Equal(1, packet["cursor"]!["nextSourceOrdinal"]!.GetValue<int>());
        Assert.Single(packet["stagedDecisions"]!.AsArray());
    }

    /// <summary>
    /// Creates an original exchange with two legitimate harmful sources sharing one closed interval.
    /// </summary>
    /// <returns>
    /// Signed fixture with a committed initial C2 checkpoint and pending pair.
    /// </returns>
    private static async Task<ResourceMaterializationTestContext> CreateTwoSourceC2ContextAsync()
    {
        var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context, conflict =>
        {
            var active = conflict["activeConflict"]!.AsObject();
            var exchange = active["exchangeLog"]![0]!.AsObject();
            exchange["after"]!["playerSideStrain"] = "broken";
            exchange["after"]!["oppositionSideStrain"] = "broken";
            active["playerSideStrain"] = "broken";
            active["oppositionSideStrain"] = "broken";
            var dice = exchange["diceAudit"]!;
            dice["diceUsed"]![0]!["sourceIndex"] = 2;
            dice["diceUsed"]![0]!["value"] = 12;
            dice["diceUsed"]![1]!["sourceIndex"] = 3;
            dice["diceUsed"]![1]!["value"] = 8;
            dice["playerTotal"] = 12;
            dice["oppositionTotal"] = 8;
            dice["margin"] = 4;
            dice["outcomeBand"] = "player_success";
        });
        return context;
    }

    /// <summary>
    /// Reopens a saved successor and withholds decision authority when its pending projection is lost.
    /// </summary>
    /// <param name="pendingState">
    /// Physical pending image after the authoritative checkpoint has been saved.
    /// </param>
    [Theory]
    [InlineData("match")]
    [InlineData("absent")]
    [InlineData("malformed")]
    public async Task OriginalSpiritualC2SavedDecision_ClassifiesAdvancedMatchingPair(string pendingState)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var staged = await StageC2SavedDecisionAsync(context, lease, materialize: false);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath,
            Encoding.UTF8.GetBytes(SpiritualWoundCaptureCheckpointState.SerializeCanonical(staged.Checkpoint!)));
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath,
            Encoding.UTF8.GetBytes(SpiritualWoundDecisionPendingState.SerializeCanonical(staged.Pending!)));
        if (pendingState == "absent")
            context.FileSystem.DeleteFile(lease, SpiritualWoundDecisionPendingState.StatePath);
        else if (pendingState == "malformed")
            await context.FileSystem.WriteFileAtomicBytesAsync(lease,
                SpiritualWoundDecisionPendingState.StatePath, [0xff]);
        var reopened = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

        var classified = await reopened.ClassifySpiritualPendingAsync(lease);

        Assert.Equal(pendingState == "match" ? "match" : "repair_required",
            classified.Disposition);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(classified.Capture);
        Assert.Equal(staged.Pending!.PacketFingerprint, classified.Pending!.PacketFingerprint);
        if (pendingState != "match")
        {
            var denied = await capture.ReadC2NextSourceAsync(lease);
            Assert.Null(denied.Source);
            Assert.NotEmpty(denied.Issues);
        }
    }

    /// <summary>
    /// Refuses to replay a different checkpoint than the pair classifier first observed.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2SavedReplay_RejectsChangedClassifierOrigin()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var initiallyObserved = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath);
        var staged = await StageC2SavedDecisionAsync(context, lease, materialize: false);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath,
            Encoding.UTF8.GetBytes(SpiritualWoundCaptureCheckpointState.SerializeCanonical(staged.Checkpoint!)));
        var reopened = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

        var replayed = await reopened.ReplaySavedSpiritualCheckpointAsync(lease, initiallyObserved);

        Assert.Null(replayed.Capture);
        Assert.Contains(replayed.Issues,
            issue => issue.Code == "spiritual_c2_saved_checkpoint_changed");
    }

    /// <summary>
    /// Saves two decisions in one exchange and replays their exact layered prose and packet cursor.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2SavedDecision_ReplaysTwoSameExchangeDecisions()
    {
        await using var context = await CreateTwoSourceC2ContextAsync();
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        const string correctedNarrative =
            "{\"response\":\"Оба противника ощутили силу одного столкновения.\",\"timestamp\":\"2026-01-01T00:00:00Z\"}";
        var first = await StageC2SavedDecisionAsync(context, lease,
            materialize: false, correctedNarrative: correctedNarrative);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath,
            Encoding.UTF8.GetBytes(SpiritualWoundCaptureCheckpointState.SerializeCanonical(first.Checkpoint!)));
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath,
            Encoding.UTF8.GetBytes(SpiritualWoundDecisionPendingState.SerializeCanonical(first.Pending!)));
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var classified = await fresh.ClassifySpiritualPendingAsync(lease);
        Assert.Equal("match", classified.Disposition);
        using var cold = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(classified.Capture);
        var next = await cold.ReadC2NextSourceAsync(lease);
        AssertNoConflictFrameErrors(next.Issues);
        var admitted = await cold.AdmitWoundSourceAsync(lease, next.Interval!, next.Source!);
        AssertNoConflictFrameErrors(admitted.Issues);
        var offered = await cold.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(offered.Issues);
        var decline = JsonSerializer.SerializeToElement(new
        {
            opportunityRef = offered.Opportunity!.PublicRef,
            decision = "none"
        });
        var command = WoundResponseInputComposer.Compose(offered.Binding!,
            [offered.Opportunity], [decline], null, []);
        Assert.True(command.Success);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            AcceptedMechanicsPlan.WoundCommandPath,
            Encoding.UTF8.GetBytes(command.CommandRoot!.ToJsonString()));

        var second = await cold.AdvanceC2NextDecisionDraftAsync(lease);

        AssertNoConflictFrameErrors(second.Issues);
        Assert.Equal(2, second.Checkpoint!.CommittedAdvance);
        var packet = JsonNode.Parse(SpiritualWoundDecisionPendingState.SerializeCanonical(second.Pending!))!
            ["pending"]!.AsObject();
        Assert.Equal(2, packet["stagedDecisions"]!.AsArray().Count);
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes(correctedNarrative)),
            packet["preservedDraft"]!["contentBase64"]!.GetValue<string>());
        var narrativeCandidate = packet["candidateAfterImages"]!.AsArray().Single(row =>
            row!["path"]!.GetValue<string>() == ProjectionNarrativePath)!;
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes(correctedNarrative)),
            narrativeCandidate["contentBase64"]!.GetValue<string>());
        cold.Dispose();
        var transportPair = await new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance).ClassifySpiritualPendingAsync(lease);
        Assert.Equal("match", transportPair.Disposition);
        using var transportOwner = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(
            transportPair.Capture);
        var transported = await transportOwner.CommitC2SavedTransportAsync(lease);
        AssertNoConflictFrameErrors(transported.Issues);
        Assert.Equal("committed", transported.Disposition);
        Assert.Equal(2, transported.Checkpoint!.CommittedAdvance);
        transportOwner.Dispose();
        context.FileSystem.DeleteFile(lease, SpiritualWoundDecisionPendingState.StatePath);
        var reopened = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

        var replayed = await reopened.ReplaySavedSpiritualCheckpointAsync(lease);

        AssertNoConflictFrameErrors(replayed.Issues);
        using var replayCapture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(replayed.Capture);
        Assert.Equal(transported.Pending!.PacketFingerprint, replayed.Pending!.PacketFingerprint);
    }

    /// <summary>
    /// Stages one physical GM command against a real first C2 pair for saved-replay controls.
    /// </summary>
    /// <param name="context">
    /// Signed conflict-frame fixture with its committed initial pair.
    /// </param>
    /// <param name="lease">
    /// Active canonical lease shared with the test's physical reads.
    /// </param>
    /// <param name="materialize">
    /// Whether to author a wound proposal or an explicit decline.
    /// </param>
    /// <param name="correctedNarrative">
    /// Optional valid prose correction saved together with this decision.
    /// </param>
    /// <returns>
    /// Strict unpublished checkpoint and pending successor.
    /// </returns>
    private static async Task<ValidationService.SpiritualC2DecisionDraftResult> StageC2SavedDecisionAsync(
        ResourceMaterializationTestContext context, FileSystemManager.CanonicalWriteLease lease,
        bool materialize, string? correctedNarrative = null)
    {
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var classified = await fresh.ClassifyInitialSpiritualPendingAsync(lease);
        Assert.Equal("match", classified.Disposition);
        using var cold = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(classified.Capture);
        var next = await cold.ReadC2NextSourceAsync(lease);
        AssertNoConflictFrameErrors(next.Issues);
        var admitted = await cold.AdmitWoundSourceAsync(lease, next.Interval!, next.Source!);
        AssertNoConflictFrameErrors(admitted.Issues);
        var offered = await cold.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(offered.Issues);
        var decision = materialize
            ? OriginalSpiritualWoundDecision(offered.Opportunity!.PublicRef)
            : JsonSerializer.SerializeToElement(new
            {
                opportunityRef = offered.Opportunity!.PublicRef,
                decision = "none"
            });
        var scene = materialize ? "Чужое давление надломило волю хранителя." : null;
        var command = WoundResponseInputComposer.Compose(offered.Binding!,
            [offered.Opportunity], [decision], scene, []);
        Assert.True(command.Success);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            AcceptedMechanicsPlan.WoundCommandPath,
            Encoding.UTF8.GetBytes(command.CommandRoot!.ToJsonString()));
        if (correctedNarrative is not null)
            await context.FileSystem.WriteFileAtomicBytesAsync(lease,
                ProjectionNarrativePath, Encoding.UTF8.GetBytes(correctedNarrative));
        var staged = await cold.AdvanceC2NextDecisionDraftAsync(lease);
        AssertNoConflictFrameErrors(staged.Issues);
        return staged;
    }
}
