using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models.GameState;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.WebUi;

public sealed class BrowserGameScreenService
{
    private readonly StateManager _stateManager;
    private readonly FileSystemManager _fs;
    private readonly BrowserLifecycleDashboardService _lifecycle;
    private readonly QteWebInteractionService _qte;
    private readonly LocalMediaService _media;

    public BrowserGameScreenService(
        StateManager stateManager,
        FileSystemManager fs,
        BrowserLifecycleDashboardService lifecycle,
        QteWebInteractionService qte,
        LocalMediaService media)
    {
        _stateManager = stateManager;
        _fs = fs;
        _lifecycle = lifecycle;
        _qte = qte;
        _media = media;
    }

    public async Task<BrowserGameScreenDto> BuildAsync()
    {
        await _stateManager.RefreshGameStateAsync();
        var state = _stateManager.CurrentState;
        var lifecycle = await _lifecycle.BuildDashboardAsync();
        var qte = await _qte.BuildReadOnlyStateAsync();
        var narrative = await BuildNarrativeAsync(state);
        var media = await BuildMediaAsync(narrative);

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
            Media: media,
            Afterlife: BrowserGameScreenAfterlifeDto.FromState(state),
            TurnState: BrowserGameScreenTurnStateDto.From(lifecycle, qte),
            ActionComposer: BrowserGameScreenActionComposerDto.From(lifecycle, qte),
            Qte: qte,
            ActionMenu: BrowserPlayerCommandMenuBuilder.Build(state, lifecycle, qte),
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
            CombatLog: ReadString(combatLog, "combat_log_markdown", "combatLogMarkdown", "combatLog"),
            ImagePrompt: ReadString(interfaceUpdates, "image_prompt", "imagePrompt"));
    }

    private async Task<BrowserGameScreenMediaDto> BuildMediaAsync(BrowserGameScreenNarrativeDto narrative)
    {
        var map = await LocalMapViewService.BuildCurrentRealmMapAsync(_fs);
        var gallery = _media.EnumerateGallery(24)
            .Select(static item => new BrowserGameScreenMediaItemDto(
                MediaId: item.MediaId,
                Url: item.Url,
                FileName: item.FileName,
                ContentType: item.ContentType,
                Length: item.Length,
                ModifiedAtUtc: item.ModifiedAtUtc))
            .ToList();

        return new BrowserGameScreenMediaDto(
            SchemaVersion: 1,
            SceneImagePrompt: narrative.ImagePrompt,
            Gallery: gallery,
            Map: map);
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
    BrowserGameScreenMediaDto Media,
    BrowserGameScreenAfterlifeDto Afterlife,
    BrowserGameScreenTurnStateDto TurnState,
    BrowserGameScreenActionComposerDto ActionComposer,
    QteWebStateDto Qte,
    BrowserPlayerCommandMenuDto ActionMenu,
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
    string CombatLog,
    string ImagePrompt);

public sealed record BrowserGameScreenDialogueOptionDto(string Id, string Text, string Category);

public sealed record BrowserGameScreenMediaDto(
    int SchemaVersion,
    string SceneImagePrompt,
    IReadOnlyList<BrowserGameScreenMediaItemDto> Gallery,
    MapViewDto Map);

public sealed record BrowserGameScreenMediaItemDto(
    string MediaId,
    string Url,
    string FileName,
    string ContentType,
    long Length,
    DateTimeOffset ModifiedAtUtc);

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
    string ValidationLabel,
    string Phase,
    string PhaseLabel,
    string Severity,
    string PlayerGuidance,
    IReadOnlyList<BrowserGameScreenTurnActionDto> RecommendedActions,
    IReadOnlyList<BrowserGameScreenTurnPhaseDto> KnownPhases)
{
    public static IReadOnlyList<BrowserGameScreenTurnPhaseDto> KnownPhaseCatalog { get; } =
    [
        new("idle", "Можно готовить ход", "Игра не ждёт ГМа; игрок может писать следующий художественный ход.", "player-default"),
        new("composing-action", "Игрок готовит действие", "Текст или быстрая сцена находятся в фазе подготовки до безопасной записи.", "player-default"),
        new("turn-submitted", "Ход отправляется", "Локальная запись уже начата; повторные действия заблокированы.", "player-default"),
        new("waiting-gm", "Ожидаем ответ ГМа", "Ход отправлен, и нужно дождаться результата ГМа.", "player-default"),
        new("ready", "Ответ ГМа готов", "Ответ ГМа готов к принятию через обычный turn lifecycle.", "player-default"),
        new("accepted", "Ответ ГМа принят", "Результат ответа ГМа уже принят в локальное состояние.", "player-default"),
        new("validation-failed", "Проверка не прошла", "Состояние требует ремонта перед продолжением.", "player-default"),
        new("repair-required", "Нужен ремонт", "Snapshot/rollback artifacts требуют repair перед новыми действиями.", "player-default"),
        new("error-restored", "Ошибка восстановлена", "GM turn завершился ошибкой; rollback/repair должен быть разобран.", "player-default"),
        new("cancelled", "Ход отменён", "Ожидающий ход был отменён или очищен безопасным lifecycle-действием.", "player-default")
    ];

    public static BrowserGameScreenTurnStateDto From(BrowserLifecycleDashboardDto lifecycle, QteWebStateDto qte)
    {
        if (lifecycle.PendingTurn.HasActiveGmTurn)
        {
            if (ArtifactExists(lifecycle.PendingTurn, BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath) ||
                ArtifactExists(lifecycle.PendingTurn, BrowserPendingTurnInspector.PendingTurnSnapshotDirectory) ||
                ArtifactExists(lifecycle.PendingTurn, BrowserPendingTurnInspector.ExplorerRollbackDirectory))
            {
                return Create(
                    state: "pending-turn-repair",
                    title: "Нужен repair pending turn",
                    message: lifecycle.Guidance.FirstOrDefault()?.Message ?? lifecycle.PendingTurn.Message,
                    canStartBrowserWrite: false,
                    lifecycle: lifecycle,
                    phase: "repair-required",
                    severity: "error",
                    playerGuidance: "Ожидающий ход оставил snapshot/rollback следы. Сначала завершите ремонт, затем возвращайтесь к игре.",
                    actions:
                    [
                        Action(
                            "open-repair-guidance",
                            "Открыть подсказки ремонта",
                            "Подробные repair-операции и rollback-детали доступны в расширенном режиме.",
                            "advanced-only")
                    ]);
            }

            if (ArtifactExists(lifecycle.PendingTurn, BrowserPendingTurnInspector.TurnErrorPath))
            {
                return Create(
                    state: "gm-turn-error",
                    title: "Ход GM завершился ошибкой",
                    message: lifecycle.Guidance.FirstOrDefault()?.Message ?? lifecycle.PendingTurn.Message,
                    canStartBrowserWrite: false,
                    lifecycle: lifecycle,
                    phase: "error-restored",
                    severity: "error",
                    playerGuidance: "GM turn завершился ошибкой. Откройте repair/rollback в расширенном режиме, прежде чем продолжать игру.",
                    actions:
                    [
                        Action(
                            "open-advanced-repair",
                            "Открыть repair в расширенном режиме",
                            "Используйте техническую панель, чтобы разобрать ошибку GM turn и восстановление состояния.",
                            "advanced-only")
                    ]);
            }

            if (ArtifactExists(lifecycle.PendingTurn, BrowserPendingTurnInspector.TurnCompletePath))
            {
                return Create(
                    state: "ready-gm-response",
                    title: "Ответ GM готов к принятию",
                    message: lifecycle.Guidance.FirstOrDefault()?.Message ?? lifecycle.PendingTurn.Message,
                    canStartBrowserWrite: false,
                    lifecycle: lifecycle,
                    phase: "ready",
                    severity: "success",
                    playerGuidance: "Ответ ГМа готов: нужно принять его через обычную обработку хода, прежде чем начинать новый ввод.",
                    actions:
                    [
                        Action(
                            "accept-gm-response",
                            "Принять ответ ГМа",
                            "Принятие ответа остаётся в безопасном lifecycle/advanced flow, пока player-default кнопка не реализована.",
                            "advanced-only")
                    ]);
            }

            return Create(
                state: "pending-gm-turn",
                title: "Ожидает ответ GM",
                message: lifecycle.PendingTurn.Message,
                canStartBrowserWrite: false,
                lifecycle: lifecycle,
                phase: "waiting-gm",
                severity: "warning",
                playerGuidance: "Ход уже отправлен ГМу. Дождитесь ответа, отмены или repair перед любыми локальными действиями.",
                actions:
                [
                    Action(
                        "wait-for-gm",
                        "Ждать ответ ГМа",
                        "Не меняйте локальное состояние, пока ожидающий ход не завершится безопасно.",
                        "player-default")
                ]);
        }

        if (qte.State is "Offer" or "Active")
        {
            return Create(
                state: "qte",
                title: qte.State == "Offer" ? "Доступна QTE-сцена" : "QTE-сцена активна",
                message: qte.Notification ?? qte.Offer?.OfferText ?? qte.ActiveScene?.Title ?? "Продолжите QTE в игровом экране.",
                canStartBrowserWrite: false,
                lifecycle: lifecycle,
                phase: "composing-action",
                severity: "warning",
                playerGuidance: "Сначала выберите действие быстрой сцены; обычный художественный ввод временно закрыт.",
                actions:
                [
                    Action(
                        "resolve-qte",
                        "Выбрать действие быстрой сцены",
                        "Откройте блок быстрой сцены и подтвердите один из доступных вариантов.",
                        "player-default")
                ]);
        }

        if (!lifecycle.CanStartBrowserWrite)
        {
            var blockedMessage = lifecycle.LocalUiLock is { Exists: true, IsStale: false }
                ? $"Локальная запись уже удерживается: {lifecycle.LocalUiLock.OwnerLabel}. Дождитесь завершения операции или истечения lease."
                : lifecycle.PendingTurn.Message;
            return Create(
                state: "blocked",
                title: "Локальная запись заблокирована",
                message: blockedMessage,
                canStartBrowserWrite: false,
                lifecycle: lifecycle,
                phase: "turn-submitted",
                severity: "warning",
                playerGuidance: "Локальная запись уже идёт или защищена блокировкой. Не запускайте второй ход до завершения текущей операции.",
                actions:
                [
                    Action(
                        "wait-local-write",
                        "Дождаться локальной записи",
                        "Повторите действие после освобождения локальной UI-блокировки.",
                        "player-default")
                ]);
        }

        if (lifecycle.Validation.ErrorCount > 0)
        {
            return Create(
                state: "validation-errors",
                title: "Нужен ремонт состояния",
                message: lifecycle.Validation.StatusLabel,
                canStartBrowserWrite: false,
                lifecycle: lifecycle,
                phase: "validation-failed",
                severity: "error",
                playerGuidance: "Проверка нашла ошибки состояния. Подробности и repair-действия доступны в расширенном режиме.",
                actions:
                [
                    Action(
                        "review-validation",
                        "Проверить состояние",
                        "Откройте расширенный режим, чтобы увидеть группы ошибок и выполнить ремонт.",
                        "advanced-only")
                ]);
        }

        return Create(
            state: "ready",
            title: "Можно продолжать",
            message: "Опишите следующее действие персонажа в прозе. Браузерный turn-writer будет подключён отдельным безопасным шагом.",
            canStartBrowserWrite: true,
            lifecycle: lifecycle,
            phase: "idle",
            severity: "success",
            playerGuidance: "Игра не ждёт ГМа; можно подготовить следующий художественный ход.",
            actions:
            [
                Action(
                    "compose-action",
                    "Подготовить действие",
                    "Заполните основной художественный ввод и подтвердите действие, когда запись хода будет подключена.",
                    "player-default")
            ]);
    }

    private static BrowserGameScreenTurnStateDto Create(
        string state,
        string title,
        string message,
        bool canStartBrowserWrite,
        BrowserLifecycleDashboardDto lifecycle,
        string phase,
        string severity,
        string playerGuidance,
        IReadOnlyList<BrowserGameScreenTurnActionDto> actions) =>
        new(
            State: state,
            Title: title,
            Message: message,
            CanStartBrowserWrite: canStartBrowserWrite,
            ValidationState: lifecycle.Validation.State,
            ValidationLabel: lifecycle.Validation.StatusLabel,
            Phase: phase,
            PhaseLabel: ToPhaseLabel(phase),
            Severity: severity,
            PlayerGuidance: playerGuidance,
            RecommendedActions: actions,
            KnownPhases: KnownPhaseCatalog);

    private static BrowserGameScreenTurnActionDto Action(
        string id,
        string label,
        string description,
        string surface,
        bool enabled = true,
        string disabledReason = "") =>
        new(id, label, description, surface, enabled, disabledReason);

    private static string ToPhaseLabel(string phase) =>
        KnownPhaseCatalog.FirstOrDefault(item => string.Equals(item.Id, phase, StringComparison.OrdinalIgnoreCase))?.Label
        ?? "Состояние хода";

    private static bool ArtifactExists(BrowserPendingTurnStatus pending, string path) =>
        pending.Artifacts.Any(artifact =>
            artifact.Exists &&
            string.Equals(artifact.Path, path, StringComparison.OrdinalIgnoreCase));
}

public sealed record BrowserGameScreenTurnActionDto(
    string Id,
    string Label,
    string Description,
    string Surface,
    bool Enabled,
    string DisabledReason);

public sealed record BrowserGameScreenTurnPhaseDto(
    string Id,
    string Label,
    string Description,
    string Surface);

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
