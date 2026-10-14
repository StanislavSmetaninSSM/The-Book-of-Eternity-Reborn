using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public Task GuaranteedTreatment_NonzeroAppliedNewRootFreshProcessAuthenticatesAndCompletesOnce() =>
        RunJointCompletionControlAsync(nonzeroProgression: true, pauseAppliedForCold: true, newPhysicalRootForCold: true);

    private sealed record JointNewRootTransportEvidence(string SourceRoot, string TargetRoot,
        string GenerationSha256, string[] SourceRuntimeDirectories, string[] SourceRuntimeFiles,
        string[] TransportedDirectories, IReadOnlyDictionary<string, string> TransportedFileHashes);

    private static async Task<JointNewRootTransportEvidence> CopyQuiescentJointNamespaceAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, string targetRoot, IReadOnlyDictionary<string, byte[]?> paused)
    {
        Assert.True(OperatingSystem.IsLinux());
        files.EnsureCanonicalWriteLeaseActive(lease);
        lease.EnsureNoPendingLocalDecision(); // Dispose clears this; proof must precede release.
        Assert.Null(lease.MutationIntentRecorder); Assert.Null(lease.ExternalPublicationContext);
        Assert.Null(lease.BrowserLocalAccess); Assert.Null(lease.WorkerPurpose); Assert.False(lease.IsLegacyStorageRecovery);
        Assert.False(lease.MainAdmission?.WasRemote ?? false); Assert.False(lease.MainAdmission?.OwnsRemote ?? false);
        files.RequireAbsentMainWorkerInventory();
        Assert.False(Directory.Exists(targetRoot)); Assert.False(File.Exists(targetRoot));
        Assert.NotEqual(Path.GetFullPath(files.BasePath), Path.GetFullPath(targetRoot));

        var runtime = ReadQuiescentJointRuntimeNames(files);
        var source = await CaptureJointDurableNamespaceAsync(files.BasePath);
        Assert.Equal(paused["$sessionGeneration"], source.Files[".boe_runtime/session-generation/current.json"]);
        Assert.Equal(paused.Keys.Where(path => path != "$sessionGeneration").OrderBy(path => path, StringComparer.Ordinal),
            source.Files.Keys.Where(path => path.StartsWith("game_session/", StringComparison.Ordinal))
                .Select(path => path["game_session/".Length..]).OrderBy(path => path, StringComparer.Ordinal));
        foreach (var pair in paused.Where(pair => pair.Key != "$sessionGeneration"))
            Assert.Equal(pair.Value, source.Files["game_session/" + pair.Key]);

        Directory.CreateDirectory(targetRoot);
        var targetScope = new TrustedLocalFileScope([targetRoot]);
        foreach (var relative in source.Directories)
            targetScope.EnsureDirectory(Path.Combine(targetRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        foreach (var pair in source.Files)
            await File.WriteAllBytesAsync(targetScope.ValidateFile(Path.Combine(targetRoot,
                pair.Key.Replace('/', Path.DirectorySeparatorChar))), pair.Value);

        var target = await CaptureJointDurableNamespaceAsync(targetRoot);
        AssertJointDurableNamespaceEqual(source, target);
        AssertJointDurableNamespaceEqual(source, await CaptureJointDurableNamespaceAsync(files.BasePath));
        var runtimeAfter = ReadQuiescentJointRuntimeNames(files);
        Assert.Equal(runtime.Directories, runtimeAfter.Directories); Assert.Equal(runtime.Files, runtimeAfter.Files);
        var targetRuntime = EnumerateJointOrdinaryNames(Path.Combine(targetRoot, ".boe_runtime"));
        Assert.Equal(new[] { "", "session-generation" }, targetRuntime.Directories);
        Assert.Equal(new[] { "session-generation/current.json" }, targetRuntime.Files); // No copied coordination locks/debt.
        lease.EnsureNoPendingLocalDecision();
        return new(files.BasePath, targetRoot, MortalArchiveHash(source.Files[".boe_runtime/session-generation/current.json"]),
            runtime.Directories, runtime.Files, source.Directories,
            source.Files.ToDictionary(pair => pair.Key, pair => MortalArchiveHash(pair.Value), StringComparer.Ordinal));
    }

    private static (string[] Directories, string[] Files) ReadQuiescentJointRuntimeNames(FileSystemManager files)
    {
        var observed = EnumerateJointOrdinaryNames(files.RuntimeRootPath);
        var emptyDebtRoots = new[] { "trusted-local-publication-v1", "file-publication-transactions", "load-transactions", "worker-apply-transactions" };
        var allowedDirectories = new HashSet<string>(emptyDebtRoots.Concat(new[] { "", "locks", "session-generation" }), StringComparer.Ordinal);
        var allowedFiles = new HashSet<string>(StringComparer.Ordinal)
        {
            "session-generation/current.json", "locks/canonical-write.lock", "locks/session-lifecycle.lock", "locks/gm-main-owner.lock"
        };
        Assert.All(observed.Directories, path => Assert.Contains(path, allowedDirectories));
        Assert.All(observed.Files, path => Assert.Contains(path, allowedFiles));
        Assert.Contains("session-generation/current.json", observed.Files);
        // No child entry under a debt root, gm-runs or worker-runs namespace is accepted.
        // Names/ordinary types only: active inert lock streams are never read or copied.
        return observed;
    }

    private sealed record JointDurableNamespace(string[] Directories, Dictionary<string, byte[]> Files);

    private static async Task<JointDurableNamespace> CaptureJointDurableNamespaceAsync(string root)
    {
        var session = EnumerateJointOrdinaryNames(Path.Combine(root, "game_session"));
        var scope = new TrustedLocalFileScope([root]);
        var images = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var relative in session.Files.Select(path => "game_session/" + path)
                     .Append(".boe_runtime/session-generation/current.json").OrderBy(path => path, StringComparer.Ordinal))
            images.Add(relative, await File.ReadAllBytesAsync(scope.ValidateFile(Path.Combine(root,
                relative.Replace('/', Path.DirectorySeparatorChar)), allowMissing: false)));
        var directories = session.Directories.Select(path => path.Length == 0 ? "game_session" : "game_session/" + path)
            .Concat(new[] { ".boe_runtime", ".boe_runtime/session-generation" }).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        return new(directories, images);
    }

    private static (string[] Directories, string[] Files) EnumerateJointOrdinaryNames(string root)
    {
        var scope = new TrustedLocalFileScope([root]);
        var directories = new List<string> { "" }; var files = new List<string>();
        var pending = new Queue<string>(); pending.Enqueue(scope.ValidateDirectory(root, allowMissing: false));
        while (pending.TryDequeue(out var directory))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory).OrderBy(path => path, StringComparer.Ordinal))
            {
                if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("A joint durable fixture member is a reparse point: " + entry);
                var relative = Path.GetRelativePath(root, entry).Replace('\\', '/');
                if (Directory.Exists(entry))
                {
                    directories.Add(relative); pending.Enqueue(scope.ValidateDirectory(entry, allowMissing: false));
                }
                else { scope.ValidateFile(entry, allowMissing: false); files.Add(relative); }
            }
        }
        return (directories.OrderBy(path => path, StringComparer.Ordinal).ToArray(), files.OrderBy(path => path, StringComparer.Ordinal).ToArray());
    }

    private static void AssertJointDurableNamespaceEqual(JointDurableNamespace expected, JointDurableNamespace actual)
    {
        Assert.Equal(expected.Directories, actual.Directories);
        Assert.Equal(expected.Files.Keys.OrderBy(path => path, StringComparer.Ordinal), actual.Files.Keys.OrderBy(path => path, StringComparer.Ordinal));
        foreach (var pair in expected.Files) Assert.Equal(pair.Value, actual.Files[pair.Key]);
    }

    private static async Task<object> CaptureJointNewRootDiagnosticAsync(string root)
    {
        try
        {
            var current = await CaptureJointDurableNamespaceAsync(root);
            return new { Directories = current.Directories,
                Files = current.Files.ToDictionary(pair => pair.Key, pair => MortalArchiveHash(pair.Value), StringComparer.Ordinal) };
        }
        catch (Exception error) { return new { DiagnosticFailure = error.ToString() }; }
    }
}
