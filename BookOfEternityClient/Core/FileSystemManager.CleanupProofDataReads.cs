namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    /// <summary>
    /// Runs the original post-recovery completed-proof DATA reader for cleanup.
    /// </summary>
    /// <typeparam name="TResult">
    /// The result produced by the original read-only proof algorithm.
    /// </typeparam>
    /// <param name="lease">
    /// The active original physical lease, retained and disposed by its caller.
    /// </param>
    /// <param name="read">
    /// The original read-only proof after recovery and the recovered marker read.
    /// It must finish before another recovery or effect begins.
    /// </param>
    /// <returns>
    /// The original proof DATA after the selected reader's source contract succeeds.
    /// </returns>
    internal Task<TResult> ReadCleanupCompletedProofDataAsync<TResult>(CanonicalWriteLease lease,
        Func<Task<TResult>> read) => ReadStableRecoveryDataAsync(lease, read);
}
