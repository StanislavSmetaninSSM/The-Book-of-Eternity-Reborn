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
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuaranteedTreatment_ComponentGenuinePendingWaveCoalescesExactRequestOrRefusesChangedCopy(bool changed)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var source = await RunActualQteStorySourceAsync(physicalDeclaration: true, physicalMaximum: 2,
            configureBeforePreparation: ConfigureGenuinePendingTreatmentWorldAsync);
        Assert.Null(source.Failure);
        Assert.True(source.ActionObserved);
        Assert.NotNull(source.Source);
        // The shared qualified oracle deliberately stays 160->155. This separate world is 10->5.
        await AssertGenuinePendingQteSourceAsync(source);
        var occurrence = await AssertActualPhysicalOccurrenceAsync(source, 2);
        foreach (var path in new[] { "input/turn_request.json", LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                     PendingTurnSnapshotAuthority.AuthorityPath, QteSceneService.QteOfferPath,
                     "ready/turn_complete.json", "ready/error.json" })
            Assert.False(_fs.FileExists(path), path);

        var input = new QueuedConsoleInputSource([]);
        string sourceGeneration;
        await using (var sourceLease = await _fs.AcquireCanonicalWriteLeaseAsync())
            sourceGeneration = Assert.IsType<string>(_fs.ReadLocalGenerationSnapshot(sourceLease).Binding.Id);
        var engine = CreateGameEngine(input, settings => { InertRealmSettings(settings); settings.AutosaveIntervalTurns = 0; });
        GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(source.Source.SessionId, 42);
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
        MortalWoundTreatmentResolution resolution;
        WoundResponseInputCompositionResult composition;
        WoundAcceptedTurnBinding binding;
        PendingTurnSnapshotReadAuthority snapshot;
        JsonObject decision;
        string generation;
        long generationRevision;
        Dictionary<string, byte[]?> beforeDistribution;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            var physicalGeneration = _fs.ReadLocalGenerationSnapshot(lease);
            generation = physicalGeneration.Binding.Id!;
            generationRevision = _fs.CanonicalRootAuthorityIdentity.SessionGenerationRevision;
            Assert.Equal(sourceGeneration, generation);
            snapshot = MortalQteWoundOpportunityProjection.ReadSnapshot(_fs, lease);
            Assert.Equal(43, snapshot.TurnNumber);
            Assert.Equal(source.Source.SessionId, snapshot.SessionId);
            Assert.NotEqual(source.Source.RequestId, snapshot.RequestId);
            Assert.NotEqual(source.SnapshotToken, snapshot.SnapshotToken);
            Assert.Equal("mortal_world", snapshot.Realm);
            var offer = Assert.Single(MortalQteWoundOpportunityProjection.Project(snapshot));
            Assert.Equal(occurrence.OpportunityRef, offer["opportunityRef"]!.GetValue<string>());
            var exported = MortalWoundTreatmentAcceptedStateAuthority.ExportCurrent(_fs, lease, context, HeldTreatmentPipelineContext.WoundId);
            Assert.True(exported.IsValid, DescribeValidationIssues(exported.Issues));
            var accepted = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(exported.Authority);
            binding = accepted.Binding;
            AssertPendingTreatmentTuple(snapshot, accepted.Binding);
            var history = ReadCurrentTreatmentHistory(_fs);
            var wound = ReadCurrentTreatmentWound(_fs);
            var prepared = MortalWoundTreatmentPlanner.PrepareGuaranteedRequest(accepted, history, wound,
                "operation_fr063_genuine_pending", HeldTreatmentPipelineContext.RouteId,
                Assert.Single(accepted.Binding.AcceptedEvents).EventRef);
            Assert.True(prepared.IsValid, DescribeValidationIssues(prepared.Issues));
            request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(prepared.Request);
            Assert.Equal(2m, Assert.Single(request.ResourceAuthority.Claims).Quantity);
            var resolved = MortalWoundTreatmentPlanner.CreateGuaranteedAttempt(request, history, wound, accepted);
            Assert.Equal("Resolved", resolved.Disposition);
            Assert.Empty(resolved.Issues);
            resolution = Assert.IsType<MortalWoundTreatmentResolution>(resolved.Resolution);
            var command = WoundResponseInputComposer.ComposeMortalWoundTreatmentCommandRoot(accepted.Binding, resolution, scene);
            var parsed = WoundResponseInputComposer.ParseCommandRoot(JsonSerializer.SerializeToElement(command));
            Assert.True(parsed.Success, DescribeValidationIssues(parsed.Issues));
            composition = WoundResponseInputComposer.RecomposeCommandRoot(accepted.Binding, parsed, Array.Empty<WoundOpportunityDecisionReceipt>());
            Assert.True(composition.Success, DescribeValidationIssues(composition.Issues));
            decision = new JsonObject { ["opportunityRef"] = occurrence.OpportunityRef, ["decision"] = "materialize",
                ["woundRef"] = "pending_impact", ["proposal"] = CreateMortalComponentProposal() };
            decision["proposal"]!["severity"] = "III"; // One lawful public leaf exceeds this actual sealed maximum II.
            beforeDistribution = await CaptureConfirmedClaimReadImagesAsync(_fs, lease);
        }
        // This real homogeneous distributor confirms the request. No common plan/publication is composed.
        await new StateDistributor(_fs, NullLogger<StateDistributor>.Instance)
            .DistributeAsync(new GameResponse { Response = scene }, composition);
        JsonObject commandRoot;
        JsonObject pending;
        Dictionary<string, byte[]?> beforeRecovery;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.Equal(generation, _fs.ReadLocalGenerationSnapshot(lease).Binding.Id);
            var afterDistribution = await CaptureConfirmedClaimReadImagesAsync(_fs, lease);
            foreach (var pair in beforeDistribution.Where(pair => !pair.Key.StartsWith("output/", StringComparison.Ordinal) &&
                         pair.Key != AcceptedMechanicsPlan.WoundCommandPath))
                Assert.Equal(pair.Value, afterDistribution[pair.Key]);
            Assert.Empty(afterDistribution.Keys.Except(beforeDistribution.Keys, StringComparer.Ordinal).Where(path =>
                !path.StartsWith("output/", StringComparison.Ordinal) && path != AcceptedMechanicsPlan.WoundCommandPath));
            commandRoot = ParseJsonObjectBytes(Assert.IsType<byte[]>(await _fs.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath)));
            var parsedCommand = WoundResponseInputComposer.ParseCommandRoot(JsonSerializer.SerializeToElement(commandRoot));
            Assert.True(parsedCommand.Success, DescribeValidationIssues(parsedCommand.Issues));
            var typedCommand = Assert.Single(parsedCommand.TreatmentCommands);
            Assert.Equal(request.RequestFingerprint, typedCommand.RequestFingerprint);
            Assert.Equal(request.Coordinates.OperationKey, typedCommand.OperationKey);
            Assert.Equal(request.Coordinates.AttemptId, typedCommand.AttemptId);
            Assert.True(JsonNode.DeepEquals(WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(request), typedCommand.Request));
            var confirmedFinalized = MortalWoundTreatmentResourceComposer.Finalize(resolution);
            Assert.True(confirmedFinalized.IsValid, DescribeValidationIssues(confirmedFinalized.Issues));
            var confirmed = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(_fs, lease, request,
                Assert.IsType<MortalWoundTreatmentResourceFinalization>(confirmedFinalized.Finalization));
            WriteConfirmedClaimInspection("genuine_pending_after_distribution", confirmed);
            Assert.True(confirmed.IsValid, DescribeValidationIssues(confirmed.Issues));
            Assert.Equal(MortalWoundTreatmentResourceReservationState.ConfirmedHeld, confirmed.State);
            Assert.Equal(request.RequestFingerprint, confirmed.RequestFingerprint);
            Assert.Null(confirmed.GenerationFingerprint);
            await _fs.WriteFileAtomicAsync(lease, MortalWoundOpportunityAdapter.DecisionPath, decision.ToJsonString());
            await _fs.WriteFileAtomicAsync(lease, "output/narrative_response.json", new JsonObject
                { ["response"] = scene, ["timestamp"] = DateTime.UtcNow.ToString("O") }.ToJsonString());
            var beforeAdapter = await CaptureConfirmedClaimReadImagesAsync(_fs, lease);
            var rejected = MortalWoundOpportunityAdapter.ComposeQteAcceptedResponse(_fs, lease, decision.ToJsonString(), scene,
                out var replay, out var opportunity);
            Assert.NotNull(rejected);
            Assert.False(rejected.Success);
            Assert.Null(replay);
            Assert.NotNull(opportunity);
            var actionable = rejected.Issues.Where(issue => issue.Code == "wound_severity_above_opportunity" &&
                issue.WoundRepairContext is not null && WoundRepairPacketBuilder.IsRepairableIssue(issue)).ToArray();
            Assert.NotEmpty(actionable);
            var packets = WoundRepairPacketBuilder.Build(actionable);
            Assert.NotEmpty(packets);
            foreach (var packet in packets)
            {
                Assert.True(packet.MatchesOpportunity(opportunity));
                Assert.True(packet.MatchesRejectedDecision(decision));
                Assert.Equal(snapshot.SessionId, packet.SessionId);
                Assert.Equal(snapshot.RequestId, packet.RequestId);
                Assert.Equal(snapshot.SnapshotToken, packet.SnapshotToken);
            }
            await AssertConfirmedClaimReadImagesAsync(_fs, lease, beforeAdapter);
            // The packet was genuinely issued from the QTE occurrence; the homogeneous command is the real held treatment.
            pending = WoundRepairPacketBuilder.ComposePendingRoot(binding, packets, parsedCommand);
            Assert.True(WoundRepairPacketBuilder.IsValidPersistedRepairWave(
                JsonSerializer.SerializeToElement(pending["repairPackets"]), JsonSerializer.SerializeToElement(pending["repairReceipts"]),
                snapshot.SessionId, snapshot.RequestId, snapshot.SnapshotToken));
            var submitted = Assert.IsType<JsonObject>(Assert.Single(pending["submittedTreatmentRequests"]!.AsArray()));
            Assert.True(JsonNode.DeepEquals(typedCommand.Request, submitted["request"]));
            Assert.Equal(request.RequestFingerprint, submitted["requestFingerprint"]!.GetValue<string>());
            var complete = MortalWoundTreatmentPersistedRequestCatalog.Parse(JsonSerializer.SerializeToElement(commandRoot),
                JsonSerializer.SerializeToElement(pending), ReadCurrentTreatmentHistory(_fs));
            Assert.True(complete.IsValid, DescribeValidationIssues(complete.Issues));
            Assert.Single(complete.Requests);
            Assert.Equal(request.RequestFingerprint, Assert.Single(complete.HeldRequests).RequestFingerprint);
            Assert.Empty(complete.FinalizedRequests);
            var exactPending = pending.DeepClone();
            if (changed)
            {
                Assert.Equal(2m, submitted["request"]!["resourceAuthority"]!["claims"]![0]!["quantity"]!.GetValue<decimal>());
                submitted["request"]!["resourceAuthority"]!["claims"]![0]!["quantity"] = 1;
                var reversed = pending.DeepClone();
                reversed!["submittedTreatmentRequests"]![0]!["request"]!["resourceAuthority"]!["claims"]![0]!["quantity"] = 2;
                Assert.True(JsonNode.DeepEquals(exactPending, reversed));
            }
            await _fs.WriteFileAtomicAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath, pending.ToJsonString());
            beforeRecovery = await CaptureConfirmedClaimReadImagesAsync(_fs, lease);
            var catalog = MortalWoundTreatmentPersistedRequestCatalog.Parse(JsonSerializer.SerializeToElement(commandRoot),
                JsonSerializer.SerializeToElement(pending), ReadCurrentTreatmentHistory(_fs));
            WritePhysicalInjuryDiagnostic("fr063_component_genuine_pending_catalog", new
            {
                changed, Source42 = source.Source.RequestId, SourceToken = source.SnapshotToken,
                snapshot.SessionId, snapshot.RequestId, snapshot.SnapshotToken, snapshot.TurnNumber, generation,
                QteAcceptedEvents = occurrence.AcceptedEvents, TreatmentAcceptedEvents = binding.AcceptedEvents,
                Draft = decision, Pending = pending, catalog.IsValid, catalog.Issues,
                Requests = catalog.Requests.Count, Held = catalog.HeldRequests.Count, Finalized = catalog.FinalizedRequests.Count,
                ComponentOnly = true, LivePendingProducerQualified = false
            });
            if (changed) AssertRejectedGenuinePendingCatalog(catalog);
            else { Assert.True(catalog.IsValid, DescribeValidationIssues(catalog.Issues)); Assert.Single(catalog.Requests); Assert.Single(catalog.HeldRequests); }
        }
        // Real lease turnover: ExportCurrent itself restores durable claims; no caller invalidation/re-reservation.
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.Equal(generation, _fs.ReadLocalGenerationSnapshot(lease).Binding.Id);
            var exported = MortalWoundTreatmentAcceptedStateAuthority.ExportCurrent(_fs, lease, context, HeldTreatmentPipelineContext.WoundId);
            WritePhysicalInjuryDiagnostic("fr063_component_genuine_pending_fresh_export", new { changed, exported.IsValid, exported.Issues });
            var finalized = MortalWoundTreatmentResourceComposer.Finalize(resolution);
            Assert.True(finalized.IsValid, DescribeValidationIssues(finalized.Issues));
            var finalization = Assert.IsType<MortalWoundTreatmentResourceFinalization>(finalized.Finalization);
            if (changed)
            {
                Assert.False(exported.IsValid);
                Assert.Null(exported.Authority);
                Assert.Contains(exported.Issues, issue => issue.Code == "mortal_wound_treatment_persisted_request_seal_mismatch");
                var inspection = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(_fs, lease, request, finalization);
                Assert.False(inspection.IsValid);
                Assert.Equal(0, inspection.ChangedCount);
                Assert.Null(inspection.GenerationFingerprint);
            }
            else
            {
                Assert.True(exported.IsValid, DescribeValidationIssues(exported.Issues));
                var accepted = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(exported.Authority);
                AssertPendingTreatmentTuple(snapshot, accepted.Binding);
                var recovered = accepted.RestorePersistedTreatmentRequests(ReadCurrentTreatmentHistory(_fs));
                Assert.True(recovered.IsValid, DescribeValidationIssues(recovered.Issues));
                Assert.Single(recovered.Requests);
                var restored = Assert.Single(recovered.HeldRequests);
                Assert.Empty(recovered.FinalizedRequests);
                Assert.Equal(request.RequestFingerprint, restored.RequestFingerprint);
                Assert.Equal(request.ResourceAuthority.ReservationId, restored.ResourceAuthority.ReservationId);
                Assert.True(JsonNode.DeepEquals(WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(request), WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(restored)));
                var inspection = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(_fs, lease, restored, finalization);
                WriteConfirmedClaimInspection("genuine_pending_fresh_export", inspection);
                Assert.True(inspection.IsValid, DescribeValidationIssues(inspection.Issues));
                Assert.Empty(inspection.Issues);
                Assert.Equal(0, inspection.ChangedCount);
                Assert.Equal(MortalWoundTreatmentResourceReservationState.ConfirmedHeld, inspection.State);
                Assert.Equal(request.Coordinates.OperationKey, inspection.OperationKey);
                Assert.Equal(request.Coordinates.AttemptId, inspection.AttemptId);
                Assert.Equal(request.RequestFingerprint, inspection.RequestFingerprint);
                Assert.Equal(finalization.ResourceAuthorityFingerprint, inspection.ResourceAuthorityFingerprint);
                Assert.Equal(finalization.FinalizationFingerprint, inspection.FinalizationFingerprint);
                Assert.False(string.IsNullOrWhiteSpace(inspection.AgreementFingerprint));
                Assert.Equal(generation, inspection.SessionGeneration);
                Assert.Equal(generationRevision, inspection.SessionGenerationRevision);
                Assert.Null(inspection.GenerationFingerprint);
            }
            await AssertConfirmedClaimReadImagesAsync(_fs, lease, beforeRecovery);
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(_fs, lease));
            var definitions = ResourceDefinitionCatalog.ParseCanonical(await _fs.ReadFileAsync(lease, ResourceMaterializationContract.DefinitionsPath), false);
            Assert.True(definitions.IsValid, DescribeValidationIssues(definitions.Issues));
            var state = ResourceStateContract.ParseCanonical(await _fs.ReadFileAsync(lease, ResourceMaterializationContract.StatePath), definitions.Catalog!, false);
            Assert.True(state.IsValid, DescribeValidationIssues(state.Issues));
            Assert.Equal(2m, Assert.Single(state.Ledger!.Entries, entry => entry.Coordinate.ResourceKey == "energy").Current);
            Assert.Equal(5m, Assert.Single(state.Ledger.Entries, entry => entry.Coordinate.ResourceKey == "health").Current);
            var resourceHistory = ResourceHistoryState.ParseCanonical(await _fs.ReadFileAsync(lease, ResourceMaterializationContract.HistoryPath), definitions.Catalog!, false);
            Assert.True(resourceHistory.IsValid, DescribeValidationIssues(resourceHistory.Issues));
            Assert.Equal(0, CountHeldTreatmentEnergySpends(resourceHistory.History!));
            Assert.Single(ReadCurrentTreatmentHistory(_fs).State!.Transitions);
            Assert.Equal("untreated", ReadCurrentTreatmentWound(_fs).Care.State);
            Assert.Equal(source.Source.RequestId, Assert.Single(MortalWoundOccurrenceState.Parse(
                await _fs.ReadFileAsync(lease, MortalWoundOccurrenceState.StatePath), MortalWoundOccurrenceState.StatePath).State!.Occurrences).SourceRequestId);
            var receipts = MortalWoundOpportunityReceiptState.Parse(await _fs.ReadFileAsync(lease, MortalWoundOpportunityReceiptState.StatePath),
                MortalWoundOpportunityReceiptState.StatePath);
            Assert.True(receipts.IsValid, DescribeValidationIssues(receipts.Issues));
            Assert.Empty(receipts.State!.Receipts);
            WritePhysicalInjuryDiagnostic("fr063_component_genuine_pending_final_preservation", new
            { changed, generation, generationRevision, CompletePhysicalBytesUnchanged = true,
                Energy = 2, Health = 5, NewTreatmentSpends = 0, WoundHistoryCount = 1,
                CommonPlanIssued = false, PendingProducerScope = "component-only; live producer OPEN" });
        }
    }

    private async Task ConfigureGenuinePendingTreatmentWorldAsync(FileSystemManager files)
    {
        // Canonical wound bootstrap is a PRECONDITION, not accepted CREATE proof. Every write precedes real QTE original42.
        var wound = CreateGuaranteedResourceTreatmentWound();
        wound["consequences"]!["slotsUsed"] = 0;
        wound["consequences"]!["entries"]!.AsArray().Clear();
        wound["consequences"]!["ownedEffectSources"]!["definitions"]!.AsArray().Clear();
        wound["consequences"]!["ownedEffectSources"]!["rootBindings"]!.AsArray().Clear();
        await SeedHeldTreatmentAuthorityAsync(files, wound);
        await PrepareGuaranteedNativeNpcReceiptShapeAsync(files);
        // Settle only canonical JSON formatting before ProtectedBefore and original minting.
        foreach (var path in new[] { WoundCarrierCatalog.PlayerPath, WoundCarrierCatalog.NpcPath,
                     WoundCarrierCatalog.EnemiesPath, WoundCarrierCatalog.AlliesPath, WoundCarrierCatalog.AfterlifeProfilesPath,
                     WoundIdentityState.StatePath, WoundHistoryState.HistoryPath, MortalWoundOccurrenceState.StatePath,
                     MortalWoundOpportunityReceiptState.StatePath, EffectCarrierCatalog.PlayerPath, EffectIdentityState.StatePath,
                     NpcCoreChangesContract.NpcCorePath, ResourceMaterializationContract.DefinitionsPath,
                     ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
                     CanonicalResourceOwnerAuthorityComposer.AuthorityPath })
        {
            var json = JsonNode.Parse(Assert.IsType<string>(await files.ReadFileAsync(path)))!;
            await files.WriteFileAtomicAsync(path, json.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        }
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
        Assert.Empty(await validator.ValidateAcceptedTurnCanonicalWoundMaterializationAsync(lease));
        Assert.Empty(await validator.ValidateAcceptedTurnCanonicalEffectMaterializationAsync(lease));
        Assert.Empty(await validator.ValidateAcceptedTurnCanonicalResourceMaterializationAsync(lease));
        Assert.Empty(await validator.ValidateAcceptedTurnCanonicalMortalLocationMaterializationAsync(lease));
        Assert.Empty(JsonNode.Parse((await files.ReadFileAsync(lease, EffectCarrierCatalog.PlayerPath))!)!["activeEffects"]!.AsArray());
    }

    private async Task AssertGenuinePendingQteSourceAsync(QteStoryRun source)
    {
        Assert.Equal(source.StoryAtHandoff, await _fs.ReadFileBytesAsync(QteStoryPath));
        var definitions = ResourceDefinitionCatalog.ParseCanonical(await _fs.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath), false);
        Assert.True(definitions.IsValid, DescribeValidationIssues(definitions.Issues));
        var state = ResourceStateContract.ParseCanonical(await _fs.ReadFileAsync(ResourceMaterializationContract.StatePath), definitions.Catalog!, false);
        var history = ResourceHistoryState.ParseCanonical(await _fs.ReadFileAsync(ResourceMaterializationContract.HistoryPath), definitions.Catalog!, false);
        Assert.True(state.IsValid, DescribeValidationIssues(state.Issues));
        Assert.True(history.IsValid, DescribeValidationIssues(history.Issues));
        Assert.Empty(history.History!.ValidateStateAgreement(state.Ledger!));
        var damage = Assert.Single(history.History.Transitions, row => row.EventRef == "turn_42:qte_terminal:1:resource:1");
        Assert.Equal(42, damage.Turn);
        Assert.Equal(ResourceTransitionOperation.Damage, damage.Operation);
        Assert.Equal(ResourceTransitionOutcome.Applied, damage.Outcome);
        Assert.Equal(5m, damage.AppliedAmount);
        Assert.Equal(10m, damage.BeforeState!.Current);
        Assert.Equal(5m, damage.AfterState!.Current);
        Assert.Equal(5m, Assert.Single(state.Ledger!.Entries, entry => entry.Coordinate.ResourceKey == "health").Current);
        var terminal = Assert.Single(JsonNode.Parse((await _fs.ReadFileAsync(QteSceneService.QteHistoryPath))!)!.AsArray())!;
        Assert.Equal(42, terminal["acceptedAtTurn"]!.GetValue<int>());
        Assert.Equal("terminal", JsonNode.Parse((await _fs.ReadFileAsync(QteDeferredEffectContinuation.StatePath))!)!["state"]!.GetValue<string>());
        Assert.Null(JsonNode.Parse((await _fs.ReadFileAsync(QteSceneService.QteRuntimePath))!)["activeScene"]);
    }

    private static void AssertPendingTreatmentTuple(PendingTurnSnapshotReadAuthority snapshot, WoundAcceptedTurnBinding binding)
    {
        Assert.Equal(snapshot.SessionId, binding.SessionId);
        Assert.Equal(snapshot.RequestId, binding.RequestId);
        Assert.Equal(snapshot.SnapshotToken, binding.SnapshotToken);
        Assert.Equal(snapshot.TurnNumber, binding.Turn);
        Assert.Equal(snapshot.Realm, binding.Realm);
    }

    private static void AssertRejectedGenuinePendingCatalog(MortalWoundTreatmentPersistedRequestCatalogResult catalog)
    {
        Assert.False(catalog.IsValid);
        Assert.Contains(catalog.Issues, issue => issue.Code == "mortal_wound_treatment_persisted_request_seal_mismatch" &&
            issue.FilePath.StartsWith(WoundAcceptedTurnSnapshotContract.PendingResolutionPath + ".submittedTreatmentRequests[0].request", StringComparison.Ordinal));
        Assert.Empty(catalog.Requests);
        Assert.Empty(catalog.HeldRequests);
        Assert.Empty(catalog.FinalizedRequests);
        // Invalid nested candidate is rejected BEFORE it can join coalescing. Remaining valid command candidates may still coalesce.
    }
}
