using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record WoundAcceptedEventAuthority(
    string EventRef,
    string Kind,
    string AuthorityId,
    string SemanticFingerprint);

internal static class WoundAcceptedEventSetFingerprint
{
    private const string Domain = "book_of_eternity.wound.accepted_event_set";
    private const string Version = "1";

    internal static string Compute(
        IReadOnlyList<WoundAcceptedEventAuthority> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        var fields = new List<string?>
        {
            Domain,
            Version,
            events.Count.ToString(CultureInfo.InvariantCulture)
        };
        for (var index = 0; index < events.Count; index++)
        {
            var value = events[index];
            fields.Add(index.ToString(CultureInfo.InvariantCulture));
            fields.Add(value?.EventRef);
            fields.Add(value?.Kind);
            fields.Add(value?.AuthorityId);
            fields.Add(value?.SemanticFingerprint);
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }
}

internal sealed record WoundAcceptedTurnBinding
{
    private WoundAcceptedEventAuthority[]? _acceptedEvents;

    internal WoundAcceptedTurnBinding(
        string sessionId,
        string requestId,
        string snapshotToken,
        string realm,
        int turn,
        IReadOnlyList<WoundAcceptedEventAuthority> acceptedEvents,
        string acceptedEventsFingerprint)
    {
        SessionId = sessionId;
        RequestId = requestId;
        SnapshotToken = snapshotToken;
        Realm = realm;
        Turn = turn;
        AcceptedEvents = acceptedEvents;
        AcceptedEventsFingerprint = acceptedEventsFingerprint;
    }

    internal string SessionId { get; init; }
    internal string RequestId { get; init; }
    internal string SnapshotToken { get; init; }
    internal string Realm { get; init; }
    internal int Turn { get; init; }

    internal IReadOnlyList<WoundAcceptedEventAuthority> AcceptedEvents
    {
        get => WoundAcceptedTurnData.CopyList(_acceptedEvents);
        init => _acceptedEvents = WoundAcceptedTurnData.FreezeList(value);
    }

    internal string AcceptedEventsFingerprint { get; init; }
}

internal sealed record WoundAcceptedTurnInput
{
    private WoundAcceptedTurnBinding? _binding;
    private WoundOpportunityAuthority[]? _opportunities;
    private WoundAcceptedTransitionDraft[]? _transitions;
    private WoundCarrierCatalogInput? _preTurnCarriers;
    private JsonObject? _preTurnIdentityIndex;
    private JsonObject? _preTurnHistory;
    private EffectCarrierCatalogInput? _preTurnEffectCarriers;
    private JsonObject? _preTurnEffectIdentityIndex;

    internal WoundAcceptedTurnInput(
        WoundAcceptedTurnBinding binding,
        IReadOnlyList<WoundOpportunityAuthority> opportunities,
        IReadOnlyList<WoundAcceptedTransitionDraft> transitions,
        WoundCarrierCatalogInput preTurnCarriers,
        JsonObject preTurnIdentityIndex,
        JsonObject preTurnHistory,
        EffectCarrierCatalogInput? preTurnEffectCarriers = null,
        JsonObject? preTurnEffectIdentityIndex = null)
    {
        Binding = binding;
        Opportunities = opportunities;
        Transitions = transitions;
        PreTurnCarriers = preTurnCarriers;
        PreTurnIdentityIndex = preTurnIdentityIndex;
        PreTurnHistory = preTurnHistory;
        PreTurnEffectCarriers = preTurnEffectCarriers;
        PreTurnEffectIdentityIndex = preTurnEffectIdentityIndex;
    }

    internal WoundAcceptedTurnBinding Binding
    {
        get => WoundAcceptedTurnData.CloneBinding(_binding)!;
        init => _binding = WoundAcceptedTurnData.CloneBinding(value);
    }

    internal IReadOnlyList<WoundOpportunityAuthority> Opportunities
    {
        get => WoundAcceptedTurnData.CopyList(
            _opportunities,
            WoundAcceptedTurnData.CloneOpportunity);
        init => _opportunities = WoundAcceptedTurnData.FreezeList(
            value,
            WoundAcceptedTurnData.CloneOpportunity);
    }

    internal IReadOnlyList<WoundAcceptedTransitionDraft> Transitions
    {
        get => WoundAcceptedTurnData.CopyList(
            _transitions,
            WoundAcceptedTurnData.CloneTransitionDraft);
        init => _transitions = WoundAcceptedTurnData.FreezeList(
            value,
            WoundAcceptedTurnData.CloneTransitionDraft);
    }

    internal WoundCarrierCatalogInput PreTurnCarriers
    {
        get => WoundAcceptedTurnData.CloneWoundCarriers(_preTurnCarriers)!;
        init => _preTurnCarriers = WoundAcceptedTurnData.CloneWoundCarriers(value);
    }

    internal JsonObject PreTurnIdentityIndex
    {
        get => WoundAcceptedTurnData.CloneObject(_preTurnIdentityIndex)!;
        init => _preTurnIdentityIndex = WoundAcceptedTurnData.CloneObject(value);
    }

    internal JsonObject PreTurnHistory
    {
        get => WoundAcceptedTurnData.CloneObject(_preTurnHistory)!;
        init => _preTurnHistory = WoundAcceptedTurnData.CloneObject(value);
    }

    internal EffectCarrierCatalogInput? PreTurnEffectCarriers
    {
        get => WoundAcceptedTurnData.CloneEffectCarriers(_preTurnEffectCarriers);
        init => _preTurnEffectCarriers =
            WoundAcceptedTurnData.CloneEffectCarriers(value);
    }

    internal JsonObject? PreTurnEffectIdentityIndex
    {
        get => WoundAcceptedTurnData.CloneObject(_preTurnEffectIdentityIndex);
        init => _preTurnEffectIdentityIndex =
            WoundAcceptedTurnData.CloneObject(value);
    }
}

internal sealed partial record WoundOpportunityAuthority(
    string SessionId,
    string RequestId,
    string SnapshotToken,
    string OpportunityId,
    string PublicRef,
    string EventRef,
    string EventKind,
    string EventAuthorityId,
    string AcceptedEventsFingerprint,
    WoundOwnerCoordinate Owner,
    string Domain,
    string ProfileKey,
    string SourceKind,
    string SourceId,
    string SourceState,
    int? MinimumSeverityRank,
    int MaximumSeverityRank,
    WoundGuaranteedTriggerAuthority? GuaranteedTrigger,
    WoundOpportunitySafeContext SafeContext,
    string InputEvidenceFingerprint,
    string AuthorityFingerprint);

internal sealed record WoundAcceptedEffectDefinitionDraft
{
    private JsonObject? _definition;

    internal WoundAcceptedEffectDefinitionDraft(
        string localEffectRef,
        JsonObject definition)
    {
        LocalEffectRef = localEffectRef;
        Definition = definition;
    }

    internal string LocalEffectRef { get; init; }

    internal JsonObject Definition
    {
        get => WoundAcceptedTurnData.CloneObject(_definition)!;
        init => _definition = WoundAcceptedTurnData.CloneObject(value);
    }
}

internal sealed record WoundRootOwnershipDomain(
    string Kind,
    string? ComplicationId)
{
    internal static WoundRootOwnershipDomain BaseWound { get; } =
        new("base_wound", null);

    internal static WoundRootOwnershipDomain ForComplication(
        string complicationId) => new("complication", complicationId);
}

internal sealed record WoundAcceptedRootApplicationDraft(
    string LocalApplicationRef,
    string LocalEffectRef,
    string OperationKey,
    WoundRootOwnershipDomain OwnershipDomain);

internal sealed record WoundAcceptedConsequenceSlotBinding(
    int Slot,
    string ProfileKey,
    string LocalApplicationRef,
    string ReadableSummary);

internal sealed record WoundAcceptedTransitionDraft
{
    private WoundMaterializationEnvelope? _proposedAfter;
    private WoundAcceptedEffectDefinitionDraft[]? _effectDefinitions;
    private WoundAcceptedRootApplicationDraft[]? _rootApplications;
    private WoundAcceptedConsequenceSlotBinding[]? _slotBindings;

    internal WoundAcceptedTransitionDraft(
        string kind,
        string operationKey,
        string localWoundRef,
        string localTransitionRef,
        string opportunityId,
        string readableSummary,
        WoundMaterializationEnvelope proposedAfter,
        IReadOnlyList<WoundAcceptedEffectDefinitionDraft> effectDefinitions,
        IReadOnlyList<WoundAcceptedRootApplicationDraft> rootApplications,
        IReadOnlyList<WoundAcceptedConsequenceSlotBinding> slotBindings)
    {
        Kind = kind;
        OperationKey = operationKey;
        LocalWoundRef = localWoundRef;
        LocalTransitionRef = localTransitionRef;
        OpportunityId = opportunityId;
        ReadableSummary = readableSummary;
        ProposedAfter = proposedAfter;
        EffectDefinitions = effectDefinitions;
        RootApplications = rootApplications;
        SlotBindings = slotBindings;
    }

    internal string Kind { get; init; }
    internal string OperationKey { get; init; }
    internal string LocalWoundRef { get; init; }
    internal string LocalTransitionRef { get; init; }
    internal string OpportunityId { get; init; }
    internal string ReadableSummary { get; init; }

    internal WoundMaterializationEnvelope ProposedAfter
    {
        get => WoundAcceptedTurnData.CloneWound(_proposedAfter)!;
        init => _proposedAfter = WoundAcceptedTurnData.CloneWound(value);
    }

    internal IReadOnlyList<WoundAcceptedEffectDefinitionDraft> EffectDefinitions
    {
        get => WoundAcceptedTurnData.CopyList(
            _effectDefinitions,
            WoundAcceptedTurnData.CloneDefinitionDraft);
        init => _effectDefinitions = WoundAcceptedTurnData.FreezeList(
            value,
            WoundAcceptedTurnData.CloneDefinitionDraft);
    }

    internal IReadOnlyList<WoundAcceptedRootApplicationDraft> RootApplications
    {
        get => WoundAcceptedTurnData.CopyList(
            _rootApplications,
            WoundAcceptedTurnData.CloneRootApplicationDraft);
        init => _rootApplications = WoundAcceptedTurnData.FreezeList(
            value,
            WoundAcceptedTurnData.CloneRootApplicationDraft);
    }

    internal IReadOnlyList<WoundAcceptedConsequenceSlotBinding> SlotBindings
    {
        get => WoundAcceptedTurnData.CopyList(
            _slotBindings,
            WoundAcceptedTurnData.CloneConsequenceSlotBinding);
        init => _slotBindings = WoundAcceptedTurnData.FreezeList(
            value,
            WoundAcceptedTurnData.CloneConsequenceSlotBinding);
    }
}

internal sealed record WoundAcceptedTurnIdentityScope(
    string SessionId,
    string RequestId,
    string SnapshotToken,
    string Realm,
    int Turn,
    string AcceptedEventsFingerprint,
    string EventRef,
    string OpportunityId,
    WoundOwnerCoordinate Owner,
    string DraftKind,
    string OperationKey,
    string LocalWoundRef);

internal interface IWoundAcceptedTurnIdentityAllocator
{
    string CreateWoundId(WoundAcceptedTurnIdentityScope scope);

    string CreateApplicationRef(
        WoundAcceptedTurnIdentityScope scope,
        string localApplicationRef,
        string definitionKey,
        string operationKey);

    string CreateTransitionId(
        WoundAcceptedTurnIdentityScope scope,
        string localTransitionRef);
}

internal sealed class WoundAcceptedTurnIdentityAllocator :
    IWoundAcceptedTurnIdentityAllocator
{
    public string CreateWoundId(WoundAcceptedTurnIdentityScope scope) =>
        WoundAcceptedTurnIdentityWriter.Create(
            "wound",
            "book_of_eternity.wound.accepted_turn_identity.wound",
            scope);

    public string CreateApplicationRef(
        WoundAcceptedTurnIdentityScope scope,
        string localApplicationRef,
        string definitionKey,
        string operationKey) =>
        WoundAcceptedTurnIdentityWriter.Create(
            "wound_application",
            "book_of_eternity.wound.accepted_turn_identity.application",
            scope,
            localApplicationRef,
            definitionKey,
            operationKey);

    public string CreateTransitionId(
        WoundAcceptedTurnIdentityScope scope,
        string localTransitionRef) =>
        WoundAcceptedTurnIdentityWriter.Create(
            "wound_transition",
            "book_of_eternity.wound.accepted_turn_identity.transition",
            scope,
            localTransitionRef);
}

internal static class WoundAcceptedTurnIdentityWriter
{
    internal static string Create(
        string prefix,
        string domain,
        WoundAcceptedTurnIdentityScope scope,
        params string?[] localFields)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var owner = scope.Owner;
        ArgumentNullException.ThrowIfNull(owner);
        var fields = new List<string?>
        {
            domain,
            "1",
            scope.SessionId,
            scope.RequestId,
            scope.SnapshotToken,
            scope.Realm,
            scope.Turn.ToString(CultureInfo.InvariantCulture),
            scope.AcceptedEventsFingerprint,
            scope.EventRef,
            scope.OpportunityId,
            owner.Realm,
            owner.OwnerKind,
            owner.OwnerId,
            owner.CarrierPath,
            scope.DraftKind,
            scope.OperationKey,
            scope.LocalWoundRef
        };
        fields.AddRange(localFields);
        return prefix + "_" +
            WoundAcceptedTurnFingerprintWriter.Compute(fields)["sha256:".Length..];
    }
}

internal sealed class WoundEffectSourceDefinition
{
    private readonly JsonObject? _definition;

    internal WoundEffectSourceDefinition(
        string definitionKey,
        JsonObject definition)
    {
        DefinitionKey = definitionKey;
        _definition = WoundAcceptedTurnData.CloneObject(definition);
    }

    internal string DefinitionKey { get; }
    internal JsonObject Definition => WoundAcceptedTurnData.CloneObject(_definition)!;
}

internal sealed record WoundEffectSourceSelector(
    string Realm,
    string Kind,
    string? SourceId,
    string? SourceRef,
    string DefinitionKey);

internal sealed record WoundEffectTargetSelector(
    string Kind,
    string? TargetId,
    string? TargetRef);

internal sealed class WoundEffectSourceExport
{
    private readonly WoundOwnerCoordinate? _owner;
    private readonly WoundEffectSourceDefinition[]? _definitions;

    internal WoundEffectSourceExport(
        int schemaVersion,
        string kind,
        string sourceId,
        string sourceRef,
        string state,
        bool materializable,
        string realm,
        WoundOwnerCoordinate owner,
        string causalEventRef,
        string eventSemanticFingerprint,
        string opportunityId,
        string opportunityAuthorityFingerprint,
        IReadOnlyList<WoundEffectSourceDefinition> definitions)
    {
        SchemaVersion = schemaVersion;
        Kind = kind;
        SourceId = sourceId;
        SourceRef = sourceRef;
        State = state;
        Materializable = materializable;
        Realm = realm;
        _owner = WoundAcceptedTurnData.CloneOwner(owner);
        CausalEventRef = causalEventRef;
        EventSemanticFingerprint = eventSemanticFingerprint;
        OpportunityId = opportunityId;
        OpportunityAuthorityFingerprint = opportunityAuthorityFingerprint;
        _definitions = WoundAcceptedTurnData.FreezeList(
            definitions,
            WoundAcceptedTurnData.CloneSourceDefinition);
    }

    internal int SchemaVersion { get; }
    internal string Kind { get; }
    internal string SourceId { get; }
    internal string SourceRef { get; }
    internal string State { get; }
    internal bool Materializable { get; }
    internal string Realm { get; }
    internal WoundOwnerCoordinate Owner => WoundAcceptedTurnData.CloneOwner(_owner)!;
    internal string CausalEventRef { get; }
    internal string EventSemanticFingerprint { get; }
    internal string OpportunityId { get; }
    internal string OpportunityAuthorityFingerprint { get; }

    internal IReadOnlyList<WoundEffectSourceDefinition> Definitions =>
        WoundAcceptedTurnData.CopyList(
            _definitions,
            WoundAcceptedTurnData.CloneSourceDefinition);
}

internal sealed record WoundRootLineageAuthorityRow(
    string? ApplicationRef,
    string? EffectId,
    string DefinitionKey,
    WoundRootOwnershipDomain OwnershipDomain);

internal sealed record WoundEffectSlotAgreement(
    int Slot,
    string ProfileKey,
    string ReadableSummary);

internal static class WoundEffectMaterializationFingerprint
{
    internal const string Domain =
        "book_of_eternity.wound.effect_materialization";
    internal const string Version = "1";

    internal static string Compute(
        EffectSourceKey sourceKey,
        int schemaVersion,
        JsonObject parameters,
        JsonArray materializedComponents)
    {
        ArgumentNullException.ThrowIfNull(sourceKey);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(materializedComponents);
        var fields = new List<string?>
        {
            Domain,
            Version,
            sourceKey.Realm,
            sourceKey.Kind,
            sourceKey.SourceId,
            sourceKey.DefinitionKey,
            schemaVersion.ToString(CultureInfo.InvariantCulture),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(parameters),
            materializedComponents.Count.ToString(CultureInfo.InvariantCulture)
        };
        for (var index = 0; index < materializedComponents.Count; index++)
        {
            fields.Add(index.ToString(CultureInfo.InvariantCulture));
            fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                materializedComponents[index]));
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }
}

internal sealed class WoundRootEffectApplication
{
    private readonly WoundEffectTargetSelector? _targetSelector;
    private readonly EffectTargetKey? _expectedTargetKey;
    private readonly WoundEffectSourceSelector? _sourceSelector;
    private readonly EffectSourceKey? _expectedSourceKey;
    private readonly JsonObject? _parameters;
    private readonly WoundEffectSlotAgreement[]? _slotBindings;
    private readonly WoundRootOwnershipDomain? _ownershipDomain;
    private readonly EffectCarrierCoordinate? _expectedCarrierCoordinate;

    internal WoundRootEffectApplication(
        string applicationRef,
        int mechanicsOrdinal,
        int operationOrdinal,
        string operationKind,
        string operationKey,
        string definitionKey,
        WoundEffectTargetSelector targetSelector,
        EffectTargetKey expectedTargetKey,
        WoundEffectSourceSelector sourceSelector,
        EffectSourceKey expectedSourceKey,
        JsonObject parameters,
        IReadOnlyList<WoundEffectSlotAgreement> slotBindings,
        int expectedComponentCount,
        string expectedMaterializationFingerprint,
        WoundRootOwnershipDomain ownershipDomain,
        string causalEventRef,
        EffectCarrierCoordinate expectedCarrierCoordinate)
    {
        ApplicationRef = applicationRef;
        MechanicsOrdinal = mechanicsOrdinal;
        OperationOrdinal = operationOrdinal;
        OperationKind = operationKind;
        OperationKey = operationKey;
        DefinitionKey = definitionKey;
        _targetSelector = WoundAcceptedTurnData.CloneTargetSelector(targetSelector);
        _expectedTargetKey = WoundAcceptedTurnData.CloneTargetKey(expectedTargetKey);
        _sourceSelector = WoundAcceptedTurnData.CloneSourceSelector(sourceSelector);
        _expectedSourceKey = WoundAcceptedTurnData.CloneSourceKey(expectedSourceKey);
        _parameters = WoundAcceptedTurnData.CloneObject(parameters);
        _slotBindings = WoundAcceptedTurnData.FreezeList(
            slotBindings,
            WoundAcceptedTurnData.CloneSlotAgreement);
        ExpectedComponentCount = expectedComponentCount;
        ExpectedMaterializationFingerprint = expectedMaterializationFingerprint;
        _ownershipDomain = WoundAcceptedTurnData.CloneOwnershipDomain(ownershipDomain);
        CausalEventRef = causalEventRef;
        _expectedCarrierCoordinate =
            WoundAcceptedTurnData.CloneEffectCarrierCoordinate(expectedCarrierCoordinate);
    }

    internal string ApplicationRef { get; }
    internal int MechanicsOrdinal { get; }
    internal int OperationOrdinal { get; }
    internal string OperationKind { get; }
    internal string OperationKey { get; }
    internal string DefinitionKey { get; }
    internal WoundEffectTargetSelector TargetSelector =>
        WoundAcceptedTurnData.CloneTargetSelector(_targetSelector)!;
    internal EffectTargetKey ExpectedTargetKey =>
        WoundAcceptedTurnData.CloneTargetKey(_expectedTargetKey)!;
    internal WoundEffectSourceSelector SourceSelector =>
        WoundAcceptedTurnData.CloneSourceSelector(_sourceSelector)!;
    internal EffectSourceKey ExpectedSourceKey =>
        WoundAcceptedTurnData.CloneSourceKey(_expectedSourceKey)!;
    internal JsonObject Parameters => WoundAcceptedTurnData.CloneObject(_parameters)!;
    internal IReadOnlyList<WoundEffectSlotAgreement> SlotBindings =>
        WoundAcceptedTurnData.CopyList(
            _slotBindings,
            WoundAcceptedTurnData.CloneSlotAgreement);
    internal int ExpectedComponentCount { get; }
    internal string ExpectedMaterializationFingerprint { get; }
    internal WoundRootOwnershipDomain OwnershipDomain =>
        WoundAcceptedTurnData.CloneOwnershipDomain(_ownershipDomain)!;
    internal string CausalEventRef { get; }
    internal EffectCarrierCoordinate ExpectedCarrierCoordinate =>
        WoundAcceptedTurnData.CloneEffectCarrierCoordinate(
            _expectedCarrierCoordinate)!;
}

internal sealed class WoundTerminalEffectOperation
{
    private readonly EffectSourceKey? _expectedSourceKey;
    private readonly EffectTargetKey? _expectedTargetKey;
    private readonly EffectCarrierCoordinate? _expectedCarrierCoordinate;
    private readonly EffectIdentityOwner? _expectedIdentityOwner;
    private readonly EffectStackCoordinate? _expectedStackCoordinate;
    private readonly WoundRootOwnershipDomain? _ownershipDomain;

    internal WoundTerminalEffectOperation(
        string operationRef,
        string operationKey,
        string effectId,
        int mechanicsOrdinal,
        int operationOrdinal,
        string operationKind,
        string causalEventRef,
        EffectSourceKey expectedSourceKey,
        EffectTargetKey expectedTargetKey,
        EffectCarrierCoordinate expectedCarrierCoordinate,
        string expectedCarrierFilePath,
        string expectedCarrierJsonPath,
        EffectIdentityOwner expectedIdentityOwner,
        EffectStackCoordinate expectedStackCoordinate,
        string expectedEffectFingerprint,
        string expectedIdentityFingerprint,
        WoundRootOwnershipDomain ownershipDomain)
    {
        OperationRef = operationRef;
        OperationKey = operationKey;
        EffectId = effectId;
        MechanicsOrdinal = mechanicsOrdinal;
        OperationOrdinal = operationOrdinal;
        OperationKind = operationKind;
        CausalEventRef = causalEventRef;
        _expectedSourceKey = WoundAcceptedTurnData.CloneSourceKey(
            expectedSourceKey);
        _expectedTargetKey = WoundAcceptedTurnData.CloneTargetKey(
            expectedTargetKey);
        _expectedCarrierCoordinate =
            WoundAcceptedTurnData.CloneEffectCarrierCoordinate(
                expectedCarrierCoordinate);
        ExpectedCarrierFilePath = expectedCarrierFilePath;
        ExpectedCarrierJsonPath = expectedCarrierJsonPath;
        _expectedIdentityOwner = expectedIdentityOwner with { };
        _expectedStackCoordinate = expectedStackCoordinate with { };
        ExpectedEffectFingerprint = expectedEffectFingerprint;
        ExpectedIdentityFingerprint = expectedIdentityFingerprint;
        _ownershipDomain = WoundAcceptedTurnData.CloneOwnershipDomain(
            ownershipDomain);
    }

    internal string OperationRef { get; }
    internal string OperationKey { get; }
    internal string EffectId { get; }
    internal int MechanicsOrdinal { get; }
    internal int OperationOrdinal { get; }
    internal string OperationKind { get; }
    internal string CausalEventRef { get; }
    internal EffectSourceKey ExpectedSourceKey =>
        WoundAcceptedTurnData.CloneSourceKey(_expectedSourceKey)!;
    internal EffectTargetKey ExpectedTargetKey =>
        WoundAcceptedTurnData.CloneTargetKey(_expectedTargetKey)!;
    internal EffectCarrierCoordinate ExpectedCarrierCoordinate =>
        WoundAcceptedTurnData.CloneEffectCarrierCoordinate(
            _expectedCarrierCoordinate)!;
    internal string ExpectedCarrierFilePath { get; }
    internal string ExpectedCarrierJsonPath { get; }
    internal EffectIdentityOwner ExpectedIdentityOwner =>
        _expectedIdentityOwner! with { };
    internal EffectStackCoordinate ExpectedStackCoordinate =>
        _expectedStackCoordinate! with { };
    internal string ExpectedEffectFingerprint { get; }
    internal string ExpectedIdentityFingerprint { get; }
    internal WoundRootOwnershipDomain OwnershipDomain =>
        WoundAcceptedTurnData.CloneOwnershipDomain(_ownershipDomain)!;
}

internal sealed record WoundPreparedTransitionAuthority(
    string PreparedInputFingerprint,
    string OpportunityId,
    string OpportunityAuthorityFingerprint,
    string OperationKey,
    string ReadableSummary,
    int MaximumSeverityRank,
    string TransitionKind,
    string? CauseKind,
    string? ExpectedBeforeFingerprint,
    string AuthoritySeal);

internal sealed class WoundEffectOperationBatch
{
    private readonly WoundEffectSourceExport? _sourceExport;
    private readonly WoundRootEffectApplication[]? _rootApplications;
    private readonly WoundTerminalEffectOperation[]? _terminalOperations;
    private readonly WoundRootLineageAuthorityRow[]? _rootLineageAuthority;
    private readonly WoundPreparedTransitionAuthority? _transitionAuthority;

    internal WoundEffectOperationBatch(
        string localWoundRef,
        string preparedWoundId,
        WoundEffectSourceExport sourceExport,
        IReadOnlyList<WoundRootEffectApplication> rootApplications,
        IReadOnlyList<WoundTerminalEffectOperation> terminalOperations,
        IReadOnlyList<WoundRootLineageAuthorityRow> rootLineageAuthority,
        string sourceExportFingerprint,
        WoundPreparedTransitionAuthority transitionAuthority)
    {
        LocalWoundRef = localWoundRef;
        PreparedWoundId = preparedWoundId;
        _sourceExport = WoundAcceptedTurnData.CloneSourceExport(sourceExport);
        _rootApplications = WoundAcceptedTurnData.FreezeList(
            rootApplications,
            WoundAcceptedTurnData.CloneRootEffectApplication);
        _terminalOperations = WoundAcceptedTurnData.FreezeList(
            terminalOperations,
            WoundAcceptedTurnData.CloneTerminalOperation);
        _rootLineageAuthority = WoundAcceptedTurnData.FreezeList(
            rootLineageAuthority,
            WoundAcceptedTurnData.CloneLineageRow);
        SourceExportFingerprint = sourceExportFingerprint;
        _transitionAuthority =
            WoundAcceptedTurnData.CloneTransitionAuthority(transitionAuthority);
    }

    internal string LocalWoundRef { get; }
    internal string PreparedWoundId { get; }
    internal WoundEffectSourceExport SourceExport =>
        WoundAcceptedTurnData.CloneSourceExport(_sourceExport)!;
    internal IReadOnlyList<WoundRootEffectApplication> RootApplications =>
        WoundAcceptedTurnData.CopyList(
            _rootApplications,
            WoundAcceptedTurnData.CloneRootEffectApplication);
    internal IReadOnlyList<WoundTerminalEffectOperation> TerminalOperations =>
        WoundAcceptedTurnData.CopyList(
            _terminalOperations,
            WoundAcceptedTurnData.CloneTerminalOperation);
    internal IReadOnlyList<WoundRootLineageAuthorityRow> RootLineageAuthority =>
        WoundAcceptedTurnData.CopyList(
            _rootLineageAuthority,
            WoundAcceptedTurnData.CloneLineageRow);
    internal string SourceExportFingerprint { get; }
    internal WoundPreparedTransitionAuthority TransitionAuthority =>
        WoundAcceptedTurnData.CloneTransitionAuthority(_transitionAuthority)!;
}

internal sealed class WoundPreparedBaselineAuthority
{
    private readonly WoundCarrierCatalogInput? _preTurnCarriers;
    private readonly JsonObject? _preTurnIdentityIndex;
    private readonly JsonObject? _preTurnHistory;

    internal WoundPreparedBaselineAuthority(
        string preparedInputFingerprint,
        WoundCarrierCatalogInput preTurnCarriers,
        JsonObject preTurnIdentityIndex,
        JsonObject preTurnHistory,
        string authoritySeal)
    {
        PreparedInputFingerprint = preparedInputFingerprint;
        _preTurnCarriers = WoundAcceptedTurnData.CloneWoundCarriers(preTurnCarriers);
        _preTurnIdentityIndex = WoundAcceptedTurnData.CloneObject(preTurnIdentityIndex);
        _preTurnHistory = WoundAcceptedTurnData.CloneObject(preTurnHistory);
        AuthoritySeal = authoritySeal;
    }

    internal string PreparedInputFingerprint { get; }
    internal WoundCarrierCatalogInput PreTurnCarriers =>
        WoundAcceptedTurnData.CloneWoundCarriers(_preTurnCarriers)!;
    internal JsonObject PreTurnIdentityIndex =>
        WoundAcceptedTurnData.CloneObject(_preTurnIdentityIndex)!;
    internal JsonObject PreTurnHistory =>
        WoundAcceptedTurnData.CloneObject(_preTurnHistory)!;
    internal string AuthoritySeal { get; }
}

internal sealed class WoundPreparedAcceptedTurnPlan
{
    private readonly WoundAcceptedTurnBinding? _binding;
    private readonly string[]? _allocatedWoundIds;
    private readonly string[]? _allocatedTransitionIds;
    private readonly WoundMaterializationEnvelope[]? _preparedWounds;
    private readonly WoundEffectOperationBatch[]? _effectOperationBatches;
    private readonly WoundPreparedBaselineAuthority? _baselineAuthority;
    private readonly object? _cacheAuthorityStateToken;
    private readonly object? _cachePreparedStageToken;

    internal WoundPreparedAcceptedTurnPlan(
        WoundAcceptedTurnBinding binding,
        string bindingFingerprint,
        string inputFingerprint,
        string woundPreparationFingerprint,
        IReadOnlyList<string> allocatedWoundIds,
        IReadOnlyList<string> allocatedTransitionIds,
        IReadOnlyList<WoundMaterializationEnvelope> preparedWounds,
        IReadOnlyList<WoundEffectOperationBatch> effectOperationBatches,
        WoundPreparedBaselineAuthority baselineAuthority,
        object? cacheAuthorityStateToken = null,
        object? cachePreparedStageToken = null)
    {
        _binding = WoundAcceptedTurnData.CloneBinding(binding);
        BindingFingerprint = bindingFingerprint;
        InputFingerprint = inputFingerprint;
        WoundPreparationFingerprint = woundPreparationFingerprint;
        _allocatedWoundIds = WoundAcceptedTurnData.FreezeList(allocatedWoundIds);
        _allocatedTransitionIds = WoundAcceptedTurnData.FreezeList(
            allocatedTransitionIds);
        _preparedWounds = WoundAcceptedTurnData.FreezeList(
            preparedWounds,
            static wound => WoundAcceptedTurnData.CloneWound(wound)!);
        _effectOperationBatches = WoundAcceptedTurnData.FreezeList(
            effectOperationBatches,
            WoundAcceptedTurnData.CloneOperationBatch);
        _baselineAuthority =
            WoundAcceptedTurnData.CloneBaselineAuthority(baselineAuthority);
        _cacheAuthorityStateToken = cacheAuthorityStateToken;
        _cachePreparedStageToken = cachePreparedStageToken;
    }

    internal WoundAcceptedTurnBinding Binding =>
        WoundAcceptedTurnData.CloneBinding(_binding)!;
    internal string BindingFingerprint { get; }
    internal string InputFingerprint { get; }
    internal string WoundPreparationFingerprint { get; }
    internal IReadOnlyList<string> AllocatedWoundIds =>
        WoundAcceptedTurnData.CopyList(_allocatedWoundIds);
    internal IReadOnlyList<string> AllocatedTransitionIds =>
        WoundAcceptedTurnData.CopyList(_allocatedTransitionIds);
    internal IReadOnlyList<WoundMaterializationEnvelope> PreparedWounds =>
        WoundAcceptedTurnData.CopyList(
            _preparedWounds,
            static wound => WoundAcceptedTurnData.CloneWound(wound)!);
    internal IReadOnlyList<WoundEffectOperationBatch> EffectOperationBatches =>
        WoundAcceptedTurnData.CopyList(
            _effectOperationBatches,
            WoundAcceptedTurnData.CloneOperationBatch);
    internal WoundPreparedBaselineAuthority BaselineAuthority =>
        WoundAcceptedTurnData.CloneBaselineAuthority(_baselineAuthority)!;

    internal WoundPreparedAcceptedTurnPlan BindToCacheAuthority(
        object authorityStateToken,
        object preparedStageToken)
    {
        ArgumentNullException.ThrowIfNull(authorityStateToken);
        ArgumentNullException.ThrowIfNull(preparedStageToken);
        return new WoundPreparedAcceptedTurnPlan(
            Binding,
            BindingFingerprint,
            InputFingerprint,
            WoundPreparationFingerprint,
            AllocatedWoundIds,
            AllocatedTransitionIds,
            PreparedWounds,
            EffectOperationBatches,
            BaselineAuthority,
            authorityStateToken,
            preparedStageToken);
    }

    internal WoundPreparedAcceptedTurnPlan ClonePreservingCacheAuthority() =>
        new(
            Binding,
            BindingFingerprint,
            InputFingerprint,
            WoundPreparationFingerprint,
            AllocatedWoundIds,
            AllocatedTransitionIds,
            PreparedWounds,
            EffectOperationBatches,
            BaselineAuthority,
            _cacheAuthorityStateToken,
            _cachePreparedStageToken);

    internal bool HasCacheAuthorityState => _cacheAuthorityStateToken is not null;

    internal bool BelongsToCacheAuthorityState(object authorityStateToken) =>
        ReferenceEquals(_cacheAuthorityStateToken, authorityStateToken);

    internal bool BelongsToPreparedStage(
        object authorityStateToken,
        object preparedStageToken) =>
        ReferenceEquals(_cacheAuthorityStateToken, authorityStateToken) &&
        ReferenceEquals(_cachePreparedStageToken, preparedStageToken);
}

internal sealed record WoundAcceptedTurnPreparationResult
{
    private readonly WoundPreparedAcceptedTurnPlan? _plan;
    private readonly ValidationIssue[]? _issues;

    internal WoundAcceptedTurnPreparationResult(
        WoundPreparedAcceptedTurnPlan? plan,
        IReadOnlyList<ValidationIssue> issues)
    {
        _plan = WoundAcceptedTurnData.ClonePreparedPlan(plan);
        _issues = WoundAcceptedTurnData.FreezeList(
            issues,
            WoundAcceptedTurnData.CloneIssue);
    }

    internal WoundPreparedAcceptedTurnPlan? Plan =>
        WoundAcceptedTurnData.ClonePreparedPlan(_plan);
    internal IReadOnlyList<ValidationIssue> Issues =>
        WoundAcceptedTurnData.CopyList(
            _issues,
            WoundAcceptedTurnData.CloneIssue);
    internal bool Success => Plan is not null && Issues is { Count: 0 };
}

internal sealed record WoundEffectMaterializationAgreement
{
    private WoundEffectSlotAgreement[]? _slotBindings;

    internal WoundEffectMaterializationAgreement(
        IReadOnlyList<WoundEffectSlotAgreement> slotBindings,
        int componentCount,
        string materializationFingerprint)
    {
        SlotBindings = slotBindings;
        ComponentCount = componentCount;
        MaterializationFingerprint = materializationFingerprint;
    }

    internal IReadOnlyList<WoundEffectSlotAgreement> SlotBindings
    {
        get => WoundAcceptedTurnData.CopyList(
            _slotBindings,
            WoundAcceptedTurnData.CloneSlotAgreement);
        init => _slotBindings = WoundAcceptedTurnData.FreezeList(
            value,
            WoundAcceptedTurnData.CloneSlotAgreement);
    }

    internal int ComponentCount { get; init; }
    internal string MaterializationFingerprint { get; init; }
}

internal sealed record EffectAcceptedApplicationResult(
    string ApplicationRef,
    string Disposition,
    string EffectId,
    string CreateTransitionId,
    string CreatedEventRef,
    string CausalEventRef,
    EffectSourceKey SourceKey,
    EffectTargetKey TargetKey,
    EffectCarrierCoordinate CarrierCoordinate,
    WoundEffectMaterializationAgreement Materialization);

internal sealed record EffectAcceptedTerminationResult(
    string OperationRef,
    string Disposition,
    string EffectId,
    string TerminalTransitionId,
    string TransitionEventRef,
    string CausalEventRef,
    EffectSourceKey SourceKey,
    EffectTargetKey TargetKey,
    EffectCarrierCoordinate CarrierCoordinate);

internal sealed class WoundEffectBatchAcceptedPlan
{
    private readonly EffectAcceptedTurnInput _effectInput;
    private readonly EffectAcceptedTurnPlan _effectPlan;
    private readonly EffectAcceptedApplicationResult[]? _applicationResults;
    private readonly EffectAcceptedTerminationResult[]? _terminationResults;
    private readonly object? _cacheAuthorityStateToken;
    private readonly object? _cacheEffectStageToken;

    private WoundEffectBatchAcceptedPlan(
        EffectAcceptedTurnInput effectInput,
        EffectAcceptedTurnPlan effectPlan,
        string woundPreparationFingerprint,
        string effectInputFingerprint,
        string effectAcceptedTurnPlanFingerprint,
        IReadOnlyList<EffectAcceptedApplicationResult> applicationResults,
        IReadOnlyList<EffectAcceptedTerminationResult> terminationResults,
        object? cacheAuthorityStateToken = null,
        object? cacheEffectStageToken = null)
    {
        _effectInput = WoundAcceptedTurnData.CloneEffectInput(effectInput);
        _effectPlan = WoundAcceptedTurnData.CloneEffectPlan(effectPlan);
        WoundPreparationFingerprint = woundPreparationFingerprint;
        EffectInputFingerprint = effectInputFingerprint;
        EffectAcceptedTurnPlanFingerprint = effectAcceptedTurnPlanFingerprint;
        _applicationResults = WoundAcceptedTurnData.FreezeList(
            applicationResults,
            WoundAcceptedTurnData.CloneApplicationResult);
        _terminationResults = WoundAcceptedTurnData.FreezeList(
            terminationResults,
            WoundAcceptedTurnData.CloneTerminationResult);
        _cacheAuthorityStateToken = cacheAuthorityStateToken;
        _cacheEffectStageToken = cacheEffectStageToken;
    }

    internal EffectAcceptedTurnInput EffectInput =>
        WoundAcceptedTurnData.CloneEffectInput(_effectInput);
    internal EffectAcceptedTurnPlan EffectPlan =>
        WoundAcceptedTurnData.CloneEffectPlan(_effectPlan);
    internal string WoundPreparationFingerprint { get; }
    internal string EffectInputFingerprint { get; }
    internal string EffectAcceptedTurnPlanFingerprint { get; }
    internal IReadOnlyList<EffectAcceptedApplicationResult> ApplicationResults =>
        WoundAcceptedTurnData.CopyList(
            _applicationResults,
            WoundAcceptedTurnData.CloneApplicationResult);
    internal IReadOnlyList<EffectAcceptedTerminationResult> TerminationResults =>
        WoundAcceptedTurnData.CopyList(
            _terminationResults,
            WoundAcceptedTurnData.CloneTerminationResult);

    internal WoundEffectBatchAcceptedPlan DetachedCopy() => new(
        _effectInput,
        _effectPlan,
        WoundPreparationFingerprint,
        EffectInputFingerprint,
        EffectAcceptedTurnPlanFingerprint,
        _applicationResults!,
        _terminationResults!,
        _cacheAuthorityStateToken,
        _cacheEffectStageToken);

    internal WoundEffectBatchAcceptedPlan BindToCacheAuthority(
        object authorityStateToken,
        object effectStageToken)
    {
        ArgumentNullException.ThrowIfNull(authorityStateToken);
        ArgumentNullException.ThrowIfNull(effectStageToken);
        return new WoundEffectBatchAcceptedPlan(
            _effectInput,
            _effectPlan,
            WoundPreparationFingerprint,
            EffectInputFingerprint,
            EffectAcceptedTurnPlanFingerprint,
            _applicationResults!,
            _terminationResults!,
            authorityStateToken,
            effectStageToken);
    }

    internal bool HasCacheAuthorityState => _cacheAuthorityStateToken is not null;

    internal bool BelongsToEffectStage(
        object authorityStateToken,
        object effectStageToken) =>
        ReferenceEquals(_cacheAuthorityStateToken, authorityStateToken) &&
        ReferenceEquals(_cacheEffectStageToken, effectStageToken);

    internal static WoundEffectBatchAcceptedPlan Create(
        WoundPreparedAcceptedTurnPlan prepared,
        EffectAcceptedTurnInput effectInput,
        EffectAcceptedTurnPlan effectPlan,
        IReadOnlyList<EffectAcceptedApplicationResult> applicationResults,
        IReadOnlyList<EffectAcceptedTerminationResult> terminationResults)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(effectInput);
        ArgumentNullException.ThrowIfNull(effectPlan);
        ArgumentNullException.ThrowIfNull(applicationResults);
        ArgumentNullException.ThrowIfNull(terminationResults);
        return new WoundEffectBatchAcceptedPlan(
            effectInput,
            effectPlan,
            WoundAcceptedTurnFingerprints.ComputePreparation(prepared),
            WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, effectInput),
            WoundAcceptedTurnFingerprints.ComputeEffectPlan(
                prepared,
                effectInput,
                effectPlan,
                applicationResults,
                terminationResults),
            applicationResults,
            terminationResults);
    }

    internal static WoundEffectBatchPlanningResult AcceptCandidate(
        WoundPreparedAcceptedTurnPlan prepared,
        EffectAcceptedTurnInput effectInput,
        EffectAcceptedTurnPlan effectPlan,
        IReadOnlyList<EffectAcceptedApplicationResult> applicationResults,
        IReadOnlyList<EffectAcceptedTerminationResult> terminationResults,
        string woundPreparationFingerprint,
        string effectInputFingerprint,
        string effectAcceptedTurnPlanFingerprint)
    {
        if (prepared is null ||
            effectInput is null ||
            effectPlan is null ||
            applicationResults is null ||
            terminationResults is null)
        {
            return InvalidCandidate(
                "wound_plan_effect_handoff_invalid",
                "complete detached wound/effect candidate",
                "missing candidate member");
        }

        try
        {
            var actualPreparation =
                WoundAcceptedTurnFingerprints.ComputePreparation(prepared);
            if (!string.Equals(
                    actualPreparation,
                    prepared.WoundPreparationFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    actualPreparation,
                    woundPreparationFingerprint,
                    StringComparison.Ordinal))
            {
                return InvalidCandidate(
                    "wound_plan_prepared_seal_mismatch",
                    actualPreparation,
                    woundPreparationFingerprint);
            }

            var actualEffectInput = WoundAcceptedTurnFingerprints.ComputeEffectInput(
                prepared,
                effectInput);
            if (!string.Equals(
                    actualEffectInput,
                    effectInputFingerprint,
                    StringComparison.Ordinal))
            {
                return InvalidCandidate(
                    "wound_plan_effect_handoff_invalid",
                    actualEffectInput,
                    effectInputFingerprint);
            }

            var actualEffectPlan = WoundAcceptedTurnFingerprints.ComputeEffectPlan(
                prepared,
                effectInput,
                effectPlan,
                applicationResults,
                terminationResults);
            if (!string.Equals(
                    actualEffectPlan,
                    effectAcceptedTurnPlanFingerprint,
                    StringComparison.Ordinal))
            {
                return InvalidCandidate(
                    "wound_plan_effect_handoff_invalid",
                    actualEffectPlan,
                    effectAcceptedTurnPlanFingerprint);
            }

            return new WoundEffectBatchPlanningResult(
                new WoundEffectBatchAcceptedPlan(
                    effectInput,
                    effectPlan,
                    actualPreparation,
                    actualEffectInput,
                    actualEffectPlan,
                    applicationResults,
                    terminationResults),
                Array.Empty<ValidationIssue>());
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or
                JsonException or NullReferenceException)
        {
            return InvalidCandidate(
                "wound_plan_effect_handoff_invalid",
                "well-formed detached wound/effect candidate",
                exception.GetType().Name);
        }
    }

    private static WoundEffectBatchPlanningResult InvalidCandidate(
        string code,
        string? expected,
        string? actual) =>
        new(
            null,
            new[]
            {
                new ValidationIssue(
                    EffectAcceptedTurnPlan.CommandPath,
                    IssueSeverity.Error,
                    "The wound effect-stage candidate did not match its sealed authority.",
                    code,
                    actor: "wound_planner",
                    section: "effectChanges",
                    expected: expected,
                    actual: actual,
                    repairHint: "Rebuild the accepted wound effect stage from the sealed preparation.",
                    repairTargetFiles: new[] { EffectAcceptedTurnPlan.CommandPath })
            });
}

internal sealed record WoundEffectBatchPlanningResult
{
    private readonly WoundEffectBatchAcceptedPlan? _plan;
    private readonly ValidationIssue[]? _issues;

    internal WoundEffectBatchPlanningResult(
        WoundEffectBatchAcceptedPlan? plan,
        IReadOnlyList<ValidationIssue> issues)
    {
        _plan = WoundAcceptedTurnData.CloneEffectBatchPlan(plan);
        _issues = WoundAcceptedTurnData.FreezeList(
            issues,
            WoundAcceptedTurnData.CloneIssue);
    }

    internal WoundEffectBatchAcceptedPlan? Plan =>
        WoundAcceptedTurnData.CloneEffectBatchPlan(_plan);
    internal IReadOnlyList<ValidationIssue> Issues =>
        WoundAcceptedTurnData.CopyList(
            _issues,
            WoundAcceptedTurnData.CloneIssue);
    internal bool Success => Plan is not null && Issues is { Count: 0 };
}

internal sealed class WoundCarrierMutation
{
    private readonly WoundMaterializationEnvelope? _beforeWound;
    private readonly WoundMaterializationEnvelope? _afterWound;

    internal WoundCarrierMutation(
        string operation,
        string woundId,
        WoundMaterializationEnvelope? beforeWound,
        WoundMaterializationEnvelope? afterWound)
    {
        Operation = operation;
        WoundId = woundId;
        _beforeWound = WoundAcceptedTurnData.CloneWound(beforeWound);
        _afterWound = WoundAcceptedTurnData.CloneWound(afterWound);
    }

    internal string Operation { get; }
    internal string WoundId { get; }
    internal WoundMaterializationEnvelope? BeforeWound =>
        WoundAcceptedTurnData.CloneWound(_beforeWound);
    internal WoundMaterializationEnvelope? AfterWound =>
        WoundAcceptedTurnData.CloneWound(_afterWound);
}

internal sealed class WoundCarrierContribution
{
    private readonly WoundOwnerCoordinate? _owner;
    private readonly WoundCarrierMutation[]? _mutations;

    internal WoundCarrierContribution(
        WoundOwnerCoordinate owner,
        string expectedWoundCollectionFingerprint,
        IReadOnlyList<WoundCarrierMutation> mutations)
    {
        _owner = WoundAcceptedTurnData.CloneOwner(owner);
        ExpectedWoundCollectionFingerprint = expectedWoundCollectionFingerprint;
        _mutations = WoundAcceptedTurnData.FreezeList(
            mutations,
            WoundAcceptedTurnData.CloneCarrierMutation);
    }

    internal WoundOwnerCoordinate Owner =>
        WoundAcceptedTurnData.CloneOwner(_owner)!;
    internal string ExpectedWoundCollectionFingerprint { get; }
    internal IReadOnlyList<WoundCarrierMutation> Mutations =>
        WoundAcceptedTurnData.CopyList(
            _mutations,
            WoundAcceptedTurnData.CloneCarrierMutation);
}

internal sealed class WoundAcceptedTurnPlan
{
    private readonly WoundAcceptedTurnBinding? _binding;
    private readonly string[]? _allocatedWoundIds;
    private readonly string[]? _allocatedTransitionIds;
    private readonly WoundCarrierContribution[]? _carrierContributions;
    private readonly JsonObject? _identityIndexAfterImage;
    private readonly JsonObject? _historyAfterImage;
    private readonly WoundTransitionIntent[]? _transitionIntents;

    internal WoundAcceptedTurnPlan(
        WoundAcceptedTurnBinding binding,
        string bindingFingerprint,
        string inputFingerprint,
        string woundPreparationFingerprint,
        string effectInputFingerprint,
        string effectAcceptedTurnPlanFingerprint,
        string woundFinalPlanFingerprint,
        IReadOnlyList<string> allocatedWoundIds,
        IReadOnlyList<string> allocatedTransitionIds,
        IReadOnlyList<WoundCarrierContribution> carrierContributions,
        JsonObject identityIndexAfterImage,
        JsonObject historyAfterImage,
        IReadOnlyList<WoundTransitionIntent> transitionIntents)
    {
        _binding = WoundAcceptedTurnData.CloneBinding(binding);
        BindingFingerprint = bindingFingerprint;
        InputFingerprint = inputFingerprint;
        WoundPreparationFingerprint = woundPreparationFingerprint;
        EffectInputFingerprint = effectInputFingerprint;
        EffectAcceptedTurnPlanFingerprint = effectAcceptedTurnPlanFingerprint;
        WoundFinalPlanFingerprint = woundFinalPlanFingerprint;
        _allocatedWoundIds = WoundAcceptedTurnData.FreezeList(allocatedWoundIds);
        _allocatedTransitionIds = WoundAcceptedTurnData.FreezeList(
            allocatedTransitionIds);
        _carrierContributions = WoundAcceptedTurnData.FreezeList(
            carrierContributions,
            WoundAcceptedTurnData.CloneCarrierContribution);
        _identityIndexAfterImage =
            WoundAcceptedTurnData.CloneObject(identityIndexAfterImage);
        _historyAfterImage = WoundAcceptedTurnData.CloneObject(historyAfterImage);
        _transitionIntents = WoundAcceptedTurnData.FreezeList(
            transitionIntents,
            WoundAcceptedTurnData.CloneTransitionIntent);
    }

    internal WoundAcceptedTurnBinding Binding =>
        WoundAcceptedTurnData.CloneBinding(_binding)!;
    internal string BindingFingerprint { get; }
    internal string InputFingerprint { get; }
    internal string WoundPreparationFingerprint { get; }
    internal string EffectInputFingerprint { get; }
    internal string EffectAcceptedTurnPlanFingerprint { get; }
    internal string WoundFinalPlanFingerprint { get; }
    internal IReadOnlyList<string> AllocatedWoundIds =>
        WoundAcceptedTurnData.CopyList(_allocatedWoundIds);
    internal IReadOnlyList<string> AllocatedTransitionIds =>
        WoundAcceptedTurnData.CopyList(_allocatedTransitionIds);
    internal IReadOnlyList<WoundCarrierContribution> CarrierContributions =>
        WoundAcceptedTurnData.CopyList(
            _carrierContributions,
            WoundAcceptedTurnData.CloneCarrierContribution);
    internal JsonObject IdentityIndexAfterImage =>
        WoundAcceptedTurnData.CloneObject(_identityIndexAfterImage)!;
    internal JsonObject HistoryAfterImage =>
        WoundAcceptedTurnData.CloneObject(_historyAfterImage)!;
    internal IReadOnlyList<WoundTransitionIntent> TransitionIntents =>
        WoundAcceptedTurnData.CopyList(
            _transitionIntents,
            WoundAcceptedTurnData.CloneTransitionIntent);
}

internal sealed record WoundAcceptedTurnPlanningResult
{
    private readonly WoundAcceptedTurnPlan? _plan;
    private readonly ValidationIssue[]? _issues;

    internal WoundAcceptedTurnPlanningResult(
        WoundAcceptedTurnPlan? plan,
        IReadOnlyList<ValidationIssue> issues)
    {
        _plan = WoundAcceptedTurnData.CloneFinalPlan(plan);
        _issues = WoundAcceptedTurnData.FreezeList(
            issues,
            WoundAcceptedTurnData.CloneIssue);
    }

    internal WoundAcceptedTurnPlan? Plan =>
        WoundAcceptedTurnData.CloneFinalPlan(_plan);
    internal IReadOnlyList<ValidationIssue> Issues =>
        WoundAcceptedTurnData.CopyList(
            _issues,
            WoundAcceptedTurnData.CloneIssue);
    internal bool Success => Plan is not null && Issues is { Count: 0 };
}

internal static class WoundAcceptedTurnData
{
    internal static T[]? FreezeList<T>(IReadOnlyList<T>? values) =>
        FreezeList(values, static value => value);

    internal static T[]? FreezeList<T>(
        IReadOnlyList<T>? values,
        Func<T, T> clone)
    {
        if (values is null)
            return null;
        var result = new T[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            var value = values[index];
            result[index] = value is null ? value! : clone(value);
        }
        return result;
    }

    internal static IReadOnlyList<T> CopyList<T>(T[]? values) =>
        CopyList(values, static value => value);

    internal static IReadOnlyList<T> CopyList<T>(
        T[]? values,
        Func<T, T> clone)
    {
        if (values is null)
            return null!;
        var result = new T[values.Length];
        for (var index = 0; index < values.Length; index++)
        {
            var value = values[index];
            result[index] = value is null ? value! : clone(value);
        }
        return new ReadOnlyCollection<T>(result);
    }

    internal static JsonObject? CloneObject(JsonObject? value) =>
        value?.DeepClone().AsObject();

    internal static JsonArray? CloneArray(JsonArray? value) =>
        value?.DeepClone().AsArray();

    internal static WoundOwnerCoordinate? CloneOwner(WoundOwnerCoordinate? value) =>
        value is null
            ? null
            : new WoundOwnerCoordinate(
                value.Realm,
                value.OwnerKind,
                value.OwnerId,
                value.CarrierPath);

    internal static WoundAcceptedTurnBinding? CloneBinding(
        WoundAcceptedTurnBinding? value) =>
        value is null
            ? null
            : new WoundAcceptedTurnBinding(
                value.SessionId,
                value.RequestId,
                value.SnapshotToken,
                value.Realm,
                value.Turn,
                value.AcceptedEvents,
                value.AcceptedEventsFingerprint);

    internal static WoundAcceptedTurnInput? CloneInput(
        WoundAcceptedTurnInput? value) =>
        value is null
            ? null
            : new WoundAcceptedTurnInput(
                value.Binding,
                value.Opportunities,
                value.Transitions,
                value.PreTurnCarriers,
                value.PreTurnIdentityIndex,
                value.PreTurnHistory,
                value.PreTurnEffectCarriers,
                value.PreTurnEffectIdentityIndex);

    internal static WoundOpportunityAuthority CloneOpportunity(
        WoundOpportunityAuthority value) =>
        new(
            value.SessionId,
            value.RequestId,
            value.SnapshotToken,
            value.OpportunityId,
            value.PublicRef,
            value.EventRef,
            value.EventKind,
            value.EventAuthorityId,
            value.AcceptedEventsFingerprint,
            CloneOwner(value.Owner)!,
            value.Domain,
            value.ProfileKey,
            value.SourceKind,
            value.SourceId,
            value.SourceState,
            value.MinimumSeverityRank,
            value.MaximumSeverityRank,
            WoundOpportunityAuthority.CloneGuarantee(value.GuaranteedTrigger),
            WoundOpportunityAuthority.CloneSafeContext(value.SafeContext),
            value.InputEvidenceFingerprint,
            value.AuthorityFingerprint)
        {
            WorseningTarget = WoundOpportunityAuthority.CloneWorseningTarget(
                value.WorseningTarget)
        };

    internal static WoundAcceptedEffectDefinitionDraft CloneDefinitionDraft(
        WoundAcceptedEffectDefinitionDraft value) =>
        new(value.LocalEffectRef, value.Definition);

    internal static WoundRootOwnershipDomain? CloneOwnershipDomain(
        WoundRootOwnershipDomain? value) =>
        value is null ? null : new(value.Kind, value.ComplicationId);

    internal static WoundAcceptedRootApplicationDraft CloneRootApplicationDraft(
        WoundAcceptedRootApplicationDraft value) =>
        value with { OwnershipDomain = CloneOwnershipDomain(value.OwnershipDomain)! };

    internal static WoundAcceptedConsequenceSlotBinding CloneConsequenceSlotBinding(
        WoundAcceptedConsequenceSlotBinding value) => value with { };

    internal static WoundAcceptedTransitionDraft CloneTransitionDraft(
        WoundAcceptedTransitionDraft value) =>
        new(
            value.Kind,
            value.OperationKey,
            value.LocalWoundRef,
            value.LocalTransitionRef,
            value.OpportunityId,
            value.ReadableSummary,
            value.ProposedAfter,
            value.EffectDefinitions,
            value.RootApplications,
            value.SlotBindings);

    internal static WoundEffectSourceDefinition CloneSourceDefinition(
        WoundEffectSourceDefinition value) =>
        new(value.DefinitionKey, value.Definition);

    internal static WoundEffectSourceSelector? CloneSourceSelector(
        WoundEffectSourceSelector? value) => value is null ? null : value with { };

    internal static WoundEffectTargetSelector? CloneTargetSelector(
        WoundEffectTargetSelector? value) => value is null ? null : value with { };

    internal static EffectSourceKey? CloneSourceKey(EffectSourceKey? value) =>
        value is null ? null : value with { };

    internal static EffectTargetKey? CloneTargetKey(EffectTargetKey? value) =>
        value is null ? null : value with { };

    internal static EffectCarrierCoordinate? CloneEffectCarrierCoordinate(
        EffectCarrierCoordinate? value) => value is null ? null : value with { };

    internal static WoundEffectSourceExport? CloneSourceExport(
        WoundEffectSourceExport? value) =>
        value is null
            ? null
            : new WoundEffectSourceExport(
                value.SchemaVersion,
                value.Kind,
                value.SourceId,
                value.SourceRef,
                value.State,
                value.Materializable,
                value.Realm,
                value.Owner,
                value.CausalEventRef,
                value.EventSemanticFingerprint,
                value.OpportunityId,
                value.OpportunityAuthorityFingerprint,
                value.Definitions);

    internal static WoundRootLineageAuthorityRow CloneLineageRow(
        WoundRootLineageAuthorityRow value) =>
        value with
        {
            OwnershipDomain = CloneOwnershipDomain(value.OwnershipDomain)!
        };

    internal static WoundEffectSlotAgreement CloneSlotAgreement(
        WoundEffectSlotAgreement value) => value with { };

    internal static WoundRootEffectApplication CloneRootEffectApplication(
        WoundRootEffectApplication value) =>
        new(
            value.ApplicationRef,
            value.MechanicsOrdinal,
            value.OperationOrdinal,
            value.OperationKind,
            value.OperationKey,
            value.DefinitionKey,
            value.TargetSelector,
            value.ExpectedTargetKey,
            value.SourceSelector,
            value.ExpectedSourceKey,
            value.Parameters,
            value.SlotBindings,
            value.ExpectedComponentCount,
            value.ExpectedMaterializationFingerprint,
            value.OwnershipDomain,
            value.CausalEventRef,
            value.ExpectedCarrierCoordinate);

    internal static WoundTerminalEffectOperation CloneTerminalOperation(
        WoundTerminalEffectOperation value) =>
        new(
            value.OperationRef,
            value.OperationKey,
            value.EffectId,
            value.MechanicsOrdinal,
            value.OperationOrdinal,
            value.OperationKind,
            value.CausalEventRef,
            value.ExpectedSourceKey,
            value.ExpectedTargetKey,
            value.ExpectedCarrierCoordinate,
            value.ExpectedCarrierFilePath,
            value.ExpectedCarrierJsonPath,
            value.ExpectedIdentityOwner,
            value.ExpectedStackCoordinate,
            value.ExpectedEffectFingerprint,
            value.ExpectedIdentityFingerprint,
            value.OwnershipDomain);

    internal static WoundPreparedTransitionAuthority? CloneTransitionAuthority(
        WoundPreparedTransitionAuthority? value) => value is null ? null : value with { };

    internal static WoundEffectOperationBatch CloneOperationBatch(
        WoundEffectOperationBatch value) =>
        new(
            value.LocalWoundRef,
            value.PreparedWoundId,
            value.SourceExport,
            value.RootApplications,
            value.TerminalOperations,
            value.RootLineageAuthority,
            value.SourceExportFingerprint,
            value.TransitionAuthority);

    internal static WoundPreparedBaselineAuthority? CloneBaselineAuthority(
        WoundPreparedBaselineAuthority? value) =>
        value is null
            ? null
            : new WoundPreparedBaselineAuthority(
                value.PreparedInputFingerprint,
                value.PreTurnCarriers,
                value.PreTurnIdentityIndex,
                value.PreTurnHistory,
                value.AuthoritySeal);

    internal static WoundPreparedAcceptedTurnPlan? ClonePreparedPlan(
        WoundPreparedAcceptedTurnPlan? value) =>
        value is null
            ? null
            : value.ClonePreservingCacheAuthority();

    internal static WoundEffectMaterializationAgreement? CloneAgreement(
        WoundEffectMaterializationAgreement? value) =>
        value is null
            ? null
            : new WoundEffectMaterializationAgreement(
                value.SlotBindings,
                value.ComponentCount,
                value.MaterializationFingerprint);

    internal static EffectAcceptedApplicationResult CloneApplicationResult(
        EffectAcceptedApplicationResult value) =>
        value with
        {
            SourceKey = CloneSourceKey(value.SourceKey)!,
            TargetKey = CloneTargetKey(value.TargetKey)!,
            CarrierCoordinate = CloneEffectCarrierCoordinate(value.CarrierCoordinate)!,
            Materialization = CloneAgreement(value.Materialization)!
        };

    internal static EffectAcceptedTerminationResult CloneTerminationResult(
        EffectAcceptedTerminationResult value) =>
        value with
        {
            SourceKey = CloneSourceKey(value.SourceKey)!,
            TargetKey = CloneTargetKey(value.TargetKey)!,
            CarrierCoordinate = CloneEffectCarrierCoordinate(value.CarrierCoordinate)!
        };

    internal static WoundEffectBatchAcceptedPlan? CloneEffectBatchPlan(
        WoundEffectBatchAcceptedPlan? value) => value?.DetachedCopy();

    internal static WoundCarrierMutation CloneCarrierMutation(
        WoundCarrierMutation value) =>
        new(value.Operation, value.WoundId, value.BeforeWound, value.AfterWound);

    internal static WoundCarrierContribution CloneCarrierContribution(
        WoundCarrierContribution value) =>
        new(value.Owner, value.ExpectedWoundCollectionFingerprint, value.Mutations);

    internal static WoundAcceptedTurnPlan? CloneFinalPlan(
        WoundAcceptedTurnPlan? value) =>
        value is null
            ? null
            : new WoundAcceptedTurnPlan(
                value.Binding,
                value.BindingFingerprint,
                value.InputFingerprint,
                value.WoundPreparationFingerprint,
                value.EffectInputFingerprint,
                value.EffectAcceptedTurnPlanFingerprint,
                value.WoundFinalPlanFingerprint,
                value.AllocatedWoundIds,
                value.AllocatedTransitionIds,
                value.CarrierContributions,
                value.IdentityIndexAfterImage,
                value.HistoryAfterImage,
                value.TransitionIntents);

    internal static WoundCarrierCatalogInput? CloneWoundCarriers(
        WoundCarrierCatalogInput? value) =>
        value is null
            ? null
            : new WoundCarrierCatalogInput(
                CloneObject(value.PlayerWounds),
                CloneObject(value.NpcWounds),
                CloneObject(value.EnemyCombatants),
                CloneObject(value.AllyCombatants),
                CloneObject(value.AfterlifeProfiles));

    internal static EffectCarrierCatalogInput? CloneEffectCarriers(
        EffectCarrierCatalogInput? value) =>
        value is null
            ? null
            : new EffectCarrierCatalogInput(
                CloneObject(value.PlayerEffects),
                CloneObject(value.NpcEffects),
                CloneObject(value.EnemyCombatants),
                CloneObject(value.AllyCombatants),
                CloneObject(value.AfterlifeProfiles),
                CloneObject(value.SpiritualConflict));

    internal static EffectAcceptedTurnInput CloneEffectInput(
        EffectAcceptedTurnInput value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EffectAcceptedTurnInput(
            value.SessionId,
            value.SnapshotToken,
            CloneObject(value.RawCommands)!,
            value.SourceAuthority,
            value.TargetAuthority,
            CloneObject(value.EventInput)!,
            value.Realm,
            CloneEffectCarriers(value.PreTurnCarriers),
            CloneObject(value.PreTurnIdentityIndex),
            CloneTargetAuthorityInput(value.TargetAuthorityInput),
            CloneEffectCarriers(value.PublicationCarrierBaselines),
            value.PreallocatedCombatantIdentities,
            CloneEffectCarriers(value.AcceptedCarrierBaselines));
    }

    internal static EffectAcceptedTurnPlan CloneEffectPlan(
        EffectAcceptedTurnPlan value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return EffectAcceptedTurnPlan.DetachedCopyOf(value);
    }

    internal static EffectTargetAuthorityInput? CloneTargetAuthorityInput(
        EffectTargetAuthorityInput? value)
    {
        if (value is null)
            return null;
        return new EffectTargetAuthorityInput(
            CopySimpleList(value.PreTurnTargets),
            CopySimpleList(value.SameTurnTargets),
            value.HistoricalTargetIds is null
                ? null!
                : new HashSet<string>(value.HistoricalTargetIds, StringComparer.Ordinal),
            value.CombatantIdentities);
    }

    private static IReadOnlyList<T> CopySimpleList<T>(IReadOnlyList<T>? values) =>
        values is null
            ? null!
            : new ReadOnlyCollection<T>(values.ToArray());

    internal static ValidationIssue CloneIssue(ValidationIssue value)
    {
        var clone = new ValidationIssue(
            value.FilePath,
            value.Severity,
            value.Message,
            value.Code,
            value.Actor,
            value.Section,
            value.Expected,
            value.Actual,
            value.RepairHint,
            value.Category,
            value.RepairTargetFiles.ToArray())
        {
            FactionRepairClassification = value.FactionRepairClassification,
            MortalItemRepairContext = CloneMortalItemRepairContext(
                value.MortalItemRepairContext),
            MortalLocationRepairContext = CloneMortalLocationRepairContext(
                value.MortalLocationRepairContext),
            EffectRepairContext = CloneEffectRepairContext(value.EffectRepairContext),
            WoundRepairContext = value.WoundRepairContext?.Clone()
        };
        return clone;
    }

    private static MortalItemRepairContext? CloneMortalItemRepairContext(
        MortalItemRepairContext? value) =>
        value is null
            ? null
            : value with
            {
                RequiredCompanionTargets = value.RequiredCompanionTargets is null
                    ? null!
                    : new ReadOnlyCollection<string>(
                        value.RequiredCompanionTargets.ToArray())
            };

    private static MortalLocationRepairContext? CloneMortalLocationRepairContext(
        MortalLocationRepairContext? value) =>
        value is null
            ? null
            : value with
            {
                RepairableFields = value.RepairableFields is null
                    ? null!
                    : new ReadOnlyCollection<string>(value.RepairableFields.ToArray())
            };

    private static EffectRepairContext? CloneEffectRepairContext(
        EffectRepairContext? value) =>
        value is null
            ? null
            : value with
            {
                ExpectedSource = CloneObject(value.ExpectedSource)!,
                ExpectedTarget = CloneObject(value.ExpectedTarget)!,
                ExpectedEventRef = CloneObject(value.ExpectedEventRef)!
            };

    internal static WoundTransitionIntent CloneTransitionIntent(
        WoundTransitionIntent value) => value switch
        {
            WoundCarrierTransitionIntent intent => intent with
            {
                Owner = CloneOwner(intent.Owner)!
            },
            WoundEffectTransitionIntent intent => new WoundEffectTransitionIntent(
                intent.Operation,
                intent.WoundId,
                CopySimpleList(intent.BeforeEffectIds),
                CopySimpleList(intent.AfterEffectIds)),
            WoundTransitionHistoryIntent intent => intent with { },
            WoundAttemptTerminalIntent intent => intent with { },
            WoundRecoverySealIntent intent => intent with { },
            WoundFollowUpHealIntent intent => intent with { },
            WoundCosmeticLegacyIntent intent => intent with { },
            WoundIndependentMechanicalLegacyIntent intent => intent with { },
            WoundArchiveProjectionIntent intent => intent with { },
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                value.GetType().FullName,
                "Unknown wound transition intent type.")
        };

    internal static WoundMaterializationEnvelope? CloneWound(
        WoundMaterializationEnvelope? value)
    {
        if (value is null)
            return null;
        return new WoundMaterializationEnvelope(
            value.SchemaVersion,
            value.WoundId,
            value.Lifecycle,
            CloneOwner(value.Owner)!,
            CloneOrigin(value.Origin)!,
            CloneClassification(value.Classification)!,
            CloneDisplay(value.Display)!,
            value.Severity is null ? null! : value.Severity with { },
            value.Care is null ? null! : value.Care with { },
            FreezeList(value.Complications, CloneComplication)!,
            CloneConsequences(value.Consequences)!,
            CloneTreatment(value.Treatment)!,
            CloneRecovery(value.Recovery)!,
            CloneRelations(value.Relations)!,
            value.LastTransition is null ? null! : value.LastTransition with { });
    }

    private static WoundOrigin? CloneOrigin(WoundOrigin? value) =>
        value is null ? null : value with { };

    private static WoundClassification? CloneClassification(
        WoundClassification? value) =>
        value is null
            ? null
            : value with
            {
                LocationProfile = value.LocationProfile is null
                    ? null!
                    : value.LocationProfile with { }
            };

    private static WoundDisplay? CloneDisplay(WoundDisplay? value) =>
        value is null
            ? null
            : value with
            {
                VisibleSymptoms = CopySimpleList(value.VisibleSymptoms)
            };

    private static WoundComplication CloneComplication(WoundComplication value) =>
        value with { OwnedEffectIds = CopySimpleList(value.OwnedEffectIds) };

    private static WoundConsequences? CloneConsequences(WoundConsequences? value)
    {
        if (value is null)
            return null;
        return new WoundConsequences(
            value.SlotBudget,
            value.SlotsUsed,
            FreezeList(value.Entries, static entry => entry with { })!)
        {
            OwnedEffectSources = CloneOwnedEffectSources(value.OwnedEffectSources)!
        };
    }

    private static WoundOwnedEffectSources? CloneOwnedEffectSources(
        WoundOwnedEffectSources? value)
    {
        if (value is null)
            return null;
        return new WoundOwnedEffectSources(
            CloneElements(value.Definitions),
            FreezeList(value.RootBindings, static binding => binding with { })!)
        {
            DefinitionFacts = value.DefinitionFacts is null
                ? null!
                : value.DefinitionFacts.Select(static fact => fact is null
                    ? null!
                    : fact with
                    {
                        ApplyDefinitionTargets = fact.ApplyDefinitionTargets.IsDefault
                            ? default
                            : fact.ApplyDefinitionTargets.ToImmutableArray()
                    }).ToImmutableArray()
        };
    }

    private static IReadOnlyList<JsonElement> CloneElements(
        IReadOnlyList<JsonElement>? values)
    {
        if (values is null)
            return null!;
        var result = values.Select(CloneElement).ToArray();
        return new ReadOnlyCollection<JsonElement>(result);
    }

    private static JsonElement CloneElement(JsonElement value) =>
        value.ValueKind == JsonValueKind.Undefined ? default : value.Clone();

    private static WoundTreatment? CloneTreatment(WoundTreatment? value) =>
        value is null
            ? null
            : new WoundTreatment(
                FreezeList(value.DiagnosisPaths, CloneDiagnosisPath)!,
                FreezeList(value.Routes, CloneTreatmentRoute)!,
                CopySimpleList(value.KnownRouteIds),
                CopySimpleList(value.CompletedRouteIds));

    private static WoundDiagnosisPath CloneDiagnosisPath(
        WoundDiagnosisPath value) =>
        new(
            value.DiagnosisPathId,
            value.DisplayName,
            value.Visibility,
            CopySimpleList(value.RequiresKnownFacts),
            CloneElements(value.Requirements),
            CloneElement(value.Check),
            CopySimpleList(value.Reveals),
            value.FailurePolicy)
        {
            SourcePath = value.SourcePath
        };

    private static WoundTreatmentRoute CloneTreatmentRoute(
        WoundTreatmentRoute value) =>
        new(
            value.RouteId,
            value.DisplayName,
            value.Visibility,
            value.Mode,
            CloneElements(value.Requirements),
            CloneElement(value.ResourcePolicy),
            CloneElement(value.Resolution),
            CloneElements(value.Outcomes),
            value.Interruption is { } interruption
                ? CloneElement(interruption)
                : null)
        {
            SourcePath = value.SourcePath
        };

    private static WoundRecovery? CloneRecovery(WoundRecovery? value) =>
        value is null
            ? null
            : new WoundRecovery(
                value.Mode,
                value.ClockKind,
                value.Cadence,
                value.CurrentStepProgress,
                value.CurrentStepThreshold,
                value.LastTickKey,
                CopySimpleList(value.Blockers),
                value.CarryOverflow,
                value.DeteriorationPolicy is { } deterioration
                    ? CloneElement(deterioration)
                    : null);

    private static WoundRelations? CloneRelations(WoundRelations? value) =>
        value is null
            ? null
            : new WoundRelations(
                value.PriorWoundId,
                CopySimpleList(value.LegacyRefs),
                CopySimpleList(value.IndependentEffectRefs));
}

internal static class WoundAcceptedTurnFingerprints
{
    private const string Version = "1";
    private const string BindingDomain = "book_of_eternity.wound.binding";
    private const string InputDomain = "book_of_eternity.wound.input";
    private const string SourceExportDomain =
        "book_of_eternity.wound.source_export";
    private const string PreparationDomain =
        "book_of_eternity.wound.preparation";
    private const string TransitionAuthorityDomain =
        "book_of_eternity.wound.prepared_transition_authority";
    private const string BaselineAuthorityDomain =
        "book_of_eternity.wound.prepared_baseline_authority";
    private const string EffectInputDomain =
        "book_of_eternity.wound.effect_input";
    private const string EffectPlanDomain =
        "book_of_eternity.wound.effect_accepted_plan";
    private const string AcceptedEffectPlanPayloadDomain =
        "book_of_eternity.effect.accepted_plan_payload";
    private const string FinalPlanDomain =
        "book_of_eternity.wound.final_plan";

    internal static string ComputeBinding(WoundAcceptedTurnBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        var fields = new List<string?> { BindingDomain, Version };
        AppendBinding(fields, binding);
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    internal static string ComputeInput(WoundAcceptedTurnInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var fields = new List<string?> { InputDomain, Version };
        var binding = input.Binding;
        fields.Add(binding is null ? null : ComputeBinding(binding));

        var opportunities = input.Opportunities;
        fields.Add(Count(opportunities));
        if (opportunities is not null)
        {
            for (var index = 0; index < opportunities.Count; index++)
            {
                fields.Add(Number(index));
                AppendOpportunity(fields, opportunities[index]);
            }
        }

        var transitions = input.Transitions;
        fields.Add(Count(transitions));
        if (transitions is not null)
        {
            for (var index = 0; index < transitions.Count; index++)
            {
                fields.Add(Number(index));
                AppendTransitionDraft(fields, transitions[index]);
            }
        }

        AppendWoundCarriers(fields, input.PreTurnCarriers);
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
            input.PreTurnIdentityIndex));
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
            input.PreTurnHistory));
        AppendEffectCarriers(fields, input.PreTurnEffectCarriers);
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
            input.PreTurnEffectIdentityIndex));
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    internal static string ComputeSourceExport(WoundEffectOperationBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        var export = batch.SourceExport;
        var owner = export.Owner;
        var definitions = export.Definitions;
        var lineageRows = batch.RootLineageAuthority;
        var rootApplications = batch.RootApplications;
        var fields = new List<string?>
        {
            SourceExportDomain,
            Version,
            Number(export.SchemaVersion),
            export.Kind,
            export.SourceId,
            export.SourceRef,
            export.State,
            Boolean(export.Materializable),
            export.Realm,
            owner.Realm,
            owner.OwnerKind,
            owner.OwnerId,
            owner.CarrierPath,
            export.CausalEventRef,
            export.EventSemanticFingerprint,
            export.OpportunityId,
            export.OpportunityAuthorityFingerprint,
            Count(definitions)
        };
        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];
            fields.Add(Number(index));
            fields.Add(definition?.DefinitionKey);
            fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                definition?.Definition));
        }

        fields.Add(Count(lineageRows));
        for (var index = 0; index < lineageRows.Count; index++)
        {
            var lineage = lineageRows[index];
            fields.Add(Number(index));
            fields.Add(lineage?.ApplicationRef);
            fields.Add(lineage?.EffectId);
            fields.Add(lineage?.DefinitionKey);
            fields.Add(lineage?.OwnershipDomain?.Kind);
            fields.Add(lineage?.OwnershipDomain?.ComplicationId);
        }

        fields.Add(Count(rootApplications));
        for (var index = 0; index < rootApplications.Count; index++)
            AppendRootApplication(fields, index, rootApplications[index]);
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    internal static string ComputePreparation(WoundPreparedAcceptedTurnPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var binding = plan.Binding;
        var acceptedEvents = binding.AcceptedEvents;
        var woundIds = plan.AllocatedWoundIds;
        var transitionIds = plan.AllocatedTransitionIds;
        var preparedWounds = plan.PreparedWounds;
        var operationBatches = plan.EffectOperationBatches;
        var fields = new List<string?>
        {
            PreparationDomain,
            Version,
            plan.BindingFingerprint,
            plan.InputFingerprint,
            binding.SessionId,
            binding.RequestId,
            binding.SnapshotToken,
            binding.Realm,
            Number(binding.Turn),
            binding.AcceptedEventsFingerprint,
            Count(acceptedEvents)
        };
        for (var index = 0; index < acceptedEvents.Count; index++)
        {
            var authority = acceptedEvents[index];
            fields.Add(Number(index));
            fields.Add(authority?.EventRef);
            fields.Add(authority?.Kind);
            fields.Add(authority?.AuthorityId);
            fields.Add(authority?.SemanticFingerprint);
        }

        fields.Add(Count(woundIds));
        AppendOrdered(fields, woundIds);
        fields.Add(Count(transitionIds));
        AppendOrdered(fields, transitionIds);
        fields.Add(Count(preparedWounds));
        AppendOrdered(
            fields,
            preparedWounds.Select(static wound =>
                wound is null
                    ? null
                    : WoundMaterializationContract.SerializeCanonical(wound)));
        fields.Add(Count(operationBatches));
        for (var index = 0; index < operationBatches.Count; index++)
        {
            var batch = operationBatches[index];
            fields.Add(Number(index));
            fields.Add(batch?.LocalWoundRef);
            fields.Add(batch?.PreparedWoundId);
            fields.Add(batch is null ? null : ComputeSourceExport(batch));
            var transitionAuthority = batch?.TransitionAuthority;
            fields.Add(transitionAuthority?.PreparedInputFingerprint);
            fields.Add(transitionAuthority?.OpportunityId);
            fields.Add(transitionAuthority?.OpportunityAuthorityFingerprint);
            fields.Add(transitionAuthority?.OperationKey);
            fields.Add(transitionAuthority?.ReadableSummary);
            fields.Add(transitionAuthority is null
                ? null
                : Number(transitionAuthority.MaximumSeverityRank));
            fields.Add(transitionAuthority?.TransitionKind);
            fields.Add(transitionAuthority?.CauseKind);
            fields.Add(transitionAuthority?.ExpectedBeforeFingerprint);
            fields.Add(transitionAuthority?.AuthoritySeal);
            var terminalOperations = batch?.TerminalOperations;
            fields.Add(Count(terminalOperations));
            if (batch is null)
                continue;
            for (var terminalIndex = 0;
                 terminalIndex < terminalOperations!.Count;
                 terminalIndex++)
            {
                AppendTerminalOperation(
                    fields,
                    terminalIndex,
                    terminalOperations[terminalIndex]);
            }
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    internal static string ComputeTransitionAuthority(
        string preparedInputFingerprint,
        string localWoundRef,
        string preparedWoundId,
        string opportunityId,
        string opportunityAuthorityFingerprint,
        string operationKey,
        string readableSummary,
        int maximumSeverityRank,
        string transitionKind = "create",
        string? causeKind = null,
        string? expectedBeforeFingerprint = null) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            TransitionAuthorityDomain,
            Version,
            preparedInputFingerprint,
            localWoundRef,
            preparedWoundId,
            opportunityId,
            opportunityAuthorityFingerprint,
            operationKey,
            readableSummary,
            Number(maximumSeverityRank),
            transitionKind,
            causeKind,
            expectedBeforeFingerprint
        });

    internal static string ComputeBaselineAuthority(
        string preparedInputFingerprint,
        WoundCarrierCatalogInput carriers,
        JsonObject identity,
        JsonObject history)
    {
        ArgumentNullException.ThrowIfNull(carriers);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(history);
        var fields = new List<string?>
        {
            BaselineAuthorityDomain,
            Version,
            preparedInputFingerprint
        };
        AppendWoundCarriers(fields, carriers);
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(identity));
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(history));
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    internal static string ComputeEffectInput(
        WoundPreparedAcceptedTurnPlan prepared,
        EffectAcceptedTurnInput input)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(input);
        var fields = new List<string?>
        {
            EffectInputDomain,
            Version,
            ComputePreparation(prepared),
            input.SessionId,
            input.SnapshotToken,
            input.Realm,
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(input.RawCommands),
            input.SourceAuthority?.Fingerprint,
            input.TargetAuthority?.Fingerprint,
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(input.EventInput)
        };
        AppendEffectCarriers(fields, input.PreTurnCarriers);
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
            input.PreTurnIdentityIndex));
        AppendTargetAuthorityInput(fields, input.TargetAuthorityInput);
        AppendEffectCarriers(fields, input.PublicationCarrierBaselines);
        fields.Add(input.PreallocatedCombatantIdentities?.Fingerprint);
        AppendEffectCarriers(fields, input.AcceptedCarrierBaselines);
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    internal static string ComputeEffectPlan(
        WoundPreparedAcceptedTurnPlan prepared,
        EffectAcceptedTurnInput input,
        EffectAcceptedTurnPlan plan,
        IReadOnlyList<EffectAcceptedApplicationResult> applicationResults,
        IReadOnlyList<EffectAcceptedTerminationResult> terminationResults)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(applicationResults);
        ArgumentNullException.ThrowIfNull(terminationResults);
        var sources = plan.Sources;
        var targets = plan.Targets;
        var sourceBindings = plan.SourceBindings;
        var deferredReactions = plan.DeferredReactions;
        var activeEffects = plan.ActiveEffects;
        var fields = new List<string?>
        {
            EffectPlanDomain,
            Version,
            ComputePreparation(prepared),
            ComputeEffectInput(prepared, input),
            plan.InputFingerprint,
            plan.CarrierAuthorityFingerprint,
            plan.SourceAuthorityFingerprint,
            plan.TargetAuthorityFingerprint
        };
        AppendOrdered(fields, plan.AllocatedCombatantIds, includeCount: true);
        AppendOrdered(fields, plan.AllocatedEffectIds, includeCount: true);
        AppendOrdered(fields, plan.AllocatedTransitionIds, includeCount: true);
        AppendWoundApplicationRootEffectBindings(
            fields,
            plan.WoundApplicationRootEffectBindings);

        fields.Add(Count(sources));
        for (var index = 0; index < sources.Count; index++)
        {
            fields.Add(Number(index));
            AppendSourceKey(fields, sources[index]);
        }
        fields.Add(Count(targets));
        for (var index = 0; index < targets.Count; index++)
        {
            fields.Add(Number(index));
            AppendTargetKey(fields, targets[index]);
        }

        fields.Add(Count(sourceBindings));
        for (var index = 0; index < sourceBindings.Count; index++)
        {
            fields.Add(Number(index));
            AppendSourceBinding(fields, sourceBindings[index]);
        }

        fields.Add(Count(deferredReactions));
        for (var index = 0; index < deferredReactions.Count; index++)
        {
            fields.Add(Number(index));
            AppendReaction(fields, deferredReactions[index]);
        }
        fields.Add(Number(plan.ReactionExpansionCount));
        var usage = plan.ReactionExpansionUsage
            .OrderBy(static pair => pair.Key.EffectId, StringComparer.Ordinal)
            .ThenBy(static pair => pair.Key.ComponentId, StringComparer.Ordinal)
            .ToArray();
        fields.Add(Number(usage.Length));
        for (var index = 0; index < usage.Length; index++)
        {
            fields.Add(Number(index));
            fields.Add(usage[index].Key.EffectId);
            fields.Add(usage[index].Key.ComponentId);
            fields.Add(Number(usage[index].Value.Count));
            fields.Add(Number(usage[index].Value.Maximum));
        }

        fields.Add(Count(activeEffects));
        for (var index = 0; index < activeEffects.Count; index++)
        {
            fields.Add(Number(index));
            fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                activeEffects[index]));
        }
        AppendEffectCarriers(fields, plan.ResourceTriggerCarriers);
        fields.Add(plan.SourceAuthority?.Fingerprint);
        fields.Add(plan.TargetAuthority?.Fingerprint);
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(plan.EventInput));
        AppendCarrierBeforeImages(fields, plan.CarrierBeforeImages);
        AppendCarrierAfterImages(fields, plan.CarrierAfterImages);
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
            plan.IdentityIndexBeforeImage));
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
            plan.IdentityIndexAfterImage));
        AppendOrdered(fields, plan.TouchedPaths, includeCount: true);
        AppendOrdered(fields, plan.DeletedPaths, includeCount: true);
        AppendEffectCarriers(fields, plan.AcceptedCarrierBaselines);
        fields.Add(Count(applicationResults));
        for (var index = 0; index < applicationResults.Count; index++)
        {
            fields.Add(Number(index));
            AppendApplicationResult(fields, applicationResults[index]);
        }
        fields.Add(Count(terminationResults));
        for (var index = 0; index < terminationResults.Count; index++)
        {
            fields.Add(Number(index));
            AppendTerminationResult(fields, terminationResults[index]);
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    internal static string ComputeAcceptedEffectPlanPayload(
        EffectAcceptedTurnPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var sources = plan.Sources;
        var targets = plan.Targets;
        var sourceBindings = plan.SourceBindings;
        var deferredReactions = plan.DeferredReactions;
        var activeEffects = plan.ActiveEffects;
        var fields = new List<string?>
        {
            AcceptedEffectPlanPayloadDomain,
            Version,
            plan.InputFingerprint,
            plan.CarrierAuthorityFingerprint,
            plan.SourceAuthorityFingerprint,
            plan.TargetAuthorityFingerprint
        };
        AppendOrdered(fields, plan.AllocatedCombatantIds, includeCount: true);
        AppendOrdered(fields, plan.AllocatedEffectIds, includeCount: true);
        AppendOrdered(fields, plan.AllocatedTransitionIds, includeCount: true);
        AppendWoundApplicationRootEffectBindings(
            fields,
            plan.WoundApplicationRootEffectBindings);

        fields.Add(Count(sources));
        for (var index = 0; index < sources.Count; index++)
        {
            fields.Add(Number(index));
            AppendSourceKey(fields, sources[index]);
        }
        fields.Add(Count(targets));
        for (var index = 0; index < targets.Count; index++)
        {
            fields.Add(Number(index));
            AppendTargetKey(fields, targets[index]);
        }
        fields.Add(Count(sourceBindings));
        for (var index = 0; index < sourceBindings.Count; index++)
        {
            fields.Add(Number(index));
            AppendSourceBinding(fields, sourceBindings[index]);
        }
        fields.Add(Count(deferredReactions));
        for (var index = 0; index < deferredReactions.Count; index++)
        {
            fields.Add(Number(index));
            AppendReaction(fields, deferredReactions[index]);
        }
        fields.Add(Number(plan.ReactionExpansionCount));
        var usage = plan.ReactionExpansionUsage
            .OrderBy(static pair => pair.Key.EffectId, StringComparer.Ordinal)
            .ThenBy(static pair => pair.Key.ComponentId, StringComparer.Ordinal)
            .ToArray();
        fields.Add(Number(usage.Length));
        for (var index = 0; index < usage.Length; index++)
        {
            fields.Add(Number(index));
            fields.Add(usage[index].Key.EffectId);
            fields.Add(usage[index].Key.ComponentId);
            fields.Add(Number(usage[index].Value.Count));
            fields.Add(Number(usage[index].Value.Maximum));
        }
        fields.Add(Count(activeEffects));
        for (var index = 0; index < activeEffects.Count; index++)
        {
            fields.Add(Number(index));
            fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                activeEffects[index]));
        }
        AppendEffectCarriers(fields, plan.ResourceTriggerCarriers);
        fields.Add(plan.SourceAuthority?.Fingerprint);
        fields.Add(plan.TargetAuthority?.Fingerprint);
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
            plan.EventInput));
        AppendCarrierBeforeImages(fields, plan.CarrierBeforeImages);
        AppendCarrierAfterImages(fields, plan.CarrierAfterImages);
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
            plan.IdentityIndexBeforeImage));
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
            plan.IdentityIndexAfterImage));
        AppendOrdered(fields, plan.TouchedPaths, includeCount: true);
        AppendOrdered(fields, plan.DeletedPaths, includeCount: true);
        AppendEffectCarriers(fields, plan.AcceptedCarrierBaselines);
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static void AppendWoundApplicationRootEffectBindings(
        ICollection<string?> fields,
        IReadOnlyList<WoundApplicationRootEffectBinding> bindings)
    {
        fields.Add(Count(bindings));
        for (var index = 0; index < bindings.Count; index++)
        {
            fields.Add(Number(index));
            fields.Add(bindings[index]?.ApplicationRef);
            fields.Add(bindings[index]?.EffectId);
        }
    }

    internal static string ComputeFinal(
        WoundPreparedAcceptedTurnPlan prepared,
        WoundEffectBatchAcceptedPlan effectBatch,
        IReadOnlyList<WoundCarrierContribution> carrierContributions,
        JsonObject identityAfter,
        JsonObject historyAfter,
        IReadOnlyList<WoundTransitionIntent> transitionIntents)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(effectBatch);
        ArgumentNullException.ThrowIfNull(carrierContributions);
        ArgumentNullException.ThrowIfNull(identityAfter);
        ArgumentNullException.ThrowIfNull(historyAfter);
        ArgumentNullException.ThrowIfNull(transitionIntents);
        var fields = new List<string?>
        {
            FinalPlanDomain,
            Version,
            prepared.BindingFingerprint,
            prepared.InputFingerprint,
            effectBatch.WoundPreparationFingerprint,
            effectBatch.EffectInputFingerprint,
            effectBatch.EffectAcceptedTurnPlanFingerprint
        };
        AppendOrdered(fields, prepared.AllocatedWoundIds, includeCount: true);
        AppendOrdered(fields, prepared.AllocatedTransitionIds, includeCount: true);
        fields.Add(Count(carrierContributions));
        for (var index = 0; index < carrierContributions.Count; index++)
        {
            fields.Add(Number(index));
            AppendCarrierContribution(fields, carrierContributions[index]);
        }
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(identityAfter));
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(historyAfter));
        fields.Add(Count(transitionIntents));
        for (var index = 0; index < transitionIntents.Count; index++)
        {
            fields.Add(Number(index));
            AppendTransitionIntent(fields, transitionIntents[index]);
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static void AppendBinding(
        ICollection<string?> fields,
        WoundAcceptedTurnBinding? binding)
    {
        var acceptedEvents = binding?.AcceptedEvents;
        fields.Add(binding?.SessionId);
        fields.Add(binding?.RequestId);
        fields.Add(binding?.SnapshotToken);
        fields.Add(binding?.Realm);
        fields.Add(binding is null ? null : Number(binding.Turn));
        fields.Add(binding?.AcceptedEventsFingerprint);
        fields.Add(Count(acceptedEvents));
        if (acceptedEvents is null)
            return;
        for (var index = 0; index < acceptedEvents.Count; index++)
        {
            var value = acceptedEvents[index];
            fields.Add(Number(index));
            fields.Add(value?.EventRef);
            fields.Add(value?.Kind);
            fields.Add(value?.AuthorityId);
            fields.Add(value?.SemanticFingerprint);
        }
    }

    private static void AppendOpportunity(
        ICollection<string?> fields,
        WoundOpportunityAuthority? value)
    {
        fields.Add(value?.SessionId);
        fields.Add(value?.RequestId);
        fields.Add(value?.SnapshotToken);
        fields.Add(value?.OpportunityId);
        fields.Add(value?.PublicRef);
        fields.Add(value?.EventRef);
        fields.Add(value?.EventKind);
        fields.Add(value?.EventAuthorityId);
        AppendOwner(fields, value?.Owner);
        fields.Add(value?.Domain);
        fields.Add(value?.ProfileKey);
        fields.Add(value?.SourceKind);
        fields.Add(value?.SourceId);
        fields.Add(value?.SourceState);
        fields.Add(value?.MinimumSeverityRank is { } minimum
            ? Number(minimum)
            : null);
        fields.Add(value is null ? null : Number(value.MaximumSeverityRank));
        var guarantee = value?.GuaranteedTrigger;
        fields.Add(guarantee?.TriggerId);
        fields.Add(guarantee?.SourceKind);
        fields.Add(guarantee?.SourceId);
        fields.Add(guarantee?.SourceState);
        fields.Add(guarantee?.Realm);
        fields.Add(guarantee?.Domain);
        AppendOwner(fields, guarantee?.Owner);
        fields.Add(guarantee is null
            ? null
            : Number(guarantee.RequiredSeverityRank));
        fields.Add(guarantee is null
            ? null
            : Number(guarantee.MaterializedAtTurn));
        fields.Add(guarantee?.SourceContractFingerprint);
        fields.Add(guarantee?.AuthorityFingerprint);
        var safeContext = value?.SafeContext;
        fields.Add(safeContext?.Target);
        fields.Add(safeContext?.Cause);
        var locationKinds = safeContext?.AllowedLocationKinds;
        fields.Add(Count(locationKinds));
        if (locationKinds is not null)
        {
            for (var index = 0; index < locationKinds.Count; index++)
            {
                fields.Add(Number(index));
                fields.Add(locationKinds[index]);
            }
        }
        fields.Add(value?.InputEvidenceFingerprint);
        var worseningTarget = value?.WorseningTarget;
        fields.Add(worseningTarget?.CauseKind);
        fields.Add(worseningTarget?.ExpectedBeforeFingerprint);
        fields.Add(worseningTarget is null
            ? null
            : WoundMaterializationContract.SerializeCanonical(
                worseningTarget.Wound));
        fields.Add(value?.AuthorityFingerprint);
    }

    private static void AppendTransitionDraft(
        ICollection<string?> fields,
        WoundAcceptedTransitionDraft? value)
    {
        fields.Add(value?.Kind);
        fields.Add(value?.OperationKey);
        fields.Add(value?.LocalWoundRef);
        fields.Add(value?.LocalTransitionRef);
        fields.Add(value?.OpportunityId);
        fields.Add(value?.ReadableSummary);
        fields.Add(value?.ProposedAfter is null
            ? null
            : WoundMaterializationContract.SerializeCanonical(value.ProposedAfter));
        var definitions = value?.EffectDefinitions;
        fields.Add(Count(definitions));
        if (definitions is not null)
        {
            for (var index = 0; index < definitions.Count; index++)
            {
                fields.Add(Number(index));
                fields.Add(definitions[index]?.LocalEffectRef);
                fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                    definitions[index]?.Definition));
            }
        }
        var roots = value?.RootApplications;
        fields.Add(Count(roots));
        if (roots is not null)
        {
            for (var index = 0; index < roots.Count; index++)
            {
                var root = roots[index];
                fields.Add(Number(index));
                fields.Add(root?.LocalApplicationRef);
                fields.Add(root?.LocalEffectRef);
                fields.Add(root?.OperationKey);
                fields.Add(root?.OwnershipDomain?.Kind);
                fields.Add(root?.OwnershipDomain?.ComplicationId);
            }
        }
        var slots = value?.SlotBindings;
        fields.Add(Count(slots));
        if (slots is null)
            return;
        for (var index = 0; index < slots.Count; index++)
        {
            var slot = slots[index];
            fields.Add(Number(index));
            fields.Add(slot is null ? null : Number(slot.Slot));
            fields.Add(slot?.ProfileKey);
            fields.Add(slot?.LocalApplicationRef);
            fields.Add(slot?.ReadableSummary);
        }
    }

    private static void AppendRootApplication(
        ICollection<string?> fields,
        int index,
        WoundRootEffectApplication? value)
    {
        fields.Add(Number(index));
        fields.Add(value?.ApplicationRef);
        fields.Add(value is null ? null : Number(value.MechanicsOrdinal));
        fields.Add(value is null ? null : Number(value.OperationOrdinal));
        fields.Add(value?.OperationKind);
        fields.Add(value?.OperationKey);
        fields.Add(value?.DefinitionKey);
        fields.Add(value?.TargetSelector?.Kind);
        fields.Add(value?.TargetSelector?.TargetId);
        fields.Add(value?.TargetSelector?.TargetRef);
        fields.Add(value?.ExpectedTargetKey?.Realm);
        fields.Add(value?.ExpectedTargetKey?.Kind);
        fields.Add(value?.ExpectedTargetKey?.TargetId);
        fields.Add(value?.SourceSelector?.Realm);
        fields.Add(value?.SourceSelector?.Kind);
        fields.Add(value?.SourceSelector?.SourceId);
        fields.Add(value?.SourceSelector?.SourceRef);
        fields.Add(value?.SourceSelector?.DefinitionKey);
        fields.Add(value?.ExpectedSourceKey?.Realm);
        fields.Add(value?.ExpectedSourceKey?.Kind);
        fields.Add(value?.ExpectedSourceKey?.SourceId);
        fields.Add(value?.ExpectedSourceKey?.DefinitionKey);
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
            value?.Parameters));
        var slots = value?.SlotBindings;
        fields.Add(Count(slots));
        if (slots is not null)
        {
            for (var slotIndex = 0; slotIndex < slots.Count; slotIndex++)
            {
                var slot = slots[slotIndex];
                fields.Add(Number(slotIndex));
                fields.Add(slot is null ? null : Number(slot.Slot));
                fields.Add(slot?.ProfileKey);
                fields.Add(slot?.ReadableSummary);
            }
        }
        fields.Add(value is null ? null : Number(value.ExpectedComponentCount));
        fields.Add(value?.ExpectedMaterializationFingerprint);
        fields.Add(value?.OwnershipDomain?.Kind);
        fields.Add(value?.OwnershipDomain?.ComplicationId);
        fields.Add(value?.CausalEventRef);
        fields.Add(value?.ExpectedCarrierCoordinate?.Kind);
        fields.Add(value?.ExpectedCarrierCoordinate?.OwnerId);
        fields.Add(value?.ExpectedCarrierCoordinate?.Path);
        fields.Add(value?.ExpectedCarrierCoordinate?.Category);
    }

    private static void AppendTerminalOperation(
        ICollection<string?> fields,
        int index,
        WoundTerminalEffectOperation? value)
    {
        fields.Add(Number(index));
        fields.Add(value?.OperationRef);
        fields.Add(value?.OperationKey);
        fields.Add(value?.EffectId);
        fields.Add(value is null ? null : Number(value.MechanicsOrdinal));
        fields.Add(value is null ? null : Number(value.OperationOrdinal));
        fields.Add(value?.OperationKind);
        fields.Add(value?.CausalEventRef);
        AppendSourceKey(fields, value?.ExpectedSourceKey);
        AppendTargetKey(fields, value?.ExpectedTargetKey);
        AppendCarrierCoordinate(fields, value?.ExpectedCarrierCoordinate);
        fields.Add(value?.ExpectedCarrierFilePath);
        fields.Add(value?.ExpectedCarrierJsonPath);
        fields.Add(value?.ExpectedIdentityOwner?.Kind);
        fields.Add(value?.ExpectedIdentityOwner?.OwnerId);
        fields.Add(value?.ExpectedIdentityOwner?.CarrierPath);
        fields.Add(value?.ExpectedIdentityOwner?.Collection);
        fields.Add(value?.ExpectedStackCoordinate?.Realm);
        fields.Add(value?.ExpectedStackCoordinate?.TargetKind);
        fields.Add(value?.ExpectedStackCoordinate?.TargetId);
        fields.Add(value?.ExpectedStackCoordinate?.SourceKind);
        fields.Add(value?.ExpectedStackCoordinate?.SourceId);
        fields.Add(value?.ExpectedStackCoordinate?.StackKey);
        fields.Add(value?.ExpectedEffectFingerprint);
        fields.Add(value?.ExpectedIdentityFingerprint);
        fields.Add(value?.OwnershipDomain?.Kind);
        fields.Add(value?.OwnershipDomain?.ComplicationId);
    }

    private static void AppendWoundCarriers(
        ICollection<string?> fields,
        WoundCarrierCatalogInput? value)
    {
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
            value?.PlayerWounds));
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(value?.NpcWounds));
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
            value?.EnemyCombatants));
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
            value?.AllyCombatants));
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
            value?.AfterlifeProfiles));
    }

    private static void AppendEffectCarriers(
        ICollection<string?> fields,
        EffectCarrierCatalogInput? value)
    {
        if (value is null)
        {
            fields.Add(null);
            return;
        }
        fields.Add("present");
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
            value.PlayerEffects));
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(value.NpcEffects));
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
            value.EnemyCombatants));
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
            value.AllyCombatants));
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
            value.AfterlifeProfiles));
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
            value.SpiritualConflict));
    }

    private static void AppendTargetAuthorityInput(
        ICollection<string?> fields,
        EffectTargetAuthorityInput? value)
    {
        if (value is null)
        {
            fields.Add(null);
            return;
        }
        fields.Add("present");
        AppendTargetExports(fields, value.PreTurnTargets);
        AppendTargetExports(fields, value.SameTurnTargets);
        var historical = value.HistoricalTargetIds?
            .OrderBy(static item => item, StringComparer.Ordinal)
            .ToArray();
        fields.Add(historical is null ? null : Number(historical.Length));
        if (historical is not null)
            AppendOrdered(fields, historical);
        fields.Add(value.CombatantIdentities?.Fingerprint);
    }

    private static void AppendTargetExports(
        ICollection<string?> fields,
        IReadOnlyList<EffectTargetExport>? values)
    {
        fields.Add(Count(values));
        if (values is null)
            return;
        for (var index = 0; index < values.Count; index++)
        {
            var value = values[index];
            fields.Add(Number(index));
            fields.Add(value?.Realm);
            fields.Add(value?.Kind);
            fields.Add(value?.TargetId);
            fields.Add(value is null ? null : Boolean(value.SameTurn));
            fields.Add(value?.TargetRef);
            fields.Add(value?.BoundNpcId);
            fields.Add(value?.BoundResourceOwnerKind is { } ownerKind
                ? Number((int)ownerKind)
                : null);
        }
    }

    private static void AppendSourceKey(
        ICollection<string?> fields,
        EffectSourceKey? value)
    {
        fields.Add(value?.Realm);
        fields.Add(value?.Kind);
        fields.Add(value?.SourceId);
        fields.Add(value?.DefinitionKey);
    }

    private static void AppendTargetKey(
        ICollection<string?> fields,
        EffectTargetKey? value)
    {
        fields.Add(value?.Realm);
        fields.Add(value?.Kind);
        fields.Add(value?.TargetId);
    }

    private static void AppendCarrierCoordinate(
        ICollection<string?> fields,
        EffectCarrierCoordinate? value)
    {
        fields.Add(value?.Kind);
        fields.Add(value?.OwnerId);
        fields.Add(value?.Path);
        fields.Add(value?.Category);
    }

    private static void AppendApplicationResult(
        ICollection<string?> fields,
        EffectAcceptedApplicationResult? value)
    {
        fields.Add(value?.ApplicationRef);
        fields.Add(value?.Disposition);
        fields.Add(value?.EffectId);
        fields.Add(value?.CreateTransitionId);
        fields.Add(value?.CreatedEventRef);
        fields.Add(value?.CausalEventRef);
        AppendSourceKey(fields, value?.SourceKey);
        AppendTargetKey(fields, value?.TargetKey);
        AppendCarrierCoordinate(fields, value?.CarrierCoordinate);
        var materialization = value?.Materialization;
        var slots = materialization?.SlotBindings;
        fields.Add(Count(slots));
        if (slots is not null)
        {
            for (var index = 0; index < slots.Count; index++)
            {
                var slot = slots[index];
                fields.Add(Number(index));
                fields.Add(slot is null ? null : Number(slot.Slot));
                fields.Add(slot?.ProfileKey);
                fields.Add(slot?.ReadableSummary);
            }
        }
        fields.Add(materialization is null
            ? null
            : Number(materialization.ComponentCount));
        fields.Add(materialization?.MaterializationFingerprint);
    }

    private static void AppendTerminationResult(
        ICollection<string?> fields,
        EffectAcceptedTerminationResult? value)
    {
        fields.Add(value?.OperationRef);
        fields.Add(value?.Disposition);
        fields.Add(value?.EffectId);
        fields.Add(value?.TerminalTransitionId);
        fields.Add(value?.TransitionEventRef);
        fields.Add(value?.CausalEventRef);
        AppendSourceKey(fields, value?.SourceKey);
        AppendTargetKey(fields, value?.TargetKey);
        AppendCarrierCoordinate(fields, value?.CarrierCoordinate);
    }

    private static void AppendSourceBinding(
        ICollection<string?> fields,
        EffectSourceAuthorityEntry? value)
    {
        AppendSourceKey(fields, value?.Key);
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(value?.Definition));
        fields.Add(value is null ? null : Boolean(value.Materializable));
        fields.Add(value is null ? null : Boolean(value.Active));
        fields.Add(value is null ? null : Boolean(value.SameTurn));
        fields.Add(value?.SourceRef);
        var predicates = value?.SatisfiedPredicates?
            .OrderBy(static item => item, StringComparer.Ordinal)
            .ToArray();
        fields.Add(predicates is null ? null : Number(predicates.Length));
        if (predicates is not null)
            AppendOrdered(fields, predicates);
        fields.Add(value?.RequiredApplicationAuthority);
    }

    private static void AppendReaction(
        ICollection<string?> fields,
        EffectReactionExecution? value)
    {
        fields.Add(value?.EventRef);
        fields.Add(value?.TriggerEventRef);
        fields.Add(value?.CausalEventRef);
        fields.Add(value is null ? null : Number(value.Turn));
        fields.Add(value?.EventKind);
        AppendTargetKey(fields, value?.Target);
        fields.Add(value?.EffectId);
        fields.Add(value?.TriggerId);
        fields.Add(value?.ComponentId);
        fields.Add(value?.ResultKind);
        fields.Add(value?.Dependency);
        fields.Add(value?.AfterComponentId);
        fields.Add(value is null ? null : Number(value.MaxExpansion));
        AppendSourceBinding(fields, value?.DownstreamSource);
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(value?.Parameters));
        AppendSourceKey(fields, value?.DownstreamSourceKey);
        fields.Add(value is null ? null : Number(value.ComponentPriority));
        fields.Add(value?.ReplacementTarget?.EffectId);
        fields.Add(value?.ReplacementTarget?.Authority.BindingKind);
        fields.Add(value?.ReplacementTarget?.Authority.AuthorityId);
    }

    private static void AppendCarrierBeforeImages(
        ICollection<string?> fields,
        IReadOnlyDictionary<string, JsonObject?> values)
    {
        var ordered = values.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .ToArray();
        fields.Add(Number(ordered.Length));
        for (var index = 0; index < ordered.Length; index++)
        {
            fields.Add(Number(index));
            fields.Add(ordered[index].Key);
            fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                ordered[index].Value));
        }
    }

    private static void AppendCarrierAfterImages(
        ICollection<string?> fields,
        IReadOnlyDictionary<string, JsonObject> values)
    {
        var ordered = values.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .ToArray();
        fields.Add(Number(ordered.Length));
        for (var index = 0; index < ordered.Length; index++)
        {
            fields.Add(Number(index));
            fields.Add(ordered[index].Key);
            fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                ordered[index].Value));
        }
    }

    private static void AppendCarrierContribution(
        ICollection<string?> fields,
        WoundCarrierContribution? value)
    {
        AppendOwner(fields, value?.Owner);
        fields.Add(value?.ExpectedWoundCollectionFingerprint);
        var mutations = value?.Mutations;
        fields.Add(Count(mutations));
        if (mutations is null)
            return;
        for (var index = 0; index < mutations.Count; index++)
        {
            var mutation = mutations[index];
            fields.Add(Number(index));
            fields.Add(mutation?.Operation);
            fields.Add(mutation?.WoundId);
            fields.Add(mutation?.BeforeWound is null
                ? null
                : WoundMaterializationContract.SerializeCanonical(
                    mutation.BeforeWound));
            fields.Add(mutation?.AfterWound is null
                ? null
                : WoundMaterializationContract.SerializeCanonical(
                    mutation.AfterWound));
        }
    }

    private static void AppendTransitionIntent(
        ICollection<string?> fields,
        WoundTransitionIntent? value)
    {
        switch (value)
        {
            case null:
                fields.Add(null);
                return;
            case WoundCarrierTransitionIntent intent:
                fields.Add(nameof(WoundCarrierTransitionIntent));
                fields.Add(intent.Operation);
                AppendOwner(fields, intent.Owner);
                fields.Add(intent.WoundId);
                return;
            case WoundEffectTransitionIntent intent:
                fields.Add(nameof(WoundEffectTransitionIntent));
                fields.Add(intent.Operation);
                fields.Add(intent.WoundId);
                AppendOrdered(fields, intent.BeforeEffectIds, includeCount: true);
                AppendOrdered(fields, intent.AfterEffectIds, includeCount: true);
                return;
            case WoundTransitionHistoryIntent intent:
                fields.Add(nameof(WoundTransitionHistoryIntent));
                fields.Add(intent.TransitionId);
                fields.Add(intent.WoundId);
                fields.Add(intent.Kind);
                fields.Add(intent.OperationKey);
                fields.Add(intent.EventRef);
                fields.Add(Number(intent.Turn));
                fields.Add(intent.BeforeFingerprint);
                fields.Add(intent.AfterFingerprint);
                fields.Add(intent.AttemptId);
                fields.Add(intent.TickKey);
                fields.Add(Boolean(intent.Terminal));
                return;
            case WoundAttemptTerminalIntent intent:
                fields.Add(nameof(WoundAttemptTerminalIntent));
                fields.Add(intent.AttemptId);
                fields.Add(intent.RouteOrGateRef);
                fields.Add(intent.WoundId);
                return;
            case WoundRecoverySealIntent intent:
                fields.Add(nameof(WoundRecoverySealIntent));
                fields.Add(intent.TickKey);
                fields.Add(intent.ClockOrCycleRef);
                fields.Add(intent.WoundId);
                return;
            case WoundFollowUpHealIntent intent:
                fields.Add(nameof(WoundFollowUpHealIntent));
                fields.Add(intent.WoundId);
                fields.Add(intent.AuthorityRef);
                return;
            case WoundCosmeticLegacyIntent intent:
                fields.Add(nameof(WoundCosmeticLegacyIntent));
                fields.Add(intent.LegacyId);
                fields.Add(intent.ProvenanceWoundId);
                fields.Add(intent.ReadableSummary);
                return;
            case WoundIndependentMechanicalLegacyIntent intent:
                fields.Add(nameof(WoundIndependentMechanicalLegacyIntent));
                fields.Add(intent.LegacyId);
                fields.Add(intent.EntityKind);
                fields.Add(intent.ProvenanceWoundId);
                fields.Add(intent.ReadableSummary);
                return;
            case WoundArchiveProjectionIntent intent:
                fields.Add(nameof(WoundArchiveProjectionIntent));
                fields.Add(intent.WoundId);
                fields.Add(intent.TerminalAuthorityRef);
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value.GetType().FullName,
                    "Unknown wound transition intent type.");
        }
    }

    private static void AppendOwner(
        ICollection<string?> fields,
        WoundOwnerCoordinate? value)
    {
        fields.Add(value?.Realm);
        fields.Add(value?.OwnerKind);
        fields.Add(value?.OwnerId);
        fields.Add(value?.CarrierPath);
    }

    private static void AppendOrdered(
        ICollection<string?> fields,
        IEnumerable<string?> values,
        bool includeCount = false)
    {
        var array = values.ToArray();
        if (includeCount)
            fields.Add(Number(array.Length));
        for (var index = 0; index < array.Length; index++)
        {
            fields.Add(Number(index));
            fields.Add(array[index]);
        }
    }

    private static string? Count<T>(IReadOnlyCollection<T>? values) =>
        values is null ? null : Number(values.Count);

    private static string Number(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string Boolean(bool value) => value ? "true" : "false";
}

internal static class WoundAcceptedTurnFingerprintWriter
{
    internal static string Compute(IEnumerable<string?> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var builder = new StringBuilder();
        foreach (var field in fields)
        {
            if (field is null)
            {
                builder.Append("-1:");
                continue;
            }
            builder.Append(Encoding.UTF8.GetByteCount(field)
                .ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(field);
        }
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    internal static string? CanonicalJson(JsonNode? node)
    {
        if (node is null)
            return null;
        return CanonicalizeJson(node)!.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = false
        });
    }

    private static JsonNode? CanonicalizeJson(JsonNode? node) => node switch
    {
        null => null,
        JsonObject value => new JsonObject(value
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => KeyValuePair.Create(
                pair.Key,
                CanonicalizeJson(pair.Value)))),
        JsonArray value => new JsonArray(value
            .Select(static item => CanonicalizeJson(item)).ToArray()),
        _ => node.DeepClone()
    };
}
