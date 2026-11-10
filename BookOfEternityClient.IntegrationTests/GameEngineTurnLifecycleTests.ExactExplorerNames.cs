using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("case_alias")]
    [InlineData("literal_backslash")]
    [InlineData("outer_trim")]
    [InlineData("fixed_alias")]
    [InlineData("unicode")]
    public async Task OriginalIncarnationInventoryAdmitsRawNamesBeforeCallerFolding(string mode)
    {
        Assert.True(OperatingSystem.IsLinux());
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var engine = CreateExactExplorerEngine();
        try
        {
            const string oldPath = "lore/current_world/retained.json";
            await _fs.WriteFileAtomicBytesAsync(oldPath, [41]);
            var explorer = GetPrivateField<ExplorerMode>(engine, "_explorer");
            await explorer.StagePendingLocalTurnRollbackSnapshotAsync(oldPath);
            var old = GetPrivateField<ExplorerMode.PendingLocalTurnRollbackSnapshot>(explorer, "_pendingLocalTurnRollbackSnapshot");
            var names = mode == "fixed_alias" ? new[] { WorldDirectiveService.PendingSetupPath.ToUpperInvariant() }
                : ExactExplorerNames(mode, oldPath);
            foreach (var path in names) PutExactEngineFile(path, [0, 255, 42]);
            var before = CaptureExactEngineFiles(); var generation = File.ReadAllBytes(_fs.SessionGenerationPath);
            var ownerBefore = CaptureExactExplorerOwner(old); var mutations = new List<string>();
            _consoleMutationObserver = mutations.Add;
            string[]? result = null;
            var wrapped = Record.Exception(() => result = InvokePrivateValue<string[]>(engine, "EnumerateIncarnationLocalPrepRollbackFiles"));
            var failure = wrapped is TargetInvocationException { InnerException: { } actual } ? actual : wrapped;
            var after = CaptureExactEngineFiles();
            var current = GetPrivateField<ExplorerMode.PendingLocalTurnRollbackSnapshot>(explorer, "_pendingLocalTurnRollbackSnapshot");
            _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { root = _rootPath, mode, names,
                failure = failure?.ToString(), before, after, mutations, ownerBefore, ownerAfter = CaptureExactExplorerOwner(current),
                sameOwner = ReferenceEquals(old, current), generationBefore = generation,
                generationAfter = File.ReadAllBytes(_fs.SessionGenerationPath), result }));
            Assert.Empty(mutations); Assert.Equal(generation, File.ReadAllBytes(_fs.SessionGenerationPath));
            AssertExactEngineTreeUnchanged(before, after); Assert.Same(old, current);
            Assert.Equal(ownerBefore, CaptureExactExplorerOwner(current));
            if (mode == "unicode") { Assert.Null(failure); Assert.Contains(names[0], result!); }
            else { Assert.IsType<InvalidDataException>(failure); Assert.Null(result); }
        }
        finally { _consoleMutationObserver = null; await GetPrivateField<AudioService>(engine, "_audioService").DisposeAsync(); }
    }

    [Theory]
    [InlineData("stage", "case_alias")]
    [InlineData("stage", "literal_backslash")]
    [InlineData("stage", "outer_trim")]
    [InlineData("stage", "retained_alias")]
    [InlineData("stage", "unicode")]
    [InlineData("stage", "exact_repeat")]
    [InlineData("mark", "case_alias")]
    [InlineData("mark", "literal_backslash")]
    [InlineData("mark", "outer_trim")]
    [InlineData("mark", "retained_alias")]
    [InlineData("mark", "unicode")]
    public async Task OriginalExplorerRawInputsPreserveActualCompletedRollbackEvidence(string route, string mode)
    {
        Assert.True(OperatingSystem.IsLinux());
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var engine = CreateExactExplorerEngine();
        try
        {
            const string oldPath = "lore/current_world/retained.json";
            await _fs.WriteFileAtomicBytesAsync(oldPath, [41]);
            var explorer = GetPrivateField<ExplorerMode>(engine, "_explorer");
            await explorer.StagePendingLocalTurnRollbackSnapshotAsync(oldPath);
            var old = explorer.ConsumePendingLocalTurnRollbackSnapshot()!;
            Assert.Equal(new byte[] { 41 }, File.ReadAllBytes(_fs.ResolvePath(old.BackupFiles[oldPath])));
            await _fs.WriteFileAtomicBytesAsync(oldPath, [51]);
            // Original restore reaches durable restoration, then the existing fixture
            // cuts its first cleanup deletion. No fabricated RestoreCompleted flag.
            ArmCanonicalWriteFailure(old.BackupFiles[oldPath]);
            var restorationFailure = await Record.ExceptionAsync(() => explorer.RestoreConsumedLocalTurnRollbackSnapshotAsync(old));
            Assert.IsType<IOException>(restorationFailure); Assert.Null(_armedCanonicalWriteFailurePath);
            Assert.True(old.RestoreCompleted); Assert.Equal(new byte[] { 41 }, File.ReadAllBytes(_fs.ResolvePath(oldPath)));
            Assert.True(File.Exists(_fs.ResolvePath(old.BackupFiles[oldPath])));
            Assert.Same(old, GetPrivateField<ExplorerMode.PendingLocalTurnRollbackSnapshot>(explorer, "_pendingLocalTurnRollbackSnapshot"));
            var names = ExactExplorerNames(mode, oldPath);
            foreach (var path in names.Distinct(StringComparer.Ordinal)) PutExactEngineFile(path, [0, 255, 42]);
            var before = CaptureExactEngineFiles(); var generation = File.ReadAllBytes(_fs.SessionGenerationPath);
            var ownerBefore = CaptureExactExplorerOwner(old); var mutations = new List<string>(); _consoleMutationObserver = mutations.Add;
            var failure = route == "stage"
                ? await Record.ExceptionAsync(() => explorer.StagePendingLocalTurnRollbackSnapshotAsync(names))
                : Record.Exception(() => explorer.MarkExistingPendingLocalTurnValidationSnapshotFiles(names));
            var after = CaptureExactEngineFiles();
            var current = GetPrivateField<ExplorerMode.PendingLocalTurnRollbackSnapshot>(explorer, "_pendingLocalTurnRollbackSnapshot");
            _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { root = _rootPath, route, mode, names,
                restorationFailure = restorationFailure.ToString(), actualRestorationCompleted = old.RestoreCompleted,
                failure = failure?.ToString(), before, after, mutations, ownerBefore, ownerAfter = CaptureExactExplorerOwner(current),
                sameOwner = ReferenceEquals(old, current), generationBefore = generation,
                generationAfter = File.ReadAllBytes(_fs.SessionGenerationPath) }));
            Assert.Equal(generation, File.ReadAllBytes(_fs.SessionGenerationPath));
            if (mode is "unicode" or "exact_repeat")
            {
                Assert.Null(failure);
                if (route == "stage")
                {
                    Assert.NotSame(old, current); Assert.False(current.RestoreCompleted);
                    foreach (var path in names.Distinct(StringComparer.Ordinal))
                        Assert.Equal(before[path], File.ReadAllBytes(_fs.ResolvePath(current.BackupFiles[path])));
                    Assert.False(File.Exists(_fs.ResolvePath(Assert.Single(old.TechnicalArtifacts))));
                }
                else { Assert.Same(old, current); Assert.Contains(names[0], current.ValidationSnapshotFiles); Assert.Empty(mutations); }
                foreach (var path in names.Distinct(StringComparer.Ordinal)) Assert.Equal(before[path], after[path]);
            }
            else
            {
                Assert.IsType<InvalidDataException>(failure); Assert.Empty(mutations);
                Assert.Same(old, current); Assert.Equal(ownerBefore, CaptureExactExplorerOwner(current));
                AssertExactEngineTreeUnchanged(before, after);
            }
        }
        finally { _consoleMutationObserver = null; await GetPrivateField<AudioService>(engine, "_audioService").DisposeAsync(); }
    }

    private GameEngine CreateExactExplorerEngine() => CreateGameEngine(new ProgressionNoInput(),
        settings => { settings.GmBridgeAutoStart = false; settings.MusicEnabled = false; settings.SoundEnabled = false; });

    private static string[] ExactExplorerNames(string mode, string oldPath) => mode switch
    {
        "case_alias" => ["lore/current_world/Entry.json", "lore/current_world/entry.json"],
        "literal_backslash" => ["lore/current_world/odd\\leaf.json"],
        "outer_trim" => ["lore/current_world/trailing.json "],
        "retained_alias" => [oldPath.ToUpperInvariant()],
        "exact_repeat" => ["lore/current_world/ История.json", "lore/current_world/ История.json"],
        _ => ["lore/current_world/ История.json"]
    };

    private static string CaptureExactExplorerOwner(ExplorerMode.PendingLocalTurnRollbackSnapshot snapshot) =>
        JsonSerializer.Serialize(new { snapshot.EvidenceRoot, snapshot.RestoreCompleted,
            Tracked = snapshot.TrackedFiles.OrderBy(x => x, StringComparer.Ordinal),
            Baseline = snapshot.BaselineFiles.OrderBy(x => x, StringComparer.Ordinal),
            Validation = snapshot.ValidationSnapshotFiles.OrderBy(x => x, StringComparer.Ordinal),
            Technical = snapshot.TechnicalArtifacts.OrderBy(x => x, StringComparer.Ordinal),
            Backups = snapshot.BackupFiles.OrderBy(x => x.Key, StringComparer.Ordinal),
            Hashes = snapshot.BackupHashes.OrderBy(x => x.Key, StringComparer.Ordinal) });
}
