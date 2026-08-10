using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal enum LocalInteractionRealmKind
{
    Unknown,
    Mortal,
    ChaosSea,
    ShiningAbode
}

internal sealed record LocalInteractionAuthoritySnapshot(string Path, string? Json);

internal sealed record LocalInteractionScope(
    LocalInteractionRealmKind RealmKind,
    bool IsResolved,
    string LocationId,
    string LocationName,
    string CurrentGuardianId,
    IReadOnlySet<string> LocalActorIds,
    IReadOnlySet<string> LocalFactionIds,
    string? UnavailableReason,
    IReadOnlyList<LocalInteractionAuthoritySnapshot> AuthoritySnapshots)
{
    public static LocalInteractionScope Unresolved(
        LocalInteractionRealmKind realmKind,
        string reason,
        IReadOnlyList<LocalInteractionAuthoritySnapshot>? authoritySnapshots = null) =>
        new(
            realmKind,
            IsResolved: false,
            LocationId: string.Empty,
            LocationName: string.Empty,
            CurrentGuardianId: string.Empty,
            LocalActorIds: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            LocalFactionIds: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            UnavailableReason: reason,
            AuthoritySnapshots: authoritySnapshots ?? Array.Empty<LocalInteractionAuthoritySnapshot>());
}

internal interface ILocalInteractionScopeResolver
{
    Task<LocalInteractionScope> ResolveAsync(string? currentRealm = null);

    Task<LocalInteractionScope> ResolveAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string? currentRealm = null);
}

internal sealed class LocalInteractionScopeService : ILocalInteractionScopeResolver
{
    private sealed record AuthorityRead(JsonObject? Root, LocalInteractionAuthoritySnapshot Snapshot);

    private const string SoulStatePath = "game_state/meta/soul_state.json";
    private const string MortalLocationPath = "game_state/world/current_location.json";
    private const string GuardiansPath = "game_state/meta/guardians.json";
    private const string ShiningAbodePath = "game_state/meta/shining_abode_state.json";

    private readonly FileSystemManager _fs;

    public LocalInteractionScopeService(FileSystemManager fs)
    {
        _fs = fs;
    }

    public async Task<LocalInteractionScope> ResolveAsync(string? currentRealm = null) =>
        await ResolveCoreAsync(writeLease: null, currentRealm);

    public async Task<LocalInteractionScope> ResolveAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string? currentRealm = null) =>
        await ResolveCoreAsync(writeLease, currentRealm);

    private async Task<LocalInteractionScope> ResolveCoreAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        string? currentRealm)
    {
        var soulRead = await ReadAuthorityObjectAsync(writeLease, SoulStatePath);
        currentRealm ??= GetString(soulRead.Root?["currentRealm"]);
        var authoritySnapshots = new[] { soulRead.Snapshot };
        switch (ClassifyRealm(currentRealm))
        {
            case LocalInteractionRealmKind.ChaosSea:
                return await ResolveChaosSeaAsync(writeLease, authoritySnapshots);
            case LocalInteractionRealmKind.ShiningAbode:
                return await ResolveShiningAbodeAsync(writeLease, authoritySnapshots);
            case LocalInteractionRealmKind.Mortal:
                return await ResolveMortalAsync(writeLease, authoritySnapshots);
            default:
                return LocalInteractionScope.Unresolved(
                    LocalInteractionRealmKind.Unknown,
                    LooksLikeNonCanonicalAfterlifeRealm(currentRealm)
                        ? "Текущая посмертная реальность указана неоднозначно. Выберите Море Хаоса или Сияющую Обитель."
                        : "Текущий мир не определён, поэтому локальные взаимодействия недоступны.",
                    authoritySnapshots);
        }
    }

    internal static LocalInteractionRealmKind ClassifyRealm(string? realm)
    {
        if (RealmSemantics.IsChaosSea(realm))
            return LocalInteractionRealmKind.ChaosSea;
        if (RealmSemantics.IsShiningRealm(realm))
            return LocalInteractionRealmKind.ShiningAbode;
        if (LooksLikeNonCanonicalAfterlifeRealm(realm))
            return LocalInteractionRealmKind.Unknown;
        return RealmSemantics.IsMortalRealm(realm)
            ? LocalInteractionRealmKind.Mortal
            : LocalInteractionRealmKind.Unknown;
    }

    public static bool IsMortalActorLocal(LocalInteractionScope scope, JsonObject actor)
    {
        if (!scope.IsResolved || scope.RealmKind != LocalInteractionRealmKind.Mortal)
            return false;

        var locationIds = GetNonEmptyStrings(
            actor,
            "currentLocationId",
            "initialLocationId",
            "locationId");
        var locationNames = GetNonEmptyStrings(
            actor,
            "currentLocation",
            "currentLocationName",
            "locationName");
        return AliasesMatchLocation(locationIds, locationNames, scope.LocationId, scope.LocationName);
    }

    public static bool IsAfterlifeActorLocal(LocalInteractionScope scope, JsonObject actor)
    {
        if (!scope.IsResolved)
            return false;

        var actorRealm = GetFirstString(actor, "realm", "currentRealm", "sourceRealm");
        var actorId = GetActorId(actor);

        if (scope.RealmKind == LocalInteractionRealmKind.ChaosSea)
        {
            if (string.IsNullOrWhiteSpace(actorRealm))
                return EqualsNonEmpty(actorId, scope.CurrentGuardianId);
            if (!RealmSemantics.IsChaosSea(actorRealm))
                return false;

            return Contains(scope.LocalActorIds, actorId) ||
                   AliasesMatchLocation(
                       GetNonEmptyStrings(actor, "abodeId", "currentAbodeId", "locationId", "currentLocationId"),
                       GetNonEmptyStrings(actor, "abodeName", "currentAbodeName", "locationName", "currentLocation"),
                       scope.LocationId,
                       scope.LocationName);
        }

        if (scope.RealmKind != LocalInteractionRealmKind.ShiningAbode)
            return false;
        if (string.IsNullOrWhiteSpace(actorRealm) || !RealmSemantics.IsShiningRealm(actorRealm))
            return false;

        var directHallIds = GetNonEmptyStrings(
            actor,
            StringComparer.Ordinal,
            "hallId",
            "currentHallId",
            "locationId",
            "currentLocationId");
        var directHallNames = GetNonEmptyStrings(
            actor,
            StringComparer.OrdinalIgnoreCase,
            "hallName",
            "currentHallName",
            "locationName",
            "currentLocation");
        var hasDirectHallEvidence = directHallIds.Count > 0 || directHallNames.Count > 0;
        if (hasDirectHallEvidence &&
            !ShiningAliasesMatchLocation(
                directHallIds,
                directHallNames,
                scope.LocationId,
                scope.LocationName))
        {
            return false;
        }

        var factionId = GetFirstString(actor, "shiningFactionId", "currentFactionId", "factionId", "originFactionId");
        if (!string.IsNullOrWhiteSpace(factionId) && !Contains(scope.LocalFactionIds, factionId))
            return false;

        return Contains(scope.LocalActorIds, actorId) ||
               Contains(scope.LocalFactionIds, factionId) ||
               hasDirectHallEvidence;
    }

    public static bool IsShiningFactionLocal(LocalInteractionScope scope, JsonObject faction)
    {
        if (!scope.IsResolved || scope.RealmKind != LocalInteractionRealmKind.ShiningAbode)
            return false;

        var factionId = GetFirstString(faction, "factionId", "id", "initialId");
        return Contains(scope.LocalFactionIds, factionId) &&
               EqualsExactNonEmpty(
                   GetFirstString(faction, "hallId"),
                   scope.LocationId);
    }

    public static bool MatchesLocation(
        string? actorLocationId,
        string? actorLocationName,
        string? currentLocationId,
        string? currentLocationName)
    {
        var actorAliases = BuildAliasSet(actorLocationId, actorLocationName);
        var currentAliases = BuildAliasSet(currentLocationId, currentLocationName);
        if (actorAliases.Count == 0 || currentAliases.Count == 0 || !actorAliases.Overlaps(currentAliases))
            return false;

        return actorAliases.Count <= 1 ||
               currentAliases.Count <= 1 ||
               actorAliases.SetEquals(currentAliases);
    }

    private async Task<LocalInteractionScope> ResolveMortalAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        IReadOnlyList<LocalInteractionAuthoritySnapshot> authoritySnapshots)
    {
        var locationRead = await ReadAuthorityObjectAsync(writeLease, MortalLocationPath);
        var root = locationRead.Root;
        var resolvedSnapshots = AppendSnapshot(authoritySnapshots, locationRead.Snapshot);
        var location = root?["currentLocationData"] as JsonObject ?? root;
        var locationId = GetFirstString(location, "locationId", "currentLocationId", "initialId", "id");
        var locationName = GetFirstString(location, "name", "currentLocation", "currentLocationName", "displayName");
        if (string.IsNullOrWhiteSpace(locationId) && string.IsNullOrWhiteSpace(locationName))
        {
            return LocalInteractionScope.Unresolved(
                LocalInteractionRealmKind.Mortal,
                "Текущая локация не определена. Обучение и торговля доступны только рядом с персонажем.",
                resolvedSnapshots);
        }

        return new LocalInteractionScope(
            LocalInteractionRealmKind.Mortal,
            IsResolved: true,
            locationId,
            locationName,
            CurrentGuardianId: string.Empty,
            LocalActorIds: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            LocalFactionIds: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            UnavailableReason: null,
            AuthoritySnapshots: resolvedSnapshots);
    }

    private async Task<LocalInteractionScope> ResolveChaosSeaAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        IReadOnlyList<LocalInteractionAuthoritySnapshot> authoritySnapshots)
    {
        var guardiansRead = await ReadAuthorityObjectAsync(writeLease, GuardiansPath);
        var residentsRead = await ReadAuthorityObjectAsync(writeLease, GuardianAbodeResidentState.StatePath);
        var root = guardiansRead.Root;
        var resolvedSnapshots = AppendSnapshot(
            AppendSnapshot(authoritySnapshots, guardiansRead.Snapshot),
            residentsRead.Snapshot);
        var navigation = root?["chaosSeaNavigation"] as JsonObject;
        var currentAbodeId = GetFirstString(navigation, "currentAbodeId", "abodeId");
        var activeGuardian = root?["activeGuardian"] as JsonObject;
        var activeGuardianId = GetFirstString(activeGuardian, "guardianId", "actorId", "id");
        activeGuardianId = FirstNonEmpty(activeGuardianId, GetFirstString(root, "activeGuardianId"));
        var navigationGuardianId = GetFirstString(navigation, "currentGuardianId", "activeGuardianId");
        if (!string.IsNullOrWhiteSpace(activeGuardianId) &&
            !string.IsNullOrWhiteSpace(navigationGuardianId) &&
            !string.Equals(activeGuardianId, navigationGuardianId, StringComparison.OrdinalIgnoreCase))
        {
            return LocalInteractionScope.Unresolved(
                LocalInteractionRealmKind.ChaosSea,
                "Активный Хранитель и текущая обитель не согласованы. Локальные взаимодействия временно заблокированы.",
                resolvedSnapshots);
        }

        activeGuardianId = FirstNonEmpty(activeGuardianId, navigationGuardianId);
        if (string.IsNullOrWhiteSpace(currentAbodeId) || string.IsNullOrWhiteSpace(activeGuardianId))
        {
            return LocalInteractionScope.Unresolved(
                LocalInteractionRealmKind.ChaosSea,
                "Текущая обитель или активный Хранитель не определены.",
                resolvedSnapshots);
        }

        var guardian = EnumerateObjects(root?["guardians"] as JsonArray)
            .FirstOrDefault(candidate => EqualsNonEmpty(GetFirstString(candidate, "guardianId", "actorId", "id"), activeGuardianId));
        guardian ??= activeGuardian;
        var guardianAbode = guardian?["abode"] as JsonObject;
        var guardianAbodeId = GetFirstString(guardianAbode, "abodeId", "id");
        if (guardian == null || !EqualsNonEmpty(guardianAbodeId, currentAbodeId))
        {
            return LocalInteractionScope.Unresolved(
                LocalInteractionRealmKind.ChaosSea,
                "Активный Хранитель не принадлежит текущей обители. Локальные взаимодействия временно заблокированы.",
                resolvedSnapshots);
        }

        var localActors = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { activeGuardianId };
        var residentsRoot = residentsRead.Root;
        foreach (var resident in EnumerateObjects(residentsRoot?[GuardianAbodeResidentState.EntriesProperty] as JsonArray))
        {
            if (!GetBoolean(resident["isPresent"], defaultValue: true) ||
                !EqualsNonEmpty(GetFirstString(resident, "abodeId", "currentAbodeId"), currentAbodeId))
            {
                continue;
            }

            var residentGuardianId = GetFirstString(resident, "guardianId", "ownerGuardianId");
            if (!string.IsNullOrWhiteSpace(residentGuardianId) && !EqualsNonEmpty(residentGuardianId, activeGuardianId))
                continue;

            AddNonEmpty(localActors, GetActorId(resident));
        }

        return new LocalInteractionScope(
            LocalInteractionRealmKind.ChaosSea,
            IsResolved: true,
            currentAbodeId,
            GetFirstString(guardianAbode, "name", "abodeName", "displayName"),
            activeGuardianId,
            localActors,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            UnavailableReason: null,
            AuthoritySnapshots: resolvedSnapshots);
    }

    private async Task<LocalInteractionScope> ResolveShiningAbodeAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        IReadOnlyList<LocalInteractionAuthoritySnapshot> authoritySnapshots)
    {
        var shiningRead = await ReadAuthorityObjectAsync(writeLease, ShiningAbodePath);
        var residentsRead = await ReadAuthorityObjectAsync(writeLease, GuardianAbodeResidentState.StatePath);
        var root = shiningRead.Root;
        var resolvedSnapshots = AppendSnapshot(
            AppendSnapshot(authoritySnapshots, shiningRead.Snapshot),
            residentsRead.Snapshot);
        var currentHallId = GetFirstString(root, "currentHallId", "activeHallId", "selectedHallId");
        var halls = root?["halls"] as JsonArray;
        var currentHall = EnumerateObjects(halls)
            .FirstOrDefault(hall => EqualsExactNonEmpty(
                GetFirstString(hall, "hallId", "id"),
                currentHallId));
        if (string.IsNullOrWhiteSpace(currentHallId) || currentHall == null)
        {
            return LocalInteractionScope.Unresolved(
                LocalInteractionRealmKind.ShiningAbode,
                "Текущий зал Сияющей обители не определён. Обучение и торговля доступны только в текущем зале.",
                resolvedSnapshots);
        }

        var localFactions = new HashSet<string>(StringComparer.Ordinal);
        var localActors = new HashSet<string>(StringComparer.Ordinal);
        var currentHallName = GetFirstString(currentHall, "hallName", "name", "displayName");
        var factions = SarefMainStoryState.GetPlayerVisibleShiningFactions(root).ToList();
        foreach (var faction in factions)
        {
            if (!EqualsExactNonEmpty(
                    GetFirstString(faction, "hallId"),
                    currentHallId))
                continue;

            var factionId = GetFirstString(faction, "factionId", "id", "initialId");
            AddNonEmpty(localFactions, factionId);
            AddNonEmpty(localActors, GetFirstString(faction["leadership"] as JsonObject, "headActorId"));
        }

        foreach (var actor in EnumerateObjects(root?["shiningPoliticalActors"] as JsonArray))
        {
            var factionId = FirstNonEmpty(
                GetFirstString(actor, "currentFactionId"),
                GetFirstString(actor, "originFactionId", "shiningFactionId", "factionId"));
            if (Contains(localFactions, factionId))
                AddNonEmpty(localActors, GetActorId(actor));
        }

        var residentsRoot = residentsRead.Root;
        AddShiningResidents(localActors, localFactions, currentHallId, currentHallName, residentsRoot);
        AddShiningResidents(localActors, localFactions, currentHallId, currentHallName, root);

        return new LocalInteractionScope(
            LocalInteractionRealmKind.ShiningAbode,
            IsResolved: true,
            currentHallId,
            currentHallName,
            CurrentGuardianId: string.Empty,
            localActors,
            localFactions,
            UnavailableReason: null,
            AuthoritySnapshots: resolvedSnapshots);
    }

    private static void AddShiningResidents(
        ISet<string> localActors,
        IReadOnlySet<string> localFactions,
        string currentHallId,
        string currentHallName,
        JsonObject? root)
    {
        var residents = root?[GuardianAbodeResidentState.EntriesProperty] as JsonArray ?? root?["residents"] as JsonArray;
        foreach (var resident in EnumerateObjects(residents))
        {
            if (!GetBoolean(resident["isPresent"], defaultValue: true))
                continue;

            var directHallIds = GetNonEmptyStrings(
                resident,
                StringComparer.Ordinal,
                "hallId",
                "currentHallId",
                "locationId",
                "currentLocationId");
            var directHallNames = GetNonEmptyStrings(
                resident,
                StringComparer.OrdinalIgnoreCase,
                "hallName",
                "currentHallName",
                "locationName",
                "currentLocation");
            var factionId = GetFirstString(resident, "shiningFactionId", "currentFactionId", "factionId");
            var hasDirectHallEvidence = directHallIds.Count > 0 || directHallNames.Count > 0;
            if (hasDirectHallEvidence &&
                !ShiningAliasesMatchLocation(
                    directHallIds,
                    directHallNames,
                    currentHallId,
                    currentHallName))
                continue;

            if (hasDirectHallEvidence || Contains(localFactions, factionId))
                AddNonEmpty(localActors, GetActorId(resident));
        }
    }

    private async Task<AuthorityRead> ReadAuthorityObjectAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        string relativePath)
    {
        var raw = writeLease == null
            ? await _fs.ReadFileAsync(relativePath)
            : await _fs.ReadFileAsync(writeLease, relativePath);
        if (string.IsNullOrWhiteSpace(raw))
            return new AuthorityRead(null, new LocalInteractionAuthoritySnapshot(relativePath, raw));

        try
        {
            return new AuthorityRead(
                JsonNode.Parse(raw) as JsonObject,
                new LocalInteractionAuthoritySnapshot(relativePath, raw));
        }
        catch
        {
            return new AuthorityRead(null, new LocalInteractionAuthoritySnapshot(relativePath, raw));
        }
    }

    private static IReadOnlyList<LocalInteractionAuthoritySnapshot> AppendSnapshot(
        IReadOnlyList<LocalInteractionAuthoritySnapshot> snapshots,
        LocalInteractionAuthoritySnapshot snapshot) =>
        snapshots.Concat(new[] { snapshot }).ToArray();

    private static IEnumerable<JsonObject> EnumerateObjects(JsonArray? array) =>
        array?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>();

    private static string GetActorId(JsonObject actor) =>
        GetFirstString(actor, "actorId", "residentId", "guardianId", "npcId", "NPCId", "id", "initialId");

    private static string GetFirstString(JsonObject? root, params string[] names)
    {
        if (root == null)
            return string.Empty;

        foreach (var name in names)
        {
            var value = GetString(root[name]);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    private static IReadOnlySet<string> GetNonEmptyStrings(
        JsonObject root,
        params string[] names) =>
        GetNonEmptyStrings(
            root,
            StringComparer.OrdinalIgnoreCase,
            names);

    private static IReadOnlySet<string> GetNonEmptyStrings(
        JsonObject root,
        StringComparer comparer,
        params string[] names)
    {
        var values = new HashSet<string>(comparer);
        foreach (var name in names)
            AddNonEmpty(values, GetString(root[name]));
        return values;
    }

    private static bool ShiningAliasesMatchLocation(
        IReadOnlySet<string> idAliases,
        IReadOnlySet<string> nameAliases,
        string? locationId,
        string? locationName)
    {
        var hasComparableEvidence = false;
        var locationIds = BuildAliasSet(
            StringComparer.Ordinal,
            locationId);
        if (idAliases.Count > 0 && locationIds.Count > 0)
        {
            hasComparableEvidence = true;
            if (!idAliases.All(locationIds.Contains))
                return false;
        }

        var locationNames = BuildAliasSet(
            StringComparer.OrdinalIgnoreCase,
            locationName);
        if (nameAliases.Count > 0 && locationNames.Count > 0)
        {
            hasComparableEvidence = true;
            if (!nameAliases.All(locationNames.Contains))
                return false;
        }

        return hasComparableEvidence;
    }

    private static bool AliasesMatchLocation(
        IReadOnlySet<string> idAliases,
        IReadOnlySet<string> nameAliases,
        string? locationId,
        string? locationName)
    {
        var hasComparableEvidence = false;
        var locationIds = BuildAliasSet(locationId);
        if (idAliases.Count > 0 && locationIds.Count > 0)
        {
            hasComparableEvidence = true;
            if (!idAliases.All(locationIds.Contains))
                return false;
        }

        var locationNames = BuildAliasSet(locationName);
        if (nameAliases.Count > 0 && locationNames.Count > 0)
        {
            hasComparableEvidence = true;
            if (!nameAliases.All(locationNames.Contains))
                return false;
        }

        return hasComparableEvidence;
    }

    private static string GetString(JsonNode? node)
    {
        if (node is not JsonValue value)
            return string.Empty;

        return value.TryGetValue<string>(out var result) ? result?.Trim() ?? string.Empty : string.Empty;
    }

    private static bool GetBoolean(JsonNode? node, bool defaultValue)
    {
        return node is JsonValue value && value.TryGetValue<bool>(out var result) ? result : defaultValue;
    }

    private static bool Contains(IReadOnlySet<string> values, string? value) =>
        !string.IsNullOrWhiteSpace(value) && values.Contains(value);

    private static bool EqualsNonEmpty(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool EqualsExactNonEmpty(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left.Trim(), right.Trim(), StringComparison.Ordinal);

    private static bool LooksLikeNonCanonicalAfterlifeRealm(string? realm)
    {
        if (string.IsNullOrWhiteSpace(realm))
            return false;

        return realm.Contains("afterlife", StringComparison.OrdinalIgnoreCase) ||
               realm.Contains("посмерт", StringComparison.OrdinalIgnoreCase) ||
               realm.Contains("chaos", StringComparison.OrdinalIgnoreCase) ||
               realm.Contains("хаос", StringComparison.OrdinalIgnoreCase) ||
               realm.Contains("shining", StringComparison.OrdinalIgnoreCase) ||
               realm.Contains("сияющ", StringComparison.OrdinalIgnoreCase) ||
               realm.Contains("обитель", StringComparison.OrdinalIgnoreCase);
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static void AddNonEmpty(ISet<string> values, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            values.Add(value.Trim());
    }

    private static HashSet<string> BuildAliasSet(params string?[] values)
        => BuildAliasSet(StringComparer.OrdinalIgnoreCase, values);

    private static HashSet<string> BuildAliasSet(
        StringComparer comparer,
        params string?[] values)
    {
        var result = new HashSet<string>(comparer);
        foreach (var value in values)
            AddNonEmpty(result, value);
        return result;
    }
}
