using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectMaterializationValidationTests
{
    private const string PendingNpcSkillsPath = "game_state/npcs/npc_skills.json";
    private const string NpcSkillCorePath = "game_state/npcs/npc_core.json";
    private const string NpcSkillOwner = "npc_test_healer";
    private const string GrantedNpcSkillId = "skill_npc_same_turn_source";

    [Fact]
    public async Task RawNpcSkillGrant_BindsSameTurnSourceAndCurrentScopeWithoutOfferingIt()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await PrepareNpcSkillGrant(context);
        var before = await context.CaptureBytesAsync(NpcSkillCorePath, PendingNpcSkillsPath,
            EffectMaterializationTestContext.CommandPath, EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath);

        var issues = await context.Validator.ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var handoff = await AcceptedMechanicsAuthorityTestProbe.PeekEffectAsync(context.FileSystem);
        Assert.NotNull(handoff);
        var plan = Assert.IsType<EffectAcceptedTurnPlan>(handoff!.Result.Plan);
        Assert.Equal(GrantedNpcSkillId, Assert.Single(plan.ActiveEffects)["source"]!["sourceId"]!.GetValue<string>());
        var scope = Assert.IsType<EffectRollSkillScopeAuthority>(plan.SkillScopeAuthority);
        var owner = new EffectTargetKey("mortal_world", "npc", NpcSkillOwner);
        Assert.True(scope.ResolveCurrent(owner, GrantedNpcSkillId, "test.current").IsUsable);
        Assert.False(scope.ResolveForNewBinding(owner, GrantedNpcSkillId, "test.offered").IsUsable);
        Assert.Equal(before, await context.CaptureBytesAsync(before.Keys.ToArray()));
    }

    [Fact]
    public async Task RawNpcSkillRemoval_RevokesExistingFocusedScopeBeforePublication()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string focused = "skill_npc_focused";
        await context.SeedPlayerSkillSourceAsync(EffectSkillScopeLifecycleTests.CreateFocusedDefinition(focused));
        var sourceRoot = (await context.ReadJsonAsync(EffectMaterializationTestContext.MaterializableSkillPath))!.AsObject();
        var focusedSkill = sourceRoot["activeSkillChanges"]![0]!.DeepClone().AsObject();
        focusedSkill["skillId"] = focused;
        focusedSkill["skillName"] = "Focused NPC skill";
        focusedSkill["activeEffectDefinitions"] = new JsonArray();
        AddNpcMastery(focusedSkill);
        var core = MortalActorTestFixtures.CreateNpcCoreRoot(NpcSkillOwner);
        core["NPCsInScene"]![0]!["activeSkills"] = new JsonArray(focusedSkill);
        await context.WriteJsonAsync(NpcSkillCorePath, core);
        await context.WriteJsonAsync(PendingNpcSkillsPath, EmptyNpcSkillCommands());
        await context.CaptureValidatedPendingSnapshotAsync();
        var apply = CreateMaterializableApplyCommand("npc");
        // Focused roll-modifier definition has no amount parameter. Match the existing
        // CreateFocusedSkillApplyCommand shape while retaining this fixture's source ID.
        apply["parameters"] = new JsonObject();
        await context.WriteJsonAsync(EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(apply));
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnRawEffectMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        Assert.NotNull(await AcceptedMechanicsAuthorityTestProbe.PeekEffectAsync(context.FileSystem));

        var pending = EmptyNpcSkillCommands();
        pending["NPCActiveSkillChanges"] = new JsonArray(new JsonObject
        {
            ["NPCId"] = NpcSkillOwner, ["skillsToRemove"] = new JsonArray("Focused NPC skill")
        });
        await context.WriteJsonAsync(PendingNpcSkillsPath, pending);
        var before = await context.CaptureBytesAsync(NpcSkillCorePath, PendingNpcSkillsPath,
            EffectMaterializationTestContext.CommandPath, EffectMaterializationTestContext.NpcEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath);

        var issues = await context.Validator.ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue => issue.Code == "effect_roll_skill_scope_unavailable");
        Assert.Null(await AcceptedMechanicsAuthorityTestProbe.PeekEffectAsync(context.FileSystem));
        Assert.Equal(before, await context.CaptureBytesAsync(before.Keys.ToArray()));
    }

    [Fact]
    public async Task RawNpcSkillSidecarMutation_ChangesPreviouslyValidatedPlanFingerprint()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await PrepareNpcSkillGrant(context);
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnRawEffectMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        var first = await AcceptedMechanicsAuthorityTestProbe.PeekEffectAsync(context.FileSystem);
        Assert.NotNull(first);
        var firstPlan = Assert.IsType<EffectAcceptedTurnPlan>(first!.Result.Plan);
        var pending = (await context.ReadJsonAsync(PendingNpcSkillsPath))!.AsObject();
        pending["NPCSkillMasteryChanges"] = new JsonArray(new JsonObject
        {
            ["NPCId"] = NpcSkillOwner, ["skillName"] = "Кровавый след", ["skillId"] = GrantedNpcSkillId,
            ["newMasteryLevel"] = 2, ["newCurrentMasteryProgress"] = 0, ["newMasteryProgressNeeded"] = 200
        });
        await context.WriteJsonAsync(PendingNpcSkillsPath, pending);
        var before = await context.CaptureBytesAsync(NpcSkillCorePath, PendingNpcSkillsPath,
            EffectMaterializationTestContext.CommandPath, EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath);

        var issues = await context.Validator.ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var changed = await AcceptedMechanicsAuthorityTestProbe.PeekEffectAsync(context.FileSystem);
        Assert.NotNull(changed);
        var changedPlan = Assert.IsType<EffectAcceptedTurnPlan>(changed!.Result.Plan);
        Assert.NotEqual(firstPlan.InputFingerprint, changedPlan.InputFingerprint);
        Assert.Equal(before, await context.CaptureBytesAsync(before.Keys.ToArray()));
    }

    private static async Task PrepareNpcSkillGrant(EffectMaterializationTestContext context)
    {
        // Reuse the production-supported complete effect-source skill fixture, then move
        // its detached profile into a pending NPC grant, not another canonical source.
        await context.SeedPlayerSkillSourceAsync(skillId: GrantedNpcSkillId);
        var seeded = (await context.ReadJsonAsync(EffectMaterializationTestContext.MaterializableSkillPath))!.AsObject();
        var skill = seeded["activeSkillChanges"]![0]!.DeepClone().AsObject();
        AddNpcMastery(skill);
        await context.WriteJsonAsync(EffectMaterializationTestContext.MaterializableSkillPath,
            new JsonObject { ["activeSkillChanges"] = new JsonArray(), ["removeActiveSkills"] = new JsonArray() });
        await context.WriteJsonAsync(NpcSkillCorePath, MortalActorTestFixtures.CreateNpcCoreRoot(NpcSkillOwner));
        await context.WriteJsonAsync(PendingNpcSkillsPath, EmptyNpcSkillCommands());
        await context.CaptureValidatedPendingSnapshotAsync();
        var pending = EmptyNpcSkillCommands();
        pending["NPCActiveSkillChanges"] = new JsonArray(new JsonObject
        {
            ["NPCId"] = NpcSkillOwner, ["skillChanges"] = new JsonArray(skill)
        });
        await context.WriteJsonAsync(PendingNpcSkillsPath, pending);
        var apply = CreateMaterializableApplyCommand();
        apply["source"]!["sourceId"] = GrantedNpcSkillId;
        await context.WriteJsonAsync(EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(apply));
    }

    private static void AddNpcMastery(JsonObject skill)
    {
        skill["masteryLevel"] = 1;
        skill["currentMasteryLevel"] = 1;
        skill["currentMasteryProgress"] = 0;
        skill["masteryProgressNeeded"] = 100;
    }

    private static JsonObject EmptyNpcSkillCommands() => new()
    {
        ["NPCActiveSkillChanges"] = new JsonArray(), ["NPCPassiveSkillChanges"] = new JsonArray(),
        ["NPCSkillMasteryChanges"] = new JsonArray(), ["NPCPassiveSkillMasteryChanges"] = new JsonArray()
    };
}
