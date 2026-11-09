using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Removing an already captured second parent is physical freshness failure, not a semantic join control.</summary>
    [Fact]
    public Task SpiritualPressureGroup_WarmSecondParentDeletionRevokes() =>
        WithPressureGroupCaseAsync(new("group_delete_second"), async window =>
        {
            AssertPressureGroupComparison(window,
                await window.Capture.ReadExistingPressureOrdinaryComparisonAsync(window.Lease));
            AssertPressureGroupComparison(window,
                await window.Capture.ReadExistingPressureOrdinaryComparisonAsync(window.Lease));
            await AssertPressureGroupWindowInertAsync(window);
            var counts = window.Capture.ReadExistingStrainReuseCounts();
            var path = AfterlifeEntityProfileState.StatePath;
            var oldBytes = await window.Copy.Context.FileSystem.ReadFileBytesAsync(window.Lease, path);
            var profiles = JsonNode.Parse(oldBytes!)!.AsObject();
            var player = Assert.Single(profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray(), row =>
                row!["actorType"]!.GetValue<string>() == "player_soul" && row["actorId"]!.GetValue<string>() == "player_soul")!;
            var wounds = player["activeWounds"]!.AsArray();
            Assert.Equal(2, wounds.Count);
            Assert.Equal("wound_retained_pressure_owner_2", wounds[1]!["woundId"]!.GetValue<string>());
            wounds.RemoveAt(1);
            var changed = Encoding.UTF8.GetBytes(profiles.ToJsonString());
            Assert.False(oldBytes!.AsSpan().SequenceEqual(changed));
            await window.Copy.Context.FileSystem.WriteFileAtomicBytesAsync(window.Lease, path, changed);
            var afterIntentionalDeletion = CaptureExactSpiritualFiles(window.Copy.Context.FileSystem.GameSessionPath);
            var refused = await window.Capture.ReadExistingPressureOrdinaryComparisonAsync(window.Lease);
            Assert.Null(refused.Comparison); Assert.False(refused.PrefixBlocked);
            var issue = Assert.Single(refused.Issues);
            Assert.Equal("spiritual_original_input_changed", issue.Code);
            Assert.Equal(path, issue.FilePath);
            Assert.Equal(IssueSeverity.Error, issue.Severity);
            Assert.False(window.Capture.IsCurrentOwner);
            var afterCounts = window.Capture.ReadExistingStrainReuseCounts();
            Assert.Equal(counts.OrdinaryEvaluations, afterCounts.OrdinaryEvaluations);
            Assert.Equal(counts.ActualEvaluations, afterCounts.ActualEvaluations);
            Assert.Equal(counts.SignedParentReads, afterCounts.SignedParentReads);
            AssertExistingPressureInputInert(window.Copy.Context, window.Lease, afterIntentionalDeletion, window.Generation);
            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await window.Capture.ReadExistingPressureOrdinaryProbeStateAsync(window.Lease));
            await window.Copy.Context.FileSystem.WriteFileAtomicBytesAsync(window.Lease, path, oldBytes);
            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await window.Capture.ReadExistingPressureOrdinaryComparisonAsync(window.Lease));
            AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
        });

    /// <summary>Actual source acquisition revokes the old source, and its retained warm pair DATA cannot revive it.</summary>
    [Fact]
    public Task SpiritualPressureGroup_WarmSourceRevocationRefuses() =>
        WithPressureGroupCaseAsync(new("group_source_revoke"), async window =>
        {
            AssertPressureGroupComparison(window,
                await window.Capture.ReadExistingPressureOrdinaryComparisonAsync(window.Lease));
            AssertPressureGroupComparison(window,
                await window.Capture.ReadExistingPressureOrdinaryComparisonAsync(window.Lease));
            await AssertPressureGroupWindowInertAsync(window);
            var counts = window.Capture.ReadExistingStrainReuseCounts();
            // This real typed entrypoint invalidates the previous source before any replacement acquisition.
            // Its replacement may accept/refuse independently; neither outcome is old-owner permission.
            var replacement = await window.Copy.Context.Validator.BeginSpiritualWoundSourceSessionAsync(window.Lease);
            try
            {
                Assert.False(window.Capture.IsCurrentOwner);
                var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await window.Capture.ReadExistingPressureOrdinaryComparisonAsync(window.Lease));
                Assert.Equal("The original turn capture was revoked.", error.Message);
                Assert.Equal(counts, window.Capture.ReadExistingStrainReuseCounts());
                AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
                AssertPressureNoPrivatePair(window.Copy.Context, window.Lease);
            }
            finally { replacement.Session?.Revoke(); }
            window.Capture.Dispose();
            AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
        });

    /// <summary>Foreign leases and disposed owners do not acquire comparison authority from another owner's detached DATA.</summary>
    [Fact]
    public Task SpiritualPressureGroup_ForeignLeaseAndDisposedOwnerRefuse() =>
        AssertPressureGroupForeignLeaseAndDisposedOwnerAsync(2);

    /// <summary>Reuses independent real owners without sharing leases or mutable state between complete group cases.</summary>
    /// <param name="parents">Exact independently retained parent count for each of two separate sessions.</param>
    private Task AssertPressureGroupForeignLeaseAndDisposedOwnerAsync(int parents) =>
        WithPressureGroupCaseAsync(new("group_owner_a_" + parents, Parents: parents), async a =>
            await WithPressureGroupCaseAsync(new("group_owner_b_" + parents, Parents: parents), async b =>
            {
                Assert.NotEqual(a.Copy.Generation, b.Copy.Generation);
                var beforeA = a.Capture.ReadExistingStrainReuseCounts();
                var beforeB = b.Capture.ReadExistingStrainReuseCounts();
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await a.Capture.ReadExistingPressureOrdinaryComparisonAsync(b.Lease));
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await b.Capture.ReadExistingPressureOrdinaryComparisonAsync(a.Lease));
                Assert.Equal(beforeA, a.Capture.ReadExistingStrainReuseCounts());
                Assert.Equal(beforeB, b.Capture.ReadExistingStrainReuseCounts());
                AssertPressureGroupComparison(a, await a.Capture.ReadExistingPressureOrdinaryComparisonAsync(a.Lease));
                AssertPressureGroupComparison(b, await b.Capture.ReadExistingPressureOrdinaryComparisonAsync(b.Lease));
                await AssertPressureGroupWindowInertAsync(a);
                await AssertPressureGroupWindowInertAsync(b);
                a.Capture.Dispose();
                Assert.False(a.Capture.IsCurrentOwner);
                await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                    await a.Capture.ReadExistingPressureOrdinaryComparisonAsync(a.Lease));
                AssertExistingPressureInputInert(a.Copy.Context, a.Lease, a.Files, a.Generation);
                Assert.True(b.Capture.IsCurrentOwner);
                var beforeWarmB = b.Capture.ReadExistingStrainReuseCounts();
                AssertPressureGroupComparison(b, await b.Capture.ReadExistingPressureOrdinaryComparisonAsync(b.Lease));
                var afterWarmB = b.Capture.ReadExistingStrainReuseCounts();
                Assert.Equal(beforeWarmB.OrdinaryEvaluations, afterWarmB.OrdinaryEvaluations);
                Assert.Equal(beforeWarmB.ActualEvaluations, afterWarmB.ActualEvaluations);
                Assert.Equal(beforeWarmB.SignedParentReads, afterWarmB.SignedParentReads);
                Assert.True(afterWarmB.FreshInputChecks > beforeWarmB.FreshInputChecks);
                Assert.True(afterWarmB.FreshDependencyChecks > beforeWarmB.FreshDependencyChecks);
                await AssertPressureGroupWindowInertAsync(b);
            }));
}
