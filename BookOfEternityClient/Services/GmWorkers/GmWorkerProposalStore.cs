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

    public async Task<string> SaveProposalAsync(WorkerProposal proposal)
    {
        if (!IsSafeId(proposal.ProposalId))
            throw new ArgumentException("Proposal id must be a safe lowercase identifier.", nameof(proposal));

        var path = GetProposalPath(proposal.ProposalId);
        var proposalRootPath = _fs.ResolvePath($"{ProposalRoot}/{proposal.ProposalId}");
        var proposalBytes = EncodeUtf8WithPreamble(GmWorkerJson.Serialize(proposal));
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        if (Directory.Exists(proposalRootPath) ||
            await _fs.CompareExchangeFileBytesAsync(
                writeLease,
                path,
                expectedContent: null,
                desiredContent: proposalBytes) != CanonicalFileMutationResult.Applied)
        {
            throw new IOException($"Worker proposal id already exists: {proposal.ProposalId}.");
        }

        return path;
    }

    internal async Task<WorkerProposalPublicationResult> PublishBundleAsync(
        WorkerProposal proposal,
        byte[] proposalBytes,
        IReadOnlyDictionary<string, byte[]> importedContent,
        string taskPath,
        byte[] expectedTaskBytes,
        string proposalInboxPath,
        Func<FileSystemManager.CanonicalWriteLease, Task>? publishDerivedAuditAsync = null)
    {
        if (!IsSafeId(proposal.ProposalId))
            return WorkerProposalPublicationResult.Rejected("Worker proposal id is unsafe.");

        var stagingRoot = Path.Combine(
            _fs.BasePath,
            ".boe_runtime",
            "proposal-staging",
            Guid.NewGuid().ToString("N"));
        var stagingBundleRoot = Path.Combine(stagingRoot, proposal.ProposalId);
        var finalBundleRoot = _fs.ResolvePath($"{ProposalRoot}/{proposal.ProposalId}");
        var contentRefPrefix = $"{ProposalRoot}/{proposal.ProposalId}/";

        try
        {
            await WriteStagedFileAsync(
                ResolveStagedPath(stagingBundleRoot, "proposal.json"),
                proposalBytes);
            foreach (var (contentRef, content) in importedContent)
            {
                if (!contentRef.StartsWith(contentRefPrefix, StringComparison.Ordinal))
                    return WorkerProposalPublicationResult.Rejected(
                        $"Worker proposal contentRef is outside its bundle: {contentRef}.");

                var relativePath = contentRef[contentRefPrefix.Length..];
                await WriteStagedFileAsync(
                    ResolveStagedPath(stagingBundleRoot, relativePath),
                    content);
            }

            await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
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

            Directory.CreateDirectory(Path.GetDirectoryName(finalBundleRoot)!);
            try
            {
                Directory.Move(stagingBundleRoot, finalBundleRoot);
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
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                warning = $"Proposal bundle is durable, but derived inbox publication failed: {ex.Message}";
            }

            if (publishDerivedAuditAsync != null)
            {
                try
                {
                    await publishDerivedAuditAsync(writeLease);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
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
                if (Directory.Exists(stagingRoot))
                    Directory.Delete(stagingRoot, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Staging cleanup cannot revoke a bundle that is already durable.
            }
        }
    }

    public async Task<WorkerProposal?> ReadProposalAsync(string proposalId)
    {
        if (!IsSafeId(proposalId))
            throw new ArgumentException("Proposal id must be a safe lowercase identifier.", nameof(proposalId));

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

    private static async Task WriteStagedFileAsync(string path, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(content);
        stream.Flush(flushToDisk: true);
    }

    private static bool ExactBytesEqual(byte[]? left, byte[]? right) =>
        left == null ? right == null : right != null && left.AsSpan().SequenceEqual(right);

    private static byte[] EncodeUtf8WithPreamble(string content)
    {
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(content);
        var bytes = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);
        return bytes;
    }
}

internal sealed record WorkerProposalPublicationResult(bool Published, string? Error, string? Warning)
{
    internal static WorkerProposalPublicationResult Rejected(string error) => new(false, error, null);
    internal static WorkerProposalPublicationResult PublishedWithWarning(string? warning) => new(true, null, warning);
}
