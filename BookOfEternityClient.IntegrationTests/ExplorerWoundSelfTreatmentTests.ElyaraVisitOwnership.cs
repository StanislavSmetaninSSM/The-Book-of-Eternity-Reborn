using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BrowserElyaraExpiredOrLostLeasePreservesForeignOwner(bool expirePrompt)
    {
        await using var fixture = await AfterlifeHealingPaymentLifecycleTests.CreateServiceFixtureAsync(1, 20, remoteElyara: true);
        var files = fixture.Files; var clock = new WoundTargetSelectionTests.TargetClock(DateTimeOffset.UtcNow);
        var locks = new LocalUiSessionLockService(files, clock);
        var commands = CreateCommands(files, new ExplorerWebPromptSessionService(files, lockService: locks, timeProvider: clock));
        var before = fixture.CaptureMutationRoots([ElyaraVisitService.SourcePaths[0], ElyaraVisitService.SourcePaths[2]]);
        var opened = await commands.ExecuteAsync(new("/элиара", Owner));
        clock.Advance(TimeSpan.FromMinutes(expirePrompt ? 21 : 3));
        var foreign = await locks.AcquireOrRefreshAsync(new("foreign", "browser", "Other", TimeSpan.FromSeconds(120)), "foreign");
        Assert.True(foreign.Acquired);
        var token = await files.ReadFileBytesAsync(LocalUiSessionLockService.LockPath);
        await commands.SubmitPromptSessionAsync(new(opened.InteractiveSession!.SessionId,
            new() { ["confirm_elyara_visit"] = JsonValue.Create(true) }, Owner));
        await commands.CancelPromptSessionAsync(new(opened.InteractiveSession.SessionId, Owner));
        Assert.Equal(token, await files.ReadFileBytesAsync(LocalUiSessionLockService.LockPath));
        Assert.Null(await files.ReadFileBytesAsync(PendingPlayerActionService.PendingPath));
        Assert.True(await locks.ReleaseAsync(foreign.Lease!));
        fixture.AssertMutationRootsUnchanged(before);
    }

    [Fact]
    public async Task BrowserElyaraOldFinalCannotPublishOrReleaseReplacementForm()
    {
        var gate = new WoundTargetSelectionTests.TargetRaceGate();
        await using var fixture = await AfterlifeHealingPaymentLifecycleTests.CreateServiceFixtureAsync(1, 20, remoteElyara: true);
        var files = fixture.Files;
        var commands = CreateCommands(files, new ExplorerWebPromptSessionService(files) { WoundSnapshotObservedForTesting = gate.AfterSnapshotAsync });
        var first = await commands.ExecuteAsync(new("/элиара", Owner));
        var pending = gate.RunAsync(() => commands.SubmitPromptSessionAsync(new(first.InteractiveSession!.SessionId,
            new() { ["confirm_elyara_visit"] = JsonValue.Create(true) }, Owner)));
        try
        {
            await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await commands.CancelPromptSessionAsync(new(first.InteractiveSession!.SessionId, Owner));
            var next = await commands.ExecuteAsync(new("/элиара", Owner));
            var token = await files.ReadFileBytesAsync(LocalUiSessionLockService.LockPath);
            gate.Release(); await pending;
            Assert.Null(await files.ReadFileBytesAsync(PendingPlayerActionService.PendingPath));
            Assert.Equal(token, await files.ReadFileBytesAsync(LocalUiSessionLockService.LockPath));
            Assert.Contains((await commands.GetPromptSessionAsync(next.InteractiveSession!.SessionId)).Prompts,
                p => p.Id == "confirm_elyara_visit");
            await commands.CancelPromptSessionAsync(new(next.InteractiveSession.SessionId, Owner));
        }
        finally { gate.Release(); await pending; }
    }

    [Fact]
    public async Task BrowserElyaraPromptExpiryDuringFreshSourceReadCannotQueue()
    {
        var clock = new WoundTargetSelectionTests.TargetClock(DateTimeOffset.UtcNow); var armed = false; var hits = 0;
        await using var fixture = await AfterlifeHealingPaymentLifecycleTests.CreateServiceFixtureAsync(1, 20,
            new FileSystemManagerHooks { BeforeCanonicalReadOpenAsync = path =>
            {
                if (armed && path == WoundCarrierCatalog.AfterlifeProfilesPath)
                { armed = false; hits++; clock.Advance(TimeSpan.FromSeconds(2)); }
                return Task.CompletedTask;
            } }, remoteElyara: true);
        var files = fixture.Files; var locks = new LocalUiSessionLockService(files, clock);
        var commands = CreateCommands(files, new ExplorerWebPromptSessionService(files, lockService: locks, timeProvider: clock));
        var first = await commands.ExecuteAsync(new("/элиара", Owner));
        var owned = Assert.IsType<LocalUiSessionLockSnapshot>(await locks.InspectAsync());
        var token = new LocalUiSessionLockLease(owned.SessionGeneration, owned.OwnerId, owned.LeaseToken);
        for (var index = 0; index < 12; index++)
        { clock.Advance(TimeSpan.FromSeconds(90)); Assert.True((await locks.RefreshAsync(token, "actual heartbeat")).Acquired); }
        clock.Advance(TimeSpan.FromSeconds(119)); // Prompt19:59 and actual UI lease both alive.
        armed = true;
        await commands.SubmitPromptSessionAsync(new(first.InteractiveSession!.SessionId,
            new() { ["confirm_elyara_visit"] = JsonValue.Create(true) }, Owner));
        Assert.Equal(1, hits); Assert.Null(await files.ReadFileBytesAsync(PendingPlayerActionService.PendingPath));
        await commands.GetPromptSessionAsync(first.InteractiveSession.SessionId); // Ordinary exact expiry cleanup.
        Assert.Null(await files.ReadFileBytesAsync(LocalUiSessionLockService.LockPath));
    }
}
