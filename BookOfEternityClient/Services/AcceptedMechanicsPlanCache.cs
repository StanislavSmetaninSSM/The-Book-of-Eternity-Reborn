using System.Text.Json;
using SpiritualC4PublicationAuthority = BookOfEternityClient.Services.ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority;
using SpiritualC4PublicationReceipt = BookOfEternityClient.Services.ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationReceipt;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal delegate AcceptedMechanicsPlanningResult AcceptedMechanicsPlanFactory(
    AcceptedMechanicsInput input,
    string inputFingerprint);

internal sealed class AcceptedMechanicsPlanCache
{
    internal sealed class ValidatedPublicationTakeSnapshot
    {
        internal ValidatedPublicationTakeSnapshot(
            object cacheAuthority,
            object fence,
            string bindingFingerprint,
            AcceptedMechanicsPlanBinding binding,
            AcceptedMechanicsPlanningResult result,
            AcceptedMechanicsPlan plan,
            string preparedPlanFingerprint)
        {
            CacheAuthority = cacheAuthority;
            Fence = fence;
            BindingFingerprint = bindingFingerprint;
            Binding = binding;
            Result = result;
            Plan = plan;
            PreparedPlanFingerprint = preparedPlanFingerprint;
        }

        internal object CacheAuthority { get; }
        internal object Fence { get; }
        internal string BindingFingerprint { get; }
        internal AcceptedMechanicsPlanBinding Binding { get; }
        internal AcceptedMechanicsPlanningResult Result { get; }
        internal AcceptedMechanicsPlan Plan { get; }
        internal string PreparedPlanFingerprint { get; }
    }

    private readonly object _gate = new();
    private readonly object _cacheAuthority = new();
    private readonly AcceptedMechanicsPlanFactory _planner;
    private object _validatedFence = new();
    private string? _inputFingerprint;
    private AcceptedMechanicsPlanningResult? _planningResult;
    private string? _validatedBindingFingerprint;
    private AcceptedMechanicsPlanBinding? _validatedBinding;
    private AcceptedMechanicsPlanningResult? _validatedResult;
    private SpiritualC4PublicationAuthority? _spiritualPublication;
    private SpiritualC4PublicationReceipt? _spiritualTaken;
    private WoundRepairPacketAuthority? _woundRepairAuthority;
    private readonly Dictionary<string, WoundRepairPacket> _woundRepairPackets =
        new(StringComparer.Ordinal);
    private readonly HashSet<WoundRepairPacketReceipt> _consumedWoundRepairReceipts =
        new();

    internal AcceptedMechanicsPlanCache(AcceptedMechanicsPlanFactory planner) =>
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));

    internal bool HasValidated
    {
        get
        {
            lock (_gate)
                return _validatedResult != null || _spiritualTaken is not null;
        }
    }

    internal bool HasWoundRepairWave
    {
        get
        {
            lock (_gate)
            {
                return _woundRepairAuthority is not null &&
                       _woundRepairPackets.Count != 0;
            }
        }
    }

    internal bool TryRegisterWoundRepairWave(
        WoundRepairPacketAuthority authority,
        IReadOnlyList<WoundRepairPacket> packets)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(packets);
        lock (_gate)
        {
            InvalidateAllCore();
            if (!ValidWoundRepairAuthority(authority) ||
                packets.Count is < 1 or > 64)
            {
                return false;
            }

            var candidateRefs = new HashSet<string>(StringComparer.Ordinal);
            var confusableRefs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var packet in packets)
            {
                if (packet is null ||
                    !WoundRepairPacketAgrees(authority, packet) ||
                    !candidateRefs.Add(packet.CandidateRef) ||
                    !confusableRefs.Add(
                        MortalLocationIdentityState.BuildConfusableKey(
                            packet.CandidateRef)))
                {
                    InvalidateAllCore();
                    return false;
                }
                _woundRepairPackets.Add(packet.CandidateRef, packet);
            }

            _woundRepairAuthority = authority;
            return true;
        }
    }

    internal bool TryTakeWoundRepairPacket(
        WoundRepairPacketAuthority liveAuthority,
        WoundRepairPacketReceipt receipt,
        out WoundRepairPacket packet)
    {
        ArgumentNullException.ThrowIfNull(liveAuthority);
        ArgumentNullException.ThrowIfNull(receipt);
        lock (_gate)
        {
            packet = null!;
            if (_woundRepairAuthority is null ||
                !Equals(_woundRepairAuthority, liveAuthority))
            {
                InvalidateAllCore();
                return false;
            }
            if (_consumedWoundRepairReceipts.Contains(receipt))
                return false;
            if (!_woundRepairPackets.TryGetValue(
                    receipt.CandidateRef,
                    out var candidate) ||
                !Equals(candidate.CreateReceipt(), receipt))
            {
                InvalidateAllCore();
                return false;
            }

            _woundRepairPackets.Remove(receipt.CandidateRef);
            _consumedWoundRepairReceipts.Add(receipt);
            packet = candidate;
            if (_woundRepairPackets.Count == 0)
                ClearWoundRepairWaveCore();
            return true;
        }
    }

    /// <summary>
    /// Registers an owner-issued completed plan without executing the ordinary planner.
    /// </summary>
    /// <param name="fileSystem">
    /// Exact filesystem whose physical inputs the authority binds.
    /// </param>
    /// <param name="lease">
    /// Active canonical lease spanning registration.
    /// </param>
    /// <param name="authority">
    /// Fresh original-capture capability; a previously transferred capability is rejected.
    /// </param>
    /// <returns>
    /// True only when the completed plan is registered under this cache's current fence.
    /// </returns>
    internal bool RegisterSpiritualPublication(FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease lease, SpiritualC4PublicationAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
        lock (_gate)
        {
            if (_spiritualTaken is not null || !authority.CurrentInputsAgree(fileSystem, lease))
                return false;
            if (!authority.TryBindCache(_cacheAuthority))
                return false;
            InvalidateAllCore();
            _spiritualPublication = authority;
            _validatedBinding = authority.Binding;
            _validatedBindingFingerprint = AcceptedMechanicsPlanFingerprints.ComputeInput(_validatedBinding);
            _validatedResult = new AcceptedMechanicsPlanningResult(authority.Plan, Array.Empty<ValidationIssue>());
            return true;
        }
    }

    /// <summary>
    /// Reads the sealed completed entry without consuming publication ownership.
    /// </summary>
    /// <param name="authority">
    /// Exact registered authority on success; null otherwise.
    /// </param>
    /// <returns>
    /// True only while the registered completion remains intact and untaken.
    /// </returns>
    internal bool TryPeekSpiritualPublication(out SpiritualC4PublicationAuthority authority)
    {
        lock (_gate)
        {
            if (_spiritualPublication is { } current && current.HasValidSeal() &&
                ReferenceEquals(_validatedResult?.Plan, current.Plan))
            {
                authority = current;
                return true;
            }
            authority = null!;
            return false;
        }
    }

    /// <summary>
    /// Takes the exact completed entry once after rechecking its entire physical binding.
    /// </summary>
    /// <param name="fileSystem">
    /// Exact filesystem bound by the completion.
    /// </param>
    /// <param name="lease">
    /// Active canonical lease retained by the common transaction.
    /// </param>
    /// <param name="authority">
    /// Exact capability returned by the dedicated peek.
    /// </param>
    /// <param name="receipt">
    /// Fenced transaction receipt on success; null otherwise.
    /// </param>
    /// <returns>
    /// True only for the first valid take of this exact registered completion.
    /// </returns>
    internal bool TryTakeSpiritualPublication(FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease lease, SpiritualC4PublicationAuthority authority,
        out SpiritualC4PublicationReceipt receipt)
    {
        lock (_gate)
        {
            receipt = null!;
            if (_spiritualTaken is not null || !ReferenceEquals(_spiritualPublication, authority) ||
                !ReferenceEquals(_validatedResult?.Plan, authority.Plan) ||
                _validatedBindingFingerprint != AcceptedMechanicsPlanFingerprints.ComputeInput(authority.Binding) ||
                !authority.CurrentInputsAgree(fileSystem, lease))
                return false;
            receipt = new SpiritualC4PublicationReceipt(authority, _cacheAuthority, _validatedFence);
            _spiritualTaken = receipt;
            ClearValidatedSlotCore();
            return true;
        }
    }

    /// <summary>
    /// Checks retained transaction ownership after publication may have changed physical inputs.
    /// </summary>
    /// <param name="receipt">
    /// Exact receipt returned by this cache's dedicated take.
    /// </param>
    /// <returns>
    /// True while receipt identity, cache fence and completed plan remain current.
    /// </returns>
    internal bool IsTakenSpiritualPublicationCurrent(SpiritualC4PublicationReceipt receipt)
    {
        lock (_gate)
            return ReferenceEquals(_spiritualTaken, receipt) &&
                ReferenceEquals(receipt.CacheOwner, _cacheAuthority) &&
                ReferenceEquals(receipt.Fence, _validatedFence) && receipt.Authority.HasValidSeal();
    }

    /// <summary>
    /// Closes an exact successful publication attempt without writing canonical state.
    /// </summary>
    /// <param name="receipt">
    /// Current transaction receipt; foreign or already settled receipts are rejected.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when this call enables detached completed comparisons and closes the successful attempt; otherwise <see langword="false"/> for a foreign, stale or invalid receipt.
    /// </returns>
    internal bool SettleSpiritualPublication(SpiritualC4PublicationReceipt receipt)
    {
        lock (_gate)
        {
            if (!IsTakenSpiritualPublicationCurrent(receipt))
                return false;
            receipt.Authority.MarkCompletedConflictValidationPublished(_cacheAuthority);
            InvalidateAllCore();
            return true;
        }
    }

    /// <summary>
    /// Revokes an exact failed take even when its plan seal has become invalid.
    /// </summary>
    /// <param name="receipt">
    /// Exact receipt owned by this cache and its current fence.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when this call closes the owned failed attempt; <see langword="false"/> for foreign or settled receipts.
    /// </returns>
    internal bool FailSpiritualPublication(SpiritualC4PublicationReceipt receipt)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_spiritualTaken, receipt) ||
                !ReferenceEquals(receipt.CacheOwner, _cacheAuthority) ||
                !ReferenceEquals(receipt.Fence, _validatedFence))
                return false;
            InvalidateAllCore();
            return true;
        }
    }

    internal AcceptedMechanicsPlanningResult GetOrBuildValidated(
        AcceptedMechanicsInput input)
        => GetOrBuildValidated(input, candidateAdmission: null);

    internal AcceptedMechanicsPlanningResult GetOrBuildValidated(
        AcceptedMechanicsInput input,
        Func<AcceptedMechanicsPlan, IReadOnlyList<ValidationIssue>>?
            candidateAdmission)
    {
        ArgumentNullException.ThrowIfNull(input);
        lock (_gate)
        {
            ClearWoundRepairWaveCore();
            InvalidateValidatedCore();
            if (input.ValidationIssues.Count > 0)
            {
                return new AcceptedMechanicsPlanningResult(
                    null,
                    input.ValidationIssues.ToArray());
            }

            var binding = input.CreateBinding();
            var bindingFingerprint =
                AcceptedMechanicsPlanFingerprints.ComputeInput(binding);
            var fingerprint =
                AcceptedMechanicsPlanFingerprints.ComputePlanningInput(input);
            AcceptedMechanicsPlanningResult result;
            var newlyPlanned = false;
            if (_planningResult != null &&
                string.Equals(_inputFingerprint, fingerprint, StringComparison.Ordinal))
            {
                result = ValidatePlannerResult(input, fingerprint, _planningResult);
                if (!result.Success)
                {
                    InvalidateAllCore();
                    return result;
                }
            }
            else
            {
                var candidate = _planner(input, fingerprint) ??
                    throw new InvalidOperationException("Accepted mechanics planner returned null.");
                result = ValidatePlannerResult(input, fingerprint, candidate);
                if (candidate.Success && !result.Success)
                {
                    InvalidateAllCore();
                    return result;
                }
                newlyPlanned = true;
            }

            if (!result.Success)
            {
                InvalidateValidatedCore();
                return result;
            }

            if (candidateAdmission is not null)
            {
                var admissionIssues = candidateAdmission(result.Plan!);
                if (admissionIssues.Count != 0)
                {
                    InvalidateValidatedCore();
                    return new AcceptedMechanicsPlanningResult(
                        null,
                        admissionIssues);
                }
            }

            if (newlyPlanned)
            {
                _inputFingerprint = fingerprint;
                _planningResult = result;
            }

            _validatedBindingFingerprint = bindingFingerprint;
            _validatedBinding = input.CreateBinding();
            _validatedResult = result;
            return result;
        }
    }

    internal bool TryTakeValidated(
        AcceptedMechanicsPlanBinding liveBinding,
        out AcceptedMechanicsPlanningResult result)
    {
        ArgumentNullException.ThrowIfNull(liveBinding);
        var fingerprint = AcceptedMechanicsPlanFingerprints.ComputeInput(liveBinding);
        lock (_gate)
        {
            if (_spiritualPublication is null && _spiritualTaken is null &&
                _validatedResult != null &&
                string.Equals(
                    _validatedBindingFingerprint,
                    fingerprint,
                    StringComparison.Ordinal) &&
                PreparedFingerprintAgrees(_validatedResult))
            {
                result = _validatedResult;
                InvalidateValidatedCore();
                return true;
            }
            InvalidateAllCore();
        }

        result = null!;
        return false;
    }

    internal bool TryTakeValidatedTreatmentPublication(
        AcceptedMechanicsPlanBinding liveBinding,
        AcceptedMechanicsPlan expectedPlan,
        out AcceptedMechanicsPlanningResult result,
        out ValidatedPublicationTakeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(liveBinding);
        ArgumentNullException.ThrowIfNull(expectedPlan);
        var fingerprint = AcceptedMechanicsPlanFingerprints.ComputeInput(liveBinding);
        lock (_gate)
        {
            if (_spiritualPublication is null && _spiritualTaken is null &&
                _validatedBinding is not null &&
                _validatedResult is { Plan: { } plan } current &&
                ReferenceEquals(plan, expectedPlan) &&
                string.Equals(
                    _validatedBindingFingerprint,
                    fingerprint,
                    StringComparison.Ordinal) &&
                PreparedFingerprintAgrees(current))
            {
                var frozenBinding = CloneBinding(_validatedBinding);
                result = current;
                snapshot = new ValidatedPublicationTakeSnapshot(
                    _cacheAuthority,
                    _validatedFence,
                    fingerprint,
                    frozenBinding,
                    current,
                    plan,
                    plan.PreparedPlanFingerprint);
                ClearValidatedSlotCore();
                return true;
            }

        }

        result = null!;
        snapshot = null!;
        return false;
    }

    internal bool IsTreatmentPublicationTakeCurrent(
        ValidatedPublicationTakeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            return PublicationTakeSnapshotAgrees(snapshot) &&
                   ReferenceEquals(_validatedFence, snapshot.Fence) &&
                   _validatedBindingFingerprint is null &&
                   _validatedBinding is null &&
                   _validatedResult is null;
        }
    }

    internal bool TryRearmValidatedTreatmentPublication(
        ValidatedPublicationTakeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            if (!PublicationTakeSnapshotAgrees(snapshot) ||
                !ReferenceEquals(_validatedFence, snapshot.Fence) ||
                _validatedBindingFingerprint is not null ||
                _validatedBinding is not null ||
                _validatedResult is not null)
            {
                return false;
            }

            _validatedBindingFingerprint = snapshot.BindingFingerprint;
            _validatedBinding = CloneBinding(snapshot.Binding);
            _validatedResult = snapshot.Result;
            return true;
        }
    }

    internal bool IsTreatmentPublicationRearmed(
        ValidatedPublicationTakeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            return PublicationTakeSnapshotAgrees(snapshot) &&
                   ReferenceEquals(_validatedFence, snapshot.Fence) &&
                   string.Equals(
                       _validatedBindingFingerprint,
                       snapshot.BindingFingerprint,
                       StringComparison.Ordinal) &&
                   _validatedBinding is not null &&
                   string.Equals(
                       AcceptedMechanicsPlanFingerprints.ComputeInput(
                           _validatedBinding),
                       snapshot.BindingFingerprint,
                       StringComparison.Ordinal) &&
                   ReferenceEquals(_validatedResult, snapshot.Result) &&
                   ReferenceEquals(_validatedResult?.Plan, snapshot.Plan) &&
                   PreparedFingerprintAgrees(snapshot.Result);
        }
    }

    internal void InvalidateValidated()
    {
        lock (_gate)
            InvalidateValidatedCore();
    }

    internal void InvalidateAll()
    {
        lock (_gate)
            InvalidateAllCore();
    }

    internal bool TryPeekValidated(
        out AcceptedMechanicsPlanBinding binding,
        out AcceptedMechanicsPlanningResult result)
    {
        lock (_gate)
        {
            if (_validatedBinding != null && _validatedResult != null)
            {
                if (!PreparedFingerprintAgrees(_validatedResult))
                {
                    InvalidateAllCore();
                    binding = null!;
                    result = null!;
                    return false;
                }
                binding = CloneBinding(_validatedBinding);
                result = _validatedResult;
                return true;
            }
        }
        binding = null!;
        result = null!;
        return false;
    }

    private void InvalidateValidatedCore()
    {
        _spiritualPublication?.Revoke();
        _spiritualTaken?.Authority.Revoke();
        _spiritualTaken = null;
        _validatedFence = new object();
        ClearValidatedSlotCore();
    }

    private void ClearValidatedSlotCore()
    {
        _spiritualPublication = null;
        _validatedBindingFingerprint = null;
        _validatedBinding = null;
        _validatedResult = null;
    }

    private void InvalidateAllCore()
    {
        _inputFingerprint = null;
        _planningResult = null;
        InvalidateValidatedCore();
        ClearWoundRepairWaveCore();
    }

    private bool PublicationTakeSnapshotAgrees(
        ValidatedPublicationTakeSnapshot snapshot)
    {
        if (!ReferenceEquals(snapshot.CacheAuthority, _cacheAuthority) ||
            !ReferenceEquals(snapshot.Result.Plan, snapshot.Plan) ||
            !string.Equals(
                snapshot.BindingFingerprint,
                AcceptedMechanicsPlanFingerprints.ComputeInput(snapshot.Binding),
                StringComparison.Ordinal) ||
            !string.Equals(
                snapshot.PreparedPlanFingerprint,
                snapshot.Plan.PreparedPlanFingerprint,
                StringComparison.Ordinal))
        {
            return false;
        }

        return PreparedFingerprintAgrees(snapshot.Result);
    }

    private static AcceptedMechanicsPlanBinding CloneBinding(
        AcceptedMechanicsPlanBinding binding) => new(
        binding.SessionId,
        binding.RequestId,
        binding.SnapshotToken,
        binding.Realm,
        binding.Turn,
        binding.AcceptedEvents,
        binding.ResourceCommands,
        binding.EffectCommands,
        binding.PendingInput,
        binding.InternalInputs,
        binding.AuthorityFingerprints,
        binding.BeforeImages,
        binding.WoundCommands,
        binding.WoundInput);

    private void ClearWoundRepairWaveCore()
    {
        _woundRepairAuthority = null;
        _woundRepairPackets.Clear();
        _consumedWoundRepairReceipts.Clear();
    }

    private static bool ValidWoundRepairAuthority(
        WoundRepairPacketAuthority authority) =>
        ResourceMaterializationContract.IsExactIdentifier(authority.SessionId) &&
        ResourceMaterializationContract.IsExactIdentifier(authority.RequestId) &&
        ResourceMaterializationContract.IsExactIdentifier(authority.SnapshotToken) &&
        ResourceMaterializationContract.IsExactIdentifier(authority.Generation) &&
        ResourceMaterializationContract.IsAuthorityFingerprint(
            authority.EventFingerprint) &&
        ResourceMaterializationContract.IsAuthorityFingerprint(
            authority.TargetFingerprint) &&
        ResourceMaterializationContract.IsAuthorityFingerprint(
            authority.RollFingerprint);

    private static bool WoundRepairPacketAgrees(
        WoundRepairPacketAuthority authority,
        WoundRepairPacket packet) =>
        string.Equals(
            authority.SessionId,
            packet.SessionId,
            StringComparison.Ordinal) &&
        string.Equals(
            authority.RequestId,
            packet.RequestId,
            StringComparison.Ordinal) &&
        string.Equals(
            authority.SnapshotToken,
            packet.SnapshotToken,
            StringComparison.Ordinal) &&
        ResourceMaterializationContract.IsExactIdentifier(packet.CandidateRef) &&
        ResourceMaterializationContract.IsAuthorityFingerprint(
            packet.SemanticFingerprint);

    private static AcceptedMechanicsPlanningResult ValidatePlannerResult(
        AcceptedMechanicsInput input,
        string fingerprint,
        AcceptedMechanicsPlanningResult result)
    {
        if (!result.Success)
        {
            if (result.Plan != null)
                return Failed("accepted_mechanics_partial_plan", "failed plan with no after-images");
            return result.Issues.Count == 0
                ? Failed("accepted_mechanics_plan_missing", "complete plan or bounded validation issues")
                : result;
        }

        var plan = result.Plan!;
        if (!string.Equals(plan.InputFingerprint, fingerprint, StringComparison.Ordinal))
            return Failed("accepted_mechanics_plan_fingerprint_mismatch", fingerprint);
        if (plan.AuthorityFingerprints != input.AuthorityFingerprints)
            return Failed("accepted_mechanics_authority_fingerprint_mismatch", "exact input authority fingerprints");
        if (!BeforeImagesEqual(plan.BeforeImages, input.BeforeImages))
            return Failed("accepted_mechanics_before_image_mismatch", "exact input before-images");
        if (!WoundStagesAgree(input, plan))
        {
            return Failed(
                "accepted_mechanics_wound_stage_mismatch",
                "the exact full-input wound stage bundle from the planning context");
        }
        if (!WoundCommandPathAgrees(input, plan))
        {
            return Failed(
                "accepted_mechanics_wound_command_path_mismatch",
                "a present wound command root touched, consumed, and protected by one exact before-image");
        }
        if (!PendingPublicationAuthorityAgrees(input, plan))
        {
            return Failed(
                "accepted_mechanics_pending_authority_mismatch",
                "pending publication authority for the exact session, request, turn, and full accepted-turn fingerprint");
        }
        if (!PreparedFingerprintAgrees(result))
        {
            return Failed(
                "accepted_mechanics_prepared_plan_fingerprint_mismatch",
                "one independently recomputed complete prepared-plan fingerprint");
        }
        return result;
    }

    private static bool PendingPublicationAuthorityAgrees(
        AcceptedMechanicsInput input,
        AcceptedMechanicsPlan plan)
    {
        var authority = plan.PendingPublicationAuthority;
        return plan.AwaitsPendingResolution
            ? authority is not null && authority.AgreesWith(input)
            : authority is null;
    }

    private static bool WoundStagesAgree(
        AcceptedMechanicsInput input,
        AcceptedMechanicsPlan plan)
    {
        try
        {
            var woundInput = input.WoundInput;
            var expected = input.PlanningContext?.WoundStageBundle;
            var actual = plan.WoundStageBundle;
            if ((woundInput is null) != (expected is null) ||
                (expected is null) != (actual is null))
            {
                return false;
            }
            if (expected is null)
                return true;
            var woundBinding = woundInput!.Binding;
            return string.Equals(
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
                   input.Turn == woundBinding.Turn &&
                   string.Equals(
                       WoundAcceptedTurnFingerprints.ComputeInput(woundInput),
                       expected.InputFingerprint,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       expected.BundleFingerprint,
                       actual!.BundleFingerprint,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       input.PlanningContext?.WoundAnchorPlan?.Fingerprint,
                       plan.WoundAnchorPlanFingerprint,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       input.PlanningContext?.DirectWoundPublicationAuthority
                           ?.Fingerprint,
                       plan.DirectWoundPublicationAuthority?.Fingerprint,
                       StringComparison.Ordinal);
        }
        catch (Exception exception) when (IsMalformedBoundary(exception))
        {
            return false;
        }
    }

    private static bool WoundCommandPathAgrees(
        AcceptedMechanicsInput input,
        AcceptedMechanicsPlan plan)
    {
        var present = input.WoundCommands is not null;
        var touched = plan.TouchedPaths.Contains(
            AcceptedMechanicsPlan.WoundCommandPath,
            StringComparer.Ordinal);
        var consumed = plan.ConsumedPaths.Contains(
            AcceptedMechanicsPlan.WoundCommandPath,
            StringComparer.Ordinal);
        if (plan.AwaitsPendingResolution)
        {
            return !touched &&
                   !consumed &&
                   (!present || plan.BeforeImages.ContainsKey(
                       AcceptedMechanicsPlan.WoundCommandPath));
        }
        return present == touched &&
               present == consumed &&
               (!present || plan.BeforeImages.ContainsKey(
                   AcceptedMechanicsPlan.WoundCommandPath));
    }

    private static bool PreparedFingerprintAgrees(
        AcceptedMechanicsPlanningResult result)
    {
        if (!result.Success || result.Plan is null)
            return false;
        try
        {
            return string.Equals(
                result.Plan.PreparedPlanFingerprint,
                AcceptedMechanicsPlanFingerprints.ComputePrepared(result.Plan),
                StringComparison.Ordinal);
        }
        catch (Exception exception) when (IsMalformedBoundary(exception))
        {
            return false;
        }
    }

    private static bool IsMalformedBoundary(Exception exception) =>
        exception is ArgumentException or InvalidOperationException or
            JsonException or NullReferenceException;

    private static bool BeforeImagesEqual(
        IReadOnlyDictionary<string, CanonicalBeforeImage> left,
        IReadOnlyDictionary<string, CanonicalBeforeImage> right)
    {
        if (left.Count != right.Count)
            return false;
        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var value) ||
                pair.Value.Existed != value.Existed ||
                !BytesEqual(pair.Value.Bytes, value.Bytes))
            {
                return false;
            }
        }
        return true;
    }

    private static bool BytesEqual(byte[]? left, byte[]? right) =>
        left == null || right == null
            ? left == null && right == null
            : left.AsSpan().SequenceEqual(right);

    private static AcceptedMechanicsPlanningResult Failed(string code, string expected) =>
        new(
            null,
            new[]
            {
                new ValidationIssue(
                    "acceptedMechanicsPlan",
                    IssueSeverity.Error,
                    "Accepted mechanics planner returned an invalid publication plan.",
                    code: code,
                    section: "accepted_mechanics",
                    expected: expected,
                    actual: "planner result",
                    repairHint: "Discard the plan and rebuild once from the complete validated accepted-turn input.")
            });

}

internal static class AcceptedMechanicsPlanAuthority
{
    internal static AcceptedMechanicsPlanningResult
        GetOrBuildMortalWoundTreatmentValidated(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            AcceptedMechanicsInput input,
            AcceptedMechanicsWoundStageBundle bundle,
            MortalWoundTreatmentAcceptedStateAuthority acceptedState,
            MortalWoundTreatmentAttemptRequest request,
            MortalWoundTreatmentResolution resolution,
            string semanticFingerprint,
            object continuationAuthority,
            object reservationAuthority)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(acceptedState);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(continuationAuthority);
        ArgumentNullException.ThrowIfNull(reservationAuthority);
        ArgumentException.ThrowIfNullOrWhiteSpace(semanticFingerprint);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        var context = input.PlanningContext;
        var supplied = context?.WoundStageBundle;
        if (input.WoundInput is null ||
            context is null ||
            supplied is null ||
            context.WoundAnchorPlan is not null ||
            context.DirectWoundPublicationAuthority is not null ||
            !string.Equals(
                supplied.BundleFingerprint,
                bundle.BundleFingerprint,
                StringComparison.Ordinal))
        {
            return WoundAnchorContextFailure(
                supplied?.BundleFingerprint ?? "missing");
        }
        return AcceptedTurnAuthorityRegistry
            .GetOrBuildCommonMortalWoundTreatmentValidated(
                fileSystem,
                writeLease,
                input,
                bundle,
                acceptedState,
                request,
                resolution,
                semanticFingerprint,
                continuationAuthority,
                reservationAuthority);
    }

    internal static AcceptedMechanicsPlanningResult GetOrBuildWoundValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        AcceptedMechanicsInput input,
        AcceptedMechanicsWoundStageBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(bundle);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);

        var planningContext = input.PlanningContext;
        var suppliedBundle = planningContext?.WoundStageBundle;
        if (input.WoundInput is null || planningContext is null ||
            suppliedBundle is null ||
            !string.Equals(
                suppliedBundle.BundleFingerprint,
                bundle.BundleFingerprint,
                StringComparison.Ordinal))
        {
            return WoundAnchorContextFailure(
                suppliedBundle?.BundleFingerprint ?? "missing");
        }

        if (bundle.PreparedPlan.RecoveryContinuationAuthority is { } recoveryAuthority)
        {
            if (!WoundAcceptedTurnPlanner.RecoveryContinuationFinalPlanAgrees(recoveryAuthority, bundle) ||
                planningContext.WoundAnchorPlan is not null ||
                planningContext.DirectWoundPublicationAuthority is not null ||
                planningContext.TreatmentResourcePublicationAuthority is not null)
                return WoundAnchorContextFailure("changed or competing recovery authority");
            return AcceptedTurnAuthorityRegistry.GetOrBuildCommonWoundValidated(
                fileSystem, writeLease, input, bundle);
        }
        var anchors = MortalWoundCanonicalAnchorPlan.Create(
            fileSystem,
            writeLease,
            bundle);
        if (!anchors.Success || anchors.Plan is null)
            return new AcceptedMechanicsPlanningResult(null, anchors.Issues);

        try
        {
            var anchoredInput = input.WithPlanningContext(
                planningContext.WithWoundAnchorPlan(anchors.Plan));
            return AcceptedTurnAuthorityRegistry.GetOrBuildCommonWoundValidated(
                fileSystem,
                writeLease,
                anchoredInput,
                bundle);
        }
        catch (ArgumentException)
        {
            return WoundAnchorContextFailure(
                "the supplied planning context rejected the sealed anchor plan");
        }
    }

    internal static AcceptedMechanicsPlanningResult GetOrBuildWoundValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        AcceptedMechanicsWoundStageBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(bundle);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);

        if (bundle.PreparedPlan.RecoveryContinuationAuthority is not null)
        {
            var recovery = AcceptedMechanicsWoundCommonInputComposer.ComposeRecoveryContinuation(
                fileSystem, writeLease, bundle);
            if (!recovery.Success || recovery.Input is null)
                return new AcceptedMechanicsPlanningResult(null, recovery.Issues);
            return AcceptedTurnAuthorityRegistry.GetOrBuildCommonWoundValidated(
                fileSystem, writeLease, recovery.Input, bundle);
        }
        var anchors = MortalWoundCanonicalAnchorPlan.Create(
            fileSystem,
            writeLease,
            bundle);
        if (!anchors.Success || anchors.Plan is null)
            return new AcceptedMechanicsPlanningResult(null, anchors.Issues);
        var composed = AcceptedMechanicsWoundCommonInputComposer.Compose(
            fileSystem,
            writeLease,
            bundle,
            anchors.Plan);
        if (!composed.Success || composed.Input is null)
            return new AcceptedMechanicsPlanningResult(null, composed.Issues);
        return AcceptedTurnAuthorityRegistry.GetOrBuildCommonWoundValidated(
            fileSystem,
            writeLease,
            composed.Input,
            bundle);
    }

    private static AcceptedMechanicsPlanningResult WoundAnchorContextFailure(
        string actual) => new(
        null,
        new[]
        {
            new ValidationIssue(
                "acceptedMechanicsPlan.woundAnchorPlan",
                IssueSeverity.Error,
                "Canonical Mortal wound anchors cannot be attached to a foreign common planning input.",
                code: "accepted_mechanics_wound_anchor_context_mismatch",
                actor: "Client",
                section: "wound_materialization",
                expected:
                    "the exact complete accepted-mechanics input and wound stage bundle",
                actual: actual,
                repairHint:
                    "Rebuild the common accepted-turn input and canonical anchor plan from one validated handoff.")
        });

    /// <summary>
    /// Registers an exact completed spiritual plan without invoking the planner.
    /// </summary>
    /// <param name="fileSystem">
    /// Owning filesystem instance.
    /// </param>
    /// <param name="lease">
    /// Active canonical write lease for the owning filesystem.
    /// </param>
    /// <param name="authority">
    /// Exact owner-issued capability, returned on a successful peek.
    /// </param>
    /// <returns>
    /// True only when the owner-issued completed plan is registered.
    /// </returns>
    internal static bool RegisterSpiritualPublication(FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease lease, SpiritualC4PublicationAuthority authority) =>
        AcceptedTurnAuthorityRegistry.RegisterSpiritualPublication(fileSystem, lease, authority);

    /// <summary>
    /// Reads the dedicated spiritual publication capability without consuming it.
    /// </summary>
    /// <param name="fileSystem">
    /// Owning filesystem instance.
    /// </param>
    /// <param name="lease">
    /// Active canonical write lease for the owning filesystem.
    /// </param>
    /// <param name="authority">
    /// Exact owner-issued capability, returned on a successful peek.
    /// </param>
    /// <returns>
    /// True only when a sealed untaken publication is available.
    /// </returns>
    internal static bool TryPeekSpiritualPublication(FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease lease, out SpiritualC4PublicationAuthority authority) =>
        AcceptedTurnAuthorityRegistry.TryPeekSpiritualPublication(fileSystem, lease, out authority);

    /// <summary>
    /// Takes one exact spiritual publication after checking current physical inputs.
    /// </summary>
    /// <param name="fileSystem">
    /// Owning filesystem instance.
    /// </param>
    /// <param name="lease">
    /// Active canonical write lease for the owning filesystem.
    /// </param>
    /// <param name="authority">
    /// Exact owner-issued capability, returned on a successful peek.
    /// </param>
    /// <param name="receipt">
    /// Exact fenced take receipt, returned on a successful take.
    /// </param>
    /// <returns>
    /// True only for the first valid take of the exact registered capability.
    /// </returns>
    internal static bool TryTakeSpiritualPublication(FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease lease, SpiritualC4PublicationAuthority authority, out SpiritualC4PublicationReceipt receipt) =>
        AcceptedTurnAuthorityRegistry.TryTakeSpiritualPublication(fileSystem, lease, authority, out receipt);

    /// <summary>
    /// Checks the exact retained spiritual transaction receipt and invalidation fence.
    /// </summary>
    /// <param name="fileSystem">
    /// Owning filesystem instance.
    /// </param>
    /// <param name="lease">
    /// Active canonical write lease for the owning filesystem.
    /// </param>
    /// <param name="receipt">
    /// Exact fenced take receipt, returned on a successful take.
    /// </param>
    /// <returns>
    /// True only while the exact taken completion remains owned by the cache.
    /// </returns>
    internal static bool IsTakenSpiritualPublicationCurrent(FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease lease, SpiritualC4PublicationReceipt receipt) =>
        AcceptedTurnAuthorityRegistry.IsTakenSpiritualPublicationCurrent(fileSystem, lease, receipt);

    /// <summary>
    /// Closes a successful spiritual publication and revokes its capability.
    /// </summary>
    /// <param name="fileSystem">
    /// Owning filesystem instance.
    /// </param>
    /// <param name="lease">
    /// Active canonical write lease for the owning filesystem.
    /// </param>
    /// <param name="receipt">
    /// Exact fenced take receipt, returned on a successful take.
    /// </param>
    /// <returns>
    /// True only when the current publication attempt is settled.
    /// </returns>
    internal static bool CompleteSpiritualPublication(FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease lease, SpiritualC4PublicationReceipt receipt) =>
        AcceptedTurnAuthorityRegistry.CompleteSpiritualPublication(fileSystem, lease, receipt);

    /// <summary>
    /// Closes a failed spiritual publication after the common transaction restores state.
    /// </summary>
    /// <param name="fileSystem">
    /// Owning filesystem instance.
    /// </param>
    /// <param name="lease">
    /// Active canonical write lease for the owning filesystem.
    /// </param>
    /// <param name="receipt">
    /// Exact fenced take receipt, returned on a successful take.
    /// </param>
    /// <returns>
    /// True only when the current publication attempt is settled.
    /// </returns>
    internal static bool FailSpiritualPublication(FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease lease, SpiritualC4PublicationReceipt receipt) =>
        AcceptedTurnAuthorityRegistry.FailSpiritualPublication(fileSystem, lease, receipt);

    internal static AcceptedMechanicsPlanningResult GetOrBuildValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        AcceptedMechanicsInput input)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(input);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        return AcceptedTurnAuthorityRegistry.GetOrBuildCommonValidated(
            fileSystem,
            writeLease,
            input);
    }

    internal static bool TryTakeValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        AcceptedMechanicsPlanBinding liveBinding,
        out AcceptedMechanicsPlanningResult result)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(liveBinding);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        return AcceptedTurnAuthorityRegistry.TryTakeCommonValidated(
            fileSystem,
            writeLease,
            liveBinding,
            out result);
    }

    internal static bool TryTakeValidatedTreatmentPublication(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        AcceptedMechanicsPlanBinding liveBinding,
        MortalItemAcceptedTurnNormalizationSnapshot mortalItemSnapshot,
        out AcceptedMechanicsPlanningResult result,
        out MortalWoundTreatmentPublicationTakeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(liveBinding);
        ArgumentNullException.ThrowIfNull(mortalItemSnapshot);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        return AcceptedTurnAuthorityRegistry
            .TryTakeCommonMortalWoundTreatmentPublication(
                fileSystem,
                writeLease,
                liveBinding,
                mortalItemSnapshot,
                out result,
                out receipt);
    }

    internal static bool TryTakeValidatedTreatmentPublicationForTerminalRelease(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        AcceptedMechanicsPlanBinding liveBinding,
        out AcceptedMechanicsPlanningResult result,
        out MortalWoundTreatmentPublicationTakeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(liveBinding);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        return AcceptedTurnAuthorityRegistry
            .TryTakeCommonMortalWoundTreatmentPublicationForTerminalRelease(
                fileSystem,
                writeLease,
                liveBinding,
                out result,
                out receipt);
    }

    internal static bool
        TryTakeCurrentValidatedTreatmentPublicationForTerminalRelease(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            AcceptedMechanicsPlanBinding liveBinding,
            MortalItemAcceptedTurnNormalizationSnapshot mortalItemSnapshot,
            out AcceptedMechanicsPlanningResult result,
            out MortalWoundTreatmentPublicationTakeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(liveBinding);
        ArgumentNullException.ThrowIfNull(mortalItemSnapshot);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        return AcceptedTurnAuthorityRegistry
            .TryTakeCurrentCommonMortalWoundTreatmentPublicationForTerminalRelease(
                fileSystem,
                writeLease,
                liveBinding,
                mortalItemSnapshot,
                out result,
                out receipt);
    }

    internal static bool IsTakenTreatmentPublicationCurrent(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        AcceptedMechanicsPlanBinding liveBinding,
        AcceptedMechanicsPlan plan,
        MortalWoundTreatmentPublicationTakeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(liveBinding);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(receipt);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        return AcceptedTurnAuthorityRegistry
            .IsTakenMortalWoundTreatmentPublicationCurrent(
                fileSystem,
                writeLease,
                liveBinding,
                plan,
                receipt);
    }

    internal static MortalWoundTreatmentPublicationProbeResult
        ProbeTakenTreatmentPublication(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            MortalWoundTreatmentPublicationTakeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(receipt);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        return AcceptedTurnAuthorityRegistry
            .ProbeTakenMortalWoundTreatmentPublication(
                fileSystem,
                writeLease,
                receipt);
    }

    internal static MortalWoundTreatmentPublicationOperationResult
        CompleteTakenTreatmentPublication(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            MortalWoundTreatmentPublicationTakeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(receipt);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        return AcceptedTurnAuthorityRegistry
            .CompleteTakenMortalWoundTreatmentPublication(
                fileSystem,
                writeLease,
                receipt);
    }

    internal static MortalWoundTreatmentPublicationOperationResult
        RearmTakenTreatmentPublication(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            MortalWoundTreatmentPublicationTakeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(receipt);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        return AcceptedTurnAuthorityRegistry
            .RearmTakenMortalWoundTreatmentPublication(
                fileSystem,
                writeLease,
                receipt);
    }

    internal static MortalWoundTreatmentPublicationOperationResult
        CloseTakenTreatmentPublicationAfterQuarantine(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            MortalWoundTreatmentPublicationTakeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(receipt);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        return AcceptedTurnAuthorityRegistry
            .CloseTakenMortalWoundTreatmentPublicationAfterQuarantine(
                fileSystem,
                writeLease,
                receipt);
    }

    internal static MortalWoundTreatmentPublicationOperationResult
        ReleaseTakenTreatmentPublicationTerminal(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            MortalWoundTreatmentPublicationTakeReceipt receipt,
            string reason)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        return AcceptedTurnAuthorityRegistry
            .ReleaseTakenMortalWoundTreatmentPublicationTerminal(
                fileSystem,
                writeLease,
                receipt,
                reason);
    }

    internal static MortalWoundTreatmentPublicationOperationResult
        FailTakenTreatmentPublicationTerminal(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            MortalWoundTreatmentPublicationTakeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(receipt);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        return AcceptedTurnAuthorityRegistry
            .FailTakenMortalWoundTreatmentPublicationTerminal(
                fileSystem,
                writeLease,
                receipt);
    }

    internal static bool TryRegisterWoundRepairWave(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        WoundRepairPacketAuthority authority,
        IReadOnlyList<WoundRepairPacket> packets)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(packets);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        return AcceptedTurnAuthorityRegistry.TryRegisterWoundRepairWave(
            fileSystem,
            writeLease,
            authority,
            packets);
    }

    internal static bool TryTakeWoundRepairPacket(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        WoundRepairPacketAuthority liveAuthority,
        WoundRepairPacketReceipt receipt,
        out WoundRepairPacket packet)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(liveAuthority);
        ArgumentNullException.ThrowIfNull(receipt);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        return AcceptedTurnAuthorityRegistry.TryTakeWoundRepairPacket(
            fileSystem,
            writeLease,
            liveAuthority,
            receipt,
            out packet);
    }

    internal static bool HasWoundRepairWave(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        return AcceptedTurnAuthorityRegistry.HasWoundRepairWave(
            fileSystem,
            writeLease);
    }

    internal static void InvalidateValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        AcceptedTurnAuthorityRegistry.InvalidateCommonValidated(
            fileSystem,
            writeLease);
    }

    internal static bool HasValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        return AcceptedTurnAuthorityRegistry.HasCommonValidated(
            fileSystem,
            writeLease);
    }

    internal static bool TryPeekValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        out AcceptedMechanicsPlanBinding binding,
        out AcceptedMechanicsPlanningResult result)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        return AcceptedTurnAuthorityRegistry.TryPeekCommonValidated(
            fileSystem,
            writeLease,
            out binding,
            out result);
    }
}
