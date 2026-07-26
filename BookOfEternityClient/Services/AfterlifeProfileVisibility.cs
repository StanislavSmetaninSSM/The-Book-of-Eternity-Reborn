using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class AfterlifeProfileVisibility
{
    private static readonly HashSet<string> HiddenVisibilityValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "concealed",
        "gm_only",
        "gmonly",
        "hidden",
        "internal",
        "private",
        "secret",
        "spoiler"
    };

    public static bool IsVisibleToPlayer(JsonObject profile)
    {
        if (string.Equals(
                ReadString(profile["actorType"]),
                ActorMaterializationContract.SystemActorType,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IsFalse(profile["isPlayerVisible"]) ||
            IsFalse(profile["playerVisible"]) ||
            IsFalse(profile["visibleToPlayer"]) ||
            IsFalse(profile["visibleForPlayer"]))
        {
            return false;
        }

        if (IsTrue(profile["isHidden"]) ||
            IsTrue(profile["hidden"]) ||
            IsTrue(profile["isSecret"]) ||
            IsTrue(profile["secret"]) ||
            IsTrue(profile["gmOnly"]) ||
            IsTrue(profile["isGmOnly"]) ||
            IsTrue(profile["internal"]) ||
            IsTrue(profile["isInternal"]))
        {
            return false;
        }

        return !IsHiddenVisibility(profile["visibility"]) &&
               !IsHiddenVisibility(profile["audience"]);
    }

    private static bool IsHiddenVisibility(JsonNode? node) =>
        HiddenVisibilityValues.Contains(ReadString(node));

    private static bool IsTrue(JsonNode? node) =>
        node is JsonValue value &&
        value.TryGetValue<bool>(out var result) &&
        result;

    private static bool IsFalse(JsonNode? node) =>
        node is JsonValue value &&
        value.TryGetValue<bool>(out var result) &&
        !result;

    private static string ReadString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var result)
            ? result?.Trim() ?? string.Empty
            : string.Empty;
}
