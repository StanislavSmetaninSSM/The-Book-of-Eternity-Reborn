using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class AcceptedTurnAuthorityRegistry
{
    internal static async Task<MortalWoundTreatmentPublicationReservation> ReserveMortalJointRepairPublicationAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease, object continuation)
    {
        // IO/source/claim revalidation happens while participating, before taking
        // the existing registry gate. No monitor is retained across an await.
        try { await WoundAcceptedTurnPlanner.ValidateJointRepairCurrentAsync(files, lease, continuation); }
        catch (Exception failure) when (failure is InvalidDataException or IOException)
        { GetState(files, lease).InvalidateOwnedJointRepairPreparation(continuation, null); throw; }
        return GetState(files, lease).ReserveMortalJointRepairPublication(continuation);
    }

    internal static async Task<WoundAcceptedTurnPreparationResult> GetOrBuildWoundJointRepairPreparedValidatedAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        WoundAcceptedTurnInput input, object continuation, object reservation)
    {
        try { await WoundAcceptedTurnPlanner.ValidateJointRepairCurrentAsync(files, lease, continuation); }
        catch (Exception failure) when (failure is InvalidDataException or IOException)
        { GetState(files, lease).InvalidateOwnedJointRepairPreparation(continuation, reservation); throw; }
        return GetState(files, lease).GetOrBuildWoundJointRepairPrepared(input, continuation, reservation);
    }

    internal static void AbortMortalJointRepairPublication(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, object continuation, object reservation) =>
        GetState(files, lease).AbortMortalJointRepairPublication(continuation, reservation);

    internal static async Task<WoundEffectBatchPlanningResult> GetOrBuildWoundJointRepairEffectValidatedAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        WoundPreparedAcceptedTurnPlan prepared, EffectAcceptedTurnInput input, object continuation, object reservation)
    {
        try
        {
            await WoundAcceptedTurnPlanner.ValidateJointRepairCurrentAsync(files, lease, continuation);
            if (!ReferenceEquals(prepared.JointRepairContinuationAuthority, continuation) ||
                !WoundAcceptedTurnPlanner.JointRepairEffectInputAgrees(prepared, input))
                throw new InvalidDataException("Joint effect input is not the complete independently composed current source/lifecycle input.");
        }
        catch (Exception failure) when (failure is InvalidDataException or IOException)
        { GetState(files, lease).InvalidateOwnedJointRepairPreparation(continuation, reservation); throw; }
        return GetState(files, lease).GetOrBuildWoundEffectValidated(prepared, input,
            reservationAuthority: reservation, jointRepairContinuationAuthority: continuation);
    }

    internal static async Task<WoundAcceptedTurnPlanningResult> GetOrBuildWoundJointRepairFinalValidatedAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        WoundPreparedAcceptedTurnPlan prepared, WoundEffectBatchPlanningResult effect, object continuation, object reservation)
    {
        try
        {
            await WoundAcceptedTurnPlanner.ValidateJointRepairCurrentAsync(files, lease, continuation);
            if (!ReferenceEquals(prepared.JointRepairContinuationAuthority, continuation))
                throw new InvalidDataException("Joint finalization belongs to a foreign private preparation.");
        }
        catch (Exception failure) when (failure is InvalidDataException or IOException)
        { GetState(files, lease).InvalidateOwnedJointRepairPreparation(continuation, reservation); throw; }
        return GetState(files, lease).GetOrBuildWoundFinal(prepared, effect,
            reservationAuthority: reservation, jointRepairContinuationAuthority: continuation);
    }

    internal static async Task<MortalItemTreatmentPublicationBaselineSealResult> SealMortalJointRepairItemBaselineAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease, object continuation, object reservation,
        GameEngine.MortalJointRepairMechanicalAdmission mechanical,
        AcceptedMechanicsWoundStageBundle bundle, IReadOnlyDictionary<string, System.Text.Json.Nodes.JsonNode?> roots,
        MortalTreatmentItemCommandEnvelope envelope, NpcCoreChangesContract.Authority npcAuthority,
        CanonicalBeforeImage npcTradePending, CanonicalBeforeImage trainingPending)
    {
        await WoundAcceptedTurnPlanner.ValidateJointRepairCurrentAsync(files, lease, continuation);
        await mechanical.ValidateCurrentAsync(files, lease, bundle.Input.Binding);
        return GetState(files, lease).SealMortalJointRepairItemBaseline(continuation, reservation, bundle,
            roots, envelope, npcAuthority, npcTradePending, trainingPending);
    }

    internal static bool ConfirmsMortalJointRepairItemBaseline(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, object continuation, object reservation,
        MortalItemAcceptedTurnNormalizationSnapshot expected) =>
        GetState(files, lease).ConfirmsMortalJointRepairItemBaseline(continuation, reservation, expected);

    internal sealed record JointCommonCandidateResult(AcceptedMechanicsPlanningResult Result,
        AcceptedMechanicsPlanCache.JointCandidateFence? Fence);
    /// <summary>
    /// Creates an unsealed joint candidate through the current registry while observing synchronous nested work.
    /// </summary>
    /// <param name="files">
    /// The filesystem whose current session owns the registry and optional passive receiver.
    /// </param>
    /// <param name="lease">
    /// The active canonical write lease used to obtain the participating registry state.
    /// </param>
    /// <param name="input">
    /// The complete common input bound by the joint authority.
    /// </param>
    /// <param name="authority">
    /// The genuinely issued joint common authority for the current reservation.
    /// </param>
    /// <returns>
    /// The raw candidate and its cache fence, or refusal issues with no fence.
    /// </returns>
    internal static JointCommonCandidateResult CreateJointRepairCommonCandidate(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, AcceptedMechanicsInput input,
        WoundAcceptedTurnPlanner.MortalJointRepairCommonAuthority authority)
    {
        using var context = files?.EnterOperationTimingContext();
        using var totalTiming = files?.MeasureOperation("JointCandidate.Registry.Total");
        using var stateTiming = files?.MeasureOperation("JointCandidate.Registry.State");
        var state = GetState(files!, lease);
        stateTiming?.Dispose();
        return state.CreateJointRepairCommonCandidate(input, authority);
    }
    internal static AcceptedMechanicsPlanningResult SealJointRepairCommonCandidate(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, AcceptedMechanicsInput input,
        WoundAcceptedTurnPlanner.MortalJointRepairCommonAuthority authority,
        AcceptedMechanicsPlanCache.JointCandidateFence fence, object proof, AcceptedMechanicsPlan candidate) =>
        GetState(files, lease).SealJointRepairCommonCandidate(input, authority, fence, proof, candidate);
    internal static void RevokeJointRepairCommonCandidate(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        WoundAcceptedTurnPlanner.MortalJointRepairCommonAuthority authority, AcceptedMechanicsPlanCache.JointCandidateFence fence) =>
        GetState(files, lease).RevokeJointRepairCommonCandidate(authority, fence);

    private sealed partial class AcceptedTurnAuthorityState
    {
        private WoundAcceptedTurnPlanner.MortalJointRepairCommonAuthority? _validatedJointRepairCommonAuthority;
        private AcceptedMechanicsPlan? _validatedJointRepairCommonPlan;
        /// <summary>
        /// Checks the current joint reservation, wound stages and confirmed hold before creating a raw cache candidate.
        /// </summary>
        /// <param name="input">
        /// The complete common input bound to <paramref name="authority"/>.
        /// </param>
        /// <param name="authority">
        /// The issued joint common authority whose current reservation and accepted state must still agree.
        /// </param>
        /// <returns>
        /// The unsealed candidate and fence, or refusal issues without a fence.
        /// </returns>
        internal JointCommonCandidateResult CreateJointRepairCommonCandidate(AcceptedMechanicsInput input,
            WoundAcceptedTurnPlanner.MortalJointRepairCommonAuthority authority)
        {
            using var lockWaitTiming = FileSystemManager.MeasureCurrentOperation("JointCandidate.Registry.LockWait");
            lock (_gate)
            {
                lockWaitTiming?.Dispose();
                using var lockWorkTiming = FileSystemManager.MeasureCurrentOperation("JointCandidate.Registry.LockWork");
                if (!OwnsJointRepairPublicationReservation(authority.Continuation, authority.Reservation) ||
                    !authority.MatchesInput(input) || GetCommonWoundStageMismatch(input) is not null ||
                    !ReferenceEquals(authority.Accepted, _mortalWoundTreatmentAcceptedState))
                    return new(JointCommonFailure("current reservation/stages changed"), null);
                var hold = _treatmentResources.ProbeConfirmed(TreatmentResourceRegistryCapability, authority.Request, authority.Finalization);
                if (!hold.IsValid) return new(JointConfirmedClaimFailure(hold), null);
                _validatedJointRepairCommonAuthority = null;
                _validatedJointRepairCommonPlan = null;
                var raw = _commonPlan.CreateJointCandidate(input, authority);
                return new(raw.Result, raw.Fence);
            }
        }
        internal AcceptedMechanicsPlanningResult SealJointRepairCommonCandidate(AcceptedMechanicsInput input,
            WoundAcceptedTurnPlanner.MortalJointRepairCommonAuthority authority,
            AcceptedMechanicsPlanCache.JointCandidateFence fence, object proof, AcceptedMechanicsPlan candidate)
        {
            lock (_gate)
            {
                if (!OwnsJointRepairPublicationReservation(authority.Continuation, authority.Reservation) ||
                    !ReferenceEquals(authority.Accepted, _mortalWoundTreatmentAcceptedState) || GetCommonWoundStageMismatch(input) is not null ||
                    !ConfirmsMortalJointRepairItemBaseline(authority.Continuation, authority.Reservation, authority.ItemsForRegistry))
                {
                    _commonPlan.RevokeJointCandidate(authority, fence);
                    return JointCommonFailure("current joint lineage changed before sealing");
                }
                var hold = _treatmentResources.ProbeConfirmed(TreatmentResourceRegistryCapability, authority.Request, authority.Finalization);
                if (!hold.IsValid)
                { _commonPlan.RevokeJointCandidate(authority, fence); return JointConfirmedClaimFailure(hold); }
                var result = _commonPlan.SealJointCandidate(input, authority, fence, proof, candidate);
                if (result.Success)
                { _validatedJointRepairCommonAuthority = authority; _validatedJointRepairCommonPlan = result.Plan; }
                return result;
            }
        }
        internal void RevokeJointRepairCommonCandidate(WoundAcceptedTurnPlanner.MortalJointRepairCommonAuthority authority,
            AcceptedMechanicsPlanCache.JointCandidateFence fence)
        {
            lock (_gate)
            {
                _commonPlan.RevokeJointCandidate(authority, fence);
                if (ReferenceEquals(_validatedJointRepairCommonAuthority, authority))
                { _validatedJointRepairCommonAuthority = null; _validatedJointRepairCommonPlan = null; }
            }
        }
        private static AcceptedMechanicsPlanningResult JointConfirmedClaimFailure(MortalWoundTreatmentResourceConfirmedHoldProbe hold) =>
            hold.Issues.Count != 0 ? new(null, hold.Issues.Select(MortalWoundOpportunityAdapter.PrivateSourceIssue).ToArray()) :
                JointCommonFailure("confirmed resource claim refused: " + hold.State);
        private static AcceptedMechanicsPlanningResult JointCommonFailure(string reason) => new(null,
            [MortalWoundOpportunityAdapter.PrivateSourceIssue(WoundAcceptedTurnPlannerCore.NewIssue(
                "accepted_mechanics_joint_common_owner_changed", reason, "the exact joint source/reservation/confirmed claim", "refused"))]);

        internal MortalItemTreatmentPublicationBaselineSealResult SealMortalJointRepairItemBaseline(
            object continuation, object reservation, AcceptedMechanicsWoundStageBundle bundle,
            IReadOnlyDictionary<string, System.Text.Json.Nodes.JsonNode?> roots, MortalTreatmentItemCommandEnvelope envelope,
            NpcCoreChangesContract.Authority npcAuthority, CanonicalBeforeImage npcTradePending, CanonicalBeforeImage trainingPending)
        {
            lock (_gate)
            {
                var binding = bundle.Input.Binding;
                if (!OwnsJointRepairPublicationReservation(continuation, reservation) || !envelope.IsEmpty ||
                    !ReferenceEquals(bundle.PreparedPlan.JointRepairContinuationAuthority, continuation) ||
                    _woundPlan.GetPreparedMismatchCode(bundle.PreparedPlan) is not null ||
                    !CurrentWoundEffectAgrees(bundle.PreparedPlan, new WoundEffectBatchPlanningResult(bundle.EffectBatchPlan, Array.Empty<ValidationIssue>())) ||
                    !_woundPlan.TryPeekFinal(out var final) || final.Plan?.WoundFinalPlanFingerprint != bundle.WoundFinalPlanFingerprint ||
                    !_mortalItems.ConfirmsTreatmentContinuation(binding.SessionId, binding.SnapshotToken, roots) ||
                    !_mortalItems.TryCaptureNormalizationSnapshot(binding.SessionId, binding.SnapshotToken, binding.Turn, out var baseline))
                    return new(null, ItemAuthorityChangedIssue());
                // Slot validity is not an emptiness proof: reject actual registered
                // concurrent item creations/transfers even when roots still match.
                if (baseline.Transfers.Count != 0 || baseline.CloneRouteCatalog().ByCreationRef.Count != 0 ||
                    !baseline.TryProjectItemPhase(out var itemPhase, out var phaseIssues) || itemPhase is null)
                    return new(null, ItemAuthorityChangedIssue());
                var finalization = WoundAcceptedTurnPlanner.ReadJointRepairResourceFinalization(continuation);
                if (!MortalItemNpcTradeTailPolicy.TryPredictNpcRootOwnership(false, finalization.Consumptions,
                        _mortalItems.GetOwners(binding.SessionId, binding.SnapshotToken), itemPhase.ItemPhaseAfterImages, out var ownsNpcRoot))
                    return new(null, ItemAuthorityChangedIssue());
                var disposition = MortalItemNpcTradeTailPolicy.SelectDisposition(hasTreatmentContinuation: true, ownsNpcRoot);
                if (baseline.HasFinalPublicationBaseline)
                {
                    if (!baseline.MatchesTreatmentPublicationBaseline(envelope, npcAuthority, npcTradePending, trainingPending, disposition))
                        return new(null, ItemBaselineChangedIssue());
                }
                else
                {
                    var sealedBaseline = baseline.CreateTreatmentPublicationBaseline(envelope, npcAuthority, npcTradePending, trainingPending, disposition);
                    if (!sealedBaseline.IsValid || sealedBaseline.Snapshot is null) return sealedBaseline;
                    if (!_mortalItems.TrySealTreatmentPublicationBaseline(baseline, sealedBaseline.Snapshot))
                        return new(null, ItemAuthorityChangedIssue());
                }
                return _mortalItems.TryCaptureNormalizationSnapshot(binding.SessionId, binding.SnapshotToken, binding.Turn, out var actual)
                    ? new(actual, Array.Empty<ValidationIssue>()) : new(null, ItemAuthorityChangedIssue());
            }
        }

        internal bool ConfirmsMortalJointRepairItemBaseline(object continuation, object reservation,
            MortalItemAcceptedTurnNormalizationSnapshot expected)
        {
            lock (_gate)
                return OwnsJointRepairPublicationReservation(continuation, reservation) &&
                    _mortalItems.TryCaptureNormalizationSnapshot(expected.SessionId, expected.SnapshotToken, expected.Turn, out var actual) &&
                    expected.ProofFingerprint == actual.ProofFingerprint && expected.MatchesFinalPublicationBaseline(actual);
        }

        // Occupies the same exclusive publication slot as old treatment, with a
        // distinct private identity. It cannot satisfy the old continuation reader.
        private sealed class JointRepairPublicationReservationAuthority(object continuation)
        {
            internal object Continuation { get; } = continuation;
        }

        internal MortalWoundTreatmentPublicationReservation ReserveMortalJointRepairPublication(object continuation)
        {
            var accepted = WoundAcceptedTurnPlanner.ReadJointRepairAcceptedState(continuation);
            var fingerprint = WoundAcceptedTurnPlanner.GetJointRepairContinuationFingerprint(continuation);
            lock (_gate)
            {
                if (_treatmentPublicationRestartBlockerFingerprint is not null)
                    return new(MortalWoundTreatmentPublicationReservationStatus.RestartRequired, null);
                if (!ReferenceEquals(_mortalWoundTreatmentAcceptedState, accepted) ||
                    _mortalWoundTreatmentPublicationReservation is not null)
                    return new(MortalWoundTreatmentPublicationReservationStatus.Conflict, null);
                if (_mortalWoundTreatmentPublicationFingerprint is not null)
                    return new(_mortalWoundTreatmentPublicationFingerprint == fingerprint
                        ? MortalWoundTreatmentPublicationReservationStatus.Exact
                        : MortalWoundTreatmentPublicationReservationStatus.Conflict, null);
                var reservation = new JointRepairPublicationReservationAuthority(continuation);
                _mortalWoundTreatmentPublicationReservation = reservation;
                _mortalWoundTreatmentReservedFingerprint = fingerprint;
                return new(MortalWoundTreatmentPublicationReservationStatus.Reserved, reservation);
            }
        }

        /// <summary>
        /// Checks that the exact joint reservation is current and has no open publication receipt.
        /// </summary>
        /// <param name="continuation">
        /// The private continuation expected to own the reservation.
        /// </param>
        /// <param name="reservation">
        /// The exact reservation object to compare against the current registry slot.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when the reservation and continuation still agree; otherwise, <see langword="false"/>.
        /// </returns>
        private bool OwnsJointRepairPublicationReservation(object continuation, object reservation)
        {
            using var operationTiming = FileSystemManager.MeasureCurrentOperation("JointCandidate.Registry.Reservation");
            return _openJointRepairPublicationReceipt is null && JointRepairPublicationReservationAgrees(continuation, reservation);
        }

        private bool JointRepairPublicationReservationAgrees(object continuation, object reservation) =>
            _mortalWoundTreatmentPublicationFingerprint is null &&
            _mortalWoundTreatmentPublicationReservation is JointRepairPublicationReservationAuthority joint &&
            ReferenceEquals(joint, reservation) && ReferenceEquals(joint.Continuation, continuation) &&
            _mortalWoundTreatmentReservedFingerprint == WoundAcceptedTurnPlanner.GetJointRepairContinuationFingerprint(continuation) &&
            WoundAcceptedTurnPlanner.JointRepairAcceptedStateAgrees(continuation, _mortalWoundTreatmentAcceptedState!);

        internal WoundAcceptedTurnPreparationResult GetOrBuildWoundJointRepairPrepared(
            WoundAcceptedTurnInput input, object continuation, object reservation)
        {
            lock (_gate)
            {
                if (!OwnsJointRepairPublicationReservation(continuation, reservation))
                    return FailedWoundPrepared("wound_plan_joint_reservation_invalid",
                        "one current privately reserved joint preparation", "stale or foreign family");
                try
                {
                    var result = _woundPlan.GetOrBuildJointRepairPrepared(input, continuation, out var reused);
                    if (!reused)
                    {
                        _effectPlan.InvalidateAll(); ClearWoundEffectCore(); _commonPlan.InvalidateAll();
                        _mortalWoundTreatmentPublicationFingerprint = null;
                        _validatedTreatmentPublicationHandoff = null;
                    }
                    return result;
                }
                catch { InvalidateAllCore(); throw; }
            }
        }

        internal void AbortMortalJointRepairPublication(object continuation, object reservation)
        {
            lock (_gate)
                if (OwnsJointRepairPublicationReservation(continuation, reservation))
                    ClearMortalWoundTreatmentPublicationReservationCore();
        }

        internal void InvalidateOwnedJointRepairPreparation(object continuation, object? reservation)
        {
            lock (_gate)
            {
                if (_openTreatmentPublicationReceipt is not null || _openJointRepairPublicationReceipt is not null ||
                    _mortalWoundTreatmentPublicationReservation is not JointRepairPublicationReservationAuthority joint ||
                    !ReferenceEquals(joint.Continuation, continuation) ||
                    reservation is not null && !ReferenceEquals(joint, reservation)) return;
                _woundPlan.InvalidateAll(); _effectPlan.InvalidateAll(); ClearWoundEffectCore(); _commonPlan.InvalidateAll();
                _mortalWoundTreatmentPublicationFingerprint = null; _validatedTreatmentPublicationHandoff = null;
                ClearMortalWoundTreatmentPublicationReservationCore();
                // The exact confirmed resource agreement and full physical graph are retained.
            }
        }
    }
}
