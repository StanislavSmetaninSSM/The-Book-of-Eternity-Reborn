using BookOfEternityClient.Core;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Services;

internal sealed class DarenRewardProfileRollbackTransaction : IDisposable
{
    private const string AuthorityName = "Daren reward profile";

    private readonly FileSystemManager _fs;
    private readonly FileSystemManager.CanonicalWriteLease _writeLease;
    private readonly string _profileDirectory;
    private readonly string _profilePath;
    private readonly PhysicalFileAuthority.StableDirectory _parentAuthority;
    private SafeFileHandle? _baselineHandle;
    private FileStream? _sourceStream;
    private ReversibleFilePublication.PendingPublication? _pendingPublication;
    private Func<
        PhysicalFileAuthority.FileIdentity,
        string,
        string,
        Task>? _recordPublishedAuthorityAsync;
    private byte[]? _publishedBytes;
    private bool _rolledBack;
    private bool _disposed;

    private DarenRewardProfileRollbackTransaction(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string profileDirectory,
        string profilePath,
        PhysicalFileAuthority.StableDirectory parentAuthority,
        PhysicalFileAuthority.FileIdentity? parentIdentity,
        SafeFileHandle? baselineHandle,
        PhysicalFileAuthority.FileIdentity? baselineIdentity,
        string? baselineSha256,
        byte[]? baselineBytes)
    {
        _fs = fs;
        _writeLease = writeLease;
        _profileDirectory = profileDirectory;
        _profilePath = profilePath;
        _parentAuthority = parentAuthority;
        ParentIdentity = parentIdentity;
        _baselineHandle = baselineHandle;
        BaselineIdentity = baselineIdentity;
        BaselineSha256 = baselineSha256;
        BaselineBytes = baselineBytes;
    }

    internal PhysicalFileAuthority.FileIdentity? ParentIdentity { get; }
    internal PhysicalFileAuthority.FileIdentity? BaselineIdentity { get; }
    internal string? BaselineSha256 { get; }
    internal byte[]? BaselineBytes { get; }
    internal bool BaselineExisted => BaselineIdentity != null;
    internal string? PublicationTransactionId =>
        _pendingPublication?.TransactionId;
    internal PhysicalFileAuthority.FileIdentity? PublishedIdentity =>
        _pendingPublication?.Result.PublishedIdentity;
    internal string? PublishedSha256 =>
        _pendingPublication?.Result.PublishedSha256;
    internal bool PublicationCommitted =>
        _pendingPublication?.IsCommitted == true;
    internal bool RetainedEvidence =>
        _pendingPublication?.RetainedEvidence == true;

    internal static DarenRewardProfileRollbackTransaction Capture(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        fs.EnsureCanonicalWriteLeaseActive(writeLease);
        fs.VerifyCurrentSessionOperation(writeLease);
        if (!fs.SupportsReversibleOpenedHandlePublication)
        {
            throw new PlatformNotSupportedException(
                "Daren profile rollback capture requires a reversible opened-handle backend.");
        }

        var profilePath = Path.Combine(
            fs.BasePath,
            DarenQteRewardProfileService.ProfileRelativePath.Replace(
                '/',
                Path.DirectorySeparatorChar));
        var profileDirectory = Path.GetDirectoryName(profilePath)
            ?? throw new InvalidDataException(
                "Daren reward profile has no physical parent.");
        var parentAuthority = PhysicalFileAuthority.EnsureStableDirectory(
            fs.BasePath,
            profileDirectory,
            AuthorityName);
        SafeFileHandle? baselineHandle = null;
        try
        {
            PhysicalFileAuthority.FileIdentity? parentIdentity = null;
            PhysicalFileAuthority.FileIdentity? baselineIdentity = null;
            string? baselineSha256 = null;
            byte[]? baselineBytes = null;
            parentIdentity = PhysicalFileAuthority.CaptureFileIdentity(
                parentAuthority.Handle!,
                AuthorityName + " parent");
            if (!parentIdentity.IsDirectory)
            {
                throw new InvalidDataException(
                    "Daren reward profile parent is not a directory.");
            }

            var profileEntry = PhysicalFileAuthority.ProbeNamespaceEntry(
                parentAuthority,
                profilePath,
                AuthorityName + " baseline");
            if (profileEntry ==
                PhysicalFileAuthority.NamespaceEntryKind.RegularFile)
            {
                baselineHandle = PhysicalFileAuthority.OpenForRename(
                    parentAuthority,
                    profilePath,
                    isDirectory: false,
                    AuthorityName + " baseline");
                baselineIdentity =
                    PhysicalFileAuthority.CaptureFileIdentity(
                        baselineHandle,
                        AuthorityName + " baseline");
                if (baselineIdentity.IsDirectory ||
                    baselineIdentity.NumberOfLinks != 1)
                {
                    throw new InvalidDataException(
                        "Daren reward profile baseline must be one single-link regular file.");
                }

                baselineBytes =
                    PhysicalFileAuthority.ReadOpenedFileBytes(
                        baselineHandle,
                        AuthorityName + " baseline");
                baselineSha256 =
                    PhysicalFileAuthority.ComputeOpenedFileSha256(
                        baselineHandle,
                        AuthorityName + " baseline");
                PhysicalFileAuthority.EnsureExactFileIdentity(
                    baselineHandle,
                    profilePath,
                    baselineIdentity,
                    AuthorityName + " baseline completion");
            }
            else if (profileEntry !=
                     PhysicalFileAuthority.NamespaceEntryKind.Missing)
            {
                throw new InvalidDataException(
                    "Daren reward profile baseline is not a physical regular file.");
            }

            return new DarenRewardProfileRollbackTransaction(
                fs,
                writeLease,
                profileDirectory,
                profilePath,
                parentAuthority,
                parentIdentity,
                baselineHandle,
                baselineIdentity,
                baselineSha256,
                baselineBytes);
        }
        catch
        {
            baselineHandle?.Dispose();
            parentAuthority.Dispose();
            throw;
        }
    }

    internal void SetPublishedAuthorityRecorder(
        Func<
            PhysicalFileAuthority.FileIdentity,
            string,
            string,
            Task> recorder)
    {
        ThrowIfDisposed();
        _recordPublishedAuthorityAsync = recorder ??
            throw new ArgumentNullException(nameof(recorder));
    }

    internal byte[]? ReadCurrentBytes()
    {
        EnsureActive();
        if (_pendingPublication != null)
        {
            _pendingPublication.ValidateForCommit();
            return _publishedBytes?.ToArray();
        }

        ValidateBaselineOrAbsence();
        return BaselineBytes?.ToArray();
    }

    internal void EnsurePublicationSupported()
    {
        EnsureActive();
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Daren profile rollback publication requires a reversible opened-handle backend.");
        }
    }

    internal async Task PublishAsync(
        byte[] content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        EnsurePublicationSupported();
        if (_pendingPublication != null)
        {
            throw new InvalidOperationException(
                "Daren reward profile was already published in this transaction.");
        }

        ValidateParent();
        ValidateBaselineOrAbsence();
        var tempPath = Path.Combine(
            _profileDirectory,
            $".qte_showcase_rewards.{Guid.NewGuid():N}.tmp");
        FileStream? sourceStream = null;
        try
        {
            sourceStream = PhysicalFileAuthority.CreateNewWritableFile(
                _parentAuthority,
                tempPath,
                AuthorityName + " temporary",
                asynchronous: true);
            await sourceStream.WriteAsync(content, cancellationToken);
            await sourceStream.FlushAsync(cancellationToken);
            sourceStream.Flush(flushToDisk: true);
            _fs.VerifyCurrentSessionOperation(_writeLease);
            ValidateParent();
            ValidateBaselineOrAbsence();

            var pending =
                await ReversibleFilePublication.PublishDeferredAsync(
                    _fs.BasePath,
                    _fs.PhysicalPublicationTransactionsRootPath,
                    _parentAuthority,
                    tempPath,
                    sourceStream,
                    _parentAuthority,
                    _profilePath,
                    AuthorityName,
                    _baselineHandle,
                    afterAuthorityValidated: null,
                    beforeSourcePublished: null,
                    afterPublished: null,
                    cancellationToken);
            _pendingPublication = pending;
            _sourceStream = sourceStream;
            sourceStream = null;
            _publishedBytes = content.ToArray();

            var recorder = _recordPublishedAuthorityAsync ??
                throw new InvalidOperationException(
                    "Daren published authority recorder is not configured.");
            await recorder(
                pending.Result.PublishedIdentity,
                pending.Result.PublishedSha256,
                pending.TransactionId);
            pending.ValidateForCommit();
            ValidateParent();
            _fs.VerifyCurrentSessionOperation(_writeLease);
        }
        catch
        {
            if (_pendingPublication is { IsCommitted: false } pending)
            {
                try
                {
                    pending.RollBack();
                    _rolledBack = true;
                }
                catch
                {
                    // The durable publication journal retains unresolved evidence.
                }
            }

            throw;
        }
        finally
        {
            if (sourceStream != null)
            {
                await sourceStream.DisposeAsync();
                try
                {
                    PhysicalFileAuthority.TryDeleteFile(
                        _parentAuthority,
                        tempPath,
                        AuthorityName + " temporary cleanup");
                }
                catch
                {
                    // Durable publication evidence takes precedence over temp cleanup.
                }
            }
        }
    }

    internal void ValidateForCommit()
    {
        EnsureActive();
        ValidateParent();
        if (_pendingPublication != null)
            _pendingPublication.ValidateForCommit();
        else
            ValidateBaselineOrAbsence();
    }

    internal void Commit()
    {
        ValidateForCommit();
        _pendingPublication?.Commit();
    }

    internal void RollBack()
    {
        EnsureActive();
        if (_pendingPublication is { IsCommitted: true })
        {
            throw new InvalidDataException(
                "Committed Daren profile publication cannot be rolled back.");
        }

        _pendingPublication?.RollBack();
        _rolledBack = true;
        ValidateParent();
        ValidateBaselineOrAbsence();
        if (RetainedEvidence)
        {
            throw new InvalidDataException(
                "Daren profile baseline was restored, but linked post-image evidence was retained.");
        }
    }

    internal bool TryAcknowledgeCommittedJournal() =>
        _pendingPublication?.TryAcknowledgeCommittedJournal() ?? true;

    internal void ValidateCommittedForCleanup()
    {
        EnsureActive();
        if (_pendingPublication is not { IsCommitted: true } pending ||
            _sourceStream == null)
        {
            throw new InvalidDataException(
                "Daren committed publication authority is unavailable.");
        }

        ValidateParent();
        PhysicalFileAuthority.EnsureExactFileIdentity(
            _sourceStream.SafeFileHandle,
            _profilePath,
            pending.Result.PublishedIdentity,
            AuthorityName + " committed post-image");
        var actualSha256 =
            PhysicalFileAuthority.ComputeOpenedFileSha256(
                _sourceStream.SafeFileHandle,
                AuthorityName + " committed post-image");
        if (!string.Equals(
                actualSha256,
                pending.Result.PublishedSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Daren committed post-image bytes changed.");
        }
    }

    internal static void VerifyRecoveredFileState(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        PhysicalFileAuthority.FileIdentity parentIdentity,
        PhysicalFileAuthority.FileIdentity? expectedFileIdentity,
        string? expectedSha256,
        bool expectExistence,
        string authorityName)
    {
        fs.EnsureCanonicalWriteLeaseActive(writeLease);
        fs.VerifyCurrentSessionOperation(writeLease);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Daren recovered physical identity validation is available only on Windows.");
        }

        var profilePath = Path.Combine(
            fs.BasePath,
            DarenQteRewardProfileService.ProfileRelativePath.Replace(
                '/',
                Path.DirectorySeparatorChar));
        var profileDirectory = Path.GetDirectoryName(profilePath)
            ?? throw new InvalidDataException(
                "Daren reward profile has no physical parent.");
        using var parentAuthority =
            PhysicalFileAuthority.EnsureStableDirectory(
                fs.BasePath,
                profileDirectory,
                authorityName);
        PhysicalFileAuthority.EnsureExactDirectoryIdentity(
            parentAuthority,
            parentIdentity,
            authorityName + " parent");
        if (!expectExistence)
        {
            if (PhysicalFileAuthority.ProbeNamespaceEntry(
                    parentAuthority,
                    profilePath,
                    authorityName + " absence") !=
                PhysicalFileAuthority.NamespaceEntryKind.Missing)
            {
                throw new InvalidDataException(
                    $"{authorityName} expected exact profile absence.");
            }

            return;
        }

        if (expectedFileIdentity == null ||
            string.IsNullOrWhiteSpace(expectedSha256))
        {
            throw new InvalidDataException(
                $"{authorityName} expected file authority is incomplete.");
        }

        using var handle = PhysicalFileAuthority.OpenForRename(
            parentAuthority,
            profilePath,
            isDirectory: false,
            authorityName);
        PhysicalFileAuthority.EnsureExactFileIdentity(
            handle,
            profilePath,
            expectedFileIdentity,
            authorityName);
        var actualSha256 =
            PhysicalFileAuthority.ComputeOpenedFileSha256(
                handle,
                authorityName);
        if (!string.Equals(
                actualSha256,
                expectedSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"{authorityName} exact-byte hash changed.");
        }
    }

    internal static void RestoreRecoveredBaseline(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        PhysicalFileAuthority.FileIdentity parentIdentity,
        PhysicalFileAuthority.FileIdentity? baselineIdentity,
        string? baselineSha256,
        byte[]? baselineBytes,
        bool baselineExisted)
    {
        if (!baselineExisted)
        {
            VerifyRecoveredFileState(
                fs,
                writeLease,
                parentIdentity,
                expectedFileIdentity: null,
                expectedSha256: null,
                expectExistence: false,
                authorityName: "Daren restored baseline absence");
            return;
        }
        if (baselineIdentity == null ||
            string.IsNullOrWhiteSpace(baselineSha256) ||
            baselineBytes == null)
        {
            throw new InvalidDataException(
                "Daren baseline recovery authority is incomplete.");
        }

        fs.EnsureCanonicalWriteLeaseActive(writeLease);
        fs.VerifyCurrentSessionOperation(writeLease);
        var profilePath = Path.Combine(
            fs.BasePath,
            DarenQteRewardProfileService.ProfileRelativePath.Replace(
                '/',
                Path.DirectorySeparatorChar));
        var profileDirectory = Path.GetDirectoryName(profilePath)
            ?? throw new InvalidDataException(
                "Daren reward profile has no physical parent.");
        using var parentAuthority =
            PhysicalFileAuthority.EnsureStableDirectory(
                fs.BasePath,
                profileDirectory,
                "Daren baseline recovery");
        PhysicalFileAuthority.EnsureExactDirectoryIdentity(
            parentAuthority,
            parentIdentity,
            "Daren baseline recovery parent");
        using var baselineHandle = PhysicalFileAuthority.OpenForRename(
            parentAuthority,
            profilePath,
            isDirectory: false,
            "Daren baseline recovery",
            writable: true);
        PhysicalFileAuthority.EnsureExactFileIdentity(
            baselineHandle,
            profilePath,
            baselineIdentity,
            "Daren baseline recovery");
        var currentSha256 =
            PhysicalFileAuthority.ComputeOpenedFileSha256(
                baselineHandle,
                "Daren baseline recovery");
        if (!string.Equals(
                currentSha256,
                baselineSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            PhysicalFileAuthority.WriteOpenedFileBytes(
                baselineHandle,
                baselineBytes,
                "Daren baseline recovery");
        }

        PhysicalFileAuthority.EnsureExactFileIdentity(
            baselineHandle,
            profilePath,
            baselineIdentity,
            "Daren baseline recovery completion");
        var restoredSha256 =
            PhysicalFileAuthority.ComputeOpenedFileSha256(
                baselineHandle,
                "Daren baseline recovery completion");
        if (!string.Equals(
                restoredSha256,
                baselineSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Daren baseline exact-byte recovery failed.");
        }
    }

    private void ValidateParent()
    {
        if (ParentIdentity == null)
            return;
        PhysicalFileAuthority.EnsureExactDirectoryIdentity(
            _parentAuthority,
            ParentIdentity,
            AuthorityName + " parent");
    }

    private void ValidateBaselineOrAbsence()
    {
        ValidateParent();
        if (BaselineIdentity == null)
        {
            if ((_pendingPublication == null || _rolledBack) &&
                PhysicalFileAuthority.ProbeNamespaceEntry(
                    _parentAuthority,
                    _profilePath,
                    AuthorityName + " baseline absence") !=
                PhysicalFileAuthority.NamespaceEntryKind.Missing)
            {
                throw new InvalidDataException(
                    "Daren reward profile baseline absence changed.");
            }

            return;
        }

        var baselineHandle = _baselineHandle ??
            throw new InvalidDataException(
                "Daren reward profile baseline authority is unavailable.");
        var expectedPath = _pendingPublication == null || _rolledBack
            ? _profilePath
            : Path.Combine(
                _profileDirectory,
                $".boe-prior-{_pendingPublication.TransactionId}.quarantine");
        PhysicalFileAuthority.EnsureExactFileIdentity(
            baselineHandle,
            expectedPath,
            BaselineIdentity,
            AuthorityName + " baseline");
        var actualSha256 =
            PhysicalFileAuthority.ComputeOpenedFileSha256(
                baselineHandle,
                AuthorityName + " baseline");
        if (!string.Equals(
                actualSha256,
                BaselineSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Daren reward profile baseline bytes changed.");
        }
    }

    private void EnsureActive()
    {
        ThrowIfDisposed();
        _fs.EnsureCanonicalWriteLeaseActive(_writeLease);
        _fs.VerifyCurrentSessionOperation(_writeLease);
        if (!ReferenceEquals(
                _writeLease.ExternalPublicationContext,
                this))
        {
            throw new InvalidOperationException(
                "Daren reward profile transaction is not active on this write lease.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _pendingPublication?.Dispose();
        _pendingPublication = null;
        _sourceStream?.Dispose();
        _sourceStream = null;
        _baselineHandle?.Dispose();
        _baselineHandle = null;
        _parentAuthority.Dispose();
    }
}
