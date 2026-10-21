using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    private const string AcquisitionNarration = "Край камня оставляет поверхностный порез на боку.";
    private const string TreatmentNarration = "Field medic стабилизирует существующую рану.";

    private sealed partial class Fixture
    {
        private object? _jointContinuation;
        private object? _jointReservation;
        /// <summary>
        /// Produces a real source occurrence, treatment hold, corrected repair, wound stages and common-input owners.
        /// No game loop, full turn staging, fake authority or publication transaction is entered.
        /// </summary>
        /// <returns>
        /// The normal shared prefix's bound input and genuine common authority, or its reported issues.
        /// </returns>
        internal async Task<WoundAcceptedTurnPlanner.JointRepairBoundCommonInputResult> PrepareBoundJointAsync()
        {
            using var timing = Files.MeasureOperation("JointBundleFixture.PrepareBoundJoint");
            var source = await PrepareOriginalAsync(43);
            await PreparePhysicalOccurrenceAsync(source);
            await Files.WriteFileAtomicAsync(Lease, "game_state/player/stat_points.json",
                new JsonObject { ["levelUpStatPointsAwardedThroughLevel"] = 1, ["unspentStatPoints"] = 0 }.ToJsonString());
            var original = await PrepareOriginalAsync(44);
            MortalJointRepairCheckpoint.RejectedCapture capture;
            try
            {
                await PrepareHeldRepairOutputsAsync(original);
                using (Files.MeasureOperation("JointBundleFixture.RotateTreatmentLease"))
                {
                    var previousLease = Lease;
                    try
                    {
                        await previousLease.DisposeAsync();
                        _lease = null;
                        // This caller owns the remaining phase: the normal synchronous
                        // acquisition entry installs AsyncLocal registration here.
                        _lease = await Files.AcquireCanonicalWriteLeaseAsync();
                    }
                    catch { _retained = true; throw; }
                    Assert.False(previousLease.IsActive);
                    Assert.True(Files.IsCurrentSessionGeneration(Lease, original.Generation));
                }
                capture = await CaptureCorrectedJointRepairAsync(original);
            }
            catch (CoordinatedStatePublicationUncertainException) { _retained = true; throw; }
            WoundAcceptedTurnPlanner.JointRepairPreparationAuthorityResult preparation;
            using (Files.MeasureOperation("JointBundleFixture.CreateJointPreparation"))
                preparation = await WoundAcceptedTurnPlanner.CreateJointRepairPreparationAsync(Files, Lease, capture);
            Assert.True(preparation.IsValid, JsonSerializer.Serialize(preparation.Issues));
            var continuation = preparation.Authority!;
            object reservation;
            using (Files.MeasureOperation("JointBundleFixture.ReserveJointPublication"))
            {
                var reserved = await AcceptedTurnAuthorityRegistry.ReserveMortalJointRepairPublicationAsync(Files, Lease, continuation);
                Assert.NotNull(reserved.Authority);
                Assert.True(reserved.Status is MortalWoundTreatmentPublicationReservationStatus.Reserved or
                    MortalWoundTreatmentPublicationReservationStatus.Exact, JsonSerializer.Serialize(reserved));
                reservation = reserved.Authority!;
            }
            try
            {
                AcceptedMechanicsWoundStageBundle bundle;
                using (Files.MeasureOperation("JointBundleFixture.BuildWoundStages"))
                {
                    var prepared = await AcceptedTurnAuthorityRegistry.GetOrBuildWoundJointRepairPreparedValidatedAsync(
                        Files, Lease, preparation.Input!, continuation, reservation);
                    Assert.True(prepared.Success, JsonSerializer.Serialize(prepared.Issues));
                    var effects = await AcceptedTurnAuthorityRegistry.GetOrBuildWoundJointRepairEffectValidatedAsync(
                        Files, Lease, prepared.Plan!, WoundAcceptedTurnPlanner.ComposeJointRepairEffectInput(prepared.Plan!),
                        continuation, reservation);
                    Assert.True(effects.Success, JsonSerializer.Serialize(effects.Issues));
                    var final = await AcceptedTurnAuthorityRegistry.GetOrBuildWoundJointRepairFinalValidatedAsync(
                        Files, Lease, prepared.Plan!, effects, continuation, reservation);
                    Assert.True(final.Success, JsonSerializer.Serialize(final.Issues));
                    bundle = new AcceptedMechanicsWoundStageBundle(preparation.Input!, prepared.Plan!, effects.Plan!, final.Plan!);
                }
                var anchors = MortalWoundCanonicalAnchorPlan.CreateJointRepair(Files, Lease, bundle);
                Assert.True(anchors.Success, JsonSerializer.Serialize(anchors.Issues));
                using (Files.MeasureOperation("JointBundleFixture.ValidateRawItems"))
                {
                    var issues = await Runtime.ValidationService.ValidateAcceptedTurnRawMortalItemMaterializationAsync(Lease);
                    Assert.Empty(issues);
                }
                GameEngine.MortalJointRepairMechanicalAdmission mechanical;
                using (Files.MeasureOperation("JointBundleFixture.CaptureMechanicalAdmission"))
                    mechanical = await Runtime.Engine.CreateJointMechanicalAdmissionForComponentAsync(
                        Lease, original, bundle, continuation);
                WoundAcceptedTurnPlanner.JointCommonBaselineResult baseline;
                using (Files.MeasureOperation("JointBundleFixture.CreateCommonBaseline"))
                    baseline = await WoundAcceptedTurnPlanner.CreateJointRepairCommonBaselineAsync(
                        Files, Lease, mechanical, bundle, anchors.Plan!, continuation, reservation);
                Assert.True(baseline.IsValid, JsonSerializer.Serialize(baseline.Issues));
                WoundAcceptedTurnPlanner.JointResourcePublicationBuildResult resources;
                using (Files.MeasureOperation("JointBundleFixture.CreateResourcePublication"))
                    resources = await WoundAcceptedTurnPlanner.CreateJointRepairResourcePublicationAsync(Files, Lease, baseline.Authority!);
                Assert.True(resources.IsValid, JsonSerializer.Serialize(resources.Issues));
                var progression = await Runtime.Engine.CaptureJointProgressionForComponentAsync(Lease, original);
                using (Files.MeasureOperation("JointBundleFixture.BindCommonInput"))
                {
                    var bound = await WoundAcceptedTurnPlanner.PrepareJointRepairBoundCommonInputForComponentAsync(
                        Files, Lease, resources.Authority!, progression);
                    if (bound.Success)
                    {
                        await AssertBeginningOwnerRefusalsAsync(original, bundle, continuation);
                        _jointContinuation = continuation;
                        _jointReservation = reservation;
                    }
                    else
                        AcceptedTurnAuthorityRegistry.AbortMortalJointRepairPublication(Files, Lease, continuation, reservation);
                    return bound;
                }
            }
            catch (CoordinatedStatePublicationUncertainException)
            {
                _retained = true;
                throw;
            }
            catch
            {
                AcceptedTurnAuthorityRegistry.AbortMortalJointRepairPublication(Files, Lease, continuation, reservation);
                throw;
            }
        }

        /// <summary>
        /// Releases only this fixture's actual unpublished joint reservation through the normal registry abort.
        /// </summary>
        private void CloseJointReservation()
        {
            if (_jointContinuation is not null && _jointReservation is not null)
            {
                AcceptedTurnAuthorityRegistry.AbortMortalJointRepairPublication(Files, Lease, _jointContinuation, _jointReservation);
                _jointContinuation = null;
                _jointReservation = null;
            }
        }

        /// <summary>
        /// Creates and confirms a genuine guaranteed treatment request and writes its rejected public outputs.
        /// The owning caller begins the separate recovery lease phase before capturing a private repair wave.
        /// </summary>
        /// <param name="original">
        /// Data coordinates of the current independently authenticated decision original.
        /// </param>
        /// <returns>
        /// A task completing after the genuine held command and timestamped rejected outputs are persisted.
        /// </returns>
        private async Task PrepareHeldRepairOutputsAsync(
            GameEngine.JointComponentOriginal original)
        {
            using var timing = Files.MeasureOperation("JointBundleFixture.PrepareHeldRepair");
            var contextResult = MortalWoundTreatmentAuthority.ParseContext(new JsonObject
            {
                ["schemaVersion"] = 1, ["realm"] = "mortal_world", ["targetKind"] = "player",
                ["targetId"] = "player_current", ["providerKind"] = "npc", ["providerId"] = "field_medic_01",
                ["currentLocationId"] = "loc_field_clinic_001"
            }.ToJsonString(), "treatmentContext");
            Assert.True(contextResult.IsValid, JsonSerializer.Serialize(contextResult.Issues));
            var carrier = JsonNode.Parse((await Files.ReadFileAsync(Lease, WoundCarrierCatalog.PlayerPath))!)!.AsObject();
            var woundData = Assert.Single(carrier["activeWounds"]!.AsArray());
            var parsedWound = WoundMaterializationContract.Parse(woundData!.ToJsonString(), WoundCarrierCatalog.PlayerPath + ".activeWounds[0]");
            Assert.True(parsedWound.IsValid, JsonSerializer.Serialize(parsedWound.Issues));
            var wound = parsedWound.Wound!;
            var exported = MortalWoundTreatmentAcceptedStateAuthority.ExportCurrent(Files, Lease, contextResult.Context!, wound.WoundId);
            Assert.True(exported.IsValid, JsonSerializer.Serialize(exported.Issues));
            var accepted = exported.Authority!;
            Assert.Equal(original.RequestId, accepted.Binding.RequestId);
            var history = WoundHistoryState.Parse(await Files.ReadFileAsync(Lease, WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
            Assert.True(history.IsValid, JsonSerializer.Serialize(history.Issues));
            var route = Assert.Single(wound.Treatment.Routes);
            var requested = MortalWoundTreatmentPlanner.PrepareGuaranteedRequest(accepted, history, wound,
                "operation_joint_component_" + _nonce, route.RouteId, Assert.Single(accepted.Binding.AcceptedEvents).EventRef);
            Assert.True(requested.IsValid, JsonSerializer.Serialize(requested.Issues));
            var request = requested.Request!;
            Assert.True(request.ResourceAuthority.Policy.ReserveBeforeResolution);
            Assert.Empty(request.ResourceAuthority.Claims);
            var attempt = MortalWoundTreatmentPlanner.CreateGuaranteedAttempt(request, history, wound, accepted);
            Assert.Equal("Resolved", attempt.Disposition);
            Assert.Empty(attempt.Issues);
            var resolution = attempt.Resolution!;
            var finalization = MortalWoundTreatmentResourceComposer.Finalize(resolution);
            Assert.True(finalization.IsValid, JsonSerializer.Serialize(finalization.Issues));
            var scene = TreatmentNarration + " " + AcquisitionNarration;
            var command = WoundResponseInputComposer.ComposeMortalWoundTreatmentCommandRoot(accepted.Binding, resolution, scene);
            var parsed = WoundResponseInputComposer.ParseCommandRoot(JsonSerializer.SerializeToElement(command));
            Assert.True(parsed.Success, JsonSerializer.Serialize(parsed.Issues));
            var composition = WoundResponseInputComposer.RecomposeCommandRoot(accepted.Binding, parsed, Array.Empty<WoundOpportunityDecisionReceipt>());
            Assert.True(composition.Success, JsonSerializer.Serialize(composition.Issues));
            Assert.NotNull(await StateDistributor.StageAcceptedWoundCommandAsync(Files, Lease, new GameResponse { Response = scene }, composition));
            var confirmed = MortalWoundTreatmentResourceComposer.ConfirmPersistedTreatmentResources(Files, Lease, [request]);
            Assert.True(confirmed.IsValid, JsonSerializer.Serialize(confirmed.Issues));
            var hold = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(Files, Lease, request, finalization.Finalization!);
            Assert.True(hold.IsValid, JsonSerializer.Serialize(hold.Issues));
            Assert.Equal(MortalWoundTreatmentResourceReservationState.ConfirmedHeld, hold.State);
            var projectionSource = MortalQteWoundOpportunityProjection.ReadSnapshot(Files, Lease);
            Assert.Equal(original.RequestId, projectionSource.RequestId);
            var opportunity = Assert.Single(MortalQteWoundOpportunityProjection.Project(projectionSource));
            var decision = new JsonObject
            {
                ["opportunityRef"] = opportunity["opportunityRef"]!.DeepClone(), ["decision"] = "materialize",
                ["woundRef"] = "component_impact", ["proposal"] = GameEngineTurnLifecycleTests.CreateMortalComponentProposal()
            };
            decision["proposal"]!["severity"] = "III";
            var timestamp = DateTime.UtcNow.ToString("O");
            await Files.WriteFileAtomicAsync(Lease, MortalWoundOpportunityAdapter.DecisionPath, decision.ToJsonString());
            await Files.WriteFileAtomicAsync(Lease, "output/narrative_response.json", JsonSerializer.Serialize(new { response = scene, timestamp }));
            await Files.WriteFileAtomicAsync(Lease, "output/interface_updates.json", JsonSerializer.Serialize(new { dialogueOptions = Array.Empty<object>(), timestamp }));
            await Files.WriteFileAtomicAsync(Lease, "output/debug_logs.json", JsonSerializer.Serialize(new
            {
                timestamp, gm_thoughts_markdown = "## NPC Scope\n- Mode: Scene-local\n- Relevant actors: нет\n" +
                    "- Why relevant: Уже запечатанное клиентом лечение и описание собственного пореза; нового решения NPC нет.\n" +
                    "- Actors outside scope: Field medic\n- Why outside scope: Нет нового взаимодействия, реплик или изменений NPC.\n\n" +
                    "## Reasoning\n- The client owns the held treatment. The physical QTE draft supplies its complete acquisition narration."
            }));
            await Files.WriteFileAtomicAsync(Lease, "ready/turn_complete.json", JsonSerializer.Serialize(new
            {
                sessionId = original.SessionId, requestId = original.RequestId, turnNumber = original.Turn,
                status = "success", timestamp, filesModified = new[] { MortalWoundOpportunityAdapter.DecisionPath,
                    "output/narrative_response.json", "output/interface_updates.json", "output/debug_logs.json" }
            }));
        }

        /// <summary>
        /// Captures and installs the genuine rejected repair under the caller's final participating lease,
        /// then changes only the public severity leaf for the normal correction factory.
        /// </summary>
        /// <param name="original">
        /// The actual decision-original coordinates, unchanged across the ordinary lease phase boundary.
        /// </param>
        /// <returns>
        /// The private rejected capture bound to the same lease used by the remaining joint preparation.
        /// </returns>
        private async Task<MortalJointRepairCheckpoint.RejectedCapture> CaptureCorrectedJointRepairAsync(
            GameEngine.JointComponentOriginal original)
        {
            using var timing = Files.MeasureOperation("JointBundleFixture.CaptureCorrectedRepair");
            var captured = await MortalJointRepairCheckpoint.CaptureAsync(Files, Lease,
                original.SessionId, original.RequestId, original.SnapshotToken, original.Turn);
            Assert.True(captured.IsValid, JsonSerializer.Serialize(captured.Issues));
            var capture = captured.Capture!;
            Assert.NotNull(capture);
            await Files.WriteFileAtomicAsync(Lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath, capture.Pending.ToJsonString());
            var decision = JsonNode.Parse((await Files.ReadFileAsync(Lease, MortalWoundOpportunityAdapter.DecisionPath))!)!.AsObject();
            Assert.Equal("III", decision["proposal"]!["severity"]!.GetValue<string>());
            decision["proposal"]!["severity"] = "II";
            await Files.WriteFileAtomicAsync(Lease, MortalWoundOpportunityAdapter.DecisionPath, decision.ToJsonString());
            return capture;
        }
    }
}
