using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Prevents a consumed source projection from authorizing another seal after its allocation owner faults.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualCapture_FaultRevokesConsumedCompletion()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = captured.Capture!;
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(advanced.Issues);
        var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
        var admitted = await capture.AdmitWoundSourceAsync(lease, advanced.Step!.Interval!, Assert.Single(source.Sources));
        AssertNoConflictFrameErrors(admitted.Issues);
        var offered = await capture.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(offered.Issues);
        var decline = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            opportunityRef = offered.Opportunity!.PublicRef, decision = "none"
        });
        AssertNoConflictFrameErrors((await capture.MaterializeWoundAsync(lease, admitted.Admission!, decline, null)).Issues);
        var completed = await capture.CompleteEffectsAsync(lease);
        Assert.True(completed.Success, string.Join(Environment.NewLine, completed.Issues));
        var resources = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(OriginalCaptureField(capture, "_resources"));
        var effects = Assert.IsType<EffectAcceptedTurnPlanner.EffectAcceptedDraft>(OriginalCaptureField(capture, "_effects"));
        var prepared = await source.PrepareContinuationAsync(lease);
        AssertNoConflictFrameErrors(prepared.Issues);
        var completion = prepared.Ticket!.ValidateCompletion(source, lease, source.CheckedExchanges.Count, resources, effects);
        Assert.True(completion.Success, string.Join(Environment.NewLine, completion.Issues));
        var projection = completion.Projection!;
        Assert.Empty(projection.Consume(source, lease, source.CheckedExchanges.Count, resources, effects, completed.Plan!));
        Assert.True(resources.SealOriginalSpiritualReduction(completed.Plan!, projection).Success);
        var journal = Assert.IsType<SpiritualWoundReplayJournal>(OriginalCaptureProperty(
            OriginalCaptureField(capture, "_allocations")!, "Journal"));
        Assert.Throws<InvalidOperationException>(() => journal.Request("effect", "effects", "outside",
            () => "effect_0123456789abcdef0123456789abcdef"));
        Assert.False(projection.OwnsConsumption(resources, effects, source, completed.Plan!));
        var rejected = resources.SealOriginalSpiritualReduction(completed.Plan!, projection);
        Assert.False(rejected.Success);
        Assert.Contains(rejected.Issues, issue => issue.Code == "spiritual_original_reduction_effect_mismatch");
    }

    /// <summary>
    /// Replays inserted wound effect identities and final completion without publishing canonical state.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualCapture_ReplaysInsertedWoundAndCompletion()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var canonical = new Dictionary<string, byte[]?>();
        foreach (var path in new[] { AfterlifeSpiritualConflictState.StatePath, AfterlifeEntityProfileState.StatePath,
                     EffectAcceptedTurnPlan.IdentityIndexPath, WoundIdentityState.StatePath, WoundHistoryState.HistoryPath })
            canonical.Add(path, await context.FileSystem.ReadFileBytesAsync(lease, path));
        var rows = "[]";
        string? woundId = null;
        string? effectId = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var captured = await ValidationService.SpiritualOriginalTurnCapture.CaptureAsync(
                context.Validator, lease, awaitOriginalPrefix: true, allocationJournalJson: rows,
                replayAllocations: attempt == 1);
            AssertNoConflictFrameErrors(captured.Issues);
            using var capture = captured.Capture!;
            AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
            var advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
            AssertNoConflictFrameErrors(advanced.Issues);
            var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
            var admitted = await capture.AdmitWoundSourceAsync(lease, advanced.Step!.Interval!, Assert.Single(source.Sources));
            AssertNoConflictFrameErrors(admitted.Issues);
            var offered = await capture.ReadWoundOpportunityAsync(lease, admitted.Admission!);
            AssertNoConflictFrameErrors(offered.Issues);
            var decision = OriginalSpiritualWoundDecision(offered.Opportunity!.PublicRef);
            const string scene = "Чужое давление надломило волю хранителя.";
            var inserted = await capture.MaterializeWoundAsync(lease, admitted.Admission!, decision, scene);
            AssertNoConflictFrameErrors(inserted.Issues);
            Assert.Same(inserted, await capture.MaterializeWoundAsync(lease, admitted.Admission!, decision, scene));
            var wound = Assert.IsType<WoundMaterializationEnvelope>(inserted.Wound);
            var root = Assert.Single(wound.Consequences.OwnedEffectSources.RootBindings);
            var completed = await capture.CompleteOrdinaryReductionAsync(lease);
            Assert.True(completed.Success, string.Join(Environment.NewLine, completed.Issues));
            Assert.Same(completed, await capture.CompleteOrdinaryReductionAsync(lease));
            var observedRows = capture.ReadAllocationJournal(lease).ToJsonString();
            if (attempt == 0)
            {
                rows = observedRows;
                woundId = wound.WoundId;
                effectId = root.EffectId;
                Assert.Contains(JsonNode.Parse(rows)!.AsArray(), row => row!["kind"]!.GetValue<string>() == "effect");
            }
            else
            {
                Assert.Equal(rows, observedRows);
                Assert.Equal(woundId, wound.WoundId);
                Assert.Equal(effectId, root.EffectId);
            }
        }
        foreach (var pair in canonical)
            Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
    }

    /// <summary>
    /// Reconstructs actual initial vehicle allocation and chronological resource execution from one capture journal.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualCapture_ReplaysOwnedAllocationsThroughExchange()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await context.WriteExactJsonAsync(StorageTransportMoveService.VehiclesPath, new JsonObject
        {
            ["vehicles"] = new JsonArray(),
            ["UpdateVehicles"] = new JsonArray(new JsonObject
            {
                ["vehicleRef"] = "vehicle_ref_capture_replay",
                ["name"] = "Телега", ["availability"] = "Parked",
                ["currentLocationId"] = "location_market",
                ["resourceMaterialization"] = new JsonObject
                {
                    ["resources"] = new JsonArray(new JsonObject
                    {
                        ["resourceKey"] = "health", ["maximum"] = 140
                    })
                }
            })
        }.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await ValidationService.SpiritualOriginalTurnCapture.CaptureAsync(
            context.Validator, lease, awaitOriginalPrefix: true);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var first = recorded.Capture!;
        AssertNoConflictFrameErrors(await first.BeginResourceExecutionAsync(lease));
        AssertNoConflictFrameErrors((await first.AdvanceNextResourceExchangeAsync(lease)).Issues);
        var rows = first.ReadAllocationJournal(lease).ToJsonString();
        Assert.Contains(JsonNode.Parse(rows)!.AsArray(), row => row!["kind"]!.GetValue<string>() == "vehicle");
        Assert.Contains(JsonNode.Parse(rows)!.AsArray(), row => row!["kind"]!.GetValue<string>() == "resource_operation");
        var firstInput = Assert.IsType<AcceptedMechanicsInput>(OriginalCaptureField(first, "_input"));
        var vehicles = firstInput.PlanningContext!.OwnerCompanionAfterImages[
            StorageTransportMoveService.VehiclesPath].ToJsonString();
        var replayed = await ValidationService.SpiritualOriginalTurnCapture.CaptureAsync(
            context.Validator, lease, awaitOriginalPrefix: true, allocationJournalJson: rows, replayAllocations: true);
        AssertNoConflictFrameErrors(replayed.Issues);
        using var second = replayed.Capture!;
        Assert.False(first.IsCurrentOwner);
        AssertNoConflictFrameErrors(await second.BeginResourceExecutionAsync(lease));
        AssertNoConflictFrameErrors((await second.AdvanceNextResourceExchangeAsync(lease)).Issues);
        Assert.Equal(rows, second.ReadAllocationJournal(lease).ToJsonString());
        var secondInput = Assert.IsType<AcceptedMechanicsInput>(OriginalCaptureField(second, "_input"));
        Assert.Equal(vehicles, secondInput.PlanningContext!.OwnerCompanionAfterImages[
            StorageTransportMoveService.VehiclesPath].ToJsonString());
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }
}
