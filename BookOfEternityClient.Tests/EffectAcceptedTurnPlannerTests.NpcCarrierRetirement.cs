using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectAcceptedTurnPlannerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Build_CanonicalNpcWoundCommandsRefuseBeforeAllocation(bool mixed) =>
        AssertNpcWoundCarrierRefused(mixed, nested: false);

    [Fact]
    public void Build_CanonicalNpcNestedWoundsRefuseBeforeAllocation() =>
        AssertNpcWoundCarrierRefused(mixed: true, nested: true);

    private static void AssertNpcWoundCarrierRefused(bool mixed, bool nested)
    {
        var current = new JsonObject
        {
            ["schemaVersion"] = EffectMaterializationContract.SchemaVersion,
            ["entries"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = "npc_test_healer", ["activeEffects"] = new JsonArray(),
                ["_fixtureNote"] = new JsonObject { ["label"] = "known NPC note" }
            })
        };
        var baselineInput = CreateInput(targetKind: "npc",
            carriers: new(null, current.DeepClone().AsObject(), null, null, null, null));
        Assert.Empty(baselineInput.SourceAuthority.Issues);
        Assert.Empty(baselineInput.TargetAuthority.Issues);
        var baselineBefore = SnapshotNpcRetirementInput(baselineInput);
        var baselineFactory = new CountingFactory();
        var baseline = new EffectAcceptedTurnPlanCache(baselineFactory).GetOrBuild(baselineInput, out var baselineReused);
        Assert.False(baselineReused);
        var baselinePlan = Assert.IsType<EffectAcceptedTurnPlan>(baseline.Plan);
        Assert.Empty(baseline.Issues);
        Assert.Single(baselinePlan.ActiveEffects);
        Assert.Equal(1, baselineFactory.EffectCalls);
        Assert.Equal(1, baselineFactory.TransitionCalls);
        Assert.Equal(baselineBefore, SnapshotNpcRetirementInput(baselineInput));

        var root = mixed ? current.DeepClone().AsObject() : new JsonObject();
        if (nested)
            root["entries"]![0]!["wounds"] = new JsonArray();
        else
            root["NPCWoundChanges"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = "npc_test_healer", ["wounds"] = new JsonArray()
            });
        var input = CreateInput(targetKind: "npc",
            carriers: new(null, root, null, null, null, null));
        Assert.Empty(input.SourceAuthority.Issues);
        Assert.Empty(input.TargetAuthority.Issues);
        var before = SnapshotNpcRetirementInput(input);
        var factory = new CountingFactory();
        var result = new EffectAcceptedTurnPlanCache(factory).GetOrBuild(input, out var reused);
        Assert.False(reused);
        Assert.Equal(before, SnapshotNpcRetirementInput(input));

        var errorPath = EffectCarrierCatalog.NpcPath + (nested ? ".entries[0]" : string.Empty);
        Assert.True(result.Issues.Any(issue => issue.Severity == IssueSeverity.Error &&
            issue.Code == "effect_materialization_legacy_carrier_unsupported" && issue.FilePath == errorPath),
            $"Missing canonical carrier refusal; effect allocations={factory.EffectCalls}, transition allocations={factory.TransitionCalls}.");
        // These zero-allocation/failure oracles are UNREACHED if the baseline misses the required error.
        Assert.False(result.Success);
        Assert.Null(result.Plan);
        Assert.Equal(0, factory.EffectCalls);
        Assert.Equal(0, factory.TransitionCalls);
    }

    private static string SnapshotNpcRetirementInput(EffectAcceptedTurnInput input) => JsonSerializer.Serialize(new
    {
        input.SessionId, input.SnapshotToken, input.Realm,
        Commands = input.RawCommands.ToJsonString(), Events = input.EventInput.ToJsonString(),
        Source = input.SourceAuthority.Fingerprint, Target = input.TargetAuthority.Fingerprint,
        input.PreTurnCarriers, input.PreTurnIdentityIndex, input.TargetAuthorityInput,
        input.PublicationCarrierBaselines, input.PreallocatedCombatantIdentities,
        input.AcceptedCarrierBaselines, SkillScope = input.SkillScopeAuthority?.Fingerprint,
        WoundLocations = input.WoundApplicationLocations?.Fingerprint
    });
}
