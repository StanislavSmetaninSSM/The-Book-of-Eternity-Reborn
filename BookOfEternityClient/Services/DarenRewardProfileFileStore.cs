using System.Text;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed class DarenRewardProfileFileStore
{
    private const string AuthorityName = "Daren reward profile";

    private readonly FileSystemManager _fs;
    private readonly string _profileDirectory;
    private readonly string _profilePath;

    internal DarenRewardProfileFileStore(FileSystemManager fs)
    {
        _fs = fs;
        _profilePath = Path.Combine(
            fs.BasePath,
            DarenQteRewardProfileService.ProfileRelativePath.Replace(
                '/',
                Path.DirectorySeparatorChar));
        _profileDirectory = Path.GetDirectoryName(_profilePath)
            ?? throw new InvalidDataException(
                "Daren reward profile has no physical parent.");
    }

    internal async Task<byte[]?> ReadExactBytesAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        CancellationToken cancellationToken = default)
    {
        EnsureLease(writeLease);
        if (writeLease.ExternalPublicationContext is
            DarenRewardProfileRollbackTransaction transaction)
        {
            return transaction.ReadCurrentBytes();
        }

        var profileEntry = ProbeProfileEntry();
        if (profileEntry ==
            PhysicalFileAuthority.NamespaceEntryKind.Missing)
        {
            return null;
        }
        EnsureRegularProfileEntry(profileEntry);

        using var parentAuthority = OpenExistingParentAuthority();
        await using var stream = PhysicalFileAuthority.OpenReadFile(
            parentAuthority,
            _profilePath,
            AuthorityName,
            asynchronous: true) ??
            throw new InvalidDataException(
                "Daren reward profile changed after exact classification.");

        using var output = new MemoryStream();
        await stream.CopyToAsync(output, cancellationToken);
        PhysicalFileAuthority.EnsureHandleMatchesExpectedPath(
            stream.SafeFileHandle,
            _profilePath,
            AuthorityName);
        _fs.VerifyCurrentSessionOperation(writeLease);
        return output.ToArray();
    }

    internal async Task<string?> ReadTextAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        CancellationToken cancellationToken = default)
    {
        var content = await ReadExactBytesAsync(writeLease, cancellationToken);
        if (content == null)
            return null;

        using var stream = new MemoryStream(content, writable: false);
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    internal async Task WriteTextAtomicAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string content,
        CancellationToken cancellationToken = default) =>
        await WriteExactBytesAtomicAsync(
            writeLease,
            Encoding.UTF8.GetBytes(content),
            cancellationToken);

    internal async Task WriteExactBytesAtomicAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        EnsureLease(writeLease);
        if (writeLease.ExternalPublicationContext is
            DarenRewardProfileRollbackTransaction transaction)
        {
            await transaction.PublishAsync(content, cancellationToken);
            return;
        }

        var destinationEntry = ProbeProfileEntry();
        _fs.EnsureAuthorityFilePublicationSupported(
            destinationEntry,
            AuthorityName);

        using var parentAuthority = OpenParentAuthority();
        var tempPath = Path.Combine(
            _profileDirectory,
            $".qte_showcase_rewards.{Guid.NewGuid():N}.tmp");
        FileStream? stream = null;
        try
        {
            stream = PhysicalFileAuthority.CreateNewWritableFile(
                parentAuthority,
                tempPath,
                AuthorityName + " temporary",
                asynchronous: true);
            await stream.WriteAsync(content, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            stream.Flush(flushToDisk: true);
            _fs.VerifyCurrentSessionOperation(writeLease);

            if (_fs.SupportsReversibleOpenedHandlePublication)
            {
                await ReversibleFilePublication.PublishAsync(
                    _fs.BasePath,
                    _fs.PhysicalPublicationTransactionsRootPath,
                    parentAuthority,
                    tempPath,
                    stream,
                    parentAuthority,
                    _profilePath,
                    AuthorityName,
                    afterAuthorityValidated: null,
                    beforeSourcePublished: null,
                    afterPublished: null,
                    cancellationToken);
            }
            else
            {
                PhysicalFileAuthority.RenameOpenedObjectRelative(
                    stream.SafeFileHandle,
                    parentAuthority,
                    _profilePath,
                    replaceExisting: false,
                    AuthorityName + " create-only publication");
            }

            _fs.VerifyCurrentSessionOperation(writeLease);
        }
        finally
        {
            if (stream != null)
                await stream.DisposeAsync();
            try
            {
                PhysicalFileAuthority.TryDeleteFile(
                    parentAuthority,
                    tempPath,
                    AuthorityName + " temporary cleanup");
            }
            catch
            {
                // A rejected path replacement must not redirect cleanup.
            }
        }
    }

    internal void EnsureWriteSupported(
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        EnsureLease(writeLease);
        if (writeLease.ExternalPublicationContext is
            DarenRewardProfileRollbackTransaction transaction)
        {
            transaction.EnsurePublicationSupported();
            return;
        }

        _fs.EnsureAuthorityFilePublicationSupported(
            ProbeProfileEntry(),
            AuthorityName);
    }

    internal async Task RestoreExactBytesAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        byte[]? content)
    {
        EnsureLease(writeLease);
        if (content != null)
        {
            await WriteExactBytesAtomicAsync(writeLease, content);
            return;
        }

        var profileEntry = ProbeProfileEntry();
        if (profileEntry ==
            PhysicalFileAuthority.NamespaceEntryKind.Missing)
        {
            return;
        }
        EnsureRegularProfileEntry(profileEntry);

        using var parentAuthority = OpenExistingParentAuthority();
        PhysicalFileAuthority.TryDeleteFile(
            parentAuthority,
            _profilePath,
            AuthorityName + " rollback deletion");
        _fs.VerifyCurrentSessionOperation(writeLease);
    }

    private PhysicalFileAuthority.StableDirectory OpenParentAuthority() =>
        PhysicalFileAuthority.EnsureStableDirectory(
            _fs.BasePath,
            _profileDirectory,
            AuthorityName);

    private PhysicalFileAuthority.StableDirectory OpenExistingParentAuthority() =>
        PhysicalFileAuthority.OpenStableDirectory(
            _profileDirectory,
            AuthorityName);

    private PhysicalFileAuthority.NamespaceEntryKind ProbeProfileEntry() =>
        PhysicalFileAuthority.ProbeNamespaceEntryFromRoot(
            _fs.BasePath,
            _profilePath,
            AuthorityName);

    private static void EnsureRegularProfileEntry(
        PhysicalFileAuthority.NamespaceEntryKind profileEntry)
    {
        if (profileEntry !=
            PhysicalFileAuthority.NamespaceEntryKind.RegularFile)
        {
            throw new InvalidDataException(
                "Daren reward profile is not a physical regular file.");
        }
    }

    private void EnsureLease(
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        _fs.EnsureCanonicalWriteLeaseActive(writeLease);
        _fs.VerifyCurrentSessionOperation(writeLease);
    }
}
