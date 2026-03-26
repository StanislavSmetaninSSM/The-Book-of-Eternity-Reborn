using System.Text.Json;
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
            createdAtUtc = "2026-03-26T00:00:00Z"
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
        Assert.Contains("4 позиций", notification.Summary, StringComparison.OrdinalIgnoreCase);
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
