using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>Actual shared batch-reader entries only; excludes current-seed validation, physical IO estimates and permission.</summary>
    internal sealed record SpiritualPressureBatchReaderCounts(int CatalogBuildAttempts, int IdentityParseAttempts,
        int HistoryParseAttempts, int HistoryAgreementAttempts, int ParentResolutionAttempts);

    /// <summary>Separates current/signed shared batch-reader work and real prospective resource-batch computations; not whole-query totals.</summary>
    internal sealed record SpiritualPressureGroupComputationCounts(SpiritualPressureBatchReaderCounts CurrentBatch,
        SpiritualPressureBatchReaderCounts SignedBatch, int ResourceBatchComputations);
    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Checks exact current source and resource ownership after the original resource prefix has closed.
        /// </summary>
        /// <param name="resources">
        /// Executor whose identity must match this capture.
        /// </param>
        /// <param name="source">
        /// Source session whose identity must match this capture.
        /// </param>
        /// <returns>
        /// True when both actual owners still belong to this current capture; otherwise false.
        /// </returns>
        internal bool OwnsMechanicsOwner(AcceptedMechanicsPlanner.ResourceExecutionSession resources,
            SpiritualWoundSourceSession source) =>
            !_disposed && ReferenceEquals(_validator._spiritualOriginalTurnCapture, this) &&
            ReferenceEquals(_resources, resources) && ReferenceEquals(_source, source) &&
            _originalPrefix != null && OwnsOriginalPrefix(source, _originalPrefix);

        /// <summary>Reads computation counts only, never a retained permission or mutable source.</summary>
        internal (int Queries, int SignedParentReads, int OrdinaryEvaluations, int ActualEvaluations,
            int FreshInputChecks, int FreshDependencyChecks) ReadExistingStrainReuseCounts() =>
            (_source.ExistingStrainComparisonQueries, _source.ExistingStrainSignedParentReads,
                _source.OrdinaryEvaluations, _source.ActualEvaluations,
                _source.OrdinaryFreshInputChecks, _source.OrdinaryFreshDependencyChecks);

        /// <summary>Reads detached shared batch-reader and resource-batch entries; current-seed validation is separate and unmeasured.</summary>
        internal SpiritualPressureGroupComputationCounts ReadExistingPressureGroupComputationCounts() =>
            _source.ReadExistingPressureGroupComputationCounts();

        /// <summary>
        /// Reads the installed routing identity for source ticket freshness checks.
        /// </summary>
        /// <param name="source">
        /// Exact source owner requesting its current epoch.
        /// </param>
        /// <returns>
        /// The retained epoch object, or <see langword="null"/> when ownership does not match.
        /// </returns>
        internal object? ReadMechanicsEpoch(SpiritualWoundSourceSession source) =>
            _resources != null && OwnsMechanicsOwner(_resources, source) ? _resources.Routing?.RoutingEpoch : null;

        /// <summary>
        /// Requests the actual resource owner's current mechanics context for source validation.
        /// </summary>
        /// <param name="source">
        /// Signed source owner to authenticate against this capture.
        /// </param>
        /// <param name="issues">
        /// Receives ownership and mechanics validation failures.
        /// </param>
        /// <returns>
        /// An owned context, or <see langword="null"/> when the executor is absent or rejects the request.
        /// </returns>
        internal AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext? PrepareSourceMechanics(
            SpiritualWoundSourceSession source, List<ValidationIssue> issues) =>
            _resources?.CaptureSpiritualMechanics(this, source, issues);
    }

    internal sealed partial class SpiritualWoundSourceSession
    {
        private AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext? _mechanicsContext;
        private readonly Dictionary<string, AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext>
            _exchangeMechanics = new(StringComparer.Ordinal);

        internal int OrdinaryEvaluations => _ordinaryEvaluations;
        internal int ActualEvaluations => _actualEvaluations;
        internal int OrdinaryFreshInputChecks => _ordinaryFreshInputChecks;
        internal int OrdinaryFreshDependencyChecks => _ordinaryFreshDependencyChecks;

        private readonly SpiritualWoundCurrentData.ReadDiagnostics _ordinaryCurrentViewDiagnostics = new();
        private readonly SpiritualWoundCurrentData.ReadDiagnostics _ordinarySignedViewDiagnostics = new();
        private int _prospectiveResourceBatchComputations;

        /// <summary>Passes observational counters to the actual shared current-view source entries.</summary>
        internal SpiritualWoundCurrentData.ReadDiagnostics OrdinaryCurrentViewDiagnostics => _ordinaryCurrentViewDiagnostics;

        /// <summary>Freezes shared current/signed batch-reader entries and real resource-batch entries, excluding other seed/provenance work.</summary>
        internal SpiritualPressureGroupComputationCounts ReadExistingPressureGroupComputationCounts() => new(
            ReadViewComputationCounts(_ordinaryCurrentViewDiagnostics), ReadViewComputationCounts(_ordinarySignedViewDiagnostics),
            _prospectiveResourceBatchComputations);

        private static SpiritualPressureBatchReaderCounts ReadViewComputationCounts(
            SpiritualWoundCurrentData.ReadDiagnostics value) => new(value.CatalogBuildAttempts, value.IdentityParseAttempts,
                value.HistoryParseAttempts, value.HistoryAgreementAttempts, value.ParentResolutionAttempts);

        private int _existingStrainSignedParentReads;
        private int _existingStrainComparisonQueries;

        /// <summary>Counts requested shared signed-parent computations, including refused requests, never physical IO or reached lookups.</summary>
        internal int ExistingStrainSignedParentReads => _existingStrainSignedParentReads;
        /// <summary>Counts current-owner comparison queries so reuse can be checked independently.</summary>
        internal int ExistingStrainComparisonQueries => _existingStrainComparisonQueries;
        /// <summary>Records a query only after the existing current source/capture/actor checks succeeded.</summary>
        internal void RecordExistingStrainComparisonQuery() => _existingStrainComparisonQueries++;

        /// <summary>Reads an existing signed parent only for the actual current capture and original player actor.</summary>
        internal WoundMaterializationEnvelope ReadSignedStrainParent(SpiritualOriginalTurnCapture capture,
            JsonObject conflict, string woundId, string actor)
        {
            if (!OwnsSignedStrainActor(capture, conflict, actor))
                throw new InvalidOperationException("Exact signed parent owner and conflict required.");
            _existingStrainSignedParentReads++;
            return ReadRetraumaWound(woundId, actor, null);
        }

        /// <summary>Resolves the complete requested signed set through one authenticated batch view, without issuing authority.</summary>
        internal IReadOnlyList<WoundMaterializationEnvelope> ReadSignedStrainParents(SpiritualOriginalTurnCapture capture,
            JsonObject conflict, IReadOnlyList<string> woundIds, string actor)
        {
            if (!OwnsSignedStrainActor(capture, conflict, actor))
                throw new InvalidOperationException("Exact signed parent owner and conflict required.");
            // Preserve the old counter's logical requested-parent meaning, separately from reached lookup entries.
            _existingStrainSignedParentReads += woundIds.Count;
            return SpiritualWoundCurrentData.ReadActiveBatch(new(
                    OptionalRoot(WoundCarrierCatalog.PlayerPath), OptionalRoot(WoundCarrierCatalog.NpcPath),
                    OptionalRoot(WoundCarrierCatalog.EnemiesPath), OptionalRoot(WoundCarrierCatalog.AlliesPath),
                    OptionalRoot(WoundCarrierCatalog.AfterlifeProfilesPath)),
                _before[WoundIdentityState.StatePath], _before[WoundHistoryState.HistoryPath], Realm, woundIds, actor,
                _ordinarySignedViewDiagnostics);
        }

        /// <summary>Reads the signed guardian parent with full History and signed creation-receipt coverage.</summary>
        /// <param name="capture">Exact current capture owning this source and original prefix.</param>
        /// <param name="conflict">Current conflict whose guardian lead must equal the signed lead.</param>
        /// <param name="woundIds">Complete selected singleton, never a caller-selected subset.</param>
        /// <param name="actor">Exact guardian coordinate.</param>
        /// <returns>Detached fully joined parent DATA; current authority is still checked by the caller.</returns>
        internal IReadOnlyList<WoundMaterializationEnvelope> ReadSignedGuardianStrainParents(SpiritualOriginalTurnCapture capture,
            JsonObject conflict, IReadOnlyList<string> woundIds, string actor)
        {
            if (!OwnsSignedGuardianStrainActor(capture, conflict, actor) || woundIds.Count != 1)
                throw new InvalidOperationException("Exact signed guardian singleton owner and conflict required.");
            _existingStrainSignedParentReads += woundIds.Count;
            var parents = SpiritualWoundCurrentData.ReadActiveBatchWithHistory(new(
                    OptionalRoot(WoundCarrierCatalog.PlayerPath), OptionalRoot(WoundCarrierCatalog.NpcPath),
                    OptionalRoot(WoundCarrierCatalog.EnemiesPath), OptionalRoot(WoundCarrierCatalog.AlliesPath),
                    OptionalRoot(WoundCarrierCatalog.AfterlifeProfilesPath)),
                _before[WoundIdentityState.StatePath], _before[WoundHistoryState.HistoryPath], Realm, woundIds, actor,
                _ordinarySignedViewDiagnostics, out var history);
            var receipts = SpiritualWoundOpportunityReceiptState.Parse(_before[SpiritualWoundOpportunityReceiptState.StatePath],
                SpiritualWoundOpportunityReceiptState.StatePath);
            if (!receipts.IsValid || receipts.State is null || !receipts.State.HasExactStrainHistory(history))
                throw new InvalidOperationException("Exact signed guardian creation receipt and History required.");
            foreach (var parent in parents)
            {
                var creations = history.ResolveWoundTransitions(parent.WoundId).Where(row =>
                    row.Kind == "create" && row.WoundTransitionOrdinal == 1).ToArray();
                if (creations.Length != 1 ||
                    !receipts.State.CoversOriginalCreation(parent.WoundId, parent.Origin.OpportunityId, creations[0]))
                    throw new InvalidOperationException("Signed guardian parent has no exact creation receipt.");
            }
            return parents;
        }

        /// <summary>Separately authenticates guardian ownership without widening the player-only strain reader.</summary>
        internal bool OwnsSignedGuardianStrainActor(SpiritualOriginalTurnCapture capture, JsonObject conflict, string actor) =>
            IsCurrentOwner && Realm == "chaos_sea" && ReferenceEquals(capture, _prefixCapture) &&
            actor.StartsWith("guardian:", StringComparison.Ordinal) &&
            _originalConflict["activeConflict"] is JsonObject signed &&
            SpiritualWoundFailedAction.HasDirectDuelModel(signed) && SpiritualWoundFailedAction.HasDirectDuelModel(conflict) &&
            signed["conflictId"]?.GetValue<string>() == conflict["conflictId"]?.GetValue<string>() &&
            ActorKey(signed["playerSide"]?["leadContestant"] as JsonObject) == "player_soul:player_soul" &&
            ActorKey(conflict["playerSide"]?["leadContestant"] as JsonObject) == "player_soul:player_soul" &&
            ActorKey(signed["oppositionSide"]?["leadContestant"] as JsonObject) == actor &&
            ActorKey(conflict["oppositionSide"]?["leadContestant"] as JsonObject) == actor;

        /// <summary>Checks fresh source/capture identity and signed conflict/actor without reparsing parent DATA.</summary>
        internal bool OwnsSignedStrainActor(SpiritualOriginalTurnCapture capture, JsonObject conflict, string actor) =>
            IsCurrentOwner && ReferenceEquals(capture, _prefixCapture) &&
            _originalConflict["activeConflict"] is JsonObject signed &&
            signed["conflictId"]?.GetValue<string>() == conflict["conflictId"]?.GetValue<string>() &&
            ActorKey(signed["playerSide"]?["leadContestant"] as JsonObject) == actor &&
            ActorKey(conflict["playerSide"]?["leadContestant"] as JsonObject) == actor;

        /// <summary>Resolves the retained raw candidate path through the actual source inventory, not a local ordinal guess.</summary>
        internal string? ReadSignedStrainExchangePointer(SpiritualOriginalTurnCapture capture, JsonObject conflict,
            JsonObject exchange, int ordinal)
        {
            var actor = ActorKey(conflict["playerSide"]?["leadContestant"] as JsonObject);
            if (actor is null || !OwnsSignedStrainActor(capture, conflict, actor) ||
                !TryReadInitialActiveExchangeInventory(out var conflictId, out var ids) ||
                conflictId != conflict["conflictId"]?.GetValue<string>() || ordinal < 0 || ordinal >= ids.Length ||
                ids[ordinal] != exchange["exchangeId"]?.GetValue<string>() ||
                _originalConflict["activeConflict"]?["exchangeLog"] is not JsonArray prior ||
                _candidate[AfterlifeSpiritualConflictState.StatePath] is not { } json) return null;
            var raw = SpiritualWoundDependentDraftPolicy.ReadStrictRoot(json);
            var resolved = AfterlifeSpiritualConflictState.ResolveRawExchange(_originalConflict, raw, prior.Count + ordinal);
            return resolved is { } row && row.Exchange["exchangeId"]?.GetValue<string>() == ids[ordinal]
                ? row.Pointer : null;
        }

        /// <summary>Reports retained existing-parent DATA that must join the completed owner receipt.</summary>
        internal bool HasExistingStrainSources => _sources.Any(source => source.StrainPreimageJson is { } json &&
            SpiritualWoundStateJson.Integer(SpiritualWoundStateJson.Parse(json)["version"]) is 2 or 3 or 4 or 5);

        /// <summary>
        /// Reads detached signed conflict membership for the exact owning capture.
        /// </summary>
        /// <param name="capture">
        /// Actual current capture bound to this source's original resource prefix.
        /// </param>
        /// <returns>
        /// A detached original conflict root; foreign or revoked ownership throws.
        /// </returns>
        internal JsonObject ReadMechanicsConflict(SpiritualOriginalTurnCapture capture)
        {
            if (!IsCurrentOwner || !ReferenceEquals(capture, _prefixCapture))
                throw new InvalidOperationException("Exact signed source and original capture required.");
            return _originalConflict.DeepClone().AsObject();
        }
    }
}
