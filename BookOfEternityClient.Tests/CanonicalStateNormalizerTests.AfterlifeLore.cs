using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class CanonicalStateNormalizerTests : IDisposable
{
    [Fact]
    public async Task NormalizeAccumulatedStateAsync_LoreResearchCompletion_UnlocksLoreAndStoresSystemEffectSummary()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 33 }
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
              "loreFragments": [
                {
                  "fragmentId": "frag_01",
                  "title": "Печать архива",
                  "content": null,
                  "category": "personal_history",
                  "requiredReputation": 50
                },
                { "fragmentId": "frag_02", "title": "Ф2", "content": null, "category": "cosmic_secret", "requiredReputation": 130 },
                { "fragmentId": "frag_03", "title": "Ф3", "content": null, "category": "world_lore", "requiredReputation": 0 },
                { "fragmentId": "frag_04", "title": "Ф4", "content": null, "category": "domain_secret", "requiredReputation": 50 },
                { "fragmentId": "frag_05", "title": "Ф5", "content": null, "category": "abode_truth", "requiredReputation": 130 },
                { "fragmentId": "frag_06", "title": "Ф6", "content": null, "category": "personal_history", "requiredReputation": 230 },
                { "fragmentId": "frag_07", "title": "Ф7", "content": null, "category": "world_lore", "requiredReputation": 0 }
              ],
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
            "loreFragments": [
              {
                "fragmentId": "frag_01",
                "title": "Печать архива",
                "content": null,
                "category": "personal_history",
                "requiredReputation": 50
              },
              { "fragmentId": "frag_02", "title": "Ф2", "content": null, "category": "cosmic_secret", "requiredReputation": 130 },
              { "fragmentId": "frag_03", "title": "Ф3", "content": null, "category": "world_lore", "requiredReputation": 0 },
              { "fragmentId": "frag_04", "title": "Ф4", "content": null, "category": "domain_secret", "requiredReputation": 50 },
              { "fragmentId": "frag_05", "title": "Ф5", "content": null, "category": "abode_truth", "requiredReputation": 130 },
              { "fragmentId": "frag_06", "title": "Ф6", "content": null, "category": "personal_history", "requiredReputation": 230 },
              { "fragmentId": "frag_07", "title": "Ф7", "content": null, "category": "world_lore", "requiredReputation": 0 }
            ],
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
                "projectId": "proj_lore",
                "projectType": "lore_research",
                "projectTier": "major",
                "projectMode": "supportive",
                "projectName": "Раскрытие архива",
                "activeState": "Breaking the seal of meaning",
                "totalWork": 12,
                "workDone": 12,
                "totalStages": 3,
                "currentStage": 3,
                "pressure": 4,
                "stability": 82
              }
            }
          ],
          "completeGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_lore",
              "finalState": "Completed",
              "outcome": "Хранитель раскрыл запечатанный архив.",
              "abodePowerDelta": 0,
              "projectOutcomeAudit": {
                "bonusLoreUnlocks": 1,
                "questHookCount": 1,
                "specialQuestLineUnlocks": 0,
                "visibleRivalClueBonus": 2,
                "unlockedLoreFragments": [
                  {
                    "fragmentId": "frag_01",
                    "title": "Печать архива",
                    "content": "За печатью скрывалась память о первом договоре.",
                    "category": "personal_history",
                    "requiredReputation": 50
                  }
                ]
              }
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 1,
          "currentRealm": "Mortal World",
          "inkFeathers": 5
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        var projectJournalJson = await _fs.ReadFileAsync(GuardianProjectState.JournalPath);
        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);

        Assert.NotNull(guardiansJson);
        Assert.Contains("\"unlockedByProjectId\": \"proj_lore\"", guardiansJson, StringComparison.Ordinal);
        Assert.Contains("За печатью скрывалась память о первом договоре.", guardiansJson, StringComparison.Ordinal);
        Assert.NotNull(projectJournalJson);
        Assert.Contains("раскрытые фрагменты знаний", projectJournalJson, StringComparison.Ordinal);
        Assert.NotNull(trackerJson);
        Assert.Contains("\"projectOutcomeAudit\"", trackerJson, StringComparison.Ordinal);
        Assert.Contains("\"systemEffectSummary\"", trackerJson, StringComparison.Ordinal);
    }

    [Fact]

    public async Task NormalizeAccumulatedStateAsync_ProcessGachaWithForgeBonus_StoresAuditAndConsumesGachaUse()
    {
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
              "relationshipData": { "currentReputation": 60, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "gachaSystem": { "chargesPerReturn": 2, "chargesUsedThisReturn": 0, "gachaHistory": [] }
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
            "relationshipData": { "currentReputation": 60, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "gachaSystem": { "chargesPerReturn": 2, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          },
          "UpdateGuardians": [
            {
              "command": "processGacha",
              "guardianId": "guardian_alpha",
              "inkFeathersSpent": 50,
              "gachaBonusAudit": {
                "baseRarity": "Common",
                "abodePowerBonusSteps": 1,
                "relicForgingBonusSteps": 1,
                "finalRarity": "Epic",
                "sourceProjectId": "forge_grand"
              },
              "result": {
                "relicId": "relic_alpha",
                "name": "Тестовая реликвия",
                "rarity": "Epic",
                "quality": "Epic"
              }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """
        {
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "forge_grand",
                "projectType": "relic_forging",
                "projectTier": "grand",
                "projectMode": "supportive",
                "projectName": "Великая ковка",
                "finalState": "Completed",
                "projectOutcomeAudit": {
                  "upgradedTradeSlots": 1,
                  "elevatedTradeSlots": 1,
                  "guardianRarityCeilingBonusSteps": 1
                },
                "effectState": {
                  "tradeRefreshUsesGranted": 1,
                  "tradeRefreshUsesSpent": 0,
                  "gachaUsesGranted": 1,
                  "gachaUsesSpent": 0,
                  "upgradedTradeSlotsGranted": 1,
                  "elevatedTradeSlotsGranted": 1,
                  "rarityCeilingBonusStepsGranted": 1
                }
              }
            }
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);

        Assert.NotNull(guardiansJson);
        Assert.Contains("\"gachaBonusAudit\"", guardiansJson, StringComparison.Ordinal);
        Assert.NotNull(trackerJson);
        Assert.Contains("\"gachaUsesSpent\": 1", trackerJson, StringComparison.Ordinal);
    }

    [Fact]

    public async Task NormalizeAccumulatedStateAsync_NewLoreResearchQuest_ConsumesHookToken()
    {
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
              "questManagement": {
                "availableQuests": [
                  {
                    "questId": "quest_lore_01",
                    "title": "След архива",
                    "description": "Найди утерянный след.",
                    "status": "available",
                    "difficulty": "normal",
                    "questOrigin": "lore_research_hook",
                    "sourceProjectId": "research_major"
                  }
                ],
                "activeQuests": [],
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
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "questManagement": {
              "availableQuests": [
                {
                  "questId": "quest_lore_01",
                  "title": "След архива",
                  "description": "Найди утерянный след.",
                  "status": "available",
                  "difficulty": "normal",
                  "questOrigin": "lore_research_hook",
                  "sourceProjectId": "research_major"
                }
              ],
              "activeQuests": [],
              "completedQuests": []
            },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """
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
                  "targetIncarnation": 1,
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
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 1,
          "currentRealm": "Mortal World"
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        Assert.NotNull(trackerJson);
        Assert.Contains("\"questHookTokensSpent\": 1", trackerJson, StringComparison.Ordinal);
    }

    [Fact]

    public async Task NormalizeAccumulatedStateAsync_ArchiveConsultationQuest_ConsumesGuaranteedQuest()
    {
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
              "questManagement": {
                "availableQuests": [
                  {
                    "questId": "quest_archive_01",
                    "title": "След летописи",
                    "description": "Архивная запись ведёт к долгу перед Азалией.",
                    "status": "available",
                    "difficulty": "normal",
                    "questOrigin": "archive_consultation_hook",
                    "sourceProjectId": "archive_consult_project",
                    "sourceArchiveId": "archive_lore_001",
                    "sourceArchiveTitle": "Летопись Серого Двора"
                  }
                ],
                "activeQuests": [],
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
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "questManagement": {
              "availableQuests": [
                {
                  "questId": "quest_archive_01",
                  "title": "След летописи",
                  "description": "Архивная запись ведёт к долгу перед Азалией.",
                  "status": "available",
                  "difficulty": "normal",
                  "questOrigin": "archive_consultation_hook",
                  "sourceProjectId": "archive_consult_project",
                  "sourceArchiveId": "archive_lore_001",
                  "sourceArchiveTitle": "Летопись Серого Двора"
                }
              ],
              "activeQuests": [],
              "completedQuests": []
            },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """
        {
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "archive_consult_project",
                "projectType": "lore_research",
                "projectOrigin": "archive_consultation",
                "projectTier": "minor",
                "projectMode": "supportive",
                "projectName": "Архивная консультация: Летопись Серого Двора",
                "finalState": "Completed",
                "projectOutcomeAudit": {
                  "bonusLoreUnlocks": 0,
                  "questHookCount": 0,
                  "guaranteedArchiveQuestCount": 1,
                  "specialQuestLineUnlocks": 0,
                  "visibleRivalClueBonus": 0,
                  "unlockedLoreFragments": []
                },
                "effectState": {
                  "targetIncarnation": 2,
                  "bonusLoreUnlocksApplied": 0,
                  "questHookTokensGranted": 0,
                  "questHookTokensSpent": 0,
                  "guaranteedArchiveQuestGranted": 1,
                  "guaranteedArchiveQuestSpawned": 0,
                  "guaranteedArchiveQuestConsumed": 0,
                  "specialQuestLineTokensGranted": 0,
                  "specialQuestLineTokensSpent": 0,
                  "visibleRivalClueBudgetGranted": 0,
                  "visibleRivalClueBudgetSpent": 0
                }
              }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 2,
          "currentRealm": "Mortal World"
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var journalJson = await _fs.ReadFileAsync(GuardianProjectState.JournalPath);

        Assert.NotNull(trackerJson);
        Assert.Contains("\"guaranteedArchiveQuestSpawned\": 1", trackerJson, StringComparison.Ordinal);
        Assert.Contains("\"guaranteedArchiveQuestConsumed\": 1", trackerJson, StringComparison.Ordinal);
        Assert.NotNull(journalJson);
        Assert.Contains("архивная гарантия квеста", journalJson, StringComparison.OrdinalIgnoreCase);
    }

}
