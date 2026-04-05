using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GuardianCorrectionServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ScenarioCoreService _scenarioCoreService;
    private readonly GuardianCorrectionService _guardianCorrectionService;

    public GuardianCorrectionServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-guardian-corrections-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _scenarioCoreService = new ScenarioCoreService(_fs, NullLogger<ScenarioCoreService>.Instance);
        _guardianCorrectionService = new GuardianCorrectionService(_fs, _scenarioCoreService, NullLogger<GuardianCorrectionService>.Instance);
    }

    [Fact]
    public async Task ApplyForNewLifeAsync_FriendlyGuardianCreatesCorrectionsAndSpendsAbodePower()
    {
        await WriteRawAsync(ScenarioCoreService.ManifestPath, """
        {
          "scenarioCoreAssertions": [
            { "assertionId": "core_role", "category": "role_status", "value": "Игрок начинает королём", "explicit": true, "source": "structured_field" }
          ],
          "candidateAssertions": [],
          "openCorrectionSlots": [
            { "slotId": "slot_protection", "slotType": "protection_or_omen", "maxSeverity": "medium", "allowsFriendly": true, "allowsHostile": true, "sourceAssertionId": "core_role" },
            { "slotId": "slot_ally", "slotType": "ally_thread", "maxSeverity": "medium", "allowsFriendly": true, "allowsHostile": true, "sourceAssertionId": "core_role" }
          ]
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guard_test_azalia",
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
              "relationshipData": { "currentReputation": 95, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 80, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "gachaSystem": { "chargesPerReturn": 3, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guard_test_azalia",
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
            "relationshipData": { "currentReputation": 95, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 80, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
            "gachaSystem": { "chargesPerReturn": 3, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await _guardianCorrectionService.ApplyForNewLifeAsync(2);
        var state = await _guardianCorrectionService.ReadAsync();
        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");

        Assert.NotNull(state);
        Assert.Equal("friendly", state!.Intent);
        Assert.NotEmpty(state.Corrections);
        Assert.True(state.PowerAfter < state.PowerBefore);
        Assert.NotNull(guardiansJson);
        Assert.Contains($"\"currentPower\": {state.PowerAfter}", guardiansJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyForNewLifeAsync_HostileGuardianCreatesAtLeastOneHostileCorrection()
    {
        await WriteRawAsync(ScenarioCoreService.ManifestPath, """
        {
          "scenarioCoreAssertions": [
            { "assertionId": "core_role", "category": "role_status", "value": "Игрок начинает королём", "explicit": true, "source": "structured_field" }
          ],
          "candidateAssertions": [],
          "openCorrectionSlots": [
            { "slotId": "slot_rival", "slotType": "rival_thread", "maxSeverity": "strong", "allowsFriendly": true, "allowsHostile": true, "sourceAssertionId": "core_role" }
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

        await _guardianCorrectionService.ApplyForNewLifeAsync(3);
        var state = await _guardianCorrectionService.ReadAsync();

        Assert.NotNull(state);
        Assert.Equal("hostile", state!.Intent);
        Assert.Contains(state.Corrections, correction => correction.Intent == "hostile");
        Assert.Contains(state.Corrections, correction => correction.SlotType == "rival_thread");
    }

    [Fact]
    public async Task ApplyForNewLifeAsync_RivalOffensiveClaimant_CanContestCorrectionSlots()
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
              "guardianId": "guard_test_active",
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
            },
            {
              "guardianId": "guard_test_rival",
              "canonicalName": "Нерис",
              "nameVariants": { "default": "Нерис", "feminine": "Нерис", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Нерис",
                "formFlexibility": "adaptive",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 70, "tier": "Могущественная", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guard_test_active",
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

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "completedProjects": [
            {
              "guardianId": "guard_test_rival",
              "project": {
                "projectId": "proj_intrigue_001",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Чужая интрига",
                "targetGuardianId": "guard_test_active",
                "finalState": "Completed",
                "completionTurn": 10
              }
            }
          ]
        }
        """);

        await _guardianCorrectionService.ApplyForNewLifeAsync(5);
        var state = await _guardianCorrectionService.ReadAsync();

        Assert.NotNull(state);
        Assert.True(state!.Claimants.Count >= 2);
        Assert.Contains(state.Claimants, claimant => claimant.GuardianId == "guard_test_rival");
        Assert.Contains(state.ContestedSlots, contest => contest.SlotId == "slot_rival" && contest.Candidates.Count >= 2);
        Assert.Contains(state.ResolutionOrder, step => step.Contains("slot_rival", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ApplyForNewLifeAsync_SoulPreparationBonuses_AggregateIntoClaimantBudget()
    {
        await WriteRawAsync(ScenarioCoreService.ManifestPath, """
        {
          "scenarioCoreAssertions": [
            { "assertionId": "core_role", "category": "role_status", "value": "Игрок начинает королём", "explicit": true, "source": "structured_field" }
          ],
          "candidateAssertions": [],
          "openCorrectionSlots": [
            { "slotId": "slot_protection", "slotType": "protection_or_omen", "maxSeverity": "medium", "allowsFriendly": true, "allowsHostile": true, "sourceAssertionId": "core_role" }
          ]
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guard_test_azalia",
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
              "relationshipData": { "currentReputation": 95, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 80, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "gachaSystem": { "chargesPerReturn": 3, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guard_test_azalia",
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
            "relationshipData": { "currentReputation": 95, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 80, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
            "gachaSystem": { "chargesPerReturn": 3, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "completedProjects": [
            {
              "guardianId": "guard_test_azalia",
              "project": {
                "projectId": "prep_major",
                "projectType": "soul_preparation",
                "projectTier": "major",
                "projectMode": "supportive",
                "projectName": "Подготовка пути",
                "finalState": "Completed",
                "completionTurn": 10,
                "projectOutcomeAudit": {
                  "preparationBudgetPoints": 2,
                  "preparationClaimPriorityBonus": 1
                }
              }
            },
            {
              "guardianId": "guard_test_azalia",
              "project": {
                "projectId": "prep_minor",
                "projectType": "soul_preparation",
                "projectTier": "minor",
                "projectMode": "supportive",
                "projectName": "Тихая настройка",
                "finalState": "Completed",
                "completionTurn": 11,
                "projectOutcomeAudit": {
                  "preparationBudgetPoints": 1,
                  "preparationClaimPriorityBonus": 1
                }
              }
            }
          ]
        }
        """);

        await _guardianCorrectionService.ApplyForNewLifeAsync(6);
        var state = await _guardianCorrectionService.ReadAsync();

        Assert.NotNull(state);
        var activeClaimant = Assert.Single(state!.Claimants, claimant => claimant.GuardianId == "guard_test_azalia");
        Assert.Equal(3, activeClaimant.PreparationBudgetPoints);
        Assert.Equal(7, activeClaimant.BaseBudgetPoints + activeClaimant.PreparationBudgetPoints);
    }

    [Fact]
    public async Task ApplyForNewLifeAsync_SoulPreparationBonuses_AreConsumedAfterFirstTargetLife()
    {
        await WriteRawAsync(ScenarioCoreService.ManifestPath, """
        {
          "scenarioCoreAssertions": [
            { "assertionId": "core_role", "category": "role_status", "value": "Игрок начинает королём", "explicit": true, "source": "structured_field" }
          ],
          "candidateAssertions": [],
          "openCorrectionSlots": [
            { "slotId": "slot_protection", "slotType": "protection_or_omen", "maxSeverity": "medium", "allowsFriendly": true, "allowsHostile": true, "sourceAssertionId": "core_role" }
          ]
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guard_test_azalia",
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
              "relationshipData": { "currentReputation": 95, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 80, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "gachaSystem": { "chargesPerReturn": 3, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guard_test_azalia",
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
            "relationshipData": { "currentReputation": 95, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 80, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
            "gachaSystem": { "chargesPerReturn": 3, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "completedProjects": [
            {
              "guardianId": "guard_test_azalia",
              "project": {
                "projectId": "prep_major",
                "projectType": "soul_preparation",
                "projectTier": "major",
                "projectMode": "supportive",
                "projectName": "Подготовка пути",
                "finalState": "Completed",
                "completionTurn": 10,
                "projectOutcomeAudit": {
                  "preparationBudgetPoints": 2,
                  "preparationClaimPriorityBonus": 1
                },
                "effectState": {
                  "targetIncarnation": 6,
                  "preparationBudgetPointsGranted": 2,
                  "preparationBudgetPointsSpent": 0,
                  "preparationClaimPriorityBonusGranted": 1,
                  "consumedAtLifeStart": false
                }
              }
            }
          ]
        }
        """);

        await _guardianCorrectionService.ApplyForNewLifeAsync(6);
        var trackerAfterFirst = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        Assert.NotNull(trackerAfterFirst);
        Assert.Contains("\"consumedAtLifeStart\": true", trackerAfterFirst, StringComparison.Ordinal);

        await _guardianCorrectionService.ApplyForNewLifeAsync(7);
        var secondState = await _guardianCorrectionService.ReadAsync();
        var secondClaimant = Assert.Single(secondState!.Claimants, claimant => claimant.GuardianId == "guard_test_azalia");
        Assert.Equal(0, secondClaimant.PreparationBudgetPoints);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_NamedMortalRealm_ReturnsReminder()
    {
        await WriteRawAsync(GuardianCorrectionService.StatePath, """
        {
          "lifeIncarnation": 3,
          "appliedAt": "2026-03-27T00:00:00Z",
          "guardianId": "guard_test_azalia",
          "guardianName": "Азалия",
          "intent": "friendly",
          "reputationAtApplication": 90,
          "powerBefore": 70,
          "powerAfter": 63,
          "baseBudgetPoints": 2,
          "remainingBudgetPoints": 0,
          "totalAbodePowerSpent": 7,
          "summary": "Азалия мягко корректирует старт.",
          "scenarioCoreSnapshot": {
            "scenarioCoreAssertions": [],
            "openCorrectionSlots": []
          },
          "claimants": [],
          "contestedSlots": [],
          "resolutionOrder": [],
          "corrections": []
        }
        """);

        var reminder = await _guardianCorrectionService.BuildSystemReminderFragmentAsync("Неон-Сити");

        Assert.NotNull(reminder);
        Assert.Contains("GUARDIAN CORRECTIONS FOR THIS LIFE", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyForNewLifeAsync_MutualNonHostileCoalitionTrace_StrengthensActivePatronClaim()
    {
        await WriteRawAsync(ScenarioCoreService.ManifestPath, """
        {
          "scenarioCoreAssertions": [
            { "assertionId": "core_role", "category": "role_status", "value": "Игрок начинает королём", "explicit": true, "source": "structured_field" }
          ],
          "candidateAssertions": [],
          "openCorrectionSlots": [
            { "slotId": "slot_rival", "slotType": "rival_thread", "maxSeverity": "strong", "allowsFriendly": true, "allowsHostile": true, "sourceAssertionId": "core_role" }
          ]
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guard_test_active",
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
              "relationshipData": { "currentReputation": 95, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 82, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guard_test_rival", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Open rivalry", "lastChangedAt": null },
                { "targetGuardianId": "guard_test_support", "attitudeScore": 5, "attitudeTier": "neutral", "reason": "Measured respect", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            },
            {
              "guardianId": "guard_test_rival",
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
              "relationshipData": { "currentReputation": -85, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 74, "tier": "Могущественная", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guard_test_active", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Open rivalry", "lastChangedAt": null },
                { "targetGuardianId": "guard_test_support", "attitudeScore": -30, "attitudeTier": "competitive", "reason": "Cold distance", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            },
            {
              "guardianId": "guard_test_support",
              "canonicalName": "Нерис",
              "nameVariants": { "default": "Нерис", "feminine": "Нерис", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Нерис",
                "formFlexibility": "adaptive",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 68, "tier": "Могущественная", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guard_test_active", "attitudeScore": 0, "attitudeTier": "neutral", "reason": "Measured respect", "lastChangedAt": null },
                { "targetGuardianId": "guard_test_rival", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Shared enemy", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guard_test_active",
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
            "relationshipData": { "currentReputation": 95, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 82, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
            "guardianRelationships": [
              { "targetGuardianId": "guard_test_rival", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Open rivalry", "lastChangedAt": null },
              { "targetGuardianId": "guard_test_support", "attitudeScore": 5, "attitudeTier": "neutral", "reason": "Measured respect", "lastChangedAt": null }
            ],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guard_test_support",
              "project": {
                "projectId": "proj_support_trace",
                "projectType": "offensive_intrigue",
                "projectTier": "minor",
                "projectMode": "offensive",
                "projectName": "Скоординированное давление",
                "targetGuardianId": "guard_test_rival",
                "activeState": "Coordinating against the shared enemy",
                "totalWork": 12,
                "workDone": 4,
                "totalStages": 2,
                "currentStage": 1,
                "pressure": 4,
                "stability": 70
              }
            }
          ],
          "completedProjects": [
            {
              "guardianId": "guard_test_rival",
              "project": {
                "projectId": "proj_intrigue_001",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Чужая интрига",
                "targetGuardianId": "guard_test_active",
                "finalState": "Completed",
                "completionTurn": 10
              }
            }
          ]
        }
        """);

        await _guardianCorrectionService.ApplyForNewLifeAsync(5);
        var state = await _guardianCorrectionService.ReadAsync();

        Assert.NotNull(state);
        var activeClaimant = Assert.Single(state!.Claimants, claimant => claimant.GuardianId == "guard_test_active");
        Assert.Contains("coalition support +1", activeClaimant.SourceSummary, StringComparison.Ordinal);
        Assert.True(activeClaimant.ClaimStrengthBase > AbodePowerRules.GetCorrectionClaimPowerBand(activeClaimant.CurrentPower));
    }

    [Fact]
    public async Task ApplyForNewLifeAsync_WithoutCurrentCoalitionTrace_DoesNotGrantActivePatronSupportBonus()
    {
        await WriteRawAsync(ScenarioCoreService.ManifestPath, """
        {
          "scenarioCoreAssertions": [
            { "assertionId": "core_role", "category": "role_status", "value": "Игрок начинает королём", "explicit": true, "source": "structured_field" }
          ],
          "candidateAssertions": [],
          "openCorrectionSlots": [
            { "slotId": "slot_rival", "slotType": "rival_thread", "maxSeverity": "strong", "allowsFriendly": true, "allowsHostile": true, "sourceAssertionId": "core_role" }
          ]
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guard_test_active",
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
              "relationshipData": { "currentReputation": 95, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 82, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guard_test_rival", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Open rivalry", "lastChangedAt": null },
                { "targetGuardianId": "guard_test_support", "attitudeScore": 55, "attitudeTier": "ally", "reason": "Trusted ally", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            },
            {
              "guardianId": "guard_test_rival",
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
              "relationshipData": { "currentReputation": -85, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 74, "tier": "Могущественная", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guard_test_active", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Open rivalry", "lastChangedAt": null },
                { "targetGuardianId": "guard_test_support", "attitudeScore": -30, "attitudeTier": "competitive", "reason": "Cold distance", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            },
            {
              "guardianId": "guard_test_support",
              "canonicalName": "Нерис",
              "nameVariants": { "default": "Нерис", "feminine": "Нерис", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Нерис",
                "formFlexibility": "adaptive",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 68, "tier": "Могущественная", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guard_test_active", "attitudeScore": 60, "attitudeTier": "ally", "reason": "Support pact", "lastChangedAt": null },
                { "targetGuardianId": "guard_test_rival", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Shared enemy", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guard_test_active",
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
            "relationshipData": { "currentReputation": 95, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 82, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
            "guardianRelationships": [
              { "targetGuardianId": "guard_test_rival", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Open rivalry", "lastChangedAt": null },
              { "targetGuardianId": "guard_test_support", "attitudeScore": 55, "attitudeTier": "ally", "reason": "Trusted ally", "lastChangedAt": null }
            ],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "completedProjects": [
            {
              "guardianId": "guard_test_rival",
              "project": {
                "projectId": "proj_intrigue_001",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Чужая интрига",
                "targetGuardianId": "guard_test_active",
                "finalState": "Completed",
                "completionTurn": 10
              }
            }
          ]
        }
        """);

        await _guardianCorrectionService.ApplyForNewLifeAsync(5);
        var state = await _guardianCorrectionService.ReadAsync();

        Assert.NotNull(state);
        var activeClaimant = Assert.Single(state!.Claimants, claimant => claimant.GuardianId == "guard_test_active");
        Assert.DoesNotContain("coalition support +1", activeClaimant.SourceSummary, StringComparison.Ordinal);
        Assert.Equal(AbodePowerRules.GetCorrectionClaimPowerBand(activeClaimant.CurrentPower) + 1, activeClaimant.ClaimStrengthBase);
    }

    [Fact]
    public async Task ApplyForNewLifeAsync_OneWayFriendlyButHostileReverseRelation_DoesNotGrantCoalitionSupportBonus()
    {
        await WriteRawAsync(ScenarioCoreService.ManifestPath, """
        {
          "scenarioCoreAssertions": [
            { "assertionId": "core_role", "category": "role_status", "value": "Игрок начинает королём", "explicit": true, "source": "structured_field" }
          ],
          "candidateAssertions": [],
          "openCorrectionSlots": [
            { "slotId": "slot_rival", "slotType": "rival_thread", "maxSeverity": "strong", "allowsFriendly": true, "allowsHostile": true, "sourceAssertionId": "core_role" }
          ]
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guard_test_active",
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
              "relationshipData": { "currentReputation": 95, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 82, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guard_test_rival", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Open rivalry", "lastChangedAt": null },
                { "targetGuardianId": "guard_test_support", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Broken accord", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            },
            {
              "guardianId": "guard_test_rival",
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
              "relationshipData": { "currentReputation": -85, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 74, "tier": "Могущественная", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guard_test_active", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Open rivalry", "lastChangedAt": null },
                { "targetGuardianId": "guard_test_support", "attitudeScore": -30, "attitudeTier": "competitive", "reason": "Cold distance", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            },
            {
              "guardianId": "guard_test_support",
              "canonicalName": "Нерис",
              "nameVariants": { "default": "Нерис", "feminine": "Нерис", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Нерис",
                "formFlexibility": "adaptive",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 68, "tier": "Могущественная", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guard_test_active", "attitudeScore": 60, "attitudeTier": "ally", "reason": "Old loyalty", "lastChangedAt": null },
                { "targetGuardianId": "guard_test_rival", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Shared enemy", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guard_test_active",
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
            "relationshipData": { "currentReputation": 95, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 82, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
            "guardianRelationships": [
              { "targetGuardianId": "guard_test_rival", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Open rivalry", "lastChangedAt": null },
              { "targetGuardianId": "guard_test_support", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Broken accord", "lastChangedAt": null }
            ],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guard_test_support",
              "project": {
                "projectId": "proj_support_trace",
                "projectType": "offensive_intrigue",
                "projectTier": "minor",
                "projectMode": "offensive",
                "projectName": "Запоздалая помощь",
                "targetGuardianId": "guard_test_rival",
                "activeState": "Attempting to coordinate",
                "totalWork": 12,
                "workDone": 4,
                "totalStages": 2,
                "currentStage": 1,
                "pressure": 4,
                "stability": 70
              }
            }
          ],
          "completedProjects": [
            {
              "guardianId": "guard_test_rival",
              "project": {
                "projectId": "proj_intrigue_001",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Чужая интрига",
                "targetGuardianId": "guard_test_active",
                "finalState": "Completed",
                "completionTurn": 10
              }
            }
          ]
        }
        """);

        await _guardianCorrectionService.ApplyForNewLifeAsync(5);
        var state = await _guardianCorrectionService.ReadAsync();

        Assert.NotNull(state);
        var activeClaimant = Assert.Single(state!.Claimants, claimant => claimant.GuardianId == "guard_test_active");
        Assert.DoesNotContain("coalition support +1", activeClaimant.SourceSummary, StringComparison.Ordinal);
    }

    private async Task WriteRawAsync(string path, string content)
    {
        await _fs.WriteFileAtomicAsync(path, content);
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
