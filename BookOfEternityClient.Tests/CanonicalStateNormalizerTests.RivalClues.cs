using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class CanonicalStateNormalizerTests : IDisposable
{
    [Fact]
    public async Task NormalizeAccumulatedStateAsync_NewVisibleBonusClue_ConsumesLoreResearchClueBudget()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 21 }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World"
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
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync(RivalSoulArcService.StatePath, """
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
                  "bonusClueRevealId": "reveal_hunter_sig_new",
                  "bonusClueCost": 1
                }
              ],
              "resolution": { "outcome": "ongoing", "notes": "" }
            }
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var journalJson = await _fs.ReadFileAsync(GuardianProjectState.JournalPath);

        Assert.NotNull(trackerJson);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 1", trackerJson, StringComparison.Ordinal);
        Assert.NotNull(journalJson);
        Assert.Contains("bonus clue для rival-нити", journalJson, StringComparison.Ordinal);
    }

    [Fact]

    public async Task NormalizeAccumulatedStateAsync_LoreResearchWorldEventBonusClue_ConsumesBudgetOnce()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 44 }
        """);

        const string preTurnTrackerJson = """
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "research_major",
                "projectType": "lore_research",
                "projectTier": "major",
                "finalState": "Completed",
                "completionTurn": 40,
                "projectOutcomeAudit": {
                  "visibleRivalClueBonus": 2
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
          "temporaryProjectModifiers": []
        }
        """;
        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, preTurnTrackerJson);
        await _fs.WriteFileAtomicAsync(
            "game_state/control/pending_turn_snapshot/game_state/meta/guardian_projects_prev.json",
            preTurnTrackerJson);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3
        }
        """);

        await _fs.WriteFileAtomicAsync(RivalSoulArcService.StatePath, """
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
              "eventTitle": "Охотник замечен у заставы",
              "summary": "По городу пошли слухи о новом преследователе.",
              "relatedRivalArcId": "arc_hunter",
              "visibility": "player_known",
              "bonusClueSourceProjectId": "research_major",
              "bonusClueRevealId": "reveal_evt_hunter_1",
              "bonusClueCost": 1
            }
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var worldEventsJson = await _fs.ReadFileAsync("game_state/world/world_events.json");

        Assert.NotNull(trackerJson);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 1", trackerJson, StringComparison.Ordinal);
        Assert.NotNull(worldEventsJson);
        Assert.Contains("\"bonusClueConsumed\": true", worldEventsJson, StringComparison.Ordinal);
        Assert.Contains("\"bonusClueConsumedProjectId\": \"research_major\"", worldEventsJson, StringComparison.Ordinal);
    }

    [Fact]

    public async Task NormalizeAccumulatedStateAsync_MirroredSignalAndWorldEventBonusClue_DoNotDoubleSpend()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 44 }
        """);

        const string preTurnTrackerJson = """
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "research_major",
                "projectType": "lore_research",
                "projectTier": "major",
                "finalState": "Completed",
                "completionTurn": 40,
                "projectOutcomeAudit": {
                  "visibleRivalClueBonus": 2
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
          "temporaryProjectModifiers": []
        }
        """;
        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, preTurnTrackerJson);
        await _fs.WriteFileAtomicAsync(
            "game_state/control/pending_turn_snapshot/game_state/meta/guardian_projects_prev.json",
            preTurnTrackerJson);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3
        }
        """);

        await _fs.WriteFileAtomicAsync(RivalSoulArcService.StatePath, """
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
                  "bonusClueRevealId": "shared_hunter_clue",
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
          "worldEventsLog": [
            {
              "eventId": "evt_hunter_1",
              "eventTitle": "Охотник замечен у заставы",
              "summary": "По городу пошли слухи о новом преследователе.",
              "relatedRivalArcId": "arc_hunter",
              "visibility": "player_known",
              "bonusClueSourceProjectId": "research_major",
              "bonusClueRevealId": "shared_hunter_clue",
              "bonusClueCost": 1
            }
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var worldEventsJson = await _fs.ReadFileAsync("game_state/world/world_events.json");
        var arcsJson = await _fs.ReadFileAsync(RivalSoulArcService.StatePath);

        Assert.NotNull(trackerJson);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 1", trackerJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"visibleRivalClueBudgetSpent\": 2", trackerJson, StringComparison.Ordinal);
        Assert.NotNull(worldEventsJson);
        Assert.Contains("\"bonusClueConsumed\": true", worldEventsJson, StringComparison.Ordinal);
        Assert.NotNull(arcsJson);
        Assert.Contains("\"bonusClueConsumed\": true", arcsJson, StringComparison.Ordinal);
    }

    [Fact]

    public async Task NormalizeAccumulatedStateAsync_HiddenWorldEventBonusClue_DoesNotSpendUntilPlayerKnown()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 44 }
        """);

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "research_major",
                "projectType": "lore_research",
                "projectTier": "major",
                "finalState": "Completed",
                "completionTurn": 40,
                "projectOutcomeAudit": {
                  "visibleRivalClueBonus": 1
                },
                "effectState": {
                  "targetIncarnation": 3,
                  "bonusLoreUnlocksApplied": 1,
                  "questHookTokensGranted": 1,
                  "questHookTokensSpent": 0,
                  "specialQuestLineTokensGranted": 0,
                  "specialQuestLineTokensSpent": 0,
                  "visibleRivalClueBudgetGranted": 1,
                  "visibleRivalClueBudgetSpent": 0
                }
              }
            }
          ],
          "temporaryProjectModifiers": []
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3
        }
        """);

        await _fs.WriteFileAtomicAsync(RivalSoulArcService.StatePath, """
        {
          "arcs": [
            {
              "arcId": "arc_hunter_hidden",
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
                "rivalSoulId": "rival_hidden",
                "displayNameOrMoniker": "Скрытый Охотник",
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
              "eventId": "evt_hunter_hidden",
              "eventTitle": "Тайный приказ охотника",
              "summary": "Событие существует, но игрок о нём ещё не знает.",
              "relatedRivalArcId": "arc_hunter_hidden",
              "visibility": "Secret",
              "bonusClueSourceProjectId": "research_major",
              "bonusClueRevealId": "reveal_hidden_evt",
              "bonusClueCost": 1
            }
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var worldEventsJson = await _fs.ReadFileAsync("game_state/world/world_events.json");

        Assert.NotNull(trackerJson);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 0", trackerJson, StringComparison.Ordinal);
        Assert.NotNull(worldEventsJson);
        Assert.DoesNotContain("\"bonusClueConsumed\": true", worldEventsJson, StringComparison.Ordinal);
    }

    [Fact]

    public async Task NormalizeAccumulatedStateAsync_PreviouslyHiddenWorldEventBonusClue_CanSpendAfterBecomingPlayerKnown()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 44 }
        """);

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "research_major",
                "projectType": "lore_research",
                "projectTier": "major",
                "finalState": "Completed",
                "completionTurn": 40,
                "projectOutcomeAudit": {
                  "visibleRivalClueBonus": 1
                },
                "effectState": {
                  "targetIncarnation": 3,
                  "bonusLoreUnlocksApplied": 1,
                  "questHookTokensGranted": 1,
                  "questHookTokensSpent": 0,
                  "specialQuestLineTokensGranted": 0,
                  "specialQuestLineTokensSpent": 0,
                  "visibleRivalClueBudgetGranted": 1,
                  "visibleRivalClueBudgetSpent": 0
                }
              }
            }
          ],
          "temporaryProjectModifiers": []
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3
        }
        """);

        await _fs.WriteFileAtomicAsync(RivalSoulArcService.StatePath, """
        {
          "arcs": [
            {
              "arcId": "arc_hunter_transition",
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
                "rivalSoulId": "rival_transition",
                "displayNameOrMoniker": "Охотник из тени",
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
              "eventId": "evt_hunter_transition",
              "eventTitle": "Тайный приказ охотника",
              "summary": "Игрок только сейчас добыл это знание.",
              "relatedRivalArcId": "arc_hunter_transition",
              "visibility": "player_known",
              "bonusClueSourceProjectId": "research_major",
              "bonusClueRevealId": "reveal_transition_evt",
              "bonusClueCost": 1
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot/game_state/world/world_events_prev.json", """
        {
          "worldEventsLog": [
            {
              "eventId": "evt_hunter_transition",
              "eventTitle": "Тайный приказ охотника",
              "summary": "Раньше это знание было скрытым.",
              "relatedRivalArcId": "arc_hunter_transition",
              "visibility": "Secret",
              "bonusClueSourceProjectId": "research_major",
              "bonusClueRevealId": "reveal_transition_evt",
              "bonusClueCost": 1
            }
          ]
        }
        """);

        var backups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["game_state/world/world_events.json"] = "game_state/control/pending_turn_snapshot/game_state/world/world_events_prev.json"
        };

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer, backups);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var worldEventsJson = await _fs.ReadFileAsync("game_state/world/world_events.json");

        Assert.NotNull(trackerJson);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 1", trackerJson, StringComparison.Ordinal);
        Assert.NotNull(worldEventsJson);
        Assert.Contains("\"bonusClueConsumed\": true", worldEventsJson, StringComparison.Ordinal);
        Assert.Contains("\"bonusClueConsumedProjectId\": \"research_major\"", worldEventsJson, StringComparison.Ordinal);
    }

}
