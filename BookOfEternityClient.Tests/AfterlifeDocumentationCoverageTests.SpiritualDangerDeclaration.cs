using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeDocumentationCoverageTests
{
    [Fact]
    public void SpiritualDangerDeclarationDocumentation_RequiresExactModesAndPersistedExample()
    {
        const string declaration =
            "The spiritual conflict danger declaration is mandatory: put `dangerMode` in the selected " +
            "`conflictState`/`activeConflict`/`conflictSeed` of `mode=start`, canonical `activeConflict`, and every " +
            "`recentConflicts[]` proof. Use exactly `training`, `controlled`, `hostile`, or `annihilation`: " +
            "lowercase JSON strings without surrounding whitespace. There is no implicit mode or old-save fallback. " +
            "Ordinary exchanges and partial `activeConflictAfter`/`conflictStateAfter` replacements may omit the " +
            "field and preserve the accepted declaration; explicit echoes must match exactly, including in the " +
            "update root and exchange `before`/`after`. `resolve` and `repair_cancel` copy that declaration into " +
            "the terminal proof and cannot replace it. During a validated turn, every retained same-ID " +
            "active/recent occurrence is compared with the signed pre-turn declarations; a missing, invalid or " +
            "conflicting baseline fails closed, and a later duplicate cannot hide a change. Legal removal from the " +
            "bounded recent-history window is unchanged. Do not infer escalation after a roll or submit a boolean " +
            "as escalation authority. This declaration/persistence stage does not implement accepted escalation, " +
            "wound production or healing. The declared wound ceilings remain training 0, controlled II, " +
            "hostile/annihilation IV; wounds are optional GM choices within validated limits, and soul dissipation " +
            "remains separately authorized and always optional.";
        foreach (var text in new[]
        {
            ReadRepoFile("TaskGuides", "CLI_Step_Main.txt"),
            ReadRepoFile("CLI_API_Specification.md"),
            ReadRepoFile("CLI_Agent_Daemon_Specification.md"),
            ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md"),
            ReadRepoFile("OtherGuides", "Afterlife_Combat_Terminology_Glossary.md"),
            ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt")
        })
            Assert.Contains(declaration, text, StringComparison.Ordinal);

        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        Assert.Contains("Danger declaration worked result:", examples, StringComparison.Ordinal);
        Assert.Contains("not a wound opportunity or permission to dissipate a soul", examples, StringComparison.Ordinal);
        using var manifest = JsonDocument.Parse(ReadRepoFile("Examples", "example_validation_manifest.json"));
        var scenario = Assert.Single(
            manifest.RootElement.GetProperty("runtimeScenarios").EnumerateArray(),
            item => item.GetProperty("id").GetString() == "afterlife_spiritual_conflict_start_response");
        Assert.Equal("gameResponseDistribution", scenario.GetProperty("runner").GetString());
        Assert.Contains(scenario.GetProperty("requiredText").EnumerateArray(),
            token => token.GetString() == "\"dangerMode\": \"hostile\"");
        var persisted = Assert.Single(scenario.GetProperty("expectedFileContains").EnumerateArray(),
            item => item.GetProperty("path").GetString() == "game_state/meta/afterlife_spiritual_conflict_state.json");
        Assert.Contains(persisted.GetProperty("requiredText").EnumerateArray(),
            token => token.GetString() == "\"dangerMode\": \"hostile\"");
    }
}
