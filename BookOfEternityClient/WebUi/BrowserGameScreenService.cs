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
        if (!HasPlayableSession(lifecycle))
        {
            throw new BrowserNoActiveSessionException(
                "game_session пока не содержит активную главу. Начните новую главу или загрузите сохранение из главного меню.");
        }

        var qte = await _qte.BuildReadOnlyStateAsync();
        var narrative = await BuildNarrativeAsync(state);
        var media = await BuildMediaAsync(narrative);

        return new BrowserGameScreenDto(
            SchemaVersion: 2,
            Theme: BrowserGameScreenThemeDto.FromState(state),
            Soul: new BrowserGameScreenSoulDto(
                Name: state.SoulName,
                FormDescription: state.SoulFormDescription,
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

    private static bool HasPlayableSession(BrowserLifecycleDashboardDto lifecycle)
    {
        if (!lifecycle.Session.GameSessionExists)
            return false;

        if (lifecycle.Soul.IsReadable)
            return true;

        return !IsExpectedEmptySoulState(lifecycle.Soul.ReadError);
    }

    private static bool IsExpectedEmptySoulState(string readError) =>
        readError.Contains("отсутствует или пуст", StringComparison.OrdinalIgnoreCase);

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

                var inputValue = ReadString(item, "inputValue", "value", "command");
                var visibleText = DialogueOptionControlTagNormalizer.NormalizeVisibleText(text) ?? string.Empty;
                result.Add(new BrowserGameScreenDialogueOptionDto(
                    Id: ReadString(item, "id", "optionId") is { Length: > 0 } id ? id : $"choice-{index}",
                    Text: visibleText,
                    InputValue: DialogueOptionControlTagNormalizer.ResolveInputValue(text, inputValue) ?? string.Empty,
                    Category: ReadString(item, "category", "type", "kind")));
            }
            else if (node is JsonValue value)
            {
                try
                {
                    var text = value.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var visibleText = DialogueOptionControlTagNormalizer.NormalizeVisibleText(text) ?? string.Empty;
                        result.Add(new BrowserGameScreenDialogueOptionDto(
                            Id: $"choice-{index}",
                            Text: visibleText,
                            InputValue: DialogueOptionControlTagNormalizer.ResolveInputValue(text, existingInputValue: null) ?? string.Empty,
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

public sealed class BrowserNoActiveSessionException : InvalidOperationException
{
    public BrowserNoActiveSessionException(string message) : base(message)
    {
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
    string FormDescription,
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

public sealed record BrowserGameScreenDialogueOptionDto(string Id, string Text, string InputValue, string Category);

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
        new("ready", "Ответ мира готов", "Результат хода готов к показу игроку.", "player-default"),
        new("accepted", "Ответ ГМа принят", "Результат ответа ГМа уже принят в локальное состояние.", "player-default"),
        new("blocked", "Ход временно недоступен", "Мир пока не готов продолжить этот ход.", "player-default"),
        new("cancelled", "Ход отменён", "Ожидающий ход был отменён; можно выбрать другое действие.", "player-default")
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
                    state: "blocked",
                    title: "Мир временно остановился",
                    message: "Предыдущий ход не завершился безопасно, поэтому продолжение пока недоступно.",
                    canStartBrowserWrite: false,
                    lifecycle: lifecycle,
                    phase: "blocked",
                    severity: "error",
                    playerGuidance: "Вернитесь к последнему доступному состоянию или повторите попытку позже.",
                    actions: []);
            }

            if (ArtifactExists(lifecycle.PendingTurn, BrowserPendingTurnInspector.TurnErrorPath))
            {
                return Create(
                    state: "blocked",
                    title: "Мир не смог завершить ход",
                    message: "Результат предыдущего хода не был принят.",
                    canStartBrowserWrite: false,
                    lifecycle: lifecycle,
                    phase: "blocked",
                    severity: "error",
                    playerGuidance: "Вернитесь к последнему доступному состоянию или повторите действие позже.",
                    actions: []);
            }

            if (ArtifactExists(lifecycle.PendingTurn, BrowserPendingTurnInspector.TurnCompletePath))
            {
                return Create(
                    state: "ready-gm-response",
                    title: "Ответ мира готов",
                    message: "Мир завершил обработку хода и готов показать результат.",
                    canStartBrowserWrite: false,
                    lifecycle: lifecycle,
                    phase: "ready",
                    severity: "success",
                    playerGuidance: "Дождитесь появления результата, прежде чем начинать новое действие.",
                    actions: []);
            }

            return Create(
                state: "pending-gm-turn",
                title: "Ожидает ответ ГМа",
                message: "Отправленный ход ещё обрабатывается.",
                canStartBrowserWrite: false,
                lifecycle: lifecycle,
                phase: "waiting-gm",
                severity: "warning",
                playerGuidance: "Дождитесь ответа перед любыми новыми действиями.",
                actions:
                [
                    Action(
                        "wait-for-gm",
                        "Ждать ответ ГМа",
                        "Не меняйте локальное состояние, пока ожидающий ход не завершится.",
                        "player-default")
            ]);
        }

        if (qte.State == "Failed")
        {
            return Create(
                state: "blocked",
                title: "Быстрая сцена временно недоступна",
                message: "Эту сцену сейчас нельзя безопасно продолжить.",
                canStartBrowserWrite: false,
                lifecycle: lifecycle,
                phase: "blocked",
                severity: "error",
                playerGuidance: "Вернитесь к последнему доступному состоянию или повторите попытку позже.",
                actions: []);
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
            return Create(
                state: "blocked",
                title: "Действие временно недоступно",
                message: "Другая операция ещё не завершилась.",
                canStartBrowserWrite: false,
                lifecycle: lifecycle,
                phase: "turn-submitted",
                severity: "warning",
                playerGuidance: "Дождитесь завершения текущей операции, прежде чем начинать новую.",
                actions:
                [
                    Action(
                        "wait-local-write",
                        "Подождать",
                        "Повторите действие после завершения текущей операции.",
                        "player-default")
                ]);
        }

        if (lifecycle.Validation.ErrorCount > 0)
        {
            return Create(
                state: "blocked",
                title: "Мир временно остановился",
                message: "Текущее состояние мира не позволяет продолжить ход.",
                canStartBrowserWrite: false,
                lifecycle: lifecycle,
                phase: "blocked",
                severity: "error",
                playerGuidance: "Вернитесь к последнему доступному состоянию или повторите попытку позже.",
                actions: []);
        }

        return Create(
            state: "ready",
            title: "Можно продолжать",
            message: "Опишите следующее действие персонажа в прозе. После подтверждения ход будет подготовлен для ГМ.",
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
                    "Заполните основной художественный ввод и подтвердите действие, когда будете готовы передать ход ГМ.",
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

    internal static bool ArtifactExists(BrowserPendingTurnStatus pending, string path) =>
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
            // Distinguish repair/error states from genuine GM-waiting (issue #743)
            if (BrowserGameScreenTurnStateDto.ArtifactExists(lifecycle.PendingTurn, BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath) ||
                BrowserGameScreenTurnStateDto.ArtifactExists(lifecycle.PendingTurn, BrowserPendingTurnInspector.PendingTurnSnapshotDirectory) ||
                BrowserGameScreenTurnStateDto.ArtifactExists(lifecycle.PendingTurn, BrowserPendingTurnInspector.ExplorerRollbackDirectory))
            {
                return new BrowserGameScreenActionComposerDto(
                    CanSubmit: false,
                    Mode: "blocked",
                    Placeholder: "Продолжение хода временно недоступно...",
                    Guidance: "Вернитесь к последнему доступному состоянию или повторите попытку позже.",
                    DisabledReason: "Предыдущий ход не завершился безопасно.");
            }

            if (BrowserGameScreenTurnStateDto.ArtifactExists(lifecycle.PendingTurn, BrowserPendingTurnInspector.TurnErrorPath))
            {
                return new BrowserGameScreenActionComposerDto(
                    CanSubmit: false,
                    Mode: "blocked",
                    Placeholder: "Мир не смог завершить ход...",
                    Guidance: "Вернитесь к последнему доступному состоянию или повторите действие позже.",
                    DisabledReason: "Результат предыдущего хода не был принят.");
            }

            if (BrowserGameScreenTurnStateDto.ArtifactExists(lifecycle.PendingTurn, BrowserPendingTurnInspector.TurnCompletePath))
            {
                return new BrowserGameScreenActionComposerDto(
                    CanSubmit: false,
                    Mode: "blocked",
                    Placeholder: "Ответ мира готов...",
                    Guidance: "Дождитесь появления результата, прежде чем начинать новое действие.",
                    DisabledReason: "Результат хода ещё открывается.");
            }

            return new BrowserGameScreenActionComposerDto(
                CanSubmit: false,
                Mode: "waiting-for-gm",
                Placeholder: "ГМ обрабатывает ход...",
                Guidance: "Ход отправлен ГМу. Дождитесь ответа перед новыми действиями.",
                DisabledReason: "Отправленный ход ещё обрабатывается.");
        }

        if (qte.State == "Failed")
        {
            return new BrowserGameScreenActionComposerDto(
                CanSubmit: false,
                Mode: "blocked",
                Placeholder: "Быстрая сцена временно недоступна...",
                Guidance: "Вернитесь к последнему доступному состоянию или повторите попытку позже.",
                DisabledReason: "Эту сцену сейчас нельзя безопасно продолжить.");
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
                Mode: "blocked",
                Placeholder: "Продолжение хода временно недоступно...",
                Guidance: "Вернитесь к последнему доступному состоянию или повторите попытку позже.",
                DisabledReason: "Текущее состояние мира не позволяет продолжить ход.");
        }

        if (!lifecycle.CanStartBrowserWrite)
        {
            return new BrowserGameScreenActionComposerDto(
                CanSubmit: false,
                Mode: "blocked",
                Placeholder: "Локальная запись сейчас недоступна...",
                Guidance: "Дождитесь завершения текущей операции и повторите действие.",
                DisabledReason: "Другая операция ещё не завершилась.");
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
