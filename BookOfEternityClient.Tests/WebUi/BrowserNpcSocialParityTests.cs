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

public sealed class BrowserNpcSocialParityTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly ExplorerWebCommandService _commandService;
    private readonly ExplorerWebPromptSessionService _promptSessions;
    private readonly BrowserMortalWorldWriteService _mortalWriteService;

    public BrowserNpcSocialParityTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-browser-npc-social-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _stateManager = new StateManager(_fs, new GameSettings(), NullLogger<StateManager>.Instance);
        var validation = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var lockService = new LocalUiSessionLockService(_fs);
        var coordinator = new BrowserLocalWriteCoordinator(_fs, lockService, TimeProvider.System);
        _mortalWriteService = new BrowserMortalWorldWriteService(
            _fs,
            coordinator,
            new ScenarioCoreService(_fs, NullLogger<ScenarioCoreService>.Instance),
            TimeProvider.System);
        _promptSessions = new ExplorerWebPromptSessionService(
            _fs,
            _stateManager,
            lockService: lockService,
            mortalWorldWriteService: _mortalWriteService,
            afterlifeWriteService: new BrowserAfterlifeWriteService(_fs, _stateManager, coordinator));
        _commandService = new ExplorerWebCommandService(_fs, _stateManager, new LocalizationManager(), validation, _promptSessions);
    }

    [Fact]
    [Trait("Category", "BrowserMortalWorldWriteService")]
    public async Task ExecuteAsync_NpcTalk_ReturnsPromptWithNpcSelectionAndTopic()
    {
        await SeedStoryTurnAsync(42);
        await SeedNpcStateAsync();

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/npc_talk npc_elara_001",
            OwnerId: "browser-npc-social-test",
            OwnerLabel: "Browser NPC social test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.NotNull(result.InteractiveSession);
        Assert.True(result.InteractiveSession!.RequiresLocalUiLock);

        var npcPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "npc_id"));
        Assert.Contains(npcPrompt.Options, option => option.Value == "npc_elara_001" && option.Label.Contains("Элара", StringComparison.Ordinal));
        Assert.Contains(npcPrompt.Options, option => option.Value == "npc_old_guard" && option.Label.Contains("Старый страж", StringComparison.Ordinal));

        var topicPrompt = Assert.IsType<UiLongTextInputPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "npc_conversation_topic"));
        Assert.True(topicPrompt.Required);
        Assert.Contains("тему", topicPrompt.Prompt, StringComparison.OrdinalIgnoreCase);

        var text = CollectResultAndPromptText(result);
        Assert.Contains("Разговор с НПС", text, StringComparison.Ordinal);
        Assert.Contains("Элара", text, StringComparison.Ordinal);
        AssertNoRawNpcSocialDiagnosticText(text);
    }

    [Fact]
    [Trait("Category", "BrowserMortalWorldWriteService")]
    public async Task SubmitAsync_NpcTalk_WritesPendingRequestWithTopicAndTurn()
    {
        await SeedStoryTurnAsync(42);
        await SeedNpcStateAsync();

        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/поговорить_с_нпс npc_elara_001",
            OwnerId: "browser-npc-social-test",
            OwnerLabel: "Browser NPC social test"));
        Assert.NotNull(prompt.InteractiveSession);

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(
                ("npc_id", "npc_elara_001"),
                ("npc_conversation_topic", "спросить о рыжем ключе")),
            OwnerId: "browser-npc-social-test"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
        AssertNoRawNpcSocialDiagnosticText(CollectResultAndPromptText(result));

        var root = JsonNode.Parse((await _fs.ReadFileAsync(ActorSocialInteractionRequestState.PendingNpcRequestPath))!)!.AsObject();
        var request = Assert.Single(root["requests"]!.AsArray())!.AsObject();
        Assert.StartsWith("npc_social_", request["requestId"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("npc_elara_001", request["npcId"]!.GetValue<string>());
        Assert.Equal("Элара", request["npcName"]!.GetValue<string>());
        Assert.Equal(ActorSocialInteractionRequestState.NpcInteractionTypeTalk, request["interactionType"]!.GetValue<string>());
        Assert.Equal(42, request["createdAtTurn"]!.GetValue<int>());
        Assert.False(string.IsNullOrWhiteSpace(request["createdAtUtc"]!.GetValue<string>()));
        Assert.Equal("спросить о рыжем ключе", request["topic"]!.GetValue<string>());
    }

    [Fact]
    [Trait("Category", "BrowserMortalWorldWriteService")]
    public async Task SubmitAsync_NpcTalkWithCommandArgument_AllowsTopicOnlySubmission()
    {
        await SeedStoryTurnAsync(42);
        await SeedNpcStateAsync();

        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/npc_talk npc_elara_001",
            OwnerId: "browser-npc-social-test",
            OwnerLabel: "Browser NPC social test"));
        Assert.NotNull(prompt.InteractiveSession);

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(("npc_conversation_topic", "спросить о рыжем ключе")),
            OwnerId: "browser-npc-social-test"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoRawNpcSocialDiagnosticText(CollectResultAndPromptText(result));

        var root = JsonNode.Parse((await _fs.ReadFileAsync(ActorSocialInteractionRequestState.PendingNpcRequestPath))!)!.AsObject();
        var request = Assert.Single(root["requests"]!.AsArray())!.AsObject();
        Assert.Equal("npc_elara_001", request["npcId"]!.GetValue<string>());
        Assert.Equal("спросить о рыжем ключе", request["topic"]!.GetValue<string>());
    }

    [Fact]
    [Trait("Category", "BrowserMortalWorldWriteService")]
    public async Task TryApplyAsync_NpcTalkDuplicatePending_ReturnsPlayerFacingPendingWithoutOverwrite()
    {
        await SeedStoryTurnAsync(42);
        await SeedNpcStateAsync();
        await ActorSocialInteractionRequestState.WriteNpcRequestAsync(_fs, new ActorSocialInteractionRequestState.PendingNpcSocialInteractionRequest
        {
            RequestId = "npc_social_existing",
            NpcId = "npc_elara_001",
            NpcName = "Элара",
            InteractionType = ActorSocialInteractionRequestState.NpcInteractionTypeTalk,
            CreatedAtTurn = 41,
            CreatedAtUtc = "2026-06-06T01:00:00Z"
        });
        var before = await _fs.ReadFileAsync(ActorSocialInteractionRequestState.PendingNpcRequestPath);

        var result = await _mortalWriteService.TryApplyAsync(
            "/npc_talk npc_elara_001",
            Answers(
                ("npc_id", "npc_elara_001"),
                ("npc_conversation_topic", "снова спросить о рыжем ключе")),
            Owner("browser-npc-social-test"));

        Assert.True(result.Handled);
        Assert.False(result.Success);
        Assert.False(result.KeepSessionOpen);
        Assert.Equal(CommandExecutionState.Pending, result.State);
        Assert.Contains("уже ожидает", result.Message, StringComparison.OrdinalIgnoreCase);
        AssertNoRawNpcSocialDiagnosticText(result.Title + "\n" + result.Message);
        Assert.Equal(before, await _fs.ReadFileAsync(ActorSocialInteractionRequestState.PendingNpcRequestPath));
    }

    [Fact]
    [Trait("Category", "BrowserMortalWorldWriteService")]
    public async Task TryApplyAsync_NpcTalkSameOwnerDuplicate_DoesNotOverwritePendingRequest()
    {
        await SeedStoryTurnAsync(42);
        await SeedNpcStateAsync();
        await ActorSocialInteractionRequestState.WriteNpcRequestAsync(_fs, new ActorSocialInteractionRequestState.PendingNpcSocialInteractionRequest
        {
            RequestId = "npc_social_existing",
            NpcId = "npc_elara_001",
            NpcName = "Элара",
            InteractionType = ActorSocialInteractionRequestState.NpcInteractionTypeTalk,
            Topic = "первая тема",
            CreatedAtTurn = 41,
            CreatedAtUtc = "2026-06-06T01:00:00Z"
        });
        var owner = Owner("browser-npc-social-test");
        await _fs.WriteFileAtomicAsync(LocalUiSessionLockService.LockPath, """
        {
          "ownerId": "browser-npc-social-test",
          "ownerKind": "browser",
          "ownerLabel": "Browser NPC social test",
          "acquiredAtUtc": "2026-06-06T01:00:00Z",
          "heartbeatAtUtc": "2026-06-06T01:00:00Z",
          "leaseSeconds": 120,
          "lastOperation": "Разговор с НПС"
        }
        """);
        var before = await _fs.ReadFileAsync(ActorSocialInteractionRequestState.PendingNpcRequestPath);

        var result = await _mortalWriteService.TryApplyAsync(
            "/npc_talk npc_elara_001",
            Answers(
                ("npc_id", "npc_elara_001"),
                ("npc_conversation_topic", "вторая тема")),
            owner);

        Assert.True(result.Handled);
        Assert.False(result.Success);
        Assert.Equal(CommandExecutionState.Pending, result.State);
        Assert.Equal(before, await _fs.ReadFileAsync(ActorSocialInteractionRequestState.PendingNpcRequestPath));
    }

    [Fact]
    [Trait("Category", "BrowserMortalWorldWriteService")]
    public async Task SubmitAsync_NpcTalkMalformedPendingFailure_ReturnsPlayerFacingCopyWithoutDiagnostics()
    {
        await SeedStoryTurnAsync(42);
        await SeedNpcStateAsync();
        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/npc_talk npc_elara_001",
            OwnerId: "browser-npc-social-test",
            OwnerLabel: "Browser NPC social test"));
        Assert.NotNull(prompt.InteractiveSession);
        await _fs.WriteFileAtomicAsync(ActorSocialInteractionRequestState.PendingNpcRequestPath, "{ malformed");

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(
                ("npc_id", "npc_elara_001"),
                ("npc_conversation_topic", "спросить о рыжем ключе")),
            OwnerId: "browser-npc-social-test"));

        Assert.Equal(CommandExecutionState.Failed, result.State);
        AssertNoRawNpcSocialDiagnosticText(CollectResultAndPromptText(result));
        Assert.Equal("{ malformed", await _fs.ReadFileAsync(ActorSocialInteractionRequestState.PendingNpcRequestPath));
    }

    [Fact]
    [Trait("Category", "BrowserMortalWorldWriteService")]
    public async Task SubmitAsync_NpcTalkMalformedLocalUiLock_ReturnsPlayerFacingCopyWithoutDiagnostics()
    {
        await SeedStoryTurnAsync(42);
        await SeedNpcStateAsync();
        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/npc_talk npc_elara_001",
            OwnerId: "browser-npc-social-test",
            OwnerLabel: "Browser NPC social test"));
        Assert.NotNull(prompt.InteractiveSession);
        await _fs.WriteFileAtomicAsync(LocalUiSessionLockService.LockPath, "{ malformed");

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(
                ("npc_id", "npc_elara_001"),
                ("npc_conversation_topic", "спросить о рыжем ключе")),
            OwnerId: "browser-npc-social-test"));

        Assert.Equal(CommandExecutionState.Blocked, result.State);
        AssertNoRawNpcSocialDiagnosticText(CollectResultAndPromptText(result));
        Assert.Equal("{ malformed", await _fs.ReadFileAsync(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    [Trait("Category", "BrowserMortalWorldWriteService")]
    public async Task ExecuteAsync_NpcTalkInAfterlifeRealm_ReturnsRealmBlockerWithoutPrompt()
    {
        await SeedStoryTurnAsync(42);
        await SeedNpcStateAsync();
        await SeedSoulRealmAsync("Chaos Sea");

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/npc_talk npc_elara_001",
            OwnerId: "browser-npc-social-test",
            OwnerLabel: "Browser NPC social test"));

        Assert.NotEqual(CommandExecutionState.RequiresInput, result.State);
        Assert.Null(result.InteractiveSession);
        Assert.Empty(result.Prompts);
        var text = CollectResultAndPromptText(result);
        Assert.Contains("смертном мире", text, StringComparison.OrdinalIgnoreCase);
        AssertNoRawNpcSocialDiagnosticText(text);
        Assert.False(_fs.FileExists(ActorSocialInteractionRequestState.PendingNpcRequestPath));
    }

    [Fact]
    [Trait("Category", "BrowserMortalWorldWriteService")]
    public async Task SubmitAsync_NpcTalkAfterRealmSwitchToAfterlife_ReturnsRealmBlockerWithoutPendingWrite()
    {
        await SeedStoryTurnAsync(42);
        await SeedNpcStateAsync();
        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/npc_talk npc_elara_001",
            OwnerId: "browser-npc-social-test",
            OwnerLabel: "Browser NPC social test"));
        Assert.NotNull(prompt.InteractiveSession);
        await SeedSoulRealmAsync("Chaos Sea");

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(("npc_conversation_topic", "спросить о рыжем ключе")),
            OwnerId: "browser-npc-social-test"));

        Assert.Equal(CommandExecutionState.Blocked, result.State);
        var text = CollectResultAndPromptText(result);
        Assert.Contains("смертном мире", text, StringComparison.OrdinalIgnoreCase);
        AssertNoRawNpcSocialDiagnosticText(text);
        Assert.False(_fs.FileExists(ActorSocialInteractionRequestState.PendingNpcRequestPath));
    }

    [Fact]
    [Trait("Category", "BrowserWebUiParity")]
    public void BrowserCommandCoverage_Issue807NpcTalkCommandIsCovered()
    {
        var coverage = BrowserCommandCoverageService.Build();

        var command = Assert.Single(coverage.Commands, item => item.Id == "npc_talk");
        Assert.Equal("covered", command.AuditStatus);
        Assert.Equal(nameof(ExplorerCommandMigrationStatus.MutatingParity), command.BrowserStatus);
        Assert.Equal("guided-form", command.FormMode);
        Assert.Equal("player-default", command.Surface);
        Assert.Contains("/npc_talk", command.Aliases);
        Assert.Contains("/поговорить_с_нпс", command.Aliases);
        Assert.DoesNotContain("#807", command.FollowUpIssue, StringComparison.Ordinal);

        var npcs = Assert.Single(coverage.Commands, item => item.Id == "npcs");
        Assert.DoesNotContain("#807", npcs.FollowUpIssue, StringComparison.Ordinal);
        Assert.DoesNotContain("start-conversation", npcs.GapSummary, StringComparison.OrdinalIgnoreCase);
    }

    private async Task SeedStoryTurnAsync(int turnNumber)
    {
        await _fs.WriteFileAtomicAsync("stories/web-npc-social-test.json", $$"""
        {
          "turnNumber": {{turnNumber}}
        }
        """);
    }

    private async Task SeedNpcStateAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_elara_001",
              "name": "Элара",
              "currentLocation": "Рыночная площадь",
              "relationshipLevel": 15,
              "progressionType": "Known"
            },
            {
              "id": "npc_old_guard",
              "name": "Старый страж",
              "currentLocation": "Северные ворота",
              "relationshipLevel": 3
            }
          ]
        }
        """);
    }

    private async Task SeedSoulRealmAsync(string realm)
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", $$"""
        {
          "soulName": "Тестовая Душа",
          "currentRealm": {{JsonSerializer.Serialize(realm)}},
          "currentIncarnation": 1
        }
        """);
    }

    private static Dictionary<string, JsonNode?> Answers(params (string Key, object Value)[] pairs)
    {
        var answers = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var (key, value) in pairs)
        {
            answers[key] = value switch
            {
                bool flag => JsonValue.Create(flag),
                int number => JsonValue.Create(number),
                string text => JsonValue.Create(text),
                _ => JsonSerializer.SerializeToNode(value)
            };
        }

        return answers;
    }

    private static LocalUiSessionLockOwner Owner(string id) =>
        new(id, "browser", "Browser NPC social test", TimeSpan.FromSeconds(120));

    private static string CollectResultAndPromptText(ExplorerCommandResult result) =>
        CollectBlockText(result.Blocks) + "\n" +
        string.Join("\n", result.Prompts.Select(CollectPromptText)) + "\n" +
        string.Join("\n", result.Notifications.Select(notification => $"{notification.Title}\n{notification.Message}"));

    private static string CollectPromptText(UiPrompt prompt)
    {
        var parts = new List<string> { prompt.Prompt };
        if (prompt is UiSelectionPrompt selection)
        {
            foreach (var option in selection.Options)
            {
                parts.Add(option.Label);
                parts.Add(option.Description);
            }
        }

        if (prompt is UiTextInputPrompt textInput)
            parts.Add(textInput.Placeholder);
        if (prompt is UiLongTextInputPrompt longTextInput)
            parts.Add(longTextInput.Placeholder);

        return string.Join("\n", parts);
    }

    private static string CollectBlockText(IEnumerable<UiBlock> blocks)
    {
        var parts = new List<string>();
        foreach (var block in blocks)
            CollectBlockText(block, parts);
        return string.Join("\n", parts);
    }

    private static void CollectBlockText(UiBlock block, List<string> parts)
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
                    CollectBlockText(child, parts);
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
        }
    }

    private static void AssertNoRawNpcSocialDiagnosticText(string text)
    {
        Assert.DoesNotContain("game_state/", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".json", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pending_", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("requestId", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTO", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rollback", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("snapshot", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("protocol", text, StringComparison.OrdinalIgnoreCase);
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
            // Ignore temp cleanup failures.
        }
    }
}
