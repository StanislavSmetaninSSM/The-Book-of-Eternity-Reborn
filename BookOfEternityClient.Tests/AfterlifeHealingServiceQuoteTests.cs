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

    [Theory]
    [InlineData(1, 50, 13)] [InlineData(1, 100, 25)] [InlineData(1, 200, 50)]
    [InlineData(2, 50, 25)] [InlineData(2, 100, 50)] [InlineData(2, 200, 100)]
    [InlineData(3, 50, 50)] [InlineData(3, 100, 100)] [InlineData(3, 200, 200)]
    [InlineData(4, 50, 100)] [InlineData(4, 100, 200)] [InlineData(4, 200, 400)]
    [InlineData(1, 51, 13)] [InlineData(2, 51, 26)] [InlineData(3, 51, 51)] [InlineData(4, 51, 102)]
    public void ExactCanonicalPrices(int rank, int percent, int expected) =>
        Assert.Equal(expected, AfterlifeHealingServiceContract.CalculatePrice(rank, percent));

    [Theory]
    [InlineData(0, 100)] [InlineData(5, 100)] [InlineData(-1, 100)] [InlineData(int.MaxValue, 100)]
    [InlineData(1, 49)] [InlineData(1, 201)] [InlineData(1, -1)] [InlineData(1, int.MaxValue)]
    public void InvalidPriceDomainDoesNotProduceAQuote(int rank, int percent) =>
        Assert.Null(AfterlifeHealingServiceContract.CalculatePrice(rank, percent));
}
