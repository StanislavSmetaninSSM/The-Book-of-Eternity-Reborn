using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectMaterializationValidationTests
{
    [Fact]
    public async Task OrdinaryNpcProgressionSkillAndSelfEffect_PublishesSameActorWithoutCoreOverwrite()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var backups = await PrepareNpcSelfEffectPublicationAsync(context);
        var sources = backups.Values.Concat(new[] { "input/turn_request.json",
            "game_state/control/pending_turn_snapshot.json", PendingTurnSnapshotAuthority.AuthorityPath })
            .Distinct(StringComparer.Ordinal).ToArray();
        var before = await context.CaptureBytesAsync(sources);
        var admitted = await AdmitNpcSkillCommonPublicationAsync(context);
        Assert.False(admitted.HasWholeCarrierAfterImage(NpcSkillCorePath));

        var result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, backups);

        Assert.DoesNotContain(result.Issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.Equal(admitted.InputFingerprint, Assert.IsType<AcceptedMechanicsPlan>(result.MechanicsPlan).InputFingerprint);
        AssertNpcSelfEffectPublished(context.FileSystem);
        Assert.Null(await context.ReadJsonAsync(EffectMaterializationTestContext.CommandPath));
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnCanonicalEffectMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        Assert.Equal(before, await context.CaptureBytesAsync(sources));
        Assert.Null(await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(context.FileSystem));
        var acceptedPaths = new[] { NpcSkillCorePath, PendingNpcSkillsPath,
            EffectMaterializationTestContext.NpcEffectsPath, EffectMaterializationTestContext.IdentityIndexPath };
        var accepted = await context.CaptureBytesAsync(acceptedPaths);
        var repeated = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, backups);
        Assert.DoesNotContain(repeated.Issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.Equal(accepted, await context.CaptureBytesAsync(acceptedPaths));
        AssertNpcSelfEffectPublished(context.FileSystem);
    }

    [Fact]
    public async Task OrdinaryNpcProgressionSkillAndSelfEffect_ReachedLateCutRestoresCoreSidecarsAndEffect()
    {
        var armed = false;
        var reached = false;
        FileSystemManager? files = null;
        const string marker = "NPC self effect reached publication cut";
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationBoundaryAsync = path =>
            {
                if (!armed || reached || path != EffectMaterializationTestContext.CommandPath)
                    return Task.CompletedTask;
                reached = true;
                AssertNpcSelfEffectPublished(files!);
                throw new InvalidOperationException(marker);
            }
        };
        await using var context = await EffectMaterializationTestContext.CreateAsync(hooks);
        files = context.FileSystem;
        var backups = await PrepareNpcSelfEffectPublicationAsync(context);
        await AdmitNpcSkillCommonPublicationAsync(context);
        var paths = CanonicalStateNormalizer.NormalizerRollbackTrackedFiles.Concat(backups.Values)
            .Concat(new[] { "input/turn_request.json", "game_state/control/pending_turn_snapshot.json",
                PendingTurnSnapshotAuthority.AuthorityPath, "output/narrative_response.json",
                "output/interface_updates.json", "output/debug_logs.json" })
            .Distinct(StringComparer.Ordinal).ToArray();
        var before = await context.CaptureBytesAsync(paths);
        armed = true;
        var failure = await Record.ExceptionAsync(() =>
            AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                context.FileSystem, context.Normalizer, context.Validator, backups));
        armed = false;

        Assert.True(reached, "The ordinary effect writer must reach command consumption after NPC publication.");
        Assert.NotNull(failure);
        Assert.Contains(marker, failure!.ToString(), StringComparison.Ordinal);
        Assert.Equal(before, await context.CaptureBytesAsync(paths));
    }

    private static async Task<IReadOnlyDictionary<string, string>> PrepareNpcSelfEffectPublicationAsync(
        EffectMaterializationTestContext context)
    {
        await context.WriteJsonAsync(MortalItemIdentityState.StatePath, MortalItemIdentityState.CreateEmptyRoot());
        await context.WriteJsonAsync(NpcSkillCorePath, new JsonObject { ["NPCsInScene"] = new JsonArray() });
        await context.CaptureValidatedPendingSnapshotAsync();
        var actor = EffectMaterializationTestFixture.CreateSameTurnMortalActor(NpcSkillOwner);
        actor["progressionProfile"] = JsonNode.Parse("""
            {"schemaVersion":1,"mode":"standard",
             "consequences":{"skills":{"levelsPerAward":2,"allowedAwards":["newSkill","masteryIncrease"]},
             "characteristics":{"mode":"currentWorld"}}}
            """);
        actor["level"] = 1;
        actor["experience"] = 99;
        actor["experienceForNextLevel"] = 100;
        actor["resourceMaterialization"] = CreateResourceMaterialization(("health", 60m));
        await context.WriteJsonAsync(NpcSkillCorePath, new JsonObject { ["UpdateNPCs"] = new JsonArray(actor) });
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        var resourceIssues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.True(resourceIssues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(Environment.NewLine, resourceIssues.Select(issue => $"{issue.Code}: {issue.FilePath}: {issue.Actual}")));
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            Assert.NotNull(await context.Normalizer.BindTo(lease).NormalizeAcceptedMechanicsAsync(backups: null));
        var canonical = (await context.ReadJsonAsync(NpcSkillCorePath))!["UpdateNPCs"]![0]!.DeepClone().AsObject();
        Assert.Equal(NpcSkillOwner, canonical["NPCId"]!.GetValue<string>());
        Assert.False(canonical.ContainsKey("resourceMaterialization"));
        var resourcePaths = new[] { ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath, CanonicalResourceOwnerAuthorityComposer.AuthorityPath };
        var resourceBefore = await context.CaptureBytesAsync(resourcePaths);
        await PrepareNpcSkillGrant(context, includeProgressionAward: true,
            originalCore: new JsonObject { ["NPCsInScene"] = new JsonArray(canonical) }, snapshotTurn: 43);
        Assert.True(JsonNode.DeepEquals(canonical,
            (await context.ReadJsonAsync(NpcSkillCorePath))!["NPCsInScene"]![0]),
            "The next-turn fixture must preserve the genuinely published canonical actor/profile/tuple.");
        Assert.Equal(resourceBefore, await context.CaptureBytesAsync(resourcePaths));
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        var apply = CreateMaterializableApplyCommand("npc");
        apply["source"]!["sourceId"] = GrantedNpcSkillId;
        apply["target"] = new JsonObject { ["kind"] = "npc", ["targetId"] = NpcSkillOwner };
        await context.WriteJsonAsync(EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(apply));
        return backups;
    }

    private static void AssertNpcSelfEffectPublished(FileSystemManager files)
    {
        JsonNode Read(string path) => JsonNode.Parse(File.ReadAllText(files.ResolvePath(path)))!;
        var core = Read(NpcSkillCorePath).AsObject();
        Assert.False(core.ContainsKey("NPCCoreChanges"));
        var actor = Assert.Single(core["NPCsInScene"]!.AsArray())!;
        Assert.Equal(NpcSkillOwner, actor["NPCId"]!.GetValue<string>());
        Assert.Equal(3, actor["level"]!.GetValue<int>());
        Assert.Equal(0, actor["experience"]!.GetValue<int>());
        Assert.Equal(1558, actor["experienceForNextLevel"]!.GetValue<int>());
        var skill = Assert.Single(actor["activeSkills"]!.AsArray())!;
        Assert.Equal(GrantedNpcSkillId, skill["skillId"]!.GetValue<string>());
        Assert.Equal(1, skill["masteryLevel"]!.GetValue<int>());
        Assert.Empty(Read(PendingNpcSkillsPath).AsObject());
        var carrier = Assert.Single(Read(EffectMaterializationTestContext.NpcEffectsPath)["entries"]!.AsArray())!;
        Assert.Equal(NpcSkillOwner, carrier["NPCId"]!.GetValue<string>());
        var effect = Assert.Single(carrier["activeEffects"]!.AsArray())!;
        Assert.Equal(GrantedNpcSkillId, effect["source"]!["sourceId"]!.GetValue<string>());
        Assert.Equal("npc", effect["target"]!["kind"]!.GetValue<string>());
        Assert.Equal(NpcSkillOwner, effect["target"]!["targetId"]!.GetValue<string>());
        var identity = Assert.Single(Read(EffectMaterializationTestContext.IdentityIndexPath)["entries"]!.AsArray())!;
        Assert.Equal(effect["effectId"]!.GetValue<string>(), identity["effectId"]!.GetValue<string>());
        Assert.Equal(NpcSkillOwner, identity["target"]!["targetId"]!.GetValue<string>());
    }
}
