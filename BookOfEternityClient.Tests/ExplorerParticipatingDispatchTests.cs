using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ExplorerParticipatingDispatchTests
{
    [Fact]
    public async Task StandaloneColdExplorerRemainsWithoutGenerationOrConfig()
    {
        await WithFixture(async fixture =>
        {
            Assert.False(File.Exists(fixture.Files.SessionGenerationPath));
            var before = fixture.GameInventory();var menu = fixture.Menu();
            Assert.Equal("", await menu.Explorer.TryProcessCommand("/storage_move"));
            Assert.True(ExplorerParticipationFixture.MenuObserved(menu.Console));
            Assert.False(File.Exists(fixture.Files.SessionGenerationPath));
            Assert.Equal(before, fixture.GameInventory());
        }, uninitialized: true);
    }

    [Fact]
    public async Task InitializedWithoutMainOwnerDispatchPreservesGameplay()
    {
        await WithFixture(async fixture =>
        {
            await InitializeAsync(fixture.Files);
            var before = fixture.GameInventory();var generation = File.ReadAllBytes(fixture.Files.SessionGenerationPath);
            var menu = fixture.Menu();
            Assert.Equal("", await GameEngine.DispatchExplorerForCurrentSessionAsync(fixture.Files, menu.Explorer, "/storage_move"));
            Assert.True(ExplorerParticipationFixture.MenuObserved(menu.Console));
            Assert.Equal(before, fixture.GameInventory());Assert.Equal(generation, File.ReadAllBytes(fixture.Files.SessionGenerationPath));
            Assert.False(SessionOperationContext.TryGetExpectedGeneration(fixture.Files.BasePath, out _));
        });
    }

    [Fact]
    public async Task SharedDispatchRefusesMissingGenerationWithoutBootstrap()
    {
        await WithFixture(async fixture =>
        {
            Assert.False(File.Exists(fixture.Files.SessionGenerationPath));
            var before = fixture.GameInventory();var menu = fixture.Menu();
            await Assert.ThrowsAsync<InvalidDataException>(() => GameEngine.DispatchExplorerForCurrentSessionAsync(fixture.Files, menu.Explorer, "/storage_move"));
            Assert.Empty(menu.Console.SelectionTitles);
            Assert.False(File.Exists(fixture.Files.SessionGenerationPath));
            Assert.False(File.Exists(fixture.Files.ResolvePath("config.json")));
            Assert.Equal(before, fixture.GameInventory());
        }, uninitialized: true);
    }

    [Fact]
    public async Task ForeignBoundGenerationRefusesMenuWithoutGameplayChange()
    {
        await WithFixture(async fixture =>
        {
            await InitializeAsync(fixture.Files);var before = fixture.GameInventory();var menu = fixture.Menu();var invoked = false;
            await Assert.ThrowsAsync<SessionReplacedException>(() => SessionOperationContext.RunBoundAsync(fixture.Files, Guid.NewGuid().ToString("N"), async () =>
            {
                invoked = true;return await GameEngine.DispatchExplorerForCurrentSessionAsync(fixture.Files, menu.Explorer, "/storage_move");
            }));
            Assert.True(invoked);Assert.Empty(menu.Console.SelectionTitles);Assert.Equal(before, fixture.GameInventory());
        });
    }

    [Fact]
    public async Task NestedDispatchRetainsOnlyBorrowedScopeAndAllowsFreshOperation()
    {
        await WithFixture(async fixture =>
        {
            await InitializeAsync(fixture.Files);var before = fixture.GameInventory();
            await SessionOperationContext.RunParticipatingCurrentSessionAsync(fixture.Files, async () =>
            {
                Assert.True(SessionOperationContext.TryGetExpectedGeneration(fixture.Files.BasePath, out var original));
                var menu = fixture.Menu();await GameEngine.DispatchExplorerForCurrentSessionAsync(fixture.Files, menu.Explorer, "/storage_move");
                Assert.True(ExplorerParticipationFixture.MenuObserved(menu.Console));
                Assert.True(SessionOperationContext.TryGetExpectedGeneration(fixture.Files.BasePath, out var retained));Assert.Equal(original, retained);
            });
            Assert.False(SessionOperationContext.TryGetExpectedGeneration(fixture.Files.BasePath, out _));
            await SessionOperationContext.RunParticipatingCurrentSessionAsync(fixture.Files, () => Task.CompletedTask);
            Assert.Equal(before, fixture.GameInventory());
        });
    }

    [Fact]
    public void BothActualEngineCallsitesAwaitSharedDispatchBeforeConsumingActions()
    {
        var source = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs"));
        Assert.DoesNotContain("_explorer.TryProcessCommand(", source);
        Assert.Contains("await DispatchExplorerForCurrentSessionAsync(_fs, _explorer, input)", source);
        Assert.Contains("await DispatchExplorerForCurrentSessionAsync(_fs, _explorer, request.OriginalCommand)", source);
        Assert.Equal(2, source.Split("await DispatchExplorerForCurrentSessionAsync(").Length - 1);
    }

    /// <summary>Prepares only an existing generation for initialized-session controls, outside the tested operation.</summary>
    private static async Task InitializeAsync(FileSystemManager files)
    {
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();files.GetOrCreateSessionGeneration(lease);
    }

    /// <summary>Owns a minimal DATA root and removes it after every bounded component case.</summary>
    private static async Task WithFixture(Func<ExplorerParticipationFixture, Task> test, bool uninitialized = false)
    {
        var root = Path.Combine(TestDiagnosticPaths.Root, "explorer-participation-data", Guid.NewGuid().ToString("N"));
        try
        {
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            await test(await ExplorerParticipationFixture.CreateAsync(files, uninitialized));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
