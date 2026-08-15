using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceTriggerGraphTests
{
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
            operationId,
            dependencies ?? Array.Empty<string>(),
            eventRequirements ?? Array.Empty<ResourceEventRequirement>());
}
