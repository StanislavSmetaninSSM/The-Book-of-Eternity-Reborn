using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests.WebUi;

public sealed class BrowserInkFeatherFateParityTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ExplorerWebCommandService _service;

    public BrowserInkFeatherFateParityTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-ink-feather-fate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        var stateManager = new StateManager(_fs, new GameSettings(), NullLogger<StateManager>.Instance);
        var validation = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        _service = new ExplorerWebCommandService(_fs, stateManager, new LocalizationManager(), validation);
    }

    [Fact]
    public async Task ExecuteAsync_FeathersInMortalRealm_OffersRevealAndRewriteFateActions()
    {
        await SeedSoulStateAsync(100);
        await SeedPendingFateAsync(isLocked: true);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/feathers"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var reveal = Assert.Single(result.Actions, action => action.Id == "ink-feather-reveal-fate");
        Assert.Equal("/reveal_fate", reveal.Command);
        Assert.Contains("Открыть Судьбу", reveal.Label, StringComparison.Ordinal);
        var rewrite = Assert.Single(result.Actions, action => action.Id == "ink-feather-rewrite-fate");
        Assert.Equal("/rewrite_fate", rewrite.Command);
        Assert.Contains("Переписать Судьбу", rewrite.Label, StringComparison.Ordinal);
        AssertNoDefaultTechnicalLeak(result);
    }

    [Fact]
    public async Task ExecuteAsync_RevealFate_OpensCostedConfirmationPromptWithoutWritingPendingState()
    {
        await SeedSoulStateAsync(100);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/reveal_fate",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal("/reveal_fate", result.Command);
        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.NotNull(result.InteractiveSession);
        Assert.True(result.InteractiveSession.RequiresLocalUiLock);
        Assert.IsType<UiConfirmationPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "confirm_ink_feather_fate_reveal"));
        var text = CollectBlockText(result.Blocks, includeRawJson: false);
        Assert.Contains("10 Чернильных Перьев", text, StringComparison.Ordinal);
        Assert.Contains("останется 90", text, StringComparison.Ordinal);
        Assert.False(_fs.FileExists(PendingTurnStateService.PendingDiceStatePath));
        Assert.Equal(100, (await ReadSoulAsync())["inkFeathers"]!["current"]!.GetValue<int>());
        AssertNoDefaultTechnicalLeak(result);
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_RevealFate_DeductsConsoleCostOnceAndLocksPendingFate()
    {
        await SeedSoulStateAsync(100);
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/reveal_fate",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));
        Assert.Equal(CommandExecutionState.RequiresInput, started.State);

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            Answers(("confirm_ink_feather_fate_reveal", true)),
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        Assert.Empty(completed.Prompts);
        var soul = await ReadSoulAsync();
        Assert.Equal(90, soul["inkFeathers"]!["current"]!.GetValue<int>());
        var pending = await ReadPendingAsync();
        Assert.True(pending["isFateLocked"]!.GetValue<bool>());
        Assert.Equal(20, pending["preGeneratedDices1d20"]!.AsArray().Count);
        Assert.NotNull(pending["gachaBaseResult"]);
        var playerText = CollectBlockText(completed.Blocks, includeRawJson: false);
        Assert.Contains("Судьба открыта", playerText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Кости судьбы", playerText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Гача-база", playerText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Списано: 10", playerText, StringComparison.Ordinal);
        var payload = completed.Blocks.OfType<UiRawJsonBlock>().Last().Json!.AsObject();
        Assert.Equal("ink_feather_fate_reveal_browser_write", payload["sourceSurface"]!.GetValue<string>());
        Assert.Equal(10, payload["spentInkFeathers"]!.GetValue<int>());
        Assert.Equal(90, payload["remainingInkFeathers"]!.GetValue<int>());
        Assert.True(payload["revealedFate"]!["isFateLocked"]!.GetValue<bool>());
        AssertNoDefaultTechnicalLeak(completed);
    }

    [Fact]
    public async Task ExecuteAsync_RewriteFate_RequiresLockedFateAndShowsConsoleCost()
    {
        await SeedSoulStateAsync(100);
        await SeedPendingFateAsync(isLocked: true);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/rewrite_fate",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.NotNull(result.InteractiveSession);
        Assert.IsType<UiConfirmationPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "confirm_ink_feather_fate_rewrite"));
        var text = CollectBlockText(result.Blocks, includeRawJson: false);
        Assert.Contains("25 Чернильных Перьев", text, StringComparison.Ordinal);
        Assert.Contains("останется 75", text, StringComparison.Ordinal);
        Assert.Contains("заменит текущие кости", text, StringComparison.OrdinalIgnoreCase);
        AssertNoDefaultTechnicalLeak(result);
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_RewriteFate_DeductsConsoleCostOnceAndReturnsOldAndNewFate()
    {
        await SeedSoulStateAsync(100);
        await SeedPendingFateAsync(isLocked: true);
        var oldPending = await _fs.ReadFileAsync(PendingTurnStateService.PendingDiceStatePath);
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/rewrite_fate",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));
        Assert.Equal(CommandExecutionState.RequiresInput, started.State);

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            Answers(("confirm_ink_feather_fate_rewrite", true)),
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        Assert.Equal(75, (await ReadSoulAsync())["inkFeathers"]!["current"]!.GetValue<int>());
        var newPendingRaw = await _fs.ReadFileAsync(PendingTurnStateService.PendingDiceStatePath);
        Assert.NotEqual(oldPending, newPendingRaw);
        var newPending = JsonNode.Parse(newPendingRaw!)!.AsObject();
        Assert.True(newPending["isFateLocked"]!.GetValue<bool>());
        var playerText = CollectBlockText(completed.Blocks, includeRawJson: false);
        Assert.Contains("Судьба переписана", playerText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Старые кости", playerText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Новые кости", playerText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Списано: 25", playerText, StringComparison.Ordinal);
        var payload = completed.Blocks.OfType<UiRawJsonBlock>().Last().Json!.AsObject();
        Assert.Equal("ink_feather_fate_rewrite_browser_write", payload["sourceSurface"]!.GetValue<string>());
        Assert.Equal(25, payload["spentInkFeathers"]!.GetValue<int>());
        Assert.NotNull(payload["oldFate"]);
        Assert.NotNull(payload["newFate"]);
        Assert.True(payload["newFate"]!["isFateLocked"]!.GetValue<bool>());
        AssertNoDefaultTechnicalLeak(completed);
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_RevealFate_StaleBalanceBlocksWithoutWriting()
    {
        await SeedSoulStateAsync(100);
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/reveal_fate",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));
        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        await SeedSoulStateAsync(4);
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        var result = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            Answers(("confirm_ink_feather_fate_reveal", true)),
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.Contains("Недостаточно", CollectBlockText(result.Blocks, includeRawJson: false) + " " + CollectNotifications(result), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        Assert.False(_fs.FileExists(PendingTurnStateService.PendingDiceStatePath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_RevealFate_MissingSoulStateBlocksWithoutWritingPendingState()
    {
        await SeedSoulStateAsync(100);
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/reveal_fate",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));
        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        _fs.DeleteFile("game_state/meta/soul_state.json");

        var result = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            Answers(("confirm_ink_feather_fate_reveal", true)),
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.Contains("Состояние души", CollectNotifications(result), StringComparison.OrdinalIgnoreCase);
        Assert.False(_fs.FileExists("game_state/meta/soul_state.json"));
        Assert.False(_fs.FileExists(PendingTurnStateService.PendingDiceStatePath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_RevealFate_MalformedSoulStateBlocksWithoutWritingPendingState()
    {
        await SeedSoulStateAsync(100);
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/reveal_fate",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));
        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{ broken");
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        var result = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            Answers(("confirm_ink_feather_fate_reveal", true)),
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.Contains("Состояние души", CollectNotifications(result), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        Assert.False(_fs.FileExists(PendingTurnStateService.PendingDiceStatePath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_RevealFate_WithoutConfirmationLeavesStateUnchanged()
    {
        await SeedSoulStateAsync(100);
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/reveal_fate",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));
        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        var result = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            Answers(("confirm_ink_feather_fate_reveal", false)),
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.Contains("Подтвердите", CollectNotifications(result), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        Assert.False(_fs.FileExists(PendingTurnStateService.PendingDiceStatePath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_RewriteFate_StaleUnlockedPendingStateBlocksWithoutSpend()
    {
        await SeedSoulStateAsync(100);
        await SeedPendingFateAsync(isLocked: true);
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/rewrite_fate",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));
        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        await SeedPendingFateAsync(isLocked: false);
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        var beforePending = await _fs.ReadFileAsync(PendingTurnStateService.PendingDiceStatePath);

        var result = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            Answers(("confirm_ink_feather_fate_rewrite", true)),
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.Contains("Сначала нужно открыть судьбу", CollectNotifications(result), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        Assert.Equal(beforePending, await _fs.ReadFileAsync(PendingTurnStateService.PendingDiceStatePath));
    }

    [Fact]
    public async Task ExecuteAsync_RewriteFate_WhenPendingStateMissingOrMalformedDoesNotCreateState()
    {
        await SeedSoulStateAsync(100);
        await _fs.WriteFileAtomicAsync(PendingTurnStateService.PendingDiceStatePath, "{ broken");
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        var beforePending = await _fs.ReadFileAsync(PendingTurnStateService.PendingDiceStatePath);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/rewrite_fate",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Null(result.InteractiveSession);
        Assert.Contains("Сначала нужно открыть судьбу", CollectBlockText(result.Blocks, includeRawJson: false), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        Assert.Equal(beforePending, await _fs.ReadFileAsync(PendingTurnStateService.PendingDiceStatePath));
    }

    [Fact]
    public async Task ExecuteAsync_RevealFate_WithActiveGmTurnBlocksPromptWithoutWriting()
    {
        await SeedSoulStateAsync(100);
        await _fs.WriteFileAtomicAsync("input/turn_request.json", "{}");
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/reveal_fate",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.Pending, result.State);
        Assert.Null(result.InteractiveSession);
        Assert.Contains("Активный ход", CollectBlockText(result.Blocks, includeRawJson: false), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        Assert.False(_fs.FileExists(PendingTurnStateService.PendingDiceStatePath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_RevealFate_StaleRealmBlocksWithoutSpend()
    {
        await SeedSoulStateAsync(100);
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/reveal_fate",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));
        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        await SeedSoulStateAsync(100, currentRealm: "Chaos Sea");
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        var result = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            Answers(("confirm_ink_feather_fate_reveal", true)),
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.Contains("смертной жизни", CollectNotifications(result), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        Assert.False(_fs.FileExists(PendingTurnStateService.PendingDiceStatePath));
    }

    [Fact]
    public void BrowserCommandCoverage_Issue815FateActionsAreCoveredAndSiblingIssuesRemainOpen()
    {
        var coverage = BrowserCommandCoverageService.Build();

        var reveal = Assert.Single(coverage.Commands, command => command.Id == "ink_feather_reveal_fate");
        Assert.Equal("covered", reveal.AuditStatus);
        Assert.Equal("guided-form", reveal.FormMode);
        Assert.DoesNotContain("#815", reveal.FollowUpIssue, StringComparison.Ordinal);
        var rewrite = Assert.Single(coverage.Commands, command => command.Id == "ink_feather_rewrite_fate");
        Assert.Equal("covered", rewrite.AuditStatus);
        Assert.Equal("guided-form", rewrite.FormMode);
        Assert.DoesNotContain("#815", rewrite.FollowUpIssue, StringComparison.Ordinal);

        var feathers = Assert.Single(coverage.Commands, command => command.Id == "feathers");
        Assert.DoesNotContain("#815", feathers.FollowUpIssue, StringComparison.Ordinal);
        Assert.Contains("#817", feathers.FollowUpIssue, StringComparison.Ordinal);
        Assert.Contains(coverage.Commands, command => command.Id == "afterlife_archive" && command.FollowUpIssue.Contains("#816", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
        }
    }

    private async Task SeedSoulStateAsync(int inkFeathers, string currentRealm = "Mortal World")
    {
        var soul = new JsonObject
        {
            ["soulName"] = "Тестовая душа",
            ["currentRealm"] = currentRealm,
            ["currentIncarnation"] = 4,
            ["inkFeathers"] = new JsonObject
            {
                ["current"] = inkFeathers,
                ["total"] = Math.Max(inkFeathers, 0)
            }
        };
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soul.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private async Task SeedPendingFateAsync(bool isLocked)
    {
        var dice = new JsonArray();
        for (var i = 1; i <= 20; i++)
            dice.Add(i);
        var gachaDice = new JsonArray(18, 18, 18, 18);
        var pending = new JsonObject
        {
            ["preGeneratedDices1d20"] = dice,
            ["gachaBaseResult"] = new JsonObject
            {
                ["diceUsed"] = gachaDice,
                ["baseScore"] = 72,
                ["baseRarity"] = "Rare",
                ["formula"] = "client-computed gacha base (range 4-80)"
            },
            ["isFateLocked"] = isLocked,
            ["createdAtUtc"] = "2026-06-02T00:00:00Z",
            ["fateLockedAtUtc"] = isLocked ? "2026-06-02T00:00:01Z" : null,
            ["lastUpdatedUtc"] = "2026-06-02T00:00:01Z"
        };
        await _fs.WriteFileAtomicAsync(PendingTurnStateService.PendingDiceStatePath, pending.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private async Task<JsonObject> ReadSoulAsync() =>
        JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();

    private async Task<JsonObject> ReadPendingAsync() =>
        JsonNode.Parse((await _fs.ReadFileAsync(PendingTurnStateService.PendingDiceStatePath))!)!.AsObject();

    private static Dictionary<string, JsonNode?> Answers(params (string key, object? value)[] pairs)
    {
        var dict = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var (key, value) in pairs)
        {
            dict[key] = value switch
            {
                null => null,
                bool flag => JsonValue.Create(flag),
                int number => JsonValue.Create(number),
                string text => JsonValue.Create(text),
                _ => JsonValue.Create(value.ToString())
            };
        }
        return dict;
    }

    private static string CollectNotifications(ExplorerCommandResult result) =>
        string.Join(" ", result.Notifications.Select(notification => notification.Title + " " + notification.Message));

    private static void AssertNoDefaultTechnicalLeak(ExplorerCommandResult result)
    {
        var text = CollectBlockText(result.Blocks, includeRawJson: false) + " " + CollectNotifications(result);
        foreach (var forbidden in new[] { ".json", "game_state", "DTO", "API", "endpoint", "validation", "debug", "exception" })
            Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
    }

    private static string CollectBlockText(IEnumerable<UiBlock> blocks, bool includeRawJson)
    {
        var parts = new List<string>();
        foreach (var block in blocks)
            CollectBlockText(block, parts, includeRawJson);
        return string.Join(" ", parts);
    }

    private static void CollectBlockText(UiBlock block, List<string> parts, bool includeRawJson)
    {
        switch (block)
        {
            case UiTextBlock text:
                parts.Add(text.Text);
                break;
            case UiMessageBlock message:
                parts.Add(message.Title);
                parts.Add(message.Message);
                break;
            case UiPanelBlock panel:
                parts.Add(panel.Title);
                foreach (var child in panel.Blocks)
                    CollectBlockText(child, parts, includeRawJson);
                break;
            case UiTableBlock table:
                parts.Add(table.Title);
                parts.AddRange(table.Columns);
                foreach (var row in table.Rows)
                    parts.AddRange(row.Cells);
                break;
            case UiListBlock list:
                parts.AddRange(list.Items);
                break;
            case UiKeyValueGridBlock grid:
                foreach (var item in grid.Items)
                {
                    parts.Add(item.Key);
                    parts.Add(item.Value);
                }
                break;
            case UiRawJsonBlock raw when includeRawJson:
                parts.Add(raw.Title);
                parts.Add(raw.Json?.ToJsonString() ?? string.Empty);
                break;
        }
    }
}
