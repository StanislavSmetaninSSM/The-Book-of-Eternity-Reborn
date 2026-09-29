using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectAcceptedTurnPlanCacheTests
{
    [Fact]
    public void SkillScope_UnchangedAcceptedAmbiguousCatalogDoesNotBlockAllScopeComposition()
    {
        var roots = SkillRoots("duplicate");
        var baseline = CreateSkillScopeInput("none", all: true);
        var composed = EffectAcceptedTurnInputComposer.Compose(baseline.SessionId, baseline.SnapshotToken, 42,
            baseline.RawCommands, baseline.PreTurnCarriers!, baseline.PreTurnCarriers!, null,
            roots, acceptedSourceRoots: roots);
        var input = baseline with { SkillScopeAuthority = composed.SkillScopeAuthority };
        Assert.True(new EffectAcceptedTurnPlanCache().GetOrBuild(input).Success);
    }

    [Fact]
    public void SkillScope_ChangingOnlySelectedSkillChangesInputFingerprint()
    {
        var first = CreateSkillScopeInput("two");
        var changed = CreateSkillScopeInput("two", selectedSkill: "skill_other");
        Assert.Equal(first.SkillScopeAuthority!.Fingerprint, changed.SkillScopeAuthority!.Fingerprint);
        var cache = new EffectAcceptedTurnPlanCache();
        var firstResult = cache.GetOrBuild(first);
        var changedResult = cache.GetOrBuild(changed, out var reused);
        Assert.True(firstResult.Success && changedResult.Success);
        Assert.False(reused);
        Assert.NotEqual(firstResult.Plan!.InputFingerprint, changedResult.Plan!.InputFingerprint);
        Assert.Equal("skill_other", Assert.Single(changedResult.Plan.ActiveEffects)["components"]![0]!["payload"]!["scope"]!["skillId"]!.GetValue<string>());
    }

    [Fact]
    public void SkillScope_TwoSourcesCannotClaimTheSameProposalCoordinate()
    {
        var key = new EffectSourceKey("mortal_world", "wound", "local_wound", "definition");
        var second = key with { DefinitionKey = "second" };
        var location = new EffectApplicationDiagnosticLocation(
            "woundDecisions[0].proposal.consequenceDefinitions[0].definition.components", "wound_materialization");
        var map = new EffectApplicationDiagnosticLocations(new[]
        {
            new KeyValuePair<EffectSourceKey, EffectApplicationDiagnosticLocation>(key, location),
            new KeyValuePair<EffectSourceKey, EffectApplicationDiagnosticLocation>(second, location)
        });
        Assert.False(map.TryResolve(key, out _));
        Assert.False(map.TryResolve(second, out _));
    }

    [Fact]
    public void SkillScope_DiagnosticLocationsAreDetachedAndFingerprintBound()
    {
        var key = new EffectSourceKey("mortal_world", "wound", "local_wound", "definition");
        var rows = new List<KeyValuePair<EffectSourceKey, EffectApplicationDiagnosticLocation>>
        {
            new(key, new("woundDecisions[1].proposal.consequenceDefinitions[0].definition.components", "wound_materialization"))
        };
        var locations = new EffectApplicationDiagnosticLocations(rows);
        var fingerprint = locations.Fingerprint;
        rows.Clear();
        Assert.True(locations.TryResolve(key, out var location));
        Assert.Equal(fingerprint, locations.Fingerprint);
        var baseline = CreateSkillScopeInput("none", all: true) with { WoundApplicationLocations = locations };
        var changed = baseline with
        {
            WoundApplicationLocations = new EffectApplicationDiagnosticLocations(new[]
            {
                new KeyValuePair<EffectSourceKey, EffectApplicationDiagnosticLocation>(key, location with
                {
                    Path = "woundDecisions[2].proposal.consequenceDefinitions[0].definition.components"
                })
            })
        };
        var cache = new EffectAcceptedTurnPlanCache();
        var first = cache.GetOrBuild(baseline);
        var second = cache.GetOrBuild(changed, out var reused);
        Assert.True(first.Success && second.Success);
        Assert.False(reused);
        Assert.NotEqual(first.Plan!.InputFingerprint, second.Plan!.InputFingerprint);
        Assert.Equal(locations.Fingerprint, WoundAcceptedTurnData.CloneEffectInput(baseline).WoundApplicationLocations!.Fingerprint);
    }

    [Fact]
    public void SkillScope_CatalogOnlyChangeInvalidatesValidatedCache()
    {
        var cache = new EffectAcceptedTurnPlanCache();
        var first = CreateSkillScopeInput("exact", all: true);
        var changed = CreateSkillScopeInput("disabled", all: true);
        Assert.Equal(first.SourceAuthority.Fingerprint, changed.SourceAuthority.Fingerprint);
        Assert.Equal(first.TargetAuthority.Fingerprint, changed.TargetAuthority.Fingerprint);
        var original = cache.GetOrBuildValidated(first, out var firstReused);
        Assert.True(original.Success, string.Join(Environment.NewLine, original.Issues));
        Assert.False(firstReused);
        Assert.Same(original, cache.GetOrBuildValidated(first, out var sameReused));
        Assert.True(sameReused);
        var replaced = cache.GetOrBuildValidated(changed, out var changedReused);
        Assert.True(replaced.Success, string.Join(Environment.NewLine, replaced.Issues));
        Assert.False(changedReused);
        Assert.NotEqual(original.Plan!.InputFingerprint, replaced.Plan!.InputFingerprint);
    }

    [Theory]
    [InlineData("none", "exact")]
    [InlineData("exact", "none")]
    [InlineData("exact", "disabled")]
    [InlineData("unknown", "exact")]
    public void SkillScope_EitherCatalogHalfChangesFingerprint(string offered, string current)
    {
        var baseline = CreateSkillScopeInput("exact", all: true);
        var changed = baseline with
        {
            SkillScopeAuthority = EffectRollSkillScopeAuthority.Build(
                new(SkillRoots(offered), SkillRoots(current)))
        };
        var cache = new EffectAcceptedTurnPlanCache();
        var first = cache.GetOrBuild(baseline);
        var second = cache.GetOrBuild(changed, out var reused);
        Assert.True(first.Success && second.Success);
        Assert.False(reused);
        Assert.NotEqual(first.Plan!.InputFingerprint, second.Plan!.InputFingerprint);
    }

    internal static IReadOnlyDictionary<string, JsonNode?> SkillRoots(string scenario)
    {
        var skills = new JsonArray();
        if (scenario != "none")
        {
            var skill = new JsonObject { ["skillId"] = "skill_grip", ["name"] = "Хват" };
            if (scenario == "unknown") skill["skillId"] = "skill_other";
            if (scenario == "idless") skill.Remove("skillId");
            if (scenario == "disabled") skill["active"] = false;
            skills.Add(skill);
            if (scenario == "duplicate") skills.Add(skill.DeepClone());
            if (scenario == "confusable") skills.Add(new JsonObject { ["skillId"] = "SKILL_GRIP" });
            if (scenario == "two") skills.Add(new JsonObject { ["skillId"] = "skill_other", ["name"] = "Другой" });
        }
        return scenario == "wrong_target"
            ? new Dictionary<string, JsonNode?>
            {
                ["game_state/npcs/npc_core.json"] = new JsonObject
                {
                    ["npcs"] = new JsonArray(new JsonObject
                    {
                        ["npcId"] = "npc_other", ["activeSkills"] = skills
                    })
                }
            }
            : new Dictionary<string, JsonNode?> { ["game_state/player/skills_active.json"] = skills };
    }

    internal static EffectAcceptedTurnInput CreateSkillScopeInput(
        string scenario, bool all = false, string? acceptedScenario = null, string selectedSkill = "skill_grip")
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition("roll_modifier");
        definition["components"]![0]!["payload"]!["operations"] = new JsonArray("skill_check");
        definition["components"]![0]!["payload"]!["scope"] = all
            ? new JsonObject { ["kind"] = "all" }
            : new JsonObject { ["kind"] = "skill", ["skillId"] = selectedSkill };
        var command = EffectMaterializationTestFixture.CreateApplyCommand();
        command["source"]!["kind"] = "quest";
        command["source"]!["sourceId"] = "quest_scope";
        command["parameters"] = new JsonObject();
        var carriers = new EffectCarrierCatalogInput(null, null, null, null, null, null);
        var input = EffectAcceptedTurnInputComposer.Compose(
            "session_scope", "snapshot_scope", 42,
            EffectMaterializationTestFixture.CreateCommandRoot(command),
            carriers, carriers, null, SkillRoots(scenario),
            acceptedSourceRoots: acceptedScenario is null ? null : new Dictionary<string, JsonNode?>
            {
                ["game_state/player/skills_active.json"] = acceptedScenario == "removed"
                    ? new JsonObject { ["removeActiveSkills"] = new JsonArray("Хват") }
                    : new JsonObject
                    {
                        ["activeSkillChanges"] = SkillRoots(acceptedScenario)["game_state/player/skills_active.json"]!.DeepClone()
                    }
            });
        return input with
        {
            SourceAuthority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
                new[] { new EffectSourceExport("mortal_world", "quest", "quest_scope",
                    new JsonArray(definition), true, true, false) },
                Array.Empty<EffectSourceExport>(), new HashSet<string>(StringComparer.Ordinal))),
            TargetAuthority = EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
                new[] { new EffectTargetExport("mortal_world", "player", "player_current", false) },
                Array.Empty<EffectTargetExport>(), new HashSet<string>(StringComparer.Ordinal), null)),
            TargetAuthorityInput = null,
            EventInput = new JsonObject
            {
                ["turn"] = 42,
                ["events"] = new JsonArray(new JsonObject
                {
                    ["kind"] = "accepted_turn", ["authorityId"] = "turn_42", ["eventRef"] = "turn_42:scope"
                })
            }
        };
    }
}
