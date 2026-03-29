using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GuardianProjectStateTests
{
    [Fact]
    public void ResolveDerivedEffects_AggregatesRecipeDrivenBonuses()
    {
        var trackerRoot = JsonNode.Parse("""
        {
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "forge_grand",
                "projectType": "relic_forging",
                "projectTier": "grand",
                "finalState": "Completed",
                "projectOutcomeAudit": {
                  "upgradedTradeSlots": 1,
                  "elevatedTradeSlots": 1,
                  "guardianRarityCeilingBonusSteps": 1
                }
              }
            },
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "research_major",
                "projectType": "lore_research",
                "projectTier": "major",
                "finalState": "Completed",
                "projectOutcomeAudit": {
                  "bonusLoreUnlocks": 1,
                  "questHookCount": 1,
                  "specialQuestLineUnlocks": 0,
                  "visibleRivalClueBonus": 2
                }
              }
            },
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "research_minor",
                "projectType": "lore_research",
                "projectTier": "minor",
                "finalState": "Completed",
                "projectOutcomeAudit": {
                  "bonusLoreUnlocks": 1,
                  "questHookCount": 0,
                  "specialQuestLineUnlocks": 0,
                  "visibleRivalClueBonus": 1
                }
              }
            },
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "prep_minor",
                "projectType": "soul_preparation",
                "projectTier": "minor",
                "finalState": "Completed",
                "projectOutcomeAudit": {
                  "preparationBudgetPoints": 1,
                  "preparationClaimPriorityBonus": 1
                }
              }
            },
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "prep_fail",
                "projectType": "soul_preparation",
                "projectTier": "major",
                "finalState": "Sabotaged",
                "projectOutcomeAudit": {
                  "hostilePriorityTokensGranted": 2
                }
              }
            }
          ]
        }
        """)!.AsObject();

        var effects = GuardianProjectState.ResolveDerivedEffects(trackerRoot, "guardian_alpha");

        Assert.Equal(1, effects.UpgradedTradeSlots);
        Assert.Equal(1, effects.ElevatedTradeSlots);
        Assert.Equal(1, effects.GuardianRarityCeilingBonusSteps);
        Assert.Equal(2, effects.BonusLoreUnlocks);
        Assert.Equal(1, effects.QuestHookCount);
        Assert.Equal(0, effects.SpecialQuestLineUnlocks);
        Assert.Equal(3, effects.VisibleRivalClueBonus);
        Assert.Equal(1, effects.PreparationBudgetPoints);
        Assert.Equal(1, effects.PreparationClaimPriorityBonus);
        Assert.Equal(2, effects.HostilePriorityTokensGranted);
        Assert.Equal(0, effects.FortificationSafePressureBonus);
        Assert.Equal(0, effects.FortificationDefenseRatingBonus);

        Assert.Equal(2, GuardianProjectState.GetEffectiveNextLifeCorrectionBudgetPoints(35, effects));
        Assert.Equal(1, GuardianProjectState.GetEffectiveGuardianRarityCeilingBonusSteps(35, effects));
        Assert.Equal(2, GuardianProjectState.GetEffectiveUpgradedTradeSlots(35, effects));
        Assert.Equal(1, GuardianProjectState.GetEffectiveElevatedTradeSlots(35, effects));
        Assert.Equal(4, GuardianProjectState.GetEffectiveRivalArcDefenseClues(35, effects));
    }

    [Fact]
    public void ResolveDerivedEffects_UsesRemainingEffectStateInsteadOfCompletedProjectPresence()
    {
        var trackerRoot = JsonNode.Parse("""
        {
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "forge_spent",
                "projectType": "relic_forging",
                "projectTier": "grand",
                "finalState": "Completed",
                "projectOutcomeAudit": {
                  "upgradedTradeSlots": 1,
                  "elevatedTradeSlots": 1,
                  "guardianRarityCeilingBonusSteps": 1
                },
                "effectState": {
                  "tradeRefreshUsesGranted": 1,
                  "tradeRefreshUsesSpent": 1,
                  "gachaUsesGranted": 1,
                  "gachaUsesSpent": 0,
                  "upgradedTradeSlotsGranted": 1,
                  "elevatedTradeSlotsGranted": 1,
                  "rarityCeilingBonusStepsGranted": 1
                }
              }
            },
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "research_live",
                "projectType": "lore_research",
                "projectTier": "major",
                "finalState": "Completed",
                "projectOutcomeAudit": {
                  "bonusLoreUnlocks": 1,
                  "questHookCount": 1,
                  "specialQuestLineUnlocks": 0,
                  "visibleRivalClueBonus": 2
                },
                "effectState": {
                  "targetIncarnation": 3,
                  "bonusLoreUnlocksApplied": 1,
                  "questHookTokensGranted": 1,
                  "questHookTokensSpent": 1,
                  "specialQuestLineTokensGranted": 0,
                  "specialQuestLineTokensSpent": 0,
                  "visibleRivalClueBudgetGranted": 2,
                  "visibleRivalClueBudgetSpent": 1
                }
              }
            },
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "prep_consumed",
                "projectType": "soul_preparation",
                "projectTier": "major",
                "finalState": "Completed",
                "projectOutcomeAudit": {
                  "preparationBudgetPoints": 2,
                  "preparationClaimPriorityBonus": 1
                },
                "effectState": {
                  "targetIncarnation": 4,
                  "preparationBudgetPointsGranted": 2,
                  "preparationBudgetPointsSpent": 2,
                  "preparationClaimPriorityBonusGranted": 1,
                  "consumedAtLifeStart": true
                }
              }
            }
          ]
        }
        """)!.AsObject();

        var effects = GuardianProjectState.ResolveDerivedEffects(trackerRoot, "guardian_alpha");

        Assert.Equal(0, effects.UpgradedTradeSlots);
        Assert.Equal(0, effects.ElevatedTradeSlots);
        Assert.Equal(0, effects.GuardianRarityCeilingBonusSteps);
        Assert.Equal(1, effects.BonusLoreUnlocks);
        Assert.Equal(0, effects.QuestHookCount);
        Assert.Equal(0, effects.SpecialQuestLineUnlocks);
        Assert.Equal(1, effects.VisibleRivalClueBonus);
        Assert.Equal(0, effects.PreparationBudgetPoints);
        Assert.Equal(0, effects.PreparationClaimPriorityBonus);
    }

    [Fact]
    public void TryConsumeVisibleRivalClue_SpendsOnlyAvailableLoreBudget()
    {
        var trackerRoot = JsonNode.Parse("""
        {
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "research_live",
                "projectType": "lore_research",
                "projectTier": "major",
                "finalState": "Completed",
                "projectOutcomeAudit": {
                  "visibleRivalClueBonus": 2
                },
                "effectState": {
                  "targetIncarnation": 3,
                  "visibleRivalClueBudgetGranted": 2,
                  "visibleRivalClueBudgetSpent": 0
                }
              }
            }
          ]
        }
        """)!.AsObject();

        Assert.True(GuardianProjectState.TryConsumeVisibleRivalClue(trackerRoot, "guardian_alpha", "research_live", 3, 1));
        Assert.Equal(1, GuardianProjectState.GetRemainingVisibleRivalClueBudget(trackerRoot, "guardian_alpha", "research_live", 3));

        Assert.True(GuardianProjectState.TryConsumeVisibleRivalClue(trackerRoot, "guardian_alpha", "research_live", 3, 1));
        Assert.Equal(0, GuardianProjectState.GetRemainingVisibleRivalClueBudget(trackerRoot, "guardian_alpha", "research_live", 3));

        Assert.False(GuardianProjectState.TryConsumeVisibleRivalClue(trackerRoot, "guardian_alpha", "research_live", 3, 1));
    }

    [Fact]
    public void EnsureRecipeEffectState_NamedMortalRealm_TargetsCurrentIncarnation()
    {
        var project = JsonNode.Parse("""
        {
          "projectId": "research_named_world",
          "projectType": "lore_research",
          "projectTier": "major",
          "finalState": "Completed",
          "projectOutcomeAudit": {
            "bonusLoreUnlocks": 1,
            "questHookCount": 1,
            "specialQuestLineUnlocks": 0,
            "visibleRivalClueBonus": 1
          }
        }
        """)!.AsObject();

        var effectState = GuardianProjectState.EnsureRecipeEffectState(project, 4, "Неон-Сити");

        Assert.Equal(4, effectState["targetIncarnation"]!.GetValue<int>());
    }

    [Fact]
    public void ResolveDerivedEffects_TracksGuaranteedArchiveQuestSeparatelyFromOrdinaryHooks()
    {
        var trackerRoot = JsonNode.Parse("""
        {
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "archive_consult_project",
                "projectType": "lore_research",
                "projectOrigin": "archive_consultation",
                "projectTier": "minor",
                "finalState": "Completed",
                "projectOutcomeAudit": {
                  "questHookCount": 0,
                  "guaranteedArchiveQuestCount": 1,
                  "visibleRivalClueBonus": 0
                },
                "effectState": {
                  "targetIncarnation": 2,
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
        """)!.AsObject();

        var effects = GuardianProjectState.ResolveDerivedEffects(trackerRoot, "guardian_alpha");

        Assert.Equal(0, effects.QuestHookCount);
        Assert.Equal(1, effects.GuaranteedArchiveQuestCount);
    }

    [Fact]
    public void TryConsumeLoreQuestToken_ArchiveConsultationHook_ConsumesGuaranteedQuestOnce()
    {
        var trackerRoot = JsonNode.Parse("""
        {
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "archive_consult_project",
                "projectType": "lore_research",
                "projectOrigin": "archive_consultation",
                "projectTier": "minor",
                "finalState": "Completed",
                "effectState": {
                  "targetIncarnation": 2,
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
        """)!.AsObject();

        Assert.True(GuardianProjectState.TryConsumeLoreQuestToken(
            trackerRoot,
            "guardian_alpha",
            "archive_consult_project",
            GuardianProjectState.ArchiveConsultationHookOrigin,
            2));

        var effectState = trackerRoot["completedProjects"]![0]!["project"]!["effectState"]!.AsObject();
        Assert.Equal(1, effectState["guaranteedArchiveQuestSpawned"]!.GetValue<int>());
        Assert.Equal(1, effectState["guaranteedArchiveQuestConsumed"]!.GetValue<int>());

        Assert.False(GuardianProjectState.TryConsumeLoreQuestToken(
            trackerRoot,
            "guardian_alpha",
            "archive_consult_project",
            GuardianProjectState.ArchiveConsultationHookOrigin,
            2));
    }

    [Fact]
    public void ResolveOffensiveImpact_UsesTargetShieldAndPoliticalBonuses()
    {
        var trackerRoot = JsonNode.Parse("""
        {
          "completedProjects": [
            {
              "guardianId": "guardian_target",
              "project": {
                "projectId": "fort_major",
                "projectType": "abode_fortification",
                "projectTier": "major",
                "projectMode": "internal",
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
              "guardianId": "guardian_target",
              "project": {
                "projectId": "counter_minor",
                "projectType": "counter_rival_operation",
                "projectTier": "minor",
                "projectMode": "supportive",
                "targetGuardianId": "guardian_attacker",
                "finalState": "Completed",
                "completionTurn": 12,
                "projectOutcomeAudit": {
                  "pressureRelief": 15,
                  "stabilityRelief": 5,
                  "abodePowerGain": 1
                }
              }
            }
          ]
        }
        """)!.AsObject();

        var impact = GuardianProjectState.ResolveOffensiveImpact(
            trackerRoot,
            "guardian_attacker",
            "guardian_target",
            "major",
            attackerCurrentPower: 70,
            targetCurrentPower: 60,
            playerDefenseBonus: 1);

        Assert.Equal(5, impact.BaseLoss);
        Assert.Equal(2, impact.AttackerBonus);
        Assert.Equal(2, impact.BaseTargetShield);
        Assert.Equal(2, impact.FortificationBonus);
        Assert.Equal(1, impact.CounterOperationBonus);
        Assert.Equal(1, impact.PlayerDefenseBonus);
        Assert.Equal(6, impact.TargetShield);
        Assert.Equal(1, impact.TargetLoss);
        Assert.Equal(0, impact.PressureDelta);
        Assert.Equal(4, impact.StabilityDamage);
    }

    [Fact]
    public void ResolveGuardianDerivedState_CombinesBaseAndProjectBonusesIntoSingleSnapshot()
    {
        var guardian = JsonNode.Parse("""
        {
          "guardianId": "guardian_alpha",
          "abodePower": {
            "currentPower": 35
          }
        }
        """)!.AsObject();

        var trackerRoot = JsonNode.Parse("""
        {
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "forge_grand",
                "projectType": "relic_forging",
                "projectTier": "grand",
                "finalState": "Completed",
                "projectOutcomeAudit": {
                  "upgradedTradeSlots": 1,
                  "elevatedTradeSlots": 1,
                  "guardianRarityCeilingBonusSteps": 1
                }
              }
            },
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "research_major",
                "projectType": "lore_research",
                "projectTier": "major",
                "finalState": "Completed",
                "projectOutcomeAudit": {
                  "bonusLoreUnlocks": 1,
                  "questHookCount": 1,
                  "specialQuestLineUnlocks": 0,
                  "visibleRivalClueBonus": 2
                }
              }
            },
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "prep_minor",
                "projectType": "soul_preparation",
                "projectTier": "minor",
                "finalState": "Completed",
                "projectOutcomeAudit": {
                  "preparationBudgetPoints": 1,
                  "preparationClaimPriorityBonus": 1
                }
              }
            }
          ],
          "temporaryProjectModifiers": [
            {
              "guardianId": "guardian_alpha",
              "modifierType": "next_internal_project_starting_pressure",
              "remainingApplications": 1
            }
          ]
        }
        """)!.AsObject();

        var derived = GuardianProjectState.ResolveGuardianDerivedState(guardian, trackerRoot);

        Assert.Equal(35, derived.CurrentPower);
        Assert.Equal("Хрупкая", derived.TierLabel);
        Assert.Equal(5, derived.TradeSlotCount);
        Assert.Equal(3, derived.GuardianQuestCap);
        Assert.Equal(0, derived.BonusGachaCharges);
        Assert.Equal(0, derived.BaseGuardianRarityCeilingBonusSteps);
        Assert.Equal(1, derived.EffectiveGuardianRarityCeilingBonusSteps);
        Assert.Equal(1, derived.BaseNextLifeCorrectionBudgetPoints);
        Assert.Equal(2, derived.EffectiveNextLifeCorrectionBudgetPoints);
        Assert.Equal(1, derived.BaseRivalArcDefenseClues);
        Assert.Equal(3, derived.EffectiveRivalArcDefenseClues);
        Assert.False(derived.RivalArcCounterQuestAccess);
        Assert.Equal(2, derived.EffectiveUpgradedTradeSlots);
        Assert.Equal(1, derived.EffectiveElevatedTradeSlots);
        Assert.Equal(1, derived.ActiveTemporaryModifierCount);
        Assert.Equal("2|1|1", GuardianProjectState.BuildTradeBonusSignature(derived));
    }
}
