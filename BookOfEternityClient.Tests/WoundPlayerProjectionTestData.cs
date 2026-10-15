using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

internal static class WoundPlayerProjectionTestData
{
    internal static WoundPlayerProjectionInput Input(params JsonObject[] wounds) => InputInRealm("mortal_world", wounds);

    internal static WoundPlayerProjectionInput InputInRealm(string realm, params JsonObject[] wounds)
    {
        var identities = new List<JsonObject>();
        var rows = new List<JsonObject>();
        wounds = wounds.Select(w => w.DeepClone().AsObject()).ToArray();
        foreach (var node in wounds)
        {
            node["lastTransition"]!["transitionId"] = "create_" + node["woundId"]!.GetValue<string>();
            var parsed = WoundMaterializationContract.Parse(node.ToJsonString(), "fixture.wound");
            Assert.True(parsed.IsValid, string.Join(";", parsed.Issues));
            var w = parsed.Wound!;
            var fingerprint = WoundIdentityState.ComputeSemanticFingerprint(w);
            identities.Add(WoundContractTestData.CreateIdentityEntry(w.WoundId, w.Owner.Realm,
                w.Owner.OwnerKind, w.Owner.OwnerId, w.Owner.CarrierPath, w.Classification.Domain,
                semanticFingerprint: fingerprint));
            var row = WoundContractTestData.CreateTransition();
            row["woundId"] = w.WoundId;
            row["transitionId"] = w.LastTransition.TransitionId;
            row["ordinal"] = rows.Count + 1;
            row["woundTransitionOrdinal"] = 1;
            row["operationKey"] = "create_" + w.WoundId;
            row["beforeFingerprint"] = WoundHistoryState.ComputeNonexistentBeforeFingerprint(w.WoundId);
            row["afterFingerprint"] = fingerprint;
            row["sourceFingerprint"] = "sha256:" + new string('e', 64);
            row["attemptId"] = null;
            row["outputFingerprint"] = WoundHistoryState.ComputeOutputFingerprint(
                row["operationKey"]!.GetValue<string>(), row["eventRef"]!.GetValue<string>(), row["readableSummary"]!.GetValue<string>());
            rows.Add(row);
        }
        var identity = WoundIdentityState.Parse(WoundContractTestData.CreateIdentityIndex(identities.ToArray()).ToJsonString(), "fixture.identities");
        var history = WoundHistoryState.Parse(WoundContractTestData.CreateHistory(rows.ToArray()).ToJsonString(), "fixture.history");
        Assert.True(identity.IsValid, string.Join(";", identity.Issues));
        Assert.True(history.IsValid, string.Join(";", history.Issues));
        var carriers = new WoundCarrierCatalogInput(
            WoundContractTestData.CreatePlayerCarrier(wounds.Where(w => w["owner"]!["ownerKind"]!.GetValue<string>() == "player").ToArray()),
            new JsonObject { ["schemaVersion"] = 1, ["entries"] = new JsonArray(wounds
                .Where(w => w["owner"]!["ownerKind"]!.GetValue<string>() == "npc")
                .GroupBy(w => w["owner"]!["ownerId"]!.GetValue<string>()).Select(g => (JsonNode)new JsonObject
                { ["npcId"] = g.Key, ["activeWounds"] = new JsonArray(g.Select(w => w.DeepClone()).ToArray()) }).ToArray()) },
            new JsonObject { ["enemiesData"] = new JsonArray() },
            new JsonObject { ["alliesData"] = new JsonArray() },
            new JsonObject { ["profiles"] = new JsonArray(wounds
                .Where(w => w["owner"]!["ownerKind"]!.GetValue<string>() == "player_soul")
                .GroupBy(w => w["owner"]!["ownerId"]!.GetValue<string>()).Select(g => (JsonNode)new JsonObject
                {
                    ["actorType"] = "player_soul", ["actorId"] = g.Key,
                    ["realm"] = g.First()["owner"]!["realm"]!.GetValue<string>() == "chaos_sea" ? "Chaos Sea" : "Shining Abode",
                    ["activeEffects"] = new JsonArray(), ["activeWounds"] = new JsonArray(g.Select(w => w.DeepClone()).ToArray())
                }).ToArray()) });
        var catalog = WoundCarrierCatalog.Build(carriers);
        Assert.Empty(catalog.Issues);
        Assert.Empty(history.State!.ValidateAgreement(identity.State!, catalog));
        return new(carriers, identity.State!, history.State!, new(true, [], [], []), realm,
            ResourceDefinitionCatalog.CreateBuiltIn(), new Dictionary<string, string>());
    }
}
