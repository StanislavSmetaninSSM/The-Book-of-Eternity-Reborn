using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

internal sealed class FileSystemManagerHooks
{
    /// <summary>
    /// Observes actual Windows path-walk managed metadata attempts; <see langword="null"/> disables diagnostics.
    /// The callback must not mutate storage or authority. Observer exceptions are ignored.
    /// </summary>
    internal Action<PathMetadataQuery>? PathMetadataQueryObserver { get; init; }

    /// <summary>
    /// Observes a named operation's begin and elapsed end; a missing observer disables timing.
    /// This diagnostic must not mutate game state or authority; observer exceptions are ignored.
    /// </summary>
    internal Action<string, string, TimeSpan>? OperationTimingObserver { get; init; }

    /// <summary>
    /// Observes complete recovery-segment counters at close without a quota; null disables export.
    /// The receiver and its descendants must not mutate storage or authority. Receiver exceptions are ignored.
    /// </summary>
    internal Action<RecoveryValidationMetrics>? RecoveryValidationObserver { get; init; }

#if DEBUG
    /// <summary>
    /// Observes an existing physical snapshot-evidence read; null disables the observation.
    /// The callback must not perform I/O, wait, or mutate storage.
    /// </summary>
    internal Action<string>? AfterSnapshotEvidenceRead { get; init; }
#endif
    /// <summary>
    /// Receives owned read-only load stage observations; null disables diagnostics.
    /// The callback must not mutate files, authority, settings or session state.
    /// </summary>
    internal Action<string>? LoadOperationObserver { get; init; }
    internal Action<TrustedLocalPublicationPhase, int>? LocalPublicationObserver { get; init; }
    /// <summary>
    /// Observes completed recovery boundaries while the canonical lease is held.
    /// A missing callback leaves normal recovery unchanged.
    /// </summary>
    internal Action<TrustedLocalPublicationPhase, int>? LocalPublicationRecoveryObserver { get; init; }
    internal Func<Task>? CanonicalWriteLockContendedAsync { get; init; }
    internal Func<Task>? BeforeMainBorrowRetainAsync { get; init; }
    internal Func<Task>? AfterBrowserOriginalPreflightAsync { get; init; }
    /// <summary>
    /// Test-only scheduling observation after browser recovery selects an existing
    /// journal name and before its fresh validation. Null leaves preflight unchanged.
    /// </summary>
    internal Action<string>? BrowserRecoveryJournalSelectedObserver { get; init; }
    // Read-only timing observations for the original admission profile. A
    // missing callback allocates no timer and changes no admission decision.
    internal Action<string, TimeSpan>? BrowserOriginalAdmissionTimingObserver { get; init; }
    internal Func<Task>? MainOwnerLockContendedAsync { get; init; }
    internal Func<Task>? SessionLifecycleLockContendedAsync { get; init; }
    internal Func<Task>? BeforeCanonicalWriteLockOpenAsync { get; init; }
    internal Func<Task>? AfterCanonicalWriteLockOpenedAsync { get; init; }
    internal Func<Task>? BeforeSessionLifecycleLockOpenAsync { get; init; }
    internal Func<Task>? AfterSessionLifecycleLockOpenedAsync { get; init; }
    internal Func<string, Task>? BeforeCanonicalMutationAsync { get; init; }
    /// <summary>
    /// Observes an actual canonical read after initial validation; null disables observation.
    /// The synchronous receiver and its descendants must not mutate storage or authority. Receiver faults are ignored.
    /// </summary>
    internal Action<string>? CanonicalReadActivityObserver { get; init; }
    /// <summary>
    /// Observes an actual exact physical read after initial validation; null disables observation.
    /// The receiver must not mutate storage or authority. Receiver faults are ignored and diagnostic reads bypass reuse.
    /// </summary>
    internal Action<string>? ExactPhysicalReadActivityObserver { get; init; }
    internal Func<string, Task>? BeforeCanonicalReadOpenAsync { get; init; }
    internal Func<string, Task>? AfterCanonicalReadInitialValidationAsync { get; init; }
    internal Func<Task>? SessionOperationClosingAsync { get; init; }
    internal Func<string, Task>? BeforeCanonicalMutationBoundaryAsync { get; init; }
    internal Func<string, Task>? AfterCanonicalMutationBoundaryValidatedAsync { get; init; }
    internal Func<string, Task>? BeforeRuntimeFileReadOpenAsync { get; init; }
    internal Func<string, Task>? AfterRuntimeFileReadOpenedAsync { get; init; }
    internal Func<string, Task>? AfterRuntimeFileReadInitialValidationAsync { get; init; }
    internal Func<string, Task>? AfterExactPhysicalReadInitialValidationAsync { get; init; }
    internal Func<string, Task>? AfterPhysicalFileAuthorityValidatedAsync { get; init; }
    internal Func<string, Task>? BeforePhysicalSourcePublishedAsync { get; init; }
    internal Func<string, Task>? AfterPhysicalFilePublishedAsync { get; init; }
    internal Func<string, Task>? BeforePhysicalRollbackAbsenceFinalValidationAsync { get; init; }
    internal Func<string, Task>? AfterCanonicalReadAttemptAsync { get; init; }
    internal Func<string, Task>? BeforeCanonicalExistenceFollowUpProbeAsync { get; init; }
    internal bool? SupportsReversibleFileReplacementOverride { get; init; }
    internal bool? SupportsDescriptorBoundCreateOnlyPublicationOverride { get; init; }
    internal Func<string, Task>? BeforeRuntimeFileCreateAsync { get; init; }
    internal Func<string, Task>? AfterRuntimeMutationBoundaryValidatedAsync { get; init; }
    internal Func<string, string, Task>? BeforeLoadDirectoryMoveAsync { get; init; }
    internal Func<string, string, Task>? AfterLoadStagingFinalValidationAsync { get; init; }
    internal Func<string, string, Task>? AfterLoadDirectoryMoveAsync { get; init; }
    internal Func<string, Task>? BeforeLoadPublicationGuardRootDeleteAsync { get; init; }
}

public enum CanonicalFileMutationResult
{
    Applied,
    Conflict
}

internal enum CanonicalWritePurpose
{
    SessionMutation,
    SessionReplacement,
    SessionFinalization,
    PublicationReadQuiescence,
    MainMetadata
}

internal sealed record CanonicalLoadTransactionPaths(
    string TransactionId,
    string TransactionRoot,
    string StagingSessionPath,
    string BackupSessionPath,
    string FailedSessionPath);

internal sealed record CanonicalWorkerApplyChange(
    string Path,
    byte[]? BaselineBytes,
    byte[]? AppliedBytes);

internal sealed record CanonicalWorkerApplyTransaction(
    string TransactionId,
    string TransactionRoot,
    TrustedLocalFilePublication.DeferredDecision? LocalDecision = null);

internal enum CanonicalMutationOperation
{
    Write,
    Delete
}

internal sealed record CanonicalMutationPublication(
    CanonicalMutationOperation Operation,
    PhysicalFileAuthority.FileIdentity PhysicalIdentity,
    string Sha256);

internal interface ICanonicalMutationIntentRecorder
{
    Task RecordMutationIntentAsync(
        string relativePath,
        byte[]? desiredContent);

    Task RecordMutationNonPublicationAsync(string relativePath);

    Task RecordMutationPublicationAsync(
        string relativePath,
        CanonicalMutationPublication publication);
}

/// <summary>
/// Manages the game_session directory structure per CLI API specification.
/// Creates all required directories and validates file system integrity.
/// </summary>
public partial class FileSystemManager
{
    internal sealed class AmbientCanonicalLeaseRegistration
    {
        private const int PendingState = 0;
        private const int ActiveState = 1;
        private const int InactiveState = 2;
        private AmbientCanonicalLeaseRegistration? _previous;
        private readonly ConcurrentDictionary<
            AmbientCanonicalLeaseRegistration,
            byte> _successors = new();
        private int _state = PendingState;

        internal AmbientCanonicalLeaseRegistration(
            AmbientCanonicalLeaseRegistration? previous)
        {
            _previous = previous;
            if (previous != null &&
                !previous.RegisterSuccessor(this))
            {
                PruneInactivePredecessors();
            }
        }

        internal AmbientCanonicalLeaseRegistration? Previous =>
            Volatile.Read(ref _previous);
        internal bool Active =>
            Volatile.Read(ref _state) == ActiveState;
        internal bool Inactive =>
            Volatile.Read(ref _state) == InactiveState;

        internal void Activate()
        {
            if (Interlocked.CompareExchange(
                    ref _state,
                    ActiveState,
                    PendingState) != PendingState)
            {
                throw new InvalidOperationException(
                    "Canonical lease registration is no longer pending.");
            }
        }

        internal void Deactivate()
        {
            Volatile.Write(ref _state, InactiveState);
            Previous?.UnregisterSuccessor(this);
            foreach (var successor in _successors.Keys)
            {
                if (successor.Inactive)
                    _successors.TryRemove(successor, out _);
                else
                    successor.PruneInactivePredecessors();
            }

            _successors.Clear();
        }

        internal void PruneInactivePredecessors()
        {
            while (true)
            {
                var previous = Previous;
                if (previous == null || !previous.Inactive)
                    return;

                var replacement = previous.Previous;
                if (!ReferenceEquals(
                        Interlocked.CompareExchange(
                            ref _previous,
                            replacement,
                            previous),
                        previous))
                {
                    continue;
                }

                previous.UnregisterSuccessor(this);
                if (replacement == null)
                    continue;
                if (replacement.RegisterSuccessor(this))
                    continue;
                if (Inactive)
                    return;
            }
        }

        private bool RegisterSuccessor(
            AmbientCanonicalLeaseRegistration successor)
        {
            if (successor.Inactive)
                return false;

            _successors.TryAdd(successor, 0);
            if (Inactive)
            {
                _successors.TryRemove(successor, out _);
                return false;
            }

            if (successor.Inactive)
            {
                _successors.TryRemove(successor, out _);
                return false;
            }

            return true;
        }

        private void UnregisterSuccessor(
            AmbientCanonicalLeaseRegistration successor) =>
            _successors.TryRemove(successor, out _);
    }

    internal sealed class SessionLifecycleLease : IAsyncDisposable
    {
        private FileStream? _stream;
        private PhysicalFileAuthority.StableDirectory? _parentAuthority;

        internal SessionLifecycleLease(
            FileSystemManager owner,
            FileStream stream,
            PhysicalFileAuthority.StableDirectory parentAuthority)
        {
            Owner = owner;
            _stream = stream;
            _parentAuthority = parentAuthority;
        }

        internal MainAdmission? MainAdmission {get;set;}
        internal FileSystemManager Owner { get; }
        internal bool IsActive =>
            _stream != null &&
            _parentAuthority != null;

        public async ValueTask DisposeAsync()
        {
            var stream = _stream;
            var parentAuthority = _parentAuthority;
            _stream = null;
            _parentAuthority = null;
            try
            {
                if (stream != null)
                    await stream.DisposeAsync();
            }
            finally
            {
                try { parentAuthority?.Dispose(); } finally { MainAdmission?.Dispose(); MainAdmission=null; }
            }
        }
    }

    internal sealed class CanonicalWriteLease : IAsyncDisposable
    {
#if DEBUG
        internal long BrowserDiagnosticId { get; set; }
#endif
        private FileStream? _stream;
        private PhysicalFileAuthority.StableDirectory? _parentAuthority;
        private AmbientCanonicalLeaseRegistration? _ambientRegistration;
        private readonly object _sourceState = new();
        private SourceDataOperation? _sourceOwner;
        private bool _sourceClosing;

        /// <summary>
        /// Registers the exact DATA owner without replacing another owner or crossing physical close intent.
        /// </summary>
        /// <param name="owner">
        /// Operation responsible for settling its actual source work.
        /// </param>
        /// <returns>
        /// Whether this live lease retains that exact owner while physical close has not begun.
        /// </returns>
        internal bool TryAttachSourceOwner(SourceDataOperation owner)
        {
            lock (_sourceState)
            {
                if (_sourceClosing || !IsActive || _sourceOwner != null && !ReferenceEquals(_sourceOwner, owner)) return false;
                _sourceOwner = owner;
                return true;
            }
        }

        /// <summary>
        /// Releases only the matching DATA guard after its source work has drained.
        /// </summary>
        /// <param name="owner">
        /// Exact registered owner; a foreign or already removed owner has no effect.
        /// </param>
        internal void DetachSourceOwner(SourceDataOperation owner)
        {
            lock (_sourceState) if (ReferenceEquals(_sourceOwner, owner)) _sourceOwner = null;
        }

        /// <summary>
        /// Excludes new DATA owners before capturing the guard that must settle prior to physical teardown.
        /// </summary>
        /// <returns>
        /// The exact retained DATA owner, or no guard when this lease has no source operation.
        /// </returns>
        private SourceDataOperation? BeginSourceClose()
        {
            lock (_sourceState)
            {
                if (_sourceClosing) throw new InvalidOperationException("The canonical lease is already closing.");
                _sourceClosing = true;
                return _sourceOwner;
            }
        }

        /// <summary>
        /// Ends close intent after refusal or completed teardown; it grants no authority to a closed lease.
        /// </summary>
        private void EndSourceClose() { lock (_sourceState) _sourceClosing = false; }

        internal CanonicalWriteLease(
            FileSystemManager owner,
            FileStream stream,
            PhysicalFileAuthority.StableDirectory parentAuthority,
            CanonicalWritePurpose purpose)
        {
            Owner = owner;
            Purpose = purpose;
            _stream = stream;
            _parentAuthority = parentAuthority;
        }

        internal MainAdmission? MainAdmission {get;set;}
        internal FileSystemManager Owner { get; }
        internal CanonicalWritePurpose Purpose { get; }
        internal GmWorkerCanonicalPurpose? WorkerPurpose { get; set; }
        internal IDisposable? WorkerRootPin { get; set; }
        internal object? ExternalPublicationContext { get; set; }
        internal object? PendingLocalDecision { get; set; }
        internal void EnsureNoPendingLocalDecision()
        {
            if (PendingLocalDecision != null)
                throw new InvalidOperationException("A worker decision is pending on this canonical lease; nested mutation or recovery is not permitted.");
        }
        internal ICanonicalMutationIntentRecorder? MutationIntentRecorder { get; set; }
        internal bool IsLegacyStorageRecovery { get; set; }
        internal BrowserLocalStorageAccess? BrowserLocalAccess { get; set; }
        internal AmbientCanonicalLeaseRegistration? AmbientRegistration
        {
            get => _ambientRegistration;
            set => _ambientRegistration = value;
        }
        internal bool IsActive =>
            _stream != null &&
            _parentAuthority != null;

        /// <summary>
        /// Closes every owned resource after reserving physical teardown against pending-snapshot readers.
        /// Repeated disposal retains the existing idempotent resource-release behavior.
        /// </summary>
        /// <returns>
        /// Completion after all resources have been attempted; disposal failures preserve unresolved ownership.
        /// </returns>
        public async ValueTask DisposeAsync()
        {
            Exception? sourceFailure = null;
            var sourceOperation = BeginSourceClose() ??
                (ValidationReuseSuppression.Value == 0 ? Owner._sourceDataOperation.Value : null);
            try
            {
                // Refuse outstanding work before detaching resources, even after ambient owner disposal.
                if (sourceOperation != null)
                {
                    MainAdmission?.RequireNoWorldReadConflict(MainAdmission.WorldRecoveryIssuance);
                    sourceOperation.BeginLeaseClose(this);
                    try { await sourceOperation.BeforeLeaseCloseAsync(this); }
                    catch (Exception error) { sourceFailure = error; }
                }
                MainAdmission?.BeginPhysicalLeaseClose();
            }
            catch
            {
                sourceOperation?.CancelLeaseClose(this);
                EndSourceClose();
                throw;
            }
            var stream = _stream;
            var parentAuthority = _parentAuthority;
            var externalPublicationContext =
                ExternalPublicationContext as IDisposable;
            _stream = null;
            _parentAuthority = null;
            ExternalPublicationContext = null;
            PendingLocalDecision = null;
            MutationIntentRecorder = null;
            BrowserLocalAccess?.Dispose();
            var mainForFailure = MainAdmission;
            Exception? failure = sourceFailure;
            void RetainFailure(Exception next) =>
                failure = failure == null ? next : new AggregateException(failure, next);

            try { externalPublicationContext?.Dispose(); }
            catch (Exception ex) { RetainFailure(ex); }
            // Every owned resource must be released even if the earlier
            // external context failed. Clearing _stream above makes retries
            // idempotent, so skipping this close would orphan the lock handle.
            try
            {
                if (stream != null)
                    await stream.DisposeAsync();
            }
            catch (Exception ex) { RetainFailure(ex); }
            try { parentAuthority?.Dispose(); }
            catch (Exception ex) { RetainFailure(ex); }
            try { MainAdmission?.Dispose(); MainAdmission=null; }
            catch (Exception ex) { RetainFailure(ex); }
            try { WorkerRootPin?.Dispose(); WorkerRootPin = null; }
            catch (Exception ex) { RetainFailure(ex); }
            try { Owner.ReleaseAmbientCanonicalLease(_ambientRegistration); }
            catch (Exception ex) { RetainFailure(ex); }
            finally
            {
                _ambientRegistration = null;
                try { sourceOperation?.LeaseClosed(this); }
                finally { EndSourceClose(); }
            }
            if (failure != null)
            {
                mainForFailure?.MarkUnresolved();
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }
    }

    private readonly record struct InProcessMutationSnapshot(
        long Version,
        bool MutationActive);

    private sealed class InProcessMutationRegistration : IDisposable
    {
        private string? _path;
        private InProcessMutationState? _state;

        internal InProcessMutationRegistration(string path)
        {
            _path = Path.GetFullPath(path);
            _state = AcquireInProcessMutationState(_path);
            _state.BeginMutation();
        }

        public void Dispose()
        {
            var path = Interlocked.Exchange(ref _path, null);
            var state = Interlocked.Exchange(ref _state, null);
            if (path == null || state == null)
                return;

            try
            {
                state.EndMutation();
            }
            finally
            {
                ReleaseInProcessMutationState(path, state);
            }
        }
    }

    private sealed class InProcessMutationObservation : IDisposable
    {
        private (string Path, InProcessMutationState State)[]? _states;

        internal InProcessMutationObservation(
            string path,
            string canonicalRoot)
        {
            var normalizedPath = Path.GetFullPath(path);
            var normalizedRoot = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(canonicalRoot));
            if (!IsSameOrDescendant(normalizedPath, normalizedRoot))
            {
                throw new InvalidDataException(
                    "Canonical mutation observation escaped the session root.");
            }

            var states =
                new List<(string Path, InProcessMutationState State)>();
            var current = normalizedPath;
            while (true)
            {
                states.Add((
                    current,
                    AcquireInProcessMutationState(current)));
                if (string.Equals(
                        Path.TrimEndingDirectorySeparator(current),
                        normalizedRoot,
                        StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                current = Path.GetDirectoryName(current) ??
                          throw new InvalidDataException(
                              "Canonical mutation observation has no session-root ancestor.");
            }

            _states = states.ToArray();
        }

        internal InProcessMutationSnapshot CaptureMutationState()
        {
            var states = _states ??
                         throw new ObjectDisposedException(
                             nameof(InProcessMutationObservation));
            long aggregateVersion = 0;
            var mutationActive = false;
            foreach (var (_, state) in states)
            {
                var snapshot = state.CaptureSnapshot();
                aggregateVersion = checked(
                    aggregateVersion + snapshot.Version);
                mutationActive |= snapshot.MutationActive;
            }

            return new InProcessMutationSnapshot(
                aggregateVersion,
                mutationActive);
        }

        public void Dispose()
        {
            var states = Interlocked.Exchange(ref _states, null);
            if (states == null)
                return;

            foreach (var (path, state) in states.Reverse())
                ReleaseInProcessMutationState(path, state);
        }
    }

    private sealed class InProcessMutationState
    {
        internal int ParticipantCount;
        internal int ActiveMutationCount;
        internal long Version;

        internal void BeginMutation()
        {
            lock (this)
            {
                ActiveMutationCount =
                    checked(ActiveMutationCount + 1);
                unchecked
                {
                    Version++;
                }
            }
        }

        internal void EndMutation()
        {
            lock (this)
            {
                if (ActiveMutationCount <= 0)
                {
                    throw new InvalidOperationException(
                        "Canonical mutation state has no active mutation to end.");
                }

                ActiveMutationCount--;
                unchecked
                {
                    Version++;
                }
            }
        }

        internal InProcessMutationSnapshot CaptureSnapshot()
        {
            lock (this)
            {
                return new InProcessMutationSnapshot(
                    Version,
                    ActiveMutationCount > 0);
            }
        }
    }

    private readonly string _basePath;
    private readonly ILogger<FileSystemManager> _logger;
    private readonly ILoadTransactionOperations _loadTransactionOperations;
    private readonly FileSystemManagerHooks? _hooks;
    private readonly AsyncLocal<AmbientCanonicalLeaseRegistration?>
        _ambientCanonicalLease = new();
    private static readonly ConcurrentDictionary<string, InProcessMutationState>
        CanonicalMutationStates = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions RecoveryJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private const int TransientFileAccessRetryCount = 20;
    private static readonly TimeSpan TransientFileAccessRetryDelay = TimeSpan.FromMilliseconds(50);
    private const int CanonicalWriteLockRetryCount = 200;
    private const int SessionLifecycleLockRetryCount = 200;

    private static readonly string[] RequiredDirectories =
    {
        "game_session/input",
        "game_session/game_state/core",
        "game_session/game_state/player",
        "game_session/game_state/inventory",
        "game_session/game_state/world",
        "game_session/game_state/quests",
        "game_session/game_state/npcs",
        "game_session/game_state/combat",
        "game_session/game_state/factions",
        "game_session/game_state/meta",
        "game_session/game_state/misc",
        "game_session/game_state/control",
        "game_session/game_state/history",
        "game_session/lore/chaos_sea",
        "game_session/lore/shining_abode",
        "game_session/lore/current_world",
        "game_session/mods",
        "game_session/world_profiles",
        "game_session/output",
        "game_session/ready",
        "game_session/saves/manual_saves",
        "game_session/saves/autosaves",
        "game_session/saves/checkpoint_saves",
        "game_session/stories",
        "game_session/images"
    };

    public string BasePath => _basePath;
    internal CanonicalRootIdentity CanonicalRootAuthorityIdentity { get; }
    public string GameSessionPath => Path.Combine(_basePath, "game_session");
    internal string RuntimeRootPath => Path.Combine(_basePath, ".boe_runtime");
    internal string CanonicalWriteLockPath =>
        Path.Combine(RuntimeRootPath, "locks", "canonical-write.lock");
    internal string SessionLifecycleLockPath =>
        Path.Combine(RuntimeRootPath, "locks", "session-lifecycle.lock");
    internal string ActiveLoadTransactionJournalPath =>
        Path.Combine(RuntimeRootPath, "load-transactions", "active.json");
    internal string SessionGenerationPath =>
        Path.Combine(RuntimeRootPath, "session-generation", "current.json");
    internal string ActiveWorkerApplyTransactionJournalPath =>
        Path.Combine(RuntimeRootPath, "worker-apply-transactions", "active.json");
    internal string PhysicalPublicationTransactionsRootPath =>
        Path.Combine(RuntimeRootPath, "file-publication-transactions");

    public FileSystemManager(string basePath, ILogger<FileSystemManager> logger)
        : this(basePath, logger, PhysicalLoadTransactionOperations.Instance, hooks: null)
    {
    }

    internal FileSystemManager(
        string basePath,
        ILogger<FileSystemManager> logger,
        ILoadTransactionOperations loadTransactionOperations)
        : this(basePath, logger, loadTransactionOperations, hooks: null)
    {
    }

    internal FileSystemManager(
        string basePath,
        ILogger<FileSystemManager> logger,
        ILoadTransactionOperations loadTransactionOperations,
        FileSystemManagerHooks? hooks)
    {
        _basePath = ResolvePhysicalBasePath(basePath);
        CanonicalRootAuthorityIdentity = CanonicalRootIdentityInterner.Get(_basePath);
        _logger = logger;
        _loadTransactionOperations = loadTransactionOperations ??
            throw new ArgumentNullException(nameof(loadTransactionOperations));
        _hooks = hooks;
#if DEBUG
        _browserAdmissionDiagnostic = BrowserAdmissionDiagnosticCollector.FromEnvironment(_basePath);
#endif
    }

    /// <summary>
    /// Creates required canonical directories through the original acquired lease and registered mutation route.
    /// </summary>
    public void EnsureDirectoryStructure()
    {
        var writeLease = AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
        try
        {
            RecoverInterruptedLoadTransaction(writeLease);
            EnsureDirectoryStructure(writeLease);
        }
        finally
        {
            writeLease.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Creates the required canonical directories while excluding a concurrent validation read.
    /// </summary>
    /// <param name="writeLease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    internal void EnsureDirectoryStructure(CanonicalWriteLease writeLease)
    {
        using var validationMutation = BeginValidationMutation(writeLease);
        EnsureWorkerGeneralMutationAllowed(writeLease);
        writeLease.EnsureNoPendingLocalDecision();
        EnsureDirectoryStructureCore();
    }

    private void EnsureDirectoryStructureCore()
    {
        foreach (var dir in RequiredDirectories)
        {
            var relativePath = dir.Equals("game_session", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : dir["game_session/".Length..];
            var fullPath = ResolvePath(relativePath);
            var existed = Directory.Exists(fullPath);
            using (PhysicalFileAuthority.EnsureStableDirectory(
                       _basePath,
                       fullPath,
                       "Canonical directory structure"))
            {
                if (!existed)
                {
                    _logger.LogDebug("Создана директория: {Path}", dir);
                }
            }
        }
    }

    /// <summary>
    /// Resolves a canonical session-relative path and performs the existing fresh namespace and reparse-point checks.
    /// </summary>
    /// <param name="relativePath">
    /// The session-relative name; <see langword="null"/> is refused and an empty value retains the session-root route.
    /// </param>
    /// <returns>
    /// The validated absolute path; malformed, escaping or unsafe names retain their original refusals.
    /// </returns>
    public string ResolvePath(string relativePath)
    {
        using var operationContext = EnterOperationTimingContext();
        using var operationTiming = MeasureOperation("CanonicalPath.Resolve");
        ArgumentNullException.ThrowIfNull(relativePath);
        var sessionRoot = Path.GetFullPath(GameSessionPath).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var normalized = relativePath.Trim().Replace(
            Path.AltDirectorySeparatorChar,
            Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalized) || normalized.Contains(':', StringComparison.Ordinal))
            throw new InvalidDataException("Canonical game-session path must be relative.");

        if (normalized.Length > 0)
        {
            var segments = normalized.Split(Path.DirectorySeparatorChar, StringSplitOptions.None);
            if (segments.Any(segment =>
                    string.IsNullOrWhiteSpace(segment) ||
                    segment is "." or ".."))
            {
                throw new InvalidDataException("Canonical game-session path contains an invalid segment.");
            }
        }

        var fullPath = Path.GetFullPath(Path.Combine(sessionRoot, normalized));
        var sessionPrefix = sessionRoot + Path.DirectorySeparatorChar;
        if (!string.Equals(fullPath, sessionRoot, StringComparison.OrdinalIgnoreCase) &&
            !fullPath.StartsWith(sessionPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Canonical game-session path escapes game_session.");
        }

        using (MeasureOperation("CanonicalPath.WindowsMetadata"))
            EnsureNoExistingReparsePoint(
                sessionRoot,
                fullPath,
                "Canonical game-session");
        return fullPath;
    }

    private static string ResolvePhysicalBasePath(string basePath)
    {
        if (string.IsNullOrWhiteSpace(basePath))
            throw new ArgumentException("Base path is required.", nameof(basePath));

        var requestedPath = Path.GetFullPath(basePath);
        Directory.CreateDirectory(requestedPath);
        var root = Path.GetPathRoot(requestedPath)
            ?? throw new InvalidDataException("Base path has no filesystem root.");
        var current = root;
        foreach (var segment in Path.GetRelativePath(root, requestedPath)
                     .Split(
                         [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                         StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!Directory.Exists(current))
                continue;

            var info = new DirectoryInfo(current);
            if ((info.Attributes & FileAttributes.ReparsePoint) == 0)
                continue;

            var target = info.ResolveLinkTarget(returnFinalTarget: true)
                ?? throw new InvalidDataException($"Cannot resolve physical base-path alias '{current}'.");
            current = Path.GetFullPath(target.FullName);
        }

        return Path.GetFullPath(current).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
    }

    /// <summary>
    /// Refuses observed root or descendant reparse traversal at the existing path-validation boundary.
    /// </summary>
    /// <param name="authorityRoot">
    /// The normalized authority root, queried as a directory only.
    /// </param>
    /// <param name="fullPath">
    /// The normalized and contained root or descendant candidate.
    /// </param>
    /// <param name="authorityName">
    /// The authority label retained in existing root and descendant refusal messages.
    /// </param>
    private void EnsureNoExistingReparsePoint(
        string authorityRoot,
        string fullPath,
        string authorityName)
    {
        if (OperatingSystem.IsWindows())
        {
            WindowsPathMetadataGuard.Ensure(authorityRoot, fullPath, authorityName,
                _hooks?.PathMetadataQueryObserver is null ? null : ObservePassivePathMetadata);
            return;
        }

        var current = authorityRoot;
        if (Directory.Exists(current) && IsReparsePoint(current))
            throw new InvalidDataException($"{authorityName} root cannot be a reparse point.");

        var relative = Path.GetRelativePath(authorityRoot, fullPath);
        if (relative == ".")
            return;

        foreach (var segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!Directory.Exists(current) && !File.Exists(current))
                break;
            if (IsReparsePoint(current))
            {
                throw new InvalidDataException(
                    $"{authorityName} path traverses reparse point '{current}'.");
            }
        }
    }

    /// <summary>
    /// Atomic write: write to temp file then rename to prevent partial writes.
    /// </summary>
    public async Task WriteFileAtomicAsync(string relativePath, string content)
    {
        await WriteFileAtomicBytesAsync(relativePath, EncodeUtf8WithPreamble(content));
    }

    internal async Task WriteFileAtomicAsync(
        CanonicalWriteLease writeLease,
        string relativePath,
        string content)
    {
        await WriteFileAtomicBytesAsync(writeLease, relativePath, EncodeUtf8WithPreamble(content));
    }

    /// <summary>
    /// Writes encoded text and records its original publication evidence under the supplied lease.
    /// </summary>
    /// <param name="writeLease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <param name="relativePath">
    /// Canonical session-relative member or subtree to operate on.
    /// </param>
    /// <param name="content">
    /// Content to publish or append.
    /// </param>
    /// <returns>
    /// The original physical publication evidence after the operation and its callbacks complete.
    /// </returns>
    internal async Task<CanonicalMutationPublication>
        WriteFileAtomicWithPublicationAsync(
            CanonicalWriteLease writeLease,
            string relativePath,
            string content)
    {
        using var validationMutation = BeginValidationMutation(writeLease);
        EnsureWorkerGeneralMutationAllowed(writeLease);
        writeLease.EnsureNoPendingLocalDecision();
        var bytes = EncodeUtf8WithPreamble(content);
        await RecordCanonicalMutationIntentAsync(
            writeLease,
            relativePath,
            bytes);
        var publication =
            await WriteFileAtomicBytesCoreAsync(
                relativePath,
                bytes,
                afterPublicationNotCommitted: () =>
                    RecordCanonicalMutationNonPublicationAsync(
                        writeLease,
                        relativePath));
        await RecordCanonicalMutationPublicationAsync(
            writeLease,
            relativePath,
            publication);
        return publication;
    }

    /// <summary>
    /// Publishes text only against the supplied physical identity and digest, retaining mutation admission through callbacks.
    /// </summary>
    /// <param name="writeLease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <param name="relativePath">
    /// Canonical session-relative member or subtree to operate on.
    /// </param>
    /// <param name="content">
    /// Content to publish or append.
    /// </param>
    /// <param name="expectedCurrentIdentity">
    /// Physical identity that must still belong to the current destination.
    /// </param>
    /// <param name="expectedCurrentSha256">
    /// SHA-256 digest that must still match the current destination.
    /// </param>
    /// <returns>
    /// The original physical publication evidence after the operation and its callbacks complete.
    /// </returns>
    internal async Task<CanonicalMutationPublication>
        WriteFileAtomicWithPublicationIfCurrentAuthorityAsync(
            CanonicalWriteLease writeLease,
            string relativePath,
            string content,
            PhysicalFileAuthority.FileIdentity expectedCurrentIdentity,
            string expectedCurrentSha256)
    {
        using var validationMutation = BeginValidationMutation(writeLease);
        EnsureWorkerGeneralMutationAllowed(writeLease);
        ArgumentNullException.ThrowIfNull(expectedCurrentIdentity);
        var expectedHash = NormalizeOwnedMutationHashes(
                [expectedCurrentSha256])
            .Single();
        var bytes = EncodeUtf8WithPreamble(content);
        await RecordCanonicalMutationIntentAsync(
            writeLease,
            relativePath,
            bytes);
        var publication = await WriteFileAtomicBytesCoreAsync(
            relativePath,
            bytes,
            CancellationToken.None,
            expectedDestinationIdentity: expectedCurrentIdentity,
            expectedDestinationSha256: expectedHash,
            afterPublicationNotCommitted: () =>
                RecordCanonicalMutationNonPublicationAsync(
                    writeLease,
                    relativePath));
        await RecordCanonicalMutationPublicationAsync(
            writeLease,
            relativePath,
            publication);
        return publication;
    }

    public async Task WriteFileAtomicBytesAsync(string relativePath, byte[] content)
    {
        var writeLock = await AcquireCanonicalWriteLeaseAsync();
        Exception? publicationUncertainty = null;
        try { await WriteFileAtomicBytesAsync(writeLock, relativePath, content); }
        catch (CoordinatedStatePublicationUncertainException failure)
        {
            publicationUncertainty = failure;
            throw;
        }
        finally
        {
            await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(
                this, writeLock, completed: false, operationFailure: publicationUncertainty);
        }
    }

    /// <summary>
    /// Publishes canonical bytes through the existing ordinary or legacy writer under the supplied lease.
    /// </summary>
    /// <param name="writeLease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <param name="relativePath">
    /// Canonical session-relative member or subtree to operate on.
    /// </param>
    /// <param name="content">
    /// The exact byte content to publish through the existing selected writer.
    /// </param>
    /// <returns>
    /// Completion after the operation and its required cleanup finish.
    /// </returns>
    internal async Task WriteFileAtomicBytesAsync(
        CanonicalWriteLease writeLease,
        string relativePath,
        byte[] content)
    {
        using var totalTiming = MeasureOperation("OrdinaryWrite.Total");
        using var admissionTiming = MeasureOperation("OrdinaryWrite.Admission");
        using var validationMutation = BeginValidationMutation(writeLease);
        EnsureWorkerGeneralMutationAllowed(writeLease);
        writeLease.EnsureNoPendingLocalDecision();
        admissionTiming?.Dispose();
        using var selectionTiming = MeasureOperation("OrdinaryWrite.SelectWriter");
        if (UsesTrustedLocalWriter(writeLease, relativePath))
        {
            selectionTiming?.Dispose();
            using var trustedTiming = MeasureOperation("OrdinaryWrite.TrustedWriter");
            await WriteTrustedLocalFileAsync(writeLease, relativePath, content);
            return;
        }
        selectionTiming?.Dispose();
        using var legacyIntentTiming = MeasureOperation("OrdinaryWrite.LegacyIntent");
        await RecordCanonicalMutationIntentAsync(
            writeLease,
            relativePath,
            content);
        legacyIntentTiming?.Dispose();
        using var legacyWriteTiming = MeasureOperation("OrdinaryWrite.LegacyWrite");
        var publication =
            await WriteFileAtomicBytesCoreAsync(
                relativePath,
                content,
                afterPublicationNotCommitted: () =>
                    RecordCanonicalMutationNonPublicationAsync(
                        writeLease,
                        relativePath));
        legacyWriteTiming?.Dispose();
        using var legacyPublicationTiming = MeasureOperation("OrdinaryWrite.LegacyPublication");
        await RecordCanonicalMutationPublicationAsync(
            writeLease,
            relativePath,
            publication);
    }

    public async Task AppendFileAtomicAsync(string relativePath, string content)
    {
        var writeLock = await AcquireCanonicalWriteLeaseAsync();
        Exception? publicationUncertainty = null;
        try { await AppendFileAtomicAsync(writeLock, relativePath, content); }
        catch (CoordinatedStatePublicationUncertainException failure)
        {
            publicationUncertainty = failure;
            throw;
        }
        finally
        {
            await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(
                this, writeLock, completed: false, operationFailure: publicationUncertainty);
        }
    }

    internal async Task AppendFileAtomicAsync(
        CanonicalWriteLease writeLease,
        string relativePath,
        string content)
    {
        await AppendFileAtomicAsync(
            writeLease,
            relativePath,
            content,
            CancellationToken.None);
    }

    /// <summary>
    /// Appends text through the existing authorized writer while retaining mutation admission through completion.
    /// </summary>
    /// <param name="writeLease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <param name="relativePath">
    /// Canonical session-relative member or subtree to operate on.
    /// </param>
    /// <param name="content">
    /// Content to publish or append.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation observed by the existing operation; the default token does not request cancellation.
    /// </param>
    /// <returns>
    /// Completion after the operation and its required cleanup finish.
    /// </returns>
    internal async Task AppendFileAtomicAsync(
        CanonicalWriteLease writeLease,
        string relativePath,
        string content,
        CancellationToken cancellationToken)
    {
        using var validationMutation = BeginValidationMutation(writeLease);
        EnsureValidCanonicalWriteLease(writeLease);
        writeLease.EnsureNoPendingLocalDecision();
        EnsureWorkerAuditAppend(writeLease, relativePath, Encoding.UTF8.GetBytes(content));
        cancellationToken.ThrowIfCancellationRequested();
        if (UsesTrustedLocalWriter(writeLease, relativePath))
        {
            RequireCommittedLocalPublication(await AppendOrdinaryFileWithOutcomeAsync(
                writeLease, relativePath, content, cancellationToken));
            return;
        }
        var beforeContent = await ReadFileBytesCoreAsync(relativePath, cancellationToken);
        var currentContent = beforeContent ?? Encoding.UTF8.GetPreamble();

        var appendedContent = Encoding.UTF8.GetBytes(content);
        var nextContent = new byte[currentContent.Length + appendedContent.Length];
        Buffer.BlockCopy(currentContent, 0, nextContent, 0, currentContent.Length);
        Buffer.BlockCopy(appendedContent, 0, nextContent, currentContent.Length, appendedContent.Length);
        EnsureWorkerAuditAppend(writeLease, relativePath, appendedContent);
        await RecordCanonicalMutationIntentAsync(
            writeLease,
            relativePath,
            nextContent);
        var publication = await WriteFileAtomicBytesCoreAsync(
            relativePath,
            nextContent,
            cancellationToken,
            afterPublicationNotCommitted: () =>
                RecordCanonicalMutationNonPublicationAsync(
                    writeLease,
                    relativePath));
        await RecordCanonicalMutationPublicationAsync(
            writeLease,
            relativePath,
            publication);
    }

    internal async Task<bool> AppendFileAtomicIfCurrentSessionAsync(
        string relativePath,
        string content,
        string expectedSessionGeneration)
    {
        return await AppendFileAtomicIfCurrentSessionAsync(
            relativePath,
            content,
            expectedSessionGeneration,
            CancellationToken.None);
    }

    internal async Task<bool> AppendFileAtomicIfCurrentSessionAsync(
        string relativePath,
        string content,
        string expectedSessionGeneration,
        CancellationToken cancellationToken)
    {
        var writeLease = await AcquireCanonicalWriteLeaseAsync(
            cancellationToken: cancellationToken);
        Exception? publicationUncertainty = null;
        try
        {
            if (!IsCurrentSessionGeneration(writeLease, expectedSessionGeneration))
                return false;

            await AppendFileAtomicAsync(
                writeLease,
                relativePath,
                content,
                cancellationToken);
            return true;
        }
        catch (CoordinatedStatePublicationUncertainException failure)
        {
            publicationUncertainty = failure;
            throw;
        }
        finally
        {
            await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(
                this, writeLease, completed: false, operationFailure: publicationUncertainty);
        }
    }

    public async Task<CanonicalFileMutationResult> CompareExchangeFileBytesAsync(
        string relativePath,
        byte[]? expectedContent,
        byte[]? desiredContent)
    {
        var writeLock = await AcquireCanonicalWriteLeaseAsync();
        Exception? publicationUncertainty = null;
        try { return await CompareExchangeFileBytesAsync(writeLock, relativePath, expectedContent, desiredContent); }
        catch (CoordinatedStatePublicationUncertainException failure)
        {
            publicationUncertainty = failure;
            throw;
        }
        finally
        {
            await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(
                this, writeLock, completed: false, operationFailure: publicationUncertainty);
        }
    }

    /// <summary>
    /// Compares current bytes and conditionally publishes the requested replacement or deletion.
    /// </summary>
    /// <param name="writeLease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <param name="relativePath">
    /// Canonical session-relative member or subtree to operate on.
    /// </param>
    /// <param name="expectedContent">
    /// Expected current bytes; <see langword="null"/> requires the member to be absent.
    /// </param>
    /// <param name="desiredContent">
    /// Desired bytes; <see langword="null"/> requests deletion.
    /// </param>
    /// <param name="validatePreparedNamespace">
    /// Optional namespace validation callback before publication; <see langword="null"/> adds no callback.
    /// </param>
    /// <returns>
    /// The existing mutation result describing whether the expected state matched and the requested change was applied.
    /// </returns>
    internal async Task<CanonicalFileMutationResult> CompareExchangeFileBytesAsync(
        CanonicalWriteLease writeLease,
        string relativePath,
        byte[]? expectedContent,
        byte[]? desiredContent,
        Func<Task>? validatePreparedNamespace = null)
    {
        using var validationMutation = BeginValidationMutation(writeLease);
        if (writeLease.WorkerPurpose?.Dispatch is { } dispatch)
            dispatch.ValidateReservation(this, writeLease, relativePath, expectedContent, desiredContent);
        else EnsureWorkerGeneralMutationAllowed(writeLease);
        writeLease.EnsureNoPendingLocalDecision();
        var useLocalWriter = UsesTrustedLocalWriter(writeLease, relativePath);
        if (!useLocalWriter && validatePreparedNamespace != null)
            throw new InvalidOperationException("A prepared ordinary guard cannot replace legacy publication authority.");
        var currentContent = useLocalWriter
            ? await ReadLocalFileBytesAsync(writeLease, relativePath)
            : await ReadFileBytesCoreAsync(relativePath);

        if (!ExactBytesEqual(currentContent, expectedContent))
            return CanonicalFileMutationResult.Conflict;
        if (useLocalWriter)
        {
            RequireCommittedLocalPublication(await PublishLocalFilesAsync(writeLease,
                [new(relativePath, currentContent, desiredContent)],
                validatePreparedNamespace: validatePreparedNamespace));
            return CanonicalFileMutationResult.Applied;
        }

        await RecordCanonicalMutationIntentAsync(
            writeLease,
            relativePath,
            desiredContent);
        CanonicalMutationPublication? publication;
        if (desiredContent == null)
        {
            publication = DeleteFileCore(
                relativePath,
                capturePublication: writeLease.MutationIntentRecorder != null);
            if (publication == null)
            {
                await RecordCanonicalMutationNonPublicationAsync(
                    writeLease,
                    relativePath);
            }
        }
        else
        {
            publication =
                await WriteFileAtomicBytesCoreAsync(
                    relativePath,
                    desiredContent,
                    afterPublicationNotCommitted: () =>
                        RecordCanonicalMutationNonPublicationAsync(
                            writeLease,
                            relativePath));
        }
        if (publication != null)
        {
            await RecordCanonicalMutationPublicationAsync(
                writeLease,
                relativePath,
                publication);
        }

        return CanonicalFileMutationResult.Applied;
    }

    /// <summary>
    /// Writes bytes only when the current digest or allowed absence matches the supplied authority.
    /// </summary>
    /// <param name="writeLease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <param name="relativePath">
    /// Canonical session-relative member or subtree to operate on.
    /// </param>
    /// <param name="content">
    /// Content to publish or append.
    /// </param>
    /// <param name="allowedCurrentSha256s">
    /// Accepted current digests; an empty collection is valid only when missing current content is allowed.
    /// </param>
    /// <param name="allowMissingCurrent">
    /// Whether an absent current destination is an accepted precondition.
    /// </param>
    /// <returns>
    /// Completion after the operation and its required cleanup finish.
    /// </returns>
    internal async Task WriteFileAtomicBytesIfCurrentOwnedAsync(
        CanonicalWriteLease writeLease,
        string relativePath,
        byte[] content,
        IReadOnlyCollection<string> allowedCurrentSha256s,
        bool allowMissingCurrent)
    {
        using var validationMutation = BeginValidationMutation(writeLease);
        EnsureWorkerGeneralMutationAllowed(writeLease);
        writeLease.EnsureNoPendingLocalDecision();
        ArgumentNullException.ThrowIfNull(allowedCurrentSha256s);
        var allowedHashes =
            allowMissingCurrent && allowedCurrentSha256s.Count == 0
                ? new HashSet<string>(StringComparer.Ordinal)
                : NormalizeOwnedMutationHashes(allowedCurrentSha256s);
        await WriteFileAtomicBytesCoreAsync(
            relativePath,
            content,
            CancellationToken.None,
            allowedHashes,
            allowMissingCurrent);
    }

    /// <summary>
    /// Replaces bytes only when both the physical identity and digest still match.
    /// </summary>
    /// <param name="writeLease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <param name="relativePath">
    /// Canonical session-relative member or subtree to operate on.
    /// </param>
    /// <param name="content">
    /// Content to publish or append.
    /// </param>
    /// <param name="expectedCurrentIdentity">
    /// Physical identity that must still belong to the current destination.
    /// </param>
    /// <param name="expectedCurrentSha256">
    /// SHA-256 digest that must still match the current destination.
    /// </param>
    /// <returns>
    /// Completion after the operation and its required cleanup finish.
    /// </returns>
    internal async Task WriteFileAtomicBytesIfCurrentAuthorityAsync(
        CanonicalWriteLease writeLease,
        string relativePath,
        byte[] content,
        PhysicalFileAuthority.FileIdentity expectedCurrentIdentity,
        string expectedCurrentSha256)
    {
        using var validationMutation = BeginValidationMutation(writeLease);
        EnsureWorkerGeneralMutationAllowed(writeLease);
        writeLease.EnsureNoPendingLocalDecision();
        ArgumentNullException.ThrowIfNull(expectedCurrentIdentity);
        var expectedHash = NormalizeOwnedMutationHashes(
                [expectedCurrentSha256])
            .Single();

        await WriteFileAtomicBytesCoreAsync(
            relativePath,
            content,
            CancellationToken.None,
            expectedDestinationIdentity: expectedCurrentIdentity,
            expectedDestinationSha256: expectedHash);
    }

    private Task<CanonicalMutationPublication> WriteFileAtomicBytesCoreAsync(
        string relativePath,
        byte[] content,
        Func<Task>? afterPublicationNotCommitted = null) =>
        WriteFileAtomicBytesCoreAsync(
            relativePath,
            content,
            CancellationToken.None,
            afterPublicationNotCommitted:
                afterPublicationNotCommitted);

    /// <summary>
    /// Performs legacy atomic publication under the current physical admission, including retries and cleanup.
    /// </summary>
    /// <param name="relativePath">
    /// Canonical session-relative member or subtree to operate on.
    /// </param>
    /// <param name="content">
    /// Content to publish or append.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation observed by the existing operation; the default token does not request cancellation.
    /// </param>
    /// <param name="allowedCurrentSha256s">
    /// Accepted current digests, or <see langword="null"/> when no digest-set condition is imposed.
    /// </param>
    /// <param name="allowMissingCurrent">
    /// Whether an absent current destination is an accepted precondition.
    /// </param>
    /// <param name="expectedDestinationIdentity">
    /// Expected physical destination identity, or <see langword="null"/> for no identity condition.
    /// </param>
    /// <param name="expectedDestinationSha256">
    /// Expected destination digest, paired with the identity, or <see langword="null"/> when unconditioned.
    /// </param>
    /// <param name="afterPublicationNotCommitted">
    /// Optional callback after a known nonpublication; <see langword="null"/> adds no callback.
    /// </param>
    /// <returns>
    /// The original physical publication evidence after the operation and its callbacks complete.
    /// </returns>
    private async Task<CanonicalMutationPublication> WriteFileAtomicBytesCoreAsync(
        string relativePath,
        byte[] content,
        CancellationToken cancellationToken,
        IReadOnlySet<string>? allowedCurrentSha256s = null,
        bool allowMissingCurrent = false,
        PhysicalFileAuthority.FileIdentity? expectedDestinationIdentity = null,
        string? expectedDestinationSha256 = null,
        Func<Task>? afterPublicationNotCommitted = null)
    {
        using var validationMutation = BeginValidationMutation();
        cancellationToken.ThrowIfCancellationRequested();
        if ((expectedDestinationIdentity == null) !=
            string.IsNullOrWhiteSpace(expectedDestinationSha256))
        {
            throw new InvalidDataException(
                "Conditional canonical replacement requires complete physical identity and digest authority.");
        }

        var fullPath = ResolvePath(relativePath);
        using var mutationRegistration =
            new InProcessMutationRegistration(fullPath);
        var destinationEntry =
            PhysicalFileAuthority.ProbeNamespaceEntryFromRoot(
                _basePath,
                fullPath,
                "Canonical atomic destination");
        EnsureAuthorityFilePublicationSupported(
            destinationEntry,
            "Canonical atomic write");

        await InvokeBeforeCanonicalMutationBoundaryAsync(relativePath);
        cancellationToken.ThrowIfCancellationRequested();
        using var parentAuthority = EnsureStableCanonicalParent(
            relativePath,
            fullPath);
        var tempRelativePath = relativePath + ".tmp." + Guid.NewGuid().ToString("N")[..8];
        var tempPath = ResolvePath(tempRelativePath);
        FileStream? stream = null;
        try
        {
            stream = PhysicalFileAuthority.CreateNewWritableFile(
                parentAuthority,
                tempPath,
                "Canonical atomic temporary",
                asynchronous: true);
            await stream.WriteAsync(content, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            stream.Flush(flushToDisk: true);

            cancellationToken.ThrowIfCancellationRequested();
            EnsureCanonicalMutationBoundary(relativePath, fullPath);
            await InvokeAfterCanonicalMutationBoundaryValidatedAsync(
                relativePath);
            ReversibleFilePublication.PublicationResult publication;
            if (SupportsReversibleFileReplacement)
            {
                EnsureRuntimeDirectoryExistsAndIsSafe(
                    PhysicalPublicationTransactionsRootPath);
                publication = await ReversibleFilePublication.PublishAsync(
                    _basePath,
                    PhysicalPublicationTransactionsRootPath,
                    parentAuthority,
                    tempPath,
                    stream,
                    parentAuthority,
                    fullPath,
                    "Canonical atomic write",
                    _hooks?.AfterPhysicalFileAuthorityValidatedAsync,
                    _hooks?.BeforePhysicalSourcePublishedAsync,
                    _hooks?.AfterPhysicalFilePublishedAsync,
                    cancellationToken,
                    _hooks
                        ?.BeforePhysicalRollbackAbsenceFinalValidationAsync,
                    allowedDestinationSha256s:
                        allowedCurrentSha256s,
                    allowMissingDestination:
                        allowMissingCurrent,
                    expectedDestinationIdentity:
                        expectedDestinationIdentity,
                    expectedDestinationSha256:
                        expectedDestinationSha256,
                    afterPublicationNotCommitted:
                        afterPublicationNotCommitted);
            }
            else
            {
                if (allowedCurrentSha256s != null ||
                    expectedDestinationIdentity != null)
                {
                    throw new PlatformNotSupportedException(
                        "Identity-bound conditional canonical replacement requires reversible file publication.");
                }

                PhysicalFileAuthority.RenameOpenedObjectRelative(
                    stream.SafeFileHandle,
                    parentAuthority,
                    fullPath,
                    replaceExisting: false,
                    "Canonical create-only publication");
                var openedAuthority =
                    PhysicalFileAuthority.CaptureOpenedFileAuthority(
                        stream.SafeFileHandle,
                        fullPath,
                        "Canonical create-only publication");
                publication = new ReversibleFilePublication.PublicationResult(
                    openedAuthority.Identity,
                    openedAuthority.Sha256);
            }

            return new CanonicalMutationPublication(
                CanonicalMutationOperation.Write,
                publication.PublishedIdentity,
                publication.PublishedSha256);
        }
        catch
        {
            if (stream != null)
            {
                await stream.DisposeAsync();
                stream = null;
            }

            TryDeleteAtomicTempFileWithoutFollowingReparsePoints(
                parentAuthority,
                tempRelativePath,
                tempPath);
            throw;
        }
        finally
        {
            if (stream != null)
                await stream.DisposeAsync();
        }
    }

    private void EnsureCanonicalMutationBoundary(string relativePath, string expectedFullPath)
    {
        RequireNoWorldValidationMutation();
        var revalidatedFullPath = ResolvePath(relativePath);
        if (!string.Equals(
                revalidatedFullPath,
                expectedFullPath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Canonical game-session path identity changed before mutation.");
        }
    }

    private void TryDeleteAtomicTempFileWithoutFollowingReparsePoints(
        PhysicalFileAuthority.StableDirectory parentAuthority,
        string tempRelativePath,
        string expectedTempPath)
    {
        try
        {
            var revalidatedTempPath = ResolvePath(tempRelativePath);
            if (string.Equals(
                    revalidatedTempPath,
                    expectedTempPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                PhysicalFileAuthority.TryDeleteFile(
                    parentAuthority,
                    revalidatedTempPath,
                    "Canonical atomic temporary cleanup");
            }
        }
        catch
        {
            // Retaining an unreachable temp file is safer than following a replaced path.
        }
    }

    public async Task<string?> ReadFileAsync(string relativePath)
    {
#if DEBUG
        BrowserDiagnosticContent(relativePath, "public-text-async", "semantic", "started");
#endif
#if DEBUG
        try
        {
#endif
        await RecoverPendingFilePublicationsBeforeCanonicalReadAsync(
            CancellationToken.None);
        var result = await ReadOrdinaryFileCoreAsync(relativePath);
#if DEBUG
        BrowserDiagnosticContent(relativePath, "public-text-async", "initial", result == null ? "missing" : "present");
#endif
        await InvokeAfterCanonicalReadAttemptAsync(relativePath);
        if (!HasAmbientCanonicalLease() &&
            (result == null || HasPendingFilePublications()))
        {
#if DEBUG
            BrowserDiagnosticContent(relativePath, "public-text-async", "followup-reason", result == null ? "missing" : "pending");
#endif
            await using var writeLease =
                await AcquirePublicationReadQuiescenceLeaseAsync();
            result = await ReadOrdinaryFileCoreAsync(relativePath);
#if DEBUG
                BrowserDiagnosticContent(relativePath, "public-text-async", "followup", result == null ? "missing" : "present");
#endif
        }

#if DEBUG
        BrowserDiagnosticContent(relativePath, "public-text-async", "semantic", result == null ? "returned-missing" : "returned-present");
#endif
        return result;

#if DEBUG
        }
        catch { BrowserDiagnosticContent(relativePath, "public-text-async", "semantic", "fault"); throw; }
#endif
    }

    /// <summary>
    /// Reads text using the declared lease and original ordinary or protected reader.
    /// </summary>
    /// <param name="writeLease">
    /// Original manager-owned lease. Fresh source reads retain full main and worker admission;
    /// cached provisional DATA requires its exact live bound adapter and the owning fresh effect or success fence.
    /// </param>
    /// <param name="relativePath">
    /// Original session-relative spelling; invalid or null paths retain their original refusal.
    /// </param>
    /// <returns>
    /// The observed content, or <see langword="null"/> for an absent source.
    /// </returns>
    internal async Task<string?> ReadFileAsync(
        CanonicalWriteLease writeLease,
        string relativePath)
    {
#if DEBUG
        BrowserDiagnosticContent(relativePath, "explicit-text-async", "semantic", "started");
        try
        {
#endif
        if (TryReadDeclaredSourceData(writeLease, relativePath, true, out var cached)) return DecodeFileSnapshot(cached);
        EnsureValidCanonicalWriteLease(writeLease);
        var ordinary = UsesTrustedLocalWriter(writeLease, relativePath);
        var result = ordinary
            ? await ReadOrdinaryFileCoreAsync(relativePath, writeLease)
            : await ReadFileCoreAsync(relativePath, writeLease);

        RememberDeclaredSourceRoute(writeLease, relativePath, true, ordinary);

#if DEBUG
        BrowserDiagnosticContent(relativePath, "explicit-text-async", "semantic", result == null ? "returned-missing" : "returned-present");
#endif
        return result;

#if DEBUG
        }
        catch { BrowserDiagnosticContent(relativePath, "explicit-text-async", "semantic", "fault"); throw; }
#endif
    }

    /// <summary>
    /// Decodes the original protected snapshot using BOM-aware text semantics.
    /// </summary>
    /// <param name="relativePath">
    /// Original session-relative file spelling.
    /// </param>
    /// <param name="lease">
    /// Declared lease for read-set identity, or <see langword="null"/> for the original lease-free reader.
    /// </param>
    /// <returns>
    /// Decoded text or <see langword="null"/> for observed absence.
    /// </returns>
    private async Task<string?> ReadFileCoreAsync(string relativePath, CanonicalWriteLease? lease = null)
    {
        var snapshot = await ReadFileSnapshotCoreAsync(
            relativePath,
            CancellationToken.None, lease);
        if (snapshot == null)
            return null;

        using var stream = new MemoryStream(snapshot.Content, writable: false);
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync();
    }

    public async Task<byte[]?> ReadFileBytesAsync(string relativePath)
    {
        return await ReadFileBytesWithPublicationRecoveryAsync(
            relativePath,
            CancellationToken.None);
    }

    internal async Task<byte[]?> ReadFileBytesAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        return await ReadFileBytesWithPublicationRecoveryAsync(
            relativePath,
            cancellationToken);
    }

    /// <summary>
    /// Reads detached bytes using the declared lease and original source reader.
    /// </summary>
    /// <param name="writeLease">
    /// Original manager-owned lease. Fresh source reads retain full main and worker admission;
    /// cached provisional DATA requires its exact live bound adapter and the owning fresh effect or success fence.
    /// </param>
    /// <param name="relativePath">
    /// Original session-relative spelling; invalid or null paths retain their original refusal.
    /// </param>
    /// <returns>
    /// The observed content, or <see langword="null"/> for an absent source.
    /// </returns>
    internal async Task<byte[]?> ReadFileBytesAsync(
        CanonicalWriteLease writeLease,
        string relativePath)
    {
#if DEBUG
        BrowserDiagnosticContent(relativePath, "explicit-bytes-async", "semantic", "started");
        try
        {
#endif
        if (TryReadDeclaredSourceData(writeLease, relativePath, true, out var cached)) return cached?.Content;
        EnsureValidCanonicalWriteLease(writeLease);
        var ordinary = UsesTrustedLocalWriter(writeLease, relativePath);
        var result = ordinary
            ? await ReadOrdinaryFileBytesCoreAsync(relativePath, CancellationToken.None, writeLease)
            : await ReadFileBytesCoreAsync(relativePath, CancellationToken.None, writeLease);

        RememberDeclaredSourceRoute(writeLease, relativePath, true, ordinary);

#if DEBUG
        BrowserDiagnosticContent(relativePath, "explicit-bytes-async", "semantic", result == null ? "returned-missing" : "returned-present");
#endif
        return result;

#if DEBUG
        }
        catch { BrowserDiagnosticContent(relativePath, "explicit-bytes-async", "semantic", "fault"); throw; }
#endif
    }

    /// <summary>
    /// Reads detached content and timestamp using the declared lease and original reader family.
    /// </summary>
    /// <param name="writeLease">
    /// Original manager-owned lease. Fresh source reads retain full main and worker admission;
    /// cached provisional DATA requires its exact live bound adapter and the owning fresh effect or success fence.
    /// </param>
    /// <param name="relativePath">
    /// Original session-relative spelling; invalid or null paths retain their original refusal.
    /// </param>
    /// <returns>
    /// The observed content, or <see langword="null"/> for an absent source.
    /// </returns>
    internal async Task<CanonicalFileReadSnapshot?> ReadFileSnapshotAsync(
        CanonicalWriteLease writeLease,
        string relativePath)
    {
#if DEBUG
        BrowserDiagnosticContent(relativePath, "explicit-snapshot-async", "semantic", "started");
        try
        {
#endif
        if (TryReadDeclaredSourceData(writeLease, relativePath, true, out var cached)) return cached;
        EnsureValidCanonicalWriteLease(writeLease);
        var ordinary = UsesTrustedLocalWriter(writeLease, relativePath);
        var result = ordinary
            ? await ReadOrdinaryFileSnapshotCoreAsync(relativePath, CancellationToken.None, writeLease)
            : await ReadFileSnapshotCoreAsync(relativePath, CancellationToken.None, writeLease);

        RememberDeclaredSourceRoute(writeLease, relativePath, true, ordinary);

#if DEBUG
        BrowserDiagnosticContent(relativePath, "explicit-snapshot-async", "semantic", result == null ? "returned-missing" : "returned-present");
#endif
        return result;

#if DEBUG
        }
        catch { BrowserDiagnosticContent(relativePath, "explicit-snapshot-async", "semantic", "fault"); throw; }
#endif
    }

    internal string? ReadFileSync(string relativePath)
    {
#if DEBUG
        BrowserDiagnosticContent(relativePath, "public-text-sync", "semantic", "started");
#endif
#if DEBUG
        try
        {
#endif
        RecoverPendingFilePublicationsBeforeCanonicalRead();
        var snapshot = ReadOrdinaryFileSnapshotCore(relativePath);
#if DEBUG
        BrowserDiagnosticContent(relativePath, "public-text-sync", "initial", snapshot == null ? "missing" : "present");
#endif
        InvokeAfterCanonicalReadAttempt(relativePath);
        if (!HasAmbientCanonicalLease() &&
            (snapshot == null || HasPendingFilePublications()))
        {
#if DEBUG
            BrowserDiagnosticContent(relativePath, "public-text-sync", "followup-reason", snapshot == null ? "missing" : "pending");
#endif
            var writeLease = AcquirePublicationReadQuiescenceLeaseAsync()
                .GetAwaiter()
                .GetResult();
            try
            {
                snapshot = ReadOrdinaryFileSnapshotCore(relativePath);
#if DEBUG
                BrowserDiagnosticContent(relativePath, "public-text-sync", "followup", snapshot == null ? "missing" : "present");
#endif
            }
            finally
            {
                writeLease.DisposeAsync()
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
            }
        }
#if DEBUG
        BrowserDiagnosticContent(relativePath, "public-text-sync", "semantic", snapshot == null ? "returned-missing" : "returned-present");
#endif
        return DecodeFileSnapshot(snapshot);

#if DEBUG
        }
        catch { BrowserDiagnosticContent(relativePath, "public-text-sync", "semantic", "fault"); throw; }
#endif
    }

    /// <summary>
    /// Reads text synchronously under the supplied original lease without acquiring a new lease.
    /// </summary>
    /// <param name="writeLease">
    /// Original manager-owned lease. Fresh source reads retain full main and worker admission;
    /// cached provisional DATA requires its exact live bound adapter and the owning fresh effect or success fence.
    /// </param>
    /// <param name="relativePath">
    /// Original session-relative spelling; invalid or null paths retain their original refusal.
    /// </param>
    /// <returns>
    /// The observed content, or <see langword="null"/> for an absent source.
    /// </returns>
    internal string? ReadFileSync(
        CanonicalWriteLease writeLease,
        string relativePath)
    {
        if (TryReadDeclaredSourceData(writeLease, relativePath, false, out var cached)) return DecodeFileSnapshot(cached);
        EnsureValidCanonicalWriteLease(writeLease);
        // The explicit lease already supplies publication quiescence, even when
        // its ambient execution context did not flow to this synchronous caller.
        var ordinary = UsesTrustedLocalWriter(writeLease, relativePath);
        var result = DecodeFileSnapshot(ordinary
            ? ReadOrdinaryFileSnapshotCore(relativePath, writeLease)
            : ReadFileSnapshotCore(relativePath, writeLease));
        RememberDeclaredSourceRoute(writeLease, relativePath, false, ordinary);
        return result;
    }

    private static string? DecodeFileSnapshot(CanonicalFileReadSnapshot? snapshot)
    {
        if (snapshot == null)
            return null;

        return DecodeFileBytes(snapshot.Content);
    }

    internal static string DecodeFileBytes(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Reads detached bytes synchronously while retaining original admission checks.
    /// </summary>
    /// <param name="writeLease">
    /// Original manager-owned lease. Fresh source reads retain full main and worker admission;
    /// cached provisional DATA requires its exact live bound adapter and the owning fresh effect or success fence.
    /// </param>
    /// <param name="relativePath">
    /// Original session-relative spelling; invalid or null paths retain their original refusal.
    /// </param>
    /// <returns>
    /// The observed content, or <see langword="null"/> for an absent source.
    /// </returns>
    internal byte[]? ReadFileBytesSync(CanonicalWriteLease writeLease, string relativePath)
    {
        if (TryReadDeclaredSourceData(writeLease, relativePath, false, out var cached)) return cached?.Content;
        EnsureValidCanonicalWriteLease(writeLease);
        var ordinary = UsesTrustedLocalWriter(writeLease, relativePath);
        var snapshot = ordinary
            ? ReadOrdinaryFileSnapshotCore(relativePath, writeLease)
            : ReadFileSnapshotCore(relativePath, writeLease);
        EnsureValidCanonicalWriteLease(writeLease);
        RememberDeclaredSourceRoute(writeLease, relativePath, false, ordinary);
        return snapshot?.Content;
    }

    internal byte[]? ReadFileBytesSync(string relativePath)
    {
#if DEBUG
        BrowserDiagnosticContent(relativePath, "public-bytes-sync", "semantic", "started");
#endif
#if DEBUG
        try
        {
#endif
        RecoverPendingFilePublicationsBeforeCanonicalRead();
        var snapshot = ReadOrdinaryFileSnapshotCore(relativePath);
#if DEBUG
        BrowserDiagnosticContent(relativePath, "public-bytes-sync", "initial", snapshot == null ? "missing" : "present");
#endif
        InvokeAfterCanonicalReadAttempt(relativePath);
        if (!HasAmbientCanonicalLease() &&
            (snapshot == null || HasPendingFilePublications()))
        {
#if DEBUG
            BrowserDiagnosticContent(relativePath, "public-bytes-sync", "followup-reason", snapshot == null ? "missing" : "pending");
#endif
            var writeLease = AcquirePublicationReadQuiescenceLeaseAsync()
                .GetAwaiter()
                .GetResult();
            try
            {
                snapshot = ReadOrdinaryFileSnapshotCore(relativePath);
#if DEBUG
                BrowserDiagnosticContent(relativePath, "public-bytes-sync", "followup", snapshot == null ? "missing" : "present");
#endif
            }
            finally
            {
                writeLease.DisposeAsync()
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
            }
        }
#if DEBUG
        BrowserDiagnosticContent(relativePath, "public-bytes-sync", "semantic", snapshot == null ? "returned-missing" : "returned-present");
#endif
        return snapshot?.Content;

#if DEBUG
        }
        catch { BrowserDiagnosticContent(relativePath, "public-bytes-sync", "semantic", "fault"); throw; }
#endif
    }

    private Task<byte[]?> ReadFileBytesCoreAsync(string relativePath) =>
        ReadFileBytesCoreAsync(relativePath, CancellationToken.None);

    private async Task<byte[]?> ReadFileBytesWithPublicationRecoveryAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
#if DEBUG
        BrowserDiagnosticContent(relativePath, "public-bytes-async", "semantic", "started");
#endif
#if DEBUG
        try
        {
#endif
        await RecoverPendingFilePublicationsBeforeCanonicalReadAsync(
            cancellationToken);
        var result = await ReadOrdinaryFileBytesCoreAsync(
            relativePath,
            cancellationToken);
#if DEBUG
        BrowserDiagnosticContent(relativePath, "public-bytes-async", "initial", result == null ? "missing" : "present");
#endif
        await InvokeAfterCanonicalReadAttemptAsync(relativePath);
        if (!HasAmbientCanonicalLease() &&
            (result == null || HasPendingFilePublications()))
        {
#if DEBUG
            BrowserDiagnosticContent(relativePath, "public-bytes-async", "followup-reason", result == null ? "missing" : "pending");
#endif
            await using var writeLease =
                await AcquirePublicationReadQuiescenceLeaseAsync(
                    cancellationToken);
            result = await ReadOrdinaryFileBytesCoreAsync(
                relativePath,
                cancellationToken);
#if DEBUG
                BrowserDiagnosticContent(relativePath, "public-bytes-async", "followup", result == null ? "missing" : "present");
#endif
        }

#if DEBUG
        BrowserDiagnosticContent(relativePath, "public-bytes-async", "semantic", result == null ? "returned-missing" : "returned-present");
#endif
        return result;

#if DEBUG
        }
        catch { BrowserDiagnosticContent(relativePath, "public-bytes-async", "semantic", "fault"); throw; }
#endif
    }

    /// <summary>
    /// Returns detached bytes from the original protected snapshot reader.
    /// </summary>
    /// <param name="relativePath">
    /// Original relative path spelling.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation for the original asynchronous read.
    /// </param>
    /// <param name="lease">
    /// Original declared lease, or <see langword="null"/> for lease-free semantics.
    /// </param>
    /// <returns>
    /// Read bytes, or <see langword="null"/> for absence.
    /// </returns>
    private async Task<byte[]?> ReadFileBytesCoreAsync(
        string relativePath,
        CancellationToken cancellationToken, CanonicalWriteLease? lease = null)
    {
        var snapshot = await ReadFileSnapshotCoreAsync(
            relativePath,
            cancellationToken, lease);
        return snapshot?.Content;
    }

    /// <summary>
    /// Reads the protected asynchronous source through its unchanged handle and retry contract.
    /// </summary>
    /// <param name="relativePath">
    /// Original file spelling passed to the canonical resolver.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation for open, retry and copy operations.
    /// </param>
    /// <returns>
    /// Fresh bytes/timestamp or observed absence.
    /// </returns>
    private async Task<CanonicalFileReadSnapshot?> ReadFileSnapshotUncachedAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        StableReadFile? openedFile = null;
        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                openedFile = await OpenCanonicalReadStreamAsync(
                    relativePath,
                    cancellationToken);
                break;
            }
            catch (Exception ex) when (
                IsTransientReadOpenException(ex) &&
                attempt < TransientFileAccessRetryCount)
            {
                await Task.Delay(TransientFileAccessRetryDelay, cancellationToken);
            }
        }

        if (openedFile == null)
            return null;

        await using (openedFile)
        {
            try
            {
                var stream = openedFile.Stream;
                var lastWriteTimeUtc = File.GetLastWriteTimeUtc(
                    stream.SafeFileHandle);
                using var buffer = stream.Length is > 0 and <= int.MaxValue
                    ? new MemoryStream((int)stream.Length)
                    : new MemoryStream();
                await stream.CopyToAsync(buffer, cancellationToken);
                openedFile.Complete();
                return new CanonicalFileReadSnapshot(
                    buffer.ToArray(),
                    lastWriteTimeUtc);
            }
            catch
            {
                openedFile.Abandon();
                throw;
            }
        }
    }

    private static bool IsTransientReadOpenException(Exception ex) =>
        ex is IOException &&
        (ex.HResult & 0xFFFF) is 32 or 33;

    private static bool IsTransientFileAccessException(Exception ex) =>
        ex is IOException or UnauthorizedAccessException;

    /// <summary>
    /// Observes ordinary canonical file presence after the existing publication recovery and follow-up probes.
    /// </summary>
    /// <param name="relativePath">
    /// Session-relative file path; <see langword="null"/> is rejected during path resolution.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when an ordinary file is observed; otherwise, <see langword="false"/> for absence.
    /// Invalid namespace entries retain their existing refusal behavior.
    /// </returns>
    public bool FileExists(string relativePath)
    {
        using var t351Exists = MeasureOperation("CanonicalExists.FileExists");
        RecoverPendingFilePublicationsBeforeCanonicalRead();
        var exists = OrdinaryFileExistsCore(relativePath);
        InvokeAfterCanonicalReadAttempt(relativePath);
        if (!HasAmbientCanonicalLease())
        {
            var expectedFullPath = ResolvePath(relativePath);
            if (HasPendingFilePublications())
            {
                var writeLease = AcquirePublicationReadQuiescenceLeaseAsync()
                    .GetAwaiter()
                    .GetResult();
                try
                {
                    exists = OrdinaryFileExistsCore(relativePath);
                }
                finally
                {
                    writeLease.DisposeAsync()
                        .AsTask()
                    .GetAwaiter()
                    .GetResult();
                }
            }
            else if (!exists)
            {
                using var observation =
                    new InProcessMutationObservation(
                        expectedFullPath,
                        GameSessionPath);
                var mutationBeforeProbe =
                    observation.CaptureMutationState();
                _hooks?.BeforeCanonicalExistenceFollowUpProbeAsync
                    ?.Invoke(relativePath)
                    .GetAwaiter()
                    .GetResult();
                exists = OrdinaryFileExistsCore(relativePath);
                var mutationAfterProbe =
                    observation.CaptureMutationState();
                if (mutationBeforeProbe.MutationActive ||
                    mutationAfterProbe.MutationActive ||
                    mutationBeforeProbe.Version !=
                    mutationAfterProbe.Version ||
                    HasPendingFilePublications())
                {
                    var writeLease =
                        AcquirePublicationReadQuiescenceLeaseAsync()
                            .GetAwaiter()
                            .GetResult();
                    try
                    {
                        exists = OrdinaryFileExistsCore(relativePath);
                    }
                    finally
                    {
                        writeLease.DisposeAsync()
                            .AsTask()
                            .GetAwaiter()
                            .GetResult();
                    }
                }
            }
        }
        RememberValidationPresence(relativePath, null, exists);
        return exists;
    }

    /// <summary>
    /// Observes file presence through the original explicitly leased reader and records any active validation dependency.
    /// </summary>
    /// <param name="writeLease">
    /// Original active canonical lease, validated before selecting its existing reader contract.
    /// </param>
    /// <param name="relativePath">
    /// Raw session-relative path passed unchanged to the original presence reader.
    /// </param>
    /// <returns>
    /// True for an existing ordinary file; false for absence. Original namespace and lease failures propagate.
    /// </returns>
    internal bool FileExists(CanonicalWriteLease writeLease, string relativePath)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        var exists = UsesTrustedLocalWriter(writeLease, relativePath)
            ? OrdinaryFileExistsCore(relativePath)
            : FileExistsCore(relativePath);
        RememberValidationPresence(relativePath, writeLease, exists);
        return exists;
    }

    private bool FileExistsCore(string relativePath)
    {
        var expectedFullPath = ResolvePath(relativePath);
        EnsureCanonicalPathStillSafe(relativePath, expectedFullPath);
        return PhysicalFileAuthority.ProbeNamespaceEntryFromRoot(
                GameSessionPath,
                expectedFullPath,
                "Canonical file existence") switch
        {
            PhysicalFileAuthority.NamespaceEntryKind.Missing => false,
            PhysicalFileAuthority.NamespaceEntryKind.RegularFile => true,
            PhysicalFileAuthority.NamespaceEntryKind.Directory =>
                throw new InvalidDataException(
                    "Canonical file authority resolved to a directory."),
            PhysicalFileAuthority.NamespaceEntryKind.ReparsePoint =>
                throw new InvalidDataException(
                    "Canonical file authority resolved to a reparse point."),
            _ => throw new InvalidDataException(
                "Canonical file authority resolved to an unknown namespace entry.")
        };
    }

    internal IReadOnlyList<string> EnumerateFiles(
        CanonicalWriteLease writeLease,
        string searchPattern)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        if (string.IsNullOrWhiteSpace(searchPattern))
            throw new ArgumentException("Search pattern is required.", nameof(searchPattern));

        return EnumerateFilesWithoutFollowingReparsePoints(GameSessionPath, searchPattern)
            .Select(path => GetLocalRelativePath(GameSessionPath, path, OperatingSystem.IsWindows()))
            .ToArray();
    }

    internal bool DirectoryHasContent(
        CanonicalWriteLease writeLease,
        string relativePath)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        return DirectoryHasLocalContent(relativePath);
    }

    internal bool DirectoryHasContent(string relativePath)
    {
        if (HasAmbientCanonicalLease()) return DirectoryHasLocalContent(relativePath);
        var lease = AcquirePublicationReadQuiescenceLeaseAsync().GetAwaiter().GetResult();
        try { return DirectoryHasContent(lease, relativePath); }
        finally { lease.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }

    private bool DirectoryHasLocalContent(string relativePath) =>
        EnumerateLocalTreeFiles(new TrustedLocalFileScope([GameSessionPath]),
            ResolvePath(relativePath)).Count != 0;

    internal bool DirectoryExists(
        CanonicalWriteLease writeLease,
        string relativePath)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        var fullPath = ResolvePath(relativePath);
        return PhysicalFileAuthority.ProbeNamespaceEntryFromRoot(
                GameSessionPath,
                fullPath,
                "Canonical directory existence") switch
        {
            PhysicalFileAuthority.NamespaceEntryKind.Missing => false,
            PhysicalFileAuthority.NamespaceEntryKind.Directory => true,
            PhysicalFileAuthority.NamespaceEntryKind.RegularFile =>
                throw new InvalidDataException(
                    "Canonical directory authority resolved to a regular file."),
            PhysicalFileAuthority.NamespaceEntryKind.ReparsePoint =>
                throw new InvalidDataException(
                    "Canonical directory authority resolved to a reparse point."),
            _ => throw new InvalidDataException(
                "Canonical directory authority resolved to an unknown namespace entry.")
        };
    }

    /// <summary>
    /// Deletes the requested canonical tree through its existing ordinary or legacy route.
    /// </summary>
    /// <param name="writeLease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <param name="relativePath">
    /// Canonical session-relative member or subtree to operate on.
    /// </param>
    internal void DeleteDirectoryTree(
        CanonicalWriteLease writeLease,
        string relativePath)
    {
        using var validationMutation = BeginValidationMutation(writeLease);
        EnsureWorkerGeneralMutationAllowed(writeLease);
        writeLease.EnsureNoPendingLocalDecision();
        EnsureSafeCanonicalRelativePath(relativePath);
        if (UsesTrustedLocalWriter(writeLease, relativePath))
        {
            DeleteTrustedLocalDirectoryTreeAsync(writeLease, relativePath).GetAwaiter().GetResult();
            return;
        }
        DeleteOriginalDirectoryTree(writeLease, relativePath);
    }

    // Original browser rollback owns dynamic cleanup even after clearing its
    // recorder. Such cleanup must never enter a new common publication journal.
    /// <summary>
    /// Deletes an original canonical tree under the existing lease and namespace checks.
    /// </summary>
    /// <param name="writeLease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <param name="relativePath">
    /// Canonical session-relative member or subtree to operate on.
    /// </param>
    internal void DeleteOriginalDirectoryTree(
        CanonicalWriteLease writeLease,
        string relativePath)
    {
        using var validationMutation = BeginValidationMutation(writeLease);
        EnsureWorkerGeneralMutationAllowed(writeLease);
        writeLease.EnsureNoPendingLocalDecision();
        EnsureSafeCanonicalRelativePath(relativePath);
        if (writeLease.BrowserLocalAccess != null)
        {
            DeleteTrustedLocalDirectoryTreeAsync(writeLease, relativePath).GetAwaiter().GetResult();
            return;
        }
        var fullPath = ResolvePath(relativePath);
        InvokeBeforeCanonicalMutationBoundaryAsync(relativePath).GetAwaiter().GetResult();
        using var parentAuthority = EnsureStableCanonicalParent(
            relativePath,
            fullPath);
        EnsureCanonicalMutationBoundary(relativePath, fullPath);
        InvokeAfterCanonicalMutationBoundaryValidatedAsync(relativePath)
            .GetAwaiter()
            .GetResult();
        PhysicalFileAuthority.TryDeleteDirectoryTree(
            parentAuthority,
            fullPath,
            "Canonical directory-tree deletion");
    }

    /// <summary>
    /// Moves the admitted runtime directory into the canonical namespace, retaining mutation admission through cleanup.
    /// </summary>
    /// <param name="writeLease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <param name="sourceDirectoryPath">
    /// Admitted runtime directory to move into the canonical namespace.
    /// </param>
    /// <param name="destinationRelativePath">
    /// Canonical session-relative destination checked by the existing publication protocol.
    /// </param>
    /// <returns>
    /// Completion after the operation and its required cleanup finish.
    /// </returns>
    internal async Task MoveRuntimeDirectoryIntoCanonicalSessionAsync(
        CanonicalWriteLease writeLease,
        string sourceDirectoryPath,
        string destinationRelativePath)
    {
        using var validationMutation = BeginValidationMutation(writeLease);
        EnsureWorkerGeneralMutationAllowed(writeLease);
        writeLease.EnsureNoPendingLocalDecision();
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectoryPath);
        EnsureSafeCanonicalRelativePath(destinationRelativePath);
        EnsureDescriptorBoundCreateOnlyPublicationSupported(
            "Runtime proposal publication");

        var stagingRoot = Path.GetFullPath(Path.Combine(
            RuntimeRootPath,
            "proposal-staging"));
        var sourcePath = Path.GetFullPath(sourceDirectoryPath);
        if (!IsSameOrDescendant(sourcePath, stagingRoot) ||
            string.Equals(sourcePath, stagingRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Runtime directory move source must be inside proposal staging.");
        }

        EnsureRuntimePathIsSafe(sourcePath);
        if (!Directory.Exists(sourcePath))
            throw new DirectoryNotFoundException(
                $"Runtime staging directory does not exist: {sourcePath}");

        var destinationPath = ResolvePath(destinationRelativePath);
        using var mutationRegistration =
            new InProcessMutationRegistration(destinationPath);
        var destinationParent = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidDataException("Canonical destination has no parent directory.");
        await InvokeBeforeCanonicalMutationBoundaryAsync(destinationRelativePath);
        EnsureRuntimePathIsSafe(sourcePath);
        using var sourceParentAuthority = EnsureStableRuntimeDirectory(
            Path.GetDirectoryName(sourcePath)
            ?? throw new InvalidDataException(
                "Runtime staging directory source has no parent."));
        using var destinationParentAuthority = EnsureStableCanonicalParent(
            destinationRelativePath,
            destinationPath);
        using var sourceHandle = PhysicalFileAuthority.OpenForRename(
            sourceParentAuthority,
            sourcePath,
            isDirectory: true,
            "Runtime proposal publication");
        EnsureCanonicalMutationBoundary(destinationRelativePath, destinationPath);
        await InvokeAfterCanonicalMutationBoundaryValidatedAsync(destinationRelativePath);
        PhysicalFileAuthority.RenameOpenedObjectRelative(
            sourceHandle,
            destinationParentAuthority,
            destinationPath,
            replaceExisting: false,
            "Runtime proposal publication",
            requireSingleLink: false);
    }

    /// <summary>
    /// Reads a fresh protected synchronous snapshot with the original native handle completion checks.
    /// </summary>
    /// <param name="relativePath">
    /// Original path spelling to resolve and validate.
    /// </param>
    /// <returns>
    /// Fresh bytes/timestamp or observed absence; original path and data refusals propagate.
    /// </returns>
    private CanonicalFileReadSnapshot? ReadFileSnapshotUncached(
        string relativePath)
    {
        for (var attempt = 0; ; attempt++)
        {
            var initialValidationComplete = false;
            try
            {
                var expectedFullPath = ResolvePath(relativePath);
                if (!FileExistsCore(relativePath))
                    return null;

                using var parentAuthority = EnsureStableCanonicalParent(
                    relativePath,
                    expectedFullPath);
                EnsureCanonicalPathStillSafe(relativePath, expectedFullPath);
                using var stream = PhysicalFileAuthority.OpenReadFile(
                    parentAuthority,
                    expectedFullPath,
                    "Canonical synchronous game-session read",
                    asynchronous: false);
                if (stream == null)
                    return null;

                initialValidationComplete = true;
                _hooks?.AfterCanonicalReadInitialValidationAsync
                    ?.Invoke(relativePath)
                    .GetAwaiter()
                    .GetResult();
                ObservePassiveRead(_hooks?.CanonicalReadActivityObserver, relativePath);
                var lastWriteTimeUtc = File.GetLastWriteTimeUtc(
                    stream.SafeFileHandle);
                using var buffer = stream.Length is > 0 and <= int.MaxValue
                    ? new MemoryStream((int)stream.Length)
                    : new MemoryStream();
                stream.CopyTo(buffer);
                PhysicalFileAuthority.EnsureRegularFileHandleMatchesExpectedPath(
                    stream.SafeFileHandle,
                    expectedFullPath,
                    "Canonical synchronous game-session read completion");
                return new CanonicalFileReadSnapshot(
                    buffer.ToArray(),
                    lastWriteTimeUtc);
            }
            catch (Exception ex) when (
                !initialValidationComplete &&
                IsTransientReadOpenException(ex) &&
                attempt < TransientFileAccessRetryCount)
            {
                Thread.Sleep(TransientFileAccessRetryDelay);
            }
        }
    }

    internal sealed record CanonicalFileReadSnapshot(
        byte[] Content,
        DateTime LastWriteTimeUtc);

    internal sealed class StableReadFile : IAsyncDisposable
    {
        private FileStream? _stream;
        private PhysicalFileAuthority.StableDirectory? _parentAuthority;
        private readonly string _expectedPath;
        private readonly string _authorityName;
        private bool _completionResolved;

        internal StableReadFile(
            FileStream stream,
            PhysicalFileAuthority.StableDirectory parentAuthority,
            string expectedPath,
            string authorityName)
        {
            _stream = stream;
            _parentAuthority = parentAuthority;
            _expectedPath = Path.GetFullPath(expectedPath);
            _authorityName = authorityName;
        }

        internal FileStream Stream => _stream ??
            throw new ObjectDisposedException(nameof(StableReadFile));

        internal void Complete()
        {
            PhysicalFileAuthority.EnsureRegularFileHandleMatchesExpectedPath(
                Stream.SafeFileHandle,
                _expectedPath,
                $"{_authorityName} completion");
            _completionResolved = true;
        }

        internal void Abandon()
        {
            _ = Stream;
            _completionResolved = true;
        }

        public async ValueTask DisposeAsync()
        {
            var stream = _stream;
            var parentAuthority = _parentAuthority;
            var completionResolved = _completionResolved;
            _stream = null;
            _parentAuthority = null;
            if (stream != null)
                await stream.DisposeAsync();
            parentAuthority?.Dispose();
            if (stream != null && !completionResolved)
            {
                throw new InvalidOperationException(
                    $"{_authorityName} was disposed without completion validation or explicit abandonment.");
            }
        }
    }

    internal sealed class RuntimeStagedFile : IAsyncDisposable
    {
        private FileStream? _stream;
        private PhysicalFileAuthority.StableDirectory? _parentAuthority;

        internal RuntimeStagedFile(
            FileSystemManager owner,
            string path,
            FileStream stream,
            PhysicalFileAuthority.StableDirectory parentAuthority)
        {
            Owner = owner;
            Path = System.IO.Path.GetFullPath(path);
            _stream = stream;
            _parentAuthority = parentAuthority;
        }

        internal FileSystemManager Owner { get; }
        internal string Path { get; }
        internal FileStream Stream => _stream ??
            throw new ObjectDisposedException(nameof(RuntimeStagedFile));
        internal PhysicalFileAuthority.StableDirectory ParentAuthority =>
            _parentAuthority ??
            throw new ObjectDisposedException(nameof(RuntimeStagedFile));

        public async ValueTask DisposeAsync()
        {
            var stream = _stream;
            var parentAuthority = _parentAuthority;
            _stream = null;
            _parentAuthority = null;
            if (stream != null)
                await stream.DisposeAsync();
            parentAuthority?.Dispose();
        }
    }

    internal string CreateRuntimeProposalStagingRoot()
        => CreateRuntimeStagingRoot("proposal-staging");

    internal void DeleteRuntimeProposalStagingRoot(string stagingRoot)
        => DeleteRuntimeStagingRoot("proposal-staging", stagingRoot);

    internal string CreateRuntimeSaveStagingRoot()
        => CreateRuntimeStagingRoot("save-staging");

    internal void DeleteRuntimeSaveStagingRoot(string stagingRoot)
        => DeleteRuntimeStagingRoot("save-staging", stagingRoot);

    internal async Task WriteRuntimeStagedFileAsync(
        string path,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureRuntimePathIsSafe(path);
        var parent = Path.GetDirectoryName(path)
            ?? throw new InvalidDataException(
                "Runtime staging file has no parent directory.");
        EnsureRuntimeDirectoryExistsAndIsSafe(parent);
        using var parentAuthority = EnsureStableRuntimeDirectory(parent);
        if (_hooks?.BeforeRuntimeFileCreateAsync != null)
            await _hooks.BeforeRuntimeFileCreateAsync(path);

        await using var stream = PhysicalFileAuthority.CreateNewWritableFile(
            parentAuthority,
            path,
            "Runtime staging",
            asynchronous: true);
        await stream.WriteAsync(content, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        stream.Flush(flushToDisk: true);
        EnsureRuntimePathIsSafe(path);
    }

    internal async Task<RuntimeStagedFile> CreateRuntimeStagedFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureRuntimePathIsSafe(path);
        var parent = Path.GetDirectoryName(path)
            ?? throw new InvalidDataException(
                "Runtime staged file has no parent directory.");
        EnsureRuntimeDirectoryExistsAndIsSafe(parent);
        var parentAuthority = EnsureStableRuntimeDirectory(parent);
        FileStream? stream = null;
        try
        {
            if (_hooks?.BeforeRuntimeFileCreateAsync != null)
                await _hooks.BeforeRuntimeFileCreateAsync(path);
            stream = PhysicalFileAuthority.CreateNewWritableFile(
                parentAuthority,
                path,
                "Runtime staged file",
                asynchronous: true);
            return new RuntimeStagedFile(
                this,
                path,
                stream,
                parentAuthority);
        }
        catch
        {
            if (stream != null)
                await stream.DisposeAsync();
            parentAuthority.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Publishes the admitted runtime file into the canonical namespace under the existing destination checks.
    /// </summary>
    /// <param name="writeLease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <param name="stagedFile">
    /// Original staged file and its retained physical authority.
    /// </param>
    /// <param name="destinationRelativePath">
    /// Canonical session-relative destination checked by the existing publication protocol.
    /// </param>
    /// <returns>
    /// Completion after the operation and its required cleanup finish.
    /// </returns>
    internal async Task MoveRuntimeFileIntoCanonicalSessionAsync(
        CanonicalWriteLease writeLease,
        RuntimeStagedFile stagedFile,
        string destinationRelativePath)
    {
        using var validationMutation = BeginValidationMutation(writeLease);
        EnsureWorkerGeneralMutationAllowed(writeLease);
        writeLease.EnsureNoPendingLocalDecision();
        ArgumentNullException.ThrowIfNull(stagedFile);
        EnsureDescriptorBoundCreateOnlyPublicationSupported(
            "Runtime save publication");
        if (!ReferenceEquals(stagedFile.Owner, this))
        {
            throw new InvalidOperationException(
                "Runtime staged file belongs to another file-system authority.");
        }

        EnsureSafeCanonicalRelativePath(destinationRelativePath);
        EnsureRuntimePathIsSafe(stagedFile.Path);
        var saveStagingRoot = Path.GetFullPath(Path.Combine(
            RuntimeRootPath,
            "save-staging"));
        if (!IsSameOrDescendant(stagedFile.Path, saveStagingRoot) ||
            string.Equals(
                stagedFile.Path,
                saveStagingRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Runtime save source must be inside save staging.");
        }

        var destinationPath = ResolvePath(destinationRelativePath);
        using var mutationRegistration =
            new InProcessMutationRegistration(destinationPath);
        await InvokeBeforeCanonicalMutationBoundaryAsync(destinationRelativePath);
        using var destinationParentAuthority = EnsureStableCanonicalParent(
            destinationRelativePath,
            destinationPath);
        PhysicalFileAuthority.EnsureHandleMatchesExpectedPath(
            stagedFile.Stream.SafeFileHandle,
            stagedFile.Path,
            "Runtime staged save");
        EnsureCanonicalMutationBoundary(destinationRelativePath, destinationPath);
        await InvokeAfterCanonicalMutationBoundaryValidatedAsync(destinationRelativePath);
        await stagedFile.Stream.FlushAsync();
        stagedFile.Stream.Flush(flushToDisk: true);
        PhysicalFileAuthority.RenameOpenedObjectRelative(
            stagedFile.Stream.SafeFileHandle,
            destinationParentAuthority,
            destinationPath,
            replaceExisting: false,
            "Runtime save publication");

        await stagedFile.DisposeAsync();
    }

    /// <summary>
    /// Publishes the admitted runtime file into the canonical namespace under the existing destination checks.
    /// </summary>
    /// <param name="writeLease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <param name="sourceFilePath">
    /// Admitted runtime file to move into the canonical namespace.
    /// </param>
    /// <param name="destinationRelativePath">
    /// Canonical session-relative destination checked by the existing publication protocol.
    /// </param>
    /// <returns>
    /// Completion after the operation and its required cleanup finish.
    /// </returns>
    internal async Task MoveRuntimeFileIntoCanonicalSessionAsync(
        CanonicalWriteLease writeLease,
        string sourceFilePath,
        string destinationRelativePath)
    {
        using var validationMutation = BeginValidationMutation(writeLease);
        EnsureWorkerGeneralMutationAllowed(writeLease);
        writeLease.EnsureNoPendingLocalDecision();
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);
        EnsureSafeCanonicalRelativePath(destinationRelativePath);
        EnsureDescriptorBoundCreateOnlyPublicationSupported(
            "Runtime save publication");

        var saveStagingRoot = Path.GetFullPath(Path.Combine(
            RuntimeRootPath,
            "save-staging"));
        var sourcePath = Path.GetFullPath(sourceFilePath);
        if (!IsSameOrDescendant(sourcePath, saveStagingRoot) ||
            string.Equals(
                sourcePath,
                saveStagingRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Runtime save source must be inside save staging.");
        }

        EnsureRuntimePathIsSafe(sourcePath);
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Runtime staged save does not exist.", sourcePath);

        var destinationPath = ResolvePath(destinationRelativePath);
        using var mutationRegistration =
            new InProcessMutationRegistration(destinationPath);
        var destinationParent = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidDataException("Canonical save destination has no parent directory.");
        await InvokeBeforeCanonicalMutationBoundaryAsync(destinationRelativePath);
        EnsureRuntimePathIsSafe(sourcePath);
        using var sourceParentAuthority = EnsureStableRuntimeDirectory(
            Path.GetDirectoryName(sourcePath)
            ?? throw new InvalidDataException(
                "Runtime staged save source has no parent."));
        using var destinationParentAuthority = EnsureStableCanonicalParent(
            destinationRelativePath,
            destinationPath);
        using var sourceHandle = PhysicalFileAuthority.OpenForRename(
            sourceParentAuthority,
            sourcePath,
            isDirectory: false,
            "Runtime save publication");
        EnsureCanonicalMutationBoundary(destinationRelativePath, destinationPath);
        await InvokeAfterCanonicalMutationBoundaryValidatedAsync(destinationRelativePath);
        PhysicalFileAuthority.RenameOpenedObjectRelative(
            sourceHandle,
            destinationParentAuthority,
            destinationPath,
            replaceExisting: false,
            "Runtime save publication");
    }

    /// <summary>
    /// Prunes empty directories beneath the requested canonical root without following reparse points.
    /// </summary>
    /// <param name="writeLease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <param name="relativeRoot">
    /// Canonical session-relative root beneath which empty structure is pruned.
    /// </param>
    internal void DeleteEmptyDirectories(
        CanonicalWriteLease writeLease,
        string relativeRoot)
    {
        using var validationMutation = BeginValidationMutation(writeLease);
        EnsureWorkerGeneralMutationAllowed(writeLease);
        writeLease.EnsureNoPendingLocalDecision();
        EnsureSafeCanonicalRelativePath(relativeRoot);
        DeleteEmptyDirectoriesWithoutFollowingReparsePoints(ResolvePath(relativeRoot));
    }

    public void DeleteFile(string relativePath)
    {
        DeleteFileWithLockAsync(relativePath).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Deletes the canonical member through its existing ordinary or legacy writer.
    /// </summary>
    /// <param name="writeLease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <param name="relativePath">
    /// Canonical session-relative member or subtree to operate on.
    /// </param>
    internal void DeleteFile(CanonicalWriteLease writeLease, string relativePath)
    {
        using var validationMutation = BeginValidationMutation(writeLease);
        EnsureWorkerGeneralMutationAllowed(writeLease);
        writeLease.EnsureNoPendingLocalDecision();
        if (UsesTrustedLocalWriter(writeLease, relativePath))
        {
            WriteTrustedLocalFileAsync(writeLease, relativePath, null).GetAwaiter().GetResult();
            return;
        }
        RecordCanonicalMutationIntentAsync(
                writeLease,
                relativePath,
                desiredContent: null)
            .GetAwaiter()
            .GetResult();
        var publication = DeleteFileCore(
            relativePath,
            capturePublication: writeLease.MutationIntentRecorder != null);
        if (publication != null)
        {
            RecordCanonicalMutationPublicationAsync(
                    writeLease,
                    relativePath,
                    publication)
                .GetAwaiter()
                .GetResult();
        }
        else
        {
            RecordCanonicalMutationNonPublicationAsync(
                    writeLease,
                    relativePath)
                .GetAwaiter()
                .GetResult();
        }
    }

    internal sealed class LoadStagingAuthoritySet : IAsyncDisposable
    {
        private readonly FileSystemManager _owner;
        private readonly string _stagingRoot;
        private readonly string _publicationGuardRoot;
        private readonly List<LoadStagingFileAuthority> _files = [];
        private readonly HashSet<string> _relativePaths =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _allowedDirectoryPaths =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly List<LoadStagingDirectoryAuthority>
            _publishedDirectories = [];
        private PhysicalFileAuthority.StableDirectory?
            _publishedRootAuthority;
        private PhysicalFileAuthority.FileIdentity?
            _publishedRootIdentity;
        private bool _preparedForDirectoryMove;
        private bool _sealedForCanonicalReads;
        private bool _disposed;

        internal LoadStagingAuthoritySet(
            FileSystemManager owner,
            string stagingRoot)
        {
            _owner = owner;
            _stagingRoot = Path.GetFullPath(stagingRoot);
            _owner.EnsureRuntimePathIsSafe(_stagingRoot);
            var stageRoot = Path.GetDirectoryName(_stagingRoot)
                ?? throw new InvalidDataException(
                    "Load-staging authority root has no stage parent.");
            var transactionRoot = Path.GetDirectoryName(stageRoot)
                ?? throw new InvalidDataException(
                    "Load-staging authority root has no transaction parent.");
            _publicationGuardRoot = Path.Combine(
                transactionRoot,
                "publication-authority");
            _owner.EnsureRuntimePathIsSafe(
                _publicationGuardRoot);
            foreach (var requiredDirectory in RequiredDirectories)
            {
                if (requiredDirectory.Equals(
                        "game_session",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AddAllowedDirectoryPath(
                    requiredDirectory["game_session/".Length..]);
            }
        }

        internal void Add(
            string stagedPath,
            FileStream stream,
            PhysicalFileAuthority.OpenedFileAuthority authority)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var fullPath = Path.GetFullPath(stagedPath);
            if (!IsSameOrDescendant(fullPath, _stagingRoot) ||
                string.Equals(
                    fullPath,
                    _stagingRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Load staging file is outside its retained authority root.");
            }

            var relativePath = Path
                .GetRelativePath(_stagingRoot, fullPath)
                .Replace('\\', '/');
            if (!_relativePaths.Add(relativePath))
            {
                throw new InvalidDataException(
                    $"Load staging authority contains duplicate path '{relativePath}'.");
            }
            AddAllowedDirectoryPath(
                Path.GetDirectoryName(
                    relativePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));

            _files.Add(
                new LoadStagingFileAuthority(
                    relativePath,
                    stream,
                    authority));
        }

        internal void EnsureExactAtRoot(
            string currentRoot,
            string authorityName)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_preparedForDirectoryMove)
            {
                throw new InvalidOperationException(
                    "Retained load-staging handles were already released for publication.");
            }

            var normalizedRoot = Path.GetFullPath(currentRoot);
            _owner.EnsureLoadTransactionOperationPathIsSafe(normalizedRoot);
            EnsureExactFileNamespace(normalizedRoot, authorityName);
            foreach (var file in _files)
            {
                var stream = file.Stream
                    ?? throw new InvalidOperationException(
                        "Retained load-staging handle is unavailable.");
                var expectedPath = ResolveExpectedPath(
                    normalizedRoot,
                    file.RelativePath);

                PhysicalFileAuthority.EnsureExactOpenedFileAuthority(
                    stream.SafeFileHandle,
                    expectedPath,
                    file.Authority.Identity,
                    file.Authority.Sha256,
                    $"{authorityName} '{file.RelativePath}'",
                    file.Authority.Length);
            }
            EnsureExactFileNamespace(normalizedRoot, authorityName);
        }

        internal void PrepareForDirectoryMove(
            string sourceRoot,
            string authorityName)
        {
            EnsureExactAtRoot(sourceRoot, authorityName);
            RetainPublicationGuards(
                sourceRoot,
                authorityName);
            _preparedForDirectoryMove = true;
        }

        private void RetainPublicationGuards(
            string sourceRoot,
            string authorityName)
        {
            var normalizedRoot = Path.GetFullPath(sourceRoot);
            _owner.CreateLoadDirectory(
                _publicationGuardRoot);
            using var guardParentAuthority =
                PhysicalFileAuthority.OpenStableDirectory(
                    _publicationGuardRoot,
                    $"{authorityName} publication guard root");
            for (var index = 0;
                 index < _files.Count;
                 index++)
            {
                var file = _files[index];
                var originalStream = file.Stream
                    ?? throw new InvalidOperationException(
                        "Retained load-staging handle is unavailable.");
                var sourcePath = ResolveExpectedPath(
                    normalizedRoot,
                    file.RelativePath);
                PhysicalFileAuthority.EnsureExactOpenedFileAuthority(
                    originalStream.SafeFileHandle,
                    sourcePath,
                    file.Authority.Identity,
                    file.Authority.Sha256,
                    $"{authorityName} '{file.RelativePath}'",
                    file.Authority.Length);
                var guardPath = Path.Combine(
                    _publicationGuardRoot,
                    $"{index:D8}.guard");
                PhysicalFileAuthority.CreateHardLinkRelative(
                    guardParentAuthority,
                    guardPath,
                    originalStream.SafeFileHandle,
                    sourcePath,
                    $"{authorityName} '{file.RelativePath}' guard");

                FileStream? guardStream = null;
                try
                {
                    guardStream =
                        PhysicalFileAuthority.OpenReadFile(
                            guardParentAuthority,
                            guardPath,
                            $"{authorityName} '{file.RelativePath}' guard",
                            asynchronous: false,
                            requireSingleLink: false) ??
                        throw new InvalidDataException(
                            "Load-staging publication guard disappeared.");
                    var guardAuthority =
                        PhysicalFileAuthority
                            .EnsureExactOpenedFileAuthority(
                                guardStream.SafeFileHandle,
                                guardPath,
                                file.Authority.Identity,
                                file.Authority.Sha256,
                                $"{authorityName} '{file.RelativePath}' guard",
                                file.Authority.Length,
                                expectedNumberOfLinks: 2);
                    PhysicalFileAuthority
                        .EnsureExactOpenedFileAuthority(
                            originalStream.SafeFileHandle,
                            sourcePath,
                            file.Authority.Identity,
                            file.Authority.Sha256,
                            $"{authorityName} '{file.RelativePath}' guarded source",
                            file.Authority.Length,
                            expectedNumberOfLinks: 2);

                    originalStream.Dispose();
                    file.Stream = guardStream;
                    file.Authority = guardAuthority;
                    file.GuardPath = guardPath;
                    guardStream = null;
                }
                finally
                {
                    guardStream?.Dispose();
                }
            }
        }

        internal void EnsureExactAfterDirectoryMove(
            string destinationRoot,
            string authorityName,
            PhysicalFileAuthority.FileIdentity expectedRootIdentity,
            PhysicalFileAuthority.StableDirectory publishedRootAuthority)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(publishedRootAuthority);
            if (!_preparedForDirectoryMove)
            {
                throw new InvalidOperationException(
                    "Load-staging authority was not prepared for directory publication.");
            }
            if (_publishedRootAuthority != null)
            {
                throw new InvalidOperationException(
                    "Load-staging root authority was already published.");
            }
            if (!expectedRootIdentity.IsDirectory)
            {
                throw new InvalidDataException(
                    "Load-staging publication root authority is not a directory.");
            }

            var normalizedRoot = Path.GetFullPath(destinationRoot);
            _owner.EnsureLoadTransactionOperationPathIsSafe(normalizedRoot);
            PhysicalFileAuthority.StableDirectory? renameRootAuthority =
                publishedRootAuthority;
            try
            {
                renameRootAuthority.RebindFullPathAfterRename(
                    normalizedRoot,
                    authorityName);
                PhysicalFileAuthority.EnsureExactDirectoryIdentity(
                    renameRootAuthority,
                    expectedRootIdentity,
                    authorityName);
                EnsureExactFileNamespace(normalizedRoot, authorityName);

                _publishedRootAuthority =
                    HandoffPublishedRootAuthority(
                        renameRootAuthority,
                        normalizedRoot,
                        expectedRootIdentity,
                        authorityName);
                renameRootAuthority = null;
                _publishedRootIdentity = expectedRootIdentity;

                EnsurePublishedRoot(normalizedRoot, authorityName);
                using var publicationGuardRootAuthority =
                    PhysicalFileAuthority.OpenStableDirectory(
                        _publicationGuardRoot,
                        $"{authorityName} publication guard root",
                        allowRename: true);
                foreach (var file in _files)
                {
                    var expectedPath = ResolveExpectedPath(
                        normalizedRoot,
                        file.RelativePath);
                    var guardStream = file.Stream
                        ?? throw new InvalidDataException(
                            "Published load-staging guard authority is unavailable.");
                    var guardPath = file.GuardPath
                        ?? throw new InvalidDataException(
                            "Published load-staging guard path is unavailable.");
                    PhysicalFileAuthority.EnsureExactOpenedFileAuthority(
                        guardStream.SafeFileHandle,
                        guardPath,
                        file.Authority.Identity,
                        file.Authority.Sha256,
                        $"{authorityName} '{file.RelativePath}' guard",
                        file.Authority.Length,
                        expectedNumberOfLinks: 2);
                    var parentPath = Path.GetDirectoryName(expectedPath)
                        ?? throw new InvalidDataException(
                            "Published load-staging file has no parent.");
                    using var parentAuthority =
                        PhysicalFileAuthority.EnsureStableDirectory(
                            normalizedRoot,
                            parentPath,
                            authorityName);
                    FileStream? publishedStream = null;
                    try
                    {
                        publishedStream =
                            PhysicalFileAuthority.OpenReadFile(
                                parentAuthority,
                                expectedPath,
                                authorityName,
                                asynchronous: false,
                                shareDelete: true,
                                requireSingleLink: false) ??
                            throw new InvalidDataException(
                                $"{authorityName} '{file.RelativePath}' is missing.");
                        PhysicalFileAuthority
                            .EnsureExactOpenedFileAuthority(
                                publishedStream.SafeFileHandle,
                                expectedPath,
                                file.Authority.Identity,
                                file.Authority.Sha256,
                                $"{authorityName} '{file.RelativePath}' guarded publication",
                                file.Authority.Length,
                                expectedNumberOfLinks: 2);

                        guardStream.Dispose();
                        file.Stream = null;
                        using (var guardHandle =
                               PhysicalFileAuthority.OpenForRename(
                                   publicationGuardRootAuthority,
                                   guardPath,
                                   isDirectory: false,
                                   $"{authorityName} '{file.RelativePath}' guard deletion",
                                   denyConcurrentWrites: true,
                                   requireSingleLink: false))
                        {
                            PhysicalFileAuthority
                                .EnsureExactOpenedFileAuthority(
                                    guardHandle,
                                    guardPath,
                                    file.Authority.Identity,
                                    file.Authority.Sha256,
                                    $"{authorityName} '{file.RelativePath}' guard deletion",
                                    file.Authority.Length,
                                    expectedNumberOfLinks: 2);
                            PhysicalFileAuthority.DeleteOpenedFile(
                                guardHandle,
                                $"{authorityName} '{file.RelativePath}' guard");
                        }

                        file.Authority =
                            PhysicalFileAuthority
                                .EnsureExactOpenedFileAuthority(
                                    publishedStream.SafeFileHandle,
                                    expectedPath,
                                    file.Authority.Identity,
                                    file.Authority.Sha256,
                                    $"{authorityName} '{file.RelativePath}'",
                                    file.Authority.Length);
                        file.Stream = publishedStream;
                        file.GuardPath = null;
                        publishedStream = null;
                    }
                    finally
                    {
                        publishedStream?.Dispose();
                    }
                }

                EnsureRetainedFileAuthorities(
                    normalizedRoot,
                    authorityName,
                    useSealedAuthority: false);
                EnsureExactFileNamespace(normalizedRoot, authorityName);
                _owner._hooks?
                    .BeforeLoadPublicationGuardRootDeleteAsync?
                    .Invoke(_publicationGuardRoot)
                    .GetAwaiter()
                    .GetResult();
                PhysicalFileAuthority.DeleteOpenedDirectory(
                    publicationGuardRootAuthority.Handle ??
                    throw new InvalidDataException(
                        $"{authorityName} publication guard root authority is unavailable."),
                    $"{authorityName} publication guard root");
            }
            finally
            {
                renameRootAuthority?.Dispose();
            }
        }

        internal void EnsurePublishedExactBeforeActivation(
            string publishedRoot,
            string authorityName)
        {
            EnsurePublishedState(
                publishedRoot,
                authorityName,
                useSealedAuthority: false);
        }

        internal PhysicalFileAuthority.OpenedFileAuthority
            YieldPublishedFileAuthorityForConditionalReplacement(
                string publishedRoot,
                string relativePath,
                string authorityName)
        {
            EnsurePublishedState(
                publishedRoot,
                authorityName,
                useSealedAuthority: false);
            if (_sealedForCanonicalReads)
            {
                throw new InvalidOperationException(
                    "Load-staging publication authority is already sealed.");
            }

            var normalizedRelativePath = relativePath
                .Replace('\\', '/');
            var file = _files.SingleOrDefault(candidate =>
                candidate.RelativePath.Equals(
                    normalizedRelativePath,
                    StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException(
                    "Client-owned load repair targeted an unregistered file.");
            if (file.SealedStream != null ||
                file.YieldedForConditionalReplacement)
            {
                throw new InvalidOperationException(
                    "Client-owned load repair authority was already yielded.");
            }

            var stream = file.Stream
                ?? throw new InvalidOperationException(
                    "Client-owned load repair authority is unavailable.");
            var normalizedRoot = Path.GetFullPath(publishedRoot);
            var expectedPath = ResolveExpectedPath(
                normalizedRoot,
                file.RelativePath);
            PhysicalFileAuthority.EnsureExactOpenedFileAuthority(
                stream.SafeFileHandle,
                expectedPath,
                file.Authority.Identity,
                file.Authority.Sha256,
                authorityName,
                file.Authority.Length);
            stream.Dispose();
            file.Stream = null;
            file.YieldedForConditionalReplacement = true;
            return file.Authority;
        }

        internal void RebindPublishedFileAuthority(
            string publishedRoot,
            string relativePath,
            CanonicalMutationPublication publication,
            string authorityName)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_preparedForDirectoryMove ||
                _publishedRootAuthority == null)
            {
                throw new InvalidOperationException(
                    "Load-staging publication authority is unavailable.");
            }
            if (_sealedForCanonicalReads)
            {
                throw new InvalidOperationException(
                    "Load-staging publication authority is already sealed.");
            }
            if (publication.Operation != CanonicalMutationOperation.Write)
            {
                throw new InvalidDataException(
                    "Load-staging authority can rebind only a published write.");
            }

            var normalizedRelativePath = relativePath
                .Replace('\\', '/');
            var file = _files.SingleOrDefault(candidate =>
                candidate.RelativePath.Equals(
                    normalizedRelativePath,
                    StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException(
                    "Client-owned load repair targeted an unregistered file.");
            if (file.SealedStream != null)
            {
                throw new InvalidOperationException(
                    "Client-owned load repair authority was already rebound.");
            }
            if (!file.YieldedForConditionalReplacement ||
                file.Stream != null)
            {
                throw new InvalidOperationException(
                    "Client-owned load repair authority was not yielded for conditional replacement.");
            }

            var normalizedRoot = Path.GetFullPath(publishedRoot);
            var expectedPath = ResolveExpectedPath(
                normalizedRoot,
                file.RelativePath);
            var parentPath = Path.GetDirectoryName(expectedPath)
                ?? throw new InvalidDataException(
                    "Rebound load-staging file has no parent.");
            using var parentAuthority =
                PhysicalFileAuthority.EnsureStableDirectory(
                    normalizedRoot,
                    parentPath,
                    authorityName);
            FileStream? stream = null;
            try
            {
                stream = PhysicalFileAuthority.OpenReadFile(
                        parentAuthority,
                        expectedPath,
                        authorityName,
                        asynchronous: false)
                    ?? throw new InvalidDataException(
                        "Client-owned load repair publication is missing.");
                file.Authority =
                    PhysicalFileAuthority.EnsureExactOpenedFileAuthority(
                        stream.SafeFileHandle,
                        expectedPath,
                        publication.PhysicalIdentity,
                        publication.Sha256,
                        authorityName);
                file.SealedStream = stream;
                stream = null;
                file.YieldedForConditionalReplacement = false;
            }
            finally
            {
                stream?.Dispose();
            }
        }

        internal void SealPublishedAuthorityForCanonicalReads(
            string publishedRoot,
            string authorityName)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_preparedForDirectoryMove ||
                _publishedRootAuthority == null)
            {
                throw new InvalidOperationException(
                    "Load-staging publication authority is unavailable.");
            }
            if (_sealedForCanonicalReads)
            {
                throw new InvalidOperationException(
                    "Load-staging publication authority was already sealed.");
            }

            var normalizedRoot = Path.GetFullPath(publishedRoot);
            EnsurePublishedRoot(normalizedRoot, authorityName);
            EnsureExactFileNamespace(normalizedRoot, authorityName);
            RetainPublishedDirectoryAuthorities(
                normalizedRoot,
                authorityName);
            foreach (var file in _files)
            {
                var expectedPath = ResolveExpectedPath(
                    normalizedRoot,
                    file.RelativePath);
                if (file.SealedStream == null)
                {
                    var parentPath = Path.GetDirectoryName(expectedPath)
                        ?? throw new InvalidDataException(
                            "Sealed load-staging file has no parent.");
                    using var parentAuthority =
                        PhysicalFileAuthority.EnsureStableDirectory(
                            normalizedRoot,
                            parentPath,
                            authorityName);
                    FileStream? stream = null;
                    try
                    {
                        stream = PhysicalFileAuthority.OpenReadFile(
                                parentAuthority,
                                expectedPath,
                                authorityName,
                                asynchronous: false)
                            ?? throw new InvalidDataException(
                                $"{authorityName} '{file.RelativePath}' is missing.");
                        PhysicalFileAuthority.EnsureExactOpenedFileAuthority(
                            stream.SafeFileHandle,
                            expectedPath,
                            file.Authority.Identity,
                            file.Authority.Sha256,
                            $"{authorityName} '{file.RelativePath}'",
                            file.Authority.Length);
                        file.SealedStream = stream;
                        stream = null;
                    }
                    finally
                    {
                        stream?.Dispose();
                    }
                }
                else
                {
                    PhysicalFileAuthority.EnsureExactOpenedFileAuthority(
                        file.SealedStream.SafeFileHandle,
                        expectedPath,
                        file.Authority.Identity,
                        file.Authority.Sha256,
                        $"{authorityName} '{file.RelativePath}'",
                        file.Authority.Length);
                }
            }

            EnsureExactFileNamespace(normalizedRoot, authorityName);
            _sealedForCanonicalReads = true;
        }

        internal void EnsureSealedExactBeforeCommit(
            string publishedRoot,
            string authorityName)
        {
            if (!_sealedForCanonicalReads)
            {
                throw new InvalidOperationException(
                    "Load-staging publication authority is not sealed.");
            }

            EnsurePublishedState(
                publishedRoot,
                authorityName,
                useSealedAuthority: true);
            foreach (var directory in _publishedDirectories)
            {
                PhysicalFileAuthority.EnsureExactDirectoryIdentity(
                    directory.Authority,
                    directory.Identity,
                    authorityName);
            }
        }

        internal void ReleaseForRecovery()
        {
            if (_disposed)
                return;

            ReleaseRetainedAuthorities();
            _preparedForDirectoryMove = true;
        }

        private void EnsurePublishedState(
            string publishedRoot,
            string authorityName,
            bool useSealedAuthority)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_preparedForDirectoryMove ||
                _publishedRootAuthority == null)
            {
                throw new InvalidOperationException(
                    "Load-staging publication authority is unavailable.");
            }

            var normalizedRoot = Path.GetFullPath(publishedRoot);
            EnsurePublishedRoot(normalizedRoot, authorityName);
            EnsureExactFileNamespace(normalizedRoot, authorityName);
            EnsureRetainedFileAuthorities(
                normalizedRoot,
                authorityName,
                useSealedAuthority);
            EnsureExactFileNamespace(normalizedRoot, authorityName);
        }

        private void EnsureRetainedFileAuthorities(
            string normalizedRoot,
            string authorityName,
            bool useSealedAuthority)
        {
            foreach (var file in _files)
            {
                var stream = useSealedAuthority
                    ? file.SealedStream
                    : file.Stream;
                if (stream == null)
                {
                    throw new InvalidOperationException(
                        "Retained load-staging file authority is unavailable.");
                }

                var expectedPath = ResolveExpectedPath(
                    normalizedRoot,
                    file.RelativePath);
                PhysicalFileAuthority.EnsureExactOpenedFileAuthority(
                    stream.SafeFileHandle,
                    expectedPath,
                    file.Authority.Identity,
                    file.Authority.Sha256,
                    $"{authorityName} '{file.RelativePath}'",
                    file.Authority.Length);
            }
        }

        private static PhysicalFileAuthority.StableDirectory
            HandoffPublishedRootAuthority(
                PhysicalFileAuthority.StableDirectory renameRootAuthority,
                string normalizedRoot,
                PhysicalFileAuthority.FileIdentity expectedRootIdentity,
                string authorityName)
        {
            PhysicalFileAuthority.StableDirectory? transitionAuthority =
                null;
            PhysicalFileAuthority.StableDirectory? pinnedAuthority =
                null;
            try
            {
                transitionAuthority =
                    PhysicalFileAuthority.OpenStableDirectory(
                        normalizedRoot,
                        authorityName,
                        shareDelete: true);
                PhysicalFileAuthority.EnsureExactDirectoryIdentity(
                    transitionAuthority,
                    expectedRootIdentity,
                    authorityName);

                renameRootAuthority.Dispose();
                pinnedAuthority =
                    PhysicalFileAuthority.OpenStableDirectory(
                        normalizedRoot,
                        authorityName);
                PhysicalFileAuthority.EnsureExactDirectoryIdentity(
                    pinnedAuthority,
                    expectedRootIdentity,
                    authorityName);
                var result = pinnedAuthority;
                pinnedAuthority = null;
                return result;
            }
            finally
            {
                pinnedAuthority?.Dispose();
                transitionAuthority?.Dispose();
                renameRootAuthority.Dispose();
            }
        }

        private void EnsurePublishedRoot(
            string normalizedRoot,
            string authorityName)
        {
            var rootAuthority = _publishedRootAuthority
                ?? throw new InvalidOperationException(
                    "Published load-staging root handle is unavailable.");
            var rootIdentity = _publishedRootIdentity
                ?? throw new InvalidOperationException(
                    "Published load-staging root identity is unavailable.");
            if (!rootAuthority.FullPath.Equals(
                    normalizedRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Published load-staging root path changed.");
            }

            PhysicalFileAuthority.EnsureExactDirectoryIdentity(
                rootAuthority,
                rootIdentity,
                authorityName);
        }

        private void EnsureExactFileNamespace(
            string normalizedRoot,
            string authorityName)
        {
            if (IsReparsePoint(normalizedRoot))
            {
                throw new InvalidDataException(
                    $"{authorityName} root cannot be a reparse point.");
            }

            var actualFiles = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(normalizedRoot);
            var observedEntryCount = 0;
            var maximumEntryCount = checked(
                _relativePaths.Count +
                _allowedDirectoryPaths.Count);
            while (pendingDirectories.Count > 0)
            {
                var currentDirectory = pendingDirectories.Pop();
                foreach (var entry in Directory.EnumerateFileSystemEntries(
                             currentDirectory,
                             "*",
                             SearchOption.TopDirectoryOnly))
                {
                    observedEntryCount++;
                    if (observedEntryCount > maximumEntryCount)
                    {
                        throw new InvalidDataException(
                            $"{authorityName} namespace contains unregistered entries.");
                    }

                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new InvalidDataException(
                            $"{authorityName} namespace contains a reparse point.");
                    }

                    var relativePath = Path
                        .GetRelativePath(normalizedRoot, entry)
                        .Replace('\\', '/');
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        if (!_allowedDirectoryPaths.Contains(relativePath))
                        {
                            throw new InvalidDataException(
                                $"{authorityName} namespace contains an unregistered directory.");
                        }

                        pendingDirectories.Push(entry);
                        continue;
                    }

                    if (!_relativePaths.Contains(relativePath))
                    {
                        throw new InvalidDataException(
                            $"{authorityName} namespace contains an unregistered file.");
                    }

                    actualFiles.Add(relativePath);
                }
            }

            if (!actualFiles.SetEquals(_relativePaths))
            {
                throw new InvalidDataException(
                    $"{authorityName} namespace is missing a registered file.");
            }
        }

        private void RetainPublishedDirectoryAuthorities(
            string normalizedRoot,
            string authorityName)
        {
            foreach (var relativePath in _allowedDirectoryPaths
                         .OrderBy(path => path.Count(character =>
                             character == '/'))
                         .ThenBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var directoryPath = Path.GetFullPath(
                    Path.Combine(
                        normalizedRoot,
                        relativePath.Replace(
                            '/',
                            Path.DirectorySeparatorChar)));
                if (!Directory.Exists(directoryPath))
                    continue;

                var authority =
                    PhysicalFileAuthority.OpenStableDirectory(
                        directoryPath,
                        authorityName);
                try
                {
                    var identity =
                        PhysicalFileAuthority.CaptureFileIdentity(
                            authority.Handle
                            ?? throw new InvalidOperationException(
                                "Published directory handle is unavailable."),
                            authorityName);
                    if (!identity.IsDirectory)
                    {
                        throw new InvalidDataException(
                            $"{authorityName} namespace parent is not a directory.");
                    }

                    _publishedDirectories.Add(
                        new LoadStagingDirectoryAuthority(
                            authority,
                            identity));
                    authority = null!;
                }
                finally
                {
                    authority?.Dispose();
                }
            }
        }

        private void AddAllowedDirectoryPath(string? relativePath)
        {
            while (!string.IsNullOrWhiteSpace(relativePath) &&
                   relativePath != ".")
            {
                var normalized = relativePath.Replace('\\', '/');
                _allowedDirectoryPaths.Add(normalized);
                relativePath = Path.GetDirectoryName(
                    normalized.Replace(
                        '/',
                        Path.DirectorySeparatorChar));
            }
        }

        private static string ResolveExpectedPath(
            string normalizedRoot,
            string relativePath)
        {
            var expectedPath = Path.GetFullPath(
                Path.Combine(
                    normalizedRoot,
                    relativePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
            if (!IsSameOrDescendant(expectedPath, normalizedRoot) ||
                string.Equals(
                    expectedPath,
                    normalizedRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Load staging authority path escaped its current root.");
            }

            return expectedPath;
        }

        private void ReleaseFileStreams(
            bool includePublished,
            bool includeSealed)
        {
            List<Exception>? failures = null;
            foreach (var file in _files)
            {
                if (includePublished && file.Stream != null)
                {
                    try
                    {
                        file.Stream.Dispose();
                        file.Stream = null;
                    }
                    catch (Exception ex)
                    {
                        failures ??= [];
                        failures.Add(ex);
                    }
                }

                if (includeSealed && file.SealedStream != null)
                {
                    try
                    {
                        file.SealedStream.Dispose();
                        file.SealedStream = null;
                    }
                    catch (Exception ex)
                    {
                        failures ??= [];
                        failures.Add(ex);
                    }
                }
            }

            if (failures is { Count: > 0 })
            {
                throw new AggregateException(
                    "Could not release every retained load-staging handle.",
                    failures);
            }
        }

        private void ReleaseRetainedAuthorities()
        {
            List<Exception>? failures = null;
            try
            {
                ReleaseFileStreams(
                    includePublished: true,
                    includeSealed: true);
            }
            catch (Exception ex)
            {
                failures ??= [];
                failures.Add(ex);
            }

            foreach (var directory in _publishedDirectories)
            {
                try
                {
                    directory.Authority.Dispose();
                }
                catch (Exception ex)
                {
                    failures ??= [];
                    failures.Add(ex);
                }
            }
            _publishedDirectories.Clear();

            if (_publishedRootAuthority != null)
            {
                try
                {
                    _publishedRootAuthority.Dispose();
                    _publishedRootAuthority = null;
                }
                catch (Exception ex)
                {
                    failures ??= [];
                    failures.Add(ex);
                }
            }

            if (failures is { Count: > 0 })
            {
                throw new AggregateException(
                    "Could not release every retained load-staging authority.",
                    failures);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;
            List<Exception>? failures = null;
            foreach (var file in _files)
            {
                if (file.Stream != null)
                {
                    try
                    {
                        await file.Stream.DisposeAsync();
                        file.Stream = null;
                    }
                    catch (Exception ex)
                    {
                        failures ??= [];
                        failures.Add(ex);
                    }
                }

                if (file.SealedStream != null)
                {
                    try
                    {
                        await file.SealedStream.DisposeAsync();
                        file.SealedStream = null;
                    }
                    catch (Exception ex)
                    {
                        failures ??= [];
                        failures.Add(ex);
                    }
                }
            }

            foreach (var directory in _publishedDirectories)
            {
                try
                {
                    directory.Authority.Dispose();
                }
                catch (Exception ex)
                {
                    failures ??= [];
                    failures.Add(ex);
                }
            }
            _publishedDirectories.Clear();

            if (_publishedRootAuthority != null)
            {
                try
                {
                    _publishedRootAuthority.Dispose();
                    _publishedRootAuthority = null;
                }
                catch (Exception ex)
                {
                    failures ??= [];
                    failures.Add(ex);
                }
            }

            _files.Clear();
            _relativePaths.Clear();
            _allowedDirectoryPaths.Clear();
            if (failures is { Count: > 0 })
            {
                throw new AggregateException(
                    "Could not release every retained load-staging authority.",
                    failures);
            }
        }
    }

    private sealed class LoadStagingFileAuthority
    {
        internal LoadStagingFileAuthority(
            string relativePath,
            FileStream stream,
            PhysicalFileAuthority.OpenedFileAuthority authority)
        {
            RelativePath = relativePath;
            Stream = stream;
            Authority = authority;
        }

        internal string RelativePath { get; }
        internal FileStream? Stream { get; set; }
        internal FileStream? SealedStream { get; set; }
        internal PhysicalFileAuthority.OpenedFileAuthority Authority { get; set; }
        internal string? GuardPath { get; set; }
        internal bool YieldedForConditionalReplacement { get; set; }
    }

    private sealed record LoadStagingDirectoryAuthority(
        PhysicalFileAuthority.StableDirectory Authority,
        PhysicalFileAuthority.FileIdentity Identity);

    /// <summary>
    /// Deletes a canonical member only while the supplied physical identity and digest remain current.
    /// </summary>
    /// <param name="writeLease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <param name="relativePath">
    /// Canonical session-relative member or subtree to operate on.
    /// </param>
    /// <param name="expectedCurrentIdentity">
    /// Physical identity that must still belong to the current destination.
    /// </param>
    /// <param name="expectedCurrentSha256">
    /// SHA-256 digest that must still match the current destination.
    /// </param>
    /// <returns>
    /// Completion after the operation and its required cleanup finish.
    /// </returns>
    internal async Task DeleteFileIfCurrentAuthorityAsync(
        CanonicalWriteLease writeLease,
        string relativePath,
        PhysicalFileAuthority.FileIdentity expectedCurrentIdentity,
        string expectedCurrentSha256)
    {
        using var validationMutation = BeginValidationMutation(writeLease);
        EnsureWorkerGeneralMutationAllowed(writeLease);
        writeLease.EnsureNoPendingLocalDecision();
        ArgumentNullException.ThrowIfNull(expectedCurrentIdentity);
        var expectedHash = NormalizeOwnedMutationHashes(
                [expectedCurrentSha256])
            .Single();
        var fullPath = ResolvePath(relativePath);
        using var mutationRegistration =
            new InProcessMutationRegistration(fullPath);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await InvokeBeforeCanonicalMutationBoundaryAsync(
                    relativePath);
                using var parentAuthority = EnsureStableCanonicalParent(
                    relativePath,
                    fullPath);
                EnsureCanonicalMutationBoundary(relativePath, fullPath);
                var entry = PhysicalFileAuthority.ProbeNamespaceEntry(
                    parentAuthority,
                    fullPath,
                    "Identity-bound canonical deletion");
                if (entry !=
                    PhysicalFileAuthority.NamespaceEntryKind.RegularFile)
                {
                    throw new InvalidDataException(
                        "Identity-bound canonical deletion target is not the expected physical regular file.");
                }

                using var targetHandle =
                    PhysicalFileAuthority.OpenForRename(
                        parentAuthority,
                        fullPath,
                        isDirectory: false,
                        "Identity-bound canonical deletion",
                        denyConcurrentWrites: true);
                PhysicalFileAuthority.EnsureExactOpenedFileAuthority(
                    targetHandle,
                    fullPath,
                    expectedCurrentIdentity,
                    expectedHash,
                    "Identity-bound canonical deletion target");
                await InvokeAfterCanonicalMutationBoundaryValidatedAsync(
                    relativePath);
                PhysicalFileAuthority.EnsureExactOpenedFileAuthority(
                    targetHandle,
                    fullPath,
                    expectedCurrentIdentity,
                    expectedHash,
                    "Identity-bound canonical deletion final authority");
                PhysicalFileAuthority.DeleteOpenedFile(
                    targetHandle,
                    "Identity-bound canonical deletion target");
                return;
            }
            catch (Exception ex) when (
                IsTransientFileAccessException(ex) &&
                attempt < TransientFileAccessRetryCount)
            {
                await Task.Delay(TransientFileAccessRetryDelay);
            }
        }
    }

    private async Task DeleteFileWithLockAsync(string relativePath)
    {
        var writeLock = await AcquireCanonicalWriteLeaseAsync();
        Exception? publicationUncertainty = null;
        try { DeleteFile(writeLock, relativePath); }
        catch (CoordinatedStatePublicationUncertainException failure)
        {
            publicationUncertainty = failure;
            throw;
        }
        finally
        {
            await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(
                this, writeLock, completed: false, operationFailure: publicationUncertainty);
        }
    }

    private static async Task RecordCanonicalMutationIntentAsync(
        CanonicalWriteLease writeLease,
        string relativePath,
        byte[]? desiredContent)
    {
        writeLease.EnsureNoPendingLocalDecision();
        if (writeLease.MutationIntentRecorder == null)
            return;

        await writeLease.MutationIntentRecorder.RecordMutationIntentAsync(
            relativePath,
            desiredContent);
    }

    private static async Task RecordCanonicalMutationNonPublicationAsync(
        CanonicalWriteLease writeLease,
        string relativePath)
    {
        if (writeLease.MutationIntentRecorder == null)
            return;

        await writeLease.MutationIntentRecorder
            .RecordMutationNonPublicationAsync(relativePath);
    }

    private static async Task RecordCanonicalMutationPublicationAsync(
        CanonicalWriteLease writeLease,
        string relativePath,
        CanonicalMutationPublication publication)
    {
        if (writeLease.MutationIntentRecorder == null)
            return;

        await writeLease.MutationIntentRecorder
            .RecordMutationPublicationAsync(
                relativePath,
                publication);
    }

    private static HashSet<string> NormalizeOwnedMutationHashes(
        IReadOnlyCollection<string> hashes)
    {
        ArgumentNullException.ThrowIfNull(hashes);
        var normalized = hashes
            .Where(static hash =>
                hash is { Length: 64 } &&
                hash.All(static character =>
                    character is >= '0' and <= '9' or
                        >= 'a' and <= 'f' or
                        >= 'A' and <= 'F'))
            .Select(static hash => hash.ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);
        if (normalized.Count != hashes.Count || normalized.Count == 0)
        {
            throw new InvalidDataException(
                "Conditional canonical mutation requires unique SHA-256 authorities.");
        }

        return normalized;
    }

    /// <summary>
    /// Performs legacy file deletion under the current physical admission and optionally returns publication evidence.
    /// </summary>
    /// <param name="relativePath">
    /// Canonical session-relative member or subtree to operate on.
    /// </param>
    /// <param name="capturePublication">
    /// Whether successful deletion should return its original physical publication evidence.
    /// </param>
    /// <returns>
    /// Deletion evidence when requested and produced; otherwise <see langword="null"/>.
    /// </returns>
    private CanonicalMutationPublication? DeleteFileCore(
        string relativePath,
        bool capturePublication = false)
    {
        using var validationMutation = BeginValidationMutation();
        var fullPath = ResolvePath(relativePath);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                InvokeBeforeCanonicalMutationBoundaryAsync(relativePath)
                    .GetAwaiter()
                    .GetResult();
                using var parentAuthority = EnsureStableCanonicalParent(
                    relativePath,
                    fullPath);
                EnsureCanonicalMutationBoundary(relativePath, fullPath);
                if (!capturePublication)
                {
                    InvokeAfterCanonicalMutationBoundaryValidatedAsync(
                            relativePath)
                        .GetAwaiter()
                        .GetResult();
                    PhysicalFileAuthority.TryDeleteFile(
                        parentAuthority,
                        fullPath,
                        "Canonical file deletion");
                    return null;
                }

                var entry = PhysicalFileAuthority.ProbeNamespaceEntry(
                    parentAuthority,
                    fullPath,
                    "Canonical file deletion receipt");
                if (entry ==
                    PhysicalFileAuthority.NamespaceEntryKind.Missing)
                {
                    InvokeAfterCanonicalMutationBoundaryValidatedAsync(
                            relativePath)
                        .GetAwaiter()
                        .GetResult();
                    return null;
                }
                if (entry !=
                    PhysicalFileAuthority.NamespaceEntryKind.RegularFile)
                {
                    throw new InvalidDataException(
                        "Canonical deletion receipt requires one physical regular file.");
                }

                using var targetHandle =
                    PhysicalFileAuthority.OpenForRename(
                        parentAuthority,
                        fullPath,
                        isDirectory: false,
                        "Canonical file deletion receipt",
                        denyConcurrentWrites: true);
                var deletedAuthority =
                    PhysicalFileAuthority.CaptureOpenedFileAuthority(
                        targetHandle,
                        fullPath,
                        "Canonical file deletion receipt");
                InvokeAfterCanonicalMutationBoundaryValidatedAsync(relativePath)
                    .GetAwaiter()
                    .GetResult();
                PhysicalFileAuthority.EnsureExactOpenedFileAuthority(
                    targetHandle,
                    fullPath,
                    deletedAuthority.Identity,
                    deletedAuthority.Sha256,
                    "Canonical file deletion final authority");
                PhysicalFileAuthority.DeleteOpenedFile(
                    targetHandle,
                    "Canonical file deletion receipt");
                return new CanonicalMutationPublication(
                    CanonicalMutationOperation.Delete,
                    deletedAuthority.Identity,
                    deletedAuthority.Sha256);
            }
            catch (Exception ex) when (IsTransientFileAccessException(ex) && attempt < TransientFileAccessRetryCount)
            {
                Thread.Sleep(TransientFileAccessRetryDelay);
            }
        }
    }

    internal Task<CanonicalWriteLease> AcquireCanonicalWriteLeaseAsync(
        CanonicalWritePurpose purpose = CanonicalWritePurpose.SessionMutation,
        CancellationToken cancellationToken = default,
        GmWorkerCanonicalPurpose? workerPurpose = null)
    {
        if (purpose == CanonicalWritePurpose.SessionReplacement)
        {
            throw new InvalidOperationException(
                "Session replacement writes require an active session lifecycle lease.");
        }

        return AcquireCanonicalWriteLeaseWithAmbientAsync(
            purpose,
            cancellationToken, workerPurpose);
    }

    internal Task<CanonicalWriteLease> AcquireSessionReplacementWriteLeaseAsync(
        SessionLifecycleLease lifecycleLease,
        CancellationToken cancellationToken = default)
    {
        EnsureValidSessionLifecycleLease(lifecycleLease);
        return AcquireCanonicalWriteLeaseWithAmbientAsync(
            CanonicalWritePurpose.SessionReplacement,
            cancellationToken);
    }

    private Task<CanonicalWriteLease>
        AcquirePublicationReadQuiescenceLeaseAsync(
            CancellationToken cancellationToken = default) =>
        AcquireCanonicalWriteLeaseWithAmbientAsync(
            CanonicalWritePurpose.PublicationReadQuiescence,
            cancellationToken);

    /// <summary>
    /// Installs ambient registration in the caller before starting the existing canonical lease acquisition.
    /// </summary>
    /// <param name="purpose">
    /// Existing canonical purpose; a read purpose alone does not enforce mutation exclusion.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation for main admission and physical lock acquisition.
    /// </param>
    /// <param name="workerPurpose">
    /// Optional existing worker authority.
    /// </param>
    /// <param name="worldIssuance">
    /// Exact read reservation issuing ordinary recovery, or null for ordinary acquisition.
    /// </param>
    /// <returns>
    /// The original acquisition task, whose failure releases only its own registrations.
    /// </returns>
    private Task<CanonicalWriteLease>
        AcquireCanonicalWriteLeaseWithAmbientAsync(
            CanonicalWritePurpose purpose,
            CancellationToken cancellationToken, GmWorkerCanonicalPurpose? workerPurpose = null, ValidationReadReservation? worldIssuance = null)
    {
        var mainAdmission=BeginMainAdmission(false, worldIssuance, physicalScope: true);
        var registration = new AmbientCanonicalLeaseRegistration(
            CompactAmbientCanonicalLeaseHead());
        _ambientCanonicalLease.Value = registration;
        return CompleteCanonicalWriteLeaseAcquisitionAsync(
            registration,
            purpose,
            cancellationToken, workerPurpose, mainAdmission);
    }

    private async Task<CanonicalWriteLease>
        CompleteCanonicalWriteLeaseAcquisitionAsync(
            AmbientCanonicalLeaseRegistration registration,
            CanonicalWritePurpose purpose,
            CancellationToken cancellationToken, GmWorkerCanonicalPurpose? workerPurpose, MainAdmission mainAdmission)
    {
        try
        {
            await mainAdmission.AcquireAsync(cancellationToken,purpose==CanonicalWritePurpose.SessionFinalization,workerPurpose:workerPurpose);
            var writeLease = await AcquireCanonicalWriteLeaseCoreAsync(
                purpose,
                cancellationToken, workerPurpose);
            writeLease.MainAdmission=mainAdmission;
            registration.Activate();
            writeLease.AmbientRegistration = registration;
            if (ValidationReuseSuppression.Value == 0) _sourceDataOperation.Value?.Observe(writeLease);
            return writeLease;
        }
        catch
        {
            mainAdmission.Dispose();
            ReleaseAmbientCanonicalLease(registration);
            throw;
        }
    }

    /// <summary>
    /// Acquires the original physical write lease and completes its existing storage recovery before returning.
    /// </summary>
    /// <param name="purpose">
    /// The existing canonical write purpose governing the original admission path.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation observed by the existing physical acquisition and recovery operations.
    /// </param>
    /// <param name="workerPurpose">
    /// Actual worker purpose when acquiring for a worker, or <see langword="null"/> for ordinary work.
    /// </param>
    /// <returns>
    /// The acquired original lease; acquisition callbacks cannot cross an active recovery reuse boundary.
    /// </returns>
    private async Task<CanonicalWriteLease> AcquireCanonicalWriteLeaseCoreAsync(
        CanonicalWritePurpose purpose,
        CancellationToken cancellationToken, GmWorkerCanonicalPurpose? workerPurpose = null)
    {
        EnsureCanonicalSessionRootIsNotReparsePoint();
        var lockPath = CanonicalWriteLockPath;
        var lockParentPath = Path.GetDirectoryName(lockPath)
            ?? throw new InvalidDataException(
                "Canonical write lock has no parent directory.");
        EnsureRuntimeDirectoryExistsAndIsSafe(lockParentPath);
        for (var attempt = 0; attempt < CanonicalWriteLockRetryCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureRuntimePathIsSafe(lockPath);
            if (_hooks?.BeforeCanonicalWriteLockOpenAsync != null)
                await _hooks.BeforeCanonicalWriteLockOpenAsync();
            var parentAuthority = EnsureStableRuntimeDirectory(lockParentPath);
            FileStream stream;
            try
            {
                stream = new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.Asynchronous);
            }
            catch (IOException) when (attempt < CanonicalWriteLockRetryCount - 1)
            {
                parentAuthority.Dispose();
#if DEBUG
                _browserAdmissionDiagnostic?.Contended();
#endif
                if (_hooks?.CanonicalWriteLockContendedAsync != null)
                    await InvokeRecoveryValidationCallbackAsync(_hooks.CanonicalWriteLockContendedAsync);
                await Task.Delay(TransientFileAccessRetryDelay, cancellationToken);
                continue;
            }
            catch
            {
                parentAuthority.Dispose();
                throw;
            }

            try
            {
                if (_hooks?.AfterCanonicalWriteLockOpenedAsync != null)
                    await InvokeRecoveryValidationCallbackAsync(_hooks.AfterCanonicalWriteLockOpenedAsync);
                EnsureRuntimePathIsSafe(lockPath);
                PhysicalFileAuthority.EnsureHandleMatchesExpectedPath(
                    stream.SafeFileHandle,
                    lockPath,
                    "Canonical write lock");
            }
            catch
            {
                await stream.DisposeAsync();
                parentAuthority.Dispose();
                throw;
            }

            var writeLease = new CanonicalWriteLease(
                this,
                stream,
                parentAuthority,
                purpose) { WorkerPurpose = workerPurpose, MainAdmission=MainAdmissions.Value };
#if DEBUG
            if (_browserAdmissionDiagnostic != null) writeLease.BrowserDiagnosticId = _browserAdmissionDiagnostic.LeaseOpened();
#endif
            try
            {
                if(purpose==CanonicalWritePurpose.MainMetadata) {
                    if(workerPurpose!=null)throw GmSessionRunPersistence.Invalid();
                    if(writeLease.MainAdmission?.MetadataOnly!=true)throw GmSessionRunPersistence.Invalid();
                    writeLease.MainAdmission.Validate(writeLease);return writeLease;
                }
                workerPurpose?.ValidateRoot(this);
                var workerContext = CanonicalRootAuthorityIdentity.WorkerContext;
                workerContext?.ValidateCanonical(this, writeLease);
                workerContext?.ValidateBeforeRecovery();
                writeLease.WorkerRootPin = workerContext?.PinCanonical();
                if(purpose==CanonicalWritePurpose.SessionFinalization) {
                    // Closing verifies the current generation without replaying
                    // publication recovery or granting mutation authority.
                    writeLease.MainAdmission!.Validate(writeLease);return writeLease;
                }
                EnsureMainBeforeRecovery(writeLease, "acquired-lock-before-recovery");
                // An explicitly bound reader is fenced before any recovery or
                // evidence classification, including quiescent local admission.
                EnsureBoundSessionOperationCanWrite(writeLease);
                // Classify the entire browser namespace before either recovery owner
                // can mutate anything. Structural direct-gacha backups are neutral.
                var browserProtocol = ExplorerLocalTurnRollbackArtifacts.ClassifyBrowserStorageEvidence(this, writeLease);
                if (browserProtocol == ExplorerLocalTurnRollbackArtifacts.BrowserStorageProtocol.Original &&
                    HasStorageEvidence(LocalPublicationRoot))
                    throw new InvalidDataException("Mixed original browser and current publication evidence; bytes retained.");
                if (OperatingSystem.IsLinux() || browserProtocol == ExplorerLocalTurnRollbackArtifacts.BrowserStorageProtocol.Current)
                {
                    EnsureNoLegacyStorageEvidence(allowPortableBrowser: true);
                    ExplorerLocalTurnRollbackArtifacts.PreflightLocalBrowserEvidence(this, writeLease);
                    try
                    {
                        // A pending member/manifest publication must settle before browser rollback.
                        RecoverTrustedLocalStorage(writeLease);
                        if (purpose is CanonicalWritePurpose.SessionMutation or CanonicalWritePurpose.SessionReplacement)
                            await ExplorerLocalTurnRollbackArtifacts.RecoverLocalBrowserEvidenceAsync(this, writeLease);
                    }
                    catch (Exception failure) when (purpose == CanonicalWritePurpose.SessionReplacement)
                    {
                        throw new CoordinatedStatePublicationUncertainException(failure);
                    }
                    EnsureNoLegacyStorageEvidence(writeLease);
                }
                else
                {
                if (!OperatingSystem.IsWindows()) EnsureNoLegacyStorageEvidence();
                await RunLegacyStorageRecoveryAsync(writeLease, async () =>
                {
                    RecoverInterruptedFilePublications();
                    if (purpose != CanonicalWritePurpose.PublicationReadQuiescence)
                    {
                        RecoverInterruptedLoadTransaction(writeLease);
                        await RecoverInterruptedWorkerApplyTransactionAsync(writeLease);
                        if (purpose is CanonicalWritePurpose.SessionMutation or CanonicalWritePurpose.SessionReplacement)
                            await ExplorerLocalTurnRollbackArtifacts.RecoverInterruptedBrowserWriteTransactionsAsync(this, writeLease);
                    }
                });
                // Original handlers may have changed the namespace/generation.
                // Reclassify now: only neutral before-images and authenticated
                // current scratch may remain before their original publisher runs.
                if (ExplorerLocalTurnRollbackArtifacts.ClassifyBrowserStorageEvidence(this, writeLease) !=
                    ExplorerLocalTurnRollbackArtifacts.BrowserStorageProtocol.None)
                    throw new InvalidDataException("Browser protocol evidence remains after original recovery; bytes retained.");
                EnsureNoLegacyStorageEvidence(allowPortableBrowser: true);
                try
                {
                    RecoverTrustedLocalStorage(writeLease);
                    EnsureNoLegacyStorageEvidence(writeLease);
                }
                catch (Exception failure) when (purpose == CanonicalWritePurpose.SessionReplacement)
                {
                    // Recovery reached retained decision evidence; an admission refusal is not a safe retry.
                    throw new CoordinatedStatePublicationUncertainException(failure);
                }
                }
                // Permitted unrelated recovery cannot leave a different browser
                // tuple or main condition behind and still return a mutable lease.
                EnsureMainBeforeRecovery(writeLease, "acquired-lock-after-recovery");
                AdmitConsoleRollbackEvidence(writeLease);
                if (purpose == CanonicalWritePurpose.SessionMutation)
                    EnsureBoundSessionOperationCanWrite(writeLease);
                return writeLease;
            }
            catch (CoordinatedStatePublicationUncertainException failure)
            {
                try
                {
                    await writeLease.DisposeAsync();
                }
                catch (Exception cleanup)
                {
                    throw new CoordinatedStatePublicationUncertainException(new AggregateException(failure, cleanup));
                }
                throw;
            }
            catch
            {
                await writeLease.DisposeAsync();
                throw;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw new IOException("Timed out waiting for the canonical game-session write lock.");
    }

    private void EnsureBoundSessionOperationCanWrite(CanonicalWriteLease writeLease)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        if (!SessionOperationContext.TryGetExpectedGeneration(_basePath, out var expectedGeneration))
            return;

        var actualGeneration = ReadLocalGenerationSnapshot(writeLease).Binding.Id;
        if (string.Equals(expectedGeneration, actualGeneration, StringComparison.Ordinal))
            return;

        throw SessionOperationContext.MarkReplaced(
            _basePath,
            actualGeneration,
            "The game session was replaced while an older operation was still running.");
    }

    internal async Task VerifyCurrentSessionOperationAsync()
    {
        if (!SessionOperationContext.TryGetExpectedGeneration(_basePath, out _))
            return;

        await using var writeLease = await AcquireCanonicalWriteLeaseAsync();
    }

    internal void VerifyCurrentSessionOperation(CanonicalWriteLease writeLease)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        EnsureBoundSessionOperationCanWrite(writeLease);
    }

    internal Task InvokeSessionOperationClosingHookAsync() =>
        _hooks?.SessionOperationClosingAsync?.Invoke() ?? Task.CompletedTask;

    internal Task<SessionLifecycleLease> AcquireSessionLifecycleLeaseAsync()
    {
        var main=BeginMainAdmission(false, physicalScope: true);
        return AcquireMainLifecycleAsync(main);
    }

    private async Task<SessionLifecycleLease> AcquireMainLifecycleAsync(MainAdmission main)
    {
        try { await main.AcquireAsync();return await AcquireSessionLifecycleLeaseCoreAsync(main); }
        catch { main.Dispose();throw; }
    }

    private async Task<SessionLifecycleLease> AcquireSessionLifecycleLeaseCoreAsync(MainAdmission main)
    {
        EnsureCanonicalSessionRootIsNotReparsePoint();
        var lockPath = SessionLifecycleLockPath;
        var lockParentPath = Path.GetDirectoryName(lockPath)
            ?? throw new InvalidDataException(
                "Session lifecycle lock has no parent directory.");
        EnsureRuntimeDirectoryExistsAndIsSafe(lockParentPath);
        for (var attempt = 0; attempt < SessionLifecycleLockRetryCount; attempt++)
        {
            EnsureRuntimePathIsSafe(lockPath);
            if (_hooks?.BeforeSessionLifecycleLockOpenAsync != null)
                await _hooks.BeforeSessionLifecycleLockOpenAsync();
            var parentAuthority = EnsureStableRuntimeDirectory(lockParentPath);
            FileStream stream;
            try
            {
                stream = new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.Asynchronous);
            }
            catch (IOException) when (attempt < SessionLifecycleLockRetryCount - 1)
            {
                parentAuthority.Dispose();
                if (_hooks?.SessionLifecycleLockContendedAsync != null)
                    await _hooks.SessionLifecycleLockContendedAsync();
                await Task.Delay(TransientFileAccessRetryDelay);
                continue;
            }
            catch
            {
                parentAuthority.Dispose();
                throw;
            }

            try
            {
                if (_hooks?.AfterSessionLifecycleLockOpenedAsync != null)
                    await _hooks.AfterSessionLifecycleLockOpenedAsync();
                EnsureRuntimePathIsSafe(lockPath);
                PhysicalFileAuthority.EnsureHandleMatchesExpectedPath(
                    stream.SafeFileHandle,
                    lockPath,
                    "Session lifecycle lock");
                return new SessionLifecycleLease(
                    this,
                    stream,
                    parentAuthority) { MainAdmission=main };
            }
            catch
            {
                await stream.DisposeAsync();
                parentAuthority.Dispose();
                throw;
            }
        }

        throw new IOException("Timed out waiting for the game-session lifecycle lock.");
    }

    internal async Task<SessionLifecycleLease?> TryAcquireSessionLifecycleLeaseAsync(
        string expectedSessionGeneration)
    {
        var lifecycleLease = await AcquireSessionLifecycleLeaseAsync();
        try
        {
            await using var writeLease = await AcquireCanonicalWriteLeaseAsync();
            if (!IsCurrentSessionGeneration(writeLease, expectedSessionGeneration))
            {
                await lifecycleLease.DisposeAsync();
                return null;
            }

            return lifecycleLease;
        }
        catch
        {
            await lifecycleLease.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Revalidates the supplied physical lease and its existing main and worker admission in the original order.
    /// </summary>
    /// <param name="writeLease">
    /// The original caller-owned lease; inactive or foreign leases are refused before later checks.
    /// </param>
    private void EnsureValidCanonicalWriteLease(CanonicalWriteLease writeLease)
    {
        using var operationContext = EnterOperationTimingContext();
        using var operationTiming = MeasureOperation("Lease.Validate.Total");
        using (MeasureOperation("Lease.Validate.Physical"))
            EnsurePhysicalCanonicalWriteLease(writeLease);
        if (writeLease.MainAdmission is { } mainAdmission)
            using (MeasureOperation("Lease.Validate.MainAdmission"))
                mainAdmission.Validate(writeLease);
        if(writeLease.Purpose==CanonicalWritePurpose.MainMetadata)return;
        if (writeLease.WorkerPurpose is { } workerPurpose)
            using (MeasureOperation("Lease.Validate.WorkerPurpose"))
                workerPurpose.ValidateRoot(this);
        if (CanonicalRootAuthorityIdentity.WorkerContext is { } workerContext)
            using (MeasureOperation("Lease.Validate.WorkerContext"))
                workerContext.ValidateCanonical(this, writeLease);
    }

    private void EnsurePhysicalCanonicalWriteLease(CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        if (!ReferenceEquals(writeLease.Owner, this) || !writeLease.IsActive)
            throw new InvalidOperationException("Canonical write lease is not active for this game session.");
    }

    internal void EnsureWorkerRecoveryAdmission(CanonicalWriteLease writeLease, string diagnosticOrigin = "worker-other")
    {
        EnsureValidCanonicalWriteLease(writeLease);
        CanonicalRootAuthorityIdentity.WorkerContext?.ValidateBeforeRecovery();
        EnsureMainBeforeRecovery(writeLease, diagnosticOrigin);
    }

    internal void EnsureCanonicalWriteLeaseActive(CanonicalWriteLease writeLease)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        if (ValidationReuseSuppression.Value == 0) _sourceDataOperation.Value?.Observe(writeLease);
    }

    private void EnsureValidSessionReplacementLease(CanonicalWriteLease writeLease)
    {
        EnsureWorkerGeneralMutationAllowed(writeLease);
        if (writeLease.Purpose != CanonicalWritePurpose.SessionReplacement)
        {
            throw new InvalidOperationException(
                "This operation requires a session replacement write lease.");
        }
    }

    private void EnsureValidSessionLifecycleLease(SessionLifecycleLease lifecycleLease)
    {
        ArgumentNullException.ThrowIfNull(lifecycleLease);
        if (!ReferenceEquals(lifecycleLease.Owner, this) || !lifecycleLease.IsActive)
            throw new InvalidOperationException("Session lifecycle lease is not active for this game session.");
    }

    private static bool ExactBytesEqual(byte[]? left, byte[]? right) =>
        left == null ? right == null : right != null && left.AsSpan().SequenceEqual(right);

    private bool SupportsReversibleFileReplacement =>
        OperatingSystem.IsWindows() &&
        (_hooks?.SupportsReversibleFileReplacementOverride ?? true);

    internal bool SupportsReversibleOpenedHandlePublication =>
        SupportsReversibleFileReplacement;

    private bool SupportsDescriptorBoundCreateOnlyPublication =>
        OperatingSystem.IsWindows() &&
        (_hooks?.SupportsDescriptorBoundCreateOnlyPublicationOverride ?? true);

    internal void EnsureAuthorityFilePublicationSupported(
        PhysicalFileAuthority.NamespaceEntryKind destinationEntry,
        string authorityName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorityName);
        switch (destinationEntry)
        {
            case PhysicalFileAuthority.NamespaceEntryKind.Missing:
                EnsureDescriptorBoundCreateOnlyPublicationSupported(
                    authorityName + " create-only publication");
                return;
            case PhysicalFileAuthority.NamespaceEntryKind.RegularFile:
                if (!SupportsReversibleFileReplacement)
                {
                    throw new PlatformNotSupportedException(
                        $"{authorityName} overwrite requires a reversible opened-handle replacement backend.");
                }

                return;
            case PhysicalFileAuthority.NamespaceEntryKind.Directory:
                throw new InvalidDataException(
                    $"{authorityName} destination is a directory.");
            case PhysicalFileAuthority.NamespaceEntryKind.ReparsePoint:
                throw new InvalidDataException(
                    $"{authorityName} destination is a reparse point.");
            default:
                throw new InvalidDataException(
                    $"{authorityName} destination has an unknown namespace kind.");
        }
    }

    private void EnsureDescriptorBoundCreateOnlyPublicationSupported(
        string authorityName)
    {
        if (!SupportsDescriptorBoundCreateOnlyPublication)
        {
            throw new PlatformNotSupportedException(
                $"{authorityName} requires a descriptor-bound relative publication backend.");
        }
    }

    private void RecoverInterruptedFilePublications() =>
        ReversibleFilePublication.RecoverPending(
            _basePath,
            PhysicalPublicationTransactionsRootPath);

    /// <summary>
    /// Checks the existing local and physical publication storage for recovery evidence.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when either root contains entries; otherwise, <see langword="false"/> when both are missing or empty.
    /// </returns>
    private bool HasPendingFilePublications()
    {
        using var t351Pending = MeasureOperation("ReadPublication.HasPending");
        return HasStorageEvidence(LocalPublicationRoot) || HasStorageEvidence(PhysicalPublicationTransactionsRootPath);
    }

    private static InProcessMutationState AcquireInProcessMutationState(
        string fullPath)
    {
        var normalizedPath = Path.GetFullPath(fullPath);
        while (true)
        {
            var state = CanonicalMutationStates.GetOrAdd(
                normalizedPath,
                static _ => new InProcessMutationState());
            lock (state)
            {
                if (CanonicalMutationStates.TryGetValue(
                        normalizedPath,
                        out var current) &&
                    ReferenceEquals(current, state))
                {
                    state.ParticipantCount =
                        checked(state.ParticipantCount + 1);
                    return state;
                }
            }
        }
    }

    private static void ReleaseInProcessMutationState(
        string fullPath,
        InProcessMutationState state)
    {
        var normalizedPath = Path.GetFullPath(fullPath);
        lock (state)
        {
            state.ParticipantCount--;
            if (state.ParticipantCount < 0)
            {
                throw new InvalidOperationException(
                    "Canonical mutation state participant count became negative.");
            }

            if (state.ParticipantCount == 0 &&
                state.ActiveMutationCount == 0)
            {
                CanonicalMutationStates.TryRemove(
                    new KeyValuePair<string, InProcessMutationState>(
                        normalizedPath,
                        state));
            }
        }
    }

    private AmbientCanonicalLeaseRegistration?
        CompactAmbientCanonicalLeaseHead()
    {
        var current = _ambientCanonicalLease.Value;
        while (current != null && current.Inactive)
            current = current.Previous;

        var cursor = current;
        while (cursor != null)
        {
            cursor.PruneInactivePredecessors();
            cursor = cursor.Previous;
        }

        if (!ReferenceEquals(current, _ambientCanonicalLease.Value))
            _ambientCanonicalLease.Value = current;
        return current;
    }

    private bool HasAmbientCanonicalLease()
    {
        var current = CompactAmbientCanonicalLeaseHead();
        while (current != null)
        {
            if (current.Active)
                return true;
            current = current.Previous;
        }

        return false;
    }

    private void ReleaseAmbientCanonicalLease(
        AmbientCanonicalLeaseRegistration? registration)
    {
        if (registration == null)
            return;

        registration.Deactivate();
        if (ReferenceEquals(
                _ambientCanonicalLease.Value,
                registration))
        {
            _ambientCanonicalLease.Value = registration.Previous;
            CompactAmbientCanonicalLeaseHead();
        }
    }

    /// <summary>
    /// Uses the existing quiescence lease to settle publication evidence before an asynchronous canonical read.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancellation forwarded to lease acquisition when recovery is needed.
    /// An ambient canonical lease or absent evidence does not require acquisition.
    /// </param>
    /// <returns>
    /// <see langword="true"/> after the quiescence lease is acquired and disposed;
    /// otherwise, <see langword="false"/> when an ambient owner or absent evidence makes recovery unnecessary.
    /// </returns>
    private async Task<bool>
        RecoverPendingFilePublicationsBeforeCanonicalReadAsync(
            CancellationToken cancellationToken)
    {
        using var t351Recovery = MeasureOperation("ReadPublication.RecoverBeforeReadAsync");
        if (HasAmbientCanonicalLease() ||
            !HasPendingFilePublications())
            return false;

        await using var writeLease =
            await AcquirePublicationReadQuiescenceLeaseAsync(
                cancellationToken);
        return true;
    }

    /// <summary>
    /// Uses the existing quiescence lease to settle publication evidence before a synchronous canonical read.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> after the quiescence lease is acquired and disposed;
    /// otherwise, <see langword="false"/> when an ambient owner or absent evidence makes recovery unnecessary.
    /// </returns>
    private bool RecoverPendingFilePublicationsBeforeCanonicalRead()
    {
        using var t351Recovery = MeasureOperation("ReadPublication.RecoverBeforeReadSync");
        if (HasAmbientCanonicalLease() ||
            !HasPendingFilePublications())
            return false;

        var writeLease = AcquirePublicationReadQuiescenceLeaseAsync()
            .GetAwaiter()
            .GetResult();
        writeLease.DisposeAsync().AsTask().GetAwaiter().GetResult();
        return true;
    }

    internal static byte[] EncodeUtf8WithPreamble(string content)
    {
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(content);
        var bytes = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);
        return bytes;
    }

    internal CanonicalLoadTransactionPaths GetLoadTransactionPaths(string transactionId)
    {
        if (!Guid.TryParseExact(transactionId, "N", out _))
            throw new ArgumentException("Load transaction ID must be a GUID in N format.", nameof(transactionId));

        var transactionRoot = Path.Combine(
            _basePath,
            ".boe_runtime",
            "load-transactions",
            transactionId);
        return new CanonicalLoadTransactionPaths(
            transactionId,
            transactionRoot,
            Path.Combine(transactionRoot, "stage", "game_session"),
            Path.Combine(transactionRoot, "backup", "game_session"),
            Path.Combine(transactionRoot, "failed", "game_session"));
    }

    internal string BeginLoadTransaction(CanonicalWriteLease writeLease, string transactionId)
    {
        EnsureValidSessionReplacementLease(writeLease);
        RecoverInterruptedLoadTransaction(writeLease);
        var previousGenerationId = RuntimeFileExists(SessionGenerationPath)
            ? ReadSessionGeneration()
            : null;
        var replacementGenerationId = Guid.NewGuid().ToString("N");
        WriteLoadTransactionJournal(
            new LoadTransactionJournal(
                SchemaVersion: 2,
                TransactionId: transactionId,
                Committed: false,
                PreviousGenerationId: previousGenerationId,
                ReplacementGenerationId: replacementGenerationId));
        return replacementGenerationId;
    }

    internal void ActivateLoadTransactionSession(
        CanonicalWriteLease writeLease,
        string transactionId)
    {
        EnsureValidSessionReplacementLease(writeLease);
        var journal = ReadLoadTransactionJournal();
        if (!string.Equals(journal.TransactionId, transactionId, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(journal.ReplacementGenerationId))
        {
            throw new InvalidDataException("Active load transaction replacement authority is invalid.");
        }

        WriteSessionGeneration(journal.ReplacementGenerationId);
        DeleteWorkerSessionArtifactsCore();
    }

    internal void CommitLoadTransaction(CanonicalWriteLease writeLease, string transactionId)
    {
        EnsureValidSessionReplacementLease(writeLease);
        var journal = ReadLoadTransactionJournal();
        // This unmigrated load journal retains its original physical generation
        // read; ordinary logical readers must not alter its commit admission.
        if (!string.Equals(journal.TransactionId, transactionId, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(journal.ReplacementGenerationId) ||
            !RuntimeFileExists(SessionGenerationPath) ||
            !string.Equals(ReadSessionGeneration(), journal.ReplacementGenerationId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Active load transaction generation was not activated.");
        }

        WriteLoadTransactionJournal(journal with { Committed = true });

        try
        {
            CleanupCommittedLoadTransaction(GetLoadTransactionPaths(transactionId));
        }
        catch (Exception ex)
        {
            // The committed journal makes cleanup retryable at the next startup.
            _logger.LogWarning(ex, "Не удалось сразу очистить завершённую транзакцию загрузки {TransactionId}.", transactionId);
        }
    }

    /// <summary>
    /// Resolves an existing load transaction under its original lease, retaining mutation admission through cleanup.
    /// </summary>
    /// <param name="writeLease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    internal void RecoverInterruptedLoadTransaction(CanonicalWriteLease writeLease)
    {
        using var validationMutation = BeginValidationMutation(writeLease);
        EnsureValidCanonicalWriteLease(writeLease);
        writeLease.EnsureNoPendingLocalDecision();
        if (!RuntimeFileExists(ActiveLoadTransactionJournalPath))
            return;

        var journal = ReadLoadTransactionJournal();
        var paths = GetLoadTransactionPaths(journal.TransactionId);

        if (!journal.Committed || !LoadDirectoryExists(GameSessionPath))
        {
            RestoreLoadTransactionBackup(paths);
            if (journal.SchemaVersion >= 2)
                RestoreLoadTransactionGeneration(journal.PreviousGenerationId);
        }
        else if (!string.IsNullOrWhiteSpace(journal.ReplacementGenerationId))
        {
            WriteSessionGeneration(journal.ReplacementGenerationId);
        }

        CleanupCommittedLoadTransaction(paths);
        _logger.LogWarning(
            "Восстановлена незавершённая транзакция загрузки {TransactionId} (committed={Committed}).",
            journal.TransactionId,
            journal.Committed);
    }

    internal void CleanupInactiveLoadTransaction(CanonicalLoadTransactionPaths paths)
    {
        if (ActiveLoadTransactionReferences(paths.TransactionId))
            return;

        if (RuntimeDirectoryExists(paths.TransactionRoot))
        {
            DeleteRuntimeDirectory(
                paths.TransactionRoot,
                requireSingleFileLinks: false);
        }
    }

    internal bool LoadDirectoryExists(string path)
    {
        EnsureLoadTransactionOperationPathIsSafe(path);
        var fullPath = Path.GetFullPath(path);
        PhysicalFileAuthority.NamespaceEntryKind kind;
        if (IsSameOrDescendant(
                fullPath,
                Path.GetFullPath(RuntimeRootPath)))
        {
            kind = ProbeRuntimeNamespaceEntry(
                fullPath,
                "Load runtime directory existence");
        }
        else
        {
            kind = PhysicalFileAuthority.ProbeNamespaceEntryFromRoot(
                _basePath,
                fullPath,
                "Canonical load directory existence");
        }

        return kind switch
        {
            PhysicalFileAuthority.NamespaceEntryKind.Missing => false,
            PhysicalFileAuthority.NamespaceEntryKind.Directory => true,
            PhysicalFileAuthority.NamespaceEntryKind.RegularFile =>
                throw new InvalidDataException(
                    "Load directory authority resolved to a regular file."),
            PhysicalFileAuthority.NamespaceEntryKind.ReparsePoint =>
                throw new InvalidDataException(
                    "Load directory authority resolved to a reparse point."),
            _ => throw new InvalidDataException(
                "Load directory authority resolved to an unknown namespace entry.")
        };
    }

    internal void CreateLoadDirectory(string path)
    {
        EnsureLoadTransactionOperationPathIsSafe(path);
        _loadTransactionOperations.BeforeCreateDirectory(path);
        var fullPath = Path.GetFullPath(path);
        var runtimeRoot = Path.GetFullPath(RuntimeRootPath);
        if (IsSameOrDescendant(fullPath, runtimeRoot))
        {
            using var authority = EnsureStableRuntimeDirectory(fullPath);
            return;
        }

        if (string.Equals(
                fullPath,
                Path.GetFullPath(GameSessionPath),
                StringComparison.OrdinalIgnoreCase))
        {
            using var authority = PhysicalFileAuthority.EnsureStableDirectory(
                _basePath,
                fullPath,
                "Canonical load session");
            EnsureCanonicalSessionRootIsNotReparsePoint();
            return;
        }

        throw new InvalidDataException(
            "Load directory creation is outside runtime and canonical session authority.");
    }

    internal LoadStagingAuthoritySet CreateLoadStagingAuthoritySet(
        string stagingSessionRoot)
    {
        EnsureRuntimePathIsSafe(stagingSessionRoot);
        return new LoadStagingAuthoritySet(this, stagingSessionRoot);
    }

    internal async Task WriteLoadTransactionFileAsync(
        string path,
        Stream source,
        CancellationToken cancellationToken = default)
    {
        if (!source.CanSeek)
        {
            throw new InvalidDataException(
                "A retained load-staging write requires an explicit expected length.");
        }

        await WriteLoadTransactionFileCoreAsync(
            path,
            source,
            source.Length - source.Position,
            authoritySet: null,
            cancellationToken);
    }

    internal Task WriteLoadTransactionFileAsync(
        string path,
        Stream source,
        long expectedLength,
        LoadStagingAuthoritySet authoritySet,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authoritySet);
        return WriteLoadTransactionFileCoreAsync(
            path,
            source,
            expectedLength,
            authoritySet,
            cancellationToken);
    }

    private async Task WriteLoadTransactionFileCoreAsync(
        string path,
        Stream source,
        long expectedLength,
        LoadStagingAuthoritySet? authoritySet,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (expectedLength < 0)
        {
            throw new InvalidDataException(
                "Load transaction file length cannot be negative.");
        }

        EnsureRuntimePathIsSafe(path);
        var parent = Path.GetDirectoryName(path)
            ?? throw new InvalidDataException("Load transaction file has no parent.");
        EnsureRuntimeDirectoryExistsAndIsSafe(parent);
        using var parentAuthority = EnsureStableRuntimeDirectory(parent);
        if (_hooks?.BeforeRuntimeFileCreateAsync != null)
            await _hooks.BeforeRuntimeFileCreateAsync(path);

        FileStream? output = null;
        try
        {
            output = PhysicalFileAuthority.CreateNewWritableFile(
                parentAuthority,
                path,
                "Load transaction staging",
                asynchronous: true,
                shareDelete: authoritySet != null,
                requestDeleteAccess: authoritySet == null);
            var buffer = new byte[64 * 1024];
            long written = 0;
            while (true)
            {
                var read = await source.ReadAsync(
                    buffer,
                    cancellationToken);
                if (read == 0)
                    break;

                written += read;
                if (written > expectedLength)
                {
                    throw new InvalidDataException(
                        "Load transaction payload exceeded its advertised length.");
                }

                await output.WriteAsync(
                    buffer.AsMemory(0, read),
                    cancellationToken);
            }

            if (written != expectedLength)
            {
                throw new InvalidDataException(
                    "Load transaction payload did not match its advertised length.");
            }

            await output.FlushAsync(cancellationToken);
            output.Flush(flushToDisk: true);
            EnsureRuntimePathIsSafe(path);
            if (authoritySet == null)
                return;

            var authority =
                PhysicalFileAuthority.CaptureOpenedFileAuthority(
                    output.SafeFileHandle,
                    path,
                    "Retained load transaction staging");
            if (authority.Length != expectedLength)
            {
                throw new InvalidDataException(
                    "Retained load transaction payload length changed.");
            }

            await output.DisposeAsync();
            output = null;
            FileStream? retainedStream = null;
            try
            {
                retainedStream = PhysicalFileAuthority.OpenReadFile(
                        parentAuthority,
                        path,
                        "Retained load transaction staging",
                        asynchronous: false)
                    ?? throw new InvalidDataException(
                        "Retained load transaction staging file disappeared.");
                var retainedAuthority =
                    PhysicalFileAuthority.EnsureExactOpenedFileAuthority(
                        retainedStream.SafeFileHandle,
                        path,
                        authority.Identity,
                        authority.Sha256,
                        "Retained load transaction staging",
                        authority.Length);
                authoritySet.Add(
                    path,
                    retainedStream,
                    retainedAuthority);
                retainedStream = null;
            }
            finally
            {
                retainedStream?.Dispose();
            }
        }
        finally
        {
            if (output != null)
                await output.DisposeAsync();
        }
    }

    internal void DeleteLoadTransactionFile(string path)
    {
        EnsureRuntimePathIsSafe(path);
        DeleteRuntimeFile(path);
    }

    internal void DeleteLoadTransactionDirectory(string path)
    {
        EnsureRuntimePathIsSafe(path);
        DeleteRuntimeDirectory(
            path,
            requireSingleFileLinks: false);
    }

    internal void MoveLoadDirectory(
        string sourcePath,
        string destinationPath,
        LoadStagingAuthoritySet? authoritySet = null)
    {
        EnsureLoadTransactionOperationPathIsSafe(sourcePath);
        EnsureLoadTransactionOperationPathIsSafe(destinationPath);
        EnsureDescriptorBoundCreateOnlyPublicationSupported(
            "Load transaction directory move");
        using var sourceParentAuthority = EnsureStableLoadOperationParent(
            sourcePath,
            "Load transaction source");
        using var destinationParentAuthority = EnsureStableLoadOperationParent(
            destinationPath,
            "Load transaction destination");
        Microsoft.Win32.SafeHandles.SafeFileHandle? sourceHandle =
            PhysicalFileAuthority.OpenForRename(
            sourceParentAuthority,
            sourcePath,
            isDirectory: true,
            "Load transaction directory move");
        try
        {
            var sourceIdentity = authoritySet == null
                ? null
                : PhysicalFileAuthority.CaptureFileIdentity(
                    sourceHandle,
                    "Load staging directory publication");
            _hooks?.BeforeLoadDirectoryMoveAsync?.Invoke(
                    sourcePath,
                    destinationPath)
                .GetAwaiter()
                .GetResult();
            _loadTransactionOperations.BeforeMoveDirectory(
                sourcePath,
                destinationPath);
            authoritySet?.PrepareForDirectoryMove(
                sourcePath,
                "Load staging immediately before directory move");
            _hooks?.AfterLoadStagingFinalValidationAsync?.Invoke(
                    sourcePath,
                    destinationPath)
                .GetAwaiter()
                .GetResult();
            PhysicalFileAuthority.RenameOpenedObjectRelative(
                sourceHandle,
                destinationParentAuthority,
                destinationPath,
                replaceExisting: false,
                "Load transaction directory move",
                requireSingleLink: false);

            _hooks?.AfterLoadDirectoryMoveAsync?.Invoke(
                    sourcePath,
                    destinationPath)
                .GetAwaiter()
                .GetResult();
            if (authoritySet != null)
            {
                var publishedRootAuthority =
                    new PhysicalFileAuthority.StableDirectory(
                        destinationPath,
                        sourceHandle);
                sourceHandle = null;
                authoritySet.EnsureExactAfterDirectoryMove(
                    destinationPath,
                    "Load staging immediately after directory move",
                    sourceIdentity
                    ?? throw new InvalidOperationException(
                        "Load-staging directory identity is unavailable."),
                    publishedRootAuthority);
            }
            EnsureLoadTransactionOperationPathIsSafe(destinationPath);
        }
        finally
        {
            sourceHandle?.Dispose();
        }
    }

    private void RestoreLoadTransactionBackup(CanonicalLoadTransactionPaths paths)
    {
        if (!RuntimeDirectoryExists(paths.BackupSessionPath))
            return;

        if (LoadDirectoryExists(GameSessionPath))
        {
            if (RuntimeDirectoryExists(paths.FailedSessionPath))
            {
                DeleteRuntimeDirectory(
                    paths.FailedSessionPath,
                    requireSingleFileLinks: false);
            }

            CreateLoadDirectory(Path.GetDirectoryName(paths.FailedSessionPath)!);
            MoveLoadDirectory(GameSessionPath, paths.FailedSessionPath);
        }

        MoveLoadDirectory(paths.BackupSessionPath, GameSessionPath);
    }

    private void CleanupCommittedLoadTransaction(CanonicalLoadTransactionPaths paths)
    {
        if (RuntimeDirectoryExists(paths.TransactionRoot))
        {
            DeleteRuntimeDirectory(
                paths.TransactionRoot,
                requireSingleFileLinks: false);
        }
        if (RuntimeFileExists(ActiveLoadTransactionJournalPath))
            DeleteRuntimeFile(ActiveLoadTransactionJournalPath);
    }

    private void WriteLoadTransactionJournal(LoadTransactionJournal journal)
    {
        var json = JsonSerializer.Serialize(journal);
        WriteRuntimeTextAtomic(ActiveLoadTransactionJournalPath, json);
    }

    private void RestoreLoadTransactionGeneration(string? previousGenerationId)
    {
        if (string.IsNullOrWhiteSpace(previousGenerationId))
        {
            if (RuntimeFileExists(SessionGenerationPath))
            {
                DeleteRuntimeFile(SessionGenerationPath);
                CanonicalRootAuthorityIdentity.AdvanceSessionGenerationRevision();
            }
            return;
        }

        WriteSessionGeneration(previousGenerationId);
    }

    private LoadTransactionJournal ReadLoadTransactionJournal()
    {
        try
        {
            var journal = StrictJsonAuthority.Deserialize<LoadTransactionJournal>(
                ReadRuntimeText(ActiveLoadTransactionJournalPath),
                RecoveryJsonOptions,
                "Active load transaction journal");
            if (journal is null ||
                journal.SchemaVersion is < 1 or > 2 ||
                !Guid.TryParseExact(journal.TransactionId, "N", out _) ||
                !IsOptionalCanonicalGenerationId(journal.PreviousGenerationId) ||
                !IsOptionalCanonicalGenerationId(journal.ReplacementGenerationId) ||
                (journal.SchemaVersion == 2 && string.IsNullOrWhiteSpace(journal.ReplacementGenerationId)))
            {
                throw new InvalidDataException("Active load transaction journal is invalid.");
            }

            return journal;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Active load transaction journal is invalid.", ex);
        }
    }

    private bool ActiveLoadTransactionReferences(string transactionId)
    {
        if (!RuntimeFileExists(ActiveLoadTransactionJournalPath))
            return false;

        try
        {
            return string.Equals(
                ReadLoadTransactionJournal().TransactionId,
                transactionId,
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // Never delete recovery evidence when the journal cannot be interpreted.
            return true;
        }
    }

    private static bool IsOptionalCanonicalGenerationId(string? generationId) =>
        string.IsNullOrWhiteSpace(generationId) ||
        Guid.TryParseExact(generationId, "N", out var parsedGeneration) &&
        string.Equals(generationId, parsedGeneration.ToString("N"), StringComparison.Ordinal);

    private sealed record LoadTransactionJournal(
        int SchemaVersion,
        string TransactionId,
        bool Committed,
        string? PreviousGenerationId = null,
        string? ReplacementGenerationId = null);

    /// <summary>
    /// Stages and applies an original worker transaction while retaining mutation admission through failure cleanup.
    /// </summary>
    /// <param name="writeLease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <param name="changes">
    /// Declared members and their original before and desired after states; the publication route validates the set.
    /// </param>
    /// <returns>
    /// The original worker transaction whose later decision remains owned by the caller.
    /// </returns>
    internal async Task<CanonicalWorkerApplyTransaction> BeginOriginalWorkerApplyTransactionAsync(
        CanonicalWriteLease writeLease,
        IReadOnlyList<CanonicalWorkerApplyChange> changes)
    {
        using var validationMutation = BeginValidationMutation(writeLease);
        EnsureWorkerGeneralMutationAllowed(writeLease);
        writeLease.EnsureNoPendingLocalDecision();
        if (changes.Count == 0)
            throw new ArgumentException("Worker apply transaction requires at least one changed file.", nameof(changes));
        if (RuntimeFileExists(ActiveWorkerApplyTransactionJournalPath))
            throw new InvalidOperationException("An active worker apply transaction already exists.");

        var transactionId = Guid.NewGuid().ToString("N");
        var transactionRoot = GetWorkerApplyTransactionRoot(transactionId);
        var beforeRoot = Path.Combine(transactionRoot, "before");
        CreateLoadDirectory(beforeRoot);
        var entries = new List<WorkerApplyTransactionEntry>(changes.Count);

        try
        {
            for (var index = 0; index < changes.Count; index++)
            {
                var change = changes[index];
                EnsureSafeCanonicalRelativePath(change.Path);
                var beforeImage = change.BaselineBytes == null
                    ? null
                    : $"before/{index:D4}.bin";
                if (beforeImage != null)
                {
                    await WriteRuntimeBytesAtomicAsync(
                        Path.Combine(transactionRoot, beforeImage.Replace('/', Path.DirectorySeparatorChar)),
                        change.BaselineBytes!);
                }

                entries.Add(new WorkerApplyTransactionEntry
                {
                    Path = change.Path.Replace('\\', '/'),
                    BaselineExists = change.BaselineBytes != null,
                    BeforeImage = beforeImage,
                    BeforeSha256 = ComputeSha256OrMissing(change.BaselineBytes),
                    AppliedSha256 = ComputeSha256OrMissing(change.AppliedBytes)
                });
            }

            var manifest = new WorkerApplyTransactionManifest
            {
                TransactionId = transactionId,
                Entries = entries
            };
            WriteRuntimeTextAtomic(
                Path.Combine(transactionRoot, "manifest.json"),
                JsonSerializer.Serialize(manifest));
            WriteWorkerApplyJournal(transactionId, committed: false, rolledBack: false);
            return new CanonicalWorkerApplyTransaction(transactionId, transactionRoot);
        }
        catch (Exception failure)
        {
            try
            {
                if (!RuntimeFileExists(ActiveWorkerApplyTransactionJournalPath) &&
                    RuntimeDirectoryExists(transactionRoot))
                    DeleteRuntimeDirectory(transactionRoot);
            }
            catch (Exception cleanup)
            {
                throw new AggregateException("Worker preparation failed and its private cleanup remains pending.", failure, cleanup);
            }
            throw;
        }
    }

    /// <summary>
    /// Commits the existing worker decision or journal and completes its required cleanup.
    /// </summary>
    /// <param name="writeLease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <param name="transaction">
    /// Original worker transaction or deferred decision to complete.
    /// </param>
    internal void CommitWorkerApplyTransaction(
        CanonicalWriteLease writeLease,
        CanonicalWorkerApplyTransaction transaction)
    {
        using var validationMutation = BeginValidationMutation(writeLease);
        EnsureWorkerGeneralMutationAllowed(writeLease);
        if (transaction.LocalDecision is { } decision)
        {
            var outcome = decision.Commit(writeLease);
            if (outcome.Disposition == TrustedLocalPublicationDisposition.Uncertain)
                throw new CoordinatedStatePublicationUncertainException(outcome.Failure);
            RequireCommittedLocalPublication(outcome);
            return;
        }
        writeLease.EnsureNoPendingLocalDecision();
        EnsureActiveWorkerApplyTransaction(transaction.TransactionId);
        WriteWorkerApplyJournal(transaction.TransactionId, committed: true, rolledBack: false);
        try
        {
            CleanupWorkerApplyTransaction(transaction.TransactionId);
        }
        catch (Exception ex)
        {
            // The committed journal makes cleanup retryable without revoking accepted canonical bytes.
            _logger.LogWarning(
                ex,
                "Не удалось сразу очистить завершённую worker apply транзакцию {TransactionId}.",
                transaction.TransactionId);
        }
    }

    /// <summary>
    /// Rolls back the existing worker transaction and retains mutation admission through recovery and cleanup.
    /// </summary>
    /// <param name="writeLease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <param name="transaction">
    /// Original worker transaction or deferred decision to complete.
    /// </param>
    /// <returns>
    /// Paths whose rollback remains conflicted after the existing recovery protocol.
    /// </returns>
    internal async Task<IReadOnlyList<string>> RollbackWorkerApplyTransactionAsync(
        CanonicalWriteLease writeLease,
        CanonicalWorkerApplyTransaction transaction)
    {
        using var validationMutation = BeginValidationMutation(writeLease);
        EnsureWorkerGeneralMutationAllowed(writeLease);
        try
        {
            if (transaction.LocalDecision is { } decision)
            {
                decision.Rollback(writeLease, _hooks?.LocalPublicationRecoveryObserver);
                return [];
            }
            writeLease.EnsureNoPendingLocalDecision();
            EnsureActiveWorkerApplyTransaction(transaction.TransactionId);
            await RecoverInterruptedWorkerApplyTransactionAsync(writeLease);
            return [];
        }
        catch (Exception ex)
        {
            return [$"worker apply rollback remains pending: {ex.Message}"];
        }
    }

    /// <summary>
    /// Recovers an interrupted worker transaction under the original lease through its final cleanup.
    /// </summary>
    /// <param name="writeLease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <returns>
    /// Completion after the operation and its required cleanup finish.
    /// </returns>
    private async Task RecoverInterruptedWorkerApplyTransactionAsync(CanonicalWriteLease writeLease)
    {
        using var validationMutation = BeginValidationMutation(writeLease);
        EnsureValidCanonicalWriteLease(writeLease);
        if (!RuntimeFileExists(ActiveWorkerApplyTransactionJournalPath))
            return;

        var journal = ReadWorkerApplyJournal();
        var transactionRoot = GetWorkerApplyTransactionRoot(journal.TransactionId);
        if (journal.Committed || journal.RolledBack)
        {
            CleanupWorkerApplyTransaction(journal.TransactionId);
            return;
        }

        var manifestPath = Path.Combine(transactionRoot, "manifest.json");
        WorkerApplyTransactionManifest manifest;
        try
        {
            manifest = StrictJsonAuthority.Deserialize<WorkerApplyTransactionManifest>(
                           ReadRuntimeText(manifestPath),
                           RecoveryJsonOptions,
                           "Worker apply transaction manifest") ??
                       throw new InvalidDataException("Worker apply transaction manifest is missing.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Worker apply transaction manifest is invalid.", ex);
        }

        if (manifest.SchemaVersion != 1 ||
            !string.Equals(manifest.TransactionId, journal.TransactionId, StringComparison.Ordinal) ||
            manifest.Entries.Count == 0 ||
            manifest.Entries
                .GroupBy(entry => entry.Path.Replace('\\', '/'), StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() > 1))
        {
            throw new InvalidDataException("Worker apply transaction manifest is invalid.");
        }

        var errors = new List<string>();
        foreach (var entry in manifest.Entries.AsEnumerable().Reverse())
        {
            try
            {
                EnsureSafeCanonicalRelativePath(entry.Path);
                var baseline = ReadWorkerApplyBeforeImage(transactionRoot, entry);
                var fullPath = ResolvePath(entry.Path);
                using var parentAuthority =
                    EnsureStableCanonicalParent(
                        entry.Path,
                        fullPath);
                EnsureCanonicalMutationBoundary(
                    entry.Path,
                    fullPath);
                var destinationEntry =
                    PhysicalFileAuthority.ProbeNamespaceEntry(
                        parentAuthority,
                        fullPath,
                        "Worker apply recovery destination");
                FileStream? retainedCurrentStream = null;
                try
                {
                    PhysicalFileAuthority.OpenedFileAuthority?
                        currentAuthority = null;
                    if (destinationEntry ==
                        PhysicalFileAuthority
                            .NamespaceEntryKind
                            .RegularFile)
                    {
                        retainedCurrentStream =
                            PhysicalFileAuthority.OpenReadFile(
                                parentAuthority,
                                fullPath,
                                "Retained worker apply recovery destination",
                                asynchronous: false,
                                shareDelete: true,
                                shareWrite: true) ??
                            throw new InvalidDataException(
                                "Worker apply recovery destination disappeared.");
                        currentAuthority =
                            PhysicalFileAuthority
                                .CaptureOpenedFileAuthority(
                                    retainedCurrentStream
                                        .SafeFileHandle,
                                    fullPath,
                                    "Retained worker apply recovery destination");
                    }
                    else if (destinationEntry !=
                             PhysicalFileAuthority
                                 .NamespaceEntryKind
                                 .Missing)
                    {
                        throw new InvalidDataException(
                            "Worker apply recovery destination is not a physical regular file.");
                    }

                    var currentHash =
                        currentAuthority?.Sha256 ??
                        "missing";
                    if (string.Equals(
                            currentHash,
                            entry.BeforeSha256,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    if (!string.Equals(
                            currentHash,
                            entry.AppliedSha256,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add(
                            $"worker apply recovery found unowned canonical bytes: {entry.Path}.");
                        continue;
                    }

                    if (baseline == null)
                    {
                        var retainedAuthority =
                            currentAuthority ??
                            throw new InvalidDataException(
                                "Worker apply recovery cannot delete a missing destination.");
                        await DeleteFileIfCurrentAuthorityAsync(
                            writeLease,
                            entry.Path,
                            retainedAuthority.Identity,
                            retainedAuthority.Sha256);
                        continue;
                    }

                    var appliedDestinationMissing = string.Equals(
                        entry.AppliedSha256,
                        "missing",
                        StringComparison.OrdinalIgnoreCase);
                    if (appliedDestinationMissing)
                    {
                        await WriteFileAtomicBytesIfCurrentOwnedAsync(
                            writeLease,
                            entry.Path,
                            baseline,
                            [],
                            allowMissingCurrent: true);
                    }
                    else
                    {
                        var retainedAuthority =
                            currentAuthority ??
                            throw new InvalidDataException(
                                "Worker apply recovery destination authority is missing.");
                        await WriteFileAtomicBytesIfCurrentAuthorityAsync(
                            writeLease,
                            entry.Path,
                            baseline,
                            retainedAuthority.Identity,
                            retainedAuthority.Sha256);
                    }
                }
                finally
                {
                    retainedCurrentStream?.Dispose();
                }
            }
            catch (Exception ex)
            {
                errors.Add($"worker apply recovery failed for {entry.Path}: {ex.Message}");
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidDataException(
                "Interrupted worker apply transaction could not be fully recovered: " +
                string.Join(" | ", errors));
        }

        WriteWorkerApplyJournal(journal.TransactionId, committed: false, rolledBack: true);
        CleanupWorkerApplyTransaction(journal.TransactionId);
    }

    private byte[]? ReadWorkerApplyBeforeImage(
        string transactionRoot,
        WorkerApplyTransactionEntry entry)
    {
        if (!entry.BaselineExists)
        {
            if (!string.IsNullOrWhiteSpace(entry.BeforeImage) ||
                !string.Equals(entry.BeforeSha256, "missing", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Missing worker baseline has an invalid before-image contract.");
            }

            return null;
        }

        if (string.IsNullOrWhiteSpace(entry.BeforeImage) ||
            !entry.BeforeImage.StartsWith("before/", StringComparison.Ordinal) ||
            entry.BeforeImage.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Worker apply before-image path is invalid.");
        }

        var beforePath = Path.GetFullPath(Path.Combine(
            transactionRoot,
            entry.BeforeImage.Replace('/', Path.DirectorySeparatorChar)));
        var expectedRoot = Path.GetFullPath(transactionRoot) + Path.DirectorySeparatorChar;
        if (!beforePath.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Worker apply before-image escapes its transaction.");
        EnsureRuntimePathIsSafe(beforePath);
        if (!File.Exists(beforePath))
            throw new InvalidDataException("Worker apply before-image is missing or escapes its transaction.");

        var bytes = ReadExactBytesFromStablePath(beforePath);
        EnsureRuntimePathIsSafe(beforePath);
        if (!string.Equals(ComputeSha256OrMissing(bytes), entry.BeforeSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Worker apply before-image hash is invalid.");
        return bytes;
    }

    private void EnsureActiveWorkerApplyTransaction(string expectedTransactionId)
    {
        var journal = ReadWorkerApplyJournal();
        if (!string.Equals(journal.TransactionId, expectedTransactionId, StringComparison.Ordinal))
            throw new InvalidOperationException("Active worker apply transaction identity changed.");
    }

    private WorkerApplyTransactionJournal ReadWorkerApplyJournal()
    {
        try
        {
            var journal = StrictJsonAuthority.Deserialize<WorkerApplyTransactionJournal>(
                ReadRuntimeText(ActiveWorkerApplyTransactionJournalPath),
                RecoveryJsonOptions,
                "Active worker apply transaction journal");
            if (journal is null || journal.SchemaVersion != 1 ||
                (journal.Committed && journal.RolledBack) ||
                !Guid.TryParseExact(journal.TransactionId, "N", out _))
            {
                throw new InvalidDataException("Active worker apply transaction journal is invalid.");
            }

            return journal;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Active worker apply transaction journal is invalid.", ex);
        }
    }

    private void WriteWorkerApplyJournal(
        string transactionId,
        bool committed,
        bool rolledBack)
    {
        var journal = new WorkerApplyTransactionJournal
        {
            TransactionId = transactionId,
            Committed = committed,
            RolledBack = rolledBack
        };
        WriteRuntimeTextAtomic(
            ActiveWorkerApplyTransactionJournalPath,
            JsonSerializer.Serialize(journal));
    }

    private void CleanupWorkerApplyTransaction(string transactionId)
    {
        var transactionRoot = GetWorkerApplyTransactionRoot(transactionId);
        if (RuntimeDirectoryExists(transactionRoot))
            DeleteRuntimeDirectory(transactionRoot);
        if (RuntimeFileExists(ActiveWorkerApplyTransactionJournalPath))
            DeleteRuntimeFile(ActiveWorkerApplyTransactionJournalPath);
    }

    private string GetWorkerApplyTransactionRoot(string transactionId)
    {
        if (!Guid.TryParseExact(transactionId, "N", out _))
            throw new InvalidDataException("Worker apply transaction ID is invalid.");
        return Path.Combine(_basePath, ".boe_runtime", "worker-apply-transactions", transactionId);
    }

    private void EnsureSafeCanonicalRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new InvalidDataException("Worker apply transaction path is invalid.");

        _ = ResolvePath(relativePath);
    }

    private void EnsureCanonicalSessionRootIsNotReparsePoint()
    {
        var sessionRoot = Path.GetFullPath(GameSessionPath);
        if (!Directory.Exists(sessionRoot) && !File.Exists(sessionRoot))
            return;

        if ((File.GetAttributes(sessionRoot) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                "Canonical game_session root must not be a reparse-point alias.");
        }
    }

    private PhysicalFileAuthority.StableDirectory EnsureStableCanonicalParent(
        string relativePath,
        string expectedFullPath)
    {
        EnsureCanonicalPathStillSafe(relativePath, expectedFullPath);
        var parent = Path.GetDirectoryName(expectedFullPath)
            ?? throw new InvalidDataException(
                "Canonical authority path has no parent directory.");
        var authority = PhysicalFileAuthority.EnsureStableDirectory(
            _basePath,
            parent,
            "Canonical game-session");
        try
        {
            EnsureCanonicalPathStillSafe(relativePath, expectedFullPath);
            return authority;
        }
        catch
        {
            authority.Dispose();
            throw;
        }
    }

    private PhysicalFileAuthority.StableDirectory EnsureStableRuntimeDirectory(
        string directoryPath)
    {
        EnsureRuntimePathIsSafe(directoryPath);
        var authority = PhysicalFileAuthority.EnsureStableDirectory(
            _basePath,
            directoryPath,
            "Client runtime");
        try
        {
            EnsureRuntimePathIsSafe(directoryPath);
            return authority;
        }
        catch
        {
            authority.Dispose();
            throw;
        }
    }

    private void EnsureRuntimeDirectoryExistsAndIsSafe(string directoryPath)
    {
        using var authority = EnsureStableRuntimeDirectory(directoryPath);
    }

    private PhysicalFileAuthority.StableDirectory EnsureStableLoadOperationParent(
        string path,
        string authorityName)
    {
        EnsureLoadTransactionOperationPathIsSafe(path);
        var fullPath = Path.GetFullPath(path);
        var parentPath = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidDataException(
                $"{authorityName} has no parent directory.");
        var runtimeRoot = Path.GetFullPath(RuntimeRootPath);
        if (IsSameOrDescendant(fullPath, runtimeRoot))
            return EnsureStableRuntimeDirectory(parentPath);

        if (string.Equals(
                fullPath,
                Path.GetFullPath(GameSessionPath),
                StringComparison.OrdinalIgnoreCase))
        {
            return PhysicalFileAuthority.EnsureStableDirectory(
                _basePath,
                parentPath,
                authorityName);
        }

        throw new InvalidDataException(
            $"{authorityName} is outside runtime and canonical session authority.");
    }

    private string CreateRuntimeStagingRoot(string area)
    {
        var areaRoot = Path.Combine(RuntimeRootPath, area);
        EnsureRuntimeDirectoryExistsAndIsSafe(areaRoot);

        var stagingRoot = Path.Combine(areaRoot, Guid.NewGuid().ToString("N"));
        using var authority = EnsureStableRuntimeDirectory(stagingRoot);
        EnsureRuntimePathIsSafe(stagingRoot);
        return stagingRoot;
    }

    private void DeleteRuntimeStagingRoot(string area, string stagingRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingRoot);
        var areaRoot = Path.GetFullPath(Path.Combine(RuntimeRootPath, area));
        var fullStagingRoot = Path.GetFullPath(stagingRoot);
        if (!IsSameOrDescendant(fullStagingRoot, areaRoot) ||
            string.Equals(fullStagingRoot, areaRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Runtime staging cleanup path is outside its authority root.");
        }

        EnsureRuntimePathIsSafe(fullStagingRoot);
        if (Directory.Exists(fullStagingRoot))
            DeleteRuntimeDirectory(fullStagingRoot);
    }

    private void EnsureRuntimePathIsSafe(string fullPath)
    {
        var runtimeRoot = Path.GetFullPath(RuntimeRootPath).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var candidate = Path.GetFullPath(fullPath);
        if (!IsSameOrDescendant(candidate, runtimeRoot))
        {
            throw new InvalidDataException(
                "Client runtime path is outside the physical runtime authority root.");
        }

        EnsureNoExistingReparsePoint(
            runtimeRoot,
            candidate,
            "Client runtime");
    }

    private void EnsureLoadTransactionOperationPathIsSafe(string path)
    {
        var candidate = Path.GetFullPath(path);
        if (IsSameOrDescendant(candidate, Path.GetFullPath(RuntimeRootPath)))
        {
            EnsureRuntimePathIsSafe(candidate);
            return;
        }

        if (string.Equals(
                candidate,
                Path.GetFullPath(GameSessionPath),
                StringComparison.OrdinalIgnoreCase))
        {
            EnsureCanonicalSessionRootIsNotReparsePoint();
            return;
        }

        throw new InvalidDataException(
            "Load transaction operation path is outside runtime and canonical session authority.");
    }

    private bool RuntimeFileExists(string path)
    {
        return ProbeRuntimeNamespaceEntry(
            path,
            "Runtime file existence") switch
        {
            PhysicalFileAuthority.NamespaceEntryKind.Missing => false,
            PhysicalFileAuthority.NamespaceEntryKind.RegularFile => true,
            PhysicalFileAuthority.NamespaceEntryKind.Directory =>
                throw new InvalidDataException(
                    "Runtime file authority resolved to a directory."),
            PhysicalFileAuthority.NamespaceEntryKind.ReparsePoint =>
                throw new InvalidDataException(
                    "Runtime file authority resolved to a reparse point."),
            _ => throw new InvalidDataException(
                "Runtime file authority resolved to an unknown namespace entry.")
        };
    }

    private bool RuntimeDirectoryExists(string path)
    {
        return ProbeRuntimeNamespaceEntry(
            path,
            "Runtime directory existence") switch
        {
            PhysicalFileAuthority.NamespaceEntryKind.Missing => false,
            PhysicalFileAuthority.NamespaceEntryKind.Directory => true,
            PhysicalFileAuthority.NamespaceEntryKind.RegularFile =>
                throw new InvalidDataException(
                    "Runtime directory authority resolved to a regular file."),
            PhysicalFileAuthority.NamespaceEntryKind.ReparsePoint =>
                throw new InvalidDataException(
                    "Runtime directory authority resolved to a reparse point."),
            _ => throw new InvalidDataException(
                "Runtime directory authority resolved to an unknown namespace entry.")
        };
    }

    private PhysicalFileAuthority.NamespaceEntryKind
        ProbeRuntimeNamespaceEntry(
            string path,
            string authorityName)
    {
        EnsureRuntimePathIsSafe(path);
        var runtimeRootKind =
            PhysicalFileAuthority.ProbeNamespaceEntryFromRoot(
                _basePath,
                RuntimeRootPath,
                authorityName + " root");
        if (runtimeRootKind ==
            PhysicalFileAuthority.NamespaceEntryKind.Missing)
        {
            return PhysicalFileAuthority.NamespaceEntryKind.Missing;
        }
        if (runtimeRootKind !=
            PhysicalFileAuthority.NamespaceEntryKind.Directory)
        {
            throw new InvalidDataException(
                $"{authorityName} root is not a physical directory.");
        }

        return PhysicalFileAuthority.ProbeNamespaceEntryFromRoot(
            RuntimeRootPath,
            path,
            authorityName);
    }

    private string ReadRuntimeText(string path)
    {
        EnsureRuntimePathIsSafe(path);
        _hooks?.BeforeRuntimeFileReadOpenAsync?.Invoke(path)
            .GetAwaiter()
            .GetResult();
        using var parentAuthority = EnsureStableRuntimeDirectory(
            Path.GetDirectoryName(path)
            ?? throw new InvalidDataException(
                "Runtime authority file has no parent."));
        using var stream = PhysicalFileAuthority.OpenReadFile(
            parentAuthority,
            path,
            "Runtime authority read",
            asynchronous: false,
            afterOpenedBeforeValidation: () =>
                _hooks?.AfterRuntimeFileReadOpenedAsync?.Invoke(path)
                    .GetAwaiter()
                    .GetResult())
            ?? throw new FileNotFoundException(
                "Runtime authority file does not exist.",
                path);
        _hooks?.AfterRuntimeFileReadInitialValidationAsync?.Invoke(path)
            .GetAwaiter()
            .GetResult();
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);
        var content = reader.ReadToEnd();
        PhysicalFileAuthority.EnsureRegularFileHandleMatchesExpectedPath(
            stream.SafeFileHandle,
            path,
            "Runtime authority read completion");
        return content;
    }

    private void WriteRuntimeTextAtomic(string path, string content)
    {
        EnsureRuntimePathIsSafe(path);
        _loadTransactionOperations.BeforeWriteAllTextAtomic(path, content);
        WriteRuntimeBytesAtomicCoreAsync(
                path,
                Encoding.UTF8.GetBytes(content),
                CancellationToken.None,
                "Runtime authority")
            .GetAwaiter()
            .GetResult();
        EnsureRuntimePathIsSafe(path);
    }

    private void DeleteRuntimeFile(string path)
    {
        EnsureRuntimePathIsSafe(path);
        using var parentAuthority = EnsureStableRuntimeDirectory(
            Path.GetDirectoryName(path)
            ?? throw new InvalidDataException(
                "Runtime authority file has no parent."));
        _loadTransactionOperations.BeforeDeleteFile(path);
        PhysicalFileAuthority.TryDeleteFile(
            parentAuthority,
            path,
            "Runtime authority file cleanup");
        EnsureRuntimePathIsSafe(
            Path.GetDirectoryName(path)
            ?? throw new InvalidDataException("Runtime authority file has no parent."));
    }

    private void DeleteRuntimeDirectory(
        string path,
        bool requireSingleFileLinks = true)
    {
        EnsureRuntimePathIsSafe(path);
        using var parentAuthority = EnsureStableRuntimeDirectory(
            Path.GetDirectoryName(path)
            ?? throw new InvalidDataException(
                "Runtime authority directory has no parent."));
        _loadTransactionOperations.BeforeDeleteDirectory(path);
        PhysicalFileAuthority.TryDeleteDirectoryTree(
            parentAuthority,
            path,
            "Runtime authority directory cleanup",
            requireSingleFileLinks);
        EnsureRuntimePathIsSafe(
            Path.GetDirectoryName(path)
            ?? throw new InvalidDataException("Runtime authority directory has no parent."));
    }

    internal StableReadFile? OpenExactPhysicalReadFile(
        string fullPath,
        string authorityName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorityName);
        var expectedPath = Path.GetFullPath(fullPath);
        var parentPath = Path.GetDirectoryName(expectedPath)
            ?? throw new InvalidDataException(
                $"{authorityName} file has no parent directory.");
        var parentAuthority = PhysicalFileAuthority.OpenStableDirectory(
            parentPath,
            authorityName);
        FileStream? stream = null;
        try
        {
            stream = PhysicalFileAuthority.OpenReadFile(
                parentAuthority,
                expectedPath,
                authorityName,
                asynchronous: true);
            if (stream == null)
            {
                parentAuthority.Dispose();
                return null;
            }

            _hooks?.AfterExactPhysicalReadInitialValidationAsync
                ?.Invoke(expectedPath)
                .GetAwaiter()
                .GetResult();
            ObservePassiveRead(_hooks?.ExactPhysicalReadActivityObserver, expectedPath);
            return new StableReadFile(
                stream,
                parentAuthority,
                expectedPath,
                authorityName);
        }
        catch
        {
            stream?.Dispose();
            parentAuthority.Dispose();
            throw;
        }
    }

    private async Task<StableReadFile?> OpenCanonicalReadStreamAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var expectedFullPath = ResolvePath(relativePath);
        if (!FileExistsCore(relativePath))
            return null;

        await InvokeBeforeCanonicalReadOpenAsync(relativePath);
        cancellationToken.ThrowIfCancellationRequested();

        PhysicalFileAuthority.StableDirectory? parentAuthority = null;
        FileStream? stream = null;
        try
        {
            parentAuthority = EnsureStableCanonicalParent(
                relativePath,
                expectedFullPath);
            EnsureCanonicalPathStillSafe(relativePath, expectedFullPath);
            stream = PhysicalFileAuthority.OpenReadFile(
                parentAuthority,
                expectedFullPath,
                "Canonical game-session read",
                asynchronous: true);
            if (stream == null)
            {
                parentAuthority.Dispose();
                return null;
            }

            if (_hooks?.AfterCanonicalReadInitialValidationAsync != null)
            {
                await _hooks.AfterCanonicalReadInitialValidationAsync(
                    relativePath);
            }
            ObservePassiveRead(_hooks?.CanonicalReadActivityObserver, relativePath);
            return new StableReadFile(
                stream,
                parentAuthority,
                expectedFullPath,
                "Canonical game-session read");
        }
        catch
        {
            stream?.Dispose();
            parentAuthority?.Dispose();
            throw;
        }
    }

    private async Task InvokeBeforeCanonicalMutationBoundaryAsync(string relativePath)
    {
        if (_hooks?.BeforeCanonicalMutationAsync != null)
            await _hooks.BeforeCanonicalMutationAsync(relativePath);
        if (_hooks?.BeforeCanonicalMutationBoundaryAsync != null)
            await _hooks.BeforeCanonicalMutationBoundaryAsync(relativePath);
    }

    private Task InvokeBeforeCanonicalReadOpenAsync(string relativePath) =>
        _hooks?.BeforeCanonicalReadOpenAsync?.Invoke(relativePath) ?? Task.CompletedTask;

    private Task InvokeAfterCanonicalReadAttemptAsync(string relativePath) =>
        _hooks?.AfterCanonicalReadAttemptAsync?.Invoke(relativePath) ??
        Task.CompletedTask;

    private void InvokeAfterCanonicalReadAttempt(string relativePath) =>
        InvokeAfterCanonicalReadAttemptAsync(relativePath)
            .GetAwaiter()
            .GetResult();

    private Task InvokeAfterCanonicalMutationBoundaryValidatedAsync(string relativePath) =>
        _hooks?.AfterCanonicalMutationBoundaryValidatedAsync?.Invoke(relativePath) ??
        Task.CompletedTask;

    private void EnsureCanonicalPathStillSafe(string relativePath, string expectedFullPath)
    {
        var currentFullPath = ResolvePath(relativePath);
        if (!string.Equals(currentFullPath, expectedFullPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Canonical game-session path identity changed before mutation.");
    }

    private static bool IsSameOrDescendant(string candidatePath, string rootPath)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (string.Equals(candidatePath, rootPath, comparison))
            return true;

        var rootWithSeparator = rootPath.EndsWith(Path.DirectorySeparatorChar) ||
                                rootPath.EndsWith(Path.AltDirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;
        return candidatePath.StartsWith(rootWithSeparator, comparison);
    }


    private static string ComputeSha256OrMissing(byte[]? content) =>
        content == null
            ? "missing"
            : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant();

    private async Task WriteRuntimeBytesAtomicAsync(string path, byte[] content)
    {
        await WriteRuntimeBytesAtomicCoreAsync(
            path,
            content,
            CancellationToken.None,
            "Runtime before-image");
    }

    private async Task WriteRuntimeBytesAtomicCoreAsync(
        string path,
        byte[] content,
        CancellationToken cancellationToken,
        string authorityName)
    {
        EnsureRuntimePathIsSafe(path);
        var destinationEntry =
            PhysicalFileAuthority.ProbeNamespaceEntryFromRoot(
                _basePath,
                path,
                authorityName + " destination");
        EnsureAuthorityFilePublicationSupported(
            destinationEntry,
            authorityName);

        var parentPath = Path.GetDirectoryName(path)
            ?? throw new InvalidDataException(
                $"{authorityName} has no parent directory.");
        EnsureRuntimeDirectoryExistsAndIsSafe(parentPath);
        using var parentAuthority = EnsureStableRuntimeDirectory(parentPath);
        var tempPath = path + ".tmp." + Guid.NewGuid().ToString("N")[..8];
        EnsureRuntimePathIsSafe(tempPath);
        FileStream? stream = null;
        try
        {
            stream = PhysicalFileAuthority.CreateNewWritableFile(
                parentAuthority,
                tempPath,
                authorityName + " temporary",
                asynchronous: true);
            await stream.WriteAsync(content, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            stream.Flush(flushToDisk: true);

            if (_hooks?.AfterRuntimeMutationBoundaryValidatedAsync != null)
            {
                await _hooks.AfterRuntimeMutationBoundaryValidatedAsync(path);
            }

            EnsureRuntimePathIsSafe(tempPath);
            EnsureRuntimePathIsSafe(path);
            if (SupportsReversibleFileReplacement)
            {
                await ReversibleFilePublication.PublishAsync(
                    _basePath,
                    PhysicalPublicationTransactionsRootPath,
                    parentAuthority,
                    tempPath,
                    stream,
                    parentAuthority,
                    path,
                    authorityName,
                    _hooks?.AfterPhysicalFileAuthorityValidatedAsync,
                    _hooks?.BeforePhysicalSourcePublishedAsync,
                    _hooks?.AfterPhysicalFilePublishedAsync,
                    cancellationToken,
                    _hooks
                        ?.BeforePhysicalRollbackAbsenceFinalValidationAsync);
            }
            else
            {
                PhysicalFileAuthority.RenameOpenedObjectRelative(
                    stream.SafeFileHandle,
                    parentAuthority,
                    path,
                    replaceExisting: false,
                    authorityName + " create-only publication");
            }

            EnsureRuntimePathIsSafe(path);
        }
        finally
        {
            if (stream != null)
                await stream.DisposeAsync();
            try
            {
                PhysicalFileAuthority.TryDeleteFile(
                    parentAuthority,
                    tempPath,
                    authorityName + " temporary cleanup");
            }
            catch
            {
                // Retaining an unreachable runtime temp is safer than following a replaced path.
            }
        }
    }

    private sealed record WorkerApplyTransactionJournal
    {
        public int SchemaVersion { get; init; } = 1;
        public string TransactionId { get; init; } = "";
        public bool Committed { get; init; }
        public bool RolledBack { get; init; }
    }

    private sealed record WorkerApplyTransactionManifest
    {
        public int SchemaVersion { get; init; } = 1;
        public string TransactionId { get; init; } = "";
        public IReadOnlyList<WorkerApplyTransactionEntry> Entries { get; init; } = [];
    }

    private sealed record WorkerApplyTransactionEntry
    {
        public string Path { get; init; } = "";
        public bool BaselineExists { get; init; }
        public string? BeforeImage { get; init; }
        public string BeforeSha256 { get; init; } = "";
        public string AppliedSha256 { get; init; } = "";
    }

    internal string GetOrCreateSessionGeneration(CanonicalWriteLease writeLease)
    {
        var snapshot = ReadLocalGenerationSnapshot(writeLease);
        if (snapshot.Binding.Exists) return snapshot.Binding.Id!;
        return CreateTrustedLocalGeneration(writeLease);
    }

    /// <summary>Reads existing logical generation authority without creating or replacing it.</summary>
    internal string? ReadExistingSessionGeneration(CanonicalWriteLease writeLease) =>
        ReadLocalGenerationSnapshot(writeLease).Binding.Id;

    internal bool IsCurrentSessionGeneration(
        CanonicalWriteLease writeLease,
        string? expectedGenerationId)
    {
        EnsureCanonicalWriteLeaseActive(writeLease);
        if (string.IsNullOrWhiteSpace(expectedGenerationId)) return false;
        return string.Equals(ReadLocalGenerationSnapshot(writeLease).Binding.Id,
            expectedGenerationId, StringComparison.Ordinal);
    }

    internal string RotateSessionGeneration(CanonicalWriteLease writeLease)
    {
        return PublishSessionReplacementAsync(writeLease, clearGameState: false)
            .GetAwaiter().GetResult();
    }

    // Original load-transaction generation read; keep its physical open contract
    // separate until the load journal itself is migrated.
    private string ReadSessionGeneration() =>
        ParseSessionGenerationText(ReadRuntimeText(SessionGenerationPath));

    private void WriteSessionGeneration(string generationId)
    {
        var json = JsonSerializer.Serialize(new SessionGenerationDocument(1, generationId));
        WriteRuntimeTextAtomic(SessionGenerationPath, json);
        CanonicalRootAuthorityIdentity.AdvanceSessionGenerationRevision();
    }

    private void DeleteWorkerSessionArtifactsCore()
    {
        DeleteDirectoryTreeWithoutFollowingReparsePoints(ResolvePath("worker_tasks"));
        DeleteDirectoryTreeWithoutFollowingReparsePoints(ResolvePath("worker_proposals"));
        DeleteFileCore("game_state/control/gm_worker_latest_validation_repair_task.json");
        DeleteFileCore("game_state/control/validation_repair_ready.json");
    }

    private static void DeleteDirectoryTreeWithoutFollowingReparsePoints(string path)
    {
        PhysicalFileAuthority.TryDeleteDirectoryTree(
            path,
            "No-follow directory cleanup");
    }

    private static void DeleteEmptyDirectoriesWithoutFollowingReparsePoints(string path)
    {
        if (!Directory.Exists(path))
            return;

        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException($"Refusing to traverse reparse-point directory '{path}'.");

        foreach (var child in Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly))
        {
            var childAttributes = File.GetAttributes(child);
            if ((childAttributes & FileAttributes.ReparsePoint) != 0)
                continue;

            DeleteEmptyDirectoriesWithoutFollowingReparsePoints(child);
        }

        if (!Directory.EnumerateFileSystemEntries(path, "*", SearchOption.TopDirectoryOnly).Any())
        {
            using var parentAuthority = PhysicalFileAuthority.OpenStableDirectory(
                Path.GetDirectoryName(path)
                ?? throw new InvalidDataException(
                    "Empty-directory cleanup target has no parent."),
                "Empty-directory cleanup");
            PhysicalFileAuthority.TryDeleteEmptyDirectory(
                parentAuthority,
                path,
                "Empty-directory cleanup");
        }
    }

    private sealed record SessionGenerationDocument(int SchemaVersion, string GenerationId);

    /// <summary>
    /// Create a backup of a file before modification.
    /// </summary>
    public string? CreateBackup(string relativePath)
    {
        return CreateBackupAsync(relativePath).GetAwaiter().GetResult();
    }

    public async Task<string?> CreateBackupAsync(string relativePath)
    {
        return await WithOwnedBackupLeaseAsync(lease => CreateBackupWithLeaseAsync(lease, relativePath));
    }

    /// <summary>
    /// Creates a backup under the supplied lease, preserving the existing missing-source behavior.
    /// </summary>
    /// <param name="writeLease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <param name="relativePath">
    /// Canonical session-relative member or subtree to operate on.
    /// </param>
    /// <returns>
    /// The backup path, or <see langword="null"/> when the original source is absent.
    /// </returns>
    internal string? CreateBackup(CanonicalWriteLease writeLease, string relativePath)
    {
        using var validationMutation = BeginValidationMutation(writeLease);
        return CreateBackupWithLeaseAsync(writeLease, relativePath).GetAwaiter().GetResult();
    }

    private string? CreateBackupCore(string relativePath)
    {
        var content = ReadFileBytesCoreAsync(relativePath).GetAwaiter().GetResult();
        if (content == null)
            return null;

        var normalizedRelativePath = GetCanonicalRelativePath(ResolvePath(relativePath));
        var backupRelativePath =
            normalizedRelativePath + $".backup.{DateTime.UtcNow.Ticks}";
        WriteFileAtomicBytesCoreAsync(backupRelativePath, content).GetAwaiter().GetResult();
        return ResolvePath(backupRelativePath);
    }

    public void RestoreBackup(string backupFullPath, string originalRelativePath)
    {
        RestoreBackupAsync(backupFullPath, originalRelativePath).GetAwaiter().GetResult();
    }

    public async Task RestoreBackupAsync(string backupFullPath, string originalRelativePath)
    {
        await WithOwnedBackupLeaseAsync(async lease =>
        {
            await RestoreBackupWithLeaseAsync(lease, backupFullPath, originalRelativePath);
            return true;
        });
    }

    /// <summary>
    /// Restores the original canonical member from its backup and completes backup cleanup.
    /// </summary>
    /// <param name="writeLease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <param name="backupFullPath">
    /// Existing backup path resolved by the canonical backup lifecycle.
    /// </param>
    /// <param name="originalRelativePath">
    /// Canonical session-relative member to restore.
    /// </param>
    internal void RestoreBackup(
        CanonicalWriteLease writeLease,
        string backupFullPath,
        string originalRelativePath)
    {
        using var validationMutation = BeginValidationMutation(writeLease);
        RestoreBackupWithLeaseAsync(writeLease, backupFullPath, originalRelativePath).GetAwaiter().GetResult();
    }

    private void RestoreBackupCore(string backupFullPath, string originalRelativePath)
    {
        var backupRelativePath = GetCanonicalRelativePath(backupFullPath);
        var content = ReadFileBytesCoreAsync(backupRelativePath).GetAwaiter().GetResult();
        if (content == null)
        {
            throw new FileNotFoundException(
                "Canonical rollback before-image is missing.",
                backupFullPath);
        }

        WriteFileAtomicBytesCoreAsync(originalRelativePath, content).GetAwaiter().GetResult();
        DeleteFileCore(backupRelativePath);
    }

    public void CleanupBackup(string backupFullPath)
    {
        CleanupBackupAsync(backupFullPath).GetAwaiter().GetResult();
    }

    public async Task CleanupBackupAsync(string backupFullPath)
    {
        await WithOwnedBackupLeaseAsync(async lease =>
        {
            await CleanupBackupWithLeaseAsync(lease, backupFullPath);
            return true;
        });
    }

    /// <summary>
    /// Removes the specified backup through the existing backup lifecycle under the supplied lease.
    /// </summary>
    /// <param name="writeLease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <param name="backupFullPath">
    /// Existing backup path resolved by the canonical backup lifecycle.
    /// </param>
    internal void CleanupBackup(CanonicalWriteLease writeLease, string backupFullPath)
    {
        using var validationMutation = BeginValidationMutation(writeLease);
        CleanupBackupWithLeaseAsync(writeLease, backupFullPath).GetAwaiter().GetResult();
    }

    private void CleanupBackupCore(string backupFullPath)
    {
        var backupRelativePath = GetCanonicalRelativePath(backupFullPath);
        DeleteFileCore(backupRelativePath);
    }

    private string GetCanonicalRelativePath(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            throw new InvalidDataException("Canonical backup path is required.");

        var canonicalRoot = Path.GetFullPath(GameSessionPath).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var normalizedFullPath = Path.GetFullPath(fullPath);
        if (!IsSameOrDescendant(normalizedFullPath, canonicalRoot) ||
            string.Equals(normalizedFullPath, canonicalRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Canonical backup path must stay inside game_session.");
        }

        var relativePath = Path.GetRelativePath(canonicalRoot, normalizedFullPath)
            .Replace('\\', '/');
        var resolvedPath = ResolvePath(relativePath);
        if (!string.Equals(
                resolvedPath,
                normalizedFullPath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Canonical backup path identity is invalid.");
        }

        return relativePath;
    }

    /// <summary>
    /// Get all JSON files in game_state directory.
    /// </summary>
    public string[] GetAllGameStateFiles()
    {
        var gameStatePath = Path.Combine(_basePath, "game_session", "game_state");
        if (!Directory.Exists(gameStatePath))
            return Array.Empty<string>();
        return EnumerateFilesWithoutFollowingReparsePoints(gameStatePath, "*.json").ToArray();
    }

    /// <summary>
    /// Clear all game state for a new game.
    /// </summary>
    public void ClearGameState()
    {
        ClearGameStateAsync().GetAwaiter().GetResult();
    }

    public async Task ClearGameStateAsync()
    {
        await using var lifecycleLease = await AcquireSessionLifecycleLeaseAsync();
        _ = await ClearGameStateAsync(lifecycleLease);
    }

    internal async Task<string> ClearGameStateAsync(SessionLifecycleLease lifecycleLease)
    {
        EnsureValidSessionLifecycleLease(lifecycleLease);
        await using var writeLock = await AcquireSessionReplacementWriteLeaseAsync(lifecycleLease);
        return await PublishSessionReplacementAsync(writeLock, clearGameState: true);
    }

    private static bool ShouldPreserveAcrossGameStateClear(string gameStatePath, string filePath)
    {
        var relative = GetLocalRelativePath(gameStatePath, filePath, OperatingSystem.IsWindows());

        return relative.Equals("control/gm_bridge_status.json", StringComparison.OrdinalIgnoreCase) ||
               relative.Equals("control/gm_cli_window_binding.json", StringComparison.OrdinalIgnoreCase) ||
               relative.Equals("control/gm_context_pack", StringComparison.OrdinalIgnoreCase) ||
               relative.StartsWith("control/gm_context_pack/", StringComparison.OrdinalIgnoreCase);
    }

    public void ClearCurrentWorldLore()
    {
        ClearCurrentWorldLoreAsync().GetAwaiter().GetResult();
    }

    public async Task ClearCurrentWorldLoreAsync()
    {
        var writeLease = await AcquireCanonicalWriteLeaseAsync();
        Exception? publicationUncertainty = null;
        try { ClearCurrentWorldLoreCore(writeLease); }
        catch (CoordinatedStatePublicationUncertainException failure)
        {
            publicationUncertainty = failure;
            throw;
        }
        finally
        {
            await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(
                this, writeLease, completed: false, operationFailure: publicationUncertainty);
        }
    }

    private void ClearCurrentWorldLoreCore(CanonicalWriteLease writeLease)
    {
        EnsureWorkerGeneralMutationAllowed(writeLease);
        VerifyCurrentSessionOperation(writeLease);
        var files = EnumerateLocalTreeFiles(
            new TrustedLocalFileScope([GameSessionPath]),
            ResolvePath("lore/current_world"),
            exclude: path => path.Contains(".rollback.", StringComparison.OrdinalIgnoreCase));
        foreach (var file in files)
        {
            var relativePath = GetLocalRelativePath(GameSessionPath, file, OperatingSystem.IsWindows());
            DeleteFile(writeLease, relativePath);
        }
    }

    internal static IEnumerable<string> EnumerateFilesWithoutFollowingReparsePoints(
        string rootPath,
        string searchPattern)
    {
        if (!Directory.Exists(rootPath) || IsReparsePoint(rootPath))
            yield break;

        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(rootPath);
        while (pendingDirectories.Count > 0)
        {
            var currentDirectory = pendingDirectories.Pop();
            foreach (var file in Directory.EnumerateFiles(
                         currentDirectory,
                         searchPattern,
                         SearchOption.TopDirectoryOnly))
            {
                if (!IsReparsePoint(file))
                    yield return file;
            }

            foreach (var directory in Directory.EnumerateDirectories(
                         currentDirectory,
                         "*",
                         SearchOption.TopDirectoryOnly))
            {
                if (!IsReparsePoint(directory))
                    pendingDirectories.Push(directory);
            }
        }
    }

    internal static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private byte[] ReadExactBytesFromStablePath(string expectedFullPath)
    {
        var normalizedExpectedPath = Path.GetFullPath(expectedFullPath);
        var openedFile = OpenExactPhysicalReadFile(
            normalizedExpectedPath,
            "Recovery evidence")
            ?? throw new FileNotFoundException(
                "Recovery evidence file does not exist.",
                normalizedExpectedPath);
        try
        {
            var stream = openedFile.Stream;
            using var buffer = stream.Length is > 0 and <= int.MaxValue
                ? new MemoryStream((int)stream.Length)
                : new MemoryStream();
            stream.CopyTo(buffer);
            openedFile.Complete();
            return buffer.ToArray();
        }
        catch
        {
            openedFile.Abandon();
            throw;
        }
        finally
        {
            openedFile.DisposeAsync()
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }
    }
}
