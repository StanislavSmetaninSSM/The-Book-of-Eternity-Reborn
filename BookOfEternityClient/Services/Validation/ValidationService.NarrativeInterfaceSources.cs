using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>Reads the same public current source, optionally retaining actual invocation observations.</summary>
    /// <param name="path">The path retained or checked by this operation.</param>
    /// <param name="source">Explicit invocation observations, or null for the unchanged ordinary route.</param>
    private Task<string?> ReadNarrativeSourceTextAsync(string path, NarrativeInterfaceSourceSession? source) =>
        source is null ? _fs.ReadFileAsync(path) : source.ReadTextAsync(path, path, "public-current");

    /// <summary>Retains supplied manifest DATA without inventing physical manifest reads.</summary>
    /// <param name="source">Explicit invocation observations, or null for the unchanged ordinary route.</param>
    /// <param name="manifest">The manifest retained or checked by this operation.</param>
    /// <param name="provenance">The actual supplied, scoped or physical lookup disposition.</param>
    private void RecordNarrativeOriginalContext(OriginalLookupObservation? source,
        ValidationPendingTurnSnapshotManifest manifest, string provenance) =>
        source?.OriginalContext(manifest.SessionId, manifest.RequestId, manifest.ManifestPayloadHash, manifest.TurnNumber, provenance, JsonSerializer.Serialize(manifest, ManifestHashJsonOpts));

    /// <summary>Preserves the selected original reader and marks unjoined scoped provenance unavailable.</summary>
    /// <param name="role">The role retained or checked by this operation.</param>
    /// <param name="path">The path retained or checked by this operation.</param>
    /// <param name="lease">The lease retained or checked by this operation.</param>
    /// <param name="source">Explicit invocation observations, or null for the unchanged ordinary route.</param>
    private async Task<byte[]?> ReadNarrativeOriginalBytesAsync(string role, string path,
        FileSystemManager.CanonicalWriteLease? lease, OriginalLookupObservation source)
    {
        if (lease is null && CurrentWorldReadPhase() is { } phase)
        {
            source.Unavailable("narrative_policy_world_phase_provenance_unavailable");
            return await phase.ReadBytesAsync(path);
        }
        return await source.ReadBytesAsync(role, path, "original-snapshot", lease);
    }
    /// <summary>Preserves actual authority text selection and explicitly observes its physical leaves.</summary>
    /// <param name="role">The role retained or checked by this operation.</param>
    /// <param name="path">The path retained or checked by this operation.</param>
    /// <param name="lease">The lease retained or checked by this operation.</param>
    /// <param name="source">Explicit invocation observations, or null for the unchanged ordinary route.</param>
    private Task<string?> ReadNarrativeAuxiliaryTextAsync(string role, string path,
        FileSystemManager.CanonicalWriteLease? lease, OriginalLookupObservation source)
    {
        if (lease is null && CurrentWorldReadPhase() is { } phase)
        {
            source.Unavailable("narrative_policy_world_phase_provenance_unavailable");
            return phase.ReadTextAsync(path);
        }
        return source.ReadTextAsync(role, path, "original-authentication", lease);
    }
    /// <summary>Preserves the original synchronous context alternatives and explicit asynchronous lease branch.</summary>
    /// <param name="role">The role retained or checked by this operation.</param>
    /// <param name="path">The path retained or checked by this operation.</param>
    /// <param name="lease">The lease retained or checked by this operation.</param>
    /// <param name="source">Explicit invocation observations, or null for the unchanged ordinary route.</param>
    private string? ReadNarrativeAuxiliaryTextSync(string role, string path,
        FileSystemManager.CanonicalWriteLease? lease, OriginalLookupObservation source)
    {
        if (lease is null && CurrentWorldReadPhase() is { } phase)
        {
            source.Unavailable("narrative_policy_world_phase_provenance_unavailable");
            return phase.ReadTextSync(path);
        }
        // The original explicit request-context branch uses async text with its
        // existing blocking caller; retain that branch instead of changing reader family.
        return lease is null ? source.ReadTextSync(role, path, "original-context") :
            source.ReadTextAsync(role, path, "original-context", lease).GetAwaiter().GetResult();
    }
    /// <summary>Retains each original rollback authentication read through its existing reader.</summary>
    /// <param name="role">The role retained or checked by this operation.</param>
    /// <param name="path">The path retained or checked by this operation.</param>
    /// <param name="lease">The lease retained or checked by this operation.</param>
    /// <param name="source">Explicit invocation observations, or null for the unchanged ordinary route.</param>
    private byte[]? ReadNarrativeAuxiliaryBytesSync(string role, string path,
        FileSystemManager.CanonicalWriteLease? lease, OriginalLookupObservation source)
    {
        if (!PendingTurnSnapshotAuthority.IsSafeRelativePath(path)) return null;
        if (lease is null && CurrentWorldReadPhase() is { } phase)
        {
            source.Unavailable("narrative_policy_world_phase_provenance_unavailable");
            return phase.ReadBytesSync(path);
        }
        return lease is null ? source.ReadBytesSync(role, path, "original-rollback") :
            source.ReadBytesAsync(role, path, "original-rollback", lease).GetAwaiter().GetResult();
    }

    /// <summary>The closed eleven logical roles consumed by ordinary narrative policy.</summary>
    internal enum NarrativeSourceRole { Narrative, Interface, Debug, Ready, Achievements, ActiveSkills, PassiveSkills, SkillMastery, RegularQuests, QuestHistory, PlotOutline }

    /// <summary>Detached diagnostic DATA for one actual read or policy observation.</summary>
    /// <param name="Ordinal">The ordinal retained or checked by this operation.</param>
    /// <param name="Role">The role retained or checked by this operation.</param>
    /// <param name="Path">The path retained or checked by this operation.</param>
    /// <param name="Route">The route retained or checked by this operation.</param>
    /// <param name="State">The state retained or checked by this operation.</param>
    /// <param name="Bytes">The bytes retained or checked by this operation.</param>
    internal sealed record NarrativeSourceOccurrence(long Ordinal, NarrativeSourceRole Role, string Path,
        string Route, string State, byte[]? Bytes);
    /// <summary>Detached invocation diagnostics; counters describe actual captured work, not publication permission.</summary>
    /// <param name="Completed">The completed retained or checked by this operation.</param>
    /// <param name="Disposed">The disposed retained or checked by this operation.</param>
    /// <param name="Fault">The fault retained or checked by this operation.</param>
    /// <param name="ProjectorCalls">The projector calls retained or checked by this operation.</param>
    /// <param name="WritesStarted">The writes started retained or checked by this operation.</param>
    /// <param name="WritesReturned">The writes returned retained or checked by this operation.</param>
    /// <param name="ProjectedJson">The projected json retained or checked by this operation.</param>
    /// <param name="ProjectionChanged">The projection changed retained or checked by this operation.</param>
    /// <param name="WriterRoute">The writer route retained or checked by this operation.</param>
    /// <param name="WriterPreimageSha256">The writer preimage sha256 retained or checked by this operation.</param>
    /// <param name="WriterPreimageIdentity">The writer preimage identity retained or checked by this operation.</param>
    /// <param name="WriterOutcome">The writer outcome retained or checked by this operation.</param>
    /// <param name="Occurrences">The occurrences retained or checked by this operation.</param>
    /// <param name="TimestampCalls">The timestamp calls retained or checked by this operation.</param>
    /// <param name="TimestampIssues">The timestamp issues retained or checked by this operation.</param>
    internal sealed record NarrativeSourceDiagnostic(bool Completed, bool Disposed, string? Fault,
        int ProjectorCalls, int WritesStarted, int WritesReturned, int TimestampCalls, ValidationIssue[] TimestampIssues,
        string? ProjectedJson, bool ProjectionChanged,
        string? WriterRoute, string? WriterPreimageSha256, PhysicalFileAuthority.FileIdentity? WriterPreimageIdentity,
        string? WriterOutcome, NarrativeSourceOccurrence[] Occurrences);

    /// <summary>Explicit per-invocation source ledger. Its lifetime ends with the pass, including failed invocations.</summary>
    internal sealed class NarrativeInterfaceSourceSession : IDisposable
    {
        private readonly FileSystemManager _files;
        private readonly string? _generation;
        private readonly List<NarrativeSourceOccurrence> _occurrences = [];
        private readonly List<(string Session, string Request, string Token, int Turn)> _contexts = [];
        private string? _fault, _projectionJson, _suppliedManifest;
        private bool _projectionChanged;
        private long _ordinal;
        private bool _completed, _disposed;
        private int _projectorCalls, _writesStarted, _writesReturned, _timestampCalls;
        private readonly List<ValidationIssue> _timestampIssues = [];
        private NarrativeWriteObservation? _write;
        private readonly Guid _observationInvocation;
        private readonly object? _observationEpoch;
        private int _observationInitialClosed, _observationDisposed;

        /// <summary>Creates an invocation ledger bound to its actual manager and current logical generation.</summary>
        /// <param name="files">The files retained or checked by this operation.</param>
        /// <param name="producerIssuer">The actual private producer issuer, or null for a manually created unattested session.</param>
        internal NarrativeInterfaceSourceSession(FileSystemManager files, object? producerIssuer = null)
        {
            _files = files;
            _observationInvocation = ReferenceEquals(producerIssuer, NarrativePassIssuer) ? Guid.NewGuid() : Guid.Empty;
            _observationEpoch = _observationInvocation != Guid.Empty ? files.CaptureCanonicalReadObservationEpoch() : null;
            if (SessionOperationContext.TryGetExpectedGeneration(files.BasePath, out var bound)) _generation = bound;
            else
            {
                try { _generation = files.ObserveExistingHelperGeneration(); }
                catch (Exception error) { Fault(error); }
            }
        }

        /// <summary>Reads actual text through the existing ordinary or declared reader and latches faults before domain catches.</summary>
        /// <param name="logicalPath">The originating closed narrative logical role.</param>
        /// <param name="path">The path retained or checked by this operation.</param>
        /// <param name="route">The actual selected reader route.</param>
        /// <param name="lease">The lease retained or checked by this operation.</param>
        internal async Task<string?> ReadTextAsync(string logicalPath, string path, string route,
            FileSystemManager.CanonicalWriteLease? lease = null)
        {
            var sink = BeginRead(logicalPath, path, route);
            try { return lease is null ? await _files.ReadFileAsync(path, sink) : await _files.ReadFileAsync(lease, path, sink); }
            catch (Exception error) { Fault(error); throw; }
        }
        /// <summary>Reads actual bytes through the existing ordinary or declared reader and latches faults before domain catches.</summary>
        /// <param name="logicalPath">The originating closed narrative logical role.</param>
        /// <param name="path">The path retained or checked by this operation.</param>
        /// <param name="route">The actual selected reader route.</param>
        /// <param name="lease">The lease retained or checked by this operation.</param>
        internal async Task<byte[]?> ReadBytesAsync(string logicalPath, string path, string route,
            FileSystemManager.CanonicalWriteLease? lease = null)
        {
            var sink = BeginRead(logicalPath, path, route);
            try { return lease is null ? await _files.ReadFileBytesObservedAsync(path, sink) : await _files.ReadFileBytesAsync(lease, path, sink); }
            catch (Exception error) { Fault(error); throw; }
        }
        /// <summary>Preserves synchronous text reads and their actual public followups.</summary>
        /// <param name="logicalPath">The originating closed narrative logical role.</param>
        /// <param name="path">The path retained or checked by this operation.</param>
        /// <param name="route">The actual selected reader route.</param>
        /// <param name="lease">The lease retained or checked by this operation.</param>
        internal string? ReadTextSync(string logicalPath, string path, string route,
            FileSystemManager.CanonicalWriteLease? lease = null)
        {
            var sink = BeginRead(logicalPath, path, route);
            try { return lease is null ? _files.ReadFileSync(path, sink) : _files.ReadFileSync(lease, path, sink); }
            catch (Exception error) { Fault(error); throw; }
        }
        /// <summary>Preserves synchronous byte reads and their actual public followups.</summary>
        /// <param name="logicalPath">The originating closed narrative logical role.</param>
        /// <param name="path">The path retained or checked by this operation.</param>
        /// <param name="route">The actual selected reader route.</param>
        /// <param name="lease">The lease retained or checked by this operation.</param>
        internal byte[]? ReadBytesSync(string logicalPath, string path, string route,
            FileSystemManager.CanonicalWriteLease? lease = null)
        {
            var sink = BeginRead(logicalPath, path, route);
            try { return lease is null ? _files.ReadFileBytesSync(path, sink) : _files.ReadFileBytesSync(lease, path, sink); }
            catch (Exception error) { Fault(error); throw; }
        }
        /// <summary>Creates a sealed explicit sink for one logical source lookup.</summary>
        /// <param name="logicalPath">The originating closed narrative logical role.</param>
        /// <param name="physicalPath">The actual selected session-relative physical leaf.</param>
        /// <param name="route">The actual selected reader route.</param>
        internal NarrativeReadObservation BeginRead(string logicalPath, string physicalPath, string route) =>
            new(this, Role(logicalPath), physicalPath, route);
        /// <summary>Records an actual branch disposition without claiming a physical read.</summary>
        /// <param name="logicalPath">The originating closed narrative logical role.</param>
        /// <param name="route">The actual selected reader route.</param>
        /// <param name="state">The state retained or checked by this operation.</param>
        internal void Policy(string logicalPath, string route, string state) => Add(Role(logicalPath), logicalPath, route, state, null);
        /// <summary>Latches a failure without changing ordinary exceptions or issues.</summary>
        /// <param name="error">The error retained or checked by this operation.</param>
        internal void Fault(Exception error) => _fault ??= error.GetType().Name + ": " + error.Message;
        /// <summary>Latches incomplete source provenance without changing ordinary diagnostics.</summary>
        /// <param name="reason">The reason retained or checked by this operation.</param>
        internal void Unavailable(string reason) => _fault ??= reason;
        /// <summary>Retains actual original context and full supplied manifest DATA within this invocation.</summary>
        /// <param name="sessionId">The session id retained or checked by this operation.</param>
        /// <param name="requestId">The request id retained or checked by this operation.</param>
        /// <param name="token">The token retained or checked by this operation.</param>
        /// <param name="turn">The turn retained or checked by this operation.</param>
        /// <param name="provenance">The actual supplied, scoped or physical lookup disposition.</param>
        /// <param name="manifestData">Detached serialization of the actually supplied or selected manifest.</param>
        internal void OriginalContext(string sessionId, string requestId, string token, int turn, string provenance, string manifestData)
        {
            if (_suppliedManifest is not null && _suppliedManifest != manifestData) Unavailable("narrative_policy_original_data_changed");
            _suppliedManifest ??= manifestData;
            _contexts.Add((sessionId, requestId, token, turn));
            Policy("output/narrative_response.json", "original-context", provenance);
        }
        /// <summary>Seals the source closure after the ordinary narrative invocation returns.</summary>
        internal void Complete()
        {
            if (!_occurrences.Any(value => value.Path == "output/narrative_response.json" && (value.State is "Present" or "Absent")))
                Unavailable("narrative_policy_missing_actual_narrative_read");
            if (!_disposed) _completed = true;
            Interlocked.Exchange(ref _observationInitialClosed, 1);
        }
        /// <summary>Retains the original computed projector result without evaluating it again.</summary>
        /// <param name="json">The json retained or checked by this operation.</param>
        /// <param name="changed">The changed retained or checked by this operation.</param>
        internal void Projection(string json, bool changed) { _projectorCalls++; _projectionJson = json; _projectionChanged = changed; }
        /// <summary>Counts the existing timestamp policy call immediately before its one actual invocation.</summary>
        internal void BeginTimestampPolicy()
        {
            _timestampCalls++;
            Policy("output/narrative_response.json", "timestamp-policy", "Started");
        }

        /// <summary>Retains only the issues emitted by the actual timestamp call; no timestamp is parsed or sampled here.</summary>
        /// <param name="issues">The ordinary ordered issues after the actual call returned.</param>
        /// <param name="issueStart">The original issue count immediately before the call.</param>
        internal void ReturnedTimestampPolicy(IReadOnlyList<ValidationIssue> issues, int issueStart)
        {
            _timestampIssues.AddRange(issues.Skip(issueStart).Select(WoundAcceptedTurnData.CloneIssue));
            Policy("output/narrative_response.json", "timestamp-policy", "Returned:IssuesAdded=" + (issues.Count - issueStart));
        }

        /// <summary>Begins observation of the actual normalization write, never a replacement writer.</summary>
        /// <param name="path">The path retained or checked by this operation.</param>
        internal NarrativeWriteObservation BeginWrite(string path)
        {
            _writesStarted++;
            if (_write is not null) Unavailable("narrative_policy_multiple_writes");
            return _write = new(this, path);
        }
        /// <summary>Matches all actually selected original context DATA to the authenticated consumer frame.</summary>
        /// <param name="sessionId">The session id retained or checked by this operation.</param>
        /// <param name="requestId">The request id retained or checked by this operation.</param>
        /// <param name="token">The token retained or checked by this operation.</param>
        /// <param name="turn">The turn retained or checked by this operation.</param>
        internal void RequireOriginalContext(string sessionId, string requestId, string token, int turn)
        {
            RequireUsable();
            if (_contexts.Count == 0 || _contexts.Any(value => value != (sessionId, requestId, token, turn)))
                throw Refuse("narrative_policy_original_context_changed");
        }
        /// <summary>Freshly checks exact source bytes and authenticated owned transitions without semantic reevaluation.</summary>
        /// <param name="files">The files retained or checked by this operation.</param>
        /// <param name="lease">The lease retained or checked by this operation.</param>
        /// <param name="authenticatedInterface">The authenticated interface retained or checked by this operation.</param>
        internal async Task RequireCurrentAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            MortalJointInterfacePublication.Record? authenticatedInterface)
        {
            using var timing = files.MeasureOperation("NarrativePolicy.Recheck");
            RequireUsable();
            try
            {
                if (!ReferenceEquals(files, _files)) throw new InvalidDataException("narrative_policy_foreign_files");
                files.EnsureCanonicalWriteLeaseActive(lease);
                if (!files.IsCurrentSessionGeneration(lease, _generation)) throw new InvalidDataException("narrative_policy_generation_changed");
                using var fresh = FileSystemManager.SuppressValidationReuse();
                using var observationRun = new ObservationRun(NarrativePassIssuer);
                // Preserve every original occurrence. A distinct path needs one
                // fresh raw check here; repeated original computations are never replayed.
                var checkedPaths = new HashSet<string>(StringComparer.Ordinal);
                foreach (var occurrence in _occurrences.Where(value => value.State is "Present" or "Absent" || value.Route == "lookup-presence"))
                {
                    if (occurrence.Route == "lookup-presence")
                    {
                        if (files.FileExists(lease, occurrence.Path) != (occurrence.State == "Exists"))
                            throw new InvalidDataException("narrative_policy_presence_changed: " + occurrence.Path);
                        continue;
                    }
                    if (!checkedPaths.Add(occurrence.Path)) continue;
                    var expected = occurrence.Bytes;
                    if (_write is { Returned: true } writer && writer.Path == occurrence.Path) expected = writer.After;
                    var current = files.HasCanonicalReadObservation
                        ? await files.ReadNarrativeRecheckBytesAsync(lease, occurrence.Path,
                            new RecheckReadObservation(NarrativePassIssuer, this, observationRun))
                        : await files.ReadFileBytesAsync(lease, occurrence.Path);
                    if (Same(expected, current)) continue;
                    if (occurrence.Role == NarrativeSourceRole.Interface && occurrence.Path == MortalJointInterfacePublication.InterfacePath &&
                        authenticatedInterface is { State: "Published", HasErrors: false } accepted &&
                        Same(expected, accepted.BeforeBytes) && Same(current, accepted.AfterBytes)) continue;
                    throw new InvalidDataException("narrative_policy_source_changed: " + occurrence.Path);
                }
                RequireUsable();
            }
            catch (Exception error) { Fault(error); throw; }
        }
        /// <summary>Copies every occurrence and actual computation count without quotas.</summary>
        internal NarrativeSourceDiagnostic CaptureDiagnostic() => new(_completed, _disposed, _fault, _projectorCalls,
            _writesStarted, _writesReturned, _timestampCalls, _timestampIssues.Select(WoundAcceptedTurnData.CloneIssue).ToArray(),
            _projectionJson, _projectionChanged, _write?.Route,
            _write?.PreimageSha256, _write?.PreimageIdentity, _write?.Outcome, _occurrences.Select(value => value with { Bytes = value.Bytes?.ToArray() }).ToArray());
        /// <summary>Rejects disposed, incomplete, faulted or uncertain invocation DATA.</summary>
        private void RequireUsable()
        {
            if (_disposed || !_completed || _fault is not null || _write is { Returned: false })
                throw Refuse("narrative_policy_source_unavailable: " + (_fault ?? (_disposed ? "expired" : "incomplete")));
        }
        /// <summary>Latches the refusal so restoration cannot revive the same closure.</summary>
        /// <param name="reason">The reason retained or checked by this operation.</param>
        private InvalidDataException Refuse(string reason) { Unavailable(reason); return new(reason); }
        /// <summary>Retains an ordered actual occurrence and latches contradictory consumed images.</summary>
        /// <param name="role">The role retained or checked by this operation.</param>
        /// <param name="path">The path retained or checked by this operation.</param>
        /// <param name="route">The actual selected reader route.</param>
        /// <param name="state">The state retained or checked by this operation.</param>
        /// <param name="bytes">The bytes retained or checked by this operation.</param>
        private void Add(NarrativeSourceRole role, string path, string route, string state, byte[]? bytes)
        {
            if (_disposed || _completed) { Unavailable("narrative_policy_observation_after_close"); return; }
            if (state is "Present" or "Absent")
            {
                var previous = _occurrences.LastOrDefault(value => value.Path == path && (value.State is "Present" or "Absent"));
                if (previous is not null && !Same(previous.Bytes, bytes) &&
                    !(_write is { Returned: true } writer && writer.Path == path && Same(previous.Bytes, writer.Before) && Same(bytes, writer.After)))
                    Unavailable("narrative_policy_observed_source_changed: " + path);
            }
            _occurrences.Add(new(++_ordinal, role, path, route, state, bytes?.ToArray()));
        }
        /// <summary>Compares exact presence and raw content.</summary>
        /// <param name="first">The first retained or checked by this operation.</param>
        /// <param name="second">The second retained or checked by this operation.</param>
        private static bool Same(byte[]? first, byte[]? second) => first is null ? second is null : second is not null && first.AsSpan().SequenceEqual(second);
        /// <summary>Resolves only one of the eleven closed narrative source roles.</summary>
        /// <param name="path">The path retained or checked by this operation.</param>
        private static NarrativeSourceRole Role(string path) => path switch
        {
            "output/narrative_response.json" => NarrativeSourceRole.Narrative,
            "output/interface_updates.json" => NarrativeSourceRole.Interface,
            "output/debug_logs.json" => NarrativeSourceRole.Debug,
            "ready/turn_complete.json" => NarrativeSourceRole.Ready,
            "game_state/meta/achievements.json" => NarrativeSourceRole.Achievements,
            "game_state/player/skills_active.json" => NarrativeSourceRole.ActiveSkills,
            "game_state/player/skills_passive.json" => NarrativeSourceRole.PassiveSkills,
            "game_state/player/skill_mastery.json" => NarrativeSourceRole.SkillMastery,
            "game_state/quests/regular_quests.json" => NarrativeSourceRole.RegularQuests,
            "game_state/quests/quest_history.json" => NarrativeSourceRole.QuestHistory,
            "game_state/quests/plot_outline.json" => NarrativeSourceRole.PlotOutline,
            _ => throw new InvalidDataException("Narrative source role is outside its closed contract: " + path)
        };
        /// <summary>Invalidates the invocation and releases retained buffers.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; Interlocked.Exchange(ref _observationDisposed, 1); _occurrences.Clear(); _contexts.Clear(); _timestampIssues.Clear(); _projectionJson = null; _suppliedManifest = null; _write?.Clear(); _write = null;
        }

        /// <summary>A sealed sink passed explicitly through the same physical reader, before its public callbacks.</summary>
        /// <param name="owner">The owner retained or checked by this operation.</param>
        /// <param name="role">The role retained or checked by this operation.</param>
        /// <param name="path">The path retained or checked by this operation.</param>
        /// <param name="route">The actual selected reader route.</param>
        internal sealed class NarrativeReadObservation(NarrativeInterfaceSourceSession owner, NarrativeSourceRole role, string path, string route)
        {
            private readonly List<Exception> _failures = [];
            /// <summary>Creates DATA provenance from this actual explicit source sink, never from a global timer.</summary>
            internal ReadOrigin Origin => new(NarrativePassIssuer, owner, null);
            /// <summary>Retains actual source bytes before later callbacks and distinguishes reused DATA.</summary>
            /// <param name="bytes">The bytes retained or checked by this operation.</param>
            /// <param name="physical">Whether this occurrence came from the actual source rather than reused DATA.</param>
            internal void Snapshot(byte[]? bytes, bool physical = true)
            {
                if (!physical) owner.Unavailable("narrative_policy_partial_source_provenance: " + path);
                owner.Add(role, path, route, bytes is null ? "Absent" : "Present", bytes);
            }
            /// <summary>Retains the existing metadata presence observation without adding a byte read.</summary>
            /// <param name="present">The present retained or checked by this operation.</param>
            internal void Presence(bool present) => owner.Add(role, path, route, present ? "Exists" : "Missing", null);
            /// <summary>Latches a failure without changing ordinary exceptions or issues.</summary>
            /// <param name="error">The error retained or checked by this operation.</param>
            internal void Fault(Exception error)
            {
                if (_failures.Any(prior => ReferenceEquals(prior, error))) return;
                _failures.Add(error);
                owner.Add(role, path, route, "SourceFault:" + error.GetType().Name, null);
                owner.Fault(error);
            }
        }

        /// <summary>Actual completed-pass recheck lifetime; it is not appended to the sealed source ledger.</summary>
        internal sealed class ObservationRun : IDisposable
        {
            private int _closed;
            /// <summary>Observes only the atomic diagnostic recheck-run lifetime.</summary>
            internal bool Open => Volatile.Read(ref _closed) == 0;
            /// <summary>Opens a run only for the actual private producer issuer.</summary>
            internal ObservationRun(object issuer)
            {
                if (!ReferenceEquals(issuer, NarrativePassIssuer)) throw new InvalidOperationException("Actual N observation issuer required.");
            }
            /// <summary>Ends only diagnostic provenance, without changing authority DATA or a borrowed lease.</summary>
            public void Dispose() => Interlocked.Exchange(ref _closed, 1);
        }

        /// <summary>Closed diagnostic-only recheck arm over the same actual completed invocation.</summary>
        internal sealed class RecheckReadObservation
        {
            /// <summary>Carries the same actual completed invocation and current recheck run.</summary>
            internal ReadOrigin Origin { get; }
            /// <summary>Creates diagnostic-only provenance for one actual recheck source call.</summary>
            internal RecheckReadObservation(object issuer, NarrativeInterfaceSourceSession owner, ObservationRun run)
            { Origin = new ReadOrigin(issuer, owner, run); }
        }

        /// <summary>Private-producer provenance; detached observation IDs grant no engine or source authority.</summary>
        internal sealed class ReadOrigin
        {
            private readonly NarrativeInterfaceSourceSession _owner;
            private readonly ObservationRun? _run;
            /// <summary>Binds the immutable observation identity to the actual private producer and optional recheck run.</summary>
            internal ReadOrigin(object issuer, NarrativeInterfaceSourceSession owner, ObservationRun? run)
            {
                if (!ReferenceEquals(issuer, NarrativePassIssuer)) throw new InvalidOperationException("Actual N observation issuer required.");
                _owner = owner; _run = run;
            }
            /// <summary>Checks the actual explicit source owner and live capture/recheck run, without reading files.</summary>
            /// <param name="files">The reader's actual manager.</param>
            /// <param name="invocation">Receives immutable DATA identity, never an importable owner.</param>
            /// <param name="recheck">Whether this is the actual completed owner's fresh recheck run.</param>
            /// <returns>True only for actual producer-attested currently live source work.</returns>
            internal bool TryCapture(FileSystemManager files, out Guid invocation, out bool recheck)
            {
                invocation = _owner._observationInvocation; recheck = _run is not null;
                return invocation != Guid.Empty && ReferenceEquals(files, _owner._files) && Volatile.Read(ref _owner._observationDisposed) == 0 &&
                    _owner._observationEpoch is not null && files.MatchesCanonicalReadObservationEpoch(_owner._observationEpoch) &&
                    (_run is null ? Volatile.Read(ref _owner._observationInitialClosed) == 0 :
                        Volatile.Read(ref _owner._observationInitialClosed) != 0 && _run.Open);
            }
        }

        /// <summary>Retains actual publication preimage/outcome and public owning return, without selecting a different writer.</summary>
        /// <param name="owner">The owner retained or checked by this operation.</param>
        /// <param name="path">The path retained or checked by this operation.</param>
        internal sealed class NarrativeWriteObservation(NarrativeInterfaceSourceSession owner, string path)
        {
            internal string Path => path;
            internal byte[]? Before { get; private set; }
            internal byte[]? After { get; private set; }
            internal bool Returned { get; private set; }
            private bool _preimage, _published;
            internal string? Route { get; private set; }
            internal string? PreimageSha256 { get; private set; }
            internal PhysicalFileAuthority.FileIdentity? PreimageIdentity { get; private set; }
            internal string? Outcome { get; private set; }
            /// <summary>Retains the trusted writer preimage from its actual publication input.</summary>
            /// <param name="bytes">The bytes retained or checked by this operation.</param>
            internal void TrustedBefore(byte[]? bytes)
            {
                Route = "Trusted"; Before = bytes?.ToArray(); _preimage = true;
                PreimageSha256 = bytes is null ? null : Convert.ToHexString(SHA256.HashData(bytes));
            }
            /// <summary>Retains the actual trusted publication outcome before facade handling.</summary>
            /// <param name="outcome">The outcome retained or checked by this operation.</param>
            internal void TrustedOutcome(TrustedLocalPublicationOutcome outcome) =>
                Outcome = outcome.Disposition + ":" + outcome.Publication?.TransactionId;
            /// <summary>Retains the existing opened destination identity and digest and joins the consumed preimage.</summary>
            /// <param name="present">The present retained or checked by this operation.</param>
            /// <param name="hash">The original opened destination digest, or null for proved absence.</param>
            /// <param name="identity">The original opened destination identity, or null for proved absence.</param>
            internal void CanonicalBefore(bool present, string? hash, PhysicalFileAuthority.FileIdentity? identity = null)
            {
                Route = "Canonical"; PreimageSha256 = hash; PreimageIdentity = identity;
                var consumed = owner._occurrences.LastOrDefault(value => value.Path == path && (value.State is "Present" or "Absent"));
                if (present != (identity is not null) || consumed is null || present != (consumed.Bytes is not null) ||
                    (present && !string.Equals(Convert.ToHexString(SHA256.HashData(consumed.Bytes!)), hash, StringComparison.OrdinalIgnoreCase)))
                    owner.Unavailable("narrative_policy_writer_preimage_changed");
                else { Before = consumed.Bytes?.ToArray(); _preimage = true; }
            }
            /// <summary>Retains actual committed desired bytes from the existing writer.</summary>
            /// <param name="bytes">The bytes retained or checked by this operation.</param>
            internal void Published(byte[] bytes) { After = bytes.ToArray(); _published = true; Outcome ??= "Committed"; }
            /// <summary>Records success only after the actual public owning release returned.</summary>
            internal void PublicReturned()
            {
                var consumed = owner._occurrences.LastOrDefault(value => value.Path == path && (value.State is "Present" or "Absent"));
                if (!_preimage || !_published || consumed is null || !Same(Before, consumed.Bytes))
                    owner.Unavailable("narrative_policy_writer_incomplete_or_preimage_changed");
                else { Returned = true; owner._writesReturned++; }
            }
            /// <summary>Latches a failure without changing ordinary exceptions or issues.</summary>
            /// <param name="error">The error retained or checked by this operation.</param>
            internal void Fault(Exception error) => owner.Fault(error);
            /// <summary>Releases retained writer preimage, afterimage and outcome references.</summary>
            internal void Clear() { Before = null; After = null; PreimageIdentity = null; PreimageSha256 = null; Outcome = null; }
        }
    }
}
