using System.Runtime.CompilerServices;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal enum MortalWoundTreatmentPublicationReservationStatus
{
    Reserved,
    Exact,
    Conflict
}

internal sealed record MortalWoundTreatmentPublicationReservation(
    MortalWoundTreatmentPublicationReservationStatus Status,
    object? Authority);

internal static class AcceptedTurnAuthorityRegistry
{
    private static readonly ConditionalWeakTable<
        CanonicalRootIdentity,
        RootAuthoritySlot> RootSlots = new();
    private static readonly object ProcedureDicePoolReadCapability = new();
    private static readonly object ProcedureReservationOwnershipCapability = new();
    private static readonly object ProcedureReservationLiveCheckCapability = new();
    private static readonly object TreatmentResourceRegistryCapability = new();
    private static readonly object DeteriorationPolicyAuthorityCapability = new();
    private static readonly object MortalWoundRecoveryPlannerCapability = new();

    internal static bool IsProcedureDicePoolReadCapability(object capability) =>
        ReferenceEquals(capability, ProcedureDicePoolReadCapability) ||
        MortalWoundProcedureClaimRecoveryCoordinator.IsDicePoolReadCapability(
            capability);

    internal static bool IsProcedureReservationOwnershipCapability(object capability) =>
        ReferenceEquals(capability, ProcedureReservationOwnershipCapability);

    internal static bool IsProcedureReservationLiveCheckCapability(object capability) =>
        ReferenceEquals(capability, ProcedureReservationLiveCheckCapability);

    internal static bool IsTreatmentResourceRegistryCapability(object capability) =>
        ReferenceEquals(capability, TreatmentResourceRegistryCapability);

    internal static bool IsDeteriorationPolicyAuthorityCapability(object capability) =>
        ReferenceEquals(capability, DeteriorationPolicyAuthorityCapability);

    internal static bool IsMortalWoundRecoveryPlannerCapability(object capability) =>
        ReferenceEquals(capability, MortalWoundRecoveryPlannerCapability);

    internal static AcceptedMechanicsPlanningResult GetOrBuildCommonValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        AcceptedMechanicsInput input) =>
        GetState(fileSystem, writeLease).GetOrBuildCommonValidated(input);

    internal static bool TryTakeCommonValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        AcceptedMechanicsPlanBinding liveBinding,
        out AcceptedMechanicsPlanningResult result) =>
        GetState(fileSystem, writeLease).TryTakeCommonValidated(
            liveBinding,
            out result);

    internal static void InvalidateCommonValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease) =>
        GetState(fileSystem, writeLease).InvalidateCommonValidated();

    internal static bool HasCommonValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease) =>
        GetState(fileSystem, writeLease).HasCommonValidated();

    internal static bool TryPeekCommonValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        out AcceptedMechanicsPlanBinding binding,
        out AcceptedMechanicsPlanningResult result) =>
        GetState(fileSystem, writeLease).TryPeekCommonValidated(
            out binding,
            out result);

    internal static bool TryRegisterWoundRepairWave(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        WoundRepairPacketAuthority authority,
        IReadOnlyList<WoundRepairPacket> packets) =>
        GetState(fileSystem, writeLease).TryRegisterWoundRepairWave(
            authority,
            packets);

    internal static bool TryTakeWoundRepairPacket(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        WoundRepairPacketAuthority liveAuthority,
        WoundRepairPacketReceipt receipt,
        out WoundRepairPacket packet) =>
        GetState(fileSystem, writeLease).TryTakeWoundRepairPacket(
            liveAuthority,
            receipt,
            out packet);

    internal static bool HasWoundRepairWave(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease) =>
        GetState(fileSystem, writeLease).HasWoundRepairWave();

    internal static EffectAcceptedTurnPlanningResult GetOrBuildEffectValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        EffectAcceptedTurnInput input) =>
        GetState(fileSystem, writeLease).GetOrBuildEffectValidated(input);

    internal static void InvalidateEffectValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease) =>
        GetState(fileSystem, writeLease).InvalidateEffectValidated();

    internal static bool TryPeekEffectValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        out EffectAcceptedTurnPlanBinding binding,
        out EffectAcceptedTurnPlanningResult result) =>
        GetState(fileSystem, writeLease).TryPeekEffectValidated(
            out binding,
            out result);

    internal static bool TryPeekEffectValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        out EffectAcceptedTurnPlanningResult result) =>
        GetState(fileSystem, writeLease).TryPeekEffectValidated(out result);

    internal static WoundEffectBatchPlanningResult
        GetOrBuildWoundEffectValidated(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            WoundPreparedAcceptedTurnPlan prepared,
            EffectAcceptedTurnInput input) =>
        GetState(fileSystem, writeLease).GetOrBuildWoundEffectValidated(
            prepared,
            input);

    internal static WoundAcceptedTurnPreparationResult
        GetOrBuildWoundPreparedValidated(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            WoundAcceptedTurnInput input) =>
        GetState(fileSystem, writeLease).GetOrBuildWoundPrepared(input);

    internal static WoundAcceptedTurnPreparationResult
        GetOrBuildWoundTreatmentContinuationPreparedValidated(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            WoundAcceptedTurnInput input,
            object treatmentContinuationAuthority,
            object reservationAuthority) =>
        GetState(fileSystem, writeLease)
            .GetOrBuildWoundTreatmentContinuationPrepared(
                input,
                treatmentContinuationAuthority,
                reservationAuthority);

    internal static WoundEffectBatchPlanningResult
        GetOrBuildWoundTreatmentContinuationEffectValidated(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            WoundPreparedAcceptedTurnPlan prepared,
            EffectAcceptedTurnInput input,
            object treatmentContinuationAuthority,
            object reservationAuthority) =>
        GetState(fileSystem, writeLease)
            .GetOrBuildWoundEffectValidated(
                prepared,
                input,
                treatmentContinuationAuthority,
                reservationAuthority);

    internal static WoundAcceptedTurnPlanningResult
        GetOrBuildWoundTreatmentContinuationFinalValidated(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            WoundPreparedAcceptedTurnPlan prepared,
            WoundEffectBatchPlanningResult effectResult,
            object treatmentContinuationAuthority,
            object reservationAuthority) =>
        GetState(fileSystem, writeLease)
            .GetOrBuildWoundFinal(
                prepared,
                effectResult,
                treatmentContinuationAuthority,
                reservationAuthority);

    internal static WoundAcceptedTurnPlanningResult GetOrBuildWoundFinalValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        WoundPreparedAcceptedTurnPlan prepared,
        WoundEffectBatchPlanningResult effectResult) =>
        GetState(fileSystem, writeLease).GetOrBuildWoundFinal(
            prepared,
            effectResult);

    internal static AcceptedMechanicsPlanningResult
        GetOrBuildCommonWoundValidated(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            AcceptedMechanicsInput input,
            AcceptedMechanicsWoundStageBundle bundle) =>
        GetState(fileSystem, writeLease).GetOrBuildCommonWoundValidated(
            input,
            bundle);

    internal static MortalWoundTreatmentPublicationReservation
        ReserveMortalWoundTreatmentPublication(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            MortalWoundTreatmentAcceptedStateAuthority acceptedState,
            string semanticFingerprint) =>
        GetState(fileSystem, writeLease).ReserveMortalWoundTreatmentPublication(
            acceptedState,
            semanticFingerprint);

    internal static void AbortMortalWoundTreatmentPublication(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        object? reservationAuthority) =>
        GetState(fileSystem, writeLease).AbortMortalWoundTreatmentPublication(
            reservationAuthority);

    internal static AcceptedMechanicsPlanningResult
        GetOrBuildCommonMortalWoundTreatmentValidated(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            AcceptedMechanicsInput input,
            AcceptedMechanicsWoundStageBundle bundle,
            MortalWoundTreatmentAcceptedStateAuthority acceptedState,
            MortalWoundTreatmentAttemptRequest request,
            MortalWoundTreatmentResolution resolution,
            string semanticFingerprint,
            object continuationAuthority,
            object reservationAuthority) =>
        GetState(fileSystem, writeLease)
            .GetOrBuildCommonMortalWoundTreatmentValidated(
                input,
                bundle,
                acceptedState,
                request,
                resolution,
                semanticFingerprint,
                continuationAuthority,
                reservationAuthority);

    internal static bool TryPeekWoundPrepared(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        out WoundAcceptedTurnPreparationResult result) =>
        GetState(fileSystem, writeLease).TryPeekWoundPrepared(out result);

    internal static bool TryPeekWoundFinal(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        out WoundAcceptedTurnPlanningResult result) =>
        GetState(fileSystem, writeLease).TryPeekWoundFinal(out result);

    internal static void InvalidateAcceptedTurnValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease) =>
        GetState(fileSystem, writeLease).InvalidateAll();

    internal static void RegisterMortalItemsValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        string sessionId,
        string snapshotToken,
        string fingerprint,
        IReadOnlyList<MortalItemAcceptedTurnAuthority.NewCandidate> newCandidates,
        IReadOnlyList<MortalItemAcceptedTurnAuthority.StableCandidate> stableCandidates,
        IReadOnlyList<string> governedItemIds) =>
        GetState(fileSystem, writeLease).RegisterMortalItemsValidated(
            sessionId,
            snapshotToken,
            fingerprint,
            newCandidates,
            stableCandidates,
            governedItemIds);

    internal static IReadOnlyList<ValidationIssue>
        RegisterMortalTreatmentItemsValidated(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            string sessionId,
            string snapshotToken,
            string fingerprint,
            IReadOnlyList<MortalItemAcceptedTurnAuthority.NewCandidate> newCandidates,
            IReadOnlyList<MortalItemAcceptedTurnAuthority.StableCandidate> stableCandidates,
            IReadOnlyList<string> governedItemIds,
            object treatmentContinuationAuthority,
            object reservationAuthority) =>
        GetState(fileSystem, writeLease).RegisterMortalTreatmentItemsValidated(
            sessionId,
            snapshotToken,
            fingerprint,
            newCandidates,
            stableCandidates,
            governedItemIds,
            treatmentContinuationAuthority,
            reservationAuthority);

    internal static bool HasMortalItemsValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease) =>
        GetState(fileSystem, writeLease).HasMortalItemsValidated();

    internal static IReadOnlyList<EffectSourceExport> GetMortalItemEffectSources(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        string sessionId,
        string snapshotToken) =>
        GetState(fileSystem, writeLease).GetMortalItemEffectSources(
            sessionId,
            snapshotToken);

    internal static void InvalidateMortalItemsValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease) =>
        GetState(fileSystem, writeLease).InvalidateMortalItemsValidated();

    internal static IReadOnlySet<EffectSourceOwnerKey> GetMortalItemReplacedSourceOwners(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        string sessionId,
        string snapshotToken) =>
        GetState(fileSystem, writeLease).GetMortalItemReplacedSourceOwners(
            sessionId,
            snapshotToken);

    internal static bool TryGetMortalItemId(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        string sessionId,
        string snapshotToken,
        string creationRef,
        out string itemId) =>
        GetState(fileSystem, writeLease).TryGetMortalItemId(
            sessionId,
            snapshotToken,
            creationRef,
            out itemId);

    internal static bool TryCaptureMortalItemNormalizationSnapshot(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        string sessionId,
        string snapshotToken,
        int turn,
        out MortalItemAcceptedTurnNormalizationSnapshot snapshot) =>
        GetState(fileSystem, writeLease).TryCaptureMortalItemNormalizationSnapshot(
            sessionId,
            snapshotToken,
            turn,
            out snapshot);

    internal static IReadOnlyList<MortalItemAcceptedTurnOwner> GetMortalItemOwners(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        string sessionId,
        string snapshotToken) =>
        GetState(fileSystem, writeLease).GetMortalItemOwners(
            sessionId,
            snapshotToken);

    internal static IReadOnlySet<string> GetMissingGovernedMortalItemIds(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        string sessionId,
        string snapshotToken) =>
        GetState(fileSystem, writeLease).GetMissingGovernedMortalItemIds(
            sessionId,
            snapshotToken);

    internal static MortalWoundTreatmentAcceptedStateAuthorityResult
        BindMortalWoundTreatmentAcceptedState(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            MortalWoundTreatmentAcceptedStateAuthorityResult candidate)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(candidate);

        if (IsValidCandidateDetached(fileSystem, writeLease, candidate))
        {
            return MortalWoundTreatmentAcceptedStateAuthority
                .RegistryCandidateBindingFailure("detached candidate authority");
        }

        try
        {
            return GetState(fileSystem, writeLease)
                .BindMortalWoundTreatmentAcceptedState(
                    fileSystem,
                    writeLease,
                    candidate);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ObjectDisposedException or
                ArgumentException or IOException or UnauthorizedAccessException)
        {
            return candidate.IsValid
                ? MortalWoundTreatmentAcceptedStateAuthority
                    .RegistryCandidateBindingFailure(exception.GetType().Name)
                : Detach(candidate);
        }
    }

    internal static bool IsCurrentMortalWoundTreatmentAcceptedState(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        MortalWoundTreatmentAcceptedStateAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(authority);
        try
        {
            return GetState(fileSystem, writeLease)
                .IsCurrentMortalWoundTreatmentAcceptedState(
                    fileSystem,
                    writeLease,
                    authority);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ObjectDisposedException or
                ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal static bool HasLiveMortalWoundProcedureReservationAgreement(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundProcedureCheckAuthority procedure)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(acceptedState);
        ArgumentNullException.ThrowIfNull(procedure);
        try
        {
            return GetState(fileSystem, writeLease)
                .HasLiveMortalWoundProcedureReservationAgreement(
                    fileSystem,
                    writeLease,
                    acceptedState,
                    procedure);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ObjectDisposedException or
                ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal static MortalWoundProcedureClaimRecoveryResult
        RestoreMortalWoundProcedureClaims(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            MortalWoundTreatmentAcceptedStateAuthority acceptedState,
            WoundHistoryParseResult history,
            object recoveryCapability,
            IReadOnlyList<MortalWoundTreatmentAttemptRequest> requests,
            IReadOnlyList<MortalWoundTreatmentAttemptRequest> heldRequests,
            IReadOnlyList<MortalWoundTreatmentAttemptRequest> finalizedRequests)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(acceptedState);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(recoveryCapability);
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(heldRequests);
        ArgumentNullException.ThrowIfNull(finalizedRequests);
        if (!MortalWoundTreatmentAcceptedStateAuthority
                .IsPersistedProcedureClaimRecoveryCapability(recoveryCapability))
        {
            return ProcedureClaimRecoveryFailure(
                "mortal_wound_treatment_procedure_claim_recovery_authority_invalid",
                "accepted-state-owned current durable-root recovery authority",
                "missing recovery capability");
        }
        try
        {
            return GetState(fileSystem, writeLease)
                .RestoreMortalWoundProcedureClaims(
                    fileSystem,
                    writeLease,
                    acceptedState,
                    history,
                    requests,
                    heldRequests,
                    finalizedRequests,
                    ProcedureDicePoolReadCapability);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ObjectDisposedException or
                ArgumentException or IOException or UnauthorizedAccessException or
                OverflowException)
        {
            return ProcedureClaimRecoveryFailure(
                "mortal_wound_treatment_procedure_claim_recovery_invalid",
                "one current atomic persisted-request claim reconstruction",
                exception.GetType().Name);
        }
    }

    internal static MortalWoundDeteriorationPolicyAuthorityResult
        CreateMortalWoundDeteriorationPolicyAuthority(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            WoundAcceptedTurnBinding? binding,
            string? woundId,
            string? policyRef)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        try
        {
            return GetState(fileSystem, writeLease)
                .CreateMortalWoundDeteriorationPolicyAuthority(
                    fileSystem,
                    writeLease,
                    binding,
                    woundId,
                    policyRef,
                    DeteriorationPolicyAuthorityCapability);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ObjectDisposedException or
                ArgumentException or IOException or UnauthorizedAccessException)
        {
            return MortalWoundDeteriorationPolicyAuthority.InvalidAuthority(
                "recovery.deteriorationPolicy",
                exception.GetType().Name);
        }
    }

    internal static MortalWoundRecoveryPlanningResult PlanMortalWoundRecovery(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        WoundAcceptedTurnBinding? binding,
        string? woundId)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        try
        {
            return GetState(fileSystem, writeLease).PlanMortalWoundRecovery(
                fileSystem,
                writeLease,
                binding,
                woundId,
                MortalWoundRecoveryPlannerCapability);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ObjectDisposedException or
                ArgumentException or IOException or UnauthorizedAccessException or
                OverflowException)
        {
            return MortalWoundRecoveryPlanner.RegistryFailure(
                exception.GetType().Name);
        }
    }

    internal static MortalWoundDeteriorationPolicyAuthorityResult
        CreateMortalWoundDeteriorationPolicyAuthority(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            MortalWoundTreatmentAcceptedStateAuthority acceptedState,
            MortalWoundTreatmentAttemptCoordinates? coordinates,
            string? policyRef)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(acceptedState);
        try
        {
            return GetState(fileSystem, writeLease)
                .CreateMortalWoundDeteriorationPolicyAuthority(
                    fileSystem,
                    writeLease,
                    acceptedState,
                    coordinates,
                    policyRef,
                    DeteriorationPolicyAuthorityCapability);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ObjectDisposedException or
                ArgumentException or IOException or UnauthorizedAccessException)
        {
            return MortalWoundDeteriorationPolicyAuthority.InvalidAuthority(
                acceptedState.WoundSourcePath + ".recovery.deteriorationPolicy",
                exception.GetType().Name);
        }
    }

    internal static MortalWoundProcedureReservationSetResult
        ReserveMortalWoundProcedureReservations(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            MortalWoundTreatmentAcceptedStateAuthority acceptedState,
            object reservationCapability,
            MortalWoundTreatmentAttemptCoordinates coordinates,
            string rollMode,
            string rollActorKind,
            string rollActorId)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(acceptedState);
        ArgumentNullException.ThrowIfNull(coordinates);
        if (!MortalWoundProcedureCheckAuthority.IsProcedureReservationCapability(
                reservationCapability))
        {
            return MortalWoundProcedureReservationSetRegistryFailure(
                "procedure-authority-owned compound reservation access",
                "missing procedure reservation authority");
        }
        try
        {
            return GetState(fileSystem, writeLease)
                .ReserveMortalWoundProcedureReservations(
                    fileSystem,
                    writeLease,
                    acceptedState,
                    coordinates,
                    rollMode,
                    rollActorKind,
                    rollActorId,
                    ProcedureDicePoolReadCapability);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ObjectDisposedException or
                ArgumentException or IOException or UnauthorizedAccessException)
        {
            return MortalWoundProcedureReservationSetRegistryFailure(
                "one current accepted-state authority and active canonical lease",
                exception.GetType().Name);
        }
    }

    internal static bool ReleaseMortalWoundProcedureReservations(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundProcedureDiceReservation diceReservation,
        MortalWoundCriticalReactionReservation? criticalReactionReservation,
        MortalWoundCriticalReactionReservationAgreement? criticalReactionAgreement)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(acceptedState);
        ArgumentNullException.ThrowIfNull(diceReservation);
        try
        {
            return GetState(fileSystem, writeLease)
                .ReleaseMortalWoundProcedureReservations(
                    fileSystem,
                    writeLease,
                    acceptedState,
                    diceReservation,
                    criticalReactionReservation,
                    criticalReactionAgreement);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ObjectDisposedException or
                ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal static bool RollbackNewMortalWoundProcedureReservations(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundProcedureDiceReservation diceReservation,
        MortalWoundCriticalReactionReservation? criticalReactionReservation,
        MortalWoundCriticalReactionReservationAgreement? criticalReactionAgreement,
        MortalWoundProcedureReservationOwnership ownership)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(acceptedState);
        ArgumentNullException.ThrowIfNull(diceReservation);
        try
        {
            return GetState(fileSystem, writeLease)
                .RollbackNewMortalWoundProcedureReservations(
                    fileSystem,
                    writeLease,
                    acceptedState,
                    diceReservation,
                    criticalReactionReservation,
                    criticalReactionAgreement,
                    ownership);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ObjectDisposedException or
                ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal static MortalWoundTreatmentResourceReservationResult
        ReserveMortalWoundTreatmentResources(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            MortalWoundTreatmentAcceptedStateAuthority acceptedState,
            object reservationCapability,
            MortalWoundTreatmentAttemptCoordinates coordinates,
            string mode,
            MortalWoundTreatmentModeAuthority modeAuthority,
            MortalWoundTreatmentResourceReservationAuthority candidate)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(acceptedState);
        ArgumentNullException.ThrowIfNull(coordinates);
        ArgumentNullException.ThrowIfNull(modeAuthority);
        ArgumentNullException.ThrowIfNull(candidate);
        try
        {
            return GetState(fileSystem, writeLease)
                .ReserveMortalWoundTreatmentResources(
                    fileSystem,
                    writeLease,
                    acceptedState,
                    reservationCapability,
                    coordinates,
                    mode,
                    modeAuthority,
                    candidate);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ObjectDisposedException or
                ArgumentException or IOException or UnauthorizedAccessException or
                OverflowException)
        {
            return MortalWoundTreatmentResourceReservationRegistry.Invalid(
                "mortal_wound_treatment_resource_reservation_authority_invalid",
                "one current accepted-state authority and active canonical lease",
                exception.GetType().Name);
        }
    }

    internal static bool RollbackNewMortalWoundTreatmentResources(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        object reservationCapability,
        MortalWoundTreatmentResourceReservationOwnership ownership,
        MortalWoundTreatmentResourceReservationAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(acceptedState);
        ArgumentNullException.ThrowIfNull(ownership);
        ArgumentNullException.ThrowIfNull(authority);
        try
        {
            return GetState(fileSystem, writeLease)
                .RollbackNewMortalWoundTreatmentResources(
                    fileSystem,
                    writeLease,
                    acceptedState,
                    reservationCapability,
                    ownership,
                    authority);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ObjectDisposedException or
                ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal static MortalWoundTreatmentResourceLifecycleResult
        ConfirmPersistedMortalWoundTreatmentResources(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            object lifecycleCapability,
            IReadOnlyList<MortalWoundTreatmentAttemptRequest> requests) =>
        GetState(fileSystem, writeLease).ConfirmPersistedTreatmentResources(
            lifecycleCapability,
            requests);

    internal static MortalWoundTreatmentResourceLifecycleResult
        ReleaseMortalWoundTreatmentResources(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            object lifecycleCapability,
            IReadOnlyList<MortalWoundTreatmentAttemptRequest> requests,
            string reason) =>
        GetState(fileSystem, writeLease).ReleaseTreatmentResources(
            lifecycleCapability,
            requests,
            reason);

    internal static MortalWoundTreatmentResourceLifecycleResult
        ConfirmPersistedMortalWoundTreatmentResources(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            MortalWoundTreatmentAcceptedStateAuthority acceptedState,
            object lifecycleCapability,
            IReadOnlyList<MortalWoundTreatmentAttemptRequest> requests)
    {
        var state = GetState(fileSystem, writeLease);
        return state.IsCurrentAcceptedState(fileSystem, writeLease, acceptedState)
            ? state.ConfirmPersistedTreatmentResources(lifecycleCapability, requests)
            : MortalWoundTreatmentResourceReservationRegistry.LifecycleFailure(
                "mortal_wound_treatment_resource_lifecycle_stale",
                "the exact current accepted-state authority",
                "stale or foreign authority");
    }

    internal static MortalWoundTreatmentResourceLifecycleResult
        ReleaseMortalWoundTreatmentResources(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            MortalWoundTreatmentAcceptedStateAuthority acceptedState,
            object lifecycleCapability,
            IReadOnlyList<MortalWoundTreatmentAttemptRequest> requests,
            string reason)
    {
        var state = GetState(fileSystem, writeLease);
        return state.IsCurrentAcceptedState(fileSystem, writeLease, acceptedState)
            ? state.ReleaseTreatmentResources(lifecycleCapability, requests, reason)
            : MortalWoundTreatmentResourceReservationRegistry.LifecycleFailure(
                "mortal_wound_treatment_resource_lifecycle_stale",
                "the exact current accepted-state authority",
                "stale or foreign authority");
    }

    internal static MortalWoundTreatmentResourceLifecycleResult
        CommitMortalWoundTreatmentResources(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            MortalWoundTreatmentAcceptedStateAuthority acceptedState,
            object lifecycleCapability,
            MortalWoundTreatmentResourceFinalization finalization)
    {
        var state = GetState(fileSystem, writeLease);
        return state.IsCurrentAcceptedState(fileSystem, writeLease, acceptedState)
            ? state.CommitTreatmentResources(lifecycleCapability, finalization)
            : MortalWoundTreatmentResourceReservationRegistry.LifecycleFailure(
                "mortal_wound_treatment_resource_lifecycle_stale",
                "the exact current accepted-state authority",
                "stale or foreign authority");
    }

    private static MortalWoundProcedureReservationSetResult
        MortalWoundProcedureReservationSetRegistryFailure(
            string expected,
            string actual) => new(
            false,
            Array.AsReadOnly(new[]
            {
                new ValidationIssue(
                    LiveTurnPreparationService.TurnRequestPath,
                    IssueSeverity.Error,
                    "The accepted die and Fate reservations for the Mortal wound procedure cannot be trusted.",
                    code: "mortal_wound_treatment_procedure_reservation_authority_invalid",
                    actor: "Client",
                    section: "wound_materialization",
                    expected: expected,
                    actual: actual)
            }),
            null,
            null);

    private static MortalWoundProcedureClaimRecoveryResult
        ProcedureClaimRecoveryFailure(
            string code,
            string expected,
            string actual) => new(
            false,
            Array.AsReadOnly(new[]
            {
                new ValidationIssue(
                    LiveTurnPreparationService.TurnRequestPath,
                    IssueSeverity.Error,
                    "The persisted die and Fate claims for Mortal wound treatment cannot be restored.",
                    code: code,
                    actor: "Client",
                    section: "wound_materialization",
                    expected: expected,
                    actual: actual)
            }),
            Array.Empty<MortalWoundTreatmentAttemptRequest>(),
            Array.Empty<MortalWoundTreatmentAttemptRequest>(),
            Array.Empty<MortalWoundTreatmentAttemptRequest>());

    private static bool IsValidCandidateDetached(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        MortalWoundTreatmentAcceptedStateAuthorityResult candidate) =>
        candidate.IsValid &&
        (candidate.Authority is null ||
         candidate.Issues.Count != 0 ||
         !candidate.Authority.IsLeaseBoundTo(fileSystem, writeLease));

    private static MortalWoundTreatmentAcceptedStateAuthorityResult Detach(
        MortalWoundTreatmentAcceptedStateAuthorityResult candidate) =>
        new(
            candidate.IsValid,
            Array.AsReadOnly(candidate.Issues.ToArray()),
            candidate.Authority);

    private static AcceptedTurnAuthorityState GetState(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        var generation = fileSystem.GetOrCreateSessionGeneration(writeLease);
        var identity = fileSystem.CanonicalRootAuthorityIdentity;
        var revision = identity.SessionGenerationRevision;
        var slot = RootSlots.GetValue(
            identity,
            static _ => new RootAuthoritySlot());
        return slot.GetForGeneration(generation, revision);
    }

    private sealed class RootAuthoritySlot
    {
        private readonly object _gate = new();
        private string? _generation;
        private long _revision = -1;
        private AcceptedTurnAuthorityState? _state;

        internal AcceptedTurnAuthorityState GetForGeneration(
            string sessionGeneration,
            long sessionGenerationRevision)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sessionGeneration);
            lock (_gate)
            {
                if (_state != null &&
                    _revision == sessionGenerationRevision &&
                    string.Equals(
                        _generation,
                        sessionGeneration,
                        StringComparison.Ordinal))
                {
                    return _state;
                }

                _state = new AcceptedTurnAuthorityState();
                _generation = sessionGeneration;
                _revision = sessionGenerationRevision;
                return _state;
            }
        }
    }

    private sealed class AcceptedTurnAuthorityState
    {
        private const string WoundPlanPath =
            "game_state/wounds/accepted_turn_plan";

        private sealed class TreatmentPublicationReservationAuthority
        {
        }

        private readonly object _gate = new();
        private readonly object _authorityStateToken = new();
        private readonly AcceptedMechanicsPlanCache _commonPlan;
        private readonly EffectAcceptedTurnPlanCache _effectPlan;
        private readonly WoundAcceptedTurnPlanCache _woundPlan;
        private readonly MortalItemAcceptedTurnAuthority.Cache _mortalItems;
        private MortalWoundProcedureDiceReservationRegistry _procedureDice = new();
        private MortalWoundCriticalReactionReservationRegistry
            _criticalReactions = new();
        private MortalWoundTreatmentResourceReservationRegistry
            _treatmentResources = new();
        private MortalWoundTreatmentAcceptedStateAuthority?
            _mortalWoundTreatmentAcceptedState;
        private bool _procedureClaimRecoveryCompleted;
        private MortalWoundTreatmentAttemptRequest[] _recoveredTreatmentRequests =
            Array.Empty<MortalWoundTreatmentAttemptRequest>();
        private MortalWoundTreatmentAttemptRequest[]
            _recoveredHeldTreatmentRequests =
                Array.Empty<MortalWoundTreatmentAttemptRequest>();
        private MortalWoundTreatmentAttemptRequest[]
            _recoveredFinalizedTreatmentRequests =
                Array.Empty<MortalWoundTreatmentAttemptRequest>();
        private object? _woundEffectStageToken;
        private string? _woundEffectFingerprint;
        private WoundEffectBatchPlanningResult? _woundEffectResult;
        private string? _mortalWoundTreatmentPublicationFingerprint;
        private object? _mortalWoundTreatmentPublicationReservation;
        private string? _mortalWoundTreatmentReservedFingerprint;

        internal AcceptedTurnAuthorityState(
            AcceptedMechanicsPlanCache? commonPlan = null,
            EffectAcceptedTurnPlanCache? effectPlan = null,
            WoundAcceptedTurnPlanCache? woundPlan = null,
            MortalItemAcceptedTurnAuthority.Cache? mortalItems = null)
        {
            _commonPlan = commonPlan ?? new AcceptedMechanicsPlanCache(
                AcceptedMechanicsPlanner.BuildAcceptedPlan);
            _effectPlan = effectPlan ?? new EffectAcceptedTurnPlanCache();
            _woundPlan = woundPlan ?? new WoundAcceptedTurnPlanCache();
            _mortalItems = mortalItems ??
                new MortalItemAcceptedTurnAuthority.Cache();
        }

        internal MortalWoundTreatmentAcceptedStateAuthorityResult
            BindMortalWoundTreatmentAcceptedState(
                FileSystemManager fileSystem,
                FileSystemManager.CanonicalWriteLease writeLease,
                MortalWoundTreatmentAcceptedStateAuthorityResult candidate)
        {
            lock (_gate)
            {
                if (!candidate.IsValid ||
                    candidate.Authority is null ||
                    candidate.Issues.Count != 0)
                {
                    _procedureDice.InvalidateAll();
                    _criticalReactions.InvalidateAll();
                    _treatmentResources.InvalidateAll();
                    ResetProcedureClaimRecovery();
                    _mortalWoundTreatmentAcceptedState = null;
                    return AcceptedTurnAuthorityRegistry.Detach(candidate);
                }

                if (!candidate.Authority.IsLeaseBoundTo(fileSystem, writeLease))
                {
                    _procedureDice.InvalidateAll();
                    _criticalReactions.InvalidateAll();
                    _treatmentResources.InvalidateAll();
                    ResetProcedureClaimRecovery();
                    _mortalWoundTreatmentAcceptedState = null;
                    return MortalWoundTreatmentAcceptedStateAuthority
                        .RegistryCandidateBindingFailure(
                            "detached candidate authority");
                }

                if (_mortalWoundTreatmentAcceptedState is not null &&
                    _mortalWoundTreatmentAcceptedState.IsLeaseBoundTo(
                        fileSystem,
                        writeLease) &&
                    _mortalWoundTreatmentAcceptedState.SemanticallyEquals(
                        candidate.Authority))
                {
                    return new MortalWoundTreatmentAcceptedStateAuthorityResult(
                        true,
                        Array.Empty<ValidationIssue>(),
                        _mortalWoundTreatmentAcceptedState);
                }

                _procedureDice.InvalidateAll();
                _criticalReactions.InvalidateAll();
                _treatmentResources.InvalidateAll();
                ResetProcedureClaimRecovery();
                _mortalWoundTreatmentAcceptedState = candidate.Authority;
                var recovery = candidate.Authority.RestorePersistedTreatmentRequests(
                    new WoundHistoryParseResult(
                        candidate.Authority.History,
                        Array.Empty<ValidationIssue>()));
                if (!recovery.IsValid)
                {
                    _procedureDice.InvalidateAll();
                    _criticalReactions.InvalidateAll();
                    _treatmentResources.InvalidateAll();
                    ResetProcedureClaimRecovery();
                    _mortalWoundTreatmentAcceptedState = null;
                    return new MortalWoundTreatmentAcceptedStateAuthorityResult(
                        false,
                        recovery.Issues,
                        null);
                }
                return AcceptedTurnAuthorityRegistry.Detach(candidate);
            }
        }

        internal MortalWoundProcedureReservationSetResult
            ReserveMortalWoundProcedureReservations(
                FileSystemManager fileSystem,
                FileSystemManager.CanonicalWriteLease writeLease,
                MortalWoundTreatmentAcceptedStateAuthority acceptedState,
                MortalWoundTreatmentAttemptCoordinates coordinates,
                string rollMode,
                string rollActorKind,
                string rollActorId,
                object dicePoolReadCapability)
        {
            lock (_gate)
            {
                if (!ReferenceEquals(_mortalWoundTreatmentAcceptedState, acceptedState) ||
                    !acceptedState.IsLeaseBoundTo(fileSystem, writeLease) ||
                    !coordinates.AgreesWithAcceptedStateSemantics(acceptedState) ||
                    !acceptedState.TryReadProcedureDicePool(
                        dicePoolReadCapability,
                        out var acceptedD20EventValues,
                        out var poolFingerprint))
                {
                    return AcceptedTurnAuthorityRegistry
                        .MortalWoundProcedureReservationSetRegistryFailure(
                            "the exact current accepted state and matching attempt coordinates",
                            "stale, foreign, or mismatched authority");
                }

                var dice = _procedureDice.Reserve(
                    dicePoolReadCapability,
                    coordinates,
                    rollMode,
                    acceptedD20EventValues,
                    poolFingerprint);
                if (!dice.IsValid || dice.Reservation is null)
                {
                    return new MortalWoundProcedureReservationSetResult(
                        false,
                        dice.Issues,
                        null,
                        null);
                }

                if (!RequiresPlayerFateReservation(
                        dice.Reservation,
                        rollMode,
                        rollActorKind,
                        rollActorId))
                {
                    var ownership = MortalWoundProcedureReservationOwnership.Create(
                        ProcedureReservationOwnershipCapability,
                        dice.Reservation,
                        criticalReactionReservation: null,
                        criticalReactionAgreement: null,
                        dice.WasCreated,
                        criticalReactionWasCreated: false);
                    return new MortalWoundProcedureReservationSetResult(
                        true,
                        Array.Empty<ValidationIssue>(),
                        dice.Reservation,
                        null,
                        null,
                        ownership);
                }

                MortalWoundCriticalReactionReservationResult reaction;
                try
                {
                    reaction = _criticalReactions.Reserve(
                        coordinates,
                        acceptedState.EffectMechanics.FateShieldReactionCandidates);
                }
                catch (Exception exception) when (
                    exception is ArgumentException or InvalidOperationException or
                        OverflowException)
                {
                    if (dice.WasCreated)
                        _procedureDice.Release(dice.Reservation);
                    return AcceptedTurnAuthorityRegistry
                        .MortalWoundProcedureReservationSetRegistryFailure(
                            "one detached eligible Fate candidate agreement",
                            exception.GetType().Name);
                }
                if (!reaction.IsValid)
                {
                    if (dice.WasCreated)
                        _procedureDice.Release(dice.Reservation);
                    return new MortalWoundProcedureReservationSetResult(
                        false,
                        reaction.Issues,
                        null,
                        null);
                }

                var reservationOwnership =
                    MortalWoundProcedureReservationOwnership.Create(
                        ProcedureReservationOwnershipCapability,
                        dice.Reservation,
                        reaction.Reservation,
                        reaction.Agreement,
                        dice.WasCreated,
                        reaction.WasCreated);
                return new MortalWoundProcedureReservationSetResult(
                    true,
                    Array.Empty<ValidationIssue>(),
                    dice.Reservation,
                    reaction.Reservation,
                    reaction.Agreement,
                    reservationOwnership);
            }
        }

        internal MortalWoundProcedureClaimRecoveryResult
            RestoreMortalWoundProcedureClaims(
                FileSystemManager fileSystem,
                FileSystemManager.CanonicalWriteLease writeLease,
                MortalWoundTreatmentAcceptedStateAuthority acceptedState,
                WoundHistoryParseResult history,
                IReadOnlyList<MortalWoundTreatmentAttemptRequest> requests,
                IReadOnlyList<MortalWoundTreatmentAttemptRequest> heldRequests,
                IReadOnlyList<MortalWoundTreatmentAttemptRequest> finalizedRequests,
                object dicePoolReadCapability)
        {
            lock (_gate)
            {
                if (!ReferenceEquals(_mortalWoundTreatmentAcceptedState, acceptedState) ||
                    !acceptedState.IsLeaseBoundTo(fileSystem, writeLease) ||
                    !acceptedState.MatchesCompleteHistory(history) ||
                    !acceptedState.TryReadProcedureDicePool(
                        dicePoolReadCapability,
                        out _,
                        out _))
                {
                    return AcceptedTurnAuthorityRegistry.ProcedureClaimRecoveryFailure(
                        "mortal_wound_treatment_procedure_claim_recovery_stale",
                        "the exact current accepted state, history, and d20 pool",
                        "stale, foreign, or mismatched recovery input");
                }

                if (_procedureClaimRecoveryCompleted)
                {
                    return PersistedRequestSetAgrees(
                            requests,
                            _recoveredTreatmentRequests)
                        ? new MortalWoundProcedureClaimRecoveryResult(
                            true,
                            Array.Empty<ValidationIssue>(),
                            _recoveredTreatmentRequests,
                            _recoveredHeldTreatmentRequests,
                            _recoveredFinalizedTreatmentRequests)
                        : AcceptedTurnAuthorityRegistry.ProcedureClaimRecoveryFailure(
                            "mortal_wound_treatment_procedure_claim_recovery_conflict",
                            "the exact request set used by the one completed recovery phase",
                            "persisted request set changed after recovery");
                }
                if (!_procedureDice.IsEmpty ||
                    !_criticalReactions.IsEmpty ||
                    !_treatmentResources.IsEmpty)
                {
                    return AcceptedTurnAuthorityRegistry.ProcedureClaimRecoveryFailure(
                        "mortal_wound_treatment_procedure_claim_recovery_late",
                        "claim recovery before any new live treatment reservation",
                        "one or more live reservations already exist");
                }

                var reconstruction =
                    new MortalWoundProcedureClaimRecoveryCoordinator().Restore(
                        acceptedState,
                        requests);
                if (!reconstruction.IsValid ||
                    reconstruction.DiceRegistry is null ||
                    reconstruction.ReactionRegistry is null)
                {
                    return new MortalWoundProcedureClaimRecoveryResult(
                        false,
                        reconstruction.Issues,
                        Array.Empty<MortalWoundTreatmentAttemptRequest>(),
                        Array.Empty<MortalWoundTreatmentAttemptRequest>(),
                        Array.Empty<MortalWoundTreatmentAttemptRequest>());
                }
                var restoredDice = reconstruction.DiceRegistry;
                var restoredReactions = reconstruction.ReactionRegistry;
                var restoredRequests = reconstruction.Requests.ToArray();
                var restoredHeldRequests =
                    new List<MortalWoundTreatmentAttemptRequest>();
                var restoredFinalizedRequests =
                    new List<MortalWoundTreatmentAttemptRequest>();
                var restoredResources =
                    new MortalWoundTreatmentResourceReservationRegistry();
                for (var index = 0; index < restoredRequests.Length; index++)
                {
                    var request = restoredRequests[index];
                    var isHeld = heldRequests.Any(candidate =>
                        SameLogicalRequest(candidate, request));
                    var isFinalized = finalizedRequests.Any(candidate =>
                        SameLogicalRequest(candidate, request));
                    if (isHeld == isFinalized)
                    {
                        return AcceptedTurnAuthorityRegistry.ProcedureClaimRecoveryFailure(
                            "mortal_wound_treatment_resource_recovery_origin_invalid",
                            "one exact held or finalized origin classification per request",
                            request.Coordinates.OperationKey);
                    }

                    var resource = MortalWoundTreatmentResourceComposer
                        .RehydratePersistedAuthority(request);
                    if (!resource.IsValid || resource.Authority is null)
                    {
                        return new MortalWoundProcedureClaimRecoveryResult(
                            false,
                            resource.Issues,
                            Array.Empty<MortalWoundTreatmentAttemptRequest>(),
                            Array.Empty<MortalWoundTreatmentAttemptRequest>(),
                            Array.Empty<MortalWoundTreatmentAttemptRequest>());
                    }
                    var attached = request.AttachRestoredResourceAuthority(
                        resource.Authority);
                    if (attached is null)
                    {
                        return AcceptedTurnAuthorityRegistry.ProcedureClaimRecoveryFailure(
                            "mortal_wound_treatment_resource_recovery_invalid",
                            "one request preserving its complete public resource seal",
                            request.Coordinates.OperationKey);
                    }

                    var restored = isHeld
                        ? restoredResources.RestoreConfirmed(
                            TreatmentResourceRegistryCapability,
                            attached,
                            resource.Authority)
                        : restoredResources.RestoreFinalized(
                            TreatmentResourceRegistryCapability,
                            attached,
                            resource.Authority);
                    if (!restored.IsValid)
                    {
                        return new MortalWoundProcedureClaimRecoveryResult(
                            false,
                            restored.Issues,
                            Array.Empty<MortalWoundTreatmentAttemptRequest>(),
                            Array.Empty<MortalWoundTreatmentAttemptRequest>(),
                            Array.Empty<MortalWoundTreatmentAttemptRequest>());
                    }
                    restoredRequests[index] = attached;
                    if (isHeld)
                        restoredHeldRequests.Add(attached);
                    else
                        restoredFinalizedRequests.Add(attached);
                }

                var previousDice = _procedureDice;
                var previousReactions = _criticalReactions;
                var previousResources = _treatmentResources;
                _procedureDice = restoredDice;
                _criticalReactions = restoredReactions;
                _treatmentResources = restoredResources;
                try
                {
                    foreach (var request in restoredHeldRequests)
                    {
                        var mismatch = !string.Equals(
                                request.Coordinates.AcceptedStateFingerprint,
                                acceptedState.AcceptedStateFingerprint,
                                StringComparison.Ordinal)
                            ? "accepted_state_fingerprint"
                            : null;
                        var routes = acceptedState.TreatmentDefinition.Routes.Where(route =>
                            string.Equals(
                                route.RouteId,
                                request.Coordinates.RouteId,
                                StringComparison.Ordinal))
                            .ToArray();
                        mismatch ??= routes.Length == 1
                            ? MortalWoundTreatmentFreshAuthorityValidator.FindMismatch(
                                request,
                                history,
                                acceptedState.CurrentWound,
                                acceptedState,
                                routes[0])
                            : "route";
                        if (mismatch is null)
                            continue;
                        _procedureDice = previousDice;
                        _criticalReactions = previousReactions;
                        _treatmentResources = previousResources;
                        return AcceptedTurnAuthorityRegistry.ProcedureClaimRecoveryFailure(
                            "mortal_wound_treatment_procedure_claim_recovery_fresh_mismatch",
                            "every restored request matching fresh accepted-state authority",
                            mismatch);
                    }
                }
                catch
                {
                    _procedureDice = previousDice;
                    _criticalReactions = previousReactions;
                    _treatmentResources = previousResources;
                    throw;
                }

                _procedureClaimRecoveryCompleted = true;
                _recoveredTreatmentRequests = restoredRequests.ToArray();
                _recoveredHeldTreatmentRequests = restoredHeldRequests.ToArray();
                _recoveredFinalizedTreatmentRequests =
                    restoredFinalizedRequests.ToArray();

                return new MortalWoundProcedureClaimRecoveryResult(
                    true,
                    Array.Empty<ValidationIssue>(),
                    _recoveredTreatmentRequests,
                    _recoveredHeldTreatmentRequests,
                    _recoveredFinalizedTreatmentRequests);
            }
        }

        private static bool PersistedRequestSetAgrees(
            IReadOnlyList<MortalWoundTreatmentAttemptRequest> left,
            IReadOnlyList<MortalWoundTreatmentAttemptRequest> right)
        {
            if (left.Count != right.Count)
                return false;
            for (var index = 0; index < left.Count; index++)
            {
                var leftRequest = left[index];
                var rightRequest = right[index];
                if (!string.Equals(
                        leftRequest.Coordinates.OperationKey,
                        rightRequest.Coordinates.OperationKey,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        leftRequest.Coordinates.AttemptId,
                        rightRequest.Coordinates.AttemptId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        leftRequest.RequestFingerprint,
                        rightRequest.RequestFingerprint,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool SameLogicalRequest(
            MortalWoundTreatmentAttemptRequest left,
            MortalWoundTreatmentAttemptRequest right) =>
            string.Equals(
                left.Coordinates.OperationKey,
                right.Coordinates.OperationKey,
                StringComparison.Ordinal) &&
            string.Equals(
                left.Coordinates.AttemptId,
                right.Coordinates.AttemptId,
                StringComparison.Ordinal) &&
            string.Equals(
                left.RequestFingerprint,
                right.RequestFingerprint,
                StringComparison.Ordinal);

        private void ResetProcedureClaimRecovery()
        {
            _procedureClaimRecoveryCompleted = false;
            _recoveredTreatmentRequests =
                Array.Empty<MortalWoundTreatmentAttemptRequest>();
            _recoveredHeldTreatmentRequests =
                Array.Empty<MortalWoundTreatmentAttemptRequest>();
            _recoveredFinalizedTreatmentRequests =
                Array.Empty<MortalWoundTreatmentAttemptRequest>();
        }

        internal MortalWoundTreatmentResourceReservationResult
            ReserveMortalWoundTreatmentResources(
                FileSystemManager fileSystem,
                FileSystemManager.CanonicalWriteLease writeLease,
                MortalWoundTreatmentAcceptedStateAuthority acceptedState,
                object reservationCapability,
                MortalWoundTreatmentAttemptCoordinates coordinates,
                string mode,
                MortalWoundTreatmentModeAuthority modeAuthority,
                MortalWoundTreatmentResourceReservationAuthority candidate)
        {
            lock (_gate)
            {
                if (!MortalWoundTreatmentResourceComposer
                        .IsResourceReservationCapability(reservationCapability) ||
                    !ReferenceEquals(
                        _mortalWoundTreatmentAcceptedState,
                        acceptedState) ||
                    !acceptedState.IsLeaseBoundTo(fileSystem, writeLease) ||
                    !coordinates.AgreesWithAcceptedStateSemantics(acceptedState))
                {
                    return MortalWoundTreatmentResourceReservationRegistry.Invalid(
                        "mortal_wound_treatment_resource_reservation_authority_invalid",
                        "the exact current accepted state and matching attempt coordinates",
                        "stale, foreign, or mismatched authority");
                }

                if (string.Equals(mode, "procedure", StringComparison.Ordinal) &&
                    (modeAuthority is not MortalWoundProcedureCheckAuthority procedure ||
                     !procedure.HasLiveReservationAgreement(
                         ProcedureReservationLiveCheckCapability,
                         _procedureDice,
                         _criticalReactions)))
                {
                    return MortalWoundTreatmentResourceReservationRegistry.Invalid(
                        "mortal_wound_treatment_resource_procedure_reservation_invalid",
                        "the exact live private die and Fate reservation agreement",
                        "released, stale, foreign, or mismatched procedure authority");
                }

                return _treatmentResources.Reserve(
                    TreatmentResourceRegistryCapability,
                    coordinates,
                    mode,
                    candidate);
            }
        }

        internal bool RollbackNewMortalWoundTreatmentResources(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            MortalWoundTreatmentAcceptedStateAuthority acceptedState,
            object reservationCapability,
            MortalWoundTreatmentResourceReservationOwnership ownership,
            MortalWoundTreatmentResourceReservationAuthority authority)
        {
            lock (_gate)
            {
                return MortalWoundTreatmentResourceComposer
                           .IsResourceReservationCapability(reservationCapability) &&
                       ReferenceEquals(
                           _mortalWoundTreatmentAcceptedState,
                           acceptedState) &&
                       acceptedState.IsLeaseBoundTo(fileSystem, writeLease) &&
                       _treatmentResources.RollbackNew(
                           TreatmentResourceRegistryCapability,
                           ownership,
                           authority);
            }
        }

        internal MortalWoundTreatmentResourceLifecycleResult
            ConfirmPersistedTreatmentResources(
                object lifecycleCapability,
                IReadOnlyList<MortalWoundTreatmentAttemptRequest> requests)
        {
            lock (_gate)
            {
                if (!MortalWoundTreatmentResourceComposer
                        .IsResourceReservationCapability(lifecycleCapability))
                {
                    return MortalWoundTreatmentResourceReservationRegistry
                        .LifecycleFailure(
                            "mortal_wound_treatment_resource_lifecycle_authority_invalid",
                            "resource-composer-owned lifecycle authority",
                            "missing lifecycle capability");
                }
                return _treatmentResources.ConfirmPersisted(
                    TreatmentResourceRegistryCapability,
                    requests);
            }
        }

        internal bool IsCurrentAcceptedState(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            MortalWoundTreatmentAcceptedStateAuthority acceptedState)
        {
            lock (_gate)
            {
                return ReferenceEquals(_mortalWoundTreatmentAcceptedState, acceptedState) &&
                       acceptedState.IsLeaseBoundTo(fileSystem, writeLease);
            }
        }

        internal MortalWoundTreatmentResourceLifecycleResult ReleaseTreatmentResources(
            object lifecycleCapability,
            IReadOnlyList<MortalWoundTreatmentAttemptRequest> requests,
            string reason)
        {
            lock (_gate)
            {
                if (!MortalWoundTreatmentResourceComposer
                        .IsResourceReservationCapability(lifecycleCapability))
                {
                    return MortalWoundTreatmentResourceReservationRegistry
                        .LifecycleFailure(
                            "mortal_wound_treatment_resource_lifecycle_authority_invalid",
                            "resource-composer-owned lifecycle authority",
                            "missing lifecycle capability");
                }
                return _treatmentResources.Release(
                    TreatmentResourceRegistryCapability,
                    requests,
                    reason);
            }
        }

        internal MortalWoundTreatmentResourceLifecycleResult CommitTreatmentResources(
            object lifecycleCapability,
            MortalWoundTreatmentResourceFinalization finalization)
        {
            lock (_gate)
            {
                if (!MortalWoundTreatmentResourceComposer
                        .IsResourceReservationCapability(lifecycleCapability))
                {
                    return MortalWoundTreatmentResourceReservationRegistry
                        .LifecycleFailure(
                            "mortal_wound_treatment_resource_lifecycle_authority_invalid",
                            "resource-composer-owned lifecycle authority",
                            "missing lifecycle capability");
                }
                return _treatmentResources.Commit(
                    TreatmentResourceRegistryCapability,
                    finalization);
            }
        }


        internal bool RollbackNewMortalWoundProcedureReservations(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            MortalWoundTreatmentAcceptedStateAuthority acceptedState,
            MortalWoundProcedureDiceReservation diceReservation,
            MortalWoundCriticalReactionReservation? criticalReactionReservation,
            MortalWoundCriticalReactionReservationAgreement? criticalReactionAgreement,
            MortalWoundProcedureReservationOwnership ownership)
        {
            lock (_gate)
            {
                if (!ReferenceEquals(_mortalWoundTreatmentAcceptedState, acceptedState) ||
                    !acceptedState.IsLeaseBoundTo(fileSystem, writeLease) ||
                    !string.Equals(
                        diceReservation.AcceptedStateFingerprint,
                        acceptedState.AcceptedStateFingerprint,
                        StringComparison.Ordinal) ||
                    !ownership.Matches(
                        diceReservation,
                        criticalReactionReservation,
                        criticalReactionAgreement) ||
                    (ownership.DiceWasCreated &&
                     !_procedureDice.CanRelease(diceReservation)) ||
                    (ownership.CriticalReactionWasCreated &&
                     !_criticalReactions.MatchesReleaseAgreement(
                         diceReservation,
                         criticalReactionReservation,
                         criticalReactionAgreement)))
                {
                    return false;
                }

                if (ownership.CriticalReactionWasCreated)
                {
                    _criticalReactions.ReleaseUnchecked(
                        diceReservation,
                        criticalReactionAgreement);
                }
                if (ownership.DiceWasCreated)
                    _procedureDice.ReleaseUnchecked(diceReservation);
                return true;
            }
        }

        internal bool ReleaseMortalWoundProcedureReservations(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            MortalWoundTreatmentAcceptedStateAuthority acceptedState,
            MortalWoundProcedureDiceReservation diceReservation,
            MortalWoundCriticalReactionReservation? criticalReactionReservation,
            MortalWoundCriticalReactionReservationAgreement? criticalReactionAgreement)
        {
            lock (_gate)
            {
                if (!ReferenceEquals(_mortalWoundTreatmentAcceptedState, acceptedState) ||
                    !acceptedState.IsLeaseBoundTo(fileSystem, writeLease) ||
                    !string.Equals(
                        diceReservation.AcceptedStateFingerprint,
                        acceptedState.AcceptedStateFingerprint,
                        StringComparison.Ordinal) ||
                    !_procedureDice.CanRelease(diceReservation) ||
                    !_criticalReactions.MatchesReleaseAgreement(
                        diceReservation,
                        criticalReactionReservation,
                        criticalReactionAgreement))
                {
                    return false;
                }

                _procedureDice.ReleaseUnchecked(diceReservation);
                _criticalReactions.ReleaseUnchecked(
                    diceReservation,
                    criticalReactionAgreement);
                return true;
            }
        }

        private static bool RequiresPlayerFateReservation(
            MortalWoundProcedureDiceReservation reservation,
            string rollMode,
            string rollActorKind,
            string rollActorId)
        {
            var selectedOrdinal = reservation.SourceRolls.Count == 1
                ? 0
                : rollMode switch
                {
                    "advantage" => reservation.SourceRolls[1] > reservation.SourceRolls[0]
                        ? 1
                        : 0,
                    "disadvantage" => reservation.SourceRolls[1] < reservation.SourceRolls[0]
                        ? 1
                        : 0,
                    _ => -1
                };
            return selectedOrdinal >= 0 &&
                   reservation.SourceRolls[selectedOrdinal] == 1 &&
                   string.Equals(rollActorKind, "player", StringComparison.Ordinal) &&
                   string.Equals(rollActorId, "player_current", StringComparison.Ordinal);
        }

        internal bool IsCurrentMortalWoundTreatmentAcceptedState(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            MortalWoundTreatmentAcceptedStateAuthority authority)
        {
            lock (_gate)
            {
                return ReferenceEquals(
                           _mortalWoundTreatmentAcceptedState,
                           authority) &&
                       authority.IsLeaseBoundTo(fileSystem, writeLease);
            }
        }

        internal bool HasLiveMortalWoundProcedureReservationAgreement(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            MortalWoundTreatmentAcceptedStateAuthority acceptedState,
            MortalWoundProcedureCheckAuthority procedure)
        {
            lock (_gate)
            {
                return ReferenceEquals(
                           _mortalWoundTreatmentAcceptedState,
                           acceptedState) &&
                       acceptedState.IsLeaseBoundTo(fileSystem, writeLease) &&
                       procedure.HasLiveReservationAgreement(
                           ProcedureReservationLiveCheckCapability,
                           _procedureDice,
                           _criticalReactions);
            }
        }

        internal MortalWoundDeteriorationPolicyAuthorityResult
            CreateMortalWoundDeteriorationPolicyAuthority(
                FileSystemManager fileSystem,
                FileSystemManager.CanonicalWriteLease writeLease,
                WoundAcceptedTurnBinding? binding,
                string? woundId,
                string? policyRef,
                object capability)
        {
            lock (_gate)
            {
                var acceptedState = _mortalWoundTreatmentAcceptedState;
                if (!AcceptedTurnAuthorityRegistry
                        .IsDeteriorationPolicyAuthorityCapability(capability) ||
                    acceptedState is null ||
                    !acceptedState.IsLeaseBoundTo(fileSystem, writeLease) ||
                    !acceptedState.AgreesWithBindingAndWound(binding, woundId))
                {
                    return MortalWoundDeteriorationPolicyAuthority.InvalidAuthority(
                        acceptedState?.WoundSourcePath + ".recovery.deteriorationPolicy" ??
                        "recovery.deteriorationPolicy",
                        "missing, stale, foreign, or mismatched accepted-state binding");
                }

                return MortalWoundDeteriorationPolicyAuthority.CreateRegistered(
                    capability,
                    acceptedState,
                    coordinates: null,
                    policyRef,
                    scope: "recovery");
            }
        }

        internal MortalWoundRecoveryPlanningResult PlanMortalWoundRecovery(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            WoundAcceptedTurnBinding? binding,
            string? woundId,
            object capability)
        {
            lock (_gate)
            {
                var acceptedState = _mortalWoundTreatmentAcceptedState;
                if (!AcceptedTurnAuthorityRegistry
                        .IsMortalWoundRecoveryPlannerCapability(capability) ||
                    acceptedState is null ||
                    !acceptedState.IsLeaseBoundTo(fileSystem, writeLease) ||
                    !acceptedState.AgreesWithBindingAndWound(binding, woundId))
                {
                    return MortalWoundRecoveryPlanner.RegistryFailure(
                        "missing, stale, foreign, or mismatched accepted-state binding");
                }

                return MortalWoundRecoveryPlanner.PlanRegistered(
                    fileSystem,
                    writeLease,
                    acceptedState,
                    binding!,
                    woundId!,
                    capability);
            }
        }

        internal MortalWoundDeteriorationPolicyAuthorityResult
            CreateMortalWoundDeteriorationPolicyAuthority(
                FileSystemManager fileSystem,
                FileSystemManager.CanonicalWriteLease writeLease,
                MortalWoundTreatmentAcceptedStateAuthority acceptedState,
                MortalWoundTreatmentAttemptCoordinates? coordinates,
                string? policyRef,
                object capability)
        {
            lock (_gate)
            {
                if (!AcceptedTurnAuthorityRegistry
                        .IsDeteriorationPolicyAuthorityCapability(capability) ||
                    !ReferenceEquals(
                        _mortalWoundTreatmentAcceptedState,
                        acceptedState) ||
                    !acceptedState.IsLeaseBoundTo(fileSystem, writeLease) ||
                    coordinates is null ||
                    !coordinates.AgreesWithAcceptedStateSemantics(acceptedState))
                {
                    return MortalWoundDeteriorationPolicyAuthority.InvalidAuthority(
                        acceptedState.WoundSourcePath + ".recovery.deteriorationPolicy",
                        "missing, stale, foreign, or mismatched attempt coordinates");
                }

                return MortalWoundDeteriorationPolicyAuthority.CreateRegistered(
                    capability,
                    acceptedState,
                    coordinates,
                    policyRef,
                    scope: "treatment_interruption");
            }
        }

        internal AcceptedMechanicsPlanningResult GetOrBuildCommonValidated(
            AcceptedMechanicsInput input)
        {
            lock (_gate)
            {
                var woundMismatch = GetCommonWoundStageMismatch(input);
                if (woundMismatch is not null)
                {
                    InvalidateWoundAndDependentCore();
                    return new AcceptedMechanicsPlanningResult(
                        null,
                        new[]
                        {
                            WoundIssue(
                                "accepted_mechanics_wound_stage_provenance_mismatch",
                                "the current registry-owned prepared, effect, and final wound stages",
                                woundMismatch)
                        });
                }
                try
                {
                    return _commonPlan.GetOrBuildValidated(input);
                }
                catch
                {
                    InvalidateAllCore();
                    throw;
                }
            }
        }

        internal AcceptedMechanicsPlanningResult GetOrBuildCommonWoundValidated(
            AcceptedMechanicsInput input,
            AcceptedMechanicsWoundStageBundle bundle)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(bundle);
            lock (_gate)
            {
                var supplied = input.PlanningContext?.WoundStageBundle;
                if (input.WoundInput is null || supplied is null ||
                    !string.Equals(
                        supplied.BundleFingerprint,
                        bundle.BundleFingerprint,
                        StringComparison.Ordinal))
                {
                    InvalidateWoundAndDependentCore();
                    return new AcceptedMechanicsPlanningResult(
                        null,
                        new[]
                        {
                            WoundIssue(
                                "accepted_mechanics_wound_stage_provenance_mismatch",
                                "the exact supplied bundle in the common planning input",
                                supplied?.BundleFingerprint ?? "missing")
                        });
                }

                var hasPrepared = _woundPlan.TryPeekPrepared(out _);
                var hasFinal = _woundPlan.TryPeekFinal(out _);
                if (hasPrepared || hasFinal)
                {
                    var mismatch = GetCommonWoundStageMismatch(input);
                    if (mismatch is not null)
                    {
                        InvalidateWoundAndDependentCore();
                        return new AcceptedMechanicsPlanningResult(
                            null,
                            new[]
                            {
                                WoundIssue(
                                    "accepted_mechanics_wound_stage_provenance_mismatch",
                                    "the current registry-owned or sole sealed wound stages",
                                    mismatch)
                            });
                    }
                }

                try
                {
                    return _commonPlan.GetOrBuildValidated(input);
                }
                catch
                {
                    InvalidateAllCore();
                    throw;
                }
            }
        }

        internal MortalWoundTreatmentPublicationReservation
            ReserveMortalWoundTreatmentPublication(
                MortalWoundTreatmentAcceptedStateAuthority acceptedState,
                string semanticFingerprint)
        {
            ArgumentNullException.ThrowIfNull(acceptedState);
            ArgumentException.ThrowIfNullOrWhiteSpace(semanticFingerprint);
            lock (_gate)
            {
                if (!ReferenceEquals(
                        _mortalWoundTreatmentAcceptedState,
                        acceptedState))
                {
                    return new(
                        MortalWoundTreatmentPublicationReservationStatus.Conflict,
                        null);
                }
                if (_mortalWoundTreatmentPublicationFingerprint is not null)
                {
                    return new(
                        string.Equals(
                            _mortalWoundTreatmentPublicationFingerprint,
                            semanticFingerprint,
                            StringComparison.Ordinal)
                            ? MortalWoundTreatmentPublicationReservationStatus.Exact
                            : MortalWoundTreatmentPublicationReservationStatus.Conflict,
                        null);
                }
                if (_mortalWoundTreatmentPublicationReservation is not null)
                {
                    return new(
                        MortalWoundTreatmentPublicationReservationStatus.Conflict,
                        null);
                }
                var reservation = new TreatmentPublicationReservationAuthority();
                _mortalWoundTreatmentPublicationReservation = reservation;
                _mortalWoundTreatmentReservedFingerprint = semanticFingerprint;
                return new(
                    MortalWoundTreatmentPublicationReservationStatus.Reserved,
                    reservation);
            }
        }

        internal void AbortMortalWoundTreatmentPublication(
            object? reservationAuthority)
        {
            lock (_gate)
            {
                if (reservationAuthority is not null && ReferenceEquals(
                        _mortalWoundTreatmentPublicationReservation,
                        reservationAuthority))
                {
                    ClearMortalWoundTreatmentPublicationReservationCore();
                }
            }
        }

        internal AcceptedMechanicsPlanningResult
            GetOrBuildCommonMortalWoundTreatmentValidated(
                AcceptedMechanicsInput input,
                AcceptedMechanicsWoundStageBundle bundle,
                MortalWoundTreatmentAcceptedStateAuthority acceptedState,
                MortalWoundTreatmentAttemptRequest request,
                MortalWoundTreatmentResolution resolution,
                string semanticFingerprint,
                object continuationAuthority,
                object reservationAuthority)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(bundle);
            ArgumentNullException.ThrowIfNull(acceptedState);
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(resolution);
            ArgumentNullException.ThrowIfNull(continuationAuthority);
            ArgumentNullException.ThrowIfNull(reservationAuthority);
            ArgumentException.ThrowIfNullOrWhiteSpace(semanticFingerprint);
            lock (_gate)
            {
                if (!ReferenceEquals(
                        _mortalWoundTreatmentAcceptedState,
                        acceptedState) ||
                    !OwnsMortalWoundTreatmentPublicationReservation(
                        continuationAuthority,
                        reservationAuthority))
                {
                    return new AcceptedMechanicsPlanningResult(
                        null,
                        new[]
                        {
                            WoundIssue(
                                "mortal_wound_treatment_publication_conflict",
                                "one fresh first publication admission",
                                "stale accepted state or existing publication")
                        });
                }

                var supplied = input.PlanningContext?.WoundStageBundle;
                var mismatch = supplied is null ||
                    !string.Equals(
                        supplied.BundleFingerprint,
                        bundle.BundleFingerprint,
                        StringComparison.Ordinal)
                    ? "missing or different supplied bundle"
                    : GetCommonWoundStageMismatch(input);
                if (mismatch is not null)
                {
                    InvalidateWoundAndDependentCore();
                    return new AcceptedMechanicsPlanningResult(
                        null,
                        new[]
                        {
                            WoundIssue(
                                "accepted_mechanics_wound_stage_provenance_mismatch",
                                "the current registry-owned prepared, effect, and final wound stages",
                                mismatch)
                        });
                }

                try
                {
                    var result = _commonPlan.GetOrBuildValidated(
                        input,
                        candidate => MortalWoundTreatmentCapabilityAuthority
                            .CandidateAdmissionGate.Validate(
                                acceptedState,
                                request,
                                resolution,
                                candidate,
                                semanticFingerprint,
                                continuationAuthority));
                    if (result.Success)
                    {
                        _mortalWoundTreatmentPublicationFingerprint =
                            semanticFingerprint;
                        ClearMortalWoundTreatmentPublicationReservationCore();
                    }
                    return result;
                }
                catch
                {
                    InvalidateAllCore();
                    throw;
                }
            }
        }

        private string? GetCommonWoundStageMismatch(
            AcceptedMechanicsInput input)
        {
            ArgumentNullException.ThrowIfNull(input);
            try
            {
                var woundInput = input.WoundInput;
                var bundle = input.PlanningContext?.WoundStageBundle;
                if (woundInput is null || bundle is null)
                {
                    if (_woundPlan.TryPeekPrepared(out var preparedResult) &&
                        preparedResult.Success &&
                        preparedResult.Plan is { } currentPrepared &&
                        CommonBindingAgrees(
                            input,
                            currentPrepared.Binding))
                    {
                        return "current wound stages were omitted from the common input";
                    }
                    return null;
                }

                var prepared = bundle.PreparedPlan;
                var preparedMismatch =
                    _woundPlan.GetPreparedMismatchCode(prepared);
                if (preparedMismatch is not null)
                    return preparedMismatch;

                var effect = bundle.EffectBatchPlan;
                if (!CurrentWoundEffectAgrees(
                        prepared,
                        new WoundEffectBatchPlanningResult(
                            effect,
                            Array.Empty<ValidationIssue>())))
                {
                    return "foreign or rotated wound effect stage";
                }

                if (!_woundPlan.TryPeekFinal(out var cachedFinal) ||
                    !cachedFinal.Success ||
                    cachedFinal.Plan is not { } currentFinal)
                {
                    return "missing current final wound stage";
                }
                var validatedFinal =
                    WoundAcceptedTurnPlanCache.ValidateFinalResult(
                        prepared,
                        effect,
                        cachedFinal);
                if (!validatedFinal.Success)
                    return "invalid current final wound stage";

                var suppliedFinal = bundle.FinalPlan;
                return string.Equals(
                    suppliedFinal.WoundFinalPlanFingerprint,
                    currentFinal.WoundFinalPlanFingerprint,
                    StringComparison.Ordinal)
                    ? null
                    : "foreign or rotated final wound stage";
            }
            catch (Exception exception) when (
                exception is ArgumentException or InvalidOperationException or
                    System.Text.Json.JsonException or NullReferenceException)
            {
                return exception.GetType().Name;
            }
        }

        private static bool CommonBindingAgrees(
            AcceptedMechanicsInput input,
            WoundAcceptedTurnBinding woundBinding) =>
            string.Equals(
                input.SessionId,
                woundBinding.SessionId,
                StringComparison.Ordinal) &&
            string.Equals(
                input.RequestId,
                woundBinding.RequestId,
                StringComparison.Ordinal) &&
            string.Equals(
                input.SnapshotToken,
                woundBinding.SnapshotToken,
                StringComparison.Ordinal) &&
            string.Equals(
                input.Realm,
                woundBinding.Realm,
                StringComparison.Ordinal) &&
            input.Turn == woundBinding.Turn;

        internal void InvalidateCommonValidated()
        {
            lock (_gate)
            {
                _commonPlan.InvalidateValidated();
                _mortalWoundTreatmentPublicationFingerprint = null;
                ClearMortalWoundTreatmentPublicationReservationCore();
            }
        }

        internal bool HasCommonValidated()
        {
            lock (_gate)
                return _commonPlan.HasValidated;
        }

        internal bool TryPeekCommonValidated(
            out AcceptedMechanicsPlanBinding binding,
            out AcceptedMechanicsPlanningResult result)
        {
            lock (_gate)
                return _commonPlan.TryPeekValidated(out binding, out result);
        }

        internal bool TryRegisterWoundRepairWave(
            WoundRepairPacketAuthority authority,
            IReadOnlyList<WoundRepairPacket> packets)
        {
            lock (_gate)
            {
                InvalidateAllCore();
                return _commonPlan.TryRegisterWoundRepairWave(
                    authority,
                    packets);
            }
        }

        internal bool TryTakeWoundRepairPacket(
            WoundRepairPacketAuthority liveAuthority,
            WoundRepairPacketReceipt receipt,
            out WoundRepairPacket packet)
        {
            lock (_gate)
            {
                var taken = _commonPlan.TryTakeWoundRepairPacket(
                    liveAuthority,
                    receipt,
                    out packet);
                if (!taken && !_commonPlan.HasWoundRepairWave)
                    InvalidateAllCore();
                return taken;
            }
        }

        internal bool HasWoundRepairWave()
        {
            lock (_gate)
                return _commonPlan.HasWoundRepairWave;
        }

        internal WoundAcceptedTurnPreparationResult GetOrBuildWoundPrepared(
            WoundAcceptedTurnInput input)
        {
            lock (_gate)
            {
                try
                {
                    var result = _woundPlan.GetOrBuildPrepared(
                        input,
                        out var reused);
                    if (!reused)
                    {
                        _effectPlan.InvalidateAll();
                        ClearWoundEffectCore();
                        _commonPlan.InvalidateAll();
                        _mortalWoundTreatmentPublicationFingerprint = null;
                        ClearMortalWoundTreatmentPublicationReservationCore();
                    }
                    return result;
                }
                catch
                {
                    InvalidateAllCore();
                    throw;
                }
            }
        }

        internal WoundAcceptedTurnPreparationResult
            GetOrBuildWoundTreatmentContinuationPrepared(
                WoundAcceptedTurnInput input,
                object treatmentContinuationAuthority,
                object reservationAuthority)
        {
            lock (_gate)
            {
                if (!OwnsMortalWoundTreatmentPublicationReservation(
                        treatmentContinuationAuthority,
                        reservationAuthority))
                {
                    return FailedWoundPrepared(
                        "mortal_wound_treatment_publication_reservation_invalid",
                        "the current exact private treatment publication reservation",
                        "stale or cancelled treatment publication reservation");
                }
                try
                {
                    var result = _woundPlan
                        .GetOrBuildTreatmentContinuationPrepared(
                            input,
                            treatmentContinuationAuthority,
                            out var reused);
                    if (!reused)
                    {
                        _effectPlan.InvalidateAll();
                        ClearWoundEffectCore();
                        _commonPlan.InvalidateAll();
                        _mortalWoundTreatmentPublicationFingerprint = null;
                    }
                    return result;
                }
                catch
                {
                    InvalidateAllCore();
                    throw;
                }
            }
        }

        internal EffectAcceptedTurnPlanningResult GetOrBuildEffectValidated(
            EffectAcceptedTurnInput input)
        {
            lock (_gate)
            {
                try
                {
                    var result = _effectPlan.GetOrBuildValidated(
                        input,
                        out var reused);
                    if (!reused || !result.Success)
                    {
                        ClearWoundEffectCore();
                        _woundPlan.InvalidateFinal();
                        _commonPlan.InvalidateAll();
                        _mortalWoundTreatmentPublicationFingerprint = null;
                    }
                    return result;
                }
                catch
                {
                    InvalidateAllCore();
                    throw;
                }
            }
        }

        internal WoundEffectBatchPlanningResult GetOrBuildWoundEffectValidated(
            WoundPreparedAcceptedTurnPlan prepared,
            EffectAcceptedTurnInput input,
            object? treatmentContinuationAuthority = null,
            object? reservationAuthority = null)
        {
            lock (_gate)
            {
                if (!WoundStageReservationAgrees(
                        prepared,
                        treatmentContinuationAuthority,
                        reservationAuthority))
                {
                    return FailedWoundEffect(
                        "mortal_wound_treatment_publication_reservation_invalid",
                        "the current exact private treatment publication reservation",
                        "stale or cancelled treatment publication reservation");
                }
                var preparedMismatch =
                    _woundPlan.GetPreparedMismatchCode(prepared);
                if (preparedMismatch is not null)
                {
                    InvalidateWoundAndDependentCore();
                    return FailedWoundEffect(
                        preparedMismatch,
                        "the current registry-owned prepared wound stage",
                        "foreign or stale prepared stage");
                }

                try
                {
                    var ordinary = _effectPlan.GetOrBuildWoundValidated(
                        prepared,
                        input,
                        out var reused);
                    var accepted = WoundEffectBatchPlanner.AcceptEffectResult(
                        prepared,
                        input,
                        ordinary);
                    if (!accepted.Success)
                    {
                        _effectPlan.InvalidateAll();
                        ClearWoundEffectCore();
                        _woundPlan.InvalidateFinal();
                        _commonPlan.InvalidateAll();
                        _mortalWoundTreatmentPublicationFingerprint = null;
                        return accepted;
                    }

                    var fingerprint =
                        accepted.Plan!.EffectAcceptedTurnPlanFingerprint;
                    if (reused &&
                        _woundEffectResult is not null &&
                        string.Equals(
                            _woundEffectFingerprint,
                            fingerprint,
                            StringComparison.Ordinal))
                    {
                        return Detach(_woundEffectResult);
                    }

                    _woundPlan.InvalidateFinal();
                    _commonPlan.InvalidateAll();
                    _mortalWoundTreatmentPublicationFingerprint = null;
                    var stageToken = new object();
                    var bound = accepted.Plan.BindToCacheAuthority(
                        _authorityStateToken,
                        stageToken);
                    _woundEffectStageToken = stageToken;
                    _woundEffectFingerprint = fingerprint;
                    _woundEffectResult = new WoundEffectBatchPlanningResult(
                        bound,
                        Array.Empty<ValidationIssue>());
                    return Detach(_woundEffectResult);
                }
                catch
                {
                    InvalidateAllCore();
                    throw;
                }
            }
        }

        internal WoundAcceptedTurnPlanningResult GetOrBuildWoundFinal(
            WoundPreparedAcceptedTurnPlan prepared,
            WoundEffectBatchPlanningResult effectResult,
            object? treatmentContinuationAuthority = null,
            object? reservationAuthority = null)
        {
            lock (_gate)
            {
                if (!WoundStageReservationAgrees(
                        prepared,
                        treatmentContinuationAuthority,
                        reservationAuthority))
                {
                    return FailedWoundFinal(
                        "mortal_wound_treatment_publication_reservation_invalid",
                        "the current exact private treatment publication reservation",
                        "stale or cancelled treatment publication reservation");
                }
                var preparedMismatch =
                    _woundPlan.GetPreparedMismatchCode(prepared);
                if (preparedMismatch is not null)
                {
                    InvalidateWoundAndDependentCore();
                    return FailedWoundFinal(
                        preparedMismatch,
                        "the current registry-owned prepared wound stage",
                        "foreign or stale prepared stage");
                }
                if (!CurrentWoundEffectAgrees(prepared, effectResult))
                {
                    InvalidateWoundAndDependentCore();
                    return FailedWoundFinal(
                        "wound_plan_effect_binding_mismatch",
                        _woundEffectFingerprint ??
                            "one current registry-owned wound effect stage",
                        SafeEffectFingerprint(effectResult));
                }

                try
                {
                    var result = _woundPlan.GetOrBuildFinal(
                        prepared,
                        Detach(_woundEffectResult!),
                        out var reused);
                    if (!result.Success)
                    {
                        InvalidateWoundAndDependentCore();
                    }
                    else if (!reused)
                    {
                        _commonPlan.InvalidateAll();
                        _mortalWoundTreatmentPublicationFingerprint = null;
                    }
                    return result;
                }
                catch
                {
                    InvalidateAllCore();
                    throw;
                }
            }
        }

        internal void InvalidateEffectValidated()
        {
            lock (_gate)
            {
                _effectPlan.InvalidateValidated();
                ClearWoundEffectCore();
                _woundPlan.InvalidateFinal();
                _commonPlan.InvalidateAll();
                _mortalWoundTreatmentPublicationFingerprint = null;
                ClearMortalWoundTreatmentPublicationReservationCore();
            }
        }

        internal bool TryPeekEffectValidated(
            out EffectAcceptedTurnPlanBinding binding,
            out EffectAcceptedTurnPlanningResult result)
        {
            lock (_gate)
                return _effectPlan.TryPeekValidated(out binding, out result);
        }

        internal bool TryPeekEffectValidated(
            out EffectAcceptedTurnPlanningResult result)
        {
            lock (_gate)
                return _effectPlan.TryPeekValidated(out result);
        }

        internal bool TryPeekWoundPrepared(
            out WoundAcceptedTurnPreparationResult result)
        {
            lock (_gate)
                return _woundPlan.TryPeekPrepared(out result);
        }

        internal bool TryPeekWoundFinal(
            out WoundAcceptedTurnPlanningResult result)
        {
            lock (_gate)
                return _woundPlan.TryPeekFinal(out result);
        }

        internal bool TryTakeCommonValidated(
            AcceptedMechanicsPlanBinding liveBinding,
            out AcceptedMechanicsPlanningResult result)
        {
            lock (_gate)
            {
                var taken = _commonPlan.TryTakeValidated(
                    liveBinding,
                    out result);
                if (!taken)
                    _commonPlan.InvalidateAll();
                _mortalWoundTreatmentPublicationFingerprint = null;
                ClearMortalWoundTreatmentPublicationReservationCore();
                _effectPlan.InvalidateAll();
                ClearWoundEffectCore();
                _woundPlan.InvalidateAll();
                _mortalItems.InvalidateValidated();
                return taken;
            }
        }

        internal void RegisterMortalItemsValidated(
            string sessionId,
            string snapshotToken,
            string fingerprint,
            IReadOnlyList<MortalItemAcceptedTurnAuthority.NewCandidate> newCandidates,
            IReadOnlyList<MortalItemAcceptedTurnAuthority.StableCandidate> stableCandidates,
            IReadOnlyList<string> governedItemIds)
        {
            lock (_gate)
                _mortalItems.Register(
                    sessionId,
                    snapshotToken,
                    fingerprint,
                    newCandidates,
                    stableCandidates,
                    governedItemIds);
        }

        internal IReadOnlyList<ValidationIssue>
            RegisterMortalTreatmentItemsValidated(
                string sessionId,
                string snapshotToken,
                string fingerprint,
                IReadOnlyList<MortalItemAcceptedTurnAuthority.NewCandidate> newCandidates,
                IReadOnlyList<MortalItemAcceptedTurnAuthority.StableCandidate> stableCandidates,
                IReadOnlyList<string> governedItemIds,
                object treatmentContinuationAuthority,
                object reservationAuthority)
        {
            lock (_gate)
            {
                if (!OwnsMortalWoundTreatmentPublicationReservation(
                        treatmentContinuationAuthority,
                        reservationAuthority))
                {
                    return new[]
                    {
                        WoundIssue(
                            "mortal_wound_treatment_publication_reservation_invalid",
                            "the current exact private treatment publication reservation",
                            "stale or cancelled treatment publication reservation")
                    };
                }
                _mortalItems.Register(
                    sessionId,
                    snapshotToken,
                    fingerprint,
                    newCandidates,
                    stableCandidates,
                    governedItemIds);
                return Array.Empty<ValidationIssue>();
            }
        }

        internal bool HasMortalItemsValidated()
        {
            lock (_gate)
                return _mortalItems.HasValidated;
        }

        internal IReadOnlyList<EffectSourceExport> GetMortalItemEffectSources(
            string sessionId,
            string snapshotToken)
        {
            lock (_gate)
                return _mortalItems.GetSources(sessionId, snapshotToken);
        }

        internal void InvalidateMortalItemsValidated()
        {
            lock (_gate)
                _mortalItems.InvalidateValidated();
        }

        internal IReadOnlySet<EffectSourceOwnerKey>
            GetMortalItemReplacedSourceOwners(
                string sessionId,
                string snapshotToken)
        {
            lock (_gate)
                return _mortalItems.GetReplacedSourceOwners(
                    sessionId,
                    snapshotToken);
        }

        internal bool TryGetMortalItemId(
            string sessionId,
            string snapshotToken,
            string creationRef,
            out string itemId)
        {
            lock (_gate)
                return _mortalItems.TryGetItemId(
                    sessionId,
                    snapshotToken,
                    creationRef,
                    out itemId);
        }

        internal bool TryCaptureMortalItemNormalizationSnapshot(
            string sessionId,
            string snapshotToken,
            int turn,
            out MortalItemAcceptedTurnNormalizationSnapshot snapshot)
        {
            lock (_gate)
                return _mortalItems.TryCaptureNormalizationSnapshot(
                    sessionId,
                    snapshotToken,
                    turn,
                    out snapshot);
        }

        internal IReadOnlyList<MortalItemAcceptedTurnOwner> GetMortalItemOwners(
            string sessionId,
            string snapshotToken)
        {
            lock (_gate)
                return _mortalItems.GetOwners(sessionId, snapshotToken);
        }

        internal IReadOnlySet<string> GetMissingGovernedMortalItemIds(
            string sessionId,
            string snapshotToken)
        {
            lock (_gate)
                return _mortalItems.GetMissingGovernedItemIds(
                    sessionId,
                    snapshotToken);
        }

        internal void InvalidateAll()
        {
            lock (_gate)
                InvalidateAllCore();
        }

        private bool CurrentWoundEffectAgrees(
            WoundPreparedAcceptedTurnPlan prepared,
            WoundEffectBatchPlanningResult result)
        {
            try
            {
                if (_woundEffectResult is null ||
                    _woundEffectStageToken is null ||
                    _woundEffectFingerprint is null ||
                    !result.Success ||
                    result.Plan is not { } plan ||
                    !plan.BelongsToEffectStage(
                        _authorityStateToken,
                        _woundEffectStageToken))
                {
                    return false;
                }

                if (!_effectPlan.TryPeekValidated(out var ordinary) ||
                    !ordinary.Success)
                {
                    return false;
                }
                var current = WoundEffectBatchPlanner.AcceptEffectResult(
                    prepared,
                    plan.EffectInput,
                    ordinary);
                if (!current.Success || current.Plan is not { } currentPlan)
                    return false;

                return string.Equals(
                    plan.EffectAcceptedTurnPlanFingerprint,
                    _woundEffectFingerprint,
                    StringComparison.Ordinal) &&
                    string.Equals(
                        currentPlan.EffectAcceptedTurnPlanFingerprint,
                        _woundEffectFingerprint,
                        StringComparison.Ordinal);
            }
            catch (Exception exception) when (
                exception is ArgumentException or InvalidOperationException or
                    System.Text.Json.JsonException or NullReferenceException)
            {
                return false;
            }
        }

        private void InvalidateWoundAndDependentCore()
        {
            _woundPlan.InvalidateAll();
            _effectPlan.InvalidateAll();
            ClearWoundEffectCore();
            _commonPlan.InvalidateAll();
            _mortalWoundTreatmentPublicationFingerprint = null;
            ClearMortalWoundTreatmentPublicationReservationCore();
            _mortalItems.InvalidateValidated();
        }

        private void InvalidateAllCore()
        {
            _commonPlan.InvalidateAll();
            _mortalWoundTreatmentPublicationFingerprint = null;
            ClearMortalWoundTreatmentPublicationReservationCore();
            _effectPlan.InvalidateAll();
            ClearWoundEffectCore();
            _woundPlan.InvalidateAll();
            _mortalItems.InvalidateValidated();
            _procedureDice.InvalidateAll();
            _criticalReactions.InvalidateAll();
            _treatmentResources.InvalidateAll();
            ResetProcedureClaimRecovery();
            _mortalWoundTreatmentAcceptedState = null;
        }

        private void ClearWoundEffectCore()
        {
            _woundEffectStageToken = null;
            _woundEffectFingerprint = null;
            _woundEffectResult = null;
        }

        private void ClearMortalWoundTreatmentPublicationReservationCore()
        {
            _mortalWoundTreatmentPublicationReservation = null;
            _mortalWoundTreatmentReservedFingerprint = null;
        }

        private bool OwnsMortalWoundTreatmentPublicationReservation(
            object treatmentContinuationAuthority,
            object reservationAuthority) =>
            _mortalWoundTreatmentPublicationFingerprint is null &&
            _mortalWoundTreatmentPublicationReservation is not null &&
            _mortalWoundTreatmentReservedFingerprint is not null &&
            ReferenceEquals(
                _mortalWoundTreatmentPublicationReservation,
                reservationAuthority) &&
            WoundAcceptedTurnPlanner.TreatmentContinuationReservationAgrees(
                treatmentContinuationAuthority,
                reservationAuthority,
                _mortalWoundTreatmentReservedFingerprint);

        private bool WoundStageReservationAgrees(
            WoundPreparedAcceptedTurnPlan prepared,
            object? treatmentContinuationAuthority,
            object? reservationAuthority)
        {
            var preparedAuthority = prepared.TreatmentContinuationAuthority;
            if (preparedAuthority is null)
            {
                return treatmentContinuationAuthority is null &&
                       reservationAuthority is null;
            }
            return treatmentContinuationAuthority is not null &&
                   reservationAuthority is not null &&
                   ReferenceEquals(
                       preparedAuthority,
                       treatmentContinuationAuthority) &&
                   OwnsMortalWoundTreatmentPublicationReservation(
                       treatmentContinuationAuthority,
                       reservationAuthority);
        }

        private static WoundEffectBatchPlanningResult Detach(
            WoundEffectBatchPlanningResult result) =>
            new(result.Plan, result.Issues);

        private static string SafeEffectFingerprint(
            WoundEffectBatchPlanningResult result)
        {
            try
            {
                return result.Plan?.EffectAcceptedTurnPlanFingerprint ??
                    "missing effect plan";
            }
            catch (Exception exception) when (
                exception is ArgumentException or InvalidOperationException or
                    System.Text.Json.JsonException or NullReferenceException)
            {
                return exception.GetType().Name;
            }
        }

        private static WoundEffectBatchPlanningResult FailedWoundEffect(
            string code,
            string expected,
            string actual) =>
            new(null, new[] { WoundIssue(code, expected, actual) });

        private static WoundAcceptedTurnPreparationResult FailedWoundPrepared(
            string code,
            string expected,
            string actual) =>
            new(null, new[] { WoundIssue(code, expected, actual) });

        private static WoundAcceptedTurnPlanningResult FailedWoundFinal(
            string code,
            string expected,
            string actual) =>
            new(null, new[] { WoundIssue(code, expected, actual) });

        private static ValidationIssue WoundIssue(
            string code,
            string expected,
            string actual) =>
            new(
                WoundPlanPath,
                IssueSeverity.Error,
                "The accepted wound stage is not owned by the current authority state.",
                code,
                actor: "accepted_turn",
                section: "wound_materialization",
                expected: expected,
                actual: actual,
                repairHint:
                    "Discard every dependent handoff and rebuild the wound effect stage through the current accepted-turn authority.",
                repairTargetFiles: new[] { WoundPlanPath });
    }
}

internal static class AcceptedTurnPlanAuthority
{
    internal static void InvalidateValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        AcceptedTurnAuthorityRegistry.InvalidateAcceptedTurnValidated(
            fileSystem,
            writeLease);
    }
}
