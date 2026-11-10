namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    /// <summary>
    /// Coalesces post-recovery DATA inside an owning operation or a standalone verified read plan.
    /// </summary>
    /// <typeparam name="TResult">
    /// The result produced by the original plan; this method does not retain authority objects.
    /// </typeparam>
    /// <param name="lease">
    /// The caller's original canonical lease, whose ownership remains with the caller.
    /// </param>
    /// <param name="read">
    /// Original read-only source plan. Its exceptions propagate unchanged. Unsupported or nested
    /// routes retain their original reader semantics and the enclosing owner's completion contract.
    /// </param>
    /// <returns>
    /// Provisional internal DATA when borrowing the owning operation's mandatory effect/success fences.
    /// A standalone plan returns only after its own fresh byte, absence, timestamp and generation proof.
    /// Independent fresh protocols bypass the failed DATA owner without reviving its state.
    /// </returns>
    internal async Task<TResult> ReadStableRecoveryDataAsync<TResult>(CanonicalWriteLease lease, Func<Task<TResult>> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        if (_sourceDataOperation.Value is { } operation && ValidationReuseSuppression.Value == 0 && CanReuseSourceData)
        {
            if (operation.IsSuspended) return await read();
            operation.RequireOpen();
            operation.Observe(lease);
            return await read();
        }
        var readSet = TryBeginReservedValidationReadSetCore(lease, preserveOriginalAdmission: true);
        Exception? primaryFailure = null;
        try
        {
            using var timing = MeasureOperation("RecoveryProof.StableReadPlan");
            if (readSet is not null) await readSet.OpenAsync();
            var result = await read();
            if (readSet is not null) await readSet.CompleteAsync();
            return result;
        }
        catch (Exception error) { primaryFailure = error; throw; }
        finally
        {
            try { if (readSet is not null) await readSet.DisposeAsync(); }
            catch when (primaryFailure is not null) { /* Preserve the original plan or exit-proof refusal. */ }
        }
    }
}
