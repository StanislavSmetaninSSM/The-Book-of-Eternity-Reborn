using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.WebUi;

public sealed class BrowserSarefStoryWriteService
{
    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly BrowserLocalWriteCoordinator _coordinator;

    public BrowserSarefStoryWriteService(
        FileSystemManager fs,
        StateManager stateManager,
        BrowserLocalWriteCoordinator coordinator)
    {
        _fs = fs;
        _stateManager = stateManager;
        _coordinator = coordinator;
    }

    public async Task<BrowserPromptWriteResult> TryApplyAsync(
        string command,
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        var normalized = NormalizeCommand(command);
        return normalized switch
        {
            "/сареф найти_крылья" or "/saref find_wings" => await ApplyFindWingsAsync(answers, owner),
            "/сареф преимущество" or "/saref use_advantage" => BuildAdvantageUsePayload(answers),
            "/сареф конфронтация" or "/saref confrontation" => BuildFinalConfrontationPayload(answers),
            "/сареф разорвать_клятву" or "/saref break_oath" => BuildOathBreakPayload(answers),
            "/сареф поручение" or "/saref agenda" => BuildAgendaPayload(answers),
            _ => BrowserPromptWriteResult.NotHandled()
        };
    }

    private async Task<BrowserPromptWriteResult> ApplyFindWingsAsync(
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        var action = ReadAnswer(answers, "saref_wings_action");
        if (!string.Equals(action, "start", StringComparison.OrdinalIgnoreCase))
            return BrowserPromptWriteResult.ValidationError("Подтвердите начало поиска Крыльев Ангелов.");

        JsonObject? payload = null;
        var result = await _coordinator.ExecuteAsync(
            new BrowserLocalWriteRequest(owner.OwnerId, owner.OwnerLabel, "Browser Saref Wings search"),
            [SarefMainStoryState.PendingWingsInfiltrationPath],
            async () =>
            {
                await _stateManager.RefreshGameStateAsync();
                if (!_stateManager.CurrentState.IsInShiningAbode)
                    throw new InvalidOperationException("Поиск Крыльев доступен только в обычной активной Сияющей Обители.");

                var shiningRoot = await ReadRequiredObjectAsync(ShiningAbodeState.StatePath, "shining_abode_state.json недоступен.");
                var pending = await SarefMainStoryState.ReadWingsInfiltrationRequestStateAsync(_fs);
                if (pending.IsMalformed)
                    throw new InvalidOperationException($"{SarefMainStoryState.PendingWingsInfiltrationPath} повреждён: {pending.Error}.");
                if (pending.Request != null)
                    throw new InvalidOperationException("Поиск Крыльев уже ожидает закрытия ГМ.");

                var blocker = await SourceOfLightCapstoneState.TryDescribeBlockingPendingContractAsync(_fs, shiningRoot);
                if (blocker != null)
                    throw new InvalidOperationException($"Поиск Крыльев заблокирован: есть {blocker}.");

                var storyRoot = await ReadRequiredObjectAsync(SarefMainStoryState.StatePath, "main_story_saref_state.json недоступен.");
                var request = SarefMainStoryState.BuildWingsInfiltrationRequest(
                    storyRoot,
                    Math.Max(1, _stateManager.CurrentState.TurnNumber + 1));
                if (request == null)
                    throw new InvalidOperationException("Ты пока не знаешь, что искать.");

                await SarefMainStoryState.WriteWingsInfiltrationRequestAsync(_fs, request);
                payload = BuildWingsInfiltrationPayload(request);
                await _stateManager.RefreshGameStateAsync();
            });

        if (!result.Success)
        {
            return BrowserPromptWriteResult.Failed(
                result.IsBlocked ? CommandExecutionState.Blocked : CommandExecutionState.Failed,
                result.IsBlocked ? UiNotificationSeverity.Warning : UiNotificationSeverity.Error,
                result.IsBlocked ? "Запись заблокирована" : "Ошибка записи",
                result.Message);
        }

        return BrowserPromptWriteResult.Completed(
            "Поиск Крыльев начат",
            "Браузер создал ожидающий запрос поиска Крыльев Ангелов и подготовил действие для ГМа.",
            payload ?? new JsonObject());
    }

    private static BrowserPromptWriteResult BuildAdvantageUsePayload(IReadOnlyDictionary<string, JsonNode?> answers)
    {
        var advantageId = ReadAnswer(answers, "saref_advantage_id");
        var sceneType = ReadAnswer(answers, "saref_scene_type");
        var summary = ReadAnswer(answers, "saref_action_summary");
        if (string.IsNullOrWhiteSpace(advantageId))
            return BrowserPromptWriteResult.ValidationError("Выберите преимущество.");
        if (string.IsNullOrWhiteSpace(sceneType))
            return BrowserPromptWriteResult.ValidationError("Выберите тип сцены.");
        if (string.IsNullOrWhiteSpace(summary))
            return BrowserPromptWriteResult.ValidationError("Опишите применение преимущества.");

        var gmAction =
            $"[SAREF_ADVANTAGE_USE: {advantageId.Trim()}] {summary.Trim()}\n\n" +
            $"Разреши это как применение преимущества Сарефа в сцене `{sceneType.Trim()}`. " +
            "Если преимущество применимо и не подавлено, запиши sarefMainStoryUpdate с sarefAdvantageUses[]; " +
            "для одноразового преимущества переведи его в state=spent со spentAudit. Не изменяй сюжетную линию без accepted-turn validation.";
        return BrowserPromptWriteResult.Completed(
            "Преимущество подготовлено",
            "Браузер сформировал действие для ГМа; состояние изменится только через accepted turn.",
            new JsonObject
            {
                ["playerActionTag"] = "SAREF_ADVANTAGE_USE",
                ["advantageId"] = advantageId.Trim(),
                ["sceneType"] = sceneType.Trim(),
                ["summary"] = summary.Trim(),
                ["expectedResponseSurface"] = SarefMainStoryState.ResponseField,
                ["gmAction"] = gmAction
            });
    }

    private static BrowserPromptWriteResult BuildFinalConfrontationPayload(IReadOnlyDictionary<string, JsonNode?> answers)
    {
        var routeType = ReadAnswer(answers, "saref_route_type");
        var intent = ReadAnswer(answers, "saref_resolution_intent");
        var summary = ReadAnswer(answers, "saref_action_summary");
        if (string.IsNullOrWhiteSpace(routeType))
            return BrowserPromptWriteResult.ValidationError("Выберите маршрут развязки.");
        if (string.IsNullOrWhiteSpace(intent))
            return BrowserPromptWriteResult.ValidationError("Выберите намерение игрока.");
        if (string.IsNullOrWhiteSpace(summary))
            return BrowserPromptWriteResult.ValidationError("Опишите действие игрока.");

        var gmAction =
            $"[SAREF_FINAL_CONFRONTATION: {routeType.Trim()}] {summary.Trim()}\n\n" +
            $"Намерение игрока: `{intent.Trim()}`. Если сцена закрывает линию Сарефа, используй sarefMainStoryUpdate.mode={SarefMainStoryState.FinalUpdateModeRecord}. " +
            "Для deal route обязательно зафиксируй playerOathState, rewardBundle и postStoryAgenda; для победы укажи route proof, Saref outcome и Wings faction outcome.";
        return BrowserPromptWriteResult.Completed(
            "Развязка подготовлена",
            "Браузер сформировал действие для ГМа; финал фиксируется только через accepted turn.",
            new JsonObject
            {
                ["playerActionTag"] = "SAREF_FINAL_CONFRONTATION",
                ["routeType"] = routeType.Trim(),
                ["resolutionIntent"] = intent.Trim(),
                ["summary"] = summary.Trim(),
                ["expectedResponseSurface"] = SarefMainStoryState.ResponseField,
                ["gmAction"] = gmAction
            });
    }

    private static BrowserPromptWriteResult BuildOathBreakPayload(IReadOnlyDictionary<string, JsonNode?> answers)
    {
        var route = ReadAnswer(answers, "saref_oath_break_route");
        var summary = ReadAnswer(answers, "saref_action_summary");
        if (string.IsNullOrWhiteSpace(route))
            return BrowserPromptWriteResult.ValidationError("Выберите путь разрыва клятвы.");
        if (string.IsNullOrWhiteSpace(summary))
            return BrowserPromptWriteResult.ValidationError("Опишите попытку разрыва клятвы.");

        var gmAction =
            $"[SAREF_OATH_BREAK: {route.Trim()}] {summary.Trim()}\n\n" +
            $"Если попытка продвигает или закрывает разрыв клятвы, используй sarefMainStoryUpdate.mode={SarefMainStoryState.OathBreakUpdateModeRecord}. " +
            "Укажи oathBreakArc, proof, consequences, advantageUseIds[] при применении преимущества и обнови playerOathState только при доказанном результате.";
        return BrowserPromptWriteResult.Completed(
            "Разрыв клятвы подготовлен",
            "Браузер сформировал действие для ГМа; клятва меняется только через accepted turn.",
            new JsonObject
            {
                ["playerActionTag"] = "SAREF_OATH_BREAK",
                ["route"] = route.Trim(),
                ["summary"] = summary.Trim(),
                ["expectedResponseSurface"] = SarefMainStoryState.ResponseField,
                ["gmAction"] = gmAction
            });
    }

    private static BrowserPromptWriteResult BuildAgendaPayload(IReadOnlyDictionary<string, JsonNode?> answers)
    {
        var agendaAction = ReadAnswer(answers, "saref_agenda_action");
        var summary = ReadAnswer(answers, "saref_action_summary");
        if (string.IsNullOrWhiteSpace(agendaAction))
            return BrowserPromptWriteResult.ValidationError("Выберите тип поручения.");
        if (string.IsNullOrWhiteSpace(summary))
            return BrowserPromptWriteResult.ValidationError("Опишите действие игрока.");

        var gmAction =
            $"[SAREF_OATHBOUND_AGENDA: {agendaAction.Trim()}] {summary.Trim()}\n\n" +
            $"Если действие продвигает послесюжетную повестку Сарефа, используй sarefMainStoryUpdate.mode={SarefMainStoryState.PostStoryUpdateModeRecordAgenda}. " +
            "Связывай поручения с Shining factionConflictCampaigns[] и breakthroughLog[].type=saref_directive; сцену доминирования фиксируй только когда значимых противников Крыльев больше нет.";
        return BrowserPromptWriteResult.Completed(
            "Поручение подготовлено",
            "Браузер сформировал действие для ГМа; повестка меняется только через accepted turn.",
            new JsonObject
            {
                ["playerActionTag"] = "SAREF_OATHBOUND_AGENDA",
                ["agendaAction"] = agendaAction.Trim(),
                ["summary"] = summary.Trim(),
                ["expectedResponseSurface"] = SarefMainStoryState.ResponseField,
                ["gmAction"] = gmAction
            });
    }

    private static JsonObject BuildWingsInfiltrationPayload(JsonObject request)
    {
        var requestId = GetNodeString(request["requestId"]) ?? string.Empty;
        var routeSafety = GetNodeString(request["routeSafety"]) ?? string.Empty;
        var entryMode = GetNodeString(request["entryMode"]) ?? string.Empty;
        var gmAction =
            $"[SAREF_WINGS_INFILTRATION: {requestId}] Душа начинает поиск входа в Крылья Ангелов.\n\n" +
            $"Закрой {SarefMainStoryState.PendingWingsInfiltrationPath} через sarefMainStoryUpdate.mode={SarefMainStoryState.WingsUpdateModeReveal}, " +
            $"{SarefMainStoryState.WingsUpdateModeRefuse} или {SarefMainStoryState.WingsUpdateModeBlock}. " +
            $"Маршрут: {routeSafety}, вход: {entryMode}. " +
            "Если routeSafety=risky/desperate, обязательно примени перечисленные disadvantages. " +
            "При reveal_wings запиши main_story_saref_state.revealStage=wings_revealed, wingsInfiltration.status=revealed, resolvedAtTurn и factionLinks.visibility=revealed. " +
            "Не оставляй ожидающий файл без accepted closure/repair.";

        return new JsonObject
        {
            ["playerActionTag"] = "SAREF_WINGS_INFILTRATION",
            ["requestId"] = requestId,
            ["routeSafety"] = routeSafety,
            ["entryMode"] = entryMode,
            ["pendingPath"] = SarefMainStoryState.PendingWingsInfiltrationPath,
            ["expectedResponseSurface"] = SarefMainStoryState.ResponseField,
            ["gmAction"] = gmAction
        };
    }

    private async Task<JsonObject> ReadRequiredObjectAsync(string path, string error)
    {
        var raw = await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException(error);
        try
        {
            return JsonNode.Parse(raw) as JsonObject
                   ?? throw new InvalidOperationException(error);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"{path} повреждён: {ex.Message}");
        }
    }

    private static string ReadAnswer(IReadOnlyDictionary<string, JsonNode?> answers, string key)
    {
        if (!answers.TryGetValue(key, out var node) || node is not JsonValue value)
            return string.Empty;
        if (value.TryGetValue<string>(out var text))
            return text.Trim();
        return node?.ToString().Trim() ?? string.Empty;
    }

    private static string? GetNodeString(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var text))
            return text.Trim();
        return null;
    }

    private static string NormalizeCommand(string command) =>
        string.Join(' ', command.Trim().Replace('-', '_').Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}
