namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    /// <summary>
    /// Runs a source DATA read plan at the caller's stable post-recovery boundary.
    /// </summary>
    /// <typeparam name="TResult">
    /// The result produced by the original plan; this method does not retain authority objects.
    /// </typeparam>
    /// <param name="lease">
    /// The caller's original canonical lease, whose ownership remains with the caller.
    /// </param>
    /// <param name="read">
    /// Original read-only source plan. Its exceptions propagate unchanged.
    /// </param>
    /// <returns>
    /// The original source result after the read plan finishes.
    /// </returns>
    internal Task<TResult> ReadStableRecoveryDataAsync<TResult>(CanonicalWriteLease lease, Func<Task<TResult>> read) => read();
}
