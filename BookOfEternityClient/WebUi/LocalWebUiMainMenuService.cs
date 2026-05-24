using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.WebUi;

public sealed class LocalWebUiMainMenuService
{
    private readonly FileSystemManager _fs;
    private readonly BrowserLifecycleDashboardService _lifecycle;
    private readonly SaveLoadService _saveLoad;
    private readonly StateManager _stateManager;
    private readonly BrowserLocalWriteCoordinator _writeCoordinator;

    public LocalWebUiMainMenuService(
        FileSystemManager fs,
        BrowserLifecycleDashboardService lifecycle,
        SaveLoadService saveLoad,
        StateManager stateManager,
        BrowserLocalWriteCoordinator writeCoordinator)
    {
        _fs = fs;
        _lifecycle = lifecycle;
        _saveLoad = saveLoad;
        _stateManager = stateManager;
        _writeCoordinator = writeCoordinator;
    }

    public async Task<BrowserMainMenuDto> BuildAsync()
    {
        var dashboard = await _lifecycle.BuildDashboardAsync();
        var terminalSoulDissipationMessage = await TryReadTerminalSoulDissipationMessageAsync();
        var saves = await BuildSaveSlotsAsync();
        var session = await BuildSessionSummaryAsync(dashboard, terminalSoulDissipationMessage);
        var actions = BuildActions(session, saves, dashboard);

        return new BrowserMainMenuDto(
            SchemaVersion: 1,
            Session: session,
            Actions: actions,
            Saves: saves,
            Options: BuildOptionsSummary(),
            About: BuildAboutSummary(),
            AdvancedShell: new BrowserAdvancedShellDto(
                Label: "Расширенный режим",
                Description: "Командная палитра, API-подсказки и debug-инструменты скрыты от обычного главного меню, но остаются доступны для проверки и перенесённых команд.",
                InitiallyExpanded: false));
    }

    public async Task<BrowserLoadSaveResultDto> LoadSaveAsync(BrowserLoadSaveRequest request)
    {
        var saveId = request.SaveId?.Trim() ?? string.Empty;
        var saves = await BuildSaveSlotsWithPathsAsync();
        var match = saves.FirstOrDefault(save => string.Equals(save.Dto.SaveId, saveId, StringComparison.Ordinal));
        if (match is null || string.IsNullOrWhiteSpace(match.FullPath))
        {
            return new BrowserLoadSaveResultDto(
                Success: false,
                Error: "Сохранение не найдено в текущем списке браузерного меню.",
                LoadedSaveId: saveId,
                Menu: await BuildAsync());
        }

        var writeStatus = await _writeCoordinator.BuildStatusAsync();
        if (!writeStatus.CanStartBrowserWrite)
        {
            return new BrowserLoadSaveResultDto(
                Success: false,
                Error: BuildBrowserWriteBlockedMessage(writeStatus),
                LoadedSaveId: saveId,
                Menu: await BuildAsync());
        }

        var loaded = false;
        var writeResult = await _writeCoordinator.ExecuteAsync(
            new BrowserLocalWriteRequest(
                OwnerId: "browser-main-menu-load",
                OwnerLabel: "Browser main menu",
                OperationLabel: "browser save load"),
            Array.Empty<string>(),
            async () =>
            {
                loaded = await _saveLoad.LoadGameAsync(match.FullPath);
                if (!loaded)
                    throw new InvalidOperationException("Не удалось загрузить выбранное сохранение.");
            });

        var success = writeResult.Success && loaded;
        return new BrowserLoadSaveResultDto(
            Success: success,
            Error: success ? string.Empty : writeResult.Message,
            LoadedSaveId: saveId,
            Menu: await BuildAsync());
    }

    private async Task<BrowserMainMenuSessionDto> BuildSessionSummaryAsync(
        BrowserLifecycleDashboardDto dashboard,
        string? terminalSoulDissipationMessage)
    {
        var turnNumber = await DetectCurrentSessionTurnNumberAsync();
        var hasReadableSoul = dashboard.Soul.IsReadable;
        var hasCurrentSession = dashboard.Session.GameSessionExists && hasReadableSoul;
        var terminalSoulDissipated = !string.IsNullOrWhiteSpace(terminalSoulDissipationMessage);
        var canContinue = hasCurrentSession && !terminalSoulDissipated;
        var continueReason = canContinue
            ? BuildContinueReadyMessage(dashboard)
            : BuildContinueBlocker(dashboard, terminalSoulDissipationMessage);

        return new BrowserMainMenuSessionDto(
            GameSessionExists: dashboard.Session.GameSessionExists,
            HasReadableSoul: hasReadableSoul,
            CanContinue: canContinue,
            ContinueReason: continueReason,
            SoulName: hasReadableSoul ? dashboard.Soul.Name : "Нет активной души",
            CurrentRealm: dashboard.Soul.CurrentRealm,
            RealmLabel: dashboard.Soul.RealmLabel,
            CurrentIncarnation: dashboard.Soul.CurrentIncarnation,
            TurnNumber: turnNumber,
            TurnLabel: $"Ход {turnNumber}",
            TerminalSoulDissipated: terminalSoulDissipated,
            ValidationState: dashboard.Validation.State,
            ValidationLabel: dashboard.Validation.StatusLabel,
            PendingTurnMessage: dashboard.PendingTurn.Message,
            CanStartBrowserWrite: dashboard.CanStartBrowserWrite,
            LocalUiLocked: dashboard.LocalUiLock.Exists,
            CheckedAtUtc: dashboard.Session.CheckedAtUtc);
    }

    private static string BuildContinueReadyMessage(BrowserLifecycleDashboardDto dashboard)
    {
        if (dashboard.Validation.ErrorCount > 0)
            return $"Сессия читается, но валидация обнаружила ошибки: {dashboard.Validation.ErrorCount}. Перед длинной игрой откройте repair/validation из панели состояния.";

        if (dashboard.Validation.WarningCount > 0)
            return $"Текущую сессию можно продолжить, но есть предупреждения валидации: {dashboard.Validation.WarningCount}.";

        return "Текущую сессию можно продолжить в браузерном игровом экране.";
    }

    private static string BuildContinueBlocker(
        BrowserLifecycleDashboardDto dashboard,
        string? terminalSoulDissipationMessage)
    {
        if (!string.IsNullOrWhiteSpace(terminalSoulDissipationMessage))
            return $"{terminalSoulDissipationMessage} Текущую сессию продолжить нельзя; загрузите сохранение через меню загрузки.";

        if (!dashboard.Session.GameSessionExists)
            return "Папка game_session ещё не создана. Начните новую игру или загрузите сохранение.";

        if (!dashboard.Soul.IsReadable)
            return string.IsNullOrWhiteSpace(dashboard.Soul.ReadError)
                ? "Не удалось прочитать текущую душу. Проверьте файлы сессии или загрузите сохранение."
                : dashboard.Soul.ReadError;

        return "Продолжение сейчас недоступно. Проверьте состояние локальной сессии.";
    }

    private static string BuildBrowserWriteBlockedMessage(BrowserLocalWriteStatus status)
    {
        if (status.PendingTurn.HasActiveGmTurn)
            return $"Загрузка сохранения заблокирована: {status.PendingTurn.Message}";

        if (status.LocalUiLock.Exists && !status.LocalUiLock.IsStale)
        {
            var owner = string.IsNullOrWhiteSpace(status.LocalUiLock.OwnerLabel)
                ? status.LocalUiLock.OwnerId
                : status.LocalUiLock.OwnerLabel;
            return $"Загрузка сохранения заблокирована свежей локальной UI-блокировкой: {owner}. Дождитесь завершения операции или освободите блокировку.";
        }

        return "Загрузка сохранения заблокирована текущим состоянием локальной сессии.";
    }

    private static IReadOnlyList<BrowserMainMenuActionDto> BuildActions(
        BrowserMainMenuSessionDto session,
        IReadOnlyList<BrowserSaveSlotDto> saves,
        BrowserLifecycleDashboardDto dashboard)
    {
        var newGameEnabled = dashboard.CanStartBrowserWrite;
        var newGameReason = newGameEnabled
            ? string.Empty
            : "Новая игра через браузерную форму недоступна, пока активен ход ГМа или локальная UI-блокировка.";
        var hasSaves = saves.Count > 0;
        var loadEnabled = hasSaves && dashboard.CanStartBrowserWrite;
        var loadDisabledReason = hasSaves
            ? BuildBrowserWriteBlockedMessage(new BrowserLocalWriteStatus(
                CanStartBrowserWrite: dashboard.CanStartBrowserWrite,
                PendingTurn: dashboard.PendingTurn,
                LocalUiLock: dashboard.LocalUiLock,
                CheckedAtUtc: dashboard.Session.CheckedAtUtc))
            : "Нет доступных сохранений для загрузки.";

        return new[]
        {
            new BrowserMainMenuActionDto(
                Id: "continue",
                Label: "Продолжить",
                Description: session.CanContinue
                    ? $"{session.SoulName} • {session.RealmLabel} • {session.TurnLabel}"
                    : session.ContinueReason,
                Enabled: session.CanContinue,
                DisabledReason: session.CanContinue ? string.Empty : session.ContinueReason,
                Kind: "client-panel",
                Command: string.Empty,
                TargetPanel: "game-shell"),
            new BrowserMainMenuActionDto(
                Id: "new-game",
                Label: "Новая игра",
                Description: "Открыть браузерную форму подготовки новой жизни/мира через тот же локальный write-flow.",
                Enabled: newGameEnabled,
                DisabledReason: newGameReason,
                Kind: "command",
                Command: "/world_setup",
                TargetPanel: "game-shell"),
            new BrowserMainMenuActionDto(
                Id: "load",
                Label: "Загрузить",
                Description: hasSaves
                    ? (loadEnabled ? "Выберите сохранение из браузерного списка." : loadDisabledReason)
                    : "В manual_saves и autosaves пока нет сохранений.",
                Enabled: loadEnabled,
                DisabledReason: loadEnabled ? string.Empty : loadDisabledReason,
                Kind: "panel",
                Command: string.Empty,
                TargetPanel: "load-panel"),
            new BrowserMainMenuActionDto(
                Id: "options",
                Label: "Настройки",
                Description: "Показать локальные настройки клиента и подсказку по настройкам консоли.",
                Enabled: true,
                DisabledReason: string.Empty,
                Kind: "panel",
                Command: string.Empty,
                TargetPanel: "options-panel"),
            new BrowserMainMenuActionDto(
                Id: "about",
                Label: "О мире / клиенте",
                Description: "Кратко описать Reborn и границы локального браузерного клиента.",
                Enabled: true,
                DisabledReason: string.Empty,
                Kind: "panel",
                Command: string.Empty,
                TargetPanel: "about-panel"),
            new BrowserMainMenuActionDto(
                Id: "exit",
                Label: "Выход",
                Description: "Закрытие вкладки или остановка локального процесса завершает браузерную сессию.",
                Enabled: false,
                DisabledReason: "Браузер не завершает локальный процесс напрямую; закройте вкладку или остановите `--web` в терминале.",
                Kind: "panel",
                Command: string.Empty,
                TargetPanel: "exit-panel")
        };
    }

    private BrowserOptionsSummaryDto BuildOptionsSummary()
    {
        var settings = _stateManager.Settings;
        return new BrowserOptionsSummaryDto(
            MusicEnabled: settings.MusicEnabled,
            SoundEnabled: settings.SoundEnabled,
            ConsoleFontSize: settings.ConsoleFontSize,
            Guidance: "Этот браузерный срез показывает состояние настроек. Полное редактирование настроек остаётся в консольном меню до отдельной Browser Client задачи.");
    }

    private static BrowserAboutDto BuildAboutSummary() =>
        new(
            Title: "Книга Вечности: Перерождение",
            Body: "Локальный браузерный клиент работает поверх того же C# runtime, game_session и игровых контрактов, что и консоль. Он постепенно заменяет debug shell на полноценный игровой интерфейс, не создавая отдельной игровой логики.");

    private async Task<IReadOnlyList<BrowserSaveSlotDto>> BuildSaveSlotsAsync()
    {
        var slots = await BuildSaveSlotsWithPathsAsync();
        return slots.Select(slot => slot.Dto).ToArray();
    }

    private async Task<IReadOnlyList<BrowserSaveSlotWithPath>> BuildSaveSlotsWithPathsAsync()
    {
        var result = new List<BrowserSaveSlotWithPath>();
        await AddSaveSlotsAsync(result, "manual", "Ручное сохранение", "saves/manual_saves");
        await AddSaveSlotsAsync(result, "autosave", "Автосохранение", "saves/autosaves");
        return result
            .OrderByDescending(slot => slot.Dto.TimestampUtc ?? DateTime.MinValue)
            .ThenBy(slot => slot.Dto.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task AddSaveSlotsAsync(
        List<BrowserSaveSlotWithPath> result,
        string scope,
        string scopeLabel,
        string saveDir)
    {
        var saves = await _saveLoad.GetAvailableSavesAsync(saveDir);
        foreach (var save in saves)
        {
            var fileName = Path.GetFileName(save.FileName);
            if (string.IsNullOrWhiteSpace(fileName))
                continue;

            var metadata = save.Metadata;
            var displayName = !string.IsNullOrWhiteSpace(metadata?.SaveName)
                ? metadata!.SaveName
                : Path.GetFileNameWithoutExtension(fileName);
            var turnLabel = metadata?.TurnNumber > 0 ? $"Ход {metadata.TurnNumber}" : "Ход не указан";
            var character = !string.IsNullOrWhiteSpace(metadata?.CharacterName)
                ? metadata!.CharacterName
                : "Душа не указана";

            result.Add(new BrowserSaveSlotWithPath(
                new BrowserSaveSlotDto(
                    SaveId: $"{scope}:{fileName}",
                    Scope: scope,
                    ScopeLabel: scopeLabel,
                    DisplayName: displayName,
                    Description: metadata?.Description ?? string.Empty,
                    CharacterName: character,
                    TurnLabel: turnLabel,
                    TimestampUtc: metadata?.Timestamp,
                    FileSizeBytes: save.FileSize),
                save.FileName));
        }
    }

    private async Task<int> DetectCurrentSessionTurnNumberAsync()
    {
        var storiesPath = _fs.ResolvePath("stories");
        if (!Directory.Exists(storiesPath))
            return 0;

        var maxTurn = 0;
        foreach (var file in Directory.EnumerateFiles(storiesPath, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var root = JsonNode.Parse(json);
                if (root is JsonArray array)
                {
                    foreach (var item in array.OfType<JsonObject>())
                        maxTurn = Math.Max(maxTurn, GetInt(item, "turn") ?? 0);
                }
                else if (root is JsonObject obj)
                {
                    maxTurn = Math.Max(maxTurn, GetInt(obj, "turn", "turnNumber") ?? 0);
                }
            }
            catch (JsonException)
            {
                // Main menu must stay available even if optional story history is malformed.
            }
            catch (IOException)
            {
                // Ignore transient file reads; the status panel can surface deeper validation details.
            }
        }

        return maxTurn;
    }

    private async Task<string?> TryReadTerminalSoulDissipationMessageAsync()
    {
        try
        {
            var json = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
            if (string.IsNullOrWhiteSpace(json) || JsonNode.Parse(json) is not JsonObject root)
                return null;

            if (root[AfterlifeSpiritualConflictState.TerminalGameOverProperty] is not JsonObject gameOver)
                return null;

            var state = GetString(gameOver, "state");
            var message = GetString(gameOver, "message");
            if (!string.Equals(state, AfterlifeSpiritualConflictState.TerminalSoulDissipationState, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(message, AfterlifeSpiritualConflictState.TerminalSoulDissipationMessage, StringComparison.Ordinal))
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(message)
                ? AfterlifeSpiritualConflictState.TerminalSoulDissipationMessage
                : message;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetString(JsonObject root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetPropertyValue(name, out var node) && node is JsonValue value)
            {
                try
                {
                    return value.GetValue<string>();
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        return null;
    }

    private static int? GetInt(JsonObject root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetPropertyValue(name, out var node) && node is JsonValue value)
            {
                try
                {
                    return value.GetValue<int>();
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        return null;
    }

    private sealed record BrowserSaveSlotWithPath(BrowserSaveSlotDto Dto, string FullPath);
}

public sealed record BrowserMainMenuDto(
    int SchemaVersion,
    BrowserMainMenuSessionDto Session,
    IReadOnlyList<BrowserMainMenuActionDto> Actions,
    IReadOnlyList<BrowserSaveSlotDto> Saves,
    BrowserOptionsSummaryDto Options,
    BrowserAboutDto About,
    BrowserAdvancedShellDto AdvancedShell);

public sealed record BrowserMainMenuSessionDto(
    bool GameSessionExists,
    bool HasReadableSoul,
    bool CanContinue,
    string ContinueReason,
    string SoulName,
    string CurrentRealm,
    string RealmLabel,
    int CurrentIncarnation,
    int TurnNumber,
    string TurnLabel,
    bool TerminalSoulDissipated,
    string ValidationState,
    string ValidationLabel,
    string PendingTurnMessage,
    bool CanStartBrowserWrite,
    bool LocalUiLocked,
    DateTime CheckedAtUtc);

public sealed record BrowserMainMenuActionDto(
    string Id,
    string Label,
    string Description,
    bool Enabled,
    string DisabledReason,
    string Kind,
    string Command,
    string TargetPanel);

public sealed record BrowserSaveSlotDto(
    string SaveId,
    string Scope,
    string ScopeLabel,
    string DisplayName,
    string Description,
    string CharacterName,
    string TurnLabel,
    DateTime? TimestampUtc,
    long FileSizeBytes);

public sealed record BrowserOptionsSummaryDto(
    bool MusicEnabled,
    bool SoundEnabled,
    int ConsoleFontSize,
    string Guidance);

public sealed record BrowserAboutDto(string Title, string Body);

public sealed record BrowserAdvancedShellDto(string Label, string Description, bool InitiallyExpanded);

public sealed record BrowserLoadSaveRequest(string? SaveId);

public sealed record BrowserLoadSaveResultDto(
    bool Success,
    string Error,
    string LoadedSaveId,
    BrowserMainMenuDto Menu);
