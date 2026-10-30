using BookOfEternityClient.Core;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

// Detached computational data. No AcceptedMechanicsInput, common authority,
// original, lease, reservation, fingerprint placeholder or publication grant.
internal sealed class AcceptedMechanicsCalculationInputs
{
    internal AcceptedMechanicsCalculationInputs(int turn, string realm,
        ResourceDefinitionCatalog definitions, ResourceStateLedger state, ResourceHistoryState history,
        ResourceOwnerAuthority owners, ResourceMutationSourceCatalog sources,
        ResourceCommandCompositionResult commands, JsonObject effectIdentityRoot, EffectAcceptedTurnPlan? effectPlan,
        IReadOnlyList<ResourceCapacityIntent> capacityTransitions, IReadOnlyList<ResourceOwnerCapacityDraft> ownerCapacityDrafts,
        IReadOnlyList<ResourceOwnerKey> terminalOwners, IReadOnlyDictionary<string, JsonObject> companions,
        IReadOnlyList<AcceptedMechanicsOwnerTransition> transitions, IReadOnlyList<IResourceRegisteredSystemOutcomeDraft> outcomes,
        ResourcePendingResolutionState? pending, AcceptedMechanicsIdentityFactory? resourceFactory,
        EffectIdentityFactory? effectFactory, int executionSequenceOffset,
        AcceptedMechanicsWoundStageBundle? bundle, MortalWoundCanonicalAnchorPlan? anchors)
    {
        Turn = turn; Realm = realm; Definitions = definitions; State = state; History = history;
        Owners = owners; Sources = sources; Commands = commands; _effectIdentity = effectIdentityRoot.DeepClone().AsObject();
        EffectPlan = effectPlan; CapacityTransitions = Array.AsReadOnly(capacityTransitions.ToArray());
        OwnerCapacityDrafts = Array.AsReadOnly(ownerCapacityDrafts.ToArray());
        TerminalOwners = Array.AsReadOnly(terminalOwners.ToArray());
        _companions = companions.ToDictionary(p => p.Key, p => p.Value.DeepClone().AsObject(), StringComparer.Ordinal);
        _transitions = transitions.Select(p => p.Clone()).ToArray();
        RegisteredSystemOutcomes = Array.AsReadOnly(outcomes.ToArray()); PendingResolutionState = pending;
        ResourceIdentityFactory = resourceFactory; EffectIdentityFactory = effectFactory;
        ExecutionSequenceOffset = executionSequenceOffset; _bundle = bundle?.DetachedCopy(); _anchors = anchors?.DetachedCopy();
    }
    private readonly JsonObject _effectIdentity;
    private readonly Dictionary<string, JsonObject> _companions;
    private readonly AcceptedMechanicsOwnerTransition[] _transitions;
    private readonly AcceptedMechanicsWoundStageBundle? _bundle;
    private readonly MortalWoundCanonicalAnchorPlan? _anchors;
    internal int Turn { get; }
    internal string Realm { get; }
    internal ResourceDefinitionCatalog Definitions { get; }
    internal ResourceStateLedger State { get; }
    internal ResourceHistoryState History { get; }
    internal ResourceOwnerAuthority Owners { get; }
    internal ResourceMutationSourceCatalog Sources { get; }
    internal ResourceCommandCompositionResult Commands { get; }
    internal JsonObject EffectIdentityRoot => _effectIdentity.DeepClone().AsObject();
    internal EffectAcceptedTurnPlan? EffectPlan { get; }
    internal IReadOnlyList<ResourceCapacityIntent> CapacityTransitions { get; }
    internal IReadOnlyList<ResourceOwnerCapacityDraft> OwnerCapacityDrafts { get; }
    internal IReadOnlyList<ResourceOwnerKey> TerminalOwners { get; }
    internal IReadOnlyDictionary<string, JsonObject> OwnerCompanionAfterImages =>
        _companions.ToDictionary(p => p.Key, p => p.Value.DeepClone().AsObject(), StringComparer.Ordinal);
    internal IReadOnlyList<AcceptedMechanicsOwnerTransition> OwnerTransitions =>
        Array.AsReadOnly(_transitions.Select(p => p.Clone()).ToArray());
    internal IReadOnlyList<IResourceRegisteredSystemOutcomeDraft> RegisteredSystemOutcomes { get; }
    internal ResourcePendingResolutionState? PendingResolutionState { get; }
    internal AcceptedMechanicsIdentityFactory? ResourceIdentityFactory { get; }
    internal EffectIdentityFactory? EffectIdentityFactory { get; }
    internal int ExecutionSequenceOffset { get; }
    internal AcceptedMechanicsWoundStageBundle? WoundStageBundle => _bundle?.DetachedCopy();
    internal MortalWoundCanonicalAnchorPlan? WoundAnchorPlan => _anchors?.DetachedCopy();
    internal static AcceptedMechanicsCalculationInputs FromContext(int turn, string realm,
        AcceptedMechanicsPlanningContext context) => new(turn, realm, context.Definitions, context.State,
        context.History, context.Owners, context.Sources, context.Commands, context.EffectIdentityRoot, context.EffectPlan,
        context.CapacityTransitions, context.OwnerCapacityDrafts, context.TerminalOwners, context.OwnerCompanionAfterImages,
        context.OwnerTransitions, context.RegisteredSystemOutcomes, context.PendingResolutionState,
        context.ResourceIdentityFactory, context.EffectIdentityFactory, context.ExecutionSequenceOffset,
        context.WoundStageBundle, context.WoundAnchorPlan);
}

internal static partial class AcceptedMechanicsPlanner
{
    internal static IReadOnlyList<ValidationIssue> ValidateUnboundedCommonBoundary(
        string sessionId, string requestId, int turn, ResourcePendingResolutionState? pendingState,
        JsonObject effectCommands, IReadOnlyList<AcceptedEffectBoundedResourceResolution> candidates)
    {
        if (pendingState?.Requests.Count > 0)
            return Issue("resource_pending_active_not_unbounded", "no active pending work in the held joint construction", "active pending");
        if (effectCommands.TryGetPropertyValue("effectResolutionReceipts", out var receipts))
        {
            if (receipts is not JsonArray array)
                return Issue("resource_pending_receipts_invalid", "effectResolutionReceipts array", receipts?.ToJsonString() ?? "null");
            if (array.Count != 0)
                return Issue("resource_pending_receipts_not_unbounded", "no submitted receipts in the held joint construction", "submitted receipts");
        }
        var terminal = ValidateMissingTerminalReceiptSet(sessionId, requestId, turn, pendingState);
        if (terminal.Count != 0) return terminal;
        return candidates.Count == 0 ? Array.Empty<ValidationIssue>() :
            Issue("resource_pending_candidates_not_unbounded", "no discovered bounded outputs in the held joint construction", "bounded outputs");
    }

    private static IReadOnlyList<ValidationIssue> ValidateMissingTerminalReceiptSet(
        string sessionId, string requestId, int turn, ResourcePendingResolutionState? pendingState) =>
        pendingState?.TerminalReceipts.Any(t => t.SessionId == sessionId &&
            t.AcceptedRequestId == requestId && t.RequestTurn == turn) == true
            ? Issue("resource_pending_terminal_replay_incomplete",
                "the complete terminal receipt set for semantic replay", "effectResolutionReceipts missing")
            : Array.Empty<ValidationIssue>();

    private sealed record CommonDiscovery(ResourceDefinitionCatalog Definitions,
        AcceptedMechanicsResourcePlanningResult Resources, EffectAcceptedTurnPlan? Effects,
        EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution BasePeriodicResolution,
        IReadOnlyList<ResourceMutationIntent> OrdinaryMutations, IReadOnlyList<ResourceCapacityIntent> CapacityTransitions,
        ResourceMutationSourceCatalog Sources);
    private sealed record CommonDiscoveryResult(CommonDiscovery? Discovery, IReadOnlyList<ValidationIssue> Issues);
    private static CommonDiscoveryResult FailedCommonDiscovery(IReadOnlyList<ValidationIssue> issues) => new(null, issues);
    private sealed record CommonCompletion(EffectAcceptedTurnPlan? Effects,
        ResourceRegisteredSystemOutcomeProjectionResult Projection);
    private sealed record CommonCompletionResult(CommonCompletion? Completion, IReadOnlyList<ValidationIssue> Issues);

    private static CommonDiscoveryResult DiscoverCommonMechanics(AcceptedMechanicsCalculationInputs calculation)
    {
        var woundStages = calculation.WoundStageBundle;
        if (woundStages is not null)
        {
            var woundEffectAgreement =
                AcceptedMechanicsCarrierAssembler.ValidateInitialEffectPlan(
                    calculation.EffectPlan,
                    woundStages);
            if (woundEffectAgreement.Count != 0)
            {
                return FailedCommonDiscovery(woundEffectAgreement);
            }
        }

        var issues = new List<ValidationIssue>();
        var definitions = calculation.Definitions;
        var sameTurnDefinitions = new Dictionary<string, ResourceDefinition>(
            StringComparer.Ordinal);
        if (calculation.Commands.DefinitionCreations.Count != 0)
        {
            var definitionBatch =
                ResourceDefinitionCatalog.BeginMaterializationBatch(
                    definitions);
            var definitionIdentityFactory = calculation.ResourceIdentityFactory
                ?? new AcceptedMechanicsIdentityFactory();
            foreach (var creation in calculation.Commands.DefinitionCreations
                         .OrderBy(static value => value.CommandOrdinal))
            {
                using var proposal = JsonDocument.Parse(
                    creation.Definition.ToJsonString());
                var materialized = definitionBatch.MaterializeProposal(
                    proposal.RootElement,
                    calculation.Turn,
                    creation.EventRef,
                    () => new ResourceDefinitionIdentity(
                        definitionIdentityFactory.CreateDefinitionId(creation, calculation.Turn),
                        definitionIdentityFactory.CreateDefinitionSeal(creation, calculation.Turn)));
                issues.AddRange(materialized.Issues);
                if (!materialized.IsValid || materialized.Definition == null)
                    continue;
                sameTurnDefinitions.Add(
                    creation.DefinitionRef,
                    materialized.Definition);
            }
            definitions = definitionBatch.Freeze();
        }

        var capacityTransitions = ComposeCapacityTransitions(
            calculation,
            definitions,
            sameTurnDefinitions,
            issues);
        var effectPlan = calculation.EffectPlan;
        var basePeriodicResolution = effectPlan == null
            ? new EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution(
                Array.Empty<ResourceMutationSourceExport>(),
                Array.Empty<ResourceMutationIntent>(),
                Array.Empty<ValidationIssue>())
            : EffectAcceptedTurnPlanner.ResolveDuePeriodicResourceMutations(
                effectPlan,
                calculation.Owners,
                definitions);
        var priorResolvedBindings = calculation.PendingResolutionState is
            { Requests.Count: > 0 } activePendingState
            ? SelectPendingReplayBindings(activePendingState)
            : Array.Empty<ResourcePendingResolvedBinding>();
        var discoveryReplay = new ResolvedPendingReplaySession(
            priorResolvedBindings);
        var periodicResolution = discoveryReplay.Bind(basePeriodicResolution);
        issues.AddRange(periodicResolution.Issues);
        var sources = calculation.Sources;
        if (periodicResolution.SourceExports.Count != 0)
        {
            var sourceResult = ResourceMutationSourceCatalog.Create(
                calculation.Sources.Exports.Concat(periodicResolution.SourceExports));
            issues.AddRange(sourceResult.Issues);
            if (sourceResult.Catalog != null)
                sources = sourceResult.Catalog;
        }
        var ordinaryMutations = ComposeOrdinaryMutations(
            calculation,
            definitions,
            issues)
            .Concat(calculation.RegisteredSystemOutcomes.SelectMany(static outcome => outcome.Mutations))
            .ToArray();
        var mutations = ordinaryMutations
            .Concat(periodicResolution.Mutations)
            .ToArray();
        if (issues.Count != 0)
            return FailedCommonDiscovery(issues);

        EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution ResolveAndCollect(
            ResourceAppliedEvent resourceEvent,
            ResourceOperationKey producer)
        {
            var expansion = EffectAcceptedTurnPlanner.ResolveResourceEventMutations(
                effectPlan!,
                resourceEvent,
                producer,
                calculation.Owners,
                definitions);
            return discoveryReplay.Bind(expansion);
        }

        var discoveryResourceResult = BuildResources(
            new AcceptedMechanicsResourceInput(
                calculation.Turn,
                definitions,
                calculation.State,
                calculation.History,
                sources,
                mutations,
                capacityTransitions,
                ExecutionSequenceOffset: calculation.ExecutionSequenceOffset,
                EventMutationResolver: effectPlan == null
                    ? null
                    : ResolveAndCollect,
                InitialTriggerCandidates: periodicResolution.TriggerCandidates,
                InitialEffectResolutionWork: periodicResolution.Work,
                EffectPlanAuthority: effectPlan == null
                    ? null
                    : CreateEffectPlanAuthority(effectPlan)),
            calculation.ResourceIdentityFactory ??
            new AcceptedMechanicsIdentityFactory());
        if (!discoveryResourceResult.IsValid ||
            discoveryResourceResult.StateAfterImage == null ||
            discoveryResourceResult.HistoryAfterImage == null)
        {
            return FailedCommonDiscovery(discoveryResourceResult.Issues);
        }
        var discoveryReplayIssues = discoveryReplay.ValidateComplete(
            discoveryResourceResult.AcceptedResolvedPendingRequestIds);
        if (discoveryReplayIssues.Count != 0)
        {
            return FailedCommonDiscovery(discoveryReplayIssues);
        }

        return new(new CommonDiscovery(definitions, discoveryResourceResult, effectPlan,
            basePeriodicResolution, ordinaryMutations, capacityTransitions, sources), Array.Empty<ValidationIssue>());
    }

    private static CommonCompletionResult CompleteCommonMechanics(AcceptedMechanicsCalculationInputs calculation,
        AcceptedMechanicsResourcePlanningResult resourceResult, EffectAcceptedTurnPlan? effectPlan)
    {
        var issues = new List<ValidationIssue>();
        if (effectPlan != null)
        {
            var finalizedEffects =
                EffectAcceptedTurnPlanner.CompleteAcceptedBoundaryTranscript(
                effectPlan,
                resourceResult.EffectBoundaryTranscript!,
                calculation.EffectIdentityFactory ?? new EffectIdentityFactory());
            if (!finalizedEffects.Success || finalizedEffects.Plan == null)
            {
                return new CommonCompletionResult(null, finalizedEffects.Issues);
            }
            effectPlan = finalizedEffects.Plan;
        }
        var ownerAgreementIssues = calculation.Owners.ValidateCanonicalAgreement(
            resourceResult.StateAfterImage,
            resourceResult.HistoryAfterImage);
        if (ownerAgreementIssues.Count != 0)
            return new CommonCompletionResult(null, ownerAgreementIssues);

        var projectedOwners = ProjectRegisteredSystemOutcomes(calculation.OwnerCompanionAfterImages, calculation.OwnerTransitions,
            calculation.RegisteredSystemOutcomes, resourceResult);
        issues.AddRange(projectedOwners.Issues);
        if (issues.Count != 0)
            return new CommonCompletionResult(null, issues);
        return new(new CommonCompletion(effectPlan, projectedOwners), Array.Empty<ValidationIssue>());
    }

    internal sealed record CommonArtifactBuildResult(CalculatedCommonArtifacts? Calculation, IReadOnlyList<ValidationIssue> Issues);

    // Immutable computed data, not a capability. Only the owning authority may
    // supply its own retained instance to the private final assembly below.
    internal sealed class CalculatedCommonArtifacts
    {
        private readonly JsonObject _definitions, _state, _history, _effectIdentity;
        private readonly JsonObject? _pending;
        private readonly Dictionary<string, JsonObject> _effects, _companions;
        private readonly AcceptedMechanicsOwnerTransition[] _transitions;
        internal CalculatedCommonArtifacts(JsonObject definitions, JsonObject state, JsonObject history,
            IReadOnlyDictionary<string, JsonObject> effects, JsonObject effectIdentity, JsonObject? pending,
            IReadOnlyDictionary<string, JsonObject> companions, IReadOnlyList<AcceptedMechanicsOwnerTransition> transitions,
            IReadOnlyList<string> touched, IReadOnlyList<string> consumed,
            AcceptedMechanicsResourcePlanningResult resources, EffectAcceptedTurnPlan? completedEffects,
            AcceptedMechanicsCarrierCompositionResult carrierComposition)
        {
            _definitions = definitions.DeepClone().AsObject(); _state = state.DeepClone().AsObject();
            _history = history.DeepClone().AsObject(); _effectIdentity = effectIdentity.DeepClone().AsObject();
            _pending = pending?.DeepClone().AsObject(); _effects = CloneObjects(effects); _companions = CloneObjects(companions);
            _transitions = transitions.Select(t => t.Clone()).ToArray();
            TouchedPaths = Array.AsReadOnly(touched.ToArray()); ConsumedPaths = Array.AsReadOnly(consumed.ToArray());
            Resources = resources; Effects = completedEffects; CarrierComposition = carrierComposition;
        }
        private static Dictionary<string, JsonObject> CloneObjects(IReadOnlyDictionary<string, JsonObject> roots) =>
            roots.ToDictionary(p => p.Key, p => p.Value.DeepClone().AsObject(), StringComparer.Ordinal);
        internal JsonObject DefinitionAfterImage => _definitions.DeepClone().AsObject();
        internal JsonObject StateAfterImage => _state.DeepClone().AsObject();
        internal JsonObject HistoryAfterImage => _history.DeepClone().AsObject();
        internal JsonObject EffectIdentityAfterImage => _effectIdentity.DeepClone().AsObject();
        internal JsonObject? PendingAfterImage => _pending?.DeepClone().AsObject();
        internal IReadOnlyDictionary<string, JsonObject> EffectCarrierAfterImages => CloneObjects(_effects);
        internal IReadOnlyDictionary<string, JsonObject> OwnerCompanionAfterImages => CloneObjects(_companions);
        internal IReadOnlyList<AcceptedMechanicsOwnerTransition> OwnerTransitions => Array.AsReadOnly(_transitions.Select(t => t.Clone()).ToArray());
        internal IReadOnlyList<string> TouchedPaths { get; }
        internal IReadOnlyList<string> ConsumedPaths { get; }
        internal AcceptedMechanicsResourcePlanningResult Resources { get; }
        internal EffectAcceptedTurnPlan? Effects { get; }
        internal AcceptedMechanicsCarrierCompositionResult CarrierComposition { get; }
        internal IReadOnlyDictionary<string, JsonObject> WoundCarrierAfterImages => CarrierComposition.WoundPublication?.CarrierAfterImages ?? new Dictionary<string, JsonObject>();
        internal JsonObject? WoundIdentityAfterImage => CarrierComposition.WoundPublication?.IdentityAfterImage;
        internal JsonObject? WoundHistoryAfterImage => CarrierComposition.WoundPublication?.HistoryAfterImage;
    }

    internal static CommonArtifactBuildResult CalculateUnboundedCommonArtifacts(
        AcceptedMechanicsCalculationInputs calculation, string sessionId, string requestId, JsonObject effectCommands)
    {
        var boundary = ValidateUnboundedCommonBoundary(sessionId, requestId, calculation.Turn,
            calculation.PendingResolutionState, effectCommands, Array.Empty<AcceptedEffectBoundedResourceResolution>());
        if (boundary.Count != 0) return new(null, boundary);
        var discovered = DiscoverCommonMechanics(calculation);
        if (discovered.Discovery is not { } discovery) return new(null, discovered.Issues);
        boundary = ValidateUnboundedCommonBoundary(sessionId, requestId, calculation.Turn,
            calculation.PendingResolutionState, effectCommands, discovery.Resources.AcceptedPendingResolutions);
        if (boundary.Count != 0) return new(null, boundary);
        var completion = CompleteCommonMechanics(calculation, discovery.Resources, discovery.Effects);
        if (completion.Completion is not { } complete) return new(null, completion.Issues);
        return AssembleCalculatedCommonArtifacts(calculation, discovery.Definitions, discovery.Resources, complete.Effects,
            null, complete.Projection.CompanionAfterImages, complete.Projection.OwnerTransitions,
            null, false, hasWoundCommands: true, isJointRepair: true);
    }

    private static CommonArtifactBuildResult AssembleCalculatedCommonArtifacts(
        AcceptedMechanicsCalculationInputs calculation, ResourceDefinitionCatalog definitions,
        AcceptedMechanicsResourcePlanningResult resourceResult, EffectAcceptedTurnPlan? effectPlan,
        JsonObject? pendingAfterImage, IReadOnlyDictionary<string, JsonObject> companions,
        IReadOnlyList<AcceptedMechanicsOwnerTransition> transitions, SpiritualLiveWoundCompletion? liveWounds,
        bool hasMortalProducerCompletion, bool hasWoundCommands, bool isJointRepair)
    {
        var woundStages = calculation.WoundStageBundle;
        var definitionAfterImage = definitions.ToCanonicalRoot();
        var stateAfterImage = JsonNode.Parse(resourceResult.StateAfterImage!.ToCanonicalJson())!.AsObject();
        var historyAfterImage = JsonNode.Parse(resourceResult.HistoryAfterImage!.ToCanonicalJson())!.AsObject();
        var ownerCompanionAfterImages = companions.ToDictionary(
            static pair => pair.Key, static pair => pair.Value.DeepClone().AsObject(), StringComparer.Ordinal);
        var ownerTransitions = transitions.Select(static value => value.Clone()).ToList();
        var issues = new List<ValidationIssue>();
        var serviceOwner = woundStages?.SpiritualHealingContinuation;
        if (serviceOwner?.HasWalletContribution == true)
        {
            try
            {
                ownerCompanionAfterImages[AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath] =
                    serviceOwner.ComposeServiceSoulAfterImage(ownerCompanionAfterImages.GetValueOrDefault(
                        AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath));
            }
            catch (InvalidDataException failure)
            { return new(null, Issue("spiritual_service_wallet_conflict", "exact original wallet and one service payment", failure.Message)); }
        }
        var carrierComposition = liveWounds is null
            ? AcceptedMechanicsCarrierAssembler.Compose(
                effectPlan, ownerCompanionAfterImages, woundStages,
                calculation.WoundAnchorPlan)
            : AcceptedMechanicsCarrierAssembler.ComposeLive(
                effectPlan, ownerCompanionAfterImages, liveWounds, woundStages);
        if (!carrierComposition.Success)
        {
            return new CommonArtifactBuildResult(
                null,
                carrierComposition.Issues);
        }
        var carriers = carrierComposition.EffectCarrierAfterImages.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.DeepClone().AsObject(),
            StringComparer.Ordinal);
        ownerCompanionAfterImages =
            carrierComposition.OwnerCompanionAfterImages.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.DeepClone().AsObject(),
                StringComparer.Ordinal);
        var woundPublication = carrierComposition.WoundPublication;
        if (serviceOwner?.HasWalletContribution == true)
        {
            try
            {
                var profiles = carriers.GetValueOrDefault(WoundCarrierCatalog.AfterlifeProfilesPath) ??
                    woundPublication?.CarrierAfterImages.GetValueOrDefault(WoundCarrierCatalog.AfterlifeProfilesPath);
                serviceOwner.RequireServiceComposedImages(ownerCompanionAfterImages[AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath],
                    profiles ?? throw new InvalidDataException("Service wound carrier is absent."),
                    woundPublication?.HistoryAfterImage ?? throw new InvalidDataException("Service History is absent."),
                    ownerCompanionAfterImages.GetValueOrDefault(GuardianAbodeResidentState.StatePath),
                    ownerCompanionAfterImages.GetValueOrDefault(ShiningAbodeState.StatePath));
            }
            catch (InvalidDataException failure)
            { return new(null, Issue("spiritual_service_payment_composition_invalid", "one exact paid treatment common plan", failure.Message)); }
        }
        foreach (var transitionPath in ownerTransitions
                     .Select(static value => value.Path)
                     .Distinct(StringComparer.Ordinal))
        {
            if (ownerCompanionAfterImages.ContainsKey(transitionPath) ||
                carriers.ContainsKey(transitionPath) ||
                (woundPublication?.CarrierAfterImages.ContainsKey(transitionPath) ??
                 false))
            {
                issues.AddRange(Issue(
                    "accepted_mechanics_owner_transition_path_conflict",
                    "one typed owner transition or one whole-root after-image producer per path",
                    transitionPath));
            }
        }
        if (issues.Count != 0)
            return new CommonArtifactBuildResult(null, issues);
        var effectIdentity = effectPlan?.IdentityIndexAfterImage ??
            calculation.EffectIdentityRoot;
        var touched = new HashSet<string>(StringComparer.Ordinal)
        {
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            EffectAcceptedTurnPlan.IdentityIndexPath
        };
        if (!calculation.Commands.IsMissing)
            touched.Add(ResourceMaterializationContract.CommandPath);
        if (effectPlan != null)
        {
            touched.UnionWith(effectPlan.TouchedPaths);
            touched.UnionWith(effectPlan.DeletedPaths);
        }
        if (woundPublication is not null)
        {
            touched.Add(AcceptedMechanicsPlan.WoundCommandPath);
            touched.UnionWith(woundPublication.CarrierAfterImages.Keys);
            touched.Add(WoundIdentityState.StatePath);
            touched.Add(WoundHistoryState.HistoryPath);
        }
        touched.UnionWith(ownerCompanionAfterImages.Keys);
        if (hasMortalProducerCompletion)
        {
            touched.UnionWith(QtePhysicalInjurySource.WoundRootPaths);
            touched.Add("input/turn_request.json");
        }
        touched.UnionWith(ownerTransitions.Select(static value => value.Path));
        if (pendingAfterImage != null)
            touched.Add(ResourcePendingResolutionState.PendingPath);
        var consumed = new HashSet<string>(StringComparer.Ordinal);
        if (!calculation.Commands.IsMissing)
            consumed.Add(ResourceMaterializationContract.CommandPath);
        if (effectPlan != null)
            consumed.UnionWith(effectPlan.DeletedPaths);
        if (woundPublication is not null)
            consumed.Add(AcceptedMechanicsPlan.WoundCommandPath);
        if (ownerCompanionAfterImages.ContainsKey(SpiritualWoundOpportunityReceiptState.StatePath))
        {
            var spiritualControls = new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath };
            touched.UnionWith(spiritualControls);
            consumed.UnionWith(spiritualControls);
        }
        if (ownerCompanionAfterImages.ContainsKey(MortalWoundOpportunityReceiptState.StatePath))
        {
            touched.Add(MortalQteDecisionPublication.StatePath);
            var mortalControls = hasWoundCommands
                ? new[] { MortalWoundOpportunityAdapter.DecisionPath, AcceptedMechanicsPlan.WoundCommandPath }
                : new[] { MortalWoundOpportunityAdapter.DecisionPath };
            touched.UnionWith(mortalControls);
            consumed.UnionWith(mortalControls);
            touched.Add("output/narrative_response.json");
        }

        if (woundStages?.PreparedPlan.SpiritualHealingContinuationAuthority is not null)
        {
            touched.Add(ProgressionScheduleService.ReportPath);
            consumed.Add(ProgressionScheduleService.ReportPath);
        }

        if (isJointRepair)
        {
            touched.Add(WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
            consumed.Add(WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
        }

        return new(new CalculatedCommonArtifacts(definitionAfterImage, stateAfterImage, historyAfterImage,
            carriers, effectIdentity, pendingAfterImage, ownerCompanionAfterImages, ownerTransitions,
            touched.ToArray(), consumed.ToArray(), resourceResult, effectPlan, carrierComposition), Array.Empty<ValidationIssue>());
    }

    /// <summary>
    /// Assembles a joint plan from the authority's retained calculation after checking the bound input.
    /// </summary>
    /// <param name="input">
    /// The complete input bearing its issued joint common authority.
    /// </param>
    /// <param name="fingerprint">
    /// The expected planning input fingerprint checked before assembly.
    /// </param>
    /// <returns>
    /// The assembled joint candidate, or refusal issues when the bound input does not agree.
    /// </returns>
    internal static AcceptedMechanicsPlanningResult AssembleRetainedJointPlan(AcceptedMechanicsInput input, string fingerprint)
    {
        using var operationTiming = FileSystemManager.MeasureCurrentOperation("JointCommon.AssembleRetainedPlan");
        if (input.PlanningContext?.JointRepairCommonAuthority is not { } authority || !authority.MatchesInput(input) ||
            fingerprint != AcceptedMechanicsPlanFingerprints.ComputePlanningInput(input))
            return new(null, Issue("accepted_mechanics_joint_retained_input_invalid", "the exact bound joint input", "foreign input"));
        return AssembleCalculatedPlan(input, fingerprint, authority.RetainedCommonCalculation, null);
    }

    private static AcceptedMechanicsPlanningResult AssembleCalculatedPlan(AcceptedMechanicsInput input,
        string inputFingerprint, CalculatedCommonArtifacts calculated, SpiritualLiveWoundCompletion? liveWounds)
    {
        var context = input.PlanningContext!;
        var woundStages = context.WoundStageBundle;
        return new AcceptedMechanicsPlanningResult(
            new AcceptedMechanicsPlan(
                inputFingerprint,
                calculated.DefinitionAfterImage,
                calculated.StateAfterImage,
                calculated.HistoryAfterImage,
                calculated.EffectCarrierAfterImages,
                calculated.EffectIdentityAfterImage,
                calculated.PendingAfterImage is not { } pendingAfterImage
                    ? new Dictionary<string, JsonObject?>()
                    : new Dictionary<string, JsonObject?> { [ResourcePendingResolutionState.PendingPath] = pendingAfterImage },
                calculated.OwnerCompanionAfterImages,
                input.BeforeImages,
                calculated.TouchedPaths,
                calculated.ConsumedPaths,
                input.AuthorityFingerprints,
                calculated.Resources.Events,
                new ResourceProjectionInput(
                    calculated.DefinitionAfterImage,
                    calculated.StateAfterImage,
                    calculated.HistoryAfterImage,
                    context.Owners.Fingerprint),
                context.Owners,
                calculated.Effects,
                ownerTransitions: calculated.OwnerTransitions,
                woundStageBundle: woundStages,
                carrierComposition: woundStages is null && liveWounds is null
                    ? null
                    : calculated.CarrierComposition,
                liveWoundCompletion: liveWounds,
                directWoundPublicationAuthority:
                    context.DirectWoundPublicationAuthority,
                treatmentResourcePublicationAuthority:
                    context.TreatmentResourcePublicationAuthority,
                jointRepairCommonAuthority: context.JointRepairCommonAuthority),
            Array.Empty<ValidationIssue>());
    }
}
