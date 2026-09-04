using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class EffectIdentityStateWoundTests
{
    [Fact]
    public void Parse_IndexesExactWoundSourceGroupCoordinateAndFirstCreateChildren()
    {
        const string woundId = "wound_lineage_index";
        const string rootId = "effect_wound_lineage_root";
        const string childId = "effect_wound_lineage_child";
        var root = CreateWoundEffect(rootId, woundId, "definition_lineage_root");
        var child = CreateWoundEffect(childId, woundId, "definition_lineage_child");
        var unrelated = CreateWoundEffect(
            "effect_wound_lineage_unrelated",
            "wound_lineage_other",
            "definition_lineage_other");
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(
            root,
            child,
            unrelated);
        var entries = index["entries"]!.AsArray().OfType<JsonObject>().ToArray();
        for (var entryIndex = 0; entryIndex < entries.Length; entryIndex++)
        {
            var create = entries[entryIndex]["transitions"]![0]!.AsObject();
            create["transitionId"] = $"effect_transition_lineage_{entryIndex}";
            create["eventRef"] = $"turn_42:lineage:{entryIndex}";
        }
        entries[1]["transitions"]![0]!["sourceEffectIds"] =
            new JsonArray(rootId);
        entries[0]["state"] = "removed";
        entries[0]["transitions"]!.AsArray().Add(new JsonObject
        {
            ["transitionId"] = "effect_transition_lineage_root_remove",
            ["kind"] = "remove",
            ["turn"] = 43,
            ["eventRef"] = "turn_43:lineage:root_remove",
            ["sourceEffectIds"] = new JsonArray(rootId),
            ["resultEffectIds"] = new JsonArray(),
            ["receiptId"] = null
        });

        using var document = JsonDocument.Parse(index.ToJsonString());
        var parsed = EffectIdentityState.Parse(
            document.RootElement,
            EffectIdentityState.StatePath);

        Assert.Empty(parsed.Issues);
        var state = Assert.IsType<EffectIdentityState>(parsed.State);
        Assert.Equal(
            new[] { childId, rootId },
            state.ResolveSourceGroup(new EffectIdentitySourceGroup(
                    "mortal_world",
                    "wound",
                    woundId))
                .Select(static entry => entry.EffectId));
        Assert.Equal(
            childId,
            Assert.Single(state.ResolveSourceCoordinate(
                new EffectIdentitySourceCoordinate(
                    "mortal_world",
                    "wound",
                    woundId,
                    "definition_lineage_child"))).EffectId);
        Assert.Equal(
            childId,
            Assert.Single(state.ResolveFirstCreateChildren(rootId)).EffectId);
        Assert.Empty(state.ResolveFirstCreateChildren(childId));
    }

    [Fact]
    public void Parse_IndexesRetiredAndCurrentSeverityGenerationsAtOneExactCoordinate()
    {
        var fixture = WoundEffectBatchPlannerTests.BuildRetainedWorsenPureFixture();
        var retainedRoot = Assert.Single(
            Assert.Single(fixture.Prepared.EffectOperationBatches).RootApplications,
            static root => root.PriorRootEffectId is not null);
        var current = Assert.Single(fixture.Accepted.ApplicationResults, result =>
            string.Equals(
                result.ApplicationRef,
                retainedRoot.ApplicationRef,
                StringComparison.Ordinal));
        using var document = JsonDocument.Parse(
            fixture.Accepted.EffectPlan.IdentityIndexAfterImage.ToJsonString());
        var parsed = EffectIdentityState.Parse(
            document.RootElement,
            EffectIdentityState.StatePath);

        Assert.Empty(parsed.Issues);
        var state = Assert.IsType<EffectIdentityState>(parsed.State);
        Assert.Equal(
            current.EffectId,
            Assert.Single(state.ResolveFirstCreateChildren(
                retainedRoot.PriorRootEffectId!)).EffectId);
        Assert.Equal(
            new[] { retainedRoot.PriorRootEffectId, current.EffectId }
                .OrderBy(static value => value, StringComparer.Ordinal),
            state.ResolveSourceCoordinate(new EffectIdentitySourceCoordinate(
                    fixture.Prepared.Binding.Realm,
                    "wound",
                    fixture.Prepared.PreparedWounds[0].WoundId,
                    retainedRoot.DefinitionKey))
                .Select(static entry => entry.EffectId));
    }

    private static JsonObject CreateWoundEffect(
        string effectId,
        string woundId,
        string definitionKey)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["effectId"] = effectId;
        effect["source"] = new JsonObject
        {
            ["kind"] = "wound",
            ["sourceId"] = woundId,
            ["definitionKey"] = definitionKey
        };
        effect["stacking"]!["stackKey"] = "stack_" + definitionKey;
        return effect;
    }
}
