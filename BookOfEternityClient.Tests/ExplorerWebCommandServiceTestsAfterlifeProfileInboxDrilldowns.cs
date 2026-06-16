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

namespace BookOfEternityClient.Tests;

public sealed class ExplorerWebCommandServiceTestsAfterlifeProfileInboxDrilldowns : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ExplorerWebCommandService _service;

    public ExplorerWebCommandServiceTestsAfterlifeProfileInboxDrilldowns()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-web-afterlife-profile-inbox-drilldowns-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        var stateManager = new StateManager(_fs, new GameSettings(), NullLogger<StateManager>.Instance);
        var validation = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        _service = new ExplorerWebCommandService(_fs, stateManager, new LocalizationManager(), validation);
    }

    [Theory]
    [InlineData("/afterlife_profiles", "afterlife-profile-detail-guardian_mirror", "/afterlife_profiles профиль guardian_mirror", "Хранитель Зеркал", "Сущности")]
    [InlineData("/afterlife_threats", "afterlife-threat-detail-threat_moth", "/afterlife_threats угроза threat_moth", "Моль Сомнений", "Видимые угрозы")]
    [InlineData("/afterlife_chronicles", "afterlife-chronicle-detail-chronicle_mirror", "/afterlife_chronicles хроника chronicle_mirror", "Зал зеркальной клятвы", "Ключевые события посмертия")]
    public async Task ExecuteAsync_AfterlifeProfileThreatChronicleOverviews_ExposeIssue1066ReadOnlyDetailActions(
        string command,
        string expectedActionId,
        string expectedCommand,
        string expectedLabelText,
        string expectedOverviewText)
    {
        await SeedRichAfterlifeProfileInboxDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoIssue1066TechnicalLeak(result);
        Assert.Contains(expectedOverviewText, CollectBlockText(result.Blocks), StringComparison.OrdinalIgnoreCase);
        AssertIssue1066Action(result, expectedActionId, expectedCommand, expectedLabelText);
    }

    [Theory]
    [InlineData("/afterlife_profiles профиль guardian_mirror", "Профиль посмертия: Хранитель Зеркал", "Собирает отражения", "hidden_profile_marker")]
    [InlineData("/afterlife_threats угроза threat_moth", "Угроза посмертия: Моль Сомнений", "плетёт сомнения", "hidden_threat_marker")]
    [InlineData("/afterlife_chronicles хроника chronicle_mirror", "Хроника посмертия: Зал зеркальной клятвы", "Игрок впервые вошёл", "hidden_chronicle_marker")]
    public async Task ExecuteAsync_AfterlifeIssue1066Details_RenderFocusedPlayerFacingDetailWithoutRawJson(
        string command,
        string expectedTitle,
        string expectedText,
        string excludedText)
    {
        await SeedRichAfterlifeProfileInboxDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoIssue1066TechnicalLeak(result);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains(expectedTitle, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedText, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(excludedText, text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/afterlife_profiles профиль missing_profile")]
    [InlineData("/afterlife_threats угроза missing_threat")]
    [InlineData("/afterlife_chronicles хроника missing_chronicle")]
    [InlineData("/afterlife_inbox уведомление missing_notice")]
    public async Task ExecuteAsync_AfterlifeIssue1066Details_UnknownIdsReturnPlayerFacingUnavailableText(string command)
    {
        await SeedRichAfterlifeProfileInboxDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoIssue1066TechnicalLeak(result);
        Assert.Contains("не удалось открыть", CollectBlockText(result.Blocks), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeInboxOverview_ExposesReadOnlyFollowThroughActionsWithoutRawOrMutation()
    {
        await SeedRichAfterlifeProfileInboxDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_inbox"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.Contains(result.Prompts, static prompt => prompt.Id == "notification_action");
        Assert.Contains(result.Prompts, static prompt => prompt.Id == "notification_id");
        AssertNoIssue1066TechnicalLeak(result);
        AssertIssue1066Action(
            result,
            "afterlife-inbox-detail-notif_guardian_profile",
            "/afterlife_inbox уведомление notif_guardian_profile",
            "Хранитель Зеркал");
        AssertIssue1066Action(
            result,
            "afterlife-inbox-guardian-notif_guardian_profile-guardian_mirror",
            "/guardians хранитель guardian_mirror",
            "Хранитель Зеркал");
        AssertIssue1066Action(
            result,
            "afterlife-inbox-profile-notif_guardian_profile-guardian_mirror",
            "/afterlife_profiles профиль guardian_mirror",
            "Хранитель Зеркал");
        AssertIssue1066Action(
            result,
            "afterlife-inbox-threat-notif_guardian_profile-threat_moth",
            "/afterlife_threats угроза threat_moth",
            "Моль Сомнений");
        AssertIssue1066Action(
            result,
            "afterlife-inbox-chronicle-notif_guardian_profile-chronicle_mirror",
            "/afterlife_chronicles хроника chronicle_mirror",
            "Зал зеркальной клятвы");
        AssertIssue1066Action(
            result,
            "afterlife-inbox-archive-notif_archive_project-archive_mirror",
            "/afterlife_archive запись archive_mirror",
            "Песнь Зеркала");
        AssertIssue1066Action(
            result,
            "afterlife-inbox-project-notif_archive_project-guardian_mirror-project_mirror",
            "/guardian_projects проект guardian_mirror::project_mirror",
            "Проект Зеркала");
        AssertIssue1066Action(
            result,
            "afterlife-inbox-shining-politics-notif_shining_foundation",
            "/shining_politics",
            "Сияющую Обитель");

        var detail = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_inbox уведомление notif_guardian_profile"));

        Assert.Equal(CommandExecutionState.Completed, detail.State);
        AssertNoIssue1066TechnicalLeak(detail);
        Assert.Contains("Уведомление загробья", CollectBlockText(detail.Blocks), StringComparison.OrdinalIgnoreCase);
        var storedJson = await _fs.ReadFileAsync("game_state/control/afterlife_notifications.json");
        Assert.NotNull(storedJson);
        var stored = JsonNode.Parse(storedJson)!;
        Assert.All(stored["notifications"]!.AsArray().OfType<JsonObject>(), notification =>
            Assert.Equal("unread", notification["status"]?.GetValue<string>()));
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeProfilesMalformedState_DefaultModeReturnsPlayerFacingUnavailableText()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/afterlife_entity_profiles.json", "{ broken profile state");

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_profiles"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoIssue1066TechnicalLeak(result);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Профили посмертия", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("не удалось прочитать", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("JSON повреждён", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Path:", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LineNumber", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BytePositionInLine", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExplorerCommandCatalog_AfterlifeIssue1066Commands_AcceptDetailArguments()
    {
        foreach (var commandId in new[] { "afterlife_profiles", "afterlife_threats", "afterlife_chronicles", "afterlife_inbox" })
        {
            var descriptor = ExplorerCommandCatalog.Require(commandId);

            Assert.Equal(ExplorerCommandBrowserHandlerKind.AfterlifeCombat, descriptor.BrowserHandlerKind);
            Assert.True(
                descriptor.AcceptsArguments,
                $"{commandId} must preserve read-only selected-detail arguments for #1066 / #949 AFD-005 browser drill-down actions.");
        }
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
            // Best-effort cleanup for temporary test files.
        }
    }

    private async Task SeedRichAfterlifeProfileInboxDrilldownFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая душа",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 12,
          "afterlifeCombatProfile": {
            "standardArts": { "pressure": 2, "guard": 1 }
          }
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/afterlife_entity_profiles.json", """
        {
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 4, "lightSparks": 1 },
              "progression": {
                "enlightenment": { "tier": 2, "experience": 8 },
                "radiance": { "tier": 1, "experience": 3 }
              },
              "standardArts": { "pressure": 2, "guard": 1 },
              "specialArts": [
                {
                  "artId": "mirror_oath",
                  "displayName": "Клятва Зеркала",
                  "tier": 1,
                  "effectSummary": "видит отражённые обещания"
                }
              ],
              "fateCards": [
                { "cardId": "mirror_card", "nameRu": "Песнь зеркальной двери", "status": "unlocked" }
              ],
              "goals": { "shortTermGoal": "защитить зеркальный зал" },
              "currentActivity": { "summary": "Собирает отражения у тихой воды" },
              "personalQuests": [
                { "questId": "quest_mirror", "title": "Собрать осколки клятвы", "status": "active" }
              ]
            },
            {
              "actorType": "guardian",
              "actorId": "hidden_profile",
              "displayName": "hidden_profile_marker",
              "isPlayerVisible": false,
              "currentActivity": { "summary": "не показывать игроку" }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/afterlife_active_threats.json", """
        {
          "threats": [
            {
              "threatId": "threat_moth",
              "displayName": "Моль Сомнений",
              "visibleToPlayer": true,
              "realm": "Chaos Sea",
              "intensity": 6,
              "threatArchetype": {
                "motivation": "subversion",
                "method": "deceptive",
                "summary": "подтачивает клятвы"
              },
              "currentActivity": {
                "summary": "плетёт сомнения вокруг зеркального зала",
                "activeState": "active",
                "startedAtTurn": 44
              },
              "impactProfile": {
                "primaryTargetType": "guardian",
                "primaryTargetName": "Хранитель Зеркал",
                "primaryImpact": "relationship",
                "baseImpactValue": 3
              },
              "linkedGuardianId": "guardian_mirror"
            },
            {
              "threatId": "hidden_threat",
              "displayName": "hidden_threat_marker",
              "visibleToPlayer": false
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/afterlife_chronicles.json", """
        {
          "chronicles": [
            {
              "chronicleId": "chronicle_mirror",
              "displayName": "Зал зеркальной клятвы",
              "isPlayerVisible": true,
              "scopeType": "guardian",
              "scopeId": "Хранитель Зеркал",
              "lastUpdatedTurn": 45,
              "lastEventsDescription": "Игрок впервые вошёл в зал зеркальной клятвы",
              "eventDescriptions": [
                "[Turn 44] Игрок впервые вошёл к зеркальной воде",
                "[Turn 45] Хранитель Зеркал признал клятву"
              ],
              "participants": [
                { "displayName": "Хранитель Зеркал", "actorType": "guardian" },
                "Душа игрока"
              ],
              "persistentConsequences": [ "клятва стала видимой" ],
              "openThreads": [ "найти второй осколок" ]
            },
            {
              "chronicleId": "hidden_chronicle_marker",
              "displayName": "hidden_chronicle_marker",
              "isPlayerVisible": false,
              "lastEventsDescription": "не раскрывать"
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_mirror",
              "canonicalName": "Хранитель Зеркал",
              "domain": "Зеркальный зал",
              "abode": { "abodeId": "abode_mirror", "name": "Приют отражений", "abodePower": 41 },
              "projects": [
                { "projectId": "project_mirror", "name": "Проект Зеркала", "state": "active", "progressPercent": 42 }
              ]
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_archive.json", """
        {
          "entries": [
            {
              "archiveId": "archive_mirror",
              "title": "Песнь Зеркала",
              "summary": "Память о первом отражении",
              "fullText": "Полный текст песни зеркала",
              "isPlayerVisible": true
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/control/afterlife_notifications.json", """
        {
          "notifications": [
            {
              "notificationId": "notif_guardian_profile",
              "notificationType": "guardian_quest_available",
              "requestId": "hidden_request_guardian",
              "status": "unread",
              "guardianId": "guardian_mirror",
              "guardianName": "Хранитель Зеркал",
              "summary": "Хранитель Зеркал ждёт разговора у тихой воды",
              "createdAtTurn": 46,
              "profileActorId": "guardian_mirror",
              "profileActorType": "guardian",
              "threatId": "threat_moth",
              "threatName": "Моль Сомнений",
              "chronicleId": "chronicle_mirror",
              "chronicleTitle": "Зал зеркальной клятвы"
            },
            {
              "notificationId": "notif_archive_project",
              "notificationType": "archive_project_fuel_accepted",
              "requestId": "hidden_request_archive",
              "status": "unread",
              "guardianId": "guardian_mirror",
              "guardianName": "Хранитель Зеркал",
              "archiveId": "archive_mirror",
              "archiveTitle": "Песнь Зеркала",
              "targetProjectId": "project_mirror",
              "targetProjectName": "Проект Зеркала",
              "summary": "Архивное знание подпитало Проект Зеркала",
              "createdAtTurn": 47
            },
            {
              "notificationId": "notif_shining_foundation",
              "notificationType": "shining_faction_founding_resolved",
              "requestId": "hidden_request_shining",
              "status": "unread",
              "summary": "Сияющая Обитель приняла основание дома",
              "createdAtTurn": 48
            }
          ]
        }
        """);
    }

    private static void AssertIssue1066Action(
        ExplorerCommandResult result,
        string expectedActionId,
        string expectedCommand,
        string expectedLabelText)
    {
        var action = Assert.Single(result.Actions, candidate => candidate.Id == expectedActionId);
        Assert.Equal(expectedCommand, action.Command);
        Assert.Contains(expectedLabelText, action.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UiActionStyle.Secondary, action.Style);
        Assert.False(action.RequiresConfirmation);
        Assert.DoesNotContain("/", action.Label, StringComparison.Ordinal);
        Assert.DoesNotContain("DTO", action.Label, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API", action.Label, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertNoIssue1066TechnicalLeak(ExplorerCommandResult result)
    {
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        var payload = SerializePlayerFacingResult(result);
        foreach (var forbidden in new[]
                 {
                     ".json", "game_state/", "DTO", "API", "endpoint", "debug", "exception",
                     "UiRawJsonBlock", "pending_", "requestId", "actionType", "hidden_",
                     "не раскрывать", "не показывать игроку", "gmThoughts"
                 })
        {
            Assert.DoesNotContain(forbidden, payload, StringComparison.OrdinalIgnoreCase);
        }
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
            case UiPanelBlock panel:
                parts.Add(panel.Title);
                foreach (var child in panel.Blocks)
                    CollectBlockText(child, parts);
                break;
            case UiMessageBlock message:
                parts.Add(message.Title);
                parts.Add(message.Message);
                break;
            case UiTextBlock text:
                parts.Add(text.Text);
                break;
            case UiKeyValueGridBlock grid:
                foreach (var item in grid.Items)
                {
                    parts.Add(item.Key);
                    parts.Add(item.Value);
                }
                break;
            case UiTableBlock table:
                parts.Add(table.Title);
                foreach (var column in table.Columns)
                    parts.Add(column);
                foreach (var row in table.Rows)
                foreach (var cell in row.Cells)
                    parts.Add(cell);
                break;
        }
    }

    private static string SerializePlayerFacingResult(ExplorerCommandResult result) =>
        JsonSerializer.Serialize(
            new
            {
                result.Blocks,
                result.Actions,
                result.Prompts,
                result.Notifications
            },
            JsonOptions);
}
