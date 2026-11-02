using System.Text.Json.Nodes;
using static BookOfEternityClient.Services.SpiritualWoundStateJson;

namespace BookOfEternityClient.Services;

/// <summary>
/// Retains intrinsic strain comparison DATA. Parent receipt/history agreement and runtime availability
/// must be independently proved by their owners; this object grants no source or lane capability.
/// </summary>
internal sealed class SpiritualStrainPreimageData
{
    private readonly JsonObject _ordinary;
    private readonly JsonObject _actual;

    private SpiritualStrainPreimageData(string parentSourceId, WoundMaterializationEnvelope parent,
        string effectId, string componentId, JsonObject ordinary, JsonObject actual)
    {
        ParentSourceId = parentSourceId;
        ParentWound = parent;
        EffectId = effectId;
        ComponentId = componentId;
        _ordinary = ordinary;
        _actual = actual.DeepClone().AsObject();
    }

    internal string ParentSourceId { get; }
    internal WoundMaterializationEnvelope ParentWound { get; }
    internal string EffectId { get; }
    internal string ComponentId { get; }

    /// <summary>Reads a closed comparison after the witness's actual shape/dice checks; issues no authority.</summary>
    internal static SpiritualStrainPreimageData ReadIntrinsic(JsonObject source, JsonObject actual)
    {
        var proof = source["strainPreimage"] as JsonObject ?? throw new FormatException("Strain preimage object required.");
        Closed(proof, "version ordinaryExchangeJsonBase64 parentSourceId parentWoundJsonBase64 effectId componentId");
        Integer(proof["version"], 1, 1);
        var parentSourceId = Text(proof["parentSourceId"]);
        Require(parentSourceId.StartsWith("spiritual_source_", StringComparison.Ordinal) &&
            parentSourceId != Text(source["sourceId"]), "Distinct parent source required.");
        Require(Text(source["affectedSide"]) == "player" &&
            Integer(source["sourceOrdinal"]) > 0 && Integer(source["exchangeOrdinal"]) > 0 &&
            Integer(source["exchangeOrdinal"]) + 1 == Integer(source["turnEvidence"]!["bounds"]!["exchangeCount"]),
            "Strain comparison requires the last player-side exchange after an earlier source.");
        Require(IsToken(actual["operationType"], "maneuver") && IsToken(actual["outcome"], "setback"),
            "Only the bounded failed maneuver comparison is supported.");

        var ordinary = ReadObject(proof["ordinaryExchangeJsonBase64"]);
        Require(ordinary["before"] is JsonObject && ordinary["after"] is JsonObject,
            "Complete ordinary snapshots required.");
        var ordinaryStrain = Text(ordinary["after"]!["playerSideStrain"]);
        Require(SpiritualWoundOpportunityMath.StrainRank(ordinaryStrain) is >= 0 and <= 4,
            "Ordinary strain required.");
        var comparison = new ValidationService.SpiritualStrainDraftCorrection("/retainedSource/exchange", ordinary);
        Require(comparison.IsCorrected(actual), "Actual exchange must contain only the proved one-step strain correction.");

        var parentObject = ReadObject(proof["parentWoundJsonBase64"]);
        var parsed = WoundMaterializationContract.Parse(parentObject.ToJsonString(), "retainedSource.strainParent");
        Require(parsed.IsValid, "Valid complete parent wound required.");
        var parent = parsed.Wound!;
        var affected = source["affectedActor"] as JsonObject ?? throw new FormatException("Affected actor required.");
        Require(Text(affected["actorKind"]) == "player_soul" &&
            parent.Owner == new WoundOwnerCoordinate(Text(source["turnEvidence"]!["realm"]), "player_soul",
                Text(affected["actorId"]), WoundCarrierCatalog.AfterlifeProfilesPath) &&
            parent.Lifecycle == "active" && parent.Classification.Domain == "spiritual" && parent.Severity.Rank == 3 &&
            parent.LastTransition.Kind == "create" && parent.LastTransition.Ordinal == 1 &&
            parent.LastTransition.Turn == Integer(source["turnEvidence"]!["turn"]) &&
            parent.Origin.CreatedAtTurn == parent.LastTransition.Turn,
            "Matching newly created rank-III parent snapshot required.");

        var effectId = Text(proof["effectId"]);
        var componentId = Text(proof["componentId"]);
        var applicable = new List<(string EffectId, JsonObject Component)>();
        foreach (var root in parent.Consequences.OwnedEffectSources.RootBindings)
        {
            var fact = parent.Consequences.OwnedEffectSources.DefinitionFacts.SingleOrDefault(
                value => value.DefinitionKey == root.DefinitionKey);
            Require(fact is not null, "Parent root definition required.");
            var definition = Parse(fact!.CanonicalJson);
            foreach (var component in definition["components"]!.AsArray().OfType<JsonObject>())
                if (component["payload"] is JsonObject payload && IsToken(payload["operation"], "maneuver"))
                    applicable.Add((root.EffectId, component));
        }
        Require(applicable.Count == 1, "Exactly one parent component may target maneuver in this route.");
        var selected = applicable[0];
        Require(selected.EffectId == effectId && Text(selected.Component["componentId"]) == componentId &&
            Text(selected.Component["profile"]) == "spiritual_strain_burden" &&
            Text(selected.Component["payload"]!["axis"]) == "sideStrain" &&
            Integer(selected.Component["payload"]!["magnitude"], 1, 1) == 1 &&
            parent.Consequences.Entries.Count(entry => entry.EffectId == effectId &&
                entry.ProfileKey == "spiritual_strain_burden") == 1,
            "Selected parent root/component/profile agreement required.");
        return new(parentSourceId, parent, effectId, componentId, ordinary, actual);
    }

    /// <summary>Copies ordinary after DATA only for the exact intrinsic actual exchange, never a current permission.</summary>
    internal JsonObject ReadOrdinaryAfter(JsonObject actual)
    {
        Require(JsonNode.DeepEquals(_actual, actual), "Strain comparison belongs to another actual exchange.");
        return _ordinary["after"]!.DeepClone().AsObject();
    }

    private static JsonObject ReadObject(JsonNode? encoded) => Parse(DecodeUtf8JsonText(DecodeJsonObjectBytes(encoded)));
    private static bool IsToken(JsonNode? node, string token) => node is JsonValue value &&
        value.TryGetValue<string>(out var text) && string.Equals(text, token, StringComparison.OrdinalIgnoreCase);
}
