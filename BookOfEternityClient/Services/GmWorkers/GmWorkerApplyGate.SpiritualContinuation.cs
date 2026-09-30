using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Services.GmWorkers;

public sealed partial class GmWorkerApplyGate
{
    /// <summary>
    /// Authenticates the current continuation and exact proposed differences before starting an apply transaction.
    /// </summary>
    /// <param name="proposal">
    /// Structurally validated response with captured proposal content references.
    /// </param>
    /// <param name="task">
    /// Physically reserved task whose public request must match freshly reconstructed C2.
    /// </param>
    /// <param name="contents">
    /// Hash-verified replacement bytes; deletions are represented separately by changedFiles.
    /// </param>
    /// <param name="lease">
    /// Active canonical lease retained through preservation and later atomic application.
    /// </param>
    /// <returns>
    /// Rejection reasons, or empty for ordinary repair or a preserved current continuation proposal.
    /// </returns>
    private async Task<IReadOnlyList<string>> VerifySpiritualContinuationProposalAsync(WorkerProposal proposal,
        WorkerTaskPacket task, IReadOnlyDictionary<string, byte[]> contents, FileSystemManager.CanonicalWriteLease lease)
    {
        if (task.SpiritualWoundContinuation is not { } request) return [];
        if (await _fs.ReadFileBytesAsync(lease, GmWorkerValidationRepairDelegator.ValidationRepairReadyPath) is not null)
            return ["A continuation Ready already awaits client consumption; the worker cannot change its scene or response."];
        if (proposal.SpiritualWoundContinuation is not { } response ||
            !await MatchesSpiritualContinuationTurnAsync(task.SourceTurn, lease))
            return ["The spiritual continuation response does not belong to the current original turn."];
        var images = proposal.ChangedFiles.ToDictionary(file => file.Path,
            file => file.ChangeKind == WorkerFileChangeKind.Delete ? null : contents[file.Path], StringComparer.Ordinal);
        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateSpiritualWoundContinuationDraftAsync(lease, request, response, images);
        return issues.Select(issue => issue.ToString()).ToArray();
    }

    /// <summary>
    /// Validates applied continuation drafts through genuine owners without publishing or consuming a decision.
    /// </summary>
    /// <param name="proposal">
    /// Correlated response already admitted by preservation preflight.
    /// </param>
    /// <param name="task">
    /// Reserved request and original turn identity to reauthenticate.
    /// </param>
    /// <param name="lease">
    /// Active lease covering the current draft inside the apply transaction or immediately before Ready publication.
    /// </param>
    /// <returns>
    /// Refusal diagnostics requiring rollback; empty means the exact issued frontier is resolved or has a proved successor.
    /// </returns>
    internal async Task<IReadOnlyList<ValidationIssue>> ValidateSpiritualContinuationAfterApplyAsync(
        WorkerProposal proposal, WorkerTaskPacket task, FileSystemManager.CanonicalWriteLease lease)
    {
        if (task.SpiritualWoundContinuation is not { } request ||
            proposal.SpiritualWoundContinuation is not { } response ||
            !await MatchesSpiritualContinuationTurnAsync(task.SourceTurn, lease))
            return [new ValidationIssue("input/turn_request.json", IssueSeverity.Error,
                "The spiritual continuation no longer belongs to the current original turn.",
                code: "spiritual_continuation_worker_turn_changed")];
        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var evaluated = await validator.EvaluateSpiritualWoundContinuationDraftAsync(lease, request, response);
        return evaluated.Disposition == ValidationService.SpiritualWoundContinuationDisposition.Rejected
            ? evaluated.Issues : [];
    }

    /// <summary>
    /// Compares public task metadata with the physical original request that C2 separately authenticates.
    /// </summary>
    /// <param name="turn">
    /// Session, request and turn metadata of the reserved task.
    /// </param>
    /// <param name="lease">
    /// Active lease also retained during genuine C2 validation.
    /// </param>
    /// <returns>
    /// <see langword="true"/> only when all three identity fields match the exact current request;
    /// otherwise, including malformed input, <see langword="false"/>.
    /// </returns>
    private async Task<bool> MatchesSpiritualContinuationTurnAsync(WorkerTurnReference turn,
        FileSystemManager.CanonicalWriteLease lease)
    {
        try
        {
            var bytes = await _fs.ReadFileBytesAsync(lease, "input/turn_request.json");
            if (bytes is null) return false;
            var original = SpiritualWoundDependentDraftPolicy.ReadStrictRoot(DecodeUtf8(bytes)!);
            return original["sessionId"]?.GetValue<string>() == turn.SessionId &&
                original["requestId"]?.GetValue<string>() == turn.RequestId &&
                original["turnNumber"]?.GetValue<int>() == turn.TurnNumber;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
            InvalidOperationException or FormatException or JsonException or OverflowException)
        {
            return false;
        }
    }
}
