using System.Text.Json;

namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    // Pure policy seam for supported Windows root/member spellings; caller wiring
    // is the next test-first correction, not part of this throwing scaffold.
    internal static string GetLocalRelativePath(string root, string path, bool windows) =>
        throw new NotImplementedException();

    private async Task<string> PublishSessionReplacementAsync(CanonicalWriteLease lease, bool clearGameState)
    {
        EnsureValidSessionReplacementLease(lease);
        VerifyCurrentSessionOperation(lease);
        if (lease.MutationIntentRecorder != null || lease.IsLegacyStorageRecovery)
            throw new InvalidOperationException("Common session replacement cannot run inside a legacy transaction.");
        EnsureNoLegacyStorageEvidence();

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
            var relative = Path.GetRelativePath(GameSessionPath, path);
            var bytes = await ReadLocalFileBytesAsync(lease, relative)
                ?? throw new InvalidDataException("A selected session file disappeared during replacement preparation.");
            changes.Add(new(path, bytes, null));
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

    private static IReadOnlyList<string> EnumerateLocalTreeFiles(TrustedLocalFileScope scope, string root,
        Func<string, bool>? exclude = null, bool jsonOnly = false)
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
                    directories.Add(scope.ValidateDirectory(entry, allowMissing: false));
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
        return files;
    }
}
