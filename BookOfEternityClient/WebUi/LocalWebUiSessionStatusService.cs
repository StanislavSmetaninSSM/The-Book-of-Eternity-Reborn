using BookOfEternityClient.Core;

namespace BookOfEternityClient.WebUi;

public sealed class LocalWebUiSessionStatusService
{
    private readonly FileSystemManager _fs;
    private readonly BrowserLocalWriteCoordinator _writeCoordinator;
    private readonly TimeProvider _timeProvider;

    public LocalWebUiSessionStatusService(
        FileSystemManager fs,
        BrowserLocalWriteCoordinator writeCoordinator,
        TimeProvider? timeProvider = null)
    {
        _fs = fs;
        _writeCoordinator = writeCoordinator;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<LocalWebUiSessionStatus> BuildStatusAsync()
    {
        var writeStatus = await _writeCoordinator.BuildStatusAsync();
        return BuildStatus(writeStatus);
    }

    internal async Task<LocalWebUiSessionStatus> BuildStatusAsync(
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        var writeStatus = await _writeCoordinator.BuildStatusAsync(writeLease);
        return BuildStatus(writeStatus);
    }

    private LocalWebUiSessionStatus BuildStatus(BrowserLocalWriteStatus writeStatus)
    {
        return new LocalWebUiSessionStatus(
            SchemaVersion: 1,
            Status: "ok",
            LocalOnly: true,
            BasePath: _fs.BasePath,
            GameSessionPath: _fs.GameSessionPath,
            GameSessionExists: Directory.Exists(_fs.GameSessionPath),
            CheckedAtUtc: _timeProvider.GetUtcNow().UtcDateTime,
            CanStartBrowserWrite: writeStatus.CanStartBrowserWrite,
            PendingTurn: writeStatus.PendingTurn,
            LocalUiLock: writeStatus.LocalUiLock);
    }
}

public sealed record LocalWebUiSessionStatus(
    int SchemaVersion,
    string Status,
    bool LocalOnly,
    string BasePath,
    string GameSessionPath,
    bool GameSessionExists,
    DateTime CheckedAtUtc,
    bool CanStartBrowserWrite,
    BrowserPendingTurnStatus PendingTurn,
    BrowserLocalUiLockStatus LocalUiLock);
