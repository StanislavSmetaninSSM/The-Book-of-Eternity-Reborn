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
        if (!ExplorerCommandMigrationRegistry.IsBrowserExecutable(descriptor.BrowserStatus))
            return BuildBlockedMigrationResult(command, descriptor);

        if (parsed.Subcommand is { } subcommand &&
            !ExplorerCommandMigrationRegistry.IsBrowserExecutable(subcommand.BrowserStatus))
            return BuildBlockedMigrationResult(command, subcommand);

        var result = await BuildMigratedResultAsync(parsed, descriptor, effectiveRequest);
        return await _promptSessions.AttachSessionIfNeededAsync(result, effectiveRequest);
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
                builderCommand,
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
                _fs,
                request.AdvancedEnabled == true),
            ExplorerCommandBrowserHandlerKind.ShiningAbode => await ExplorerShiningAbodeCommandResultBuilder.TryBuildAsync(
                commandToken,
                _stateManager,
                _fs,
                request.AdvancedEnabled == true),
            ExplorerCommandBrowserHandlerKind.AfterlifeCombat => await ExplorerAfterlifeCombatCommandResultBuilder.TryBuildAsync(
                commandToken,
                _stateManager,
                _fs,
                request.AdvancedEnabled == true),
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
