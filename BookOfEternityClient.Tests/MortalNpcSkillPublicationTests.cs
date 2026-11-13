using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Fixture = BookOfEternityClient.Tests.MortalNpcProgressionPublicationTests.Fixture;

namespace BookOfEternityClient.Tests;

public sealed class MortalNpcSkillPublicationTests
{
    private const string SkillsPath = "game_state/npcs/npc_skills.json";

    [Fact]
    public async Task Coordinator_NoPlan_SkillOnlyMaterializesCoreAndConsumesPendingCommands()
    {
        using var fixture = await Fixture.Create(includeSkillSidecar: true, progression: false);
        await WritePending(fixture, mastery: false);
        var result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            fixture.Files, fixture.Normalizer, fixture.Validator, fixture.Backups);
        Assert.Empty(result.Issues);
        Assert.Null(result.MechanicsPlan);
        var actor = (await Read(fixture, NpcCoreChangesContract.NpcCorePath))["NPCsInScene"]![0]!;
        Assert.Equal(1, actor["level"]!.GetValue<int>());
        Assert.Equal("Archive", Assert.Single(actor["passiveSkills"]!.AsArray())!["skillName"]!.GetValue<string>());
        await AssertConsumed(fixture);
        // Genuine signed original is a clean command baseline, not the current pending sidecar.
        var original = JsonNode.Parse((await fixture.Files.ReadFileAsync(fixture.Backups[SkillsPath]))!)!;
        Assert.Empty(original["NPCPassiveSkillChanges"]!.AsArray());
    }

    [Fact]
    public async Task Coordinator_NoPlan_ProgressionAndSkillMasteryShareCanonicalCandidate()
    {
        using var fixture = await Fixture.Create(includeSkillSidecar: true);
        await WritePending(fixture, mastery: true);
        var result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            fixture.Files, fixture.Normalizer, fixture.Validator, fixture.Backups);
        Assert.Empty(result.Issues);
        Assert.Null(result.MechanicsPlan);
        var core = await Read(fixture, NpcCoreChangesContract.NpcCorePath);
        Assert.Null(core["NPCCoreChanges"]);
        Assert.Equal(2, core["NPCsInScene"]![0]!["level"]!.GetValue<int>());
        Assert.Equal(3, Assert.Single(core["NPCsInScene"]![0]!["passiveSkills"]!.AsArray())!["masteryLevel"]!.GetValue<int>());
        await AssertConsumed(fixture);
    }

    [Fact]
    public Task Coordinator_PostwriteSkillTamperIsRefusedAndRolledBack() => VerifyLateFailure(tamper: true);

    [Fact]
    public Task Coordinator_LatePostvalidatorFailureRestoresCoreAndSidecarBytes() => VerifyLateFailure(tamper: false);

    [Fact]
    public async Task Coordinator_CurrentWorldAuthorityRemovedAfterAdmission_RefusesAndRollsBackSkills()
    {
        using var fixture = await Fixture.Create(includeSkillSidecar: true, progression: false);
        await WritePending(fixture, mastery: false);
        const string characteristicsPath = "game_state/misc/characteristics.json";
        await fixture.Files.WriteFileAtomicAsync(characteristicsPath, "{\"ArchiveInsight\":0}");
        var raw = await Read(fixture, NpcCoreChangesContract.NpcCorePath);
        Assert.Null(raw["NPCsInScene"]![0]!["characteristics"]);
        raw["NPCCoreChanges"] = new JsonArray(new JsonObject
        {
            ["NPCId"] = "npc_source", ["reason"] = "Study the archive",
            ["characteristicValues"] = new JsonObject { ["ArchiveInsight"] = 2 }
        });
        await fixture.Files.WriteFileAtomicAsync(NpcCoreChangesContract.NpcCorePath, raw.ToJsonString());
        await fixture.Files.WriteFileAtomicAsync(ValidationService.CanonicalSkillMasteryPath, "{}");
        var beforeCore = await fixture.Files.ReadFileBytesAsync(NpcCoreChangesContract.NpcCorePath);
        var beforeSkills = await fixture.Files.ReadFileBytesAsync(SkillsPath);
        var injected = false;
        var files = fixture.CreateFiles(new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (injected || path != ValidationService.CanonicalSkillMasteryPath) return Task.CompletedTask;
                var core = JsonNode.Parse(File.ReadAllText(fixture.Physical(NpcCoreChangesContract.NpcCorePath)))!;
                var sidecar = JsonNode.Parse(File.ReadAllText(fixture.Physical(SkillsPath)))!.AsObject();
                if (core["NPCCoreChanges"] is not null ||
                    core["NPCsInScene"]![0]!["passiveSkills"] is not JsonArray { Count: 1 } ||
                    sidecar.ContainsKey("NPCPassiveSkillChanges")) return Task.CompletedTask;
                Assert.Equal(2, core["NPCsInScene"]![0]!["characteristics"]!["ArchiveInsight"]!.GetValue<int>());
                injected = true;
                // This key was authorized only by the current world, never by the original actor.
                File.WriteAllText(fixture.Physical(characteristicsPath), "{}");
                return Task.CompletedTask;
            }
        });
        AcceptedTurnCanonicalStateRefresh.Result? result = null;
        var error = await Record.ExceptionAsync(async () =>
        {
            result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(files,
                new CanonicalStateNormalizer(files, NullLogger<CanonicalStateNormalizer>.Instance),
                new ValidationService(files, NullLogger<ValidationService>.Instance), fixture.Backups);
        });
        Assert.True(injected, "Must admit and materialize the world-authorized characteristic and skill before removing authority.");
        Assert.True(error is InvalidDataException || result?.Issues.Count > 0,
            "Changed current-world authority must invalidate the retained candidate before success.");
        Assert.Equal(beforeCore, await fixture.Files.ReadFileBytesAsync(NpcCoreChangesContract.NpcCorePath));
        Assert.Equal(beforeSkills, await fixture.Files.ReadFileBytesAsync(SkillsPath));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("null")]
    public async Task Coordinator_MissingCoreWithMalformedOrNonObjectSkillSidecar_Refuses(string sidecar)
    {
        using var fixture = await Fixture.Create(includeSkillSidecar: true, progression: false);
        File.Delete(fixture.Physical(NpcCoreChangesContract.NpcCorePath));
        await fixture.Files.WriteFileAtomicAsync(SkillsPath, sidecar);
        var beforeSkills = await fixture.Files.ReadFileBytesAsync(SkillsPath);
        AcceptedTurnCanonicalStateRefresh.Result? result = null;
        var error = await Record.ExceptionAsync(async () =>
        {
            result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                fixture.Files, fixture.Normalizer, fixture.Validator, fixture.Backups);
        });
        Assert.True(error is InvalidDataException || result?.Issues.Count > 0,
            "Missing core must not bypass malformed or non-object skill command input validation.");
        Assert.Null(await fixture.Files.ReadFileBytesAsync(NpcCoreChangesContract.NpcCorePath));
        Assert.Equal(beforeSkills, await fixture.Files.ReadFileBytesAsync(SkillsPath));
    }

    [Fact]
    public async Task GenericNormalizer_PendingSkillsWithoutPublicationSource_RefusesBeforeWrites()
    {
        using var fixture = await Fixture.Create(includeSkillSidecar: true, progression: false);
        await WritePending(fixture, mastery: false);
        var before = await fixture.CaptureCanonical();
        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            fixture.Normalizer.NormalizeAccumulatedStateAsync(fixture.Backups));
        Assert.Contains("NPC skill", error.Message, StringComparison.Ordinal);
        await fixture.AssertCanonicalUnchanged(before);
    }

    [Fact]
    public async Task Coordinator_SignedAbsentOriginalSidecar_FirstSkillPublishesAndConsumes()
    {
        using var fixture = await Fixture.Create(includeSkillSidecar: true, progression: false,
            originalSkillSidecarExists: false);
        Assert.False(fixture.Backups.ContainsKey(SkillsPath));
        await WritePending(fixture, mastery: false);
        var result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            fixture.Files, fixture.Normalizer, fixture.Validator, fixture.Backups);
        Assert.Empty(result.Issues);
        var core = await Read(fixture, NpcCoreChangesContract.NpcCorePath);
        Assert.Equal("Archive", Assert.Single(core["NPCsInScene"]![0]!["passiveSkills"]!.AsArray())!["skillName"]!.GetValue<string>());
        await AssertConsumed(fixture);
    }

    [Fact]
    public async Task Coordinator_SignedAbsentSidecarWithOrphanHashRefusesDespiteValidAuthentication()
    {
        using var fixture = await Fixture.Create(includeSkillSidecar: true,
            originalSkillSidecarExists: false);
        const string manifestPath = "game_state/control/pending_turn_snapshot.json";
        var manifest = await Read(fixture, manifestPath);
        Assert.False(manifest["files"]!.AsObject().ContainsKey(SkillsPath));
        Assert.False(manifest["originalPathPresenceV1"]![SkillsPath]!.GetValue<bool>());
        manifest["snapshotFileHashes"]![SkillsPath] = PendingTurnSnapshotAuthority.ComputeSha256("{}");
        manifest["manifestPayloadHash"] = string.Empty;
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
        await fixture.Files.WriteFileAtomicAsync(manifestPath, manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(fixture.Files);

        // The same re-signed manifest still authenticates an ordinary progression
        // source. The later refusal must be the contradictory sidecar observation,
        // not an invalid manifest hash, stale request, or missing detached signature.
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            var source = await fixture.Validator.PrepareNpcProgressionPublicationAsync(lease,
                await fixture.Files.ReadFileBytesAsync(lease, NpcCoreChangesContract.NpcCorePath));
            Assert.Empty(source.Issues);
            Assert.NotNull(source.Context);
        }
        await WritePending(fixture, mastery: false);
        var before = await fixture.CaptureCanonical();
        var result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            fixture.Files, fixture.Normalizer, fixture.Validator, fixture.Backups);
        Assert.Contains(result.Issues, issue => issue.Code == "npc_core_changes_invalid_json" &&
            issue.Actual == "authenticated original NPC skill sidecar unavailable");
        await fixture.AssertCanonicalUnchanged(before);
    }

    [Theory]
    [InlineData("uncovered")]
    [InlineData("presentMissing")]
    [InlineData("absentWithPresentBackup")]
    [InlineData("absentWithMalformedBackup")]
    public async Task Coordinator_OriginalSidecarAbsenceRequiresProofAndActualBackupAbsence(string scenario)
    {
        bool? originalExists = scenario == "uncovered" ? null : scenario == "presentMissing";
        using var fixture = await Fixture.Create(includeSkillSidecar: true, progression: false,
            originalSkillSidecarExists: originalExists);
        if (scenario == "presentMissing") File.Delete(fixture.Physical(fixture.Backups[SkillsPath]));
        if (scenario.StartsWith("absentWith", StringComparison.Ordinal))
        {
            const string suppliedBackup = "game_state/control/extra_npc_skills_backup.json";
            await fixture.Files.WriteFileAtomicAsync(suppliedBackup,
                scenario == "absentWithPresentBackup" ? "{}" : "{");
            fixture.Backups[SkillsPath] = suppliedBackup;
        }
        await WritePending(fixture, mastery: false);
        var before = await fixture.CaptureCanonical();
        AcceptedTurnCanonicalStateRefresh.Result? result = null;
        var error = await Record.ExceptionAsync(async () =>
        {
            result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                fixture.Files, fixture.Normalizer, fixture.Validator, fixture.Backups);
        });
        Assert.True(error is InvalidDataException || result?.Issues.Count > 0,
            "Only authenticated absence with an actually absent reducer backup can bootstrap NPC skills.");
        await fixture.AssertCanonicalUnchanged(before);
    }

    private static async Task VerifyLateFailure(bool tamper)
    {
        using var fixture = await Fixture.Create(includeSkillSidecar: true);
        await WritePending(fixture, mastery: true);
        await fixture.Files.WriteFileAtomicAsync(ValidationService.CanonicalSkillMasteryPath, "{}");
        var before = await fixture.CaptureCanonical();
        var injected = false;
        var files = fixture.CreateFiles(new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (injected || path != ValidationService.CanonicalSkillMasteryPath) return Task.CompletedTask;
                var core = JsonNode.Parse(File.ReadAllText(fixture.Physical(NpcCoreChangesContract.NpcCorePath)))!;
                var sidecar = JsonNode.Parse(File.ReadAllText(fixture.Physical(SkillsPath)))!;
                if (core["NPCCoreChanges"] is not null ||
                    core["NPCsInScene"]![0]!["passiveSkills"] is not JsonArray { Count: 1 } skills ||
                    sidecar["NPCPassiveSkillChanges"] is JsonArray { Count: > 0 }) return Task.CompletedTask;
                Assert.Equal(3, skills[0]!["masteryLevel"]!.GetValue<int>());
                injected = true;
                if (!tamper) throw new InvalidDataException("injected NPC skill late postvalidation failure");
                // Valid-shaped but wrong final numeric output must fail exact candidate readback.
                skills[0]!["masteryLevel"] = 2;
                File.WriteAllText(fixture.Physical(NpcCoreChangesContract.NpcCorePath), core.ToJsonString());
                return Task.CompletedTask;
            }
        });
        AcceptedTurnCanonicalStateRefresh.Result? result = null;
        var error = await Record.ExceptionAsync(async () =>
        {
            result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(files,
                new CanonicalStateNormalizer(files, NullLogger<CanonicalStateNormalizer>.Instance),
                new ValidationService(files, NullLogger<ValidationService>.Instance), fixture.Backups);
        });
        Assert.True(injected, "Must reach actual postwrite core+consumed-sidecar before injecting failure.");
        if (tamper)
            Assert.True(error is InvalidDataException || result?.Issues.Count > 0,
                "Tampered canonical output must be refused, not accepted.");
        else
        {
            Assert.IsType<InvalidDataException>(error);
            Assert.Contains("injected NPC skill late postvalidation failure", error!.Message, StringComparison.Ordinal);
        }
        await fixture.AssertCanonicalUnchanged(before);
        Assert.Equal(before[NpcCoreChangesContract.NpcCorePath], await fixture.Files.ReadFileBytesAsync(NpcCoreChangesContract.NpcCorePath));
        Assert.Equal(before[SkillsPath], await fixture.Files.ReadFileBytesAsync(SkillsPath));
    }

    private static async Task WritePending(Fixture fixture, bool mastery)
    {
        var pending = await Read(fixture, SkillsPath);
        pending["NPCPassiveSkillChanges"] = new JsonArray(new JsonObject
        {
            ["NPCId"] = "npc_source", ["skillChanges"] = new JsonArray(new JsonObject
            {
                ["skillName"] = "Archive", ["skillDescription"] = "Archive knowledge", ["rarity"] = "Common",
                ["type"] = "KnowledgeBased", ["group"] = "Archives", ["masteryLevel"] = 1,
                ["maxMasteryLevel"] = 5, ["structuredBonuses"] = null, ["knowledgeDomain"] = "Archives",
                ["unlockedActiveSkillsCount"] = 0, ["maxUnlockableActiveSkills"] = 2
            })
        });
        if (mastery)
            pending["NPCPassiveSkillMasteryChanges"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = "npc_source", ["skillName"] = "Archive", ["newMasteryLevel"] = 3
            });
        await fixture.Files.WriteFileAtomicAsync(SkillsPath, pending.ToJsonString());
    }

    private static async Task<JsonObject> Read(Fixture fixture, string path) =>
        JsonNode.Parse((await fixture.Files.ReadFileAsync(path))!)!.AsObject();

    private static async Task AssertConsumed(Fixture fixture)
    {
        var sidecar = await Read(fixture, SkillsPath);
        foreach (var property in new[] { "NPCActiveSkillChanges", "NPCPassiveSkillChanges", "NPCSkillMasteryChanges", "NPCPassiveSkillMasteryChanges" })
            Assert.False(sidecar.ContainsKey(property), "Consumed command property must be absent: " + property);
    }
}
