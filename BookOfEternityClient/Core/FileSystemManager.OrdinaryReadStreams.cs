namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    /// <summary>
    /// Opens a validated ordinary canonical stream under the supplied participating-writer lease.
    /// </summary>
    /// <param name="lease">
    /// The active lease owned by this manager; this method never acquires another lease.
    /// </param>
    /// <param name="relativePath">
    /// The canonical file path relative to the current game session.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels preparation before the file is opened.
    /// </param>
    /// <returns>
    /// An opened ordinary file requiring completion or abandonment, or null when the validated name is absent.
    /// </returns>
    internal Task<OrdinaryReadFile?> OpenOrdinaryReadFileAsync(CanonicalWriteLease lease,
        string relativePath, CancellationToken cancellationToken = default) =>
        Task.FromException<OrdinaryReadFile?>(new NotSupportedException("Ordinary save-list stream implementation awaits the scoped RED gate."));

    /// <summary>
    /// Owns an ordinary seekable read stream while retaining its lease and completion-validation contract.
    /// </summary>
    internal sealed class OrdinaryReadFile : IAsyncDisposable
    {
        /// <summary>
        /// Exposes the opened file stream for raw preflight and bounded archive reads.
        /// </summary>
        internal FileStream Stream => throw new NotSupportedException();

        /// <summary>
        /// Returns the length of the opened file used for metadata inspection.
        /// </summary>
        internal long Length => throw new NotSupportedException();

        /// <summary>
        /// Validates the ordinary path, type and current lease after successful consumption.
        /// </summary>
        internal void Complete() => throw new NotSupportedException();

        /// <summary>
        /// Explicitly abandons a failed read without asserting successful completion.
        /// </summary>
        internal void Abandon() => throw new NotSupportedException();

        /// <summary>
        /// Closes the owned stream without releasing its caller's lease.
        /// </summary>
        /// <returns>
        /// Completion of stream disposal, rejecting an unresolved read lifetime.
        /// </returns>
        public ValueTask DisposeAsync() => throw new NotSupportedException();
    }
}
