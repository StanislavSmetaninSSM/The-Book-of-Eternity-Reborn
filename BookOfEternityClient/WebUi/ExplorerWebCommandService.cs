using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;

namespace BookOfEternityClient.WebUi;

public sealed record ExplorerWebCommandRequest(
    string Command,
    string? OwnerId = null,
    string? OwnerLabel = null);

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
        _promptSessions = promptSessions ?? new ExplorerWebPromptSessionService(fs);
    }

    public async Task<ExplorerCommandResult> ExecuteAsync(ExplorerWebCommandRequest? request)
    {
        var command = request?.Command?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(command))
            return MessageResult(command, CommandExecutionState.Failed, UiNotificationSeverity.Error, "Команда не выполнена", "Команда пустая.");
        var effectiveRequest = request ?? new ExplorerWebCommandRequest(command);

        var descriptor = ExplorerCommandCatalog.FindByAlias(command);
        if (descriptor is null)
            return MessageResult(command, CommandExecutionState.Failed, UiNotificationSeverity.Error, "Команда не найдена", "Команда не зарегистрирована в ExplorerMode.");

        if (descriptor.BrowserStatus != ExplorerCommandMigrationStatus.Migrated)
            return BuildBlockedMigrationResult(command, descriptor);

        var commandToken = ExplorerCommandCatalog.ExtractCommandToken(command);
        var result = await BuildMigratedResultAsync(command, commandToken, descriptor);
        return await _promptSessions.AttachSessionIfNeededAsync(result, effectiveRequest);
    }

    public Task<ExplorerCommandResult> SubmitPromptSessionAsync(ExplorerPromptSessionSubmitRequest request) =>
        _promptSessions.SubmitAsync(request);

    public Task<ExplorerCommandResult> CancelPromptSessionAsync(ExplorerPromptSessionCancelRequest request) =>
        _promptSessions.CancelAsync(request);

    public ExplorerCommandResult GetPromptSession(string sessionId) =>
        _promptSessions.GetSession(sessionId);

    private async Task<ExplorerCommandResult> BuildMigratedResultAsync(
        string command,
        string commandToken,
        ExplorerCommandDescriptor descriptor)
    {
        await _stateManager.RefreshGameStateAsync();
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
                commandToken,
                _stateManager,
                _fs,
                _localization),
            ExplorerCommandBrowserHandlerKind.MortalWorld => await ExplorerMortalWorldCommandResultBuilder.TryBuildAsync(
                commandToken,
                _stateManager,
                _fs),
            ExplorerCommandBrowserHandlerKind.ChaosSea => await ExplorerChaosSeaCommandResultBuilder.TryBuildAsync(
                commandToken,
                _stateManager,
                _fs),
            ExplorerCommandBrowserHandlerKind.ShiningAbode => await ExplorerShiningAbodeCommandResultBuilder.TryBuildAsync(
                commandToken,
                _stateManager,
                _fs),
            ExplorerCommandBrowserHandlerKind.AfterlifeCombat => await ExplorerAfterlifeCombatCommandResultBuilder.TryBuildAsync(
                commandToken,
                _stateManager,
                _fs),
            ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn => await ExplorerLifecycleLocalTurnCommandResultBuilder.TryBuildAsync(
                commandToken,
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
