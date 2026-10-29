using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeHealingServiceRequestTests
{
    [Fact]
    public void PublicServiceSelectionRoundTripsExactQuoteWithoutPrivateAidGrant()
    {
        var quote = AfterlifeHealingServiceTestData.Quote();
        var root = new JsonObject
        {
            ["schemaVersion"] = 7, ["mode"] = "service", ["woundId"] = quote.WoundId,
            ["expectedWoundFingerprint"] = quote.WoundFingerprint, ["quote"] = quote.ToJson(),
            ["confirmation"] = "required"
        };
        var parsed = SpiritualSelfTreatmentSelection.Parse(root);
        Assert.True(JsonNode.DeepEquals(root, parsed.ToJson()));
        Assert.Null(parsed.Aid);
        Assert.Null(parsed.Combat);
        Assert.Null(root["participationFingerprint"]);
    }
}
