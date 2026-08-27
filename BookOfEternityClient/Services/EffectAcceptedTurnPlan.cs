using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record EffectAcceptedTurnInput(
    string SessionId,
    string SnapshotToken,
    JsonObject RawCommands,
    EffectSourceAuthority SourceAuthority,
    EffectTargetAuthority TargetAuthority,
    JsonObject EventInput,
    string Realm = "mortal_world",
    EffectCarrierCatalogInput? PreTurnCarriers = null,
    JsonObject? PreTurnIdentityIndex = null,
    EffectTargetAuthorityInput? TargetAuthorityInput = null,
    EffectCarrierCatalogInput? PublicationCarrierBaselines = null,
    CombatantIdentityState? PreallocatedCombatantIdentities = null,
    EffectCarrierCatalogInput? AcceptedCarrierBaselines = null);

internal sealed class EffectAcceptedTurnPlan
{
    private readonly JsonObject[] _activeEffects;
    private readonly EffectReactionExecution[] _deferredReactions;
    private readonly Dictionary<
        EffectReactionExpansionKey,
        EffectReactionExpansionUsage> _reactionExpansionUsage;
    private readonly Dictionary<string, JsonObject> _carrierAfterImages;
    private readonly Dictionary<string, JsonObject?> _carrierBeforeImages;
    private readonly JsonObject _identityIndexAfterImage;
    private readonly JsonObject? _identityIndexBeforeImage;
    private readonly EffectSourceAuthorityEntry[] _sourceBindings;
    private readonly Dictionary<EffectSourceKey, EffectSourceAuthorityEntry>
        _sourceBindingsByKey;
    private readonly FrozenDictionary<EffectSourceKey, EffectSourceRoutingBinding>
        _routingSourceBindingsByKey;
    private readonly EffectCarrierCatalogInput _resourceTriggerCarriers;
    private readonly EffectCarrierCatalogInput _acceptedCarrierBaselines;
    private readonly Lazy<EffectAcceptedTurnPlanner.EffectResourceTriggerIndex>
        _resourceTriggerIndex;
    private readonly JsonObject _eventInput;
    private readonly EffectAcceptedTurnPlanner.AcceptedBoundaryCompletionProof?
        _acceptedBoundaryCompletionProof;
    private readonly string? _acceptedBoundaryBasePlanFingerprint;
    private readonly string? _acceptedBoundaryFinalPlanFingerprint;

    internal const string CommandPath = "game_state/effects/effect_commands.json";
    internal const string IdentityIndexPath = "game_state/effects/effect_identity_index.json";

    internal EffectAcceptedTurnPlan(
        string inputFingerprint,
        string carrierAuthorityFingerprint,
        string sourceAuthorityFingerprint,
        string targetAuthorityFingerprint,
        IReadOnlyList<string> allocatedCombatantIds,
        IReadOnlyList<string> allocatedEffectIds,
        IReadOnlyList<string> allocatedTransitionIds,
        IReadOnlyList<EffectSourceKey> sources,
        IReadOnlyList<EffectTargetKey> targets,
        IReadOnlyList<EffectSourceAuthorityEntry> sourceBindings,
        IReadOnlyList<EffectReactionExecution> deferredReactions,
        int reactionExpansionCount,
        IReadOnlyDictionary<
            EffectReactionExpansionKey,
            EffectReactionExpansionUsage> reactionExpansionUsage,
        IReadOnlyList<JsonObject> activeEffects,
        EffectCarrierCatalogInput resourceTriggerCarriers,
        EffectSourceAuthority sourceAuthority,
        EffectTargetAuthority targetAuthority,
        JsonObject eventInput,
        IReadOnlyDictionary<string, JsonObject?> carrierBeforeImages,
        IReadOnlyDictionary<string, JsonObject> carrierAfterImages,
        JsonObject? identityIndexBeforeImage,
        JsonObject identityIndexAfterImage,
        IReadOnlyList<string> touchedPaths,
        IReadOnlyList<string> deletedPaths,
        EffectCarrierCatalogInput? acceptedCarrierBaselines = null,
        EffectAcceptedTurnPlanner.AcceptedBoundaryCompletionProof?
            acceptedBoundaryCompletionProof = null,
        string? acceptedBoundaryBasePlanFingerprint = null)
    {
        InputFingerprint = inputFingerprint;
        CarrierAuthorityFingerprint = carrierAuthorityFingerprint;
        SourceAuthorityFingerprint = sourceAuthorityFingerprint;
        TargetAuthorityFingerprint = targetAuthorityFingerprint;
        AllocatedCombatantIds = ReadOnly(allocatedCombatantIds);
        AllocatedEffectIds = ReadOnly(allocatedEffectIds);
        AllocatedTransitionIds = ReadOnly(allocatedTransitionIds);
        Sources = ReadOnly(sources);
        Targets = ReadOnly(targets);
        _sourceBindings = sourceBindings
            .Select(FreezeSourceBinding)
            .ToArray();
        _sourceBindingsByKey = _sourceBindings.ToDictionary(
            static entry => entry.Key);
        _routingSourceBindingsByKey = _sourceBindings.ToFrozenDictionary(
            static entry => entry.Key,
            static entry => EffectSourceRoutingBinding.FromEntry(entry));
        _deferredReactions = deferredReactions
            .Select(CloneReaction)
            .ToArray();
        if (reactionExpansionCount < 0)
            throw new ArgumentOutOfRangeException(nameof(reactionExpansionCount));
        ReactionExpansionCount = reactionExpansionCount;
        _reactionExpansionUsage = reactionExpansionUsage.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value);
        if (_reactionExpansionUsage.Values.Sum(static usage => usage.Count) !=
            reactionExpansionCount)
        {
            throw new ArgumentException(
                "Reaction expansion usage must account for the whole accepted-turn reaction count.",
                nameof(reactionExpansionUsage));
        }
        _activeEffects = activeEffects
            .Select(static effect => effect.DeepClone().AsObject())
            .ToArray();
        _resourceTriggerCarriers = CloneCarriers(
            resourceTriggerCarriers ?? throw new ArgumentNullException(nameof(resourceTriggerCarriers)));
        _acceptedCarrierBaselines = CloneCarriers(
            acceptedCarrierBaselines ?? CreateCarriers(carrierBeforeImages));
        SourceAuthority = sourceAuthority ?? throw new ArgumentNullException(nameof(sourceAuthority));
        TargetAuthority = targetAuthority ?? throw new ArgumentNullException(nameof(targetAuthority));
        _resourceTriggerIndex = new Lazy<
            EffectAcceptedTurnPlanner.EffectResourceTriggerIndex>(
                () => EffectAcceptedTurnPlanner.CreateResourceTriggerIndex(
                    _resourceTriggerCarriers,
                    TargetAuthority),
                LazyThreadSafetyMode.ExecutionAndPublication);
        _eventInput = (eventInput ?? throw new ArgumentNullException(nameof(eventInput)))
            .DeepClone().AsObject();
        _carrierAfterImages = carrierAfterImages.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.DeepClone().AsObject(),
            StringComparer.Ordinal);
        _carrierBeforeImages = carrierBeforeImages.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value?.DeepClone().AsObject(),
            StringComparer.Ordinal);
        _identityIndexBeforeImage = identityIndexBeforeImage?.DeepClone().AsObject();
        _identityIndexAfterImage = identityIndexAfterImage.DeepClone().AsObject();
        TouchedPaths = ReadOnly(touchedPaths);
        DeletedPaths = ReadOnly(deletedPaths);
        if (acceptedBoundaryCompletionProof is not null &&
            !EffectAcceptedTurnPlanner.IsAcceptedBoundaryCompletionProof(
                acceptedBoundaryCompletionProof))
        {
            throw new ArgumentException(
                "Only the accepted effect boundary completer may seal a final plan.",
                nameof(acceptedBoundaryCompletionProof));
        }
        _acceptedBoundaryCompletionProof = acceptedBoundaryCompletionProof;
        _acceptedBoundaryBasePlanFingerprint = IsAcceptedBoundaryComplete
            ? acceptedBoundaryBasePlanFingerprint ?? throw new ArgumentNullException(
                nameof(acceptedBoundaryBasePlanFingerprint))
            : null;
        _acceptedBoundaryFinalPlanFingerprint = IsAcceptedBoundaryComplete
            ? WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(this)
            : null;
    }

    internal string InputFingerprint { get; }

    internal string CarrierAuthorityFingerprint { get; }

    internal string SourceAuthorityFingerprint { get; }

    internal string TargetAuthorityFingerprint { get; }

    internal IReadOnlyList<string> AllocatedCombatantIds { get; }

    internal IReadOnlyList<string> AllocatedEffectIds { get; }

    internal IReadOnlyList<string> AllocatedTransitionIds { get; }

    internal IReadOnlyList<EffectSourceKey> Sources { get; }

    internal IReadOnlyList<EffectTargetKey> Targets { get; }

    internal IReadOnlyList<EffectSourceAuthorityEntry> SourceBindings =>
        new ReadOnlyCollection<EffectSourceAuthorityEntry>(
            _sourceBindings.Select(CopySourceBinding).ToArray());

    internal bool TryResolveSourceBinding(
        EffectSourceKey key,
        out EffectSourceAuthorityEntry? binding)
    {
        if (!_sourceBindingsByKey.TryGetValue(key, out var stored))
        {
            binding = null;
            return false;
        }

        binding = CopySourceBinding(stored);
        return true;
    }

    internal bool TryResolveRoutingSourceBinding(
        EffectSourceKey key,
        EffectAcceptedTurnPlanner.EffectResourceRoutingWorkMeter workMeter,
        out EffectSourceRoutingBinding binding)
    {
        ArgumentNullException.ThrowIfNull(workMeter);
        if (!_routingSourceBindingsByKey.TryGetValue(key, out binding))
            return false;
        workMeter.RecordSourceBindingBorrow();
        return true;
    }

    internal IReadOnlyList<EffectReactionExecution> DeferredReactions =>
        new ReadOnlyCollection<EffectReactionExecution>(
            _deferredReactions.Select(CloneReaction).ToArray());

    internal IReadOnlyList<EffectReactionExecution> DeferredRoutingReactions =>
        new ReadOnlyCollection<EffectReactionExecution>(
            _deferredReactions.Select(CloneRoutingReaction).ToArray());

    internal int ReactionExpansionCount { get; }

    internal IReadOnlyDictionary<
        EffectReactionExpansionKey,
        EffectReactionExpansionUsage> ReactionExpansionUsage =>
        new ReadOnlyDictionary<
            EffectReactionExpansionKey,
            EffectReactionExpansionUsage>(
                _reactionExpansionUsage.ToDictionary(
                    static pair => pair.Key,
                    static pair => pair.Value));

    internal IReadOnlyList<JsonObject> ActiveEffects =>
        new ReadOnlyCollection<JsonObject>(
            _activeEffects.Select(static effect => effect.DeepClone().AsObject()).ToArray());

    internal EffectCarrierCatalogInput ResourceTriggerCarriers =>
        CloneCarriers(_resourceTriggerCarriers);

    internal EffectCarrierCatalogInput AcceptedCarrierBaselines =>
        CloneCarriers(_acceptedCarrierBaselines);

    internal EffectAcceptedTurnPlanner.EffectResourceTriggerIndex ResourceTriggerIndex =>
        _resourceTriggerIndex.Value;

    internal EffectSourceAuthority SourceAuthority { get; }

    internal EffectTargetAuthority TargetAuthority { get; }

    internal JsonObject EventInput => _eventInput.DeepClone().AsObject();

    internal IReadOnlyDictionary<string, JsonObject> CarrierAfterImages =>
        new ReadOnlyDictionary<string, JsonObject>(
            _carrierAfterImages.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.DeepClone().AsObject(),
                StringComparer.Ordinal));

    internal IReadOnlyDictionary<string, JsonObject?> CarrierBeforeImages =>
        new ReadOnlyDictionary<string, JsonObject?>(
            _carrierBeforeImages.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value?.DeepClone().AsObject(),
                StringComparer.Ordinal));

    internal JsonObject? IdentityIndexBeforeImage =>
        _identityIndexBeforeImage?.DeepClone().AsObject();

    internal JsonObject IdentityIndexAfterImage => _identityIndexAfterImage.DeepClone().AsObject();

    internal IReadOnlyList<string> TouchedPaths { get; }

    internal IReadOnlyList<string> DeletedPaths { get; }

    internal bool IsAcceptedBoundaryComplete =>
        EffectAcceptedTurnPlanner.IsAcceptedBoundaryCompletionProof(
            _acceptedBoundaryCompletionProof);

    internal string? AcceptedBoundaryBasePlanFingerprint =>
        _acceptedBoundaryBasePlanFingerprint;

    internal string? AcceptedBoundaryFinalPlanFingerprint =>
        _acceptedBoundaryFinalPlanFingerprint;

    internal static EffectAcceptedTurnPlan DetachedCopyOf(
        EffectAcceptedTurnPlan source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new EffectAcceptedTurnPlan(
            source.InputFingerprint,
            source.CarrierAuthorityFingerprint,
            source.SourceAuthorityFingerprint,
            source.TargetAuthorityFingerprint,
            source.AllocatedCombatantIds,
            source.AllocatedEffectIds,
            source.AllocatedTransitionIds,
            source.Sources,
            source.Targets,
            source.SourceBindings,
            source.DeferredReactions,
            source.ReactionExpansionCount,
            source.ReactionExpansionUsage,
            source.ActiveEffects,
            source.ResourceTriggerCarriers,
            source.SourceAuthority,
            source.TargetAuthority,
            source.EventInput,
            source.CarrierBeforeImages,
            source.CarrierAfterImages,
            source.IdentityIndexBeforeImage,
            source.IdentityIndexAfterImage,
            source.TouchedPaths,
            source.DeletedPaths,
            source.AcceptedCarrierBaselines,
            source._acceptedBoundaryCompletionProof,
            source._acceptedBoundaryBasePlanFingerprint);
    }

    private static ReadOnlyCollection<T> ReadOnly<T>(IReadOnlyList<T> values) =>
        new(values.ToArray());

    private static EffectCarrierCatalogInput CreateCarriers(
        IReadOnlyDictionary<string, JsonObject?> roots)
    {
        JsonObject? Read(string path) =>
            roots.TryGetValue(path, out var root)
                ? root?.DeepClone().AsObject()
                : null;

        return new EffectCarrierCatalogInput(
            Read(EffectCarrierCatalog.PlayerPath),
            Read(EffectCarrierCatalog.NpcPath),
            Read(EffectCarrierCatalog.EnemiesPath),
            Read(EffectCarrierCatalog.AlliesPath),
            Read(EffectCarrierCatalog.AfterlifeProfilesPath),
            Read(EffectCarrierCatalog.SpiritualConflictPath));
    }

    private static EffectSourceAuthorityEntry FreezeSourceBinding(
        EffectSourceAuthorityEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(entry.Definition);
        ArgumentNullException.ThrowIfNull(entry.SatisfiedPredicates);
        return entry with
        {
            Definition = entry.Definition.DeepClone().AsObject(),
            SatisfiedPredicates = entry.SatisfiedPredicates.ToFrozenSet(
                StringComparer.Ordinal)
        };
    }

    private static EffectSourceAuthorityEntry CopySourceBinding(
        EffectSourceAuthorityEntry entry) =>
        entry with
        {
            Definition = entry.Definition.DeepClone().AsObject(),
            SatisfiedPredicates = new HashSet<string>(
                entry.SatisfiedPredicates,
                StringComparer.Ordinal)
        };

    private static EffectCarrierCatalogInput CloneCarriers(EffectCarrierCatalogInput value) =>
        new(
            value.PlayerEffects?.DeepClone().AsObject(),
            value.NpcEffects?.DeepClone().AsObject(),
            value.EnemyCombatants?.DeepClone().AsObject(),
            value.AllyCombatants?.DeepClone().AsObject(),
            value.AfterlifeProfiles?.DeepClone().AsObject(),
            value.SpiritualConflict?.DeepClone().AsObject());

    private static EffectReactionExecution CloneReaction(
        EffectReactionExecution value) => value with
    {
        DownstreamSource = value.DownstreamSource is null
            ? null
            : value.DownstreamSource with
            {
                Definition = value.DownstreamSource.Definition.DeepClone().AsObject()
        },
        Parameters = value.Parameters?.DeepClone().AsObject()
    };

    private static EffectReactionExecution CloneRoutingReaction(
        EffectReactionExecution value) => value with
    {
        DownstreamSource = null,
        DownstreamSourceKey = value.DownstreamSourceKey ??
            value.DownstreamSource?.Key,
        Parameters = value.Parameters?.DeepClone().AsObject()
    };
}

internal sealed record EffectAcceptedTurnPlanningResult(
    EffectAcceptedTurnPlan? Plan,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool Success => Plan != null && Issues.Count == 0;
}
