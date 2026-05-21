using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.UI;

namespace BookOfEternityClient.WebUi;

public sealed record ExplorerWebCommandRequest(string Command);

public sealed class ExplorerWebCommandService
{
    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly LocalizationManager _localization;

    public ExplorerWebCommandService(FileSystemManager fs, StateManager stateManager, LocalizationManager localization)
    {
        _fs = fs;
        _stateManager = stateManager;
        _localization = localization;
    }

    public async Task<ExplorerCommandResult> ExecuteAsync(ExplorerWebCommandRequest? request)
    {
        var command = request?.Command?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(command))
            return MessageResult(command, CommandExecutionState.Failed, UiNotificationSeverity.Error, "Команда не выполнена", "Команда пустая.");

        var entry = ExplorerCommandMigrationRegistry.Entries
            .FirstOrDefault(item => string.Equals(item.Command, command, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
            return MessageResult(command, CommandExecutionState.Failed, UiNotificationSeverity.Error, "Команда не найдена", "Команда не зарегистрирована в ExplorerMode.");

        if (entry.Status != ExplorerCommandMigrationStatus.Migrated)
            return BuildBlockedMigrationResult(command, entry);

        return await BuildMigratedResultAsync(command);
    }

    private async Task<ExplorerCommandResult> BuildMigratedResultAsync(string command)
    {
        await _stateManager.RefreshGameStateAsync();
        if (string.Equals(command, "/help", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command, "/помощь", StringComparison.OrdinalIgnoreCase))
        {
            var state = _stateManager.CurrentState;
            return ExplorerHelpCommandResultBuilder.Build(new ExplorerHelpCommandContext
            {
                Command = command,
                Title = _localization.T("help"),
                IsChaosSea = state.IsInChaosSea,
                IsShiningAbode = state.IsInShiningAbode,
                IsPendingShiningAbodeBootstrap = state.IsInShiningAbodePendingBootstrap,
                CanReenterShiningAbode = state.CanReenterShiningAbode
            });
        }

        var universalResult = await ExplorerUniversalMetaCommandResultBuilder.TryBuildAsync(
            command,
            _stateManager,
            _fs,
            _localization);
        if (universalResult != null)
            return universalResult;

        var mortalResult = await ExplorerMortalWorldCommandResultBuilder.TryBuildAsync(
            command,
            _stateManager,
            _fs);
        if (mortalResult != null)
            return mortalResult;

        return MessageResult(
            command,
            CommandExecutionState.Failed,
            UiNotificationSeverity.Error,
            "Команда не подключена",
            "Команда помечена как перенесенная, но web command service пока не знает, как ее построить.");
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
