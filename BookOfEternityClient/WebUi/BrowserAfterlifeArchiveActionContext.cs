using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.WebUi;

internal sealed record BrowserAfterlifeArchiveEntryChoice(
    string ArchiveId,
    string Title,
    string EntryType,
    string Rarity,
    string Summary);

internal sealed record BrowserAfterlifeArchiveGuardianChoice(
    string GuardianId,
    string GuardianName,
    string Domain,
    int Reputation,
    bool FuelAvailable,
    string TargetProjectId,
    string TargetProjectName);

internal sealed record BrowserAfterlifeArchiveActionContext(
    string CurrentRealm,
    IReadOnlyList<BrowserAfterlifeArchiveEntryChoice> Entries,
    IReadOnlyList<BrowserAfterlifeArchiveGuardianChoice> Guardians,
    string BlockerTitle = "",
    string BlockerMessage = "")
{
    public bool IsBlocked => !string.IsNullOrWhiteSpace(BlockerMessage);

    public BrowserAfterlifeArchiveEntryChoice? ResolveEntry(string archiveId) =>
        Entries.FirstOrDefault(entry => string.Equals(entry.ArchiveId, archiveId, StringComparison.OrdinalIgnoreCase));

    public BrowserAfterlifeArchiveGuardianChoice? ResolveGuardian(string guardianId) =>
        Guardians.FirstOrDefault(guardian => string.Equals(guardian.GuardianId, guardianId, StringComparison.OrdinalIgnoreCase));
}

internal static class BrowserAfterlifeArchiveActionContextReader
{
    private const string SoulStatePath = "game_state/meta/soul_state.json";
    private const string GuardiansPath = "game_state/meta/guardians.json";

    public static async Task<BrowserAfterlifeArchiveActionContext> ReadConsultationAsync(
        FileSystemManager fs,
        StateManager stateManager)
    {
        var baseContext = await ReadBaseContextAsync(fs, stateManager, requireProjectFuel: false);
        if (baseContext.IsBlocked)
            return baseContext;

        var pending = await AfterlifeArchiveActionState.ReadConsultationStateAsync(fs);
        if (pending.IsMalformed)
            return Blocked(baseContext.CurrentRealm, "Архивная консультация недоступна", "Ожидающий запрос консультации повреждён. Завершите проверку состояния, затем повторите действие.");
        if (pending.Exists)
            return Blocked(baseContext.CurrentRealm, "Архивная консультация уже ожидает", "Уже есть незакрытая архивная консультация. Дождитесь ответа ГМ перед новым запросом.");

        if (baseContext.Guardians.Count == 0)
            return Blocked(baseContext.CurrentRealm, "Нет подходящего Хранителя", "Сейчас нет дружественных Хранителей для архивной консультации.");

        return baseContext;
    }

    public static async Task<BrowserAfterlifeArchiveActionContext> ReadProjectFuelAsync(
        FileSystemManager fs,
        StateManager stateManager)
    {
        var baseContext = await ReadBaseContextAsync(fs, stateManager, requireProjectFuel: true);
        if (baseContext.IsBlocked)
            return baseContext;

        var pending = await AfterlifeArchiveActionState.ReadProjectFuelStateAsync(fs);
        if (pending.IsMalformed)
            return Blocked(baseContext.CurrentRealm, "Подпитка проекта недоступна", "Ожидающий запрос подпитки проекта повреждён. Завершите проверку состояния, затем повторите действие.");
        if (pending.Exists)
            return Blocked(baseContext.CurrentRealm, "Подпитка проекта уже ожидает", "Уже есть незакрытая архивная подпитка проекта. Дождитесь ответа ГМ перед новым запросом.");

        if (baseContext.Guardians.Count == 0)
            return Blocked(baseContext.CurrentRealm, "Нет активного проекта", "Сейчас нет дружественного Хранителя с активным проектом для архивной подпитки.");

        return baseContext;
    }

    private static async Task<BrowserAfterlifeArchiveActionContext> ReadBaseContextAsync(
        FileSystemManager fs,
        StateManager stateManager,
        bool requireProjectFuel)
    {
        await stateManager.RefreshGameStateAsync();
        var soulRoot = await TryReadObjectAsync(fs, SoulStatePath);
        if (soulRoot == null)
            return Blocked(string.Empty, "Архив души недоступен", "Состояние души сейчас не читается. Повторите действие после проверки состояния.");

        var currentRealm = FirstNonEmpty(GetNodeString(soulRoot["currentRealm"]), stateManager.CurrentState.CurrentRealm);
        if (!RealmSemantics.IsAfterlifeRealm(currentRealm))
            return Blocked(currentRealm, "Архивное действие недоступно", "Архивные действия доступны только в посмертии.");

        AfterlifeArchiveState.NormalizeShape(soulRoot);
        var entries = ReadEligibleEntries(soulRoot).ToList();
        if (entries.Count == 0)
            return Blocked(currentRealm, "Нет доступной записи Архива", "Сейчас нет свободной записи Архива, подходящей для этого действия.");

        var guardiansRoot = await TryReadObjectAsync(fs, GuardiansPath);
        if (guardiansRoot == null)
            return Blocked(currentRealm, "Хранители недоступны", "Список Хранителей сейчас не читается. Повторите действие после проверки состояния.");

        var trackerRoot = await TryReadObjectAsync(fs, GuardianProjectState.TrackerPath);
        var guardians = ReadFriendlyGuardians(guardiansRoot, trackerRoot)
            .Where(guardian => !requireProjectFuel || guardian.FuelAvailable)
            .OrderByDescending(guardian => guardian.Reputation)
            .ThenBy(guardian => guardian.GuardianName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new BrowserAfterlifeArchiveActionContext(currentRealm, entries, guardians);
    }

    private static IEnumerable<BrowserAfterlifeArchiveEntryChoice> ReadEligibleEntries(JsonObject soulRoot)
    {
        var stored = AfterlifeArchiveState.EnsureStoredArray(soulRoot);
        foreach (var entry in stored.OfType<JsonObject>())
        {
            var archiveId = GetNodeString(entry["archiveId"]);
            var entryType = GetNodeString(entry["entryType"]);
            if (string.IsNullOrWhiteSpace(archiveId) ||
                !AfterlifeArchiveState.IsAllowedEntryType(entryType) ||
                AfterlifeArchiveState.IsReserved(entry))
            {
                continue;
            }

            yield return new BrowserAfterlifeArchiveEntryChoice(
                archiveId,
                FirstNonEmpty(GetNodeString(entry["title"]), archiveId),
                entryType!,
                FirstNonEmpty(GetNodeString(entry["rarity"]), "Common"),
                FirstNonEmpty(GetNodeString(entry["summary"]), "Сохранённая запись Архива."));
        }
    }

    private static IEnumerable<BrowserAfterlifeArchiveGuardianChoice> ReadFriendlyGuardians(
        JsonObject guardiansRoot,
        JsonObject? trackerRoot)
    {
        foreach (var guardian in EnumerateCanonicalGuardianObjects(guardiansRoot))
        {
            var guardianId = FirstNonEmpty(GetNodeString(guardian["guardianId"]), GetNodeString(guardian["id"]));
            if (string.IsNullOrWhiteSpace(guardianId))
                continue;

            var relationship = guardian["relationshipData"] as JsonObject;
            var reputation = GetNodeInt(relationship?["currentReputation"], GetNodeInt(guardian["reputation"]));
            if (reputation < 50)
                continue;

            var manifestation = guardian["manifestation"] as JsonObject;
            var name = FirstNonEmpty(
                GetNodeString(guardian["canonicalName"]),
                GetNodeString(guardian["guardianName"]),
                GetNodeString(guardian["name"]),
                GetNodeString(manifestation?["currentDisplayName"]),
                GetNodeString(guardian["displayName"]),
                guardianId);
            var (fuelAvailable, projectId, projectName) = ResolveArchiveFuelTarget(trackerRoot, guardianId);
            yield return new BrowserAfterlifeArchiveGuardianChoice(
                guardianId,
                name,
                FirstNonEmpty(GetNodeString(guardian["domain"]), GetNodeString(guardian["domainTag"]), "—"),
                reputation,
                fuelAvailable,
                projectId,
                projectName);
        }
    }

    private static (bool Available, string ProjectId, string ProjectName) ResolveArchiveFuelTarget(
        JsonObject? trackerRoot,
        string guardianId)
    {
        if (trackerRoot?["activeProjects"] is not JsonArray activeProjects)
            return (false, string.Empty, string.Empty);

        foreach (var entry in activeProjects.OfType<JsonObject>())
        {
            if (!string.Equals(GetNodeString(entry["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase) ||
                entry["project"] is not JsonObject project)
            {
                continue;
            }

            var projectId = GetNodeString(project["projectId"]);
            if (string.IsNullOrWhiteSpace(projectId))
                continue;

            return (true, projectId, FirstNonEmpty(GetNodeString(project["projectName"]), projectId));
        }

        return (false, string.Empty, string.Empty);
    }

    private static IEnumerable<JsonObject> EnumerateCanonicalGuardianObjects(JsonObject root)
    {
        if (root["guardians"] is JsonArray guardians)
        {
            foreach (var guardian in guardians.OfType<JsonObject>())
                yield return guardian;
        }

        if (root["activeGuardian"] is JsonObject activeGuardian)
            yield return activeGuardian;
    }

    private static async Task<JsonObject?> TryReadObjectAsync(FileSystemManager fs, string path)
    {
        var raw = await fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            return JsonNode.Parse(raw) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static BrowserAfterlifeArchiveActionContext Blocked(string currentRealm, string title, string message) =>
        new(currentRealm, [], [], title, message);

    private static string? GetNodeString(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text))
                return text;
            if (value.TryGetValue<int>(out var number))
                return number.ToString();
        }

        return null;
    }

    private static int GetNodeInt(JsonNode? node, int fallback = 0)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var number))
                return number;
            if (value.TryGetValue<string>(out var text) && int.TryParse(text, out var parsed))
                return parsed;
        }

        return fallback;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
