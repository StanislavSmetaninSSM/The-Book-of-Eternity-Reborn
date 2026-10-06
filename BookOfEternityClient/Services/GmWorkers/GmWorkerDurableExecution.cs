using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services.GmWorkers;

// This object is born from one original Prepared operation and stays with its
// cleanup owner. No persisted field, public DTO or replacement process mints it.
internal sealed class GmWorkerDurableExecution
{
    private readonly WorkerRunLedgerCoordinator _coordinator;
    private readonly GmWorkerRootExecutionLease _root;
    private readonly WorkerTaskPacket _task;
    private readonly byte[] _taskBytes;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private WorkerRunRecord _record;
    private Mutation? _pending;
    private GmWorkerOwnedLaunch? _owner;
    private GmWorkerExecutionAuthority? _authority;
    private bool _preparedAcknowledged, _registered, _startAttempted, _releaseAttempted, _publicationAcknowledged, _retirementAcknowledged;
    private int _uncertain;
    private WorkerRunCleanup? _cleanup, _boundAudit;

    private GmWorkerDurableExecution(WorkerRunLedgerCoordinator coordinator, GmWorkerRootExecutionLease root,
        WorkerRunEntryHandle entry, WorkerTaskPacket task, byte[] taskBytes, bool prepared)
    {
        _coordinator = coordinator; _root = root; Entry = entry; _task = task; _taskBytes = taskBytes.ToArray();
        _record = new(2, entry.Identity, WorkerRunPhase.Prepared);
        _preparedAcknowledged = prepared;
    }
    internal static GmWorkerDurableExecution RetainPrepared(WorkerRunLedgerCoordinator coordinator,
        GmWorkerRootExecutionLease root, WorkerLedgerMutationResult prepared, WorkerTaskPacket task, byte[] bytes)
    {
        if (prepared.Entry is null || prepared.Kind is not (WorkerLedgerMutationKind.Applied or WorkerLedgerMutationKind.CommitPending))
            throw new InvalidOperationException("Prepared authority was not retained.");
        return new(coordinator, root, prepared.Entry, task, bytes, prepared.Kind == WorkerLedgerMutationKind.Applied);
    }
    internal WorkerRunEntryHandle Entry { get; }
    internal WorkerRunIdentity Identity => Entry.Identity;
    internal GmWorkerRootContext Context => _root.Context;
    internal bool IsUncertain => Volatile.Read(ref _uncertain) != 0;
    internal bool RetirementAcknowledged => _retirementAcknowledged;
    internal bool TerminalPlanFrozen => _cleanup != null;
    internal GmWorkerExecutionAuthority Authority => _authority ??= GmWorkerExecutionAuthority.NoLaunch(_task, _taskBytes);

    // Called under native state locks. Only two atomic stores: no I/O, journal,
    // context mutex, user hook, exception or positive evidence can enter here.
    internal void CloseForUncertainty()
    {
        Interlocked.Exchange(ref _uncertain, 1);
        Context.CloseForUncertainty();
    }

    private void Register()
    {
        _coordinator.RegisterExecution(this); _registered = true;
    }
    internal async Task EnsurePreparedAsync()
    {
        if (_registered) return;
        if (!_preparedAcknowledged)
        {
            var result = await _coordinator.RetryPendingAsync();
            if (result.Kind != WorkerLedgerMutationKind.Applied || !ReferenceEquals(result.Entry, Entry))
                throw new IOException("Prepared commit is still pending; original cleanup capacity is retained.");
            _preparedAcknowledged = true;
        }
        try { Register(); }
        catch { CloseForUncertainty(); throw; }
        Context.ClearMetadataPending(this);
    }

    internal async Task PlanLaunchAsync()
    {
        await EnsurePreparedAsync();
        if (_startAttempted || _cleanup != null) throw new InvalidOperationException("This execution cannot attempt another Start.");
        _startAttempted = true;
        await MoveAsync(WorkerRunPhase.LaunchIntent, null);
    }
    internal void ConsumeNativeStart(GmWorkerProcessHostLaunch host)
    {
        if (!_startAttempted || _record.Phase != WorkerRunPhase.LaunchIntent ||
            _owner != null || Interlocked.Exchange(ref _nativeStartConsumed, 1) != 0 ||
            !string.Equals(Path.GetFullPath(host.WorkerWorkingDirectory), Identity.WorkspacePath, StringComparison.Ordinal))
            throw new InvalidOperationException("Native Start does not match the original launch plan.");
        Context.RequireOpen();
    }
    private int _nativeStartConsumed;
    internal GmWorkerExecutionAuthority BindOwner(GmWorkerOwnedLaunch owner)
    {
        if (_owner != null || !_startAttempted || owner.Identity.RunId != Identity.RunId ||
            owner.Identity.Backend != GmWorkerBackend.NativeLineage || owner.Identity.Guarantee != GmWorkerBackendSelector.NativeGuarantee)
            throw new InvalidOperationException("Original native owner does not match Prepared identity.");
        _owner = owner;
        _authority = new(owner.Identity, _task, _taskBytes, this);
        if (IsUncertain) _authority.ObserveUncertainty("Native authority was lost before binding.");
        return _authority;
    }
    internal async Task PlanReleaseAsync(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease)
    {
        RequireBound(); fs.EnsureCanonicalWriteLeaseActive(lease);
        await MoveAsync(WorkerRunPhase.ReleaseIntent, null);
        fs.EnsureCanonicalWriteLeaseActive(lease);
    }
    internal void ConsumeRelease(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease)
    {
        RequireBound(); fs.EnsureCanonicalWriteLeaseActive(lease);
        if (_releaseAttempted || _record.Phase != WorkerRunPhase.ReleaseIntent)
            throw new InvalidOperationException("Release is not a fresh original attempt.");
        _releaseAttempted = true;
    }
    internal Task AcknowledgeReleaseAsync() => MoveAsync(WorkerRunPhase.Released, null);

    internal async Task ObserveStoppedAsync()
    {
        RequireBound(); _ = Authority.RequireCleanupEvidence();
        if (_record.Phase is WorkerRunPhase.LaunchIntent or WorkerRunPhase.ReleaseIntent or WorkerRunPhase.Released)
            await MoveAsync(WorkerRunPhase.StopValidated, null);
    }

    internal async Task BeginPublicationAsync(WorkerProposal proposal, byte[] proposalBytes,
        IReadOnlyDictionary<string, byte[]> contents)
    {
        RequireBound(); Authority.RequirePublication();
        if (_record.Phase != WorkerRunPhase.StopValidated || proposal.WorkerId != Identity.WorkerId || proposal.TaskId != Identity.TaskId)
            throw new InvalidOperationException("Publication does not match this original stopped execution.");
        var contentDigest = GmWorkerRunLedgerCodec.Hash(JsonSerializer.SerializeToUtf8Bytes(contents.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new[] { x.Key, GmWorkerRunLedgerCodec.Hash(x.Value) }).ToArray()));
        await MoveAsync(WorkerRunPhase.PublicationIntent, new(new(proposal.ProposalId,
            GmWorkerRunLedgerCodec.Hash(proposalBytes), contentDigest, false), null));
    }
    internal async Task AcknowledgePublicationAsync()
    {
        RequireBound(); Authority.RequirePublication();
        var publication = _record.Progress?.Publication ?? throw new InvalidOperationException("Publication intent is missing.");
        await MoveAsync(WorkerRunPhase.Published, new(publication with { Committed = true }, null));
        // RetryPending deliberately never sets this bit. A failed caller cannot
        // manufacture a new success response from an eventual metadata ACK.
        _publicationAcknowledged = true;
    }
    internal void RequireAcknowledgedPublication(WorkerProposal proposal, byte[] bytes)
    {
        if (!_publicationAcknowledged || IsUncertain || _record.Progress?.Publication is not { Committed: true } publication ||
            publication.ProposalId != proposal.ProposalId || publication.ProposalSha256 != GmWorkerRunLedgerCodec.Hash(bytes))
            throw new InvalidOperationException("Original caller has no acknowledged publication permit.");
    }

    internal void HoldUnresolvedPublication()
    {
        if (_record.Phase == WorkerRunPhase.PublicationIntent) Context.MarkMetadataPending(this);
    }
    internal void MarkCleanupDeferred() => Context.MarkCleanupDeferred(this);
    internal async Task RequireCleanupAuthorityAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await EnsurePreparedAsync();
            await RetryOriginalPendingAsync();
            if (IsUncertain && _record.Phase != WorkerRunPhase.Uncertain) await MoveUnderGateAsync(WorkerRunPhase.Uncertain, _record.Progress);
        }
        finally { _gate.Release(); }
        if (IsUncertain) throw new InvalidOperationException("Execution uncertainty retains its original quarantine and capacity.");
    }

    internal GmWorkerCanonicalPurpose ReleasePurpose() => new(this, GmWorkerCanonicalOperation.Release, null);
    internal GmWorkerCanonicalPurpose PublicationPurpose() => new(this, GmWorkerCanonicalOperation.ProposalPublication, null);
    internal void BindCleanupAudit(WorkerAuditEvent audit)
    {
        if (_boundAudit != null || audit.WorkerId != Identity.WorkerId || audit.TaskId != Identity.TaskId ||
            audit.EventType != "process-tree-cleanup-confirmed")
            throw new InvalidOperationException("Cleanup audit must bind once to the original task.");
        _boundAudit = AuditFacts(audit);
    }
    internal GmWorkerCanonicalPurpose CleanupPurpose(WorkerAuditEvent audit)
    {
        if (_boundAudit != AuditFacts(audit)) throw new InvalidOperationException("Cleanup audit is not the original frozen event.");
        _ = Authority.RequireCleanupEvidence();
        return new(this, GmWorkerCanonicalOperation.ConfirmedCleanupAudit, AuditFacts(audit));
    }
    internal static WorkerRunCleanup AuditFacts(WorkerAuditEvent audit) => new(true, audit.EventId,
        GmWorkerRunLedgerCodec.Hash(Encoding.UTF8.GetBytes(GmWorkerJson.Serialize(audit))));
    internal void ValidatePurpose(GmWorkerCanonicalPurpose purpose)
    {
        if (!ReferenceEquals(purpose.Execution, this) || _retirementAcknowledged || !_registered || IsUncertain)
            throw new InvalidOperationException("Original worker canonical purpose is no longer valid.");
        if (purpose.Operation == GmWorkerCanonicalOperation.ConfirmedCleanupAudit)
        {
            if (purpose.Audit == null || purpose.Audit != _boundAudit) throw new InvalidOperationException("Cleanup token belongs to another audit event.");
            _ = Authority.RequireCleanupEvidence();
        }
        else if (purpose.Operation == GmWorkerCanonicalOperation.ProposalPublication)
            Authority.RequirePublication();
        else RequireBound();
    }

    internal async Task RetireAsync(bool quarantined, WorkerAuditEvent audit)
    {
        await _gate.WaitAsync();
        try
        {
            if (_retirementAcknowledged) return;
            await EnsurePreparedAsync();
            await RetryOriginalPendingAsync();
            if (IsUncertain)
            {
                if (_record.Phase != WorkerRunPhase.Uncertain)
                    await MoveUnderGateAsync(WorkerRunPhase.Uncertain, _record.Progress);
                throw new InvalidOperationException("Execution Uncertain is absorbing; original slot and root remain retained.");
            }
            _ = Authority.RequireCleanupEvidence();
            if (_record.Phase == WorkerRunPhase.PublicationIntent)
                throw new IOException("Publication boundary is unresolved; terminal cleanup cannot accept or discard it.");
            if (_record.Phase == WorkerRunPhase.Retired || _record.Phase == WorkerRunPhase.AbortedBeforeLaunch)
            { _retirementAcknowledged = true; return; }
            if (quarantined && _boundAudit != AuditFacts(audit)) throw new InvalidOperationException("Terminal audit facts do not match original cleanup.");
            _cleanup ??= quarantined ? AuditFacts(audit) : new(false, null, null);
            var progress = new WorkerRunProgress(_record.Progress?.Publication, _cleanup);
            if (!_startAttempted)
            {
                if (_record.Phase != WorkerRunPhase.Prepared) throw new InvalidOperationException("Never-Start capability is absent.");
                if (_record.Progress != progress) await MoveUnderGateAsync(WorkerRunPhase.Prepared, progress);
                await MoveUnderGateAsync(WorkerRunPhase.AbortedBeforeLaunch, progress);
            }
            else
            {
                RequireBound();
                if (_record.Phase is WorkerRunPhase.LaunchIntent or WorkerRunPhase.ReleaseIntent or WorkerRunPhase.Released)
                    await MoveUnderGateAsync(WorkerRunPhase.StopValidated, null);
                if (_record.Phase != WorkerRunPhase.CleanupPending) await MoveUnderGateAsync(WorkerRunPhase.CleanupPending, progress);
                await MoveUnderGateAsync(WorkerRunPhase.Retired, progress);
            }
            _retirementAcknowledged = true;
        }
        finally { _gate.Release(); }
    }
    internal void ReleaseRootAfterCleanup()
    {
        if (!_retirementAcknowledged) throw new InvalidOperationException("Retirement ACK is required before releasing root capacity.");
        Context.ClearCleanupDeferred(this);
        _root.ReleaseAfterCleanup();
    }
    private void RequireBound()
    {
        if (_owner == null || _authority == null || IsUncertain || _authority.IsUncertain)
            throw new InvalidOperationException("Original bound execution authority is unavailable.");
    }
    private async Task MoveAsync(WorkerRunPhase phase, WorkerRunProgress? progress)
    {
        await _gate.WaitAsync();
        try { await MoveUnderGateAsync(phase, progress); }
        finally { _gate.Release(); }
    }
    private async Task MoveUnderGateAsync(WorkerRunPhase phase, WorkerRunProgress? progress)
    {
        if (_pending != null) throw new IOException("Original metadata plan is pending.");
        if (IsUncertain && phase != WorkerRunPhase.Uncertain) throw new InvalidOperationException("Execution authority is uncertain.");
        var mutation = new Mutation(this, _record, _record with { Phase = phase, Progress = progress });
        _pending = mutation;
        var result = await _coordinator.ApplyLiveAsync(this, mutation);
        if (result == WorkerLedgerMutationKind.Busy)
        {
            _pending = null;
            throw new IOException("Another original worker metadata plan is pending; this transition has not started.");
        }
        if (result != WorkerLedgerMutationKind.Applied)
        {
            Context.MarkMetadataPending(this);
            if (result == WorkerLedgerMutationKind.Blocked) CloseForUncertainty();
            throw new IOException("Worker metadata transition did not receive its durable ACK.");
        }
        _record = mutation.After; _pending = null;
    }
    private async Task RetryOriginalPendingAsync()
    {
        if (_pending == null) return;
        var result = await _coordinator.RetryLiveAsync(this, _pending);
        if (result != WorkerLedgerMutationKind.Applied) throw new IOException("Original worker metadata transition remains pending.");
        _record = _pending.After; _pending = null; Context.ClearMetadataPending(this);
    }

    internal sealed class Mutation
    {
        private readonly GmWorkerDurableExecution _execution;
        private readonly WorkerRunRecord _before;
        internal WorkerRunRecord After { get; }
        internal WorkerRunIdentity Identity => _before.Identity;
        internal Mutation(GmWorkerDurableExecution execution, WorkerRunRecord before, WorkerRunRecord after)
        { _execution = execution; _before = before; After = after; }
        internal bool BelongsTo(GmWorkerDurableExecution execution) => ReferenceEquals(_execution, execution) && ReferenceEquals(execution._pending, this);
        internal WorkerRunRecord ApplyTo(WorkerRunRecord current)
        {
            if (!BelongsTo(_execution) || current != _before || current.Identity != After.Identity || current.Phase == WorkerRunPhase.Uncertain ||
                !Allowed(current, After)) throw GmWorkerRunRecordCodec.Invalid();
            _ = GmWorkerRunRecordCodec.Encode(After); return After;
        }
        private static bool Allowed(WorkerRunRecord before, WorkerRunRecord after)
        {
            if (after.Phase == WorkerRunPhase.Uncertain) return after.Progress == before.Progress;
            return (before.Phase, after.Phase) switch
            {
                (WorkerRunPhase.Prepared, WorkerRunPhase.LaunchIntent) => before.Progress == null && after.Progress == null,
                (WorkerRunPhase.Prepared, WorkerRunPhase.Prepared) => before.Progress == null && after.Progress is { Publication: null, Cleanup: not null },
                (WorkerRunPhase.Prepared, WorkerRunPhase.AbortedBeforeLaunch) => before.Progress == after.Progress,
                (WorkerRunPhase.LaunchIntent, WorkerRunPhase.ReleaseIntent) or (WorkerRunPhase.ReleaseIntent, WorkerRunPhase.Released) => after.Progress == null,
                (WorkerRunPhase.LaunchIntent or WorkerRunPhase.ReleaseIntent or WorkerRunPhase.Released, WorkerRunPhase.StopValidated) => after.Progress == null,
                (WorkerRunPhase.StopValidated, WorkerRunPhase.PublicationIntent) => after.Progress is { Publication.Committed: false, Cleanup: null },
                (WorkerRunPhase.PublicationIntent, WorkerRunPhase.Published) => after.Progress?.Publication == (before.Progress!.Publication! with { Committed = true }) && after.Progress?.Cleanup == null,
                (WorkerRunPhase.StopValidated or WorkerRunPhase.Published, WorkerRunPhase.CleanupPending) => after.Progress?.Publication == before.Progress?.Publication && after.Progress?.Cleanup != null,
                (WorkerRunPhase.CleanupPending, WorkerRunPhase.Retired) => before.Progress == after.Progress && before.Progress?.Cleanup != null,
                _ => false
            };
        }
    }
    internal static bool IsConsistentTerminalCandidate(WorkerRunRecord active, WorkerRunRecord terminal) =>
        active.Identity == terminal.Identity && terminal.Progress == active.Progress &&
        (active.Phase == WorkerRunPhase.Prepared && terminal.Phase == WorkerRunPhase.AbortedBeforeLaunch ||
         active.Phase == WorkerRunPhase.CleanupPending && terminal.Phase == WorkerRunPhase.Retired);
}

internal enum GmWorkerCanonicalOperation { Release, ProposalPublication, ConfirmedCleanupAudit }
internal sealed class GmWorkerCanonicalPurpose
{
    internal GmWorkerDurableExecution Execution { get; }
    internal GmWorkerCanonicalOperation Operation { get; }
    internal WorkerRunCleanup? Audit { get; }
    internal GmWorkerCanonicalPurpose(GmWorkerDurableExecution execution, GmWorkerCanonicalOperation operation, WorkerRunCleanup? audit)
    { Execution = execution; Operation = operation; Audit = audit; }
}
