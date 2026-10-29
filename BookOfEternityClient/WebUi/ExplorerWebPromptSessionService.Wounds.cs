using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.WebUi;

public sealed partial class ExplorerWebPromptSessionService
{
    private async Task<PromptSessionSnapshot?> GetLiveSnapshotAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || !_sessions.TryGetValue(sessionId, out var observed)) return null;
        if (observed.Result.WoundCommandState is null)
            return TryGetLiveSnapshot(sessionId, out var ordinary) ? ordinary : null;
        if (observed.ExpiresAtUtc > _timeProvider.GetUtcNow().UtcDateTime) return observed;
        // Expiry cannot remove a phase while its final queue admission owns the lease.
        try
        {
            return await _woundCoordinator.RunBoundTransactionAsync<PromptSessionSnapshot?>(async lease =>
            {
                if (_sessions.TryGetValue(sessionId, out var current) && ReferenceEquals(current, observed) &&
                    observed.ExpiresAtUtc <= _timeProvider.GetUtcNow().UtcDateTime && RemoveSnapshot(sessionId, observed))
                    await ReleaseWoundLockAsync(lease, observed);
                return null;
            });
        }
        catch (SessionReplacedException) { RemoveSnapshot(sessionId, observed); return null; }
    }

    private bool WoundSnapshotCurrent(string sessionId, PromptSessionSnapshot snapshot,
        FileSystemManager.CanonicalWriteLease lease) =>
        _sessions.TryGetValue(sessionId, out var current) && ReferenceEquals(current, snapshot) &&
        snapshot.ExpiresAtUtc > _timeProvider.GetUtcNow().UtcDateTime &&
        _fs.IsCurrentSessionGeneration(lease, snapshot.ExpectedSessionGeneration);

    private Task ReleaseWoundLockAsync(FileSystemManager.CanonicalWriteLease lease, PromptSessionSnapshot snapshot) =>
        snapshot.Owner.Lease is { } token && _fs.IsCurrentSessionGeneration(lease, snapshot.ExpectedSessionGeneration)
            ? _lockService.ReleaseAsync(lease, token) : Task.CompletedTask;

    private Task<ExplorerCommandResult> SubmitWoundAsync(string sessionId, PromptSessionSnapshot snapshot,
        IReadOnlyDictionary<string, JsonNode?> answers) =>
        _woundCoordinator.RunBoundTransactionAsync(async lease =>
        {
            if (!WoundSnapshotCurrent(sessionId, snapshot, lease)) return MissingSessionResult(sessionId);
            if (BrowserPendingTurnInspector.Build(_fs, lease).HasActiveGmTurn)
                return WoundRefusal(snapshot, "Книга занята текущим ходом. Вернитесь к форме после его завершения.");
            if (snapshot.Owner.Lease is not { } token ||
                !(await _lockService.RefreshAsync(lease, token, "Выбор лечения")).Acquired)
                return WoundRefusal(snapshot, "Эта форма потеряла право записи. Закройте её и откройте /лечить заново.");
            if (snapshot.Result.WoundCommandState is not { } state) return MissingSessionResult(sessionId);
            try
            {
                if (state.IsTargetSelection)
                {
                    var next = await WoundApplicationService.SelectTargetAsync(_fs, lease, state,
                        answers["wound_target"]?.GetValue<string>() ?? "", snapshot.Result.Command);
                    if (!WoundSnapshotCurrent(sessionId, snapshot, lease)) return MissingSessionResult(sessionId);
                    if (next.State == CommandExecutionState.RequiresInput)
                    {
                        var replacement = new PromptSessionSnapshot(snapshot.Session, next, snapshot.Owner,
                            snapshot.RequiresLocalUiLock, snapshot.ExpiresAtUtc, snapshot.ExpectedSessionGeneration);
                        if (!_sessions.TryUpdate(sessionId, replacement, snapshot)) return MissingSessionResult(sessionId);
                        return WithSession(next, snapshot.Session, next.State, next.Prompts);
                    }
                    if (RemoveSnapshot(sessionId, snapshot)) await ReleaseWoundLockAsync(lease, snapshot);
                    return next;
                }
                // The same actual lease orders final queue publication against cancellation.
                var result = await _woundWriteService.ApplyWithinTransactionAsync(lease, state, answers, snapshot.Owner);
                if (result.KeepSessionOpen)
                    return WithSession(snapshot.Result, snapshot.Session, CommandExecutionState.RequiresInput,
                        snapshot.Result.Prompts, [new UiNotification { Severity = result.Severity, Title = result.Title, Message = result.Message }]);
                var removed = RemoveSnapshot(sessionId, snapshot);
                if (removed && !result.Success && !result.ContinuationBlocked) await ReleaseWoundLockAsync(lease, snapshot);
                return BuildDomainWriteResult(snapshot, result);
            }
            catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException &&
                failure is (InvalidDataException or InvalidOperationException or KeyNotFoundException))
            {
                return WoundRefusal(snapshot, "Цель, рана или доступность лечения изменились. Закройте форму и откройте /лечить заново.");
            }
        });

    private Task<ExplorerCommandResult> CancelWoundAsync(string sessionId, PromptSessionSnapshot snapshot) =>
        _woundCoordinator.RunBoundTransactionAsync(async lease =>
        {
            if (!_sessions.TryGetValue(sessionId, out var current) || !ReferenceEquals(current, snapshot))
                return MissingSessionResult(sessionId);
            if (RemoveSnapshot(sessionId, snapshot)) await ReleaseWoundLockAsync(lease, snapshot);
            return new ExplorerCommandResult
            {
                Command = snapshot.Result.Command, State = CommandExecutionState.Completed,
                Blocks = [new UiMessageBlock { Severity = UiNotificationSeverity.Info, Title = "Форма отменена", Message = "Форма закрыта без изменений." }],
                Notifications = [new UiNotification { Severity = UiNotificationSeverity.Info, Title = "Форма отменена", Message = "Форма закрыта; действие можно выбрать заново." }]
            };
        });

    private static ExplorerCommandResult WoundRefusal(PromptSessionSnapshot snapshot, string message) =>
        WithSession(snapshot.Result, snapshot.Session, CommandExecutionState.RequiresInput, snapshot.Result.Prompts,
            [new UiNotification { Severity = UiNotificationSeverity.Error, Title = "Форма изменилась", Message = message }]);
}
