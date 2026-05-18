using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeLocalActionGuardTests
{
    [Fact]
    public async Task ActiveGmTurnBlocker_DetectsTurnArtifactsAndIgnoresEmptySnapshotDirectory()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            Directory.CreateDirectory(fs.ResolvePath("game_state/control/pending_turn_snapshot"));

            Assert.Null(AfterlifeLocalActionGuard.TryDescribeActiveGmTurnLifecycleBlocker(
                fs,
                "локальная операция",
                "test_surface.json"));

            await fs.WriteFileAtomicAsync("input/turn_request.json", "{}");
            var turnRequestBlocker = AfterlifeLocalActionGuard.TryDescribeActiveGmTurnLifecycleBlocker(
                fs,
                "локальная операция",
                "test_surface.json");

            Assert.NotNull(turnRequestBlocker);
            Assert.Contains("GM-turn", turnRequestBlocker, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("input/turn_request.json", turnRequestBlocker, StringComparison.OrdinalIgnoreCase);

            fs.DeleteFile("input/turn_request.json");
            await fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", "{}");
            var snapshotBlocker = AfterlifeLocalActionGuard.TryDescribeActiveGmTurnLifecycleBlocker(
                fs,
                "локальная операция",
                "test_surface.json");

            Assert.NotNull(snapshotBlocker);
            Assert.Contains("pending_turn_snapshot", snapshotBlocker, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-afterlife-local-action-guard-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void CleanupTempRoot(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch
        {
            // ignored
        }
    }
}
