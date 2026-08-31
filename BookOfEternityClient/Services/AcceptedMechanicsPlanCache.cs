using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal delegate AcceptedMechanicsPlanningResult AcceptedMechanicsPlanFactory(
    AcceptedMechanicsInput input,
    string inputFingerprint);

internal sealed class AcceptedMechanicsPlanCache
{
    private readonly object _gate = new();
    private readonly AcceptedMechanicsPlanFactory _planner;
    private string? _inputFingerprint;
    private AcceptedMechanicsPlanningResult? _planningResult;
    private string? _validatedBindingFingerprint;
    private AcceptedMechanicsPlanBinding? _validatedBinding;
    private AcceptedMechanicsPlanningResult? _validatedResult;
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
                return _validatedResult != null;
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

    internal AcceptedMechanicsPlanningResult GetOrBuildValidated(
        AcceptedMechanicsInput input)
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
                _inputFingerprint = fingerprint;
                _planningResult = result;
            }

            if (!result.Success)
            {
                InvalidateValidatedCore();
                return result;
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
            if (_validatedResult != null &&
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
                binding = new AcceptedMechanicsPlanBinding(
                    _validatedBinding.SessionId,
                    _validatedBinding.RequestId,
                    _validatedBinding.SnapshotToken,
                    _validatedBinding.Realm,
                    _validatedBinding.Turn,
                    _validatedBinding.AcceptedEvents,
                    _validatedBinding.ResourceCommands,
                    _validatedBinding.EffectCommands,
                    _validatedBinding.PendingInput,
                    _validatedBinding.InternalInputs,
                    _validatedBinding.AuthorityFingerprints,
                    _validatedBinding.BeforeImages,
                    _validatedBinding.WoundCommands,
                    _validatedBinding.WoundInput);
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
