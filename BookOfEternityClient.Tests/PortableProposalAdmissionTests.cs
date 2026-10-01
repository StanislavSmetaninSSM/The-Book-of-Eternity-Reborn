using System.Text;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PortableProposalAdmissionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-proposal-admission-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemManager _files;
    private readonly WorkerProposal _proposal = GmWorkerBridgeTestFixtures.NarrativeDraftProposal();
    private readonly string _generation = Guid.NewGuid().ToString("N");
    private readonly byte[] _taskBytes = [1, 2, 3];
    private readonly byte[] _generationBytes;
    private string TaskPath => GmWorkerBridgePool.GetTaskPacketPath(_proposal.TaskId);
    private string InboxPath => GmWorkerBridgePool.GetProposalInboxPath(_proposal.TaskId);
    private string Destination => _files.ResolvePath($"{GmWorkerProposalStore.ProposalRoot}/{_proposal.ProposalId}");

    public PortableProposalAdmissionTests()
    {
        Directory.CreateDirectory(_root);
        _files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        _files.EnsureDirectoryStructure();
        _generationBytes = Encoding.UTF8.GetBytes("{\"SchemaVersion\":1,\"GenerationId\":\"" + _generation + "\"}");
        Directory.CreateDirectory(Path.GetDirectoryName(_files.SessionGenerationPath)!);
        File.WriteAllBytes(_files.SessionGenerationPath, _generationBytes);
        var task = _files.ResolvePath(TaskPath); Directory.CreateDirectory(Path.GetDirectoryName(task)!);
        File.WriteAllBytes(task, _taskBytes);
    }

    private Task<WorkerProposalPublicationResult> Publish(string? expectedGeneration = null, byte[]? expectedTask = null) =>
        new GmWorkerProposalStore(_files).PublishBundleAsync(_proposal,
            Encoding.UTF8.GetBytes(GmWorkerJson.Serialize(_proposal)), new Dictionary<string, byte[]>(),
            TaskPath, expectedTask ?? _taskBytes, expectedGeneration ?? _generation, InboxPath);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyDestinationReachesExistingBackendAfterActualStoreAdmission(bool nested)
    {
        var destination = Destination;
        Directory.CreateDirectory(nested ? Path.Combine(destination, "empty", "deep") : destination);
        if (OperatingSystem.IsWindows())
        {
            var result = await Publish();
            Assert.True(result.Published, result.Error);
            Assert.Equal(Encoding.UTF8.GetBytes(GmWorkerJson.Serialize(_proposal)),
                File.ReadAllBytes(_files.ResolvePath(GmWorkerProposalStore.GetProposalPath(_proposal.ProposalId))));
        }
        else
        {
            // The old directory publication backend remains explicitly unavailable.
            // Reaching it proves admission only, never Linux proposal publication.
            var error = await Assert.ThrowsAsync<PlatformNotSupportedException>(() => Publish());
            Assert.Contains("Runtime proposal publication", error.Message, StringComparison.Ordinal);
            Assert.False(Directory.Exists(destination));
            Assert.False(File.Exists(_files.ResolvePath(InboxPath)));
        }
        Assert.Equal(_taskBytes, File.ReadAllBytes(_files.ResolvePath(TaskPath)));
        Assert.Equal(_generationBytes, File.ReadAllBytes(_files.SessionGenerationPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleGenerationOrTaskCannotRemoveEmptyDestination(bool staleGeneration)
    {
        var destination = Destination; Directory.CreateDirectory(Path.Combine(destination, "empty"));
        var result = await Publish(staleGeneration ? Guid.NewGuid().ToString("N") : null,
            staleGeneration ? null : [99]);
        Assert.False(result.Published);
        Assert.Equal(staleGeneration, result.SessionReplaced);
        Assert.True(Directory.Exists(Path.Combine(destination, "empty")));
        Assert.Empty(Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories));
        Assert.False(File.Exists(_files.ResolvePath(InboxPath)));
        Assert.Equal(_taskBytes, File.ReadAllBytes(_files.ResolvePath(TaskPath)));
        Assert.Equal(_generationBytes, File.ReadAllBytes(_files.SessionGenerationPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NonemptyDestinationIsRejectedWithoutDeletingUnknownBytes(bool nested)
    {
        var destination = Destination;
        var data = Path.Combine(destination, nested ? "nested" : "", "unknown.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(data)!); File.WriteAllBytes(data, [0xFF, 0]);
        var result = await Publish();
        Assert.False(result.Published);
        Assert.False(result.SessionReplaced);
        Assert.Equal(new byte[] { 0xFF, 0 }, File.ReadAllBytes(data));
        Assert.False(File.Exists(_files.ResolvePath(InboxPath)));
    }

    [Fact]
    public async Task WrongTypeDestinationFailsAdmissionBeforeTheBackendAndPreservesBytes()
    {
        var destination = Destination;
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.WriteAllBytes(destination, [42]);
        var result = await Publish();
        Assert.False(result.Published);
        Assert.False(result.SessionReplaced);
        Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(destination));
        Assert.False(File.Exists(_files.ResolvePath(InboxPath)));
    }

    [Fact]
    public async Task LinkedDestinationRetainsOutsideDataAndNeverPublishesInbox()
    {
        var destination = Destination;
        var outside = Path.Combine(_root, "outside"); Directory.CreateDirectory(outside);
        var sentinel = Path.Combine(outside, "sentinel.bin"); File.WriteAllBytes(sentinel, [42]);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        Directory.CreateSymbolicLink(destination, outside);
        await Assert.ThrowsAsync<InvalidDataException>(() => Publish());
        Assert.NotNull(new DirectoryInfo(destination).LinkTarget);
        Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(sentinel));
        Assert.False(File.Exists(_files.ResolvePath(InboxPath)));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
