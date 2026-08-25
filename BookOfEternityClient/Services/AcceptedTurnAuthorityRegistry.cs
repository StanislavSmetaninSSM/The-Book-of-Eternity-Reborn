using System.Runtime.CompilerServices;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class AcceptedTurnAuthorityRegistry
{
    private static readonly ConditionalWeakTable<
        CanonicalRootIdentity,
        RootAuthoritySlot> RootSlots = new();

    internal static AcceptedMechanicsPlanningResult GetOrBuildCommonValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        AcceptedMechanicsInput input) =>
        GetState(fileSystem, writeLease).CommonPlan.GetOrBuildValidated(input);

    internal static bool TryTakeCommonValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        AcceptedMechanicsPlanBinding liveBinding,
        out AcceptedMechanicsPlanningResult result) =>
        GetState(fileSystem, writeLease).CommonPlan.TryTakeValidated(
            liveBinding,
            out result);

    internal static void InvalidateCommonValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease) =>
        GetState(fileSystem, writeLease).CommonPlan.InvalidateValidated();

    internal static bool HasCommonValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease) =>
        GetState(fileSystem, writeLease).CommonPlan.HasValidated;

    internal static bool TryPeekCommonValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        out AcceptedMechanicsPlanBinding binding,
        out AcceptedMechanicsPlanningResult result) =>
        GetState(fileSystem, writeLease).CommonPlan.TryPeekValidated(
            out binding,
            out result);

    internal static EffectAcceptedTurnPlanningResult GetOrBuildEffectValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        EffectAcceptedTurnInput input) =>
        GetState(fileSystem, writeLease).EffectPlan.GetOrBuildValidated(input);

    internal static void InvalidateEffectValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease) =>
        GetState(fileSystem, writeLease).EffectPlan.InvalidateValidated();

    internal static bool TryPeekEffectValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        out EffectAcceptedTurnPlanBinding binding,
        out EffectAcceptedTurnPlanningResult result) =>
        GetState(fileSystem, writeLease).EffectPlan.TryPeekValidated(
            out binding,
            out result);

    internal static bool TryPeekEffectValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        out EffectAcceptedTurnPlanningResult result) =>
        GetState(fileSystem, writeLease).EffectPlan.TryPeekValidated(out result);

    internal static void RegisterMortalItemsValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        string sessionId,
        string snapshotToken,
        string fingerprint,
        IReadOnlyList<MortalItemAcceptedTurnAuthority.NewCandidate> newCandidates,
        IReadOnlyList<MortalItemAcceptedTurnAuthority.StableCandidate> stableCandidates,
        IReadOnlyList<string> governedItemIds) =>
        GetState(fileSystem, writeLease).MortalItems.Register(
            sessionId,
            snapshotToken,
            fingerprint,
            newCandidates,
            stableCandidates,
            governedItemIds);

    internal static bool HasMortalItemsValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease) =>
        GetState(fileSystem, writeLease).MortalItems.HasValidated;

    internal static IReadOnlyList<EffectSourceExport> GetMortalItemEffectSources(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        string sessionId,
        string snapshotToken) =>
        GetState(fileSystem, writeLease).MortalItems.GetSources(
            sessionId,
            snapshotToken);

    internal static void InvalidateMortalItemsValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease) =>
        GetState(fileSystem, writeLease).MortalItems.InvalidateValidated();

    internal static IReadOnlySet<EffectSourceOwnerKey> GetMortalItemReplacedSourceOwners(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        string sessionId,
        string snapshotToken) =>
        GetState(fileSystem, writeLease).MortalItems.GetReplacedSourceOwners(
            sessionId,
            snapshotToken);

    internal static bool TryGetMortalItemId(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        string sessionId,
        string snapshotToken,
        string creationRef,
        out string itemId) =>
        GetState(fileSystem, writeLease).MortalItems.TryGetItemId(
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
        GetState(fileSystem, writeLease).MortalItems.TryCaptureNormalizationSnapshot(
            sessionId,
            snapshotToken,
            turn,
            out snapshot);

    internal static IReadOnlyList<MortalItemAcceptedTurnOwner> GetMortalItemOwners(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        string sessionId,
        string snapshotToken) =>
        GetState(fileSystem, writeLease).MortalItems.GetOwners(
            sessionId,
            snapshotToken);

    internal static IReadOnlySet<string> GetMissingGovernedMortalItemIds(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        string sessionId,
        string snapshotToken) =>
        GetState(fileSystem, writeLease).MortalItems.GetMissingGovernedItemIds(
            sessionId,
            snapshotToken);

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
        internal AcceptedMechanicsPlanCache CommonPlan { get; } = new(
            AcceptedMechanicsPlanner.BuildAcceptedPlan);

        internal EffectAcceptedTurnPlanCache EffectPlan { get; } = new();

        internal MortalItemAcceptedTurnAuthority.Cache MortalItems { get; } = new();
    }
}
