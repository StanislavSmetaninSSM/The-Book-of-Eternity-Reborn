using System.Collections.Concurrent;
using System.Diagnostics;

namespace BookOfEternityClient.Services.GmWorkers;

internal interface IGmWorkerQuarantineOwner
{
    string Identity { get; }
    Task ConfirmDeathAsync();
    Task CleanupConfirmedAsync();
    Task RecordReaperFailureAsync(Exception failure);
}

internal sealed class GmWorkerQuarantinedExecution : IGmWorkerQuarantineOwner
{
    private readonly SemaphoreSlim _confirmationGate = new(1, 1);
    private readonly SemaphoreSlim _cleanupGate = new(1, 1);
    private readonly Func<string, Task>? _beforeWorkspaceCleanupAsync;
    private readonly Func<Task<GmWorkerAuditAppendDisposition>>
        _recordCleanupConfirmedAsync;
    private readonly Func<Exception, Task> _recordFailureAsync;
    private readonly string _sessionGeneration;
    private readonly WorkerAuditEvent _cleanupConfirmedAuditEvent;
    private IGmWorkerProcessTree? _processTree;
    private Process? _process;
    private GmWorkerProcessHostLaunch? _processHostLaunch;
    private GmWorkerExecutionWorkspace? _workspace;
    private IDisposable? _workerSlot;
    private Task<int>? _workerCompletionTask;
    private Task<string>? _outputCaptureTask;
    private Task<string>? _errorCaptureTask;
    private int _deathConfirmed;
    private int _cleanupCompleted;
    private bool _workspaceHookCompleted;
    private bool _terminalAuditRecorded;

    internal GmWorkerQuarantinedExecution(
        string identity,
        bool deathConfirmed,
        IGmWorkerProcessTree? processTree,
        Process? process,
        GmWorkerProcessHostLaunch? processHostLaunch,
        GmWorkerExecutionWorkspace? workspace,
        IDisposable workerSlot,
        Task<int>? workerCompletionTask,
        Task<string>? outputCaptureTask,
        Task<string>? errorCaptureTask,
        Func<string, Task>? beforeWorkspaceCleanupAsync,
        string sessionGeneration,
        WorkerAuditEvent cleanupConfirmedAuditEvent,
        Func<Task<GmWorkerAuditAppendDisposition>>
            recordCleanupConfirmedAsync,
        Func<Exception, Task> recordFailureAsync)
    {
        Identity = identity;
        _processTree = processTree;
        _process = process;
        _processHostLaunch = processHostLaunch;
        _workspace = workspace;
        _workerSlot = workerSlot;
        _workerCompletionTask = workerCompletionTask;
        _outputCaptureTask = outputCaptureTask;
        _errorCaptureTask = errorCaptureTask;
        _beforeWorkspaceCleanupAsync = beforeWorkspaceCleanupAsync;
        _sessionGeneration = sessionGeneration;
        _cleanupConfirmedAuditEvent = cleanupConfirmedAuditEvent;
        _recordCleanupConfirmedAsync = recordCleanupConfirmedAsync;
        _recordFailureAsync = recordFailureAsync;
        _deathConfirmed = deathConfirmed
            ? 1
            : 0;
    }

    public string Identity { get; }

    public async Task ConfirmDeathAsync()
    {
        if (Volatile.Read(ref _deathConfirmed) != 0)
            return;

        await _confirmationGate.WaitAsync();
        try
        {
            if (Volatile.Read(ref _deathConfirmed) != 0)
                return;

            if (_processTree != null)
            {
                await _processTree.StopAndWaitAsync();
            }
            else if (_process != null)
            {
                await GmWorkerBridgePool.StopUnattachedProcessTreeAsync(
                    _process);
            }

            Volatile.Write(ref _deathConfirmed, 1);
        }
        finally
        {
            _confirmationGate.Release();
        }
    }

    public async Task CleanupConfirmedAsync()
    {
        if (Volatile.Read(ref _cleanupCompleted) != 0)
            return;

        await _cleanupGate.WaitAsync();
        try
        {
            if (Volatile.Read(ref _cleanupCompleted) != 0)
                return;
            if (Volatile.Read(ref _deathConfirmed) == 0)
            {
                throw new InvalidOperationException(
                    "Quarantined worker cleanup requires confirmed process-tree death.");
            }

            if (_processTree != null)
            {
                await _processTree.DisposeAsync();
                _processTree = null;
            }

            ObserveFault(_workerCompletionTask);
            ObserveFault(_outputCaptureTask);
            ObserveFault(_errorCaptureTask);
            _workerCompletionTask = null;
            _outputCaptureTask = null;
            _errorCaptureTask = null;

            if (_process != null)
            {
                _process.Dispose();
                _process = null;
            }

            if (_processHostLaunch != null)
            {
                await _processHostLaunch.DisposeAsync();
                _processHostLaunch = null;
            }

            if (_workspace != null &&
                !_workspaceHookCompleted)
            {
                if (_beforeWorkspaceCleanupAsync != null)
                {
                    await _beforeWorkspaceCleanupAsync(
                        _workspace.GameSessionPath);
                }

                _workspaceHookCompleted = true;
            }

            if (_workspace != null)
            {
                await _workspace
                    .DeleteDetachedSessionRetainingRuntimeAuthorityAsync();
            }

            if (!_terminalAuditRecorded)
            {
                var disposition =
                    await _recordCleanupConfirmedAsync();
                if (disposition !=
                    GmWorkerAuditAppendDisposition.Appended)
                {
                    if (_workspace == null)
                    {
                        throw new InvalidOperationException(
                            "Quarantine terminal audit fallback requires retained workspace authority.");
                    }

                    await _workspace.PersistQuarantineAuditReceiptAsync(
                        _sessionGeneration,
                        _cleanupConfirmedAuditEvent);
                }

                _terminalAuditRecorded = true;
            }

            if (_workspace != null)
            {
                await _workspace.DisposeAsync();
                _workspace = null;
            }

            _workerSlot?.Dispose();
            _workerSlot = null;
            Volatile.Write(ref _cleanupCompleted, 1);
        }
        finally
        {
            _cleanupGate.Release();
        }
    }

    public Task RecordReaperFailureAsync(Exception failure) =>
        _recordFailureAsync(failure);

    private static void ObserveFault(Task? task)
    {
        if (task == null)
            return;

        _ = task.ContinueWith(
            completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted |
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
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
