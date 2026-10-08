using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    private async Task DeleteTrustedLocalDirectoryTreeAsync(CanonicalWriteLease lease, string relativePath)
    {
        VerifyCurrentSessionOperation(lease);
        EnsureSafeCanonicalRelativePath(relativePath);
        EnsureNoLegacyStorageEvidence(lease);
        // A previous call can commit and retain cleanup debt on this same lease.
        // Resolve it before even empty-tree pruning can remove its scratch parents.
        RecoverTrustedLocalStorage(lease);
        var scope = new TrustedLocalFileScope([GameSessionPath]);
        var fullPath = ResolvePath(relativePath);
        var generation = ReadLocalGenerationSnapshot(lease);
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var selected = EnumerateLocalTreeFiles(scope, fullPath).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        var changes = new List<TrustedLocalFileChange>(selected.Length + 1);
        foreach (var path in selected)
        {
            var relative = GetLocalRelativePath(GameSessionPath, path, OperatingSystem.IsWindows());
            var bytes = await ReadLocalFileBytesAsync(lease, relative)
                ?? throw new InvalidDataException("A selected directory member disappeared before deletion.");
            changes.Add(new(ResolvePath(relative), bytes, null));
        }

        void RevalidateTree()
        {
            VerifyCurrentSessionOperation(lease);
            EnsureCanonicalMutationBoundary(relativePath, fullPath);
            if (!selected.ToHashSet(comparer).SetEquals(EnumerateLocalTreeFiles(scope, fullPath)))
                throw new InvalidDataException("The directory member set changed before deletion.");
            if (ReadLocalGenerationSnapshot(lease).Binding != generation.Binding)
                throw new InvalidDataException("The session generation changed before directory deletion.");
        }

        await InvokeBeforeCanonicalMutationBoundaryAsync(relativePath);
        EnsureCanonicalMutationBoundary(relativePath, fullPath);
        scope.ValidateDirectory(fullPath);
        await InvokeAfterCanonicalMutationBoundaryValidatedAsync(relativePath);
        RevalidateTree();
        if (changes.Count == 0)
        {
            // Structure has no byte decision to journal, and must not invent a generation.
            TryRemoveEmptyCanonicalDirectory(lease, relativePath);
            return;
        }

        // A fresh root establishes its generation in this same complete decision.
        if (!generation.Binding.Exists)
            changes.Add(GenerationCreation(Guid.NewGuid().ToString("N")));
        var outcome = await PublishLocalCoreAsync(lease, generation.Binding, changes,
            CancellationToken.None, () => { RevalidateTree(); return Task.CompletedTask; });
        if (outcome.Disposition != TrustedLocalPublicationDisposition.Committed)
        {
            RequireCommittedLocalPublication(outcome);
            return;
        }
        if (!generation.Binding.Exists)
            CanonicalRootAuthorityIdentity.AdvanceSessionGenerationRevision();

        // ScratchScope requires the existing member parents, even for absent
        // scratch files. Leave all structure until retained journal cleanup finishes.
        if (outcome.Failure != null)
        {
            LogCommittedDirectoryCleanupFailure(outcome.Failure);
            return;
        }
        try
        {
            // Nonrecursive deletion preserves a new/unknown file instead of
            // treating structural cleanup as a second unjournaled publication.
            if (!TryRemoveEmptyCanonicalDirectory(lease, relativePath))
                LogCommittedDirectoryCleanupFailure(new InvalidDataException("Directory cleanup retained new content."));
        }
        catch (Exception failure) { LogCommittedDirectoryCleanupFailure(failure); }
    }

    private void LogCommittedDirectoryCleanupFailure(Exception failure)
    {
        try { _logger.LogWarning(failure, "Directory member deletion committed; structural or journal cleanup remains pending."); }
        catch { /* Diagnostics cannot reverse the durable byte decision. */ }
    }
}
