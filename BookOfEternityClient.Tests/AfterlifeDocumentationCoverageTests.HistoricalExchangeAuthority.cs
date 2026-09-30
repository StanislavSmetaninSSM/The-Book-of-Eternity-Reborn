using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeDocumentationCoverageTests
{
    [Fact]
    public void HistoricalExchangeAuthorityDocumentation_RequiresSnapshotNotBackdating()
    {
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var mainGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        Assert.DoesNotContain(
            "Do not rewrite accepted historical `exchangeLog[]` entries with `exchangeAtTurn` lower than the current turn",
            mainGuide, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Historical exchange logs from earlier accepted turns (`exchangeAtTurn` lower than the current turn)",
            matrix, StringComparison.Ordinal);
        Assert.Contains("`diceUsed[].sourceIndex` is zero-based", examples, StringComparison.Ordinal);
        Assert.Contains("This example starts from `before.conflictPosition = \"contested\"`", examples, StringComparison.Ordinal);
        Assert.Contains("do not demand that the text begin with `[AFTERLIFE_SPIRITUAL_ACTION: ...]`", examples, StringComparison.Ordinal);
        foreach (var text in new[]
        {
            mainGuide,
            matrix,
            examples
        })
            foreach (var invariant in new[]
            {
                "same validated pre-turn active conflictId",
                "each snapshot occurrence once",
                "exchangeAtTurn alone is not authority",
                "only the top-level summary",
                "missing/null/string",
                "current dice, matchup and action-cost checks",
                "validation compatibility only",
                "exact pre-turn prefix"
            })
                Assert.Contains(invariant, text, StringComparison.Ordinal);
        foreach (var token in new[]
        {
            "afterlife_spiritual_exchange_history_authority_v1",
            "exchange_accepted_history_006",
            "afterlife_conflict_dice_value_not_authorized",
            "afterlife_conflict_matchup_audit_missing",
            "afterlife_conflict_action_cost_art_tier_authority_mismatch",
            "afterlife_conflict_resource_log_prefix_mutated"
        })
            Assert.Contains(token, examples, StringComparison.Ordinal);
        using var manifest = JsonDocument.Parse(ReadRepoFile("Examples", "example_validation_manifest.json"));
        var entry = Assert.Single(manifest.RootElement.GetProperty("afterlifeEntityProfileCoverage").EnumerateArray(), item => item.GetProperty("contractId").GetString() == "afterlife_spiritual_exchange_history_authority_v1");
        Assert.Equal("E_CLI_Afterlife_Turns.txt", entry.GetProperty("file").GetString());
        Assert.Equal("production-validator", entry.GetProperty("validationKind").GetString());
        Assert.Contains("T085_HistoricalExchange_", entry.GetProperty("validationRoute").GetString()!, StringComparison.Ordinal);
        Assert.Contains("HistoricalExchangeAuthorityDocumentation_", entry.GetProperty("validationRoute").GetString()!, StringComparison.Ordinal);
        Assert.Contains("not full wound or publication authority", entry.GetProperty("coverageLimit").GetString()!, StringComparison.Ordinal);
        var required = entry.GetProperty("requiredText").EnumerateArray().ToArray();
        Assert.NotEmpty(required);
        Assert.All(required, token => Assert.Contains(token.GetString()!, examples, StringComparison.Ordinal));
    }
}
