using System.Runtime.ExceptionServices;

namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    private readonly AsyncLocal<CanonicalWriteLease?> _gameStateDataReadLease = new();

    /// <summary>
    /// Executes one read-only GameState source phase without retaining its validation verdict.
    /// </summary>
    /// <typeparam name="TResult">
    /// The result constructed by the original phase on its current source data.
    /// </typeparam>
    /// <param name="read">
    /// The original read-only phase, which must not acquire another canonical lease or
    /// perform effects. Its exception remains the primary failure.
    /// </param>
    /// <returns>
    /// The original phase result after a fresh source fence for eligible DATA reuse;
    /// unsupported callers retain the original phase and reader contracts.
    /// </returns>
    internal async Task<TResult> ReadGameStateDataAsync<TResult>(Func<Task<TResult>> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        var entry = MainAdmissions.Value;
        // Async lease disposal may leave its closed physical head in this caller.
        // Never compact an owned remote close into a new eligible source lifetime.
        while (entry is { Closed: true, OwnsRemote: false }) entry = entry.Parent;
        if (ValidationReuseSuppression.Value != 0 || !CanReuseSourceData || HasBrowserOriginalOperation ||
            HasAmbientCanonicalLease() || _validationReadSet.Value != null ||
            _sourceDataOperation.Value is { IsSuspended: true } ||
            !SessionOperationContext.TryGetExpectedGeneration(BasePath, out _) ||
            entry == null || !ReferenceEquals(entry.RootIdentity, CanonicalRootAuthorityIdentity))
        {
            using var fallback = MeasureOperation("GameStateData.Fallback");
            return await read();
        }

        bool eligible;
        lock (CanonicalRootAuthorityIdentity.WorkerContextGate)
            eligible = entry.CanStartSourceDataRead();
        if (!eligible)
        {
            using var fallback = MeasureOperation("GameStateData.Fallback");
            return await read();
        }

        using var phase = MeasureOperation("GameStateData.Phase");
        CanonicalWriteLease? lease = null;
        SourceDataOperation? ownedOperation = null;
        var previous = _gameStateDataReadLease.Value;
        TResult result = default!;
        Exception? failure = null;
        try
        {
            // This operation already authorizes ordinary session access. Standalone,
            // worker, browser and reserved read callers never acquire this lease.
            using (MeasureOperation("GameStateData.EntryLease"))
                lease = await AcquireCanonicalWriteLeaseAsync();
            var generation = ReadLocalGenerationSnapshot(lease);
            if (generation.Bytes == null)
                result = await read();
            else
            {
                ownedOperation = BeginSourceDataOperationIfNeeded();
                var operation = _sourceDataOperation.Value!;
                operation.RequireOpen();
                operation.Observe(lease);
                _gameStateDataReadLease.Value = lease;
                result = await read();
                _gameStateDataReadLease.Value = previous;
                using var fence = MeasureOperation("GameStateData.ValidateSources");
                if (ownedOperation != null) await ownedOperation.CompleteAsync();
                else await operation.VerifyAsync(lease);
            }
        }
        catch (Exception error)
        {
            failure = error;
            InvalidateRecoveryValidationAfterFailure();
        }
        finally
        {
            _gameStateDataReadLease.Value = previous;
            try { ownedOperation?.Dispose(); }
            catch (Exception close) { RetainGameStateReadFailure(ref failure, close); }
            try { if (lease != null) await lease.DisposeAsync(); }
            catch (Exception close) { RetainGameStateReadFailure(ref failure, close); }
        }
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        return result;
    }

    /// <summary>
    /// Preserves the first functional or closing failure while still attempting owned cleanup.
    /// </summary>
    /// <param name="primary">
    /// First failure, or <see langword="null"/> until cleanup first fails.
    /// </param>
    /// <param name="close">
    /// Actual subsequent cleanup failure, never converted into successful validation.
    /// </param>
    private static void RetainGameStateReadFailure(ref Exception? primary, Exception close)
    {
        if (primary == null) primary = close;
        else try { primary.Data["GameStateData.CloseFailure"] = close; } catch { }
    }

    /// <summary>
    /// Selects only existing implicit ordinary DATA under the exact selected GameState adapter.
    /// </summary>
    /// <param name="path">
    /// Raw ordinal relative spelling used by the original public reader.
    /// </param>
    /// <param name="asynchronous">
    /// Whether the original reader belongs to the asynchronous ordinary family.
    /// </param>
    /// <param name="snapshot">
    /// Detached DATA on a hit, including <see langword="null"/> for recorded absence.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for a current service hit; otherwise, <see langword="false"/>
    /// leaves original recovery, admission and source reading unchanged.
    /// </returns>
    private bool TryReadImplicitGameStateData(string path, bool asynchronous, out CanonicalFileReadSnapshot? snapshot)
    {
        snapshot = null;
        var hit = ValidationReuseSuppression.Value == 0 && _validationReadSet.Value == null &&
            _gameStateDataReadLease.Value is { } lease && _sourceDataOperation.Value is { } operation &&
            operation.TryReadImplicitSnapshot(lease, path, asynchronous, out snapshot);
        if (hit && _controlGameStateReadActivity.Value is { Closed: false } activity)
            Interlocked.Increment(ref activity.SourceHits);
        return hit;
    }
}
