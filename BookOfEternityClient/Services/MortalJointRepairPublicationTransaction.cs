using System.Security.Cryptography;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed class MortalJointRepairPublicationTransaction
{
    private static readonly object ReadbackIssuer = new();
    private readonly FileSystemManager _files;
    private readonly FileSystemManager.CanonicalWriteLease _lease;
    private CanonicalLocalFileChange[]? _members;
    private ReadbackProof? _readback;
    private int _committedBatch;
    private int _compensationStarted;
    private MortalJointRepairPublicationTransaction(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        MortalJointRepairPublicationTakeReceipt receipt) { _files = files; _lease = lease; Receipt = receipt; }
    internal MortalJointRepairPublicationTakeReceipt Receipt { get; }

    internal static MortalJointRepairPublicationTransaction Create(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, MortalJointRepairPublicationTakeReceipt receipt)
    {
        files.EnsureCanonicalWriteLeaseActive(lease);
        var probe = AcceptedTurnAuthorityRegistry.ProbeTakenJointRepairPublication(files, lease, receipt);
        if (!probe.IsValid) throw new InvalidDataException("Joint transaction requires its exact current private take receipt.");
        return new(files, lease, receipt);
    }

    internal void BindIntendedMembers(object issuer, IReadOnlyList<CanonicalLocalFileChange> members)
    {
        if (!CanonicalStateNormalizer.IsJointBatchPublicationIssuer(issuer) || Receipt.IsConsumed)
            throw new InvalidOperationException("Only the taken common publisher may freeze joint intended after-images.");
        Receipt.Preparation.RequireCompletePreparedMembers(members);
        var frozen = members.Select(row => new CanonicalLocalFileChange(row.RelativePath, row.Before?.ToArray(), row.After?.ToArray())).ToArray();
        if (Interlocked.CompareExchange(ref _members, frozen, null) is not null)
            throw new InvalidOperationException("Joint intended members are immutable and can be installed only once.");
    }

    internal void ObserveCommittedBatch(object issuer)
    {
        if (!CanonicalStateNormalizer.IsJointBatchPublicationIssuer(issuer) || Volatile.Read(ref _members) is null)
            throw new InvalidOperationException("Joint commit observation requires the owning batch publisher.");
        Volatile.Write(ref _committedBatch, 1);
    }

    internal sealed class ReadbackProof
    {
        private readonly MortalJointRepairPublicationTransaction _transaction;
        private readonly string _fingerprint;
        internal ReadbackProof(object issuer, MortalJointRepairPublicationTransaction transaction)
        {
            if (!ReferenceEquals(issuer, ReadbackIssuer)) throw new InvalidOperationException("Joint readback proof requires its private publisher.");
            _transaction = transaction; _fingerprint = transaction.ComputeMemberFingerprint();
        }
        internal bool AgreesWith(MortalJointRepairPublicationTakeReceipt receipt) =>
            ReferenceEquals(_transaction.Receipt, receipt) && ReferenceEquals(_transaction._readback, this) &&
            Volatile.Read(ref _transaction._committedBatch) == 1 && Volatile.Read(ref _transaction._compensationStarted) == 0 &&
            _fingerprint == _transaction.ComputeMemberFingerprint() && !receipt.IsConsumed;
    }

    internal async Task<ReadbackProof> ValidatePublishedReadbackAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
    {
        EnsureOwner(files, lease);
        var members = Volatile.Read(ref _members) ?? throw new InvalidOperationException("Joint intended members were not frozen.");
        if (Volatile.Read(ref _committedBatch) != 1 || Volatile.Read(ref _compensationStarted) != 0 || Receipt.IsConsumed)
            throw new InvalidDataException("Joint readback requires one known committed, unsettled cohort.");
        var byPath = members.ToDictionary(row => row.RelativePath, StringComparer.Ordinal);
        foreach (var (path, before) in Receipt.Preparation.BeforeImages)
        {
            var expected = byPath.TryGetValue(path, out var changed) ? changed.After : before.Bytes;
            if (!Same(await files.ReadFileBytesAsync(lease, path), expected))
                throw new InvalidDataException("Joint complete readback differs at " + path);
        }
        await MortalQteDecisionPublication.ValidatePublishedJointAsync(files, lease, Receipt);
        var probe = AcceptedTurnAuthorityRegistry.ProbeTakenJointRepairPublication(files, lease, Receipt);
        if (!probe.IsValid) throw new InvalidDataException("Joint claim/cache changed before final readback sealing: " + string.Join(";", probe.Issues.Select(row => row.Code)));
        var proof = new ReadbackProof(ReadbackIssuer, this);
        _readback = proof;
        return proof;
    }

    internal async Task<MortalJointRepairPublicationSettlement> CompensateAndRearmAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease)
    {
        try
        {
            EnsureOwner(files, lease);
            if (Receipt.IsConsumed || Interlocked.CompareExchange(ref _compensationStarted, 1, 0) != 0)
                return Uncertain("A closed or already compensating joint transaction cannot restore an accepted cohort.");
            _readback = null;
            var members = Volatile.Read(ref _members) ?? Array.Empty<CanonicalLocalFileChange>();
            var byPath = members.ToDictionary(row => row.RelativePath, StringComparer.Ordinal);
            var restore = new List<CanonicalLocalFileChange>();
            foreach (var (path, before) in Receipt.Preparation.BeforeImages)
            {
                var actual = await files.ReadFileBytesAsync(lease, path);
                if (byPath.TryGetValue(path, out var member))
                {
                    if (!Same(actual, member.Before) && !Same(actual, member.After))
                        throw new InvalidDataException("Joint compensation refuses unknown member bytes: " + path);
                    restore.Add(new(path, actual, member.Before));
                }
                else if (!Same(actual, before.Bytes))
                    throw new InvalidDataException("Joint compensation refuses changed retained authority: " + path);
            }
            if (restore.Count != 0) files.RequireCommittedLocalPublication(await files.PublishLocalFilesAsync(lease, restore));
            await Receipt.Preparation.ValidateInventoryCurrentAsync(files, lease);
            return await AcceptedTurnAuthorityRegistry.RearmTakenJointRepairPublicationAsync(files, lease, Receipt);
        }
        catch (Exception error) when (error is CoordinatedStatePublicationUncertainException or InvalidDataException or IOException or
            InvalidOperationException or ArgumentException or System.Text.Json.JsonException)
        { return Uncertain(error.Message); }
    }

    private void EnsureOwner(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
    {
        files.EnsureCanonicalWriteLeaseActive(lease);
        if (!ReferenceEquals(files, _files) || !ReferenceEquals(lease, _lease) ||
            !files.IsCurrentSessionGeneration(lease, Receipt.Generation)) throw new InvalidDataException("Joint transaction owner/issuing lease/G changed.");
    }
    private string ComputeMemberFingerprint()
    {
        var fields = new List<string?> { "book_of_eternity.mortal_joint_repair.published_member_readback.v1", Receipt.Fingerprint };
        foreach (var row in (Volatile.Read(ref _members) ?? Array.Empty<CanonicalLocalFileChange>()).OrderBy(row => row.RelativePath, StringComparer.Ordinal))
        { fields.Add(row.RelativePath); fields.Add(Hash(row.Before)); fields.Add(Hash(row.After)); }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }
    private static string? Hash(byte[]? bytes) => bytes is null ? null : Convert.ToHexString(SHA256.HashData(bytes));
    private static bool Same(byte[]? left, byte[]? right) => left is null ? right is null : right is not null && left.AsSpan().SequenceEqual(right);
    private static MortalJointRepairPublicationSettlement Uncertain(string reason) => new(MortalJointRepairPublicationDisposition.PublicationUncertain,
        [MortalWoundOpportunityAdapter.PrivateSourceIssue(WoundAcceptedTurnPlannerCore.NewIssue(
            "mortal_joint_repair_publication_uncertain", reason, "exact known whole-cohort compensation/settlement", "blocked"))]);
}
