using System.Globalization;
using System.Text.Json.Nodes;
using static BookOfEternityClient.Services.SpiritualWoundStateJson;

namespace BookOfEternityClient.Services;

internal sealed partial class SpiritualStrainPreimageData
{
    /// <summary>Reads a closed singleton-v5 or complete-group-v7 guardian comparison after full actual witness validation.</summary>
    /// <param name="source">Complete actual player-harm/guardian-target source whose calculation has passed.</param>
    /// <param name="actual">The same fully validated actual payload.</param>
    /// <param name="actualCalculation">The witness's one completed harmful-source calculation.</param>
    /// <returns>Detached intrinsic comparison DATA, never current availability or signed authority.</returns>
    internal static SpiritualStrainPreimageData ReadGuardianPressureIntrinsic(JsonObject source, JsonObject actual,
        SpiritualWoundCalculation actualCalculation)
    {
        var proof = Object(source["strainPreimage"]);
        var version = ReadVersion(source);
        Require(version is 5 or 7, "Guardian comparison requires version 5 or 7.");
        Closed(proof, version == 5
            ? "version inputRoleRevision ordinaryExchangeJsonBase64 originalInput parentWoundJsonBase64 effectId componentId ordinaryCalculation"
            : "version inputRoleRevision ordinaryExchangeJsonBase64 originalInput parents ordinaryCalculation");
        Require(Text(proof["inputRoleRevision"]) == (version == 5 ? SpiritualWoundFailedAction.Role : SpiritualWoundFailedAction.GroupRole),
            "Unsupported guardian input role.");
        var evidence = Object(source["turnEvidence"]);
        Require(Text(evidence["realm"]) == "chaos_sea" && Integer(source["exchangeOrdinal"], 0, 0) == 0 &&
            Integer(evidence["bounds"]!["exchangeCount"], 1, 1) == 1 && Text(source["operation"]) == "pressure" &&
            Text(source["appliedArtKind"]) == "standard" && source["guaranteedSeverityRank"] is null &&
            source["retraumaWoundId"] is null && actualCalculation is { HasIncreasingStrain: true, MaximumSeverityRank: >= 0 } &&
            actualCalculation.PreviousStrainRank == 1 && actualCalculation.NewStrainRank is 3 or 4 && actual["diceAudit"] is JsonObject,
            "Only the last ordinal-zero increasing guardian I-to-III/IV standard pressure source with nonnegative maximum is supported.");
        var failed = SpiritualWoundFailedAction.ReadRetained(source, actual);
        var ordinary = ReadPressureObject(proof["ordinaryExchangeJsonBase64"]);
        Require(ordinary["before"] is JsonObject && ordinary["after"] is JsonObject &&
            Text(ordinary["before"]!["oppositionSideStrain"]) == "strained" &&
            Text(ordinary["after"]!["oppositionSideStrain"]) is "fractured" or "overwhelmed" or "broken",
            "Guardian ordinary strain must be independently proved I-to-II/III/IV.");
        var expected = ordinary.DeepClone().AsObject();
        expected["after"]!["oppositionSideStrain"] = actual["after"]!["oppositionSideStrain"]!.DeepClone();
        Require(JsonNode.DeepEquals(expected, actual), "Only the selected guardian after-strain may differ.");
        foreach (var field in new[] { "playerSideStrain", "conflictPosition", "controlState", "tempoAdvantage" })
            Require(JsonNode.DeepEquals(ordinary["before"]![field], ordinary["after"]![field]),
                "Guardian attribution preserves all other snapshot fields.");
        var issues = new List<ValidationIssue>();
        ValidationService.ValidateRetainedSpiritualExchangeShape(ordinary, "retainedSource.guardianOrdinary", issues);
        ValidationService.ValidateSpiritualWoundSourceActionShape(ordinary, "retainedSource.guardianOrdinary", issues);
        ValidationService.ValidateRetainedSpiritualDiceAudit(ordinary, evidence, issues);
        ValidationService.ValidateRetainedSpiritualExchangeRules(ordinary, issues);
        Require(!issues.Any(issue => issue.Severity == IssueSeverity.Error), "Valid complete guardian ordinary pressure required.");
        Require(SpiritualWoundFailedAction.ReadRetained(source, ordinary) == failed, "Ordinary/actual failed action differs.");
        // Parse every parent before applying the cap; saturation never makes later ancestry expendable.
        var parents = version == 7 ? ReadPressureParents(source, proof, version) : null;
        var ordinaryCalculation = SpiritualWoundOpportunityMath.Calculate(actualCalculation.Input with { NewStrain = Text(ordinary["after"]!["oppositionSideStrain"]) });
        Require(ordinaryCalculation is { HasIncreasingStrain: true, MaximumSeverityRank: >= 0 } &&
            actualCalculation.NewStrainRank == Math.Min(4L, (long)ordinaryCalculation.NewStrainRank + (parents?.Count ?? 1)),
            "Increasing ordinary guardian source with nonnegative maximum and one capped step per parent required.");
        var declared = Object(proof["ordinaryCalculation"]);
        Closed(declared, "traumaPressure maximumSeverityRank");
        Require(Integer(declared["traumaPressure"], long.MinValue, long.MaxValue) == ordinaryCalculation!.TraumaPressure &&
            Integer(declared["maximumSeverityRank"], 0, 4) == ordinaryCalculation.MaximumSeverityRank,
            "Declared ordinary guardian calculation differs.");
        var input = Object(proof["originalInput"]);
        Closed(input, "path existed contentFingerprint exchangePointer");
        Require(Text(input["path"]) == AfterlifeSpiritualConflictState.StatePath &&
            input["existed"] is JsonValue existed && existed.TryGetValue<bool>(out var present) && present,
            "Existing original conflict input required.");
        var fingerprint = Fingerprint(input["contentFingerprint"]);
        var pointer = Text(input["exchangePointer"]);
        const string prefix = "/activeConflict/exchangeLog/";
        Require(pointer.StartsWith(prefix, StringComparison.Ordinal) &&
            int.TryParse(pointer[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var index) &&
            index >= 0 && pointer == prefix + index.ToString(CultureInfo.InvariantCulture), "Canonical direct-root exchange pointer required.");
        if (version == 7)
            return new(version, parents!, ordinary, actual, ordinaryCalculation, actualCalculation, fingerprint, pointer);
        var selected = ReadGuardianParent(source, proof, version);
        return new(version, null, selected.Parent, selected.EffectId, selected.ComponentId, ordinary, actual,
            ordinaryCalculation, actualCalculation, fingerprint, pointer);
    }

    /// <summary>Checks exact affected guardian ownership before sharing complete parent-component validation.</summary>
    /// <param name="source">Fully validated guardian-target harmful source.</param>
    /// <param name="proof">The singleton proof or one closed group row.</param>
    /// <param name="version">Validated guardian wire version5 or7.</param>
    /// <returns>Detached complete parent facts; no signed membership or live authority.</returns>
    private static (WoundMaterializationEnvelope Parent, string EffectId, string ComponentId) ReadGuardianParent(
        JsonObject source, JsonObject proof, int version)
    {
        var parentJson = ReadPressureObject(proof["parentWoundJsonBase64"]);
        var parsed = WoundMaterializationContract.Parse(parentJson.ToJsonString(), "retainedSource.guardianParent");
        Require(parsed.IsValid, "Valid complete guardian parent required.");
        var parent = parsed.Wound!;
        var affected = Object(source["affectedActor"]);
        Require(Text(source["turnEvidence"]!["realm"]) == "chaos_sea" && Text(affected["actorKind"]) == "guardian" &&
            parent.Owner == new WoundOwnerCoordinate("chaos_sea", "guardian", Text(affected["actorId"]),
                WoundCarrierCatalog.AfterlifeProfilesPath) && parent.Lifecycle == "active" &&
            parent.Classification.Domain == "spiritual" && parent.Severity.Rank is 3 or 4 &&
            parent.LastTransition.Turn <= Integer(source["turnEvidence"]!["turn"]), "Matching active existing guardian III/IV parent required.");
        return ReadParentComponent(parent, proof, "pressure", version);
    }
}
