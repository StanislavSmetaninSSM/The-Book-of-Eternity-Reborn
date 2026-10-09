using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("baseline", "case_alias")]
    [InlineData("baseline", "fixed_alias")]
    [InlineData("baseline", "output_alias")]
    [InlineData("baseline", "literal_backslash")]
    [InlineData("baseline", "outer_trim")]
    [InlineData("baseline", "story_alias")]
    [InlineData("baseline", "cleanup_alias")]
    [InlineData("baseline", "unicode")]
    [InlineData("backup", "case_alias")]
    [InlineData("backup", "fixed_alias")]
    [InlineData("backup", "output_alias")]
    [InlineData("backup", "literal_backslash")]
    [InlineData("backup", "outer_trim")]
    [InlineData("backup", "story_alias")]
    [InlineData("backup", "cleanup_alias")]
    [InlineData("backup", "unicode")]
    public async Task OriginalEngineSnapshotProducerRefusesRawNamesBeforeChangingEvidence(string route, string mode)
    {
        Assert.True(OperatingSystem.IsLinux());
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{\"currentRealm\":\"Chaos Sea\"}");
        var engine = CreateGameEngine(new ProgressionNoInput(),
            settings => { settings.GmBridgeAutoStart = false; settings.MusicEnabled = false; settings.SoundEnabled = false; });
        try
        {
            foreach (var path in ExactEngineRetainedPaths)
                PutExactEngineFile(path, Encoding.UTF8.GetBytes("retained-engine-evidence:" + path));
            var names = mode switch
            {
                "case_alias" => new[] { "lore/Entry.json", "lore/entry.json" },
                "fixed_alias" => new[] { "game_state/meta/SOUL_STATE.json" },
                "output_alias" => new[] { "output/" + Path.GetFileName(QteSceneService.QteOfferPath).ToUpperInvariant() },
                "literal_backslash" => new[] { "lore/odd\\leaf.json" },
                "outer_trim" => new[] { "lore/trailing.json " },
                "story_alias" => new[] { "stories/Chapter.jsonl", "stories/chapter.jsonl" },
                "cleanup_alias" => new[] { "game_state/control/Terminal_Protocol_Failure_Request.json" },
                _ => new[] { "lore/ История.json" }
            };
            foreach (var path in names)
                PutExactEngineFile(path, Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("{} ")).ToArray());
            if (mode == "fixed_alias") File.Delete(_fs.ResolvePath("game_state/meta/soul_state.json"));
            var before = CaptureExactEngineFiles();
            var generation = File.ReadAllBytes(_fs.SessionGenerationPath);
            object? result = null;
            var failure = await Record.ExceptionAsync(async () =>
            {
                if (route == "backup") result = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "exact-engine-names");
                else
                {
                    var request = CreateSnapshotByteContractRequest("exact-engine-names");
                    request.ProgressionControl!.CurrentRealm = "Chaos Sea";
                    result = await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, null, "exact-engine-names");
                }
            });
            var after = CaptureExactEngineFiles();
            _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { route, mode, root = _rootPath, names,
                failure = failure?.ToString(), before, after, generationBefore = generation,
                generationAfter = File.ReadAllBytes(_fs.SessionGenerationPath), resultReturned = result != null }));
            Assert.Equal(generation, File.ReadAllBytes(_fs.SessionGenerationPath));
            if (mode == "unicode")
            {
                Assert.Null(failure); Assert.NotNull(result);
                var map = route == "baseline" ? Assert.IsType<Dictionary<string, string>>(result)
                    : Assert.IsType<Dictionary<string, string>>(result.GetType().GetProperty("BackupFiles")!.GetValue(result));
                Assert.True(map.ContainsKey(names[0]));
                Assert.Equal(before[names[0]], File.ReadAllBytes(_fs.ResolvePath(map[names[0]])));
                Assert.Equal(before[names[0]], after[names[0]]);
            }
            else
            {
                Assert.IsType<InvalidDataException>(failure); Assert.Null(result);
                AssertExactEngineTreeUnchanged(before, after);
            }
        }
        finally { GetPrivateField<AudioService>(engine, "_audioService").Dispose(); }
    }

    [Theory]
    [InlineData("literal_backslash")]
    [InlineData("case_alias")]
    [InlineData("outer_trim")]
    [InlineData("unicode")]
    public async Task OriginalEngineCleanupValidatesRawPreservedPathsBeforeDeletingEvidence(string mode)
    {
        Assert.True(OperatingSystem.IsLinux());
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var engine = CreateGameEngine(new ProgressionNoInput(),
            settings => { settings.GmBridgeAutoStart = false; settings.MusicEnabled = false; settings.SoundEnabled = false; });
        try
        {
            foreach (var path in ExactEngineRetainedPaths)
                PutExactEngineFile(path, Encoding.UTF8.GetBytes("retained-cleanup-evidence:" + path));
            var preserved = mode switch
            {
                "literal_backslash" => new[] { "lore/odd\\leaf.json.rollback.original" },
                "case_alias" => new[] { "lore/Entry.json.rollback.original", "lore/entry.json.rollback.original" },
                "outer_trim" => new[] { "lore/trailing.json.rollback.original " },
                _ => new[] { "lore/ История.json.rollback.original" }
            };
            foreach (var path in preserved) PutExactEngineFile(path, Encoding.Unicode.GetBytes("exact original rollback"));
            var before = CaptureExactEngineFiles(); var generation = File.ReadAllBytes(_fs.SessionGenerationPath);
            var failure = await Record.ExceptionAsync(() => InvokePrivateTaskAsync(engine, "CleanupPendingTurnSnapshotAsync", new object?[] { preserved }));
            var after = CaptureExactEngineFiles();
            _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { mode, root = _rootPath, preserved,
                failure = failure?.ToString(), before, after, generationBefore = generation,
                generationAfter = File.ReadAllBytes(_fs.SessionGenerationPath) }));
            Assert.Equal(generation, File.ReadAllBytes(_fs.SessionGenerationPath));
            if (mode == "unicode")
            {
                Assert.Null(failure); Assert.Equal(before[preserved[0]], after[preserved[0]]);
                Assert.False(after.ContainsKey(LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
                Assert.False(after.ContainsKey(PendingTurnSnapshotAuthority.AuthorityPath));
                Assert.False(after.ContainsKey(LiveTurnPreparationService.PendingTurnSnapshotDirectory + "/old.bin"));
            }
            else { Assert.IsType<InvalidDataException>(failure); AssertExactEngineTreeUnchanged(before, after); }
        }
        finally { GetPrivateField<AudioService>(engine, "_audioService").Dispose(); }
    }

    [Theory]
    [InlineData("backup_value")]
    [InlineData("validation_trim")]
    public async Task OriginalEngineBaselineValidatesSuppliedRollbackBeforeChangingEvidence(string mode)
    {
        Assert.True(OperatingSystem.IsLinux());
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        const string tracked = "lore/codex_entries.json";
        await _fs.WriteFileAtomicAsync(tracked, "{\"entries\":[]}");
        var engine = CreateGameEngine(new ProgressionNoInput(),
            settings => { settings.GmBridgeAutoStart = false; settings.MusicEnabled = false; settings.SoundEnabled = false; });
        try
        {
            // Obtain the real original producer's object and existing exact backups.
            // Disclose corruption of one caller-supplied path, without fabricating a
            // publication result, signed authority or replacement capture owner.
            var rollback = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "supplied-exact-engine");
            var backups = Assert.IsType<Dictionary<string, string>>(rollback.GetType().GetProperty("BackupFiles")!.GetValue(rollback));
            var actualBackup = backups[tracked];
            Assert.Equal(File.ReadAllBytes(_fs.ResolvePath(tracked)), File.ReadAllBytes(_fs.ResolvePath(actualBackup)));
            var suppliedPath = mode == "backup_value" ? "lore/odd\\leaf.json.rollback.supplied" : " " + tracked;
            if (mode == "backup_value") backups[tracked] = suppliedPath;
            else Assert.IsType<HashSet<string>>(rollback.GetType().GetProperty("ValidationSnapshotFiles")!.GetValue(rollback)).Add(suppliedPath);
            foreach (var path in ExactEngineRetainedPaths)
                PutExactEngineFile(path, Encoding.UTF8.GetBytes("retained-supplied-rollback-evidence:" + path));
            var before = CaptureExactEngineFiles(); var generation = File.ReadAllBytes(_fs.SessionGenerationPath);
            object? result = null;
            var failure = await Record.ExceptionAsync(async () => result = await InvokePrivateTaskResultAsync(engine,
                "CreateCanonicalBaselineSnapshotAsync", CreateSnapshotByteContractRequest("supplied-exact-engine"), rollback, "supplied-exact-engine"));
            var after = CaptureExactEngineFiles();
            _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { mode, root = _rootPath, actualBackup, suppliedPath,
                failure = failure?.ToString(), before, after, generationBefore = generation,
                generationAfter = File.ReadAllBytes(_fs.SessionGenerationPath), resultReturned = result != null }));
            Assert.Equal(generation, File.ReadAllBytes(_fs.SessionGenerationPath));
            Assert.IsType<InvalidDataException>(failure); Assert.Null(result); AssertExactEngineTreeUnchanged(before, after);
        }
        finally { GetPrivateField<AudioService>(engine, "_audioService").Dispose(); }
    }

    private static readonly string[] ExactEngineRetainedPaths =
    [
        "game_state/control/terminal_protocol_failure_request.json",
        LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath,
        LiveTurnPreparationService.PendingTurnSnapshotDirectory + "/old.bin"
    ];

    private void PutExactEngineFile(string path, byte[] bytes)
    {
        var full = Path.Combine(_fs.GameSessionPath, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!); File.WriteAllBytes(full, bytes);
    }

    private Dictionary<string, byte[]> CaptureExactEngineFiles() =>
        Directory.EnumerateFiles(_fs.GameSessionPath, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(_fs.GameSessionPath, path), File.ReadAllBytes, StringComparer.Ordinal);

    private static void AssertExactEngineTreeUnchanged(Dictionary<string, byte[]> before, Dictionary<string, byte[]> after)
    {
        Assert.Equal(before.Keys.OrderBy(path => path, StringComparer.Ordinal), after.Keys.OrderBy(path => path, StringComparer.Ordinal));
        foreach (var (path, bytes) in before) Assert.Equal(bytes, after[path]);
    }
}
