using BookOfEternityClient.Core;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ReputationDisplayTests
{
    [Theory]
    [InlineData(-100, "Враждебный")]
    [InlineData(-51, "Враждебный")]
    [InlineData(-50, "Недружелюбный")]
    [InlineData(-21, "Недружелюбный")]
    [InlineData(-20, "Нейтральный")]
    [InlineData(49, "Нейтральный")]
    [InlineData(50, "Дружелюбный")]
    [InlineData(129, "Дружелюбный")]
    [InlineData(130, "Преданный")]
    [InlineData(229, "Преданный")]
    [InlineData(230, "Легендарный")]
    [InlineData(300, "Легендарный")]
    public void GuardianScale_ResolvesBoundaryLabels(int reputation, string expectedLabel)
    {
        var tier = ReputationDisplay.GetTier(ReputationScaleKind.Guardian, reputation);

        Assert.Equal(expectedLabel, tier.Label);
    }

    [Theory]
    [InlineData(-400, "Непримиримый Враг")]
    [InlineData(-200, "Противник")]
    [InlineData(-50, "Неприязнь")]
    [InlineData(0, "Нейтралитет")]
    [InlineData(101, "Доверие и Расположение")]
    [InlineData(251, "Глубокая Связь")]
    [InlineData(351, "Легендарная Преданность")]
    public void NpcRelationshipScale_ResolvesBoundaryLabels(int reputation, string expectedLabel)
    {
        var tier = ReputationDisplay.GetTier(ReputationScaleKind.NpcRelationship, reputation);

        Assert.Equal(expectedLabel, tier.Label);
    }

    [Theory]
    [InlineData(-400, "Заклятый враг")]
    [InlineData(-200, "Враг")]
    [InlineData(-50, "Недоверие")]
    [InlineData(0, "Нейтралитет")]
    [InlineData(101, "Сочувствующий")]
    [InlineData(251, "Почётный член")]
    [InlineData(351, "Живая легенда")]
    public void FactionScale_ResolvesBoundaryLabels(int reputation, string expectedLabel)
    {
        var tier = ReputationDisplay.GetTier(ReputationScaleKind.Faction, reputation);

        Assert.Equal(expectedLabel, tier.Label);
    }

    [Theory]
    [InlineData((int)ReputationScaleKind.Guardian, -100, 0)]
    [InlineData((int)ReputationScaleKind.Guardian, 300, 20)]
    [InlineData((int)ReputationScaleKind.NpcRelationship, -400, 0)]
    [InlineData((int)ReputationScaleKind.NpcRelationship, 400, 20)]
    public void Normalize_UsesScaleBoundaries(int scaleValue, int reputation, int expected)
    {
        var scale = (ReputationScaleKind)scaleValue;
        var normalized = ReputationDisplay.Normalize(reputation, scale, 20);

        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void GuardianLegend_UsesCanonicalBands()
    {
        var lines = ReputationDisplay.BuildLegendLines(ReputationScaleKind.Guardian);

        Assert.Collection(lines,
            line => Assert.Contains("-100..-51", line, StringComparison.Ordinal),
            line => Assert.Contains("-50..-21", line, StringComparison.Ordinal),
            line => Assert.Contains("-20..49", line, StringComparison.Ordinal),
            line => Assert.Contains("50..129", line, StringComparison.Ordinal),
            line => Assert.Contains("130..229", line, StringComparison.Ordinal),
            line => Assert.Contains("230..300", line, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0, "Дружелюбный", 50)]
    [InlineData(50, "Преданный", 130)]
    [InlineData(130, "Легендарный", 230)]
    public void GuardianNextThreshold_UsesCanonicalBands(int reputation, string expectedLabel, int expectedThreshold)
    {
        var ok = ReputationDisplay.TryGetNextThreshold(ReputationScaleKind.Guardian, reputation, out var label, out var threshold);

        Assert.True(ok);
        Assert.Equal(expectedLabel, label);
        Assert.Equal(expectedThreshold, threshold);
    }
}
