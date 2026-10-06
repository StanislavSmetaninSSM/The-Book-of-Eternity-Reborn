using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

// Created only by the original browser handler under its live canonical lease.
// JSON describes recovery evidence; it never supplies a live main/worker pin.
internal sealed class BrowserLocalStorageAccess(FileSystemManager files,
    FileSystemManager.CanonicalWriteLease lease, string root, string generation) : IDisposable
{
    internal string Root { get; } = root;
    internal string Generation { get; } = generation;
    internal Func<string, byte[]?, Task>? RecordIntent { get; set; }
    internal void Validate()
    {
        files.EnsureCanonicalWriteLeaseActive(lease);
        files.VerifyCurrentSessionOperation(lease);
        if (!ReferenceEquals(lease.BrowserLocalAccess, this) ||
            !string.Equals(files.ReadExistingSessionGeneration(lease), Generation, StringComparison.Ordinal))
            throw new InvalidDataException("Original browser storage scope no longer owns its generation.");
        lease.EnsureNoPendingLocalDecision();
    }
    internal bool OwnsArtifact(string path) => path.StartsWith(Root + "/", StringComparison.Ordinal);
    public void Dispose()
    {
        RecordIntent = null;
        if (ReferenceEquals(lease.BrowserLocalAccess, this)) lease.BrowserLocalAccess = null;
    }
}

public partial class FileSystemManager
{
    internal BrowserLocalStorageAccess BeginBrowserLocalStorage(CanonicalWriteLease lease, string root, string generation)
    {
        EnsureCanonicalWriteLeaseActive(lease);
        VerifyCurrentSessionOperation(lease);
        if (!OperatingSystem.IsLinux() || lease.BrowserLocalAccess != null || lease.MutationIntentRecorder != null ||
            lease.IsLegacyStorageRecovery || !root.StartsWith(ExplorerLocalTurnRollbackArtifacts.Root + "/", StringComparison.Ordinal) ||
            !string.Equals(ReadExistingSessionGeneration(lease), generation, StringComparison.Ordinal))
            throw new InvalidOperationException("Browser storage requires its original supported lease and generation.");
        var access = new BrowserLocalStorageAccess(this, lease, root, generation);
        lease.BrowserLocalAccess = access;
        access.Validate();
        return access;
    }
}
