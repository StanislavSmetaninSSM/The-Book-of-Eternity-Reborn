using System.Text.Json.Nodes;
using System.Text.Json;
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
    private static readonly OperationComputation<CachedCalculation> Calculation = new(
        "JointQte.ContributionData", 1, value => new(Copy(value.Data), value.Observations));

    /// <summary>
    /// Retains detached calculation values and immutable source comparison metadata only.
    /// </summary>
    /// <param name="Data">
    /// Independently copied fields and successor images.
    /// </param>
    /// <param name="Observations">
    /// Immutable source metadata with no reader or registry capability.
    /// </param>
    private sealed record CachedCalculation(JointQtePreparedData Data,
        FileSystemManager.ValidationReadObservations Observations);

    /// <summary>
    /// Detaches every mutable field collection, dictionary and JSON node.
    /// </summary>
    /// <param name="data">
    /// Successful original or retained calculation DATA.
    /// </param>
    /// <returns>
    /// Independent calculation values with no mutable object shared with the source.
    /// </returns>
    private static JointQtePreparedData Copy(JointQtePreparedData data) => new(data.Fields.ToList(),
        data.AfterImages.ToDictionary(row => row.Key, row => row.Value.DeepClone().AsObject(), StringComparer.Ordinal));

    /// <summary>
    /// Captures optional comparison DATA without replacing the original source refusal for an unsupported shape.
    /// </summary>
    /// <param name="capture">
    /// Complete typed capture policy used by the existing operation dependency.
    /// </param>
    /// <returns>
    /// Current immutable comparison DATA, or <see langword="null"/> when the original source must decide the result.
    /// </returns>
    private static OperationStructuralSnapshot? CaptureInput(Func<OperationStructuralSnapshot?> capture)
    {
        try { return capture(); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or
            JsonException or OverflowException or NullReferenceException or NotSupportedException) { return null; }
    }

    /// <summary>
    /// Computes or reuses detached calculation DATA between the original fresh baseline and source-proof boundaries.
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
    /// Stable complete structural capture policy; unsupported inputs return <see langword="null"/>.
    /// </param>
    /// <param name="canReuse">
    /// Fresh eligibility for skipping only idempotent registry operations; <see langword="false"/> selects the source.
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
            var prepared = await JointRepairValidationCoordinator.RecomputeWithBaselineAsync(validateBaseline,
                () => ReadAsync(files, lease, owner, capture, readSet, canReuse, compute));
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
            catch
            {
                files.InvalidateRecoveryValidationAfterFailure();
                if (primaryFailure == null) throw;
            }
        }
    }

    /// <summary>
    /// Reuses calculation DATA only after current registry eligibility and complete source replay.
    /// </summary>
    /// <param name="files">
    /// Actual file manager supplying the existing operation service.
    /// </param>
    /// <param name="lease">
    /// Current original lease retained by the caller.
    /// </param>
    /// <param name="owner">
    /// Exact private input owner for this calculation.
    /// </param>
    /// <param name="capture">
    /// Stable complete current structural capture policy.
    /// </param>
    /// <param name="readSet">
    /// Current reserved phase, or <see langword="null"/> for original source execution.
    /// </param>
    /// <param name="canReuse">
    /// Fresh non-authorizing registry eligibility; <see langword="false"/> preserves original binding/restoration.
    /// </param>
    /// <param name="compute">
    /// Original successful calculation source, including its existing refusal mapping.
    /// </param>
    /// <returns>
    /// Independent calculation DATA, still subject to the surrounding baseline and exit proof.
    /// </returns>
    private static async Task<JointQtePreparedData> ReadAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, object owner,
        Func<OperationStructuralSnapshot?> capture, FileSystemManager.ReservedValidationReadSet? readSet,
        Func<bool> canReuse, Func<Task<JointQtePreparedData>> compute)
    {
        bool Eligible()
        {
            using var timing = files.MeasureOperation("JointQte.LiveCacheEligibility");
            return canReuse();
        }
        if (readSet == null || !Eligible()) return Copy(await compute());
        var input = CaptureInput(capture);
        if (input == null) return Copy(await compute());
        var computed = false;
        async Task<CachedCalculation> Source()
        {
            computed = true;
            using var timing = files.MeasureOperation("JointQte.CalculationSource");
            var data = await compute();
            return new(data, readSet.CaptureObservations());
        }
        var retained = await files.ComputeRecoveryDataAsync(Calculation, owner, lease, Source, capture);
        if (!computed)
        {
            using var replay = files.MeasureOperation("JointQte.ValidateCachedSources");
            if (!Eligible() || !await readSet.MatchesObservationsAsync(retained.Observations) || !Eligible() ||
                CaptureInput(capture) is not { } currentInput || !input.Matches(currentInput))
            {
                using var fresh = files.ForceFreshRecoveryValidation();
                using var fallback = files.MeasureOperation("JointQte.ChangedCacheSource");
                return Copy(await compute());
            }
            using var reused = files.MeasureOperation("JointQte.CalculationReused");
        }
        return retained.Data;
    }
}
