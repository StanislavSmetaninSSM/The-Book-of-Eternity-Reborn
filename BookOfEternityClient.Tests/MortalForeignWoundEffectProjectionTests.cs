using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Current typed target DATA grants a card, never treatment or publication authority.</summary>
public sealed class MortalForeignWoundEffectProjectionTests
{
    /// <summary>Visible physical wounds expose only the exact accepted owner's visible source-bound consequence.</summary>
    [Theory]
    [InlineData("npc")]
    [InlineData("combatant")]
    public void KnownMortalCardPreservesOwnerAndSourcePrivacyBeforeShowingConsequence(string kind)
    {
        const string visibleEffect = "Известное последствие чужой физической раны";
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(kind);
        var ownerId = effect["target"]!["targetId"]!.GetValue<string>();
        var wound = WoundContractTestData.CreateActiveWound(ownerKind: kind, ownerId: ownerId,
            carrierPath: kind == "npc" ? WoundCarrierCatalog.NpcPath : WoundCarrierCatalog.EnemiesPath);
        var input = WoundPlayerProjectionTestData.Input(wound);
        var catalog = WoundCarrierCatalog.Build(input.Carriers);
        Assert.Empty(catalog.Issues);
        Assert.Empty(input.History.ValidateAgreement(input.Identities, catalog));
        var occurrence = Assert.Single(catalog.Occurrences);
        var summary = new WoundTargetWoundSummary(occurrence.Wound.Display.Name, occurrence.Wound.Severity.Value,
            occurrence.Wound.Display.Description, occurrence.WoundId,
            WoundIdentityState.ComputeSemanticFingerprint(occurrence.Wound));
        var target = new WoundTargetChoice("current_target", "Известный участник", "Текущая локация", [summary],
            occurrence.Wound.Owner, null, WoundAcceptedTurnFingerprintWriter.Compute(
                ["mortal_card_fixture", kind, ownerId, summary.WoundId, summary.Fingerprint]));
        effect["effectId"] = "effect_wound_test_bleeding";
        effect["source"]!["definitionKey"] = "definition_wound_test_bleeding";
        effect["display"]!["name"] = visibleEffect;
        var before = input.Carriers.NpcWounds!.ToJsonString() + input.Carriers.EnemyCombatants!.ToJsonString() + effect.ToJsonString();

        foreach (var scenario in new[] { "hidden", "other_source", "other_definition", "other_owner" })
        {
            var changed = effect.DeepClone().AsObject();
            switch (scenario)
            {
                case "hidden": changed["display"]!["visibility"] = "hidden"; break;
                case "other_source": changed["source"]!["sourceId"] = "wound_other_source"; break;
                case "other_definition": changed["source"]!["definitionKey"] = "definition_other_source"; break;
                default: changed["target"]!["targetId"] = kind + "_other_owner"; break;
            }
            var rejected = WoundPlayerProjection.ProjectKnownTarget(Context(changed), target);
            Assert.True(rejected.IsAvailable);
            Assert.DoesNotContain(Assert.Single(rejected.Active).Facts,
                fact => fact.Label == "Последствие" && fact.Value == visibleEffect);
        }

        var context = Context(effect);
        var accepted = Assert.Single(context.Input.Effects.Effects);
        Assert.Equal(kind, accepted.OwnerKind);
        Assert.Equal(kind, accepted.TargetKind);
        Assert.Equal(ownerId, accepted.OwnerId);
        Assert.Equal(ownerId, accepted.TargetId);
        Assert.Equal(occurrence.WoundId, accepted.CanonicalEffect.GetProperty("source").GetProperty("sourceId").GetString());
        var result = WoundPlayerProjection.ProjectKnownTarget(context, target);
        Assert.True(result.IsAvailable);
        Assert.Equal(summary.Name, Assert.Single(result.Active).Name);
        Assert.Empty(result.History);
        Assert.Equal(before, input.Carriers.NpcWounds!.ToJsonString() + input.Carriers.EnemyCombatants!.ToJsonString() + effect.ToJsonString());
        // Intended RED: the legitimate consequence is missing, after all real metadata/privacy premises.
        Assert.Contains(Assert.Single(result.Active).Facts, fact => fact.Label == "Последствие" && fact.Value == visibleEffect);

        Assert.False(WoundPlayerProjection.ProjectKnownTarget(context with { MortalTargets = [] }, target).IsAvailable);
        var stale = target with { Wounds = [summary with { Fingerprint = "sha256:" + new string('0', 64) }] };
        Assert.False(WoundPlayerProjection.ProjectKnownTarget(context, stale).IsAvailable);
        var json = JsonSerializer.Serialize(result);
        foreach (var hidden in new[] { ownerId, occurrence.WoundId, "effect_wound_test_bleeding", "definition_wound_test_bleeding", "sha256:", "game_state/" })
            Assert.DoesNotContain(hidden, json);

        WoundPlayerReadContext Context(JsonObject current)
        {
            var id = current["target"]!["targetId"]!.GetValue<string>();
            var roots = kind == "npc"
                ? new EffectCarrierCatalogInput(null, new JsonObject { ["schemaVersion"] = 1,
                    ["entries"] = new JsonArray(new JsonObject { ["NPCId"] = id,
                        ["activeEffects"] = new JsonArray(current.DeepClone()) }) }, null, null, null, null)
                : new EffectCarrierCatalogInput(null, null, new JsonObject { ["enemiesData"] = new JsonArray(
                    new JsonObject { ["combatantId"] = id, ["activeDebuffs"] = new JsonArray(current.DeepClone()) }) }, null, null, null);
            var snapshot = EffectMechanicsSnapshot.Build(new(roots, EffectMaterializationTestFixture.CreateIdentityIndex(current)));
            Assert.True(snapshot.IsAccepted, string.Join(";", snapshot.Issues));
            var currentInput = input with { Effects = snapshot };
            var projection = WoundPlayerProjection.Build(currentInput, catalog);
            Assert.True(projection.IsAvailable);
            return new(currentInput, catalog, new JsonObject(), null, projection) { MortalTargets = [target] };
        }
    }
}
