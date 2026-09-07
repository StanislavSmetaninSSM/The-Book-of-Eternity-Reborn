using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectMechanicsSnapshotTests
{
    public static IEnumerable<object[]> AfterlifeSpiritualAxes =>
        AfterlifeSpiritualConflictState.CombatConditionMechanicalAxes
            .OrderBy(static axis => axis, StringComparer.Ordinal)
            .Select(static axis => new object[] { axis });

    [Theory]
    [MemberData(nameof(AfterlifeSpiritualAxes))]
    public void Build_AfterlifeConditionProjectsOnlyRegisteredSpiritualAxis(
        string axis)
    {
        var (root, effect) = CreateAfterlifeConditionCarrier(
            axis,
            visibility: "visible");

        var snapshot = EffectMechanicsSnapshot.Build(
            CreateAfterlifeInput(root, effect));

        Assert.True(snapshot.IsAccepted, Describe(snapshot.Issues));
        Assert.Empty(snapshot.Issues);
        var component = Assert.Single(snapshot.Components);
        Assert.Equal("afterlife_combat_condition", component.Profile);
        Assert.Equal("shining_abode", component.Realm);
        Assert.Equal("spiritual_conflict_side", component.TargetKind);
        Assert.Equal("conflict_snapshot_afterlife:opposition", component.TargetId);
        Assert.Equal(
            [axis],
            component.Payload.GetProperty("axes")
                .EnumerateArray()
                .Select(static value => value.GetString()!)
                .ToArray());
        Assert.DoesNotContain(
            snapshot.Components,
            candidate => string.Equals(
                candidate.Profile,
                "characteristic_modifier",
                StringComparison.Ordinal));
        Assert.Single(snapshot.Audit);
    }

    [Fact]
    public void Build_AfterlifeConditionRejectsAxisOutsideRegisteredSpiritualSet()
    {
        var (root, effect) = CreateAfterlifeConditionCarrier(
            "strength",
            visibility: "visible");

        var snapshot = EffectMechanicsSnapshot.Build(
            CreateAfterlifeInput(root, effect));

        Assert.False(snapshot.IsAccepted);
        Assert.Contains(
            snapshot.Issues,
            issue => string.Equals(
                issue.Code,
                "effect_materialization_invalid_component",
                StringComparison.Ordinal));
        Assert.Empty(snapshot.Components);
        Assert.Empty(snapshot.Audit);
    }

    [Theory]
    [InlineData("hidden")]
    [InlineData("gm_only")]
    public void Build_NonVisibleAfterlifeConditionExecutesWithoutPlayerAuditLeak(
        string visibility)
    {
        var (root, effect) = CreateAfterlifeConditionCarrier(
            "rollMode",
            visibility);

        var snapshot = EffectMechanicsSnapshot.Build(
            CreateAfterlifeInput(root, effect));

        Assert.True(snapshot.IsAccepted, Describe(snapshot.Issues));
        var component = Assert.Single(snapshot.Components);
        Assert.False(component.IsPlayerVisible);
        Assert.Equal("Скрытый эффект", component.EffectName);
        Assert.Equal(string.Empty, component.EffectDescription);
        Assert.Empty(snapshot.Audit);
        var playerAuditJson = new JsonArray(
            snapshot.Audit
                .Select(static entry => (JsonNode)new JsonObject
                {
                    ["displayName"] = entry.DisplayName,
                    ["category"] = entry.Category,
                    ["targetKind"] = entry.TargetKind,
                    ["state"] = entry.State,
                    ["profiles"] = new JsonArray(
                        entry.Profiles.Select(static profile =>
                            (JsonNode)JsonValue.Create(profile)!).ToArray())
                })
                .ToArray())
            .ToJsonString();
        Assert.DoesNotContain(
            EffectMaterializationTestFixture.EffectId,
            playerAuditJson,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Тайная печать",
            playerAuditJson,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SpiritualConflictPreview_ProjectsAcceptedConditionMechanicsWithoutMortalStats()
    {
        var (root, effect) = CreateAfterlifeConditionCarrier(
            "actionCostAudit.opposition",
            visibility: "gm_only");
        var snapshot = EffectMechanicsSnapshot.Build(
            CreateAfterlifeInput(root, effect));
        Assert.True(snapshot.IsAccepted, Describe(snapshot.Issues));
        var activeConflict = Assert.IsType<JsonObject>(root["activeConflict"]);

        var projection = AfterlifeSpiritualConflictTurnPreviewService
            .BuildConditionMechanics(snapshot, activeConflict);

        Assert.Equal(
            EffectMechanicsSnapshot.Source,
            projection["source"]!.GetValue<string>());
        var contribution = Assert.IsType<JsonObject>(
            Assert.Single(projection["contributions"]!.AsArray()));
        Assert.Equal(
            EffectMaterializationTestFixture.EffectId,
            contribution["conditionId"]!.GetValue<string>());
        Assert.Equal(
            "opposition",
            contribution["targetSide"]!.GetValue<string>());
        Assert.Equal(
            "guardian_snapshot_afterlife",
            contribution["targetActorId"]!.GetValue<string>());
        Assert.Equal(
            ["actionCostAudit.opposition"],
            contribution["mechanicalAxes"]!.AsArray()
                .Select(static axis => axis!.GetValue<string>())
                .ToArray());
        Assert.Equal(1, contribution["currentStacks"]!.GetValue<int>());
        Assert.False(contribution.ContainsKey("characteristics"));
        Assert.False(contribution.ContainsKey("statModifiers"));
    }

    private static EffectMechanicsInput CreateAfterlifeInput(
        JsonObject spiritualConflict,
        JsonObject effect) =>
        new(
            new EffectCarrierCatalogInput(
                null,
                null,
                null,
                null,
                null,
                spiritualConflict),
            EffectMaterializationTestFixture.CreateIdentityIndex(effect));

    private static (JsonObject Root, JsonObject Effect)
        CreateAfterlifeConditionCarrier(
            string axis,
            string visibility)
    {
        const string conflictId = "conflict_snapshot_afterlife";
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "afterlife_combat_condition");
        effect["realm"] = "shining_abode";
        effect["target"] = new JsonObject
        {
            ["kind"] = "spiritual_conflict_side",
            ["targetId"] = conflictId + ":opposition"
        };
        effect["display"]!["name"] = "Тайная печать";
        effect["display"]!["description"] =
            "Духовная печать меняет исход точного обмена.";
        effect["display"]!["category"] = "condition";
        effect["display"]!["visibility"] = visibility;
        effect["source"] = new JsonObject
        {
            ["kind"] = "spiritual_art",
            ["sourceId"] = "art_snapshot_afterlife",
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        effect["components"] = new JsonArray(new JsonObject
        {
            ["componentId"] = "component_001",
            ["profile"] = "afterlife_combat_condition",
            ["priority"] = 100,
            ["payload"] = new JsonObject
            {
                ["conditionKind"] = "burden",
                ["targetSide"] = "opposition",
                ["actorId"] = "guardian_snapshot_afterlife",
                ["operations"] = new JsonArray("pressure"),
                ["axes"] = new JsonArray(axis),
                ["counterplay"] = new JsonArray(
                    "Ответить действием guard или counter."),
                ["payoff"] = "impose_disadvantage"
            }
        });
        effect["lifetime"] = new JsonObject
        {
            ["mode"] = "uses",
            ["remainingUses"] = 2,
            ["consumingTriggerIds"] = new JsonArray("condition_exchange_consumed"),
            ["displayText"] = "Ещё два обмена"
        };
        effect["triggers"] = new JsonArray(new JsonObject
        {
            ["triggerId"] = "condition_exchange_consumed",
            ["eventType"] = "afterlife_exchange_end",
            ["priority"] = 100,
            ["componentIds"] = new JsonArray("component_001"),
            ["consumeUses"] = true,
            ["resolutionMode"] = "deterministic"
        });

        Assert.True(
            AfterlifeSpiritualConflictState.TryProjectCombatCondition(
                effect,
                out var projected,
                out var reason),
            reason);
        foreach (var field in AfterlifeSpiritualConflictState
                     .CombatConditionProjectionFields)
        {
            effect[field] = projected[field]?.DeepClone();
        }

        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["activeConflict"] = new JsonObject
            {
                ["dangerMode"] = "hostile",
                ["conflictId"] = conflictId,
                ["realm"] = "Shining Abode",
                ["sideModel"] = "direct_duel",
                ["status"] = "active",
                ["resolutionState"] = "active",
                ["conflictPosition"] = "contested",
                ["playerSideStrain"] = "clear",
                ["oppositionSideStrain"] = "clear",
                ["playerSide"] = new JsonObject
                {
                    ["leadContestant"] = new JsonObject
                    {
                        ["actorType"] = "player",
                        ["actorId"] = "player_soul",
                        ["displayName"] = "Асуран"
                    },
                    ["supporters"] = new JsonArray()
                },
                ["oppositionSide"] = new JsonObject
                {
                    ["leadContestant"] = new JsonObject
                    {
                        ["actorType"] = "guardian",
                        ["actorId"] = "guardian_snapshot_afterlife",
                        ["displayName"] = "Хранитель печати"
                    },
                    ["supporters"] = new JsonArray()
                },
                ["combatConditions"] = new JsonArray(effect.DeepClone()),
                ["exchangeLog"] = new JsonArray()
            },
            ["recentConflicts"] = new JsonArray()
        };
        return (root, effect);
    }

    private static string Describe(IEnumerable<ValidationIssue> issues) =>
        string.Join(
            Environment.NewLine,
            issues.Select(issue =>
                $"{issue.FilePath}: {issue.Code} {issue.Actual}"));
}
