using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MathAssistantPromptGuidanceTests
{
    [Fact]
    public void LiveGmPrompt_ContainsMathAssistantRule()
    {
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var turnLifecycle = ReadRepoFile("BookOfEternityClient", "Core", "GameEngine", "GameEngine.TurnLifecycle.cs");

        foreach (var text in new[] { daemonSpec, turnLifecycle })
        {
            Assert.Contains("MATH ASSISTANT / МАТЕМАТИК", text, StringComparison.Ordinal);
            Assert.Contains("mathRequests[]", text, StringComparison.Ordinal);
            Assert.Contains("mathAudit[]", text, StringComparison.Ordinal);
            Assert.Contains("formulaVersion = math_assistant_v1", text, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("Examples/E_Block_6.txt", "calc_mortal_combat_damage_example")]
    [InlineData("Examples/E_CLI_Afterlife_Turns.txt", "calc_afterlife_conflict_margin_example")]
    [InlineData("Examples/E_Block_10.txt", "calc_economy_price_example")]
    [InlineData("Examples/E_CLI_Afterlife_Turns.txt", "calc_shining_treasury_interest_example")]
    [InlineData("Examples/E_Block_32.txt", "calc_guardian_project_progress_example")]
    public void GmFacingExamples_ShowValidMathAuditForMajorCalculationSurfaces(
        string relativePath,
        string expectedRequestId)
    {
        var text = ReadRepoFile(relativePath.Split('/'));

        Assert.Contains(expectedRequestId, text, StringComparison.Ordinal);
        Assert.Contains("\"mathAudit\"", text, StringComparison.Ordinal);
        Assert.Contains("\"formulaVersion\": \"math_assistant_v1\"", text, StringComparison.Ordinal);
        Assert.Contains("\"applicationState\": \"applied_to_state\"", text, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(params string[] segments)
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, Path.Combine(segments));
        return File.ReadAllText(path);
    }
}
