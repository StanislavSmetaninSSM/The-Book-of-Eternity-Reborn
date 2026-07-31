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
                  "questHookTokensSpent": 1,
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

        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": []
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
                  "questHookTokensSpent": 1,
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
                  "questHookTokensSpent": 1,
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
    public async Task NormalizeAccumulatedStateAsync_LoreResearchMixedBonusClue_TruncatedBeforeClueMarkersStillFailsClosed()
    {
        await SeedVisibleBonusClueRivalScenarioAsync();
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
              "eventId": "evt_hunter_1",
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer));

        Assert.Contains("world_events.json", ex.Message, StringComparison.OrdinalIgnoreCase);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var arcsJson = await _fs.ReadFileAsync(RivalSoulArcService.StatePath);
        var worldEventsJson = await _fs.ReadFileAsync("game_state/world/world_events.json");

        Assert.NotNull(trackerJson);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 0", trackerJson, StringComparison.Ordinal);
        Assert.NotNull(arcsJson);
        Assert.DoesNotContain("\"bonusClueConsumed\": true", arcsJson, StringComparison.Ordinal);
        Assert.NotNull(worldEventsJson);
        Assert.Contains("\"eventId\": \"evt_hunter_1\"", worldEventsJson, StringComparison.Ordinal);
        Assert.DoesNotContain("relatedRivalArcId", worldEventsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_LoreResearchMixedBonusClue_BareCurrentWorldEventContainerStillFailsClosed()
    {
        await SeedVisibleBonusClueRivalScenarioAsync();
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer));

        Assert.Contains("world_events.json", ex.Message, StringComparison.OrdinalIgnoreCase);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var arcsJson = await _fs.ReadFileAsync(RivalSoulArcService.StatePath);
        var worldEventsJson = await _fs.ReadFileAsync("game_state/world/world_events.json");

        Assert.NotNull(trackerJson);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 0", trackerJson, StringComparison.Ordinal);
        Assert.NotNull(arcsJson);
        Assert.DoesNotContain("\"bonusClueConsumed\": true", arcsJson, StringComparison.Ordinal);
        Assert.Equal("""
        {
          "worldEventsLog": [
            {
        """, worldEventsJson);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_LoreResearchMixedBonusClue_PartialRelevantCurrentWorldEventKeyStillFailsClosed()
    {
        await SeedVisibleBonusClueRivalScenarioAsync();
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
              "related
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer));

        Assert.Contains("world_events.json", ex.Message, StringComparison.OrdinalIgnoreCase);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var arcsJson = await _fs.ReadFileAsync(RivalSoulArcService.StatePath);
        var worldEventsJson = await _fs.ReadFileAsync("game_state/world/world_events.json");

        Assert.NotNull(trackerJson);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 0", trackerJson, StringComparison.Ordinal);
        Assert.NotNull(arcsJson);
        Assert.DoesNotContain("\"bonusClueConsumed\": true", arcsJson, StringComparison.Ordinal);
        Assert.Equal("""
        {
          "worldEventsLog": [
            {
              "related
        """, worldEventsJson);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_LoreResearchMixedBonusClue_ExhaustedPublicSignalBudgetMalformedWorldEventsStillFailsClosed()
    {
        await SeedVisibleBonusClueRivalScenarioAsync(visibleRivalClueBudget: 1);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
              "eventId": "evt_hunter_1",
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer));

        Assert.Contains("world_events.json", ex.Message, StringComparison.OrdinalIgnoreCase);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var arcsJson = await _fs.ReadFileAsync(RivalSoulArcService.StatePath);
        var worldEventsJson = await _fs.ReadFileAsync("game_state/world/world_events.json");

        Assert.NotNull(trackerJson);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 0", trackerJson, StringComparison.Ordinal);
        Assert.NotNull(arcsJson);
        Assert.DoesNotContain("\"bonusClueConsumed\": true", arcsJson, StringComparison.Ordinal);
        Assert.NotNull(worldEventsJson);
        Assert.Contains("\"eventId\": \"evt_hunter_1\"", worldEventsJson, StringComparison.Ordinal);
        Assert.DoesNotContain("relatedRivalArcId", worldEventsJson, StringComparison.Ordinal);
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
                  "questHookTokensSpent": 1,
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
                  "questHookTokensSpent": 1,
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

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_LoreResearchVisibleBonusClue_MissingCurrentSoulStateFailsClosed()
    {
        await SeedVisibleBonusClueRivalScenarioAsync();

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer));

        Assert.Contains("soul_state.json", ex.Message, StringComparison.OrdinalIgnoreCase);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var arcsJson = await _fs.ReadFileAsync(RivalSoulArcService.StatePath);

        Assert.NotNull(trackerJson);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 0", trackerJson, StringComparison.Ordinal);
        Assert.NotNull(arcsJson);
        Assert.DoesNotContain("\"bonusClueConsumed\": true", arcsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_LoreResearchVisibleBonusClue_MissingCurrentWorldEventsFailsClosed()
    {
        await SeedVisibleBonusClueRivalScenarioAsync();
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World"
        }
        """);
        _fs.DeleteFile("game_state/world/world_events.json");

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer));

        Assert.Contains("world_events.json", ex.Message, StringComparison.OrdinalIgnoreCase);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var arcsJson = await _fs.ReadFileAsync(RivalSoulArcService.StatePath);

        Assert.NotNull(trackerJson);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 0", trackerJson, StringComparison.Ordinal);
        Assert.NotNull(arcsJson);
        Assert.DoesNotContain("\"bonusClueConsumed\": true", arcsJson, StringComparison.Ordinal);
        Assert.False(_fs.FileExists("game_state/world/world_events.json"));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_LoreResearchVisibleBonusClue_MalformedCurrentRivalSoulArcsFailsClosed()
    {
        await SeedVisibleBonusClueRivalScenarioAsync();
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World"
        }
        """);
        await _fs.WriteFileAtomicAsync(RivalSoulArcService.StatePath, "{");

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer));

        Assert.Contains("rival_soul_arcs.json", ex.Message, StringComparison.OrdinalIgnoreCase);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var arcsJson = await _fs.ReadFileAsync(RivalSoulArcService.StatePath);

        Assert.NotNull(trackerJson);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 0", trackerJson, StringComparison.Ordinal);
        Assert.Equal("{", arcsJson);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_LoreResearchVisibleBonusClue_MalformedCurrentSoulStateFailsClosed()
    {
        await SeedVisibleBonusClueRivalScenarioAsync();
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{");

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer));

        Assert.Contains("soul_state.json", ex.Message, StringComparison.OrdinalIgnoreCase);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var arcsJson = await _fs.ReadFileAsync(RivalSoulArcService.StatePath);

        Assert.NotNull(trackerJson);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 0", trackerJson, StringComparison.Ordinal);
        Assert.NotNull(arcsJson);
        Assert.DoesNotContain("\"bonusClueConsumed\": true", arcsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_LoreResearchVisibleBonusClue_MixedValidAndUnsupportedCurrentSoulStateFailsClosed()
    {
        await SeedVisibleBonusClueRivalScenarioAsync();
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World",
          "soulRelics": {
            "equipped": [],
            "stored": []
          },
          "foo": []
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer));

        Assert.Contains("soul_state.json", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("foo", ex.Message, StringComparison.OrdinalIgnoreCase);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var arcsJson = await _fs.ReadFileAsync(RivalSoulArcService.StatePath);

        Assert.NotNull(trackerJson);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 0", trackerJson, StringComparison.Ordinal);
        Assert.NotNull(arcsJson);
        Assert.DoesNotContain("\"bonusClueConsumed\": true", arcsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_SponsoredWorldEventsWithoutBonusCluePath_MalformedCurrentSoulStateRemainsPermissive()
    {
        await SeedVisibleBonusClueWorldEventScenarioAsync();
        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{");
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
              "eventId": "evt_hunter_1",
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var arcsJson = await _fs.ReadFileAsync(RivalSoulArcService.StatePath);
        var worldEventsJson = await _fs.ReadFileAsync("game_state/world/world_events.json");

        Assert.NotNull(arcsJson);
        Assert.DoesNotContain("\"bonusClueConsumed\": true", arcsJson, StringComparison.Ordinal);
        Assert.NotNull(worldEventsJson);
        Assert.Contains("\"eventId\": \"evt_hunter_1\"", worldEventsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_DormantLoreResearchBudgetWithoutCurrentBonusClueSurface_MalformedCurrentSoulStateRemainsPermissive()
    {
        await SeedVisibleBonusClueWorldEventScenarioAsync();
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{");
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": []
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var worldEventsJson = await _fs.ReadFileAsync("game_state/world/world_events.json");

        Assert.NotNull(trackerJson);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 0", trackerJson, StringComparison.Ordinal);
        Assert.NotNull(worldEventsJson);
        Assert.Contains("\"worldEventsLog\": []", worldEventsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_DormantLoreResearchBudgetWithoutCurrentBonusClueSurface_MixedValidAndUnsupportedCurrentSoulStateRemainsPermissive()
    {
        await SeedVisibleBonusClueWorldEventScenarioAsync();
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "soulRelics": {
            "equipped": [],
            "stored": []
          },
          "foo": []
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": []
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var worldEventsJson = await _fs.ReadFileAsync("game_state/world/world_events.json");

        Assert.NotNull(trackerJson);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 0", trackerJson, StringComparison.Ordinal);
        Assert.NotNull(worldEventsJson);
        Assert.Contains("\"worldEventsLog\": []", worldEventsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_DormantLoreResearchBudgetWithoutCurrentBonusClueSurface_MissingCurrentSoulStateRemainsPermissive()
    {
        await SeedVisibleBonusClueWorldEventScenarioAsync();
        _fs.DeleteFile("game_state/meta/soul_state.json");
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": []
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var worldEventsJson = await _fs.ReadFileAsync("game_state/world/world_events.json");

        Assert.NotNull(trackerJson);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 0", trackerJson, StringComparison.Ordinal);
        Assert.NotNull(worldEventsJson);
        Assert.Contains("\"worldEventsLog\": []", worldEventsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_LoreResearchWorldEventBonusClue_MalformedCurrentWorldEventsFailsClosed()
    {
        await SeedVisibleBonusClueWorldEventScenarioAsync();
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
              "eventId": "evt_hunter_1",
              "relatedRivalArcId": "arc_hunter",
              "bonusClueSourceProjectId": "research_major"
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer));

        Assert.Contains("world_events.json", ex.Message, StringComparison.OrdinalIgnoreCase);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var worldEventsJson = await _fs.ReadFileAsync("game_state/world/world_events.json");

        Assert.NotNull(trackerJson);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 0", trackerJson, StringComparison.Ordinal);
        Assert.Contains("\"relatedRivalArcId\": \"arc_hunter\"", worldEventsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_LoreResearchWorldEventBonusClue_MissingCurrentWorldEventsFailsClosed()
    {
        await SeedVisibleBonusClueWorldEventScenarioAsync();
        _fs.DeleteFile("game_state/world/world_events.json");

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer));

        Assert.Contains("world_events.json", ex.Message, StringComparison.OrdinalIgnoreCase);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);

        Assert.NotNull(trackerJson);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 0", trackerJson, StringComparison.Ordinal);
        Assert.False(_fs.FileExists("game_state/world/world_events.json"));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_LoreResearchWorldEventBonusClue_TruncatedRelevantCurrentWorldEventsStillFailClosed()
    {
        await SeedVisibleBonusClueWorldEventScenarioAsync();
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
              "eventId": "evt_hunter_1",
              "relatedRivalArcId": "arc_hunter"
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer));

        Assert.Contains("world_events.json", ex.Message, StringComparison.OrdinalIgnoreCase);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var worldEventsJson = await _fs.ReadFileAsync("game_state/world/world_events.json");

        Assert.NotNull(trackerJson);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 0", trackerJson, StringComparison.Ordinal);
        Assert.Contains("\"relatedRivalArcId\": \"arc_hunter\"", worldEventsJson, StringComparison.Ordinal);
        Assert.DoesNotContain("bonusClueSourceProjectId", worldEventsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_LoreResearchWorldEventBonusClue_TruncatedBeforeClueMarkersStillFailClosed()
    {
        await SeedVisibleBonusClueWorldEventScenarioAsync();
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
              "eventId": "evt_hunter_1",
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer));

        Assert.Contains("world_events.json", ex.Message, StringComparison.OrdinalIgnoreCase);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var worldEventsJson = await _fs.ReadFileAsync("game_state/world/world_events.json");

        Assert.NotNull(trackerJson);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 0", trackerJson, StringComparison.Ordinal);
        Assert.Contains("\"eventId\": \"evt_hunter_1\"", worldEventsJson, StringComparison.Ordinal);
        Assert.DoesNotContain("relatedRivalArcId", worldEventsJson, StringComparison.Ordinal);
        Assert.DoesNotContain("bonusClueSourceProjectId", worldEventsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_LoreResearchWorldEventBonusClue_BareCurrentWorldEventContainerStillFailClosed()
    {
        await SeedVisibleBonusClueWorldEventScenarioAsync();
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer));

        Assert.Contains("world_events.json", ex.Message, StringComparison.OrdinalIgnoreCase);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var worldEventsJson = await _fs.ReadFileAsync("game_state/world/world_events.json");

        Assert.NotNull(trackerJson);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 0", trackerJson, StringComparison.Ordinal);
        Assert.Equal("""
        {
          "worldEventsLog": [
            {
        """, worldEventsJson);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_LoreResearchWorldEventBonusClue_PartialRelevantCurrentWorldEventKeyStillFailClosed()
    {
        await SeedVisibleBonusClueWorldEventScenarioAsync();
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
              "ev
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer));

        Assert.Contains("world_events.json", ex.Message, StringComparison.OrdinalIgnoreCase);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var worldEventsJson = await _fs.ReadFileAsync("game_state/world/world_events.json");

        Assert.NotNull(trackerJson);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 0", trackerJson, StringComparison.Ordinal);
        Assert.Equal("""
        {
          "worldEventsLog": [
            {
              "ev
        """, worldEventsJson);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_LoreResearchVisibleBonusClue_MalformedIrrelevantCurrentWorldEventsRemainPermissive()
    {
        await SeedVisibleBonusClueRivalScenarioAsync();
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", "{");

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var arcsJson = await _fs.ReadFileAsync(RivalSoulArcService.StatePath);
        var worldEventsJson = await _fs.ReadFileAsync("game_state/world/world_events.json");

        Assert.NotNull(trackerJson);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 1", trackerJson, StringComparison.Ordinal);
        Assert.NotNull(arcsJson);
        Assert.Contains("\"bonusClueConsumed\": true", arcsJson, StringComparison.Ordinal);
        Assert.Equal("{", worldEventsJson);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_LoreResearchVisibleBonusClue_NonTrivialMalformedIrrelevantCurrentWorldEventsRemainPermissive()
    {
        await SeedVisibleBonusClueRivalScenarioAsync();
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", "{\"foo\":");

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var arcsJson = await _fs.ReadFileAsync(RivalSoulArcService.StatePath);
        var worldEventsJson = await _fs.ReadFileAsync("game_state/world/world_events.json");

        Assert.NotNull(trackerJson);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 1", trackerJson, StringComparison.Ordinal);
        Assert.NotNull(arcsJson);
        Assert.Contains("\"bonusClueConsumed\": true", arcsJson, StringComparison.Ordinal);
        Assert.Equal("{\"foo\":", worldEventsJson);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_LoreResearchVisibleBonusClue_SchemaShapedMalformedIrrelevantCurrentWorldEventsRemainPermissive()
    {
        await SeedVisibleBonusClueRivalScenarioAsync();
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {"worldEventsLog":[{"foo":
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var arcsJson = await _fs.ReadFileAsync(RivalSoulArcService.StatePath);
        var worldEventsJson = await _fs.ReadFileAsync("game_state/world/world_events.json");

        Assert.NotNull(trackerJson);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 1", trackerJson, StringComparison.Ordinal);
        Assert.NotNull(arcsJson);
        Assert.Contains("\"bonusClueConsumed\": true", arcsJson, StringComparison.Ordinal);
        Assert.Equal("{\"worldEventsLog\":[{\"foo\":", worldEventsJson);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_LoreResearchVisibleBonusClue_BackupLinkedWorldEventsDoNotRequireMalformedIrrelevantCurrentWorldEvents()
    {
        await SeedVisibleBonusClueRivalScenarioAsync();
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", "{");
        await _fs.WriteFileAtomicAsync("test_backups/rival_visible_bonus_clue_world_events_prev.json", """
        {
          "worldEventsLog": [
            {
              "eventId": "evt_old_hunter_bonus",
              "relatedRivalArcId": "arc_hunter",
              "visibility": "player_known",
              "bonusClueSourceProjectId": "research_major",
              "bonusClueRevealId": "reveal_hunter_evt_old",
              "bonusClueCost": 1
            }
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(
            normalizer,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["game_state/world/world_events.json"] = "test_backups/rival_visible_bonus_clue_world_events_prev.json"
            });

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var arcsJson = await _fs.ReadFileAsync(RivalSoulArcService.StatePath);
        var worldEventsJson = await _fs.ReadFileAsync("game_state/world/world_events.json");

        Assert.NotNull(trackerJson);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 1", trackerJson, StringComparison.Ordinal);
        Assert.NotNull(arcsJson);
        Assert.Contains("\"bonusClueConsumed\": true", arcsJson, StringComparison.Ordinal);
        Assert.Equal("{", worldEventsJson);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_LoreResearchVisibleBonusClue_UsesBackupDerivedCurrentIncarnation()
    {
        await SeedVisibleBonusClueRivalScenarioAsync();
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Mortal World"
        }
        """);
        await _fs.WriteFileAtomicAsync("test_backups/rival_visible_bonus_clue_soul_prev.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World"
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(
            normalizer,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["game_state/meta/soul_state.json"] = "test_backups/rival_visible_bonus_clue_soul_prev.json"
            });

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var arcsJson = await _fs.ReadFileAsync(RivalSoulArcService.StatePath);

        Assert.NotNull(trackerJson);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 1", trackerJson, StringComparison.Ordinal);
        Assert.NotNull(arcsJson);
        Assert.Contains("\"bonusClueConsumed\": true", arcsJson, StringComparison.Ordinal);
    }

    private async Task SeedVisibleBonusClueRivalScenarioAsync(int visibleRivalClueBudget = 2)
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 21 }
        """);

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, $$"""
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
                  "visibleRivalClueBonus": {{visibleRivalClueBudget}},
                  "unlockedLoreFragments": []
                },
                "effectState": {
                  "targetIncarnation": 3,
                  "bonusLoreUnlocksApplied": 1,
                  "questHookTokensGranted": 1,
                  "questHookTokensSpent": 1,
                  "specialQuestLineTokensGranted": 0,
                  "specialQuestLineTokensSpent": 0,
                  "visibleRivalClueBudgetGranted": {{visibleRivalClueBudget}},
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

        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": []
        }
        """);
    }

    private async Task SeedVisibleBonusClueWorldEventScenarioAsync()
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
                  "questHookTokensSpent": 1,
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
          "currentIncarnation": 3,
          "currentRealm": "Mortal World"
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
    }

}
