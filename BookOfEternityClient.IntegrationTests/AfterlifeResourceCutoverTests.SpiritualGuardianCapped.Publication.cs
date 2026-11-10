using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Two positive schema1 capped journeys retain full comparison despite equal roots, then publish once and validate signed47.</summary>
    /// <param name="rank">Complete guardian parent III or IV fixed before original signing.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public Task SpiritualGuardianCapped_PublishesOnceAndValidatesSignedNext(int rank)
    {
        var name = "guardian_capped_normal_" + rank;
        return WithSingletonIvJourneyFailureAsync(name, () => WithGuardianAttributionJourneyAsync(name, async setup =>
        {
            Assert.Equal(rank, setup.ExpectedRank);
            Assert.Equal(CappedControlCalculation(), setup.OrdinaryCalculation);
            Assert.Equal(CappedControlCalculation(), setup.ActualCalculation);
            Assert.True(JsonNode.DeepEquals(setup.Copy.Conflict, setup.ActualRoot));
            var attempt = new GuardianCappedAttemptCapture();
            var completed = await CompleteGuardianAttributionColdC3Async(setup, setup.Copy.Context.Validator, cappedAttempt: attempt);
            Assert.NotNull(attempt.FirstJson); Assert.NotNull(attempt.CompletedJson); Assert.Null(attempt.PreparedJson);
            await PublishGuardianAttributionAndReadNextAsync(setup, setup.Copy.Context.Validator, completed, attempt);
            Assert.NotNull(attempt.PreparedJson);
        }, retainedRank: rank, contour: GuardianSingletonPressureContour.CappedBroken));
    }
}
