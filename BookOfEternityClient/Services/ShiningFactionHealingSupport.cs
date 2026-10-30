using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record ShiningFactionRosterEntry(string ResidentId, string Name, string Role, int? HealingTier,
    AfterlifeHealingServiceDisplayTerms? DisplayTerms);

/// <summary>
/// Detached roster/capability DATA over the caller's current parsed roots.
/// This creates no service, access, payment or publication authority.
/// </summary>
internal static partial class ShiningFactionHealingSupport
{
    /// <summary>Requires a living healer unless the authored lifecycle is valid broken/dissolved history.</summary>
    internal static bool RequiresLivingHealer(JsonObject faction) =>
        faction["factionLifecycle"] is not JsonObject || !ShiningAbodeState.IsFactionDefeated(faction);

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
        return AfterlifeProfileVisibility.IsVisibleToPlayer(profile) &&
            TryReadCanonicalHealerTierFromValidatedProfile(resident, profile, out tier);
    }

    /// <summary>Reads canonical capability independently of player knowledge, after exact materialization validation.</summary>
    /// <param name="resident">The unique current ascended resident and exact faction affiliation.</param>
    /// <param name="profile">The caller's already validated exact materialized profile; no authority is exported.</param>
    /// <param name="tier">The explicitly authored integer Healing tier, when qualified.</param>
    internal static bool TryReadCanonicalHealerTierFromValidatedProfile(
        JsonObject resident, JsonObject profile, out int tier)
    {
        tier = 0;
        var factionId = Text(resident["shiningFactionId"]);
        if (!IsCurrentHealerResident(resident, factionId) || !MatchesShiningProfile(resident, profile))
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
        return ProjectVisibleRoster(faction, residents, residentIndex, profileIndex);
    }

    // One caller operation may project several factions. Reuse its exact indexes;
    // each distinct profile still receives its real materialization validation once.
    private static IReadOnlyList<ShiningFactionRosterEntry> ProjectVisibleRoster(JsonObject faction,
        JsonArray residents, ILookup<string, JsonObject> residentIndex,
        ILookup<(string, string), JsonObject> profileIndex, string? onlyResidentId = null)
    {
        var factionId = Text(faction["factionId"]);
        var result = new List<ShiningFactionRosterEntry>();
        foreach (var resident in residents.OfType<JsonObject>())
        {
            if (!IsCurrentMember(resident, factionId)) continue;
            var id = Text(resident["residentId"]);
            if (onlyResidentId is not null && id != onlyResidentId || residentIndex[id].Count() != 1) continue;
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
            var terms = tier is null ? null : AfterlifeHealingServiceContract.ReadPublicDisplayTerms(
                new("shining_abode", "resident", id, WoundCarrierCatalog.AfterlifeProfilesPath), profile);
            result.Add(new(id, name, ShiningAbodeState.DescribeResidentRole(Text(resident["residentRole"])), tier, terms));
        }
        return result.AsReadOnly();
    }

    internal static string DescribeMember(ShiningFactionRosterEntry member) =>
        member.Name + " — " + DescribeRoleAndTier(member);

    internal static string DescribeRoleAndTier(ShiningFactionRosterEntry member) =>
        member.Role + (member.HealingTier is int tier ? "; Исцеление " + RomanTier(tier) : string.Empty);

    internal static IReadOnlyList<string> DescribeAccess(ShiningFactionRosterEntry member)
    {
        if (member.HealingTier is not int tier) return Array.Empty<string>();
        var lines = new List<string>();
        if (member.DisplayTerms is not { } terms)
            lines.Add("Подтверждённые публичные условия помощи пока не объявлены.");
        else
        {
            lines.Add(terms.DeclaredAvailable
                ? "Целитель объявил приём открытым; ваш доступ нужно подтвердить отдельно."
                : "Объявленный приём временно недоступен.");
            if (terms.MinimumRelationship is int relationship)
                lines.Add("Требуемый уровень отношений: не ниже " + relationship.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
            foreach (var condition in terms.AccessConditions) lines.Add("Условие: " + condition);
            lines.Add("Виды вознаграждения: " + string.Join(", ", terms.CompensationKinds.Select(kind => kind switch
            {
                "ink_feathers" => "Чернильные Перья", "favor" => "ответная услуга", "debt" => "долг",
                "quest" => "выполнение задания", "allegiance" => "обязательство верности", "free_aid" => "безвозмездная помощь",
                _ => "не подтверждено"
            })) + ".");
            if (terms.CompensationKinds.Contains("ink_feathers", StringComparer.Ordinal))
                for (var severity = 1; severity <= Math.Min(tier, 4); severity++)
                    if (AfterlifeHealingServiceContract.CalculatePrice(severity, terms.MultiplierPercent) is int price)
                        lines.Add("Объявленный тариф для раны " + RomanTier(severity) + ": " +
                            price.ToString(System.Globalization.CultureInfo.InvariantCulture) + " Чернильных Перьев.");
        }
        lines.Add(member.DisplayTerms is { DeclaredAvailable: true, MinimumRelationship: null } current &&
            current.AccessConditions.Count == 0 && current.CompensationKinds.Contains("ink_feathers", StringComparer.Ordinal)
            ? "Проверить подходящую рану и текущую цену можно через /лечить. Зал, условия и состояние целителя проверяются заново; затем требуется отдельное подтверждение."
            : "Лечение через команду этого целителя пока недоступно.");
        return lines.AsReadOnly();
    }

    private static string RomanTier(int tier) => tier switch
    {
        1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => "—"
    };

    private static bool IsCurrentMember(JsonObject resident, string factionId) =>
        !string.IsNullOrWhiteSpace(factionId) && !string.IsNullOrWhiteSpace(Text(resident["residentId"])) &&
        string.Equals(Text(resident["ascensionState"]), ShiningAbodeState.AscensionStateAscended, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Text(resident["shiningFactionId"]), factionId, StringComparison.Ordinal);

    private static bool MatchesVisibleShiningProfile(JsonObject resident, JsonObject profile) =>
        MatchesShiningProfile(resident, profile) && AfterlifeProfileVisibility.IsVisibleToPlayer(profile);

    private static bool MatchesShiningProfile(JsonObject resident, JsonObject profile) =>
        Text(profile["actorType"]) == "resident" && Text(profile["actorId"]) == Text(resident["residentId"]) &&
        AfterlifeEntityProfileState.TryNormalizeEffectRealm(Text(profile["realm"]), out var realm) && realm == "shining_abode";

    private static bool HasReadableMaterialization(JsonObject profile) =>
        ActorMaterializationContract.ValidateAfterlifeProfile(JsonSerializer.SerializeToElement(profile),
            AfterlifeEntityProfileState.StatePath, requireEnvelope: true, canTradeEvidence: null).Count == 0;
    // Read-only projection deliberately makes no claim about trade authority.
    // The accepted-turn owner retains its existing current-trade proof above.

    private static string Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text)
        ? text ?? string.Empty : string.Empty;
}
