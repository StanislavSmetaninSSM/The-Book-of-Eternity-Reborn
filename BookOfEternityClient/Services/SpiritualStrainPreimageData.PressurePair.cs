using System.Text.Json.Nodes;
using static BookOfEternityClient.Services.SpiritualWoundStateJson;

namespace BookOfEternityClient.Services;

internal sealed partial class SpiritualStrainPreimageData
{
    /// <summary>Reads a closed canonical complete III/IV parent list, without granting current or historical authority.</summary>
    /// <param name="source">The fully validated containing actual witness.</param>
    /// <param name="proof">The closed version-4 pair or version-6 group pressure comparison.</param>
    /// <param name="version">Four requires exactly two parents; six requires at least three.</param>
    /// <returns>Complete immutable parent facts after local graph and cross-parent global identity checks.</returns>
    private static IReadOnlyList<SpiritualStrainParentData> ReadPressureParents(JsonObject source, JsonObject proof, int version)
    {
        Require(proof["parents"] is JsonArray rowsForCount &&
            (version == 4 && rowsForCount.Count == 2 || version == 6 && rowsForCount.Count >= 3),
            "Pair v4 requires exactly two parents; group v6 requires at least three.");
        var rows = (JsonArray)proof["parents"]!;
        var parsed = new (WoundMaterializationEnvelope Parent, string EffectId, string ComponentId)[rows.Count];
        var woundIds = new HashSet<string>(StringComparer.Ordinal);
        var woundAliases = new HashSet<string>(StringComparer.Ordinal);
        var effectIds = new HashSet<string>(StringComparer.Ordinal);
        var effectAliases = new HashSet<string>(StringComparer.Ordinal);
        string? previousWoundId = null;
        for (var index = 0; index < rows.Count; index++)
        {
            var row = Object(rows[index]);
            Closed(row, "parentWoundJsonBase64 effectId componentId");
            var parent = ReadParent(source, row, "pressure", version);
            Require(parent.Parent.Severity.Rank is 3 or 4, "Every pressure group member requires a rank-III/IV parent snapshot.");
            var woundId = parent.Parent.WoundId;
            Require((previousWoundId is null || StringComparer.Ordinal.Compare(previousWoundId, woundId) < 0) &&
                woundIds.Add(woundId) && woundAliases.Add(MortalLocationIdentityState.BuildConfusableKey(woundId)),
                "Pressure parents require canonical distinct exact and confusable wound identities.");
            previousWoundId = woundId;
            foreach (var root in parent.Parent.Consequences.OwnedEffectSources.RootBindings)
                Require(effectIds.Add(root.EffectId) &&
                    effectAliases.Add(MortalLocationIdentityState.BuildConfusableKey(root.EffectId)),
                    "Every group-owned root requires a distinct exact and confusable effect identity.");
            // Definition and stack identities remain source-scoped; component identities remain definition-local.
            parsed[index] = parent;
        }
        return parsed.Select(parent => new SpiritualStrainParentData(null, parent.Parent,
            parent.EffectId, parent.ComponentId)).ToArray();
    }
}
