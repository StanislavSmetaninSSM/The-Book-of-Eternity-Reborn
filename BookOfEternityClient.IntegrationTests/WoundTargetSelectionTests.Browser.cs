using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class WoundTargetSelectionTests
{
    /// <summary>Both navigation phases cancel without staging a draw, queue or world change.</summary>
    [Fact]
    public async Task BrowserTargetAndWoundPhasesCancelWithoutTreatmentSideEffects()
    {
        await using var fixture = await CreateTargetFixtureAsync();
        var commands = Commands(fixture.Files); var before = fixture.Snapshot();
        var first = await commands.ExecuteAsync(new("/лечить", "target_browser"));
        Assert.Single(first.Prompts); Assert.Equal("wound_target", first.Prompts[0].Id);
        var cancelled = await commands.CancelPromptSessionAsync(new(first.InteractiveSession!.SessionId, "target_browser"));
        Assert.Equal(CommandExecutionState.Completed, cancelled.State); Assert.Equal(before, fixture.Snapshot());
        first = await commands.ExecuteAsync(new("/лечить", "target_browser"));
        var target = Assert.IsType<UiSelectionPrompt>(Assert.Single(first.Prompts));
        Assert.Equal("Себя", target.Options[0].Label);
        var second = await commands.SubmitPromptSessionAsync(new(first.InteractiveSession!.SessionId,
            new() { ["wound_target"] = JsonValue.Create(target.Options[0].Value) }, "target_browser"));
        Assert.Equal(CommandExecutionState.RequiresInput, second.State);
        Assert.Equal(first.InteractiveSession.SessionId, second.InteractiveSession!.SessionId);
        Assert.Contains(second.Prompts, p => p.Id == "wound_selection");
        Assert.Contains(second.Prompts, p => p.Id == "confirm_wound_treatment");
        Assert.Null(await fixture.Files.ReadFileBytesAsync(PendingPlayerActionService.PendingPath));
        await commands.CancelPromptSessionAsync(new(second.InteractiveSession.SessionId, "target_browser"));
        Assert.Equal(before, fixture.Snapshot());
    }

    /// <summary>A visible target with no grant opens a read-only view and cannot queue treatment.</summary>
    [Fact]
    public async Task BrowserVisibleActorWithoutGrantIsReadOnlyAndReplayedTargetAnswersCannotConfirmSelf()
    {
        await using var fixture = await CreateTargetFixtureAsync();
        var commands = Commands(fixture.Files); var before = fixture.Snapshot();
        var first = await commands.ExecuteAsync(new("/лечить", "target_browser"));
        var target = Assert.IsType<UiSelectionPrompt>(Assert.Single(first.Prompts));
        var foreign = await commands.SubmitPromptSessionAsync(new(first.InteractiveSession!.SessionId,
            new() { ["wound_target"] = JsonValue.Create(target.Options[0].Value), ["wound_selection"] = JsonValue.Create("wound_0"), ["confirm_wound_treatment"] = JsonValue.Create(true) }, "target_browser"));
        Assert.Equal(CommandExecutionState.RequiresInput, foreign.State);
        Assert.Contains(foreign.Prompts, p => p.Id == "wound_target");
        Assert.Null(await fixture.Files.ReadFileBytesAsync(PendingPlayerActionService.PendingPath));
        var other = Assert.Single(target.Options, o => o.Label.Contains("Хранитель выбора", StringComparison.Ordinal));
        var view = await commands.SubmitPromptSessionAsync(new(first.InteractiveSession.SessionId,
            new() { ["wound_target"] = JsonValue.Create(other.Value) }, "target_browser"));
        Assert.Equal(CommandExecutionState.Completed, view.State); Assert.Empty(view.Prompts);
        Assert.Null(view.InteractiveSession);
        var texts = view.Blocks.OfType<UiTextBlock>().Select(b => b.Text).Concat(view.Blocks.OfType<UiPanelBlock>()
            .SelectMany(p => p.Blocks.OfType<UiTextBlock>().Select(b => b.Text).Prepend(p.Title))).ToArray();
        Assert.DoesNotContain(texts, t => t.Contains("Своя рана выбора", StringComparison.Ordinal) || t.Contains("Описание собственной раны", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains("нет известных активных ран", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains("Лечение пока недоступно", StringComparison.Ordinal));
        Assert.Null(await fixture.Files.ReadFileBytesAsync(PendingPlayerActionService.PendingPath));
        Assert.Equal(before, fixture.Snapshot());
        first = await commands.ExecuteAsync(new("/лечить", "target_browser"));
        target = Assert.IsType<UiSelectionPrompt>(Assert.Single(first.Prompts));
        var second = await commands.SubmitPromptSessionAsync(new(first.InteractiveSession!.SessionId,
            new() { ["wound_target"] = JsonValue.Create(target.Options[0].Value) }, "target_browser"));
        var replay = await commands.SubmitPromptSessionAsync(new(second.InteractiveSession!.SessionId,
            new() { ["wound_target"] = JsonValue.Create(target.Options[0].Value), ["wound_selection"] = JsonValue.Create("wound_0"), ["confirm_wound_treatment"] = JsonValue.Create(true) }, "target_browser"));
        Assert.Equal(CommandExecutionState.RequiresInput, replay.State);
        Assert.Null(await fixture.Files.ReadFileBytesAsync(PendingPlayerActionService.PendingPath));
        await commands.CancelPromptSessionAsync(new(second.InteractiveSession.SessionId, "target_browser"));
        Assert.Equal(before, fixture.Snapshot());
    }

    /// <summary>The second phase queues exactly one unchanged self action; navigation itself draws nothing.</summary>
    [Fact]
    public async Task BrowserConfirmedSelfQueuesOnceAndCompletedReplayCannotQueueAgain()
    {
        await using var fixture = await CreateTargetFixtureAsync(); var commands = Commands(fixture.Files);
        var before = await fixture.Files.ReadFileBytesAsync(WoundHistoryState.HistoryPath);
        var first = await commands.ExecuteAsync(new("/лечить", "target_browser"));
        var second = await commands.SubmitPromptSessionAsync(new(first.InteractiveSession!.SessionId,
            new() { ["wound_target"] = JsonValue.Create("target_0") }, "target_browser"));
        var selection = Assert.IsType<UiSelectionPrompt>(Assert.Single(second.Prompts, p => p.Id == "wound_selection"));
        var answers = new Dictionary<string, JsonNode?> { ["wound_selection"] = JsonValue.Create(Assert.Single(selection.Options, o => !o.Disabled).Value), ["confirm_wound_treatment"] = JsonValue.Create(true) };
        var final = await commands.SubmitPromptSessionAsync(new(second.InteractiveSession!.SessionId, answers, "target_browser"));
        Assert.Equal(CommandExecutionState.Completed, final.State);
        var queued = Assert.IsType<byte[]>(await fixture.Files.ReadFileBytesAsync(PendingPlayerActionService.PendingPath));
        await commands.SubmitPromptSessionAsync(new(second.InteractiveSession.SessionId, answers, "target_browser"));
        Assert.Equal(queued, await fixture.Files.ReadFileBytesAsync(PendingPlayerActionService.PendingPath));
        Assert.Equal(before, await fixture.Files.ReadFileBytesAsync(WoundHistoryState.HistoryPath));
        Assert.Null(await fixture.Files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
    }

    /// <summary>Cancellation captured before phase advance cannot erase the new exact snapshot.</summary>
    [Fact]
    public async Task BrowserOldCancelCannotRemoveAdvancedSameSessionPhase()
    {
        var gate = new TargetRaceGate();
        await using var fixture = await CreateTargetFixtureAsync();
        var prompts = new ExplorerWebPromptSessionService(fixture.Files) { WoundSnapshotObservedForTesting = gate.AfterSnapshotAsync };
        var commands = Commands(fixture.Files, prompts); var before = fixture.Snapshot();
        var first = await commands.ExecuteAsync(new("/лечить", "target_browser"));
        var oldCancel = gate.RunAsync(() => commands.CancelPromptSessionAsync(new(first.InteractiveSession!.SessionId, "target_browser")));
        try
        {
            await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var second = await commands.SubmitPromptSessionAsync(new(first.InteractiveSession!.SessionId,
                new() { ["wound_target"] = JsonValue.Create("target_0") }, "target_browser"));
            Assert.Equal(CommandExecutionState.RequiresInput, second.State);
            gate.Release(); await oldCancel;
            var live = await commands.GetPromptSessionAsync(second.InteractiveSession!.SessionId);
            Assert.Contains(live.Prompts, p => p.Id == "wound_selection");
            await commands.CancelPromptSessionAsync(new(second.InteractiveSession.SessionId, "target_browser"));
            Assert.Equal(before, fixture.Snapshot());
        }
        finally { gate.Release(); await oldCancel; }
    }

    /// <summary>A cancelled old final cannot publish or release a newly opened form's lock.</summary>
    [Fact]
    public async Task BrowserOldFinalCannotPublishAfterCancellationAndReplacementForm()
    {
        var gate = new TargetRaceGate();
        await using var fixture = await CreateTargetFixtureAsync();
        var prompts = new ExplorerWebPromptSessionService(fixture.Files) { WoundSnapshotObservedForTesting = gate.AfterSnapshotAsync };
        var commands = Commands(fixture.Files, prompts); var before = fixture.Snapshot();
        var first = await commands.ExecuteAsync(new("/лечить", "target_browser"));
        var second = await commands.SubmitPromptSessionAsync(new(first.InteractiveSession!.SessionId,
            new() { ["wound_target"] = JsonValue.Create("target_0") }, "target_browser"));
        var choice = Assert.Single(Assert.IsType<UiSelectionPrompt>(Assert.Single(second.Prompts, p => p.Id == "wound_selection")).Options, o => !o.Disabled);
        var oldFinal = gate.RunAsync(() => commands.SubmitPromptSessionAsync(new(second.InteractiveSession!.SessionId,
            new() { ["wound_selection"] = JsonValue.Create(choice.Value), ["confirm_wound_treatment"] = JsonValue.Create(true) }, "target_browser")));
        try
        {
            await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await commands.CancelPromptSessionAsync(new(second.InteractiveSession!.SessionId, "target_browser"));
            var replacement = await commands.ExecuteAsync(new("/лечить", "target_browser"));
            var lockBefore = await fixture.Files.ReadFileBytesAsync(LocalUiSessionLockService.LockPath);
            Assert.NotEqual(first.InteractiveSession.SessionId, replacement.InteractiveSession!.SessionId);
            gate.Release(); await oldFinal;
            Assert.Null(await fixture.Files.ReadFileBytesAsync(PendingPlayerActionService.PendingPath));
            Assert.Equal(lockBefore, await fixture.Files.ReadFileBytesAsync(LocalUiSessionLockService.LockPath));
            Assert.Contains((await commands.GetPromptSessionAsync(replacement.InteractiveSession.SessionId)).Prompts, p => p.Id == "wound_target");
            await commands.CancelPromptSessionAsync(new(replacement.InteractiveSession.SessionId, "target_browser"));
            Assert.Equal(before, fixture.Snapshot());
        }
        finally { gate.Release(); await oldFinal; }
    }

    /// <summary>Expired/lost authority never reacquires the UI lease or removes a foreign token.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BrowserLostOrExpiredUiAuthorityPreservesForeignLease(bool expireSession)
    {
        await using var fixture = await CreateTargetFixtureAsync();
        var clock = new TargetClock(DateTimeOffset.UtcNow);
        var locks = new LocalUiSessionLockService(fixture.Files, clock);
        var prompts = new ExplorerWebPromptSessionService(fixture.Files, lockService: locks, timeProvider: clock);
        var commands = Commands(fixture.Files, prompts); var before = fixture.Snapshot();
        var first = await commands.ExecuteAsync(new("/лечить", "target_browser"));
        clock.Advance(expireSession ? TimeSpan.FromMinutes(21) : TimeSpan.FromMinutes(3));
        var foreign = await locks.AcquireOrRefreshAsync(new("foreign", "browser", "Other", TimeSpan.FromSeconds(120)), "foreign");
        Assert.True(foreign.Acquired);
        var token = await fixture.Files.ReadFileBytesAsync(LocalUiSessionLockService.LockPath);
        await commands.SubmitPromptSessionAsync(new(first.InteractiveSession!.SessionId,
            new() { ["wound_target"] = JsonValue.Create("target_0") }, "target_browser"));
        await commands.CancelPromptSessionAsync(new(first.InteractiveSession.SessionId, "target_browser"));
        Assert.Equal(token, await fixture.Files.ReadFileBytesAsync(LocalUiSessionLockService.LockPath));
        Assert.Null(await fixture.Files.ReadFileBytesAsync(PendingPlayerActionService.PendingPath));
        Assert.True(await locks.ReleaseAsync(foreign.Lease!)); Assert.Equal(before, fixture.Snapshot());
    }

    /// <summary>Fresh target membership and actual owner/generation remain mandatory before phase advance.</summary>
    [Theory]
    [InlineData("moved")]
    [InlineData("hidden")]
    [InlineData("generation")]
    [InlineData("owner")]
    public async Task BrowserChangedTargetOrSessionCannotAdvance(string change)
    {
        await using var fixture = await CreateTargetFixtureAsync(); var commands = Commands(fixture.Files);
        var first = await commands.ExecuteAsync(new("/лечить", "target_browser"));
        var other = Assert.Single(Assert.IsType<UiSelectionPrompt>(Assert.Single(first.Prompts)).Options, o => o.Label.Contains("Хранитель выбора", StringComparison.Ordinal));
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            if (change == "generation") fixture.Files.RotateSessionGeneration(lease);
            if (change is "moved" or "hidden")
            {
                var profiles = JsonNode.Parse((await fixture.Files.ReadFileAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!)!;
                profiles["profiles"]![1]![change == "hidden" ? "visibility" : "locationId"] = change == "hidden" ? "gm_only" : "elsewhere";
                await fixture.Files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
            }
        }
        var result = await commands.SubmitPromptSessionAsync(new(first.InteractiveSession!.SessionId,
            new() { ["wound_target"] = JsonValue.Create(other.Value) }, change == "owner" ? "foreign" : "target_browser"));
        Assert.DoesNotContain(result.Prompts, p => p.Id == "wound_selection");
        Assert.Null(await fixture.Files.ReadFileBytesAsync(PendingPlayerActionService.PendingPath));
        await commands.CancelPromptSessionAsync(new(first.InteractiveSession.SessionId, "target_browser"));
    }

    /// <summary>Final confirmation rechecks the actual Self wound and current locality after navigation.</summary>
    [Theory]
    [InlineData("history")]
    [InlineData("locality")]
    public async Task BrowserFinalRechecksSelectedWoundAndLocality(string changed)
    {
        await using var fixture = await CreateTargetFixtureAsync(); var commands = Commands(fixture.Files);
        var first = await commands.ExecuteAsync(new("/лечить", "target_browser"));
        var second = await commands.SubmitPromptSessionAsync(new(first.InteractiveSession!.SessionId,
            new() { ["wound_target"] = JsonValue.Create("target_0") }, "target_browser"));
        var choice = Assert.Single(Assert.IsType<UiSelectionPrompt>(Assert.Single(second.Prompts, p => p.Id == "wound_selection")).Options, o => !o.Disabled);
        if (changed == "history") await fixture.DamageAsync("stale_history");
        else
        {
            await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
            var path = AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0];
            var root = JsonNode.Parse((await fixture.Files.ReadFileAsync(lease, path))!)!;
            root["chaosSeaNavigation"]!["currentAbodeId"] = "another_abode";
            await fixture.Files.WriteFileAtomicAsync(lease, path, root.ToJsonString());
        }
        var result = await commands.SubmitPromptSessionAsync(new(second.InteractiveSession!.SessionId,
            new() { ["wound_selection"] = JsonValue.Create(choice.Value), ["confirm_wound_treatment"] = JsonValue.Create(true) }, "target_browser"));
        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.Contains(result.Notifications, n => n.Severity == UiNotificationSeverity.Error);
        Assert.Null(await fixture.Files.ReadFileBytesAsync(PendingPlayerActionService.PendingPath));
        await commands.CancelPromptSessionAsync(new(second.InteractiveSession.SessionId, "target_browser"));
    }

    /// <summary>A prompt that expires during fresh confirmation cannot queue, even while its UI lease stays valid.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task BrowserFinalCannotPublishWhenPromptExpiresDuringFreshValidation(int heartbeatBoundary)
    {
        var clock = new TargetClock(DateTimeOffset.UtcNow); var armed = false; var advanced = 0;
        await using var fixture = await CreateTargetFixtureAsync(beforeMutation: path =>
        {
            if (armed && path == LocalUiSessionLockService.LockPath && Interlocked.Increment(ref advanced) == heartbeatBoundary)
                clock.Advance(TimeSpan.FromSeconds(2));
        });
        var locks = new LocalUiSessionLockService(fixture.Files, clock);
        var prompts = new ExplorerWebPromptSessionService(fixture.Files, lockService: locks, timeProvider: clock);
        var commands = Commands(fixture.Files, prompts); var before = fixture.Snapshot();
        var first = await commands.ExecuteAsync(new("/лечить", "target_browser"));
        var second = await commands.SubmitPromptSessionAsync(new(first.InteractiveSession!.SessionId,
            new() { ["wound_target"] = JsonValue.Create("target_0") }, "target_browser"));
        var choice = Assert.Single(Assert.IsType<UiSelectionPrompt>(Assert.Single(second.Prompts, p => p.Id == "wound_selection")).Options, o => !o.Disabled);
        var owned = Assert.IsType<LocalUiSessionLockSnapshot>(await locks.InspectAsync());
        var token = new LocalUiSessionLockLease(owned.SessionGeneration, owned.OwnerId, owned.LeaseToken);
        // Keep the real UI token alive; prompt expiry must be the independent refusal.
        for (var i = 0; i < 12; i++)
        { clock.Advance(TimeSpan.FromSeconds(90)); Assert.True((await locks.RefreshAsync(token, "actual heartbeat")).Acquired); }
        clock.Advance(TimeSpan.FromSeconds(119)); // 19:59, original prompt and UI token both live.
        armed = true;
        var result = await commands.SubmitPromptSessionAsync(new(second.InteractiveSession!.SessionId,
            new() { ["wound_selection"] = JsonValue.Create(choice.Value), ["confirm_wound_treatment"] = JsonValue.Create(true) }, "target_browser"));
        Assert.True(advanced >= heartbeatBoundary);
        Assert.Contains(result.Notifications, n => n.Severity is UiNotificationSeverity.Error or UiNotificationSeverity.Warning);
        Assert.Null(await fixture.Files.ReadFileBytesAsync(PendingPlayerActionService.PendingPath));
        if (heartbeatBoundary == 1)
            Assert.False(Assert.IsType<LocalUiSessionLockSnapshot>(await locks.InspectAsync()).IsStale);
        await commands.GetPromptSessionAsync(second.InteractiveSession.SessionId); // Normal exact expiry cleanup.
        Assert.Equal(before, fixture.Snapshot());
    }

    private sealed class TargetClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        internal void Advance(TimeSpan delta) => _now += delta;
    }

    private sealed class TargetRaceGate
    {
        private readonly AsyncLocal<bool> _selected = new();
        private int _remaining = 1;
        internal TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal async Task AfterSnapshotAsync()
        {
            if (!_selected.Value || Interlocked.Exchange(ref _remaining, 0) != 1) return;
            Reached.TrySetResult(); await _released.Task;
        }
        internal async Task<ExplorerCommandResult> RunAsync(Func<Task<ExplorerCommandResult>> operation)
        { _selected.Value = true; try { return await operation(); } finally { _selected.Value = false; } }
        internal void Release() => _released.TrySetResult();
    }

    private static ExplorerWebCommandService Commands(FileSystemManager files, ExplorerWebPromptSessionService? prompts = null)
    {
        var state = new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance);
        return new(files, state, new LocalizationManager(), new ValidationService(files, NullLogger<ValidationService>.Instance), prompts);
    }

    private async Task<WoundPlayerReadFixture> CreateTargetFixtureAsync(CanonicalReadActivityAccumulator? reads = null, Action<string>? beforeMutation = null)
    {
        var wound = WoundContractTestData.CreateSpiritualActiveWound("wound_target_flow", "chaos_sea", "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath);
        wound["display"]!["name"] = "Своя рана выбора";
        wound["display"]!["description"] = "Описание собственной раны";
        var input = WoundPlayerProjectionTestData.InputInRealm("chaos_sea", wound);
        input.Carriers.AfterlifeProfiles!["profiles"]![0]!["standardArts"] = new JsonObject { ["spiritual_healing"] = 3 };
        input.Carriers.AfterlifeProfiles["profiles"]!.AsArray().Add(new JsonObject
        { ["actorType"] = "guardian", ["actorId"] = "guardian_target_flow", ["displayName"] = "Хранитель выбора", ["realm"] = "Chaos Sea", ["locationId"] = "abode_target_flow", ["activeWounds"] = new JsonArray(), ["activeEffects"] = new JsonArray() });
        var fixture = await WoundPlayerReadFixture.CreateAsync(output, realm: "chaos_sea", suppliedInput: input, reads: reads, beforeMutation: beforeMutation);
        try
        {
            await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
            var soul = JsonNode.Parse((await fixture.Files.ReadFileAsync(lease, "game_state/meta/soul_state.json"))!)!.AsObject();
            soul["afterlifeCombatProfile"] = new JsonObject { ["artTiers"] = new JsonObject { ["spiritual_healing"] = 3 } };
            await fixture.Files.WriteFileAtomicAsync(lease, "game_state/meta/soul_state.json", soul.ToJsonString());
            var guardian = new JsonObject { ["guardianId"] = "guardian_target_flow", ["canonicalName"] = "Хранитель выбора", ["abode"] = new JsonObject { ["abodeId"] = "abode_target_flow", ["name"] = "Обитель выбора" } };
            await fixture.Files.WriteFileAtomicAsync(lease, "game_state/meta/guardians.json", new JsonObject
            { ["guardians"] = new JsonArray(guardian), ["activeGuardian"] = guardian.DeepClone(), ["chaosSeaNavigation"] = new JsonObject { ["currentGuardianId"] = "guardian_target_flow", ["currentAbodeId"] = "abode_target_flow" } }.ToJsonString());
            await fixture.Files.WriteFileAtomicAsync(lease, GuardianAbodeResidentState.StatePath, new JsonObject { ["entries"] = new JsonArray() }.ToJsonString());
            _ = fixture.Files.GetOrCreateSessionGeneration(lease);
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }
}
