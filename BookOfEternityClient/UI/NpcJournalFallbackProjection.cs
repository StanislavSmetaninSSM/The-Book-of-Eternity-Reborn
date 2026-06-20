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

            var journalEntries = ReadJournalEntries(item);
            var latest = journalEntries.LastOrDefault();
            var latestNote = FirstNonEmpty(
                latest?.Description,
                latest?.Event,
                GetString(item, "lastJournalNote"));

            if (string.IsNullOrWhiteSpace(latestNote))
                latestNote = "Последняя запись пока без описания.";

            var latestEvent = latest?.Event ?? string.Empty;
            var relationshipChange = FirstNonEmpty(latest?.RelationshipChange, GetString(item, "relationshipChange"));
            var entryCount = journalEntries.Count;
            if (entryCount == 0 && !string.IsNullOrWhiteSpace(GetString(item, "lastJournalNote")))
                entryCount = 1;

            entries.Add(new NpcJournalFallbackEntry(
                npcId.Trim(),
                npcName.Trim(),
                latestEvent.Trim(),
                latestNote.Trim(),
                relationshipChange.Trim(),
                entryCount,
                journalEntries));
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

    public static IReadOnlyList<UiBlock> BuildDetailBlocks(NpcJournalFallbackEntry entry)
    {
        var blocks = new List<UiBlock>
        {
            new UiMessageBlock
            {
                Severity = UiNotificationSeverity.Info,
                Title = $"Журнал НПС: {entry.DisplayName}",
                Message = Notice
            },
            new UiKeyValueGridBlock
            {
                Items =
                [
                    new UiKeyValueItem { Key = "НПС", Value = entry.DisplayName },
                    new UiKeyValueItem { Key = "Последняя запись", Value = entry.LatestNote },
                    new UiKeyValueItem { Key = "Журнал", Value = entry.JournalSummary }
                ]
            }
        };

        var rows = entry.JournalEntries.Count > 0
            ? entry.JournalEntries
                .Select(static journalEntry => new UiTableRow
                {
                    Cells =
                    [
                        EmptyFallback(journalEntry.Event, "Событие не подписано"),
                        EmptyFallback(journalEntry.Description, "Описание пока не записано."),
                        EmptyFallback(journalEntry.RelationshipChange, "без изменения")
                    ]
                })
                .ToList()
            :
            [
                new UiTableRow
                {
                    Cells =
                    [
                        EmptyFallback(entry.LatestEvent, "Последняя запись"),
                        entry.LatestNote,
                        EmptyFallback(entry.RelationshipChange, "без изменения")
                    ]
                }
            ];

        blocks.Add(new UiTableBlock
        {
            Title = "Записи журнала",
            Columns = ["Событие", "Запись", "Отношение"],
            Rows = rows
        });
        blocks.Add(new UiTextBlock { Text = "Назад к списку можно командой /нпс.", Tone = UiTone.Muted });
        return blocks;
    }

    private static IReadOnlyList<NpcJournalEntry> ReadJournalEntries(JsonElement item)
    {
        if (!item.TryGetProperty("journalEntries", out var journalEntries) ||
            journalEntries.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var entries = new List<NpcJournalEntry>();
        foreach (var entry in journalEntries.EnumerateArray())
        {
            switch (entry.ValueKind)
            {
                case JsonValueKind.Object:
                    entries.Add(new NpcJournalEntry(
                        GetString(entry, "event").Trim(),
                        FirstNonEmpty(GetString(entry, "description"), GetString(entry, "text")).Trim(),
                        GetString(entry, "relationshipChange").Trim()));
                    break;
                case JsonValueKind.String:
                    entries.Add(new NpcJournalEntry(
                        string.Empty,
                        (entry.GetString() ?? string.Empty).Trim(),
                        string.Empty));
                    break;
            }
        }

        return entries;
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

    private static string EmptyFallback(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}

internal sealed record NpcJournalFallbackEntry(
    string NpcId,
    string NpcName,
    string LatestEvent,
    string LatestNote,
    string RelationshipChange,
    int EntryCount,
    IReadOnlyList<NpcJournalEntry> JournalEntries)
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

internal sealed record NpcJournalEntry(
    string Event,
    string Description,
    string RelationshipChange);

internal sealed record NpcJournalFallbackConsoleRow(
    string Name,
    string LatestNote,
    string JournalSummary);
