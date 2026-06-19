using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    public async Task ValidateGameStateAsync_CommandOnlyArchiveActionResolutions_DoesNotReportUnknownStateKey()
    {
        await WriteJsonAsync(
            "game_state/meta/soul_state.json",
            new
            {
                currentRealm = "Chaos Sea",
                currentIncarnation = 1,
                archiveActionResolutions = Array.Empty<object>()
            });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            IsFlexibleTopLevelKeyIssue(issue) &&
            issue.FilePath.Contains("archiveActionResolutions", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CommandOnlyGuardianTradeReceipts_DoesNotReportUnknownStateKey()
    {
        await WriteJsonAsync(
            "game_state/meta/guardians.json",
            new
            {
                UpdateGuardianTradeInventoryReceipts = Array.Empty<object>()
            });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            IsFlexibleTopLevelKeyIssue(issue) &&
            (issue.FilePath.Contains(GuardianTradeRequestState.UpdateReceiptsProperty, StringComparison.OrdinalIgnoreCase) ||
             issue.FilePath.Contains("game_state/meta/guardians.json", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsFlexibleTopLevelKeyIssue(ValidationIssue issue) =>
        string.Equals(issue.Code, "flexible_state_unknown_top_level_key", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(issue.Code, "missing_allowed_top_level_key", StringComparison.OrdinalIgnoreCase);

    [Fact]
    public async Task ValidateGameStateAsync_PendingManifestationRequestWithInvalidResidentTierSnapshot_Fails()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 2,
            soulRelics = new
            {
                equipped = Array.Empty<object>(),
                stored = Array.Empty<object>()
            }
        });

        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "resident_manifest_req_invalid_snapshot",
                    manifestationSource = "resident_relic",
                    relicId = "relic_companion_echo_snapshot_invalid",
                    relicName = "Эхо Лиоры",
                    sourceResidentId = "resident_alpha_1",
                    sourceGuardianId = "guardian_alpha",
                    sourceGuardianName = "Азалия",
                    targetIncarnation = 2,
                    companionNameHint = "Лиора",
                    originWorldSummary = "Бывшая гонец при храме дорог.",
                    futureCompanionPrompt = "Swift wanderer",
                    personalityProfile = new
                    {
                        archetype = "Road Messenger",
                        worldview = "Каждая связь требует движения.",
                        culturalLayer = "Храм дорог",
                        coreValues = new[] { "верность", "путь" },
                        personalityTraits = new object[]
                        {
                            new { traitName = "Restless Loyalty", value = 8, valueDescription = "Всегда ищет дорогу обратно." }
                        }
                    },
                    abodeDisposition = new
                    {
                        powerSensitivity = "medium",
                        migrationDisposition = "selective",
                        communalOrientation = "high",
                        stabilityNeed = "medium"
                    },
                    abodeDevotionLevel = 74,
                    abodeDevotionTier = "uncertain",
                    restlessness = 28,
                    migrationState = "settled",
                    createdAtUtc = "2026-04-15T00:00:00Z"
                }
            }
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_abode_resident_abode_devotion_tier_mismatch", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("pending_resident_companion_manifestation_request", StringComparison.OrdinalIgnoreCase));
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
                soulName = "Тестовая Душа",
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
        await EnsureCurrentGuardianAndTrackerValidatedBaselinesAsync();

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
    public async Task ValidateGameStateAsync_AfterlifeArchiveDuplicateReceiptIdentity_Fails()
    {
        await WriteJsonAsync(
            "game_state/meta/soul_state.json",
            new
            {
                soulName = "Тестовая Душа",
                currentRealm = "Chaos Sea",
                currentIncarnation = 1,
                afterlifeArchive = new
                {
                    stored = new[]
                    {
                        new
                        {
                            archiveId = "archive_duplicate",
                            entryType = "lore_fragment",
                            title = "Летопись Серого Двора",
                            summary = "Описание",
                            rarity = "Rare",
                            sourceLife = 1,
                            sourceKind = "codex",
                            acquiredAtUtc = "2026-03-26T00:00:00Z"
                        }
                    },
                    actionReceipts = new object[]
                    {
                        new
                        {
                            requestId = "req_duplicate",
                            archiveId = "archive_duplicate",
                            requestedMode = "consultation",
                            status = "rejected",
                            guardianId = "guardian_archive_001",
                            guardianName = "Азалия",
                            reason = "",
                            resolvedAtTurn = 12,
                            resolvedAtUtc = "2026-03-26T00:01:00Z"
                        },
                        new
                        {
                            requestId = " req_duplicate ",
                            archiveId = " archive_duplicate ",
                            requestedMode = "consultation",
                            status = "rejected",
                            guardianId = "guardian_archive_001",
                            guardianName = "Азалия",
                            reason = "",
                            resolvedAtTurn = 13,
                            resolvedAtUtc = "2026-03-26T00:02:00Z"
                        }
                    }
                }
            });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "afterlife_archive_duplicate_receipt_identity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ArchiveConsultationRequest_DoesNotAcceptReceiptWithWrongArchiveIdentity()
    {
        const string requestId = "archive_consult_identity_mismatch";

        await WriteJsonAsync(AfterlifeArchiveActionState.ConsultationRequestPath, new
        {
            requestId,
            guardianId = "guardian_archive_001",
            guardianName = "Азалия",
            archiveId = "archive_expected",
            archiveTitle = "Летопись Серого Двора",
            archiveEntryType = "lore_fragment",
            archiveRarity = "Rare",
            archiveSourceKind = "codex",
            targetIncarnation = 2,
            createdAtTurn = 12,
            createdAtUtc = "2026-03-26T00:00:00Z",
            requestedMode = "consultation"
        });

        await WriteJsonAsync(
            "game_state/meta/soul_state.json",
            new
            {
                soulName = "Тестовая Душа",
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
                            archiveId = "archive_other",
                            requestedMode = "consultation",
                            status = "accepted",
                            guardianId = "guardian_archive_001",
                            guardianName = "Азалия",
                            guaranteedArchiveQuestCount = 1,
                            questHookCount = 0,
                            specialQuestLineUnlocks = 0,
                            visibleRivalClueBonus = 0,
                            archiveWarningTierBonus = 0,
                            resolvedAtTurn = 12,
                            resolvedAtUtc = "2026-03-26T00:01:00Z"
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

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "archive_consultation_request_missing_reservation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ArchiveConsultationRequest_WithMalformedCurrentArchiveOwnerState_RaisesCurrentArchiveAuthorityIssue()
    {
        await WriteJsonAsync(AfterlifeArchiveActionState.ConsultationRequestPath, new
        {
            requestId = "archive_consult_malformed_current_owner",
            guardianId = "guardian_archive_001",
            guardianName = "Азалия",
            archiveId = "archive_expected",
            archiveTitle = "Летопись Серого Двора",
            archiveEntryType = "lore_fragment",
            archiveRarity = "Rare",
            archiveSourceKind = "codex",
            targetIncarnation = 2,
            createdAtTurn = 12,
            createdAtUtc = "2026-03-26T00:00:00Z",
            requestedMode = "consultation"
        });

        await WriteJsonAsync(
            "game_state/meta/soul_state.json",
            new
            {
                soulName = "Тестовая Душа",
                currentRealm = "Chaos Sea",
                currentIncarnation = 1,
                afterlifeArchive = new
                {
                    stored = Array.Empty<object>(),
                    foo = 1
                }
            });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "archive_consultation_request_missing_current_archive_authority", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "archive_consultation_request_missing_reservation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_AcceptedArchiveConsultation_DoesNotAcceptPreTurnReceiptWithWrongArchiveIdentity()
    {
        const string requestId = "archive_consult_resolution_mismatch";
        const string guardianId = "guardian_archive_001";

        var request = new
        {
            requestId,
            guardianId,
            guardianName = "Азалия",
            archiveId = "archive_expected",
            archiveTitle = "Летопись Серого Двора",
            archiveEntryType = "lore_fragment",
            archiveRarity = "Rare",
            archiveSourceKind = "codex",
            targetIncarnation = 2,
            createdAtTurn = 12,
            createdAtUtc = "2026-03-26T00:00:00Z",
            requestedMode = "consultation"
        };

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
                            archiveId = "archive_other",
                            requestedMode = "consultation",
                            status = "accepted",
                            guardianId,
                            guardianName = "Азалия",
                            guaranteedArchiveQuestCount = 1,
                            questHookCount = 0,
                            specialQuestLineUnlocks = 0,
                            visibleRivalClueBonus = 0,
                            archiveWarningTierBonus = 0,
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
                            projectId = "archive_consult_project_identity_mismatch",
                            projectType = "lore_research",
                            projectOrigin = "archive_consultation",
                            projectTier = "minor",
                            projectMode = "supportive",
                            projectName = "Архивная консультация: Летопись Серого Двора",
                            finalState = "Completed",
                            completionTurn = 12,
                            consultationRequestId = requestId,
                            consultationArchiveId = "archive_other",
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
        await EnsureCurrentGuardianAndTrackerValidatedBaselinesAsync();

        await WriteJsonAsync(
            "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json",
            new
            {
                currentRealm = "Chaos Sea",
                currentIncarnation = 1
            });

        const string backupPath = "game_state/control/pending_turn_snapshot/pre_pending_archive_consultation_request_identity_mismatch.json";
        await WriteJsonAsync(backupPath, request);
        await WriteJsonAsync(AfterlifeArchiveActionState.ConsultationRequestPath, request);
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [AfterlifeArchiveActionState.ConsultationRequestPath] = backupPath
        });
        await _fs.WriteFileAtomicAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "archive_consultation_request_missing_resolution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_AcceptedArchiveConsultation_RequiresMatchingConsultationRequestIdForCanonicalResult()
    {
        const string requestId = "archive_consult_result_request_mismatch";
        const string archiveId = "archive_expected";
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
                soulName = "Тестовая Душа",
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
                            projectId = "archive_consult_project_request_mismatch",
                            projectType = "lore_research",
                            projectOrigin = "archive_consultation",
                            projectTier = "minor",
                            projectMode = "supportive",
                            projectName = "Архивная консультация: Летопись Серого Двора",
                            finalState = "Completed",
                            completionTurn = 12,
                            consultationRequestId = "archive_consult_other_request",
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
        await EnsureCurrentGuardianAndTrackerValidatedBaselinesAsync();

        await WriteJsonAsync(
            "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json",
            new
            {
                currentRealm = "Chaos Sea",
                currentIncarnation = 1
            });

        const string backupPath = "game_state/control/pending_turn_snapshot/pre_pending_archive_consultation_request_request_mismatch.json";
        await WriteJsonAsync(backupPath, request);
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [AfterlifeArchiveActionState.ConsultationRequestPath] = backupPath
        });
        await _fs.WriteFileAtomicAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "archive_consultation_request_missing_canonical_result", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_AcceptedArchiveConsultation_FlagsMalformedValidatedSnapshotRequest()
    {
        const string requestId = "archive_consult_snapshot_malformed";
        const string archiveId = "archive_expected";
        const string guardianId = "guardian_archive_001";

        await WriteJsonAsync(AfterlifeArchiveActionState.ConsultationRequestPath, new
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
        });

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
                            resolvedAtTurn = 12,
                            resolvedAtUtc = "2026-03-26T00:01:00Z"
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

        const string backupPath = "game_state/control/pending_turn_snapshot/pre_pending_archive_consultation_request_malformed.json";
        await _fs.WriteFileAtomicAsync(backupPath, """
        {
          "requestId": "archive_consult_snapshot_malformed",
          "guardianId":
        }
        """);
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [AfterlifeArchiveActionState.ConsultationRequestPath] = backupPath
        });
        await _fs.WriteFileAtomicAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "archive_consultation_request_malformed_validated_snapshot_request", StringComparison.OrdinalIgnoreCase));
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
    public async Task ValidateGameStateAsync_GuardianTradePendingFileMutation_IsRejectedAsClientOwned()
    {
        var preTurnRequest = new
        {
            requestId = "guardian_trade_original",
            guardianId = "guardian_alpha",
            guardianName = "Азалия",
            abodeId = "abode_alpha",
            returnCycleId = "return_1",
            currentReputation = 110,
            derivedTradeSlotCount = 4,
            effectiveRarityCeilingBonusSteps = 0,
            projectBonusSignature = "0|0|0",
            createdAtUtc = "2026-03-26T00:00:00Z",
            createdAtTurn = 11
        };
        var mutatedLiveRequest = new
        {
            requestId = "guardian_trade_retargeted",
            guardianId = "guardian_beta",
            guardianName = "Варак",
            abodeId = "abode_beta",
            returnCycleId = "return_2",
            currentReputation = 120,
            derivedTradeSlotCount = 4,
            effectiveRarityCeilingBonusSteps = 0,
            projectBonusSignature = "0|0|0",
            createdAtUtc = "2026-03-26T01:00:00Z",
            createdAtTurn = 12
        };

        await WriteJsonAsync(GuardianTradeRequestState.PendingRequestPath, mutatedLiveRequest);
        await WriteJsonAsync("ready/turn_complete.json", new { accepted = true });
        const string backupPath = "game_state/control/pending_turn_snapshot/pre_pending_guardian_trade_request_original.json";
        await WriteJsonAsync(backupPath, preTurnRequest);
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GuardianTradeRequestState.PendingRequestPath] = backupPath
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "client_owned_guardian_trade_request_modified", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PendingManifestationFileMutation_IsRejectedAsClientOwned()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            currentRealm = "Chaos Sea",
            currentIncarnation = 4,
            inkFeathers = new { current = 10, total = 10 },
            soulRelics = new
            {
                equipped = Array.Empty<object>(),
                stored = Array.Empty<object>()
            }
        });

        var preTurnRequest = new
        {
            requests = new[]
            {
                new
                {
                    requestId = "manifest_original",
                    manifestationSource = "resident_relic",
                    relicId = "relic_alpha",
                    relicName = "Эхо Зари",
                    sourceResidentId = "resident_alpha",
                    sourceGuardianId = "guardian_alpha",
                    sourceGuardianName = "Азалия",
                    targetIncarnation = 5,
                    companionNameHint = "Ирия",
                    originWorldSummary = "Будущая смертная жизнь.",
                    futureCompanionPrompt = "Ирия должна проявиться как ранняя спутница в следующей смертной жизни.",
                    bondReason = "Связь создана через реликвию резидента.",
                    coreTraits = new[] { "loyal" },
                    archetypeHints = new[] { "guide" },
                    appearanceMotifs = new[] { "dawn" },
                    createdAtUtc = "2026-04-20T00:00:00Z"
                }
            }
        };
        var mutatedLiveRequest = new
        {
            requests = new[]
            {
                new
                {
                    requestId = "manifest_retargeted",
                    manifestationSource = "resident_relic",
                    relicId = "relic_beta",
                    relicName = "Эхо Заката",
                    sourceResidentId = "resident_beta",
                    sourceGuardianId = "guardian_beta",
                    sourceGuardianName = "Мириэль",
                    targetIncarnation = 5,
                    companionNameHint = "Селия",
                    originWorldSummary = "Будущая смертная жизнь.",
                    futureCompanionPrompt = "Селия должна проявиться как ранняя спутница в следующей смертной жизни.",
                    bondReason = "Связь переписана через другую реликвию резидента.",
                    coreTraits = new[] { "watchful" },
                    archetypeHints = new[] { "sentinel" },
                    appearanceMotifs = new[] { "dusk" },
                    createdAtUtc = "2026-04-20T01:00:00Z"
                }
            }
        };

        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, mutatedLiveRequest);
        await WriteJsonAsync("ready/turn_complete.json", new { accepted = true });
        const string backupPath = "game_state/control/pending_turn_snapshot/pre_pending_resident_manifestation_request_original.json";
        await WriteJsonAsync(backupPath, preTurnRequest);
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GuardianAbodeResidentRequestState.PendingManifestationRequestPath] = backupPath
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "client_owned_resident_manifestation_request_modified", StringComparison.OrdinalIgnoreCase));
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
            createdAtUtc = "2026-03-26T00:00:00Z",
            createdAtTurn = 11
        };

        await WriteJsonAsync(GuardianTradeRequestState.PendingRequestPath, request);
        await WriteJsonAsync(
            "game_state/meta/soul_state.json",
            new
            {
                soulName = "Тестовая Душа",
                currentRealm = "Chaos Sea",
                currentIncarnation = 1
            });
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
        await EnsureCurrentGuardianAndTrackerValidatedBaselinesAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => HasExpectedOrGuardianAuthorityGate(issue, "guardian_trade_request_missing_guardian_resolution"));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "guardian_trade_request_missing_inventory_resolution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PreTurnTradeRequestWithInventoryButWithoutReceipt_FailsResolutionContract()
    {
        var request = new
        {
            requestId = "guardian_trade_test_002",
            guardianId = "guardian_alpha",
            guardianName = "Азалия",
            abodeId = "abode_alpha",
            returnCycleId = "return_1",
            currentReputation = 110,
            derivedTradeSlotCount = 4,
            effectiveRarityCeilingBonusSteps = 0,
            projectBonusSignature = "0|0|0",
            createdAtUtc = "2026-03-26T00:00:00Z",
            createdAtTurn = 11
        };

        await WriteJsonAsync(GuardianTradeRequestState.PendingRequestPath, request);
        await WriteJsonAsync(
            "game_state/meta/soul_state.json",
            new
            {
                soulName = "Тестовая Душа",
                currentRealm = "Chaos Sea",
                currentIncarnation = 1
            });
        await WriteJsonAsync(
            "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json",
            new
            {
                currentRealm = "Chaos Sea",
                currentIncarnation = 1
            });

        const string backupPath = "game_state/control/pending_turn_snapshot/pre_pending_guardian_trade_request_receipt.json";
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
              "tradeInventory": {
                "tradeCycleId": "return_1",
                "generatedAtUtc": "2026-03-26T00:10:00Z",
                "generationReputationTier": "Friendly",
                "pricingReputationTier": "Friendly",
                "projectBonusSignature": "0|0|0",
                "effectiveRarityCeilingBonusSteps": 0,
                "items": [
                  { "slotId": "slot_1", "priceInFeathers": 30, "soldOut": false, "rarityBonusStepsApplied": 0, "relicData": { "relicId": "relic_1", "name": "Реликвия 1", "quality": "Common" } },
                  { "slotId": "slot_2", "priceInFeathers": 70, "soldOut": false, "rarityBonusStepsApplied": 0, "relicData": { "relicId": "relic_2", "name": "Реликвия 2", "quality": "Uncommon" } },
                  { "slotId": "slot_3", "priceInFeathers": 140, "soldOut": false, "rarityBonusStepsApplied": 0, "relicData": { "relicId": "relic_3", "name": "Реликвия 3", "quality": "Rare" } },
                  { "slotId": "slot_4", "priceInFeathers": 140, "soldOut": false, "rarityBonusStepsApplied": 0, "relicData": { "relicId": "relic_4", "name": "Реликвия 4", "quality": "Rare" } }
                ]
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
            "abodePower": { "currentPower": 10, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель" },
            "tradeInventory": {
              "tradeCycleId": "return_1",
              "generatedAtUtc": "2026-03-26T00:10:00Z",
              "generationReputationTier": "Friendly",
              "pricingReputationTier": "Friendly",
              "projectBonusSignature": "0|0|0",
              "effectiveRarityCeilingBonusSteps": 0,
              "items": [
                { "slotId": "slot_1", "priceInFeathers": 30, "soldOut": false, "rarityBonusStepsApplied": 0, "relicData": { "relicId": "relic_1", "name": "Реликвия 1", "quality": "Common" } },
                { "slotId": "slot_2", "priceInFeathers": 70, "soldOut": false, "rarityBonusStepsApplied": 0, "relicData": { "relicId": "relic_2", "name": "Реликвия 2", "quality": "Uncommon" } },
                { "slotId": "slot_3", "priceInFeathers": 140, "soldOut": false, "rarityBonusStepsApplied": 0, "relicData": { "relicId": "relic_3", "name": "Реликвия 3", "quality": "Rare" } },
                { "slotId": "slot_4", "priceInFeathers": 140, "soldOut": false, "rarityBonusStepsApplied": 0, "relicData": { "relicId": "relic_4", "name": "Реликвия 4", "quality": "Rare" } }
              ]
            },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          },
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_alpha"
          }
        }
        """);
        await EnsureCurrentGuardianAndTrackerValidatedBaselinesAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => HasExpectedOrGuardianAuthorityGate(issue, "guardian_trade_request_missing_guardian_resolution"));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "guardian_trade_request_missing_receipt_resolution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_GuardianBuybackRelicsWithMalformedEntry_FailsValidation()
    {
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new object[]
            {
                new
                {
                    guardianId = "guardian_alpha",
                    canonicalName = "Азалия",
                    domain = "Порог Сна",
                    nameVariants = new { @default = "Азалия", feminine = "Азалия", masculine = (string?)null, neutral = (string?)null },
                    manifestation = new
                    {
                        currentDisplayName = "Азалия",
                        formFlexibility = "selective",
                        currentPresentationStyle = "feminine",
                        currentPronouns = "она/её",
                        appearanceDescription = "Тестовая форма."
                    },
                    manifestationHistory = Array.Empty<object>(),
                    relationshipData = new { currentReputation = 120, reputationHistory = Array.Empty<object>(), lastInteraction = (string?)null },
                    abodePower = new { currentPower = 10, tier = "Хрупкая", lastUpdatedAt = "2026-03-24T00:00:00Z", history = Array.Empty<object>() },
                    abode = new { abodeId = "abode_alpha", name = "Тестовая обитель" },
                    gachaSystem = new { chargesPerReturn = 0, chargesUsedThisReturn = 0, gachaHistory = Array.Empty<object>() },
                    buybackRelics = new object[]
                    {
                        new
                        {
                            buybackEntryId = "guardian_buyback_001",
                            guardianId = "guardian_wrong",
                            guardianName = "Азалия",
                            relicId = "relic_buyback_001",
                            relicData = new
                            {
                                relicId = "relic_buyback_002",
                                name = "Сломанная запись"
                            },
                            soldByPlayerAtTurn = -1,
                            soldByPlayerAtUtc = "not-a-timestamp",
                            soldForPrice = 0,
                            buybackPrice = 0,
                            acquiredFromPlayer = "yes",
                            status = "available"
                        }
                    }
                }
            }
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "guardian_buyback_relic_guardian_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "guardian_buyback_relic_id_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "guardian_buyback_relic_sold_turn_invalid", StringComparison.OrdinalIgnoreCase));
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
        await EnsureCurrentGuardianAndTrackerValidatedBaselinesAsync();

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
    public async Task ValidateGameStateAsync_ArchiveProjectFuelRequest_DoesNotAcceptReceiptWithWrongArchiveIdentity()
    {
        const string requestId = "archive_project_fuel_identity_mismatch";

        await WriteJsonAsync(AfterlifeArchiveActionState.ProjectFuelRequestPath, new
        {
            requestId,
            guardianId = "guardian_archive_001",
            guardianName = "Азалия",
            archiveId = "archive_expected",
            archiveTitle = "Секрет старого договора",
            archiveEntryType = "secret_record",
            archiveRarity = "Epic",
            archiveSourceKind = "codex",
            targetProjectId = "project_pressure_001",
            targetProjectName = "Нить тайного двора",
            createdAtTurn = 12,
            createdAtUtc = "2026-03-26T00:00:00Z",
            requestedMode = "project_fuel"
        });

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
                            archiveId = "archive_other",
                            requestedMode = "project_fuel",
                            status = "accepted",
                            guardianId = "guardian_archive_001",
                            guardianName = "Азалия",
                            targetProjectId = "project_pressure_001",
                            resultMode = "project_work",
                            resultAmount = 2,
                            resolvedAtTurn = 12,
                            resolvedAtUtc = "2026-03-26T00:01:00Z"
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

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "archive_project_fuel_request_missing_reservation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ArchiveProjectFuelResolution_WithMalformedCurrentArchiveOwnerState_RaisesCurrentArchiveAuthorityIssue()
    {
        const string requestId = "archive_project_fuel_malformed_current_owner";
        const string projectId = "project_pressure_001";

        var request = new
        {
            requestId,
            guardianId = "guardian_archive_001",
            guardianName = "Азалия",
            archiveId = "archive_expected",
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
                    foo = 1
                }
            });
        await WriteJsonAsync(
            "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json",
            new
            {
                currentRealm = "Chaos Sea",
                currentIncarnation = 1
            });

        const string backupPath = "game_state/control/pending_turn_snapshot/pre_pending_archive_project_fuel_request_malformed_current_owner.json";
        await WriteJsonAsync(backupPath, request);
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [AfterlifeArchiveActionState.ProjectFuelRequestPath] = backupPath
        });
        await _fs.WriteFileAtomicAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "archive_project_fuel_request_missing_current_archive_authority", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "archive_project_fuel_request_missing_resolution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ArchiveProjectFuelResolution_DoesNotAcceptPreTurnReceiptWithWrongArchiveIdentity()
    {
        const string requestId = "archive_project_fuel_resolution_mismatch";
        const string guardianId = "guardian_archive_001";
        const string projectId = "project_pressure_001";

        var request = new
        {
            requestId,
            guardianId,
            guardianName = "Азалия",
            archiveId = "archive_expected",
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
                            archiveId = "archive_other",
                            requestedMode = "project_fuel",
                            status = "accepted",
                            guardianId,
                            guardianName = "Азалия",
                            targetProjectId = projectId,
                            resultMode = "project_work",
                            resultAmount = 2,
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
                        entryId = "journal_fuel_identity_mismatch",
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
                            "ArchiveId: archive_other"
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

        const string backupPath = "game_state/control/pending_turn_snapshot/pre_pending_archive_project_fuel_request_identity_mismatch.json";
        await WriteJsonAsync(backupPath, request);
        await WriteJsonAsync(AfterlifeArchiveActionState.ProjectFuelRequestPath, request);
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [AfterlifeArchiveActionState.ProjectFuelRequestPath] = backupPath
        });
        await _fs.WriteFileAtomicAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "archive_project_fuel_request_missing_resolution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_AcceptedArchiveProjectFuel_RequiresMatchingArchiveFuelRequestIdForCanonicalResult()
    {
        const string requestId = "archive_project_fuel_result_request_mismatch";
        const string archiveId = "archive_expected";
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
                            resultMode = "project_work",
                            resultAmount = 2,
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
                        entryId = "journal_fuel_request_mismatch",
                        turn = 12,
                        guardianId,
                        projectId,
                        eventType = "assisted",
                        visibility = "player_known",
                        archiveFuelRequestId = "archive_project_fuel_other_request",
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

        const string backupPath = "game_state/control/pending_turn_snapshot/pre_pending_archive_project_fuel_request_request_mismatch.json";
        await WriteJsonAsync(backupPath, request);
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [AfterlifeArchiveActionState.ProjectFuelRequestPath] = backupPath
        });
        await _fs.WriteFileAtomicAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "archive_project_fuel_request_missing_canonical_result", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_AcceptedArchiveProjectFuel_FlagsMalformedValidatedSnapshotRequest()
    {
        const string requestId = "archive_project_fuel_snapshot_malformed";
        const string archiveId = "archive_expected";
        const string guardianId = "guardian_archive_001";
        const string projectId = "project_pressure_001";

        await WriteJsonAsync(AfterlifeArchiveActionState.ProjectFuelRequestPath, new
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
        });

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
                            resultMode = "project_work",
                            resultAmount = 2,
                            resolvedAtTurn = 12,
                            resolvedAtUtc = "2026-03-26T00:01:00Z"
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

        const string backupPath = "game_state/control/pending_turn_snapshot/pre_pending_archive_project_fuel_request_malformed.json";
        await _fs.WriteFileAtomicAsync(backupPath, """
        {
          "requestId": "archive_project_fuel_snapshot_malformed",
          "guardianId":
        }
        """);
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [AfterlifeArchiveActionState.ProjectFuelRequestPath] = backupPath
        });
        await _fs.WriteFileAtomicAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "archive_project_fuel_request_malformed_validated_snapshot_request", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_GuardianAvailableQuestWithoutQuestId_FailsHardIdentityContract()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", BuildChaosSeaSoulState());
        await WriteJsonAsync("game_state/meta/guardians.json", BuildGuardianState(questManagement: new
        {
            availableQuests = new object[]
            {
                new
                {
                    title = "След незавершённого имени",
                    description = "Квест без canonical questId.",
                    status = "available",
                    difficulty = "normal"
                }
            },
            activeQuests = Array.Empty<object>(),
            completedQuests = Array.Empty<object>()
        }));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_live_quest_missing_quest_id", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "guardian_missing_validated_preturn_guardians_snapshot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_GuardianActiveQuestWithoutQuestId_FailsHardIdentityContract()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", BuildChaosSeaSoulState());
        await WriteJsonAsync("game_state/meta/guardians.json", BuildGuardianState(questManagement: new
        {
            availableQuests = Array.Empty<object>(),
            activeQuests = new object[]
            {
                new
                {
                    title = "Нить молчащего долга",
                    description = "Активный квест без canonical questId.",
                    status = "active",
                    difficulty = "hard"
                }
            },
            completedQuests = Array.Empty<object>()
        }));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_live_quest_missing_quest_id", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "guardian_missing_validated_preturn_guardians_snapshot", StringComparison.OrdinalIgnoreCase));
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

    [Fact]
    public async Task ValidateGameStateAsync_PendingResidentTalkRequestWithoutReceipt_Fails()
    {
        await WriteJsonAsync("game_state/meta/guardians.json", BuildGuardianState());
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    bondLevel = 30,
                    bondTier = "familiar",
                    canGrantCompanionRelic = false,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = false,
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                }
            }
        });

        var request = new
        {
            requestId = "resident_talk_req_1",
            guardianId = "guardian_alpha",
            guardianName = "Азалия",
            abodeId = "abode_alpha",
            abodeName = "Сад Нитей",
            residentId = "resident_alpha_1",
            residentName = "Лиора",
            interactionType = "talk",
            createdAtTurn = 12,
            createdAtUtc = "2026-03-27T00:00:00Z"
        };
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath, new { requests = new[] { request } });
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new { currentRealm = "Chaos Sea" });
        const string backupPath = "game_state/control/pending_turn_snapshot/pre_pending_guardian_abode_resident_interactions.json";
        await WriteJsonAsync(backupPath, new { requests = new[] { request } });
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GuardianAbodeResidentRequestState.PendingInteractionsRequestPath] = backupPath
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => HasExpectedOrGuardianAuthorityGate(issue, "abode_resident_interaction_missing_resolution"));
    }

    [Fact]
    public async Task ValidateGameStateAsync_AcceptedResidentHistoryWithoutCanonicalResult_Fails()
    {
        await WriteJsonAsync("game_state/meta/guardians.json", BuildGuardianState());

        var currentResidentState = new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    bondLevel = 30,
                    bondTier = "familiar",
                    canGrantCompanionRelic = false,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = false,
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                }
            },
            interactionReceipts = new object[]
            {
                new
                {
                    requestId = "resident_history_req_1",
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    interactionType = "history",
                    status = "accepted",
                    responseMode = "history_partial",
                    resolvedAtTurn = 12,
                    resolvedAtUtc = "2026-03-27T00:05:00Z"
                }
            }
        };
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, currentResidentState);

        var request = new
        {
            requestId = "resident_history_req_1",
            guardianId = "guardian_alpha",
            guardianName = "Азалия",
            abodeId = "abode_alpha",
            abodeName = "Сад Нитей",
            residentId = "resident_alpha_1",
            residentName = "Лиора",
            interactionType = "history",
            createdAtTurn = 12,
            createdAtUtc = "2026-03-27T00:00:00Z"
        };
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath, new { requests = new[] { request } });
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new { currentRealm = "Chaos Sea" });
        const string requestBackupPath = "game_state/control/pending_turn_snapshot/pre_pending_guardian_abode_resident_interactions_history.json";
        const string residentBackupPath = "game_state/control/pending_turn_snapshot/pre_guardian_abode_residents.json";
        await WriteJsonAsync(requestBackupPath, new { requests = new[] { request } });
        await WriteJsonAsync(residentBackupPath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    bondLevel = 30,
                    bondTier = "familiar",
                    canGrantCompanionRelic = false,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = false,
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                }
            }
        });
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GuardianAbodeResidentRequestState.PendingInteractionsRequestPath] = requestBackupPath,
            [GuardianAbodeResidentState.StatePath] = residentBackupPath
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => HasExpectedOrGuardianAuthorityGate(issue, "abode_resident_history_missing_canonical_result"));
    }

    [Fact]
    public async Task ValidateGameStateAsync_AcceptedResidentTalkWithoutMemoryUpdate_Fails()
    {
        await WriteJsonAsync("game_state/meta/guardians.json", BuildGuardianState());

        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    bondLevel = 30,
                    bondTier = "familiar",
                    canGrantCompanionRelic = false,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = false,
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                }
            },
            interactionReceipts = new object[]
            {
                new
                {
                    requestId = "resident_talk_req_1",
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    interactionType = "talk",
                    status = "accepted",
                    responseMode = "talk_scene",
                    resolvedAtTurn = 12,
                    resolvedAtUtc = "2026-03-27T00:05:00Z"
                }
            }
        });

        var request = new
        {
            requestId = "resident_talk_req_1",
            guardianId = "guardian_alpha",
            guardianName = "Азалия",
            abodeId = "abode_alpha",
            abodeName = "Сад Нитей",
            residentId = "resident_alpha_1",
            residentName = "Лиора",
            interactionType = "talk",
            createdAtTurn = 12,
            createdAtUtc = "2026-03-27T00:00:00Z"
        };
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath, new { requests = new[] { request } });
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new { currentRealm = "Chaos Sea" });
        const string requestBackupPath = "game_state/control/pending_turn_snapshot/pre_pending_guardian_abode_resident_interactions_talk.json";
        const string residentBackupPath = "game_state/control/pending_turn_snapshot/pre_guardian_abode_residents_talk.json";
        await WriteJsonAsync(requestBackupPath, new { requests = new[] { request } });
        await WriteJsonAsync(residentBackupPath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    bondLevel = 30,
                    bondTier = "familiar",
                    canGrantCompanionRelic = false,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = false,
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                }
            }
        });
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GuardianAbodeResidentRequestState.PendingInteractionsRequestPath] = requestBackupPath,
            [GuardianAbodeResidentState.StatePath] = residentBackupPath
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => HasExpectedOrGuardianAuthorityGate(issue, "abode_resident_talk_missing_memory_update"));
    }

    [Fact]
    public async Task ValidateGameStateAsync_AcceptedResidentHistoryWithoutMemoryUpdate_Fails()
    {
        await WriteJsonAsync("game_state/meta/guardians.json", BuildGuardianState());

        var request = new
        {
            requestId = "resident_history_req_memory_1",
            guardianId = "guardian_alpha",
            guardianName = "Азалия",
            abodeId = "abode_alpha",
            abodeName = "Сад Нитей",
            residentId = "resident_alpha_1",
            residentName = "Лиора",
            interactionType = "history",
            createdAtTurn = 12,
            createdAtUtc = "2026-03-27T00:00:00Z"
        };

        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    bondLevel = 30,
                    bondTier = "familiar",
                    canGrantCompanionRelic = false,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = true,
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                }
            },
            interactionReceipts = new object[]
            {
                new
                {
                    requestId = request.requestId,
                    residentId = request.residentId,
                    guardianId = request.guardianId,
                    abodeId = request.abodeId,
                    interactionType = request.interactionType,
                    status = "accepted",
                    responseMode = "history_revealed",
                    resolvedAtTurn = 12,
                    resolvedAtUtc = "2026-03-27T00:05:00Z"
                }
            }
        });
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath, new { requests = new[] { request } });
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new { currentRealm = "Chaos Sea" });
        const string requestBackupPath = "game_state/control/pending_turn_snapshot/pre_pending_guardian_abode_resident_interactions_history_memory.json";
        const string residentBackupPath = "game_state/control/pending_turn_snapshot/pre_guardian_abode_residents_history_memory.json";
        await WriteJsonAsync(requestBackupPath, new { requests = new[] { request } });
        await WriteJsonAsync(residentBackupPath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    bondLevel = 30,
                    bondTier = "familiar",
                    canGrantCompanionRelic = false,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = false,
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                }
            }
        });
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GuardianAbodeResidentRequestState.PendingInteractionsRequestPath] = requestBackupPath,
            [GuardianAbodeResidentState.StatePath] = residentBackupPath
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => HasExpectedOrGuardianAuthorityGate(issue, "abode_resident_history_missing_memory_update"));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ResidentQuestGrantWithoutInteractionLog_Fails()
    {
        await WriteJsonAsync("game_state/meta/guardians.json", BuildGuardianState());

        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    bondLevel = 55,
                    bondTier = "trusted",
                    canGrantCompanionRelic = false,
                    bondRewardState = "none",
                    linkedSoulQuestId = "soul_quest_resident_1",
                    grantedRelicId = "",
                    historyRevealed = true,
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец при храме дорог.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                }
            }
        });
        await WriteJsonAsync("game_state/quests/soul_quests.json", new
        {
            quests = new object[]
            {
                new
                {
                    questId = "soul_quest_resident_1",
                    guardianId = "guardian_alpha",
                    title = "След гонца",
                    description = "Нужно вернуть долговое письмо.",
                    relatedAfterlifeResidentId = "resident_alpha_1",
                    status = "active",
                    objectives = new object[]
                    {
                        new { objectiveId = "obj_1", description = "Найти письмо", status = "Active" }
                    },
                    progress = new { completed = 0, total = 1 },
                    rewards = new { inkFeathers = 0, enlightenmentExperience = 0 },
                    crossIncarnation = false,
                    completionTimestamp = (string?)null
                }
            }
        });

        const string residentBackupPath = "game_state/control/pending_turn_snapshot/pre_guardian_abode_residents_quest_memory.json";
        const string soulQuestBackupPath = "game_state/control/pending_turn_snapshot/pre_soul_quests_resident_memory.json";
        await WriteJsonAsync(residentBackupPath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    bondLevel = 55,
                    bondTier = "trusted",
                    canGrantCompanionRelic = false,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = true,
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец при храме дорог.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                }
            }
        });
        await WriteJsonAsync(soulQuestBackupPath, new { quests = Array.Empty<object>() });
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GuardianAbodeResidentState.StatePath] = residentBackupPath,
            ["game_state/quests/soul_quests.json"] = soulQuestBackupPath
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => HasExpectedOrGuardianAuthorityGate(issue, "abode_resident_quest_missing_interaction_log_update"));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ResidentRewardGrantWithoutInteractionLog_Fails()
    {
        await WriteJsonAsync("game_state/meta/guardians.json", BuildGuardianState());

        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    bondLevel = 80,
                    bondTier = "bound",
                    canGrantCompanionRelic = true,
                    bondRewardState = "granted",
                    linkedSoulQuestId = "",
                    grantedRelicId = "relic_echo_liora",
                    historyRevealed = true,
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец при храме дорог.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                }
            }
        });
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            currentRealm = "Chaos Sea",
            soulRelics = new
            {
                equipped = Array.Empty<object>(),
                stored = new object[]
                {
                    new
                    {
                        relicId = "relic_echo_liora",
                        name = "Эхо Лиоры",
                        rarity = "Rare",
                        relicType = "companion_echo",
                        companionSeed = new
                        {
                            sourceResidentId = "resident_alpha_1",
                            sourceGuardianId = "guardian_alpha",
                            companionNameHint = "Лиора",
                            originWorldSummary = "Бывшая гонец при храме дорог.",
                            futureCompanionPrompt = "Swift wanderer"
                        }
                    }
                }
            }
        });

        const string residentBackupPath = "game_state/control/pending_turn_snapshot/pre_guardian_abode_residents_reward_memory.json";
        await WriteJsonAsync(residentBackupPath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    bondLevel = 80,
                    bondTier = "bound",
                    canGrantCompanionRelic = true,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = true,
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец при храме дорог.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                }
            }
        });
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GuardianAbodeResidentState.StatePath] = residentBackupPath
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => HasExpectedOrGuardianAuthorityGate(issue, "abode_resident_reward_missing_interaction_log_update"));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ResidentAbodeShiftWithoutCanonicalTrigger_Fails()
    {
        await WriteJsonAsync("game_state/meta/guardians.json", BuildGuardianState());

        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    bondLevel = 55,
                    bondTier = "trusted",
                    canGrantCompanionRelic = false,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = true,
                    isPresent = true,
                    abodeDisposition = new
                    {
                        powerSensitivity = "medium",
                        migrationDisposition = "selective",
                        communalOrientation = "high",
                        stabilityNeed = "medium"
                    },
                    abodeDevotionLevel = 24,
                    abodeDevotionTier = "uncertain",
                    restlessness = 70,
                    migrationState = "considering_departure",
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец при храме дорог.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                }
            },
            interactionReceipts = Array.Empty<object>(),
            historyLog = Array.Empty<object>(),
            thoughtJournal = Array.Empty<object>(),
            interactionLog = Array.Empty<object>()
        });

        const string residentBackupPath = "game_state/control/pending_turn_snapshot/pre_guardian_abode_residents_drift_trigger.json";
        await WriteJsonAsync(residentBackupPath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    bondLevel = 55,
                    bondTier = "trusted",
                    canGrantCompanionRelic = false,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = true,
                    isPresent = true,
                    abodeDisposition = new
                    {
                        powerSensitivity = "medium",
                        migrationDisposition = "selective",
                        communalOrientation = "high",
                        stabilityNeed = "medium"
                    },
                    abodeDevotionLevel = 55,
                    abodeDevotionTier = "attached",
                    restlessness = 34,
                    migrationState = "wavering",
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец при храме дорог.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                }
            },
            interactionReceipts = Array.Empty<object>(),
            historyLog = Array.Empty<object>(),
            thoughtJournal = Array.Empty<object>(),
            interactionLog = Array.Empty<object>()
        });
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GuardianAbodeResidentState.StatePath] = residentBackupPath
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => HasExpectedOrGuardianAuthorityGate(issue, "abode_resident_devotion_shift_missing_canonical_trigger"));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ResidentTransferRequestBelowReadyToTransfer_Fails()
    {
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingTransfersRequestPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "resident_transfer_req_invalid_gate",
                    residentId = "resident_alpha_1",
                    residentName = "Лиора",
                    sourceGuardianId = "guardian_alpha",
                    sourceGuardianName = "Азалия",
                    sourceAbodeId = "abode_alpha",
                    sourceAbodeName = "Лазурная Обитель",
                    targetGuardianId = "guardian_beta",
                    targetGuardianName = "Мириэль",
                    targetAbodeId = "abode_beta",
                    targetAbodeName = "Сад Перекрёстков",
                    abodeDevotionLevel = 24,
                    abodeDevotionTier = "uncertain",
                    restlessness = 60,
                    migrationState = "considering_departure",
                    transferMode = "accepted_transfer",
                    createdAtTurn = 12,
                    createdAtUtc = "2026-04-16T04:12:00Z"
                }
            }
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "pending_abode_resident_transfer_invalid_migration_gate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ResidentTransferRequestWithInvalidSelectionMode_Fails()
    {
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingTransfersRequestPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "resident_transfer_req_invalid_selection",
                    residentId = "resident_alpha_1",
                    residentName = "Лиора",
                    sourceGuardianId = "guardian_alpha",
                    sourceGuardianName = "Азалия",
                    sourceAbodeId = "abode_alpha",
                    sourceAbodeName = "Лазурная Обитель",
                    targetGuardianId = "guardian_beta",
                    targetGuardianName = "Мириэль",
                    targetAbodeId = "abode_beta",
                    targetAbodeName = "Сад Перекрёстков",
                    abodeDevotionLevel = 12,
                    abodeDevotionTier = "alienated",
                    restlessness = 79,
                    migrationState = "ready_to_transfer",
                    transferMode = "accepted_transfer",
                    selectionMode = "auto_pick",
                    competitionScore = 76,
                    competitionLabel = "strong_pull",
                    competitionReason = "цель сильнее текущей Обители",
                    createdAtTurn = 12,
                    createdAtUtc = "2026-04-16T04:12:00Z"
                }
            }
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "pending_abode_resident_transfer_invalid_selection_mode", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_DepartureOnlyTransferWithCompetitionMetadata_Fails()
    {
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingTransfersRequestPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "resident_transfer_req_departure_metadata",
                    residentId = "resident_alpha_1",
                    residentName = "Лиора",
                    sourceGuardianId = "guardian_alpha",
                    sourceGuardianName = "Азалия",
                    sourceAbodeId = "abode_alpha",
                    sourceAbodeName = "Лазурная Обитель",
                    targetGuardianId = "guardian_beta",
                    targetGuardianName = "Мириэль",
                    targetAbodeId = "abode_beta",
                    targetAbodeName = "Сад Перекрёстков",
                    abodeDevotionLevel = 12,
                    abodeDevotionTier = "alienated",
                    restlessness = 79,
                    migrationState = "ready_to_transfer",
                    transferMode = "departure_only",
                    selectionMode = "competition_recommended",
                    competitionScore = 71,
                    competitionLabel = "strong_pull",
                    competitionReason = "цель сильнее текущей Обители",
                    createdAtTurn = 12,
                    createdAtUtc = "2026-04-16T04:12:00Z"
                }
            }
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "pending_abode_resident_transfer_inconsistent_selection_metadata", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PendingResidentTransferWithoutMatchingReceipt_Fails()
    {
        await WriteJsonAsync("game_state/meta/guardians.json", BuildTwoGuardianState());

        var residentState = new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    roleLabel = "Вестница",
                    summary = "Тонкая душа на границе светлых троп.",
                    bondLevel = 61,
                    bondTier = "trusted",
                    canGrantCompanionRelic = true,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = true,
                    isPresent = true,
                    abodeDisposition = new
                    {
                        powerSensitivity = "high",
                        migrationDisposition = "selective",
                        communalOrientation = "medium",
                        stabilityNeed = "high"
                    },
                    abodeDevotionLevel = 12,
                    abodeDevotionTier = "alienated",
                    restlessness = 79,
                    migrationState = "ready_to_transfer",
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец при храме дорог.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                }
            },
            transferReceipts = Array.Empty<object>(),
            interactionReceipts = Array.Empty<object>(),
            historyLog = Array.Empty<object>(),
            thoughtJournal = Array.Empty<object>(),
            interactionLog = Array.Empty<object>()
        };
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, residentState);

        var request = new
        {
            requestId = "resident_transfer_req_missing_receipt",
            residentId = "resident_alpha_1",
            residentName = "Лиора",
            sourceGuardianId = "guardian_alpha",
            sourceGuardianName = "Азалия",
            sourceAbodeId = "abode_alpha",
            sourceAbodeName = "Лазурная Обитель",
            targetGuardianId = "guardian_beta",
            targetGuardianName = "Мириэль",
            targetAbodeId = "abode_beta",
            targetAbodeName = "Сад Перекрёстков",
            abodeDevotionLevel = 12,
            abodeDevotionTier = "alienated",
            restlessness = 79,
            migrationState = "ready_to_transfer",
            transferMode = "accepted_transfer",
            createdAtTurn = 12,
            createdAtUtc = "2026-04-16T04:12:00Z"
        };
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingTransfersRequestPath, new { requests = new[] { request } });
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new { currentRealm = "Chaos Sea" });
        var requestBackupPath = "test_backups/resident_transfer_req_missing_receipt.json";
        var residentBackupPath = "test_backups/resident_transfer_state_missing_receipt.json";
        await WriteJsonAsync(requestBackupPath, new { requests = new[] { request } });
        await WriteJsonAsync(residentBackupPath, residentState);
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GuardianAbodeResidentRequestState.PendingTransfersRequestPath] = requestBackupPath,
            [GuardianAbodeResidentState.StatePath] = residentBackupPath
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => HasExpectedOrGuardianAuthorityGate(issue, "abode_resident_transfer_missing_resolution"));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PendingResidentTransferMustMatchValidatedPreTurnReadyState()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            currentRealm = "Chaos Sea"
        });

        await WriteJsonAsync("game_state/meta/guardians.json", BuildTwoGuardianState());

        var currentResidentState = new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    roleLabel = "Вестница",
                    summary = "Тонкая душа на границе светлых троп.",
                    bondLevel = 61,
                    bondTier = "trusted",
                    canGrantCompanionRelic = true,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = true,
                    isPresent = true,
                    abodeDisposition = new
                    {
                        powerSensitivity = "high",
                        migrationDisposition = "selective",
                        communalOrientation = "medium",
                        stabilityNeed = "high"
                    },
                    abodeDevotionLevel = 12,
                    abodeDevotionTier = "alienated",
                    restlessness = 79,
                    migrationState = "ready_to_transfer",
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец при храме дорог.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                }
            },
            transferReceipts = Array.Empty<object>(),
            interactionReceipts = Array.Empty<object>(),
            historyLog = Array.Empty<object>(),
            thoughtJournal = Array.Empty<object>(),
            interactionLog = Array.Empty<object>()
        };
        var preTurnResidentState = new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    roleLabel = "Вестница",
                    summary = "Тонкая душа на границе светлых троп.",
                    bondLevel = 61,
                    bondTier = "trusted",
                    canGrantCompanionRelic = true,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = true,
                    isPresent = true,
                    abodeDisposition = new
                    {
                        powerSensitivity = "high",
                        migrationDisposition = "selective",
                        communalOrientation = "medium",
                        stabilityNeed = "high"
                    },
                    abodeDevotionLevel = 24,
                    abodeDevotionTier = "uncertain",
                    restlessness = 60,
                    migrationState = "considering_departure",
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец при храме дорог.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                }
            },
            transferReceipts = Array.Empty<object>(),
            interactionReceipts = Array.Empty<object>(),
            historyLog = Array.Empty<object>(),
            thoughtJournal = Array.Empty<object>(),
            interactionLog = Array.Empty<object>()
        };
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, currentResidentState);

        var request = new
        {
            requestId = "resident_transfer_req_invalid_preturn_gate",
            residentId = "resident_alpha_1",
            residentName = "Лиора",
            sourceGuardianId = "guardian_alpha",
            sourceGuardianName = "Азалия",
            sourceAbodeId = "abode_alpha",
            sourceAbodeName = "Лазурная Обитель",
            targetGuardianId = "guardian_beta",
            targetGuardianName = "Мириэль",
            targetAbodeId = "abode_beta",
            targetAbodeName = "Сад Перекрёстков",
            abodeDevotionLevel = 12,
            abodeDevotionTier = "alienated",
            restlessness = 79,
            migrationState = "ready_to_transfer",
            transferMode = "accepted_transfer",
            createdAtTurn = 12,
            createdAtUtc = "2026-04-16T04:12:00Z"
        };
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingTransfersRequestPath, new { requests = new[] { request } });

        var soulStateBackupPath = "test_backups/resident_transfer_invalid_preturn_gate_soul_state.json";
        var residentBackupPath = "test_backups/resident_transfer_invalid_preturn_gate_residents.json";
        await WriteJsonAsync(soulStateBackupPath, new { currentRealm = "Chaos Sea" });
        await WriteJsonAsync(residentBackupPath, preTurnResidentState);
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["game_state/meta/soul_state.json"] = soulStateBackupPath,
            [GuardianAbodeResidentState.StatePath] = residentBackupPath
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "abode_resident_transfer_invalid_preturn_eligibility", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_AcceptedResidentTransferCanonicalArrivalDoesNotTripGenericDriftMismatch()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            currentRealm = "Chaos Sea"
        });

        await WriteJsonAsync("game_state/meta/guardians.json", BuildTwoGuardianState());

        var preTurnResident = JsonNode.Parse("""
        {
          "residentId": "resident_alpha_1",
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "abodeId": "abode_alpha",
          "abodeName": "Лазурная Обитель",
          "displayName": "Лиора",
          "residentKind": "wayfaring_soul",
          "originType": "traveler_soul",
          "roleLabel": "Вестница",
          "summary": "Тонкая душа на границе светлых троп.",
          "bondLevel": 61,
          "bondTier": "trusted",
          "canGrantCompanionRelic": true,
          "bondRewardState": "none",
          "linkedSoulQuestId": "",
          "grantedRelicId": "",
          "historyRevealed": true,
          "isPresent": true,
          "abodeDisposition": {
            "powerSensitivity": "high",
            "migrationDisposition": "selective",
            "communalOrientation": "medium",
            "stabilityNeed": "high"
          },
          "abodeDevotionLevel": 12,
          "abodeDevotionTier": "alienated",
          "restlessness": 79,
          "migrationState": "ready_to_transfer",
          "mortalWorldImprint": {
            "originWorldSummary": "Бывшая гонец при храме дорог.",
            "futureCompanionPrompt": "Swift wanderer"
          }
        }
        """)!.AsObject();
        var arrivalSeed = preTurnResident.DeepClone().AsObject();
        GuardianAbodeResidentState.NormalizeResidentObject(arrivalSeed);
        arrivalSeed["guardianId"] = "guardian_beta";
        arrivalSeed["guardianName"] = "Мириэль";
        arrivalSeed["abodeId"] = "abode_beta";
        arrivalSeed["abodeName"] = "Сад Перекрёстков";
        var canonicalArrival = GuardianAbodeResidentState.BuildCanonicalTransferArrivalResident(arrivalSeed, targetAbodePower: 74);

        var currentResidentsRoot = new JsonObject
        {
            ["entries"] = new JsonArray(canonicalArrival),
            ["transferReceipts"] = new JsonArray(
                new JsonObject
                {
                    ["requestId"] = "resident_transfer_req_accepted",
                    ["residentId"] = "resident_alpha_1",
                    ["residentName"] = "Лиора",
                    ["sourceGuardianId"] = "guardian_alpha",
                    ["sourceGuardianName"] = "Азалия",
                    ["sourceAbodeId"] = "abode_alpha",
                    ["sourceAbodeName"] = "Лазурная Обитель",
                    ["targetGuardianId"] = "guardian_beta",
                    ["targetGuardianName"] = "Мириэль",
                    ["targetAbodeId"] = "abode_beta",
                    ["targetAbodeName"] = "Сад Перекрёстков",
                    ["status"] = "accepted",
                    ["transferMode"] = "accepted_transfer",
                    ["departureHistoryEntryId"] = "hist_departure_1",
                    ["arrivalHistoryEntryId"] = "hist_arrival_1",
                    ["resolvedAtTurn"] = 12,
                    ["resolvedAtUtc"] = "2026-04-16T04:15:00Z"
                }),
            ["interactionReceipts"] = new JsonArray(),
            ["historyLog"] = new JsonArray(
                new JsonObject
                {
                    ["entryId"] = "hist_departure_1",
                    ["residentId"] = "resident_alpha_1",
                    ["title"] = "Лиора покинула Обитель",
                    ["summary"] = "Лиора оставила Лазурную Обитель и шагнула к иному свету.",
                    ["tags"] = new JsonArray("departure", "transfer"),
                    ["revealedAtTurn"] = 12,
                    ["revealedAtUtc"] = "2026-04-16T04:15:00Z"
                },
                new JsonObject
                {
                    ["entryId"] = "hist_arrival_1",
                    ["residentId"] = "resident_alpha_1",
                    ["title"] = "Лиора прибыла в новую Обитель",
                    ["summary"] = "Лиора обрела новое пристанище в Саду Перекрёстков.",
                    ["tags"] = new JsonArray("arrival", "transfer"),
                    ["revealedAtTurn"] = 12,
                    ["revealedAtUtc"] = "2026-04-16T04:15:00Z"
                }),
            ["thoughtJournal"] = new JsonArray(),
            ["interactionLog"] = new JsonArray()
        };
        await _fs.WriteFileAtomicAsync(
            GuardianAbodeResidentState.StatePath,
            currentResidentsRoot.ToJsonString());

        var request = new
        {
            requestId = "resident_transfer_req_accepted",
            residentId = "resident_alpha_1",
            residentName = "Лиора",
            sourceGuardianId = "guardian_alpha",
            sourceGuardianName = "Азалия",
            sourceAbodeId = "abode_alpha",
            sourceAbodeName = "Лазурная Обитель",
            targetGuardianId = "guardian_beta",
            targetGuardianName = "Мириэль",
            targetAbodeId = "abode_beta",
            targetAbodeName = "Сад Перекрёстков",
            abodeDevotionLevel = 12,
            abodeDevotionTier = "alienated",
            restlessness = 79,
            migrationState = "ready_to_transfer",
            transferMode = "accepted_transfer",
            createdAtTurn = 12,
            createdAtUtc = "2026-04-16T04:12:00Z"
        };

        var requestBackupPath = "test_backups/resident_transfer_req_accepted.json";
        var residentBackupPath = "test_backups/resident_transfer_state_accepted.json";
        await WriteJsonAsync(requestBackupPath, new { requests = new[] { request } });
        await _fs.WriteFileAtomicAsync(
            residentBackupPath,
            new JsonObject
            {
                ["entries"] = new JsonArray(preTurnResident),
                ["transferReceipts"] = new JsonArray(),
                ["interactionReceipts"] = new JsonArray(),
                ["historyLog"] = new JsonArray(),
                ["thoughtJournal"] = new JsonArray(),
                ["interactionLog"] = new JsonArray()
            }.ToJsonString());
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GuardianAbodeResidentRequestState.PendingTransfersRequestPath] = requestBackupPath,
            [GuardianAbodeResidentState.StatePath] = residentBackupPath
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "abode_resident_transfer_invalid_accepted_resolution", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "abode_resident_devotion_projection_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ResidentWithInvalidInteractionToken_Fails()
    {
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    bondLevel = 30,
                    bondTier = "familiar",
                    canGrantCompanionRelic = false,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = false,
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец.",
                        futureCompanionPrompt = "Swift wanderer"
                    },
                    availableInteractions = new[] { "talkk" }
                }
            }
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "guardian_abode_resident_invalid_interaction_token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ResidentWithUnknownGuardian_Fails()
    {
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_unknown",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    bondLevel = 30,
                    bondTier = "familiar",
                    canGrantCompanionRelic = false,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = false,
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                }
            }
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "guardian_abode_resident_unknown_guardian_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PendingManifestationDuplicateRelicId_Fails()
    {
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "manifest_1",
                    manifestationSource = "imprint_relic",
                    relicId = "relic_dup_1",
                    relicName = "Двойной зов",
                    sourceImprintId = "imprint_1",
                    targetIncarnation = 2,
                    companionNameHint = "Тарен",
                    originWorldSummary = "Первый след.",
                    futureCompanionPrompt = "Faithful ally",
                    createdAtUtc = "2026-03-27T00:00:00Z"
                },
                new
                {
                    requestId = "manifest_2",
                    manifestationSource = "imprint_relic",
                    relicId = "relic_dup_1",
                    relicName = "Двойной зов",
                    sourceImprintId = "imprint_2",
                    targetIncarnation = 2,
                    companionNameHint = "Элис",
                    originWorldSummary = "Второй след.",
                    futureCompanionPrompt = "Faithful ally",
                    createdAtUtc = "2026-03-27T00:00:01Z"
                }
            }
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "pending_resident_companion_manifestation_duplicate_relic_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RosterRequestWithoutCanonicalReceipt_Fails()
    {
        await WriteJsonAsync("game_state/meta/guardians.json", BuildGuardianState());
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    bondLevel = 30,
                    bondTier = "familiar",
                    canGrantCompanionRelic = false,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = false,
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                }
            }
        });

        var request = new
        {
            requestId = "abode_req_1",
            guardianId = "guardian_alpha",
            guardianName = "Азалия",
            abodeId = "abode_alpha",
            abodeName = "Сад Нитей",
            currentReputation = 80,
            createdAtTurn = 12,
            createdAtUtc = "2026-03-27T00:00:00Z"
        };
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingResidentsRequestPath, new { requests = new[] { request } });
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new { currentRealm = "Chaos Sea" });
        const string requestBackupPath = "game_state/control/pending_turn_snapshot/pre_pending_guardian_abode_residents_request.json";
        await WriteJsonAsync(requestBackupPath, new { requests = new[] { request } });
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GuardianAbodeResidentRequestState.PendingResidentsRequestPath] = requestBackupPath
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => HasExpectedOrGuardianAuthorityGate(issue, "abode_resident_roster_missing_receipt_resolution"));
    }

    [Fact]
    public async Task ValidateGameStateAsync_GuardianThoughtJournalWithUnknownGuardian_Fails()
    {
        await WriteJsonAsync(GuardianThoughtJournalState.StatePath, new
        {
            entries = new[]
            {
                new
                {
                    entryId = "gthought_1",
                    guardianId = "guardian_missing",
                    turn = 12,
                    timestamp = "2026-03-27T10:00:00Z",
                    title = "Скрытая оценка",
                    summary = "Хранитель пока не верит душе."
                }
            }
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "guardian_thought_unknown_guardian_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_NpcInteractionJournalWithUnknownNpc_Fails()
    {
        await WriteJsonAsync(NpcInteractionJournalState.StatePath, new
        {
            entries = new[]
            {
                new
                {
                    entryId = "npc_event_1",
                    npcId = "npc_missing",
                    turn = 12,
                    timestamp = "2026-03-27T10:00:00Z",
                    title = "Сделка не состоялась",
                    summary = "Торговец отказался продавать амулет."
                }
            }
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "npc_interaction_journal_unknown_npc_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ResidentThoughtJournalWithUnknownResident_Fails()
    {
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = Array.Empty<object>(),
            thoughtJournal = new[]
            {
                new
                {
                    entryId = "rthought_1",
                    residentId = "resident_missing",
                    turn = 12,
                    timestamp = "2026-03-27T10:00:00Z",
                    title = "Ждёт ответа",
                    summary = "Резидент не знает, доверять ли душе."
                }
            }
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "resident_thought_unknown_resident_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MalformedArchiveConsultationRequestFile_ReportMalformedControlFile()
    {
        await _fs.WriteFileAtomicAsync(
            AfterlifeArchiveActionState.ConsultationRequestPath,
            """
            {
              "requestId": "consult_req_broken",
              "guardianId":
            """
        );

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "archive_consultation_request_malformed_file", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MalformedArchiveProjectFuelRequestFile_ReportMalformedControlFile()
    {
        await _fs.WriteFileAtomicAsync(
            AfterlifeArchiveActionState.ProjectFuelRequestPath,
            """
            {
              "requestId": "project_fuel_req_broken",
              "guardianId":
            """
        );

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "archive_project_fuel_request_malformed_file", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MetaStateUpdatesRejectsInvalidInkFeatherBuckets()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            metaStateUpdates = new
            {
                inkFeatherChanges = new
                {
                    add = "broken",
                    spend = -1,
                    bonus = 3
                }
            }
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "meta_state_invalid_ink_feather_change_value", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "meta_state_unknown_ink_feather_change_key", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MetaStateUpdatesRejectsMalformedEnlightenmentProgression()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            metaStateUpdates = new
            {
                enlightenmentProgression = new
                {
                    newTier = -1,
                    bonus = 3
                }
            }
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "meta_state_invalid_enlightenment_progression_value", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "meta_state_unknown_enlightenment_progression_key", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "meta_state_missing_enlightenment_progression_experience", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MetaStateUpdatesRejectsMalformedSoulRelicOperations()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            metaStateUpdates = new
            {
                soulRelicOperations = new
                {
                    addRelic = new
                    {
                        name = "Broken relic"
                    },
                    updateRelicField = new
                    {
                        relicId = "relic_alpha"
                    },
                    changeRelic = new
                    {
                        relicId = "relic_beta"
                    }
                }
            }
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "missing_required_string", StringComparison.OrdinalIgnoreCase) &&
                                         issue.FilePath.Contains("metaStateUpdates.soulRelicOperations.addRelic.relicId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "missing_required_string", StringComparison.OrdinalIgnoreCase) &&
                                         issue.FilePath.Contains("metaStateUpdates.soulRelicOperations.updateRelicField.field", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "meta_state_unknown_soul_relic_operation_key", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MetaStateUpdatesRejectsUnknownTopLevelCommand()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            metaStateUpdates = new
            {
                unknownCommand = new
                {
                    value = 1
                }
            }
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "meta_state_unknown_top_level_update_key", StringComparison.OrdinalIgnoreCase));
    }

    private async Task WritePendingTurnSnapshotManifestAsync(Dictionary<string, string> rollbackBackups)
    {
        var normalizedBackups = rollbackBackups.ToDictionary(
            pair => NormalizeRelativePath(pair.Key),
            pair => NormalizeRelativePath(pair.Value),
            StringComparer.OrdinalIgnoreCase);

        await AddCurrentGuardianBaselinesIfPresentAsync(normalizedBackups);

        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId = "session-test",
            requestId = "request-test",
            turnNumber = 12
        });

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
            RollbackBackups = normalizedBackups,
            RollbackBaselineFiles = normalizedBackups.Keys
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            SourceLabel = "обработки хода",
            ManifestPayloadHash = string.Empty
        };

        await RegisterSnapshotFilesAsync(manifest);
        manifest.ManifestPayloadHash = ComputeManifestPayloadHash(manifest);
        await WriteJsonAsync("game_state/control/pending_turn_snapshot.json", manifest);
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private async Task AddCurrentGuardianBaselinesIfPresentAsync(Dictionary<string, string> rollbackBackups)
    {
        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        if (string.IsNullOrWhiteSpace(guardiansJson))
            return;

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        if (string.IsNullOrWhiteSpace(trackerJson))
        {
            trackerJson = """
            {
              "activeProjects": [],
              "completedProjects": []
            }
            """;
            await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, trackerJson);
        }

        await _fs.WriteFileAtomicAsync("test_backups/guardian_archive_trade_auto_guardians.json", guardiansJson);
        await _fs.WriteFileAtomicAsync("test_backups/guardian_archive_trade_auto_tracker.json", trackerJson);

        rollbackBackups.TryAdd("game_state/meta/guardians.json", "test_backups/guardian_archive_trade_auto_guardians.json");
        rollbackBackups.TryAdd(GuardianProjectState.TrackerPath, "test_backups/guardian_archive_trade_auto_tracker.json");
    }

    private async Task RegisterSnapshotFilesAsync(PendingTurnSnapshotManifest manifest)
    {
        foreach (var pair in manifest.RollbackBackups)
        {
            var snapshotPath = $"game_state/control/pending_turn_snapshot/{pair.Key}";
            var snapshotJson = await _fs.ReadFileAsync(snapshotPath);
            if (string.IsNullOrWhiteSpace(snapshotJson))
            {
                snapshotJson = await _fs.ReadFileAsync(pair.Value);
                if (string.IsNullOrWhiteSpace(snapshotJson))
                    continue;

                await _fs.WriteFileAtomicAsync(snapshotPath, snapshotJson);
            }

            manifest.Files[pair.Key] = snapshotPath;
            manifest.SnapshotFileHashes[pair.Key] = ComputeSha256(snapshotJson);
        }

        var snapshotRoot = _fs.ResolvePath("game_state/control/pending_turn_snapshot");
        if (!Directory.Exists(snapshotRoot))
            return;

        foreach (var snapshotFile in Directory.GetFiles(snapshotRoot, "*", SearchOption.AllDirectories))
        {
            var relativeSnapshotPath = NormalizeRelativePath(Path.GetRelativePath(snapshotRoot, snapshotFile));
            if (!relativeSnapshotPath.Contains('/'))
                continue;

            var trackedPath = relativeSnapshotPath;
            if (manifest.Files.ContainsKey(trackedPath))
                continue;

            var snapshotJson = await File.ReadAllTextAsync(snapshotFile);
            if (string.IsNullOrWhiteSpace(snapshotJson))
                continue;

            manifest.Files[trackedPath] = $"game_state/control/pending_turn_snapshot/{trackedPath}";
            manifest.SnapshotFileHashes[trackedPath] = ComputeSha256(snapshotJson);
        }
    }

    private async Task EnsureCurrentGuardianAndTrackerValidatedBaselinesAsync()
    {
        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        if (string.IsNullOrWhiteSpace(guardiansJson))
            return;

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        if (string.IsNullOrWhiteSpace(trackerJson))
        {
            trackerJson = """
            {
              "activeProjects": [],
              "completedProjects": []
            }
            """;
            await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, trackerJson);
        }

        await _fs.WriteFileAtomicAsync("test_backups/guardian_archive_trade_auto_guardians.json", guardiansJson);
        await _fs.WriteFileAtomicAsync("test_backups/guardian_archive_trade_auto_tracker.json", trackerJson);

        var rollbackBackups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var manifestPath = _fs.ResolvePath("game_state/control/pending_turn_snapshot.json");
        if (File.Exists(manifestPath))
        {
            var existingManifestJson = await File.ReadAllTextAsync(manifestPath);
            if (!string.IsNullOrWhiteSpace(existingManifestJson) &&
                JsonSerializer.Deserialize<PendingTurnSnapshotManifest>(existingManifestJson) is { RollbackBackups: not null } existingManifest)
            {
                foreach (var pair in existingManifest.RollbackBackups)
                    rollbackBackups[NormalizeRelativePath(pair.Key)] = NormalizeRelativePath(pair.Value);
            }
        }

        rollbackBackups["game_state/meta/guardians.json"] = "test_backups/guardian_archive_trade_auto_guardians.json";
        rollbackBackups[GuardianProjectState.TrackerPath] = "test_backups/guardian_archive_trade_auto_tracker.json";

        await WritePendingTurnSnapshotManifestAsync(rollbackBackups);
    }

    private static object BuildGuardianState(
        string guardianId = "guardian_alpha",
        string canonicalName = "Азалия",
        string abodeId = "abode_alpha",
        string abodeName = "Сад Нитей",
        object? questManagement = null)
    {
        var guardian = BuildGuardianObject(guardianId, canonicalName, abodeId, abodeName, questManagement);
        return new
        {
            guardians = new[] { guardian },
            activeGuardian = guardian,
            chaosSeaNavigation = new
            {
                currentAbodeId = abodeId,
                discoveredAbodes = new[] { abodeId }
            }
        };
    }

    private static object BuildChaosSeaSoulState() => new
    {
        soulName = "Тестовая Душа",
        currentRealm = "Chaos Sea",
        currentIncarnation = 1,
        enlightenment = new
        {
            currentTier = "Новичок",
            experience = 0,
            level = 0
        },
        inkFeathers = new
        {
            current = 0,
            total = 0
        },
        soulRelics = new
        {
            equipped = Array.Empty<object>(),
            stored = Array.Empty<object>()
        },
        afterlifeArchive = new
        {
            stored = Array.Empty<object>()
        },
        livesHistory = Array.Empty<object>(),
        pendingMemoryLegacy = (object?)null
    };

    private static object BuildTwoGuardianState()
    {
        var alpha = BuildGuardianObject(
            "guardian_alpha",
            "Азалия",
            "abode_alpha",
            "Лазурная Обитель",
            guardianRelationships: new[] { BuildGuardianRelationship("guardian_beta", "Мириэль") });
        var beta = BuildGuardianObject(
            "guardian_beta",
            "Мириэль",
            "abode_beta",
            "Сад Перекрёстков",
            guardianRelationships: new[] { BuildGuardianRelationship("guardian_alpha", "Азалия") });
        return new
        {
            guardians = new[] { alpha, beta },
            activeGuardian = alpha,
            chaosSeaNavigation = new
            {
                currentAbodeId = "abode_alpha",
                discoveredAbodes = new[] { "abode_alpha", "abode_beta" }
            }
        };
    }

    private static object BuildGuardianObject(
        string guardianId,
        string canonicalName,
        string abodeId,
        string abodeName,
        object? questManagement = null,
        object[]? guardianRelationships = null)
    {
        return new
        {
            guardianId,
            canonicalName,
            domain = "Порог Сна",
            nameVariants = new
            {
                @default = canonicalName,
                feminine = canonicalName,
                masculine = canonicalName,
                neutral = canonicalName
            },
            manifestation = new
            {
                currentDisplayName = canonicalName,
                formFlexibility = "selective",
                currentPresentationStyle = "feminine",
                currentPronouns = "она/её",
                appearanceDescription = "Тестовая форма."
            },
            manifestationHistory = Array.Empty<object>(),
            personalityProfile = new
            {
                archetype = "Тестовый хранитель",
                speechPattern = "Спокойная речь",
                coreValues = new[] { "память", "долг", "связь" }
            },
            relationshipData = new
            {
                currentReputation = 0,
                reputationHistory = Array.Empty<object>(),
                lastInteraction = "2026-03-24T00:00:00Z"
            },
            abodePower = new
            {
                currentPower = 35,
                tier = "Хрупкая",
                lastUpdatedAt = "2026-03-24T00:00:00Z",
                history = Array.Empty<object>()
            },
            abode = new
            {
                abodeId,
                name = abodeName,
                isDiscovered = true
            },
            guardianRelationships = guardianRelationships ?? Array.Empty<object>(),
            mood = new
            {
                current = "contemplative",
                intensity = 10,
                reason = "Тестовое состояние.",
                since = 0
            },
            loreFragments = BuildGuardianLoreFragments(guardianId),
            questManagement = questManagement ?? EmptyGuardianQuestManagement(),
            gachaSystem = new
            {
                chargesPerReturn = 1,
                chargesUsedThisReturn = 0,
                gachaHistory = Array.Empty<object>()
            }
        };
    }

    private static object BuildGuardianRelationship(string targetGuardianId, string targetName) => new
    {
        targetGuardianId,
        targetName,
        reason = "Тестовая нейтральная связь.",
        attitudeScore = 0,
        attitudeTier = "neutral",
        lastChangedAt = "2026-03-24T00:00:00Z",
        awarenessLevel = "known"
    };

    private static object[] BuildGuardianLoreFragments(string guardianId)
    {
        var categories = new[]
        {
            "personal_history",
            "cosmic_secret",
            "domain_mastery",
            "lost_world",
            "other_guardians",
            "soul_mechanics",
            "personal_history"
        };
        var thresholds = new[] { 0, 50, 130, 230, 0, 50, 130 };
        return Enumerable.Range(0, 7)
            .Select(index => new
            {
                fragmentId = $"{guardianId}_lore_{index + 1}",
                category = categories[index],
                title = $"Фрагмент {index + 1}",
                content = (string?)null,
                requiredReputation = thresholds[index]
            })
            .Cast<object>()
            .ToArray();
    }

    private static object EmptyGuardianQuestManagement() => new
    {
        availableQuests = Array.Empty<object>(),
        activeQuests = Array.Empty<object>(),
        completedQuests = Array.Empty<object>()
    };

    private static bool HasExpectedOrGuardianAuthorityGate(ValidationIssue issue, params string[] expectedCodes)
    {
        if (expectedCodes.Any(code => string.Equals(issue.Code, code, StringComparison.OrdinalIgnoreCase)))
            return true;

        return string.Equals(issue.Code, "guardian_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "realm_segregation_invalid_validated_snapshot_realm", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "realm_segregation_missing_validated_preturn_realm", StringComparison.OrdinalIgnoreCase);
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

    private static string ComputeSha256(string content)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(content)));
    }

    private static string NormalizeRelativePath(string path) => path.Replace('\\', '/');

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
