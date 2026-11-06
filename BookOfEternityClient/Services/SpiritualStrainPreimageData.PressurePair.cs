using System.Text.Json.Nodes;
using static BookOfEternityClient.Services.SpiritualWoundStateJson;

namespace BookOfEternityClient.Services;

internal sealed partial class SpiritualStrainPreimageData
{
    /// <summary>Reads exactly two canonical complete III/IV parents, without granting current or historical authority.</summary>
    /// <param name="source">The fully validated containing actual witness.</param>
    /// <param name="proof">The closed version-4 pressure comparison.</param>
    /// <returns>Complete immutable parent facts after local graph and cross-parent global identity checks.</returns>
    private static IReadOnlyList<SpiritualStrainParentData> ReadPressurePairParents(JsonObject source, JsonObject proof)
    {
        Require(proof["parents"] is JsonArray { Count: 2 }, "Pair pressure requires exactly two parents.");
        var rows = (JsonArray)proof["parents"]!;
        var parsed = new (WoundMaterializationEnvelope Parent, string EffectId, string ComponentId)[2];
        var woundIds = new HashSet<string>(StringComparer.Ordinal);
        var woundAliases = new HashSet<string>(StringComparer.Ordinal);
        var effectIds = new HashSet<string>(StringComparer.Ordinal);
        var effectAliases = new HashSet<string>(StringComparer.Ordinal);
        string? previousWoundId = null;
        for (var index = 0; index < rows.Count; index++)
        {
            var row = Object(rows[index]);
            Closed(row, "parentWoundJsonBase64 effectId componentId");
            var parent = ReadParent(source, row, "pressure", 4);
            Require(parent.Parent.Severity.Rank is 3 or 4, "Pair pressure requires two rank-III/IV parent snapshots.");
            var woundId = parent.Parent.WoundId;
            Require((previousWoundId is null || StringComparer.Ordinal.Compare(previousWoundId, woundId) < 0) &&
                woundIds.Add(woundId) && woundAliases.Add(MortalLocationIdentityState.BuildConfusableKey(woundId)),
                "Pair parents require canonical distinct exact and confusable wound identities.");
            previousWoundId = woundId;
            foreach (var root in parent.Parent.Consequences.OwnedEffectSources.RootBindings)
                Require(effectIds.Add(root.EffectId) &&
                    effectAliases.Add(MortalLocationIdentityState.BuildConfusableKey(root.EffectId)),
                    "Every pair-owned root requires a distinct exact and confusable effect identity.");
            // Definition and stack identities remain source-scoped; component identities remain definition-local.
            parsed[index] = parent;
        }
        return parsed.Select(parent => new SpiritualStrainParentData(null, parent.Parent,
            parent.EffectId, parent.ComponentId)).ToArray();
    }
}
