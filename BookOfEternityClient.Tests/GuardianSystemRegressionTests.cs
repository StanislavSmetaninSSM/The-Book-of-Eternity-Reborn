using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GuardianSystemRegressionTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public GuardianSystemRegressionTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-guardian-system-regressions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
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
        await normalizer.NormalizeAccumulatedStateAsync();

        var guardiansRoot = await ReadObjectAsync("game_state/meta/guardians.json");
        var activeGuardian = guardiansRoot["activeGuardian"]!.AsObject();
        var derivedState = GuardianProjectState.ResolveGuardianDerivedState(activeGuardian, trackerRoot: null);
        var tradeService = new GuardianTradeService(_fs, NullLogger<GuardianTradeService>.Instance);
        var tradeView = await tradeService.EnsureTradeInventoryAsync("guardian_alpha", 1);
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

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, trackerRoot.ToJsonString());

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync();

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
        var storedEntry = soulRoot["afterlifeArchive"]!["stored"]!.AsArray().Single().AsObject();
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
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
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
        """);
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

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync();

        var guardiansRoot = await ReadObjectAsync("game_state/meta/guardians.json");
        var trackerAfterFirst = await ReadObjectAsync(GuardianProjectState.TrackerPath);
        var guardian = guardiansRoot["activeGuardian"]!.AsObject();
        var diagnostic = GuardianProjectState.BuildDiagnosticSnapshot(guardian, trackerAfterFirst);

        Assert.Equal(2, diagnostic.DerivedState.EffectiveRivalArcDefenseClues);
        Assert.Single(diagnostic.ActiveTemporaryModifiers);
        Assert.Equal(1, diagnostic.ActiveTemporaryModifiers[0].RemainingApplications);

        await normalizer.NormalizeAccumulatedStateAsync();
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

    private async Task WriteRawAsync(string path, string json) =>
        await _fs.WriteFileAtomicAsync(path, json);

    private async Task<JsonObject> ReadObjectAsync(string path)
    {
        var raw = await _fs.ReadFileAsync(path);
        Assert.False(string.IsNullOrWhiteSpace(raw));
        var node = JsonNode.Parse(raw!) as JsonObject;
        Assert.NotNull(node);
        return node!;
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
            // ignore temp cleanup failures
        }
    }
}
