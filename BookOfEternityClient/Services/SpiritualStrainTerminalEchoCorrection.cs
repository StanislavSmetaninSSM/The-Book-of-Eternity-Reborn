using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Retains only a direct-root final player strain echo after the actual corrected exchange is proved.</summary>
internal sealed class SpiritualStrainTerminalEchoCorrection
{
    private readonly JsonNode? _original;
    private readonly JsonNode? _corrected;

    internal SpiritualStrainTerminalEchoCorrection(JsonObject originalActive, JsonObject correctedAfter)
    {
        _original = originalActive["playerSideStrain"]?.DeepClone();
        _corrected = correctedAfter["playerSideStrain"]?.DeepClone();
    }

    internal const string Pointer = "/activeConflict/playerSideStrain";

    internal bool Allows(JsonObject raw, bool correctedOnly = false) =>
        !raw.ContainsKey(AfterlifeSpiritualConflictState.ResponseField) && raw["activeConflict"] is JsonObject active &&
        (JsonNode.DeepEquals(active["playerSideStrain"], _corrected) ||
         !correctedOnly && JsonNode.DeepEquals(active["playerSideStrain"], _original));

    internal bool TryApply(JsonObject raw)
    {
        if (!Allows(raw)) return false;
        raw["activeConflict"]!["playerSideStrain"] = _corrected?.DeepClone();
        return true;
    }
}
