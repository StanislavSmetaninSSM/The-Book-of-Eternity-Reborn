using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;

namespace BookOfEternityClient.WebUi;

public sealed record ExplorerWebCommandRequest(
    string Command,
    string? OwnerId = null,
    string? OwnerLabel = null,
    bool? AdvancedEnabled = null);

public sealed class ExplorerWebCommandService
{
    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly LocalizationManager _localization;
    private readonly ValidationService _validationService;
    private readonly ExplorerWebPromptSessionService _promptSessions;

    public ExplorerWebCommandService(
        FileSystemManager fs,
        StateManager stateManager,
        LocalizationManager localization,
        ValidationService validationService,
        ExplorerWebPromptSessionService? promptSessions = null)
    {
        _fs = fs;
        _stateManager = stateManager;
        _localization = localization;
        _validationService = validationService;
        _promptSessions = promptSessions ?? new ExplorerWebPromptSessionService(fs, stateManager);
    }

    public async Task<ExplorerCommandResult> ExecuteAsync(ExplorerWebCommandRequest? request)
    {
        var command = request?.Command?.Trim() ?? string.Empty;
        var parsed = ExplorerCommandParser.Parse(command);
        if (!parsed.Success)
            return MessageResult(command, CommandExecutionState.Failed, UiNotificationSeverity.Error, parsed.ErrorTitle, parsed.ErrorMessage);
        var effectiveRequest = request ?? new ExplorerWebCommandRequest(command);

        var descriptor = parsed.Descriptor!;
        if (string.Equals(descriptor.Id, "gacha", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(parsed.Arguments))
        {
            return MessageResult(
                command,
                CommandExecutionState.Failed,
                UiNotificationSeverity.Error,
                "Некорректные аргументы",
                "Команда /gacha не принимает аргументы. Выберите поддерживаемый прямой призыв Моря Хаоса через браузерную форму.");
        }

        if (!ExplorerCommandMigrationRegistry.IsBrowserExecutable(descriptor.BrowserStatus))
            return BuildBlockedMigrationResult(command, descriptor);

        if (parsed.Subcommand is { } subcommand &&
            !ExplorerCommandMigrationRegistry.IsBrowserExecutable(subcommand.BrowserStatus))
            return BuildBlockedMigrationResult(command, subcommand);

        var result = await BuildMigratedResultAsync(parsed, descriptor, effectiveRequest);
        var withPromptSession = await _promptSessions.AttachSessionIfNeededAsync(result, effectiveRequest);
        return ApplyDefaultPlayerSurface(withPromptSession, descriptor, effectiveRequest);
    }

    public Task<ExplorerCommandResult> SubmitPromptSessionAsync(ExplorerPromptSessionSubmitRequest request) =>
        _promptSessions.SubmitAsync(request);

    public Task<ExplorerCommandResult> CancelPromptSessionAsync(ExplorerPromptSessionCancelRequest request) =>
        _promptSessions.CancelAsync(request);

    public ExplorerCommandResult GetPromptSession(string sessionId) =>
        _promptSessions.GetSession(sessionId);

    private async Task<ExplorerCommandResult> BuildMigratedResultAsync(
        ExplorerParsedCommand parsed,
        ExplorerCommandDescriptor descriptor,
        ExplorerWebCommandRequest request)
    {
        await _stateManager.RefreshGameStateAsync();
        var command = parsed.BuilderCommand;
        var commandToken = ExplorerCommandCatalog.ExtractCommandToken(command);
        var builderCommand = parsed.Subcommand == null ? commandToken : parsed.CanonicalCommand;
        if (descriptor.BrowserHandlerKind == ExplorerCommandBrowserHandlerKind.Math)
            return ExplorerMathCommandResultBuilder.Build(command);

        if (descriptor.BrowserHandlerKind == ExplorerCommandBrowserHandlerKind.Help)
        {
            var state = _stateManager.CurrentState;
            return ExplorerHelpCommandResultBuilder.Build(new ExplorerHelpCommandContext
            {
                Command = commandToken,
                Title = _localization.T("help"),
                IsChaosSea = state.IsInChaosSea,
                IsShiningAbode = state.IsInShiningAbode,
                IsPendingShiningAbodeBootstrap = state.IsInShiningAbodePendingBootstrap,
                CanReenterShiningAbode = state.CanReenterShiningAbode
            });
        }

        var result = descriptor.BrowserHandlerKind switch
        {
            ExplorerCommandBrowserHandlerKind.UniversalMeta => await ExplorerUniversalMetaCommandResultBuilder.TryBuildAsync(
                descriptor.AcceptsArguments ? parsed.BuilderCommand : builderCommand,
                _stateManager,
                _fs,
                _localization),
            ExplorerCommandBrowserHandlerKind.MortalWorld => await ExplorerMortalWorldCommandResultBuilder.TryBuildAsync(
                descriptor.AcceptsArguments ? parsed.BuilderCommand : commandToken,
                _stateManager,
                _fs),
            ExplorerCommandBrowserHandlerKind.ChaosSea => await ExplorerChaosSeaCommandResultBuilder.TryBuildAsync(
                descriptor.AcceptsArguments ? parsed.BuilderCommand : commandToken,
                _stateManager,
                _fs,
                request.AdvancedEnabled == true),
            ExplorerCommandBrowserHandlerKind.ShiningAbode => await ExplorerShiningAbodeCommandResultBuilder.TryBuildAsync(
                descriptor.AcceptsArguments ? parsed.BuilderCommand : commandToken,
                _stateManager,
                _fs,
                request.AdvancedEnabled == true),
            ExplorerCommandBrowserHandlerKind.AfterlifeCombat => await ExplorerAfterlifeCombatCommandResultBuilder.TryBuildAsync(
                descriptor.AcceptsArguments ? parsed.BuilderCommand : commandToken,
                _stateManager,
                _fs,
                request.AdvancedEnabled == true),
            ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn => await ExplorerLifecycleLocalTurnCommandResultBuilder.TryBuildAsync(
                descriptor.AcceptsArguments ? parsed.BuilderCommand : commandToken,
                _stateManager,
                _fs,
                _validationService),
            _ => null
        };
        if (result != null)
            return result;

        return MessageResult(
            command,
            CommandExecutionState.Failed,
            UiNotificationSeverity.Error,
            "Команда не подключена",
            "Команда помечена как перенесенная, но web command service пока не знает, как ее построить.");
    }

    private ExplorerCommandResult ApplyDefaultPlayerSurface(
        ExplorerCommandResult result,
        ExplorerCommandDescriptor descriptor,
        ExplorerWebCommandRequest request)
    {
        if (request.AdvancedEnabled == true || _stateManager.Settings.ShowGmThoughts)
            return result;

        if (descriptor.BrowserHandlerKind == ExplorerCommandBrowserHandlerKind.Help)
            return result;

        var metadata = BrowserPlayerCommandMenuBuilder.GetCoverageMetadata(descriptor);
        if (!string.Equals(metadata.Surface, "player-default", StringComparison.OrdinalIgnoreCase))
            return BrowserEntityDossierPrototypeNormalizer.Normalize(result);

        var projected = new ExplorerCommandResult
        {
            Command = result.Command,
            State = result.State,
            Blocks = ProjectPlayerDefaultBlocks(result.Blocks),
            Actions = result.Actions,
            Prompts = result.Prompts,
            Notifications = result.Notifications,
            InteractiveSession = result.InteractiveSession
        };

        return BrowserEntityDossierPrototypeNormalizer.Normalize(projected);
    }

    private static List<UiBlock> ProjectPlayerDefaultBlocks(IEnumerable<UiBlock> blocks)
    {
        var filtered = new List<UiBlock>();
        foreach (var block in blocks)
        {
            switch (block)
            {
                case UiRawJsonBlock:
                    continue;
                case UiTextBlock text:
                    if (TryProjectPlayerDefaultText(text.Text, out var playerText))
                        filtered.Add(new UiTextBlock { Text = playerText, Tone = text.Tone });
                    break;
                case UiPanelBlock panel:
                    var childBlocks = ProjectPlayerDefaultBlocks(panel.Blocks);
                    if (childBlocks.Count > 0)
                    {
                        filtered.Add(new UiPanelBlock
                        {
                            Title = ProjectPlayerDefaultText(panel.Title),
                            Blocks = childBlocks
                        });
                    }
                    break;
                case UiTableBlock table:
                    if (TryProjectPlayerDefaultTable(table, out var playerTable))
                        filtered.Add(playerTable);
                    break;
                case UiListBlock list:
                    var playerItems = list.Items
                        .Select(ProjectPlayerDefaultText)
                        .Where(static item => !string.IsNullOrWhiteSpace(item) && !ContainsPlayerDefaultTechnicalMarker(item))
                        .ToList();
                    if (playerItems.Count > 0)
                        filtered.Add(new UiListBlock { Ordered = list.Ordered, Items = playerItems });
                    break;
                case UiKeyValueGridBlock grid:
                    var playerKeyValues = grid.Items
                        .Select(ProjectPlayerDefaultKeyValue)
                        .Where(static item => item != null)
                        .Cast<UiKeyValueItem>()
                        .ToList();
                    if (playerKeyValues.Count > 0)
                        filtered.Add(new UiKeyValueGridBlock { Items = playerKeyValues });
                    break;
                case UiMessageBlock message:
                    if (TryProjectPlayerDefaultMessage(message, out var playerMessage))
                        filtered.Add(playerMessage);
                    break;
                default:
                    filtered.Add(block);
                    break;
            }
        }

        return filtered;
    }

    private static bool TryProjectPlayerDefaultText(string text, out string playerText)
    {
        playerText = ProjectPlayerDefaultText(text);
        if (string.IsNullOrWhiteSpace(playerText))
            return false;

        return !ContainsPlayerDefaultTechnicalMarker(playerText);
    }

    private static bool TryProjectPlayerDefaultMessage(UiMessageBlock message, out UiMessageBlock playerMessage)
    {
        var title = ProjectPlayerDefaultText(message.Title);
        var text = ProjectPlayerDefaultText(message.Message);
        if (IsDiagnosticJsonText(message.Title) || IsDiagnosticJsonText(message.Message))
        {
            playerMessage = null!;
            return false;
        }

        if (ContainsPlayerDefaultTechnicalMarker(title) || ContainsPlayerDefaultTechnicalMarker(text))
        {
            playerMessage = null!;
            return false;
        }

        playerMessage = new UiMessageBlock
        {
            Severity = message.Severity,
            Title = title,
            Message = text
        };
        return !string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(text);
    }

    private static bool TryProjectPlayerDefaultTable(UiTableBlock table, out UiTableBlock playerTable)
    {
        var title = ProjectPlayerDefaultText(table.Title);
        var removePathColumn = table.Columns.Any(static column =>
            string.Equals(column, "Путь", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(column, "Path", StringComparison.OrdinalIgnoreCase));
        var pathColumnIndex = removePathColumn
            ? table.Columns.FindIndex(static column =>
                string.Equals(column, "Путь", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(column, "Path", StringComparison.OrdinalIgnoreCase))
            : -1;

        var columns = new List<string>();
        for (var i = 0; i < table.Columns.Count; i++)
        {
            if (i == pathColumnIndex)
                continue;
            var column = ProjectPlayerDefaultText(table.Columns[i]);
            if (removePathColumn && i == 0 && string.Equals(column, "Артефакт", StringComparison.OrdinalIgnoreCase))
                column = "Проверка";
            if (!string.IsNullOrWhiteSpace(column) && !ContainsPlayerDefaultTechnicalMarker(column))
                columns.Add(column);
        }

        var rows = new List<UiTableRow>();
        foreach (var row in table.Rows)
        {
            var cells = new List<string>();
            for (var i = 0; i < row.Cells.Count; i++)
            {
                if (i == pathColumnIndex)
                    continue;

                var cell = ProjectPlayerDefaultText(row.Cells[i]);
                cells.Add(ContainsPlayerDefaultTechnicalMarker(cell) ? string.Empty : cell);
            }

            if (cells.Count > 0)
                rows.Add(new UiTableRow { Cells = cells });
        }

        playerTable = new UiTableBlock { Title = title, Columns = columns, Rows = rows };
        return columns.Count > 0 && rows.Count > 0 && !ContainsPlayerDefaultTechnicalMarker(title);
    }

    private static UiKeyValueItem? ProjectPlayerDefaultKeyValue(UiKeyValueItem item)
    {
        var key = ProjectPlayerDefaultText(item.Key);
        var value = ProjectPlayerDefaultText(item.Value);
        if (string.IsNullOrWhiteSpace(key) ||
            ContainsPlayerDefaultTechnicalMarker(key) ||
            ContainsPlayerDefaultTechnicalMarker(value))
        {
            return null;
        }

        return new UiKeyValueItem { Key = key, Value = value };
    }

    private static string ProjectPlayerDefaultText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var value = text
            .Replace("Локальный ход / GM-turn protocol", "Локальный ход", StringComparison.OrdinalIgnoreCase)
            .Replace("Артефакты протокола", "Состояние локальной записи", StringComparison.OrdinalIgnoreCase)
            .Replace("GM-turn protocol", "GM-ход", StringComparison.OrdinalIgnoreCase)
            .Replace("Готов terminal error", "Готова ошибка хода", StringComparison.OrdinalIgnoreCase)
            .Replace("terminal error", "ошибка хода", StringComparison.OrdinalIgnoreCase)
            .Replace("Validated pending snapshot", "Снимок состояния хода", StringComparison.OrdinalIgnoreCase)
            .Replace("Копии snapshot файлов", "Копии файлов текущего хода", StringComparison.OrdinalIgnoreCase)
            .Replace("Локальные rollback backup", "Копии восстановления локальной записи", StringComparison.OrdinalIgnoreCase)
            .Replace("Browser-write команды должны дождаться завершения, ошибки или отмены этого протокола.", "Дождитесь завершения, ошибки или отмены текущего хода.", StringComparison.OrdinalIgnoreCase)
            .Replace("Browser DTO может безопасно показать форму локального действия.", "Можно открыть форму локального действия.", StringComparison.OrdinalIgnoreCase)
            .Replace("Браузерный DTO фиксирует route tag и форму, но не вызывает console-bound отправку хода.", "Форма подготовит описание действия для следующего хода.", StringComparison.OrdinalIgnoreCase)
            .Replace("Текущий realm", "Текущее царство", StringComparison.OrdinalIgnoreCase)
            .Replace("Response surface", "Поверхность ответа", StringComparison.OrdinalIgnoreCase)
            .Replace("State file", "Файл состояния", StringComparison.OrdinalIgnoreCase);

        return value.Trim();
    }

    private static bool IsDiagnosticJsonText(string text) =>
        !string.IsNullOrWhiteSpace(text) &&
        (text.Contains("JSON:", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("Файл не найден:", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("Файл пуст или не содержит JSON:", StringComparison.OrdinalIgnoreCase));

    private static bool ContainsPlayerDefaultTechnicalMarker(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        foreach (var marker in new[]
                 {
                     "game_state/",
                     ".json",
                     "UiRawJsonBlock",
                     "image_prompt",
                     "factionColor",
                     "gm_thoughts",
                     "currentRealm",
                     "DTO",
                     "API",
                     "endpoint",
                     "exception",
                     "console-bound",
                     "route tag"
                 })
        {
            if (text.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static ExplorerCommandResult BuildBlockedMigrationResult(string command, ExplorerCommandDescriptor descriptor)
    {
        var reason = string.IsNullOrWhiteSpace(descriptor.Reason)
            ? "Эта команда еще не перенесена в браузерный API."
            : descriptor.Reason;
        var followUp = string.IsNullOrWhiteSpace(descriptor.FollowUpIssue)
            ? string.Empty
            : $" Следующая задача: {descriptor.FollowUpIssue}.";

        return MessageResult(
            command,
            CommandExecutionState.Blocked,
            UiNotificationSeverity.Warning,
            "Команда пока недоступна в браузерном API",
            reason + followUp);
    }

    private static ExplorerCommandResult BuildBlockedMigrationResult(string command, ExplorerCommandSubcommandDescriptor subcommand)
    {
        var reason = string.IsNullOrWhiteSpace(subcommand.Reason)
            ? "Эта подкоманда еще не перенесена в браузерный API."
            : subcommand.Reason;
        var followUp = string.IsNullOrWhiteSpace(subcommand.FollowUpIssue)
            ? string.Empty
            : $" Следующая задача: {subcommand.FollowUpIssue}.";

        return MessageResult(
            command,
            CommandExecutionState.Blocked,
            UiNotificationSeverity.Warning,
            "Подкоманда пока недоступна в браузерном API",
            reason + followUp);
    }

    private static ExplorerCommandResult MessageResult(
        string command,
        CommandExecutionState state,
        UiNotificationSeverity severity,
        string title,
        string message) =>
        new()
        {
            Command = command,
            State = state,
            Blocks =
            [
                new UiMessageBlock
                {
                    Severity = severity,
                    Title = title,
                    Message = message
                }
            ]
        };
}
