using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using static BookOfEternityClient.Services.SpiritualWoundStateJson;

namespace BookOfEternityClient.Services;

internal sealed partial class SpiritualStrainPreimageData
{
    /// <summary>
    /// Checks distinct closed v3/v4/v6 comparison DATA only after the containing witness has fully validated actual.
    /// This intrinsic read cannot prove original input membership, current applicability or publication authority.
    /// </summary>
    /// <param name="source">Complete containing source, already checked by the witness owner.</param>
    /// <param name="actual">The exact actual exchange decoded and validated by that same witness call.</param>
    /// <param name="actualCalculation">The witness's single completed actual calculation.</param>
    /// <returns>Detached comparison DATA with independently recomputed ordinary facts.</returns>
    internal static SpiritualStrainPreimageData ReadPressureIntrinsic(JsonObject source, JsonObject actual,
        SpiritualWoundCalculation actualCalculation)
    {
        var proof = Object(source["strainPreimage"]);
        var version = ReadVersion(source);
        Require(version is 3 or 4 or 6, "Pressure comparison requires version 3, 4 or 6.");
        Closed(proof, version == 3
            ? "version inputRoleRevision ordinaryExchangeJsonBase64 originalInput parentWoundJsonBase64 effectId componentId ordinaryCalculation"
            : "version inputRoleRevision ordinaryExchangeJsonBase64 originalInput parents ordinaryCalculation");
        Require(Text(proof["inputRoleRevision"]) == (version == 3
                ? "existing_pressure_ordinary_v1" : version == 4
                    ? "existing_pressure_ordinary_pair_v1" : "existing_pressure_ordinary_group_v1"),
            "Unsupported ordinary pressure input role.");
        Require(version != 6 || Integer(source["exchangeOrdinal"]) == 0 && Integer(source["sourceOrdinal"]) == 0,
            "Multi-parent v6 pressure requires exchange and source ordinal zero.");
        var evidence = Object(source["turnEvidence"]);
        Require(Text(evidence["realm"]) == "chaos_sea" && Text(source["affectedSide"]) == "player" &&
            Text(source["affectedActor"]!["actorKind"]) == "player_soul" &&
            Text(source["affectedActor"]!["actorId"]) == "player_soul" &&
            Integer(source["exchangeOrdinal"]) + 1 == Integer(evidence["bounds"]!["exchangeCount"]),
            "Pressure comparison requires the last Chaos Sea player-soul exchange.");
        Require(IsToken(actual["operationType"], "pressure") && IsToken(actual["outcome"], "setback") &&
            actual["incomingAction"] is JsonObject incoming &&
            IsToken(incoming["finalOperationType"] ?? incoming["operationType"], "pressure") &&
            Text(source["operation"]) == "pressure" && Text(source["appliedArtKind"]) == "standard" &&
            source["guaranteedSeverityRank"] is null && source["retraumaWoundId"] is null &&
            actualCalculation.HasIncreasingStrain &&
            actualCalculation.MaximumSeverityRank >= 0 &&
            actual["diceAudit"] is JsonObject,
            "Only increasing non-guaranteed standard pressure/setback comparison with a nonnegative maximum is supported.");
        Require(!actual.ContainsKey("specialArtAudit") && !actual.ContainsKey("specialArtAudits") &&
            actual["actionCostAudit"]?["player"]?["specialArtId"] is null &&
            actual["actionCostAudit"]?["opposition"]?["specialArtId"] is null,
            "Both pressure actions must use standard arts.");

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
            index >= 0 && pointer == prefix + index.ToString(CultureInfo.InvariantCulture),
            "Canonical direct-root exchange pointer required.");

        // Count only the complete validated distinct list, never a caller's scalar contribution claim.
        var parents = version is 4 or 6 ? ReadPressureParents(source, proof, version) : null;

        var ordinary = ReadPressureObject(proof["ordinaryExchangeJsonBase64"]);
        Require(ordinary["before"] is JsonObject && ordinary["after"] is JsonObject,
            "Complete ordinary pressure snapshots required.");
        var ordinaryStrain = Text(ordinary["after"]!["playerSideStrain"]);
        var ordinaryRank = SpiritualWoundOpportunityMath.StrainRank(ordinaryStrain);
        if (version == 3)
            Require(actualCalculation.PreviousStrainRank < ordinaryRank && ordinaryRank is >= 1 and <= 4 &&
                actualCalculation.NewStrainRank == Math.Min(4, ordinaryRank + 1),
                "Pressure comparison requires before < ordinary <= broken and one added step capped at broken.");
        else
            Require(actualCalculation.PreviousStrainRank < ordinaryRank && ordinaryRank is >= 1 and <= 4 &&
                actualCalculation.NewStrainRank == Math.Min(4L, (long)ordinaryRank + parents!.Count),
                "Multi-parent pressure requires increasing ordinary I through IV and one step per parent capped at broken.");
        var expectedActual = ordinary.DeepClone().AsObject();
        expectedActual["after"]!["playerSideStrain"] = actual["after"]!["playerSideStrain"]!.DeepClone();
        Require(JsonNode.DeepEquals(expectedActual, actual),
            "Actual pressure may differ only in its proved added player strain steps.");
        var issues = new List<ValidationIssue>();
        ValidationService.ValidateRetainedSpiritualExchangeShape(ordinary, "retainedSource.ordinaryPressure", issues);
        ValidationService.ValidateSpiritualWoundSourceActionShape(ordinary, "retainedSource.ordinaryPressure", issues);
        ValidationService.ValidateRetainedSpiritualDiceAudit(ordinary, evidence, issues);
        ValidationService.ValidateRetainedSpiritualExchangeRules(ordinary, issues);
        Require(!issues.Any(issue => issue.Severity == IssueSeverity.Error), "Valid complete ordinary pressure required.");

        // All other inputs come from the completed actual witness, not duplicated scalar claims.
        var ordinaryCalculation = SpiritualWoundOpportunityMath.Calculate(
            actualCalculation.Input with { NewStrain = ordinaryStrain });
        Require(ordinaryCalculation is { HasIncreasingStrain: true }, "Valid increasing ordinary source calculation required.");
        var declared = Object(proof["ordinaryCalculation"]);
        Closed(declared, "traumaPressure maximumSeverityRank");
        Require(Integer(declared["traumaPressure"], long.MinValue, long.MaxValue) == ordinaryCalculation!.TraumaPressure &&
            Integer(declared["maximumSeverityRank"], 0, 4) == ordinaryCalculation.MaximumSeverityRank,
            "Declared ordinary pressure calculation differs.");
        if (version is 4 or 6)
            return new(version, parents!, ordinary, actual, ordinaryCalculation, actualCalculation, fingerprint, pointer);
        var parent = ReadParent(source, proof, "pressure", 3);
        Require(parent.Parent.Severity.Rank is 3 or 4, "Existing pressure requires a rank-III/IV parent snapshot.");
        return new(3, null, parent.Parent, parent.EffectId, parent.ComponentId, ordinary, actual,
            ordinaryCalculation, actualCalculation, fingerprint, pointer);
    }

    /// <summary>Decodes and parses a complete pressure payload once, retaining strict UTF-8 and duplicate rejection.</summary>
    /// <param name="encoded">Canonical base64 string containing the complete ordinary JSON object.</param>
    /// <returns>A detached duplicate-checked object; malformed encoding or payload throws.</returns>
    private static JsonObject ReadPressureObject(JsonNode? encoded)
    {
        try { return Parse(DecodeUtf8JsonText(DecodeBase64(encoded))); }
        catch (DecoderFallbackException error) { throw new FormatException("Strict UTF-8 JSON required.", error); }
    }
}
