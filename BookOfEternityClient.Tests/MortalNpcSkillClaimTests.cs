using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Independent small filesystem cases for declared NPC claims, not NLP completeness.</summary>
public sealed class MortalNpcSkillClaimTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "npc-claim-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;
    public MortalNpcSkillClaimTests()
    {
        _fs = new(_root, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new(_fs, NullLogger<ValidationService>.Instance);
    }
    public void Dispose() => Directory.Delete(_root, true);
    private const string Quote = "Мирон применил Искру и открыл дверь.";
    private static JsonObject Claim(string resolution = "stateDelta") => new()
    {
        ["actorKey"] = "id:npc_a", ["skillName"] = "Искра", ["skillKind"] = "active",
        ["claimType"] = "use", ["narrativeQuote"] = Quote, ["resolution"] = resolution,
        ["reason"] = resolution == "noProgress" ? "Обычное повторение без нового опыта." : null
    };
    private static string Core(string name = "Искра") => new JsonObject
    {
        ["UpdateNPCs"] = new JsonArray(new JsonObject { ["NPCId"] = "npc_a",
            ["activeSkills"] = new JsonArray(new JsonObject { ["skillName"] = name,
                ["masteryLevel"] = 1, ["currentMasteryProgress"] = 0, ["masteryProgressNeeded"] = 100 }) })
    }.ToJsonString();
    private static string Skills(int progress, string owner = "npc_a") => new JsonObject
    {
        ["NPCSkillMasteryChanges"] = new JsonArray(new JsonObject { ["NPCId"] = owner,
            ["skillName"] = "Искра", ["newMasteryLevel"] = 1, ["newCurrentMasteryProgress"] = progress,
            ["newMasteryProgressNeeded"] = 100 })
    }.ToJsonString();
    private async Task Setup(JsonNode? report, bool include = true, string realm = "Mortal World")
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", new JsonObject { ["currentRealm"] = realm }.ToJsonString());
        await _fs.WriteFileAtomicAsync("output/narrative_response.json", new JsonObject
            { ["response"] = Quote, ["timestamp"] = DateTimeOffset.UtcNow.ToString("O") }.ToJsonString());
        var debug = new JsonObject();
        if (include) debug["npcSkillClaims"] = report;
        await _fs.WriteFileAtomicAsync("output/debug_logs.json", debug.ToJsonString());
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", Core());
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_skills.json", Skills(5));
    }
    private async Task<List<ValidationIssue>> Evaluate() => (await _validator.ValidateAcceptedTurnNarrativePayloadAsync())
        .Where(x => x.Code?.StartsWith("npc_skill_claim_", StringComparison.Ordinal) == true).ToList();

    [Theory]
    [InlineData("missing")][InlineData("null")][InlineData("object")][InlineData("row")]
    public async Task MortalReport_IsMandatoryAndClosedShape(string mode)
    {
        await Setup(mode == "object" ? new JsonObject() : mode == "row" ? new JsonArray(4) : null, mode != "missing");
        Assert.NotEmpty(await Evaluate());
    }
    [Fact]
    public async Task EmptyReport_DoesNotInferClaimsFromNpcProse()
    {
        await Setup(new JsonArray()); Assert.Empty(await Evaluate());
    }
    [Fact]
    public async Task Afterlife_DoesNotRequireMortalReport()
    {
        await Setup(null, false, "Chaos Sea"); Assert.Empty(await Evaluate());
    }
    [Fact]
    public async Task ExplicitNoProgressUse_RequiresOnlyCurrentExactOwnership()
    {
        await Setup(new JsonArray(Claim("noProgress"))); Assert.Empty(await Evaluate());
    }
    [Theory]
    [InlineData("actorKey", "id:npc_b")][InlineData("skillName", "искра")]
    [InlineData("skillKind", "passive")][InlineData("claimType", "learning")]
    [InlineData("narrativeQuote", "Непроизнесённая фраза")][InlineData("reason", " ")]
    public async Task NoProgress_RejectsWrongBindingOrUnsupportedException(string field, string value)
    {
        var claim = Claim("noProgress"); claim[field] = value;
        await Setup(new JsonArray(claim)); Assert.NotEmpty(await Evaluate());
    }
    [Fact]
    public async Task StateDelta_RejectsMissingOriginalEvidence()
    {
        await Setup(new JsonArray(Claim())); Assert.NotEmpty(await Evaluate());
    }
    [Theory]
    [InlineData(5, "npc_a", false)]
    [InlineData(0, "npc_a", true)]
    [InlineData(5, "npc_b", true)]
    public async Task Progression_RequiresFreshExactOwnerEntry(int progress, string owner, bool rejected)
    {
        await Setup(new JsonArray(Claim()));
        await Original(Core(), Skills(0));
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_skills.json", Skills(progress, owner));
        var result = await Evaluate();
        Assert.Equal(rejected, result.Count > 0);
    }
    [Fact]
    public async Task Learning_RequiresSkillAbsentFromProvenOriginal()
    {
        var claim = Claim(); claim["claimType"] = "learning";
        await Setup(new JsonArray(claim));
        await Original(Core(), Skills(0));
        Assert.NotEmpty(await Evaluate());
    }
    private async Task Original(string core, string skills)
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json",
            """{"sessionId":"npc-claims","requestId":"claim-turn","turnNumber":9,"playerAction":"Открыть дверь"}""");
        var files = new JsonObject(); var hashes = new JsonObject(); var baseline = new JsonArray();
        foreach (var (path, json) in new[] { ("game_state/npcs/npc_core.json", core), ("game_state/npcs/npc_skills.json", skills) })
        {
            var snapshotPath = "game_state/control/pending_turn_snapshot/" + path;
            await _fs.WriteFileAtomicAsync(snapshotPath, json);
            files[path] = snapshotPath; hashes[path] = PendingTurnSnapshotAuthority.ComputeSha256(json); baseline.Add(path);
        }
        var manifest = new JsonObject
        {
            ["sessionId"] = "npc-claims", ["requestId"] = "claim-turn", ["turnNumber"] = 9,
            ["requestTimestamp"] = "2026-11-13T03:00:00Z", ["playerAction"] = "Открыть дверь",
            ["files"] = files, ["snapshotFileHashes"] = hashes,
            ["clientOwnedValidationHashes"] = new JsonObject(), ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = baseline, ["sourceLabel"] = "NPC claim test", ["manifestPayloadHash"] = ""
        };
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
        await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.json", manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

}
