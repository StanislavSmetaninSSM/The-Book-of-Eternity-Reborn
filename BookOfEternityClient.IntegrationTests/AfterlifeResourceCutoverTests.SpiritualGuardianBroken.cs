using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Desired real signed/current guardianIII/IV I→ordinaryIII→actualIV comparison and C1 after genuine ordinary cold/warm proof.</summary>
    /// <param name="rank">Independent complete retained parentIII/IV, fixed before signing.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public Task SpiritualGuardianBroken_RealOwnerAdvancesAndComposesC1(int rank) =>
        AssertGuardianSingletonOwnerAsync("guardian_broken_owner_" + rank, rank, GuardianSingletonPressureContour.ReachingBroken);
    /// <summary>Both independent parent graphs traverse actual/C1, first C2, one cold C3, common C4 and signed47.</summary>
    /// <param name="rank">Complete parent III/IV authored before signing.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public Task SpiritualGuardianBroken_PublishesOnceAndValidatesSignedNext(int rank) =>
        WithGuardianAttributionJourneyAsync("guardian_broken_publication_" + rank, async setup =>
        {
            var completed = await CompleteGuardianAttributionColdC3Async(setup, setup.Copy.Context.Validator);
            await PublishGuardianAttributionAndReadNextAsync(setup, setup.Copy.Context.Validator, completed);
        }, retainedRank: rank, contour: GuardianSingletonPressureContour.ReachingBroken);

    /// <summary>Representative IV full late cut restores signed46, retires owners and publishes one fresh same-byte retry through signed47.</summary>
    [Fact]
    public Task SpiritualGuardianBroken_LateCutRestoresAndFreshRetryPublishesOnce() =>
        AssertGuardianAttributionLatePublicationCutAsync("guardian_broken_rollback_4", retainedRank: 4,
            guardianContour: GuardianSingletonPressureContour.ReachingBroken);
}
