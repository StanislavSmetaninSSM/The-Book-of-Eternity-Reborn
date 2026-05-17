using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class AfterlifeEntityProfileState
{
    public const string StatePath = "game_state/meta/afterlife_entity_profiles.json";
    public const string ProfilesProperty = "profiles";
    public const string ResponseProfilesProperty = "afterlifeEntityProfiles";
    public const string UpdateProperty = "afterlifeEntityProfileUpdates";
    public const string CustomStateChangesProperty = "afterlifeEntityCustomStateChanges";
    public const string CustomStatesProperty = "customStates";
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
        ApplyCustomStateChanges(result, currentRoot?[CustomStateChangesProperty]);

        result.Remove(UpdateProperty);
        result.Remove(ResponseProfilesProperty);
        result.Remove(CustomStateChangesProperty);
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

    private static void ApplyCustomStateChanges(JsonObject result, JsonNode? changesNode)
    {
        if (changesNode is not JsonArray changes)
            return;

        var profiles = EnsureProfilesArray(result);
        foreach (var change in changes.OfType<JsonObject>())
        {
            var targetKey = BuildIdentityKey(change);
            if (string.IsNullOrWhiteSpace(targetKey))
                continue;

            var profile = profiles
                .OfType<JsonObject>()
                .FirstOrDefault(item => string.Equals(BuildIdentityKey(item), targetKey, StringComparison.OrdinalIgnoreCase));
            if (profile == null)
                continue;

            if (change["statesToRemove"] is JsonArray removals)
                RemoveCustomStates(profile, removals);

            if (change["statesToAddOrUpdate"] is JsonArray upserts)
                UpsertCustomStates(profile, upserts);
        }
    }

    private static void UpsertCustomStates(JsonObject profile, JsonArray upserts)
    {
        var states = EnsureCustomStatesArray(profile);
        foreach (var state in upserts.OfType<JsonObject>())
        {
            var identity = BuildCustomStateIdentity(state);
            if (string.IsNullOrWhiteSpace(identity))
            {
                states.Add(CloneObject(state));
                continue;
            }

            var replaced = false;
            for (var index = 0; index < states.Count; index++)
            {
                if (states[index] is not JsonObject existing)
                    continue;

                if (!string.Equals(BuildCustomStateIdentity(existing), identity, StringComparison.OrdinalIgnoreCase))
                    continue;

                states[index] = CloneObject(state);
                replaced = true;
                break;
            }

            if (!replaced)
                states.Add(CloneObject(state));
        }
    }

    private static void RemoveCustomStates(JsonObject profile, JsonArray removals)
    {
        if (profile[CustomStatesProperty] is not JsonArray states)
            return;

        var removeIds = removals
            .Select(GetNodeString)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (removeIds.Count == 0)
            return;

        for (var index = states.Count - 1; index >= 0; index--)
        {
            if (states[index] is JsonObject state &&
                removeIds.Contains(BuildCustomStateIdentity(state) ?? string.Empty))
            {
                states.RemoveAt(index);
            }
        }
    }

    private static JsonArray EnsureCustomStatesArray(JsonObject profile)
    {
        if (profile[CustomStatesProperty] is JsonArray states)
            return states;

        states = new JsonArray();
        profile[CustomStatesProperty] = states;
        return states;
    }

    private static string? BuildCustomStateIdentity(JsonObject state) =>
        GetNodeString(state["stateId"]) ??
        GetNodeString(state["stateKey"]) ??
        GetNodeString(state["key"]) ??
        GetNodeString(state["name"]) ??
        GetNodeString(state["title"]) ??
        GetNodeString(state["stateName"]);

    private static JsonObject CloneObject(JsonObject source) =>
        source.DeepClone() as JsonObject ?? new JsonObject();
}
