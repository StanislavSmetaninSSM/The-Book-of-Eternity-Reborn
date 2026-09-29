using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Reads wound and effect changes from their shared carrier after a selected spiritual wound.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC1CandidateOwnerImages_ComposeSelectedWoundAndEffectCarrier()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteOriginalIntakeDraftAsync(context);
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
        var offered = await capture.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(offered.Issues);
        var decision = OriginalSpiritualWoundDecision(offered.Opportunity!.PublicRef);
        var result = await capture.MaterializeWoundAsync(lease, admitted.Admission!, decision,
            "Чужое давление надломило волю хранителя.");
        AssertNoConflictFrameErrors(result.Issues);
        var wound = Assert.IsType<WoundMaterializationEnvelope>(result.Wound);
        var binding = Assert.Single(wound.Consequences.OwnedEffectSources.RootBindings);

        var images = await capture.ReadC1CandidateOwnerImagesAsync(lease, interval);
        var profile = JsonNode.Parse(Encoding.UTF8.GetString(images[
            AfterlifeEntityProfileState.StatePath].Bytes!))!.AsObject();
        var guardian = Assert.Single(profile[AfterlifeEntityProfileState.ProfilesProperty]!
            .AsArray().OfType<JsonObject>(), value =>
                (string?)value["actorId"] == "guardian_frame");
        Assert.Contains(guardian["activeWounds"]!.AsArray(), value =>
            (string?)value?["woundId"] == wound.WoundId);
        Assert.Contains(guardian["activeEffects"]!.AsArray(), value =>
            (string?)value?["effectId"] == binding.EffectId);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    /// <summary>
    /// Accepts a projected conflict companion when the original draft uses its response wrapper.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC1CandidateOwnerImages_AcceptProjectedConflictCompanion()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteOriginalIntakeDraftAsync(context);
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

        var images = await capture.ReadC1CandidateOwnerImagesAsync(lease, interval);
        var conflict = JsonNode.Parse(Encoding.UTF8.GetString(images[
            AfterlifeSpiritualConflictState.StatePath].Bytes!))!.AsObject();
        Assert.Single(Assert.IsType<JsonArray>(conflict["activeConflict"]!["exchangeLog"]));
    }

    /// <summary>
    /// Composes only owned first-exchange candidate roots without promoting the future draft exchange.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC1CandidateOwnerImages_UseClosedPrefixAndRemainDetached()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        await WriteOriginalIntakeDraftAsync(context);
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

        var images = await capture.ReadC1CandidateOwnerImagesAsync(lease, interval);
        var conflict = JsonNode.Parse(Encoding.UTF8.GetString(images[
            AfterlifeSpiritualConflictState.StatePath].Bytes!))!.AsObject();
        var log = Assert.IsType<JsonArray>(conflict["activeConflict"]!["exchangeLog"]);
        Assert.Single(log);
        Assert.Equal("exchange_conflict_frame_42", (string?)log[0]?["exchangeId"]);
        Assert.False(images.ContainsKey(SpiritualWoundDecisionPendingState.StatePath));
        Assert.False(images.ContainsKey(SpiritualWoundCaptureCheckpointState.StatePath));
        var resource = capture.ReadClosedResourcePrefix(lease, interval);
        Assert.Equal(resource.StateJson, Encoding.UTF8.GetString(images[
            ResourceMaterializationContract.StatePath].Bytes!));
        Assert.Equal(resource.HistoryJson, Encoding.UTF8.GetString(images[
            ResourceMaterializationContract.HistoryPath].Bytes!));
        var bytes = images[AfterlifeSpiritualConflictState.StatePath].Bytes!;
        bytes[0] ^= 1;
        var reread = await capture.ReadC1CandidateOwnerImagesAsync(lease, interval);
        Assert.Equal(images[AfterlifeSpiritualConflictState.StatePath].Fingerprint,
            reread[AfterlifeSpiritualConflictState.StatePath].Fingerprint);

        var next = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(next.Issues);
        Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(next.Step?.Interval);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await capture.ReadC1CandidateOwnerImagesAsync(lease, interval));
    }
}
