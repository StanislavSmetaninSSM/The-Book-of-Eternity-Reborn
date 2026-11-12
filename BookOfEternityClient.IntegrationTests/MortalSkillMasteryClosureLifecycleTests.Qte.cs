using System.Text.Json.Nodes;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalSkillMasteryClosureLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QteTerminal_RemovalOrReplacementRejectsSnapshotMasteryAndRestores(bool rename)
    {
        await WriteValidPairAsync();
        await QteSnapshotAsync(Active("Fireball"));
        await _fs.WriteFileAtomicAsync("game_state/player/experience.json", "{\"totalExperience\":10}");
        var paths = new[] { ActivePath, MasteryPath, "game_state/player/experience.json", "output/narrative_response.json",
            "game_state/control/pending_turn_snapshot.json", PendingTurnSnapshotAuthority.AuthorityPath };
        var before = new Dictionary<string, byte[]?>();
        foreach (var path in paths) before[path] = await _fs.ReadFileBytesAsync(path);
        var generation = File.ReadAllBytes(_fs.SessionGenerationPath);
        var fragment = new JsonObject { ["experienceGained"] = 5 };
        if (rename)
        {
            fragment["activeSkillChanges"] = Active("Iceball")["activeSkillChanges"]!.DeepClone();
            var rows = JsonNode.Parse(Mastery("Fireball"))!["skillMasteryChanges"]!.AsArray();
            var next = rows[0]!.DeepClone();
            next["skillName"] = "Iceball";
            rows.Add(next);
            fragment["skillMasteryChanges"] = rows.DeepClone();
        }
        else fragment["removeActiveSkills"] = new JsonArray("Fireball");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateQteService().ApplyTerminalOutcomeValidatedStateChangesAsync(QteOutcome(fragment), allowPreexistingStateIssues: true));
        Assert.Contains("skill_mastery_unknown_active_skill at " + MasteryPath + ".skillMasteryChanges[0].skillName",
            failure.Message, StringComparison.Ordinal);
        foreach (var (path, bytes) in before) Assert.Equal(bytes, await _fs.ReadFileBytesAsync(path));
        Assert.Equal(generation, File.ReadAllBytes(_fs.SessionGenerationPath));
        Assert.False(Directory.Exists(_fs.ResolvePath(QteSceneService.QteNormalizerBackupDirectory)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QteTerminal_ExactRetainedOrNewSkillKeepsUnrelatedPreexistingWaiver(bool newSkill)
    {
        await _fs.WriteFileAtomicAsync(ActivePath, (newSkill ? Active() : Active("Fireball")).ToJsonString());
        await _fs.WriteFileAtomicAsync(MasteryPath, newSkill ? "{\"skillMasteryChanges\":[]}" : Mastery("Fireball"));
        await QteSnapshotAsync(newSkill ? Active() : Active("Fireball"));
        var prior = await _validator.ValidateQteTerminalGameStateAsync(null);
        Assert.Contains(prior, issue => issue.Code == "missing_player_status_in_mortal_world");
        Assert.DoesNotContain(prior, issue => issue.Code?.StartsWith("skill_", StringComparison.Ordinal) == true);
        var fragment = JsonNode.Parse(Mastery("Fireball"))!.AsObject();
        fragment["skillMasteryChanges"]![0]!["newCurrentMasteryProgress"] = 8;
        if (newSkill) fragment["activeSkillChanges"] = Active("Fireball")["activeSkillChanges"]!.DeepClone();

        await CreateQteService().ApplyTerminalOutcomeValidatedStateChangesAsync(QteOutcome(fragment), allowPreexistingStateIssues: true);
        var active = await _fs.ReadFileAsync(ActivePath);
        var mastery = await _fs.ReadFileAsync(MasteryPath);
        Assert.Empty(ValidationService.ValidateCanonicalPlayerSkillMastery(active, mastery));
        Assert.Equal("Fireball", JsonNode.Parse(active!)!["activeSkillChanges"]![0]!["skillName"]!.GetValue<string>());
        Assert.Equal(8, JsonNode.Parse(mastery!)!["skillMasteryChanges"]![0]!["newCurrentMasteryProgress"]!.GetValue<int>());
        Assert.False(Directory.Exists(_fs.ResolvePath(QteSceneService.QteNormalizerBackupDirectory)));
    }

    [Theory]
    [InlineData("RemovedSpell", "skill_mastery_unknown_active_skill")]
    [InlineData("fireball", "skill_mastery_confusable_reference")]
    public async Task QteTerminal_CurrentMasteryIntegrityCannotBeWaivedAsPreexisting(string reference, string code)
    {
        await _fs.WriteFileAtomicAsync(ActivePath, Active("Fireball").ToJsonString());
        await _fs.WriteFileAtomicAsync(MasteryPath, Mastery(reference));
        var prior = await _validator.ValidateQteTerminalGameStateAsync(null);
        Assert.Contains(prior, issue => issue.Code == code);
        var before = await _fs.ReadFileBytesAsync(MasteryPath);
        var generation = File.ReadAllBytes(_fs.SessionGenerationPath);
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateQteService().ApplyTerminalOutcomeValidatedStateChangesAsync(QteOutcome(new JsonObject()), allowPreexistingStateIssues: true));
        Assert.Contains(code, failure.Message, StringComparison.Ordinal);
        Assert.Equal(before, await _fs.ReadFileBytesAsync(MasteryPath));
        Assert.Equal(generation, File.ReadAllBytes(_fs.SessionGenerationPath));
        Assert.False(_fs.FileExists("output/narrative_response.json"));
        Assert.False(Directory.Exists(_fs.ResolvePath(QteSceneService.QteNormalizerBackupDirectory)));
    }

    private QteSceneService CreateQteService() => new(_fs, _state.Settings, null!, null!, null!,
        new StateDistributor(_fs, NullLogger<StateDistributor>.Instance), _validator,
        new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance), _state,
        NullLogger<QteSceneService>.Instance);

    private static QteSceneService.QteTerminalOutcome QteOutcome(JsonObject fragment) => new()
    {
        OutcomeId = "qte_skill_closure", Title = "Skill outcome", FinalNarrative = "Исход завершён.",
        GmSummary = "Current skill mastery closure.", ResponseFragment = fragment
    };

    private async Task QteSnapshotAsync(JsonObject active)
    {
        await SnapshotAsync(active);
        const string soulPath = "game_state/meta/soul_state.json";
        var soul = (await _fs.ReadFileAsync(soulPath))!;
        var backup = "game_state/control/pending_turn_snapshot/" + soulPath;
        await _fs.WriteFileAtomicAsync(backup, soul);
        const string manifestPath = "game_state/control/pending_turn_snapshot.json";
        var manifest = JsonNode.Parse((await _fs.ReadFileAsync(manifestPath))!)!.AsObject();
        manifest["files"]![soulPath] = backup;
        manifest["snapshotFileHashes"]![soulPath] = PendingTurnSnapshotAuthority.ComputeSha256(soul);
        manifest["rollbackBaselineFiles"]!.AsArray().Add(soulPath);
        manifest["sourceLabel"] = QteSceneService.OrdinaryPlayerTurnSourceLabel;
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
        await _fs.WriteFileAtomicAsync(manifestPath, manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }
}
