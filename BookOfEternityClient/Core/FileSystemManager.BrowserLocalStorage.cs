using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

// Created only by the original browser handler under its live canonical lease.
// JSON describes recovery evidence; it never supplies a live main/worker pin.
internal sealed class BrowserLocalStorageAccess(FileSystemManager files,
    FileSystemManager.CanonicalWriteLease lease, string root, string generation, string? profilePath) : IDisposable
{
    internal string Root { get; } = root;
    internal string Generation { get; } = generation;
    internal string? ProfilePath { get; } = profilePath;
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
    private string RegisteredDarenProfilePath => Path.Combine(BasePath, DarenQteRewardProfileService.ProfileRelativePath);
    internal IReadOnlyList<string> ReadPendingBrowserPublicationScratch(CanonicalWriteLease lease) =>
        new TrustedLocalFilePublication(this, new TrustedLocalFileScope([BasePath])).ReadBrowserRecoveryScratch(lease)
            .Where(path => path.StartsWith(ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Select(path => GetLocalRelativePath(GameSessionPath, path, false)).ToArray();

    // Reads need the active original lease but do not grant an external mutation.
    // Used by actual browser state refresh after its transaction scope has closed.
    internal async Task<byte[]?> ReadLocalDarenProfileAsync(CanonicalWriteLease lease, CancellationToken token = default)
    {
        EnsureCanonicalWriteLeaseActive(lease);
        VerifyCurrentSessionOperation(lease);
        var scope = new TrustedLocalFileScope([], [RegisteredDarenProfilePath]);
        var path = scope.ValidateFile(RegisteredDarenProfilePath);
        if (!File.Exists(path)) return null;
        var bytes = await File.ReadAllBytesAsync(path, token);
        scope.ValidateFile(path, allowMissing: false);
        VerifyCurrentSessionOperation(lease);
        return bytes;
    }
    internal void RequireDeclaredDarenProfile(CanonicalWriteLease lease)
    {
        EnsureWorkerGeneralMutationAllowed(lease);
        var access = lease.BrowserLocalAccess ?? throw new InvalidOperationException("Original declared browser scope is missing.");
        access.Validate();
        if (access.ProfilePath != RegisteredDarenProfilePath)
            throw new InvalidOperationException("Daren profile is not a declared member of the original browser transaction.");
        new TrustedLocalFileScope([], [RegisteredDarenProfilePath]).ValidateFile(RegisteredDarenProfilePath);
    }
    internal async Task PublishLocalDarenProfileAsync(CanonicalWriteLease lease, byte[]? content, CancellationToken token = default)
    {
        RequireDeclaredDarenProfile(lease);
        var before = await ReadLocalDarenProfileAsync(lease, token);
        RequireCommittedLocalPublication(await PublishLocalCoreAsync(lease,
            TrustedLocalGeneration.Existing(lease.BrowserLocalAccess!.Generation),
            [new(RegisteredDarenProfilePath, before, content)], token));
    }

    internal IReadOnlyList<string> ListBrowserStorageEvidence(CanonicalWriteLease lease)
    {
        EnsureCanonicalWriteLeaseActive(lease);
        VerifyCurrentSessionOperation(lease);
        return EnumerateLocalTreeFiles(new TrustedLocalFileScope([GameSessionPath]), ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root))
            .Select(path => GetLocalRelativePath(GameSessionPath, path, false)).ToArray();
    }

    internal byte[]? ReadBrowserStorageEvidence(CanonicalWriteLease lease, string relativePath)
    {
        EnsureCanonicalWriteLeaseActive(lease);
        VerifyCurrentSessionOperation(lease);
        var scope = new TrustedLocalFileScope([GameSessionPath]);
        var path = scope.ValidateFile(ResolvePath(relativePath));
        if (!File.Exists(path)) return null;
        var bytes = File.ReadAllBytes(path);
        scope.ValidateFile(path, allowMissing: false);
        VerifyCurrentSessionOperation(lease);
        return bytes;
    }

    internal BrowserLocalStorageAccess BeginBrowserLocalStorage(CanonicalWriteLease lease, string root, string generation, bool includeDaren = false)
    {
        EnsureCanonicalWriteLeaseActive(lease);
        VerifyCurrentSessionOperation(lease);
        if (!OperatingSystem.IsLinux() || lease.BrowserLocalAccess != null || lease.MutationIntentRecorder != null ||
            lease.IsLegacyStorageRecovery || !root.StartsWith(ExplorerLocalTurnRollbackArtifacts.Root + "/", StringComparison.Ordinal) ||
            !string.Equals(ReadExistingSessionGeneration(lease), generation, StringComparison.Ordinal))
            throw new InvalidOperationException("Browser storage requires its original supported lease and generation.");
        var access = new BrowserLocalStorageAccess(this, lease, root, generation, includeDaren ? RegisteredDarenProfilePath : null);
        lease.BrowserLocalAccess = access;
        access.Validate();
        return access;
    }
}
