using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services.GmWorkers;

internal sealed record WorkerLedgerTarget(string RootPath)
{
    internal string DirectoryPath => Path.Combine(RootPath, ".boe_runtime", "worker-runs-v1");
}

internal enum WorkerLedgerMutationKind { Applied, AlreadyExact, Blocked, CommitPending }
internal sealed record WorkerLedgerObservation(WorkerRunObservationKind Kind, long Sequence, long EpochHighWater,
    IReadOnlyList<WorkerRunRecord> Entries);

internal static class GmWorkerRunLedger
{
    internal static Task<WorkerLedgerObservation> ObserveAsync(WorkerLedgerTarget target, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var bytes = WorkerRunLedgerPersistence.ReadSnapshot(target);
            return Task.FromResult(bytes is null ? new WorkerLedgerObservation(WorkerRunObservationKind.Missing, 0, 0, []) : ReadInitial(target, bytes));
        }
        catch (Exception error) when (Unavailable(error))
        { return Task.FromResult(new WorkerLedgerObservation(WorkerRunObservationKind.Blocked, 0, 0, [])); }
    }

    internal static Task<WorkerRunLedgerCoordinator?> OpenCoordinatorAsync(WorkerLedgerTarget target, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WorkerRunLedgerPersistence? storage = null;
        try
        {
            storage = WorkerRunLedgerPersistence.Open(target);
            if (!storage.CreatedNamespace) _ = ReadInitial(target, WorkerRunLedgerPersistence.ReadSnapshot(target)!);
            return Task.FromResult<WorkerRunLedgerCoordinator?>(new(storage, target));
        }
        catch (Exception error) when (Unavailable(error))
        { storage?.Dispose(); return Task.FromResult<WorkerRunLedgerCoordinator?>(null); }
    }

    private static WorkerLedgerObservation ReadInitial(WorkerLedgerTarget target, byte[] bytes)
    {
        using var json = JsonDocument.Parse(new UTF8Encoding(false, true).GetString(bytes), new JsonDocumentOptions { MaxDepth = 8 });
        var root = GmWorkerRunRecordCodec.Object(json.RootElement, "SchemaVersion", "RootKey", "Sequence", "EpochHighWater", "Entries", "Retired");
        if (root.GetProperty("SchemaVersion").GetInt32() != 1 || GmWorkerRunRecordCodec.String(root, "RootKey") != target.RootPath ||
            root.GetProperty("Sequence").GetInt64() != 1 || root.GetProperty("EpochHighWater").GetInt64() != 0 ||
            root.GetProperty("Entries").GetArrayLength() != 0 || root.GetProperty("Retired").GetArrayLength() != 0)
            throw WorkerRunLedgerPersistence.Invalid();
        return new(WorkerRunObservationKind.Quiescent, 1, 0, []);
    }

    internal static bool Unavailable(Exception error) => error is IOException or UnauthorizedAccessException or
        JsonException or ArgumentException or InvalidOperationException or OverflowException or PlatformNotSupportedException;
}

internal sealed class WorkerRunLedgerCoordinator : IAsyncDisposable
{
    private readonly WorkerRunLedgerPersistence _storage;
    private readonly WorkerLedgerTarget _target;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed, _pending, _initialized;
    internal WorkerRunLedgerCoordinator(WorkerRunLedgerPersistence storage, WorkerLedgerTarget target)
    { _storage = storage; _target = target; _initialized = !storage.CreatedNamespace; }

    internal async Task<WorkerLedgerMutationKind> InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_disposed) return WorkerLedgerMutationKind.Blocked;
            if (_pending) return WorkerLedgerMutationKind.CommitPending;
            if (_initialized) return WorkerLedgerMutationKind.AlreadyExact;
            cancellationToken.ThrowIfCancellationRequested();
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new { SchemaVersion = 1, RootKey = _target.RootPath,
                Sequence = 1L, EpochHighWater = 0L, Entries = Array.Empty<WorkerRunRecord>(), Retired = Array.Empty<object>() });
            try { _storage.PublishInitial(bytes); _initialized = true; return WorkerLedgerMutationKind.Applied; }
            catch (Exception error) when (GmWorkerRunLedger.Unavailable(error))
            { _pending = true; return WorkerLedgerMutationKind.CommitPending; }
        }
        finally { _gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try { if (!_disposed) { _disposed = true; _storage.Dispose(); } }
        finally { _gate.Release(); }
    }
}
