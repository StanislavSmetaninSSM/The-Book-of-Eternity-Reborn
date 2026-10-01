using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PortableCoordinatedAuthorityTests
{
    [Fact]
    public async Task ActualAuthorityFactoryRetainsEveryPlatformDistinctInitialGuard()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-coordinated-authority-" + Guid.NewGuid().ToString("N"));
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        try
        {
            files.EnsureDirectoryStructure();
            const string lower = "game_state/meta/authority.json";
            const string upper = "game_state/meta/Authority.json";
            const string before = "{\"authority\":1}";
            File.WriteAllText(files.ResolvePath(lower), before);
            File.WriteAllText(files.ResolvePath(upper), before);
            var scope = LocalInteractionScope.Unresolved(LocalInteractionRealmKind.Mortal, "test",
                [new(lower, before), new(upper, before)]);
            var guards = CoordinatedStateWriteHelper.CreateAuthorityGuardWrites(scope);
            Assert.True(await CoordinatedStateWriteHelper.TryCommitAsync(files, guards));

            File.WriteAllText(files.ResolvePath(lower), "{\"authority\":2}");

            Assert.False(await CoordinatedStateWriteHelper.TryCommitAsync(files, guards));
            Assert.Equal(OperatingSystem.IsWindows() ? 1 : 2, guards.Length);
            Assert.False(File.Exists(files.SessionGenerationPath));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
}
