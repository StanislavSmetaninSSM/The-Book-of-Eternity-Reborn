using System.Text.Json;
using System.Linq;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeNotificationStateTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public AfterlifeNotificationStateTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-afterlife-notifications-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_TradeInventoryReady_CreatesSingleUnreadNotification()
    {
        await WriteJsonAsync(GuardianTradeRequestState.PendingRequestPath, new
        {
            requestId = "guardian_trade_req_1",
            guardianId = "guardian_alpha",
            guardianName = "Азалия",
            abodeId = "abode_alpha",
            returnCycleId = "return_1",
            currentReputation = 120,
            derivedTradeSlotCount = 4,
            effectiveRarityCeilingBonusSteps = 0,
            projectBonusSignature = "0|0|0",
            createdAtUtc = "2026-03-26T00:00:00Z",
            createdAtTurn = 11
        });
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guardian_alpha",
                    canonicalName = "Азалия",
                    tradeInventory = new
                    {
                        tradeCycleId = "return_1",
                        generatedAtUtc = "2026-03-26T00:10:00Z",
                        generationReputationTier = "Friendly",
                        pricingReputationTier = "Friendly",
                        projectBonusSignature = "0|0|0",
                        effectiveRarityCeilingBonusSteps = 0,
                        items = new object[]
                        {
                            new { slotId = "slot_1", priceInFeathers = 30, soldOut = false, rarityBonusStepsApplied = 0, relicData = new { relicId = "relic_1", name = "Реликвия 1", rarity = "Common", quality = "Common" } },
                            new { slotId = "slot_2", priceInFeathers = 70, soldOut = false, rarityBonusStepsApplied = 0, relicData = new { relicId = "relic_2", name = "Реликвия 2", rarity = "Uncommon", quality = "Uncommon" } },
                            new { slotId = "slot_3", priceInFeathers = 140, soldOut = false, rarityBonusStepsApplied = 0, relicData = new { relicId = "relic_3", name = "Реликвия 3", rarity = "Rare", quality = "Rare" } },
                            new { slotId = "slot_4", priceInFeathers = 140, soldOut = false, rarityBonusStepsApplied = 0, relicData = new { relicId = "relic_4", name = "Реликвия 4", rarity = "Rare", quality = "Rare" } }
                        }
                    },
                    tradeInventoryReceipts = new object[]
                    {
                        new
                        {
                            requestId = "guardian_trade_req_1",
                            guardianId = "guardian_alpha",
                            guardianName = "Азалия",
                            abodeId = "abode_alpha",
                            tradeCycleId = "return_1",
                            status = "ready",
                            itemCount = 4,
                            resolvedAtTurn = 12,
                            resolvedAtUtc = "2026-03-26T00:10:00Z"
                        }
                    }
                }
            }
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);
        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var notification = Assert.Single(notifications);
        Assert.Equal(AfterlifeNotificationState.TypeGuardianTradeInventoryReady, notification.NotificationType);
        Assert.Equal(AfterlifeNotificationState.StatusUnread, notification.Status);
        Assert.Equal("guardian_trade_req_1", notification.RequestId);
        Assert.Equal(12, notification.CreatedAtTurn);
        Assert.Equal("2026-03-26T00:10:00Z", notification.CreatedAtUtc);
        Assert.Contains("4 позиций", notification.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_TradeInventoryReady_RebuildsFromCanonicalReceiptWithoutPendingRequest()
    {
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guardian_alpha",
                    canonicalName = "Азалия",
                    tradeInventory = new
                    {
                        tradeCycleId = "return_2",
                        generatedAtUtc = "2026-03-27T00:10:00Z",
                        generationReputationTier = "Friendly",
                        pricingReputationTier = "Friendly",
                        projectBonusSignature = "0|0|0",
                        effectiveRarityCeilingBonusSteps = 0,
                        items = new object[]
                        {
                            new { slotId = "slot_1", priceInFeathers = 30, soldOut = false, rarityBonusStepsApplied = 0, relicData = new { relicId = "relic_1", name = "Реликвия 1", rarity = "Common", quality = "Common" } },
                            new { slotId = "slot_2", priceInFeathers = 70, soldOut = false, rarityBonusStepsApplied = 0, relicData = new { relicId = "relic_2", name = "Реликвия 2", rarity = "Uncommon", quality = "Uncommon" } }
                        }
                    },
                    tradeInventoryReceipts = new object[]
                    {
                        new
                        {
                            requestId = "guardian_trade_req_rebuild",
                            guardianId = "guardian_alpha",
                            guardianName = "Азалия",
                            abodeId = "abode_alpha",
                            tradeCycleId = "return_2",
                            status = "ready",
                            itemCount = 2,
                            resolvedAtTurn = 18,
                            resolvedAtUtc = "2026-03-27T00:12:00Z"
                        }
                    }
                }
            }
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var notification = Assert.Single(notifications);
        Assert.Equal("guardian_trade_req_rebuild", notification.RequestId);
        Assert.Equal(18, notification.CreatedAtTurn);
        Assert.Equal("2026-03-27T00:12:00Z", notification.CreatedAtUtc);
        Assert.Contains("2 позиций", notification.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ShiningTradeInventoryReady_CreatesNotification()
    {
        await WriteJsonAsync("game_state/meta/shining_abode_state.json", new
        {
            availability = "active",
            radiance = new { experience = 250, tier = 2 },
            lightSparks = 80,
            halls = Array.Empty<object>(),
            shiningPoliticalActors = Array.Empty<object>(),
            pendingNativeFactionDiscovery = (object?)null,
            gates = new
            {
                draftVersion = 0,
                hasOpenDraft = false,
                isStale = false,
                allCandidateBlessingCards = Array.Empty<object>(),
                availableBlessingCards = Array.Empty<object>(),
                shownBlessingCardIds = Array.Empty<string>(),
                selectedBlessingCardIds = Array.Empty<string>(),
                nextCandidateCursor = 0,
                rerollsRemaining = 0
            },
            preparedIncarnationPackage = (object?)null,
            coreActionReceipts = Array.Empty<object>(),
            factionFoundingReceipts = Array.Empty<object>(),
            factionRealignmentReceipts = Array.Empty<object>(),
            factions = new object[]
            {
                new
                {
                    factionId = "faction_old",
                    originType = "ascended_guardian",
                    hallId = "hall_old",
                    charter = new
                    {
                        factionName = "Старый Дом",
                        favoredArchetype = "provision",
                        patronEffectFamily = "resource",
                        summary = "Торговая фракция."
                    },
                    leadership = new
                    {
                        headActorType = "guardian",
                        headActorId = "guardian_old",
                        leadershipState = "secure"
                    },
                    baseStrength = 35,
                    factionStrength = 62,
                    investCountThisAscension = 0,
                    projectArchetypesCountedThisAscension = Array.Empty<string>(),
                    projects = Array.Empty<object>(),
                    tradeInventory = new
                    {
                        tradeCycleId = "shining_return_2",
                        generatedAtUtc = "2026-04-17T01:00:00Z",
                        generationTradeTier = 2,
                        generationRarityCeiling = "rare",
                        serviceMultiplierSnapshot = 1.25,
                        merchantProfile = "shining_faction",
                        items = new object[]
                        {
                            new
                            {
                                slotId = "slot_1",
                                priceInFeathers = 70,
                                soldOut = false,
                                relicData = new { relicId = "relic_1", name = "Реликвия 1", rarity = "Rare", quality = "Rare" }
                            },
                            new
                            {
                                slotId = "slot_2",
                                priceInFeathers = 30,
                                soldOut = false,
                                relicData = new { relicId = "relic_2", name = "Реликвия 2", rarity = "Common", quality = "Common" }
                            },
                            new
                            {
                                slotId = "slot_3",
                                priceInFeathers = 30,
                                soldOut = false,
                                relicData = new { relicId = "relic_3", name = "Реликвия 3", rarity = "Common", quality = "Common" }
                            },
                            new
                            {
                                slotId = "slot_4",
                                priceInFeathers = 30,
                                soldOut = false,
                                relicData = new { relicId = "relic_4", name = "Реликвия 4", rarity = "Common", quality = "Common" }
                            },
                            new
                            {
                                slotId = "slot_5",
                                priceInFeathers = 30,
                                soldOut = false,
                                relicData = new { relicId = "relic_5", name = "Реликвия 5", rarity = "Common", quality = "Common" }
                            },
                            new
                            {
                                slotId = "slot_6",
                                priceInFeathers = 30,
                                soldOut = false,
                                relicData = new { relicId = "relic_6", name = "Реликвия 6", rarity = "Common", quality = "Common" }
                            }
                        }
                    },
                    tradeInventoryReceipts = new object[]
                    {
                        new
                        {
                            requestId = "shining_trade_req_1",
                            factionId = "faction_old",
                            factionName = "Старый Дом",
                            tradeCycleId = "shining_return_2",
                            status = "ready",
                            itemCount = 6,
                            resolvedAtTurn = 14,
                            resolvedAtUtc = "2026-04-17T01:00:00Z"
                        }
                    },
                    leadershipReceipts = Array.Empty<object>(),
                    leadershipHistory = Array.Empty<object>()
                }
            }
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var notification = Assert.Single(notifications.Where(item => string.Equals(item.NotificationType, AfterlifeNotificationState.TypeShiningTradeInventoryReady, StringComparison.OrdinalIgnoreCase)));
        Assert.Contains("Старый Дом", notification.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("слотов 6", notification.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_PlayerGuardianFoundationResolved_CreatesNotification()
    {
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new object[]
            {
                new
                {
                    guardianId = "guardian_old",
                    canonicalName = "Азалия"
                },
                new
                {
                    guardianId = "guardian_player",
                    canonicalName = "Трон Прилива",
                    originType = PlayerGuardianFoundationState.OriginTypePlayerFoundedAscendedSoul,
                    foundationRequestId = "foundation_req_1"
                }
            },
            activeGuardian = new
            {
                guardianId = "guardian_player",
                canonicalName = "Трон Прилива"
            },
            chaosSeaNavigation = new
            {
                currentAbodeId = "abode_player"
            },
            playerGuardianFoundationHistory = new object[]
            {
                new
                {
                    requestId = "foundation_req_1",
                    guardianId = "guardian_player",
                    guardianDisplayName = "Трон Прилива",
                    founderSoulName = "Тестовая Душа",
                    formerPatronGuardianId = "guardian_old",
                    formerPatronGuardianName = "Азалия",
                    foundationSource = PlayerGuardianFoundationState.FoundationSourceShiningReturn,
                    resolvedAtTurn = 25,
                    resolvedAtUtc = "2026-04-18T00:15:00Z"
                }
            }
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var notification = Assert.Single(notifications.Where(item =>
            string.Equals(item.NotificationType, AfterlifeNotificationState.TypePlayerGuardianFoundationResolved, StringComparison.OrdinalIgnoreCase)));
        Assert.Equal("foundation_req_1", notification.RequestId);
        Assert.Equal("guardian_player", notification.GuardianId);
        Assert.Contains("Трон Прилива", notification.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Азалия", notification.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("afterlife", notification.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ShiningCoreReceipt_CreatesResolvedNotification()
    {
        await WriteJsonAsync("game_state/meta/shining_abode_state.json", new
        {
            availability = "active",
            radiance = new { experience = 250, tier = 2 },
            lightSparks = 80,
            halls = Array.Empty<object>(),
            factions = Array.Empty<object>(),
            shiningPoliticalActors = Array.Empty<object>(),
            pendingNativeFactionDiscovery = (object?)null,
            gates = new
            {
                draftVersion = 0,
                hasOpenDraft = false,
                isStale = false,
                allCandidateBlessingCards = Array.Empty<object>(),
                availableBlessingCards = Array.Empty<object>(),
                shownBlessingCardIds = Array.Empty<string>(),
                selectedBlessingCardIds = Array.Empty<string>(),
                nextCandidateCursor = 0,
                rerollsRemaining = 0
            },
            preparedIncarnationPackage = (object?)null,
            coreActionReceipts = new object[]
            {
                new
                {
                    requestId = "core_req_open_gates",
                    actionType = "open_gates",
                    status = "accepted",
                    factionId = "",
                    projectId = "",
                    hallId = "",
                    resolvedFactionId = "",
                    selectedCardIds = Array.Empty<string>(),
                    newResidentIds = Array.Empty<string>(),
                    seededProjectIds = Array.Empty<string>(),
                    generatedDraftVersion = 2,
                    resolvedAtTurn = 15,
                    resolvedAtUtc = "2026-04-17T02:00:00Z"
                }
            },
            factionFoundingReceipts = Array.Empty<object>(),
            factionRealignmentReceipts = Array.Empty<object>()
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var notification = Assert.Single(notifications.Where(item => string.Equals(item.NotificationType, AfterlifeNotificationState.TypeShiningCoreActionResolved, StringComparison.OrdinalIgnoreCase)));
        Assert.Contains("Открытие Врат", notification.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("версия 2", notification.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_MalformedGuardianTradePendingFile_CreatesAttentionNotification()
    {
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = Array.Empty<object>()
        });
        await _fs.WriteFileAtomicAsync(GuardianTradeRequestState.PendingRequestPath, "{");

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var notification = Assert.Single(notifications.Where(item => string.Equals(item.NotificationType, AfterlifeNotificationState.TypeGuardianTradePendingAttention, StringComparison.OrdinalIgnoreCase)));
        Assert.Contains("поврежден", notification.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pending_guardian_trade_request.json", notification.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("repair", notification.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cleanup", notification.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ShiningCoreReceipt_UsesResolvedProjectAndFactionNamesInSummary()
    {
        await WriteJsonAsync("game_state/meta/shining_abode_state.json", new
        {
            availability = "active",
            radiance = new { experience = 250, tier = 2 },
            lightSparks = 80,
            halls = Array.Empty<object>(),
            shiningPoliticalActors = Array.Empty<object>(),
            pendingNativeFactionDiscovery = (object?)null,
            gates = new
            {
                draftVersion = 0,
                hasOpenDraft = false,
                isStale = false,
                allCandidateBlessingCards = Array.Empty<object>(),
                availableBlessingCards = Array.Empty<object>(),
                shownBlessingCardIds = Array.Empty<string>(),
                selectedBlessingCardIds = Array.Empty<string>(),
                nextCandidateCursor = 0,
                rerollsRemaining = 0
            },
            preparedIncarnationPackage = (object?)null,
            coreActionReceipts = new object[]
            {
                new
                {
                    requestId = "core_req_project_named",
                    actionType = "complete_project",
                    status = "accepted",
                    factionId = "faction_old",
                    factionName = "Старый Дом",
                    projectId = "project_bridge",
                    projectName = "Мост Света",
                    hallId = "",
                    resolvedFactionId = "",
                    relicId = "",
                    relicName = "",
                    targetFormTag = "",
                    selectedCardIds = Array.Empty<string>(),
                    newResidentIds = Array.Empty<string>(),
                    seededProjectIds = Array.Empty<string>(),
                    generatedDraftVersion = 0,
                    resolvedAtTurn = 19,
                    resolvedAtUtc = "2026-04-17T03:00:00Z"
                }
            },
            factionFoundingReceipts = Array.Empty<object>(),
            factionRealignmentReceipts = Array.Empty<object>(),
            factions = new object[]
            {
                new
                {
                    factionId = "faction_old",
                    originType = "ascended_guardian",
                    hallId = "hall_old",
                    charter = new
                    {
                        factionName = "Старый Дом",
                        favoredArchetype = "provision",
                        patronEffectFamily = "resource",
                        summary = "Торговая фракция."
                    },
                    leadership = new
                    {
                        headActorType = "guardian",
                        headActorId = "guardian_old",
                        leadershipState = "secure"
                    },
                    baseStrength = 35,
                    factionStrength = 62,
                    investCountThisAscension = 0,
                    projectArchetypesCountedThisAscension = Array.Empty<string>(),
                    projects = new object[]
                    {
                        new
                        {
                            projectId = "project_bridge",
                            displayName = "Мост Света",
                            projectArchetype = "accord",
                            outputEffectFamily = "social",
                            tier = 2,
                            status = "completed",
                            supportedByPlayerSoul = true
                        }
                    },
                    tradeInventoryReceipts = Array.Empty<object>(),
                    leadershipReceipts = Array.Empty<object>(),
                    leadershipHistory = Array.Empty<object>()
                }
            }
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var notification = Assert.Single(notifications.Where(item => string.Equals(item.NotificationType, AfterlifeNotificationState.TypeShiningCoreActionResolved, StringComparison.OrdinalIgnoreCase)));
        Assert.Contains("Мост Света", notification.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Старый Дом", notification.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ShiningCoreReceipt_HumanizesRarityAndForgeTokensInSummary()
    {
        await WriteJsonAsync("game_state/meta/shining_abode_state.json", new
        {
            availability = "active",
            radiance = new { experience = 250, tier = 2 },
            lightSparks = 80,
            halls = Array.Empty<object>(),
            factions = Array.Empty<object>(),
            shiningPoliticalActors = Array.Empty<object>(),
            pendingNativeFactionDiscovery = (object?)null,
            gates = new
            {
                draftVersion = 0,
                hasOpenDraft = false,
                isStale = false,
                allCandidateBlessingCards = Array.Empty<object>(),
                availableBlessingCards = Array.Empty<object>(),
                shownBlessingCardIds = Array.Empty<string>(),
                selectedBlessingCardIds = Array.Empty<string>(),
                nextCandidateCursor = 0,
                rerollsRemaining = 0
            },
            preparedIncarnationPackage = (object?)null,
            coreActionReceipts = new object[]
            {
                new
                {
                    requestId = "core_req_gacha",
                    actionType = "pull_relic_gacha",
                    status = "accepted",
                    factionId = "faction_old",
                    factionName = "Старый Дом",
                    relicId = "relic_sun",
                    relicName = "Солнечный Венец",
                    baseRarity = "rare",
                    finalRarity = "epic",
                    selectedCardIds = Array.Empty<string>(),
                    newResidentIds = Array.Empty<string>(),
                    seededProjectIds = Array.Empty<string>(),
                    generatedDraftVersion = 0,
                    resolvedAtTurn = 21,
                    resolvedAtUtc = "2026-04-17T03:00:00Z"
                },
                new
                {
                    requestId = "core_req_forge_form",
                    actionType = "forge_relic.reshape",
                    status = "accepted",
                    relicId = "relic_sun",
                    relicName = "Солнечный Венец",
                    targetFormTag = "solar_crown",
                    selectedCardIds = Array.Empty<string>(),
                    newResidentIds = Array.Empty<string>(),
                    seededProjectIds = Array.Empty<string>(),
                    generatedDraftVersion = 0,
                    resolvedAtTurn = 22,
                    resolvedAtUtc = "2026-04-17T03:10:00Z"
                },
                new
                {
                    requestId = "core_req_forge_property",
                    actionType = "forge_relic.retune_property",
                    status = "accepted",
                    relicId = "relic_sun",
                    relicName = "Солнечный Венец",
                    propertyIndex = 0,
                    selectedCardIds = Array.Empty<string>(),
                    newResidentIds = Array.Empty<string>(),
                    seededProjectIds = Array.Empty<string>(),
                    generatedDraftVersion = 0,
                    resolvedAtTurn = 23,
                    resolvedAtUtc = "2026-04-17T03:11:00Z"
                }
            },
            factionFoundingReceipts = Array.Empty<object>(),
            factionRealignmentReceipts = Array.Empty<object>()
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var gacha = notifications.Single(item => string.Equals(item.RequestId, "core_req_gacha", StringComparison.OrdinalIgnoreCase));
        var forgeForm = notifications.Single(item => string.Equals(item.RequestId, "core_req_forge_form", StringComparison.OrdinalIgnoreCase));
        var forgeProperty = notifications.Single(item => string.Equals(item.RequestId, "core_req_forge_property", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("редкая -> эпическая", gacha.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rare", gacha.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("солнечный венец", forgeForm.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("solar_crown", forgeForm.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("[property 0]", forgeProperty.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[свойство 1]", forgeProperty.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ShiningCoreNotification_PreservesStoredHistoricalSummary()
    {
        await WriteJsonAsync("game_state/control/afterlife_notifications.json", new
        {
            notifications = new[]
            {
                new
                {
                    notificationId = "shining_core_action_resolved:core_req_project_named",
                    notificationType = "shining_core_action_resolved",
                    requestId = "core_req_project_named",
                    status = "unread",
                    guardianId = "",
                    guardianName = "",
                    archiveId = "",
                    archiveTitle = "",
                    targetProjectId = "project_bridge",
                    targetProjectName = "Мост Света",
                    summary = "Историческая сводка о проекте «Мост Света» фракции «Старый Дом».",
                    createdAtTurn = 19,
                    createdAtUtc = "2026-04-17T03:00:00Z"
                }
            }
        });
        await WriteJsonAsync("game_state/meta/shining_abode_state.json", new
        {
            availability = "active",
            radiance = new { experience = 250, tier = 2 },
            lightSparks = 80,
            halls = Array.Empty<object>(),
            shiningPoliticalActors = Array.Empty<object>(),
            pendingNativeFactionDiscovery = (object?)null,
            gates = new
            {
                draftVersion = 0,
                hasOpenDraft = false,
                isStale = false,
                allCandidateBlessingCards = Array.Empty<object>(),
                availableBlessingCards = Array.Empty<object>(),
                shownBlessingCardIds = Array.Empty<string>(),
                selectedBlessingCardIds = Array.Empty<string>(),
                nextCandidateCursor = 0,
                rerollsRemaining = 0
            },
            preparedIncarnationPackage = (object?)null,
            coreActionReceipts = new object[]
            {
                new
                {
                    requestId = "core_req_project_named",
                    actionType = "complete_project",
                    status = "accepted",
                    factionId = "faction_old",
                    factionName = "Новое текущее имя",
                    projectId = "project_bridge",
                    projectName = "Новое текущее имя проекта",
                    selectedCardIds = Array.Empty<string>(),
                    newResidentIds = Array.Empty<string>(),
                    seededProjectIds = Array.Empty<string>(),
                    generatedDraftVersion = 0,
                    resolvedAtTurn = 19,
                    resolvedAtUtc = "2026-04-17T03:00:00Z"
                }
            },
            factionFoundingReceipts = Array.Empty<object>(),
            factionRealignmentReceipts = Array.Empty<object>(),
            factions = new object[]
            {
                new
                {
                    factionId = "faction_old",
                    originType = "ascended_guardian",
                    hallId = "hall_old",
                    charter = new
                    {
                        factionName = "Новое текущее имя",
                        favoredArchetype = "provision",
                        patronEffectFamily = "resource",
                        summary = "Торговая фракция."
                    },
                    leadership = new
                    {
                        headActorType = "guardian",
                        headActorId = "guardian_old",
                        leadershipState = "secure"
                    },
                    baseStrength = 35,
                    factionStrength = 62,
                    investCountThisAscension = 0,
                    projectArchetypesCountedThisAscension = Array.Empty<string>(),
                    projects = new object[]
                    {
                        new
                        {
                            projectId = "project_bridge",
                            displayName = "Новое текущее имя проекта",
                            projectArchetype = "accord",
                            outputEffectFamily = "social",
                            tier = 2,
                            status = "completed",
                            supportedByPlayerSoul = true
                        }
                    },
                    tradeInventoryReceipts = Array.Empty<object>(),
                    leadershipReceipts = Array.Empty<object>(),
                    leadershipHistory = Array.Empty<object>()
                }
            }
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var notification = Assert.Single(notifications.Where(item => string.Equals(item.NotificationType, AfterlifeNotificationState.TypeShiningCoreActionResolved, StringComparison.OrdinalIgnoreCase)));
        Assert.Equal("Историческая сводка о проекте «Мост Света» фракции «Старый Дом».", notification.Summary);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ShiningPoliticalReceipts_UseDetailedResolvedSummaries()
    {
        await WriteJsonAsync("game_state/meta/shining_abode_state.json", new
        {
            availability = "active",
            radiance = new { experience = 250, tier = 2 },
            lightSparks = 80,
            halls = Array.Empty<object>(),
            shiningPoliticalActors = Array.Empty<object>(),
            pendingNativeFactionDiscovery = (object?)null,
            gates = new
            {
                draftVersion = 0,
                hasOpenDraft = false,
                isStale = false,
                allCandidateBlessingCards = Array.Empty<object>(),
                availableBlessingCards = Array.Empty<object>(),
                shownBlessingCardIds = Array.Empty<string>(),
                selectedBlessingCardIds = Array.Empty<string>(),
                nextCandidateCursor = 0,
                rerollsRemaining = 0
            },
            preparedIncarnationPackage = (object?)null,
            coreActionReceipts = Array.Empty<object>(),
            factionFoundingReceipts = new object[]
            {
                new
                {
                    requestId = "founding_req_1",
                    proposedFactionId = "faction_dawn",
                    proposedHallId = "hall_dawn",
                    hallName = "Зал Рассветного Хора",
                    factionId = "faction_dawn",
                    factionName = "Дом Рассвета",
                    hallId = "hall_dawn",
                    status = "accepted",
                    supportingResidentIds = new[] { "resident_liora", "resident_mael", "resident_serit" },
                    resolvedAtTurn = 20,
                    resolvedAtUtc = "2026-04-17T03:10:00Z"
                }
            },
            factionRealignmentReceipts = new object[]
            {
                new
                {
                    requestId = "realign_req_1",
                    residentId = "resident_liora",
                    residentName = "Лиора",
                    sourceFactionId = "faction_old",
                    sourceFactionName = "faction_old",
                    targetFactionId = "faction_dawn",
                    targetFactionName = "Дом Рассвета",
                    status = "accepted",
                    realignmentMode = "accepted_transfer",
                    residentHistoryEntryId = "history_entry_1",
                    resolvedAtTurn = 21,
                    resolvedAtUtc = "2026-04-17T03:12:00Z"
                }
            },
            factions = new object[]
            {
                new
                {
                    factionId = "faction_dawn",
                    originType = "native_radiant",
                    hallId = "hall_dawn",
                    charter = new
                    {
                        factionName = "Дом Рассвета",
                        favoredArchetype = "accord",
                        patronEffectFamily = "social",
                        summary = "Новая сияющая фракция."
                    },
                    leadership = new
                    {
                        headActorType = "resident",
                        headActorId = "resident_liora",
                        leadershipState = "secure"
                    },
                    baseStrength = 35,
                    factionStrength = 62,
                    investCountThisAscension = 0,
                    projectArchetypesCountedThisAscension = Array.Empty<string>(),
                    projects = Array.Empty<object>(),
                    tradeInventoryReceipts = Array.Empty<object>(),
                    leadershipReceipts = new object[]
                    {
                        new
                        {
                            requestId = "leadership_req_1",
                            factionName = "Дом Рассвета",
                            transitionMode = "peaceful_succession",
                            previousHeadActorType = "guardian",
                            previousHeadActorId = "guardian_old",
                            newHeadActorType = "resident",
                            newHeadActorId = "resident_liora",
                            newHeadLabel = "резидент resident_liora",
                            status = "accepted",
                            resolvedAtTurn = 22,
                            resolvedAtUtc = "2026-04-17T03:15:00Z"
                        }
                    },
                    leadershipHistory = Array.Empty<object>()
                }
            }
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);

        var founding = Assert.Single(notifications.Where(item => string.Equals(item.NotificationType, AfterlifeNotificationState.TypeShiningFactionFoundingResolved, StringComparison.OrdinalIgnoreCase)));
        Assert.Contains("сторонников 3", founding.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Дом Рассвета", founding.Summary, StringComparison.OrdinalIgnoreCase);

        var realignment = Assert.Single(notifications.Where(item => string.Equals(item.NotificationType, AfterlifeNotificationState.TypeShiningFactionRealignmentResolved, StringComparison.OrdinalIgnoreCase)));
        Assert.Contains("согласованный переход", realignment.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("faction_old", realignment.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Дом Рассвета", realignment.Summary, StringComparison.OrdinalIgnoreCase);

        var leadership = Assert.Single(notifications.Where(item => string.Equals(item.NotificationType, AfterlifeNotificationState.TypeShiningFactionLeadershipResolved, StringComparison.OrdinalIgnoreCase)));
        Assert.Contains("мирная преемственность", leadership.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("резидент resident_liora", leadership.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_GuardianQuestAvailable_CreatesSingleUnreadNotification()
    {
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guardian_alpha",
                    canonicalName = "Азалия",
                    nameVariants = new { @default = "Азалия", feminine = "Азалия", masculine = (string?)null, neutral = (string?)null },
                    manifestation = new
                    {
                        currentDisplayName = "Азалия",
                        formFlexibility = "selective",
                        currentPresentationStyle = "feminine",
                        currentPronouns = "она/её",
                        appearanceDescription = "Тестовая форма."
                    },
                    questManagement = new
                    {
                        availableQuests = new[]
                        {
                            new
                            {
                                questId = "quest_archive_1",
                                title = "След летописи",
                                description = "Архивная запись указывает путь.",
                                status = "available",
                                difficulty = "normal",
                                questOrigin = "archive_consultation_hook",
                                sourceProjectId = "archive_consult_project"
                            }
                        },
                        activeQuests = Array.Empty<object>(),
                        completedQuests = Array.Empty<object>()
                    }
                }
            }
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);
        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var notification = Assert.Single(notifications);
        Assert.Equal(AfterlifeNotificationState.TypeGuardianQuestAvailable, notification.NotificationType);
        Assert.Equal(AfterlifeNotificationState.StatusUnread, notification.Status);
        Assert.Equal("guardian_alpha:quest_archive_1", notification.RequestId);
        Assert.Contains("архивный квест", notification.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("След летописи", notification.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_BaselineGuardianQuestAvailable_CreatesNotification()
    {
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guardian_alpha",
                    canonicalName = "Азалия",
                    nameVariants = new { @default = "Азалия", feminine = "Азалия", masculine = (string?)null, neutral = (string?)null },
                    manifestation = new
                    {
                        currentDisplayName = "Азалия",
                        formFlexibility = "selective",
                        currentPresentationStyle = "feminine",
                        currentPronouns = "она/её",
                        appearanceDescription = "Тестовая форма."
                    },
                    questManagement = new
                    {
                        availableQuests = new[]
                        {
                            new
                            {
                                questId = "quest_baseline_1",
                                title = "Первое поручение",
                                description = "Добровольная проверка для следующей жизни.",
                                status = "available",
                                difficulty = "normal",
                                questOrigin = GuardianProjectState.BaselineMortalLifeHookOrigin
                            }
                        },
                        activeQuests = Array.Empty<object>(),
                        completedQuests = Array.Empty<object>()
                    }
                }
            }
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var notification = Assert.Single(notifications);
        Assert.Equal(AfterlifeNotificationState.TypeGuardianQuestAvailable, notification.NotificationType);
        Assert.Equal("guardian_alpha:quest_baseline_1", notification.RequestId);
        Assert.Contains("добровольное поручение", notification.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Первое поручение", notification.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_GuardianQuestWithoutQuestId_DoesNotCreateNotification()
    {
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guardian_alpha",
                    canonicalName = "Азалия",
                    nameVariants = new { @default = "Азалия", feminine = "Азалия", masculine = (string?)null, neutral = (string?)null },
                    manifestation = new
                    {
                        currentDisplayName = "Азалия",
                        formFlexibility = "selective",
                        currentPresentationStyle = "feminine",
                        currentPronouns = "она/её",
                        appearanceDescription = "Тестовая форма."
                    },
                    questManagement = new
                    {
                        availableQuests = new[]
                        {
                            new
                            {
                                title = "След без идентификатора",
                                description = "Нарушенный контракт квеста.",
                                status = "available",
                                difficulty = "normal",
                                questOrigin = "archive_consultation_hook",
                                sourceProjectId = "archive_consult_project"
                            }
                        },
                        activeQuests = Array.Empty<object>(),
                        completedQuests = Array.Empty<object>()
                    }
                }
            }
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        Assert.Empty(notifications);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ArchiveConsultationReceipt_CreatesSingleUnreadNotification()
    {
        await WriteJsonAsync(AfterlifeArchiveActionState.ConsultationRequestPath, new
        {
            requestId = "archive_consult_req_1",
            guardianId = "guardian_alpha",
            guardianName = "Азалия",
            archiveId = "archive_1",
            archiveTitle = "Тень старого закона",
            archiveEntryType = "lore_fragment",
            archiveRarity = "Rare",
            archiveSourceKind = "codex",
            targetIncarnation = 2,
            createdAtTurn = 7,
            createdAtUtc = "2026-03-26T00:00:00Z",
            requestedMode = "consultation"
        });
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            afterlifeArchive = new
            {
                stored = Array.Empty<object>(),
                actionReceipts = new object[]
                {
                    new
                    {
                        requestId = "archive_consult_req_1",
                        archiveId = "archive_1",
                        requestedMode = "consultation",
                        status = "accepted",
                        guardianId = "guardian_alpha",
                        guardianName = "Азалия",
                        guaranteedArchiveQuestCount = 1,
                        questHookCount = 0,
                        specialQuestLineUnlocks = 0,
                        visibleRivalClueBonus = 0,
                        archiveWarningTierBonus = 0,
                        resolvedAtTurn = 8,
                        resolvedAtUtc = "2026-03-26T00:05:00Z"
                    }
                }
            }
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);
        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var notification = Assert.Single(notifications);
        Assert.Equal(AfterlifeNotificationState.TypeArchiveConsultationAccepted, notification.NotificationType);
        Assert.Equal(AfterlifeNotificationState.StatusUnread, notification.Status);
        Assert.Equal("Тень старого закона", notification.ArchiveTitle);
        Assert.Contains("принял архивную консультацию", notification.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("гарантирован 1 квест Хранителя", notification.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ArchiveConsultationAcceptedWithoutCanonicalOutcomeReceipt_DoesNotCreateAcceptedNotification()
    {
        await WriteJsonAsync(AfterlifeArchiveActionState.ConsultationRequestPath, new
        {
            requestId = "archive_consult_req_strict",
            guardianId = "guardian_alpha",
            guardianName = "Азалия",
            archiveId = "archive_1",
            archiveTitle = "Тень старого закона",
            archiveEntryType = "lore_fragment",
            archiveRarity = "Rare",
            archiveSourceKind = "codex",
            targetIncarnation = 2,
            createdAtTurn = 7,
            createdAtUtc = "2026-03-26T00:00:00Z",
            requestedMode = "consultation"
        });
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            afterlifeArchive = new
            {
                stored = Array.Empty<object>(),
                actionReceipts = new object[]
                {
                    new
                    {
                        requestId = "archive_consult_req_strict",
                        archiveId = "archive_1",
                        requestedMode = "consultation",
                        status = "accepted",
                        guardianId = "guardian_alpha",
                        guardianName = "Азалия",
                        resolvedAtTurn = 8,
                        resolvedAtUtc = "2026-03-26T00:05:00Z"
                    }
                }
            }
        });
        await WriteJsonAsync(GuardianProjectState.TrackerPath, new
        {
            activeProjects = Array.Empty<object>(),
            completedProjects = new[]
            {
                new
                {
                    guardianId = "guardian_alpha",
                    project = new
                    {
                        projectOrigin = "archive_consultation",
                        consultationRequestId = "archive_consult_req_other",
                        consultationArchiveId = "archive_1",
                        projectOutcomeAudit = new
                        {
                            guaranteedArchiveQuestCount = 1,
                            questHookCount = 0,
                            specialQuestLineUnlocks = 0,
                            visibleRivalClueBonus = 0,
                            archiveWarningTierBonus = 0
                        }
                    }
                }
            }
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        Assert.Empty(await AfterlifeNotificationState.ReadAsync(_fs));
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ArchiveConsultationAcceptedWithMalformedCurrentArchiveOwnerState_DoesNotCreateAcceptedNotification()
    {
        await WriteJsonAsync(AfterlifeArchiveActionState.ConsultationRequestPath, new
        {
            requestId = "archive_consult_req_owner_invalid",
            guardianId = "guardian_alpha",
            guardianName = "Азалия",
            archiveId = "archive_1",
            archiveTitle = "Тень старого закона",
            archiveEntryType = "lore_fragment",
            archiveRarity = "Rare",
            archiveSourceKind = "codex",
            targetIncarnation = 2,
            createdAtTurn = 7,
            createdAtUtc = "2026-03-26T00:00:00Z",
            requestedMode = "consultation"
        });
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            afterlifeArchive = new
            {
                actionReceipts = new object[]
                {
                    new
                    {
                        requestId = "archive_consult_req_owner_invalid",
                        archiveId = "archive_1",
                        requestedMode = "consultation",
                        status = "accepted",
                        guardianId = "guardian_alpha",
                        guardianName = "Азалия",
                        guaranteedArchiveQuestCount = 1,
                        resolvedAtTurn = 8,
                        resolvedAtUtc = "2026-03-26T00:05:00Z"
                    }
                }
            }
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        Assert.Empty(await AfterlifeNotificationState.ReadAsync(_fs));
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_MalformedArchiveConsultationRequest_CreatesAttentionNotification()
    {
        await _fs.WriteFileAtomicAsync(
            AfterlifeArchiveActionState.ConsultationRequestPath,
            """
            {
              "requestId": "archive_consult_req_broken",
              "guardianId":
            """
        );

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var notification = Assert.Single(notifications);
        Assert.Equal(AfterlifeNotificationState.TypeArchiveConsultationPendingAttention, notification.NotificationType);
        Assert.Contains("повреждён", notification.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("заблокирован", notification.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ArchiveProjectFuelReceipt_UsesExactWorkDeltaInSummary()
    {
        await WriteJsonAsync(AfterlifeArchiveActionState.ProjectFuelRequestPath, new
        {
            requestId = "archive_fuel_req_1",
            guardianId = "guardian_alpha",
            guardianName = "Азалия",
            archiveId = "archive_2",
            archiveTitle = "Медная карта осад",
            archiveEntryType = "lore_fragment",
            archiveRarity = "Rare",
            archiveSourceKind = "codex",
            targetProjectId = "project_alpha",
            targetProjectName = "Башня Наблюдений",
            createdAtTurn = 7,
            createdAtUtc = "2026-03-26T00:00:00Z",
            requestedMode = "project_fuel"
        });
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            afterlifeArchive = new
            {
                stored = Array.Empty<object>(),
                actionReceipts = new object[]
                {
                    new
                    {
                        requestId = "archive_fuel_req_1",
                        archiveId = "archive_2",
                        requestedMode = "project_fuel",
                        status = "accepted",
                        guardianId = "guardian_alpha",
                        guardianName = "Азалия",
                        targetProjectId = "project_alpha",
                        resultMode = "project_work",
                        resultAmount = 2,
                        resolvedAtTurn = 8,
                        resolvedAtUtc = "2026-03-26T00:05:00Z"
                    }
                }
            }
        });
        await WriteJsonAsync(
            GuardianProjectState.JournalPath,
            new
            {
                entries = new[]
                {
                    new
                    {
                        entryId = "jp_1",
                        turn = 8,
                        guardianId = "guardian_alpha",
                        projectId = "project_alpha",
                        eventType = "assisted",
                        archiveFuelRequestId = "archive_fuel_req_1",
                        title = "Проект усилен архивной записью",
                        summary = "Хранитель продвинул проект вперёд.",
                        details = new[]
                        {
                            "Проект: Башня Наблюдений",
                            "Работа: 6 -> 8"
                        }
                    }
                }
            });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var notification = Assert.Single(notifications);
        Assert.Equal(AfterlifeNotificationState.TypeArchiveProjectFuelAccepted, notification.NotificationType);
        Assert.Contains("работа +2", notification.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ArchiveProjectFuelAcceptedWithoutCanonicalResultReceipt_DoesNotCreateAcceptedNotification()
    {
        await WriteJsonAsync(AfterlifeArchiveActionState.ProjectFuelRequestPath, new
        {
            requestId = "archive_fuel_req_strict",
            guardianId = "guardian_alpha",
            guardianName = "Азалия",
            archiveId = "archive_2",
            archiveTitle = "Медная карта осад",
            archiveEntryType = "lore_fragment",
            archiveRarity = "Rare",
            archiveSourceKind = "codex",
            targetProjectId = "project_alpha",
            targetProjectName = "Башня Наблюдений",
            createdAtTurn = 7,
            createdAtUtc = "2026-03-26T00:00:00Z",
            requestedMode = "project_fuel"
        });
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            afterlifeArchive = new
            {
                stored = Array.Empty<object>(),
                actionReceipts = new object[]
                {
                    new
                    {
                        requestId = "archive_fuel_req_strict",
                        archiveId = "archive_2",
                        requestedMode = "project_fuel",
                        status = "accepted",
                        guardianId = "guardian_alpha",
                        guardianName = "Азалия",
                        targetProjectId = "project_alpha",
                        resolvedAtTurn = 8,
                        resolvedAtUtc = "2026-03-26T00:05:00Z"
                    }
                }
            }
        });
        await WriteJsonAsync(
            GuardianProjectState.JournalPath,
            new
            {
                entries = new[]
                {
                    new
                    {
                        entryId = "jp_strict",
                        turn = 8,
                        guardianId = "guardian_alpha",
                        projectId = "project_alpha",
                        eventType = "assisted",
                        archiveFuelRequestId = "archive_fuel_req_other",
                        title = "Проект усилен архивной записью",
                        summary = "Хранитель продвинул проект вперёд.",
                        details = new[]
                        {
                            "Проект: Башня Наблюдений",
                            "Работа: 6 -> 8",
                            "ArchiveId: archive_2"
                        }
                    }
                }
            });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        Assert.Empty(await AfterlifeNotificationState.ReadAsync(_fs));
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ArchiveProjectFuelAcceptedWithMalformedCurrentArchiveOwnerState_DoesNotCreateAcceptedNotification()
    {
        await WriteJsonAsync(AfterlifeArchiveActionState.ProjectFuelRequestPath, new
        {
            requestId = "archive_fuel_req_owner_invalid",
            guardianId = "guardian_alpha",
            guardianName = "Азалия",
            archiveId = "archive_2",
            archiveTitle = "Медная карта осад",
            archiveEntryType = "lore_fragment",
            archiveRarity = "Rare",
            archiveSourceKind = "codex",
            targetProjectId = "project_alpha",
            targetProjectName = "Башня Наблюдений",
            createdAtTurn = 7,
            createdAtUtc = "2026-03-26T00:00:00Z",
            requestedMode = "project_fuel"
        });
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            afterlifeArchive = new
            {
                actionReceipts = new object[]
                {
                    new
                    {
                        requestId = "archive_fuel_req_owner_invalid",
                        archiveId = "archive_2",
                        requestedMode = "project_fuel",
                        status = "accepted",
                        guardianId = "guardian_alpha",
                        guardianName = "Азалия",
                        targetProjectId = "project_alpha",
                        resultMode = "project_work",
                        resultAmount = 2,
                        resolvedAtTurn = 8,
                        resolvedAtUtc = "2026-03-26T00:05:00Z"
                    }
                }
            }
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        Assert.Empty(await AfterlifeNotificationState.ReadAsync(_fs));
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_MalformedArchiveProjectFuelRequest_CreatesAttentionNotification()
    {
        await _fs.WriteFileAtomicAsync(
            AfterlifeArchiveActionState.ProjectFuelRequestPath,
            """
            {
              "requestId": "archive_fuel_req_broken",
              "guardianId":
            """
        );

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var notification = Assert.Single(notifications);
        Assert.Equal(AfterlifeNotificationState.TypeArchiveProjectFuelPendingAttention, notification.NotificationType);
        Assert.Contains("повреждён", notification.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("заблокирован", notification.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_AbodeResidentsReady_CreatesNotificationFromPendingRequest()
    {
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingResidentsRequestPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "abode_residents_req_1",
                    guardianId = "guardian_alpha",
                    guardianName = "Азалия",
                    abodeId = "abode_alpha",
                    abodeName = "Сад Нитей",
                    currentReputation = 110,
                    createdAtTurn = 9,
                    createdAtUtc = "2026-03-27T00:00:00Z"
                }
            }
        });
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    roleLabel = "Вестница",
                    summary = "Тонкая душа на границе светлых троп.",
                    bondLevel = 40,
                    bondTier = "familiar",
                    canGrantCompanionRelic = true,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = false,
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец при храме дорог.",
                        futureCompanionPrompt = "Swift wanderer",
                        bondReason = "",
                        coreTraits = new[] { "верность" },
                        archetypeHints = new[] { "courier" },
                        appearanceMotifs = new[] { "threaded cloak" }
                    },
                    availableInteractions = new[] { "talk", "history", "quest" }
                }
            }
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var notification = Assert.Single(notifications);
        Assert.Equal(AfterlifeNotificationState.TypeAbodeResidentsReady, notification.NotificationType);
        Assert.Contains("проявились обитатели", notification.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ResidentQuestAvailable_CreatesNotification()
    {
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    roleLabel = "Вестница",
                    summary = "Тонкая душа на границе светлых троп.",
                    bondLevel = 55,
                    bondTier = "trusted",
                    canGrantCompanionRelic = true,
                    bondRewardState = "none",
                    linkedSoulQuestId = "soul_quest_resident_1",
                    grantedRelicId = "",
                    historyRevealed = true,
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец при храме дорог.",
                        futureCompanionPrompt = "Swift wanderer",
                        bondReason = "Она помнит старые клятвы.",
                        coreTraits = new[] { "верность" },
                        archetypeHints = new[] { "courier" },
                        appearanceMotifs = new[] { "threaded cloak" }
                    },
                    availableInteractions = new[] { "talk", "history", "quest" }
                }
            }
        });
        await WriteJsonAsync("game_state/quests/soul_quests.json", new
        {
            quests = new object[]
            {
                new
                {
                    questId = "soul_quest_resident_1",
                    title = "След гонца",
                    description = "Нужно вернуть долговое письмо.",
                    status = "active",
                    guardianId = "guardian_alpha",
                    relatedAfterlifeResidentId = "resident_alpha_1",
                    createdAtTurn = 12,
                    createdAtUtc = "2026-03-27T00:05:00Z"
                }
            }
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var notification = Assert.Single(notifications);
        Assert.Equal(AfterlifeNotificationState.TypeAbodeResidentQuestAvailable, notification.NotificationType);
        Assert.Contains("стала квестом души", notification.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Лиора", notification.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_RosterReceipt_RebuildsReadyNotificationWithoutPendingFile()
    {
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    roleLabel = "Вестница",
                    summary = "Тонкая душа на границе светлых троп.",
                    bondLevel = 55,
                    bondTier = "trusted",
                    canGrantCompanionRelic = true,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = false,
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец при храме дорог.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                }
            },
            rosterReceipts = new object[]
            {
                new
                {
                    requestId = "abode_req_1",
                    guardianId = "guardian_alpha",
                    guardianName = "Азалия",
                    abodeId = "abode_alpha",
                    abodeName = "Сад Нитей",
                    rosterCount = 1,
                    resolvedAtTurn = 12,
                    resolvedAtUtc = "2026-03-27T00:12:00Z"
                }
            }
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var notification = Assert.Single(
            notifications,
            entry => string.Equals(entry.NotificationType, AfterlifeNotificationState.TypeAbodeResidentsReady, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("abode_req_1", notification.RequestId);
        Assert.Equal(12, notification.CreatedAtTurn);
        Assert.Contains("Сад Нитей", notification.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ResidentRelicGranted_CreatesNotification()
    {
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new object[]
            {
                new
                {
                    guardianId = "guardian_alpha",
                    canonicalName = "Азалия",
                    nameVariants = new { @default = "Азалия" },
                    manifestation = new
                    {
                        currentDisplayName = "Азалия",
                        formFlexibility = "selective",
                        currentPresentationStyle = "feminine",
                        currentPronouns = "она/её",
                        appearanceDescription = "Тестовая форма."
                    },
                    manifestationHistory = Array.Empty<object>()
                }
            }
        });
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    roleLabel = "Вестница",
                    summary = "Тонкая душа на границе светлых троп.",
                    bondLevel = 80,
                    bondTier = "bound",
                    canGrantCompanionRelic = true,
                    bondRewardState = "granted",
                    linkedSoulQuestId = "soul_quest_resident_1",
                    grantedRelicId = "relic_echo_liora",
                    historyRevealed = true,
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец при храме дорог.",
                        futureCompanionPrompt = "Swift wanderer",
                        bondReason = "Она помнит старые клятвы.",
                        coreTraits = new[] { "верность" },
                        archetypeHints = new[] { "courier" },
                        appearanceMotifs = new[] { "threaded cloak" }
                    },
                    availableInteractions = new[] { "reward" }
                }
            },
            interactionLog = new object[]
            {
                new
                {
                    entryId = "resident_relic_log_1",
                    residentId = "resident_alpha_1",
                    turn = 22,
                    timestamp = "2026-03-27T00:22:00Z",
                    eventType = "relic_grant",
                    title = "Дар реликвии",
                    summary = "Лиора доверила душе реликвию связи.",
                    relatedRelicId = "relic_echo_liora"
                }
            }
        });
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulRelics = new
            {
                equipped = Array.Empty<object>(),
                stored = new object[]
                {
                    new
                    {
                        relicId = "relic_echo_liora",
                        name = "Эхо Лиоры",
                        rarity = "Rare",
                        relicType = "companion_echo",
                        companionSeed = new
                        {
                            sourceResidentId = "resident_alpha_1",
                            sourceGuardianId = "guardian_alpha",
                            companionNameHint = "Лиора",
                            originWorldSummary = "Бывшая гонец при храме дорог.",
                            futureCompanionPrompt = "Swift wanderer"
                        }
                    }
                }
            }
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var notification = Assert.Single(notifications);
        Assert.Equal(AfterlifeNotificationState.TypeAbodeResidentRelicGranted, notification.NotificationType);
        Assert.Equal("Азалия", notification.GuardianName);
        Assert.Equal(22, notification.CreatedAtTurn);
        Assert.Equal("2026-03-27T00:22:00Z", notification.CreatedAtUtc);
        Assert.Contains("даровал реликвию связи", notification.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Эхо Лиоры", notification.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ResidentRelicGrantedWithMalformedCurrentSoulRelics_DoesNotCreateNotification()
    {
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    roleLabel = "Вестница",
                    summary = "Тонкая душа на границе светлых троп.",
                    bondLevel = 80,
                    bondTier = "bound",
                    canGrantCompanionRelic = true,
                    bondRewardState = "granted",
                    linkedSoulQuestId = "soul_quest_resident_1",
                    grantedRelicId = "relic_echo_liora",
                    historyRevealed = true,
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец при храме дорог.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                }
            },
            interactionLog = new object[]
            {
                new
                {
                    entryId = "resident_relic_log_1",
                    residentId = "resident_alpha_1",
                    turn = 22,
                    timestamp = "2026-03-27T00:22:00Z",
                    eventType = "relic_grant",
                    title = "Дар реликвии",
                    summary = "Лиора доверила душе реликвию связи.",
                    relatedRelicId = "relic_echo_liora"
                }
            }
        });
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulRelics = new
            {
                equipped = Array.Empty<object>(),
                stored = new object[]
                {
                    new
                    {
                        relicId = "relic_echo_liora",
                        name = "Эхо Лиоры",
                        rarity = "Rare",
                        relicType = "companion_echo",
                        companionSeed = new
                        {
                            sourceResidentId = "resident_alpha_1"
                        }
                    }
                }
            }
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        Assert.Empty(await AfterlifeNotificationState.ReadAsync(_fs));
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ResidentRelicGrantedWithOrphanedPendingSnapshotTriggerContext_DoesNotCreateNotification()
    {
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 2
        });
        await WriteJsonAsync("game_state/control/life_transitions.json", new
        {
            reason = "Death",
            summary = "Жизнь завершена."
        });
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    roleLabel = "Вестница",
                    summary = "Тонкая душа на границе светлых троп.",
                    bondLevel = 80,
                    bondTier = "bound",
                    canGrantCompanionRelic = true,
                    bondRewardState = "granted",
                    linkedSoulQuestId = "soul_quest_resident_1",
                    grantedRelicId = "relic_echo_liora",
                    historyRevealed = true,
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец при храме дорог.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                }
            },
            interactionLog = new object[]
            {
                new
                {
                    entryId = "resident_relic_log_1",
                    residentId = "resident_alpha_1",
                    turn = 22,
                    timestamp = "2026-03-27T00:22:00Z",
                    eventType = "relic_grant",
                    title = "Дар реликвии",
                    summary = "Лиора доверила душе реликвию связи.",
                    relatedRelicId = "relic_echo_liora"
                }
            }
        });
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 2,
            metaStateUpdates = new
            {
                lifeTransitions = new
                {
                    recordLifeCompletion = new
                    {
                        characterFinalState = new { causeOfDeath = "Test" },
                        majorAchievements = Array.Empty<string>(),
                        relationshipsFormed = Array.Empty<object>(),
                        moralChoices = Array.Empty<object>(),
                        skillsLearned = Array.Empty<string>(),
                        enlightenmentGained = 0
                    }
                }
            },
            soulRelics = new
            {
                equipped = Array.Empty<object>(),
                stored = new object[]
                {
                    new
                    {
                        relicId = "relic_echo_liora",
                        name = "Эхо Лиоры",
                        rarity = "Rare",
                        relicType = "companion_echo",
                        companionSeed = new
                        {
                            sourceResidentId = "resident_alpha_1",
                            sourceGuardianId = "guardian_alpha",
                            companionNameHint = "Лиора",
                            originWorldSummary = "Бывшая гонец при храме дорог.",
                            futureCompanionPrompt = "Swift wanderer"
                        }
                    }
                }
            }
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        Assert.Empty(await AfterlifeNotificationState.ReadAsync(_fs));
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ResidentManifestationReady_CreatesNotification()
    {
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new object[]
            {
                new
                {
                    guardianId = "guardian_alpha",
                    canonicalName = "Азалия",
                    nameVariants = new { @default = "Азалия" },
                    manifestation = new
                    {
                        currentDisplayName = "Азалия",
                        formFlexibility = "selective",
                        currentPresentationStyle = "feminine",
                        currentPronouns = "она/её",
                        appearanceDescription = "Тестовая форма."
                    },
                    manifestationHistory = Array.Empty<object>()
                }
            }
        });
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    roleLabel = "Вестница",
                    summary = "Тонкая душа на границе светлых троп.",
                    bondLevel = 80,
                    bondTier = "bound",
                    canGrantCompanionRelic = true,
                    bondRewardState = "consumed",
                    linkedSoulQuestId = "soul_quest_resident_1",
                    grantedRelicId = "relic_echo_liora",
                    historyRevealed = true,
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец при храме дорог.",
                        futureCompanionPrompt = "Swift wanderer"
                    },
                    availableInteractions = new[] { "reward" }
                }
            }
        });
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = "relic_echo_liora",
                        name = "Эхо Лиоры",
                        rarity = "Rare",
                        relicType = "companion_echo",
                        companionManifestationStatus = "materialized",
                        companionManifestationResolvedRequestId = "resident_manifest_req_1",
                        companionManifestationResolvedNpcId = "npc_manifested_liora",
                        companionManifestationResolvedAtTurn = 18,
                        companionManifestationResolvedAtUtc = "2026-03-27T00:18:00Z",
                        companionSeed = new
                        {
                            sourceResidentId = "resident_alpha_1",
                            sourceGuardianId = "guardian_alpha",
                            companionNameHint = "Лиора",
                            originWorldSummary = "Бывшая гонец при храме дорог.",
                            futureCompanionPrompt = "Swift wanderer"
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_manifested_liora",
              "npcName": "Лиора из новой жизни",
              "sourceCompanionRelicId": "relic_echo_liora",
              "sourceAfterlifeResidentId": "resident_alpha_1"
            }
          ]
        }
        """);

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var notification = Assert.Single(
            notifications,
            entry => string.Equals(entry.NotificationType, AfterlifeNotificationState.TypeAbodeResidentManifestationReady, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(AfterlifeNotificationState.TypeAbodeResidentManifestationReady, notification.NotificationType);
        Assert.Equal("resident_manifest_req_1", notification.RequestId);
        Assert.Equal(18, notification.CreatedAtTurn);
        Assert.Equal("2026-03-27T00:18:00Z", notification.CreatedAtUtc);
        Assert.Contains("Лиора", notification.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("новой жизни", notification.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ResidentManifestationReadyWithMalformedCurrentSoulRelics_DoesNotCreateNotification()
    {
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    roleLabel = "Вестница",
                    summary = "Тонкая душа на границе светлых троп.",
                    bondLevel = 80,
                    bondTier = "bound",
                    canGrantCompanionRelic = true,
                    bondRewardState = "consumed",
                    linkedSoulQuestId = "soul_quest_resident_1",
                    grantedRelicId = "relic_echo_liora",
                    historyRevealed = true,
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец при храме дорог.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                }
            }
        });
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = "relic_echo_liora",
                        name = "Эхо Лиоры",
                        rarity = "Rare",
                        relicType = "companion_echo",
                        companionManifestationStatus = "materialized",
                        companionManifestationResolvedRequestId = "resident_manifest_req_1",
                        companionManifestationResolvedNpcId = "npc_manifested_liora",
                        companionManifestationResolvedAtTurn = 18,
                        companionManifestationResolvedAtUtc = "2026-03-27T00:18:00Z",
                        companionSeed = new
                        {
                            sourceResidentId = "resident_alpha_1"
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_manifested_liora",
              "npcName": "Лиора из новой жизни",
              "sourceCompanionRelicId": "relic_echo_liora",
              "sourceAfterlifeResidentId": "resident_alpha_1"
            }
          ]
        }
        """);

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        Assert.Empty(await AfterlifeNotificationState.ReadAsync(_fs));
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ImprintManifestationReady_CreatesDedicatedNotification()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = "relic_imprint_1",
                        name = "Отголосок стража",
                        rarity = "Rare",
                        companionManifestationStatus = "materialized",
                        companionManifestationResolvedRequestId = "imprint_manifest_req_1",
                        companionManifestationResolvedNpcId = "npc_manifested_guard",
                        companionManifestationResolvedAtTurn = 22,
                        companionManifestationResolvedAtUtc = "2026-03-27T00:22:00Z",
                        soulImprint = new
                        {
                            imprintId = "imprint_guard_1",
                            npcName = "Страж Кел",
                            description = "Бывший страж северных ворот.",
                            personalityTraits = new[] { "стойкость" }
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_manifested_guard",
              "npcName": "Кел Страж",
              "sourceCompanionRelicId": "relic_imprint_1",
              "sourceSoulImprintId": "imprint_guard_1"
            }
          ]
        }
        """);

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var notification = Assert.Single(
            notifications,
            entry => string.Equals(entry.NotificationType, AfterlifeNotificationState.TypeCompanionImprintManifestationReady, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("imprint_manifest_req_1", notification.RequestId);
        Assert.Equal(22, notification.CreatedAtTurn);
        Assert.Equal("2026-03-27T00:22:00Z", notification.CreatedAtUtc);
        Assert.Contains("Кел", notification.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("спутника", notification.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ImprintManifestationReadyWithMalformedCurrentSoulRelics_DoesNotCreateNotification()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = "relic_imprint_1",
                        name = "Отголосок стража",
                        rarity = "Rare",
                        companionManifestationStatus = "materialized",
                        companionManifestationResolvedRequestId = "imprint_manifest_req_1",
                        companionManifestationResolvedNpcId = "npc_manifested_guard",
                        companionManifestationResolvedAtTurn = 22,
                        companionManifestationResolvedAtUtc = "2026-03-27T00:22:00Z",
                        soulImprint = new
                        {
                            imprintId = "imprint_guard_1",
                            npcName = "Страж Кел",
                            description = "Бывший страж северных ворот."
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_manifested_guard",
              "npcName": "Кел Страж",
              "sourceCompanionRelicId": "relic_imprint_1",
              "sourceSoulImprintId": "imprint_guard_1"
            }
          ]
        }
        """);

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        Assert.Empty(await AfterlifeNotificationState.ReadAsync(_fs));
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ManifestationReady_LegacyAliasNpcSectionDoesNotCreateCanonicalNotification()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = "relic_echo_liora",
                        name = "Эхо Лиоры",
                        rarity = "Rare",
                        relicType = "companion_echo",
                        companionManifestationStatus = "materialized",
                        companionManifestationResolvedRequestId = "resident_manifest_req_legacy_alias",
                        companionManifestationResolvedNpcId = "npc_manifested_liora",
                        companionManifestationResolvedAtTurn = 18,
                        companionManifestationResolvedAtUtc = "2026-03-27T00:18:00Z",
                        companionSeed = new
                        {
                            sourceResidentId = "resident_alpha_1",
                            sourceGuardianId = "guardian_alpha",
                            companionNameHint = "Лиора",
                            originWorldSummary = "Бывшая гонец при храме дорог.",
                            futureCompanionPrompt = "Swift wanderer"
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });
        await WriteJsonAsync("game_state/npcs/npc_core.json", new
        {
            npcs = new object[]
            {
                new
                {
                    npcId = "npc_manifested_liora",
                    npcName = "Лиора из новой жизни",
                    sourceCompanionRelicId = "relic_echo_liora",
                    sourceAfterlifeResidentId = "resident_alpha_1"
                }
            }
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        Assert.DoesNotContain(
            notifications,
            entry => string.Equals(entry.RequestId, "resident_manifest_req_legacy_alias", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            notifications,
            entry => string.Equals(entry.NotificationType, AfterlifeNotificationState.TypeAbodeResidentManifestationReady, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ResidentTalkAnswered_CreatesNotification()
    {
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "resident_talk_req_1",
                    guardianId = "guardian_alpha",
                    guardianName = "Азалия",
                    abodeId = "abode_alpha",
                    abodeName = "Сад Нитей",
                    residentId = "resident_alpha_1",
                    residentName = "Лиора",
                    interactionType = "talk",
                    createdAtTurn = 14,
                    createdAtUtc = "2026-03-27T00:10:00Z"
                }
            }
        });
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    roleLabel = "Вестница",
                    summary = "Тонкая душа на границе светлых троп.",
                    bondLevel = 40,
                    bondTier = "familiar",
                    canGrantCompanionRelic = true,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = false,
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец при храме дорог.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                }
            },
            interactionReceipts = new object[]
            {
                new
                {
                    requestId = "resident_talk_req_1",
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    guardianName = "Азалия",
                    abodeId = "abode_alpha",
                    abodeName = "Сад Нитей",
                    residentName = "Лиора",
                    interactionType = "talk",
                    status = "accepted",
                    responseMode = "talk_scene",
                    resolvedAtTurn = 15,
                    resolvedAtUtc = "2026-03-27T00:15:00Z"
                }
            }
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var notification = Assert.Single(notifications);
        Assert.Equal(AfterlifeNotificationState.TypeAbodeResidentTalkAnswered, notification.NotificationType);
        Assert.Contains("ответил", notification.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_HistoryReceipt_RebuildsNotificationWithoutPendingFile()
    {
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new object[]
            {
                new
                {
                    guardianId = "guardian_alpha",
                    canonicalName = "Азалия",
                    manifestation = new
                    {
                        currentDisplayName = "Азалия",
                        formFlexibility = "selective",
                        currentPresentationStyle = "feminine",
                        currentPronouns = "она/её",
                        appearanceDescription = "Тестовая форма."
                    },
                    manifestationHistory = Array.Empty<object>()
                }
            }
        });
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    roleLabel = "Вестница",
                    summary = "Тонкая душа на границе светлых троп.",
                    bondLevel = 40,
                    bondTier = "familiar",
                    canGrantCompanionRelic = true,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = true,
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец при храме дорог.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                }
            },
            interactionReceipts = new object[]
            {
                new
                {
                    requestId = "resident_history_req_1",
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    guardianName = "Азалия",
                    abodeId = "abode_alpha",
                    abodeName = "Сад Нитей",
                    residentName = "Лиора",
                    interactionType = "history",
                    status = "accepted",
                    responseMode = "history_revealed",
                    historyEntryId = "hist_1",
                    resolvedAtTurn = 15,
                    resolvedAtUtc = "2026-03-27T00:15:00Z"
                }
            },
            historyLog = new object[]
            {
                new
                {
                    entryId = "hist_1",
                    residentId = "resident_alpha_1",
                    title = "Письмо через огонь",
                    summary = "Когда-то она несла письмо через горящий мост.",
                    revealedAtTurn = 15,
                    revealedAtUtc = "2026-03-27T00:15:00Z"
                }
            }
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var notification = Assert.Single(
            notifications,
            entry => string.Equals(entry.NotificationType, AfterlifeNotificationState.TypeAbodeResidentHistoryRevealed, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("resident_history_req_1", notification.RequestId);
        Assert.Equal(15, notification.CreatedAtTurn);
        Assert.Contains("Письмо через огонь", notification.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ResidentHistoryRevealed_CreatesNotification()
    {
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "resident_history_req_1",
                    guardianId = "guardian_alpha",
                    guardianName = "Азалия",
                    abodeId = "abode_alpha",
                    abodeName = "Сад Нитей",
                    residentId = "resident_alpha_1",
                    residentName = "Лиора",
                    interactionType = "history",
                    createdAtTurn = 14,
                    createdAtUtc = "2026-03-27T00:10:00Z"
                }
            }
        });
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    roleLabel = "Вестница",
                    summary = "Тонкая душа на границе светлых троп.",
                    bondLevel = 55,
                    bondTier = "trusted",
                    canGrantCompanionRelic = true,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = true,
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец при храме дорог.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                }
            },
            interactionReceipts = new object[]
            {
                new
                {
                    requestId = "resident_history_req_1",
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    guardianName = "Азалия",
                    abodeId = "abode_alpha",
                    abodeName = "Сад Нитей",
                    residentName = "Лиора",
                    interactionType = "history",
                    status = "accepted",
                    responseMode = "history_revealed",
                    historyEntryId = "history_liora_1",
                    resolvedAtTurn = 15,
                    resolvedAtUtc = "2026-03-27T00:15:00Z"
                }
            },
            historyLog = new object[]
            {
                new
                {
                    entryId = "history_liora_1",
                    residentId = "resident_alpha_1",
                    title = "Клятва гонца",
                    summary = "Лиора поклялась довести письмо сквозь осаду.",
                    revealedAtTurn = 15,
                    revealedAtUtc = "2026-03-27T00:15:00Z"
                }
            }
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var notification = Assert.Single(notifications);
        Assert.Equal(AfterlifeNotificationState.TypeAbodeResidentHistoryRevealed, notification.NotificationType);
        Assert.Contains("Клятва гонца", notification.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ResidentHistoryRefused_CreatesNotification()
    {
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "resident_history_req_refused",
                    guardianId = "guardian_alpha",
                    guardianName = "Азалия",
                    abodeId = "abode_alpha",
                    abodeName = "Сад Нитей",
                    residentId = "resident_alpha_1",
                    residentName = "Лиора",
                    interactionType = "history",
                    createdAtTurn = 14,
                    createdAtUtc = "2026-03-27T00:10:00Z"
                }
            }
        });
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    roleLabel = "Вестница",
                    summary = "Тонкая душа на границе светлых троп.",
                    bondLevel = 30,
                    bondTier = "familiar",
                    canGrantCompanionRelic = true,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = false,
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец при храме дорог.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                }
            },
            interactionReceipts = new object[]
            {
                new
                {
                    requestId = "resident_history_req_refused",
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    guardianName = "Азалия",
                    abodeId = "abode_alpha",
                    abodeName = "Сад Нитей",
                    residentName = "Лиора",
                    interactionType = "history",
                    status = "rejected",
                    responseMode = "history_refused",
                    reason = "Недостаточно доверия",
                    resolvedAtTurn = 15,
                    resolvedAtUtc = "2026-03-27T00:15:00Z"
                }
            }
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var notification = Assert.Single(notifications);
        Assert.Equal(AfterlifeNotificationState.TypeAbodeResidentHistoryRefused, notification.NotificationType);
        Assert.Contains("не раскрыл", notification.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_PressuredResidentInitialBaseline_DoesNotCreateBacklogNotification()
    {
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, BuildResidentPressureState(
            GuardianAbodeResidentState.MigrationStateRestless,
            abodeDevotionLevel: 34,
            abodeDevotionTier: GuardianAbodeResidentState.AbodeDevotionTierUncertain,
            restlessness: 61,
            turn: 21,
            timestamp: "2026-03-27T00:21:00Z"));

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        Assert.Empty(await AfterlifeNotificationState.ReadAsync(_fs));
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ResidentWaveringTransition_CreatesSingleNotificationWithoutDuplicates()
    {
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, BuildResidentPressureState(
            GuardianAbodeResidentState.MigrationStateSettled,
            abodeDevotionLevel: 72,
            abodeDevotionTier: GuardianAbodeResidentState.AbodeDevotionTierDevoted,
            restlessness: 18,
            turn: 19,
            timestamp: "2026-03-27T00:19:00Z"));

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);
        Assert.Empty(await AfterlifeNotificationState.ReadAsync(_fs));

        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, BuildResidentPressureState(
            GuardianAbodeResidentState.MigrationStateWavering,
            abodeDevotionLevel: 58,
            abodeDevotionTier: GuardianAbodeResidentState.AbodeDevotionTierAttached,
            restlessness: 32,
            turn: 22,
            timestamp: "2026-03-27T00:22:00Z"));

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var notification = Assert.Single(notifications);
        Assert.Equal(AfterlifeNotificationState.TypeAbodeResidentWavering, notification.NotificationType);
        Assert.Contains("заколебался", notification.Summary, StringComparison.OrdinalIgnoreCase);

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);
        notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        Assert.Single(notifications);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ResidentPressureRecoveryAndReentry_ReissuesNotification()
    {
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, BuildResidentPressureState(
            GuardianAbodeResidentState.MigrationStateSettled,
            abodeDevotionLevel: 68,
            abodeDevotionTier: GuardianAbodeResidentState.AbodeDevotionTierDevoted,
            restlessness: 20,
            turn: 18,
            timestamp: "2026-03-27T00:18:00Z"));

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, BuildResidentPressureState(
            GuardianAbodeResidentState.MigrationStateRestless,
            abodeDevotionLevel: 36,
            abodeDevotionTier: GuardianAbodeResidentState.AbodeDevotionTierUncertain,
            restlessness: 63,
            turn: 23,
            timestamp: "2026-03-27T00:23:00Z"));
        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);
        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        Assert.Single(notifications);
        Assert.Equal(AfterlifeNotificationState.TypeAbodeResidentRestless, notifications[0].NotificationType);

        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, BuildResidentPressureState(
            GuardianAbodeResidentState.MigrationStateSettled,
            abodeDevotionLevel: 61,
            abodeDevotionTier: GuardianAbodeResidentState.AbodeDevotionTierDevoted,
            restlessness: 22,
            turn: 24,
            timestamp: "2026-03-27T00:24:00Z"));
        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);
        Assert.Empty(await AfterlifeNotificationState.ReadAsync(_fs));

        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, BuildResidentPressureState(
            GuardianAbodeResidentState.MigrationStateRestless,
            abodeDevotionLevel: 31,
            abodeDevotionTier: GuardianAbodeResidentState.AbodeDevotionTierUncertain,
            restlessness: 66,
            turn: 25,
            timestamp: "2026-03-27T00:25:00Z"));
        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var notification = Assert.Single(notifications);
        Assert.Equal(AfterlifeNotificationState.TypeAbodeResidentRestless, notification.NotificationType);
        Assert.Equal("resident_pressure:resident_alpha_1:restless", notification.RequestId);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ReadyToTransfer_UsesConsideringDepartureNotificationType()
    {
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, BuildResidentPressureState(
            GuardianAbodeResidentState.MigrationStateSettled,
            abodeDevotionLevel: 62,
            abodeDevotionTier: GuardianAbodeResidentState.AbodeDevotionTierDevoted,
            restlessness: 20,
            turn: 18,
            timestamp: "2026-03-27T00:18:00Z"));
        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, BuildResidentPressureState(
            GuardianAbodeResidentState.MigrationStateReadyToTransfer,
            abodeDevotionLevel: 12,
            abodeDevotionTier: GuardianAbodeResidentState.AbodeDevotionTierAlienated,
            restlessness: 79,
            turn: 27,
            timestamp: "2026-03-27T00:27:00Z"));

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var notification = Assert.Single(notifications);
        Assert.Equal(AfterlifeNotificationState.TypeAbodeResidentConsideringDeparture, notification.NotificationType);
        Assert.Contains("готов искать иной свет", notification.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_PendingResidentTransferRequest_CreatesPendingNotification()
    {
        await GuardianAbodeResidentRequestState.WriteTransferRequestAsync(_fs, new GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentTransferRequest
        {
            RequestId = "resident_transfer_req_1",
            ResidentId = "resident_alpha_1",
            ResidentName = "Лиора",
            SourceGuardianId = "guardian_alpha",
            SourceGuardianName = "Азалия",
            SourceAbodeId = "abode_alpha",
            SourceAbodeName = "Лазурная Обитель",
            TargetGuardianId = "guardian_beta",
            TargetGuardianName = "Мириэль",
            TargetAbodeId = "abode_beta",
            TargetAbodeName = "Сад Перекрёстков",
            AbodeDevotionLevel = 12,
            AbodeDevotionTier = "alienated",
            Restlessness = 84,
            MigrationState = "ready_to_transfer",
            TransferMode = GuardianAbodeResidentState.TransferModeAcceptedTransfer,
            SelectionMode = GuardianAbodeResidentRequestState.TransferSelectionModeCompetitionRecommended,
            CompetitionScore = 78,
            CompetitionLabel = GuardianAbodeResidentState.TransferCompetitionLabelStrongPull,
            CompetitionReason = "цель заметно сильнее текущей Обители и обещает более устойчивый порядок.",
            CreatedAtTurn = 41,
            CreatedAtUtc = "2026-04-16T04:41:00Z"
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var notification = Assert.Single(notifications);
        Assert.Equal(AfterlifeNotificationState.TypeAbodeResidentTransferPending, notification.NotificationType);
        Assert.Equal("resident_transfer_req_1", notification.RequestId);
        Assert.Contains("может перейти", notification.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("сильный зов 78/100", notification.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ResidentTransferReceipt_CreatesAcceptedNotificationAndClearsPendingRequestNotice()
    {
        await GuardianAbodeResidentRequestState.WriteTransferRequestAsync(_fs, new GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentTransferRequest
        {
            RequestId = "resident_transfer_req_2",
            ResidentId = "resident_alpha_1",
            ResidentName = "Лиора",
            SourceGuardianId = "guardian_alpha",
            SourceGuardianName = "Азалия",
            SourceAbodeId = "abode_alpha",
            SourceAbodeName = "Лазурная Обитель",
            TargetGuardianId = "guardian_beta",
            TargetGuardianName = "Мириэль",
            TargetAbodeId = "abode_beta",
            TargetAbodeName = "Сад Перекрёстков",
            AbodeDevotionLevel = 12,
            AbodeDevotionTier = "alienated",
            Restlessness = 84,
            MigrationState = "ready_to_transfer",
            TransferMode = GuardianAbodeResidentState.TransferModeAcceptedTransfer,
            CreatedAtTurn = 41,
            CreatedAtUtc = "2026-04-16T04:41:00Z"
        });
        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        GuardianAbodeResidentRequestState.ClearTransferRequests(_fs);
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = Array.Empty<object>(),
            transferReceipts = new object[]
            {
                new
                {
                    requestId = "resident_transfer_req_2",
                    residentId = "resident_alpha_1",
                    residentName = "Лиора",
                    sourceGuardianId = "guardian_alpha",
                    sourceGuardianName = "Азалия",
                    sourceAbodeId = "abode_alpha",
                    sourceAbodeName = "Лазурная Обитель",
                    targetGuardianId = "guardian_beta",
                    targetGuardianName = "Мириэль",
                    targetAbodeId = "abode_beta",
                    targetAbodeName = "Сад Перекрёстков",
                    status = "accepted",
                    transferMode = "accepted_transfer",
                    departureHistoryEntryId = "resident_transfer_depart_1",
                    arrivalHistoryEntryId = "resident_transfer_arrive_1",
                    resolvedAtTurn = 42,
                    resolvedAtUtc = "2026-04-16T04:42:00Z"
                }
            },
            historyLog = Array.Empty<object>(),
            interactionReceipts = Array.Empty<object>(),
            rosterReceipts = Array.Empty<object>(),
            thoughtJournal = Array.Empty<object>(),
            interactionLog = Array.Empty<object>()
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        Assert.DoesNotContain(notifications, notification => string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentTransferPending, StringComparison.OrdinalIgnoreCase));
        var acceptedNotification = Assert.Single(notifications);
        Assert.Equal(AfterlifeNotificationState.TypeAbodeResidentTransferAccepted, acceptedNotification.NotificationType);
        Assert.Contains("принят", acceptedNotification.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnsureHealthyAsync_PreservesReadNotificationHistory()
    {
        var notifications = Enumerable.Range(0, 105)
            .Select(index => new
            {
                notificationId = $"read_{index}",
                notificationType = "guardian_trade_inventory_ready",
                requestId = $"req_{index}",
                status = "read",
                guardianId = "guardian_alpha",
                guardianName = "Азалия",
                archiveId = "",
                archiveTitle = "",
                targetProjectId = "",
                targetProjectName = "",
                summary = $"Read notification {index}",
                createdAtTurn = index,
                createdAtUtc = $"2026-03-26T00:{index % 60:00}:00Z"
            })
            .Concat(new[]
            {
                new
                {
                    notificationId = "unread_latest",
                    notificationType = "archive_consultation_accepted",
                    requestId = "req_unread",
                    status = "unread",
                    guardianId = "guardian_alpha",
                    guardianName = "Азалия",
                    archiveId = "archive_1",
                    archiveTitle = "Тень старого закона",
                    targetProjectId = "",
                    targetProjectName = "",
                    summary = "Unread notification",
                    createdAtTurn = 999,
                    createdAtUtc = "2026-03-26T23:59:00Z"
                }
            })
            .ToArray();

        await WriteJsonAsync(AfterlifeNotificationState.NotificationsPath, new { notifications });

        await AfterlifeNotificationState.EnsureHealthyAsync(_fs);

        var result = await AfterlifeNotificationState.ReadAsync(_fs);
        Assert.Equal(106, result.Count);
        Assert.Contains(result, entry => string.Equals(entry.NotificationId, "unread_latest", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result, entry => string.Equals(entry.NotificationId, "read_104", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result, entry => string.Equals(entry.NotificationId, "read_0", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SyncFromCurrentStateAsync_ResidentQuestNotification_PersistsExactResidentSnapshot()
    {
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    roleLabel = "Вестница",
                    bondLevel = 64,
                    bondTier = "trusted",
                    abodeDevotionLevel = 61,
                    abodeDevotionTier = "attached",
                    restlessness = 12,
                    migrationState = "settled",
                    historyRevealed = true
                }
            },
            thoughtJournal = Array.Empty<object>(),
            interactionLog = Array.Empty<object>(),
            historyLog = Array.Empty<object>(),
            transferReceipts = Array.Empty<object>(),
            interactionReceipts = Array.Empty<object>(),
            rosterReceipts = Array.Empty<object>()
        });
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guardian_alpha",
                    canonicalName = "Азалия"
                }
            }
        });
        await WriteJsonAsync("game_state/quests/soul_quests.json", new
        {
            quests = new[]
            {
                new
                {
                    questId = "quest_alpha_1",
                    title = "Просьба Лиоры",
                    status = "active",
                    relatedAfterlifeResidentId = "resident_alpha_1",
                    createdAtTurn = 18,
                    createdAtUtc = "2026-04-22T04:00:00Z"
                }
            }
        });

        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);

        var notification = Assert.Single(
            await AfterlifeNotificationState.ReadAsync(_fs),
            entry => string.Equals(entry.NotificationType, AfterlifeNotificationState.TypeAbodeResidentQuestAvailable, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("resident_alpha_1", notification.ResidentId);
        Assert.Equal("Лиора", notification.ResidentName);
    }

    private async Task WriteJsonAsync(string relativePath, object payload) =>
        await _fs.WriteFileAtomicAsync(relativePath, JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));

    private static object BuildResidentPressureState(
        string migrationState,
        int abodeDevotionLevel,
        string abodeDevotionTier,
        int restlessness,
        int turn,
        string timestamp) => new
    {
        entries = new object[]
        {
            new
            {
                residentId = "resident_alpha_1",
                guardianId = "guardian_alpha",
                abodeId = "abode_alpha",
                displayName = "Лиора",
                residentKind = "wayfaring_soul",
                originType = "traveler_soul",
                roleLabel = "Вестница",
                summary = "Тонкая душа на границе светлых троп.",
                bondLevel = 56,
                bondTier = "trusted",
                canGrantCompanionRelic = true,
                bondRewardState = "none",
                linkedSoulQuestId = "",
                grantedRelicId = "",
                historyRevealed = true,
                isPresent = true,
                personalityProfile = new
                {
                    archetype = "Road Messenger",
                    worldview = "Belonging must still feel true to remain sacred.",
                    culturalLayer = "Way shrine pilgrim traditions",
                    coreValues = new[] { "верность", "путь" },
                    personalityTraits = new object[]
                    {
                        new
                        {
                            traitName = "sensitivity_to_decline",
                            value = 7,
                            valueDescription = "замечает упадок быстро"
                        }
                    }
                },
                abodeDisposition = new
                {
                    powerSensitivity = "high",
                    migrationDisposition = "selective",
                    communalOrientation = "medium",
                    stabilityNeed = "high"
                },
                abodeDevotionLevel,
                abodeDevotionTier,
                restlessness,
                migrationState,
                mortalWorldImprint = new
                {
                    originWorldSummary = "Бывшая гонец при храме дорог.",
                    futureCompanionPrompt = "Swift wanderer",
                    bondReason = "Она помнит старые клятвы.",
                    coreTraits = new[] { "верность" },
                    archetypeHints = new[] { "courier" },
                    appearanceMotifs = new[] { "threaded cloak" }
                }
            }
        },
        thoughtJournal = new object[]
        {
            new
            {
                entryId = $"resident_pressure_{turn}",
                residentId = "resident_alpha_1",
                title = "Сдвиг в сердце Обители",
                summary = "Лиора ощущает, как её связь с Обителью меняется.",
                eventType = "abode_devotion_shift",
                consequence = "migration_pressure",
                turn,
                timestamp
            }
        }
    };

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // ignore temp cleanup failures
        }
    }
}
