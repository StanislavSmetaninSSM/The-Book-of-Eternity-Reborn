using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Spectre.Console;
using Spectre.Console.Rendering;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "RegressionIntegration")]
public sealed partial class QteSceneServiceTests : IDisposable
{
    private const string QteNormalizerBackupDirectory = "game_state/control/qte_normalizer_backups";
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly QteSceneService _service;

    private readonly Xunit.Abstractions.ITestOutputHelper? _cleanupOutput;

    public QteSceneServiceTests(Xunit.Abstractions.ITestOutputHelper? output = null)
    {
        _cleanupOutput = output;
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

    [Fact]
    public async Task EnsureRuntimeStateHealthyAsync_InvalidJsonFailsWithoutMutation()
    {
        const string originalJson = "{ invalid json";
        await _fs.WriteFileAtomicAsync(
            QteSceneService.QteRuntimePath,
            originalJson);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => _service.EnsureRuntimeStateHealthyAsync());

        Assert.Equal(
            originalJson,
            await _fs.ReadFileAsync(QteSceneService.QteRuntimePath));
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
    public async Task EnsureRuntimeStateHealthyAsync_BrokenActiveSceneFailsWithoutMutation()
    {
        const string originalJson = """
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
        """;
        await _fs.WriteFileAtomicAsync(
            QteSceneService.QteRuntimePath,
            originalJson);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => _service.EnsureRuntimeStateHealthyAsync());

        Assert.Equal(
            originalJson,
            await _fs.ReadFileAsync(QteSceneService.QteRuntimePath));
    }

    [Theory]
    [InlineData(
        """
        {
          "qteId": "qte_duplicate_turn",
          "sourceTurnNumber": 12,
          "sourceTurnNumber": 12
        }
        """)]
    [InlineData(
        """
        {
          "qteId": "qte_wrong_turn_type",
          "sourceTurnNumber": "12"
        }
        """)]
    [InlineData("""{"qteId":"qte_malformed_turn","sourceTurnNumber":""")]
    [InlineData("   ")]
    [InlineData("null")]
    public async Task TryReadOfferAsync_InvalidTurnAuthorityIsDistinctFromMissingOffer(
        string invalidOfferJson)
    {
        const string runtimeJson = """
        {
          "lastDeclinedQteId": "qte_previous",
          "lastDeclinedAtTurn": 11
        }
        """;
        await _fs.WriteFileAtomicAsync(
            QteSceneService.QteOfferPath,
            invalidOfferJson);
        await _fs.WriteFileAtomicAsync(
            QteSceneService.QteRuntimePath,
            runtimeJson);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => _service.TryReadOfferAsync());

        Assert.Equal(
            invalidOfferJson,
            await _fs.ReadFileAsync(QteSceneService.QteOfferPath));
        Assert.Equal(
            runtimeJson,
            await _fs.ReadFileAsync(QteSceneService.QteRuntimePath));
    }

    [Theory]
    [InlineData(
        """
        {
          "pendingOffer": null,
          "activeScene": {
            "offer": {
              "qteId": "qte_duplicate_accepted_turn",
              "sourceTurnNumber": 12
            },
            "currentChapterId": "yard",
            "acceptedAtTurn": 12,
            "acceptedAtTurn": 12
          }
        }
        """)]
    [InlineData(
        """
        {
          "pendingOffer": null,
          "activeScene": {
            "offer": {
              "qteId": "qte_wrong_accepted_turn_type",
              "sourceTurnNumber": 12
            },
            "currentChapterId": "yard",
            "acceptedAtTurn": "12"
          }
        }
        """)]
    [InlineData(
        """
        {
          "pendingOffer": null,
          "activeScene": {
            "acceptedAtTurn":
        """)]
    public async Task ResolveActiveActionAsync_InvalidPersistedTurnAuthorityFailsWithoutMutation(
        string invalidRuntimeJson)
    {
        await _fs.WriteFileAtomicAsync(
            QteSceneService.QteRuntimePath,
            invalidRuntimeJson);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            _service.ResolveActiveActionAsync(
                "cross_yard",
                submittedGrade: null,
                currentTurnNumber: 12,
                allowPreexistingStateIssues: true));

        Assert.Equal(
            invalidRuntimeJson,
            await _fs.ReadFileAsync(QteSceneService.QteRuntimePath));
        Assert.False(_fs.FileExists(QteSceneService.QteHistoryPath));
    }

    [Fact]
    public async Task BindAcceptedTurnAuthorityAsync_PersistsTrustedPositiveSourceTurn()
    {
        var offer = BuildUnscoredBranchChoiceOffer();
        await _fs.WriteFileAtomicAsync(
            QteSceneService.QteOfferPath,
            JsonSerializer.Serialize(offer));

        var boundOffer = await _service.BindAcceptedTurnAuthorityAsync(
            offer,
            sourceTurnNumber: 17);

        Assert.Equal(17, boundOffer.SourceTurnNumber);
        using var persisted = await ReadJsonDocumentAsync(QteSceneService.QteOfferPath);
        Assert.Equal(
            17,
            persisted.RootElement.GetProperty("sourceTurnNumber").GetInt32());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task BindAcceptedTurnAuthorityAsync_NonPositiveTurnFailsWithoutMutation(
        int sourceTurnNumber)
    {
        var offer = BuildUnscoredBranchChoiceOffer();
        var originalJson = JsonSerializer.Serialize(offer);
        await _fs.WriteFileAtomicAsync(QteSceneService.QteOfferPath, originalJson);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.BindAcceptedTurnAuthorityAsync(offer, sourceTurnNumber));

        Assert.Equal(
            originalJson,
            await _fs.ReadFileAsync(QteSceneService.QteOfferPath));
    }

    [Theory]
    [InlineData(null, 12)]
    [InlineData(0, 12)]
    [InlineData(-1, 12)]
    [InlineData(11, 12)]
    public async Task BeginAcceptedSceneAsync_UnboundOrMismatchedTurnFailsWithoutRuntimeMutation(
        int? sourceTurnNumber,
        int acceptedAtTurn)
    {
        var offer = BuildUnscoredBranchChoiceOffer();
        offer.SourceTurnNumber = sourceTurnNumber;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.BeginAcceptedSceneAsync(offer, acceptedAtTurn));

        Assert.False(_fs.FileExists(QteSceneService.QteRuntimePath));
    }

    [Theory]
    [InlineData(null, 12)]
    [InlineData(0, 12)]
    [InlineData(-1, 12)]
    [InlineData(11, 12)]
    public async Task RecordDeclineAsync_UnboundOrMismatchedTurnFailsWithoutMutation(
        int? sourceTurnNumber,
        int declinedAtTurn)
    {
        var offer = BuildUnscoredBranchChoiceOffer();
        offer.SourceTurnNumber = sourceTurnNumber;
        var originalJson = JsonSerializer.Serialize(offer);
        await _fs.WriteFileAtomicAsync(QteSceneService.QteOfferPath, originalJson);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.RecordDeclineAsync(offer, declinedAtTurn));

        Assert.Equal(
            originalJson,
            await _fs.ReadFileAsync(QteSceneService.QteOfferPath));
        Assert.False(_fs.FileExists(QteSceneService.QteRuntimePath));
    }

    [Fact]
    public async Task EnsureRuntimeStateHealthyAsync_NonPositiveAcceptedTurnFailsWithoutMutation()
    {
        const string originalJson = """
        {
          "pendingOffer": {
            "qteId": "qte_bridge",
            "sourceTurnNumber": 0
          },
          "activeScene": {
            "offer": {
              "qteId": "qte_bridge",
              "sourceTurnNumber": 0
            },
            "currentChapterId": "start",
            "acceptedAtTurn": 0
          },
          "lastResolvedQteSummaryPendingReminder": "QTE summary"
        }
        """;
        await _fs.WriteFileAtomicAsync(
            QteSceneService.QteRuntimePath,
            originalJson);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => _service.EnsureRuntimeStateHealthyAsync());

        Assert.Equal(
            originalJson,
            await _fs.ReadFileAsync(QteSceneService.QteRuntimePath));
    }

    [Fact]
    public async Task ResolveActiveActionAsync_NonPositiveRuntimeAuthorityFailsWithoutMutation()
    {
        var offer = BuildScoredBranchChoiceOffer();
        offer.SourceTurnNumber = 0;
        var runtime = new QteSceneService.QteRuntimeState
        {
            PendingOffer = offer,
            ActiveScene = new QteSceneService.ActiveQteSceneState
            {
                Offer = offer,
                CurrentChapterId = offer.StartChapterId,
                AcceptedAtTurn = 0
            }
        };
        var originalJson = JsonSerializer.Serialize(runtime);
        await _fs.WriteFileAtomicAsync(
            QteSceneService.QteRuntimePath,
            originalJson);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ResolveActiveActionAsync(
                "cross_yard",
                submittedGrade: null,
                currentTurnNumber: 21,
                allowPreexistingStateIssues: true));

        Assert.Equal(
            originalJson,
            await _fs.ReadFileAsync(QteSceneService.QteRuntimePath));
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

        const string originalPowerJournal = """
        {
          "entries": [
            {
              "entryId": "journal_qte_preserve",
              "eventId": "event_qte_preserve",
              "turn": 59,
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "delta": 1,
              "reasonType": "project_assist",
              "sourceSurface": "guardianProjectUpdates",
              "sourceId": "proj_existing",
              "title": "Existing QTE-adjacent journal entry",
              "summary": "QTE normalization must not run turn-level journal repair.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {}
            }
          ]
        }
        """;
        await _fs.WriteFileAtomicAsync(
            GuardianPowerEventState.JournalPath,
            originalPowerJournal);

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
        Assert.Equal(
            originalPowerJournal.Replace("\r\n", "\n"),
            (await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath))?
                .Replace("\r\n", "\n"));
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
    public async Task ResolveActiveActionAsync_AppliesScoreDeltasComputesRankAndWritesHistory()
    {
        await SeedPristineResourceStateAsync();
        var service = CreateRuntimeCapableService();
        var offer = BuildScoredBranchChoiceOffer();

        await service.BeginAcceptedSceneAsync(offer, currentTurnNumber: 10);

        using (var initialRuntime = await ReadJsonDocumentAsync(QteSceneService.QteRuntimePath))
        {
            var scoreState = initialRuntime.RootElement
                .GetProperty("activeScene")
                .GetProperty("scoreState");
            AssertMetricValue(scoreState, "stealth", 50);
            AssertMetricValue(scoreState, "alarm", 10);
            AssertMetricValue(scoreState, "evidence", 0);
        }

        var first = await service.ResolveActiveActionAsync("cross_yard", null, currentTurnNumber: 11, allowPreexistingStateIssues: true);
        Assert.Equal("Active", first.State);

        using (var afterFirst = await ReadJsonDocumentAsync(QteSceneService.QteRuntimePath))
        {
            var scoreState = afterFirst.RootElement
                .GetProperty("activeScene")
                .GetProperty("scoreState");
            AssertMetricValue(scoreState, "stealth", 100);
            AssertMetricValue(scoreState, "alarm", 0);
            AssertMetricValue(scoreState, "evidence", 0);
        }

        var second = await service.ResolveActiveActionAsync("search_study", null, currentTurnNumber: 12, allowPreexistingStateIssues: true);
        Assert.Equal("Active", second.State);

        var final = await service.ResolveActiveActionAsync("escape_roof", null, currentTurnNumber: 13, allowPreexistingStateIssues: true);
        Assert.Equal("Completed", final.State);
        Assert.NotNull(final.Completion);
        Assert.Contains("Ранг: Удачный исход", final.Completion!.Summary, StringComparison.Ordinal);

        using var runtime = await ReadJsonDocumentAsync(QteSceneService.QteRuntimePath);
        Assert.False(runtime.RootElement.TryGetProperty("activeScene", out var activeScene) &&
                     activeScene.ValueKind != JsonValueKind.Null);
        Assert.Contains(
            "Ранг: Удачный исход",
            runtime.RootElement.GetProperty("lastResolvedQteSummaryPendingReminder").GetString(),
            StringComparison.Ordinal);

        using var history = await ReadJsonDocumentAsync(QteSceneService.QteHistoryPath);
        var entry = Assert.Single(history.RootElement.EnumerateArray());
        var finalScore = entry.GetProperty("finalScore");
        Assert.Equal("good", finalScore.GetProperty("rank").GetProperty("id").GetString());
        AssertMetricValue(finalScore, "stealth", 65);
        AssertMetricValue(finalScore, "alarm", 35);
        AssertMetricValue(finalScore, "evidence", 37);

        var audit = entry.GetProperty("scoreAudit").EnumerateArray().ToArray();
        Assert.Equal(7, audit.Length);
        Assert.Equal("cross_yard", audit[0].GetProperty("actionId").GetString());
        Assert.Equal("success", audit[0].GetProperty("grade").GetString());
        Assert.Equal("stealth", audit[0].GetProperty("metric").GetString());
        Assert.Equal(50, audit[0].GetProperty("previousValue").GetDouble());
        Assert.Equal(75, audit[0].GetProperty("delta").GetDouble());
        Assert.Equal(100, audit[0].GetProperty("newValue").GetDouble());
        Assert.Equal("escape_roof", audit[^1].GetProperty("actionId").GetString());
        Assert.Equal("fail", audit[^1].GetProperty("grade").GetString());
    }

    [Fact]
    public async Task StartAcceptedSceneAsync_RendersFinalScoreSummaryInConsoleAndResponse()
    {
        var originalConsole = AnsiConsole.Console;
        var console = new QueuedAnsiConsole(Enumerable.Repeat(Key(ConsoleKey.Enter), 8));
        AnsiConsole.Console = console;

        try
        {
            await SeedMinimalValidatedMortalStateAsync();
            var service = CreateRuntimeCapableService(new QueuedConsoleInputSource(Enumerable.Repeat(Key(ConsoleKey.Enter), 3)));
            var offer = BuildScoredBranchChoiceOffer();
            offer.ScoreModel!.Metrics.Add(new QteSceneService.QteScoreMetricDefinition
            {
                Id = "secretTrace",
                Label = "Тайный след",
                Initial = 7,
                Min = 0,
                Max = 10,
                Visibility = "hidden"
            });

            var completion = await service.StartAcceptedSceneAsync(offer, currentTurnNumber: 10);
            var output = console.Output;

            Assert.Contains("Удачный исход", output, StringComparison.Ordinal);
            Assert.Contains("Скрытность: 65", output, StringComparison.Ordinal);
            Assert.Contains("Тревога: 35", output, StringComparison.Ordinal);
            Assert.Contains("Улики: 37", output, StringComparison.Ordinal);
            Assert.DoesNotContain("Тайный след", output, StringComparison.Ordinal);
            Assert.DoesNotContain("scoreModel", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("scoreDeltas", output, StringComparison.OrdinalIgnoreCase);

            Assert.NotNull(completion.ScoreSummary);
            Assert.Contains("Ранг: Удачный исход", completion.Response.Response, StringComparison.Ordinal);
            Assert.Contains("Скрытность: 65", completion.Response.Response, StringComparison.Ordinal);
            Assert.Contains("Тревога: 35", completion.Response.Response, StringComparison.Ordinal);
            Assert.Contains("Улики: 37", completion.Response.Response, StringComparison.Ordinal);
            Assert.DoesNotContain("Тайный след", completion.Response.Response, StringComparison.Ordinal);
            Assert.DoesNotContain("scoreModel", completion.Response.Response, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("scoreDeltas", completion.Response.Response, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }

    [Fact]
    public async Task ResolveActiveActionAsync_LeavesUnscoredQteHistoryUnchanged()
    {
        await SeedPristineResourceStateAsync();
        var service = CreateRuntimeCapableService();
        var offer = BuildUnscoredBranchChoiceOffer();

        await service.BeginAcceptedSceneAsync(offer, currentTurnNumber: 20);
        var final = await service.ResolveActiveActionAsync("open_gate", null, currentTurnNumber: 21, allowPreexistingStateIssues: true);

        Assert.Equal("Completed", final.State);

        var runtimeJson = await _fs.ReadFileAsync(QteSceneService.QteRuntimePath);
        Assert.DoesNotContain("scoreState", runtimeJson, StringComparison.Ordinal);
        using var history = await ReadJsonDocumentAsync(QteSceneService.QteHistoryPath);
        var entry = Assert.Single(history.RootElement.EnumerateArray());
        Assert.False(entry.TryGetProperty("finalScore", out _));
        Assert.False(entry.TryGetProperty("scoreAudit", out _));
    }

    [Theory]
    [InlineData("success", 5)]
    [InlineData("fail", 20)]
    public async Task ResolveActiveActionAsync_AppliesOnlySelectedTerminalResourceDamageThroughCanonicalQuartet(
        string selectedGrade,
        int expectedDamage)
    {
        var before = await SeedMortalPlayerResourceQuartetAsync();
        var service = CreateRuntimeCapableService();
        var offer = BuildResourceTerminalOffer(selectedGrade);
        await service.BeginAcceptedSceneAsync(offer, currentTurnNumber: 12);

        var result = await service.ResolveActiveActionAsync(
            "brace",
            selectedGrade,
            currentTurnNumber: 12,
            allowPreexistingStateIssues: true);

        Assert.Equal("Completed", result.State);
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            await _fs.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath),
            allowMissingPristine: false);
        Assert.True(definitions.IsValid, string.Join("; ", definitions.Issues.Select(issue => issue.Code)));
        var state = ResourceStateContract.ParseCanonical(
            await _fs.ReadFileAsync(ResourceMaterializationContract.StatePath),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.True(state.IsValid, string.Join("; ", state.Issues.Select(issue => issue.Code)));
        Assert.True(state.Ledger!.TryResolveExact(before.Coordinate, out var after));
        Assert.Equal(before.Current - expectedDamage, after!.Current);

        var history = ResourceHistoryState.ParseCanonical(
            await _fs.ReadFileAsync(ResourceMaterializationContract.HistoryPath),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.True(history.IsValid, string.Join("; ", history.Issues.Select(issue => issue.Code)));
        var transition = Assert.Single(
            history.History!.Transitions,
            candidate => candidate.EventRef.Contains(
                ":qte_terminal:",
                StringComparison.Ordinal));
        var expectedOutcomeOrdinal = selectedGrade == "success" ? 1 : 2;
        Assert.Equal(
            $"turn_12:qte_terminal:{expectedOutcomeOrdinal}:resource:1",
            transition.EventRef);
        Assert.Equal("narrative_outcome", transition.OriginKind);
        Assert.Equal((decimal)expectedDamage, transition.RequestedAmount);
        Assert.False(_fs.FileExists(ResourceMaterializationContract.CommandPath));
    }

    [Fact]
    public async Task ResolveActiveActionAsync_ResurrectedRuntimeAgainstTerminalContinuationFailsWithoutDuplicateDamage()
    {
        var before = await SeedMortalPlayerResourceQuartetAsync();
        var service = CreateRuntimeCapableService();
        var offer = BuildResourceTerminalOffer("fail");
        await service.BeginAcceptedSceneAsync(offer, currentTurnNumber: 12);
        var activeRuntime = await _fs.ReadFileBytesAsync(QteSceneService.QteRuntimePath);

        var first = await service.ResolveActiveActionAsync(
            "brace",
            "fail",
            currentTurnNumber: 12,
            allowPreexistingStateIssues: true);
        Assert.Equal("Completed", first.State);
        Assert.NotNull(activeRuntime);
        await _fs.WriteFileAtomicBytesAsync(QteSceneService.QteRuntimePath, activeRuntime!);
        var beforeReplayAttempt = await CaptureTrackedBytesAsync(
            QteSceneService.BrowserTransactionRollbackPaths);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.ResolveActiveActionAsync(
                "brace",
                "fail",
                currentTurnNumber: 12,
                allowPreexistingStateIssues: true));

        Assert.Contains(
            "qte_deferred_continuation_runtime_mismatch",
            error.Message,
            StringComparison.Ordinal);
        await AssertTrackedBytesAsync(beforeReplayAttempt);
        var quartet = await ReadResourceQuartetAsync();
        Assert.True(quartet.State.TryResolveExact(before.Coordinate, out var after));
        Assert.Equal(before.Current - 20m, after!.Current);
        Assert.Single(
            quartet.History.Transitions,
            transition => transition.EventRef.Contains(
                ":qte_terminal:",
                StringComparison.Ordinal));
        Assert.False(_fs.FileExists(ResourceMaterializationContract.CommandPath));
    }

    [Fact]
    public async Task ResolveActiveActionAsync_DirectLatePersistenceFailureRollsBackResourceQuartetAndPreservesSelectedBoundary()
    {
        await SeedMortalPlayerResourceQuartetAsync();
        var failure = new IOException("Injected direct terminal history failure.");
        var service = CreateRuntimeCapableService(
            hooks: new QteSceneServiceHooks
            {
                AfterHistoryWrittenAsync = () => Task.FromException(failure)
            });
        var offer = BuildResourceTerminalOffer("fail");
        await service.BeginAcceptedSceneAsync(offer, currentTurnNumber: 12);
        var before = await CaptureResourceQuartetBytesAsync();

        var error = await Assert.ThrowsAsync<IOException>(() =>
            service.ResolveActiveActionAsync(
                "brace",
                "fail",
                currentTurnNumber: 12,
                allowPreexistingStateIssues: true));

        Assert.Same(failure, error);
        await AssertResourceQuartetBytesAsync(before);
        await AssertTerminalSelectedContinuationAndRuntimeAsync();
        Assert.False(_fs.FileExists(QteSceneService.QteHistoryPath));
        Assert.False(_fs.FileExists(ResourceMaterializationContract.CommandPath));
    }

    [Fact]
    public async Task ResolveActiveActionAsync_LateFailureRestoresBomAndNonTextBytesExactly()
    {
        const string targetPath = "output/narrative_response.json";
        var originalBytes = new byte[]
        {
            0xEF, 0xBB, 0xBF,
            (byte)'b', (byte)'e', (byte)'f', (byte)'o', (byte)'r', (byte)'e',
            0x00, 0xFF, 0x80
        };
        await SeedMortalPlayerResourceQuartetAsync();
        var failure = new IOException("Injected byte-exact terminal history failure.");
        var service = CreateRuntimeCapableService(
            hooks: new QteSceneServiceHooks
            {
                AfterHistoryWrittenAsync = () => Task.FromException(failure)
            });
        var offer = BuildResourceTerminalOffer("fail");
        await service.BeginAcceptedSceneAsync(offer, currentTurnNumber: 12);
        await _fs.WriteFileAtomicBytesAsync(targetPath, originalBytes);

        var error = await Assert.ThrowsAsync<IOException>(() =>
            service.ResolveActiveActionAsync(
                "brace",
                "fail",
                currentTurnNumber: 12,
                allowPreexistingStateIssues: true));

        Assert.Same(failure, error);
        Assert.Equal(originalBytes, await _fs.ReadFileBytesAsync(targetPath));
        AssertNoQteBackupArtifacts();
    }

    [Fact]
    public async Task ResolveActiveActionAsync_MissingPresentFileBackupFailsClosedAndRetainsRecoveryEvidence()
    {
        const string targetPath = "output/narrative_response.json";
        var originalBytes = new byte[] { 0xEF, 0xBB, 0xBF, (byte)'o', (byte)'l', (byte)'d' };
        await SeedMortalPlayerResourceQuartetAsync();
        var injectedFailure = new IOException("Injected failure after backup removal.");
        var removedBackup = false;
        var service = CreateRuntimeCapableService(
            hooks: new QteSceneServiceHooks
            {
                AfterHistoryWrittenAsync = () =>
                {
                    var backupRoot = _fs.ResolvePath(QteNormalizerBackupDirectory);
                    var targetBackup = Directory
                        .GetFiles(backupRoot, "*", SearchOption.AllDirectories)
                        .Single(path => Path.GetFileName(path).Contains(
                            "output_narrative_response.json",
                            StringComparison.OrdinalIgnoreCase));
                    File.Delete(targetBackup);
                    removedBackup = true;
                    return Task.FromException(injectedFailure);
                }
            });
        var offer = BuildResourceTerminalOffer("fail");
        await service.BeginAcceptedSceneAsync(offer, currentTurnNumber: 12);
        await _fs.WriteFileAtomicBytesAsync(targetPath, originalBytes);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ResolveActiveActionAsync(
                "brace",
                "fail",
                currentTurnNumber: 12,
                allowPreexistingStateIssues: true));

        Assert.True(removedBackup);
        Assert.Contains("QTE rollback", error.Message, StringComparison.Ordinal);
        Assert.Contains(targetPath, error.Message, StringComparison.Ordinal);
        var combinedFailure = Assert.IsType<AggregateException>(error.InnerException);
        Assert.Contains(injectedFailure, combinedFailure.InnerExceptions);
        Assert.True(_fs.FileExists(targetPath));
        var backupRootAfterFailure = _fs.ResolvePath(QteNormalizerBackupDirectory);
        Assert.True(Directory.Exists(backupRootAfterFailure));
        Assert.NotEmpty(Directory.GetFiles(
            backupRootAfterFailure,
            "*",
            SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ResolveActiveActionAsync_RollbackRefreshFailureAggregatesOriginalAndRetainsBackups()
    {
        const string targetPath = "output/narrative_response.json";
        var originalBytes = new byte[] { 0xEF, 0xBB, 0xBF, (byte)'o', (byte)'l', (byte)'d' };
        await SeedMortalPlayerResourceQuartetAsync();
        var originalFailure = new IOException("Injected terminal history failure.");
        var refreshFailure = new IOException("Injected post-rollback refresh failure.");
        var failRefresh = false;
        var hookedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                BeforeCanonicalReadOpenAsync = path =>
                    failRefresh && string.Equals(
                        path,
                        ResourceMaterializationContract.StatePath,
                        StringComparison.OrdinalIgnoreCase)
                        ? Task.FromException(refreshFailure)
                        : Task.CompletedTask
            });
        var service = CreateRuntimeCapableService(
            hookedFs,
            hooks: new QteSceneServiceHooks
            {
                AfterHistoryWrittenAsync = () =>
                {
                    failRefresh = true;
                    return Task.FromException(originalFailure);
                }
            });
        var offer = BuildResourceTerminalOffer("fail");
        await service.BeginAcceptedSceneAsync(offer, currentTurnNumber: 12);
        await _fs.WriteFileAtomicBytesAsync(targetPath, originalBytes);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ResolveActiveActionAsync(
                "brace",
                "fail",
                currentTurnNumber: 12,
                allowPreexistingStateIssues: true));

        var combined = Assert.IsType<AggregateException>(error.InnerException).Flatten();
        Assert.Contains(originalFailure, combined.InnerExceptions);
        Assert.Contains(refreshFailure, combined.InnerExceptions);
        Assert.Equal(originalBytes, await _fs.ReadFileBytesAsync(targetPath));
        var backupRoot = _fs.ResolvePath(QteNormalizerBackupDirectory);
        Assert.True(Directory.Exists(backupRoot));
        Assert.NotEmpty(Directory.GetFiles(backupRoot, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ResolveActiveActionAsync_BackupCleanupFailureDoesNotMaskCommittedResult()
    {
        await SeedMortalPlayerResourceQuartetAsync();
        var cleanupFailure = new IOException("Injected QTE backup cleanup failure.");
        var cleanupArmed = false;
        var hookedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                BeforeCanonicalMutationAsync = path =>
                    cleanupArmed && path.StartsWith(
                        QteNormalizerBackupDirectory,
                        StringComparison.OrdinalIgnoreCase)
                        ? Task.FromException(cleanupFailure)
                        : Task.CompletedTask
            });
        var logger = new RecordingLogger<QteSceneService>();
        var service = CreateRuntimeCapableService(
            hookedFs,
            hooks: new QteSceneServiceHooks
            {
                AfterRuntimeWrittenAsync = state =>
                {
                    if (state.ActiveScene == null)
                        cleanupArmed = true;
                    return Task.CompletedTask;
                }
            },
            logger: logger);
        var offer = BuildResourceTerminalOffer("fail");
        await service.BeginAcceptedSceneAsync(offer, currentTurnNumber: 12);

        var resolution = await service.ResolveActiveActionAsync(
            "brace",
            "fail",
            currentTurnNumber: 12,
            allowPreexistingStateIssues: true);

        Assert.Equal("Completed", resolution.State);
        var runtime = JsonNode.Parse(Assert.IsType<string>(
            await _fs.ReadFileAsync(QteSceneService.QteRuntimePath)))!.AsObject();
        Assert.Null(runtime["activeScene"]);
        var backupRoot = _fs.ResolvePath(QteNormalizerBackupDirectory);
        Assert.True(Directory.Exists(backupRoot));
        Assert.NotEmpty(Directory.GetFiles(backupRoot, "*", SearchOption.AllDirectories));
        Assert.Contains(logger.Messages, message =>
            message.Contains("retained", StringComparison.OrdinalIgnoreCase) &&
            message.Contains(QteNormalizerBackupDirectory, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ResolveActiveActionAsync_RejectsMalformedResourceOwnerAuthorityBeforeAnyOutcomeMutation()
    {
        await SeedMortalPlayerResourceQuartetAsync();
        var service = CreateRuntimeCapableService();
        var offer = BuildResourceTerminalOffer("fail");
        await service.BeginAcceptedSceneAsync(offer, currentTurnNumber: 12);
        await _fs.WriteFileAtomicAsync(
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            """{ "schemaVersion": 1, "historicalOwners": [], "capacityDrafts": [] }""");
        var before = await CaptureTrackedBytesAsync(
            QteSceneService.BrowserTransactionRollbackPaths);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.ResolveActiveActionAsync(
                "brace",
                "fail",
                currentTurnNumber: 12,
                allowPreexistingStateIssues: true));

        Assert.Contains(
            "qte_deferred_continuation_sealed_root_mismatch",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            error.Message,
            StringComparison.Ordinal);
        await AssertTrackedBytesAsync(before);
        Assert.False(_fs.FileExists(QteSceneService.QteHistoryPath));
        Assert.False(_fs.FileExists(ResourceMaterializationContract.CommandPath));
    }

    [Fact]
    public async Task ResolveActiveActionAsync_RawEffectWithoutImmutableContinuation_PersistsSelectedBoundaryAndPreservesFinalPublicationState()
    {
        await SeedMortalPlayerResourceQuartetAsync();
        var service = CreateRuntimeCapableService();
        var offer = BuildUnscoredBranchChoiceOffer();
        var outcome = Assert.Single(offer.TerminalOutcomes);
        outcome.ResponseFragment!["effectChanges"] = new JsonArray(new JsonObject
        {
            ["operation"] = "apply"
        });
        await service.BeginAcceptedSceneAsync(offer, currentTurnNumber: 20);
        var before = await CaptureTrackedBytesAsync(QteSceneService.BrowserTransactionRollbackPaths);
        Assert.NotNull(before[QteSceneService.QteRuntimePath]);
        Assert.Null(before[QteSceneService.QteHistoryPath]);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.ResolveActiveActionAsync(
                "open_gate",
                submittedGrade: null,
                currentTurnNumber: 21,
                allowPreexistingStateIssues: true));

        Assert.Contains(
            "validated common-plan binding",
            exception.Message,
            StringComparison.Ordinal);
        await AssertTrackedBytesAsync(
            before,
            QteSceneService.QteRuntimePath,
            QteDeferredEffectContinuation.StatePath);
        await AssertTerminalSelectedContinuationAndRuntimeAsync();
        Assert.False(_fs.FileExists(QteSceneService.QteHistoryPath));
        AssertNoQteBackupArtifacts();
    }

    [Fact]
    public async Task QteTerminalResourceOutcome_WorkCountersRemainLinearForMultiCommandSelection()
    {
        await SeedMortalPlayerResourceQuartetAsync();
        var quartet = await ReadResourceQuartetAsync();
        var baselineHistoryCount = quartet.History.Transitions.Count;

        var sixteen = await BuildQteTerminalResourcePlanAsync(commandCount: 16);
        var thirtyTwo = await BuildQteTerminalResourcePlanAsync(commandCount: 32);

        Assert.True(sixteen.IsValid, string.Join("; ", sixteen.Issues.Select(issue => issue.Code)));
        Assert.True(thirtyTwo.IsValid, string.Join("; ", thirtyTwo.Issues.Select(issue => issue.Code)));
        AssertQteResourceWork(sixteen, 16, baselineHistoryCount);
        AssertQteResourceWork(thirtyTwo, 32, baselineHistoryCount);
    }

    [Fact]
    public async Task ApplyTerminalOutcomeValidatedStateChangesAsync_ExistedIdNullOnlyWithoutImmutableContinuation_FailsClosedAndRollsBack()
    {
        await SeedMinimalValidatedMortalStateAsync();
        await _fs.WriteFileAtomicAsync(
            "input/turn_request.json",
            new JsonObject
            {
                ["sessionId"] = "session_qte_null_existed_id",
                ["requestId"] = "request_qte_null_existed_id",
                ["turnNumber"] = 42,
                ["playerAction"] = "Resolve a deferred QTE outcome."
            }.ToJsonString());
        var trackedPaths = new[]
        {
            InventoryEquipmentService.ItemsPath,
            MortalItemIdentityState.StatePath,
            "game_state/player/experience.json",
            "output/narrative_response.json"
        };
        var before = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in trackedPaths)
            before[path] = await _fs.ReadFileBytesAsync(path);
        var service = CreateRuntimeCapableService();
        var outcome = new QteSceneService.QteTerminalOutcome
        {
            OutcomeId = "qte_null_existed_id_without_continuation",
            Title = "Deferred item outcome",
            FinalNarrative = "Исход пытается выдать предмет.",
            GmSummary = "Raw item must wait for immutable accepted continuation authority.",
            ResponseFragment = new JsonObject
            {
                ["response"] = "Исход пытается выдать предмет.",
                ["UpdateInventory"] = new JsonArray(new JsonObject
                {
                    ["existedId"] = null,
                    ["name"] = "Отложенный предмет без identity allocation"
                })
            }
        };

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.ApplyTerminalOutcomeValidatedStateChangesAsync(
                outcome,
                allowPreexistingStateIssues: true));

        Assert.Contains(
            "validated common-plan binding",
            exception.Message,
            StringComparison.Ordinal);
        foreach (var (path, expected) in before)
            Assert.Equal(expected, await _fs.ReadFileBytesAsync(path));
        AssertNoQteBackupArtifacts();
    }

    [Fact]
    public async Task ApplyTerminalOutcomeStateChangesAsync_CreationRefOnlyWithoutImmutableContinuation_FailsBeforeWriteLeaseOrMutation()
    {
        await SeedMinimalValidatedMortalStateAsync();
        var before = await CaptureTrackedBytesAsync(QteSceneService.BrowserTransactionRollbackPaths);
        var writeLeaseOpenAttempts = 0;
        var mutationAttempts = 0;
        var hookedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                BeforeCanonicalWriteLockOpenAsync = () =>
                {
                    writeLeaseOpenAttempts++;
                    return Task.CompletedTask;
                },
                BeforeCanonicalMutationAsync = _ =>
                {
                    mutationAttempts++;
                    return Task.CompletedTask;
                }
            });
        var service = CreateRuntimeCapableService(hookedFs);
        var outcome = new QteSceneService.QteTerminalOutcome
        {
            OutcomeId = "qte_creation_ref_without_continuation",
            Title = "Deferred item outcome",
            FinalNarrative = "Исход пытается выдать предмет.",
            GmSummary = "A creationRef must wait for immutable accepted continuation authority.",
            ResponseFragment = new JsonObject
            {
                ["response"] = "Исход пытается выдать предмет.",
                ["UpdateInventory"] = new JsonArray(new JsonObject
                {
                    ["creationRef"] = "new_item_qte_creation_ref_only",
                    ["name"] = "Отложенный предмет с creationRef"
                })
            }
        };

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.ApplyTerminalOutcomeStateChangesAsync(outcome));

        Assert.Contains(
            "validated common-plan binding",
            exception.Message,
            StringComparison.Ordinal);
        Assert.Equal(0, writeLeaseOpenAttempts);
        Assert.Equal(0, mutationAttempts);
        await AssertTrackedBytesAsync(before);
        AssertNoQteBackupArtifacts();
    }

    [Fact]
    public async Task ApplyTerminalOutcomeStateChangesAsync_PermanentExistedIdUpdate_DoesNotRequireContinuation()
    {
        await SeedMinimalValidatedMortalStateAsync();
        var existingItem = MortalItemTestFixture.CreateCanonicalRoot("itm_qte_permanent");
        await _fs.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            MortalItemTestFixture.CreateCarrier(
                    existingItem,
                    "player_inventory",
                    "player")
                .ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync(
            MortalItemIdentityState.StatePath,
            MortalItemTestFixture.CreateIndex(existingItem)
                .ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var service = CreateRuntimeCapableService();
        var outcome = new QteSceneService.QteTerminalOutcome
        {
            OutcomeId = "qte_existing_item_update",
            Title = "Existing item outcome",
            FinalNarrative = "Существующий предмет остаётся тем же экземпляром.",
            GmSummary = "A permanent existedId is an update, not raw materialization.",
            ResponseFragment = new JsonObject
            {
                ["response"] = "Существующий предмет остаётся тем же экземпляром.",
                ["UpdateInventory"] = new JsonArray(new JsonObject
                {
                    ["existedId"] = "itm_qte_permanent",
                    ["count"] = 1
                })
            }
        };

        var response = await service.ApplyTerminalOutcomeStateChangesAsync(outcome);

        var update = Assert.Single(response.UpdateInventory!);
        Assert.Equal("itm_qte_permanent", update.GetProperty("existedId").GetString());
        AssertNoQteBackupArtifacts();
    }

    [Fact]
    public async Task ApplyTerminalOutcomeValidatedStateChangesAsync_RawEffectWithoutImmutableContinuation_FailsClosedAndRollsBack()
    {
        await SeedMinimalValidatedMortalStateAsync();
        await _fs.WriteFileAtomicAsync(
            "input/turn_request.json",
            new JsonObject
            {
                ["sessionId"] = "session_qte_raw_effect",
                ["requestId"] = "request_qte_raw_effect",
                ["turnNumber"] = 42,
                ["playerAction"] = "Resolve a deferred QTE outcome."
            }.ToJsonString());
        var trackedPaths = new[]
        {
            EffectAcceptedTurnPlan.CommandPath,
            EffectAcceptedTurnPlan.IdentityIndexPath,
            "game_state/player/experience.json",
            "output/narrative_response.json"
        };
        var before = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in trackedPaths)
            before[path] = await _fs.ReadFileBytesAsync(path);
        var service = CreateRuntimeCapableService();
        var outcome = new QteSceneService.QteTerminalOutcome
        {
            OutcomeId = "qte_raw_effect_without_continuation",
            Title = "Deferred effect outcome",
            FinalNarrative = "Исход пытается наложить эффект.",
            GmSummary = "Raw effect must wait for immutable accepted continuation authority.",
            ResponseFragment = new JsonObject
            {
                ["response"] = "Исход пытается наложить эффект.",
                ["effectChanges"] = new JsonArray(new JsonObject
                {
                    ["operation"] = "apply"
                })
            }
        };

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.ApplyTerminalOutcomeValidatedStateChangesAsync(
                outcome,
                allowPreexistingStateIssues: true));

        Assert.Contains(
            "validated common-plan binding",
            exception.Message,
            StringComparison.Ordinal);
        foreach (var (path, expected) in before)
            Assert.Equal(expected, await _fs.ReadFileBytesAsync(path));
        AssertNoQteBackupArtifacts();
    }

    [Fact]
    public async Task ApplyTerminalOutcomeValidatedStateChangesAsync_MalformedEffectSurface_FailsClosedBeforeTypedDeserialization()
    {
        var before = await CaptureTrackedBytesAsync(QteSceneService.BrowserTransactionRollbackPaths);
        var service = CreateRuntimeCapableService();
        var outcome = new QteSceneService.QteTerminalOutcome
        {
            OutcomeId = "qte_malformed_effect_without_continuation",
            Title = "Malformed deferred effect outcome",
            FinalNarrative = "Исход содержит malformed effect authority.",
            GmSummary = "Malformed effect authority must fail closed before typed deserialization.",
            ResponseFragment = new JsonObject
            {
                ["response"] = "Исход содержит malformed effect authority.",
                ["effectChanges"] = new JsonObject
                {
                    ["operation"] = "apply"
                }
            }
        };

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.ApplyTerminalOutcomeValidatedStateChangesAsync(
                outcome,
                allowPreexistingStateIssues: true));

        Assert.Contains(
            "validated common-plan binding",
            exception.Message,
            StringComparison.Ordinal);
        await AssertTrackedBytesAsync(before);
        AssertNoQteBackupArtifacts();
    }

    [Fact]
    public async Task ApplyTerminalOutcomeValidatedStateChangesAsync_RawOwnerWithoutImmutableContinuation_FailsClosedAndRollsBack()
    {
        await SeedMinimalValidatedMortalStateAsync();
        await _fs.WriteFileAtomicAsync(
            "input/turn_request.json",
            new JsonObject
            {
                ["sessionId"] = "session_qte_raw_owner",
                ["requestId"] = "request_qte_raw_owner",
                ["turnNumber"] = 42,
                ["playerAction"] = "Resolve a deferred QTE outcome."
            }.ToJsonString());
        var trackedPaths = new[]
        {
            NpcCoreChangesContract.NpcCorePath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            ResourceMaterializationContract.StatePath,
            "game_state/player/experience.json",
            "output/narrative_response.json"
        };
        var before = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in trackedPaths)
            before[path] = await _fs.ReadFileBytesAsync(path);
        var service = CreateRuntimeCapableService();
        var outcome = new QteSceneService.QteTerminalOutcome
        {
            OutcomeId = "qte_raw_owner_without_continuation",
            Title = "Deferred owner outcome",
            FinalNarrative = "Исход пытается создать нового владельца ресурса.",
            GmSummary = "Raw owner must wait for immutable accepted continuation authority.",
            ResponseFragment = new JsonObject
            {
                ["response"] = "Исход пытается создать нового владельца ресурса.",
                ["UpdateNPCs"] = new JsonArray(new JsonObject
                {
                    ["initialId"] = "npcref_qte_raw_owner",
                    ["resourceMaterialization"] = new JsonObject
                    {
                        ["resources"] = new JsonArray(new JsonObject
                        {
                            ["resourceKey"] = "health",
                            ["maximum"] = 10
                        })
                    }
                })
            }
        };

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.ApplyTerminalOutcomeValidatedStateChangesAsync(
                outcome,
                allowPreexistingStateIssues: true));

        Assert.Contains(
            "validated common-plan binding",
            exception.Message,
            StringComparison.Ordinal);
        foreach (var (path, expected) in before)
            Assert.Equal(expected, await _fs.ReadFileBytesAsync(path));
        AssertNoQteBackupArtifacts();
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
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Mortal World"
        }
        """);
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
        var emptySiblingDirectory = _fs.ResolvePath($"{QteNormalizerBackupDirectory}/empty_sibling_run");
        Directory.CreateDirectory(emptySiblingDirectory);
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
        Assert.Equal(2, runDirectories.Length);
        Assert.Contains(siblingDirectory, runDirectories);
        Assert.Contains(emptySiblingDirectory, runDirectories);
        Assert.Empty(Directory.EnumerateFileSystemEntries(emptySiblingDirectory));
    }

    private QteSceneService CreateRuntimeCapableService(
        IConsoleInputSource? inputSource = null,
        QteSceneServiceHooks? hooks = null,
        StateManagerHooks? stateManagerHooks = null,
        ILogger<QteSceneService>? logger = null)
        => CreateRuntimeCapableService(
            _fs,
            inputSource,
            hooks,
            stateManagerHooks,
            logger);

    private static QteSceneService CreateRuntimeCapableService(
        FileSystemManager fs,
        IConsoleInputSource? inputSource = null,
        QteSceneServiceHooks? hooks = null,
        StateManagerHooks? stateManagerHooks = null,
        ILogger<QteSceneService>? logger = null)
    {
        var settings = new GameSettings();
        var stateManager = new StateManager(
            fs,
            settings,
            NullLogger<StateManager>.Instance,
            stateManagerHooks);
        return new QteSceneService(
            fs,
            settings,
            null!,
            new ImageService(fs, settings, new LocalizationManager { CurrentLanguage = "ru" }, NullLogger<ImageService>.Instance),
            new AudioService(fs, settings, NullLogger<AudioService>.Instance),
            new StateDistributor(fs, NullLogger<StateDistributor>.Instance),
            new ValidationService(fs, NullLogger<ValidationService>.Instance),
            new CanonicalStateNormalizer(fs, NullLogger<CanonicalStateNormalizer>.Instance),
            stateManager,
            logger ?? NullLogger<QteSceneService>.Instance,
            inputSource,
            hooks);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly List<string> _messages = new();

        internal IReadOnlyList<string> Messages
        {
            get
            {
                lock (_messages)
                    return _messages.ToArray();
            }
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (_messages)
                _messages.Add(formatter(state, exception));
        }
    }

    private async Task<SaveLoadService> CreateSaveLoadServiceAsync()
    {
        await SeedPristineResourceStateAsync();
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

    private async Task<JsonDocument> ReadJsonDocumentAsync(string relativePath)
    {
        var json = await _fs.ReadFileAsync(relativePath);
        Assert.False(string.IsNullOrWhiteSpace(json));
        return JsonDocument.Parse(json!);
    }

    private async Task SeedMinimalValidatedMortalStateAsync()
    {
        await SeedPristineResourceStateAsync();

        await _fs.WriteFileAtomicAsync(
            MortalItemIdentityState.StatePath,
            MortalItemIdentityState.CreateEmptyRoot().ToJsonString());

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая душа",
          "currentIncarnation": 0,
          "currentRealm": "Mortal World"
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/core/player_status.json", """
        {
          "currentCondition": "Собран",
          "money": 0
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/abode_power_journal.json", """
        {
          "entries": []
        }
        """);
    }

    private async Task SeedPristineResourceStateAsync()
    {
        var resources = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(resources.IsValid, string.Join("; ", resources.Issues.Select(issue => issue.Code)));

        await _fs.WriteFileAtomicAsync(
            ResourceMaterializationContract.DefinitionsPath,
            resources.Definitions!.ToCanonicalJson());
        await _fs.WriteFileAtomicAsync(
            ResourceMaterializationContract.StatePath,
            resources.State!.ToCanonicalJson());
        await _fs.WriteFileAtomicAsync(
            ResourceMaterializationContract.HistoryPath,
            resources.History!.ToCanonicalJson());
        var authority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
            resources.Definitions,
            _fs.ReadFileAsync,
            resources.State,
            resources.History,
            CanonicalResourceOwnerAuthorityPurpose.FinalAfterImage);
        Assert.True(authority.IsValid, string.Join("; ", authority.Issues.Select(issue => issue.Code)));
        await _fs.WriteFileAtomicAsync(
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            authority.CanonicalAuthorityJson!);
    }

    private async Task<ResourceStateEntry> SeedMortalPlayerResourceQuartetAsync()
    {
        var resources = ResourceBootstrapStateBuilder.BuildMortalPlayer(
            incarnationNumber: 1,
            turn: 1,
            permanentStrength: 20,
            permanentConstitution: 20,
            permanentIntelligence: 20,
            permanentWisdom: 20,
            permanentFaith: 20);
        Assert.True(resources.IsValid, string.Join("; ", resources.Issues.Select(issue => issue.Code)));
        await _fs.WriteFileAtomicAsync(
            ResourceMaterializationContract.DefinitionsPath,
            resources.Definitions!.ToCanonicalJson());
        await _fs.WriteFileAtomicAsync(
            ResourceMaterializationContract.StatePath,
            resources.State!.ToCanonicalJson());
        await _fs.WriteFileAtomicAsync(
            ResourceMaterializationContract.HistoryPath,
            resources.History!.ToCanonicalJson());

        var authority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
            resources.Definitions,
            path => _fs.ReadFileAsync(path),
            resources.State,
            resources.History,
            CanonicalResourceOwnerAuthorityPurpose.FinalAfterImage);
        Assert.True(authority.IsValid, string.Join("; ", authority.Issues.Select(issue => issue.Code)));
        await _fs.WriteFileAtomicAsync(
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            authority.CanonicalAuthorityJson!);

        return resources.State.Entries.Single(entry =>
            entry.Coordinate.OwnerKind == ResourceOwnerKind.Player &&
            string.Equals(entry.Coordinate.ResourceOwnerId, "player_current", StringComparison.Ordinal) &&
            string.Equals(entry.Coordinate.ResourceKey, "poise", StringComparison.Ordinal));
    }

    private async Task<(ResourceStateLedger State, ResourceHistoryState History)>
        ReadResourceQuartetAsync()
    {
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            await _fs.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath),
            allowMissingPristine: false);
        Assert.True(definitions.IsValid, string.Join("; ", definitions.Issues.Select(issue => issue.Code)));
        var state = ResourceStateContract.ParseCanonical(
            await _fs.ReadFileAsync(ResourceMaterializationContract.StatePath),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.True(state.IsValid, string.Join("; ", state.Issues.Select(issue => issue.Code)));
        var history = ResourceHistoryState.ParseCanonical(
            await _fs.ReadFileAsync(ResourceMaterializationContract.HistoryPath),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.True(history.IsValid, string.Join("; ", history.Issues.Select(issue => issue.Code)));
        return (state.Ledger!, history.History!);
    }

    private async Task<QteTerminalResourceFilePlan> BuildQteTerminalResourcePlanAsync(
        int commandCount)
    {
        var resourceChanges = new JsonArray();
        for (var index = 0; index < commandCount; index++)
        {
            resourceChanges.Add(new JsonObject
            {
                ["operation"] = "damage",
                ["target"] = new JsonObject
                {
                    ["kind"] = "player",
                    ["targetId"] = "player_current"
                },
                ["resourceKey"] = "poise",
                ["amount"] = 1,
                ["source"] = new JsonObject
                {
                    ["kind"] = "narrative_outcome"
                },
                ["eventRef"] = $"turn_12:qte_terminal:1:resource:{index + 1}",
                ["reason"] = "Linear QTE resource adapter work regression."
            });
        }

        var selection = new QteTerminalResourceSelection(
            sourceTurn: 12,
            qteId: "qte_resource_linear_work",
            chapterId: "impact",
            actionId: "brace",
            grade: "fail",
            outcomeOrdinal: 1,
            outcomeId: "crushing",
            responseFragment: new JsonObject
            {
                ["response"] = "Каменная волна сбивает равновесие.",
                ["resourceChanges"] = resourceChanges
            });
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        return await QteTerminalResourceOutcome.BuildAsync(
            _fs,
            writeLease,
            selection);
    }

    private static void AssertQteResourceWork(
        QteTerminalResourceFilePlan plan,
        int commandCount,
        int baselineHistoryCount)
    {
        var work = plan.Work;

        Assert.Equal(1, work.DefinitionAuthorityLookupCount);
        Assert.Equal(commandCount, work.DefinitionBindingLookupCount);
        Assert.Equal(
            baselineHistoryCount,
            work.ExistingReplayTransitionVisitCount);
        Assert.Equal(
            commandCount,
            work.ExistingReplayIdentityLookupCount);
        Assert.Equal(
            commandCount,
            work.ProjectionTransitionVisitCount);
        Assert.Equal(
            commandCount,
            work.ProjectionIdentityLookupCount);
        Assert.Equal(
            commandCount,
            work.ProjectionCardinalityLookupCount);
        Assert.Equal(
            baselineHistoryCount + 5L * commandCount + 1L,
            work.TotalWorkUnits);
    }

    private async Task<Dictionary<string, byte[]?>> CaptureResourceQuartetBytesAsync()
    {
        var paths = new[]
        {
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath
        };
        var result = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in paths)
            result[path] = await _fs.ReadFileBytesAsync(path);
        return result;
    }

    private async Task AssertResourceQuartetBytesAsync(
        IReadOnlyDictionary<string, byte[]?> expected)
    {
        foreach (var path in new[]
                 {
                     ResourceMaterializationContract.DefinitionsPath,
                     ResourceMaterializationContract.StatePath,
                     ResourceMaterializationContract.HistoryPath,
                     CanonicalResourceOwnerAuthorityComposer.AuthorityPath
                 })
        {
            Assert.Equal(expected[path], await _fs.ReadFileBytesAsync(path));
        }
    }

    private async Task<Dictionary<string, byte[]?>> CaptureTrackedBytesAsync(
        IEnumerable<string> paths)
    {
        var result = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            result[path] = await _fs.ReadFileBytesAsync(path);
        return result;
    }

    private async Task AssertTrackedBytesAsync(
        IReadOnlyDictionary<string, byte[]?> expected,
        params string[] excludedPaths)
    {
        var exclusions = excludedPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (path, bytes) in expected)
        {
            if (exclusions.Contains(path))
                continue;
            Assert.Equal(bytes, await _fs.ReadFileBytesAsync(path));
        }
    }

    private async Task AssertTerminalSelectedContinuationAndRuntimeAsync()
    {
        using var continuation = await ReadJsonDocumentAsync(
            QteDeferredEffectContinuation.StatePath);
        var continuationRoot = continuation.RootElement;
        Assert.Equal(
            "terminal_selected",
            continuationRoot.GetProperty("state").GetString());
        Assert.Equal(
            JsonValueKind.Object,
            continuationRoot.GetProperty("selectedTerminalBinding").ValueKind);

        using var runtime = await ReadJsonDocumentAsync(QteSceneService.QteRuntimePath);
        var activeScene = runtime.RootElement.GetProperty("activeScene");
        Assert.Equal(JsonValueKind.Object, activeScene.ValueKind);
        Assert.Equal(
            "terminal_selected",
            activeScene.GetProperty("effectResolutionState").GetString());
        Assert.Equal(
            continuationRoot.GetProperty("continuationId").GetString(),
            activeScene.GetProperty("deferredEffectContinuationId").GetString());
        Assert.Equal(
            continuationRoot.GetProperty("authorityFingerprint").GetString(),
            activeScene.GetProperty("deferredEffectContinuationFingerprint").GetString());
    }

    private static void AssertMetricValue(JsonElement scoreContainer, string metricId, double expectedValue)
    {
        var metrics = scoreContainer.TryGetProperty("metrics", out var metricsElement)
            ? metricsElement
            : scoreContainer;
        Assert.Equal(JsonValueKind.Array, metrics.ValueKind);

        var metric = metrics.EnumerateArray().Single(item =>
            string.Equals(item.GetProperty("id").GetString(), metricId, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(expectedValue, metric.GetProperty("value").GetDouble());
    }

    private static QteSceneService.QteOffer BuildScoredBranchChoiceOffer()
    {
        var json = """
        {
          "qteId": "qte_scored_manor_runtime",
          "title": "Тихое проникновение",
          "offerText": "Нужно пройти двор, собрать улики и уйти до тревоги.",
          "introNarrative": "Фонари качаются над мокрым двором усадьбы.",
          "startChapterId": "yard",
          "sourceTurnNumber": 10,
          "scoreModel": {
            "metrics": [
              { "id": "stealth", "label": "Скрытность", "initial": 50, "min": 0, "max": 100, "visibility": "always" },
              { "id": "alarm", "label": "Тревога", "initial": 10, "min": 0, "max": 100, "visibility": "always" },
              { "id": "evidence", "label": "Улики", "initial": 0, "min": 0, "max": 100, "visibility": "final" }
            ],
            "rankOrder": ["best", "good", "partial", "bad"],
            "ranks": [
              {
                "id": "best",
                "label": "Безупречный исход",
                "summary": "Усадьба осталась спокойной, а улики собраны чисто.",
                "allOf": [
                  { "metric": "stealth", "op": ">=", "value": 85 },
                  { "metric": "alarm", "op": "<=", "value": 20 },
                  { "metric": "evidence", "op": ">=", "value": 40 }
                ]
              },
              {
                "id": "good",
                "label": "Удачный исход",
                "summary": "Цель достигнута, тревога осталась управляемой.",
                "allOf": [
                  { "metric": "stealth", "op": ">=", "value": 55 },
                  { "metric": "alarm", "op": "<=", "value": 40 },
                  { "metric": "evidence", "op": ">=", "value": 30 }
                ]
              },
              {
                "id": "partial",
                "label": "Неровный исход",
                "summary": "Победа есть, но следы заметны.",
                "allOf": [
                  { "metric": "stealth", "op": ">=", "value": 20 }
                ]
              },
              {
                "id": "bad",
                "label": "Провальный исход",
                "summary": "Сцена завершилась тяжёлыми последствиями.",
                "fallback": true
              }
            ]
          },
          "chapters": [
            {
              "chapterId": "yard",
              "title": "Двор",
              "narrative": "Патруль разворачивается у ворот.",
              "actions": [
                {
                  "actionId": "cross_yard",
                  "label": "Пройти между фонарями",
                  "check": {
                    "type": "BranchChoice",
                    "baseDifficulty": 2,
                    "primaryCharacteristic": "dexterity",
                    "config": { "choiceGrade": "success" }
                  },
                  "scoreDeltas": {
                    "success": [
                      { "metric": "stealth", "delta": 75 },
                      { "metric": "alarm", "delta": -20 }
                    ]
                  },
                  "routing": {
                    "success": { "nextChapterId": "study" },
                    "partial": { "nextChapterId": "study" },
                    "fail": { "nextChapterId": "study" }
                  }
                }
              ]
            },
            {
              "chapterId": "study",
              "title": "Кабинет",
              "narrative": "В кабинете пахнет мокрой бумагой.",
              "actions": [
                {
                  "actionId": "search_study",
                  "label": "Обыскать стол",
                  "check": {
                    "type": "BranchChoice",
                    "baseDifficulty": 3,
                    "primaryCharacteristic": "perception",
                    "config": { "choiceGrade": "partial" }
                  },
                  "scoreDeltas": {
                    "partial": [
                      { "metric": "stealth", "delta": -10 },
                      { "metric": "evidence", "delta": 12 }
                    ]
                  },
                  "routing": {
                    "success": { "nextChapterId": "roof" },
                    "partial": { "nextChapterId": "roof" },
                    "fail": { "nextChapterId": "roof" }
                  }
                }
              ]
            },
            {
              "chapterId": "roof",
              "title": "Крыша",
              "narrative": "Над крышей уже слышны шаги.",
              "actions": [
                {
                  "actionId": "escape_roof",
                  "label": "Уйти по крыше",
                  "check": {
                    "type": "BranchChoice",
                    "baseDifficulty": 4,
                    "primaryCharacteristic": "speed",
                    "config": { "choiceGrade": "fail" }
                  },
                  "scoreDeltas": {
                    "fail": [
                      { "metric": "stealth", "delta": -25 },
                      { "metric": "alarm", "delta": 35 },
                      { "metric": "evidence", "delta": 25 }
                    ]
                  },
                  "routing": {
                    "success": { "terminalOutcomeId": "escaped" },
                    "partial": { "terminalOutcomeId": "escaped" },
                    "fail": { "terminalOutcomeId": "escaped" }
                  }
                }
              ]
            }
          ],
          "terminalOutcomes": [
            {
              "outcomeId": "escaped",
              "title": "Уход с крыши",
              "finalNarrative": "Вы уходите по мокрой черепице.",
              "gmSummary": "Игрок завершил scored QTE.",
              "responseFragment": {
                "response": "Вы уходите из усадьбы с уликами.",
                "experienceGained": 25
              }
            }
          ]
        }
        """;

        return JsonSerializer.Deserialize<QteSceneService.QteOffer>(json)!;
    }

    private static QteSceneService.QteOffer BuildUnscoredBranchChoiceOffer()
    {
        return new QteSceneService.QteOffer
        {
            QteId = "qte_unscored_gate",
            Title = "Старые ворота",
            OfferText = "Нужно открыть ворота.",
            IntroNarrative = "Засов заедает от ржавчины.",
            StartChapterId = "gate",
            SourceTurnNumber = 20,
            Chapters =
            [
                new QteSceneService.QteChapter
                {
                    ChapterId = "gate",
                    Title = "Ворота",
                    Narrative = "Ворота поддаются с трудом.",
                    Actions =
                    [
                        new QteSceneService.QteAction
                        {
                            ActionId = "open_gate",
                            Label = "Открыть ворота",
                            Check = new QteSceneService.QteCheck
                            {
                                Type = "BranchChoice",
                                BaseDifficulty = 1,
                                PrimaryCharacteristic = Characteristics.Strength,
                                Config = new JsonObject { ["choiceGrade"] = "success" }
                            },
                            Routing = new QteSceneService.QteRouting
                            {
                                Success = new QteSceneService.QteBranchTarget { TerminalOutcomeId = "gate_open" },
                                Partial = new QteSceneService.QteBranchTarget { TerminalOutcomeId = "gate_open" },
                                Fail = new QteSceneService.QteBranchTarget { TerminalOutcomeId = "gate_open" }
                            }
                        }
                    ]
                }
            ],
            TerminalOutcomes =
            [
                new QteSceneService.QteTerminalOutcome
                {
                    OutcomeId = "gate_open",
                    Title = "Ворота открыты",
                    FinalNarrative = "Проход свободен.",
                    GmSummary = "Игрок открыл обычную QTE-сцену.",
                    ResponseFragment = JsonNode.Parse("""
                    {
                      "response": "Ворота открываются.",
                      "experienceGained": 5
                    }
                    """)!.AsObject()
                }
            ]
        };
    }

    private static QteSceneService.QteOffer BuildResourceTerminalOffer(string selectedGrade)
    {
        JsonObject Fragment(int outcomeOrdinal, int amount) => new()
        {
            ["response"] = "Каменная волна сбивает равновесие.",
            ["experienceGained"] = outcomeOrdinal == 1 ? 1 : 0,
            ["resourceChanges"] = new JsonArray
            {
                new JsonObject
                {
                    ["operation"] = "damage",
                    ["target"] = new JsonObject
                    {
                        ["kind"] = "player",
                        ["targetId"] = "player_current"
                    },
                    ["resourceKey"] = "poise",
                    ["amount"] = amount,
                    ["source"] = new JsonObject
                    {
                        ["kind"] = "narrative_outcome"
                    },
                    ["eventRef"] = $"turn_12:qte_terminal:{outcomeOrdinal}:resource:1",
                    ["reason"] = "Цена выбранного исхода QTE."
                }
            }
        };

        return new QteSceneService.QteOffer
        {
            QteId = "qte_resource_selection",
            Title = "Каменная волна",
            OfferText = "Удержаться на ногах.",
            IntroNarrative = "Пол содрогается.",
            StartChapterId = "impact",
            SourceTurnNumber = 12,
            Chapters =
            [
                new QteSceneService.QteChapter
                {
                    ChapterId = "impact",
                    Title = "Удар",
                    Narrative = "Волна достигает героя.",
                    Actions =
                    [
                        new QteSceneService.QteAction
                        {
                            ActionId = "brace",
                            Label = "Упереться",
                            Check = new QteSceneService.QteCheck
                            {
                                Type = "BranchChoice",
                                BaseDifficulty = 1,
                                Config = new JsonObject { ["choiceGrade"] = selectedGrade }
                            },
                            Routing = new QteSceneService.QteRouting
                            {
                                Success = new QteSceneService.QteBranchTarget
                                {
                                    TerminalOutcomeId = "glancing"
                                },
                                Partial = new QteSceneService.QteBranchTarget
                                {
                                    TerminalOutcomeId = "glancing"
                                },
                                Fail = new QteSceneService.QteBranchTarget
                                {
                                    TerminalOutcomeId = "crushing"
                                }
                            }
                        }
                    ]
                }
            ],
            TerminalOutcomes =
            [
                new QteSceneService.QteTerminalOutcome
                {
                    OutcomeId = "glancing",
                    Title = "Скользящий удар",
                    FinalNarrative = "Герой удержался.",
                    GmSummary = "Успешная ветка наносит 5 poise damage.",
                    ResponseFragment = Fragment(outcomeOrdinal: 1, amount: 5)
                },
                new QteSceneService.QteTerminalOutcome
                {
                    OutcomeId = "crushing",
                    Title = "Сокрушительный удар",
                    FinalNarrative = "Герой падает.",
                    GmSummary = "Провальная ветка наносит 20 poise damage.",
                    ResponseFragment = Fragment(outcomeOrdinal: 2, amount: 20)
                }
            ]
        };
    }

    private static ConsoleKeyInfo Key(ConsoleKey key)
    {
        var keyChar = key == ConsoleKey.Spacebar ? ' ' : char.ToLowerInvariant(key.ToString()[0]);
        return new ConsoleKeyInfo(keyChar, key, false, false, false);
    }

    private sealed class QueuedConsoleInputSource : IConsoleInputSource
    {
        private readonly Queue<ConsoleKeyInfo> _keys;

        public QueuedConsoleInputSource(IEnumerable<ConsoleKeyInfo> keys)
        {
            _keys = new Queue<ConsoleKeyInfo>(keys);
        }

        public bool IsScripted => true;

        public bool KeyAvailable => _keys.Count > 0;

        public ConsoleKeyInfo ReadKey(bool intercept = true) =>
            _keys.Count > 0 ? _keys.Dequeue() : Key(ConsoleKey.Enter);

        public string? ReadLine() => string.Empty;

        public void AssertCompleted()
        {
            Assert.Empty(_keys);
        }
    }

    private sealed class QueuedAnsiConsole : IAnsiConsole
    {
        private readonly StringWriter _writer = new();
        private readonly IAnsiConsole _inner;

        public QueuedAnsiConsole(IEnumerable<ConsoleKeyInfo> keys)
        {
            _inner = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.Yes,
                ColorSystem = ColorSystemSupport.Standard,
                Interactive = InteractionSupport.Yes,
                Out = new AnsiConsoleOutput(_writer)
            });
            Input = new QueuedAnsiConsoleInput(keys);
        }

        public string Output => _writer.ToString();

        public Profile Profile => _inner.Profile;

        public IAnsiConsoleCursor Cursor => _inner.Cursor;

        public IAnsiConsoleInput Input { get; }

        public RenderPipeline Pipeline => _inner.Pipeline;

        public IExclusivityMode ExclusivityMode => _inner.ExclusivityMode;

        public void Clear(bool home)
        {
        }

        public void Write(IRenderable renderable) => _inner.Write(renderable);
    }

    private sealed class QueuedAnsiConsoleInput : IAnsiConsoleInput
    {
        private readonly Queue<ConsoleKeyInfo> _keys;

        public QueuedAnsiConsoleInput(IEnumerable<ConsoleKeyInfo> keys)
        {
            _keys = new Queue<ConsoleKeyInfo>(keys);
        }

        public bool IsKeyAvailable() => _keys.Count > 0;

        public ConsoleKeyInfo? ReadKey(bool intercept) =>
            _keys.Count > 0 ? _keys.Dequeue() : Key(ConsoleKey.Enter);

        public Task<ConsoleKeyInfo?> ReadKeyAsync(bool intercept, CancellationToken cancellationToken) =>
            Task.FromResult(ReadKey(intercept));
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
            // ignored
        }
    }
}
