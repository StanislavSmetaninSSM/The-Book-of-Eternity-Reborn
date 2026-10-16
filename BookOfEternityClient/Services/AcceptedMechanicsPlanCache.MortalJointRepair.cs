namespace BookOfEternityClient.Services;

internal sealed partial class AcceptedMechanicsPlanCache
{
    // Non-authorizing exclusion: no validation, invalidation or planner call.
    // A terminal pre-plan token must not erase a raw/invalid common candidate.
    internal bool HasPlanOrPublicationForJointRelease
    {
        get
        {
            lock (_gate)
                return _planningResult?.Plan is not null || _validatedResult is not null ||
                    _spiritualPublication is not null || _spiritualTaken is not null;
        }
    }

    internal bool TryTakeValidatedJointPublication(MortalJointRepairPublicationPreparation preparation,
        out AcceptedMechanicsPlanningResult result, out ValidatedPublicationTakeSnapshot snapshot)
    {
        lock (_gate)
        {
            var fingerprint = AcceptedMechanicsPlanFingerprints.ComputeInput(preparation.Binding);
            if (_spiritualPublication is null && _spiritualTaken is null && _validatedBinding is not null &&
                _validatedResult is { Plan: { } plan } current && ReferenceEquals(plan, preparation.Plan) &&
                ReferenceEquals(plan.JointRepairCommonAuthority, preparation.Authority) &&
                _validatedBindingFingerprint == fingerprint && PreparedFingerprintAgrees(current))
            {
                result = current;
                snapshot = new(_cacheAuthority, _validatedFence, fingerprint, CloneBinding(_validatedBinding),
                    current, plan, plan.PreparedPlanFingerprint);
                ClearValidatedSlotCore();
                return true;
            }
            result = null!; snapshot = null!; return false;
        }
    }

    internal bool TryRearmValidatedJointPublication(MortalJointRepairPublicationPreparation preparation,
        ValidatedPublicationTakeSnapshot snapshot)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(snapshot.Plan, preparation.Plan) ||
                !ReferenceEquals(snapshot.Plan.JointRepairCommonAuthority, preparation.Authority) ||
                !PublicationTakeSnapshotAgrees(snapshot) || !ReferenceEquals(_validatedFence, snapshot.Fence) ||
                _validatedBindingFingerprint is not null || _validatedBinding is not null || _validatedResult is not null)
                return false;
            _validatedBindingFingerprint = snapshot.BindingFingerprint;
            _validatedBinding = CloneBinding(snapshot.Binding);
            _validatedResult = snapshot.Result;
            return true;
        }
    }

    internal sealed record JointCandidateFence(object Cache, object Fence,
        WoundAcceptedTurnPlanner.MortalJointRepairCommonAuthority Authority, AcceptedMechanicsInput Input,
        string InputFingerprint, AcceptedMechanicsPlanningResult Result);

    // Planning is allowed here; validation/takeability is not. Fresh final source
    // IO runs outside both cache and registry monitors before the exact fence seals.
    internal (AcceptedMechanicsPlanningResult Result, JointCandidateFence? Fence) CreateJointCandidate(
        AcceptedMechanicsInput input, WoundAcceptedTurnPlanner.MortalJointRepairCommonAuthority authority)
    {
        lock (_gate)
        {
            if (!authority.MatchesInput(input) || input.ValidationIssues.Count != 0)
                return (new(null, input.ValidationIssues.Count != 0 ? input.ValidationIssues : JointCacheIssue("foreign input")), null);
            ClearWoundRepairWaveCore();
            InvalidateValidatedCore();
            var fingerprint = AcceptedMechanicsPlanFingerprints.ComputePlanningInput(input);
            var planned = _inputFingerprint == fingerprint && _planningResult is not null &&
                ReferenceEquals(_planningResult.Plan?.JointRepairCommonAuthority, authority)
                ? _planningResult : AcceptedMechanicsPlanner.AssembleRetainedJointPlan(input, fingerprint);
            var result = ValidatePlannerResult(input, fingerprint, planned);
            if (!result.Success) { InvalidateAllCore(); return (result, null); }
            _inputFingerprint = fingerprint;
            _planningResult = result;
            return (result, new(_cacheAuthority, _validatedFence, authority, input, fingerprint, result));
        }
    }
    internal AcceptedMechanicsPlanningResult SealJointCandidate(AcceptedMechanicsInput input,
        WoundAcceptedTurnPlanner.MortalJointRepairCommonAuthority authority, JointCandidateFence fence,
        object admissionProof, AcceptedMechanicsPlan candidate)
    {
        lock (_gate)
        {
            if (!JointFenceAgrees(input, authority, fence, candidate) ||
                !WoundAcceptedTurnPlanner.JointCandidateAdmissionAgrees(admissionProof, authority, candidate))
                return new(null, JointCacheIssue("stale candidate/cache fence"));
            var validated = ValidatePlannerResult(input, fence.InputFingerprint, fence.Result);
            if (!validated.Success) { InvalidateAllCore(); return validated; }
            _validatedBinding = CloneBinding(input.CreateBinding());
            _validatedBindingFingerprint = AcceptedMechanicsPlanFingerprints.ComputeInput(_validatedBinding);
            _validatedResult = validated;
            return validated;
        }
    }
    internal void RevokeJointCandidate(WoundAcceptedTurnPlanner.MortalJointRepairCommonAuthority authority,
        JointCandidateFence fence)
    {
        lock (_gate)
            if (ReferenceEquals(fence.Cache, _cacheAuthority) && ReferenceEquals(fence.Fence, _validatedFence) &&
                ReferenceEquals(fence.Authority, authority)) InvalidateAllCore();
    }
    private bool JointFenceAgrees(AcceptedMechanicsInput input,
        WoundAcceptedTurnPlanner.MortalJointRepairCommonAuthority authority, JointCandidateFence fence,
        AcceptedMechanicsPlan candidate) => ReferenceEquals(fence.Cache, _cacheAuthority) &&
        ReferenceEquals(fence.Fence, _validatedFence) && ReferenceEquals(fence.Authority, authority) &&
        ReferenceEquals(fence.Input, input) && ReferenceEquals(fence.Result, _planningResult) &&
        ReferenceEquals(fence.Result.Plan, candidate) && _inputFingerprint == fence.InputFingerprint &&
        authority.MatchesInput(input) && PreparedFingerprintAgrees(fence.Result);
    private static IReadOnlyList<ValidationIssue> JointCacheIssue(string actual) =>
        [MortalWoundOpportunityAdapter.PrivateSourceIssue(WoundAcceptedTurnPlannerCore.NewIssue(
            "accepted_mechanics_joint_cache_fence_invalid", actual, "the exact privately issued current joint candidate", "refused"))];
}
