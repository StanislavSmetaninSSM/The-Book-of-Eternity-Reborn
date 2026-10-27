using System.Text.Json;
using Xunit;
using Xunit.Sdk;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    /// <summary>Pairs two actual independently returned phases even when their object lifetimes close out of order.</summary>
    /// <returns>Completion after both actual returned objects and their fixture are closed.</returns>
    [Fact]
    public async Task TimingTree_IndependentReturnedPhasesKeepExactFactoryLinks()
    {
        var fixture = new Fixture(_output, nameof(TimingTree_IndependentReturnedPhasesKeepExactFactoryLinks),
            passiveTimingEvents: true, gzipRecords: true);
        await using (fixture) fixture.CaptureIndependentReturnedPhases();
        fixture.AssertIndependentReturnedPhaseTree();
    }

    /// <summary>Refuses unrelated actual crossed function scopes and their unavailable-identity legacy projection.</summary>
    /// <returns>Completion after all real source scopes and normal fixture closing.</returns>
    [Fact]
    public async Task TimingTree_ActualFunctionCrossingAndLegacyCrossingStillRefuse()
    {
        var fixture = new Fixture(_output, nameof(TimingTree_ActualFunctionCrossingAndLegacyCrossingStillRefuse),
            passiveTimingEvents: true, gzipRecords: true);
        await using (fixture) fixture.CaptureActualFunctionCrossing();
        fixture.AssertActualFunctionCrossingRefused();
    }

    /// <summary>Refuses a still-open actual returned lifetime and always disposes it before diagnostic closing.</summary>
    /// <returns>Completion after the temporary refusal and successful final source/fixture closing.</returns>
    [Fact]
    public async Task TimingTree_IncompleteActualLifetimeRefusesBeforeGuaranteedDispose()
    {
        var fixture = new Fixture(_output, nameof(TimingTree_IncompleteActualLifetimeRefusesBeforeGuaranteedDispose),
            passiveTimingEvents: true, gzipRecords: true);
        await using (fixture) fixture.AssertIncompleteActualLifetimeRefused();
        fixture.AssertCompletedReturnedPhaseAfterIncompleteProbe();
    }

    /// <summary>Validates sliced real IDs and original all-zero legacy semantics while refusing finite corrupted detached DATA.</summary>
    /// <returns>Completion after one tiny actual capture, its closing and all independent reader-DATA checks.</returns>
    [Fact]
    public async Task TimingTree_SlicedIdsLegacyAndMalformedDetachedDataStayStrict()
    {
        var fixture = new Fixture(_output, nameof(TimingTree_SlicedIdsLegacyAndMalformedDetachedDataStayStrict),
            passiveTimingEvents: true, gzipRecords: true);
        await using (fixture)
        {
            fixture.CaptureActualOrdinaryPhaseForTree();
            fixture.CaptureActualNestedFunctionData();
        }
        fixture.AssertSlicedLegacyAndMalformedTreeData();
    }

    private sealed partial class Fixture
    {
        /// <summary>Requires the actual no-runtime/lease/issuer/semantic-owner contour of these file-only controls.</summary>
        private void RequireTreeControlHasNoAuthority()
        {
            Assert.Null(_runtime); Assert.Null(_lease); Assert.Null(_jointBundle);
            Assert.Null(_jointContinuation); Assert.Null(_jointReservation); Assert.Null(Files.GetCurrentSemanticDataOperation());
        }

        /// <summary>Creates two real phases through the actual factory and disposes the first while the second remains live.</summary>
        internal void CaptureIndependentReturnedPhases()
        {
            RequireTreeControlHasNoAuthority();
            using (Files.EnterOperationTimingContext())
            {
                var first = Files.BeginOrdinaryReadPathPhase(); Assert.NotNull(first);
                try
                {
                    var second = Files.BeginOrdinaryReadPathPhase(); Assert.NotNull(second);
                    try
                    {
                        Assert.Same(first, first.Active); Assert.Same(second, second.Active);
                        first.Dispose(); Assert.Null(first.Active); Assert.Same(second, second.Active);
                    }
                    finally { second.Dispose(); }
                }
                finally { first.Dispose(); }
            }
            Assert.Equal(8, CaptureCopyPhaseBoundary());
        }

        /// <summary>Checks both complete factory identities and the actual non-LIFO lifetime closure order.</summary>
        internal void AssertIndependentReturnedPhaseTree()
        {
            var result = ReadAdmissionTimingTree(_observations.ToArray());
            Assert.True(result.SourceScopeIdPairingAvailable); Assert.Equal(8, result.RowsConsumed);
            Assert.Equal(2, result.Functions.Count); Assert.Equal(2, result.Lifetimes.Count);
            var first = result.Lifetimes[0]; var second = result.Lifetimes[1];
            Assert.NotEqual(first.SourceScopeId, second.SourceScopeId);
            Assert.NotEqual(first.FactoryScopeId, second.FactoryScopeId);
            Assert.True(first.BeginOrdinal < second.BeginOrdinal && first.EndOrdinal < second.EndOrdinal);
            Assert.All(result.Lifetimes, lifetime => Assert.True(lifetime.BeginOrdinal < lifetime.FactoryEndOrdinal &&
                lifetime.FactoryEndOrdinal < lifetime.EndOrdinal));
            WriteTreeControlReceipt(nameof(TimingTree_IndependentReturnedPhasesKeepExactFactoryLinks), ["ActualIndependentLifetimes"]);
        }

        /// <summary>Records a real pair of test-labelled function scopes that deliberately violates lexical disposal order.</summary>
        internal void CaptureActualFunctionCrossing()
        {
            RequireTreeControlHasNoAuthority();
            using (Files.EnterOperationTimingContext())
            {
                using var outer = Files.MeasureOperation("T608.Tree.FunctionOuter"); Assert.NotNull(outer);
                using var inner = Files.MeasureOperation("T608.Tree.FunctionInner"); Assert.NotNull(inner);
                outer.Dispose(); inner.Dispose();
            }
            Assert.Equal(4, CaptureCopyPhaseBoundary());
        }

        /// <summary>Preserves rejection of both original recorded crossing and its explicitly unavailable-identity DATA view.</summary>
        internal void AssertActualFunctionCrossingRefused()
        {
            var rows = _observations.ToArray();
            Assert.ThrowsAny<XunitException>(() => ReadAdmissionTimingTree(rows));
            var legacy = rows.Select(row => (row.Name, row.Boundary, row.Milliseconds, SourceScopeId: 0L)).ToArray();
            Assert.ThrowsAny<XunitException>(() => ReadAdmissionTimingTree(legacy));
            WriteTreeControlReceipt(nameof(TimingTree_ActualFunctionCrossingAndLegacyCrossingStillRefuse), ["ActualFunctionCrossing", "LegacyCrossing"]);
        }

        /// <summary>Reads the real incomplete prefix while its object is active and preserves guaranteed closing afterward.</summary>
        internal void AssertIncompleteActualLifetimeRefused()
        {
            RequireTreeControlHasNoAuthority();
            using (Files.EnterOperationTimingContext())
            {
                var phase = Files.BeginOrdinaryReadPathPhase(); Assert.NotNull(phase);
                try
                {
                    Assert.Same(phase, phase.Active);
                    Assert.Equal(3, CaptureCopyPhaseBoundary());
                    Assert.ThrowsAny<XunitException>(() => ReadAdmissionTimingTree(_observations.ToArray()));
                }
                finally { phase.Dispose(); }
                Assert.True(phase.CaptureState().Closed);
            }
            Assert.Equal(4, CaptureCopyPhaseBoundary());
        }

        /// <summary>Requires that incomplete-prefix refusal did not alter the later complete original source DATA.</summary>
        internal void AssertCompletedReturnedPhaseAfterIncompleteProbe()
        {
            var result = ReadAdmissionTimingTree(_observations.ToArray());
            Assert.Single(result.Functions); Assert.Single(result.Lifetimes);
            Assert.True(result.SourceScopeIdPairingAvailable); Assert.Equal(4, result.RowsConsumed);
            WriteTreeControlReceipt(nameof(TimingTree_IncompleteActualLifetimeRefusesBeforeGuaranteedDispose), ["ActualIncompleteRefusal", "ActualFinallyClosed"]);
        }

        /// <summary>Records real nested functions and an independent complete sibling whose IDs remain in full diagnostics.</summary>
        internal void CaptureActualNestedFunctionData()
        {
            RequireTreeControlHasNoAuthority();
            using (Files.EnterOperationTimingContext())
            using (Files.MeasureOperation("T608.Tree.NestedOuter"))
            {
                using (Files.MeasureOperation("T608.Tree.CompleteSibling")) { }
                using (Files.MeasureOperation("T608.Tree.NestedInner")) { }
            }
            Assert.Equal(10, CaptureCopyPhaseBoundary());
        }

        /// <summary>Checks finite modified reader DATA without emitting fabricated or altered NamedEvents into any recorder.</summary>
        internal void AssertSlicedLegacyAndMalformedTreeData()
        {
            var all = _observations.ToArray(); Assert.Equal(10, all.Length);
            var phase = all.Take(4).ToArray();
            var sliced = all.Skip(4).Where(row => row.Name != "T608.Tree.CompleteSibling").ToArray();
            Assert.Equal(4, sliced.Length); Assert.True(sliced[0].SourceScopeId > 1);
            Assert.True(sliced[1].SourceScopeId > sliced[0].SourceScopeId + 1);
            var recorded = ReadAdmissionTimingTree(sliced); Assert.True(recorded.SourceScopeIdPairingAvailable);
            Assert.Equal(2, recorded.Functions.Count); Assert.Empty(recorded.Lifetimes); Assert.Equal(4, recorded.RowsConsumed);
            var legacy = sliced.Select(row => (row.Name, row.Boundary, row.Milliseconds, SourceScopeId: 0L)).ToArray();
            var legacyResult = ReadAdmissionTimingTree(legacy); Assert.False(legacyResult.SourceScopeIdPairingAvailable);
            Assert.Equal(2, legacyResult.Functions.Count); Assert.Empty(legacyResult.Lifetimes);
            var changes = new List<(string Name, (string Name, string Boundary, double Milliseconds, long SourceScopeId)[] Rows)>();
            var mixed = phase.ToArray(); mixed[0].SourceScopeId = 0; changes.Add(("MixedIds", mixed));
            var negative = phase.ToArray(); negative[0].SourceScopeId = -1; changes.Add(("NegativeId", negative));
            var repeated = phase.ToArray(); repeated[1].SourceScopeId = repeated[0].SourceScopeId; changes.Add(("RepeatedBeginId", repeated));
            var foreignEnd = phase.ToArray(); foreignEnd[2].SourceScopeId = phase[1].SourceScopeId; changes.Add(("ForeignEndId", foreignEnd));
            var wrongName = phase.ToArray(); wrongName[2].Name = "T608.Tree.WrongDetachedName"; changes.Add(("WrongEndName", wrongName));
            changes.Add(("AbsentFactory", [phase[1], phase[3]]));
            changes.Add(("EarlyLifetimeClose", [phase[0], phase[1], phase[3], phase[2]]));
            changes.Add(("OrphanLifetimeEnd", [phase[0], phase[2], phase[3]]));
            changes.Add(("MissingLifetimeEnd", phase.Take(3).ToArray()));
            foreach (var change in changes) Assert.ThrowsAny<XunitException>(() => ReadAdmissionTimingTree(change.Rows));
            WriteTreeControlReceipt(nameof(TimingTree_SlicedIdsLegacyAndMalformedDetachedDataStayStrict),
                new[] { "SlicedActualIdsNotFromOneOrContiguous", "LegacyAllZeroNestedSuccess" }.Concat(changes.Select(change => change.Name)).ToArray());
        }

        /// <summary>Writes only a post-close reader-control receipt beside retained raw, without touching the closed writer.</summary>
        /// <param name="control">The actual executed control method.</param>
        /// <param name="checks">Every finite checked DATA contour; this is not a product authority claim.</param>
        private void WriteTreeControlReceipt(string control, string[] checks) =>
            File.WriteAllText(Path.Combine(_output.DiagnosticDirectory, "tree-controls-oracle.json"), JsonSerializer.Serialize(new
            {
                Kind = "T608TimingTreeControls", Control = control, Checks = checks,
                OriginalCapturedRows = _observations.Count, ActualGenuineAuthorityClaim = false
            }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
