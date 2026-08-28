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

    internal static WoundAcceptedTurnPlanningResult GetOrBuildWoundFinalValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        WoundPreparedAcceptedTurnPlan prepared,
        WoundEffectBatchPlanningResult effectResult) =>
        GetState(fileSystem, writeLease).GetOrBuildWoundFinal(
            prepared,
            effectResult);

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

        private readonly object _gate = new();
        private readonly object _authorityStateToken = new();
        private readonly AcceptedMechanicsPlanCache _commonPlan;
        private readonly EffectAcceptedTurnPlanCache _effectPlan;
        private readonly WoundAcceptedTurnPlanCache _woundPlan;
        private readonly MortalItemAcceptedTurnAuthority.Cache _mortalItems;
        private object? _woundEffectStageToken;
        private string? _woundEffectFingerprint;
        private WoundEffectBatchPlanningResult? _woundEffectResult;

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
                _commonPlan.InvalidateValidated();
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
            EffectAcceptedTurnInput input)
        {
            lock (_gate)
            {
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
            WoundEffectBatchPlanningResult effectResult)
        {
            lock (_gate)
            {
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
            _mortalItems.InvalidateValidated();
        }

        private void InvalidateAllCore()
        {
            _commonPlan.InvalidateAll();
            _effectPlan.InvalidateAll();
            ClearWoundEffectCore();
            _woundPlan.InvalidateAll();
            _mortalItems.InvalidateValidated();
        }

        private void ClearWoundEffectCore()
        {
            _woundEffectStageToken = null;
            _woundEffectFingerprint = null;
            _woundEffectResult = null;
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
