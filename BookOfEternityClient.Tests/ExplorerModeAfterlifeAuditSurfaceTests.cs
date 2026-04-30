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
                ["legacyType"] = "stat_bonus",
                ["grantSource"] = "archive",
                ["applicationState"] = "pending",
                ["grantSnapshot"] = new JsonObject
                {
                    ["sourceTurn"] = 41,
                    ["sourceActionTag"] = "ARCHIVE"
                },
                ["bonus"] = new JsonObject
                {
                    ["playerStatBonus"] = 2,
                    ["characteristic"] = "Wisdom"
                }
            }
        };

        var lines = ExplorerMode.BuildMemoryGatesPreviewAuditLines(24, 120, soulRoot);
        var text = string.Join("\n", lines);

        Assert.Contains("120 -> 96", text, StringComparison.Ordinal);
        Assert.Contains("legacy_memory_old", text, StringComparison.Ordinal);
        Assert.Contains("grantSource=archive", text, StringComparison.Ordinal);
        Assert.Contains("full before payload", text, StringComparison.Ordinal);
        Assert.Contains("grantSnapshot", text, StringComparison.Ordinal);
        Assert.Contains("Canonical after payload schema", text, StringComparison.Ordinal);
        Assert.Contains("pendingMemoryLegacy.legacyId", text, StringComparison.Ordinal);
        Assert.Contains("memory_gates", text, StringComparison.Ordinal);
        Assert.Contains("exactly one mechanical bonus", text, StringComparison.Ordinal);
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

        var audit = ExplorerMode.BuildGuardianBuybackAuditNode(offer, currentFeathers: 100);

        Assert.Equal("buyback_relic_001", audit["buybackEntryId"]?.GetValue<string>());
        Assert.Equal("relic_oath_001", audit["relicId"]?.GetValue<string>());
        Assert.Equal(45, audit["priceInFeathers"]?.GetValue<int>());
        Assert.Equal(100, audit["currentFeathers"]?.GetValue<int>());
        Assert.Equal(55, audit["projectedFeathers"]?.GetValue<int>());
        Assert.Equal(30, audit["soldForPrice"]?.GetValue<int>());
        Assert.Equal(118, audit["soldAtTurn"]?.GetValue<int>());
        Assert.NotNull(audit["relicData"]?.AsObject());
    }
}
