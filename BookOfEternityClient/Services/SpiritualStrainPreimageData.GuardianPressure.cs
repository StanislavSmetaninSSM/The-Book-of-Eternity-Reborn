using System.Globalization;
using System.Text.Json.Nodes;
using static BookOfEternityClient.Services.SpiritualWoundStateJson;

namespace BookOfEternityClient.Services;

internal sealed partial class SpiritualStrainPreimageData
{
    /// <summary>Reads the one closed guardian pressure comparison after full actual witness validation.</summary>
    /// <param name="source">Complete actual player-harm/guardian-target source whose calculation has passed.</param>
    /// <param name="actual">The same fully validated actual payload.</param>
    /// <param name="actualCalculation">The witness's one completed harmful-source calculation.</param>
    /// <returns>Detached intrinsic comparison DATA, never current availability or signed authority.</returns>
    internal static SpiritualStrainPreimageData ReadGuardianPressureIntrinsic(JsonObject source, JsonObject actual,
        SpiritualWoundCalculation actualCalculation)
    {
        var proof = Object(source["strainPreimage"]);
        Require(ReadVersion(source) == 5, "Guardian comparison requires version 5.");
        Closed(proof, "version inputRoleRevision ordinaryExchangeJsonBase64 originalInput parentWoundJsonBase64 effectId componentId ordinaryCalculation");
        Require(Text(proof["inputRoleRevision"]) == SpiritualWoundFailedAction.Role, "Unsupported guardian input role.");
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
        var ordinaryCalculation = SpiritualWoundOpportunityMath.Calculate(actualCalculation.Input with { NewStrain = Text(ordinary["after"]!["oppositionSideStrain"]) });
        Require(ordinaryCalculation is { HasIncreasingStrain: true, MaximumSeverityRank: >= 0 } &&
            actualCalculation.NewStrainRank == Math.Min(4, ordinaryCalculation.NewStrainRank + 1), "Increasing ordinary guardian source with nonnegative maximum required.");
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
        var parentJson = ReadPressureObject(proof["parentWoundJsonBase64"]);
        var parsed = WoundMaterializationContract.Parse(parentJson.ToJsonString(), "retainedSource.guardianParent");
        Require(parsed.IsValid, "Valid complete guardian parent required.");
        var parent = parsed.Wound!;
        Require(parent.Owner == new WoundOwnerCoordinate("chaos_sea", "guardian", failed.ActorId,
                WoundCarrierCatalog.AfterlifeProfilesPath) && parent.Lifecycle == "active" &&
            parent.Classification.Domain == "spiritual" && parent.Severity.Rank is 3 or 4 &&
            parent.LastTransition.Turn <= Integer(evidence["turn"]), "Matching active existing guardian III/IV parent required.");
        var selected = ReadParentComponent(parent, proof, "pressure", 5);
        return new(5, null, selected.Parent, selected.EffectId, selected.ComponentId, ordinary, actual,
            ordinaryCalculation, actualCalculation, fingerprint, pointer);
    }
}
