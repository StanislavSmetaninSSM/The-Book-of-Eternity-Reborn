namespace BookOfEternityClient.Services;

/// <summary>Distinguishes load admission from a committed, restored or unresolved replacement.</summary>
internal enum LoadReplacementDisposition { NotLoaded, Committed, RolledBack, Uncertain }

/// <summary>Retains selected source and generation authority through replacement follow-up.</summary>
internal sealed record LoadReplacementResult(LoadReplacementDisposition Disposition,
    string? SelectedSourcePath, string? EstablishedGeneration, bool NeedsFollowUp,
    Exception? Failure, bool ContinuationBlocked = false);

public partial class SaveLoadService
{
    /// <summary>Ordinary portable load entry; public callers remain on the original route until cutover.</summary>
    /// <param name="saveFilePath">The exact selected archive or canonical relative archive name.</param>
    /// <param name="cancellationToken">Cancellation before an established replacement decision.</param>
    /// <returns>The established replacement decision and any separate follow-up diagnostic.</returns>
    internal Task<LoadReplacementResult> LoadGameWithOutcomeAsync(string saveFilePath,
        CancellationToken cancellationToken = default)
    {
        // T032-B1 RED scaffolding only. This explicitly missing behavior cannot
        // satisfy the specific admission guards or publication-cut assertions.
        return Task.FromResult(new LoadReplacementResult(LoadReplacementDisposition.NotLoaded,
            null, null, false, new NotImplementedException("Ordinary portable load is not implemented.")));
    }
}
