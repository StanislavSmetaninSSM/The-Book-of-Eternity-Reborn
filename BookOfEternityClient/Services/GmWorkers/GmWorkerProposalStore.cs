using BookOfEternityClient.Core;
using System.Text;

namespace BookOfEternityClient.Services.GmWorkers;

public sealed class GmWorkerProposalStore
{
    public const string ProposalRoot = "worker_proposals";

    private readonly FileSystemManager _fs;
    private readonly Func<FileSystemManager.CanonicalWriteLease, string, byte[], Task> _publishInboxAsync;

    public GmWorkerProposalStore(FileSystemManager fs)
        : this(fs, (lease, path, content) => fs.WriteFileAtomicBytesAsync(lease, path, content))
    {
    }

    internal GmWorkerProposalStore(
        FileSystemManager fs,
        Func<FileSystemManager.CanonicalWriteLease, string, byte[], Task> publishInboxAsync)
    {
        _fs = fs;
        _publishInboxAsync = publishInboxAsync ?? throw new ArgumentNullException(nameof(publishInboxAsync));
    }

    internal async Task<WorkerProposalPublicationResult> PublishBundleAsync(
        WorkerProposal proposal,
        byte[] proposalBytes,
        IReadOnlyDictionary<string, byte[]> importedContent,
        string taskPath,
        byte[] expectedTaskBytes,
        string expectedSessionGeneration,
        string proposalInboxPath,
        Func<FileSystemManager.CanonicalWriteLease, Task>? publishDerivedAuditAsync = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsSafeId(proposal.ProposalId))
            return WorkerProposalPublicationResult.Rejected("Worker proposal id is unsafe.");
        if (IsReservedProposalId(proposal.ProposalId))
            return WorkerProposalPublicationResult.Rejected(
                "Worker proposal id is reserved for the derived proposal inbox namespace.");

        var stagingRoot = _fs.CreateRuntimeProposalStagingRoot();
        var stagingBundleRoot = Path.Combine(stagingRoot, proposal.ProposalId);
        var finalBundleRelativePath = $"{ProposalRoot}/{proposal.ProposalId}";
        var finalBundleRoot = _fs.ResolvePath(finalBundleRelativePath);
        var contentRefPrefix = $"{ProposalRoot}/{proposal.ProposalId}/";

        using var publicationAuthority = new WorkerProposalPublicationAuthority(cancellationToken);
        try
        {
            await WriteStagedFileAsync(
                ResolveStagedPath(stagingBundleRoot, "proposal.json"),
                proposalBytes,
                cancellationToken);
            foreach (var (contentRef, content) in importedContent)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!contentRef.StartsWith(contentRefPrefix, StringComparison.Ordinal))
                    return WorkerProposalPublicationResult.Rejected(
                        $"Worker proposal contentRef is outside its bundle: {contentRef}.");

                var relativePath = contentRef[contentRefPrefix.Length..];
                await WriteStagedFileAsync(
                    ResolveStagedPath(stagingBundleRoot, relativePath),
                    content,
                    cancellationToken);
            }

            await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync(
                cancellationToken: cancellationToken);
            if (!_fs.IsCurrentSessionGeneration(writeLease, expectedSessionGeneration))
            {
                return WorkerProposalPublicationResult.SessionWasReplaced(
                    "Worker task no longer belongs to the current game session generation.");
            }

            var currentTaskBytes = await _fs.ReadFileBytesAsync(taskPath);
            if (!ExactBytesEqual(currentTaskBytes, expectedTaskBytes))
            {
                return WorkerProposalPublicationResult.Rejected(
                    "Worker task no longer belongs to the current game session generation.");
            }

            if (Directory.Exists(finalBundleRoot) || _fs.FileExists(proposalInboxPath))
            {
                return WorkerProposalPublicationResult.Rejected(
                    $"Worker proposal id already exists and cannot be overwritten: {proposal.ProposalId}.");
            }

            if (!publicationAuthority.TryBeginPublication())
                throw new OperationCanceledException(cancellationToken);

            try
            {
                await _fs.MoveRuntimeDirectoryIntoCanonicalSessionAsync(
                    writeLease,
                    stagingBundleRoot,
                    finalBundleRelativePath);
            }
            catch (IOException) when (Directory.Exists(finalBundleRoot))
            {
                return WorkerProposalPublicationResult.Rejected(
                    $"Worker proposal id already exists and cannot be overwritten: {proposal.ProposalId}.");
            }

            string? warning = null;
            try
            {
                await _publishInboxAsync(writeLease, proposalInboxPath, proposalBytes);
            }
            catch (Exception ex)
            {
                warning = $"Proposal bundle is durable, but derived inbox publication failed: {ex.Message}";
            }

            if (publishDerivedAuditAsync != null)
            {
                try
                {
                    await publishDerivedAuditAsync(writeLease);
                }
                catch (Exception ex)
                {
                    warning = string.IsNullOrWhiteSpace(warning)
                        ? $"Proposal bundle is durable, but derived audit publication failed: {ex.Message}"
                        : warning + $" Derived audit publication also failed: {ex.Message}";
                }
            }

            return WorkerProposalPublicationResult.PublishedWithWarning(warning);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return WorkerProposalPublicationResult.Rejected(
                $"Worker proposal bundle publication failed: {ex.Message}");
        }
        finally
        {
            try
            {
                _fs.DeleteRuntimeProposalStagingRoot(stagingRoot);
            }
            catch (Exception)
            {
                // Staging cleanup cannot revoke a bundle that is already durable.
            }
        }
    }

    public async Task<WorkerProposal?> ReadProposalAsync(string proposalId)
    {
        if (!IsSafeId(proposalId) || IsReservedProposalId(proposalId))
            throw new ArgumentException(
                "Proposal id must be a safe, non-reserved lowercase identifier.",
                nameof(proposalId));

        var json = await _fs.ReadFileAsync(GetProposalPath(proposalId));
        return string.IsNullOrWhiteSpace(json)
            ? null
            : GmWorkerJson.Deserialize<WorkerProposal>(json);
    }

    public static string GetProposalPath(string proposalId) =>
        $"{ProposalRoot}/{proposalId}/proposal.json";

    private static bool IsSafeId(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.All(ch => char.IsLower(ch) || char.IsDigit(ch) || ch is '_' or '-');

    internal static bool IsReservedProposalId(string? value) =>
        string.Equals(value, "inbox", StringComparison.Ordinal);

    private static string ResolveStagedPath(string stagingBundleRoot, string relativePath)
    {
        var normalized = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalized))
            throw new InvalidDataException("Proposal bundle path must be relative.");

        var root = Path.GetFullPath(stagingBundleRoot);
        var candidate = Path.GetFullPath(Path.Combine(root, normalized));
        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Proposal bundle path escapes staging root.");

        return candidate;
    }

    private static async Task WriteStagedFileAsync(
        string path,
        byte[] content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(content, cancellationToken);
        stream.Flush(flushToDisk: true);
    }

    private static bool ExactBytesEqual(byte[]? left, byte[]? right) =>
        left == null ? right == null : right != null && left.AsSpan().SequenceEqual(right);

}

internal sealed class WorkerProposalPublicationAuthority : IDisposable
{
    private readonly object _sync = new();
    private readonly CancellationTokenRegistration _registration;
    private WorkerProposalPublicationState _state;

    internal WorkerProposalPublicationAuthority(CancellationToken cancellationToken)
    {
        _registration = cancellationToken.Register(
            static state => ((WorkerProposalPublicationAuthority)state!).CancelPendingPublication(),
            this);
    }

    internal bool TryBeginPublication()
    {
        lock (_sync)
        {
            if (_state == WorkerProposalPublicationState.Canceled)
                return false;
            if (_state != WorkerProposalPublicationState.Pending)
                throw new InvalidOperationException("Worker proposal publication was already decided.");

            _state = WorkerProposalPublicationState.Publishing;
            return true;
        }
    }

    public void Dispose() => _registration.Dispose();

    private void CancelPendingPublication()
    {
        lock (_sync)
        {
            if (_state == WorkerProposalPublicationState.Pending)
                _state = WorkerProposalPublicationState.Canceled;
        }
    }

    private enum WorkerProposalPublicationState
    {
        Pending,
        Canceled,
        Publishing
    }
}

internal sealed record WorkerProposalPublicationResult(
    bool Published,
    bool SessionReplaced,
    string? Error,
    string? Warning)
{
    internal static WorkerProposalPublicationResult Rejected(string error) => new(false, false, error, null);
    internal static WorkerProposalPublicationResult SessionWasReplaced(string error) => new(false, true, error, null);
    internal static WorkerProposalPublicationResult PublishedWithWarning(string? warning) => new(true, false, null, warning);
}
