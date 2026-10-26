using System.Collections.Concurrent;

namespace BookOfEternityClient.Core;

/// <summary>
/// Counts canonical sources; an explicit DATA-only epoch optionally retains selected bytes without invoking caller code.
/// </summary>
internal sealed class CanonicalReadActivityAccumulator
{
    private readonly ConcurrentDictionary<string, long> _sources = new(StringComparer.Ordinal);
    private ObservationWindow? _observation;

    /// <summary>Gets whether an actual observation epoch remains open, without invoking caller code.</summary>
    internal bool IsObserving => Volatile.Read(ref _observation) is not null;

    /// <summary>Captures this actual observation epoch as immutable provenance, never read permission.</summary>
    internal object? CaptureEpoch() => Volatile.Read(ref _observation);

    /// <summary>Checks actual epoch/files/window/generation provenance without importing another producer's identity.</summary>
    internal bool MatchesEpoch(FileSystemManager files, object epoch) =>
        Volatile.Read(ref _observation) is { } current && ReferenceEquals(current, epoch) && current.MatchesFiles(files);

    /// <summary>Retains diagnostic allocation/record failure without replacing the functional result.</summary>
    internal void FailObservation() => Volatile.Read(ref _observation)?.Fail();

    /// <summary>Begins one DATA-only source observation epoch on the actual configured files/recorder/window.</summary>
    /// <param name="files">The actual filesystem carrying this accumulator.</param>
    /// <param name="window">Its configured passive recorder's currently active window.</param>
    /// <returns>An exclusive owner to close at the original capture cut and dispose after detached capture.</returns>
    internal ObservationWindow BeginObservation(FileSystemManager files, OperationTimingAccumulator.Window window)
    {
        ArgumentNullException.ThrowIfNull(files); ArgumentNullException.ThrowIfNull(window);
        if (!files.MatchesCanonicalReadObservation(this, window))
            throw new InvalidOperationException("Read observation requires its actual files and active timing window.");
        var owner = new ObservationWindow(this, files, window, files.CanonicalReadObservationGeneration);
        if (Interlocked.CompareExchange(ref _observation, owner, null) is not null)
            throw new InvalidOperationException("A read observation epoch is already active.");
        return owner;
    }

    /// <summary>Creates an explicit per-call DATA token only while an actual matching observation is active.</summary>
    /// <param name="issuer">Private closed read transport token.</param>
    /// <param name="files">Actual reader filesystem.</param>
    /// <param name="path">Original path spelling.</param>
    /// <param name="family">Actual API/reader family.</param>
    /// <param name="origin">Optional producer-attested source identity; a timer never creates one.</param>
    /// <returns>The per-call token, or null on the ordinary inactive route.</returns>
    internal ReadObservation? BeginRead(object issuer, FileSystemManager files, string path, string family,
        BookOfEternityClient.Services.ValidationService.NarrativeInterfaceSourceSession.ReadOrigin? origin) =>
        FileReadObservation.IsActivityIssuer(issuer)
            ? Volatile.Read(ref _observation)?.BeginRead(issuer, files, path, family, origin)
            : throw new InvalidOperationException("Actual closed read transport required.");

    /// <summary>Owns all uncapped path events until the original cut; holds no lease or read/write authority.</summary>
    internal sealed class ObservationWindow : IDisposable
    {
        private readonly CanonicalReadActivityAccumulator _owner;
        private readonly FileSystemManager _files;
        private readonly OperationTimingAccumulator.Window _window;
        private readonly long _generation;
        private readonly object _gate = new();
        private readonly List<CanonicalReadEvent> _events = [];
        private long _nextRead, _ordinal, _pending;
        private bool _closed, _disposed;
        private string? _fault;

        /// <summary>Creates the actual accumulator/files/window/generation DATA lifetime.</summary>
        internal ObservationWindow(CanonicalReadActivityAccumulator owner, FileSystemManager files,
            OperationTimingAccumulator.Window window, long generation)
        { _owner = owner; _files = files; _window = window; _generation = generation; }

        /// <summary>Registers a call only through the actual closed reader transport.</summary>
        internal ReadObservation? BeginRead(object issuer, FileSystemManager files, string path, string family,
            BookOfEternityClient.Services.ValidationService.NarrativeInterfaceSourceSession.ReadOrigin? origin)
        {
            if (!FileReadObservation.IsActivityIssuer(issuer)) throw new InvalidOperationException("Actual closed read transport required.");
            lock (_gate)
            {
                if (_closed || _disposed) return null;
                if (!ReferenceEquals(files, _files)) { _fault ??= "foreign_read_files"; return null; }
                if (!IsCurrent()) { _fault ??= "expired_read_observation_binding"; return null; }
                _pending++;
                return new ReadObservation(issuer, this, ++_nextRead, path, family, origin);
            }
        }

        /// <summary>Latches diagnostic failure without changing functional source behavior.</summary>
        internal void Fail() { lock (_gate) _fault ??= "diagnostic_read_record_failed"; }

        /// <summary>Registers an actual nested physical read in the already selected epoch, including late tails.</summary>
        internal ReadObservation? BeginChild(object issuer, ReadObservation parent, FileSystemManager files,
            string path, string family, BookOfEternityClient.Services.ValidationService.NarrativeInterfaceSourceSession.ReadOrigin? origin)
        {
            if (!FileReadObservation.IsActivityIssuer(issuer) || !parent.BelongsTo(this))
                throw new InvalidOperationException("Actual registered reader dispatch required.");
            lock (_gate)
            {
                if (_disposed) return null;
                if (!ReferenceEquals(files, _files)) { _fault ??= "foreign_read_files"; return null; }
                if (_closed) _fault ??= "physical_child_started_after_cut";
                if (!IsCurrent()) _fault ??= "expired_read_observation_binding";
                _pending++;
                return new ReadObservation(issuer, this, ++_nextRead, path, family, origin);
            }
        }

        private bool IsCurrent() => _files.CanonicalReadObservationGeneration == _generation &&
            _files.MatchesCanonicalReadObservation(_owner, _window);

        /// <summary>Checks this still-open actual files/window/generation identity.</summary>
        internal bool MatchesFiles(FileSystemManager files)
        { lock (_gate) return !_closed && !_disposed && ReferenceEquals(files, _files) && IsCurrent(); }

        /// <summary>Records one actual DATA boundary; diagnostic failure remains incomplete and nonthrowing.</summary>
        internal void Record(ReadObservation read, CanonicalReadEventKind kind, byte[]? bytes = null,
            bool physical = true, Exception? error = null)
        {
            try { RecordCore(read, kind, bytes, physical, error); }
            catch (Exception) { lock (_gate) _fault ??= "diagnostic_read_record_failed"; }
        }

        private void RecordCore(ReadObservation read, CanonicalReadEventKind kind, byte[]? bytes,
            bool physical, Exception? error)
        {
            lock (_gate)
            {
                if (_disposed) return;
                if (_closed) _fault ??= "read_event_after_cut";
                if (!IsCurrent()) _fault ??= "expired_read_observation_binding";
                var evaluation = _window.CaptureNamedCounts("NarrativePolicy.Evaluate");
                var recheck = _window.CaptureNamedCounts("NarrativePolicy.Recheck");
                var phase = CanonicalReadSourcePhase.Ordinary;
                Guid? invocation = null;
                if (read.Origin is { } origin)
                {
                    if (origin.TryCapture(_files, out var identity, out var isRecheck))
                    {
                        invocation = identity;
                        var active = isRecheck ? recheck.Started > recheck.Completed : evaluation.Started > evaluation.Completed;
                        phase = active ? isRecheck ? CanonicalReadSourcePhase.NarrativeRecheck : CanonicalReadSourcePhase.NarrativeEvaluation
                            : CanonicalReadSourcePhase.Unavailable;
                        if (!active) _fault ??= "narrative_origin_outside_actual_named_phase";
                    }
                    else { phase = CanonicalReadSourcePhase.Unavailable; _fault ??= "unattested_or_expired_narrative_origin"; }
                }
                _events.Add(new(++_ordinal, read.Id, read.Path, read.Family, kind, phase, invocation,
                    physical, read.Path == "game_state/meta/achievements.json" ? bytes?.ToArray() : null,
                    error is null ? null : error.GetType().Name + ": " + error.Message,
                    evaluation.Started, evaluation.Completed, recheck.Started, recheck.Completed));
            }
        }

        /// <summary>Settles one actual registered call once; no functional owner is disposed.</summary>
        internal void Returned(ReadObservation read)
        {
            lock (_gate)
            {
                if (read.Ended) return;
                read.Ended = true; _pending--;
                if (_closed && !_disposed) _fault ??= "read_completed_after_cut";
            }
        }

        /// <summary>Stops new observations at the actual cut; pending calls cannot qualify a complete DATA capture.</summary>
        internal void CloseCapture()
        {
            lock (_gate)
            {
                if (_closed) return;
                if (!IsCurrent()) _fault ??= "expired_read_observation_binding";
                _closed = true;
                if (_pending != 0) _fault ??= "pending_read_at_cut";
            }
            Interlocked.CompareExchange(ref _owner._observation, null, this);
        }

        /// <summary>Returns detached bytes/events without callbacks or another physical read.</summary>
        /// <returns>Current observation validity and every retained event.</returns>
        internal CanonicalReadObservationSnapshot Capture()
        {
            lock (_gate)
            {
                if (!_closed && !_disposed && !IsCurrent()) _fault ??= "expired_read_observation_binding";
                try { return new(_fault is null && !_disposed, _closed, _disposed, _pending, _fault,
                    _events.Select(row => row with { Bytes = row.Bytes?.ToArray() }).ToArray()); }
                catch (Exception)
                {
                    _fault ??= "diagnostic_read_capture_copy_failed";
                    return new(false, _closed, _disposed, _pending, _fault, Array.Empty<CanonicalReadEvent>());
                }
            }
        }

        /// <summary>Ends this epoch once and releases buffers without disposing any functional owner.</summary>
        public void Dispose()
        {
            CloseCapture();
            lock (_gate) { _disposed = true; _events.Clear(); }
        }
    }

    /// <summary>One explicit read call; tokens never enter cached snapshot DATA.</summary>
    internal sealed class ReadObservation
    {
        private readonly ObservationWindow _owner;
        /// <summary>Identifies this actual call within its one observation epoch.</summary>
        internal readonly long Id;
        /// <summary>Retains original source spelling and concrete reader family.</summary>
        internal readonly string Path, Family;
        /// <summary>Carries only explicit private-producer provenance; no timer can issue it.</summary>
        internal readonly BookOfEternityClient.Services.ValidationService.NarrativeInterfaceSourceSession.ReadOrigin? Origin;
        /// <summary>Tracks settlement under the owning observation monitor.</summary>
        internal bool Ended;
        /// <summary>Checks the actual registered DATA owner without exposing a setter.</summary>
        internal bool BelongsTo(ObservationWindow owner) => ReferenceEquals(_owner, owner);
        internal ReadObservation? BeginChild(object issuer, FileSystemManager files, string path, string family,
            BookOfEternityClient.Services.ValidationService.NarrativeInterfaceSourceSession.ReadOrigin? origin) =>
            _owner.BeginChild(issuer, this, files, path, family, origin);
        /// <summary>Creates a call only when supplied the private closed transport issuer.</summary>
        internal ReadObservation(object issuer, ObservationWindow owner, long id, string path, string family,
            BookOfEternityClient.Services.ValidationService.NarrativeInterfaceSourceSession.ReadOrigin? origin)
        {
            if (!FileReadObservation.IsActivityIssuer(issuer)) throw new InvalidOperationException("Actual closed read transport required.");
            _owner = owner; Id = id; Path = path; Family = family; Origin = origin;
        }
        /// <summary>Forwards an actual event only through the private closed transport.</summary>
        internal void Record(object issuer, CanonicalReadEventKind kind, byte[]? bytes = null, bool physical = true, Exception? error = null)
        {
            if (!FileReadObservation.IsActivityIssuer(issuer)) throw new InvalidOperationException("Actual closed read transport required.");
            _owner.Record(this, kind, bytes, physical, error);
        }
        /// <summary>Settles DATA only through the private closed transport.</summary>
        internal void Complete(object issuer)
        {
            if (!FileReadObservation.IsActivityIssuer(issuer)) throw new InvalidOperationException("Actual closed read transport required.");
            _owner.Returned(this);
        }
    }

    /// <summary>
    /// Records one actual source event without a diagnostic record quota.
    /// </summary>
    /// <param name="path">
    /// Exact original relative source spelling; <see langword="null"/> is invalid.
    /// </param>
    internal void Record(string path) => _sources.AddOrUpdate(path, 1, static (_, count) => count + 1);

    /// <summary>
    /// Reads the cumulative event count for an exact path.
    /// </summary>
    /// <param name="path">
    /// Exact source spelling; <see langword="null"/> is invalid.
    /// </param>
    /// <returns>
    /// Observed count, or zero before the first event for that spelling.
    /// </returns>
    internal long ReadCount(string path) => _sources.GetValueOrDefault(path);

    /// <summary>
    /// Copies every observed path count without a record or byte limit.
    /// </summary>
    /// <returns>
    /// Detached counters; these are canonical source events, not all filesystem reads.
    /// </returns>
    internal Dictionary<string, long> Capture() => new(_sources, StringComparer.Ordinal);
}


/// <summary>Distinct actual reader events; a snapshot never implies successful owning return.</summary>
internal enum CanonicalReadEventKind
{
    InitialOpened, PhysicalSnapshot, PhysicalAbsence, NonPhysicalData, ReaderReturned,
    OriginalAttemptReturned, PublicInitialAttemptReturned, PublicFinalReturned, LeasedReturned, Fault, Cancelled
}

/// <summary>Source ownership; named timing counts alone never attribute an ordinary read to N.</summary>
internal enum CanonicalReadSourcePhase { Ordinary, NarrativeEvaluation, NarrativeRecheck, Unavailable }

/// <summary>Detached passive read DATA. Selected source buffers are copied both on capture and export.</summary>
internal sealed record CanonicalReadEvent(long Ordinal, long ReadId, string Path, string Family,
    CanonicalReadEventKind Kind, CanonicalReadSourcePhase Phase, Guid? InvocationId, bool Physical,
    byte[]? Bytes, string? Fault, long EvaluationStarted, long EvaluationCompleted, long RecheckStarted, long RecheckCompleted);

/// <summary>The full epoch's DATA-only capture, never authority to accept or mutate.</summary>
internal sealed record CanonicalReadObservationSnapshot(bool Available, bool Closed, bool Disposed,
    long Pending, string? Fault, CanonicalReadEvent[] Events);
