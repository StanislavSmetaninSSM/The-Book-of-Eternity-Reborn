using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal sealed record SpiritualWoundSourceContinuationPreparation(
        SpiritualWoundSourceSession.PreparedContinuation? Ticket,
        IReadOnlyList<ValidationIssue> Issues);

    /// <summary>
    /// Reports the source-owner completion projection or the diagnostics that rejected it.
    /// </summary>
    /// <param name="Projection">
    /// Owner-issued final source projection, or <see langword="null"/> when completion was rejected.
    /// </param>
    /// <param name="Issues">
    /// Validation diagnostics; empty when <paramref name="Projection"/> is available.
    /// </param>
    internal sealed record SpiritualWoundSourceCompletionResult(
        SpiritualWoundSourceSession.PreparedContinuation.CompletionProjection? Projection,
        IReadOnlyList<ValidationIssue> Issues)
    {
        /// <summary>
        /// Gets whether one owner-issued projection is available without diagnostics.
        /// </summary>
        internal bool Success => Projection is not null && Issues.Count == 0;
    }

    internal sealed partial class SpiritualWoundSourceSession
    {
        private long _continuationRevision;
        internal long ContinuationRevision => _continuationRevision;

        // Ticket authority is minted inside this class only after the actual signed
        // read and candidate checks. No factory accepts caller-provided validated data.
        internal sealed partial class PreparedContinuation
        {
            private readonly SpiritualWoundSourceSession _owner;
            private readonly SpiritualWoundSourceSession _prospective;
            private readonly FileSystemManager.CanonicalWriteLease _lease;
            private readonly long _revision;
            private readonly string _baseBinding;
            private readonly string _baseProjectedConflict;
            private readonly PreparedSpiritualSource[] _baseSources;
            private readonly SpiritualSourcePendingRequirement[] _basePending;
            private readonly int[] _baseDice;
            private readonly string[] _baseCoordinates;
            private readonly KeyValuePair<string, SourceFrontier>[] _baseFrontiers;
            private readonly KeyValuePair<string, string>[] _baseCheckedCoordinates;
            private readonly KeyValuePair<string, int?>[] _baseActionPoints;
            private readonly KeyValuePair<string, string>[] _baseMembers;
            private readonly MissingActionCostAuditLeaf? _baseMissingLeaf;
            private readonly IReadOnlyList<ValidationIssue> _issues;
            private readonly object? _routingEpoch;
            private readonly SpiritualOriginalDraftInputs? _coldLayer;
            private readonly SpiritualOriginalTurnCapture? _coldLayerCapture;
            private readonly long _coldLayerRevision;
            private readonly bool _coldLayerAdvancingCapture;
            private bool _used;
            private bool _completionValidated;

            /// <summary>
            /// Retains the final spiritual-conflict image issued by a validated source-completion ticket.
            /// </summary>
            internal sealed class CompletionProjection
            {
                private readonly PreparedContinuation _ticket;
                private readonly SpiritualWoundSourceSession _sourceOwner;
                private readonly FileSystemManager.CanonicalWriteLease _lease;
                private readonly int _closedExchangeCount;
                private readonly AcceptedMechanicsPlanner.ResourceExecutionSession _resources;
                private readonly EffectAcceptedTurnPlanner.EffectAcceptedDraft _effects;
                private readonly JsonObject _conflictAfterImage;
                private EffectAcceptedTurnPlan? _consumedPlan;
                private bool _consumed;

                /// <summary>
                /// Captures the exact owner tuple after this ticket validates the complete source frontier.
                /// </summary>
                /// <param name="ticket">
                /// Owner-issued ticket whose complete frontier has already passed validation.
                /// </param>
                /// <param name="sourceOwner">
                /// Exact current source owner that produced <paramref name="ticket"/>.
                /// </param>
                /// <param name="lease">
                /// Exact canonical lease retained by <paramref name="ticket"/>.
                /// </param>
                /// <param name="closedExchangeCount">
                /// Source exchange count validated for this projection.
                /// </param>
                /// <param name="resources">
                /// Exact resource owner bound to the original capture.
                /// </param>
                /// <param name="effects">
                /// Exact effect draft bound to <paramref name="resources"/> and
                /// <paramref name="sourceOwner"/>.
                /// </param>
                internal CompletionProjection(
                    PreparedContinuation ticket,
                    SpiritualWoundSourceSession sourceOwner,
                    FileSystemManager.CanonicalWriteLease lease,
                    int closedExchangeCount,
                    AcceptedMechanicsPlanner.ResourceExecutionSession resources,
                    EffectAcceptedTurnPlanner.EffectAcceptedDraft effects)
                {
                    ArgumentNullException.ThrowIfNull(ticket);
                    ArgumentNullException.ThrowIfNull(sourceOwner);
                    ArgumentNullException.ThrowIfNull(lease);
                    ArgumentNullException.ThrowIfNull(resources);
                    ArgumentNullException.ThrowIfNull(effects);
                    if (!ticket._completionValidated ||
                        !ReferenceEquals(sourceOwner, ticket._owner) ||
                        !ReferenceEquals(lease, ticket._lease) ||
                        !resources.OwnsOriginalCompletionOwners(effects, sourceOwner))
                    {
                        throw new InvalidOperationException(
                            "A source completion projection requires its validated exact owner tuple.");
                    }
                    _ticket = ticket;
                    _sourceOwner = sourceOwner;
                    _lease = lease;
                    _closedExchangeCount = closedExchangeCount;
                    _resources = resources;
                    _effects = effects;
                    _conflictAfterImage = ticket._prospective._candidateConflict
                        .DeepClone().AsObject();
                }

                /// <summary>
                /// Revalidates and consumes this projection for one exact completed effect plan.
                /// </summary>
                /// <param name="caller">
                /// Exact current source owner retained by the original capture.
                /// </param>
                /// <param name="lease">
                /// Active canonical lease, which must be the lease retained by this projection.
                /// </param>
                /// <param name="closedExchangeCount">
                /// Current number of source exchanges closed by the original capture.
                /// </param>
                /// <param name="resources">
                /// Exact resource owner retained by the original capture.
                /// </param>
                /// <param name="effects">
                /// Exact effect draft bound to <paramref name="resources"/> and <paramref name="caller"/>.
                /// </param>
                /// <param name="completedPlan">
                /// Exact effect plan issued from the final transcript of <paramref name="resources"/>.
                /// </param>
                /// <returns>
                /// An empty collection after one successful consumption, or diagnostics rejecting stale,
                /// foreign, reused or mismatched evidence.
                /// </returns>
                internal IReadOnlyList<ValidationIssue> Consume(
                    SpiritualWoundSourceSession caller,
                    FileSystemManager.CanonicalWriteLease lease,
                    int closedExchangeCount,
                    AcceptedMechanicsPlanner.ResourceExecutionSession resources,
                    EffectAcceptedTurnPlanner.EffectAcceptedDraft effects,
                    EffectAcceptedTurnPlan completedPlan)
                {
                    ArgumentNullException.ThrowIfNull(caller);
                    ArgumentNullException.ThrowIfNull(lease);
                    ArgumentNullException.ThrowIfNull(resources);
                    ArgumentNullException.ThrowIfNull(effects);
                    ArgumentNullException.ThrowIfNull(completedPlan);
                    caller._validator._fs.EnsureCanonicalWriteLeaseActive(lease);
                    if (!caller._continuationGate.Wait(0))
                    {
                        return FailureIssues(
                            "spiritual_source_continuation_busy",
                            "source continuation is already running");
                    }
                    try
                    {
                        if (_consumed)
                        {
                            return FailureIssues(
                                "spiritual_source_completion_projection_used",
                                "source completion projection has already been consumed");
                        }
                        var invalid = _ticket.ValidateCommitContext(caller, lease);
                        if (invalid is not null)
                            return invalid;
                        if (!ReferenceEquals(caller, _sourceOwner) ||
                            !ReferenceEquals(lease, _lease) ||
                            closedExchangeCount != _closedExchangeCount ||
                            !ReferenceEquals(resources, _resources) ||
                            !ReferenceEquals(effects, _effects))
                        {
                            return FailureIssues(
                                "spiritual_original_completion_owner_mismatch",
                                "the exact validated source, lease, frontier, resource and effect owners");
                        }
                        var frontierIssues = _ticket._prospective.ValidateCompletionFrontier(
                            caller,
                            closedExchangeCount);
                        if (frontierIssues.Count != 0)
                            return frontierIssues;
                        if (!resources.OwnsOriginalCompletionOwners(effects, caller) ||
                            !resources.OwnsOriginalEffectCompletion(effects, completedPlan))
                        {
                            return FailureIssues(
                                "spiritual_original_completion_owner_mismatch",
                                "the bound effect draft's exact completion from the final resource transcript");
                        }
                        _consumed = true;
                        _consumedPlan = completedPlan;
                        return Array.Empty<ValidationIssue>();
                    }
                    finally
                    {
                        caller._continuationGate.Release();
                    }
                }

                /// <summary>
                /// Checks whether this projection was consumed by one exact reduction owner tuple.
                /// </summary>
                /// <param name="resources">
                /// Expected resource owner.
                /// </param>
                /// <param name="effects">
                /// Expected effect draft.
                /// </param>
                /// <param name="sourceOwner">
                /// Expected spiritual source owner.
                /// </param>
                /// <param name="completedPlan">
                /// Expected completed effect plan.
                /// </param>
                /// <returns>
                /// <see langword="true"/> only after this projection consumed the four exact objects
                /// and their source owner remains current and healthy;
                /// otherwise, <see langword="false"/>.
                /// </returns>
                internal bool OwnsConsumption(
                    AcceptedMechanicsPlanner.ResourceExecutionSession resources,
                    EffectAcceptedTurnPlanner.EffectAcceptedDraft effects,
                    SpiritualWoundSourceSession sourceOwner,
                    EffectAcceptedTurnPlan completedPlan) =>
                    _consumed && ReferenceEquals(resources, _resources) &&
                    ReferenceEquals(effects, _effects) &&
                    ReferenceEquals(sourceOwner, _sourceOwner) &&
                    sourceOwner.IsCurrentOwner &&
                    ReferenceEquals(completedPlan, _consumedPlan);

                /// <summary>
                /// Gets a detached copy of the consumed final spiritual-conflict image.
                /// </summary>
                internal JsonObject ConflictAfterImage
                {
                    get
                    {
                        if (!_consumed)
                            throw new InvalidOperationException(
                                "Consume the source completion projection before reading its image.");
                        return _conflictAfterImage.DeepClone().AsObject();
                    }
                }
            }

            /// <summary>
            /// Binds a prospective source result to the retained owner, lease, and selected cold layer.
            /// </summary>
            /// <param name="owner">
            /// Exact source session whose accepted prefix is preserved until commit.
            /// </param>
            /// <param name="prospective">
            /// Private candidate source state to commit after all checks pass.
            /// </param>
            /// <param name="lease">
            /// Active canonical write lease used by preparation and commit.
            /// </param>
            /// <param name="issues">
            /// Diagnostics produced while preparing the prospective state.
            /// </param>
            /// <param name="coldLayerCapture">
            /// Cold capture that selected the layer, or <see langword="null"/> for a warm source.
            /// </param>
            /// <param name="coldLayer">
            /// Selected cold inputs, or <see langword="null"/> for a warm source.
            /// </param>
            /// <param name="coldLayerRevision">
            /// Committed layer revision observed during preparation.
            /// </param>
            /// <param name="coldLayerAdvancingCapture">
            /// Whether the cold capture requested its next resource exchange.
            /// </param>
            private PreparedContinuation(SpiritualWoundSourceSession owner,
                SpiritualWoundSourceSession prospective, FileSystemManager.CanonicalWriteLease lease,
                IReadOnlyList<ValidationIssue> issues,
                SpiritualOriginalTurnCapture? coldLayerCapture,
                SpiritualOriginalDraftInputs? coldLayer, long coldLayerRevision,
                bool coldLayerAdvancingCapture)
            {
                _owner = owner;
                _prospective = prospective;
                _lease = lease;
                _revision = owner._continuationRevision;
                _routingEpoch = owner._prefixCapture?.ReadMechanicsEpoch(owner);
                _coldLayerCapture = coldLayerCapture;
                _coldLayer = coldLayer;
                _coldLayerRevision = coldLayerRevision;
                _coldLayerAdvancingCapture = coldLayerAdvancingCapture;
                _baseBinding = owner.BuildInputBinding().ToJsonString();
                _baseProjectedConflict = owner._candidateConflict.ToJsonString();
                _baseSources = owner._sources.ToArray();
                _basePending = owner._pending.ToArray();
                _baseDice = owner._claimedDice.Order().ToArray();
                _baseCoordinates = owner._coordinates.Order(StringComparer.Ordinal).ToArray();
                _baseFrontiers = owner._frontiers.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
                _baseCheckedCoordinates = owner._checkedByCoordinate.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
                _baseActionPoints = owner._expectedActionPoints.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
                _baseMembers = owner._members.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
                _baseMissingLeaf = owner._missingActionCostAudit;
                _issues = Array.AsReadOnly(issues.ToArray());
            }

            /// <summary>
            /// Revalidates retained input and optionally prepares the actual capture's next source frontier.
            /// </summary>
            /// <param name="owner">
            /// Current source owner whose accepted evidence is preserved until commit.
            /// </param>
            /// <param name="lease">
            /// Active lease for signed and candidate input reads.
            /// </param>
            /// <param name="advancingCapture">
            /// Exact writable capture requesting its next exchange; null retains the existing frontier in the capture path.
            /// </param>
            /// <returns>
            /// A private uncommitted ticket or issues rejecting the proposed continuation.
            /// </returns>
            internal static async Task<SpiritualWoundSourceContinuationPreparation> PrepareAsync(
                SpiritualWoundSourceSession owner, FileSystemManager.CanonicalWriteLease lease,
                SpiritualOriginalTurnCapture? advancingCapture = null)
            {
                owner._validator._fs.EnsureCanonicalWriteLeaseActive(lease);
                await owner._continuationGate.WaitAsync();
                try
                {
                    return await PrepareCoreAsync(owner, lease, advancingCapture);
                }
                finally
                {
                    owner._continuationGate.Release();
                }
            }

            internal static async Task<SpiritualWoundSourcePreparation> ContinueAsync(
                SpiritualWoundSourceSession owner, FileSystemManager.CanonicalWriteLease lease)
            {
                owner._validator._fs.EnsureCanonicalWriteLeaseActive(lease);
                await owner._continuationGate.WaitAsync();
                try
                {
                    var prepared = await PrepareCoreAsync(owner, lease);
                    return prepared.Ticket is null
                        ? new(null, prepared.Issues)
                        : prepared.Ticket.CommitCore(owner, lease);
                }
                finally
                {
                    owner._continuationGate.Release();
                }
            }

            /// <summary>
            /// Reads and checks a prospective source view while the source continuation gate is held.
            /// </summary>
            /// <param name="owner">
            /// Exact source owner providing the signed original and accepted prefix.
            /// </param>
            /// <param name="lease">
            /// Active canonical lease retained by any resulting ticket.
            /// </param>
            /// <param name="advancingCapture">
            /// Capture whose next ordinal determines the limit, or null to leave it unchanged.
            /// </param>
            /// <returns>
            /// A validated prospective ticket without mutating the owner's accepted source evidence, or rejection issues.
            /// </returns>
            private static async Task<SpiritualWoundSourceContinuationPreparation> PrepareCoreAsync(
                SpiritualWoundSourceSession owner, FileSystemManager.CanonicalWriteLease lease,
                SpiritualOriginalTurnCapture? advancingCapture = null)
            {
                owner._validator._fs.EnsureCanonicalWriteLeaseActive(lease);
                if (!owner.IsCurrentOwner)
                    return PreparationFailure("spiritual_source_session_revoked", "current source owner required");
                if (owner._awaitingOriginalPrefix)
                    return PreparationFailure("spiritual_source_prefix_pending", "complete the actual original resource prefix first");
                var read = PendingTurnSnapshotReader.ReadCurrent(owner._validator._fs, lease,
                    PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(RequiredPaths, OptionalPaths));
                if (!read.Success || read.Snapshot is null)
                {
                    owner.RevokeCurrent();
                    return new(null, read.Issues);
                }
                var request = await owner._validator._fs.ReadFileBytesAsync(lease, "input/turn_request.json");
                if (!owner.SameOriginal(read.Snapshot) || !SameBytes(owner._originalRequestBytes, request))
                {
                    owner.RevokeCurrent();
                    return PreparationFailure("spiritual_source_continuation_origin_changed",
                        "same signed original and exact retained current request required");
                }
                var issues = new List<ValidationIssue>();
                try
                {
                    var coldInputs = owner._coldOriginalInputs;
                    var coldCapture = coldInputs is null ? null : advancingCapture ?? owner._prefixCapture;
                    var coldRevision = 0L;
                    if (coldCapture != null &&
                        !coldCapture.TryReadColdSourceLayer(owner, advancingCapture != null,
                            out coldInputs, out coldRevision))
                        return PreparationFailure("spiritual_cold_continuation_layer_stale",
                            "the capture-owned selected cold layer");
                    var candidate = new Dictionary<string, string?>(StringComparer.Ordinal);
                    foreach (var path in owner.SelectedPaths)
                    {
                        var bytes = coldInputs is not null
                            ? coldInputs.ReadImage(path).Bytes
                            : await owner._validator._fs.ReadFileBytesAsync(lease, path);
                        candidate[path] = bytes is null ? null : DecodeSourceUtf8(bytes);
                    }
                    var limit = owner._acceptedExchangeLimit;
                    if (advancingCapture != null)
                    {
                        if (!ReferenceEquals(advancingCapture, owner._prefixCapture) ||
                            !advancingCapture.TryReadNextSourceFrontier(owner, out var nextLimit))
                            return PreparationFailure("spiritual_source_frontier_owner_mismatch", "actual advancing capture required");
                        limit = nextLimit;
                    }
                    owner.ValidateRetainedCandidateCoordinates(candidate);
                    var prospective = owner.CreateProspective(candidate, limit);
                    prospective._coldOriginalInputs = coldInputs;
                    if (advancingCapture != null)
                    {
                        prospective._mechanicsContext = advancingCapture.PrepareSourceMechanics(owner, issues);
                        if (prospective._mechanicsContext == null || issues.Count != 0)
                            return new(null, issues.AsReadOnly());
                    }
                    prospective.EvaluateContinuation(owner, issues);
                    if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
                        return new(null, issues.AsReadOnly());
                    owner._validator._fs.EnsureCanonicalWriteLeaseActive(lease);
                    if (!owner.IsCurrentOwner)
                        return PreparationFailure("spiritual_source_session_revoked", "source owner changed during capture");
                    // The original owner has not been modified. Prospective source
                    // objects and dice are private until CommitCore succeeds.
                    return new(new PreparedContinuation(owner, prospective, lease, issues,
                        coldCapture, coldInputs, coldRevision, advancingCapture != null), issues.AsReadOnly());
                }
                catch (Exception exception) when (exception is System.Text.Json.JsonException or
                    DecoderFallbackException or InvalidOperationException or ArgumentException or OverflowException)
                {
                    issues.Add(SourceIssue(AfterlifeSpiritualConflictState.StatePath,
                        "spiritual_source_continuation_invalid", exception.Message));
                    return new(null, issues.AsReadOnly());
                }
            }

            internal SpiritualWoundSourcePreparation Commit(SpiritualWoundSourceSession caller,
                FileSystemManager.CanonicalWriteLease lease)
            {
                // Throw for a disposed/wrong-filesystem lease before touching the ticket.
                caller._validator._fs.EnsureCanonicalWriteLeaseActive(lease);
                if (!caller._continuationGate.Wait(0))
                    return CommitFailure("spiritual_source_continuation_busy", "source continuation is already running");
                try
                {
                    return CommitCore(caller, lease);
                }
                finally
                {
                    caller._continuationGate.Release();
                }
            }

            internal AfterlifeSpiritualConflictResourceOutcome.BuildResult BuildResourceBatches(
                SpiritualWoundSourceSession caller, FileSystemManager.CanonicalWriteLease lease,
                ResourceOwnerAuthority owners, ResourceStateLedger state)
            {
                caller._validator._fs.EnsureCanonicalWriteLeaseActive(lease);
                if (!caller._continuationGate.Wait(0))
                    return new(null, FailureIssues("spiritual_source_continuation_busy", "source continuation is already running"));
                try
                {
                    var invalid = ValidateCommitContext(caller, lease);
                    if (invalid is not null)
                        return new(null, invalid);
                    if (caller._resourcePrefix != null && !ReferenceEquals(state, caller._resourcePrefix.State))
                        return new(null, FailureIssues("spiritual_source_prefix_mismatch", "exact retained prefix ledger required"));
                    var frontier = _prospective._mechanicsContext;
                    var result = AfterlifeSpiritualConflictResourceOutcome.TryCreate(caller.TurnNumber,
                        _prospective._originalConflict.DeepClone().AsObject(),
                        _prospective._candidateConflict.DeepClone().AsObject(), owners, state,
                        frontier, frontier == null ? null : _prospective._resourceBatches
                            .Where(pair => pair.Key < frontier.Ordinal).Select(pair => pair.Value).ToArray(),
                        _prospective.TerminalPreparation);
                    if (result.IsValid && frontier != null)
                        foreach (var batch in result.Exchanges)
                            _prospective._resourceBatches[batch.Ordinal] = batch;
                    return result;
                }
                finally
                {
                    caller._continuationGate.Release();
                }
            }

            /// <summary>
            /// Validates this fresh uncommitted source view as the complete frontier of the
            /// owning original capture without advancing or committing source evidence.
            /// </summary>
            /// <param name="caller">
            /// Exact current source owner that produced this ticket.
            /// </param>
            /// <param name="lease">
            /// Active canonical lease retained by this ticket.
            /// </param>
            /// <param name="closedExchangeCount">
            /// Number of source exchanges already closed by the owning original capture.
            /// </param>
            /// <param name="resources">
            /// Exact resource owner bound to the owning original capture.
            /// </param>
            /// <param name="effects">
            /// Exact effect draft bound to <paramref name="resources"/> and <paramref name="caller"/>.
            /// </param>
            /// <returns>
            /// The owner-issued final source projection, or diagnostics when the fresh candidate
            /// contains unexecuted or unauthenticated source work.
            /// </returns>
            internal SpiritualWoundSourceCompletionResult ValidateCompletion(
                SpiritualWoundSourceSession caller, FileSystemManager.CanonicalWriteLease lease,
                int closedExchangeCount,
                AcceptedMechanicsPlanner.ResourceExecutionSession resources,
                EffectAcceptedTurnPlanner.EffectAcceptedDraft effects)
            {
                ArgumentNullException.ThrowIfNull(caller);
                ArgumentNullException.ThrowIfNull(lease);
                ArgumentNullException.ThrowIfNull(resources);
                ArgumentNullException.ThrowIfNull(effects);
                caller._validator._fs.EnsureCanonicalWriteLeaseActive(lease);
                if (!caller._continuationGate.Wait(0))
                    return new(null, FailureIssues("spiritual_source_continuation_busy",
                        "source continuation is already running"));
                try
                {
                    var issues = ValidateCompletionCore(caller, lease, closedExchangeCount, resources, effects);
                    _completionValidated = issues.Count == 0;
                    return issues.Count == 0
                        ? new SpiritualWoundSourceCompletionResult(
                            new CompletionProjection(
                                this,
                                caller,
                                lease,
                                closedExchangeCount,
                                resources,
                                effects),
                            Array.Empty<ValidationIssue>())
                        : new SpiritualWoundSourceCompletionResult(null, issues);
                }
                finally
                {
                    caller._continuationGate.Release();
                }
            }

            private SpiritualWoundSourcePreparation CommitCore(SpiritualWoundSourceSession caller,
                FileSystemManager.CanonicalWriteLease lease)
            {
                caller._validator._fs.EnsureCanonicalWriteLeaseActive(lease);
                var invalid = ValidateCommitContext(caller, lease);
                if (invalid is not null)
                    return new(null, invalid);
                if (caller._continuationRevision == long.MaxValue)
                    return CommitFailure("spiritual_source_continuation_revision_exhausted", "source revision exhausted");
                _used = true;
                caller.CommitContinuation(_prospective);
                caller._continuationRevision++;
                return new(caller, _issues);
            }

            private IReadOnlyList<ValidationIssue>? ValidateCommitContext(
                SpiritualWoundSourceSession caller, FileSystemManager.CanonicalWriteLease lease)
            {
                if (!ReferenceEquals(caller, _owner))
                    return FailureIssues("spiritual_source_continuation_ticket_foreign", "ticket belongs to another exact source owner");
                if (!caller.IsCurrentOwner)
                    return FailureIssues("spiritual_source_session_revoked", "current source owner required");
                if (!ReferenceEquals(lease, _lease))
                    return FailureIssues("spiritual_source_continuation_ticket_lease", "ticket requires its original active lease");
                if (_used)
                    return FailureIssues("spiritual_source_continuation_ticket_used", "ticket has already committed");
                if (!ReferenceEquals(_routingEpoch, caller._prefixCapture?.ReadMechanicsEpoch(caller)))
                    return FailureIssues("spiritual_source_continuation_ticket_stale", "retained resource routing epoch changed");
                if (_coldLayerCapture != null && _coldLayer != null &&
                    !_coldLayerCapture.OwnsColdSourceLayer(caller, _coldLayerAdvancingCapture,
                        _coldLayer, _coldLayerRevision))
                    return FailureIssues("spiritual_cold_continuation_layer_stale",
                        "the exact selected cold layer and revision");
                if (caller._continuationRevision != _revision || !MatchesRetainedPrefix(caller))
                    return FailureIssues("spiritual_source_continuation_ticket_stale", "retained source revision or prefix changed");
                return null;
            }

            private bool MatchesRetainedPrefix(SpiritualWoundSourceSession owner) =>
                owner.BuildInputBinding().ToJsonString() == _baseBinding &&
                owner._candidateConflict.ToJsonString() == _baseProjectedConflict &&
                owner._sources.Count == _baseSources.Length &&
                owner._sources.Select((source, index) => ReferenceEquals(source, _baseSources[index])).All(value => value) &&
                owner._pending.Count == _basePending.Length &&
                owner._pending.Select((pending, index) => ReferenceEquals(pending, _basePending[index])).All(value => value) &&
                owner._claimedDice.Order().SequenceEqual(_baseDice) &&
                owner._coordinates.Order(StringComparer.Ordinal).SequenceEqual(_baseCoordinates, StringComparer.Ordinal) &&
                owner._frontiers.OrderBy(pair => pair.Key, StringComparer.Ordinal).SequenceEqual(_baseFrontiers) &&
                owner._checkedByCoordinate.OrderBy(pair => pair.Key, StringComparer.Ordinal).SequenceEqual(_baseCheckedCoordinates) &&
                owner._expectedActionPoints.OrderBy(pair => pair.Key, StringComparer.Ordinal).SequenceEqual(_baseActionPoints) &&
                owner._members.OrderBy(pair => pair.Key, StringComparer.Ordinal).SequenceEqual(_baseMembers) &&
                ReferenceEquals(owner._missingActionCostAudit, _baseMissingLeaf);

            private static SpiritualWoundSourceContinuationPreparation PreparationFailure(string code, string detail) =>
                new(null, FailureIssues(code, detail));

            private static SpiritualWoundSourcePreparation CommitFailure(string code, string detail) =>
                new(null, FailureIssues(code, detail));

            private static IReadOnlyList<ValidationIssue> FailureIssues(string code, string detail) =>
                new[] { SourceIssue(AfterlifeSpiritualConflictState.StatePath, code, detail) };
        }
    }
}
