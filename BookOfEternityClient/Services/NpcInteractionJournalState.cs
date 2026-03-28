using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class NpcInteractionJournalState
{
    public const string StatePath = "game_state/npcs/npc_interaction_journal.json";
    public const string UpdateProperty = "npcInteractionJournalUpdates";
    public const string ActorIdProperty = "npcId";

    public static JsonArray EnsureEntriesArray(JsonObject root)
        => ActorJournalState.EnsureEntriesArray(root, ActorIdProperty, UpdateProperty);

    public static void ApplyUpdates(JsonObject root, JsonArray updates)
        => ActorJournalState.ApplyUpdates(root, updates, ActorIdProperty, UpdateProperty);

    public static IEnumerable<JsonObject> CollectEntries(JsonNode? root)
        => ActorJournalState.CollectEntries(root, ActorIdProperty, UpdateProperty);
}
