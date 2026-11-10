using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class WoundTargetSelectionTests
{
    /// <summary>One existing source read per locality root feeds navigation; a changed profile is freshly refused.</summary>
    [Fact]
    public async Task TargetNavigationSharesPreparedSourcesAndRechecksChangedProfile()
    {
        var reads = new CanonicalReadActivityAccumulator();
        await using var fixture = await CreateTargetFixtureAsync(reads: reads);
        var paths = new[] { AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0], GuardianAbodeResidentState.StatePath };
        var initial = paths.Select(reads.ReadCount).ToArray();
        var opened = await WoundApplicationService.OpenAsync(fixture.Files, "/лечить", treatment: true);
        var state = Assert.IsType<WoundCommandState>(opened.WoundCommandState);
        var target = Assert.Single(state.Targets, t => t.Owner.OwnerKind == "guardian");
        for (var n = 0; n < paths.Length; n++) Assert.Equal(1L, reads.ReadCount(paths[n]) - initial[n]);
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            var root = JsonNode.Parse((await fixture.Files.ReadFileAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!)!;
            root["profiles"]![1]!["displayName"] = "Другое известное имя";
            await fixture.Files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, root.ToJsonString());
        }
        var before = fixture.Snapshot();
        var nextCounts = paths.Select(reads.ReadCount).ToArray();
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            await Assert.ThrowsAsync<InvalidDataException>(() => WoundApplicationService.SelectTargetAsync(fixture.Files, lease, state, target.Value, "/лечить"));
        for (var n = 0; n < paths.Length; n++) Assert.Equal(1L, reads.ReadCount(paths[n]) - nextCounts[n]);
        Assert.Equal(before, fixture.Snapshot());
        output.WriteLine("Actual passive canonical source events: each locality root once per invocation; changed profile freshly refused, no action/queue. No all-filesystem or speedup claim.");
    }
}
