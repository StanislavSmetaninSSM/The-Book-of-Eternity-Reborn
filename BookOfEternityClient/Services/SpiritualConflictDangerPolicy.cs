using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class SpiritualConflictDangerPolicy
{
    internal static int SeverityCap(string? mode) => mode switch
    {
        "training" => 0,
        "controlled" => 2,
        "hostile" or "annihilation" => 4,
        _ => -1
    };

    internal static string? ReadDeclaration(JsonObject? conflict) =>
        conflict?["dangerMode"] is JsonValue value &&
        value.TryGetValue<string>(out var mode) &&
        SeverityCap(mode) >= 0
            ? mode
            : null;

    internal static bool IsOmittedOrExactEcho(JsonObject? candidate, string mode) =>
        candidate is null ||
        !candidate.ContainsKey("dangerMode") ||
        string.Equals(ReadDeclaration(candidate), mode, StringComparison.Ordinal);
}
