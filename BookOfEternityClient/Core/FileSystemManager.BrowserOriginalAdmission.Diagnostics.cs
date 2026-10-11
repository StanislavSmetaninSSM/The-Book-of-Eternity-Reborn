#if DEBUG
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookOfEternityClient.Core;

// Extends the existing test-only original-browser observer. Every measured scope
// below is synchronous: no frame may span an await. Per-process/request sums are
// wall work, potentially overlapping across threads, never acceptance latency.
internal sealed class BrowserAdmissionDiagnosticCollector
{
    [ThreadStatic] private static ContextFrame? _context;
    [ThreadStatic] private static TimingFrame? _timing;
    private readonly object _gate = new();
    private readonly Dictionary<RowKey, Row> _rows = new();
    private readonly Dictionary<ContentKey, ContentRow> _contentRows = new();
    private readonly AsyncLocal<LogicalTag?> _logical = new(); // Allocated only with an enabled collector.
    private readonly List<WindowRecord> _windows = new();
    private int _droppedWindows, _activeContentCalls, _droppedTimingRows, _droppedContentRows;
    private readonly Func<long> _clock;
    private readonly int _capacity;
    private readonly string? _directory, _nonce, _canonicalRoot;
    private int _droppedRows, _accountingErrors, _diagnosticFailures, _exportFailures, _activeFrames;
    private long _overheadTicks, _acquisitions, _contention, _emptyRecovery, _presentRecovery;
    private bool _exported, _captureSealed;

    private BrowserAdmissionDiagnosticCollector(Func<long> clock, int capacity,
        string? directory = null, string? nonce = null, string? canonicalRoot = null)
    {
        _clock = clock; _capacity = capacity;
        _directory = directory; _nonce = nonce; _canonicalRoot = canonicalRoot;
    }

    internal static BrowserAdmissionDiagnosticCollector CreateForTests(Func<long> clock, int capacity) =>
        new(clock, capacity);

    internal static BrowserAdmissionDiagnosticCollector? CreateOwned(string? directory, string? nonce, string canonicalRoot)
    {
        try
        {
            if (!OwnedOptionsValid(directory, nonce, canonicalRoot)) return null;
            return new(Stopwatch.GetTimestamp, 4096, Path.GetFullPath(directory!), nonce, Path.GetFullPath(canonicalRoot));
        }
        catch (Exception) { return null; } // Diagnostics cannot change admission.
    }

    private static bool OwnedOptionsValid(string? directory, string? nonce, string canonicalRoot)
    {
        if (directory == null || nonce == null || !Guid.TryParseExact(nonce, "N", out _) ||
            !Path.IsPathFullyQualified(directory) || !Path.IsPathFullyQualified(canonicalRoot)) return false;
        var evidence = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        var canonical = Path.TrimEndingDirectorySeparator(Path.GetFullPath(canonicalRoot));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (evidence.Equals(canonical, comparison) ||
            evidence.StartsWith(canonical + Path.DirectorySeparatorChar, comparison) ||
            canonical.StartsWith(evidence + Path.DirectorySeparatorChar, comparison)) return false;
        var scope = new TrustedLocalFileScope([evidence]);
        scope.ValidateDirectory(evidence, false);
        var marker = scope.ValidateFile(Path.Combine(evidence, "owner-nonce"), false);
        using var stream = new FileStream(marker, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length != 32) return false;
        var bytes = new byte[32];
        stream.ReadExactly(bytes);
        scope.ValidateFile(marker, false);
        return bytes.Length == 32 && System.Text.Encoding.ASCII.GetString(bytes) == nonce;
    }

    // The enabled holder is reached only after explicit options exist. Disabled
    // normal construction creates no collector, event registration or timer.
    internal static BrowserAdmissionDiagnosticCollector? FromEnvironment(string canonicalRoot)
    {
        try
        {
            var directory = Environment.GetEnvironmentVariable("BOE_TEST_BROWSER_ADMISSION_PROFILE_DIR");
            var nonce = Environment.GetEnvironmentVariable("BOE_TEST_BROWSER_ADMISSION_PROFILE_NONCE");
            if (directory == null || nonce == null) return null;
            return Enabled.Get(directory, nonce, canonicalRoot);
        }
        catch (Exception) { return null; }
    }
    private static class Enabled
    {
        private static readonly object Gate = new();
        private static BrowserAdmissionDiagnosticCollector? _collector;
        internal static BrowserAdmissionDiagnosticCollector? Get(string directory, string nonce, string canonicalRoot)
        {
            lock (Gate)
            {
                if (_collector != null) return _collector._directory == directory && _collector._nonce == nonce &&
                    _collector._canonicalRoot == Path.GetFullPath(canonicalRoot) ? _collector : null;
                _collector = CreateOwned(directory, nonce, canonicalRoot);
                if (_collector != null) AppDomain.CurrentDomain.ProcessExit += (_, _) => _collector.Export();
                return _collector;
            }
        }
    }

    private sealed record Labels(string ValidationPhase = "unbound", string EngineCaller = "unbound",
        string RequestId = "unbound", string ActionId = "unbound", string Generation = "unbound",
        Services.GmRuntime.GmSessionRunIdentity? OwnerIdentity = null, bool? SnapshotOverridePresent = null,
        string ProcessingAttemptId = "unbound", string WindowId = "unbound");
    private sealed record LogicalTag(Labels Labels, WindowRecord? Window);
    private sealed record RowKey(string Stage, string Origin, string Request, string Phase, string Purpose, bool Held, Labels Labels);
    private sealed class Row(RowKey key)
    {
        public string Stage => key.Stage;
        public string Origin => key.Origin;
        public string ActionId => key.Request == "unbound" ? key.Labels.ActionId : key.Request;
        public string Phase => key.Phase;
        public string Purpose => key.Purpose;
        public bool Held => key.Held;
        public string ValidationPhase => key.Labels.ValidationPhase;
        public string EngineCaller => key.Labels.EngineCaller;
        public string RequestId => key.Labels.RequestId;
        public string Generation => key.Labels.Generation;
        public Services.GmRuntime.GmSessionRunIdentity? OwnerIdentity => key.Labels.OwnerIdentity;
        public bool? SnapshotOverridePresent => key.Labels.SnapshotOverridePresent;
        public string ProcessingAttemptId => key.Labels.ProcessingAttemptId;
        public string WindowId => key.Labels.WindowId;
        public long Count { get; set; }
        public long InclusiveTicks { get; set; }
        public long SelfTicks { get; set; }
        public long ContainedCount { get; set; }
        public long ContainedInclusiveTicks { get; set; }
        public long ContainedSelfTicks { get; set; }
        public long CrossingCount { get; set; }
        public long CrossingInclusiveTicks { get; set; }
        public long LateCount { get; set; }
        public long LateInclusiveTicks { get; set; }
        public long TupleReads { get; set; }
        public long TupleBytes { get; set; }
        public long RollbackReads { get; set; }
        public long RollbackBytes { get; set; }
        public long SnapshotReads { get; set; }
        public long SnapshotBytes { get; set; }
        public long SnapshotHashCalls { get; set; }
        public long SnapshotHashBytes { get; set; }
        public long OtherReads { get; set; }
        public long OtherBytes { get; set; }
        public long FirstLeaseId { get; set; }
        public long LastLeaseId { get; set; }
        public int SnapshotMembers { get; set; }
        public int RollbackMembers { get; set; }
    }

    private sealed record ContentKey(string ReaderFamily, Labels Labels);
    private sealed class ContentCounts
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long Events { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long ObservedBytes { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long SemanticStarts { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long SemanticReturnedMissing { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long SemanticReturnedPresent { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long SemanticFaults { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long InitialMissing { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long InitialPresent { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long FollowupReasonMissing { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long FollowupReasonPending { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long FollowupMissing { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long FollowupPresent { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long PhysicalAttempts { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long PhysicalMissing { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long PhysicalPresent { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long PhysicalFaults { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long PhysicalDataFaults { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long OpenRetries { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long LeafMissing { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long LeafPresent { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long LeafDataFaults { get; set; }
    }
    private sealed class ContentRow(ContentKey key)
    {
        public string ReaderFamily => key.ReaderFamily;
        public Labels Labels => key.Labels;
        public string ValidationPhase => key.Labels.ValidationPhase;
        public ContentCounts InsideWindow { get; } = new();
        public ContentCounts AfterWindow { get; } = new();
        public ContentCounts OutsideWindow { get; } = new();
    }
    private sealed class WindowRecord(string id, long started, string request, string action, string generation)
    {
        public string WindowId => id;
        public string ProcessingAttemptId => id;
        public string RequestId { get; set; } = request;
        public string ActionId => action;
        public string Generation => generation;
        public long StartedTicks => started;
        public long? AcceptedTicks { get; set; }
        public long? ClosedTicks { get; set; }
        public string Outcome { get; set; } = "open";
        public Dictionary<string, long> Markers { get; } = new(StringComparer.Ordinal);
        public Services.GmRuntime.GmSessionRunIdentity? OwnerIdentity { get; set; }
        internal long? EndTicks;
    }
    private sealed class LogicalScope(BrowserAdmissionDiagnosticCollector owner, LogicalTag? previous,
        LogicalTag installed, WindowRecord? closes = null) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (closes != null)
                {
                    lock (owner._gate)
                    {
                        var ended = owner._clock();
                        closes.ClosedTicks = ended;
                        closes.Markers.TryAdd("finalizer-closed", ended);
                        if (closes.EndTicks == null)
                        { closes.EndTicks = ended; closes.Outcome = "incomplete"; }
                    }
                }
            }
            catch (Exception) { Interlocked.Increment(ref owner._diagnosticFailures); }
            finally
            {
                // A diagnostic clock/bookkeeping fault must not leak labels.
                try
                {
                    if (ReferenceEquals(owner._logical.Value, installed)) owner._logical.Value = previous;
                    else Interlocked.Increment(ref owner._accountingErrors);
                }
                catch (Exception) { Interlocked.Increment(ref owner._diagnosticFailures); }
            }
        }
    }
    private IDisposable Install(LogicalTag tag, WindowRecord? closes = null)
    {
        var previous = _logical.Value;
        var scope = new LogicalScope(this, previous, tag, closes);
        _logical.Value = tag;
        return scope;
    }
    internal IDisposable Logical(string? phase, string? caller)
    {
        try
        {
            var previous = _logical.Value;
            if (phase?.Length > 128 || caller?.Length > 128)
            { Interlocked.Increment(ref _diagnosticFailures); return Noop.Instance; }
            var labels = previous?.Labels ?? new();
            return Install(new(labels with
            { ValidationPhase = phase ?? labels.ValidationPhase, EngineCaller = caller ?? labels.EngineCaller }, previous?.Window));
        }
        catch (Exception) { Interlocked.Increment(ref _diagnosticFailures); return Noop.Instance; }
    }
    internal IDisposable Identity(string? request, string? action, string? generation,
        Services.GmRuntime.GmSessionRunIdentity? owner)
    {
        try
        {
            var previous = _logical.Value;
            var labels = previous?.Labels ?? new();
            if (previous?.Window != null && request != null)
                lock (_gate) previous.Window.RequestId = request;
            return Install(new(labels with { RequestId = request ?? labels.RequestId, ActionId = action ?? labels.ActionId,
                Generation = generation ?? labels.Generation, OwnerIdentity = owner ?? labels.OwnerIdentity }, previous?.Window));
        }
        catch (Exception) { Interlocked.Increment(ref _diagnosticFailures); return Noop.Instance; }
    }
    internal IDisposable SnapshotOverride(bool present)
    {
        try
        {
            var previous = _logical.Value;
            return Install(new((previous?.Labels ?? new()) with { SnapshotOverridePresent = present }, previous?.Window));
        }
        catch (Exception) { Interlocked.Increment(ref _diagnosticFailures); return Noop.Instance; }
    }
    internal IDisposable BeginWindow(string request, string action, string generation) =>
        BeginWindowCore(request, action, generation, "terminal-wait-returned");
    internal IDisposable BeginLateWindow(string request, string action, string generation) =>
        BeginWindowCore(request, action, generation, "late-terminal-observed");
    private IDisposable BeginWindowCore(string request, string action, string generation, string startMarker)
    {
        try
        {
            WindowRecord window;
            lock (_gate)
            {
                if (_captureSealed) return Noop.Instance;
                if (_windows.Count == 8) { _droppedWindows++; return Noop.Instance; }
                var id = Environment.ProcessId + "-" + (_windows.Count + 1);
                window = new(id, _clock(), request, action, generation);
                window.Markers.Add(startMarker, window.StartedTicks);
                _windows.Add(window);
            }
            var labels = (_logical.Value?.Labels ?? new()) with
            { RequestId = request, ActionId = action, Generation = generation, OwnerIdentity = null,
                ProcessingAttemptId = window.WindowId, WindowId = window.WindowId };
            return Install(new(labels, window), window);
        }
        catch (Exception) { Interlocked.Increment(ref _diagnosticFailures); return Noop.Instance; }
    }
    internal void Mark(string marker)
    {
        try
        {
            var tag = _logical.Value;
            if (tag?.Window == null) return;
            if (marker is not ("processing-owner" or "story-published" or "accepted-artifact-cleanup-begun" or
                "accepted-receipt-published" or "finalizer-failed")) return;
            lock (_gate)
            {
                if (_captureSealed) return;
                var ticks = _clock();
                tag.Window.Markers.TryAdd(marker, ticks);
                if (marker == "processing-owner") tag.Window.OwnerIdentity = tag.Labels.OwnerIdentity;
                if (marker == "accepted-receipt-published" && tag.Window.EndTicks == null)
                { tag.Window.AcceptedTicks = ticks; tag.Window.EndTicks = ticks; tag.Window.Outcome = "accepted"; }
                if (marker == "finalizer-failed")
                {
                    if (tag.Window.EndTicks == null) { tag.Window.EndTicks = ticks; tag.Window.Outcome = "failed"; }
                    else if (tag.Window.AcceptedTicks != null) tag.Window.Outcome = "accepted-then-finalizer-failed";
                }
            }
        }
        catch (Exception) { Interlocked.Increment(ref _diagnosticFailures); }
    }
    internal void Content(string path, string family, string attempt, string outcome, long bytes)
    {
        try
        {
            // Already supplied relative name only; no filesystem/path validation for labels.
            if (path.Length > 256 || family.Length > 64 || attempt.Length > 64 || outcome.Length > 64)
            { Interlocked.Increment(ref _diagnosticFailures); return; }
            var logical = _logical.Value;
            var observed = logical?.Window == null ? (long?)null : _clock();
            lock (_gate)
            {
                if (_captureSealed) return;
                if (attempt == "semantic")
                {
                    if (outcome == "started") _activeContentCalls++;
                    else if (outcome is "returned-missing" or "returned-present" or "fault") _activeContentCalls--;
                    if (_activeContentCalls < 0) _accountingErrors++;
                }
                var segment = logical?.Window == null ? "outside" :
                    logical.Window.EndTicks is { } end && observed >= end ? "after-window" : "inside-window";
                var key = new ContentKey(family, logical?.Labels ?? new());
                if (!_contentRows.TryGetValue(key, out var row))
                {
                    if (_rows.Count + _contentRows.Count >= _capacity) { _droppedRows++; _droppedContentRows++; return; }
                    row = new(key); _contentRows.Add(key, row);
                }
                var counts = segment == "inside-window" ? row.InsideWindow :
                    segment == "after-window" ? row.AfterWindow : row.OutsideWindow;
                counts.Events++; counts.ObservedBytes += bytes;
                switch (attempt, outcome)
                {
                    case ("semantic", "started"): counts.SemanticStarts++; break;
                    case ("semantic", "returned-missing"): counts.SemanticReturnedMissing++; break;
                    case ("semantic", "returned-present"): counts.SemanticReturnedPresent++; break;
                    case ("semantic", "fault"): counts.SemanticFaults++; break;
                    case ("initial", "missing"): counts.InitialMissing++; break;
                    case ("initial", "present"): counts.InitialPresent++; break;
                    case ("followup-reason", "missing"): counts.FollowupReasonMissing++; break;
                    case ("followup-reason", "pending"): counts.FollowupReasonPending++; break;
                    case ("followup", "missing"): counts.FollowupMissing++; break;
                    case ("followup", "present"): counts.FollowupPresent++; break;
                    case ("physical-attempt", "started"): counts.PhysicalAttempts++; break;
                    case ("physical-result", "missing"): counts.PhysicalMissing++; break;
                    case ("physical-result", "present"): counts.PhysicalPresent++; break;
                    case ("physical-result", "fault"): counts.PhysicalFaults++; break;
                    case ("physical-result", "data-fault"): counts.PhysicalDataFaults++; break;
                    case ("retry", "open-fault"): counts.OpenRetries++; break;
                    case ("leaf", "missing"): counts.LeafMissing++; break;
                    case ("leaf", "present"): counts.LeafPresent++; break;
                    case ("leaf", "data-fault"): counts.LeafDataFaults++; break;
                    default: _diagnosticFailures++; break;
                }
            }
        }
        catch (Exception) { Interlocked.Increment(ref _diagnosticFailures); }
    }

    private sealed class ContextFrame(BrowserAdmissionDiagnosticCollector owner, ContextFrame? parent,
        string origin, string request, string phase, string purpose, long lease, bool held) : IDisposable
    {
        internal readonly BrowserAdmissionDiagnosticCollector Owner = owner;
        internal readonly string Origin = origin, Request = request, Phase = phase, Purpose = purpose;
        internal readonly long Lease = lease;
        internal readonly bool Held = held;
        private readonly int _thread = Environment.CurrentManagedThreadId;
        public void Dispose()
        {
            if (_thread == Environment.CurrentManagedThreadId && ReferenceEquals(_context, this)) _context = parent;
            else Interlocked.Increment(ref Owner._accountingErrors);
        }
    }
    private sealed class Noop : IDisposable
    {
        internal static readonly Noop Instance = new();
        public void Dispose() { }
    }
    internal IDisposable Context(string origin, string request, string phase, string purpose, long lease, bool held)
    {
        try
        {
            var frame = new ContextFrame(this, _context, origin,
                Guid.TryParseExact(request, "N", out _) ? request : "unbound", phase, purpose, lease, held);
            _context = frame; return frame;
        }
        catch (Exception) { Interlocked.Increment(ref _diagnosticFailures); return Noop.Instance; }
    }
    internal string CurrentOrigin => _context?.Owner == this ? _context.Origin : "other";

    private sealed class TimingFrame(BrowserAdmissionDiagnosticCollector owner, TimingFrame? parent,
        Row row, long lease, long started, WindowRecord? window) : IDisposable
    {
        internal readonly BrowserAdmissionDiagnosticCollector Owner = owner;
        internal readonly Row Row = row;
        internal long ChildTicks;
        private readonly int _thread = Environment.CurrentManagedThreadId;
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                var ended = Owner._clock();
                if (_thread != Environment.CurrentManagedThreadId || !ReferenceEquals(_timing, this))
                { Interlocked.Increment(ref Owner._accountingErrors); return; }
                _timing = parent;
                var inclusive = ended - started;
                var self = inclusive - ChildTicks;
                if (inclusive < 0 || self < 0) { Interlocked.Increment(ref Owner._accountingErrors); return; }
                if (parent?.Owner == Owner) parent.ChildTicks += inclusive;
                lock (Owner._gate)
                {
                    row.Count++; row.InclusiveTicks += inclusive; row.SelfTicks += self;
                    if (window != null)
                    {
                        if (window.EndTicks is { } end && started >= end)
                        { row.LateCount++; row.LateInclusiveTicks += inclusive; }
                        else if (window.EndTicks is { } boundary && ended > boundary)
                        { row.CrossingCount++; row.CrossingInclusiveTicks += inclusive; }
                        else
                        { row.ContainedCount++; row.ContainedInclusiveTicks += inclusive; row.ContainedSelfTicks += self; }
                    }
                    if (lease != 0) { if (row.FirstLeaseId == 0) row.FirstLeaseId = lease; row.LastLeaseId = lease; }
                }
                Interlocked.Add(ref Owner._overheadTicks, Math.Max(0, Owner._clock() - ended));
            }
            catch (Exception) { Interlocked.Increment(ref Owner._diagnosticFailures); }
            finally { Interlocked.Decrement(ref Owner._activeFrames); }
        }
    }
    internal IDisposable Measure(string stage)
    {
        var reserved = false;
        try
        {
            var setup = _clock();
            var context = _context?.Owner == this ? _context : null;
            var logical = _logical.Value;
            var key = new RowKey(stage, context?.Origin ?? "other", context?.Request ?? "unbound",
                context?.Phase ?? "unbound", context?.Purpose ?? "unbound", context?.Held == true, logical?.Labels ?? new());
            Row row;
            lock (_gate)
            {
                if (_captureSealed) return Noop.Instance;
                if (!_rows.TryGetValue(key, out row!))
                {
                    if (_rows.Count + _contentRows.Count >= _capacity) { _droppedRows++; _droppedTimingRows++; return Noop.Instance; }
                    row = new(key); _rows.Add(key, row);
                }
                // Snapshot/export uses the same gate: even clock/frame setup
                // belongs to a pending invocation, never a complete empty row.
                Interlocked.Increment(ref _activeFrames);
                reserved = true;
            }
            var started = _clock();
            Interlocked.Add(ref _overheadTicks, Math.Max(0, started - setup));
            var frame = new TimingFrame(this, _timing, row, context?.Lease ?? 0, started, logical?.Window);
            _timing = frame; reserved = false; return frame;
        }
        catch (Exception)
        {
            lock (_gate)
            {
                Interlocked.Increment(ref _diagnosticFailures);
                if (reserved) Interlocked.Decrement(ref _activeFrames);
            }
            return Noop.Instance;
        }
    }
    internal void Read(string kind, long bytes)
    {
        try
        {
            if (_timing?.Owner != this) return;
            var row = _timing.Row;
            lock (_gate)
                switch (kind)
                {
                    case "tuple": row.TupleReads++; row.TupleBytes += bytes; break;
                    case "rollback": row.RollbackReads++; row.RollbackBytes += bytes; break;
                    case "snapshot": row.SnapshotReads++; row.SnapshotBytes += bytes; break;
                    case "snapshot-hash": row.SnapshotHashCalls++; row.SnapshotHashBytes += bytes; break;
                    default: row.OtherReads++; row.OtherBytes += bytes; break;
                }
        }
        catch (Exception) { Interlocked.Increment(ref _diagnosticFailures); }
    }
    internal long LeaseOpened() => Interlocked.Increment(ref _acquisitions);
    internal void Contended() => Interlocked.Increment(ref _contention);
    internal void Recovery(bool present)
    {
        if (present) Interlocked.Increment(ref _presentRecovery); else Interlocked.Increment(ref _emptyRecovery);
    }
    internal void Inventory(int snapshots, int rollback)
    {
        try
        {
            if (_timing?.Owner != this) return;
            lock (_gate)
            {
                _timing.Row.SnapshotMembers = Math.Max(_timing.Row.SnapshotMembers, snapshots);
                _timing.Row.RollbackMembers = Math.Max(_timing.Row.RollbackMembers, rollback);
            }
        }
        catch (Exception) { Interlocked.Increment(ref _diagnosticFailures); }
    }
    internal string SnapshotJson()
    {
        lock (_gate)
            return JsonSerializer.Serialize(new
            {
                ProcessId = Environment.ProcessId, StopwatchFrequency = Stopwatch.Frequency,
                Qualification = "Raw synchronous attempt wall work; concurrent totals may overlap, never CPU/latency. Engine terminal-wait-returned OR late-terminal-observed→accepted-receipt-published window is distinct from external HelperComplete→Acceptance. The late path does not wait for a terminal; its marker records the already staged non-idle branch before original owner acquisition. True RequestId is separate from original pending ActionId; unavailable identity stays unbound/null.",
                WindowQualification = "Contained spans only, with separate crossing/late raw spans, no clipping or subtraction. No incoming pre-window span accounting. Finalizer failure/incomplete window is not accepted. Stopwatch ticks are process-domain anchors, no assumed Python clock mapping. Pending owner lifecycle Phase is not ValidationPhase.",
                CaptureSealed = _captureSealed,
                CompleteQualification = "Completeness of diagnostic recording at this finite boundary only, never successful/accepted processing: each window Outcome and AcceptedTicks are independent. First owned export attempt seals later measurement starts, which are excluded; not whole-process completeness. LeaseId zero means unavailable, not absence of an ambient lease.",
                SelfTicksQualification = "RAW INSTRUMENTED SELF WALL: inclusive minus direct child inclusive in the same invocation. Includes child Measure setup/Dispose bookkeeping and Read/Inventory/Context costs; not pure IO/hash work.",
                CollectorBookkeepingTicksQualification = "Partial setup/dispose estimate overlapping parent SelfTicks. Never subtract as exact overhead or sum with SelfTicks; end-to-end observer overhead remains uncalibrated.",
                Rows = _rows.Values.ToArray(), ContentRows = _contentRows.Values.ToArray(), Windows = _windows.ToArray(),
                DroppedWindows = _droppedWindows, DroppedRows = _droppedRows,
                DroppedTimingRows = _droppedTimingRows, DroppedContentRows = _droppedContentRows, AccountingErrors = _accountingErrors,
                DiagnosticFailures = _diagnosticFailures, ExportFailures = _exportFailures,
                ActiveFrames = _activeFrames,
                ActiveContentCalls = _activeContentCalls,
                ContentQualification = "Aggregation is ReaderFamily × full immutable Labels; no Path dimension or representative path is retained. Semantic starts/completions versus physical attempts/retries/qualified absent results. Fault/data-fault never missing. Omitted fixed counter fields mean zero (not unavailable); failed/dropped recording is qualified separately. Fixed counters carry inside/after/outside-window observation-point classification, not read duration or admission counts. Families describe different layers of the same read; do not sum their bytes/events as unique reads. Existing raw direct IO outside these selected filesystem readers is not inventoried. Present/absence observations are not authority grants.",
                CollectorBookkeepingTicks = _overheadTicks, Acquisitions = _acquisitions, Contention = _contention,
                EmptyRecovery = _emptyRecovery, PresentRecovery = _presentRecovery,
                Complete = _droppedRows == 0 && _accountingErrors == 0 && _diagnosticFailures == 0 &&
                    _exportFailures == 0 && _activeFrames == 0 && _activeContentCalls == 0 && _droppedWindows == 0 && _windows.All(window => window.ClosedTicks != null)
            });
    }
    internal void Export()
    {
        try
        {
            lock (_gate)
            {
                if (_exported || _directory == null || _nonce == null || _canonicalRoot == null) return;
                _captureSealed = true;
                if (!OwnedOptionsValid(_directory, _nonce, _canonicalRoot)) { _exportFailures++; return; }
                var scope = new TrustedLocalFileScope([_directory]);
                var path = scope.ValidateFile(Path.Combine(_directory, "process-" + Environment.ProcessId + "-" + _nonce + ".json"));
                var bytes = System.Text.Encoding.UTF8.GetBytes(SnapshotJson());
                if (bytes.Length > 4 * 1024 * 1024) { _exportFailures++; return; }
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                stream.Write(bytes); stream.Flush(); _exported = true;
            }
        }
        catch (Exception) { Interlocked.Increment(ref _exportFailures); }
    }
}

public partial class FileSystemManager
{
    private readonly BrowserAdmissionDiagnosticCollector? _browserAdmissionDiagnostic;
    internal IDisposable? BrowserDiagnosticLogical(string? phase, string? caller) =>
        _browserAdmissionDiagnostic?.Logical(phase, caller);
    internal IDisposable? BrowserDiagnosticSnapshotOverride(bool present) =>
        _browserAdmissionDiagnostic?.SnapshotOverride(present);
    internal IDisposable? BrowserDiagnosticWindow(string request, string action, string generation) =>
        _browserAdmissionDiagnostic?.BeginWindow(request, action, generation);
    internal IDisposable? BrowserDiagnosticLateWindow(string request, string action, string generation) =>
        _browserAdmissionDiagnostic?.BeginLateWindow(request, action, generation);
    internal IDisposable? BrowserDiagnosticIdentity(string? request, string? action, string? generation,
        Services.GmRuntime.GmSessionRunIdentity? owner) =>
        _browserAdmissionDiagnostic?.Identity(request, action, generation, owner);
    internal void BrowserDiagnosticMark(string marker) => _browserAdmissionDiagnostic?.Mark(marker);
    private void BrowserDiagnosticContent(string path, string family, string attempt, string outcome, long bytes = 0) =>
        _browserAdmissionDiagnostic?.Content(path, family, attempt, outcome, bytes);
    private IDisposable? BrowserDiagnosticContext(string origin, Services.PendingPlayerActionService.Staged? staged = null,
        CanonicalWriteLease? lease = null)
    {
        if (_browserAdmissionDiagnostic == null) return null;
        var held = lease != null || HasAmbientCanonicalLease();
        return _browserAdmissionDiagnostic.Context(origin,
            staged?.Binding.ActionId ?? CurrentBrowserOriginalScope()?.Binding.ActionId ?? "unbound",
            CurrentBrowserOriginalScope()?.ExpectedState?.Phase ?? "unbound",
            lease?.Purpose.ToString() ?? (held ? "unknown-ambient-purpose" : "unheld"),
            lease?.BrowserDiagnosticId ?? 0, held);
    }
}
#endif
