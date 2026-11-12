using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Current NPC ownership through the real state-file phase, without a game engine.</summary>
public sealed class MortalNpcSkillMasteryOwnershipPhaseTests : IDisposable
{
    private const string CorePath = "game_state/npcs/npc_core.json";
    private const string SkillsPath = "game_state/npcs/npc_skills.json";
    private const string Code = "npc_skill_mastery_unknown_owned_skill";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-npc-ownership-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public MortalNpcSkillMasteryOwnershipPhaseTests()
    {
        _fs = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Theory]
    [InlineData(false, "foreign")]
    [InlineData(true, "foreign")]
    [InlineData(false, "orphan")]
    [InlineData(true, "orphan")]
    [InlineData(false, "wrong-kind")]
    [InlineData(true, "wrong-kind")]
    public async Task CurrentState_RejectsUnownedMasteryOnEveryValidation(bool passive, string mode)
    {
        var skills = Mastery(passive);
        if (mode != "orphan")
            AddSkill(skills, mode == "wrong-kind" ? !passive : passive,
                mode == "foreign" ? "npc_a" : "npc_b");
        var text = skills.ToJsonString();
        await _fs.WriteFileAtomicAsync(SkillsPath, text);
        for (var repeat = 0; repeat < 2; repeat++)
        {
            var issues = await _validator.ValidateGameStateAsync(GameStateValidationPhase.NpcStateFiles);
            var issue = Assert.Single(issues, item => item.Code == Code);
            Assert.Equal("id:npc_b", issue.Actor);
            Assert.Equal(SkillsPath + "." + MasterySection(passive) + "[0].skillName", issue.FilePath);
        }
        Assert.Equal(text, await _fs.ReadFileAsync(SkillsPath));
    }

    [Theory]
    [InlineData(false, "UpdateNPCs")]
    [InlineData(true, "UpdateNPCs")]
    [InlineData(false, "NPCsInScene")]
    [InlineData(true, "NPCsInScene")]
    public async Task SkillsOnlySelection_UsesCurrentCoreWithoutEmittingUnselectedFileShapeFindings(bool passive, string carrier)
    {
        var core = Core(passive, carrier);
        core["unselectedInvalidMember"] = true;
        await WriteAsync(CorePath, core);
        await WriteAsync(SkillsPath, Mastery(passive));
        var issues = await _validator.ValidateGameStateAsync(new GameStateValidationSelection(
            GameStateValidationPhase.NpcStateFiles, new[] { SkillsPath }));
        Assert.Empty(issues);
        // A different operation must observe replacement, not retain the old name index.
        await WriteAsync(CorePath, new JsonObject());
        issues = await _validator.ValidateGameStateAsync(new GameStateValidationSelection(
            GameStateValidationPhase.NpcStateFiles, new[] { SkillsPath }));
        Assert.Single(issues, item => item.Code == Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SameTurnSkillAndMastery_PreserveInitialization(bool passive)
    {
        var skills = Mastery(passive);
        AddSkill(skills, passive, "npc_b");
        await WriteAsync(SkillsPath, skills);
        var issues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.NpcStateFiles | GameStateValidationPhase.SkillContractConsistency);
        Assert.DoesNotContain(issues, i => i.Code == Code || i.Code == "npc_skill_missing_mastery_initialization");
        skills.Remove(MasterySection(passive));
        await WriteAsync(SkillsPath, skills);
        issues = await _validator.ValidateGameStateAsync(GameStateValidationPhase.SkillContractConsistency);
        Assert.Contains(issues, i => i.Code == "npc_skill_missing_mastery_initialization");
    }

    [Fact]
    public async Task MalformedSelectedCore_KeepsFileFailureAndCannotGrantOwnership()
    {
        await _fs.WriteFileAtomicAsync(CorePath, "[");
        await WriteAsync(SkillsPath, Mastery(false));
        var issues = await _validator.ValidateGameStateAsync(GameStateValidationPhase.NpcStateFiles);
        Assert.Contains(issues, i => i.Code == "npc_contract_invalid_json" && i.FilePath == CorePath);
        Assert.Single(issues, i => i.Code == Code);
    }

    [Fact]
    public async Task ExcludedSkills_DoNotEmitOwnershipFindings()
    {
        await WriteAsync(SkillsPath, Mastery(false));
        var issues = await _validator.ValidateGameStateAsync(new GameStateValidationSelection(
            GameStateValidationPhase.NpcStateFiles, new[] { CorePath }));
        Assert.DoesNotContain(issues, i => i.Code == Code);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ValidPendingSnapshot_CannotGrantSnapshotOnlyOwnership(bool passive, bool retained)
    {
        var core = Core(passive, "UpdateNPCs");
        var skillRoot = Mastery(passive);
        AddSkill(skillRoot, passive, "npc_b");
        var files = new JsonObject();
        var hashes = new JsonObject();
        foreach (var entry in new[] { (CorePath, core), (SkillsPath, skillRoot) })
        {
            var path = "game_state/control/pending_turn_snapshot/" + entry.Item1;
            var json = entry.Item2.ToJsonString();
            await _fs.WriteFileAtomicAsync(path, json);
            files[entry.Item1] = path;
            hashes[entry.Item1] = PendingTurnSnapshotAuthority.ComputeSha256(json);
        }
        await WriteAsync("input/turn_request.json", new JsonObject
        { ["sessionId"] = "ownership-session", ["requestId"] = "ownership-request", ["turnNumber"] = 1 });
        var manifest = new JsonObject
        {
            ["sessionId"] = "ownership-session", ["requestId"] = "ownership-request", ["turnNumber"] = 1,
            ["requestTimestamp"] = "2026-11-11T00:00:00Z", ["playerAction"] = "train",
            ["files"] = files, ["snapshotFileHashes"] = hashes,
            ["clientOwnedValidationHashes"] = new JsonObject(), ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = new JsonArray(CorePath, SkillsPath), ["sourceLabel"] = "NPC skill ownership test",
            ["manifestPayloadHash"] = ""
        };
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
        await WriteAsync("game_state/control/pending_turn_snapshot.json", manifest);
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
        await WriteAsync(CorePath, retained ? core : new JsonObject());
        await WriteAsync(SkillsPath, Mastery(passive));
        var issues = await _validator.ValidateGameStateAsync(GameStateValidationPhase.NpcStateFiles);
        Assert.DoesNotContain(issues, i => i.Code != null && i.Code.Contains("snapshot", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(retained ? 0 : 1, issues.Count(i => i.Code == Code));
    }

    private Task WriteAsync(string path, JsonObject value) => _fs.WriteFileAtomicAsync(path, value.ToJsonString());
    private static string MasterySection(bool passive) => passive ? "NPCPassiveSkillMasteryChanges" : "NPCSkillMasteryChanges";
    private static JsonObject Mastery(bool passive) => new()
    {
        [MasterySection(passive)] = new JsonArray(new JsonObject
        {
            ["NPCId"] = "npc_b", ["skillName"] = "Whirlwind", ["newMasteryLevel"] = 1,
            ["newCurrentMasteryProgress"] = 0, ["newMasteryProgressNeeded"] = 100
        })
    };
    private static void AddSkill(JsonObject root, bool passive, string owner) =>
        root[passive ? "NPCPassiveSkillChanges" : "NPCActiveSkillChanges"] = new JsonArray(new JsonObject
        { ["NPCId"] = owner, ["skillChanges"] = new JsonArray(Skill(passive)) });
    private static JsonObject Core(bool passive, string carrier) => new()
    {
        [carrier] = new JsonArray(new JsonObject
        { ["NPCId"] = "npc_b", [passive ? "passiveSkills" : "activeSkills"] = new JsonArray(Skill(passive)) })
    };
    private static JsonObject Skill(bool passive) => passive
        ? new JsonObject { ["skillName"] = "Whirlwind", ["skillDescription"] = "Skill", ["rarity"] = "Common",
            ["type"] = "KnowledgeBased", ["group"] = "Archive", ["masteryLevel"] = 1, ["maxMasteryLevel"] = 5, ["structuredBonuses"] = null }
        : new JsonObject { ["skillName"] = "Whirlwind", ["skillDescription"] = "Skill", ["rarity"] = "Rare",
            ["scalingCharacteristic"] = "Intelligence", ["combatEffect"] = new JsonObject
            { ["actionName"] = "Whirlwind", ["actionCost"] = "Main", ["isActivatedEffect"] = true,
                ["effects"] = new JsonArray(new JsonObject { ["effectType"] = "Damage", ["targetType"] = "Enemy",
                    ["effectDescription"] = "Hit", ["value"] = "45%", ["poiseDamage"] = "5%" }) } };
    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
