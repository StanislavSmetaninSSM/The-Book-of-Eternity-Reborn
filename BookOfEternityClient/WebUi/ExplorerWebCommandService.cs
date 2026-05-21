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

        var exactEntry = ExplorerCommandMigrationRegistry.Entries
            .FirstOrDefault(item => string.Equals(item.Command, command, StringComparison.OrdinalIgnoreCase));
        var commandToken = exactEntry?.Command ?? ExtractCommandToken(command);

        var entry = exactEntry ?? (ExplorerMathCommandResultBuilder.CanBuild(commandToken)
            ? ExplorerCommandMigrationRegistry.Entries
                .FirstOrDefault(item => string.Equals(item.Command, commandToken, StringComparison.OrdinalIgnoreCase))
            : null);

        if (entry is null)
            return MessageResult(command, CommandExecutionState.Failed, UiNotificationSeverity.Error, "Команда не найдена", "Команда не зарегистрирована в ExplorerMode.");

        if (entry.Status != ExplorerCommandMigrationStatus.Migrated)
            return BuildBlockedMigrationResult(command, entry);

        var result = await BuildMigratedResultAsync(command, commandToken);
        return await _promptSessions.AttachSessionIfNeededAsync(result, effectiveRequest);
    }

    public Task<ExplorerCommandResult> SubmitPromptSessionAsync(ExplorerPromptSessionSubmitRequest request) =>
        _promptSessions.SubmitAsync(request);

    public Task<ExplorerCommandResult> CancelPromptSessionAsync(ExplorerPromptSessionCancelRequest request) =>
        _promptSessions.CancelAsync(request);

    public ExplorerCommandResult GetPromptSession(string sessionId) =>
        _promptSessions.GetSession(sessionId);

    private async Task<ExplorerCommandResult> BuildMigratedResultAsync(string command, string commandToken)
    {
        await _stateManager.RefreshGameStateAsync();
        if (ExplorerMathCommandResultBuilder.CanBuild(commandToken))
            return ExplorerMathCommandResultBuilder.Build(command);

        if (string.Equals(commandToken, "/help", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(commandToken, "/помощь", StringComparison.OrdinalIgnoreCase))
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

        var universalResult = await ExplorerUniversalMetaCommandResultBuilder.TryBuildAsync(
            commandToken,
            _stateManager,
            _fs,
            _localization);
        if (universalResult != null)
            return universalResult;

        var mortalResult = await ExplorerMortalWorldCommandResultBuilder.TryBuildAsync(
            commandToken,
            _stateManager,
            _fs);
        if (mortalResult != null)
            return mortalResult;

        var chaosSeaResult = await ExplorerChaosSeaCommandResultBuilder.TryBuildAsync(
            commandToken,
            _stateManager,
            _fs);
        if (chaosSeaResult != null)
            return chaosSeaResult;

        var shiningResult = await ExplorerShiningAbodeCommandResultBuilder.TryBuildAsync(
            commandToken,
            _stateManager,
            _fs);
        if (shiningResult != null)
            return shiningResult;

        var afterlifeCombatResult = await ExplorerAfterlifeCombatCommandResultBuilder.TryBuildAsync(
            commandToken,
            _stateManager,
            _fs);
        if (afterlifeCombatResult != null)
            return afterlifeCombatResult;

        var lifecycleLocalTurnResult = await ExplorerLifecycleLocalTurnCommandResultBuilder.TryBuildAsync(
            commandToken,
            _stateManager,
            _fs,
            _validationService);
        if (lifecycleLocalTurnResult != null)
            return lifecycleLocalTurnResult;

        return MessageResult(
            command,
            CommandExecutionState.Failed,
            UiNotificationSeverity.Error,
            "Команда не подключена",
            "Команда помечена как перенесенная, но web command service пока не знает, как ее построить.");
    }

    private static string ExtractCommandToken(string command)
    {
        var parts = command.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? string.Empty : parts[0];
    }

    private static ExplorerCommandResult BuildBlockedMigrationResult(string command, ExplorerCommandMigrationEntry entry)
    {
        var reason = string.IsNullOrWhiteSpace(entry.Reason)
            ? "Эта команда еще не перенесена в браузерный API."
            : entry.Reason;
        var followUp = string.IsNullOrWhiteSpace(entry.FollowUpIssue)
            ? string.Empty
            : $" Следующая задача: {entry.FollowUpIssue}.";

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
