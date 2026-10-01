using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PortableDirectoryDeletionWindowsTests
{
    [Fact]
    public async Task ExtendedRootUsesNormalizedMembersAndCanonicalHookNames()
    {
        Assert.True(OperatingSystem.IsWindows(), "This fixture requires an actual Windows filesystem.");
        var root = Path.Combine(Path.GetTempPath(), "boe-tree-windows-" + Guid.NewGuid().ToString("N"));
        const string tree = "game_state/control/pending_turn_snapshot";
        try
        {
            var normal = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            await normal.WriteFileAtomicBytesAsync(tree + "/nested/A.bin", [0xFF, 0]);
            var boundaries = new List<string>();
            var extended = root.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + root[2..] : @"\\?\" + root;
            var files = new FileSystemManager(extended, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                {
                    BeforeCanonicalMutationBoundaryAsync = relative => { boundaries.Add(relative.Replace('\\', '/')); return Task.CompletedTask; }
                });
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync(); files.DeleteDirectoryTree(lease, tree);
            Assert.False(Directory.Exists(normal.ResolvePath(tree)));
            Assert.Equal(new[] { tree, tree + "/nested/A.bin" }, boundaries);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
}
