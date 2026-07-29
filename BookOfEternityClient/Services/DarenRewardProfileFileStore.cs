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

        using var parentAuthority = OpenParentAuthority();
        await using var stream = PhysicalFileAuthority.OpenReadFile(
            parentAuthority,
            _profilePath,
            AuthorityName,
            asynchronous: true);
        if (stream == null)
            return null;

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

        var destinationExists =
            File.Exists(_profilePath) ||
            Directory.Exists(_profilePath);
        if (destinationExists && !OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Daren profile overwrite requires a reversible opened-handle backend.");
        }

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

            if (OperatingSystem.IsWindows())
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
                File.Move(tempPath, _profilePath, overwrite: false);
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

        if (!OperatingSystem.IsWindows() &&
            (File.Exists(_profilePath) ||
             Directory.Exists(_profilePath)))
        {
            throw new PlatformNotSupportedException(
                "Daren profile overwrite requires a reversible opened-handle backend.");
        }
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

        using var parentAuthority = OpenParentAuthority();
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

    private void EnsureLease(
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        _fs.EnsureCanonicalWriteLeaseActive(writeLease);
        _fs.VerifyCurrentSessionOperation(writeLease);
    }
}
