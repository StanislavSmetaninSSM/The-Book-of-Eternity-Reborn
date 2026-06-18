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

public sealed class BrowserAfterlifeArchiveParityTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ExplorerWebCommandService _commandService;

    public BrowserAfterlifeArchiveParityTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-browser-archive-parity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        var stateManager = new StateManager(_fs, new GameSettings(), NullLogger<StateManager>.Instance);
        var validation = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        _commandService = new ExplorerWebCommandService(_fs, stateManager, new LocalizationManager(), validation);
    }

    [Fact]
    [Trait("Category", "BrowserAfterlifeArchiveParity")]
    public async Task ExecuteAsync_ArchiveConsultation_ReturnsEntryGuardianAndConfirmationPrompts()
    {
        await SeedArchiveStateAsync();
        await SeedGuardiansAsync();

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/archive_consultation",
            OwnerId: "browser-archive-test",
            OwnerLabel: "Browser archive test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.NotNull(result.InteractiveSession);
        Assert.True(result.InteractiveSession!.RequiresLocalUiLock);

        var archivePrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "archive_id"));
        Assert.Contains(archivePrompt.Options, option => option.Value == "archive_lore_001" && option.Label.Contains("Песнь Первого Маяка", StringComparison.Ordinal));
        Assert.DoesNotContain(archivePrompt.Options, option => option.Value == "archive_reserved");

        var guardianPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "guardian_id"));
        Assert.Contains(guardianPrompt.Options, option => option.Value == "guardian_azalia" && option.Label.Contains("Азалия", StringComparison.Ordinal));
        Assert.DoesNotContain(guardianPrompt.Options, option => option.Value == "guardian_wary");
        Assert.IsType<UiConfirmationPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "confirm_archive_consultation"));

        Assert.False(_fs.FileExists(AfterlifeArchiveActionState.ConsultationRequestPath));
        AssertNoBrowserArchiveTechnicalLeak(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "BrowserAfterlifeArchiveParity")]
    public async Task SubmitPromptSessionAsync_ArchiveConsultation_WritesExistingPendingRequestAndReservesEntry()
    {
        await SeedArchiveStateAsync();
        await SeedGuardiansAsync();

        var started = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/архивная_консультация",
            OwnerId: "browser-archive-test",
            OwnerLabel: "Browser archive test"));
        Assert.Equal(CommandExecutionState.RequiresInput, started.State);

        var completed = await _commandService.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            Answers(
                ("archive_id", "archive_lore_001"),
                ("guardian_id", "guardian_azalia"),
                ("confirm_archive_consultation", true)),
            OwnerId: "browser-archive-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));

        var request = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeArchiveActionState.ConsultationRequestPath))!)!.AsObject();
        Assert.Equal(AfterlifeArchiveActionState.RequestedModeConsultation, request["requestedMode"]!.GetValue<string>());
        Assert.Equal("guardian_azalia", request["guardianId"]!.GetValue<string>());
        Assert.Equal("Азалия", request["guardianName"]!.GetValue<string>());
        Assert.Equal("archive_lore_001", request["archiveId"]!.GetValue<string>());
        Assert.StartsWith("archive_consult_", request["requestId"]!.GetValue<string>(), StringComparison.Ordinal);

        var entry = ReadStoredArchiveEntry("archive_lore_001");
        var reservation = entry["reservation"]!.AsObject();
        Assert.Equal(AfterlifeArchiveState.ReservationKindConsultation, reservation["reservationKind"]!.GetValue<string>());
        Assert.Equal(request["requestId"]!.GetValue<string>(), reservation["requestId"]!.GetValue<string>());
        Assert.Equal("guardian_azalia", reservation["guardianId"]!.GetValue<string>());

        var playerText = CollectBlockText(completed.Blocks, includeRawJson: false) + " " + CollectNotifications(completed);
        Assert.Contains("Архивная консультация", playerText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Азалия", playerText, StringComparison.Ordinal);
        AssertNoBrowserArchiveTechnicalLeak(playerText);
    }

    [Fact]
    [Trait("Category", "BrowserAfterlifeArchiveParity")]
    public async Task SubmitPromptSessionAsync_ArchiveConsultation_StaleReservedEntryBlocksWithoutWriting()
    {
        await SeedArchiveStateAsync();
        await SeedGuardiansAsync();

        var started = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/archive_consultation",
            OwnerId: "browser-archive-test",
            OwnerLabel: "Browser archive test"));
        Assert.Equal(CommandExecutionState.RequiresInput, started.State);

        await SeedArchiveStateAsync(reservePrimaryEntry: true);
        var staleSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        var result = await _commandService.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            Answers(
                ("archive_id", "archive_lore_001"),
                ("guardian_id", "guardian_azalia"),
                ("confirm_archive_consultation", true)),
            OwnerId: "browser-archive-test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.False(_fs.FileExists(AfterlifeArchiveActionState.ConsultationRequestPath));
        Assert.Equal(staleSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        Assert.Contains("запись", CollectResultAndPromptText(result), StringComparison.OrdinalIgnoreCase);
        AssertNoBrowserArchiveTechnicalLeak(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "BrowserAfterlifeArchiveParity")]
    public async Task ExecuteAsync_ArchiveConsultation_WrongRealmBlocksWithoutWriting()
    {
        await SeedArchiveStateAsync(currentRealm: "Mortal World");
        await SeedGuardiansAsync();

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/archive_consultation",
            OwnerId: "browser-archive-test",
            OwnerLabel: "Browser archive test"));

        Assert.Equal(CommandExecutionState.Blocked, result.State);
        Assert.Null(result.InteractiveSession);
        Assert.False(_fs.FileExists(AfterlifeArchiveActionState.ConsultationRequestPath));
        Assert.Contains("посмертии", CollectResultAndPromptText(result), StringComparison.OrdinalIgnoreCase);
        AssertNoBrowserArchiveTechnicalLeak(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "BrowserAfterlifeArchiveParity")]
    public async Task ExecuteAsync_ArchiveConsultation_MalformedPendingRequestBlocksWithoutTechnicalCopy()
    {
        await SeedArchiveStateAsync();
        await SeedGuardiansAsync();
        await _fs.WriteFileAtomicAsync(AfterlifeArchiveActionState.ConsultationRequestPath, "{ broken");

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/archive_consultation",
            OwnerId: "browser-archive-test",
            OwnerLabel: "Browser archive test"));

        Assert.Equal(CommandExecutionState.Blocked, result.State);
        Assert.Null(result.InteractiveSession);
        Assert.Equal("{ broken", await _fs.ReadFileAsync(AfterlifeArchiveActionState.ConsultationRequestPath));
        Assert.Contains("провер", CollectResultAndPromptText(result), StringComparison.OrdinalIgnoreCase);
        AssertNoBrowserArchiveTechnicalLeak(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "BrowserAfterlifeArchiveParity")]
    public async Task SubmitPromptSessionAsync_ArchiveConsultation_UnconfirmedKeepsStateUnchanged()
    {
        await SeedArchiveStateAsync();
        await SeedGuardiansAsync();

        var started = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/archive_consultation",
            OwnerId: "browser-archive-test",
            OwnerLabel: "Browser archive test"));
        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        var result = await _commandService.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            Answers(
                ("archive_id", "archive_lore_001"),
                ("guardian_id", "guardian_azalia"),
                ("confirm_archive_consultation", false)),
            OwnerId: "browser-archive-test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.False(_fs.FileExists(AfterlifeArchiveActionState.ConsultationRequestPath));
        Assert.Equal(beforeSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        Assert.Contains("Подтвердите", CollectNotifications(result), StringComparison.OrdinalIgnoreCase);
        AssertNoBrowserArchiveTechnicalLeak(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "BrowserAfterlifeArchiveParity")]
    public async Task SubmitPromptSessionAsync_ArchiveConsultation_ActiveGmTurnBlocksWithoutWriting()
    {
        await SeedArchiveStateAsync();
        await SeedGuardiansAsync();

        var started = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/archive_consultation",
            OwnerId: "browser-archive-test",
            OwnerLabel: "Browser archive test"));
        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """{"playerAction":"already waiting"}""");
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        var result = await _commandService.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            Answers(
                ("archive_id", "archive_lore_001"),
                ("guardian_id", "guardian_azalia"),
                ("confirm_archive_consultation", true)),
            OwnerId: "browser-archive-test"));

        Assert.Equal(CommandExecutionState.Blocked, result.State);
        Assert.False(_fs.FileExists(AfterlifeArchiveActionState.ConsultationRequestPath));
        Assert.Equal(beforeSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        Assert.Contains("ход", CollectResultAndPromptText(result), StringComparison.OrdinalIgnoreCase);
        AssertNoBrowserArchiveTechnicalLeak(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "BrowserAfterlifeArchiveParity")]
    public async Task ExecuteAsync_ArchiveProjectFuel_ReturnsOnlyFriendlyGuardiansWithActiveProjects()
    {
        await SeedArchiveStateAsync();
        await SeedGuardiansAsync();
        await SeedActiveProjectsAsync();

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/archive_project_fuel",
            OwnerId: "browser-archive-test",
            OwnerLabel: "Browser archive test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        var archivePrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "archive_id"));
        Assert.Contains(archivePrompt.Options, option => option.Value == "archive_lore_001");

        var guardianPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "guardian_id"));
        var option = Assert.Single(guardianPrompt.Options);
        Assert.Equal("guardian_azalia", option.Value);
        Assert.Contains("Песнь кузни", option.Description, StringComparison.Ordinal);
        Assert.IsType<UiConfirmationPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "confirm_archive_project_fuel"));
        AssertNoBrowserArchiveTechnicalLeak(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "BrowserAfterlifeArchiveParity")]
    public async Task ExecuteAsync_ArchiveProjectFuel_NoActiveProjectCompletesUnavailableWithoutWriting()
    {
        await SeedArchiveStateAsync();
        await SeedGuardiansAsync();
        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """{"activeProjects": []}""");

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/archive_project_fuel",
            OwnerId: "browser-archive-test",
            OwnerLabel: "Browser archive test"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Null(result.InteractiveSession);
        Assert.Empty(result.Prompts);
        Assert.False(_fs.FileExists(AfterlifeArchiveActionState.ProjectFuelRequestPath));
        Assert.Contains("проект", CollectResultAndPromptText(result), StringComparison.OrdinalIgnoreCase);
        AssertNoBrowserArchiveTechnicalLeak(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "BrowserAfterlifeArchiveParity")]
    public async Task SubmitPromptSessionAsync_ArchiveProjectFuel_WritesExistingPendingRequestWithTargetProject()
    {
        await SeedArchiveStateAsync();
        await SeedGuardiansAsync();
        await SeedActiveProjectsAsync();

        var started = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/архивная_подпитка_проекта",
            OwnerId: "browser-archive-test",
            OwnerLabel: "Browser archive test"));
        Assert.Equal(CommandExecutionState.RequiresInput, started.State);

        var completed = await _commandService.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            Answers(
                ("archive_id", "archive_lore_001"),
                ("guardian_id", "guardian_azalia"),
                ("confirm_archive_project_fuel", true)),
            OwnerId: "browser-archive-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var request = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeArchiveActionState.ProjectFuelRequestPath))!)!.AsObject();
        Assert.Equal(AfterlifeArchiveActionState.RequestedModeProjectFuel, request["requestedMode"]!.GetValue<string>());
        Assert.Equal("guardian_azalia", request["guardianId"]!.GetValue<string>());
        Assert.Equal("project_forge_song", request["targetProjectId"]!.GetValue<string>());
        Assert.Equal("Песнь кузни", request["targetProjectName"]!.GetValue<string>());
        Assert.Equal("archive_lore_001", request["archiveId"]!.GetValue<string>());

        var entry = ReadStoredArchiveEntry("archive_lore_001");
        var reservation = entry["reservation"]!.AsObject();
        Assert.Equal(AfterlifeArchiveState.ReservationKindProjectFuel, reservation["reservationKind"]!.GetValue<string>());
        Assert.Equal("project_forge_song", reservation["targetProjectId"]!.GetValue<string>());

        var playerText = CollectBlockText(completed.Blocks, includeRawJson: false) + " " + CollectNotifications(completed);
        Assert.Contains("подпитка проекта", playerText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Песнь кузни", playerText, StringComparison.Ordinal);
        AssertNoBrowserArchiveTechnicalLeak(playerText);
    }

    [Fact]
    [Trait("Category", "BrowserAfterlifeArchiveParity")]
    public async Task SubmitPromptSessionAsync_ArchiveProjectFuel_StaleProjectBlocksWithoutWriting()
    {
        await SeedArchiveStateAsync();
        await SeedGuardiansAsync();
        await SeedActiveProjectsAsync();

        var started = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/archive_project_fuel",
            OwnerId: "browser-archive-test",
            OwnerLabel: "Browser archive test"));
        Assert.Equal(CommandExecutionState.RequiresInput, started.State);

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """{"activeProjects": []}""");
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        var result = await _commandService.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            Answers(
                ("archive_id", "archive_lore_001"),
                ("guardian_id", "guardian_azalia"),
                ("confirm_archive_project_fuel", true)),
            OwnerId: "browser-archive-test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.False(_fs.FileExists(AfterlifeArchiveActionState.ProjectFuelRequestPath));
        Assert.Equal(beforeSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        Assert.Contains("проект", CollectResultAndPromptText(result), StringComparison.OrdinalIgnoreCase);
        AssertNoBrowserArchiveTechnicalLeak(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "BrowserAfterlifeArchiveParity")]
    public void BrowserCommandCoverage_Issue816ArchiveAndDirectPullAreCoveredWithoutParentFollowUp()
    {
        var coverage = BrowserCommandCoverageService.Build();

        var consultation = Assert.Single(coverage.Commands, command => command.Id == "archive_consultation");
        Assert.Equal("covered", consultation.AuditStatus);
        Assert.Equal("guided-form", consultation.FormMode);
        Assert.DoesNotContain("#816", consultation.FollowUpIssue, StringComparison.Ordinal);

        var projectFuel = Assert.Single(coverage.Commands, command => command.Id == "archive_project_fuel");
        Assert.Equal("covered", projectFuel.AuditStatus);
        Assert.Equal("guided-form", projectFuel.FormMode);
        Assert.DoesNotContain("#816", projectFuel.FollowUpIssue, StringComparison.Ordinal);

        var gacha = Assert.Single(coverage.Commands, command => command.Id == "gacha");
        Assert.Equal("covered", gacha.AuditStatus);
        Assert.Equal("guided-form", gacha.FormMode);
        Assert.DoesNotContain("#816", gacha.FollowUpIssue, StringComparison.Ordinal);
        Assert.Contains("direct", string.Join(" ", gacha.Aliases) + " " + gacha.BrowserEvidence + " " + gacha.ParityNotes, StringComparison.OrdinalIgnoreCase);

        var archive = Assert.Single(coverage.Commands, command => command.Id == "afterlife_archive");
        Assert.DoesNotContain("#816", archive.FollowUpIssue, StringComparison.Ordinal);
        Assert.DoesNotContain("#817", archive.FollowUpIssue, StringComparison.Ordinal);
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

    private async Task SeedArchiveStateAsync(string currentRealm = "Chaos Sea", bool reservePrimaryEntry = false)
    {
        var reservationJson = reservePrimaryEntry
            ? """
              ,
                      "reservation": {
                        "reservationKind": "consultation",
                        "requestId": "archive_consult_existing",
                        "guardianId": "guardian_azalia",
                        "guardianName": "Азалия",
                        "targetProjectId": "",
                        "targetProjectName": "",
                        "createdAtTurn": 40,
                        "createdAtUtc": "2026-03-27T00:00:00Z"
                      }
              """
            : string.Empty;

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", $$"""
        {
          "soulName": "Тестовая душа",
          "currentRealm": "{{currentRealm}}",
          "currentIncarnation": 2,
          "inkFeathers": {
            "current": 12,
            "total": 12
          },
          "soulRelics": {
            "stored": [],
            "equipped": []
          },
          "afterlifeArchive": {
            "stored": [
              {
                "archiveId": "archive_lore_001",
                "entryType": "lore_fragment",
                "title": "Песнь Первого Маяка",
                "summary": "Фрагмент знания о первом свете.",
                "rarity": "Rare",
                "sourceLife": 2,
                "sourceKind": "codex",
                "acquiredAtUtc": "2026-03-26T00:00:00Z"{{reservationJson}}
              },
              {
                "archiveId": "archive_reserved",
                "entryType": "secret_record",
                "title": "Запечатанный договор",
                "summary": "Эта запись уже занята другим действием.",
                "rarity": "Uncommon",
                "sourceLife": 1,
                "sourceKind": "codex",
                "acquiredAtUtc": "2026-03-25T00:00:00Z",
                "reservation": {
                  "reservationKind": "project_fuel",
                  "requestId": "archive_fuel_existing",
                  "guardianId": "guardian_other",
                  "guardianName": "Другой Хранитель",
                  "targetProjectId": "other_project",
                  "targetProjectName": "Чужой проект",
                  "createdAtTurn": 39,
                  "createdAtUtc": "2026-03-25T00:00:00Z"
                }
              }
            ],
            "actionReceipts": []
          },
          "afterlifeArchiveUpdates": [],
          "archiveActionResolutions": []
        }
        """);
    }

    private async Task SeedGuardiansAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия",
              "domain": "memory",
              "relationshipData": {
                "currentReputation": 80
              }
            },
            {
              "guardianId": "guardian_wary",
              "canonicalName": "Недоверчивый Страж",
              "domain": "silence",
              "relationshipData": {
                "currentReputation": 49
              }
            }
          ]
        }
        """);
    }

    private async Task SeedActiveProjectsAsync()
    {
        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_azalia",
              "project": {
                "projectId": "project_forge_song",
                "projectName": "Песнь кузни",
                "projectType": "lore_research",
                "projectTier": "minor",
                "projectMode": "supportive"
              }
            },
            {
              "guardianId": "guardian_wary",
              "project": {
                "projectId": "project_hidden",
                "projectName": "Скрытый проект"
              }
            }
          ]
        }
        """);
    }

    private JsonObject ReadStoredArchiveEntry(string archiveId)
    {
        var soul = JsonNode.Parse(_fs.ReadFileAsync("game_state/meta/soul_state.json").GetAwaiter().GetResult()!)!.AsObject();
        return soul["afterlifeArchive"]!["stored"]!.AsArray()
            .OfType<JsonObject>()
            .Single(entry => string.Equals(entry["archiveId"]!.GetValue<string>(), archiveId, StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, JsonNode?> Answers(params (string Key, object? Value)[] values)
    {
        var result = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            result[key] = value switch
            {
                null => null,
                bool flag => JsonValue.Create(flag),
                int number => JsonValue.Create(number),
                string text => JsonValue.Create(text),
                _ => JsonValue.Create(value.ToString())
            };
        }

        return result;
    }

    private static string CollectResultAndPromptText(ExplorerCommandResult result) =>
        CollectBlockText(result.Blocks, includeRawJson: false) + "\n" +
        string.Join("\n", result.Prompts.Select(CollectPromptText)) + "\n" +
        CollectNotifications(result);

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

        return string.Join("\n", parts);
    }

    private static string CollectBlockText(IEnumerable<UiBlock> blocks, bool includeRawJson)
    {
        var parts = new List<string>();
        foreach (var block in blocks)
            CollectBlockText(block, parts, includeRawJson);
        return string.Join("\n", parts);
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
                foreach (var row in table.Rows)
                    parts.AddRange(row.Cells);
                break;
            case UiKeyValueGridBlock grid:
                foreach (var item in grid.Items)
                {
                    parts.Add(item.Key);
                    parts.Add(item.Value);
                }
                break;
            case UiListBlock list:
                parts.AddRange(list.Items);
                break;
            case UiRawJsonBlock raw when includeRawJson:
                parts.Add(raw.Title);
                parts.Add(raw.Json?.ToJsonString() ?? "");
                break;
        }
    }

    private static string CollectNotifications(ExplorerCommandResult result) =>
        string.Join("\n", result.Notifications.Select(notification => notification.Title + "\n" + notification.Message));

    private static void AssertNoBrowserArchiveTechnicalLeak(string text)
    {
        foreach (var forbidden in new[]
                 {
                     ".json", "game_state/", "DTO", "endpoint", "validation", "debug", "exception",
                     "requestId=", "contract", "raw", "pending_"
                 })
        {
            Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
        }
    }
}
