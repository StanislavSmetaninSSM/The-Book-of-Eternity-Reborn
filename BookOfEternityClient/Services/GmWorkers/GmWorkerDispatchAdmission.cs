using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services.GmWorkers;

// Original request capability, before capacity and before canonical recovery.
// It cannot stand in for a launched execution or a saved result permit.
internal sealed class GmWorkerDispatchAdmission : IDisposable
{
    private readonly GmWorkerRootExecutionLease _root;
    private readonly byte[] _taskBytes;
    private readonly GmWorkerCanonicalPurpose _cold, _reservation;
    private int _coldCompleted, _reservationCompleted, _disposed;
    internal string GenerationId { get; }
    internal string WorkerId { get; }
    internal string TaskId { get; }
    internal GmWorkerRootContext Context => _root.Context;
    internal GmWorkerCanonicalPurpose ColdPurpose => _cold;
    internal GmWorkerCanonicalPurpose ReservationPurpose => _reservation;

    internal GmWorkerDispatchAdmission(GmWorkerRootExecutionLease root, WorkerTaskPacket task, byte[] bytes)
    {
        _root = root; _taskBytes = bytes.ToArray();
        GenerationId = task.SessionGeneration; WorkerId = task.WorkerId; TaskId = task.TaskId;
        _cold = new(this, GmWorkerCanonicalOperation.ColdAdmission);
        _reservation = new(this, GmWorkerCanonicalOperation.TaskReservation);
    }
    internal void ValidatePurpose(GmWorkerCanonicalPurpose purpose)
    {
        if (Volatile.Read(ref _disposed) != 0 || !_root.IsActive ||
            !(ReferenceEquals(purpose, _cold) && Volatile.Read(ref _coldCompleted) == 0 ||
              ReferenceEquals(purpose, _reservation) && Volatile.Read(ref _coldCompleted) != 0 && Volatile.Read(ref _reservationCompleted) == 0))
            throw new InvalidOperationException("Original task admission purpose is not active.");
    }
    internal void CompleteColdAdmission(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease)
    {
        if (!ReferenceEquals(lease.WorkerPurpose, _cold)) throw new InvalidOperationException("Cold admission lease differs.");
        fs.EnsureCanonicalWriteLeaseActive(lease);
        Interlocked.Exchange(ref _coldCompleted, 1);
    }
    internal void ValidateReservation(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease,
        string relativePath, byte[]? before, byte[]? after)
    {
        if (!ReferenceEquals(lease.WorkerPurpose, _reservation) || lease.IsLegacyStorageRecovery || lease.MutationIntentRecorder != null ||
            relativePath != GmWorkerBridgePool.GetTaskPacketPath(TaskId) || before != null || after == null || !_taskBytes.AsSpan().SequenceEqual(after))
            throw new InvalidOperationException("Task reservation only permits its exact original create-only packet.");
        fs.EnsureCanonicalWriteLeaseActive(lease);
    }
    internal async Task CompleteReservationAsync(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease)
    {
        if (!ReferenceEquals(lease.WorkerPurpose, _reservation)) throw new InvalidOperationException("Task reservation lease differs.");
        var bytes = await fs.ReadFileBytesAsync(lease, GmWorkerBridgePool.GetTaskPacketPath(TaskId));
        ValidateReservation(fs, lease, GmWorkerBridgePool.GetTaskPacketPath(TaskId), null, bytes);
        Interlocked.Exchange(ref _reservationCompleted, 1);
    }
    public void Dispose() => Interlocked.Exchange(ref _disposed, 1);
}
