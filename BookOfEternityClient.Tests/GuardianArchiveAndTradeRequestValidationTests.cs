using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GuardianArchiveAndTradeRequestValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public GuardianArchiveAndTradeRequestValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-guardian-request-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        CopyDirectory(TestRepoPaths.BaseSessionRoot, Path.Combine(_rootPath, "game_session"));

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateGameStateAsync_AcceptedArchiveConsultation_AllowsReceiptBeforeUiCleanup()
    {
        const string requestId = "archive_consult_test_001";
        const string archiveId = "archive_lore_001";
        const string guardianId = "guardian_archive_001";

        var request = new
        {
            requestId,
            guardianId,
            guardianName = "Азалия",
            archiveId,
            archiveTitle = "Летопись Серого Двора",
            archiveEntryType = "lore_fragment",
            archiveRarity = "Rare",
            archiveSourceKind = "codex",
            targetIncarnation = 2,
            createdAtTurn = 12,
            createdAtUtc = "2026-03-26T00:00:00Z",
            requestedMode = "consultation"
        };

        await WriteJsonAsync(AfterlifeArchiveActionState.ConsultationRequestPath, request);
        await WriteJsonAsync(
            "game_state/meta/soul_state.json",
            new
            {
                currentRealm = "Chaos Sea",
                currentIncarnation = 1,
                afterlifeArchive = new
                {
                    stored = Array.Empty<object>(),
                    actionReceipts = new[]
                    {
                        new
                        {
                            requestId,
                            archiveId,
                            requestedMode = "consultation",
                            status = "accepted",
                            guardianId,
                            guardianName = "Азалия",
                            guaranteedArchiveQuestCount = 1,
                            questHookCount = 0,
                            specialQuestLineUnlocks = 0,
                            visibleRivalClueBonus = 0,
                            archiveWarningTierBonus = 0,
                            targetProjectId = "",
                            reason = "",
                            resolvedAtTurn = 12,
                            resolvedAtUtc = "2026-03-26T00:01:00Z"
                        }
                    }
                }
            });

        await WriteJsonAsync(
            GuardianProjectState.TrackerPath,
            new
            {
                activeProjects = Array.Empty<object>(),
                completedProjects = new[]
                {
                    new
                    {
                        guardianId,
                        project = new
                        {
                            projectId = "archive_consult_project",
                            projectType = "lore_research",
                            projectOrigin = "archive_consultation",
                            projectTier = "minor",
                            projectMode = "supportive",
                            projectName = "Архивная консультация: Летопись Серого Двора",
                            finalState = "Completed",
                            completionTurn = 12,
                            consultationRequestId = requestId,
                            consultationArchiveId = archiveId,
                            projectOutcomeAudit = new
                            {
                                bonusLoreUnlocks = 0,
                                questHookCount = 0,
                                guaranteedArchiveQuestCount = 1,
                                specialQuestLineUnlocks = 0,
                                visibleRivalClueBonus = 0,
                                unlockedLoreFragments = Array.Empty<object>()
                            },
                            effectState = new
                            {
                                targetIncarnation = 2,
                                bonusLoreUnlocksApplied = 0,
                                questHookTokensGranted = 0,
                                questHookTokensSpent = 0,
                                guaranteedArchiveQuestGranted = 1,
                                guaranteedArchiveQuestSpawned = 0,
                                guaranteedArchiveQuestConsumed = 0,
                                specialQuestLineTokensGranted = 0,
                                specialQuestLineTokensSpent = 0,
                                visibleRivalClueBudgetGranted = 0,
                                visibleRivalClueBudgetSpent = 0
                            }
                        }
                    }
                },
                temporaryProjectModifiers = Array.Empty<object>()
            });

        await _fs.WriteFileAtomicAsync(
            "game_state/meta/guardians.json",
            await File.ReadAllTextAsync(Path.Combine(
                TestRepoPaths.RepoRoot,
                "FileSystemExample",
                "validator_fixtures",
                "guardian_archive_consultation_guaranteed_quest_contract",
                "fixed",
                "guardians.json")));

        await WriteJsonAsync(
            "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json",
            new
            {
                currentRealm = "Chaos Sea",
                currentIncarnation = 1
            });

        const string backupPath = "game_state/control/pending_turn_snapshot/pre_pending_archive_consultation_request.json";
        await WriteJsonAsync(backupPath, request);
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [AfterlifeArchiveActionState.ConsultationRequestPath] = backupPath
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "archive_consultation_request_missing_reservation", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "archive_consultation_request_missing_resolution", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "archive_consultation_request_missing_canonical_result", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_AcceptedArchiveConsultation_RequiresMachineReadableOutcome()
    {
        const string requestId = "archive_consult_test_missing_outcome";
        const string archiveId = "archive_lore_missing_outcome";
        const string guardianId = "guardian_archive_001";

        var request = new
        {
            requestId,
            guardianId,
            guardianName = "Азалия",
            archiveId,
            archiveTitle = "Летопись Пепельного Предела",
            archiveEntryType = "lore_fragment",
            archiveRarity = "Rare",
            archiveSourceKind = "codex",
            targetIncarnation = 2,
            createdAtTurn = 12,
            createdAtUtc = "2026-03-26T00:00:00Z",
            requestedMode = "consultation"
        };

        await WriteJsonAsync(AfterlifeArchiveActionState.ConsultationRequestPath, request);
        await WriteJsonAsync(
            "game_state/meta/soul_state.json",
            new
            {
                currentRealm = "Chaos Sea",
                currentIncarnation = 1,
                afterlifeArchive = new
                {
                    stored = Array.Empty<object>(),
                    actionReceipts = new[]
                    {
                        new
                        {
                            requestId,
                            archiveId,
                            requestedMode = "consultation",
                            status = "accepted",
                            guardianId,
                            guardianName = "Азалия",
                            resolvedAtTurn = 12,
                            resolvedAtUtc = "2026-03-26T00:01:00Z"
                        }
                    }
                }
            });

        await WriteJsonAsync(
            GuardianProjectState.TrackerPath,
            new
            {
                activeProjects = Array.Empty<object>(),
                completedProjects = new[]
                {
                    new
                    {
                        guardianId,
                        project = new
                        {
                            projectId = "archive_consult_project_missing_outcome",
                            projectType = "lore_research",
                            projectOrigin = "archive_consultation",
                            projectTier = "minor",
                            projectMode = "supportive",
                            projectName = "Архивная консультация: Летопись Пепельного Предела",
                            finalState = "Completed",
                            completionTurn = 12,
                            consultationRequestId = requestId,
                            consultationArchiveId = archiveId,
                            projectOutcomeAudit = new
                            {
                                bonusLoreUnlocks = 0,
                                questHookCount = 0,
                                guaranteedArchiveQuestCount = 1,
                                specialQuestLineUnlocks = 0,
                                visibleRivalClueBonus = 0,
                                unlockedLoreFragments = Array.Empty<object>()
                            },
                            effectState = new
                            {
                                targetIncarnation = 2,
                                bonusLoreUnlocksApplied = 0,
                                questHookTokensGranted = 0,
                                questHookTokensSpent = 0,
                                guaranteedArchiveQuestGranted = 1,
                                guaranteedArchiveQuestSpawned = 0,
                                guaranteedArchiveQuestConsumed = 0,
                                specialQuestLineTokensGranted = 0,
                                specialQuestLineTokensSpent = 0,
                                visibleRivalClueBudgetGranted = 0,
                                visibleRivalClueBudgetSpent = 0
                            }
                        }
                    }
                },
                temporaryProjectModifiers = Array.Empty<object>()
            });

        await _fs.WriteFileAtomicAsync(
            "game_state/meta/guardians.json",
            await File.ReadAllTextAsync(Path.Combine(
                TestRepoPaths.RepoRoot,
                "FileSystemExample",
                "validator_fixtures",
                "guardian_archive_consultation_guaranteed_quest_contract",
                "fixed",
                "guardians.json")));

        await WriteJsonAsync(
            "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json",
            new
            {
                currentRealm = "Chaos Sea",
                currentIncarnation = 1
            });

        const string backupPath = "game_state/control/pending_turn_snapshot/pre_pending_archive_consultation_request.json";
        await WriteJsonAsync(backupPath, request);
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [AfterlifeArchiveActionState.ConsultationRequestPath] = backupPath
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "afterlife_archive_consultation_receipt_missing_outcome", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PreTurnTradeRequestWithoutInventory_FailsResolutionContract()
    {
        var request = new
        {
            requestId = "guardian_trade_test_001",
            guardianId = "guardian_alpha",
            guardianName = "Азалия",
            abodeId = "abode_alpha",
            returnCycleId = "return_1",
            currentReputation = 110,
            derivedTradeSlotCount = 4,
            effectiveRarityCeilingBonusSteps = 0,
            projectBonusSignature = "0|0|0",
            createdAtUtc = "2026-03-26T00:00:00Z"
        };

        await WriteJsonAsync(GuardianTradeRequestState.PendingRequestPath, request);
        await WriteJsonAsync(
            "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json",
            new
            {
                currentRealm = "Chaos Sea",
                currentIncarnation = 1
            });

        const string backupPath = "game_state/control/pending_turn_snapshot/pre_pending_guardian_trade_request.json";
        await WriteJsonAsync(backupPath, request);
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GuardianTradeRequestState.PendingRequestPath] = backupPath
        });

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "domain": "Порог Сна",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 110, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 10, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель" },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_alpha",
            "canonicalName": "Азалия",
            "domain": "Порог Сна",
            "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Тестовая форма."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": 110, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 10, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель" },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          },
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_alpha"
          }
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "guardian_trade_request_missing_inventory_resolution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_AcceptedArchiveProjectFuel_RequiresMachineReadableOutcome()
    {
        const string requestId = "archive_fuel_test_001";
        const string archiveId = "archive_secret_001";
        const string guardianId = "guardian_archive_001";
        const string projectId = "project_pressure_001";

        var request = new
        {
            requestId,
            guardianId,
            guardianName = "Азалия",
            archiveId,
            archiveTitle = "Секрет старого договора",
            archiveEntryType = "secret_record",
            archiveRarity = "Epic",
            archiveSourceKind = "codex",
            targetProjectId = projectId,
            targetProjectName = "Нить тайного двора",
            createdAtTurn = 12,
            createdAtUtc = "2026-03-26T00:00:00Z",
            requestedMode = "project_fuel"
        };

        await WriteJsonAsync(AfterlifeArchiveActionState.ProjectFuelRequestPath, request);
        await WriteJsonAsync(
            "game_state/meta/soul_state.json",
            new
            {
                currentRealm = "Chaos Sea",
                currentIncarnation = 1,
                afterlifeArchive = new
                {
                    stored = Array.Empty<object>(),
                    actionReceipts = new[]
                    {
                        new
                        {
                            requestId,
                            archiveId,
                            requestedMode = "project_fuel",
                            status = "accepted",
                            guardianId,
                            guardianName = "Азалия",
                            targetProjectId = projectId,
                            resolvedAtTurn = 12,
                            resolvedAtUtc = "2026-03-26T00:01:00Z"
                        }
                    }
                }
            });

        await WriteJsonAsync(
            GuardianProjectState.JournalPath,
            new
            {
                entries = new[]
                {
                    new
                    {
                        entryId = "journal_fuel_001",
                        turn = 12,
                        guardianId,
                        projectId,
                        eventType = "assisted",
                        visibility = "player_known",
                        archiveFuelRequestId = requestId,
                        title = "Проект усилен архивной записью",
                        summary = "Хранитель ослабил давление на проект.",
                        details = new[]
                        {
                            "Проект: Нить тайного двора",
                            "Pressure: 9 -> 4",
                            $"ArchiveId: {archiveId}"
                        }
                    }
                }
            });

        await WriteJsonAsync(
            "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json",
            new
            {
                currentRealm = "Chaos Sea",
                currentIncarnation = 1
            });

        const string backupPath = "game_state/control/pending_turn_snapshot/pre_pending_archive_project_fuel_request.json";
        await WriteJsonAsync(backupPath, request);
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [AfterlifeArchiveActionState.ProjectFuelRequestPath] = backupPath
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "afterlife_archive_project_fuel_receipt_invalid_result_mode", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "afterlife_archive_project_fuel_receipt_invalid_result_amount", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_GuardianAvailableQuestWithoutQuestId_FailsHardIdentityContract()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "domain": "Порог Сна",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 110, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель" },
              "questManagement": {
                "availableQuests": [
                  {
                    "title": "След незавершённого имени",
                    "description": "Квест без canonical questId.",
                    "status": "available",
                    "difficulty": "normal"
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
            "domain": "Порог Сна",
            "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Тестовая форма."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": 110, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель" },
            "questManagement": {
              "availableQuests": [
                {
                  "title": "След незавершённого имени",
                  "description": "Квест без canonical questId.",
                  "status": "available",
                  "difficulty": "normal"
                }
              ],
              "activeQuests": [],
              "completedQuests": []
            },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          },
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_alpha"
          }
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "guardian_live_quest_missing_quest_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_GuardianActiveQuestWithoutQuestId_FailsHardIdentityContract()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "domain": "Порог Сна",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 110, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель" },
              "questManagement": {
                "availableQuests": [],
                "activeQuests": [
                  {
                    "title": "Нить молчащего долга",
                    "description": "Активный квест без canonical questId.",
                    "status": "active",
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
            "domain": "Порог Сна",
            "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Тестовая форма."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": 110, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель" },
            "questManagement": {
              "availableQuests": [],
              "activeQuests": [
                {
                  "title": "Нить молчащего долга",
                  "description": "Активный квест без canonical questId.",
                  "status": "active",
                  "difficulty": "hard"
                }
              ],
              "completedQuests": []
            },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          },
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_alpha"
          }
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "guardian_live_quest_missing_quest_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_HostileDirectRivalArc_AllowsLinkedWorldEventAsSecondVisibleClue()
    {
        await SeedRivalArcGuardiansAsync();
        await WriteJsonAsync(RivalSoulArcService.StatePath, new
        {
            arcs = new object[]
            {
                new
                {
                    arcId = "arc_hunt_001",
                    scope = "major",
                    arcType = "hostile_hunt",
                    status = "intersecting",
                    sponsorGuardianRef = new { mode = "guardianId", guardianId = "guard_social_azalia_001", displayName = "Азалия" },
                    rivalSoul = new { rivalSoulId = "rival_001", displayNameOrMoniker = "Алый Палач", roleSummary = "Охотник", isKnownToPlayer = true },
                    objective = "Найти и убить игрока",
                    playerIntersection = new { targetsPlayerDirectly = true, stakes = "Смертельная угроза", canBecomeSoulQuest = true, recommendedCounterQuestTone = "urgent" },
                    milestones = new[] { new { stage = 0, title = "Слух", summary = "О нём говорят.", visibleToPlayer = true } },
                    currentStage = 1,
                    publicSignals = new[]
                    {
                        new { signalId = "signal_001", stage = 1, description = "На рынке говорят о следопыте.", source = "rumor", visibleToPlayer = true }
                    },
                    resolution = new { outcome = "ongoing", notes = "" }
                }
            }
        });
        await WriteJsonAsync("game_state/world/world_events.json", new
        {
            worldEventsLog = new[]
            {
                new
                {
                    eventId = "event_001",
                    title = "Кровавый знак на воротах",
                    description = "Горожане нашли предупреждение охотника.",
                    relatedRivalArcId = "arc_hunt_001",
                    visibility = "Public"
                }
            }
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "rival_arc_hostile_direct_target_needs_two_visible_signals", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_HostileDirectRivalArc_AllowsPlayerKnownWorldEventAsSecondVisibleClue()
    {
        await SeedRivalArcGuardiansAsync();
        await WriteJsonAsync(RivalSoulArcService.StatePath, new
        {
            arcs = new object[]
            {
                new
                {
                    arcId = "arc_hunt_player_known_001",
                    scope = "major",
                    arcType = "hostile_hunt",
                    status = "intersecting",
                    sponsorGuardianRef = new { mode = "guardianId", guardianId = "guard_social_azalia_001", displayName = "Азалия" },
                    rivalSoul = new { rivalSoulId = "rival_player_known_001", displayNameOrMoniker = "Серый Преследователь", roleSummary = "Охотник", isKnownToPlayer = true },
                    objective = "Найти игрока",
                    playerIntersection = new { targetsPlayerDirectly = true, stakes = "Смертельная угроза", canBecomeSoulQuest = true, recommendedCounterQuestTone = "urgent" },
                    milestones = new[] { new { stage = 0, title = "Слух", summary = "О нём говорят.", visibleToPlayer = true } },
                    currentStage = 1,
                    publicSignals = new[]
                    {
                        new { signalId = "signal_player_known_001", stage = 1, description = "Игрок выведал имя охотника.", source = "broker", visibleToPlayer = true }
                    },
                    resolution = new { outcome = "ongoing", notes = "" }
                }
            }
        });
        await WriteJsonAsync("game_state/world/world_events.json", new
        {
            worldEventsLog = new[]
            {
                new
                {
                    eventId = "event_player_known_001",
                    title = "Секретный приказ охотника",
                    description = "Игрок добыл сведения о тайном приказе преследователя.",
                    relatedRivalArcId = "arc_hunt_player_known_001",
                    visibility = "player_known"
                }
            }
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "rival_arc_hostile_direct_target_needs_two_visible_signals", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RivalPublicSignalWithoutVisibleToPlayer_FailsStrictVisibilityContract()
    {
        await SeedRivalArcGuardiansAsync();
        await WriteJsonAsync(RivalSoulArcService.StatePath, new
        {
            arcs = new object[]
            {
                new
                {
                    arcId = "arc_claim_001",
                    scope = "minor",
                    arcType = "political_claim",
                    status = "rising",
                    sponsorGuardianRef = new { mode = "guardianId", guardianId = "guard_social_azalia_001", displayName = "Азалия" },
                    rivalSoul = new { rivalSoulId = "rival_002", displayNameOrMoniker = "Тайный Претендент", roleSummary = "Претендент", isKnownToPlayer = false },
                    objective = "Захватить власть в городе",
                    playerIntersection = new { targetsPlayerDirectly = false, stakes = "Городская власть", canBecomeSoulQuest = false, recommendedCounterQuestTone = "political" },
                    milestones = new[] { new { stage = 0, title = "Шёпот двора", summary = "О нём тихо говорят.", visibleToPlayer = true } },
                    currentStage = 0,
                    publicSignals = new[]
                    {
                        new { signalId = "signal_missing_visibility", stage = 0, description = "Странные слухи без явной видимости.", source = "rumor" }
                    },
                    resolution = new { outcome = "ongoing", notes = "" }
                }
            }
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "missing_required_boolean_field", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("publicSignals[0].visibleToPlayer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MirroredRivalSignalAndWorldEventClue_CountsOnlyOnce()
    {
        await SeedRivalArcGuardiansAsync();
        await WriteJsonAsync(RivalSoulArcService.StatePath, new
        {
            arcs = new object[]
            {
                new
                {
                    arcId = "arc_hunt_002",
                    scope = "major",
                    arcType = "hostile_hunt",
                    status = "intersecting",
                    sponsorGuardianRef = new { mode = "guardianId", guardianId = "guard_social_azalia_001", displayName = "Азалия" },
                    rivalSoul = new { rivalSoulId = "rival_003", displayNameOrMoniker = "Пепельный Охотник", roleSummary = "Охотник", isKnownToPlayer = true },
                    objective = "Догнать и убить игрока",
                    playerIntersection = new { targetsPlayerDirectly = true, stakes = "Смертельная угроза", canBecomeSoulQuest = true, recommendedCounterQuestTone = "urgent" },
                    milestones = new[] { new { stage = 0, title = "Знак погони", summary = "Охотник уже близко.", visibleToPlayer = true } },
                    currentStage = 1,
                    publicSignals = new[]
                    {
                        new { signalId = "signal_shared_clue", stage = 1, description = "Один и тот же след охотника.", source = "rumor", visibleToPlayer = true, bonusClueRevealId = "shared_clue" }
                    },
                    resolution = new { outcome = "ongoing", notes = "" }
                }
            }
        });
        await WriteJsonAsync("game_state/world/world_events.json", new
        {
            worldEventsLog = new[]
            {
                new
                {
                    eventId = "event_shared_clue",
                    title = "Тот же след охотника",
                    description = "Событие мира повторяет уже замеченный след.",
                    relatedRivalArcId = "arc_hunt_002",
                    visibility = "player_known",
                    bonusClueRevealId = "shared_clue"
                }
            }
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "rival_arc_hostile_direct_target_needs_two_visible_signals", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_HostileDirectRivalArc_DoesNotCountSecretWorldEventAsVisibleClue()
    {
        await SeedRivalArcGuardiansAsync();
        await WriteJsonAsync(RivalSoulArcService.StatePath, new
        {
            arcs = new object[]
            {
                new
                {
                    arcId = "arc_hunt_secret_001",
                    scope = "major",
                    arcType = "hostile_hunt",
                    status = "intersecting",
                    sponsorGuardianRef = new { mode = "guardianId", guardianId = "guard_social_azalia_001", displayName = "Азалия" },
                    rivalSoul = new { rivalSoulId = "rival_secret_001", displayNameOrMoniker = "Тёмный Гонец", roleSummary = "Охотник", isKnownToPlayer = true },
                    objective = "Найти игрока",
                    playerIntersection = new { targetsPlayerDirectly = true, stakes = "Смертельная угроза", canBecomeSoulQuest = true, recommendedCounterQuestTone = "urgent" },
                    milestones = new[] { new { stage = 0, title = "Слух", summary = "О нём говорят.", visibleToPlayer = true } },
                    currentStage = 1,
                    publicSignals = new[]
                    {
                        new { signalId = "signal_secret_001", stage = 1, description = "Есть только один явный след.", source = "rumor", visibleToPlayer = true }
                    },
                    resolution = new { outcome = "ongoing", notes = "" }
                }
            }
        });
        await WriteJsonAsync("game_state/world/world_events.json", new
        {
            worldEventsLog = new[]
            {
                new
                {
                    eventId = "event_secret_001",
                    title = "Тайный приказ гонца",
                    description = "Существует скрытый приказ, но игрок его ещё не знает.",
                    relatedRivalArcId = "arc_hunt_secret_001",
                    visibility = "Secret"
                }
            }
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "rival_arc_hostile_direct_target_needs_two_visible_signals", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RivalLinkedWorldEventWithoutVisibility_FailsExplicitly()
    {
        await SeedRivalArcGuardiansAsync();
        await WriteJsonAsync(RivalSoulArcService.StatePath, new
        {
            arcs = new object[]
            {
                new
                {
                    arcId = "arc_visibility_missing_001",
                    scope = "minor",
                    arcType = "political_claim",
                    status = "rising",
                    sponsorGuardianRef = new { mode = "guardianId", guardianId = "guard_social_azalia_001", displayName = "Азалия" },
                    rivalSoul = new { rivalSoulId = "rival_visibility_missing_001", displayNameOrMoniker = "Теневой Наследник", roleSummary = "Претендент", isKnownToPlayer = false },
                    objective = "Подготовить переворот",
                    playerIntersection = new { targetsPlayerDirectly = false, stakes = "Городская власть", canBecomeSoulQuest = true, recommendedCounterQuestTone = "political" },
                    milestones = Array.Empty<object>(),
                    currentStage = 0,
                    publicSignals = Array.Empty<object>(),
                    resolution = new { outcome = "ongoing", notes = "" }
                }
            }
        });
        await WriteJsonAsync("game_state/world/world_events.json", new
        {
            worldEventsLog = new[]
            {
                new
                {
                    eventId = "event_visibility_missing_001",
                    title = "Ночной сход заговорщиков",
                    description = "Событие связано с чужой нитью, но visibility не указана.",
                    relatedRivalArcId = "arc_visibility_missing_001"
                }
            }
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "world_event_rival_arc_missing_visibility", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RivalLinkedWorldEventWithInvalidVisibility_FailsExplicitly()
    {
        await SeedRivalArcGuardiansAsync();
        await WriteJsonAsync(RivalSoulArcService.StatePath, new
        {
            arcs = new object[]
            {
                new
                {
                    arcId = "arc_visibility_invalid_001",
                    scope = "minor",
                    arcType = "political_claim",
                    status = "rising",
                    sponsorGuardianRef = new { mode = "guardianId", guardianId = "guard_social_azalia_001", displayName = "Азалия" },
                    rivalSoul = new { rivalSoulId = "rival_visibility_invalid_001", displayNameOrMoniker = "Пепельный Наследник", roleSummary = "Претендент", isKnownToPlayer = false },
                    objective = "Подготовить переворот",
                    playerIntersection = new { targetsPlayerDirectly = false, stakes = "Городская власть", canBecomeSoulQuest = true, recommendedCounterQuestTone = "political" },
                    milestones = Array.Empty<object>(),
                    currentStage = 0,
                    publicSignals = Array.Empty<object>(),
                    resolution = new { outcome = "ongoing", notes = "" }
                }
            }
        });
        await WriteJsonAsync("game_state/world/world_events.json", new
        {
            worldEventsLog = new[]
            {
                new
                {
                    eventId = "event_visibility_invalid_001",
                    title = "Сбой в контракте видимости",
                    description = "Событие связано с чужой нитью, но visibility задано с ошибкой.",
                    relatedRivalArcId = "arc_visibility_invalid_001",
                    visibility = "Obscured"
                }
            }
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "world_event_rival_arc_invalid_visibility", StringComparison.OrdinalIgnoreCase));
    }

    private async Task WritePendingTurnSnapshotManifestAsync(Dictionary<string, string> rollbackBackups)
    {
        var manifest = new PendingTurnSnapshotManifest
        {
            SessionId = "session-test",
            RequestId = "request-test",
            TurnNumber = 12,
            RequestTimestamp = "2026-03-26T00:00:00Z",
            PlayerAction = "test",
            ProgressionControl = null,
            Files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            SnapshotFileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            ClientOwnedValidationHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            RollbackBackups = rollbackBackups,
            RollbackBaselineFiles = new List<string>(),
            SourceLabel = "обработки хода",
            ManifestPayloadHash = string.Empty
        };

        manifest.ManifestPayloadHash = ComputeManifestPayloadHash(manifest);
        await WriteJsonAsync("game_state/control/pending_turn_snapshot.json", manifest);
    }

    private static string ComputeManifestPayloadHash(PendingTurnSnapshotManifest manifest)
    {
        var payload = new PendingTurnSnapshotManifest
        {
            SessionId = manifest.SessionId,
            RequestId = manifest.RequestId,
            TurnNumber = manifest.TurnNumber,
            RequestTimestamp = manifest.RequestTimestamp,
            PlayerAction = manifest.PlayerAction,
            ProgressionControl = manifest.ProgressionControl,
            Files = manifest.Files,
            SnapshotFileHashes = manifest.SnapshotFileHashes,
            ClientOwnedValidationHashes = manifest.ClientOwnedValidationHashes,
            RollbackBackups = manifest.RollbackBackups,
            RollbackBaselineFiles = manifest.RollbackBaselineFiles,
            SourceLabel = manifest.SourceLabel,
            ManifestPayloadHash = string.Empty
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(json)));
    }

    private async Task WriteJsonAsync(string relativePath, object payload)
    {
        await _fs.WriteFileAtomicAsync(relativePath, JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));
    }

    private async Task SeedRivalArcGuardiansAsync()
    {
        var fixturePath = Path.Combine(
            TestRepoPaths.ValidatorFixturesRoot,
            "rival_soul_arc_contract",
            "shared",
            "guardians.json");
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", await File.ReadAllTextAsync(fixturePath));
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(directory.Replace(sourceDir, destinationDir));

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(sourceDir, destinationDir), overwrite: true);
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

    private sealed class PendingTurnSnapshotManifest
    {
        public string SessionId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public int TurnNumber { get; set; }
        public string RequestTimestamp { get; set; } = string.Empty;
        public string PlayerAction { get; set; } = string.Empty;
        public ProgressionControl? ProgressionControl { get; set; }
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> SnapshotFileHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ClientOwnedValidationHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> RollbackBackups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> RollbackBaselineFiles { get; set; } = new();
        public string? SourceLabel { get; set; }
        public string ManifestPayloadHash { get; set; } = string.Empty;
    }
}
