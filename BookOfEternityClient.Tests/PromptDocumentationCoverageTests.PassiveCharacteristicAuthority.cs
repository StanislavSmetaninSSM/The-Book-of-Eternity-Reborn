using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PromptDocumentationCoverageTests
{
    [Fact]
    public async Task PassiveCharacteristicAuthority_WorkedExampleUsesStructuredMechanicsOnly()
    {
        foreach (var name in new[] { "Block_8.txt", "Block_12.txt", "Block_14.txt" })
        {
            var text = ReadRepoFile("Rules", name);
            Assert.Contains("Never parse 'playerStatBonus' to grant mechanics", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Legacy Fallback", text, StringComparison.Ordinal);
        }
        var example = ReadRepoFile("Examples", "E_Block_8.txt");
        var marker = example.IndexOf("Worked display-only characteristic authority (#1533 T105)", StringComparison.Ordinal);
        Assert.True(marker >= 0);
        var start = example.IndexOf("<![CDATA[", marker, StringComparison.Ordinal) + "<![CDATA[".Length;
        var end = example.IndexOf("]]>", start, StringComparison.Ordinal);
        var states = JsonNode.Parse(example[start..end])!.AsObject();
        using var fixture = new MortalPassiveSkillCharacteristicAuthorityTests();
        var displayOnly = states["displayOnly"]!.AsObject();
        var structured = states["structured"]!.AsObject();
        MortalPassiveSkillCharacteristicAuthorityTests.AssertValidProfile(displayOnly);
        MortalPassiveSkillCharacteristicAuthorityTests.AssertValidProfile(structured);
        Assert.Equal((0, 0), await fixture.ComputeSkillAsync(displayOnly));
        Assert.Equal((2, 0), await fixture.ComputeSkillAsync(structured));
    }
}
