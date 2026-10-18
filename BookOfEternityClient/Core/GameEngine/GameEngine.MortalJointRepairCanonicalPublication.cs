using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    // A settled owning result is separate from the old homogeneous transaction.
    // The witness below is diagnostic, never permission to take/replay a plan.
    private sealed record MortalJointRepairCanonicalRefresh(
        MortalJointRepairPublicationDisposition Disposition,
        AcceptedMechanicsPlan? Plan,
        IReadOnlyList<ValidationIssue> Issues,
        MortalQteDecisionPublication.JointCompletedWitness? Completed = null,
        ValidatedPendingTurnSnapshotContext? Frame = null);

    private MortalJointRepairCanonicalRefresh? _mortalJointRepairCanonicalRefresh;

    private async Task<MortalJointRepairCanonicalRefresh> PublishCurrentMortalJointRepairAsync(
        ValidatedPendingTurnSnapshotContext frame, int expectedTurn,
        IReadOnlyDictionary<string, string> backups)
    {
        if (_mortalJointPublicationAttempt is { Live: true })
            throw new InvalidOperationException("A live joint attempt already owns this engine.");
        if (!MatchesMortalJointRepairFrame(frame, expectedTurn) || !_mortalJointRepairCorrectionReady)
            throw new InvalidDataException("Joint canonical publication requires the actual corrected admitted frame.");
        if (_mortalJointRepairCanonicalRefresh is { Disposition: MortalJointRepairPublicationDisposition.Committed or
            MortalJointRepairPublicationDisposition.PublicationUncertain })
            throw new InvalidDataException("A retained joint committed/uncertain attempt requires its dedicated recovery route, not a second publication.");
        MortalJointRepairCanonicalRefresh result;
        MortalJointPublicationAttempt? attempt = null;
        try
        {
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            RequireOrdinaryJointRepairLease(lease);
            // The raw intake lease owns no plan. Reconstruct BOTH owners and the
            // still-confirmed original request here, under the canonical lease.
            var intake = await InspectOrdinaryMortalJointRepairAsync(lease, frame, expectedTurn);
            if (intake.Disposition != MortalJointRepairRawDisposition.Owned)
                return Retain(new(MortalJointRepairPublicationDisposition.PublicationUncertain, null, intake.Issues));
            await intake.ValidateCurrentAsync(_fs, lease, frame.SessionId, frame.RequestId,
                frame.Payload.ManifestPayloadHash, expectedTurn);
            var capture = await ReadCurrentJointRejectedCaptureAsync(lease, _mortalJointRepairCheckpoint!.ToJsonObject());
            attempt = BeginCurrentJointAttempt(lease, frame, capture);
            try
            {
                await CaptureCurrentHotCustodyAsync(attempt, lease);
                await InvokeSessionFinalizationCheckpointAsync(SessionFinalizationCheckpoint.MortalJointCustodyCaptureCompletedBeforePreparation);
                var preparation = await WoundAcceptedTurnPlanner.CreateJointRepairPreparationAsync(_fs, lease, capture);
                attempt.RetainStage(JointAttemptIssuer, JointAttemptStage.Preparation, preparation);
                if (!preparation.IsValid) return Refused(preparation.Issues);
                var continuation = preparation.Authority!;
                attempt.RetainContinuation(JointAttemptIssuer, continuation);
                var reservation = await AcceptedTurnAuthorityRegistry.ReserveMortalJointRepairPublicationAsync(_fs, lease, continuation);
                if (reservation.Authority is null || reservation.Status is not
                    (MortalWoundTreatmentPublicationReservationStatus.Reserved or MortalWoundTreatmentPublicationReservationStatus.Exact))
                    throw new InvalidDataException("The same joint owner could not reserve its exclusive publication slot.");
                var reservationAuthority = reservation.Authority;
                attempt.RetainReservation(JointAttemptIssuer, reservationAuthority);
                var prepared = await AcceptedTurnAuthorityRegistry.GetOrBuildWoundJointRepairPreparedValidatedAsync(
                    _fs, lease, preparation.Input!, continuation, reservationAuthority);
                attempt.RetainStage(JointAttemptIssuer, JointAttemptStage.Prepared, prepared);
                if (!prepared.Success || prepared.Plan is not { } preparedPlan) return Refused(prepared.Issues);
                var effectInput = WoundAcceptedTurnPlanner.ComposeJointRepairEffectInput(preparedPlan);
                var effects = await AcceptedTurnAuthorityRegistry.GetOrBuildWoundJointRepairEffectValidatedAsync(
                    _fs, lease, preparedPlan, effectInput, continuation, reservationAuthority);
                attempt.RetainStage(JointAttemptIssuer, JointAttemptStage.Effects, effects);
                if (!effects.Success || effects.Plan is not { } effectPlan) return Refused(effects.Issues);
                var final = await AcceptedTurnAuthorityRegistry.GetOrBuildWoundJointRepairFinalValidatedAsync(
                    _fs, lease, preparedPlan, effects, continuation, reservationAuthority);
                attempt.RetainStage(JointAttemptIssuer, JointAttemptStage.Final, final);
                if (!final.Success || final.Plan is not { } finalPlan) return Refused(final.Issues);
                var bundle = new AcceptedMechanicsWoundStageBundle(preparation.Input!, preparedPlan, effectPlan, finalPlan);
                var anchors = MortalWoundCanonicalAnchorPlan.CreateJointRepair(_fs, lease, bundle);
                if (!anchors.Success || anchors.Plan is null) return Refused(anchors.Issues);
                var mechanical = await CreateMortalJointRepairMechanicalAdmissionAsync(lease, frame, bundle, continuation);
                var baseline = await WoundAcceptedTurnPlanner.CreateJointRepairCommonBaselineAsync(
                    _fs, lease, mechanical, bundle, anchors.Plan, continuation, reservationAuthority);
                if (!baseline.IsValid) return Refused(baseline.Issues);
                var resources = await WoundAcceptedTurnPlanner.CreateJointRepairResourcePublicationAsync(_fs, lease, baseline.Authority!);
                if (!resources.IsValid) return Refused(resources.Issues);
                // Freeze and authenticate progression BEFORE the common input,
                // cache fingerprint and take. No after-seal acknowledgement may
                // attach to the already-issued mechanics candidate.
                var progression = await CaptureMortalJointProgressionSourceAsync(lease, frame);
                var common = await WoundAcceptedTurnPlanner.CreateJointRepairCommonPlanAsync(_fs, lease, resources.Authority!, progression);
                if (!common.Success || common.Plan is not { } commonPlan) return Refused(common.Issues);
                attempt.RetainStage(JointAttemptIssuer, JointAttemptStage.Common, common);
                var bound = _normalizer.BindTo(lease);
                var preflight = await bound.PrevalidateJointRepairPublicationTransactionAsync() ??
                    throw new InvalidDataException("The exact complete joint common plan was not preflighted.");
                if (preflight.Validated.Plan.InputFingerprint != commonPlan.InputFingerprint ||
                    preflight.Validated.Plan.PreparedPlanFingerprint != commonPlan.PreparedPlanFingerprint)
                    throw new InvalidDataException("Joint canonical preflight no longer selects the same complete common candidate.");
                // From here only the owning transaction can classify/compensate
                // attempted publication. A generic catch must never abort/rearm it.
                attempt.RetainStage(JointAttemptIssuer, JointAttemptStage.Preflight, preflight);
                attempt.MarkDelegated(JointAttemptIssuer);
                var published = await bound.NormalizeAndValidateJointRepairAsync(preflight, _validator, backups);
                result = Retain(new(published.Disposition, published.Plan, published.Issues));
                if (published.Disposition == MortalJointRepairPublicationDisposition.Committed)
                {
                    // Retain Committed BEFORE this additional diagnostic read.
                    // Its failure cannot reinterpret a finalized hold as rollback.
                    var witness = await MortalQteDecisionPublication.ReadJointCompletedWitnessAsync(_fs, lease,
                        frame.SessionId, frame.RequestId, frame.Payload.ManifestPayloadHash, expectedTurn);
                    result = Retain(result with { Completed = witness });
                }
                lease.EnsureNoPendingLocalDecision();
            }
            catch (Exception error) when (error is not SessionReplacedException &&
                error is InvalidDataException or IOException or InvalidOperationException or ArgumentException or
                    System.Text.Json.JsonException or AggregateException or OperationCanceledException)
            {
                CloseCurrentJointAttempt(attempt, lease);
                var existing = _mortalJointRepairCanonicalRefresh;
                result = Retain(new(existing?.Disposition == MortalJointRepairPublicationDisposition.Committed
                    ? MortalJointRepairPublicationDisposition.Committed : MortalJointRepairPublicationDisposition.PublicationUncertain,
                    existing?.Plan, JointRepairAdmissionIssue(error.Message), existing?.Completed));
                _logger.LogError(error,
                    "Joint canonical publication retained {Disposition} after an exception.", result.Disposition);
            }

            MortalJointRepairCanonicalRefresh Refused(IReadOnlyList<ValidationIssue> issues)
            {
                CloseCurrentJointAttempt(attempt, lease);
                return Retain(new(MortalJointRepairPublicationDisposition.PublicationUncertain, null, issues));
            }
        }
        }
        finally
        {
            // Expiry follows the real await-using close, including its failure.
            attempt?.Expire(JointAttemptIssuer);
        }
        // Runtime reads occur only after the participating canonical lease closes.
        if (result.Disposition == MortalJointRepairPublicationDisposition.Committed && result.Completed is not null)
            await RefreshRuntimeStateAsync();
        return result;

        MortalJointRepairCanonicalRefresh Retain(MortalJointRepairCanonicalRefresh value) =>
            _mortalJointRepairCanonicalRefresh = value with { Frame = frame };
    }
}
