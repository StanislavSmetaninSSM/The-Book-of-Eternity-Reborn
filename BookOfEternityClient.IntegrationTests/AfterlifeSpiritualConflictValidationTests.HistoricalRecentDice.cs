using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeSpiritualConflictValidationTests
{
    [Theory]
    [InlineData(6)]
    [InlineData(null)]
    public async Task HistoricalRecentDice_ExactRetainedProofKeepsItsOriginalDice(int? turn)
    {
        await CreateHistoricalRecentDiceRootAsync(turn);
        await WriteValidatedConflictSnapshotFromCurrentAsync("Continue after the accepted resolution with new dice.");
        // Preserve the original single-argument API as a typed compile-time contract.
        Func<ValidationService.SpiritualConflictFileImage?, Task<ValidationService.SpiritualConflictValidationFrame>> capture =
            _validator.CaptureSpiritualConflictValidationFrameAsync;
        var frame = await capture(null);
        Assert.True(frame.HasValidatedSnapshot);
        Assert.Equal(AuthoritativeConflictDice, frame.SnapshotDice);
        var issues = await ValidateHistoricalRecentDiceWithoutMutationAsync(frame);
        Assert.Empty(issues.Where(issue => issue.Severity == IssueSeverity.Error));
    }

    [Theory]
    [InlineData("changed")]
    [InlineData("new")]
    [InlineData("backdated")]
    public async Task HistoricalRecentDice_NonRetainedProofRequiresCurrentDice(string mutation)
    {
        var root = await CreateHistoricalRecentDiceRootAsync(turn: null);
        if (mutation != "changed")
        {
            var empty = root.DeepClone().AsObject();
            empty["recentConflicts"] = new JsonArray();
            await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, empty.ToJsonString());
        }
        await WriteValidatedConflictSnapshotFromCurrentAsync("Resolve using only this turn's authorized dice.");
        var proof = root["recentConflicts"]![0]!;
        if (mutation == "changed") proof["summary"] = "Changed accepted proof, not the signed original.";
        if (mutation == "backdated") proof["resolvedAtTurn"] = 6;
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        var issues = await ValidateHistoricalRecentDiceWithoutMutationAsync();
        AssertHistoricalRecentDiceMismatch(issues, index: 0);
    }

    [Fact]
    public async Task HistoricalRecentDice_OneSignedOccurrenceExemptsOnlyOneCopy()
    {
        var root = await CreateHistoricalRecentDiceRootAsync(turn: 6);
        await WriteValidatedConflictSnapshotFromCurrentAsync("One accepted proof does not authorize a second copy.");
        var recent = root["recentConflicts"]!.AsArray();
        recent.Add(recent[0]!.DeepClone());
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        var issues = await ValidateHistoricalRecentDiceWithoutMutationAsync();
        Assert.DoesNotContain(issues, issue => issue.FilePath.Contains(".recentConflicts[0].diceAudit", StringComparison.Ordinal) &&
            issue.Severity == IssueSeverity.Error);
        AssertHistoricalRecentDiceMismatch(issues, index: 1);
    }

    [Fact]
    public async Task HistoricalRecentDice_RetainedProofStillValidatesArithmetic()
    {
        var root = await CreateHistoricalRecentDiceRootAsync(turn: 6);
        // Deliberately malformed signed DATA proves that retention does not skip audit validation.
        root["recentConflicts"]![0]!["diceAudit"]!["playerTotal"] = 14;
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        await WriteValidatedConflictSnapshotFromCurrentAsync("Retained history must still have valid arithmetic.");
        var issues = await ValidateHistoricalRecentDiceWithoutMutationAsync();
        Assert.Contains(issues, issue => issue.Code == "afterlife_conflict_dice_player_total_mismatch" &&
            issue.FilePath.EndsWith(".recentConflicts[0].diceAudit.playerTotal", StringComparison.Ordinal));
        Assert.DoesNotContain(issues, issue => issue.Code == "afterlife_conflict_dice_value_not_authorized");
    }

    private async Task<JsonObject> CreateHistoricalRecentDiceRootAsync(int? turn)
    {
        var root = await CreateLightIncarnateHistoryRootAsync(recent: true, turn);
        await WriteSoulStateAsync();
        var audit = JsonNode.Parse(BuildPriorTurnDiceAuditJson())!;
        Assert.NotEqual(AuthoritativeConflictDice[0], audit["diceUsed"]![0]!["value"]!.GetValue<int>());
        Assert.NotEqual(AuthoritativeConflictDice[1], audit["diceUsed"]![1]!["value"]!.GetValue<int>());
        root["recentConflicts"]![0]!["diceAudit"] = audit;
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        return root;
    }

    private async Task<IReadOnlyList<ValidationIssue>> ValidateHistoricalRecentDiceWithoutMutationAsync(
        ValidationService.SpiritualConflictValidationFrame? frame = null)
    {
        frame ??= await _validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.True(frame.HasValidatedSnapshot);
        Assert.Equal(AuthoritativeConflictDice, frame.SnapshotDice);
        var before = await _fs.ReadFileBytesAsync(AfterlifeSpiritualConflictState.StatePath);
        var issues = await ValidateAfterlifeSpiritualConflictAsync();
        Assert.Equal(before, await _fs.ReadFileBytesAsync(AfterlifeSpiritualConflictState.StatePath));
        return issues;
    }

    private static void AssertHistoricalRecentDiceMismatch(IReadOnlyList<ValidationIssue> issues, int index)
    {
        var mismatches = issues.Where(issue => issue.Code == "afterlife_conflict_dice_value_not_authorized" &&
            issue.FilePath.Contains($".recentConflicts[{index}].diceAudit.", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, mismatches.Length);
        Assert.Contains(mismatches, issue => issue.FilePath.EndsWith(".diceUsed[0].value", StringComparison.Ordinal));
        Assert.Contains(mismatches, issue => issue.FilePath.EndsWith(".diceUsed[1].value", StringComparison.Ordinal));
    }
}
