using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class CanonicalStateNormalizerTests : IDisposable
{
    [Fact]
    public void TryResolveGuardianProjectAuthoritySoulContext_RecordLifeCompletionWithCanonicalTriggerLifeEnd_IsReadable()
    {
        const string soulStateJson = """
        {
          "currentRealm": "Mortal World",
          "currentIncarnation": 3,
          "metaStateUpdates": {
            "lifeTransitions": {
              "recordLifeCompletion": {
                "characterFinalState": { "causeOfDeath": "Test" },
                "majorAchievements": [],
                "relationshipsFormed": [],
                "moralChoices": [],
                "skillsLearned": [],
                "enlightenmentGained": 0
              }
            }
          }
        }
        """;

        const string lifeTransitionsJson = """
        {
          "reason": "Death",
          "summary": "Жизнь завершена."
        }
        """;

        const string preTurnSoulStateJson = """
        {
          "currentRealm": "Mortal World",
          "currentIncarnation": 3
        }
        """;

        var success = CanonicalStateNormalizer.TryResolveGuardianProjectAuthoritySoulContext(
            soulStateJson,
            preTurnSoulStateJson,
            lifeTransitionsJson,
            currentTurn: 12,
            new GuardianProjectSoulContextRequirements(
                RequiresCurrentIncarnation: true,
                RequiresCurrentRealm: true),
            out var currentIncarnation,
            out var currentRealm,
            out var failureDescription);

        Assert.True(success, failureDescription);
        Assert.Equal(3, currentIncarnation);
        Assert.Equal("Mortal World", currentRealm);
    }

    [Fact]
    public void TryResolveGuardianProjectAuthoritySoulContext_RecordLifeCompletionWithoutCanonicalTriggerLifeEnd_FailsReadable()
    {
        const string soulStateJson = """
        {
          "currentRealm": "Mortal World",
          "currentIncarnation": 3,
          "metaStateUpdates": {
            "lifeTransitions": {
              "recordLifeCompletion": {
                "characterFinalState": { "causeOfDeath": "Test" },
                "majorAchievements": [],
                "relationshipsFormed": [],
                "moralChoices": [],
                "skillsLearned": [],
                "enlightenmentGained": 0
              }
            }
          }
        }
        """;

        var success = CanonicalStateNormalizer.TryResolveGuardianProjectAuthoritySoulContext(
            soulStateJson,
            null,
            null,
            currentTurn: 12,
            new GuardianProjectSoulContextRequirements(
                RequiresCurrentIncarnation: true,
                RequiresCurrentRealm: true),
            out _,
            out _,
            out var failureDescription);

        Assert.False(success);
        Assert.Contains("recordLifeCompletion", failureDescription, StringComparison.Ordinal);
    }

    [Fact]
    public void HasCanonicalTriggerLifeEnd_MissingSummary_ReturnsFalse()
    {
        const string lifeTransitionsJson = """
        {
          "reason": "Death"
        }
        """;

        Assert.False(CanonicalStateNormalizer.HasCanonicalTriggerLifeEnd(lifeTransitionsJson));
    }

    [Fact]
    public void HasCanonicalTriggerLifeEnd_NonStringSummary_ReturnsFalse()
    {
        const string lifeTransitionsJson = """
        {
          "reason": "Death",
          "summary": 123
        }
        """;

        Assert.False(CanonicalStateNormalizer.HasCanonicalTriggerLifeEnd(lifeTransitionsJson));
    }

    [Fact]
    public void HasCanonicalTriggerLifeEnd_UnknownVisibleKey_ReturnsFalse()
    {
        const string lifeTransitionsJson = """
        {
          "reason": "Death",
          "summary": "Жизнь завершена.",
          "unexpected": true
        }
        """;

        Assert.False(CanonicalStateNormalizer.HasCanonicalTriggerLifeEnd(lifeTransitionsJson));
    }

    [Fact]
    public void HasLifecycleAuthorizedTriggerLifeEnd_MortalRealm_ReturnsTrue()
    {
        const string lifeTransitionsJson = """
        {
          "reason": "Death",
          "summary": "Жизнь завершена."
        }
        """;

        Assert.True(CanonicalStateNormalizer.HasLifecycleAuthorizedTriggerLifeEnd(
            lifeTransitionsJson,
            "Mortal World"));
    }

    [Fact]
    public void HasLifecycleAuthorizedTriggerLifeEnd_AfterlifeRealm_ReturnsFalse()
    {
        const string lifeTransitionsJson = """
        {
          "reason": "Death",
          "summary": "Жизнь завершена."
        }
        """;

        Assert.False(CanonicalStateNormalizer.HasLifecycleAuthorizedTriggerLifeEnd(
            lifeTransitionsJson,
            "Chaos Sea"));
    }

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
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

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
    public async Task NormalizeAccumulatedStateAsync_BackupDerivedCurrentLoreIncarnationWithMalformedCanonicalCurrentSoulState_FailsBeforeEarlierWrites()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "inkFeathers": {
            "current": 19,
            "foo": 1
          }
        }
        """);

        const string trackerBackupPath = "test_backups/preturn_tracker_backup_derived_current_lore_malformed_canonical_soul_state.json";
        const string guardiansBackupPath = "test_backups/preturn_guardians_backup_derived_current_lore_malformed_canonical_soul_state.json";
        const string soulStateBackupPath = "test_backups/preturn_soul_state_backup_derived_current_lore_malformed_canonical_soul_state.json";
        var trackerJson = """
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_backup_derived_current_lore_malformed_canonical_soul",
                "projectType": "lore_research",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Backup-derived current lore with malformed canonical soul_state",
                "finalState": "Completed",
                "projectOutcomeAudit": {
                  "questHookCount": 1,
                  "visibleRivalClueBonus": 0
                },
                "effectState": {
                  "targetIncarnation": 3,
                  "questHookTokensGranted": 1,
                  "questHookTokensSpent": 0,
                  "guaranteedArchiveQuestGranted": 0,
                  "guaranteedArchiveQuestConsumed": 0,
                  "specialQuestLineTokensGranted": 0,
                  "specialQuestLineTokensSpent": 0,
                  "visibleRivalClueBudgetGranted": 0,
                  "visibleRivalClueBudgetSpent": 0
                }
              }
            }
          ],
          "temporaryProjectModifiers": []
        }
        """;

        await _fs.WriteFileAtomicAsync(trackerBackupPath, trackerJson);
        await _fs.WriteFileAtomicAsync(guardiansBackupPath, """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "questManagement": {
                "availableQuests": [],
                "activeQuests": [],
                "completedQuests": []
              },
              "gachaSystem": {
                "chargesPerReturn": 0,
                "chargesUsedThisReturn": 0,
                "gachaHistory": []
              }
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync(soulStateBackupPath, """
        {
          "currentIncarnation": 2,
          "currentRealm": "Chaos Sea",
          "inkFeathers": {
            "current": 19,
            "total": 19
          }
        }
        """);
        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, trackerJson);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [GuardianProjectState.TrackerPath] = trackerBackupPath,
                ["game_state/meta/guardians.json"] = guardiansBackupPath,
                ["game_state/meta/soul_state.json"] = soulStateBackupPath
            }));

        Assert.Contains("readable current soul_state.json authority surface", exception.Message, StringComparison.OrdinalIgnoreCase);
        var currentTrackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var soulStateJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        Assert.Equal(trackerJson, currentTrackerJson);
        Assert.NotNull(soulStateJson);
        Assert.Contains("\"foo\": 1", soulStateJson, StringComparison.Ordinal);
        Assert.False(_fs.FileExists("game_state/meta/guardians.json"));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_OffensiveTargetStrike_PreservesSourceProjectIdentityInAudit()
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
                "projectId": "proj_offense_identity",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Теневая интрига идентичности",
                "activeState": "Triggering the decisive breach",
                "totalWork": 18,
                "workDone": 18,
                "totalStages": 3,
                "currentStage": 3,
                "pressure": 12,
                "stability": 70,
                "targetGuardianId": "guardian_beta"
              }
            }
          ],
          "completeGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_offense_identity",
              "finalState": "Completed",
              "outcome": "Интрига сломала защиту rival-Обители.",
              "targetGuardianId": "guardian_beta"
            }
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var journalJson = await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath);
        Assert.NotNull(journalJson);

        using var journalDoc = System.Text.Json.JsonDocument.Parse(journalJson!);
        var strikeEntry = journalDoc.RootElement.GetProperty("entries").EnumerateArray()
            .Single(entry =>
                string.Equals(entry.GetProperty("reasonType").GetString(), "rival_strike", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.GetProperty("guardianId").GetString(), "guardian_beta", StringComparison.OrdinalIgnoreCase));
        var audit = strikeEntry.GetProperty("audit");

        Assert.Equal("guardian_alpha", audit.GetProperty("projectGuardianId").GetString());
        Assert.Equal("proj_offense_identity", audit.GetProperty("projectId").GetString());
        Assert.Equal("Теневая интрига идентичности", audit.GetProperty("projectName").GetString());
        Assert.Equal("offensive_intrigue", audit.GetProperty("projectType").GetString());
        Assert.Equal("major", audit.GetProperty("projectTier").GetString());
        Assert.Equal("Completed", audit.GetProperty("finalState").GetString());
        Assert.True(audit.GetProperty("targetLoss").GetInt32() > 0);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_DuplicateGuardianStarts_DoNotOverwriteExistingActiveProject()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 43 }
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
                "projectId": "proj_existing",
                "projectType": "abode_expansion",
                "projectTier": "major",
                "projectMode": "internal",
                "projectName": "Существующий проект",
                "activeState": "Maintaining the current lattice",
                "totalWork": 18,
                "workDone": 6,
                "totalStages": 3,
                "currentStage": 1,
                "pressure": 4,
                "stability": 72
              }
            }
          ],
          "startGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_shadow_one",
                "projectType": "abode_expansion",
                "projectTier": "major",
                "projectMode": "internal",
                "projectName": "Первый конфликтный старт",
                "activeState": "Building the first competing ring",
                "totalWork": 12,
                "workDone": 0,
                "totalStages": 2,
                "currentStage": 0,
                "pressure": 0,
                "stability": 100
              }
            },
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_shadow_two",
                "projectType": "lore_research",
                "projectTier": "major",
                "projectMode": "internal",
                "projectName": "Второй конфликтный старт",
                "activeState": "Charting rival echoes",
                "totalWork": 12,
                "workDone": 0,
                "totalStages": 2,
                "currentStage": 0,
                "pressure": 0,
                "stability": 100
              }
            }
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var journalJson = await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath);

        Assert.NotNull(trackerJson);
        Assert.Contains("\"proj_existing\"", trackerJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"proj_shadow_one\"", trackerJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"proj_shadow_two\"", trackerJson, StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(journalJson))
        {
            Assert.DoesNotContain("proj_shadow_one", journalJson, StringComparison.Ordinal);
            Assert.DoesNotContain("proj_shadow_two", journalJson, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_DuplicateGuardianStarts_DoNotMaterializePhantomUpdateOrCompletion()
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
          "startGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_shadow_one",
                "projectType": "abode_expansion",
                "projectTier": "major",
                "projectMode": "internal",
                "projectName": "Первый конфликтный старт",
                "activeState": "Building the first competing ring",
                "totalWork": 12,
                "workDone": 0,
                "totalStages": 2,
                "currentStage": 0,
                "pressure": 0,
                "stability": 100
              }
            },
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_shadow_two",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Второй конфликтный старт",
                "targetGuardianId": "guardian_beta",
                "activeState": "Charting rival echoes",
                "totalWork": 12,
                "workDone": 0,
                "totalStages": 2,
                "currentStage": 0,
                "pressure": 0,
                "stability": 100
              }
            }
          ],
          "guardianProjectUpdates": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_shadow_one",
              "activeState": "Attempting a phantom update",
              "workDone": 5
            }
          ],
          "completeGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_shadow_two",
              "finalState": "Completed",
              "outcome": "Попытка завершить фантомный проект.",
              "targetGuardianId": "guardian_beta",
              "offensiveImpactAudit": {
                "targetLoss": 3
              }
            }
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var journalJson = await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath);

        Assert.NotNull(trackerJson);
        Assert.DoesNotContain("\"proj_shadow_one\"", trackerJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"proj_shadow_two\"", trackerJson, StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(journalJson))
        {
            Assert.DoesNotContain("proj_shadow_one", journalJson, StringComparison.Ordinal);
            Assert.DoesNotContain("proj_shadow_two", journalJson, StringComparison.Ordinal);
        }
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
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

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
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

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
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

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

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "guardian_project_missing_project_outcome_audit", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "guardian_project_missing_offensive_impact_audit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_NonOffensiveCompletion_IgnoresSmuggledOffensiveAudit()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 58 }
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
              "abodePower": { "currentPower": 44, "tier": "Стабильная", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
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
              "abodePower": { "currentPower": 61, "tier": "Могущественная", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
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
            "abodePower": { "currentPower": 44, "tier": "Стабильная", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
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
                "projectId": "proj_non_offensive_smuggle",
                "projectType": "abode_expansion",
                "projectTier": "major",
                "projectMode": "internal",
                "projectName": "Расширение без интриги",
                "activeState": "Finishing the outer chamber",
                "totalWork": 18,
                "workDone": 18,
                "totalStages": 3,
                "currentStage": 3,
                "pressure": 4,
                "stability": 79
              }
            }
          ],
          "completeGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_non_offensive_smuggle",
              "finalState": "Completed",
              "outcome": "Строительство завершено.",
              "targetGuardianId": "guardian_beta",
              "offensiveImpactAudit": {
                "targetLoss": 4
              }
            }
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var powerJournalJson = await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath);

        Assert.NotNull(trackerJson);
        Assert.Contains("proj_non_offensive_smuggle", trackerJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"offensiveImpactAudit\"", trackerJson, StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(powerJournalJson))
            Assert.DoesNotContain("rival_strike", powerJournalJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_OffensiveIntrigueCompletion_MissingTargetGuardianContext_FailsClosed()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 59 }
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
                "projectId": "proj_missing_guardian_context",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Интрига без разрешимого target context",
                "targetGuardianId": "guardian_beta",
                "activeState": "Attempting the final breach",
                "totalWork": 18,
                "workDone": 18,
                "totalStages": 3,
                "currentStage": 3,
                "pressure": 9,
                "stability": 71
              }
            }
          ],
          "completeGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_missing_guardian_context",
              "finalState": "Completed",
              "outcome": "Интрига завершена в битом guardian context.",
              "targetGuardianId": "guardian_beta",
              "offensiveImpactAudit": {
                "targetLoss": 5,
                "playerDefenseBonus": 2
              }
            }
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var powerJournalJson = await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath);
        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.NotNull(trackerJson);
        var trackerRoot = JsonNode.Parse(trackerJson!)?.AsObject();
        Assert.NotNull(trackerRoot);
        var activeProjects = trackerRoot!["activeProjects"]?.AsArray();
        var completedProjects = trackerRoot["completedProjects"]?.AsArray();
        Assert.NotNull(activeProjects);
        Assert.NotNull(completedProjects);
        Assert.Contains(activeProjects!, entry =>
            string.Equals(entry?["guardianId"]?.GetValue<string>(), "guardian_alpha", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(entry?["project"]?["projectId"]?.GetValue<string>(), "proj_missing_guardian_context", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(completedProjects!, entry =>
            string.Equals(entry?["guardianId"]?.GetValue<string>(), "guardian_alpha", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(entry?["project"]?["projectId"]?.GetValue<string>(), "proj_missing_guardian_context", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(powerJournalJson))
            Assert.DoesNotContain("rival_strike", powerJournalJson, StringComparison.Ordinal);
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "guardian_project_missing_offensive_impact_audit", StringComparison.OrdinalIgnoreCase));
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
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

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

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_GuardianRelationships_MigratesLegacyAttitudeAndExpandsNetwork()
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
              "abodePower": { "currentPower": 45, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                {
                  "targetGuardianId": "guardian_beta",
                  "attitude": "Enemy",
                  "reason": "Legacy feud"
                }
              ],
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
              "abodePower": { "currentPower": 52, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
            "abodePower": { "currentPower": 45, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [
              {
                "targetGuardianId": "guardian_beta",
                "attitude": "Enemy",
                "reason": "Legacy feud"
              }
            ],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        Assert.NotNull(guardiansJson);

        using var doc = System.Text.Json.JsonDocument.Parse(guardiansJson!);
        var guardians = doc.RootElement.GetProperty("guardians").EnumerateArray().ToList();
        var alpha = guardians.Single(item => item.GetProperty("guardianId").GetString() == "guardian_alpha");
        var beta = guardians.Single(item => item.GetProperty("guardianId").GetString() == "guardian_beta");

        var alphaRelationships = alpha.GetProperty("guardianRelationships").EnumerateArray().ToList();
        Assert.Single(alphaRelationships);
        Assert.Equal("guardian_beta", alphaRelationships[0].GetProperty("targetGuardianId").GetString());
        Assert.Equal(-90, alphaRelationships[0].GetProperty("attitudeScore").GetInt32());
        Assert.Equal("enemy", alphaRelationships[0].GetProperty("attitudeTier").GetString());

        var betaRelationships = beta.GetProperty("guardianRelationships").EnumerateArray().ToList();
        Assert.Single(betaRelationships);
        Assert.Equal("guardian_alpha", betaRelationships[0].GetProperty("targetGuardianId").GetString());
        Assert.True(betaRelationships[0].TryGetProperty("attitudeScore", out var betaScoreNode));
        Assert.InRange(betaScoreNode.GetInt32(), GuardianRelationshipRules.MinAttitudeScore, GuardianRelationshipRules.MaxAttitudeScore);

        var activeRelationships = doc.RootElement.GetProperty("activeGuardian").GetProperty("guardianRelationships").EnumerateArray().ToList();
        Assert.Single(activeRelationships);
        Assert.Equal(-90, activeRelationships[0].GetProperty("attitudeScore").GetInt32());
        Assert.Equal("enemy", activeRelationships[0].GetProperty("attitudeTier").GetString());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_CounterRivalOperation_AppliesCoalitionSupportBonus()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 62 }
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
              "guardianRelationships": [
                { "targetGuardianId": "guardian_beta", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Hostile feud", "lastChangedAt": null },
                { "targetGuardianId": "guardian_gamma", "attitudeScore": 0, "attitudeTier": "neutral", "reason": "Working distance", "lastChangedAt": null }
              ],
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
              "abodePower": { "currentPower": 58, "tier": "Могущественная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guardian_alpha", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Open rivalry", "lastChangedAt": null },
                { "targetGuardianId": "guardian_gamma", "attitudeScore": -30, "attitudeTier": "competitive", "reason": "Cold rivalry", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            },
            {
              "guardianId": "guardian_gamma",
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
              "abodePower": { "currentPower": 54, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guardian_alpha", "attitudeScore": 10, "attitudeTier": "neutral", "reason": "Loose accord", "lastChangedAt": null },
                { "targetGuardianId": "guardian_beta", "attitudeScore": -60, "attitudeTier": "rival", "reason": "Shared enemy", "lastChangedAt": null }
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
            "guardianRelationships": [
              { "targetGuardianId": "guardian_beta", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Hostile feud", "lastChangedAt": null },
              { "targetGuardianId": "guardian_gamma", "attitudeScore": 0, "attitudeTier": "neutral", "reason": "Working distance", "lastChangedAt": null }
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
                "projectId": "proj_counter",
                "projectType": "counter_rival_operation",
                "projectTier": "major",
                "projectMode": "supportive",
                "projectName": "Контр-сеть Азалии",
                "targetGuardianId": "guardian_beta",
                "activeState": "Stabilizing the contested threads",
                "totalWork": 18,
                "workDone": 18,
                "totalStages": 3,
                "currentStage": 3,
                "pressure": 6,
                "stability": 80
              }
            },
            {
              "guardianId": "guardian_beta",
              "project": {
                "projectId": "proj_beta_pressure",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Враждебное давление Варака",
                "targetGuardianId": "guardian_alpha",
                "activeState": "Undermining the rival's hold",
                "totalWork": 18,
                "workDone": 8,
                "totalStages": 3,
                "currentStage": 1,
                "pressure": 20,
                "stability": 50
              }
            },
            {
              "guardianId": "guardian_gamma",
              "project": {
                "projectId": "proj_gamma_pressure",
                "projectType": "offensive_intrigue",
                "projectTier": "minor",
                "projectMode": "offensive",
                "projectName": "Нерис давит на Варака",
                "targetGuardianId": "guardian_beta",
                "activeState": "Pressuring the shared enemy",
                "totalWork": 12,
                "workDone": 4,
                "totalStages": 2,
                "currentStage": 1,
                "pressure": 4,
                "stability": 70
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

        var expectedPressureRelief = GuardianProjectState.GetCounterOperationPressureRelief("major") + 1;
        var expectedStabilityRelief = GuardianProjectState.GetCounterOperationStabilityRelief("major") + 1;

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        Assert.NotNull(trackerJson);

        using var doc = System.Text.Json.JsonDocument.Parse(trackerJson!);
        var completedProject = doc.RootElement.GetProperty("completedProjects").EnumerateArray().Single();
        var audit = completedProject.GetProperty("project").GetProperty("projectOutcomeAudit");
        Assert.Equal(1, audit.GetProperty("coalitionSupportBonus").GetInt32());
        Assert.True(audit.GetProperty("coalitionEligible").GetBoolean());
        Assert.Equal(expectedPressureRelief, audit.GetProperty("pressureRelief").GetInt32());
        Assert.Equal(expectedStabilityRelief, audit.GetProperty("stabilityRelief").GetInt32());

        var targetProject = doc.RootElement.GetProperty("activeProjects")
            .EnumerateArray()
            .Single(item => item.GetProperty("guardianId").GetString() == "guardian_beta")
            .GetProperty("project");
        Assert.Equal(20 - expectedPressureRelief, targetProject.GetProperty("pressure").GetInt32());
        Assert.Equal(50 + expectedStabilityRelief, targetProject.GetProperty("stability").GetInt32());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_CounterRivalOperation_WithoutCurrentCoalitionTrace_DoesNotApplyCoalitionBonus()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 63 }
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
              "guardianRelationships": [
                { "targetGuardianId": "guardian_beta", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Hostile feud", "lastChangedAt": null },
                { "targetGuardianId": "guardian_gamma", "attitudeScore": 0, "attitudeTier": "neutral", "reason": "Working distance", "lastChangedAt": null }
              ],
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
              "abodePower": { "currentPower": 58, "tier": "Могущественная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guardian_alpha", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Open rivalry", "lastChangedAt": null },
                { "targetGuardianId": "guardian_gamma", "attitudeScore": -30, "attitudeTier": "competitive", "reason": "Cold rivalry", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            },
            {
              "guardianId": "guardian_gamma",
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
              "abodePower": { "currentPower": 54, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guardian_alpha", "attitudeScore": 10, "attitudeTier": "neutral", "reason": "Loose accord", "lastChangedAt": null },
                { "targetGuardianId": "guardian_beta", "attitudeScore": -60, "attitudeTier": "rival", "reason": "Shared enemy", "lastChangedAt": null }
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
            "guardianRelationships": [
              { "targetGuardianId": "guardian_beta", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Hostile feud", "lastChangedAt": null },
              { "targetGuardianId": "guardian_gamma", "attitudeScore": 0, "attitudeTier": "neutral", "reason": "Working distance", "lastChangedAt": null }
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
                "projectId": "proj_counter",
                "projectType": "counter_rival_operation",
                "projectTier": "major",
                "projectMode": "supportive",
                "projectName": "Контр-сеть Азалии",
                "targetGuardianId": "guardian_beta",
                "activeState": "Stabilizing the contested threads",
                "totalWork": 18,
                "workDone": 18,
                "totalStages": 3,
                "currentStage": 3,
                "pressure": 6,
                "stability": 80
              }
            },
            {
              "guardianId": "guardian_beta",
              "project": {
                "projectId": "proj_beta_pressure",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Враждебное давление Варака",
                "targetGuardianId": "guardian_alpha",
                "activeState": "Undermining the rival's hold",
                "totalWork": 18,
                "workDone": 8,
                "totalStages": 3,
                "currentStage": 1,
                "pressure": 20,
                "stability": 50
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

        var expectedPressureRelief = GuardianProjectState.GetCounterOperationPressureRelief("major");
        var expectedStabilityRelief = GuardianProjectState.GetCounterOperationStabilityRelief("major");

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        Assert.NotNull(trackerJson);

        using var doc = System.Text.Json.JsonDocument.Parse(trackerJson!);
        var completedProject = doc.RootElement.GetProperty("completedProjects").EnumerateArray().Single();
        var audit = completedProject.GetProperty("project").GetProperty("projectOutcomeAudit");
        Assert.Equal(0, audit.GetProperty("coalitionSupportBonus").GetInt32());
        Assert.False(audit.GetProperty("coalitionEligible").GetBoolean());
        Assert.Equal(expectedPressureRelief, audit.GetProperty("pressureRelief").GetInt32());
        Assert.Equal(expectedStabilityRelief, audit.GetProperty("stabilityRelief").GetInt32());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_InvalidGuardianProjectStartIdentity_IsIgnoredFailClosed()
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
              "guardianRelationships": [],
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
            "guardianRelationships": [],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """
        {
          "startGuardianProjects": [
            {
              "guardianId": "guardian_missing",
              "project": {
                "projectId": "proj_missing_guardian",
                "projectType": "abode_fortification",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Фантомный старт",
                "activeState": "Trying to start",
                "totalWork": 8,
                "workDone": 0,
                "totalStages": 2,
                "currentStage": 0,
                "pressure": 0,
                "stability": 100
              }
            },
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_missing_target",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Удар в пустоту",
                "targetGuardianId": "guardian_missing",
                "activeState": "Aiming into the void",
                "totalWork": 16,
                "workDone": 0,
                "totalStages": 3,
                "currentStage": 0,
                "pressure": 4,
                "stability": 70
              }
            }
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        Assert.NotNull(trackerJson);
        var trackerRoot = JsonNode.Parse(trackerJson!)!.AsObject();
        var activeProjects = trackerRoot["activeProjects"]?.AsArray();
        Assert.NotNull(activeProjects);
        Assert.Empty(activeProjects!);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_StartGuardianProjects_DoesNotReplaceExistingActiveProject()
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
              "guardianRelationships": [],
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
            "guardianRelationships": [],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await _fs.WriteFileAtomicAsync("test_backups/preturn_guardian_projects_existing_active.json", """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_existing",
                "projectType": "abode_expansion",
                "projectTier": "major",
                "projectMode": "internal",
                "projectName": "Старый проект",
                "activeState": "Still active",
                "totalWork": 18,
                "workDone": 9,
                "totalStages": 3,
                "currentStage": 1,
                "pressure": 4,
                "stability": 78
              }
            }
          ],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """
        {
          "startGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_replacement_attempt",
                "projectType": "abode_fortification",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Попытка замены",
                "activeState": "Trying to replace",
                "totalWork": 8,
                "workDone": 0,
                "totalStages": 2,
                "currentStage": 0,
                "pressure": 0,
                "stability": 100
              }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": []
        }
        """);
        await _fs.WriteFileAtomicAsync("test_backups/preturn_guardians_existing_active.json", """
        {
          "guardians": []
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>
        {
            [GuardianProjectState.TrackerPath] = "test_backups/preturn_guardian_projects_existing_active.json",
            ["game_state/meta/guardians.json"] = "test_backups/preturn_guardians_existing_active.json"
        });

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        Assert.NotNull(trackerJson);
        var trackerRoot = JsonNode.Parse(trackerJson!)!.AsObject();
        var activeProjects = trackerRoot["activeProjects"]?.AsArray();
        Assert.NotNull(activeProjects);
        Assert.Single(activeProjects!);
        Assert.Equal("proj_existing", activeProjects![0]!["project"]!["projectId"]!.GetValue<string>());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_CurrentMaterializedTrackerArraysWithoutBackups_RaisesExplicitBaselineFailure()
    {
        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_phantom_active",
                "projectType": "abode_expansion",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Phantom active state"
              }
            }
          ],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_phantom_completed",
                "projectType": "abode_fortification",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Phantom completed state"
              }
            }
          ],
          "temporaryProjectModifiers": [
            {
              "modifierId": "tmp_guardian_alpha_phantom",
              "guardianId": "guardian_alpha",
              "modifierType": "next_internal_project_starting_pressure",
              "value": 2,
              "remainingApplications": 1
            }
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => normalizer.NormalizeAccumulatedStateAsync());
        Assert.Contains("usable pre-normalization backup baseline", exception.Message, StringComparison.OrdinalIgnoreCase);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        Assert.NotNull(trackerJson);
        var trackerRoot = JsonNode.Parse(trackerJson!)!.AsObject();

        Assert.Single(trackerRoot["activeProjects"]!.AsArray());
        Assert.Single(trackerRoot["completedProjects"]!.AsArray());
        Assert.Single(trackerRoot["temporaryProjectModifiers"]!.AsArray());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_NoBackupsWithCommands_RaisesExplicitBaselineFailure()
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
                "projectId": "proj_phantom_active",
                "projectType": "abode_expansion",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Phantom active state"
              }
            }
          ],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_phantom_completed",
                "projectType": "abode_fortification",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Phantom completed state"
              }
            }
          ],
          "temporaryProjectModifiers": [
            {
              "modifierId": "tmp_guardian_alpha_phantom",
              "guardianId": "guardian_alpha",
              "modifierType": "next_internal_project_starting_pressure",
              "value": 2,
              "remainingApplications": 1
            }
          ],
          "startGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_started",
                "projectType": "abode_expansion",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Started from command",
                "activeState": "Planning",
                "totalWork": 5,
                "workDone": 0,
                "totalStages": 1,
                "currentStage": 0,
                "pressure": 1,
                "stability": 100
              }
            }
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => normalizer.NormalizeAccumulatedStateAsync());
        Assert.Contains("usable pre-normalization backup baseline", exception.Message, StringComparison.OrdinalIgnoreCase);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        Assert.NotNull(trackerJson);
        var trackerRoot = JsonNode.Parse(trackerJson!)!.AsObject();

        Assert.Single(trackerRoot["activeProjects"]!.AsArray());
        Assert.Single(trackerRoot["completedProjects"]!.AsArray());
        Assert.Single(trackerRoot["temporaryProjectModifiers"]!.AsArray());
        Assert.Single(trackerRoot["startGuardianProjects"]!.AsArray());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_PartialBackupsWithoutTrackerBaseline_RaisesExplicitBaselineFailure()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "inkFeathers": 5
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_phantom_active",
                "projectType": "abode_expansion",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Phantom active state"
              }
            }
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["game_state/meta/soul_state.json"] = "test_backups/unrelated_soul_state_backup.json"
            }));
        Assert.Contains("usable pre-normalization backup baseline", exception.Message, StringComparison.OrdinalIgnoreCase);

        var soulStateJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.NotNull(soulStateJson);
        using var soulStateDoc = JsonDocument.Parse(soulStateJson!);
        Assert.Equal(JsonValueKind.Number, soulStateDoc.RootElement.GetProperty("inkFeathers").ValueKind);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_TrackerBaselineWithoutGuardiansBaseline_RaisesExplicitBaselineFailure()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "inkFeathers": 5
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "questManagement": {
                "availableQuests": [],
                "activeQuests": [],
                "completedQuests": []
              },
              "gachaSystem": {
                "chargesPerReturn": 0,
                "chargesUsedThisReturn": 0,
                "gachaHistory": []
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
                "projectId": "proj_lore_token",
                "projectType": "lore_research",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Lore token project",
                "effectState": {
                  "questHookTokensRemaining": 1
                }
              }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("test_backups/preturn_tracker_only_baseline.json", """
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_lore_token",
                "projectType": "lore_research",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Lore token project",
                "effectState": {
                  "questHookTokensRemaining": 1
                }
              }
            }
          ],
          "temporaryProjectModifiers": []
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [GuardianProjectState.TrackerPath] = "test_backups/preturn_tracker_only_baseline.json"
            }));
        Assert.Contains("usable pre-normalization backup baseline", exception.Message, StringComparison.OrdinalIgnoreCase);

        var soulStateJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.NotNull(soulStateJson);
        using var soulStateDoc = JsonDocument.Parse(soulStateJson!);
        Assert.Equal(JsonValueKind.Number, soulStateDoc.RootElement.GetProperty("inkFeathers").ValueKind);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_MissingGuardianProjectBaseline_FailsBeforeEarlierFilesAreNormalized()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "inkFeathers": 5
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_phantom_active",
                "projectType": "abode_expansion",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Phantom active state"
              }
            }
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => normalizer.NormalizeAccumulatedStateAsync());
        Assert.Contains("usable pre-normalization backup baseline", exception.Message, StringComparison.OrdinalIgnoreCase);

        var soulStateJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.NotNull(soulStateJson);
        using var soulStateDoc = JsonDocument.Parse(soulStateJson!);
        Assert.Equal(JsonValueKind.Number, soulStateDoc.RootElement.GetProperty("inkFeathers").ValueKind);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_MalformedCurrentGuardianProjectTracker_RaisesExplicitCurrentTrackerFailure()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "inkFeathers": 3
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "questManagement": {
                "availableQuests": [],
                "activeQuests": [],
                "completedQuests": []
              },
              "gachaSystem": {
                "chargesPerReturn": 0,
                "chargesUsedThisReturn": 0,
                "gachaHistory": []
              }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("test_backups/preturn_tracker_current_malformed.json", """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """);

        await _fs.WriteFileAtomicAsync("test_backups/preturn_guardians_current_malformed.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "questManagement": {
                "availableQuests": [],
                "activeQuests": [],
                "completedQuests": []
              },
              "gachaSystem": {
                "chargesPerReturn": 0,
                "chargesUsedThisReturn": 0,
                "gachaHistory": []
              }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, "{ malformed tracker");

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [GuardianProjectState.TrackerPath] = "test_backups/preturn_tracker_current_malformed.json",
                ["game_state/meta/guardians.json"] = "test_backups/preturn_guardians_current_malformed.json"
            }));

        Assert.Contains("readable current guardian_projects.json tracker surface", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("{ malformed tracker", await _fs.ReadFileAsync(GuardianProjectState.TrackerPath));
        Assert.Contains("\"inkFeathers\": 3", await _fs.ReadFileAsync("game_state/meta/soul_state.json"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_MalformedCurrentGuardiansDuringProjectReconciliation_RaisesExplicitCurrentGuardiansFailure()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "inkFeathers": {
            "current": 5,
            "total": 5
          }
        }
        """);

        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 61 }
        """);

        await _fs.WriteFileAtomicAsync("test_backups/preturn_tracker_current_guardians_invalid.json", """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """);

        await _fs.WriteFileAtomicAsync("test_backups/preturn_guardians_current_guardians_invalid.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "questManagement": {
                "availableQuests": [],
                "activeQuests": [],
                "completedQuests": []
              },
              "gachaSystem": {
                "chargesPerReturn": 0,
                "chargesUsedThisReturn": 0,
                "gachaHistory": []
              }
            }
          ]
        }
        """);

        var currentTrackerJson = """
        {
          "startGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_start_requires_guardian_state",
                "projectType": "abode_expansion",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Проект, требующий guardian reconciliation",
                "activeState": "Planning",
                "totalWork": 5,
                "workDone": 0,
                "totalStages": 1,
                "currentStage": 0,
                "pressure": 1,
                "stability": 100
              }
            }
          ]
        }
        """;
        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, currentTrackerJson);
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", "{ malformed guardians");

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [GuardianProjectState.TrackerPath] = "test_backups/preturn_tracker_current_guardians_invalid.json",
                ["game_state/meta/guardians.json"] = "test_backups/preturn_guardians_current_guardians_invalid.json"
            }));

        Assert.Contains("readable current guardians.json authority surface", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(currentTrackerJson, await _fs.ReadFileAsync(GuardianProjectState.TrackerPath));
        Assert.Equal("{ malformed guardians", await _fs.ReadFileAsync("game_state/meta/guardians.json"));
        Assert.Contains("\"current\": 5", await _fs.ReadFileAsync("game_state/meta/soul_state.json"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("lore_research")]
    [InlineData("relic_forging")]
    public async Task NormalizeAccumulatedStateAsync_MissingCurrentGuardiansDuringCompletedProjectSideConsumption_RaisesExplicitCurrentGuardiansFailure(string projectType)
    {
        var projectSpecificStateJson = string.Equals(projectType, "lore_research", StringComparison.OrdinalIgnoreCase)
            ? """
                "projectOutcomeAudit": {
                  "questHookCount": 1,
                  "visibleRivalClueBonus": 0
                },
                "effectState": {
                  "targetIncarnation": 0,
                  "questHookTokensGranted": 1,
                  "questHookTokensSpent": 0,
                  "guaranteedArchiveQuestGranted": 0,
                  "guaranteedArchiveQuestConsumed": 0,
                  "specialQuestLineTokensGranted": 0,
                  "specialQuestLineTokensSpent": 0,
                  "visibleRivalClueBudgetGranted": 0,
                  "visibleRivalClueBudgetSpent": 0
                }
            """
            : """
                "effectState": {
                  "gachaUsesGranted": 1,
                  "gachaUsesSpent": 0
                }
            """;

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 0,
          "inkFeathers": {
            "current": 7,
            "total": 7
          }
        }
        """);

        await _fs.WriteFileAtomicAsync("test_backups/preturn_tracker_side_consumption_guardians_missing.json", $$"""
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_completed_side_consumption",
                "projectType": "{{projectType}}",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Completed side-consumption project",
                "finalState": "Completed",
                {{projectSpecificStateJson}}
              }
            }
          ],
          "temporaryProjectModifiers": []
        }
        """);

        await _fs.WriteFileAtomicAsync("test_backups/preturn_guardians_side_consumption_guardians_missing.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "questManagement": {
                "availableQuests": [],
                "activeQuests": [],
                "completedQuests": []
              },
              "gachaSystem": {
                "chargesPerReturn": 0,
                "chargesUsedThisReturn": 0,
                "gachaHistory": []
              }
            }
          ]
        }
        """);

        var currentTrackerJson = $$"""
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_completed_side_consumption",
                "projectType": "{{projectType}}",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Completed side-consumption project",
                "finalState": "Completed",
                {{projectSpecificStateJson}}
              }
            }
          ],
          "temporaryProjectModifiers": []
        }
        """;

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, currentTrackerJson);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [GuardianProjectState.TrackerPath] = "test_backups/preturn_tracker_side_consumption_guardians_missing.json",
                ["game_state/meta/guardians.json"] = "test_backups/preturn_guardians_side_consumption_guardians_missing.json"
            }));

        Assert.Contains("readable current guardians.json authority surface", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(currentTrackerJson, await _fs.ReadFileAsync(GuardianProjectState.TrackerPath));
        Assert.Contains("\"current\": 7", await _fs.ReadFileAsync("game_state/meta/soul_state.json"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("lore_research")]
    [InlineData("relic_forging")]
    public async Task NormalizeAccumulatedStateAsync_MissingCurrentGuardiansWithOnlyTrackerLocalCompletedEffects_DoesNotRequireCurrentGuardians(string projectType)
    {
        var projectSpecificStateJson = string.Equals(projectType, "lore_research", StringComparison.OrdinalIgnoreCase)
            ? """
                "projectOutcomeAudit": {
                  "questHookCount": 1,
                  "visibleRivalClueBonus": 1
                },
                "effectState": {
                  "targetIncarnation": 0,
                  "questHookTokensGranted": 1,
                  "questHookTokensSpent": 1,
                  "guaranteedArchiveQuestGranted": 0,
                  "guaranteedArchiveQuestConsumed": 0,
                  "specialQuestLineTokensGranted": 0,
                  "specialQuestLineTokensSpent": 0,
                  "visibleRivalClueBudgetGranted": 1,
                  "visibleRivalClueBudgetSpent": 0
                }
            """
            : """
                "effectState": {
                  "gachaUsesGranted": 1,
                  "gachaUsesSpent": 1
                }
            """;
        var expectedTrackerMarker = string.Equals(projectType, "lore_research", StringComparison.OrdinalIgnoreCase)
            ? "\"visibleRivalClueBudgetSpent\": 0"
            : "\"gachaUsesSpent\": 1";

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "inkFeathers": {
            "current": 9,
            "total": 9
          }
        }
        """);

        await _fs.WriteFileAtomicAsync("test_backups/preturn_tracker_tracker_only_guardians_absent.json", $$"""
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_completed_tracker_only",
                "projectType": "{{projectType}}",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Completed tracker-only project",
                "finalState": "Completed",
                {{projectSpecificStateJson}}
              }
            }
          ],
          "temporaryProjectModifiers": []
        }
        """);

        await _fs.WriteFileAtomicAsync("test_backups/preturn_guardians_tracker_only_guardians_absent.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "questManagement": {
                "availableQuests": [],
                "activeQuests": [],
                "completedQuests": []
              },
              "gachaSystem": {
                "chargesPerReturn": 0,
                "chargesUsedThisReturn": 0,
                "gachaHistory": []
              }
            }
          ]
        }
        """);

        var currentTrackerJson = $$"""
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_completed_tracker_only",
                "projectType": "{{projectType}}",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Completed tracker-only project",
                "finalState": "Completed",
                {{projectSpecificStateJson}}
              }
            }
          ],
          "temporaryProjectModifiers": []
        }
        """;

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, currentTrackerJson);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GuardianProjectState.TrackerPath] = "test_backups/preturn_tracker_tracker_only_guardians_absent.json",
            ["game_state/meta/guardians.json"] = "test_backups/preturn_guardians_tracker_only_guardians_absent.json"
        });

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        Assert.NotNull(trackerJson);
        Assert.Contains(expectedTrackerMarker, trackerJson, StringComparison.Ordinal);
        Assert.False(_fs.FileExists("game_state/meta/guardians.json"));
        Assert.Contains("\"current\": 9", await _fs.ReadFileAsync("game_state/meta/soul_state.json"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_MissingCurrentGuardiansWithFutureIncarnationLoreConsumables_DoesNotRequireCurrentGuardians()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 0,
          "currentRealm": "underworld",
          "inkFeathers": {
            "current": 11,
            "total": 11
          }
        }
        """);

        const string trackerBackupPath = "test_backups/preturn_tracker_future_incarnation_lore_guardians_absent.json";
        const string guardiansBackupPath = "test_backups/preturn_guardians_future_incarnation_lore_guardians_absent.json";
        var trackerJson = """
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_future_incarnation_lore",
                "projectType": "lore_research",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Future-incarnation lore project",
                "finalState": "Completed",
                "projectOutcomeAudit": {
                  "questHookCount": 1,
                  "visibleRivalClueBonus": 0
                },
                "effectState": {
                  "targetIncarnation": 1,
                  "questHookTokensGranted": 1,
                  "questHookTokensSpent": 0,
                  "guaranteedArchiveQuestGranted": 0,
                  "guaranteedArchiveQuestConsumed": 0,
                  "specialQuestLineTokensGranted": 0,
                  "specialQuestLineTokensSpent": 0,
                  "visibleRivalClueBudgetGranted": 0,
                  "visibleRivalClueBudgetSpent": 0
                }
              }
            }
          ],
          "temporaryProjectModifiers": []
        }
        """;

        await _fs.WriteFileAtomicAsync(trackerBackupPath, trackerJson);
        await _fs.WriteFileAtomicAsync(guardiansBackupPath, """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "questManagement": {
                "availableQuests": [],
                "activeQuests": [],
                "completedQuests": []
              },
              "gachaSystem": {
                "chargesPerReturn": 0,
                "chargesUsedThisReturn": 0,
                "gachaHistory": []
              }
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, trackerJson);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GuardianProjectState.TrackerPath] = trackerBackupPath,
            ["game_state/meta/guardians.json"] = guardiansBackupPath
        });

        var normalizedTracker = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        Assert.Contains("\"targetIncarnation\": 1", normalizedTracker, StringComparison.Ordinal);
        Assert.Contains("\"questHookTokensSpent\": 0", normalizedTracker, StringComparison.Ordinal);
        Assert.False(_fs.FileExists("game_state/meta/guardians.json"));
        Assert.Contains("\"current\": 11", await _fs.ReadFileAsync("game_state/meta/soul_state.json"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_BackupDerivedCurrentLoreIncarnationStillRequiresCurrentGuardians()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "inkFeathers": {
            "current": 13,
            "total": 13
          }
        }
        """);

        const string trackerBackupPath = "test_backups/preturn_tracker_backup_derived_current_lore_guardians_missing.json";
        const string guardiansBackupPath = "test_backups/preturn_guardians_backup_derived_current_lore_guardians_missing.json";
        const string soulStateBackupPath = "test_backups/preturn_soul_state_backup_derived_current_lore_guardians_missing.json";
        var trackerJson = """
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_backup_derived_current_lore",
                "projectType": "lore_research",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Backup-derived current lore project",
                "finalState": "Completed",
                "projectOutcomeAudit": {
                  "questHookCount": 1,
                  "visibleRivalClueBonus": 0
                },
                "effectState": {
                  "targetIncarnation": 3,
                  "questHookTokensGranted": 1,
                  "questHookTokensSpent": 0,
                  "guaranteedArchiveQuestGranted": 0,
                  "guaranteedArchiveQuestConsumed": 0,
                  "specialQuestLineTokensGranted": 0,
                  "specialQuestLineTokensSpent": 0,
                  "visibleRivalClueBudgetGranted": 0,
                  "visibleRivalClueBudgetSpent": 0
                }
              }
            }
          ],
          "temporaryProjectModifiers": []
        }
        """;

        await _fs.WriteFileAtomicAsync(trackerBackupPath, trackerJson);
        await _fs.WriteFileAtomicAsync(guardiansBackupPath, """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "questManagement": {
                "availableQuests": [],
                "activeQuests": [],
                "completedQuests": []
              },
              "gachaSystem": {
                "chargesPerReturn": 0,
                "chargesUsedThisReturn": 0,
                "gachaHistory": []
              }
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync(soulStateBackupPath, """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World",
          "inkFeathers": {
            "current": 13,
            "total": 13
          }
        }
        """);
        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, trackerJson);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [GuardianProjectState.TrackerPath] = trackerBackupPath,
                ["game_state/meta/guardians.json"] = guardiansBackupPath,
                ["game_state/meta/soul_state.json"] = soulStateBackupPath
            }));

        Assert.Contains("readable current guardians.json authority surface", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(trackerJson, await _fs.ReadFileAsync(GuardianProjectState.TrackerPath));
        var soulStateJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.Contains("\"current\": 13", soulStateJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"currentIncarnation\": 3", soulStateJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_BackupDerivedFutureLoreIncarnationDoesNotRequireCurrentGuardians()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "inkFeathers": {
            "current": 17,
            "total": 17
          }
        }
        """);

        const string trackerBackupPath = "test_backups/preturn_tracker_backup_derived_future_lore_guardians_absent.json";
        const string guardiansBackupPath = "test_backups/preturn_guardians_backup_derived_future_lore_guardians_absent.json";
        const string soulStateBackupPath = "test_backups/preturn_soul_state_backup_derived_future_lore_guardians_absent.json";
        var trackerJson = """
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_backup_derived_future_lore",
                "projectType": "lore_research",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Backup-derived future lore project",
                "finalState": "Completed",
                "projectOutcomeAudit": {
                  "questHookCount": 1,
                  "visibleRivalClueBonus": 0
                },
                "effectState": {
                  "targetIncarnation": 4,
                  "questHookTokensGranted": 1,
                  "questHookTokensSpent": 0,
                  "guaranteedArchiveQuestGranted": 0,
                  "guaranteedArchiveQuestConsumed": 0,
                  "specialQuestLineTokensGranted": 0,
                  "specialQuestLineTokensSpent": 0,
                  "visibleRivalClueBudgetGranted": 0,
                  "visibleRivalClueBudgetSpent": 0
                }
              }
            }
          ],
          "temporaryProjectModifiers": []
        }
        """;

        await _fs.WriteFileAtomicAsync(trackerBackupPath, trackerJson);
        await _fs.WriteFileAtomicAsync(guardiansBackupPath, """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "questManagement": {
                "availableQuests": [],
                "activeQuests": [],
                "completedQuests": []
              },
              "gachaSystem": {
                "chargesPerReturn": 0,
                "chargesUsedThisReturn": 0,
                "gachaHistory": []
              }
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync(soulStateBackupPath, """
        {
          "currentIncarnation": 3,
          "currentRealm": "Shining Abode",
          "inkFeathers": {
            "current": 17,
            "total": 17
          }
        }
        """);
        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, trackerJson);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GuardianProjectState.TrackerPath] = trackerBackupPath,
            ["game_state/meta/guardians.json"] = guardiansBackupPath,
            ["game_state/meta/soul_state.json"] = soulStateBackupPath
        });

        var normalizedTracker = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        Assert.Contains("\"targetIncarnation\": 4", normalizedTracker, StringComparison.Ordinal);
        Assert.Contains("\"questHookTokensSpent\": 0", normalizedTracker, StringComparison.Ordinal);
        Assert.False(_fs.FileExists("game_state/meta/guardians.json"));
        var normalizedSoulState = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.Contains("\"currentIncarnation\": 3", normalizedSoulState, StringComparison.Ordinal);
        Assert.Contains("\"current\": 17", normalizedSoulState, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_CurrentIncarnationOnlySoulPreparationDoesNotRequireCurrentRealm()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 4,
          "inkFeathers": {
            "current": 23,
            "total": 23
          }
        }
        """);

        const string trackerBackupPath = "test_backups/preturn_tracker_soul_preparation_incarnation_only_soul.json";
        var trackerJson = """
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_soul_prep_incarnation_only",
                "projectType": "soul_preparation",
                "projectTier": "major",
                "projectMode": "internal",
                "projectName": "Soul preparation with incarnation-only soul context",
                "finalState": "Completed"
              }
            }
          ],
          "temporaryProjectModifiers": []
        }
        """;

        await _fs.WriteFileAtomicAsync(trackerBackupPath, trackerJson);
        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, trackerJson);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GuardianProjectState.TrackerPath] = trackerBackupPath
        });

        var normalizedTracker = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        Assert.Contains("\"targetIncarnation\": 5", normalizedTracker, StringComparison.Ordinal);
        Assert.Contains("\"preparationBudgetPointsSpent\": 0", normalizedTracker, StringComparison.Ordinal);
        Assert.DoesNotContain("\"currentRealm\"", await _fs.ReadFileAsync("game_state/meta/soul_state.json"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_BackupDerivedCurrentLoreIncarnationWithMalformedCurrentSoulState_RaisesExplicitSoulStateFailure()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{ malformed soul");

        const string trackerBackupPath = "test_backups/preturn_tracker_backup_derived_current_lore_malformed_soul_state.json";
        const string guardiansBackupPath = "test_backups/preturn_guardians_backup_derived_current_lore_malformed_soul_state.json";
        const string soulStateBackupPath = "test_backups/preturn_soul_state_backup_derived_current_lore_malformed_soul_state.json";
        var trackerJson = """
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_backup_derived_current_lore_malformed_soul",
                "projectType": "lore_research",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Backup-derived current lore with malformed soul_state",
                "finalState": "Completed",
                "projectOutcomeAudit": {
                  "questHookCount": 1,
                  "visibleRivalClueBonus": 0
                },
                "effectState": {
                  "targetIncarnation": 3,
                  "questHookTokensGranted": 1,
                  "questHookTokensSpent": 0,
                  "guaranteedArchiveQuestGranted": 0,
                  "guaranteedArchiveQuestConsumed": 0,
                  "specialQuestLineTokensGranted": 0,
                  "specialQuestLineTokensSpent": 0,
                  "visibleRivalClueBudgetGranted": 0,
                  "visibleRivalClueBudgetSpent": 0
                }
              }
            }
          ],
          "temporaryProjectModifiers": []
        }
        """;

        await _fs.WriteFileAtomicAsync(trackerBackupPath, trackerJson);
        await _fs.WriteFileAtomicAsync(guardiansBackupPath, """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "questManagement": {
                "availableQuests": [],
                "activeQuests": [],
                "completedQuests": []
              },
              "gachaSystem": {
                "chargesPerReturn": 0,
                "chargesUsedThisReturn": 0,
                "gachaHistory": []
              }
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync(soulStateBackupPath, """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World",
          "inkFeathers": {
            "current": 19,
            "total": 19
          }
        }
        """);
        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, trackerJson);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [GuardianProjectState.TrackerPath] = trackerBackupPath,
                ["game_state/meta/guardians.json"] = guardiansBackupPath,
                ["game_state/meta/soul_state.json"] = soulStateBackupPath
            }));

        Assert.Contains("readable current soul_state.json authority surface", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(trackerJson, await _fs.ReadFileAsync(GuardianProjectState.TrackerPath));
        Assert.Equal("{ malformed soul", await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        Assert.False(_fs.FileExists("game_state/meta/guardians.json"));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_BackupDerivedCurrentLoreIncarnationWithUnsupportedTopLevelCurrentSoulState_RaisesExplicitSoulStateFailure()
    {
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

        const string trackerBackupPath = "test_backups/preturn_tracker_backup_derived_current_lore_unsupported_key_soul_state.json";
        const string guardiansBackupPath = "test_backups/preturn_guardians_backup_derived_current_lore_unsupported_key_soul_state.json";
        const string soulStateBackupPath = "test_backups/preturn_soul_state_backup_derived_current_lore_unsupported_key_soul_state.json";
        var trackerJson = """
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_backup_derived_current_lore_unsupported_key_soul",
                "projectType": "lore_research",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Backup-derived current lore with unsupported current soul_state key",
                "finalState": "Completed",
                "projectOutcomeAudit": {
                  "questHookCount": 1,
                  "visibleRivalClueBonus": 0
                },
                "effectState": {
                  "targetIncarnation": 3,
                  "questHookTokensGranted": 1,
                  "questHookTokensSpent": 0,
                  "guaranteedArchiveQuestGranted": 0,
                  "guaranteedArchiveQuestConsumed": 0,
                  "specialQuestLineTokensGranted": 0,
                  "specialQuestLineTokensSpent": 0,
                  "visibleRivalClueBudgetGranted": 0,
                  "visibleRivalClueBudgetSpent": 0
                }
              }
            }
          ],
          "temporaryProjectModifiers": []
        }
        """;

        await _fs.WriteFileAtomicAsync(trackerBackupPath, trackerJson);
        await _fs.WriteFileAtomicAsync(guardiansBackupPath, """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "questManagement": {
                "availableQuests": [],
                "activeQuests": [],
                "completedQuests": []
              },
              "gachaSystem": {
                "chargesPerReturn": 0,
                "chargesUsedThisReturn": 0,
                "gachaHistory": []
              }
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync(soulStateBackupPath, """
        {
          "currentIncarnation": 2,
          "currentRealm": "Chaos Sea",
          "inkFeathers": {
            "current": 19,
            "total": 19
          }
        }
        """);
        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, trackerJson);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [GuardianProjectState.TrackerPath] = trackerBackupPath,
                ["game_state/meta/guardians.json"] = guardiansBackupPath,
                ["game_state/meta/soul_state.json"] = soulStateBackupPath
            }));

        Assert.Contains("readable current soul_state.json authority surface", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(trackerJson, await _fs.ReadFileAsync(GuardianProjectState.TrackerPath));
        Assert.Contains("\"foo\": []", await _fs.ReadFileAsync("game_state/meta/soul_state.json"), StringComparison.Ordinal);
        Assert.False(_fs.FileExists("game_state/meta/guardians.json"));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_BackupDerivedCurrentLoreIncarnationWithArchiveActionResolutionsCurrentSoulState_RemainsValid()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World",
          "archiveActionResolutions": []
        }
        """);

        const string trackerBackupPath = "test_backups/preturn_tracker_backup_derived_current_lore_archive_action_resolutions_soul_state.json";
        const string guardiansBackupPath = "test_backups/preturn_guardians_backup_derived_current_lore_archive_action_resolutions_soul_state.json";
        const string soulStateBackupPath = "test_backups/preturn_soul_state_backup_derived_current_lore_archive_action_resolutions_soul_state.json";
        const string guardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "questManagement": {
                "availableQuests": [],
                "activeQuests": [],
                "completedQuests": []
              },
              "gachaSystem": {
                "chargesPerReturn": 0,
                "chargesUsedThisReturn": 0,
                "gachaHistory": []
              }
            }
          ]
        }
        """;
        var trackerJson = """
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_backup_derived_current_lore_archive_action_resolutions",
                "projectType": "lore_research",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Backup-derived current lore with archiveActionResolutions current soul_state key",
                "finalState": "Completed",
                "projectOutcomeAudit": {
                  "questHookCount": 1,
                  "visibleRivalClueBonus": 0
                },
                "effectState": {
                  "targetIncarnation": 3,
                  "questHookTokensGranted": 1,
                  "questHookTokensSpent": 0,
                  "guaranteedArchiveQuestGranted": 0,
                  "guaranteedArchiveQuestConsumed": 0,
                  "specialQuestLineTokensGranted": 0,
                  "specialQuestLineTokensSpent": 0,
                  "visibleRivalClueBudgetGranted": 0,
                  "visibleRivalClueBudgetSpent": 0
                }
              }
            }
          ],
          "temporaryProjectModifiers": []
        }
        """;

        await _fs.WriteFileAtomicAsync(trackerBackupPath, trackerJson);
        await _fs.WriteFileAtomicAsync(guardiansBackupPath, guardiansJson);
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", guardiansJson);
        await _fs.WriteFileAtomicAsync(soulStateBackupPath, """
        {
          "currentIncarnation": 2,
          "currentRealm": "Chaos Sea",
          "inkFeathers": {
            "current": 19,
            "total": 19
          }
        }
        """);
        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, trackerJson);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GuardianProjectState.TrackerPath] = trackerBackupPath,
            ["game_state/meta/guardians.json"] = guardiansBackupPath,
            ["game_state/meta/soul_state.json"] = soulStateBackupPath
        });

        var normalizedTracker = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var normalizedSoulState = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        Assert.NotNull(normalizedTracker);
        Assert.Contains("\"targetIncarnation\": 3", normalizedTracker, StringComparison.Ordinal);
        Assert.NotNull(normalizedSoulState);
        Assert.Contains("\"currentIncarnation\": 3", normalizedSoulState, StringComparison.Ordinal);
        Assert.DoesNotContain("\"archiveActionResolutions\"", normalizedSoulState, StringComparison.Ordinal);
        Assert.True(_fs.FileExists("game_state/meta/guardians.json"));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_BackupDerivedFutureLoreIncarnationWithMissingCurrentSoulState_RaisesExplicitSoulStateFailure()
    {
        const string trackerBackupPath = "test_backups/preturn_tracker_backup_derived_future_lore_missing_soul_state.json";
        const string guardiansBackupPath = "test_backups/preturn_guardians_backup_derived_future_lore_missing_soul_state.json";
        const string soulStateBackupPath = "test_backups/preturn_soul_state_backup_derived_future_lore_missing_soul_state.json";
        var trackerJson = """
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_backup_derived_future_lore_missing_soul",
                "projectType": "lore_research",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Backup-derived future lore with missing soul_state",
                "finalState": "Completed",
                "projectOutcomeAudit": {
                  "questHookCount": 1,
                  "visibleRivalClueBonus": 0
                },
                "effectState": {
                  "targetIncarnation": 4,
                  "questHookTokensGranted": 1,
                  "questHookTokensSpent": 0,
                  "guaranteedArchiveQuestGranted": 0,
                  "guaranteedArchiveQuestConsumed": 0,
                  "specialQuestLineTokensGranted": 0,
                  "specialQuestLineTokensSpent": 0,
                  "visibleRivalClueBudgetGranted": 0,
                  "visibleRivalClueBudgetSpent": 0
                }
              }
            }
          ],
          "temporaryProjectModifiers": []
        }
        """;

        await _fs.WriteFileAtomicAsync(trackerBackupPath, trackerJson);
        await _fs.WriteFileAtomicAsync(guardiansBackupPath, """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "questManagement": {
                "availableQuests": [],
                "activeQuests": [],
                "completedQuests": []
              },
              "gachaSystem": {
                "chargesPerReturn": 0,
                "chargesUsedThisReturn": 0,
                "gachaHistory": []
              }
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync(soulStateBackupPath, """
        {
          "currentIncarnation": 3,
          "currentRealm": "Shining Abode",
          "inkFeathers": {
            "current": 23,
            "total": 23
          }
        }
        """);
        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, trackerJson);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [GuardianProjectState.TrackerPath] = trackerBackupPath,
                ["game_state/meta/guardians.json"] = guardiansBackupPath,
                ["game_state/meta/soul_state.json"] = soulStateBackupPath
            }));

        Assert.Contains("readable current soul_state.json authority surface", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(trackerJson, await _fs.ReadFileAsync(GuardianProjectState.TrackerPath));
        Assert.False(_fs.FileExists("game_state/meta/soul_state.json"));
        Assert.False(_fs.FileExists("game_state/meta/guardians.json"));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_StartDoesNotReuseCompletedProjectKey()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
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
            "guardianRelationships": [],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_completed_reuse",
                "projectType": "abode_expansion",
                "projectTier": "major",
                "projectMode": "internal",
                "projectName": "Завершённый проект",
                "finalState": "Completed",
                "completionTurn": 59,
                "outcome": "Уже завершён."
              }
            }
          ],
          "startGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_completed_reuse",
                "projectType": "abode_fortification",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Новый проект с reused key",
                "activeState": "Should not materialize",
                "totalWork": 8,
                "workDone": 0,
                "totalStages": 2,
                "currentStage": 0,
                "pressure": 0,
                "stability": 100
              }
            }
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        Assert.NotNull(trackerJson);
        using var doc = JsonDocument.Parse(trackerJson!);
        var activeProjects = doc.RootElement.GetProperty("activeProjects").EnumerateArray().ToList();
        var completedProjects = doc.RootElement.GetProperty("completedProjects").EnumerateArray().ToList();

        Assert.Empty(activeProjects);
        Assert.Single(completedProjects);
        Assert.Equal("proj_completed_reuse", completedProjects[0].GetProperty("project").GetProperty("projectId").GetString());
        Assert.Equal("abode_expansion", completedProjects[0].GetProperty("project").GetProperty("projectType").GetString());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_SabotageEvent_OmitsUnknownRelatedGuardian()
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
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
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
            "guardianRelationships": [],
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
                "activeState": "Under pressure",
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
              "pressure": 9,
              "stability": 69,
              "relatedGuardianId": "guardian_missing",
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
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var journalJson = await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath);
        Assert.NotNull(journalJson);
        Assert.DoesNotContain("guardian_missing", journalJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_SabotageEvent_OmitsSelfRelatedGuardian()
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
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
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
            "guardianRelationships": [],
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
                "activeState": "Under pressure",
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
              "pressure": 9,
              "stability": 69,
              "relatedGuardianId": "guardian_alpha",
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
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var journalJson = await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath);
        Assert.NotNull(journalJson);
        var journalRoot = JsonNode.Parse(journalJson!)!.AsObject();
        var entries = journalRoot["entries"]?.AsArray();
        Assert.NotNull(entries);
        var firstEntry = entries!.OfType<JsonObject>().First();
        Assert.True(firstEntry["relatedGuardianId"] is null || string.IsNullOrWhiteSpace(firstEntry["relatedGuardianId"]?.GetValue<string>()));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_OffensiveIntrigueCompletion_ClampedRivalStrikeRemainsCanonicallyValid()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 80 }
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
              "abodePower": { "currentPower": 85, "tier": "Могущественная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guardian_beta", "attitudeScore": -80, "attitudeTier": "enemy", "reason": "Open hostility", "lastChangedAt": null }
              ],
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
              "abodePower": { "currentPower": 1, "tier": "Угасающая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
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
            "abodePower": { "currentPower": 85, "tier": "Могущественная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [
              { "targetGuardianId": "guardian_beta", "attitudeScore": -80, "attitudeTier": "enemy", "reason": "Open hostility", "lastChangedAt": null }
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
                "projectId": "proj_clamped_strike",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Интрига на истощение",
                "targetGuardianId": "guardian_beta",
                "activeState": "Driving the final collapse",
                "totalWork": 18,
                "workDone": 18,
                "totalStages": 3,
                "currentStage": 3,
                "pressure": 10,
                "stability": 72
              }
            }
          ],
          "completeGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_clamped_strike",
              "finalState": "Completed",
              "outcome": "Цель почти лишилась силы.",
              "targetGuardianId": "guardian_beta",
              "offensiveImpactAudit": {
                "playerDefenseBonus": 0
              }
            }
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var journalJson = await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath);
        Assert.NotNull(journalJson);
        var journalRoot = JsonNode.Parse(journalJson!)!.AsObject();
        var entries = journalRoot["entries"]?.AsArray();
        Assert.NotNull(entries);
        var strikeEntry = entries!.OfType<JsonObject>().Single(entry =>
            string.Equals(entry["reasonType"]?.GetValue<string>(), "rival_strike", StringComparison.OrdinalIgnoreCase));
        var strikeAudit = strikeEntry["audit"]!.AsObject();
        var appliedDelta = strikeEntry["delta"]!.GetValue<int>();
        var targetLoss = strikeAudit["targetLoss"]!.GetValue<int>();

        Assert.Equal(-1, appliedDelta);
        Assert.True(targetLoss > 1);
        Assert.True(Math.Abs(appliedDelta) <= targetLoss);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_rival_strike_delta_target_loss_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_CanonicalizesLegacyCompletionRivalStrikeIdentityInJournal()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 81 }
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
              "abodePower": { "currentPower": 85, "tier": "Могущественная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guardian_beta", "attitudeScore": -80, "attitudeTier": "enemy", "reason": "Open hostility", "lastChangedAt": null }
              ],
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
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
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
            "abodePower": { "currentPower": 85, "tier": "Могущественная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [
              { "targetGuardianId": "guardian_beta", "attitudeScore": -80, "attitudeTier": "enemy", "reason": "Open hostility", "lastChangedAt": null }
            ],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_repair_strike",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Интрига на восстановление связи",
                "targetGuardianId": "guardian_beta",
                "completionTurn": 81,
                "finalState": "Completed",
                "outcome": "Старый strike должен восстановить атакующего."
              }
            }
          ],
          "temporaryProjectModifiers": []
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "journal_repair_strike",
              "eventId": "evt_repair_strike",
              "turn": 81,
              "guardianId": "guardian_beta",
              "guardianName": "Варак",
              "delta": -2,
              "reasonType": "rival_strike",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_repair_strike",
              "title": "Legacy strike",
              "summary": "Старый target-side strike с устаревшей attacker/project identity.",
              "visibility": "player_known",
              "relatedGuardianId": "guardian_beta",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectId": "proj_repair_strike",
                "projectName": "Ложное имя legacy strike",
                "projectType": "counter_rival_operation",
                "projectTier": "minor",
                "finalState": "Completed",
                "targetLoss": 2,
                "pressureDelta": 1,
                "stabilityDamage": 1
              }
            }
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var journalJson = await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath);
        Assert.NotNull(journalJson);
        var journalRoot = JsonNode.Parse(journalJson!)!.AsObject();
        var entries = journalRoot["entries"]?.AsArray();
        Assert.NotNull(entries);
        var repairedEntry = entries!.OfType<JsonObject>().Single();

        Assert.Equal("guardian_alpha", repairedEntry["relatedGuardianId"]?.GetValue<string>());
        Assert.Equal("guardian_alpha", repairedEntry["audit"]?["projectGuardianId"]?.GetValue<string>());
        Assert.Equal("Интрига на восстановление связи", repairedEntry["audit"]?["projectName"]?.GetValue<string>());
        Assert.Equal("offensive_intrigue", repairedEntry["audit"]?["projectType"]?.GetValue<string>());
        Assert.Equal("major", repairedEntry["audit"]?["projectTier"]?.GetValue<string>());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_RepairsLegacyPoliticalJournalFromPreTurnTrackerSnapshot()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "sessionId": "test-session", "requestId": "test-request", "turnNumber": 82 }
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
              "abodePower": { "currentPower": 72, "tier": "Могущественная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
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
            "abodePower": { "currentPower": 72, "tier": "Могущественная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """);

        const string preTurnTrackerJson = """
        {
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_preturn_journal_repair",
                "projectType": "abode_expansion",
                "projectTier": "major",
                "projectMode": "internal",
                "projectName": "Проект из pre-turn snapshot",
                "finalState": "Completed",
                "completionTurn": 80,
                "outcome": "Завершён раньше."
              }
            }
          ]
        }
        """;

        await _fs.WriteFileAtomicAsync("test_backups/preturn_guardian_projects_repair_journal.json", preTurnTrackerJson);
        await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot/game_state/meta/guardian_projects.json", preTurnTrackerJson);

        var manifest = new JsonObject
        {
            ["sessionId"] = "test-session",
            ["requestId"] = "test-request",
            ["turnNumber"] = 82,
            ["requestTimestamp"] = "2026-03-24T00:00:00Z",
            ["playerAction"] = "canonical-state-normalizer-test",
            ["files"] = new JsonObject
            {
                [GuardianProjectState.TrackerPath] = "game_state/control/pending_turn_snapshot/game_state/meta/guardian_projects.json"
            },
            ["snapshotFileHashes"] = new JsonObject
            {
                [GuardianProjectState.TrackerPath] = ComputeSha256(preTurnTrackerJson)
            },
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject
            {
                [GuardianProjectState.TrackerPath] = "test_backups/preturn_guardian_projects_repair_journal.json"
            },
            ["rollbackBaselineFiles"] = new JsonArray(GuardianProjectState.TrackerPath),
            ["sourceLabel"] = "canonical-state-normalizer-tests",
            ["manifestPayloadHash"] = string.Empty
        };
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await _fs.WriteFileAtomicAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);

        await _fs.WriteFileAtomicAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "journal_preturn_repair",
              "eventId": "evt_preturn_repair",
              "turn": 80,
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "delta": 2,
              "reasonType": "project_completion",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_preturn_journal_repair",
              "title": "Legacy pre-turn journal",
              "summary": "Repair должен добрать project identity из pending snapshot.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {}
            }
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await NormalizeAccumulatedStateWithTrackerBaselineAsync(normalizer);

        var journalJson = await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath);
        Assert.NotNull(journalJson);
        var journalRoot = JsonNode.Parse(journalJson!)!.AsObject();
        var repairedEntry = journalRoot["entries"]!.AsArray().OfType<JsonObject>().Single();
        var audit = repairedEntry["audit"]!.AsObject();

        Assert.Equal("guardian_alpha", audit["projectGuardianId"]?.GetValue<string>());
        Assert.Equal("proj_preturn_journal_repair", audit["projectId"]?.GetValue<string>());
        Assert.Equal("Проект из pre-turn snapshot", audit["projectName"]?.GetValue<string>());
        Assert.Equal("abode_expansion", audit["projectType"]?.GetValue<string>());
        Assert.Equal("major", audit["projectTier"]?.GetValue<string>());
        Assert.Equal("Completed", audit["finalState"]?.GetValue<string>());
    }

    private static string ComputeManifestPayloadHash(JsonObject manifest)
    {
        var clone = manifest.DeepClone().AsObject();
        clone["manifestPayloadHash"] = string.Empty;
        return ComputeSha256(clone.ToJsonString(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));
    }

    private static string ComputeSha256(string content)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }


}

