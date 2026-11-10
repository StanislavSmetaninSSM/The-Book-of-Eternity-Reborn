using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Immutable original wallet DATA; presence and the complete subtree are preserved independently of currency interpretation.</summary>
internal sealed record SpiritualTreatmentWalletInvariant
{
    private SpiritualTreatmentWalletInvariant(bool present, string? valueJson) { Present = present; ValueJson = valueJson; }
    private bool Present { get; }
    private string? ValueJson { get; }

    internal static SpiritualTreatmentWalletInvariant Capture(JsonObject soul) => soul.ContainsKey("inkFeathers")
        ? new(true, WoundAcceptedTurnFingerprintWriter.CanonicalJson(soul["inkFeathers"])) : new(false, null);

    internal JsonObject ToJson()
    {
        var before = new JsonObject { ["present"] = Present };
        if (Present) before["value"] = ValueJson is null ? null : JsonNode.Parse(ValueJson);
        return new() { ["walletBefore"] = before };
    }

    internal static SpiritualTreatmentWalletInvariant Parse(JsonNode? value)
    {
        if (value is not JsonObject { Count: 1 } root || root["walletBefore"] is not JsonObject before ||
            before["present"] is not JsonValue flag || !flag.TryGetValue<bool>(out var present) ||
            before.Count != (present ? 2 : 1) || present && !before.ContainsKey("value"))
            throw new InvalidDataException("Negotiated treatment requires an exact presence-aware wallet invariant.");
        return new(present, present ? WoundAcceptedTurnFingerprintWriter.CanonicalJson(before["value"]) : null);
    }

    internal void RequirePreserved(JsonObject proposedSoul)
    {
        if (Capture(proposedSoul) != this)
            throw new InvalidDataException("Negotiated treatment cannot change or create the original Ink Feather wallet.");
    }
}
