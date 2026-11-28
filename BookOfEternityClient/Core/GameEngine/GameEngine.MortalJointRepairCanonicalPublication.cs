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

    /// <summary>
    /// Publishes the actual joint repair under one issuing lease and temporary source-validation context.
    /// </summary>
    /// <param name="frame">
    /// The actual corrected admitted frame whose original lineage must remain current.
    /// </param>
    /// <param name="expectedTurn">
    /// Turn owned by the current joint correction.
    /// </param>
    /// <param name="backups">
    /// Existing backup names used by the original coordinated validation and publication path.
    /// </param>
    /// <returns>
    /// The retained committed or uncertain owning result, preserving all original source and settlement checks.
    /// </returns>
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
        if (_mortalJointAuditExpected is not null)
            await RefreshCurrentMortalJointAuditNarrativeAsync(frame);
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            RequireOrdinaryJointRepairLease(lease);
            using var sourceOperation = _fs.BeginSourceDataOperationIfNeeded();
            using var recoveryValidation = _fs.BeginRecoveryValidation(lease);
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
                var bound = _normalizer.BindTo(lease);
                var (preflight, effectAllocations) = await BuildCurrentMortalJointPreflightAsync(lease, frame, capture, attempt, bound);
                RequireCurrentMortalJointAuditCandidate(preflight, effectAllocations);
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
            catch (MortalJointPreparationRejectedException error)
            {
                result = Refused(error.Issues);
            }
            catch (MortalJointDebugPrefixRejectedException error)
            {
                result = Refused(error.Issues);
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

            if (sourceOperation != null && result.Disposition == MortalJointRepairPublicationDisposition.Committed && result.Completed != null)
                await sourceOperation.CompleteAsync();

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
