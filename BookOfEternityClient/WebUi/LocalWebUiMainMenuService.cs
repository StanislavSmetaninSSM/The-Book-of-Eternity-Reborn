using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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
        var writeResult = await _writeCoordinator.ExecuteSessionReplacementAsync(
            new BrowserLocalWriteRequest(
                OwnerId: "browser-main-menu-load",
                OwnerLabel: "Browser main menu",
                OperationLabel: "browser save load"),
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

    /// <summary>
    /// Creates a manual save while retaining its publication decision through browser follow-up and bound close.
    /// </summary>
    /// <param name="request">
    /// The requested display name; a null or empty name uses the current chapter label.
    /// </param>
    /// <returns>
    /// The exact confirmed save identity and a truthful decision, even when later browser work fails.
    /// </returns>
    public async Task<BrowserCreateSaveResultDto> CreateManualSaveAsync(BrowserCreateSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        BrowserCreateSaveResultDto? retained = null;
        try
        {
            return await _writeCoordinator.RunBoundTransactionAsync(
                writeLease => CreateManualSaveBoundAsync(writeLease, request, result => retained = result));
        }
        catch (Exception failure)
        {
            if (retained != null)
                return retained with
                {
                    NeedsFollowUp = true,
                    ContinuationBlocked = true,
                    Menu = null,
                    Error = retained.Success
                        ? "Сохранение создано. Завершение операции требует проверки; следующее действие остановлено."
                        : retained.Error
                };
            return new BrowserCreateSaveResultDto(
                Success: false,
                Error: "Не удалось безопасно начать сохранение. Проверьте локальное хранилище перед следующим действием.",
                CreatedSaveId: string.Empty,
                Menu: null,
                Disposition: failure is CoordinatedStatePublicationUncertainException
                    ? BrowserPreparedWriteDisposition.Uncertain : BrowserPreparedWriteDisposition.Blocked,
                NeedsFollowUp: true,
                ContinuationBlocked: true);
        }
    }

    /// <summary>
    /// Prepares and publishes a manual archive on the browser's held lease without nesting publication or acquisition.
    /// </summary>
    /// <param name="writeLease">
    /// The held canonical lease used for admission, snapshot, publication and menu refresh.
    /// </param>
    /// <param name="request">
    /// The requested optional save display name.
    /// </param>
    /// <param name="retain">
    /// Stores the established browser result before later refresh or bound close can fail.
    /// </param>
    /// <returns>
    /// The established decision and an optional safely refreshed menu.
    /// </returns>
    private async Task<BrowserCreateSaveResultDto> CreateManualSaveBoundAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        BrowserCreateSaveRequest request,
        Action<BrowserCreateSaveResultDto> retain)
    {
        var currentMenu = await BuildBoundAsync(writeLease);
        if (!currentMenu.Session.GameSessionExists || !currentMenu.Session.HasReadableSoul)
        {
            return new BrowserCreateSaveResultDto(
                Success: false,
                Error: "Активная глава не найдена. Сначала начните игру или загрузите существующее сохранение.",
                CreatedSaveId: string.Empty,
                Menu: currentMenu);
        }

        var writeStatus = await _writeCoordinator.BuildStatusAsync(writeLease);
        if (!writeStatus.CanStartBrowserWrite)
        {
            return new BrowserCreateSaveResultDto(
                Success: false,
                Error: BuildBrowserWriteBlockedMessage(writeStatus, "Сохранение сейчас недоступно из-за состояния главы."),
                CreatedSaveId: string.Empty,
                Menu: currentMenu);
        }

        var saveName = BuildManualSaveName(request.SaveName, currentMenu.Session);
        var description = BuildManualSaveDescription(currentMenu.Session);
        var result = await _writeCoordinator.ExecutePreparedSaveWithinTransactionAsync(
            writeLease,
            new BrowserLocalWriteRequest(
                OwnerId: "browser-main-menu-save",
                OwnerLabel: "Browser main menu",
                OperationLabel: "browser manual save"),
            lease => _saveLoad.PrepareSaveArchiveAsync(
                    lease,
                    saveName,
                    description,
                    "saves/manual_saves",
                    currentMenu.Session.TurnNumber),
            _saveLoad.PublishPreparedSaveAsync);

        var response = BuildCreateSaveResult(result, null);
        retain(response);
        if (result.Disposition == SaveCreationDisposition.Uncertain || result.ContinuationBlocked)
            return response;
        try
        {
            _fs.ResolveBackupPublicationRecovery(writeLease);
            response = BuildCreateSaveResult(result, await BuildBoundAsync(writeLease));
        }
        catch (Exception failure)
        {
            response = BuildCreateSaveResult(result.WithFollowUp(failure, blocksContinuation: true), null);
        }
        retain(response);
        return response;
    }

    /// <summary>
    /// Projects the established archive decision without inferring identity from a later save list.
    /// </summary>
    /// <param name="result">
    /// The exact archive decision and follow-up state.
    /// </param>
    /// <param name="menu">
    /// A safely refreshed menu, or null when no refresh is available.
    /// </param>
    /// <returns>
    /// The transport decision, exact committed identifier and continuation state.
    /// </returns>
    private static BrowserCreateSaveResultDto BuildCreateSaveResult(SaveCreationResult result, BrowserMainMenuDto? menu) => new(
        Success: result.Committed,
        Error: result.Disposition switch
        {
            SaveCreationDisposition.Committed => result.NeedsFollowUp
                ? "Сохранение создано. Служебная очистка или обновление интерфейса требуют проверки."
                : string.Empty,
            SaveCreationDisposition.RolledBack => "Сохранение не создано: предыдущие файлы восстановлены.",
            SaveCreationDisposition.Uncertain => "Не удалось подтвердить состояние сохранения. Следующее действие остановлено до проверки хранилища.",
            _ => "Сохранение не создано: подготовка или локальное право записи заблокированы."
        },
        CreatedSaveId: result.Committed ? "manual:" + Path.GetFileName(result.DestinationRelativePath) : string.Empty,
        Menu: menu,
        Disposition: result.Disposition switch
        {
            SaveCreationDisposition.Committed => BrowserPreparedWriteDisposition.Committed,
            SaveCreationDisposition.RolledBack => BrowserPreparedWriteDisposition.RolledBack,
            SaveCreationDisposition.Uncertain => BrowserPreparedWriteDisposition.Uncertain,
            _ => BrowserPreparedWriteDisposition.Blocked
        },
        NeedsFollowUp: result.NeedsFollowUp,
        ContinuationBlocked: result.ContinuationBlocked);

    private async Task<BrowserMainMenuDto> BuildBoundAsync(
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        var dashboard = await _lifecycle.BuildDashboardAsync(writeLease);
        var terminalSoulDissipationMessage = await TryReadTerminalSoulDissipationMessageAsync();
        var saves = await BuildSaveSlotsAsync(writeLease);
        var session = await BuildSessionSummaryAsync(
            dashboard,
            terminalSoulDissipationMessage,
            writeLease);
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

    private async Task<BrowserMainMenuSessionDto> BuildSessionSummaryAsync(
        BrowserLifecycleDashboardDto dashboard,
        string? terminalSoulDissipationMessage,
        FileSystemManager.CanonicalWriteLease? writeLease = null)
    {
        if (writeLease == null)
            await _stateManager.RefreshGameStateAsync();
        else
            await _stateManager.RefreshGameStateAsync(writeLease);
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

    private static string BuildBrowserWriteBlockedMessage(
        BrowserLocalWriteStatus status,
        string fallbackMessage = "Загрузка сохранения сейчас недоступна из-за состояния главы.")
    {
        if (status.PendingTurn.HasActiveGmTurn)
            return $"Книга занята текущим ходом: {status.PendingTurn.Message}";

        if (status.LocalUiLock.Exists && !status.LocalUiLock.IsStale)
        {
            return "Книга занята другим действием. Дождитесь завершения операции или откройте расширенный режим.";
        }

        return fallbackMessage;
    }

    private static string BuildManualSaveName(string? requestedName, BrowserMainMenuSessionDto session)
    {
        var trimmed = requestedName?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmed))
            return trimmed;

        var soul = string.IsNullOrWhiteSpace(session.SoulName) || session.SoulName == "Нет активной души"
            ? "герой"
            : session.SoulName;
        return $"Браузерное сохранение - {soul}, {session.TurnLabel}";
    }

    private static string BuildManualSaveDescription(BrowserMainMenuSessionDto session) =>
        $"Ручное сохранение из браузера: {session.SoulName}, {session.RealmLabel}, {session.TurnLabel}.";

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

    /// <summary>
    /// Builds both save-library sections using the caller's lease when one is held.
    /// </summary>
    /// <param name="writeLease">
    /// The held canonical lease, or <see langword="null"/> to acquire ordinary listing leases.
    /// </param>
    /// <returns>
    /// The available save slots in display order.
    /// </returns>
    private async Task<IReadOnlyList<BrowserSaveSlotDto>> BuildSaveSlotsAsync(
        FileSystemManager.CanonicalWriteLease? writeLease = null)
    {
        var slots = await BuildSaveSlotsWithPathsAsync(writeLease);
        return slots.Select(slot => slot.Dto).ToArray();
    }

    /// <summary>
    /// Builds save slots and their physical load paths without nesting lease acquisition.
    /// </summary>
    /// <param name="writeLease">
    /// The held canonical lease, or <see langword="null"/> for independent listing.
    /// </param>
    /// <returns>
    /// The available slots and physical paths in display order.
    /// </returns>
    private async Task<IReadOnlyList<BrowserSaveSlotWithPath>> BuildSaveSlotsWithPathsAsync(
        FileSystemManager.CanonicalWriteLease? writeLease = null)
    {
        var result = new List<BrowserSaveSlotWithPath>();
        await AddSaveSlotsAsync(result, "manual", "Ручное сохранение", "saves/manual_saves", writeLease);
        await AddSaveSlotsAsync(result, "autosave", "Автосохранение", "saves/autosaves", writeLease);
        return result
            .OrderByDescending(slot => slot.Dto.TimestampUtc ?? DateTime.MinValue)
            .ThenBy(slot => slot.Dto.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Appends validated save slots from one library section.
    /// </summary>
    /// <param name="result">
    /// The destination list to extend.
    /// </param>
    /// <param name="scope">
    /// The section identifier used in each save identifier.
    /// </param>
    /// <param name="scopeLabel">
    /// The section's display label.
    /// </param>
    /// <param name="saveDir">
    /// The relative save-library directory to read.
    /// </param>
    /// <param name="writeLease">
    /// The held canonical lease, or <see langword="null"/> for independent listing.
    /// </param>
    private async Task AddSaveSlotsAsync(
        List<BrowserSaveSlotWithPath> result,
        string scope,
        string scopeLabel,
        string saveDir,
        FileSystemManager.CanonicalWriteLease? writeLease = null)
    {
        var saves = writeLease == null
            ? await _saveLoad.GetAvailableSavesAsync(saveDir)
            : await _saveLoad.GetAvailableSavesAsync(writeLease, saveDir);
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

public sealed record BrowserCreateSaveRequest(string? SaveName);

/// <summary>
/// Carries the save publication decision separately from subsequent browser follow-up work.
/// </summary>
/// <param name="Success">
/// Whether the save archive was committed.
/// </param>
/// <param name="Error">
/// The explanation of a blocked operation or follow-up requirement; empty on ordinary success.
/// </param>
/// <param name="CreatedSaveId">
/// The identifier derived from the committed destination, or empty when no commit is confirmed.
/// </param>
/// <param name="Menu">
/// The refreshed menu, or <see langword="null"/> when continuation cannot safely refresh it.
/// </param>
/// <param name="Disposition">
/// The established publication decision, including unresolved uncertainty.
/// </param>
/// <param name="NeedsFollowUp">
/// Whether publication or subsequent browser work requires follow-up.
/// </param>
/// <param name="ContinuationBlocked">
/// Whether further browser operations must stop until the session is reconciled.
/// </param>
public sealed record BrowserCreateSaveResultDto(
    bool Success,
    string Error,
    string CreatedSaveId,
    BrowserMainMenuDto? Menu,
    [property: JsonConverter(typeof(JsonStringEnumConverter<BrowserPreparedWriteDisposition>))]
    BrowserPreparedWriteDisposition Disposition = BrowserPreparedWriteDisposition.Blocked,
    bool NeedsFollowUp = false,
    bool ContinuationBlocked = false);
