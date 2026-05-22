using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

public static class ExplorerShiningAbodeCommandResultBuilder
{
    private const string SoulStatePath = "game_state/meta/soul_state.json";
    private const string GuardiansPath = "game_state/meta/guardians.json";

    private enum CommandKind
    {
        Overview,
        Politics,
        Treasury,
        SourceOfLight
    }

    private static readonly IReadOnlyDictionary<string, CommandKind> CommandKinds =
        new Dictionary<string, CommandKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["/shining_abode"] = CommandKind.Overview,
            ["/сияющая_обитель"] = CommandKind.Overview,
            ["/shining_politics"] = CommandKind.Politics,
            ["/сияющая_политика"] = CommandKind.Politics,
            ["/shining_treasury"] = CommandKind.Treasury,
            ["/казначейство"] = CommandKind.Treasury,
            ["/source_of_light"] = CommandKind.SourceOfLight,
            ["/источник_света"] = CommandKind.SourceOfLight
        };

    public static bool CanBuild(string command) => CommandKinds.ContainsKey(command.Trim());

    public static async Task<ExplorerCommandResult?> TryBuildAsync(
        string command,
        StateManager stateManager,
        FileSystemManager fs)
    {
        var normalizedCommand = command.Trim();
        if (!CommandKinds.TryGetValue(normalizedCommand, out var kind))
            return null;

        await stateManager.RefreshGameStateAsync();

        return kind switch
        {
            CommandKind.Overview => await BuildOverview(normalizedCommand, fs, stateManager),
            CommandKind.Politics => await BuildPolitics(normalizedCommand, fs),
            CommandKind.Treasury => await BuildTreasury(normalizedCommand, fs),
            CommandKind.SourceOfLight => await BuildSourceOfLight(normalizedCommand, fs),
            _ => null
        };
    }

    private static async Task<ExplorerCommandResult> BuildOverview(string command, FileSystemManager fs, StateManager stateManager)
    {
        var shining = await ReadJson(fs, ShiningAbodeState.StatePath);
        var residents = await ReadJson(fs, GuardianAbodeResidentState.StatePath);
        var soul = await ReadJson(fs, SoulStatePath);
        var core = await ReadJson(fs, ShiningCoreActionRequestState.PendingActionsRequestPath);
        var trade = await ReadJson(fs, ShiningTradeRequestState.PendingRequestsPath);
        var source = await ReadJson(fs, SourceOfLightCapstoneState.PendingRequestPath);

        var blocks = new List<UiBlock>
        {
            Panel("Сияющая Обитель",
                Grid(
                    ("Фаза", EmptyFallback(stateManager.CurrentState.CurrentRealm)),
                    ("Душа", EmptyFallback(stateManager.CurrentState.SoulName)),
                    ("Доступность", GetString(shining.Node, "availability", "не указана")),
                    ("Сияние", DescribeRadiance(shining.Node)),
                    ("Искры Света", GetNumberOrString(shining.Node, "lightSparks", "0")),
                    ("Залов", CountArray(shining.Node, "halls").ToString()),
                    ("Фракций", CountArray(shining.Node, "factions").ToString()),
                    ("Резидентов", CountArray(residents.Node, GuardianAbodeResidentState.EntriesProperty).ToString()),
                    ("Открытый черновик Врат", GetBoolText(shining.Node?["gates"]?["hasOpenDraft"])),
                    ("Ожиданий Обители", CountRequests(core).ToString()),
                    ("Ожиданий торговли", CountRequests(trade).ToString()),
                    ("Источник Света", DescribePresence(source))))
        };

        AddRawOrWarning(blocks, $"Полный JSON {ShiningAbodeState.StatePath}", shining);
        AddRawOrWarning(blocks, $"Полный JSON {GuardianAbodeResidentState.StatePath}", residents);
        AddRawOrWarning(blocks, $"Полный JSON {SoulStatePath}", soul);
        AddRawOrWarning(blocks, $"Полный JSON {ShiningCoreActionRequestState.PendingActionsRequestPath}", core);
        AddRawOrWarning(blocks, $"Полный JSON {ShiningTradeRequestState.PendingRequestsPath}", trade);
        AddRawOrWarning(blocks, $"Полный JSON {SourceOfLightCapstoneState.PendingRequestPath}", source);
        return Completed(command, blocks);
    }

    private static async Task<ExplorerCommandResult> BuildPolitics(string command, FileSystemManager fs)
    {
        var shining = await ReadJson(fs, ShiningAbodeState.StatePath);
        var residents = await ReadJson(fs, GuardianAbodeResidentState.StatePath);
        var guardians = await ReadJson(fs, GuardiansPath);
        var foundings = await ReadJson(fs, ShiningFactionRequestState.PendingFoundingsRequestPath);
        var realignments = await ReadJson(fs, ShiningFactionRequestState.PendingRealignmentsRequestPath);
        var leadership = await ReadJson(fs, ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath);

        var blocks = new List<UiBlock>
        {
            Panel("Политика Сияющей Обители",
                Grid(
                    ("Фракций", CountArray(shining.Node, "factions").ToString()),
                    ("Светозарных акторов", CountArray(shining.Node, "shiningPoliticalActors").ToString()),
                    ("Кампаний против фракций", CountArray(shining.Node, ShiningAbodeState.FactionConflictCampaignsProperty).ToString()),
                    ("Резидентов", CountArray(residents.Node, GuardianAbodeResidentState.EntriesProperty).ToString()),
                    ("Ожиданий основания", CountRequests(foundings).ToString()),
                    ("Ожиданий перехода", CountRequests(realignments).ToString()),
                    ("Ожиданий смены власти", CountRequests(leadership).ToString())))
        };

        AddRawOrWarning(blocks, $"Полный JSON {ShiningAbodeState.StatePath}", shining);
        AddRawOrWarning(blocks, $"Полный JSON {GuardianAbodeResidentState.StatePath}", residents);
        AddRawOrWarning(blocks, $"Полный JSON {GuardiansPath}", guardians);
        AddRawOrWarning(blocks, $"Полный JSON {ShiningFactionRequestState.PendingFoundingsRequestPath}", foundings);
        AddRawOrWarning(blocks, $"Полный JSON {ShiningFactionRequestState.PendingRealignmentsRequestPath}", realignments);
        AddRawOrWarning(blocks, $"Полный JSON {ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath}", leadership);
        return Completed(command, blocks);
    }

    private static async Task<ExplorerCommandResult> BuildTreasury(string command, FileSystemManager fs)
    {
        var shining = await ReadJson(fs, ShiningAbodeState.StatePath);
        var soul = await ReadJson(fs, SoulStatePath);
        var treasury = shining.Node?["treasury"];

        var blocks = new List<UiBlock>
        {
            Panel("Казначейство Сияющей Обители",
                Grid(
                    ("Режим браузера", "локальные операции доступны через форму"),
                    ("Чернильные Перья души", DescribeInkFeathers(soul.Node)),
                    ("Искры Света", GetNumberOrString(shining.Node, "lightSparks", "0")),
                    ("Вклад Перьями", GetNumberOrString(treasury, "depositedInkFeathers", "0")),
                    ("Проценты к получению", GetNumberOrString(treasury, "claimableInkFeatherInterest", "0")),
                    ("Цикл процентов", GetString(treasury, "lastInterestSettlementCycleId", "не указан")),
                    ("Цикл обмена", GetString(treasury, "exchangeCycleId", "не указан")),
                    ("Искр обменяно в цикле", GetNumberOrString(treasury, "exchangeThisCycleLightSparks", "0")))),
            Message(
                UiNotificationSeverity.Info,
                "Браузерная запись",
                "Форма использует общий протокол локальной блокировки/отката UI и блокируется при активном ходе ГМа.")
        };

        AddRawOrWarning(blocks, $"Полный JSON {ShiningAbodeState.StatePath}", shining);
        AddRawOrWarning(blocks, $"Полный JSON {SoulStatePath}", soul);
        return Result(
            command,
            CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "treasury_operation",
                    Prompt = "Операция казначейства",
                    Required = true,
                    Options =
                    [
                        Option("deposit", "Внести Перья", "Перевести Чернильные Перья души во вклад казны."),
                        Option("withdraw", "Вывести Перья", "Вернуть Чернильные Перья из вклада душе."),
                        Option("claim_interest", "Получить проценты", "Начислить и получить проценты текущего сияющего цикла."),
                        Option("exchange", "Обменять на Искры", "Потратить Перья на Искры Света в рамках лимита цикла.")
                    ]
                },
                new UiTextInputPrompt
                {
                    Id = "treasury_amount",
                    Prompt = "Сумма",
                    Placeholder = "Для процентов можно оставить 0"
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildSourceOfLight(string command, FileSystemManager fs)
    {
        var shining = await ReadJson(fs, ShiningAbodeState.StatePath);
        var soul = await ReadJson(fs, SoulStatePath);
        var pending = await ReadJson(fs, SourceOfLightCapstoneState.PendingRequestPath);

        var status = DescribeSourceOfLightStatus(shining.Node, soul.Node, pending);
        var blocks = new List<UiBlock>
        {
            Panel("Источник Света",
                Grid(
                    ("Режим браузера", "создание ожидающего запроса доступно через форму"),
                    ("Статус", status),
                    ("Сияние", DescribeRadiance(shining.Node)),
                    ("Награда-пассив", SourceOfLightCapstoneState.PassiveId),
                    ("Награда-реликвия", SourceOfLightCapstoneState.RelicId))),
            Message(
                UiNotificationSeverity.Info,
                "Браузерная запись",
                "Форма создаёт pending_source_of_light_capstone.json только если требования полного Сияния выполнены.")
        };

        AddRawOrWarning(blocks, $"Полный JSON {ShiningAbodeState.StatePath}", shining);
        AddRawOrWarning(blocks, $"Полный JSON {SoulStatePath}", soul);
        AddRawOrWarning(blocks, $"Полный JSON {SourceOfLightCapstoneState.PendingRequestPath}", pending);
        return Result(
            command,
            CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "source_of_light_action",
                    Prompt = "Действие Источника Света",
                    Required = true,
                    Options =
                    [
                        Option("open", "Открыть Источник", "Создать клиентский ожидающий запрос для вершинной сцены.")
                    ]
                }
            ]);
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

    private static ExplorerCommandResult Completed(string command, IEnumerable<UiBlock> blocks) =>
        Result(command, CommandExecutionState.Completed, blocks);

    private static ExplorerCommandResult Result(
        string command,
        CommandExecutionState state,
        IEnumerable<UiBlock> blocks,
        IEnumerable<UiPrompt>? prompts = null) =>
        new()
        {
            Command = command,
            State = state,
            Blocks = blocks.ToList(),
            Prompts = prompts?.ToList() ?? []
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

    private static UiSelectionOption Option(string value, string label, string description) =>
        new() { Value = value, Label = label, Description = description };

    private static UiRawJsonBlock Raw(string title, JsonNode node) =>
        new()
        {
            Title = title,
            Json = node.DeepClone()
        };

    private static string DescribeRadiance(JsonNode? root)
    {
        var radiance = root?["radiance"];
        var experience = GetNumberOrString(radiance, "experience", "0");
        var tier = GetNumberOrString(radiance, "tier", "0");
        return $"{experience} опыта / тир {tier}";
    }

    private static string DescribeInkFeathers(JsonNode? soulRoot)
    {
        var inkFeathers = soulRoot?["inkFeathers"];
        if (inkFeathers is JsonObject)
        {
            var current = GetNumberOrString(inkFeathers, "current", "0");
            var total = GetNumberOrString(inkFeathers, "total", "0");
            return $"{current} / всего {total}";
        }

        return GetNumberOrString(soulRoot, "inkFeathers", "0");
    }

    private static string DescribeSourceOfLightStatus(JsonNode? shiningRoot, JsonNode? soulRoot, JsonReadResult pending)
    {
        if (pending.Node != null)
            return "ожидает закрытия ГМ";
        if (pending.FileExists)
            return "pending-файл повреждён";
        if (shiningRoot?[SourceOfLightCapstoneState.ShiningStateProperty]?["completed"] is JsonValue completedValue &&
            completedValue.TryGetValue<bool>(out var completed) &&
            completed)
        {
            return "завершён";
        }
        if (soulRoot?["afterlifeCombatProfile"]?[SourceOfLightCapstoneState.CapstonesProperty]?[SourceOfLightCapstoneState.LightIncarnateProperty] != null)
            return "пассив уже есть у души";

        var experience = GetInt(shiningRoot?["radiance"], "experience");
        var tier = GetInt(shiningRoot?["radiance"], "tier");
        return tier >= SourceOfLightCapstoneState.RequiredRadianceTier &&
               experience >= SourceOfLightCapstoneState.RequiredRadianceExperience
            ? "требования выполнены; создание запроса доступно только через console/local-turn UX"
            : $"закрыт: нужно {SourceOfLightCapstoneState.RequiredRadianceExperience} опыта и тир {SourceOfLightCapstoneState.RequiredRadianceTier}";
    }

    private static string DescribePresence(JsonReadResult read)
    {
        if (read.Node != null)
            return "найдено";
        return read.FileExists ? "повреждено" : "отсутствует";
    }

    private static int CountRequests(JsonReadResult read)
    {
        if (read.Node is JsonArray rootArray)
            return rootArray.Count;
        if (read.Node?["requests"] is JsonArray requests)
            return requests.Count;
        return 0;
    }

    private static int CountArray(JsonNode? root, string propertyName) =>
        root?[propertyName] is JsonArray array ? array.Count : 0;

    private static string GetBoolText(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<bool>(out var result))
            return result ? "да" : "нет";
        return "не указано";
    }

    private static int GetInt(JsonNode? node, string propertyName)
    {
        if (node?[propertyName] is JsonValue value)
        {
            if (value.TryGetValue<int>(out var intValue))
                return intValue;
            if (value.TryGetValue<long>(out var longValue))
                return longValue > int.MaxValue ? int.MaxValue : (int)longValue;
        }

        return 0;
    }

    private static string GetString(JsonNode? node, string propertyName, string fallback)
    {
        if (node?[propertyName] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
            return text.Trim();
        return fallback;
    }

    private static string GetNumberOrString(JsonNode? node, string propertyName, string fallback)
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

    private static string EmptyFallback(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "не указано" : value.Trim();

    private sealed record JsonReadResult(string Path, bool FileExists, JsonNode? Node, string Error);
}
