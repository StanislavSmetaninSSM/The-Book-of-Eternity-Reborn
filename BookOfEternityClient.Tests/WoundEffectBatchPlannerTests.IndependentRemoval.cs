using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class WoundEffectBatchPlannerTests
{
    [Fact]
    public void OrdinaryRemoval_WoundOwnedEffectTerminatesWithoutHealingSourceWound()
    {
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(
            CreateInput(), new RecordingWoundIdentityAllocator("independent_removal")));
        var creation = BuildEffectStage(prepared, new ScriptedEffectIdentityFactory("independent_removal"));
        var accepted = AssertEffectPlan(creation.Result);
        var finalized = AssertFinalized(WoundAcceptedTurnPlanner.Finalize(prepared, creation.Result));
        var wound = Assert.IsType<WoundMaterializationEnvelope>(
            Assert.Single(Assert.Single(finalized.CarrierContributions).Mutations).AfterWound);
        var originalWound = WoundMaterializationContract.SerializeCanonical(wound);
        var originalFingerprint = WoundIdentityState.ComputeSemanticFingerprint(wound);
        var woundCarriers = CreatePreTurnWoundCarriers(OwnerFlavor.Player,
            JsonNode.Parse(originalWound)!.AsObject());
        var originalCarrier = woundCarriers.PlayerWounds!.ToJsonString();
        var catalog = WoundCarrierCatalog.Build(woundCarriers);
        Assert.Empty(catalog.Issues);
        var effectId = Assert.Single(wound.Consequences.OwnedEffectSources.RootBindings).EffectId;
        var effects = CreateEffectCarriersFromPlan(accepted.EffectPlan);
        var existing = Assert.Single(effects.PlayerEffects!["activeEffects"]!.AsArray());
        Assert.Equal(effectId, existing!["effectId"]!.GetValue<string>());
        Assert.Equal("wound", existing["source"]!["kind"]!.GetValue<string>());
        Assert.Equal(wound.WoundId, existing["source"]!["sourceId"]!.GetValue<string>());
        var command = new JsonObject
        {
            ["operation"] = "remove", ["effectId"] = effectId,
            ["target"] = new JsonObject { ["kind"] = "player", ["targetId"] = "player_current" },
            ["authority"] = new JsonObject { ["kind"] = "stop_bleeding", ["authorityId"] = "authority_remove" },
            ["eventRef"] = new JsonObject { ["kind"] = "accepted_turn", ["authorityId"] = "authority_remove" },
            ["reason"] = "Bleeding is stopped without healing the wound."
        };
        var eventInput = new JsonObject
        {
            ["turn"] = Turn + 1,
            ["events"] = new JsonArray(new JsonObject
            {
                ["eventRef"] = "turn_43:independent_remove", ["kind"] = "accepted_turn",
                ["authorityId"] = "authority_remove"
            })
        };
        var targetAuthority = EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
            new[] { new EffectTargetExport(Realm, "player", "player_current", SameTurn: false) },
            Array.Empty<EffectTargetExport>(), new HashSet<string>(StringComparer.Ordinal), null));
        var input = new EffectAcceptedTurnInput(SessionId, SnapshotToken,
            EffectMaterializationTestFixture.CreateCommandRoot(command),
            EffectAcceptedTurnInputComposer.BuildCanonicalWoundSourceAuthority(catalog),
            targetAuthority, eventInput, Realm, effects, accepted.EffectPlan.IdentityIndexAfterImage);
        var removed = EffectAcceptedTurnPlanner.Build(input, Fingerprint("independent_removal"),
            new ScriptedEffectIdentityFactory("independent_remove_terminal"));
        Assert.Empty(removed.Issues);
        var plan = Assert.IsType<EffectAcceptedTurnPlan>(removed.Plan);
        Assert.Empty(CreateEffectCarriersFromPlan(plan).PlayerEffects!["activeEffects"]!.AsArray());
        var entry = Assert.Single(plan.IdentityIndexAfterImage["entries"]!.AsArray());
        Assert.Equal(effectId, entry!["effectId"]!.GetValue<string>());
        Assert.Equal("removed", entry["state"]!.GetValue<string>());
        Assert.Equal(originalCarrier, woundCarriers.PlayerWounds.ToJsonString());
        var retainedCatalog = WoundCarrierCatalog.Build(woundCarriers);
        Assert.Empty(retainedCatalog.Issues);
        var retainedWound = Assert.Single(retainedCatalog.Occurrences).Wound;
        Assert.Equal(originalWound, WoundMaterializationContract.SerializeCanonical(retainedWound));
        Assert.Equal(originalFingerprint, WoundIdentityState.ComputeSemanticFingerprint(retainedWound));
        Assert.Equal("active", retainedWound.Lifecycle);
        Assert.DoesNotContain(WoundCarrierCatalog.PlayerPath, plan.TouchedPaths);
        Assert.DoesNotContain(WoundIdentityState.StatePath, plan.TouchedPaths);
        Assert.DoesNotContain(WoundHistoryState.HistoryPath, plan.TouchedPaths);
        Assert.DoesNotContain(WoundCarrierCatalog.PlayerPath, plan.DeletedPaths);
        Assert.DoesNotContain(WoundIdentityState.StatePath, plan.DeletedPaths);
        Assert.DoesNotContain(WoundHistoryState.HistoryPath, plan.DeletedPaths);
    }
}
