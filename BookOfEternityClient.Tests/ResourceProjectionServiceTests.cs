using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceProjectionServiceTests
{
    private const string FingerprintA =
        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string FingerprintB =
        "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public void Project_PlayerVisibleResourceUsesLocalizedSafeFactsAndRecentDelta()
    {
        var fixture = CreateFixture(
            ResourceVisibility.PlayerVisible,
            current: 75m,
            maximum: 100m,
            ResourceLifecycleState.Active,
            ResourceOwnerKind.Player,
            resourceKey: "health",
            operation: ResourceTransitionOperation.Damage,
            appliedAmount: 25m);

        var result = ResourceProjectionService.Project(
            fixture.Request,
            ResourceProjectionAudience.Player);

        Assert.True(result.IsAvailable);
        Assert.Null(result.UnavailableMessage);
        var row = Assert.Single(result.Rows);
        Assert.Equal("герой", row.SafeOwnerSelector);
        Assert.Equal("health", row.ResourceKey);
        Assert.Equal("Здоровье", row.DisplayName);
        Assert.Equal("%", row.Unit);
        Assert.Equal(75m, row.Current);
        Assert.Equal(100m, row.Maximum);
        Assert.Equal(75m, row.Percentage);
        Assert.Equal(-25m, row.RecentVisibleDelta);
        Assert.Equal(ResourceLifecycleState.Active, row.State);
        Assert.Equal(new[] { "damage", "restore" }, row.AvailableOperations);
    }

    [Fact]
    public void Project_OwnerVisibleResourceRequiresOwningPlayerScope()
    {
        var fixture = CreateFixture(
            ResourceVisibility.OwnerVisible,
            current: 2m,
            maximum: 5m,
            ResourceLifecycleState.Active,
            ResourceOwnerKind.Item,
            resourceKey: "owner_reserve",
            operation: ResourceTransitionOperation.Spend,
            appliedAmount: 3m);

        var nonOwner = ResourceProjectionService.Project(
            fixture.Request with
            {
                OwnerScopes = new[]
                {
                    fixture.Scope with { IsOwningPlayer = false }
                }
            },
            ResourceProjectionAudience.Player);
        var owner = ResourceProjectionService.Project(
            fixture.Request,
            ResourceProjectionAudience.Player);

        Assert.True(nonOwner.IsAvailable);
        Assert.Empty(nonOwner.Rows);
        Assert.Single(owner.Rows);
    }

    [Fact]
    public void Project_HiddenAndGmOnlyResourcesNeverAppearToPlayer()
    {
        var gmOnly = CreateFixture(
            ResourceVisibility.GmOnly,
            current: 5m,
            maximum: 5m,
            ResourceLifecycleState.Active,
            ResourceOwnerKind.Player,
            resourceKey: "gm_reserve");
        var hidden = CreateFixture(
            ResourceVisibility.Hidden,
            current: 5m,
            maximum: 5m,
            ResourceLifecycleState.Active,
            ResourceOwnerKind.Player,
            resourceKey: "hidden_reserve");
        var definitions = gmOnly.Definitions.With(hidden.Definition);
        var state = new ResourceStateLedger(
            gmOnly.State.Entries.Concat(hidden.State.Entries));
        var history = CreateHistory(
            definitions,
            gmOnly.Transitions.Concat(hidden.Transitions.Select(
                static transition => transition with
                {
                    ExecutionSequence = transition.ExecutionSequence + 1
                })));
        var input = CreateRequest(definitions, state, history, gmOnly.Scope);

        var player = ResourceProjectionService.Project(input, ResourceProjectionAudience.Player);
        var gm = ResourceProjectionService.Project(input, ResourceProjectionAudience.GameMaster);

        Assert.True(player.IsAvailable);
        Assert.Empty(player.Rows);
        Assert.Equal(
            new[] { "gm_reserve", "hidden_reserve" },
            gm.Rows.Select(static row => row.ResourceKey).OrderBy(static key => key, StringComparer.Ordinal));
    }

    [Fact]
    public void Project_SuspendedResourceHasNoMechanicalActions()
    {
        var fixture = CreateFixture(
            ResourceVisibility.PlayerVisible,
            current: 10m,
            maximum: 10m,
            ResourceLifecycleState.Suspended,
            ResourceOwnerKind.Player,
            resourceKey: "energy");

        var result = ResourceProjectionService.Project(
            fixture.Request,
            ResourceProjectionAudience.Player);

        var row = Assert.Single(result.Rows);
        Assert.Equal(ResourceLifecycleState.Suspended, row.State);
        Assert.Empty(row.AvailableOperations);
    }

    [Fact]
    public void Project_MismatchedSnapshotFailsClosedWithoutRowsOrTechnicalCause()
    {
        var fixture = CreateFixture(
            ResourceVisibility.PlayerVisible,
            current: 75m,
            maximum: 100m,
            ResourceLifecycleState.Active,
            ResourceOwnerKind.Player,
            resourceKey: "health",
            operation: ResourceTransitionOperation.Damage,
            appliedAmount: 25m);
        var staleEntry = fixture.State.Entries[0] with { Current = 74m };

        var result = ResourceProjectionService.Project(
            CreateRequest(
                fixture.Definitions,
                new ResourceStateLedger(new[] { staleEntry }),
                fixture.History,
                fixture.Scope),
            ResourceProjectionAudience.Player);

        Assert.False(result.IsAvailable);
        Assert.Empty(result.Rows);
        Assert.Equal(ResourcePlayerFailureMessages.Unavailable, result.UnavailableMessage);
        Assert.DoesNotContain("resource", result.UnavailableMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("json", result.UnavailableMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PlayerResultSerializationContainsNoProtectedAuthority()
    {
        var fixture = CreateFixture(
            ResourceVisibility.PlayerVisible,
            current: 75m,
            maximum: 100m,
            ResourceLifecycleState.Active,
            ResourceOwnerKind.Player,
            resourceKey: "health",
            operation: ResourceTransitionOperation.Damage,
            appliedAmount: 25m);

        var result = ResourceProjectionService.Project(
            fixture.Request,
            ResourceProjectionAudience.Player);
        var json = JsonSerializer.Serialize(result);

        Assert.DoesNotContain("player_internal_42", json, StringComparison.Ordinal);
        Assert.DoesNotContain("transition_", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("operation_", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("eventRef", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fingerprint", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("receipt", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("repair", json, StringComparison.OrdinalIgnoreCase);
    }

    private static ProjectionFixture CreateFixture(
        ResourceVisibility visibility,
        decimal current,
        decimal maximum,
        ResourceLifecycleState state,
        ResourceOwnerKind ownerKind,
        string resourceKey,
        ResourceTransitionOperation? operation = null,
        decimal appliedAmount = 0m)
    {
        var builtIns = ResourceDefinitionCatalog.CreateBuiltIn();
        ResourceDefinition definition;
        if (builtIns.TryResolveExact(resourceKey, out var builtIn) &&
            builtIn != null &&
            builtIn.Visibility == visibility)
        {
            definition = builtIn;
        }
        else
        {
            definition = new ResourceDefinition(
                resourceKey,
                1,
                resourceKey switch
                {
                    "owner_reserve" => "Личный резерв",
                    "gm_reserve" => "Резерв ведущего",
                    _ => "Скрытый резерв"
                },
                ResourceNumericKind.Integer,
                "point",
                1m,
                new ResourceMinimumPolicy(ResourceMinimumKind.DefinitionFixed, 0m),
                new ResourceCapacityPolicy(ResourceCapacityKind.InstanceFixed, null, null),
                new ResourceInitializationPolicy(ResourceInitializationKind.Maximum, null, null),
                new HashSet<ResourceOwnerKind> { ownerKind },
                new HashSet<ResourceOperation> { ResourceOperation.Spend, ResourceOperation.Gain },
                ResourceBoundPolicy.RejectBelowMinimum,
                ResourceBoundPolicy.ClampToMaximum,
                visibility,
                new ResourceDefinitionMaterialization(
                    1,
                    "definition_" + resourceKey,
                    "seal_" + resourceKey,
                    1,
                    "turn_1"));
        }

        var definitions = builtIns.TryResolveExact(resourceKey, out _)
            ? builtIns
            : builtIns.With(definition);
        var coordinate = new ResourceCoordinate(
            "mortal_world",
            ownerKind,
            "player_internal_42",
            resourceKey);
        var binding = new ResourceCapacityBinding(
            definition.CapacityPolicy.Kind,
            definition.CapacityPolicy.FormulaKey ?? "capacity_" + resourceKey,
            FingerprintA);
        var initialized = new ResourceStateSnapshot(
            maximum,
            maximum,
            binding,
            ResourceLifecycleState.Active);
        var initialize = new ResourceTransition(
            "transition_initialize_" + resourceKey,
            "operation_initialize_" + resourceKey,
            "turn_1:resource:1",
            "owner_materialization",
            "owner_materialization_" + resourceKey,
            ResourceMutationPhase.RegisteredSystemOutcome,
            50,
            0,
            coordinate,
            ResourceTransitionOperation.Initialize,
            0m,
            0m,
            ResourceTransitionOutcome.Applied,
            ResourceCapacityDisposition.InitializeFromDefinition,
            null,
            initialized,
            new ResourceSourceEvidence("owner_materialization", "owner_materialization_" + resourceKey, FingerprintA),
            FingerprintA,
            null,
            1);
        var transitions = new List<ResourceTransition> { initialize };
        ResourceTransition latest = initialize;
        if (operation.HasValue)
        {
            latest = new ResourceTransition(
                "transition_recent_" + resourceKey,
                "operation_recent_" + resourceKey,
                "turn_2:resource:1",
                "ordinary_action",
                "ordinary_action_" + resourceKey,
                ResourceMutationPhase.DirectOutcome,
                100,
                1,
                coordinate,
                operation.Value,
                appliedAmount,
                appliedAmount,
                ResourceTransitionOutcome.Applied,
                null,
                initialized,
                initialized with { Current = current },
                new ResourceSourceEvidence("ordinary_action", "ordinary_action_" + resourceKey, FingerprintB),
                FingerprintB,
                null,
                2);
            transitions.Add(latest);
        }
        else if (current != maximum)
        {
            throw new ArgumentException("A non-maximum fixture requires an ordinary operation.");
        }

        if (state == ResourceLifecycleState.Suspended)
        {
            var beforeSuspend = latest.AfterState!;
            latest = new ResourceTransition(
                "transition_suspend_" + resourceKey,
                "operation_suspend_" + resourceKey,
                "turn_3:resource:1",
                "owner_lifecycle",
                "owner_lifecycle_" + resourceKey,
                ResourceMutationPhase.RegisteredSystemOutcome,
                50,
                2,
                coordinate,
                ResourceTransitionOperation.Suspend,
                0m,
                0m,
                ResourceTransitionOutcome.Applied,
                null,
                beforeSuspend,
                beforeSuspend with { State = ResourceLifecycleState.Suspended },
                new ResourceSourceEvidence("owner_lifecycle", "owner_lifecycle_" + resourceKey, FingerprintB),
                FingerprintB,
                null,
                3);
            transitions.Add(latest);
        }

        var history = CreateHistory(definitions, transitions);
        var entry = new ResourceStateEntry(
            coordinate,
            current,
            maximum,
            binding,
            state,
            new ResourceChronology(
                1,
                initialize.EventRef,
                latest.TransitionId,
                latest.EventRef,
                latest.Turn));
        var scope = new ResourceProjectionOwnerScope(
            new ResourceOwnerKey(coordinate.Realm, coordinate.OwnerKind, coordinate.ResourceOwnerId),
            "герой",
            IsOwningPlayer: true);
        var stateLedger = new ResourceStateLedger(new[] { entry });
        return new ProjectionFixture(
            CreateRequest(definitions, stateLedger, history, scope),
            scope,
            definition,
            definitions,
            stateLedger,
            history,
            transitions);
    }

    private static ResourceProjectionRequest CreateRequest(
        ResourceDefinitionCatalog definitions,
        ResourceStateLedger state,
        ResourceHistoryState history,
        params ResourceProjectionOwnerScope[] scopes) =>
        new(
            new ResourceProjectionInput(
                definitions.ToCanonicalRoot(),
                JsonNode.Parse(state.ToCanonicalJson())!.AsObject(),
                JsonNode.Parse(history.ToCanonicalJson())!.AsObject(),
                FingerprintA),
            scopes);

    private static ResourceHistoryState CreateHistory(
        ResourceDefinitionCatalog definitions,
        IEnumerable<ResourceTransition> transitions)
    {
        var result = ResourceHistoryState.CreateValidated(transitions, definitions);
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        return result.History!;
    }

    private sealed record ProjectionFixture(
        ResourceProjectionRequest Request,
        ResourceProjectionOwnerScope Scope,
        ResourceDefinition Definition,
        ResourceDefinitionCatalog Definitions,
        ResourceStateLedger State,
        ResourceHistoryState History,
        IReadOnlyList<ResourceTransition> Transitions);

}
