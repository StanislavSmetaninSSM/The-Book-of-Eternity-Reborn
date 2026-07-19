using System.Text.Json;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private const string ChaosSeaTradeRealm = "Chaos Sea";
    private const string ShiningAbodeTradeRealm = "Shining Abode";

    private async Task<HashSet<string>> LoadCurrentAfterlifeActorTradeAuthoritiesAsync()
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        AddCurrentGuardianTradeAuthority(
            await _fs.ReadFileAsync(GuardiansStatePath),
            result);
        AddCurrentShiningFactionHeadTradeAuthorities(
            await _fs.ReadFileAsync(ShiningAbodeState.StatePath),
            result);
        return result;
    }

    private static bool HasCurrentAfterlifeActorTradeAuthority(
        JsonElement profile,
        IReadOnlySet<string> authorities)
    {
        if (!TryReadCanonicalAfterlifePreTurnActorIdentity(
                profile,
                out var actorType,
                out var actorId,
                out _) ||
            !TryReadExactNonEmptyString(profile, "realm", out var realm))
        {
            return false;
        }

        var canonicalRealm = NormalizeAfterlifeTradeRealm(realm);
        return canonicalRealm != null &&
               authorities.Contains(BuildAfterlifeTradeAuthorityKey(
                   canonicalRealm,
                   actorType,
                   actorId));
    }

    private static void AddCurrentGuardianTradeAuthority(
        string? json,
        ISet<string> result)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !TryReadExactOptionalProperty(
                    root,
                    "chaosSeaNavigation",
                    out var navigation,
                    out var hasNavigation) ||
                !hasNavigation ||
                navigation.ValueKind != JsonValueKind.Object ||
                !TryReadExactNonEmptyString(navigation, "currentAbodeId", out var currentAbodeId) ||
                !TryReadExactOptionalProperty(
                    root,
                    "activeGuardian",
                    out var activeGuardian,
                    out var hasActiveGuardian) ||
                !hasActiveGuardian ||
                activeGuardian.ValueKind != JsonValueKind.Object ||
                !TryReadExactNonEmptyString(activeGuardian, "guardianId", out var activeGuardianId) ||
                !TryReadOptionalArray(root, "guardians", out var guardians))
            {
                return;
            }

            var matches = 0;
            foreach (var guardian in guardians.EnumerateArray())
            {
                if (!TryReadExactNonEmptyString(guardian, "guardianId", out var guardianId) ||
                    !string.Equals(guardianId, activeGuardianId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!TryReadExactOptionalProperty(
                        guardian,
                        "abode",
                        out var abode,
                        out var hasAbode) ||
                    !hasAbode ||
                    abode.ValueKind != JsonValueKind.Object ||
                    !TryReadExactNonEmptyString(abode, "abodeId", out var abodeId) ||
                    !string.Equals(abodeId, currentAbodeId, StringComparison.Ordinal))
                {
                    return;
                }

                matches++;
            }

            if (matches == 1)
            {
                result.Add(BuildAfterlifeTradeAuthorityKey(
                    ChaosSeaTradeRealm,
                    "guardian",
                    activeGuardianId));
            }
        }
        catch (JsonException)
        {
            // Malformed canonical state cannot provide positive trade authority.
        }
    }

    private static void AddCurrentShiningFactionHeadTradeAuthorities(
        string? json,
        ISet<string> result)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !TryReadOptionalArray(root, "factions", out var factions))
            {
                return;
            }

            foreach (var faction in factions.EnumerateArray())
            {
                if (!TryReadExactInt32(faction, "factionStrength", out var factionStrength) ||
                    ShiningAbodeState.GetTradeTier(factionStrength) < 1 ||
                    !HasOperationalShiningFactionLifecycle(faction) ||
                    !TryReadExactOptionalProperty(
                        faction,
                        "leadership",
                        out var leadership,
                        out var hasLeadership) ||
                    !hasLeadership ||
                    leadership.ValueKind != JsonValueKind.Object ||
                    !TryReadExactNonEmptyString(leadership, "leadershipState", out var leadershipState) ||
                    leadershipState is not (ShiningAbodeState.LeadershipStateSecure or
                        ShiningAbodeState.LeadershipStateContested) ||
                    !TryReadExactNonEmptyString(leadership, "headActorType", out var actorType) ||
                    !TryReadExactNonEmptyString(leadership, "headActorId", out var actorId) ||
                    IsPlayerSoulIdentity(actorType, actorId))
                {
                    continue;
                }

                result.Add(BuildAfterlifeTradeAuthorityKey(
                    ShiningAbodeTradeRealm,
                    actorType,
                    actorId));
            }
        }
        catch (JsonException)
        {
            // Malformed canonical state cannot provide positive trade authority.
        }
    }

    private static bool HasOperationalShiningFactionLifecycle(JsonElement faction)
    {
        if (!TryReadExactOptionalProperty(
                faction,
                "factionLifecycle",
                out var lifecycle,
                out var hasLifecycle))
        {
            return false;
        }
        if (!hasLifecycle)
            return true;
        if (lifecycle.ValueKind != JsonValueKind.Object ||
            !TryReadExactOptionalProperty(
                lifecycle,
                "state",
                out var stateNode,
                out var hasState))
        {
            return false;
        }
        if (!hasState)
            return true;
        if (stateNode.ValueKind != JsonValueKind.String)
            return false;

        var state = stateNode.GetString();
        return string.Equals(
                   state,
                   ShiningAbodeState.FactionLifecycleStateActive,
                   StringComparison.Ordinal) ||
               string.Equals(
                   state,
                   ShiningAbodeState.FactionLifecycleStateWeakened,
                   StringComparison.Ordinal);
    }

    private static bool TryReadExactInt32(
        JsonElement root,
        string propertyName,
        out int value)
    {
        value = 0;
        if (!TryReadExactOptionalProperty(
                root,
                propertyName,
                out var property,
                out var exists) ||
            !exists ||
            property.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        return property.TryGetInt32(out value);
    }

    private static string? NormalizeAfterlifeTradeRealm(string realm)
    {
        if (string.Equals(realm, ChaosSeaTradeRealm, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(realm, "Море Хаоса", StringComparison.OrdinalIgnoreCase))
        {
            return ChaosSeaTradeRealm;
        }

        if (string.Equals(realm, ShiningAbodeTradeRealm, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(realm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase))
        {
            return ShiningAbodeTradeRealm;
        }

        return null;
    }

    private static string BuildAfterlifeTradeAuthorityKey(
        string realm,
        string actorType,
        string actorId) =>
        $"{realm}\u001f{actorType}\u001f{actorId}";
}
