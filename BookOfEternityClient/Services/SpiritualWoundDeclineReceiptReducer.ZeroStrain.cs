using System.Text.Json.Nodes;
using static BookOfEternityClient.Services.SpiritualWoundStateJson;

namespace BookOfEternityClient.Services;

internal static partial class SpiritualWoundDeclineReceiptReducer
{
    /// <summary>Selects comparison DATA already checked by the actual source/C2 owner; the resulting ledger is validated again.</summary>
    private static bool IsZeroStrainComparison(JsonObject witness) =>
        Integer(witness["maximumSeverityRank"]) == 0 && witness["guaranteedSeverityRank"] is null &&
        witness["strainPreimage"] is JsonObject proof && Integer(proof["version"]) == 2;

    /// <summary>Appends no opportunity or decision; preserves existing injury/healing arrays and their exact prefixes.</summary>
    private static void AppendZeroStrainComparison(JsonObject root, string instanceId, JsonObject witness)
    {
        if (!IsZeroStrainComparison(witness))
            throw new FormatException("An increasing zero-ceiling strain comparison is required.");
        if (Integer(root["schemaVersion"]) != 3)
        {
            root["schemaVersion"] = 3;
            if (root["healingSources"] is null)
            {
                root["healingSources"] = new JsonArray(); root["healingDecisions"] = new JsonArray();
                root["nextHealingSourceOrdinal"] = 1; root["nextHealingDecisionOrdinal"] = 1;
            }
            root["strainComparisons"] = new JsonArray(); root["nextStrainComparisonOrdinal"] = 1;
        }
        var rows = root["strainComparisons"]!.AsArray();
        var ordinal = (int)Integer(root["nextStrainComparisonOrdinal"]);
        rows.Add(new JsonObject { ["ordinal"] = ordinal, ["instanceId"] = instanceId,
            ["instanceComparisonOrdinal"] = rows.Count(row => Text(row!["instanceId"]) == instanceId) + 1,
            ["witness"] = witness.DeepClone() });
        root["nextStrainComparisonOrdinal"] = checked(ordinal + 1);
    }
}
