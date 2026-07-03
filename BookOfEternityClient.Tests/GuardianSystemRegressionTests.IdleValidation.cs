using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GuardianSystemRegressionTests
{
    [Fact]
    public async Task ValidateGameState_AllowsGuardianTradeInventoryWithCurrentTrackerWhenIdle()
    {
        DeletePendingTurnSurfaces();

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteGuardianRawWithoutValidatedSnapshotAsync("""
        {
          "guardians": [
            {
              "guardianId": "guardian_idle_trade",
              "canonicalName": "Мири",
              "nameVariants": { "default": "Мири", "feminine": "Мири", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Мири",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Хранительница с готовой витриной реликвий в стабильном состоянии Моря Хаоса."
              },
              "manifestationHistory": [],
              "abode": { "abodeId": "abode_idle_trade", "title": "Тихая пристань" },
              "relationshipData": { "currentReputation": 0, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 35, "tier": "Слабая", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] },
              "tradeInventory": {
                "tradeCycleId": "cycle_12",
                "generatedAtUtc": "2026-03-28T00:00:00Z",
                "generationReputationTier": "neutral",
                "pricingReputationTier": "neutral",
                "effectiveRarityCeilingBonusSteps": 0,
                "upgradedTradeSlots": 0,
                "elevatedTradeSlots": 0,
                "projectBonusSignature": "0|0|0",
                "items": [
                  { "slotId": "slot_1", "priceInFeathers": 35, "soldOut": false, "rarityBonusStepsApplied": 0, "relicData": { "relicId": "relic_idle_1", "name": "Реликвия 1", "quality": "Common" } },
                  { "slotId": "slot_2", "priceInFeathers": 35, "soldOut": false, "rarityBonusStepsApplied": 0, "relicData": { "relicId": "relic_idle_2", "name": "Реликвия 2", "quality": "Common" } },
                  { "slotId": "slot_3", "priceInFeathers": 35, "soldOut": false, "rarityBonusStepsApplied": 0, "relicData": { "relicId": "relic_idle_3", "name": "Реликвия 3", "quality": "Common" } },
                  { "slotId": "slot_4", "priceInFeathers": 35, "soldOut": false, "rarityBonusStepsApplied": 0, "relicData": { "relicId": "relic_idle_4", "name": "Реликвия 4", "quality": "Common" } },
                  { "slotId": "slot_5", "priceInFeathers": 35, "soldOut": false, "rarityBonusStepsApplied": 0, "relicData": { "relicId": "relic_idle_5", "name": "Реликвия 5", "quality": "Common" } }
                ]
              }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, EmptyGuardianProjectTrackerJson);
        await _fs.WriteFileAtomicAsync(GuardianPowerEventState.JournalPath, EmptyGuardianPowerJournalJson);
        DeletePendingTurnSurfaces();

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_inventory_missing_current_tracker_authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateCrossReferences_AllowsNpcSurfacesWithoutGuardianSnapshotWhenIdle()
    {
        DeletePendingTurnSurfaces();

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "NPCsInScene": [
            {
              "NPCId": "npc_idle_broker",
              "name": "Мирна"
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_inventory.json", """
        {
          "NPCInventoryAdds": [
            {
              "NPCId": "npc_idle_broker",
              "NPCName": "Мирна",
              "item": {
                "itemId": "npc_idle_note",
                "name": "Записка Мирны"
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_npc_boundary_missing_validated_preturn_guardians_snapshot", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_npc_command_crossrefs_missing_validated_preturn_guardians_snapshot", StringComparison.OrdinalIgnoreCase));
    }

    private void DeletePendingTurnSurfaces()
    {
        _fs.DeleteFile("input/turn_request.json");
        _fs.DeleteFile("ready/turn_complete.json");
        _fs.DeleteFile("ready/turn_error.json");
        _fs.DeleteFile("game_state/control/pending_turn_snapshot.json");
        _fs.DeleteFile("game_state/control/pending_turn_snapshot.authority.json");
        var snapshotDirectory = _fs.ResolvePath("game_state/control/pending_turn_snapshot");
        if (Directory.Exists(snapshotDirectory))
            Directory.Delete(snapshotDirectory, recursive: true);
    }
}
