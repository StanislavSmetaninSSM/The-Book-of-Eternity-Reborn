namespace BookOfEternityClient.Services;

internal static class MortalItemPlayerFailureMessages
{
    internal const string StateRequiresRepair =
        "Действие не выполнено: состояние предметов изменилось или требует исправления. Откройте панель заново.";

    private static readonly string[] InternalMarkers =
    {
        "receipt",
        "materialization",
        "identity",
        "itemId",
        "creationRef",
        "carrier",
        "transitionId",
        "индекс идентичности",
        "identity history",
        "game_state/",
        "game_state\\",
        ".json",
        ":\\",
        "item_identity_index",
        "sha256:",
        "mirec_"
    };

    internal static string TransitionRejected() => StateRequiresRepair;

    internal static string Sanitize(string? message, string? fallback = null)
    {
        var safeFallback = string.IsNullOrWhiteSpace(fallback) ? StateRequiresRepair : fallback;
        if (string.IsNullOrWhiteSpace(message))
            return safeFallback;
        return InternalMarkers.Any(marker =>
                message.Contains(marker, StringComparison.OrdinalIgnoreCase))
            ? safeFallback
            : message;
    }
}
