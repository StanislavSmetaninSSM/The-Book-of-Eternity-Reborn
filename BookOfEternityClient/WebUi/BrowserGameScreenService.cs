using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models.GameState;

namespace BookOfEternityClient.WebUi;

public sealed class BrowserGameScreenService
{
    private readonly StateManager _stateManager;
    private readonly FileSystemManager _fs;
    private readonly BrowserLifecycleDashboardService _lifecycle;
    private readonly QteWebInteractionService _qte;

    public BrowserGameScreenService(
        StateManager stateManager,
        FileSystemManager fs,
        BrowserLifecycleDashboardService lifecycle,
        QteWebInteractionService qte)
    {
        _stateManager = stateManager;
        _fs = fs;
        _lifecycle = lifecycle;
        _qte = qte;
    }

    public async Task<BrowserGameScreenDto> BuildAsync()
    {
        await _stateManager.RefreshGameStateAsync();
        var state = _stateManager.CurrentState;
        var lifecycle = await _lifecycle.BuildDashboardAsync();
        var qte = await _qte.BuildReadOnlyStateAsync();
        var narrative = await BuildNarrativeAsync(state);

        return new BrowserGameScreenDto(
            SchemaVersion: 2,
            Theme: BrowserGameScreenThemeDto.FromState(state),
            Soul: new BrowserGameScreenSoulDto(
                Name: state.SoulName,
                Realm: state.CurrentRealm,
                Incarnation: state.Incarnation,
                InkFeathers: state.InkFeathers,
                EnlightenmentTier: state.EnlightenmentTier,
                ActiveGuardianName: state.ActiveGuardianName),
            Player: new BrowserGameScreenPlayerDto(
                Name: state.CharacterName,
                Class: state.CharacterClass,
                Race: state.CharacterRace,
                CurrentCondition: state.PlayerStatus.CurrentCondition,
                HealthPercentage: state.PlayerStatus.HealthPercentage,
                EnergyPercentage: state.PlayerStatus.EnergyPercentage,
                PoisePercentage: state.PlayerStatus.PoisePercentage,
                ActiveConditions: state.PlayerStatus.ActiveConditions),
            World: new BrowserGameScreenWorldDto(
                Location: state.CurrentLocation,
                WorldTime: state.WorldTime,
                TurnNumber: state.TurnNumber,
                SessionId: state.SessionId),
            Narrative: narrative,
            Afterlife: BrowserGameScreenAfterlifeDto.FromState(state),
            TurnState: BrowserGameScreenTurnStateDto.From(lifecycle, qte),
            ActionComposer: BrowserGameScreenActionComposerDto.From(lifecycle, qte),
            Qte: qte,
            Flags: new BrowserGameScreenFlagsDto(
                IsInChaosSea: state.IsInChaosSea,
                IsInAnyShiningAbodeState: state.IsInAnyShiningAbodeState,
                IsInShiningAbode: state.IsInShiningAbode,
                IsInShiningAbodePendingBootstrap: state.IsInShiningAbodePendingBootstrap,
                IsInAfterlifeRealm: state.IsInAfterlifeRealm,
                CanReenterShiningAbode: state.CanReenterShiningAbode));
    }

    private async Task<BrowserGameScreenNarrativeDto> BuildNarrativeAsync(AggregatedGameState state)
    {
        var interfaceUpdates = await ReadJsonObjectAsync("output/interface_updates.json");
        var combatLog = await ReadJsonObjectAsync("game_state/combat/combat_log.json");

        return new BrowserGameScreenNarrativeDto(
            Text: state.Narrative,
            DialogueOptions: ReadDialogueOptions(interfaceUpdates),
            GmThoughts: string.Empty,
            CombatLog: ReadString(combatLog, "combat_log_markdown", "combatLogMarkdown", "combatLog"),
            ImagePrompt: ReadString(interfaceUpdates, "image_prompt", "imagePrompt"));
    }

    private async Task<JsonObject?> ReadJsonObjectAsync(string relativePath)
    {
        var raw = await _fs.ReadFileAsync(relativePath);
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

    private static IReadOnlyList<BrowserGameScreenDialogueOptionDto> ReadDialogueOptions(JsonObject? root)
    {
        var options = FindArray(root, "dialogueOptions", "dialogue_options", "choices", "availableChoices");
        if (options == null || options.Count == 0)
            return [];

        var result = new List<BrowserGameScreenDialogueOptionDto>();
        var index = 1;
        foreach (var node in options)
        {
            if (node is JsonObject item)
            {
                var text = ReadString(item, "text", "label", "title", "option");
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                result.Add(new BrowserGameScreenDialogueOptionDto(
                    Id: ReadString(item, "id", "optionId") is { Length: > 0 } id ? id : $"choice-{index}",
                    Text: text,
                    Category: ReadString(item, "category", "type", "kind")));
            }
            else if (node is JsonValue value)
            {
                try
                {
                    var text = value.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        result.Add(new BrowserGameScreenDialogueOptionDto(
                            Id: $"choice-{index}",
                            Text: text,
                            Category: string.Empty));
                    }
                }
                catch (InvalidOperationException)
                {
                }
            }

            index++;
        }

        return result;
    }

    private static JsonArray? FindArray(JsonObject? root, params string[] names)
    {
        if (root == null)
            return null;

        foreach (var name in names)
        {
            if (root.TryGetPropertyValue(name, out var node) && node is JsonArray array)
                return array;
        }

        return null;
    }

    private static string ReadString(JsonObject? root, params string[] names)
    {
        if (root == null)
            return string.Empty;

        foreach (var name in names)
        {
            if (root.TryGetPropertyValue(name, out var node) && node is JsonValue value)
            {
                try
                {
                    return value.GetValue<string>() ?? string.Empty;
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        return string.Empty;
    }
}

public sealed record BrowserGameScreenDto(
    int SchemaVersion,
    BrowserGameScreenThemeDto Theme,
    BrowserGameScreenSoulDto Soul,
    BrowserGameScreenPlayerDto Player,
    BrowserGameScreenWorldDto World,
    BrowserGameScreenNarrativeDto Narrative,
    BrowserGameScreenAfterlifeDto Afterlife,
    BrowserGameScreenTurnStateDto TurnState,
    BrowserGameScreenActionComposerDto ActionComposer,
    QteWebStateDto Qte,
    BrowserGameScreenFlagsDto Flags);

public sealed record BrowserGameScreenThemeDto(string Key, string Label, string Icon, string Accent)
{
    public static BrowserGameScreenThemeDto FromState(AggregatedGameState state)
    {
        if (state.IsInShiningAbodePendingBootstrap)
            return new BrowserGameScreenThemeDto("shining-handoff", "Переход из Сияющей Обители", "🚪", "#d8c7ff");
        if (state.IsInShiningAbode)
            return new BrowserGameScreenThemeDto("shining-abode", "Сияющая Обитель", "✨", "#f5d976");
        if (state.IsInChaosSea)
            return new BrowserGameScreenThemeDto("chaos-sea", "Море Хаоса", "🌊", "#78d2c9");

        return new BrowserGameScreenThemeDto("mortal-world", "Смертный мир", "🕯", "#e1b85e");
    }
}

public sealed record BrowserGameScreenSoulDto(
    string Name,
    string Realm,
    int Incarnation,
    int InkFeathers,
    string EnlightenmentTier,
    string ActiveGuardianName);

public sealed record BrowserGameScreenPlayerDto(
    string Name,
    string Class,
    string Race,
    string CurrentCondition,
    string HealthPercentage,
    string EnergyPercentage,
    string PoisePercentage,
    IReadOnlyList<string> ActiveConditions);

public sealed record BrowserGameScreenWorldDto(
    string Location,
    string WorldTime,
    int TurnNumber,
    string SessionId);

public sealed record BrowserGameScreenNarrativeDto(
    string Text,
    IReadOnlyList<BrowserGameScreenDialogueOptionDto> DialogueOptions,
    string GmThoughts,
    string CombatLog,
    string ImagePrompt);

public sealed record BrowserGameScreenDialogueOptionDto(string Id, string Text, string Category);

public sealed record BrowserGameScreenAfterlifeDto(
    int ShiningRadianceExperience,
    int ShiningRadianceTier,
    int ShiningLightSparks,
    int ShiningHallCount,
    int ShiningFactionCount,
    bool HasOpenShiningGatesDraft,
    bool IsShiningGatesDraftStale)
{
    public static BrowserGameScreenAfterlifeDto FromState(AggregatedGameState state) =>
        new(
            state.ShiningRadianceExperience,
            state.ShiningRadianceTier,
            state.ShiningLightSparks,
            state.ShiningHallCount,
            state.ShiningFactionCount,
            state.HasOpenShiningGatesDraft,
            state.IsShiningGatesDraftStale);
}

public sealed record BrowserGameScreenTurnStateDto(
    string State,
    string Title,
    string Message,
    bool CanStartBrowserWrite,
    string ValidationState,
    string ValidationLabel)
{
    public static BrowserGameScreenTurnStateDto From(BrowserLifecycleDashboardDto lifecycle, QteWebStateDto qte)
    {
        if (lifecycle.PendingTurn.HasActiveGmTurn)
        {
            if (ArtifactExists(lifecycle.PendingTurn, BrowserPendingTurnInspector.TurnErrorPath))
            {
                return new BrowserGameScreenTurnStateDto(
                    State: "gm-turn-error",
                    Title: "Ход GM завершился ошибкой",
                    Message: lifecycle.Guidance.FirstOrDefault()?.Message ?? lifecycle.PendingTurn.Message,
                    CanStartBrowserWrite: false,
                    ValidationState: lifecycle.Validation.State,
                    ValidationLabel: lifecycle.Validation.StatusLabel);
            }

            if (ArtifactExists(lifecycle.PendingTurn, BrowserPendingTurnInspector.TurnCompletePath))
            {
                return new BrowserGameScreenTurnStateDto(
                    State: "ready-gm-response",
                    Title: "Ответ GM готов к принятию",
                    Message: lifecycle.Guidance.FirstOrDefault()?.Message ?? lifecycle.PendingTurn.Message,
                    CanStartBrowserWrite: false,
                    ValidationState: lifecycle.Validation.State,
                    ValidationLabel: lifecycle.Validation.StatusLabel);
            }

            if (ArtifactExists(lifecycle.PendingTurn, BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath) ||
                ArtifactExists(lifecycle.PendingTurn, BrowserPendingTurnInspector.PendingTurnSnapshotDirectory) ||
                ArtifactExists(lifecycle.PendingTurn, BrowserPendingTurnInspector.ExplorerRollbackDirectory))
            {
                return new BrowserGameScreenTurnStateDto(
                    State: "pending-turn-repair",
                    Title: "Нужен repair pending turn",
                    Message: lifecycle.Guidance.FirstOrDefault()?.Message ?? lifecycle.PendingTurn.Message,
                    CanStartBrowserWrite: false,
                    ValidationState: lifecycle.Validation.State,
                    ValidationLabel: lifecycle.Validation.StatusLabel);
            }

            return new BrowserGameScreenTurnStateDto(
                State: "pending-gm-turn",
                Title: "Ожидает ответ GM",
                Message: lifecycle.PendingTurn.Message,
                CanStartBrowserWrite: false,
                ValidationState: lifecycle.Validation.State,
                ValidationLabel: lifecycle.Validation.StatusLabel);
        }

        if (qte.State is "Offer" or "Active")
        {
            return new BrowserGameScreenTurnStateDto(
                State: "qte",
                Title: qte.State == "Offer" ? "Доступна QTE-сцена" : "QTE-сцена активна",
                Message: qte.Notification ?? qte.Offer?.OfferText ?? qte.ActiveScene?.Title ?? "Продолжите QTE в игровом экране.",
                CanStartBrowserWrite: false,
                ValidationState: lifecycle.Validation.State,
                ValidationLabel: lifecycle.Validation.StatusLabel);
        }

        if (lifecycle.Validation.ErrorCount > 0)
        {
            return new BrowserGameScreenTurnStateDto(
                State: "validation-errors",
                Title: "Нужен ремонт состояния",
                Message: lifecycle.Validation.StatusLabel,
                CanStartBrowserWrite: false,
                ValidationState: lifecycle.Validation.State,
                ValidationLabel: lifecycle.Validation.StatusLabel);
        }

        return new BrowserGameScreenTurnStateDto(
            State: lifecycle.CanStartBrowserWrite ? "ready" : "blocked",
            Title: lifecycle.CanStartBrowserWrite ? "Можно продолжать" : "Локальная запись заблокирована",
            Message: lifecycle.CanStartBrowserWrite
                ? "Опишите следующее действие персонажа в прозе. Браузерный turn-writer будет подключён отдельным безопасным шагом."
                : lifecycle.PendingTurn.Message,
            CanStartBrowserWrite: lifecycle.CanStartBrowserWrite,
            ValidationState: lifecycle.Validation.State,
            ValidationLabel: lifecycle.Validation.StatusLabel);
    }

    private static bool ArtifactExists(BrowserPendingTurnStatus pending, string path) =>
        pending.Artifacts.Any(artifact =>
            artifact.Exists &&
            string.Equals(artifact.Path, path, StringComparison.OrdinalIgnoreCase));
}

public sealed record BrowserGameScreenActionComposerDto(
    bool CanSubmit,
    string Mode,
    string Placeholder,
    string Guidance,
    string DisabledReason)
{
    public static BrowserGameScreenActionComposerDto From(BrowserLifecycleDashboardDto lifecycle, QteWebStateDto qte)
    {
        if (lifecycle.PendingTurn.HasActiveGmTurn)
        {
            return new BrowserGameScreenActionComposerDto(
                CanSubmit: false,
                Mode: "waiting-for-gm",
                Placeholder: "GM уже обрабатывает ход...",
                Guidance: "Дождитесь ответа GM или откройте расширенный режим для ремонта.",
                DisabledReason: lifecycle.PendingTurn.Message);
        }

        if (qte.State is "Offer" or "Active")
        {
            return new BrowserGameScreenActionComposerDto(
                CanSubmit: false,
                Mode: "qte",
                Placeholder: "Сначала завершите QTE-сцену...",
                Guidance: "Выберите QTE-действие на игровом экране.",
                DisabledReason: qte.Notification ?? "Активна QTE-сцена.");
        }

        if (lifecycle.Validation.ErrorCount > 0)
        {
            return new BrowserGameScreenActionComposerDto(
                CanSubmit: false,
                Mode: "repair-required",
                Placeholder: "Сначала нужен repair состояния...",
                Guidance: "Исправьте ошибки валидации перед новым художественным вводом.",
                DisabledReason: lifecycle.Validation.StatusLabel);
        }

        if (!lifecycle.CanStartBrowserWrite)
        {
            return new BrowserGameScreenActionComposerDto(
                CanSubmit: false,
                Mode: "blocked",
                Placeholder: "Локальная запись сейчас недоступна...",
                Guidance: "Проверьте панель состояния или расширенный режим.",
                DisabledReason: lifecycle.Guidance.FirstOrDefault()?.Message ?? "Локальная запись заблокирована.");
        }

        return new BrowserGameScreenActionComposerDto(
            CanSubmit: true,
            Mode: "prose",
            Placeholder: "Опишите действие персонажа: что он делает, говорит или исследует...",
            Guidance: "Обычная проза — основной игровой ввод. Slash-команды открываются только через расширенный режим.",
            DisabledReason: string.Empty);
    }
}

public sealed record BrowserGameScreenFlagsDto(
    bool IsInChaosSea,
    bool IsInAnyShiningAbodeState,
    bool IsInShiningAbode,
    bool IsInShiningAbodePendingBootstrap,
    bool IsInAfterlifeRealm,
    bool CanReenterShiningAbode);
