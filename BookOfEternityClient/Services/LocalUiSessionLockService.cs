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

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var snapshot = await TryReadSnapshotAsync(writeLease, now, owner.LeaseDuration);
        if (snapshot is { IsReadable: true } &&
            string.Equals(snapshot.OwnerId, owner.OwnerId, StringComparison.Ordinal))
        {
            await WriteLockAsync(writeLease, owner, operationLabel, now);
            return LocalUiSessionLockResult.AcquiredFor(snapshot with
            {
                HeartbeatAtUtc = now,
                LastOperation = operationLabel
            });
        }

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

        await WriteLockAsync(writeLease, owner, operationLabel, now);
        return LocalUiSessionLockResult.AcquiredFor(new LocalUiSessionLockSnapshot(
            owner.OwnerId,
            owner.OwnerKind,
            owner.OwnerLabel,
            now,
            now,
            owner.LeaseDuration,
            operationLabel,
            IsReadable: true,
            IsStale: false));
    }

    public async Task ReleaseAsync(LocalUiSessionLockOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        await RunCanonicalAsync(
            writeLease => ReleaseAsync(writeLease, owner));
    }

    internal async Task ReleaseAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        LocalUiSessionLockOwner owner)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(owner);

        var snapshot = await TryReadSnapshotAsync(
            writeLease,
            _timeProvider.GetUtcNow().UtcDateTime,
            owner.LeaseDuration);
        if (snapshot is { IsReadable: true } &&
            string.Equals(snapshot.OwnerId, owner.OwnerId, StringComparison.Ordinal))
        {
            _fs.DeleteFile(writeLease, LockPath);
        }
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
        return ParseSnapshot(file, nowUtc, fallbackLease);
    }

    private LocalUiSessionLockSnapshot? ParseSnapshot(
        FileSystemManager.CanonicalFileReadSnapshot? file,
        DateTime nowUtc,
        TimeSpan fallbackLease)
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
            var heartbeatText = ReadString(root, "heartbeatAtUtc");
            var acquiredText = ReadString(root, "acquiredAtUtc");
            var lastOperation = ReadString(root, "lastOperation");
            var leaseSeconds = ReadDouble(root, "leaseSeconds");
            if (string.IsNullOrWhiteSpace(ownerId) ||
                string.IsNullOrWhiteSpace(ownerKind) ||
                string.IsNullOrWhiteSpace(ownerLabel) ||
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
            var isStale = heartbeatAt.Add(lease) <= nowUtc;
            return new LocalUiSessionLockSnapshot(
                ownerId,
                ownerKind,
                ownerLabel,
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
            OwnerId: string.Empty,
            OwnerKind: "unknown",
            OwnerLabel: "повреждённый lock-файл",
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
        LocalUiSessionLockOwner owner,
        string operationLabel,
        DateTime nowUtc) =>
        _fs.WriteFileAtomicAsync(
            writeLease,
            LockPath,
            BuildLockJson(owner, operationLabel, nowUtc));

    private static string BuildLockJson(LocalUiSessionLockOwner owner, string operationLabel, DateTime nowUtc)
    {
        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["ownerId"] = owner.OwnerId,
            ["ownerKind"] = owner.OwnerKind,
            ["ownerLabel"] = owner.OwnerLabel,
            ["acquiredAtUtc"] = nowUtc.ToString("O"),
            ["heartbeatAtUtc"] = nowUtc.ToString("O"),
            ["leaseSeconds"] = owner.LeaseDuration.TotalSeconds,
            ["lastOperation"] = operationLabel
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
}

public sealed record LocalUiSessionLockOwner(
    string OwnerId,
    string OwnerKind,
    string OwnerLabel,
    TimeSpan LeaseDuration);

public sealed record LocalUiSessionLockSnapshot(
    string OwnerId,
    string OwnerKind,
    string OwnerLabel,
    DateTime AcquiredAtUtc,
    DateTime HeartbeatAtUtc,
    TimeSpan LeaseDuration,
    string LastOperation,
    bool IsReadable,
    bool IsStale);

public sealed record LocalUiSessionLockResult(
    bool Acquired,
    string BlockerMessage,
    LocalUiSessionLockSnapshot? ActiveLock)
{
    public static LocalUiSessionLockResult AcquiredFor(LocalUiSessionLockSnapshot snapshot) =>
        new(true, string.Empty, snapshot);

    public static LocalUiSessionLockResult BlockedBy(LocalUiSessionLockSnapshot? snapshot, string message) =>
        new(false, message, snapshot);
}
