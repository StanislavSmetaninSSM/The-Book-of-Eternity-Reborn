using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Authors a rank-I position burden for the actual binding offer without changing its operation or source.
    /// </summary>
    /// <param name="opportunityRef">
    /// Exact reference in the client-issued decision request.
    /// </param>
    /// <param name="strong">
    /// Targets force binding instead of ordinary binding.
    /// </param>
    /// <returns>
    /// One complete GM-authored decision with magnitude one.
    /// </returns>
    internal static JsonElement CreateBindingLifecycleDecision(string opportunityRef, bool strong)
    {
        var decision = JsonNode.Parse(OriginalSpiritualWoundDecision(opportunityRef,
            "spiritual_position_burden", strong ? "force_binding" : "binding", "player").GetRawText())!;
        decision["proposal"]!["consequenceDefinitions"]![0]!["definition"]!["components"]![0]!["payload"]!["magnitude"] = 1;
        return JsonSerializer.SerializeToElement(decision);
    }

    /// <summary>
    /// Authors the last exchange's exact failed result and arithmetic while leaving its final control echo unchanged.
    /// </summary>
    /// <param name="original">
    /// Original direct two-exchange conflict draft.
    /// </param>
    /// <param name="strong">
    /// Selects the surviving plus-one position of force binding.
    /// </param>
    /// <returns>
    /// A detached A draft using the existing no_effect outcome and exact before-control value.
    /// </returns>
    internal static JsonObject CreateBindingLifecycleCorrectionA(JsonObject original, bool strong)
    {
        var copy = original.DeepClone().AsObject();
        var binding = copy["activeConflict"]!["exchangeLog"]![1]!;
        binding["outcome"] = "no_effect";
        binding["after"]!["controlState"] = binding["before"]!["controlState"]!.DeepClone();
        binding["diceAudit"]!["modifierBreakdown"]!["player"] = strong
            ? new JsonArray(new JsonObject { ["modifierType"] = "conflict_position", ["source"] = "conflictPosition",
                ["position"] = "player_advantaged", ["value"] = 2 }) : new JsonArray();
        binding["diceAudit"]!["playerTotal"] = strong ? 15 : 13;
        binding["diceAudit"]!["margin"] = strong ? 4 : 3;
        return copy;
    }
}
