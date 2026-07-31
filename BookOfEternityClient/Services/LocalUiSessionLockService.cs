using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public sealed class LocalUiSessionLockService
{
    public const string LockPath = "game_state/control/local_ui_session_lock.json";

    private readonly FileSystemManager _fs;
    private readonly TimeProvider _timeProvider;

    public LocalUiSessionLockService(FileSystemManager fs, TimeProvider? timeProvider = null)
    {
        _fs = fs;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<LocalUiSessionLockResult> AcquireOrRefreshAsync(
        LocalUiSessionLockOwner owner,
        string operationLabel)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return await RunCanonicalAsync(
            writeLease => AcquireOrRefreshAsync(
                writeLease,
                owner,
                operationLabel));
    }

    internal async Task<LocalUiSessionLockResult> AcquireOrRefreshAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        LocalUiSessionLockOwner owner,
        string operationLabel)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(owner);

        var sessionGeneration = _fs.GetOrCreateSessionGeneration(writeLease);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var snapshot = await TryReadSnapshotAsync(writeLease, now, owner.LeaseDuration);
        if (snapshot is { IsReadable: true, IsStale: false })
        {
            return LocalUiSessionLockResult.BlockedBy(
                snapshot,
                BuildActiveOwnerBlocker(snapshot, operationLabel));
        }

        if (snapshot is { IsReadable: false, IsStale: false })
        {
            return LocalUiSessionLockResult.BlockedBy(
                snapshot,
                $"{operationLabel} заблокировано: файл локальной UI-блокировки повреждён и ещё не устарел. " +
                $"Закройте другой интерфейс или удалите {LockPath}, если уверены, что другой UI не работает.");
        }

        if (snapshot != null)
            _fs.DeleteFile(writeLease, LockPath);

        var acquired = new LocalUiSessionLockSnapshot(
            sessionGeneration,
            owner.OwnerId,
            owner.OwnerKind,
            owner.OwnerLabel,
            Guid.NewGuid().ToString("N"),
            now,
            now,
            owner.LeaseDuration,
            operationLabel,
            IsReadable: true,
            IsStale: false);
        await WriteLockAsync(writeLease, acquired);
        return LocalUiSessionLockResult.AcquiredFor(acquired);
    }

    public async Task<LocalUiSessionLockResult> RefreshAsync(
        LocalUiSessionLockLease lease,
        string operationLabel)
    {
        ArgumentNullException.ThrowIfNull(lease);
        return await RunCanonicalAsync(
            writeLease => RefreshAsync(
                writeLease,
                lease,
                operationLabel));
    }

    internal async Task<LocalUiSessionLockResult> RefreshAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        LocalUiSessionLockLease lease,
        string operationLabel)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(lease);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var snapshot = await TryReadSnapshotAsync(
            writeLease,
            now,
            TimeSpan.FromSeconds(120));
        if (!MatchesLease(snapshot, lease) ||
            !_fs.IsCurrentSessionGeneration(writeLease, lease.SessionGeneration))
        {
            return LocalUiSessionLockResult.BlockedBy(
                snapshot,
                $"{operationLabel} заблокировано: lease локальной UI-блокировки больше не является текущим.");
        }

        var refreshed = snapshot! with
        {
            HeartbeatAtUtc = now,
            LastOperation = operationLabel
        };
        await WriteLockAsync(writeLease, refreshed);
        return LocalUiSessionLockResult.AcquiredFor(refreshed);
    }

    public async Task<bool> ReleaseAsync(LocalUiSessionLockLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        return await RunCanonicalAsync(
            writeLease => ReleaseAsync(writeLease, lease));
    }

    internal async Task<bool> ReleaseAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        LocalUiSessionLockLease lease)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(lease);

        var snapshot = await TryReadSnapshotAsync(
            writeLease,
            _timeProvider.GetUtcNow().UtcDateTime,
            TimeSpan.FromSeconds(120));
        if (!MatchesLease(snapshot, lease) ||
            !_fs.IsCurrentSessionGeneration(writeLease, lease.SessionGeneration))
        {
            return false;
        }

        _fs.DeleteFile(writeLease, LockPath);
        return true;
    }

    public async Task<LocalUiSessionLockSnapshot?> InspectAsync(
        TimeSpan? fallbackLease = null) =>
        await RunCanonicalAsync(
            writeLease => InspectAsync(
                writeLease,
                fallbackLease));

    internal Task<LocalUiSessionLockSnapshot?> InspectAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        TimeSpan? fallbackLease = null) =>
        TryReadSnapshotAsync(
            writeLease,
            _timeProvider.GetUtcNow().UtcDateTime,
            fallbackLease ?? TimeSpan.FromSeconds(120));

    private async Task<LocalUiSessionLockSnapshot?> TryReadSnapshotAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        DateTime nowUtc,
        TimeSpan fallbackLease)
    {
        var file = await _fs.ReadFileSnapshotAsync(writeLease, LockPath);
        return ParseSnapshot(
            file,
            nowUtc,
            fallbackLease,
            _fs.GetOrCreateSessionGeneration(writeLease));
    }

    private LocalUiSessionLockSnapshot? ParseSnapshot(
        FileSystemManager.CanonicalFileReadSnapshot? file,
        DateTime nowUtc,
        TimeSpan fallbackLease,
        string currentGeneration)
    {
        if (file == null)
            return null;

        try
        {
            using var stream = new MemoryStream(file.Content, writable: false);
            using var reader = new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true);
            var root = JsonNode.Parse(reader.ReadToEnd())?.AsObject();
            if (root == null)
                return MalformedSnapshot(
                    file.LastWriteTimeUtc,
                    nowUtc,
                    fallbackLease);

            var ownerId = ReadString(root, "ownerId");
            var ownerKind = ReadString(root, "ownerKind");
            var ownerLabel = ReadString(root, "ownerLabel");
            var sessionGeneration = ReadString(root, "sessionGeneration");
            var leaseToken = ReadString(root, "leaseToken");
            var heartbeatText = ReadString(root, "heartbeatAtUtc");
            var acquiredText = ReadString(root, "acquiredAtUtc");
            var lastOperation = ReadString(root, "lastOperation");
            var leaseSeconds = ReadDouble(root, "leaseSeconds");
            if (!ReadInt(root, "schemaVersion", out var schemaVersion) ||
                schemaVersion != 2 ||
                string.IsNullOrWhiteSpace(sessionGeneration) ||
                string.IsNullOrWhiteSpace(ownerId) ||
                string.IsNullOrWhiteSpace(ownerKind) ||
                string.IsNullOrWhiteSpace(ownerLabel) ||
                string.IsNullOrWhiteSpace(leaseToken) ||
                string.IsNullOrWhiteSpace(heartbeatText) ||
                leaseSeconds <= 0 ||
                !DateTime.TryParse(heartbeatText, null, System.Globalization.DateTimeStyles.RoundtripKind, out var heartbeatAt) ||
                !DateTime.TryParse(acquiredText, null, System.Globalization.DateTimeStyles.RoundtripKind, out var acquiredAt))
            {
                return MalformedSnapshot(
                    file.LastWriteTimeUtc,
                    nowUtc,
                    fallbackLease);
            }

            heartbeatAt = heartbeatAt.ToUniversalTime();
            acquiredAt = acquiredAt.ToUniversalTime();
            var lease = TimeSpan.FromSeconds(leaseSeconds);
            var isStale =
                heartbeatAt.Add(lease) <= nowUtc ||
                !string.Equals(
                    sessionGeneration,
                    currentGeneration,
                    StringComparison.Ordinal);
            return new LocalUiSessionLockSnapshot(
                sessionGeneration,
                ownerId,
                ownerKind,
                ownerLabel,
                leaseToken,
                acquiredAt,
                heartbeatAt,
                lease,
                lastOperation,
                IsReadable: true,
                IsStale: isStale);
        }
        catch
        {
            return MalformedSnapshot(
                file.LastWriteTimeUtc,
                nowUtc,
                fallbackLease);
        }
    }

    private static LocalUiSessionLockSnapshot MalformedSnapshot(
        DateTime lastWriteUtc,
        DateTime nowUtc,
        TimeSpan fallbackLease)
    {
        return new LocalUiSessionLockSnapshot(
            SessionGeneration: string.Empty,
            OwnerId: string.Empty,
            OwnerKind: "unknown",
            OwnerLabel: "повреждённый lock-файл",
            LeaseToken: string.Empty,
            AcquiredAtUtc: lastWriteUtc,
            HeartbeatAtUtc: lastWriteUtc,
            LeaseDuration: fallbackLease,
            LastOperation: string.Empty,
            IsReadable: false,
            IsStale: lastWriteUtc.Add(fallbackLease) <= nowUtc);
    }

    private async Task<T> RunCanonicalAsync<T>(
        Func<FileSystemManager.CanonicalWriteLease, Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var hasExpectedGeneration = SessionOperationContext.TryGetExpectedGeneration(
            _fs.BasePath,
            out var expectedGeneration);
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        if (!hasExpectedGeneration)
            expectedGeneration = _fs.GetOrCreateSessionGeneration(writeLease);
        else if (!_fs.IsCurrentSessionGeneration(writeLease, expectedGeneration))
            throw new SessionReplacedException(
                "The local UI lock operation belongs to a replaced session.",
                expectedGeneration,
                actualGeneration: null);

        return await SessionOperationContext.RunBoundAsync(
            _fs,
            expectedGeneration,
            writeLease,
            () => operation(writeLease));
    }

    private async Task RunCanonicalAsync(
        Func<FileSystemManager.CanonicalWriteLease, Task> operation)
    {
        await RunCanonicalAsync(
            async writeLease =>
            {
                await operation(writeLease);
                return true;
            });
    }

    private Task WriteLockAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        LocalUiSessionLockSnapshot snapshot) =>
        _fs.WriteFileAtomicAsync(
            writeLease,
            LockPath,
            BuildLockJson(snapshot));

    private static string BuildLockJson(LocalUiSessionLockSnapshot snapshot)
    {
        var root = new JsonObject
        {
            ["schemaVersion"] = 2,
            ["sessionGeneration"] = snapshot.SessionGeneration,
            ["ownerId"] = snapshot.OwnerId,
            ["ownerKind"] = snapshot.OwnerKind,
            ["ownerLabel"] = snapshot.OwnerLabel,
            ["leaseToken"] = snapshot.LeaseToken,
            ["acquiredAtUtc"] = snapshot.AcquiredAtUtc.ToString("O"),
            ["heartbeatAtUtc"] = snapshot.HeartbeatAtUtc.ToString("O"),
            ["leaseSeconds"] = snapshot.LeaseDuration.TotalSeconds,
            ["lastOperation"] = snapshot.LastOperation
        };

        return root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
    }

    private static string BuildActiveOwnerBlocker(LocalUiSessionLockSnapshot? snapshot, string operationLabel)
    {
        if (snapshot == null)
            return $"{operationLabel} заблокировано: другой интерфейс успел создать локальную UI-блокировку.";

        return $"{operationLabel} заблокировано: game_session уже удерживает {snapshot.OwnerLabel} " +
               $"({snapshot.OwnerKind}, heartbeat {snapshot.HeartbeatAtUtc:O}). " +
               $"Закройте другой интерфейс или дождитесь истечения lease {snapshot.LeaseDuration.TotalSeconds:0} сек.";
    }

    private static string ReadString(JsonObject root, string propertyName) =>
        root.TryGetPropertyValue(propertyName, out var node) && node is JsonValue value
            ? value.TryGetValue<string>(out var text) ? text : string.Empty
            : string.Empty;

    private static double ReadDouble(JsonObject root, string propertyName) =>
        root.TryGetPropertyValue(propertyName, out var node) && node is JsonValue value
            ? value.TryGetValue<double>(out var number) ? number : 0
            : 0;

    private static bool ReadInt(
        JsonObject root,
        string propertyName,
        out int number)
    {
        number = 0;
        return root.TryGetPropertyValue(propertyName, out var node) &&
               node is JsonValue value &&
               value.TryGetValue(out number);
    }

    private static bool MatchesLease(
        LocalUiSessionLockSnapshot? snapshot,
        LocalUiSessionLockLease lease) =>
        snapshot is { IsReadable: true, IsStale: false } &&
        string.Equals(
            snapshot.SessionGeneration,
            lease.SessionGeneration,
            StringComparison.Ordinal) &&
        string.Equals(snapshot.OwnerId, lease.OwnerId, StringComparison.Ordinal) &&
        string.Equals(
            snapshot.LeaseToken,
            lease.LeaseToken,
            StringComparison.Ordinal);

}

public sealed record LocalUiSessionLockOwner(
    string OwnerId,
    string OwnerKind,
    string OwnerLabel,
    TimeSpan LeaseDuration,
    LocalUiSessionLockLease? Lease = null);

public sealed record LocalUiSessionLockLease(
    string SessionGeneration,
    string OwnerId,
    string LeaseToken);

public sealed record LocalUiSessionLockSnapshot(
    string SessionGeneration,
    string OwnerId,
    string OwnerKind,
    string OwnerLabel,
    string LeaseToken,
    DateTime AcquiredAtUtc,
    DateTime HeartbeatAtUtc,
    TimeSpan LeaseDuration,
    string LastOperation,
    bool IsReadable,
    bool IsStale);

public sealed record LocalUiSessionLockResult(
    bool Acquired,
    string BlockerMessage,
    LocalUiSessionLockSnapshot? ActiveLock,
    LocalUiSessionLockLease? Lease)
{
    public static LocalUiSessionLockResult AcquiredFor(LocalUiSessionLockSnapshot snapshot) =>
        new(
            true,
            string.Empty,
            snapshot,
            new LocalUiSessionLockLease(
                snapshot.SessionGeneration,
                snapshot.OwnerId,
                snapshot.LeaseToken));

    public static LocalUiSessionLockResult BlockedBy(LocalUiSessionLockSnapshot? snapshot, string message) =>
        new(false, message, snapshot, null);
}
