using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

[Collection(GameEngineTurnLifecycleCollection.CollectionName)]
public sealed class SignedSnapshotExactPathTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("key_backslash")]
    [InlineData("value_backslash")]
    [InlineData("baseline_backslash")]
    [InlineData("key_trim")]
    [InlineData("value_trim")]
    [InlineData("baseline_trim")]
    [InlineData("keys_case")]
    [InlineData("values_case")]
    [InlineData("cross_map_case")]
    [InlineData("baseline_case")]
    [InlineData("rollback_value_case")]
    [InlineData("dot_segment")]
    [InlineData("rooted")]
    [InlineData("blank")]
    [InlineData("exact_duplicate")]
    [InlineData("unicode")]
    public void OriginalDetachedSignerRequiresExactRepresentablePathsBeforeByteReads(string mode)
    {
        const string logical = "lore/entry.json";
        const string snapshot = "game_state/control/pending_turn_snapshot/lore/entry.json";
        var manifest = new LiveTurnPendingSnapshotManifest
        {
            SessionId = "signed-session", RequestId = "signed-request", TurnNumber = 7,
            Files = new Dictionary<string, string>(StringComparer.Ordinal) { [logical] = snapshot },
            SnapshotFileHashes = new Dictionary<string, string>(StringComparer.Ordinal) { [logical] = "AABB" },
            ClientOwnedValidationHashes = new Dictionary<string, string>(StringComparer.Ordinal),
            RollbackBackups = new Dictionary<string, string>(StringComparer.Ordinal),
            RollbackBaselineFiles = [logical], SourceLabel = "signed-exact-source"
        };
        switch (mode)
        {
            case "key_backslash": manifest.Files.Add("lore/odd\\leaf.json", snapshot + ".second"); break;
            case "value_backslash": manifest.Files[logical] = "game_state/control/odd\\leaf.json"; break;
            case "baseline_backslash": manifest.RollbackBaselineFiles.Add("lore/odd\\leaf.json"); break;
            case "key_trim": manifest.Files.Add("lore/entry.json ", snapshot + ".second"); break;
            case "value_trim": manifest.Files[logical] = snapshot + " "; break;
            case "baseline_trim": manifest.RollbackBaselineFiles.Add(" lore/entry.json"); break;
            case "keys_case": manifest.Files.Add("lore/Entry.json", snapshot + ".second"); break;
            case "values_case": manifest.Files.Add("lore/other.json", "game_state/control/pending_turn_snapshot/lore/Entry.json"); break;
            case "cross_map_case": manifest.ClientOwnedValidationHashes.Add("lore/Entry.json", "FF"); break;
            case "baseline_case": manifest.RollbackBaselineFiles.Add("lore/Entry.json"); break;
            case "rollback_value_case": manifest.RollbackBackups.Add("lore/other.json", "game_state/control/pending_turn_snapshot/lore/Entry.json"); break;
            case "dot_segment": manifest.RollbackBaselineFiles.Add("lore/../entry.json"); break;
            case "rooted": manifest.ClientOwnedValidationHashes.Add("/lore/entry.json", "FF"); break;
            case "blank": manifest.Files.Add("", snapshot + ".second"); break;
            case "exact_duplicate": manifest.RollbackBaselineFiles.Add(logical); manifest.ClientOwnedValidationHashes.Add(logical, "FF"); break;
            case "unicode": manifest.Files.Add("lore/ История.json", "game_state/control/pending_turn_snapshot/lore/ История.json"); break;
        }
        var byteReads = 0;
        string? result = null;
        var failure = Record.Exception(() => result = Sign(manifest, _ => { byteReads++; return [2, 3, 5]; }));
        output.WriteLine(JsonSerializer.Serialize(new { mode, failure = failure?.ToString(), byteReads, result }));
        if (mode is "exact_duplicate" or "unicode")
        {
            Assert.Null(failure); Assert.NotNull(result);
            using var envelope = JsonDocument.Parse(result);
            using var payload = JsonDocument.Parse(Convert.FromBase64String(envelope.RootElement.GetProperty("payloadJsonBase64").GetString()!));
            foreach (var (key, value) in manifest.Files)
                Assert.Equal(value, payload.RootElement.GetProperty("files").GetProperty(key).GetString());
        }
        else
        {
            Assert.IsType<InvalidDataException>(failure); Assert.Null(result); Assert.Equal(0, byteReads);
        }
    }

    [Theory]
    [InlineData("case_alias")]
    [InlineData("fixed_alias")]
    [InlineData("literal_backslash")]
    [InlineData("outer_trim")]
    [InlineData("story_alias")]
    [InlineData("unicode")]
    public async Task OriginalLivePreparationRefusesUnrepresentableInventoryBeforeOldEvidenceCleanup(string mode)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(Path.GetTempPath(), "boe-signed-preflight-" + Guid.NewGuid().ToString("N"));
        using var owned = new CleanupOwnedFixture(root, output.WriteLine);
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        files.EnsureDirectoryStructure();
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync()) files.GetOrCreateSessionGeneration(lease);
        await files.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{\"currentRealm\":\"Mortal World\"}");
        var retained = new[] { LiveTurnPreparationService.TurnRequestPath, LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
            PendingTurnSnapshotAuthority.AuthorityPath, LiveTurnPreparationService.PendingTurnSnapshotDirectory + "/old.bin",
            PendingTurnStateService.PendingDiceStatePath };
        foreach (var path in retained)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(files.ResolvePath(path))!);
            File.WriteAllBytes(files.ResolvePath(path), Encoding.UTF8.GetBytes("retained-original-evidence:" + path));
        }
        var names = mode switch
        {
            "case_alias" => new[] { "lore/Entry.json", "lore/entry.json" },
            "fixed_alias" => new[] { "game_state/meta/SOUL_STATE.json" },
            "literal_backslash" => new[] { "lore/odd\\leaf.json" },
            "outer_trim" => new[] { "lore/trailing.json " },
            "story_alias" => new[] { "stories/Chapter.jsonl", "stories/chapter.jsonl" },
            "cleanup_alias" => new[] { "input/Turn_Request.json" },
            _ => new[] { "lore/ История.json" }
        };
        foreach (var path in names)
        {
            var full = Path.Combine(files.GameSessionPath, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("{} ")).ToArray());
        }
        if (mode == "fixed_alias") File.Delete(files.ResolvePath("game_state/meta/soul_state.json"));
        var before = Capture(files.GameSessionPath);
        if (mode == "fixed_alias") Assert.False(before.ContainsKey("game_state/meta/soul_state.json"));
        var generationBefore = File.ReadAllBytes(files.SessionGenerationPath);
        LiveTurnPreparationResult? result = null;
        var failure = await Record.ExceptionAsync(async () => result = await new LiveTurnPreparationService(files).PrepareAsync(
            new LiveTurnPreparationOptions { SessionId = "signed-session", RequestId = "signed-request", TurnNumber = 7,
                CurrentRealm = "Mortal World", PlayerAction = "Просмотреть записи.", PreGeneratedDices1d20 = [7, 11, 13] }));
        var after = Capture(files.GameSessionPath);
        output.WriteLine(JsonSerializer.Serialize(new { mode, root, names, retained, before, after,
            failure = failure?.ToString(), result, generationBefore, generationAfter = File.ReadAllBytes(files.SessionGenerationPath) }));
        Assert.Equal(generationBefore, File.ReadAllBytes(files.SessionGenerationPath));
        if (mode == "unicode")
        {
            Assert.Null(failure); Assert.NotNull(result);
            var manifest = JsonSerializer.Deserialize<LiveTurnPendingSnapshotManifest>(File.ReadAllText(files.ResolvePath(result.ManifestPath)), LiveTurnPreparationService.ManifestJsonOptions)!;
            Assert.Equal(LiveTurnPreparationService.PendingTurnSnapshotDirectory + "/" + names[0], manifest.Files[names[0]]);
            Assert.Equal(before[names[0]], File.ReadAllBytes(files.ResolvePath(manifest.Files[names[0]])));
            Assert.Equal(before[names[0]], after[names[0]]);
            Assert.False(after.ContainsKey(retained[3])); Assert.False(after.ContainsKey(retained[4]));
        }
        else
        {
            Assert.IsType<InvalidDataException>(failure); Assert.Null(result);
            Assert.Equal(before.Keys.OrderBy(k => k, StringComparer.Ordinal), after.Keys.OrderBy(k => k, StringComparer.Ordinal));
            foreach (var (path, bytes) in before) Assert.Equal(bytes, after[path]);
        }
    }

    [Fact]
    public Task OriginalLivePreparationRefusesCleanupPathAliasBeforeOldEvidenceCleanup() =>
        OriginalLivePreparationRefusesUnrepresentableInventoryBeforeOldEvidenceCleanup("cleanup_alias");

    private static Dictionary<string, byte[]> Capture(string root) => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllBytes, StringComparer.Ordinal);

    private static string Sign(LiveTurnPendingSnapshotManifest manifest, Func<string, byte[]?> read) => PendingTurnSnapshotAuthority.CreateDetachedAuthorityJson(
        manifest, LiveTurnPreparationService.ManifestHashJsonOptions, m => m.ManifestPayloadHash, (m, h) => m.ManifestPayloadHash = h,
        m => m.SessionId, m => m.RequestId, m => m.TurnNumber, m => m.Files, m => m.SnapshotFileHashes,
        m => m.ClientOwnedValidationHashes, m => m.RollbackBaselineFiles, m => m.SourceLabel, m => m.RollbackBackups, read);
}
