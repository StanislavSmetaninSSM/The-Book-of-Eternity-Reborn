using BookOfEternityClient.Core;

namespace BookOfEternityClient.WebUi;

public sealed class LocalWebUiSessionStatusService
{
    private readonly FileSystemManager _fs;
    private readonly TimeProvider _timeProvider;

    public LocalWebUiSessionStatusService(FileSystemManager fs, TimeProvider? timeProvider = null)
    {
        _fs = fs;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public LocalWebUiSessionStatus BuildStatus()
    {
        return new LocalWebUiSessionStatus(
            SchemaVersion: 1,
            Status: "ok",
            LocalOnly: true,
            BasePath: _fs.BasePath,
            GameSessionPath: _fs.GameSessionPath,
            GameSessionExists: Directory.Exists(_fs.GameSessionPath),
            CheckedAtUtc: _timeProvider.GetUtcNow().UtcDateTime);
    }
}

public sealed record LocalWebUiSessionStatus(
    int SchemaVersion,
    string Status,
    bool LocalOnly,
    string BasePath,
    string GameSessionPath,
    bool GameSessionExists,
    DateTime CheckedAtUtc);
