using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class EffectAcceptedTurnPlanner
{
    internal sealed class AcceptedBoundaryCompletionProof
    {
        internal AcceptedBoundaryCompletionProof()
        {
        }
    }

    private static readonly AcceptedBoundaryCompletionProof
        AcceptedBoundaryCompletionAuthority =
            new();

    internal static bool IsAcceptedBoundaryCompletionProof(
        AcceptedBoundaryCompletionProof? proof) =>
        ReferenceEquals(proof, AcceptedBoundaryCompletionAuthority);
    private static readonly HashSet<string> RootFields = Set(
        "effectChanges",
        "effectResolutionReceipts",
        EffectAcceptedEventReportCatalog.ResponseField);
    private static readonly HashSet<string> ApplyFields = Set(
        "operation", "target", "source", "parameters", "eventRef", "reason");
    private static readonly HashSet<string> TerminalFields = Set(
        "operation", "effectId", "target", "authority", "eventRef", "reason");
    private static readonly HashSet<string> AuthorityFields = Set("kind", "authorityId");
    private static readonly HashSet<string> ClientOwnedOrLegacyFields = Set(
        "effectId", "currentStacks", "remainingTurns", "remainingUses", "deadline",
        "transitionId", "receiptId", "components", "carrierPath", "duration",
        "activeEffects", "activeBuffs", "activeDebuffs", "combatConditions",
        "effectIdentityIndex", "playerActiveEffectsChanges", "NPCEffectChanges");

    internal sealed record EffectPeriodicResourceResolution(
        IReadOnlyList<ResourceMutationSourceExport> SourceExports,
        IReadOnlyList<ResourceMutationIntent> Mutations,
        IReadOnlyList<ValidationIssue> Issues)
    {
        internal bool IsValid => Issues.Count == 0;

        internal IReadOnlyList<EffectResourceTriggerExecution> TriggerExecutions
        {
            get;
            init;
        } = Array.Empty<EffectResourceTriggerExecution>();

        internal IReadOnlyList<EffectBoundedResourceResolution> PendingResolutions
        {
            get;
            init;
        } = Array.Empty<EffectBoundedResourceResolution>();

        internal IReadOnlyList<string> ExecutedComponentIds
        {
            get;
            init;
        } = Array.Empty<string>();

        internal IReadOnlyList<EffectReactionExecution> ReactionExecutions
        {
            get;
            init;
        } = Array.Empty<EffectReactionExecution>();

        internal IReadOnlyList<EffectResourceTriggerCandidate> TriggerCandidates
        {
            get;
            init;
        } = Array.Empty<EffectResourceTriggerCandidate>();

        internal IReadOnlyDictionary<ResourceOperationKey, string>
            ComponentIdsByMutation
        {
            get;
            init;
        } = new Dictionary<ResourceOperationKey, string>();

        internal EffectResourceResolutionWork Work { get; init; } =
            EffectResourceResolutionWork.Empty;
    }

    internal sealed class EffectResourceCandidateOrigin
    {
        private readonly ResourceMutationIntent[] _mutations;
        private readonly string[] _componentIds;
        private readonly FrozenDictionary<ResourceOperationKey, string>
            _componentIdsByMutation;
        private readonly ResourceMutationSourceExport[] _sourceExports;

        internal static EffectResourceCandidateOrigin Empty { get; } = new(
            Array.Empty<ResourceMutationIntent>(),
            Array.Empty<string>(),
            new Dictionary<ResourceOperationKey, string>(),
            Array.Empty<ResourceMutationSourceExport>());

        internal EffectResourceCandidateOrigin(
            IReadOnlyList<ResourceMutationIntent> mutations,
            IReadOnlyList<string> componentIds,
            IReadOnlyDictionary<ResourceOperationKey, string>
                componentIdsByMutation,
            IReadOnlyList<ResourceMutationSourceExport> sourceExports)
        {
            ArgumentNullException.ThrowIfNull(mutations);
            ArgumentNullException.ThrowIfNull(componentIds);
            ArgumentNullException.ThrowIfNull(componentIdsByMutation);
            ArgumentNullException.ThrowIfNull(sourceExports);
            _mutations = mutations.Select(CloneMutation).ToArray();
            _componentIds = componentIds.ToArray();
            _componentIdsByMutation = componentIdsByMutation.ToFrozenDictionary();
            _sourceExports = sourceExports.Select(CloneSource).ToArray();
        }

        internal IReadOnlyList<ResourceMutationIntent> Mutations =>
            Array.AsReadOnly(_mutations.Select(CloneMutation).ToArray());

        internal IReadOnlyList<string> ComponentIds =>
            Array.AsReadOnly(_componentIds.ToArray());

        internal IReadOnlyDictionary<ResourceOperationKey, string>
            ComponentIdsByMutation => _componentIdsByMutation;

        internal IReadOnlyList<ResourceMutationSourceExport> SourceExports =>
            Array.AsReadOnly(_sourceExports.Select(CloneSource).ToArray());

        private static ResourceMutationIntent CloneMutation(
            ResourceMutationIntent mutation)
        {
            ArgumentNullException.ThrowIfNull(mutation);
            return mutation with
            {
                Dependencies = Array.AsReadOnly(
                    mutation.Dependencies.ToArray()),
                EventRequirements = Array.AsReadOnly(
                    mutation.EventRequirements.ToArray())
            };
        }

        private static ResourceMutationSourceExport CloneSource(
            ResourceMutationSourceExport source)
        {
            ArgumentNullException.ThrowIfNull(source);
            return source with
            {
                BoundOwner = source.BoundOwner is null
                    ? null
                    : source.BoundOwner with { }
            };
        }
    }

    internal sealed record EffectResourceTriggerCandidate
    {
        private readonly EffectBoundedResourceResolution[] _pendingOutputs;
        private readonly EffectReactionExecution[] _reactionOutputs;
        private readonly Dictionary<string, ResourcePendingResolvedBinding>
            _resolvedPendingBindings;

        internal EffectResourceTriggerCandidate(
            EffectActivationCandidate activation,
            CanonicalEffectUseSeed? useSeed,
            ResourceOperationKey? producer,
            IReadOnlyList<ResourceOperationKey> plannedMutationKeys,
            IReadOnlyList<string> plannedComponentIds,
            IReadOnlyDictionary<ResourceOperationKey, string>
                plannedComponentIdsByMutation,
            IReadOnlyList<EffectBoundedResourceResolution> pendingOutputs,
            IReadOnlyList<EffectReactionExecution> reactionOutputs,
            EffectResourceCandidateOrigin origin,
            string? candidateFingerprint = null,
            IReadOnlyDictionary<string, ResourcePendingResolvedBinding>?
                resolvedPendingBindings = null,
            int pendingWaveOrdinal = 0,
            string? causalMaterialFingerprint = null,
            ResourcePendingAuthorityBinding? effectAuthority = null)
        {
            Activation = activation ??
                throw new ArgumentNullException(nameof(activation));
            UseSeed = useSeed;
            Producer = producer;
            PlannedMutationKeys = Array.AsReadOnly(
                (plannedMutationKeys ??
                 throw new ArgumentNullException(nameof(plannedMutationKeys)))
                .ToArray());
            PlannedComponentIds = Array.AsReadOnly(
                (plannedComponentIds ??
                 throw new ArgumentNullException(nameof(plannedComponentIds)))
                .ToArray());
            PlannedComponentIdsByMutation =
                (plannedComponentIdsByMutation ??
                 throw new ArgumentNullException(
                     nameof(plannedComponentIdsByMutation)))
                .ToFrozenDictionary();
            _pendingOutputs = (pendingOutputs ??
                 throw new ArgumentNullException(nameof(pendingOutputs)))
                .Select(ClonePendingOutput)
                .ToArray();
            _reactionOutputs = (reactionOutputs ??
                 throw new ArgumentNullException(nameof(reactionOutputs)))
                .Select(CloneRoutingReaction)
                .ToArray();
            var pendingEffectAuthorities = _pendingOutputs
                .Select(static output => output.EffectAuthority)
                .Distinct()
                .ToArray();
            effectAuthority ??= pendingEffectAuthorities.Length == 1
                ? pendingEffectAuthorities[0]
                : new ResourcePendingAuthorityBinding(
                    "permanent",
                    Activation.Identity.EffectId);
            if (pendingEffectAuthorities.Any(authority =>
                    authority != effectAuthority))
            {
                throw new ArgumentException(
                    "Every bounded output must share the candidate effect authority.",
                    nameof(effectAuthority));
            }
            if (UseSeed != null && !string.Equals(
                    UseSeed.EffectId,
                    Activation.Identity.EffectId,
                    StringComparison.Ordinal) ||
                _pendingOutputs.Any(output => !string.Equals(
                    output.EffectId,
                    Activation.Identity.EffectId,
                    StringComparison.Ordinal)) ||
                _reactionOutputs.Any(output => !string.Equals(
                    output.EffectId,
                    Activation.Identity.EffectId,
                    StringComparison.Ordinal)))
            {
                throw new ArgumentException(
                    "Use, bounded-output, and reaction effect ids must equal the owning activation effect id.");
            }
            ValidatePendingEffectAuthority(
                Activation.Identity.EffectId,
                effectAuthority);
            if (Activation.EffectAuthority != effectAuthority)
            {
                throw new ArgumentException(
                    "The activation and candidate must share one exact effect authority.",
                    nameof(effectAuthority));
            }
            if (candidateFingerprint != null &&
                !ResourceMaterializationContract.IsAuthorityFingerprint(
                    candidateFingerprint))
            {
                throw new ArgumentException(
                    "Candidate fingerprint must be an exact authority fingerprint.",
                    nameof(candidateFingerprint));
            }
            if (causalMaterialFingerprint != null &&
                !ResourceMaterializationContract.IsAuthorityFingerprint(
                    causalMaterialFingerprint))
            {
                throw new ArgumentException(
                    "Causal material fingerprint must be an exact authority fingerprint.",
                    nameof(causalMaterialFingerprint));
            }
            if ((candidateFingerprint == null) !=
                (causalMaterialFingerprint == null))
            {
                throw new ArgumentException(
                    "Cached candidate and causal material fingerprints must be supplied together.");
            }
            if (pendingWaveOrdinal < 0)
                throw new ArgumentOutOfRangeException(nameof(pendingWaveOrdinal));
            CandidateFingerprint = candidateFingerprint;
            CausalMaterialFingerprint = causalMaterialFingerprint;
            Origin = origin ?? throw new ArgumentNullException(nameof(origin));
            _resolvedPendingBindings = (resolvedPendingBindings ??
                new Dictionary<string, ResourcePendingResolvedBinding>(
                    StringComparer.Ordinal))
                .ToDictionary(
                    static pair => pair.Key,
                    static pair => pair.Value.DeepClone(),
                    StringComparer.Ordinal);
            PendingWaveOrdinal = pendingWaveOrdinal;
            EffectAuthority = effectAuthority with { };
        }

        internal EffectActivationCandidate Activation { get; }

        internal CanonicalEffectUseSeed? UseSeed { get; }

        internal ResourcePendingAuthorityBinding EffectAuthority { get; }

        internal ResourceOperationKey? Producer { get; }

        internal IReadOnlyList<ResourceOperationKey> PlannedMutationKeys { get; }

        internal IReadOnlyList<string> PlannedComponentIds { get; }

        internal IReadOnlyDictionary<ResourceOperationKey, string>
            PlannedComponentIdsByMutation { get; }

        internal IReadOnlyList<EffectBoundedResourceResolution> PendingOutputs
            => Array.AsReadOnly(
                _pendingOutputs.Select(ClonePendingOutput).ToArray());

        internal IReadOnlyList<EffectReactionExecution> ReactionOutputs =>
            Array.AsReadOnly(
                _reactionOutputs.Select(CloneRoutingReaction).ToArray());

        internal string? CandidateFingerprint { get; }

        internal string? CausalMaterialFingerprint { get; }

        internal EffectResourceCandidateOrigin Origin { get; }

        internal int PendingWaveOrdinal { get; }

        internal bool TryResolvePendingBinding(
            string componentId,
            out ResourcePendingResolvedBinding binding)
        {
            if (_resolvedPendingBindings.TryGetValue(componentId, out var found))
            {
                binding = found.DeepClone();
                return true;
            }
            binding = null!;
            return false;
        }

        private static EffectBoundedResourceResolution ClonePendingOutput(
            EffectBoundedResourceResolution value) =>
            value with
            {
                Source = value.Source.DeepClone().AsObject(),
                Target = value.Target.DeepClone().AsObject(),
                Dependencies = Array.AsReadOnly(value.Dependencies.ToArray()),
                EventRequirements = Array.AsReadOnly(
                    value.EventRequirements.ToArray())
            };

        private static EffectReactionExecution CloneRoutingReaction(
            EffectReactionExecution value) =>
            value with
            {
                DownstreamSource = null,
                DownstreamSourceKey = value.DownstreamSourceKey ??
                    value.DownstreamSource?.Key,
                Parameters = value.Parameters?.DeepClone().AsObject()
            };
    }

    internal sealed record EffectResourceResolutionWork(
        int IndexLookupCount,
        int CandidateVisitCount,
        int SourceBindingIndexLookupCount = 0,
        int SourceBindingCandidateVisitCount = 0,
        long RoutingDescriptorAccessCount = 0,
        long OccurrenceCloneCount = 0,
        long FullEffectValidationPassCount = 0,
        long TriggerArrayVisitCount = 0,
        long ComponentIndexLookupCount = 0,
        long SelectedComponentVisitCount = 0,
        long SourceBindingBorrowCount = 0,
        long SourceBindingDefinitionCloneCount = 0,
        long SourceBindingDefinitionCloneNodeCount = 0,
        long PendingCandidateFingerprintOutputVisitCount = 0,
        long PendingProjectionDependencyVisitCount = 0)
    {
        internal static EffectResourceResolutionWork Empty { get; } =
            new(0, 0);

        internal long TotalWorkUnits =>
            (long)IndexLookupCount +
            CandidateVisitCount +
            SourceBindingIndexLookupCount +
            SourceBindingCandidateVisitCount +
            RoutingDescriptorAccessCount +
            OccurrenceCloneCount +
            FullEffectValidationPassCount +
            TriggerArrayVisitCount +
            ComponentIndexLookupCount +
            SelectedComponentVisitCount +
            SourceBindingBorrowCount +
            SourceBindingDefinitionCloneCount +
            SourceBindingDefinitionCloneNodeCount +
            PendingCandidateFingerprintOutputVisitCount +
            PendingProjectionDependencyVisitCount;
    }

    internal sealed class EffectResourceRoutingWorkMeter
    {
        internal long RoutingDescriptorAccessCount { get; private set; }

        internal long OccurrenceCloneCount { get; private set; }

        internal long FullEffectValidationPassCount { get; private set; }

        internal long TriggerArrayVisitCount { get; private set; }

        internal long ComponentIndexLookupCount { get; private set; }

        internal long SelectedComponentVisitCount { get; private set; }

        internal long SourceBindingBorrowCount { get; private set; }

        internal long SourceBindingDefinitionCloneCount { get; private set; }

        internal long SourceBindingDefinitionCloneNodeCount { get; private set; }

        internal void RecordRoutingDescriptorAccess() =>
            RoutingDescriptorAccessCount++;

        internal void RecordOccurrenceClone() => OccurrenceCloneCount++;

        internal void RecordFullEffectValidationPass() =>
            FullEffectValidationPassCount++;

        internal void RecordTriggerArrayVisit() => TriggerArrayVisitCount++;

        internal void RecordComponentIndexLookup() =>
            ComponentIndexLookupCount++;

        internal void RecordSelectedComponentVisit() =>
            SelectedComponentVisitCount++;

        internal void RecordSourceBindingBorrow() =>
            SourceBindingBorrowCount++;

        internal void RecordSourceBindingDefinitionClone(long nodeCount)
        {
            SourceBindingDefinitionCloneCount++;
            SourceBindingDefinitionCloneNodeCount += nodeCount;
        }

        internal EffectResourceResolutionWork Snapshot(
            int indexLookupCount,
            int candidateVisitCount,
            int sourceBindingIndexLookupCount,
            int sourceBindingCandidateVisitCount) =>
            new(
                indexLookupCount,
                candidateVisitCount,
                sourceBindingIndexLookupCount,
                sourceBindingCandidateVisitCount,
                RoutingDescriptorAccessCount,
                OccurrenceCloneCount,
                FullEffectValidationPassCount,
                TriggerArrayVisitCount,
                ComponentIndexLookupCount,
                SelectedComponentVisitCount,
                SourceBindingBorrowCount,
                SourceBindingDefinitionCloneCount,
                SourceBindingDefinitionCloneNodeCount);
    }

    internal sealed class EffectResourceRoutingBudgetSession
    {
        // Temporary compile-only compatibility surface. AcceptedMechanicsPlanner
        // still constructs this type until the arbiter consumer cutover lands.
        // Candidate materialization must never reserve, clamp, or commit uses.
        internal EffectResourceRoutingBudgetSession(
            IEnumerable<EffectResourceTriggerExecution>? seededExecutions = null)
        {
            _ = seededExecutions;
        }
    }

    internal sealed record EffectResourceTriggerIndexStatistics(
        int CatalogBuildCount,
        int OccurrenceVisitCount,
        int OccurrenceSnapshotBuildCount,
        int TriggerDescriptorVisitCount,
        int ReplacementTargetOccurrenceVisitCount)
    {
        internal long TotalWorkUnits =>
            (long)OccurrenceVisitCount +
            OccurrenceSnapshotBuildCount +
            TriggerDescriptorVisitCount +
            ReplacementTargetOccurrenceVisitCount;
    }

    internal sealed class EffectResourceTriggerIndex
    {
        private readonly Dictionary<
            ResourceTriggerLookupKey,
            IReadOnlyList<IndexedResourceTrigger>> _resourceTriggers;
        private readonly Dictionary<
            LifecycleTriggerLookupKey,
            IReadOnlyList<IndexedResourceTrigger>> _lifecycleTriggers;
        private readonly Dictionary<
            ExactEffectLifecycleTriggerLookupKey,
            IReadOnlyList<IndexedResourceTrigger>> _exactEffectLifecycleTriggers;
        private readonly Dictionary<
            ExactTriggerLifecycleLookupKey,
            IReadOnlyList<IndexedResourceTrigger>> _exactTriggerLifecycleTriggers;
        private readonly Dictionary<
            ExactLifecycleDescriptorLookupKey,
            IReadOnlyList<IndexedResourceTrigger>> _exactLifecycleDescriptors;
        private static IReadOnlyList<IndexedResourceTrigger> EmptyTriggers { get; } =
            Array.AsReadOnly(Array.Empty<IndexedResourceTrigger>());

        private EffectResourceTriggerIndex(
            EffectCarrierCatalog catalog,
            EffectTargetAuthority targetAuthority)
        {
            if (catalog.Issues.Count != 0)
            {
                _resourceTriggers = new Dictionary<
                    ResourceTriggerLookupKey,
                    IReadOnlyList<IndexedResourceTrigger>>();
                _lifecycleTriggers = new Dictionary<
                    LifecycleTriggerLookupKey,
                    IReadOnlyList<IndexedResourceTrigger>>();
                _exactEffectLifecycleTriggers = new Dictionary<
                    ExactEffectLifecycleTriggerLookupKey,
                    IReadOnlyList<IndexedResourceTrigger>>();
                _exactTriggerLifecycleTriggers = new Dictionary<
                    ExactTriggerLifecycleLookupKey,
                    IReadOnlyList<IndexedResourceTrigger>>();
                _exactLifecycleDescriptors = new Dictionary<
                    ExactLifecycleDescriptorLookupKey,
                    IReadOnlyList<IndexedResourceTrigger>>();
                Issues = Array.AsReadOnly(catalog.Issues.ToArray());
                Statistics = new EffectResourceTriggerIndexStatistics(
                    CatalogBuildCount: 1,
                    OccurrenceVisitCount: 0,
                    OccurrenceSnapshotBuildCount: 0,
                    TriggerDescriptorVisitCount: 0,
                    ReplacementTargetOccurrenceVisitCount: 0);
                return;
            }

            var resourceTriggers = new Dictionary<
                ResourceTriggerLookupKey,
                List<IndexedResourceTrigger>>();
            var lifecycleTriggers = new Dictionary<
                LifecycleTriggerLookupKey,
                List<IndexedResourceTrigger>>();
            var exactEffectLifecycleTriggers = new Dictionary<
                ExactEffectLifecycleTriggerLookupKey,
                List<IndexedResourceTrigger>>();
            var exactTriggerLifecycleTriggers = new Dictionary<
                ExactTriggerLifecycleLookupKey,
                List<IndexedResourceTrigger>>();
            var exactLifecycleDescriptors = new Dictionary<
                ExactLifecycleDescriptorLookupKey,
                List<IndexedResourceTrigger>>();
            var occurrenceVisitCount = 0;
            var occurrenceSnapshotBuildCount = 0;
            var triggerDescriptorVisitCount = 0;
            var replacementTargets = EffectReplacementTargetIndex.Build(
                catalog.Occurrences,
                out var replacementTargetOccurrenceVisitCount);

            foreach (var occurrence in catalog.Occurrences)
            {
                occurrenceVisitCount++;
                if (!TryReadExact(occurrence.Effect["state"], out var state) ||
                    !string.Equals(state, "active", StringComparison.Ordinal) ||
                    occurrence.Effect["target"] is not JsonObject target ||
                    !TryReadExact(occurrence.Effect["realm"], out var realm) ||
                    !TryReadExact(target["kind"], out var targetKind) ||
                    !TryReadExact(target["targetId"], out var targetId) ||
                    occurrence.Effect["triggers"] is not JsonArray triggers)
                {
                    continue;
                }

                var occurrenceSnapshot = new IndexedEffectOccurrenceSnapshot(
                    occurrence,
                    replacementTargets);
                occurrenceSnapshotBuildCount++;
                var targetKey = new EffectTargetKey(realm, targetKind, targetId);
                ResourceOwnerKey? resourceOwner = null;
                if (targetAuthority.TryResolveAcceptedTarget(
                        targetKey,
                        out var targetExport) &&
                    targetExport != null &&
                    TryMapEffectTargetToResourceOwner(
                        targetKey,
                        targetExport,
                        out var mappedResourceOwner))
                {
                    resourceOwner = mappedResourceOwner;
                }

                foreach (var trigger in triggers.OfType<JsonObject>())
                {
                    triggerDescriptorVisitCount++;
                    if (!TryReadExact(trigger["triggerId"], out var triggerId) ||
                        !TryReadExact(trigger["eventType"], out var eventKind) ||
                        trigger["priority"] is not JsonValue priorityNode ||
                        !priorityNode.TryGetValue<int>(out var priority))
                    {
                        continue;
                    }

                    var indexed = new IndexedResourceTrigger(
                        occurrenceSnapshot,
                        trigger,
                        triggerId,
                        eventKind,
                        priority);
                    AddIndexed(
                        lifecycleTriggers,
                        new LifecycleTriggerLookupKey(targetKey, eventKind),
                        indexed);
                    AddIndexed(
                        exactEffectLifecycleTriggers,
                        new ExactEffectLifecycleTriggerLookupKey(
                            targetKey,
                            eventKind,
                            occurrence.EffectId),
                        indexed);
                    AddIndexed(
                        exactTriggerLifecycleTriggers,
                        new ExactTriggerLifecycleLookupKey(
                            targetKey,
                            eventKind,
                            triggerId),
                        indexed);
                    AddIndexed(
                        exactLifecycleDescriptors,
                        new ExactLifecycleDescriptorLookupKey(
                            targetKey,
                            eventKind,
                            occurrence.EffectId,
                            triggerId),
                        indexed);
                    if (resourceOwner != null &&
                        EffectEventTypeCatalog.IsResourceEvent(eventKind))
                    {
                        AddIndexed(
                            resourceTriggers,
                            new ResourceTriggerLookupKey(resourceOwner, eventKind),
                            indexed);
                    }
                }
            }

            _resourceTriggers = Freeze(resourceTriggers);
            _lifecycleTriggers = Freeze(lifecycleTriggers);
            _exactEffectLifecycleTriggers = Freeze(
                exactEffectLifecycleTriggers);
            _exactTriggerLifecycleTriggers = Freeze(
                exactTriggerLifecycleTriggers);
            _exactLifecycleDescriptors = Freeze(exactLifecycleDescriptors);
            Issues = Array.AsReadOnly(catalog.Issues.ToArray());
            Statistics = new EffectResourceTriggerIndexStatistics(
                CatalogBuildCount: 1,
                occurrenceVisitCount,
                occurrenceSnapshotBuildCount,
                triggerDescriptorVisitCount,
                replacementTargetOccurrenceVisitCount);
        }

        internal IReadOnlyList<ValidationIssue> Issues { get; }

        internal EffectResourceTriggerIndexStatistics Statistics { get; }

        internal IReadOnlyList<IndexedResourceTrigger> ResolveResourceEvent(
            ResourceCoordinate coordinate,
            string eventKind) =>
            _resourceTriggers.TryGetValue(
                new ResourceTriggerLookupKey(
                    new ResourceOwnerKey(
                        coordinate.Realm,
                        coordinate.OwnerKind,
                        coordinate.ResourceOwnerId),
                    eventKind),
                out var triggers)
                ? triggers
                : EmptyTriggers;

        internal IReadOnlyList<IndexedResourceTrigger> ResolveLifecycleEvent(
            EffectTargetKey target,
            string eventKind,
            string? effectId = null,
            string? triggerId = null)
        {
            if (effectId != null && triggerId != null)
            {
                return _exactLifecycleDescriptors.TryGetValue(
                    new ExactLifecycleDescriptorLookupKey(
                        target,
                        eventKind,
                        effectId,
                        triggerId),
                    out var exactTriggers)
                    ? exactTriggers
                    : EmptyTriggers;
            }
            if (effectId != null)
            {
                return _exactEffectLifecycleTriggers.TryGetValue(
                    new ExactEffectLifecycleTriggerLookupKey(
                        target,
                        eventKind,
                        effectId),
                    out var exactEffectTriggers)
                    ? exactEffectTriggers
                    : EmptyTriggers;
            }
            if (triggerId != null)
            {
                return _exactTriggerLifecycleTriggers.TryGetValue(
                    new ExactTriggerLifecycleLookupKey(
                        target,
                        eventKind,
                        triggerId),
                    out var exactTriggerMatches)
                    ? exactTriggerMatches
                    : EmptyTriggers;
            }

            return _lifecycleTriggers.TryGetValue(
                    new LifecycleTriggerLookupKey(target, eventKind),
                    out var triggers)
                ? triggers
                : EmptyTriggers;
        }

        internal static EffectResourceTriggerIndex Build(
            EffectCarrierCatalogInput carriers,
            EffectTargetAuthority targetAuthority) =>
            new(EffectCarrierCatalog.Build(carriers), targetAuthority);

        private static void AddIndexed<TKey>(
            Dictionary<TKey, List<IndexedResourceTrigger>> index,
            TKey key,
            IndexedResourceTrigger value)
            where TKey : notnull
        {
            if (!index.TryGetValue(key, out var entries))
            {
                entries = new List<IndexedResourceTrigger>();
                index.Add(key, entries);
            }
            entries.Add(value);
        }

        private static Dictionary<TKey, IReadOnlyList<IndexedResourceTrigger>> Freeze<TKey>(
            Dictionary<TKey, List<IndexedResourceTrigger>> source)
            where TKey : notnull =>
            source.ToDictionary(
                static pair => pair.Key,
                static pair => (IReadOnlyList<IndexedResourceTrigger>)
                    Array.AsReadOnly(pair.Value
                        .OrderBy(
                            static value => value.EffectId,
                            StringComparer.Ordinal)
                        .ThenBy(static value => value.Priority)
                        .ThenBy(
                            static value => value.TriggerId,
                            StringComparer.Ordinal)
                        .ToArray()));
    }

    internal sealed class IndexedResourceTrigger
    {
        private readonly IndexedEffectOccurrenceSnapshot _occurrenceSnapshot;

        internal IndexedResourceTrigger(
            IndexedEffectOccurrenceSnapshot occurrenceSnapshot,
            JsonObject trigger,
            string triggerId,
            string eventKind,
            int priority)
        {
            _occurrenceSnapshot = occurrenceSnapshot ??
                throw new ArgumentNullException(nameof(occurrenceSnapshot));
            ArgumentNullException.ThrowIfNull(trigger);
            _triggerSnapshot = trigger.DeepClone().AsObject();
            TriggerId = triggerId;
            EventKind = eventKind;
            Priority = priority;
            UseSeed = occurrenceSnapshot.CanonicalUseSeed;
            ConsumesUse = occurrenceSnapshot.ConsumesUse(triggerId);
            RemainingUseBudget = ConsumesUse
                ? UseSeed?.RemainingUses
                : null;
        }

        private readonly JsonObject _triggerSnapshot;

        internal string EffectId => _occurrenceSnapshot.EffectId;

        internal EffectCarrierOccurrence Occurrence =>
            _occurrenceSnapshot.CloneOccurrence();

        private IndexedResourceTriggerRoutingHandle OpenRoutingDescriptor(
            EffectResourceRoutingWorkMeter workMeter)
        {
            return _occurrenceSnapshot.OpenRoutingDescriptor(
                _triggerSnapshot,
                RemainingUseBudget,
                workMeter);
        }

        internal IndexedResourceTriggerRoutingHandle OpenRoutingHandle(
            EffectResourceRoutingWorkMeter workMeter) =>
            OpenRoutingDescriptor(workMeter);

        internal string TriggerId { get; }

        internal string EventKind { get; }

        internal int Priority { get; }

        internal bool ConsumesUse { get; }

        internal CanonicalEffectUseSeed? UseSeed { get; }

        internal int? RemainingUseBudget { get; }
    }

    internal sealed class IndexedEffectOccurrenceSnapshot
    {
        private readonly EffectCarrierOccurrence _occurrence;
        private readonly FrozenDictionary<string, JsonObject> _componentsById;
        private readonly FrozenSet<string> _consumingTriggerIds;
        private readonly int? _remainingUses;
        private readonly EffectReplacementTargetIndex _replacementTargets;

        internal IndexedEffectOccurrenceSnapshot(
            EffectCarrierOccurrence occurrence,
            EffectReplacementTargetIndex replacementTargets)
        {
            ArgumentNullException.ThrowIfNull(occurrence);
            ArgumentNullException.ThrowIfNull(replacementTargets);
            _occurrence = Clone(occurrence);
            _replacementTargets = replacementTargets;
            var componentsById = new Dictionary<string, JsonObject>(
                StringComparer.Ordinal);
            if (_occurrence.Effect["components"] is JsonArray components)
            {
                foreach (var component in components.OfType<JsonObject>())
                {
                    if (TryReadExact(component["componentId"], out var componentId))
                        componentsById.TryAdd(componentId, component);
                }
            }
            _componentsById = componentsById.ToFrozenDictionary(
                StringComparer.Ordinal);
            var consumingTriggerIds = new HashSet<string>(StringComparer.Ordinal);
            if (_occurrence.Effect["lifetime"] is JsonObject lifetime &&
                TryReadExact(lifetime["mode"], out var mode) &&
                string.Equals(mode, "uses", StringComparison.Ordinal) &&
                TryReadPositiveInt(lifetime["remainingUses"], out var remainingUses) &&
                lifetime["consumingTriggerIds"] is JsonArray consumingIds)
            {
                _remainingUses = remainingUses;
                foreach (var node in consumingIds)
                {
                    if (TryReadExact(node, out var triggerId))
                        consumingTriggerIds.Add(triggerId);
                }
            }
            _consumingTriggerIds = consumingTriggerIds.ToFrozenSet(
                StringComparer.Ordinal);
        }

        internal string EffectId => _occurrence.EffectId;

        internal CanonicalEffectUseSeed? CanonicalUseSeed =>
            _remainingUses is { } remainingUses
                ? new CanonicalEffectUseSeed(EffectId, remainingUses)
                : null;

        internal EffectCarrierOccurrence CloneOccurrence() => Clone(_occurrence);

        internal IndexedResourceTriggerRoutingHandle OpenRoutingDescriptor(
            JsonObject triggerSnapshot,
            int? remainingUseBudget,
            EffectResourceRoutingWorkMeter workMeter)
        {
            ArgumentNullException.ThrowIfNull(triggerSnapshot);
            ArgumentNullException.ThrowIfNull(workMeter);
            workMeter.RecordRoutingDescriptorAccess();
            return new IndexedResourceTriggerRoutingHandle(
                _occurrence,
                triggerSnapshot,
                _componentsById,
                remainingUseBudget,
                _replacementTargets,
                workMeter);
        }

        internal bool ConsumesUse(string triggerId) =>
            _consumingTriggerIds.Contains(triggerId);

        private static EffectCarrierOccurrence Clone(
            EffectCarrierOccurrence occurrence) =>
            occurrence with
            {
                Coordinate = occurrence.Coordinate with { },
                Effect = occurrence.Effect.DeepClone().AsObject()
            };
    }

    internal sealed class IndexedResourceTriggerRoutingHandle
    {
        private readonly EffectCarrierOccurrence _occurrence;
        private readonly JsonObject _trigger;
        private readonly IReadOnlyDictionary<string, JsonObject> _componentsById;
        private readonly EffectReplacementTargetIndex _replacementTargets;
        private readonly EffectResourceRoutingWorkMeter _workMeter;

        internal IndexedResourceTriggerRoutingHandle(
            EffectCarrierOccurrence occurrence,
            JsonObject trigger,
            IReadOnlyDictionary<string, JsonObject> componentsById,
            int? remainingUseBudget,
            EffectReplacementTargetIndex replacementTargets,
            EffectResourceRoutingWorkMeter workMeter)
        {
            _occurrence = occurrence;
            _trigger = trigger;
            _componentsById = componentsById;
            RemainingUseBudget = remainingUseBudget;
            _replacementTargets = replacementTargets;
            _workMeter = workMeter;
        }

        internal string EffectId => _occurrence.EffectId;

        internal int? RemainingUseBudget { get; }

        internal bool WasCreatedByCausalEvent(string? causalEventRef) =>
            EffectAcceptedTurnPlanner.WasCreatedByCausalEvent(
                _occurrence.Effect,
                causalEventRef);

        internal string CreateResourceEventActivationRef(
            string triggerId,
            ResourceOperationKey producer,
            string eventKind,
            int turn) =>
            CreateResourceEventTriggerRef(
                _occurrence.Effect,
                triggerId,
                producer,
                eventKind,
                turn);

        internal ResourcePendingAuthorityBinding ResolvePendingEffectAuthority(
            int turn) =>
            EffectAcceptedTurnPlanner.ResolvePendingEffectAuthority(
                _occurrence.Effect,
                turn);

        internal EffectSourceRoutingBinding? ResolvePlanSourceBinding(
            EffectAcceptedTurnPlan plan,
            ref int indexLookupCount,
            ref int candidateVisitCount) =>
            EffectAcceptedTurnPlanner.ResolvePlanSourceBinding(
                plan,
                _occurrence.Effect,
                _workMeter,
                ref indexLookupCount,
                ref candidateVisitCount);

        internal EffectReactionPlanningResult PlanResourceEvent(
            string triggerId,
            ResourceAppliedEvent producerEvent,
            EffectSourceAuthority sourceAuthority,
            WoundReactionLineageAuthority? woundLineageAuthority = null) =>
            EffectReactionExecutor.PlanResourceEvent(
                _occurrence,
                triggerId,
                _trigger,
                _componentsById,
                producerEvent,
                sourceAuthority,
                _replacementTargets,
                _workMeter,
                woundLineageAuthority);

        internal EffectPeriodicResourceResolution ResolvePeriodic(
            string triggerId,
            EffectLifecycleEvent acceptedEvent,
            EffectTargetAuthority targetAuthority,
            ResourceOwnerAuthority ownerAuthority,
            ResourceDefinitionCatalog definitions,
            EffectSourceRoutingBinding? sourceAuthority,
            int? remainingUseBudget) =>
            ResolvePeriodicResourceMutationsCore(
                _occurrence.Effect,
                triggerId,
                acceptedEvent,
                targetAuthority,
                ownerAuthority,
                definitions,
                sourceAuthority,
                _trigger,
                _componentsById,
                remainingUseBudget,
                validateFullEffect: false,
                workMeter: _workMeter);

        internal EffectPeriodicResourceResolution ResolveResourceEvent(
            string triggerId,
            ResourceOperationKey producer,
            ResourceAppliedEvent producerEvent,
            EffectTargetAuthority targetAuthority,
            ResourceOwnerAuthority ownerAuthority,
            ResourceDefinitionCatalog definitions,
            EffectSourceRoutingBinding? sourceAuthority,
            int? remainingUseBudget) =>
            ResolveResourceEventMutationsCore(
                _occurrence.Effect,
                triggerId,
                producer,
                producerEvent.EventKind,
                producerEvent.Turn,
                targetAuthority,
                ownerAuthority,
                definitions,
                sourceAuthority,
                _trigger,
                _componentsById,
                remainingUseBudget,
                validateFullEffect: false,
                workMeter: _workMeter);
    }

    private sealed record ResourceTriggerLookupKey(
        ResourceOwnerKey Owner,
        string EventKind);

    private sealed record LifecycleTriggerLookupKey(
        EffectTargetKey Target,
        string EventKind);

    private sealed record ExactEffectLifecycleTriggerLookupKey(
        EffectTargetKey Target,
        string EventKind,
        string EffectId);

    private sealed record ExactTriggerLifecycleLookupKey(
        EffectTargetKey Target,
        string EventKind,
        string TriggerId);

    private sealed record ExactLifecycleDescriptorLookupKey(
        EffectTargetKey Target,
        string EventKind,
        string EffectId,
        string TriggerId);

    internal static EffectResourceTriggerIndex CreateResourceTriggerIndex(
        EffectCarrierCatalogInput carriers,
        EffectTargetAuthority targetAuthority)
    {
        ArgumentNullException.ThrowIfNull(carriers);
        ArgumentNullException.ThrowIfNull(targetAuthority);
        return EffectResourceTriggerIndex.Build(carriers, targetAuthority);
    }

    internal sealed record EffectBoundedResourceResolution(
        string EventRef,
        string EffectId,
        ResourcePendingAuthorityBinding EffectAuthority,
        JsonObject Source,
        ResourcePendingAuthorityBinding SourceAuthority,
        JsonObject Target,
        ResourcePendingAuthorityBinding TargetAuthority,
        string TriggerId,
        string ComponentId,
        string TriggerEventRef,
        string EventKind,
        ResourceCoordinate Coordinate,
        ResourcePendingAuthorityBinding ResourceAuthority,
        ResourceOperation Operation,
        decimal MinimumAmount,
        decimal MaximumAmount,
        string SourceAuthorityFingerprint,
        string PolicyFingerprint,
        IReadOnlyList<ResourceOperationKey> Dependencies,
        IReadOnlyList<ResourceMutationEventRequirement> EventRequirements,
        ResourceMutationResultConstraint? ResultConstraint,
        int? RemainingUseBudget,
        string SafeSourceLabel,
        string SafeTargetLabel,
        string SafeResourceLabel,
        string SafeOperationLabel,
        string? AfterComponentId = null);

    internal sealed record EffectResourceTriggerExecution(
        string EffectId,
        string TriggerId,
        string EventKind,
        string EventRef,
        IReadOnlyList<ResourceOperationKey> MutationKeys,
        int? RemainingUseBudget,
        IReadOnlyList<string>? ComponentIds = null,
        string? TriggerEventRef = null,
        IReadOnlyDictionary<ResourceOperationKey, string>? ComponentIdsByMutation = null);

    private static EffectResourceTriggerExecution ProjectCompatibilityExecution(
        EffectResourceTriggerCandidate candidate)
    {
        var activation = candidate.Activation;
        return new EffectResourceTriggerExecution(
            activation.Identity.EffectId,
            activation.Identity.TriggerId,
            activation.Identity.EventKind,
            activation.Identity.EventRef,
            candidate.PlannedMutationKeys,
            activation.ConsumesUse
                ? candidate.UseSeed?.RemainingUses
                : null,
            candidate.PlannedComponentIds,
            activation.Identity.TriggerEventRef,
            candidate.PlannedComponentIdsByMutation);
    }

    private static EffectResourceCandidateOrigin CreateCandidateOrigin(
        EffectPeriodicResourceResolution resolved)
    {
        var referencedSources = resolved.Mutations
            .Select(static mutation => (
                mutation.Source.SourceKind,
                mutation.Source.SourceId))
            .ToHashSet();
        return new EffectResourceCandidateOrigin(
            resolved.Mutations,
            resolved.ExecutedComponentIds,
            resolved.ComponentIdsByMutation,
            resolved.SourceExports
                .Where(source => referencedSources.Contains((
                    source.SourceKind,
                    source.SourceId)))
                .ToArray());
    }

    internal static EffectPeriodicResourceResolution ResolvePeriodicResourceMutations(
        JsonObject effect,
        string triggerId,
        EffectLifecycleEvent acceptedEvent,
        EffectTargetAuthority targetAuthority,
        ResourceOwnerAuthority ownerAuthority,
        ResourceDefinitionCatalog definitions,
        EffectSourceAuthorityEntry? sourceAuthority = null) =>
        ResolvePeriodicResourceMutationsCore(
            effect,
            triggerId,
            acceptedEvent,
            targetAuthority,
            ownerAuthority,
            definitions,
            sourceAuthority is null
                ? null
                : EffectSourceRoutingBinding.FromEntry(sourceAuthority),
            exactTrigger: null,
            componentsById: null,
            exactRemainingUseBudget: null,
            validateFullEffect: true,
            workMeter: null);

    private static EffectPeriodicResourceResolution
        ResolvePeriodicResourceMutationsCore(
        JsonObject effect,
        string triggerId,
        EffectLifecycleEvent acceptedEvent,
        EffectTargetAuthority targetAuthority,
        ResourceOwnerAuthority ownerAuthority,
        ResourceDefinitionCatalog definitions,
        EffectSourceRoutingBinding? sourceAuthority,
        JsonObject? exactTrigger,
        IReadOnlyDictionary<string, JsonObject>? componentsById,
        int? exactRemainingUseBudget,
        bool validateFullEffect,
        EffectResourceRoutingWorkMeter? workMeter)
    {
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentNullException.ThrowIfNull(acceptedEvent);
        ArgumentNullException.ThrowIfNull(targetAuthority);
        ArgumentNullException.ThrowIfNull(ownerAuthority);
        ArgumentNullException.ThrowIfNull(definitions);

        var issues = new List<ValidationIssue>();
        issues.AddRange(targetAuthority.Issues);
        issues.AddRange(ownerAuthority.Issues);
        if (validateFullEffect)
        {
            workMeter?.RecordFullEffectValidationPass();
            using var document = JsonDocument.Parse(effect.ToJsonString());
            var isSpiritualCondition =
                effect["target"] is JsonObject effectTarget &&
                string.Equals(
                    effectTarget["kind"]?.GetValue<string>(),
                    "spiritual_conflict_side",
                    StringComparison.Ordinal);
            issues.AddRange(
                isSpiritualCondition
                    ? EffectMaterializationContract.ValidateAfterlifeCombatCondition(
                        document.RootElement,
                        "effect")
                    : EffectMaterializationContract.Validate(
                        document.RootElement,
                        "effect",
                        EffectMaterializationPhase.CanonicalActive));
        }

        var hasEffectId = TryReadExact(effect["effectId"], out var effectId);
        var hasRealm = TryReadExact(effect["realm"], out var realm);
        var hasState = TryReadExact(effect["state"], out var state);
        var target = effect["target"] as JsonObject;
        var hasTargetKind = TryReadExact(target?["kind"], out var targetKind);
        var hasTargetId = TryReadExact(target?["targetId"], out var targetId);
        var targetKindSupported = hasTargetKind &&
            TryMapEffectTargetKind(targetKind, out _);
        if (hasTargetKind && !targetKindSupported)
        {
            Add(
                issues,
                "effect.target.kind",
                "effect_resource_target_unsupported",
                "effect target kind with a registered resource-owner mapping",
                targetKind);
        }
        if (!TryExact(triggerId) ||
            acceptedEvent.Turn <= 0 ||
            !TryExact(acceptedEvent.EventRef) ||
            !TryExact(acceptedEvent.TriggerId ?? string.Empty) ||
            !string.Equals(
                acceptedEvent.TriggerId,
                triggerId,
                StringComparison.Ordinal) ||
            !TryExact(acceptedEvent.Phase ?? string.Empty))
        {
            Add(
                issues,
                "effect.event",
                "effect_resource_event_authority_invalid",
                "positive accepted event with exact matching triggerId, phase, and eventRef",
                $"turn={acceptedEvent.Turn};trigger={acceptedEvent.TriggerId};phase={acceptedEvent.Phase};eventRef={acceptedEvent.EventRef}");
        }
        if (hasState && !string.Equals(state, "active", StringComparison.Ordinal))
        {
            Add(
                issues,
                "effect.state",
                "effect_resource_effect_inactive",
                "active canonical effect",
                state);
        }

        if (issues.Count != 0 || !hasEffectId || !hasRealm ||
            !hasTargetKind || !hasTargetId || target == null ||
            !targetKindSupported)
        {
            return FailedPeriodicResourceResolution(issues);
        }

        var targetKey = new EffectTargetKey(realm, targetKind, targetId);
        if (!targetAuthority.TryResolveAcceptedTarget(targetKey, out var targetExport) ||
            targetExport == null)
        {
            Add(
                issues,
                "effect.target",
                "effect_resource_target_unresolved",
                "one exact accepted canonical effect target",
                $"{realm}/{targetKind}/{targetId}");
            return FailedPeriodicResourceResolution(issues);
        }

        if (!TryMapEffectTargetToResourceOwner(
                targetKey,
                targetExport,
                out var ownerKey))
        {
            Add(
                issues,
                "effect.target.kind",
                "effect_resource_target_unsupported",
                "effect target kind with one exact resource owner",
                targetKind);
            return FailedPeriodicResourceResolution(issues);
        }

        JsonObject? trigger = null;
        if (exactTrigger != null)
        {
            if (TryReadExact(exactTrigger["triggerId"], out var exactTriggerId) &&
                string.Equals(exactTriggerId, triggerId, StringComparison.Ordinal))
            {
                trigger = exactTrigger;
            }
        }
        else if (effect["triggers"] is JsonArray triggers)
        {
            foreach (var candidate in triggers.OfType<JsonObject>())
            {
                workMeter?.RecordTriggerArrayVisit();
                if (!string.Equals(
                        candidate["triggerId"]?.GetValue<string>(),
                        triggerId,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                if (trigger != null)
                {
                    trigger = null;
                    break;
                }
                trigger = candidate;
            }
        }
        if (trigger == null)
        {
            Add(
                issues,
                "effect.triggers",
                "effect_resource_trigger_unresolved",
                "one exact selected trigger",
                triggerId);
            return FailedPeriodicResourceResolution(issues);
        }

        var triggerEventType = trigger["eventType"]!.GetValue<string>();
        if (!string.Equals(
                triggerEventType,
                acceptedEvent.Phase,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                "effect.triggers.eventType",
                "effect_resource_trigger_event_mismatch",
                triggerEventType,
                acceptedEvent.Phase!);
        }
        var resolutionMode = trigger["resolutionMode"]!.GetValue<string>();
        var isBoundedResolution = string.Equals(
            resolutionMode,
            "bounded_receipt",
            StringComparison.Ordinal);
        if (!isBoundedResolution && !string.Equals(
                resolutionMode,
                "deterministic",
                StringComparison.Ordinal))
        {
            Add(
                issues,
                "effect.triggers.resolutionMode",
                "effect_resource_resolution_mode_invalid",
                "deterministic or bounded_receipt trigger resolution",
                resolutionMode);
        }
        if (issues.Count != 0)
            return FailedPeriodicResourceResolution(issues);

        var components = componentsById == null
            ? effect["components"]!.AsArray().OfType<JsonObject>().ToArray()
            : Array.Empty<JsonObject>();
        var selected = new List<(
            EffectPeriodicResourceComponent Parsed,
            JsonObject Node,
            string? AfterComponentId)>();
        var executableComponents = componentsById == null
            ? EffectReactionExecutor.ResolveExecutableComponents(
                effect,
                trigger,
                acceptedEvent.Phase!,
                issues)
            : EffectReactionExecutor.ResolveExecutableComponents(
                trigger,
                acceptedEvent.Phase!,
                componentsById,
                issues,
                workMeter!);
        foreach (var selection in executableComponents)
        {
            var componentId = selection.ComponentId;
            JsonObject? componentNode = null;
            if (componentsById != null)
            {
                componentNode = selection.IndexedComponent;
            }
            else
            {
                var matches = components.Where(candidate =>
                        string.Equals(
                            candidate["componentId"]?.GetValue<string>(),
                            componentId,
                            StringComparison.Ordinal))
                    .ToArray();
                if (matches.Length == 1)
                    componentNode = matches[0];
            }
            if (componentNode == null)
            {
                Add(
                    issues,
                    "effect.triggers.componentIds",
                    "effect_resource_component_unresolved",
                    "one exact component owned by this effect",
                    componentId);
                continue;
            }

            var profile = componentNode["profile"]!.GetValue<string>();
            if (profile is not ("periodic_damage" or "periodic_restore"))
                continue;
            using var componentDocument = JsonDocument.Parse(
                componentNode.ToJsonString());
            var parsed = EffectComponentProfiles.ParsePeriodicResourceComponent(
                componentDocument.RootElement,
                $"effect.components[{componentId}]");
            issues.AddRange(parsed.Issues);
            if (parsed.IsValid)
                selected.Add((
                    parsed.Component!,
                    componentNode,
                    selection.AfterComponentId));
        }
        if (issues.Count != 0)
            return FailedPeriodicResourceResolution(issues);

        var sourceExports = new List<ResourceMutationSourceExport>();
        var mutations = new List<ResourceMutationIntent>();
        var pendingResolutions = new List<EffectBoundedResourceResolution>();
        var executedComponentIds = new List<string>();
        var componentIdsByMutation = new Dictionary<ResourceOperationKey, string>();
        foreach (var candidate in selected
                     .OrderBy(static value => value.Parsed.Priority)
                     .ThenBy(
                         static value => value.Parsed.ComponentId,
                         StringComparer.Ordinal))
        {
            var component = candidate.Parsed;
            if (!definitions.TryResolveExact(
                    component.ResourceKey,
                    out var definition) ||
                definition == null)
            {
                Add(
                    issues,
                    $"effect.components[{component.ComponentId}].payload.resource",
                    "effect_resource_definition_unknown",
                    "one exact sealed common resource definition",
                    component.ResourceKey);
                continue;
            }
            if (!definition.AllowedOwnerKinds.Contains(ownerKey.OwnerKind))
            {
                Add(
                    issues,
                    $"effect.components[{component.ComponentId}].payload.resource",
                    "effect_resource_owner_kind_forbidden",
                    "target owner kind allowed by the sealed resource definition",
                    ownerKey.OwnerKind.ToString());
                continue;
            }
            if (!definition.AllowedOperations.Contains(component.Operation))
            {
                Add(
                    issues,
                    $"effect.components[{component.ComponentId}].profile",
                    "effect_resource_operation_forbidden",
                    "periodic operation allowed by the sealed resource definition",
                    component.Operation.ToString());
                continue;
            }

            var ownerResolution = ownerAuthority.ResolveAcceptedCoordinate(
                ownerKey,
                component.ResourceKey);
            issues.AddRange(ownerResolution.Issues);
            if (!ownerResolution.Success)
                continue;

            var constraint = ResolvePeriodicResultConstraint(
                component,
                definition,
                issues);
            if (issues.Count != 0)
                continue;

            var effectAuthority = ResolvePendingEffectAuthority(
                effect,
                acceptedEvent.Turn);
            var sameTurn = string.Equals(
                effectAuthority.BindingKind,
                "accepted_application",
                StringComparison.Ordinal);
            var sourceBinding = CreatePendingSourceAuthority(
                effect["source"]!.AsObject(),
                sourceAuthority);
            var targetBinding = CreatePendingTargetAuthority(targetExport);
            var resourceBinding = CreatePendingResourceAuthority(
                ownerResolution.Entry!);
            var sourceId = CreatePeriodicSourceId(
                effectId,
                effectAuthority,
                triggerId,
                component.ComponentId);
            var sourceFingerprint = isBoundedResolution
                ? CreateBoundedCandidateSourceFingerprint(
                    effectAuthority,
                    sourceBinding,
                    targetBinding,
                    resourceBinding,
                    realm,
                    targetKind,
                    ownerKey.OwnerKind,
                    trigger,
                    component,
                    definition)
                : CreatePeriodicSourceFingerprint(
                    effectId,
                    effectAuthority,
                    realm,
                    targetKey,
                    ownerKey,
                    trigger,
                    component,
                    definition);
            if (isBoundedResolution)
            {
                pendingResolutions.Add(new EffectBoundedResourceResolution(
                    acceptedEvent.EventRef,
                    effectId,
                    effectAuthority,
                    effect["source"]!.DeepClone().AsObject(),
                    sourceBinding,
                    target.DeepClone().AsObject(),
                    targetBinding,
                    triggerId,
                    component.ComponentId,
                    acceptedEvent.CausalEventRef ?? acceptedEvent.EventRef,
                    acceptedEvent.Phase!,
                    new ResourceCoordinate(
                        ownerKey.Realm,
                        ownerKey.OwnerKind,
                        ownerKey.ResourceOwnerId,
                        component.ResourceKey),
                    resourceBinding,
                    component.Operation,
                    MinimumAmount: 0m,
                    MaximumAmount: component.Amount,
                    sourceFingerprint,
                    CreateBoundedResolutionPolicyFingerprint(
                        sourceFingerprint,
                        component,
                        definition,
                        constraint),
                    Array.Empty<ResourceOperationKey>(),
                    Array.Empty<ResourceMutationEventRequirement>(),
                    constraint,
                    exactTrigger != null
                        ? exactRemainingUseBudget
                        : ReadRemainingUseBudget(effect, triggerId),
                    ReadSafeSourceLabel(effect),
                    ReadSafeTargetLabel(targetKind),
                    definition.DisplayName,
                    ReadSafeOperationLabel(component.Operation),
                    candidate.AfterComponentId));
                continue;
            }
            sourceExports.Add(new ResourceMutationSourceExport(
                "effect_component",
                sourceId,
                sourceFingerprint,
                ResourceMutationSourceState.Active,
                sameTurn,
                ownerKey));
            var mutation = new ResourceMutationIntent(
                acceptedEvent.EventRef,
                new ResourceCoordinate(
                    ownerKey.Realm,
                    ownerKey.OwnerKind,
                    ownerKey.ResourceOwnerId,
                    component.ResourceKey),
                component.Amount,
                new ResourceMutationSourceRequest(
                    "effect_component",
                    sourceId,
                    component.Operation),
                Array.Empty<ResourceOperationKey>(),
                Array.Empty<ResourceMutationEventRequirement>(),
                ReceiptId: null,
                ResultConstraint: constraint);
            mutations.Add(mutation);
            componentIdsByMutation.Add(mutation.Key, component.ComponentId);
            executedComponentIds.Add(component.ComponentId);
        }

        var mutationByComponent = componentIdsByMutation
            .ToDictionary(
                static pair => pair.Value,
                pair => mutations.Single(mutation => mutation.Key == pair.Key),
                StringComparer.Ordinal);
        foreach (var candidate in selected.Where(static candidate =>
                     candidate.AfterComponentId != null))
        {
            if (isBoundedResolution)
                continue;
            if (!mutationByComponent.TryGetValue(
                    candidate.Parsed.ComponentId,
                    out var dependent) ||
                !mutationByComponent.TryGetValue(
                    candidate.AfterComponentId!,
                    out var predecessor))
            {
                Add(
                    issues,
                    $"effect.components[{candidate.Parsed.ComponentId}]",
                    "effect_reaction_component_dependency_unresolved",
                    "one exact executable predecessor and dependent component",
                    candidate.AfterComponentId!);
                continue;
            }

            var bound = dependent with
            {
                Dependencies = dependent.Dependencies
                    .Append(predecessor.Key)
                    .Distinct()
                    .ToArray(),
                EventRequirements = dependent.EventRequirements
                    .Append(new ResourceMutationEventRequirement(
                        predecessor.Key,
                        PrimaryResourceEvent(predecessor.Source.Operation)))
                    .Distinct()
                    .ToArray()
            };
            var index = mutations.FindIndex(mutation => mutation.Key == dependent.Key);
            mutations[index] = bound;
            mutationByComponent[candidate.Parsed.ComponentId] = bound;
        }

        return issues.Count == 0
            ? new EffectPeriodicResourceResolution(
                sourceExports.ToArray(),
                mutations.ToArray(),
                Array.Empty<ValidationIssue>())
            {
                PendingResolutions = pendingResolutions.ToArray(),
                ExecutedComponentIds = executedComponentIds
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static value => value, StringComparer.Ordinal)
                    .ToArray(),
                ComponentIdsByMutation = new Dictionary<ResourceOperationKey, string>(
                    componentIdsByMutation)
            }
            : FailedPeriodicResourceResolution(issues);
    }

    internal static EffectPeriodicResourceResolution ResolveResourceEventMutations(
        JsonObject effect,
        string triggerId,
        ResourceOperationKey producer,
        string eventKind,
        int turn,
        EffectTargetAuthority targetAuthority,
        ResourceOwnerAuthority ownerAuthority,
        ResourceDefinitionCatalog definitions,
        EffectSourceAuthorityEntry? sourceAuthority = null) =>
        ResolveResourceEventMutationsCore(
            effect,
            triggerId,
            producer,
            eventKind,
            turn,
            targetAuthority,
            ownerAuthority,
            definitions,
            sourceAuthority is null
                ? null
                : EffectSourceRoutingBinding.FromEntry(sourceAuthority),
            exactTrigger: null,
            componentsById: null,
            exactRemainingUseBudget: null,
            validateFullEffect: true,
            workMeter: null);

    private static EffectPeriodicResourceResolution
        ResolveResourceEventMutationsCore(
        JsonObject effect,
        string triggerId,
        ResourceOperationKey producer,
        string eventKind,
        int turn,
        EffectTargetAuthority targetAuthority,
        ResourceOwnerAuthority ownerAuthority,
        ResourceDefinitionCatalog definitions,
        EffectSourceRoutingBinding? sourceAuthority,
        JsonObject? exactTrigger,
        IReadOnlyDictionary<string, JsonObject>? componentsById,
        int? exactRemainingUseBudget,
        bool validateFullEffect,
        EffectResourceRoutingWorkMeter? workMeter)
    {
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentNullException.ThrowIfNull(producer);
        ArgumentNullException.ThrowIfNull(targetAuthority);
        ArgumentNullException.ThrowIfNull(ownerAuthority);
        ArgumentNullException.ThrowIfNull(definitions);

        var issues = new List<ValidationIssue>();
        if (!EffectEventTypeCatalog.IsResourceEvent(eventKind))
        {
            Add(
                issues,
                "effect.triggers.eventType",
                "effect_resource_event_kind_invalid",
                "one closed common resource event kind",
                eventKind ?? "null");
        }
        if (turn <= 0)
        {
            Add(
                issues,
                "effect.event.turn",
                "effect_resource_event_authority_invalid",
                "positive accepted turn",
                turn.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        if (issues.Count != 0)
            return FailedPeriodicResourceResolution(issues);

        var exactEventKind = eventKind!;
        var eventRef = CreateResourceEventTriggerRef(
            effect,
            triggerId,
            producer,
            exactEventKind,
            turn);
        var resolved = ResolvePeriodicResourceMutationsCore(
            effect,
            triggerId,
            new EffectLifecycleEvent(
                eventRef,
                turn,
                Phase: exactEventKind,
                TriggerId: triggerId,
                CausalEventRef: producer.EventRef),
            targetAuthority,
            ownerAuthority,
            definitions,
            sourceAuthority,
            exactTrigger,
            componentsById,
            exactRemainingUseBudget,
            validateFullEffect,
            workMeter);
        if (!resolved.IsValid)
            return resolved;

        var producerRequirement = new ResourceMutationEventRequirement(
            producer,
            exactEventKind);
        return new EffectPeriodicResourceResolution(
            resolved.SourceExports,
            resolved.Mutations.Select(mutation => mutation with
            {
                EventRequirements = mutation.EventRequirements
                    .Append(producerRequirement)
                    .Distinct()
                    .ToArray()
            }).ToArray(),
            Array.Empty<ValidationIssue>())
        {
            PendingResolutions = resolved.PendingResolutions.Select(pending => pending with
            {
                EventRequirements = pending.EventRequirements
                    .Append(producerRequirement)
                    .Distinct()
                    .ToArray()
            }).ToArray(),
            ExecutedComponentIds = resolved.ExecutedComponentIds.ToArray(),
            ComponentIdsByMutation = new Dictionary<ResourceOperationKey, string>(
                resolved.ComponentIdsByMutation)
        };
    }

    internal static EffectPeriodicResourceResolution ResolveDuePeriodicResourceMutations(
        EffectAcceptedTurnPlan plan,
        ResourceOwnerAuthority ownerAuthority,
        ResourceDefinitionCatalog definitions)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(ownerAuthority);
        ArgumentNullException.ThrowIfNull(definitions);

        var issues = new List<ValidationIssue>();
        var triggerIndex = plan.ResourceTriggerIndex;
        issues.AddRange(triggerIndex.Issues);
        var sourceExports = new Dictionary<(string Kind, string Id), ResourceMutationSourceExport>();
        var mutations = new List<ResourceMutationIntent>();
        var triggerCandidates = new List<EffectResourceTriggerCandidate>();
        var triggerExecutions = new List<EffectResourceTriggerExecution>();
        var indexLookupCount = 0;
        var candidateVisitCount = 0;
        var sourceBindingIndexLookupCount = 0;
        var sourceBindingCandidateVisitCount = 0;
        var workMeter = new EffectResourceRoutingWorkMeter();
        if (issues.Count != 0)
        {
            return FailedPeriodicResourceResolution(issues) with
            {
                Work = workMeter.Snapshot(
                    indexLookupCount,
                    candidateVisitCount,
                    sourceBindingIndexLookupCount,
                    sourceBindingCandidateVisitCount)
            };
        }
        if (plan.EventInput["lifecycleEvents"] is not JsonArray lifecycleEvents)
            return new EffectPeriodicResourceResolution(
                Array.Empty<ResourceMutationSourceExport>(),
                Array.Empty<ResourceMutationIntent>(),
                issues)
            {
                Work = workMeter.Snapshot(
                    indexLookupCount,
                    candidateVisitCount,
                    sourceBindingIndexLookupCount,
                    sourceBindingCandidateVisitCount)
            };

        var deferredReactionsByActivation = plan.DeferredRoutingReactions
            .GroupBy(static reaction => (
                reaction.EffectId,
                reaction.TriggerId,
                reaction.EventKind,
                reaction.TriggerEventRef))
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<EffectReactionExecution>)
                    Array.AsReadOnly(group.ToArray()));

        foreach (var node in lifecycleEvents)
        {
            if (!TryParseLifecycleAuthority(node, issues, out var authority))
                continue;
            var candidates = triggerIndex.ResolveLifecycleEvent(
                authority.Target,
                authority.Phase,
                authority.EffectId,
                authority.TriggerId);
            indexLookupCount++;
            candidateVisitCount += candidates.Count;
            foreach (var candidate in candidates)
            {
                var routing = candidate.OpenRoutingHandle(workMeter);
                if ((authority.EffectId != null &&
                     !string.Equals(
                         routing.EffectId,
                         authority.EffectId,
                         StringComparison.Ordinal)) ||
                    routing.WasCreatedByCausalEvent(
                        authority.CausalEventRef))
                {
                    continue;
                }
                var triggerId = candidate.TriggerId;
                var pendingEffectAuthority = routing.ResolvePendingEffectAuthority(
                    authority.Turn);
                var sourceBinding = routing.ResolvePlanSourceBinding(
                    plan,
                    ref sourceBindingIndexLookupCount,
                    ref sourceBindingCandidateVisitCount);
                var acceptedEvent = new EffectLifecycleEvent(
                    CreateLifecycleResourceTriggerEventRef(
                        authority.EventRef,
                        routing.EffectId,
                        pendingEffectAuthority,
                        triggerId),
                    authority.Turn,
                    authority.Phase,
                    triggerId,
                    authority.CurrentTime,
                    authority.CurrentSceneId,
                    authority.SceneClosed,
                    authority.SourceSatisfied,
                    authority.ConditionSatisfied,
                    authority.CurrentRealm);
                var resolved = routing.ResolvePeriodic(
                    triggerId,
                    acceptedEvent,
                    plan.TargetAuthority,
                    ownerAuthority,
                    definitions,
                    sourceBinding,
                    candidate.RemainingUseBudget);
                issues.AddRange(resolved.Issues);
                if (!resolved.IsValid)
                    continue;
                deferredReactionsByActivation.TryGetValue(
                    (
                        routing.EffectId,
                        triggerId,
                        authority.Phase,
                        authority.EventRef),
                    out var reactionOutputs);
                reactionOutputs ??= Array.Empty<EffectReactionExecution>();
                var activation = new EffectActivationCandidate(
                    new EffectActivationCandidateIdentity(
                        routing.EffectId,
                        triggerId,
                        authority.Phase,
                        acceptedEvent.EventRef,
                        authority.EventRef),
                    candidate.Priority,
                    candidate.ConsumesUse,
                    pendingEffectAuthority);
                var materializedCandidate = new EffectResourceTriggerCandidate(
                    activation,
                    candidate.UseSeed,
                    null,
                    resolved.Mutations
                        .Select(static mutation => mutation.Key)
                        .ToArray(),
                    resolved.ExecutedComponentIds.ToArray(),
                    new Dictionary<ResourceOperationKey, string>(
                        resolved.ComponentIdsByMutation),
                    resolved.PendingResolutions,
                    reactionOutputs,
                    CreateCandidateOrigin(resolved),
                    effectAuthority: pendingEffectAuthority);
                triggerCandidates.Add(materializedCandidate);
                foreach (var source in resolved.SourceExports)
                {
                    var key = (source.SourceKind, source.SourceId);
                    if (sourceExports.TryGetValue(key, out var existing) && existing != source)
                    {
                        Add(
                            issues,
                            "effect.resourceSources",
                            "effect_resource_source_conflict",
                            "one exact source policy per effect/trigger/component",
                            source.SourceKind + "/" + source.SourceId);
                        continue;
                    }
                    sourceExports[key] = source;
                }
                mutations.AddRange(resolved.Mutations);
                triggerExecutions.Add(ProjectCompatibilityExecution(
                    materializedCandidate));
            }
        }

        return issues.Count == 0
            ? new EffectPeriodicResourceResolution(
                sourceExports.Values
                    .OrderBy(static source => source.SourceKind, StringComparer.Ordinal)
                    .ThenBy(static source => source.SourceId, StringComparer.Ordinal)
                    .ToArray(),
                mutations.ToArray(),
                Array.Empty<ValidationIssue>())
            {
                TriggerCandidates = triggerCandidates.ToArray(),
                TriggerExecutions = triggerExecutions.ToArray(),
                Work = workMeter.Snapshot(
                    indexLookupCount,
                    candidateVisitCount,
                    sourceBindingIndexLookupCount,
                    sourceBindingCandidateVisitCount)
            }
            : FailedPeriodicResourceResolution(issues) with
            {
                Work = workMeter.Snapshot(
                    indexLookupCount,
                    candidateVisitCount,
                    sourceBindingIndexLookupCount,
                    sourceBindingCandidateVisitCount)
            };
    }

    private static int? ReadRemainingUseBudget(
        JsonObject effect,
        string triggerId)
    {
        if (effect["lifetime"] is not JsonObject lifetime ||
            !string.Equals(
                lifetime["mode"]?.GetValue<string>(),
                "uses",
                StringComparison.Ordinal) ||
            !TryReadPositiveInt(lifetime["remainingUses"], out var remainingUses) ||
            lifetime["consumingTriggerIds"] is not JsonArray consumingTriggerIds ||
            !consumingTriggerIds.OfType<JsonValue>().Any(value =>
                value.TryGetValue<string>(out var candidate) &&
                string.Equals(candidate, triggerId, StringComparison.Ordinal)))
        {
            return null;
        }

        return remainingUses;
    }

    internal static EffectPeriodicResourceResolution ResolveResourceEventMutations(
        EffectAcceptedTurnPlan plan,
        ResourceAppliedEvent producerEvent,
        ResourceOperationKey producer,
        ResourceOwnerAuthority ownerAuthority,
        ResourceDefinitionCatalog definitions) =>
        ResolveResourceEventMutationCandidates(
            plan,
            producerEvent,
            producer,
            ownerAuthority,
            definitions);

    // Remove with EffectResourceRoutingBudgetSession when AcceptedMechanicsPlanner
    // consumes TriggerCandidates through the accepted-event arbiter.
    internal static EffectPeriodicResourceResolution ResolveResourceEventMutations(
        EffectAcceptedTurnPlan plan,
        ResourceAppliedEvent producerEvent,
        ResourceOperationKey producer,
        ResourceOwnerAuthority ownerAuthority,
        ResourceDefinitionCatalog definitions,
        EffectResourceRoutingBudgetSession routingBudgetSession)
    {
        ArgumentNullException.ThrowIfNull(routingBudgetSession);
        return ResolveResourceEventMutationCandidates(
            plan,
            producerEvent,
            producer,
            ownerAuthority,
            definitions);
    }

    private static EffectPeriodicResourceResolution
        ResolveResourceEventMutationCandidates(
        EffectAcceptedTurnPlan plan,
        ResourceAppliedEvent producerEvent,
        ResourceOperationKey producer,
        ResourceOwnerAuthority ownerAuthority,
        ResourceDefinitionCatalog definitions)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(producerEvent);
        ArgumentNullException.ThrowIfNull(producer);
        ArgumentNullException.ThrowIfNull(ownerAuthority);
        ArgumentNullException.ThrowIfNull(definitions);

        var issues = new List<ValidationIssue>();
        if (!EffectEventTypeCatalog.IsResourceEvent(producerEvent.EventKind) ||
            producerEvent.Turn <= 0 ||
            producerEvent.Coordinate != producer.Coordinate)
        {
            Add(
                issues,
                "effect.resourceEvent",
                "effect_resource_event_authority_invalid",
                "one exact emitted resource event bound to its producer coordinate",
                $"{producerEvent.EventKind}/{producerEvent.Turn}/{producerEvent.Coordinate}");
            return FailedPeriodicResourceResolution(issues);
        }

        var triggerIndex = plan.ResourceTriggerIndex;
        issues.AddRange(triggerIndex.Issues);
        var identityState = ParseIdentity(plan.IdentityIndexAfterImage);
        issues.AddRange(identityState.Issues);
        var carrierCatalog = EffectCarrierCatalog.Build(plan.ResourceTriggerCarriers);
        issues.AddRange(carrierCatalog.Issues);
        WoundReactionLineageAuthority? woundLineageAuthority = null;
        if (identityState.State != null && issues.Count == 0)
        {
            woundLineageAuthority = WoundReactionLineageAuthority.Build(
                plan.SourceAuthority,
                identityState.State,
                carrierCatalog,
                plan.WoundApplicationRootEffectBindings);
            issues.AddRange(woundLineageAuthority.Issues);
        }
        var sourceExports = new Dictionary<(string Kind, string Id), ResourceMutationSourceExport>();
        var mutations = new List<ResourceMutationIntent>();
        var triggerCandidates = new List<EffectResourceTriggerCandidate>();
        var executions = new List<EffectResourceTriggerExecution>();
        var sourceBindingIndexLookupCount = 0;
        var sourceBindingCandidateVisitCount = 0;
        var workMeter = new EffectResourceRoutingWorkMeter();
        if (issues.Count != 0)
        {
            return FailedPeriodicResourceResolution(issues) with
            {
                Work = workMeter.Snapshot(
                    indexLookupCount: 0,
                    candidateVisitCount: 0,
                    sourceBindingIndexLookupCount,
                    sourceBindingCandidateVisitCount)
            };
        }
        var candidates = triggerIndex.ResolveResourceEvent(
            producerEvent.Coordinate,
            producerEvent.EventKind);
        foreach (var candidate in candidates)
        {
            var routing = candidate.OpenRoutingHandle(workMeter);
            var triggerId = candidate.TriggerId;
            var plannedReactions = routing.PlanResourceEvent(
                triggerId,
                producerEvent,
                plan.SourceAuthority,
                woundLineageAuthority);
            issues.AddRange(plannedReactions.Issues);
            var sourceBinding = routing.ResolvePlanSourceBinding(
                plan,
                ref sourceBindingIndexLookupCount,
                ref sourceBindingCandidateVisitCount);
            var resolved = routing.ResolveResourceEvent(
                triggerId,
                producer,
                producerEvent,
                plan.TargetAuthority,
                ownerAuthority,
                definitions,
                sourceBinding,
                candidate.RemainingUseBudget);
            issues.AddRange(resolved.Issues);
            if (!resolved.IsValid)
                continue;
            var pendingEffectAuthority = routing.ResolvePendingEffectAuthority(
                producerEvent.Turn);
            var activation = new EffectActivationCandidate(
                new EffectActivationCandidateIdentity(
                    routing.EffectId,
                    triggerId,
                    producerEvent.EventKind,
                    routing.CreateResourceEventActivationRef(
                        triggerId,
                        producer,
                        producerEvent.EventKind,
                        producerEvent.Turn),
                    producerEvent.EventRef),
                candidate.Priority,
                candidate.ConsumesUse,
                pendingEffectAuthority);
            var materializedCandidate = new EffectResourceTriggerCandidate(
                activation,
                candidate.UseSeed,
                producer,
                resolved.Mutations
                    .Select(static mutation => mutation.Key)
                    .ToArray(),
                resolved.ExecutedComponentIds.ToArray(),
                new Dictionary<ResourceOperationKey, string>(
                    resolved.ComponentIdsByMutation),
                resolved.PendingResolutions,
                plannedReactions.Executions,
                CreateCandidateOrigin(resolved),
                effectAuthority: pendingEffectAuthority);
            triggerCandidates.Add(materializedCandidate);
            foreach (var source in resolved.SourceExports)
            {
                var key = (source.SourceKind, source.SourceId);
                if (sourceExports.TryGetValue(key, out var existing) && existing != source)
                {
                    Add(
                        issues,
                        "effect.resourceSources",
                        "effect_resource_source_conflict",
                        "one exact source policy per effect/trigger/component",
                        source.SourceKind + "/" + source.SourceId);
                    continue;
                }
                sourceExports[key] = source;
            }
            mutations.AddRange(resolved.Mutations);
            if (resolved.Mutations.Count != 0)
            {
                executions.Add(ProjectCompatibilityExecution(
                    materializedCandidate));
            }
        }

        return issues.Count == 0
            ? new EffectPeriodicResourceResolution(
                sourceExports.Values
                    .OrderBy(static source => source.SourceKind, StringComparer.Ordinal)
                    .ThenBy(static source => source.SourceId, StringComparer.Ordinal)
                    .ToArray(),
                mutations.ToArray(),
                Array.Empty<ValidationIssue>())
            {
                TriggerCandidates = triggerCandidates.ToArray(),
                TriggerExecutions = executions.ToArray(),
                Work = workMeter.Snapshot(
                    indexLookupCount: 1,
                    candidateVisitCount: candidates.Count,
                    sourceBindingIndexLookupCount:
                        sourceBindingIndexLookupCount,
                    sourceBindingCandidateVisitCount:
                        sourceBindingCandidateVisitCount)
            }
            : FailedPeriodicResourceResolution(issues) with
            {
                Work = workMeter.Snapshot(
                    indexLookupCount: 1,
                    candidateVisitCount: candidates.Count,
                    sourceBindingIndexLookupCount:
                        sourceBindingIndexLookupCount,
                    sourceBindingCandidateVisitCount:
                        sourceBindingCandidateVisitCount)
            };
    }

    internal static EffectAcceptedTurnPlanningResult CompleteAcceptedBoundaryTranscript(
        EffectAcceptedTurnPlan plan,
        AcceptedEffectBoundaryTranscript transcript,
        EffectIdentityFactory identityFactory)
    {
        if (plan == null || transcript == null || identityFactory == null)
        {
            var missingIssues = new List<ValidationIssue>();
            Add(
                missingIssues,
                EffectAcceptedTurnPlan.CommandPath,
                "effect_boundary_transcript_missing",
                "one accepted plan, validated boundary transcript, and identity factory",
                "null");
            return Failed(missingIssues);
        }
        if (plan.IsAcceptedBoundaryComplete)
        {
            var repeatedIssues = new List<ValidationIssue>();
            Add(
                repeatedIssues,
                EffectAcceptedTurnPlan.CommandPath,
                "effect_boundary_completion_repeated",
                "one initial effect plan completed exactly once",
                "already completed");
            return Failed(repeatedIssues);
        }
        var expectedPlanAuthority =
            AcceptedMechanicsPlanner.CreateEffectPlanAuthority(plan);
        if (!transcript.IsComplete ||
            transcript.PlanAuthority != expectedPlanAuthority)
        {
            var mismatchIssues = new List<ValidationIssue>();
            Add(
                mismatchIssues,
                EffectAcceptedTurnPlan.CommandPath,
                "effect_boundary_transcript_plan_mismatch",
                "one complete transcript with the exact five-field accepted effect plan authority including skill scope",
                transcript.IsComplete
                    ? transcript.PlanAuthority?.InputFingerprint ?? "missing"
                    : "pending-frontier");
            return Failed(mismatchIssues);
        }
        var appliedByActivation = transcript.AppliedComponentEvidence
            .GroupBy(static evidence => evidence.Activation)
            .ToDictionary(static group => group.Key, static group => group.ToArray());
        var executions = transcript.AcceptedActivations
            .OrderBy(static accepted =>
                accepted.Activation.Stamp.ActivationOrdinal)
            .Select(accepted =>
            {
                appliedByActivation.TryGetValue(
                    accepted.Activation.Stamp.Identity,
                    out var evidence);
                evidence ??= Array.Empty<AppliedEffectComponentEvidence>();
                var componentMap = evidence.ToDictionary(
                    static value => value.Mutation,
                    static value => value.ComponentId);
                return new EffectResourceTriggerExecution(
                    accepted.Activation.Stamp.Identity.EffectId,
                    accepted.Activation.Stamp.Identity.TriggerId,
                    accepted.Activation.Stamp.Identity.EventKind,
                    accepted.Activation.Stamp.Identity.EventRef,
                    componentMap.Keys.ToArray(),
                    accepted.Activation.Stamp.UsesBefore,
                    componentMap.Values
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(static value => value, StringComparer.Ordinal)
                        .ToArray(),
                    accepted.Activation.Stamp.Identity.TriggerEventRef,
                    componentMap);
            })
            .ToArray();
        return FinalizeAfterResourceGraphCore(
            plan,
            executions,
            identityFactory,
            transcript);
    }

    private static EffectAcceptedTurnPlanningResult FinalizeAfterResourceGraphCore(
        EffectAcceptedTurnPlan plan,
        IReadOnlyList<EffectResourceTriggerExecution> executedTriggers,
        EffectIdentityFactory identityFactory,
        AcceptedEffectBoundaryTranscript authoritativeTranscript)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(executedTriggers);
        ArgumentNullException.ThrowIfNull(identityFactory);
        ArgumentNullException.ThrowIfNull(authoritativeTranscript);

        var issues = new List<ValidationIssue>();
        var eventInput = plan.EventInput;
        if (!TryReadPositiveInt(eventInput["turn"], out var turn))
        {
            Add(
                issues,
                "eventInput.turn",
                "effect_plan_event_authority_invalid",
                "positive accepted turn",
                Describe(eventInput["turn"]));
            return Failed(issues);
        }

        var workspace = new CarrierWorkspace(plan.ResourceTriggerCarriers);
        using var identityRoot = new EffectIdentityHistoryOwner(
            plan.IdentityIndexAfterImage, identityFactory,
            plan.AllocatedEffectIds, plan.AllocatedTransitionIds);
        identityFactory = identityRoot.Factory;
        var identityState = ParseIdentity(identityRoot.ReadSnapshot());
        issues.AddRange(identityState.Issues);
        if (identityState.State == null || issues.Count != 0)
            return Failed(issues);
        var processedEventRefs = identityState.State.Entries
            .SelectMany(static entry => entry.Transitions)
            .Select(static transition => transition.EventRef)
            .ToHashSet(StringComparer.Ordinal);
        var transitionIds = plan.AllocatedTransitionIds.ToList();
        var effectIds = plan.AllocatedEffectIds.ToList();
        var activeEffects = plan.ActiveEffects
            .Select(static effect => effect.DeepClone().AsObject())
            .ToList();
        var usedSources = plan.SourceBindings.ToList();
        var usedTargets = plan.Targets.ToList();
        var executionKeys = new HashSet<string>(StringComparer.Ordinal);
        var acceptedExecutions = new List<EffectResourceTriggerExecution>();
        var deferredReactions = new List<EffectReactionExecution>();
        var reactionEventRefs = new HashSet<string>(StringComparer.Ordinal);
        var reactionSourceBindings = new Dictionary<
            EffectSourceKey,
            EffectSourceAuthorityEntry>();
        var authoritativeReleasedReactions = authoritativeTranscript
            .ReleasedReactions
            .OrderBy(static value => value.MechanicsOrdinal)
            .ToArray();
        var acceptedStamps = authoritativeTranscript.AcceptedActivations
            .Select(static accepted => accepted.Activation.Stamp)
            .ToHashSet();
        foreach (var released in authoritativeReleasedReactions)
        {
            var reaction = released.Reaction;
            if (!acceptedStamps.Contains(released.Activation) ||
                !string.Equals(
                    released.Activation.Identity.EffectId,
                    reaction.EffectId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    released.Activation.Identity.TriggerId,
                    reaction.TriggerId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    released.Activation.Identity.EventKind,
                    reaction.EventKind,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    released.Activation.Identity.TriggerEventRef,
                    reaction.TriggerEventRef,
                    StringComparison.Ordinal))
            {
                Add(
                    issues,
                    "effect.reactions",
                    "effect_reaction_activation_authority_invalid",
                    "one exact accepted activation stamp for every released reaction",
                    reaction.EventRef);
                continue;
            }
            if (!ReactionStageMatchesDependency(
                    released.Stage,
                    reaction.Dependency))
            {
                Add(
                    issues,
                    "effect.reactions",
                    "effect_reaction_release_stage_invalid",
                    reaction.Dependency,
                    released.Stage.ToString());
                continue;
            }
            if (!reactionEventRefs.Add(reaction.EventRef))
            {
                Add(
                    issues,
                    "effect.reactions",
                    "effect_reaction_event_duplicate",
                    "one exact reaction execution per derived event identity",
                    reaction.EventRef);
                continue;
            }
            deferredReactions.Add(reaction);
        }
        var reactionExpansionUsage = authoritativeTranscript.ExpansionUsage
            .ToDictionary(static pair => pair.Key, static pair => pair.Value);
        foreach (var group in deferredReactions.GroupBy(static reaction =>
                     new EffectReactionExpansionKey(
                         reaction.EffectId,
                         reaction.ComponentId)))
        {
            if (!reactionExpansionUsage.TryGetValue(
                    group.Key,
                    out var usage) ||
                usage.Count != group.Count() ||
                group.Any(reaction =>
                    reaction.MaxExpansion != usage.Maximum) ||
                usage.Count > usage.Maximum)
            {
                Add(
                    issues,
                    "effect.reactions",
                    "effect_reaction_expansion_authority_invalid",
                    "exact released-reaction count and source maximum",
                    group.Key.EffectId + "/" + group.Key.ComponentId);
            }
        }
        if (reactionExpansionUsage.Values.Sum(static usage => usage.Count) !=
            deferredReactions.Count)
        {
            Add(
                issues,
                "effect.reactions",
                "effect_reaction_expansion_authority_invalid",
                "expansion usage accounting for every released reaction exactly once",
                authoritativeTranscript.ExpansionCount.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
        }
        var totalReactionExpansion = reactionExpansionUsage.Values.Sum(
            static usage => (long)usage.Count);
        if (totalReactionExpansion > EffectReactionContract.MaximumExpansion)
        {
            Add(
                issues,
                "effect.reactions",
                "effect_reaction_expansion_exceeded",
                $"at most {EffectReactionContract.MaximumExpansion} reaction operations in one accepted transition",
                totalReactionExpansion.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
        }
        if (issues.Count != 0)
            return Failed(issues);

        foreach (var execution in executedTriggers)
        {
            var executionKey = string.Join(
                "\0",
                execution.EffectId,
                execution.TriggerId,
                execution.EventKind,
                execution.EventRef);
            if (!executionKeys.Add(executionKey))
            {
                Add(
                    issues,
                    "effect.resourceTriggerExecutions",
                    "effect_resource_trigger_execution_duplicate",
                    "one exact applied trigger activation",
                    executionKey.Replace('\0', '/'));
                continue;
            }
            acceptedExecutions.Add(execution);
        }
        if (issues.Count != 0)
            return Failed(issues);

        var preMutationCatalog = EffectCarrierCatalog.Build(
            workspace.ToInput());
        issues.AddRange(preMutationCatalog.Issues);
        var woundLineageAuthority = WoundReactionLineageAuthority.Build(
            plan.SourceAuthority,
            identityState.State,
            preMutationCatalog,
            plan.WoundApplicationRootEffectBindings);
        issues.AddRange(woundLineageAuthority.Issues);
        var reactionApplicationPlans =
            PrepareReleasedReactionApplicationPlans(
                authoritativeReleasedReactions,
                preMutationCatalog,
                plan.SourceAuthority,
                woundLineageAuthority,
                plan.SkillScopeAuthority,
                issues);
        if (issues.Count != 0)
            return Failed(issues);
        var reactionApplicationResults = new Dictionary<
            string,
            ReactionApplicationResult>(StringComparer.Ordinal);

        foreach (var execution in acceptedExecutions.Where(static execution =>
                     !execution.RemainingUseBudget.HasValue))
        {
            ApplyNonConsumingTriggerEvidence(
                execution,
                turn,
                workspace,
                identityRoot,
                identityFactory,
                transitionIds,
                activeEffects,
                processedEventRefs,
                issues);
        }
        if (issues.Count != 0)
            return Failed(issues);

        var preReactionCatalog = EffectCarrierCatalog.Build(workspace.ToInput());
        issues.AddRange(preReactionCatalog.Issues);
        var preReactionEffects = new Dictionary<string, JsonObject>(
            StringComparer.Ordinal);
        foreach (var occurrence in preReactionCatalog.Occurrences)
        {
            if (!preReactionEffects.TryAdd(
                    occurrence.EffectId,
                    occurrence.Effect.DeepClone().AsObject()))
            {
                Add(
                    issues,
                    "effect.resourceTriggerExecutions",
                    "effect_resource_trigger_identity_target_ambiguous",
                    "one exact pre-reaction active effect per identity",
                    occurrence.EffectId);
            }
        }
        if (issues.Count != 0)
            return Failed(issues);

        var reactionsToApplyBeforeProjection = authoritativeReleasedReactions
            .Where(static released =>
                !IsTerminalAvailabilityReaction(released.Reaction))
            .Select(static released => released.Reaction)
            .ToList();
        foreach (var reaction in reactionsToApplyBeforeProjection)
        {
            ApplyReactionExecution(
                reaction,
                reaction.Target.Realm,
                eventInput,
                workspace,
                identityRoot,
                identityFactory,
                effectIds,
                transitionIds,
                activeEffects,
                usedSources,
                usedTargets,
                processedEventRefs,
                issues,
                plan.SourceAuthority,
                reactionSourceBindings,
                reactionApplicationPlans,
                reactionApplicationResults,
                plan.SkillScopeAuthority);
            if (issues.Count != 0)
                return Failed(issues);
        }
        ValidateReleasedReplacementAuthority(
            authoritativeReleasedReactions,
            reactionApplicationPlans,
            reactionApplicationResults,
            identityRoot,
            issues);
        if (issues.Count != 0)
            return Failed(issues);

        foreach (var execution in acceptedExecutions)
        {
            if (!execution.RemainingUseBudget.HasValue)
                continue;

            var catalog = EffectCarrierCatalog.Build(workspace.ToInput());
            issues.AddRange(catalog.Issues);
            if (!catalog.TryResolveOne(execution.EffectId, out var occurrence))
            {
                if (TryApplyConsumingTriggerEvidenceBeforeReplacement(
                        execution,
                        turn,
                        preReactionEffects,
                        authoritativeReleasedReactions,
                        identityRoot,
                        identityFactory,
                        transitionIds,
                        processedEventRefs,
                        issues))
                {
                    continue;
                }
                Add(
                    issues,
                    "effect.resourceTriggerExecutions",
                    "effect_resource_trigger_lifetime_target_unresolved",
                    "one exact active effect for an applied consuming trigger",
                    execution.EffectId);
                continue;
            }
            var lifetime = occurrence.Effect["lifetime"] as JsonObject;
            if (lifetime == null ||
                !string.Equals(
                    lifetime["mode"]?.GetValue<string>(),
                    "uses",
                    StringComparison.Ordinal) ||
                !TryReadPositiveInt(
                    lifetime["remainingUses"],
                    out var currentRemainingUses) ||
                currentRemainingUses != execution.RemainingUseBudget.Value)
            {
                Add(
                    issues,
                    "effect.resourceTriggerExecutions",
                    "effect_activation_replay_budget_mismatch",
                    "the exact accepted uses-before stamp at transcript projection",
                    execution.EffectId + "/" +
                    execution.RemainingUseBudget.Value.ToString(
                        System.Globalization.CultureInfo.InvariantCulture) + "/" +
                    Describe(lifetime?["remainingUses"]));
                continue;
            }
            ApplyLifecycleReduction(
                occurrence,
                new EffectLifecycleEvent(
                    execution.EventRef,
                    turn,
                    execution.EventKind,
                    execution.TriggerId),
                workspace,
                identityRoot,
                identityFactory,
                transitionIds,
                activeEffects,
                processedEventRefs,
                issues);
        }
        if (issues.Count != 0)
            return Failed(issues);

        ApplyDueLifecycleEvents(
            eventInput,
            sourceAuthority: null,
            workspace,
            identityRoot,
            identityFactory,
            transitionIds,
            activeEffects,
            processedEventRefs,
            issues,
            boundContinuationsOnly: false);
        if (issues.Count != 0)
            return Failed(issues);

        ApplyAcceptedTerminalReactionFold(
            authoritativeReleasedReactions,
            eventInput,
            workspace,
            identityRoot,
            identityFactory,
            effectIds,
            transitionIds,
            activeEffects,
            usedSources,
            usedTargets,
            processedEventRefs,
            issues,
            plan.SourceAuthority,
            reactionSourceBindings);
        if (issues.Count != 0)
            return Failed(issues);
        workspace.FinalizeAcceptedSpiritualConflict(
            plan.AcceptedCarrierBaselines.SpiritualConflict);
        ValidateAfterImages(workspace, identityRoot, activeEffects, issues);
        if (issues.Count != 0)
            return Failed(issues);

        var afterImages = plan.CarrierAfterImages.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.DeepClone().AsObject(),
            StringComparer.Ordinal);
        foreach (var pair in workspace.AfterImages)
            afterImages[pair.Key] = pair.Value.DeepClone().AsObject();
        var touchedPaths = plan.TouchedPaths
            .Concat(afterImages.Keys)
            .Append(EffectAcceptedTurnPlan.IdentityIndexPath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

        return new EffectAcceptedTurnPlanningResult(
            new EffectAcceptedTurnPlan(
                plan.InputFingerprint,
                plan.CarrierAuthorityFingerprint,
                plan.SourceAuthorityFingerprint,
                plan.TargetAuthorityFingerprint,
                plan.AllocatedCombatantIds,
                identityRoot.AllocatedEffectIds,
                identityRoot.AllocatedTransitionIds,
                usedSources
                    .Select(static entry => entry.Key)
                    .Distinct()
                    .ToArray(),
                usedTargets
                    .Distinct()
                    .ToArray(),
                usedSources
                    .DistinctBy(static entry => entry.Key)
                    .ToArray(),
                Array.Empty<EffectReactionExecution>(),
                checked((int)totalReactionExpansion),
                reactionExpansionUsage,
                activeEffects,
                workspace.ToInput(),
                plan.SourceAuthority,
                plan.TargetAuthority,
                eventInput,
                plan.CarrierBeforeImages,
                afterImages,
                plan.IdentityIndexBeforeImage,
                identityRoot.Publish(),
                touchedPaths,
                plan.DeletedPaths,
                acceptedCarrierBaselines: plan.AcceptedCarrierBaselines,
                acceptedBoundaryCompletionProof:
                    AcceptedBoundaryCompletionAuthority,
                acceptedBoundaryBasePlanFingerprint:
                    WoundAcceptedTurnFingerprints
                        .ComputeAcceptedEffectPlanPayload(plan),
                woundApplicationRootEffectBindings:
                    plan.WoundApplicationRootEffectBindings,
                skillScopeAuthority: plan.SkillScopeAuthority),
            Array.Empty<ValidationIssue>());
    }

    private static PreparedWoundOperations PrepareWoundOperations(
        EffectAcceptedTurnInput input,
        WoundPreparedAcceptedTurnPlan prepared,
        List<ValidationIssue> issues)
    {
        var applications = new List<WoundApplicationRequest>();
        var terminations = new List<WoundTerminalRequest>();
        var binding = prepared.Binding;
        var batches = prepared.EffectOperationBatches;
        var preparedWounds = prepared.PreparedWounds;
        var allocatedWoundIds = prepared.AllocatedWoundIds;
        if (binding is null ||
            batches is null ||
            preparedWounds is null ||
            allocatedWoundIds is null ||
            input.RawCommands is null ||
            input.SourceAuthority is null ||
            input.TargetAuthority is null ||
            input.EventInput is null)
        {
            AddWoundBatchIssue(
                issues,
                "prepared",
                "wound_plan_effect_handoff_invalid",
                "complete detached prepared plan and ordinary effect input",
                "missing nested authority");
            return new PreparedWoundOperations(applications, terminations);
        }

        if (!string.Equals(input.SessionId, binding.SessionId, StringComparison.Ordinal) ||
            !string.Equals(
                input.SnapshotToken,
                binding.SnapshotToken,
                StringComparison.Ordinal) ||
            !string.Equals(input.Realm, binding.Realm, StringComparison.Ordinal) ||
            !TryReadPositiveInt(input.EventInput["turn"], out var turn) ||
            turn != binding.Turn)
        {
            AddWoundBatchIssue(
                issues,
                "effectInput",
                "wound_plan_effect_handoff_invalid",
                "effect input bound to the exact prepared session, snapshot, realm, and turn",
                $"{input.SessionId}/{input.SnapshotToken}/{input.Realm}/{Describe(input.EventInput["turn"])}");
        }

        if (!WoundAcceptedTurnPlannerCore.SameTurnWoundAuthorityAgrees(
                prepared,
                input.SourceAuthority))
        {
            AddWoundBatchIssue(
                issues,
                "effectInput.sourceAuthority",
                "wound_plan_effect_handoff_invalid",
                "the exact prepared same-turn wound source set and no additional wound authority",
                "same-turn wound source authority mismatch");
            return new PreparedWoundOperations(applications, terminations);
        }

        if (WoundAcceptedTurnPlannerCore.IsEmptyWoundStage(prepared))
            return new PreparedWoundOperations(applications, terminations);

        var acceptedEvents = PrepareWoundAcceptedEvents(
            binding,
            input.EventInput["events"],
            issues);
        var allocatedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var woundId in allocatedWoundIds)
        {
            if (!TryExact(woundId) || !allocatedIds.Add(woundId))
            {
                AddWoundBatchIssue(
                    issues,
                    "prepared.allocatedWoundIds",
                    "wound_plan_effect_handoff_invalid",
                    "exact unique allocated wound identities",
                    woundId ?? "missing");
            }
        }

        var woundsById = new Dictionary<
            string,
            WoundMaterializationEnvelope>(StringComparer.Ordinal);
        for (var index = 0; index < preparedWounds.Count; index++)
        {
            var wound = preparedWounds[index];
            if (wound is null ||
                !TryExact(wound.WoundId) ||
                !woundsById.TryAdd(wound.WoundId, wound))
            {
                AddWoundBatchIssue(
                    issues,
                    $"prepared.preparedWounds[{index}]",
                    "wound_plan_effect_handoff_invalid",
                    "one exact unique prepared wound",
                    wound?.WoundId ?? "missing");
            }
        }

        var applicationRefs = new HashSet<string>(StringComparer.Ordinal);
        var applicationAliases = new HashSet<string>(StringComparer.Ordinal);
        var createdEvents = acceptedEvents.Keys.ToHashSet(StringComparer.Ordinal);
        var createdEventAliases = acceptedEvents.Keys
            .Select(MortalLocationIdentityState.BuildConfusableKey)
            .ToHashSet(StringComparer.Ordinal);
        var terminalEffectIds = new HashSet<string>(StringComparer.Ordinal);
        for (var batchIndex = 0; batchIndex < batches.Count; batchIndex++)
        {
            var batch = batches[batchIndex];
            var path = $"prepared.effectOperationBatches[{batchIndex}]";
            if (batch is null)
            {
                AddWoundBatchIssue(
                    issues,
                    path,
                    "wound_plan_effect_handoff_invalid",
                    "complete typed wound effect batch",
                    "null");
                continue;
            }

            var actualSourceExportFingerprint =
                WoundAcceptedTurnFingerprints.ComputeSourceExport(batch);
            if (!string.Equals(
                    actualSourceExportFingerprint,
                    batch.SourceExportFingerprint,
                    StringComparison.Ordinal))
            {
                AddWoundBatchIssue(
                    issues,
                    path + ".sourceExportFingerprint",
                    "wound_plan_prepared_seal_mismatch",
                    "the exact recomputed detached source export fingerprint",
                    batch.SourceExportFingerprint);
                continue;
            }

            PrepareWoundBatchApplications(
                input,
                prepared,
                batch,
                path,
                allocatedIds,
                woundsById,
                acceptedEvents,
                applicationRefs,
                applicationAliases,
                createdEvents,
                createdEventAliases,
                terminalEffectIds,
                applications,
                terminations,
                issues);
        }
        return new PreparedWoundOperations(applications, terminations);
    }

    private static IReadOnlyDictionary<string, WoundAcceptedEventAuthority>
        PrepareWoundAcceptedEvents(
            WoundAcceptedTurnBinding binding,
            JsonNode? inputEvents,
            List<ValidationIssue> issues)
    {
        var result = new Dictionary<
            string,
            WoundAcceptedEventAuthority>(StringComparer.Ordinal);
        var accepted = binding.AcceptedEvents;
        if (accepted is null ||
            inputEvents is not JsonArray events ||
            events.Count != accepted.Count)
        {
            AddWoundBatchIssue(
                issues,
                "effectInput.eventInput.events",
                "wound_plan_effect_handoff_invalid",
                "the exact ordered prepared accepted-event set",
                Describe(inputEvents));
            return result;
        }

        var aliases = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < accepted.Count; index++)
        {
            var authority = accepted[index];
            var node = events[index];
            var path = $"effectInput.eventInput.events[{index}]";
            if (authority is null ||
                !TryExact(authority.EventRef) ||
                !TryExact(authority.Kind) ||
                !TryExact(authority.AuthorityId) ||
                !TryExact(authority.SemanticFingerprint) ||
                node is not JsonObject value ||
                !HasOnly(value, "kind", "authorityId", "eventRef") ||
                !TryReadExact(value["eventRef"], out var eventRef) ||
                !TryReadExact(value["kind"], out var kind) ||
                !TryReadExact(value["authorityId"], out var authorityId) ||
                !string.Equals(eventRef, authority.EventRef, StringComparison.Ordinal) ||
                !string.Equals(kind, authority.Kind, StringComparison.Ordinal) ||
                !string.Equals(
                    authorityId,
                    authority.AuthorityId,
                    StringComparison.Ordinal) ||
                !result.TryAdd(authority.EventRef, authority) ||
                !aliases.Add(MortalLocationIdentityState.BuildConfusableKey(
                    authority.EventRef)))
            {
                AddWoundBatchIssue(
                    issues,
                    path,
                    "wound_plan_effect_handoff_invalid",
                    "one exact/confusable-unique prepared accepted event at the same ordinal",
                    Describe(node));
            }
        }
        return result;
    }

    private static void PrepareWoundBatchApplications(
        EffectAcceptedTurnInput input,
        WoundPreparedAcceptedTurnPlan prepared,
        WoundEffectOperationBatch batch,
        string path,
        IReadOnlySet<string> allocatedWoundIds,
        IReadOnlyDictionary<string, WoundMaterializationEnvelope> woundsById,
        IReadOnlyDictionary<string, WoundAcceptedEventAuthority> acceptedEvents,
        HashSet<string> applicationRefs,
        HashSet<string> applicationAliases,
        HashSet<string> createdEvents,
        HashSet<string> createdEventAliases,
        HashSet<string> terminalEffectIds,
        List<WoundApplicationRequest> applications,
        List<WoundTerminalRequest> terminations,
        List<ValidationIssue> issues)
    {
        var sourceExport = batch.SourceExport;
        var roots = batch.RootApplications;
        var terminals = batch.TerminalOperations;
        var lineage = batch.RootLineageAuthority;
        var transitionAuthority = batch.TransitionAuthority;
        if (sourceExport is null ||
            roots is null ||
            terminals is null ||
            lineage is null ||
            transitionAuthority is null ||
            !TryExact(batch.LocalWoundRef) ||
            !TryExact(batch.PreparedWoundId) ||
            !allocatedWoundIds.Contains(batch.PreparedWoundId) ||
            !woundsById.TryGetValue(batch.PreparedWoundId, out var wound))
        {
            AddWoundBatchIssue(
                issues,
                path,
                "wound_plan_effect_handoff_invalid",
                "complete typed batch for one exact allocated prepared wound",
                $"{batch.LocalWoundRef}/{batch.PreparedWoundId}");
            return;
        }
        var terminalWound = wound;
        var hasSelectedGraph = WoundAcceptedTurnPlanner.TryGetTreatmentSelectedGraph(prepared, out var selectedGraph);
        if (WoundAcceptedTurnPlanner.TryReadTreatmentContinuation(prepared.TreatmentContinuationAuthority!, out var selectedContinuation) &&
            selectedContinuation.Resolution.OutcomeIntents.Any(static intent => intent is MortalWoundAddComplicationOutcomeIntent) && !hasSelectedGraph)
        {
            AddWoundBatchIssue(issues, path, "wound_plan_effect_handoff_invalid",
                "one authenticated private selected complication graph", "missing or changed");
            return;
        }
        var authenticatedRemoval = WoundAcceptedTurnPlanner.TryReadTreatmentContinuation(
                prepared.TreatmentContinuationAuthority!, out var treatmentContinuation) &&
            WoundAcceptedTurnPlanner.TreatmentContinuationPreparedAgrees(prepared) &&
            treatmentContinuation.OutcomePreparation.GetOrderedRemovalIds().Length != 0;
        var removalOnly = !hasSelectedGraph && authenticatedRemoval && treatmentContinuation.OutcomePreparation.SeverityReduction is null;
        if (transitionAuthority.TransitionKind is "worsen" or "treat")
        {
            var baselineCatalog = WoundCarrierCatalog.Build(
                prepared.BaselineAuthority.PreTurnCarriers);
            var matches = baselineCatalog.Occurrences.Where(value =>
                    string.Equals(
                        value.WoundId,
                        batch.PreparedWoundId,
                        StringComparison.Ordinal))
                .ToArray();
            if (baselineCatalog.Issues.Count != 0 ||
                matches.Length != 1 ||
                !string.Equals(
                    WoundIdentityState.ComputeSemanticFingerprint(
                        matches[0].Wound),
                    transitionAuthority.ExpectedBeforeFingerprint,
                    StringComparison.Ordinal))
            {
                AddWoundBatchIssue(
                    issues,
                    path + ".terminalOperations",
                    "wound_plan_effect_handoff_invalid",
                    "the exact sealed pre-turn wound selected for rematerialization teardown",
                    batch.PreparedWoundId);
                return;
            }
            terminalWound = matches[0].Wound;
        }
        var retainedDefinitionKeys = sourceExport.Definitions
            .Select(static value => value.DefinitionKey)
            .ToHashSet(StringComparer.Ordinal);
        var fullRematerialization = hasSelectedGraph ? selectedGraph!.FinalSeverityChanged : roots.Count != 0 &&
            transitionAuthority.TransitionKind is "worsen" or "treat";
        IReadOnlyList<string> selectedTerminalRoots = terminalWound.Consequences.OwnedEffectSources
            .RootBindings.Select(static binding => binding.EffectId).ToArray();
        if (removalOnly)
        {
            var selected = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var rootsSeen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in treatmentContinuation.OutcomePreparation.GetOrderedRemovalIds())
            {
                var matches = terminalWound.Complications.Where(complication =>
                    string.Equals(complication.ComplicationId, id, StringComparison.Ordinal)).ToArray();
                if (!ids.Add(id) || matches.Length != 1 ||
                    matches[0].OwnedEffectIds.Any(rootId => !rootsSeen.Add(rootId) ||
                        !selectedTerminalRoots.Contains(rootId, StringComparer.Ordinal)))
                {
                    AddWoundBatchIssue(issues, path + ".terminalOperations",
                        "wound_plan_effect_handoff_invalid",
                        "exact unique complications and declared roots from the sealed pre-turn wound", id);
                    return;
                }
                selected.AddRange(matches[0].OwnedEffectIds);
            }
            selectedTerminalRoots = selected.ToArray();
        }
        if (hasSelectedGraph) selectedTerminalRoots = selectedGraph!.SelectedTerminalRootIds;
        var expectedExistingLineageCount = hasSelectedGraph
            ? selectedGraph!.FinalSeverityChanged ? 0 : selectedGraph.FinalRoots.Count(row => row.OriginalEffectId is not null)
            : (terminals.Count == 0 && !removalOnly) ||
                                           fullRematerialization
            ? 0
            : terminalWound.Consequences.OwnedEffectSources.RootBindings.Count(
                value => retainedDefinitionKeys.Contains(value.DefinitionKey));
        var expectedLineageCount = roots.Count + expectedExistingLineageCount;
        var existingLineage = lineage.Skip(roots.Count).ToArray();
        if (lineage.Count != expectedLineageCount ||
            expectedExistingLineageCount != 0 && !ExistingWoundLineageMatches(
                terminalWound,
                existingLineage,
                retainedDefinitionKeys))
        {
            AddWoundBatchIssue(
                issues,
                path + ".rootLineageAuthority",
                "wound_plan_effect_handoff_invalid",
                fullRematerialization
                    ? "ordered application roots only for full rematerialization"
                    : "ordered application roots followed by retained existing-root lineage",
                $"{lineage.Count}/{expectedLineageCount}");
        }

        if (!ValidateWoundSourceExport(
                prepared,
                batch,
                sourceExport,
                wound,
                transitionAuthority,
                acceptedEvents,
                path,
                issues))
        {
            return;
        }

        var definitions = new Dictionary<
            string,
            WoundEffectSourceDefinition>(StringComparer.Ordinal);
        var definitionAliases = new HashSet<string>(StringComparer.Ordinal);
        var sourceDefinitions = sourceExport.Definitions;
        if (sourceDefinitions is null)
        {
            AddWoundBatchIssue(
                issues,
                path + ".sourceExport.definitions",
                "wound_plan_effect_handoff_invalid",
                "ordered detached source definitions",
                "missing");
            return;
        }
        var definitionIssueCount = issues.Count;
        for (var index = 0; index < sourceDefinitions.Count; index++)
        {
            var definition = sourceDefinitions[index];
            if (definition is null ||
                !TryExact(definition.DefinitionKey) ||
                definition.Definition is not JsonObject definitionJson ||
                !TryReadExact(definitionJson["definitionKey"], out var embeddedKey) ||
                !string.Equals(
                    embeddedKey,
                    definition.DefinitionKey,
                    StringComparison.Ordinal) ||
                !definitions.TryAdd(definition.DefinitionKey, definition) ||
                !definitionAliases.Add(
                    MortalLocationIdentityState.BuildConfusableKey(
                        definition.DefinitionKey)))
            {
                AddWoundBatchIssue(
                    issues,
                    $"{path}.sourceExport.definitions[{index}]",
                    "wound_plan_effect_handoff_invalid",
                    "one exact/confusable-unique canonical definition key and body",
                    definition?.DefinitionKey ?? "missing");
            }
        }
        if (issues.Count != definitionIssueCount ||
            !ValidateWoundDefinitionAuthorities(
                input.SourceAuthority,
                sourceExport,
                batch.LocalWoundRef,
                definitions,
                path,
                issues))
        {
            return;
        }
        if (selectedGraph is not null && !ValidateSelectedDefinitionScopes(input, selectedGraph, definitions, issues))
            return;
        if (roots.Count != 0 && !ValidateGenerationPredecessors(
                input,
                transitionAuthority.TransitionKind,
                terminalWound,
                definitions,
                roots,
                path,
                issues,
                authenticatedRemoval ? treatmentContinuation.OutcomePreparation.SeverityReduction?.Before : null,
                selectedGraph))
        {
            return;
        }

        for (var rootIndex = 0; rootIndex < roots.Count; rootIndex++)
        {
            var root = roots[rootIndex];
            var rootPath = $"{path}.rootApplications[{rootIndex}]";
            if (root is null ||
                !TryExact(root.ApplicationRef) ||
                !applicationRefs.Add(root.ApplicationRef) ||
                !applicationAliases.Add(
                    MortalLocationIdentityState.BuildConfusableKey(
                        root.ApplicationRef)))
            {
                AddWoundBatchIssue(
                    issues,
                    rootPath + ".applicationRef",
                    "wound_plan_effect_handoff_invalid",
                    "globally exact/confusable-unique typed root applicationRef",
                    root?.ApplicationRef ?? "missing");
                continue;
            }
            if (rootIndex >= lineage.Count ||
                !WoundLineageMatches(lineage[rootIndex], root))
            {
                AddWoundBatchIssue(
                    issues,
                    rootPath,
                    "wound_plan_effect_handoff_invalid",
                    "exact ordered prepared root lineage authority",
                    root.ApplicationRef);
                continue;
            }
            if (!definitions.TryGetValue(root.DefinitionKey, out var definition))
            {
                AddWoundBatchIssue(
                    issues,
                    rootPath + ".definitionKey",
                    "wound_plan_effect_handoff_invalid",
                    "one exact allowlisted detached root definition",
                    root.DefinitionKey);
                continue;
            }

            var request = PrepareSelectedWoundApplication(
                input,
                batch,
                sourceExport,
                definition,
                root,
                acceptedEvents,
                rootPath,
                issues,
                selectedGraph);
            if (request is null)
                continue;
            if (!createdEvents.Add(request.CreatedEventRef) ||
                !createdEventAliases.Add(
                    MortalLocationIdentityState.BuildConfusableKey(
                        request.CreatedEventRef)))
            {
                AddWoundBatchIssue(
                    issues,
                    rootPath + ".createdEventRef",
                    "wound_plan_effect_handoff_invalid",
                    "globally exact/confusable-unique derived wound create event",
                    request.CreatedEventRef);
                continue;
            }
            applications.Add(request);
        }

        for (var terminalIndex = 0;
             terminalIndex < terminals.Count;
             terminalIndex++)
        {
            var operation = terminals[terminalIndex];
            var terminalPath =
                $"{path}.terminalOperations[{terminalIndex}]";
            if (!ValidatePreparedWoundTerminalOperation(
                    operation,
                    batch,
                    terminalWound,
                    acceptedEvents,
                    terminalPath,
                    issues))
            {
                continue;
            }
            if (!createdEvents.Add(operation.OperationRef) ||
                !createdEventAliases.Add(
                    MortalLocationIdentityState.BuildConfusableKey(
                        operation.OperationRef)) ||
                !terminalEffectIds.Add(operation.EffectId))
            {
                AddWoundBatchIssue(
                    issues,
                    terminalPath,
                    "wound_plan_effect_handoff_invalid",
                    "globally exact/confusable-unique terminal event and one terminal operation per effect",
                    operation.OperationRef + "/" + operation.EffectId);
                continue;
            }
            terminations.Add(new WoundTerminalRequest(
                batch,
                terminalWound,
                operation,
                selectedTerminalRoots.ToImmutableArray()));
        }
    }

    private static bool ValidatePreparedWoundTerminalOperation(
        WoundTerminalEffectOperation? operation,
        WoundEffectOperationBatch batch,
        WoundMaterializationEnvelope wound,
        IReadOnlyDictionary<string, WoundAcceptedEventAuthority> acceptedEvents,
        string path,
        List<ValidationIssue> issues)
    {
        if (operation is null)
        {
            AddWoundBatchIssue(
                issues,
                path,
                "wound_plan_effect_handoff_invalid",
                "one complete typed wound terminal operation",
                "null");
            return false;
        }
        var expectedEventRef = operation.MechanicsOrdinal > 0 &&
                               operation.OperationOrdinal > 0
            ? WoundEffectOperationEventRef.Create(
                operation.CausalEventRef,
                operation.MechanicsOrdinal,
                operation.OperationOrdinal,
                operation.OperationKind)
            : string.Empty;
        var targetIsExpected = WoundEffectCarrierAdapter.TryCreateTargetKey(
            wound.Owner,
            out var expectedTarget) &&
            operation.ExpectedTargetKey == expectedTarget;
        var valid =
            TryExact(operation.OperationRef) &&
            TryExact(operation.OperationKey) &&
            TryExact(operation.EffectId) &&
            operation.MechanicsOrdinal > 0 &&
            operation.OperationOrdinal > 0 &&
            string.Equals(
                operation.OperationKind,
                "expire",
                StringComparison.Ordinal) &&
            string.Equals(
                operation.OperationKey,
                batch.TransitionAuthority.OperationKey,
                StringComparison.Ordinal) &&
            string.Equals(
                operation.CausalEventRef,
                batch.SourceExport.CausalEventRef,
                StringComparison.Ordinal) &&
            acceptedEvents.ContainsKey(operation.CausalEventRef) &&
            string.Equals(
                operation.OperationRef,
                expectedEventRef,
                StringComparison.Ordinal) &&
            operation.ExpectedSourceKey == new EffectSourceKey(
                wound.Owner.Realm,
                "wound",
                wound.WoundId,
                operation.ExpectedSourceKey.DefinitionKey) &&
            targetIsExpected &&
            operation.ExpectedCarrierCoordinate is not null &&
            operation.ExpectedIdentityOwner is not null &&
            operation.ExpectedStackCoordinate is not null &&
            TryExact(operation.ExpectedCarrierFilePath) &&
            TryExact(operation.ExpectedCarrierJsonPath) &&
            ResourceMaterializationContract.IsAuthorityFingerprint(
                operation.ExpectedEffectFingerprint) &&
            ResourceMaterializationContract.IsAuthorityFingerprint(
                operation.ExpectedIdentityFingerprint) &&
            operation.OwnershipDomain is not null &&
            operation.OwnershipDomain.Kind is "base_wound" or "complication" &&
            (operation.OwnershipDomain.Kind == "base_wound"
                ? operation.OwnershipDomain.ComplicationId is null
                : operation.OwnershipDomain.ComplicationId is not null &&
                  TryExact(operation.OwnershipDomain.ComplicationId));
        if (valid)
            return true;

        AddWoundBatchIssue(
            issues,
            path,
            "wound_plan_effect_handoff_invalid",
            "one exact prepared expire operation bound to its wound transition, causal event, source, target, carrier, identity, stack, fingerprints, and ownership domain",
            operation.OperationRef + "/" + operation.EffectId);
        return false;
    }

    private static bool ValidateWoundDefinitionAuthorities(
        EffectSourceAuthority authority,
        WoundEffectSourceExport sourceExport,
        string localWoundRef,
        IReadOnlyDictionary<string, WoundEffectSourceDefinition> definitions,
        string path,
        List<ValidationIssue> issues)
    {
        if (!TryMapWoundOwnerTarget(sourceExport.Owner, out var targetKind))
        {
            AddWoundBatchIssue(
                issues,
                path + ".sourceExport.owner",
                "wound_plan_effect_handoff_invalid",
                "one narrowly supported wound owner target kind",
                sourceExport.Owner.OwnerKind);
            return false;
        }

        var issueCount = issues.Count;
        foreach (var pair in definitions)
        {
            var key = new EffectSourceKey(
                sourceExport.Realm,
                "wound",
                sourceExport.SourceId,
                pair.Key);
            var resolution = authority.ResolveCanonicalBinding(key, targetKind);
            issues.AddRange(resolution.Issues.Select(issue =>
                Prefix(issue, path + ".sourceExport.definitions")));
            var resolved = resolution.Source;
            if (!resolution.Success ||
                resolved is null ||
                resolved.Key != key ||
                !resolved.SameTurn ||
                resolved.Materializable ||
                !resolved.Active ||
                !string.Equals(
                    resolved.SourceRef,
                    localWoundRef,
                    StringComparison.Ordinal) ||
                !JsonNode.DeepEquals(
                    resolved.Definition,
                    pair.Value.Definition))
            {
                AddWoundBatchIssue(
                    issues,
                    path + ".sourceExport.definitions",
                    "wound_plan_effect_handoff_invalid",
                    "exact active same-turn non-materializable canonical authority for every detached wound definition",
                    key.ToString());
            }
        }
        return issues.Count == issueCount;
    }

    private static bool ValidateWoundSourceExport(
        WoundPreparedAcceptedTurnPlan prepared,
        WoundEffectOperationBatch batch,
        WoundEffectSourceExport source,
        WoundMaterializationEnvelope wound,
        WoundPreparedTransitionAuthority transition,
        IReadOnlyDictionary<string, WoundAcceptedEventAuthority> acceptedEvents,
        string path,
        List<ValidationIssue> issues)
    {
        var owner = source.Owner;
        var expectedOwner = wound.Owner;
        var origin = wound.Origin;
        var createAuthority = string.Equals(
            transition.TransitionKind,
            "create",
            StringComparison.Ordinal) &&
            origin is not null &&
            string.Equals(source.CausalEventRef, wound.Severity.LastChangeEventRef,
                StringComparison.Ordinal) &&
            transition.CauseKind is null &&
            transition.ExpectedBeforeFingerprint is null &&
            string.Equals(
                source.CausalEventRef,
                origin.EventRef,
                StringComparison.Ordinal);
        var worseningAuthority = string.Equals(
            transition.TransitionKind,
            "worsen",
            StringComparison.Ordinal) &&
            string.Equals(source.CausalEventRef, wound.Severity.LastChangeEventRef,
                StringComparison.Ordinal) &&
            transition.CauseKind is { } causeKind &&
            TryExact(causeKind) &&
            ResourceMaterializationContract.IsAuthorityFingerprint(
                transition.ExpectedBeforeFingerprint);
        var treatmentAuthority = string.Equals(
            transition.TransitionKind,
            "treat",
            StringComparison.Ordinal) &&
            prepared.TreatmentContinuationAuthority is not null &&
            WoundAcceptedTurnPlanner.TryReadTreatmentContinuation(
                prepared.TreatmentContinuationAuthority, out var continuation) &&
            string.Equals(source.CausalEventRef, continuation.Resolution.Coordinates.EventRef,
                StringComparison.Ordinal) &&
            WoundAcceptedTurnPlanner.TreatmentContinuationPreparedAgrees(prepared) &&
            ResourceMaterializationContract.IsAuthorityFingerprint(
                transition.ExpectedBeforeFingerprint);
        var valid =
            source.SchemaVersion == 1 &&
            string.Equals(source.Kind, "wound", StringComparison.Ordinal) &&
            string.Equals(
                source.SourceId,
                batch.PreparedWoundId,
                StringComparison.Ordinal) &&
            string.Equals(
                source.SourceRef,
                batch.LocalWoundRef,
                StringComparison.Ordinal) &&
            string.Equals(source.State, "active", StringComparison.Ordinal) &&
            !source.Materializable &&
            string.Equals(
                source.Realm,
                prepared.Binding.Realm,
                StringComparison.Ordinal) &&
            owner is not null &&
            expectedOwner is not null &&
            owner == expectedOwner &&
            origin is not null &&
            string.Equals(
                transition.TransitionKind,
                WoundAcceptedTurnPlanner.TryGetTreatmentSelectedGraph(prepared, out var selectedGraph)
                    ? selectedGraph!.FinalScalars.LastTransition.Kind : wound.LastTransition.Kind,
                StringComparison.Ordinal) &&
            (createAuthority || worseningAuthority || treatmentAuthority) &&
            string.Equals(
                transition.PreparedInputFingerprint,
                prepared.InputFingerprint,
                StringComparison.Ordinal) &&
            string.Equals(
                transition.OpportunityId,
                source.OpportunityId,
                StringComparison.Ordinal) &&
            string.Equals(
                transition.OpportunityAuthorityFingerprint,
                source.OpportunityAuthorityFingerprint,
                StringComparison.Ordinal) &&
            acceptedEvents.TryGetValue(
                source.CausalEventRef,
                out var acceptedEvent) &&
            string.Equals(
                acceptedEvent.SemanticFingerprint,
                source.EventSemanticFingerprint,
                StringComparison.Ordinal);
        if (!valid)
        {
            AddWoundBatchIssue(
                issues,
                path + ".sourceExport",
                "wound_plan_effect_handoff_invalid",
                "exact active non-materializable wound source export bound to its prepared wound, opportunity, owner, and accepted event",
                $"{source.Realm}/{source.Kind}/{source.SourceId}/{source.SourceRef}/{source.State}/{source.Materializable}");
        }
        return valid;
    }

    private static bool WoundLineageMatches(
        WoundRootLineageAuthorityRow? lineage,
        WoundRootEffectApplication root) =>
        lineage is not null &&
        string.Equals(
            lineage.ApplicationRef,
            root.ApplicationRef,
            StringComparison.Ordinal) &&
        lineage.EffectId is null &&
        string.Equals(
            lineage.DefinitionKey,
            root.DefinitionKey,
            StringComparison.Ordinal) &&
        lineage.OwnershipDomain == root.OwnershipDomain;

    private static bool ExistingWoundLineageMatches(
        WoundMaterializationEnvelope wound,
        IReadOnlyList<WoundRootLineageAuthorityRow> lineage,
        IReadOnlySet<string>? retainedDefinitionKeys = null)
    {
        var domains = wound.Consequences.OwnedEffectSources.RootBindings
            .ToDictionary(
                static binding => binding.EffectId,
                static _ => WoundRootOwnershipDomain.BaseWound,
                StringComparer.Ordinal);
        foreach (var complication in wound.Complications)
        {
            foreach (var effectId in complication.OwnedEffectIds)
            {
                if (!domains.ContainsKey(effectId) ||
                    domains[effectId] != WoundRootOwnershipDomain.BaseWound)
                {
                    return false;
                }
                domains[effectId] = WoundRootOwnershipDomain.ForComplication(
                    complication.ComplicationId);
            }
        }
        var bindings = wound.Consequences.OwnedEffectSources.RootBindings
            .Where(binding => retainedDefinitionKeys is null ||
                retainedDefinitionKeys.Contains(binding.DefinitionKey))
            .OrderBy(static binding => binding.EffectId, StringComparer.Ordinal)
            .ThenBy(static binding => binding.DefinitionKey, StringComparer.Ordinal)
            .ToArray();
        if (bindings.Length != lineage.Count)
            return false;
        for (var index = 0; index < bindings.Length; index++)
        {
            var expected = bindings[index];
            var actual = lineage[index];
            if (actual is null ||
                actual.ApplicationRef is not null ||
                !string.Equals(
                    actual.EffectId,
                    expected.EffectId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    actual.DefinitionKey,
                    expected.DefinitionKey,
                    StringComparison.Ordinal) ||
                !domains.TryGetValue(expected.EffectId, out var domain) ||
                actual.OwnershipDomain != domain)
            {
                return false;
            }
        }
        return true;
    }

    private static string SelectedDefinitionPath(MortalWoundTreatmentSelectedDefinitionOrigin origin) =>
        origin.ComponentsPath;

    private static bool ValidateSelectedDefinitionScopes(EffectAcceptedTurnInput input,
        MortalWoundTreatmentSelectedGraphCompilation selected,
        IReadOnlyDictionary<string, WoundEffectSourceDefinition> definitions, List<ValidationIssue> issues)
    {
        var count = issues.Count;
        if (!WoundEffectCarrierAdapter.TryCreateTargetKey(selected.Before.Owner, out var target)) return false;
        foreach (var origin in selected.NewDefinitionOrigins)
        {
            if (!definitions.TryGetValue(origin.DefinitionKey, out var definition) ||
                definition.Definition["components"] is not JsonArray components)
            {
                AddWoundBatchIssue(issues, SelectedDefinitionPath(origin), "wound_materialization_effect_binding_invalid",
                    "one exact sealed new typed definition", "missing");
                continue;
            }
            if (input.SkillScopeAuthority is { } authority)
                issues.AddRange(authority.ValidateNewComponents(target, components, SelectedDefinitionPath(origin), "wound_materialization")
                    .Select(issue => new ValidationIssue(issue.FilePath, issue.Severity, issue.Message,
                        code: "wound_materialization_effect_binding_invalid", section: "wound_materialization",
                        expected: issue.Expected, actual: issue.Actual, repairHint: issue.RepairHint)));
            else
                for (var i = 0; i < components.Count; i++)
                    if (components[i]?["profile"]?.GetValue<string>() == "roll_modifier" &&
                        components[i]?["payload"]?["scope"]?["kind"]?.GetValue<string>() == "skill")
                        AddWoundBatchIssue(issues, $"{SelectedDefinitionPath(origin)}[{i}].payload.scope.skillId",
                            "wound_materialization_effect_binding_invalid", "fresh Offered/Current exact-skill authority", "missing authority");
        }
        return issues.Count == count;
    }

    private static bool ValidateGenerationPredecessors(
        EffectAcceptedTurnInput input,
        string transitionKind,
        WoundMaterializationEnvelope beforeWound,
        IReadOnlyDictionary<string, WoundEffectSourceDefinition> definitions,
        IReadOnlyList<WoundRootEffectApplication> roots,
        string path,
        List<ValidationIssue> issues,
        WoundMaterializationEnvelope? retainedCoordinateWound,
        MortalWoundTreatmentSelectedGraphCompilation? selectedGraph)
    {
        var coordinateWound = retainedCoordinateWound ?? beforeWound;
        var beforeDomains = coordinateWound.Consequences.OwnedEffectSources
            .RootBindings.ToDictionary(
                static binding => binding.EffectId,
                static _ => WoundRootOwnershipDomain.BaseWound,
                StringComparer.Ordinal);
        foreach (var complication in coordinateWound.Complications)
        {
            foreach (var effectId in complication.OwnedEffectIds)
            {
                if (beforeDomains.ContainsKey(effectId))
                {
                    beforeDomains[effectId] =
                        WoundRootOwnershipDomain.ForComplication(
                            complication.ComplicationId);
                }
            }
        }
        var beforeByCoordinate = coordinateWound.Consequences.OwnedEffectSources
            .RootBindings.ToDictionary(
                binding => (
                    binding.DefinitionKey,
                    beforeDomains[binding.EffectId].Kind,
                    beforeDomains[binding.EffectId].ComplicationId),
                static binding => binding.EffectId);
        if (selectedGraph is not null)
        {
            var originalApplications = selectedGraph.FinalRoots.Where(row => selectedGraph.FinalSeverityChanged && row.OriginalEffectId is not null)
                .Select(row => (row.DefinitionKey, row.OwnershipDomain.Kind, row.OwnershipDomain.ComplicationId)).ToHashSet();
            beforeByCoordinate = beforeByCoordinate.Where(pair => originalApplications.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        }
        EffectCarrierCatalog? effectCatalog = null;
        EffectIdentityState? effectIdentities = null;
        if (transitionKind is "worsen" or "treat" &&
            input.PreTurnCarriers is not null &&
            input.PreTurnIdentityIndex is not null)
        {
            effectCatalog = EffectCarrierCatalog.Build(input.PreTurnCarriers);
            using var document = JsonDocument.Parse(
                input.PreTurnIdentityIndex.ToJsonString());
            var parsed = EffectIdentityState.Parse(
                document.RootElement,
                EffectIdentityState.StatePath);
            if (effectCatalog.Issues.Count != 0 ||
                parsed.Issues.Count != 0)
            {
                effectCatalog = null;
            }
            effectIdentities = parsed.State;
        }
        var suppliedPredecessors = new HashSet<string>(StringComparer.Ordinal);
        var generationAuthority = transitionKind is "worsen" or "treat" &&
            effectCatalog is not null && effectIdentities is not null
                ? new WoundRootGenerationAuthority(beforeWound, effectCatalog, effectIdentities)
                : null;
        if (generationAuthority is { Issues.Count: > 0 })
        {
            issues.AddRange(generationAuthority.Issues);
            return false;
        }
        var valid = true;
        foreach (var root in roots)
        {
            var coordinate = (
                root.DefinitionKey,
                root.OwnershipDomain.Kind,
                root.OwnershipDomain.ComplicationId);
            beforeByCoordinate.TryGetValue(coordinate, out var expectedPrior);
            if (expectedPrior is not null &&
                (!definitions.TryGetValue(root.DefinitionKey, out var definition) ||
                 !WoundEffectCarrierAdapter.TryCreateTargetKey(
                     beforeWound.Owner,
                     out var expectedTarget) ||
                 !WoundEffectCarrierAdapter.TryCreateCarrierCoordinate(
                     beforeWound.Owner,
                     expectedTarget,
                     definition.Definition,
                     out var expectedCarrier) ||
                 generationAuthority is null ||
                 !generationAuthority.Agrees(
                     expectedPrior,
                     new EffectSourceKey(
                         beforeWound.Owner.Realm,
                         "wound",
                         beforeWound.WoundId,
                         root.DefinitionKey),
                     expectedTarget,
                     expectedCarrier,
                     definition.Definition,
                     root.OwnershipDomain)))
            {
                if (generationAuthority?.IsTerminalRoot(expectedPrior) == true)
                    valid = false;
                expectedPrior = null;
            }
            var expected = transitionKind switch
            {
                "create" => null,
                "worsen" => expectedPrior,
                "treat" => expectedPrior,
                _ => null
            };
            if (!string.Equals(
                    root.PriorRootEffectId,
                    expected,
                    StringComparison.Ordinal) ||
                root.PriorRootEffectId is not null &&
                (!TryExact(root.PriorRootEffectId) ||
                 !suppliedPredecessors.Add(root.PriorRootEffectId)))
            {
                valid = false;
            }
        }
        if (string.Equals(transitionKind, "treat", StringComparison.Ordinal) &&
            (selectedGraph is null ? roots.Count != beforeByCoordinate.Count || suppliedPredecessors.Count != beforeByCoordinate.Count
                : !suppliedPredecessors.SetEquals(beforeByCoordinate.Values) ||
                  roots.Count != selectedGraph.FinalRoots.Count(row => selectedGraph.FinalSeverityChanged || row.AdditionBinding is not null)))
        {
            valid = false;
        }
        if (!valid)
        {
            AddWoundBatchIssue(
                issues,
                path + ".rootApplications",
                "wound_plan_effect_handoff_invalid",
                "exact canonical severity-generation predecessor matching for every retained wound root coordinate",
                transitionKind + "/changed predecessor bijection");
        }
        return valid;
    }

    private static WoundApplicationRequest? PrepareWoundApplication(
        EffectAcceptedTurnInput input,
        WoundEffectOperationBatch batch,
        WoundEffectSourceExport sourceExport,
        WoundEffectSourceDefinition sourceDefinition,
        WoundRootEffectApplication root,
        IReadOnlyDictionary<string, WoundAcceptedEventAuthority> acceptedEvents,
        string path,
        List<ValidationIssue> issues) => PrepareSelectedWoundApplication(input, batch, sourceExport,
            sourceDefinition, root, acceptedEvents, path, issues, null);

    private static WoundApplicationRequest? PrepareSelectedWoundApplication(
        EffectAcceptedTurnInput input,
        WoundEffectOperationBatch batch,
        WoundEffectSourceExport sourceExport,
        WoundEffectSourceDefinition sourceDefinition,
        WoundRootEffectApplication root,
        IReadOnlyDictionary<string, WoundAcceptedEventAuthority> acceptedEvents,
        string path,
        List<ValidationIssue> issues,
        MortalWoundTreatmentSelectedGraphCompilation? selectedGraph)
    {
        var issueCount = issues.Count;
        var sourceSelector = root.SourceSelector;
        var expectedSource = root.ExpectedSourceKey;
        var targetSelector = root.TargetSelector;
        var expectedTarget = root.ExpectedTargetKey;
        var parameters = root.Parameters;
        var expectedCoordinate = root.ExpectedCarrierCoordinate;
        var owner = sourceExport.Owner;
        if (sourceSelector is null ||
            expectedSource is null ||
            targetSelector is null ||
            expectedTarget is null ||
            parameters is null ||
            expectedCoordinate is null ||
            owner is null ||
            root.SlotBindings is null ||
            root.MechanicsOrdinal <= 0 ||
            root.OperationOrdinal <= 0 ||
            !TryExact(root.OperationKey) ||
            !TryExact(root.DefinitionKey) ||
            !string.Equals(root.OperationKind, "apply", StringComparison.Ordinal) ||
            !string.Equals(
                root.CausalEventRef,
                sourceExport.CausalEventRef,
                StringComparison.Ordinal) ||
            !acceptedEvents.ContainsKey(root.CausalEventRef))
        {
            AddWoundBatchIssue(
                issues,
                path,
                "wound_plan_effect_handoff_invalid",
                "complete direct apply root bound to its exact prepared operation and causal event",
                root.ApplicationRef);
            return null;
        }

        if (!TryMapWoundOwnerTarget(owner, out var targetKind) ||
            expectedTarget != new EffectTargetKey(
                owner.Realm,
                targetKind,
                owner.OwnerId) ||
            !string.Equals(
                targetSelector.Kind,
                targetKind,
                StringComparison.Ordinal))
        {
            AddWoundBatchIssue(
                issues,
                path + ".target",
                "wound_plan_effect_handoff_invalid",
                "exact narrowly mapped wound-owner target",
                expectedTarget.ToString());
        }
        ValidateWoundTargetSelector(
            input.TargetAuthority,
            targetSelector,
            expectedTarget,
            path + ".target",
            issues);

        if (!string.Equals(
                sourceSelector.Realm,
                sourceExport.Realm,
                StringComparison.Ordinal) ||
            !string.Equals(
                sourceSelector.Kind,
                "wound",
                StringComparison.Ordinal) ||
            sourceSelector.SourceId is not null ||
            !string.Equals(
                sourceSelector.SourceRef,
                batch.LocalWoundRef,
                StringComparison.Ordinal) ||
            !string.Equals(
                sourceSelector.DefinitionKey,
                root.DefinitionKey,
                StringComparison.Ordinal) ||
            expectedSource != new EffectSourceKey(
                sourceExport.Realm,
                "wound",
                batch.PreparedWoundId,
                root.DefinitionKey))
        {
            AddWoundBatchIssue(
                issues,
                path + ".source",
                "wound_plan_effect_handoff_invalid",
                "exact sourceRef-only wound selector and resolved permanent source key",
                expectedSource.ToString());
        }

        EffectSourceAuthorityEntry? resolvedSource = null;
        var sourceIdentityKind = sourceSelector.SourceRef is not null &&
                                 sourceSelector.SourceId is null
            ? WoundTypedRootSourceIdentityKind.NewSourceRef
            : WoundTypedRootSourceIdentityKind.ExistingSourceId;
        var sourceResolution = input.SourceAuthority.ResolveTypedWoundRootBinding(
            new WoundTypedRootBindingRequest(
                expectedSource,
                sourceSelector,
                sourceIdentityKind,
                expectedTarget,
                batch.SourceExportFingerprint,
                root.ApplicationRef,
                root.OwnershipDomain));
        issues.AddRange(sourceResolution.Issues.Select(issue =>
            Prefix(issue, path + ".source")));
        if (sourceResolution.Success)
        {
            resolvedSource = sourceResolution.Source;
            var detachedDefinition = sourceDefinition.Definition;
            if (resolvedSource is null ||
                resolvedSource.Key != expectedSource ||
                !resolvedSource.SameTurn ||
                resolvedSource.Materializable ||
                !resolvedSource.Active ||
                !string.Equals(
                    resolvedSource.SourceRef,
                    batch.LocalWoundRef,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    sourceDefinition.DefinitionKey,
                    root.DefinitionKey,
                    StringComparison.Ordinal) ||
                detachedDefinition is null ||
                !JsonNode.DeepEquals(
                    resolvedSource.Definition,
                    detachedDefinition))
            {
                AddWoundBatchIssue(
                    issues,
                    path + ".source",
                    "wound_plan_effect_handoff_invalid",
                    "exact active same-turn non-materializable canonical wound source and definition",
                    expectedSource.ToString());
            }
        }

        if (resolvedSource is not null)
        {
            issues.AddRange(input.SourceAuthority
                .ValidateCanonicalParameters(resolvedSource, parameters)
                .Select(issue => Prefix(issue, path + ".parameters")));
            ValidateWoundMaterializationExpectation(
                root,
                resolvedSource,
                parameters,
                path,
                issues);
            if (!TryCreateExpectedWoundCarrierCoordinate(
                    owner,
                    expectedTarget,
                    resolvedSource.Definition,
                    out var actualCoordinate) ||
                actualCoordinate != expectedCoordinate)
            {
                AddWoundBatchIssue(
                    issues,
                    path + ".expectedCarrierCoordinate",
                    "wound_plan_effect_handoff_invalid",
                    "exact carrier coordinate derived from the owner, target, and canonical definition",
                    expectedCoordinate.ToString());
            }
        }

        if (issues.Count != issueCount || resolvedSource is null)
            return null;
        var createdEventRef = WoundEffectOperationEventRef.Create(
            root.CausalEventRef,
            root.MechanicsOrdinal,
            root.OperationOrdinal,
            root.OperationKind);
        if (!TryExact(createdEventRef) ||
            string.Equals(
                createdEventRef,
                root.CausalEventRef,
                StringComparison.Ordinal))
        {
            AddWoundBatchIssue(
                issues,
                path + ".createdEventRef",
                "wound_plan_effect_handoff_invalid",
                "one distinct exact derived wound effect event",
                createdEventRef);
            return null;
        }

        // A typed, authenticated treatment batch re-materializes its sealed definition graph;
        // it does not offer a new selector choice. Its structural diagnostics remain internal.
        var origin = selectedGraph?.NewDefinitionOrigins.SingleOrDefault(row => row.DefinitionKey == root.DefinitionKey);
        var acceptedContinuation = batch.TransitionAuthority.TransitionKind == "treat" && origin is null;
        var diagnosticPath = path + ".components";
        if (origin is not null)
            diagnosticPath = SelectedDefinitionPath(origin);
        else if (!acceptedContinuation &&
            (input.SkillScopeAuthority is not null || input.WoundApplicationLocations is not null))
        {
            if (input.WoundApplicationLocations is null ||
                !input.WoundApplicationLocations.TryResolve(resolvedSource.Key, out var location))
            {
                AddWoundBatchIssue(issues, path, "wound_plan_effect_diagnostic_location_invalid",
                    "one exact immutable original wound proposal component coordinate", "missing or ambiguous mapping");
                return null;
            }
            diagnosticPath = location.Path;
        }

        return new WoundApplicationRequest(
            batch,
            root,
            sourceExport,
            sourceDefinition,
            new Application(
                resolvedSource,
                expectedTarget,
                parameters.DeepClone().AsObject(),
                createdEventRef,
                root.CausalEventRef,
                diagnosticPath,
                "wound_materialization",
                acceptedContinuation),
            createdEventRef);
    }

    private static void ValidateWoundTargetSelector(
        EffectTargetAuthority authority,
        WoundEffectTargetSelector selector,
        EffectTargetKey expected,
        string path,
        List<ValidationIssue> issues)
    {
        var hasTargetId = TryExact(selector.TargetId ?? string.Empty);
        var hasTargetRef = TryExact(selector.TargetRef ?? string.Empty);
        if (hasTargetId == hasTargetRef ||
            !authority.TryResolveAcceptedTarget(expected, out var accepted) ||
            accepted is null ||
            (hasTargetId &&
             (!string.Equals(
                  selector.TargetId,
                  expected.TargetId,
                  StringComparison.Ordinal) ||
              accepted.SameTurn)) ||
            (hasTargetRef &&
             (!accepted.SameTurn ||
              !string.Equals(
                  selector.TargetRef,
                  accepted.TargetRef,
                  StringComparison.Ordinal))))
        {
            AddWoundBatchIssue(
                issues,
                path,
                "wound_plan_effect_handoff_invalid",
                "exact accepted targetId for a pre-turn target or targetRef for a same-turn target",
                $"{selector.Kind}/{selector.TargetId}/{selector.TargetRef}");
        }
    }

    private static void ValidateWoundMaterializationExpectation(
        WoundRootEffectApplication root,
        EffectSourceAuthorityEntry source,
        JsonObject parameters,
        string path,
        List<ValidationIssue> issues)
    {
        if (source.Definition["components"] is not JsonArray components)
        {
            AddWoundBatchIssue(
                issues,
                path + ".definition.components",
                "wound_plan_effect_handoff_invalid",
                "canonical source-owned component array",
                Describe(source.Definition["components"]));
            return;
        }
        var materialized = components.DeepClone().AsArray();
        BindParameters(materialized, parameters);
        var fingerprint = WoundEffectMaterializationFingerprint.Compute(
            source.Key,
            EffectMaterializationContract.SchemaVersion,
            parameters,
            materialized);
        if (root.ExpectedComponentCount != materialized.Count ||
            !string.Equals(
                root.ExpectedMaterializationFingerprint,
                fingerprint,
                StringComparison.Ordinal))
        {
            AddWoundBatchIssue(
                issues,
                path + ".expectedMaterializationFingerprint",
                "wound_plan_effect_handoff_invalid",
                "exact component count and materialization fingerprint recomputed from canonical source and parameters",
                $"{root.ExpectedComponentCount}/{root.ExpectedMaterializationFingerprint}");
        }
    }

    private static bool TryMapWoundOwnerTarget(
        WoundOwnerCoordinate owner,
        out string targetKind)
    {
        if (WoundEffectCarrierAdapter.TryCreateTargetKey(owner, out var target))
        {
            targetKind = target.Kind;
            return true;
        }
        targetKind = string.Empty;
        return false;
    }

    private static bool TryCreateExpectedWoundCarrierCoordinate(
        WoundOwnerCoordinate owner,
        EffectTargetKey target,
        JsonObject definition,
        out EffectCarrierCoordinate coordinate) =>
        WoundEffectCarrierAdapter.TryCreateCarrierCoordinate(
            owner,
            target,
            definition,
            out coordinate);

    private static void ValidateWoundReplayAuthority(
        IReadOnlyList<WoundApplicationRequest> woundApplications,
        IReadOnlyList<WoundTerminalRequest> woundTerminations,
        IReadOnlyList<Application> rawApplications,
        IReadOnlyList<TerminalOperation> rawTerminalOperations,
        IReadOnlyList<EventAuthority> acceptedEvents,
        IReadOnlySet<string> processedEventRefs,
        List<ValidationIssue> issues)
    {
        var exact = processedEventRefs
            .Concat(acceptedEvents.Select(static value => value.EventRef))
            .Concat(rawApplications.Select(static value => value.EventRef))
            .Concat(rawTerminalOperations.Select(static value => value.EventRef))
            .ToHashSet(StringComparer.Ordinal);
        var aliases = exact
            .Select(MortalLocationIdentityState.BuildConfusableKey)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var request in woundApplications)
        {
            if (!exact.Add(request.CreatedEventRef) ||
                !aliases.Add(MortalLocationIdentityState.BuildConfusableKey(
                    request.CreatedEventRef)))
            {
                AddWoundBatchIssue(
                    issues,
                    "prepared.rootApplications.createdEventRef",
                    "wound_plan_effect_handoff_invalid",
                    "derived wound create event absent from immutable effect history and current raw operations",
                    request.CreatedEventRef);
            }
        }

        var rawTerminalEffectIds = rawTerminalOperations
            .Select(static value => value.EffectId)
            .ToHashSet(StringComparer.Ordinal);
        var typedEffectIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var request in woundTerminations)
        {
            var operation = request.Operation;
            if (!exact.Add(operation.OperationRef) ||
                !aliases.Add(MortalLocationIdentityState.BuildConfusableKey(
                    operation.OperationRef)) ||
                !typedEffectIds.Add(operation.EffectId) ||
                rawTerminalEffectIds.Contains(operation.EffectId))
            {
                AddWoundBatchIssue(
                    issues,
                    "prepared.terminalOperations",
                    "wound_plan_effect_handoff_invalid",
                    "derived wound terminal events must be exact/confusable unique and each effect may be terminalized by only one typed or raw operation",
                    operation.OperationRef + "/" + operation.EffectId);
            }
        }
    }

    private static void ValidateWoundTerminalRequests(
        IReadOnlyList<WoundTerminalRequest> requests,
        EffectCarrierCatalog catalog,
        EffectIdentityState identities,
        List<ValidationIssue> issues)
    {
        foreach (var group in requests.GroupBy(static request =>
                     (request.Batch.LocalWoundRef,
                      request.Batch.PreparedWoundId)))
        {
            var first = group.First();
            var selectedRoots = first.SelectedRootEffectIds;
            var lineage = WoundEffectLineagePlanner.Plan(
                first.Wound,
                identities,
                selectedRoots);
            issues.AddRange(lineage.Issues);
            if (lineage.Issues.Count != 0)
                continue;

            var operations = group
                .Select(static request => request.Operation)
                .OrderBy(static operation => operation.OperationOrdinal)
                .ToArray();
            if (!lineage.ActiveOrSuspendedEffectIds.SequenceEqual(
                    operations.Select(static operation => operation.EffectId),
                    StringComparer.Ordinal))
            {
                AddWoundBatchIssue(
                    issues,
                    "prepared.terminalOperations",
                    "wound_plan_effect_handoff_invalid",
                    "the exact ordered active/suspended first-create lineage closure",
                    string.Join(",", operations.Select(static operation =>
                        operation.EffectId)));
                continue;
            }

            foreach (var operation in operations)
            {
                if (!catalog.TryResolveOne(
                        operation.EffectId,
                        out var occurrence) ||
                    !identities.TryGetEntry(
                        operation.EffectId,
                        out var identity) ||
                    !WoundEffectTerminalOperationPlanner.OccurrenceAndIdentityAgree(
                        occurrence,
                        identity,
                        operation.ExpectedSourceKey,
                        operation.ExpectedTargetKey,
                        operation.ExpectedCarrierCoordinate,
                        operation.ExpectedIdentityOwner,
                        operation.ExpectedStackCoordinate) ||
                    !string.Equals(
                        occurrence.FilePath,
                        operation.ExpectedCarrierFilePath,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        occurrence.JsonPath,
                        operation.ExpectedCarrierJsonPath,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        WoundEffectTerminalOperationPlanner.ComputeEffectFingerprint(
                            occurrence),
                        operation.ExpectedEffectFingerprint,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        WoundEffectTerminalOperationPlanner.ComputeIdentityFingerprint(
                            identity),
                        operation.ExpectedIdentityFingerprint,
                        StringComparison.Ordinal) ||
                    !lineage.OwnershipByEffectId.TryGetValue(
                        operation.EffectId,
                        out var ownership) ||
                    ownership != operation.OwnershipDomain)
                {
                    AddWoundBatchIssue(
                        issues,
                        "prepared.terminalOperations",
                        "wound_plan_effect_handoff_invalid",
                        "one exact frozen carrier/identity occurrence and ownership domain for every terminal operation",
                        operation.OperationRef + "/" + operation.EffectId);
                }
            }
        }
    }

    private static void ValidateWoundApplicationExecution(
        WoundApplicationRequest request,
        ApplicationExecutionFacts execution,
        List<ValidationIssue> issues)
    {
        if (!string.Equals(
                execution.Disposition,
                "created_new_identity",
                StringComparison.Ordinal))
        {
            AddWoundBatchIssue(
                issues,
                "woundApplication.disposition",
                "wound_plan_effect_result_disposition_mismatch",
                "created_new_identity",
                execution.Disposition);
            return;
        }

        var root = request.Root;
        var application = request.Application;
        var effect = execution.CreatedEffect;
        var identity = execution.CreatedIdentityEntry;
        var parameters = application.Parameters;
        var valid =
            TryExact(execution.EffectId) &&
            TryExact(execution.TransitionId) &&
            string.Equals(
                execution.CreatedEventRef,
                request.CreatedEventRef,
                StringComparison.Ordinal) &&
            string.Equals(
                execution.CausalEventRef,
                root.CausalEventRef,
                StringComparison.Ordinal) &&
            execution.Source == root.ExpectedSourceKey &&
            execution.Target == root.ExpectedTargetKey &&
            execution.CarrierCoordinate == root.ExpectedCarrierCoordinate &&
            effect is not null &&
            identity is not null &&
            parameters is not null &&
            EffectEvidenceMatches(
                effect,
                execution,
                root,
                parameters) &&
            IdentityEvidenceMatches(identity, execution);
        if (!valid)
        {
            AddWoundBatchIssue(
                issues,
                "woundApplication",
                "wound_plan_effect_result_agreement_mismatch",
                "exact created effect, create transition, chronology, source, target, carrier, and materialization evidence",
                root.ApplicationRef);
        }
    }

    private static bool EffectEvidenceMatches(
        JsonObject effect,
        ApplicationExecutionFacts execution,
        WoundRootEffectApplication root,
        JsonObject parameters)
    {
        if (!TryReadExact(effect["effectId"], out var effectId) ||
            !string.Equals(effectId, execution.EffectId, StringComparison.Ordinal) ||
            effect["schemaVersion"] is not JsonValue schemaNode ||
            !schemaNode.TryGetValue<int>(out var schemaVersion) ||
            schemaVersion != EffectMaterializationContract.SchemaVersion ||
            !TryReadExact(effect["state"], out var state) ||
            !string.Equals(state, "active", StringComparison.Ordinal) ||
            !TryReadExact(effect["realm"], out var realm) ||
            !string.Equals(realm, execution.Target.Realm, StringComparison.Ordinal) ||
            effect["source"] is not JsonObject source ||
            !TryReadExact(source["kind"], out var sourceKind) ||
            !TryReadExact(source["sourceId"], out var sourceId) ||
            !TryReadExact(source["definitionKey"], out var definitionKey) ||
            !string.Equals(
                sourceKind,
                execution.Source.Kind,
                StringComparison.Ordinal) ||
            !string.Equals(
                sourceId,
                execution.Source.SourceId,
                StringComparison.Ordinal) ||
            !string.Equals(
                definitionKey,
                execution.Source.DefinitionKey,
                StringComparison.Ordinal) ||
            effect["target"] is not JsonObject target ||
            !TryReadExact(target["kind"], out var targetKind) ||
            !TryReadExact(target["targetId"], out var targetId) ||
            !string.Equals(
                targetKind,
                execution.Target.Kind,
                StringComparison.Ordinal) ||
            !string.Equals(
                targetId,
                execution.Target.TargetId,
                StringComparison.Ordinal) ||
            effect["chronology"] is not JsonObject chronology ||
            !TryReadExact(
                chronology["createdEventRef"],
                out var createdEventRef) ||
            !TryReadExact(
                chronology["causalEventRef"],
                out var causalEventRef) ||
            !TryReadExact(
                chronology["lastTransitionId"],
                out var lastTransitionId) ||
            !string.Equals(
                createdEventRef,
                execution.CreatedEventRef,
                StringComparison.Ordinal) ||
            !string.Equals(
                causalEventRef,
                execution.CausalEventRef,
                StringComparison.Ordinal) ||
            !string.Equals(
                lastTransitionId,
                execution.TransitionId,
                StringComparison.Ordinal) ||
            effect["components"] is not JsonArray components ||
            components.Count != root.ExpectedComponentCount)
        {
            return false;
        }

        var materializationFingerprint =
            WoundEffectMaterializationFingerprint.Compute(
                execution.Source,
                schemaVersion,
                parameters,
                components);
        return string.Equals(
            materializationFingerprint,
            root.ExpectedMaterializationFingerprint,
            StringComparison.Ordinal);
    }

    private static bool IdentityEvidenceMatches(
        JsonObject identity,
        ApplicationExecutionFacts execution)
    {
        if (!TryReadExact(identity["effectId"], out var effectId) ||
            !string.Equals(effectId, execution.EffectId, StringComparison.Ordinal) ||
            !TryReadExact(identity["state"], out var state) ||
            !string.Equals(state, "active", StringComparison.Ordinal) ||
            !TryReadExact(identity["realm"], out var realm) ||
            !string.Equals(realm, execution.Target.Realm, StringComparison.Ordinal) ||
            identity["owner"] is not JsonObject owner ||
            !TryReadExact(owner["ownerId"], out var ownerId) ||
            !TryReadExact(owner["carrierPath"], out var carrierPath) ||
            !string.Equals(
                ownerId,
                execution.Target.TargetId,
                StringComparison.Ordinal) ||
            !string.Equals(
                carrierPath,
                execution.CarrierCoordinate.Path,
                StringComparison.Ordinal) ||
            identity["transitions"] is not JsonArray { Count: 1 } transitions ||
            transitions[0] is not JsonObject create ||
            !TryReadExact(create["transitionId"], out var transitionId) ||
            !TryReadExact(create["kind"], out var transitionKind) ||
            !TryReadExact(create["eventRef"], out var eventRef) ||
            !string.Equals(
                transitionId,
                execution.TransitionId,
                StringComparison.Ordinal) ||
            !string.Equals(transitionKind, "create", StringComparison.Ordinal) ||
            !string.Equals(
                eventRef,
                execution.CreatedEventRef,
                StringComparison.Ordinal) ||
            create["sourceEffectIds"] is not JsonArray sourceEffectIds)
        {
            return false;
        }
        var expectedParent = execution.Provenance switch
        {
            ApplicationProvenance.DirectProvenance => null,
            ApplicationProvenance.Reaction reaction => reaction.ParentEffectId,
            ApplicationProvenance.SeverityGeneration generation =>
                generation.ParentEffectId,
            _ => null
        };
        return expectedParent is null
            ? sourceEffectIds.Count == 0
            : sourceEffectIds.Count == 1 &&
              TryReadExact(sourceEffectIds[0], out var actualParent) &&
              string.Equals(
                  actualParent,
                  expectedParent,
                  StringComparison.Ordinal);
    }

    internal static EffectAcceptedTurnPlanningResult Build(
        EffectAcceptedTurnInput input,
        string fingerprint,
        EffectIdentityFactory identityFactory) =>
        BuildCore(
            input,
            fingerprint,
            identityFactory,
            Array.Empty<WoundApplicationRequest>(),
            Array.Empty<WoundTerminalRequest>());

    internal static EffectAcceptedTurnPlanningResult BuildWoundBatch(
        EffectAcceptedTurnInput input,
        WoundPreparedAcceptedTurnPlan prepared,
        EffectIdentityFactory identityFactory)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(identityFactory);

        var issues = new List<ValidationIssue>();
        PreparedWoundOperations operations;
        string effectInputFingerprint;
        try
        {
            var actualPreparationFingerprint =
                WoundAcceptedTurnFingerprints.ComputePreparation(prepared);
            if (!string.Equals(
                    actualPreparationFingerprint,
                    prepared.WoundPreparationFingerprint,
                    StringComparison.Ordinal))
            {
                AddWoundBatchIssue(
                    issues,
                    "prepared.woundPreparationFingerprint",
                    "wound_plan_prepared_seal_mismatch",
                    "the exact recomputed detached wound preparation fingerprint",
                    prepared.WoundPreparationFingerprint);
                return Failed(issues);
            }

            operations = PrepareWoundOperations(input, prepared, issues);
            if (issues.Count > 0)
                return Failed(issues);

            effectInputFingerprint =
                WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, input);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or
                JsonException or NullReferenceException)
        {
            AddWoundBatchIssue(
                issues,
                "prepared",
                "wound_plan_effect_handoff_invalid",
                "well-formed detached wound preparation and effect input",
                exception.GetType().Name);
            return Failed(issues);
        }
        return BuildCore(
            input,
            effectInputFingerprint,
            identityFactory,
            operations.Applications,
            operations.Terminations);
    }

    private static EffectAcceptedTurnPlanningResult BuildCore(
        EffectAcceptedTurnInput input,
        string fingerprint,
        EffectIdentityFactory identityFactory,
        IReadOnlyList<WoundApplicationRequest> woundApplications,
        IReadOnlyList<WoundTerminalRequest> woundTerminations)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(identityFactory);
        ArgumentNullException.ThrowIfNull(woundApplications);
        ArgumentNullException.ThrowIfNull(woundTerminations);
        var issues = new List<ValidationIssue>();
        issues.AddRange(input.SourceAuthority.Issues);
        if (!TryExact(input.SessionId) || !TryExact(input.SnapshotToken) || !TryExact(input.Realm))
            Add(issues, "effectAcceptedTurn", "effect_plan_input_invalid", "exact session, snapshot, and realm authority", input.SessionId + "/" + input.SnapshotToken + "/" + input.Realm);

        ValidateRoot(input.RawCommands, issues);
        var turn = 0;
        if (!TryReadPositiveInt(input.EventInput["turn"], out turn))
            Add(issues, "eventInput.turn", "effect_plan_event_authority_invalid", "positive accepted turn", Describe(input.EventInput["turn"]));
        var acceptedEvents = ParseAcceptedEvents(input.EventInput["events"], issues);
        if (issues.Count > 0)
            return Failed(issues);

        var carriers = input.PreTurnCarriers ??
            new EffectCarrierCatalogInput(null, null, null, null, null, null);
        var publicationCarrierBaselines = input.PublicationCarrierBaselines ?? carriers;
        var carrierAuthorityFingerprint =
            EffectCarrierCatalog.CreateAuthorityFingerprint(publicationCarrierBaselines);
        PrepareCombatantTargets(
            input,
            carriers,
            identityFactory,
            issues,
            out var preparedCarriers,
            out var targetAuthority,
            out var combatantIds);
        issues.AddRange(targetAuthority.Issues);
        issues.AddRange(targetAuthority.ValidateNamedCombatantBindings(
            preparedCarriers));
        if (issues.Count > 0)
            return Failed(issues);

        var effectiveInput = input with
        {
            TargetAuthority = targetAuthority,
            PreTurnCarriers = preparedCarriers
        };

        carriers = effectiveInput.PreTurnCarriers!;
        var carrierCatalog = EffectCarrierCatalog.Build(carriers);
        issues.AddRange(carrierCatalog.Issues);
        var identityBeforeImage = input.PreTurnIdentityIndex?.DeepClone().AsObject();
        using var identityRoot = new EffectIdentityHistoryOwner(identityBeforeImage ?? EmptyIdentityIndex(), identityFactory);
        identityFactory = identityRoot.Factory;
        var identityState = ParseIdentity(identityRoot.ReadSnapshot());
        issues.AddRange(identityState.Issues);
        if (issues.Count > 0)
            return Failed(issues);

        var processedEventRefs = identityState.State!.Entries
            .SelectMany(static entry => entry.Transitions)
            .Select(static transition => transition.EventRef)
            .ToHashSet(StringComparer.Ordinal);
        ParseOperations(
            effectiveInput,
            acceptedEvents,
            processedEventRefs,
            issues,
            out var applications,
            out var terminalOperations);
        ValidateWoundReplayAuthority(
            woundApplications,
            woundTerminations,
            applications,
            terminalOperations,
            acceptedEvents,
            processedEventRefs,
            issues);
        ValidateWoundTerminalRequests(
            woundTerminations,
            carrierCatalog,
            identityState.State,
            issues);
        if (issues.Count > 0)
            return Failed(issues);

        var workspace = new CarrierWorkspace(carriers);
        // Reject the entire new-application batch before any lifecycle or application allocation.
        foreach (var application in applications.Concat(woundApplications.Select(request => request.Application)))
        {
            var components = application.Source.Definition["components"]!.DeepClone().AsArray();
            BindParameters(components, application.Parameters);
            issues.AddRange(ValidateBoundApplicationComponents(application, components, input.SkillScopeAuthority));
        }
        if (issues.Count != 0)
            return Failed(issues);
        workspace.IncludeRewrittenCombatantRoots(publicationCarrierBaselines);
        var effectIds = new List<string>();
        var transitionIds = new List<string>();
        var activeEffects = new List<JsonObject>();
        var usedSources = new List<EffectSourceAuthorityEntry>();
        var usedTargets = new List<EffectTargetKey>();
        var woundApplicationRootEffectBindings =
            new List<WoundApplicationRootEffectBinding>();

        foreach (var request in woundTerminations)
        {
            ApplyWoundTerminalOperation(
                request,
                workspace,
                identityState.State,
                identityRoot,
                identityFactory,
                turn,
                transitionIds,
                activeEffects,
                processedEventRefs,
                issues);
        }
        if (issues.Count > 0)
            return Failed(issues);

        ApplyDueLifecycleEvents(
            input.EventInput,
            input.SourceAuthority,
            workspace,
            identityRoot,
            identityFactory,
            transitionIds,
            activeEffects,
            processedEventRefs,
            issues,
            boundContinuationsOnly: true);
        if (issues.Count > 0)
            return Failed(issues);

        foreach (var operation in terminalOperations)
        {
            ApplyTerminalOperation(
                operation,
                workspace,
                identityState.State,
                identityRoot,
                identityFactory,
                turn,
                transitionIds,
                processedEventRefs,
                issues);
        }
        if (issues.Count > 0)
            return Failed(issues);

        foreach (var application in applications)
        {
            ApplyApplication(
                application,
                input.Realm,
                input.EventInput,
                workspace,
                identityRoot,
                identityFactory,
                turn,
                effectIds,
                transitionIds,
                activeEffects,
                processedEventRefs,
                issues,
                ApplicationProvenance.Direct,
                skillScopeAuthority: input.SkillScopeAuthority);
            usedSources.Add(application.Source);
            usedTargets.Add(application.Target);
        }
        if (issues.Count > 0)
            return Failed(issues);

        foreach (var request in woundApplications)
        {
            var issueCount = issues.Count;
            var execution = ApplyApplication(
                request.Application,
                input.Realm,
                input.EventInput,
                workspace,
                identityRoot,
                identityFactory,
                turn,
                effectIds,
                transitionIds,
                activeEffects,
                processedEventRefs,
                issues,
                request.Root.PriorRootEffectId is null
                    ? ApplicationProvenance.Direct
                    : new ApplicationProvenance.SeverityGeneration(
                        request.Root.PriorRootEffectId),
                skillScopeAuthority: input.SkillScopeAuthority);
            if (execution is null && issues.Count == issueCount)
            {
                AddWoundBatchIssue(
                    issues,
                    "woundApplication",
                    "wound_plan_effect_result_agreement_mismatch",
                    "one exact typed wound application execution",
                    request.Root.ApplicationRef);
            }
            else if (execution != null)
            {
                ValidateWoundApplicationExecution(request, execution, issues);
                woundApplicationRootEffectBindings.Add(
                    new WoundApplicationRootEffectBinding(
                        request.Root.ApplicationRef,
                        execution.EffectId));
            }
            usedSources.Add(request.Application.Source);
            usedTargets.Add(request.Application.Target);
        }
        if (issues.Count > 0)
            return Failed(issues);

        var reactionIdentityState = ParseIdentity(identityRoot.ReadSnapshot());
        issues.AddRange(reactionIdentityState.Issues);
        var reactionCarrierCatalog = EffectCarrierCatalog.Build(workspace.ToInput());
        issues.AddRange(reactionCarrierCatalog.Issues);
        if (reactionIdentityState.State == null || issues.Count > 0)
            return Failed(issues);
        var woundLineageAuthority = WoundReactionLineageAuthority.Build(
            input.SourceAuthority,
            reactionIdentityState.State,
            reactionCarrierCatalog,
            woundApplicationRootEffectBindings);
        issues.AddRange(woundLineageAuthority.Issues);
        if (!woundLineageAuthority.Success)
            return Failed(issues);

        var reactionPlan = EffectReactionExecutor.Plan(
            input.EventInput,
            input.SourceAuthority,
            workspace.ToInput(),
            woundLineageAuthority);
        issues.AddRange(reactionPlan.Issues);
        if (!reactionPlan.Success)
            return Failed(issues);

        var resourceTriggerCarriers = workspace.ToInput();

        ValidateAfterImages(workspace, identityRoot, activeEffects, issues);
        if (issues.Count > 0)
            return Failed(issues);

        var afterImages = workspace.AfterImages;
        var carrierBeforeImages = new[]
        {
            EffectCarrierCatalog.PlayerPath,
            EffectCarrierCatalog.NpcPath,
            EffectCarrierCatalog.EnemiesPath,
            EffectCarrierCatalog.AlliesPath,
            EffectCarrierCatalog.AfterlifeProfilesPath,
            EffectCarrierCatalog.SpiritualConflictPath
        }.ToDictionary(
            static path => path,
            path => GetCarrierRoot(publicationCarrierBaselines, path)?.DeepClone().AsObject(),
            StringComparer.Ordinal);
        var touchedPaths = afterImages.Keys
            .Append(EffectAcceptedTurnPlan.IdentityIndexPath)
            .Append(EffectAcceptedTurnPlan.CommandPath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        return new EffectAcceptedTurnPlanningResult(
            new EffectAcceptedTurnPlan(
                fingerprint,
                carrierAuthorityFingerprint,
                input.SourceAuthority.CanonicalFingerprint,
                targetAuthority.CanonicalFingerprint,
                combatantIds,
                identityRoot.AllocatedEffectIds,
                identityRoot.AllocatedTransitionIds,
                usedSources.Select(static item => item.Key).ToArray(),
                usedTargets,
                usedSources
                    .DistinctBy(static entry => entry.Key)
                    .ToArray(),
                reactionPlan.Executions,
                reactionExpansionCount: 0,
                new Dictionary<
                    EffectReactionExpansionKey,
                    EffectReactionExpansionUsage>(),
                activeEffects,
                resourceTriggerCarriers,
                input.SourceAuthority,
                targetAuthority,
                input.EventInput,
                carrierBeforeImages,
                afterImages,
                identityBeforeImage,
                identityRoot.Publish(),
                touchedPaths,
                new[] { EffectAcceptedTurnPlan.CommandPath },
                acceptedCarrierBaselines:
                    input.AcceptedCarrierBaselines ?? carriers,
                woundApplicationRootEffectBindings:
                    woundApplicationRootEffectBindings,
                skillScopeAuthority: input.SkillScopeAuthority),
            Array.Empty<ValidationIssue>());
    }

    private static bool ReactionStageMatchesDependency(
        EffectReactionReleaseStage stage,
        string dependency) => stage switch
        {
            EffectReactionReleaseStage.BeforeCurrentEvent => string.Equals(
                dependency,
                "before_current_event",
                StringComparison.Ordinal),
            EffectReactionReleaseStage.AfterComponent => string.Equals(
                dependency,
                "after_component",
                StringComparison.Ordinal),
            EffectReactionReleaseStage.AfterCurrentEvent => string.Equals(
                dependency,
                "after_current_event",
                StringComparison.Ordinal),
            _ => false
        };

    private static bool IsReactionBehavior(
        EffectReactionExecution reaction,
        EffectReactionResultBehavior behavior) =>
        EffectReactionResultCatalog.TryResolve(reaction.ResultKind, out var descriptor) &&
        descriptor.Behavior == behavior;

    private static bool IsTerminalAvailabilityReaction(
        EffectReactionExecution reaction) =>
        IsReactionBehavior(reaction, EffectReactionResultBehavior.Suspend) ||
        IsReactionBehavior(reaction, EffectReactionResultBehavior.Remove);

    private static Dictionary<string, ReactionApplicationPlan>
        PrepareReleasedReactionApplicationPlans(
            IReadOnlyList<ReleasedEffectReaction> releasedReactions,
            EffectCarrierCatalog preMutationCatalog,
            EffectSourceAuthority sourceAuthority,
            WoundReactionLineageAuthority woundLineageAuthority,
            EffectRollSkillScopeAuthority? skillScopeAuthority,
            List<ValidationIssue> issues)
    {
        var plans = new Dictionary<string, ReactionApplicationPlan>(
            StringComparer.Ordinal);
        var applicationsByCoordinate = new Dictionary<
            EffectStackCoordinate,
            List<(
                ReleasedEffectReaction Release,
                ReactionApplicationPlan Plan)>>();
        var plannedReplacements = new List<(
            ReleasedEffectReaction Release,
            ReactionApplicationPlan Plan)>();
        foreach (var released in releasedReactions)
        {
            var reaction = released.Reaction;
            if (!IsReactionBehavior(
                    reaction,
                    EffectReactionResultBehavior.ApplyDefinition))
            {
                continue;
            }

            if (!TryResolveReactionApplicationSource(
                    reaction,
                    preMutationCatalog,
                    sourceAuthority,
                    woundLineageAuthority,
                    issues,
                    out var source,
                    out var woundOwnershipDomain))
            {
                continue;
            }
            if (!string.Equals(
                    source.Key.Realm,
                    reaction.Target.Realm,
                    StringComparison.Ordinal) ||
                source.Definition["stacking"] is not JsonObject stacking ||
                !TryReadExact(stacking["stackKey"], out var stackKey) ||
                !TryReadExact(stacking["policy"], out var stackPolicy))
            {
                Add(
                    issues,
                    "effect.reactions",
                    "effect_reaction_downstream_source_invalid",
                    "one same-realm downstream definition with exact stacking authority",
                    reaction.EventRef);
                continue;
            }
            var components = source.Definition["components"]!.DeepClone().AsArray();
            BindParameters(components, reaction.Parameters);
            var bindingIssues = ValidateBoundApplicationComponents(
                new Application(source, reaction.Target, reaction.Parameters,
                    reaction.EventRef, reaction.CausalEventRef,
                    $"effect.reactions[{reaction.EventRef}].components", "effect_materialization"),
                components, skillScopeAuthority);
            issues.AddRange(bindingIssues);
            if (bindingIssues.Count != 0)
                continue;

            var coordinate = new EffectStackCoordinate(
                reaction.Target.Realm,
                reaction.Target.Kind,
                reaction.Target.TargetId,
                source.Key.Kind,
                source.Key.SourceId,
                stackKey);
            var isReplacement = string.Equals(
                stackPolicy,
                "replace",
                StringComparison.Ordinal);
            if (!isReplacement && reaction.ReplacementTarget != null)
            {
                Add(
                    issues,
                    "effect.reactions",
                    "effect_reaction_replacement_authority_invalid",
                    "no frozen replacement target for a non-replace definition",
                    reaction.EventRef);
                continue;
            }
            var plan = new ReactionApplicationPlan(
                source,
                coordinate,
                stackPolicy,
                ReactionReplacementExpectationKind.None,
                WoundOwnershipDomain: woundOwnershipDomain);
            if (!plans.TryAdd(reaction.EventRef, plan))
            {
                Add(
                    issues,
                    "effect.reactions",
                    "effect_reaction_event_duplicate",
                    "one exact reaction application plan per event identity",
                    reaction.EventRef);
                continue;
            }
            if (!applicationsByCoordinate.TryGetValue(
                    plan.Coordinate,
                    out var coordinateApplications))
            {
                coordinateApplications = new List<(
                    ReleasedEffectReaction Release,
                    ReactionApplicationPlan Plan)>();
                applicationsByCoordinate.Add(
                    plan.Coordinate,
                    coordinateApplications);
            }
            coordinateApplications.Add((released, plan));
            if (isReplacement)
                plannedReplacements.Add((released, plan));
        }
        if (issues.Count != 0)
            return plans;

        var effectsByCoordinate = new Dictionary<
            EffectStackCoordinate,
            List<JsonObject>>();
        foreach (var occurrence in preMutationCatalog.Occurrences)
        {
            if (!TryResolveEffectStackCoordinate(
                    occurrence.Effect,
                    out var coordinate))
            {
                Add(
                    issues,
                    occurrence.JsonPath,
                    "effect_reaction_replacement_target_drift",
                    "one exact canonical pre-reaction stack coordinate",
                    occurrence.EffectId);
                continue;
            }
            if (!effectsByCoordinate.TryGetValue(coordinate, out var effects))
            {
                effects = new List<JsonObject>();
                effectsByCoordinate.Add(coordinate, effects);
            }
            effects.Add(occurrence.Effect);
        }
        if (issues.Count != 0)
            return plans;

        foreach (var coordinateApplications in applicationsByCoordinate)
        {
            var woundDomains = coordinateApplications.Value
                .Select(static value => value.Plan.WoundOwnershipDomain)
                .Where(static value => value is not null)
                .Distinct()
                .ToArray();
            if (woundDomains.Length > 1)
            {
                Add(
                    issues,
                    "effect.reactions",
                    "effect_reaction_wound_lineage_cross_domain",
                    "Every simultaneous wound application at one stack coordinate must inherit one exact ownership domain.",
                    coordinateApplications.Key.ToString());
                continue;
            }
            if (woundDomains.Length != 1 ||
                !effectsByCoordinate.TryGetValue(
                    coordinateApplications.Key,
                    out var existingEffects))
            {
                continue;
            }
            foreach (var existingEffect in existingEffects)
            {
                if (!TryReadExact(existingEffect["effectId"], out var effectId))
                {
                    Add(
                        issues,
                        "effect.reactions",
                        "effect_reaction_wound_lineage_cross_domain",
                        "Every existing wound stack occupant must expose one exact effect identity before mutation.",
                        coordinateApplications.Key.ToString());
                    continue;
                }
                issues.AddRange(woundLineageAuthority.ValidateExistingOccupantDomain(
                    effectId,
                    woundDomains[0]!));
            }
        }
        if (issues.Count != 0)
            return plans;

        foreach (var group in plannedReplacements
                     .GroupBy(static value => value.Plan.Coordinate)
                     .OrderBy(static group => group.Key.Realm, StringComparer.Ordinal)
                     .ThenBy(static group => group.Key.TargetKind, StringComparer.Ordinal)
                     .ThenBy(static group => group.Key.TargetId, StringComparer.Ordinal)
                     .ThenBy(static group => group.Key.SourceKind, StringComparer.Ordinal)
                     .ThenBy(static group => group.Key.SourceId, StringComparer.Ordinal)
                     .ThenBy(static group => group.Key.StackKey, StringComparer.Ordinal))
        {
            var ordered = group
                .OrderBy(static value => value.Release.MechanicsOrdinal)
                .ThenBy(
                    static value => value.Release.Reaction.EventRef,
                    StringComparer.Ordinal)
                .ToArray();
            var frozenTargets = ordered
                .Select(static value => value.Release.Reaction.ReplacementTarget)
                .Distinct()
                .ToArray();
            if (frozenTargets.Length != 1)
            {
                Add(
                    issues,
                    "effect.reactions",
                    "effect_reaction_replacement_batch_conflict",
                    "one common frozen typed target or common frozen absence per replacement coordinate",
                    DescribeReplacementBatch(group.Key, ordered));
                continue;
            }
            var frozenTarget = frozenTargets[0];
            if (ordered.Length > 1)
            {
                var typedOwners = ordered
                    .Select(static value => new EffectReplayIdentity(
                        value.Release.Reaction.EffectId,
                        value.Release.Activation.EffectAuthority))
                    .Distinct()
                    .ToArray();
                var isOneBoundary = ordered
                    .Select(static value =>
                        value.Release.Boundary.BoundaryOrdinal)
                    .Distinct()
                    .Count() == 1;
                var isAuthorizedSelfCascade = frozenTarget != null &&
                    isOneBoundary &&
                    typedOwners.Length == 1 &&
                    typedOwners[0] == frozenTarget &&
                    ordered.All(static value =>
                        value.Release.Activation.ConsumesUse);
                if (!isAuthorizedSelfCascade)
                {
                    Add(
                        issues,
                        "effect.reactions",
                        "effect_reaction_replacement_batch_conflict",
                        "one replacement release, or one same-boundary consuming self-replacement cascade rooted in the frozen typed target",
                        DescribeReplacementBatch(group.Key, ordered));
                    continue;
                }
            }

            effectsByCoordinate.TryGetValue(group.Key, out var existing);
            existing ??= new List<JsonObject>();
            if (frozenTarget == null)
            {
                if (existing.Count != 0)
                {
                    Add(
                        issues,
                        "effect.reactions",
                        "effect_reaction_replacement_target_drift",
                        "the replacement coordinate frozen as empty remains empty before reaction execution",
                        DescribeReplacementBatch(group.Key, ordered));
                    continue;
                }
            }
            else
            {
                EffectReplayIdentity? exact = null;
                if (existing.Count == 1 &&
                    TryReadExact(
                        existing[0]["effectId"],
                        out var existingEffectId))
                {
                    exact = new EffectReplayIdentity(
                        existingEffectId,
                        ResolvePendingEffectAuthority(
                            existing[0],
                            ordered[0].Release.Reaction.Turn));
                }
                if (exact != frozenTarget)
                {
                    Add(
                        issues,
                        "effect.reactions",
                        "effect_reaction_replacement_target_drift",
                        "one runtime occupant matching the frozen typed replacement target",
                        DescribeReplacementBatch(group.Key, ordered));
                    continue;
                }
            }

            var orderedApplications = applicationsByCoordinate[group.Key]
                .OrderBy(static value => value.Release.MechanicsOrdinal)
                .ThenBy(
                    static value => value.Release.Reaction.EventRef,
                    StringComparer.Ordinal)
                .ToArray();
            if (!PreflightReactionApplicationOccupancy(
                    group.Key,
                    existing,
                    frozenTarget,
                    orderedApplications,
                    ordered,
                    issues))
            {
                continue;
            }

            string? priorReactionEventRef = null;
            for (var index = 0; index < ordered.Length; index++)
            {
                var eventRef = ordered[index].Release.Reaction.EventRef;
                var expectation = index == 0
                    ? frozenTarget == null
                        ? ReactionReplacementExpectationKind.FrozenAbsent
                        : ReactionReplacementExpectationKind.FrozenExact
                    : ReactionReplacementExpectationKind
                        .PriorSelfReplacementResult;
                plans[eventRef] = ordered[index].Plan with
                {
                    ReplacementExpectation = expectation,
                    PriorReactionEventRef = priorReactionEventRef
                };
                priorReactionEventRef = eventRef;
            }
        }
        return plans;
    }

    private static bool TryResolveReactionApplicationSource(
        EffectReactionExecution reaction,
        EffectCarrierCatalog preMutationCatalog,
        EffectSourceAuthority sourceAuthority,
        WoundReactionLineageAuthority woundLineageAuthority,
        List<ValidationIssue> issues,
        out EffectSourceAuthorityEntry source,
        out WoundRootOwnershipDomain? woundOwnershipDomain)
    {
        source = null!;
        woundOwnershipDomain = null;
        var sourceKey = reaction.DownstreamSourceKey ??
                        reaction.DownstreamSource?.Key;
        if (sourceKey == null)
        {
            Add(
                issues,
                "effect.reactions",
                "effect_reaction_downstream_source_invalid",
                "one exact canonical downstream source key",
                reaction.EventRef);
            return false;
        }

        var producerIsWound = preMutationCatalog.TryResolveOne(
                reaction.EffectId,
                out var producer) &&
            producer.Effect["source"] is JsonObject producerSource &&
            TryReadExact(producerSource["kind"], out var producerSourceKind) &&
            string.Equals(producerSourceKind, "wound", StringComparison.Ordinal);
        var requestedIsWound = string.Equals(
            sourceKey.Kind,
            "wound",
            StringComparison.Ordinal);
        if (producerIsWound || requestedIsWound)
        {
            if (!producerIsWound || !requestedIsWound || producer is null ||
                producer.Effect["components"] is not JsonArray components)
            {
                Add(
                    issues,
                    "effect.reactions",
                    "effect_reaction_wound_lineage_producer_invalid",
                    "one exact active wound producer and wound-owned downstream source",
                    reaction.EventRef);
                return false;
            }
            var matchingComponents = components.OfType<JsonObject>()
                .Where(component =>
                    TryReadExact(component["componentId"], out var componentId) &&
                    string.Equals(
                        componentId,
                        reaction.ComponentId,
                        StringComparison.Ordinal))
                .ToArray();
            if (matchingComponents.Length != 1)
            {
                Add(
                    issues,
                    producer.JsonPath + ".components",
                    "effect_reaction_wound_lineage_producer_invalid",
                    "one exact persisted producer component for the released wound reaction",
                    reaction.ComponentId);
                return false;
            }
            var resolution = woundLineageAuthority.ResolveApplyDefinition(
                producer,
                matchingComponents[0],
                reaction.Target,
                sourceKey.DefinitionKey);
            issues.AddRange(resolution.Issues);
            if (!resolution.Success ||
                resolution.Source!.Key != sourceKey)
            {
                if (resolution.Success)
                {
                    Add(
                        issues,
                        "effect.reactions",
                        "effect_reaction_wound_lineage_source_invalid",
                        "the released downstream source key must equal the exact typed wound edge result",
                        reaction.EventRef);
                }
                return false;
            }
            source = resolution.Source;
            woundOwnershipDomain = resolution.OwnershipDomain;
            return true;
        }

        var canonical = sourceAuthority.ResolveCanonicalBinding(
            sourceKey,
            reaction.Target.Kind);
        issues.AddRange(canonical.Issues);
        if (!canonical.Success)
            return false;
        source = canonical.Source!;
        return true;
    }

    private static bool TryResolveEffectStackCoordinate(
        JsonObject effect,
        out EffectStackCoordinate coordinate)
    {
        coordinate = null!;
        if (!TryReadExact(effect["realm"], out var realm) ||
            effect["target"] is not JsonObject target ||
            !TryReadExact(target["kind"], out var targetKind) ||
            !TryReadExact(target["targetId"], out var targetId) ||
            effect["source"] is not JsonObject source ||
            !TryReadExact(source["kind"], out var sourceKind) ||
            !TryReadExact(source["sourceId"], out var sourceId) ||
            effect["stacking"] is not JsonObject stacking ||
            !TryReadExact(stacking["stackKey"], out var stackKey))
        {
            return false;
        }
        coordinate = new EffectStackCoordinate(
            realm,
            targetKind,
            targetId,
            sourceKind,
            sourceId,
            stackKey);
        return true;
    }

    private static string DescribeReplacementBatch(
        EffectStackCoordinate coordinate,
        IReadOnlyList<(
            ReleasedEffectReaction Release,
            ReactionApplicationPlan Plan)> releases) =>
        string.Join(
            "/",
            coordinate.Realm,
            coordinate.TargetKind,
            coordinate.TargetId,
            coordinate.SourceKind,
            coordinate.SourceId,
            coordinate.StackKey,
            string.Join(
                ",",
                releases.Select(static value =>
                    value.Release.Reaction.EffectId + "@" +
                    value.Release.Activation.EffectAuthority.BindingKind + ":" +
                    value.Release.Activation.EffectAuthority.AuthorityId)),
            releases[0].Release.Reaction.ReplacementTarget == null
                ? "absent"
                : releases[0].Release.Reaction.ReplacementTarget!.EffectId +
                  "@" +
                  releases[0].Release.Reaction.ReplacementTarget!.Authority
                      .BindingKind + ":" +
                  releases[0].Release.Reaction.ReplacementTarget!.Authority
                      .AuthorityId);

    private static bool PreflightReactionApplicationOccupancy(
        EffectStackCoordinate coordinate,
        IReadOnlyList<JsonObject> existing,
        EffectReplayIdentity? frozenTarget,
        IReadOnlyList<(
            ReleasedEffectReaction Release,
            ReactionApplicationPlan Plan)> orderedApplications,
        IReadOnlyList<(
            ReleasedEffectReaction Release,
            ReactionApplicationPlan Plan)> orderedReplacements,
        List<ValidationIssue> issues)
    {
        var occupancy = new List<ReactionStackOccupancy>(existing.Count);
        foreach (var effect in existing)
        {
            if (!TryReadReactionStackOccupancy(effect, out var entry))
            {
                Add(
                    issues,
                    "effect.reactions",
                    "effect_reaction_replacement_target_drift",
                    "complete canonical stacking authority for every frozen coordinate occupant",
                    DescribeReplacementBatch(coordinate, orderedReplacements));
                return false;
            }
            occupancy.Add(entry);
        }

        var replacementIndex = 0;
        foreach (var application in orderedApplications)
        {
            if (replacementIndex >= orderedReplacements.Count)
                break;
            if (!TryReadReactionApplicationStacking(
                    application.Plan,
                    out var createdOccupancy,
                    out var sourceMaximum,
                    out var atMaximum))
            {
                Add(
                    issues,
                    "effect.reactions",
                    "effect_reaction_replacement_authority_invalid",
                    "complete closed stacking authority for every released application on a replacement coordinate",
                    application.Release.Reaction.EventRef);
                return false;
            }

            if (application.Plan.IsReplacement)
            {
                var expectedReplacement =
                    orderedReplacements[replacementIndex];
                if (!string.Equals(
                        application.Release.Reaction.EventRef,
                        expectedReplacement.Release.Reaction.EventRef,
                        StringComparison.Ordinal))
                {
                    Add(
                        issues,
                        "effect.reactions",
                        "effect_reaction_replacement_authority_invalid",
                        "one monotonic replacement sequence inside the complete application sequence",
                        application.Release.Reaction.EventRef);
                    return false;
                }

                var expectedOccupancyCount = replacementIndex == 0 &&
                                             frozenTarget == null
                    ? 0
                    : 1;
                if (occupancy.Count != expectedOccupancyCount)
                {
                    Add(
                        issues,
                        "effect.reactions",
                        "effect_reaction_replacement_batch_conflict",
                        "the frozen replacement occupancy remains exact through every preceding apply-definition release",
                        DescribeReplacementApplicationConflict(
                            coordinate,
                            application,
                            occupancy.Count,
                            expectedOccupancyCount));
                    return false;
                }

                occupancy.Clear();
                occupancy.Add(createdOccupancy);
                replacementIndex++;
                continue;
            }

            if (!IsReactionApplicationStackCompatible(
                    occupancy,
                    createdOccupancy))
            {
                Add(
                    issues,
                    "effect.reactions",
                    "effect_reaction_replacement_batch_conflict",
                    "every preceding apply-definition has stack policy compatible with the simulated replacement occupancy",
                    DescribeReplacementApplicationConflict(
                        coordinate,
                        application,
                        occupancy.Count,
                        occupancy.Count));
                return false;
            }

            if (occupancy.Count == 0)
            {
                occupancy.Add(createdOccupancy);
                continue;
            }
            if (!string.Equals(
                    application.Plan.StackPolicy,
                    "independent",
                    StringComparison.Ordinal))
            {
                continue;
            }
            if (occupancy.Count < sourceMaximum)
            {
                occupancy.Add(createdOccupancy);
                continue;
            }
            if (!string.Equals(atMaximum, "no_change", StringComparison.Ordinal))
            {
                Add(
                    issues,
                    "effect.reactions",
                    "effect_reaction_replacement_batch_conflict",
                    "an independent application at maximum resolves as no_change before a later replacement",
                    DescribeReplacementApplicationConflict(
                        coordinate,
                        application,
                        occupancy.Count,
                        occupancy.Count));
                return false;
            }
        }

        if (replacementIndex == orderedReplacements.Count)
            return true;
        Add(
            issues,
            "effect.reactions",
            "effect_reaction_replacement_authority_invalid",
            "every replacement appears once in the monotonic application preflight",
            DescribeReplacementBatch(coordinate, orderedReplacements));
        return false;
    }

    private static bool TryReadReactionStackOccupancy(
        JsonObject effect,
        out ReactionStackOccupancy occupancy)
    {
        occupancy = null!;
        if (effect["stacking"] is not JsonObject stacking ||
            !TryReadExact(stacking["policy"], out var policy) ||
            !IsClosedReactionStackPolicy(policy) ||
            !TryReadPositiveInt(stacking["maxStacks"], out var maximum))
        {
            return false;
        }
        occupancy = new ReactionStackOccupancy(
            policy,
            maximum,
            stacking["refreshMode"]?.DeepClone(),
            stacking["mergeRule"]?.DeepClone());
        return true;
    }

    private static bool TryReadReactionApplicationStacking(
        ReactionApplicationPlan plan,
        out ReactionStackOccupancy createdOccupancy,
        out int sourceMaximum,
        out string atMaximum)
    {
        createdOccupancy = null!;
        sourceMaximum = 0;
        atMaximum = string.Empty;
        if (plan.Source.Definition["stacking"] is not JsonObject stacking ||
            !TryReadExact(stacking["policy"], out var policy) ||
            !string.Equals(policy, plan.StackPolicy, StringComparison.Ordinal) ||
            !IsClosedReactionStackPolicy(policy) ||
            !TryReadPositiveInt(stacking["maxStacks"], out sourceMaximum) ||
            !TryReadExact(stacking["atMaximum"], out atMaximum))
        {
            return false;
        }
        createdOccupancy = new ReactionStackOccupancy(
            policy,
            string.Equals(policy, "independent", StringComparison.Ordinal)
                ? 1
                : sourceMaximum,
            stacking["refreshMode"]?.DeepClone(),
            stacking["mergeRule"]?.DeepClone());
        return true;
    }

    private static bool IsReactionApplicationStackCompatible(
        IReadOnlyList<ReactionStackOccupancy> occupancy,
        ReactionStackOccupancy source)
    {
        if (occupancy.Count == 0)
            return true;
        if (!string.Equals(
                source.Policy,
                "independent",
                StringComparison.Ordinal) &&
            occupancy.Count > 1)
        {
            return false;
        }
        return occupancy.All(entry =>
            string.Equals(
                entry.Policy,
                source.Policy,
                StringComparison.Ordinal) &&
            entry.Maximum == source.Maximum &&
            JsonNode.DeepEquals(entry.RefreshMode, source.RefreshMode) &&
            JsonNode.DeepEquals(entry.MergeRule, source.MergeRule));
    }

    private static bool IsClosedReactionStackPolicy(string policy) =>
        policy is "independent" or "stack" or "refresh" or "replace" or
            "merge";

    private static string DescribeReplacementApplicationConflict(
        EffectStackCoordinate coordinate,
        (
            ReleasedEffectReaction Release,
            ReactionApplicationPlan Plan) application,
        int actualOccupancyCount,
        int expectedOccupancyCount) =>
        string.Join(
            "/",
            coordinate.Realm,
            coordinate.TargetKind,
            coordinate.TargetId,
            coordinate.SourceKind,
            coordinate.SourceId,
            coordinate.StackKey,
            application.Release.Reaction.EventRef,
            application.Plan.StackPolicy,
            "occupancy=" + actualOccupancyCount.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            "expected=" + expectedOccupancyCount.ToString(
                System.Globalization.CultureInfo.InvariantCulture));

    private static JsonObject? GetCarrierRoot(
        EffectCarrierCatalogInput carriers,
        string path) => path switch
    {
        EffectCarrierCatalog.PlayerPath => carriers.PlayerEffects,
        EffectCarrierCatalog.NpcPath => carriers.NpcEffects,
        EffectCarrierCatalog.EnemiesPath => carriers.EnemyCombatants,
        EffectCarrierCatalog.AlliesPath => carriers.AllyCombatants,
        EffectCarrierCatalog.AfterlifeProfilesPath => carriers.AfterlifeProfiles,
        EffectCarrierCatalog.SpiritualConflictPath => carriers.SpiritualConflict,
        _ => throw new InvalidOperationException(
            $"Unsupported effect carrier baseline path '{path}'.")
    };

    private static void PrepareCombatantTargets(
        EffectAcceptedTurnInput input,
        EffectCarrierCatalogInput carriers,
        EffectIdentityFactory identityFactory,
        List<ValidationIssue> issues,
        out EffectCarrierCatalogInput preparedCarriers,
        out EffectTargetAuthority targetAuthority,
        out IReadOnlyList<string> allocatedCombatantIds)
    {
        preparedCarriers = carriers;
        targetAuthority = input.TargetAuthority;
        allocatedCombatantIds = Array.Empty<string>();
        if (input.TargetAuthorityInput == null)
            return;

        if (input.PreallocatedCombatantIdentities != null)
        {
            targetAuthority = EffectTargetAuthority.Build(
                input.TargetAuthorityInput with
                {
                    CombatantIdentities = input.PreallocatedCombatantIdentities
                });
            allocatedCombatantIds = input.PreallocatedCombatantIdentities
                .CombatantIdsByRef
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(static pair => pair.Value)
                .ToArray();
            return;
        }

        var candidates = new JsonArray();
        var coordinates = new List<(bool Enemy, int Index)>();
        CollectNewCombatantCandidates(
            carriers.EnemyCombatants,
            "enemiesData",
            enemy: true,
            candidates,
            coordinates);
        CollectNewCombatantCandidates(
            carriers.AllyCombatants,
            "alliesData",
            enemy: false,
            candidates,
            coordinates);
        if (candidates.Count == 0)
            return;

        var identityBuild = CombatantIdentityState.BuildNew(
            candidates,
            identityFactory);
        issues.AddRange(identityBuild.Issues);
        if (identityBuild.State == null || identityBuild.Issues.Count > 0)
            return;

        var enemies = carriers.EnemyCombatants?.DeepClone().AsObject();
        var allies = carriers.AllyCombatants?.DeepClone().AsObject();
        for (var candidateIndex = 0; candidateIndex < coordinates.Count; candidateIndex++)
        {
            var coordinate = coordinates[candidateIndex];
            var root = coordinate.Enemy ? enemies : allies;
            var collection = coordinate.Enemy ? "enemiesData" : "alliesData";
            if (root?[collection] is not JsonArray combatants)
                continue;
            combatants[coordinate.Index] =
                identityBuild.RewrittenCombatants[candidateIndex]?.DeepClone();
        }

        preparedCarriers = carriers with
        {
            EnemyCombatants = enemies,
            AllyCombatants = allies
        };
        targetAuthority = EffectTargetAuthority.Build(
            input.TargetAuthorityInput with
            {
                CombatantIdentities = identityBuild.State
            });
        allocatedCombatantIds = identityBuild.State.CombatantIdsByRef
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => pair.Value)
            .ToArray();
    }

    private static void CollectNewCombatantCandidates(
        JsonObject? root,
        string collection,
        bool enemy,
        JsonArray candidates,
        List<(bool Enemy, int Index)> coordinates)
    {
        if (root?[collection] is not JsonArray combatants)
            return;
        for (var index = 0; index < combatants.Count; index++)
        {
            if (combatants[index] is not JsonObject combatant ||
                !combatant.ContainsKey("combatantRef"))
            {
                continue;
            }
            candidates.Add(combatant.DeepClone());
            coordinates.Add((enemy, index));
        }
    }

    private static void ParseOperations(
        EffectAcceptedTurnInput input,
        IReadOnlyList<EventAuthority> acceptedEvents,
        IReadOnlySet<string> processedEventRefs,
        List<ValidationIssue> issues,
        out List<Application> applications,
        out List<TerminalOperation> terminalOperations)
    {
        applications = new List<Application>();
        terminalOperations = new List<TerminalOperation>();
        var eventRefs = new HashSet<string>(StringComparer.Ordinal);
        if (input.RawCommands["effectChanges"] is not JsonArray changes)
            return;

        for (var index = 0; index < changes.Count; index++)
        {
            var path = $"effectChanges[{index}]";
            if (changes[index] is not JsonObject change)
            {
                Add(issues, path, "effect_plan_command_invalid", "closed effect operation", Describe(changes[index]));
                continue;
            }
            var issueCount = issues.Count;
            if (!TryReadExact(change["operation"], out var operation) ||
                operation is not ("apply" or "dispel" or "remove"))
            {
                Add(
                    issues,
                    path + ".operation",
                    "effect_plan_operation_unsupported",
                    "apply, dispel, or remove",
                    Describe(change["operation"]));
                continue;
            }

            var allowedFields = string.Equals(operation, "apply", StringComparison.Ordinal)
                ? ApplyFields
                : TerminalFields;
            foreach (var property in change)
            {
                if (ClientOwnedOrLegacyFields.Contains(property.Key) &&
                    !(property.Key == "effectId" &&
                      !string.Equals(operation, "apply", StringComparison.Ordinal)))
                {
                    Add(issues, path + "." + property.Key, "effect_plan_client_field_forbidden", "client-owned/post-state field absent", property.Key);
                }
                else if (!allowedFields.Contains(property.Key))
                {
                    Add(issues, path + "." + property.Key, "effect_plan_unknown_field", "registered " + operation + " field", property.Key);
                }
            }
            foreach (var field in allowedFields.Where(field =>
                         !(field == "parameters" &&
                           string.Equals(operation, "apply", StringComparison.Ordinal))))
            {
                if (!change.ContainsKey(field))
                    Add(issues, path + "." + field, "effect_plan_command_invalid", "required " + operation + " field", "missing");
            }
            if (!TryReadReadable(change["reason"], out _))
                Add(issues, path + ".reason", "effect_plan_command_invalid", "non-empty readable reason", Describe(change["reason"]));

            var acceptedEventRef = string.Empty;
            if (TryResolveEventRef(
                    change["eventRef"],
                    index < acceptedEvents.Count ? acceptedEvents[index] : null,
                    path + ".eventRef",
                    issues,
                    out acceptedEventRef))
            {
                var eventAlias = MortalLocationIdentityState.BuildConfusableKey(
                    acceptedEventRef);
                if (!eventRefs.Add(eventAlias))
                {
                    Add(
                        issues,
                        path + ".eventRef",
                        "effect_plan_event_replay_conflict",
                        "one effect operation for the exact accepted event",
                        acceptedEventRef);
                }
                if (processedEventRefs.Any(processed => string.Equals(
                        MortalLocationIdentityState.BuildConfusableKey(processed),
                        eventAlias,
                        StringComparison.Ordinal)))
                {
                    Add(
                        issues,
                        path + ".eventRef",
                        "effect_lifecycle_event_replay",
                        "accepted event absent from immutable effect history",
                        acceptedEventRef);
                }
            }

            if (change["target"] is not JsonObject target ||
                !TryReadExact(target["kind"], out var targetKind))
            {
                Add(issues, path + ".target", "effect_plan_command_invalid", "closed exact target selector", Describe(change["target"]));
                continue;
            }
            var targetResolution = input.TargetAuthority.Resolve(target, input.Realm);
            issues.AddRange(targetResolution.Issues.Select(issue => Prefix(issue, path + ".target")));

            if (string.Equals(operation, "apply", StringComparison.Ordinal))
            {
                ParseApplication(
                    input,
                    change,
                    targetKind,
                    targetResolution,
                    acceptedEventRef,
                    path,
                    issueCount,
                    issues,
                    applications);
                continue;
            }

            if (!TryReadExact(change["effectId"], out var effectId))
            {
                Add(issues, path + ".effectId", "effect_plan_command_invalid", "exact active effectId", Describe(change["effectId"]));
            }
            var authorityKind = string.Empty;
            var authorityId = string.Empty;
            if (change["authority"] is not JsonObject authority ||
                !HasOnly(authority, AuthorityFields.ToArray()) ||
                !TryReadExact(authority["kind"], out authorityKind) ||
                !TryReadExact(authority["authorityId"], out authorityId))
            {
                Add(issues, path + ".authority", "effect_plan_command_invalid", "closed exact removal authority", Describe(change["authority"]));
            }
            if (issues.Count == issueCount && targetResolution.Success)
            {
                terminalOperations.Add(new TerminalOperation(
                    operation,
                    effectId,
                    targetResolution.Target!,
                    authorityKind,
                    authorityId,
                    acceptedEventRef));
            }
        }
    }

    private static void ParseApplication(
        EffectAcceptedTurnInput input,
        JsonObject change,
        string targetKind,
        EffectTargetResolution targetResolution,
        string acceptedEventRef,
        string path,
        int issueCount,
        List<ValidationIssue> issues,
        List<Application> applications)
    {
        JsonObject? parameters = null;
        if (change.ContainsKey("parameters") && change["parameters"] != null)
        {
            if (change["parameters"] is JsonObject parameterObject)
                parameters = parameterObject;
            else
                Add(issues, path + ".parameters", "effect_plan_command_invalid", "closed object or null", Describe(change["parameters"]));
        }
        if (change["source"] is not JsonObject source ||
            !TryReadExact(source["kind"], out _) ||
            !TryReadExact(source["definitionKey"], out _))
        {
            Add(issues, path + ".source", "effect_plan_command_invalid", "closed exact source selector", Describe(change["source"]));
            return;
        }
        var sourceResolution = input.SourceAuthority.Resolve(
            source,
            input.Realm,
            targetKind,
            parameters);
        var sourceIssues = BuildApplicationSourceIssues(
            input,
            change,
            source,
            targetKind,
            targetResolution,
            path,
            issueCount,
            issues.Count,
            sourceResolution);
        issues.AddRange(sourceIssues);
        if (issues.Count == issueCount && sourceResolution.Success && targetResolution.Success)
        {
            if (string.Equals(
                    targetResolution.Target!.Kind,
                    "spiritual_conflict_side",
                    StringComparison.Ordinal) &&
                !AfterlifeSpiritualConflictState
                    .TryValidateCombatConditionParticipantBinding(
                        input.PreTurnCarriers?.SpiritualConflict,
                        targetResolution.Target,
                        sourceResolution.Source!.Definition,
                        out var participantReason))
            {
                Add(
                    issues,
                    path + ".source",
                    "effect_target_spiritual_participant_unresolved",
                    "source-owned actorId bound to one exact participant of the selected current conflict side",
                    participantReason);
                return;
            }

            applications.Add(new Application(
                sourceResolution.Source!,
                targetResolution.Target!,
                parameters?.DeepClone().AsObject(),
                acceptedEventRef,
                acceptedEventRef,
                path + ".apply.components",
                "effect_materialization"));
        }
    }

    private static IReadOnlyList<ValidationIssue> BuildApplicationSourceIssues(
        EffectAcceptedTurnInput input,
        JsonObject change,
        JsonObject source,
        string targetKind,
        EffectTargetResolution targetResolution,
        string operationPath,
        int operationIssueCount,
        int currentIssueCount,
        EffectSourceResolution sourceResolution)
    {
        var canBindRepair =
            currentIssueCount == operationIssueCount &&
            targetResolution.Success &&
            sourceResolution.Issues.Count == 1 &&
            string.Equals(
                sourceResolution.Issues[0].Code,
                "effect_source_parameter_required",
                StringComparison.Ordinal) &&
            sourceResolution.Issues[0].FilePath.StartsWith(
                "parameters.",
                StringComparison.Ordinal);
        EffectSourceAuthorityEntry? repairSource = null;
        if (canBindRepair)
        {
            var repairResolution = input.SourceAuthority.ResolveRepairCandidate(
                source,
                input.Realm,
                targetKind);
            if (repairResolution.Success)
                repairSource = repairResolution.Source;
        }

        return sourceResolution.Issues.Select(issue =>
        {
            var isParameter = issue.FilePath.StartsWith(
                "parameters.",
                StringComparison.Ordinal);
            var exactPath = isParameter
                ? operationPath + "." + issue.FilePath
                : operationPath + ".source." + issue.FilePath;
            EffectRepairContext? repairContext = null;
            if (repairSource != null &&
                isParameter &&
                TryResolveSingletonParameterValue(
                    repairSource.Definition,
                    issue.FilePath["parameters.".Length..],
                    out var expectedValueJson) &&
                change["target"] is JsonObject target &&
                change["eventRef"] is JsonObject eventRef &&
                TryReadExact(source["definitionKey"], out var definitionKey))
            {
                repairContext = new EffectRepairContext(
                    "effect-apply:" + operationPath,
                    "effectChanges",
                    exactPath,
                    source.DeepClone().AsObject(),
                    target.DeepClone().AsObject(),
                    definitionKey,
                    eventRef.DeepClone().AsObject(),
                    expectedValueJson);
            }

            var prefixed = new ValidationIssue(
                exactPath,
                issue.Severity,
                issue.Message,
                code: issue.Code,
                actor: repairContext?.Actor ?? issue.Actor,
                section: issue.Section,
                expected: issue.Expected,
                actual: issue.Actual,
                repairHint: issue.RepairHint,
                category: issue.Category,
                repairTargetFiles: repairContext == null
                    ? issue.RepairTargetFiles
                    : new[] { EffectAcceptedTurnPlan.CommandPath });
            prefixed.EffectRepairContext = repairContext;
            return prefixed;
        }).ToArray();
    }

    private static bool TryResolveSingletonParameterValue(
        JsonObject definition,
        string parameter,
        out string expectedValueJson)
    {
        expectedValueJson = string.Empty;
        if (definition["parameterBounds"] is not JsonObject bounds ||
            bounds[parameter] is not JsonObject bound ||
            bound["required"] is not JsonValue requiredValue ||
            !requiredValue.TryGetValue<bool>(out var required) ||
            !required ||
            !TryReadExact(bound["kind"], out var kind))
        {
            return false;
        }

        if (string.Equals(kind, "enum", StringComparison.Ordinal) &&
            bound["allowedValues"] is JsonArray { Count: 1 } allowed &&
            allowed[0] is JsonValue value &&
            value.TryGetValue<string>(out var token) &&
            !string.IsNullOrEmpty(token) &&
            string.Equals(token, token.Trim(), StringComparison.Ordinal))
        {
            expectedValueJson = value.ToJsonString();
            return true;
        }

        if (kind is not ("number" or "integer") ||
            !TryReadExactRepairNumber(bound["minimum"], out var minimum) ||
            !TryReadExactRepairNumber(bound["maximum"], out var maximum) ||
            minimum != maximum ||
            kind == "integer" && minimum != decimal.Truncate(minimum))
        {
            return false;
        }

        expectedValueJson = bound["minimum"]!.ToJsonString();
        return true;
    }

    private static bool TryReadExactRepairNumber(
        JsonNode? node,
        out decimal number)
    {
        number = 0;
        if (node is not JsonValue)
            return false;
        try
        {
            using var document = JsonDocument.Parse(node.ToJsonString());
            return ResourceMaterializationContract.TryReadExactDecimal(
                document.RootElement,
                out number);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void ApplyWoundTerminalOperation(
        WoundTerminalRequest request,
        CarrierWorkspace workspace,
        EffectIdentityState identities,
        EffectIdentityHistoryOwner identityRoot,
        EffectIdentityFactory identityFactory,
        int turn,
        List<string> transitionIds,
        List<JsonObject> activeEffects,
        HashSet<string> processedEventRefs,
        List<ValidationIssue> issues)
    {
        var operation = request.Operation;
        var catalog = EffectCarrierCatalog.Build(workspace.ToInput());
        issues.AddRange(catalog.Issues);
        if (!catalog.TryResolveOne(operation.EffectId, out var occurrence) ||
            !identities.TryGetEntry(operation.EffectId, out var identity) ||
            !WoundEffectTerminalOperationPlanner.OccurrenceAndIdentityAgree(
                occurrence,
                identity,
                operation.ExpectedSourceKey,
                operation.ExpectedTargetKey,
                operation.ExpectedCarrierCoordinate,
                operation.ExpectedIdentityOwner,
                operation.ExpectedStackCoordinate) ||
            !string.Equals(
                occurrence.FilePath,
                operation.ExpectedCarrierFilePath,
                StringComparison.Ordinal) ||
            !string.Equals(
                WoundEffectTerminalOperationPlanner.ComputeIdentityFingerprint(
                    identity),
                operation.ExpectedIdentityFingerprint,
                StringComparison.Ordinal))
        {
            AddWoundBatchIssue(
                issues,
                "prepared.terminalOperations",
                "wound_plan_effect_handoff_invalid",
                "the exact sealed wound-owned effect occurrence immediately before mutation",
                operation.OperationRef + "/" + operation.EffectId);
            return;
        }
        if (!workspace.TryRemoveEffect(operation.EffectId, out _))
        {
            AddWoundBatchIssue(
                issues,
                "prepared.terminalOperations",
                "wound_plan_effect_handoff_invalid",
                "one exact mutable wound-owned carrier occurrence",
                operation.OperationRef + "/" + operation.EffectId);
            return;
        }

        var transitionId = identityFactory.CreateTransitionId();
        transitionIds.Add(transitionId);
        AppendIdentityTransition(
            identityRoot,
            operation.EffectId,
            "expired",
            CreateTransition(
                transitionId,
                "expire",
                turn,
                operation.OperationRef,
                new[] { operation.EffectId },
                Array.Empty<string>()),
            issues);
        RemoveAffected(activeEffects, operation.EffectId);
        processedEventRefs.Add(operation.OperationRef);
    }

    private static void ApplyTerminalOperation(
        TerminalOperation operation,
        CarrierWorkspace workspace,
        EffectIdentityState identityState,
        EffectIdentityHistoryOwner identityRoot,
        EffectIdentityFactory identityFactory,
        int turn,
        List<string> transitionIds,
        HashSet<string> processedEventRefs,
        List<ValidationIssue> issues)
    {
        var catalog = EffectCarrierCatalog.Build(workspace.ToInput());
        issues.AddRange(catalog.Issues);
        if (!catalog.TryResolveOne(operation.EffectId, out var occurrence) ||
            !identityState.TryGetEntry(operation.EffectId, out var identity))
        {
            Add(
                issues,
                "effectChanges.effectId",
                "effect_lifecycle_terminal_effect_unresolved",
                "one exact active/suspended carrier and identity entry",
                operation.EffectId);
            return;
        }
        var targetKind = string.Empty;
        var targetId = string.Empty;
        if (occurrence.Effect["target"] is not JsonObject target ||
            !TryReadExact(target["kind"], out targetKind) ||
            !TryReadExact(target["targetId"], out targetId) ||
            !string.Equals(targetKind, operation.Target.Kind, StringComparison.Ordinal) ||
            !string.Equals(targetId, operation.Target.TargetId, StringComparison.Ordinal) ||
            !string.Equals(identity.Target["kind"]?.GetValue<string>(), operation.Target.Kind, StringComparison.Ordinal) ||
            !string.Equals(identity.Target["targetId"]?.GetValue<string>(), operation.Target.TargetId, StringComparison.Ordinal))
        {
            Add(
                issues,
                "effectChanges.target",
                "effect_lifecycle_terminal_target_mismatch",
                $"exact effect owner {targetKind}:{targetId}",
                $"{operation.Target.Kind}:{operation.Target.TargetId}");
            return;
        }
        if (identity.State is not ("active" or "suspended") ||
            !IsTerminalAuthorityAllowed(
                occurrence.Effect,
                operation.Operation,
                operation.AuthorityKind))
        {
            Add(
                issues,
                "effectChanges.authority",
                "effect_lifecycle_terminal_authority_forbidden",
                "exact source-declared dispel, cure, or manual authority for an active effect",
                operation.AuthorityKind + ":" + operation.AuthorityId);
            return;
        }
        if (!workspace.TryRemoveEffect(operation.EffectId, out _))
        {
            Add(
                issues,
                "effectChanges.effectId",
                "effect_lifecycle_terminal_effect_unresolved",
                "one mutable active carrier occurrence",
                operation.EffectId);
            return;
        }

        var transitionId = identityFactory.CreateTransitionId();
        transitionIds.Add(transitionId);
        AppendIdentityTransition(
            identityRoot,
            operation.EffectId,
            operation.Operation == "dispel" ? "dispelled" : "removed",
            CreateTransition(
                transitionId,
                operation.Operation,
                turn,
                operation.EventRef,
                new[] { operation.EffectId },
                Array.Empty<string>()),
            issues);
        processedEventRefs.Add(operation.EventRef);
    }

    private static bool IsTerminalAuthorityAllowed(
        JsonObject effect,
        string operation,
        string authorityKind)
    {
        if (effect["removal"] is not JsonObject removal)
            return false;
        var fields = string.Equals(operation, "dispel", StringComparison.Ordinal)
            ? new[] { "dispelCategories" }
            : new[] { "cureKinds", "manualAuthorities" };
        if (fields.Any(field => removal[field] is JsonArray values &&
            values.OfType<JsonValue>().Any(value =>
                value.TryGetValue<string>(out var candidate) &&
                string.Equals(candidate, authorityKind, StringComparison.Ordinal))))
        {
            return true;
        }
        return string.Equals(operation, "remove", StringComparison.Ordinal) &&
            effect["lifetime"] is JsonObject lifetime &&
            string.Equals(
                lifetime["mode"]?.GetValue<string>(),
                "manual",
                StringComparison.Ordinal) &&
            lifetime["authorities"] is JsonArray authorities &&
            authorities.OfType<JsonValue>().Any(value =>
                value.TryGetValue<string>(out var candidate) &&
                string.Equals(candidate, authorityKind, StringComparison.Ordinal));
    }

    private static bool ValidateReactionReplacementOccupancy(
        IReadOnlyList<JsonObject> existing,
        ReactionReplacementRuntimeExpectation expectation,
        int turn,
        string eventRef,
        List<ValidationIssue> issues)
    {
        var hasExactTarget = expectation.ExactTarget != null;
        if (expectation.ExpectsAbsence == hasExactTarget)
        {
            Add(
                issues,
                "effect.reactions",
                "effect_reaction_replacement_authority_invalid",
                "exactly one closed replacement expectation: frozen absence or one typed target",
                eventRef);
            return false;
        }
        if (expectation.ExpectsAbsence)
        {
            if (existing.Count == 0)
                return true;
            Add(
                issues,
                "effect.reactions",
                "effect_reaction_replacement_target_drift",
                "the replacement coordinate remains empty until its accepted reaction executes",
                eventRef + "/" + DescribeRuntimeReplacementOccupants(
                    existing,
                    turn));
            return false;
        }

        var target = expectation.ExactTarget!;
        if (existing.Count == 1 &&
            TryReadExact(existing[0]["effectId"], out var effectId) &&
            string.Equals(effectId, target.EffectId, StringComparison.Ordinal) &&
            ResolvePendingEffectAuthority(existing[0], turn) == target.Authority)
        {
            return true;
        }
        Add(
            issues,
            "effect.reactions",
            "effect_reaction_replacement_target_drift",
            "one runtime occupant matching the exact typed replacement expectation",
            eventRef + "/expected=" + target.EffectId + "@" +
            target.Authority.BindingKind + ":" +
            target.Authority.AuthorityId + "/actual=" +
            DescribeRuntimeReplacementOccupants(existing, turn));
        return false;
    }

    private static string DescribeRuntimeReplacementOccupants(
        IReadOnlyList<JsonObject> existing,
        int turn) =>
        existing.Count == 0
            ? "absent"
            : string.Join(
                ",",
                existing.Select(effect =>
                {
                    var effectId = TryReadExact(
                        effect["effectId"],
                        out var parsedEffectId)
                        ? parsedEffectId
                        : "missing";
                    var authority = ResolvePendingEffectAuthority(effect, turn);
                    return effectId + "@" + authority.BindingKind + ":" +
                           authority.AuthorityId;
                }));

    private static ApplicationExecutionFacts? ApplyApplication(
        Application application,
        string realm,
        JsonObject eventInput,
        CarrierWorkspace workspace,
        EffectIdentityHistoryOwner identityRoot,
        EffectIdentityFactory identityFactory,
        int turn,
        List<string> effectIds,
        List<string> transitionIds,
        List<JsonObject> activeEffects,
        HashSet<string> processedEventRefs,
        List<ValidationIssue> issues,
        ApplicationProvenance provenance,
        ReactionReplacementRuntimeExpectation? replacementExpectation = null,
        EffectRollSkillScopeAuthority? skillScopeAuthority = null)
    {
        var definition = application.Source.Definition;
        if (definition["display"] is not JsonObject display ||
            !TryReadExact(display["category"], out var category) ||
            definition["stacking"] is not JsonObject sourceStacking ||
            !TryReadExact(sourceStacking["stackKey"], out var stackKey) ||
            !TryReadExact(sourceStacking["policy"], out var stackPolicy) ||
            !workspace.TryLocate(application.Target, category, issues, out var slot) ||
            !TryBuildLifetime(
                definition,
                application.Source.Key,
                eventInput,
                issues,
                out var lifetime))
        {
            return null;
        }

        var components = definition["components"]!.DeepClone().AsArray();
        BindParameters(components, application.Parameters);
        var bindingIssues = ValidateBoundApplicationComponents(application, components, skillScopeAuthority);
        issues.AddRange(bindingIssues);
        if (bindingIssues.Count != 0)
            return null;
        var existing = workspace.FindStackEffects(
            realm,
            application.Target,
            application.Source.Key,
            stackKey);
        if (replacementExpectation != null &&
            (!string.Equals(stackPolicy, "replace", StringComparison.Ordinal) ||
             !ValidateReactionReplacementOccupancy(
                 existing,
                 replacementExpectation,
                 turn,
                 application.EventRef,
                 issues)))
        {
            return null;
        }
        var resolution = EffectLifecycleScheduler.ResolveApplication(
            new EffectStackApplicationInput(
                existing,
                definition,
                components,
                lifetime,
                application.EventRef,
                processedEventRefs));
        issues.AddRange(resolution.Issues);
        if (!resolution.Success)
            return null;
        if (replacementExpectation != null)
        {
            var resolutionMatches = resolution.CreatesNewIdentity &&
                (replacementExpectation.ExpectsAbsence
                    ? !resolution.TerminatesExisting &&
                      resolution.ExistingEffectId == null
                    : resolution.TerminatesExisting &&
                      string.Equals(
                          resolution.ExistingEffectId,
                          replacementExpectation.ExactTarget!.EffectId,
                          StringComparison.Ordinal));
            if (!resolutionMatches)
            {
                Add(
                    issues,
                    "effect.reactions",
                    "effect_reaction_replacement_target_drift",
                    "the replace scheduler retires exactly the runtime-checked target, or creates against the runtime-checked frozen absence",
                    application.EventRef + "/" + resolution.Outcome + "/" +
                    (resolution.ExistingEffectId ?? "absent"));
                return null;
            }
        }

        if (resolution.TerminatesExisting)
        {
            if (!TryExact(resolution.ExistingEffectId ?? string.Empty) ||
                !workspace.TryRemoveEffect(resolution.ExistingEffectId!, out _))
            {
                Add(
                    issues,
                    "activeEffects",
                    "effect_lifecycle_replace_target_unresolved",
                    "one exact existing stack-coordinate effect",
                    resolution.ExistingEffectId ?? "missing");
                return null;
            }
            RemoveAffected(activeEffects, resolution.ExistingEffectId!);
        }

        if (resolution.CreatesNewIdentity)
        {
            var effectId = identityFactory.CreateEffectId();
            effectIds.Add(effectId);
            if (resolution.TerminatesExisting)
            {
                var replaceTransitionId = identityFactory.CreateTransitionId();
                transitionIds.Add(replaceTransitionId);
                AppendIdentityTransition(
                    identityRoot,
                    resolution.ExistingEffectId!,
                    "replaced",
                    CreateTransition(
                        replaceTransitionId,
                        "replace",
                        turn,
                        application.EventRef,
                        new[] { resolution.ExistingEffectId! },
                        new[] { effectId }),
                    issues);
            }

            var createTransitionId = identityFactory.CreateTransitionId();
            transitionIds.Add(createTransitionId);
            var createEventRef = resolution.TerminatesExisting
                ? CreateApplicationTransitionEventRef(
                    application.EventRef,
                    "replacement_result_create")
                : application.EventRef;
            var candidate = new PreparedApplication(application, slot, resolution.NewEffectLifetime);
            var effect = CreateEffect(
                candidate,
                realm,
                effectId,
                createTransitionId,
                turn,
                createEventRef,
                resolution.NewEffectComponents,
                resolution.NewEffectStacking);
            if (string.Equals(
                    application.Target.Kind,
                    "spiritual_conflict_side",
                    StringComparison.Ordinal))
            {
                if (!AfterlifeSpiritualConflictState.TryProjectCombatCondition(
                        effect,
                        out var condition,
                        out var adapterReason))
                {
                    Add(
                        issues,
                        "effectChanges.target",
                        "effect_plan_afterlife_condition_adapter_invalid",
                        "one complete finite afterlife combat-condition projection",
                        adapterReason);
                    return null;
                }
                effect = condition;
            }
            slot.Collection.Add(effect.DeepClone());
            workspace.Touch(slot);
            activeEffects.Add(effect);
            var identityEntry = CreateIdentityEntry(
                effect,
                slot,
                effectId,
                createTransitionId,
                turn,
                createEventRef,
                provenance);
            identityRoot.CreateEntry(identityEntry);
            processedEventRefs.Add(application.EventRef);
            var reactionResult = new ReactionApplicationResult(
                new EffectReplayIdentity(
                    effectId,
                    ResolvePendingEffectAuthority(effect, turn)),
                resolution.TerminatesExisting
                    ? replacementExpectation?.ExactTarget
                    : null);
            return new ApplicationExecutionFacts(
                resolution.TerminatesExisting
                    ? "replace"
                    : "created_new_identity",
                effectId,
                createTransitionId,
                createEventRef,
                application.CausalEventRef,
                application.Source.Key,
                application.Target,
                slot.Coordinate,
                effect.DeepClone().AsObject(),
                identityEntry.DeepClone().AsObject(),
                reactionResult,
                provenance);
        }

        if (!TryExact(resolution.ExistingEffectId ?? string.Empty) ||
            resolution.UpdatedExistingEffect is not JsonObject updated)
        {
            Add(
                issues,
                "activeEffects",
                "effect_lifecycle_stack_result_invalid",
                "one updated existing effect for non-create stack result",
                resolution.Outcome);
            return null;
        }

        var transitionKind = resolution.Outcome == "no_change"
            ? "stack"
            : resolution.Outcome;
        var transitionId = identityFactory.CreateTransitionId();
        transitionIds.Add(transitionId);
        UpdateEffectChronology(updated, transitionId, turn);
        if (!workspace.TryReplaceEffect(resolution.ExistingEffectId!, updated))
        {
            Add(
                issues,
                "activeEffects",
                "effect_lifecycle_stack_target_unresolved",
                "one exact mutable existing effect",
                resolution.ExistingEffectId!);
            return null;
        }
        AppendIdentityTransition(
            identityRoot,
            resolution.ExistingEffectId!,
            updated["state"]!.GetValue<string>(),
            CreateTransition(
                transitionId,
                transitionKind,
                turn,
                application.EventRef,
                new[] { resolution.ExistingEffectId! },
                new[] { resolution.ExistingEffectId! }),
            issues);
        AddOrReplaceAffected(activeEffects, updated);
        processedEventRefs.Add(application.EventRef);
        return new ApplicationExecutionFacts(
            resolution.Outcome,
            resolution.ExistingEffectId!,
            transitionId,
            null,
            application.CausalEventRef,
            application.Source.Key,
            application.Target,
            slot.Coordinate,
            null,
            null,
            null,
            provenance);
    }

    private static IReadOnlyList<ValidationIssue> ValidateBoundApplicationComponents(
        Application application,
        JsonArray boundComponents,
        EffectRollSkillScopeAuthority? authority)
    {
        var issues = new List<ValidationIssue>();
        var index = 0;
        foreach (var component in JsonSerializer.SerializeToElement(boundComponents).EnumerateArray())
            EffectComponentProfiles.ValidateComponent(component, $"{application.Path}[{index++}]", issues);
        // Every application retains structural validation, including accepted continuations.
        // Only a well-formed new selector reaches Offered/Current scope resolution.
        if (issues.Count == 0 && !application.AcceptedContinuation && authority is not null)
            issues.AddRange(authority.ValidateNewComponents(
                application.Target, boundComponents, application.Path, application.Section));
        return application.Section != "wound_materialization" ? issues : issues.Select(issue =>
            new ValidationIssue(issue.FilePath, issue.Severity, issue.Message,
                code: "wound_materialization_effect_binding_invalid", section: application.Section,
                expected: issue.Expected, actual: issue.Actual, repairHint: issue.RepairHint)).ToArray();
    }

    private static void ApplyDueLifecycleEvents(
        JsonObject eventInput,
        EffectSourceAuthority? sourceAuthority,
        CarrierWorkspace workspace,
        EffectIdentityHistoryOwner identityRoot,
        EffectIdentityFactory identityFactory,
        List<string> transitionIds,
        List<JsonObject> activeEffects,
        HashSet<string> processedEventRefs,
        List<ValidationIssue> issues,
        bool boundContinuationsOnly)
    {
        if (eventInput["lifecycleEvents"] is not JsonArray lifecycleEvents)
            return;
        foreach (var node in lifecycleEvents)
        {
            if (!TryParseLifecycleAuthority(node, issues, out var authority))
                continue;
            var occurrences = EffectCarrierCatalog.Build(workspace.ToInput()).Occurrences
                .Where(occurrence =>
                    (authority.EffectId == null ||
                     string.Equals(
                         occurrence.EffectId,
                         authority.EffectId,
                         StringComparison.Ordinal)) &&
                    string.Equals(
                        occurrence.Effect["realm"]?.GetValue<string>(),
                        authority.Target.Realm,
                        StringComparison.Ordinal) &&
                    occurrence.Effect["target"] is JsonObject target &&
                    string.Equals(
                        target["kind"]?.GetValue<string>(),
                        authority.Target.Kind,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        target["targetId"]?.GetValue<string>(),
                        authority.Target.TargetId,
                        StringComparison.Ordinal) &&
                    !WasCreatedByCausalEvent(
                        occurrence.Effect,
                        authority.CausalEventRef))
                .OrderBy(static occurrence => occurrence.EffectId, StringComparer.Ordinal)
                .ToArray();
            foreach (var occurrence in occurrences)
            {
                var mode = occurrence.Effect["lifetime"]?["mode"]?.GetValue<string>();
                if (string.Equals(mode, "uses", StringComparison.Ordinal))
                    continue;
                var isBoundContinuation = mode is "source_bound" or "condition_bound";
                if (boundContinuationsOnly != isBoundContinuation)
                    continue;
                var triggerId = authority.TriggerId;
                if (triggerId == null &&
                    string.Equals(mode, "uses", StringComparison.Ordinal) &&
                    IsAfterlifeCombatCondition(occurrence.Effect) &&
                    string.Equals(
                        authority.Phase,
                        "afterlife_exchange_end",
                        StringComparison.Ordinal) &&
                    !TryResolveOneConsumingTrigger(
                        occurrence.Effect,
                        authority.Phase,
                        out triggerId))
                {
                    Add(
                        issues,
                        occurrence.JsonPath + ".triggers",
                        "effect_lifecycle_consuming_trigger_invalid",
                        "one exact afterlife exchange trigger consuming the bounded condition use",
                        authority.Phase);
                    continue;
                }
                var eventRef = CreateLifecycleTransitionEventRef(
                    authority.EventRef,
                    occurrence.EffectId,
                    ResolvePendingEffectAuthority(
                        occurrence.Effect,
                        authority.Turn));
                var sourceSatisfied = authority.SourceSatisfied;
                if (mode == "source_bound" && !sourceSatisfied.HasValue)
                {
                    if (sourceAuthority == null)
                    {
                        Add(
                            issues,
                            "effect.source",
                            "effect_lifecycle_source_authority_missing",
                            "one exact source authority for source-bound continuation",
                            occurrence.EffectId);
                        continue;
                    }
                    sourceSatisfied = IsSourceBindingSatisfied(
                        occurrence.Effect,
                        sourceAuthority);
                }
                ApplyLifecycleReduction(
                    occurrence,
                    new EffectLifecycleEvent(
                        eventRef,
                        authority.Turn,
                        authority.Phase,
                        triggerId,
                        authority.CurrentTime,
                        authority.CurrentSceneId,
                        authority.SceneClosed,
                    sourceSatisfied,
                    authority.ConditionSatisfied,
                    authority.CurrentRealm,
                    authority.CausalEventRef,
                    authority.TargetSatisfied),
                    workspace,
                    identityRoot,
                    identityFactory,
                    transitionIds,
                    activeEffects,
                    processedEventRefs,
                    issues);
            }
        }
    }

    private static void ApplyAcceptedTerminalReactionFold(
        IReadOnlyList<ReleasedEffectReaction> releasedReactions,
        JsonObject eventInput,
        CarrierWorkspace workspace,
        EffectIdentityHistoryOwner identityRoot,
        EffectIdentityFactory identityFactory,
        List<string> effectIds,
        List<string> transitionIds,
        List<JsonObject> activeEffects,
        List<EffectSourceAuthorityEntry> usedSources,
        List<EffectTargetKey> usedTargets,
        HashSet<string> processedEventRefs,
        List<ValidationIssue> issues,
        EffectSourceAuthority sourceAuthority,
        Dictionary<EffectSourceKey, EffectSourceAuthorityEntry>
            reactionSourceBindings)
    {
        foreach (var group in releasedReactions
                     .Where(static released =>
                         IsTerminalAvailabilityReaction(released.Reaction))
                     .GroupBy(static released => released.Reaction.EffectId)
                     .OrderBy(static group =>
                         group.Min(static released =>
                             released.MechanicsOrdinal)))
        {
            var acceptedBoundaries = group
                .Select(static released => released.Boundary.BoundaryOrdinal)
                .Distinct()
                .ToArray();
            if (acceptedBoundaries.Length != 1)
            {
                Add(
                    issues,
                    "effect.reactions",
                    "effect_reaction_terminal_boundary_conflict",
                    "terminal releases for one effect from one frozen accepted boundary",
                    group.Key);
                continue;
            }
            var ordered = group
                .OrderBy(static released => released.MechanicsOrdinal)
                .ToArray();
            var winner = ordered.FirstOrDefault(static released =>
                IsReactionBehavior(
                    released.Reaction,
                    EffectReactionResultBehavior.Remove)) ??
                ordered[0];
            var reaction = winner.Reaction;
            var catalog = EffectCarrierCatalog.Build(workspace.ToInput());
            issues.AddRange(catalog.Issues);
            if (catalog.TryResolveOne(reaction.EffectId, out _))
            {
                ApplyReactionExecution(
                    reaction,
                    reaction.Target.Realm,
                    eventInput,
                    workspace,
                    identityRoot,
                    identityFactory,
                    effectIds,
                    transitionIds,
                    activeEffects,
                    usedSources,
                    usedTargets,
                    processedEventRefs,
                    issues,
                    sourceAuthority,
                    reactionSourceBindings);
                continue;
            }
            if (IsReactionBehavior(
                    reaction,
                    EffectReactionResultBehavior.Remove))
            {
                AppendAcceptedTerminalAfterEarlierTerminal(
                    winner,
                    identityRoot,
                    identityFactory,
                    transitionIds,
                    processedEventRefs,
                    issues);
                continue;
            }
            AppendAcceptedTerminalAfterEarlierTerminal(
                winner,
                identityRoot,
                identityFactory,
                transitionIds,
                processedEventRefs,
                issues);
        }
    }

    private static void AppendAcceptedTerminalAfterEarlierTerminal(
        ReleasedEffectReaction released,
        EffectIdentityHistoryOwner identityRoot,
        EffectIdentityFactory identityFactory,
        List<string> transitionIds,
        HashSet<string> processedEventRefs,
        List<ValidationIssue> issues)
    {
        var reaction = released.Reaction;
        if (!HasAcceptedTerminalProjection(
                released,
                identityRoot,
                processedEventRefs))
        {
            Add(
                issues,
                "effect.reaction.effectId",
                "effect_reaction_terminal_fold_invalid",
                "one earlier accepted terminal projection for this activation",
                reaction.EffectId);
            return;
        }
        var isRemove = IsReactionBehavior(
            reaction,
            EffectReactionResultBehavior.Remove);
        if (!isRemove)
        {
            var entry = identityRoot.FindEntries(reaction.EffectId).SingleOrDefault();
            if (entry?["transitions"] is not JsonArray transitions ||
                transitions.Count == 0)
            {
                Add(
                    issues,
                    EffectAcceptedTurnPlan.IdentityIndexPath,
                    "effect_reaction_terminal_fold_invalid",
                    "one exact earlier terminal identity transition",
                    reaction.EffectId);
                return;
            }
            var suspendTransitionId = identityFactory.CreateTransitionId();
            transitionIds.Add(suspendTransitionId);
            identityRoot.InsertBeforeTransition(
                reaction.EffectId,
                transitions.Count - 1,
                transitions[^1]!.AsObject(),
                CreateTransition(
                    suspendTransitionId,
                    "suspend",
                    reaction.Turn,
                    reaction.EventRef,
                    new[] { reaction.EffectId },
                    new[] { reaction.EffectId }));
            processedEventRefs.Add(reaction.EventRef);
            return;
        }
        var transitionId = identityFactory.CreateTransitionId();
        transitionIds.Add(transitionId);
        AppendIdentityTransition(
            identityRoot,
            reaction.EffectId,
            "removed",
            CreateTransition(
                transitionId,
                "remove",
                reaction.Turn,
                reaction.EventRef,
                new[] { reaction.EffectId },
                Array.Empty<string>()),
            issues);
        processedEventRefs.Add(reaction.EventRef);
    }

    private static bool HasAcceptedTerminalProjection(
        ReleasedEffectReaction released,
        EffectIdentityHistoryOwner identityRoot,
        IReadOnlySet<string> processedEventRefs)
    {
        if (!processedEventRefs.Contains(
                released.Activation.Identity.EventRef))
        {
            return false;
        }
        var entries = identityRoot.FindEntries(released.Reaction.EffectId);
        if (entries.Length != 1)
            return false;
        return entries[0]["state"]?.GetValue<string>() is
            "expired" or "dispelled" or "removed" or "replaced";
    }

    private static void ApplyReactionExecution(
        EffectReactionExecution reaction,
        string realm,
        JsonObject eventInput,
        CarrierWorkspace workspace,
        EffectIdentityHistoryOwner identityRoot,
        EffectIdentityFactory identityFactory,
        List<string> effectIds,
        List<string> transitionIds,
        List<JsonObject> activeEffects,
        List<EffectSourceAuthorityEntry> usedSources,
        List<EffectTargetKey> usedTargets,
        HashSet<string> processedEventRefs,
        List<ValidationIssue> issues,
        EffectSourceAuthority? sourceAuthority = null,
        Dictionary<EffectSourceKey, EffectSourceAuthorityEntry>?
            sourceBindingCache = null,
        IReadOnlyDictionary<string, ReactionApplicationPlan>?
            reactionApplicationPlans = null,
        IDictionary<string, ReactionApplicationResult>?
            reactionApplicationResults = null,
        EffectRollSkillScopeAuthority? skillScopeAuthority = null)
    {
        if (processedEventRefs.Contains(reaction.EventRef))
        {
            Add(
                issues,
                "eventInput.lifecycleEvents",
                "effect_lifecycle_event_replay",
                "one unprocessed exact reaction event",
                reaction.EventRef);
            return;
        }

        if (!EffectReactionResultCatalog.TryResolve(
                reaction.ResultKind,
                out var descriptor))
        {
            Add(
                issues,
                "effect.reaction.resultKind",
                "effect_reaction_result_unsupported",
                "registered executable reaction result",
                reaction.ResultKind);
            return;
        }

        if (descriptor.Behavior == EffectReactionResultBehavior.ApplyDefinition)
        {
            ReactionApplicationPlan? applicationPlan = null;
            if (reactionApplicationPlans != null &&
                !reactionApplicationPlans.TryGetValue(
                    reaction.EventRef,
                    out applicationPlan))
            {
                Add(
                    issues,
                    "effect.reactions",
                    "effect_reaction_application_plan_missing",
                    "one preflighted application plan for every released apply-definition reaction",
                    reaction.EventRef);
                return;
            }
            var downstreamSource = applicationPlan?.Source ??
                                   reaction.DownstreamSource;
            if (downstreamSource == null &&
                reaction.DownstreamSourceKey is { } downstreamKey &&
                sourceAuthority != null)
            {
                sourceBindingCache ??= new Dictionary<
                    EffectSourceKey,
                    EffectSourceAuthorityEntry>();
                if (!sourceBindingCache.TryGetValue(
                        downstreamKey,
                        out downstreamSource))
                {
                    var resolution = sourceAuthority.ResolveCanonicalBinding(
                        downstreamKey,
                        reaction.Target.Kind);
                    issues.AddRange(resolution.Issues);
                    if (resolution.Success)
                    {
                        downstreamSource = resolution.Source!;
                        sourceBindingCache.Add(downstreamKey, downstreamSource);
                    }
                }
            }
            if (downstreamSource == null)
            {
                Add(
                    issues,
                    "effect.reaction.definitionKey",
                    "effect_reaction_downstream_source_invalid",
                    "one resolved same-source downstream definition",
                    "missing");
                return;
            }
            ReactionReplacementRuntimeExpectation? replacementExpectation =
                null;
            if (applicationPlan?.IsReplacement == true)
            {
                switch (applicationPlan.ReplacementExpectation)
                {
                    case ReactionReplacementExpectationKind.FrozenAbsent:
                        replacementExpectation = new(
                            ExactTarget: null,
                            ExpectsAbsence: true);
                        break;
                    case ReactionReplacementExpectationKind.FrozenExact:
                        if (reaction.ReplacementTarget == null)
                        {
                            Add(
                                issues,
                                "effect.reactions",
                                "effect_reaction_replacement_authority_invalid",
                                "one exact typed frozen replacement target",
                                reaction.EventRef);
                            return;
                        }
                        replacementExpectation = new(
                            reaction.ReplacementTarget,
                            ExpectsAbsence: false);
                        break;
                    case ReactionReplacementExpectationKind
                            .PriorSelfReplacementResult:
                        if (applicationPlan.PriorReactionEventRef == null ||
                            reactionApplicationResults == null ||
                            !reactionApplicationResults.TryGetValue(
                                applicationPlan.PriorReactionEventRef,
                                out var priorResult))
                        {
                            Add(
                                issues,
                                "effect.reactions",
                                "effect_reaction_replacement_authority_invalid",
                                "the exact result identity of the preceding accepted self-replacement",
                                reaction.EventRef);
                            return;
                        }
                        replacementExpectation = new(
                            priorResult.ResultIdentity,
                            ExpectsAbsence: false);
                        break;
                    default:
                        Add(
                            issues,
                            "effect.reactions",
                            "effect_reaction_replacement_authority_invalid",
                            "one closed runtime replacement expectation",
                            reaction.EventRef);
                        return;
                }
            }
            var applicationExecution = ApplyApplication(
                new Application(
                    downstreamSource,
                    reaction.Target,
                    reaction.Parameters?.DeepClone().AsObject(),
                    reaction.EventRef,
                    reaction.CausalEventRef,
                    $"effect.reactions[{reaction.EventRef}].components",
                    "effect_materialization"),
                realm,
                eventInput,
                workspace,
                identityRoot,
                identityFactory,
                reaction.Turn,
                effectIds,
                transitionIds,
                activeEffects,
                processedEventRefs,
                issues,
                new ApplicationProvenance.Reaction(reaction.EffectId),
                replacementExpectation,
                skillScopeAuthority);
            var applicationResult = applicationExecution?.ReactionResult;
            if (issues.Count == 0 && applicationPlan?.IsReplacement == true)
            {
                if (applicationResult == null ||
                    reactionApplicationResults == null ||
                    !reactionApplicationResults.TryAdd(
                        reaction.EventRef,
                        applicationResult))
                {
                    Add(
                        issues,
                        "effect.reactions",
                        "effect_reaction_replacement_authority_invalid",
                        "one exact materialized result for every preflighted replacement reaction",
                        reaction.EventRef);
                    return;
                }
            }
            if (issues.Count == 0)
            {
                usedSources.Add(downstreamSource);
                usedTargets.Add(reaction.Target);
            }
            return;
        }

        if (descriptor.Behavior == EffectReactionResultBehavior.PeriodicComponent)
            return;

        var catalog = EffectCarrierCatalog.Build(workspace.ToInput());
        issues.AddRange(catalog.Issues);
        if (!catalog.TryResolveOne(reaction.EffectId, out var occurrence))
        {
            Add(
                issues,
                "effect.reaction.effectId",
                "effect_reaction_target_unresolved",
                "one exact active effect selected by client event authority",
                reaction.EffectId);
            return;
        }

        var transitionId = identityFactory.CreateTransitionId();
        transitionIds.Add(transitionId);
        if (descriptor.Behavior == EffectReactionResultBehavior.EventOutcome)
        {
            var triggered = occurrence.Effect.DeepClone().AsObject();
            UpdateEffectChronology(triggered, transitionId, reaction.Turn);
            if (!workspace.TryReplaceEffect(reaction.EffectId, triggered))
            {
                Add(
                    issues,
                    "effect.reaction.effectId",
                    "effect_reaction_target_unresolved",
                    "one exact mutable active effect",
                    reaction.EffectId);
                return;
            }
            AppendIdentityTransition(
                identityRoot,
                reaction.EffectId,
                "active",
                CreateTransition(
                    transitionId,
                    "trigger",
                    reaction.Turn,
                    reaction.EventRef,
                    new[] { reaction.EffectId },
                    new[] { reaction.EffectId }),
                issues);
            AddOrReplaceAffected(activeEffects, triggered);
            processedEventRefs.Add(reaction.EventRef);
            return;
        }
        if (descriptor.Behavior == EffectReactionResultBehavior.Remove)
        {
            if (!workspace.TryRemoveEffect(reaction.EffectId, out _))
            {
                Add(
                    issues,
                    "effect.reaction.effectId",
                    "effect_reaction_target_unresolved",
                    "one exact mutable active effect",
                    reaction.EffectId);
                return;
            }
            AppendIdentityTransition(
                identityRoot,
                reaction.EffectId,
                "removed",
                CreateTransition(
                    transitionId,
                    "remove",
                    reaction.Turn,
                    reaction.EventRef,
                    new[] { reaction.EffectId },
                    Array.Empty<string>()),
                issues);
            RemoveAffected(activeEffects, reaction.EffectId);
            processedEventRefs.Add(reaction.EventRef);
            return;
        }

        if (descriptor.Behavior != EffectReactionResultBehavior.Suspend)
        {
            Add(
                issues,
                "effect.reaction.resultKind",
                "effect_reaction_result_unsupported",
                "registered executable reaction result",
                reaction.ResultKind);
            return;
        }

        var suspended = occurrence.Effect.DeepClone().AsObject();
        suspended["state"] = "suspended";
        UpdateEffectChronology(suspended, transitionId, reaction.Turn);
        if (!workspace.TryReplaceEffect(reaction.EffectId, suspended))
        {
            Add(
                issues,
                "effect.reaction.effectId",
                "effect_reaction_target_unresolved",
                "one exact mutable active effect",
                reaction.EffectId);
            return;
        }
        AppendIdentityTransition(
            identityRoot,
            reaction.EffectId,
            "suspended",
            CreateTransition(
                transitionId,
                "suspend",
                reaction.Turn,
                reaction.EventRef,
                new[] { reaction.EffectId },
                new[] { reaction.EffectId }),
            issues);
        AddOrReplaceAffected(activeEffects, suspended);
        processedEventRefs.Add(reaction.EventRef);
    }

    private static bool IsAfterlifeCombatCondition(JsonObject effect) =>
        effect["components"] is JsonArray { Count: 1 } components &&
        components[0] is JsonObject component &&
        string.Equals(
            component["profile"]?.GetValue<string>(),
            "afterlife_combat_condition",
            StringComparison.Ordinal);

    private static bool TryResolveOneConsumingTrigger(
        JsonObject effect,
        string phase,
        out string? triggerId)
    {
        triggerId = null;
        if (effect["triggers"] is not JsonArray triggers)
            return false;
        var matching = triggers
            .OfType<JsonObject>()
            .Where(trigger =>
                string.Equals(
                    trigger["eventType"]?.GetValue<string>(),
                    phase,
                    StringComparison.Ordinal) &&
                trigger["consumeUses"]?.GetValue<bool>() == true)
            .Select(trigger => trigger["triggerId"]?.GetValue<string>())
            .Where(static id => !string.IsNullOrEmpty(id))
            .ToArray();
        if (matching.Length != 1)
            return false;
        triggerId = matching[0];
        return true;
    }

    private static void ApplyLifecycleReduction(
        EffectCarrierOccurrence occurrence,
        EffectLifecycleEvent lifecycleEvent,
        CarrierWorkspace workspace,
        EffectIdentityHistoryOwner identityRoot,
        EffectIdentityFactory identityFactory,
        List<string> transitionIds,
        List<JsonObject> activeEffects,
        HashSet<string> processedEventRefs,
        List<ValidationIssue> issues)
    {
        var reduction = EffectLifecycleScheduler.AdvanceLifetime(
            new EffectLifetimeReductionInput(
                occurrence.Effect,
                lifecycleEvent,
                processedEventRefs));
        issues.AddRange(reduction.Issues);
        if (!reduction.Success || reduction.Outcome == "no_change")
            return;

        var transitionId = identityFactory.CreateTransitionId();
        transitionIds.Add(transitionId);
        if (reduction.Outcome == "expire")
        {
            if (!workspace.TryRemoveEffect(occurrence.EffectId, out _))
            {
                Add(
                    issues,
                    "activeEffects",
                    "effect_lifecycle_expiry_target_unresolved",
                    "one exact mutable active effect",
                    occurrence.EffectId);
                return;
            }
            AppendIdentityTransition(
                identityRoot,
                occurrence.EffectId,
                "expired",
                CreateTransition(
                    transitionId,
                    "expire",
                    lifecycleEvent.Turn,
                    lifecycleEvent.EventRef,
                    new[] { occurrence.EffectId },
                    Array.Empty<string>()),
                issues);
            RemoveAffected(activeEffects, occurrence.EffectId);
        }
        else if (reduction.UpdatedEffect is JsonObject updated)
        {
            UpdateEffectChronology(updated, transitionId, lifecycleEvent.Turn);
            if (!workspace.TryReplaceEffect(occurrence.EffectId, updated))
            {
                Add(
                    issues,
                    "activeEffects",
                    "effect_lifecycle_update_target_unresolved",
                    "one exact mutable active effect",
                    occurrence.EffectId);
                return;
            }
            var transitionKind = reduction.Outcome == "advance"
                ? "consume"
                : reduction.Outcome;
            AppendIdentityTransition(
                identityRoot,
                occurrence.EffectId,
                updated["state"]!.GetValue<string>(),
                CreateTransition(
                    transitionId,
                    transitionKind,
                    lifecycleEvent.Turn,
                    lifecycleEvent.EventRef,
                    new[] { occurrence.EffectId },
                    new[] { occurrence.EffectId }),
                issues);
            AddOrReplaceAffected(activeEffects, updated);
        }
        processedEventRefs.Add(lifecycleEvent.EventRef);
    }

    private static bool TryApplyConsumingTriggerEvidenceBeforeReplacement(
        EffectResourceTriggerExecution execution,
        int turn,
        Dictionary<string, JsonObject> preReactionEffects,
        IReadOnlyList<ReleasedEffectReaction> releasedReactions,
        EffectIdentityHistoryOwner identityRoot,
        EffectIdentityFactory identityFactory,
        List<string> transitionIds,
        HashSet<string> processedEventRefs,
        List<ValidationIssue> issues)
    {
        var entries = identityRoot.FindEntries(execution.EffectId);
        if (entries.Length != 1 ||
            !string.Equals(
                entries[0]["state"]?.GetValue<string>(),
                "replaced",
                StringComparison.Ordinal) ||
            entries[0]["transitions"] is not JsonArray { Count: > 0 } transitions ||
            transitions[^1] is not JsonObject replacementTransition ||
            !string.Equals(
                replacementTransition["kind"]?.GetValue<string>(),
                "replace",
                StringComparison.Ordinal) ||
            !TryReadExact(replacementTransition["eventRef"], out var replacementEventRef))
        {
            return false;
        }

        var matchingReleases = releasedReactions
            .Where(released =>
                IsReactionBehavior(
                    released.Reaction,
                    EffectReactionResultBehavior.ApplyDefinition) &&
                string.Equals(
                    released.Reaction.EventRef,
                    replacementEventRef,
                    StringComparison.Ordinal) &&
                string.Equals(
                    released.Reaction.ReplacementTargetEffectId,
                    execution.EffectId,
                    StringComparison.Ordinal))
            .ToArray();
        if (matchingReleases.Length != 1)
        {
            Add(
                issues,
                "effect.resourceTriggerExecutions",
                "effect_resource_trigger_replacement_authority_invalid",
                "one exact accepted apply-definition release whose frozen target produced this effect's replace transition",
                execution.EffectId + "/" + replacementEventRef);
            return true;
        }

        if (!preReactionEffects.TryGetValue(
                execution.EffectId,
                out var preReactionEffect))
        {
            Add(
                issues,
                "effect.resourceTriggerExecutions",
                "effect_activation_replay_budget_mismatch",
                "the exact accepted uses-before stamp from immutable pre-reaction authority",
                execution.EffectId + "/" +
                execution.RemainingUseBudget?.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) +
                "/missing");
            return true;
        }
        var lifetime = preReactionEffect["lifetime"] as JsonObject;
        if (lifetime == null ||
            !string.Equals(
                lifetime["mode"]?.GetValue<string>(),
                "uses",
                StringComparison.Ordinal) ||
            !TryReadPositiveInt(
                lifetime["remainingUses"],
                out var currentRemainingUses) ||
            currentRemainingUses != execution.RemainingUseBudget)
        {
            Add(
                issues,
                "effect.resourceTriggerExecutions",
                "effect_activation_replay_budget_mismatch",
                "the exact accepted uses-before stamp from immutable pre-reaction authority",
                execution.EffectId + "/" +
                execution.RemainingUseBudget?.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) + "/" +
                Describe(lifetime?["remainingUses"]));
            return true;
        }

        var reduction = EffectLifecycleScheduler.AdvanceLifetime(
            new EffectLifetimeReductionInput(
                preReactionEffect,
                new EffectLifecycleEvent(
                    execution.EventRef,
                    turn,
                    execution.EventKind,
                    execution.TriggerId),
                processedEventRefs));
        issues.AddRange(reduction.Issues);
        if (!reduction.Success)
            return true;
        if (reduction.Outcome is not ("advance" or "expire"))
        {
            Add(
                issues,
                "effect.resourceTriggerExecutions",
                "effect_resource_trigger_consumption_invalid",
                "advance or expire for one accepted consuming trigger",
                execution.EffectId + "/" + reduction.Outcome);
            return true;
        }

        var transitionId = identityFactory.CreateTransitionId();
        transitionIds.Add(transitionId);
        identityRoot.InsertBeforeTransition(
            execution.EffectId,
            transitions.Count - 1,
            replacementTransition,
            CreateTransition(
                transitionId,
                "consume",
                turn,
                execution.EventRef,
                new[] { execution.EffectId },
                new[] { execution.EffectId }));
        processedEventRefs.Add(execution.EventRef);
        if (reduction.UpdatedEffect is JsonObject updatedEffect)
        {
            preReactionEffects[execution.EffectId] =
                updatedEffect.DeepClone().AsObject();
        }
        else
        {
            preReactionEffects.Remove(execution.EffectId);
        }
        return true;
    }

    private static void ValidateReleasedReplacementAuthority(
        IReadOnlyList<ReleasedEffectReaction> releasedReactions,
        IReadOnlyDictionary<string, ReactionApplicationPlan> applicationPlans,
        IReadOnlyDictionary<string, ReactionApplicationResult> applicationResults,
        EffectIdentityHistoryOwner identityRoot,
        List<ValidationIssue> issues)
    {
        var identityEntries = new Dictionary<string, JsonObject>(
            StringComparer.Ordinal);
        foreach (var entry in identityRoot.ReadEntries())
        {
            if (TryReadExact(entry["effectId"], out var effectId))
                identityEntries.TryAdd(effectId, entry);
        }
        foreach (var released in releasedReactions
                     .OrderBy(static value => value.MechanicsOrdinal))
        {
            var reaction = released.Reaction;
            if (!applicationPlans.TryGetValue(
                    reaction.EventRef,
                    out var applicationPlan) ||
                !applicationPlan.IsReplacement)
            {
                continue;
            }
            if (!applicationResults.TryGetValue(
                    reaction.EventRef,
                    out var result))
            {
                Add(
                    issues,
                    "effect.reactions",
                    "effect_reaction_replacement_authority_invalid",
                    "one exact runtime result for every released replacement",
                    reaction.EventRef);
                continue;
            }
            EffectReplayIdentity? expectedTarget;
            switch (applicationPlan.ReplacementExpectation)
            {
                case ReactionReplacementExpectationKind.FrozenAbsent:
                    expectedTarget = null;
                    break;
                case ReactionReplacementExpectationKind.FrozenExact:
                    expectedTarget = reaction.ReplacementTarget;
                    break;
                case ReactionReplacementExpectationKind
                        .PriorSelfReplacementResult:
                    expectedTarget =
                        applicationPlan.PriorReactionEventRef != null &&
                        applicationResults.TryGetValue(
                            applicationPlan.PriorReactionEventRef,
                            out var priorResult)
                            ? priorResult.ResultIdentity
                            : null;
                    break;
                default:
                    expectedTarget = null;
                    break;
            }
            var expectsAbsence = applicationPlan.ReplacementExpectation ==
                ReactionReplacementExpectationKind.FrozenAbsent;
            var hasExactExpectedTarget = expectedTarget != null;
            var validExpectation = expectsAbsence != hasExactExpectedTarget;
            var expectedCreateEventRef = expectedTarget == null
                ? reaction.EventRef
                : CreateApplicationTransitionEventRef(
                    reaction.EventRef,
                    "replacement_result_create");
            var validCreate = HasExactReactionReplacementCreate(
                identityEntries,
                result.ResultIdentity,
                expectedCreateEventRef,
                reaction.EffectId);
            var validReplacement = expectsAbsence
                ? result.ReplacedIdentity == null
                : result.ReplacedIdentity == expectedTarget &&
                  HasExactReactionReplacementTransition(
                      identityEntries,
                      expectedTarget!,
                      result.ResultIdentity,
                      reaction.EventRef);
            if (validExpectation && validCreate && validReplacement)
            {
                identityRoot.RetainReplacementAgreement(
                    reaction.EventRef, result.ResultIdentity, expectedTarget, expectedCreateEventRef, reaction.EffectId);
                continue;
            }
            Add(
                issues,
                "effect.reactions",
                "effect_reaction_replacement_authority_invalid",
                "every released replacement retires its runtime-checked typed target exactly once and creates one exact typed result",
                reaction.EventRef);
        }
    }

    private static bool HasExactReactionReplacementCreate(
        IReadOnlyDictionary<string, JsonObject> identityEntries,
        EffectReplayIdentity result,
        string expectedCreateEventRef,
        string expectedProducerEffectId)
    {
        if (!string.Equals(
                result.Authority.BindingKind,
                "accepted_application",
                StringComparison.Ordinal) ||
            !string.Equals(
                result.Authority.AuthorityId,
                expectedCreateEventRef,
                StringComparison.Ordinal))
        {
            return false;
        }
        if (!identityEntries.TryGetValue(result.EffectId, out var entry) ||
            entry["transitions"] is not JsonArray transitions)
        {
            return false;
        }
        return transitions.OfType<JsonObject>().Count(transition =>
            string.Equals(
                transition["kind"]?.GetValue<string>(),
                "create",
                StringComparison.Ordinal) &&
            string.Equals(
                transition["eventRef"]?.GetValue<string>(),
                result.Authority.AuthorityId,
                StringComparison.Ordinal) &&
            transition["sourceEffectIds"] is JsonArray sourceIds &&
            sourceIds.Count == 1 &&
            string.Equals(
                sourceIds[0]?.GetValue<string>(),
                expectedProducerEffectId,
                StringComparison.Ordinal) &&
            transition["resultEffectIds"] is JsonArray resultIds &&
            resultIds.Count == 1 &&
            string.Equals(
                resultIds[0]?.GetValue<string>(),
                result.EffectId,
                StringComparison.Ordinal)) == 1;
    }

    private static bool HasExactReactionReplacementTransition(
        IReadOnlyDictionary<string, JsonObject> identityEntries,
        EffectReplayIdentity target,
        EffectReplayIdentity result,
        string reactionEventRef)
    {
        return identityEntries.TryGetValue(target.EffectId, out var entry) &&
               string.Equals(
                   entry["state"]?.GetValue<string>(),
                   "replaced",
                   StringComparison.Ordinal) &&
               entry["transitions"] is JsonArray { Count: > 0 }
                   transitions &&
               transitions[^1] is JsonObject replacementTransition &&
               string.Equals(
                   replacementTransition["kind"]?.GetValue<string>(),
                   "replace",
                   StringComparison.Ordinal) &&
               string.Equals(
                   replacementTransition["eventRef"]?.GetValue<string>(),
                   reactionEventRef,
                   StringComparison.Ordinal) &&
               replacementTransition["sourceEffectIds"] is JsonArray
                   sourceIds &&
               sourceIds.Count == 1 &&
               string.Equals(
                   sourceIds[0]?.GetValue<string>(),
                   target.EffectId,
                   StringComparison.Ordinal) &&
               replacementTransition["resultEffectIds"] is JsonArray
                   resultIds &&
               resultIds.Count == 1 &&
               string.Equals(
                   resultIds[0]?.GetValue<string>(),
                   result.EffectId,
                   StringComparison.Ordinal);
    }

    private static void ApplyNonConsumingTriggerEvidence(
        EffectResourceTriggerExecution execution,
        int turn,
        CarrierWorkspace workspace,
        EffectIdentityHistoryOwner identityRoot,
        EffectIdentityFactory identityFactory,
        List<string> transitionIds,
        List<JsonObject> activeEffects,
        HashSet<string> processedEventRefs,
        List<ValidationIssue> issues)
    {
        if (processedEventRefs.Contains(execution.EventRef))
            return;

        var catalog = EffectCarrierCatalog.Build(workspace.ToInput());
        issues.AddRange(catalog.Issues);
        if (!catalog.TryResolveOne(execution.EffectId, out var occurrence))
        {
            Add(
                issues,
                "effect.resourceTriggerExecutions",
                "effect_resource_trigger_identity_target_unresolved",
                "one exact active effect for an accepted non-consuming trigger",
                execution.EffectId);
            return;
        }

        var transitionId = identityFactory.CreateTransitionId();
        var triggered = occurrence.Effect.DeepClone().AsObject();
        UpdateEffectChronology(triggered, transitionId, turn);
        if (!workspace.TryReplaceEffect(execution.EffectId, triggered))
        {
            Add(
                issues,
                "effect.resourceTriggerExecutions",
                "effect_resource_trigger_identity_target_unresolved",
                "one exact mutable active effect for trigger evidence",
                execution.EffectId);
            return;
        }

        transitionIds.Add(transitionId);
        AppendIdentityTransition(
            identityRoot,
            execution.EffectId,
            triggered["state"]!.GetValue<string>(),
            CreateTransition(
                transitionId,
                "trigger",
                turn,
                execution.EventRef,
                new[] { execution.EffectId },
                new[] { execution.EffectId }),
            issues);
        AddOrReplaceAffected(activeEffects, triggered);
        processedEventRefs.Add(execution.EventRef);
    }

    private static bool IsSourceBindingSatisfied(
        JsonObject effect,
        EffectSourceAuthority sourceAuthority)
    {
        if (effect["source"] is not JsonObject source ||
            effect["target"] is not JsonObject target ||
            effect["lifetime"] is not JsonObject lifetime ||
            !TryReadExact(effect["realm"], out var realm) ||
            !TryReadExact(source["kind"], out var sourceKind) ||
            !TryReadExact(source["sourceId"], out var sourceId) ||
            !TryReadExact(source["definitionKey"], out var definitionKey) ||
            !TryReadExact(target["kind"], out var targetKind) ||
            !TryReadExact(lifetime["activePredicate"], out var predicate))
        {
            return false;
        }
        var resolution = sourceAuthority.ResolveCanonicalBinding(
            new EffectSourceKey(realm, sourceKind, sourceId, definitionKey),
            targetKind);
        return resolution.Source is { Active: true } binding &&
            (string.Equals(predicate, "active", StringComparison.Ordinal) ||
             binding.SatisfiedPredicates.Contains(predicate));
    }

    private static bool TryParseLifecycleAuthority(
        JsonNode? node,
        List<ValidationIssue> issues,
        out LifecycleAuthority authority)
    {
        authority = null!;
        if (node is not JsonObject value ||
            !TryReadExact(value["eventRef"], out var eventRef) ||
            !TryReadPositiveInt(value["turn"], out var turn) ||
            !TryReadExact(value["phase"], out var phase) ||
            !TryReadExact(value["realm"], out var realm) ||
            value["target"] is not JsonObject target ||
            !TryReadExact(target["kind"], out var targetKind) ||
            !TryReadExact(target["targetId"], out var targetId))
        {
            Add(
                issues,
                "eventInput.lifecycleEvents",
                "effect_lifecycle_event_invalid",
                "closed client-derived lifecycle event authority",
                Describe(node));
            return false;
        }
        authority = new LifecycleAuthority(
            eventRef,
            turn,
            phase,
            new EffectTargetKey(realm, targetKind, targetId),
            ReadOptionalExact(value["effectId"]),
            ReadOptionalExact(value["triggerId"]),
            TryReadNonNegativeLong(value["currentTime"], out var currentTime)
                ? currentTime
                : null,
            ReadOptionalExact(value["currentSceneId"]),
            value["sceneClosed"]?.GetValue<bool>() == true,
            value["sourceSatisfied"]?.GetValue<bool>(),
            value["conditionSatisfied"]?.GetValue<bool>(),
            ReadOptionalExact(value["currentRealm"]),
            ReadOptionalExact(value["causalEventRef"]),
            value["targetSatisfied"]?.GetValue<bool>());
        return true;
    }

    private static bool WasCreatedByCausalEvent(
        JsonObject effect,
        string? causalEventRef) =>
        causalEventRef != null &&
        effect["chronology"] is JsonObject chronology &&
        (TryReadExact(chronology["causalEventRef"], out var createdEventRef) ||
         TryReadExact(chronology["createdEventRef"], out createdEventRef)) &&
        string.Equals(
            createdEventRef,
            causalEventRef,
            StringComparison.Ordinal);

    private static void AppendIdentityTransition(
        EffectIdentityHistoryOwner identityRoot,
        string effectId,
        string state,
        JsonObject transition,
        List<ValidationIssue> issues)
    {
        if (!identityRoot.TryAppendTransition(effectId, state, transition))
        {
            Add(
                issues,
                EffectAcceptedTurnPlan.IdentityIndexPath,
                "effect_lifecycle_identity_unresolved",
                "one exact identity entry with transition history",
                effectId);
            return;
        }
    }

    private static JsonObject CreateTransition(
        string transitionId,
        string kind,
        int turn,
        string eventRef,
        IReadOnlyList<string> sourceEffectIds,
        IReadOnlyList<string> resultEffectIds) =>
        new()
        {
            ["transitionId"] = transitionId,
            ["kind"] = kind,
            ["turn"] = turn,
            ["eventRef"] = eventRef,
            ["sourceEffectIds"] = new JsonArray(sourceEffectIds
                .Select(static value => (JsonNode)value)
                .ToArray()),
            ["resultEffectIds"] = new JsonArray(resultEffectIds
                .Select(static value => (JsonNode)value)
                .ToArray()),
            ["receiptId"] = null
        };

    private static void UpdateEffectChronology(
        JsonObject effect,
        string transitionId,
        int turn)
    {
        var chronology = effect["chronology"]!.AsObject();
        chronology["lastTransitionId"] = transitionId;
        chronology["lastTransitionTurn"] = turn;
    }

    private static void AddOrReplaceAffected(
        List<JsonObject> activeEffects,
        JsonObject effect)
    {
        var effectId = effect["effectId"]!.GetValue<string>();
        RemoveAffected(activeEffects, effectId);
        activeEffects.Add(effect.DeepClone().AsObject());
    }

    private static void RemoveAffected(
        List<JsonObject> activeEffects,
        string effectId) =>
        activeEffects.RemoveAll(effect => string.Equals(
            effect["effectId"]?.GetValue<string>(),
            effectId,
            StringComparison.Ordinal));

    private static JsonObject CreateEffect(
        PreparedApplication candidate,
        string realm,
        string effectId,
        string transitionId,
        int turn,
        string eventRef,
        JsonArray components,
        JsonObject stacking)
    {
        var definition = candidate.Application.Source.Definition;
        return new JsonObject
        {
            ["schemaVersion"] = EffectMaterializationContract.SchemaVersion,
            ["entityKind"] = "active_effect",
            ["effectId"] = effectId,
            ["state"] = "active",
            ["realm"] = realm,
            ["target"] = new JsonObject
            {
                ["kind"] = candidate.Application.Target.Kind,
                ["targetId"] = candidate.Application.Target.TargetId
            },
            ["display"] = definition["display"]!.DeepClone(),
            ["source"] = new JsonObject
            {
                ["kind"] = candidate.Application.Source.Key.Kind,
                ["sourceId"] = candidate.Application.Source.Key.SourceId,
                ["definitionKey"] = candidate.Application.Source.Key.DefinitionKey
            },
            ["components"] = components.DeepClone(),
            ["lifetime"] = candidate.Lifetime.DeepClone(),
            ["stacking"] = stacking.DeepClone(),
            ["triggers"] = definition["triggers"]!.DeepClone(),
            ["removal"] = definition["removal"]!.DeepClone(),
            ["links"] = definition["links"]!.DeepClone(),
            ["chronology"] = new JsonObject
            {
                ["createdAtTurn"] = turn,
                ["createdEventRef"] = eventRef,
                ["causalEventRef"] = candidate.Application.CausalEventRef,
                ["lastTransitionId"] = transitionId,
                ["lastTransitionTurn"] = turn
            }
        };
    }

    private static JsonObject CreateIdentityEntry(
        JsonObject effect,
        CarrierSlot slot,
        string effectId,
        string transitionId,
        int turn,
        string eventRef,
        ApplicationProvenance provenance)
    {
        var target = effect["target"]!.DeepClone().AsObject();
        var source = effect["source"]!.DeepClone().AsObject();
        return new JsonObject
        {
            ["effectId"] = effectId,
            ["state"] = "active",
            ["realm"] = effect["realm"]!.GetValue<string>(),
            ["owner"] = new JsonObject
            {
                ["kind"] = target["kind"]!.GetValue<string>(),
                ["ownerId"] = target["targetId"]!.GetValue<string>(),
                ["carrierPath"] = slot.Path,
                ["collection"] = slot.CollectionName
            },
            ["target"] = target,
            ["source"] = source.DeepClone(),
            ["stackCoordinate"] = new JsonObject
            {
                ["realm"] = effect["realm"]!.GetValue<string>(),
                ["targetKind"] = target["kind"]!.GetValue<string>(),
                ["targetId"] = target["targetId"]!.GetValue<string>(),
                ["sourceKind"] = source["kind"]!.GetValue<string>(),
                ["sourceId"] = source["sourceId"]!.GetValue<string>(),
                ["stackKey"] = effect["stacking"]!["stackKey"]!.GetValue<string>()
            },
            ["createdAtTurn"] = turn,
            ["transitions"] = new JsonArray(new JsonObject
            {
                ["transitionId"] = transitionId,
                ["kind"] = "create",
                ["turn"] = turn,
                ["eventRef"] = eventRef,
                ["sourceEffectIds"] = provenance switch
                {
                    ApplicationProvenance.DirectProvenance => new JsonArray(),
                    ApplicationProvenance.Reaction reaction =>
                        new JsonArray(reaction.ParentEffectId),
                    ApplicationProvenance.SeverityGeneration generation =>
                        new JsonArray(generation.ParentEffectId),
                    _ => throw new InvalidOperationException(
                        "Unknown closed application provenance.")
                },
                ["resultEffectIds"] = new JsonArray(effectId),
                ["receiptId"] = null
            })
        };
    }

    private static bool TryBuildLifetime(
        JsonObject definition,
        EffectSourceKey source,
        JsonObject eventInput,
        List<ValidationIssue> issues,
        out JsonObject lifetime)
    {
        lifetime = new JsonObject();
        if (definition["lifetime"] is not JsonObject policy || !TryReadExact(policy["mode"], out var mode))
        {
            Add(issues, "source.lifetime", "effect_plan_lifetime_invalid", "complete source-owned lifetime policy", Describe(definition["lifetime"]));
            return false;
        }
        lifetime["mode"] = mode;
        switch (mode)
        {
            case "turns":
                lifetime["remainingTurns"] = policy["initialTurns"]!.DeepClone();
                lifetime["advancePhase"] = policy["advancePhase"]!.DeepClone();
                return true;
            case "uses":
                lifetime["remainingUses"] = policy["initialUses"]!.DeepClone();
                var consumingTypes = policy["consumingEventTypes"]!.AsArray()
                    .Select(static item => item!.GetValue<string>())
                    .ToHashSet(StringComparer.Ordinal);
                lifetime["consumingTriggerIds"] = new JsonArray(definition["triggers"]!.AsArray()
                    .OfType<JsonObject>()
                    .Where(trigger =>
                        trigger["consumeUses"]?.GetValue<bool>() == true &&
                        consumingTypes.Contains(trigger["eventType"]!.GetValue<string>()))
                    .Select(trigger => (JsonNode)trigger["triggerId"]!.GetValue<string>())
                    .ToArray());
                return true;
            case "until_time":
                if (!TryReadPositiveInt(policy["duration"], out var duration) ||
                    !TryReadExact(policy["timeAuthority"], out var timeAuthority) ||
                    !string.Equals(
                        timeAuthority,
                        EffectSourceDefinitionContract.CanonicalWorldTimeAuthority,
                        StringComparison.Ordinal) ||
                    !TryReadNonNegativeLong(eventInput["currentTime"], out var currentTime) ||
                    !TryReadExact(eventInput["timeAuthority"], out var acceptedTimeAuthority) ||
                    !string.Equals(timeAuthority, acceptedTimeAuthority, StringComparison.Ordinal))
                {
                    Add(
                        issues,
                        "eventInput.currentTime",
                        "effect_plan_lifetime_context_missing",
                        "client-derived non-negative canonical world time matching the source timeAuthority",
                        Describe(eventInput["currentTime"]));
                    return false;
                }
                try
                {
                    lifetime["deadline"] = checked(currentTime + duration);
                }
                catch (OverflowException)
                {
                    Add(
                        issues,
                        "source.lifetime.duration",
                        "effect_plan_lifetime_overflow",
                        "canonical deadline within Int64 range",
                        $"currentTime={currentTime}; duration={duration}");
                    return false;
                }
                return true;
            case "scene":
                if (!TryReadExact(eventInput["sceneId"], out var sceneId))
                {
                    Add(issues, "eventInput.sceneId", "effect_plan_lifetime_context_missing", "exact accepted sceneId", Describe(eventInput["sceneId"]));
                    return false;
                }
                lifetime["sceneId"] = sceneId;
                lifetime["onSceneExit"] = policy["onSceneExit"]!.DeepClone();
                return true;
            case "source_bound":
                lifetime["linkKind"] = EffectSourceAuthority.CanonicalLinkKind(source.Kind);
                lifetime["targetId"] = source.SourceId;
                lifetime["activePredicate"] = policy["activePredicate"]!.DeepClone();
                lifetime["onSourceLoss"] = policy["onSourceLoss"]!.DeepClone();
                return true;
            case "condition_bound":
                if (eventInput["conditionOperands"] is not JsonObject operands)
                {
                    Add(issues, "eventInput.conditionOperands", "effect_plan_lifetime_context_missing", "validated condition operands", Describe(eventInput["conditionOperands"]));
                    return false;
                }
                lifetime["conditionKey"] = policy["conditionKey"]!.DeepClone();
                lifetime["operands"] = operands.DeepClone();
                lifetime["onConditionLoss"] = policy["onConditionLoss"]!.DeepClone();
                return true;
            case "permanent":
                return true;
            case "manual":
                lifetime["authorities"] = policy["authorities"]!.DeepClone();
                return true;
            default:
                Add(issues, "source.lifetime.mode", "effect_plan_lifetime_invalid", "registered lifetime mode", mode);
                return false;
        }
    }

    private static void BindParameters(JsonArray components, JsonObject? parameters)
    {
        if (parameters == null)
            return;
        foreach (var component in components.OfType<JsonObject>())
        {
            if (component["payload"] is not JsonObject payload)
                continue;
            foreach (var parameter in parameters)
            {
                if (payload.ContainsKey(parameter.Key))
                    payload[parameter.Key] = parameter.Value?.DeepClone();
            }
        }
    }

    private static void ValidateAfterImages(
        CarrierWorkspace workspace,
        EffectIdentityHistoryOwner identityRoot,
        IReadOnlyList<JsonObject> activeEffects,
        List<ValidationIssue> issues)
    {
        foreach (var effect in activeEffects)
        {
            using var document = JsonDocument.Parse(effect.ToJsonString());
            var isSpiritualCondition = effect["target"] is JsonObject target &&
                string.Equals(
                    target["kind"]?.GetValue<string>(),
                    "spiritual_conflict_side",
                    StringComparison.Ordinal);
            issues.AddRange(isSpiritualCondition
                ? EffectMaterializationContract.ValidateAfterlifeCombatCondition(
                    document.RootElement,
                    "plannedActiveEffect")
                : EffectMaterializationContract.Validate(
                    document.RootElement,
                    "plannedActiveEffect",
                    EffectMaterializationPhase.CanonicalActive));
        }
        issues.AddRange(EffectCarrierCatalog.Build(workspace.ToInput()).Issues);
        issues.AddRange(ParseIdentity(identityRoot.ReadSnapshot()).Issues);
    }

    private static void ValidateRoot(JsonObject root, List<ValidationIssue> issues)
    {
        foreach (var property in root)
        {
            if (ClientOwnedOrLegacyFields.Contains(property.Key))
                Add(issues, property.Key, "effect_plan_client_field_forbidden", "client-owned/post-state field absent", property.Key);
            else if (!RootFields.Contains(property.Key))
                Add(issues, property.Key, "effect_plan_unknown_field", "effectChanges, effectResolutionReceipts, or effectEventReports", property.Key);
        }
        if (root.ContainsKey("effectChanges") && root["effectChanges"] is not JsonArray)
            Add(issues, "effectChanges", "effect_plan_input_invalid", "effectChanges array", Describe(root["effectChanges"]));
        if (root.ContainsKey("effectResolutionReceipts") && root["effectResolutionReceipts"] is not JsonArray)
            Add(issues, "effectResolutionReceipts", "effect_plan_input_invalid", "effectResolutionReceipts array", Describe(root["effectResolutionReceipts"]));
        if (root.ContainsKey(EffectAcceptedEventReportCatalog.ResponseField) &&
            root[EffectAcceptedEventReportCatalog.ResponseField] is not JsonArray)
            Add(issues, EffectAcceptedEventReportCatalog.ResponseField, "effect_plan_input_invalid", "effectEventReports array", Describe(root[EffectAcceptedEventReportCatalog.ResponseField]));
    }

    private static bool TryResolveEventRef(
        JsonNode? node,
        EventAuthority? expectedEvent,
        string path,
        List<ValidationIssue> issues,
        out string acceptedEventRef)
    {
        acceptedEventRef = string.Empty;
        if (node is not JsonObject eventRef || !HasOnly(eventRef, "kind", "authorityId") ||
            !TryReadExact(eventRef["kind"], out var kind) ||
            !TryReadExact(eventRef["authorityId"], out var authorityId))
        {
            Add(issues, path, "effect_plan_event_authority_invalid", "closed exact event authority", Describe(node));
            return false;
        }

        if (expectedEvent == null ||
            !string.Equals(kind, expectedEvent.Kind, StringComparison.Ordinal) ||
            !string.Equals(authorityId, expectedEvent.AuthorityId, StringComparison.Ordinal))
        {
            Add(
                issues,
                path,
                "effect_plan_event_authority_mismatch",
                expectedEvent == null
                    ? "accepted event authority at the same effectChanges ordinal"
                    : expectedEvent.Kind + ":" + expectedEvent.AuthorityId,
                kind + ":" + authorityId);
            return false;
        }

        acceptedEventRef = expectedEvent.EventRef;
        return true;
    }

    private static IReadOnlyList<EventAuthority> ParseAcceptedEvents(
        JsonNode? node,
        List<ValidationIssue> issues)
    {
        var result = new List<EventAuthority>();
        var authorityKeys = new HashSet<string>(StringComparer.Ordinal);
        if (node is not JsonArray events || events.Count == 0)
        {
            Add(issues, "eventInput.events", "effect_plan_event_authority_invalid", "non-empty accepted event authority array", Describe(node));
            return result;
        }

        var authorityAliases = new HashSet<string>(StringComparer.Ordinal);
        var eventRefs = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < events.Count; index++)
        {
            var path = $"eventInput.events[{index}]";
            if (events[index] is not JsonObject value ||
                !HasOnly(value, "kind", "authorityId", "eventRef") ||
                !TryReadExact(value["kind"], out var kind) ||
                !TryReadExact(value["authorityId"], out var authorityId) ||
                !TryReadExact(value["eventRef"], out var eventRef))
            {
                Add(issues, path, "effect_plan_event_authority_invalid", "closed exact accepted event authority", Describe(events[index]));
                continue;
            }

            var key = EventAuthorityKey(kind, authorityId);
            var authorityAlias = EventAuthorityKey(
                MortalLocationIdentityState.BuildConfusableKey(kind),
                MortalLocationIdentityState.BuildConfusableKey(authorityId));
            var eventAlias = MortalLocationIdentityState.BuildConfusableKey(eventRef);
            if (!authorityKeys.Add(key) ||
                !authorityAliases.Add(authorityAlias) ||
                !eventRefs.Add(eventAlias))
            {
                Add(issues, path, "effect_plan_event_authority_ambiguous", "globally exact/confusable-unique accepted event authority and eventRef", value.ToJsonString());
                continue;
            }
            result.Add(new EventAuthority(kind, authorityId, eventRef));
        }
        return result;
    }

    private static string EventAuthorityKey(string kind, string authorityId) =>
        kind + "\u001f" + authorityId;

    private static bool HasOnly(JsonObject value, params string[] fields) =>
        value.Count == fields.Length && fields.All(value.ContainsKey);

    private static bool TryReadExact(JsonNode? node, out string value)
    {
        value = node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text) ? text : string.Empty;
        return TryExact(value);
    }

    private static bool TryReadReadable(JsonNode? node, out string value) =>
        TryReadExact(node, out value) && value.Any(char.IsLetterOrDigit);

    private static bool TryReadPositiveInt(JsonNode? node, out int value)
    {
        value = node is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var number) ? number : 0;
        return value > 0;
    }

    private static bool TryReadNonNegativeLong(JsonNode? node, out long value)
    {
        value = node is JsonValue jsonValue && jsonValue.TryGetValue<long>(out var number)
            ? number
            : -1;
        return value >= 0;
    }

    private static string? ReadOptionalExact(JsonNode? node) =>
        TryReadExact(node, out var value) ? value : null;

    private static bool TryExact(string value) =>
        value.Length > 0 && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static JsonObject EmptyIdentityIndex() => new()
    {
        ["schemaVersion"] = EffectIdentityState.SchemaVersion,
        ["entries"] = new JsonArray()
    };

    private static EffectIdentityParseResult ParseIdentity(JsonObject root)
    {
        using var document = JsonDocument.Parse(root.ToJsonString());
        return EffectIdentityState.Parse(document.RootElement, EffectAcceptedTurnPlan.IdentityIndexPath);
    }

    private static bool TryMapEffectTargetKind(
        string targetKind,
        out ResourceOwnerKind ownerKind)
    {
        ownerKind = targetKind switch
        {
            "player" => ResourceOwnerKind.Player,
            "npc" => ResourceOwnerKind.Npc,
            "combatant" => ResourceOwnerKind.Combatant,
            "guardian" or "resident" or "radiant_actor" or "afterlife_actor" =>
                ResourceOwnerKind.AfterlifeActor,
            "spiritual_conflict_side" => ResourceOwnerKind.AfterlifeConflictSide,
            _ => default
        };
        return targetKind is
            "player" or
            "npc" or
            "combatant" or
            "guardian" or
            "resident" or
            "radiant_actor" or
            "afterlife_actor" or
            "spiritual_conflict_side";
    }

    internal static bool TryMapEffectTargetToResourceOwner(
        EffectTargetKey target,
        EffectTargetExport export,
        out ResourceOwnerKey owner)
    {
        if (!TryMapEffectTargetKind(target.Kind, out var ownerKind))
        {
            owner = null!;
            return false;
        }

        var ownerId = target.TargetId;
        if (export.BoundResourceOwnerKind is { } boundOwnerKind)
        {
            ownerKind = boundOwnerKind;
        }
        else if (string.Equals(target.Kind, "player", StringComparison.Ordinal) &&
            !string.Equals(target.Realm, "mortal_world", StringComparison.Ordinal) &&
            string.Equals(target.TargetId, "player_soul", StringComparison.Ordinal))
        {
            ownerKind = ResourceOwnerKind.AfterlifeActor;
        }
        else if (string.Equals(target.Kind, "combatant", StringComparison.Ordinal) &&
            export.BoundNpcId != null)
        {
            ownerKind = ResourceOwnerKind.Npc;
            ownerId = export.BoundNpcId;
        }
        owner = new ResourceOwnerKey(target.Realm, ownerKind, ownerId);
        return true;
    }

    private static ResourceMutationResultConstraint? ResolvePeriodicResultConstraint(
        EffectPeriodicResourceComponent component,
        ResourceDefinition definition,
        List<ValidationIssue> issues)
    {
        if (component.Operation != ResourceOperation.Damage)
            return null;
        if (string.Equals(
                component.BoundPolicy,
                "registered_resource_floor",
                StringComparison.Ordinal))
        {
            return null;
        }
        if (string.Equals(
                component.BoundPolicy,
                "may_reach_zero",
                StringComparison.Ordinal))
        {
            if (definition.MinimumPolicy.Value > 0m)
            {
                Add(
                    issues,
                    $"effect.components[{component.ComponentId}].payload.floorPolicy",
                    "effect_resource_floor_policy_incompatible",
                    "sealed resource minimum at or below zero",
                    definition.MinimumPolicy.Value.ToString());
            }
            return null;
        }
        if (string.Equals(
                component.BoundPolicy,
                "cannot_reduce_below_one",
                StringComparison.Ordinal))
        {
            if (definition.MinimumPolicy.Value >= 1m)
                return null;
            if (!ResourceMaterializationContract.IsQuantumAligned(
                    1m,
                    definition.MinimumPolicy.Value,
                    definition.Quantum))
            {
                Add(
                    issues,
                    $"effect.components[{component.ComponentId}].payload.floorPolicy",
                    "effect_resource_floor_policy_incompatible",
                    "resource definition capable of representing exact current value one",
                    definition.ResourceKey);
                return null;
            }
            return new ResourceMutationResultConstraint(
                RejectBelow: 1m,
                RejectAbove: null);
        }

        Add(
            issues,
            $"effect.components[{component.ComponentId}].payload.floorPolicy",
            "effect_resource_floor_policy_unknown",
            "registered closed periodic-damage floor policy",
            component.BoundPolicy);
        return null;
    }

    private static string CreatePeriodicSourceId(
        string effectId,
        ResourcePendingAuthorityBinding effectAuthority,
        string triggerId,
        string componentId)
    {
        using var builder = new ResourceFingerprintBuilder(
            "effect-resource-source-id-v2");
        builder.Append(CreatePendingEffectReplayIdentity(
            effectId,
            effectAuthority));
        builder.Append(triggerId);
        builder.Append(componentId);
        return "effect_component_" + builder.Build()["sha256:".Length..];
    }

    private static string CreateResourceEventTriggerRef(
        JsonObject effect,
        string triggerId,
        ResourceOperationKey producer,
        string eventKind,
        int turn)
    {
        var effectAuthority = ResolvePendingEffectAuthority(effect, turn);
        using var builder = new ResourceFingerprintBuilder(
            "effect-resource-trigger-event-v1");
        builder.Append(effectAuthority.BindingKind);
        builder.Append(effectAuthority.AuthorityId);
        builder.Append(triggerId);
        builder.Append(eventKind);
        builder.Append(producer.EventRef);
        builder.Append(producer.OriginKind);
        builder.Append(producer.OriginId);
        ResourceStateContract.AppendCoordinate(builder, producer.Coordinate);
        builder.Append((int)producer.Operation);
        return "effect_resource_event_" + triggerId + "_" +
            builder.Build()["sha256:".Length..];
    }

    private static string CreatePeriodicSourceFingerprint(
        string effectId,
        ResourcePendingAuthorityBinding effectAuthority,
        string realm,
        EffectTargetKey target,
        ResourceOwnerKey owner,
        JsonObject trigger,
        EffectPeriodicResourceComponent component,
        ResourceDefinition definition)
    {
        using var builder = new ResourceFingerprintBuilder(
            "effect-resource-source-authority-v2");
        builder.Append(CreatePendingEffectReplayIdentity(
            effectId,
            effectAuthority));
        builder.Append(realm);
        builder.Append(target.Kind);
        builder.Append(target.TargetId);
        builder.Append((int)owner.OwnerKind);
        builder.Append(owner.ResourceOwnerId);
        builder.Append(trigger["triggerId"]!.GetValue<string>());
        builder.Append(trigger["eventType"]!.GetValue<string>());
        builder.Append(trigger["priority"]!.GetValue<int>());
        builder.Append(trigger["consumeUses"]!.GetValue<bool>());
        builder.Append(trigger["resolutionMode"]!.GetValue<string>());
        builder.Append(component.ComponentId);
        builder.Append(component.Profile);
        builder.Append(component.Priority);
        builder.Append(component.ResourceKey);
        builder.Append(component.Amount);
        builder.Append((int)component.Operation);
        builder.Append(component.BoundPolicy);
        builder.Append(definition.DefinitionVersion);
        builder.Append(definition.Materialization.DefinitionId);
        builder.Append(definition.Materialization.Seal);
        return builder.Build();
    }

    private static ResourcePendingAuthorityBinding CreatePendingSourceAuthority(
        JsonObject source,
        EffectSourceRoutingBinding? sourceAuthority)
    {
        if (sourceAuthority is { SameTurn: true, SourceRef: { } sourceRef } &&
            TryExact(sourceRef))
        {
            return new ResourcePendingAuthorityBinding(
                "same_turn_ref",
                sourceRef);
        }
        if (sourceAuthority is { SameTurn: true })
        {
            return new ResourcePendingAuthorityBinding(
                "permanent",
                source["sourceId"]!.GetValue<string>());
        }
        return new ResourcePendingAuthorityBinding(
            "permanent",
            source["sourceId"]!.GetValue<string>());
    }

    internal static ResourcePendingAuthorityBinding ResolvePendingEffectAuthority(
        JsonObject effect,
        int turn)
    {
        if (effect["chronology"] is JsonObject chronology &&
            chronology["createdAtTurn"] is JsonValue createdAtTurnNode &&
            createdAtTurnNode.TryGetValue<int>(out var createdAtTurn) &&
            createdAtTurn == turn &&
            TryReadExact(chronology["createdEventRef"], out var createdEventRef))
        {
            return new ResourcePendingAuthorityBinding(
                "accepted_application",
                createdEventRef);
        }
        return new ResourcePendingAuthorityBinding(
            "permanent",
            TryReadExact(effect["effectId"], out var effectId)
                ? effectId
                : "missing");
    }

    internal static string CreatePendingEffectReplayIdentity(
        string effectId,
        ResourcePendingAuthorityBinding effectAuthority)
    {
        using var builder = new ResourceFingerprintBuilder(
            "effect-pending-replay-identity-v1");
        AppendPendingEffectReplayIdentity(
            builder,
            effectId,
            effectAuthority);
        return "effect_replay_" + builder.Build()["sha256:".Length..];
    }

    internal static string CreateLifecycleResourceTriggerEventRef(
        string acceptedEventRef,
        string effectId,
        ResourcePendingAuthorityBinding effectAuthority,
        string triggerId)
    {
        using var builder = new ResourceFingerprintBuilder(
            "effect-lifecycle-resource-trigger-event-ref-v1");
        builder.Append(acceptedEventRef);
        AppendPendingEffectReplayIdentity(
            builder,
            effectId,
            effectAuthority);
        builder.Append(triggerId);
        return "effect_lifecycle_resource_trigger_" +
            builder.Build()["sha256:".Length..];
    }

    internal static string CreateLifecycleTransitionEventRef(
        string lifecycleEventRef,
        string effectId,
        ResourcePendingAuthorityBinding effectAuthority)
    {
        using var builder = new ResourceFingerprintBuilder(
            "effect-lifecycle-transition-event-ref-v1");
        builder.Append(lifecycleEventRef);
        AppendPendingEffectReplayIdentity(
            builder,
            effectId,
            effectAuthority);
        return "effect_lifecycle_transition_" +
            builder.Build()["sha256:".Length..];
    }

    internal static string CreateApplicationTransitionEventRef(
        string applicationEventRef,
        string transitionRole)
    {
        using var builder = new ResourceFingerprintBuilder(
            "effect-application-transition-event-ref-v1");
        builder.Append(applicationEventRef);
        builder.Append(transitionRole);
        return "effect_application_transition_" +
            builder.Build()["sha256:".Length..];
    }

    internal static void AppendPendingEffectReplayIdentity(
        ResourceFingerprintBuilder builder,
        string effectId,
        ResourcePendingAuthorityBinding effectAuthority)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ValidatePendingEffectAuthority(effectId, effectAuthority);
        builder.Append(effectAuthority.BindingKind);
        builder.Append(effectAuthority.AuthorityId);
        if (!string.Equals(
                effectAuthority.BindingKind,
                "accepted_application",
                StringComparison.Ordinal))
        {
            builder.Append(effectId);
        }
    }

    internal static void ValidatePendingEffectAuthority(
        string effectId,
        ResourcePendingAuthorityBinding effectAuthority)
    {
        if (!ResourceMaterializationContract.IsExactIdentifier(effectId) ||
            !ResourceMaterializationContract.IsExactIdentifier(
                effectAuthority.BindingKind) ||
            !ResourceMaterializationContract.IsExactIdentifier(
                effectAuthority.AuthorityId))
        {
            throw new ArgumentException(
                "Effect authority and effect id must be exact normalized identifiers.",
                nameof(effectAuthority));
        }
        if (string.Equals(
                effectAuthority.BindingKind,
                "accepted_application",
                StringComparison.Ordinal))
        {
            return;
        }
        if (string.Equals(
                effectAuthority.BindingKind,
                "permanent",
                StringComparison.Ordinal) &&
            string.Equals(
                effectAuthority.AuthorityId,
                effectId,
                StringComparison.Ordinal))
        {
            return;
        }
        throw new ArgumentException(
            "Effect authority must be accepted_application, or permanent with an authority id equal to the effect id.",
            nameof(effectAuthority));
    }

    private static EffectSourceRoutingBinding? ResolvePlanSourceBinding(
        EffectAcceptedTurnPlan plan,
        JsonObject effect,
        EffectResourceRoutingWorkMeter workMeter,
        ref int indexLookupCount,
        ref int candidateVisitCount)
    {
        if (effect["source"] is not JsonObject source ||
            !TryReadExact(effect["realm"], out var realm) ||
            !TryReadExact(source["kind"], out var kind) ||
            !TryReadExact(source["sourceId"], out var sourceId) ||
            !TryReadExact(source["definitionKey"], out var definitionKey))
        {
            return null;
        }
        var key = new EffectSourceKey(realm, kind, sourceId, definitionKey);
        indexLookupCount++;
        if (!plan.TryResolveRoutingSourceBinding(key, workMeter, out var binding))
            return null;
        candidateVisitCount++;
        return binding;
    }

    private static ResourcePendingAuthorityBinding CreatePendingTargetAuthority(
        EffectTargetExport target)
    {
        if (target is { SameTurn: true, TargetRef: { } targetRef } &&
            TryExact(targetRef))
        {
            return new ResourcePendingAuthorityBinding(
                "same_turn_ref",
                targetRef);
        }
        return new ResourcePendingAuthorityBinding(
            "permanent",
            target.TargetId);
    }

    private static ResourcePendingAuthorityBinding CreatePendingResourceAuthority(
        ResourceOwnerAuthorityEntry owner)
    {
        if (owner is { SameTurn: true, SameTurnRef: { } sameTurnRef } &&
            TryExact(sameTurnRef))
        {
            return new ResourcePendingAuthorityBinding(
                "same_turn_ref",
                sameTurnRef);
        }
        return new ResourcePendingAuthorityBinding(
            "permanent",
            owner.Key.ResourceOwnerId);
    }

    private static string CreateBoundedCandidateSourceFingerprint(
        ResourcePendingAuthorityBinding effectAuthority,
        ResourcePendingAuthorityBinding sourceAuthority,
        ResourcePendingAuthorityBinding targetAuthority,
        ResourcePendingAuthorityBinding resourceAuthority,
        string realm,
        string targetKind,
        ResourceOwnerKind ownerKind,
        JsonObject trigger,
        EffectPeriodicResourceComponent component,
        ResourceDefinition definition)
    {
        using var builder = new ResourceFingerprintBuilder(
            "effect-bounded-candidate-authority-v1");
        AppendPendingAuthority(builder, effectAuthority);
        AppendPendingAuthority(builder, sourceAuthority);
        AppendPendingAuthority(builder, targetAuthority);
        AppendPendingAuthority(builder, resourceAuthority);
        builder.Append(realm);
        builder.Append(targetKind);
        builder.Append((int)ownerKind);
        builder.Append(trigger["triggerId"]!.GetValue<string>());
        builder.Append(trigger["eventType"]!.GetValue<string>());
        builder.Append(trigger["priority"]!.GetValue<int>());
        builder.Append(trigger["consumeUses"]!.GetValue<bool>());
        builder.Append(trigger["resolutionMode"]!.GetValue<string>());
        builder.Append(component.ComponentId);
        builder.Append(component.Profile);
        builder.Append(component.Priority);
        builder.Append(component.ResourceKey);
        builder.Append(component.Amount);
        builder.Append((int)component.Operation);
        builder.Append(component.BoundPolicy);
        builder.Append(definition.ResourceKey);
        builder.Append(definition.DefinitionVersion);
        return builder.Build();
    }

    private static void AppendPendingAuthority(
        ResourceFingerprintBuilder builder,
        ResourcePendingAuthorityBinding binding)
    {
        builder.Append(binding.BindingKind);
        builder.Append(binding.AuthorityId);
    }

    private static string CreateBoundedResolutionPolicyFingerprint(
        string sourceAuthorityFingerprint,
        EffectPeriodicResourceComponent component,
        ResourceDefinition definition,
        ResourceMutationResultConstraint? constraint)
    {
        using var builder = new ResourceFingerprintBuilder(
            "effect-bounded-resource-policy-v1");
        builder.Append(sourceAuthorityFingerprint);
        builder.Append(component.Amount);
        builder.Append((int)component.Operation);
        builder.Append(component.BoundPolicy);
        builder.Append(definition.ResourceKey);
        builder.Append(definition.DefinitionVersion);
        builder.Append(definition.Quantum);
        builder.Append((int)definition.NumericKind);
        builder.Append((int)definition.FloorPolicy);
        builder.Append((int)definition.CapPolicy);
        builder.Append(constraint?.RejectBelow?.ToString(
            System.Globalization.CultureInfo.InvariantCulture) ?? "<none>");
        builder.Append(constraint?.RejectAbove?.ToString(
            System.Globalization.CultureInfo.InvariantCulture) ?? "<none>");
        return builder.Build();
    }

    private static string ReadSafeSourceLabel(JsonObject effect)
    {
        if (effect["display"] is JsonObject display)
        {
            if (display["sourceLabel"] is JsonValue sourceLabelNode &&
                sourceLabelNode.TryGetValue<string>(out var sourceLabel) &&
                !string.IsNullOrWhiteSpace(sourceLabel))
            {
                return sourceLabel.Trim();
            }
            if (display["name"] is JsonValue nameNode &&
                nameNode.TryGetValue<string>(out var name) &&
                !string.IsNullOrWhiteSpace(name))
            {
                return name.Trim();
            }
        }
        return "активный эффект";
    }

    private static string ReadSafeTargetLabel(string targetKind) =>
        targetKind switch
        {
            "player" => "герой",
            "npc" => "персонаж",
            "combatant" => "участник боя",
            "guardian" => "Хранитель",
            "resident" => "резидент",
            "radiant_actor" => "сияющий персонаж",
            "afterlife_actor" => "обитатель посмертия",
            _ => "цель эффекта"
        };

    private static string ReadSafeOperationLabel(ResourceOperation operation) =>
        operation switch
        {
            ResourceOperation.Damage => "урон",
            ResourceOperation.Restore => "восстановление",
            ResourceOperation.Spend => "расход",
            ResourceOperation.Gain => "получение",
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };

    private static string PrimaryResourceEvent(ResourceOperation operation) =>
        operation switch
        {
            ResourceOperation.Damage => "resource_damaged",
            ResourceOperation.Restore => "resource_restored",
            ResourceOperation.Spend => "resource_spent",
            ResourceOperation.Gain => "resource_gained",
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };

    private static EffectPeriodicResourceResolution FailedPeriodicResourceResolution(
        IEnumerable<ValidationIssue> issues) =>
        new(
            Array.Empty<ResourceMutationSourceExport>(),
            Array.Empty<ResourceMutationIntent>(),
            issues.ToArray());

    private static EffectAcceptedTurnPlanningResult Failed(List<ValidationIssue> issues) =>
        new(null, issues.ToArray());

    private static ValidationIssue Prefix(ValidationIssue issue, string prefix) =>
        new(
            prefix + "." + issue.FilePath,
            issue.Severity,
            issue.Message,
            code: issue.Code,
            section: issue.Section,
            expected: issue.Expected,
            actual: issue.Actual,
            repairHint: issue.RepairHint);

    private static string Describe(JsonNode? node) => node?.ToJsonString() ?? "missing";

    private static HashSet<string> Set(params string[] values) => new(values, StringComparer.Ordinal);

    private static void Add(List<ValidationIssue> issues, string path, string code, string expected, string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Accepted effect command cannot produce one exact complete after-image.",
            code: code,
            section: "effect_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Resubmit one closed source-authorized effect operation and omit all client-owned identity, carrier, stack, lifetime, receipt, and post-state fields."));

    private static void AddWoundBatchIssue(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Typed wound effect authority cannot produce one exact accepted effect after-image.",
            code: code,
            section: "wound_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Recompute the detached prepared wound batch and route only its exact typed root applications through the accepted effect planner."));

    private sealed record Application(
        EffectSourceAuthorityEntry Source,
        EffectTargetKey Target,
        JsonObject? Parameters,
        string EventRef,
        string CausalEventRef,
        string Path,
        string Section,
        bool AcceptedContinuation = false);

    private sealed record WoundApplicationRequest(
        WoundEffectOperationBatch Batch,
        WoundRootEffectApplication Root,
        WoundEffectSourceExport SourceExport,
        WoundEffectSourceDefinition SourceDefinition,
        Application Application,
        string CreatedEventRef);

    private sealed record WoundTerminalRequest(
        WoundEffectOperationBatch Batch,
        WoundMaterializationEnvelope Wound,
        WoundTerminalEffectOperation Operation,
        ImmutableArray<string> SelectedRootEffectIds);

    private sealed record PreparedWoundOperations(
        IReadOnlyList<WoundApplicationRequest> Applications,
        IReadOnlyList<WoundTerminalRequest> Terminations);

    private sealed record ApplicationExecutionFacts(
        string Disposition,
        string EffectId,
        string TransitionId,
        string? CreatedEventRef,
        string CausalEventRef,
        EffectSourceKey Source,
        EffectTargetKey Target,
        EffectCarrierCoordinate CarrierCoordinate,
        JsonObject? CreatedEffect,
        JsonObject? CreatedIdentityEntry,
        ReactionApplicationResult? ReactionResult,
        ApplicationProvenance Provenance);

    private abstract record ApplicationProvenance
    {
        internal static ApplicationProvenance Direct { get; } =
            new DirectProvenance();

        internal sealed record DirectProvenance : ApplicationProvenance;
        internal sealed record Reaction(string ParentEffectId) :
            ApplicationProvenance;
        internal sealed record SeverityGeneration(string ParentEffectId) :
            ApplicationProvenance;
    }

    private enum ReactionReplacementExpectationKind
    {
        None,
        FrozenAbsent,
        FrozenExact,
        PriorSelfReplacementResult
    }

    private sealed record ReactionApplicationPlan(
        EffectSourceAuthorityEntry Source,
        EffectStackCoordinate Coordinate,
        string StackPolicy,
        ReactionReplacementExpectationKind ReplacementExpectation,
        WoundRootOwnershipDomain? WoundOwnershipDomain = null,
        string? PriorReactionEventRef = null)
    {
        internal bool IsReplacement => string.Equals(
            StackPolicy,
            "replace",
            StringComparison.Ordinal);
    }

    private sealed record ReactionStackOccupancy(
        string Policy,
        int Maximum,
        JsonNode? RefreshMode,
        JsonNode? MergeRule);

    private sealed record ReactionReplacementRuntimeExpectation(
        EffectReplayIdentity? ExactTarget,
        bool ExpectsAbsence);

    private sealed record ReactionApplicationResult(
        EffectReplayIdentity ResultIdentity,
        EffectReplayIdentity? ReplacedIdentity);

    private sealed record TerminalOperation(
        string Operation,
        string EffectId,
        EffectTargetKey Target,
        string AuthorityKind,
        string AuthorityId,
        string EventRef);

    private sealed record LifecycleAuthority(
        string EventRef,
        int Turn,
        string Phase,
        EffectTargetKey Target,
        string? EffectId,
        string? TriggerId,
        long? CurrentTime,
        string? CurrentSceneId,
        bool SceneClosed,
        bool? SourceSatisfied,
        bool? ConditionSatisfied,
        string? CurrentRealm,
        string? CausalEventRef,
        bool? TargetSatisfied);

    private sealed record EventAuthority(string Kind, string AuthorityId, string EventRef);

    private sealed record PreparedApplication(
        Application Application,
        CarrierSlot Slot,
        JsonObject Lifetime);

    private sealed record CarrierSlot(
        string Path,
        string CollectionName,
        JsonArray Collection,
        JsonObject Root,
        EffectCarrierCoordinate Coordinate);

    private sealed class CarrierWorkspace
    {
        private JsonObject? _player = null;
        private JsonObject? _npcs = null;
        private readonly JsonObject? _enemies;
        private readonly JsonObject? _allies;
        private readonly JsonObject? _afterlifeProfiles;
        private JsonObject? _spiritualConflict;
        private readonly Dictionary<string, JsonObject> _afterImages = new(StringComparer.Ordinal);

        internal CarrierWorkspace(EffectCarrierCatalogInput input)
        {
            _player = input.PlayerEffects?.DeepClone().AsObject();
            _npcs = input.NpcEffects?.DeepClone().AsObject();
            _enemies = input.EnemyCombatants?.DeepClone().AsObject();
            _allies = input.AllyCombatants?.DeepClone().AsObject();
            _afterlifeProfiles = input.AfterlifeProfiles?.DeepClone().AsObject();
            _spiritualConflict = input.SpiritualConflict?.DeepClone().AsObject();
        }

        internal IReadOnlyDictionary<string, JsonObject> AfterImages => _afterImages;

        internal void Touch(CarrierSlot slot) =>
            _afterImages[slot.Path] = slot.Root;

        internal IReadOnlyList<JsonObject> FindStackEffects(
            string realm,
            EffectTargetKey target,
            EffectSourceKey source,
            string stackKey) =>
            EffectCarrierCatalog.Build(ToInput()).Occurrences
                .Where(occurrence =>
                    string.Equals(
                        occurrence.Effect["realm"]?.GetValue<string>(),
                        realm,
                        StringComparison.Ordinal) &&
                    occurrence.Effect["target"] is JsonObject effectTarget &&
                    string.Equals(
                        effectTarget["kind"]?.GetValue<string>(),
                        target.Kind,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        effectTarget["targetId"]?.GetValue<string>(),
                        target.TargetId,
                        StringComparison.Ordinal) &&
                    occurrence.Effect["source"] is JsonObject effectSource &&
                    string.Equals(
                        effectSource["kind"]?.GetValue<string>(),
                        source.Kind,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        effectSource["sourceId"]?.GetValue<string>(),
                        source.SourceId,
                        StringComparison.Ordinal) &&
                    occurrence.Effect["stacking"] is JsonObject effectStacking &&
                    string.Equals(
                        effectStacking["stackKey"]?.GetValue<string>(),
                        stackKey,
                        StringComparison.Ordinal))
                .OrderBy(static occurrence => occurrence.EffectId, StringComparer.Ordinal)
                .Select(static occurrence => occurrence.Effect.DeepClone().AsObject())
                .ToArray();

        internal bool TryRemoveEffect(string effectId, out JsonObject removed)
        {
            removed = null!;
            var matches = FindMutableOccurrences(effectId);
            if (matches.Count != 1)
                return false;
            var match = matches[0];
            removed = match.Effect.DeepClone().AsObject();
            match.Slot.Collection.RemoveAt(match.Index);
            Touch(match.Slot);
            return true;
        }

        internal bool TryReplaceEffect(string effectId, JsonObject replacement)
        {
            ArgumentNullException.ThrowIfNull(replacement);
            var matches = FindMutableOccurrences(effectId);
            if (matches.Count != 1)
                return false;
            var match = matches[0];
            if (string.Equals(
                    match.Slot.Path,
                    EffectCarrierCatalog.SpiritualConflictPath,
                    StringComparison.Ordinal))
            {
                if (!AfterlifeSpiritualConflictState.TryProjectCombatCondition(
                        replacement,
                        out var projectedReplacement,
                        out _))
                {
                    return false;
                }
                replacement = projectedReplacement;
            }
            match.Slot.Collection[match.Index] = replacement.DeepClone();
            Touch(match.Slot);
            return true;
        }

        internal void IncludeRewrittenCombatantRoots(
            EffectCarrierCatalogInput publicationBaselines)
        {
            if (_enemies != null &&
                !JsonNode.DeepEquals(_enemies, publicationBaselines.EnemyCombatants))
            {
                _afterImages[EffectCarrierCatalog.EnemiesPath] = _enemies;
            }
            if (_allies != null &&
                !JsonNode.DeepEquals(_allies, publicationBaselines.AllyCombatants))
            {
                _afterImages[EffectCarrierCatalog.AlliesPath] = _allies;
            }
        }

        internal void FinalizeAcceptedSpiritualConflict(
            JsonObject? acceptedRoot)
        {
            if (acceptedRoot == null ||
                acceptedRoot["activeConflict"] != null ||
                _spiritualConflict?["activeConflict"] is not JsonObject lifecycleConflict ||
                lifecycleConflict["combatConditions"] is not JsonArray { Count: 0 })
            {
                return;
            }

            _spiritualConflict = acceptedRoot.DeepClone().AsObject();
            _afterImages[EffectCarrierCatalog.SpiritualConflictPath] =
                _spiritualConflict;
        }

        internal bool TryLocate(
            EffectTargetKey target,
            string category,
            List<ValidationIssue> issues,
            out CarrierSlot slot)
        {
            slot = null!;
            switch (target.Kind)
            {
                case "player":
                    if (!string.Equals(
                            target.Realm,
                            "mortal_world",
                            StringComparison.Ordinal))
                    {
                        return TryLocateAfterlifeProfile(target, issues, out slot);
                    }
                    if (!string.Equals(target.TargetId, "player_current", StringComparison.Ordinal))
                    {
                        Add(issues, "target.targetId", "effect_plan_target_carrier_unresolved", "player_current logical player owner", target.TargetId);
                        return false;
                    }
                    _player ??= new JsonObject
                    {
                        ["schemaVersion"] = EffectMaterializationContract.SchemaVersion,
                        ["activeEffects"] = new JsonArray()
                    };
                    if (_player["activeEffects"] is not JsonArray playerEffects)
                        return InvalidCarrier(issues, EffectCarrierCatalog.PlayerPath, _player, out slot);
                    _afterImages[EffectCarrierCatalog.PlayerPath] = _player;
                    slot = new CarrierSlot(
                        EffectCarrierCatalog.PlayerPath,
                        "activeEffects",
                        playerEffects,
                        _player,
                        new EffectCarrierCoordinate(
                            "player",
                            "player_current",
                            EffectCarrierCatalog.PlayerPath,
                            null));
                    return true;
                case "npc":
                    _npcs ??= new JsonObject
                    {
                        ["schemaVersion"] = EffectMaterializationContract.SchemaVersion,
                        ["entries"] = new JsonArray()
                    };
                    if (_npcs["entries"] == null &&
                        _npcs["NPCWoundChanges"] is JsonArray)
                    {
                        _npcs["schemaVersion"] = EffectMaterializationContract.SchemaVersion;
                        _npcs["entries"] = new JsonArray();
                    }
                    if (_npcs["entries"] is not JsonArray entries)
                        return InvalidCarrier(issues, EffectCarrierCatalog.NpcPath, _npcs, out slot);
                    var matches = entries.OfType<JsonObject>()
                        .Where(entry => entry["NPCId"] is JsonValue value &&
                            value.TryGetValue<string>(out var npcId) &&
                            string.Equals(npcId, target.TargetId, StringComparison.Ordinal))
                        .ToArray();
                    if (matches.Length > 1)
                    {
                        Add(issues, EffectCarrierCatalog.NpcPath, "effect_plan_target_carrier_ambiguous", "one exact NPC effect entry", target.TargetId);
                        return false;
                    }
                    var npcEntry = matches.SingleOrDefault();
                    if (npcEntry == null)
                    {
                        npcEntry = new JsonObject
                        {
                            ["NPCId"] = target.TargetId,
                            ["activeEffects"] = new JsonArray()
                        };
                        entries.Add(npcEntry);
                    }
                    if (npcEntry["activeEffects"] is not JsonArray npcEffects)
                        return InvalidCarrier(issues, EffectCarrierCatalog.NpcPath, npcEntry, out slot);
                    _afterImages[EffectCarrierCatalog.NpcPath] = _npcs;
                    slot = new CarrierSlot(
                        EffectCarrierCatalog.NpcPath,
                        "activeEffects",
                        npcEffects,
                        _npcs,
                        new EffectCarrierCoordinate(
                            "npc",
                            target.TargetId,
                            EffectCarrierCatalog.NpcPath,
                            null));
                    return true;
                case "combatant":
                    return TryLocateCombatant(target.TargetId, category, issues, out slot);
                case "guardian":
                case "resident":
                case "radiant_actor":
                case "afterlife_actor":
                    return TryLocateAfterlifeProfile(target, issues, out slot);
                case "spiritual_conflict_side":
                    return TryLocateSpiritualConflict(target, issues, out slot);
                default:
                    Add(issues, "target.kind", "effect_plan_target_carrier_unsupported", "Mortal or persistent afterlife actor carrier", target.Kind);
                    return false;
            }
        }

        internal EffectCarrierCatalogInput ToInput() => new(
            _player,
            _npcs,
            _enemies,
            _allies,
            _afterlifeProfiles,
            _spiritualConflict);

        private bool TryLocateCombatant(
            string targetId,
            string category,
            List<ValidationIssue> issues,
            out CarrierSlot slot)
        {
            slot = null!;
            if (category is not ("buff" or "debuff"))
            {
                Add(
                    issues,
                    "source.display.category",
                    "effect_plan_combat_category_unsupported",
                    "buff or debuff for a category-separated Mortal combatant carrier",
                    category);
                return false;
            }
            var matches = new List<(string Path, JsonObject Root, JsonObject Combatant)>();
            AddCombatantMatches(_enemies, EffectCarrierCatalog.EnemiesPath, "enemiesData", targetId, matches);
            AddCombatantMatches(_allies, EffectCarrierCatalog.AlliesPath, "alliesData", targetId, matches);
            if (matches.Count != 1)
            {
                Add(issues, "target.targetId", matches.Count == 0 ? "effect_plan_target_carrier_unresolved" : "effect_plan_target_carrier_ambiguous", "one exact accepted combatant carrier", targetId);
                return false;
            }
            var match = matches[0];
            var collectionName = string.Equals(category, "buff", StringComparison.Ordinal)
                ? "activeBuffs"
                : "activeDebuffs";
            if (match.Combatant[collectionName] is not JsonArray effects)
            {
                if (TryReadExact(match.Combatant["memberId"], out _) &&
                    !match.Combatant.ContainsKey(collectionName))
                {
                    effects = new JsonArray();
                    match.Combatant[collectionName] = effects;
                }
                else
                {
                    return InvalidCarrier(
                        issues,
                        match.Path,
                        match.Combatant,
                        out slot);
                }
            }
            _afterImages[match.Path] = match.Root;
            slot = new CarrierSlot(
                match.Path,
                collectionName,
                effects,
                match.Root,
                new EffectCarrierCoordinate(
                    "combatant",
                    targetId,
                    match.Path,
                    category));
            return true;
        }

        private bool TryLocateAfterlifeProfile(
            EffectTargetKey target,
            List<ValidationIssue> issues,
            out CarrierSlot slot)
        {
            slot = null!;
            if (_afterlifeProfiles?[AfterlifeEntityProfileState.ProfilesProperty]
                is not JsonArray profiles)
            {
                return InvalidCarrier(
                    issues,
                    EffectCarrierCatalog.AfterlifeProfilesPath,
                    _afterlifeProfiles,
                    out slot);
            }

            var matches = profiles
                .OfType<JsonObject>()
                .Where(profile =>
                    AfterlifeEntityProfileState.TryResolveEffectTarget(
                        profile,
                        out var candidate) &&
                    candidate == target)
                .ToArray();
            if (matches.Length != 1)
            {
                Add(
                    issues,
                    "target.targetId",
                    matches.Length == 0
                        ? "effect_plan_target_carrier_unresolved"
                        : "effect_plan_target_carrier_ambiguous",
                    "one exact accepted afterlife profile carrier in the target realm",
                    $"{target.Realm}/{target.Kind}/{target.TargetId}");
                return false;
            }

            var profile = matches[0];
            if (!profile.ContainsKey("activeEffects"))
                profile["activeEffects"] = new JsonArray();
            if (profile["activeEffects"] is not JsonArray effects)
                return InvalidCarrier(
                    issues,
                    EffectCarrierCatalog.AfterlifeProfilesPath,
                    profile,
                    out slot);

            _afterImages[EffectCarrierCatalog.AfterlifeProfilesPath] =
                _afterlifeProfiles;
            slot = new CarrierSlot(
                EffectCarrierCatalog.AfterlifeProfilesPath,
                "activeEffects",
                effects,
                _afterlifeProfiles,
                new EffectCarrierCoordinate(
                    "afterlife_profile",
                    target.TargetId,
                    EffectCarrierCatalog.AfterlifeProfilesPath,
                    null));
            return true;
        }

        private bool TryLocateSpiritualConflict(
            EffectTargetKey target,
            List<ValidationIssue> issues,
            out CarrierSlot slot)
        {
            slot = null!;
            var side = AfterlifeSpiritualConflictState.CombatConditionTargetSides
                .SingleOrDefault(candidate =>
                    AfterlifeSpiritualConflictState.TryResolveEffectTarget(
                        _spiritualConflict,
                        candidate,
                        out var resolved) &&
                    resolved == target);
            if (side == null ||
                _spiritualConflict?["activeConflict"] is not JsonObject conflict)
            {
                Add(
                    issues,
                    "target.targetId",
                    "effect_plan_target_carrier_unresolved",
                    "one exact active spiritual-conflict side in the target realm",
                    $"{target.Realm}/{target.Kind}/{target.TargetId}");
                return false;
            }

            if (!conflict.ContainsKey("combatConditions"))
                conflict["combatConditions"] = new JsonArray();
            if (conflict["combatConditions"] is not JsonArray conditions)
                return InvalidCarrier(
                    issues,
                    EffectCarrierCatalog.SpiritualConflictPath,
                    conflict,
                    out slot);

            _afterImages[EffectCarrierCatalog.SpiritualConflictPath] =
                _spiritualConflict;
            slot = new CarrierSlot(
                EffectCarrierCatalog.SpiritualConflictPath,
                "combatConditions",
                conditions,
                _spiritualConflict,
                new EffectCarrierCoordinate(
                    "spiritual_conflict",
                    target.TargetId,
                    EffectCarrierCatalog.SpiritualConflictPath,
                    side));
            return true;
        }

        private List<MutableOccurrence> FindMutableOccurrences(string effectId)
        {
            var result = new List<MutableOccurrence>();
            foreach (var slot in EnumerateSlots())
            {
                for (var index = 0; index < slot.Collection.Count; index++)
                {
                    if (slot.Collection[index] is JsonObject effect &&
                        (slot.Coordinate.Kind != "spiritual_conflict" ||
                         EffectMatchesSpiritualSlot(effect, slot.Coordinate)) &&
                        string.Equals(
                            effect["effectId"]?.GetValue<string>(),
                            effectId,
                            StringComparison.Ordinal))
                    {
                        result.Add(new MutableOccurrence(slot, index, effect));
                    }
                }
            }
            return result;
        }

        private IEnumerable<CarrierSlot> EnumerateSlots()
        {
            if (_player?["activeEffects"] is JsonArray playerEffects)
            {
                yield return new CarrierSlot(
                    EffectCarrierCatalog.PlayerPath,
                    "activeEffects",
                    playerEffects,
                    _player,
                    new EffectCarrierCoordinate(
                        "player",
                        "player_current",
                        EffectCarrierCatalog.PlayerPath,
                        null));
            }
            if (_npcs?["entries"] is JsonArray npcEntries)
            {
                foreach (var entry in npcEntries.OfType<JsonObject>())
                {
                    if (TryReadExact(entry["NPCId"], out var npcId) &&
                        entry["activeEffects"] is JsonArray effects)
                    {
                        yield return new CarrierSlot(
                            EffectCarrierCatalog.NpcPath,
                            "activeEffects",
                            effects,
                            _npcs,
                            new EffectCarrierCoordinate(
                                "npc",
                                npcId,
                                EffectCarrierCatalog.NpcPath,
                                null));
                    }
                }
            }
            foreach (var slot in EnumerateCombatantSlots(
                         _enemies,
                         EffectCarrierCatalog.EnemiesPath,
                         "enemiesData"))
            {
                yield return slot;
            }
            foreach (var slot in EnumerateCombatantSlots(
                         _allies,
                         EffectCarrierCatalog.AlliesPath,
                         "alliesData"))
            {
                yield return slot;
            }
            if (_afterlifeProfiles?["profiles"] is JsonArray profiles)
            {
                foreach (var profile in profiles.OfType<JsonObject>())
                {
                    if (AfterlifeEntityProfileState.TryResolveEffectTarget(
                            profile,
                            out var target) &&
                        profile["activeEffects"] is JsonArray effects)
                    {
                        yield return new CarrierSlot(
                            EffectCarrierCatalog.AfterlifeProfilesPath,
                            "activeEffects",
                            effects,
                            _afterlifeProfiles,
                            new EffectCarrierCoordinate(
                                "afterlife_profile",
                                target.TargetId,
                                EffectCarrierCatalog.AfterlifeProfilesPath,
                                null));
                    }
                }
            }
            if (_spiritualConflict?["activeConflict"] is JsonObject conflict &&
                conflict["combatConditions"] is JsonArray conditions)
            {
                foreach (var side in AfterlifeSpiritualConflictState
                             .CombatConditionTargetSides)
                {
                    if (!AfterlifeSpiritualConflictState.TryResolveEffectTarget(
                            _spiritualConflict,
                            side,
                            out var target))
                    {
                        continue;
                    }
                    yield return new CarrierSlot(
                        EffectCarrierCatalog.SpiritualConflictPath,
                        "combatConditions",
                        conditions,
                        _spiritualConflict,
                        new EffectCarrierCoordinate(
                            "spiritual_conflict",
                            target.TargetId,
                            EffectCarrierCatalog.SpiritualConflictPath,
                            side));
                }
            }
        }

        private static IEnumerable<CarrierSlot> EnumerateCombatantSlots(
            JsonObject? root,
            string path,
            string collection)
        {
            if (root?[collection] is not JsonArray combatants)
                yield break;
            foreach (var combatant in combatants.OfType<JsonObject>())
            {
                if (TryResolveCombatantOwnerId(combatant, out var targetId))
                {
                    if (combatant["activeBuffs"] is JsonArray buffs)
                    {
                        yield return new CarrierSlot(
                            path,
                            "activeBuffs",
                            buffs,
                            root,
                            new EffectCarrierCoordinate(
                                "combatant",
                                targetId,
                                path,
                                "buff"));
                    }
                    if (combatant["activeDebuffs"] is JsonArray debuffs)
                    {
                        yield return new CarrierSlot(
                            path,
                            "activeDebuffs",
                            debuffs,
                            root,
                            new EffectCarrierCoordinate(
                                "combatant",
                                targetId,
                                path,
                                "debuff"));
                    }
                }
                if (combatant["isGroup"] is not JsonValue groupNode ||
                    !groupNode.TryGetValue<bool>(out var isGroup) ||
                    !isGroup ||
                    combatant["members"] is not JsonArray members)
                {
                    continue;
                }
                foreach (var member in members.OfType<JsonObject>())
                {
                    if (!TryResolveCombatantOwnerId(member, out var memberId))
                        continue;
                    if (member["activeBuffs"] is JsonArray memberBuffs)
                    {
                        yield return new CarrierSlot(
                            path,
                            "activeBuffs",
                            memberBuffs,
                            root,
                            new EffectCarrierCoordinate(
                                "combatant",
                                memberId,
                                path,
                                "buff"));
                    }
                    if (member["activeDebuffs"] is JsonArray memberDebuffs)
                    {
                        yield return new CarrierSlot(
                            path,
                            "activeDebuffs",
                            memberDebuffs,
                            root,
                            new EffectCarrierCoordinate(
                                "combatant",
                                memberId,
                                path,
                                "debuff"));
                    }
                }
            }
        }

        private static bool TryResolveCombatantOwnerId(
            JsonObject owner,
            out string targetId)
        {
            var hasCombatantId = TryReadExact(
                owner["combatantId"],
                out var combatantId);
            var hasMemberId = TryReadExact(owner["memberId"], out var memberId);
            targetId = hasMemberId ? memberId : combatantId;
            return hasCombatantId != hasMemberId;
        }

        private static bool EffectMatchesSpiritualSlot(
            JsonObject effect,
            EffectCarrierCoordinate coordinate) =>
            effect["target"] is JsonObject target &&
            TryReadExact(target["targetId"], out var targetId) &&
            string.Equals(targetId, coordinate.OwnerId, StringComparison.Ordinal);

        private static void AddCombatantMatches(
            JsonObject? root,
            string path,
            string collection,
            string targetId,
            List<(string Path, JsonObject Root, JsonObject Combatant)> matches)
        {
            if (root?[collection] is not JsonArray combatants)
                return;
            foreach (var combatant in combatants.OfType<JsonObject>())
            {
                AddCombatOwnerMatch(
                    combatant,
                    path,
                    root,
                    targetId,
                    matches);
                if (combatant["isGroup"] is not JsonValue groupNode ||
                    !groupNode.TryGetValue<bool>(out var isGroup) ||
                    !isGroup ||
                    combatant["members"] is not JsonArray members)
                {
                    continue;
                }
                foreach (var member in members.OfType<JsonObject>())
                    AddCombatOwnerMatch(member, path, root, targetId, matches);
            }
        }

        private static void AddCombatOwnerMatch(
            JsonObject owner,
            string path,
            JsonObject root,
            string targetId,
            List<(string Path, JsonObject Root, JsonObject Combatant)> matches)
        {
            var combatantIdentity = ReadOptionalExact(owner["combatantId"]);
            var memberIdentity = ReadOptionalExact(owner["memberId"]);
            if ((combatantIdentity == null) == (memberIdentity == null))
                return;
            var identity = memberIdentity ?? combatantIdentity!;
            if (string.Equals(identity, targetId, StringComparison.Ordinal))
                matches.Add((path, root, owner));
        }

        private static bool InvalidCarrier(
            List<ValidationIssue> issues,
            string path,
            JsonObject? actual,
            out CarrierSlot slot)
        {
            slot = null!;
            Add(
                issues,
                path,
                "effect_plan_target_carrier_invalid",
                "current canonical owner carrier",
                actual?.ToJsonString() ?? "missing");
            return false;
        }

        private sealed record MutableOccurrence(
            CarrierSlot Slot,
            int Index,
            JsonObject Effect);
    }
}
