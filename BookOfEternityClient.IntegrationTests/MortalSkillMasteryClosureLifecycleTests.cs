using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalSkillMasteryClosureLifecycleTests : IDisposable
{
    private const string ActivePath = "game_state/player/skills_active.json";
    private const string MasteryPath = "game_state/player/skill_mastery.json";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-skill-closure-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemManager _fs;
    private readonly StateManager _state;
    private readonly ValidationService _validator;

    public MortalSkillMasteryClosureLifecycleTests()
    {
        _fs = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        _state = PortableSaveFixture.Seed(_fs);
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        File.WriteAllText(_fs.ResolvePath(MortalItemIdentityState.StatePath), MortalItemIdentityState.CreateEmptyRoot().ToJsonString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcceptedRefresh_RemovedOrRenamedSkillRejectsSnapshotOnlyMasteryAndCompensates(bool rename)
    {
        var before = Active("Fireball");
        var backups = await SnapshotAsync(before);
        var raw = rename ? Active("Iceball") : new JsonObject { ["activeSkillChanges"] = new JsonArray(), ["removeActiveSkills"] = new JsonArray("Fireball") };
        await _fs.WriteFileAtomicAsync(ActivePath, raw.ToJsonString());
        await _fs.WriteFileAtomicAsync(MasteryPath, Mastery("Fireball"));
        var snapshotBefore = await _fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json");
        var masteryBefore = await _fs.ReadFileAsync(MasteryPath);
        var issues = await RefreshAsync(backups);
        var issue = Assert.Single(issues);
        Assert.Equal("skill_mastery_unknown_active_skill", issue.Code);
        Assert.Equal(MasteryPath + ".skillMasteryChanges[0].skillName", issue.FilePath);
        Assert.Equal(raw.ToJsonString(), await _fs.ReadFileAsync(ActivePath));
        Assert.Equal(masteryBefore, await _fs.ReadFileAsync(MasteryPath));
        Assert.Equal(snapshotBefore, await _fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcceptedRefresh_RetainedMasteryOnlyOrSameTurnSkillPasses(bool newSkill)
    {
        var backups = await SnapshotAsync(newSkill ? Active() : Active("Fireball"));
        await _fs.WriteFileAtomicAsync(ActivePath, (newSkill ? Active("Fireball") : Active()).ToJsonString());
        await _fs.WriteFileAtomicAsync(MasteryPath, Mastery("Fireball"));
        Assert.Empty(await _validator.ValidateGameStateAsync(new GameStateValidationSelection(
            GameStateValidationPhase.PlayerStateFiles, new[] { ActivePath, MasteryPath })));
        Assert.Empty(await RefreshAsync(backups));
        var active = JsonNode.Parse((await _fs.ReadFileAsync(ActivePath))!)!;
        Assert.Equal("Fireball", active["activeSkillChanges"]![0]!["skillName"]!.GetValue<string>());
        Assert.Equal(Mastery("Fireball"), await _fs.ReadFileAsync(MasteryPath));
    }

    [Theory]
    [InlineData("RemovedSpell", "skill_mastery_unknown_active_skill")]
    [InlineData("fireball", "skill_mastery_confusable_reference")]
    public async Task Save_RejectsInvalidCurrentMasteryWithoutCreatingArchiveOrMutatingState(string reference, string code)
    {
        await SnapshotAsync(Active(reference)); // A historical name must not grant save admission.
        await _fs.WriteFileAtomicAsync(ActivePath, Active("Fireball").ToJsonString());
        await _fs.WriteFileAtomicAsync(MasteryPath, Mastery(reference));
        var before = await _fs.ReadFileAsync(MasteryPath);
        var generation = File.ReadAllBytes(_fs.SessionGenerationPath);
        var logger = new PortableSaveFixture.CaptureLogger();
        var service = new SaveLoadService(_fs, _state, logger);
        Assert.False(await service.SaveGameAsync("invalid-skill", "must reject"));
        Assert.Contains(logger.Errors, error => error.ToString().Contains(code, StringComparison.Ordinal));
        Assert.Empty(Directory.GetFiles(_fs.ResolvePath("saves/manual_saves"), "*.zip"));
        Assert.Equal(before, await _fs.ReadFileAsync(MasteryPath));
        Assert.Equal(generation, File.ReadAllBytes(_fs.SessionGenerationPath));
    }

    [Theory]
    [InlineData("RemovedSpell", "skill_mastery_unknown_active_skill")]
    [InlineData("fireball", "skill_mastery_confusable_reference")]
    public async Task Load_RejectsResealedInvalidMasteryBeforeReplacingLiveState(string reference, string code)
    {
        var service = new SaveLoadService(_fs, _state, NullLogger<SaveLoadService>.Instance);
        await WriteValidPairAsync();
        Assert.True(await service.SaveGameAsync("incoming-skill", "valid original archive"));
        var archive = Assert.Single(Directory.GetFiles(_fs.ResolvePath("saves/manual_saves"), "*.zip"));
        RewriteAndReseal(archive, MasteryPath, Mastery(reference));
        var archiveBefore = File.ReadAllBytes(archive);
        await _fs.WriteFileAtomicAsync(MasteryPath, "{\"skillMasteryChanges\":[]}");
        var liveBefore = await _fs.ReadFileAsync(MasteryPath);
        var generationBefore = File.ReadAllBytes(_fs.SessionGenerationPath);
        var result = await service.LoadGameWithOutcomeAsync(archive);
        Assert.Equal(LoadReplacementDisposition.NotLoaded, result.Disposition);
        Assert.Contains(code, result.Failure!.ToString(), StringComparison.Ordinal);
        Assert.Equal(liveBefore, await _fs.ReadFileAsync(MasteryPath));
        Assert.Equal(generationBefore, File.ReadAllBytes(_fs.SessionGenerationPath));
        Assert.Equal(archiveBefore, File.ReadAllBytes(archive));
    }

    [Fact]
    public async Task SaveLoad_ExactCurrentPairRoundTripsWithoutSnapshot()
    {
        var service = new SaveLoadService(_fs, _state, NullLogger<SaveLoadService>.Instance);
        await WriteValidPairAsync();
        Assert.True(await service.SaveGameAsync("valid-skill", "exact current pair"));
        var archive = Assert.Single(Directory.GetFiles(_fs.ResolvePath("saves/manual_saves"), "*.zip"));
        await _fs.WriteFileAtomicAsync(MasteryPath, "{\"skillMasteryChanges\":[]}");
        Assert.True(await service.LoadGameAsync(archive));
        Assert.Equal(Mastery("Fireball"), await _fs.ReadFileAsync(MasteryPath));
    }

    private Task<IReadOnlyList<ValidationIssue>> RefreshAsync(IReadOnlyDictionary<string, string> backups) =>
        AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateAsync(_fs,
            new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance), _validator, backups);

    private async Task WriteValidPairAsync()
    {
        await _fs.WriteFileAtomicAsync(ActivePath, Active("Fireball").ToJsonString());
        await _fs.WriteFileAtomicAsync(MasteryPath, Mastery("Fireball"));
    }

    private async Task<IReadOnlyDictionary<string, string>> SnapshotAsync(JsonObject before)
    {
        var backup = "game_state/control/pending_turn_snapshot/" + ActivePath;
        var json = before.ToJsonString();
        await _fs.WriteFileAtomicAsync(backup, json);
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """{"sessionId":"closure-session","requestId":"closure-request","turnNumber":1}""");
        var manifest = new JsonObject
        {
            ["sessionId"] = "closure-session", ["requestId"] = "closure-request", ["turnNumber"] = 1,
            ["requestTimestamp"] = "2026-11-11T00:00:00Z", ["playerAction"] = "use or forget a skill",
            ["files"] = new JsonObject { [ActivePath] = backup },
            ["snapshotFileHashes"] = new JsonObject { [ActivePath] = PendingTurnSnapshotAuthority.ComputeSha256(json) },
            ["clientOwnedValidationHashes"] = new JsonObject(), ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = new JsonArray(ActivePath), ["sourceLabel"] = "T103 closure test", ["manifestPayloadHash"] = ""
        };
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
        await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.json", manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
        return new Dictionary<string, string> { [ActivePath] = backup };
    }

    private static JsonObject Active(params string[] names) => new()
    {
        ["activeSkillChanges"] = new JsonArray(names.Select(name => (JsonNode)new JsonObject
        {
            ["skillId"] = "spell_1", ["skillName"] = name, ["skillDescription"] = "Spell", ["rarity"] = "Rare",
            ["scalingCharacteristic"] = "Intelligence", ["combatEffect"] = new JsonObject
            {
                ["actionName"] = name, ["actionCost"] = "Main", ["isActivatedEffect"] = true,
                ["effects"] = new JsonArray(new JsonObject { ["effectType"] = "Damage", ["targetType"] = "Enemy", ["effectDescription"] = "Hit", ["value"] = "45%", ["poiseDamage"] = "5%" })
            }
        }).ToArray())
    };

    private static string Mastery(string name) => new JsonObject
    {
        ["skillMasteryChanges"] = new JsonArray(new JsonObject { ["skillName"] = name, ["newMasteryLevel"] = 3,
            ["newCurrentMasteryProgress"] = 7, ["newMasteryProgressNeeded"] = 100, ["masteryLeveledUp"] = false })
    }.ToJsonString();

    private static void RewriteAndReseal(string path, string entryPath, string json)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Update);
        JsonObject manifest;
        using (var reader = new StreamReader(archive.GetEntry("save_manifest.json")!.Open()))
            manifest = JsonNode.Parse(reader.ReadToEnd())!.AsObject();
        var bytes = Encoding.UTF8.GetBytes(json);
        archive.GetEntry(entryPath)!.Delete();
        using (var stream = archive.CreateEntry(entryPath).Open()) stream.Write(bytes);
        var entry = manifest["entries"]!.AsArray().Single(item => item!["path"]!.GetValue<string>() == entryPath)!;
        entry["length"] = bytes.Length;
        entry["sha256"] = Convert.ToHexString(SHA256.HashData(bytes));
        archive.GetEntry("save_manifest.json")!.Delete();
        using var writer = new StreamWriter(archive.CreateEntry("save_manifest.json").Open(), new UTF8Encoding(false));
        writer.Write(manifest.ToJsonString());
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
