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
        var pending = BrowserPendingTurnInspector.Build(_fs);
        var lockSnapshot = await _lockService.InspectAsync(LockLease);
        var lockStatus = BrowserLocalUiLockStatus.FromSnapshot(lockSnapshot);
        var canStart = !pending.HasActiveGmTurn &&
                       (!lockStatus.Exists || lockStatus.IsStale);

        return new BrowserLocalWriteStatus(
            CanStartBrowserWrite: canStart,
            PendingTurn: pending,
            LocalUiLock: lockStatus,
            CheckedAtUtc: _timeProvider.GetUtcNow().UtcDateTime);
    }

    public async Task<BrowserLocalWriteResult> ExecuteAsync(
        BrowserLocalWriteRequest request,
        IReadOnlyCollection<string> rollbackPaths,
        Func<Task> writeOperation)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(rollbackPaths);
        ArgumentNullException.ThrowIfNull(writeOperation);

        var pending = BrowserPendingTurnInspector.Build(_fs);
        if (pending.HasActiveGmTurn)
        {
            return BrowserLocalWriteResult.Blocked(
                "Browser-write заблокирован: активный GM-turn или rollback/snapshot artifact должен быть завершён до локальной записи.");
        }

        var owner = BuildOwner(request);
        var lockResult = await _lockService.AcquireOrRefreshAsync(owner, request.OperationLabel);
        if (!lockResult.Acquired)
            return BrowserLocalWriteResult.Blocked(lockResult.BlockerMessage);

        var backups = await CaptureRollbackAsync(rollbackPaths);
        try
        {
            await writeOperation();
            await _lockService.ReleaseAsync(owner);
            return BrowserLocalWriteResult.Completed("Browser-write завершён.");
        }
        catch (Exception ex)
        {
            await RestoreRollbackAsync(backups);
            await _lockService.ReleaseAsync(owner);
            return BrowserLocalWriteResult.Failed($"Browser-write отменён, rollback восстановлен: {ex.Message}");
        }
    }

    private async Task<List<BrowserRollbackBackup>> CaptureRollbackAsync(IEnumerable<string> rollbackPaths)
    {
        var backups = new List<BrowserRollbackBackup>();
        foreach (var path in rollbackPaths.Where(static path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var normalized = path.Replace('\\', '/');
            var exists = _fs.FileExists(normalized);
            var content = exists ? await _fs.ReadFileAsync(normalized) : null;
            backups.Add(new BrowserRollbackBackup(normalized, exists, content));
        }

        return backups;
    }

    private async Task RestoreRollbackAsync(IEnumerable<BrowserRollbackBackup> backups)
    {
        foreach (var backup in backups)
        {
            if (backup.Existed)
                await _fs.WriteFileAtomicAsync(backup.Path, backup.Content ?? string.Empty);
            else if (_fs.FileExists(backup.Path))
                _fs.DeleteFile(backup.Path);
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

    private sealed record BrowserRollbackBackup(string Path, bool Existed, string? Content);
}

public sealed record BrowserLocalWriteRequest(
    string? OwnerId,
    string? OwnerLabel,
    string OperationLabel);

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
