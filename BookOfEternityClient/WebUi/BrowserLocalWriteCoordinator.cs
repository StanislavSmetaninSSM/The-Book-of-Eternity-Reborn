using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.WebUi;

public sealed partial class BrowserLocalWriteCoordinator
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
        var pending = BrowserPendingTurnInspector.Build(
            _fs,
            writeLease);
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

    /// <summary>Retains load decisions and revalidates the acquired UI token on the loader's actual replacement lease.</summary>
    internal async Task<LoadReplacementResult> ExecuteSessionReplacementAsync(
        BrowserLocalWriteRequest request,
        Func<Func<FileSystemManager.CanonicalWriteLease, Task>, Task<LoadReplacementResult>> replacementOperation)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(replacementOperation);
        using var mainAdmission=_fs.BeginMainAdmission();
        LocalUiSessionLockLease? replacementGuard = null;
        LoadReplacementResult? retained = null;
        var dispatched = false;
        try
        {
            await mainAdmission.AcquireAsync(quiescentOnly: true);
            await using (var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync())
            {
                if (BrowserPendingTurnInspector.Build(_fs, writeLease).HasActiveGmTurn)
                    return new(LoadReplacementDisposition.NotLoaded, null, null, false,
                        new InvalidOperationException("Load admission refused an active turn."));
                if (_fs.ReadExistingSessionGeneration(writeLease) == null)
                    return new(LoadReplacementDisposition.NotLoaded, null, null, false,
                        new InvalidOperationException("Load admission requires existing browser session authority."));
                var acquisition = await _lockService.AcquireOrRefreshAsync(writeLease,
                    BuildOwner(request), request.OperationLabel);
                if (!acquisition.Acquired || acquisition.Lease == null)
                    return new(LoadReplacementDisposition.NotLoaded, null, null, false,
                        new InvalidOperationException("Load admission refused another UI owner."));
                replacementGuard = acquisition.Lease;
            }

            dispatched = true;
            retained = await replacementOperation(async writeLease =>
            {
                if (BrowserPendingTurnInspector.Build(_fs, writeLease).HasActiveGmTurn)
                    throw new InvalidOperationException("Load admission refused a late active turn.");
                if (!string.Equals(_fs.ReadExistingSessionGeneration(writeLease), replacementGuard.SessionGeneration, StringComparison.Ordinal))
                    throw new InvalidOperationException("Load admission no longer owns the original generation.");
                var refreshed = await _lockService.RefreshAsync(writeLease, replacementGuard, request.OperationLabel);
                if (!refreshed.Acquired)
                    throw new InvalidOperationException("Load admission no longer owns the exact UI lease.");
            });
        }
        catch (Exception failure)
        {
            retained = retained?.WithFollowUp(failure, blocksContinuation: true)
                ?? new(dispatched || failure is CoordinatedStatePublicationUncertainException
                    ? LoadReplacementDisposition.Uncertain : LoadReplacementDisposition.NotLoaded,
                    null, null, true, failure, true);
        }

        // A commit replaces the old lock; uncertainty must not trigger recovery through a release.
        // Refusal/rollback may release only the exact old token, never a newer same-owner lock.
        if (replacementGuard != null && !retained.ContinuationBlocked &&
            retained.Disposition is LoadReplacementDisposition.NotLoaded or LoadReplacementDisposition.RolledBack)
        {
            try
            {
                await using var releaseLease = await _fs.AcquireCanonicalWriteLeaseAsync();
                if (string.Equals(_fs.ReadExistingSessionGeneration(releaseLease), replacementGuard.SessionGeneration, StringComparison.Ordinal))
                    await _lockService.ReleaseAsync(releaseLease, replacementGuard);
            }
            catch (Exception failure) { retained = retained.WithFollowUp(failure, blocksContinuation: true); }
        }
        return retained;
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
                async writeLease => CaptureBrowserResult(await ExecuteAtomicCoreAsync(
                    writeLease,
                    request,
                    rollbackPaths,
                    writeOperation,
                    prepareAfterRollback,
                    rollbackCleanupDirectories,
                    rollbackExternalFileIds)));
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
            return CaptureBrowserResult(await ExecuteAtomicCoreAsync(
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

    internal Task<T> RunBoundAsync<T>(Func<Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return SessionOperationContext.RunParticipatingCurrentSessionAsync(_fs, operation);
    }

    private sealed class BrowserDecisionCapture
    {
        internal MainOperationOutcome Outcome = MainOperationOutcome.Completed;
        internal BrowserLocalWriteResult? Result;
    }
    private readonly AsyncLocal<BrowserDecisionCapture?> _browserDecision = new();
    private BrowserLocalWriteResult CaptureBrowserResult(BrowserLocalWriteResult result)
    {
        if (_browserDecision.Value is { } captured)
        {
            captured.Result = result;
            captured.Outcome = result.Disposition switch
            {
                BrowserPreparedWriteDisposition.Committed => MainOperationOutcome.Committed,
                BrowserPreparedWriteDisposition.RolledBack => MainOperationOutcome.RolledBack,
                BrowserPreparedWriteDisposition.Uncertain => MainOperationOutcome.Uncertain,
                _ => MainOperationOutcome.Failed
            };
        }
        return result;
    }

    internal async Task<T> RunBoundTransactionAsync<T>(Func<FileSystemManager.CanonicalWriteLease, Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var previous = _browserDecision.Value;
        var captured = previous ?? new BrowserDecisionCapture();
        _browserDecision.Value = captured;
        try
        {
            return await SessionOperationContext.RunParticipatingCurrentSessionAsync(_fs, async () =>
            {
                await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
                var generation = _fs.ReadExistingSessionGeneration(writeLease) ?? throw new InvalidDataException("Original transaction generation is missing.");
                return await SessionOperationContext.RunBoundAsync(_fs, generation, writeLease,
                    () => operation(writeLease), () => captured.Outcome);
            }, () => captured.Outcome);
        }
        catch (MainOperationContinuationException<T> failure) when (failure.EstablishedResult is BrowserLocalWriteResult)
        {
            return (T)(object)((BrowserLocalWriteResult)(object)failure.EstablishedResult!).WithFollowUp();
        }
        catch (SessionReplacedException) when (captured.Result != null && typeof(T) == typeof(BrowserLocalWriteResult))
        {
            return (T)(object)captured.Result.WithFollowUp();
        }
        finally { _browserDecision.Value = previous; }
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
        var lockResult = await AcquireLocalWriteGuardAsync(writeLease, request);
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
                (object?)backups.LocalTransaction ?? backups.DarenTransaction;
            await writeOperation(writeLease);
            await ExplorerLocalTurnRollbackArtifacts.MarkBrowserWriteTransactionCommittedAsync(
                _fs,
                writeLease,
                backups);
        }
        catch (SessionReplacedException)
        {
            writeLease.ExternalPublicationContext = null;
            writeLease.MutationIntentRecorder = null;
            backups?.DarenTransaction?.Dispose();
            backups?.LocalTransaction?.Access.Dispose();
            await TryReleaseAsync(writeLease, lockLease);
            throw;
        }
        catch (Exception ex)
        {
            writeLease.MutationIntentRecorder = null;
            Exception? rollbackFailure = null;
            var rollbackConfirmed = false;
            try
            {
                if (backups != null)
                {
                    try
                    {
                        await RestoreRollbackAsync(writeLease, backups);
                        rollbackConfirmed = true;
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
                writeLease.MutationIntentRecorder = null;
                backups?.DarenTransaction?.Dispose();
            backups?.LocalTransaction?.Access.Dispose();
                await TryReleaseAsync(writeLease, lockLease);
            }

            return backups == null
                ? BrowserLocalWriteResult.Failed(
                    $"Browser-write отменён до применения изменений: {ex.Message}")
                : rollbackConfirmed
                ? BrowserLocalWriteResult.Failed(
                    $"Browser-write отменён, rollback восстановлен: {ex.Message}", BrowserPreparedWriteDisposition.RolledBack)
                    with { NeedsFollowUp = rollbackFailure != null,
                        Message = rollbackFailure == null ? $"Browser-write отменён, rollback восстановлен: {ex.Message}"
                            : $"Browser-write отменён, файлы восстановлены; служебная очистка требует проверки: {ex.Message}" }
                : BrowserLocalWriteResult.Failed(
                    $"Browser-write отменён; rollback завершён не полностью: {ex.Message}; {rollbackFailure.Message}", BrowserPreparedWriteDisposition.Uncertain);
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
        writeLease.MutationIntentRecorder = null;
        backups?.DarenTransaction?.Dispose();
        backups?.LocalTransaction?.Access.Dispose();
        var released = await TryReleaseAsync(writeLease, lockLease);
        return BrowserLocalWriteResult.Completed(
            released && rollbackEvidenceCleaned
                ? "Browser-write завершён."
                : "Browser-write завершён; служебная очистка будет повторена после устранения блокирующего файлового доступа.") with { NeedsFollowUp = !released || !rollbackEvidenceCleaned };
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
        var kind = string.IsNullOrWhiteSpace(request.OwnerKind) ? "browser" : request.OwnerKind.Trim();
        var ownerId = string.IsNullOrWhiteSpace(request.OwnerId)
            ? $"{kind}:{Environment.MachineName}:{Environment.ProcessId}"
            : request.OwnerId.Trim();
        var label = string.IsNullOrWhiteSpace(request.OwnerLabel)
            ? $"Local Browser UI PID {Environment.ProcessId}"
            : request.OwnerLabel.Trim();
        return new LocalUiSessionLockOwner(ownerId, kind, label, LockLease);
    }

}

public sealed record BrowserLocalWriteRequest(
    string? OwnerId,
    string? OwnerLabel,
    string OperationLabel,
    LocalUiSessionLockLease? ExistingLease = null,
    string OwnerKind = "browser");

public sealed record BrowserLocalWriteResult(
    bool Success,
    bool IsBlocked,
    string Message)
{
    public static BrowserLocalWriteResult Completed(string message) => new(true, false, message);

    public static BrowserLocalWriteResult Blocked(string message) => new(false, true, message);

    public BrowserPreparedWriteDisposition Disposition { get; init; } = Success
        ? BrowserPreparedWriteDisposition.Committed : BrowserPreparedWriteDisposition.Blocked;
    public bool NeedsFollowUp { get; init; }
    public bool ContinuationBlocked { get; init; }

    public static BrowserLocalWriteResult Failed(string message,
        BrowserPreparedWriteDisposition disposition = BrowserPreparedWriteDisposition.Blocked) => new(false, false, message)
        { Disposition = disposition, NeedsFollowUp = disposition == BrowserPreparedWriteDisposition.Uncertain,
          ContinuationBlocked = disposition == BrowserPreparedWriteDisposition.Uncertain };

    internal BrowserLocalWriteResult WithFollowUp() => this with
    {
        NeedsFollowUp = true, ContinuationBlocked = true,
        Message = Message + " Продолжение не подтверждено; требуется проверка текущего состояния. Не повторяйте неизвестную операцию."
    };
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
