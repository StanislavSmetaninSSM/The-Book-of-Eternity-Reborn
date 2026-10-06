using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services.GmWorkers;

// Kept by the weak root identity graph and by original execution leases. The
// registration is shared by all cooperating FS instances for that exact root.
internal sealed class GmWorkerRootContext
{
    private readonly CanonicalRootIdentity _root;
    private readonly WorkerRunLedgerCoordinator? _coordinator;
    private readonly WorkerLegacyFixtureOwner? _legacy;
    private readonly HashSet<GmWorkerDurableExecution> _pending = [];
    private int _closed;
    private int _clients, _executions;
    private bool _disposed;
    internal bool Durable { get; }
    private GmWorkerRootContext(CanonicalRootIdentity root, bool durable, Action<WorkerLedgerIoStage>? observer)
    {
        _root = root; Durable = durable;
        if (durable)
        {
            _coordinator = GmWorkerRunLedger.OpenCoordinatorAsync(new(root.RootPath), observeIo: observer).GetAwaiter().GetResult();
            if (_coordinator == null || _coordinator.InitializeAsync().GetAwaiter().GetResult() is not
                (WorkerLedgerMutationKind.Applied or WorkerLedgerMutationKind.AlreadyExact)) _closed = 1;
        }
        else
        {
            _legacy = GmWorkerRunLedger.OpenLegacyFixtureOwnerAsync(new(root.RootPath)).GetAwaiter().GetResult();
            if (_legacy == null) _closed = 1;
        }
    }
    internal static GmWorkerRootContext Attach(FileSystemManager fs, bool durable, Action<WorkerLedgerIoStage>? observer)
    {
        var identity = fs.CanonicalRootAuthorityIdentity;
        lock (identity.WorkerContextGate)
        {
            var context = identity.WorkerContext;
            if (context == null || context._disposed)
                identity.WorkerContext = context = new(identity, durable, observer);
            if (context.Durable != durable) throw new InvalidOperationException("Synthetic fixture mode cannot change for this root.");
            context._clients++;
            return context;
        }
    }
    internal void ReleaseClient()
    {
        lock (_root.WorkerContextGate) { if (_clients > 0) _clients--; TryDispose(); }
    }
    internal GmWorkerRootExecutionLease Enter()
    {
        lock (_root.WorkerContextGate)
        {
            RequireOpen(); _executions++;
            return new(this);
        }
    }
    internal IDisposable PinCanonical()
    {
        lock (_root.WorkerContextGate) { _executions++; return new GmWorkerRootExecutionLease(this); }
    }
    internal void ReleaseExecution()
    {
        lock (_root.WorkerContextGate)
        {
            if (_executions <= 0) throw new InvalidOperationException("Root execution lease was already released.");
            _executions--; TryDispose();
        }
    }
    private void TryDispose()
    {
        if (_disposed || _clients != 0 || _executions != 0 || Volatile.Read(ref _closed) != 0 || _pending.Count != 0) return;
        if (_coordinator != null && !_coordinator.VerifyAdmission(requireQuiescent: true)) return;
        _coordinator?.DisposeAsync().AsTask().GetAwaiter().GetResult(); _legacy?.Dispose(); _disposed = true;
        // Keep the closed registration until another explicit matching admission
        // reopens the original namespace. An unbound FS cannot remove the fence.
    }
    internal void CloseForUncertainty() => Interlocked.Exchange(ref _closed, 1);
    internal void MarkMetadataPending(GmWorkerDurableExecution execution)
    { lock (_root.WorkerContextGate) _pending.Add(execution); }
    internal void ClearMetadataPending(GmWorkerDurableExecution execution)
    { lock (_root.WorkerContextGate) _pending.Remove(execution); }
    internal void RequireOpen()
    {
        lock (_root.WorkerContextGate)
        {
            if (_disposed || Volatile.Read(ref _closed) != 0 || _pending.Count != 0)
                throw new InvalidOperationException("This worker root is closed while original authority or metadata is unresolved.");
            if (_coordinator != null)
            {
                if (!_coordinator.VerifyAdmission()) { CloseForUncertainty(); throw new InvalidOperationException("Worker ledger authority is no longer current."); }
            }
            else
            {
                try { (_legacy ?? throw new InvalidOperationException("Legacy owner is unavailable.")).Verify(); }
                catch { CloseForUncertainty(); throw; }
            }
        }
    }
    internal void ValidateCanonical(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease)
    {
        var purpose = lease.WorkerPurpose;
        lock (_root.WorkerContextGate)
        {
            if (_disposed) throw new InvalidOperationException("Explicit worker admission is required before canonical recovery.");
            if (purpose == null) { RequireOpen(); return; }
            if (!Durable || !ReferenceEquals(purpose.Execution.Context, this) || _coordinator == null ||
                _pending.Count != 0 || !_coordinator.VerifyAdmission(allowUncertain: purpose.Operation == GmWorkerCanonicalOperation.ConfirmedCleanupAudit))
                throw new InvalidOperationException("Original worker purpose has no current journal authority.");
            if (purpose.Operation != GmWorkerCanonicalOperation.ConfirmedCleanupAudit && Volatile.Read(ref _closed) != 0)
                throw new InvalidOperationException("Worker root closed before canonical operation.");
            purpose.Execution.ValidatePurpose(purpose);
            if (purpose.Operation != GmWorkerCanonicalOperation.ConfirmedCleanupAudit &&
                fs.ReadLocalGenerationSnapshotBelowWorkerFence(lease).Binding.Id != purpose.Execution.Identity.GenerationId)
                throw new InvalidOperationException("Worker generation changed before its canonical operation.");
        }
    }
    internal async Task<GmWorkerDurableExecution> PrepareAsync(GmWorkerRootExecutionLease lease,
        WorkerTaskPacket task, byte[] bytes, string workspace)
    {
        RequireOpen();
        var coordinator = _coordinator ?? throw new InvalidOperationException("Legacy mode has no durable execution authority.");
        var result = await coordinator.PrepareCurrentAsync(new(task.SessionGeneration, task.WorkerId, task.TaskId,
            GmWorkerRunLedgerCodec.Hash(bytes), WorkerRunBackend.LinuxNativeLineage, WorkerRunScope.OrdinarySamePidNamespace, workspace));
        if (result.Entry == null) throw new InvalidOperationException("Worker Prepared reservation was refused.");
        var execution = GmWorkerDurableExecution.RetainPrepared(coordinator, lease, result, task, bytes);
        if (result.Kind == WorkerLedgerMutationKind.CommitPending) MarkMetadataPending(execution);
        return execution;
    }
}

internal sealed class GmWorkerRootExecutionLease : IDisposable
{
    private int _released;
    private bool _transferred;
    internal void RetainForCleanup() => _transferred = true;
    internal void ReleaseAfterCleanup() { if (Interlocked.Exchange(ref _released, 1) == 0) Context.ReleaseExecution(); }
    internal GmWorkerRootContext Context { get; }
    internal GmWorkerRootExecutionLease(GmWorkerRootContext context) { Context = context; }
    public void Dispose() { if (!_transferred) ReleaseAfterCleanup(); }
}
