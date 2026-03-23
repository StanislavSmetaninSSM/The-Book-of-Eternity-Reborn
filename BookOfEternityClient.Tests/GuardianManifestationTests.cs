using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GuardianManifestationTests
{
    [Fact]
    public void GetDisplayName_PrefersCurrentManifestationDisplayName()
    {
        using var doc = JsonDocument.Parse("""
        {
          "guardianId": "guard_social_azalia_001",
          "canonicalName": "Азалия",
          "manifestation": {
            "currentDisplayName": "Азалий",
            "formFlexibility": "selective",
            "currentPresentationStyle": "masculine",
            "currentPronouns": "он/его",
            "appearanceDescription": "Тестовая форма."
          }
        }
        """);

        var displayName = GuardianManifestation.GetDisplayName(doc.RootElement);

        Assert.Equal("Азалий", displayName);
    }

    [Fact]
    public void GetDisplayName_ForJsonObject_UsesCanonicalNameWhenManifestationIsMissing()
    {
        var guardian = JsonNode.Parse("""
        {
          "guardianId": "guard_social_azalia_001",
          "canonicalName": "Азалия"
        }
        """)!.AsObject();

        var displayName = GuardianManifestation.GetDisplayName(guardian);

        Assert.Equal("Азалия", displayName);
    }

    [Theory]
    [InlineData("fixed", "Постоянная форма")]
    [InlineData("selective", "Избирательная смена формы")]
    [InlineData("adaptive", "Адаптивная смена формы")]
    public void GetFormFlexibilityLabel_ReturnsLocalizedLabel(string raw, string expected)
    {
        Assert.Equal(expected, GuardianManifestation.GetFormFlexibilityLabel(raw));
    }

    [Theory]
    [InlineData("feminine", "Женская подача")]
    [InlineData("masculine", "Мужская подача")]
    [InlineData("neutral", "Нейтральная подача")]
    public void GetPresentationStyleLabel_ReturnsLocalizedLabel(string raw, string expected)
    {
        Assert.Equal(expected, GuardianManifestation.GetPresentationStyleLabel(raw));
    }
}
