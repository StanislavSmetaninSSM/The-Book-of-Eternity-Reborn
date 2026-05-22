using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class CraftRequestState
{
    public const string PendingRequestPath = "game_state/control/pending_craft_request.json";

    public static async Task<string?> BuildSystemReminderFragmentAsync(FileSystemManager fs, string? currentRealm)
    {
        if (!IsMortalRealm(currentRealm) || !fs.FileExists(PendingRequestPath))
            return null;

        var raw = await fs.ReadFileAsync(PendingRequestPath);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Join("\n",
                "MORTAL CRAFT REQUEST:",
                $"- {PendingRequestPath} exists but is empty. Treat it as malformed client-authored crafting state and ask for repair before resolving crafting.");
        }

        JsonObject? request;
        try
        {
            request = JsonNode.Parse(raw) as JsonObject;
        }
        catch
        {
            request = null;
        }

        if (request == null)
        {
            return string.Join("\n",
                "MORTAL CRAFT REQUEST:",
                $"- {PendingRequestPath} exists but is not a readable JSON object. Treat it as malformed client-authored crafting state and ask for repair before resolving crafting.");
        }

        var requestId = ReadString(request, "requestId");
        var recipeId = ReadString(request, "recipeId");
        var craftIntent = ReadString(request, "craftIntent");
        var status = ReadString(request, "status");

        return string.Join("\n",
            "MORTAL CRAFT REQUEST:",
            $"- Pending client-authored request file: {PendingRequestPath}.",
            $"- requestId={Display(requestId)}, status={Display(status)}, recipeId={Display(recipeId)}.",
            $"- craftIntent: {Display(Truncate(craftIntent, 400))}",
            "- Resolve this Mortal World crafting request in the next GM response. On success, materialize the item/material changes through UpdateInventory or canonical inventory state; on failure, explain the failed craft in narrative_response and do not invent unrelated rewards.",
            "- Do not ignore this request while authoring the turn; keep the outcome tied to the requestId/recipeId/craftIntent above.");
    }

    private static bool IsMortalRealm(string? currentRealm)
    {
        if (string.IsNullOrWhiteSpace(currentRealm))
            return false;

        return !string.Equals(currentRealm, "Chaos Sea", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(currentRealm, "Море Хаоса", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(currentRealm, "Shining Abode", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(currentRealm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadString(JsonObject obj, string propertyName)
    {
        if (!obj.TryGetPropertyValue(propertyName, out var node) || node is not JsonValue value)
            return null;

        if (value.TryGetValue<string>(out var text))
            return text;
        if (value.TryGetValue<int>(out var number))
            return number.ToString();
        if (value.TryGetValue<bool>(out var flag))
            return flag ? "true" : "false";

        return null;
    }

    private static string Display(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "<missing>" : value.Trim();

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : trimmed[..maxLength] + "...";
    }
}
