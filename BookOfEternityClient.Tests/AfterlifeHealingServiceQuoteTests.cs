using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeHealingServiceQuoteTests
{
    [Fact]
    public void QuoteUsesCanonicalPriceWithUpwardRounding()
    {
        Assert.Null(AfterlifeHealingServiceContract.CalculatePrice(0, 100));
        Assert.Null(AfterlifeHealingServiceContract.CalculatePrice(5, 100));
        Assert.Null(AfterlifeHealingServiceContract.CalculatePrice(1, 49));
        Assert.Null(AfterlifeHealingServiceContract.CalculatePrice(1, 201));
        Assert.Equal(13, AfterlifeHealingServiceContract.CalculatePrice(1, 50));
    }
}
