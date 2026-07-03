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

public sealed class ExplorerWebCommandServiceTestsSpiritualConflictArtDrilldowns : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ExplorerWebCommandService _service;

    public ExplorerWebCommandServiceTestsSpiritualConflictArtDrilldowns()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-web-spiritual-conflict-art-drilldowns-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        var stateManager = new StateManager(_fs, new GameSettings(), NullLogger<StateManager>.Instance);
        var validation = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        _service = new ExplorerWebCommandService(_fs, stateManager, new LocalizationManager(), validation);
    }

    [Fact]
    public async Task ExecuteAsync_SpiritualConflictOverview_ExposesIssue1067ReadOnlyExchangeDetailActions()
    {
        await SeedRichSpiritualConflictArtDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/spiritual_conflict"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoIssue1067TechnicalLeak(result);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Духовный конфликт", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Стороны конфликта", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Рассветный нажим", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_exchange_marker", SerializeResult(result), StringComparison.OrdinalIgnoreCase);
        AssertIssue1067Action(
            result,
            "spiritual-conflict-exchange-detail-1",
            "/spiritual_conflict обмен 1",
            "Осмотреть обмен",
            "Рассветный нажим");
        Assert.DoesNotContain("exchange_sun_001", SerializeResult(result), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_SpiritualCombatLogOverview_ExposesIssue1067ReadOnlyExchangeAndRecentDetailActions()
    {
        await SeedRichSpiritualConflictArtDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/spiritual_combat_log"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoIssue1067TechnicalLeak(result);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Журнал духовного боя", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Рассветный нажим", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Победа у рассветной кромки", text, StringComparison.OrdinalIgnoreCase);
        AssertIssue1067Action(
            result,
            "spiritual-combat-log-exchange-detail-1",
            "/spiritual_combat_log обмен 1",
            "Разобрать запись боя",
            "Рассветный нажим");
        AssertIssue1067Action(
            result,
            "spiritual-combat-log-recent-detail-1",
            "/spiritual_combat_log итог 1",
            "Разобрать итог",
            "Победа у рассветной кромки");
        Assert.DoesNotContain("exchange_sun_001", SerializeResult(result), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("conflict_completed_001", SerializeResult(result), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_SpiritualArtsOverview_PreservesUpgradePromptsAndExposesReadOnlyArtInspectActions()
    {
        await SeedRichSpiritualConflictArtDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/spiritual_arts",
            OwnerId: "browser-issue-1067",
            OwnerLabel: "Browser issue 1067"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.Contains(result.Prompts, prompt => prompt.Id == "upgrade_target");
        Assert.Contains(result.Prompts, prompt => prompt.Id == "upgrade_currency");
        AssertNoIssue1067TechnicalLeak(result);
        AssertIssue1067Action(
            result,
            "spiritual-art-detail-pressure",
            "/spiritual_arts искусство pressure",
            "Осмотреть искусство",
            "Давление");
        AssertIssue1067Action(
            result,
            "spiritual-special-art-detail-mirror_guard",
            "/spiritual_arts особое mirror_guard",
            "Осмотреть искусство",
            "Зеркальная Защита");
    }

    [Theory]
    [InlineData("/spiritual_conflict обмен exchange_sun_001", "Обмен духовного конфликта: Рассветный нажим", "Тестовая Душа", "Хранитель Тени")]
    [InlineData("/spiritual_combat_log обмен exchange_sun_001", "Запись духовного боя: Рассветный нажим", "Давление игрока", "Чернильные Перья")]
    public async Task ExecuteAsync_SpiritualExchangeDetails_RenderFocusedPlayerFacingContextWithoutRawJson(
        string command,
        string expectedTitle,
        string expectedText,
        string expectedMoreText)
    {
        await SeedRichSpiritualConflictArtDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoIssue1067TechnicalLeak(result);
        Assert.Empty(result.Prompts);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains(expectedTitle, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedText, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedMoreText, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Давление", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("позиция", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("напряжение", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ОД", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Контрприём", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_exchange_marker", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gmOnlyNote", SerializeResult(result), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_SpiritualCombatLogRecentDetail_RendersOutcomeResolutionAndRewardWithoutHiddenFields()
    {
        await SeedRichSpiritualConflictArtDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/spiritual_combat_log итог conflict_completed_001"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoIssue1067TechnicalLeak(result);
        Assert.Empty(result.Prompts);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Итог духовного боя: Победа у рассветной кромки", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("решён", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("победа", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Чернильные Перья", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("4", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("за проверенный спор", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gm_only_recent_marker", SerializeResult(result), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/spiritual_arts искусство pressure", "Духовное искусство: Давление", "Тир", "база 3 ОД")]
    [InlineData("/spiritual_arts особое mirror_guard", "Особое духовное искусство: Зеркальная Защита", "Защита", "75% базовой стоимости")]
    public async Task ExecuteAsync_SpiritualArtDetails_RenderRankCostUseAvailabilityAndWriteBoundary(
        string command,
        string expectedTitle,
        string expectedText,
        string expectedMoreText)
    {
        await SeedRichSpiritualConflictArtDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoIssue1067TechnicalLeak(result);
        Assert.Empty(result.Prompts);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains(expectedTitle, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedText, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedMoreText, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("применение", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("доступно", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("локальная прокачка", text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/spiritual_conflict обмен missing_exchange")]
    [InlineData("/spiritual_combat_log обмен missing_exchange")]
    [InlineData("/spiritual_combat_log итог missing_recent")]
    [InlineData("/spiritual_arts искусство missing_art")]
    [InlineData("/spiritual_arts особое missing_special")]
    public async Task ExecuteAsync_SpiritualIssue1067Details_UnknownTargetsReturnPlayerFacingUnavailableText(string command)
    {
        await SeedRichSpiritualConflictArtDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoIssue1067TechnicalLeak(result);
        Assert.Contains("не удалось открыть", CollectBlockText(result.Blocks), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/spiritual_conflict обмен exchange_sun_001", AfterlifeSpiritualConflictState.StatePath, "{ broken spiritual conflict")]
    [InlineData("/spiritual_combat_log итог conflict_completed_001", AfterlifeSpiritualConflictState.StatePath, "{ broken spiritual conflict")]
    [InlineData("/spiritual_arts искусство pressure", "game_state/meta/soul_state.json", "{ broken soul profile")]
    public async Task ExecuteAsync_SpiritualIssue1067Details_MalformedStateDoesNotLeakParserDiagnostics(
        string command,
        string path,
        string malformedJson)
    {
        await SeedRichSpiritualConflictArtDrilldownFilesAsync();
        await _fs.WriteFileAtomicAsync(path, malformedJson);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoIssue1067TechnicalLeak(result);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("не удалось открыть", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("JSON повреждён", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Path:", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LineNumber", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BytePositionInLine", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_SpiritualIssue1067Details_DoNotMutateStateOrCreatePendingFiles()
    {
        await SeedRichSpiritualConflictArtDrilldownFilesAsync();
        var conflictBefore = await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath);
        var soulBefore = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        var profilesBefore = await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath);

        foreach (var command in new[]
                 {
                     "/spiritual_conflict обмен exchange_sun_001",
                     "/spiritual_combat_log итог conflict_completed_001",
                     "/spiritual_arts искусство pressure",
                     "/spiritual_arts особое mirror_guard"
                 })
        {
            var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

            Assert.Equal(CommandExecutionState.Completed, result.State);
            Assert.Empty(result.Prompts);
            AssertNoIssue1067TechnicalLeak(result);
        }

        Assert.Equal(conflictBefore, await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath));
        Assert.Equal(soulBefore, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        Assert.Equal(profilesBefore, await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath));
        Assert.False(_fs.FileExists("game_state/control/pending_spiritual_art_upgrade.json"));
        Assert.False(_fs.FileExists("game_state/control/pending_spiritual_action.json"));
        Assert.False(_fs.FileExists("game_state/control/pending_afterlife_spiritual_conflict_action.json"));
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public void ExplorerCommandCatalog_SpiritualIssue1067Commands_AcceptReadOnlyDetailArguments()
    {
        foreach (var commandId in new[] { "spiritual_conflict", "spiritual_combat_log", "spiritual_arts" })
        {
            var descriptor = ExplorerCommandCatalog.Require(commandId);

            Assert.Equal(ExplorerCommandBrowserHandlerKind.AfterlifeCombat, descriptor.BrowserHandlerKind);
            Assert.True(
                descriptor.AcceptsArguments,
                $"{commandId} must preserve read-only selected-detail arguments for #1067 / #949 AFD-006 browser drill-down actions.");
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

    private async Task SeedRichSpiritualConflictArtDrilldownFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 12,
          "inkFeathers": { "current": 9 },
          "lightSparks": { "current": 2 },
          "afterlifeCombatProfile": {
            "schemaVersion": 1,
            "enlightenmentTier": 3,
            "radianceTier": 1,
            "spiritFocusTier": 2,
            "standardArts": {
              "pressure": 2,
              "guard": 1,
              "counter": 1
            }
          }
        }
        """);

        await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, """
        {
          "profiles": [
            {
              "actorType": "player_soul",
              "actorId": "player_soul",
              "displayName": "Тестовая Душа",
              "realm": "Chaos Sea",
              "standardArts": {
                "pressure": 2,
                "guard": 1,
                "counter": 1
              },
              "specialArts": [
                {
                  "artId": "mirror_guard",
                  "displayName": "Зеркальная Защита",
                  "baseOperation": "guard",
                  "tier": 1,
                  "effectSummary": "Отражает часть чужого давления в мягкую брешь.",
                  "combatEffect": {
                    "triggerOperation": "guard",
                    "grantsOperation": "counter",
                    "summary": "Когда защита выдерживает давление, следующий контрприём получает преимущество."
                  },
                  "costMultiplierPercent": 75
                }
              ]
            },
            {
              "actorType": "guardian",
              "actorId": "guardian_shadow",
              "displayName": "Хранитель Тени",
              "visibility": "public"
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, """
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "conflict_sun_001",
            "displayName": "Спор у рассветной кромки",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "conflictPosition": "player_advantaged",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "resolutionState": "active",
            "controlState": {
              "controlId": "control_sun_001",
              "controllerSide": "player",
              "level": "hindered",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Оковы света мешают противнику отступить."
            },
            "actionEconomy": {
              "player": { "current": 6, "max": 8, "source": "spirit_focus" },
              "opposition": { "current": 4, "max": 6, "source": "profile" }
            },
            "playerSide": {
              "leadContestant": { "actorId": "player_soul", "actorType": "player", "displayName": "Тестовая Душа" },
              "supporters": []
            },
            "oppositionSide": {
              "leadContestant": { "actorId": "guardian_shadow", "actorType": "guardian", "displayName": "Хранитель Тени" },
              "supporters": []
            },
            "exchangeLog": [
              {
                "exchangeId": "exchange_sun_001",
                "displayName": "Рассветный нажим",
                "operationType": "pressure",
                "incomingAction": "Хранитель Тени давит холодной клятвой.",
                "playerAction": "Тестовая Душа отвечает рассветным именем.",
                "outcome": "success",
                "resultSummary": "Давление игрока раскрыло брешь и удержало позицию.",
                "reason": "за проверенный спор",
                "exchangeAtTurn": 12,
                "actionPointCost": { "player": 3, "opposition": 2 },
                "before": { "conflictPosition": "contested", "playerSideStrain": "clear", "oppositionSideStrain": "clear" },
                "after": { "conflictPosition": "player_advantaged", "playerSideStrain": "clear", "oppositionSideStrain": "strained" },
                "diceAudit": {
                  "rolls": [
                    { "side": "player", "value": 15 },
                    { "side": "opposition", "value": 9 }
                  ],
                  "playerTotal": 18,
                  "oppositionTotal": 11,
                  "margin": 7,
                  "gmOnlyNote": "hidden_exchange_marker"
                },
                "rewardAudit": {
                  "currency": "ink_feathers",
                  "finalAmount": 3,
                  "reason": "за проверенный спор",
                  "resolvedAtTurn": 12
                }
              },
              {
                "exchangeId": "hidden_exchange_marker",
                "displayName": "hidden_exchange_marker",
                "visibility": "gm_only",
                "operationType": "counter",
                "outcome": "success",
                "resultSummary": "hidden_exchange_marker"
              }
            ]
          },
          "recentConflicts": [
            {
              "conflictId": "conflict_completed_001",
              "displayName": "Победа у рассветной кромки",
              "resolutionState": "resolved",
              "operationType": "pressure",
              "playerOutcome": "victory",
              "resolvedAtTurn": 11,
              "resolutionSummary": "Конфликт решён: душа удержала кромку и получила передышку.",
              "rewardAudit": {
                "currency": "ink_feathers",
                "finalAmount": 4,
                "reason": "за проверенный спор",
                "resolvedAtTurn": 11
              },
              "gmOnlySummary": "gm_only_recent_marker"
            }
          ]
        }
        """);
    }

    private static void AssertIssue1067Action(
        ExplorerCommandResult result,
        string expectedActionId,
        string expectedCommand,
        string expectedVerb,
        string expectedLabelText)
    {
        var action = Assert.Single(result.Actions, candidate => candidate.Id == expectedActionId);
        Assert.Equal(expectedCommand, action.Command);
        Assert.Contains(expectedVerb, action.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedLabelText, action.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UiActionStyle.Secondary, action.Style);
        Assert.False(action.RequiresConfirmation);
        Assert.DoesNotContain("/", action.Label, StringComparison.Ordinal);
        Assert.DoesNotContain("DTO", action.Label, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API", action.Label, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertNoIssue1067TechnicalLeak(ExplorerCommandResult result)
    {
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        var payload = SerializeResult(result);
        foreach (var forbidden in new[]
                 {
                     "JsonException", "JSON повреждён", "Path:", "LineNumber", "BytePositionInLine",
                     "game_state/", ".json", "DTO", "API", "endpoint", "protocol", "debug", "UiRawJsonBlock",
                     "gmOnly", "gm_only", "hidden_", "secret_", "internal_", "requestId", "actionType"
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
            case UiEntityDossierBlock dossier:
                parts.Add(dossier.Title);
                parts.Add(dossier.Subtitle);
                parts.Add(dossier.Summary);
                parts.AddRange(dossier.Badges.Select(static badge => badge.Label));
                CollectEntityFacts(dossier.Facts, parts);
                CollectEntityMetrics(dossier.Metrics, parts);
                CollectEntityHints(dossier.Hints, parts);
                parts.AddRange(dossier.List);
                foreach (var card in dossier.Cards)
                    CollectEntityCardText(card, parts);
                foreach (var section in dossier.Sections)
                {
                    parts.Add(section.Title);
                    parts.Add(section.Summary);
                    parts.Add(section.CollectionLabel);
                    CollectEntityFacts(section.Facts, parts);
                    CollectEntityMetrics(section.Metrics, parts);
                    CollectEntityHints(section.Hints, parts);
                    parts.AddRange(section.List);
                    foreach (var card in section.Cards)
                        CollectEntityCardText(card, parts);
                    foreach (var child in section.Blocks)
                        CollectBlockText(child, parts);
                }
                break;
            case UiMessageBlock message:
                parts.Add(message.Title);
                parts.Add(message.Message);
                break;
            case UiTextBlock text:
                parts.Add(text.Text);
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
            case UiTableBlock table:
                parts.Add(table.Title);
                parts.AddRange(table.Columns);
                foreach (var row in table.Rows)
                    parts.AddRange(row.Cells);
                break;
            case UiRawJsonBlock raw:
                parts.Add(raw.Title);
                parts.Add(raw.Json?.ToJsonString(JsonOptions) ?? string.Empty);
                break;
        }
    }

    private static void CollectEntityCardText(UiEntityCard card, List<string> parts)
    {
        parts.Add(card.Title);
        parts.Add(card.Subtitle);
        parts.Add(card.Summary);
        parts.AddRange(card.Badges.Select(static badge => badge.Label));
        CollectEntityFacts(card.Facts, parts);
        CollectEntityMetrics(card.Metrics, parts);
        CollectEntityHints(card.Hints, parts);
        parts.AddRange(card.List);
        foreach (var child in card.Nested)
            CollectEntityCardText(child, parts);
        foreach (var child in card.Cards)
            CollectEntityCardText(child, parts);
    }

    private static void CollectEntityFacts(IEnumerable<UiEntityFact> facts, List<string> parts)
    {
        foreach (var fact in facts)
        {
            parts.Add(fact.Label);
            parts.Add(fact.Value);
        }
    }

    private static void CollectEntityMetrics(IEnumerable<UiEntityMetric> metrics, List<string> parts)
    {
        foreach (var metric in metrics)
        {
            parts.Add(metric.Label);
            parts.Add(metric.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            parts.Add(metric.Max.ToString(System.Globalization.CultureInfo.InvariantCulture));
            parts.Add(metric.Note);
        }
    }

    private static void CollectEntityHints(IEnumerable<UiEntityHint> hints, List<string> parts)
    {
        foreach (var hint in hints)
        {
            parts.Add(hint.Title);
            parts.Add(hint.Text);
        }
    }

    private static string SerializeResult(ExplorerCommandResult result) =>
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
