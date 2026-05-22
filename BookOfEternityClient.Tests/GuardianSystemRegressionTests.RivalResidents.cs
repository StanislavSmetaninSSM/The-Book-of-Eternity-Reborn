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
{    [Fact]
    public async Task GuardianTradeValidation_OrphanConventionalSnapshotCopyDoesNotCreateStrictEvidence()
    {
        await WriteRawAsync(
            $"game_state/control/pending_turn_snapshot/{GuardianTradeRequestState.PendingRequestPath}",
            """
            {
              "guardianId": "guardian_alpha",
              "requestedBySoulAtUtc": "2026-04-14T00:00:00Z",
              "requestedTradeKind": "gift",
              "requestedTradePayload": {
                "itemId": "orphan_snapshot_only"
              }
            }
            """,
            syncPendingSnapshotAuthority: false);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_request_missing_validated_snapshot_request", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MemoryGatesValidation_OrphanRawSnapshotDoesNotTriggerLegacyNotReplaced()
    {
        await WriteRawAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: MEMORY_GATES] 10 Чернильных Перьев"
        }
        """);

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12,
          "actionTag": "MEMORY_GATES",
          "resolutionType": "memoryLegacy",
          "summary": "Новое наследие выбрано.",
          "resolved": true,
          "stateEvidence": {
            "legacyId": "legacy_current",
            "legacyType": "startingCharacteristicBonus",
            "affectedFiles": [
              "game_state/meta/soul_state.json"
            ]
          }
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "pendingMemoryLegacy": {
            "legacyId": "legacy_current",
            "legacyType": "startingCharacteristicBonus",
            "characteristic": "Mind",
            "bonus": 2,
            "grantSource": "memoryLegacyGrant",
            "grantSnapshot": {
              "legacyId": "legacy_current",
              "legacyType": "startingCharacteristicBonus",
              "characteristic": "Mind",
              "bonus": 2
            }
          }
        }
        """);

        await WriteRawAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", """
        {
          "pendingMemoryLegacy": {
            "legacyId": "legacy_current",
            "legacyType": "startingCharacteristicBonus",
            "characteristic": "Mind",
            "bonus": 2,
            "grantSource": "memoryLegacyGrant",
            "grantSnapshot": {
              "legacyId": "legacy_current",
              "legacyType": "startingCharacteristicBonus",
              "characteristic": "Mind",
              "bonus": 2
            }
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "memory_gates_legacy_not_replaced", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FriendlyGrowthLoop_QuestCompletionUpdatesPowerAndTradeFromSharedRules()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """{ "turnNumber": 17 }""");
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "domain": "Knowledge",
              "abode": { "abodeId": "abode_alpha", "title": "Обитель Азалии" },
              "relationshipData": { "currentReputation": 80, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 35, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "questManagement": {
                "availableQuests": [],
                "activeQuests": [
                  {
                    "questId": "quest_alpha",
                    "questName": "Укрепить контур Обители",
                    "difficulty": "hard"
                  }
                ],
                "completedQuests": []
              },
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
              "appearanceDescription": "Тестовая форма."
            },
            "manifestationHistory": [],
            "domain": "Knowledge",
            "abode": { "abodeId": "abode_alpha", "title": "Обитель Азалии" },
            "relationshipData": { "currentReputation": 80, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 35, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
            "questManagement": {
              "availableQuests": [],
              "activeQuests": [
                {
                  "questId": "quest_alpha",
                  "questName": "Укрепить контур Обители",
                  "difficulty": "hard"
                }
              ],
              "completedQuests": []
            },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          },
          "UpdateGuardians": [
            {
              "command": "completeQuest",
              "guardianId": "guardian_alpha",
              "questId": "quest_alpha",
              "outcome": "success",
              "questPowerAudit": {
                "questDifficultyTier": "hard",
                "outcome": "success",
                "supportsCurrentProject": true,
                "defendsAgainstRivalPressure": false,
                "baseDelta": 5,
                "bonusDelta": 2,
                "finalDelta": 7
              }
            }
          ],
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_alpha"
          }
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await WriteCurrentGuardiansNormalizerBackupAsync("test_backups/preturn_guardians_lore_research_bonus_loop_normalizer.json");
        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>
        {
            [GuardianProjectState.TrackerPath] = "test_backups/preturn_tracker_lore_research_bonus_loop.json",
            ["game_state/meta/guardians.json"] = "test_backups/preturn_guardians_lore_research_bonus_loop_normalizer.json"
        });

        var guardiansRoot = await ReadObjectAsync("game_state/meta/guardians.json");
        var activeGuardian = guardiansRoot["activeGuardian"]!.AsObject();
        var derivedState = GuardianProjectState.ResolveGuardianDerivedState(activeGuardian, trackerRoot: null);
        var tradeService = new GuardianTradeService(_fs, NullLogger<GuardianTradeService>.Instance);
        var tradeView = await tradeService.EnsureTradeInventoryAsync("guardian_alpha", 1, currentTurn: 17);
        var powerJournal = await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath);

        Assert.Equal(42, AbodePowerRules.GetCurrentPower(activeGuardian));
        Assert.Equal(6, derivedState.TradeSlotCount);
        Assert.Equal(3, derivedState.GuardianQuestCap);
        Assert.NotNull(tradeView);
        Assert.False(tradeView!.TradeBlocked);
        Assert.False(tradeView.InventoryReady);
        Assert.True(tradeView.InventoryRequestPending);
        Assert.Empty(tradeView.Offers);
        Assert.NotNull(powerJournal);
        Assert.Contains("guardian_quest", powerJournal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HostileCorrectionLoop_UsesCentralizedSeverityCostsAndPowerSpend()
    {
        await WriteRawAsync(ScenarioCoreService.ManifestPath, """
        {
          "scenarioCoreAssertions": [
            { "assertionId": "core_role", "category": "role_status", "value": "Игрок начинает королём", "explicit": true, "source": "structured_field" }
          ],
          "candidateAssertions": [],
          "openCorrectionSlots": [
            { "slotId": "slot_rival", "slotType": "rival_thread", "maxSeverity": "strong", "allowsFriendly": true, "allowsHostile": true, "sourceAssertionId": "core_role" },
            { "slotId": "slot_debt", "slotType": "debt_or_oath", "maxSeverity": "medium", "allowsFriendly": false, "allowsHostile": true, "sourceAssertionId": "core_role" }
          ]
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guard_test_varak",
              "canonicalName": "Варак",
              "nameVariants": { "default": "Варак", "feminine": null, "masculine": "Варак", "neutral": null },
              "manifestation": {
                "currentDisplayName": "Варак",
                "formFlexibility": "fixed",
                "currentPresentationStyle": "masculine",
                "currentPronouns": "он/его",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": -80, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 82, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guard_test_varak",
            "canonicalName": "Варак",
            "nameVariants": { "default": "Варак", "feminine": null, "masculine": "Варак", "neutral": null },
            "manifestation": {
              "currentDisplayName": "Варак",
              "formFlexibility": "fixed",
              "currentPresentationStyle": "masculine",
              "currentPronouns": "он/его",
              "appearanceDescription": "Тестовая форма."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": -80, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 82, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        var scenarioCore = new ScenarioCoreService(_fs, NullLogger<ScenarioCoreService>.Instance);
        var correctionService = new GuardianCorrectionService(_fs, scenarioCore, NullLogger<GuardianCorrectionService>.Instance);

        await correctionService.ApplyForNewLifeAsync(3);
        var state = await correctionService.ReadAsync();

        Assert.NotNull(state);
        Assert.Equal("hostile", state!.Intent);
        Assert.NotEmpty(state.Corrections);
        Assert.Equal(
            state.Corrections.Sum(item => AbodePowerRules.GetCorrectionSeverityAbodePowerCost(item.Severity)),
            state.TotalAbodePowerSpent);
        Assert.Equal(
            state.Corrections.Sum(item => AbodePowerRules.GetCorrectionSeverityBudgetCost(item.Severity)),
            state.Corrections.Sum(item => item.BudgetCostPoints));
        Assert.True(state.Corrections.Sum(item => item.BudgetCostPoints) <= state.BaseBudgetPoints);
        Assert.Equal(state.PowerBefore - state.TotalAbodePowerSpent, state.PowerAfter);
    }

    [Fact]
    public async Task PoliticsLoop_OffensiveCompletionMatchesSharedImpactResolver()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """{ "turnNumber": 52 }""");
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "abodePower": { "currentPower": 50, "tier": "Стабильная", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            },
            {
              "guardianId": "guardian_beta",
              "canonicalName": "Варак",
              "nameVariants": { "default": "Варак", "feminine": null, "masculine": "Варак", "neutral": null },
              "manifestation": {
                "currentDisplayName": "Варак",
                "formFlexibility": "fixed",
                "currentPresentationStyle": "masculine",
                "currentPronouns": "он/его",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "abodePower": { "currentPower": 60, "tier": "Могущественная", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
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
              "appearanceDescription": "Тестовая форма."
            },
            "manifestationHistory": [],
            "abodePower": { "currentPower": 50, "tier": "Стабильная", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        var trackerRoot = JsonNode.Parse("""
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_offense",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Теневая интрига",
                "activeState": "Triggering the decisive breach",
                "totalWork": 18,
                "workDone": 18,
                "totalStages": 3,
                "currentStage": 3,
                "pressure": 12,
                "stability": 70
              }
            },
            {
              "guardianId": "guardian_beta",
              "project": {
                "projectId": "proj_beta_active",
                "projectType": "abode_expansion",
                "projectTier": "major",
                "projectMode": "internal",
                "projectName": "Контур Варака",
                "activeState": "Binding",
                "totalWork": 18,
                "workDone": 9,
                "totalStages": 3,
                "currentStage": 1,
                "pressure": 5,
                "stability": 80
              }
            }
          ],
          "completedProjects": [
            {
              "guardianId": "guardian_beta",
              "project": {
                "projectId": "fort_major",
                "projectType": "abode_fortification",
                "projectTier": "major",
                "projectMode": "internal",
                "projectName": "Щит Обители",
                "finalState": "Completed",
                "completionTurn": 10,
                "projectOutcomeAudit": {
                  "safePressureBonus": 10,
                  "defenseRatingBonus": 2
                },
                "effectState": {
                  "safePressureBonusGranted": 10,
                  "defenseRatingBonusGranted": 2
                }
              }
            },
            {
              "guardianId": "guardian_beta",
              "project": {
                "projectId": "counter_minor",
                "projectType": "counter_rival_operation",
                "projectTier": "minor",
                "projectMode": "supportive",
                "targetGuardianId": "guardian_alpha",
                "projectName": "Ответная сеть",
                "finalState": "Completed",
                "completionTurn": 12,
                "projectOutcomeAudit": {
                  "pressureRelief": 15,
                  "stabilityRelief": 5,
                  "abodePowerGain": 1
                }
              }
            }
          ],
          "completeGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_offense",
              "finalState": "Completed",
              "outcome": "Интрига сломала защиту rival-Обители.",
              "targetGuardianId": "guardian_beta"
            }
          ]
        }
        """)!.AsObject();

        var expectedImpact = GuardianProjectState.ResolveOffensiveImpact(
            trackerRoot,
            "guardian_alpha",
            "guardian_beta",
            "major",
            attackerCurrentPower: 50,
            targetCurrentPower: 60);
        var expectedAttackerPower = 50 + GuardianProjectState.GetDefaultTerminalAbodePowerDelta("offensive_intrigue", "Completed", "major");

        var trackerBaselineRoot = trackerRoot.DeepClone().AsObject();
        trackerBaselineRoot.Remove("completeGuardianProjects");
        await _fs.WriteFileAtomicAsync(
            "test_backups/preturn_tracker_politics_loop_completion.json",
            trackerBaselineRoot.ToJsonString());

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """
        {
          "completeGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_offense",
              "finalState": "Completed",
              "outcome": "Интрига сломала защиту rival-Обители.",
              "targetGuardianId": "guardian_beta"
            }
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await WriteCurrentGuardiansNormalizerBackupAsync("test_backups/preturn_guardians_politics_loop_completion_normalizer.json");
        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>
        {
            [GuardianProjectState.TrackerPath] = "test_backups/preturn_tracker_politics_loop_completion.json",
            ["game_state/meta/guardians.json"] = "test_backups/preturn_guardians_politics_loop_completion_normalizer.json"
        });

        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);

        Assert.NotNull(guardiansJson);
        Assert.Contains($"\"currentPower\": {expectedAttackerPower}", guardiansJson, StringComparison.Ordinal);
        Assert.Contains($"\"currentPower\": {60 - expectedImpact.TargetLoss}", guardiansJson, StringComparison.Ordinal);
        Assert.NotNull(trackerJson);
        Assert.Contains($"\"pressure\": {5 + expectedImpact.PressureDelta}", trackerJson, StringComparison.Ordinal);
        Assert.Contains($"\"stability\": {80 - expectedImpact.StabilityDamage}", trackerJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ArchiveCandidateLoop_CodexToArchiveToOfferingRequest_StaysInsideAfterlifeState()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тест",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 2,
          "livesHistory": [
            { "incarnation": 1 },
            { "incarnation": 2 }
          ],
          "afterlifeArchive": { "stored": [] }
        }
        """);
        await WriteRawAsync("lore/codex_entries.json", """
        {
          "entries": [
            {
              "entryId": "codex_life_002",
              "title": "Свод рун",
              "category": "magic",
              "content": "Тайные руны, пережившие смерть героя.",
              "discoveredAt": "2026-03-24T00:00:00Z",
              "discoveryContext": "life",
              "incarnation": 2,
              "tags": ["hidden"]
            }
          ],
          "totalEntries": 1,
          "categories": {
            "cosmology": 0,
            "geography": 0,
            "history": 0,
            "cultures": 0,
            "creatures": 0,
            "characters": 0,
            "artifacts": 0,
            "factions": 0,
            "magic": 1,
            "other": 0
          }
        }
        """);

        var candidateService = new AfterlifeArchiveCandidateService(_fs, NullLogger<AfterlifeArchiveCandidateService>.Instance);
        await candidateService.RefreshFromCurrentStateAsync();
        var manifest = await candidateService.ReadAsync();
        Assert.NotNull(manifest);
        var candidate = Assert.Single(manifest!.Candidates);

        Assert.True(await candidateService.ArchiveCandidateAsync(candidate.CandidateId));

        var soulRoot = await ReadObjectAsync("game_state/meta/soul_state.json");
        var afterlifeArchive = Assert.IsType<System.Text.Json.Nodes.JsonObject>(soulRoot["afterlifeArchive"]);
        var storedEntries = Assert.IsType<System.Text.Json.Nodes.JsonArray>(afterlifeArchive["stored"]);
        var storedEntry = Assert.IsType<System.Text.Json.Nodes.JsonObject>(storedEntries.Single());
        Assert.Equal("codex_life_002", storedEntry["sourceEntryId"]?.GetValue<string>());

        Assert.True(AfterlifeArchiveState.TryGetOfferingTypeForEntryType(
            storedEntry["entryType"]?.GetValue<string>(),
            out var offeringType));

        var request = new GuardianAbodeOfferingState.PendingAbodeOfferingRequest
        {
            GuardianId = "guardian_alpha",
            GuardianName = "Азалия",
            OfferingType = offeringType,
            ArchiveId = storedEntry["archiveId"]?.GetValue<string>(),
            ArchiveTitle = storedEntry["title"]?.GetValue<string>(),
            ArchiveEntryType = storedEntry["entryType"]?.GetValue<string>(),
            ArchiveRarity = storedEntry["rarity"]?.GetValue<string>(),
            ReturnCycleId = GuardianAbodeOfferingState.BuildReturnCycleId(2)
        };
        await GuardianAbodeOfferingState.WriteAsync(_fs, request);
        var roundTrip = await GuardianAbodeOfferingState.ReadAsync(_fs);

        Assert.NotNull(roundTrip);
        Assert.Equal(3, GuardianAbodeOfferingState.ResolvePowerGainForPendingRequest(roundTrip!));
        Assert.False(_fs.FileExists("game_state/inventory/items.json"));
    }

    [Fact]
    public async Task LoreResearchBonusClueLoop_ConsumesOnlyOnceAndDiagnosticSnapshotTracksRemainingBudget()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """{ "turnNumber": 21 }""");
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "abodePower": { "currentPower": 35, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
              "appearanceDescription": "Тестовая форма."
            },
            "manifestationHistory": [],
            "abodePower": { "currentPower": 35, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World"
        }
        """);
        const string preTurnLoreResearchTrackerJson = """
        {
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "research_major",
                "projectType": "lore_research",
                "projectTier": "major",
                "projectMode": "supportive",
                "projectName": "Раскрытие архива",
                "finalState": "Completed",
                "projectOutcomeAudit": {
                  "bonusLoreUnlocks": 1,
                  "questHookCount": 1,
                  "specialQuestLineUnlocks": 0,
                  "visibleRivalClueBonus": 2,
                  "unlockedLoreFragments": []
                },
                "effectState": {
                  "targetIncarnation": 3,
                  "bonusLoreUnlocksApplied": 1,
                  "questHookTokensGranted": 1,
                  "questHookTokensSpent": 0,
                  "specialQuestLineTokensGranted": 0,
                  "specialQuestLineTokensSpent": 0,
                  "visibleRivalClueBudgetGranted": 2,
                  "visibleRivalClueBudgetSpent": 0
                }
              }
            }
          ],
          "temporaryProjectModifiers": [
            {
              "guardianId": "guardian_alpha",
              "modifierId": "temp_minor_pressure",
              "modifierType": "next_internal_project_starting_pressure",
              "value": 10,
              "remainingApplications": 1
            }
          ]
        }
        """;
        await _fs.WriteFileAtomicAsync(
            "test_backups/preturn_tracker_lore_research_bonus_loop.json",
            preTurnLoreResearchTrackerJson);
        await WriteRawAsync(RivalSoulArcService.StatePath, """
        {
          "arcs": [
            {
              "arcId": "arc_hunter",
              "scope": "major",
              "arcType": "hostile_hunt",
              "status": "rising",
              "objective": "Find the player",
              "sponsorGuardianRef": {
                "mode": "guardianId",
                "guardianId": "guardian_alpha",
                "displayName": "Азалия"
              },
              "rivalSoul": {
                "rivalSoulId": "rival_1",
                "displayNameOrMoniker": "Багровый Охотник",
                "roleSummary": "Охотник rival-Хранителя",
                "isKnownToPlayer": true
              },
              "playerIntersection": {
                "targetsPlayerDirectly": true,
                "stakes": "Опасность для героя",
                "canBecomeSoulQuest": true,
                "recommendedCounterQuestTone": "urgent"
              },
              "milestones": [
                { "stage": 1, "title": "Слух", "summary": "О нём говорят", "visibleToPlayer": true }
              ],
              "currentStage": 1,
              "publicSignals": [
                {
                  "signalId": "sig_new",
                  "stage": 1,
                  "source": "Слух",
                  "description": "Наёмник ищет след героя",
                  "visibleToPlayer": true,
                  "bonusClueSourceProjectId": "research_major",
                  "bonusClueRevealId": "reveal_sig_new",
                  "bonusClueCost": 1
                }
              ],
              "resolution": { "outcome": "ongoing", "notes": "" }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": []
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await WriteCurrentGuardiansNormalizerBackupAsync("test_backups/preturn_guardians_lore_research_bonus_loop_normalizer_second.json");
        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>
        {
            [GuardianProjectState.TrackerPath] = "test_backups/preturn_tracker_lore_research_bonus_loop.json",
            ["game_state/meta/guardians.json"] = "test_backups/preturn_guardians_lore_research_bonus_loop_normalizer_second.json"
        });

        var guardiansRoot = await ReadObjectAsync("game_state/meta/guardians.json");
        var trackerAfterFirst = await ReadObjectAsync(GuardianProjectState.TrackerPath);
        var guardian = guardiansRoot["activeGuardian"]!.AsObject();
        var diagnostic = GuardianProjectState.BuildDiagnosticSnapshot(guardian, trackerAfterFirst);

        Assert.Equal(2, diagnostic.DerivedState.EffectiveRivalArcDefenseClues);
        Assert.Single(diagnostic.ActiveTemporaryModifiers);
        Assert.Equal(1, diagnostic.ActiveTemporaryModifiers[0].RemainingApplications);

        await _fs.WriteFileAtomicAsync(
            "test_backups/preturn_tracker_lore_research_bonus_loop.json",
            trackerAfterFirst.ToJsonString());
        await WriteCurrentGuardiansNormalizerBackupAsync("test_backups/preturn_guardians_lore_research_bonus_loop_normalizer_second_pass.json");
        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>
        {
            [GuardianProjectState.TrackerPath] = "test_backups/preturn_tracker_lore_research_bonus_loop.json",
            ["game_state/meta/guardians.json"] = "test_backups/preturn_guardians_lore_research_bonus_loop_normalizer_second_pass.json"
        });
        var trackerAfterSecond = await ReadObjectAsync(GuardianProjectState.TrackerPath);
        var spent = trackerAfterSecond["completedProjects"]!
            .AsArray()
            .Single()!
            .AsObject()["project"]!
            .AsObject()["effectState"]!
            .AsObject()["visibleRivalClueBudgetSpent"]!
            .GetValue<int>();

        Assert.Equal(1, spent);
    }

    [Fact]
    public async Task ValidateGameState_RivalBonusClue_CurrentLifeLoreResearchBudgetRemainsValid()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": []
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "rival_arc_bonus_clue_unknown_source_project", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "rival_arc_bonus_clue_inactive_source_project", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "rival_arc_bonus_clue_budget_exceeded", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_RivalBonusClue_FutureIncarnationLoreResearchBudgetIsRejected()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 4);
        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        Assert.NotNull(trackerJson);

        using var trackerDoc = JsonDocument.Parse(trackerJson);
        var lookupMethod = typeof(ValidationService).GetMethod(
            "ReadGrantedLoreResearchVisibleClueBudget",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(lookupMethod);

        var lookupResult = lookupMethod!.Invoke(
            null,
            new object?[]
            {
                trackerDoc.RootElement.Clone(),
                "guardian_alpha",
                "research_major",
                3
            });
        Assert.NotNull(lookupResult);

        var resultType = lookupResult!.GetType();
        Assert.True((bool)resultType.GetProperty("HasProject")!.GetValue(lookupResult)!);
        Assert.False((bool)resultType.GetProperty("IsCurrentLifeApplicable")!.GetValue(lookupResult)!);
        Assert.Equal(0, (int)resultType.GetProperty("GrantedBudget")!.GetValue(lookupResult)!);
    }

    [Fact]
    public async Task ValidateGameState_RivalBonusClue_MalformedCurrentRivalSoulArcsProducesExplicitIssue()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await _fs.WriteFileAtomicAsync(RivalSoulArcService.StatePath, "{");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "rival_arc_invalid_current_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_RivalBonusClue_MalformedCurrentWorldEventsProducesExplicitIssue()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
              "eventId": "evt_hunter_1",
              "relatedRivalArcId": "arc_hunter",
              "bonusClueSourceProjectId": "research_major"
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "world_event_bonus_clue_invalid_current_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_RivalBonusClue_MissingCurrentWorldEventsProducesExplicitIssue()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        _fs.DeleteFile("game_state/world/world_events.json");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "world_event_bonus_clue_invalid_current_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_RivalBonusClue_TruncatedRelevantCurrentWorldEventsProducesExplicitIssue()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
              "eventId": "evt_hunter_1",
              "relatedRivalArcId": "arc_hunter"
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "world_event_bonus_clue_invalid_current_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_RivalBonusClue_WorldEventOnlyTruncatedBeforeClueMarkersProducesExplicitIssue()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await WriteRawAsync(RivalSoulArcService.StatePath, """
        {
          "arcs": [
            {
              "arcId": "arc_hunter",
              "scope": "major",
              "arcType": "hostile_hunt",
              "status": "rising",
              "objective": "Find the player",
              "sponsorGuardianRef": {
                "mode": "guardianId",
                "guardianId": "guardian_alpha",
                "displayName": "Азалия"
              },
              "rivalSoul": {
                "rivalSoulId": "rival_1",
                "displayNameOrMoniker": "Багровый Охотник",
                "roleSummary": "Охотник rival-Хранителя",
                "isKnownToPlayer": true
              },
              "playerIntersection": {
                "targetsPlayerDirectly": true,
                "stakes": "Опасность для героя",
                "canBecomeSoulQuest": true,
                "recommendedCounterQuestTone": "urgent"
              },
              "milestones": [
                { "stage": 1, "title": "Слух", "summary": "О нём говорят", "visibleToPlayer": true }
              ],
              "currentStage": 1,
              "publicSignals": [],
              "resolution": { "outcome": "ongoing", "notes": "" }
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
              "eventId": "evt_hunter_1",
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "world_event_bonus_clue_invalid_current_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_RivalBonusClue_WorldEventOnlyMissingCurrentWorldEventsProducesExplicitIssue()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await WriteRawAsync(RivalSoulArcService.StatePath, """
        {
          "arcs": [
            {
              "arcId": "arc_hunter",
              "scope": "major",
              "arcType": "hostile_hunt",
              "status": "rising",
              "objective": "Find the player",
              "sponsorGuardianRef": {
                "mode": "guardianId",
                "guardianId": "guardian_alpha",
                "displayName": "Азалия"
              },
              "rivalSoul": {
                "rivalSoulId": "rival_1",
                "displayNameOrMoniker": "Багровый Охотник",
                "roleSummary": "Охотник rival-Хранителя",
                "isKnownToPlayer": true
              },
              "playerIntersection": {
                "targetsPlayerDirectly": true,
                "stakes": "Опасность для героя",
                "canBecomeSoulQuest": true,
                "recommendedCounterQuestTone": "urgent"
              },
              "milestones": [
                { "stage": 1, "title": "Слух", "summary": "О нём говорят", "visibleToPlayer": true }
              ],
              "currentStage": 1,
              "publicSignals": [],
              "resolution": { "outcome": "ongoing", "notes": "" }
            }
          ]
        }
        """);
        _fs.DeleteFile("game_state/world/world_events.json");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "world_event_bonus_clue_invalid_current_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_RivalBonusClue_WorldEventOnlyBareCurrentWorldEventContainerProducesExplicitIssue()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await WriteRawAsync(RivalSoulArcService.StatePath, """
        {
          "arcs": [
            {
              "arcId": "arc_hunter",
              "scope": "major",
              "arcType": "hostile_hunt",
              "status": "rising",
              "objective": "Find the player",
              "sponsorGuardianRef": {
                "mode": "guardianId",
                "guardianId": "guardian_alpha",
                "displayName": "Азалия"
              },
              "rivalSoul": {
                "rivalSoulId": "rival_1",
                "displayNameOrMoniker": "Багровый Охотник",
                "roleSummary": "Охотник rival-Хранителя",
                "isKnownToPlayer": true
              },
              "playerIntersection": {
                "targetsPlayerDirectly": true,
                "stakes": "Опасность для героя",
                "canBecomeSoulQuest": true,
                "recommendedCounterQuestTone": "urgent"
              },
              "milestones": [
                { "stage": 1, "title": "Слух", "summary": "О нём говорят", "visibleToPlayer": true }
              ],
              "currentStage": 1,
              "publicSignals": [],
              "resolution": { "outcome": "ongoing", "notes": "" }
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "world_event_bonus_clue_invalid_current_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_RivalBonusClue_WorldEventOnlyPartialRelevantCurrentWorldEventKeyProducesExplicitIssue()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await WriteRawAsync(RivalSoulArcService.StatePath, """
        {
          "arcs": [
            {
              "arcId": "arc_hunter",
              "scope": "major",
              "arcType": "hostile_hunt",
              "status": "rising",
              "objective": "Find the player",
              "sponsorGuardianRef": {
                "mode": "guardianId",
                "guardianId": "guardian_alpha",
                "displayName": "Азалия"
              },
              "rivalSoul": {
                "rivalSoulId": "rival_1",
                "displayNameOrMoniker": "Багровый Охотник",
                "roleSummary": "Охотник rival-Хранителя",
                "isKnownToPlayer": true
              },
              "playerIntersection": {
                "targetsPlayerDirectly": true,
                "stakes": "Опасность для героя",
                "canBecomeSoulQuest": true,
                "recommendedCounterQuestTone": "urgent"
              },
              "milestones": [
                { "stage": 1, "title": "Слух", "summary": "О нём говорят", "visibleToPlayer": true }
              ],
              "currentStage": 1,
              "publicSignals": [],
              "resolution": { "outcome": "ongoing", "notes": "" }
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
              "ev
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "world_event_bonus_clue_invalid_current_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_RivalBonusClue_MalformedIrrelevantCurrentWorldEventsDoNotProduceIssue()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", "{");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "world_event_bonus_clue_invalid_current_state", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "rival_arc_world_event_invalid_current_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_RivalBonusClue_NonTrivialMalformedIrrelevantCurrentWorldEventsDoNotProduceIssue()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", "{\"foo\":");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "world_event_bonus_clue_invalid_current_state", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "rival_arc_world_event_invalid_current_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_RivalBonusClue_SchemaShapedMalformedIrrelevantCurrentWorldEventsDoNotProduceIssue()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", "{\"worldEventsLog\":[{\"foo\":");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "world_event_bonus_clue_invalid_current_state", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "rival_arc_world_event_invalid_current_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_RivalBonusClue_MixedPassTruncatedBeforeClueMarkersProducesExplicitIssue()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3, visibleRivalClueBudget: 2);
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
              "eventId": "evt_hunter_1",
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "world_event_bonus_clue_invalid_current_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_RivalBonusClue_MixedPassBareCurrentWorldEventContainerProducesExplicitIssue()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3, visibleRivalClueBudget: 2);
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "world_event_bonus_clue_invalid_current_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_RivalBonusClue_MixedPassPartialRelevantCurrentWorldEventKeyProducesExplicitIssue()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3, visibleRivalClueBudget: 2);
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
              "related
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "world_event_bonus_clue_invalid_current_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_RivalBonusClue_MixedPassExhaustedPublicSignalBudgetMalformedWorldEventsProducesExplicitIssue()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3, visibleRivalClueBudget: 1);
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
              "eventId": "evt_hunter_1",
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "world_event_bonus_clue_invalid_current_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_RivalBonusClue_SponsoredWorldEventsWithoutBonusCluePath_MissingCurrentSoulStateDoesNotProduceIssue()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);

        const string emptyTrackerJson = """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """;
        await WriteRawAsync(GuardianProjectState.TrackerPath, emptyTrackerJson);
        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_tracker_rival_bonus_validation_no_bonus_path.json",
            emptyTrackerJson);
        await WriteRawAsync(RivalSoulArcService.StatePath, """
        {
          "arcs": [
            {
              "arcId": "arc_hunter",
              "scope": "major",
              "arcType": "hostile_hunt",
              "status": "rising",
              "objective": "Find the player",
              "sponsorGuardianRef": {
                "mode": "guardianId",
                "guardianId": "guardian_alpha",
                "displayName": "Азалия"
              },
              "rivalSoul": {
                "rivalSoulId": "rival_1",
                "displayNameOrMoniker": "Багровый Охотник",
                "roleSummary": "Охотник rival-Хранителя",
                "isKnownToPlayer": true
              },
              "playerIntersection": {
                "targetsPlayerDirectly": true,
                "stakes": "Опасность для героя",
                "canBecomeSoulQuest": true,
                "recommendedCounterQuestTone": "urgent"
              },
              "milestones": [
                { "stage": 1, "title": "Слух", "summary": "О нём говорят", "visibleToPlayer": true }
              ],
              "currentStage": 1,
              "publicSignals": [],
              "resolution": { "outcome": "ongoing", "notes": "" }
            }
          ]
        }
        """);
        _fs.DeleteFile("game_state/meta/soul_state.json");
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
              "eventId": "evt_hunter_1",
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "rival_arc_bonus_clue_invalid_current_soul_state", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "world_event_bonus_clue_invalid_current_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task VisibleRivalCluePreflight_DormantLoreResearchBudgetWithoutCurrentBonusClueSurface_DoesNotRequireCurrentIncarnation()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await WriteDormantRivalBonusClueValidationArcAsync();
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": []
        }
        """);
        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var arcsJson = await _fs.ReadFileAsync(RivalSoulArcService.StatePath);
        var worldEventsJson = await _fs.ReadFileAsync("game_state/world/world_events.json");
        Assert.NotNull(trackerJson);
        Assert.NotNull(arcsJson);
        var trackerRoot = JsonNode.Parse(trackerJson!)!.AsObject();
        var arcsRoot = JsonNode.Parse(arcsJson!)!.AsObject();

        Assert.False(CanonicalStateNormalizer.RequiresCurrentIncarnationForVisibleRivalCluePreflight(
            arcsRoot,
            trackerRoot,
            hasCurrentWorldEventsFile: true,
            worldEventsJson));
    }

    [Fact]
    public async Task ValidateGameState_RivalBonusClue_DormantLoreResearchBudgetWithoutCurrentBonusClueSurface_MissingCurrentSoulStateDoesNotProduceIssue()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await WriteDormantRivalBonusClueValidationArcAsync();
        _fs.DeleteFile("game_state/meta/soul_state.json");
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": []
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "rival_arc_bonus_clue_invalid_current_soul_state", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "world_event_bonus_clue_invalid_current_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_RivalBonusClue_DormantLoreResearchBudgetWithoutCurrentBonusClueSurface_MalformedCurrentSoulStateDoesNotProduceIssue()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await WriteDormantRivalBonusClueValidationArcAsync();
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{");
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": []
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "rival_arc_bonus_clue_invalid_current_soul_state", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "world_event_bonus_clue_invalid_current_state", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "afterlife_resident_invalid_current_soul_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_AfterlifeResidents_ResidentOnlyMalformedCurrentSoulStateDoesNotProduceIssue()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await WriteDormantRivalBonusClueValidationArcAsync();
        await WriteRawAsync(GuardianAbodeResidentState.StatePath, """
        {
          "entries": [
            {
              "residentId": "resident_alpha",
              "guardianId": "guardian_alpha",
              "abodeId": "abode_alpha",
              "displayName": "Обычный свидетель"
            }
          ],
          "rosterReceipts": [],
          "interactionReceipts": [],
          "historyLog": []
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{");
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": []
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_resident_invalid_current_soul_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_AfterlifeResidents_ResidentOnlyMissingCurrentSoulStateDoesNotProduceIssue()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await WriteDormantRivalBonusClueValidationArcAsync();
        await WriteRawAsync(GuardianAbodeResidentState.StatePath, """
        {
          "entries": [
            {
              "residentId": "resident_alpha",
              "guardianId": "guardian_alpha",
              "abodeId": "abode_alpha",
              "displayName": "Обычный свидетель"
            }
          ],
          "rosterReceipts": [],
          "interactionReceipts": [],
          "historyLog": []
        }
        """);
        _fs.DeleteFile("game_state/meta/soul_state.json");
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": []
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_resident_invalid_current_soul_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_AfterlifeResidents_MalformedCurrentSoulStateWithGrantedRelicSurfaceReturnsLocalAfterlifeIssue()
    {
        await WriteAfterlifeResidentGuardianFixtureAsync(
            "Resident cross-ref validation should still stay strict when granted relic links exist.");
        await WriteSingleAfterlifeResidentAsync("Свидетель реликвии", grantedRelicId: "relic_alpha");
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_resident_invalid_current_soul_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_AfterlifeResidents_ReverseOnlyCanonicalCurrentSoulReportsUnknownSourceResidentId()
    {
        await WriteAfterlifeResidentGuardianFixtureAsync(
            "Reverse resident-link validation must stay strict even without granted relic surfaces.");
        await WriteSingleAfterlifeResidentAsync("Обычный свидетель");
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World",
          "soulRelics": {
            "equipped": [],
            "stored": [
              {
                "relicId": "relic_alpha",
                "name": "Реликвия свидетеля",
                "rarity": "Rare",
                "relicType": "companion_echo",
                "companionSeed": {
                  "sourceResidentId": "resident_missing",
                  "sourceGuardianId": "guardian_alpha",
                  "companionNameHint": "Свидетель",
                  "originWorldSummary": "Память о старом союзе.",
                  "futureCompanionPrompt": "Faithful witness"
                }
              }
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "companion_echo_unknown_source_resident_id", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_resident_invalid_current_soul_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_AfterlifeResidents_CurrentSoulStateWithCrossIncarnationDataStaysReadable()
    {
        await WriteAfterlifeResidentGuardianFixtureAsync(
            "Lifecycle-compatible crossIncarnationData must not invalidate strict current soul_state resident validation.");
        await WriteSingleAfterlifeResidentAsync("Свидетель реликвии", grantedRelicId: "relic_alpha");
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World",
          "crossIncarnationData": {
            "legacyThreadId": "thread_alpha"
          },
          "soulRelics": {
            "equipped": [],
            "stored": [
              {
                "relicId": "relic_alpha",
                "name": "Реликвия свидетеля",
                "rarity": "Rare"
              }
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_resident_invalid_current_soul_state", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_abode_resident_unknown_granted_relic_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_AfterlifeResidents_MalformedSiblingCurrentSoulRootReturnsLocalAfterlifeIssue()
    {
        await WriteAfterlifeResidentGuardianFixtureAsync(
            "Malformed sibling canonical roots must make current soul_state unreadable for resident/relic validation.");
        await WriteSingleAfterlifeResidentAsync("Свидетель реликвии", grantedRelicId: "relic_alpha");
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World",
          "inkFeathers": {
            "current": "5"
          },
          "soulRelics": {
            "equipped": [],
            "stored": [
              {
                "relicId": "relic_alpha",
                "name": "Реликвия свидетеля",
                "rarity": "Rare"
              }
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_resident_invalid_current_soul_state", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_abode_resident_unknown_granted_relic_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_AfterlifeResidents_ReverseOnlyMalformedCurrentSoulStateReturnsLocalAfterlifeIssue()
    {
        await WriteAfterlifeResidentGuardianFixtureAsync(
            "Malformed current soul state must stay strict on reverse-only resident-link validation.");
        await WriteSingleAfterlifeResidentAsync("Обычный свидетель");
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_afterlife_resident_reverse_only_malformed_current_soul.json",
            """
            {
              "currentIncarnation": 3,
              "currentRealm": "Mortal World",
              "soulRelics": [
                {
                  "relicId": "relic_alpha",
                  "companionSeed": {
                    "sourceResidentId": "resident_alpha"
                  }
                }
              ]
            }
            """);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_resident_invalid_current_soul_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_AfterlifeResidents_ReverseOnlyMissingCurrentSoulStateReturnsLocalAfterlifeIssue()
    {
        await WriteAfterlifeResidentGuardianFixtureAsync(
            "Missing current soul state must stay strict on reverse-only resident-link validation.");
        await WriteSingleAfterlifeResidentAsync("Обычный свидетель");
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_afterlife_resident_reverse_only_missing_current_soul.json",
            """
            {
              "currentIncarnation": 3,
              "currentRealm": "Mortal World",
              "soulRelics": [
                {
                  "relicId": "relic_alpha",
                  "companionSeed": {
                    "sourceResidentId": "resident_alpha"
                  }
                }
              ]
            }
            """);
        _fs.DeleteFile("game_state/meta/soul_state.json");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_resident_invalid_current_soul_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_AfterlifeResidents_WhenRivalArcsSkipped_ManifestedCompanionUnknownSourceRelicStillReportsIssue()
    {
        await WriteAfterlifeResidentGuardianFixtureAsync(
            "Manifested companion source relic lookup must stay strict even when rival arc validation is skipped.");
        await WriteSingleAfterlifeResidentAsync("Обычный свидетель");
        await WriteManifestedCompanionNpcCoreAsync("relic_missing");
        _fs.DeleteFile(RivalSoulArcService.StatePath);
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World",
          "soulRelics": {
            "equipped": [],
            "stored": []
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "manifested_companion_unknown_source_relic_id", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_resident_invalid_current_soul_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_AfterlifeResidents_WhenRivalArcsSkipped_ManifestedCompanionMalformedCurrentSoulReturnsLocalAfterlifeIssue()
    {
        await WriteAfterlifeResidentGuardianFixtureAsync(
            "Manifested companion source relic lookup must fail closed on malformed current soul even when rival arcs are absent.");
        await WriteSingleAfterlifeResidentAsync("Обычный свидетель");
        await WriteManifestedCompanionNpcCoreAsync("relic_alpha");
        _fs.DeleteFile(RivalSoulArcService.StatePath);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_resident_invalid_current_soul_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_AfterlifeResidents_WhenRivalArcsSkipped_ManifestedCompanionMissingCurrentSoulReturnsLocalAfterlifeIssue()
    {
        await WriteAfterlifeResidentGuardianFixtureAsync(
            "Manifested companion source relic lookup must fail closed on missing current soul even when rival arcs are absent.");
        await WriteSingleAfterlifeResidentAsync("Обычный свидетель");
        await WriteManifestedCompanionNpcCoreAsync("relic_alpha");
        _fs.DeleteFile(RivalSoulArcService.StatePath);
        _fs.DeleteFile("game_state/meta/soul_state.json");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_resident_invalid_current_soul_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_AfterlifeResidents_MalformedCurrentRivalSoulArcsStillReportsUnknownGrantedRelicId()
    {
        await WriteAfterlifeResidentGuardianFixtureAsync(
            "Malformed rival arcs must not suppress unrelated granted relic resident validation.");
        await WriteSingleAfterlifeResidentAsync("Свидетель реликвии", grantedRelicId: "relic_missing");
        await WriteRawAsync(RivalSoulArcService.StatePath, "{");
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World",
          "soulRelics": {
            "equipped": [],
            "stored": []
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "rival_arc_invalid_current_state", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_abode_resident_unknown_granted_relic_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_AfterlifeResidents_MalformedCurrentRivalSoulArcsStillReportsUnknownSourceResidentId()
    {
        await WriteAfterlifeResidentGuardianFixtureAsync(
            "Malformed rival arcs must not suppress reverse resident-link validation.");
        await WriteSingleAfterlifeResidentAsync("Обычный свидетель");
        await WriteRawAsync(RivalSoulArcService.StatePath, "{");
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World",
          "soulRelics": {
            "equipped": [],
            "stored": [
              {
                "relicId": "relic_alpha",
                "name": "Реликвия свидетеля",
                "rarity": "Rare",
                "relicType": "companion_echo",
                "companionSeed": {
                  "sourceResidentId": "resident_missing",
                  "sourceGuardianId": "guardian_alpha",
                  "companionNameHint": "Свидетель",
                  "originWorldSummary": "Память о старом союзе.",
                  "futureCompanionPrompt": "Faithful witness"
                }
              }
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "rival_arc_invalid_current_state", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "companion_echo_unknown_source_resident_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_AfterlifeResidents_MalformedCurrentRivalSoulArcsStillReportsManifestedCompanionUnknownSourceRelicId()
    {
        await WriteAfterlifeResidentGuardianFixtureAsync(
            "Malformed rival arcs must not suppress manifested companion source relic validation.");
        await WriteSingleAfterlifeResidentAsync("Обычный свидетель");
        await WriteManifestedCompanionNpcCoreAsync("relic_missing");
        await WriteRawAsync(RivalSoulArcService.StatePath, "{");
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World",
          "soulRelics": {
            "equipped": [],
            "stored": []
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "rival_arc_invalid_current_state", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "manifested_companion_unknown_source_relic_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_AfterlifeResidents_MalformedCurrentWorldEventsStillReportsUnknownGrantedRelicId()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await WriteSingleAfterlifeResidentAsync("Свидетель реликвии", grantedRelicId: "relic_missing");
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
              "eventId": "evt_hunter_1",
              "relatedRivalArcId": "arc_hunter",
              "bonusClueSourceProjectId": "research_major"
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "world_event_bonus_clue_invalid_current_state", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_abode_resident_unknown_granted_relic_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_AfterlifeResidents_MalformedCurrentWorldEvents_DoesNotInvalidateResidentSoulStateProof()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await WriteSingleAfterlifeResidentAsync("Обычный свидетель", grantedRelicId: "relic_alpha");
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World",
          "soulRelics": {
            "equipped": [],
            "stored": [
              {
                "relicId": "relic_alpha",
                "name": "Реликвия свидетеля",
                "rarity": "Rare",
                "relicType": "companion_echo",
                "companionSeed": {
                  "sourceResidentId": "resident_missing",
                  "sourceGuardianId": "guardian_alpha",
                  "companionNameHint": "Свидетель",
                  "originWorldSummary": "Память о старом союзе.",
                  "futureCompanionPrompt": "Faithful witness"
                }
              }
            ]
          }
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
              "eventId": "evt_hunter_1",
              "relatedRivalArcId": "arc_hunter",
              "bonusClueSourceProjectId": "research_major"
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.FilePath, "game_state/world/world_events.json", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_resident_invalid_current_soul_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_AfterlifeResidents_MissingCurrentWorldEventsStillReportsManifestedCompanionUnknownSourceRelicId()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await WriteSingleAfterlifeResidentAsync("Обычный свидетель");
        await WriteManifestedCompanionNpcCoreAsync("relic_missing");
        _fs.DeleteFile("game_state/world/world_events.json");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "world_event_bonus_clue_invalid_current_state", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "manifested_companion_unknown_source_relic_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_AfterlifeResidents_MalformedCurrentNpcCoreWithUnboundedCarrierDependencyStaysPermissive()
    {
        await WriteAfterlifeResidentGuardianFixtureAsync(
            "Malformed npc_core with unbounded carrier dependency must stay permissive until a canonical carrier section can be safely bounded.");
        await WriteSingleAfterlifeResidentAsync("Обычный свидетель");
        _fs.DeleteFile(RivalSoulArcService.StatePath);
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World",
          "soulRelics": {
            "equipped": [],
            "stored": []
          }
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "NPCsInScene": [
            {
              "npcId": "npc_companion_alpha",
              "sourceAfterlifeResidentId": "resident_alpha",
              "sourceCompanionRelicId":
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_resident_invalid_current_npc_state", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "npc_contract_invalid_json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_AfterlifeResidents_MalformedCurrentNpcCoreWithoutManifestedCompanionDependencyDoesNotProduceLocalNpcIssue()
    {
        await WriteAfterlifeResidentGuardianFixtureAsync(
            "Malformed npc_core without manifested companion source-relic surface must not create false-positive resident issue.");
        await WriteSingleAfterlifeResidentAsync("Обычный свидетель");
        _fs.DeleteFile(RivalSoulArcService.StatePath);
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World",
          "soulRelics": {
            "equipped": [],
            "stored": []
          }
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "NPCsInScene": [
            {
              "npcId": "npc_ordinary",
              "foo":
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_resident_invalid_current_npc_state", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "npc_contract_invalid_json", StringComparison.OrdinalIgnoreCase));
    }

    // Owner-state matrix regressions

    [Theory]
    [MemberData(nameof(InvalidManifestedCompanionNpcCurrentStateCases))]
    public async Task ValidateGameState_AfterlifeResidents_CurrentNpcOwnerStateMatrixWithManifestedCompanionDependencyReturnsLocalNpcIssue(CurrentStateCase currentState)
    {
        await WriteAfterlifeResidentGuardianFixtureAsync(
            "Manifested companion owner-state matrix must surface local current-NPC issue.");
        await WriteSingleAfterlifeResidentAsync("Обычный свидетель");
        _fs.DeleteFile(RivalSoulArcService.StatePath);
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World",
          "soulRelics": {
            "equipped": [],
            "stored": []
          }
        }
        """);
        await ApplyCurrentStateCaseAsync("game_state/npcs/npc_core.json", currentState);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        AssertContainsIssueCodes(issues, "afterlife_resident_invalid_current_npc_state");
        AssertDoesNotContainIssueCodes(
            issues,
            "manifested_companion_missing_source_relic_id",
            "manifested_companion_duplicate_source_relic_id",
            "manifested_companion_unknown_source_relic_id");
    }

    [Theory]
    [MemberData(nameof(InvalidNonManifestedNpcCurrentStateCases))]
    public async Task ValidateGameState_AfterlifeResidents_CurrentNpcOwnerStateMatrixWithoutManifestedCompanionDependencyStaysPermissive(CurrentStateCase currentState)
    {
        await WriteAfterlifeResidentGuardianFixtureAsync(
            "NPC owner-state matrix without manifested companion dependency must stay permissive.");
        await WriteSingleAfterlifeResidentAsync("Обычный свидетель");
        _fs.DeleteFile(RivalSoulArcService.StatePath);
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World",
          "soulRelics": {
            "equipped": [],
            "stored": []
          }
        }
        """);
        await ApplyCurrentStateCaseAsync("game_state/npcs/npc_core.json", currentState);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        AssertDoesNotContainIssueCodes(issues, "afterlife_resident_invalid_current_npc_state");
        AssertDoesNotContainIssueCodes(
            issues,
            "manifested_companion_missing_source_relic_id",
            "manifested_companion_duplicate_source_relic_id",
            "manifested_companion_unknown_source_relic_id");
    }

    [Fact]
    public async Task ValidateGameState_AfterlifeResidents_CurrentNpcRenameDataWithInjectedCompanionFieldsStaysNonParticipating()
    {
        await WriteAfterlifeResidentGuardianFixtureAsync(
            "NPCsRenameData must stay lifecycle-valid but non-participating for manifested companion owner validation.");
        await WriteSingleAfterlifeResidentAsync("Обычный свидетель");
        _fs.DeleteFile(RivalSoulArcService.StatePath);
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World",
          "soulRelics": {
            "equipped": [],
            "stored": []
          }
        }
        """);
        await WriteRawAsync("game_state/npcs/npc_core.json", """
        {
          "NPCsRenameData": [
            {
              "oldName": "Обычный прохожий",
              "newName": "Переименованный прохожий",
              "sourceAfterlifeResidentId": "resident_alpha"
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        AssertDoesNotContainIssueCodes(
            issues,
            "afterlife_resident_invalid_current_npc_state",
            "manifested_companion_missing_source_relic_id",
            "manifested_companion_duplicate_source_relic_id",
            "manifested_companion_unknown_source_relic_id");
    }

    [Fact]
    public async Task ValidateGameState_AfterlifeResidents_CurrentNpcTradeReceiptUpdatesStayLifecycleValidAndNonParticipating()
    {
        await WriteAfterlifeResidentGuardianFixtureAsync(
            "UpdateNpcTradeInventoryReceipts must stay lifecycle-valid but non-participating for manifested companion owner validation.");
        await WriteSingleAfterlifeResidentAsync("Обычный свидетель");
        _fs.DeleteFile(RivalSoulArcService.StatePath);
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World",
          "soulRelics": {
            "equipped": [],
            "stored": []
          }
        }
        """);
        await WriteRawAsync("game_state/npcs/npc_core.json", """
        {
          "UpdateNpcTradeInventoryReceipts": [
            {
              "requestId": "npc_trade_req_002",
              "npcId": "npc_merchant_001",
              "npcName": "Марек",
              "tradeCycleId": "world_trade_0",
              "merchantProfile": "GeneralGoods",
              "status": "ready",
              "itemCount": 7,
              "resolvedAtTurn": 7,
              "resolvedAtUtc": "2026-03-28T00:05:00Z"
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        AssertDoesNotContainIssueCodes(
            issues,
            "afterlife_resident_invalid_current_npc_state",
            "npc_contract_missing_allowed_top_level_key",
            "npc_contract_unknown_top_level_key",
            "manifested_companion_missing_source_relic_id",
            "manifested_companion_duplicate_source_relic_id",
            "manifested_companion_unknown_source_relic_id");
    }

    [Fact]
    public async Task ValidateGameState_AfterlifeResidents_ContractInvalidNpcAliasDoesNotEmitManifestedCompanionSemanticNoise()
    {
        await WriteAfterlifeResidentGuardianFixtureAsync(
            "Lifecycle-invalid npc alias must stop at contract validation without fake manifested companion owner-state semantics.");
        await WriteSingleAfterlifeResidentAsync("Обычный свидетель");
        _fs.DeleteFile(RivalSoulArcService.StatePath);
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World",
          "soulRelics": {
            "equipped": [],
            "stored": []
          }
        }
        """);
        await WriteRawAsync("game_state/npcs/npc_core.json", """
        {
          "NPCs": [
            {
              "npcId": "npc_companion_alpha",
              "name": "Эхо спутника",
              "sourceAfterlifeResidentId": "resident_alpha"
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        AssertContainsIssueCodes(issues, "npc_contract_missing_allowed_top_level_key");
        AssertDoesNotContainIssueCodes(
            issues,
            "afterlife_resident_invalid_current_npc_state",
            "manifested_companion_missing_source_relic_id",
            "manifested_companion_duplicate_source_relic_id",
            "manifested_companion_unknown_source_relic_id");
    }

    [Theory]
    [MemberData(nameof(InvalidResidentCurrentStateCases))]
    public async Task ValidateGameState_AfterlifeResidents_CurrentResidentOwnerStateMatrixSuppressesFalseDownstreamDiagnostics(CurrentStateCase currentState)
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await ApplyCurrentStateCaseAsync(GuardianAbodeResidentState.StatePath, currentState);
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World",
          "soulRelics": [
            {
              "relicId": "relic_alpha",
              "companionSeed": {
                "sourceResidentId": "resident_missing"
              }
            }
          ]
        }
        """);
        await WriteRawAsync("game_state/quests/soul_quests.json", """
        {
          "quests": [
            {
              "questId": "soul_alpha",
              "relatedAfterlifeResidentId": "resident_missing"
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        AssertContainsIssueCodes(issues, "afterlife_resident_invalid_current_resident_state");
        AssertDoesNotContainIssueCodes(
            issues,
            "companion_echo_unknown_source_resident_id",
            "soul_quest_unknown_afterlife_resident_id");
    }

    [Fact]
    public async Task ValidateGameState_AfterlifeResidents_MalformedCurrentResidentStateWhenRivalArcsSkippedReturnsLocalResidentIssue()
    {
        await WriteAfterlifeResidentGuardianFixtureAsync(
            "Malformed current resident state must return local resident issue instead of aborting when rival arcs are skipped.");
        _fs.DeleteFile(RivalSoulArcService.StatePath);
        await WriteRawAsync(GuardianAbodeResidentState.StatePath, "{");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_resident_invalid_current_resident_state", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [MemberData(nameof(InvalidSoulQuestCurrentStateCases))]
    public async Task ValidateGameState_SoulQuests_CurrentOwnerStateMatrixSuppressesFalseResidentQuestDiagnostics(CurrentStateCase currentState)
    {
        await WriteAfterlifeResidentGuardianFixtureAsync(
            "Soul-quest owner-state matrix must surface owner issue and not fabricate resident quest back-link diagnostics.");
        _fs.DeleteFile(RivalSoulArcService.StatePath);
        await WriteSingleAfterlifeResidentAsync("Обычный свидетель", linkedSoulQuestId: "quest_missing");
        await ApplyCurrentStateCaseAsync("game_state/quests/soul_quests.json", currentState);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        AssertContainsIssueCodes(issues, "soul_quest_invalid_current_state");
        AssertDoesNotContainIssueCodes(
            issues,
            "guardian_abode_resident_unknown_linked_soul_quest_id",
            "guardian_abode_resident_linked_soul_quest_mismatch");
    }

    [Theory]
    [MemberData(nameof(InvalidSoulQuestCurrentStateCases))]
    public async Task ValidateGameState_SoulQuests_CurrentOwnerStateMatrixOnQuestOwnedRivalArcPathReturnsOwnerIssue(CurrentStateCase currentState)
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await WritePreTurnTrackedFileAsync(
            "game_state/quests/soul_quests.json",
            "test_backups/preturn_soul_quests_current_owner_state_quest_owned_rival_arc_matrix.json",
            """
            {
              "quests": [
                {
                  "questId": "quest_alpha",
                  "relatedRivalArcId": "arc_hunter"
                }
              ]
            }
            """);
        await ApplyCurrentStateCaseAsync("game_state/quests/soul_quests.json", currentState);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        AssertContainsIssueCodes(issues, "soul_quest_invalid_current_state");
        AssertDoesNotContainIssueCodes(issues, "soul_quest_unknown_rival_arc_id");
    }

    [Fact]
    public async Task ValidateGameState_WorldEvents_SkippedRivalFlowAlsoValidatesRivalVisibilityConstraints()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await WritePreTurnTrackedFileAsync(
            RivalSoulArcService.StatePath,
            "test_backups/preturn_rival_soul_arcs_skipped_world_event_visibility_reference.json",
            """
            {
              "arcs": [
                {
                  "arcId": "arc_hunter"
                }
              ]
            }
            """);
        _fs.DeleteFile(RivalSoulArcService.StatePath);
        await WriteRawAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
              "eventId": "world_evt_alpha",
              "title": "Тень чужой нити",
              "relatedRivalArcId": "arc_hunter"
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "world_event_rival_arc_missing_visibility", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_WorldEvents_BrokenCurrentRivalSoulArcsDoNotUseSkippedWorldEventRivalArcFallback()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await WritePreTurnTrackedFileAsync(
            RivalSoulArcService.StatePath,
            "test_backups/preturn_rival_soul_arcs_broken_current_skipped_world_event_arc_reference.json",
            """
            {
              "arcs": [
                {
                  "arcId": "arc_hunter"
                }
              ]
            }
            """);
        await WriteRawAsync(RivalSoulArcService.StatePath, """
        {
          "foo": []
        }
        """);
        await WriteRawAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
              "eventId": "world_evt_alpha",
              "title": "Сбой в чужой нити",
              "relatedRivalArcId": "arc_missing"
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "rival_arc_invalid_current_state", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "world_event_unknown_rival_arc_id", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "world_event_rival_arc_missing_visibility", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [MemberData(nameof(InvalidCurrentWorldEventOwnerStateCases))]
    public async Task ValidateGameState_RivalSoulArcs_CurrentWorldEventOwnerStateMatrixOnHostileDirectTargetPathReturnsExplicitIssue(CurrentStateCase currentState)
    {
        await WriteAfterlifeResidentGuardianFixtureAsync(
            "World-event owner-state matrix must fail closed on hostile direct-target rivalry contracts.");
        await WriteRawAsync(RivalSoulArcService.StatePath, """
        {
          "arcs": [
            {
              "arcId": "arc_hostile_alpha",
              "scope": "major",
              "arcType": "hostile_hunt",
              "status": "intersecting",
              "objective": "Corner the player",
              "rivalSoul": {
                "rivalSoulId": "rival_alpha",
                "displayNameOrMoniker": "Гончий из тени",
                "roleSummary": "Прямой охотник",
                "isKnownToPlayer": true
              },
              "playerIntersection": {
                "targetsPlayerDirectly": true,
                "stakes": "Немедленная угроза",
                "canBecomeSoulQuest": true,
                "recommendedCounterQuestTone": "urgent"
              },
              "publicSignals": [
                {
                  "signalId": "sig_hostile_alpha",
                  "source": "Слух",
                  "description": "Игрок знает только один след",
                  "visibleToPlayer": true
                }
              ],
              "resolution": { "outcome": "ongoing", "notes": "" }
            }
          ]
        }
        """);
        await ApplyCurrentStateCaseAsync("game_state/world/world_events.json", currentState);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        AssertContainsIssueCodes(issues, "rival_arc_world_event_invalid_current_state");
        AssertDoesNotContainIssueCodes(issues, "rival_arc_hostile_direct_target_needs_two_visible_signals");
    }

    [Theory]
    [MemberData(nameof(BrokenPresentCurrentWorldEventOwnerStateCases))]
    public async Task ValidateGameState_WorldEvents_BrokenCurrentOwnerStateMatrixWhenRivalArcsSkippedReturnsOwnerIssueWithoutFallbackDiagnostics(CurrentStateCase currentState)
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await WritePreTurnTrackedFileAsync(
            RivalSoulArcService.StatePath,
            "test_backups/preturn_rival_soul_arcs_broken_current_skipped_world_event_owner_matrix.json",
            """
            {
              "arcs": [
                {
                  "arcId": "arc_hunter"
                }
              ]
            }
            """);
        _fs.DeleteFile(RivalSoulArcService.StatePath);
        await ApplyCurrentStateCaseAsync("game_state/world/world_events.json", currentState);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        AssertContainsIssueCodes(issues, "rival_arc_world_event_invalid_current_state");
        AssertDoesNotContainIssueCodes(
            issues,
            "world_event_unknown_rival_arc_id",
            "world_event_rival_arc_missing_visibility");
    }

    [Theory]
    [MemberData(nameof(RivalCurrentStateFallbackMatrixCases))]
    public async Task ValidateGameState_SoulQuests_CurrentRivalOwnerStateMatrixControlsRivalArcFallback(
        CurrentStateCase currentState,
        bool expectOwnerIssue,
        bool expectUnknownArcIssue)
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await WritePreTurnTrackedFileAsync(
            RivalSoulArcService.StatePath,
            "test_backups/preturn_rival_soul_arcs_matrix_skipped_soul_quest_arc_reference.json",
            """
            {
              "arcs": [
                {
                  "arcId": "arc_hunter"
                }
              ]
            }
            """);
        await ApplyCurrentStateCaseAsync(RivalSoulArcService.StatePath, currentState);
        await WriteRawAsync("game_state/quests/soul_quests.json", """
        {
          "quests": [
            {
              "questId": "quest_alpha",
              "relatedRivalArcId": "arc_missing"
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        if (expectOwnerIssue)
            AssertContainsIssueCodes(issues, "rival_arc_invalid_current_state");
        else
            AssertDoesNotContainIssueCodes(issues, "rival_arc_invalid_current_state");

        if (expectUnknownArcIssue)
            AssertContainsIssueCodes(issues, "soul_quest_unknown_rival_arc_id");
        else
            AssertDoesNotContainIssueCodes(issues, "soul_quest_unknown_rival_arc_id");
    }

    [Theory]
    [MemberData(nameof(RivalCurrentStateFallbackMatrixCases))]
    public async Task ValidateGameState_WorldEvents_CurrentRivalOwnerStateMatrixControlsRivalArcFallback(
        CurrentStateCase currentState,
        bool expectOwnerIssue,
        bool expectUnknownArcIssue)
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await WritePreTurnTrackedFileAsync(
            RivalSoulArcService.StatePath,
            "test_backups/preturn_rival_soul_arcs_matrix_skipped_world_event_arc_reference.json",
            """
            {
              "arcs": [
                {
                  "arcId": "arc_hunter"
                }
              ]
            }
            """);
        await ApplyCurrentStateCaseAsync(RivalSoulArcService.StatePath, currentState);
        await WriteRawAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
              "eventId": "world_evt_alpha",
              "title": "Сбой матрицы rival state",
              "relatedRivalArcId": "arc_missing",
              "visibility": "Public"
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        if (expectOwnerIssue)
            AssertContainsIssueCodes(issues, "rival_arc_invalid_current_state");
        else
            AssertDoesNotContainIssueCodes(issues, "rival_arc_invalid_current_state");

        if (expectUnknownArcIssue)
            AssertContainsIssueCodes(issues, "world_event_unknown_rival_arc_id");
        else
            AssertDoesNotContainIssueCodes(issues, "world_event_unknown_rival_arc_id");
    }

    [Theory]
    [MemberData(nameof(RivalBonusClueCurrentSoulStateMatrixCases))]
    public async Task ValidateGameState_RivalBonusClue_CurrentSoulStateMatrixOnStrictPath(
        CurrentStateCase currentState,
        bool expectOwnerIssue)
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await WriteRawAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": []
        }
        """);
        await ApplyRawCurrentStateCaseAsync("game_state/meta/soul_state.json", currentState);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        if (expectOwnerIssue)
            AssertContainsIssueCodes(issues, "rival_arc_bonus_clue_invalid_current_soul_state");
        else
            AssertDoesNotContainIssueCodes(issues, "rival_arc_bonus_clue_invalid_current_soul_state");
    }

    [Theory]
    [MemberData(nameof(InvalidRivalBonusClueCurrentSoulStateCases))]
    public async Task ValidateGameState_RivalBonusClue_CurrentSoulStateMatrixOnDormantPathStaysPermissive(CurrentStateCase currentState)
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await WriteDormantRivalBonusClueValidationArcAsync();
        await WriteRawAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": []
        }
        """);
        await ApplyRawCurrentStateCaseAsync("game_state/meta/soul_state.json", currentState);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        AssertDoesNotContainIssueCodes(
            issues,
            "rival_arc_bonus_clue_invalid_current_soul_state",
            "world_event_bonus_clue_invalid_current_state",
            "afterlife_resident_invalid_current_soul_state");
    }

    [Theory]
    [MemberData(nameof(ResidentRelicCurrentSoulStateMatrixCases))]
    public async Task ValidateGameState_AfterlifeResidents_CurrentSoulStateMatrixOnRelicDependentPath(
        CurrentStateCase currentState,
        bool expectOwnerIssue)
    {
        await WriteAfterlifeResidentGuardianFixtureAsync(
            "Resident/relic soul-state matrix must distinguish broken current soul from readable empty relic state.");
        await WriteSingleAfterlifeResidentAsync("Свидетель реликвии", grantedRelicId: "relic_alpha");
        await ApplyRawCurrentStateCaseAsync("game_state/meta/soul_state.json", currentState);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        if (expectOwnerIssue)
        {
            AssertContainsIssueCodes(issues, "afterlife_resident_invalid_current_soul_state");
            AssertDoesNotContainIssueCodes(issues, "guardian_abode_resident_unknown_granted_relic_id");
            return;
        }

        AssertDoesNotContainIssueCodes(issues, "afterlife_resident_invalid_current_soul_state");
        AssertContainsIssueCodes(issues, "guardian_abode_resident_unknown_granted_relic_id");
    }

    [Theory]
    [MemberData(nameof(InvalidResidentRelicCurrentSoulStateCases))]
    public async Task ValidateGameState_AfterlifeResidents_CurrentSoulStateMatrixOnResidentOnlyPathStaysPermissive(CurrentStateCase currentState)
    {
        await WriteAfterlifeResidentGuardianFixtureAsync(
            "Resident-only path must not over-require current soul_state in owner-state matrix.");
        await WriteSingleAfterlifeResidentAsync("Обычный свидетель");
        await ApplyRawCurrentStateCaseAsync("game_state/meta/soul_state.json", currentState);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        AssertDoesNotContainIssueCodes(issues, "afterlife_resident_invalid_current_soul_state");
    }

    [Fact]
    public async Task ValidateGameState_SoulQuests_MissingCurrentSoulQuestsStayPermissiveInSkippedRivalFlowWithoutRivalArcReferenceSet()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await WritePreTurnTrackedFileAsync(
            "game_state/quests/soul_quests.json",
            "test_backups/preturn_soul_quests_missing_current_skipped_rival_without_arc_reference.json",
            """
            {
              "quests": [
                {
                  "questId": "quest_alpha",
                  "relatedRivalArcId": "arc_hunter"
                }
              ]
            }
            """);
        _fs.DeleteFile(RivalSoulArcService.StatePath);
        _fs.DeleteFile("game_state/quests/soul_quests.json");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "soul_quest_invalid_current_state", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "soul_quest_unknown_rival_arc_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_AfterlifeResidents_MissingCurrentResidentStateDoesNotRequireResidentFileFromPreTurnOnlyReverseSoulEvidence()
    {
        await SeedRivalBonusClueValidationScenarioAsync(targetIncarnation: 3);
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World"
        }
        """);
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_afterlife_resident_missing_current_resident_preturn_only_reverse.json",
            """
            {
              "currentIncarnation": 3,
              "currentRealm": "Mortal World",
              "soulRelics": [
                {
                  "relicId": "relic_alpha",
                  "companionSeed": {
                    "sourceResidentId": "resident_alpha"
                  }
                }
              ]
            }
            """);
        _fs.DeleteFile(GuardianAbodeResidentState.StatePath);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_resident_invalid_current_resident_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_AfterlifeResidents_ReverseOnlyMalformedCurrentSoulStateTruncatedBeforeSourceResidentTokenReturnsLocalAfterlifeIssue()
    {
        await WriteAfterlifeResidentGuardianFixtureAsync(
            "Reverse resident-link validation must stay strict even when malformed soul truncates before sourceResidentId.");
        await WriteSingleAfterlifeResidentAsync("Обычный свидетель");
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World",
          "soulRelics": [
            {
              "relicId": "relic_alpha",
              "companionSeed": {
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_resident_invalid_current_soul_state", StringComparison.OrdinalIgnoreCase));
    }

}

