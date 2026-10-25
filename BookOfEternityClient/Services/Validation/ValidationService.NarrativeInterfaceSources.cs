using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private Task<string?> ReadNarrativeSourceTextAsync(string path, NarrativeInterfaceSourceSession? source) =>
        source is null ? _fs.ReadFileAsync(path) : source.ReadTextAsync(path, path, "public-current");

    private void RecordNarrativeOriginalContext(NarrativeInterfaceSourceSession? source,
        ValidationPendingTurnSnapshotManifest manifest, string provenance) =>
        source?.OriginalContext(manifest.SessionId, manifest.RequestId, manifest.ManifestPayloadHash, manifest.TurnNumber, provenance);

    private async Task<byte[]?> ReadNarrativeOriginalBytesAsync(string role, string path,
        FileSystemManager.CanonicalWriteLease? lease, NarrativeInterfaceSourceSession source)
    {
        if (lease is null && CurrentWorldReadPhase() is { } phase)
        {
            source.Unavailable("narrative_policy_world_phase_provenance_unavailable");
            return await phase.ReadBytesAsync(path);
        }
        return await source.ReadBytesAsync(role, path, "original-snapshot", lease);
    }
    private Task<string?> ReadNarrativeAuxiliaryTextAsync(string role, string path,
        FileSystemManager.CanonicalWriteLease? lease, NarrativeInterfaceSourceSession source)
    {
        if (lease is null && CurrentWorldReadPhase() is { } phase)
        {
            source.Unavailable("narrative_policy_world_phase_provenance_unavailable");
            return phase.ReadTextAsync(path);
        }
        return source.ReadTextAsync(role, path, "original-authentication", lease);
    }
    private string? ReadNarrativeAuxiliaryTextSync(string role, string path,
        FileSystemManager.CanonicalWriteLease? lease, NarrativeInterfaceSourceSession source)
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
    private byte[]? ReadNarrativeAuxiliaryBytesSync(string role, string path,
        FileSystemManager.CanonicalWriteLease? lease, NarrativeInterfaceSourceSession source)
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
    internal sealed record NarrativeSourceOccurrence(long Ordinal, NarrativeSourceRole Role, string Path,
        string Route, string State, byte[]? Bytes);
    /// <summary>Detached invocation diagnostics; counters describe actual captured work, not publication permission.</summary>
    internal sealed record NarrativeSourceDiagnostic(bool Completed, bool Disposed, string? Fault,
        int ProjectorCalls, int WritesStarted, int WritesReturned, NarrativeSourceOccurrence[] Occurrences);

    /// <summary>Explicit per-invocation source ledger. Its lifetime ends with the pass, including failed invocations.</summary>
    internal sealed class NarrativeInterfaceSourceSession : IDisposable
    {
        private readonly FileSystemManager _files;
        private readonly string? _generation;
        private readonly List<NarrativeSourceOccurrence> _occurrences = [];
        private readonly List<(string Session, string Request, string Token, int Turn)> _contexts = [];
        private string? _fault;
        private long _ordinal;
        private bool _completed, _disposed;
        private int _projectorCalls, _writesStarted, _writesReturned;
        private NarrativeWriteObservation? _write;

        internal NarrativeInterfaceSourceSession(FileSystemManager files)
        {
            _files = files;
            if (SessionOperationContext.TryGetExpectedGeneration(files.BasePath, out var bound)) _generation = bound;
            else
            {
                try { _generation = files.ObserveExistingHelperGeneration(); }
                catch (Exception error) { Fault(error); }
            }
        }

        internal async Task<string?> ReadTextAsync(string logicalPath, string path, string route,
            FileSystemManager.CanonicalWriteLease? lease = null)
        {
            var sink = BeginRead(logicalPath, path, route);
            try { return lease is null ? await _files.ReadFileAsync(path, sink) : await _files.ReadFileAsync(lease, path, sink); }
            catch (Exception error) { Fault(error); throw; }
        }
        internal async Task<byte[]?> ReadBytesAsync(string logicalPath, string path, string route,
            FileSystemManager.CanonicalWriteLease? lease = null)
        {
            var sink = BeginRead(logicalPath, path, route);
            try { return lease is null ? await _files.ReadFileBytesObservedAsync(path, sink) : await _files.ReadFileBytesAsync(lease, path, sink); }
            catch (Exception error) { Fault(error); throw; }
        }
        internal string? ReadTextSync(string logicalPath, string path, string route,
            FileSystemManager.CanonicalWriteLease? lease = null)
        {
            var sink = BeginRead(logicalPath, path, route);
            try { return lease is null ? _files.ReadFileSync(path, sink) : _files.ReadFileSync(lease, path, sink); }
            catch (Exception error) { Fault(error); throw; }
        }
        internal byte[]? ReadBytesSync(string logicalPath, string path, string route,
            FileSystemManager.CanonicalWriteLease? lease = null)
        {
            var sink = BeginRead(logicalPath, path, route);
            try { return lease is null ? _files.ReadFileBytesSync(path, sink) : _files.ReadFileBytesSync(lease, path, sink); }
            catch (Exception error) { Fault(error); throw; }
        }
        internal NarrativeReadObservation BeginRead(string logicalPath, string physicalPath, string route) =>
            new(this, Role(logicalPath), physicalPath, route);
        internal void Policy(string logicalPath, string route, string state) => Add(Role(logicalPath), logicalPath, route, state, null);
        internal void Fault(Exception error) => _fault ??= error.GetType().Name + ": " + error.Message;
        internal void Unavailable(string reason) => _fault ??= reason;
        internal void OriginalContext(string sessionId, string requestId, string token, int turn, string provenance)
        {
            _contexts.Add((sessionId, requestId, token, turn));
            Policy("output/narrative_response.json", "original-context", provenance);
        }
        internal void Complete()
        {
            if (!_occurrences.Any(value => value.Path == "output/narrative_response.json" && (value.State is "Present" or "Absent")))
                Unavailable("narrative_policy_missing_actual_narrative_read");
            if (!_disposed) _completed = true;
        }
        internal void Projection() => _projectorCalls++;
        internal NarrativeWriteObservation BeginWrite(string path)
        {
            _writesStarted++;
            if (_write is not null) Unavailable("narrative_policy_multiple_writes");
            return _write = new(this, path);
        }
        internal void RequireOriginalContext(string sessionId, string requestId, string token, int turn)
        {
            RequireUsable();
            if (_contexts.Count == 0 || _contexts.Any(value => value != (sessionId, requestId, token, turn)))
                throw Refuse("narrative_policy_original_context_changed");
        }
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
                // Preserve every original occurrence. A distinct path needs one
                // fresh raw check here; repeated original computations are never replayed.
                var checkedPaths = new HashSet<string>(StringComparer.Ordinal);
                foreach (var occurrence in _occurrences.Where(value => value.State is "Present" or "Absent" or "Exists" or "Missing"))
                {
                    if (occurrence.State is "Exists" or "Missing")
                    {
                        if (files.FileExists(lease, occurrence.Path) != (occurrence.State == "Exists"))
                            throw new InvalidDataException("narrative_policy_presence_changed: " + occurrence.Path);
                        continue;
                    }
                    if (!checkedPaths.Add(occurrence.Path)) continue;
                    var expected = occurrence.Bytes;
                    if (_write is { Returned: true } writer && writer.Path == occurrence.Path) expected = writer.After;
                    var current = await files.ReadFileBytesAsync(lease, occurrence.Path);
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
        internal NarrativeSourceDiagnostic CaptureDiagnostic() => new(_completed, _disposed, _fault, _projectorCalls,
            _writesStarted, _writesReturned, _occurrences.Select(value => value with { Bytes = value.Bytes?.ToArray() }).ToArray());
        private void RequireUsable()
        {
            if (_disposed || !_completed || _fault is not null || _write is { Returned: false })
                throw Refuse("narrative_policy_source_unavailable: " + (_fault ?? (_disposed ? "expired" : "incomplete")));
        }
        private InvalidDataException Refuse(string reason) { Unavailable(reason); return new(reason); }
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
        private static bool Same(byte[]? first, byte[]? second) => first is null ? second is null : second is not null && first.AsSpan().SequenceEqual(second);
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
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; _occurrences.Clear(); _contexts.Clear(); _write?.Clear(); _write = null;
        }

        /// <summary>A sealed sink passed explicitly through the same physical reader, before its public callbacks.</summary>
        internal sealed class NarrativeReadObservation(NarrativeInterfaceSourceSession owner, NarrativeSourceRole role, string path, string route)
        {
            internal void Snapshot(byte[]? bytes, bool physical = true)
            {
                if (!physical) owner.Unavailable("narrative_policy_partial_source_provenance: " + path);
                owner.Add(role, path, route, bytes is null ? "Absent" : "Present", bytes);
            }
            internal void Presence(bool present) => owner.Add(role, path, route, present ? "Exists" : "Missing", null);
            internal void Fault(Exception error) => owner.Fault(error);
        }

        /// <summary>Retains actual publication preimage/outcome and public owning return, without selecting a different writer.</summary>
        internal sealed class NarrativeWriteObservation(NarrativeInterfaceSourceSession owner, string path)
        {
            internal string Path => path;
            internal byte[]? Before { get; private set; }
            internal byte[]? After { get; private set; }
            internal bool Returned { get; private set; }
            private bool _preimage, _published;
            internal void TrustedBefore(byte[]? bytes) { Before = bytes?.ToArray(); _preimage = true; }
            internal void CanonicalBefore(bool present, string? hash)
            {
                var consumed = owner._occurrences.LastOrDefault(value => value.Path == path && (value.State is "Present" or "Absent"));
                if (consumed is null || present != (consumed.Bytes is not null) ||
                    (present && !string.Equals(Convert.ToHexString(SHA256.HashData(consumed.Bytes!)), hash, StringComparison.OrdinalIgnoreCase)))
                    owner.Unavailable("narrative_policy_writer_preimage_changed");
                else { Before = consumed.Bytes?.ToArray(); _preimage = true; }
            }
            internal void Published(byte[] bytes) { After = bytes.ToArray(); _published = true; }
            internal void PublicReturned()
            {
                var consumed = owner._occurrences.LastOrDefault(value => value.Path == path && (value.State is "Present" or "Absent"));
                if (!_preimage || !_published || consumed is null || !Same(Before, consumed.Bytes))
                    owner.Unavailable("narrative_policy_writer_incomplete_or_preimage_changed");
                else { Returned = true; owner._writesReturned++; }
            }
            internal void Fault(Exception error) => owner.Fault(error);
            internal void Clear() { Before = null; After = null; }
        }
    }
}
