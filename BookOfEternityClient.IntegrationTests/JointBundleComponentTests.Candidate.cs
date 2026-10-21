using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    /// <summary>
    /// Measures an actual joint cache miss and hit after setup, preserving the retained plan,
    /// canonical afterimages, foreign-input refusal and stale-fence invalidation behavior.
    /// </summary>
    [Fact]
    public async Task JointCandidate_MissAndHit_PreservePlanAndRotateFence()
    {
        await using var fixture = new Fixture(_output, nameof(JointCandidate_MissAndHit_PreservePlanAndRotateFence));
        await fixture.InitializeAsync();
        var bound = await fixture.PrepareBoundJointAsync();
        Assert.True(bound.Success, JsonSerializer.Serialize(bound.Issues));
        var input = bound.Input!;
        var authority = bound.Authority!;
        var before = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in input.BeforeImages.Keys)
            before.Add(path, await fixture.Files.ReadFileBytesAsync(fixture.Lease, path));

        var missOffset = fixture.ResetCandidateCapture("Miss");
        AcceptedTurnAuthorityRegistry.JointCommonCandidateResult miss;
        using (fixture.Files.MeasureOperation("JointBundleCandidate.Miss"))
            miss = AcceptedTurnAuthorityRegistry.CreateJointRepairCommonCandidate(fixture.Files, fixture.Lease, input, authority);
        var missCounts = fixture.CompleteCandidateCapture("Miss", missOffset);
        Assert.True(miss.Result.Success, JsonSerializer.Serialize(miss.Result.Issues));
        Assert.NotNull(miss.Fence);
        Assert.Equal(1, missCounts.GetValueOrDefault("JointCommon.AssembleRetainedPlan"));
        Assert.True(missCounts.GetValueOrDefault("WoundBundle.DetachedCopy") > 0);

        var hitOffset = fixture.ResetCandidateCapture("Hit");
        AcceptedTurnAuthorityRegistry.JointCommonCandidateResult hit;
        using (fixture.Files.MeasureOperation("JointBundleCandidate.Hit"))
            hit = AcceptedTurnAuthorityRegistry.CreateJointRepairCommonCandidate(fixture.Files, fixture.Lease, input, authority);
        var hitCounts = fixture.CompleteCandidateCapture("Hit", hitOffset);
        Assert.True(hit.Result.Success, JsonSerializer.Serialize(hit.Result.Issues));
        Assert.NotNull(hit.Fence);
        Assert.Equal(0, hitCounts.GetValueOrDefault("JointCommon.AssembleRetainedPlan"));
        Assert.Same(miss.Result.Plan, hit.Result.Plan);
        Assert.NotSame(miss.Fence!.Fence, hit.Fence!.Fence);
        Assert.Same(miss.Fence.Cache, hit.Fence.Cache);
        Assert.Equal(miss.Result.Plan!.PreparedPlanFingerprint, hit.Result.Plan!.PreparedPlanFingerprint);
        fixture.Output.WriteLine(JsonSerializer.Serialize(new
        {
            Kind = "JointCandidateResults", input.SessionId, input.RequestId, input.SnapshotToken, input.Turn,
            hit.Result.Plan.InputFingerprint, hit.Result.Plan.PreparedPlanFingerprint,
            SamePlan = ReferenceEquals(miss.Result.Plan, hit.Result.Plan), RotatedFence = !ReferenceEquals(miss.Fence.Fence, hit.Fence.Fence),
            DefinitionAfterImage = hit.Result.Plan.DefinitionAfterImage, StateAfterImage = hit.Result.Plan.StateAfterImage,
            HistoryAfterImage = hit.Result.Plan.HistoryAfterImage, PendingAfterImages = hit.Result.Plan.PendingAfterImages,
            OwnerCompanionAfterImages = hit.Result.Plan.OwnerCompanionAfterImages,
            EffectCarrierAfterImages = hit.Result.Plan.EffectCarrierAfterImages, hit.Result.Plan.EffectIdentityAfterImage
        }));

        var foreign = new AcceptedMechanicsInput(input.SessionId, "foreign_request", input.SnapshotToken, input.Realm, input.Turn,
            input.AcceptedEvents, input.ResourceCommands, input.EffectCommands, input.PendingInput, input.InternalInputs,
            input.AuthorityFingerprints, input.BeforeImages, input.ValidationIssues, input.PlanningContext, input.WoundCommands, input.WoundInput);
        var refused = AcceptedTurnAuthorityRegistry.CreateJointRepairCommonCandidate(fixture.Files, fixture.Lease, foreign, authority);
        Assert.False(refused.Result.Success);
        Assert.Null(refused.Fence);
        AcceptedTurnAuthorityRegistry.RevokeJointRepairCommonCandidate(fixture.Files, fixture.Lease, authority, miss.Fence);
        var stillCached = AcceptedTurnAuthorityRegistry.CreateJointRepairCommonCandidate(fixture.Files, fixture.Lease, input, authority);
        Assert.True(stillCached.Result.Success, JsonSerializer.Serialize(stillCached.Result.Issues));
        Assert.Same(hit.Result.Plan, stillCached.Result.Plan);
        foreach (var row in before)
            Assert.Equal(row.Value, await fixture.Files.ReadFileBytesAsync(fixture.Lease, row.Key));
        fixture.Output.WriteLine(JsonSerializer.Serialize(new { Kind = "JointCandidateOracles", ForeignInputRefused = true,
            StaleFenceRevocationInert = true, UnchangedCanonicalPaths = before.Keys.ToArray(), Published = false }));
        await fixture.AssertPreparedBeginningOwnerRefusalsAsync();
    }

    private sealed partial class Fixture
    {
        /// <summary>
        /// Begins a new observation slice without discarding any earlier setup or diagnostic record.
        /// </summary>
        /// <param name="phase">
        /// The literal phase label emitted before entering the synchronous candidate operation.
        /// </param>
        /// <returns>
        /// The first observation index belonging to this slice.
        /// </returns>
        internal int ResetCandidateCapture(string phase)
        {
            var offset = _observations.Count;
            _output.WriteLine(JsonSerializer.Serialize(new { Kind = "CandidateCaptureReset", Phase = phase, Offset = offset }));
            return offset;
        }

        /// <summary>
        /// Reports every operation count and duration from a completed synchronous candidate slice.
        /// </summary>
        /// <param name="phase">
        /// The phase label matching the start of this observation slice.
        /// </param>
        /// <param name="offset">
        /// The first retained observation index returned by <see cref="ResetCandidateCapture"/>.
        /// </param>
        /// <returns>
        /// Actual completed invocation counts by literal operation name; nested durations remain inclusive.
        /// </returns>
        internal Dictionary<string, int> CompleteCandidateCapture(string phase, int offset)
        {
            var rows = _observations.Skip(offset).ToArray();
            var groups = rows.Where(row => row.Boundary == "end").GroupBy(row => row.Name).ToArray();
            var counts = groups.ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            _output.WriteLine(JsonSerializer.Serialize(new
            {
                Kind = "CandidateCaptureCompleted", Phase = phase, Offset = offset, EndOffset = _observations.Count,
                Operations = groups.Select(group => new { Name = group.Key, Count = group.Count(),
                    InclusiveMilliseconds = group.Sum(row => row.Milliseconds) }).ToArray(),
                Observer = "Uncapped AutoFlush JSONL with measured timing-write costs; observer overhead and this small fixture limit attribution to larger runs."
            }));
            return counts;
        }
    }
}
