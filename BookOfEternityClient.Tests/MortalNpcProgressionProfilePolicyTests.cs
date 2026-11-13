using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalNpcProgressionProfilePolicyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CanonicalActor_ProgressingProfileRequiresExplicitValidConsequences(bool malformed)
    {
        var actor = JsonNode.Parse("""
            {"level":1,"experience":0,"experienceForNextLevel":100,
             "progressionProfile":{"schemaVersion":1,"mode":"standard"}}
            """)!.AsObject();
        if (malformed)
            actor["progressionProfile"]!["consequences"] = JsonNode.Parse("""
                {"skills":{"levelsPerAward":3,"allowedAwards":["newSkill","masteryIncrease"]},
                 "characteristics":{"mode":"currentWorld"}}
                """);
        var result = MortalNpcProgressionContract.ValidateCanonicalActor(actor);
        Assert.False(result.IsValid);
        Assert.Contains(result.Problems, problem => problem.Field.StartsWith("progressionProfile.consequences", StringComparison.Ordinal));
    }
}
