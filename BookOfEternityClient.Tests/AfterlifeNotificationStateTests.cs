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
                            description = "Бывший страж северных ворот."
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
                    npcId = "npc_manifested_guard",
                    npcName = "Кел Страж",
                    sourceCompanionRelicId = "relic_imprint_1",
                    sourceSoulImprintId = "imprint_guard_1"
                }
            }
        });

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
    public async Task EnsureHealthyAsync_TrimsOldReadNotificationsButKeepsUnread()
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
        Assert.Equal(101, result.Count);
        Assert.Contains(result, entry => string.Equals(entry.NotificationId, "unread_latest", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result, entry => string.Equals(entry.NotificationId, "read_104", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result, entry => string.Equals(entry.NotificationId, "read_0", StringComparison.OrdinalIgnoreCase));
    }

    private async Task WriteJsonAsync(string relativePath, object payload) =>
        await _fs.WriteFileAtomicAsync(relativePath, JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));

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
