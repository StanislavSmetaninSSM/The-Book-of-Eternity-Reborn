namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    /// <summary>
    /// Coalesces source DATA reads at a stable post-recovery boundary and proves them freshly before returning.
    /// </summary>
    /// <typeparam name="TResult">
    /// The result produced by the original plan; this method does not retain authority objects.
    /// </typeparam>
    /// <param name="lease">
    /// The caller's original canonical lease, whose ownership remains with the caller.
    /// </param>
    /// <param name="read">
    /// Original read-only source plan. Its exceptions propagate unchanged. Unsupported or nested
    /// routes retain their original reader semantics and an enclosing read-set owner's completion contract.
    /// </param>
    /// <returns>
    /// The original result after the owned source set passes fresh byte, absence, timestamp and
    /// generation checks. No result is returned when the plan or its exit proof fails.
    /// </returns>
    internal async Task<TResult> ReadStableRecoveryDataAsync<TResult>(CanonicalWriteLease lease, Func<Task<TResult>> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        var readSet = TryBeginReservedValidationReadSet(lease);
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
