using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ExplorerWebCommandServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly ExplorerWebCommandService _service;
    private readonly ValidationService _validationService;

    public ExplorerWebCommandServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-web-command-service-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _stateManager = new StateManager(_fs, new GameSettings(), NullLogger<StateManager>.Instance);
        _validationService = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        _service = new ExplorerWebCommandService(_fs, _stateManager, new LocalizationManager(), _validationService);
    }

    [Fact]
    public async Task ExecuteAsync_MigratedHelp_ReturnsCompletedDto()
    {
        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/help"));

        Assert.Equal("/help", result.Command);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains(result.Blocks, static block => block is UiTableBlock table && table.Columns.Contains("Описание"));
    }

    [Theory]
    [InlineData("/validate", "Валидация")]
    [InlineData("/world_setup", "Подготовка следующего мира")]
    [InlineData("/distribute", "Распределение характеристик")]
    [InlineData("/companion_directive", "Директивы компаньонов")]
    [InlineData("/faction_directive", "Директивы фракций")]
    [InlineData("/craft", "Ремесло")]
    [InlineData("/abode_offering", "Подношение Обители")]
    [InlineData("/found_guardian_mantle", "Основание собственной мантии")]
    [InlineData("/spiritual_action", "Духовное действие")]
    public async Task ExecuteAsync_LifecycleAndLocalTurnCommands_ReturnProtocolDtos(string command, string expectedRussianLabel)
    {
        await SeedUniversalMetaFilesAsync();
        await SeedMortalFilesAsync();
        await SeedChaosSeaFilesAsync();
        await SeedAfterlifeCombatAndEntityFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(command, result.Command);
        Assert.NotEqual(CommandExecutionState.Blocked, result.State);
        Assert.NotEmpty(result.Blocks);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains(expectedRussianLabel, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Локальный ход", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_LocalTurnCommandWithActiveGmTurn_ShowsPendingTurnProtocolState()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "session_web",
          "requestId": "request_web",
          "turnNumber": 12,
          "playerAction": "Тестовый ход",
          "timestamp": "2026-05-20T00:00:00Z"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.json", """
        {
          "sessionId": "session_web",
          "requestId": "request_web",
          "turnNumber": 12,
          "files": {}
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/world_setup"));

        Assert.Equal(CommandExecutionState.Pending, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Активный ход GM", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("input/turn_request.json", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("game_state/control/pending_turn_snapshot.json", text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/status")]
    [InlineData("/soul")]
    [InlineData("/codex")]
    [InlineData("/story")]
    [InlineData("/debug")]
    [InlineData("/галерея")]
    [InlineData("/saref")]
    public async Task ExecuteAsync_MigratedUniversalMetaCommands_ReturnCompletedDtos(string command)
    {
        await SeedUniversalMetaFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(command, result.Command);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.NotEmpty(result.Blocks);
        Assert.DoesNotContain(
            result.Blocks,
            static block => block is UiMessageBlock message &&
                            message.Title.Contains("пока недоступна", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("/inv")]
    [InlineData("/npc")]
    [InlineData("/quests")]
    [InlineData("/map")]
    [InlineData("/stats")]
    [InlineData("/combat")]
    [InlineData("/weather")]
    [InlineData("/books")]
    [InlineData("/interactions")]
    public async Task ExecuteAsync_MigratedMortalReadOnlyCommands_ReturnCompletedDtos(string command)
    {
        await SeedMortalFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(command, result.Command);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.NotEmpty(result.Blocks);
        Assert.DoesNotContain(
            result.Blocks,
            static block => block is UiMessageBlock message &&
                            message.Title.Contains("пока недоступна", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("/chaos_sea")]
    [InlineData("/guardians")]
    [InlineData("/abode_power")]
    [InlineData("/guardian_projects")]
    [InlineData("/abodes")]
    [InlineData("/gacha")]
    public async Task ExecuteAsync_MigratedChaosSeaReadOnlyCommands_ReturnCompletedDtos(string command)
    {
        await SeedChaosSeaFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(command, result.Command);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.NotEmpty(result.Blocks);
        Assert.DoesNotContain(
            result.Blocks,
            static block => block is UiMessageBlock message &&
                            message.Title.Contains("пока недоступна", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("/shining_abode")]
    [InlineData("/shining_politics")]
    [InlineData("/shining_treasury")]
    [InlineData("/source_of_light")]
    public async Task ExecuteAsync_MigratedShiningAbodeCommands_ReturnCompletedDtos(string command)
    {
        await SeedShiningAbodeFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(command, result.Command);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.NotEmpty(result.Blocks);
        Assert.DoesNotContain(
            result.Blocks,
            static block => block is UiMessageBlock message &&
                            message.Title.Contains("пока недоступна", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("/afterlife_profiles", "Профили сущностей посмертия")]
    [InlineData("/afterlife_inbox", "Уведомления загробья")]
    [InlineData("/spiritual_conflict", "Духовный конфликт")]
    [InlineData("/spiritual_combat_log", "Журнал духовного боя")]
    [InlineData("/spiritual_combat_help", "Духовный бой")]
    [InlineData("/spiritual_arts", "Духовные искусства")]
    public async Task ExecuteAsync_MigratedAfterlifeCombatAndEntityCommands_ReturnCompletedDtos(string command, string expectedRussianLabel)
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(command, result.Command);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.NotEmpty(result.Blocks);
        Assert.Contains(expectedRussianLabel, CollectBlockText(result.Blocks), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            result.Blocks,
            static block => block is UiMessageBlock message &&
                            message.Title.Contains("пока недоступна", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_EmptyCommand_ReturnsFailedDto()
    {
        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("   "));

        Assert.Equal(CommandExecutionState.Failed, result.State);
        var message = Assert.IsType<UiMessageBlock>(Assert.Single(result.Blocks));
        Assert.Contains("пустая", message.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }

    private async Task SeedUniversalMetaFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Test Soul",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 3,
          "inkFeathers": { "current": 12, "total": 34 },
          "enlightenment": { "currentTier": "Искра", "experience": 42 },
          "livesHistory": [
            { "incarnation": 1, "summary": "Первая жизнь" }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("lore/codex_entries.json", """
        {
          "entries": [
            { "title": "Первый знак", "content": "Тестовая запись кодекса" }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "Тестовые мысли ГМ"
        }
        """);

        await _fs.WriteFileAtomicAsync("stories/chaos_sea.jsonl", """
        {"turn":1,"timestamp":"2026-05-20T00:00:00Z","realm":"Chaos Sea","player":"test","narrative":"story"}

        """);
    }

    private async Task SeedMortalFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", """
        {
          "items": [
            { "itemId": "blade_1", "itemName": "Старый клинок", "quantity": 1 }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "npcs": [
            { "npcId": "npc_1", "name": "Мирра", "status": "alive" }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/quests/regular_quests.json", """
        {
          "activeQuests": [
            { "questId": "quest_1", "title": "Найти след", "status": "active" }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", """
        {
          "locationName": "Старый тракт",
          "region": "Северный край",
          "description": "Дорога под серым небом."
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/world_map.json", """
        {
          "newLocations": [
            { "locationId": "old_road", "locationName": "Старый тракт" }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/combat/enemies.json", """
        {
          "enemies": [
            { "enemyId": "wolf_1", "name": "Волк", "status": "hostile" }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/world_time.json", """
        { "currentTime": "ночь" }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/weather.json", """
        { "currentState": "дождь" }
        """);

        await _fs.WriteFileAtomicAsync("game_state/inventory/item_text_updates.json", """
        {
          "entries": [
            { "itemId": "letter_1", "title": "Письмо" }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/misc/player_interactions.json", """
        {
          "interactions": [
            { "interactionId": "int_1", "summary": "Игроки встретились на тракте." }
          ]
        }
        """);
    }

    private async Task SeedChaosSeaFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Test Soul",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 4,
          "inkFeathers": { "current": 18, "total": 55 },
          "enlightenment": { "currentTier": "Пепельная искра", "experience": 70 }
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "activeGuardian": { "guardianId": "guardian_azalia", "guardianName": "Азалия" },
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_azalia",
            "currentAbodeName": "Сад Ночных Роз",
            "knownAbodes": [
              { "abodeId": "abode_azalia", "name": "Сад Ночных Роз", "guardianId": "guardian_azalia" }
            ]
          },
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия",
              "domain": "Social",
              "relationshipData": { "currentReputation": 12 },
              "abode": { "abodeId": "abode_azalia", "name": "Сад Ночных Роз" },
              "abodePower": { "currentPower": 30, "maxPower": 100 },
              "gachaSystem": {
                "chargesPerReturn": 1,
                "chargesUsedThisReturn": 0,
                "gachaHistory": []
              }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardian_projects.json", """
        {
          "projects": [
            { "projectId": "project_1", "guardianId": "guardian_azalia", "status": "active", "title": "Садовая клятва" }
          ],
          "journal": [
            { "entryId": "entry_1", "guardianId": "guardian_azalia", "summary": "Проект начат." }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/abode_power_journal.json", """
        {
          "guardianPowerEvents": [
            { "eventId": "power_1", "guardianId": "guardian_azalia", "reasonType": "offering", "finalDelta": 5 }
          ]
        }
        """);
    }

    private async Task SeedShiningAbodeFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Test Soul",
          "currentRealm": "Shining Abode",
          "currentIncarnation": 5,
          "inkFeathers": { "current": 24, "total": 90 },
          "afterlifeCombatProfile": { "capstones": {} }
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/shining_abode_state.json", """
        {
          "availability": "active",
          "lightSparks": 7,
          "radiance": { "experience": 260, "tier": 3 },
          "gates": { "hasOpenDraft": true },
          "treasury": {
            "depositedInkFeathers": 20,
            "claimableInkFeatherInterest": 2,
            "lastInterestSettlementCycleId": "cycle_5",
            "exchangeCycleId": "cycle_5",
            "exchangeThisCycleLightSparks": 1
          },
          "gachaSystem": {
            "chargesPerReturn": 2,
            "chargesUsedThisReturn": 1,
            "currentReturnCycleId": "cycle_5",
            "gachaHistory": []
          },
          "halls": [
            { "hallId": "hall_dawn", "hallName": "Зал Рассвета" }
          ],
          "factions": [
            {
              "factionId": "faction_lanterns",
              "hallId": "hall_dawn",
              "factionStrength": 40,
              "charter": { "factionName": "Фонари Рассвета" },
              "leadership": { "headActorType": "resident", "headActorId": "resident_1", "leadershipState": "secure" },
              "projects": [
                { "projectId": "project_light", "displayName": "Световой мост", "status": "active", "tier": 1 }
              ]
            }
          ],
          "shiningPoliticalActors": [
            { "actorId": "actor_1", "displayName": "Светозарный судья", "politicalStatus": "elder" }
          ],
          "coreActionReceipts": [
            { "receiptId": "receipt_1", "actionType": "draft_incarnation_package" }
          ],
          "sourceOfLightCapstone": { "completed": false }
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardian_abode_residents.json", """
        {
          "entries": [
            {
              "residentId": "resident_1",
              "displayName": "Лиара",
              "ascensionState": "ascended",
              "shiningFactionId": "faction_lanterns",
              "factionLoyaltyLevel": 60,
              "factionLoyaltyTier": "attached"
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            { "guardianId": "guardian_azalia", "canonicalName": "Азалия" }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/control/pending_shining_abode_actions.json", """
        {
          "requests": [
            { "requestId": "core_req_1", "actionType": "draft_incarnation_package" }
          ]
        }
        """);
    }

    private async Task SeedAfterlifeCombatAndEntityFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Test Soul",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 6,
          "inkFeathers": { "current": 40, "total": 120 },
          "enlightenment": { "currentTier": "Пламенный знак", "experience": 160 },
          "afterlifeCombatProfile": {
            "enlightenmentTier": 3,
            "radianceTier": 1,
            "spiritFocusTier": 2,
            "standardArts": {
              "pressure": 2,
              "guard": 1,
              "counter": 1,
              "maneuver": 2,
              "binding": 1
            }
          }
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/afterlife_entity_profiles.json", """
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "player_soul",
              "actorId": "player_soul",
              "displayName": "Test Soul",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 40, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "tier": 3, "experience": 160 },
                "radiance": { "tier": 1, "experience": 20 }
              },
              "standardArts": {
                "pressure": 2,
                "guard": 1,
                "counter": 1,
                "maneuver": 2,
                "binding": 1,
                "recover_spiritual_power": 1
              },
              "specialArts": [
                {
                  "artId": "rose_mirror_counter",
                  "displayName": "Зеркало Ночной Розы",
                  "baseOperation": "counter",
                  "tier": 1,
                  "effectSummary": "Контрприём оставляет болезненный образ в клятве противника.",
                  "costMultiplierPercent": 150,
                  "canTeachPlayer": true
                }
              ],
              "customStates": [
                { "stateId": "memory_echo", "stateName": "Эхо памяти", "currentValue": 2, "maxValue": 5 }
              ],
              "soulDissipationTier": 1,
              "progressionStrategy": {
                "summary": "Сначала усилить защиту и манёвр.",
                "priorityOrder": [ "guard", "maneuver", "pressure" ],
                "lastAutoProgressionCycleKey": "cycle_6"
              }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/afterlife_spiritual_conflict_state.json", """
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "conflict_1",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "conflictPosition": "player_advantaged",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "resolutionState": "active",
            "controlState": {
              "controlId": "control_1",
              "controllerSide": "player",
              "level": "hindered",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Оковы держат противника у края клятвы."
            },
            "actionEconomy": {
              "player": { "current": 7, "max": 8, "source": "spirit_focus" },
              "opposition": { "current": 5, "max": 6, "source": "profile" }
            },
            "playerSide": { "leadContestant": { "actorId": "player_soul", "displayName": "Test Soul" } },
            "oppositionSide": { "leadContestant": { "actorId": "guardian_shadow", "displayName": "Тень Хранителя" } },
            "exchangeLog": [
              {
                "exchangeId": "exchange_1",
                "operationType": "pressure",
                "outcome": "success",
                "exchangeAtTurn": 6,
                "before": { "conflictPosition": "contested", "oppositionSideStrain": "clear" },
                "after": { "conflictPosition": "player_advantaged", "oppositionSideStrain": "strained" },
                "diceAudit": {
                  "rolls": [
                    { "side": "player", "value": 15 },
                    { "side": "opposition", "value": 9 }
                  ],
                  "playerTotal": 18,
                  "oppositionTotal": 11,
                  "margin": 7
                },
                "rewardAudit": {
                  "currency": "ink_feathers",
                  "finalAmount": 3,
                  "resolvedAtTurn": 6
                }
              }
            ]
          },
          "recentConflicts": [
            {
              "conflictId": "conflict_done",
              "resolutionState": "resolved",
              "operationType": "pressure",
              "playerOutcome": "victory",
              "resolvedAtTurn": 5,
              "rewardAudit": {
                "currency": "ink_feathers",
                "finalAmount": 2,
                "resolvedAtTurn": 5
              }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/control/afterlife_notifications.json", """
        {
          "notifications": [
            {
              "notificationId": "notification_1",
              "notificationType": "guardian_quest_available",
              "requestId": "quest_req_1",
              "status": "unread",
              "guardianId": "guardian_azalia",
              "guardianName": "Азалия",
              "summary": "Хранитель предлагает тёмный след из прошлой жизни.",
              "createdAtTurn": 6,
              "createdAtUtc": "2026-05-20T00:00:00Z"
            }
          ]
        }
        """);
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
            case UiPanelBlock panel:
                parts.Add(panel.Title);
                foreach (var child in panel.Blocks)
                    CollectBlockText(child, parts);
                break;
            case UiTableBlock table:
                parts.Add(table.Title);
                parts.AddRange(table.Columns);
                parts.AddRange(table.Rows.SelectMany(static row => row.Cells));
                break;
            case UiListBlock list:
                parts.AddRange(list.Items);
                break;
            case UiKeyValueGridBlock grid:
                parts.AddRange(grid.Items.SelectMany(static item => new[] { item.Key, item.Value }));
                break;
            case UiMessageBlock message:
                parts.Add(message.Title);
                parts.Add(message.Message);
                break;
            case UiRawJsonBlock raw:
                parts.Add(raw.Title);
                break;
        }
    }
}
