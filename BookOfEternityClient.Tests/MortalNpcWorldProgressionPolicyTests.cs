using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalNpcWorldProgressionPolicyTests
{
    private const string None = """{"npcProgression":{"schemaVersion":1,"characteristics":{"mode":"none"}}}""";
    private const string Fixed = """{"npcProgression":{"schemaVersion":1,"characteristics":{"mode":"fixedIncrements","perLevel":{"strength":1}}}}""";

    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("""{"npcProgression":{},"npcProgression":{"schemaVersion":1,"characteristics":{"mode":"none"}}}""")]
    [InlineData("""{"npcProgression":{"schemaVersion":1,"characteristics":{"mode":"fixedIncrements","perLevel":{"strength":1,"strength":2}}}}""")]
    public void Parse_MalformedDocumentOrDuplicateOwnedAuthorityFailsClosed(string text)
    {
        var result = MortalNpcWorldProgressionPolicy.Parse(text);

        Assert.False(result.IsValid);
        Assert.Null(result.Value);
        Assert.Contains(result.Problems, problem => problem.Code == "npc_progression_consequence_policy_invalid");
    }

    [Fact]
    public void Parse_ConfusableOwnedRootKeyCannotBecomeMissingPolicy()
    {
        // Cyrillic small o in Prоgression, not the canonical ASCII property.
        const string key = "npcPr\u043egression";
        Assert.Equal(MortalLocationIdentityState.BuildConfusableKey("npcProgression"),
            MortalLocationIdentityState.BuildConfusableKey(key));
        var root = new JsonObject { [key] = JsonNode.Parse(None)!["npcProgression"]!.DeepClone() };

        var result = MortalNpcWorldProgressionPolicy.Parse(root.ToJsonString());

        Assert.False(result.IsValid);
        Assert.Null(result.Value);
        Assert.Contains(result.Problems, problem => problem.Code == "npc_progression_consequence_policy_invalid");
    }

    [Fact]
    public void Fingerprint_MissingExplicitNoneAndUnrelatedProseHaveSameSemanticIdentity()
    {
        var absent = Authority(null, "strength");
        var explicitNone = Authority(None, "strength");
        var unrelated = Authority("""{"worldName":"Archive","description":"No mechanical policy here"}""", "strength");
        var withProse = JsonNode.Parse(None)!.AsObject();
        withProse["description"] = "Changed narrative, unchanged NPC mechanics";

        foreach (var authority in new[] { absent, explicitNone, unrelated, Authority(withProse.ToJsonString(), "strength") })
        {
            Assert.True(authority.NpcCharacteristicPolicy.IsValid);
            Assert.Equal(MortalNpcProgressionConsequences.CharacteristicMode.None, authority.NpcCharacteristicPolicy.Value!.Mode);
            Assert.Equal(MortalNpcWorldProgressionPolicy.Fingerprint(absent), MortalNpcWorldProgressionPolicy.Fingerprint(authority));
        }
    }

    [Fact]
    public void Fingerprint_BindsPolicyAndAllowedKeysButNotSetInsertionOrder()
    {
        var baseline = Authority(None, "strength", "endurance");
        var fingerprint = MortalNpcWorldProgressionPolicy.Fingerprint(baseline);

        Assert.Equal(fingerprint, MortalNpcWorldProgressionPolicy.Fingerprint(Authority(None, "endurance", "strength")));
        Assert.NotEqual(fingerprint, MortalNpcWorldProgressionPolicy.Fingerprint(Authority(Fixed, "strength", "endurance")));
        Assert.NotEqual(fingerprint, MortalNpcWorldProgressionPolicy.Fingerprint(Authority(None, "strength")));
        Assert.NotEqual(fingerprint, MortalNpcWorldProgressionPolicy.Fingerprint(Authority("""{"npcProgression":null}""", "strength", "endurance")));
        baseline.WorldCharacteristicKeys.Remove("endurance");
        Assert.NotEqual(fingerprint, MortalNpcWorldProgressionPolicy.Fingerprint(baseline));
    }

    [Fact]
    public void Parse_DetachesOwnedPolicyFromCallerJsonAndFreshParsingObservesMutation()
    {
        var caller = JsonNode.Parse(Fixed)!.AsObject();
        var original = caller.ToJsonString();
        var accepted = Authority(original, "strength");
        Assert.True(accepted.NpcCharacteristicPolicy.IsValid);
        Assert.Equal(original, caller.ToJsonString());
        var fingerprint = MortalNpcWorldProgressionPolicy.Fingerprint(accepted);

        caller["npcProgression"]!["characteristics"]!["perLevel"]!["strength"] = 99;

        Assert.Equal(1, accepted.NpcCharacteristicPolicy.Value!.PerLevel!["strength"]);
        Assert.Equal(fingerprint, MortalNpcWorldProgressionPolicy.Fingerprint(accepted));
        var changed = Authority(caller.ToJsonString(), "strength");
        Assert.True(changed.NpcCharacteristicPolicy.IsValid);
        Assert.Equal(99, changed.NpcCharacteristicPolicy.Value!.PerLevel!["strength"]);
        Assert.NotEqual(fingerprint, MortalNpcWorldProgressionPolicy.Fingerprint(changed));
    }

    private static NpcCoreChangesContract.Authority Authority(string? text, params string[] keys) => new(
        new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(keys, StringComparer.Ordinal))
    {
        NpcCharacteristicPolicy = MortalNpcWorldProgressionPolicy.Parse(text)
    };
}
