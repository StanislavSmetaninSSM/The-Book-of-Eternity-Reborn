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
                Description: "Служебные сведения и перенесённые команды скрыты от обычного главного меню, но остаются доступны для проверки в расширенном режиме.",
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
                Error: "Сохранение не найдено в текущем списке загрузки.",
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
        await _stateManager.RefreshGameStateAsync();
        var turnNumber = _stateManager.CurrentState.TurnNumber;
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
            return $"Книга открылась, но проверка нашла ошибки: {dashboard.Validation.ErrorCount}. Перед длинной игрой откройте расширенный режим и проверьте состояние.";

        if (dashboard.Validation.WarningCount > 0)
            return $"Текущую главу можно продолжить, но книга просит внимания: {dashboard.Validation.WarningCount}.";

        return "Текущую главу можно продолжить.";
    }

    private static string BuildContinueBlocker(
        BrowserLifecycleDashboardDto dashboard,
        string? terminalSoulDissipationMessage)
    {
        if (!string.IsNullOrWhiteSpace(terminalSoulDissipationMessage))
            return $"{terminalSoulDissipationMessage} Текущую сессию продолжить нельзя; загрузите сохранение через меню загрузки.";

        if (!dashboard.Session.GameSessionExists)
            return "Активной главы пока нет. Начните новую игру или загрузите сохранение.";

        if (!dashboard.Soul.IsReadable)
            return string.IsNullOrWhiteSpace(dashboard.Soul.ReadError)
                ? "Не удалось прочитать текущую душу. Загрузите сохранение или откройте расширенный режим."
                : dashboard.Soul.ReadError;

        return "Продолжение сейчас недоступно. Проверьте состояние главы.";
    }

    private static string BuildBrowserWriteBlockedMessage(BrowserLocalWriteStatus status)
    {
        if (status.PendingTurn.HasActiveGmTurn)
            return $"Книга занята текущим ходом: {status.PendingTurn.Message}";

        if (status.LocalUiLock.Exists && !status.LocalUiLock.IsStale)
        {
            return "Книга занята другим действием. Дождитесь завершения операции или откройте расширенный режим.";
        }

        return "Загрузка сохранения сейчас недоступна из-за состояния главы.";
    }

    private static IReadOnlyList<BrowserMainMenuActionDto> BuildActions(
        BrowserMainMenuSessionDto session,
        IReadOnlyList<BrowserSaveSlotDto> saves,
        BrowserLifecycleDashboardDto dashboard)
    {
        var newGameEnabled = dashboard.CanStartBrowserWrite;
        var newGameReason = newGameEnabled
            ? string.Empty
            : "Новая глава недоступна, пока книга занята ходом ГМа или другим действием.";
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
                Description: "Открыть подготовку новой жизни и мира.",
                Enabled: newGameEnabled,
                DisabledReason: newGameReason,
                Kind: "command",
                Command: "/world_setup",
                TargetPanel: "game-shell"),
            new BrowserMainMenuActionDto(
                Id: "qte-practice",
                Label: "Тренировка QTE",
                Description: "Свободная тренировка быстрых сцен без наград и без изменения прохождения.",
                Enabled: true,
                DisabledReason: string.Empty,
                Kind: "client-panel",
                Command: string.Empty,
                TargetPanel: "practice-panel"),
            new BrowserMainMenuActionDto(
                Id: "daren-showcase",
                Label: "Вылазка Дарена",
                Description: "Отдельное QTE-ограбление поместья с постоянным лучшим итогом для будущей новой игры.",
                Enabled: true,
                DisabledReason: string.Empty,
                Kind: "client-panel",
                Command: string.Empty,
                TargetPanel: "daren-showcase-panel"),
            new BrowserMainMenuActionDto(
                Id: "load",
                Label: "Загрузить",
                Description: hasSaves
                    ? (loadEnabled ? "Выберите сохранение из списка." : loadDisabledReason)
                    : "Сохранений пока нет.",
                Enabled: loadEnabled,
                DisabledReason: loadEnabled ? string.Empty : loadDisabledReason,
                Kind: "panel",
                Command: string.Empty,
                TargetPanel: "load-panel"),
            new BrowserMainMenuActionDto(
                Id: "options",
                Label: "Настройки",
                Description: "Показать настройки книги, звука и доступности.",
                Enabled: true,
                DisabledReason: string.Empty,
                Kind: "panel",
                Command: string.Empty,
                TargetPanel: "options-panel"),
            new BrowserMainMenuActionDto(
                Id: "about",
                Label: "О мире / книге",
                Description: "Кратко описать Reborn и границы этой книги.",
                Enabled: true,
                DisabledReason: string.Empty,
                Kind: "panel",
                Command: string.Empty,
                TargetPanel: "about-panel"),
            new BrowserMainMenuActionDto(
                Id: "exit",
                Label: "Выход",
                Description: "Закрытие вкладки оставляет книгу в текущем состоянии.",
                Enabled: false,
                DisabledReason: "Чтобы завершить работу, закройте вкладку или остановите игру в том окне, где она была запущена.",
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
            Guidance: "Настройки книги, звука и доступности открываются в отдельном разделе.");
    }

    private static BrowserAboutDto BuildAboutSummary() =>
        new(
            Title: "Книга Вечности: Перерождение",
            Body: "Книга Вечности: Перерождение открывает текущую главу, сохранения и настройки в одном локальном окне. Игровые решения остаются в книге; этот экран помогает выбрать продолжение, новую главу или сохранение.");

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
