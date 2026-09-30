namespace BookOfEternityClient.Core;

internal sealed record TrustedLocalFileChange(string Path, byte[]? Before, byte[]? After);
internal sealed record TrustedLocalGeneration(bool Exists, string? Id)
{
    internal static TrustedLocalGeneration Absent => new(false, null);
    internal static TrustedLocalGeneration Existing(string id) => new(true, id);
}
internal sealed record TrustedLocalPublishedMember(string Path, bool Exists, string? Sha256);
internal sealed record TrustedLocalPublicationResult(string TransactionId, TrustedLocalGeneration Generation,
    IReadOnlyList<TrustedLocalPublishedMember> Members);
internal enum TrustedLocalPublicationPhase
{
    IntentStaged, IntentPublished, MemberStaged, MemberPublished, CommitStaged, Committed,
    RollbackStaged, MemberRestored, CleanupMember, CleanupComplete
}

/// <summary>Exact-byte, generation-bound publication under an existing canonical write lease.</summary>
internal sealed class TrustedLocalFilePublication
{
    internal TrustedLocalFilePublication(FileSystemManager files, TrustedLocalFileScope scope) { }
    internal TrustedLocalPublicationResult Publish(FileSystemManager.CanonicalWriteLease lease,
        TrustedLocalGeneration generation, IReadOnlyList<TrustedLocalFileChange> changes,
        Action<TrustedLocalPublicationPhase, int>? observer = null) => throw new NotImplementedException();
    internal void Recover(FileSystemManager.CanonicalWriteLease lease,
        Action<TrustedLocalPublicationPhase, int>? observer = null) => throw new NotImplementedException();
}
