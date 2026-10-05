using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PortableWorkerDiagnosticTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-worker-diagnostic-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Begin_PreservesInitiatingFailureWhenPrivateCleanupAlsoFails(bool failBeforeImage)
    {
        var cleanup = new IOException("owned cleanup diagnostic");
        var files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
            new CleanupFailure(cleanup), new FileSystemManagerHooks
            {
                SupportsDescriptorBoundCreateOnlyPublicationOverride = false
            });
        files.EnsureDirectoryStructure();
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        _ = files.GetOrCreateSessionGeneration(lease);

        var failure = await Assert.ThrowsAsync<AggregateException>(() => files.BeginOriginalWorkerApplyTransactionAsync(
            lease, [new CanonicalWorkerApplyChange(failBeforeImage ? "game_state/world/weather.json" : "", [1], [2])]));

        Assert.Equal(2, failure.InnerExceptions.Count);
        if (failBeforeImage)
        {
            var primary = Assert.IsType<PlatformNotSupportedException>(failure.InnerExceptions[0]);
            Assert.Contains("Runtime before-image create-only publication", primary.Message, StringComparison.Ordinal);
        }
        else
            Assert.IsType<InvalidDataException>(failure.InnerExceptions[0]);
        Assert.Same(cleanup, failure.InnerExceptions[1]);
        Assert.False(File.Exists(files.ActiveWorkerApplyTransactionJournalPath));
        Assert.False(File.Exists(files.ResolvePath("game_state/world/weather.json")));
        Assert.Single(Directory.GetDirectories(Path.GetDirectoryName(files.ActiveWorkerApplyTransactionJournalPath)!));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class CleanupFailure(Exception failure) : ILoadTransactionOperations
    {
        public bool DirectoryExists(string path) => Directory.Exists(path);
        public bool FileExists(string path) => File.Exists(path);
        public void BeforeDeleteDirectory(string path) => throw failure;
    }
}
