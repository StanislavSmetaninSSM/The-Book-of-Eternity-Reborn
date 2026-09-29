using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceTriggerGraphTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Build_ChronologicalPhaseCrossingRequiresTheCompletedParent(bool completedParent, bool eventDependency)
    {
        var parent = Node("node_recovery", operationId: "operation_recovery");
        var child = Node("node_cost", phase: ResourceMutationPhase.DirectCost,
            operationId: "operation_cost",
            dependencies: eventDependency ? null : new[] { parent.NodeId },
            eventRequirements: eventDependency
                ? new[] { new ResourceEventRequirement(parent.NodeId, "resource_filled") } : null);
        var completed = new HashSet<string>(StringComparer.Ordinal)
        {
            completedParent ? parent.OperationId : "operation_unrelated"
        };
        var result = ResourceTriggerGraph.Build(new[] { parent, child }, completed);
        if (!completedParent)
        {
            Assert.Contains(result.Issues, issue => issue.Code == "resource_graph_phase_inversion");
            Assert.Null(result.Graph);
            return;
        }
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        // The edge and parent remain in the graph; the resumed scheduler alone
        // skips the exact completed operation and executes the new child once.
        Assert.Equal(new[] { parent.NodeId, child.NodeId }, result.Graph!.OrderedNodes.Select(node => node.NodeId));
        var scheduler = result.Graph.CreateExecutionScheduler(_ => true, completed);
        Assert.True(scheduler.TryTakeNext(out var next, out var execute));
        Assert.Equal(child.NodeId, next.NodeId);
        Assert.True(execute);
        scheduler.Complete(next);
        Assert.False(scheduler.HasPendingNodes);
    }

    [Fact]
    public void Build_RejectsDuplicateConfusableMissingDependencyAndCycle()
    {
        var duplicate = ResourceTriggerGraph.Build(new[]
        {
            Node("node_alpha"),
            Node("NODE_ALPHA")
        });
        var missing = ResourceTriggerGraph.Build(new[]
        {
            Node("node_alpha", dependencies: new[] { "node_missing" })
        });
        var cycle = ResourceTriggerGraph.Build(new[]
        {
            Node("node_alpha", dependencies: new[] { "node_beta" }),
            Node("node_beta", dependencies: new[] { "node_alpha" })
        });

        Assert.Contains(duplicate.Issues, issue =>
            issue.Code == "resource_graph_duplicate_node");
        Assert.Contains(missing.Issues, issue =>
            issue.Code == "resource_graph_dependency_missing");
        Assert.Contains(cycle.Issues, issue =>
            issue.Code == "resource_graph_cycle");
        Assert.Null(duplicate.Graph);
        Assert.Null(missing.Graph);
        Assert.Null(cycle.Graph);
    }

    [Fact]
    public void Build_Accepts1024NodesAndRejects1025()
    {
        var accepted = ResourceTriggerGraph.Build(
            Enumerable.Range(0, 1024)
                .Select(index => Node($"node_{index:D4}")));
        var rejected = ResourceTriggerGraph.Build(
            Enumerable.Range(0, 1025)
                .Select(index => Node($"node_{index:D4}")));

        Assert.True(accepted.IsValid, string.Join(Environment.NewLine, accepted.Issues));
        Assert.Equal(1024, accepted.Graph!.OrderedNodes.Count);
        Assert.Contains(rejected.Issues, issue =>
            issue.Code == "resource_graph_node_limit_exceeded");
    }

    [Fact]
    public void Build_AcceptsDepth32AndRejectsDepth33()
    {
        var accepted = ResourceTriggerGraph.Build(Chain(32));
        var rejected = ResourceTriggerGraph.Build(Chain(33));

        Assert.True(accepted.IsValid, string.Join(Environment.NewLine, accepted.Issues));
        Assert.Equal(32, accepted.Graph!.MaximumDepth);
        Assert.Contains(rejected.Issues, issue =>
            issue.Code == "resource_graph_depth_limit_exceeded");
    }

    [Fact]
    public void Build_UsesPhasePriorityOriginOperationOrderAcrossRandomEnumeration()
    {
        var nodes = new[]
        {
            Node(
                "node_effect",
                phase: ResourceMutationPhase.EffectTrigger,
                priority: 10,
                originId: "origin_a",
                operationId: "operation_a"),
            Node(
                "node_direct_b",
                phase: ResourceMutationPhase.DirectOutcome,
                priority: 100,
                originId: "origin_b",
                operationId: "operation_b"),
            Node(
                "node_cost",
                phase: ResourceMutationPhase.DirectCost,
                priority: 900,
                originId: "origin_z",
                operationId: "operation_z"),
            Node(
                "node_direct_a2",
                phase: ResourceMutationPhase.DirectOutcome,
                priority: 100,
                originId: "origin_a",
                operationId: "operation_b"),
            Node(
                "node_direct_a1",
                phase: ResourceMutationPhase.DirectOutcome,
                priority: 100,
                originId: "origin_a",
                operationId: "operation_a")
        };
        var expected = new[]
        {
            "node_cost",
            "node_direct_a1",
            "node_direct_a2",
            "node_direct_b",
            "node_effect"
        };

        for (var seed = 0; seed < 25; seed++)
        {
            var random = new Random(seed);
            var result = ResourceTriggerGraph.Build(nodes.OrderBy(_ => random.Next()).ToArray());

            Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
            Assert.Equal(expected, result.Graph!.OrderedNodes.Select(node => node.NodeId));
        }
    }

    [Fact]
    public void Build_RejectsPhaseInversionAndInvalidEventRequirement()
    {
        var inversion = ResourceTriggerGraph.Build(new[]
        {
            Node("node_effect", phase: ResourceMutationPhase.EffectTrigger),
            Node(
                "node_cost",
                phase: ResourceMutationPhase.DirectCost,
                dependencies: new[] { "node_effect" })
        });
        var invalidEvent = ResourceTriggerGraph.Build(new[]
        {
            Node("node_alpha"),
            Node(
                "node_beta",
                eventRequirements: new[]
                {
                    new ResourceEventRequirement("node_alpha", "arbitrary_event")
                })
        });

        Assert.Contains(inversion.Issues, issue =>
            issue.Code == "resource_graph_phase_inversion");
        Assert.Contains(invalidEvent.Issues, issue =>
            issue.Code == "resource_graph_event_invalid");
    }

    [Fact]
    public void Build_TreatsExactEventProducerAsDependencyAndOrdersNestedChain()
    {
        var result = ResourceTriggerGraph.Build(new[]
        {
            Node("node_root"),
            Node(
                "node_depleted",
                phase: ResourceMutationPhase.RegisteredSystemOutcome,
                eventRequirements: new[]
                {
                    new ResourceEventRequirement("node_root", "resource_depleted")
                }),
            Node(
                "node_filled",
                phase: ResourceMutationPhase.EffectTrigger,
                eventRequirements: new[]
                {
                    new ResourceEventRequirement("node_depleted", "resource_filled")
                })
        });

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        Assert.Equal(
            new[] { "node_root", "node_depleted", "node_filled" },
            result.Graph!.OrderedNodes.Select(node => node.NodeId));
        Assert.Equal(3, result.Graph.MaximumDepth);
    }

    [Fact]
    public void EffectAdapter_BindsDeclaredResourceEventTriggerToExactProducerOperation()
    {
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var ownerKey = new ResourceOwnerKey(
            "mortal_world",
            ResourceOwnerKind.Player,
            "player_current");
        var owners = ResourceOwnerAuthority.Build(new ResourceOwnerAuthorityInput(
            new[]
            {
                new ResourceOwnerExport(
                    ownerKey,
                    ResourceOwnerLifecycle.Active,
                    SameTurn: false,
                    OwnerRef: null,
                    BoundNpcId: null,
                    new HashSet<string>(StringComparer.Ordinal) { "health" },
                    FingerprintA)
            },
            Array.Empty<ResourceOwnerExport>(),
            Array.Empty<ResourceOwnerKey>()));
        var targets = EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
            new[]
            {
                new EffectTargetExport(
                    "mortal_world",
                    "player",
                    "player_current",
                    SameTurn: false)
            },
            Array.Empty<EffectTargetExport>(),
            new HashSet<string>(StringComparer.Ordinal),
            CombatantIdentities: null));
        Assert.Empty(owners.Issues);
        Assert.Empty(targets.Issues);
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "periodic_restore");
        effect["triggers"]![0]!["triggerId"] = "on_resource_depleted";
        effect["triggers"]![0]!["eventType"] = "resource_depleted";
        var producer = new ResourceMutationIntent(
            "turn_43:damage:1",
            new ResourceCoordinate(
                "mortal_world",
                ResourceOwnerKind.Player,
                "player_current",
                "health"),
            Amount: 10m,
            new ResourceMutationSourceRequest(
                "combat_outcome",
                "combat_damage_alpha",
                ResourceOperation.Damage),
            Array.Empty<ResourceOperationKey>(),
            Array.Empty<ResourceMutationEventRequirement>(),
            ReceiptId: null);

        var resolution = InvokeResourceEventResolution(
            effect,
            "on_resource_depleted",
            producer.Key,
            "resource_depleted",
            turn: 43,
            targets,
            owners,
            definitions);

        Assert.Empty(resolution.Issues);
        var source = Assert.Single(resolution.SourceExports);
        var mutation = Assert.Single(resolution.Mutations);
        Assert.Equal("effect_component", source.SourceKind);
        Assert.Equal(ownerKey, source.BoundOwner);
        var requirement = Assert.Single(mutation.EventRequirements);
        Assert.Equal(producer.Key, requirement.Producer);
        Assert.Equal("resource_depleted", requirement.EventKind);
        Assert.Equal(ResourceOperation.Restore, mutation.Source.Operation);
    }

    [Fact]
    public void Build_OrdersNestedResourceEventReadySetAndRejectsEventCycleAndExpansion()
    {
        var root = Node("root_damage", originId: "root");
        var first = Node(
            "effect_a",
            phase: ResourceMutationPhase.EffectTrigger,
            priority: 200,
            originId: "effect_a",
            eventRequirements: new[]
            {
                new ResourceEventRequirement("root_damage", "resource_depleted")
            });
        var second = Node(
            "effect_b",
            phase: ResourceMutationPhase.EffectTrigger,
            priority: 200,
            originId: "effect_b",
            eventRequirements: new[]
            {
                new ResourceEventRequirement("root_damage", "resource_depleted")
            });
        var nested = Node(
            "effect_nested",
            phase: ResourceMutationPhase.EffectTrigger,
            priority: 100,
            originId: "effect_0_nested",
            eventRequirements: new[]
            {
                new ResourceEventRequirement("effect_b", "resource_filled")
            });
        var ordered = ResourceTriggerGraph.Build(new[] { nested, second, root, first });
        var cycle = ResourceTriggerGraph.Build(new[]
        {
            Node(
                "cycle_a",
                eventRequirements: new[]
                {
                    new ResourceEventRequirement("cycle_b", "resource_depleted")
                }),
            Node(
                "cycle_b",
                eventRequirements: new[]
                {
                    new ResourceEventRequirement("cycle_a", "resource_filled")
                })
        });
        var expansion = ResourceTriggerGraph.Build(Enumerable.Range(0, 1025)
            .Select(index => Node(
                $"event_node_{index:D4}",
                eventRequirements: index == 0
                    ? Array.Empty<ResourceEventRequirement>()
                    : new[]
                    {
                        new ResourceEventRequirement(
                            $"event_node_{index - 1:D4}",
                            index % 2 == 0
                                ? "resource_depleted"
                                : "resource_filled")
                    })));

        Assert.True(ordered.IsValid, string.Join(Environment.NewLine, ordered.Issues));
        Assert.Equal(
            new[] { "root_damage", "effect_a", "effect_b", "effect_nested" },
            ordered.Graph!.OrderedNodes.Select(static node => node.NodeId));
        Assert.Contains(cycle.Issues, issue => issue.Code == "resource_graph_cycle");
        Assert.Contains(expansion.Issues, issue =>
            issue.Code == "resource_graph_node_limit_exceeded");
    }

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

    private static IReadOnlyList<ResourceTriggerGraphNode> Chain(int count) =>
        Enumerable.Range(0, count)
            .Select(index => Node(
                $"node_{index:D2}",
                dependencies: index == 0
                    ? Array.Empty<string>()
                    : new[] { $"node_{index - 1:D2}" }))
            .ToArray();

    private static ResourceTriggerGraphNode Node(
        string nodeId,
        ResourceMutationPhase phase = ResourceMutationPhase.DirectOutcome,
        int priority = 100,
        string originId = "origin_alpha",
        string operationId = "operation_alpha",
        IReadOnlyList<string>? dependencies = null,
        IReadOnlyList<ResourceEventRequirement>? eventRequirements = null) =>
        new(
            nodeId,
            phase,
            priority,
            originId,
            nodeId,
            operationId,
            dependencies ?? Array.Empty<string>(),
            eventRequirements ?? Array.Empty<ResourceEventRequirement>());

    private const string FingerprintA =
        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
}
