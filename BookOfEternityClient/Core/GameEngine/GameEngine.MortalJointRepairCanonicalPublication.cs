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
        using var namedOperationTimer1 = _fs.MeasureOperation("GameEngine.PublishCurrentMortalJointRepairAsync");
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
            using var namedOperationTimer2 = _fs.MeasureOperation("GameEngine.InspectOrdinaryMortalJointRepairAsync");
            var intake = await InspectOrdinaryMortalJointRepairAsync(lease, frame, expectedTurn);
            namedOperationTimer2?.Dispose();
            if (intake.Disposition != MortalJointRepairRawDisposition.Owned)
                return Retain(new(MortalJointRepairPublicationDisposition.PublicationUncertain, null, intake.Issues));
            using var namedOperationTimer3 = _fs.MeasureOperation("GameEngine.Intake.ValidateCurrentAsync");
            await intake.ValidateCurrentAsync(_fs, lease, frame.SessionId, frame.RequestId,
                frame.Payload.ManifestPayloadHash, expectedTurn);
            namedOperationTimer3?.Dispose();
            using var namedOperationTimer4 = _fs.MeasureOperation("GameEngine.ReadCurrentJointRejectedCaptureAsync");
            var capture = await ReadCurrentJointRejectedCaptureAsync(lease, _mortalJointRepairCheckpoint!.ToJsonObject());
            namedOperationTimer4?.Dispose();
            attempt = BeginCurrentJointAttempt(lease, frame, capture);
            try
            {
                using var namedOperationTimer5 = _fs.MeasureOperation("GameEngine.CaptureCurrentHotCustodyAsync");
                await CaptureCurrentHotCustodyAsync(attempt, lease);
                namedOperationTimer5?.Dispose();
                await InvokeSessionFinalizationCheckpointAsync(SessionFinalizationCheckpoint.MortalJointCustodyCaptureCompletedBeforePreparation);
                using var namedOperationTimer6 = _fs.MeasureOperation("GameEngine.CreateJointRepairPreparationAsync");
                var preparation = await WoundAcceptedTurnPlanner.CreateJointRepairPreparationAsync(_fs, lease, capture);
                namedOperationTimer6?.Dispose();
                attempt.RetainStage(JointAttemptIssuer, JointAttemptStage.Preparation, preparation);
                if (!preparation.IsValid) return Refused(preparation.Issues);
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
                if (!prepared.Success || prepared.Plan is not { } preparedPlan) return Refused(prepared.Issues);
                var effectInput = WoundAcceptedTurnPlanner.ComposeJointRepairEffectInput(preparedPlan);
                using var namedOperationTimer9 = _fs.MeasureOperation("GameEngine.GetOrBuildEffectValidatedAsync");
                var effects = await AcceptedTurnAuthorityRegistry.GetOrBuildWoundJointRepairEffectValidatedAsync(
                    _fs, lease, preparedPlan, effectInput, continuation, reservationAuthority);
                namedOperationTimer9?.Dispose();
                attempt.RetainStage(JointAttemptIssuer, JointAttemptStage.Effects, effects);
                if (!effects.Success || effects.Plan is not { } effectPlan) return Refused(effects.Issues);
                using var namedOperationTimer10 = _fs.MeasureOperation("GameEngine.GetOrBuildFinalValidatedAsync");
                var final = await AcceptedTurnAuthorityRegistry.GetOrBuildWoundJointRepairFinalValidatedAsync(
                    _fs, lease, preparedPlan, effects, continuation, reservationAuthority);
                namedOperationTimer10?.Dispose();
                attempt.RetainStage(JointAttemptIssuer, JointAttemptStage.Final, final);
                if (!final.Success || final.Plan is not { } finalPlan) return Refused(final.Issues);
                var bundle = new AcceptedMechanicsWoundStageBundle(preparation.Input!, preparedPlan, effectPlan, finalPlan);
                using var namedOperationTimer11 = _fs.MeasureOperation("GameEngine.CreateJointRepairAnchors");
                var anchors = MortalWoundCanonicalAnchorPlan.CreateJointRepair(_fs, lease, bundle);
                namedOperationTimer11?.Dispose();
                if (!anchors.Success || anchors.Plan is null) return Refused(anchors.Issues);
                using var namedOperationTimer12 = _fs.MeasureOperation("GameEngine.CreateMechanicalAdmissionAsync");
                var mechanical = await CreateMortalJointRepairMechanicalAdmissionAsync(lease, frame, bundle, continuation);
                namedOperationTimer12?.Dispose();
                using var namedOperationTimer13 = _fs.MeasureOperation("GameEngine.CreateJointRepairCommonBaselineAsync");
                var baseline = await WoundAcceptedTurnPlanner.CreateJointRepairCommonBaselineAsync(
                    _fs, lease, mechanical, bundle, anchors.Plan, continuation, reservationAuthority);
                namedOperationTimer13?.Dispose();
                if (!baseline.IsValid) return Refused(baseline.Issues);
                using var namedOperationTimer14 = _fs.MeasureOperation("GameEngine.CreateJointRepairResourcePublicationAsync");
                var resources = await WoundAcceptedTurnPlanner.CreateJointRepairResourcePublicationAsync(_fs, lease, baseline.Authority!);
                namedOperationTimer14?.Dispose();
                if (!resources.IsValid) return Refused(resources.Issues);
                // Freeze and authenticate progression BEFORE the common input,
                // cache fingerprint and take. No after-seal acknowledgement may
                // attach to the already-issued mechanics candidate.
                using var namedOperationTimer15 = _fs.MeasureOperation("GameEngine.CaptureMortalJointProgressionSourceAsync");
                var progression = await CaptureMortalJointProgressionSourceAsync(lease, frame);
                namedOperationTimer15?.Dispose();
                using var namedOperationTimer16 = _fs.MeasureOperation("GameEngine.CreateJointRepairCommonPlanAsync");
                var common = await WoundAcceptedTurnPlanner.CreateJointRepairCommonPlanAsync(_fs, lease, resources.Authority!, progression);
                namedOperationTimer16?.Dispose();
                if (!common.Success || common.Plan is not { } commonPlan) return Refused(common.Issues);
                attempt.RetainStage(JointAttemptIssuer, JointAttemptStage.Common, common);
                var bound = _normalizer.BindTo(lease);
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
                attempt.MarkDelegated(JointAttemptIssuer);
                using var namedOperationTimer18 = _fs.MeasureOperation("GameEngine.NormalizeAndValidateJointRepairAsync");
                var published = await bound.NormalizeAndValidateJointRepairAsync(preflight, _validator, backups);
                namedOperationTimer18?.Dispose();
                result = Retain(new(published.Disposition, published.Plan, published.Issues));
                if (published.Disposition == MortalJointRepairPublicationDisposition.Committed)
                {
                    // Retain Committed BEFORE this additional diagnostic read.
                    // Its failure cannot reinterpret a finalized hold as rollback.
                    using var namedOperationTimer19 = _fs.MeasureOperation("GameEngine.ReadJointCompletedWitnessAsync");
                    var witness = await MortalQteDecisionPublication.ReadJointCompletedWitnessAsync(_fs, lease,
                        frame.SessionId, frame.RequestId, frame.Payload.ManifestPayloadHash, expectedTurn);
                    namedOperationTimer19?.Dispose();
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
