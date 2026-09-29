using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Keeps the canonical before-position shape requirement during cold intrinsic validation without a pending snapshot.
    /// </summary>
    /// <param name="position">
    /// Unsupported position text, or <see langword="null"/> to remove the required snapshot member.
    /// </param>
    /// <returns>
    /// A task completing after a fresh filesystem and validator reject the malformed retained snapshot without changing it.
    /// </returns>
    [Theory]
    [InlineData(null)]
    [InlineData("invented_position")]
    public async Task SpiritualPosition_ColdIntrinsicValidationStillRequiresCanonicalBeforePosition(string? position)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var root = await ReadProjectedSourceContinuationCandidateAsync(context);
        var before = root["activeConflict"]!["exchangeLog"]![0]!["before"]!.AsObject();
        if (position is null) before.Remove("conflictPosition");
        else before["conflictPosition"] = position;
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        context.FileSystem.DeleteFile("input/turn_request.json");
        context.FileSystem.DeleteFile("game_state/control/pending_turn_snapshot.json");
        context.FileSystem.DeleteFile("game_state/control/pending_turn_snapshot.authority.json");
        var original = await context.FileSystem.ReadFileBytesAsync(AfterlifeSpiritualConflictState.StatePath);
        var coldFs = new FileSystemManager(context.RootPath, NullLogger<FileSystemManager>.Instance);
        var coldValidator = new ValidationService(coldFs, NullLogger<ValidationService>.Instance);

        var issues = await coldValidator.ValidateGameStateAsync(
            new GameStateValidationSelection(GameStateValidationPhase.AfterlifeSpiritualConflictState));

        Assert.Contains(issues, issue => issue.Code == "afterlife_conflict_exchange_missing_before_position");
        Assert.Equal(original, await coldFs.ReadFileBytesAsync(AfterlifeSpiritualConflictState.StatePath));
        await using var lease = await coldFs.AcquireCanonicalWriteLeaseAsync();
        var capture = await coldValidator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        using var rejected = capture.Capture;
        Assert.Null(capture.Capture);
        Assert.Contains(capture.Issues, issue => issue.Severity == IssueSeverity.Error);
    }
}
