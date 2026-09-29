namespace BookOfEternityClient.Services;

internal static partial class AcceptedMechanicsPlanner
{
    internal sealed record MissingSidePreparationResult(
        ResourceExecutionSession.MissingSidePreparation? Preparation,
        IReadOnlyList<ValidationIssue> Issues);

    internal sealed partial class ResourceExecutionSession
    {
        private PendingSpiritualExchange? _missingAuditObservation;
        private AfterlifeSpiritualConflictResourceOutcome.MissingSideExtension? _missingAuditExtension;
        private AfterlifeSpiritualConflictResourceOutcome.ExchangeBatch? _stagedRawProducerBatch;
        private AfterlifeSpiritualConflictResourceOutcome.ExchangeBatch? _activeRawProducerBatch;
        private readonly Dictionary<ResourceOperationKey, ResourceOperationKey> _missingSideAliases = new();
        private int _missingSideAliasVersion;
        private MissingSidePreparation? _preparedMissingSide;

        // Construction alone is not authority. Commit accepts only the exact
        // current object retained in this session, with its original wait/batches.
        internal sealed class MissingSidePreparation
        {
            internal MissingSidePreparation(ResourceExecutionSession owner,
                PendingSpiritualExchange expected,
                AfterlifeSpiritualConflictResourceOutcome.ExchangeBatch originalRaw,
                AfterlifeSpiritualConflictResourceOutcome.ExchangeBatch originalExecution,
                AfterlifeSpiritualConflictResourceOutcome.ExchangeBatch checkedCompletion,
                AfterlifeSpiritualConflictResourceOutcome.MissingSideExtension extension,
                int aliasVersion,
                IReadOnlyDictionary<ResourceOperationKey, ResourceOperationKey> retainedAliases)
            {
                Owner = owner;
                Expected = expected;
                OriginalRaw = originalRaw;
                OriginalExecution = originalExecution;
                CheckedCompletion = checkedCompletion;
                Extension = extension;
                AliasVersion = aliasVersion;
                RetainedAliases = new System.Collections.ObjectModel.ReadOnlyDictionary<ResourceOperationKey, ResourceOperationKey>(
                    retainedAliases.ToDictionary(pair => pair.Key, pair => pair.Value));
            }

            internal ResourceExecutionSession Owner { get; }
            internal PendingSpiritualExchange Expected { get; }
            internal AfterlifeSpiritualConflictResourceOutcome.ExchangeBatch OriginalRaw { get; }
            internal AfterlifeSpiritualConflictResourceOutcome.ExchangeBatch OriginalExecution { get; }
            internal AfterlifeSpiritualConflictResourceOutcome.ExchangeBatch CheckedCompletion { get; }
            internal AfterlifeSpiritualConflictResourceOutcome.MissingSideExtension Extension { get; }
            internal int AliasVersion { get; }
            internal IReadOnlyDictionary<ResourceOperationKey, ResourceOperationKey> RetainedAliases { get; }
            internal IReadOnlyDictionary<ResourceOperationKey, ResourceOperationKey> Aliases => Extension.Aliases;
            internal IReadOnlyList<ResourceMutationSourceExport> CheckedSources => Extension.CheckedSources;
            internal AfterlifeSpiritualConflictResourceOutcome.ExchangeBatch Appended => Extension.Appended;
            internal AfterlifeSpiritualConflictResourceOutcome.ExchangeBatch Merged => Extension.Merged;
        }

        internal void OwnMissingAuditWait(PendingSpiritualExchange observation)
        {
            if (_active == null || _missingAuditObservation != null ||
                observation.ConflictId != _active.ConflictId ||
                observation.ExchangeId != _active.ExchangeId || observation.Ordinal != _active.Ordinal ||
                !observation.MissingAuditSides.SequenceEqual(_active.PendingSides))
                throw new InvalidOperationException("Missing-side wait must belong to the active exchange.");
            _missingAuditObservation = observation;
        }

        internal MissingSidePreparationResult PrepareMissingAuditSide(PendingSpiritualExchange expected,
            AfterlifeSpiritualConflictResourceOutcome.ExchangeBatch checkedCompletion)
        {
            ArgumentNullException.ThrowIfNull(expected);
            ArgumentNullException.ThrowIfNull(checkedCompletion);
            Enter();
            try
            {
                EnsureUsable();
                if (!_live || !_pendingExchange || _active == null || _activeRawProducerBatch == null ||
                    !ReferenceEquals(expected, _missingAuditObservation))
                    return new(null, Issue("resource_missing_side_owner_mismatch",
                        "the exact current missing-side observation", "foreign or stale"));
                var extension = _activeRawProducerBatch.PrepareMissingSide(
                    checkedCompletion, _active, _missingSideAliases);
                if (extension == null)
                    return new(null, Issue("resource_missing_side_prefix_mismatch",
                        "immutable producer context and known sides, plus new evaluated side", "changed or missing"));
                var preflight = _state.ValidateMissingSideAppend(extension.Appended);
                if (preflight.Count != 0)
                    return new(null, preflight);
                var prepared = new MissingSidePreparation(this, expected, _activeRawProducerBatch,
                    _active, checkedCompletion, extension, _missingSideAliasVersion, _missingSideAliases);
                _preparedMissingSide = prepared;
                return new(prepared, Array.Empty<ValidationIssue>());
            }
            finally { Exit(); }
        }

        // The common owner can inspect and accept this exact source-to-alias
        // relation before commit. Preparation never advances or allocates IDs.
        internal ResourceContinuationResult CommitMissingAuditSide(MissingSidePreparation prepared)
        {
            ArgumentNullException.ThrowIfNull(prepared);
            Enter();
            try
            {
                EnsureUsable();
                if (!_live || !_pendingExchange || !ReferenceEquals(prepared.Owner, this) ||
                    !ReferenceEquals(_preparedMissingSide, prepared) ||
                    !ReferenceEquals(_missingAuditObservation, prepared.Expected) ||
                    !ReferenceEquals(_activeRawProducerBatch, prepared.OriginalRaw) ||
                    !ReferenceEquals(_active, prepared.OriginalExecution) ||
                    prepared.AliasVersion != _missingSideAliasVersion)
                    return new(null, Issue("resource_missing_side_preparation_mismatch",
                        "the exact current preparation of this retained wait", "foreign, stale or consumed"));
                var extension = prepared.Extension;
                _preparedMissingSide = null;
                foreach (var pair in extension.Aliases)
                    _missingSideAliases.Add(pair.Key, pair.Value);
                _missingSideAliasVersion++;
                _missingAuditExtension = extension;
                _active = extension.Merged;
                // Merged/remapped execution batches never become producer evidence.
                _activeRawProducerBatch = prepared.CheckedCompletion;
                _missingAuditObservation = null;
                _pendingExchange = false;
                var step = MoveNextOwned();
                return new(step, step.Result?.Issues ?? Array.Empty<ValidationIssue>());
            }
            finally { Exit(); }
        }

        // Resource-fixture convenience. Integrated source admission uses the
        // explicit preparation/commit pair and retains the consumed capability.
        internal ResourceContinuationResult ResumeMissingAuditSide(PendingSpiritualExchange expected,
            AfterlifeSpiritualConflictResourceOutcome.ExchangeBatch checkedCompletion)
        {
            var prepared = PrepareMissingAuditSide(expected, checkedCompletion);
            return prepared.Preparation is { } owned
                ? CommitMissingAuditSide(owned)
                : new(null, prepared.Issues);
        }

        internal AfterlifeSpiritualConflictResourceOutcome.MissingSideExtension TakeMissingAuditExtension()
        {
            var extension = _missingAuditExtension ??
                throw new InvalidOperationException("No checked extension belongs to this wait.");
            _missingAuditExtension = null;
            return extension;
        }
    }

    private sealed partial class ResourceExecutionState
    {
        internal IReadOnlyList<ValidationIssue> ValidateMissingSideAppend(
            AfterlifeSpiritualConflictResourceOutcome.ExchangeBatch appended)
        {
            var exports = graphPreparation.SourceExports;
            if (appended.Sources.Any(s => exports.Any(old => old.SourceKind == s.SourceKind &&
                    (old.SourceId == s.SourceId || ResourceMaterializationContract.BuildConfusableKey(old.SourceId) ==
                        ResourceMaterializationContract.BuildConfusableKey(s.SourceId)))) ||
                appended.Mutations.Any(m => operationIdByKey.ContainsKey(m.Key)))
                return Issue("resource_missing_side_alias_collision",
                    "fresh exact and confusable continuation source identities", "collision");
            var catalog = ResourceMutationSourceCatalog.Create(exports.Concat(appended.Sources));
            return catalog.Issues;
        }
    }
}
