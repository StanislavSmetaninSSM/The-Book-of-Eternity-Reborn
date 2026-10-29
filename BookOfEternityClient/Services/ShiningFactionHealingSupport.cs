using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record ShiningFactionRosterEntry(string Name, string Role, int? HealingTier);

/// <summary>
/// Detached roster/capability DATA over the caller's current parsed roots.
/// This creates no service, access, payment or publication authority.
/// </summary>
internal static class ShiningFactionHealingSupport
{
    internal static bool IsCurrentHealerResident(JsonObject resident, string factionId) =>
        IsCurrentMember(resident, factionId) && string.Equals(Text(resident["residentRole"]),
            ShiningAbodeState.ResidentRoleHealingSupport, StringComparison.OrdinalIgnoreCase);

    internal static bool TryReadHealerTier(JsonObject resident, JsonObject profile, out int tier)
    {
        tier = 0;
        return HasReadableMaterialization(profile) &&
            TryReadHealerTierFromValidatedProfile(resident, profile, out tier);
    }

    // Accepted-turn reference validation supplies its already-resolved profile
    // after checking materialization with actual current trade evidence. Reuse
    // those DATA rather than repeat its lookup, serialization and validation.
    internal static bool TryReadHealerTierFromValidatedProfile(
        JsonObject resident, JsonObject profile, out int tier)
    {
        tier = 0;
        var factionId = Text(resident["shiningFactionId"]);
        if (!IsCurrentHealerResident(resident, factionId) || !MatchesVisibleShiningProfile(resident, profile))
            return false;
        if (profile.TryGetPropertyValue("lifeState", out var life) && Text(life) != "alive")
            return false;
        return AfterlifeEntityProfileState.TryReadRequiredWoundArtTier(profile,
            AfterlifeSpiritualConflictState.SpiritualHealingArtId, out _, out tier) && tier >= 1;
    }

    /// <summary>
    /// The caller has already resolved the faction through the existing revealed
    /// faction gate. This projection additionally filters exact resident/profile
    /// visibility. It must not be used to authorize access to a hidden faction.
    /// </summary>
    internal static IReadOnlyList<ShiningFactionRosterEntry> ProjectVisibleRoster(
        JsonObject faction, JsonObject? residentsRoot, JsonObject? profilesRoot)
    {
        var factionId = Text(faction["factionId"]);
        if (string.IsNullOrWhiteSpace(factionId) || residentsRoot?["entries"] is not JsonArray residents ||
            profilesRoot?["profiles"] is not JsonArray profiles)
            return Array.Empty<ShiningFactionRosterEntry>();

        // Operation-local indexes, not caches: private parsed inputs remain
        // unchanged; nothing survives or grants permission to the next action.
        var residentIndex = residents.OfType<JsonObject>().ToLookup(r => Text(r["residentId"]), StringComparer.Ordinal);
        var profileIndex = profiles.OfType<JsonObject>().ToLookup(p => (Text(p["actorType"]), Text(p["actorId"])));
        var result = new List<ShiningFactionRosterEntry>();
        foreach (var resident in residents.OfType<JsonObject>())
        {
            if (!IsCurrentMember(resident, factionId)) continue;
            var id = Text(resident["residentId"]);
            if (residentIndex[id].Count() != 1) continue;
            var matching = profileIndex[("resident", id)].ToArray();
            if (matching is not { Length: 1 }) continue;
            var profile = matching[0];
            if (!MatchesVisibleShiningProfile(resident, profile) || !HasReadableMaterialization(profile)) continue;
            int? tier = null;
            if (IsCurrentHealerResident(resident, factionId))
            {
                if (!TryReadHealerTierFromValidatedProfile(resident, profile, out var healingTier)) continue;
                tier = healingTier;
            }
            var name = Text(resident["displayName"]);
            if (string.IsNullOrWhiteSpace(name)) name = Text(profile["displayName"]);
            if (string.IsNullOrWhiteSpace(name)) name = "Житель фракции";
            result.Add(new(name, ShiningAbodeState.DescribeResidentRole(Text(resident["residentRole"])), tier));
        }
        return result.AsReadOnly();
    }

    private static bool IsCurrentMember(JsonObject resident, string factionId) =>
        !string.IsNullOrWhiteSpace(factionId) && !string.IsNullOrWhiteSpace(Text(resident["residentId"])) &&
        string.Equals(Text(resident["ascensionState"]), ShiningAbodeState.AscensionStateAscended, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Text(resident["shiningFactionId"]), factionId, StringComparison.Ordinal);

    private static bool MatchesVisibleShiningProfile(JsonObject resident, JsonObject profile) =>
        Text(profile["actorType"]) == "resident" && Text(profile["actorId"]) == Text(resident["residentId"]) &&
        AfterlifeEntityProfileState.TryNormalizeEffectRealm(Text(profile["realm"]), out var realm) && realm == "shining_abode" &&
        AfterlifeProfileVisibility.IsVisibleToPlayer(profile);

    private static bool HasReadableMaterialization(JsonObject profile) =>
        ActorMaterializationContract.ValidateAfterlifeProfile(JsonSerializer.SerializeToElement(profile),
            AfterlifeEntityProfileState.StatePath, requireEnvelope: true, canTradeEvidence: null).Count == 0;
    // Read-only projection deliberately makes no claim about trade authority.
    // The accepted-turn owner retains its existing current-trade proof above.

    private static string Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text)
        ? text ?? string.Empty : string.Empty;
}
