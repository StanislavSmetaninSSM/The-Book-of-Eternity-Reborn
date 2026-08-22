using System.Text.Json;
using System.Text.Json.Nodes;
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
        var boundOwner = sourceKind.StartsWith("local_item_", StringComparison.Ordinal)
            ? new ResourceOwnerKey(
                "mortal_world",
                ResourceOwnerKind.Item,
                "source_alpha")
            : RequiresPlayerTargetBinding(sourceKind)
                ? new ResourceOwnerKey(
                    "mortal_world",
                    ResourceOwnerKind.Player,
                    "player_current")
                : null;
        var catalog = CreateCatalog(new ResourceMutationSourceExport(
            sourceKind,
            "source_alpha",
            FingerprintA,
            ResourceMutationSourceState.Active,
            SameTurn: true,
            boundOwner));

        var result = catalog.Resolve(
            new ResourceMutationSourceRequest(sourceKind, "source_alpha", operation),
            definition!,
            boundOwner == null
                ? null
                : new ResourceCoordinate(
                    boundOwner.Realm,
                    boundOwner.OwnerKind,
                    boundOwner.ResourceOwnerId,
                    resourceKey));

        Assert.True(
            result.IsValid,
            string.Join(
                Environment.NewLine,
                result.Issues.Select(issue =>
                    $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
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
            SameTurn: false,
            PlayerOwner));

        var result = catalog.Resolve(
            new ResourceMutationSourceRequest(
                "combat_outcome",
                "source_alpha",
                ResourceOperation.Damage),
            definition!,
            HealthCoordinate);

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
            SameTurn: true,
            PlayerOwner));

        var exact = catalog.Resolve(
            new ResourceMutationSourceRequest(
                "combat_outcome",
                "exchange_alpha",
                ResourceOperation.Damage),
            definition!,
            HealthCoordinate);
        var wrongCase = catalog.Resolve(
            new ResourceMutationSourceRequest(
                "combat_outcome",
                "Exchange_alpha",
                ResourceOperation.Damage),
            definition!,
            HealthCoordinate);

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
                SameTurn: false,
                PlayerOwner),
            new ResourceMutationSourceExport(
                "combat_outcome",
                "SOURCE_BETA",
                FingerprintB,
                ResourceMutationSourceState.Active,
                SameTurn: false,
                PlayerOwner)
        });
        var validCatalog = CreateCatalog(new ResourceMutationSourceExport(
            "action_cost",
            "source_alpha",
            FingerprintA,
            ResourceMutationSourceState.Active,
            SameTurn: false,
            PlayerOwner));
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        Assert.True(definitions.TryResolveExact("health", out var definition));

        var unsupported = validCatalog.Resolve(
            new ResourceMutationSourceRequest(
                "action_cost",
                "source_alpha",
                ResourceOperation.Damage),
            definition!,
            HealthCoordinate);

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
            SameTurn: false,
            PlayerOwner)).Resolve(
                new ResourceMutationSourceRequest(
                    "combat_outcome",
                    "source_alpha",
                    ResourceOperation.Damage),
                definition!,
                HealthCoordinate);
        var changed = CreateCatalog(new ResourceMutationSourceExport(
            "combat_outcome",
            "source_alpha",
            FingerprintB,
            ResourceMutationSourceState.Active,
            SameTurn: false,
            PlayerOwner)).Resolve(
                new ResourceMutationSourceRequest(
                    "combat_outcome",
                    "source_alpha",
                    ResourceOperation.Damage),
                definition!,
                HealthCoordinate);

        Assert.NotEqual(
            first.Route!.PolicyBinding.AuthorityFingerprint,
            changed.Route!.PolicyBinding.AuthorityFingerprint);
    }

    [Fact]
    public void SourceCatalog_EffectComponentRequiresExactTargetOwnerBinding()
    {
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        Assert.True(definitions.TryResolveExact("health", out var definition));
        var owner = new ResourceOwnerKey(
            "mortal_world",
            ResourceOwnerKind.Player,
            "player_current");
        var bound = ResourceMutationSourceCatalog.Create(new[]
        {
            new ResourceMutationSourceExport(
                "effect_component",
                "effect_component_alpha",
                FingerprintA,
                ResourceMutationSourceState.Active,
                SameTurn: false,
                owner)
        });
        var unbound = ResourceMutationSourceCatalog.Create(new[]
        {
            new ResourceMutationSourceExport(
                "effect_component",
                "effect_component_beta",
                FingerprintB,
                ResourceMutationSourceState.Active,
                SameTurn: false)
        });

        Assert.True(bound.IsValid, string.Join(Environment.NewLine, bound.Issues));
        var exact = bound.Catalog!.Resolve(
            new ResourceMutationSourceRequest(
                "effect_component",
                "effect_component_alpha",
                ResourceOperation.Damage),
            definition!,
            HealthCoordinate);
        var wrongTarget = bound.Catalog.Resolve(
            new ResourceMutationSourceRequest(
                "effect_component",
                "effect_component_alpha",
                ResourceOperation.Damage),
            definition!,
            HealthCoordinate with { ResourceOwnerId = "player_other" });

        Assert.True(exact.IsValid, string.Join(Environment.NewLine, exact.Issues));
        Assert.Contains(wrongTarget.Issues, issue =>
            issue.Code == "resource_source_target_mismatch");
        Assert.Null(unbound.Catalog);
        Assert.Contains(unbound.Issues, issue =>
            issue.Code == "resource_source_owner_binding_required");
    }

    [Theory]
    [InlineData("action_cost", "Spend", "charges")]
    [InlineData("combat_outcome", "Damage", "health")]
    [InlineData("narrative_outcome", "Restore", "health")]
    public void SourceCatalog_OrdinarySourceRequiresExactTargetOwnerBinding(
        string sourceKind,
        string operationToken,
        string resourceKey)
    {
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        Assert.True(definitions.TryResolveExact(resourceKey, out var definition));
        var operation = Enum.Parse<ResourceOperation>(operationToken);
        var coordinate = new ResourceCoordinate(
            PlayerOwner.Realm,
            PlayerOwner.OwnerKind,
            PlayerOwner.ResourceOwnerId,
            resourceKey);
        var bound = ResourceMutationSourceCatalog.Create(new[]
        {
            new ResourceMutationSourceExport(
                sourceKind,
                "turn_42:resource:1",
                FingerprintA,
                ResourceMutationSourceState.Active,
                SameTurn: true,
                PlayerOwner)
        });
        var unbound = ResourceMutationSourceCatalog.Create(new[]
        {
            new ResourceMutationSourceExport(
                sourceKind,
                "turn_42:resource:2",
                FingerprintB,
                ResourceMutationSourceState.Active,
                SameTurn: true)
        });

        Assert.True(bound.IsValid, string.Join(Environment.NewLine, bound.Issues));
        var exact = bound.Catalog!.Resolve(
            new ResourceMutationSourceRequest(
                sourceKind,
                "turn_42:resource:1",
                operation),
            definition!,
            coordinate);
        var wrongTarget = bound.Catalog.Resolve(
            new ResourceMutationSourceRequest(
                sourceKind,
                "turn_42:resource:1",
                operation),
            definition!,
            coordinate with { ResourceOwnerId = "player_other" });

        Assert.True(exact.IsValid, string.Join(Environment.NewLine, exact.Issues));
        Assert.Contains(wrongTarget.Issues, issue =>
            issue.Code == "resource_source_target_mismatch");
        Assert.Null(unbound.Catalog);
        Assert.Contains(unbound.Issues, issue =>
            issue.Code == "resource_source_owner_binding_required");
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
    public void Planner_ResolvesRegisteredLossRecoveryInsideTheSingleFourPhasePlan()
    {
        var baseline = BaselineCharges();
        var intents = new[]
        {
            Intent(
                "turn_2:resource:1",
                "action_cost",
                "action_alpha",
                ResourceOperation.Spend,
                4m),
            new ResourceMutationIntent(
                "turn_2:registered_recovery:1",
                ChargesCoordinate,
                Amount: 0m,
                new ResourceMutationSourceRequest(
                    "registered_system_outcome",
                    "system_alpha",
                    ResourceOperation.Gain),
                Array.Empty<ResourceOperationKey>(),
                Array.Empty<ResourceMutationEventRequirement>(),
                ReceiptId: null,
                DerivedAmount: new ResourceLossRecoveryPolicy(50))
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
                ResourceMutationPhase.RegisteredSystemOutcome
            },
            result.AppliedTransitions.Select(static transition => transition.Phase));
        Assert.Equal(
            new[] { 4m, 2m },
            result.AppliedTransitions.Select(static transition => transition.AppliedAmount));
        Assert.Equal(
            new[] { 0, 1 },
            result.AppliedTransitions.Select(static transition => transition.ExecutionSequence));
        Assert.Empty(result.HistoryAfterImage!.ValidateStateAgreement(result.StateAfterImage));
    }

    [Fact]
    public void Planner_ReplaysDerivedLossRecoveryFromImmutableRequestedAmount()
    {
        var baseline = BaselineCharges();
        var intents = new[]
        {
            Intent(
                "turn_2:resource:1",
                "action_cost",
                "action_alpha",
                ResourceOperation.Spend,
                4m),
            new ResourceMutationIntent(
                "turn_2:registered_recovery:1",
                ChargesCoordinate,
                Amount: 0m,
                new ResourceMutationSourceRequest(
                    "registered_system_outcome",
                    "system_alpha",
                    ResourceOperation.Gain),
                Array.Empty<ResourceOperationKey>(),
                Array.Empty<ResourceMutationEventRequirement>(),
                ReceiptId: null,
                DerivedAmount: new ResourceLossRecoveryPolicy(50))
        };
        var first = AcceptedMechanicsPlanner.BuildResources(
            Input(baseline, intents),
            IdentityFactory(seed: 10));
        Assert.True(first.IsValid, string.Join(Environment.NewLine, first.Issues));

        var replay = AcceptedMechanicsPlanner.BuildResources(
            Input(
                new Baseline(
                    baseline.Definitions,
                    first.StateAfterImage!,
                    first.HistoryAfterImage!,
                    baseline.Sources),
                intents),
            IdentityFactory(seed: 100));

        Assert.True(replay.IsValid, string.Join(Environment.NewLine, replay.Issues));
        Assert.Empty(replay.AppliedTransitions);
        Assert.Equal(2, replay.ReplayTransitions.Count);
        Assert.Equal(first.StateAfterImage!.Fingerprint, replay.StateAfterImage!.Fingerprint);
        Assert.Equal(first.HistoryAfterImage!.Fingerprint, replay.HistoryAfterImage!.Fingerprint);
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
            SameTurn: false,
            ChargesOwner));
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

    [Theory]
    [InlineData("Active", "Suspended", "Suspend")]
    [InlineData("Suspended", "Active", "Resume")]
    public void OwnerCapacityPlanner_SynchronizesLiveOwnerLifecycle(
        string currentStateToken,
        string ownerLifecycleToken,
        string expectedOperationToken)
    {
        var currentState = Enum.Parse<ResourceLifecycleState>(currentStateToken);
        var ownerLifecycle = Enum.Parse<ResourceOwnerLifecycle>(ownerLifecycleToken);
        var expectedOperation = Enum.Parse<ResourceCapacityOperation>(expectedOperationToken);
        var ownerKey = new ResourceOwnerKey(
            ChargesCoordinate.Realm,
            ChargesCoordinate.OwnerKind,
            ChargesCoordinate.ResourceOwnerId);
        var authority = ResourceOwnerAuthority.Build(new ResourceOwnerAuthorityInput(
            new[]
            {
                new ResourceOwnerExport(
                    ownerKey,
                    ownerLifecycle,
                    SameTurn: false,
                    OwnerRef: null,
                    BoundNpcId: null,
                    new HashSet<string>(StringComparer.Ordinal) { ChargesCoordinate.ResourceKey },
                    FingerprintA)
            },
            Array.Empty<ResourceOwnerExport>(),
            Array.Empty<ResourceOwnerKey>()));
        Assert.Empty(authority.Issues);
        var state = new ResourceStateLedger(new[]
        {
            new ResourceStateEntry(
                ChargesCoordinate,
                Current: 5m,
                Maximum: 10m,
                new ResourceCapacityBinding(
                    ResourceCapacityKind.InstanceFixed,
                    "capacity_item_alpha_charges",
                    FingerprintA),
                currentState,
                new ResourceChronology(
                    CreatedAtTurn: 1,
                    CreatedEventRef: "turn_1:resource:1",
                    LastTransitionId: "transition_baseline_initialize",
                    LastEventRef: "turn_1:resource:1",
                    LastTransitionTurn: 1))
        });
        var issues = new List<ValidationIssue>();

        var transitions = AcceptedMechanicsPlanner.ComposeOwnerCapacityTransitions(
            turn: 2,
            authority,
            state,
            Array.Empty<ResourceOwnerCapacityDraft>(),
            Array.Empty<ResourceOwnerKey>(),
            issues);

        Assert.Empty(issues);
        var transition = Assert.Single(transitions);
        Assert.Equal(expectedOperation, transition.Operation);
        Assert.Equal(ChargesCoordinate, transition.Coordinate);
        Assert.Null(transition.ResolvedCapacity);
        Assert.Null(transition.CurrentDisposition);
        Assert.Equal("owner_lifecycle", transition.SourceEvidence.SourceKind);
    }

    [Theory]
    [InlineData("periodic_damage", "Damage", 10, 7)]
    [InlineData("periodic_restore", "Restore", 5, 8)]
    public void SelectedPeriodicComponent_ResolvesExactCoordinateAndUsesCommonReducer(
        string profile,
        string operationToken,
        int current,
        int expectedCurrent)
    {
        var baseline = BaselineHealth(current);
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            ownerKind: "player",
            profile);
        var acceptedEvent = new EffectLifecycleEvent(
            EventRef: "turn_43:effect:on_owner_turn_end",
            Turn: 43,
            Phase: "owner_turn_end",
            TriggerId: "on_owner_turn_end");

        var resolution = InvokePeriodicResourceResolution(
            effect,
            "on_owner_turn_end",
            acceptedEvent,
            baseline.Targets,
            baseline.Owners,
            baseline.Definitions);

        Assert.Empty(resolution.Issues);
        var source = Assert.Single(resolution.SourceExports);
        var mutation = Assert.Single(resolution.Mutations);
        var operation = Enum.Parse<ResourceOperation>(operationToken);
        Assert.Equal(HealthCoordinate, mutation.Coordinate);
        Assert.Equal(operation, mutation.Source.Operation);
        Assert.Equal(3m, mutation.Amount);
        Assert.Equal(acceptedEvent.EventRef, mutation.EventRef);
        Assert.Equal("effect_component", mutation.Source.SourceKind);
        Assert.Equal(mutation.Source.SourceId, source.SourceId);
        Assert.NotEqual("component_001", source.SourceId);

        var result = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: acceptedEvent.Turn,
                Definitions: baseline.Definitions,
                State: baseline.State,
                History: baseline.History,
                Sources: CreateCatalog(source),
                Mutations: new[] { mutation }),
            IdentityFactory());

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        Assert.Equal(expectedCurrent, Assert.Single(result.StateAfterImage!.Entries).Current);
        var transition = Assert.Single(result.AppliedTransitions);
        Assert.Equal(ResourceMutationPhase.EffectTrigger, transition.Phase);
        Assert.Equal(source.SourceId, transition.OriginId);
        Assert.Equal(source.AuthorityFingerprint, transition.SourceEvidence.AuthorityFingerprint);
    }

    [Fact]
    public void SelectedPeriodicComponent_RejectsUnknownResource()
    {
        var baseline = BaselineHealth(current: 10m);
        var acceptedEvent = PeriodicEvent();
        var unknownResource = EffectMaterializationTestFixture.CreateCanonicalEffect();
        unknownResource["components"]![0]!["payload"]!["resource"] = "unknown_vitality";

        var unknownResolution = InvokePeriodicResourceResolution(
            unknownResource,
            "on_owner_turn_end",
            acceptedEvent,
            baseline.Targets,
            baseline.Owners,
            baseline.Definitions);

        Assert.Contains(unknownResolution.Issues, issue =>
            issue.Code == "effect_resource_definition_unknown");
        Assert.Empty(unknownResolution.Mutations);
    }

    [Theory]
    [InlineData("trigger_component", "deterministic", 1, 0)]
    [InlineData("bounded_receipt", "bounded_receipt", 0, 1)]
    public void EventReaction_DispatchesReferencedPeriodicComponentThroughCommonPlanner(
        string resultKind,
        string resolutionMode,
        int expectedMutations,
        int expectedPending)
    {
        var baseline = BaselineHealth(current: 10m);
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        var periodic = effect["components"]![0]!.AsObject();
        periodic["componentId"] = "reaction_periodic";
        var reaction = new JsonObject
        {
            ["componentId"] = "reaction_dispatch",
            ["profile"] = "event_reaction",
            ["priority"] = 50,
            ["payload"] = new JsonObject
            {
                ["eventType"] = "owner_turn_end",
                ["resultKind"] = resultKind,
                ["componentId"] = "reaction_periodic",
                ["dependency"] = "before_current_event",
                ["maxExpansion"] = 1
            }
        };
        effect["components"] = new JsonArray(reaction, periodic.DeepClone());
        effect["triggers"]![0]!["componentIds"] = new JsonArray("reaction_dispatch");
        effect["triggers"]![0]!["resolutionMode"] = resolutionMode;

        var resolution = InvokePeriodicResourceResolution(
            effect,
            "on_owner_turn_end",
            PeriodicEvent(),
            baseline.Targets,
            baseline.Owners,
            baseline.Definitions);

        Assert.True(resolution.IsValid, string.Join("\n", resolution.Issues));
        Assert.Equal(expectedMutations, resolution.Mutations.Count);
        Assert.Equal(expectedPending, resolution.PendingResolutions.Count);
    }

    [Fact]
    public void EventReaction_AfterComponentBindsTargetMutationToExactAppliedPredecessor()
    {
        var baseline = BaselineHealth(current: 10m);
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        var target = effect["components"]![0]!.AsObject();
        target["componentId"] = "reaction_target";
        var predecessor = target.DeepClone().AsObject();
        predecessor["componentId"] = "reaction_predecessor";
        var reaction = new JsonObject
        {
            ["componentId"] = "reaction_dispatch",
            ["profile"] = "event_reaction",
            ["priority"] = 50,
            ["payload"] = new JsonObject
            {
                ["eventType"] = "owner_turn_end",
                ["resultKind"] = "trigger_component",
                ["componentId"] = "reaction_target",
                ["dependency"] = "after_component",
                ["afterComponentId"] = "reaction_predecessor",
                ["maxExpansion"] = 1
            }
        };
        effect["components"] = new JsonArray(
            reaction,
            predecessor,
            target.DeepClone());
        effect["triggers"]![0]!["componentIds"] = new JsonArray(
            "reaction_predecessor",
            "reaction_dispatch");
        effect["triggers"]![0]!["resolutionMode"] = "deterministic";

        var resolution = InvokePeriodicResourceResolution(
            effect,
            "on_owner_turn_end",
            PeriodicEvent(),
            baseline.Targets,
            baseline.Owners,
            baseline.Definitions);

        Assert.True(resolution.IsValid, string.Join("\n", resolution.Issues));
        Assert.Equal(2, resolution.Mutations.Count);
        var byComponent = resolution.ComponentIdsByMutation
            .ToDictionary(static pair => pair.Value, static pair => pair.Key);
        var predecessorKey = byComponent["reaction_predecessor"];
        var targetMutation = Assert.Single(
            resolution.Mutations,
            mutation => mutation.Key == byComponent["reaction_target"]);
        Assert.Equal(new[] { predecessorKey }, targetMutation.Dependencies);
        var requirement = Assert.Single(targetMutation.EventRequirements);
        Assert.Equal(predecessorKey, requirement.Producer);
        Assert.Equal("resource_damaged", requirement.EventKind);
    }

    [Fact]
    public void ResourceTriggerExecution_ReportsOnlyComponentsWhoseMutationActuallyApplied()
    {
        var baseline = BaselineHealth(current: 10m);
        var appliedSource = new ResourceMutationSourceExport(
            "effect_component",
            "component_applied_source",
            FingerprintA,
            ResourceMutationSourceState.Active,
            SameTurn: false,
            new ResourceOwnerKey(
                "mortal_world",
                ResourceOwnerKind.Player,
                "player_current"));
        var skippedSource = new ResourceMutationSourceExport(
            "effect_component",
            "component_skipped_source",
            FingerprintB,
            ResourceMutationSourceState.Active,
            SameTurn: false,
            new ResourceOwnerKey(
                "mortal_world",
                ResourceOwnerKind.Player,
                "player_current"));
        var producerSource = new ResourceMutationSourceExport(
            "effect_component",
            "component_producer_source",
            FingerprintA,
            ResourceMutationSourceState.Active,
            SameTurn: false,
            new ResourceOwnerKey(
                "mortal_world",
                ResourceOwnerKind.Player,
                "player_current"));
        var producer = new ResourceMutationIntent(
            "turn_43:component:producer",
            baseline.Coordinate,
            1m,
            new ResourceMutationSourceRequest(
                producerSource.SourceKind,
                producerSource.SourceId,
                ResourceOperation.Damage),
            Array.Empty<ResourceOperationKey>(),
            Array.Empty<ResourceMutationEventRequirement>(),
            ReceiptId: null);
        var applied = new ResourceMutationIntent(
            "turn_43:component:z_applied",
            baseline.Coordinate,
            1m,
            new ResourceMutationSourceRequest(
                appliedSource.SourceKind,
                appliedSource.SourceId,
                ResourceOperation.Damage),
            Array.Empty<ResourceOperationKey>(),
            Array.Empty<ResourceMutationEventRequirement>(),
            ReceiptId: null);
        var skipped = new ResourceMutationIntent(
            "turn_43:component:a_skipped",
            baseline.Coordinate,
            1m,
            new ResourceMutationSourceRequest(
                skippedSource.SourceKind,
                skippedSource.SourceId,
                ResourceOperation.Damage),
            Array.Empty<ResourceOperationKey>(),
            new[]
            {
                new ResourceMutationEventRequirement(
                    producer.Key,
                    "resource_depleted")
            },
            ReceiptId: null);
        var execution = new EffectAcceptedTurnPlanner.EffectResourceTriggerExecution(
            EffectMaterializationTestFixture.EffectId,
            "on_owner_turn_end",
            "owner_turn_end",
            "turn_43:effect:trigger",
            new[] { applied.Key, skipped.Key },
            RemainingUseBudget: null,
            ComponentIds: new[] { "component_applied", "component_skipped" },
            TriggerEventRef: "turn_43:lifecycle:owner_turn_end",
            ComponentIdsByMutation: new Dictionary<ResourceOperationKey, string>
            {
                [applied.Key] = "component_applied",
                [skipped.Key] = "component_skipped"
            });

        var result = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 43,
                Definitions: baseline.Definitions,
                State: baseline.State,
                History: baseline.History,
                Sources: CreateCatalog(appliedSource, skippedSource, producerSource),
                Mutations: new[] { skipped, applied, producer },
                InitialTriggerExecutions: new[] { execution }),
            IdentityFactory());

        Assert.True(
            result.IsValid,
            string.Join(
                Environment.NewLine,
                result.Issues.Select(issue =>
                    $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
        Assert.Equal(2, result.AppliedTransitions.Count);
        Assert.Empty(result.ReplayTransitions);
        Assert.Equal(
            new[] { "component_applied" },
            Assert.Single(result.ResourceTriggerExecutions).ComponentIds);
    }

    [Theory]
    [InlineData("vehicle", "vehicle_alpha")]
    [InlineData("combat_group_member", "member_alpha")]
    public void SelectedPeriodicComponent_RejectsTargetKindAbsentFromEffectAuthority(
        string targetKind,
        string targetId)
    {
        var baseline = BaselineHealth(current: 10m);
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["target"] = new JsonObject
        {
            ["kind"] = targetKind,
            ["targetId"] = targetId
        };

        var targetResolution = InvokePeriodicResourceResolution(
            effect,
            "on_owner_turn_end",
            PeriodicEvent(),
            baseline.Targets,
            baseline.Owners,
            baseline.Definitions);

        Assert.Contains(targetResolution.Issues, issue =>
            issue.Code == "effect_resource_target_unsupported");
        Assert.Empty(targetResolution.Mutations);
    }

    [Fact]
    public void SelectedPeriodicComponent_CombatMemberTargetUsesGroupMemberResourceOwner()
    {
        const string memberId = "member_periodic_target";
        var baseline = BaselineHealth(
            current: 10m,
            ownerKind: ResourceOwnerKind.CombatGroupMember,
            resourceOwnerId: memberId,
            targetKind: "combatant",
            targetId: memberId,
            boundResourceOwnerKind: ResourceOwnerKind.CombatGroupMember);
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            ownerKind: "combatant");
        effect["target"]!["targetId"] = memberId;

        var resolution = InvokePeriodicResourceResolution(
            effect,
            "on_owner_turn_end",
            PeriodicEvent(),
            baseline.Targets,
            baseline.Owners,
            baseline.Definitions);

        Assert.True(resolution.IsValid, string.Join(Environment.NewLine, resolution.Issues));
        Assert.Equal(baseline.Coordinate, Assert.Single(resolution.Mutations).Coordinate);
    }

    [Fact]
    public void SelectedPeriodicComponent_RejectsInvalidComposedAuthoritySibling()
    {
        var baseline = BaselineHealth(current: 10m);
        var targets = EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
            new[]
            {
                new EffectTargetExport(
                    "mortal_world",
                    "player",
                    "player_current",
                    SameTurn: false),
                new EffectTargetExport(
                    "mortal_world",
                    "npc",
                    "npc_duplicate",
                    SameTurn: false),
                new EffectTargetExport(
                    "mortal_world",
                    "npc",
                    "npc_duplicate",
                    SameTurn: false)
            },
            Array.Empty<EffectTargetExport>(),
            new HashSet<string>(StringComparer.Ordinal),
            CombatantIdentities: null));
        var duplicateItem = new ResourceOwnerExport(
            new ResourceOwnerKey(
                "mortal_world",
                ResourceOwnerKind.Item,
                "item_duplicate"),
            ResourceOwnerLifecycle.Active,
            SameTurn: false,
            OwnerRef: null,
            BoundNpcId: null,
            new HashSet<string>(StringComparer.Ordinal) { "charges" },
            FingerprintB);
        var owners = ResourceOwnerAuthority.Build(new ResourceOwnerAuthorityInput(
            baseline.Owners.ExportInput().PreTurnOwners
                .Concat(new[] { duplicateItem, duplicateItem })
                .ToArray(),
            Array.Empty<ResourceOwnerExport>(),
            Array.Empty<ResourceOwnerKey>()));
        Assert.Contains(targets.Issues, issue =>
            issue.Code == "effect_target_authority_duplicate_target");
        Assert.Contains(owners.Issues, issue =>
            issue.Code == "resource_owner_identity_duplicate");

        var resolution = InvokePeriodicResourceResolution(
            EffectMaterializationTestFixture.CreateCanonicalEffect(),
            "on_owner_turn_end",
            PeriodicEvent(),
            targets,
            owners,
            baseline.Definitions);

        Assert.Contains(resolution.Issues, issue =>
            issue.Code == "effect_target_authority_duplicate_target");
        Assert.Contains(resolution.Issues, issue =>
            issue.Code == "resource_owner_identity_duplicate");
        Assert.Empty(resolution.SourceExports);
        Assert.Empty(resolution.Mutations);
    }

    [Fact]
    public void SelectedPeriodicComponent_RejectsOwnerWithoutExactResourceCapability()
    {
        var baseline = BaselineHealth(current: 10m);
        var owners = ResourceOwnerAuthority.Build(new ResourceOwnerAuthorityInput(
            new[]
            {
                new ResourceOwnerExport(
                    new ResourceOwnerKey("mortal_world", ResourceOwnerKind.Player, "player_current"),
                    ResourceOwnerLifecycle.Active,
                    SameTurn: false,
                    OwnerRef: null,
                    BoundNpcId: null,
                    new HashSet<string>(StringComparer.Ordinal) { "energy" },
                    FingerprintA)
            },
            Array.Empty<ResourceOwnerExport>(),
            Array.Empty<ResourceOwnerKey>()));
        Assert.Empty(owners.Issues);

        var resolution = InvokePeriodicResourceResolution(
            EffectMaterializationTestFixture.CreateCanonicalEffect(),
            "on_owner_turn_end",
            PeriodicEvent(),
            baseline.Targets,
            owners,
            baseline.Definitions);

        Assert.Contains(resolution.Issues, issue =>
            issue.Code == "resource_owner_capability_missing");
        Assert.Empty(resolution.Mutations);
    }

    [Theory]
    [InlineData("npc", "Npc", "npc_test_healer", null)]
    [InlineData("combatant", "Combatant", EffectMaterializationTestFixture.CombatantId, null)]
    [InlineData("combatant", "Npc", "npc_test_healer", "npc_test_healer")]
    public void SelectedPeriodicComponent_MapsPermittedMortalTargetToExactResourceOwner(
        string targetKind,
        string ownerKindToken,
        string resourceOwnerId,
        string? boundNpcId)
    {
        var ownerKind = Enum.Parse<ResourceOwnerKind>(ownerKindToken);
        var targetId = targetKind == "combatant"
            ? EffectMaterializationTestFixture.CombatantId
            : resourceOwnerId;
        var baseline = BaselineHealth(
            current: 10m,
            ownerKind,
            resourceOwnerId,
            targetKind,
            targetId,
            boundNpcId);
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(targetKind);

        var resolution = InvokePeriodicResourceResolution(
            effect,
            "on_owner_turn_end",
            PeriodicEvent(),
            baseline.Targets,
            baseline.Owners,
            baseline.Definitions);

        Assert.Empty(resolution.Issues);
        Assert.Equal(
            baseline.Coordinate,
            Assert.Single(resolution.Mutations).Coordinate);
    }

    [Fact]
    public void SelectedPeriodicComponent_MapsAfterlifeActorToSettingDefinedResource()
    {
        var baseline = BaselineAfterlifeIntegrity(current: 10m);
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["realm"] = "chaos_sea";
        effect["target"] = new JsonObject
        {
            ["kind"] = "afterlife_actor",
            ["targetId"] = "afterlife_actor_alpha"
        };
        effect["components"]![0]!["payload"]!["resource"] = "soul_integrity";

        var resolution = InvokePeriodicResourceResolution(
            effect,
            "on_owner_turn_end",
            PeriodicEvent(),
            baseline.Targets,
            baseline.Owners,
            baseline.Definitions);

        Assert.Empty(resolution.Issues);
        Assert.Equal(
            baseline.Coordinate,
            Assert.Single(resolution.Mutations).Coordinate);
    }

    [Fact]
    public void SelectedPeriodicComponent_MapsRealmSpecificPlayerSoulToAfterlifeOwner()
    {
        var definitions = CreateAfterlifeIntegrityDefinitions();
        var coordinate = new ResourceCoordinate(
            "chaos_sea",
            ResourceOwnerKind.AfterlifeActor,
            "player_soul",
            "soul_integrity");
        var baseline = BaselinePeriodicResource(
            definitions,
            coordinate,
            current: 10m,
            targetKind: "player",
            targetId: "player_soul",
            boundNpcId: null);
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["realm"] = "chaos_sea";
        effect["target"] = new JsonObject
        {
            ["kind"] = "player",
            ["targetId"] = "player_soul"
        };
        effect["components"]![0]!["payload"]!["resource"] = "soul_integrity";

        var resolution = InvokePeriodicResourceResolution(
            effect,
            "on_owner_turn_end",
            PeriodicEvent(),
            baseline.Targets,
            baseline.Owners,
            baseline.Definitions);

        Assert.Empty(resolution.Issues);
        Assert.Equal(
            baseline.Coordinate,
            Assert.Single(resolution.Mutations).Coordinate);
    }

    [Fact]
    public void SelectedPeriodicComponent_ResolvesAcceptedSameTurnPermanentTargetInternally()
    {
        var baseline = BaselineHealth(
            current: 10m,
            ResourceOwnerKind.Npc,
            "npc_same_turn",
            targetKind: "npc",
            targetId: "npc_same_turn");
        var owners = ResourceOwnerAuthority.Build(new ResourceOwnerAuthorityInput(
            Array.Empty<ResourceOwnerExport>(),
            new[]
            {
                new ResourceOwnerExport(
                    new ResourceOwnerKey(
                        "mortal_world",
                        ResourceOwnerKind.Npc,
                        "npc_same_turn"),
                    ResourceOwnerLifecycle.Active,
                    SameTurn: true,
                    OwnerRef: "npc_ref_same_turn",
                    BoundNpcId: "npc_same_turn",
                    new HashSet<string>(StringComparer.Ordinal) { "health" },
                    FingerprintA)
            },
            Array.Empty<ResourceOwnerKey>()));
        var targets = EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
            Array.Empty<EffectTargetExport>(),
            new[]
            {
                new EffectTargetExport(
                    "mortal_world",
                    "npc",
                    "npc_same_turn",
                    SameTurn: true,
                    TargetRef: "npc_ref_same_turn")
            },
            new HashSet<string>(StringComparer.Ordinal),
            CombatantIdentities: null));
        Assert.Empty(owners.Issues);
        Assert.Empty(targets.Issues);
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect("npc");
        effect["target"]!["targetId"] = "npc_same_turn";

        var resolution = InvokePeriodicResourceResolution(
            effect,
            "on_owner_turn_end",
            PeriodicEvent(),
            targets,
            owners,
            baseline.Definitions);

        Assert.Empty(resolution.Issues);
        Assert.Equal(
            baseline.Coordinate,
            Assert.Single(resolution.Mutations).Coordinate);
    }

    [Fact]
    public void BoundedPeriodicComponent_BindsSameTurnTargetAndResourceByExactRef()
    {
        var baseline = BaselineHealth(
            current: 10m,
            ResourceOwnerKind.Npc,
            "npc_same_turn",
            targetKind: "npc",
            targetId: "npc_same_turn");
        var owners = ResourceOwnerAuthority.Build(new ResourceOwnerAuthorityInput(
            Array.Empty<ResourceOwnerExport>(),
            new[]
            {
                new ResourceOwnerExport(
                    new ResourceOwnerKey(
                        "mortal_world",
                        ResourceOwnerKind.Npc,
                        "npc_same_turn"),
                    ResourceOwnerLifecycle.Active,
                    SameTurn: true,
                    OwnerRef: "npc_ref_same_turn",
                    BoundNpcId: "npc_same_turn",
                    new HashSet<string>(StringComparer.Ordinal) { "health" },
                    FingerprintA)
            },
            Array.Empty<ResourceOwnerKey>()));
        var targets = EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
            Array.Empty<EffectTargetExport>(),
            new[]
            {
                new EffectTargetExport(
                    "mortal_world",
                    "npc",
                    "npc_same_turn",
                    SameTurn: true,
                    TargetRef: "npc_ref_same_turn")
            },
            new HashSet<string>(StringComparer.Ordinal),
            CombatantIdentities: null));
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect("npc");
        effect["target"]!["targetId"] = "npc_same_turn";
        effect["source"]!["sourceId"] = "wound_same_turn";
        effect["triggers"]![0]!["resolutionMode"] = "bounded_receipt";
        var source = new EffectSourceAuthorityEntry(
            new EffectSourceKey(
                "mortal_world",
                "wound",
                "wound_same_turn",
                "bleeding_consequence"),
            new JsonObject(),
            Materializable: true,
            Active: true,
            SameTurn: true,
            SourceRef: "wound_ref_same_turn",
            new HashSet<string>(StringComparer.Ordinal) { "active" });

        var resolution = EffectAcceptedTurnPlanner.ResolvePeriodicResourceMutations(
            effect,
            "on_owner_turn_end",
            PeriodicEvent(),
            targets,
            owners,
            baseline.Definitions,
            source);

        Assert.Empty(resolution.Issues);
        Assert.Empty(resolution.Mutations);
        var pending = Assert.Single(resolution.PendingResolutions);
        Assert.Equal(
            new ResourcePendingAuthorityBinding(
                "same_turn_ref",
                "npc_ref_same_turn"),
            pending.TargetAuthority);
        Assert.Equal(
            new ResourcePendingAuthorityBinding(
                "same_turn_ref",
                "npc_ref_same_turn"),
            pending.ResourceAuthority);
        Assert.Equal(
            new ResourcePendingAuthorityBinding(
                "permanent",
                "effect_test_bleeding"),
            pending.EffectAuthority);
        Assert.Equal(
            new ResourcePendingAuthorityBinding(
                "same_turn_ref",
                "wound_ref_same_turn"),
            pending.SourceAuthority);
    }

    [Fact]
    public void BoundedPeriodicComponent_IgnoresUnpublishedDefinitionIdentityInAuthorityBinding()
    {
        var baseline = BaselineHealth(current: 10m);
        Assert.True(baseline.Definitions.TryResolveExact("health", out var health));
        var empty = ResourceDefinitionCatalog.ParseCanonical(
            json: null,
            allowMissingPristine: true).Catalog!;
        var firstDefinitions = empty.With(health! with
        {
            Materialization = new ResourceDefinitionMaterialization(
                ResourceMaterializationContract.SchemaVersion,
                "resource_definition_health_first",
                "resource_definition_seal_health_first",
                42,
                "turn_42:resource:1")
        });
        var secondDefinitions = empty.With(health with
        {
            Materialization = new ResourceDefinitionMaterialization(
                ResourceMaterializationContract.SchemaVersion,
                "resource_definition_health_second",
                "resource_definition_seal_health_second",
                42,
                "turn_42:resource:1")
        });
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["triggers"]![0]!["resolutionMode"] = "bounded_receipt";

        var first = InvokePeriodicResourceResolution(
            effect,
            "on_owner_turn_end",
            PeriodicEvent(),
            baseline.Targets,
            baseline.Owners,
            firstDefinitions);
        var second = InvokePeriodicResourceResolution(
            effect,
            "on_owner_turn_end",
            PeriodicEvent(),
            baseline.Targets,
            baseline.Owners,
            secondDefinitions);

        Assert.Empty(first.Issues);
        Assert.Empty(second.Issues);
        var firstPending = Assert.Single(first.PendingResolutions);
        var secondPending = Assert.Single(second.PendingResolutions);
        Assert.Equal(
            firstPending.SourceAuthorityFingerprint,
            secondPending.SourceAuthorityFingerprint);
        Assert.Equal(firstPending.PolicyFingerprint, secondPending.PolicyFingerprint);
    }

    [Fact]
    public void SelectedPeriodicComponent_BindsSourceIdentityToEffectTriggerAndComponent()
    {
        var baseline = BaselineHealth(current: 10m);
        var firstEffect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        var secondEffect = firstEffect.DeepClone().AsObject();
        secondEffect["effectId"] = "effect_test_bleeding_other";
        var secondTrigger = firstEffect["triggers"]![0]!.DeepClone().AsObject();
        secondTrigger["triggerId"] = "on_owner_turn_end_secondary";
        firstEffect["triggers"]!.AsArray().Add(secondTrigger);

        var first = InvokePeriodicResourceResolution(
            firstEffect,
            "on_owner_turn_end",
            PeriodicEvent(),
            baseline.Targets,
            baseline.Owners,
            baseline.Definitions);
        var sameComponentOtherTrigger = InvokePeriodicResourceResolution(
            firstEffect,
            "on_owner_turn_end_secondary",
            PeriodicEvent() with
            {
                EventRef = "turn_43:effect:on_owner_turn_end_secondary",
                TriggerId = "on_owner_turn_end_secondary"
            },
            baseline.Targets,
            baseline.Owners,
            baseline.Definitions);
        var sameComponentOtherEffect = InvokePeriodicResourceResolution(
            secondEffect,
            "on_owner_turn_end",
            PeriodicEvent(),
            baseline.Targets,
            baseline.Owners,
            baseline.Definitions);

        var sourceIds = new[] { first, sameComponentOtherTrigger, sameComponentOtherEffect }
            .Select(result => Assert.Single(result.SourceExports).SourceId)
            .ToArray();
        Assert.Equal(3, sourceIds.Distinct(StringComparer.Ordinal).Count());
        Assert.All(sourceIds, sourceId =>
            Assert.StartsWith("effect_component_", sourceId, StringComparison.Ordinal));
    }

    [Fact]
    public void SelectedPeriodicComponent_RejectsMismatchedTriggerEventPhase()
    {
        var baseline = BaselineHealth(current: 10m);

        var resolution = InvokePeriodicResourceResolution(
            EffectMaterializationTestFixture.CreateCanonicalEffect(),
            "on_owner_turn_end",
            PeriodicEvent() with { Phase = "owner_turn_start" },
            baseline.Targets,
            baseline.Owners,
            baseline.Definitions);

        Assert.Contains(resolution.Issues, issue =>
            issue.Code == "effect_resource_trigger_event_mismatch");
        Assert.Empty(resolution.Mutations);
    }

    [Fact]
    public void SelectedPeriodicDamage_EnforcesClosedFloorPolicyInCommonReducer()
    {
        var baseline = BaselineHealth(current: 10m);
        var surviveAtOne = EffectMaterializationTestFixture.CreateCanonicalEffect();
        surviveAtOne["components"]![0]!["payload"]!["amount"] = 9;
        surviveAtOne["components"]![0]!["payload"]!["floorPolicy"] =
            "cannot_reduce_below_one";
        var cannotReachZero = EffectMaterializationTestFixture.CreateCanonicalEffect();
        cannotReachZero["components"]![0]!["payload"]!["amount"] = 10;
        cannotReachZero["components"]![0]!["payload"]!["floorPolicy"] =
            "cannot_reduce_below_one";
        var mayReachZero = EffectMaterializationTestFixture.CreateCanonicalEffect();
        mayReachZero["components"]![0]!["payload"]!["amount"] = 10;
        mayReachZero["components"]![0]!["payload"]!["floorPolicy"] =
            "may_reach_zero";

        var atOne = ResolveAndApplyPeriodicResource(surviveAtOne, baseline);
        var rejectedZero = ResolveAndApplyPeriodicResource(cannotReachZero, baseline);
        var atZero = ResolveAndApplyPeriodicResource(mayReachZero, baseline);

        Assert.True(atOne.IsValid, string.Join(Environment.NewLine, atOne.Issues));
        Assert.Equal(1m, Assert.Single(atOne.StateAfterImage!.Entries).Current);
        Assert.False(rejectedZero.IsValid);
        Assert.Contains(rejectedZero.Issues, issue =>
            issue.Code == "resource_mutation_result_constraint_violated");
        Assert.True(atZero.IsValid, string.Join(Environment.NewLine, atZero.Issues));
        Assert.Equal(0m, Assert.Single(atZero.StateAfterImage!.Entries).Current);
    }

    [Fact]
    public void SelectedPeriodicComponent_LeavesQuantumAndBoundsToCommonReducer()
    {
        var quantumBaseline = BaselineHealth(current: 10m);
        var quantumEffect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        quantumEffect["components"]![0]!["payload"]!["amount"] = 0.5m;
        var quantumResolution = InvokePeriodicResourceResolution(
            quantumEffect,
            "on_owner_turn_end",
            PeriodicEvent(),
            quantumBaseline.Targets,
            quantumBaseline.Owners,
            quantumBaseline.Definitions);
        Assert.Empty(quantumResolution.Issues);
        var quantumSource = Assert.Single(quantumResolution.SourceExports);
        var quantumMutation = Assert.Single(quantumResolution.Mutations);

        var quantumResult = AcceptedMechanicsPlanner.BuildResources(
            ResourceInput(quantumBaseline, quantumSource, quantumMutation),
            IdentityFactory());

        Assert.False(quantumResult.IsValid);
        Assert.Contains(quantumResult.Issues, issue =>
            issue.Code == "resource_mutation_amount_invalid");

        var floorResult = ApplyPeriodicComponent(
            BaselineHealth(current: 10m),
            profile: "periodic_damage",
            amount: 12m);
        var capResult = ApplyPeriodicComponent(
            BaselineHealth(current: 5m),
            profile: "periodic_restore",
            amount: 12m);
        Assert.True(floorResult.IsValid, string.Join(Environment.NewLine, floorResult.Issues));
        Assert.True(capResult.IsValid, string.Join(Environment.NewLine, capResult.Issues));
        Assert.Equal(0m, Assert.Single(floorResult.StateAfterImage!.Entries).Current);
        Assert.Equal(
            ResourceTransitionOutcome.ClampedMinimum,
            Assert.Single(floorResult.AppliedTransitions).Outcome);
        Assert.Equal(10m, Assert.Single(capResult.StateAfterImage!.Entries).Current);
        Assert.Equal(
            ResourceTransitionOutcome.ClampedMaximum,
            Assert.Single(capResult.AppliedTransitions).Outcome);
    }

    [Fact]
    public void SelectedPeriodicComponent_ExactReplayDoesNotAppendOrEmitAgain()
    {
        var baseline = BaselineHealth(current: 10m);
        var resolution = InvokePeriodicResourceResolution(
            EffectMaterializationTestFixture.CreateCanonicalEffect(),
            "on_owner_turn_end",
            PeriodicEvent(),
            baseline.Targets,
            baseline.Owners,
            baseline.Definitions);
        Assert.Empty(resolution.Issues);
        var source = Assert.Single(resolution.SourceExports);
        var mutation = Assert.Single(resolution.Mutations);
        var first = AcceptedMechanicsPlanner.BuildResources(
            ResourceInput(baseline, source, mutation),
            IdentityFactory());
        Assert.True(first.IsValid, string.Join(Environment.NewLine, first.Issues));

        var replay = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: PeriodicEvent().Turn,
                Definitions: baseline.Definitions,
                State: first.StateAfterImage!,
                History: first.HistoryAfterImage!,
                Sources: CreateCatalog(source),
                Mutations: new[] { mutation }),
            IdentityFactory(seed: 50));

        Assert.True(replay.IsValid, string.Join(Environment.NewLine, replay.Issues));
        Assert.Empty(replay.AppliedTransitions);
        Assert.Empty(replay.Events);
        Assert.Single(replay.ReplayTransitions);
        Assert.Equal(first.StateAfterImage!.ToCanonicalJson(), replay.StateAfterImage!.ToCanonicalJson());
        Assert.Equal(first.HistoryAfterImage!.ToCanonicalJson(), replay.HistoryAfterImage!.ToCanonicalJson());
    }

    [Fact]
    public void Planner_ChangedResultConstraintConflictsWithImmutableReplayPolicy()
    {
        var baseline = BaselineHealth(current: 10m);
        var resolution = InvokePeriodicResourceResolution(
            EffectMaterializationTestFixture.CreateCanonicalEffect(),
            "on_owner_turn_end",
            PeriodicEvent(),
            baseline.Targets,
            baseline.Owners,
            baseline.Definitions);
        Assert.Empty(resolution.Issues);
        var source = Assert.Single(resolution.SourceExports);
        var mutation = Assert.Single(resolution.Mutations);
        var first = AcceptedMechanicsPlanner.BuildResources(
            ResourceInput(baseline, source, mutation),
            IdentityFactory());
        Assert.True(first.IsValid, string.Join(Environment.NewLine, first.Issues));

        var changedConstraint = mutation with
        {
            ResultConstraint = new ResourceMutationResultConstraint(
                RejectBelow: 1m,
                RejectAbove: null)
        };
        var replay = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: PeriodicEvent().Turn,
                Definitions: baseline.Definitions,
                State: first.StateAfterImage!,
                History: first.HistoryAfterImage!,
                Sources: CreateCatalog(source),
                Mutations: new[] { changedConstraint }),
            IdentityFactory(seed: 50));

        Assert.False(replay.IsValid);
        Assert.Contains(replay.Issues, issue =>
            issue.Code == "resource_transition_conflicting_replay");
        Assert.Null(replay.StateAfterImage);
        Assert.Null(replay.HistoryAfterImage);
    }

    [Fact]
    public void OrdinaryAndSelectedPeriodicDamage_AreSemanticallyByteEquivalentAcrossOneHundredRuns()
    {
        var baseline = BaselineHealth(current: 10m);
        var ordinarySource = new ResourceMutationSourceExport(
            "combat_outcome",
            "ordinary_equivalent_damage",
            FingerprintB,
            ResourceMutationSourceState.Active,
            SameTurn: false,
            PlayerOwner);
        var ordinaryMutation = new ResourceMutationIntent(
            PeriodicEvent().EventRef,
            HealthCoordinate,
            Amount: 3m,
            new ResourceMutationSourceRequest(
                ordinarySource.SourceKind,
                ordinarySource.SourceId,
                ResourceOperation.Damage),
            Array.Empty<ResourceOperationKey>(),
            Array.Empty<ResourceMutationEventRequirement>(),
            ReceiptId: null);
        var ordinary = AcceptedMechanicsPlanner.BuildResources(
            ResourceInput(baseline, ordinarySource, ordinaryMutation),
            IdentityFactory());
        Assert.True(ordinary.IsValid, string.Join(Environment.NewLine, ordinary.Issues));
        var expected = SerializeSemanticResourceResult(ordinary);

        for (var run = 0; run < 100; run++)
        {
            var resolution = InvokePeriodicResourceResolution(
                EffectMaterializationTestFixture.CreateCanonicalEffect(),
                "on_owner_turn_end",
                PeriodicEvent(),
                baseline.Targets,
                baseline.Owners,
                baseline.Definitions);
            Assert.Empty(resolution.Issues);
            var source = Assert.Single(resolution.SourceExports);
            var mutation = Assert.Single(resolution.Mutations);
            var noise = new[]
            {
                new ResourceMutationSourceExport(
                    "narrative_outcome",
                    "noise_alpha",
                    FingerprintA,
                    ResourceMutationSourceState.Active,
                    SameTurn: false,
                    PlayerOwner),
                new ResourceMutationSourceExport(
                    "registered_system_outcome",
                    "noise_beta",
                    FingerprintB,
                    ResourceMutationSourceState.Active,
                    SameTurn: false),
                source
            };
            var randomized = run % 2 == 0
                ? noise
                : noise.Reverse().ToArray();
            var actual = AcceptedMechanicsPlanner.BuildResources(
                new AcceptedMechanicsResourceInput(
                    Turn: PeriodicEvent().Turn,
                    Definitions: baseline.Definitions,
                    State: baseline.State,
                    History: baseline.History,
                    Sources: CreateCatalog(randomized),
                    Mutations: new[] { mutation }),
                IdentityFactory(seed: run + 100));

            Assert.True(actual.IsValid, string.Join(Environment.NewLine, actual.Issues));
            Assert.Equal(expected, SerializeSemanticResourceResult(actual));
        }
    }

    [Theory]
    [InlineData(3, 3, 2, 3)]
    [InlineData(3, 2, 1, 1)]
    public void ResourceEventTriggeredPeriodicRestore_ReentersSameReducerOnlyOnActualBoundaryEvent(
        int current,
        int damage,
        int expectedTransitionCount,
        int expectedCurrent)
    {
        var baseline = BaselineHealth(current);
        var rootSource = new ResourceMutationSourceExport(
            "combat_outcome",
            "combat_damage_resource_event",
            FingerprintB,
            ResourceMutationSourceState.Active,
            SameTurn: false,
            PlayerOwner);
        var root = new ResourceMutationIntent(
            "turn_43:combat_damage:1",
            HealthCoordinate,
            damage,
            new ResourceMutationSourceRequest(
                rootSource.SourceKind,
                rootSource.SourceId,
                ResourceOperation.Damage),
            Array.Empty<ResourceOperationKey>(),
            Array.Empty<ResourceMutationEventRequirement>(),
            ReceiptId: null);
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "periodic_restore");
        effect["triggers"]![0]!["triggerId"] = "on_resource_depleted";
        effect["triggers"]![0]!["eventType"] = "resource_depleted";
        var resolution = InvokeResourceEventResolution(
            effect,
            "on_resource_depleted",
            root.Key,
            "resource_depleted",
            turn: 43,
            baseline.Targets,
            baseline.Owners,
            baseline.Definitions);
        Assert.Empty(resolution.Issues);
        var effectSource = Assert.Single(resolution.SourceExports);
        var effectMutation = Assert.Single(resolution.Mutations);

        var result = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 43,
                Definitions: baseline.Definitions,
                State: baseline.State,
                History: baseline.History,
                Sources: CreateCatalog(rootSource, effectSource),
                Mutations: new[] { effectMutation, root }),
            IdentityFactory());

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        Assert.Equal(expectedCurrent, Assert.Single(result.StateAfterImage!.Entries).Current);
        Assert.Equal(expectedTransitionCount, result.AppliedTransitions.Count);
        if (expectedTransitionCount == 2)
        {
            Assert.Equal(
                new[] { rootSource.SourceId, effectSource.SourceId },
                result.AppliedTransitions.Select(static transition => transition.OriginId));
            Assert.Equal(
                new[]
                {
                    "resource_damaged",
                    "resource_depleted",
                    "resource_restored"
                },
                result.Events.Select(static resourceEvent => resourceEvent.EventKind));
        }
        else
        {
            Assert.DoesNotContain(result.AppliedTransitions, transition =>
                transition.OriginId == effectSource.SourceId);
        }
    }

    [Fact]
    public void ResourceEventCycle_IsRejectedBeforeAnyReducerAppend()
    {
        var baseline = BaselineHealth(current: 3m);
        var rootSource = new ResourceMutationSourceExport(
            "combat_outcome",
            "cycle_root",
            FingerprintA,
            ResourceMutationSourceState.Active,
            SameTurn: false,
            PlayerOwner);
        var root = new ResourceMutationIntent(
            "turn_43:cycle:root",
            baseline.Coordinate,
            3m,
            new ResourceMutationSourceRequest(
                rootSource.SourceKind,
                rootSource.SourceId,
                ResourceOperation.Damage),
            Array.Empty<ResourceOperationKey>(),
            Array.Empty<ResourceMutationEventRequirement>(),
            ReceiptId: null);

        var result = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 43,
                Definitions: baseline.Definitions,
                State: baseline.State,
                History: baseline.History,
                Sources: CreateCatalog(rootSource),
                Mutations: new[] { root },
                EventMutationResolver: ResolveCycle),
            IdentityFactory());

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "resource_graph_cycle");
        Assert.Equal(0, result.Statistics.HistoryAppendCount);

        EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution ResolveCycle(
            ResourceAppliedEvent resourceEvent,
            ResourceOperationKey producer)
        {
            var restores = string.Equals(
                resourceEvent.EventKind,
                "resource_damaged",
                StringComparison.Ordinal);
            var damages = string.Equals(
                resourceEvent.EventKind,
                "resource_restored",
                StringComparison.Ordinal);
            if (!restores && !damages)
            {
                return new EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution(
                    Array.Empty<ResourceMutationSourceExport>(),
                    Array.Empty<ResourceMutationIntent>(),
                    Array.Empty<ValidationIssue>());
            }

            var effectId = restores ? "effect_cycle_a" : "effect_cycle_b";
            var triggerId = restores ? "on_depleted" : "on_filled";
            var sourceId = restores ? "cycle_effect_a" : "cycle_effect_b";
            var fingerprint = restores ? FingerprintA : FingerprintB;
            var operation = restores
                ? ResourceOperation.Restore
                : ResourceOperation.Damage;
            var eventRef = producer.EventRef + ":" + triggerId;
            var source = new ResourceMutationSourceExport(
                "effect_component",
                sourceId,
                fingerprint,
                ResourceMutationSourceState.Active,
                SameTurn: false,
                new ResourceOwnerKey(
                    producer.Coordinate.Realm,
                    producer.Coordinate.OwnerKind,
                    producer.Coordinate.ResourceOwnerId));
            var mutation = new ResourceMutationIntent(
                eventRef,
                producer.Coordinate,
                3m,
                new ResourceMutationSourceRequest(
                    source.SourceKind,
                    source.SourceId,
                    operation),
                new[] { producer },
                new[]
                {
                    new ResourceMutationEventRequirement(
                        producer,
                        resourceEvent.EventKind)
                },
                ReceiptId: null);
            return new EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution(
                new[] { source },
                new[] { mutation },
                Array.Empty<ValidationIssue>())
            {
                TriggerExecutions = new[]
                {
                    new EffectAcceptedTurnPlanner.EffectResourceTriggerExecution(
                        effectId,
                        triggerId,
                        resourceEvent.EventKind,
                        eventRef,
                        new[] { mutation.Key },
                        RemainingUseBudget: null)
                }
            };
        }
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

    private static AcceptedMechanicsResourceInput ResourceInput(
        PeriodicBaseline baseline,
        ResourceMutationSourceExport source,
        ResourceMutationIntent mutation) =>
        new(
            Turn: PeriodicEvent().Turn,
            Definitions: baseline.Definitions,
            State: baseline.State,
            History: baseline.History,
            Sources: CreateCatalog(source),
            Mutations: new[] { mutation });

    private static AcceptedMechanicsResourcePlanningResult ApplyPeriodicComponent(
        PeriodicBaseline baseline,
        string profile,
        decimal amount)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            ownerKind: "player",
            profile);
        effect["components"]![0]!["payload"]!["amount"] = amount;
        var resolution = InvokePeriodicResourceResolution(
            effect,
            "on_owner_turn_end",
            PeriodicEvent(),
            baseline.Targets,
            baseline.Owners,
            baseline.Definitions);
        Assert.Empty(resolution.Issues);
        var source = Assert.Single(resolution.SourceExports);
        var mutation = Assert.Single(resolution.Mutations);
        return AcceptedMechanicsPlanner.BuildResources(
            ResourceInput(baseline, source, mutation),
            IdentityFactory());
    }

    private static AcceptedMechanicsResourcePlanningResult ResolveAndApplyPeriodicResource(
        JsonObject effect,
        PeriodicBaseline baseline)
    {
        var resolution = InvokePeriodicResourceResolution(
            effect,
            "on_owner_turn_end",
            PeriodicEvent(),
            baseline.Targets,
            baseline.Owners,
            baseline.Definitions);
        Assert.Empty(resolution.Issues);
        var source = Assert.Single(resolution.SourceExports);
        var mutation = Assert.Single(resolution.Mutations);
        return AcceptedMechanicsPlanner.BuildResources(
            ResourceInput(baseline, source, mutation),
            IdentityFactory());
    }

    private static EffectLifecycleEvent PeriodicEvent() => new(
        EventRef: "turn_43:effect:on_owner_turn_end",
        Turn: 43,
        Phase: "owner_turn_end",
        TriggerId: "on_owner_turn_end");

    private static string SerializeSemanticResourceResult(
        AcceptedMechanicsResourcePlanningResult result)
    {
        var state = Assert.Single(result.StateAfterImage!.Entries);
        var transition = Assert.Single(result.AppliedTransitions);
        return new JsonObject
        {
            ["state"] = SnapshotNode(new ResourceStateSnapshot(
                state.Current,
                state.Maximum,
                state.CapacityBinding,
                state.State)),
            ["transition"] = new JsonObject
            {
                ["eventRef"] = transition.EventRef,
                ["coordinate"] = CoordinateNode(transition.Coordinate),
                ["operation"] = transition.Operation.ToString(),
                ["requestedAmount"] = transition.RequestedAmount,
                ["appliedAmount"] = transition.AppliedAmount,
                ["outcome"] = transition.Outcome.ToString(),
                ["capacityDisposition"] = transition.CapacityDisposition?.ToString(),
                ["beforeState"] = SnapshotNode(transition.BeforeState),
                ["afterState"] = SnapshotNode(transition.AfterState),
                ["turn"] = transition.Turn,
                ["executionSequence"] = transition.ExecutionSequence
            },
            ["events"] = new JsonArray(result.Events.Select(value => (JsonNode)new JsonObject
            {
                ["eventKind"] = value.EventKind,
                ["eventRef"] = value.EventRef,
                ["coordinate"] = CoordinateNode(value.Coordinate),
                ["before"] = value.Before,
                ["after"] = value.After,
                ["appliedAmount"] = value.AppliedAmount,
                ["turn"] = value.Turn,
                ["executionSequence"] = value.ExecutionSequence
            }).ToArray())
        }.ToJsonString();
    }

    private static JsonObject CoordinateNode(ResourceCoordinate coordinate) => new()
    {
        ["realm"] = coordinate.Realm,
        ["ownerKind"] = coordinate.OwnerKind.ToString(),
        ["resourceOwnerId"] = coordinate.ResourceOwnerId,
        ["resourceKey"] = coordinate.ResourceKey
    };

    private static JsonNode? SnapshotNode(ResourceStateSnapshot? snapshot) =>
        snapshot == null
            ? null
            : new JsonObject
            {
                ["current"] = snapshot.Current,
                ["maximum"] = snapshot.Maximum,
                ["capacityBinding"] = new JsonObject
                {
                    ["kind"] = snapshot.CapacityBinding.Kind.ToString(),
                    ["authorityKey"] = snapshot.CapacityBinding.AuthorityKey,
                    ["authorityFingerprint"] = snapshot.CapacityBinding.AuthorityFingerprint
                },
                ["state"] = snapshot.State.ToString()
            };

    private static EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution
        InvokePeriodicResourceResolution(
        JsonObject effect,
        string triggerId,
        EffectLifecycleEvent acceptedEvent,
        EffectTargetAuthority targets,
        ResourceOwnerAuthority owners,
        ResourceDefinitionCatalog definitions) =>
        EffectAcceptedTurnPlanner.ResolvePeriodicResourceMutations(
            effect,
            triggerId,
            acceptedEvent,
            targets,
            owners,
            definitions);

    private static EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution
        InvokeResourceEventResolution(
            JsonObject effect,
            string triggerId,
            ResourceOperationKey producer,
            string eventKind,
            int turn,
            EffectTargetAuthority targets,
            ResourceOwnerAuthority owners,
            ResourceDefinitionCatalog definitions)
        => EffectAcceptedTurnPlanner.ResolveResourceEventMutations(
            effect,
            triggerId,
            producer,
            eventKind,
            turn,
            targets,
            owners,
            definitions);

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
        sources = sources.Select(source =>
                RequiresPlayerTargetBinding(source.SourceKind) &&
                source.BoundOwner == null
                    ? source with
                    {
                        BoundOwner = new ResourceOwnerKey(
                            ChargesCoordinate.Realm,
                            ChargesCoordinate.OwnerKind,
                            ChargesCoordinate.ResourceOwnerId)
                    }
                    : source)
            .ToArray();

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

    private static PeriodicBaseline BaselineHealth(
        decimal current,
        ResourceOwnerKind ownerKind = ResourceOwnerKind.Player,
        string resourceOwnerId = "player_current",
        string targetKind = "player",
        string targetId = "player_current",
        string? boundNpcId = null,
        ResourceOwnerKind? boundResourceOwnerKind = null)
    {
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var coordinate = new ResourceCoordinate(
            "mortal_world",
            ownerKind,
            resourceOwnerId,
            "health");
        return BaselinePeriodicResource(
            definitions,
            coordinate,
            current,
            targetKind,
            targetId,
            boundNpcId,
            boundResourceOwnerKind);
    }

    private static PeriodicBaseline BaselineAfterlifeIntegrity(decimal current)
    {
        var definitions = CreateAfterlifeIntegrityDefinitions();
        var coordinate = new ResourceCoordinate(
            "chaos_sea",
            ResourceOwnerKind.AfterlifeActor,
            "afterlife_actor_alpha",
            "soul_integrity");
        return BaselinePeriodicResource(
            definitions,
            coordinate,
            current,
            targetKind: "afterlife_actor",
            targetId: coordinate.ResourceOwnerId,
            boundNpcId: null);
    }

    private static PeriodicBaseline BaselinePeriodicResource(
        ResourceDefinitionCatalog definitions,
        ResourceCoordinate coordinate,
        decimal current,
        string targetKind,
        string targetId,
        string? boundNpcId,
        ResourceOwnerKind? boundResourceOwnerKind = null)
    {
        Assert.True(definitions.TryResolveExact(coordinate.ResourceKey, out var definition));
        var binding = new ResourceCapacityBinding(
            definition!.CapacityPolicy.Kind,
            definition.CapacityPolicy.FormulaKey ??
            "capacity_test_" + coordinate.ResourceKey,
            FingerprintA);
        var initializedSnapshot = new ResourceStateSnapshot(
            Current: 10m,
            Maximum: 10m,
            binding,
            ResourceLifecycleState.Active);
        var initialize = new ResourceTransition(
            TransitionId: "transition_health_initialize",
            OperationId: "operation_health_initialize",
            EventRef: "turn_1:resource:1",
            OriginKind: "owner_materialization",
            OriginId: "player_current",
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
            AfterState: initializedSnapshot,
            SourceEvidence: new ResourceSourceEvidence(
                "owner_materialization",
                "player_current",
                FingerprintA),
            PolicyFingerprint: FingerprintA,
            ReceiptId: null,
            Turn: 1);
        var transitions = new List<ResourceTransition> { initialize };
        ResourceTransition? latest = null;
        if (current != 10m)
        {
            latest = new ResourceTransition(
                TransitionId: "transition_health_baseline_damage",
                OperationId: "operation_health_baseline_damage",
                EventRef: "turn_1:resource:2",
                OriginKind: "combat_outcome",
                OriginId: "baseline_damage",
                Phase: ResourceMutationPhase.DirectOutcome,
                Priority: 100,
                ExecutionSequence: 1,
                Coordinate: HealthCoordinate,
                Operation: ResourceTransitionOperation.Damage,
                RequestedAmount: 10m - current,
                AppliedAmount: 10m - current,
                Outcome: ResourceTransitionOutcome.Applied,
                CapacityDisposition: null,
                BeforeState: initializedSnapshot,
                AfterState: initializedSnapshot with { Current = current },
                SourceEvidence: new ResourceSourceEvidence(
                    "combat_outcome",
                    "baseline_damage",
                    FingerprintB),
                PolicyFingerprint: FingerprintB,
                ReceiptId: null,
                Turn: 1);
            transitions.Add(latest);
        }
        var historyResult = ResourceHistoryState.CreateValidated(
            transitions,
            definitions);
        Assert.True(
            historyResult.IsValid,
            string.Join(Environment.NewLine, historyResult.Issues));
        var state = new ResourceStateLedger(new[]
        {
            new ResourceStateEntry(
                coordinate,
                current,
                10m,
                binding,
                ResourceLifecycleState.Active,
                new ResourceChronology(
                    CreatedAtTurn: 1,
                    CreatedEventRef: initialize.EventRef,
                    LastTransitionId: (latest ?? initialize).TransitionId,
                    LastEventRef: (latest ?? initialize).EventRef,
                    LastTransitionTurn: 1))
        });
        var owners = ResourceOwnerAuthority.Build(new ResourceOwnerAuthorityInput(
            new[]
            {
                new ResourceOwnerExport(
                    new ResourceOwnerKey(
                        coordinate.Realm,
                        coordinate.OwnerKind,
                        coordinate.ResourceOwnerId),
                    ResourceOwnerLifecycle.Active,
                    SameTurn: false,
                    OwnerRef: null,
                    BoundNpcId: null,
                    new HashSet<string>(StringComparer.Ordinal) { coordinate.ResourceKey },
                    FingerprintA)
            },
            Array.Empty<ResourceOwnerExport>(),
            Array.Empty<ResourceOwnerKey>()));
        Assert.Empty(owners.Issues);
        var targets = EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
            new[]
            {
                new EffectTargetExport(
                    coordinate.Realm,
                    targetKind,
                    targetId,
                    SameTurn: false,
                    BoundNpcId: boundNpcId,
                    BoundResourceOwnerKind: boundResourceOwnerKind)
            },
            Array.Empty<EffectTargetExport>(),
            new HashSet<string>(StringComparer.Ordinal),
            CombatantIdentities: null));
        Assert.Empty(targets.Issues);
        Assert.Empty(historyResult.History!.ValidateStateAgreement(state));
        Assert.Empty(owners.ValidateCanonicalAgreement(state, historyResult.History));
        return new PeriodicBaseline(
            definitions,
            state,
            historyResult.History,
            owners,
            targets,
            coordinate);
    }

    private static ResourceDefinitionCatalog CreateAfterlifeIntegrityDefinitions()
    {
        var proposal = new JsonObject
        {
            ["resourceKey"] = "soul_integrity",
            ["definitionVersion"] = 1,
            ["displayName"] = "Целостность души",
            ["numericKind"] = "integer",
            ["unit"] = "point",
            ["quantum"] = 1,
            ["minimumPolicy"] = new JsonObject
            {
                ["kind"] = "definition_fixed",
                ["value"] = 0
            },
            ["capacityPolicy"] = new JsonObject
            {
                ["kind"] = "instance_fixed"
            },
            ["initializationPolicy"] = new JsonObject
            {
                ["kind"] = "maximum"
            },
            ["allowedOwnerKinds"] = new JsonArray("afterlife_actor"),
            ["allowedOperations"] = new JsonArray("damage", "restore"),
            ["defaultFloorPolicy"] = "clamp_to_minimum",
            ["defaultCapPolicy"] = "clamp_to_maximum",
            ["visibility"] = "owner_visible"
        };
        using var document = JsonDocument.Parse(proposal.ToJsonString());
        var materialized = ResourceDefinitionCatalog.MaterializeProposal(
            document.RootElement,
            ResourceDefinitionCatalog.CreateBuiltIn(),
            createdAtTurn: 1,
            createdEventRef: "turn_1:resource_definition:1",
            static () => new ResourceDefinitionIdentity(
                "resource_definition_soul_integrity",
                "resource_definition_seal_soul_integrity"));
        Assert.True(
            materialized.IsValid,
            string.Join(Environment.NewLine, materialized.Issues));
        return ResourceDefinitionCatalog.CreateBuiltIn().With(materialized.Definition!);
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

    private static ResourceCoordinate HealthCoordinate { get; } = new(
        "mortal_world",
        ResourceOwnerKind.Player,
        "player_current",
        "health");

    private static ResourceOwnerKey PlayerOwner { get; } = new(
        HealthCoordinate.Realm,
        HealthCoordinate.OwnerKind,
        HealthCoordinate.ResourceOwnerId);

    private static ResourceOwnerKey ChargesOwner { get; } = new(
        ChargesCoordinate.Realm,
        ChargesCoordinate.OwnerKind,
        ChargesCoordinate.ResourceOwnerId);

    private static bool RequiresPlayerTargetBinding(string sourceKind) =>
        sourceKind is "action_cost" or
            "combat_outcome" or
            "narrative_outcome" or
            "effect_component";

    private sealed record Baseline(
        ResourceDefinitionCatalog Definitions,
        ResourceStateLedger State,
        ResourceHistoryState History,
        ResourceMutationSourceCatalog Sources);

    private sealed record PeriodicBaseline(
        ResourceDefinitionCatalog Definitions,
        ResourceStateLedger State,
        ResourceHistoryState History,
        ResourceOwnerAuthority Owners,
        EffectTargetAuthority Targets,
        ResourceCoordinate Coordinate);

    private static ResourceMutationSourceCatalog CreateCatalog(
        params ResourceMutationSourceExport[] exports)
    {
        var result = ResourceMutationSourceCatalog.Create(exports);
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        return result.Catalog!;
    }
}
