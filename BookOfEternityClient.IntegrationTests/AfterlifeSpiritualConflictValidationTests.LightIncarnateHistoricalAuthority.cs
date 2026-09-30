using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeSpiritualConflictValidationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task T085_LightIncarnateHistory_BackdatedAppendRequiresCurrentAuthority(bool recent)
    {
        var root = await CreateLightIncarnateHistoryRootAsync(recent, 6);
        await SnapshotEmptyLightIncarnateHistoryAsync(root, recent);
        await WriteAndAssertLightIncarnateHistoryAsync(root, recent, 0, expectMismatch: true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task T085_LightIncarnateHistory_ChangedOldPayloadIsNotPreGrantEvidence(bool recent)
    {
        var root = await CreateLightIncarnateHistoryRootAsync(recent, 6);
        await WriteValidatedConflictSnapshotFromCurrentAsync("Continue after the accepted old audit.");
        GetLightIncarnateHistoryLog(root, recent)[0]!["operationType"] = "pressure";
        await WriteAndAssertLightIncarnateHistoryAsync(root, recent, 0, expectMismatch: true);
    }

    [Theory]
    [InlineData(false, 6)]
    [InlineData(true, 6)]
    [InlineData(false, null)]
    [InlineData(true, null)]
    public async Task T085_LightIncarnateHistory_OneOccurrenceCannotExemptAnother(bool recent, int? turn)
    {
        var root = await CreateLightIncarnateHistoryRootAsync(recent, turn);
        await WriteValidatedConflictSnapshotFromCurrentAsync("Continue after one accepted occurrence.");
        var log = GetLightIncarnateHistoryLog(root, recent);
        log.Add(log[0]!.DeepClone());
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        var before = await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath);
        var issues = await ValidateAfterlifeSpiritualConflictAsync();

        AssertLightIncarnateHistoryIssues(issues, recent, 0, expectMismatch: false);
        AssertLightIncarnateHistoryIssues(issues, recent, 1, expectMismatch: true);
        Assert.Equal(before, await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath));
    }

    [Fact]
    public async Task T085_LightIncarnateHistory_ForeignActiveConflictCannotBorrowNoTurnPayload()
    {
        var root = await CreateLightIncarnateHistoryRootAsync(recent: false, turn: null);
        await WriteValidatedConflictSnapshotFromCurrentAsync("The old active conflict is accepted.");
        root["activeConflict"]!["conflictId"] = "afterlife_conflict_replacement";
        await WriteAndAssertLightIncarnateHistoryAsync(root, recent: false, 0, expectMismatch: true);
    }

    [Fact]
    public async Task T085_LightIncarnateHistory_RecentProofCannotBorrowActiveNoTurnPayload()
    {
        var root = await CreateLightIncarnateHistoryRootAsync(recent: false, turn: null);
        await WriteValidatedConflictSnapshotFromCurrentAsync("An exchange is not an accepted resolution.");
        var payload = GetLightIncarnateHistoryLog(root, recent: false)[0]!.DeepClone();
        root["activeConflict"] = null;
        root["recentConflicts"] = new JsonArray(payload);
        await WriteAndAssertLightIncarnateHistoryAsync(root, recent: true, 0, expectMismatch: true);
    }

    [Theory]
    [InlineData("payload_resolved")]
    [InlineData("dice_turn")]
    [InlineData("nested_resolution")]
    public async Task T085_LightIncarnateHistory_OldMarkerPrecedenceCannotExemptCurrentExchange(string marker)
    {
        var root = await CreateLightIncarnateHistoryRootAsync(recent: false, turn: 7);
        await SnapshotEmptyLightIncarnateHistoryAsync(root, recent: false);
        var payload = GetLightIncarnateHistoryLog(root, recent: false)[0]!.AsObject();
        switch (marker)
        {
            case "payload_resolved":
                payload["resolvedAtTurn"] = 6;
                break;
            case "dice_turn":
                payload.Remove("exchangeAtTurn");
                payload["diceAudit"]!["turnNumber"] = 6;
                break;
            case "nested_resolution":
                payload.Remove("exchangeAtTurn");
                payload["resolution"] = new JsonObject { ["resolvedAtTurn"] = 6 };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(marker), marker, null);
        }
        await WriteAndAssertLightIncarnateHistoryAsync(root, recent: false, 0, expectMismatch: true);
    }

    [Theory]
    [InlineData(false, 6)]
    [InlineData(true, 6)]
    [InlineData(false, null)]
    [InlineData(true, null)]
    public async Task T085_LightIncarnateHistory_ValidatedBaselineWithoutDiceStillRequiresCurrentAuthority(
        bool recent, int? turn)
    {
        var root = await CreateLightIncarnateHistoryRootAsync(recent, turn);
        await SnapshotEmptyLightIncarnateHistoryAsync(root, recent);
        await RemoveLightIncarnateHistoryFixtureDiceAsync();
        await WriteAndAssertLightIncarnateHistoryAsync(root, recent, 0, expectMismatch: true);
    }

    [Theory]
    [InlineData(false, 6)]
    [InlineData(true, 6)]
    [InlineData(false, null)]
    [InlineData(true, null)]
    public async Task T085_LightIncarnateHistory_ExactAcceptedOccurrenceKeepsCompatibility(bool recent, int? turn)
    {
        var root = await CreateLightIncarnateHistoryRootAsync(recent, turn);
        await WriteValidatedConflictSnapshotFromCurrentAsync("Preserve the accepted old audit.");
        await WriteAndAssertLightIncarnateHistoryAsync(root, recent, 0, expectMismatch: false);
    }

    [Fact]
    public async Task T085_LightIncarnateHistory_OldActiveReadableSummaryKeepsExistingCompatibility()
    {
        var root = await CreateLightIncarnateHistoryRootAsync(recent: false, turn: 6);
        await WriteValidatedConflictSnapshotFromCurrentAsync("Preserve all accepted mechanics.");
        GetLightIncarnateHistoryLog(root, recent: false)[0]!["summary"] = "Only the readable wording differs.";
        await WriteAndAssertLightIncarnateHistoryAsync(root, recent: false, 0, expectMismatch: false);
    }

    [Fact]
    public async Task T085_LightIncarnateHistory_ChangedRecentSummaryIsNotPreGrantEvidence()
    {
        var root = await CreateLightIncarnateHistoryRootAsync(recent: true, turn: 6);
        await WriteValidatedConflictSnapshotFromCurrentAsync("Preserve the exact accepted resolution.");
        GetLightIncarnateHistoryLog(root, recent: true)[0]!["summary"] = "Changed recent resolution wording.";
        await WriteAndAssertLightIncarnateHistoryAsync(root, recent: true, 0, expectMismatch: true);
    }

    private async Task<JsonObject> CreateLightIncarnateHistoryRootAsync(bool recent, int? turn)
    {
        await WriteSoulStateWithLightIncarnateAsync();
        if (recent)
        {
            await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, $$"""
            {
              "schemaVersion": 1,
              "activeConflict": null,
              "recentConflicts": [{
                "dangerMode": "hostile",
                "conflictId": "afterlife_conflict_light_history_006",
                "resolutionState": "resolved",
                "operationType": "guard",
                "playerOutcome": "won",
                "summary": "The accepted conflict ended before the grant.",
                "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
              }]
            }
            """);
        }
        else
        {
            await WriteConflictStateWithRawExchangeAsync($$"""
            {
              "exchangeId": "exchange_light_history_006",
              "operationType": "guard",
              "outcome": "success",
              "summary": "The accepted exchange predates the grant.",
              "before": { "conflictPosition": "contested" },
              "after": { "conflictPosition": "player_advantaged" },
              "diceAudit": {{BuildPlayerSuccessDiceAuditJson()}}
            }
            """);
        }

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath))!)!.AsObject();
        if (turn.HasValue)
            GetLightIncarnateHistoryLog(root, recent)[0]![recent ? "resolvedAtTurn" : "exchangeAtTurn"] = turn.Value;
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        return root;
    }

    private async Task SnapshotEmptyLightIncarnateHistoryAsync(JsonObject candidate, bool recent)
    {
        var baseline = candidate.DeepClone().AsObject();
        GetLightIncarnateHistoryLog(baseline, recent).Clear();
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, baseline.ToJsonString());
        await WriteValidatedConflictSnapshotFromCurrentAsync("Resolve a genuinely new current audit.");
    }

    private async Task RemoveLightIncarnateHistoryFixtureDiceAsync()
    {
        const string requestPath = "input/turn_request.json";
        const string manifestPath = "game_state/control/pending_turn_snapshot.json";
        var request = JsonNode.Parse((await _fs.ReadFileAsync(requestPath))!)!.AsObject();
        var manifest = JsonNode.Parse((await _fs.ReadFileAsync(manifestPath))!)!.AsObject();
        request["preGeneratedDices1d20"] = new JsonArray();
        manifest["preGeneratedDices1d20"] = new JsonArray();
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
        await _fs.WriteFileAtomicAsync(requestPath, request.ToJsonString());
        await _fs.WriteFileAtomicAsync(manifestPath, manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private static JsonArray GetLightIncarnateHistoryLog(JsonObject root, bool recent) =>
        (recent ? root["recentConflicts"] : root["activeConflict"]!["exchangeLog"])!.AsArray();

    private async Task WriteAndAssertLightIncarnateHistoryAsync(
        JsonObject root, bool recent, int index, bool expectMismatch)
    {
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        var before = await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath);
        var issues = await ValidateAfterlifeSpiritualConflictAsync();
        AssertLightIncarnateHistoryIssues(issues, recent, index, expectMismatch);
        Assert.Equal(before, await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath));
    }

    private static void AssertLightIncarnateHistoryIssues(
        IReadOnlyList<ValidationIssue> issues, bool recent, int index, bool expectMismatch)
    {
        var path = recent ? $".recentConflicts[{index}].diceAudit" : $".activeConflict.exchangeLog[{index}].diceAudit";
        var selected = issues.Where(issue => issue.FilePath.Contains(path, StringComparison.Ordinal)).ToArray();
        var mismatches = selected.Where(issue =>
            string.Equals(issue.Code, "afterlife_conflict_light_incarnate_modifier_mismatch", StringComparison.Ordinal)).ToArray();
        if (expectMismatch)
            Assert.Single(mismatches);
        else
            Assert.Empty(mismatches);
        Assert.DoesNotContain(selected, issue =>
            string.Equals(issue.Code, "afterlife_conflict_light_incarnate_modifier_unauthorized", StringComparison.Ordinal));
    }
}
