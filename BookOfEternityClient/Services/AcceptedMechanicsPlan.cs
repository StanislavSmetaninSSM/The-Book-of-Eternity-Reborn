using System.Collections.ObjectModel;
using System.Globalization;
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
    string InternalInputs,
    string WoundCarriers,
    string WoundIdentityIndex,
    string WoundHistory)
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
        yield return new("woundCarriers", WoundCarriers);
        yield return new("woundIdentityIndex", WoundIdentityIndex);
        yield return new("woundHistory", WoundHistory);
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
    private readonly JsonObject? _woundCommands;
    private readonly WoundAcceptedTurnInput? _woundInput;
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
        IReadOnlyDictionary<string, CanonicalBeforeImage> beforeImages,
        JsonObject? woundCommands = null,
        WoundAcceptedTurnInput? woundInput = null)
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
        _woundCommands = woundCommands?.DeepClone().AsObject();
        _woundInput = WoundAcceptedTurnData.CloneInput(woundInput);
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

    internal JsonObject? WoundCommands => _woundCommands?.DeepClone().AsObject();

    internal WoundAcceptedTurnInput? WoundInput =>
        WoundAcceptedTurnData.CloneInput(_woundInput);

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
        AcceptedMechanicsPlanningContext? PlanningContext = null,
        JsonObject? WoundCommands = null,
        WoundAcceptedTurnInput? WoundInput = null)
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
            BeforeImages,
            WoundCommands,
            WoundInput);
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

    internal JsonObject? WoundCommands => _binding.WoundCommands;

    internal WoundAcceptedTurnInput? WoundInput => _binding.WoundInput;

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
        BeforeImages,
        WoundCommands,
        WoundInput);
}

internal sealed class AcceptedMechanicsWoundStageBundle
{
    private readonly WoundAcceptedTurnInput _input;
    private readonly WoundPreparedAcceptedTurnPlan _preparedPlan;
    private readonly WoundEffectBatchAcceptedPlan _effectBatchPlan;
    private readonly WoundAcceptedTurnPlan _finalPlan;
    private readonly Dictionary<string, EffectAcceptedApplicationResult>
        _applicationResults;
    private readonly Dictionary<string, EffectAcceptedTerminationResult>
        _terminationResults;

    internal AcceptedMechanicsWoundStageBundle(
        WoundAcceptedTurnInput input,
        WoundPreparedAcceptedTurnPlan preparedPlan,
        WoundEffectBatchAcceptedPlan effectBatchPlan,
        WoundAcceptedTurnPlan finalPlan)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(preparedPlan);
        ArgumentNullException.ThrowIfNull(effectBatchPlan);
        ArgumentNullException.ThrowIfNull(finalPlan);

        var inputFingerprint = WoundAcceptedTurnFingerprints.ComputeInput(input);
        var validatedPrepared = WoundAcceptedTurnPlanCache.ValidatePreparedResult(
            input,
            inputFingerprint,
            new WoundAcceptedTurnPreparationResult(
                preparedPlan,
                Array.Empty<ValidationIssue>()));
        if (!validatedPrepared.Success)
        {
            throw new ArgumentException(
                "The prepared wound stage does not match the full detached input.",
                nameof(preparedPlan));
        }
        var validatedEffect = WoundEffectBatchPlanner.AcceptEffectResult(
            preparedPlan,
            effectBatchPlan.EffectInput,
            new EffectAcceptedTurnPlanningResult(
                effectBatchPlan.EffectPlan,
                Array.Empty<ValidationIssue>()));
        if (!validatedEffect.Success ||
            !string.Equals(
                validatedEffect.Plan!.EffectAcceptedTurnPlanFingerprint,
                effectBatchPlan.EffectAcceptedTurnPlanFingerprint,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The effect wound stage does not match the full detached preparation.",
                nameof(effectBatchPlan));
        }
        var validatedFinal = WoundAcceptedTurnPlanCache.ValidateFinalResult(
            preparedPlan,
            effectBatchPlan,
            new WoundAcceptedTurnPlanningResult(
                finalPlan,
                Array.Empty<ValidationIssue>()));
        if (!validatedFinal.Success)
        {
            throw new ArgumentException(
                "The final wound stage does not match the full detached handoff.",
                nameof(finalPlan));
        }
        RequireEqual(
            inputFingerprint,
            preparedPlan.InputFingerprint,
            nameof(preparedPlan),
            "prepared input");
        RequireEqual(
            WoundAcceptedTurnFingerprints.ComputeBinding(input.Binding),
            preparedPlan.BindingFingerprint,
            nameof(preparedPlan),
            "prepared binding");
        ValidatePreparedInternalAuthority(preparedPlan);

        var preparationFingerprint =
            WoundAcceptedTurnFingerprints.ComputePreparation(preparedPlan);
        RequireEqual(
            preparationFingerprint,
            preparedPlan.WoundPreparationFingerprint,
            nameof(preparedPlan),
            "prepared plan");
        RequireEqual(
            preparationFingerprint,
            effectBatchPlan.WoundPreparationFingerprint,
            nameof(effectBatchPlan),
            "effect preparation handoff");

        var effectInputFingerprint =
            WoundAcceptedTurnFingerprints.ComputeEffectInput(
                preparedPlan,
                effectBatchPlan.EffectInput);
        RequireEqual(
            effectInputFingerprint,
            effectBatchPlan.EffectInputFingerprint,
            nameof(effectBatchPlan),
            "effect input");

        var effectPlanFingerprint =
            WoundAcceptedTurnFingerprints.ComputeEffectPlan(
                preparedPlan,
                effectBatchPlan.EffectInput,
                effectBatchPlan.EffectPlan,
                effectBatchPlan.ApplicationResults,
                effectBatchPlan.TerminationResults);
        RequireEqual(
            effectPlanFingerprint,
            effectBatchPlan.EffectAcceptedTurnPlanFingerprint,
            nameof(effectBatchPlan),
            "effect accepted plan");

        RequireEqual(
            preparedPlan.BindingFingerprint,
            finalPlan.BindingFingerprint,
            nameof(finalPlan),
            "final binding");
        RequireEqual(
            WoundAcceptedTurnFingerprints.ComputeBinding(finalPlan.Binding),
            finalPlan.BindingFingerprint,
            nameof(finalPlan),
            "final binding payload");
        RequireEqual(
            inputFingerprint,
            finalPlan.InputFingerprint,
            nameof(finalPlan),
            "final input");
        RequireEqual(
            preparationFingerprint,
            finalPlan.WoundPreparationFingerprint,
            nameof(finalPlan),
            "final preparation handoff");
        RequireEqual(
            effectInputFingerprint,
            finalPlan.EffectInputFingerprint,
            nameof(finalPlan),
            "final effect-input handoff");
        RequireEqual(
            effectPlanFingerprint,
            finalPlan.EffectAcceptedTurnPlanFingerprint,
            nameof(finalPlan),
            "final effect-plan handoff");
        RequireSequenceEqual(
            preparedPlan.AllocatedWoundIds,
            finalPlan.AllocatedWoundIds,
            nameof(finalPlan),
            "allocated wound identities");
        RequireSequenceEqual(
            preparedPlan.AllocatedTransitionIds,
            finalPlan.AllocatedTransitionIds,
            nameof(finalPlan),
            "allocated wound transition identities");

        var finalFingerprint = WoundAcceptedTurnFingerprints.ComputeFinal(
            preparedPlan,
            effectBatchPlan,
            finalPlan.CarrierContributions,
            finalPlan.IdentityIndexAfterImage,
            finalPlan.HistoryAfterImage,
            finalPlan.TransitionIntents);
        RequireEqual(
            finalFingerprint,
            finalPlan.WoundFinalPlanFingerprint,
            nameof(finalPlan),
            "final wound plan");

        _input = WoundAcceptedTurnData.CloneInput(input)!;
        _preparedPlan = WoundAcceptedTurnData.ClonePreparedPlan(preparedPlan)!;
        _effectBatchPlan =
            WoundAcceptedTurnData.CloneEffectBatchPlan(effectBatchPlan)!;
        _finalPlan = WoundAcceptedTurnData.CloneFinalPlan(finalPlan)!;
        _applicationResults = CreateApplicationMap(
            effectBatchPlan.ApplicationResults);
        _terminationResults = CreateTerminationMap(
            effectBatchPlan.TerminationResults);
        InputFingerprint = inputFingerprint;
        WoundPreparationFingerprint = preparationFingerprint;
        EffectInputFingerprint = effectInputFingerprint;
        EffectAcceptedTurnPlanFingerprint = effectPlanFingerprint;
        WoundFinalPlanFingerprint = finalFingerprint;
        BundleFingerprint = ComputeBundleFingerprint(
            preparedPlan,
            inputFingerprint,
            preparationFingerprint,
            effectInputFingerprint,
            effectPlanFingerprint,
            finalFingerprint);
    }

    internal WoundAcceptedTurnInput Input =>
        WoundAcceptedTurnData.CloneInput(_input)!;

    internal WoundPreparedAcceptedTurnPlan PreparedPlan =>
        WoundAcceptedTurnData.ClonePreparedPlan(_preparedPlan)!;

    internal WoundEffectBatchAcceptedPlan EffectBatchPlan =>
        WoundAcceptedTurnData.CloneEffectBatchPlan(_effectBatchPlan)!;

    internal WoundAcceptedTurnPlan FinalPlan =>
        WoundAcceptedTurnData.CloneFinalPlan(_finalPlan)!;

    internal string InputFingerprint { get; }

    internal string WoundPreparationFingerprint { get; }

    internal string EffectInputFingerprint { get; }

    internal string EffectAcceptedTurnPlanFingerprint { get; }

    internal string WoundFinalPlanFingerprint { get; }

    internal string BundleFingerprint { get; }

    internal IReadOnlyDictionary<string, EffectAcceptedApplicationResult>
        ApplicationResults =>
        new ReadOnlyDictionary<string, EffectAcceptedApplicationResult>(
            _applicationResults.ToDictionary(
                static pair => pair.Key,
                static pair =>
                    WoundAcceptedTurnData.CloneApplicationResult(pair.Value),
                StringComparer.Ordinal));

    internal IReadOnlyDictionary<string, EffectAcceptedTerminationResult>
        TerminationResults =>
        new ReadOnlyDictionary<string, EffectAcceptedTerminationResult>(
            _terminationResults.ToDictionary(
                static pair => pair.Key,
                static pair =>
                    WoundAcceptedTurnData.CloneTerminationResult(pair.Value),
                StringComparer.Ordinal));

    internal AcceptedMechanicsWoundStageBundle DetachedCopy() =>
        new(_input, _preparedPlan, _effectBatchPlan, _finalPlan);

    private static Dictionary<string, EffectAcceptedApplicationResult>
        CreateApplicationMap(
            IReadOnlyList<EffectAcceptedApplicationResult> values)
    {
        var result = new Dictionary<string, EffectAcceptedApplicationResult>(
            StringComparer.Ordinal);
        foreach (var value in values)
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!result.TryAdd(
                    value.ApplicationRef,
                    WoundAcceptedTurnData.CloneApplicationResult(value)))
            {
                throw new ArgumentException(
                    "Wound effect application results require unique refs.",
                    nameof(values));
            }
        }
        return result;
    }

    private static Dictionary<string, EffectAcceptedTerminationResult>
        CreateTerminationMap(
            IReadOnlyList<EffectAcceptedTerminationResult> values)
    {
        var result = new Dictionary<string, EffectAcceptedTerminationResult>(
            StringComparer.Ordinal);
        foreach (var value in values)
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!result.TryAdd(
                    value.OperationRef,
                    WoundAcceptedTurnData.CloneTerminationResult(value)))
            {
                throw new ArgumentException(
                    "Wound effect termination results require unique refs.",
                    nameof(values));
            }
        }
        return result;
    }

    private static void RequireEqual(
        string expected,
        string actual,
        string parameterName,
        string stage)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"The {stage} does not belong to one complete wound stage bundle.",
                parameterName);
        }
    }

    private static void ValidatePreparedInternalAuthority(
        WoundPreparedAcceptedTurnPlan preparedPlan)
    {
        var baseline = preparedPlan.BaselineAuthority;
        RequireEqual(
            preparedPlan.InputFingerprint,
            baseline.PreparedInputFingerprint,
            nameof(preparedPlan),
            "prepared baseline input");
        RequireEqual(
            WoundAcceptedTurnFingerprints.ComputeBaselineAuthority(
                baseline.PreparedInputFingerprint,
                baseline.PreTurnCarriers,
                baseline.PreTurnIdentityIndex,
                baseline.PreTurnHistory),
            baseline.AuthoritySeal,
            nameof(preparedPlan),
            "prepared baseline authority");

        foreach (var batch in preparedPlan.EffectOperationBatches)
        {
            RequireEqual(
                WoundAcceptedTurnFingerprints.ComputeSourceExport(batch),
                batch.SourceExportFingerprint,
                nameof(preparedPlan),
                "prepared source export");
            var transition = batch.TransitionAuthority;
            RequireEqual(
                preparedPlan.InputFingerprint,
                transition.PreparedInputFingerprint,
                nameof(preparedPlan),
                "prepared transition input");
            RequireEqual(
                WoundAcceptedTurnFingerprints.ComputeTransitionAuthority(
                    transition.PreparedInputFingerprint,
                    batch.LocalWoundRef,
                    batch.PreparedWoundId,
                    transition.OpportunityId,
                    transition.OpportunityAuthorityFingerprint,
                    transition.OperationKey,
                    transition.ReadableSummary,
                    transition.MaximumSeverityRank),
                transition.AuthoritySeal,
                nameof(preparedPlan),
                "prepared transition authority");
        }
    }

    private static void RequireSequenceEqual(
        IReadOnlyList<string> expected,
        IReadOnlyList<string> actual,
        string parameterName,
        string stage)
    {
        if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"The {stage} do not belong to one complete wound stage bundle.",
                parameterName);
        }
    }

    private static string ComputeBundleFingerprint(
        WoundPreparedAcceptedTurnPlan preparedPlan,
        string inputFingerprint,
        string preparationFingerprint,
        string effectInputFingerprint,
        string effectPlanFingerprint,
        string finalFingerprint)
    {
        var fields = new List<string?>
        {
            "book_of_eternity.accepted_mechanics.wound_stage_bundle",
            "1",
            preparedPlan.BindingFingerprint,
            inputFingerprint,
            preparationFingerprint,
            effectInputFingerprint,
            effectPlanFingerprint,
            finalFingerprint,
            preparedPlan.BaselineAuthority.AuthoritySeal,
            preparedPlan.EffectOperationBatches.Count.ToString(
                CultureInfo.InvariantCulture)
        };
        foreach (var batch in preparedPlan.EffectOperationBatches)
        {
            fields.Add(batch.SourceExportFingerprint);
            fields.Add(batch.TransitionAuthority.AuthoritySeal);
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }
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
    private readonly AcceptedMechanicsWoundStageBundle? _woundStageBundle;

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
        IReadOnlyList<IResourceRegisteredSystemOutcomeDraft>? registeredSystemOutcomes = null,
        ResourcePendingResolutionState? pendingResolutionState = null,
        AcceptedMechanicsIdentityFactory? resourceIdentityFactory = null,
        EffectIdentityFactory? effectIdentityFactory = null,
        int executionSequenceOffset = 0,
        AcceptedMechanicsWoundStageBundle? woundStageBundle = null)
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
        PendingResolutionState = pendingResolutionState;
        ResourceIdentityFactory = resourceIdentityFactory;
        EffectIdentityFactory = effectIdentityFactory;
        _woundStageBundle = woundStageBundle?.DetachedCopy();
        if (executionSequenceOffset < 0)
            throw new ArgumentOutOfRangeException(nameof(executionSequenceOffset));
        ExecutionSequenceOffset = executionSequenceOffset;
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
    internal AcceptedMechanicsWoundStageBundle? WoundStageBundle =>
        _woundStageBundle?.DetachedCopy();
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
    internal ResourcePendingResolutionState? PendingResolutionState { get; }
    internal AcceptedMechanicsIdentityFactory? ResourceIdentityFactory { get; }
    internal EffectIdentityFactory? EffectIdentityFactory { get; }
    internal int ExecutionSequenceOffset { get; }
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

internal sealed class AcceptedMechanicsWoundPublication
{
    private readonly Dictionary<string, JsonObject> _carrierAfterImages;
    private readonly JsonObject _identityAfterImage;
    private readonly JsonObject _historyAfterImage;

    internal AcceptedMechanicsWoundPublication(
        IReadOnlyDictionary<string, JsonObject> carrierAfterImages,
        JsonObject identityAfterImage,
        JsonObject historyAfterImage)
    {
        ArgumentNullException.ThrowIfNull(carrierAfterImages);
        ArgumentNullException.ThrowIfNull(identityAfterImage);
        ArgumentNullException.ThrowIfNull(historyAfterImage);
        _carrierAfterImages = new Dictionary<string, JsonObject>(
            StringComparer.Ordinal);
        foreach (var pair in carrierAfterImages)
        {
            AcceptedMechanicsPlanBinding.ValidatePath(
                pair.Key,
                nameof(carrierAfterImages));
            _carrierAfterImages.Add(
                pair.Key,
                (pair.Value ?? throw new ArgumentNullException(
                    nameof(carrierAfterImages))).DeepClone().AsObject());
        }
        _identityAfterImage = identityAfterImage.DeepClone().AsObject();
        _historyAfterImage = historyAfterImage.DeepClone().AsObject();
    }

    internal IReadOnlyDictionary<string, JsonObject> CarrierAfterImages =>
        new ReadOnlyDictionary<string, JsonObject>(
            _carrierAfterImages.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.DeepClone().AsObject(),
                StringComparer.Ordinal));

    internal JsonObject IdentityAfterImage =>
        _identityAfterImage.DeepClone().AsObject();

    internal JsonObject HistoryAfterImage =>
        _historyAfterImage.DeepClone().AsObject();

    internal AcceptedMechanicsWoundPublication DetachedCopy() =>
        new(_carrierAfterImages, _identityAfterImage, _historyAfterImage);
}

internal sealed class AcceptedMechanicsPlan
{
    internal const string DefinitionPath = "game_state/resources/resource_definitions.json";
    internal const string StatePath = "game_state/resources/resource_state.json";
    internal const string HistoryPath = "game_state/resources/resource_history.json";
    internal const string WoundCommandPath =
        "game_state/wounds/wound_commands.json";

    private readonly JsonObject _definitionAfterImage;
    private readonly JsonObject _stateAfterImage;
    private readonly JsonObject _historyAfterImage;
    private readonly Dictionary<string, JsonObject> _effectCarrierAfterImages;
    private readonly JsonObject _effectIdentityAfterImage;
    private readonly Dictionary<string, JsonObject?> _pendingAfterImages;
    private readonly JsonObject? _pendingGmPacket;
    private readonly Dictionary<string, JsonObject> _ownerCompanionAfterImages;
    private readonly AcceptedMechanicsOwnerTransition[] _ownerTransitions;
    private readonly Dictionary<string, CanonicalBeforeImage> _beforeImages;
    private readonly ResourceAppliedEvent[] _resourceEvents;
    private readonly AcceptedMechanicsWoundStageBundle? _woundStageBundle;
    private readonly AcceptedMechanicsWoundPublication? _woundPublication;

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
        IReadOnlyList<AcceptedMechanicsOwnerTransition>? ownerTransitions = null,
        JsonObject? pendingGmPacket = null,
        AcceptedMechanicsWoundStageBundle? woundStageBundle = null,
        IReadOnlyDictionary<string, JsonObject>? woundCarrierAfterImages = null,
        JsonObject? woundIdentityAfterImage = null,
        JsonObject? woundHistoryAfterImage = null)
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
        _pendingGmPacket = pendingGmPacket?.DeepClone().AsObject();
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
        _woundStageBundle = woundStageBundle?.DetachedCopy();
        var hasAnyWoundPublication =
            woundCarrierAfterImages is not null ||
            woundIdentityAfterImage is not null ||
            woundHistoryAfterImage is not null;
        if ((_woundStageBundle is null) != !hasAnyWoundPublication ||
            (hasAnyWoundPublication &&
             (woundCarrierAfterImages is null ||
              woundIdentityAfterImage is null ||
              woundHistoryAfterImage is null)))
        {
            throw new ArgumentException(
                "A wound stage bundle and all wound publication after-images must be supplied together.",
                nameof(woundStageBundle));
        }
        _woundPublication = !hasAnyWoundPublication
            ? null
            : new AcceptedMechanicsWoundPublication(
                woundCarrierAfterImages!,
                woundIdentityAfterImage!,
                woundHistoryAfterImage!);
        if (_woundStageBundle is not null && _woundPublication is not null)
        {
            var final = _woundStageBundle.FinalPlan;
            if (!JsonNode.DeepEquals(
                    final.IdentityIndexAfterImage,
                    _woundPublication.IdentityAfterImage) ||
                !JsonNode.DeepEquals(
                    final.HistoryAfterImage,
                    _woundPublication.HistoryAfterImage))
            {
                throw new ArgumentException(
                    "Wound identity/history publication must equal the final wound stage after-images.",
                    nameof(woundStageBundle));
            }
        }
        TouchedPaths = NormalizePaths(touchedPaths, nameof(touchedPaths));
        ConsumedPaths = NormalizePaths(consumedPaths, nameof(consumedPaths));
        ValidateWholeRootProducerConflicts();
        ValidatePathCoverage();
        PreparedPlanFingerprint =
            AcceptedMechanicsPlanFingerprints.ComputePrepared(this);
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

    internal bool AwaitsPendingResolution => _pendingGmPacket != null;

    internal JsonObject? PendingGmPacket => _pendingGmPacket?.DeepClone().AsObject();

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

    internal AcceptedMechanicsWoundStageBundle? WoundStageBundle =>
        _woundStageBundle?.DetachedCopy();

    internal IReadOnlyDictionary<string, JsonObject> WoundCarrierAfterImages =>
        _woundPublication?.CarrierAfterImages ??
        new ReadOnlyDictionary<string, JsonObject>(
            new Dictionary<string, JsonObject>(StringComparer.Ordinal));

    internal JsonObject? WoundIdentityAfterImage =>
        _woundPublication?.IdentityAfterImage;

    internal JsonObject? WoundHistoryAfterImage =>
        _woundPublication?.HistoryAfterImage;

    internal string PreparedPlanFingerprint { get; }

    private void ValidatePathCoverage()
    {
        var touched = new HashSet<string>(TouchedPaths, StringComparer.Ordinal);
        var required = new HashSet<string>(StringComparer.Ordinal)
        {
            DefinitionPath,
            StatePath,
            HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            EffectAcceptedTurnPlan.IdentityIndexPath
        };
        required.UnionWith(_effectCarrierAfterImages.Keys);
        if (_woundPublication is not null)
        {
            required.UnionWith(_woundPublication.CarrierAfterImages.Keys);
            required.Add(WoundIdentityState.StatePath);
            required.Add(WoundHistoryState.HistoryPath);
        }
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

    private void ValidateWholeRootProducerConflicts()
    {
        var producers = new Dictionary<string, string>(StringComparer.Ordinal);
        AddProducers(
            new[] { DefinitionPath, StatePath, HistoryPath },
            "resource",
            producers);
        AddProducers(
            new[] { CanonicalResourceOwnerAuthorityComposer.AuthorityPath },
            "resource owner authority",
            producers);
        AddProducers(
            new[] { EffectAcceptedTurnPlan.IdentityIndexPath },
            "effect identity",
            producers);
        AddProducers(_effectCarrierAfterImages.Keys, "effect", producers);
        if (_woundPublication is not null)
        {
            AddProducers(
                new[]
                {
                    WoundIdentityState.StatePath,
                    WoundHistoryState.HistoryPath
                },
                "wound identity/history",
                producers);
            AddProducers(_woundPublication.CarrierAfterImages.Keys, "wound", producers);
        }
        AddProducers(_ownerCompanionAfterImages.Keys, "owner", producers);
        AddProducers(
            _ownerTransitions
                .Select(static value => value.Path)
                .Distinct(StringComparer.Ordinal),
            "transition",
            producers);
        AddProducers(
            _pendingAfterImages
                .Where(static pair => pair.Value is not null)
                .Select(static pair => pair.Key),
            "pending",
            producers);
        AddProducers(
            _pendingAfterImages
                .Where(static pair => pair.Value is null)
                .Select(static pair => pair.Key)
                .Concat(ConsumedPaths)
                .Distinct(StringComparer.Ordinal),
            "delete",
            producers);
    }

    private static void AddProducers(
        IEnumerable<string> paths,
        string producer,
        IDictionary<string, string> producers)
    {
        foreach (var path in paths)
        {
            if (producers.TryGetValue(path, out var existing))
            {
                throw new ArgumentException(
                    $"Plan path '{path}' has competing {existing} and {producer} whole-root producers.",
                    nameof(paths));
            }
            producers.Add(path, producer);
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

internal static class AcceptedMechanicsPlanFingerprints
{
    private const string Version = "1";
    private const string InputDomain =
        "book_of_eternity.accepted_mechanics.input";
    private const string PreparedDomain =
        "book_of_eternity.accepted_mechanics.prepared_plan";
    private const string PlanningInputDomain =
        "book_of_eternity.accepted_mechanics.planning_input";

    internal static string ComputeInput(AcceptedMechanicsPlanBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        var fields = new List<string?>
        {
            InputDomain,
            Version,
            binding.SessionId,
            binding.RequestId,
            binding.SnapshotToken,
            binding.Realm,
            Number(binding.Turn),
            Canonical(binding.AcceptedEvents),
            Canonical(binding.ResourceCommands),
            Canonical(binding.EffectCommands),
            Canonical(binding.PendingInput),
            Canonical(binding.InternalInputs)
        };
        AppendOptionalJson(fields, binding.WoundCommands);
        var woundInput = binding.WoundInput;
        fields.Add(woundInput is null ? null : "present");
        fields.Add(woundInput is null
            ? null
            : WoundAcceptedTurnFingerprints.ComputeInput(woundInput));
        AppendAuthorities(fields, binding.AuthorityFingerprints);
        AppendBeforeImages(fields, binding.BeforeImages);
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    internal static string ComputePrepared(AcceptedMechanicsPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var fields = new List<string?>
        {
            PreparedDomain,
            Version,
            plan.InputFingerprint,
            Canonical(plan.DefinitionAfterImage),
            Canonical(plan.StateAfterImage),
            Canonical(plan.HistoryAfterImage)
        };
        AppendAuthorities(fields, plan.AuthorityFingerprints);
        AppendObjectMap(fields, plan.EffectCarrierAfterImages);
        fields.Add(Canonical(plan.EffectIdentityAfterImage));
        AppendNullableObjectMap(fields, plan.PendingAfterImages);
        AppendOptionalJson(fields, plan.PendingGmPacket);
        AppendObjectMap(fields, plan.OwnerCompanionAfterImages);
        AppendOwnerTransitions(fields, plan.OwnerTransitions);
        AppendResourceEvents(fields, plan.ResourceEvents);
        fields.Add(Canonical(plan.ProjectionInput.Definitions));
        fields.Add(Canonical(plan.ProjectionInput.State));
        fields.Add(Canonical(plan.ProjectionInput.History));
        fields.Add(plan.ProjectionInput.OwnerAuthorityFingerprint);
        fields.Add(plan.OwnerAuthority.Fingerprint);
        AppendEffectPlan(fields, plan.EffectPlan);
        AppendWoundStages(fields, plan.WoundStageBundle);
        AppendObjectMap(fields, plan.WoundCarrierAfterImages);
        AppendOptionalJson(fields, plan.WoundIdentityAfterImage);
        AppendOptionalJson(fields, plan.WoundHistoryAfterImage);
        AppendBeforeImages(fields, plan.BeforeImages);
        AppendOrdered(fields, plan.TouchedPaths);
        AppendOrdered(fields, plan.ConsumedPaths);
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    internal static string ComputePlanningInput(AcceptedMechanicsInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var fields = new List<string?>
        {
            PlanningInputDomain,
            Version,
            ComputeInput(input.CreateBinding())
        };
        AppendWoundStages(fields, input.PlanningContext?.WoundStageBundle);
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static void AppendWoundStages(
        ICollection<string?> fields,
        AcceptedMechanicsWoundStageBundle? bundle)
    {
        if (bundle is null)
        {
            fields.Add(null);
            return;
        }
        fields.Add("present");
        var input = bundle.Input;
        var prepared = bundle.PreparedPlan;
        var effect = bundle.EffectBatchPlan;
        var final = bundle.FinalPlan;
        fields.Add(WoundAcceptedTurnFingerprints.ComputeInput(input));
        fields.Add(WoundAcceptedTurnFingerprints.ComputePreparation(prepared));
        fields.Add(WoundAcceptedTurnFingerprints.ComputeEffectInput(
            prepared,
            effect.EffectInput));
        fields.Add(WoundAcceptedTurnFingerprints.ComputeEffectPlan(
            prepared,
            effect.EffectInput,
            effect.EffectPlan,
            effect.ApplicationResults,
            effect.TerminationResults));
        fields.Add(WoundAcceptedTurnFingerprints.ComputeFinal(
            prepared,
            effect,
            final.CarrierContributions,
            final.IdentityIndexAfterImage,
            final.HistoryAfterImage,
            final.TransitionIntents));
        fields.Add(bundle.BundleFingerprint);
    }

    private static void AppendEffectPlan(
        ICollection<string?> fields,
        EffectAcceptedTurnPlan? plan)
    {
        if (plan is null)
        {
            fields.Add(null);
            return;
        }
        fields.Add("present");
        fields.Add(WoundAcceptedTurnFingerprints
            .ComputeAcceptedEffectPlanPayload(plan));
    }

    private static void AppendAuthorities(
        ICollection<string?> fields,
        AcceptedMechanicsAuthorityFingerprints value)
    {
        var entries = value.Enumerate()
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .ToArray();
        fields.Add(Number(entries.Length));
        foreach (var pair in entries)
        {
            fields.Add(pair.Key);
            fields.Add(pair.Value);
        }
    }

    private static void AppendBeforeImages(
        ICollection<string?> fields,
        IReadOnlyDictionary<string, CanonicalBeforeImage> values)
    {
        fields.Add(Number(values.Count));
        foreach (var pair in values.OrderBy(
                     static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            fields.Add(pair.Key);
            fields.Add(pair.Value.Existed ? "present" : "missing");
            fields.Add(pair.Value.Bytes is null
                ? null
                : Convert.ToBase64String(pair.Value.Bytes));
        }
    }

    private static void AppendObjectMap(
        ICollection<string?> fields,
        IReadOnlyDictionary<string, JsonObject> values)
    {
        fields.Add(Number(values.Count));
        foreach (var pair in values.OrderBy(
                     static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            fields.Add(pair.Key);
            fields.Add(Canonical(pair.Value));
        }
    }

    private static void AppendNullableObjectMap(
        ICollection<string?> fields,
        IReadOnlyDictionary<string, JsonObject?> values)
    {
        fields.Add(Number(values.Count));
        foreach (var pair in values.OrderBy(
                     static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            fields.Add(pair.Key);
            AppendOptionalJson(fields, pair.Value);
        }
    }

    private static void AppendOwnerTransitions(
        ICollection<string?> fields,
        IReadOnlyList<AcceptedMechanicsOwnerTransition> values)
    {
        fields.Add(Number(values.Count));
        foreach (var value in values)
            fields.Add(Canonical(value.ToFingerprintNode()));
    }

    private static void AppendResourceEvents(
        ICollection<string?> fields,
        IReadOnlyList<ResourceAppliedEvent> values)
    {
        fields.Add(Number(values.Count));
        foreach (var value in values)
        {
            fields.Add(value.EventKind);
            fields.Add(value.OperationId);
            fields.Add(value.EventRef);
            fields.Add(value.Coordinate.Realm);
            fields.Add(value.Coordinate.OwnerKind.ToString());
            fields.Add(value.Coordinate.ResourceOwnerId);
            fields.Add(value.Coordinate.ResourceKey);
            fields.Add(value.Before.ToString("G29", CultureInfo.InvariantCulture));
            fields.Add(value.After.ToString("G29", CultureInfo.InvariantCulture));
            fields.Add(value.AppliedAmount.ToString(
                "G29",
                CultureInfo.InvariantCulture));
            fields.Add(Number(value.Turn));
            fields.Add(Number(value.ExecutionSequence));
            fields.Add(value.SourceFingerprint);
        }
    }

    private static void AppendOptionalJson(
        ICollection<string?> fields,
        JsonObject? value)
    {
        fields.Add(value is null ? null : "present");
        if (value is not null)
            fields.Add(Canonical(value));
    }

    private static void AppendOrdered(
        ICollection<string?> fields,
        IReadOnlyList<string> values)
    {
        fields.Add(Number(values.Count));
        foreach (var value in values)
            fields.Add(value);
    }

    private static string? Canonical(JsonNode? value) =>
        WoundAcceptedTurnFingerprintWriter.CanonicalJson(value);

    private static string Number(int value) =>
        value.ToString(CultureInfo.InvariantCulture);
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
