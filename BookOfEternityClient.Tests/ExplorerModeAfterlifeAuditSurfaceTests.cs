using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ExplorerModeAfterlifeAuditSurfaceTests
{
    [Fact]
    public void SoulImprintPreviewAuditLines_ExposeSourceAndStateEvidenceContract()
    {
        var lines = ExplorerMode.BuildSoulImprintPreviewAuditLines(120, 300);
        var text = string.Join("\n", lines);

        Assert.Contains("300 -> 180", text, StringComparison.Ordinal);
        Assert.Contains("sourceCompanionId", text, StringComparison.Ordinal);
        Assert.Contains("coreTraits", text, StringComparison.Ordinal);
        Assert.Contains("personalityMarkers", text, StringComparison.Ordinal);
        Assert.Contains("stateEvidence.imprintId", text, StringComparison.Ordinal);
        Assert.Contains("game_state/meta/soul_state.json", text, StringComparison.Ordinal);
    }

    [Fact]
    public void MemoryGatesPreviewAuditLines_ExposeExistingLegacyAndCanonicalReplacementSchema()
    {
        var soulRoot = new JsonObject
        {
            ["pendingMemoryLegacy"] = new JsonObject
            {
                ["legacyId"] = "legacy_memory_old",
                ["legacyType"] = "startingCharacteristicBonus",
                ["grantSource"] = "memoryLegacyGrant",
                ["applicationState"] = "pending",
                ["grantSnapshot"] = new JsonObject
                {
                    ["sourceTurn"] = 41,
                    ["sourceActionTag"] = "ARCHIVE",
                    ["legacyType"] = "startingCharacteristicBonus",
                    ["characteristic"] = "Wisdom",
                    ["bonus"] = 2
                },
                ["characteristic"] = "Wisdom",
                ["bonus"] = 2
            }
        };

        var lines = ExplorerMode.BuildMemoryGatesPreviewAuditLines(24, 120, soulRoot);
        var text = string.Join("\n", lines);

        Assert.Contains("120 -> 96", text, StringComparison.Ordinal);
        Assert.Contains("legacy_memory_old", text, StringComparison.Ordinal);
        Assert.Contains("grantSource=memoryLegacyGrant", text, StringComparison.Ordinal);
        Assert.Contains("full before payload", text, StringComparison.Ordinal);
        Assert.Contains("grantSnapshot", text, StringComparison.Ordinal);
        Assert.Contains("Canonical after payload schema", text, StringComparison.Ordinal);
        Assert.Contains("pendingMemoryLegacy.legacyId", text, StringComparison.Ordinal);
        Assert.Contains("startingCharacteristicBonus", text, StringComparison.Ordinal);
        Assert.Contains("startingPassiveKnowledgeSkill", text, StringComparison.Ordinal);
        Assert.Contains("memoryLegacyGrant", text, StringComparison.Ordinal);
        Assert.Contains("sourceLifeHint", text, StringComparison.Ordinal);
        Assert.Contains("group=Knowledge", text, StringComparison.Ordinal);
        Assert.Contains("playerStatBonus", text, StringComparison.Ordinal);
        Assert.Contains("bonus=2", text, StringComparison.Ordinal);
        Assert.Contains("structuredBonuses", text, StringComparison.Ordinal);
        Assert.Contains("optional source ids/context", text, StringComparison.Ordinal);
        Assert.DoesNotContain("stat_bonus", text, StringComparison.Ordinal);
        Assert.DoesNotContain("knowledge_skill", text, StringComparison.Ordinal);
        Assert.DoesNotContain("grantSource: memory_gates", text, StringComparison.Ordinal);
        Assert.DoesNotContain("source ids/context: carry sourceLifeHint", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AbodeOfferingPreviewAuditLines_ShowConsumedObjectAndPowerDelta()
    {
        var lines = ExplorerMode.BuildAbodeOfferingPreviewAuditLines(
            GuardianAbodeOfferingState.OfferingTypeSoulRelic,
            "Клинок Пепельной Памяти",
            "relic_ash_memory_001",
            "soul_relic",
            "Rare",
            currentPower: 40,
            basePowerGain: 12);
        var text = string.Join("\n", lines);

        Assert.Contains("Abode Power: 40 -> 52", text, StringComparison.Ordinal);
        Assert.Contains("baseDelta=12", text, StringComparison.Ordinal);
        Assert.Contains("finalDelta=12", text, StringComparison.Ordinal);
        Assert.Contains("relic_ash_memory_001", text, StringComparison.Ordinal);
        Assert.Contains("guardianPowerEvents audit", text, StringComparison.Ordinal);
        Assert.Contains("sourceSurface=guardianAbodeOffering", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ResidentNotificationReceiptAuditLines_ShowMatchingReceiptAndStateDelta()
    {
        using var root = JsonDocument.Parse(
            """
            {
              "interactionReceipts": [
                {
                  "requestId": "res_hist_212",
                  "residentId": "res_liora",
                  "guardianId": "guardian_azalia",
                  "abodeId": "abode_azalia",
                  "interactionType": "history",
                  "status": "accepted",
                  "responseMode": "history_revealed",
                  "historyEntryId": "hist_liora_212_previous_life",
                  "resolvedAtTurn": 212,
                  "resolvedAtUtc": "2026-04-24T10:00:00Z"
                }
              ],
              "transferReceipts": [],
              "rosterReceipts": []
            }
            """);
        var notification = new AfterlifeNotificationState.NotificationEntry
        {
            NotificationType = AfterlifeNotificationState.TypeAbodeResidentHistoryRevealed,
            RequestId = "res_hist_212",
            ResidentId = "res_liora",
            ResidentName = "Лиора",
            GuardianId = "guardian_azalia"
        };
        var resident = new JsonObject
        {
            ["residentId"] = "res_liora",
            ["guardianId"] = "guardian_azalia",
            ["abodeId"] = "abode_azalia",
            ["isPresent"] = true,
            ["bondLevel"] = 64,
            ["bondTier"] = "trusted",
            ["abodeDevotionLevel"] = 58,
            ["abodeDevotionTier"] = "attached",
            ["restlessness"] = 12,
            ["migrationState"] = "settled"
        };

        var lines = ExplorerMode.BuildResidentNotificationReceiptAuditLines(root.RootElement, notification, resident);
        var text = string.Join("\n", lines);

        Assert.Contains("requestId=[dim]res_hist_212", text, StringComparison.Ordinal);
        Assert.Contains("status=[dim]accepted", text, StringComparison.Ordinal);
        Assert.Contains("type=[dim]history", text, StringComparison.Ordinal);
        Assert.Contains("hist_liora_212_previous_life", text, StringComparison.Ordinal);
        Assert.Contains("bondLevel=[dim]64", text, StringComparison.Ordinal);
        Assert.Contains("migrationState=[dim]settled", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GuardianBuybackAuditNode_ExposesTransactionAndProjectedBalance()
    {
        var view = CreateGuardianTradeView();
        var offer = new GuardianTradeService.GuardianBuybackOffer(
            "buyback_relic_001",
            "relic_oath_001",
            "Кольцо Возврата",
            "Uncommon",
            45,
            30,
            118,
            "Реликвия, ранее проданная Хранителю.",
            new JsonObject
            {
                ["relicId"] = "relic_oath_001",
                ["name"] = "Кольцо Возврата",
                ["rarity"] = "Uncommon"
            });

        var audit = ExplorerMode.BuildGuardianBuybackAuditNode(view, offer, currentFeathers: 100);

        Assert.Equal("guardian_alpha", audit["guardianId"]?.GetValue<string>());
        Assert.Equal("Азалия", audit["guardianName"]?.GetValue<string>());
        Assert.Equal("return_7", audit["tradeCycleId"]?.GetValue<string>());
        Assert.Equal("buyback_relic_001", audit["buybackEntryId"]?.GetValue<string>());
        Assert.Equal("relic_oath_001", audit["relicId"]?.GetValue<string>());
        Assert.Equal(45, audit["priceInFeathers"]?.GetValue<int>());
        Assert.Equal(100, audit["currentFeathers"]?.GetValue<int>());
        Assert.Equal(55, audit["projectedFeathers"]?.GetValue<int>());
        Assert.Equal(30, audit["soldForPrice"]?.GetValue<int>());
        Assert.Equal(118, audit["soldAtTurn"]?.GetValue<int>());
        Assert.Equal("guardian_trade_buyback", audit["transactionKind"]?.GetValue<string>());
        Assert.Contains("guardian_alpha:return_7:buyback_relic_001", audit["transactionCorrelationId"]?.GetValue<string>() ?? string.Empty, StringComparison.Ordinal);
        Assert.NotNull(audit["relicData"]?.AsObject());
    }

    [Fact]
    public void GuardianBuyAuditNode_ExposesGuardianTradeCycleAndSlotIds()
    {
        var view = CreateGuardianTradeView();
        var offer = new GuardianTradeService.GuardianTradeOffer(
            "slot_lantern_001",
            "Фонарь Возврата",
            "Rare",
            70,
            "Светит путём к старым обетам.",
            "memory",
            false,
            new JsonObject
            {
                ["relicId"] = "relic_lantern_001",
                ["name"] = "Фонарь Возврата",
                ["rarity"] = "Rare"
            });

        var audit = ExplorerMode.BuildGuardianBuyAuditNode(view, offer, currentFeathers: 120);

        Assert.Equal("guardian_alpha", audit["guardianId"]?.GetValue<string>());
        Assert.Equal("Азалия", audit["guardianName"]?.GetValue<string>());
        Assert.Equal("return_7", audit["tradeCycleId"]?.GetValue<string>());
        Assert.Equal("slot_lantern_001", audit["slotId"]?.GetValue<string>());
        Assert.Equal("relic_lantern_001", audit["relicId"]?.GetValue<string>());
        Assert.Equal("guardian_trade_buy", audit["transactionKind"]?.GetValue<string>());
        Assert.Contains("guardian_alpha:return_7:slot_lantern_001:relic_lantern_001", audit["transactionCorrelationId"]?.GetValue<string>() ?? string.Empty, StringComparison.Ordinal);
        Assert.NotNull(audit["relicData"]?.AsObject());
    }

    [Fact]
    public void GuardianSellAuditNode_ExposesGuardianTradeCycleAndGeneratedBuybackFields()
    {
        var view = CreateGuardianTradeView();
        var offer = new GuardianTradeService.GuardianSellOffer(
            "relic_sold_001",
            "Пепельное Кольцо",
            "Uncommon",
            35,
            "Можно продать Хранителю.",
            new JsonObject
            {
                ["relicId"] = "relic_sold_001",
                ["name"] = "Пепельное Кольцо",
                ["rarity"] = "Uncommon"
            });

        var audit = ExplorerMode.BuildGuardianSellAuditNode(view, offer, currentFeathers: 10);

        Assert.Equal("guardian_alpha", audit["guardianId"]?.GetValue<string>());
        Assert.Equal("Азалия", audit["guardianName"]?.GetValue<string>());
        Assert.Equal("return_7", audit["tradeCycleId"]?.GetValue<string>());
        Assert.Equal("relic_sold_001", audit["relicId"]?.GetValue<string>());
        Assert.Equal("guardian_trade_sell", audit["transactionKind"]?.GetValue<string>());
        Assert.Contains("guardian_alpha:return_7:relic_sold_001", audit["transactionCorrelationId"]?.GetValue<string>() ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains(audit["generatedBuybackEntryFields"]!.AsArray().Select(node => node!.GetValue<string>()), value => value == "buybackEntryId");
    }

    private static GuardianTradeService.GuardianTradeView CreateGuardianTradeView() =>
        new(
            "guardian_alpha",
            "Азалия",
            "memory",
            "Память",
            72,
            "trusted",
            false,
            null,
            "return_7",
            true,
            false,
            false,
            null,
            null,
            null,
            null,
            Array.Empty<GuardianTradeService.GuardianTradeOffer>(),
            Array.Empty<GuardianTradeService.GuardianBuybackOffer>());
}
