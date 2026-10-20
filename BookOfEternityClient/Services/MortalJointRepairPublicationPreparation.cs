using System.Security.Cryptography;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

// A lease-local, privately constructed complete rollback inventory. This is not
// a plan take, publication receipt, durable completion proof or release token.
internal sealed class MortalJointRepairPublicationPreparation
{
    private readonly FileSystemManager _files;
    private readonly FileSystemManager.CanonicalWriteLease _lease;
    private readonly IReadOnlyDictionary<string, CanonicalBeforeImage> _before;
    private readonly string[] _memberPaths;

    private MortalJointRepairPublicationPreparation(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, AcceptedMechanicsPlan plan,
        AcceptedMechanicsPlanBinding binding, string generation,
        IReadOnlyDictionary<string, CanonicalBeforeImage> before, string[] memberPaths)
    {
        _files = files; _lease = lease; Plan = plan; Binding = binding;
        Generation = generation;
        _before = AcceptedMechanicsPlanBinding.CloneReadOnlyBeforeImages(before);
        _memberPaths = memberPaths.ToArray();
        PreparedPlanFingerprint = plan.PreparedPlanFingerprint;
        InventoryFingerprint = ComputeInventoryFingerprint();
    }

    internal AcceptedMechanicsPlan Plan { get; }
    internal AcceptedMechanicsPlanBinding Binding { get; }
    internal string Generation { get; }
    internal string PreparedPlanFingerprint { get; }
    internal string InventoryFingerprint { get; }
    internal WoundAcceptedTurnPlanner.MortalJointRepairCommonAuthority Authority =>
        Plan.JointRepairCommonAuthority ?? throw new InvalidDataException("Joint publication lost its private owner.");
    internal IReadOnlyDictionary<string, CanonicalBeforeImage> BeforeImages =>
        AcceptedMechanicsPlanBinding.CloneReadOnlyBeforeImages(_before);
    internal IReadOnlyList<string> MemberPaths => Array.AsReadOnly(_memberPaths.ToArray());

    internal static async Task<MortalJointRepairPublicationPreparation> CreateAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        AcceptedMechanicsPlan plan, AcceptedMechanicsPlanBinding binding)
    {
        files.EnsureCanonicalWriteLeaseActive(lease);
        var authority = plan.JointRepairCommonAuthority;
        if (authority is null || !authority.HasValidSeal || plan.AwaitsPendingResolution ||
            plan.TreatmentResourcePublicationAuthority is not null || plan.DirectWoundPublicationAuthority is not null ||
            plan.LiveWoundProofFingerprint is not null || plan.OwnerTransitions.Count != 0 ||
            plan.WoundAnchorPlanFingerprint != authority.Anchors.Fingerprint ||
            !authority.AgreesWith(plan.WoundStageBundle, authority.Anchors, plan.OwnerAuthority))
            throw new InvalidDataException("Joint publication requires its complete exclusive sealed candidate.");
        await authority.ValidateCurrentAsync(files, lease);
        var finalIssues = MortalWoundTreatmentCapabilityAuthority.CandidateAdmissionGate.ValidateJoint(authority, plan);
        if (finalIssues.Count != 0) throw Refused(finalIssues);
        var bounds = await authority.ValidateCandidateResourceBoundsAsync(files, lease, plan);
        if (bounds.Count != 0) throw Refused(bounds);

        // This is the precise possible write/delete cohort, including the future
        // v2 marker. No dynamic caller path or independent normalization writer
        // can expand it after the owning cache slots have been taken.
        var members = MortalQteDecisionPublication.QuartetPaths
            .Append(EffectAcceptedTurnPlan.IdentityIndexPath)
            .Concat(plan.OwnerCompanionAfterImages.Keys)
            .Concat(plan.EffectCarrierAfterImages.Keys)
            .Concat(CanonicalStateNormalizer.ComposeWoundPublicationWrites(plan).Keys)
            .Concat(plan.PendingAfterImages.Keys).Concat(plan.ConsumedPaths)
            .Append(MortalQteDecisionPublication.StatePath)
            .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        var before = plan.BeforeImages;
        foreach (var path in members)
            if (!plan.TouchedPaths.Contains(path, StringComparer.Ordinal) || !before.ContainsKey(path))
                throw new InvalidDataException("Joint publication lacks sealed complete member coverage: " + path);
        foreach (var path in new[] { AcceptedMechanicsPlan.WoundCommandPath,
                     WoundAcceptedTurnSnapshotContract.PendingResolutionPath, MortalWoundOpportunityAdapter.DecisionPath })
            if (!plan.ConsumedPaths.Contains(path, StringComparer.Ordinal) ||
                !before.TryGetValue(path, out var image) || !image.Existed)
                throw new InvalidDataException("Joint publication must consume the exact whole owned graph: " + path);
        var prepared = new MortalJointRepairPublicationPreparation(files, lease, plan, binding,
            files.ReadLocalGenerationSnapshot(lease).Binding.Id ?? throw new InvalidDataException("Joint generation is absent."),
            before, members);
        await prepared.ValidateInventoryCurrentAsync(files, lease);
        await authority.ValidateCurrentAsync(files, lease);
        return prepared;
    }

    internal bool AgreesWith(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        AcceptedMechanicsPlan plan, AcceptedMechanicsPlanBinding binding) =>
        ReferenceEquals(files, _files) && ReferenceEquals(lease, _lease) && ReferenceEquals(plan, Plan) &&
        ReferenceEquals(plan.JointRepairCommonAuthority, Authority) && Authority.HasValidSeal &&
        PreparedPlanFingerprint == plan.PreparedPlanFingerprint &&
        AcceptedMechanicsPlanFingerprints.ComputePrepared(plan) == PreparedPlanFingerprint &&
        AcceptedMechanicsPlanFingerprints.ComputeInput(binding) == AcceptedMechanicsPlanFingerprints.ComputeInput(Binding) &&
        ComputeInventoryFingerprint() == InventoryFingerprint;

    // Also usable after exact compensation, while the taken item slot is vacant.
    // It grants no rearm; the registry must separately prove both cache fences and
    // the same still-confirmed agreement. All IO remains outside its monitors.
    internal async Task ValidateInventoryCurrentAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
    {
        using var innerTiming1 = files.MeasureOperation("JointInventory.ValidateCurrentAsync");
        using var innerTiming2 = files.MeasureOperation("JointInventory.ValidateOwnerAndSeal");
        files.EnsureCanonicalWriteLeaseActive(lease);
        if (!AgreesWith(files, lease, Plan, Binding) || !files.IsCurrentSessionGeneration(lease, Generation))
            throw new InvalidDataException("Joint transaction inventory belongs to a stale or foreign lease/owner.");
        innerTiming2?.Dispose();
        using var innerTiming3 = files.MeasureOperation("JointInventory.ReadAndCheckSnapshot");
        var original = MortalQteWoundOpportunityProjection.ReadSnapshot(files, lease);
        if (original.SessionId != Binding.SessionId || original.RequestId != Binding.RequestId ||
            original.SnapshotToken != Binding.SnapshotToken || original.TurnNumber != Binding.Turn || original.Realm != Binding.Realm)
            throw new InvalidDataException("The actual original no longer matches the joint transaction inventory.");
        innerTiming3?.Dispose();
        using var innerTiming4 = files.MeasureOperation("JointInventory.CompareBeforeImages");
        foreach (var (path, expected) in _before.OrderBy(row => row.Key, StringComparer.Ordinal))
        {
            var current = await files.ReadFileBytesAsync(lease, path);
            if (expected.Existed != (current is not null) || !Same(expected.Bytes, current))
                throw new InvalidDataException("Joint transaction before-image changed: " + path);
        }
        innerTiming4?.Dispose();
    }

    internal void RequireCompletePreparedMembers(IReadOnlyList<CanonicalLocalFileChange> changes)
    {
        if (changes.Count != _memberPaths.Length ||
            !changes.Select(row => row.RelativePath).ToHashSet(StringComparer.Ordinal).SetEquals(_memberPaths))
            throw new InvalidDataException("Joint transaction cannot publish a partial, duplicate or expanded member cohort.");
        foreach (var change in changes)
            if (!_before.TryGetValue(change.RelativePath, out var before) || !Same(before.Bytes, change.Before))
                throw new InvalidDataException("Joint transaction member before-image changed: " + change.RelativePath);
    }

    private string ComputeInventoryFingerprint()
    {
        var fields = new List<string?> { "book_of_eternity.mortal_joint_repair.transaction_inventory.v1", Generation,
            PreparedPlanFingerprint, AcceptedMechanicsPlanFingerprints.ComputeInput(Binding) };
        foreach (var path in _memberPaths) fields.Add(path);
        foreach (var (path, image) in _before.OrderBy(row => row.Key, StringComparer.Ordinal))
        { fields.Add(path); fields.Add(image.Bytes is { } bytes ? Convert.ToHexString(SHA256.HashData(bytes)) : null); }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }
    private static bool Same(byte[]? left, byte[]? right) => left is null ? right is null :
        right is not null && left.AsSpan().SequenceEqual(right);
    private static InvalidDataException Refused(IReadOnlyList<ValidationIssue> issues) =>
        new(string.Join("; ", issues.Select(row => row.Code + ": " + row.FilePath)));
}
