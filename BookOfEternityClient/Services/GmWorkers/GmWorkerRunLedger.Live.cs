using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services.GmWorkers;

internal sealed partial class WorkerRunLedgerCoordinator
{
    private readonly Dictionary<string, GmWorkerDurableExecution> _executions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WorkerRunEntryHandle> _liveEntries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GmWorkerDurableExecution> _preparedOwners = new(StringComparer.Ordinal);
    private GmWorkerDurableExecution.Mutation? _livePending;
    internal void ClaimOriginalExecution(GmWorkerDurableExecution execution)
    {
        _gate.Wait();
        try
        {
            if (!_liveEntries.TryGetValue(execution.Identity.RunId, out var entry) || !ReferenceEquals(entry, execution.Entry) ||
                _preparedOwners.TryGetValue(execution.Identity.RunId, out var owner) && !ReferenceEquals(owner, execution))
                throw new InvalidOperationException("Prepared entry already belongs to another original execution.");
            _preparedOwners[execution.Identity.RunId] = execution;
        }
        finally { _gate.Release(); }
    }
    internal async Task<WorkerLedgerMutationResult> RetryPreparedAsync(GmWorkerDurableExecution execution)
    {
        await _gate.WaitAsync();
        try
        {
            if (_disposed || _authorityLost || _livePending != null || _pending?.Phase != WorkerRunPhase.Prepared ||
                !ReferenceEquals(_pending.Entry, execution.Entry) ||
                !_preparedOwners.TryGetValue(execution.Identity.RunId, out var owner) || !ReferenceEquals(owner, execution))
                return new(WorkerLedgerMutationKind.Blocked);
            return Commit(_pending, _storage.RetryPending);
        }
        finally { _gate.Release(); }
    }
    internal bool VerifyAdmission(bool requireQuiescent = false, bool allowUncertain = false) =>
        InspectAdmission(requireQuiescent, allowUncertain) == true;
    // null is unresolved metadata, distinct from permanent authority loss.
    internal bool? InspectAdmission(bool requireQuiescent = false, bool allowUncertain = false)
    {
        _gate.Wait();
        try
        {
            if (_pending != null && !_authorityLost && !_disposed) return null;
            return !_disposed && _state != null && VerifyCurrent() &&
                (!requireQuiescent || _state.Entries.Length == 0) && _state.Entries.All(record =>
                    (allowUncertain || record.Phase != WorkerRunPhase.Uncertain) &&
                    _ownedEntries.TryGetValue(record.Identity.RunId, out var entry) && entry.Identity == record.Identity);
        }
        finally { _gate.Release(); }
    }
    internal void RegisterExecution(GmWorkerDurableExecution execution)
    {
        _gate.Wait();
        try
        {
            _observeLive?.Invoke(WorkerLedgerIoStage.BeforeLiveRegistration);
            if (_disposed || _pending != null || !_preparedOwners.TryGetValue(execution.Identity.RunId, out var owner) ||
                !ReferenceEquals(owner, execution) || !_ownedEntries.TryGetValue(execution.Identity.RunId, out var original) ||
                !ReferenceEquals(original, execution.Entry) || !VerifyCurrent() || !_executions.TryAdd(execution.Identity.RunId, execution))
                throw new InvalidOperationException("Only one original live execution may bind a Prepared entry.");
        }
        finally { _gate.Release(); }
    }
    internal async Task<WorkerLedgerMutationResult> PrepareCurrentAsync(WorkerRunPreparation preparation)
    {
        // The original R1 API has an explicit CAS sequence; acquire a sequence
        // optimistically and retry only the harmless pre-mutation sequence race.
        while (true)
        {
            var sequence = Sequence;
            var result = await PrepareCoreAsync(preparation, sequence, forLiveExecution: true, CancellationToken.None);
            if (result.Kind != WorkerLedgerMutationKind.Blocked || Sequence == sequence) return result;
        }
    }
    internal async Task<WorkerLedgerMutationKind> ApplyLiveAsync(GmWorkerDurableExecution execution, GmWorkerDurableExecution.Mutation mutation)
    {
        await _gate.WaitAsync();
        try
        {
            if (_disposed || _authorityLost || _state == null || !Original(execution, mutation))
                return WorkerLedgerMutationKind.Blocked;
            // A different original operation may await its exact metadata ACK.
            // Nothing for this caller has been attempted or made ambiguous.
            if (_pending != null) return WorkerLedgerMutationKind.Busy;
            if (!VerifyCurrent()) return WorkerLedgerMutationKind.Blocked;
            WorkerLedgerState next;
            try { next = GmWorkerRunLedgerCodec.TransitionLive(_state, mutation); }
            catch (Exception error) when (GmWorkerRunLedger.Unavailable(error)) { return WorkerLedgerMutationKind.Blocked; }
            if (mutation.After.Phase == WorkerRunPhase.LaunchIntent) _startConsumed.Add(execution.Identity.RunId);
            if (mutation.After.Phase == WorkerRunPhase.AbortedBeforeLaunch && _startConsumed.Contains(execution.Identity.RunId))
                return WorkerLedgerMutationKind.Blocked;
            _livePending = mutation;
            var result = Commit(new(next, GmWorkerRunLedgerCodec.Encode(next), execution.Entry, mutation.After.Phase, _state.Sequence),
                () => _storage.PublishLiveTransition(_stateBytes!, mutation)).Kind;
            if (result == WorkerLedgerMutationKind.Applied) _livePending = null;
            return result;
        }
        finally { _gate.Release(); }
    }
    internal async Task<WorkerLedgerMutationKind> RetryLiveAsync(GmWorkerDurableExecution execution, GmWorkerDurableExecution.Mutation mutation)
    {
        await _gate.WaitAsync();
        try
        {
            if (_disposed || _authorityLost || _pending == null || !ReferenceEquals(_livePending, mutation) || !Original(execution, mutation))
                return WorkerLedgerMutationKind.Blocked;
            var result = Commit(_pending, _storage.RetryPending).Kind;
            if (result == WorkerLedgerMutationKind.Applied) _livePending = null;
            return result;
        }
        finally { _gate.Release(); }
    }
    private bool Original(GmWorkerDurableExecution execution, GmWorkerDurableExecution.Mutation mutation) =>
        _executions.TryGetValue(execution.Identity.RunId, out var original) && ReferenceEquals(original, execution) && mutation.BelongsTo(execution);
}
