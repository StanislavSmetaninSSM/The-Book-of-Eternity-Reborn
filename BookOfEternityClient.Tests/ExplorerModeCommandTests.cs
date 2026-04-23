using System.Text.Json;
using System.Reflection;
using System.Collections;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;
using Spectre.Console.Rendering;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerModeCommandTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly GameSettings _settings;
    private readonly StateManager _stateManager;
    private readonly LocalizationManager _loc;
    private readonly TestExplorerConsole _console;
    private readonly TestClipboardService _clipboard;
    private readonly StoryService _storyService;
    private readonly WorldDirectiveService _worldDirectiveService;
    private readonly ScenarioCoreService _scenarioCoreService;
    private readonly AfterlifeArchiveCandidateService _afterlifeArchiveCandidateService;
    private readonly AfterlifeArchiveConsultationService _afterlifeArchiveConsultationService;
    private readonly AfterlifeArchiveProjectFuelService _afterlifeArchiveProjectFuelService;
    private readonly GuardianCorrectionService _guardianCorrectionService;
    private readonly SystemModService _systemModService;
    private readonly SystemGuardianLibraryService _systemGuardianLibraryService;
    private readonly NpcTradeService _npcTradeService;
    private readonly GuardianTradeService _guardianTradeService;
    private readonly PendingTurnStateService _pendingTurnStateService;
    private readonly SoulIdentityService _soulIdentityService;
    private readonly ExplorerMode _explorer;

    public ExplorerModeCommandTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-explorer-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _settings = new GameSettings();
        _stateManager = new StateManager(_fs, _settings, NullLogger<StateManager>.Instance);
        _loc = new LocalizationManager { CurrentLanguage = "ru" };
        _console = new TestExplorerConsole();
        _clipboard = new TestClipboardService();
        _storyService = new StoryService(_fs, NullLogger<StoryService>.Instance);
        _worldDirectiveService = new WorldDirectiveService(_fs, NullLogger<WorldDirectiveService>.Instance);
        _scenarioCoreService = new ScenarioCoreService(_fs, NullLogger<ScenarioCoreService>.Instance);
        _afterlifeArchiveCandidateService = new AfterlifeArchiveCandidateService(_fs, NullLogger<AfterlifeArchiveCandidateService>.Instance);
        _afterlifeArchiveConsultationService = new AfterlifeArchiveConsultationService(_fs, NullLogger<AfterlifeArchiveConsultationService>.Instance);
        _afterlifeArchiveProjectFuelService = new AfterlifeArchiveProjectFuelService(_fs, NullLogger<AfterlifeArchiveProjectFuelService>.Instance);
        _guardianCorrectionService = new GuardianCorrectionService(_fs, _scenarioCoreService, NullLogger<GuardianCorrectionService>.Instance);
        _systemModService = new SystemModService(_fs, _settings, NullLogger<SystemModService>.Instance);
        _systemGuardianLibraryService = new SystemGuardianLibraryService(_fs, NullLogger<SystemGuardianLibraryService>.Instance);
        _npcTradeService = new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance);
        _guardianTradeService = new GuardianTradeService(_fs, NullLogger<GuardianTradeService>.Instance);
        _pendingTurnStateService = new PendingTurnStateService(_fs, NullLogger<PendingTurnStateService>.Instance);
        _soulIdentityService = new SoulIdentityService(_fs, NullLogger<SoulIdentityService>.Instance);
        _explorer = new ExplorerMode(_stateManager, _fs, _loc,
            npcTradeService: _npcTradeService,
            guardianTradeService: _guardianTradeService,
            storyService: _storyService,
            pendingTurnState: _pendingTurnStateService,
            systemModService: _systemModService,
            systemGuardianLibraryService: _systemGuardianLibraryService,
            worldDirectiveService: _worldDirectiveService,
            scenarioCoreService: _scenarioCoreService,
            afterlifeArchiveCandidateService: _afterlifeArchiveCandidateService,
            afterlifeArchiveConsultationService: _afterlifeArchiveConsultationService,
            afterlifeArchiveProjectFuelService: _afterlifeArchiveProjectFuelService,
            guardianCorrectionService: _guardianCorrectionService,
            soulIdentityService: _soulIdentityService,
            clipboardService: _clipboard,
            console: _console);
    }

    private async Task SeedSessionForCommandAsync(string command)
    {
        var isAfterlife = command is "/душа" or "/хранители" or "/сила_обители" or "/проекты_хранителей" or "/реликвии" or "/архив_души" or "/архив_кандидаты";
        var realm = isAfterlife ? "Chaos Sea" : "Mortal Realm";

        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = realm,
            currentIncarnation = 1,
            inkFeathers = new { current = 3 },
            enlightenment = new { currentTier = "Новичок" },
            soulRelics = new
            {
                stored = new[]
                {
                    new
                    {
                        relicId = "relic_test_001",
                        name = "Искра Памяти",
                        description = "Реликвия для smoke test explorer mode.",
                        rarity = "Rare"
                    }
                },
                equipped = Array.Empty<object>()
            },
            afterlifeArchive = new
            {
                stored = new[]
                {
                    new
                    {
                        archiveId = "archive_test_001",
                        entryType = "lore_fragment",
                        title = "Фрагмент Лунной Летописи",
                        summary = "Сохранённая после смерти запись о древнем договоре.",
                        rarity = "Rare",
                        sourceLife = 1,
                        acquiredAtUtc = "2026-03-23T00:00:00Z",
                        tags = new[] { "lore", "moon" }
                    }
                }
            }
        });

        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guard_test_azalia",
                    canonicalName = "Азалия",
                    domain = "Social",
                    nameVariants = new
                    {
                        @default = "Азалия",
                        feminine = "Азалия",
                        masculine = (string?)null,
                        neutral = (string?)null
                    },
                    manifestation = new
                    {
                        currentDisplayName = "Азалия",
                        formFlexibility = "selective",
                        currentPresentationStyle = "feminine",
                        currentPronouns = "она/её",
                        appearanceDescription = "Тестовая текущая форма Азалии."
                    },
                    manifestationHistory = Array.Empty<object>(),
                    description = "Тестовая хранительница для smoke test explorer mode.",
                    abodePower = new
                    {
                        currentPower = 35,
                        tier = "Хрупкая",
                        lastUpdatedAt = "2026-03-23T00:00:00Z",
                        history = Array.Empty<object>()
                    },
                    relationshipData = new
                    {
                        currentReputation = 25,
                        lastInteraction = "2026-03-19T00:00:00Z",
                        reputationHistory = Array.Empty<object>()
                    },
                    gachaSystem = new
                    {
                        chargesPerReturn = 1,
                        chargesUsedThisReturn = 0,
                        gachaHistory = Array.Empty<object>()
                    },
                    questManagement = new
                    {
                        activeQuests = Array.Empty<object>(),
                        completedQuests = Array.Empty<object>()
                    }
                }
            },
            activeGuardian = new
            {
                guardianId = "guard_test_azalia",
                canonicalName = "Азалия",
                nameVariants = new
                {
                    @default = "Азалия",
                    feminine = "Азалия",
                    masculine = (string?)null,
                    neutral = (string?)null
                },
                manifestation = new
                {
                    currentDisplayName = "Азалия",
                    formFlexibility = "selective",
                    currentPresentationStyle = "feminine",
                    currentPronouns = "она/её",
                    appearanceDescription = "Тестовая текущая форма Азалии."
                },
                manifestationHistory = Array.Empty<object>()
            },
            chaosSeaNavigation = new
            {
                currentAbodeId = (string?)null
            }
        });

        if (isAfterlife)
        {
            await WriteJsonAsync(AfterlifeArchiveCandidateService.ManifestPath, new
            {
                sourceLife = 1,
                lastExtractedAt = "2026-03-26T00:00:00Z",
                candidates = new[]
                {
                    new
                    {
                        candidateId = "archive_candidate_codex_test_001",
                        sourceEntryId = "codex_test_001",
                        sourceLife = 1,
                        proposedEntryType = "lore_fragment",
                        title = "Летопись Серого Двора",
                        summary = "Кандидат в Архив для smoke test.",
                        rarity = "Uncommon",
                        status = "pending",
                        discoveredAt = "2026-03-24T00:00:00Z",
                        tags = new[] { "lore" }
                    }
                }
            });
        }

        if (command == "/проекты_хранителей")
        {
            await WriteJsonAsync(GuardianProjectState.TrackerPath, new
            {
                activeProjects = new[]
                {
                    new
                    {
                        guardianId = "guard_test_azalia",
                        project = new
                        {
                            projectId = "guardian_project_smoke_001",
                            projectType = "abode_expansion",
                            projectTier = "major",
                            projectMode = "internal",
                            projectName = "Тестовое расширение Обители",
                            activeState = "Surveying",
                            totalWork = 18,
                            workDone = 3,
                            totalStages = 3,
                            currentStage = 0,
                            pressure = 4,
                            stability = 78,
                            startedTurn = 1,
                            estimatedCompletionTurn = 6,
                            playerCanAssist = true,
                            assistDescription = "Поддержать контур силы."
                        }
                    }
                },
                completedProjects = Array.Empty<object>(),
                temporaryProjectModifiers = Array.Empty<object>()
            });
            await WriteJsonAsync(GuardianProjectState.JournalPath, new
            {
                entries = new[]
                {
                    new
                    {
                        entryId = "gpj_smoke_001",
                        turn = 2,
                        guardianId = "guard_test_azalia",
                        projectId = "guardian_project_smoke_001",
                        eventType = "started",
                        visibility = "player_known",
                        title = "Проект Хранителя начат",
                        summary = "Азалия начала тестовое расширение Обители.",
                        details = new[] { "Работа: 0 -> 3", "Pressure: 0 -> 4", "Stability: 80 -> 78" }
                    }
                }
            });
        }

        if (command == "/сила_обители")
        {
            await WriteJsonAsync(GuardianPowerEventState.JournalPath, new
            {
                entries = new[]
                {
                    new
                    {
                        entryId = "ape_smoke_001",
                        eventId = "ape_event_001",
                        turn = 2,
                        guardianId = "guard_test_azalia",
                        guardianName = "Азалия",
                        delta = 8,
                        reasonType = "project_completion",
                        sourceSurface = "completeGuardianProjects",
                        sourceId = "guardian_project_smoke_001",
                        title = "Проект усилил Обитель",
                        summary = "Тестовый проект поднял силу Обители Азалии.",
                        visibility = "player_known",
                        relatedGuardianId = (string?)null,
                        appliedAt = "2026-03-23T00:00:00Z",
                        audit = new
                        {
                            projectId = "guardian_project_smoke_001",
                            projectType = "abode_expansion"
                        }
                    }
                }
            });
        }

        if (command == "/коррективы_хранителя")
        {
            await WriteJsonAsync(GuardianCorrectionService.StatePath, new
            {
                lifeIncarnation = 1,
                appliedAt = "2026-03-23T00:00:00Z",
                guardianId = "guard_test_azalia",
                guardianName = "Азалия",
                intent = "hostile",
                reputationAtApplication = -65,
                powerBefore = 78,
                powerAfter = 58,
                baseBudgetPoints = 3,
                remainingBudgetPoints = 0,
                totalAbodePowerSpent = 20,
                summary = "Азалия уже встроила в старт враждебную нить судьбы.",
                scenarioCoreSnapshot = new
                {
                    scenarioCoreAssertions = new[]
                    {
                        new { assertionId = "core_role", category = "role_status", value = "Игрок начинает королём", @explicit = true, source = "structured_field" }
                    },
                    openCorrectionSlots = new[]
                    {
                        new { slotId = "slot_rival", slotType = "rival_thread", maxSeverity = "strong", allowsFriendly = true, allowsHostile = true, sourceAssertionId = "core_role" }
                    }
                },
                claimants = new[]
                {
                    new
                    {
                        guardianId = "guard_test_azalia",
                        guardianName = "Азалия",
                        intent = "hostile",
                        isActivePatron = true,
                        currentPower = 78,
                        powerAfter = 58,
                        baseBudgetPoints = 3,
                        preparationBudgetPoints = 0,
                        remainingBudgetPoints = 0,
                        claimStrengthBase = 5,
                        eligible = true,
                        sourceSummary = "Active patron claim."
                    },
                    new
                    {
                        guardianId = "guard_test_rival",
                        guardianName = "Нерис",
                        intent = "hostile",
                        isActivePatron = false,
                        currentPower = 64,
                        powerAfter = 64,
                        baseBudgetPoints = 3,
                        preparationBudgetPoints = 0,
                        remainingBudgetPoints = 3,
                        claimStrengthBase = 4,
                        eligible = true,
                        sourceSummary = "Rival hostile claim."
                    }
                },
                contestedSlots = new[]
                {
                    new
                    {
                        slotId = "slot_rival",
                        slotType = "rival_thread",
                        winnerGuardianId = "guard_test_azalia",
                        winnerGuardianName = "Азалия",
                        winnerCorrectionId = "corr_rival",
                        candidates = new[]
                        {
                            new
                            {
                                candidateCorrectionId = "corr_rival",
                                sourceGuardianId = "guard_test_azalia",
                                sourceGuardianName = "Азалия",
                                intent = "hostile",
                                severity = "strong",
                                budgetCostPoints = 3,
                                abodePowerCost = 20,
                                claimStrength = 8,
                                title = "Враждебная нить судьбы"
                            },
                            new
                            {
                                candidateCorrectionId = "corr_rival_other",
                                sourceGuardianId = "guard_test_rival",
                                sourceGuardianName = "Нерис",
                                intent = "hostile",
                                severity = "medium",
                                budgetCostPoints = 2,
                                abodePowerCost = 12,
                                claimStrength = 6,
                                title = "Чужая интрига"
                            }
                        }
                    }
                },
                resolutionOrder = new[] { "slot_rival: Азалия [strong]" },
                corrections = new[]
                {
                    new
                    {
                        correctionId = "corr_rival",
                        sourceGuardianId = "guard_test_azalia",
                        sourceGuardianName = "Азалия",
                        intent = "hostile",
                        slotId = "slot_rival",
                        slotType = "rival_thread",
                        severity = "strong",
                        budgetCostPoints = 3,
                        abodePowerCost = 20,
                        claimStrength = 8,
                        title = "Враждебная нить судьбы (сильная корректива)",
                        summary = "Азалия вносит сильную коррективу: в мире уже зреет параллельная враждебная линия, способная войти в конфликт с игроком.",
                        reason = "Азалия враждебно тратит силу Обители, навязывая совместимый конфликт вокруг исходного сценария.",
                        affectsStartAs = "rival_thread"
                    }
                }
            });
        }

        if (command == "/инв")
        {
            await WriteJsonAsync("game_state/inventory/items.json", new
            {
                items = new[]
                {
                    new
                    {
                        itemId = "item_test_sword",
                        name = "Тестовый меч",
                        description = "Обычный клинок для explorer smoke test.",
                        type = "weapon",
                        quality = "Common"
                    }
                }
            });
        }

        if (command == "/карта")
        {
            await WriteJsonAsync("game_state/world/current_location.json", new
            {
                name = "Тестовая площадь",
                description = "Текущая локация для smoke test.",
                coordinates = new { x = 1, y = 2, z = 0 },
                adjacencyMap = Array.Empty<object>()
            });
        }

        if (command == "/квесты")
        {
            await WriteJsonAsync("game_state/quests/regular_quests.json", new
            {
                quests = new[]
                {
                    new
                    {
                        questId = "quest_test_001",
                        questName = "Тестовый контракт",
                        status = "Active",
                        description = "Тестовый квест для smoke test explorer mode.",
                        objectives = new[]
                        {
                            new
                            {
                                description = "Проверить экран квестов",
                                status = "Active"
                            }
                        }
                    }
                }
            });
        }

        if (command == "/чужие_нити")
        {
            await WriteJsonAsync("game_state/world/rival_soul_arcs.json", new
            {
                arcs = new object[]
                {
                    new
                    {
                        arcId = "arc_test_001",
                        scope = "major",
                        arcType = "rival_ascension",
                        status = "rising",
                        sponsorGuardianRef = new { mode = "guardianId", guardianId = "guard_test_azalia", displayName = "Азалия" },
                        rivalSoul = new { rivalSoulId = "soul_rival_001", displayNameOrMoniker = "Юный Император", roleSummary = "Восходящий гений", isKnownToPlayer = true },
                        objective = "Подняться к власти",
                        playerIntersection = new { targetsPlayerDirectly = false, stakes = "Баланс власти", canBecomeSoulQuest = true, recommendedCounterQuestTone = "political" },
                        milestones = new[]
                        {
                            new { stage = 0, title = "Первые слухи", summary = "О нем уже говорят.", visibleToPlayer = true }
                        },
                        currentStage = 0,
                        publicSignals = new[]
                        {
                            new { signalId = "signal_test_001", stage = 0, description = "По рынкам ходят слухи о новом чуде.", source = "rumor", visibleToPlayer = true }
                        },
                        resolution = new { outcome = "ongoing", notes = "Нить только разворачивается." }
                    }
                }
            });
        }
    }


    private Task SeedMortalStateAsync() => WriteJsonAsync("game_state/meta/soul_state.json", new
    {
        soulName = "Тестовая Душа",
        currentRealm = "Mortal Realm",
        currentIncarnation = 1,
        inkFeathers = new { current = 3 }
    });


    private Task SeedAfterlifeStateAsync() => WriteJsonAsync("game_state/meta/soul_state.json", new
    {
        soulName = "Тестовая Душа",
        currentRealm = "Chaos Sea",
        currentIncarnation = 1,
        inkFeathers = new { current = 3 }
    });


    private async Task SeedNpcTradeStateAsync(
        bool includeSellableInventoryItem = false,
        bool canTrade = true,
        bool locationMatches = true,
        bool includeTradeInventory = true,
        bool includeTradeReceipt = true,
        bool includeBuybackInventory = false)
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("input/turn_request.json", new { turnNumber = 12 });
        var blockedReasonField = canTrade ? "" : ",\n              \"tradeBlockedReason\": \"Торговля сейчас недоступна.\"";
        var tradeInventoryField = includeTradeInventory
            ? """
            ,
              "tradeInventory": {
                "tradeCycleId": "world_trade_0",
                "generatedAtWorldDate": 100,
                "refreshAfterWorldDate": 43200,
                "generationTradeTier": "Good",
                "pricingTradeTier": "Neutral",
                "items": [
                  {
                    "slotId": "npc_trade_slot_001",
                    "itemId": "npc_item_merchant_001",
                    "price": 110,
                    "merchantProfile": "GeneralGoods",
                    "soldOut": false,
                    "itemData": {
                      "itemId": "npc_item_merchant_001",
                      "name": "Полевой набор торговца",
                      "description": "Тестовый authored ассортимент.",
                      "type": "Tool",
                      "tradeItemClass": "Functional",
                      "quality": "Rare",
                      "price": 90,
                      "baseSellPrice": 36,
                      "weight": "1.0",
                      "group": "Инструменты"
                    }
                  },
                  {
                    "slotId": "npc_trade_slot_002",
                    "itemId": "npc_item_merchant_002",
                    "price": 37,
                    "merchantProfile": "GeneralGoods",
                    "soldOut": false,
                    "itemData": {
                      "itemId": "npc_item_merchant_002",
                      "name": "Карта соседних кварталов",
                      "description": "Тестовый authored ассортимент.",
                      "type": "Document",
                      "tradeItemClass": "FlavorOrUtility",
                      "quality": "Common",
                      "price": 30,
                      "baseSellPrice": 12,
                      "weight": "0.1",
                      "group": "Документы и медиа"
                    }
                  },
                  {
                    "slotId": "npc_trade_slot_003",
                    "itemId": "npc_item_merchant_003",
                    "price": 25,
                    "merchantProfile": "GeneralGoods",
                    "soldOut": false,
                    "itemData": {
                      "itemId": "npc_item_merchant_003",
                      "name": "Запас крепежа",
                      "description": "Тестовый authored ассортимент.",
                      "type": "Material",
                      "tradeItemClass": "Material",
                      "quality": "Common",
                      "price": 20,
                      "baseSellPrice": 8,
                      "weight": "0.4",
                      "group": "Материалы"
                    }
                  },
                  {
                    "slotId": "npc_trade_slot_004",
                    "itemId": "npc_item_merchant_004",
                    "price": 49,
                    "merchantProfile": "GeneralGoods",
                    "soldOut": false,
                    "itemData": {
                      "itemId": "npc_item_merchant_004",
                      "name": "Дорожный фонарь",
                      "description": "Тестовый authored ассортимент.",
                      "type": "Tool",
                      "tradeItemClass": "Functional",
                      "quality": "Uncommon",
                      "price": 40,
                      "baseSellPrice": 16,
                      "weight": "0.8",
                      "group": "Инструменты"
                    }
                  },
                  {
                    "slotId": "npc_trade_slot_005",
                    "itemId": "npc_item_merchant_005",
                    "price": 74,
                    "merchantProfile": "GeneralGoods",
                    "soldOut": false,
                    "itemData": {
                      "itemId": "npc_item_merchant_005",
                      "name": "Плотный плащ",
                      "description": "Тестовый authored ассортимент.",
                      "type": "Armor",
                      "tradeItemClass": "Functional",
                      "quality": "Uncommon",
                      "price": 60,
                      "baseSellPrice": 24,
                      "weight": "1.5",
                      "group": "Защита"
                    }
                  },
                  {
                    "slotId": "npc_trade_slot_006",
                    "itemId": "npc_item_merchant_006",
                    "price": 61,
                    "merchantProfile": "GeneralGoods",
                    "soldOut": false,
                    "itemData": {
                      "itemId": "npc_item_merchant_006",
                      "name": "Складной кофр",
                      "description": "Тестовый authored ассортимент.",
                      "type": "Container",
                      "tradeItemClass": "Functional",
                      "quality": "Uncommon",
                      "price": 50,
                      "baseSellPrice": 20,
                      "weight": "1.2",
                      "group": "Контейнеры"
                    }
                  },
                  {
                    "slotId": "npc_trade_slot_007",
                    "itemId": "npc_item_merchant_007",
                    "price": 31,
                    "merchantProfile": "GeneralGoods",
                    "soldOut": false,
                    "itemData": {
                      "itemId": "npc_item_merchant_007",
                      "name": "Записная книжка",
                      "description": "Тестовый authored ассортимент.",
                      "type": "Document",
                      "tradeItemClass": "FlavorOrUtility",
                      "quality": "Common",
                      "price": 25,
                      "baseSellPrice": 10,
                      "weight": "0.1",
                      "group": "Документы и медиа"
                    }
                  }
                ]
              }
            """
            : "";
        var tradeReceiptField = includeTradeInventory && includeTradeReceipt
            ? """
            ,
              "tradeInventoryReceipts": [
                {
                  "requestId": "npc_trade_req_seed_001",
                  "npcId": "npc_merchant_001",
                  "npcName": "Марек",
                  "tradeCycleId": "world_trade_0",
                  "merchantProfile": "GeneralGoods",
                  "status": "ready",
                  "itemCount": 7,
                  "resolvedAtTurn": 5,
                  "resolvedAtUtc": "2026-03-28T00:05:00Z"
                }
              ]
            """
            : "";
        var buybackInventoryField = includeBuybackInventory
            ? """
            ,
              "buybackInventory": [
                {
                  "buybackEntryId": "npc_buyback_001",
                  "npcId": "npc_merchant_001",
                  "npcName": "Марек",
                  "itemId": "item_sell_lantern_001",
                  "itemData": {
                    "itemId": "item_sell_lantern_001",
                    "name": "Походный фонарь",
                    "description": "Ранее проданный фонарь.",
                    "type": "tool",
                    "tradeItemClass": "Functional",
                    "quality": "Common",
                    "price": 20,
                    "baseSellPrice": 8
                  },
                  "soldByPlayerAtTurn": 6,
                  "soldByPlayerAtUtc": "2026-03-28T00:04:00Z",
                  "soldAtWorldDate": 95,
                  "soldForPrice": 8,
                  "buybackPrice": 8,
                  "acquiredFromPlayer": true,
                  "sourceMerchantProfile": "GeneralGoods",
                  "status": "available"
                }
              ]
            """
            : "";
        await WriteRawJsonAsync("game_state/npcs/npc_core.json", $$"""
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_merchant_001",
              "name": "Марек",
              "currentLocationId": "loc_market_square",
              "currentLocation": "Рыночная площадь",
              "level": 10,
              "relationshipLevel": 80,
              "characteristics": { "modifiedTrade": 14 },
              "tradeState": {
                "canTrade": {{canTrade.ToString().ToLowerInvariant()}},
                "merchantProfile": "GeneralGoods"{{blockedReasonField}}
              }{{tradeInventoryField}}{{tradeReceiptField}}{{buybackInventoryField}}
            }
          ]
        }
        """);
        await WriteJsonAsync("game_state/world/current_location.json", new
        {
            locationId = locationMatches ? "loc_market_square" : "loc_other_square",
            name = locationMatches ? "Рыночная площадь" : "Другая площадь"
        });
        await WriteJsonAsync("game_state/core/player_status.json", new
        {
            money = 500,
            trade = 12
        });
        await WriteJsonAsync("game_state/world/world_time.json", new
        {
            currentTimeInMinutes = 100
        });
        await WriteJsonAsync("game_state/inventory/items.json", includeSellableInventoryItem
            ? new
            {
                items = new[]
                {
                    new
                    {
                        itemId = "item_sell_lantern_001",
                        name = "Походный фонарь",
                        quality = "Common",
                        type = "tool"
                    }
                },
                equipment = new { }
            }
            : new
            {
                items = Array.Empty<object>(),
                equipment = new { }
            });
    }


    private async Task SeedGuardianTradeStateAsync(
        bool includeStoredRelicForSale = false,
        bool includeTradeInventory = true,
        bool includeBuybackRelics = false)
    {
        await WriteJsonAsync("input/turn_request.json", new { turnNumber = 12 });
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            inkFeathers = new { current = 500 },
            soulRelics = new
            {
                equipped = Array.Empty<object>(),
                stored = includeStoredRelicForSale
                    ? new object[]
                    {
                        new
                        {
                            relicId = "relic_sell_001",
                            name = "Реликвия для продажи",
                            rarity = "Rare",
                            description = "Подходит для теста продажи."
                        }
                    }
                    : Array.Empty<object>()
            }
        });
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guardian_trade_001",
                    canonicalName = "Азалия",
                    domain = "Двор Зеркал",
                    nameVariants = new
                    {
                        @default = "Азалия",
                        feminine = "Азалия",
                        masculine = (string?)null,
                        neutral = (string?)null
                    },
                    manifestation = new
                    {
                        currentDisplayName = "Азалия",
                        formFlexibility = "selective",
                        currentPresentationStyle = "feminine",
                        currentPronouns = "она/её",
                        appearanceDescription = "Тестовая текущая форма Азалии."
                    },
                    manifestationHistory = Array.Empty<object>(),
                    relationshipData = new
                    {
                        currentReputation = 120
                    },
                    abodePower = new
                    {
                        currentPower = 10,
                        tier = "Хрупкая",
                        lastUpdatedAt = "2026-03-26T00:00:00Z",
                        history = Array.Empty<object>()
                    },
                    abode = new
                    {
                        abodeId = "abode_social_001",
                        name = "Шелковая Обитель"
                    },
                    gachaSystem = new
                    {
                        chargesPerReturn = 1,
                        chargesUsedThisReturn = 0,
                        gachaHistory = Array.Empty<object>()
                    },
                    tradeInventory = includeTradeInventory
                        ? new
                        {
                            tradeCycleId = "return_1",
                            generatedAtUtc = "2026-03-26T00:00:00Z",
                            generationReputationTier = "Friendly",
                            pricingReputationTier = "Friendly",
                            projectBonusSignature = "0|0|0",
                            upgradedTradeSlots = 0,
                            elevatedTradeSlots = 0,
                            effectiveRarityCeilingBonusSteps = 0,
                            items = new object[]
                            {
                                new
                                {
                                    slotId = "trade_social_1",
                                    priceInFeathers = 30,
                                    domainTag = "Двор Зеркал",
                                    soldOut = false,
                                    rarityBonusStepsApplied = 0,
                                    relicData = new
                                    {
                                        relicId = "trade_relic_1",
                                        name = "Печать Зеркального Двора",
                                        rarity = "Common",
                                        quality = "Common",
                                        description = "Тестовая реликвия витрины."
                                    }
                                },
                                new
                                {
                                    slotId = "trade_social_2",
                                    priceInFeathers = 70,
                                    domainTag = "Двор Зеркал",
                                    soldOut = false,
                                    rarityBonusStepsApplied = 0,
                                    relicData = new
                                    {
                                        relicId = "trade_relic_2",
                                        name = "Колье Серебряной Вежливости",
                                        rarity = "Uncommon",
                                        quality = "Uncommon",
                                        description = "Тестовая реликвия витрины."
                                    }
                                },
                                new
                                {
                                    slotId = "trade_social_3",
                                    priceInFeathers = 140,
                                    domainTag = "Двор Зеркал",
                                    soldOut = false,
                                    rarityBonusStepsApplied = 0,
                                    relicData = new
                                    {
                                        relicId = "trade_relic_3",
                                        name = "Знак Тихой Интриги",
                                        rarity = "Rare",
                                        quality = "Rare",
                                        description = "Тестовая реликвия витрины."
                                    }
                                },
                                new
                                {
                                    slotId = "trade_social_4",
                                    priceInFeathers = 140,
                                    domainTag = "Двор Зеркал",
                                    soldOut = false,
                                    rarityBonusStepsApplied = 0,
                                    relicData = new
                                    {
                                        relicId = "trade_relic_4",
                                        name = "Плащ Дворцового Отзвука",
                                        rarity = "Rare",
                                        quality = "Rare",
                                        description = "Тестовая реликвия витрины."
                                    }
                                }
                            }
                        }
                        : null,
                    tradeInventoryReceipts = includeTradeInventory
                        ? new object[]
                        {
                            new
                            {
                                requestId = "guardian_trade_req_seeded",
                                guardianId = "guardian_trade_001",
                                guardianName = "Азалия",
                                abodeId = "abode_social_001",
                                tradeCycleId = "return_1",
                                status = "ready",
                                itemCount = 4,
                                resolvedAtTurn = 12,
                                resolvedAtUtc = "2026-03-26T00:05:00Z"
                            }
                        }
                        : Array.Empty<object>(),
                    buybackRelics = includeBuybackRelics
                        ? new object[]
                        {
                            new
                            {
                                buybackEntryId = "guardian_buyback_001",
                                guardianId = "guardian_trade_001",
                                guardianName = "Азалия",
                                relicId = "relic_buyback_001",
                                relicData = new
                                {
                                    relicId = "relic_buyback_001",
                                    name = "Отзвук Зеркального Двора",
                                    rarity = "Rare",
                                    description = "Ранее проданная Хранителю реликвия."
                                },
                                soldByPlayerAtTurn = 9,
                                soldByPlayerAtUtc = "2026-03-26T00:10:00Z",
                                soldForPrice = 60,
                                buybackPrice = 60,
                                acquiredFromPlayer = true,
                                status = "available"
                            }
                        }
                        : Array.Empty<object>()
                }
            },
            activeGuardian = new
            {
                guardianId = "guardian_trade_001",
                canonicalName = "Азалия",
                domain = "Двор Зеркал",
                nameVariants = new
                {
                    @default = "Азалия",
                    feminine = "Азалия",
                    masculine = (string?)null,
                    neutral = (string?)null
                },
                manifestation = new
                {
                    currentDisplayName = "Азалия",
                    formFlexibility = "selective",
                    currentPresentationStyle = "feminine",
                    currentPronouns = "она/её",
                    appearanceDescription = "Тестовая текущая форма Азалии."
                },
                manifestationHistory = Array.Empty<object>(),
                relationshipData = new { currentReputation = 120 },
                abodePower = new
                {
                    currentPower = 10,
                    tier = "Хрупкая",
                    lastUpdatedAt = "2026-03-26T00:00:00Z",
                    history = Array.Empty<object>()
                },
                abode = new
                {
                    abodeId = "abode_social_001",
                    name = "Шелковая Обитель"
                },
                gachaSystem = new
                {
                    chargesPerReturn = 1,
                    chargesUsedThisReturn = 0,
                    gachaHistory = Array.Empty<object>()
                },
                tradeInventory = new
                {
                    tradeCycleId = "return_1",
                    generatedAtUtc = "2026-03-26T00:00:00Z",
                    generationReputationTier = "Friendly",
                    pricingReputationTier = "Friendly",
                    projectBonusSignature = "0|0|0",
                    upgradedTradeSlots = 0,
                    elevatedTradeSlots = 0,
                    effectiveRarityCeilingBonusSteps = 0,
                    items = new object[]
                    {
                        new
                        {
                            slotId = "trade_social_1",
                            priceInFeathers = 30,
                            domainTag = "Двор Зеркал",
                            soldOut = false,
                            rarityBonusStepsApplied = 0,
                            relicData = new
                            {
                                relicId = "trade_relic_1",
                                name = "Печать Зеркального Двора",
                                rarity = "Common",
                                quality = "Common",
                                description = "Тестовая реликвия витрины."
                            }
                        },
                        new
                        {
                            slotId = "trade_social_2",
                            priceInFeathers = 70,
                            domainTag = "Двор Зеркал",
                            soldOut = false,
                            rarityBonusStepsApplied = 0,
                            relicData = new
                            {
                                relicId = "trade_relic_2",
                                name = "Колье Серебряной Вежливости",
                                rarity = "Uncommon",
                                quality = "Uncommon",
                                description = "Тестовая реликвия витрины."
                            }
                        },
                        new
                        {
                            slotId = "trade_social_3",
                            priceInFeathers = 140,
                            domainTag = "Двор Зеркал",
                            soldOut = false,
                            rarityBonusStepsApplied = 0,
                            relicData = new
                            {
                                relicId = "trade_relic_3",
                                name = "Знак Тихой Интриги",
                                rarity = "Rare",
                                quality = "Rare",
                                description = "Тестовая реликвия витрины."
                            }
                        },
                        new
                        {
                            slotId = "trade_social_4",
                            priceInFeathers = 140,
                            domainTag = "Двор Зеркал",
                            soldOut = false,
                            rarityBonusStepsApplied = 0,
                            relicData = new
                            {
                                relicId = "trade_relic_4",
                                name = "Плащ Дворцового Отзвука",
                                rarity = "Rare",
                                quality = "Rare",
                                description = "Тестовая реликвия витрины."
                            }
                        }
                    }
                },
                tradeInventoryReceipts = includeTradeInventory
                    ? new object[]
                    {
                        new
                        {
                            requestId = "guardian_trade_req_seeded",
                            guardianId = "guardian_trade_001",
                            guardianName = "Азалия",
                            abodeId = "abode_social_001",
                            tradeCycleId = "return_1",
                            status = "ready",
                            itemCount = 4,
                            resolvedAtTurn = 12,
                            resolvedAtUtc = "2026-03-26T00:05:00Z"
                        }
                    }
                    : Array.Empty<object>(),
                buybackRelics = includeBuybackRelics
                    ? new object[]
                    {
                        new
                        {
                            buybackEntryId = "guardian_buyback_001",
                            guardianId = "guardian_trade_001",
                            guardianName = "Азалия",
                            relicId = "relic_buyback_001",
                            relicData = new
                            {
                                relicId = "relic_buyback_001",
                                name = "Отзвук Зеркального Двора",
                                rarity = "Rare",
                                description = "Ранее проданная Хранителю реликвия."
                            },
                            soldByPlayerAtTurn = 9,
                            soldByPlayerAtUtc = "2026-03-26T00:10:00Z",
                            soldForPrice = 60,
                            buybackPrice = 60,
                            acquiredFromPlayer = true,
                            status = "available"
                        }
                    }
                    : Array.Empty<object>()
            },
            chaosSeaNavigation = new
            {
                currentAbodeId = "abode_social_001"
            }
        });
    }


    private async Task SeedSystemGuardianPresetAsync(string presetId, string displayName, string domain, string abodeName)
    {
        var presetDir = Path.Combine(_systemGuardianLibraryService.GetBuiltInDirectoryPath(), presetId);
        Directory.CreateDirectory(presetDir);

        await File.WriteAllTextAsync(Path.Combine(presetDir, "manifest.json"), $$"""
        {
          "presetId": "{{presetId}}",
          "displayName": "{{displayName}}",
          "summary": "Тестовый системный хранитель для regression tests.",
          "alwaysAvailable": true,
          "category": "system_guardian",
          "identity": {
            "domain": "{{domain}}",
            "archetype": "Test Archetype",
            "tone": "Measured",
            "coreValues": ["ценность 1", "ценность 2", "ценность 3"]
          },
          "nameVariants": {
            "default": "{{displayName}}",
            "feminine": "{{displayName}}",
            "masculine": null,
            "neutral": null
          },
          "manifestationDefaults": {
            "formFlexibility": "selective",
            "defaultPresentationStyle": "feminine",
            "defaultPronouns": "она/её",
            "appearanceDescription": "Тестовая текущая форма проявления."
          },
          "abode": {
            "name": "{{abodeName}}",
            "theme": "тестовая обитель"
          },
          "generationRules": {
            "mustPreserve": ["Имя {{displayName}}"],
            "canVary": ["мелкие детали"],
            "forbidden": ["ломать тест"]
          },
          "searchAttraction": {
            "enabled": true,
            "label": "Притяжение к {{displayName}}",
            "keywords": ["тест"]
          },
          "authoring": {
            "author": "tests",
            "version": "1.0"
          }
        }
        """);

        await File.WriteAllTextAsync(Path.Combine(presetDir, "dossier.md"), $"# {displayName}\n\nТестовое досье для системного хранителя.");
    }


    private async Task WriteJsonAsync(string relativePath, object payload)
    {
        await _fs.WriteFileAtomicAsync(relativePath, JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));
    }


    private async Task WriteStoryAsync(string relativePath, object payload)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        await _fs.WriteFileAtomicAsync(relativePath, json + Environment.NewLine);
    }


    private Task WriteRawJsonAsync(string relativePath, string json)
    {
        return _fs.WriteFileAtomicAsync(relativePath, json.Replace("\n", Environment.NewLine));
    }


    private string BuildConsoleDiagnostics(string scenario)
    {
        var choices = string.Join(" || ", _console.SelectionChoicesHistory.SelectMany(entry => entry.Choices));
        return $"{scenario} | titles: {string.Join(" || ", _console.SelectionTitles)} | choices: {choices} | ask: {string.Join(" || ", _console.AskPrompts)} | confirm: {string.Join(" || ", _console.ConfirmPrompts)} | markup: {string.Join(" || ", _console.MarkupLines)}";
    }


    private string ExtractRenderedText()
    {
        return string.Join("\n", _console.Rendered.Select(ExtractRenderableText));
    }


    private static string ExtractRenderableText(IRenderable renderable)
    {
        return renderable switch
        {
            Panel panel => ExtractPanelText(panel),
            Tree tree => ExtractTreeText(tree),
            Grid grid => ExtractGridText(grid),
            Table table => ExtractTableText(table),
            Markup markup => ExtractMarkupText(markup),
            _ => renderable.ToString() ?? string.Empty
        };
    }


    private static string ExtractPanelText(Panel panel)
    {
        var parts = new List<string>();
        if (panel.Header is { } header && !string.IsNullOrWhiteSpace(header.Text))
            parts.Add(header.Text);

        var childField = typeof(Panel).GetField("_child", BindingFlags.Instance | BindingFlags.NonPublic);
        if (childField?.GetValue(panel) is IRenderable child)
        {
            var childText = ExtractRenderableText(child);
            if (!string.IsNullOrWhiteSpace(childText))
                parts.Add(childText);
        }

        return string.Join("\n", parts);
    }

    private static string ExtractTreeText(Tree tree)
    {
        var rootField = typeof(Tree).GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic);
        if (rootField?.GetValue(tree) is not TreeNode root)
            return string.Empty;

        var parts = new List<string>();
        AppendTreeNodeText(parts, root);
        return string.Join("\n", parts);
    }

    private static void AppendTreeNodeText(List<string> parts, TreeNode node)
    {
        var renderableProperty = node.GetType().GetProperty("Renderable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (renderableProperty?.GetValue(node) is IRenderable renderable)
        {
            var nodeText = ExtractRenderableText(renderable);
            if (!string.IsNullOrWhiteSpace(nodeText))
                parts.Add(nodeText);
        }

        var nodesProperty = node.GetType().GetProperty("Nodes", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (nodesProperty?.GetValue(node) is IEnumerable children)
        {
            foreach (var child in children.OfType<TreeNode>())
                AppendTreeNodeText(parts, child);
        }
    }


    private static string ExtractMarkupText(Markup markup)
    {
        var paragraphField = typeof(Markup).GetField("_paragraph", BindingFlags.Instance | BindingFlags.NonPublic);
        var paragraph = paragraphField?.GetValue(markup);
        if (paragraph == null)
            return string.Empty;

        var linesField = paragraph.GetType().GetField("_lines", BindingFlags.Instance | BindingFlags.NonPublic);
        if (linesField?.GetValue(paragraph) is not IEnumerable<object> lines)
            return string.Empty;

        var lineTexts = new List<string>();
        foreach (var line in lines)
        {
            var itemsField = line.GetType().GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic);
            if (itemsField?.GetValue(line) is not Array items)
                continue;

            var text = string.Concat(items.Cast<object?>().Where(segment => segment != null).Select(segment =>
            {
                var textProperty = segment!.GetType().GetProperty("Text", BindingFlags.Instance | BindingFlags.Public);
                return textProperty?.GetValue(segment)?.ToString() ?? string.Empty;
            }));
            lineTexts.Add(text);
        }

        return string.Join("\n", lineTexts);
    }

    private static string ExtractGridText(Grid grid)
    {
        var rowTexts = new List<string>();
        foreach (var row in grid.Rows)
        {
            var itemsField = row.GetType().GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic);
            if (itemsField?.GetValue(row) is not IEnumerable<IRenderable> items)
                continue;

            foreach (var item in items)
            {
                var text = ExtractRenderableText(item);
                if (!string.IsNullOrWhiteSpace(text))
                    rowTexts.Add(text);
            }
        }

        return string.Join("\n", rowTexts);
    }

    private static string ExtractTableText(Table table)
    {
        var rowTexts = new List<string>();

        if (table.ShowHeaders)
        {
            var headerTexts = table.Columns
                .Select(column => ExtractRenderableText(column.Header))
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();
            if (headerTexts.Count > 0)
                rowTexts.Add(string.Join(" | ", headerTexts));
        }

        foreach (var row in table.Rows)
        {
            var cells = new List<string>();
            for (var index = 0; index < row.Count; index++)
            {
                var text = ExtractRenderableText(row[index]);
                if (!string.IsNullOrWhiteSpace(text))
                    cells.Add(text);
            }

            if (cells.Count > 0)
                rowTexts.Add(string.Join(" | ", cells));
        }

        return string.Join("\n", rowTexts);
    }


    private void AssertNoHiddenExplorerErrors(string scenario)
    {
        Assert.False(
            _console.MarkupLines.Any(line => line.Contains("Ошибка при выполнении команды", StringComparison.OrdinalIgnoreCase)),
            BuildConsoleDiagnostics(scenario));
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
            // ignored
        }
    }

}
