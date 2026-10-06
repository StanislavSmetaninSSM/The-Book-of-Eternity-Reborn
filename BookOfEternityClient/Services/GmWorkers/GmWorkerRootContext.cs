using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services.GmWorkers;

// Kept by the weak root identity graph and by original execution leases. The
// registration is shared by all cooperating FS instances for that exact root.
internal sealed class GmWorkerRootContext
{
    private readonly CanonicalRootIdentity _root;
    private readonly WorkerRunLedgerCoordinator? _coordinator;
    private readonly WorkerLegacyFixtureOwner? _legacy;
    private readonly HashSet<GmWorkerDurableExecution> _pending = [], _cleanupDeferred = [];
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
        if (_disposed || _clients != 0 || _executions != 0 || Volatile.Read(ref _closed) != 0 || _pending.Count != 0 || _cleanupDeferred.Count != 0) return;
        if (_coordinator != null && !_coordinator.VerifyAdmission(requireQuiescent: true)) return;
        _coordinator?.DisposeAsync().AsTask().GetAwaiter().GetResult(); _legacy?.Dispose(); _disposed = true;
        // Keep the closed registration until another explicit matching admission
        // reopens the original namespace. An unbound FS cannot remove the fence.
    }
    internal void CloseForUncertainty() => Interlocked.Exchange(ref _closed, 1);
    internal void MarkCleanupDeferred(GmWorkerDurableExecution execution)
    { lock (_root.WorkerContextGate) _cleanupDeferred.Add(execution); }
    internal void ClearCleanupDeferred(GmWorkerDurableExecution execution)
    { lock (_root.WorkerContextGate) _cleanupDeferred.Remove(execution); }
    internal void MarkMetadataPending(GmWorkerDurableExecution execution)
    { lock (_root.WorkerContextGate) _pending.Add(execution); }
    internal void ClearMetadataPending(GmWorkerDurableExecution execution)
    { lock (_root.WorkerContextGate) _pending.Remove(execution); }
    internal void RequireOpen()
    {
        lock (_root.WorkerContextGate)
        {
            if (_disposed || Volatile.Read(ref _closed) != 0 || _pending.Count != 0 || _cleanupDeferred.Count != 0)
                throw new InvalidOperationException("This worker root is closed while original authority or metadata is unresolved.");
            if (_coordinator != null)
            {
                var observed = _coordinator.InspectAdmission();
                if (observed != true)
                {
                    if (observed == false) CloseForUncertainty();
                    throw new InvalidOperationException("Worker ledger authority or metadata is unresolved.");
                }
            }
            else
            {
                try { (_legacy ?? throw new InvalidOperationException("Legacy owner is unavailable.")).Verify(); }
                catch { CloseForUncertainty(); throw; }
            }
        }
    }
    internal void RequireMainQuiescence()
    {
        lock(_root.WorkerContextGate) {
            RequireOpen();
            if(_coordinator==null || !_coordinator.VerifyAdmission(requireQuiescent:true))
                throw new InvalidOperationException("Production main requires the retained original quiescent worker inventory.");
        }
    }
    internal void RequireCanonicalRoot(FileSystemManager fs)
    {
        if (!ReferenceEquals(fs.CanonicalRootAuthorityIdentity, _root) || !ReferenceEquals(_root.WorkerContext, this))
            throw new InvalidOperationException("Worker purpose belongs to another original canonical root.");
    }

    internal void ValidateBeforeRecovery()
    {
        lock (_root.WorkerContextGate)
        {
            if (_disposed || Volatile.Read(ref _closed) != 0 || _pending.Count != 0 ||
                _coordinator != null && _coordinator.InspectAdmission() != true)
                throw new InvalidOperationException("Canonical recovery is closed while any original worker authority or journal is unresolved.");
            // Known cleanup debt alone may perform its exact audit operation;
            // it never exempts another Uncertain entry or a pending journal.
            _legacy?.Verify();
        }
    }
    internal void ValidateCanonical(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease)
    {
        var purpose = lease.WorkerPurpose;
        lock (_root.WorkerContextGate)
        {
            if (_disposed) throw new InvalidOperationException("Explicit worker admission is required before canonical recovery.");
            if (purpose == null) { RequireOpen(); return; }
            if (purpose.Dispatch is { } dispatch)
            {
                if (!Durable || !ReferenceEquals(dispatch.Context, this)) throw new InvalidOperationException("Task purpose belongs to another root.");
                RequireNewTask(dispatch);
                dispatch.ValidatePurpose(purpose);
                if (fs.ReadLocalGenerationSnapshotBelowWorkerFence(lease).Binding.Id != dispatch.GenerationId)
                    throw new InvalidOperationException("Task generation changed before admission or reservation.");
                return;
            }
            if (!Durable || !ReferenceEquals(purpose.Execution.Context, this) || _coordinator == null ||
                _pending.Count != 0 || !_coordinator.VerifyAdmission())
                throw new InvalidOperationException("Original worker purpose has no current journal authority.");
            if (Volatile.Read(ref _closed) != 0 ||
                purpose.Operation != GmWorkerCanonicalOperation.ConfirmedCleanupAudit && _cleanupDeferred.Count != 0)
                throw new InvalidOperationException("Worker root closed before canonical operation.");
            purpose.Execution.ValidatePurpose(purpose);
            if (purpose.Operation != GmWorkerCanonicalOperation.ConfirmedCleanupAudit &&
                fs.ReadLocalGenerationSnapshotBelowWorkerFence(lease).Binding.Id != purpose.Execution.Identity.GenerationId)
                throw new InvalidOperationException("Worker generation changed before its canonical operation.");
        }
    }
    internal GmWorkerDispatchAdmission CreateDispatch(GmWorkerRootExecutionLease lease, WorkerTaskPacket task, byte[] bytes)
    {
        if (!ReferenceEquals(lease.Context, this) || !lease.IsActive) throw new InvalidOperationException("Original root lease is unavailable.");
        var dispatch = new GmWorkerDispatchAdmission(lease, task, bytes);
        RequireNewTask(dispatch);
        return dispatch;
    }
    private void RequireNewTask(GmWorkerDispatchAdmission dispatch)
    {
        RequireOpen();
        (_coordinator ?? throw new InvalidOperationException("Durable task admission is unavailable."))
            .RequireNewTask(dispatch.GenerationId, dispatch.WorkerId, dispatch.TaskId);
    }
    internal async Task<GmWorkerDurableExecution> PrepareAsync(GmWorkerRootExecutionLease lease,
        WorkerTaskPacket task, byte[] bytes, GmWorkerExecutionWorkspace workspace)
    {
        RequireOpen();
        var coordinator = _coordinator ?? throw new InvalidOperationException("Legacy mode has no durable execution authority.");
        var result = await coordinator.PrepareCurrentAsync(new(task.SessionGeneration, task.WorkerId, task.TaskId,
            GmWorkerRunLedgerCodec.Hash(bytes), WorkerRunBackend.LinuxNativeLineage, WorkerRunScope.OrdinarySamePidNamespace, workspace.GameSessionPath));
        if (result.Entry == null) throw new InvalidOperationException("Worker Prepared reservation was refused.");
        var execution = GmWorkerDurableExecution.RetainPrepared(coordinator, lease, result, task, bytes, workspace);
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
    internal bool IsActive => Volatile.Read(ref _released) == 0;
    internal GmWorkerRootExecutionLease(GmWorkerRootContext context) { Context = context; }
    public void Dispose() { if (!_transferred) ReleaseAfterCleanup(); }
}
