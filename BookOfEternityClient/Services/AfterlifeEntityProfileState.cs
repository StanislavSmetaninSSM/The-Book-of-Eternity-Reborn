using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class AfterlifeEntityProfileState
{
    public const string StatePath = "game_state/meta/afterlife_entity_profiles.json";
    public const string ProfilesProperty = "profiles";
    public const string ResponseProfilesProperty = "afterlifeEntityProfiles";
    public const string UpdateProperty = "afterlifeEntityProfileUpdates";
    public const int SchemaVersion = 1;
    public const int MaxProfileTier = 5;

    public static readonly HashSet<string> ActorTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "player_soul",
        "guardian",
        "resident",
        "shining_faction_head",
        "radiant_actor",
        "custom_afterlife_actor"
    };

    public static readonly HashSet<string> Realms = new(StringComparer.OrdinalIgnoreCase)
    {
        "Chaos Sea",
        "Море Хаоса",
        "Shining Abode",
        "Сияющая Обитель"
    };

    public static readonly HashSet<string> StandardArtIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "pressure",
        "counter",
        "guard",
        "maneuver",
        "break_binding",
        "binding",
        "force_binding",
        "incarnation_resistance",
        "champion_coordination",
        "recover_spiritual_power"
    };

    public static readonly HashSet<string> SpecialArtBaseOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "pressure",
        "counter",
        "guard",
        "maneuver",
        "binding",
        "break_binding",
        "force_binding",
        "incarnation_resistance",
        "champion_coordination",
        "recover_spiritual_power"
    };

    public static JsonObject CreateDefaultRoot() =>
        new()
        {
            ["schemaVersion"] = SchemaVersion,
            [ProfilesProperty] = new JsonArray()
        };

    public static JsonObject ProjectCanonicalRoot(JsonObject? currentRoot, JsonObject? previousRoot)
    {
        var result = CreateDefaultRoot();

        UpsertProfiles(result, previousRoot?[ProfilesProperty]);
        UpsertProfiles(result, currentRoot?[ProfilesProperty]);
        UpsertProfiles(result, currentRoot?[ResponseProfilesProperty]);
        UpsertProfiles(result, currentRoot?[UpdateProperty]);

        result.Remove(UpdateProperty);
        result.Remove(ResponseProfilesProperty);
        return result;
    }

    public static void UpsertProfile(JsonArray profiles, JsonObject profile)
    {
        var identityKey = BuildIdentityKey(profile);
        if (string.IsNullOrWhiteSpace(identityKey))
        {
            profiles.Add(CloneObject(profile));
            return;
        }

        for (var index = 0; index < profiles.Count; index++)
        {
            if (profiles[index] is not JsonObject existing)
                continue;

            if (!string.Equals(BuildIdentityKey(existing), identityKey, StringComparison.OrdinalIgnoreCase))
                continue;

            profiles[index] = CloneObject(profile);
            return;
        }

        profiles.Add(CloneObject(profile));
    }

    public static string? BuildIdentityKey(JsonObject? profile)
    {
        if (profile == null)
            return null;

        var actorType = GetNodeString(profile["actorType"]);
        var actorId = GetNodeString(profile["actorId"]) ?? GetNodeString(profile["actorRef"]);
        return string.IsNullOrWhiteSpace(actorType) || string.IsNullOrWhiteSpace(actorId)
            ? null
            : $"{actorType.Trim()}:{actorId.Trim()}";
    }

    public static string? GetNodeString(JsonNode? node)
    {
        if (node == null)
            return null;

        if (node is JsonValue value && value.TryGetValue<string>(out var result))
            return string.IsNullOrWhiteSpace(result) ? null : result.Trim();

        return null;
    }

    public static int GetNodeInt(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<int>(out var result))
            return result;

        return 0;
    }

    private static void UpsertProfiles(JsonObject result, JsonNode? profilesNode)
    {
        if (profilesNode is not JsonArray profiles)
            return;

        var resultProfiles = EnsureProfilesArray(result);
        foreach (var profile in profiles.OfType<JsonObject>())
            UpsertProfile(resultProfiles, profile);
    }

    private static JsonArray EnsureProfilesArray(JsonObject root)
    {
        if (root[ProfilesProperty] is JsonArray profiles)
            return profiles;

        profiles = new JsonArray();
        root[ProfilesProperty] = profiles;
        return profiles;
    }

    private static JsonObject CloneObject(JsonObject source) =>
        source.DeepClone() as JsonObject ?? new JsonObject();
}
