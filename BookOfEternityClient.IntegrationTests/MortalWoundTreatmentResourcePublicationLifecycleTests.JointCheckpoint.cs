using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("exact")]
    [InlineData("unknown_field")]
    [InlineData("draft_hash")]
    [InlineData("binding")]
    [InlineData("fingerprint")]
    public async Task GuaranteedTreatment_ComponentJointCheckpointRetainsGenuineProvenanceOrRefusesTampering(string mutation)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        IDisposable? sourceInput = null;
        QteStoryRun source;
        try
        {
            source = await RunActualQteStorySourceAsync(physicalDeclaration: true, physicalMaximum: 2,
                retainLiveInput: input => sourceInput = input,
                configureBeforePreparation: ConfigureGenuinePendingTreatmentWorldAsync);
        }
        finally { sourceInput?.Dispose(); }
        Assert.Null(source.Failure);
        Assert.True(source.ActionObserved);
        Assert.NotNull(source.Source);
        await AssertGenuinePendingQteSourceAsync(source); // Actual effect-free source10->5, not the old160->155 world.
        var occurrence = await AssertActualPhysicalOccurrenceAsync(source, 2);

        var input = new QueuedConsoleInputSource([]);
        var logger = new SpiritualLifecycleTestLogger();
        var entered = false;
        var engine = CreateGameEngine(input, settings => { InertRealmSettings(settings); settings.AutosaveIntervalTurns = 0; },
            finalizationHooks: new GameEngineSessionFinalizationHooks
            {
                AtCheckpointAsync = checkpoint =>
                {
                    if (checkpoint == SessionFinalizationCheckpoint.LateTerminalAndIdleOperationBound) entered = true;
                    return Task.CompletedTask;
                }
            }, logger: logger);
        GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(source.Source!.SessionId, 42);
        await GetPrivateField<StateManager>(engine, "_stateManager").RefreshGameStateAsync();
        await SessionOperationContext.RunParticipatingCurrentSessionAsync(_fs, async () =>
        {
            Assert.NotNull(await InvokePrivateTaskNullableResultAsync(engine, "StagePlayerTurnAsync", "Осмотреть ушиб.", null, null));
            return true;
        });

        var contextResult = MortalWoundTreatmentAuthority.ParseContext(new JsonObject
        {
            ["schemaVersion"] = 1, ["realm"] = "mortal_world", ["targetKind"] = "player",
            ["targetId"] = "player_current", ["providerKind"] = "npc", ["providerId"] = "field_medic_01",
            ["currentLocationId"] = "loc_field_clinic_001"
        }.ToJsonString(), "treatmentContext");
        Assert.True(contextResult.IsValid, DescribeValidationIssues(contextResult.Issues));
        var context = Assert.IsType<MortalWoundTreatmentAuthority.Context>(contextResult.Context);
        var scene = HeldTreatmentPipelineContext.FinalSceneText + " " + MortalComponentAcquisition;
        MortalWoundTreatmentAttemptRequest request;
        MortalWoundTreatmentResourceFinalization finalization;
        WoundResponseInputCompositionResult composition;
        PendingTurnSnapshotReadAuthority original;
        JsonObject decision;
        string generation;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            generation = Assert.IsType<string>(_fs.ReadLocalGenerationSnapshot(lease).Binding.Id);
            original = MortalQteWoundOpportunityProjection.ReadSnapshot(_fs, lease);
            Assert.Equal(43, original.TurnNumber);
            Assert.Equal(source.Source.SessionId, original.SessionId);
            Assert.NotEqual(source.Source.RequestId, original.RequestId);
            Assert.NotEqual(source.SnapshotToken, original.SnapshotToken);
            Assert.Equal("mortal_world", original.Realm);
            var offer = Assert.Single(MortalQteWoundOpportunityProjection.Project(original));
            Assert.Equal(occurrence.OpportunityRef, offer["opportunityRef"]!.GetValue<string>());
            Assert.Equal("II", offer["maximumSeverity"]!.GetValue<string>());
            var exported = MortalWoundTreatmentAcceptedStateAuthority.ExportCurrent(_fs, lease, context, HeldTreatmentPipelineContext.WoundId);
            Assert.True(exported.IsValid, DescribeValidationIssues(exported.Issues));
            var accepted = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(exported.Authority);
            AssertPendingTreatmentTuple(original, accepted.Binding);
            var history = ReadCurrentTreatmentHistory(_fs);
            var wound = ReadCurrentTreatmentWound(_fs);
            var prepared = MortalWoundTreatmentPlanner.PrepareGuaranteedRequest(accepted, history, wound,
                "operation_fr063_live_repair", HeldTreatmentPipelineContext.RouteId,
                Assert.Single(accepted.Binding.AcceptedEvents).EventRef);
            Assert.True(prepared.IsValid, DescribeValidationIssues(prepared.Issues));
            request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(prepared.Request);
            Assert.Equal(2m, Assert.Single(request.ResourceAuthority.Claims).Quantity);
            var resolved = MortalWoundTreatmentPlanner.CreateGuaranteedAttempt(request, history, wound, accepted);
            Assert.Equal("Resolved", resolved.Disposition);
            Assert.Empty(resolved.Issues);
            var resolution = Assert.IsType<MortalWoundTreatmentResolution>(resolved.Resolution);
            var finalized = MortalWoundTreatmentResourceComposer.Finalize(resolution);
            Assert.True(finalized.IsValid, DescribeValidationIssues(finalized.Issues));
            finalization = Assert.IsType<MortalWoundTreatmentResourceFinalization>(finalized.Finalization);
            var command = WoundResponseInputComposer.ComposeMortalWoundTreatmentCommandRoot(accepted.Binding, resolution, scene);
            var parsed = WoundResponseInputComposer.ParseCommandRoot(JsonSerializer.SerializeToElement(command));
            Assert.True(parsed.Success, DescribeValidationIssues(parsed.Issues));
            composition = WoundResponseInputComposer.RecomposeCommandRoot(accepted.Binding, parsed, Array.Empty<WoundOpportunityDecisionReceipt>());
            Assert.True(composition.Success, DescribeValidationIssues(composition.Issues));
            decision = new JsonObject { ["opportunityRef"] = occurrence.OpportunityRef, ["decision"] = "materialize",
                ["woundRef"] = "live_pending_impact", ["proposal"] = CreateMortalComponentProposal() };
            decision["proposal"]!["severity"] = "III"; // Only this public severity leaf contradicts the genuine maximumII.
        }
        await new StateDistributor(_fs, NullLogger<StateDistributor>.Instance)
            .DistributeAsync(new GameResponse { Response = scene }, composition);

        Dictionary<string, byte[]?> before;
        JsonObject heldCommand;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.Equal(generation, _fs.ReadLocalGenerationSnapshot(lease).Binding.Id);
            var confirmed = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(_fs, lease, request, finalization);
            WriteConfirmedClaimInspection("live_repair_before_actual_entry", confirmed);
            Assert.True(confirmed.IsValid, DescribeValidationIssues(confirmed.Issues));
            Assert.Equal(MortalWoundTreatmentResourceReservationState.ConfirmedHeld, confirmed.State);
            Assert.Equal(request.RequestFingerprint, confirmed.RequestFingerprint);
            Assert.Null(confirmed.GenerationFingerprint); // This read is diagnostic, not publication authority.
            var definitions = ResourceDefinitionCatalog.ParseCanonical(await _fs.ReadFileAsync(lease, ResourceMaterializationContract.DefinitionsPath), false);
            Assert.True(definitions.IsValid, DescribeValidationIssues(definitions.Issues));
            var resources = ResourceStateContract.ParseCanonical(await _fs.ReadFileAsync(lease, ResourceMaterializationContract.StatePath), definitions.Catalog!, false);
            Assert.True(resources.IsValid, DescribeValidationIssues(resources.Issues));
            Assert.Equal(2m, Assert.Single(resources.Ledger!.Entries, entry => entry.Coordinate.ResourceKey == "energy").Current);
            Assert.Equal(5m, Assert.Single(resources.Ledger.Entries, entry => entry.Coordinate.ResourceKey == "health").Current);
            Assert.Single(ReadCurrentTreatmentHistory(_fs).State!.Transitions);
            Assert.Equal("untreated", ReadCurrentTreatmentWound(_fs).Care.State);
            heldCommand = ParseJsonObjectBytes(Assert.IsType<byte[]>(await _fs.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath)));
            var parsed = WoundResponseInputComposer.ParseCommandRoot(JsonSerializer.SerializeToElement(heldCommand));
            Assert.True(parsed.Success, DescribeValidationIssues(parsed.Issues));
            Assert.Empty(parsed.Commands);
            var treatment = Assert.Single(parsed.TreatmentCommands);
            Assert.Equal(request.RequestFingerprint, treatment.RequestFingerprint);
            Assert.True(JsonNode.DeepEquals(WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(request), treatment.Request));
            Assert.Equal(scene, treatment.FinalSceneText);
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(_fs, lease));
            await WriteLiveRepairOrdinaryOutputsAsync(lease, original, decision, scene);
            before = await CaptureConfirmedClaimReadImagesAsync(_fs, lease);
        }

        await using var captureLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        var captured = await MortalJointRepairCheckpoint.CaptureAsync(_fs, captureLease,
            original.SessionId, original.RequestId, original.SnapshotToken, 43);
        WritePhysicalInjuryDiagnostic("fr063_joint_checkpoint_capture", new
        {
            mutation, captured.IsValid, captured.Issues, Checkpoint = captured.Checkpoint?.ToJsonObject(),
            Original = new { original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber },
            generation, request.RequestFingerprint, request.ResourceAuthority.ReservationId,
            ComponentOnly = true, LiveRestoreDispatchQualified = false, JointPublicationQualified = false
        });
        Assert.True(captured.IsValid, DescribeValidationIssues(captured.Issues));
        var rejectedCapture = Assert.IsType<MortalJointRepairCheckpoint.RejectedCapture>(captured.Capture);
        rejectedCapture.EnsureCurrent(_fs, captureLease);
        var checkpoint = Assert.IsType<MortalJointRepairCheckpoint>(captured.Checkpoint);
        var root = checkpoint.ToJsonObject();
        Assert.Equal(generation, root["sessionGeneration"]!.GetValue<string>());
        Assert.Equal(request.RequestFingerprint, root["requestFingerprint"]!.GetValue<string>());
        Assert.Equal(occurrence.OpportunityRef, root["opportunityRef"]!.GetValue<string>());
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(before[AcceptedMechanicsPlan.WoundCommandPath]!)), root["commandHash"]!.GetValue<string>());
        Assert.Equal(before[MortalWoundOpportunityAdapter.DecisionPath], Convert.FromBase64String(root["rejectedDraftBase64"]!.GetValue<string>()));
        Assert.Equal(before["output/narrative_response.json"], Convert.FromBase64String(root["narrativeBase64"]!.GetValue<string>()));
        Assert.Equal(1, root["packetFingerprints"]!.AsArray().Count);
        Assert.Equal(1, root["receiptFingerprints"]!.AsArray().Count);
        if (mutation == "unknown_field") root["allowMixed"] = true;
        if (mutation == "draft_hash") root["rejectedDraftHash"] = new string('0', 64);
        if (mutation == "binding") root["requestId"] = "foreign_original";
        if (mutation == "fingerprint") root["fingerprint"] = "sha256:" + new string('0', 64);
        var parsedCheckpoint = MortalJointRepairCheckpoint.Parse(JsonSerializer.SerializeToElement(root));
        Assert.Null(parsedCheckpoint.Capture); // A parsed seal never issues a lease-local rejected-source proof.
        if (mutation == "exact")
        {
            Assert.True(parsedCheckpoint.IsValid, DescribeValidationIssues(parsedCheckpoint.Issues));
            Assert.True(JsonNode.DeepEquals(checkpoint.ToJsonObject(), parsedCheckpoint.Checkpoint!.ToJsonObject()));
        }
        else
        {
            Assert.False(parsedCheckpoint.IsValid);
            Assert.Null(parsedCheckpoint.Checkpoint);
            var expectedCode = mutation == "unknown_field" ? "mortal_joint_checkpoint_shape" :
                mutation == "draft_hash" ? "mortal_joint_checkpoint_blob" : "mortal_joint_checkpoint_fingerprint";
            Assert.Contains(parsedCheckpoint.Issues, issue => issue.Code == expectedCode);
        }
        await AssertConfirmedClaimReadImagesAsync(_fs, captureLease, before);
        var stillHeld = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(_fs, captureLease, request, finalization);
        Assert.True(stillHeld.IsValid, DescribeValidationIssues(stillHeld.Issues));
        Assert.Equal(MortalWoundTreatmentResourceReservationState.ConfirmedHeld, stillHeld.State);
        Assert.Null(stillHeld.GenerationFingerprint);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(_fs, captureLease));
        Assert.False(_fs.FileExists(captureLease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath));
    }
}
