using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
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

    private static ExplorerWebCommandService Commands(FileSystemManager files, ExplorerWebPromptSessionService? prompts = null)
    {
        var state = new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance);
        return new(files, state, new LocalizationManager(), new ValidationService(files, NullLogger<ValidationService>.Instance), prompts);
    }

    private async Task<WoundPlayerReadFixture> CreateTargetFixtureAsync()
    {
        var wound = WoundContractTestData.CreateSpiritualActiveWound("wound_target_flow", "chaos_sea", "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath);
        var input = WoundPlayerProjectionTestData.InputInRealm("chaos_sea", wound);
        input.Carriers.AfterlifeProfiles!["profiles"]![0]!["standardArts"] = new JsonObject { ["spiritual_healing"] = 3 };
        input.Carriers.AfterlifeProfiles["profiles"]!.AsArray().Add(new JsonObject
        { ["actorType"] = "guardian", ["actorId"] = "guardian_target_flow", ["displayName"] = "Хранитель выбора", ["realm"] = "Chaos Sea", ["locationId"] = "abode_target_flow", ["activeWounds"] = new JsonArray(), ["activeEffects"] = new JsonArray() });
        var fixture = await WoundPlayerReadFixture.CreateAsync(output, realm: "chaos_sea", suppliedInput: input);
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
