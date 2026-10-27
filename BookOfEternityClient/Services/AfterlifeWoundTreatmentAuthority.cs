using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

/// <summary>Exact self selection; display names and visible indexes never identify a wound.</summary>
internal sealed record SpiritualSelfTreatmentSelection(string WoundId, string ExpectedWoundFingerprint);

/// <summary>Client staging coordinates and the existing scheduler's ordinary turn control.</summary>
internal sealed record SpiritualSelfTreatmentStageContext(
    string SessionId, string RequestId, int Turn, ProgressionControl Progression);

/// <summary>Staging status only; it grants no wound, cycle or accepted-publication authority.</summary>
internal sealed record SpiritualSelfTreatmentStageResult(
    bool IsStaged, bool AlreadyStaged, string? IssueCode, string? RequestFingerprint);

/// <summary>Owns exact source admission for spiritual treatment requests.</summary>
internal static class AfterlifeWoundTreatmentAuthority
{
    /// <summary>Stages one retained self-treatment request before ordinary signed snapshot capture.</summary>
    /// <param name="files">The current canonical filesystem.</param>
    /// <param name="lease">Its actual active canonical write lease.</param>
    /// <param name="context">The client turn and scheduler coordinates.</param>
    /// <param name="selection">The exact current wound selected by the player.</param>
    /// <param name="drawDice">Optional entropy dependency; null uses the existing secure GameLoop generator.</param>
    /// <returns>A durable pending request or a refusal with no treatment/publication authority.</returns>
    internal static Task<SpiritualSelfTreatmentStageResult> StageSelfTreatmentAsync(
        FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease,
        SpiritualSelfTreatmentStageContext context,
        SpiritualSelfTreatmentSelection selection,
        Func<int, int[]>? drawDice = null)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(selection);
        // Semantic RED scaffold; no entropy, source mutation or publication is admitted yet.
        return Task.FromResult(new SpiritualSelfTreatmentStageResult(false, false,
            "spiritual_self_treatment_not_implemented", null));
    }
}
