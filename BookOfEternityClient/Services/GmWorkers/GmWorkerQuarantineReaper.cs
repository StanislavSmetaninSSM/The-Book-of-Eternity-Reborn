using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

namespace BookOfEternityClient.Services.GmWorkers;

internal interface IGmWorkerQuarantineOwner
{
    string Identity { get; }
    Task<GmWorkerCleanupEvidence> ConfirmDeathAsync();
    Task CleanupConfirmedAsync();
    Task RecordReaperFailureAsync(Exception failure);
}

// The same phase state handles ordinary cleanup and is transferred intact when
// any remaining stop/output/disposal/filesystem/receipt operation fails.
internal sealed class GmWorkerQuarantinedExecution : IGmWorkerQuarantineOwner
{
    private readonly SemaphoreSlim _confirmationGate = new(1, 1);
    private readonly SemaphoreSlim _cleanupGate = new(1, 1);
    private readonly GmWorkerExecutionAuthority _authority;
    private readonly GmWorkerDurableExecution? _durable;
    private readonly GmWorkerRootExecutionLease? _rootLease;
    private readonly Action? _afterRetirementAcknowledged;
    private readonly Func<string, Task>? _beforeWorkspaceCleanupAsync;
    private readonly Func<Task<GmWorkerAuditAppendDisposition>> _recordCleanupConfirmedAsync;
    private readonly Func<Exception, Task> _recordFailureAsync;
    private readonly string _sessionGeneration;
    private readonly WorkerAuditEvent _cleanupConfirmedAuditEvent;
    private GmWorkerOwnedLaunch? _owner;
    private GmWorkerProcessHostLaunch? _processHostLaunch;
    private GmWorkerExecutionWorkspace? _workspace;
    private readonly GmWorkerExecutionWorkspace? _originalWorkspace;
    private CleanupCompletion? _completion;
    private bool _workspaceDeletionCompleted;
    private IDisposable? _workerSlot;
    private Task<int>? _workerCompletionTask;
    private int _cleanupCompleted;
    private bool _workspaceHookCompleted, _terminalAuditRecorded, _quarantined;

    internal GmWorkerQuarantinedExecution(
        string identity, GmWorkerExecutionAuthority authority, GmWorkerOwnedLaunch? owner,
        GmWorkerProcessHostLaunch? processHostLaunch, GmWorkerExecutionWorkspace? workspace,
        IDisposable workerSlot, Task<int>? workerCompletionTask,
        Func<string, Task>? beforeWorkspaceCleanupAsync, string sessionGeneration,
        WorkerAuditEvent cleanupConfirmedAuditEvent,
        Func<Task<GmWorkerAuditAppendDisposition>> recordCleanupConfirmedAsync,
        Func<Exception, Task> recordFailureAsync, bool quarantined = true,
        GmWorkerDurableExecution? durable = null, GmWorkerRootExecutionLease? rootLease = null,
        Action? afterRetirementAcknowledged = null)
    {
        Identity = identity; _authority = authority; _owner = owner;
        _processHostLaunch = processHostLaunch; _workspace = workspace; _workerSlot = workerSlot;
        _workerCompletionTask = workerCompletionTask; _beforeWorkspaceCleanupAsync = beforeWorkspaceCleanupAsync;
        _sessionGeneration = sessionGeneration; _cleanupConfirmedAuditEvent = cleanupConfirmedAuditEvent;
        _recordCleanupConfirmedAsync = recordCleanupConfirmedAsync; _recordFailureAsync = recordFailureAsync;
        _quarantined = quarantined; _durable = durable; _rootLease = rootLease;
        _originalWorkspace = workspace; _afterRetirementAcknowledged = afterRetirementAcknowledged;
        durable?.BindCleanupOwner(this, workspace, authority);
    }

    public string Identity { get; }
    internal void RetainForRetry()
    {
        _durable?.MarkCleanupDeferred();
        if (_completion == null && _durable?.TerminalPlanFrozen != true) _quarantined = true;
    }

    public async Task<GmWorkerCleanupEvidence> ConfirmDeathAsync()
    {
        await _confirmationGate.WaitAsync();
        try
        {
            GmWorkerCleanupEvidence? evidence = null;
            Exception? stopFailure = null;
            try { evidence = await _authority.StopForCleanupAsync(_owner); }
            catch (Exception failure) { stopFailure = failure; }
            // Even sticky uncertainty permits an original bounded stop attempt.
            // Metadata validation/persistence still runs when that attempt fails.
            try { if (_durable != null) await _durable.RequireCleanupAuthorityAsync(); }
            catch (Exception metadataFailure)
            {
                if (stopFailure != null) throw new AggregateException(stopFailure, metadataFailure);
                throw;
            }
            if (stopFailure != null) ExceptionDispatchInfo.Capture(stopFailure).Throw();
            if (evidence == null) throw new InvalidOperationException("Original stop did not return evidence.");
            if (!evidence.NoLaunch && !_authority.OutputsSettled)
                await _authority.SettleOutputsAsync(_owner ?? throw new InvalidOperationException("Original output owner is missing."));
            return _authority.RequireCleanupEvidence();
        }
        finally { _confirmationGate.Release(); }
    }

    public async Task CleanupConfirmedAsync()
    {
        if (Volatile.Read(ref _cleanupCompleted) != 0) return;
        await _cleanupGate.WaitAsync();
        try
        {
            if (Volatile.Read(ref _cleanupCompleted) != 0) return;
            if (_durable != null) await _durable.RequireCleanupAuthorityAsync();
            _ = _authority.RequireCleanupEvidence();
            if (_owner != null) { await _owner.DisposeAsync(); _owner = null; }

            // The pool canceled this original waiter. Observe its actual settlement
            // before disposing the named-channel gates it may still be using.
            if (_workerCompletionTask != null)
            {
                try { await _workerCompletionTask.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch when (_workerCompletionTask.IsCompleted) { }
                _workerCompletionTask = null;
            }
            if (_processHostLaunch != null)
            {
                await _processHostLaunch.DisposeAsync();
                _processHostLaunch = null;
            }
            if (_workspace != null && !_workspaceHookCompleted)
            {
                if (_beforeWorkspaceCleanupAsync != null) await _beforeWorkspaceCleanupAsync(_workspace.GameSessionPath);
                _workspaceHookCompleted = true;
            }
            if (_workspace != null)
            {
                // The awaited hook cannot carry earlier authority into deletion.
                if (_durable != null) await _durable.RequireCleanupAuthorityAsync();
                _ = _authority.RequireCleanupEvidence();
                await _workspace.DeleteDetachedSessionRetainingRuntimeAuthorityAsync();
            }
            _workspaceDeletionCompleted = true;
            if (_quarantined && !_terminalAuditRecorded)
            {
                var disposition = await _recordCleanupConfirmedAsync();
                if (disposition != GmWorkerAuditAppendDisposition.Appended)
                {
                    if (_workspace == null) throw new InvalidOperationException("Quarantine terminal audit fallback requires retained workspace authority.");
                    await _workspace.PersistQuarantineAuditReceiptAsync(_sessionGeneration, _cleanupConfirmedAuditEvent);
                }
                _terminalAuditRecorded = true;
            }
            if (_durable != null)
            {
                _completion ??= new(this);
                await _durable.RetireAsync(_completion);
                // Negative/observation only: actual original retirement has returned.
                _afterRetirementAcknowledged?.Invoke();
            }
            if (_workspace != null) { await _workspace.DisposeAsync(); _workspace = null; }
            if (_durable != null) _durable.ReleaseRootAfterCleanup(); else _rootLease?.ReleaseAfterCleanup();
            _workerSlot?.Dispose(); _workerSlot = null;
            Volatile.Write(ref _cleanupCompleted, 1);
        }
        finally { _cleanupGate.Release(); }
    }

    public Task RecordReaperFailureAsync(Exception failure) => _recordFailureAsync(failure);

    // Constructor visibility does not grant authority: the completion must be
    // this original owner's one private, phase-checked retained instance.
    internal sealed class CleanupCompletion
    {
        private readonly GmWorkerQuarantinedExecution _owner;
        internal WorkerRunCleanup Facts { get; }
        internal CleanupCompletion(GmWorkerQuarantinedExecution owner)
        {
            _owner = owner;
            Facts = owner._quarantined ? GmWorkerDurableExecution.AuditFacts(owner._cleanupConfirmedAuditEvent) : new(false, null, null);
        }
        internal bool BelongsTo(GmWorkerQuarantinedExecution owner, GmWorkerDurableExecution execution,
            GmWorkerExecutionWorkspace workspace) =>
            ReferenceEquals(_owner, owner) && ReferenceEquals(owner._completion, this) && ReferenceEquals(owner._durable, execution) &&
            ReferenceEquals(owner._originalWorkspace, workspace) && owner._workspaceDeletionCompleted &&
            owner._owner == null && owner._processHostLaunch == null && owner._workerCompletionTask == null &&
            (!Facts.RequiredAudit || owner._terminalAuditRecorded);
    }
}

internal sealed class GmWorkerQuarantineReservation : IDisposable
{
    private readonly object _sync = new();
    private GmWorkerQuarantineReaper? _reaper;

    internal GmWorkerQuarantineReservation(
        GmWorkerQuarantineReaper reaper)
    {
        _reaper = reaper;
    }

    internal void Transfer(IGmWorkerQuarantineOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        lock (_sync)
        {
            var reaper = _reaper
                ?? throw new InvalidOperationException(
                    "Worker quarantine reservation is no longer owned.");
            reaper.AcceptTransfer(owner);
            _reaper = null;
        }
    }

    public void Dispose()
    {
        GmWorkerQuarantineReaper? reaper;
        lock (_sync)
        {
            reaper = _reaper;
            _reaper = null;
        }

        reaper?.ReleaseReservation();
    }
}

internal sealed class GmWorkerQuarantineReaper
{
    internal const int DefaultCapacity = 32;
    internal static readonly IReadOnlyList<TimeSpan>
        DefaultRetrySchedule =
        [
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(250),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(30)
        ];

    internal static GmWorkerQuarantineReaper Shared { get; } =
        new(
            DefaultCapacity,
            DefaultRetrySchedule,
            runInBackground: true);

    private readonly ConcurrentDictionary<long, QuarantineEntry>
        _entries = new();
    private readonly SemaphoreSlim _capacity;
    private readonly IReadOnlyList<TimeSpan> _retrySchedule;
    private readonly Func<TimeSpan, CancellationToken, Task>
        _delayAsync;
    private readonly bool _runInBackground;
    private long _nextEntryId;
    private int _ownedCapacity;

    internal GmWorkerQuarantineReaper(
        int capacity = DefaultCapacity,
        IReadOnlyList<TimeSpan>? retrySchedule = null,
        bool runInBackground = true,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        if (capacity is < 1 or > DefaultCapacity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                $"Worker quarantine capacity must be between 1 and {DefaultCapacity}.");
        }

        _capacity = new SemaphoreSlim(
            capacity,
            capacity);
        _retrySchedule = retrySchedule
            ?? DefaultRetrySchedule;
        if (_retrySchedule.Any(delay => delay < TimeSpan.Zero))
        {
            throw new ArgumentOutOfRangeException(
                nameof(retrySchedule),
                "Worker quarantine retry delays cannot be negative.");
        }
        if (runInBackground &&
            (_retrySchedule.Count == 0 ||
             _retrySchedule[^1] <= TimeSpan.Zero))
        {
            throw new ArgumentException(
                "Background worker quarantine requires a positive terminal retry delay.",
                nameof(retrySchedule));
        }

        _runInBackground = runInBackground;
        _delayAsync = delayAsync
            ?? ((delay, cancellationToken) =>
                Task.Delay(delay, cancellationToken));
    }

    internal int EntryCount => _entries.Count;
    internal int OwnedCapacity =>
        Volatile.Read(ref _ownedCapacity);

    internal GmWorkerQuarantineReservation? TryReserve()
    {
        if (!_capacity.Wait(0))
            return null;

        Interlocked.Increment(ref _ownedCapacity);
        return new GmWorkerQuarantineReservation(this);
    }

    internal async Task RunPassAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var pair in _entries.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await TryReapEntryAsync(
                pair.Key,
                pair.Value);
        }
    }

    internal Task DrainConfirmedAsync(
        CancellationToken cancellationToken = default) =>
        RunPassAsync(cancellationToken);

    internal void AcceptTransfer(
        IGmWorkerQuarantineOwner owner)
    {
        var entry = new QuarantineEntry(owner);
        long entryId;
        do
        {
            entryId = Interlocked.Increment(
                ref _nextEntryId);
        }
        while (!_entries.TryAdd(entryId, entry));

        if (_runInBackground)
            _ = ReapWithScheduleAsync(entryId, entry);
    }

    internal void ReleaseReservation()
    {
        var owned = Interlocked.Decrement(
            ref _ownedCapacity);
        if (owned < 0)
        {
            throw new InvalidOperationException(
                "Worker quarantine capacity became negative.");
        }

        _capacity.Release();
    }

    private async Task ReapWithScheduleAsync(
        long entryId,
        QuarantineEntry entry)
    {
        try
        {
            var scheduleIndex = 0;
            while (true)
            {
                var delay = _retrySchedule[scheduleIndex];
                if (scheduleIndex < _retrySchedule.Count - 1)
                    scheduleIndex++;
                if (delay > TimeSpan.Zero)
                {
                    await _delayAsync(
                        delay,
                        CancellationToken.None);
                }

                if (!_entries.TryGetValue(
                        entryId,
                        out var current) ||
                    !ReferenceEquals(current, entry))
                {
                    return;
                }

                if (await TryReapEntryAsync(
                        entryId,
                        entry))
                {
                    return;
                }
            }
        }
        catch
        {
            // A scheduled pass may never discard the retained owner.
        }
    }

    private async Task<bool> TryReapEntryAsync(
        long entryId,
        QuarantineEntry entry)
    {
        if (!await entry.TryReapAsync())
            return false;
        if (!_entries.TryRemove(
                new KeyValuePair<long, QuarantineEntry>(
                    entryId,
                    entry)))
        {
            return false;
        }

        ReleaseReservation();
        return true;
    }

    private sealed class QuarantineEntry
    {
        private readonly IGmWorkerQuarantineOwner _owner;
        private int _passActive;
        private int _completed;

        internal QuarantineEntry(
            IGmWorkerQuarantineOwner owner)
        {
            _owner = owner;
        }

        internal async Task<bool> TryReapAsync()
        {
            if (Volatile.Read(ref _completed) != 0)
                return true;
            if (Interlocked.CompareExchange(
                    ref _passActive,
                    1,
                    0) != 0)
            {
                return false;
            }

            try
            {
                await _owner.ConfirmDeathAsync();
                await _owner.CleanupConfirmedAsync();
                Volatile.Write(ref _completed, 1);
                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    await _owner.RecordReaperFailureAsync(ex);
                }
                catch
                {
                    // Audit failure retains the same bounded owner for a later pass.
                }

                return false;
            }
            finally
            {
                Volatile.Write(ref _passActive, 0);
            }
        }
    }
}
