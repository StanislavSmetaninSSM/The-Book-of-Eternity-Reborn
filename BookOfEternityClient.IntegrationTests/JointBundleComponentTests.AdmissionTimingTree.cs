using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    private sealed partial class Fixture
    {
        /// <summary>Validates strict lexical function identity and only the documented returned read-path lifetime.</summary>
        /// <param name="rows">The complete detached interval from this fixture's same owned queue/window.</param>
        /// <returns>Validated function nodes, every linked returned lifetime and truthful pairing availability.</returns>
        private static AdmissionTimingAnalysis ReadAdmissionTimingTree(
            (string Name, string Boundary, double Milliseconds, long SourceScopeId)[] rows)
        {
            if (rows.All(row => row.SourceScopeId == 0))
                return new(ReadLegacyStrictAdmissionTree(rows), [], rows.Length, false);
            var functions = new List<AdmissionTimingNode>();
            var stack = new Stack<FunctionTimingFrame>();
            var returned = new Dictionary<long, ReturnedReadPathLifetimeBuilder>();
            var lifetimes = new List<ReturnedReadPathLifetime>();
            long previousBeginId = 0;
            for (var ordinal = 0; ordinal < rows.Length; ordinal++)
            {
                var row = rows[ordinal];
                Assert.True(row.SourceScopeId > 0, "Recorded intervals cannot mix unavailable or negative source IDs.");
                Assert.True(double.IsFinite(row.Milliseconds) && row.Milliseconds >= 0);
                if (row.Boundary == "begin")
                {
                    Assert.Equal(0, row.Milliseconds);
                    Assert.True(row.SourceScopeId > previousBeginId, "Original begin IDs must increase, not necessarily contiguously or from one.");
                    previousBeginId = row.SourceScopeId;
                    if (row.Name == "OrdinaryReadPath.Phase")
                    {
                        Assert.NotEmpty(stack);
                        var factory = stack.Peek();
                        Assert.Equal("OrdinaryReadPath.OpenPhase", factory.Node.Name);
                        var factoryState = factory.Factory;
                        Assert.NotNull(factoryState);
                        Assert.False(factoryState.ReturnedLifetimeAssigned);
                        factoryState.ReturnedLifetimeAssigned = true;
                        returned.Add(row.SourceScopeId, new(row.SourceScopeId, factoryState, ordinal));
                    }
                    else
                    {
                        var node = new AdmissionTimingNode(row.Name, []);
                        functions.Add(node);
                        if (stack.TryPeek(out var parent)) parent.Node.Children.Add(node);
                        stack.Push(new(row.SourceScopeId, node, row.Name == "OrdinaryReadPath.OpenPhase"
                            ? new ReturnedPhaseFactoryState(row.SourceScopeId) : null));
                    }
                }
                else
                {
                    Assert.Equal("end", row.Boundary);
                    if (row.Name == "OrdinaryReadPath.Phase")
                    {
                        Assert.True(returned.Remove(row.SourceScopeId, out var lifetime), "Returned lifetime has no matching original begin.");
                        Assert.NotNull(lifetime);
                        Assert.True(lifetime.BeginOrdinal < lifetime.Factory.EndOrdinal && lifetime.Factory.EndOrdinal < ordinal,
                            "The actual factory must close after lifetime creation and before returned-object disposal.");
                        lifetimes.Add(new(lifetime.SourceScopeId, lifetime.Factory.SourceScopeId,
                            lifetime.BeginOrdinal, lifetime.Factory.EndOrdinal, ordinal));
                    }
                    else
                    {
                        Assert.NotEmpty(stack);
                        var current = stack.Peek();
                        Assert.Equal(current.SourceScopeId, row.SourceScopeId);
                        Assert.Equal(current.Node.Name, row.Name);
                        stack.Pop();
                        if (current.Factory is { } factory) factory.EndOrdinal = ordinal;
                    }
                }
            }
            Assert.Empty(stack);
            Assert.Empty(returned);
            Assert.Equal(rows.Length, 2 * (functions.Count + lifetimes.Count));
            return new(functions, lifetimes, rows.Length, true);
        }

        /// <summary>Describes the fixed lifetime interpretation after every input row and creation link was validated.</summary>
        /// <param name="analysis">This invocation's already validated DATA; legacy never gains source identity.</param>
        /// <returns>Truthful bounded proof counts, with all underlying original rows retained in full capture.</returns>
        private static object DescribeReturnedReadPathLifetimes(AdmissionTimingAnalysis analysis) => new
        {
            Producer = "OrdinaryReadPath.Phase", Factory = "OrdinaryReadPath.OpenPhase",
            Count = analysis.Lifetimes.Count, LinkedFactoryCount = analysis.Lifetimes.Count,
            ReturnBeforeCloseVerified = analysis.SourceScopeIdPairingAvailable,
            IdentitySource = analysis.SourceScopeIdPairingAvailable ? "same-private-passive-window" : "legacy-unavailable"
        };

        /// <summary>Detached reader result; its same-window IDs never grant filesystem or publication authority.</summary>
        private sealed record AdmissionTimingAnalysis(IReadOnlyList<AdmissionTimingNode> Functions,
            IReadOnlyList<ReturnedReadPathLifetime> Lifetimes, int RowsConsumed, bool SourceScopeIdPairingAvailable);

        /// <summary>One original returned-object span and its exact creating factory's original source identity.</summary>
        private sealed record ReturnedReadPathLifetime(long SourceScopeId, long FactoryScopeId,
            int BeginOrdinal, int FactoryEndOrdinal, int EndOrdinal);

        /// <summary>Value stack state; ordinary function scopes allocate no second per-function frame object.</summary>
        private readonly record struct FunctionTimingFrame(long SourceScopeId, AdmissionTimingNode Node, ReturnedPhaseFactoryState? Factory);

        /// <summary>Only an actual named factory needs shared close state while its one returned object remains open.</summary>
        /// <param name="sourceScopeId">The original positive factory identity.</param>
        private sealed class ReturnedPhaseFactoryState(long sourceScopeId)
        {
            internal long SourceScopeId { get; } = sourceScopeId;
            internal int EndOrdinal { get; set; } = -1;
            internal bool ReturnedLifetimeAssigned { get; set; }
        }

        /// <summary>Temporary open lifetime association; removal on end releases its factory frame.</summary>
        private sealed record ReturnedReadPathLifetimeBuilder(long SourceScopeId, ReturnedPhaseFactoryState Factory, int BeginOrdinal);

        /// <summary>
        /// Preserves the original all-zero legacy strict name-stack interpretation without an identity claim.
        /// </summary>
        /// <param name="rows">
        /// Every event from one complete synchronous observation interval, in original order.
        /// </param>
        /// <returns>
        /// Every node, retaining its direct children for the declared chain checks.
        /// </returns>
        private static IReadOnlyList<AdmissionTimingNode> ReadLegacyStrictAdmissionTree(
            (string Name, string Boundary, double Milliseconds, long SourceScopeId)[] rows)
        {
            var nodes = new List<AdmissionTimingNode>();
            var stack = new Stack<AdmissionTimingNode>();
            foreach (var row in rows)
            {
                Assert.True(double.IsFinite(row.Milliseconds) && row.Milliseconds >= 0);
                if (row.Boundary == "begin")
                {
                    var node = new AdmissionTimingNode(row.Name, []);
                    nodes.Add(node);
                    if (stack.TryPeek(out var parent)) parent.Children.Add(node);
                    stack.Push(node);
                }
                else
                {
                    Assert.Equal("end", row.Boundary);
                    Assert.NotEmpty(stack);
                    Assert.Equal(stack.Pop().Name, row.Name);
                }
            }
            Assert.Empty(stack);
            return nodes;
        }

    }
}
