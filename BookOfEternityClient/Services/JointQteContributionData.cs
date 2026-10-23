using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

/// <summary>
/// Holds detached QTE calculation values without granting source or publication authority.
/// </summary>
/// <param name="Fields">
/// Ordered fingerprint fields produced by the original calculation.
/// </param>
/// <param name="AfterImages">
/// Successor JSON images produced by the original calculation.
/// </param>
internal sealed record JointQtePreparedData(List<string?> Fields,
    IReadOnlyDictionary<string, JsonObject> AfterImages);

/// <summary>
/// Separates reusable QTE calculation DATA from fresh source and registry admission.
/// </summary>
internal static class JointQteContributionData
{
    /// <summary>
    /// Reads detached calculation DATA for one already admitted baseline and current read phase.
    /// </summary>
    /// <param name="files">
    /// Actual manager owning the current operation and read phase.
    /// </param>
    /// <param name="lease">
    /// Active physical lease retained by the caller.
    /// </param>
    /// <param name="owner">
    /// Exact private baseline identity.
    /// </param>
    /// <param name="capture">
    /// Stable complete structural capture policy; unsupported inputs return null.
    /// </param>
    /// <param name="canReuse">
    /// Fresh eligibility for skipping only idempotent registry operations; false selects the source.
    /// </param>
    /// <param name="validateBaseline">
    /// Original fresh baseline validation required at entry and exit.
    /// </param>
    /// <param name="compute">
    /// Original QTE source returning successful DATA or throwing its original refusal.
    /// </param>
    /// <returns>
    /// Detached calculation DATA after both baseline checks and current source proof succeed.
    /// </returns>
    internal static async Task<JointQtePreparedData> RecomputeAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, object owner,
        Func<OperationStructuralSnapshot?> capture, Func<bool> canReuse,
        Func<Task> validateBaseline, Func<Task<JointQtePreparedData>> compute)
    {
        var readSet = files.TryBeginReservedValidationReadSet(lease);
        Exception? primaryFailure = null;
        try
        {
            if (readSet != null) await readSet.OpenAsync();
            var prepared = await JointRepairValidationCoordinator.RecomputeWithBaselineAsync(validateBaseline, compute);
            if (readSet != null) await readSet.CompleteAsync();
            return prepared;
        }
        catch (Exception error)
        {
            primaryFailure = error;
            files.InvalidateRecoveryValidationAfterFailure();
            throw;
        }
        finally
        {
            try { if (readSet != null) await readSet.DisposeAsync(); }
            catch when (primaryFailure != null) { /* Preserve the original refusal. */ }
        }
    }
}
