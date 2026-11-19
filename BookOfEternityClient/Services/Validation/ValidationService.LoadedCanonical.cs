using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>Refuses original/marker state before Load settings or UI health can alter it.</summary>
    internal Task<List<ValidationIssue>> RequireLoadedCanonicalIdleAsync(string generation)
        => RunPendingSnapshotValidationCoreAsync(() => Task.FromResult(new List<ValidationIssue>()),
            () => _fs.BeginPendingSnapshotValidationRead(CancellationToken.None), loadedGeneration: generation);

    /// <summary>Validates canonical Mortal DATA under the same retained quiescent Load owner, never a DTO grant.</summary>
    internal Task<List<ValidationIssue>> ValidateLoadedCanonicalGameStateAsync(string generation)
        => RunPendingSnapshotValidationCoreAsync(
            () => ValidateGameStateCoreAsync(GameStateValidationSelection.All, false, null, null),
            () => _fs.BeginPendingSnapshotValidationRead(CancellationToken.None),
            ordinaryCanonical: true, loadedGeneration: generation);
}
