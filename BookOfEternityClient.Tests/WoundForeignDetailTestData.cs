using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Independent minimal coherent Self and nearby wound inputs, no game/bootstrap.</summary>
internal static class WoundForeignDetailTestData
{
    internal const string GuardianId = "guardian_detail";
    internal const string ResidentId = "resident_detail";
    internal const string AbodeId = "abode_detail";
    internal const string VisibleSymptom = "Заметное дрожание духовной ткани";

    internal static (WoundPlayerProjectionInput Input, JsonObject Guardians, JsonObject Residents) Create(
        string kind = "guardian", Action<JsonObject>? adjust = null)
    {
        var self = WoundContractTestData.CreateSpiritualActiveWound("wound_self_detail", "chaos_sea", "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath);
        self["display"]!["name"] = "ЛИЧНАЯ_РАНА_ИГРОКА";
        var input = WoundPlayerProjectionTestData.InputInRealm("chaos_sea", self);
        var id = kind == "guardian" ? GuardianId : ResidentId;
        var node = WoundContractTestData.CreateSpiritualActiveWound("wound_foreign_detail", "chaos_sea", kind, id, WoundCarrierCatalog.AfterlifeProfilesPath);
        // Keep each wound's consequence roots independently named even in the symptom-only fixture.
        foreach (var field in new[] { node["consequences"]!["entries"]!.AsArray(), node["consequences"]!["ownedEffectSources"]!["rootBindings"]!.AsArray() })
            foreach (var root in field.OfType<JsonObject>()) root["effectId"] = "foreign_" + root["effectId"]!.GetValue<string>();
        node["display"]!["name"] = "Известная рана обитателя";
        node["display"]!["description"] = "Известное повреждение духовной ткани.";
        node["display"]!["visibleSymptoms"] = new JsonArray(VisibleSymptom);
        node["treatment"]!["routes"] = new JsonArray(new JsonObject
        {
            ["routeId"] = "route_detail_known", ["displayName"] = "Укрепить духовную ткань",
            ["visibility"] = "known_to_player", ["mode"] = "procedure", ["requirements"] = new JsonArray(),
            ["resourcePolicy"] = new JsonObject(), ["resolution"] = new JsonObject(), ["outcomes"] = new JsonArray(), ["interruption"] = null
        });
        node["treatment"]!["knownRouteIds"] = new JsonArray("route_detail_known");
        node["treatment"]!["diagnosisPaths"] = new JsonArray();
        adjust?.Invoke(node);
        node["lastTransition"]!["transitionId"] = "create_wound_foreign_detail";
        var parsed = WoundMaterializationContract.Parse(node.ToJsonString(), "detail.fixture.wound");
        Assert.True(parsed.IsValid, string.Join(";", parsed.Issues));
        var wound = parsed.Wound!; var fingerprint = WoundIdentityState.ComputeSemanticFingerprint(wound);
        var profiles = input.Carriers.AfterlifeProfiles!["profiles"]!.AsArray();
        profiles.Add(Profile("guardian", GuardianId, "Хранитель подробностей", kind == "guardian" ? node : null));
        if (kind == "resident") profiles.Add(Profile("resident", ResidentId, "Житель подробностей", node));
        var identities = JsonNode.Parse(WoundIdentityState.SerializeCanonical(input.Identities))!.AsObject();
        identities["entries"]!.AsArray().Add(WoundContractTestData.CreateIdentityEntry(wound.WoundId, wound.Owner.Realm,
            wound.Owner.OwnerKind, wound.Owner.OwnerId, wound.Owner.CarrierPath, "spiritual", semanticFingerprint: fingerprint));
        var history = JsonNode.Parse(WoundHistoryState.SerializeCanonical(input.History))!.AsObject();
        var row = WoundContractTestData.CreateTransition(ordinal: 2); row["woundId"] = wound.WoundId;
        row["transitionId"] = wound.LastTransition.TransitionId; row["operationKey"] = "create_wound_foreign_detail";
        row["beforeFingerprint"] = WoundHistoryState.ComputeNonexistentBeforeFingerprint(wound.WoundId);
        row["afterFingerprint"] = fingerprint; row["sourceFingerprint"] = "sha256:" + new string('e', 64); row["attemptId"] = null;
        row["outputFingerprint"] = WoundHistoryState.ComputeOutputFingerprint(row["operationKey"]!.GetValue<string>(), row["eventRef"]!.GetValue<string>(), row["readableSummary"]!.GetValue<string>());
        history["transitions"]!.AsArray().Add(row); history["nextOrdinal"] = 3;
        var identity = WoundIdentityState.Parse(identities.ToJsonString(), "detail.fixture.identity");
        var historyState = WoundHistoryState.Parse(history.ToJsonString(), "detail.fixture.history");
        Assert.True(identity.IsValid); Assert.True(historyState.IsValid);
        input = input with { Identities = identity.State!, History = historyState.State! };
        var catalog = WoundCarrierCatalog.Build(input.Carriers); Assert.Empty(catalog.Issues);
        Assert.Empty(input.History.ValidateAgreement(input.Identities, catalog));
        var guardian = new JsonObject { ["guardianId"] = GuardianId, ["canonicalName"] = "Хранитель подробностей",
            ["abode"] = new JsonObject { ["abodeId"] = AbodeId, ["name"] = "Обитель подробностей" } };
        var guardians = new JsonObject { ["guardians"] = new JsonArray(guardian), ["activeGuardian"] = guardian.DeepClone(),
            ["chaosSeaNavigation"] = new JsonObject { ["currentGuardianId"] = GuardianId, ["currentAbodeId"] = AbodeId } };
        var residents = new JsonObject { ["entries"] = kind == "resident" ? new JsonArray(new JsonObject
        { ["residentId"] = ResidentId, ["guardianId"] = GuardianId, ["abodeId"] = AbodeId, ["isPresent"] = true,
            ["residentKind"] = "wayfaring_soul", ["roleLabel"] = "Смотритель", ["summary"] = "Известный обитатель." }) : new JsonArray() };
        return (input, guardians, residents);
    }

    private static JsonObject Profile(string kind, string id, string name, JsonObject? wound) => new()
    {
        ["actorType"] = kind, ["actorId"] = id, ["realm"] = "Chaos Sea", ["locationId"] = AbodeId,
        ["displayName"] = name, ["activeWounds"] = wound is null ? new JsonArray() : new JsonArray(wound.DeepClone()), ["activeEffects"] = new JsonArray()
    };
}
