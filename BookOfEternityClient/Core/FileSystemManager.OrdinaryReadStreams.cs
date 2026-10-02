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
    /// An opened ordinary file requiring completion or abandonment, or <see langword="null"/> when the validated name is absent.
    /// </returns>
    internal async Task<OrdinaryReadFile?> OpenOrdinaryReadFileAsync(CanonicalWriteLease lease,
        string relativePath, CancellationToken cancellationToken = default)
    {
        EnsureValidCanonicalWriteLease(lease);
        cancellationToken.ThrowIfCancellationRequested();
        ResolveBackupPublicationRecovery(lease);
        if (!UsesTrustedLocalWriter(lease, relativePath))
            throw new InvalidOperationException("An ordinary stream cannot replace an original transaction's physical authority.");
        var expectedPath = ResolvePath(relativePath);
        var scope = new TrustedLocalFileScope([GameSessionPath]);
        EnsureCanonicalPathStillSafe(relativePath, expectedPath);
        if (!File.Exists(scope.ValidateFile(expectedPath))) return null;
        await InvokeBeforeCanonicalReadOpenAsync(relativePath);
        cancellationToken.ThrowIfCancellationRequested();
        ResolveBackupPublicationRecovery(lease);
        EnsureCanonicalPathStillSafe(relativePath, expectedPath);
        // The common check rejects links and Linux special files before any open.
        // Linux follows this implementation but its native execution remains a separate qualification.
        FileStream? stream = null;
        try
        {
            stream = OpenValidatedOrdinaryFile(scope, expectedPath, asynchronous: true);
            if (stream == null) return null;
            if (_hooks?.AfterCanonicalReadInitialValidationAsync != null)
                await _hooks.AfterCanonicalReadInitialValidationAsync(relativePath);
            ResolveBackupPublicationRecovery(lease);
            return new OrdinaryReadFile(this, lease, scope, relativePath, expectedPath, stream);
        }
        catch
        {
            if (stream != null) await stream.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Owns an ordinary seekable read stream while retaining its lease and completion-validation contract.
    /// </summary>
    internal sealed class OrdinaryReadFile : IAsyncDisposable
    {
        private readonly FileSystemManager _owner;
        private readonly CanonicalWriteLease _lease;
        private readonly TrustedLocalFileScope _scope;
        private readonly string _relativePath;
        private readonly string _expectedPath;
        private FileStream? _stream;
        private bool _completionResolved;

        /// <summary>
        /// Retains the opened ordinary file and its caller-owned validation context.
        /// </summary>
        /// <param name="owner">
        /// The manager responsible for the canonical namespace.
        /// </param>
        /// <param name="lease">
        /// The caller's active canonical lease, which this object never releases.
        /// </param>
        /// <param name="scope">
        /// The validated ordinary game-session scope.
        /// </param>
        /// <param name="relativePath">
        /// The canonical member's relative path.
        /// </param>
        /// <param name="expectedPath">
        /// The full path validated before opening.
        /// </param>
        /// <param name="stream">
        /// The opened seekable file stream whose disposal this object owns.
        /// </param>
        internal OrdinaryReadFile(FileSystemManager owner, CanonicalWriteLease lease, TrustedLocalFileScope scope,
            string relativePath, string expectedPath, FileStream stream)
        {
            _owner = owner; _lease = lease; _scope = scope; _relativePath = relativePath; _expectedPath = expectedPath;
            _stream = stream; Length = stream.Length;
        }

        /// <summary>
        /// Exposes the opened file stream for raw preflight and bounded archive reads.
        /// </summary>
        internal FileStream Stream => _stream ?? throw new ObjectDisposedException(nameof(OrdinaryReadFile));

        /// <summary>
        /// Returns the length of the opened file used for metadata inspection.
        /// </summary>
        internal long Length { get; }

        /// <summary>
        /// Validates the ordinary path, type and current lease after successful consumption.
        /// </summary>
        internal void Complete()
        {
            _ = Stream;
            _owner.ResolveBackupPublicationRecovery(_lease);
            _owner.CompleteOrdinaryRead(_scope, _relativePath, _expectedPath);
            if (Stream.Length != Length)
                throw new InvalidDataException("The ordinary file length changed during stream consumption.");
            _completionResolved = true;
        }

        /// <summary>
        /// Explicitly abandons a failed read without asserting successful completion.
        /// </summary>
        internal void Abandon() { _ = Stream; _completionResolved = true; }

        /// <summary>
        /// Closes the owned stream without releasing its caller's lease.
        /// </summary>
        /// <returns>
        /// Completion of stream disposal, rejecting an unresolved read lifetime.
        /// </returns>
        public async ValueTask DisposeAsync()
        {
            var stream = _stream; _stream = null;
            if (stream != null) await stream.DisposeAsync();
            if (stream != null && !_completionResolved)
                throw new InvalidOperationException("The ordinary file was disposed without completion validation or explicit abandonment.");
        }
    }
}
