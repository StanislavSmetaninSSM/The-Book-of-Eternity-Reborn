using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ShiningStateValidationTests
{
    [Theory]
    [InlineData("\"malformed_contract\"")]
    [InlineData("[\"malformed_contract\"]")]
    public void ValidateShiningAbodeStateFile_MalformedLegacyPendingNativeFactionDiscovery_RaisesExpectedObject(string pendingDiscoveryJson)
    {
        var root = new JsonObject
        {
            ["availability"] = "active",
            ["radiance"] = new JsonObject
            {
                ["experience"] = 0,
                ["tier"] = 0
            },
            ["lightSparks"] = 50,
            ["halls"] = new JsonArray(),
            ["factions"] = new JsonArray(),
            ["shiningPoliticalActors"] = new JsonArray(),
            ["pendingNativeFactionDiscovery"] = JsonNode.Parse(pendingDiscoveryJson),
            ["factionFoundingReceipts"] = new JsonArray(),
            ["factionRealignmentReceipts"] = new JsonArray(),
            ["coreActionReceipts"] = new JsonArray(),
            ["gates"] = new JsonObject
            {
                ["draftVersion"] = 0,
                ["hasOpenDraft"] = false,
                ["isStale"] = false,
                ["allCandidateBlessingCards"] = new JsonArray(),
                ["availableBlessingCards"] = new JsonArray(),
                ["shownBlessingCardIds"] = new JsonArray(),
                ["selectedBlessingCardIds"] = new JsonArray(),
                ["nextCandidateCursor"] = 0,
                ["rerollsRemaining"] = 0
            },
            ["gachaSystem"] = new JsonObject
            {
                ["chargesPerReturn"] = 0,
                ["chargesUsedThisReturn"] = 0,
                ["currentReturnCycleId"] = string.Empty,
                ["gachaHistory"] = new JsonArray()
            }
        };

        using var document = JsonDocument.Parse(root.ToJsonString());
        var validator = new ValidationService(
            new FileSystemManager(Path.GetTempPath(), NullLogger<FileSystemManager>.Instance),
            NullLogger<ValidationService>.Instance);
        var issues = new List<ValidationIssue>();
        var method = typeof(ValidationService).GetMethod(
            "ValidateShiningAbodeStateFile",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        method!.Invoke(validator, new object[] { document.RootElement, ShiningAbodeState.StatePath, issues });

        Assert.Contains(
            issues,
            issue => string.Equals(issue.Code, "expected_object", StringComparison.OrdinalIgnoreCase) &&
                     issue.FilePath.Contains("pendingNativeFactionDiscovery", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateShiningAbodeStateFile_NullTreasury_RaisesExpectedObject()
    {
        var root = CreateMinimalShiningStateForBlessingCardValidation();
        root[ShiningAbodeState.TreasuryProperty] = null;

        var issues = InvokeShiningStateValidation(root);

        Assert.Contains(
            issues,
            issue => string.Equals(issue.Code, "expected_object", StringComparison.OrdinalIgnoreCase) &&
                     issue.FilePath.Contains(ShiningAbodeState.TreasuryProperty, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateShiningAbodeStateFile_MissingTreasury_RemainsLegacyCompatible()
    {
        var root = CreateMinimalShiningStateForBlessingCardValidation();

        var issues = InvokeShiningStateValidation(root);

        Assert.DoesNotContain(
            issues,
            issue => issue.FilePath.Contains(ShiningAbodeState.TreasuryProperty, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("changed")]
    public async Task ValidateGameStateAsync_PreTurnTreasuryIsClientOwnedAndMustBePreserved(string mutationMode)
    {
        var basePath = Path.Combine(Path.GetTempPath(), $"boe_shining_treasury_validation_{Guid.NewGuid():N}");
        try
        {
            var fs = new FileSystemManager(basePath, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            const string sessionId = "session_shining_treasury_preserve_001";
            const string requestId = "request_shining_treasury_preserve_001";
            const int turnNumber = 42;
            const string snapshotPath = $"game_state/control/pending_turn_snapshot/{ShiningAbodeState.StatePath}";

            var preTurnRoot = CreateMinimalShiningStateForBlessingCardValidation();
            preTurnRoot[ShiningAbodeState.TreasuryProperty] = new JsonObject
            {
                ["depositedInkFeathers"] = 250,
                ["claimableInkFeatherInterest"] = 5,
                ["totalInterestClaimed"] = 10,
                ["lastInterestSettlementCycleId"] = "shining_return_7",
                ["exchangeCycleId"] = "shining_return_7",
                ["exchangeThisCycleLightSparks"] = 2,
                ["exchangeHistory"] = new JsonArray(new JsonObject
                {
                    ["exchangeId"] = "exchange_preserved_001",
                    ["cycleId"] = "shining_return_7",
                    ["inkFeathersSpent"] = 50,
                    ["lightSparksReceived"] = 2,
                    ["rateFeathersPerSpark"] = ShiningAbodeState.TreasuryFeathersPerLightSpark,
                    ["createdAtUtc"] = "2026-05-09T00:00:00Z"
                })
            };

            var currentRoot = preTurnRoot.DeepClone().AsObject();
            if (string.Equals(mutationMode, "missing", StringComparison.OrdinalIgnoreCase))
            {
                currentRoot.Remove(ShiningAbodeState.TreasuryProperty);
            }
            else
            {
                currentRoot[ShiningAbodeState.TreasuryProperty]!["depositedInkFeathers"] = 0;
            }

            var preTurnJson = preTurnRoot.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
            await fs.WriteFileAtomicAsync(snapshotPath, preTurnJson);
            await fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, currentRoot.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            await fs.WriteFileAtomicAsync("input/turn_request.json", JsonSerializer.Serialize(new
            {
                sessionId,
                requestId,
                turnNumber
            }, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

            var manifest = new JsonObject
            {
                ["sessionId"] = sessionId,
                ["requestId"] = requestId,
                ["turnNumber"] = turnNumber,
                ["requestTimestamp"] = "2026-05-09T00:00:00Z",
                ["playerAction"] = "Shining treasury preservation validation test",
                ["files"] = new JsonObject
                {
                    [ShiningAbodeState.StatePath] = snapshotPath
                },
                ["snapshotFileHashes"] = new JsonObject
                {
                    [ShiningAbodeState.StatePath] = PendingTurnSnapshotAuthority.ComputeSha256(preTurnJson)
                },
                ["clientOwnedValidationHashes"] = new JsonObject(),
                ["rollbackBackups"] = new JsonObject(),
                ["rollbackBaselineFiles"] = new JsonArray(),
                ["sourceLabel"] = "shining-treasury-validation-tests",
                ["manifestPayloadHash"] = string.Empty
            };
            manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
            await fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.json", manifest.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(fs);

            var validator = new ValidationService(fs, NullLogger<ValidationService>.Instance);
            var issues = await validator.ValidateGameStateAsync();

            Assert.Contains(
                issues,
                issue => string.Equals(issue.Code, "shining_treasury_client_owned_modified", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(basePath))
                Directory.Delete(basePath, recursive: true);
        }
    }

    [Fact]
    public void ValidateShiningAbodeStateFile_InvalidEnumBackedFields_RaiseExplicitErrors()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": { "experience": 120, "tier": 1 },
          "lightSparks": 40,
          "halls": [],
          "factions": [
            {
              "factionId": "faction_invalid",
              "originType": "broken_origin",
              "hallId": "hall_invalid",
              "charter": {
                "factionName": "Испорченная фракция",
                "favoredArchetype": "broken_archetype",
                "patronEffectFamily": "broken_family",
                "summary": "Тест"
              },
              "leadership": {
                "headActorType": "guardian",
                "headActorId": "guardian_old",
                "leadershipState": "secure"
              },
              "baseStrength": 30,
              "factionStrength": 30,
              "investCountThisAscension": 0,
              "projects": [
                {
                  "projectId": "project_invalid",
                  "displayName": "Ломанный проект",
                  "summary": "Тест",
                  "toneTags": ["broken"],
                  "targetFactionIds": [],
                  "projectArchetype": "broken_project_archetype",
                  "outputEffectFamily": "broken_output_family",
                  "tier": 2,
                  "status": "broken_status",
                  "isSupported": false,
                  "strengthReward": 0
                }
              ],
              "tradeInventoryReceipts": [],
              "leadershipReceipts": [],
              "leadershipHistory": []
            }
          ],
          "shiningPoliticalActors": [],
          "factionFoundingReceipts": [],
          "factionRealignmentReceipts": [],
          "coreActionReceipts": [],
          "gates": {
            "draftVersion": 0,
            "hasOpenDraft": false,
            "isStale": false,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": [],
            "selectedBlessingCardIds": [],
            "nextCandidateCursor": 0,
            "rerollsRemaining": 0
          },
          "gachaSystem": {
            "chargesPerReturn": 0,
            "chargesUsedThisReturn": 0,
            "currentReturnCycleId": "return_1",
            "gachaHistory": []
          }
        }
        """)!.AsObject();

        using var document = JsonDocument.Parse(root.ToJsonString());
        var validator = new ValidationService(
            new FileSystemManager(Path.GetTempPath(), NullLogger<FileSystemManager>.Instance),
            NullLogger<ValidationService>.Instance);
        var issues = new List<ValidationIssue>();
        var method = typeof(ValidationService).GetMethod(
            "ValidateShiningAbodeStateFile",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        method!.Invoke(validator, new object[] { document.RootElement, ShiningAbodeState.StatePath, issues });

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_abode_invalid_origin_type", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_abode_invalid_favored_archetype", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_abode_invalid_patron_effect_family", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_abode_invalid_project_archetype", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_abode_invalid_output_effect_family", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_abode_invalid_project_status", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateShiningAbodeStateFile_ZeroTradePrice_RaisesPositivePriceError()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": { "experience": 120, "tier": 1 },
          "lightSparks": 40,
          "halls": [],
          "factions": [
            {
              "factionId": "faction_trade",
              "originType": "native_radiant",
              "hallId": "hall_trade",
              "charter": {
                "factionName": "Торговый Дом",
                "favoredArchetype": "provision",
                "patronEffectFamily": "resource",
                "summary": "Тест"
              },
              "leadership": {
                "headActorType": "radiant_actor",
                "headActorId": "actor_trade",
                "leadershipState": "secure"
              },
              "baseStrength": 30,
              "factionStrength": 30,
              "investCountThisAscension": 0,
              "projectArchetypesCountedThisAscension": [],
              "projects": [],
              "tradeInventory": {
                "tradeCycleId": "cycle_1",
                "generatedAtUtc": "2026-04-17T01:00:00Z",
                "generationTradeTier": 1,
                "generationRarityCeiling": "common",
                "serviceMultiplierSnapshot": 1.0,
                "merchantProfile": "shining_faction",
                "items": [
                  {
                    "slotId": "slot_zero",
                    "priceInFeathers": 0,
                    "soldOut": false,
                    "relicData": {
                      "relicId": "relic_zero",
                      "name": "Нулевая реликвия",
                      "rarity": "Common",
                      "quality": "Common"
                    }
                  }
                ]
              },
              "tradeInventoryReceipts": [],
              "leadershipReceipts": [],
              "leadershipHistory": []
            }
          ],
          "shiningPoliticalActors": [],
          "factionFoundingReceipts": [],
          "factionRealignmentReceipts": [],
          "coreActionReceipts": [],
          "gates": {
            "draftVersion": 0,
            "hasOpenDraft": false,
            "isStale": false,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": [],
            "selectedBlessingCardIds": [],
            "nextCandidateCursor": 0,
            "rerollsRemaining": 0
          },
          "gachaSystem": {
            "chargesPerReturn": 0,
            "chargesUsedThisReturn": 0,
            "currentReturnCycleId": "return_1",
            "gachaHistory": []
          }
        }
        """)!.AsObject();

        using var document = JsonDocument.Parse(root.ToJsonString());
        var validator = new ValidationService(
            new FileSystemManager(Path.GetTempPath(), NullLogger<FileSystemManager>.Instance),
            NullLogger<ValidationService>.Instance);
        var issues = new List<ValidationIssue>();
        var method = typeof(ValidationService).GetMethod(
            "ValidateShiningAbodeStateFile",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        method!.Invoke(validator, new object[] { document.RootElement, ShiningAbodeState.StatePath, issues });

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "invalid_positive_integer_field", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("priceInFeathers", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateShiningAbodeStateFile_InvalidPoliticalEnums_RaiseExplicitErrors()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": { "experience": 120, "tier": 1 },
          "lightSparks": 40,
          "halls": [],
          "factions": [
            {
              "factionId": "faction_invalid",
              "originType": "ascended_guardian",
              "hallId": "hall_invalid",
              "charter": {
                "factionName": "Испорченная фракция",
                "favoredArchetype": "accord",
                "patronEffectFamily": "social",
                "summary": "Тест"
              },
              "leadership": {
                "headActorType": "guardian",
                "headActorId": "guardian_old",
                "leadershipState": "broken_state"
              },
              "baseStrength": 30,
              "factionStrength": 30,
              "investCountThisAscension": 0,
              "projects": [],
              "tradeInventoryReceipts": [],
              "leadershipReceipts": [],
              "leadershipHistory": []
            }
          ],
          "shiningPoliticalActors": [
            {
              "actorId": "actor_invalid",
              "actorType": "broken_actor_type",
              "displayName": "Ломаный актор",
              "summary": "Тест",
              "originFactionId": "faction_invalid",
              "currentFactionId": "faction_invalid",
              "politicalStatus": "broken_status"
            }
          ],
          "factionFoundingReceipts": [],
          "factionRealignmentReceipts": [],
          "coreActionReceipts": [],
          "gates": {
            "draftVersion": 0,
            "hasOpenDraft": false,
            "isStale": false,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": [],
            "selectedBlessingCardIds": [],
            "nextCandidateCursor": 0,
            "rerollsRemaining": 0
          },
          "gachaSystem": {
            "chargesPerReturn": 0,
            "chargesUsedThisReturn": 0,
            "currentReturnCycleId": "return_1",
            "gachaHistory": []
          }
        }
        """)!.AsObject();

        using var document = JsonDocument.Parse(root.ToJsonString());
        var validator = new ValidationService(
            new FileSystemManager(Path.GetTempPath(), NullLogger<FileSystemManager>.Instance),
            NullLogger<ValidationService>.Instance);
        var issues = new List<ValidationIssue>();
        var method = typeof(ValidationService).GetMethod(
            "ValidateShiningAbodeStateFile",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        method!.Invoke(validator, new object[] { document.RootElement, ShiningAbodeState.StatePath, issues });

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_leadership_invalid_state", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_political_actor_invalid_type", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_political_actor_invalid_status", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateShiningAbodeStateFile_DuplicateReceiptRequestIds_RaiseExplicitErrors()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": { "experience": 120, "tier": 1 },
          "lightSparks": 40,
          "halls": [],
          "factions": [
            {
              "factionId": "faction_alpha",
              "originType": "ascended_guardian",
              "hallId": "hall_alpha",
              "charter": {
                "factionName": "Фракция Альфа",
                "favoredArchetype": "accord",
                "patronEffectFamily": "social",
                "summary": "Тест"
              },
              "leadership": {
                "headActorType": "guardian",
                "headActorId": "guardian_old",
                "leadershipState": "secure"
              },
              "baseStrength": 30,
              "factionStrength": 30,
              "investCountThisAscension": 0,
              "projects": [],
              "tradeInventoryReceipts": [
                { "requestId": "dup_trade", "factionId": "faction_alpha", "tradeCycleId": "cycle_1", "status": "ready", "itemCount": 1, "soldOutCount": 0, "resolvedAtTurn": 1, "resolvedAtUtc": "2026-04-20T00:00:00Z" },
                { "requestId": "dup_trade", "factionId": "faction_alpha", "tradeCycleId": "cycle_1", "status": "ready", "itemCount": 1, "soldOutCount": 0, "resolvedAtTurn": 2, "resolvedAtUtc": "2026-04-20T00:01:00Z" }
              ],
              "leadershipReceipts": [
                { "requestId": "dup_lead", "transitionMode": "peaceful_succession", "status": "accepted", "resolvedAtTurn": 1, "resolvedAtUtc": "2026-04-20T00:00:00Z", "previousHeadActorType": "guardian", "previousHeadActorId": "guardian_old", "newHeadActorType": "resident", "newHeadActorId": "resident_new" },
                { "requestId": "dup_lead", "transitionMode": "peaceful_succession", "status": "accepted", "resolvedAtTurn": 2, "resolvedAtUtc": "2026-04-20T00:01:00Z", "previousHeadActorType": "guardian", "previousHeadActorId": "guardian_old", "newHeadActorType": "resident", "newHeadActorId": "resident_new" }
              ],
              "leadershipHistory": [
                { "requestId": "dup_history", "eventType": "peaceful_succession", "turnNumber": 1, "summary": "old" },
                { "requestId": "dup_history", "eventType": "peaceful_succession", "turnNumber": 2, "summary": "new" }
              ]
            }
          ],
          "shiningPoliticalActors": [],
          "factionFoundingReceipts": [
            { "requestId": "dup_found", "proposedFactionId": "faction_beta", "proposedHallId": "hall_beta", "hallName": "Зал", "factionId": "faction_beta", "hallId": "hall_beta", "status": "accepted", "supportingResidentIds": [], "resolvedAtTurn": 1, "resolvedAtUtc": "2026-04-20T00:00:00Z" },
            { "requestId": "dup_found", "proposedFactionId": "faction_beta", "proposedHallId": "hall_beta", "hallName": "Зал", "factionId": "faction_beta", "hallId": "hall_beta", "status": "accepted", "supportingResidentIds": [], "resolvedAtTurn": 2, "resolvedAtUtc": "2026-04-20T00:01:00Z" }
          ],
          "factionRealignmentReceipts": [
            { "requestId": "dup_realign", "residentId": "resident_alpha", "residentName": "Резидент", "sourceFactionId": "faction_alpha", "targetFactionId": "faction_beta", "status": "accepted", "realignmentMode": "accepted_transfer", "resolvedAtTurn": 1, "resolvedAtUtc": "2026-04-20T00:00:00Z" },
            { "requestId": "dup_realign", "residentId": "resident_alpha", "residentName": "Резидент", "sourceFactionId": "faction_alpha", "targetFactionId": "faction_beta", "status": "accepted", "realignmentMode": "accepted_transfer", "resolvedAtTurn": 2, "resolvedAtUtc": "2026-04-20T00:01:00Z" }
          ],
          "coreActionReceipts": [
            { "requestId": "dup_core", "actionType": "open_gates", "status": "accepted", "selectedCardIds": [], "newResidentIds": [], "seededProjectIds": [], "generatedDraftVersion": 0, "resolvedAtTurn": 1, "resolvedAtUtc": "2026-04-20T00:00:00Z" },
            { "requestId": "dup_core", "actionType": "open_gates", "status": "accepted", "selectedCardIds": [], "newResidentIds": [], "seededProjectIds": [], "generatedDraftVersion": 0, "resolvedAtTurn": 2, "resolvedAtUtc": "2026-04-20T00:01:00Z" }
          ],
          "gates": {
            "draftVersion": 0,
            "hasOpenDraft": false,
            "isStale": false,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": [],
            "selectedBlessingCardIds": [],
            "nextCandidateCursor": 0,
            "rerollsRemaining": 0
          },
          "gachaSystem": {
            "chargesPerReturn": 0,
            "chargesUsedThisReturn": 0,
            "currentReturnCycleId": "return_1",
            "gachaHistory": []
          }
        }
        """)!.AsObject();

        using var document = JsonDocument.Parse(root.ToJsonString());
        var validator = new ValidationService(
            new FileSystemManager(Path.GetTempPath(), NullLogger<FileSystemManager>.Instance),
            NullLogger<ValidationService>.Instance);
        var issues = new List<ValidationIssue>();
        var method = typeof(ValidationService).GetMethod(
            "ValidateShiningAbodeStateFile",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        method!.Invoke(validator, new object[] { document.RootElement, ShiningAbodeState.StatePath, issues });

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_core_action_duplicate_receipt_request_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_founding_duplicate_receipt_request_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_realignment_duplicate_receipt_request_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_trade_duplicate_receipt_request_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_leadership_duplicate_receipt_request_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_leadership_duplicate_history_request_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateShiningAbodeStateFile_DuplicateFactionAndProjectIds_RaiseExplicitErrors()
    {
        var root = CreateMinimalShiningStateForBlessingCardValidation();
        var factionA = CreateFaction("faction_duplicate", CreateSecureLeadership("guardian_a"));
        var factionB = CreateFaction("faction_duplicate", CreateSecureLeadership("guardian_b"));
        factionA["projects"] = new JsonArray(CreateProject("project_duplicate", isSupported: false));
        factionB["projects"] = new JsonArray(CreateProject("project_duplicate", isSupported: false));
        root["factions"] = new JsonArray(factionA, factionB);

        var issues = InvokeShiningStateValidation(root);

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_abode_duplicate_faction_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_abode_duplicate_project_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateShiningAbodeStateFile_SupportedProjectCapExceeded_RaisesExplicitError()
    {
        var root = CreateMinimalShiningStateForBlessingCardValidation();
        root["radiance"] = new JsonObject
        {
            ["experience"] = 120,
            ["tier"] = 1
        };
        var factionA = CreateFaction("faction_support_a", CreateSecureLeadership("guardian_a"));
        var factionB = CreateFaction("faction_support_b", CreateSecureLeadership("guardian_b"));
        factionA["projects"] = new JsonArray(CreateProject("project_support_a", isSupported: true));
        factionB["projects"] = new JsonArray(CreateProject("project_support_b", isSupported: true));
        root["factions"] = new JsonArray(factionA, factionB);

        var issues = InvokeShiningStateValidation(root);

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_abode_supported_project_cap_exceeded", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateShiningAbodeStateFile_InvalidFactionLifecycle_RaisesExplicitError()
    {
        var root = CreateMinimalShiningStateForBlessingCardValidation();
        var faction = CreateFaction("faction_lifecycle_invalid", CreateSecureLeadership("guardian_a"));
        faction["factionLifecycle"] = new JsonObject
        {
            ["state"] = "conquered"
        };
        root["factions"] = new JsonArray(faction);

        var issues = InvokeShiningStateValidation(root);

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_faction_lifecycle_invalid_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateShiningAbodeStateFile_DefeatedFactionWithActiveSurfaces_RaisesExplicitErrors()
    {
        var root = CreateMinimalShiningStateForBlessingCardValidation();
        var faction = CreateFaction("faction_defeated", CreateSecureLeadership("guardian_a"));
        faction["factionLifecycle"] = new JsonObject
        {
            ["state"] = ShiningAbodeState.FactionLifecycleStateBroken,
            ["defeatedAtTurn"] = 77,
            ["defeatedAtUtc"] = "2026-05-20T00:00:00Z",
            ["defeatReason"] = "Разгромлена в Сияющей Обители.",
            ["remnantsSummary"] = "Остались разрозненные осколки."
        };
        faction["projects"] = new JsonArray(CreateProject("project_should_not_stay_supported", isSupported: true));
        faction["tradeInventory"] = new JsonObject
        {
            ["tradeCycleId"] = "shining_return_7",
            ["generatedAtUtc"] = "2026-05-20T00:00:00Z",
            ["generationTradeTier"] = 1,
            ["generationRarityCeiling"] = ShiningAbodeState.RarityUncommon,
            ["serviceMultiplierSnapshot"] = 1.0,
            ["merchantProfile"] = ShiningTradeRequestState.MerchantProfileShiningFaction,
            ["items"] = new JsonArray()
        };
        root["factions"] = new JsonArray(faction);

        var issues = InvokeShiningStateValidation(root);

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_faction_defeated_has_non_vacant_leadership", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_faction_defeated_has_supported_project", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_faction_defeated_has_trade_inventory", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateShiningAbodeStateFile_EmptyGachaCycleWithUsedCharges_RaisesExplicitError()
    {
        var root = CreateMinimalShiningStateForBlessingCardValidation();
        root["gachaSystem"] = new JsonObject
        {
            ["chargesPerReturn"] = 3,
            ["chargesUsedThisReturn"] = 2,
            ["currentReturnCycleId"] = "",
            ["gachaHistory"] = new JsonArray()
        };

        var issues = InvokeShiningStateValidation(root);

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_gacha_used_charges_without_cycle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateShiningAbodeStateFile_CoreReceiptWithoutResolvedMarkers_RaisesExplicitErrors()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": { "experience": 120, "tier": 1 },
          "lightSparks": 40,
          "halls": [],
          "factions": [],
          "shiningPoliticalActors": [],
          "factionFoundingReceipts": [],
          "factionRealignmentReceipts": [],
          "coreActionReceipts": [
            {
              "requestId": "core_stub",
              "actionType": "open_gates",
              "status": "accepted",
              "selectedCardIds": [],
              "newResidentIds": [],
              "seededProjectIds": [],
              "generatedDraftVersion": 0,
              "resolvedAtTurn": 0,
              "resolvedAtUtc": ""
            }
          ],
          "gates": {
            "draftVersion": 0,
            "hasOpenDraft": false,
            "isStale": false,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": [],
            "selectedBlessingCardIds": [],
            "nextCandidateCursor": 0,
            "rerollsRemaining": 0
          },
          "gachaSystem": {
            "chargesPerReturn": 0,
            "chargesUsedThisReturn": 0,
            "currentReturnCycleId": "return_1",
            "gachaHistory": []
          }
        }
        """)!.AsObject();

        using var document = JsonDocument.Parse(root.ToJsonString());
        var validator = new ValidationService(
            new FileSystemManager(Path.GetTempPath(), NullLogger<FileSystemManager>.Instance),
            NullLogger<ValidationService>.Instance);
        var issues = new List<ValidationIssue>();
        var method = typeof(ValidationService).GetMethod(
            "ValidateShiningAbodeStateFile",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        method!.Invoke(validator, new object[] { document.RootElement, ShiningAbodeState.StatePath, issues });

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_core_action_receipt_missing_resolved_at_turn", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_core_action_receipt_missing_resolved_at_utc", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateShiningAbodeStateFile_PreparedPackageOrderMismatch_RaisesExplicitError()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": { "experience": 380, "tier": 3 },
          "lightSparks": 40,
          "halls": [],
          "factions": [],
          "shiningPoliticalActors": [],
          "factionFoundingReceipts": [],
          "factionRealignmentReceipts": [],
          "coreActionReceipts": [],
          "preparedIncarnationPackage": {
            "generatedFromDraftVersion": 4,
            "preparedAtTurn": 155,
            "preparedAtUtc": "2026-04-19T10:00:00Z",
            "selectedCardIds": ["card_b", "card_a"],
            "selectedCards": [
              {
                "cardId": "card_a",
                "dedupeKey": "a",
                "sourceType": "head",
                "sourceFactionId": "faction_a",
                "effectFamily": "social",
                "rarity": "Rare",
                "displayName": "Карта А",
                "displaySummary": "Первая карта.",
                "effectPayload": {}
              },
              {
                "cardId": "card_b",
                "dedupeKey": "b",
                "sourceType": "project",
                "sourceFactionId": "faction_b",
                "effectFamily": "route",
                "rarity": "Epic",
                "displayName": "Карта Б",
                "displaySummary": "Вторая карта.",
                "effectPayload": {}
              }
            ]
          },
          "gates": {
            "draftVersion": 0,
            "hasOpenDraft": false,
            "isStale": false,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": [],
            "selectedBlessingCardIds": [],
            "nextCandidateCursor": 0,
            "rerollsRemaining": 0
          },
          "gachaSystem": {
            "chargesPerReturn": 0,
            "chargesUsedThisReturn": 0,
            "currentReturnCycleId": "return_1",
            "gachaHistory": []
          }
        }
        """)!.AsObject();

        using var document = JsonDocument.Parse(root.ToJsonString());
        var validator = new ValidationService(
            new FileSystemManager(Path.GetTempPath(), NullLogger<FileSystemManager>.Instance),
            NullLogger<ValidationService>.Instance);
        var issues = new List<ValidationIssue>();
        var method = typeof(ValidationService).GetMethod(
            "ValidateShiningAbodeStateFile",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        method!.Invoke(validator, new object[] { document.RootElement, ShiningAbodeState.StatePath, issues });

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_abode_prepare_package_selected_card_sequence_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateShiningAbodeStateFile_EmptyPreparedPackage_RaisesBootstrapError()
    {
        var root = CreateMinimalShiningStateForBlessingCardValidation();
        root["preparedIncarnationPackage"] = new JsonObject
        {
            ["generatedFromDraftVersion"] = 4,
            ["preparedAtTurn"] = 155,
            ["preparedAtUtc"] = "2026-04-19T10:00:00Z",
            ["selectedCardIds"] = new JsonArray(),
            ["selectedCards"] = new JsonArray()
        };

        var issues = InvokeShiningStateValidation(root);

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_abode_prepare_package_bootstrap_invalid", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateShiningAbodeStateFile_DuplicatePreparedPackageCardIds_RaisesBootstrapError()
    {
        var cardA = CreateBlessingCard("card_route");
        var cardB = CreateBlessingCard("card_route");
        var root = CreateMinimalShiningStateForBlessingCardValidation();
        root["preparedIncarnationPackage"] = new JsonObject
        {
            ["generatedFromDraftVersion"] = 4,
            ["preparedAtTurn"] = 155,
            ["preparedAtUtc"] = "2026-04-19T10:00:00Z",
            ["selectedCardIds"] = new JsonArray("card_route", "card_route"),
            ["selectedCards"] = new JsonArray(cardA, cardB)
        };

        var issues = InvokeShiningStateValidation(root);

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_abode_prepare_package_bootstrap_invalid", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateShiningAbodeStateFile_GatesBlessingCardUnsupportedTokens_RaiseExplicitErrors()
    {
        var card = CreateBlessingCard("card_bad_gates");
        card["sourceType"] = "broken_source";
        card["effectFamily"] = "broken_family";
        card["rarity"] = "mythic";
        var root = CreateMinimalShiningStateForBlessingCardValidation();
        root["gates"]!["hasOpenDraft"] = true;
        root["gates"]!["allCandidateBlessingCards"] = new JsonArray(card.DeepClone());
        root["gates"]!["availableBlessingCards"] = new JsonArray(card.DeepClone());
        root["gates"]!["shownBlessingCardIds"] = new JsonArray("card_bad_gates");
        root["gates"]!["selectedBlessingCardIds"] = new JsonArray("card_bad_gates");

        var issues = InvokeShiningStateValidation(root);

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_abode_invalid_blessing_card_source_type", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_abode_invalid_blessing_card_effect_family", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_abode_invalid_blessing_card_rarity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateShiningAbodeStateFile_PreparedPackageBlessingCardUnsupportedTokens_RaiseExplicitErrors()
    {
        var card = CreateBlessingCard("card_bad_package");
        card["sourceType"] = "broken_source";
        card["effectFamily"] = "broken_family";
        card["rarity"] = "mythic";
        var root = CreateMinimalShiningStateForBlessingCardValidation();
        root["preparedIncarnationPackage"] = new JsonObject
        {
            ["generatedFromDraftVersion"] = 4,
            ["preparedAtTurn"] = 155,
            ["preparedAtUtc"] = "2026-04-19T10:00:00Z",
            ["selectedCardIds"] = new JsonArray("card_bad_package"),
            ["selectedCards"] = new JsonArray(card)
        };

        var issues = InvokeShiningStateValidation(root);

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_abode_invalid_blessing_card_source_type", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_abode_invalid_blessing_card_effect_family", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_abode_invalid_blessing_card_rarity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateShiningAbodeStateFile_PreparePackageReceiptWithStaleSelectedCards_RaisesExplicitError()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": { "experience": 380, "tier": 3 },
          "lightSparks": 40,
          "halls": [],
          "factions": [],
          "shiningPoliticalActors": [],
          "factionFoundingReceipts": [],
          "factionRealignmentReceipts": [],
          "coreActionReceipts": [
            {
              "requestId": "package_receipt_1",
              "actionType": "prepare_incarnation_package",
              "status": "accepted",
              "selectedCardIds": ["card_b", "card_a"],
              "selectedCards": [
                {
                  "cardId": "card_a",
                  "dedupeKey": "a",
                  "sourceType": "head",
                  "sourceFactionId": "faction_a",
                  "effectFamily": "social",
                  "rarity": "Rare",
                  "displayName": "Карта А",
                  "displaySummary": "Первая карта.",
                  "effectPayload": {}
                },
                {
                  "cardId": "card_b",
                  "dedupeKey": "b",
                  "sourceType": "project",
                  "sourceFactionId": "faction_b",
                  "effectFamily": "route",
                  "rarity": "Epic",
                  "displayName": "Карта Б",
                  "displaySummary": "Вторая карта.",
                  "effectPayload": {}
                }
              ],
              "newResidentIds": [],
              "seededProjectIds": [],
              "generatedDraftVersion": 4,
              "resolvedAtTurn": 155,
              "resolvedAtUtc": "2026-04-19T10:00:00Z"
            }
          ],
          "gates": {
            "draftVersion": 0,
            "hasOpenDraft": false,
            "isStale": false,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": [],
            "selectedBlessingCardIds": [],
            "nextCandidateCursor": 0,
            "rerollsRemaining": 0
          },
          "gachaSystem": {
            "chargesPerReturn": 0,
            "chargesUsedThisReturn": 0,
            "currentReturnCycleId": "return_1",
            "gachaHistory": []
          }
        }
        """)!.AsObject();

        using var document = JsonDocument.Parse(root.ToJsonString());
        var validator = new ValidationService(
            new FileSystemManager(Path.GetTempPath(), NullLogger<FileSystemManager>.Instance),
            NullLogger<ValidationService>.Instance);
        var issues = new List<ValidationIssue>();
        var method = typeof(ValidationService).GetMethod(
            "ValidateShiningAbodeStateFile",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        method!.Invoke(validator, new object[] { document.RootElement, ShiningAbodeState.StatePath, issues });

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_prepare_package_receipt_selected_cards_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateShiningAbodeStateFile_CoreReceiptWithoutCostAudit_AllowsHistoricalReceipt()
    {
        var root = CreateMinimalShiningStateForBlessingCardValidation();
        root["coreActionReceipts"]!.AsArray().Add(new JsonObject
        {
            ["requestId"] = "core_without_costs",
            ["actionType"] = ShiningCoreActionRequestState.ActionTypeOpenGates,
            ["status"] = ShiningCoreActionRequestState.RequestStatusAccepted,
            ["selectedCardIds"] = new JsonArray(),
            ["newResidentIds"] = new JsonArray(),
            ["seededProjectIds"] = new JsonArray(),
            ["generatedDraftVersion"] = 1,
            ["resolvedAtTurn"] = 12,
            ["resolvedAtUtc"] = "2026-04-19T10:00:00Z"
        });

        var issues = InvokeShiningStateValidation(root);

        Assert.DoesNotContain(issues, issue => issue.FilePath.Contains("quotedCostFeathers", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => issue.FilePath.Contains("quotedCostLightSparks", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateShiningAbodeStateFile_CoreReceiptWithMalformedCostAudit_RaisesIntegerErrors()
    {
        var root = CreateMinimalShiningStateForBlessingCardValidation();
        root["coreActionReceipts"]!.AsArray().Add(new JsonObject
        {
            ["requestId"] = "core_malformed_costs",
            ["actionType"] = ShiningCoreActionRequestState.ActionTypeOpenGates,
            ["status"] = ShiningCoreActionRequestState.RequestStatusAccepted,
            ["quotedCostFeathers"] = "0",
            ["quotedCostLightSparks"] = "0",
            ["selectedCardIds"] = new JsonArray(),
            ["newResidentIds"] = new JsonArray(),
            ["seededProjectIds"] = new JsonArray(),
            ["generatedDraftVersion"] = 1,
            ["resolvedAtTurn"] = 12,
            ["resolvedAtUtc"] = "2026-04-19T10:00:00Z"
        });

        var issues = InvokeShiningStateValidation(root);

        Assert.Contains(issues, issue => issue.FilePath.EndsWith(".coreActionReceipts[0].quotedCostFeathers", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => issue.FilePath.EndsWith(".coreActionReceipts[0].quotedCostLightSparks", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateShiningAbodeStateFile_PreparePackageAcceptedReceiptWithoutSelectedCards_RaisesExplicitError()
    {
        var root = CreateMinimalShiningStateForBlessingCardValidation();
        root["coreActionReceipts"]!.AsArray().Add(new JsonObject
        {
            ["requestId"] = "package_without_cards",
            ["actionType"] = ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage,
            ["status"] = ShiningCoreActionRequestState.RequestStatusAccepted,
            ["quotedCostFeathers"] = 0,
            ["quotedCostLightSparks"] = 0,
            ["selectedCardIds"] = new JsonArray("card_route"),
            ["newResidentIds"] = new JsonArray(),
            ["seededProjectIds"] = new JsonArray(),
            ["generatedDraftVersion"] = 4,
            ["resolvedAtTurn"] = 155,
            ["resolvedAtUtc"] = "2026-04-19T10:00:00Z"
        });

        var issues = InvokeShiningStateValidation(root);

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_prepare_package_receipt_missing_selected_cards", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateShiningAbodeStateFile_ResolvedRealignmentWithoutResidentHistoryEntry_RaisesExplicitError()
    {
        var root = CreateMinimalShiningStateForBlessingCardValidation();
        root["factionRealignmentReceipts"]!.AsArray().Add(new JsonObject
        {
            ["requestId"] = "realign_without_history",
            ["residentId"] = "resident_liora",
            ["sourceFactionId"] = "faction_old",
            ["targetFactionId"] = "faction_new",
            ["status"] = ShiningFactionRequestState.RequestStatusDepartedToNeutral,
            ["realignmentMode"] = ShiningFactionRequestState.RealignmentModeDepartureToNeutral,
            ["resolvedAtTurn"] = 120,
            ["resolvedAtUtc"] = "2026-04-19T10:00:00Z"
        });

        var issues = InvokeShiningStateValidation(root);

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_realignment_receipt_missing_resident_history_entry_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateShiningLeadershipHeadReferencesAsync_MissingGuardianBinding_RaisesExplicitError()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, """
            {
              "availability": "active",
              "radiance": { "experience": 150, "tier": 1 },
              "lightSparks": 55,
              "halls": [],
              "factions": [
                {
                  "factionId": "faction_orphan",
                  "originType": "ascended_guardian",
                  "hallId": "hall_void",
                  "charter": {
                    "factionName": "Осиротевший хор",
                    "favoredArchetype": "accord",
                    "patronEffectFamily": "social",
                    "summary": "Тест"
                  },
                  "leadership": {
                    "headActorType": "guardian",
                    "headActorId": "guardian_missing",
                    "leadershipState": "secure"
                  },
                  "baseStrength": 30,
                  "factionStrength": 30,
                  "investCountThisAscension": 0,
                  "projects": [],
                  "tradeInventoryReceipts": [],
                  "leadershipReceipts": [],
                  "leadershipHistory": []
                }
              ],
              "shiningPoliticalActors": [],
              "factionFoundingReceipts": [],
              "factionRealignmentReceipts": [],
              "coreActionReceipts": [],
              "gates": {
                "draftVersion": 0,
                "hasOpenDraft": false,
                "isStale": false,
                "allCandidateBlessingCards": [],
                "availableBlessingCards": [],
                "shownBlessingCardIds": [],
                "selectedBlessingCardIds": [],
                "nextCandidateCursor": 0,
                "rerollsRemaining": 0
              },
              "gachaSystem": {
                "chargesPerReturn": 0,
                "chargesUsedThisReturn": 0,
                "currentReturnCycleId": "return_1",
                "gachaHistory": []
              }
            }
            """);
            await fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
            {
              "guardians": [],
              "activeGuardian": {
                "guardianId": "guardian_live",
                "canonicalName": "Азалия"
              },
              "chaosSeaNavigation": {
                "currentAbodeId": "abode_live"
              }
            }
            """);
            await fs.WriteFileAtomicAsync(GuardianAbodeResidentState.StatePath, """
            {
              "entries": [],
              "thoughtJournal": [],
              "interactionLog": [],
              "historyLog": [],
              "transferReceipts": [],
              "interactionReceipts": [],
              "rosterReceipts": []
            }
            """);

            var validator = new ValidationService(fs, NullLogger<ValidationService>.Instance);
            var issues = new List<ValidationIssue>();
            var method = typeof(ValidationService).GetMethod(
                "ValidateShiningLeadershipHeadReferencesAsync",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(method);

            var task = method!.Invoke(validator, new object[] { issues }) as Task;
            Assert.NotNull(task);
            await task!;

            Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_leadership_missing_head_actor_reference", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ValidateShiningLeadershipHeadReferencesAsync_ResidentHeadMustBeAscendedAndAligned()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteNodeAsync(fs, ShiningAbodeState.StatePath, CreateLeadershipStateRoot(new JsonObject
            {
                ["headActorType"] = ShiningAbodeState.HeadActorTypeResident,
                ["headActorId"] = "resident_liora",
                ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure
            }));
            await WriteNodeAsync(fs, "game_state/meta/guardians.json", CreateMinimalGuardiansRoot());
            await WriteNodeAsync(fs, GuardianAbodeResidentState.StatePath, new JsonObject
            {
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["residentId"] = "resident_liora",
                    ["displayName"] = "Лиора",
                    ["ascensionState"] = ShiningAbodeState.AscensionStateRemainedInChaosSea,
                    ["shiningFactionId"] = "faction_other"
                }),
                ["thoughtJournal"] = new JsonArray(),
                ["interactionLog"] = new JsonArray(),
                ["historyLog"] = new JsonArray(),
                ["transferReceipts"] = new JsonArray(),
                ["interactionReceipts"] = new JsonArray(),
                ["rosterReceipts"] = new JsonArray()
            });

            var issues = await InvokeLeadershipValidationAsync(fs);

            Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_leadership_resident_head_not_ascended", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_leadership_resident_head_faction_mismatch", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ValidateShiningLeadershipHeadReferencesAsync_RadiantHeadMustBeAlignedAndExclusive()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            var shiningRoot = CreateLeadershipStateRoot(new JsonObject
            {
                ["headActorType"] = ShiningAbodeState.HeadActorTypeRadiantActor,
                ["headActorId"] = "actor_shared",
                ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure
            });
            var factions = shiningRoot["factions"]!.AsArray();
            factions.Add(CreateFaction("faction_other", new JsonObject
            {
                ["headActorType"] = ShiningAbodeState.HeadActorTypeRadiantActor,
                ["headActorId"] = "actor_shared",
                ["leadershipState"] = ShiningAbodeState.LeadershipStateContested
            }));
            shiningRoot["shiningPoliticalActors"] = new JsonArray(new JsonObject
            {
                ["actorId"] = "actor_shared",
                ["actorType"] = ShiningAbodeState.HeadActorTypeRadiantActor,
                ["displayName"] = "Архон",
                ["summary"] = "Shared actor.",
                ["originFactionId"] = "faction_main",
                ["currentFactionId"] = "faction_main",
                ["politicalStatus"] = ShiningAbodeState.PoliticalStatusHead
            });

            await WriteNodeAsync(fs, ShiningAbodeState.StatePath, shiningRoot);
            await WriteNodeAsync(fs, "game_state/meta/guardians.json", CreateMinimalGuardiansRoot());
            await WriteNodeAsync(fs, GuardianAbodeResidentState.StatePath, CreateEmptyResidentRoot());

            var issues = await InvokeLeadershipValidationAsync(fs);

            Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_leadership_radiant_head_faction_mismatch", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_leadership_duplicate_head_actor", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ValidateShiningLeadershipHeadReferencesAsync_RadiantHeadMustHaveHeadPoliticalStatus()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            var shiningRoot = CreateLeadershipStateRoot(new JsonObject
            {
                ["headActorType"] = ShiningAbodeState.HeadActorTypeRadiantActor,
                ["headActorId"] = "actor_current_head",
                ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure
            });
            shiningRoot["shiningPoliticalActors"] = new JsonArray(new JsonObject
            {
                ["actorId"] = "actor_current_head",
                ["actorType"] = ShiningAbodeState.HeadActorTypeRadiantActor,
                ["displayName"] = "Архон",
                ["summary"] = "Actor is bound to the faction but has stale status.",
                ["originFactionId"] = "faction_main",
                ["currentFactionId"] = "faction_main",
                ["politicalStatus"] = ShiningAbodeState.PoliticalStatusFormerHead
            });

            await WriteNodeAsync(fs, ShiningAbodeState.StatePath, shiningRoot);
            await WriteNodeAsync(fs, "game_state/meta/guardians.json", CreateMinimalGuardiansRoot());
            await WriteNodeAsync(fs, GuardianAbodeResidentState.StatePath, CreateEmptyResidentRoot());

            var issues = await InvokeLeadershipValidationAsync(fs);

            Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_leadership_radiant_head_status_mismatch", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-shining-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static async Task<List<ValidationIssue>> InvokeLeadershipValidationAsync(FileSystemManager fs)
    {
        var validator = new ValidationService(fs, NullLogger<ValidationService>.Instance);
        var issues = new List<ValidationIssue>();
        var method = typeof(ValidationService).GetMethod(
            "ValidateShiningLeadershipHeadReferencesAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = method!.Invoke(validator, new object[] { issues }) as Task;
        Assert.NotNull(task);
        await task!;
        return issues;
    }

    private static JsonObject CreateLeadershipStateRoot(JsonObject leadership) => new()
    {
        ["availability"] = ShiningAbodeState.AvailabilityActive,
        ["radiance"] = new JsonObject
        {
            ["experience"] = 150,
            ["tier"] = 1
        },
        ["lightSparks"] = 55,
        ["halls"] = new JsonArray(),
        ["factions"] = new JsonArray(CreateFaction("faction_main", leadership)),
        ["shiningPoliticalActors"] = new JsonArray(),
        ["factionFoundingReceipts"] = new JsonArray(),
        ["factionRealignmentReceipts"] = new JsonArray(),
        ["coreActionReceipts"] = new JsonArray(),
        ["pendingNativeFactionDiscovery"] = null,
        ["gates"] = new JsonObject
        {
            ["draftVersion"] = 0,
            ["hasOpenDraft"] = false,
            ["isStale"] = false,
            ["allCandidateBlessingCards"] = new JsonArray(),
            ["availableBlessingCards"] = new JsonArray(),
            ["shownBlessingCardIds"] = new JsonArray(),
            ["selectedBlessingCardIds"] = new JsonArray(),
            ["nextCandidateCursor"] = 0,
            ["rerollsRemaining"] = 0
        },
        ["gachaSystem"] = new JsonObject
        {
            ["chargesPerReturn"] = 0,
            ["chargesUsedThisReturn"] = 0,
            ["currentReturnCycleId"] = "return_1",
            ["gachaHistory"] = new JsonArray()
        }
    };

    private static JsonObject CreateFaction(string factionId, JsonObject leadership) => new()
    {
        ["factionId"] = factionId,
        ["originType"] = ShiningAbodeState.OriginTypeNativeRadiant,
        ["hallId"] = $"hall_{factionId}",
        ["charter"] = new JsonObject
        {
            ["factionName"] = factionId,
            ["favoredArchetype"] = ShiningAbodeState.ProjectArchetypeAccord,
            ["patronEffectFamily"] = ShiningAbodeState.EffectFamilySocial,
            ["summary"] = factionId
        },
        ["leadership"] = leadership,
        ["baseStrength"] = 30,
        ["factionStrength"] = 30,
        ["investCountThisAscension"] = 0,
        ["projects"] = new JsonArray(),
        ["tradeInventoryReceipts"] = new JsonArray(),
        ["leadershipReceipts"] = new JsonArray(),
        ["leadershipHistory"] = new JsonArray()
    };

    private static JsonObject CreateSecureLeadership(string guardianId) => new()
    {
        ["headActorType"] = ShiningAbodeState.HeadActorTypeGuardian,
        ["headActorId"] = guardianId,
        ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure
    };

    private static JsonObject CreateProject(string projectId, bool isSupported) => new()
    {
        ["projectId"] = projectId,
        ["displayName"] = projectId,
        ["summary"] = projectId,
        ["toneTags"] = new JsonArray("bright"),
        ["targetFactionIds"] = new JsonArray(),
        ["projectArchetype"] = ShiningAbodeState.ProjectArchetypeAccord,
        ["outputEffectFamily"] = ShiningAbodeState.EffectFamilySocial,
        ["tier"] = 1,
        ["status"] = ShiningAbodeState.ProjectStatusCompleted,
        ["isSupported"] = isSupported,
        ["strengthReward"] = 8
    };

    private static JsonObject CreateMinimalGuardiansRoot() => new()
    {
        ["guardians"] = new JsonArray(),
        ["activeGuardian"] = null
    };

    private static JsonObject CreateMinimalShiningStateForBlessingCardValidation() => new()
    {
        ["availability"] = ShiningAbodeState.AvailabilityActive,
        ["radiance"] = new JsonObject
        {
            ["experience"] = 380,
            ["tier"] = 3
        },
        ["lightSparks"] = 40,
        ["halls"] = new JsonArray(),
        ["factions"] = new JsonArray(),
        ["shiningPoliticalActors"] = new JsonArray(),
        ["factionFoundingReceipts"] = new JsonArray(),
        ["factionRealignmentReceipts"] = new JsonArray(),
        ["coreActionReceipts"] = new JsonArray(),
        ["gates"] = new JsonObject
        {
            ["draftVersion"] = 0,
            ["hasOpenDraft"] = false,
            ["isStale"] = false,
            ["allCandidateBlessingCards"] = new JsonArray(),
            ["availableBlessingCards"] = new JsonArray(),
            ["shownBlessingCardIds"] = new JsonArray(),
            ["selectedBlessingCardIds"] = new JsonArray(),
            ["nextCandidateCursor"] = 0,
            ["rerollsRemaining"] = 0
        },
        ["gachaSystem"] = new JsonObject
        {
            ["chargesPerReturn"] = 0,
            ["chargesUsedThisReturn"] = 0,
            ["currentReturnCycleId"] = "return_1",
            ["gachaHistory"] = new JsonArray()
        }
    };

    private static JsonObject CreateBlessingCard(string cardId) => new()
    {
        ["cardId"] = cardId,
        ["dedupeKey"] = cardId,
        ["sourceType"] = ShiningAbodeState.CardSourceTypeProject,
        ["sourceFactionId"] = "faction_dawn",
        ["sourceActorId"] = "project_dawn",
        ["effectFamily"] = ShiningAbodeState.EffectFamilyRoute,
        ["rarity"] = ShiningAbodeState.RarityRare,
        ["displayName"] = "Тропа",
        ["displaySummary"] = "Открывает путь.",
        ["effectPayload"] = new JsonObject()
    };

    private static List<ValidationIssue> InvokeShiningStateValidation(JsonObject root)
    {
        using var document = JsonDocument.Parse(root.ToJsonString());
        var validator = new ValidationService(
            new FileSystemManager(Path.GetTempPath(), NullLogger<FileSystemManager>.Instance),
            NullLogger<ValidationService>.Instance);
        var issues = new List<ValidationIssue>();
        var method = typeof(ValidationService).GetMethod(
            "ValidateShiningAbodeStateFile",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        method!.Invoke(validator, new object[] { document.RootElement, ShiningAbodeState.StatePath, issues });
        return issues;
    }

    private static JsonObject CreateEmptyResidentRoot() => new()
    {
        ["entries"] = new JsonArray(),
        ["thoughtJournal"] = new JsonArray(),
        ["interactionLog"] = new JsonArray(),
        ["historyLog"] = new JsonArray(),
        ["transferReceipts"] = new JsonArray(),
        ["interactionReceipts"] = new JsonArray(),
        ["rosterReceipts"] = new JsonArray()
    };

    private static async Task WriteNodeAsync(FileSystemManager fs, string relativePath, JsonNode node)
    {
        await fs.WriteFileAtomicAsync(relativePath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void CleanupTempRoot(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch
        {
            // ignored
        }
    }
}
