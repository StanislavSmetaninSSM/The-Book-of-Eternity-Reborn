using System.Runtime.ExceptionServices;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private readonly object _worldReadGate = new();
    private readonly AsyncLocal<WorldValidationInvocation?> _worldValidationInvocation = new();
    private int _worldValidationCalls, _worldOverrideMutations;
    private WorldValidationInvocation? _worldReservation;
    private WorldReadPhase? _worldReadPhase;

    /// <summary>
    /// Identifies an admitted validator call and its exclusive shared-state reservations.
    /// </summary>
    /// <param name="worldReserved">
    /// Whether the browser World read phase is reserved.
    /// </param>
    /// <param name="metaReserved">
    /// Whether the browser Meta read phase is reserved.
    /// </param>
    /// <param name="pendingReserved">
    /// Whether one console pending-snapshot invocation is reserved; defaults to false.
    /// </param>
    private sealed class WorldValidationInvocation(bool worldReserved, bool metaReserved, bool pendingReserved = false)
    {
        internal readonly bool WorldReserved = worldReserved, MetaReserved = metaReserved;
        internal readonly bool PendingReserved = pendingReserved;
        /// <summary>
        /// Whether this invocation exclusively reserves any validation state.
        /// </summary>
        internal bool Reserved => WorldReserved || MetaReserved || PendingReserved;
        internal bool Closed;
    }

    /// <summary>
    /// Admits an existing validation route before shared-cache reset, rejecting conflicting reservations.
    /// </summary>
    /// <param name="gameState">
    /// Whether this call validates game-state phases rather than a response.
    /// </param>
    /// <param name="selection">
    /// Selected game-state phases when <paramref name="gameState"/> is true; null for response validation.
    /// </param>
    /// <returns>
    /// The invocation identity that must be released after its call.
    /// </returns>
    private WorldValidationInvocation AdmitWorldValidation(bool gameState, GameStateValidationSelection? selection = null)
    {
        _pendingSnapshotInvocation.Value?.RequireCurrent();
        var browser = gameState && _sameTurnOwnerInputs == null && _fs.HasBrowserOriginalOperation;
        lock (_worldReadGate)
        {
            if (_worldReservation != null) throw new InvalidOperationException("World validation already reserves this instance.");
            var world = browser && _prevalidatedPendingTurnSnapshotOverride != null &&
                selection!.Phases.Includes(GameStateValidationPhase.WorldQuestCombatFactionStateFiles);
            var meta = browser && _prevalidatedPendingTurnSnapshotOverride != null &&
                selection!.Phases.Includes(GameStateValidationPhase.MetaMiscStateFiles) &&
                MetaControlShapeCurrentPaths.Any(selection.IncludesStateFile);
            var reserve = world || meta;
            if (reserve && (_worldValidationCalls != 0 || _worldOverrideMutations != 0))
                throw new InvalidOperationException("World validation conflicts with an already admitted instance consumer.");
            var invocation = new WorldValidationInvocation(world, meta);
            _worldValidationCalls++;
            if (reserve) _worldReservation = invocation;
            return invocation;
        }
    }

    private Task<List<ValidationIssue>> ValidateGameStateInternalAsync(GameStateValidationSelection selection) =>
        StartAdmittedGameStateAsync(selection, metaControlShapesOnly: false);

    private Task<List<ValidationIssue>> StartAdmittedGameStateAsync(
        GameStateValidationSelection selection, bool metaControlShapesOnly, StoryService.AcceptedStoryAppend? acceptedStoryAppend = null,
        StoryService.MortalJointStoryProof? jointStoryProof = null)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var invocation = AdmitWorldValidation(true, selection);
        var previous = _worldValidationInvocation.Value;
        _worldValidationInvocation.Value = invocation;
        try { return RunAdmittedGameStateAsync(selection, invocation, metaControlShapesOnly, acceptedStoryAppend, jointStoryProof); }
        catch { ReleaseWorldValidation(invocation); throw; }
        finally { _worldValidationInvocation.Value = previous; }
    }

    private async Task<List<ValidationIssue>> RunAdmittedGameStateAsync(
        GameStateValidationSelection selection, WorldValidationInvocation invocation, bool metaControlShapesOnly, StoryService.AcceptedStoryAppend? acceptedStoryAppend,
        StoryService.MortalJointStoryProof? jointStoryProof)
    {
        try { return await ValidateGameStateCoreAsync(selection, metaControlShapesOnly, acceptedStoryAppend, jointStoryProof); }
        finally { ReleaseWorldValidation(invocation); }
    }

    private void ReleaseWorldValidation(WorldValidationInvocation invocation)
    {
        lock (_worldReadGate)
        {
            if (invocation.Closed) return;
            invocation.Closed = true;
            _worldValidationCalls--;
            if (ReferenceEquals(_worldReservation, invocation)) _worldReservation = null;
        }
    }

    /// <summary>
    /// Refuses foreign or retired invocation access to reserved shared validation caches.
    /// </summary>
    private void RequireWorldCacheAccess()
    {
        _pendingSnapshotInvocation.Value?.RequireCurrent();
        lock (_worldReadGate)
        {
            if (_worldReservation != null &&
                (!ReferenceEquals(_worldReservation, _worldValidationInvocation.Value) || _worldReservation.Closed))
                throw new InvalidOperationException("Foreign validation cannot access reserved World caches.");
        }
    }

    /// <summary>
    /// Admits override mutation only outside a conflicting validation reservation.
    /// </summary>
    /// <param name="withdrawal">
    /// Whether a refused withdrawal must also invalidate the current read proof.
    /// </param>
    private void BeginWorldOverrideMutation(bool withdrawal)
    {
        lock (_worldReadGate)
        {
            if (_worldReservation != null && (_worldReservation.PendingReserved || _worldReadPhase != null || _metaControlShapeReadPhase != null ||
                !ReferenceEquals(_worldReservation, _worldValidationInvocation.Value) || _worldReservation.Closed))
            {
                if (withdrawal)
                {
                    if (_pendingSnapshotReservation != null) _pendingSnapshotReservation.Invalidated = true;
                    if (_worldReadPhase != null) _worldReadPhase.Invalidated = true;
                    if (_metaControlShapeReadPhase != null) _metaControlShapeReadPhase.Invalidated = true;
                }
                throw new InvalidOperationException("Snapshot override cannot change during reserved World validation.");
            }
            _worldOverrideMutations++;
        }
    }
    private void EndWorldOverrideMutation() { lock (_worldReadGate) _worldOverrideMutations--; }

    private async Task RunWorldReadPhaseAsync(List<ValidationIssue> issues)
    {
#if DEBUG
        using var diagnosticPhase = _fs.BrowserDiagnosticLogical("WorldQuestCombatFactionStateFiles", null);
#endif
        var invocation = _worldValidationInvocation.Value;
        if (invocation is not { WorldReserved: true })
        {
            RequireWorldCacheAccess();
            await ValidateWorldQuestCombatFactionStateFilesCore(issues);
            return;
        }
        WorldReadPhase phase;
        lock (_worldReadGate)
        {
            if (!ReferenceEquals(_worldReservation, invocation) || invocation.Closed ||
                _worldReadPhase != null || _metaControlShapeReadPhase != null || _worldOverrideMutations != 0)
                throw new InvalidOperationException("World validation cannot enter its read phase.");
            phase = new WorldReadPhase(this, _prevalidatedPendingTurnSnapshotOverride!);
            _worldReadPhase = phase;
        }
        FileSystemManager.WorldValidationReadScope? physical = null;
        HashSet<string>? previousIds = null, previousNames = null;
        var isolated = false;
        Exception? primary = null;
        var localIssues = new List<ValidationIssue>();
        var completedItemIssueIndex = -1;
        var priorIssueCount = issues.Count;
        try
        {
            physical = _fs.BeginWorldValidationRead();
            if (physical == null)
            {
                // Genuine noneligible held/closing/draft context: retain the
                // instance reservation while running the original scalar path.
                lock (_worldReadGate) _worldReadPhase = null;
                await ValidateWorldQuestCombatFactionStateFilesCore(issues);
                return;
            }
            phase.Physical = physical;
            await physical.OpenAsync();
            lock (_worldReadGate)
            {
                previousIds = _knownCanonicalFactionIdsCache;
                previousNames = _knownCanonicalFactionNamesCache;
                _knownCanonicalFactionIdsCache = null;
                _knownCanonicalFactionNamesCache = null;
                isolated = true;
                phase.Active = true;
            }
            phase.RequireCurrent();
            await ValidateWorldQuestCombatFactionStateFilesCore(localIssues);
            phase.RequireCurrent();
            phase.ReadFailure?.Throw();
            completedItemIssueIndex = await CompareCompletedTreatmentItemsAsync(phase,
                issues.Concat(localIssues).ToArray());
            physical.RequireFullEndProof();
#if DEBUG
            using (_fs.BrowserDiagnosticLogical("WorldQuestCombatFactionStateFiles/fresh-readset", null))
#endif
            {
            await phase.RequireUnchangedReadsAsync();
            }
            phase.RequireCurrent();
        }
        catch (Exception error)
        {
            primary = phase.ReadFailure?.SourceException ?? error;
            if (!ReferenceEquals(primary, error))
                try { primary.Data["WorldReadPhase.LaterFailure"] = error; } catch { }
        }
        finally
        {
            lock (_worldReadGate)
            {
                if (isolated)
                {
                    _knownCanonicalFactionIdsCache = previousIds;
                    _knownCanonicalFactionNamesCache = previousNames;
                }
                phase.Active = false;
            }
            try { if (physical != null) await physical.DisposeAsync(); }
            catch (Exception close)
            {
                if (primary == null) primary = close;
                else
                {
                    // A hostile exception Data accessor cannot replace the real read refusal.
                    try { primary.Data["WorldReadPhase.CloseFailure"] = close; } catch { }
                }
            }
            finally { lock (_worldReadGate) _worldReadPhase = null; }
        }
        if (primary != null) ExceptionDispatchInfo.Capture(primary).Throw();
        physical!.RequireOwnerAfterClose();
        if (phase.Invalidated || !ReferenceEquals(phase.Manifest, _prevalidatedPendingTurnSnapshotOverride))
            throw new IOException("Original World validation snapshot context changed before issue publication.");
        if (completedItemIssueIndex >= 0)
        {
            if (completedItemIssueIndex < priorIssueCount) issues.RemoveAt(completedItemIssueIndex);
            else localIssues.RemoveAt(completedItemIssueIndex - priorIssueCount);
        }
        issues.AddRange(localIssues);
    }

    // Specific source-audited World leaves. New current dependencies must extend
    // this set and its closure test; signed paths come only from the bound image.
    private static readonly HashSet<string> WorldReadCurrentPaths = new(StringComparer.Ordinal) {
        "game_state/world/current_location.json", "game_state/world/world_map.json", "game_state/world/world_events.json",
        "game_state/world/rival_soul_arcs.json", "game_state/world/world_flags.json", "game_state/world/world_time.json",
        "game_state/world/weather.json", "game_state/world/progression.json", "game_state/control/progression_report.json",
        "game_state/control/pending_ink_actions.json", "game_state/quests/regular_quests.json", "game_state/quests/soul_quests.json",
        "game_state/quests/quest_history.json", "game_state/quests/plot_outline.json", "game_state/combat/enemies.json",
        "game_state/combat/allies.json", "game_state/combat/combat_log.json", "game_state/factions/faction_core.json",
        "game_state/factions/faction_structure.json", "game_state/factions/faction_resources.json",
        "game_state/factions/faction_projects.json", "game_state/factions/faction_custom.json", "game_state/factions/faction_chronicles.json",
        "game_state/control/validation_repair_request.json", "input/turn_request.json", "ready/turn_complete.json",
        PendingTurnSnapshotAuthority.AuthorityPath, PendingTurnSnapshotManifestPath
    };

    private WorldReadPhase? CurrentWorldReadPhase()
    {
        RequireWorldCacheAccess();
        var phase = _worldReadPhase;
        if (phase is not { Active: true }) return null;
        phase.RequireCurrent();
        return phase;
    }

    private sealed class WorldReadPhase(ValidationService service, ValidationPendingTurnSnapshotManifest manifest)
    {
        internal readonly ValidationPendingTurnSnapshotManifest Manifest = manifest;
        internal FileSystemManager.WorldValidationReadScope Physical = null!;
        internal FileSystemManager.CanonicalWriteLease Lease => Physical.Lease!;
        internal bool Active, Invalidated;
        internal ExceptionDispatchInfo? ReadFailure;
        private readonly Dictionary<string, byte[]?> _reads = new(StringComparer.Ordinal);
        internal void RequireCurrent()
        {
            service.RequireWorldCacheAccess();
            if (!Active || Invalidated || !ReferenceEquals(Manifest, service._prevalidatedPendingTurnSnapshotOverride))
                throw new IOException("Original World validation context changed.");
            Physical.RequireCurrent();
        }
        private void RequirePath(string path)
        {
            if (!WorldReadCurrentPaths.Contains(path) && !Manifest.Files.Values.Contains(path, StringComparer.Ordinal) &&
                !Manifest.RollbackBackups.Values.Contains(path, StringComparer.Ordinal))
                throw new InvalidOperationException("World validation read is outside its source-audited closure.");
        }
        internal async Task<byte[]?> ReadBytesAsync(string path)
        {
            RequireCurrent(); RequirePath(path);
            var bytes = await service._fs.ReadWorldValidationBytesAsync(Lease, path);
            RequireCurrent();
            Remember(path, bytes);
            return bytes;
        }
        internal byte[]? ReadBytesSync(string path)
        {
            RequireCurrent(); RequirePath(path);
            var bytes = service._fs.ReadWorldValidationBytesSync(Lease, path, notifyAttempt: true);
            RequireCurrent();
            Remember(path, bytes);
            return bytes;
        }
        internal async Task<string?> ReadTextAsync(string path)
        {
            RequireCurrent(); RequirePath(path);
            var result = await service._fs.ReadWorldValidationTextAsync(Lease, path);
            RequireCurrent(); Remember(path, result.Bytes);
            return result.Text;
        }
        internal string? ReadTextSync(string path) => ReadBytesSync(path) is { } bytes ? DecodePendingSnapshotText(bytes) : null;
        internal string? ReadNullableTextSync(string path, bool directCurrent = false)
        {
            // Actual authority checks are outside the legacy data/null catch.
            // A caught data fault is unverifiable, never recorded as absence.
            RequireCurrent(); RequirePath(path);
            var read = service._fs.ReadWorldValidationNullableBytesSync(Lease, path, notifyAttempt: !directCurrent);
            if (read.DataFailure != null)
            {
                ReadFailure ??= read.DataFailure;
                return null;
            }
            RequireCurrent(); Remember(path, read.Bytes);
            return read.Bytes == null ? null : DecodePendingSnapshotText(read.Bytes);
        }
        private void Remember(string path, byte[]? bytes)
        {
            if (_reads.TryGetValue(path, out var before))
            {
                if (!Same(before, bytes)) throw new IOException("World validation consumed changed current/signed bytes.");
            }
            else _reads.Add(path, bytes?.ToArray());
        }
        private static bool Same(byte[]? first, byte[]? second) => first == null ? second == null :
            second != null && first.AsSpan().SequenceEqual(second);
        internal async Task RequireUnchangedReadsAsync()
        {
            foreach (var (path, bytes) in _reads)
            {
                RequireCurrent();
                var current = await service._fs.ReadFileBytesAsync(Lease, path);
                RequireCurrent();
                if (!Same(bytes, current)) throw new IOException("World validation input changed before issue publication.");
            }
        }
    }
}
