using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AcceptedMechanicsPlannerTests
{
    private const string FingerprintA =
        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string FingerprintB =
        "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    public static TheoryData<string, string, string, int, bool>
        RegisteredRouteCases => new()
        {
            { "action_cost", "Spend", "DirectCost", 100, true },
            { "combat_outcome", "Damage", "DirectOutcome", 100, false },
            { "narrative_outcome", "Restore", "DirectOutcome", 110, false },
            { "local_item_cost", "Spend", "DirectCost", 120, true },
            { "local_item_outcome", "Gain", "DirectOutcome", 120, false },
            { "afterlife_cost", "Spend", "DirectCost", 130, true },
            { "afterlife_outcome", "Gain", "DirectOutcome", 130, false },
            { "registered_system_outcome", "Gain", "RegisteredSystemOutcome", 100, false },
            { "effect_component", "Damage", "EffectTrigger", 200, false },
            { "bounded_receipt", "Restore", "EffectTrigger", 210, false }
        };

    [Theory]
    [MemberData(nameof(RegisteredRouteCases))]
    public void SourceCatalog_BindsClosedRoutePhasePriorityPolicyAndEvidence(
        string sourceKind,
        string operationToken,
        string expectedPhaseToken,
        int expectedPriority,
        bool rejectsAtMinimum)
    {
        var operation = Enum.Parse<ResourceOperation>(operationToken);
        var expectedPhase = Enum.Parse<ResourceMutationPhase>(expectedPhaseToken);
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var resourceKey = operation is ResourceOperation.Spend or ResourceOperation.Gain
            ? "charges"
            : "health";
        Assert.True(definitions.TryResolveExact(resourceKey, out var definition));
        var catalog = CreateCatalog(new ResourceMutationSourceExport(
            sourceKind,
            "source_alpha",
            FingerprintA,
            ResourceMutationSourceState.Active,
            SameTurn: true));

        var result = catalog.Resolve(
            new ResourceMutationSourceRequest(sourceKind, "source_alpha", operation),
            definition!);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        Assert.Equal(expectedPhase, result.Route!.Phase);
        Assert.Equal(expectedPriority, result.Route.Priority);
        Assert.Equal(
            rejectsAtMinimum
                ? ResourceBoundPolicy.RejectBelowMinimum
                : definition!.FloorPolicy,
            result.Route.PolicyBinding.FloorPolicy);
        Assert.Equal(
            rejectsAtMinimum
                ? ResourceBoundPolicy.RejectAboveMaximum
                : definition!.CapPolicy,
            result.Route.PolicyBinding.CapPolicy);
        Assert.Equal(
            new ResourceSourceEvidence(sourceKind, "source_alpha", FingerprintA),
            result.Route.SourceEvidence);
        Assert.True(result.Route.SameTurn);
        Assert.True(ResourceMaterializationContract.IsAuthorityFingerprint(
            result.Route.PolicyBinding.AuthorityFingerprint));
    }

    [Theory]
    [InlineData("Inactive")]
    [InlineData("Historical")]
    public void SourceCatalog_RejectsInactiveOrHistoricalAuthority(
        string stateToken)
    {
        var state = Enum.Parse<ResourceMutationSourceState>(stateToken);
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        Assert.True(definitions.TryResolveExact("health", out var definition));
        var catalog = CreateCatalog(new ResourceMutationSourceExport(
            "combat_outcome",
            "source_alpha",
            FingerprintA,
            state,
            SameTurn: false));

        var result = catalog.Resolve(
            new ResourceMutationSourceRequest(
                "combat_outcome",
                "source_alpha",
                ResourceOperation.Damage),
            definition!);

        Assert.Null(result.Route);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_source_inactive");
    }

    [Fact]
    public void SourceCatalog_AcceptsExactSameTurnAuthorityAndRejectsCaseVariant()
    {
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        Assert.True(definitions.TryResolveExact("health", out var definition));
        var catalog = CreateCatalog(new ResourceMutationSourceExport(
            "combat_outcome",
            "exchange_alpha",
            FingerprintA,
            ResourceMutationSourceState.Active,
            SameTurn: true));

        var exact = catalog.Resolve(
            new ResourceMutationSourceRequest(
                "combat_outcome",
                "exchange_alpha",
                ResourceOperation.Damage),
            definition!);
        var wrongCase = catalog.Resolve(
            new ResourceMutationSourceRequest(
                "combat_outcome",
                "Exchange_alpha",
                ResourceOperation.Damage),
            definition!);

        Assert.True(exact.IsValid);
        Assert.True(exact.Route!.SameTurn);
        Assert.Contains(wrongCase.Issues, issue =>
            issue.Code == "resource_source_unknown");
    }

    [Fact]
    public void SourceCatalog_RejectsUnsupportedRouteOperationAndConfusableAuthority()
    {
        var invalidCatalog = ResourceMutationSourceCatalog.Create(new[]
        {
            new ResourceMutationSourceExport(
                "unknown_route",
                "source_alpha",
                FingerprintA,
                ResourceMutationSourceState.Active,
                SameTurn: false),
            new ResourceMutationSourceExport(
                "combat_outcome",
                "source_beta",
                FingerprintA,
                ResourceMutationSourceState.Active,
                SameTurn: false),
            new ResourceMutationSourceExport(
                "combat_outcome",
                "SOURCE_BETA",
                FingerprintB,
                ResourceMutationSourceState.Active,
                SameTurn: false)
        });
        var validCatalog = CreateCatalog(new ResourceMutationSourceExport(
            "action_cost",
            "source_alpha",
            FingerprintA,
            ResourceMutationSourceState.Active,
            SameTurn: false));
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        Assert.True(definitions.TryResolveExact("health", out var definition));

        var unsupported = validCatalog.Resolve(
            new ResourceMutationSourceRequest(
                "action_cost",
                "source_alpha",
                ResourceOperation.Damage),
            definition!);

        Assert.Null(invalidCatalog.Catalog);
        Assert.Contains(invalidCatalog.Issues, issue =>
            issue.Code == "resource_source_route_unknown");
        Assert.Contains(invalidCatalog.Issues, issue =>
            issue.Code == "resource_source_duplicate_confusable");
        Assert.Contains(unsupported.Issues, issue =>
            issue.Code == "resource_source_operation_forbidden");
    }

    [Fact]
    public void SourceRequest_HasNoGmPhasePriorityPolicyOrFingerprintOverrides()
    {
        var propertyNames = typeof(ResourceMutationSourceRequest)
            .GetProperties()
            .Select(static property => property.Name)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "Operation", "SourceId", "SourceKind" },
            propertyNames);
    }

    [Fact]
    public void SourceCatalog_PolicyFingerprintBindsSourceAndDefinitionAuthority()
    {
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        Assert.True(definitions.TryResolveExact("health", out var definition));
        var first = CreateCatalog(new ResourceMutationSourceExport(
            "combat_outcome",
            "source_alpha",
            FingerprintA,
            ResourceMutationSourceState.Active,
            SameTurn: false)).Resolve(
                new ResourceMutationSourceRequest(
                    "combat_outcome",
                    "source_alpha",
                    ResourceOperation.Damage),
                definition!);
        var changed = CreateCatalog(new ResourceMutationSourceExport(
            "combat_outcome",
            "source_alpha",
            FingerprintB,
            ResourceMutationSourceState.Active,
            SameTurn: false)).Resolve(
                new ResourceMutationSourceRequest(
                    "combat_outcome",
                    "source_alpha",
                    ResourceOperation.Damage),
                definition!);

        Assert.NotEqual(
            first.Route!.PolicyBinding.AuthorityFingerprint,
            changed.Route!.PolicyBinding.AuthorityFingerprint);
    }

    [Fact]
    public void Planner_ExecutesFourPhasesAndAssignsOneCanonicalSequence()
    {
        var baseline = BaselineCharges();
        var intents = new[]
        {
            Intent("turn_2:resource:4", "effect_component", "effect_alpha", ResourceOperation.Gain, 3m),
            Intent("turn_2:resource:2", "narrative_outcome", "story_alpha", ResourceOperation.Gain, 7m),
            Intent("turn_2:resource:3", "registered_system_outcome", "system_alpha", ResourceOperation.Spend, 5m),
            Intent("turn_2:resource:1", "action_cost", "action_alpha", ResourceOperation.Spend, 2m)
        };
        var result = AcceptedMechanicsPlanner.BuildResources(
            Input(baseline, intents),
            IdentityFactory());

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        Assert.Equal(8m, result.StateAfterImage!.Entries.Single().Current);
        Assert.Equal(
            new[]
            {
                ResourceMutationPhase.DirectCost,
                ResourceMutationPhase.DirectOutcome,
                ResourceMutationPhase.RegisteredSystemOutcome,
                ResourceMutationPhase.EffectTrigger
            },
            result.AppliedTransitions.Select(static transition => transition.Phase));
        Assert.Equal(
            new[] { 0, 1, 2, 3 },
            result.AppliedTransitions.Select(static transition => transition.ExecutionSequence));
        Assert.Equal(
            new[] { "action_alpha", "story_alpha", "system_alpha", "effect_alpha" },
            result.AppliedTransitions.Select(static transition => transition.OriginId));
        Assert.Equal(1, result.Statistics.HistorySeedCount);
        Assert.Equal(1, result.Statistics.HistoryFreezeCount);
        Assert.Equal(4, result.Statistics.HistoryAppendCount);
        Assert.Empty(result.HistoryAfterImage!.ValidateStateAgreement(result.StateAfterImage));
    }

    [Fact]
    public void Planner_ExactReplayDoesNotAppendOrEmitEventsAgain()
    {
        var baseline = BaselineCharges();
        var intents = new[]
        {
            Intent("turn_2:resource:1", "action_cost", "action_alpha", ResourceOperation.Spend, 2m),
            Intent("turn_2:resource:2", "narrative_outcome", "story_alpha", ResourceOperation.Gain, 1m)
        };
        var first = AcceptedMechanicsPlanner.BuildResources(
            Input(baseline, intents),
            IdentityFactory(seed: 10));
        Assert.True(first.IsValid, string.Join(Environment.NewLine, first.Issues));

        var replayInput = Input(
            new Baseline(
                baseline.Definitions,
                first.StateAfterImage!,
                first.HistoryAfterImage!,
                baseline.Sources),
            intents);
        var replay = AcceptedMechanicsPlanner.BuildResources(
            replayInput,
            IdentityFactory(seed: 100));

        Assert.True(replay.IsValid, string.Join(Environment.NewLine, replay.Issues));
        Assert.Empty(replay.AppliedTransitions);
        Assert.Equal(2, replay.ReplayTransitions.Count);
        Assert.Empty(replay.Events);
        Assert.Equal(first.StateAfterImage!.Fingerprint, replay.StateAfterImage!.Fingerprint);
        Assert.Equal(first.HistoryAfterImage!.Fingerprint, replay.HistoryAfterImage!.Fingerprint);
        Assert.Equal(0, replay.Statistics.HistoryAppendCount);
        Assert.Equal(1, replay.Statistics.HistoryFreezeCount);
    }

    [Fact]
    public void Planner_InvalidSiblingDiscardsEveryAfterImageAndEvent()
    {
        var baseline = BaselineCharges(
            new ResourceMutationSourceExport(
                "action_cost",
                "action_alpha",
                FingerprintA,
                ResourceMutationSourceState.Active,
                SameTurn: false),
            new ResourceMutationSourceExport(
                "action_cost",
                "action_omega",
                FingerprintB,
                ResourceMutationSourceState.Active,
                SameTurn: false));
        var result = AcceptedMechanicsPlanner.BuildResources(
            Input(
                baseline,
                new[]
                {
                    Intent("turn_2:resource:1", "action_cost", "action_alpha", ResourceOperation.Spend, 1m),
                    Intent("turn_2:resource:2", "action_cost", "action_omega", ResourceOperation.Spend, 20m)
                }),
            IdentityFactory());

        Assert.False(result.IsValid);
        Assert.Null(result.StateAfterImage);
        Assert.Null(result.HistoryAfterImage);
        Assert.Empty(result.Events);
        Assert.Empty(result.AppliedTransitions);
        Assert.Empty(result.ReplayTransitions);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_mutation_below_minimum");
        Assert.Equal(10m, baseline.State.Entries.Single().Current);
        Assert.Single(baseline.History.Transitions);
    }

    [Fact]
    public void Planner_ActivatesNestedNodesOnlyFromExactProducedEvents()
    {
        var baseline = BaselineCharges(
            new ResourceMutationSourceExport(
                "action_cost",
                "action_alpha",
                FingerprintA,
                ResourceMutationSourceState.Active,
                SameTurn: false),
            new ResourceMutationSourceExport(
                "registered_system_outcome",
                "system_alpha",
                FingerprintA,
                ResourceMutationSourceState.Active,
                SameTurn: false),
            new ResourceMutationSourceExport(
                "effect_component",
                "effect_alpha",
                FingerprintA,
                ResourceMutationSourceState.Active,
                SameTurn: false),
            new ResourceMutationSourceExport(
                "effect_component",
                "effect_unmet",
                FingerprintB,
                ResourceMutationSourceState.Active,
                SameTurn: false));
        var root = Intent(
            "turn_2:resource:1",
            "action_cost",
            "action_alpha",
            ResourceOperation.Spend,
            10m);
        var filled = Intent(
            "turn_2:resource:2",
            "registered_system_outcome",
            "system_alpha",
            ResourceOperation.Gain,
            10m,
            eventRequirements: new[]
            {
                new ResourceMutationEventRequirement(root.Key, "resource_depleted")
            });
        var nested = Intent(
            "turn_2:resource:3",
            "effect_component",
            "effect_alpha",
            ResourceOperation.Spend,
            1m,
            eventRequirements: new[]
            {
                new ResourceMutationEventRequirement(filled.Key, "resource_filled")
            });
        var unmet = Intent(
            "turn_2:resource:4",
            "effect_component",
            "effect_unmet",
            ResourceOperation.Spend,
            9m,
            eventRequirements: new[]
            {
                new ResourceMutationEventRequirement(root.Key, "resource_filled")
            });

        var result = AcceptedMechanicsPlanner.BuildResources(
            Input(baseline, new[] { unmet, nested, filled, root }),
            IdentityFactory());

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        Assert.Equal(9m, result.StateAfterImage!.Entries.Single().Current);
        Assert.Equal(
            new[] { "action_alpha", "system_alpha", "effect_alpha" },
            result.AppliedTransitions.Select(static transition => transition.OriginId));
        Assert.Equal(
            new[]
            {
                "resource_spent",
                "resource_depleted",
                "resource_gained",
                "resource_filled",
                "resource_spent"
            },
            result.Events.Select(static value => value.EventKind));
        Assert.DoesNotContain(result.AppliedTransitions, transition =>
            transition.OriginId == "effect_unmet");
    }

    [Fact]
    public void Planner_AppliesCapacityLifecycleBeforeEveryMutationPhase()
    {
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        Assert.True(definitions.TryResolveExact("charges", out var definition));
        var capacity = ResolvedResourceCapacity.Resolve(
            definition!,
            ChargesCoordinate,
            new InstanceFixedCapacityInput(
                new ResourceFormulaOwner(
                    ChargesCoordinate.Realm,
                    ChargesCoordinate.OwnerKind,
                    ChargesCoordinate.ResourceOwnerId),
                10m,
                FingerprintA),
            instanceAuthorityKey: "capacity_item_alpha_charges",
            includeInitialization: true);
        Assert.True(capacity.IsValid, string.Join(Environment.NewLine, capacity.Issues));
        var history = ResourceHistoryState.ParseCanonical(
            "{\"schemaVersion\":1,\"entries\":[]}",
            definitions,
            allowMissingPristine: false).History!;
        var sources = CreateCatalog(new ResourceMutationSourceExport(
            "action_cost",
            "action_alpha",
            FingerprintA,
            ResourceMutationSourceState.Active,
            SameTurn: false));
        var input = new AcceptedMechanicsResourceInput(
            Turn: 2,
            Definitions: definitions,
            State: new ResourceStateLedger(Array.Empty<ResourceStateEntry>()),
            History: history,
            Sources: sources,
            Mutations: new[]
            {
                Intent("turn_2:resource:2", "action_cost", "action_alpha", ResourceOperation.Spend, 2m)
            },
            CapacityTransitions: new[]
            {
                new ResourceCapacityIntent(
                    EventRef: "turn_2:resource:1",
                    OriginKind: "setting_materialization",
                    OriginId: "item_alpha",
                    Coordinate: ChargesCoordinate,
                    Operation: ResourceCapacityOperation.Initialize,
                    ResolvedCapacity: capacity.Capacity,
                    CurrentDisposition: ResourceCurrentDisposition.InitializeFromDefinition,
                    Phase: ResourceMutationPhase.RegisteredSystemOutcome,
                    Priority: 50,
                    SourceEvidence: new ResourceSourceEvidence(
                        "setting_materialization",
                        "item_alpha",
                        FingerprintA),
                    PolicyFingerprint: capacity.Capacity!.Initialization!.AuthorityFingerprint,
                    ReceiptId: null)
            });

        var result = AcceptedMechanicsPlanner.BuildResources(input, IdentityFactory());

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        Assert.Equal(8m, result.StateAfterImage!.Entries.Single().Current);
        Assert.Equal(
            new[]
            {
                ResourceTransitionOperation.Initialize,
                ResourceTransitionOperation.Spend
            },
            result.AppliedTransitions.Select(static transition => transition.Operation));
        Assert.Equal(
            new[] { 0, 1 },
            result.AppliedTransitions.Select(static transition => transition.ExecutionSequence));
    }

    private static AcceptedMechanicsResourceInput Input(
        Baseline baseline,
        IReadOnlyList<ResourceMutationIntent> intents) =>
        new(
            Turn: 2,
            Definitions: baseline.Definitions,
            State: baseline.State,
            History: baseline.History,
            Sources: baseline.Sources,
            Mutations: intents);

    private static ResourceMutationIntent Intent(
        string eventRef,
        string sourceKind,
        string sourceId,
        ResourceOperation operation,
        decimal amount,
        IReadOnlyList<ResourceOperationKey>? dependencies = null,
        IReadOnlyList<ResourceMutationEventRequirement>? eventRequirements = null) =>
        new(
            eventRef,
            ChargesCoordinate,
            amount,
            new ResourceMutationSourceRequest(sourceKind, sourceId, operation),
            dependencies ?? Array.Empty<ResourceOperationKey>(),
            eventRequirements ?? Array.Empty<ResourceMutationEventRequirement>(),
            ReceiptId: null);

    private static Baseline BaselineCharges(
        params ResourceMutationSourceExport[] sources)
    {
        if (sources.Length == 0)
        {
            sources =
            [
                new("action_cost", "action_alpha", FingerprintA, ResourceMutationSourceState.Active, false),
                new("narrative_outcome", "story_alpha", FingerprintA, ResourceMutationSourceState.Active, false),
                new("registered_system_outcome", "system_alpha", FingerprintA, ResourceMutationSourceState.Active, false),
                new("effect_component", "effect_alpha", FingerprintA, ResourceMutationSourceState.Active, false)
            ];
        }

        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var binding = new ResourceCapacityBinding(
            ResourceCapacityKind.InstanceFixed,
            "capacity_item_alpha_charges",
            FingerprintA);
        var snapshot = new ResourceStateSnapshot(
            Current: 10m,
            Maximum: 10m,
            binding,
            ResourceLifecycleState.Active);
        var transition = new ResourceTransition(
            TransitionId: "transition_baseline_initialize",
            OperationId: "operation_baseline_initialize",
            EventRef: "turn_1:resource:1",
            OriginKind: "setting_materialization",
            OriginId: "item_alpha",
            Phase: ResourceMutationPhase.RegisteredSystemOutcome,
            Priority: 50,
            ExecutionSequence: 0,
            Coordinate: ChargesCoordinate,
            Operation: ResourceTransitionOperation.Initialize,
            RequestedAmount: 0m,
            AppliedAmount: 0m,
            Outcome: ResourceTransitionOutcome.Applied,
            CapacityDisposition: ResourceCapacityDisposition.InitializeFromDefinition,
            BeforeState: null,
            AfterState: snapshot,
            SourceEvidence: new ResourceSourceEvidence(
                "setting_materialization",
                "item_alpha",
                FingerprintA),
            PolicyFingerprint: FingerprintA,
            ReceiptId: null,
            Turn: 1);
        var history = ResourceHistoryState.CreateValidated(
            new[] { transition },
            definitions);
        Assert.True(history.IsValid, string.Join(Environment.NewLine, history.Issues));
        var state = new ResourceStateLedger(new[]
        {
            new ResourceStateEntry(
                ChargesCoordinate,
                10m,
                10m,
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
        return new Baseline(
            definitions,
            state,
            history.History,
            CreateCatalog(sources));
    }

    private static AcceptedMechanicsIdentityFactory IdentityFactory(int seed = 1)
    {
        var next = seed;
        return new AcceptedMechanicsIdentityFactory(() =>
            new Guid(next++, 0, 0, new byte[8]));
    }

    private static ResourceCoordinate ChargesCoordinate { get; } = new(
        "mortal_world",
        ResourceOwnerKind.Item,
        "item_alpha",
        "charges");

    private sealed record Baseline(
        ResourceDefinitionCatalog Definitions,
        ResourceStateLedger State,
        ResourceHistoryState History,
        ResourceMutationSourceCatalog Sources);

    private static ResourceMutationSourceCatalog CreateCatalog(
        params ResourceMutationSourceExport[] exports)
    {
        var result = ResourceMutationSourceCatalog.Create(exports);
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        return result.Catalog!;
    }
}
