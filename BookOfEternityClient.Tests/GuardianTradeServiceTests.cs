using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GuardianTradeServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public GuardianTradeServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-guardian-trade-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task EnsureTradeInventoryAsync_UsesExplicitPersistedInventoryWithoutDomainGeneration()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "domain": "Порог Сна",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 110, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 10, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель" },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] },
              "tradeInventory": {
                "tradeCycleId": "return_1",
                "generatedAtUtc": "2026-03-26T00:00:00Z",
                "generationReputationTier": "Friendly",
                "pricingReputationTier": "Friendly",
                "projectBonusSignature": "0|0|0",
                "upgradedTradeSlots": 0,
                "elevatedTradeSlots": 0,
                "effectiveRarityCeilingBonusSteps": 0,
                "items": [
                  {
                    "slotId": "trade_1",
                    "priceInFeathers": 30,
                    "domainTag": "Сны и Переходы",
                    "soldOut": false,
                    "rarityBonusStepsApplied": 0,
                    "relicData": {
                      "relicId": "relic_1",
                      "name": "Печать Сумеречного Порога",
                      "rarity": "Common",
                      "quality": "Common",
                      "description": "Тестовая явная витрина."
                    }
                  },
                  {
                    "slotId": "trade_2",
                    "priceInFeathers": 70,
                    "domainTag": "Сны и Переходы",
                    "soldOut": false,
                    "rarityBonusStepsApplied": 0,
                    "relicData": {
                      "relicId": "relic_2",
                      "name": "Колье Шёпота",
                      "rarity": "Uncommon",
                      "quality": "Uncommon",
                      "description": "Тестовая явная витрина."
                    }
                  },
                  {
                    "slotId": "trade_3",
                    "priceInFeathers": 140,
                    "domainTag": "Сны и Переходы",
                    "soldOut": false,
                    "rarityBonusStepsApplied": 0,
                    "relicData": {
                      "relicId": "relic_3",
                      "name": "Знак Грёзы",
                      "rarity": "Rare",
                      "quality": "Rare",
                      "description": "Тестовая явная витрина."
                    }
                  },
                  {
                    "slotId": "trade_4",
                    "priceInFeathers": 140,
                    "domainTag": "Сны и Переходы",
                    "soldOut": false,
                    "rarityBonusStepsApplied": 0,
                    "relicData": {
                      "relicId": "relic_4",
                      "name": "Плащ Межпорога",
                      "rarity": "Rare",
                      "quality": "Rare",
                      "description": "Тестовая явная витрина."
                    }
                  }
                ]
              },
              "tradeInventoryReceipts": [
                {
                  "requestId": "guardian_trade_receipt_1",
                  "guardianId": "guardian_alpha",
                  "guardianName": "Азалия",
                  "abodeId": "abode_alpha",
                  "tradeCycleId": "return_1",
                  "status": "ready",
                  "itemCount": 4,
                  "resolvedAtTurn": 7,
                  "resolvedAtUtc": "2026-03-26T00:01:00Z"
                }
              ]
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_alpha",
            "canonicalName": "Азалия",
            "domain": "Порог Сна",
            "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
            "manifestation": {
                "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Тестовая форма."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": 110, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 10, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель" },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] },
            "tradeInventory": {
              "tradeCycleId": "return_1",
              "generatedAtUtc": "2026-03-26T00:00:00Z",
              "generationReputationTier": "Friendly",
              "pricingReputationTier": "Friendly",
              "projectBonusSignature": "0|0|0",
              "upgradedTradeSlots": 0,
              "elevatedTradeSlots": 0,
              "effectiveRarityCeilingBonusSteps": 0,
              "items": [
                {
                  "slotId": "trade_1",
                  "priceInFeathers": 30,
                  "domainTag": "Сны и Переходы",
                  "soldOut": false,
                  "rarityBonusStepsApplied": 0,
                  "relicData": {
                    "relicId": "relic_1",
                    "name": "Печать Сумеречного Порога",
                    "rarity": "Common",
                    "quality": "Common",
                    "description": "Тестовая явная витрина."
                  }
                },
                {
                  "slotId": "trade_2",
                  "priceInFeathers": 70,
                  "domainTag": "Сны и Переходы",
                  "soldOut": false,
                  "rarityBonusStepsApplied": 0,
                  "relicData": {
                    "relicId": "relic_2",
                    "name": "Колье Шёпота",
                    "rarity": "Uncommon",
                    "quality": "Uncommon",
                    "description": "Тестовая явная витрина."
                  }
                },
                {
                  "slotId": "trade_3",
                  "priceInFeathers": 140,
                  "domainTag": "Сны и Переходы",
                  "soldOut": false,
                  "rarityBonusStepsApplied": 0,
                  "relicData": {
                    "relicId": "relic_3",
                    "name": "Знак Грёзы",
                    "rarity": "Rare",
                    "quality": "Rare",
                    "description": "Тестовая явная витрина."
                  }
                },
                {
                  "slotId": "trade_4",
                  "priceInFeathers": 140,
                  "domainTag": "Сны и Переходы",
                  "soldOut": false,
                  "rarityBonusStepsApplied": 0,
                  "relicData": {
                    "relicId": "relic_4",
                    "name": "Плащ Межпорога",
                    "rarity": "Rare",
                    "quality": "Rare",
                    "description": "Тестовая явная витрина."
                  }
                }
              ]
            },
            "tradeInventoryReceipts": [
              {
                "requestId": "guardian_trade_receipt_1",
                "guardianId": "guardian_alpha",
                "guardianName": "Азалия",
                "abodeId": "abode_alpha",
                "tradeCycleId": "return_1",
                "status": "ready",
                "itemCount": 4,
                "resolvedAtTurn": 7,
                "resolvedAtUtc": "2026-03-26T00:01:00Z"
              }
            ]
          },
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_alpha"
          }
        }
        """);

        var service = new GuardianTradeService(_fs, NullLogger<GuardianTradeService>.Instance);
        var view = await service.EnsureTradeInventoryAsync("guardian_alpha", 1, currentTurn: 7);
        var sameCycleView = await service.EnsureTradeInventoryAsync("guardian_alpha", 1, currentTurn: 7);
        var nextCycleView = await service.EnsureTradeInventoryAsync("guardian_alpha", 2, currentTurn: 8);

        Assert.NotNull(view);
        Assert.Equal(4, view!.Offers.Count);
        Assert.Equal("Порог Сна", view.Domain);
        Assert.Equal("Сны и Переходы", view.Offers[0].DomainTag);
        Assert.NotNull(sameCycleView);
        Assert.Equal(4, sameCycleView!.Offers.Count);
        Assert.NotNull(nextCycleView);
        Assert.False(nextCycleView!.TradeBlocked);
        Assert.False(nextCycleView.InventoryReady);
        Assert.True(nextCycleView.InventoryRequestPending);
        Assert.Empty(nextCycleView.Offers);
    }

    [Fact]
    public async Task EnsureTradeInventoryAsync_MaterializedInventoryWithoutReceipt_RemainsPending()
    {
        await SeedMinimalGuardianTradeStateAsync(includeTradeInventory: true, includeTradeReceipt: false, includeBuybackEntry: false);

        var service = new GuardianTradeService(_fs, NullLogger<GuardianTradeService>.Instance);
        var view = await service.EnsureTradeInventoryAsync("guardian_alpha", 1, currentTurn: 7);

        Assert.NotNull(view);
        Assert.False(view!.InventoryReady);
        Assert.True(view.InventoryRequestPending);
        Assert.Empty(view.Offers);

        var pendingRequest = await GuardianTradeRequestState.ReadAsync(_fs);
        Assert.NotNull(pendingRequest);
        Assert.Equal("guardian_alpha", pendingRequest!.GuardianId);
        Assert.Equal("return_1", pendingRequest.ReturnCycleId);
    }

    [Fact]
    public async Task EnsureTradeInventoryAsync_BlocksForeignLivePendingRequestWhenDerivedContractChanged()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "domain": "Порог Сна",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 110, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 10, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель" },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_alpha",
            "canonicalName": "Азалия",
            "domain": "Порог Сна",
            "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Тестовая форма."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": 110, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 10, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель" },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          },
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_alpha"
          }
        }
        """);

        await _fs.WriteFileAtomicAsync(
            GuardianTradeRequestState.PendingRequestPath,
            """
            {
              "requestId": "guardian_trade_old",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "abodeId": "abode_alpha",
              "returnCycleId": "return_1",
              "currentReputation": 110,
              "derivedTradeSlotCount": 2,
              "effectiveRarityCeilingBonusSteps": 0,
              "projectBonusSignature": "9|9|9",
              "createdAtUtc": "2026-03-26T00:00:00Z"
            }
            """);

        var service = new GuardianTradeService(_fs, NullLogger<GuardianTradeService>.Instance);
        var view = await service.EnsureTradeInventoryAsync("guardian_alpha", 1, currentTurn: 7);

        Assert.NotNull(view);
        Assert.True(view!.TradeBlocked);
        Assert.False(view.InventoryReady);
        Assert.True(view.InventoryRequestPending);
        Assert.False(view.InventoryRequestCreatedThisCall);
        Assert.Contains("другой живой торговый контракт", view.BlockReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        var request = await GuardianTradeRequestState.ReadAsync(_fs);
        Assert.NotNull(request);
        Assert.Equal("guardian_trade_old", request!.RequestId);
        Assert.Equal(2, request.DerivedTradeSlotCount);
        Assert.Equal("9|9|9", request.ProjectBonusSignature);
    }

    [Fact]
    public async Task EnsureTradeInventoryAsync_StaleReadyReceiptWithDifferentRequestId_DoesNotUnlockInventoryOrClearPending()
    {
        await SeedMinimalGuardianTradeStateAsync(includeTradeInventory: true, includeTradeReceipt: true, includeBuybackEntry: false);
        await _fs.WriteFileAtomicAsync(
            GuardianTradeRequestState.PendingRequestPath,
            """
            {
              "requestId": "guardian_trade_current",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "abodeId": "abode_alpha",
              "returnCycleId": "return_1",
              "currentReputation": 120,
              "derivedTradeSlotCount": 1,
              "effectiveRarityCeilingBonusSteps": 0,
              "projectBonusSignature": "0|0|0",
              "createdAtTurn": 7,
              "createdAtUtc": "2026-03-26T00:00:00Z"
            }
            """);

        var service = new GuardianTradeService(_fs, NullLogger<GuardianTradeService>.Instance);
        var view = await service.EnsureTradeInventoryAsync("guardian_alpha", 1, currentTurn: 7);

        Assert.NotNull(view);
        Assert.False(view!.InventoryReady);
        Assert.True(view.InventoryRequestPending);
        Assert.False(view.InventoryRequestCreatedThisCall);

        var request = await GuardianTradeRequestState.ReadAsync(_fs);
        Assert.NotNull(request);
        Assert.Equal("guardian_trade_current", request!.RequestId);
    }

    [Fact]
    public async Task SellAsync_SellableRelic_CreatesGuardianBuybackEntryAndAwardsFeathers()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "domain": "Порог Сна",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 120, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 10, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель" },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_alpha",
            "canonicalName": "Азалия",
            "domain": "Порог Сна",
            "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Тестовая форма."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": 120, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 10, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель" },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          },
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_alpha"
          }
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "inkFeathers": { "current": 100 },
          "soulRelics": {
            "equipped": [],
            "stored": [
              {
                "relicId": "relic_sell_001",
                "name": "Реликвия для продажи",
                "rarity": "Rare",
                "description": "Подходит для теста продажи."
              }
            ]
          }
        }
        """);

        var service = new GuardianTradeService(_fs, NullLogger<GuardianTradeService>.Instance);
        var result = await service.SellAsync("guardian_alpha", "relic_sell_001", currentTurn: 14);

        Assert.True(result.Success);

        var guardiansRaw = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        var soulRaw = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.DoesNotContain("Реликвия для продажи", soulRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"buybackRelics\"", guardiansRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"status\": \"available\"", guardiansRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"soldByPlayerAtTurn\": 14", guardiansRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("\"current\": 100", soulRaw ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuyBackAsync_AvailableEntry_ReturnsRelicAndMarksEntryRebought()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "domain": "Порог Сна",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 120, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 10, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель" },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] },
              "buybackRelics": [
                {
                  "buybackEntryId": "guardian_buyback_001",
                  "guardianId": "guardian_alpha",
                  "guardianName": "Азалия",
                  "relicId": "relic_buyback_001",
                  "relicData": {
                    "relicId": "relic_buyback_001",
                    "name": "Отзвук Зеркального Двора",
                    "rarity": "Rare",
                    "description": "Ранее проданная реликвия."
                  },
                  "soldByPlayerAtTurn": 11,
                  "soldByPlayerAtUtc": "2026-03-26T00:10:00Z",
                  "soldForPrice": 60,
                  "buybackPrice": 60,
                  "acquiredFromPlayer": true,
                  "status": "available"
                }
              ]
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_alpha",
            "canonicalName": "Азалия",
            "domain": "Порог Сна",
            "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Тестовая форма."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": 120, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 10, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель" },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] },
            "buybackRelics": [
              {
                "buybackEntryId": "guardian_buyback_001",
                "guardianId": "guardian_alpha",
                "guardianName": "Азалия",
                "relicId": "relic_buyback_001",
                "relicData": {
                  "relicId": "relic_buyback_001",
                  "name": "Отзвук Зеркального Двора",
                  "rarity": "Rare",
                  "description": "Ранее проданная реликвия."
                },
                "soldByPlayerAtTurn": 11,
                "soldByPlayerAtUtc": "2026-03-26T00:10:00Z",
                "soldForPrice": 60,
                "buybackPrice": 60,
                "acquiredFromPlayer": true,
                "status": "available"
              }
            ]
          },
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_alpha"
          }
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "inkFeathers": { "current": 100 },
          "soulRelics": {
            "equipped": [],
            "stored": []
          }
        }
        """);

        var service = new GuardianTradeService(_fs, NullLogger<GuardianTradeService>.Instance);
        var result = await service.BuyBackAsync("guardian_alpha", "guardian_buyback_001", currentTurn: 15);

        Assert.True(result.Success);

        var guardiansRaw = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        var soulRaw = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.Contains("Отзвук Зеркального Двора", soulRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"status\": \"rebought\"", guardiansRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"reboughtAtTurn\": 15", guardiansRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("\"current\": 100", soulRaw ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SellAsync_WithoutValidCurrentTurn_FailsWithoutMutatingState()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "domain": "Порог Сна",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 120, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 10, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель" },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_alpha",
            "canonicalName": "Азалия",
            "domain": "Порог Сна",
            "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Тестовая форма."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": 120, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 10, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель" },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          },
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_alpha"
          }
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "inkFeathers": { "current": 100 },
          "soulRelics": {
            "equipped": [],
            "stored": [
              {
                "relicId": "relic_sell_001",
                "name": "Реликвия для продажи",
                "rarity": "Rare",
                "description": "Подходит для теста продажи."
              }
            ]
          }
        }
        """);

        var service = new GuardianTradeService(_fs, NullLogger<GuardianTradeService>.Instance);
        var beforeGuardians = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        var result = await service.SellAsync("guardian_alpha", "relic_sell_001", currentTurn: 0);

        Assert.False(result.Success);
        Assert.False(result.StateChanged);
        Assert.Contains("номер хода", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeGuardians, await _fs.ReadFileAsync("game_state/meta/guardians.json"));
        Assert.Equal(beforeSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
    }

    [Fact]
    public async Task BuyBackAsync_WithoutValidCurrentTurn_FailsWithoutMutatingState()
    {
        await SeedMinimalGuardianTradeStateAsync(includeTradeInventory: false, includeTradeReceipt: false, includeBuybackEntry: true);

        var service = new GuardianTradeService(_fs, NullLogger<GuardianTradeService>.Instance);
        var beforeGuardians = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        var result = await service.BuyBackAsync("guardian_alpha", "guardian_buyback_001", currentTurn: 0);

        Assert.False(result.Success);
        Assert.False(result.StateChanged);
        Assert.Contains("номер хода", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeGuardians, await _fs.ReadFileAsync("game_state/meta/guardians.json"));
        Assert.Equal(beforeSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
    }

    [Fact]
    public async Task BuyAsync_WithoutValidCurrentTurn_FailsWithoutMutatingState()
    {
        await SeedMinimalGuardianTradeStateAsync(includeTradeInventory: true, includeTradeReceipt: true, includeBuybackEntry: false);

        var service = new GuardianTradeService(_fs, NullLogger<GuardianTradeService>.Instance);
        var beforeGuardians = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        var result = await service.BuyAsync("guardian_alpha", "trade_1", currentIncarnation: 1, currentTurn: 0);

        Assert.False(result.Success);
        Assert.False(result.StateChanged);
        Assert.Contains("номер хода", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeGuardians, await _fs.ReadFileAsync("game_state/meta/guardians.json"));
        Assert.Equal(beforeSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
    }

    [Fact]
    public async Task BuyAsync_DuplicateSlotIds_FailsWithoutMutatingState()
    {
        await SeedMinimalGuardianTradeStateAsync(includeTradeInventory: true, includeTradeReceipt: true, includeBuybackEntry: false);
        await DuplicateFirstGuardianTradeSlotAsync();

        var service = new GuardianTradeService(_fs, NullLogger<GuardianTradeService>.Instance);
        var beforeGuardians = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        var result = await service.BuyAsync("guardian_alpha", "trade_1", currentIncarnation: 1, currentTurn: 13);

        Assert.False(result.Success);
        Assert.False(result.StateChanged);
        Assert.Contains("duplicate slotId", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeGuardians, await _fs.ReadFileAsync("game_state/meta/guardians.json"));
        Assert.Equal(beforeSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
    }

    [Fact]
    public void InventoryMatchesRequestContract_DuplicateSlotIds_Fails()
    {
        var inventory = new JsonObject
        {
            ["tradeCycleId"] = "return_1",
            ["generatedAtUtc"] = "2026-03-26T00:00:00Z",
            ["generationReputationTier"] = "Friendly",
            ["pricingReputationTier"] = "Friendly",
            ["projectBonusSignature"] = "0|0|0",
            ["upgradedTradeSlots"] = 0,
            ["elevatedTradeSlots"] = 0,
            ["effectiveRarityCeilingBonusSteps"] = 0,
            ["items"] = new JsonArray(
                CreateGuardianTradeSlot("trade_1", "relic_1"),
                CreateGuardianTradeSlot("trade_1", "relic_2"))
        };
        var request = new GuardianTradeRequestState.PendingGuardianTradeRequest
        {
            GuardianId = "guardian_alpha",
            ReturnCycleId = "return_1",
            CurrentReputation = 120,
            DerivedTradeSlotCount = 2,
            EffectiveRarityCeilingBonusSteps = 0,
            ProjectBonusSignature = "0|0|0"
        };

        Assert.False(GuardianTradeRequestState.InventoryMatchesRequestContract(inventory, request));
    }

    [Fact]
    public async Task ValidateGameStateAsync_GuardianTradeInventoryDuplicateSlotIds_Fails()
    {
        await SeedMinimalGuardianTradeStateAsync(includeTradeInventory: true, includeTradeReceipt: true, includeBuybackEntry: false);
        await DuplicateFirstGuardianTradeSlotAsync();

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "guardian_trade_inventory_duplicate_slot_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BuyAsync_StaleInventoryRequest_UsesRealCurrentTurnInPendingRequest()
    {
        await SeedMinimalGuardianTradeStateAsync(includeTradeInventory: false, includeTradeReceipt: false, includeBuybackEntry: false);

        var service = new GuardianTradeService(_fs, NullLogger<GuardianTradeService>.Instance);
        var result = await service.BuyAsync("guardian_alpha", "trade_1", currentIncarnation: 1, currentTurn: 13);

        Assert.False(result.Success);
        Assert.False(result.StateChanged);

        var request = await GuardianTradeRequestState.ReadAsync(_fs);
        Assert.NotNull(request);
        Assert.Equal(13, request!.CreatedAtTurn);
        Assert.Equal("guardian_alpha", request.GuardianId);
        Assert.Equal("return_1", request.ReturnCycleId);
    }

    [Fact]
    public async Task SellAsync_StripsConflictingInkFeatherChangesButPreservesUnrelatedPendingMetaWork()
    {
        await SeedMinimalGuardianTradeStateAsync(includeTradeInventory: false, includeTradeReceipt: false, includeBuybackEntry: false);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "inkFeathers": { "current": 100 },
          "crossIncarnationData": {
            "legacyThreadId": "thread_alpha"
          },
          "metaStateUpdates": {
            "inkFeatherChanges": {
              "add": 75,
              "spend": 3
            },
            "soulRelicOperations": {
              "removeRelic": {
                "relicId": "relic_sell_001"
              },
              "addRelic": {
                "relicId": "relic_keep"
              }
            },
            "memoryLegacyGrant": {
              "legacyId": "legacy_alpha",
              "legacyType": "startingCharacteristicBonus",
              "sourceLifeHint": "life_001",
              "characteristic": "strength",
              "bonus": 2
            }
          },
          "afterlifeArchiveUpdates": [],
          "archiveActionResolutions": [],
          "soulRelics": {
            "equipped": [],
            "stored": [
              {
                "relicId": "relic_sell_001",
                "name": "Реликвия для продажи",
                "rarity": "Rare",
                "description": "Подходит для теста продажи."
              }
            ]
          }
        }
        """);

        var service = new GuardianTradeService(_fs, NullLogger<GuardianTradeService>.Instance);
        var result = await service.SellAsync("guardian_alpha", "relic_sell_001", currentTurn: 13);

        Assert.True(result.Success);

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        Assert.False(soulDoc.RootElement.TryGetProperty("crossIncarnationData", out _));
        var metaStateUpdates = soulDoc.RootElement.GetProperty("metaStateUpdates");
        Assert.False(metaStateUpdates.TryGetProperty("inkFeatherChanges", out _));
        var soulRelicOperations = metaStateUpdates.GetProperty("soulRelicOperations");
        Assert.False(soulRelicOperations.TryGetProperty("removeRelic", out _));
        Assert.True(soulRelicOperations.TryGetProperty("addRelic", out var addRelic));
        Assert.Equal("relic_keep", addRelic.GetProperty("relicId").GetString());
        Assert.True(metaStateUpdates.TryGetProperty("memoryLegacyGrant", out _));
        Assert.True(soulDoc.RootElement.TryGetProperty("afterlifeArchiveUpdates", out _));
        Assert.True(soulDoc.RootElement.TryGetProperty("archiveActionResolutions", out _));
    }

    [Fact]
    public async Task SellAsync_MalformedInkFeatherChanges_FailClosedWithoutMaskingPendingCommand()
    {
        await SeedMinimalGuardianTradeStateAsync(includeTradeInventory: false, includeTradeReceipt: false, includeBuybackEntry: false);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "inkFeathers": { "current": 100 },
          "metaStateUpdates": {
            "inkFeatherChanges": {
              "add": "75"
            }
          },
          "soulRelics": {
            "equipped": [],
            "stored": [
              {
                "relicId": "relic_sell_001",
                "name": "Реликвия для продажи",
                "rarity": "Rare",
                "description": "Подходит для теста продажи."
              }
            ]
          }
        }
        """);

        var service = new GuardianTradeService(_fs, NullLogger<GuardianTradeService>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SellAsync("guardian_alpha", "relic_sell_001", currentTurn: 13));
        Assert.Contains("metaStateUpdates.inkFeatherChanges", exception.Message, StringComparison.Ordinal);

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        var metaStateUpdates = soulDoc.RootElement.GetProperty("metaStateUpdates");
        Assert.Equal("75", metaStateUpdates.GetProperty("inkFeatherChanges").GetProperty("add").GetString());
        Assert.Equal("relic_sell_001", soulDoc.RootElement.GetProperty("soulRelics").GetProperty("stored")[0].GetProperty("relicId").GetString());
    }

    [Fact]
    public async Task SellAsync_MalformedTopLevelMetaStateUpdates_FailClosedWithoutMaskingPendingCommand()
    {
        await SeedMinimalGuardianTradeStateAsync(includeTradeInventory: false, includeTradeReceipt: false, includeBuybackEntry: false);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "inkFeathers": { "current": 100 },
          "metaStateUpdates": [],
          "soulRelics": {
            "equipped": [],
            "stored": [
              {
                "relicId": "relic_sell_001",
                "name": "Реликвия для продажи",
                "rarity": "Rare",
                "description": "Подходит для теста продажи."
              }
            ]
          }
        }
        """);

        var service = new GuardianTradeService(_fs, NullLogger<GuardianTradeService>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SellAsync("guardian_alpha", "relic_sell_001", currentTurn: 13));
        Assert.Contains("current metaStateUpdates", exception.Message, StringComparison.Ordinal);

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        Assert.Equal(JsonValueKind.Array, soulDoc.RootElement.GetProperty("metaStateUpdates").ValueKind);
        Assert.Equal("relic_sell_001", soulDoc.RootElement.GetProperty("soulRelics").GetProperty("stored")[0].GetProperty("relicId").GetString());
    }

    [Fact]
    public async Task SellAsync_MalformedCanonicalInkFeathersRoot_FailClosedWithoutRepairingCurrentState()
    {
        await SeedMinimalGuardianTradeStateAsync(includeTradeInventory: false, includeTradeReceipt: false, includeBuybackEntry: false);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "inkFeathers": {
            "current": 100,
            "foo": 99
          },
          "soulRelics": {
            "equipped": [],
            "stored": [
              {
                "relicId": "relic_sell_001",
                "name": "Реликвия для продажи",
                "rarity": "Rare",
                "description": "Подходит для теста продажи."
              }
            ]
          }
        }
        """);

        var service = new GuardianTradeService(_fs, NullLogger<GuardianTradeService>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SellAsync("guardian_alpha", "relic_sell_001", currentTurn: 13));
        Assert.Contains("current inkFeathers", exception.Message, StringComparison.Ordinal);

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        Assert.Equal(99, soulDoc.RootElement.GetProperty("inkFeathers").GetProperty("foo").GetInt32());
        Assert.Equal("relic_sell_001", soulDoc.RootElement.GetProperty("soulRelics").GetProperty("stored")[0].GetProperty("relicId").GetString());
    }

    [Fact]
    public async Task BuyBackAsync_StripsConflictingInkFeatherChangesButPreservesUnrelatedPendingMetaWork()
    {
        await SeedMinimalGuardianTradeStateAsync(includeTradeInventory: false, includeTradeReceipt: false, includeBuybackEntry: true);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "inkFeathers": { "current": 100 },
          "metaStateUpdates": {
            "inkFeatherChanges": {
              "add": 5,
              "spend": 61
            },
            "soulRelicOperations": {
              "addRelic": {
                "relicId": "relic_buyback_001"
              },
              "removeRelic": {
                "relicId": "relic_keep"
              }
            },
            "memoryLegacyGrant": {
              "legacyId": "legacy_alpha",
              "legacyType": "startingCharacteristicBonus",
              "sourceLifeHint": "life_001",
              "characteristic": "strength",
              "bonus": 2
            }
          },
          "afterlifeArchiveUpdates": [],
          "archiveActionResolutions": [],
          "soulRelics": {
            "equipped": [],
            "stored": []
          }
        }
        """);

        var service = new GuardianTradeService(_fs, NullLogger<GuardianTradeService>.Instance);
        var result = await service.BuyBackAsync("guardian_alpha", "guardian_buyback_001", currentTurn: 13);

        Assert.True(result.Success);

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        var metaStateUpdates = soulDoc.RootElement.GetProperty("metaStateUpdates");
        Assert.False(metaStateUpdates.TryGetProperty("inkFeatherChanges", out _));
        var soulRelicOperations = metaStateUpdates.GetProperty("soulRelicOperations");
        Assert.False(soulRelicOperations.TryGetProperty("addRelic", out _));
        Assert.True(soulRelicOperations.TryGetProperty("removeRelic", out var removeRelic));
        Assert.Equal("relic_keep", removeRelic.GetProperty("relicId").GetString());
        Assert.True(metaStateUpdates.TryGetProperty("memoryLegacyGrant", out _));
    }

    private async Task DuplicateFirstGuardianTradeSlotAsync()
    {
        var guardiansRoot = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/guardians.json"))!)!.AsObject();
        DuplicateFirstSlot(guardiansRoot["guardians"]!.AsArray()[0]!.AsObject());
        DuplicateFirstSlot(guardiansRoot["activeGuardian"]!.AsObject());
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", guardiansRoot.ToJsonString());
    }

    private static void DuplicateFirstSlot(JsonObject guardian)
    {
        var items = guardian["tradeInventory"]!["items"]!.AsArray();
        items.Add(items[0]!.DeepClone());
    }

    private static JsonObject CreateGuardianTradeSlot(string slotId, string relicId) => new()
    {
        ["slotId"] = slotId,
        ["priceInFeathers"] = 30,
        ["domainTag"] = "Сны и Переходы",
        ["soldOut"] = false,
        ["rarityBonusStepsApplied"] = 0,
        ["relicData"] = new JsonObject
        {
            ["relicId"] = relicId,
            ["name"] = $"Реликвия {relicId}",
            ["rarity"] = "Common",
            ["quality"] = "Common",
            ["description"] = "Тестовая реликвия."
        }
    };

    private async Task SeedMinimalGuardianTradeStateAsync(bool includeTradeInventory, bool includeTradeReceipt, bool includeBuybackEntry)
    {
        var tradeInventoryJson = includeTradeInventory
            ? """
            ,
            "tradeInventory": {
              "tradeCycleId": "return_1",
              "generatedAtUtc": "2026-03-26T00:00:00Z",
              "generationReputationTier": "Friendly",
              "pricingReputationTier": "Friendly",
              "projectBonusSignature": "0|0|0",
              "upgradedTradeSlots": 0,
              "elevatedTradeSlots": 0,
              "effectiveRarityCeilingBonusSteps": 0,
              "items": [
                {
                  "slotId": "trade_1",
                  "priceInFeathers": 30,
                  "domainTag": "Сны и Переходы",
                  "soldOut": false,
                  "rarityBonusStepsApplied": 0,
                  "relicData": {
                    "relicId": "relic_1",
                    "name": "Печать Сумеречного Порога",
                    "rarity": "Common",
                    "quality": "Common",
                    "description": "Тестовая явная витрина."
                  }
                }
              ]
            }
            """
            : "";

        var receiptJson = includeTradeReceipt
            ? """
            ,
            "tradeInventoryReceipts": [
              {
                "requestId": "guardian_trade_req_001",
                "guardianId": "guardian_alpha",
                "guardianName": "Азалия",
                "abodeId": "abode_alpha",
                "tradeCycleId": "return_1",
                "status": "ready",
                "itemCount": 1,
                "resolvedAtTurn": 7,
                "resolvedAtUtc": "2026-03-26T00:10:00Z"
              }
            ]
            """
            : "";

        var buybackJson = includeBuybackEntry
            ? """
            ,
            "buybackRelics": [
              {
                "buybackEntryId": "guardian_buyback_001",
                "guardianId": "guardian_alpha",
                "guardianName": "Азалия",
                "relicId": "relic_buyback_001",
                "relicData": {
                  "relicId": "relic_buyback_001",
                  "name": "Отзвук Зеркального Двора",
                  "rarity": "Rare",
                  "description": "Ранее проданная реликвия."
                },
                "soldByPlayerAtTurn": 11,
                "soldByPlayerAtUtc": "2026-03-26T00:10:00Z",
                "soldForPrice": 60,
                "buybackPrice": 60,
                "acquiredFromPlayer": true,
                "status": "available"
              }
            ]
            """
            : "";

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", $$"""
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "domain": "Порог Сна",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 120, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 10, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель" },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }{{tradeInventoryJson}}{{receiptJson}}{{buybackJson}}
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_alpha",
            "canonicalName": "Азалия",
            "domain": "Порог Сна",
            "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Тестовая форма."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": 120, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 10, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель" },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }{{tradeInventoryJson}}{{receiptJson}}{{buybackJson}}
          },
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_alpha"
          }
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "inkFeathers": { "current": 100 },
          "soulRelics": {
            "equipped": [],
            "stored": []
          }
        }
        """);
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
