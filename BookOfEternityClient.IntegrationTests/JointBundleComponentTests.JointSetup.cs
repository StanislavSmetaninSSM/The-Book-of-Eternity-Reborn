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
            try { capture = await PrepareHeldRepairAsync(original); }
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
        /// Creates and confirms a genuine guaranteed treatment request, then installs its rejected repair wave
        /// and changes only the authorized public severity leaf for normal reconstruction.
        /// </summary>
        /// <param name="original">
        /// Data coordinates of the current independently authenticated decision original.
        /// </param>
        /// <returns>
        /// The genuine lease-local rejected capture used by the normal correction factory.
        /// </returns>
        private async Task<MortalJointRepairCheckpoint.RejectedCapture> PrepareHeldRepairAsync(
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
            var read = PendingTurnSnapshotReader.ReadCurrent(Files, Lease, [MortalWoundOccurrenceState.StatePath]);
            Assert.True(read.Success, JsonSerializer.Serialize(read.Issues));
            var opportunity = Assert.Single(MortalQteWoundOpportunityProjection.Project(read.Snapshot!));
            var decision = new JsonObject
            {
                ["opportunityRef"] = opportunity["opportunityRef"]!.DeepClone(), ["decision"] = "materialize",
                ["woundRef"] = "component_impact", ["proposal"] = GameEngineTurnLifecycleTests.CreateMortalComponentProposal()
            };
            decision["proposal"]!["severity"] = "III";
            await Files.WriteFileAtomicAsync(Lease, MortalWoundOpportunityAdapter.DecisionPath, decision.ToJsonString());
            await Files.WriteFileAtomicAsync(Lease, "output/narrative_response.json", JsonSerializer.Serialize(new { response = scene }));
            await Files.WriteFileAtomicAsync(Lease, "output/interface_updates.json", "{\"dialogueOptions\":[]}");
            await Files.WriteFileAtomicAsync(Lease, "ready/turn_complete.json", JsonSerializer.Serialize(new
            {
                sessionId = original.SessionId, requestId = original.RequestId, turnNumber = original.Turn,
                status = "success", filesModified = new[] { MortalWoundOpportunityAdapter.DecisionPath,
                    "output/narrative_response.json", "output/interface_updates.json" }
            }));
            var captured = await MortalJointRepairCheckpoint.CaptureAsync(Files, Lease,
                original.SessionId, original.RequestId, original.SnapshotToken, original.Turn);
            Assert.True(captured.IsValid, JsonSerializer.Serialize(captured.Issues));
            var capture = captured.Capture!;
            Assert.NotNull(capture);
            await Files.WriteFileAtomicAsync(Lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath, capture.Pending.ToJsonString());
            decision["proposal"]!["severity"] = "II";
            await Files.WriteFileAtomicAsync(Lease, MortalWoundOpportunityAdapter.DecisionPath, decision.ToJsonString());
            return capture;
        }
    }
}
