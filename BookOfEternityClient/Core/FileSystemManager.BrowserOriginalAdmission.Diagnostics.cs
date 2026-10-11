#if DEBUG
using System.Diagnostics;
using System.Text.Json;

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

    private sealed record RowKey(string Stage, string Origin, string Request, string Phase, string Purpose, bool Held);
    private sealed class Row(RowKey key)
    {
        public string Stage => key.Stage;
        public string Origin => key.Origin;
        public string ActionId => key.Request;
        public string Phase => key.Phase;
        public string Purpose => key.Purpose;
        public bool Held => key.Held;
        public long Count { get; set; }
        public long InclusiveTicks { get; set; }
        public long SelfTicks { get; set; }
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
        Row row, long lease, long started) : IDisposable
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
            var key = new RowKey(stage, context?.Origin ?? "other", context?.Request ?? "unbound",
                context?.Phase ?? "unbound", context?.Purpose ?? "unbound", context?.Held == true);
            Row row;
            lock (_gate)
            {
                if (_captureSealed) return Noop.Instance;
                if (!_rows.TryGetValue(key, out row!))
                {
                    if (_rows.Count >= _capacity) { _droppedRows++; return Noop.Instance; }
                    row = new(key); _rows.Add(key, row);
                }
                // Snapshot/export uses the same gate: even clock/frame setup
                // belongs to a pending invocation, never a complete empty row.
                Interlocked.Increment(ref _activeFrames);
                reserved = true;
            }
            var started = _clock();
            Interlocked.Add(ref _overheadTicks, Math.Max(0, started - setup));
            var frame = new TimingFrame(this, _timing, row, context?.Lease ?? 0, started);
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
                Qualification = "Per-action/process aggregate synchronous wall work; concurrent totals may overlap. ActionId joins RequestId only through the actual C5 identity assertion. No acceptance-window accounting or CPU measurement.",
                CaptureSealed = _captureSealed,
                CompleteQualification = "Completeness at this finite capture boundary only. First owned export attempt seals later measurement starts, which are excluded; not whole-process completeness. LeaseId zero means unavailable, not absence of an ambient lease.",
                SelfTicksQualification = "RAW INSTRUMENTED SELF WALL: inclusive minus direct child inclusive in the same invocation. Includes child Measure setup/Dispose bookkeeping and Read/Inventory/Context costs; not pure IO/hash work.",
                CollectorBookkeepingTicksQualification = "Partial setup/dispose estimate overlapping parent SelfTicks. Never subtract as exact overhead or sum with SelfTicks; end-to-end observer overhead remains uncalibrated.",
                Rows = _rows.Values.ToArray(), DroppedRows = _droppedRows, AccountingErrors = _accountingErrors,
                DiagnosticFailures = _diagnosticFailures, ExportFailures = _exportFailures,
                ActiveFrames = _activeFrames,
                CollectorBookkeepingTicks = _overheadTicks, Acquisitions = _acquisitions, Contention = _contention,
                EmptyRecovery = _emptyRecovery, PresentRecovery = _presentRecovery,
                Complete = _droppedRows == 0 && _accountingErrors == 0 && _diagnosticFailures == 0 &&
                    _exportFailures == 0 && _activeFrames == 0
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
