using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

internal static class NpcDetailSectionProjection
{
    public const string SectionSummaryTitle = "Разделы НПС";

    public static IReadOnlyList<NpcDetailProjection> BuildAll(JsonNode? npcCoreRoot, NpcDetailSectionDocuments documents)
    {
        var result = new List<NpcDetailProjection>();
        foreach (var npc in EnumerateNpcCoreObjects(npcCoreRoot))
        {
            var projection = Build(npc, documents);
            if (projection.Sections.Count > 0)
                result.Add(projection);
        }

        return result;
    }

    public static NpcDetailProjection Build(JsonNode? npcNode, NpcDetailSectionDocuments documents)
    {
        if (npcNode is not JsonObject npc)
            return new NpcDetailProjection(string.Empty, "Неизвестный НПС", []);

        var npcId = GetNpcId(npc);
        var npcName = GetNpcName(npc);
        var sections = new List<NpcDetailSection>();

        AddJournalSection(sections, npcName, npcId, documents);
        AddQuestSection(sections, npcName, npcId, npc, documents);
        AddActivitySection(sections, npcName, npcId, npc, documents);
        AddRelationshipSection(sections, npcName, npcId, npc, documents);
        AddMechanicsSection(sections, npcName, npcId, npc, documents);
        AddMemorySection(sections, npcName, npcId, npc, documents);

        return new NpcDetailProjection(npcId, npcName, sections);
    }

    public static IReadOnlyList<UiTableRow> BuildNpcOverviewRows(JsonNode? npcCoreRoot)
    {
        var npcs = EnumerateNpcCoreObjects(npcCoreRoot).ToList();
        if (npcs.Count == 0)
            return [];

        var preview = string.Join(", ", npcs
            .Take(3)
            .Select(GetNpcName)
            .Where(static value => !string.IsNullOrWhiteSpace(value)));
        var state = string.IsNullOrWhiteSpace(preview)
            ? npcs.Count.ToString()
            : $"{npcs.Count}: {preview}";

        return [new UiTableRow { Cells = ["NPC", state] }];
    }

    public static UiTableBlock BuildSectionSummaryTable(IReadOnlyList<NpcDetailProjection> projections) =>
        new()
        {
            Title = SectionSummaryTitle,
            Columns = ["НПС", "Раздел", "Состояние"],
            Rows = projections
                .SelectMany(static projection => projection.Sections.Select(section => new UiTableRow
                {
                    Cells = [projection.NpcName, section.Label, section.Hint]
                }))
                .ToList()
        };

    private static void AddJournalSection(
        List<NpcDetailSection> sections,
        string npcName,
        string npcId,
        NpcDetailSectionDocuments documents)
    {
        var rows = new List<UiTableRow>();
        var count = 0;

        foreach (var entry in CollectMatchingObjects(documents.Journals, npcId, npcName))
        {
            if (entry["journalEntries"] is JsonArray journalEntries && journalEntries.Count > 0)
            {
                foreach (var journalEntry in journalEntries.OfType<JsonObject>())
                {
                    count++;
                    var title = FirstNonEmpty(
                        GetNodeString(journalEntry, "event"),
                        GetNodeString(journalEntry, "topic"),
                        GetNodeString(journalEntry, "timestamp"),
                        $"Запись {count}");
                    var details = JoinDetails(
                        GetNodeString(journalEntry, "description"),
                        Prefix("Эмоциональный след", GetNodeString(journalEntry, "emotionalImpact")),
                        Prefix("Изменение отношения", GetNodeString(journalEntry, "relationshipChange")));
                    rows.Add(new UiTableRow { Cells = [title, EmptyFallback(details)] });
                }
            }
            else
            {
                var note = FirstNonEmpty(
                    GetNodeString(entry, "lastJournalNote"),
                    GetNodeString(entry, "thought"),
                    GetNodeString(entry, "description"));
                if (!string.IsNullOrWhiteSpace(note))
                {
                    count++;
                    rows.Add(new UiTableRow { Cells = [$"Запись {count}", note] });
                }
            }
        }

        foreach (var entry in CollectActorJournalObjects(documents.InteractionJournal, "npcId", npcId))
        {
            count++;
            rows.Add(new UiTableRow
            {
                Cells =
                [
                    FirstNonEmpty(GetNodeString(entry, "title"), GetNodeString(entry, "eventType"), $"Взаимодействие {count}"),
                    EmptyFallback(JoinDetails(GetNodeString(entry, "summary"), GetNodeString(entry, "responseSummary")))
                ]
            });
        }

        if (rows.Count == 0)
            return;

        sections.Add(new NpcDetailSection(
            "journal",
            "Дневник / мысли",
            FormatCount(count, "запись", "записи", "записей"),
            [
                new UiTableBlock
                {
                    Title = $"{npcName} — Дневник / мысли",
                    Columns = ["Запись", "Подробности"],
                    Rows = rows
                }
            ]));
    }

    private static void AddQuestSection(
        List<NpcDetailSection> sections,
        string npcName,
        string npcId,
        JsonObject npc,
        NpcDetailSectionDocuments documents)
    {
        var quests = new List<JsonObject>();
        AddObjectArray(quests, npc["personalQuests"]);

        foreach (var entry in CollectMatchingObjects(documents.Goals, npcId, npcName))
        {
            AddObjectArray(quests, entry["personalQuests"]);
            if (HasAnyString(entry, "questName", "title", "name") || entry["objectives"] is JsonArray)
                quests.Add(entry);
        }

        if (quests.Count == 0)
            return;

        var rows = new List<UiTableRow>();
        foreach (var quest in quests)
        {
            var questName = FirstNonEmpty(
                GetNodeString(quest, "questName"),
                GetNodeString(quest, "title"),
                GetNodeString(quest, "name"),
                "Безымянный квест");
            var status = EmptyFallback(GetNodeString(quest, "status"));
            var objectives = ReadObjectiveDescriptions(quest["objectives"]);
            var details = JoinDetails(
                GetNodeString(quest, "description"),
                Prefix("Предпосылка", GetNodeString(quest, "questBackground")),
                objectives.Count > 0 ? "Цели: " + string.Join("; ", objectives) : string.Empty,
                Prefix("Награда", DescribeNodeValue(quest["rewards"])),
                Prefix("Провал", GetNodeString(quest, "failureConsequences")));

            rows.Add(new UiTableRow
            {
                Cells = [questName, status, EmptyFallback(details)]
            });
        }

        sections.Add(new NpcDetailSection(
            "personal-quests",
            "Личные квесты",
            FormatCount(rows.Count, "квест", "квеста", "квестов"),
            [
                new UiTableBlock
                {
                    Title = $"{npcName} — Личные квесты",
                    Columns = ["Квест", "Статус", "Подробности"],
                    Rows = rows
                }
            ]));
    }

    private static void AddActivitySection(
        List<NpcDetailSection> sections,
        string npcName,
        string npcId,
        JsonObject npc,
        NpcDetailSectionDocuments documents)
    {
        var rows = new List<UiTableRow>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddActivityRow(rows, seen, "Текущая", npc["currentActivity"] as JsonObject);
        AddActivityArray(rows, seen, "Завершённая", npc["completedActivities"] as JsonArray);

        foreach (var entry in CollectMatchingObjects(documents.Activities, npcId, npcName))
        {
            var activity = entry["activityUpdate"] as JsonObject ?? entry;
            AddActivityRow(rows, seen, "Активность", activity);
        }

        if (rows.Count == 0)
            return;

        sections.Add(new NpcDetailSection(
            "activities",
            "Активности",
            FormatCount(rows.Count, "активность", "активности", "активностей"),
            [
                new UiTableBlock
                {
                    Title = $"{npcName} — Активности",
                    Columns = ["Активность", "Состояние", "Подробности"],
                    Rows = rows
                }
            ]));
    }

    private static void AddRelationshipSection(
        List<NpcDetailSection> sections,
        string npcName,
        string npcId,
        JsonObject npc,
        NpcDetailSectionDocuments documents)
    {
        var rows = new List<UiTableRow>();
        AddObjectArray(rows, npc["npcRelationships"], "Связь");

        if (npc["relationshipLock"] is JsonObject relationshipLock)
            rows.Add(new UiTableRow { Cells = ["Замок отношения", DescribeNodeValue(relationshipLock)] });
        if (npc["relationshipData"] is JsonObject relationshipData)
            rows.Add(new UiTableRow { Cells = ["Отношение", DescribeNodeValue(relationshipData)] });

        foreach (var entry in CollectMatchingObjects(documents.Relationships, npcId, npcName))
            rows.Add(new UiTableRow { Cells = ["Отношение", DescribeNodeValue(entry)] });

        if (rows.Count == 0)
            return;

        sections.Add(new NpcDetailSection(
            "relationships",
            "Отношения / замки",
            FormatCount(rows.Count, "запись", "записи", "записей"),
            [
                new UiTableBlock
                {
                    Title = $"{npcName} — Отношения / замки",
                    Columns = ["Раздел", "Подробности"],
                    Rows = rows
                }
            ]));
    }

    private static void AddMechanicsSection(
        List<NpcDetailSection> sections,
        string npcName,
        string npcId,
        JsonObject npc,
        NpcDetailSectionDocuments documents)
    {
        var rows = new List<UiTableRow>();
        AddNamedArrayRows(rows, "Активный навык", npc["activeSkills"]);
        AddNamedArrayRows(rows, "Пассивный навык", npc["passiveSkills"]);
        AddNamedArrayRows(rows, "Предмет", npc["inventory"]);

        if (npc["equippedItems"] is JsonObject equipment)
        {
            foreach (var item in equipment)
            {
                if (item.Value == null || item.Value.GetValueKind() is JsonValueKind.Null or JsonValueKind.Undefined)
                    continue;

                rows.Add(new UiTableRow { Cells = ["Экипировка", $"{item.Key}: {DescribeNodeValue(item.Value)}"] });
            }
        }

        foreach (var entry in CollectMatchingObjects(documents.Skills, npcId, npcName))
            rows.Add(new UiTableRow { Cells = ["Навык", DescribeNodeValue(entry)] });
        foreach (var entry in CollectMatchingObjects(documents.Effects, npcId, npcName))
            rows.Add(new UiTableRow { Cells = ["Эффект", DescribeNodeValue(entry)] });
        foreach (var entry in CollectMatchingObjects(documents.Inventory, npcId, npcName))
            rows.Add(new UiTableRow { Cells = ["Инвентарь", DescribeNodeValue(entry)] });

        if (rows.Count == 0)
            return;

        sections.Add(new NpcDetailSection(
            "mechanics",
            "Навыки / эффекты / снаряжение",
            FormatCount(rows.Count, "запись", "записи", "записей"),
            [
                new UiTableBlock
                {
                    Title = $"{npcName} — Навыки / эффекты / снаряжение",
                    Columns = ["Раздел", "Подробности"],
                    Rows = rows
                }
            ]));
    }

    private static void AddMemorySection(
        List<NpcDetailSection> sections,
        string npcName,
        string npcId,
        JsonObject npc,
        NpcDetailSectionDocuments documents)
    {
        var rows = new List<UiTableRow>();
        AddNamedArrayRows(rows, "Карта судьбы", npc["fateCards"]);
        AddNamedArrayRows(rows, "Маска", npc["masks"]);
        AddNamedArrayRows(rows, "Особое состояние", npc["customStates"]);

        foreach (var entry in CollectMatchingObjects(documents.Memory, npcId, npcName))
            rows.Add(new UiTableRow { Cells = ["Воспоминание", DescribeNodeValue(entry)] });
        foreach (var entry in CollectMatchingObjects(documents.Masks, npcId, npcName))
            rows.Add(new UiTableRow { Cells = ["Маска", DescribeNodeValue(entry)] });
        foreach (var entry in CollectMatchingObjects(documents.FateCards, npcId, npcName))
            rows.Add(new UiTableRow { Cells = ["Карта судьбы", DescribeNodeValue(entry)] });
        foreach (var entry in CollectMatchingObjects(documents.CustomStates, npcId, npcName))
            rows.Add(new UiTableRow { Cells = ["Особое состояние", DescribeNodeValue(entry)] });

        if (rows.Count == 0)
            return;

        sections.Add(new NpcDetailSection(
            "memory",
            "Память / состояния",
            FormatCount(rows.Count, "запись", "записи", "записей"),
            [
                new UiTableBlock
                {
                    Title = $"{npcName} — Память / состояния",
                    Columns = ["Раздел", "Подробности"],
                    Rows = rows
                }
            ]));
    }

    private static IEnumerable<JsonObject> EnumerateNpcCoreObjects(JsonNode? root)
    {
        if (root is JsonArray array)
        {
            foreach (var npc in array.OfType<JsonObject>())
                yield return npc;
            yield break;
        }

        if (root is not JsonObject obj)
            yield break;

        foreach (var sectionName in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections
                     .Concat(GuardianPolicyContracts.NpcCoreLegacyAliasSections))
        {
            if (obj[sectionName] is not JsonArray section)
                continue;

            foreach (var npc in section.OfType<JsonObject>())
            {
                if (HasVisibleNpcIdentity(npc))
                    yield return npc;
            }
        }
    }

    private static List<JsonObject> CollectMatchingObjects(JsonNode? root, string npcId, string npcName)
    {
        var result = new List<JsonObject>();
        Visit(root);
        return result;

        void Visit(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject obj:
                    if (MatchesNpc(obj, npcId, npcName))
                        result.Add(obj);

                    foreach (var child in obj)
                        Visit(child.Value);
                    break;
                case JsonArray array:
                    foreach (var child in array)
                        Visit(child);
                    break;
            }
        }
    }

    private static List<JsonObject> CollectActorJournalObjects(JsonNode? root, string actorIdProperty, string npcId)
    {
        if (string.IsNullOrWhiteSpace(npcId) || root is not JsonObject obj)
            return [];

        var entries = obj[ActorJournalState.EntriesProperty] as JsonArray;
        if (entries == null)
            return [];

        return entries
            .OfType<JsonObject>()
            .Where(entry => string.Equals(GetNodeString(entry, actorIdProperty), npcId, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static bool MatchesNpc(JsonObject obj, string npcId, string npcName)
    {
        var entryId = GetNpcId(obj);
        if (!string.IsNullOrWhiteSpace(npcId) &&
            !string.IsNullOrWhiteSpace(entryId) &&
            string.Equals(entryId, npcId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var entryName = FirstNonEmpty(
            GetNodeString(obj, "NPCName"),
            GetNodeString(obj, "npcName"),
            GetNodeString(obj, "name"));

        return !string.IsNullOrWhiteSpace(npcName) &&
               !string.IsNullOrWhiteSpace(entryName) &&
               string.Equals(entryName, npcName, StringComparison.OrdinalIgnoreCase);
    }

    private static void AddActivityArray(List<UiTableRow> rows, HashSet<string> seen, string state, JsonArray? array)
    {
        if (array == null)
            return;

        foreach (var item in array)
        {
            if (item is JsonObject obj)
            {
                AddActivityRow(rows, seen, state, obj);
            }
            else
            {
                var value = DescribeNodeValue(item);
                if (!string.IsNullOrWhiteSpace(value) && seen.Add($"{state}:{value}"))
                    rows.Add(new UiTableRow { Cells = [value, state, "не указано"] });
            }
        }
    }

    private static void AddActivityRow(List<UiTableRow> rows, HashSet<string> seen, string state, JsonObject? activity)
    {
        if (activity == null)
            return;

        var name = FirstNonEmpty(
            GetNodeString(activity, "activityName"),
            GetNodeString(activity, "name"),
            GetNodeString(activity, "activity"),
            "Безымянная активность");
        var description = FirstNonEmpty(
            GetNodeString(activity, "description"),
            GetNodeString(activity, "summary"),
            GetNodeString(activity, "narrativeSummary"));
        var activeState = FirstNonEmpty(
            GetNodeString(activity, "activeState"),
            GetNodeString(activity, "status"),
            state);
        var progress = DescribeProgress(activity);
        var key = $"{name}:{description}";
        if (!seen.Add(key))
            return;

        rows.Add(new UiTableRow
        {
            Cells = [name, EmptyFallback(activeState), EmptyFallback(JoinDetails(description, progress))]
        });
    }

    private static void AddObjectArray(List<JsonObject> target, JsonNode? node)
    {
        if (node is not JsonArray array)
            return;

        target.AddRange(array.OfType<JsonObject>());
    }

    private static void AddObjectArray(List<UiTableRow> target, JsonNode? node, string label)
    {
        if (node is not JsonArray array)
            return;

        foreach (var item in array)
        {
            var details = DescribeNodeValue(item);
            if (!string.IsNullOrWhiteSpace(details))
                target.Add(new UiTableRow { Cells = [label, details] });
        }
    }

    private static void AddNamedArrayRows(List<UiTableRow> rows, string label, JsonNode? node)
    {
        if (node is not JsonArray array)
            return;

        foreach (var item in array)
        {
            var details = DescribeNodeValue(item);
            if (!string.IsNullOrWhiteSpace(details))
                rows.Add(new UiTableRow { Cells = [label, details] });
        }
    }

    private static List<string> ReadObjectiveDescriptions(JsonNode? node)
    {
        if (node is not JsonArray array)
            return [];

        return array
            .Select(static item => item switch
            {
                JsonObject obj => FirstNonEmpty(GetNodeString(obj, "description"), GetNodeString(obj, "objective"), GetNodeString(obj, "title")),
                _ => DescribeNodeValue(item)
            })
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    private static string DescribeProgress(JsonObject activity)
    {
        var total = GetNodeInt(activity, "totalTimeCostMinutes");
        var spent = GetNodeInt(activity, "timeSpentMinutes");
        if (total > 0 && spent >= 0)
            return $"Прогресс: {Math.Min(100, spent * 100 / total)}%";

        var currentStep = GetNodeInt(activity, "currentStepNumber");
        var totalSteps = GetNodeInt(activity, "totalStepsInActivity");
        if (totalSteps > 0 && currentStep >= 0)
            return $"Шаг: {currentStep}/{totalSteps}";

        return string.Empty;
    }

    private static string DescribeNodeValue(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return string.Empty;
            case JsonValue value:
                return TryGetScalarString(value, out var text) ? text : string.Empty;
            case JsonArray array:
                return string.Join(", ", array.Select(DescribeNodeValue).Where(static value => !string.IsNullOrWhiteSpace(value)));
            case JsonObject obj:
                var preferred = FirstNonEmpty(
                    GetNodeString(obj, "displayName"),
                    GetNodeString(obj, "questName"),
                    GetNodeString(obj, "activityName"),
                    GetNodeString(obj, "skillName"),
                    GetNodeString(obj, "effectType"),
                    GetNodeString(obj, "itemName"),
                    GetNodeString(obj, "name"),
                    GetNodeString(obj, "title"),
                    GetNodeString(obj, "description"),
                    GetNodeString(obj, "summary"));
                var extras = obj
                    .Where(static prop => !prop.Key.StartsWith("_", StringComparison.Ordinal))
                    .Where(static prop => !IdentityFieldNames.Contains(prop.Key))
                    .Select(static prop => DescribeNodeValue(prop.Value))
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(4)
                    .ToList();
                if (!string.IsNullOrWhiteSpace(preferred))
                    extras.Insert(0, preferred);

                return string.Join("; ", extras.Distinct(StringComparer.OrdinalIgnoreCase));
            default:
                return string.Empty;
        }
    }

    private static string GetNpcId(JsonObject npc) =>
        FirstNonEmpty(
            GetNodeString(npc, "NPCId"),
            GetNodeString(npc, "npcId"),
            GetNodeString(npc, "id"));

    private static string GetNpcName(JsonObject npc) =>
        FirstNonEmpty(
            GetNodeString(npc, "NPCName"),
            GetNodeString(npc, "npcName"),
            GetNodeString(npc, "name"),
            "Неизвестный НПС");

    private static bool HasVisibleNpcIdentity(JsonObject item) =>
        !string.IsNullOrWhiteSpace(FirstNonEmpty(
            GetNpcId(item),
            GetNpcName(item)));

    private static bool HasAnyString(JsonObject obj, params string[] properties) =>
        properties.Any(property => !string.IsNullOrWhiteSpace(GetNodeString(obj, property)));

    private static string? GetNodeString(JsonObject obj, string property)
    {
        var node = obj[property];
        return node == null ? null : TryGetScalarString(node, out var value) ? value : null;
    }

    private static int GetNodeInt(JsonObject obj, string property)
    {
        var node = obj[property];
        if (node is not JsonValue value)
            return -1;

        if (value.TryGetValue<int>(out var number))
            return number;
        if (value.TryGetValue<long>(out var longNumber) && longNumber is >= int.MinValue and <= int.MaxValue)
            return (int)longNumber;
        if (value.TryGetValue<string>(out var text) && int.TryParse(text, out var parsed))
            return parsed;

        return -1;
    }

    private static bool TryGetScalarString(JsonNode node, out string value)
    {
        value = string.Empty;
        if (node is not JsonValue jsonValue)
            return false;

        if (jsonValue.TryGetValue<string>(out var text))
        {
            value = text ?? string.Empty;
            return true;
        }

        if (jsonValue.TryGetValue<int>(out var intValue))
        {
            value = intValue.ToString();
            return true;
        }

        if (jsonValue.TryGetValue<long>(out var longValue))
        {
            value = longValue.ToString();
            return true;
        }

        if (jsonValue.TryGetValue<bool>(out var boolValue))
        {
            value = boolValue ? "да" : "нет";
            return true;
        }

        return false;
    }

    private static string JoinDetails(params string?[] values) =>
        string.Join("; ", values.Where(static value => !string.IsNullOrWhiteSpace(value)).Select(static value => value!.Trim()));

    private static string Prefix(string label, string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : $"{label}: {value.Trim()}";

    private static string EmptyFallback(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "не указано" : value.Trim();

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string FormatCount(int count, string one, string few, string many)
    {
        var mod100 = count % 100;
        var mod10 = count % 10;
        var suffix = mod100 is >= 11 and <= 14
            ? many
            : mod10 switch
            {
                1 => one,
                >= 2 and <= 4 => few,
                _ => many
            };

        return $"{count} {suffix}";
    }

    private static readonly HashSet<string> IdentityFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "NPCId",
        "npcId",
        "id",
        "NPCName",
        "npcName",
        "name"
    };
}

internal sealed record NpcDetailProjection(
    string NpcId,
    string NpcName,
    IReadOnlyList<NpcDetailSection> Sections);

internal sealed record NpcDetailSection(
    string Id,
    string Label,
    string Hint,
    IReadOnlyList<UiBlock> Blocks)
{
    public string ChoiceLabel => $"{Label} — {Hint}";
}

internal sealed record NpcDetailSectionDocuments(
    JsonNode? Relationships = null,
    JsonNode? Goals = null,
    JsonNode? Activities = null,
    JsonNode? Inventory = null,
    JsonNode? Effects = null,
    JsonNode? Skills = null,
    JsonNode? Personality = null,
    JsonNode? Journals = null,
    JsonNode? InteractionJournal = null,
    JsonNode? Masks = null,
    JsonNode? Memory = null,
    JsonNode? FateCards = null,
    JsonNode? CustomStates = null);
