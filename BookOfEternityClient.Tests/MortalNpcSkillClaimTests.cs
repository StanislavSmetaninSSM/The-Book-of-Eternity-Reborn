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

    [Fact]
    public async Task CaseVariantOwner_DuplicateReportIsRejected()
    {
        var other = Claim("noProgress"); other["actorKey"] = "id:NPC_A";
        await Setup(new JsonArray(Claim("noProgress"), other));
        Assert.Contains(await Evaluate(), x => x.Code == "npc_skill_claim_duplicate");
    }
    [Fact]
    public async Task PrivateReportMarker_IsRejectedInPlayerNarrative()
    {
        await Setup(new JsonArray());
        await _fs.WriteFileAtomicAsync("output/narrative_response.json", new JsonObject
            { ["response"] = "npcSkillClaims: []", ["timestamp"] = DateTimeOffset.UtcNow.ToString("O") }.ToJsonString());
        Assert.Contains(await _validator.ValidateAcceptedTurnNarrativePayloadAsync(),
            x => x.Code == "narrative_response_technical_repair_leak");
    }
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

    [Fact]
    public async Task SkillDeltaCannotInventAnAbsentNpcOwner()
    {
        await Setup(new JsonArray(Claim("noProgress")));
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", "{}");
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_skills.json", """
        {"NPCActiveSkillChanges":[{"NPCId":"npc_a","skillChanges":[{"skillName":"Искра"}]}]}
        """);
        Assert.NotEmpty(await Evaluate());
    }
    [Theory]
    [InlineData("duplicate-report")][InlineData("duplicate-skill")][InlineData("duplicate-key")]
    [InlineData("original-corrupt")][InlineData("cosmetic")]
    public async Task AmbiguousOrUnreadableOrCosmeticEvidence_IsRejected(string mode)
    {
        await Setup(new JsonArray(Claim()));
        await Original(mode == "original-corrupt" ? "{" : Core(), Skills(0));
        if (mode == "duplicate-report") await _fs.WriteFileAtomicAsync("output/debug_logs.json",
            new JsonObject { ["npcSkillClaims"] = new JsonArray(Claim(), Claim()) }.ToJsonString());
        if (mode == "duplicate-key") await _fs.WriteFileAtomicAsync("output/debug_logs.json", """{"npcSkillClaims":[],"npcSkillClaims":[]}""");
        if (mode == "duplicate-skill")
        {
            var duplicate = JsonNode.Parse(Skills(5))!.AsObject();
            duplicate["NPCSkillMasteryChanges"]!.AsArray().Add(duplicate["NPCSkillMasteryChanges"]![0]!.DeepClone());
            await _fs.WriteFileAtomicAsync("game_state/npcs/npc_skills.json", duplicate.ToJsonString());
        }
        if (mode == "cosmetic")
        {
            var current = JsonNode.Parse(Skills(0))!.AsObject();
            current["NPCSkillMasteryChanges"]![0]!["description"] = "New decorative wording";
            await _fs.WriteFileAtomicAsync("game_state/npcs/npc_skills.json", current.ToJsonString());
        }
        Assert.NotEmpty(await Evaluate());
    }
    [Fact]
    public async Task Learning_AcceptsGenuinelyNewOwnedSkill()
    {
        var claim = Claim(); claim["claimType"] = "learning";
        await Setup(new JsonArray(claim)); await Original("{}", "{}");
        Assert.Empty(await Evaluate());
    }
    [Fact]
    public async Task PassiveProgress_UsesOnlyItsOwnKind()
    {
        var claim = Claim(); claim["skillKind"] = "passive";
        await Setup(new JsonArray(claim));
        await Original(Core().Replace("activeSkills", "passiveSkills"), Skills(0).Replace("NPCSkillMasteryChanges", "NPCPassiveSkillMasteryChanges"));
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", Core().Replace("activeSkills", "passiveSkills"));
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_skills.json", Skills(5).Replace("NPCSkillMasteryChanges", "NPCPassiveSkillMasteryChanges"));
        Assert.Empty(await Evaluate());
    }

    [Theory]
    [InlineData("owner-case")][InlineData("consistent-mirror")]
    public async Task ExistingNpcIdentityAndConsistentMirrors_RemainValid(string mode)
    {
        await Setup(new JsonArray(Claim("noProgress")));
        var core = JsonNode.Parse(Core())!.AsObject();
        if (mode == "owner-case") core["UpdateNPCs"]![0]!["NPCId"] = "NPC_A";
        else core["NPCsInScene"] = core["UpdateNPCs"]!.DeepClone();
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", core.ToJsonString());
        Assert.Empty(await Evaluate());
    }
    [Fact]
    public async Task NewlyMortalSoul_StillRequiresReportWhenRequestStartedInAfterlife()
    {
        await Setup(null, false);
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """{"progressionControl":{"currentRealm":"Chaos Sea"}}""");
        Assert.NotEmpty(await Evaluate());
    }
    [Theory]
    [InlineData("game_state/meta/soul_state.json")]
    [InlineData("game_state/npcs/npc_core.json")]
    [InlineData("game_state/npcs/npc_skills.json")]
    public async Task CapturedPass_RechecksFreshRealmAndNpcSourcesUnderLease(string path)
    {
        await Setup(new JsonArray(Claim("noProgress")));
        using var pass = await _validator.ValidateAcceptedTurnNarrativePolicyPassAsync();
        Assert.DoesNotContain(pass.Issues, x => x.Code?.StartsWith("npc_skill_claim_", StringComparison.Ordinal) == true);
        Assert.Contains(pass.CaptureDiagnostic().Occurrences, x => x.Path == path);
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        await pass.RequireCurrentAsync(_fs, lease);
        var physical = _fs.ResolvePath(path); var before = File.ReadAllText(physical);
        File.WriteAllText(physical, before + " ");
        await Assert.ThrowsAsync<InvalidDataException>(() => pass.RequireCurrentAsync(_fs, lease));
        File.WriteAllText(physical, before);
        await Assert.ThrowsAsync<InvalidDataException>(() => pass.RequireCurrentAsync(_fs, lease));
    }
    [Fact]
    public async Task EmptyReport_ObservesRealmButDoesNotReadNpcData()
    {
        await Setup(new JsonArray());
        using var pass = await _validator.ValidateAcceptedTurnNarrativePolicyPassAsync();
        Assert.DoesNotContain(pass.CaptureDiagnostic().Occurrences, x => x.Path is "game_state/npcs/npc_core.json" or "game_state/npcs/npc_skills.json");
        Assert.Contains(pass.CaptureDiagnostic().Occurrences, x => x.Path == "game_state/meta/soul_state.json");
    }

    [Fact]
    public async Task PublicReasoningValidator_AcceptsTheMandatoryReportField()
    {
        await Setup(new JsonArray());
        await _fs.WriteFileAtomicAsync("output/debug_logs.json", new JsonObject
        {
            ["npcSkillClaims"] = new JsonArray(), ["gm_thoughts_markdown"] = OrdinaryReasoningFixture.ValidThoughts,
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("O")
        }.ToJsonString());
        Assert.DoesNotContain(await _validator.ValidateAcceptedTurnReasoningAsync(), x => x.Code == "debug_logs_unknown_field");
    }
    [Theory]
    [InlineData("case-drift")][InlineData("malformed-carrier")][InlineData("malformed-actor")]
    public async Task Learning_CannotUseCorruptOriginalOrOwnerCaseDriftAsAbsence(string mode)
    {
        var claim = Claim(); claim["claimType"] = "learning";
        await Setup(new JsonArray(claim));
        var oldCore = mode switch { "case-drift" => Core().Replace("npc_a", "NPC_A"),
            "malformed-carrier" => """{"UpdateNPCs":{}}""", _ => """{"UpdateNPCs":[4]}""" };
        await Original(oldCore, "{}");
        Assert.NotEmpty(await Evaluate());
    }

    [Fact]
    public async Task CurrentRemovalCannotSatisfyUseOwnership()
    {
        await Setup(new JsonArray(Claim("noProgress")));
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_skills.json", """
        {"NPCActiveSkillChanges":[{"NPCId":"npc_a","skillsToRemove":["Искра"]}]}
        """);
        Assert.NotEmpty(await Evaluate());
    }
    [Fact]
    public async Task ExactCanonicalWhitespaceInSkillName_IsPreserved()
    {
        var claim = Claim("noProgress"); claim["skillName"] = "Искра ";
        await Setup(new JsonArray(claim));
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", Core("Искра "));
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_skills.json", "{}");
        Assert.Empty(await Evaluate());
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
