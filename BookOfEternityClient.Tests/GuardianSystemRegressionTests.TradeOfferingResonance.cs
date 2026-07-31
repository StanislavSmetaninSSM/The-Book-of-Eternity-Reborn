using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GuardianSystemRegressionTests
{
    [Fact]
    public async Task GuardianTradeResolution_AllowsExactMaterializedInventoryAndReceiptInStrictAuthority()
    {
        const string preTurnGuardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Authority guardian before trade resolution."
              },
              "manifestationHistory": [],
              "mood": { "current": "focused", "intensity": 40, "reason": "Authority baseline before trade.", "since": 10 },
              "abode": { "abodeId": "abode_alpha", "title": "Обитель Азалии" },
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": "2026-03-24T00:00:00Z" },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        const string currentGuardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Authority guardian before trade resolution."
              },
              "manifestationHistory": [],
              "mood": { "current": "focused", "intensity": 40, "reason": "Authority baseline before trade.", "since": 10 },
              "abode": { "abodeId": "abode_alpha", "title": "Обитель Азалии" },
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": "2026-03-24T00:00:00Z" },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] },
              "tradeInventory": {
                "tradeCycleId": "cycle_12",
                "generatedAtUtc": "2026-03-28T00:00:00Z",
                "generationReputationTier": "neutral",
                "pricingReputationTier": "neutral",
                "effectiveRarityCeilingBonusSteps": 0,
                "projectBonusSignature": "0|0|0",
                "items": [
                  { "slotId": "slot_1", "priceInFeathers": 35, "soldOut": false, "rarityBonusStepsApplied": 0, "relicData": { "relicId": "relic_1", "name": "Реликвия 1", "quality": "Common" } },
                  { "slotId": "slot_2", "priceInFeathers": 35, "soldOut": false, "rarityBonusStepsApplied": 0, "relicData": { "relicId": "relic_2", "name": "Реликвия 2", "quality": "Common" } },
                  { "slotId": "slot_3", "priceInFeathers": 35, "soldOut": false, "rarityBonusStepsApplied": 0, "relicData": { "relicId": "relic_3", "name": "Реликвия 3", "quality": "Common" } },
                  { "slotId": "slot_4", "priceInFeathers": 35, "soldOut": false, "rarityBonusStepsApplied": 0, "relicData": { "relicId": "relic_4", "name": "Реликвия 4", "quality": "Common" } },
                  { "slotId": "slot_5", "priceInFeathers": 35, "soldOut": false, "rarityBonusStepsApplied": 0, "relicData": { "relicId": "relic_5", "name": "Реликвия 5", "quality": "Common" } },
                  { "slotId": "slot_6", "priceInFeathers": 35, "soldOut": false, "rarityBonusStepsApplied": 0, "relicData": { "relicId": "relic_6", "name": "Реликвия 6", "quality": "Common" } }
                ]
              },
              "tradeInventoryReceipts": [
                {
                  "requestId": "trade_req_exact_resolution",
                  "guardianId": "guardian_alpha",
                  "guardianName": "Азалия",
                  "abodeId": "abode_alpha",
                  "tradeCycleId": "cycle_12",
                  "status": "ready",
                  "itemCount": 6,
                  "resolvedAtTurn": 12,
                  "resolvedAtUtc": "2026-03-28T00:00:00Z"
                }
              ]
            }
          ]
        }
        """;

        var preTurnGuardiansWithActiveJson = WithActiveGuardian(preTurnGuardiansJson);
        var currentGuardiansWithActiveJson = WithActiveGuardian(currentGuardiansJson);

        const string requestJson = """
        {
          "requestId": "trade_req_exact_resolution",
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "abodeId": "abode_alpha",
          "returnCycleId": "cycle_12",
          "currentReputation": 30,
          "derivedTradeSlotCount": 6,
          "effectiveRarityCeilingBonusSteps": 0,
          "projectBonusSignature": "0|0|0",
          "createdAtTurn": 12,
          "createdAtUtc": "2026-03-28T00:00:00Z"
        }
        """;

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson(currentGuardiansWithActiveJson));
        await WriteRawAsync(GuardianTradeRequestState.PendingRequestPath, requestJson);
        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianTradeRequestState.PendingRequestPath,
            "test_backups/preturn_guardian_trade_exact_resolution_request.json",
            requestJson);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_trade_exact_resolution.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_guardian_trade_exact_resolution.json",
            NormalizeGuardianStateJson(preTurnGuardiansWithActiveJson));
        await AddCurrentWorldLoreToValidatedPreTurnSnapshotAsync("test_backups/preturn_world_lore_guardian_trade_exact_resolution");
        await EnsureEmptyCurrentGuardianProjectTrackerAndPowerJournalAsync();
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_missing_guardian_resolution", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_missing_inventory_resolution", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_missing_receipt_resolution", StringComparison.OrdinalIgnoreCase));

        static string WithActiveGuardian(string json)
        {
            var root = JsonNode.Parse(json)!.AsObject();
            root["activeGuardian"] = root["guardians"]!.AsArray()[0]!.DeepClone();
            return root.ToJsonString();
        }
    }

    [Fact]
    public async Task GuardianTradeResolution_PendingTradePriceMismatchesAreRepairVisibleErrors()
    {
        const string preTurnGuardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Authority guardian before trade resolution."
              },
              "manifestationHistory": [],
              "mood": { "current": "focused", "intensity": 40, "reason": "Authority baseline before trade.", "since": 10 },
              "abode": { "abodeId": "abode_alpha", "title": "Обитель Азалии" },
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": "2026-03-24T00:00:00Z" },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        const string currentGuardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Authority guardian before trade resolution."
              },
              "manifestationHistory": [],
              "mood": { "current": "focused", "intensity": 40, "reason": "Authority baseline before trade.", "since": 10 },
              "abode": { "abodeId": "abode_alpha", "title": "Обитель Азалии" },
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": "2026-03-24T10:00:00+10:00" },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T10:00:00+10:00", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] },
              "tradeInventory": {
                "tradeCycleId": "cycle_12",
                "generatedAtUtc": "2026-03-28T00:00:00Z",
                "generationReputationTier": "neutral",
                "pricingReputationTier": "neutral",
                "effectiveRarityCeilingBonusSteps": 0,
                "projectBonusSignature": "0|0|0",
                "items": [
                  { "slotId": "slot_1", "priceInFeathers": 999, "soldOut": false, "rarityBonusStepsApplied": 0, "relicData": { "relicId": "relic_1", "name": "Реликвия 1", "quality": "Common" } },
                  { "slotId": "slot_2", "priceInFeathers": 999, "soldOut": false, "rarityBonusStepsApplied": 0, "relicData": { "relicId": "relic_2", "name": "Реликвия 2", "quality": "Common" } }
                ]
              },
              "tradeInventoryReceipts": [
                {
                  "requestId": "trade_req_price_mismatch",
                  "guardianId": "guardian_alpha",
                  "guardianName": "Азалия",
                  "abodeId": "abode_alpha",
                  "tradeCycleId": "cycle_12",
                  "status": "ready",
                  "itemCount": 2,
                  "resolvedAtTurn": 12,
                  "resolvedAtUtc": "2026-03-28T00:00:00Z"
                }
              ]
            }
          ]
        }
        """;

        const string requestJson = """
        {
          "requestId": "trade_req_price_mismatch",
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "abodeId": "abode_alpha",
          "returnCycleId": "cycle_12",
          "currentReputation": 30,
          "derivedTradeSlotCount": 2,
          "effectiveRarityCeilingBonusSteps": 0,
          "projectBonusSignature": "0|0|0",
          "createdAtTurn": 12,
          "createdAtUtc": "2026-03-28T00:00:00Z"
        }
        """;

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        var preTurnGuardiansWithActiveJson = WithActiveGuardian(preTurnGuardiansJson);
        var currentGuardiansWithActiveJson = WithActiveGuardian(currentGuardiansJson);

        await WriteRawAsync("game_state/meta/guardians.json", currentGuardiansWithActiveJson);
        await WriteRawAsync(GuardianTradeRequestState.PendingRequestPath, requestJson);
        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianTradeRequestState.PendingRequestPath,
            "test_backups/preturn_guardian_trade_price_mismatch_request.json",
            requestJson);
        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_trade_price_mismatch.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);
        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_guardian_trade_price_mismatch.json",
            NormalizeGuardianStateJson(preTurnGuardiansWithActiveJson));
        await EnsureEmptyCurrentGuardianProjectTrackerAndPowerJournalAsync();
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        var priceIssues = issues
            .Where(issue => string.Equals(issue.Code, "guardian_trade_inventory_price_mismatch", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(priceIssues);
        Assert.All(priceIssues, issue => Assert.Equal(IssueSeverity.Error, issue.Severity));

        static string WithActiveGuardian(string json)
        {
            var root = JsonNode.Parse(json)!.AsObject();
            root["activeGuardian"] = root["guardians"]!.AsArray()[0]!.DeepClone();
            return root.ToJsonString();
        }
    }

    [Fact]
    public async Task GuardianTradeResolution_PendingTradeActiveGuardianMirrorMismatchesAreRepairVisibleErrors()
    {
        const string preTurnGuardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Authority guardian before trade resolution."
              },
              "manifestationHistory": [],
              "mood": { "current": "focused", "intensity": 40, "reason": "Authority baseline before trade.", "since": 10 },
              "abode": { "abodeId": "abode_alpha", "title": "Обитель Азалии" },
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": "2026-03-24T00:00:00Z" },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        const string currentGuardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Authority guardian before trade resolution."
              },
              "manifestationHistory": [],
              "mood": { "current": "focused", "intensity": 40, "reason": "Authority baseline before trade.", "since": 10 },
              "abode": { "abodeId": "abode_alpha", "title": "Обитель Азалии" },
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": "2026-03-24T10:00:00+10:00" },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T10:00:00+10:00", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] },
              "tradeInventory": {
                "tradeCycleId": "cycle_12",
                "generatedAtUtc": "2026-03-28T00:00:00Z",
                "generationReputationTier": "neutral",
                "pricingReputationTier": "neutral",
                "effectiveRarityCeilingBonusSteps": 0,
                "projectBonusSignature": "0|0|0",
                "items": [
                  { "slotId": "slot_1", "priceInFeathers": 30, "soldOut": false, "rarityBonusStepsApplied": 0, "relicData": { "relicId": "relic_1", "name": "Реликвия 1", "quality": "Common" } },
                  { "slotId": "slot_2", "priceInFeathers": 30, "soldOut": false, "rarityBonusStepsApplied": 0, "relicData": { "relicId": "relic_2", "name": "Реликвия 2", "quality": "Common" } }
                ]
              },
              "tradeInventoryReceipts": [
                {
                  "requestId": "trade_req_active_mirror_mismatch",
                  "guardianId": "guardian_alpha",
                  "guardianName": "Азалия",
                  "abodeId": "abode_alpha",
                  "tradeCycleId": "cycle_12",
                  "status": "ready",
                  "itemCount": 2,
                  "resolvedAtTurn": 12,
                  "resolvedAtUtc": "2026-03-28T00:00:00Z"
                }
              ]
            }
          ]
        }
        """;

        const string requestJson = """
        {
          "requestId": "trade_req_active_mirror_mismatch",
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "abodeId": "abode_alpha",
          "returnCycleId": "cycle_12",
          "currentReputation": 30,
          "derivedTradeSlotCount": 2,
          "effectiveRarityCeilingBonusSteps": 0,
          "projectBonusSignature": "0|0|0",
          "createdAtTurn": 12,
          "createdAtUtc": "2026-03-28T00:00:00Z"
        }
        """;

        var preTurnGuardiansWithActiveJson = WithActiveGuardian(preTurnGuardiansJson);
        var currentGuardiansWithStaleActiveJson = WithActiveGuardian(currentGuardiansJson, preTurnGuardiansJson);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);
        await WriteRawAsync("game_state/meta/guardians.json", currentGuardiansWithStaleActiveJson);
        await WriteRawAsync(GuardianTradeRequestState.PendingRequestPath, requestJson);
        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianTradeRequestState.PendingRequestPath,
            "test_backups/preturn_guardian_trade_active_mirror_mismatch_request.json",
            requestJson);
        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_trade_active_mirror_mismatch.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);
        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_guardian_trade_active_mirror_mismatch.json",
            NormalizeGuardianStateJson(preTurnGuardiansWithActiveJson));

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        var mirrorIssue = Assert.Single(
            issues,
            issue => string.Equals(issue.Code, "guardian_trade_inventory_presence_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(IssueSeverity.Error, mirrorIssue.Severity);

        static string WithActiveGuardian(string json, string? activeSourceJson = null)
        {
            var root = JsonNode.Parse(json)!.AsObject();
            var activeRoot = JsonNode.Parse(activeSourceJson ?? json)!.AsObject();
            root["activeGuardian"] = activeRoot["guardians"]!.AsArray()[0]!.DeepClone();
            return root.ToJsonString();
        }
    }

    [Fact]
    public async Task GuardianTradeResolution_StaleManifestDoesNotReusePreTurnRequest()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Current guardian state without trade inventory."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_alpha", "title": "Лазурный порог" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 25, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 18, "tier": "Угасающая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "mood": {
                "current": "focused",
                "intensity": 40,
                "reason": "Awaiting trade.",
                "since": 22
              },
              "loreFragments": [
                { "fragmentId": "guardian_alpha_lore_1", "category": "personal_history", "title": "След прилива", "content": null, "requiredReputation": 0 },
                { "fragmentId": "guardian_alpha_lore_2", "category": "cosmic_secret", "title": "Тайна течения", "content": null, "requiredReputation": 50 },
                { "fragmentId": "guardian_alpha_lore_3", "category": "domain_mastery", "title": "Узел имени", "content": null, "requiredReputation": 130 },
                { "fragmentId": "guardian_alpha_lore_4", "category": "lost_world", "title": "Имя на берегу", "content": null, "requiredReputation": 230 },
                { "fragmentId": "guardian_alpha_lore_5", "category": "other_guardians", "title": "Имена в пене", "content": null, "requiredReputation": 0 },
                { "fragmentId": "guardian_alpha_lore_6", "category": "soul_mechanics", "title": "Память соли", "content": null, "requiredReputation": 50 },
                { "fragmentId": "guardian_alpha_lore_7", "category": "personal_history", "title": "Возвращение имени", "content": null, "requiredReputation": 130 }
              ],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianTradeRequestState.PendingRequestPath,
            "test_backups/preturn_guardian_trade_request_stale.json",
            """
            {
              "requestId": "trade_req_stale",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "abodeId": "abode_alpha",
              "returnCycleId": "cycle_12",
              "derivedTradeSlotCount": 4,
              "createdAtTurn": 12,
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["sessionId"] = "stale-session";
        manifest["requestId"] = "stale-request";
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_missing_inventory_resolution", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_missing_receipt_resolution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianTradeResolution_CurrentRequestWithoutValidatedSnapshotRaisesExplicitError()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": []
        }
        """);

        await WriteRawAsync(GuardianTradeRequestState.PendingRequestPath, """
        {
          "requestId": "trade_req_missing_snapshot",
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "abodeId": "abode_alpha",
          "returnCycleId": "cycle_12",
          "derivedTradeSlotCount": 4,
          "createdAtTurn": 12,
          "createdAtUtc": "2026-03-28T00:00:00Z"
        }
        """);

        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_trade_snapshot_only.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_missing_validated_snapshot_request", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_missing_inventory_resolution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianTradeRequestContext_StaleManifestReportsSnapshotContextInsteadOfWrongRealm()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync(GuardianTradeRequestState.PendingRequestPath, """
        {
          "requestId": "trade_req_stale_context",
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "abodeId": "abode_alpha",
          "returnCycleId": "cycle_12",
          "derivedTradeSlotCount": 4,
          "createdAtTurn": 12,
          "createdAtUtc": "2026-03-28T00:00:00Z"
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_trade_stale_context.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_invalid_validated_snapshot_context", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_wrong_realm", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianTradeResolution_StaleManifestRaisesExplicitSnapshotError()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": []
        }
        """);

        await WriteRawAsync(GuardianTradeRequestState.PendingRequestPath, """
        {
          "requestId": "trade_req_stale_resolution",
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "abodeId": "abode_alpha",
          "returnCycleId": "cycle_12",
          "derivedTradeSlotCount": 4,
          "createdAtTurn": 12,
          "createdAtUtc": "2026-03-28T00:00:00Z"
        }
        """);

        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianTradeRequestState.PendingRequestPath,
            "test_backups/preturn_guardian_trade_stale_resolution.json",
            """
            {
              "requestId": "trade_req_stale_resolution",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "abodeId": "abode_alpha",
              "returnCycleId": "cycle_12",
              "derivedTradeSlotCount": 4,
              "createdAtTurn": 12,
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_missing_validated_snapshot_request", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_missing_inventory_resolution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianTradeResolution_MalformedValidatedSnapshotRaisesExplicitInvalidSnapshotError()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": []
        }
        """);

        await WriteRawAsync(GuardianTradeRequestState.PendingRequestPath, """
        {
          "requestId": "trade_req_invalid_snapshot",
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "abodeId": "abode_alpha",
          "returnCycleId": "cycle_12",
          "derivedTradeSlotCount": 4,
          "createdAtTurn": 12,
          "createdAtUtc": "2026-03-28T00:00:00Z"
        }
        """);

        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianTradeRequestState.PendingRequestPath,
            "test_backups/preturn_guardian_trade_invalid_snapshot_contract.json",
            """
            {
              "requestId": "trade_req_invalid_snapshot",
              "guardianId":
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_trade_invalid_snapshot_contract.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_invalid_validated_snapshot_request", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_missing_inventory_resolution", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_missing_receipt_resolution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianTradeResolution_MissingMaterializedGuardianRaisesExplicitGuardianResolutionError()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": []
        }
        """);

        await WriteRawAsync(GuardianTradeRequestState.PendingRequestPath, """
        {
          "requestId": "trade_req_missing_guardian",
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "abodeId": "abode_alpha",
          "returnCycleId": "cycle_12",
          "currentReputation": 30,
          "derivedTradeSlotCount": 4,
          "effectiveRarityCeilingBonusSteps": 0,
          "projectBonusSignature": "0|0|0",
          "createdAtTurn": 12,
          "createdAtUtc": "2026-03-28T00:00:00Z"
        }
        """);

        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianTradeRequestState.PendingRequestPath,
            "test_backups/preturn_guardian_trade_missing_guardian_resolution.json",
            """
            {
              "requestId": "trade_req_missing_guardian",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "abodeId": "abode_alpha",
              "returnCycleId": "cycle_12",
              "currentReputation": 30,
              "derivedTradeSlotCount": 4,
              "effectiveRarityCeilingBonusSteps": 0,
              "projectBonusSignature": "0|0|0",
              "createdAtTurn": 12,
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_trade_missing_guardian_resolution.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        var missingGuardianIssue = Assert.Single(
            issues,
            issue => string.Equals(issue.Code, "guardian_trade_request_missing_guardian_resolution", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("guardian guardian_alpha missing from current guardian authority", missingGuardianIssue.Actual);
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_missing_inventory_resolution", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_missing_receipt_resolution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianTradeResolution_DeletedCurrentPendingRequestStillRequiresAuthorityBackedResolution()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": []
        }
        """);

        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianTradeRequestState.PendingRequestPath,
            "test_backups/preturn_guardian_trade_request_deleted_current_resolution.json",
            """
            {
              "requestId": "trade_req_deleted_current",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "abodeId": "abode_alpha",
              "returnCycleId": "cycle_12",
              "currentReputation": 30,
              "derivedTradeSlotCount": 4,
              "effectiveRarityCeilingBonusSteps": 0,
              "projectBonusSignature": "0|0|0",
              "createdAtTurn": 12,
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_trade_deleted_current_resolution.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_guardian_trade_deleted_current_resolution.json",
            """
            {
              "guardians": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_missing_guardian_resolution", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_missing_validated_snapshot_request", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianTradeResolution_DeletedCurrentPendingRequestWithMissingSnapshotEntryRaisesStrictSnapshotError()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": []
        }
        """);

        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianTradeRequestState.PendingRequestPath,
            "test_backups/preturn_guardian_trade_request_missing_snapshot_entry.json",
            """
            {
              "requestId": "trade_req_missing_snapshot_entry",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "abodeId": "abode_alpha",
              "returnCycleId": "cycle_12",
              "currentReputation": 30,
              "derivedTradeSlotCount": 4,
              "effectiveRarityCeilingBonusSteps": 0,
              "projectBonusSignature": "0|0|0",
              "createdAtTurn": 12,
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_trade_missing_snapshot_entry.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_guardian_trade_missing_snapshot_entry.json",
            """
            {
              "guardians": []
            }
            """);

        await RemoveTrackedSnapshotEntryFromCurrentPendingTurnSnapshotAsync(GuardianTradeRequestState.PendingRequestPath);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_missing_validated_snapshot_request", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "guardian_missing_validated_preturn_guardians_snapshot", StringComparison.OrdinalIgnoreCase) ||
            issue.Message.Contains("Current canonical guardians[] требует readable validated pre-turn guardians baseline", StringComparison.Ordinal));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_missing_guardian_resolution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianTradeResolution_DeletedCurrentPendingRequestWithUnusableManifestRaisesStrictSnapshotError()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": []
        }
        """);

        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianTradeRequestState.PendingRequestPath,
            "test_backups/preturn_guardian_trade_request_unusable_manifest.json",
            """
            {
              "requestId": "trade_req_unusable_manifest",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "abodeId": "abode_alpha",
              "returnCycleId": "cycle_12",
              "currentReputation": 30,
              "derivedTradeSlotCount": 4,
              "effectiveRarityCeilingBonusSteps": 0,
              "projectBonusSignature": "0|0|0",
              "createdAtTurn": 12,
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_trade_unusable_manifest.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_guardian_trade_unusable_manifest.json",
            """
            {
              "guardians": []
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["manifestPayloadHash"] = "BROKEN_MANIFEST_HASH";
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_missing_validated_snapshot_request", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "guardian_missing_validated_preturn_guardians_snapshot", StringComparison.OrdinalIgnoreCase) ||
            issue.Message.Contains("Current canonical guardians[] требует readable validated pre-turn guardians baseline", StringComparison.Ordinal));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_missing_guardian_resolution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianTradeResolution_DeletedCurrentPendingRequestWithMalformedManifestRaisesExplicitInvalidSnapshotError()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": []
        }
        """);

        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianTradeRequestState.PendingRequestPath,
            "test_backups/preturn_guardian_trade_request_malformed_manifest.json",
            """
            {
              "requestId": "trade_req_malformed_manifest",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "abodeId": "abode_alpha",
              "returnCycleId": "cycle_12",
              "currentReputation": 30,
              "derivedTradeSlotCount": 4,
              "effectiveRarityCeilingBonusSteps": 0,
              "projectBonusSignature": "0|0|0",
              "createdAtTurn": 12,
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_trade_malformed_manifest.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_guardian_trade_malformed_manifest.json",
            """
            {
              "guardians": []
            }
            """);

        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            "{\n  \"files\": {\n");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_invalid_validated_snapshot_request", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_missing_guardian_resolution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianTradeResolution_DeletedCurrentPendingRequestWithSemanticallyInvalidSnapshotContractFailsExplicitly()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": []
        }
        """);

        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianTradeRequestState.PendingRequestPath,
            "test_backups/preturn_guardian_trade_request_invalid_contract.json",
            """
            {
              "requestId": "",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "abodeId": "abode_alpha",
              "returnCycleId": "cycle_12",
              "derivedTradeSlotCount": 0,
              "createdAtTurn": 12,
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_trade_invalid_contract.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_guardian_trade_invalid_contract.json",
            """
            {
              "guardians": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_missing_fields", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_missing_guardian_resolution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianTradeResolution_DeletedCurrentPendingRequestWithWrongRealmFailsExplicitly()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": []
        }
        """);

        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianTradeRequestState.PendingRequestPath,
            "test_backups/preturn_guardian_trade_request_wrong_realm_deleted_resolution.json",
            """
            {
              "requestId": "trade_req_wrong_realm_deleted",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "abodeId": "abode_alpha",
              "returnCycleId": "cycle_12",
              "currentReputation": 30,
              "derivedTradeSlotCount": 4,
              "effectiveRarityCeilingBonusSteps": 0,
              "projectBonusSignature": "0|0|0",
              "createdAtTurn": 12,
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_trade_wrong_realm_deleted_resolution.json",
            """
            {
              "currentRealm": "Mortal World"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_guardian_trade_wrong_realm_deleted_resolution.json",
            """
            {
              "guardians": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_wrong_realm", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_missing_guardian_resolution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AbodeOfferingResolution_CurrentRequestWithoutValidatedSnapshotRaisesExplicitError()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync(GuardianAbodeOfferingState.PendingRequestPath, """
        {
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "offeringType": "soul_relic",
          "returnCycleId": "cycle_12",
          "relicId": "relic_current",
          "relicName": "Текущая реликвия",
          "relicRarity": "Rare",
          "createdAtUtc": "2026-03-28T00:00:00Z"
        }
        """);

        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_abode_offering_context.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_missing_validated_snapshot_request", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_missing_matching_power_event", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AbodeOfferingResolution_DeletedCurrentPendingRequestStillRequiresStrictResolution()
    {
        const string guardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Guardian without resolved offering outcome."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson(guardiansJson));
        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": []
        }
        """);
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """);
        await EnsureReadableCurrentGuardianProjectTrackerAsync();

        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_deleted_current_resolution.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "ink_feathers",
              "inkFeathersOffered": 100,
              "returnCycleId": "cycle_12",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_abode_offering_deleted_current_resolution.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_abode_offering_deleted_current_resolution.json",
            NormalizeGuardianStateJson(guardiansJson));

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_deleted_current_resolution.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_deleted_current_resolution.json",
            """
            {
              "entries": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_missing_matching_power_event", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_expected_power_gain_missing", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_missing_validated_snapshot_request", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AbodeOfferingResolution_DeletedCurrentPendingRequestWithWrongRealmFailsExplicitly()
    {
        const string guardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Guardian for wrong realm offering regression."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", guardiansJson);
        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": []
        }
        """);

        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_wrong_realm_resolution.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "ink_feathers",
              "inkFeathersOffered": 100,
              "returnCycleId": "cycle_12",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_abode_offering_wrong_realm_resolution.json",
            """
            {
              "currentRealm": "Mortal World"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_abode_offering_wrong_realm_resolution.json",
            guardiansJson);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_wrong_realm", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_missing_matching_power_event", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AbodeOfferingResolution_DeletedCurrentPendingRequestWithMissingSnapshotEntryRaisesStrictSnapshotError()
    {
        const string guardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Guardian for missing snapshot entry offering regression."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", guardiansJson);
        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": []
        }
        """);

        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_missing_snapshot_entry.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "ink_feathers",
              "inkFeathersOffered": 100,
              "returnCycleId": "cycle_12",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_abode_offering_missing_snapshot_entry.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_abode_offering_missing_snapshot_entry.json",
            guardiansJson);

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_missing_snapshot_entry.json",
            """
            {
              "entries": []
            }
            """);

        await RemoveTrackedSnapshotEntryFromCurrentPendingTurnSnapshotAsync(GuardianAbodeOfferingState.PendingRequestPath);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_missing_validated_snapshot_request", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "guardian_missing_validated_preturn_guardians_snapshot", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "guardian_power_event_missing_validated_preturn_guardians_snapshot", StringComparison.OrdinalIgnoreCase) ||
            issue.Message.Contains("Current canonical guardians[] требует readable validated pre-turn guardians baseline", StringComparison.Ordinal) ||
            issue.Message.Contains("Abode power journal validation требует readable validated pre-turn guardians baseline", StringComparison.Ordinal));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_missing_matching_power_event", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_expected_power_gain_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AbodeOfferingResolution_DeletedCurrentPendingRequestWithUnusableManifestRaisesStrictSnapshotError()
    {
        const string guardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Guardian for unusable manifest offering regression."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", guardiansJson);
        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": []
        }
        """);

        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_unusable_manifest.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "ink_feathers",
              "inkFeathersOffered": 100,
              "returnCycleId": "cycle_12",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_abode_offering_unusable_manifest.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_abode_offering_unusable_manifest.json",
            guardiansJson);

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_unusable_manifest.json",
            """
            {
              "entries": []
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["manifestPayloadHash"] = "BROKEN_MANIFEST_HASH";
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_missing_validated_snapshot_request", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "guardian_missing_validated_preturn_guardians_snapshot", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "guardian_power_event_missing_validated_preturn_guardians_snapshot", StringComparison.OrdinalIgnoreCase) ||
            issue.Message.Contains("Current canonical guardians[] требует readable validated pre-turn guardians baseline", StringComparison.Ordinal) ||
            issue.Message.Contains("Abode power journal validation требует readable validated pre-turn guardians baseline", StringComparison.Ordinal));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_missing_matching_power_event", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_expected_power_gain_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AbodeOfferingResolution_DeletedCurrentPendingRequestWithMalformedManifestRaisesExplicitInvalidSnapshotError()
    {
        const string guardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Guardian for malformed manifest offering regression."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", guardiansJson);
        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": []
        }
        """);

        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_malformed_manifest.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "ink_feathers",
              "inkFeathersOffered": 100,
              "returnCycleId": "cycle_12",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_abode_offering_malformed_manifest.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_abode_offering_malformed_manifest.json",
            guardiansJson);

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_malformed_manifest.json",
            """
            {
              "entries": []
            }
            """);

        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            "{\n  \"files\": {\n");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_validated_snapshot_request", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_missing_matching_power_event", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AbodeOfferingResolution_MalformedValidatedSnapshotRaisesExplicitInvalidSnapshotError()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync(GuardianAbodeOfferingState.PendingRequestPath, """
        {
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "offeringType": "ink_feathers",
          "inkFeathersOffered": 25,
          "returnCycleId": "cycle_12",
          "createdAtUtc": "2026-03-28T00:00:00Z"
        }
        """);

        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_invalid_snapshot_contract.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName":
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_abode_offering_invalid_snapshot_contract.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_validated_snapshot_request", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_missing_matching_power_event", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_expected_power_gain_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AbodeOfferingResolution_DeletedCurrentPendingRequestWithSemanticallyInvalidSnapshotContractFailsExplicitly()
    {
        const string guardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Guardian for invalid offering snapshot contract."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", guardiansJson);
        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": []
        }
        """);

        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_invalid_contract.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "unknown_offering",
              "inkFeathersOffered": 100,
              "returnCycleId": "cycle_12",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_abode_offering_invalid_contract.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_abode_offering_invalid_contract.json",
            guardiansJson);

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_invalid_contract.json",
            """
            {
              "entries": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_type", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_missing_matching_power_event", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_expected_power_gain_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AbodeOfferingResolution_MalformedJournalEntryDoesNotSatisfyStrictProof()
    {
        const string preTurnGuardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Pre-turn guardian before malformed journal proof."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        const string currentGuardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Current guardian after offering with valid power event but malformed journal."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "offering_evt_malformed_journal",
              "guardianId": "guardian_alpha",
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_malformed_journal",
              "title": "Offering applied before journal proof",
              "summary": "Authority power changes, but malformed journal must not count as proof.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """;

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson(currentGuardiansJson));
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """);
        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "eventId": "offering_evt_malformed_journal",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_malformed_journal",
              "title": "Malformed journal proof",
              "summary": "Missing finalDelta must block journal proof.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """);

        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_malformed_journal_proof.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "ink_feathers",
              "inkFeathersOffered": 100,
              "returnCycleId": "cycle_12",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_abode_offering_malformed_journal_proof.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_abode_offering_malformed_journal_proof.json",
            NormalizeGuardianStateJson(preTurnGuardiansJson));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_abode_offering_malformed_journal_proof.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_malformed_journal_proof.json",
            """
            {
              "entries": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_current_journal_proof", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_expected_power_gain_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AbodeOfferingResolution_MalformedValidatedPreTurnJournalBaselineFailsExplicitly()
    {
        const string preTurnGuardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Pre-turn guardian before malformed journal baseline."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        const string currentGuardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Current guardian after offering with corrupted pre-turn journal baseline."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "offering_evt_invalid_preturn_journal",
              "guardianId": "guardian_alpha",
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_invalid_preturn_journal",
              "title": "Offering with corrupted pre-turn journal baseline",
              "summary": "Current-only journal proof must not survive malformed validated baseline.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """;

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", currentGuardiansJson);
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """);
        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_journal_invalid_preturn_001",
              "eventId": "offering_evt_invalid_preturn_journal",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_invalid_preturn_journal",
              "title": "Offering with corrupted pre-turn journal baseline",
              "summary": "Current journal is valid, but baseline is not.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """);

        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_invalid_journal_baseline.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "ink_feathers",
              "inkFeathersOffered": 100,
              "returnCycleId": "cycle_12",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_abode_offering_invalid_journal_baseline.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_abode_offering_invalid_journal_baseline.json",
            NormalizeGuardianStateJson(preTurnGuardiansJson));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_abode_offering_invalid_journal_baseline.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_invalid_journal_baseline.json",
            "{");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);
        var issueCodes = string.Join(", ", issues.Select(issue => issue.Code));

        Assert.True(
            issues.Any(issue => string.Equals(issue.Code, "abode_offering_invalid_validated_snapshot_journal", StringComparison.OrdinalIgnoreCase)),
            issueCodes);
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_missing_matching_power_event", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_expected_power_gain_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AbodeOfferingResolution_UnreadableCurrentSoulStateFailsConsumptionProof()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: ABODE_OFFERING] Offer relic"
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{");

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Current guardian after accepted-turn relic offering with unreadable current soul state."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "offering_evt_invalid_current_soul_accepted",
              "guardianId": "guardian_alpha",
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_invalid_current_soul_accepted",
              "title": "Accepted-turn offering with unreadable current soul state",
              "summary": "Consumption proof must fail closed when current soul_state is unreadable.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "soul_relic",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "relicId": "relic_invalid_current_soul",
                "relicName": "Реликвия нечитаемого current soul",
                "relicRarity": "Rare"
              }
            }
          ]
        }
        """);
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_tracker_missing_journal_baseline.json",
            """
            {
              "activeProjects": [],
              "completedProjects": []
            }
            """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_journal_invalid_current_soul_accepted_001",
              "eventId": "offering_evt_invalid_current_soul_accepted",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_invalid_current_soul_accepted",
              "title": "Accepted-turn offering with unreadable current soul state",
              "summary": "Current soul_state must still be readable to prove consumption.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "soul_relic",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "relicId": "relic_invalid_current_soul",
                "relicName": "Реликвия нечитаемого current soul",
                "relicRarity": "Rare"
              }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianAbodeOfferingState.PendingRequestPath, """
        {
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "offeringType": "soul_relic",
          "returnCycleId": "cycle_12",
          "relicId": "relic_invalid_current_soul",
          "relicName": "Реликвия нечитаемого current soul",
          "relicRarity": "Rare",
          "createdAtUtc": "2026-03-28T00:00:00Z"
        }
        """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_invalid_current_soul_accepted.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "soul_relic",
              "returnCycleId": "cycle_12",
              "relicId": "relic_invalid_current_soul",
              "relicName": "Реликвия нечитаемого current soul",
              "relicRarity": "Rare",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_invalid_current_soul_accepted.json",
            NormalizeGuardianStateJson("""
            {
              "guardians": [
                {
                  "guardianId": "guardian_alpha",
                  "canonicalName": "Азалия",
                  "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
                  "manifestation": {
                    "currentDisplayName": "Азалия",
                    "formFlexibility": "selective",
                    "currentPresentationStyle": "feminine",
                    "currentPronouns": "она/её",
                    "appearanceDescription": "Pre-turn guardian before accepted-turn relic offering."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_invalid_current_soul_accepted.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_invalid_current_soul_accepted.json",
            """
            {
              "currentRealm": "Chaos Sea",
              "soulRelics": [
                {
                  "relicId": "relic_invalid_current_soul",
                  "name": "Реликвия нечитаемого current soul",
                  "rarity": "Rare"
                }
              ]
            }
            """);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "ABODE_OFFERING",
          "resolutionType": "abodeOffering",
          "summary": "Accepted-turn relic offering must fail closed when current soul_state is unreadable.",
          "resolved": true,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "powerGain": 2,
            "returnCycleId": "cycle_12",
            "powerEventId": "offering_evt_invalid_current_soul_accepted",
            "affectedFiles": [
              "game_state/meta/guardians.json",
              "game_state/meta/abode_power_journal.json",
              "game_state/meta/soul_state.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_current_soul_state", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_soul_relic_not_consumed", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("{\"currentRealm\":\"Chaos Sea\"}")]
    [InlineData("{\"currentRealm\":\"Chaos Sea\",\"soulRelics\":{\"stored\":{}}}")]
    [InlineData("{\"currentRealm\":\"Chaos Sea\",\"soulRelics\":{\"equipped\":[{\"relicId\":\"relic_invalid_current_soul_shape\",\"name\":\"Реликвия invalid current soul shape\",\"rarity\":\"Rare\",\"relicType\":\"companion_echo\",\"companionSeed\":{\"sourceResidentId\":\"resident_alpha_1\"}}],\"stored\":[]}}")]
    [InlineData("{\"currentRealm\":\"Chaos Sea\",\"inkFeathers\":{\"current\":\"5\"},\"soulRelics\":{\"equipped\":[],\"stored\":[]}}")]
    public async Task AcceptedTurnAbodeOffering_InvalidCurrentSoulProofShapeFailsConsumptionProof(string currentSoulJson)
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: ABODE_OFFERING] Offer relic"
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", currentSoulJson);

        await WriteRawAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson("""
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Current guardian after offering with parseable invalid current soul shape."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "offering_evt_invalid_current_soul_shape",
              "guardianId": "guardian_alpha",
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_invalid_current_soul_shape",
              "title": "Accepted-turn offering with parseable invalid current soul shape",
              "summary": "Current soul_state shape must be canonical to prove consumption.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "soul_relic",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "relicId": "relic_invalid_current_soul_shape",
                "relicName": "Реликвия invalid current soul shape",
                "relicRarity": "Rare"
              }
            }
          ]
        }
        """));
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_tracker_missing_journal_baseline.json",
            """
            {
              "activeProjects": [],
              "completedProjects": []
            }
            """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_journal_invalid_current_soul_shape_001",
              "eventId": "offering_evt_invalid_current_soul_shape",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_invalid_current_soul_shape",
              "title": "Accepted-turn offering with parseable invalid current soul shape",
              "summary": "Current soul_state shape must stay canonical for consumption proof.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "soul_relic",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "relicId": "relic_invalid_current_soul_shape",
                "relicName": "Реликвия invalid current soul shape",
                "relicRarity": "Rare"
              }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianAbodeOfferingState.PendingRequestPath, """
        {
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "offeringType": "soul_relic",
          "returnCycleId": "cycle_12",
          "relicId": "relic_invalid_current_soul_shape",
          "relicName": "Реликвия invalid current soul shape",
          "relicRarity": "Rare",
          "createdAtUtc": "2026-03-28T00:00:00Z"
        }
        """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_invalid_current_soul_shape.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "soul_relic",
              "returnCycleId": "cycle_12",
              "relicId": "relic_invalid_current_soul_shape",
              "relicName": "Реликвия invalid current soul shape",
              "relicRarity": "Rare",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_invalid_current_soul_shape.json",
            NormalizeGuardianStateJson("""
            {
              "guardians": [
                {
                  "guardianId": "guardian_alpha",
                  "canonicalName": "Азалия",
                  "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
                  "manifestation": {
                    "currentDisplayName": "Азалия",
                    "formFlexibility": "selective",
                    "currentPresentationStyle": "feminine",
                    "currentPronouns": "она/её",
                    "appearanceDescription": "Pre-turn guardian before accepted-turn relic offering with invalid current soul shape."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_invalid_current_soul_shape.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_invalid_current_soul_shape.json",
            """
            {
              "currentRealm": "Chaos Sea",
              "soulRelics": [
                {
                  "relicId": "relic_invalid_current_soul_shape",
                  "name": "Реликвия invalid current soul shape",
                  "rarity": "Rare"
                }
              ]
            }
            """);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "ABODE_OFFERING",
          "resolutionType": "abodeOffering",
          "summary": "Accepted-turn relic offering must fail closed on parseable invalid current soul_state shape.",
          "resolved": true,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "powerGain": 2,
            "returnCycleId": "cycle_12",
            "powerEventId": "offering_evt_invalid_current_soul_shape",
            "affectedFiles": [
              "game_state/meta/guardians.json",
              "game_state/meta/abode_power_journal.json",
              "game_state/meta/soul_state.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_current_soul_state", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_soul_relic_not_consumed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnAbodeOffering_ArchiveProofRejectsLegacyCurrentArchiveArrayShape()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: ABODE_OFFERING] Offer archive fragment"
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "afterlifeArchive": []
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson("""
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Current guardian after archive offering with legacy current archive shape."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "offering_evt_invalid_current_archive_shape",
              "guardianId": "guardian_alpha",
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_invalid_current_archive_shape",
              "title": "Accepted-turn archive offering with legacy current archive shape",
              "summary": "Current afterlifeArchive proof surface must use canonical object+stored shape.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "archive_lore_fragment",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "archiveId": "archive_invalid_current_shape",
                "archiveTitle": "Летопись invalid current archive shape",
                "archiveEntryType": "lore_fragment",
                "archiveRarity": "Rare"
              }
            }
          ]
        }
        """));
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_tracker_invalid_current_archive_shape.json",
            """
            {
              "activeProjects": [],
              "completedProjects": []
            }
            """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_journal_invalid_current_archive_shape_001",
              "eventId": "offering_evt_invalid_current_archive_shape",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_invalid_current_archive_shape",
              "title": "Accepted-turn archive offering with legacy current archive shape",
              "summary": "Journal and power exist, but current afterlifeArchive shape is non-canonical.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "archive_lore_fragment",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "archiveId": "archive_invalid_current_shape",
                "archiveTitle": "Летопись invalid current archive shape",
                "archiveEntryType": "lore_fragment",
                "archiveRarity": "Rare"
              }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianAbodeOfferingState.PendingRequestPath, """
        {
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "offeringType": "archive_lore_fragment",
          "returnCycleId": "cycle_12",
          "archiveId": "archive_invalid_current_shape",
          "archiveTitle": "Летопись invalid current archive shape",
          "archiveEntryType": "lore_fragment",
          "archiveRarity": "Rare",
          "createdAtUtc": "2026-03-28T00:00:00Z"
        }
        """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_invalid_current_archive_shape.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "archive_lore_fragment",
              "returnCycleId": "cycle_12",
              "archiveId": "archive_invalid_current_shape",
              "archiveTitle": "Летопись invalid current archive shape",
              "archiveEntryType": "lore_fragment",
              "archiveRarity": "Rare",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_invalid_current_archive_shape.json",
            NormalizeGuardianStateJson("""
            {
              "guardians": [
                {
                  "guardianId": "guardian_alpha",
                  "canonicalName": "Азалия",
                  "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
                  "manifestation": {
                    "currentDisplayName": "Азалия",
                    "formFlexibility": "selective",
                    "currentPresentationStyle": "feminine",
                    "currentPronouns": "она/её",
                    "appearanceDescription": "Pre-turn guardian before accepted-turn archive offering with legacy current archive shape."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_invalid_current_archive_shape.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_invalid_current_archive_shape.json",
            """
            {
              "currentRealm": "Chaos Sea",
              "afterlifeArchive": {
                "stored": [
                  {
                    "archiveId": "archive_invalid_current_shape",
                    "title": "Летопись invalid current archive shape",
                    "entryType": "lore_fragment",
                    "summary": "Canonical pre-turn archive leaf for legacy current archive shape regression.",
                    "rarity": "Rare",
                    "sourceLife": 1,
                    "acquiredAtUtc": "2026-03-24T00:00:00Z"
                  }
                ],
                "actionReceipts": []
              }
            }
            """);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "ABODE_OFFERING",
          "resolutionType": "abodeOffering",
          "summary": "Accepted-turn archive offering must fail closed on legacy current afterlifeArchive array shape.",
          "resolved": true,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "powerGain": 2,
            "returnCycleId": "cycle_12",
            "powerEventId": "offering_evt_invalid_current_archive_shape",
            "affectedFiles": [
              "game_state/meta/guardians.json",
              "game_state/meta/abode_power_journal.json",
              "game_state/meta/soul_state.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_current_soul_state", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_archive_entry_not_consumed", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("{\"currentRealm\":\"Chaos Sea\"}")]
    [InlineData("{\"currentRealm\":\"Chaos Sea\",\"soulRelics\":{\"stored\":{}}}")]
    public async Task AbodeOfferingResolution_InvalidValidatedPreTurnSoulStateFailsSnapshotProof(string preTurnSoulJson)
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: ABODE_OFFERING] Offer relic"
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "soulRelics": {
            "stored": [],
            "equipped": []
          }
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Current guardian after offering with invalid pre-turn soul baseline shape."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "offering_evt_invalid_preturn_soul",
              "guardianId": "guardian_alpha",
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_invalid_preturn_soul",
              "title": "Accepted-turn offering with invalid pre-turn soul baseline shape",
              "summary": "Parseable invalid pre-turn soul_state must fail snapshot proof, not degrade into ownership miss.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "soul_relic",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "relicId": "relic_invalid_preturn_soul",
                "relicName": "Реликвия битого baseline",
                "relicRarity": "Rare"
              }
            }
          ]
        }
        """);
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_journal_invalid_preturn_soul_001",
              "eventId": "offering_evt_invalid_preturn_soul",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_invalid_preturn_soul",
              "title": "Accepted-turn offering with invalid pre-turn soul baseline shape",
              "summary": "Journal and power exist, but validated pre-turn soul_state shape is invalid.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "soul_relic",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "relicId": "relic_invalid_preturn_soul",
                "relicName": "Реликвия битого baseline",
                "relicRarity": "Rare"
              }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianAbodeOfferingState.PendingRequestPath, """
        {
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "offeringType": "soul_relic",
          "returnCycleId": "cycle_12",
          "relicId": "relic_invalid_preturn_soul",
          "relicName": "Реликвия битого baseline",
          "relicRarity": "Rare",
          "createdAtUtc": "2026-03-28T00:00:00Z"
        }
        """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_invalid_preturn_soul.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "soul_relic",
              "returnCycleId": "cycle_12",
              "relicId": "relic_invalid_preturn_soul",
              "relicName": "Реликвия битого baseline",
              "relicRarity": "Rare",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_abode_offering_invalid_preturn_soul.json",
            NormalizeGuardianStateJson("""
            {
              "guardians": [
                {
                  "guardianId": "guardian_alpha",
                  "canonicalName": "Азалия",
                  "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
                  "manifestation": {
                    "currentDisplayName": "Азалия",
                    "formFlexibility": "selective",
                    "currentPresentationStyle": "feminine",
                    "currentPronouns": "она/её",
                    "appearanceDescription": "Pre-turn guardian before offering with invalid pre-turn soul baseline shape."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_invalid_preturn_soul.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_abode_offering_invalid_preturn_soul.json",
            preTurnSoulJson);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "ABODE_OFFERING",
          "resolutionType": "abodeOffering",
          "summary": "Accepted-turn relic offering must fail closed on parseable invalid validated pre-turn soul_state shape.",
          "resolved": true,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "powerGain": 2,
            "returnCycleId": "cycle_12",
            "powerEventId": "offering_evt_invalid_preturn_soul",
            "affectedFiles": [
              "game_state/meta/guardians.json",
              "game_state/meta/abode_power_journal.json",
              "game_state/meta/soul_state.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_validated_snapshot_soul_state", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_soul_relic_missing_preturn_ownership", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnAbodeOffering_ArchiveProofRejectsLegacyValidatedSnapshotArchiveArrayShape()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: ABODE_OFFERING] Offer archive fragment"
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "afterlifeArchive": {
            "stored": [],
            "actionReceipts": []
          }
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Current guardian after archive offering with legacy validated snapshot archive shape."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "offering_evt_invalid_preturn_archive_shape",
              "guardianId": "guardian_alpha",
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_invalid_preturn_archive_shape",
              "title": "Archive offering with legacy validated snapshot archive shape",
              "summary": "Validated pre-turn afterlifeArchive proof surface must use canonical object+stored shape.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "archive_lore_fragment",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "archiveId": "archive_invalid_preturn_shape",
                "archiveTitle": "Летопись invalid preturn archive shape",
                "archiveEntryType": "lore_fragment",
                "archiveRarity": "Rare"
              }
            }
          ]
        }
        """);
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_journal_invalid_preturn_archive_shape_001",
              "eventId": "offering_evt_invalid_preturn_archive_shape",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_invalid_preturn_archive_shape",
              "title": "Archive offering with legacy validated snapshot archive shape",
              "summary": "Journal and power exist, but validated pre-turn afterlifeArchive shape is non-canonical.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "archive_lore_fragment",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "archiveId": "archive_invalid_preturn_shape",
                "archiveTitle": "Летопись invalid preturn archive shape",
                "archiveEntryType": "lore_fragment",
                "archiveRarity": "Rare"
              }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianAbodeOfferingState.PendingRequestPath, """
        {
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "offeringType": "archive_lore_fragment",
          "returnCycleId": "cycle_12",
          "archiveId": "archive_invalid_preturn_shape",
          "archiveTitle": "Летопись invalid preturn archive shape",
          "archiveEntryType": "lore_fragment",
          "archiveRarity": "Rare",
          "createdAtUtc": "2026-03-28T00:00:00Z"
        }
        """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_invalid_preturn_archive_shape.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "archive_lore_fragment",
              "returnCycleId": "cycle_12",
              "archiveId": "archive_invalid_preturn_shape",
              "archiveTitle": "Летопись invalid preturn archive shape",
              "archiveEntryType": "lore_fragment",
              "archiveRarity": "Rare",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_abode_offering_invalid_preturn_archive_shape.json",
            NormalizeGuardianStateJson("""
            {
              "guardians": [
                {
                  "guardianId": "guardian_alpha",
                  "canonicalName": "Азалия",
                  "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
                  "manifestation": {
                    "currentDisplayName": "Азалия",
                    "formFlexibility": "selective",
                    "currentPresentationStyle": "feminine",
                    "currentPronouns": "она/её",
                    "appearanceDescription": "Pre-turn guardian before archive offering with legacy validated snapshot archive shape."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_invalid_preturn_archive_shape.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_abode_offering_invalid_preturn_archive_shape.json",
            """
            {
              "currentRealm": "Chaos Sea",
              "afterlifeArchive": []
            }
            """);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "ABODE_OFFERING",
          "resolutionType": "abodeOffering",
          "summary": "Accepted-turn archive offering must fail closed on legacy validated snapshot afterlifeArchive array shape.",
          "resolved": true,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "powerGain": 2,
            "returnCycleId": "cycle_12",
            "powerEventId": "offering_evt_invalid_preturn_archive_shape",
            "affectedFiles": [
              "game_state/meta/guardians.json",
              "game_state/meta/abode_power_journal.json",
              "game_state/meta/soul_state.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_validated_snapshot_soul_state", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_archive_entry_missing_preturn_ownership", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AbodeOfferingResolution_UsesAuthorityPowerInsteadOfMaterializedGuardianDrift()
    {
        const string preTurnGuardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Authority baseline before offering."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        const string currentGuardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Materialized guardian drift pretends the offering already increased power."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", currentGuardiansJson);
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """);
        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": []
        }
        """);

        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_authority_power.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "ink_feathers",
              "inkFeathersOffered": 100,
              "returnCycleId": "cycle_12",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_abode_offering_authority_power.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_abode_offering_authority_power.json",
            NormalizeGuardianStateJson(preTurnGuardiansJson));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_abode_offering_authority_power.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_authority_power.json",
            """
            {
              "entries": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AbodeOfferingResolution_UsesValidatedSnapshotRequestInsteadOfCurrentDrift()
    {
        const string preTurnGuardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Pre-turn guardian before offering."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        const string currentGuardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Current guardian after offering."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "offering_evt_snapshot",
              "guardianId": "guardian_alpha",
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_snapshot",
              "title": "Дар из snapshot принят",
              "summary": "Validation must anchor to the validated snapshot request.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "soul_relic",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "relicId": "relic_snapshot",
                "relicName": "Реликвия из snapshot",
                "relicRarity": "Rare"
              }
            }
          ]
        }
        """;

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", currentGuardiansJson);
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """);
        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_journal_snapshot_001",
              "eventId": "offering_evt_snapshot",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_snapshot",
              "title": "Дар из snapshot принят",
              "summary": "Validation must anchor to the validated snapshot request.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "soul_relic",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "relicId": "relic_snapshot",
                "relicName": "Реликвия из snapshot",
                "relicRarity": "Rare"
              }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianAbodeOfferingState.PendingRequestPath, """
        {
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "offeringType": "soul_relic",
          "returnCycleId": "cycle_12",
          "relicId": "relic_current_drift",
          "relicName": "Дрейфующая реликвия",
          "relicRarity": "Rare",
          "createdAtUtc": "2026-03-28T00:00:00Z"
        }
        """);

        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_abode_offering_snapshot_request.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "soul_relic",
              "returnCycleId": "cycle_12",
              "relicId": "relic_snapshot",
              "relicName": "Реликвия из snapshot",
              "relicRarity": "Rare",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_abode_offering_snapshot.json",
            """
            {
              "currentRealm": "Chaos Sea",
              "soulRelics": [
                {
                  "relicId": "relic_snapshot",
                  "name": "Реликвия из snapshot"
                }
              ]
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_abode_offering_snapshot.json",
            NormalizeGuardianStateJson(preTurnGuardiansJson));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_abode_offering_snapshot.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_offering_snapshot.json",
            """
            {
              "entries": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_missing_validated_snapshot_request", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_missing_matching_power_event", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.FilePath, "game_state/meta/guardians.json.guardians", StringComparison.OrdinalIgnoreCase) &&
            issue.Message.Contains("kernel-authoritative guardian state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AbodeOfferingResolution_SoulRelicWithoutPreTurnOwnershipFailsExplicitly()
    {
        const string preTurnGuardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Pre-turn guardian before relic offering."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        const string currentGuardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Current guardian after fabricated relic offering."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "offering_evt_missing_preturn_relic",
              "guardianId": "guardian_alpha",
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_missing_preturn_relic",
              "title": "Fabricated relic offering",
              "summary": "Journal and power exist, but pre-turn ownership is missing.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "soul_relic",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "relicId": "relic_missing_preturn",
                "relicName": "Несуществующая реликвия",
                "relicRarity": "Rare"
              }
            }
          ]
        }
        """;

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", currentGuardiansJson);
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """);
        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_journal_missing_preturn_relic_001",
              "eventId": "offering_evt_missing_preturn_relic",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_missing_preturn_relic",
              "title": "Fabricated relic offering",
              "summary": "Journal proof alone must not be enough.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "soul_relic",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "relicId": "relic_missing_preturn",
                "relicName": "Несуществующая реликвия",
                "relicRarity": "Rare"
              }
            }
          ]
        }
        """);

        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_abode_offering_missing_preturn_relic_request.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "soul_relic",
              "returnCycleId": "cycle_12",
              "relicId": "relic_missing_preturn",
              "relicName": "Несуществующая реликвия",
              "relicRarity": "Rare",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_missing_preturn_relic.json",
            """
            {
              "currentRealm": "Chaos Sea",
              "soulRelics": {
                "stored": [],
                "equipped": []
              }
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_missing_preturn_relic.json",
            NormalizeGuardianStateJson(preTurnGuardiansJson));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_missing_preturn_relic.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_missing_preturn_relic.json",
            """
            {
              "entries": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_soul_relic_missing_preturn_ownership", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_missing_matching_power_event", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_expected_power_gain_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianTradeResolution_RejectsGuardianDriftOutsideTradeSurfaces()
    {
        const string preTurnGuardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Authority guardian before trade resolution."
              },
              "manifestationHistory": [],
              "mood": { "current": "focused", "intensity": 40, "reason": "Authority baseline before trade.", "since": 10 },
              "abode": { "abodeId": "abode_alpha", "title": "Обитель Азалии" },
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        const string currentGuardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Materialized guardian drift changes unrelated mood while keeping a matching trade inventory."
              },
              "manifestationHistory": [],
              "mood": { "current": "distorted", "intensity": 95, "reason": "Materialized guardian drift outside trade write-set.", "since": 12 },
              "abode": { "abodeId": "abode_alpha", "title": "Обитель Азалии" },
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] },
              "tradeInventory": {
                "tradeCycleId": "cycle_12",
                "generatedAtUtc": "2026-03-28T00:00:00Z",
                "generationReputationTier": "neutral",
                "pricingReputationTier": "neutral",
                "effectiveRarityCeilingBonusSteps": 0,
                "projectBonusSignature": "0|0|0",
                "items": [
                  { "itemId": "offer_1" },
                  { "itemId": "offer_2" },
                  { "itemId": "offer_3" },
                  { "itemId": "offer_4" }
                ]
              },
              "tradeInventoryReceipts": [
                {
                  "requestId": "trade_req_guardian_drift",
                  "guardianId": "guardian_alpha",
                  "guardianName": "Азалия",
                  "abodeId": "abode_alpha",
                  "tradeCycleId": "cycle_12",
                  "status": "ready",
                  "itemCount": 4,
                  "resolvedAtTurn": 12,
                  "resolvedAtUtc": "2026-03-28T00:00:00Z"
                }
              ]
            }
          ]
        }
        """;

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", currentGuardiansJson);
        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianTradeRequestState.PendingRequestPath,
            "test_backups/preturn_guardian_trade_guardian_drift.json",
            """
            {
              "requestId": "trade_req_guardian_drift",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "abodeId": "abode_alpha",
              "returnCycleId": "cycle_12",
              "currentReputation": 30,
              "derivedTradeSlotCount": 4,
              "effectiveRarityCeilingBonusSteps": 0,
              "projectBonusSignature": "0|0|0",
              "createdAtTurn": 12,
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_trade_guardian_drift.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_guardian_trade_guardian_drift.json",
            preTurnGuardiansJson);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_missing_guardian_resolution", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_missing_inventory_resolution", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_missing_receipt_resolution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ArchiveConsultationResolution_StaleManifestDoesNotReusePreTurnRequest()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "afterlifeArchive": {
            "stored": [],
            "actionReceipts": []
          }
        }
        """);

        await WriteRawAsync(AfterlifeArchiveActionState.ConsultationRequestPath, """
        {
          "requestId": "archive_consult_stale",
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "archiveId": "archive_lore_001",
          "archiveTitle": "Летопись Серого Двора",
          "archiveEntryType": "lore_fragment",
          "archiveRarity": "Rare",
          "archiveSourceKind": "codex",
          "targetIncarnation": 2,
          "createdAtTurn": 12,
          "createdAtUtc": "2026-03-28T00:00:00Z",
          "requestedMode": "consultation"
        }
        """);

        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            AfterlifeArchiveActionState.ConsultationRequestPath,
            "test_backups/preturn_archive_consultation_stale.json",
            """
            {
              "requestId": "archive_consult_stale",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "archiveId": "archive_lore_001",
              "archiveTitle": "Летопись Серого Двора",
              "archiveEntryType": "lore_fragment",
              "archiveRarity": "Rare",
              "archiveSourceKind": "codex",
              "targetIncarnation": 2,
              "createdAtTurn": 12,
              "createdAtUtc": "2026-03-28T00:00:00Z",
              "requestedMode": "consultation"
            }
            """);

        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "archive_consultation_request_invalid_validated_snapshot_context", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "archive_consultation_request_missing_resolution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianResonanceValidation_TamperedRollbackBackupFailsClosedBeforeDomainSpecificProof()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Guardian for resonance validation."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "UpdateGuardians": [
            {
              "command": "updateReputation",
              "guardianId": "guardian_alpha",
              "reputationChange": 10,
              "reason": "Validated snapshot favor outcome"
            }
          ]
        }
        """);
        await EnsureValidatedPreTurnGuardiansSnapshotAsync(await _fs.ReadFileAsync("game_state/meta/guardians.json") ?? "{}");
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_tampered_resonance.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_resonance_wrong_turn.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await WriteRawAsync("test_backups/preturn_abode_power_journal_tampered_resonance.json", """
        {
          "entries": [
            {
              "eventId": "resonance_evt_001",
              "guardianId": "guardian_alpha",
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_001",
              "title": "Resonance already present only in tampered backup",
              "summary": "Tampered backup should not suppress the new resonance event.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "lifeId": "life_001",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "resonance_journal_001",
              "eventId": "resonance_evt_001",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_001",
              "title": "Resonance on non-life-evaluation turn",
              "summary": "This must still count as a new resonance event.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "lifeId": "life_001",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "client_owned_pending_snapshot_manifest_modified", StringComparison.OrdinalIgnoreCase) ||
            issue.Message.Contains("readable validated pre-turn guardians baseline", StringComparison.Ordinal));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_wrong_turn_context", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianResonanceValidation_StaleLifeEvaluationManifestRaisesSnapshotContextError()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Guardian for stale manifest resonance validation."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await EnsureValidatedPreTurnGuardiansSnapshotAsync(await _fs.ReadFileAsync("game_state/meta/guardians.json") ?? "{}");
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_stale_life_eval.json",
            """
            {
              "entries": []
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["sourceLabel"] = LifeEvaluationRewardAnalyzer.AutomaticLifeEvaluationSourceLabel;
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "resonance_journal_stale_manifest_001",
              "eventId": "resonance_evt_stale_manifest",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_stale_manifest",
              "title": "Stale life-evaluation manifest must not authorize resonance",
              "summary": "Validated current manifest is required.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "lifeId": "life_stale_manifest",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_validated_snapshot_context", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_wrong_turn_context", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianResonanceValidation_StaleSnapshotJournalCannotSuppressSnapshotContextError()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Guardian for stale snapshot resonance suppression test."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await EnsureValidatedPreTurnGuardiansSnapshotAsync(await _fs.ReadFileAsync("game_state/meta/guardians.json") ?? "{}");
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_stale_duplicate_resonance.json",
            """
            {
              "entries": [
                {
                  "eventId": "resonance_evt_stale_duplicate",
                  "guardianId": "guardian_alpha",
                  "delta": 7,
                  "reasonType": "resonance",
                  "sourceSurface": "life_evaluation",
                  "sourceId": "life_eval_stale_duplicate",
                  "title": "Stale snapshot entry must not suppress current resonance validation",
                  "summary": "This event exists only in the stale snapshot.",
                  "visibility": "player_known",
                  "appliedAt": "2026-03-28T00:00:00Z",
                  "audit": {
                    "lifeId": "life_stale_duplicate"
                  }
                }
              ]
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["sourceLabel"] = LifeEvaluationRewardAnalyzer.AutomaticLifeEvaluationSourceLabel;
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "resonance_journal_stale_duplicate_001",
              "eventId": "resonance_evt_stale_duplicate",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_stale_duplicate",
              "title": "Stale snapshot entry must not suppress current resonance validation",
              "summary": "Current validator must fail closed on unusable snapshot context.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "lifeId": "life_stale_duplicate",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_validated_snapshot_context", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianResonanceValidation_DifferentLifeIdsDoNotTriggerDuplicateForSameLife()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Guardian for resonance duplicate-by-life validation."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 47, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await EnsureValidatedPreTurnGuardiansSnapshotAsync(await _fs.ReadFileAsync("game_state/meta/guardians.json") ?? "{}");
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_resonance_duplicate_by_life.json",
            """
            {
              "entries": []
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["sourceLabel"] = LifeEvaluationRewardAnalyzer.AutomaticLifeEvaluationSourceLabel;
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "resonance_journal_duplicate_by_life_001",
              "eventId": "resonance_evt_duplicate_by_life_001",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_duplicate_by_life_001",
              "title": "First life-scoped resonance event",
              "summary": "Different lives should not collide in duplicate guard.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "lifeId": "life_duplicate_scope_001",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            },
            {
              "entryId": "resonance_journal_duplicate_by_life_002",
              "eventId": "resonance_evt_duplicate_by_life_002",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_duplicate_by_life_002",
              "title": "Second life-scoped resonance event",
              "summary": "Same guardian on a different life must remain allowed.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "lifeId": "life_duplicate_scope_002",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_duplicate_for_same_life", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_current_journal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianResonanceValidation_PreTurnSameLifeDuplicateWithNewEventIdFails()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Guardian for pre-turn resonance duplicate validation."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await EnsureValidatedPreTurnGuardiansSnapshotAsync(await _fs.ReadFileAsync("game_state/meta/guardians.json") ?? "{}");
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_resonance_existing_same_life.json",
            """
            {
              "entries": [
                {
                  "entryId": "resonance_journal_existing_same_life_001",
                  "eventId": "resonance_evt_existing_same_life_001",
                  "guardianId": "guardian_alpha",
                  "guardianName": "Азалия",
                  "turn": 11,
                  "delta": 7,
                  "reasonType": "resonance",
                  "sourceSurface": "life_evaluation",
                  "sourceId": "life_eval_existing_same_life",
                  "title": "Existing resonance for the same life",
                  "summary": "Pre-turn baseline already contains a resonance for this completed life.",
                  "visibility": "player_known",
                  "appliedAt": "2026-03-27T00:00:00Z",
                  "audit": {
                    "lifeId": "life_duplicate_from_baseline",
                    "domainAlignment": 8,
                    "worldScale": 7,
                    "permanence": 6,
                    "sacrifice": 5,
                    "publicImpact": 4,
                    "resonanceScore": 30,
                    "classification": "meaningful resonance",
                    "finalDelta": 7
                  }
                }
              ]
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_resonance_same_life.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["sourceLabel"] = LifeEvaluationRewardAnalyzer.AutomaticLifeEvaluationSourceLabel;
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "resonance_journal_existing_same_life_001",
              "eventId": "resonance_evt_existing_same_life_001",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 11,
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_existing_same_life",
              "title": "Existing resonance for the same life",
              "summary": "Pre-turn baseline already contains a resonance for this completed life.",
              "visibility": "player_known",
              "appliedAt": "2026-03-27T00:00:00Z",
              "audit": {
                "lifeId": "life_duplicate_from_baseline",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            },
            {
              "entryId": "resonance_journal_duplicate_from_baseline_002",
              "eventId": "resonance_evt_duplicate_from_baseline_002",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_duplicate_from_baseline",
              "title": "Second resonance for the same life must fail",
              "summary": "A new eventId must not bypass one-resonance-per-life enforcement.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "lifeId": "life_duplicate_from_baseline",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_duplicate_for_same_life", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianResonanceValidation_CarriedForwardSameLifeDuplicateStillFailsWithoutNewEvent()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Guardian for carried-forward resonance duplicate validation."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await EnsureValidatedPreTurnGuardiansSnapshotAsync(await _fs.ReadFileAsync("game_state/meta/guardians.json") ?? "{}");
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        const string duplicateCurrentJournal = """
        {
          "entries": [
            {
              "entryId": "resonance_journal_same_life_carried_001",
              "eventId": "resonance_evt_same_life_carried_001",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 11,
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_same_life_carried_001",
              "title": "Carried first resonance",
              "summary": "Current state already contains duplicate resonance for the same life.",
              "visibility": "player_known",
              "appliedAt": "2026-03-27T00:00:00Z",
              "audit": {
                "lifeId": "life_carried_duplicate",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            },
            {
              "entryId": "resonance_journal_same_life_carried_002",
              "eventId": "resonance_evt_same_life_carried_002",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 11,
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_same_life_carried_002",
              "title": "Carried second resonance",
              "summary": "No new event this turn should still leave duplicate-by-life invalid.",
              "visibility": "player_known",
              "appliedAt": "2026-03-27T00:00:00Z",
              "audit": {
                "lifeId": "life_carried_duplicate",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            }
          ]
        }
        """;

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_resonance_carried_duplicate.json",
            duplicateCurrentJournal);
        await WriteRawAsync(GuardianPowerEventState.JournalPath, duplicateCurrentJournal);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_resonance_carried_duplicate.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["sourceLabel"] = LifeEvaluationRewardAnalyzer.AutomaticLifeEvaluationSourceLabel;
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_duplicate_for_same_life", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_current_journal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianResonanceValidation_ReusedBaselineEventIdFailsCurrentJournalProof()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Guardian for reused eventId resonance validation."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await EnsureValidatedPreTurnGuardiansSnapshotAsync(await _fs.ReadFileAsync("game_state/meta/guardians.json") ?? "{}");
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_resonance_reused_event_id.json",
            """
            {
              "entries": [
                {
                  "entryId": "resonance_journal_reused_event_existing_001",
                  "eventId": "resonance_evt_reused_identity",
                  "guardianId": "guardian_alpha",
                  "guardianName": "Азалия",
                  "turn": 11,
                  "delta": 7,
                  "reasonType": "resonance",
                  "sourceSurface": "life_evaluation",
                  "sourceId": "life_eval_reused_identity_existing",
                  "title": "Existing baseline resonance",
                  "summary": "This baseline entry must remain append-only.",
                  "visibility": "player_known",
                  "appliedAt": "2026-03-27T00:00:00Z",
                  "audit": {
                    "lifeId": "life_reused_identity_existing",
                    "domainAlignment": 8,
                    "worldScale": 7,
                    "permanence": 6,
                    "sacrifice": 5,
                    "publicImpact": 4,
                    "resonanceScore": 30,
                    "classification": "meaningful resonance",
                    "finalDelta": 7
                  }
                }
              ]
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_resonance_reused_event_id.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["sourceLabel"] = LifeEvaluationRewardAnalyzer.AutomaticLifeEvaluationSourceLabel;
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "resonance_journal_reused_event_current_002",
              "eventId": "resonance_evt_reused_identity",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_reused_identity_current",
              "title": "Reused eventId must not hide a new resonance",
              "summary": "Changing the baseline entry identity under the same eventId must fail append-only proof.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "lifeId": "life_reused_identity_current",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_current_journal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianResonanceValidation_MalformedValidatedSnapshotJournalRaisesExplicitBaselineError()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Guardian for malformed snapshot journal resonance validation."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await EnsureValidatedPreTurnGuardiansSnapshotAsync(await _fs.ReadFileAsync("game_state/meta/guardians.json") ?? "{}");
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_invalid_resonance.json",
            "{");

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_invalid_resonance.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "resonance_journal_invalid_baseline_001",
              "eventId": "resonance_evt_invalid_baseline",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_invalid_baseline",
              "title": "Malformed validated journal must not behave as empty baseline",
              "summary": "Strict resonance proof requires a readable validated journal baseline.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "lifeId": "life_invalid_baseline",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_validated_snapshot_journal", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_wrong_turn_context", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianResonanceValidation_ParseableButInvalidValidatedSnapshotJournalRaisesExplicitBaselineError()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Guardian for parseable-but-invalid snapshot journal resonance validation."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await EnsureValidatedPreTurnGuardiansSnapshotAsync(await _fs.ReadFileAsync("game_state/meta/guardians.json") ?? "{}");
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_parseable_invalid_resonance.json",
            """
            {
              "entries": [
                {
                  "eventId": "invalid_baseline_entry_without_audit"
                }
              ]
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_parseable_invalid_resonance.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "resonance_journal_parseable_invalid_baseline_001",
              "eventId": "resonance_evt_parseable_invalid_baseline",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_parseable_invalid_baseline",
              "title": "Parseable invalid baseline must not count as usable resonance proof",
              "summary": "Strict resonance proof requires a canonical validated journal baseline.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "lifeId": "life_parseable_invalid_baseline",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_validated_snapshot_journal", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_wrong_turn_context", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianResonanceValidation_ParseableButInvalidCurrentJournalRaisesExplicitCurrentProofError()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Guardian for invalid current resonance journal proof."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await EnsureValidatedPreTurnGuardiansSnapshotAsync(await _fs.ReadFileAsync("game_state/meta/guardians.json") ?? "{}");
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_invalid_current_resonance.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_invalid_current_resonance.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "resonance_journal_invalid_current_001",
              "eventId": "resonance_evt_invalid_current_journal",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_invalid_current_journal",
              "title": "Parseable invalid current journal must not count as resonance proof",
              "summary": "Strict resonance proof requires canonical current journal entries.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "lifeId": "life_invalid_current_journal"
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_current_journal", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_wrong_turn_context", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianResonanceValidation_ValidatedBaselineDoesNotReuseCurrentOnlyGuardianCreateKnowledge()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "UpdateGuardians": [
            {
              "command": "create",
              "data": {
                "guardianId": "guardian_new",
                "canonicalName": "Лира",
                "nameVariants": { "default": "Лира", "feminine": "Лира", "masculine": "Лира", "neutral": "Лира" },
                "manifestation": {
                  "currentDisplayName": "Лира",
                  "formFlexibility": "selective",
                  "currentPresentationStyle": "feminine",
                  "currentPronouns": "она/её",
                  "appearanceDescription": "Guardian exists only through same-turn authorized create."
                },
                "manifestationHistory": [],
                "domain": "Tide",
                "abode": { "abodeId": "abode_new", "title": "Тихий прилив" },
                "personalityProfile": {
                  "archetype": "Tide Keeper",
                  "speechPattern": "Measured and tidal",
                  "coreValues": [ "balance", "memory", "patience" ]
                },
                "relationshipData": { "currentReputation": 25, "reputationHistory": [], "lastInteraction": null },
                "abodePower": { "currentPower": 18, "tier": "Угасающая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                "guardianRelationships": [],
                "mood": {
                  "current": "focused",
                  "intensity": 40,
                  "reason": "A newly formed purpose.",
                  "since": 22
                },
                "loreFragments": [
                  { "fragmentId": "guardian_new_lore_1", "category": "personal_history", "title": "Вход в прилив", "content": null, "requiredReputation": 0 },
                  { "fragmentId": "guardian_new_lore_2", "category": "cosmic_secret", "title": "Тайна глубины", "content": null, "requiredReputation": 50 },
                  { "fragmentId": "guardian_new_lore_3", "category": "domain_mastery", "title": "Узел течений", "content": null, "requiredReputation": 130 },
                  { "fragmentId": "guardian_new_lore_4", "category": "lost_world", "title": "Затонувший берег", "content": null, "requiredReputation": 230 },
                  { "fragmentId": "guardian_new_lore_5", "category": "other_guardians", "title": "Имена в пене", "content": null, "requiredReputation": 0 },
                  { "fragmentId": "guardian_new_lore_6", "category": "soul_mechanics", "title": "Память соли", "content": null, "requiredReputation": 50 },
                  { "fragmentId": "guardian_new_lore_7", "category": "personal_history", "title": "Возвращение волны", "content": null, "requiredReputation": 130 }
                ],
                "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
              }
            }
          ]
        }
        """);
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_snapshot_only_create_knowledge.json",
            """
            {
              "guardians": []
            }
            """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_snapshot_only_create_knowledge.json",
            """
            {
              "entries": [
                {
                  "entryId": "resonance_journal_snapshot_only_create_knowledge_pre",
                  "eventId": "resonance_evt_snapshot_only_create_knowledge_pre",
                  "turn": 11,
                  "guardianId": "guardian_new",
                  "guardianName": "Лира",
                  "delta": 5,
                  "reasonType": "resonance",
                  "sourceSurface": "life_evaluation",
                  "sourceId": "life_eval_snapshot_only_create_knowledge_pre",
                  "title": "Pre-turn journal baseline must use validated guardian knowledge only",
                  "summary": "Current-only guardian create must not rescue the validated journal baseline.",
                  "visibility": "player_known",
                  "appliedAt": "2026-03-27T00:00:00Z",
                  "audit": {
                    "lifeId": "life_snapshot_only_create_knowledge_pre",
                    "domainAlignment": 7,
                    "worldScale": 7,
                    "permanence": 6,
                    "sacrifice": 5,
                    "publicImpact": 4,
                    "resonanceScore": 29,
                    "classification": "meaningful resonance",
                    "finalDelta": 5
                  }
                }
              ]
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_snapshot_only_political_knowledge.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["sourceLabel"] = LifeEvaluationRewardAnalyzer.AutomaticLifeEvaluationSourceLabel;
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "resonance_journal_snapshot_only_create_knowledge_current",
              "eventId": "resonance_evt_snapshot_only_create_knowledge_current",
              "guardianId": "guardian_new",
              "guardianName": "Лира",
              "turn": 12,
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_snapshot_only_create_knowledge_current",
              "title": "Current resonance proof should not rescue invalid validated baseline guardian knowledge",
              "summary": "Validated baseline must stay snapshot-self-contained.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "lifeId": "life_snapshot_only_create_knowledge_current",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);
        var issueCodes = string.Join(", ", issues.Select(issue => issue.Code));

        Assert.True(
            issues.Any(issue => string.Equals(issue.Code, "guardian_resonance_invalid_validated_snapshot_journal", StringComparison.OrdinalIgnoreCase)),
            issueCodes);
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_wrong_turn_context", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianResonanceValidation_HistoricalPoliticalBaselineDoesNotRequireSnapshotTrackerForNonPoliticalProof()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Guardian for invalid tracker snapshot proof knowledge."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await EnsureValidatedPreTurnGuardiansSnapshotAsync(await _fs.ReadFileAsync("game_state/meta/guardians.json") ?? "{}");

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_tracker_parseable_invalid_resonance.json",
            """
            {
              "completedProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_invalid_tracker"
                  }
                }
              ]
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_invalid_tracker_knowledge.json",
            """
            {
              "entries": [
                {
                  "entryId": "political_pre_invalid_tracker_001",
                  "eventId": "political_evt_pre_invalid_tracker_001",
                  "guardianId": "guardian_alpha",
                  "guardianName": "Азалия",
                  "turn": 11,
                  "delta": 3,
                  "reasonType": "project_completion",
                  "sourceSurface": "completeGuardianProjects",
                  "sourceId": "proj_invalid_tracker",
                  "title": "Political baseline should require canonical tracker snapshot",
                  "summary": "Parseable tracker metadata must not count as snapshot proof knowledge.",
                  "visibility": "player_known",
                  "appliedAt": "2026-03-27T00:00:00Z",
                  "audit": {
                    "projectGuardianId": "guardian_alpha",
                    "projectId": "proj_invalid_tracker",
                    "projectName": "Тонкая интрига",
                    "projectType": "offensive_intrigue",
                    "projectTier": "minor",
                    "finalState": "Completed"
                  }
                }
              ]
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_invalid_tracker_knowledge.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["sourceLabel"] = LifeEvaluationRewardAnalyzer.AutomaticLifeEvaluationSourceLabel;
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "resonance_invalid_tracker_knowledge_current",
              "eventId": "resonance_evt_invalid_tracker_knowledge_current",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_invalid_tracker_knowledge_current",
              "title": "Historical political baseline must not force tracker for non-political resonance proof",
              "summary": "Unrelated pre-turn political entries must not taint non-political resonance proof with tracker dependency.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "lifeId": "life_invalid_tracker_knowledge_current",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_validated_snapshot_tracker", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianResonanceValidation_UnrelatedSnapshotRawPoliticalEventsDoNotRequireSnapshotTrackerForNonPoliticalProof()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        const string currentGuardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Guardian for unrelated snapshot raw political resonance regression."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        await WriteRawAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson(currentGuardiansJson));

        var snapshotGuardiansRoot = JsonNode.Parse(NormalizeGuardianStateJson(currentGuardiansJson))!.AsObject();
        snapshotGuardiansRoot["guardianPowerEvents"] = JsonNode.Parse("""
        [
          {
            "eventId": "snapshot_political_evt_for_nonpolitical_resonance",
            "guardianId": "guardian_alpha",
            "delta": 1,
            "reasonType": "project_completion",
            "sourceSurface": "completeGuardianProjects",
            "sourceId": "proj_snapshot_political_for_nonpolitical_resonance",
            "title": "Snapshot political raw event should not taint non-political resonance proof",
            "summary": "Proof-relevant resonance validation must ignore unrelated snapshot political raw events.",
            "visibility": "player_known",
            "appliedAt": "2026-03-27T00:00:00Z",
            "audit": {
              "projectGuardianId": "guardian_alpha",
              "projectId": "proj_snapshot_political_for_nonpolitical_resonance",
              "projectName": "Политический фон",
              "projectType": "offensive_intrigue",
              "projectTier": "minor",
              "finalState": "Completed"
            }
          }
        ]
        """);
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_snapshot_raw_political_nonpolitical_resonance.json",
            snapshotGuardiansRoot.ToJsonString());

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_tracker_snapshot_raw_political_nonpolitical_resonance.json",
            "{ invalid tracker");

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_snapshot_raw_political_nonpolitical_resonance.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_snapshot_raw_political_nonpolitical_resonance.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["sourceLabel"] = LifeEvaluationRewardAnalyzer.AutomaticLifeEvaluationSourceLabel;
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "resonance_snapshot_raw_political_current",
              "eventId": "resonance_evt_snapshot_raw_political_current",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_snapshot_raw_political_current",
              "title": "Unrelated snapshot political raw events must not require tracker for resonance proof",
              "summary": "Snapshot raw political history must not taint non-political resonance proof with tracker dependency.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "lifeId": "life_snapshot_raw_political_current",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_validated_snapshot_tracker", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianResonanceValidation_NonArraySnapshotGuardianPowerEventsInvalidateValidatedSnapshotGuardians()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        const string currentGuardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Guardian for non-array snapshot guardianPowerEvents resonance regression."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        await WriteRawAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson(currentGuardiansJson));

        var snapshotGuardiansRoot = JsonNode.Parse(NormalizeGuardianStateJson(currentGuardiansJson))!.AsObject();
        snapshotGuardiansRoot["guardianPowerEvents"] = new JsonObject();
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_snapshot_non_array_power_events_resonance.json",
            snapshotGuardiansRoot.ToJsonString());

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_snapshot_non_array_power_events_resonance.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["sourceLabel"] = LifeEvaluationRewardAnalyzer.AutomaticLifeEvaluationSourceLabel;
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "resonance_snapshot_non_array_power_events_current",
              "eventId": "resonance_evt_snapshot_non_array_power_events_current",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_snapshot_non_array_power_events_current",
              "title": "Non-array snapshot guardianPowerEvents must invalidate guardians proof knowledge",
              "summary": "Pre-turn resonance proof must not silently skip malformed guardianPowerEvents surfaces.",
              "category": "other",
              "content": "Snapshot guardianPowerEvents must invalidate guardians proof knowledge.",
              "discoveredAt": "2026-03-28T00:00:00Z",
              "discoveryContext": "life_evaluation",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "lifeId": "life_snapshot_non_array_power_events_current",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_validated_snapshot_guardians", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "guardian_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_validated_snapshot_journal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianResonanceValidation_NonPoliticalBaselineDoesNotRequireSnapshotTrackerCommandSurface()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Guardian for invalid tracker command surface resonance regression."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await EnsureValidatedPreTurnGuardiansSnapshotAsync(await _fs.ReadFileAsync("game_state/meta/guardians.json") ?? "{}");

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_tracker_invalid_command_surface_resonance.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [],
              "guardianProjectUpdates": [
                {
                  "guardianId": "guardian_alpha",
                  "projectId": "proj_missing_from_snapshot"
                }
              ]
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_invalid_tracker_command_surface_resonance.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_invalid_tracker_command_surface_resonance.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["sourceLabel"] = LifeEvaluationRewardAnalyzer.AutomaticLifeEvaluationSourceLabel;
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "resonance_invalid_tracker_command_surface_current",
              "eventId": "resonance_evt_invalid_tracker_command_surface_current",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_invalid_tracker_command_surface_current",
              "title": "Current resonance proof must not depend on tracker command surfaces",
              "summary": "Non-political resonance proof should not require snapshot tracker command surfaces.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "lifeId": "life_invalid_tracker_command_surface_current",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_validated_snapshot_tracker", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianResonanceValidation_ValidatedBaselineDoesNotReuseInvalidGuardianPowerEventSnapshotSurface()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        const string currentGuardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Guardian for invalid snapshot guardianPowerEvents resonance regression."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        await WriteRawAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson(currentGuardiansJson));

        var invalidSnapshotRoot = JsonNode.Parse(NormalizeGuardianStateJson(currentGuardiansJson))!.AsObject();
        invalidSnapshotRoot["guardianPowerEvents"] = JsonNode.Parse("""
        [
          {
            "eventId": "snapshot_invalid_resonance_power_event",
            "guardianId": "guardian_alpha",
            "delta": "invalid",
            "reasonType": "resonance",
            "sourceSurface": "life_evaluation",
            "sourceId": "life_eval_snapshot_invalid_resonance_power_event",
            "title": "Snapshot guardian power event must stay canonical",
            "summary": "Resonance proof knowledge must reject invalid snapshot guardianPowerEvents.",
            "visibility": "player_known",
            "appliedAt": "2026-03-28T00:00:00Z",
            "audit": {
              "lifeId": "life_snapshot_invalid_resonance_power_event",
              "domainAlignment": 8,
              "worldScale": 7,
              "permanence": 6,
              "sacrifice": 5,
              "publicImpact": 4,
              "resonanceScore": 30,
              "classification": "meaningful resonance",
              "finalDelta": 7
            }
          }
        ]
        """);
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_invalid_power_events_resonance.json",
            invalidSnapshotRoot.ToJsonString());

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_invalid_power_events_resonance.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_invalid_power_events_resonance.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["sourceLabel"] = LifeEvaluationRewardAnalyzer.AutomaticLifeEvaluationSourceLabel;
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "resonance_invalid_snapshot_power_event_current",
              "eventId": "resonance_evt_invalid_snapshot_power_event_current",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_invalid_snapshot_power_event_current",
              "title": "Current resonance proof must not rescue invalid snapshot guardianPowerEvents",
              "summary": "Guardian snapshot proof must validate guardianPowerEvents too.",
              "category": "other",
              "content": "Invalid snapshot guardianPowerEvents must not rescue current resonance proof.",
              "discoveredAt": "2026-03-28T00:00:00Z",
              "discoveryContext": "life_evaluation",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "lifeId": "life_invalid_snapshot_power_event_current",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_validated_snapshot_guardians", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_validated_snapshot_journal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianResonanceValidation_MissingValidatedTrackerSnapshotEntryRaisesTrackerProofError()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Guardian for missing snapshot tracker resonance regression."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await EnsureValidatedPreTurnGuardiansSnapshotAsync(await _fs.ReadFileAsync("game_state/meta/guardians.json") ?? "{}");

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();
        await RemoveTrackedFileFromCurrentPendingTurnSnapshotAsync(GuardianProjectState.TrackerPath);

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_missing_tracker_snapshot_resonance.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_missing_tracker_snapshot_resonance.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["sourceLabel"] = LifeEvaluationRewardAnalyzer.AutomaticLifeEvaluationSourceLabel;
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "resonance_missing_tracker_snapshot_current",
              "eventId": "resonance_evt_missing_tracker_snapshot_current",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_missing_tracker_snapshot_current",
              "title": "Current resonance proof must not downgrade missing tracker snapshot to journal corruption",
              "summary": "Snapshot tracker entry is mandatory proof knowledge, not optional journal context.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "lifeId": "life_missing_tracker_snapshot_current",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_validated_snapshot_tracker", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "guardian_power_event_missing_validated_preturn_tracker_snapshot", StringComparison.OrdinalIgnoreCase) ||
            issue.Message.Contains("validated pre-turn project tracker baseline", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_validated_snapshot_journal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianResonanceValidation_CurrentTrackerAuthorityNotRequiredForNonPoliticalResonanceProof()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Guardian for current tracker authority resonance regression."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await EnsureValidatedPreTurnGuardiansSnapshotAsync(await _fs.ReadFileAsync("game_state/meta/guardians.json") ?? "{}");

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();
        await WriteRawAsync(GuardianProjectState.TrackerPath, "{");

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_current_tracker_authority_resonance.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_current_tracker_authority_resonance.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["sourceLabel"] = LifeEvaluationRewardAnalyzer.AutomaticLifeEvaluationSourceLabel;
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "resonance_current_tracker_authority_current",
              "eventId": "resonance_evt_current_tracker_authority_current",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_current_tracker_authority_current",
              "title": "Current resonance proof must not depend on tracker authority",
              "summary": "Canonical non-political resonance proof should not be blocked by unrelated tracker authority failures.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "lifeId": "life_current_tracker_authority_current",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_current_tracker_authority", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_current_journal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianResonanceValidation_UnrelatedInvalidRawPowerEventsDoNotBlockNonPoliticalJournalProof()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Guardian with unrelated invalid raw political power event."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "invalid_raw_political_for_resonance",
              "guardianId": "guardian_alpha",
              "delta": 3,
              "reasonType": "project_completion",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "project_invalid_raw_for_resonance",
              "title": "Invalid raw political event should not block non-political resonance proof",
              "summary": "Raw invalid power-event authority must not poison non-political journal proof.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "projectGuardianId": "guardian_alpha",
                "projectName": "Broken political payload",
                "projectType": "offensive_intrigue",
                "projectTier": "minor",
                "finalState": "Completed"
              }
            }
          ]
        }
        """);
        await EnsureValidatedPreTurnGuardiansSnapshotAsync(await _fs.ReadFileAsync("game_state/meta/guardians.json") ?? "{}");

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_unrelated_invalid_raw_resonance.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_unrelated_invalid_raw_resonance.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["sourceLabel"] = LifeEvaluationRewardAnalyzer.AutomaticLifeEvaluationSourceLabel;
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "resonance_unrelated_invalid_raw_current",
              "eventId": "resonance_evt_unrelated_invalid_raw_current",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_unrelated_invalid_raw_current",
              "title": "Non-political resonance proof must ignore unrelated invalid raw political events",
              "summary": "Current resonance journal proof should not map unrelated raw power-event failure to guardian authority failure.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "lifeId": "life_unrelated_invalid_raw_current",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_current_guardian_authority", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_current_journal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianResonanceValidation_CurrentGuardianAuthorityFailureRaisesGuardianSpecificProofError()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await EnsureValidatedPreTurnGuardiansSnapshotAsync("""
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Validated guardian for current guardian authority resonance regression."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", "{");
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_current_guardian_authority_resonance.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_current_guardian_authority_resonance.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["sourceLabel"] = LifeEvaluationRewardAnalyzer.AutomaticLifeEvaluationSourceLabel;
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "resonance_current_guardian_authority_current",
              "eventId": "resonance_evt_current_guardian_authority_current",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_current_guardian_authority_current",
              "title": "Current resonance proof must point at guardian authority failure",
              "summary": "Canonical current journal should not be silently ignored when guardian authority is unavailable.",
              "category": "other",
              "content": "Canonical current journal should not be silently ignored when guardian authority is unavailable.",
              "discoveredAt": "2026-03-28T00:00:00Z",
              "discoveryContext": "life_evaluation",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "lifeId": "life_current_guardian_authority_current",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_current_guardian_authority", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.FilePath, "game_state/meta/guardians.json", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_current_journal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianResonanceValidation_UnreadableCurrentJournalRaisesExplicitCurrentProofError()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Guardian for unreadable current resonance journal proof."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await EnsureValidatedPreTurnGuardiansSnapshotAsync(await _fs.ReadFileAsync("game_state/meta/guardians.json") ?? "{}");
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_unreadable_current_resonance.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_unreadable_current_resonance.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["sourceLabel"] = LifeEvaluationRewardAnalyzer.AutomaticLifeEvaluationSourceLabel;
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync(GuardianPowerEventState.JournalPath, "{");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_current_journal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianResonanceValidation_UnreadableCurrentJournalFailsClosedEvenWithUnusableSnapshotContext()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Guardian for unreadable resonance journal with unusable snapshot context."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await EnsureValidatedPreTurnGuardiansSnapshotAsync(await _fs.ReadFileAsync("game_state/meta/guardians.json") ?? "{}");
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_unusable_snapshot_context_resonance.json",
            """
            {
              "entries": []
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["manifestPayloadHash"] = "stale";
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync(GuardianPowerEventState.JournalPath, "{");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_current_journal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianResonanceValidation_ParseableButInvalidCurrentJournalFailsClosedEvenWithUnusableSnapshotContext()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Guardian for parseable invalid resonance journal with unusable snapshot context."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await EnsureValidatedPreTurnGuardiansSnapshotAsync(await _fs.ReadFileAsync("game_state/meta/guardians.json") ?? "{}");
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_parseable_invalid_unusable_context_resonance.json",
            """
            {
              "entries": []
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["manifestPayloadHash"] = "stale";
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "eventId": "resonance_evt_parseable_invalid_unusable_context",
              "guardianId": "guardian_alpha"
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_current_journal", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_validated_snapshot_context", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianResonanceValidation_AuditInvalidCurrentJournalBeatsUnusableSnapshotContext()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Guardian for audit-invalid resonance journal with unusable snapshot context."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await EnsureValidatedPreTurnGuardiansSnapshotAsync(await _fs.ReadFileAsync("game_state/meta/guardians.json") ?? "{}");
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_audit_invalid_unusable_context_resonance.json",
            """
            {
              "entries": []
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["manifestPayloadHash"] = "stale";
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "resonance_audit_invalid_unusable_context",
              "eventId": "resonance_evt_audit_invalid_unusable_context",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 5,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_audit_invalid_unusable_context",
              "title": "Audit-invalid resonance entry must beat snapshot-context failure",
              "summary": "Missing finalDelta should be treated as current journal proof failure before stale snapshot context.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "lifeId": "life_audit_invalid_unusable_context",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance"
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_current_journal", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_validated_snapshot_context", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnAbodeOffering_UsesValidatedSnapshotRequestInsteadOfCurrentDrift()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: ABODE_OFFERING] 100 Чернильных Перьев"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Current guardian after accepted-turn offering."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_journal_accepted_snapshot_001",
              "eventId": "offering_evt_accepted_snapshot",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_accepted_snapshot",
              "title": "Accepted-turn offering from validated snapshot",
              "summary": "Current request drift must be ignored.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianAbodeOfferingState.PendingRequestPath, """
        {
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "offeringType": "ink_feathers",
          "inkFeathersOffered": 50,
          "returnCycleId": "cycle_12",
          "createdAtUtc": "2026-03-28T00:00:00Z"
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_accepted_snapshot.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "ink_feathers",
              "inkFeathersOffered": 100,
              "returnCycleId": "cycle_12",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_accepted_offering_snapshot.json",
            """
            {
              "guardians": [
                {
                  "guardianId": "guardian_alpha",
                  "canonicalName": "Азалия",
                  "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
                  "manifestation": {
                    "currentDisplayName": "Азалия",
                    "formFlexibility": "selective",
                    "currentPresentationStyle": "feminine",
                    "currentPronouns": "она/её",
                    "appearanceDescription": "Pre-turn guardian before offering."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_accepted_offering_snapshot.json",
            """
            {
              "entries": []
            }
            """);
        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_accepted_offering_snapshot.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "ABODE_OFFERING",
          "resolutionType": "abodeOffering",
          "summary": "Validated snapshot request should win over current drifted request.",
          "resolved": true,
          "costInFeathers": 100,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "powerGain": 2,
            "returnCycleId": "cycle_12",
            "powerEventId": "offering_evt_accepted_snapshot",
            "affectedFiles": [
              "game_state/meta/guardians.json",
              "game_state/meta/guardian_abode_power_journal.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_missing_validated_snapshot_request", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_cost_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_power_gain_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_missing_power_event_journal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnAbodeOffering_ParseableButInvalidValidatedGuardiansSnapshotRaisesSnapshotDataError()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: ABODE_OFFERING] 100 Чернильных Перьев"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Current guardian after accepted-turn offering with invalid validated guardians baseline."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_journal_parseable_invalid_guardian_snapshot_001",
              "eventId": "offering_evt_parseable_invalid_guardian_snapshot",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_parseable_invalid_guardian_snapshot",
              "title": "Accepted-turn offering with invalid guardian snapshot baseline",
              "summary": "Parseable-but-invalid guardian baseline must not authorize abode power proof.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianAbodeOfferingState.PendingRequestPath, """
        {
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "offeringType": "ink_feathers",
          "inkFeathersOffered": 100,
          "returnCycleId": "cycle_12",
          "createdAtUtc": "2026-03-28T00:00:00Z"
        }
        """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_parseable_invalid_guardian_snapshot.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "ink_feathers",
              "inkFeathersOffered": 100,
              "returnCycleId": "cycle_12",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_parseable_invalid_accepted_offering_snapshot.json",
            """
            {
              "guardians": [
                {
                  "guardianId": "guardian_alpha",
                  "abodePower": { "currentPower": 40 }
                }
              ]
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_parseable_invalid_guardian_snapshot.json",
            """
            {
              "entries": []
            }
            """);
        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_parseable_invalid_guardian_snapshot.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "ABODE_OFFERING",
          "resolutionType": "abodeOffering",
          "summary": "Parseable invalid guardian snapshot must not authorize accepted-turn offering power proof.",
          "resolved": true,
          "costInFeathers": 100,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "powerGain": 2,
            "returnCycleId": "cycle_12",
            "powerEventId": "offering_evt_parseable_invalid_guardian_snapshot",
            "affectedFiles": [
              "game_state/meta/guardians.json",
              "game_state/meta/guardian_abode_power_journal.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_action_invalid_validated_snapshot_data", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_power_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnAbodeOffering_SoulRelicWithoutPreTurnOwnershipFailsExplicitly()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: ABODE_OFFERING] Offer relic"
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Current guardian after fabricated accepted-turn relic offering."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "offering_evt_accepted_missing_preturn_relic",
              "guardianId": "guardian_alpha",
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_accepted_missing_preturn_relic",
              "title": "Accepted-turn fabricated relic offering",
              "summary": "Outcome must require pre-turn relic ownership.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "soul_relic",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "relicId": "relic_accepted_missing_preturn",
                "relicName": "Отсутствующая реликвия",
                "relicRarity": "Rare"
              }
            }
          ]
        }
        """);
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_journal_accepted_missing_preturn_relic_001",
              "eventId": "offering_evt_accepted_missing_preturn_relic",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_accepted_missing_preturn_relic",
              "title": "Accepted-turn fabricated relic offering",
              "summary": "Journal proof alone must not be enough.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "soul_relic",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "relicId": "relic_accepted_missing_preturn",
                "relicName": "Отсутствующая реликвия",
                "relicRarity": "Rare"
              }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianAbodeOfferingState.PendingRequestPath, """
        {
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "offeringType": "soul_relic",
          "returnCycleId": "cycle_12",
          "relicId": "relic_accepted_missing_preturn",
          "relicName": "Отсутствующая реликвия",
          "relicRarity": "Rare",
          "createdAtUtc": "2026-03-28T00:00:00Z"
        }
        """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_accepted_missing_preturn_relic.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "soul_relic",
              "returnCycleId": "cycle_12",
              "relicId": "relic_accepted_missing_preturn",
              "relicName": "Отсутствующая реликвия",
              "relicRarity": "Rare",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_accepted_missing_preturn_relic.json",
            NormalizeGuardianStateJson("""
            {
              "guardians": [
                {
                  "guardianId": "guardian_alpha",
                  "canonicalName": "Азалия",
                  "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
                  "manifestation": {
                    "currentDisplayName": "Азалия",
                    "formFlexibility": "selective",
                    "currentPresentationStyle": "feminine",
                    "currentPronouns": "она/её",
                    "appearanceDescription": "Pre-turn guardian before fabricated accepted-turn relic offering."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_accepted_missing_preturn_relic.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_accepted_missing_preturn_relic.json",
            """
            {
              "currentRealm": "Chaos Sea",
              "soulRelics": {
                "stored": [],
                "equipped": []
              }
            }
            """);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "ABODE_OFFERING",
          "resolutionType": "abodeOffering",
          "summary": "Accepted-turn offering must require pre-turn relic ownership.",
          "resolved": true,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "powerGain": 2,
            "returnCycleId": "cycle_12",
            "powerEventId": "offering_evt_accepted_missing_preturn_relic",
            "affectedFiles": [
              "game_state/meta/guardians.json",
              "game_state/meta/guardian_abode_power_journal.json",
              "game_state/meta/soul_state.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_soul_relic_missing_preturn_ownership", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_missing_power_event_journal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnAbodeOffering_SoulRelicMetadataMustMatchConsumedPreTurnRelic()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: ABODE_OFFERING] Offer relic"
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "soulRelics": {
            "stored": [],
            "equipped": []
          }
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Current guardian after forged relic offering metadata."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 44, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "offering_evt_relic_metadata_mismatch",
              "guardianId": "guardian_alpha",
              "delta": 4,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_relic_metadata_mismatch",
              "title": "Forged relic metadata",
              "summary": "Offering proof must bind authored metadata to the actually consumed pre-turn relic.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "soul_relic",
                "returnCycleId": "cycle_12",
                "baseDelta": 4,
                "finalDelta": 4,
                "relicId": "relic_metadata_mismatch",
                "relicName": "Фальшивый осколок",
                "relicRarity": "Legendary"
              }
            }
          ]
        }
        """);
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_journal_relic_metadata_mismatch_001",
              "eventId": "offering_evt_relic_metadata_mismatch",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 4,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_relic_metadata_mismatch",
              "title": "Forged relic metadata",
              "summary": "Request and journal agree, but the consumed pre-turn relic has different canonical metadata.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "soul_relic",
                "returnCycleId": "cycle_12",
                "baseDelta": 4,
                "finalDelta": 4,
                "relicId": "relic_metadata_mismatch",
                "relicName": "Фальшивый осколок",
                "relicRarity": "Legendary"
              }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianAbodeOfferingState.PendingRequestPath, """
        {
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "offeringType": "soul_relic",
          "returnCycleId": "cycle_12",
          "relicId": "relic_metadata_mismatch",
          "relicName": "Фальшивый осколок",
          "relicRarity": "Legendary",
          "createdAtUtc": "2026-03-28T00:00:00Z"
        }
        """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_relic_metadata_mismatch.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "soul_relic",
              "returnCycleId": "cycle_12",
              "relicId": "relic_metadata_mismatch",
              "relicName": "Фальшивый осколок",
              "relicRarity": "Legendary",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_relic_metadata_mismatch.json",
            NormalizeGuardianStateJson("""
            {
              "guardians": [
                {
                  "guardianId": "guardian_alpha",
                  "canonicalName": "Азалия",
                  "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
                  "manifestation": {
                    "currentDisplayName": "Азалия",
                    "formFlexibility": "selective",
                    "currentPresentationStyle": "feminine",
                    "currentPronouns": "она/её",
                    "appearanceDescription": "Pre-turn guardian before relic metadata mismatch offering."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_relic_metadata_mismatch.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_relic_metadata_mismatch.json",
            """
            {
              "currentRealm": "Chaos Sea",
              "soulRelics": {
                "stored": [
                  {
                    "relicId": "relic_metadata_mismatch",
                    "name": "Подлинный осколок",
                    "rarity": "Rare"
                  }
                ],
                "equipped": []
              }
            }
            """);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "ABODE_OFFERING",
          "resolutionType": "abodeOffering",
          "summary": "Offering proof must reject forged relic metadata against the consumed pre-turn relic.",
          "resolved": true,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "powerGain": 4,
            "returnCycleId": "cycle_12",
            "powerEventId": "offering_evt_relic_metadata_mismatch",
            "affectedFiles": [
              "game_state/meta/guardians.json",
              "game_state/meta/abode_power_journal.json",
              "game_state/meta/soul_state.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_soul_relic_metadata_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_missing_power_event_journal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnAbodeOffering_SoulRelicRequiresSoulStateAffectedFile()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: ABODE_OFFERING] Offer relic"
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Current guardian after valid accepted-turn relic offering."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "offering_evt_missing_soul_affected_file",
              "guardianId": "guardian_alpha",
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_missing_soul_affected_file",
              "title": "Accepted-turn valid relic offering",
              "summary": "Proof should still require soul_state in affectedFiles.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "soul_relic",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "relicId": "relic_missing_soul_affected_file",
                "relicName": "Реликвия без affectedFiles soul_state",
                "relicRarity": "Rare"
              }
            }
          ]
        }
        """);
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_journal_missing_soul_affected_file_001",
              "eventId": "offering_evt_missing_soul_affected_file",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_missing_soul_affected_file",
              "title": "Accepted-turn valid relic offering",
              "summary": "Everything is valid except missing soul_state in affectedFiles.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "soul_relic",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "relicId": "relic_missing_soul_affected_file",
                "relicName": "Реликвия без affectedFiles soul_state",
                "relicRarity": "Rare"
              }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianAbodeOfferingState.PendingRequestPath, """
        {
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "offeringType": "soul_relic",
          "returnCycleId": "cycle_12",
          "relicId": "relic_missing_soul_affected_file",
          "relicName": "Реликвия без affectedFiles soul_state",
          "relicRarity": "Rare",
          "createdAtUtc": "2026-03-28T00:00:00Z"
        }
        """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_missing_soul_affected_file.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "soul_relic",
              "returnCycleId": "cycle_12",
              "relicId": "relic_missing_soul_affected_file",
              "relicName": "Реликвия без affectedFiles soul_state",
              "relicRarity": "Rare",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_missing_soul_affected_file.json",
            NormalizeGuardianStateJson("""
            {
              "guardians": [
                {
                  "guardianId": "guardian_alpha",
                  "canonicalName": "Азалия",
                  "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
                  "manifestation": {
                    "currentDisplayName": "Азалия",
                    "formFlexibility": "selective",
                    "currentPresentationStyle": "feminine",
                    "currentPronouns": "она/её",
                    "appearanceDescription": "Pre-turn guardian before valid accepted-turn relic offering."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_missing_soul_affected_file.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_missing_soul_affected_file.json",
            """
            {
              "currentRealm": "Chaos Sea",
              "soulRelics": [
                {
                  "relicId": "relic_missing_soul_affected_file",
                  "name": "Реликвия без affectedFiles soul_state"
                }
              ]
            }
            """);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "ABODE_OFFERING",
          "resolutionType": "abodeOffering",
          "summary": "Accepted-turn relic offering must include soul_state in affected files.",
          "resolved": true,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "powerGain": 2,
            "returnCycleId": "cycle_12",
            "powerEventId": "offering_evt_missing_soul_affected_file",
            "affectedFiles": [
              "game_state/meta/guardians.json",
              "game_state/meta/guardian_abode_power_journal.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_missing_soul_state_affected_file", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_missing_power_event_journal", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_current_soul_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnAbodeOffering_MalformedJournalEntryDoesNotSatisfyStrictProof()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: ABODE_OFFERING] 100 Чернильных Перьев"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Current guardian after accepted-turn offering with malformed journal."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_journal_malformed_accepted_001",
              "eventId": "offering_evt_malformed_accepted",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_malformed_accepted",
              "title": "Malformed accepted-turn offering journal",
              "summary": "Missing finalDelta must block accepted-turn proof.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_accepted_malformed_journal.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "ink_feathers",
              "inkFeathersOffered": 100,
              "returnCycleId": "cycle_12",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_accepted_offering_malformed_journal.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_accepted_offering_malformed_journal.json",
            NormalizeGuardianStateJson("""
            {
              "guardians": [
                {
                  "guardianId": "guardian_alpha",
                  "canonicalName": "Азалия",
                  "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
                  "manifestation": {
                    "currentDisplayName": "Азалия",
                    "formFlexibility": "selective",
                    "currentPresentationStyle": "feminine",
                    "currentPronouns": "она/её",
                    "appearanceDescription": "Pre-turn guardian before malformed accepted-turn offering journal."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_accepted_malformed_journal.json",
            """
            {
              "entries": []
            }
            """);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "ABODE_OFFERING",
          "resolutionType": "abodeOffering",
          "summary": "Malformed journal must not satisfy accepted-turn offering proof.",
          "resolved": true,
          "costInFeathers": 100,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "powerGain": 2,
            "returnCycleId": "cycle_12",
            "powerEventId": "offering_evt_malformed_accepted",
            "affectedFiles": [
              "game_state/meta/guardians.json",
              "game_state/meta/abode_power_journal.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_current_journal_proof", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnAbodeOffering_InvalidCurrentJournalBeatsBrokenTrackerAuthority()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: ABODE_OFFERING] 100 Чернильных Перьев"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Current guardian after accepted-turn offering with malformed journal and broken tracker authority."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();
        await WriteRawAsync(GuardianProjectState.TrackerPath, "{");

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_journal_invalid_beats_tracker_001",
              "eventId": "offering_evt_invalid_beats_tracker",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_invalid_beats_tracker",
              "title": "Malformed accepted-turn offering journal should beat broken tracker authority",
              "summary": "Missing finalDelta must be classified as current journal proof failure before tracker authority failure.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_invalid_beats_tracker.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "ink_feathers",
              "inkFeathersOffered": 100,
              "returnCycleId": "cycle_12",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_accepted_offering_invalid_beats_tracker.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_accepted_offering_invalid_beats_tracker.json",
            NormalizeGuardianStateJson("""
            {
              "guardians": [
                {
                  "guardianId": "guardian_alpha",
                  "canonicalName": "Азалия",
                  "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
                  "manifestation": {
                    "currentDisplayName": "Азалия",
                    "formFlexibility": "selective",
                    "currentPresentationStyle": "feminine",
                    "currentPronouns": "она/её",
                    "appearanceDescription": "Pre-turn guardian before malformed accepted-turn offering journal with broken tracker authority."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_accepted_invalid_beats_tracker.json",
            """
            {
              "entries": []
            }
            """);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "ABODE_OFFERING",
          "resolutionType": "abodeOffering",
          "summary": "Malformed journal must beat broken tracker authority in accepted-turn offering proof.",
          "resolved": true,
          "costInFeathers": 100,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "powerGain": 2,
            "returnCycleId": "cycle_12",
            "powerEventId": "offering_evt_invalid_beats_tracker",
            "affectedFiles": [
              "game_state/meta/guardians.json",
              "game_state/meta/abode_power_journal.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_current_journal_proof", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_current_tracker_authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnAbodeOffering_InvalidValidatedSnapshotSoulRelicLeafFailsAsSoulStateProof()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: ABODE_OFFERING] Offer relic"
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "soulRelics": {
            "stored": [],
            "equipped": []
          }
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson("""
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Current guardian after relic offering with invalid validated snapshot relic leaf."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """));

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_journal_invalid_snapshot_relic_leaf_001",
              "eventId": "offering_evt_invalid_snapshot_relic_leaf",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_invalid_snapshot_relic_leaf",
              "title": "Offering with invalid validated snapshot relic leaf",
              "summary": "Consumed relic proof must reject semantically invalid pre-turn relic leaf objects.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "soul_relic",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "relicId": "relic_invalid_snapshot_leaf",
                "relicName": "Подлинный осколок",
                "relicRarity": "Rare"
              }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianAbodeOfferingState.PendingRequestPath, """
        {
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "offeringType": "soul_relic",
          "returnCycleId": "cycle_12",
          "relicId": "relic_invalid_snapshot_leaf",
          "relicName": "Подлинный осколок",
          "relicRarity": "Rare",
          "createdAtUtc": "2026-03-28T00:00:00Z"
        }
        """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_invalid_snapshot_relic_leaf.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "soul_relic",
              "returnCycleId": "cycle_12",
              "relicId": "relic_invalid_snapshot_leaf",
              "relicName": "Подлинный осколок",
              "relicRarity": "Rare",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_invalid_snapshot_relic_leaf.json",
            NormalizeGuardianStateJson("""
            {
              "guardians": [
                {
                  "guardianId": "guardian_alpha",
                  "canonicalName": "Азалия",
                  "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
                  "manifestation": {
                    "currentDisplayName": "Азалия",
                    "formFlexibility": "selective",
                    "currentPresentationStyle": "feminine",
                    "currentPronouns": "она/её",
                    "appearanceDescription": "Pre-turn guardian before relic offering with invalid validated snapshot relic leaf."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_invalid_snapshot_relic_leaf.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_invalid_snapshot_relic_leaf.json",
            """
            {
              "currentRealm": "Chaos Sea",
              "soulRelics": {
                "stored": [
                  {
                    "relicId": "relic_invalid_snapshot_leaf",
                    "name": "Подлинный осколок",
                    "rarity": "banana"
                  }
                ],
                "equipped": []
              }
            }
            """);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "ABODE_OFFERING",
          "resolutionType": "abodeOffering",
          "summary": "Accepted-turn relic offering must fail closed on semantically invalid validated snapshot relic leaf.",
          "resolved": true,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "powerGain": 2,
            "returnCycleId": "cycle_12",
            "powerEventId": "offering_evt_invalid_snapshot_relic_leaf",
            "affectedFiles": [
              "game_state/meta/guardians.json",
              "game_state/meta/abode_power_journal.json",
              "game_state/meta/soul_state.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_validated_snapshot_soul_state", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_soul_relic_metadata_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnAbodeOffering_ValidatedSnapshotSoulRelicQualityAliasRemainsCanonicalProof()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: ABODE_OFFERING] Offer relic"
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "soulRelics": {
            "stored": [],
            "equipped": []
          }
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson("""
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Current guardian after accepted-turn relic offering backed by quality alias."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """));

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_journal_quality_alias_relic_001",
              "eventId": "offering_evt_quality_alias_relic",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_quality_alias_relic",
              "title": "Soul Relic offering with quality alias",
              "summary": "Validated snapshot Soul Relic may use canonical quality alias and still satisfy proof.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "soul_relic",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "relicId": "relic_quality_alias",
                "relicName": "Реликвия quality alias",
                "relicRarity": "Rare"
              }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianAbodeOfferingState.PendingRequestPath, """
        {
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "offeringType": "soul_relic",
          "returnCycleId": "cycle_12",
          "relicId": "relic_quality_alias",
          "relicName": "Реликвия quality alias",
          "relicRarity": "Rare",
          "createdAtUtc": "2026-03-28T00:00:00Z"
        }
        """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_quality_alias_relic.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "soul_relic",
              "returnCycleId": "cycle_12",
              "relicId": "relic_quality_alias",
              "relicName": "Реликвия quality alias",
              "relicRarity": "Rare",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_quality_alias_relic.json",
            NormalizeGuardianStateJson("""
            {
              "guardians": [
                {
                  "guardianId": "guardian_alpha",
                  "canonicalName": "Азалия",
                  "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
                  "manifestation": {
                    "currentDisplayName": "Азалия",
                    "formFlexibility": "selective",
                    "currentPresentationStyle": "feminine",
                    "currentPronouns": "она/её",
                    "appearanceDescription": "Pre-turn guardian before accepted-turn relic offering backed by quality alias."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_quality_alias_relic.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_quality_alias_relic.json",
            """
            {
              "currentRealm": "Chaos Sea",
              "soulRelics": {
                "stored": [
                  {
                    "relicId": "relic_quality_alias",
                    "name": "Реликвия quality alias",
                    "quality": "Rare"
                  }
                ],
                "equipped": []
              }
            }
            """);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "ABODE_OFFERING",
          "resolutionType": "abodeOffering",
          "summary": "Soul Relic proof should accept canonical quality alias in validated snapshot soul_state.",
          "resolved": true,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "powerGain": 2,
            "returnCycleId": "cycle_12",
            "powerEventId": "offering_evt_quality_alias_relic",
            "affectedFiles": [
              "game_state/meta/guardians.json",
              "game_state/meta/abode_power_journal.json",
              "game_state/meta/soul_state.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_validated_snapshot_soul_state", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_soul_relic_metadata_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnAbodeOffering_InvalidValidatedSnapshotArchiveLeafFailsAsSoulStateProof()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: ABODE_OFFERING] Offer archive fragment"
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "afterlifeArchive": {
            "stored": [],
            "actionReceipts": []
          }
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson("""
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Current guardian after archive offering with invalid validated snapshot archive leaf."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """));

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_journal_invalid_snapshot_archive_leaf_001",
              "eventId": "offering_evt_invalid_snapshot_archive_leaf",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_invalid_snapshot_archive_leaf",
              "title": "Offering with invalid validated snapshot archive leaf",
              "summary": "Consumed archive proof must reject semantically invalid pre-turn archive leaf objects.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "archive_lore_fragment",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "archiveId": "archive_invalid_snapshot_leaf",
                "archiveTitle": "Свидетельство прилива",
                "archiveEntryType": "lore_fragment",
                "archiveRarity": "Rare"
              }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianAbodeOfferingState.PendingRequestPath, """
        {
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "offeringType": "archive_lore_fragment",
          "returnCycleId": "cycle_12",
          "archiveId": "archive_invalid_snapshot_leaf",
          "archiveTitle": "Свидетельство прилива",
          "archiveEntryType": "lore_fragment",
          "archiveRarity": "Rare",
          "createdAtUtc": "2026-03-28T00:00:00Z"
        }
        """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_invalid_snapshot_archive_leaf.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "archive_lore_fragment",
              "returnCycleId": "cycle_12",
              "archiveId": "archive_invalid_snapshot_leaf",
              "archiveTitle": "Свидетельство прилива",
              "archiveEntryType": "lore_fragment",
              "archiveRarity": "Rare",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_invalid_snapshot_archive_leaf.json",
            NormalizeGuardianStateJson("""
            {
              "guardians": [
                {
                  "guardianId": "guardian_alpha",
                  "canonicalName": "Азалия",
                  "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
                  "manifestation": {
                    "currentDisplayName": "Азалия",
                    "formFlexibility": "selective",
                    "currentPresentationStyle": "feminine",
                    "currentPronouns": "она/её",
                    "appearanceDescription": "Pre-turn guardian before archive offering with invalid validated snapshot archive leaf."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_invalid_snapshot_archive_leaf.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_invalid_snapshot_archive_leaf.json",
            """
            {
              "currentRealm": "Chaos Sea",
              "afterlifeArchive": {
                "stored": [
                  {
                    "archiveId": "archive_invalid_snapshot_leaf",
                    "entryType": "banana",
                    "title": "Свидетельство прилива",
                    "summary": "Неканонический тип записи должен ломать consumed-entry proof.",
                    "rarity": "Rare",
                    "sourceLife": 1,
                    "acquiredAtUtc": "2026-03-24T00:00:00Z"
                  }
                ],
                "actionReceipts": []
              }
            }
            """);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "ABODE_OFFERING",
          "resolutionType": "abodeOffering",
          "summary": "Accepted-turn archive offering must fail closed on semantically invalid validated snapshot archive leaf.",
          "resolved": true,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "powerGain": 2,
            "returnCycleId": "cycle_12",
            "powerEventId": "offering_evt_invalid_snapshot_archive_leaf",
            "affectedFiles": [
              "game_state/meta/guardians.json",
              "game_state/meta/abode_power_journal.json",
              "game_state/meta/soul_state.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_validated_snapshot_soul_state", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_archive_entry_metadata_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianResonanceValidation_SnapshotGuardianPowerEventsMustNotReuseSnapshotJournalEventId()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        const string currentGuardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Current guardian for snapshot guardianPowerEvents identity regression."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;
        await WriteRawAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson(currentGuardiansJson));

        var invalidSnapshotRoot = JsonNode.Parse(NormalizeGuardianStateJson(currentGuardiansJson))!.AsObject();
        invalidSnapshotRoot["guardianPowerEvents"] = JsonNode.Parse("""
        [
          {
            "eventId": "snapshot_evt_conflict_with_snapshot_journal",
            "guardianId": "guardian_alpha",
            "delta": 7,
            "reasonType": "resonance",
            "sourceSurface": "life_evaluation",
            "sourceId": "life_eval_snapshot_conflict",
            "title": "Snapshot guardianPowerEvents must not reuse snapshot journal eventId",
            "summary": "Validated snapshot guardians baseline must validate raw power-event identity against snapshot journal.",
            "visibility": "player_known",
            "appliedAt": "2026-03-28T00:00:00Z",
            "audit": {
              "lifeId": "life_snapshot_conflict",
              "domainAlignment": 8,
              "worldScale": 7,
              "permanence": 6,
              "sacrifice": 5,
              "publicImpact": 4,
              "resonanceScore": 30,
              "classification": "meaningful resonance",
              "finalDelta": 7
            }
          }
        ]
        """);
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_snapshot_identity_conflict_resonance.json",
            invalidSnapshotRoot.ToJsonString());

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_snapshot_identity_conflict_resonance.json",
            """
            {
              "entries": [
                {
                  "entryId": "snapshot_journal_identity_conflict_001",
                  "eventId": "snapshot_evt_conflict_with_snapshot_journal",
                  "guardianId": "guardian_alpha",
                  "guardianName": "Азалия",
                  "turn": 11,
                  "delta": 7,
                  "reasonType": "resonance",
                  "sourceSurface": "life_evaluation",
                  "sourceId": "life_eval_snapshot_conflict_old",
                  "title": "Existing snapshot resonance event",
                  "summary": "Snapshot guardianPowerEvents must not reuse this append-only eventId.",
                  "visibility": "player_known",
                  "appliedAt": "2026-03-27T00:00:00Z",
                  "audit": {
                    "lifeId": "life_snapshot_conflict_old",
                    "domainAlignment": 8,
                    "worldScale": 7,
                    "permanence": 6,
                    "sacrifice": 5,
                    "publicImpact": 4,
                    "resonanceScore": 30,
                    "classification": "meaningful resonance",
                    "finalDelta": 7
                  }
                }
              ]
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_snapshot_identity_conflict_resonance.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["sourceLabel"] = LifeEvaluationRewardAnalyzer.AutomaticLifeEvaluationSourceLabel;
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "resonance_snapshot_identity_conflict_current",
              "eventId": "resonance_evt_snapshot_identity_conflict_current",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_snapshot_identity_conflict_current",
              "title": "Current resonance should not rescue invalid snapshot raw identity",
              "summary": "Snapshot guardian baseline must reject raw guardianPowerEvents that reuse snapshot journal eventId.",
              "category": "other",
              "content": "Snapshot guardian baseline must reject raw guardianPowerEvents that reuse snapshot journal eventId.",
              "discoveredAt": "2026-03-28T00:00:00Z",
              "discoveryContext": "life_evaluation",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "lifeId": "life_snapshot_identity_conflict_current",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_validated_snapshot_guardians", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnAbodeOffering_HistoricalPoliticalJournalEntriesDoNotRequireTrackerForNonPoliticalProof()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: ABODE_OFFERING] 100 Чернильных Перьев"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson("""
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Current guardian after offering with unrelated historical political entries and broken tracker."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """));
        await WriteRawAsync(GuardianProjectState.TrackerPath, "{ invalid tracker");

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "journal_historical_political_before_offering",
              "eventId": "evt_historical_political_before_offering",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 11,
              "delta": 2,
              "reasonType": "project_completion",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "project_historical_before_offering",
              "title": "Historical political entry",
              "summary": "Unrelated historical political entries must not taint non-political offering proof.",
              "visibility": "player_known",
              "appliedAt": "2026-03-27T00:00:00Z",
              "audit": {
                "projectGuardianId": "guardian_alpha",
                "projectId": "project_historical_before_offering",
                "projectName": "Старая интрига",
                "projectType": "offensive_intrigue",
                "projectTier": "minor",
                "finalState": "Completed"
              }
            },
            {
              "entryId": "journal_offering_after_historical_political",
              "eventId": "offering_evt_historical_political_irrelevant",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_historical_political_irrelevant",
              "title": "Offering after historical political entry",
              "summary": "Offering proof should ignore unrelated historical political tracker dependency.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_historical_political_irrelevant.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "ink_feathers",
              "inkFeathersOffered": 100,
              "returnCycleId": "cycle_12",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_historical_political_irrelevant.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_historical_political_irrelevant.json",
            NormalizeGuardianStateJson("""
            {
              "guardians": [
                {
                  "guardianId": "guardian_alpha",
                  "canonicalName": "Азалия",
                  "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
                  "manifestation": {
                    "currentDisplayName": "Азалия",
                    "formFlexibility": "selective",
                    "currentPresentationStyle": "feminine",
                    "currentPronouns": "она/её",
                    "appearanceDescription": "Pre-turn guardian before offering with unrelated historical political entries."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_historical_political_irrelevant.json",
            """
            {
              "entries": [
                {
                  "entryId": "journal_historical_political_before_offering",
                  "eventId": "evt_historical_political_before_offering",
                  "guardianId": "guardian_alpha",
                  "guardianName": "Азалия",
                  "turn": 11,
                  "delta": 2,
                  "reasonType": "project_completion",
                  "sourceSurface": "completeGuardianProjects",
                  "sourceId": "project_historical_before_offering",
                  "title": "Historical political entry",
                  "summary": "Unrelated historical political entries must not taint non-political offering proof.",
                  "visibility": "player_known",
                  "appliedAt": "2026-03-27T00:00:00Z",
                  "audit": {
                    "projectGuardianId": "guardian_alpha",
                    "projectId": "project_historical_before_offering",
                    "projectName": "Старая интрига",
                    "projectType": "offensive_intrigue",
                    "projectTier": "minor",
                    "finalState": "Completed"
                  }
                }
              ]
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_tracker_historical_political_irrelevant.json",
            "{ invalid snapshot tracker");

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "ABODE_OFFERING",
          "resolutionType": "abodeOffering",
          "summary": "Historical political entries must not force tracker authority for non-political offering proof.",
          "resolved": true,
          "costInFeathers": 100,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "powerGain": 2,
            "returnCycleId": "cycle_12",
            "powerEventId": "offering_evt_historical_political_irrelevant",
            "affectedFiles": [
              "game_state/meta/guardians.json",
              "game_state/meta/abode_power_journal.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_validated_snapshot_tracker", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_current_tracker_authority", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_current_journal_proof", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnAbodeOffering_JournalAuditMismatchDoesNotSatisfyStrictProof()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: ABODE_OFFERING] 100 Чернильных Перьев"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Current guardian after offering with mismatched journal audit."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_journal_audit_mismatch_001",
              "eventId": "offering_evt_audit_mismatch",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_audit_mismatch",
              "title": "Audit mismatch offering journal",
              "summary": "Request-aware matcher must reject wrong offering audit even with matching event id and delta.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "inkFeathersOffered": 50,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_accepted_audit_mismatch.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "ink_feathers",
              "inkFeathersOffered": 100,
              "returnCycleId": "cycle_12",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_accepted_offering_audit_mismatch.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_accepted_offering_audit_mismatch.json",
            NormalizeGuardianStateJson("""
            {
              "guardians": [
                {
                  "guardianId": "guardian_alpha",
                  "canonicalName": "Азалия",
                  "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
                  "manifestation": {
                    "currentDisplayName": "Азалия",
                    "formFlexibility": "selective",
                    "currentPresentationStyle": "feminine",
                    "currentPronouns": "она/её",
                    "appearanceDescription": "Pre-turn guardian before accepted offering audit mismatch."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_accepted_audit_mismatch.json",
            """
            {
              "entries": []
            }
            """);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "ABODE_OFFERING",
          "resolutionType": "abodeOffering",
          "summary": "Accepted-turn offering proof must be request-aware, not just event-aware.",
          "resolved": true,
          "costInFeathers": 100,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "powerGain": 2,
            "returnCycleId": "cycle_12",
            "powerEventId": "offering_evt_audit_mismatch",
            "affectedFiles": [
              "game_state/meta/guardians.json",
              "game_state/meta/abode_power_journal.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_current_journal_proof", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnAbodeOffering_MalformedValidatedSnapshotUsesInvalidSnapshotCode()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: ABODE_OFFERING] 100 Чернильных Перьев"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Current guardian for malformed snapshot accepted-turn test."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": []
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_accepted_invalid_snapshot.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName":
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_accepted_offering_invalid_snapshot.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_accepted_offering_invalid_snapshot.json",
            """
            {
              "guardians": [
                {
                  "guardianId": "guardian_alpha",
                  "canonicalName": "Азалия",
                  "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
                  "manifestation": {
                    "currentDisplayName": "Азалия",
                    "formFlexibility": "selective",
                    "currentPresentationStyle": "feminine",
                    "currentPronouns": "она/её",
                    "appearanceDescription": "Pre-turn guardian before malformed snapshot accepted-turn test."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_accepted_offering_invalid_snapshot.json",
            """
            {
              "entries": []
            }
            """);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "ABODE_OFFERING",
          "resolutionType": "abodeOffering",
          "summary": "Malformed snapshot contract must surface explicit invalid-snapshot code.",
          "resolved": true,
          "costInFeathers": 100,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "powerGain": 2,
            "returnCycleId": "cycle_12",
            "powerEventId": "offering_evt_invalid_snapshot",
            "affectedFiles": [
              "game_state/meta/guardians.json",
              "game_state/meta/guardian_abode_power_journal.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_validated_snapshot_request", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "ink_feather_guardian_action_invalid_validated_snapshot_context", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_missing_validated_snapshot_request", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnGuardianFavor_MissingValidatedGuardianSnapshotFileRaisesSnapshotFileError()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: GUARDIAN_FAVOR] 30 Чернильных Перьев"
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteGuardianRawWithoutValidatedSnapshotAsync("""
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Current guardian without pre-turn snapshot entry."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_favor_missing_guardians_snapshot.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "GUARDIAN_FAVOR",
          "resolutionType": "guardianReputation",
          "summary": "Guardian favor without validated guardians snapshot entry must fail strictly.",
          "resolved": true,
          "costInFeathers": 30,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "reputationChange": 10,
            "affectedFiles": [
              "game_state/meta/guardians.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_action_missing_validated_snapshot_file", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.FilePath, "game_state/meta/guardians.json", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_favor_reputation_missing", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "ink_feather_missing_state_effect", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnGuardianInkFeatherActions_MissingValidatedSnapshotRaiseSnapshotContextError()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        foreach (var scenario in new[]
                 {
                     (ActionTag: "DONATE_TO_GUARDIAN", Feathers: 30),
                     (ActionTag: "GUARDIAN_FAVOR", Feathers: 30),
                     (ActionTag: GuardianAbodeOfferingState.ActionTag, Feathers: 100)
                 })
        {
            await _fs.WriteFileAtomicAsync("input/turn_request.json", $$"""
            {
              "sessionId": "live-session",
              "requestId": "live-request",
              "turnNumber": 12,
              "playerAction": "[INK_FEATHER_ACTION: {{scenario.ActionTag}}] {{scenario.Feathers}} Чернильных Перьев"
            }
            """);

            var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
            var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

            Assert.Contains(issues, issue =>
                string.Equals(issue.Code, "ink_feather_guardian_action_invalid_validated_snapshot_context", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task ForcedGuardianIncarnation_StaleManifestRaisesSnapshotContextError()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Forced incarnation guardian."
              },
              "manifestationHistory": [],
              "abode": { "abodeId": "abode_alpha", "title": "Обитель Азалии" },
              "relationshipData": { "currentReputation": -30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_alpha",
            "canonicalName": "Азалия",
            "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Forced incarnation guardian."
            },
            "manifestationHistory": [],
            "abode": { "abodeId": "abode_alpha", "title": "Обитель Азалии" },
            "relationshipData": { "currentReputation": -30, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          },
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_alpha"
          }
        }
        """);

        await WriteRawAsync("game_state/control/incarnation_trigger.json", """
        {
          "worldDescription": "Новый мир как кара хранителя.",
          "characterDescription": "Душа просыпается в новом теле.",
          "circumstances": "Принудительное воплощение после провокации.",
          "source": "guardian_forced",
          "guardianId": "guardian_alpha",
          "severityBand": "harsh",
          "reason": "Провокация Хранителя",
          "provocationSummary": "Игрок сознательно оскорбил хранителя."
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_forced_incarnation_stale_manifest.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12
        }
        """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["sourceLabel"] = "обработки хода";
        manifest["playerAction"] = "[GUARDIAN_PROVOCATION: guardian_alpha] Я сознательно вывожу хранителя из себя.";
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.TradeOfferingResonance);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "incarnation_trigger_invalid_validated_snapshot_context", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "forced_incarnation_invalid_source_turn", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "forced_incarnation_missing_player_action_provocation_evidence", StringComparison.OrdinalIgnoreCase));
    }

}

