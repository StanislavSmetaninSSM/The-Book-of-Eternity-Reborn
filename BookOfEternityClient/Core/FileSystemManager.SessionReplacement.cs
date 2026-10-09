using System.Text.Json;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    // Paths have already passed common scope validation. Normalize both sides
    // before deriving a canonical relative name, including extended Windows roots.
    internal static string GetLocalRelativePath(string root, string path, bool windows)
    {
        var separator = windows ? '\\' : Path.DirectorySeparatorChar;
        root = TrustedLocalFilePublication.NormalizeAuthorityPath(root, windows).TrimEnd(separator);
        path = TrustedLocalFilePublication.NormalizeAuthorityPath(path, windows);
        var comparison = windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(root, path.TrimEnd(separator), comparison)) return string.Empty;
        var prefix = root + separator;
        if (!path.StartsWith(prefix, comparison))
            throw new InvalidDataException("The local member is outside its declared root.");
        var relative = path[prefix.Length..];
        return windows ? relative.Replace('\\', '/') : relative;
    }

    private async Task<string> PublishSessionReplacementAsync(CanonicalWriteLease lease, bool clearGameState)
    {
        EnsureValidSessionReplacementLease(lease);
        VerifyCurrentSessionOperation(lease);
        if (lease.MutationIntentRecorder != null || lease.IsLegacyStorageRecovery)
            throw new InvalidOperationException("Common session replacement cannot run inside a legacy transaction.");
        // Clear consumes a current pending turn only after its original signed
        // authority validates every retained before-image. Rotation alone cannot.
        EnsureNoLegacyStorageEvidence(currentPendingClear: clearGameState ? lease : null);

        var scope = new TrustedLocalFileScope([BasePath]);
        var generation = ReadLocalGenerationSnapshot(lease);
        var selected = new HashSet<string>(StringComparer.Ordinal);
        void AddTree(string relative, Func<string, bool>? exclude = null, bool jsonOnly = false)
        {
            foreach (var file in EnumerateLocalTreeFiles(scope, Path.Combine(GameSessionPath, relative), exclude, jsonOnly))
                selected.Add(file);
        }
        if (clearGameState)
        {
            scope.ValidateFile(Path.Combine(GameSessionPath,
                LocalUiSessionLockService.LockPath.Replace('/', Path.DirectorySeparatorChar)));
            // Directory creation can leave harmless structure on failure, but a
            // wrong-type required path must fail before publishing any file.
            foreach (var directory in RequiredDirectories)
                scope.ValidateDirectory(Path.Combine(BasePath, directory.Replace('/', Path.DirectorySeparatorChar)));
            AddTree("input", jsonOnly: true);
            AddTree("output", jsonOnly: true);
            AddTree("ready", jsonOnly: true);
            var gameState = Path.Combine(GameSessionPath, "game_state");
            AddTree("game_state", entry => ShouldPreserveAcrossGameStateClear(gameState, entry));
            AddTree("lore");
            AddTree("stories");
        }
        AddTree("worker_tasks");
        AddTree("worker_proposals");
        if (!clearGameState)
        {
            foreach (var relative in new[] { "game_state/control/gm_worker_latest_validation_repair_task.json",
                         "game_state/control/validation_repair_ready.json" })
            {
                var path = scope.ValidateFile(Path.Combine(GameSessionPath, relative.Replace('/', Path.DirectorySeparatorChar)));
                if (File.Exists(path)) selected.Add(path);
            }
        }

        // Capture every before image before the single generation/member decision.
        var changes = new List<TrustedLocalFileChange>();
        foreach (var path in selected.OrderBy(path => path, StringComparer.Ordinal))
        {
            var relative = GetLocalRelativePath(GameSessionPath, path, OperatingSystem.IsWindows());
            var bytes = await ReadLocalFileBytesAsync(lease, relative)
                ?? throw new InvalidDataException("A selected session file disappeared during replacement preparation.");
            // Keep the manager's root spelling at its existing mutation/lease boundaries.
            changes.Add(new(ResolvePath(relative), bytes, null));
        }
        var replacement = Guid.NewGuid().ToString("N");
        changes.Add(new(SessionGenerationPath, generation.Bytes,
            JsonSerializer.SerializeToUtf8Bytes(new SessionGenerationDocument(1, replacement))));
        if (clearGameState)
            foreach (var directory in RequiredDirectories)
                scope.EnsureDirectory(Path.Combine(BasePath, directory.Replace('/', Path.DirectorySeparatorChar)));

        var outcome = await PublishLocalCoreAsync(lease, generation.Binding, changes, CancellationToken.None);
        RequireCommittedLocalPublication(outcome);
        CanonicalRootAuthorityIdentity.AdvanceSessionGenerationRevision();
        return replacement;
    }

    internal bool TryRemoveEmptyCanonicalDirectory(CanonicalWriteLease lease, string relativePath, bool includeEmptyDescendants = true)
    {
        EnsureWorkerGeneralMutationAllowed(lease);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        VerifyCurrentSessionOperation(lease);
        lease.EnsureNoPendingLocalDecision();
        if (!UsesTrustedLocalWriter(lease, relativePath))
            throw new InvalidOperationException("Empty structure normalization cannot alter a legacy transaction namespace.");
        var scope = new TrustedLocalFileScope([GameSessionPath]);
        var path = scope.ValidateDirectory(ResolvePath(relativePath));
        if (!includeEmptyDescendants)
        {
            if (!Directory.Exists(path)) return true;
            if (Directory.EnumerateFileSystemEntries(path).Any()) return false;
            Directory.Delete(scope.ValidateDirectory(path, allowMissing: false), recursive: false);
            return true;
        }
        var directories = new List<string>();
        if (EnumerateLocalTreeFiles(scope, path, inspectedDirectories: directories).Count != 0)
            return false;
        // Only validated empty structure is removed. Nonrecursive deletion also
        // retains a file arriving after preflight instead of silently deleting it.
        for (var index = directories.Count - 1; index >= 0; index--)
            Directory.Delete(scope.ValidateDirectory(directories[index], allowMissing: false), recursive: false);
        return true;
    }

    // Fixed caller-selected subtree only: no path is discovered from diagnostic metadata.
    internal IReadOnlyList<string> EnumerateCanonicalLocalTreeFiles(CanonicalWriteLease lease, string relativeRoot,
        bool recursive = true)
    {
        EnsureCanonicalWriteLeaseActive(lease);
        VerifyCurrentSessionOperation(lease);
        EnsureSafeCanonicalRelativePath(relativeRoot);
        var paths = EnumerateLocalTreeFiles(new TrustedLocalFileScope([GameSessionPath]), ResolvePath(relativeRoot), recursive: recursive)
            .Select(path => GetLocalRelativePath(GameSessionPath, path, OperatingSystem.IsWindows())).ToArray();
        VerifyCurrentSessionOperation(lease);
        return paths;
    }

    private static IReadOnlyList<string> EnumerateLocalTreeFiles(TrustedLocalFileScope scope, string root,
        Func<string, bool>? exclude = null, bool jsonOnly = false, List<string>? inspectedDirectories = null,
        bool recursive = true)
    {
        root = scope.ValidateDirectory(root);
        if (!Directory.Exists(root)) return [];
        var directories = new List<string> { root };
        var files = new List<string>();
        for (var index = 0; index < directories.Count; index++)
        {
            var directory = scope.ValidateDirectory(directories[index], allowMissing: false);
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                // Opaque preserved boundaries are never probed or traversed.
                if (exclude?.Invoke(entry) == true) continue;
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if (recursive) directories.Add(scope.ValidateDirectory(entry, allowMissing: false));
                }
                else
                {
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                        scope.ValidateFile(entry, allowMissing: false);
                    if (!jsonOnly || entry.EndsWith(".json", OperatingSystem.IsWindows()
                            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                        files.Add(scope.ValidateFile(entry, allowMissing: false));
                }
            }
        }
        inspectedDirectories?.AddRange(directories);
        return files;
    }
}
