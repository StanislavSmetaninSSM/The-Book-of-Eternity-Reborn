using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class CanonicalStateNormalizerTests : IDisposable
{
    [Fact]
    public async Task NormalizeAccumulatedStateAsync_GuardianProjectCompletion_MaterializesPowerEventsAndJournal()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 42 }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
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

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """
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
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync();

        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        var journalJson = await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath);

        Assert.NotNull(guardiansJson);
        Assert.Contains("\"currentPower\": 58", guardiansJson, StringComparison.Ordinal);
        Assert.Contains("\"currentPower\": 55", guardiansJson, StringComparison.Ordinal);
        Assert.NotNull(journalJson);
        Assert.Contains("project_completion", journalJson, StringComparison.Ordinal);
        Assert.Contains("rival_strike", journalJson, StringComparison.Ordinal);
    }

    [Fact]

    public async Task NormalizeAccumulatedStateAsync_GuardianCompleteQuest_MaterializesGuardianQuestPowerEvent()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 17 }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
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
              "abodePower": { "currentPower": 35, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "relationshipData": { "currentReputation": 80, "reputationHistory": [], "lastInteraction": null },
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
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync();

        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        var journalJson = await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath);

        Assert.NotNull(guardiansJson);
        Assert.Contains("\"currentPower\": 42", guardiansJson, StringComparison.Ordinal);
        Assert.NotNull(journalJson);
        Assert.Contains("guardian_quest", journalJson, StringComparison.Ordinal);
        Assert.Contains("quest_alpha", journalJson, StringComparison.Ordinal);
    }

    [Fact]

    public async Task NormalizeAccumulatedStateAsync_ProjectUpdateAudits_MaterializePowerEvents()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 44 }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
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
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_active",
                "projectType": "abode_expansion",
                "projectTier": "major",
                "projectMode": "internal",
                "projectName": "Расширение Обители",
                "activeState": "Laying the outer ring",
                "totalWork": 18,
                "workDone": 6,
                "totalStages": 3,
                "currentStage": 1,
                "pressure": 4,
                "stability": 72
              }
            }
          ],
          "guardianProjectUpdates": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_active",
              "workDone": 8,
              "assistAudit": {
                "auditKind": "assist",
                "DomainRelevance": 2,
                "RiskOrCost": 1,
                "ScarcityOrUniqueness": 1,
                "DirectProjectImpact": 1,
                "assistScore": 5,
                "classification": "meaningful assist"
              }
            },
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_active",
              "pressure": 9,
              "stability": 69,
              "relatedGuardianId": "guardian_beta",
              "sabotageAudit": {
                "HostileReach": 1,
                "ProjectExposure": 1,
                "DamageIntent": 2,
                "DamageAchieved": 1,
                "PlayerComplicity": 1,
                "sabotageSeverityScore": 6,
                "classification": "major sabotage"
              }
            }
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync();

        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        var powerJournalJson = await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath);

        Assert.NotNull(guardiansJson);
        Assert.Contains("\"currentPower\": 40", guardiansJson, StringComparison.Ordinal);
        Assert.NotNull(powerJournalJson);
        Assert.Contains("project_assist", powerJournalJson, StringComparison.Ordinal);
        Assert.Contains("rival_strike", powerJournalJson, StringComparison.Ordinal);
    }

    [Fact]

    public async Task NormalizeAccumulatedStateAsync_OffensiveIntrigueCompletion_AppliesPoliticalImpactToTargetProject()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 52 }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
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

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """
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
        await normalizer.NormalizeAccumulatedStateAsync();

        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var journalJson = await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath);
        var projectJournalJson = await _fs.ReadFileAsync(GuardianProjectState.JournalPath);

        Assert.NotNull(guardiansJson);
        Assert.Contains("\"currentPower\": 58", guardiansJson, StringComparison.Ordinal);
        Assert.Contains("\"currentPower\": 55", guardiansJson, StringComparison.Ordinal);
        Assert.NotNull(trackerJson);
        Assert.Contains("\"pressure\": 11", trackerJson, StringComparison.Ordinal);
        Assert.Contains("\"stability\": 72", trackerJson, StringComparison.Ordinal);
        Assert.NotNull(journalJson);
        Assert.Contains("rival_strike", journalJson, StringComparison.Ordinal);
        Assert.NotNull(projectJournalJson);
        Assert.Contains("rival-интрига", projectJournalJson, StringComparison.Ordinal);
    }

    [Fact]

    public async Task NormalizeAccumulatedStateAsync_CounterRivalOperationCompletion_RelievesDefendedProjectAndDefaultsPowerDelta()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 61 }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
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
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
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
              "abodePower": { "currentPower": 65, "tier": "Могущественная", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
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
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_counter",
                "projectType": "counter_rival_operation",
                "projectTier": "major",
                "projectMode": "supportive",
                "targetGuardianId": "guardian_beta",
                "projectName": "Контр-операция Азалии",
                "activeState": "Severing the rival pressure",
                "totalWork": 16,
                "workDone": 16,
                "totalStages": 3,
                "currentStage": 3,
                "pressure": 9,
                "stability": 68
              }
            },
            {
              "guardianId": "guardian_beta",
              "project": {
                "projectId": "proj_beta_active",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "targetGuardianId": "guardian_alpha",
                "projectName": "Враждебное давление Варака",
                "activeState": "Undermining the rival's hold",
                "totalWork": 18,
                "workDone": 8,
                "totalStages": 3,
                "currentStage": 1,
                "pressure": 30,
                "stability": 60
              }
            }
          ],
          "completeGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_counter",
              "targetGuardianId": "guardian_beta",
              "finalState": "Completed",
              "outcome": "Контр-операция сбила вражеское давление."
            }
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync();

        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var powerJournalJson = await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath);

        Assert.NotNull(guardiansJson);
        Assert.Contains("\"currentPower\": 42", guardiansJson, StringComparison.Ordinal);
        Assert.NotNull(trackerJson);
        Assert.Contains("\"pressure\": 12", trackerJson, StringComparison.Ordinal);
        Assert.Contains("\"stability\": 68", trackerJson, StringComparison.Ordinal);
        Assert.Contains("\"abodePowerDelta\": 2", trackerJson, StringComparison.Ordinal);
        Assert.NotNull(powerJournalJson);
        Assert.Contains("rival_defense", powerJournalJson, StringComparison.Ordinal);
    }


}
