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
    CombatantIdentityState? PreallocatedCombatantIdentities = null);

internal sealed class EffectAcceptedTurnPlan
{
    private readonly JsonObject[] _activeEffects;
    private readonly Dictionary<string, JsonObject> _carrierAfterImages;
    private readonly Dictionary<string, JsonObject?> _carrierBeforeImages;
    private readonly JsonObject _identityIndexAfterImage;
    private readonly JsonObject? _identityIndexBeforeImage;
    private readonly EffectSourceAuthorityEntry[] _sourceBindings;

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
        IReadOnlyList<JsonObject> activeEffects,
        IReadOnlyDictionary<string, JsonObject?> carrierBeforeImages,
        IReadOnlyDictionary<string, JsonObject> carrierAfterImages,
        JsonObject? identityIndexBeforeImage,
        JsonObject identityIndexAfterImage,
        IReadOnlyList<string> touchedPaths,
        IReadOnlyList<string> deletedPaths)
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
            .Select(static entry => entry with
            {
                Definition = entry.Definition.DeepClone().AsObject()
            })
            .ToArray();
        _activeEffects = activeEffects
            .Select(static effect => effect.DeepClone().AsObject())
            .ToArray();
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
            _sourceBindings.Select(static entry => entry with
            {
                Definition = entry.Definition.DeepClone().AsObject()
            }).ToArray());

    internal IReadOnlyList<JsonObject> ActiveEffects =>
        new ReadOnlyCollection<JsonObject>(
            _activeEffects.Select(static effect => effect.DeepClone().AsObject()).ToArray());

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

    private static ReadOnlyCollection<T> ReadOnly<T>(IReadOnlyList<T> values) =>
        new(values.ToArray());
}

internal sealed record EffectAcceptedTurnPlanningResult(
    EffectAcceptedTurnPlan? Plan,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool Success => Plan != null && Issues.Count == 0;
}
