using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectMaterializationValidationTests
{
    [Fact]
    public async Task OrdinaryNpcProgressionAndSkillGrant_CommonEffectPublishesCompletedCandidate()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var backups = await PrepareNpcSkillCommonPublicationAsync(context, includeProgressionAward: true);
        var sourcePaths = backups.Values.Concat(new[]
        {
            "game_state/control/pending_turn_snapshot.json", PendingTurnSnapshotAuthority.AuthorityPath,
            "input/turn_request.json"
        }).Distinct(StringComparer.Ordinal).ToArray();
        var sourceBefore = await context.CaptureBytesAsync(sourcePaths);
        var rawIssues = await context.Validator.ValidateNpcCoreChangesBeforeNormalizationAsync();
        Assert.True(rawIssues.Count == 0, string.Join(Environment.NewLine,
            rawIssues.Select(issue => $"{issue.Code}: {issue.FilePath}: {issue.Message}")));
        // Unlike the skill-only control, this enters NPC core command validation again
        // inside effect owner export and must reuse the completed candidate there.
        var admitted = await AdmitNpcSkillCommonPublicationAsync(context);
        var admittedActor = admitted.EffectPlan!.NpcSkillInput!.CandidateCore["NPCsInScene"]![0]!;
        Assert.Equal(3, admittedActor["level"]!.GetValue<int>());
        Assert.Equal(GrantedNpcSkillId, Assert.Single(admittedActor["activeSkills"]!.AsArray())!["skillId"]!.GetValue<string>());

        var result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, backups);

        Assert.DoesNotContain(result.Issues, issue => issue.Severity == IssueSeverity.Error);
        var published = Assert.IsType<AcceptedMechanicsPlan>(result.MechanicsPlan);
        Assert.Equal(admitted.InputFingerprint, published.InputFingerprint);
        var core = (await context.ReadJsonAsync(NpcSkillCorePath))!.AsObject();
        Assert.False(core.ContainsKey("NPCCoreChanges"));
        var actor = Assert.Single(core["NPCsInScene"]!.AsArray())!;
        Assert.Equal(3, actor["level"]!.GetValue<int>());
        Assert.Equal(0, actor["experience"]!.GetValue<int>());
        Assert.Equal(1558, actor["experienceForNextLevel"]!.GetValue<int>());
        var skill = Assert.Single(actor["activeSkills"]!.AsArray())!;
        Assert.Equal(GrantedNpcSkillId, skill["skillId"]!.GetValue<string>());
        Assert.Equal(1, skill["masteryLevel"]!.GetValue<int>());
        Assert.Empty((await context.ReadJsonAsync(PendingNpcSkillsPath))!.AsObject());
        Assert.Null(await context.ReadJsonAsync(EffectMaterializationTestContext.CommandPath));
        var effects = (await context.ReadJsonAsync(EffectMaterializationTestContext.PlayerEffectsPath))!["activeEffects"]!.AsArray();
        var effect = Assert.Single(effects)!;
        Assert.Equal(GrantedNpcSkillId, effect["source"]!["sourceId"]!.GetValue<string>());
        var index = (await context.ReadJsonAsync(EffectMaterializationTestContext.IdentityIndexPath))!["entries"]!.AsArray();
        Assert.Equal(effect["effectId"]!.GetValue<string>(), Assert.Single(index)!["effectId"]!.GetValue<string>());
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnCanonicalEffectMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        Assert.Equal(sourceBefore, await context.CaptureBytesAsync(sourcePaths));
        Assert.Null(await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(context.FileSystem));
    }

    [Fact]
    public async Task OrdinaryNpcSkillGrant_SidecarDriftAfterCommonAdmissionRefusesPublicationWithoutMutation()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var backups = await PrepareNpcSkillCommonPublicationAsync(context);
        var admitted = await AdmitNpcSkillCommonPublicationAsync(context);
        var admittedSidecar = admitted.EffectPlan!.NpcSkillInput!.RawSidecar;
        var changed = (await context.ReadJsonAsync(PendingNpcSkillsPath))!.AsObject();
        changed["NPCActiveSkillChanges"]![0]!["skillChanges"]![0]!["skillDescription"] =
            "Changed valid skill description after common admission";
        Assert.False(JsonNode.DeepEquals(admittedSidecar, changed));
        await context.WriteJsonAsync(PendingNpcSkillsPath, changed);
        var paths = CanonicalStateNormalizer.NormalizerRollbackTrackedFiles.Concat(new[]
        {
            NpcSkillCorePath, PendingNpcSkillsPath, "output/narrative_response.json",
            "output/interface_updates.json", "output/debug_logs.json"
        }).Distinct(StringComparer.Ordinal).ToArray();
        var before = await context.CaptureBytesAsync(paths);
        AcceptedTurnCanonicalStateRefresh.Result? result = null;

        var failure = await Record.ExceptionAsync(async () =>
        {
            result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                context.FileSystem, context.Normalizer, context.Validator, backups);
        });

        Assert.True(failure is InvalidDataException || result?.Issues.Any(issue => issue.Severity == IssueSeverity.Error) == true,
            "A valid but changed raw sidecar must not publish through its previously admitted common plan. " + failure);
        Assert.Equal(before, await context.CaptureBytesAsync(paths));
        Assert.Equal(changed.ToJsonString(), (await context.ReadJsonAsync(PendingNpcSkillsPath))!.ToJsonString());
    }

    [Fact]
    public async Task OrdinaryNpcSkillGrant_Utf16CommonWitnessPublishesWithoutChangingSnapshotEncoding()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var backups = await PrepareNpcSkillCommonPublicationAsync(context);
        var snapshotBefore = await context.CaptureBytesAsync(backups.Values
            .Append("game_state/control/pending_turn_snapshot.json").ToArray());
        foreach (var path in new[] { NpcSkillCorePath, PendingNpcSkillsPath })
        {
            var text = (await context.FileSystem.ReadFileAsync(path))!;
            var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(text)).ToArray();
            await context.FileSystem.WriteFileAtomicBytesAsync(path, bytes);
        }
        var admitted = await AdmitNpcSkillCommonPublicationAsync(context);
        var witnesses = admitted.BeforeImages;
        Assert.Equal(new byte[] { 0xff, 0xfe }, witnesses[NpcSkillCorePath].Bytes!.Take(2).ToArray());
        Assert.Equal(new byte[] { 0xff, 0xfe }, witnesses[PendingNpcSkillsPath].Bytes!.Take(2).ToArray());

        var result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, backups);

        Assert.DoesNotContain(result.Issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.NotNull(result.MechanicsPlan);
        var actor = (await context.ReadJsonAsync(NpcSkillCorePath))!["NPCsInScene"]![0]!;
        Assert.Equal(GrantedNpcSkillId, Assert.Single(actor["activeSkills"]!.AsArray())!["skillId"]!.GetValue<string>());
        Assert.Empty((await context.ReadJsonAsync(PendingNpcSkillsPath))!.AsObject());
        Assert.Null(await context.ReadJsonAsync(EffectMaterializationTestContext.CommandPath));
        var carrier = (await context.ReadJsonAsync(EffectMaterializationTestContext.PlayerEffectsPath))!;
        Assert.Equal(GrantedNpcSkillId, Assert.Single(carrier["activeEffects"]!.AsArray())!["source"]!["sourceId"]!.GetValue<string>());
        Assert.Equal(snapshotBefore, await context.CaptureBytesAsync(snapshotBefore.Keys.ToArray()));
    }

    [Fact]
    public async Task OrdinaryNpcSkillGrant_ValidLocationUpdateRefusesUnsupportedCommonCompositionWithoutMutation()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var location = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            MortalActorTestFixtures.DefaultLocationId, MortalActorTestFixtures.DefaultLocationName);
        var map = MortalLocationTestFixture.CreateWorldMap(location);
        await context.WriteJsonAsync(MortalLocationMaterializationContract.WorldMapPath, map);
        await context.WriteJsonAsync(MortalLocationMaterializationContract.CurrentLocationPath,
            MortalLocationTestFixture.CreateCurrentProjection(location));
        await context.WriteJsonAsync(MortalLocationIdentityState.StatePath,
            MortalLocationTestFixture.CreateIdentityIndex(location));
        var backups = await PrepareNpcSkillCommonPublicationAsync(context);
        map["worldMapUpdates"] = new JsonObject
        {
            ["locationUpdates"] = new JsonArray(new JsonObject
            {
                ["locationId"] = MortalActorTestFixtures.DefaultLocationId,
                ["displayName"] = "Changed location alongside NPC skill grant"
            })
        };
        await context.WriteJsonAsync(MortalLocationMaterializationContract.WorldMapPath, map);
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        await AdmitNpcSkillCommonPublicationAsync(context);
        var before = await context.CaptureBytesAsync(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);

        var failure = await Assert.ThrowsAsync<InvalidDataException>(() =>
            AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                context.FileSystem, context.Normalizer, context.Validator, backups));

        Assert.Equal(
            "Ordinary NPC skill common publication does not yet support changed item/location projection inputs: " +
            MortalLocationMaterializationContract.CurrentLocationPath,
            failure.Message);
        Assert.Equal(before, await context.CaptureBytesAsync(before.Keys.ToArray()));
    }

    [Fact]
    public async Task OrdinaryNpcSkillGrant_CommonEffectPublishesCatalogAndConsumesBothCommands()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var backups = await PrepareNpcSkillCommonPublicationAsync(context);
        var admitted = await AdmitNpcSkillCommonPublicationAsync(context);

        var result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, backups);

        Assert.DoesNotContain(result.Issues, issue => issue.Severity == IssueSeverity.Error);
        var published = Assert.IsType<AcceptedMechanicsPlan>(result.MechanicsPlan);
        Assert.Equal(admitted.InputFingerprint, published.InputFingerprint);
        Assert.False(published.AwaitsPendingResolution);
        var core = (await context.ReadJsonAsync(NpcSkillCorePath))!.AsObject();
        var actor = Assert.IsType<JsonObject>(Assert.Single(core["NPCsInScene"]!.AsArray()));
        Assert.Equal(NpcSkillOwner, actor["NPCId"]!.GetValue<string>());
        var skill = Assert.IsType<JsonObject>(Assert.Single(actor["activeSkills"]!.AsArray()));
        Assert.Equal(GrantedNpcSkillId, skill["skillId"]!.GetValue<string>());
        Assert.Equal(1, skill["masteryLevel"]!.GetValue<int>());
        Assert.Equal(0, skill["currentMasteryProgress"]!.GetValue<int>());
        Assert.Equal(100, skill["masteryProgressNeeded"]!.GetValue<int>());
        Assert.Empty((await context.ReadJsonAsync(PendingNpcSkillsPath))!.AsObject());
        Assert.Null(await context.ReadJsonAsync(EffectMaterializationTestContext.CommandPath));

        var carrier = (await context.ReadJsonAsync(EffectMaterializationTestContext.PlayerEffectsPath))!.AsObject();
        var effect = Assert.IsType<JsonObject>(Assert.Single(carrier["activeEffects"]!.AsArray()));
        Assert.Equal(GrantedNpcSkillId, effect["source"]!["sourceId"]!.GetValue<string>());
        var index = (await context.ReadJsonAsync(EffectMaterializationTestContext.IdentityIndexPath))!.AsObject();
        var entry = Assert.IsType<JsonObject>(Assert.Single(index["entries"]!.AsArray()));
        Assert.Equal(effect["effectId"]!.GetValue<string>(), entry["effectId"]!.GetValue<string>());
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnCanonicalEffectMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        Assert.Null(await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(context.FileSystem));
    }

    [Fact]
    public async Task OrdinaryNpcSkillGrant_CommonEffectLateCutRestoresCatalogSidecarAndEffectOutputs()
    {
        var armed = false;
        var cutReached = false;
        var materializedSkillAtCut = false;
        var consumedSidecarAtCut = false;
        var materializedEffectAtCut = false;
        FileSystemManager? files = null;
        const string failureMarker = "ordinary NPC skill common effect late cut";
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationBoundaryAsync = path =>
            {
                if (!armed || cutReached || path != EffectMaterializationTestContext.CommandPath)
                    return Task.CompletedTask;
                cutReached = true;
                // This ordinary common writer consumes effect commands after its after-images.
                // Observe actual disk state so an early admission refusal cannot satisfy rollback.
                var core = JsonNode.Parse(File.ReadAllText(files!.ResolvePath(NpcSkillCorePath)))!.AsObject();
                materializedSkillAtCut = core["NPCsInScene"]![0]!["activeSkills"]!.AsArray()
                    .OfType<JsonObject>().Any(skill => skill["skillId"]?.GetValue<string>() == GrantedNpcSkillId);
                consumedSidecarAtCut = JsonNode.Parse(File.ReadAllText(files.ResolvePath(PendingNpcSkillsPath)))!
                    .AsObject().Count == 0;
                var effectPath = files.ResolvePath(EffectMaterializationTestContext.PlayerEffectsPath);
                materializedEffectAtCut = File.Exists(effectPath) &&
                    JsonNode.Parse(File.ReadAllText(effectPath))?["activeEffects"] is JsonArray effects &&
                    effects.OfType<JsonObject>().Any(effect =>
                        effect["source"]?["sourceId"]?.GetValue<string>() == GrantedNpcSkillId);
                throw new InvalidOperationException(failureMarker);
            }
        };
        await using var context = await EffectMaterializationTestContext.CreateAsync(hooks);
        files = context.FileSystem;
        var backups = await PrepareNpcSkillCommonPublicationAsync(context);
        await AdmitNpcSkillCommonPublicationAsync(context);
        var paths = CanonicalStateNormalizer.NormalizerRollbackTrackedFiles
            .Concat(new[] { NpcSkillCorePath, PendingNpcSkillsPath,
                "output/narrative_response.json", "output/interface_updates.json", "output/debug_logs.json" })
            .Distinct(StringComparer.Ordinal).ToArray();
        var before = await context.CaptureBytesAsync(paths);

        armed = true;
        var failure = await Record.ExceptionAsync(() =>
            AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                context.FileSystem, context.Normalizer, context.Validator, backups));
        armed = false;

        Assert.True(cutReached, "The real common writer must reach effect-command consumption.");
        Assert.True(materializedSkillAtCut, "NPC skill must have reached canonical core before the late cut.");
        Assert.True(consumedSidecarAtCut, "NPC sidecar must have been consumed before the late cut.");
        Assert.True(materializedEffectAtCut, "The common effect after-image must have reached disk before the late cut.");
        Assert.NotNull(failure);
        Assert.Contains(failureMarker, failure!.ToString(), StringComparison.Ordinal);
        Assert.Equal(before, await context.CaptureBytesAsync(paths));
    }

    private static async Task<IReadOnlyDictionary<string, string>> PrepareNpcSkillCommonPublicationAsync(
        EffectMaterializationTestContext context, bool includeProgressionAward = false)
    {
        // Existing source fixture, real signed snapshot and empty ordinary item owner.
        // No item/location commands, treatment envelopes or constructed plans are involved.
        await context.WriteJsonAsync(MortalItemIdentityState.StatePath, MortalItemIdentityState.CreateEmptyRoot());
        await PrepareNpcSkillGrant(context, includeProgressionAward: includeProgressionAward);
        return await context.ReadPendingSnapshotBackupsAsync();
    }

    private static async Task<AcceptedMechanicsPlan> AdmitNpcSkillCommonPublicationAsync(
        EffectMaterializationTestContext context)
    {
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        var handoff = await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(context.FileSystem);
        Assert.NotNull(handoff);
        var plan = Assert.IsType<AcceptedMechanicsPlan>(handoff!.Result.Plan);
        Assert.False(plan.AwaitsPendingResolution);
        var effect = Assert.IsType<EffectAcceptedTurnPlan>(plan.EffectPlan);
        Assert.NotNull(effect.NpcSkillInput);
        Assert.Equal(GrantedNpcSkillId, Assert.Single(effect.ActiveEffects)["source"]!["sourceId"]!.GetValue<string>());
        var scope = Assert.IsType<EffectRollSkillScopeAuthority>(effect.SkillScopeAuthority);
        var owner = new EffectTargetKey("mortal_world", "npc", NpcSkillOwner);
        Assert.True(scope.ResolveCurrent(owner, GrantedNpcSkillId, "test.current").IsUsable);
        Assert.False(scope.ResolveForNewBinding(owner, GrantedNpcSkillId, "test.offered").IsUsable);
        return plan;
    }
}
