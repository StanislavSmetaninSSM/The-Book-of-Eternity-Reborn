using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerExecutionAuthorityTests
{
    private static readonly GmWorkerExecutionIdentity Identity = new("fixture-original-run",
        GmWorkerBackend.NativeLineage, GmWorkerBackendSelector.NativeGuarantee);
    private static GmWorkerStopEvidence Stopped => new(Identity.RunId, Identity.Backend, Identity.Guarantee,
        GmWorkerStopState.StoppedWithinScope, "reaped-echild", true, false, null);
    private static GmWorkerExecutionAuthority Authority() => new(Identity, GmWorkerBridgeTestFixtures.AnalysisTask());

    [Fact]
    public void MatchingScopedStop_IsAcceptedAsCleanupEvidence()
    {
        Assert.True(Authority().ObserveStop(Stopped));
    }

    [Theory]
    [InlineData("wrong-run")]
    [InlineData("empty-run")]
    [InlineData("wrong-backend")]
    [InlineData("unknown-backend")]
    [InlineData("wrong-scope")]
    [InlineData("empty-scope")]
    [InlineData("unknown-state")]
    [InlineData("not-cleaned")]
    [InlineData("still-authoritative")]
    public void MismatchedOrMalformedStop_CannotBeUsedOrRevivedByLaterMatchingRecord(string fault)
    {
        var authority = Authority();
        var bad = fault switch
        {
            "wrong-run" => Stopped with { RunId = "another-execution" },
            "empty-run" => Stopped with { RunId = "" },
            "wrong-backend" => Stopped with { Backend = GmWorkerBackend.WindowsJob },
            "unknown-backend" => Stopped with { Backend = (GmWorkerBackend)99 },
            "wrong-scope" => Stopped with { Guarantee = "windows-job" },
            "empty-scope" => Stopped with { Guarantee = "" },
            "unknown-state" => Stopped with { State = (GmWorkerStopState)99 },
            "not-cleaned" => Stopped with { CleanupComplete = false },
            "still-authoritative" => Stopped with { AuthorityRetained = true },
            _ => throw new ArgumentException(fault)
        };
        Assert.False(authority.ObserveStop(bad));
        Assert.False(authority.ObserveStop(Stopped));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitUncertain_RemainsStickyAfterLatePhysicalCleanup(bool cleaned)
    {
        var authority = Authority();
        Assert.False(authority.ObserveStop(Stopped with
        {
            State = GmWorkerStopState.Uncertain, CleanupComplete = cleaned, AuthorityRetained = !cleaned
        }));
        Assert.False(authority.ObserveStop(Stopped));
    }

    [Fact]
    public void OutputObservationFailure_RevokesPreviouslyValidatedStop()
    {
        var authority = Authority();
        Assert.True(authority.ObserveStop(Stopped));
        authority.ObserveUncertainty("output-observation-timeout");
        Assert.False(authority.ObserveStop(Stopped));
    }
}
