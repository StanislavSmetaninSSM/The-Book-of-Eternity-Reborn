using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeSpiritualConflictValidationTests
{
    [Fact]
    public async Task T085_HistoricalExchange_BackdatedAppendRequiresAllCurrentAudits()
    {
        await WriteHistoricalExchangeAuthoritySoulAsync();
        await WritePreTurnActiveConflictSnapshotWithAuthorityAsync();
        await WriteConflictStateWithRawExchangeAsync(
            BuildHistoricalExchangeAuthorityPayload().ToJsonString(),
            addDefaultMatchupAudit: false, addDefaultActionCostAudit: false);
        await AssertHistoricalExchangeAuthorityAsync(0, expectCurrent: true);
    }

    [Theory]
    [InlineData("art_tier")]
    [InlineData("dice_value")]
    [InlineData("nested_summary")]
    [InlineData("added_member")]
    [InlineData("removed_member")]
    [InlineData("turn_marker")]
    [InlineData("invalid_summary_number")]
    [InlineData("invalid_summary_object")]
    public async Task T085_HistoricalExchange_ChangedEvidenceRequiresAllCurrentAudits(string mutation)
    {
        var root = await WriteHistoricalExchangeAuthoritySnapshotAsync();
        var exchange = root["activeConflict"]!["exchangeLog"]![0]!.AsObject();
        switch (mutation)
        {
            case "art_tier": exchange["actionCostAudit"]!["player"]!["artTier"] = 4; break;
            case "dice_value": exchange["diceAudit"]!["diceUsed"]![0]!["value"] = 10; break;
            case "nested_summary": exchange["diceAudit"]!["summary"] = "Not the readable exchange summary."; break;
            case "added_member": exchange["unacceptedEvidence"] = true; break;
            case "removed_member": exchange.Remove("exchangeId"); break;
            case "turn_marker": exchange["exchangeAtTurn"] = 5; break;
            case "invalid_summary_number": exchange["summary"] = 42; break;
            case "invalid_summary_object": exchange["summary"] = new JsonObject { ["text"] = "Not a string." }; break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        await AssertHistoricalExchangeAuthorityAsync(0, expectCurrent: true);
    }

    [Fact]
    public async Task T085_HistoricalExchange_ForeignConflictCannotBorrowAcceptedOccurrence()
    {
        var root = await WriteHistoricalExchangeAuthoritySnapshotAsync();
        root["activeConflict"]!["conflictId"] = "afterlife_conflict_unaccepted_replacement";
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        await AssertHistoricalExchangeAuthorityAsync(0, expectCurrent: true);
    }

    [Theory]
    [InlineData("unchanged")]
    [InlineData("reworded")]
    [InlineData("null")]
    [InlineData("missing")]
    public async Task T085_HistoricalExchange_ExactOrReadableSummaryOnlyKeepsAuditCompatibility(string summary)
    {
        var root = await WriteHistoricalExchangeAuthoritySnapshotAsync();
        var exchange = root["activeConflict"]!["exchangeLog"]![0]!.AsObject();
        switch (summary)
        {
            case "unchanged": break;
            case "reworded": exchange["summary"] = "The old exchange remains old; only its readable wording differs."; break;
            case "null": exchange["summary"] = null; break;
            case "missing": exchange.Remove("summary"); break;
            default: throw new ArgumentOutOfRangeException(nameof(summary), summary, null);
        }
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        await AssertHistoricalExchangeAuthorityAsync(0, expectCurrent: false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task T085_HistoricalExchange_OneOccurrenceCannotExemptSecondCopy(bool changeSecondSummary)
    {
        var root = await WriteHistoricalExchangeAuthoritySnapshotAsync();
        var log = root["activeConflict"]!["exchangeLog"]!.AsArray();
        var second = log[0]!.DeepClone()!.AsObject();
        if (changeSecondSummary) second["summary"] = "A second copy is not another accepted occurrence.";
        log.Add(second);
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        var before = await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath);
        var issues = await ValidateAfterlifeSpiritualConflictAsync();
        AssertHistoricalExchangeAuthorityIssues(issues, 0, expectCurrent: false);
        AssertHistoricalExchangeAuthorityIssues(issues, 1, expectCurrent: true);
        Assert.Equal(before, await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath));
    }

    private Task WriteHistoricalExchangeAuthoritySoulAsync() => WriteSoulStateWithAfterlifeCombatProfileAsync("Chaos Sea", """
        {
          "schemaVersion": 1,
          "enlightenmentRank": 1,
          "radianceRank": 0,
          "retainedRadianceRank": 0,
          "spiritFocusTier": 0,
          "lastRecoveryTurn": 0,
          "artTiers": { "pressure": 0 }
        }
        """);

    private async Task<JsonObject> WriteHistoricalExchangeAuthoritySnapshotAsync()
    {
        await WriteHistoricalExchangeAuthoritySoulAsync();
        await WriteConflictStateWithRawExchangeAsync(BuildHistoricalExchangeAuthorityPayload().ToJsonString(), addDefaultMatchupAudit: false, addDefaultActionCostAudit: false);
        await WriteValidatedConflictSnapshotFromCurrentAsync("I continue the same accepted conflict.");
        return JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath))!)!.AsObject();
    }

    private static JsonObject BuildHistoricalExchangeAuthorityPayload() => JsonNode.Parse($$"""
    {
      "exchangeId": "exchange_accepted_history_006", "exchangeAtTurn": 6, "operationType": "pressure", "outcome": "no_effect", "summary": "The old pressure exchange left no new strain.",
      "before": { "playerSideStrain": "clear", "oppositionSideStrain": "clear", "conflictPosition": "contested" },
      "after": { "playerSideStrain": "clear", "oppositionSideStrain": "clear", "conflictPosition": "contested" },
      "diceAudit": {{BuildPriorTurnDiceAuditJson()}},
      "actionCostAudit": { "player": { "operationType": "pressure", "baseCost": 3, "minCost": 1, "artTier": 5, "effectiveCost": 1, "before": 6, "after": 5 } }
    }
    """)!.AsObject();

    private async Task AssertHistoricalExchangeAuthorityAsync(int index, bool expectCurrent)
    {
        var before = await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath);
        var issues = await ValidateAfterlifeSpiritualConflictAsync();
        AssertHistoricalExchangeAuthorityIssues(issues, index, expectCurrent);
        Assert.Equal(before, await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath));
    }

    private static void AssertHistoricalExchangeAuthorityIssues(IReadOnlyList<ValidationIssue> issues, int index, bool expectCurrent)
    {
        foreach (var code in new[] { "afterlife_conflict_dice_value_not_authorized", "afterlife_conflict_matchup_audit_missing", "afterlife_conflict_action_cost_art_tier_authority_mismatch" })
        {
            var matches = issues.Where(issue => string.Equals(issue.Code, code, StringComparison.Ordinal) && issue.FilePath.Contains($".activeConflict.exchangeLog[{index}]", StringComparison.Ordinal));
            if (expectCurrent) Assert.NotEmpty(matches); else Assert.Empty(matches);
        }
    }
}
