using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeHealingPaymentLifecycleTests
{
    [Theory]
    [InlineData(1, "NoImprovement")] [InlineData(2, "RecoveryPoint")] [InlineData(20, "ReduceTwo")]
    public Task ShiningSameElyaraChargesActualOutcomeOnceAndReplays(int die, string band) =>
        RunAcceptedServiceAsync(die, band, "Shining Abode");
}
