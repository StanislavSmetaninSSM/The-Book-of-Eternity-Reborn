using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GuardianDomainDisplayTests
{
    [Theory]
    [InlineData("Combat", "Боевой домен")]
    [InlineData("Magic", "Магический домен")]
    [InlineData("Trade", "Торговый домен")]
    [InlineData("Social", "Социальный домен")]
    [InlineData("Crafting", "Ремесленный домен")]
    [InlineData("Survival", "Домен выживания")]
    [InlineData("Knowledge", "Домен знания")]
    [InlineData("Memory", "Домен памяти")]
    [InlineData("Passage", "Домен переходов")]
    [InlineData("Protection", "Домен защиты")]
    [InlineData("Archive", "Архивный домен")]
    public void ForPlayer_LocalizesKnownGuardianDomain(string domain, string expected)
    {
        Assert.Equal(expected, GuardianDomainDisplay.ForPlayer(domain));
    }
}
