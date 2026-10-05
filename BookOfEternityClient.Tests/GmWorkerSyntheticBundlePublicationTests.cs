using System.Text;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GmWorkerSyntheticBundlePublicationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-synthetic-bundle-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemManager _fs;
    private readonly WorkerProposal _proposal = GmWorkerBridgeTestFixtures.ValidationRepairProposal();
    private readonly byte[] _taskBytes = [0, 1, 0xFF];
    private readonly byte[] _content = [0, 0xFF, 0xEF, 0xBB, 0xBF];
    private readonly string _generation = Guid.NewGuid().ToString("N");
    private Func<Task>? _afterBoundary;
    private string TaskPath => GmWorkerBridgePool.GetTaskPacketPath(_proposal.TaskId);
    private string InboxPath => GmWorkerBridgePool.GetProposalInboxPath(_proposal.TaskId);
    private string BundlePath => $"{GmWorkerProposalStore.ProposalRoot}/{_proposal.ProposalId}";
    private string ContentPath => BundlePath + "/files/binary.bin";
    private byte[] ProposalBytes => Encoding.UTF8.GetBytes(GmWorkerJson.Serialize(_proposal));

    public GmWorkerSyntheticBundlePublicationTests()
    {
        Assert.True(OperatingSystem.IsLinux(), "This category qualifies the Linux fixture-only bundle backend.");
        Directory.CreateDirectory(_root);
        _fs = new(_root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                AfterCanonicalMutationBoundaryValidatedAsync = path =>
                    path == BundlePath ? _afterBoundary?.Invoke() ?? Task.CompletedTask : Task.CompletedTask
            });
        _fs.EnsureDirectoryStructure();
        Directory.CreateDirectory(Path.GetDirectoryName(_fs.SessionGenerationPath)!);
        File.WriteAllText(_fs.SessionGenerationPath, $$"""{"SchemaVersion":1,"GenerationId":"{{_generation}}"}""");
        Directory.CreateDirectory(Path.GetDirectoryName(_fs.ResolvePath(TaskPath))!);
        File.WriteAllBytes(_fs.ResolvePath(TaskPath), _taskBytes);
    }

    private Task<WorkerProposalPublicationResult> Publish(GmWorkerProposalStore store,
        string? expectedGeneration = null, byte[]? expectedTask = null, CancellationToken cancellationToken = default) =>
        store.PublishBundleAsync(_proposal, ProposalBytes, new Dictionary<string, byte[]> { [ContentPath] = _content },
            TaskPath, expectedTask ?? _taskBytes, expectedGeneration ?? _generation, InboxPath,
            lease => new GmWorkerAuditLog(_fs).AppendEventAsync(lease, new WorkerAuditEvent
            {
                EventId = "synthetic-bundle-published", EventType = "proposal-published", WorkerId = _proposal.WorkerId,
                TaskId = _proposal.TaskId, ProposalId = _proposal.ProposalId, TimestampUtc = "2026-10-05T00:00:00Z"
            }), cancellationToken);

    [Fact]
    public async Task ActualStore_PublishesCompleteBundleAndDerivedInboxAudit()
    {
        var admission = new GmWorkerNativePoolAdmission(Path.Combine(_root, "unused-package"), _root);
        var result = await Publish(admission.CreateProposalStore(_fs));
        Assert.True(result.Published, result.Error);
        Assert.Null(result.Warning);
        Assert.Equal(ProposalBytes, File.ReadAllBytes(_fs.ResolvePath(BundlePath + "/proposal.json")));
        Assert.Equal(_content, File.ReadAllBytes(_fs.ResolvePath(ContentPath)));
        Assert.Equal(ProposalBytes, File.ReadAllBytes(_fs.ResolvePath(InboxPath)));
        Assert.Contains("synthetic-bundle-published", File.ReadAllText(_fs.ResolvePath(GmWorkerAuditLog.AuditLogPath)));
        Assert.Equal(_taskBytes, File.ReadAllBytes(_fs.ResolvePath(TaskPath)));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(_fs.RuntimeRootPath, "proposal-staging")));
    }

    [Fact]
    public async Task DefaultStore_RetainsExistingLinuxDescriptorBackendRefusal()
    {
        var error = await Assert.ThrowsAsync<PlatformNotSupportedException>(() => Publish(new GmWorkerProposalStore(_fs)));
        Assert.Contains("Runtime proposal publication", error.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(_fs.ResolvePath(BundlePath)));
        Assert.False(File.Exists(_fs.ResolvePath(InboxPath)));
        Assert.False(File.Exists(_fs.ResolvePath(GmWorkerAuditLog.AuditLogPath)));
        Assert.Equal(_taskBytes, File.ReadAllBytes(_fs.ResolvePath(TaskPath)));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
