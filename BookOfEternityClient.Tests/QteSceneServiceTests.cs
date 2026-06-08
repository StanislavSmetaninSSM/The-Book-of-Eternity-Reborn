using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class QteSceneServiceTests : IDisposable
{
    private const string QteNormalizerBackupDirectory = "game_state/control/qte_normalizer_backups";
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly QteSceneService _service;

    public QteSceneServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-qte-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _service = new QteSceneService(
            _fs,
            new GameSettings(),
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            NullLogger<QteSceneService>.Instance);
    }

    [Theory]
    [InlineData('q', "q")]
    [InlineData('Q', "q")]
    [InlineData('й', "q")]
    [InlineData('Й', "q")]
    [InlineData('ц', "w")]
    [InlineData('Ц', "w")]
    [InlineData('у', "e")]
    [InlineData('У', "e")]
    [InlineData('ф', "a")]
    [InlineData('Ф', "a")]
    [InlineData('ы', "s")]
    [InlineData('Ы', "s")]
    [InlineData('в', "d")]
    [InlineData('В', "d")]
    public void QteKeyInput_NormalizesConsoleFallbackCharacters(char input, string expectedToken)
    {
        Assert.Equal(expectedToken, QteKeyInput.NormalizeCharacter(input));
        Assert.Equal(expectedToken, QteKeyInput.NormalizeConsoleInput(new ConsoleKeyInfo(input, 0, false, false, false)));
    }

    [Theory]
    [InlineData(ConsoleKey.Q, "Q / Й")]
    [InlineData(ConsoleKey.W, "W / Ц")]
    [InlineData(ConsoleKey.E, "E / У")]
    [InlineData(ConsoleKey.A, "A / Ф")]
    [InlineData(ConsoleKey.S, "S / Ы")]
    [InlineData(ConsoleKey.D, "D / В")]
    [InlineData(ConsoleKey.Spacebar, "Space")]
    public void QteKeyInput_FormatsPhysicalKeyLabelsWithRuFallback(ConsoleKey key, string expectedLabel)
    {
        Assert.Equal(expectedLabel, QteKeyInput.FormatPromptLabel(key));
    }

    [Theory]
    [InlineData('й', ConsoleKey.Q)]
    [InlineData('ц', ConsoleKey.W)]
    [InlineData('у', ConsoleKey.E)]
    [InlineData('ф', ConsoleKey.A)]
    [InlineData('ы', ConsoleKey.S)]
    [InlineData('в', ConsoleKey.D)]
    [InlineData(' ', ConsoleKey.Spacebar)]
    public void QteKeyInput_MatchesConsoleFallbackInputToExpectedPhysicalKey(char input, ConsoleKey expectedKey)
    {
        var keyInfo = new ConsoleKeyInfo(input, 0, false, false, false);

        Assert.True(QteKeyInput.MatchesConsoleKey(keyInfo, expectedKey));
    }

    [Fact]
    public void QteKeyInput_LeavesUnsupportedCharactersUnmatched()
    {
        Assert.Null(QteKeyInput.NormalizeCharacter('ж'));
        Assert.False(QteKeyInput.MatchesConsoleKey(new ConsoleKeyInfo('ж', 0, false, false, false), ConsoleKey.Q));
    }

    [Fact]
    public void MashInputGrade_ResolvesSuccessPartialAndFailFromMatchingPressCounts()
    {
        Assert.Equal(
            "success",
            ResolveMashInputGrade(["space"], successTarget: 5, partialTarget: 3, RepeatKey(ConsoleKey.Spacebar, 5)));
        Assert.Equal(
            "partial",
            ResolveMashInputGrade(["space"], successTarget: 5, partialTarget: 3, RepeatKey(ConsoleKey.Spacebar, 3)));
        Assert.Equal(
            "fail",
            ResolveMashInputGrade(["space"], successTarget: 5, partialTarget: 3, RepeatKey(ConsoleKey.Spacebar, 2)));
    }

    [Fact]
    public void MashInputGrade_EscapeCancelsAsFail()
    {
        var inputs = new[]
        {
            new ConsoleKeyInfo(' ', ConsoleKey.Spacebar, false, false, false),
            new ConsoleKeyInfo('\u001b', ConsoleKey.Escape, false, false, false),
            new ConsoleKeyInfo(' ', ConsoleKey.Spacebar, false, false, false),
            new ConsoleKeyInfo(' ', ConsoleKey.Spacebar, false, false, false)
        };

        Assert.Equal(
            "fail",
            ResolveMashInputGrade(["space"], successTarget: 3, partialTarget: 1, inputs));
    }

    [Fact]
    public void MashInputGrade_CountsRuFallbackOnlyForConfiguredQteKeys()
    {
        var inputs = new[]
        {
            new ConsoleKeyInfo('й', 0, false, false, false),
            new ConsoleKeyInfo('ц', 0, false, false, false),
            new ConsoleKeyInfo('q', 0, false, false, false),
            new ConsoleKeyInfo(' ', ConsoleKey.Spacebar, false, false, false)
        };

        Assert.Equal(
            "success",
            ResolveMashInputGrade(["q"], successTarget: 2, partialTarget: 1, inputs));
    }

    [Fact]
    public void MashInputEffectiveTarget_IsMonotonicForStatTierAndDifficulty()
    {
        var lowStatTarget = ComputeMashInputEffectiveTargetPresses(12, baseDifficulty: 3, statTier: -2);
        var highStatTarget = ComputeMashInputEffectiveTargetPresses(12, baseDifficulty: 3, statTier: 3);
        var easyDifficultyTarget = ComputeMashInputEffectiveTargetPresses(12, baseDifficulty: 1, statTier: 0);
        var hardDifficultyTarget = ComputeMashInputEffectiveTargetPresses(12, baseDifficulty: 5, statTier: 0);

        Assert.True(highStatTarget <= lowStatTarget);
        Assert.True(hardDifficultyTarget >= easyDifficultyTarget);
        Assert.Equal(6, ComputeMashInputPartialTargetPresses(successTarget: 12, partialThreshold: 0.5));
    }

    [Fact]
    public async Task EnsureRuntimeStateHealthyAsync_DeletesInvalidJsonRuntimeFile()
    {
        await _fs.WriteFileAtomicAsync(QteSceneService.QteRuntimePath, "{ invalid json");

        await _service.EnsureRuntimeStateHealthyAsync();

        Assert.False(_fs.FileExists(QteSceneService.QteRuntimePath));
    }

    [Fact]
    public async Task EnsureRuntimeStateHealthyAsync_RemovesPendingOfferWithoutActiveScene()
    {
        await _fs.WriteFileAtomicAsync(QteSceneService.QteRuntimePath, """
        {
          "pendingOffer": {
            "qteId": "qte_bridge",
            "title": "Bridge",
            "offerText": "Offer"
          },
          "lastDeclinedQteId": "older_qte"
        }
        """);

        await _service.EnsureRuntimeStateHealthyAsync();

        var json = await _fs.ReadFileAsync(QteSceneService.QteRuntimePath);
        Assert.False(string.IsNullOrWhiteSpace(json));
        Assert.DoesNotContain("pendingOffer", json!, StringComparison.Ordinal);
        Assert.Contains("lastDeclinedQteId", json!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureRuntimeStateHealthyAsync_ClearsBrokenActiveSceneButPreservesReminder()
    {
        await _fs.WriteFileAtomicAsync(QteSceneService.QteRuntimePath, """
        {
          "pendingOffer": {
            "qteId": "qte_bridge",
            "title": "Bridge",
            "offerText": "Offer"
          },
          "activeScene": {
            "offer": null,
            "currentChapterId": 42,
            "acceptedAtTurn": "bad"
          },
          "lastResolvedQteSummaryPendingReminder": "QTE summary"
        }
        """);

        await _service.EnsureRuntimeStateHealthyAsync();

        var json = await _fs.ReadFileAsync(QteSceneService.QteRuntimePath);
        Assert.False(string.IsNullOrWhiteSpace(json));
        Assert.DoesNotContain("activeScene", json!, StringComparison.Ordinal);
        Assert.DoesNotContain("pendingOffer", json!, StringComparison.Ordinal);
        Assert.Contains("lastResolvedQteSummaryPendingReminder", json!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyTerminalOutcomeStateChangesAsync_CapturesBaselineForGuardianProjectNormalization()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 60 }
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
                "appearanceDescription": "Тестовая хранительница."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 75, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_existing",
                "projectType": "abode_expansion",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Существующий проект",
                "activeState": "Planning",
                "totalWork": 10,
                "workDone": 2,
                "totalStages": 2,
                "currentStage": 0,
                "pressure": 1,
                "stability": 98
              }
            }
          ],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """);

        var service = CreateRuntimeCapableService();

        var outcome = new QteSceneService.QteTerminalOutcome
        {
            OutcomeId = "qte_done",
            Title = "QTE complete",
            FinalNarrative = "Исход применён.",
            GmSummary = "Regression summary.",
            ResponseFragment = JsonNode.Parse("""
            {
              "response": "Исход применён.",
              "experienceGained": 5,
              "guardianProjectUpdates": [
                {
                  "guardianId": "guardian_alpha",
                  "projectId": "proj_existing",
                  "workDone": 5,
                  "activeState": "Advancing"
                }
              ]
            }
            """)!.AsObject()
        };

        await service.ApplyTerminalOutcomeStateChangesAsync(outcome);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        Assert.NotNull(trackerJson);

        using var trackerDoc = JsonDocument.Parse(trackerJson!);
        var activeProjects = trackerDoc.RootElement.GetProperty("activeProjects").EnumerateArray().ToList();
        Assert.Single(activeProjects);
        Assert.Equal("proj_existing", activeProjects[0].GetProperty("project").GetProperty("projectId").GetString());
        Assert.Equal(5, activeProjects[0].GetProperty("project").GetProperty("workDone").GetInt32());
        Assert.Equal("Advancing", activeProjects[0].GetProperty("project").GetProperty("activeState").GetString());
        Assert.False(trackerDoc.RootElement.TryGetProperty("guardianProjectUpdates", out _));
    }

    [Fact]
    public async Task ApplyTerminalOutcomeStateChangesAsync_CapturesWorldEventsBaselineForRivalNormalization()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 44 }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World"
        }
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
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
              "eventId": "evt_hunter_repeat",
              "eventTitle": "Тайный приказ охотника",
              "summary": "Игрок уже знал об этом следе.",
              "relatedRivalArcId": "arc_hunter_repeat",
              "visibility": "player_known",
              "bonusClueSourceProjectId": "research_major",
              "bonusClueRevealId": "reveal_repeat_evt",
              "bonusClueCost": 1
            }
          ]
        }
        """);

        var service = CreateRuntimeCapableService();
        var outcome = new QteSceneService.QteTerminalOutcome
        {
            OutcomeId = "qte_repeat_clue",
            Title = "Repeat clue",
            FinalNarrative = "Исход применён.",
            GmSummary = "Regression summary.",
            ResponseFragment = JsonNode.Parse("""
            {
              "response": "Исход применён.",
              "UpdateRivalSoulArcs": [
                {
                  "arcId": "arc_hunter_repeat",
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
              ],
              "worldEventsLog": [
                {
                  "eventId": "evt_hunter_repeat",
                  "eventTitle": "Тайный приказ охотника",
                  "summary": "Игрок уже знал об этом следе.",
                  "relatedRivalArcId": "arc_hunter_repeat",
                  "visibility": "player_known",
                  "bonusClueSourceProjectId": "research_major",
                  "bonusClueRevealId": "reveal_repeat_evt",
                  "bonusClueCost": 1
                }
              ]
            }
            """)!.AsObject()
        };

        await service.ApplyTerminalOutcomeStateChangesAsync(outcome);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        Assert.NotNull(trackerJson);
        Assert.DoesNotContain("\"visibleRivalClueBudgetSpent\": 1", trackerJson, StringComparison.Ordinal);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 0", trackerJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyTerminalOutcomeValidatedStateChangesAsync_RestoresStateAfterValidationFailure()
    {
        await _fs.WriteFileAtomicAsync("game_state/player/experience.json", """
        {
          "totalExperience": 10
        }
        """);

        var service = CreateRuntimeCapableService();
        var outcome = new QteSceneService.QteTerminalOutcome
        {
            OutcomeId = "qte_invalid",
            Title = "Invalid outcome",
            FinalNarrative = "Исход применён.",
            GmSummary = "Regression summary.",
            ResponseFragment = JsonNode.Parse("""
            {
              "response": "Исход применён.",
              "experienceGained": 5,
              "playerCharacterNameChange": "Новая личность"
            }
            """)!.AsObject()
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyTerminalOutcomeValidatedStateChangesAsync(outcome));

        var experienceJson = await _fs.ReadFileAsync("game_state/player/experience.json");
        Assert.NotNull(experienceJson);
        using (var experienceDoc = JsonDocument.Parse(experienceJson!))
            Assert.Equal(10, experienceDoc.RootElement.GetProperty("totalExperience").GetInt32());

        Assert.False(_fs.FileExists("game_state/player/transformation.json"));
        Assert.False(_fs.FileExists("output/narrative_response.json"));

        AssertNoQteBackupArtifacts();
    }

    [Fact]
    public async Task ApplyTerminalOutcomeValidatedStateChangesAsync_RestoresGuardianProjectJournalAfterNormalizationFailure()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 44 }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World"
        }
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
                "appearanceDescription": "Тестовая хранительница."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 75, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
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
                  "bonusLoreUnlocksApplied": 0,
                  "questHookTokensGranted": 0,
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

        const string originalJournal = """
        {
          "entries": [
            {
              "entryId": "existing_entry",
              "guardianId": "guardian_alpha",
              "projectId": "research_major",
              "eventType": "completed",
              "title": "Старое событие",
              "summary": "Журнал до QTE."
            }
          ]
        }
        """;
        await _fs.WriteFileAtomicAsync(GuardianProjectState.JournalPath, originalJournal);

        var service = CreateRuntimeCapableService();
        var outcome = new QteSceneService.QteTerminalOutcome
        {
            OutcomeId = "qte_invalid_journal_restore",
            Title = "Invalid outcome",
            FinalNarrative = "Исход применён.",
            GmSummary = "Regression summary.",
            ResponseFragment = JsonNode.Parse("""
            {
              "response": "Исход применён.",
              "playerCharacterNameChange": "Новая личность",
              "UpdateRivalSoulArcs": [
                {
                  "arcId": "arc_new_clue",
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
                  "publicSignals": [
                    {
                      "signalId": "signal_new_clue",
                      "description": "Новый след охотника.",
                      "visibleToPlayer": true,
                      "bonusClueSourceProjectId": "research_major",
                      "bonusClueCost": 1
                    }
                  ],
                  "currentStage": 1,
                  "resolution": { "outcome": "ongoing", "notes": "" }
                }
              ]
            }
            """)!.AsObject()
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyTerminalOutcomeValidatedStateChangesAsync(outcome));

        var journalJson = await _fs.ReadFileAsync(GuardianProjectState.JournalPath);
        Assert.Equal(originalJournal.Replace("\r\n", "\n"), journalJson?.Replace("\r\n", "\n"));
        AssertNoQteBackupArtifacts();
    }

    [Fact]
    public async Task SaveGameAsync_ExcludesQteNormalizerBackupsFromArchive()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        { "guardians": [] }
        """);
        await _fs.WriteFileAtomicAsync($"{QteNormalizerBackupDirectory}/stale/run_backup.json", """
        { "temporary": true }
        """);

        var saveService = await CreateSaveLoadServiceAsync();
        var saved = await saveService.SaveGameAsync("qte_backups", "Regression", "saves/test", 1);

        Assert.True(saved);

        var saveDir = _fs.ResolvePath("saves/test");
        var savePath = Directory.GetFiles(saveDir, "*.zip", SearchOption.TopDirectoryOnly).Single();
        using var archive = ZipFile.OpenRead(savePath);
        Assert.DoesNotContain(archive.Entries, entry =>
            entry.FullName.StartsWith(QteNormalizerBackupDirectory + "/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ApplyTerminalOutcomeStateChangesAsync_RemovesQteBackupRootAfterSuccessfulRun()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 60 }
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
                "appearanceDescription": "Тестовая хранительница."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 75, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_existing",
                "projectType": "abode_expansion",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Существующий проект",
                "activeState": "Planning",
                "totalWork": 10,
                "workDone": 2,
                "totalStages": 2,
                "currentStage": 0,
                "pressure": 1,
                "stability": 98
              }
            }
          ],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """);

        var service = CreateRuntimeCapableService();
        var outcome = new QteSceneService.QteTerminalOutcome
        {
            OutcomeId = "qte_success_cleanup",
            Title = "QTE complete",
            FinalNarrative = "Исход применён.",
            GmSummary = "Regression summary.",
            ResponseFragment = JsonNode.Parse("""
            {
              "response": "Исход применён.",
              "experienceGained": 5,
              "guardianProjectUpdates": [
                {
                  "guardianId": "guardian_alpha",
                  "projectId": "proj_existing",
                  "workDone": 5,
                  "activeState": "Advancing"
                }
              ]
            }
            """)!.AsObject()
        };

        await service.ApplyTerminalOutcomeStateChangesAsync(outcome);

        AssertNoQteBackupArtifacts();
    }

    [Fact]
    public async Task ApplyTerminalOutcomeValidatedStateChangesAsync_PreservesSiblingBackupRunDirectory()
    {
        await _fs.WriteFileAtomicAsync("game_state/player/experience.json", """
        {
          "totalExperience": 10
        }
        """);

        var siblingDirectory = _fs.ResolvePath($"{QteNormalizerBackupDirectory}/sibling_run");
        Directory.CreateDirectory(siblingDirectory);
        var siblingFile = Path.Combine(siblingDirectory, "stale_backup.json");
        await File.WriteAllTextAsync(siblingFile, "{ \"temporary\": true }");

        var service = CreateRuntimeCapableService();
        var outcome = new QteSceneService.QteTerminalOutcome
        {
            OutcomeId = "qte_invalid_sibling_cleanup",
            Title = "Invalid outcome",
            FinalNarrative = "Исход применён.",
            GmSummary = "Regression summary.",
            ResponseFragment = JsonNode.Parse("""
            {
              "response": "Исход применён.",
              "experienceGained": 5,
              "playerCharacterNameChange": "Новая личность"
            }
            """)!.AsObject()
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyTerminalOutcomeValidatedStateChangesAsync(outcome));

        Assert.True(File.Exists(siblingFile));

        var backupRoot = _fs.ResolvePath(QteNormalizerBackupDirectory);
        Assert.True(Directory.Exists(backupRoot));
        var backupFiles = Directory.GetFiles(backupRoot, "*", SearchOption.AllDirectories);
        Assert.Single(backupFiles);
        Assert.Equal("stale_backup.json", Path.GetFileName(backupFiles[0]));
        Assert.Equal("{ \"temporary\": true }", await File.ReadAllTextAsync(backupFiles[0]));
        var runDirectories = Directory.GetDirectories(backupRoot, "*", SearchOption.TopDirectoryOnly);
        Assert.Single(runDirectories);
        Assert.Equal("sibling_run", Path.GetFileName(runDirectories[0]));
    }

    private QteSceneService CreateRuntimeCapableService()
    {
        var settings = new GameSettings();
        return new QteSceneService(
            _fs,
            settings,
            null!,
            null!,
            null!,
            new StateDistributor(_fs, NullLogger<StateDistributor>.Instance),
            new ValidationService(_fs, NullLogger<ValidationService>.Instance),
            new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance),
            new StateManager(_fs, settings, NullLogger<StateManager>.Instance),
            NullLogger<QteSceneService>.Instance);
    }

    private async Task<SaveLoadService> CreateSaveLoadServiceAsync()
    {
        var settings = new GameSettings();
        var stateManager = new StateManager(_fs, settings, NullLogger<StateManager>.Instance);
        await stateManager.RefreshGameStateAsync();
        return new SaveLoadService(_fs, stateManager, NullLogger<SaveLoadService>.Instance);
    }

    private void AssertNoQteBackupArtifacts()
    {
        var backupDirectory = _fs.ResolvePath(QteNormalizerBackupDirectory);
        Assert.False(Directory.Exists(backupDirectory));
    }

    private static ConsoleKeyInfo[] RepeatKey(ConsoleKey key, int count)
    {
        var keyChar = key == ConsoleKey.Spacebar ? ' ' : char.ToLowerInvariant(key.ToString()[0]);
        return Enumerable.Range(0, count)
            .Select(_ => new ConsoleKeyInfo(keyChar, key, false, false, false))
            .ToArray();
    }

    private static string ResolveMashInputGrade(
        string[] acceptedTokens,
        int successTarget,
        int partialTarget,
        ConsoleKeyInfo[] inputs) =>
        QteSceneService.ResolveMashInputGrade(acceptedTokens, successTarget, partialTarget, inputs);

    private static int ComputeMashInputEffectiveTargetPresses(int targetPresses, int baseDifficulty, int statTier) =>
        QteSceneService.ComputeMashInputEffectiveTargetPresses(targetPresses, baseDifficulty, statTier);

    private static int ComputeMashInputPartialTargetPresses(int successTarget, double partialThreshold) =>
        QteSceneService.ComputeMashInputPartialTargetPresses(successTarget, partialThreshold);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // ignored
        }
    }
}
