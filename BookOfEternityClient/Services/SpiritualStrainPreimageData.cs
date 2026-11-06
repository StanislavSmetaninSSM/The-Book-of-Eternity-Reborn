using System.Text.Json.Nodes;
using static BookOfEternityClient.Services.SpiritualWoundStateJson;

namespace BookOfEternityClient.Services;

/// <summary>
/// Retains intrinsic strain comparison DATA. Parent receipt/history agreement and runtime availability
/// must be independently proved by their owners; this object grants no source or lane capability.
/// </summary>
internal sealed partial class SpiritualStrainPreimageData
{
    private readonly JsonObject _ordinary;
    private readonly JsonObject _actual;

    private SpiritualStrainPreimageData(int version, string? parentSourceId, WoundMaterializationEnvelope parent,
        string effectId, string componentId, JsonObject ordinary, JsonObject actual,
        SpiritualWoundCalculation? ordinaryCalculation = null, SpiritualWoundCalculation? actualCalculation = null,
        string? originalInputFingerprint = null, string? originalExchangePointer = null)
        : this(version, [new SpiritualStrainParentData(parentSourceId, parent, effectId, componentId)],
            ordinary, actual, ordinaryCalculation, actualCalculation, originalInputFingerprint, originalExchangePointer)
    {
    }

    private SpiritualStrainPreimageData(int version, IReadOnlyList<SpiritualStrainParentData> parents,
        JsonObject ordinary, JsonObject actual, SpiritualWoundCalculation? ordinaryCalculation = null,
        SpiritualWoundCalculation? actualCalculation = null, string? originalInputFingerprint = null,
        string? originalExchangePointer = null)
    {
        Version = version;
        Parents = Array.AsReadOnly(parents.ToArray());
        _ordinary = ordinary;
        _actual = actual.DeepClone().AsObject();
        OrdinaryCalculation = ordinaryCalculation;
        ActualCalculation = actualCalculation;
        OriginalInputFingerprint = originalInputFingerprint;
        OriginalExchangePointer = originalExchangePointer;
    }

    internal int Version { get; }
    /// <summary>Gets one immutable parent for versions 1–3 or the canonical complete pair for version 4.</summary>
    internal IReadOnlyList<SpiritualStrainParentData> Parents { get; }
    private SpiritualStrainParentData SingleParent => Version is >= 1 and <= 3 && Parents.Count == 1
        ? Parents[0] : throw new FormatException("A singular parent accessor requires a single-parent wire version.");
    /// <summary>Gets the earlier version-1 source, refusing pair DATA rather than choosing its first parent.</summary>
    internal string? ParentSourceId => SingleParent.ParentSourceId;
    /// <summary>Gets the complete legacy single parent; pair DATA requires explicit list traversal.</summary>
    internal WoundMaterializationEnvelope ParentWound => SingleParent.ParentWound;
    /// <summary>Gets the legacy single parent's fingerprint, refusing pair DATA.</summary>
    internal string ParentSemanticFingerprint => SingleParent.ParentSemanticFingerprint;
    /// <summary>Gets the legacy single selected root identity, refusing pair DATA.</summary>
    internal string EffectId => SingleParent.EffectId;
    /// <summary>Gets the legacy single selected component identity, refusing pair DATA.</summary>
    internal string ComponentId => SingleParent.ComponentId;

    /// <summary>Gets the recomputed ordinary pressure DATA; old wire versions have no such retained calculation.</summary>
    internal SpiritualWoundCalculation? OrdinaryCalculation { get; }
    /// <summary>Gets the already checked actual pressure calculation without recomputing the outer witness.</summary>
    internal SpiritualWoundCalculation? ActualCalculation { get; }
    /// <summary>Gets the raw ordinary input comparator; this hash is never original-input authority.</summary>
    internal string? OriginalInputFingerprint { get; }
    /// <summary>Gets the canonical global exchange pointer retained by a pressure comparison.</summary>
    internal string? OriginalExchangePointer { get; }

    /// <summary>Strictly selects a supported closed wire branch before its version-specific validation.</summary>
    /// <param name="source">Containing source with a required strain preimage object.</param>
    /// <returns>The supported integer version; malformed or unknown versions throw.</returns>
    internal static int ReadVersion(JsonObject source) =>
        (int)Integer(Object(source["strainPreimage"])["version"], 1, 4);

    /// <summary>Reads a closed comparison after the witness's actual shape/dice checks; issues no authority.</summary>
    internal static SpiritualStrainPreimageData ReadIntrinsic(JsonObject source, JsonObject actual)
    {
        var proof = source["strainPreimage"] as JsonObject ?? throw new FormatException("Strain preimage object required.");
        var version = ReadVersion(source);
        if (version is 3 or 4)
        {
            // The public DATA convenience path must not bypass any actual-source check.
            // The witness calls ReadPressureIntrinsic directly, so this is not recursive.
            _ = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var checkedData);
            Require(checkedData is not null && JsonNode.DeepEquals(checkedData._actual, actual),
                "Pressure comparison belongs to another actual exchange.");
            return checkedData!;
        }
        var fields = "version ordinaryExchangeJsonBase64 parentWoundJsonBase64 effectId componentId";
        Closed(proof, version == 1 ? fields + " parentSourceId" : fields);
        string? parentSourceId = null;
        if (version == 1)
        {
            parentSourceId = Text(proof["parentSourceId"]);
            Require(parentSourceId.StartsWith("spiritual_source_", StringComparison.Ordinal) &&
                parentSourceId != Text(source["sourceId"]), "Distinct parent source required.");
            Require(Integer(source["sourceOrdinal"]) > 0 && Integer(source["exchangeOrdinal"]) > 0,
                "Same-request strain requires an earlier source and exchange.");
        }
        Require(Text(source["affectedSide"]) == "player" &&
            Integer(source["exchangeOrdinal"]) + 1 == Integer(source["turnEvidence"]!["bounds"]!["exchangeCount"]),
            "Strain comparison requires the last player-side exchange.");
        var cappedGuard = IsToken(actual["operationType"], "guard") &&
            actual["incomingAction"] is JsonObject incoming &&
            IsToken(incoming["finalOperationType"] ?? incoming["operationType"], "pressure") &&
            actual["before"] is JsonObject guardBefore && actual["after"] is JsonObject guardAfter &&
            IsToken(guardBefore["playerSideStrain"], "overwhelmed") && IsToken(guardAfter["playerSideStrain"], "broken");
        Require((IsToken(actual["operationType"], "maneuver") || version == 1 && cappedGuard) && IsToken(actual["outcome"], "setback"),
            "Only the bounded failed maneuver or independently proved capped guard comparison is supported.");

        var ordinary = ReadObject(proof["ordinaryExchangeJsonBase64"]);
        Require(ordinary["before"] is JsonObject && ordinary["after"] is JsonObject,
            "Complete ordinary snapshots required.");
        var ordinaryStrain = Text(ordinary["after"]!["playerSideStrain"]);
        Require(SpiritualWoundOpportunityMath.StrainRank(ordinaryStrain) is >= 0 and <= 4,
            "Ordinary strain required.");
        var comparison = new ValidationService.SpiritualStrainDraftCorrection("/retainedSource/exchange", ordinary);
        Require(comparison.IsCorrected(actual), "Actual exchange must contain only the proved one-step strain correction.");
        Require(!cappedGuard || JsonNode.DeepEquals(ordinary, actual), "Capped guard must preserve its complete ordinary exchange.");
        if (version == 2)
        {
            Require(actual["incomingAction"] is JsonObject incomingGuard &&
                IsToken(incomingGuard["finalOperationType"] ?? incomingGuard["operationType"], "guard"),
                "Existing-parent maneuver requires incoming guard.");
            Require(JsonNode.DeepEquals(ordinary["before"]!["playerSideStrain"], ordinary["after"]!["playerSideStrain"]) &&
                JsonNode.DeepEquals(ordinary["before"]!["oppositionSideStrain"], ordinary["after"]!["oppositionSideStrain"]),
                "Existing-parent maneuver must retain both ordinary side strains.");
            var issues = new List<ValidationIssue>();
            ValidationService.ValidateRetainedSpiritualExchangeShape(ordinary, "retainedSource.ordinary", issues);
            ValidationService.ValidateSpiritualWoundSourceActionShape(ordinary, "retainedSource.ordinary", issues);
            ValidationService.ValidateRetainedSpiritualDiceAudit(ordinary, source["turnEvidence"]!.AsObject(), issues);
            ValidationService.ValidateRetainedSpiritualExchangeRules(ordinary, issues);
            Require(!issues.Any(issue => issue.Severity == IssueSeverity.Error), "Valid complete ordinary maneuver required.");
        }
        var operation = cappedGuard ? "guard" : "maneuver";

        var parent = ReadParent(source, proof, operation, version);
        return new(version, parentSourceId, parent.Parent, parent.EffectId, parent.ComponentId, ordinary, actual);
    }

    /// <summary>Validates the shared complete parent graph without proving current availability or accepted History.</summary>
    /// <param name="source">Containing source's already selected actor and turn coordinates.</param>
    /// <param name="proof">Version-specific closed comparison object.</param>
    /// <param name="operation">The bounded operation whose strain component must be unique.</param>
    /// <param name="version">Validated wire version; version 1 requires a newly created parent.</param>
    /// <returns>The parsed parent and its exact selected root/component identifiers.</returns>
    private static (WoundMaterializationEnvelope Parent, string EffectId, string ComponentId) ReadParent(
        JsonObject source, JsonObject proof, string operation, int version)
    {
        var parentObject = ReadObject(proof["parentWoundJsonBase64"]);
        var parsed = WoundMaterializationContract.Parse(parentObject.ToJsonString(), "retainedSource.strainParent");
        Require(parsed.IsValid, "Valid complete parent wound required.");
        var parent = parsed.Wound!;
        var affected = source["affectedActor"] as JsonObject ?? throw new FormatException("Affected actor required.");
        Require(Text(affected["actorKind"]) == "player_soul" &&
            parent.Owner == new WoundOwnerCoordinate(Text(source["turnEvidence"]!["realm"]), "player_soul",
                Text(affected["actorId"]), WoundCarrierCatalog.AfterlifeProfilesPath) &&
            parent.Lifecycle == "active" && parent.Classification.Domain == "spiritual",
            "Matching active spiritual parent snapshot required.");
        if (version == 1)
            Require(parent.Severity.Rank == 3 && parent.LastTransition.Kind == "create" &&
                parent.LastTransition.Ordinal == 1 &&
                parent.LastTransition.Turn == Integer(source["turnEvidence"]!["turn"]) &&
                parent.Origin.CreatedAtTurn == parent.LastTransition.Turn,
                "Matching newly created rank-III parent snapshot required.");
        else
            Require(parent.Severity.Rank is 3 or 4 &&
                parent.LastTransition.Turn <= Integer(source["turnEvidence"]!["turn"]),
                "Existing III/IV parent cannot follow its compared source.");

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
                if (component["payload"] is JsonObject payload && IsToken(payload["operation"], operation) &&
                    (version == 1 || IsToken(component["profile"], "spiritual_strain_burden")))
                    applicable.Add((root.EffectId, component));
        }
        Require(applicable.Count == 1, "Exactly one parent component may target the compared operation in this route.");
        var selected = applicable[0];
        Require(selected.EffectId == effectId && Text(selected.Component["componentId"]) == componentId &&
            Text(selected.Component["profile"]) == "spiritual_strain_burden" &&
            Text(selected.Component["payload"]!["axis"]) == "sideStrain" &&
            Integer(selected.Component["payload"]!["magnitude"], 1, 1) == 1 &&
            parent.Consequences.Entries.Count(entry => entry.EffectId == effectId &&
                entry.ProfileKey == "spiritual_strain_burden") == 1,
            "Selected parent root/component/profile agreement required.");
        return (parent, effectId, componentId);
    }

    /// <summary>Copies ordinary after DATA only for the exact intrinsic actual exchange, never a current permission.</summary>
    internal JsonObject ReadOrdinaryAfter(JsonObject actual)
    {
        Require(JsonNode.DeepEquals(_actual, actual), "Strain comparison belongs to another actual exchange.");
        return _ordinary["after"]!.DeepClone().AsObject();
    }

    /// <summary>Detaches the exact accepted actual DATA for an independently authenticated historical comparison.</summary>
    internal JsonObject ReadActualExchange() => _actual.DeepClone().AsObject();

    // Preserve strict encoding/duplicate checks without decoding and parsing the same object twice.
    private static JsonObject ReadObject(JsonNode? encoded) => ReadPressureObject(encoded);
    private static JsonObject Object(JsonNode? node) => node as JsonObject ?? throw new FormatException("Object required.");
    private static bool IsToken(JsonNode? node, string token) => node is JsonValue value &&
        value.TryGetValue<string>(out var text) && string.Equals(text, token, StringComparison.OrdinalIgnoreCase);
}
