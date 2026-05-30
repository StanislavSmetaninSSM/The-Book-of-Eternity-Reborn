using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

public static class ExplorerChaosSeaCommandResultBuilder
{
    private const string SoulStatePath = "game_state/meta/soul_state.json";
    private const string GuardiansPath = "game_state/meta/guardians.json";
    private const string AbodePowerJournalPath = "game_state/meta/abode_power_journal.json";

    private enum CommandKind
    {
        Overview,
        Guardians,
        AbodePower,
        GuardianProjects,
        GuardianPolitics,
        Abodes,
        Gacha
    }

    private static readonly IReadOnlyDictionary<string, CommandKind> CommandKinds =
        new Dictionary<string, CommandKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["/chaos_sea"] = CommandKind.Overview,
            ["/море_хаоса"] = CommandKind.Overview,
            ["/guardians"] = CommandKind.Guardians,
            ["/хранители"] = CommandKind.Guardians,
            ["/abode_power"] = CommandKind.AbodePower,
            ["/сила_обители"] = CommandKind.AbodePower,
            ["/guardian_projects"] = CommandKind.GuardianProjects,
            ["/проекты_хранителей"] = CommandKind.GuardianProjects,
            ["/guardian_politics"] = CommandKind.GuardianPolitics,
            ["/политика_хранителей"] = CommandKind.GuardianPolitics,
            ["/abodes"] = CommandKind.Abodes,
            ["/обители"] = CommandKind.Abodes,
            ["/gacha"] = CommandKind.Gacha,
            ["/гача"] = CommandKind.Gacha
        };

    public static bool CanBuild(string command) => CommandKinds.ContainsKey(command.Trim());

    public static async Task<ExplorerCommandResult?> TryBuildAsync(
        string command,
        StateManager stateManager,
        FileSystemManager fs,
        bool includeAdvancedDiagnostics = false)
    {
        var normalizedCommand = command.Trim();
        if (!CommandKinds.TryGetValue(normalizedCommand, out var kind))
            return null;

        await stateManager.RefreshGameStateAsync();

        return kind switch
        {
            CommandKind.Overview => await BuildOverview(normalizedCommand, fs, stateManager),
            CommandKind.Guardians => await BuildGuardians(normalizedCommand, fs),
            CommandKind.AbodePower => await BuildAbodePower(normalizedCommand, fs),
            CommandKind.GuardianProjects => await BuildBundle(normalizedCommand, fs, "Проекты Хранителей", [
                new(GuardianProjectState.TrackerPath, "projects", "Проектов"),
                new(GuardianProjectState.TrackerPath, "journal", "Записей журнала"),
                new(GuardiansPath, "guardians", "Хранителей")
            ]),
            CommandKind.GuardianPolitics => await BuildGuardianPolitics(
                normalizedCommand,
                fs,
                includeAdvancedDiagnostics || stateManager.Settings.ShowGmThoughts),
            CommandKind.Abodes => await BuildAbodes(normalizedCommand, fs),
            CommandKind.Gacha => await BuildGacha(normalizedCommand, fs),
            _ => null
        };
    }

    private static async Task<ExplorerCommandResult> BuildOverview(string command, FileSystemManager fs, StateManager stateManager)
    {
        var soul = await ReadJson(fs, SoulStatePath);
        var guardians = await ReadJson(fs, GuardiansPath);
        var offering = await ReadJson(fs, GuardianAbodeOfferingState.PendingRequestPath);
        var foundation = await ReadJson(fs, PlayerGuardianFoundationState.PendingRequestPath);

        var blocks = new List<UiBlock>
        {
            Panel("Море Хаоса",
                Grid(
                    ("Фаза", EmptyFallback(stateManager.CurrentState.CurrentRealm)),
                    ("Душа", EmptyFallback(stateManager.CurrentState.SoulName)),
                    ("Чернильные Перья", stateManager.CurrentState.InkFeathers.ToString()),
                    ("Просветление", EmptyFallback(stateManager.CurrentState.EnlightenmentTier)),
                    ("Активный Хранитель", DescribeActiveGuardian(guardians.Node)),
                    ("Текущая Обитель", DescribeCurrentAbode(guardians.Node)),
                    ("Pending подношение", DescribePresence(offering)),
                    ("Pending основание мантии", DescribePresence(foundation))))
        };

        AddRawOrWarning(blocks, "JSON: soul_state", soul);
        AddRawOrWarning(blocks, "JSON: guardians", guardians);
        AddRawOrWarning(blocks, "JSON: pending_abode_offering", offering);
        AddRawOrWarning(blocks, "JSON: pending_player_guardian_foundation", foundation);
        return Completed(command, blocks);
    }

    private static async Task<ExplorerCommandResult> BuildGuardians(string command, FileSystemManager fs)
    {
        var read = await ReadJson(fs, GuardiansPath);
        if (read.Node == null)
            return MissingOrMalformed(command, "Хранители", read);

        return Completed(command,
            Panel("Хранители",
                Grid(
                    ("Хранителей", CountArray(read.Node, "guardians").ToString()),
                    ("Активный Хранитель", DescribeActiveGuardian(read.Node)),
                    ("Текущая Обитель", DescribeCurrentAbode(read.Node)),
                    ("Ожидаемое создание Хранителя", DescribeNodePresence(read.Node["pendingGuardianCreation"])))),
            Raw($"Полный JSON {GuardiansPath}", read.Node));
    }

    private static async Task<ExplorerCommandResult> BuildAbodePower(string command, FileSystemManager fs)
    {
        var guardians = await ReadJson(fs, GuardiansPath);
        var journal = await ReadJson(fs, AbodePowerJournalPath);

        var blocks = new List<UiBlock>
        {
            Panel("Сила Обители",
                Grid(
                    ("Хранителей с данными", CountGuardiansWithObject(guardians.Node, "abodePower").ToString()),
                    ("Событий силы", CountArray(journal.Node, "guardianPowerEvents").ToString()),
                    ("Журнал", DescribePresence(journal))))
        };

        AddRawOrWarning(blocks, $"Полный JSON {GuardiansPath}", guardians);
        AddRawOrWarning(blocks, $"Полный JSON {AbodePowerJournalPath}", journal);
        return Completed(command, blocks);
    }

    private static async Task<ExplorerCommandResult> BuildAbodes(string command, FileSystemManager fs)
    {
        var read = await ReadJson(fs, GuardiansPath);
        if (read.Node == null)
            return MissingOrMalformed(command, "Обители", read);

        return Completed(command,
            Panel("Обители Моря Хаоса",
                Grid(
                    ("Текущая Обитель", DescribeCurrentAbode(read.Node)),
                    ("Известных Обителей", CountKnownAbodes(read.Node).ToString()),
                    ("Хранителей с Обителью", CountGuardiansWithObject(read.Node, "abode").ToString()))),
            Raw($"Полный JSON {GuardiansPath}", read.Node));
    }

    private static async Task<ExplorerCommandResult> BuildGacha(string command, FileSystemManager fs)
    {
        var soul = await ReadJson(fs, SoulStatePath);
        var guardians = await ReadJson(fs, GuardiansPath);

        var blocks = new List<UiBlock>
        {
            Panel("Гача Моря Хаоса",
                Grid(
                    ("Чернильные Перья", DescribeInkFeathers(soul.Node)),
                    ("Хранителей с гачей", CountGuardiansWithObject(guardians.Node, "gachaSystem").ToString()),
                    ("Исторических попыток", CountGuardianNestedArray(guardians.Node, "gachaSystem", "gachaHistory").ToString()),
                    ("Примечание", "Прямой призыв и подношения остаются client-owned pending/local-turn действиями.")))
        };

        AddRawOrWarning(blocks, $"Полный JSON {SoulStatePath}", soul);
        AddRawOrWarning(blocks, $"Полный JSON {GuardiansPath}", guardians);
        return Completed(command, blocks);
    }

    private static async Task<ExplorerCommandResult> BuildGuardianPolitics(
        string command,
        FileSystemManager fs,
        bool includeAdvancedDiagnostics)
    {
        var politics = await ReadJson(fs, ChaosSeaGuardianPoliticsState.StatePath);
        if (politics.Node == null)
            return MissingOrMalformed(command, "Политика Хранителей", politics);

        var root = politics.Node;
        var relations = VisibleObjects(root[ChaosSeaGuardianPoliticsState.RelationsProperty] as JsonArray).ToList();
        var projects = VisibleObjects(root[ChaosSeaGuardianPoliticsState.ProjectsProperty] as JsonArray).ToList();
        var zones = VisibleObjects(root[ChaosSeaGuardianPoliticsState.InfluenceZonesProperty] as JsonArray).ToList();
        var chronicle = VisibleObjects(root[ChaosSeaGuardianPoliticsState.ChronicleProperty] as JsonArray).ToList();
        var hiddenCount =
            HiddenCount(root[ChaosSeaGuardianPoliticsState.RelationsProperty] as JsonArray) +
            HiddenCount(root[ChaosSeaGuardianPoliticsState.ProjectsProperty] as JsonArray) +
            HiddenCount(root[ChaosSeaGuardianPoliticsState.InfluenceZonesProperty] as JsonArray) +
            HiddenCount(root[ChaosSeaGuardianPoliticsState.ChronicleProperty] as JsonArray);

        var blocks = new List<UiBlock>
        {
            Panel("Политика Хранителей",
                Grid(
                    ("Видимых связей", relations.Count.ToString()),
                    ("Видимых проектов", projects.Count.ToString()),
                    ("Видимых зон влияния", zones.Count.ToString()),
                    ("Видимых записей хроники", chronicle.Count.ToString()),
                    ("Скрытых записей", hiddenCount.ToString())))
        };

        if (relations.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Связи Хранителей",
                Columns = ["Источник", "Цель", "Тип", "Отношение", "Причина"],
                Rows = relations.Select(static relation => new UiTableRow
                {
                    Cells =
                    [
                        GetString(relation, "sourceGuardianId"),
                        GetString(relation, "targetGuardianId"),
                        TranslateRelationType(GetString(relation, "relationType")),
                        GetNumberOrString(relation, "attitudeScore", "0"),
                        GetString(relation, "reason")
                    ]
                }).ToList()
            });
        }

        if (projects.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Политические проекты",
                Columns = ["Проект", "Владелец", "Цель", "Статус", "Прогресс", "Сводка"],
                Rows = projects.Select(static project => new UiTableRow
                {
                    Cells =
                    [
                        GetString(project, "projectId"),
                        GetString(project, "ownerGuardianId"),
                        GetString(project, "targetGuardianId"),
                        TranslateProjectStatus(GetString(project, "status")),
                        $"{GetNumberOrString(project, "currentProgress", "0")}/{GetNumberOrString(project, "requiredProgress", "0")}",
                        GetString(project, "summary")
                    ]
                }).ToList()
            });
        }

        if (zones.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Зоны влияния",
                Columns = ["Зона", "Хранитель", "Область", "Влияние", "Контроль"],
                Rows = zones.Select(static zone => new UiTableRow
                {
                    Cells =
                    [
                        GetString(zone, "displayName", "zoneId"),
                        GetString(zone, "guardianId"),
                        $"{GetString(zone, "scopeType")}:{GetString(zone, "scopeId")}",
                        GetNumberOrString(zone, "influenceValue", "0"),
                        GetNumberOrString(zone, "controlLevel", "0")
                    ]
                }).ToList()
            });
        }

        if (chronicle.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Хроника политики",
                Columns = ["Ход", "Событие", "Сводка"],
                Rows = chronicle.Select(static entry => new UiTableRow
                {
                    Cells =
                    [
                        GetNumberOrString(entry, "turnNumber", "0"),
                        GetString(entry, "eventType"),
                        GetString(entry, "summary")
                    ]
                }).ToList()
            });
        }

        if (blocks.Count == 1)
            blocks.Add(Message(UiNotificationSeverity.Info, "Нет видимых политических событий", "Скрытые связи не показываются игроку до раскрытия через сцену, хронику или прямое свидетельство."));

        if (includeAdvancedDiagnostics)
            blocks.Add(Raw($"Полный JSON {ChaosSeaGuardianPoliticsState.StatePath}", root));

        return Completed(command, blocks);
    }

    private static async Task<ExplorerCommandResult> BuildBundle(string command, FileSystemManager fs, string title, IReadOnlyList<SummarySpec> specs)
    {
        var grouped = specs.GroupBy(static spec => spec.Path, StringComparer.OrdinalIgnoreCase).ToArray();
        var reads = new Dictionary<string, JsonReadResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in grouped)
            reads[group.Key] = await ReadJson(fs, group.Key);

        var rows = new List<UiTableRow>();
        foreach (var spec in specs)
        {
            var read = reads[spec.Path];
            rows.Add(new UiTableRow
            {
                Cells =
                [
                    spec.Label,
                    spec.Path,
                    DescribeSpec(read, spec.PropertyName)
                ]
            });
        }

        var blocks = new List<UiBlock>
        {
            new UiTableBlock
            {
                Title = title,
                Columns = ["Раздел", "Файл", "Состояние"],
                Rows = rows
            }
        };

        foreach (var (path, read) in reads)
            AddRawOrWarning(blocks, $"Полный JSON {path}", read);

        return Completed(command, blocks);
    }

    private static string DescribeSpec(JsonReadResult read, string propertyName)
    {
        if (read.Node == null)
            return read.FileExists ? "повреждён" : "отсутствует";

        if (read.Node is JsonArray rootArray)
            return rootArray.Count.ToString();

        var node = read.Node is JsonObject obj ? obj[propertyName] : null;
        return node switch
        {
            JsonArray array => array.Count.ToString(),
            JsonObject nested => $"{nested.Count} полей",
            JsonValue value when TryGetScalarString(value, out var text) => EmptyFallback(text),
            _ => "не найдено"
        };
    }

    private static void AddRawOrWarning(List<UiBlock> blocks, string title, JsonReadResult read)
    {
        if (read.Node != null)
        {
            blocks.Add(Raw(title, read.Node));
            return;
        }

        if (read.FileExists)
            blocks.Add(Message(UiNotificationSeverity.Warning, title, $"Файл найден, но не разобран как JSON: {read.Path}. {read.Error}"));
    }

    private static async Task<JsonReadResult> ReadJson(FileSystemManager fs, string path)
    {
        var raw = await fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(raw))
            return new JsonReadResult(path, FileExists: fs.FileExists(path), Node: null, Error: string.Empty);

        try
        {
            return new JsonReadResult(path, FileExists: true, Node: JsonNode.Parse(raw), Error: string.Empty);
        }
        catch (JsonException ex)
        {
            return new JsonReadResult(path, FileExists: true, Node: null, Error: ex.Message);
        }
    }

    private static ExplorerCommandResult MissingOrMalformed(string command, string title, JsonReadResult read) =>
        Completed(command, Message(
            read.FileExists ? UiNotificationSeverity.Warning : UiNotificationSeverity.Info,
            title,
            read.FileExists
                ? $"Файл найден, но не разобран как JSON: {read.Path}. {read.Error}"
                : $"Файл отсутствует: {read.Path}."));

    private static ExplorerCommandResult Completed(string command, params UiBlock[] blocks) =>
        Completed(command, (IEnumerable<UiBlock>)blocks);

    private static ExplorerCommandResult Completed(string command, IEnumerable<UiBlock> blocks) =>
        new()
        {
            Command = command,
            State = CommandExecutionState.Completed,
            Blocks = blocks.ToList()
        };

    private static UiPanelBlock Panel(string title, params UiBlock[] blocks) =>
        new()
        {
            Title = title,
            Blocks = blocks.ToList()
        };

    private static UiKeyValueGridBlock Grid(params (string Key, string Value)[] items) =>
        new()
        {
            Items = items
                .Select(static item => new UiKeyValueItem { Key = item.Key, Value = EmptyFallback(item.Value) })
                .ToList()
        };

    private static UiMessageBlock Message(UiNotificationSeverity severity, string title, string message) =>
        new()
        {
            Severity = severity,
            Title = title,
            Message = message
        };

    private static UiRawJsonBlock Raw(string title, JsonNode node) =>
        new()
        {
            Title = title,
            Json = node.DeepClone()
        };

    private static string DescribeActiveGuardian(JsonNode? root)
    {
        var active = root?["activeGuardian"];
        var name = GetString(active, "guardianName", "name", "canonicalName");
        var id = GetString(active, "guardianId");
        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(id))
            return $"{name} ({id})";
        if (!string.IsNullOrWhiteSpace(name))
            return name;
        if (!string.IsNullOrWhiteSpace(id))
            return id;
        return "не указан";
    }

    private static string DescribeCurrentAbode(JsonNode? root)
    {
        var navigation = root?["chaosSeaNavigation"];
        var name = GetString(navigation, "currentAbodeName", "abodeName", "name");
        var id = GetString(navigation, "currentAbodeId", "abodeId");
        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(id))
            return $"{name} ({id})";
        if (!string.IsNullOrWhiteSpace(name))
            return name;
        if (!string.IsNullOrWhiteSpace(id))
            return id;
        return "не указана";
    }

    private static string DescribeInkFeathers(JsonNode? soulRoot)
    {
        var inkFeathers = soulRoot?["inkFeathers"];
        if (inkFeathers is JsonObject)
        {
            var current = GetNumberOrString(inkFeathers, "current");
            var total = GetNumberOrString(inkFeathers, "total");
            if (!string.IsNullOrWhiteSpace(current) || !string.IsNullOrWhiteSpace(total))
                return $"{EmptyFallback(current)} / всего {EmptyFallback(total)}";
        }

        return GetNumberOrString(soulRoot, "inkFeathers", "0");
    }

    private static string DescribePresence(JsonReadResult read)
    {
        if (read.Node != null)
            return "найдено";
        return read.FileExists ? "повреждено" : "отсутствует";
    }

    private static string DescribeNodePresence(JsonNode? node) =>
        node == null ? "отсутствует" : "найдено";

    private static int CountArray(JsonNode? root, string propertyName) =>
        root?[propertyName] is JsonArray array ? array.Count : 0;

    private static int CountKnownAbodes(JsonNode? root)
    {
        var navigation = root?["chaosSeaNavigation"];
        if (navigation?["knownAbodes"] is JsonArray knownAbodes)
            return knownAbodes.Count;
        if (navigation?["visitedAbodes"] is JsonArray visitedAbodes)
            return visitedAbodes.Count;
        return 0;
    }

    private static int CountGuardiansWithObject(JsonNode? root, string propertyName)
    {
        if (root?["guardians"] is not JsonArray guardians)
            return 0;

        return guardians
            .OfType<JsonObject>()
            .Count(guardian => guardian[propertyName] is JsonObject);
    }

    private static int CountGuardianNestedArray(JsonNode? root, string objectPropertyName, string arrayPropertyName)
    {
        if (root?["guardians"] is not JsonArray guardians)
            return 0;

        return guardians
            .OfType<JsonObject>()
            .Select(guardian => guardian[objectPropertyName]?[arrayPropertyName] as JsonArray)
            .Where(static array => array != null)
            .Sum(static array => array!.Count);
    }

    private static IEnumerable<JsonObject> VisibleObjects(JsonArray? array)
    {
        if (array == null)
            yield break;

        foreach (var item in array.OfType<JsonObject>())
        {
            if (!IsHidden(item))
                yield return item;
        }
    }

    private static int HiddenCount(JsonArray? array) =>
        array?.OfType<JsonObject>().Count(IsHidden) ?? 0;

    private static bool IsHidden(JsonObject item)
    {
        var visibility = GetString(item, "visibility");
        return string.Equals(visibility, "hidden", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(visibility, "gm_only", StringComparison.OrdinalIgnoreCase);
    }

    private static string TranslateRelationType(string relationType) =>
        relationType.ToLowerInvariant() switch
        {
            "alliance" => "союз",
            "rivalry" => "соперничество",
            "debt" => "долг",
            "fear" => "страх",
            "patronage" => "покровительство",
            "memory_oath" => "клятва памяти",
            "trade" => "обмен",
            "hostility" => "вражда",
            "hidden_dependency" => "скрытая зависимость",
            _ => EmptyFallback(relationType)
        };

    private static string TranslateProjectStatus(string status) =>
        status.ToLowerInvariant() switch
        {
            "active" => "активен",
            "blocked" => "заблокирован",
            "completed" => "завершён",
            "failed" => "провален",
            "abandoned" => "оставлен",
            _ => EmptyFallback(status)
        };

    private static string GetString(JsonNode? node, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (node?[propertyName] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
                return text.Trim();
        }

        return string.Empty;
    }

    private static string GetNumberOrString(JsonNode? node, string propertyName, string fallback = "")
    {
        if (node?[propertyName] is not JsonValue value)
            return fallback;

        if (value.TryGetValue<int>(out var intValue))
            return intValue.ToString();
        if (value.TryGetValue<long>(out var longValue))
            return longValue.ToString();
        if (value.TryGetValue<string>(out var text))
            return text.Trim();
        return fallback;
    }

    private static bool TryGetScalarString(JsonNode? node, out string value)
    {
        value = string.Empty;
        if (node is not JsonValue jsonValue)
            return false;

        if (jsonValue.TryGetValue<string>(out var text))
        {
            value = text;
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
            value = boolValue ? "true" : "false";
            return true;
        }

        return false;
    }

    private static string EmptyFallback(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "не указано" : value.Trim();

    private sealed record SummarySpec(string Path, string PropertyName, string Label);

    private sealed record JsonReadResult(string Path, bool FileExists, JsonNode? Node, string Error);
}
