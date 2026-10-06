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
internal sealed record WorkerRunPreparation(string GenerationId, string WorkerId, string TaskId, string TaskSha256,
    WorkerRunBackend Backend, WorkerRunScope Scope, string WorkspacePath);
internal sealed class WorkerRunEntryHandle(WorkerRunIdentity identity)
{
    internal WorkerRunIdentity Identity { get; } = identity;
}
internal sealed record WorkerLedgerMutationResult(WorkerLedgerMutationKind Kind, WorkerRunEntryHandle? Entry = null);

internal static class GmWorkerRunLedger
{
    internal static Task<WorkerLedgerObservation> ObserveAsync(WorkerLedgerTarget target, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var bytes = WorkerRunLedgerPersistence.ReadSnapshot(target);
            return Task.FromResult(bytes is null ? new WorkerLedgerObservation(WorkerRunObservationKind.Missing, 0, 0, []) : ObserveState(GmWorkerRunLedgerCodec.Decode(target, bytes)));
        }
        catch (Exception error) when (Unavailable(error))
        { return Task.FromResult(new WorkerLedgerObservation(WorkerRunObservationKind.Blocked, 0, 0, [])); }
    }

    internal static Task<WorkerRunLedgerCoordinator?> OpenCoordinatorAsync(WorkerLedgerTarget target, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return WorkerRunLedgerCoordinator.OpenAsync(target);
    }

    internal static WorkerLedgerObservation ObserveState(WorkerLedgerState state) => new(
        state.Entries.Length == 0 ? WorkerRunObservationKind.Quiescent : WorkerRunObservationKind.Uncertain,
        state.Sequence, state.EpochHighWater, Array.AsReadOnly(state.Entries));

    internal static bool Unavailable(Exception error) => error is IOException or InvalidDataException or UnauthorizedAccessException or
        JsonException or ArgumentException or InvalidOperationException or FormatException or OverflowException or
        PlatformNotSupportedException or EntryPointNotFoundException or DllNotFoundException;
}

internal sealed class WorkerRunLedgerCoordinator : IAsyncDisposable
{
    private readonly WorkerRunLedgerPersistence _storage;
    private readonly WorkerLedgerTarget _target;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed, _pending;
    private WorkerLedgerState? _state;
    private byte[]? _stateBytes;
    private readonly string _hostInstance = Guid.NewGuid().ToString("N");
    private readonly Dictionary<string, WorkerRunEntryHandle> _ownedEntries = new(StringComparer.Ordinal);
    private WorkerRunLedgerCoordinator(WorkerRunLedgerPersistence storage, WorkerLedgerTarget target,
        WorkerLedgerState? state, byte[]? bytes)
    { _storage = storage; _target = target; _state = state; _stateBytes = bytes; }

    internal long Sequence => _state?.Sequence ?? 0;
    internal static Task<WorkerRunLedgerCoordinator?> OpenAsync(WorkerLedgerTarget target)
    {
        WorkerRunLedgerPersistence? storage = null;
        try
        {
            storage = WorkerRunLedgerPersistence.Open(target);
            byte[]? bytes = null; WorkerLedgerState? state = null;
            if (!storage.CreatedNamespace)
            {
                bytes = WorkerRunLedgerPersistence.ReadSnapshot(target) ?? throw WorkerRunLedgerPersistence.Invalid();
                state = GmWorkerRunLedgerCodec.Decode(target, bytes);
                // R1 cannot adopt cold active entries. This is not a pool/root dispatch policy.
                if (state.Entries.Length != 0) throw WorkerRunLedgerPersistence.Invalid();
            }
            return Task.FromResult<WorkerRunLedgerCoordinator?>(new(storage, target, state, bytes));
        }
        catch (Exception error) when (GmWorkerRunLedger.Unavailable(error))
        { storage?.Dispose(); return Task.FromResult<WorkerRunLedgerCoordinator?>(null); }
    }

    internal async Task<WorkerLedgerMutationResult> PrepareAsync(WorkerRunPreparation preparation, long expectedSequence,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_disposed || _state is null || _pending || expectedSequence != _state.Sequence)
                return new(WorkerLedgerMutationKind.Blocked);
            cancellationToken.ThrowIfCancellationRequested();
            WorkerRunRecord record; WorkerLedgerState next;
            try
            {
                record = new(1, new(_target.RootPath, checked(_state.EpochHighWater + 1), Guid.NewGuid().ToString("N"),
                    preparation.GenerationId, preparation.WorkerId, preparation.TaskId, preparation.TaskSha256,
                    preparation.Backend, preparation.Scope, _hostInstance, preparation.WorkspacePath), WorkerRunPhase.Prepared);
                next = GmWorkerRunLedgerCodec.AddPrepared(_state, record);
            }
            catch (Exception error) when (GmWorkerRunLedger.Unavailable(error)) { return new(WorkerLedgerMutationKind.Blocked); }
            try { _storage.PublishPrepared(_stateBytes!, record); }
            catch (Exception error) when (GmWorkerRunLedger.Unavailable(error))
            { _pending = true; return new(WorkerLedgerMutationKind.CommitPending); }
            _state = next; _stateBytes = GmWorkerRunLedgerCodec.Encode(next);
            var entry = new WorkerRunEntryHandle(record.Identity);
            _ownedEntries.Add(record.Identity.RunId, entry);
            return new(WorkerLedgerMutationKind.Applied, entry);
        }
        finally { _gate.Release(); }
    }

    internal async Task<WorkerLedgerMutationKind> InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_disposed) return WorkerLedgerMutationKind.Blocked;
            if (_pending) return WorkerLedgerMutationKind.CommitPending;
            if (_state is not null) return WorkerLedgerMutationKind.AlreadyExact;
            cancellationToken.ThrowIfCancellationRequested();
            var initial = GmWorkerRunLedgerCodec.Initial(_target);
            var bytes = GmWorkerRunLedgerCodec.Encode(initial);
            try { _storage.PublishInitial(bytes); _state = initial; _stateBytes = bytes; return WorkerLedgerMutationKind.Applied; }
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
