using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed class CanonicalBeforeImage
{
    private readonly byte[]? _bytes;

    internal CanonicalBeforeImage(bool existed, byte[]? bytes)
    {
        if (existed == (bytes == null))
        {
            throw new ArgumentException(
                "A present path requires exact bytes and an absent path requires null bytes.",
                nameof(bytes));
        }

        Existed = existed;
        _bytes = bytes?.ToArray();
        Fingerprint = CreateFingerprint(existed, _bytes);
    }

    internal bool Existed { get; }

    internal byte[]? Bytes => _bytes?.ToArray();

    internal string Fingerprint { get; }

    private static string CreateFingerprint(bool existed, byte[]? bytes)
    {
        var prefix = Encoding.UTF8.GetBytes(existed ? "present\0" : "missing\0");
        var payload = new byte[prefix.Length + (bytes?.Length ?? 0)];
        prefix.CopyTo(payload, 0);
        bytes?.CopyTo(payload, prefix.Length);
        return "sha256:" + Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
    }
}

internal sealed record AcceptedMechanicsAuthorityFingerprints(
    string Definitions,
    string Owners,
    string ResourceState,
    string ResourceHistory,
    string EffectSources,
    string EffectTargets,
    string EffectCarriers,
    string EffectIdentityIndex,
    string AcceptedEvents,
    string Commands,
    string Pending,
    string InternalInputs)
{
    internal IEnumerable<KeyValuePair<string, string>> Enumerate()
    {
        yield return new("definitions", Definitions);
        yield return new("owners", Owners);
        yield return new("resourceState", ResourceState);
        yield return new("resourceHistory", ResourceHistory);
        yield return new("effectSources", EffectSources);
        yield return new("effectTargets", EffectTargets);
        yield return new("effectCarriers", EffectCarriers);
        yield return new("effectIdentityIndex", EffectIdentityIndex);
        yield return new("acceptedEvents", AcceptedEvents);
        yield return new("commands", Commands);
        yield return new("pending", Pending);
        yield return new("internalInputs", InternalInputs);
    }

    internal void Validate()
    {
        foreach (var pair in Enumerate())
        {
            if (!ResourceMaterializationContract.IsAuthorityFingerprint(pair.Value))
            {
                throw new ArgumentException(
                    $"Accepted mechanics authority fingerprint '{pair.Key}' is invalid.",
                    nameof(AcceptedMechanicsAuthorityFingerprints));
            }
        }
    }
}

internal sealed class AcceptedMechanicsPlanBinding
{
    private readonly JsonObject _acceptedEvents;
    private readonly JsonObject _resourceCommands;
    private readonly JsonObject _effectCommands;
    private readonly JsonObject _pendingInput;
    private readonly JsonObject _internalInputs;
    private readonly Dictionary<string, CanonicalBeforeImage> _beforeImages;
    private readonly ReadOnlyDictionary<string, CanonicalBeforeImage> _readOnlyBeforeImages;

    internal AcceptedMechanicsPlanBinding(
        string sessionId,
        string requestId,
        string snapshotToken,
        string realm,
        int turn,
        JsonObject acceptedEvents,
        JsonObject resourceCommands,
        JsonObject effectCommands,
        JsonObject pendingInput,
        JsonObject internalInputs,
        AcceptedMechanicsAuthorityFingerprints authorityFingerprints,
        IReadOnlyDictionary<string, CanonicalBeforeImage> beforeImages)
    {
        SessionId = RequireExact(sessionId, nameof(sessionId));
        RequestId = RequireExact(requestId, nameof(requestId));
        SnapshotToken = RequireExact(snapshotToken, nameof(snapshotToken));
        Realm = realm is "mortal_world" or "chaos_sea" or "shining_abode"
            ? realm
            : throw new ArgumentOutOfRangeException(nameof(realm));
        if (turn <= 0)
            throw new ArgumentOutOfRangeException(nameof(turn));
        Turn = turn;
        _acceptedEvents = Clone(acceptedEvents, nameof(acceptedEvents));
        _resourceCommands = Clone(resourceCommands, nameof(resourceCommands));
        _effectCommands = Clone(effectCommands, nameof(effectCommands));
        _pendingInput = Clone(pendingInput, nameof(pendingInput));
        _internalInputs = Clone(internalInputs, nameof(internalInputs));
        AuthorityFingerprints = authorityFingerprints ??
            throw new ArgumentNullException(nameof(authorityFingerprints));
        AuthorityFingerprints.Validate();
        _beforeImages = CloneBeforeImages(beforeImages);
        _readOnlyBeforeImages = new ReadOnlyDictionary<string, CanonicalBeforeImage>(
            _beforeImages);
    }

    internal string SessionId { get; }

    internal string RequestId { get; }

    internal string SnapshotToken { get; }

    internal string Realm { get; }

    internal int Turn { get; }

    internal JsonObject AcceptedEvents => _acceptedEvents.DeepClone().AsObject();

    internal JsonObject ResourceCommands => _resourceCommands.DeepClone().AsObject();

    internal JsonObject EffectCommands => _effectCommands.DeepClone().AsObject();

    internal JsonObject PendingInput => _pendingInput.DeepClone().AsObject();

    internal JsonObject InternalInputs => _internalInputs.DeepClone().AsObject();

    internal AcceptedMechanicsAuthorityFingerprints AuthorityFingerprints { get; }

    internal IReadOnlyDictionary<string, CanonicalBeforeImage> BeforeImages =>
        CloneReadOnlyBeforeImages(_readOnlyBeforeImages);

    private static string RequireExact(string value, string parameterName) =>
        ResourceMaterializationContract.IsExactIdentifier(value)
            ? value
            : throw new ArgumentException("Expected an exact non-empty identifier.", parameterName);

    private static JsonObject Clone(JsonObject value, string parameterName) =>
        (value ?? throw new ArgumentNullException(parameterName)).DeepClone().AsObject();

    private static Dictionary<string, CanonicalBeforeImage> CloneBeforeImages(
        IReadOnlyDictionary<string, CanonicalBeforeImage> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var clone = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
        foreach (var pair in values)
        {
            ValidatePath(pair.Key, nameof(values));
            ArgumentNullException.ThrowIfNull(pair.Value);
            clone.Add(pair.Key, new CanonicalBeforeImage(pair.Value.Existed, pair.Value.Bytes));
        }
        return clone;
    }

    internal static IReadOnlyDictionary<string, CanonicalBeforeImage> CloneReadOnlyBeforeImages(
        IReadOnlyDictionary<string, CanonicalBeforeImage> values) =>
        new ReadOnlyDictionary<string, CanonicalBeforeImage>(
            values.ToDictionary(
                static pair => pair.Key,
                static pair => new CanonicalBeforeImage(pair.Value.Existed, pair.Value.Bytes),
                StringComparer.Ordinal));

    internal static void ValidatePath(string path, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !string.Equals(path, path.Trim(), StringComparison.Ordinal) ||
            path.StartsWith("/", StringComparison.Ordinal) ||
            Path.IsPathRooted(path) ||
            path.Contains('\\') ||
            path.Split('/').Any(static segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException(
                "Expected a normalized repository-relative canonical path.",
                parameterName);
        }
    }
}

internal sealed class AcceptedMechanicsInput
{
    private readonly AcceptedMechanicsPlanBinding _binding;
    private readonly ValidationIssue[] _validationIssues;

    internal AcceptedMechanicsInput(
        string SessionId,
        string RequestId,
        string SnapshotToken,
        string Realm,
        int Turn,
        JsonObject AcceptedEvents,
        JsonObject ResourceCommands,
        JsonObject EffectCommands,
        JsonObject PendingInput,
        JsonObject InternalInputs,
        AcceptedMechanicsAuthorityFingerprints AuthorityFingerprints,
        IReadOnlyDictionary<string, CanonicalBeforeImage> BeforeImages,
        IReadOnlyList<ValidationIssue> ValidationIssues,
        AcceptedMechanicsPlanningContext? PlanningContext = null)
    {
        _binding = new AcceptedMechanicsPlanBinding(
            SessionId,
            RequestId,
            SnapshotToken,
            Realm,
            Turn,
            AcceptedEvents,
            ResourceCommands,
            EffectCommands,
            PendingInput,
            InternalInputs,
            AuthorityFingerprints,
            BeforeImages);
        ArgumentNullException.ThrowIfNull(ValidationIssues);
        _validationIssues = ValidationIssues.ToArray();
        this.PlanningContext = PlanningContext;
    }

    internal string SessionId => _binding.SessionId;

    internal string RequestId => _binding.RequestId;

    internal string SnapshotToken => _binding.SnapshotToken;

    internal string Realm => _binding.Realm;

    internal int Turn => _binding.Turn;

    internal JsonObject AcceptedEvents => _binding.AcceptedEvents;

    internal JsonObject ResourceCommands => _binding.ResourceCommands;

    internal JsonObject EffectCommands => _binding.EffectCommands;

    internal JsonObject PendingInput => _binding.PendingInput;

    internal JsonObject InternalInputs => _binding.InternalInputs;

    internal AcceptedMechanicsAuthorityFingerprints AuthorityFingerprints =>
        _binding.AuthorityFingerprints;

    internal IReadOnlyDictionary<string, CanonicalBeforeImage> BeforeImages =>
        _binding.BeforeImages;

    internal IReadOnlyList<ValidationIssue> ValidationIssues =>
        Array.AsReadOnly(_validationIssues.ToArray());

    internal AcceptedMechanicsPlanningContext? PlanningContext { get; }

    internal AcceptedMechanicsPlanBinding CreateBinding() => new(
        SessionId,
        RequestId,
        SnapshotToken,
        Realm,
        Turn,
        AcceptedEvents,
        ResourceCommands,
        EffectCommands,
        PendingInput,
        InternalInputs,
        AuthorityFingerprints,
        BeforeImages);
}

internal sealed class AcceptedMechanicsPlanningContext
{
    private readonly JsonObject _definitionRoot;
    private readonly JsonObject _effectIdentityRoot;
    private readonly ResourceCapacityIntent[] _capacityTransitions;
    private readonly ResourceOwnerCapacityDraft[] _ownerCapacityDrafts;
    private readonly ResourceOwnerKey[] _terminalOwners;
    private readonly Dictionary<string, JsonObject> _ownerCompanionAfterImages;
    private readonly AcceptedMechanicsOwnerTransition[] _ownerTransitions;
    private readonly IResourceRegisteredSystemOutcomeDraft[] _registeredSystemOutcomes;

    internal AcceptedMechanicsPlanningContext(
        JsonObject definitionRoot,
        ResourceDefinitionCatalog definitions,
        ResourceStateLedger state,
        ResourceHistoryState history,
        ResourceOwnerAuthority owners,
        ResourceMutationSourceCatalog sources,
        ResourceCommandCompositionResult commands,
        JsonObject effectIdentityRoot,
        EffectAcceptedTurnPlan? effectPlan,
        IReadOnlyList<ResourceCapacityIntent>? capacityTransitions = null,
        IReadOnlyList<ResourceOwnerCapacityDraft>? ownerCapacityDrafts = null,
        IReadOnlyList<ResourceOwnerKey>? terminalOwners = null,
        IReadOnlyDictionary<string, JsonObject>? ownerCompanionAfterImages = null,
        IReadOnlyList<AcceptedMechanicsOwnerTransition>? ownerTransitions = null,
        IReadOnlyList<IResourceRegisteredSystemOutcomeDraft>? registeredSystemOutcomes = null)
    {
        _definitionRoot = (definitionRoot ?? throw new ArgumentNullException(nameof(definitionRoot)))
            .DeepClone().AsObject();
        Definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        State = state ?? throw new ArgumentNullException(nameof(state));
        History = history ?? throw new ArgumentNullException(nameof(history));
        Owners = owners ?? throw new ArgumentNullException(nameof(owners));
        Sources = sources ?? throw new ArgumentNullException(nameof(sources));
        Commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _effectIdentityRoot = (effectIdentityRoot ??
            throw new ArgumentNullException(nameof(effectIdentityRoot))).DeepClone().AsObject();
        EffectPlan = effectPlan;
        _ownerCompanionAfterImages = new Dictionary<string, JsonObject>(
            StringComparer.Ordinal);
        foreach (var pair in ownerCompanionAfterImages ??
                     new Dictionary<string, JsonObject>(StringComparer.Ordinal))
        {
            AcceptedMechanicsPlanBinding.ValidatePath(
                pair.Key,
                nameof(ownerCompanionAfterImages));
            _ownerCompanionAfterImages.Add(
                pair.Key,
                (pair.Value ?? throw new ArgumentNullException(
                    nameof(ownerCompanionAfterImages))).DeepClone().AsObject());
        }
        _ownerTransitions = ownerTransitions?.Select(value =>
            (value ?? throw new ArgumentNullException(nameof(ownerTransitions))).Clone()).ToArray() ??
            Array.Empty<AcceptedMechanicsOwnerTransition>();
        _capacityTransitions = capacityTransitions?.Select(value =>
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(value.Coordinate);
            ArgumentNullException.ThrowIfNull(value.SourceEvidence);
            return value;
        }).ToArray() ?? Array.Empty<ResourceCapacityIntent>();
        _ownerCapacityDrafts = ownerCapacityDrafts?.Select(value =>
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(value.Coordinate);
            ArgumentNullException.ThrowIfNull(value.ResolvedCapacity);
            ArgumentNullException.ThrowIfNull(value.SourceEvidence);
            if (!value.ResolvedCapacity.IsValid ||
                value.ResolvedCapacity.Capacity == null ||
                value.AcceptedMaximum != value.ResolvedCapacity.Capacity.Maximum)
            {
                throw new ArgumentException(
                    "Owner capacity drafts require one exact resolved capacity.",
                    nameof(ownerCapacityDrafts));
            }
            return value;
        }).ToArray() ?? Array.Empty<ResourceOwnerCapacityDraft>();
        _terminalOwners = terminalOwners?.Select(value =>
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!ResourceMaterializationContract.IsExactIdentifier(value.Realm) ||
                !Enum.IsDefined(typeof(ResourceOwnerKind), value.OwnerKind) ||
                !ResourceMaterializationContract.IsExactIdentifier(value.ResourceOwnerId))
            {
                throw new ArgumentException(
                    "Terminal owners require one exact owner key.",
                    nameof(terminalOwners));
            }
            return value;
        }).Distinct().ToArray() ??
            Array.Empty<ResourceOwnerKey>();
        _registeredSystemOutcomes = registeredSystemOutcomes?.Select(value =>
            value ?? throw new ArgumentNullException(nameof(registeredSystemOutcomes))).ToArray() ??
            Array.Empty<IResourceRegisteredSystemOutcomeDraft>();
    }

    internal JsonObject DefinitionRoot => _definitionRoot.DeepClone().AsObject();
    internal ResourceDefinitionCatalog Definitions { get; }
    internal ResourceStateLedger State { get; }
    internal ResourceHistoryState History { get; }
    internal ResourceOwnerAuthority Owners { get; }
    internal ResourceMutationSourceCatalog Sources { get; }
    internal ResourceCommandCompositionResult Commands { get; }
    internal JsonObject EffectIdentityRoot => _effectIdentityRoot.DeepClone().AsObject();
    internal EffectAcceptedTurnPlan? EffectPlan { get; }
    internal IReadOnlyDictionary<string, JsonObject> OwnerCompanionAfterImages =>
        new ReadOnlyDictionary<string, JsonObject>(
            _ownerCompanionAfterImages.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.DeepClone().AsObject(),
                StringComparer.Ordinal));
    internal IReadOnlyList<AcceptedMechanicsOwnerTransition> OwnerTransitions =>
        Array.AsReadOnly(_ownerTransitions.Select(static value => value.Clone()).ToArray());
    internal IReadOnlyList<ResourceCapacityIntent> CapacityTransitions =>
        Array.AsReadOnly(_capacityTransitions.ToArray());
    internal IReadOnlyList<ResourceOwnerCapacityDraft> OwnerCapacityDrafts =>
        Array.AsReadOnly(_ownerCapacityDrafts.ToArray());
    internal IReadOnlyList<ResourceOwnerKey> TerminalOwners =>
        Array.AsReadOnly(_terminalOwners.ToArray());
    internal IReadOnlyList<IResourceRegisteredSystemOutcomeDraft> RegisteredSystemOutcomes =>
        Array.AsReadOnly(_registeredSystemOutcomes.ToArray());
}

internal sealed record ResourceAppliedEvent(
    string EventKind,
    string OperationId,
    string EventRef,
    ResourceCoordinate Coordinate,
    decimal Before,
    decimal After,
    decimal AppliedAmount,
    int Turn,
    int ExecutionSequence,
    string SourceFingerprint);

internal sealed class ResourceProjectionInput
{
    private readonly JsonObject _definitions;
    private readonly JsonObject _state;
    private readonly JsonObject _history;

    internal ResourceProjectionInput(
        JsonObject definitions,
        JsonObject state,
        JsonObject history,
        string ownerAuthorityFingerprint)
    {
        _definitions = (definitions ?? throw new ArgumentNullException(nameof(definitions)))
            .DeepClone().AsObject();
        _state = (state ?? throw new ArgumentNullException(nameof(state)))
            .DeepClone().AsObject();
        _history = (history ?? throw new ArgumentNullException(nameof(history)))
            .DeepClone().AsObject();
        if (!ResourceMaterializationContract.IsAuthorityFingerprint(
                ownerAuthorityFingerprint))
        {
            throw new ArgumentException(
                "Expected a lowercase SHA-256 owner-authority fingerprint.",
                nameof(ownerAuthorityFingerprint));
        }
        OwnerAuthorityFingerprint = ownerAuthorityFingerprint;
    }

    internal JsonObject Definitions => _definitions.DeepClone().AsObject();

    internal JsonObject State => _state.DeepClone().AsObject();

    internal JsonObject History => _history.DeepClone().AsObject();

    internal string OwnerAuthorityFingerprint { get; }
}

internal sealed class AcceptedMechanicsPlan
{
    internal const string DefinitionPath = "game_state/resources/resource_definitions.json";
    internal const string StatePath = "game_state/resources/resource_state.json";
    internal const string HistoryPath = "game_state/resources/resource_history.json";

    private readonly JsonObject _definitionAfterImage;
    private readonly JsonObject _stateAfterImage;
    private readonly JsonObject _historyAfterImage;
    private readonly Dictionary<string, JsonObject> _effectCarrierAfterImages;
    private readonly JsonObject _effectIdentityAfterImage;
    private readonly Dictionary<string, JsonObject?> _pendingAfterImages;
    private readonly Dictionary<string, JsonObject> _ownerCompanionAfterImages;
    private readonly AcceptedMechanicsOwnerTransition[] _ownerTransitions;
    private readonly Dictionary<string, CanonicalBeforeImage> _beforeImages;
    private readonly ResourceAppliedEvent[] _resourceEvents;

    internal AcceptedMechanicsPlan(
        string inputFingerprint,
        JsonObject definitionAfterImage,
        JsonObject stateAfterImage,
        JsonObject historyAfterImage,
        IReadOnlyDictionary<string, JsonObject> effectCarrierAfterImages,
        JsonObject effectIdentityAfterImage,
        IReadOnlyDictionary<string, JsonObject?> pendingAfterImages,
        IReadOnlyDictionary<string, JsonObject> ownerCompanionAfterImages,
        IReadOnlyDictionary<string, CanonicalBeforeImage> beforeImages,
        IReadOnlyList<string> touchedPaths,
        IReadOnlyList<string> consumedPaths,
        AcceptedMechanicsAuthorityFingerprints authorityFingerprints,
        IReadOnlyList<ResourceAppliedEvent> resourceEvents,
        ResourceProjectionInput projectionInput,
        ResourceOwnerAuthority ownerAuthority,
        EffectAcceptedTurnPlan? effectPlan,
        IReadOnlyList<AcceptedMechanicsOwnerTransition>? ownerTransitions = null)
    {
        if (!ResourceMaterializationContract.IsAuthorityFingerprint(inputFingerprint))
            throw new ArgumentException("Expected a lowercase SHA-256 plan fingerprint.", nameof(inputFingerprint));
        InputFingerprint = inputFingerprint;
        _definitionAfterImage = Clone(definitionAfterImage, nameof(definitionAfterImage));
        _stateAfterImage = Clone(stateAfterImage, nameof(stateAfterImage));
        _historyAfterImage = Clone(historyAfterImage, nameof(historyAfterImage));
        _effectCarrierAfterImages = CloneObjects(effectCarrierAfterImages, nameof(effectCarrierAfterImages));
        _effectIdentityAfterImage = Clone(effectIdentityAfterImage, nameof(effectIdentityAfterImage));
        _pendingAfterImages = CloneNullableObjects(pendingAfterImages, nameof(pendingAfterImages));
        _ownerCompanionAfterImages = CloneObjects(ownerCompanionAfterImages, nameof(ownerCompanionAfterImages));
        _ownerTransitions = ownerTransitions?.Select(value =>
            (value ?? throw new ArgumentNullException(nameof(ownerTransitions))).Clone()).ToArray() ??
            Array.Empty<AcceptedMechanicsOwnerTransition>();
        _beforeImages = CloneBeforeImages(beforeImages, nameof(beforeImages));
        AuthorityFingerprints = authorityFingerprints ??
            throw new ArgumentNullException(nameof(authorityFingerprints));
        AuthorityFingerprints.Validate();
        ArgumentNullException.ThrowIfNull(resourceEvents);
        _resourceEvents = resourceEvents
            .OrderBy(static value => value.Turn)
            .ThenBy(static value => value.ExecutionSequence)
            .ThenBy(static value => value.EventKind, StringComparer.Ordinal)
            .ThenBy(static value => value.OperationId, StringComparer.Ordinal)
            .ToArray();
        ProjectionInput = projectionInput ?? throw new ArgumentNullException(nameof(projectionInput));
        OwnerAuthority = ownerAuthority ?? throw new ArgumentNullException(nameof(ownerAuthority));
        EffectPlan = effectPlan;

        TouchedPaths = NormalizePaths(touchedPaths, nameof(touchedPaths));
        ConsumedPaths = NormalizePaths(consumedPaths, nameof(consumedPaths));
        ValidatePathCoverage();
    }

    internal string InputFingerprint { get; }

    internal JsonObject DefinitionAfterImage => _definitionAfterImage.DeepClone().AsObject();

    internal JsonObject StateAfterImage => _stateAfterImage.DeepClone().AsObject();

    internal JsonObject HistoryAfterImage => _historyAfterImage.DeepClone().AsObject();

    internal IReadOnlyDictionary<string, JsonObject> EffectCarrierAfterImages =>
        ReadOnlyObjects(_effectCarrierAfterImages);

    internal JsonObject EffectIdentityAfterImage => _effectIdentityAfterImage.DeepClone().AsObject();

    internal IReadOnlyDictionary<string, JsonObject?> PendingAfterImages =>
        ReadOnlyNullableObjects(_pendingAfterImages);

    internal IReadOnlyDictionary<string, JsonObject> OwnerCompanionAfterImages =>
        ReadOnlyObjects(_ownerCompanionAfterImages);

    internal IReadOnlyList<AcceptedMechanicsOwnerTransition> OwnerTransitions =>
        Array.AsReadOnly(_ownerTransitions.Select(static value => value.Clone()).ToArray());

    internal IReadOnlyDictionary<string, CanonicalBeforeImage> BeforeImages =>
        AcceptedMechanicsPlanBinding.CloneReadOnlyBeforeImages(_beforeImages);

    internal IReadOnlyList<string> TouchedPaths { get; }

    internal IReadOnlyList<string> ConsumedPaths { get; }

    internal AcceptedMechanicsAuthorityFingerprints AuthorityFingerprints { get; }

    internal IReadOnlyList<ResourceAppliedEvent> ResourceEvents =>
        Array.AsReadOnly(_resourceEvents.ToArray());

    internal ResourceProjectionInput ProjectionInput { get; }

    internal ResourceOwnerAuthority OwnerAuthority { get; }

    internal EffectAcceptedTurnPlan? EffectPlan { get; }

    private void ValidatePathCoverage()
    {
        var touched = new HashSet<string>(TouchedPaths, StringComparer.Ordinal);
        var required = new HashSet<string>(StringComparer.Ordinal)
        {
            DefinitionPath,
            StatePath,
            HistoryPath,
            EffectAcceptedTurnPlan.IdentityIndexPath
        };
        required.UnionWith(_effectCarrierAfterImages.Keys);
        required.UnionWith(_pendingAfterImages.Keys);
        required.UnionWith(_ownerCompanionAfterImages.Keys);
        required.UnionWith(_ownerTransitions.Select(static value => value.Path));
        required.UnionWith(ConsumedPaths);
        foreach (var path in required)
        {
            if (!touched.Contains(path))
                throw new ArgumentException($"Plan path '{path}' is not declared touched.", nameof(TouchedPaths));
        }
        foreach (var path in TouchedPaths)
        {
            if (!_beforeImages.ContainsKey(path))
                throw new ArgumentException($"Plan path '{path}' has no exact before-image.", nameof(BeforeImages));
        }
    }

    private static JsonObject Clone(JsonObject value, string parameterName) =>
        (value ?? throw new ArgumentNullException(parameterName)).DeepClone().AsObject();

    private static Dictionary<string, JsonObject> CloneObjects(
        IReadOnlyDictionary<string, JsonObject> values,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        var result = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var pair in values)
        {
            AcceptedMechanicsPlanBinding.ValidatePath(pair.Key, parameterName);
            result.Add(pair.Key, Clone(pair.Value, parameterName));
        }
        return result;
    }

    private static Dictionary<string, JsonObject?> CloneNullableObjects(
        IReadOnlyDictionary<string, JsonObject?> values,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        var result = new Dictionary<string, JsonObject?>(StringComparer.Ordinal);
        foreach (var pair in values)
        {
            AcceptedMechanicsPlanBinding.ValidatePath(pair.Key, parameterName);
            result.Add(pair.Key, pair.Value?.DeepClone().AsObject());
        }
        return result;
    }

    private static Dictionary<string, CanonicalBeforeImage> CloneBeforeImages(
        IReadOnlyDictionary<string, CanonicalBeforeImage> values,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        var result = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
        foreach (var pair in values)
        {
            AcceptedMechanicsPlanBinding.ValidatePath(pair.Key, parameterName);
            ArgumentNullException.ThrowIfNull(pair.Value);
            result.Add(
                pair.Key,
                new CanonicalBeforeImage(pair.Value.Existed, pair.Value.Bytes));
        }
        return result;
    }

    private static IReadOnlyDictionary<string, JsonObject> ReadOnlyObjects(
        IReadOnlyDictionary<string, JsonObject> values) =>
        new ReadOnlyDictionary<string, JsonObject>(values.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.DeepClone().AsObject(),
            StringComparer.Ordinal));

    private static IReadOnlyDictionary<string, JsonObject?> ReadOnlyNullableObjects(
        IReadOnlyDictionary<string, JsonObject?> values) =>
        new ReadOnlyDictionary<string, JsonObject?>(values.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value?.DeepClone().AsObject(),
            StringComparer.Ordinal));

    private static IReadOnlyList<string> NormalizePaths(
        IReadOnlyList<string> paths,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(paths, parameterName);
        foreach (var path in paths)
            AcceptedMechanicsPlanBinding.ValidatePath(path, parameterName);
        return Array.AsReadOnly(paths
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray());
    }
}

internal sealed record AcceptedMechanicsPlanningResult
{
    private readonly ValidationIssue[] _issues;

    internal AcceptedMechanicsPlanningResult(
        AcceptedMechanicsPlan? plan,
        IReadOnlyList<ValidationIssue> issues)
    {
        Plan = plan;
        ArgumentNullException.ThrowIfNull(issues);
        _issues = issues.ToArray();
    }

    internal AcceptedMechanicsPlan? Plan { get; }

    internal IReadOnlyList<ValidationIssue> Issues =>
        Array.AsReadOnly(_issues.ToArray());

    internal bool Success => Plan != null && Issues.Count == 0;
}
