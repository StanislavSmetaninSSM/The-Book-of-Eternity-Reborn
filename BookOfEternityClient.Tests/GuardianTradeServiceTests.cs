using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
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
              }
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
            }
          },
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_alpha"
          }
        }
        """);

        var service = new GuardianTradeService(_fs, NullLogger<GuardianTradeService>.Instance);
        var view = await service.EnsureTradeInventoryAsync("guardian_alpha", 1);
        var sameCycleView = await service.EnsureTradeInventoryAsync("guardian_alpha", 1);
        var nextCycleView = await service.EnsureTradeInventoryAsync("guardian_alpha", 2);

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
    public async Task EnsureTradeInventoryAsync_RewritesStalePendingRequestWhenDerivedContractChanged()
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
        var view = await service.EnsureTradeInventoryAsync("guardian_alpha", 1);

        Assert.NotNull(view);
        Assert.False(view!.InventoryReady);
        Assert.True(view.InventoryRequestPending);
        Assert.True(view.InventoryRequestCreatedThisCall);

        var request = await GuardianTradeRequestState.ReadAsync(_fs);
        Assert.NotNull(request);
        Assert.NotEqual("guardian_trade_old", request!.RequestId);
        Assert.Equal(4, request.DerivedTradeSlotCount);
        Assert.Equal("0|0|0", request.ProjectBonusSignature);
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
