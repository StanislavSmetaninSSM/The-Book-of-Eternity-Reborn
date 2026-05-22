using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.WebUi;

public sealed class BrowserMortalWorldWriteService
{
    public const string PendingCraftRequestPath = CraftRequestState.PendingRequestPath;

    private const string StatPointsPath = "game_state/player/stat_points.json";
    private const string CharacteristicsPath = "game_state/misc/characteristics.json";
    private const string NpcCorePath = "game_state/npcs/npc_core.json";
    private const string FactionCorePath = "game_state/factions/faction_core.json";

    private readonly FileSystemManager _fs;
    private readonly BrowserLocalWriteCoordinator _coordinator;
    private readonly ScenarioCoreService _scenarioCoreService;
    private readonly TimeProvider _timeProvider;

    public BrowserMortalWorldWriteService(
        FileSystemManager fs,
        BrowserLocalWriteCoordinator coordinator,
        ScenarioCoreService scenarioCoreService,
        TimeProvider? timeProvider = null)
    {
        _fs = fs;
        _coordinator = coordinator;
        _scenarioCoreService = scenarioCoreService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<BrowserPromptWriteResult> TryApplyAsync(
        string command,
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        var token = NormalizeCommand(command);
        return token switch
        {
            "/world_setup" or "/настройка_мира" => await ApplyWorldSetupAsync(answers, owner),
            "/distribute" or "/распределить" => await ApplyStatDistributionAsync(answers, owner),
            "/companion_directive" or "/директива_компаньону" => await ApplyCompanionDirectiveAsync(answers, owner),
            "/faction_directive" or "/директива_фракции" => await ApplyFactionDirectiveAsync(answers, owner),
            "/craft" or "/ремесло" => await ApplyCraftAsync(answers, owner),
            _ => BrowserPromptWriteResult.NotHandled()
        };
    }

    private async Task<BrowserPromptWriteResult> ApplyWorldSetupAsync(
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        var mode = ReadAnswer(answers, "world_setup_mode");
        if (string.Equals(mode, "clear", StringComparison.OrdinalIgnoreCase))
        {
            return await ExecuteAsync(
                owner,
                "Очистка подготовки следующего мира",
                [WorldDirectiveService.PendingSetupPath, ScenarioCoreService.ManifestPath],
                async () =>
                {
                    _fs.DeleteFile(WorldDirectiveService.PendingSetupPath);
                    await _scenarioCoreService.ClearAsync();
                },
                "Подготовка мира очищена",
                "Файлы подготовки следующей смертной жизни и сценарного ядра удалены.",
                new JsonObject { ["mode"] = "clear" });
        }

        if (!string.Equals(mode, "create_or_edit", StringComparison.OrdinalIgnoreCase))
        {
            return BrowserPromptWriteResult.ValidationError(
                "Поддерживаются только режимы create_or_edit и clear. Профили мира пока выбираются в консольном интерфейсе.");
        }

        var title = ReadAnswer(answers, "world_title");
        var directivesText = ReadAnswer(answers, "world_directives");
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(directivesText))
            return BrowserPromptWriteResult.ValidationError("Укажите название мира или директивы мира.");

        var now = NowText();
        var setup = new WorldDirectiveService.PendingWorldSetup
        {
            Mode = "manual",
            WorldDirectives = new WorldDirectiveService.WorldDirectives
            {
                WorldTitle = title,
                SettingSummary = directivesText,
                DetailedWorldDescription = directivesText,
                LastUpdated = now
            },
            LastUpdated = now
        };

        var payload = PendingWorldSetupToJson(setup);
        return await ExecuteAsync(
            owner,
            "Запись подготовки следующего мира",
            [WorldDirectiveService.PendingSetupPath, ScenarioCoreService.ManifestPath],
            async () =>
            {
                await _fs.WriteFileAtomicAsync(
                    WorldDirectiveService.PendingSetupPath,
                    JsonSerializer.Serialize(setup, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                await _scenarioCoreService.RefreshFromPendingSetupAsync();
            },
            "Подготовка мира записана",
            "Браузерная форма обновила client-owned подготовку следующей смертной жизни.",
            payload);
    }

    private async Task<BrowserPromptWriteResult> ApplyStatDistributionAsync(
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        var raw = ReadAnswer(answers, "stat_allocation_json");
        if (string.IsNullOrWhiteSpace(raw))
            return BrowserPromptWriteResult.ValidationError("Укажите JSON распределения характеристик.");

        if (!TryParseAllocation(raw, out var allocation, out var error))
            return BrowserPromptWriteResult.ValidationError(error);

        if (allocation.Count == 0)
            return BrowserPromptWriteResult.ValidationError("Распределение не содержит положительных значений.");

        var statPoints = await ReadObjectAsync(StatPointsPath) ?? new JsonObject();
        var available = ReadInt(statPoints, "unspentStatPoints", 0);
        var total = allocation.Values.Sum();
        if (total > available)
            return BrowserPromptWriteResult.ValidationError($"Недостаточно очков характеристик: доступно {available}, запрошено {total}.");

        var characteristics = await ReadObjectAsync(CharacteristicsPath) ?? new JsonObject();
        foreach (var (stat, amount) in allocation)
        {
            var current = ReadInt(characteristics, stat, 1);
            if (current + amount > 100)
                return BrowserPromptWriteResult.ValidationError($"Характеристика {stat} превысит максимум 100.");
        }

        return await ExecuteAsync(
            owner,
            "Распределение характеристик",
            [StatPointsPath, CharacteristicsPath, "game_state/player/computed_characteristics.json"],
            async () =>
            {
                foreach (var (stat, amount) in allocation)
                {
                    var current = ReadInt(characteristics, stat, 1);
                    characteristics[stat] = current + amount;
                }

                statPoints["unspentStatPoints"] = available - total;
                await _fs.WriteFileAtomicAsync(CharacteristicsPath, characteristics.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                await _fs.WriteFileAtomicAsync(StatPointsPath, statPoints.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            },
            "Характеристики распределены",
            $"Потрачено очков: {total}. Осталось: {available - total}.",
            AllocationToJson(allocation));
    }

    private async Task<BrowserPromptWriteResult> ApplyCompanionDirectiveAsync(
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        var companionId = ReadAnswer(answers, "companion_id");
        var directive = ReadAnswer(answers, "companion_directive");
        if (string.IsNullOrWhiteSpace(companionId))
            return BrowserPromptWriteResult.ValidationError("Укажите ID компаньона.");

        var root = await ReadNodeAsync(NpcCorePath);
        if (root == null)
            return BrowserPromptWriteResult.ValidationError("Файл npc_core.json не найден или пуст.");

        var target = FindObjectById(root, ["npcId", "id"], companionId);
        if (target == null)
            return BrowserPromptWriteResult.ValidationError($"Компаньон {companionId} не найден.");

        if (!string.Equals(ReadString(target, "progressionType"), "Companion", StringComparison.OrdinalIgnoreCase))
            return BrowserPromptWriteResult.ValidationError($"НПС {companionId} не является активным компаньоном.");

        return await ExecuteAsync(
            owner,
            "Запись директивы компаньона",
            [NpcCorePath],
            async () =>
            {
                target["playerCompanionDirective"] = string.IsNullOrWhiteSpace(directive) ? null : directive.Trim();
                await _fs.WriteFileAtomicAsync(NpcCorePath, root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            },
            "Директива компаньона записана",
            string.IsNullOrWhiteSpace(directive)
                ? $"Директива компаньона {companionId} очищена."
                : $"Директива компаньона {companionId} обновлена.",
            new JsonObject
            {
                ["companionId"] = companionId,
                ["playerCompanionDirective"] = string.IsNullOrWhiteSpace(directive) ? null : directive.Trim()
            });
    }

    private async Task<BrowserPromptWriteResult> ApplyFactionDirectiveAsync(
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        var factionId = ReadAnswer(answers, "faction_id");
        var directive = ReadAnswer(answers, "faction_directive");
        if (string.IsNullOrWhiteSpace(factionId))
            return BrowserPromptWriteResult.ValidationError("Укажите ID фракции.");

        var root = await ReadNodeAsync(FactionCorePath);
        if (root == null)
            return BrowserPromptWriteResult.ValidationError("Файл faction_core.json не найден или пуст.");

        var target = FindObjectById(root, ["factionId", "id"], factionId);
        if (target == null)
            return BrowserPromptWriteResult.ValidationError($"Фракция {factionId} не найдена.");

        if (!ReadBool(target, "isPlayerFaction") && !ReadBool(target, "isPlayerMember"))
            return BrowserPromptWriteResult.ValidationError($"Фракция {factionId} не является фракцией игрока или членством игрока.");

        return await ExecuteAsync(
            owner,
            "Запись директивы фракции",
            [FactionCorePath],
            async () =>
            {
                target["playerStrategyDirective"] = string.IsNullOrWhiteSpace(directive) ? null : directive.Trim();
                await _fs.WriteFileAtomicAsync(FactionCorePath, root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            },
            "Директива фракции записана",
            string.IsNullOrWhiteSpace(directive)
                ? $"Стратегическая директива фракции {factionId} очищена."
                : $"Стратегическая директива фракции {factionId} обновлена.",
            new JsonObject
            {
                ["factionId"] = factionId,
                ["playerStrategyDirective"] = string.IsNullOrWhiteSpace(directive) ? null : directive.Trim()
            });
    }

    private async Task<BrowserPromptWriteResult> ApplyCraftAsync(
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        var recipeId = ReadAnswer(answers, "recipe_id");
        var intent = ReadAnswer(answers, "craft_intent");
        if (string.IsNullOrWhiteSpace(recipeId))
            return BrowserPromptWriteResult.ValidationError("Укажите рецепт или название рецепта.");
        if (string.IsNullOrWhiteSpace(intent))
            return BrowserPromptWriteResult.ValidationError("Опишите ремесленное действие.");

        var request = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["requestId"] = "craft_" + Guid.NewGuid().ToString("N"),
            ["createdAtUtc"] = NowText(),
            ["source"] = "browser",
            ["status"] = "pending_gm_resolution",
            ["recipeId"] = recipeId.Trim(),
            ["craftIntent"] = intent.Trim()
        };

        return await ExecuteAsync(
            owner,
            "Запись ремесленного запроса",
            [PendingCraftRequestPath],
            async () => await _fs.WriteFileAtomicAsync(PendingCraftRequestPath, request.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)),
            "Ремесленный запрос записан",
            "Создан pending-запрос для разрешения ремесленного действия ГМ.",
            request);
    }

    private async Task<BrowserPromptWriteResult> ExecuteAsync(
        LocalUiSessionLockOwner owner,
        string operationLabel,
        IReadOnlyCollection<string> rollbackPaths,
        Func<Task> writeOperation,
        string title,
        string message,
        JsonObject payload)
    {
        var result = await _coordinator.ExecuteAsync(
            new BrowserLocalWriteRequest(owner.OwnerId, owner.OwnerLabel, operationLabel),
            rollbackPaths,
            writeOperation);

        if (result.Success)
            return BrowserPromptWriteResult.Completed(title, message, payload);

        return BrowserPromptWriteResult.Failed(
            result.IsBlocked ? CommandExecutionState.Blocked : CommandExecutionState.Failed,
            result.IsBlocked ? UiNotificationSeverity.Warning : UiNotificationSeverity.Error,
            result.IsBlocked ? "Запись заблокирована" : "Ошибка записи",
            result.Message);
    }

    private async Task<JsonNode?> ReadNodeAsync(string path)
    {
        var raw = await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            return JsonNode.Parse(raw);
        }
        catch
        {
            return null;
        }
    }

    private async Task<JsonObject?> ReadObjectAsync(string path) =>
        await ReadNodeAsync(path) as JsonObject;

    private static bool TryParseAllocation(string raw, out Dictionary<string, int> allocation, out string error)
    {
        allocation = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        error = string.Empty;

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(raw);
        }
        catch (Exception ex)
        {
            error = $"JSON распределения не читается: {ex.Message}";
            return false;
        }

        if (node is not JsonObject obj)
        {
            error = "JSON распределения должен быть объектом.";
            return false;
        }

        var validStats = Characteristics.All.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in obj)
        {
            if (!validStats.Contains(key))
            {
                error = $"Неизвестная характеристика: {key}.";
                return false;
            }

            if (!TryReadInt(value, out var amount) || amount <= 0)
            {
                error = $"Значение для {key} должно быть положительным целым числом.";
                return false;
            }

            allocation[key] = amount;
        }

        return true;
    }

    private static JsonObject? FindObjectById(JsonNode node, IReadOnlyCollection<string> idProperties, string expectedId)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in idProperties)
            {
                if (string.Equals(ReadString(obj, property), expectedId, StringComparison.OrdinalIgnoreCase))
                    return obj;
            }

            foreach (var (_, child) in obj)
            {
                if (child == null)
                    continue;

                var found = FindObjectById(child, idProperties, expectedId);
                if (found != null)
                    return found;
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item == null)
                    continue;

                var found = FindObjectById(item, idProperties, expectedId);
                if (found != null)
                    return found;
            }
        }

        return null;
    }

    private static JsonObject PendingWorldSetupToJson(WorldDirectiveService.PendingWorldSetup setup)
    {
        var serialized = JsonSerializer.Serialize(setup, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
        return JsonNode.Parse(serialized)!.AsObject();
    }

    private static JsonObject AllocationToJson(Dictionary<string, int> allocation)
    {
        var obj = new JsonObject();
        foreach (var (key, value) in allocation)
            obj[key] = value;
        return obj;
    }

    private static string NormalizeCommand(string command)
    {
        var trimmed = command.Trim();
        var split = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return split.Length == 0 ? string.Empty : split[0].ToLowerInvariant();
    }

    private static string ReadAnswer(IReadOnlyDictionary<string, JsonNode?> answers, string key)
    {
        if (!answers.TryGetValue(key, out var node) || node == null)
            return string.Empty;

        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text))
                return text?.Trim() ?? string.Empty;
            if (value.TryGetValue<int>(out var number))
                return number.ToString();
            if (value.TryGetValue<bool>(out var flag))
                return flag ? "true" : "false";
        }

        return node.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
    }

    private static string ReadString(JsonObject obj, string propertyName)
    {
        if (!obj.TryGetPropertyValue(propertyName, out var node) || node is not JsonValue value)
            return string.Empty;

        if (value.TryGetValue<string>(out var text))
            return text ?? string.Empty;
        if (value.TryGetValue<int>(out var number))
            return number.ToString();
        return string.Empty;
    }

    private static int ReadInt(JsonObject obj, string propertyName, int fallback)
    {
        if (!obj.TryGetPropertyValue(propertyName, out var node) || !TryReadInt(node, out var value))
            return fallback;
        return value;
    }

    private static bool TryReadInt(JsonNode? node, out int value)
    {
        value = 0;
        if (node is not JsonValue jsonValue)
            return false;

        if (jsonValue.TryGetValue<int>(out value))
            return true;

        if (jsonValue.TryGetValue<string>(out var text) && int.TryParse(text, out value))
            return true;

        return false;
    }

    private static bool ReadBool(JsonObject obj, string propertyName)
    {
        if (!obj.TryGetPropertyValue(propertyName, out var node) || node is not JsonValue value)
            return false;
        return value.TryGetValue<bool>(out var parsed) && parsed;
    }

    private string NowText() => _timeProvider.GetUtcNow().UtcDateTime.ToString("O");
}

public sealed record BrowserPromptWriteResult(
    bool Handled,
    bool Success,
    bool KeepSessionOpen,
    CommandExecutionState State,
    UiNotificationSeverity Severity,
    string Title,
    string Message,
    JsonObject? Payload = null)
{
    public static BrowserPromptWriteResult NotHandled() =>
        new(false, false, false, CommandExecutionState.Completed, UiNotificationSeverity.Info, string.Empty, string.Empty);

    public static BrowserPromptWriteResult Completed(string title, string message, JsonObject payload) =>
        new(true, true, false, CommandExecutionState.Completed, UiNotificationSeverity.Success, title, message, payload);

    public static BrowserPromptWriteResult ValidationError(string message) =>
        new(true, false, true, CommandExecutionState.RequiresInput, UiNotificationSeverity.Error, "Ошибка формы", message);

    public static BrowserPromptWriteResult Failed(
        CommandExecutionState state,
        UiNotificationSeverity severity,
        string title,
        string message) =>
        new(true, false, false, state, severity, title, message);
}
