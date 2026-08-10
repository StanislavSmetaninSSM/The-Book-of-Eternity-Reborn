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

public sealed class ExplorerWebCommandServiceTestsShiningAbodeDrilldowns : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly ExplorerWebCommandService _service;

    public ExplorerWebCommandServiceTestsShiningAbodeDrilldowns()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-web-shining-drilldowns-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _stateManager = new StateManager(_fs, new GameSettings(), NullLogger<StateManager>.Instance);
        var validation = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        _service = new ExplorerWebCommandService(_fs, _stateManager, new LocalizationManager(), validation);
    }

    [Fact]
    public async Task ExecuteAsync_ShiningAbodeOverview_ExposesIssue1065ReadOnlyDetailActions()
    {
        await SeedRichShiningAbodeDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/shining_abode"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoShiningIssue1065TechnicalLeak(result);
        AssertIssue1065Action(
            result,
            "shining-gate-detail-card_social",
            "/shining_abode врата card_social",
            "Песнь Рассвета");
        AssertIssue1065Action(
            result,
            "shining-faction-detail-faction_lanterns",
            "/shining_politics фракция faction_lanterns",
            "Дом Фонарей");
        AssertIssue1065Action(
            result,
            "shining-project-detail-faction_lanterns-project_dawn",
            "/shining_abode проект faction_lanterns::project_dawn",
            "Проект Рассвета");
        AssertIssue1065Action(
            result,
            "shining-pending-core-detail-coreopen001",
            "/shining_abode ожидание coreopen001",
            "Открытие Врат");
        AssertIssue1065Action(
            result,
            "shining-core-receipt-detail-core_receipt_open",
            "/shining_abode исход core_receipt_open",
            "Врата открылись");
    }

    [Fact]
    public async Task ExecuteAsync_ShiningPoliticsOverview_ExposesIssue1065ReadOnlyDetailActions()
    {
        await SeedRichShiningAbodeDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/shining_politics"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoShiningIssue1065TechnicalLeak(result);
        AssertIssue1065Action(
            result,
            "shining-faction-detail-faction_lanterns",
            "/shining_politics фракция faction_lanterns",
            "Дом Фонарей");
        AssertIssue1065Action(
            result,
            "shining-chronicle-detail-chronicle_dawn",
            "/shining_politics хроника chronicle_dawn",
            "Рассветный спор");
        AssertIssue1065Action(
            result,
            "shining-resource-detail-ledger_sparks",
            "/shining_politics ресурс ledger_sparks",
            "Искры Света");
        AssertIssue1065Action(
            result,
            "shining-political-pending-detail-founding001",
            "/shining_politics ожидание founding001",
            "Дом Рассвета");
        AssertIssue1065Action(
            result,
            "shining-political-resolution-detail-founding_receipt_dawn",
            "/shining_politics решение founding_receipt_dawn",
            "Дом Рассвета");
    }

    [Theory]
    [InlineData("/shining_gates_select", "blessing_card_id", "shining-gate-detail-card_social", "/shining_abode врата card_social", "Песнь Рассвета")]
    [InlineData("/shining_project_support", "project_choice", "shining-project-detail-faction_lanterns-project_dawn", "/shining_abode проект faction_lanterns::project_dawn", "Проект Рассвета")]
    public async Task ExecuteAsync_ShiningLocalForms_PreservePromptsAndExposeReadOnlyContextActions(
        string command,
        string expectedPromptId,
        string expectedActionId,
        string expectedDetailCommand,
        string expectedLabelText)
    {
        await SeedRichShiningAbodeDrilldownFilesAsync(includePendingCoreAction: false);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            command,
            OwnerId: "browser-issue-1065",
            OwnerLabel: "Browser issue 1065"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.Contains(result.Prompts, prompt => prompt.Id == expectedPromptId);
        AssertIssue1065Action(result, expectedActionId, expectedDetailCommand, expectedLabelText);
    }

    [Theory]
    [InlineData("/shining_abode врата card_social", "Благословение Врат: Песнь Рассвета", "Песнь открывает мягкий путь", "Память Эха")]
    [InlineData("/shining_abode проект faction_lanterns::project_dawn", "Проект Сияющей Обители: Проект Рассвета", "первый сад над светлой площадью", "Скрытый Дом")]
    [InlineData("/shining_abode ожидание coreopen001", "Ожидающее действие Обители: Открытие Врат", "создано на ходу 89", "requestId")]
    [InlineData("/shining_abode исход core_receipt_open", "Итог действия Обители: Врата открылись", "черновик Врат", "actionType")]
    [InlineData("/shining_politics фракция faction_lanterns", "Фракция Сияющей Обители: Дом Фонарей", "Хартия дома фонарей", "Скрытый Дом")]
    [InlineData("/shining_politics хроника chronicle_dawn", "Хроника фракции: Рассветный спор", "открытый совет", "hidden_chronicle_marker")]
    [InlineData("/shining_politics ресурс ledger_sparks", "Ресурс фракции: Искры Света", "+8", "hidden_ledger_marker")]
    [InlineData("/shining_politics ожидание founding001", "Ожидающее решение фракций: Дом Рассвета", "должен получить зал", "pending_")]
    [InlineData("/shining_politics решение founding_receipt_dawn", "Решение фракций: Дом Рассвета", "основание принято", "raw")]
    public async Task ExecuteAsync_ShiningIssue1065Details_RenderFocusedPlayerFacingDetailWithoutRawJson(
        string command,
        string expectedTitle,
        string expectedText,
        string excludedText)
    {
        await SeedRichShiningAbodeDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoShiningIssue1065TechnicalLeak(result);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains(expectedTitle, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedText, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(excludedText, text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/shining_abode врата missing_card")]
    [InlineData("/shining_abode проект faction_lanterns::missing_project")]
    [InlineData("/shining_abode ожидание missing_core")]
    [InlineData("/shining_politics фракция missing_faction")]
    [InlineData("/shining_politics хроника missing_chronicle")]
    [InlineData("/shining_politics ресурс missing_ledger")]
    [InlineData("/shining_politics решение missing_resolution")]
    public async Task ExecuteAsync_ShiningIssue1065Details_UnknownIdsReturnPlayerFacingUnavailableText(string command)
    {
        await SeedRichShiningAbodeDrilldownFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoShiningIssue1065TechnicalLeak(result);
        Assert.Contains("не удалось открыть", CollectBlockText(result.Blocks), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExplorerCommandCatalog_ShiningIssue1065ReadOnlyCommands_AcceptDetailArguments()
    {
        Assert.True(ExplorerCommandCatalog.Require("shining_abode").AcceptsArguments);
        Assert.True(ExplorerCommandCatalog.Require("shining_politics").AcceptsArguments);
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

    private async Task SeedRichShiningAbodeDrilldownFilesAsync(bool includePendingCoreAction = true)
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая душа",
          "currentRealm": "Shining Abode",
          "currentIncarnation": 9,
          "inkFeathers": { "current": 80, "total": 80 },
          "afterlifeCombatProfile": { "capstones": {} }
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "activeGuardian": {
            "guardianId": "guardian_alpha",
            "canonicalName": "Азалия",
            "domain": "Порог Сна",
            "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель", "abodePower": 72 }
          },
          "guardians": [
            { "guardianId": "guardian_alpha", "canonicalName": "Азалия" }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardian_abode_residents.json", """
        {
          "schemaVersion": 1,
          "entries": [
            {
              "residentId": "resident_alen",
              "displayName": "Ален",
              "ascensionState": "ascended",
              "isPlayerVisible": true,
              "shiningFactionId": "faction_lanterns",
              "factionLoyaltyLevel": 60,
              "factionLoyaltyTier": "attached"
            },
            {
              "residentId": "resident_hidden",
              "displayName": "Скрытый резидент",
              "ascensionState": "ascended",
              "isPlayerVisible": false,
              "shiningFactionId": "faction_hidden"
            }
          ]
        }
        """);

        var shiningRoot = JsonNode.Parse("""
        {
          "availability": "active",
          "lightSparks": 60,
          "radiance": { "experience": 700, "tier": 4 },
          "treasury": {
            "depositedInkFeathers": 0,
            "claimableInkFeatherInterest": 0,
            "totalInterestClaimed": 0,
            "lastInterestSettlementCycleId": "",
            "exchangeCycleId": "",
            "exchangeThisCycleLightSparks": 0,
            "exchangeHistory": []
          },
          "gates": {
            "hasOpenDraft": true,
            "draftVersion": 7,
            "isStale": false,
            "availableBlessingCards": [
              {
                "cardId": "card_social",
                "dedupeKey": "social:card_social",
                "sourceType": "project",
                "displayName": "Песнь Рассвета",
                "displaySummary": "Песнь открывает мягкий путь для новой жизни.",
                "effectFamily": "social",
                "rarity": "rare",
                "sourceFactionId": "faction_lanterns",
                "sourceActorId": "project_dawn",
                "effectPayload": { "type": "test" }
              },
              {
                "cardId": "card_memory",
                "dedupeKey": "memory:card_memory",
                "sourceType": "project",
                "displayName": "Память Эха",
                "displaySummary": "Скрытая память не должна попасть в выбранную деталь.",
                "effectFamily": "memory",
                "rarity": "uncommon",
                "sourceFactionId": "faction_lanterns",
                "sourceActorId": "project_memory",
                "effectPayload": { "type": "test" }
              }
            ],
            "allCandidateBlessingCards": [
              {
                "cardId": "card_social",
                "dedupeKey": "social:card_social",
                "sourceType": "project",
                "displayName": "Песнь Рассвета",
                "displaySummary": "Песнь открывает мягкий путь для новой жизни.",
                "effectFamily": "social",
                "rarity": "rare",
                "sourceFactionId": "faction_lanterns",
                "sourceActorId": "project_dawn",
                "effectPayload": { "type": "test" }
              },
              {
                "cardId": "card_memory",
                "dedupeKey": "memory:card_memory",
                "sourceType": "project",
                "displayName": "Память Эха",
                "displaySummary": "Скрытая память не должна попасть в выбранную деталь.",
                "effectFamily": "memory",
                "rarity": "uncommon",
                "sourceFactionId": "faction_lanterns",
                "sourceActorId": "project_memory",
                "effectPayload": { "type": "test" }
              }
            ],
            "selectedBlessingCardIds": [ "card_social" ],
            "shownBlessingCardIds": [ "card_social", "card_memory" ],
            "nextCandidateCursor": 2,
            "rerollsRemaining": 1
          },
          "halls": [
            {
              "hallId": "hall_lanterns",
              "hallName": "Зал Фонарей",
              "description": "Зал принимает светлых резидентов."
            }
          ],
          "factions": [
            {
              "factionId": "faction_lanterns",
              "originType": "native_radiant",
              "hallId": "hall_lanterns",
              "isPlayerVisible": true,
              "visibility": "revealed",
              "factionStrength": 58,
              "factionLifecycle": { "state": "active" },
              "charter": {
                "factionName": "Дом Фонарей",
                "summary": "Хартия дома фонарей удерживает первый свет.",
                "favoredArchetype": "accord",
                "patronEffectFamily": "social"
              },
              "leadership": {
                "headActorType": "resident",
                "headActorId": "resident_alen",
                "headDisplayName": "Ален",
                "leadershipState": "secure"
              },
              "projects": [
                {
                  "projectId": "project_dawn",
                  "displayName": "Проект Рассвета",
                  "summary": "Строит первый сад над светлой площадью.",
                  "status": "completed",
                  "tier": 2,
                  "isSupported": false,
                  "isPlayerVisible": true,
                  "projectArchetype": "accord",
                  "outputEffectFamily": "social",
                  "strengthReward": 5
                }
              ],
              "chronicle": [
                {
                  "entryId": "chronicle_dawn",
                  "displayName": "Рассветный спор",
                  "turnNumber": 91,
                  "eventType": "public_aid",
                  "summary": "Дом Фонарей провёл открытый совет.",
                  "visibility": "visible",
                  "consequences": [ "Игрок может просить фракцию о публичной помощи." ],
                  "occurredAtUtc": "2026-05-25T12:00:00Z"
                },
                {
                  "entryId": "chronicle_hidden",
                  "displayName": "Тайная клятва",
                  "turnNumber": 92,
                  "eventType": "hidden_oath",
                  "summary": "hidden_chronicle_marker",
                  "visibility": "hidden",
                  "consequences": [ "hidden_chronicle_marker" ]
                }
              ],
              "territorialInfluence": [
                {
                  "zoneId": "lanterns_hall_public",
                  "displayName": "Серебряный Зал",
                  "controlLevel": 64,
                  "influenceValue": 58,
                  "publicStatus": "известное убежище",
                  "summary": "Фракция публично удерживает безопасный прием резидентов."
                }
              ],
              "resourceLedger": [
                {
                  "entryId": "ledger_sparks",
                  "turnNumber": 91,
                  "resourceType": "lightSparks",
                  "delta": 8,
                  "balanceAfter": 18,
                  "reason": "Публичная помощь привела к пожертвованиям Искр Света.",
                  "internalNote": "hidden_ledger_marker",
                  "occurredAtUtc": "2026-05-25T12:05:00Z"
                }
              ]
            },
            {
              "factionId": "faction_hidden",
              "originType": "native_radiant",
              "hallId": "hall_lanterns",
              "visibility": "hidden",
              "isPlayerVisible": false,
              "factionLifecycle": { "state": "active" },
              "charter": {
                "factionName": "Скрытый Дом",
                "summary": "Скрытый Дом не должен появляться.",
                "favoredArchetype": "accord",
                "patronEffectFamily": "social"
              },
              "leadership": {
                "headActorType": "resident",
                "headActorId": "resident_hidden",
                "leadershipState": "secure"
              },
              "projects": [
                {
                  "projectId": "project_secret",
                  "displayName": "Тайный проект",
                  "summary": "Скрытое содержание.",
                  "status": "completed",
                  "tier": 1,
                  "isSupported": false,
                  "projectArchetype": "accord",
                  "outputEffectFamily": "social",
                  "strengthReward": 1
                }
              ]
            }
          ],
          "shiningPoliticalActors": [],
          "coreActionReceipts": [
            {
              "receiptId": "core_receipt_open",
              "actionType": "open_gates",
              "status": "accepted",
              "summary": "Врата открылись и показали черновик Врат.",
              "generatedDraftVersion": 7,
              "resolvedAtTurn": 90
            }
          ],
          "factionFoundingReceipts": [
            {
              "receiptId": "founding_receipt_dawn",
              "requestId": "founding001",
              "status": "accepted",
              "factionId": "faction_dawn",
              "factionName": "Дом Рассвета",
              "summary": "основание принято после светлого совета.",
              "resolvedAtTurn": 90
            }
          ],
          "sourceOfLightCapstone": { "completed": false }
        }
        """)!.AsObject();

        foreach (var faction in shiningRoot["factions"]!
                     .AsArray()
                     .OfType<JsonObject>())
        {
            ShiningFactionTestMaterialization.Apply(
                faction,
                materializedAtTurn: 90,
                hasResidentAffiliations: true,
                canTrade: true);
        }

        await _fs.WriteFileAtomicAsync(
            "game_state/meta/shining_abode_state.json",
            shiningRoot.ToJsonString(JsonOptions));

        await _fs.WriteFileAtomicAsync(
            "game_state/control/pending_shining_abode_actions.json",
            includePendingCoreAction
                ? """
                  {
                    "requests": [
                      {
                        "requestId": "coreopen001",
                        "actionType": "open_gates",
                        "createdAtTurn": 89,
                        "summary": "Открыть Врата перед новым воплощением."
                      }
                    ]
                  }
                  """
                : """
                  {
                    "requests": []
                  }
                  """);

        await _fs.WriteFileAtomicAsync("game_state/control/pending_shining_faction_foundings.json", """
        {
          "requests": [
            {
              "requestId": "founding001",
              "proposedFactionId": "faction_dawn",
              "proposedFactionName": "Дом Рассвета",
              "proposedHallName": "Зал Рассвета",
              "createdAtTurn": 89,
              "summary": "Дом Рассвета должен получить зал и хартию."
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/control/pending_shining_faction_realignments.json", """
        {
          "requests": []
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/control/pending_shining_faction_leadership.json", """
        {
          "requests": []
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/control/pending_shining_trade.json", """
        {
          "requests": []
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/control/pending_source_of_light_capstone.json", """
        {
          "requests": []
        }
        """);
    }

    private static void AssertIssue1065Action(
        ExplorerCommandResult result,
        string expectedActionId,
        string expectedCommand,
        string expectedLabelText)
    {
        var action = Assert.Single(result.Actions, candidate => candidate.Id == expectedActionId);
        Assert.Equal(expectedCommand, action.Command);
        Assert.Contains("Подробно", action.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedLabelText, action.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UiActionStyle.Secondary, action.Style);
        Assert.False(action.RequiresConfirmation);
        Assert.DoesNotContain("/", action.Label, StringComparison.Ordinal);
        Assert.DoesNotContain("DTO", action.Label, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API", action.Label, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertNoShiningIssue1065TechnicalLeak(ExplorerCommandResult result)
    {
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        var payload = SerializeResult(result);
        foreach (var forbidden in new[]
                 {
                     ".json", "game_state/", "DTO", "API", "endpoint", "debug", "exception",
                     "UiRawJsonBlock", "pending_", "requestId", "actionType"
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
        JsonSerializer.Serialize(result, JsonOptions);
}
