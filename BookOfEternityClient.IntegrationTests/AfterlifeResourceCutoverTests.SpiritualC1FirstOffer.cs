using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Retains a closed training source and dice without opening a decision packet.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC1FirstOffer_ZeroCeilingProducesNoPacket()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        var conflict = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            AfterlifeSpiritualConflictState.StatePath));
        conflict["activeConflict"]!["dangerMode"] = "training";
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath,
            conflict.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 42, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 12, 8]);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteOriginalIntakeDraftAsync(context);
        await context.WriteExactBytesAsync(ProjectionNarrativePath,
            Encoding.UTF8.GetBytes("{\"response\":\"Тренировочный обмен завершён.\"}"));
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var first = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(first.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(first.Step?.Interval);

        var offered = await capture.ComposeC1FirstOfferAsync(lease, interval);

        Assert.Empty(offered.Issues);
        Assert.Null(offered.Pending);
        Assert.Null(offered.ProposedInstanceRow);
        var checkpoint = await capture.ComposeC2FirstCheckpointDraftAsync(lease, interval);
        Assert.Empty(checkpoint.Issues);
        Assert.Null(checkpoint.Checkpoint);
        Assert.Null(checkpoint.Pending);
        var transport = await capture.CommitC2FirstTransportAsync(lease, interval);
        Assert.Empty(transport.Issues);
        Assert.Equal("no_offer", transport.Disposition);
        Assert.False(context.FileSystem.FileExists(lease, SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.False(context.FileSystem.FileExists(lease, SpiritualWoundDecisionPendingState.StatePath));
    }

    /// <summary>
    /// Builds the first detached decision packet from a real closed named exchange.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC1FirstOffer_UsesOwnedExchangeAndPreservesSignedOrigin()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        const string dynamicPath = "game_state/custom/c1_first_offer_registered.json";
        await context.WriteExactJsonAsync(dynamicPath, "{\"registered\":true}");
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteOriginalIntakeDraftAsync(context);
        await context.WriteExactBytesAsync(ProjectionNarrativePath,
            Encoding.UTF8.GetBytes("{\"response\":\"Духовный обмен завершён.\"}"));
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var first = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(first.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(first.Step?.Interval);
        var initialInventory = capture.ReadC1ImageInventory(lease);
        var uncovered = initialInventory.RegisteredPaths
            .Where(path => !initialInventory.BeforeImages.ContainsKey(path)).ToArray();
        Assert.True(uncovered.Length == 0, string.Join(", ", uncovered));

        var offered = await capture.ComposeC1FirstOfferAsync(lease, interval);

        AssertNoConflictFrameErrors(offered.Issues);
        var pending = Assert.IsType<SpiritualWoundDecisionPendingState>(offered.Pending);
        var root = JsonNode.Parse(SpiritualWoundDecisionPendingState.SerializeCanonical(pending))!.AsObject();
        var packet = Assert.IsType<JsonObject>(root["pending"]);
        Assert.Equal(1, (int?)packet["continuationGeneration"]);
        Assert.Equal(interval.ConflictId, (string?)packet["sources"]![0]!["conflictId"]);
        Assert.Equal(interval.ExchangeId, (string?)packet["sources"]![0]!["exchangeId"]);
        Assert.Empty(Assert.IsType<JsonArray>(packet["stagedDecisions"]));
        Assert.Equal(capture.ReadVerifiedSignedC1Origin(lease).SnapshotFingerprint,
            (string?)packet["originalSnapshotFingerprint"]);
        var registered = capture.ReadC1ImageInventory(lease).RegisteredPaths;
        Assert.Equal(registered.OrderBy(path => path, StringComparer.Ordinal),
            packet["beforeImages"]!.AsArray().Select(row => (string?)row!["path"])
                .OrderBy(path => path, StringComparer.Ordinal));
        var repeated = await capture.ComposeC1FirstOfferAsync(lease, interval);
        AssertNoConflictFrameErrors(repeated.Issues);
        Assert.Equal(SpiritualWoundDecisionPendingState.SerializeCanonical(pending),
            SpiritualWoundDecisionPendingState.SerializeCanonical(repeated.Pending!));
        Assert.True(JsonNode.DeepEquals(offered.ProposedInstanceRow, repeated.ProposedInstanceRow));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        Assert.False(context.FileSystem.FileExists(lease, SpiritualWoundDecisionPendingState.StatePath));

        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            dynamicPath, Encoding.UTF8.GetBytes("{\"registered\":false}"));
        var changedPhysical = await capture.ComposeC1FirstOfferAsync(lease, interval);
        Assert.Null(changedPhysical.Pending);
        Assert.NotEmpty(changedPhysical.Issues);
    }

    /// <summary>
    /// Rejects a changed signed manifest before exposing another first offer.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC1FirstOffer_RejectsChangedSignedManifest()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteOriginalIntakeDraftAsync(context);
        await context.WriteExactBytesAsync(ProjectionNarrativePath,
            Encoding.UTF8.GetBytes("{\"response\":\"Духовный обмен завершён.\"}"));
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var first = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(first.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(first.Step?.Interval);

        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
            Encoding.UTF8.GetBytes("{}"));
        var changedSigned = await capture.ComposeC1FirstOfferAsync(lease, interval);

        Assert.Null(changedSigned.Pending);
        Assert.NotEmpty(changedSigned.Issues);
        Assert.False(context.FileSystem.FileExists(lease, SpiritualWoundDecisionPendingState.StatePath));
    }

    /// <summary>
    /// Rejects a first packet once a retained source has already received a legacy decision.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC1FirstOffer_RejectsAlreadySelectedSource()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteOriginalIntakeDraftAsync(context);
        await context.WriteExactBytesAsync(ProjectionNarrativePath,
            Encoding.UTF8.GetBytes("{\"response\":\"Духовный обмен завершён.\"}"));
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var first = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(first.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(first.Step?.Interval);
        var sourceOwner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(
            OriginalCaptureField(capture, "_source"));
        var admitted = await capture.AdmitWoundSourceAsync(lease, interval,
            Assert.Single(sourceOwner.Sources));
        AssertNoConflictFrameErrors(admitted.Issues);
        var opportunity = await capture.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(opportunity.Issues);
        var decision = OriginalSpiritualWoundDecision(opportunity.Opportunity!.PublicRef);
        var selected = await capture.MaterializeWoundAsync(lease, admitted.Admission!, decision,
            "Чужое давление надломило волю хранителя.");
        AssertNoConflictFrameErrors(selected.Issues);
        Assert.NotNull(selected.Wound);

        var offered = await capture.ComposeC1FirstOfferAsync(lease, interval);

        Assert.Null(offered.Pending);
        Assert.Contains(offered.Issues,
            issue => issue.Code == "spiritual_first_offer_already_decided");
        Assert.False(context.FileSystem.FileExists(lease, SpiritualWoundDecisionPendingState.StatePath));
    }
}
