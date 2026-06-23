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

public sealed class BrowserGuardianSocialParityTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly ExplorerWebCommandService _commandService;
    private readonly ExplorerWebPromptSessionService _promptSessions;
    private readonly BrowserAfterlifeWriteService _afterlifeWriteService;

    public BrowserGuardianSocialParityTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-browser-guardian-social-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _stateManager = new StateManager(_fs, new GameSettings(), NullLogger<StateManager>.Instance);
        var validation = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var lockService = new LocalUiSessionLockService(_fs);
        var coordinator = new BrowserLocalWriteCoordinator(_fs, lockService, TimeProvider.System);
        var mortalWriteService = new BrowserMortalWorldWriteService(
            _fs,
            coordinator,
            new ScenarioCoreService(_fs, NullLogger<ScenarioCoreService>.Instance),
            TimeProvider.System);
        _afterlifeWriteService = new BrowserAfterlifeWriteService(_fs, _stateManager, coordinator);
        _promptSessions = new ExplorerWebPromptSessionService(
            _fs,
            _stateManager,
            lockService: lockService,
            mortalWorldWriteService: mortalWriteService,
            afterlifeWriteService: _afterlifeWriteService);
        _commandService = new ExplorerWebCommandService(_fs, _stateManager, new LocalizationManager(), validation, _promptSessions);
    }

    [Fact]
    [Trait("Category", "BrowserGuardianSocialParity")]
    public async Task ExecuteAsync_GuardianSocial_ReturnsPromptWithGuardianSelectionAndInteractionChoices()
    {
        await SeedStoryTurnAsync(42);
        await SeedGuardianStateAsync("Chaos Sea");

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/guardian_social guardian_alpha",
            OwnerId: "browser-guardian-social-test",
            OwnerLabel: "Browser Guardian social test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.NotNull(result.InteractiveSession);
        Assert.True(result.InteractiveSession!.RequiresLocalUiLock);

        var guardianPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "guardian_id"));
        Assert.Contains(guardianPrompt.Options, option => option.Value == "guardian_alpha" && option.Label.Contains("Азалия", StringComparison.Ordinal));
        Assert.Contains(guardianPrompt.Options, option => option.Value == "guardian_mirror" && option.Label.Contains("Зеркальный Страж", StringComparison.Ordinal));

        var interactionPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "guardian_interaction_type"));
        Assert.Contains(interactionPrompt.Options, option => option.Value == ActorSocialInteractionRequestState.GuardianInteractionTypeTalk && option.Label.Contains("Поговорить", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(interactionPrompt.Options, option => option.Value == ActorSocialInteractionRequestState.GuardianInteractionTypeLore && option.Label.Contains("зн", StringComparison.OrdinalIgnoreCase));

        var text = CollectResultAndPromptText(result);
        Assert.Contains("Общение с Хранителем", text, StringComparison.Ordinal);
        Assert.Contains("Азалия", text, StringComparison.Ordinal);
        AssertNoRawGuardianSocialDiagnosticText(text);
    }

    [Fact]
    [Trait("Category", "BrowserGuardianSocialParity")]
    public async Task ExecuteAsync_GuardianSocial_IgnoresNestedNonGuardianReferences()
    {
        await SeedStoryTurnAsync(42);
        await SeedGuardianStateWithNestedNonGuardianReferencesAsync("Chaos Sea");

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/guardian_social",
            OwnerId: "browser-guardian-social-test",
            OwnerLabel: "Browser Guardian social test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        var guardianPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "guardian_id"));
        Assert.Contains(guardianPrompt.Options, option => option.Value == "guardian_alpha" && option.Label.Contains("Азалия", StringComparison.Ordinal));
        Assert.DoesNotContain(guardianPrompt.Options, option => option.Value == "abode_shadow");
        Assert.DoesNotContain(guardianPrompt.Options, option => option.Value == "trade_echo");
        Assert.DoesNotContain(guardianPrompt.Options, option => option.Label.Contains("Ложная", StringComparison.OrdinalIgnoreCase));
        AssertNoRawGuardianSocialDiagnosticText(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "BrowserGuardianSocialParity")]
    public async Task ExecuteAsync_GuardianSocial_UsesActiveGuardianMirrorWhenGuardiansArrayMissing()
    {
        await SeedStoryTurnAsync(42);
        await SeedActiveGuardianOnlyStateAsync("Chaos Sea");

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/guardian_social guardian_active_only",
            OwnerId: "browser-guardian-social-test",
            OwnerLabel: "Browser Guardian social test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        var guardianPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "guardian_id"));
        var option = Assert.Single(guardianPrompt.Options);
        Assert.Equal("guardian_active_only", option.Value);
        Assert.Contains("Единственный Страж", option.Label, StringComparison.Ordinal);
        AssertNoRawGuardianSocialDiagnosticText(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "BrowserGuardianSocialParity")]
    public async Task SubmitAsync_GuardianSocial_RejectsNestedNonGuardianReferenceWithoutPendingWrite()
    {
        await SeedStoryTurnAsync(42);
        await SeedGuardianStateWithNestedNonGuardianReferencesAsync("Chaos Sea");

        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/guardian_social",
            OwnerId: "browser-guardian-social-test",
            OwnerLabel: "Browser Guardian social test"));
        Assert.NotNull(prompt.InteractiveSession);

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(
                ("guardian_id", "abode_shadow"),
                ("guardian_interaction_type", ActorSocialInteractionRequestState.GuardianInteractionTypeTalk)),
            OwnerId: "browser-guardian-social-test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        AssertNoRawGuardianSocialDiagnosticText(CollectResultAndPromptText(result));
        Assert.False(_fs.FileExists(ActorSocialInteractionRequestState.PendingGuardianRequestPath));
    }

    [Fact]
    [Trait("Category", "BrowserGuardianSocialParity")]
    public async Task SubmitAsync_GuardianTalk_WritesPendingRequestWithTurn()
    {
        await SeedStoryTurnAsync(42);
        await SeedGuardianStateAsync("Chaos Sea");

        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/общение_хранителя guardian_alpha",
            OwnerId: "browser-guardian-social-test",
            OwnerLabel: "Browser Guardian social test"));
        Assert.NotNull(prompt.InteractiveSession);

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(
                ("guardian_id", "guardian_alpha"),
                ("guardian_interaction_type", ActorSocialInteractionRequestState.GuardianInteractionTypeTalk)),
            OwnerId: "browser-guardian-social-test"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
        AssertNoRawGuardianSocialDiagnosticText(CollectResultAndPromptText(result));

        var root = JsonNode.Parse((await _fs.ReadFileAsync(ActorSocialInteractionRequestState.PendingGuardianRequestPath))!)!.AsObject();
        var request = Assert.Single(root["requests"]!.AsArray())!.AsObject();
        Assert.StartsWith("guardian_social_", request["requestId"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("guardian_alpha", request["guardianId"]!.GetValue<string>());
        Assert.Equal("Азалия", request["guardianName"]!.GetValue<string>());
        Assert.Equal(ActorSocialInteractionRequestState.GuardianInteractionTypeTalk, request["interactionType"]!.GetValue<string>());
        Assert.Equal(42, request["createdAtTurn"]!.GetValue<int>());
        Assert.False(string.IsNullOrWhiteSpace(request["createdAtUtc"]!.GetValue<string>()));
    }

    [Fact]
    [Trait("Category", "BrowserGuardianSocialParity")]
    public async Task SubmitAsync_GuardianLore_WritesPendingRequestWithLoreInteractionType()
    {
        await SeedStoryTurnAsync(42);
        await SeedGuardianStateAsync("Chaos Sea");

        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/guardian_social guardian_alpha",
            OwnerId: "browser-guardian-social-test",
            OwnerLabel: "Browser Guardian social test"));
        Assert.NotNull(prompt.InteractiveSession);

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(
                ("guardian_id", "guardian_alpha"),
                ("guardian_interaction_type", ActorSocialInteractionRequestState.GuardianInteractionTypeLore)),
            OwnerId: "browser-guardian-social-test"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoRawGuardianSocialDiagnosticText(CollectResultAndPromptText(result));

        var root = JsonNode.Parse((await _fs.ReadFileAsync(ActorSocialInteractionRequestState.PendingGuardianRequestPath))!)!.AsObject();
        var request = Assert.Single(root["requests"]!.AsArray())!.AsObject();
        Assert.Equal("guardian_alpha", request["guardianId"]!.GetValue<string>());
        Assert.Equal("Азалия", request["guardianName"]!.GetValue<string>());
        Assert.Equal(ActorSocialInteractionRequestState.GuardianInteractionTypeLore, request["interactionType"]!.GetValue<string>());
        Assert.Equal(42, request["createdAtTurn"]!.GetValue<int>());
    }

    [Fact]
    [Trait("Category", "BrowserGuardianSocialParity")]
    public async Task TryApplyAsync_GuardianSocialDuplicatePending_ReturnsPlayerFacingPendingWithoutOverwrite()
    {
        await SeedStoryTurnAsync(42);
        await SeedGuardianStateAsync("Chaos Sea");
        await ActorSocialInteractionRequestState.WriteGuardianRequestAsync(_fs, new ActorSocialInteractionRequestState.PendingGuardianSocialInteractionRequest
        {
            RequestId = "guardian_social_existing",
            GuardianId = "guardian_alpha",
            GuardianName = "Азалия",
            InteractionType = ActorSocialInteractionRequestState.GuardianInteractionTypeTalk,
            CreatedAtTurn = 41,
            CreatedAtUtc = "2026-06-06T01:00:00Z"
        });
        var before = await _fs.ReadFileAsync(ActorSocialInteractionRequestState.PendingGuardianRequestPath);

        var result = await _afterlifeWriteService.TryApplyAsync(
            "/guardian_social guardian_alpha",
            Answers(
                ("guardian_id", "guardian_alpha"),
                ("guardian_interaction_type", ActorSocialInteractionRequestState.GuardianInteractionTypeTalk)),
            Owner("browser-guardian-social-test"));

        Assert.True(result.Handled);
        Assert.False(result.Success);
        Assert.False(result.KeepSessionOpen);
        Assert.Equal(CommandExecutionState.Pending, result.State);
        Assert.Contains("уже ожидает", result.Message, StringComparison.OrdinalIgnoreCase);
        AssertNoRawGuardianSocialDiagnosticText(result.Title + "\n" + result.Message);
        Assert.Equal(before, await _fs.ReadFileAsync(ActorSocialInteractionRequestState.PendingGuardianRequestPath));
    }

    [Fact]
    [Trait("Category", "BrowserGuardianSocialParity")]
    public async Task ExecuteAsync_GuardianSocialInMortalWorld_ReturnsRealmBlockerWithoutPrompt()
    {
        await SeedStoryTurnAsync(42);
        await SeedGuardianStateAsync("Mortal World");

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/guardian_social guardian_alpha",
            OwnerId: "browser-guardian-social-test",
            OwnerLabel: "Browser Guardian social test"));

        Assert.NotEqual(CommandExecutionState.RequiresInput, result.State);
        Assert.Null(result.InteractiveSession);
        Assert.Empty(result.Prompts);
        var text = CollectResultAndPromptText(result);
        Assert.Contains("посмертии", text, StringComparison.OrdinalIgnoreCase);
        AssertNoRawGuardianSocialDiagnosticText(text);
        Assert.False(_fs.FileExists(ActorSocialInteractionRequestState.PendingGuardianRequestPath));
    }

    [Fact]
    [Trait("Category", "BrowserGuardianSocialParity")]
    public async Task SubmitAsync_GuardianSocialAfterRealmSwitchToMortalWorld_ReturnsRealmBlockerWithoutPendingWrite()
    {
        await SeedStoryTurnAsync(42);
        await SeedGuardianStateAsync("Chaos Sea");
        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/guardian_social guardian_alpha",
            OwnerId: "browser-guardian-social-test",
            OwnerLabel: "Browser Guardian social test"));
        Assert.NotNull(prompt.InteractiveSession);
        await SeedSoulRealmAsync("Mortal World");

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(
                ("guardian_id", "guardian_alpha"),
                ("guardian_interaction_type", ActorSocialInteractionRequestState.GuardianInteractionTypeLore)),
            OwnerId: "browser-guardian-social-test"));

        Assert.Equal(CommandExecutionState.Blocked, result.State);
        var text = CollectResultAndPromptText(result);
        Assert.Contains("посмертии", text, StringComparison.OrdinalIgnoreCase);
        AssertNoRawGuardianSocialDiagnosticText(text);
        Assert.False(_fs.FileExists(ActorSocialInteractionRequestState.PendingGuardianRequestPath));
    }

    [Fact]
    [Trait("Category", "BrowserGuardianSocialParity")]
    public async Task SubmitAsync_GuardianSocialMalformedPendingFailure_ReturnsPlayerFacingCopyWithoutDiagnostics()
    {
        await SeedStoryTurnAsync(42);
        await SeedGuardianStateAsync("Chaos Sea");
        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/guardian_social guardian_alpha",
            OwnerId: "browser-guardian-social-test",
            OwnerLabel: "Browser Guardian social test"));
        Assert.NotNull(prompt.InteractiveSession);
        await _fs.WriteFileAtomicAsync(ActorSocialInteractionRequestState.PendingGuardianRequestPath, "{ malformed");

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(
                ("guardian_id", "guardian_alpha"),
                ("guardian_interaction_type", ActorSocialInteractionRequestState.GuardianInteractionTypeTalk)),
            OwnerId: "browser-guardian-social-test"));

        Assert.Equal(CommandExecutionState.Failed, result.State);
        AssertNoRawGuardianSocialDiagnosticText(CollectResultAndPromptText(result));
        Assert.Equal("{ malformed", await _fs.ReadFileAsync(ActorSocialInteractionRequestState.PendingGuardianRequestPath));
    }

    [Fact]
    [Trait("Category", "BrowserWebUiParity")]
    public void BrowserCommandCoverage_Issue808GuardianSocialCommandIsCovered()
    {
        var coverage = BrowserCommandCoverageService.Build();

        var command = Assert.Single(coverage.Commands, item => item.Id == "guardian_social");
        Assert.Equal("covered", command.AuditStatus);
        Assert.Equal(nameof(ExplorerCommandMigrationStatus.MutatingParity), command.BrowserStatus);
        Assert.Equal("guided-form", command.FormMode);
        Assert.Equal("player-default", command.Surface);
        Assert.Contains("/guardian_social", command.Aliases);
        Assert.Contains("/общение_хранителя", command.Aliases);
        Assert.DoesNotContain("#808", command.FollowUpIssue, StringComparison.Ordinal);

        var guardians = Assert.Single(coverage.Commands, item => item.Id == "guardians");
        Assert.Equal("covered", guardians.AuditStatus);
        Assert.DoesNotContain("#808", guardians.FollowUpIssue, StringComparison.Ordinal);
        Assert.DoesNotContain("#817", guardians.FollowUpIssue, StringComparison.Ordinal);
        Assert.DoesNotContain("remain tracked interactive work", guardians.GapSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "BrowserWebUiParity")]
    public async Task Help_GuardianSocialCommandIsListedInAfterlifeHelp()
    {
        await SeedStoryTurnAsync(42);
        await SeedGuardianStateAsync("Chaos Sea");

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest("/help"));

        var text = CollectResultAndPromptText(result);
        Assert.Contains("Хранител", text, StringComparison.OrdinalIgnoreCase);
    }

    private async Task SeedStoryTurnAsync(int turnNumber)
    {
        await _fs.WriteFileAtomicAsync("stories/web-guardian-social-test.json", $$"""
        {
          "turnNumber": {{turnNumber}}
        }
        """);
    }

    private async Task SeedGuardianStateAsync(string realm)
    {
        await SeedSoulRealmAsync(realm);
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "domain": "Порог Сна",
              "manifestation": {
                "currentDisplayName": "Азалия"
              },
              "relationshipData": { "currentReputation": 120 },
              "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель" }
            },
            {
              "id": "guardian_mirror",
              "guardianName": "Зеркальный Страж",
              "domain": "Отражения",
              "relationshipData": { "currentReputation": 10 },
              "abode": { "abodeId": "abode_mirror", "name": "Зеркальная обитель" }
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_alpha",
            "canonicalName": "Азалия",
            "domain": "Порог Сна",
            "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель" }
          },
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_alpha"
          }
        }
        """);
    }

    private async Task SeedGuardianStateWithNestedNonGuardianReferencesAsync(string realm)
    {
        await SeedSoulRealmAsync(realm);
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "domain": "Порог Сна",
              "manifestation": {
                "currentDisplayName": "Азалия"
              },
              "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель" }
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_alpha",
            "canonicalName": "Азалия",
            "domain": "Порог Сна"
          },
          "chaosSeaNavigation": {
            "knownAbodes": [
              {
                "guardianId": "abode_shadow",
                "name": "Ложная Обитель",
                "abodeId": "abode_shadow"
              }
            ]
          },
          "tradeInventoryReceipts": [
            {
              "guardianId": "trade_echo",
              "name": "Ложный торговый след",
              "requestId": "guardian_trade_echo"
            }
          ]
        }
        """);
    }

    private async Task SeedActiveGuardianOnlyStateAsync(string realm)
    {
        await SeedSoulRealmAsync(realm);
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "activeGuardian": {
            "guardianId": "guardian_active_only",
            "canonicalName": "Единственный Страж",
            "domain": "Тихий Порог",
            "abode": { "abodeId": "abode_active", "name": "Одинокая обитель" }
          },
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_active"
          }
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
        new(id, "browser", "Browser Guardian social test", TimeSpan.FromSeconds(120));

    private static string CollectResultAndPromptText(ExplorerCommandResult result) =>
        UiTestTextCollector.CollectResultAndPromptText(result);

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

    private static void AssertNoRawGuardianSocialDiagnosticText(string text)
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
        Assert.DoesNotContain("raw", text, StringComparison.OrdinalIgnoreCase);
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
