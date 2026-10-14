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

    private sealed partial class AcceptedTurnAuthorityState
    {
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

        private bool OwnsJointRepairPublicationReservation(object continuation, object reservation) =>
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
                if (_openTreatmentPublicationReceipt is not null ||
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
