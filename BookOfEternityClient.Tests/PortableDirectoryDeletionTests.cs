using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PortableDirectoryDeletionTests : IDisposable
{
    private const string Tree = "pending_turn_snapshot";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-directory-delete-" + Guid.NewGuid().ToString("N"));
    private readonly string _generation = Guid.NewGuid().ToString("N");
    private FileSystemManager Manager(FileSystemManagerHooks? hooks = null) =>
        new(_root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance, hooks);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyOrAbsentTreeDoesNotPublishOrInventGeneration(bool exists)
    {
        var publications = 0;
        var files = Manager(new FileSystemManagerHooks { LocalPublicationObserver = (_, _) => publications++ });
        if (exists) Directory.CreateDirectory(files.ResolvePath(Tree + "/nested/empty"));

        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            files.DeleteDirectoryTree(lease, Tree);

        Assert.False(Directory.Exists(files.ResolvePath(Tree)));
        Assert.False(File.Exists(files.SessionGenerationPath));
        Assert.Equal(0, publications);
    }

    [Fact]
    public async Task NestedTreeDeletesEveryExactFileInOneDecisionAndPreservesSibling()
    {
        var intents = 0;
        var commits = 0;
        var files = Manager(new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, _) =>
            {
                if (phase == TrustedLocalPublicationPhase.IntentPublished) intents++;
                if (phase == TrustedLocalPublicationPhase.Committed) commits++;
            }
        });
        var generationBytes = SeedGeneration(files);
        Seed(files, Tree + "/binary.bin", [0xFF, 0, 0xFE]);
        Seed(files, Tree + "/nested/bom.json", [0xEF, 0xBB, 0xBF, 0x7B, 0x7D]);
        Seed(files, Tree + "/nested/empty.bin", []);
        Seed(files, "pending_turn_snapshot_other/keep.bin", [42]);

        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            files.DeleteDirectoryTree(lease, Tree);

        Assert.False(Directory.Exists(files.ResolvePath(Tree)));
        Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(files.ResolvePath("pending_turn_snapshot_other/keep.bin")));
        Assert.Equal(generationBytes, File.ReadAllBytes(files.SessionGenerationPath));
        Assert.Equal(1, intents);
        Assert.Equal(1, commits);
        Assert.False(File.Exists(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
    }

    private byte[] SeedGeneration(FileSystemManager files)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, generationId = _generation });
        Directory.CreateDirectory(Path.GetDirectoryName(files.SessionGenerationPath)!);
        File.WriteAllBytes(files.SessionGenerationPath, bytes);
        return bytes;
    }

    private static void Seed(FileSystemManager files, string relative, byte[] bytes)
    {
        var path = files.ResolvePath(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
