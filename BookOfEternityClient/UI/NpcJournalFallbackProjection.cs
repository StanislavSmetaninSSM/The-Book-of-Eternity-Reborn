using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

internal static class NpcJournalFallbackProjection
{
    public const string JournalPath = "game_state/npcs/npc_journals.json";

    public const string Notice =
        "Показаны известные заметки о НПС. Полная карточка, разговор и торговля доступны только после появления НПС в основном списке.";

    public static async Task<IReadOnlyList<NpcJournalFallbackEntry>> ReadAsync(FileSystemManager fs)
    {
        var json = await fs.ReadFileAsync(JournalPath);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            using var doc = JsonDocument.Parse(json);
            return Collect(doc.RootElement);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static async Task<IReadOnlyList<NpcJournalFallbackEntry>> ReadAsync(StateManager stateManager)
    {
        using var doc = await stateManager.LoadGameStateFileAsync(JournalPath);
        return doc == null ? [] : Collect(doc.RootElement);
    }

    public static bool HasVisibleNpcCore(JsonNode? node)
    {
        if (node is JsonArray array)
            return array.Any(static item => HasVisibleNpcIdentity(item as JsonObject));

        if (node is not JsonObject root)
            return false;

        foreach (var sectionName in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections
                     .Concat(GuardianPolicyContracts.NpcCoreLegacyAliasSections))
        {
            if (root[sectionName] is JsonArray section &&
                section.Any(static item => HasVisibleNpcIdentity(item as JsonObject)))
            {
                return true;
            }
        }

        return false;
    }

    public static IReadOnlyList<NpcJournalFallbackConsoleRow> BuildConsoleRows(
        IReadOnlyList<NpcJournalFallbackEntry> entries) =>
        entries
            .Select(static entry => new NpcJournalFallbackConsoleRow(
                entry.DisplayName,
                entry.LatestNote,
                entry.JournalSummary))
            .ToList();

    public static IReadOnlyList<UiBlock> BuildBlocks(IReadOnlyList<NpcJournalFallbackConsoleRow> rows)
    {
        if (rows.Count == 0)
            return [];

        return
        [
            new UiMessageBlock
            {
                Severity = UiNotificationSeverity.Info,
                Title = "Персонажи",
                Message = Notice
            },
            new UiTableBlock
            {
                Title = "Известные НПС по заметкам",
                Columns = ["НПС", "Последняя запись", "Журнал"],
                Rows = rows
                    .Select(static row => new UiTableRow
                    {
                        Cells = [row.Name, row.LatestNote, row.JournalSummary]
                    })
                    .ToList()
            }
        ];
    }

    private static IReadOnlyList<NpcJournalFallbackEntry> Collect(JsonElement root)
    {
        var journals = ResolveJournalArray(root);
        if (journals.ValueKind != JsonValueKind.Array)
            return [];

        var entries = new List<NpcJournalFallbackEntry>();
        foreach (var item in journals.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var npcId = FirstNonEmpty(
                GetString(item, "npcId"),
                GetString(item, "NPCId"),
                GetString(item, "id"));
            var npcName = FirstNonEmpty(
                GetString(item, "npcName"),
                GetString(item, "NPCName"),
                GetString(item, "name"));

            if (string.IsNullOrWhiteSpace(npcId) && string.IsNullOrWhiteSpace(npcName))
                continue;

            var latest = ReadLatestJournalEntry(item);
            var latestNote = FirstNonEmpty(
                latest.Description,
                latest.Event,
                GetString(item, "lastJournalNote"));

            if (string.IsNullOrWhiteSpace(latestNote))
                latestNote = "Последняя запись пока без описания.";

            entries.Add(new NpcJournalFallbackEntry(
                npcId.Trim(),
                npcName.Trim(),
                latest.Event.Trim(),
                latestNote.Trim(),
                latest.RelationshipChange.Trim(),
                latest.EntryCount));
        }

        return entries;
    }

    private static JsonElement ResolveJournalArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return root;

        if (root.ValueKind != JsonValueKind.Object)
            return default;

        if (root.TryGetProperty("npcJournals", out var npcJournals) &&
            npcJournals.ValueKind == JsonValueKind.Array)
        {
            return npcJournals;
        }

        if (root.TryGetProperty("NPCJournals", out var legacyNpcJournals) &&
            legacyNpcJournals.ValueKind == JsonValueKind.Array)
        {
            return legacyNpcJournals;
        }

        return default;
    }

    private static JournalEntrySummary ReadLatestJournalEntry(JsonElement item)
    {
        if (!item.TryGetProperty("journalEntries", out var journalEntries) ||
            journalEntries.ValueKind != JsonValueKind.Array)
        {
            return new JournalEntrySummary(
                Event: string.Empty,
                Description: string.Empty,
                RelationshipChange: string.Empty,
                EntryCount: string.IsNullOrWhiteSpace(GetString(item, "lastJournalNote")) ? 0 : 1);
        }

        var count = 0;
        var latest = new JournalEntrySummary(string.Empty, string.Empty, string.Empty, 0);
        foreach (var entry in journalEntries.EnumerateArray())
        {
            count++;
            latest = entry.ValueKind switch
            {
                JsonValueKind.Object => new JournalEntrySummary(
                    GetString(entry, "event"),
                    FirstNonEmpty(GetString(entry, "description"), GetString(entry, "text")),
                    GetString(entry, "relationshipChange"),
                    count),
                JsonValueKind.String => new JournalEntrySummary(
                    string.Empty,
                    entry.GetString() ?? string.Empty,
                    string.Empty,
                    count),
                _ => latest with { EntryCount = count }
            };
        }

        return latest with { EntryCount = count };
    }

    private static bool HasVisibleNpcIdentity(JsonObject? item)
    {
        if (item == null)
            return false;

        return !string.IsNullOrWhiteSpace(FirstNonEmpty(
            GetNodeString(item, "npcId"),
            GetNodeString(item, "NPCId"),
            GetNodeString(item, "id"),
            GetNodeString(item, "name"),
            GetNodeString(item, "npcName"),
            GetNodeString(item, "NPCName")));
    }

    private static string GetString(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var value))
            return string.Empty;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => string.Empty
        };
    }

    private static string GetNodeString(JsonObject item, string property)
    {
        var node = item[property];
        if (node is not JsonValue value)
            return string.Empty;

        if (value.TryGetValue<string>(out var text))
            return text ?? string.Empty;
        if (value.TryGetValue<int>(out var number))
            return number.ToString();
        if (value.TryGetValue<long>(out var longNumber))
            return longNumber.ToString();
        if (value.TryGetValue<bool>(out var flag))
            return flag ? "true" : "false";

        return string.Empty;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private sealed record JournalEntrySummary(
        string Event,
        string Description,
        string RelationshipChange,
        int EntryCount);
}

internal sealed record NpcJournalFallbackEntry(
    string NpcId,
    string NpcName,
    string LatestEvent,
    string LatestNote,
    string RelationshipChange,
    int EntryCount)
{
    public string DisplayName =>
        string.IsNullOrWhiteSpace(NpcName)
            ? string.IsNullOrWhiteSpace(NpcId) ? "Неизвестный НПС" : NpcId
            : NpcName;

    public string JournalSummary =>
        string.IsNullOrWhiteSpace(RelationshipChange)
            ? FormatEntryCount(EntryCount)
            : $"{FormatEntryCount(EntryCount)}; отношение {RelationshipChange}";

    private static string FormatEntryCount(int count)
    {
        if (count <= 0)
            return "записей пока нет";

        var mod100 = count % 100;
        var mod10 = count % 10;
        var suffix = mod100 is >= 11 and <= 14
            ? "записей"
            : mod10 switch
            {
                1 => "запись",
                >= 2 and <= 4 => "записи",
                _ => "записей"
            };

        return $"{count} {suffix}";
    }
}

internal sealed record NpcJournalFallbackConsoleRow(
    string Name,
    string LatestNote,
    string JournalSummary);
