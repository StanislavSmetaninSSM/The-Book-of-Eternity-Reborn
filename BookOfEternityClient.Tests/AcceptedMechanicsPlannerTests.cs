using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AcceptedMechanicsPlannerTests
{
    private const string FingerprintA =
        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string FingerprintB =
        "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public void WoundLineage_SeverityGenerationReturnsOnlyCurrentRematerializedClosure()
    {
        var fixture = WoundEffectBatchPlannerTests.BuildRetainedWorsenPureFixture();
        var finalization = WoundAcceptedTurnPlanner.Finalize(
            fixture.Prepared,
            new WoundEffectBatchPlanningResult(
                fixture.Accepted,
                Array.Empty<ValidationIssue>()));
        Assert.True(finalization.Success, string.Join(Environment.NewLine,
            finalization.Issues.Select(static issue =>
                $"{issue.Code}: {issue.Message}")));
        var finalPlan = Assert.IsType<WoundAcceptedTurnPlan>(finalization.Plan);
        var wound = Assert.IsType<WoundMaterializationEnvelope>(Assert.Single(
            Assert.Single(finalPlan.CarrierContributions).Mutations).AfterWound);
        using var document = JsonDocument.Parse(
            fixture.Accepted.EffectPlan.IdentityIndexAfterImage.ToJsonString());
        var parsed = EffectIdentityState.Parse(
            document.RootElement,
            EffectIdentityState.StatePath);
        Assert.Empty(parsed.Issues);
        var identities = Assert.IsType<EffectIdentityState>(parsed.State);
        var currentRoots = wound.Consequences.OwnedEffectSources.RootBindings
            .Select(static root => root.EffectId)
            .ToArray();

        var result = WoundEffectLineagePlanner.Plan(
            wound,
            identities,
            currentRoots);

        Assert.True(result.Success, string.Join(Environment.NewLine,
            result.Issues.Select(static issue =>
                $"{issue.Code}: {issue.Message}")));
        Assert.Equal(
            currentRoots.OrderBy(static value => value, StringComparer.Ordinal),
            result.ClosureEffectIds);
        Assert.Equal(3, result.Work.SourceGroupIdentityCount);
        Assert.Equal(2, result.Work.VisitedIdentityCount);
        var prior = Assert.Single(
            Assert.Single(fixture.Prepared.EffectOperationBatches).RootApplications,
            static root => root.PriorRootEffectId is not null).PriorRootEffectId;
        Assert.DoesNotContain(prior, result.ClosureEffectIds);
    }

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

        Assert.True(
            result.IsValid,
            string.Join(
                Environment.NewLine,
                result.Issues.Select(static issue =>
                    $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}; {issue.Message}")));
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
    public void Planner_SameOriginSiblingsReplayIndependentlyOfAllocatedOperationIds()
    {
        var baseline = BaselineCharges();
        var intents = new[]
        {
            Intent(
                "turn_2:registered_sibling:a",
                "registered_system_outcome",
                "system_alpha",
                ResourceOperation.Spend,
                1m),
            Intent(
                "turn_2:registered_sibling:b",
                "registered_system_outcome",
                "system_alpha",
                ResourceOperation.Spend,
                1m),
            Intent(
                "turn_2:registered_sibling:c",
                "registered_system_outcome",
                "system_alpha",
                ResourceOperation.Spend,
                1m)
        };
        var first = AcceptedMechanicsPlanner.BuildResources(
            Input(baseline, intents),
            OperationOrderedIdentityFactory(1, 2, 3));
        Assert.True(first.IsValid, string.Join(Environment.NewLine, first.Issues));
        Assert.Equal(
            intents.Select(static intent => intent.EventRef),
            first.AppliedTransitions.Select(static transition => transition.EventRef));

        var replay = AcceptedMechanicsPlanner.BuildResources(
            Input(
                new Baseline(
                    baseline.Definitions,
                    first.StateAfterImage!,
                    first.HistoryAfterImage!,
                    baseline.Sources),
                intents),
            OperationOrderedIdentityFactory(3, 2, 1));

        Assert.True(replay.IsValid, string.Join(Environment.NewLine, replay.Issues));
        Assert.Empty(replay.AppliedTransitions);
        Assert.Equal(3, replay.ReplayTransitions.Count);
        Assert.Equal(first.StateAfterImage!.Fingerprint, replay.StateAfterImage!.Fingerprint);
        Assert.Equal(first.HistoryAfterImage!.Fingerprint, replay.HistoryAfterImage!.Fingerprint);
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
    public void Planner_SkipsTheWholeUnmetReadyFrontierBeforeExecutingItsRunnableSibling()
    {
        var baseline = BaselineChargesForEffects(
            "effect_a0",
            "effect_a1",
            "effect_a2",
            "effect_b",
            "effect_c");
        var producer = Intent(
            "turn_2:frontier:producer",
            "effect_component",
            "effect_a0",
            ResourceOperation.Spend,
            1m);
        var unmetA = Intent(
            "turn_2:frontier:unmet_a",
            "effect_component",
            "effect_a1",
            ResourceOperation.Spend,
            1m,
            eventRequirements: new[]
            {
                new ResourceMutationEventRequirement(
                    producer.Key,
                    "resource_depleted")
            });
        var runnableB = Intent(
            "turn_2:frontier:runnable_b",
            "effect_component",
            "effect_b",
            ResourceOperation.Spend,
            4m);
        var unmetC = Intent(
            "turn_2:frontier:unmet_c",
            "effect_component",
            "effect_c",
            ResourceOperation.Spend,
            1m,
            eventRequirements: new[]
            {
                new ResourceMutationEventRequirement(
                    producer.Key,
                    "resource_depleted")
            });
        var unlockedD = Intent(
            "turn_2:frontier:unlocked_d",
            "effect_component",
            "effect_a2",
            ResourceOperation.Gain,
            10m,
            dependencies: new[] { unmetC.Key });

        var result = AcceptedMechanicsPlanner.BuildResources(
            Input(
                baseline,
                new[] { runnableB, unlockedD, unmetC, unmetA, producer }),
            IdentityFactory());

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        Assert.Equal(6m, result.StateAfterImage!.Entries.Single().Current);
        Assert.Equal(
            new[] { "effect_a0", "effect_a2", "effect_b" },
            result.AppliedTransitions.Select(static transition => transition.OriginId));
        Assert.Equal(
            new[] { 0, 1, 2 },
            result.AppliedTransitions.Select(static transition =>
                transition.ExecutionSequence));
        Assert.Equal(5, result.Statistics.SchedulingDescriptorVisitCount);
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
    public void BuildResources_ResourceBoundaryPlacesBeforeCurrentBetweenProducerAndContinuation()
    {
        var baseline = BaselineHealth(current: 5m);
        var producerSource = new ResourceMutationSourceExport(
            "combat_outcome",
            "boundary_damage_source",
            FingerprintA,
            ResourceMutationSourceState.Active,
            SameTurn: false,
            PlayerOwner);
        var continuationSource = new ResourceMutationSourceExport(
            "effect_component",
            "boundary_restore_source",
            FingerprintB,
            ResourceMutationSourceState.Active,
            SameTurn: false,
            PlayerOwner);
        var producer = new ResourceMutationIntent(
            "turn_43:boundary:damage",
            HealthCoordinate,
            Amount: 1m,
            new ResourceMutationSourceRequest(
                producerSource.SourceKind,
                producerSource.SourceId,
                ResourceOperation.Damage),
            Array.Empty<ResourceOperationKey>(),
            Array.Empty<ResourceMutationEventRequirement>(),
            ReceiptId: null);
        var continuation = new ResourceMutationIntent(
            "turn_43:boundary:restore",
            HealthCoordinate,
            Amount: 1m,
            new ResourceMutationSourceRequest(
                continuationSource.SourceKind,
                continuationSource.SourceId,
                ResourceOperation.Restore),
            new[] { producer.Key },
            new[]
            {
                new ResourceMutationEventRequirement(
                    producer.Key,
                    "resource_damaged")
            },
            ReceiptId: null);
        var activationIdentity = new EffectActivationCandidateIdentity(
            "effect_boundary_order",
            "trigger_boundary_order",
            "resource_damaged",
            "turn_43:boundary:activation",
            producer.EventRef);
        var reaction = new EffectReactionExecution(
            EventRef: "turn_43:boundary:reaction",
            TriggerEventRef: producer.EventRef,
            CausalEventRef: producer.EventRef,
            Turn: 43,
            EventKind: "resource_damaged",
            Target: new EffectTargetKey(
                "mortal_world",
                "player",
                "player_current"),
            EffectId: activationIdentity.EffectId,
            TriggerId: activationIdentity.TriggerId,
            ComponentId: "component_boundary_reaction",
            ResultKind: "event_outcome",
            Dependency: "before_current_event",
            AfterComponentId: null,
            MaxExpansion: 1,
            DownstreamSource: null,
            Parameters: null);
        var componentMap = new Dictionary<ResourceOperationKey, string>
        {
            [continuation.Key] = "component_boundary_restore"
        };
        var candidate = new EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate(
            new EffectActivationCandidate(
                activationIdentity,
                Priority: 50,
                ConsumesUse: false,
                EffectAuthority: PermanentEffectAuthority(
                    activationIdentity.EffectId)),
            useSeed: null,
            producer.Key,
            new[] { continuation.Key },
            new[] { "component_boundary_restore" },
            componentMap,
            Array.Empty<EffectAcceptedTurnPlanner.EffectBoundedResourceResolution>(),
            new[] { reaction },
            CandidateOrigin(
                new[] { continuation },
                componentMap,
                continuationSource));

        var result = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 43,
                Definitions: baseline.Definitions,
                State: baseline.State,
                History: baseline.History,
                Sources: CreateCatalog(producerSource),
                Mutations: new[] { producer },
                EventMutationResolver: (resourceEvent, actualProducer) =>
                {
                    if (actualProducer != producer.Key ||
                        !string.Equals(
                            resourceEvent.EventKind,
                            "resource_damaged",
                            StringComparison.Ordinal))
                    {
                        return new EffectAcceptedTurnPlanner
                            .EffectPeriodicResourceResolution(
                                Array.Empty<ResourceMutationSourceExport>(),
                                Array.Empty<ResourceMutationIntent>(),
                                Array.Empty<ValidationIssue>());
                    }
                    return new EffectAcceptedTurnPlanner
                        .EffectPeriodicResourceResolution(
                            new[] { continuationSource },
                            new[] { continuation },
                            Array.Empty<ValidationIssue>())
                    {
                        TriggerCandidates = new[] { candidate }
                    };
                }),
            IdentityFactory());

        Assert.True(
            result.IsValid,
            string.Join(
                Environment.NewLine,
                result.Issues.Select(static issue =>
                    $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}; {issue.Message}")));
        var transcript = result.EffectBoundaryTranscript;
        var boundary = Assert.Single(transcript.Boundaries);
        var close = Assert.Single(transcript.BoundaryCloses);
        var release = Assert.Single(transcript.ReleasedReactions);
        var evidence = Assert.Single(transcript.AppliedComponentEvidence);
        Assert.Equal(continuation.Key, evidence.Mutation);
        Assert.True(boundary.ProducerMechanicsOrdinal < boundary.OpenMechanicsOrdinal);
        Assert.True(boundary.OpenMechanicsOrdinal < release.MechanicsOrdinal);
        Assert.True(release.MechanicsOrdinal < evidence.MechanicsOrdinal);
        Assert.True(evidence.MechanicsOrdinal < close.MechanicsOrdinal);
        Assert.Equal(boundary, close.Boundary);
        Assert.True(close.MechanicsOrdinal < transcript.UseProjectionOrdinal);
        Assert.Equal(
            EffectReactionReleaseStage.BeforeCurrentEvent,
            release.Stage);
    }

    [Fact]
    public void BuildResources_AfterCurrentReleasesAtCausalCloseBeforeIndependentReadyNode()
    {
        var baseline = BaselineHealth(current: 10m);
        var producerSource = new ResourceMutationSourceExport(
            "combat_outcome",
            "boundary_close_root",
            FingerprintA,
            ResourceMutationSourceState.Active,
            SameTurn: false,
            PlayerOwner);
        var continuationSource = new ResourceMutationSourceExport(
            "effect_component",
            "zz_boundary_close_continuation",
            FingerprintB,
            ResourceMutationSourceState.Active,
            SameTurn: false,
            PlayerOwner);
        var independentSource = new ResourceMutationSourceExport(
            "effect_component",
            "aa_boundary_close_independent",
            FingerprintA,
            ResourceMutationSourceState.Active,
            SameTurn: false,
            PlayerOwner);
        var producer = new ResourceMutationIntent(
            "turn_43:boundary_close:damage",
            HealthCoordinate,
            Amount: 1m,
            new ResourceMutationSourceRequest(
                producerSource.SourceKind,
                producerSource.SourceId,
                ResourceOperation.Damage),
            Array.Empty<ResourceOperationKey>(),
            Array.Empty<ResourceMutationEventRequirement>(),
            ReceiptId: null);
        var continuation = new ResourceMutationIntent(
            "turn_43:boundary_close:restore",
            HealthCoordinate,
            Amount: 1m,
            new ResourceMutationSourceRequest(
                continuationSource.SourceKind,
                continuationSource.SourceId,
                ResourceOperation.Restore),
            new[] { producer.Key },
            new[]
            {
                new ResourceMutationEventRequirement(
                    producer.Key,
                    "resource_damaged")
            },
            ReceiptId: null);
        var independent = new ResourceMutationIntent(
            "turn_43:boundary_close:independent",
            HealthCoordinate,
            Amount: 1m,
            new ResourceMutationSourceRequest(
                independentSource.SourceKind,
                independentSource.SourceId,
                ResourceOperation.Damage),
            Array.Empty<ResourceOperationKey>(),
            Array.Empty<ResourceMutationEventRequirement>(),
            ReceiptId: null);
        var activationIdentity = new EffectActivationCandidateIdentity(
            "effect_boundary_close",
            "trigger_boundary_close",
            "resource_damaged",
            "turn_43:boundary_close:activation",
            producer.EventRef);
        var reaction = new EffectReactionExecution(
            EventRef: "turn_43:boundary_close:reaction",
            TriggerEventRef: producer.EventRef,
            CausalEventRef: producer.EventRef,
            Turn: 43,
            EventKind: "resource_damaged",
            Target: new EffectTargetKey(
                "mortal_world",
                "player",
                "player_current"),
            EffectId: activationIdentity.EffectId,
            TriggerId: activationIdentity.TriggerId,
            ComponentId: "component_boundary_close_reaction",
            ResultKind: "event_outcome",
            Dependency: "after_current_event",
            AfterComponentId: null,
            MaxExpansion: 1,
            DownstreamSource: null,
            Parameters: null);
        var componentMap = new Dictionary<ResourceOperationKey, string>
        {
            [continuation.Key] = "component_boundary_close_restore"
        };
        var candidate = new EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate(
            new EffectActivationCandidate(
                activationIdentity,
                Priority: 50,
                ConsumesUse: false,
                EffectAuthority: PermanentEffectAuthority(
                    activationIdentity.EffectId)),
            useSeed: null,
            producer.Key,
            new[] { continuation.Key },
            new[] { "component_boundary_close_restore" },
            componentMap,
            Array.Empty<EffectAcceptedTurnPlanner.EffectBoundedResourceResolution>(),
            new[] { reaction },
            CandidateOrigin(
                new[] { continuation },
                componentMap,
                continuationSource));

        var result = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 43,
                Definitions: baseline.Definitions,
                State: baseline.State,
                History: baseline.History,
                Sources: CreateCatalog(producerSource, independentSource),
                Mutations: new[] { producer, independent },
                EventMutationResolver: (resourceEvent, actualProducer) =>
                {
                    if (actualProducer != producer.Key ||
                        !string.Equals(
                            resourceEvent.EventKind,
                            "resource_damaged",
                            StringComparison.Ordinal))
                    {
                        return new EffectAcceptedTurnPlanner
                            .EffectPeriodicResourceResolution(
                                Array.Empty<ResourceMutationSourceExport>(),
                                Array.Empty<ResourceMutationIntent>(),
                                Array.Empty<ValidationIssue>());
                    }
                    return new EffectAcceptedTurnPlanner
                        .EffectPeriodicResourceResolution(
                            new[] { continuationSource },
                            new[] { continuation },
                            Array.Empty<ValidationIssue>())
                    {
                        TriggerCandidates = new[] { candidate }
                    };
                }),
            IdentityFactory());

        Assert.True(
            result.IsValid,
            string.Join(
                Environment.NewLine,
                result.Issues.Select(static issue =>
                    $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}; {issue.Message}")));
        var transcript = result.EffectBoundaryTranscript;
        var close = Assert.Single(transcript.BoundaryCloses);
        var release = Assert.Single(transcript.ReleasedReactions);
        var applied = Assert.Single(transcript.AppliedComponentEvidence);
        var independentMutation = Assert.Single(
            transcript.ResourceMutations,
            evidence => evidence.Mutation == independent.Key);
        Assert.True(applied.MechanicsOrdinal < release.MechanicsOrdinal);
        Assert.True(release.MechanicsOrdinal < close.MechanicsOrdinal);
        Assert.True(close.MechanicsOrdinal < independentMutation.MechanicsOrdinal);
        Assert.True(
            independentMutation.MechanicsOrdinal < transcript.UseProjectionOrdinal);
        Assert.Equal(
            EffectReactionReleaseStage.AfterCurrentEvent,
            release.Stage);
    }

    [Fact]
    public void BuildResources_AfterCurrentTerminalReservesEffectBeforeLaterRootBoundary()
    {
        var baseline = BaselineChargesForEffects("terminal_reservation_source");
        var mutation = Intent(
            "turn_2:terminal_reservation:spend",
            "effect_component",
            "terminal_reservation_source",
            ResourceOperation.Spend,
            amount: 1m);
        var firstIdentity = new EffectActivationCandidateIdentity(
            "effect_terminal_reservation",
            "trigger_terminal_reservation_first",
            "owner_turn_end",
            "turn_2:terminal_reservation:a:activation",
            "turn_2:terminal_reservation:a:boundary");
        var firstMap = new Dictionary<ResourceOperationKey, string>
        {
            [mutation.Key] = "component_terminal_reservation_spend"
        };
        var terminalReaction = new EffectReactionExecution(
            EventRef: "turn_2:terminal_reservation:remove",
            TriggerEventRef: firstIdentity.TriggerEventRef,
            CausalEventRef: firstIdentity.TriggerEventRef,
            Turn: 2,
            EventKind: firstIdentity.EventKind,
            Target: new EffectTargetKey(
                "mortal_world",
                "player",
                "player_current"),
            EffectId: firstIdentity.EffectId,
            TriggerId: firstIdentity.TriggerId,
            ComponentId: "component_terminal_reservation_remove",
            ResultKind: "remove",
            Dependency: "after_current_event",
            AfterComponentId: null,
            MaxExpansion: 1,
            DownstreamSource: null,
            Parameters: null);
        var firstCandidate = new EffectAcceptedTurnPlanner
            .EffectResourceTriggerCandidate(
                new EffectActivationCandidate(
                    firstIdentity,
                    Priority: 100,
                    ConsumesUse: false,
                    EffectAuthority: PermanentEffectAuthority(
                        firstIdentity.EffectId)),
                useSeed: null,
                producer: null,
                plannedMutationKeys: new[] { mutation.Key },
                plannedComponentIds: new[]
                {
                    "component_terminal_reservation_spend"
                },
                plannedComponentIdsByMutation: firstMap,
                pendingOutputs: Array.Empty<EffectAcceptedTurnPlanner
                    .EffectBoundedResourceResolution>(),
                reactionOutputs: new[] { terminalReaction },
                origin: CandidateOrigin(
                    new[] { mutation },
                    firstMap,
                    baseline.Sources.Exports.Single()));
        var laterIdentity = new EffectActivationCandidateIdentity(
            firstIdentity.EffectId,
            "trigger_terminal_reservation_later",
            "owner_turn_end",
            "turn_2:terminal_reservation:b:activation",
            "turn_2:terminal_reservation:b:boundary");
        var laterCandidate = new EffectAcceptedTurnPlanner
            .EffectResourceTriggerCandidate(
                new EffectActivationCandidate(
                    laterIdentity,
                    Priority: 100,
                    ConsumesUse: false,
                    EffectAuthority: PermanentEffectAuthority(
                        laterIdentity.EffectId)),
                useSeed: null,
                producer: null,
                plannedMutationKeys: Array.Empty<ResourceOperationKey>(),
                plannedComponentIds: Array.Empty<string>(),
                plannedComponentIdsByMutation:
                    new Dictionary<ResourceOperationKey, string>(),
                pendingOutputs: Array.Empty<EffectAcceptedTurnPlanner
                    .EffectBoundedResourceResolution>(),
                reactionOutputs: Array.Empty<EffectReactionExecution>(),
                origin: EffectAcceptedTurnPlanner
                    .EffectResourceCandidateOrigin.Empty);

        var result = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 2,
                Definitions: baseline.Definitions,
                State: baseline.State,
                History: baseline.History,
                Sources: baseline.Sources,
                Mutations: new[] { mutation },
                InitialTriggerCandidates: new[]
                {
                    firstCandidate,
                    laterCandidate
                }),
            IdentityFactory());

        Assert.True(
            result.IsValid,
            string.Join(
                Environment.NewLine,
                result.Issues.Select(static issue =>
                    $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
        var transcript = result.EffectBoundaryTranscript;
        var accepted = Assert.Single(transcript.AcceptedActivations);
        Assert.Equal(firstIdentity, accepted.Activation.Stamp.Identity);
        var rejected = Assert.Single(transcript.RejectedActivations);
        Assert.Equal(laterIdentity, rejected.Candidate.Activation.Identity);
        Assert.Equal(
            EffectActivationRejectionReason.EffectTerminal,
            rejected.Reason);
        var reservation = Assert.Single(
            transcript.TerminalAvailabilityReservations);
        Assert.Equal(
            EffectTerminalAvailabilityReservationKind.AfterCurrentReaction,
            reservation.Kind);
        Assert.Equal(firstIdentity, reservation.Activation.Identity);
        var released = Assert.Single(transcript.ReleasedReactions);
        Assert.Equal(
            EffectReactionReleaseStage.AfterCurrentEvent,
            released.Stage);
        Assert.Equal(terminalReaction, released.Reaction);
    }

    [Fact]
    public void BuildResources_IndependentRootClosesBeforePendingSiblingWithReplayStablePrefix()
    {
        var baseline = BaselineChargesForEffects("forest_root_source");
        var rootMutation = Intent(
            "turn_2:forest:a:spend",
            "effect_component",
            "forest_root_source",
            ResourceOperation.Spend,
            amount: 1m);
        var rootIdentity = new EffectActivationCandidateIdentity(
            "effect_forest_root",
            "trigger_forest_root",
            "owner_turn_end",
            "turn_2:forest:a:activation",
            "turn_2:forest:a:boundary");
        var rootComponentMap = new Dictionary<ResourceOperationKey, string>
        {
            [rootMutation.Key] = "component_forest_root"
        };
        var rootCandidate = new EffectAcceptedTurnPlanner
            .EffectResourceTriggerCandidate(
                new EffectActivationCandidate(
                    rootIdentity,
                    Priority: 100,
                    ConsumesUse: false,
                    EffectAuthority: PermanentEffectAuthority(
                        rootIdentity.EffectId)),
                useSeed: null,
                producer: null,
                plannedMutationKeys: new[] { rootMutation.Key },
                plannedComponentIds: new[] { "component_forest_root" },
                plannedComponentIdsByMutation: rootComponentMap,
                pendingOutputs: Array.Empty<EffectAcceptedTurnPlanner
                    .EffectBoundedResourceResolution>(),
                reactionOutputs: Array.Empty<EffectReactionExecution>(),
                origin: CandidateOrigin(
                    new[] { rootMutation },
                    rootComponentMap,
                    baseline.Sources.Exports.Single()));
        var pendingIdentity = new EffectActivationCandidateIdentity(
            "effect_forest_pending",
            "trigger_forest_pending",
            "owner_turn_end",
            "turn_2:forest:b:activation",
            "turn_2:forest:b:boundary");
        var pendingCandidate = new EffectAcceptedTurnPlanner
            .EffectResourceTriggerCandidate(
                new EffectActivationCandidate(
                    pendingIdentity,
                    Priority: 100,
                    ConsumesUse: false,
                    EffectAuthority: PermanentEffectAuthority(
                        pendingIdentity.EffectId)),
                useSeed: null,
                producer: null,
                plannedMutationKeys: Array.Empty<ResourceOperationKey>(),
                plannedComponentIds: Array.Empty<string>(),
                plannedComponentIdsByMutation:
                    new Dictionary<ResourceOperationKey, string>(),
                pendingOutputs: new[]
                {
                    CreateFingerprintPendingOutput(
                        pendingIdentity,
                        ChargesCoordinate)
                },
                reactionOutputs: Array.Empty<EffectReactionExecution>(),
                origin: EffectAcceptedTurnPlanner
                    .EffectResourceCandidateOrigin.Empty);
        var input = new AcceptedMechanicsResourceInput(
            Turn: 2,
            Definitions: baseline.Definitions,
            State: baseline.State,
            History: baseline.History,
            Sources: baseline.Sources,
            Mutations: new[] { rootMutation },
            InitialTriggerCandidates: new[]
            {
                rootCandidate,
                pendingCandidate
            });

        var first = AcceptedMechanicsPlanner.BuildResources(
            input,
            IdentityFactory(seed: 1));
        var replay = AcceptedMechanicsPlanner.BuildResources(
            input,
            IdentityFactory(seed: 101));

        Assert.True(
            first.IsValid,
            string.Join(
                Environment.NewLine,
                first.Issues.Select(static issue =>
                    $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
        Assert.True(
            replay.IsValid,
            string.Join(
                Environment.NewLine,
                replay.Issues.Select(static issue =>
                    $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
        Assert.All(
            first.EffectBoundaryTranscript.Boundaries,
            static boundary => Assert.Null(boundary.ParentBoundaryOrdinal));
        Assert.Equal(
            new long[] { 0 },
            first.EffectBoundaryTranscript.BoundaryCloses
                .Select(static close => close.Boundary.BoundaryOrdinal));
        Assert.Equal(
            1,
            first.EffectBoundaryTranscript.PendingFrontierBoundaryOrdinal);
        Assert.Equal(
            Assert.Single(first.AcceptedPendingResolutions)
                .CausalAuthority.TranscriptPrefixFingerprint,
            Assert.Single(replay.AcceptedPendingResolutions)
                .CausalAuthority.TranscriptPrefixFingerprint);
    }

    [Fact]
    public void BuildResources_ClosedRootBeforePendingRootHasReplayStablePrefix()
    {
        var baseline = BaselineHealth(current: 10m);
        var firstProducerSource = new ResourceMutationSourceExport(
            "combat_outcome",
            "aa_forest_first_producer",
            FingerprintA,
            ResourceMutationSourceState.Active,
            SameTurn: false,
            PlayerOwner);
        var continuationSource = new ResourceMutationSourceExport(
            "effect_component",
            "forest_first_continuation",
            FingerprintB,
            ResourceMutationSourceState.Active,
            SameTurn: false,
            PlayerOwner);
        var pendingProducerSource = new ResourceMutationSourceExport(
            "combat_outcome",
            "zz_forest_pending_producer",
            FingerprintA,
            ResourceMutationSourceState.Active,
            SameTurn: false,
            PlayerOwner);
        var firstProducer = new ResourceMutationIntent(
            "turn_43:forest:first:damage",
            HealthCoordinate,
            Amount: 1m,
            new ResourceMutationSourceRequest(
                firstProducerSource.SourceKind,
                firstProducerSource.SourceId,
                ResourceOperation.Damage),
            Array.Empty<ResourceOperationKey>(),
            Array.Empty<ResourceMutationEventRequirement>(),
            ReceiptId: null);
        var continuation = new ResourceMutationIntent(
            "turn_43:forest:first:restore",
            HealthCoordinate,
            Amount: 1m,
            new ResourceMutationSourceRequest(
                continuationSource.SourceKind,
                continuationSource.SourceId,
                ResourceOperation.Restore),
            new[] { firstProducer.Key },
            new[]
            {
                new ResourceMutationEventRequirement(
                    firstProducer.Key,
                    "resource_damaged")
            },
            ReceiptId: null);
        var pendingProducer = new ResourceMutationIntent(
            "turn_43:forest:pending:damage",
            HealthCoordinate,
            Amount: 1m,
            new ResourceMutationSourceRequest(
                pendingProducerSource.SourceKind,
                pendingProducerSource.SourceId,
                ResourceOperation.Damage),
            Array.Empty<ResourceOperationKey>(),
            Array.Empty<ResourceMutationEventRequirement>(),
            ReceiptId: null);
        var firstIdentity = new EffectActivationCandidateIdentity(
            "effect_forest_first",
            "trigger_forest_first",
            "resource_damaged",
            "turn_43:forest:first:activation",
            firstProducer.EventRef);
        var firstComponentMap = new Dictionary<ResourceOperationKey, string>
        {
            [continuation.Key] = "component_forest_first"
        };
        var firstCandidate = new EffectAcceptedTurnPlanner
            .EffectResourceTriggerCandidate(
                new EffectActivationCandidate(
                    firstIdentity,
                    Priority: 100,
                    ConsumesUse: false,
                    EffectAuthority: PermanentEffectAuthority(
                        firstIdentity.EffectId)),
                useSeed: null,
                firstProducer.Key,
                new[] { continuation.Key },
                new[] { "component_forest_first" },
                firstComponentMap,
                Array.Empty<EffectAcceptedTurnPlanner
                    .EffectBoundedResourceResolution>(),
                Array.Empty<EffectReactionExecution>(),
                CandidateOrigin(
                    new[] { continuation },
                    firstComponentMap,
                    continuationSource));
        var pendingIdentity = new EffectActivationCandidateIdentity(
            "effect_forest_late_pending",
            "trigger_forest_late_pending",
            "resource_damaged",
            "turn_43:forest:pending:activation",
            pendingProducer.EventRef);
        var pendingCandidate = new EffectAcceptedTurnPlanner
            .EffectResourceTriggerCandidate(
                new EffectActivationCandidate(
                    pendingIdentity,
                    Priority: 100,
                    ConsumesUse: false,
                    EffectAuthority: PermanentEffectAuthority(
                        pendingIdentity.EffectId)),
                useSeed: null,
                pendingProducer.Key,
                Array.Empty<ResourceOperationKey>(),
                Array.Empty<string>(),
                new Dictionary<ResourceOperationKey, string>(),
                new[]
                {
                    CreateFingerprintPendingOutput(
                        pendingIdentity,
                        HealthCoordinate) with
                    {
                        Operation = ResourceOperation.Damage
                    }
                },
                Array.Empty<EffectReactionExecution>(),
                EffectAcceptedTurnPlanner.EffectResourceCandidateOrigin.Empty);
        var input = new AcceptedMechanicsResourceInput(
            Turn: 43,
            Definitions: baseline.Definitions,
            State: baseline.State,
            History: baseline.History,
            Sources: CreateCatalog(
                firstProducerSource,
                pendingProducerSource),
            Mutations: new[] { firstProducer, pendingProducer },
            EventMutationResolver: (resourceEvent, actualProducer) =>
            {
                if (!string.Equals(
                        resourceEvent.EventKind,
                        "resource_damaged",
                        StringComparison.Ordinal))
                {
                    return new EffectAcceptedTurnPlanner
                        .EffectPeriodicResourceResolution(
                            Array.Empty<ResourceMutationSourceExport>(),
                            Array.Empty<ResourceMutationIntent>(),
                            Array.Empty<ValidationIssue>());
                }
                if (actualProducer == firstProducer.Key)
                {
                    return new EffectAcceptedTurnPlanner
                        .EffectPeriodicResourceResolution(
                            new[] { continuationSource },
                            new[] { continuation },
                            Array.Empty<ValidationIssue>())
                    {
                        TriggerCandidates = new[] { firstCandidate }
                    };
                }
                return new EffectAcceptedTurnPlanner
                    .EffectPeriodicResourceResolution(
                        Array.Empty<ResourceMutationSourceExport>(),
                        Array.Empty<ResourceMutationIntent>(),
                        Array.Empty<ValidationIssue>())
                {
                    TriggerCandidates = actualProducer == pendingProducer.Key
                        ? new[] { pendingCandidate }
                        : Array.Empty<EffectAcceptedTurnPlanner
                            .EffectResourceTriggerCandidate>()
                };
            });

        var first = AcceptedMechanicsPlanner.BuildResources(
            input,
            IdentityFactory(seed: 1));
        var replay = AcceptedMechanicsPlanner.BuildResources(
            input,
            IdentityFactory(seed: 101));

        Assert.True(
            first.IsValid,
            string.Join(
                Environment.NewLine,
                first.Issues.Select(static issue =>
                    $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
        Assert.True(
            replay.IsValid,
            string.Join(
                Environment.NewLine,
                replay.Issues.Select(static issue =>
                    $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
        Assert.Equal(
            new long[] { 0 },
            first.EffectBoundaryTranscript.BoundaryCloses
                .Select(static close => close.Boundary.BoundaryOrdinal));
        Assert.Equal(
            1,
            first.EffectBoundaryTranscript.PendingFrontierBoundaryOrdinal);
        Assert.All(
            first.EffectBoundaryTranscript.Boundaries,
            static boundary => Assert.Null(boundary.ParentBoundaryOrdinal));
        Assert.Equal(
            Assert.Single(first.AcceptedPendingResolutions)
                .CausalAuthority.TranscriptPrefixFingerprint,
            Assert.Single(replay.AcceptedPendingResolutions)
                .CausalAuthority.TranscriptPrefixFingerprint);
    }

    [Fact]
    public void BuildResources_FailureDoesNotInventAnEmptyBoundaryTranscript()
    {
        var baseline = BaselineCharges();
        var duplicate = Intent(
            "turn_2:boundary:duplicate",
            "action_cost",
            "action_alpha",
            ResourceOperation.Spend,
            amount: 1m);

        var result = AcceptedMechanicsPlanner.BuildResources(
            Input(baseline, new[] { duplicate, duplicate }),
            IdentityFactory());

        Assert.False(result.IsValid);
        Assert.Null(result.EffectBoundaryTranscript);
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
        var candidateComponentMap = new Dictionary<ResourceOperationKey, string>
        {
            [applied.Key] = "component_applied",
            [skipped.Key] = "component_skipped"
        };
        var candidate = new EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate(
            new EffectActivationCandidate(
                new EffectActivationCandidateIdentity(
                    EffectMaterializationTestFixture.EffectId,
                    "on_owner_turn_end",
                    "owner_turn_end",
                    "turn_43:effect:trigger",
                    "turn_43:lifecycle:owner_turn_end"),
                Priority: 0,
                ConsumesUse: false,
                EffectAuthority: PermanentEffectAuthority(
                    EffectMaterializationTestFixture.EffectId)),
            useSeed: null,
            producer: null,
            new[] { applied.Key, skipped.Key },
            new[] { "component_applied", "component_skipped" },
            candidateComponentMap,
            Array.Empty<EffectAcceptedTurnPlanner.EffectBoundedResourceResolution>(),
            Array.Empty<EffectReactionExecution>(),
            CandidateOrigin(
                new[] { applied, skipped },
                candidateComponentMap,
                appliedSource,
                skippedSource));

        var result = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 43,
                Definitions: baseline.Definitions,
                State: baseline.State,
                History: baseline.History,
                Sources: CreateCatalog(appliedSource, skippedSource, producerSource),
                Mutations: new[] { skipped, applied, producer },
                InitialTriggerCandidates: new[] { candidate }),
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
            var componentMap = new Dictionary<ResourceOperationKey, string>
            {
                [mutation.Key] = "component_cycle"
            };
            return new EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution(
                new[] { source },
                new[] { mutation },
                Array.Empty<ValidationIssue>())
            {
                TriggerCandidates = new[]
                {
                    new EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate(
                        new EffectActivationCandidate(
                            new EffectActivationCandidateIdentity(
                                effectId,
                                triggerId,
                                resourceEvent.EventKind,
                                eventRef,
                                resourceEvent.EventRef),
                            Priority: 0,
                            ConsumesUse: false,
                            EffectAuthority: PermanentEffectAuthority(effectId)),
                        useSeed: null,
                        producer,
                        new[] { mutation.Key },
                        new[] { "component_cycle" },
                        componentMap,
                        Array.Empty<EffectAcceptedTurnPlanner.EffectBoundedResourceResolution>(),
                        Array.Empty<EffectReactionExecution>(),
                        CandidateOrigin(
                            new[] { mutation },
                            componentMap,
                            source))
                }
            };
        }
    }

    [Fact]
    public void BuildResources_RejectsSixtyFifthReleasedPureReactionAcrossAcceptedTurn()
    {
        var baseline = BaselineCharges();
        var candidates = Enumerable.Range(
                1,
                EffectReactionContract.MaximumExpansion + 1)
            .Select(index => CreatePureReactionCandidate(
                ordinal: index,
                effectId: $"effect_global_expansion_{index}",
                triggerId: $"trigger_global_expansion_{index}",
                componentId: $"component_global_expansion_{index}",
                maxExpansion: 1,
                producer: null,
                eventKind: "owner_turn_end",
                triggerEventRef: "turn_2:global_expansion:lifecycle"))
            .ToArray();

        var result = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 2,
                Definitions: baseline.Definitions,
                State: baseline.State,
                History: baseline.History,
                Sources: baseline.Sources,
                Mutations: Array.Empty<ResourceMutationIntent>(),
                InitialTriggerCandidates: candidates),
            IdentityFactory());

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "effect_reaction_expansion_exceeded");
        Assert.Null(result.StateAfterImage);
        Assert.Null(result.HistoryAfterImage);
    }

    [Fact]
    public void BuildResources_AccumulatesPerComponentExpansionAcrossLifecycleAndResourceBoundaries()
    {
        var rootSource = new ResourceMutationSourceExport(
            "narrative_outcome",
            "component_expansion_root",
            FingerprintA,
            ResourceMutationSourceState.Active,
            SameTurn: false,
            ChargesOwner);
        var baseline = BaselineCharges(rootSource);
        var root = new ResourceMutationIntent(
            "turn_2:component_expansion:root",
            ChargesCoordinate,
            Amount: 1m,
            new ResourceMutationSourceRequest(
                rootSource.SourceKind,
                rootSource.SourceId,
                ResourceOperation.Spend),
            Array.Empty<ResourceOperationKey>(),
            Array.Empty<ResourceMutationEventRequirement>(),
            ReceiptId: null);
        const string effectId = "effect_component_expansion";
        const string triggerId = "trigger_component_expansion";
        const string componentId = "component_expansion_shared";
        var lifecycleCandidate = CreatePureReactionCandidate(
            ordinal: 1,
            effectId,
            triggerId,
            componentId,
            maxExpansion: 1,
            producer: null,
            eventKind: "owner_turn_end",
            triggerEventRef: "turn_2:component_expansion:lifecycle");

        var result = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 2,
                Definitions: baseline.Definitions,
                State: baseline.State,
                History: baseline.History,
                Sources: baseline.Sources,
                Mutations: new[] { root },
                EventMutationResolver: ResolveResourceEvent,
                InitialTriggerCandidates: new[] { lifecycleCandidate }),
            IdentityFactory());

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "effect_reaction_expansion_exceeded");
        Assert.Null(result.StateAfterImage);
        Assert.Null(result.HistoryAfterImage);

        EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution
            ResolveResourceEvent(
                ResourceAppliedEvent resourceEvent,
                ResourceOperationKey producer)
        {
            if (!string.Equals(
                    resourceEvent.EventKind,
                    "resource_spent",
                    StringComparison.Ordinal))
            {
                return new EffectAcceptedTurnPlanner
                    .EffectPeriodicResourceResolution(
                        Array.Empty<ResourceMutationSourceExport>(),
                        Array.Empty<ResourceMutationIntent>(),
                        Array.Empty<ValidationIssue>());
            }

            return new EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution(
                Array.Empty<ResourceMutationSourceExport>(),
                Array.Empty<ResourceMutationIntent>(),
                Array.Empty<ValidationIssue>())
            {
                TriggerCandidates = new[]
                {
                    CreatePureReactionCandidate(
                        ordinal: 2,
                        effectId,
                        triggerId,
                        componentId,
                        maxExpansion: 1,
                        producer,
                        resourceEvent.EventKind,
                        resourceEvent.EventRef)
                }
            };
        }
    }

    [Fact]
    public void PendingCausalProducerAuthority_DistinguishesCoordinateOwnerKind()
    {
        var player = ResolveProducerFingerprints(ResourceOwnerKind.Player);
        var npc = ResolveProducerFingerprints(ResourceOwnerKind.Npc);

        Assert.False(string.IsNullOrWhiteSpace(player.ProducerOperationKey));
        Assert.False(string.IsNullOrWhiteSpace(npc.ProducerOperationKey));
        Assert.NotEqual(player.ProducerOperationKey, npc.ProducerOperationKey);
        Assert.NotEqual(player.CandidateFingerprint, npc.CandidateFingerprint);
    }

    [Fact]
    public void StableProducerOperationKey_DistinguishesDelimiterCollisionInCoordinateIds()
    {
        var (ownerDelimited, resourceDelimited) =
            CreateDelimiterCollisionOperationKeys();

        var ownerDelimitedFingerprint =
            AcceptedMechanicsPlanner.CreateStableProducerOperationKey(
                ownerDelimited);
        var resourceDelimitedFingerprint =
            AcceptedMechanicsPlanner.CreateStableProducerOperationKey(
                resourceDelimited);

        Assert.NotEqual(
            ownerDelimitedFingerprint,
            resourceDelimitedFingerprint);
    }

    [Fact]
    public void PendingEffectReplayIdentity_IsStableAcrossAcceptedApplicationIds()
    {
        var authority = new ResourcePendingAuthorityBinding(
            "accepted_application",
            "turn_2:effect_application:stable");

        var first = EffectAcceptedTurnPlanner.CreatePendingEffectReplayIdentity(
            "effect_random_a",
            authority);
        var second = EffectAcceptedTurnPlanner.CreatePendingEffectReplayIdentity(
            "effect_random_b",
            authority);

        Assert.Equal(first, second);
    }

    [Fact]
    public void PendingEffectReplayIdentity_DistinguishesTypedAuthorityFromPermanentLookalike()
    {
        const string lookalike =
            "accepted_application:turn_2:effect_application:stable";
        var accepted = EffectAcceptedTurnPlanner.CreatePendingEffectReplayIdentity(
            "effect_random",
            new ResourcePendingAuthorityBinding(
                "accepted_application",
                "turn_2:effect_application:stable"));
        var permanent = EffectAcceptedTurnPlanner.CreatePendingEffectReplayIdentity(
            lookalike,
            new ResourcePendingAuthorityBinding("permanent", lookalike));

        Assert.NotEqual(accepted, permanent);
    }

    [Theory]
    [InlineData("same_turn_ref", "turn_2:effect_application:stable")]
    [InlineData("permanent", "effect_other")]
    public void PendingEffectReplayIdentity_RejectsInvalidEffectAuthority(
        string bindingKind,
        string authorityId)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            EffectAcceptedTurnPlanner.CreatePendingEffectReplayIdentity(
                "effect_current",
                new ResourcePendingAuthorityBinding(bindingKind, authorityId)));

        Assert.Contains(
            "effect authority",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CandidateFingerprint_RebindsForeignAcceptedReplacementTarget()
    {
        var targetAuthority = new ResourcePendingAuthorityBinding(
            "accepted_application",
            "turn_2:foreign_target_application");
        var firstReaction = CreateFingerprintReaction() with
        {
            ReplacementTarget = new EffectReplayIdentity(
                "effect_foreign_target_random_a",
                targetAuthority)
        };
        var resubmittedReaction = firstReaction with
        {
            ReplacementTarget = new EffectReplayIdentity(
                "effect_foreign_target_random_b",
                targetAuthority)
        };

        var first = CreateFingerprintCandidate(
            Array.Empty<ResourceMutationIntent>(),
            new Dictionary<ResourceOperationKey, string>(),
            Array.Empty<ResourceMutationSourceExport>(),
            firstReaction);
        var resubmitted = CreateFingerprintCandidate(
            Array.Empty<ResourceMutationIntent>(),
            new Dictionary<ResourceOperationKey, string>(),
            Array.Empty<ResourceMutationSourceExport>(),
            resubmittedReaction);

        Assert.Equal(
            AcceptedMechanicsPlanner.CreateCandidateFingerprint(first),
            AcceptedMechanicsPlanner.CreateCandidateFingerprint(resubmitted));
    }

    [Fact]
    public void ReactionFingerprint_RebindsOnlyExactAcceptedReplacementAuthority()
    {
        var ownerAuthority = new ResourcePendingAuthorityBinding(
            "permanent",
            "effect_fingerprint_owner");
        var targetAuthority = new ResourcePendingAuthorityBinding(
            "accepted_application",
            "turn_2:foreign_target_application");
        var first = CreateFingerprintReaction() with
        {
            EffectId = ownerAuthority.AuthorityId,
            ReplacementTarget = new EffectReplayIdentity(
                "effect_foreign_target_random_a",
                targetAuthority)
        };
        var rebound = first with
        {
            ReplacementTarget = new EffectReplayIdentity(
                "effect_foreign_target_random_b",
                targetAuthority)
        };
        var drifted = rebound with
        {
            ReplacementTarget = new EffectReplayIdentity(
                "effect_foreign_target_random_b",
                new ResourcePendingAuthorityBinding(
                    "accepted_application",
                    "turn_2:foreign_target_application_drifted"))
        };

        Assert.NotEqual(
            AcceptedMechanicsPlanner
                .CreateReactionCandidateOutputFingerprint(first),
            AcceptedMechanicsPlanner
                .CreateReactionCandidateOutputFingerprint(rebound));
        Assert.Equal(
            AcceptedMechanicsPlanner
                .CreateReplayStableReactionCandidateOutputFingerprint(
                    first,
                    first.EffectId,
                    ownerAuthority),
            AcceptedMechanicsPlanner
                .CreateReplayStableReactionCandidateOutputFingerprint(
                    rebound,
                    rebound.EffectId,
                    ownerAuthority));
        Assert.NotEqual(
            AcceptedMechanicsPlanner
                .CreateReplayStableReactionCandidateOutputFingerprint(
                    rebound,
                    rebound.EffectId,
                    ownerAuthority),
            AcceptedMechanicsPlanner
                .CreateReplayStableReactionCandidateOutputFingerprint(
                    drifted,
                    drifted.EffectId,
                    ownerAuthority));
    }

    [Fact]
    public void EffectReplayIdentity_RejectsPermanentAuthorityMismatch()
    {
        Assert.Throws<ArgumentException>(() => new EffectReplayIdentity(
            "effect_foreign_target_a",
            new ResourcePendingAuthorityBinding(
                "permanent",
                "effect_foreign_target_b")));
    }

    [Fact]
    public void CandidateFingerprint_DistinguishesMutationKeyToComponentAssociation()
    {
        var baseline = BaselineChargesForEffects(
            "fingerprint_effect_alpha",
            "fingerprint_effect_beta");
        var alpha = Intent(
            "turn_2:fingerprint:alpha",
            "effect_component",
            "fingerprint_effect_alpha",
            ResourceOperation.Spend,
            1m);
        var beta = Intent(
            "turn_2:fingerprint:beta",
            "effect_component",
            "fingerprint_effect_beta",
            ResourceOperation.Spend,
            1m);
        var fingerprintMutations = new[] { alpha, beta };
        var referencedSources = ReferencedCandidateSources(
            baseline.Sources,
            fingerprintMutations);
        var direct = CreateFingerprintCandidate(
            fingerprintMutations,
            new Dictionary<ResourceOperationKey, string>
            {
                [alpha.Key] = "component_alpha",
                [beta.Key] = "component_beta"
            },
            referencedSources);
        var swapped = CreateFingerprintCandidate(
            fingerprintMutations,
            new Dictionary<ResourceOperationKey, string>
            {
                [alpha.Key] = "component_beta",
                [beta.Key] = "component_alpha"
            },
            referencedSources);

        var directFingerprint = ResolveCandidateFingerprint(
            baseline,
            new[] { alpha, beta },
            direct);
        var swappedFingerprint = ResolveCandidateFingerprint(
            baseline,
            new[] { alpha, beta },
            swapped);

        Assert.NotEqual(directFingerprint, swappedFingerprint);
    }

    [Fact]
    public void CandidateFingerprint_DistinguishesDelimiterCollisionInPlannedKeyAndComponentMap()
    {
        var (ownerDelimited, resourceDelimited) =
            CreateDelimiterCollisionOperationKeys();
        var ownerDelimitedCandidate =
            CreateDelimiterFingerprintCandidate(ownerDelimited);
        var resourceDelimitedCandidate =
            CreateDelimiterFingerprintCandidate(resourceDelimited);

        var ownerDelimitedFingerprint =
            AcceptedMechanicsPlanner.CreateCandidateFingerprint(
                ownerDelimitedCandidate);
        var resourceDelimitedFingerprint =
            AcceptedMechanicsPlanner.CreateCandidateFingerprint(
                resourceDelimitedCandidate);

        Assert.NotEqual(
            ownerDelimitedFingerprint,
            resourceDelimitedFingerprint);
    }

    [Theory]
    [InlineData("dependency")]
    [InlineData("trigger_ref")]
    [InlineData("causal_ref")]
    [InlineData("turn")]
    [InlineData("event_kind")]
    [InlineData("trigger_id")]
    [InlineData("component_priority")]
    [InlineData("target")]
    [InlineData("after_component")]
    [InlineData("downstream_source")]
    [InlineData("parameters")]
    [InlineData("max_expansion")]
    [InlineData("replacement_target")]
    public void CandidateFingerprint_DistinguishesFullReactionOutputSemantics(
        string changedField)
    {
        var canonical = CreateFingerprintReaction();
        var changed = ChangeFingerprintReaction(canonical, changedField);
        var canonicalCandidate = CreateFingerprintCandidate(
            Array.Empty<ResourceMutationIntent>(),
            new Dictionary<ResourceOperationKey, string>(),
            Array.Empty<ResourceMutationSourceExport>(),
            canonical);
        var changedCandidate = CreateFingerprintCandidate(
            Array.Empty<ResourceMutationIntent>(),
            new Dictionary<ResourceOperationKey, string>(),
            Array.Empty<ResourceMutationSourceExport>(),
            changed);

        var canonicalFingerprint =
            AcceptedMechanicsPlanner.CreateCandidateFingerprint(
                canonicalCandidate);
        var changedFingerprint =
            AcceptedMechanicsPlanner.CreateCandidateFingerprint(
                changedCandidate);

        Assert.NotEqual(canonicalFingerprint, changedFingerprint);
    }

    [Fact]
    public void CandidateAuthority_RejectsReactionOwnedByDifferentEffect()
    {
        var changed = ChangeFingerprintReaction(
            CreateFingerprintReaction(),
            "effect_id");

        var exception = Assert.Throws<ArgumentException>(() =>
            CreateFingerprintCandidate(
                Array.Empty<ResourceMutationIntent>(),
                new Dictionary<ResourceOperationKey, string>(),
                Array.Empty<ResourceMutationSourceExport>(),
                changed));

        Assert.Contains(
            "reaction effect ids",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BoundCandidate_PureFingerprintMatchesCachedAuthorityWithoutRuntimeCatalog()
    {
        var baseline = BaselineCharges();
        var candidate = CreateFingerprintCandidate(
            Array.Empty<ResourceMutationIntent>(),
            new Dictionary<ResourceOperationKey, string>(),
            Array.Empty<ResourceMutationSourceExport>());
        var bound = BindCandidateFromAcceptedPendingResolutions(
            baseline,
            candidate);
        var boundCandidate = Assert.Single(bound.TriggerCandidates);

        var fingerprint = AcceptedMechanicsPlanner.CreateCandidateFingerprint(
            boundCandidate);

        Assert.Equal(boundCandidate.CandidateFingerprint, fingerprint);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("reaction")]
    public void BoundCandidate_RejectsCachedFingerprintWhenOutputsChange(
        string changedOutput)
    {
        var baseline = BaselineCharges();
        var candidate = CreateFingerprintCandidate(
            Array.Empty<ResourceMutationIntent>(),
            new Dictionary<ResourceOperationKey, string>(),
            Array.Empty<ResourceMutationSourceExport>(),
            CreateFingerprintReaction());
        var bound = BindCandidateFromAcceptedPendingResolutions(
            baseline,
            candidate);
        var boundCandidate = Assert.Single(bound.TriggerCandidates);
        var pendingOutputs = boundCandidate.PendingOutputs.ToArray();
        var reactionOutputs = boundCandidate.ReactionOutputs.ToArray();
        if (string.Equals(changedOutput, "pending", StringComparison.Ordinal))
        {
            pendingOutputs[0] = pendingOutputs[0] with
            {
                SafeResourceLabel = "changed resource label"
            };
        }
        else
        {
            reactionOutputs[0] = ChangeFingerprintReaction(
                reactionOutputs[0],
                "parameters");
        }
        var tampered = CloneBoundCandidate(
            boundCandidate,
            pendingOutputs,
            reactionOutputs,
            boundCandidate.PlannedComponentIdsByMutation);

        var result = BuildBoundCandidate(baseline, bound, tampered);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_pending_candidate_origin_mismatch");
    }

    [Fact]
    public void BoundCandidate_RejectsSwappedReceiptMutationComponentBindings()
    {
        var baseline = BaselineCharges();
        var identity = new EffectActivationCandidateIdentity(
            "effect_fingerprint_candidate",
            "trigger_fingerprint_candidate",
            "owner_turn_end",
            "turn_2:fingerprint:activation",
            "turn_2:fingerprint:lifecycle");
        var firstOutput = CreateFingerprintPendingOutput(
            identity,
            ChargesCoordinate);
        var secondOutput = firstOutput with
        {
            ComponentId = "component_fingerprint_pending_second",
            SafeResourceLabel = "second resource"
        };
        var candidate = new EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate(
            new EffectActivationCandidate(
                identity,
                Priority: 100,
                ConsumesUse: false,
                EffectAuthority: PermanentEffectAuthority(identity.EffectId)),
            useSeed: null,
            producer: null,
            Array.Empty<ResourceOperationKey>(),
            Array.Empty<string>(),
            new Dictionary<ResourceOperationKey, string>(),
            new[] { firstOutput, secondOutput },
            Array.Empty<EffectReactionExecution>(),
            EffectAcceptedTurnPlanner.EffectResourceCandidateOrigin.Empty);
        var bound = BindCandidateFromAcceptedPendingResolutions(
            baseline,
            candidate);
        var boundCandidate = Assert.Single(bound.TriggerCandidates);
        var componentMap = boundCandidate.PlannedComponentIdsByMutation
            .ToDictionary(static pair => pair.Key, static pair => pair.Value);
        var mappedKeys = componentMap.Keys.ToArray();
        Assert.Equal(2, mappedKeys.Length);
        (componentMap[mappedKeys[0]], componentMap[mappedKeys[1]]) =
            (componentMap[mappedKeys[1]], componentMap[mappedKeys[0]]);
        var tampered = CloneBoundCandidate(
            boundCandidate,
            boundCandidate.PendingOutputs,
            boundCandidate.ReactionOutputs,
            componentMap);

        var result = BuildBoundCandidate(baseline, bound, tampered);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_pending_candidate_origin_mismatch");
    }

    [Fact]
    public void ResolvedPendingReplayBind_DuplicateSourceExportsFailsClosed()
    {
        var duplicate = new ResourceMutationSourceExport(
            "bounded_receipt",
            "resource_resolution_duplicate_source",
            FingerprintA,
            ResourceMutationSourceState.Active,
            SameTurn: true);
        var input = new EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution(
            new[] { duplicate, duplicate with { } },
            Array.Empty<ResourceMutationIntent>(),
            Array.Empty<ValidationIssue>());

        var result = BindResolvedPendingReplay(
            Array.Empty<ResourcePendingResolvedBinding>(),
            input);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_source_duplicate_exact");
    }

    [Fact]
    public void ResolvedPendingReplayBind_ProjectedMutationCollidingWithPlannedMutationFailsClosed()
    {
        const string requestId = "resource_resolution_projected_collision";
        var identity = new EffectActivationCandidateIdentity(
            "effect_fingerprint_candidate",
            "trigger_fingerprint_candidate",
            "owner_turn_end",
            "turn_2:fingerprint:activation",
            "turn_2:fingerprint:lifecycle");
        var output = CreateFingerprintPendingOutput(identity, ChargesCoordinate);
        var planned = new ResourceMutationIntent(
            output.EventRef,
            output.Coordinate,
            Amount: 1m,
            new ResourceMutationSourceRequest(
                "bounded_receipt",
                requestId,
                output.Operation),
            Array.Empty<ResourceOperationKey>(),
            Array.Empty<ResourceMutationEventRequirement>(),
            ReceiptId: requestId);
        var plannedSource = new ResourceMutationSourceExport(
            planned.Source.SourceKind,
            planned.Source.SourceId,
            FingerprintA,
            ResourceMutationSourceState.Active,
            SameTurn: true);
        var componentMap = new Dictionary<ResourceOperationKey, string>
        {
            [planned.Key] = "component_already_planned"
        };
        var candidate = new EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate(
            new EffectActivationCandidate(
                identity,
                Priority: 100,
                ConsumesUse: false,
                EffectAuthority: PermanentEffectAuthority(identity.EffectId)),
            useSeed: null,
            producer: null,
            new[] { planned.Key },
            new[] { "component_already_planned" },
            componentMap,
            new[] { output },
            Array.Empty<EffectReactionExecution>(),
            CandidateOrigin(
                new[] { planned },
                componentMap,
                plannedSource));
        var binding = CreateResolvedPendingBinding(
            candidate,
            output,
            requestId);
        var input = new EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution(
            new[] { plannedSource },
            new[] { planned },
            Array.Empty<ValidationIssue>())
        {
            TriggerCandidates = new[] { candidate },
            ComponentIdsByMutation =
                new Dictionary<ResourceOperationKey, string>
                {
                    [planned.Key] = "component_already_planned"
                }
        };

        var result = BindResolvedPendingReplay(new[] { binding }, input);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_planner_duplicate_operation");
    }

    private static (ResourceOperationKey OwnerDelimited,
        ResourceOperationKey ResourceDelimited)
        CreateDelimiterCollisionOperationKeys()
    {
        var ownerDelimited = new ResourceOperationKey(
            "turn_2:fingerprint:delimiter_collision",
            "effect_component",
            "effect_fingerprint_candidate",
            new ResourceCoordinate(
                "mortal_world",
                ResourceOwnerKind.Player,
                "a/b",
                "c"),
            ResourceOperation.Spend);
        var resourceDelimited = ownerDelimited with
        {
            Coordinate = ownerDelimited.Coordinate with
            {
                ResourceOwnerId = "a",
                ResourceKey = "b/c"
            }
        };
        return (ownerDelimited, resourceDelimited);
    }

    private static EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution
        BindResolvedPendingReplay(
            IReadOnlyList<ResourcePendingResolvedBinding> bindings,
            EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution resolution)
    {
        var replayType = typeof(AcceptedMechanicsPlanner).GetNestedType(
            "ResolvedPendingReplaySession",
            System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(replayType);
        var constructor = replayType!.GetConstructor(
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic,
            binder: null,
            new[] { typeof(IReadOnlyList<ResourcePendingResolvedBinding>) },
            modifiers: null);
        Assert.NotNull(constructor);
        var replay = constructor!.Invoke(new object[] { bindings });
        var bind = replayType.GetMethod(
            "Bind",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(bind);

        try
        {
            return Assert.IsType<
                EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution>(
                    bind!.Invoke(replay, new object[] { resolution }));
        }
        catch (System.Reflection.TargetInvocationException exception)
            when (exception.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(exception.InnerException)
                .Throw();
            throw;
        }
    }

    private static EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution
        BindCandidateFromAcceptedPendingResolutions(
        Baseline baseline,
        EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate candidate)
    {
        var discovery = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 2,
                Definitions: baseline.Definitions,
                State: baseline.State,
                History: baseline.History,
                Sources: baseline.Sources,
                Mutations: Array.Empty<ResourceMutationIntent>(),
                InitialTriggerCandidates: new[] { candidate }),
            IdentityFactory());
        Assert.True(
            discovery.IsValid,
            string.Join(
                Environment.NewLine,
                discovery.Issues.Select(static issue =>
                    $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
        var accepted = discovery.AcceptedPendingResolutions;
        Assert.Equal(candidate.PendingOutputs.Count, accepted.Count);
        var bindings = accepted.Select((resolution, index) =>
            CreateResolvedPendingBinding(
                candidate,
                resolution.Resolution,
                $"resource_resolution_cached_candidate_{index}",
                resolution.CausalAuthority)).ToArray();
        var unbound = new EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution(
            Array.Empty<ResourceMutationSourceExport>(),
            Array.Empty<ResourceMutationIntent>(),
            Array.Empty<ValidationIssue>())
        {
            TriggerCandidates = new[] { candidate },
            ComponentIdsByMutation =
                new Dictionary<ResourceOperationKey, string>()
        };

        var bound = BindResolvedPendingReplay(bindings, unbound);
        Assert.True(bound.IsValid, string.Join(Environment.NewLine, bound.Issues));
        return bound;
    }

    private static EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate
        CloneBoundCandidate(
        EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate candidate,
        IReadOnlyList<EffectAcceptedTurnPlanner.EffectBoundedResourceResolution>
            pendingOutputs,
        IReadOnlyList<EffectReactionExecution> reactionOutputs,
        IReadOnlyDictionary<ResourceOperationKey, string> componentMap)
    {
        var resolvedBindings = new Dictionary<
            string,
            ResourcePendingResolvedBinding>(StringComparer.Ordinal);
        foreach (var output in candidate.PendingOutputs)
        {
            if (candidate.TryResolvePendingBinding(
                    output.ComponentId,
                    out var binding))
            {
                resolvedBindings.Add(output.ComponentId, binding);
            }
        }

        return new EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate(
            candidate.Activation,
            candidate.UseSeed,
            candidate.Producer,
            candidate.PlannedMutationKeys,
            candidate.PlannedComponentIds,
            componentMap,
            pendingOutputs,
            reactionOutputs,
            candidate.Origin,
            candidate.CandidateFingerprint,
            resolvedBindings,
            candidate.PendingWaveOrdinal,
            candidate.CausalMaterialFingerprint);
    }

    private static AcceptedMechanicsResourcePlanningResult BuildBoundCandidate(
        Baseline baseline,
        EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution bound,
        EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate candidate) =>
        AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 2,
                Definitions: baseline.Definitions,
                State: baseline.State,
                History: baseline.History,
                Sources: CreateCatalog(bound.SourceExports.ToArray()),
                Mutations: bound.Mutations,
                InitialTriggerCandidates: new[] { candidate }),
            IdentityFactory());

    private static ResourcePendingResolvedBinding CreateResolvedPendingBinding(
        EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate candidate,
        EffectAcceptedTurnPlanner.EffectBoundedResourceResolution output,
        string requestId,
        ResourcePendingCausalAuthority? acceptedCausalAuthority = null)
    {
        var candidateFingerprint =
            AcceptedMechanicsPlanner.CreateCandidateFingerprint(candidate);
        var causalAuthority = acceptedCausalAuthority ?? new ResourcePendingCausalAuthority(
            candidate.Activation.Identity.EffectId,
            candidate.Activation.Identity.TriggerId,
            candidate.Activation.Identity.EventRef,
            candidate.Activation.Identity.TriggerEventRef,
            candidate.Producer == null
                ? null
                : AcceptedMechanicsPlanner.CreateStableProducerOperationKey(
                    candidate.Producer),
            candidate.Activation.Priority,
            0,
            candidate.Activation.ConsumesUse,
            null,
            output.ComponentId,
            output.AfterComponentId,
            candidateFingerprint,
            FingerprintA,
            0);
        var request = new ResourcePendingRequest(
            requestId,
            "session_fingerprint_replay",
            "accepted_request_fingerprint_replay",
            2,
            output.EventRef,
            output.EffectId,
            output.EffectAuthority,
            output.Source,
            output.SourceAuthority,
            output.Target,
            output.TargetAuthority,
            output.TriggerId,
            output.Coordinate,
            output.ResourceAuthority,
            output.Operation,
            output.MinimumAmount,
            output.MaximumAmount,
            output.SourceAuthorityFingerprint,
            output.PolicyFingerprint,
            FingerprintA,
            FingerprintA,
            Array.Empty<string>(),
            "2026-08-23T00:00:00.0000000+00:00",
            FingerprintA,
            output.SafeSourceLabel,
            output.SafeTargetLabel,
            output.SafeResourceLabel,
            output.SafeOperationLabel,
            causalAuthority);
        return new ResourcePendingResolvedBinding(
            request,
            "resource_delta",
            1m,
            "resolved",
            FingerprintB,
            2);
    }

    private static (string? ProducerOperationKey, string CandidateFingerprint)
        ResolveProducerFingerprints(ResourceOwnerKind ownerKind)
    {
        var producer = new ResourceOperationKey(
            "turn_43:fingerprint:producer",
            "registered_system_outcome",
            "fingerprint_producer",
            new ResourceCoordinate(
                "mortal_world",
                ownerKind,
                "fingerprint_owner",
                "health"),
            ResourceOperation.Damage);
        var identity = new EffectActivationCandidateIdentity(
            "effect_fingerprint_producer",
            "trigger_fingerprint_producer",
            "resource_damaged",
            "turn_43:fingerprint:activation",
            producer.EventRef);
        var candidate = new EffectAcceptedTurnPlanner
            .EffectResourceTriggerCandidate(
                new EffectActivationCandidate(
                    identity,
                    Priority: 100,
                    ConsumesUse: false,
                    EffectAuthority: PermanentEffectAuthority(identity.EffectId)),
                useSeed: null,
                producer,
                Array.Empty<ResourceOperationKey>(),
                Array.Empty<string>(),
                new Dictionary<ResourceOperationKey, string>(),
                Array.Empty<EffectAcceptedTurnPlanner
                    .EffectBoundedResourceResolution>(),
                Array.Empty<EffectReactionExecution>(),
                EffectAcceptedTurnPlanner.EffectResourceCandidateOrigin.Empty);
        return (
            AcceptedMechanicsPlanner.CreateStableProducerOperationKey(producer),
            AcceptedMechanicsPlanner.CreateCandidateFingerprint(candidate));
    }

    private static string ResolveCandidateFingerprint(
        Baseline baseline,
        IReadOnlyList<ResourceMutationIntent> mutations,
        EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate candidate)
    {
        var result = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 2,
                Definitions: baseline.Definitions,
                State: baseline.State,
                History: baseline.History,
                Sources: baseline.Sources,
                Mutations: mutations,
                InitialTriggerCandidates: new[] { candidate }),
            IdentityFactory());

        Assert.True(
            result.IsValid,
            string.Join(
                Environment.NewLine,
                result.Issues.Select(static issue =>
                    $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
        return Assert.Single(result.AcceptedPendingResolutions)
            .CausalAuthority.CandidateFingerprint;
    }

    private static EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate
        CreateFingerprintCandidate(
            IReadOnlyList<ResourceMutationIntent> mutations,
            IReadOnlyDictionary<ResourceOperationKey, string> componentMap,
            IReadOnlyList<ResourceMutationSourceExport> sourceExports,
            EffectReactionExecution? reaction = null)
    {
        var identity = new EffectActivationCandidateIdentity(
            "effect_fingerprint_candidate",
            "trigger_fingerprint_candidate",
            "owner_turn_end",
            "turn_2:fingerprint:activation",
            "turn_2:fingerprint:lifecycle");
        return new EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate(
            new EffectActivationCandidate(
                identity,
                Priority: 100,
                ConsumesUse: false,
                EffectAuthority: PermanentEffectAuthority(identity.EffectId)),
            useSeed: null,
            producer: null,
            mutations.Select(static mutation => mutation.Key).ToArray(),
            componentMap.Values
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static value => value, StringComparer.Ordinal)
                .ToArray(),
            componentMap,
            new[] { CreateFingerprintPendingOutput(identity, ChargesCoordinate) },
            reaction == null
                ? Array.Empty<EffectReactionExecution>()
                : new[] { reaction },
            mutations.Count == 0 &&
            componentMap.Count == 0 &&
            sourceExports.Count == 0
                ? EffectAcceptedTurnPlanner.EffectResourceCandidateOrigin.Empty
                : CandidateOrigin(
                    mutations,
                    componentMap,
                    sourceExports.ToArray()));
    }

    private static EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate
        CreatePureReactionCandidate(
            int ordinal,
            string effectId,
            string triggerId,
            string componentId,
            int maxExpansion,
            ResourceOperationKey? producer,
            string eventKind,
            string triggerEventRef)
    {
        var activation = new EffectActivationCandidateIdentity(
            effectId,
            triggerId,
            eventKind,
            $"turn_2:pure_reaction:activation:{ordinal}",
            triggerEventRef);
        var reaction = new EffectReactionExecution(
            EventRef: $"turn_2:pure_reaction:output:{ordinal}",
            TriggerEventRef: triggerEventRef,
            CausalEventRef: triggerEventRef,
            Turn: 2,
            EventKind: eventKind,
            Target: new EffectTargetKey(
                "mortal_world",
                "player",
                "player_current"),
            EffectId: effectId,
            TriggerId: triggerId,
            ComponentId: componentId,
            ResultKind: "event_outcome",
            Dependency: "before_current_event",
            AfterComponentId: null,
            MaxExpansion: maxExpansion,
            DownstreamSource: null,
            Parameters: null);
        return new EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate(
            new EffectActivationCandidate(
                activation,
                Priority: 100,
                ConsumesUse: false,
                EffectAuthority: PermanentEffectAuthority(activation.EffectId)),
            useSeed: null,
            producer,
            Array.Empty<ResourceOperationKey>(),
            Array.Empty<string>(),
            new Dictionary<ResourceOperationKey, string>(),
            Array.Empty<EffectAcceptedTurnPlanner
                .EffectBoundedResourceResolution>(),
            new[] { reaction },
            EffectAcceptedTurnPlanner.EffectResourceCandidateOrigin.Empty);
    }

    private static ResourcePendingAuthorityBinding PermanentEffectAuthority(
        string effectId) =>
        new("permanent", effectId);

    private static EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate
        CreateDelimiterFingerprintCandidate(ResourceOperationKey key)
    {
        var mutation = new ResourceMutationIntent(
            key.EventRef,
            key.Coordinate,
            Amount: 1m,
            new ResourceMutationSourceRequest(
                key.OriginKind,
                key.OriginId,
                key.Operation),
            Array.Empty<ResourceOperationKey>(),
            Array.Empty<ResourceMutationEventRequirement>(),
            ReceiptId: null);
        var source = new ResourceMutationSourceExport(
            key.OriginKind,
            key.OriginId,
            FingerprintA,
            ResourceMutationSourceState.Active,
            SameTurn: false,
            BoundOwner: new ResourceOwnerKey(
                key.Coordinate.Realm,
                key.Coordinate.OwnerKind,
                key.Coordinate.ResourceOwnerId));
        return CreateFingerprintCandidate(
            new[] { mutation },
            new Dictionary<ResourceOperationKey, string>
            {
                [key] = "component_delimiter_collision"
            },
            new[] { source });
    }

    private static EffectAcceptedTurnPlanner.EffectResourceCandidateOrigin
        CandidateOrigin(
            IReadOnlyList<ResourceMutationIntent> mutations,
            IReadOnlyDictionary<ResourceOperationKey, string> componentMap,
            params ResourceMutationSourceExport[] sourceExports) =>
        new(
            mutations,
            componentMap.Values
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static value => value, StringComparer.Ordinal)
                .ToArray(),
            componentMap,
            sourceExports);

    private static IReadOnlyList<ResourceMutationSourceExport>
        ReferencedCandidateSources(
            ResourceMutationSourceCatalog sources,
            IReadOnlyList<ResourceMutationIntent> mutations)
    {
        var referenced = mutations
            .Select(static mutation => (
                mutation.Source.SourceKind,
                mutation.Source.SourceId))
            .ToHashSet();
        return sources.Exports
            .Where(source => referenced.Contains((
                source.SourceKind,
                source.SourceId)))
            .ToArray();
    }

    private static EffectAcceptedTurnPlanner.EffectBoundedResourceResolution
        CreateFingerprintPendingOutput(
            EffectActivationCandidateIdentity identity,
            ResourceCoordinate coordinate) =>
        new(
            identity.EventRef,
            identity.EffectId,
            new ResourcePendingAuthorityBinding("permanent", identity.EffectId),
            new JsonObject
            {
                ["kind"] = "wound",
                ["sourceId"] = "fingerprint_source",
                ["definitionKey"] = "fingerprint_definition"
            },
            new ResourcePendingAuthorityBinding(
                "permanent",
                "fingerprint_source"),
            new JsonObject
            {
                ["kind"] = coordinate.OwnerKind == ResourceOwnerKind.Npc
                    ? "npc"
                    : coordinate.OwnerKind == ResourceOwnerKind.Player
                        ? "player"
                        : "item",
                ["targetId"] = coordinate.ResourceOwnerId
            },
            new ResourcePendingAuthorityBinding(
                "permanent",
                coordinate.ResourceOwnerId),
            identity.TriggerId,
            "component_fingerprint_pending",
            identity.TriggerEventRef,
            identity.EventKind,
            coordinate,
            new ResourcePendingAuthorityBinding(
                "permanent",
                coordinate.ResourceOwnerId),
            ResourceOperation.Gain,
            MinimumAmount: 0m,
            MaximumAmount: 1m,
            FingerprintA,
            FingerprintB,
            Array.Empty<ResourceOperationKey>(),
            Array.Empty<ResourceMutationEventRequirement>(),
            ResultConstraint: null,
            RemainingUseBudget: null,
            SafeSourceLabel: "source",
            SafeTargetLabel: "target",
            SafeResourceLabel: "resource",
            SafeOperationLabel: "gain");

    private static EffectReactionExecution CreateFingerprintReaction() =>
        new(
            EventRef: "turn_2:fingerprint:reaction",
            TriggerEventRef: "turn_2:fingerprint:lifecycle",
            CausalEventRef: "turn_2:fingerprint:reaction_cause",
            Turn: 2,
            EventKind: "owner_turn_end",
            Target: new EffectTargetKey(
                "mortal_world",
                "player",
                "player_current"),
            EffectId: "effect_fingerprint_candidate",
            TriggerId: "trigger_fingerprint_candidate",
            ComponentId: "component_fingerprint_reaction",
            ResultKind: "apply_definition",
            Dependency: "before_current_event",
            AfterComponentId: null,
            MaxExpansion: 1,
            DownstreamSource: null,
            Parameters: new JsonObject { ["amount"] = 1 },
            DownstreamSourceKey: new EffectSourceKey(
                "mortal_world",
                "wound",
                "fingerprint_downstream_alpha",
                "fingerprint_definition"),
            ReplacementTarget: new EffectReplayIdentity(
                "effect_fingerprint_candidate",
                new ResourcePendingAuthorityBinding(
                    "permanent",
                    "effect_fingerprint_candidate")));

    private static EffectReactionExecution ChangeFingerprintReaction(
        EffectReactionExecution canonical,
        string changedField) => changedField switch
        {
            "dependency" => canonical with
            {
                Dependency = "after_current_event"
            },
            "trigger_ref" => canonical with
            {
                TriggerEventRef = "turn_2:fingerprint:reaction_trigger_changed"
            },
            "causal_ref" => canonical with
            {
                CausalEventRef = "turn_2:fingerprint:reaction_cause_changed"
            },
            "turn" => canonical with
            {
                Turn = 3
            },
            "event_kind" => canonical with
            {
                EventKind = "scene_end"
            },
            "effect_id" => canonical with
            {
                EffectId = "effect_fingerprint_changed"
            },
            "trigger_id" => canonical with
            {
                TriggerId = "trigger_fingerprint_changed"
            },
            "component_priority" => canonical with
            {
                ComponentPriority = canonical.ComponentPriority + 1
            },
            "target" => canonical with
            {
                Target = canonical.Target with { TargetId = "player_changed" }
            },
            "after_component" => canonical with
            {
                AfterComponentId = "component_fingerprint_predecessor"
            },
            "downstream_source" => canonical with
            {
                DownstreamSourceKey = canonical.DownstreamSourceKey! with
                {
                    SourceId = "fingerprint_downstream_beta"
                }
            },
            "parameters" => canonical with
            {
                Parameters = new JsonObject { ["amount"] = 2 }
            },
            "max_expansion" => canonical with
            {
                MaxExpansion = 2
            },
            "replacement_target" => canonical with
            {
                ReplacementTarget = new EffectReplayIdentity(
                    "effect_fingerprint_replaced",
                    new ResourcePendingAuthorityBinding(
                        "permanent",
                        "effect_fingerprint_replaced"))
            },
            _ => throw new ArgumentOutOfRangeException(
                nameof(changedField),
                changedField,
                "Unsupported reaction fingerprint variant.")
        };

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

    private static Baseline BaselineChargesForEffects(params string[] effectSourceIds) =>
        BaselineCharges(effectSourceIds.Select(sourceId =>
            new ResourceMutationSourceExport(
                "effect_component",
                sourceId,
                FingerprintA,
                ResourceMutationSourceState.Active,
                SameTurn: false)).ToArray());

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

    private static AcceptedMechanicsIdentityFactory OperationOrderedIdentityFactory(
        params int[] operationOrder)
    {
        var allocated = operationOrder
            .SelectMany(static value => new[] { value, value + 1000 })
            .GetEnumerator();
        return new AcceptedMechanicsIdentityFactory(() =>
        {
            Assert.True(allocated.MoveNext());
            return new Guid(allocated.Current, 0, 0, new byte[8]);
        });
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
