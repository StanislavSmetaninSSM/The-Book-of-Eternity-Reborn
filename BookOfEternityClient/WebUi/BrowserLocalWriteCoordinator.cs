using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.WebUi;

public sealed class BrowserLocalWriteCoordinator
{
    private static readonly TimeSpan LockLease = TimeSpan.FromSeconds(120);

    private readonly FileSystemManager _fs;
    private readonly LocalUiSessionLockService _lockService;
    private readonly TimeProvider _timeProvider;

    public BrowserLocalWriteCoordinator(
        FileSystemManager fs,
        LocalUiSessionLockService lockService,
        TimeProvider? timeProvider = null)
    {
        _fs = fs;
        _lockService = lockService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<BrowserLocalWriteStatus> BuildStatusAsync()
    {
        await using var recoveryLease =
            await _fs.AcquireCanonicalWriteLeaseAsync();
        // Lease acquisition performs fail-closed recovery of interrupted browser writes.
        return await BuildStatusCoreAsync(recoveryLease);
    }

    internal async Task<BrowserLocalWriteStatus> BuildStatusAsync(
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        _fs.VerifyCurrentSessionOperation(writeLease);
        return await BuildStatusCoreAsync(writeLease);
    }

    private async Task<BrowserLocalWriteStatus> BuildStatusCoreAsync(
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        var pending = BrowserPendingTurnInspector.Build(_fs);
        var lockSnapshot = await _lockService.InspectAsync(
            writeLease,
            LockLease);
        var lockStatus = BrowserLocalUiLockStatus.FromSnapshot(lockSnapshot);
        var canStart = !pending.HasActiveGmTurn &&
                       (!lockStatus.Exists || lockStatus.IsStale);

        return new BrowserLocalWriteStatus(
            CanStartBrowserWrite: canStart,
            PendingTurn: pending,
            LocalUiLock: lockStatus,
            CheckedAtUtc: _timeProvider.GetUtcNow().UtcDateTime);
    }

    internal async Task<BrowserLocalWriteResult> ExecuteAsync(
        BrowserLocalWriteRequest request,
        IReadOnlyCollection<string> rollbackPaths,
        Func<FileSystemManager.CanonicalWriteLease, Task> writeOperation)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(rollbackPaths);
        ArgumentNullException.ThrowIfNull(writeOperation);

        return await ExecuteAtomicAsync(
            request,
            rollbackPaths,
            writeOperation);
    }

    internal async Task<BrowserLocalWriteResult> ExecuteSessionReplacementAsync(
        BrowserLocalWriteRequest request,
        Func<Task> replacementOperation)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(replacementOperation);

        LocalUiSessionLockLease replacementGuard;
        await using (var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            var generation = _fs.GetOrCreateSessionGeneration(writeLease);
            var acquisition = await SessionOperationContext.RunBoundAsync(
                _fs,
                generation,
                writeLease,
                async () =>
                {
                    var pending = BrowserPendingTurnInspector.Build(_fs);
                    if (pending.HasActiveGmTurn)
                    {
                        return LocalUiSessionLockResult.BlockedBy(
                            snapshot: null,
                            "Browser-write заблокирован: активный GM-turn или rollback/snapshot artifact должен быть завершён до локальной записи.");
                    }

                    return await _lockService.AcquireOrRefreshAsync(
                        writeLease,
                        BuildOwner(request),
                        request.OperationLabel);
                });
            if (!acquisition.Acquired || acquisition.Lease == null)
                return BrowserLocalWriteResult.Blocked(acquisition.BlockerMessage);

            replacementGuard = acquisition.Lease;
        }

        try
        {
            await replacementOperation();
            return BrowserLocalWriteResult.Completed("Browser-write завершён.");
        }
        catch (Exception ex)
        {
            await TryReleaseAsync(replacementGuard);
            return BrowserLocalWriteResult.Failed(
                $"Browser-write отменён до завершения замены сессии: {ex.Message}");
        }
    }

    internal async Task<BrowserLocalWriteResult> ExecuteAtomicAsync(
        BrowserLocalWriteRequest request,
        IReadOnlyCollection<string> rollbackPaths,
        Func<FileSystemManager.CanonicalWriteLease, Task> writeOperation,
        Func<Action?>? prepareAfterRollback = null,
        IReadOnlyCollection<string>? rollbackCleanupDirectories = null,
        IReadOnlyCollection<string>? rollbackExternalFileIds = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(rollbackPaths);
        ArgumentNullException.ThrowIfNull(writeOperation);

        try
        {
            return await RunBoundTransactionAsync(
                writeLease => ExecuteAtomicCoreAsync(
                    writeLease,
                    request,
                    rollbackPaths,
                    writeOperation,
                    prepareAfterRollback,
                    rollbackCleanupDirectories,
                    rollbackExternalFileIds));
        }
        catch (SessionReplacedException)
        {
            return BrowserLocalWriteResult.Failed(
                "Игровая сессия была заменена до завершения транзакции. Изменения старой сессии не применены.");
        }
    }

    internal async Task<BrowserLocalWriteResult> ExecuteAtomicWithinTransactionAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        BrowserLocalWriteRequest request,
        IReadOnlyCollection<string> rollbackPaths,
        Func<FileSystemManager.CanonicalWriteLease, Task> writeOperation,
        Func<Action?>? prepareAfterRollback = null,
        IReadOnlyCollection<string>? rollbackCleanupDirectories = null,
        IReadOnlyCollection<string>? rollbackExternalFileIds = null)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(rollbackPaths);
        ArgumentNullException.ThrowIfNull(writeOperation);

        try
        {
            return await ExecuteAtomicCoreAsync(
                writeLease,
                request,
                rollbackPaths,
                writeOperation,
                prepareAfterRollback,
                rollbackCleanupDirectories,
                rollbackExternalFileIds);
        }
        catch (SessionReplacedException)
        {
            return BrowserLocalWriteResult.Failed(
                "Игровая сессия была заменена до завершения транзакции. Изменения старой сессии не применены.");
        }
    }

    internal async Task<T> RunBoundAsync<T>(Func<Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        string generation;
        if (!SessionOperationContext.TryGetExpectedGeneration(_fs.BasePath, out generation))
        {
            await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
            generation = _fs.GetOrCreateSessionGeneration(writeLease);
        }

        return await SessionOperationContext.RunBoundAsync(_fs, generation, operation);
    }

    internal async Task<T> RunBoundTransactionAsync<T>(
        Func<FileSystemManager.CanonicalWriteLease, Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (!SessionOperationContext.TryGetExpectedGeneration(_fs.BasePath, out var generation))
        {
            await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
            generation = _fs.GetOrCreateSessionGeneration(writeLease);
            return await SessionOperationContext.RunBoundAsync(
                _fs,
                generation,
                writeLease,
                () => operation(writeLease));
        }

        return await SessionOperationContext.RunBoundAsync(
            _fs,
            generation,
            async () =>
            {
                await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
                return await operation(writeLease);
            });
    }

    internal async Task RunBoundTransactionAsync(
        Func<FileSystemManager.CanonicalWriteLease, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await RunBoundTransactionAsync(
            async writeLease =>
            {
                await operation(writeLease);
                return true;
            });
    }

    private async Task<BrowserLocalWriteResult> ExecuteAtomicCoreAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        BrowserLocalWriteRequest request,
        IReadOnlyCollection<string> rollbackPaths,
        Func<FileSystemManager.CanonicalWriteLease, Task> writeOperation,
        Func<Action?>? prepareAfterRollback,
        IReadOnlyCollection<string>? rollbackCleanupDirectories,
        IReadOnlyCollection<string>? rollbackExternalFileIds)
    {
        var pending = BrowserPendingTurnInspector.Build(_fs);
        if (pending.HasActiveGmTurn)
        {
            return BrowserLocalWriteResult.Blocked(
                "Browser-write заблокирован: активный GM-turn или rollback/snapshot artifact должен быть завершён до локальной записи.");
        }

        var owner = BuildOwner(request);
        var lockResult = request.ExistingLease == null
            ? await _lockService.AcquireOrRefreshAsync(
                writeLease,
                owner,
                request.OperationLabel)
            : await _lockService.RefreshAsync(
                writeLease,
                request.ExistingLease,
                request.OperationLabel);
        if (!lockResult.Acquired || lockResult.Lease == null)
            return BrowserLocalWriteResult.Blocked(lockResult.BlockerMessage);
        var lockLease = lockResult.Lease;

        Action? afterRollback = null;
        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction? backups = null;
        try
        {
            afterRollback = prepareAfterRollback?.Invoke();
            backups = await CaptureRollbackAsync(
                writeLease,
                rollbackPaths,
                rollbackCleanupDirectories,
                rollbackExternalFileIds);
            writeLease.ExternalPublicationContext =
                backups.DarenTransaction;
            await writeOperation(writeLease);
            await ExplorerLocalTurnRollbackArtifacts.MarkBrowserWriteTransactionCommittedAsync(
                _fs,
                writeLease,
                backups);
        }
        catch (SessionReplacedException)
        {
            writeLease.ExternalPublicationContext = null;
            backups?.DarenTransaction?.Dispose();
            await TryReleaseAsync(writeLease, lockLease);
            throw;
        }
        catch (Exception ex)
        {
            Exception? rollbackFailure = null;
            try
            {
                if (backups != null)
                {
                    try
                    {
                        await RestoreRollbackAsync(writeLease, backups);
                        if (!ExplorerLocalTurnRollbackArtifacts.TryDeleteBrowserWriteTransaction(
                                _fs,
                                writeLease,
                                backups,
                                ExplorerLocalTurnRollbackArtifacts
                                    .BrowserWriteCleanupOutcome.Restored,
                                out var cleanupFailure))
                        {
                            throw new IOException(
                                "Canonical files were restored, but durable browser rollback evidence could not be cleaned.",
                                cleanupFailure);
                        }
                    }
                    catch (Exception restoreEx)
                    {
                        rollbackFailure = restoreEx;
                    }

                    try
                    {
                        afterRollback?.Invoke();
                    }
                    catch (Exception runtimeRestoreEx)
                    {
                        rollbackFailure = rollbackFailure == null
                            ? runtimeRestoreEx
                            : new AggregateException(rollbackFailure, runtimeRestoreEx);
                    }
                }
            }
            finally
            {
                writeLease.ExternalPublicationContext = null;
                backups?.DarenTransaction?.Dispose();
                await TryReleaseAsync(writeLease, lockLease);
            }

            return backups == null
                ? BrowserLocalWriteResult.Failed(
                    $"Browser-write отменён до применения изменений: {ex.Message}")
                : rollbackFailure == null
                ? BrowserLocalWriteResult.Failed(
                    $"Browser-write отменён, rollback восстановлен: {ex.Message}")
                : BrowserLocalWriteResult.Failed(
                    $"Browser-write отменён; rollback завершён не полностью: {ex.Message}; {rollbackFailure.Message}");
        }

        var rollbackEvidenceCleaned = backups == null ||
                                      ExplorerLocalTurnRollbackArtifacts.TryDeleteBrowserWriteTransaction(
                                          _fs,
                                          writeLease,
                                          backups,
                                          ExplorerLocalTurnRollbackArtifacts
                                              .BrowserWriteCleanupOutcome.Committed,
                                          out _);
        writeLease.ExternalPublicationContext = null;
        backups?.DarenTransaction?.Dispose();
        var released = await TryReleaseAsync(writeLease, lockLease);
        return BrowserLocalWriteResult.Completed(
            released && rollbackEvidenceCleaned
                ? "Browser-write завершён."
                : "Browser-write завершён; служебная очистка будет повторена после устранения блокирующего файлового доступа.");
    }

    private async Task<bool> TryReleaseAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        LocalUiSessionLockLease lease)
    {
        try
        {
            return await _lockService.ReleaseAsync(writeLease, lease);
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> TryReleaseAsync(LocalUiSessionLockLease lease)
    {
        try
        {
            return await _lockService.ReleaseAsync(lease);
        }
        catch
        {
            return false;
        }
    }

    private async Task<ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction> CaptureRollbackAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        IEnumerable<string> rollbackPaths,
        IEnumerable<string>? rollbackCleanupDirectories,
        IEnumerable<string>? rollbackExternalFileIds) =>
        await ExplorerLocalTurnRollbackArtifacts.StageBrowserWriteTransactionAsync(
            _fs,
            writeLease,
            rollbackPaths,
            "browser_write",
            rollbackCleanupDirectories,
            rollbackExternalFileIds);

    private async Task RestoreRollbackAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction transaction) =>
        await ExplorerLocalTurnRollbackArtifacts.RestoreBrowserWriteTransactionAsync(
            _fs,
            writeLease,
            transaction);

    private static void ThrowIfRollbackRestoreFailed(IReadOnlyCollection<Exception> failures)
    {
        if (failures.Count > 0)
        {
            throw new AggregateException(
                "Не удалось полностью восстановить browser-write transaction.",
                failures);
        }
    }

    private static LocalUiSessionLockOwner BuildOwner(BrowserLocalWriteRequest request)
    {
        var ownerId = string.IsNullOrWhiteSpace(request.OwnerId)
            ? $"browser:{Environment.MachineName}:{Environment.ProcessId}"
            : request.OwnerId.Trim();
        var label = string.IsNullOrWhiteSpace(request.OwnerLabel)
            ? $"Local Browser UI PID {Environment.ProcessId}"
            : request.OwnerLabel.Trim();
        return new LocalUiSessionLockOwner(ownerId, "browser", label, LockLease);
    }

}

public sealed record BrowserLocalWriteRequest(
    string? OwnerId,
    string? OwnerLabel,
    string OperationLabel,
    LocalUiSessionLockLease? ExistingLease = null);

public sealed record BrowserLocalWriteResult(
    bool Success,
    bool IsBlocked,
    string Message)
{
    public static BrowserLocalWriteResult Completed(string message) => new(true, false, message);

    public static BrowserLocalWriteResult Blocked(string message) => new(false, true, message);

    public static BrowserLocalWriteResult Failed(string message) => new(false, false, message);
}

public sealed record BrowserLocalWriteStatus(
    bool CanStartBrowserWrite,
    BrowserPendingTurnStatus PendingTurn,
    BrowserLocalUiLockStatus LocalUiLock,
    DateTime CheckedAtUtc);

public sealed record BrowserLocalUiLockStatus(
    bool Exists,
    bool IsReadable,
    bool IsStale,
    string OwnerId,
    string OwnerKind,
    string OwnerLabel,
    DateTime? AcquiredAtUtc,
    DateTime? HeartbeatAtUtc,
    double LeaseSeconds,
    string LastOperation)
{
    public static BrowserLocalUiLockStatus FromSnapshot(LocalUiSessionLockSnapshot? snapshot)
    {
        if (snapshot == null)
        {
            return new BrowserLocalUiLockStatus(
                Exists: false,
                IsReadable: false,
                IsStale: false,
                OwnerId: string.Empty,
                OwnerKind: string.Empty,
                OwnerLabel: string.Empty,
                AcquiredAtUtc: null,
                HeartbeatAtUtc: null,
                LeaseSeconds: 0,
                LastOperation: string.Empty);
        }

        return new BrowserLocalUiLockStatus(
            Exists: true,
            IsReadable: snapshot.IsReadable,
            IsStale: snapshot.IsStale,
            OwnerId: snapshot.OwnerId,
            OwnerKind: snapshot.OwnerKind,
            OwnerLabel: snapshot.OwnerLabel,
            AcquiredAtUtc: snapshot.AcquiredAtUtc,
            HeartbeatAtUtc: snapshot.HeartbeatAtUtc,
            LeaseSeconds: snapshot.LeaseDuration.TotalSeconds,
            LastOperation: snapshot.LastOperation);
    }
}
