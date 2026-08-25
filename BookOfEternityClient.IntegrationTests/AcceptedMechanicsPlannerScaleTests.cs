using System.Globalization;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AcceptedMechanicsPlannerScaleTests
{
    private const string FingerprintA =
        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string FingerprintB =
        "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public void Planner_EnforcesExactCapacityAndPreTriggerMutationBoundaries()
    {
        var acceptedCapacity = PlanCapacityTransitions(
            ResourceMaterializationContract.MaxCapacityTransitionsPerTurn);
        var rejectedCapacity = PlanCapacityTransitions(
            ResourceMaterializationContract.MaxCapacityTransitionsPerTurn + 1);

        Assert.True(acceptedCapacity.IsValid, Format(acceptedCapacity.Issues));
        Assert.Equal(256, acceptedCapacity.AppliedTransitions.Count);
        Assert.Equal(256, acceptedCapacity.Statistics.CapacityDescriptorCount);
        Assert.Contains(rejectedCapacity.Issues, issue =>
            issue.Code == "resource_planner_capacity_limit_exceeded" &&
            issue.Actual == "257");
        Assert.Null(rejectedCapacity.StateAfterImage);
        Assert.Null(rejectedCapacity.HistoryAfterImage);

        var acceptedMutations = PlanMutations(
            ResourceMaterializationContract.MaxMutationsBeforeTriggers,
            sourceKind: "registered_system_outcome");
        var rejectedMutations = PlanMutations(
            ResourceMaterializationContract.MaxMutationsBeforeTriggers + 1,
            sourceKind: "registered_system_outcome");

        Assert.True(acceptedMutations.IsValid, Format(acceptedMutations.Issues));
        Assert.Equal(512, acceptedMutations.AppliedTransitions.Count);
        Assert.Equal(512, acceptedMutations.Statistics.MutationDescriptorCount);
        Assert.Equal(512, acceptedMutations.Statistics.GraphNodeDescriptorCount);
        Assert.Equal(512, acceptedMutations.Statistics.SchedulingDescriptorVisitCount);
        Assert.Contains(rejectedMutations.Issues, issue =>
            issue.Code == "resource_planner_mutation_limit_exceeded" &&
            issue.Actual == "513");
        Assert.Null(rejectedMutations.StateAfterImage);
        Assert.Null(rejectedMutations.HistoryAfterImage);
    }

    [Fact]
    public void Planner_EnforcesFinalLiveEntryLimitAndAllowsRetireReplaceAtBoundary()
    {
        var below = CreateLiveEntryBaseline(
            ResourceMaterializationContract.MaxLiveEntries - 1);
        var boundary = CreateLiveEntryBaseline(
            ResourceMaterializationContract.MaxLiveEntries);
        var initialize = CapacityIntent(
            ResourceMaterializationContract.MaxCapacityTransitionsPerTurn - 1,
            below.Definition);

        var accepted = PlanCapacityTransitions(below, new[] { initialize });
        var rejected = PlanCapacityTransitions(boundary, new[] { initialize });
        var replaced = PlanCapacityTransitions(
            boundary,
            new[]
            {
                RetireIntent(boundary.State.Entries[0]),
                initialize
            });

        Assert.True(accepted.IsValid, Format(accepted.Issues));
        Assert.Equal(
            ResourceMaterializationContract.MaxLiveEntries,
            accepted.StateAfterImage!.Entries.Count);
        Assert.False(rejected.IsValid);
        Assert.Contains(rejected.Issues, issue =>
            issue.Code == "resource_state_limit_exceeded" &&
            issue.Actual == "20001");
        Assert.Null(rejected.StateAfterImage);
        Assert.Null(rejected.HistoryAfterImage);
        Assert.True(replaced.IsValid, Format(replaced.Issues));
        Assert.Equal(
            ResourceMaterializationContract.MaxLiveEntries,
            replaced.StateAfterImage!.Entries.Count);
        Assert.Equal(2, replaced.AppliedTransitions.Count);
    }

    [Fact]
    public void Planner_DoubledTriggerPopulationStaysWithinTwoPointFiveTimesExactWork()
    {
        var one = PlanMutations(512, sourceKind: "effect_component");
        var two = PlanMutations(1_024, sourceKind: "effect_component");
        var rejected = PlanMutations(1_025, sourceKind: "effect_component");

        Assert.True(one.IsValid, Format(one.Issues));
        Assert.True(two.IsValid, Format(two.Issues));
        Assert.Equal(512, one.Statistics.MutationDescriptorCount);
        Assert.Equal(512, one.Statistics.GraphNodeDescriptorCount);
        Assert.Equal(512, one.Statistics.SchedulingDescriptorVisitCount);
        Assert.Equal(1_024, two.Statistics.MutationDescriptorCount);
        Assert.Equal(1_024, two.Statistics.GraphNodeDescriptorCount);
        Assert.Equal(1_024, two.Statistics.SchedulingDescriptorVisitCount);
        Assert.Equal(1_024, two.AppliedTransitions.Count);
        Assert.True(one.Statistics.TotalWorkUnits > 0);
        Assert.True(
            two.Statistics.TotalWorkUnits <= one.Statistics.TotalWorkUnits * 2.5,
            $"Expected near-linear accepted-plan work, but " +
            $"{one.Statistics.TotalWorkUnits} units became " +
            $"{two.Statistics.TotalWorkUnits}.");
        Assert.Contains(rejected.Issues, issue =>
            issue.Code == "resource_graph_node_limit_exceeded" &&
            issue.Actual == "1025");
        Assert.Null(rejected.StateAfterImage);
        Assert.Null(rejected.HistoryAfterImage);
    }

    [Fact]
    public void Planner_DerivedAmountsUseOneIndexedReplayLookupPerMutation()
    {
        var result = PlanDerivedMutations(
            ResourceMaterializationContract.MaxMutationsBeforeTriggers / 2);

        Assert.True(result.IsValid, Format(result.Issues));
        Assert.Equal(512, result.Statistics.MutationDescriptorCount);
        Assert.Equal(512, result.Statistics.GraphNodeDescriptorCount);
        Assert.Equal(512, result.Statistics.SchedulingDescriptorVisitCount);
        Assert.Equal(256, result.Statistics.HistoryReplayIdentityLookupCount);
        Assert.Equal(1_792, result.Statistics.HistoryWorkUnits);
        Assert.Equal(3_328, result.Statistics.TotalWorkUnits);
        Assert.Equal(512, result.AppliedTransitions.Count);
    }

    [Fact]
    public void Planner_EnforcesExactTriggerDepthBoundary()
    {
        var accepted = PlanMutations(
            ResourceMaterializationContract.MaxTriggerDepth,
            sourceKind: "effect_component",
            chainDependencies: true);
        var rejected = PlanMutations(
            ResourceMaterializationContract.MaxTriggerDepth + 1,
            sourceKind: "effect_component",
            chainDependencies: true);

        Assert.True(accepted.IsValid, Format(accepted.Issues));
        Assert.Equal(32, accepted.Statistics.MaximumTriggerDepth);
        Assert.Equal(32, accepted.AppliedTransitions.Count);
        Assert.Contains(rejected.Issues, issue =>
            issue.Code == "resource_graph_depth_limit_exceeded" &&
            issue.Actual == "33");
        Assert.Null(rejected.StateAfterImage);
        Assert.Null(rejected.HistoryAfterImage);
    }

    [Fact]
    public void PendingResolution_EnforcesExactRequestBoundary()
    {
        var accepted = CreatePending(ResourceMaterializationContract.MaxPendingRequestsPerTurn);
        var rejected = CreatePending(
            ResourceMaterializationContract.MaxPendingRequestsPerTurn + 1);

        Assert.True(accepted.IsValid, Format(accepted.Issues));
        Assert.Equal(64, accepted.State!.Requests.Count);
        Assert.Equal(64, accepted.SafeGmPacket!["requests"]!.AsArray().Count);
        Assert.Contains(rejected.Issues, issue =>
            issue.Code == "resource_pending_request_limit_exceeded" &&
            issue.Actual == "65");
        Assert.Null(rejected.State);
        Assert.Null(rejected.SafeGmPacket);
    }

    private static AcceptedMechanicsResourcePlanningResult PlanCapacityTransitions(int count)
    {
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        Assert.True(definitions.TryResolveExact("charges", out var definition));
        var history = ResourceHistoryState.CreateValidated(
            Array.Empty<ResourceTransition>(),
            definitions);
        Assert.True(history.IsValid, Format(history.Issues));
        var sources = CreateSourceCatalog();
        var transitions = Enumerable.Range(0, count)
            .Select(index => CapacityIntent(index, definition!))
            .ToArray();

        return AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 2,
                Definitions: definitions,
                State: new ResourceStateLedger(Array.Empty<ResourceStateEntry>()),
                History: history.History!,
                Sources: sources,
                Mutations: Array.Empty<ResourceMutationIntent>(),
                CapacityTransitions: transitions),
            IdentityFactory());
    }

    private static AcceptedMechanicsResourcePlanningResult PlanCapacityTransitions(
        LiveEntryBaseline baseline,
        IReadOnlyList<ResourceCapacityIntent> transitions) =>
        AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 2,
                Definitions: baseline.Definitions,
                State: baseline.State,
                History: baseline.History,
                Sources: CreateSourceCatalog(),
                Mutations: Array.Empty<ResourceMutationIntent>(),
                CapacityTransitions: transitions),
            IdentityFactory());

    private static LiveEntryBaseline CreateLiveEntryBaseline(int count)
    {
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        Assert.True(definitions.TryResolveExact("charges", out var definition));
        var states = new List<ResourceStateEntry>(count);
        var transitions = new List<ResourceTransition>(count);
        for (var index = 0; index < count; index++)
        {
            var suffix = index.ToString("D5", CultureInfo.InvariantCulture);
            var coordinate = new ResourceCoordinate(
                "mortal_world",
                ResourceOwnerKind.Item,
                "live_scale_item_" + suffix,
                "charges");
            var binding = new ResourceCapacityBinding(
                ResourceCapacityKind.InstanceFixed,
                "live_scale_capacity_" + suffix,
                FingerprintA);
            var snapshot = new ResourceStateSnapshot(
                Current: 1m,
                Maximum: 1m,
                binding,
                ResourceLifecycleState.Active);
            var transition = new ResourceTransition(
                TransitionId: "live_scale_initialize_transition_" + suffix,
                OperationId: "live_scale_initialize_operation_" + suffix,
                EventRef: "turn_1:live_scale_initialize:" + suffix,
                OriginKind: "setting_materialization",
                OriginId: "live_scale_item_" + suffix,
                Phase: ResourceMutationPhase.RegisteredSystemOutcome,
                Priority: 50,
                ExecutionSequence: index,
                Coordinate: coordinate,
                Operation: ResourceTransitionOperation.Initialize,
                RequestedAmount: 0m,
                AppliedAmount: 0m,
                Outcome: ResourceTransitionOutcome.Applied,
                CapacityDisposition: ResourceCapacityDisposition.InitializeFromDefinition,
                BeforeState: null,
                AfterState: snapshot,
                SourceEvidence: new ResourceSourceEvidence(
                    "setting_materialization",
                    "live_scale_item_" + suffix,
                    FingerprintA),
                PolicyFingerprint: FingerprintA,
                ReceiptId: null,
                Turn: 1);
            transitions.Add(transition);
            states.Add(new ResourceStateEntry(
                coordinate,
                snapshot.Current,
                snapshot.Maximum,
                binding,
                snapshot.State,
                new ResourceChronology(
                    CreatedAtTurn: 1,
                    CreatedEventRef: transition.EventRef,
                    LastTransitionId: transition.TransitionId,
                    LastEventRef: transition.EventRef,
                    LastTransitionTurn: 1)));
        }

        var history = ResourceHistoryState.CreateValidated(transitions, definitions);
        Assert.True(history.IsValid, Format(history.Issues));
        var state = new ResourceStateLedger(states);
        Assert.Empty(history.History!.ValidateStateAgreement(state));
        return new LiveEntryBaseline(
            definitions,
            definition!,
            state,
            history.History);
    }

    private static ResourceCapacityIntent RetireIntent(ResourceStateEntry entry) => new(
        EventRef: "turn_2:live_scale_retire",
        OriginKind: "owner_lifecycle",
        OriginId: entry.Coordinate.ResourceOwnerId,
        Coordinate: entry.Coordinate,
        Operation: ResourceCapacityOperation.Retire,
        ResolvedCapacity: null,
        CurrentDisposition: null,
        Phase: ResourceMutationPhase.RegisteredSystemOutcome,
        Priority: 0,
        SourceEvidence: new ResourceSourceEvidence(
            "owner_lifecycle",
            entry.Coordinate.ResourceOwnerId,
            FingerprintA),
        PolicyFingerprint: FingerprintA,
        ReceiptId: null);

    private static ResourceCapacityIntent CapacityIntent(
        int index,
        ResourceDefinition definition)
    {
        var suffix = index.ToString("D4", CultureInfo.InvariantCulture);
        var ownerId = "scale_item_" + suffix;
        var coordinate = new ResourceCoordinate(
            "mortal_world",
            ResourceOwnerKind.Item,
            ownerId,
            "charges");
        var capacity = ResolvedResourceCapacity.Resolve(
            definition,
            coordinate,
            new InstanceFixedCapacityInput(
                new ResourceFormulaOwner(
                    coordinate.Realm,
                    coordinate.OwnerKind,
                    coordinate.ResourceOwnerId),
                Maximum: 10m,
                CapacityAuthorityFingerprint: FingerprintA),
            instanceAuthorityKey: "scale_capacity_" + suffix,
            includeInitialization: true);
        Assert.True(capacity.IsValid, Format(capacity.Issues));

        return new ResourceCapacityIntent(
            EventRef: "turn_2:capacity:" + suffix,
            OriginKind: "setting_materialization",
            OriginId: ownerId,
            Coordinate: coordinate,
            Operation: ResourceCapacityOperation.Initialize,
            ResolvedCapacity: capacity.Capacity,
            CurrentDisposition: ResourceCurrentDisposition.InitializeFromDefinition,
            Phase: ResourceMutationPhase.RegisteredSystemOutcome,
            Priority: 50,
            SourceEvidence: new ResourceSourceEvidence(
                "setting_materialization",
                ownerId,
                FingerprintA),
            PolicyFingerprint: capacity.Capacity!.Initialization!.AuthorityFingerprint,
            ReceiptId: null);
    }

    private static AcceptedMechanicsResourcePlanningResult PlanMutations(
        int count,
        string sourceKind,
        bool chainDependencies = false)
    {
        var baseline = CreateMutationBaseline(count, sourceKind);
        var mutations = new List<ResourceMutationIntent>(count);
        ResourceOperationKey? previous = null;
        for (var index = 0; index < count; index++)
        {
            var suffix = index.ToString("D4", CultureInfo.InvariantCulture);
            var operation = index % 2 == 0
                ? ResourceOperation.Spend
                : ResourceOperation.Gain;
            var mutation = new ResourceMutationIntent(
                EventRef: "turn_2:scale:" + suffix,
                Coordinate: baseline.Coordinate,
                Amount: 1m,
                Source: new ResourceMutationSourceRequest(
                    sourceKind,
                    SourceId(sourceKind),
                    operation),
                Dependencies: chainDependencies && previous != null
                    ? new[] { previous }
                    : Array.Empty<ResourceOperationKey>(),
                EventRequirements: Array.Empty<ResourceMutationEventRequirement>(),
                ReceiptId: null);
            mutations.Add(mutation);
            previous = mutation.Key;
        }

        return AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 2,
                Definitions: baseline.Definitions,
                State: baseline.State,
                History: baseline.History,
                Sources: baseline.Sources,
                Mutations: mutations),
            IdentityFactory());
    }

    private static AcceptedMechanicsResourcePlanningResult PlanDerivedMutations(
        int coordinateCount)
    {
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var states = new List<ResourceStateEntry>(coordinateCount);
        var transitions = new List<ResourceTransition>(coordinateCount);
        var mutations = new List<ResourceMutationIntent>(coordinateCount * 2);
        var sourceExports = new List<ResourceMutationSourceExport>(coordinateCount + 1)
        {
            new(
                "registered_system_outcome",
                "derived_scale_system",
                FingerprintB,
                ResourceMutationSourceState.Active,
                SameTurn: false)
        };
        for (var index = 0; index < coordinateCount; index++)
        {
            var suffix = index.ToString("D4", CultureInfo.InvariantCulture);
            var coordinate = new ResourceCoordinate(
                "mortal_world",
                ResourceOwnerKind.Item,
                "derived_scale_item_" + suffix,
                "charges");
            var binding = new ResourceCapacityBinding(
                ResourceCapacityKind.InstanceFixed,
                "derived_scale_capacity_" + suffix,
                FingerprintA);
            var snapshot = new ResourceStateSnapshot(
                Current: 3m,
                Maximum: 3m,
                binding,
                ResourceLifecycleState.Active);
            var transition = new ResourceTransition(
                TransitionId: "derived_scale_initialize_transition_" + suffix,
                OperationId: "derived_scale_initialize_operation_" + suffix,
                EventRef: "turn_1:derived_scale_initialize:" + suffix,
                OriginKind: "setting_materialization",
                OriginId: "derived_scale_item_" + suffix,
                Phase: ResourceMutationPhase.RegisteredSystemOutcome,
                Priority: 50,
                ExecutionSequence: index,
                Coordinate: coordinate,
                Operation: ResourceTransitionOperation.Initialize,
                RequestedAmount: 0m,
                AppliedAmount: 0m,
                Outcome: ResourceTransitionOutcome.Applied,
                CapacityDisposition: ResourceCapacityDisposition.InitializeFromDefinition,
                BeforeState: null,
                AfterState: snapshot,
                SourceEvidence: new ResourceSourceEvidence(
                    "setting_materialization",
                    "derived_scale_item_" + suffix,
                    FingerprintA),
                PolicyFingerprint: FingerprintA,
                ReceiptId: null,
                Turn: 1);
            transitions.Add(transition);
            states.Add(new ResourceStateEntry(
                coordinate,
                snapshot.Current,
                snapshot.Maximum,
                binding,
                snapshot.State,
                new ResourceChronology(
                    CreatedAtTurn: 1,
                    CreatedEventRef: transition.EventRef,
                    LastTransitionId: transition.TransitionId,
                    LastEventRef: transition.EventRef,
                    LastTransitionTurn: 1)));
            var owner = new ResourceOwnerKey(
                coordinate.Realm,
                coordinate.OwnerKind,
                coordinate.ResourceOwnerId);
            sourceExports.Add(new ResourceMutationSourceExport(
                "action_cost",
                "derived_scale_action_" + suffix,
                FingerprintA,
                ResourceMutationSourceState.Active,
                SameTurn: false,
                owner));
            mutations.Add(new ResourceMutationIntent(
                EventRef: "turn_2:derived_scale_spend:" + suffix,
                Coordinate: coordinate,
                Amount: 2m,
                Source: new ResourceMutationSourceRequest(
                    "action_cost",
                    "derived_scale_action_" + suffix,
                    ResourceOperation.Spend),
                Dependencies: Array.Empty<ResourceOperationKey>(),
                EventRequirements: Array.Empty<ResourceMutationEventRequirement>(),
                ReceiptId: null));
            mutations.Add(new ResourceMutationIntent(
                EventRef: "turn_2:derived_scale_recovery:" + suffix,
                Coordinate: coordinate,
                Amount: 0m,
                Source: new ResourceMutationSourceRequest(
                    "registered_system_outcome",
                    "derived_scale_system",
                    ResourceOperation.Gain),
                Dependencies: Array.Empty<ResourceOperationKey>(),
                EventRequirements: Array.Empty<ResourceMutationEventRequirement>(),
                ReceiptId: null,
                DerivedAmount: new ResourceLossRecoveryPolicy(50)));
        }

        var history = ResourceHistoryState.CreateValidated(transitions, definitions);
        Assert.True(history.IsValid, Format(history.Issues));
        var state = new ResourceStateLedger(states);
        Assert.Empty(history.History!.ValidateStateAgreement(state));
        var sources = ResourceMutationSourceCatalog.Create(sourceExports);
        Assert.True(sources.IsValid, Format(sources.Issues));

        return AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 2,
                Definitions: definitions,
                State: state,
                History: history.History,
                Sources: sources.Catalog!,
                Mutations: mutations),
            IdentityFactory());
    }

    private static MutationBaseline CreateMutationBaseline(int count, string sourceKind)
    {
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var coordinate = new ResourceCoordinate(
            "mortal_world",
            ResourceOwnerKind.Item,
            "scale_item",
            "charges");
        var maximum = Math.Max(10m, count + 2m);
        var current = maximum;
        var binding = new ResourceCapacityBinding(
            ResourceCapacityKind.InstanceFixed,
            "scale_capacity",
            FingerprintA);
        var snapshot = new ResourceStateSnapshot(
            current,
            maximum,
            binding,
            ResourceLifecycleState.Active);
        var transition = new ResourceTransition(
            TransitionId: "scale_transition_initialize",
            OperationId: "scale_operation_initialize",
            EventRef: "turn_1:scale_initialize",
            OriginKind: "setting_materialization",
            OriginId: "scale_item",
            Phase: ResourceMutationPhase.RegisteredSystemOutcome,
            Priority: 50,
            ExecutionSequence: 0,
            Coordinate: coordinate,
            Operation: ResourceTransitionOperation.Initialize,
            RequestedAmount: 0m,
            AppliedAmount: 0m,
            Outcome: ResourceTransitionOutcome.Applied,
            CapacityDisposition: ResourceCapacityDisposition.InitializeFromDefinition,
            BeforeState: null,
            AfterState: snapshot,
            SourceEvidence: new ResourceSourceEvidence(
                "setting_materialization",
                "scale_item",
                FingerprintA),
            PolicyFingerprint: FingerprintA,
            ReceiptId: null,
            Turn: 1);
        var history = ResourceHistoryState.CreateValidated(
            new[] { transition },
            definitions);
        Assert.True(history.IsValid, Format(history.Issues));
        var state = new ResourceStateLedger(new[]
        {
            new ResourceStateEntry(
                coordinate,
                current,
                maximum,
                binding,
                ResourceLifecycleState.Active,
                new ResourceChronology(
                    CreatedAtTurn: 1,
                    CreatedEventRef: transition.EventRef,
                    LastTransitionId: transition.TransitionId,
                    LastEventRef: transition.EventRef,
                    LastTransitionTurn: 1))
        });
        Assert.Empty(history.History!.ValidateStateAgreement(state));

        var owner = new ResourceOwnerKey(
            coordinate.Realm,
            coordinate.OwnerKind,
            coordinate.ResourceOwnerId);
        var source = new ResourceMutationSourceExport(
            sourceKind,
            SourceId(sourceKind),
            FingerprintB,
            ResourceMutationSourceState.Active,
            SameTurn: false,
            BoundOwner: sourceKind == "effect_component" ? owner : null);
        var sources = ResourceMutationSourceCatalog.Create(new[] { source });
        Assert.True(sources.IsValid, Format(sources.Issues));
        return new MutationBaseline(
            definitions,
            state,
            history.History,
            sources.Catalog!,
            coordinate);
    }

    private static ResourceMutationSourceCatalog CreateSourceCatalog()
    {
        var result = ResourceMutationSourceCatalog.Create(new[]
        {
            new ResourceMutationSourceExport(
                "registered_system_outcome",
                "scale_system",
                FingerprintA,
                ResourceMutationSourceState.Active,
                SameTurn: false)
        });
        Assert.True(result.IsValid, Format(result.Issues));
        return result.Catalog!;
    }

    private static ResourcePendingResolutionCreationResult CreatePending(int count)
    {
        var drafts = Enumerable.Range(0, count)
            .Select(PendingDraft)
            .ToArray();
        var nextId = 0;
        return ResourcePendingResolutionState.CreatePending(
            canonicalJson: null,
            drafts,
            ResourceDefinitionCatalog.CreateBuiltIn(),
            () => "resource_resolution_scale_" +
                  (nextId++).ToString("D4", CultureInfo.InvariantCulture),
            new DateTimeOffset(2026, 8, 22, 12, 0, 0, TimeSpan.Zero));
    }

    private static ResourcePendingResolutionDraft PendingDraft(int index)
    {
        var suffix = index.ToString("D4", CultureInfo.InvariantCulture);
        var effectId = "effect_scale_" + suffix;
        var woundId = "wound_scale_" + suffix;
        return new ResourcePendingResolutionDraft(
            ResolutionMode: "bounded_receipt",
            SessionId: "session_scale",
            AcceptedRequestId: "request_turn_42",
            RequestTurn: 42,
            EventRef: "turn_42:effect:" + suffix,
            EffectId: effectId,
            EffectAuthority: new ResourcePendingAuthorityBinding("permanent", effectId),
            Source: new JsonObject
            {
                ["kind"] = "wound",
                ["sourceId"] = woundId,
                ["definitionKey"] = "bleeding_consequence"
            },
            SourceAuthority: new ResourcePendingAuthorityBinding("permanent", woundId),
            Target: new JsonObject
            {
                ["kind"] = "player",
                ["targetId"] = "player_current"
            },
            TargetAuthority: new ResourcePendingAuthorityBinding(
                "permanent",
                "player_current"),
            TriggerId: "trigger_scale_" + suffix,
            Coordinate: new ResourceCoordinate(
                "mortal_world",
                ResourceOwnerKind.Player,
                "player_current",
                "health"),
            ResourceAuthority: new ResourcePendingAuthorityBinding(
                "permanent",
                "player_current"),
            Operation: ResourceOperation.Damage,
            MinimumAmount: 0m,
            MaximumAmount: 5m,
            SourceAuthorityFingerprint: FingerprintA,
            PolicyFingerprint: FingerprintB,
            FullTurnFingerprint: FingerprintA,
            SemanticTurnFingerprint: FingerprintA,
            SafeSourceLabel: "Рана",
            SafeTargetLabel: "герой",
            SafeResourceLabel: "Здоровье",
            SafeOperationLabel: "урон",
            CausalAuthority: new ResourcePendingCausalAuthority(
                effectId,
                "trigger_scale_" + suffix,
                "turn_42:effect:" + suffix,
                "turn_42:effect:" + suffix,
                ResourceProducerOperationKey: null,
                Priority: index,
                ActivationOrdinal: index,
                ConsumesUse: false,
                UsesBefore: null,
                "component_scale_" + suffix,
                AfterComponentId: null,
                FingerprintA,
                FingerprintB,
                WaveOrdinal: 0));
    }

    private static AcceptedMechanicsIdentityFactory IdentityFactory()
    {
        var next = 1;
        return new AcceptedMechanicsIdentityFactory(() =>
            new Guid(next++, 0, 0, new byte[8]));
    }

    private static string SourceId(string sourceKind) =>
        sourceKind == "effect_component" ? "scale_effect" : "scale_system";

    private static string Format(IEnumerable<ValidationIssue> issues) =>
        string.Join(Environment.NewLine, issues.Select(static issue =>
            $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}"));

    private sealed record MutationBaseline(
        ResourceDefinitionCatalog Definitions,
        ResourceStateLedger State,
        ResourceHistoryState History,
        ResourceMutationSourceCatalog Sources,
        ResourceCoordinate Coordinate);

    private sealed record LiveEntryBaseline(
        ResourceDefinitionCatalog Definitions,
        ResourceDefinition Definition,
        ResourceStateLedger State,
        ResourceHistoryState History);
}
