using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    private sealed class MortalJointPreparationRejectedException(IReadOnlyList<ValidationIssue> issues)
        : Exception("Joint preparation refused its candidate.")
    {
        internal IReadOnlyList<ValidationIssue> Issues { get; } = issues;
    }

    // All capabilities returned here remain inside the caller's single owning lease.
    // Both preview and publication use the exact same preparation and preflight.
    private async Task<(CanonicalStateNormalizer.JointRepairPublicationPreflight Preflight,
        MortalJointAuditEffectData? AuditEffects)> BuildCurrentMortalJointPreflightAsync(
        FileSystemManager.CanonicalWriteLease lease, ValidatedPendingTurnSnapshotContext frame,
        MortalJointRepairCheckpoint.RejectedCapture capture, MortalJointPublicationAttempt attempt,
        CanonicalStateNormalizer bound, bool requestAuditEffectData = false)
    {
                using var namedOperationTimer5 = _fs.MeasureOperation("GameEngine.CaptureCurrentHotCustodyAsync");
                await CaptureCurrentHotCustodyAsync(attempt, lease, frame);
                namedOperationTimer5?.Dispose();
                await InvokeSessionFinalizationCheckpointAsync(SessionFinalizationCheckpoint.MortalJointCustodyCaptureCompletedBeforePreparation);
                await RecheckMortalJointNarrativeBeforePreparationAsync(attempt, lease, frame);
                var expectedAuditEffects = _mortalJointAuditExpected?.EffectAllocations;
                var useAuditEffects = requestAuditEffectData || expectedAuditEffects is not null;
                var auditOwner = await attempt.RecheckClosedDebugPrefixAsync(JointAttemptIssuer, useAuditEffects);
                if (useAuditEffects && (auditOwner is null ||
                    expectedAuditEffects is not null && expectedAuditEffects.Owner != auditOwner))
                    throw new InvalidDataException("joint_audit_effect_owner_changed");
                using var namedOperationTimer6 = _fs.MeasureOperation("GameEngine.CreateJointRepairPreparationAsync");
                var preparation = await WoundAcceptedTurnPlanner.CreateJointRepairPreparationAsync(_fs, lease, capture);
                namedOperationTimer6?.Dispose();
                attempt.RetainStage(JointAttemptIssuer, JointAttemptStage.Preparation, preparation);
                if (!preparation.IsValid) throw new MortalJointPreparationRejectedException(preparation.Issues);
                var continuation = preparation.Authority!;
                attempt.RetainContinuation(JointAttemptIssuer, continuation);
                using var namedOperationTimer7 = _fs.MeasureOperation("GameEngine.ReserveMortalJointRepairPublicationAsync");
                var reservation = await AcceptedTurnAuthorityRegistry.ReserveMortalJointRepairPublicationAsync(_fs, lease, continuation);
                namedOperationTimer7?.Dispose();
                if (reservation.Authority is null || reservation.Status is not
                    (MortalWoundTreatmentPublicationReservationStatus.Reserved or MortalWoundTreatmentPublicationReservationStatus.Exact))
                    throw new InvalidDataException("The same joint owner could not reserve its exclusive publication slot.");
                var reservationAuthority = reservation.Authority;
                attempt.RetainReservation(JointAttemptIssuer, reservationAuthority);
                using var namedOperationTimer8 = _fs.MeasureOperation("GameEngine.GetOrBuildPreparedValidatedAsync");
                var prepared = await AcceptedTurnAuthorityRegistry.GetOrBuildWoundJointRepairPreparedValidatedAsync(
                    _fs, lease, preparation.Input!, continuation, reservationAuthority);
                namedOperationTimer8?.Dispose();
                attempt.RetainStage(JointAttemptIssuer, JointAttemptStage.Prepared, prepared);
                if (!prepared.Success || prepared.Plan is not { } preparedPlan) throw new MortalJointPreparationRejectedException(prepared.Issues);
                var effectInput = WoundAcceptedTurnPlanner.ComposeJointRepairEffectInput(preparedPlan);
                using var namedOperationTimer9 = _fs.MeasureOperation("GameEngine.GetOrBuildEffectValidatedAsync");
                SpiritualWoundReplayJournal? journal = null;
                string? effectFingerprint = null;
                if (auditOwner is not null)
                {
                    // This additional exact comparison is only on the audited route.
                    // Current authority is reconstructed; the journal stores values only.
                    effectFingerprint = WoundAcceptedTurnFingerprints.ComputeEffectInput(preparedPlan, effectInput);
                    if (expectedAuditEffects is not null && expectedAuditEffects.EffectInputFingerprint != effectFingerprint)
                        throw new InvalidDataException("joint_audit_effect_input_changed");
                    journal = expectedAuditEffects is null
                        ? SpiritualWoundReplayJournal.CreateAppend("[]", requireScope: true)
                        : SpiritualWoundReplayJournal.CreateReplay(expectedAuditEffects.AllocationsJson, requireScope: true);
                }
                // Both the wound-effect stage and its later common trigger and
                // consumption transcript use the same attempt's causal stream.
                using var allocationScope = journal?.BeginSpeculation();
                var effectFactory = journal is null ? null :
                    new SpiritualWoundEffectIdentityFactory(journal, new EffectIdentityFactory());
                var effects = await AcceptedTurnAuthorityRegistry.GetOrBuildWoundJointRepairEffectValidatedAsync(
                    _fs, lease, preparedPlan, effectInput, continuation, reservationAuthority, effectFactory);
                namedOperationTimer9?.Dispose();
                attempt.RetainStage(JointAttemptIssuer, JointAttemptStage.Effects, effects);
                if (!effects.Success || effects.Plan is not { } effectPlan) throw new MortalJointPreparationRejectedException(effects.Issues);
                using var namedOperationTimer10 = _fs.MeasureOperation("GameEngine.GetOrBuildFinalValidatedAsync");
                var final = await AcceptedTurnAuthorityRegistry.GetOrBuildWoundJointRepairFinalValidatedAsync(
                    _fs, lease, preparedPlan, effects, continuation, reservationAuthority);
                namedOperationTimer10?.Dispose();
                attempt.RetainStage(JointAttemptIssuer, JointAttemptStage.Final, final);
                if (!final.Success || final.Plan is not { } finalPlan) throw new MortalJointPreparationRejectedException(final.Issues);
                var bundle = new AcceptedMechanicsWoundStageBundle(preparation.Input!, preparedPlan, effectPlan, finalPlan);
                using var namedOperationTimer11 = _fs.MeasureOperation("GameEngine.CreateJointRepairAnchors");
                var anchors = MortalWoundCanonicalAnchorPlan.CreateJointRepair(_fs, lease, bundle);
                namedOperationTimer11?.Dispose();
                if (!anchors.Success || anchors.Plan is null) throw new MortalJointPreparationRejectedException(anchors.Issues);
                using var namedOperationTimer12 = _fs.MeasureOperation("GameEngine.CreateMechanicalAdmissionAsync");
                var mechanical = await CreateMortalJointRepairMechanicalAdmissionAsync(lease, frame, bundle, continuation);
                namedOperationTimer12?.Dispose();
                using var namedOperationTimer13 = _fs.MeasureOperation("GameEngine.CreateJointRepairCommonBaselineAsync");
                var baseline = await WoundAcceptedTurnPlanner.CreateJointRepairCommonBaselineAsync(
                    _fs, lease, mechanical, bundle, anchors.Plan, continuation, reservationAuthority);
                namedOperationTimer13?.Dispose();
                if (!baseline.IsValid) throw new MortalJointPreparationRejectedException(baseline.Issues);
                using var namedOperationTimer14 = _fs.MeasureOperation("GameEngine.CreateJointRepairResourcePublicationAsync");
                var resources = await WoundAcceptedTurnPlanner.CreateJointRepairResourcePublicationAsync(_fs, lease, baseline.Authority!);
                namedOperationTimer14?.Dispose();
                if (!resources.IsValid) throw new MortalJointPreparationRejectedException(resources.Issues);
                // Freeze and authenticate progression BEFORE the common input,
                // cache fingerprint and take. No after-seal acknowledgement may
                // attach to the already-issued mechanics candidate.
                using var namedOperationTimer15 = _fs.MeasureOperation("GameEngine.CaptureMortalJointProgressionSourceAsync");
                var progression = await CaptureMortalJointProgressionSourceAsync(lease, frame);
                namedOperationTimer15?.Dispose();
                using var namedOperationTimer16 = _fs.MeasureOperation("GameEngine.CreateJointRepairCommonPlanAsync");
                var common = await WoundAcceptedTurnPlanner.CreateJointRepairCommonPlanAsync(
                    _fs, lease, resources.Authority!, progression, effectFactory);
                namedOperationTimer16?.Dispose();
                if (!common.Success || common.Plan is not { } commonPlan) throw new MortalJointPreparationRejectedException(common.Issues);
                attempt.RetainStage(JointAttemptIssuer, JointAttemptStage.Common, common);
                using var namedOperationTimer17 = _fs.MeasureOperation("GameEngine.PrevalidateJointRepairPublicationTransactionAsync");
                var preflight = await bound.PrevalidateJointRepairPublicationTransactionAsync() ??
                    throw new InvalidDataException("The exact complete joint common plan was not preflighted.");
                namedOperationTimer17?.Dispose();
                if (preflight.Validated.Plan.InputFingerprint != commonPlan.InputFingerprint ||
                    preflight.Validated.Plan.PreparedPlanFingerprint != commonPlan.PreparedPlanFingerprint)
                    throw new InvalidDataException("Joint canonical preflight no longer selects the same complete common candidate.");
                // From here only the owning transaction can classify/compensate
                // attempted publication. A generic catch must never abort/rearm it.
                attempt.RetainStage(JointAttemptIssuer, JointAttemptStage.Preflight, preflight);
                MortalJointAuditEffectData? auditEffects = null;
                if (journal is not null)
                {
                    allocationScope!.Commit();
                    auditEffects = new(auditOwner!, effectFingerprint!, journal.Export().ToJsonString());
                }
                return (preflight, auditEffects);
    }
}
