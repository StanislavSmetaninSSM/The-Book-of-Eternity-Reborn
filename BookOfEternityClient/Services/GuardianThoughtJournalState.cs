using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class GuardianThoughtJournalState
{
    public const string StatePath = "game_state/meta/guardian_thought_journal.json";
    public const string UpdateProperty = "guardianThoughtJournalUpdates";
    public const string ActorIdProperty = "guardianId";

    public static JsonArray EnsureEntriesArray(JsonObject root)
        => ActorJournalState.EnsureEntriesArray(root, ActorIdProperty, UpdateProperty);

    public static void ApplyUpdates(JsonObject root, JsonArray updates)
        => ActorJournalState.ApplyUpdates(root, updates, ActorIdProperty, UpdateProperty);

    public static IEnumerable<JsonObject> CollectEntries(JsonNode? root)
        => ActorJournalState.CollectEntries(root, ActorIdProperty, UpdateProperty);
}
