using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GuardianSystemRegressionTests
{
    [Fact]
    public async Task ValidateGameState_GuardianPowerEvents_NonPoliticalOfferingDoesNotRequireCurrentTrackerAuthority()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardianPowerEvents": [
            {
              "eventId": "evt_unreadable_tracker_power",
              "guardianId": "guardian_alpha",
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_unreadable_tracker",
              "title": "Non-political power event must not require current tracker authority",
              "summary": "Offering power events should not be blocked by unrelated tracker authority failures.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "offeringType": "soul_relic",
                "returnCycleId": "cycle_unreadable_tracker",
                "baseDelta": 2,
                "finalDelta": 2,
                "relicId": "relic_unreadable_tracker",
                "relicName": "Реликт отказа",
                "relicRarity": "Rare"
              }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, "{ invalid tracker");

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_unreadable_tracker_power.json",
            """
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
                    "appearanceDescription": "Power event guardian from validated baseline."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_tracker_unreadable_power_event.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.PowerJournalOfferings);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_missing_current_tracker_authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_EmptyGuardianPowerEventsArrayDoesNotRequireValidatedPreTurnJournalBaseline()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [],
          "guardianPowerEvents": []
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_empty_power_events_live_validation.json",
            """
            {
              "guardians": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.PowerJournalOfferings);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_missing_validated_preturn_journal_snapshot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_AbodePowerJournal_NonPoliticalOfferingDoesNotRequireCurrentTrackerAuthority()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": []
        }
        """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "journal_unreadable_tracker",
              "eventId": "evt_journal_unreadable_tracker",
              "turn": 12,
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_unreadable_tracker_journal",
              "title": "Non-political journal must not require current tracker authority",
              "summary": "Offering journal validation should not depend on unrelated tracker authority failures.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "offeringType": "soul_relic",
                "returnCycleId": "cycle_unreadable_tracker_journal",
                "baseDelta": 2,
                "finalDelta": 2,
                "relicId": "relic_unreadable_tracker_journal",
                "relicName": "Реликт журнала",
                "relicRarity": "Rare"
              }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, "{ invalid tracker");

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_unreadable_tracker_journal.json",
            """
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
                    "appearanceDescription": "Journal guardian from validated baseline."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_tracker_unreadable_journal.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.PowerJournalOfferings);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_missing_current_tracker_authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_GuardianPowerEvents_PoliticalCompletionStillRequiresCurrentTrackerAuthority()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardianPowerEvents": [
            {
              "eventId": "evt_unreadable_tracker_political_power",
              "guardianId": "guardian_alpha",
              "delta": 2,
              "reasonType": "project_completion",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_unreadable_tracker_political_power",
              "title": "Political power event must require current tracker authority",
              "summary": "Project-backed power events still need current tracker authority.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectGuardianId": "guardian_alpha",
                "projectId": "proj_unreadable_tracker_political_power",
                "projectName": "Политическая интрига",
                "projectType": "offensive_intrigue",
                "projectTier": "minor",
                "finalState": "Completed"
              }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, "{ invalid tracker");

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_unreadable_tracker_political_power.json",
            """
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
                    "appearanceDescription": "Political power event guardian from validated baseline."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_tracker_unreadable_political_power_event.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.PowerJournalOfferings);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_missing_current_tracker_authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_GuardianPowerEvents_PoliticalValidationUsesStrictTrackerAuthorityInsteadOfCompatibilityProjection()
    {
        const string trackerJson = """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_strict_tracker_current_power",
                "projectType": "offensive_intrigue",
                "projectTier": "minor",
                "projectMode": "offensive",
                "projectName": "Строгая интрига",
                "activeState": "Escalating the scheme",
                "totalWork": 12,
                "workDone": 8,
                "totalStages": 3,
                "currentStage": 2,
                "pressure": 4,
                "stability": 72
              }
            }
          ],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """;

        await WriteRawAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson("""
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
                "appearanceDescription": "Political power-event validation must not read compatibility tracker projection."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "evt_strict_tracker_current_power",
              "guardianId": "guardian_alpha",
              "delta": 2,
              "reasonType": "project_assist",
              "sourceSurface": "guardianProjectUpdates",
              "sourceId": "proj_strict_tracker_current_power",
              "title": "Political power event must use strict tracker authority",
              "summary": "Generic political power validation must not accept compatibility tracker projection when strict guardian baseline is broken.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectGuardianId": "guardian_alpha",
                "projectId": "proj_strict_tracker_current_power",
                "projectName": "Строгая интрига",
                "projectType": "offensive_intrigue",
                "projectTier": "minor"
              }
            }
          ]
        }
        """));

        await WriteRawAsync(GuardianProjectState.TrackerPath, trackerJson);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_strict_tracker_current_power.json",
            NormalizeGuardianStateJson("""
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
                    "appearanceDescription": "Validated pre-turn guardian baseline with broken strict authority surface."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ],
              "guardianPowerEvents": {}
        }
        """));
        await MirrorCurrentGuardiansToPreTurnSnapshotAsync("test_backups/preturn_guardians_repairable_legacy_related_guardian.json");

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_tracker_strict_tracker_current_power.json",
            trackerJson);

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_journal_strict_tracker_current_power.json",
            """
            {
              "entries": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.PowerJournalOfferings);

        Assert.Contains(issues, issue =>
            issue.FilePath.EndsWith("game_state/meta/guardians.json.guardianPowerEvents", StringComparison.OrdinalIgnoreCase) &&
            issue.Message.Contains("readable current guardian project tracker authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_AbodePowerJournal_PoliticalCompletionStillRequiresCurrentTrackerAuthority()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": []
        }
        """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "journal_unreadable_tracker_political",
              "eventId": "evt_journal_unreadable_tracker_political",
              "turn": 12,
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "delta": 2,
              "reasonType": "project_completion",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_unreadable_tracker_political_journal",
              "title": "Political journal entry must require current tracker authority",
              "summary": "Project-backed journal validation still needs current tracker authority.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectGuardianId": "guardian_alpha",
                "projectId": "proj_unreadable_tracker_political_journal",
                "projectName": "Политическая летопись",
                "projectType": "offensive_intrigue",
                "projectTier": "minor",
                "finalState": "Completed"
              }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, "{ invalid tracker");

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_unreadable_tracker_political_journal.json",
            """
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
                    "appearanceDescription": "Political journal guardian from validated baseline."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_tracker_unreadable_political_journal.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.PowerJournalOfferings);

        Assert.Contains(issues, issue =>
            issue.FilePath.EndsWith("game_state/meta/abode_power_journal.json", StringComparison.OrdinalIgnoreCase) &&
            issue.Message.Contains("readable current guardian project tracker authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_AbodePowerJournal_PoliticalValidationUsesStrictTrackerAuthorityInsteadOfCompatibilityProjection()
    {
        const string trackerJson = """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_strict_tracker_current_journal",
                "projectType": "offensive_intrigue",
                "projectTier": "minor",
                "projectMode": "offensive",
                "projectName": "Строгая летопись",
                "activeState": "Escalating the scheme",
                "totalWork": 12,
                "workDone": 8,
                "totalStages": 3,
                "currentStage": 2,
                "pressure": 4,
                "stability": 72
              }
            }
          ],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """;

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": []
        }
        """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "journal_strict_tracker_current_political",
              "eventId": "evt_journal_strict_tracker_current_political",
              "turn": 12,
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "delta": 2,
              "reasonType": "project_assist",
              "sourceSurface": "guardianProjectUpdates",
              "sourceId": "proj_strict_tracker_current_journal",
              "title": "Political journal validation must use strict tracker authority",
              "summary": "Generic political journal validation must not accept compatibility tracker projection when strict guardian baseline is broken.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectGuardianId": "guardian_alpha",
                "projectId": "proj_strict_tracker_current_journal",
                "projectName": "Строгая летопись",
                "projectType": "offensive_intrigue",
                "projectTier": "minor"
              }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, trackerJson);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_strict_tracker_current_journal.json",
            NormalizeGuardianStateJson("""
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
                    "appearanceDescription": "Validated pre-turn guardian baseline with broken strict authority surface for political journal."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ],
              "guardianPowerEvents": {}
            }
            """));

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_tracker_strict_tracker_current_journal.json",
            trackerJson);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.PowerJournalOfferings);

        Assert.Contains(issues, issue =>
            issue.FilePath.EndsWith("game_state/meta/abode_power_journal.json", StringComparison.OrdinalIgnoreCase) &&
            issue.Message.Contains("readable current guardian project tracker authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerEventValidation_NonPoliticalReasonTypesMustMatchRuntimeSourceSurfaces()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
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
                "appearanceDescription": "Guardian for non-political sourceSurface runtime contract."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "offering_evt_wrong_surface_runtime",
              "guardianId": "guardian_alpha",
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "life_evaluation",
              "sourceId": "offering_evt_wrong_surface_runtime",
              "title": "Offering must not reuse resonance surface",
              "summary": "Non-political guardian power events need a strict sourceSurface runtime contract.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_runtime_surface",
                "baseDelta": 2,
                "finalDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            },
            {
              "eventId": "resonance_evt_wrong_surface_runtime",
              "guardianId": "guardian_alpha",
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "resonance_evt_wrong_surface_runtime",
              "title": "Resonance must not reuse offering surface",
              "summary": "Resonance events belong only to the life evaluation surface.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            }
          ]
        }
        """);

        await EnsureReadableCurrentGuardianProjectTrackerAsync();
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.PowerJournalOfferings);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_reason_type_source_surface_mismatch", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".guardianPowerEvents[0].sourceSurface", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_reason_type_source_surface_mismatch", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".guardianPowerEvents[1].sourceSurface", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerEventValidation_OfferingRuntimeContractRejectsUnknownTypeAndDeltaFinalDeltaMismatch()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
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
                "appearanceDescription": "Guardian for strict offering runtime contract."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "offering_evt_invalid_type_runtime",
              "guardianId": "guardian_alpha",
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_evt_invalid_type_runtime",
              "title": "Unknown offeringType must fail generic validator",
              "summary": "Raw guardianPowerEvents must not rely on accepted-turn parser for invalid offeringType rejection.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "offeringType": "unknown_offering",
                "returnCycleId": "cycle_runtime_invalid_type",
                "baseDelta": 2,
                "finalDelta": 2
              }
            },
            {
              "eventId": "offering_evt_delta_mismatch_runtime",
              "guardianId": "guardian_alpha",
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_evt_delta_mismatch_runtime",
              "title": "Offering finalDelta must match top-level delta",
              "summary": "Generic power-event validator must reject offering delta/finalDelta mismatch before proof matching.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_runtime_delta_mismatch",
                "baseDelta": 2,
                "finalDelta": 1,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """);

        await EnsureReadableCurrentGuardianProjectTrackerAsync();
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.PowerJournalOfferings);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_offering_invalid_type", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_offering_delta_final_delta_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerEventValidation_OfferingRuntimeContractRejectsImpossibleDeterministicGain()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
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
                "appearanceDescription": "Guardian for impossible deterministic offering gain validation."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "offering_evt_impossible_gain_runtime",
              "guardianId": "guardian_alpha",
              "delta": 1,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_evt_impossible_gain_runtime",
              "title": "Impossible offering gain must fail generic validator",
              "summary": "Raw guardianPowerEvents must not accept authored deltas that disagree with canonical offering gain rules.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_runtime_impossible_gain",
                "baseDelta": 1,
                "finalDelta": 1,
                "inkFeathersOffered": 150,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """);

        await EnsureReadableCurrentGuardianProjectTrackerAsync();
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.PowerJournalOfferings);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_offering_delta_formula_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerEventValidation_OfferingRuntimeContractRejectsUnknownSoulRelicRarity()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
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
                "appearanceDescription": "Guardian for soul relic rarity runtime validation."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "offering_evt_invalid_relic_rarity_runtime",
              "guardianId": "guardian_alpha",
              "delta": 1,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_evt_invalid_relic_rarity_runtime",
              "title": "Unknown relic rarity must fail generic validator",
              "summary": "Soul relic offerings must use canonical rarity tiers instead of permissive fallback power gains.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "offeringType": "soul_relic",
                "returnCycleId": "cycle_runtime_invalid_relic_rarity",
                "baseDelta": 1,
                "finalDelta": 1,
                "relicId": "relic_invalid_rarity",
                "relicName": "Banana Relic",
                "relicRarity": "banana"
              }
            }
          ]
        }
        """);

        await EnsureReadableCurrentGuardianProjectTrackerAsync();
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.PowerJournalOfferings);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_offering_relic_invalid_rarity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerJournalValidation_DuplicateEntryIdAndEventIdFailIdentityContract()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
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
                "appearanceDescription": "Guardian for journal identity validation."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_power_duplicate_identity",
              "eventId": "offering_evt_duplicate_identity",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_duplicate_identity_001",
              "title": "First duplicate identity entry",
              "summary": "The journal identity contract requires unique entryId and eventId.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_duplicate_identity",
                "baseDelta": 2,
                "finalDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            },
            {
              "entryId": "abode_power_duplicate_identity",
              "eventId": "offering_evt_duplicate_identity",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_duplicate_identity_002",
              "title": "Second duplicate identity entry",
              "summary": "Reusing entryId or eventId must fail generic journal validation.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_duplicate_identity",
                "baseDelta": 2,
                "finalDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.PowerJournalOfferings);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_duplicate_entry_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_duplicate_event_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerEventValidation_DuplicateRawEventIdFailsIdentityContract()
    {
        const string guardiansJson = """
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
                "appearanceDescription": "Guardian for raw power-event identity validation."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "offering_evt_duplicate_raw_identity",
              "guardianId": "guardian_alpha",
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_duplicate_raw_identity_001",
              "title": "First raw duplicate identity event",
              "summary": "Raw guardianPowerEvents must not reuse eventId before journal materialization.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_duplicate_raw_identity",
                "baseDelta": 2,
                "finalDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            },
            {
              "eventId": "offering_evt_duplicate_raw_identity",
              "guardianId": "guardian_alpha",
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_duplicate_raw_identity_002",
              "title": "Second raw duplicate identity event",
              "summary": "Duplicate raw eventId must fail before kernel authorization and journal materialization.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_duplicate_raw_identity",
                "baseDelta": 2,
                "finalDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """;

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);
        await WriteRawAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson(guardiansJson));
        await EnsureValidatedPreTurnGuardiansSnapshotAsync(NormalizeGuardianStateJson("""
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
                "appearanceDescription": "Pre-turn guardian before raw duplicate identity validation."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
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
                "appearanceDescription": "Target guardian for strict tracker political metadata regression."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 52, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """));
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.PowerJournalOfferings);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_duplicate_raw_event_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerEventValidation_RawEventIdConflictingWithValidatedPreTurnJournalFailsIdentityContract()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
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
                "appearanceDescription": "Guardian for raw eventId conflict against validated pre-turn journal validation."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "offering_evt_conflict_with_current_journal",
              "guardianId": "guardian_alpha",
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_evt_conflict_with_current_journal",
              "title": "Raw eventId conflicts with validated pre-turn journal",
              "summary": "Raw guardianPowerEvents must not reuse a validated pre-turn journal eventId.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_conflict_current_journal",
                "baseDelta": 2,
                "finalDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_conflict_raw_event_id.json",
            """
        {
          "entries": [
            {
              "entryId": "abode_journal_conflict_current_journal_001",
              "eventId": "offering_evt_conflict_with_current_journal",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 11,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "prior_offering_evt_conflict_with_current_journal",
              "title": "Existing pre-turn journal event",
              "summary": "Validated pre-turn journal already owns this append-only eventId.",
              "visibility": "player_known",
              "appliedAt": "2026-03-27T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_conflict_current_journal",
                "baseDelta": 2,
                "finalDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_journal_conflict_current_journal_001",
              "eventId": "offering_evt_conflict_with_current_journal",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 11,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "prior_offering_evt_conflict_with_current_journal",
              "title": "Existing carried journal event",
              "summary": "Current journal carries forward the same append-only eventId from pre-turn baseline.",
              "visibility": "player_known",
              "appliedAt": "2026-03-27T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_conflict_current_journal",
                "baseDelta": 2,
                "finalDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """);

        await EnsureValidatedPreTurnGuardiansSnapshotAsync(NormalizeGuardianStateJson("""
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
                "appearanceDescription": "Pre-turn guardian before raw eventId conflict validation."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """));
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.PowerJournalOfferings);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_raw_event_id_conflicts_with_validated_preturn_journal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerEventValidation_DuplicateRawResonanceForSameLifeFailsIdentityContract()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
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
                "appearanceDescription": "Guardian for raw duplicate resonance life-scope validation."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "resonance_evt_raw_same_life_001",
              "guardianId": "guardian_alpha",
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_raw_same_life_001",
              "title": "First raw resonance for same life",
              "summary": "Raw authority input must allow only one resonance per completed life.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "lifeId": "life_raw_duplicate_same_life",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            },
            {
              "eventId": "resonance_evt_raw_same_life_002",
              "guardianId": "guardian_alpha",
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_raw_same_life_002",
              "title": "Second raw resonance for same life",
              "summary": "Different eventId must not bypass same-life resonance uniqueness at raw authority input.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "lifeId": "life_raw_duplicate_same_life",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": []
        }
        """);

        await EnsureValidatedPreTurnGuardiansSnapshotAsync(NormalizeGuardianStateJson("""
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
                "appearanceDescription": "Pre-turn guardian before raw duplicate same-life resonance validation."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """));
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.PowerJournalOfferings);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_duplicate_raw_resonance_for_same_life", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PendingAbodeOfferingValidation_SoulRelicRequiresCanonicalRarity()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync(GuardianAbodeOfferingState.PendingRequestPath, """
        {
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "offeringType": "soul_relic",
          "relicId": "relic_invalid_pending_rarity",
          "relicName": "Banana Relic",
          "relicRarity": "banana",
          "returnCycleId": "return_12",
          "createdAtUtc": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.PowerJournalOfferings);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_soul_relic_invalid_rarity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerEventValidation_ResonanceRuntimeContractRequiresLifeIdAndCanonicalDelta()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
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
                "appearanceDescription": "Guardian for strict resonance runtime contract."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "resonance_evt_missing_life_runtime",
              "guardianId": "guardian_alpha",
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_missing_life_runtime",
              "title": "Resonance must carry lifeId",
              "summary": "Generic resonance contract must require explicit life identity.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            },
            {
              "eventId": "resonance_evt_negative_delta_runtime",
              "guardianId": "guardian_alpha",
              "delta": -1,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_negative_delta_runtime",
              "title": "Resonance delta must stay positive and match finalDelta",
              "summary": "Generic resonance contract must reject negative or mismatched authored deltas.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "lifeId": "life_negative_delta_runtime",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 2
              }
            }
          ]
        }
        """);

        await EnsureReadableCurrentGuardianProjectTrackerAsync();
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.PowerJournalOfferings);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "missing_required_string", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".guardianPowerEvents[0].audit.lifeId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_resonance_delta_sign_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_resonance_delta_final_delta_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerJournalValidation_NonPoliticalReasonTypesMustMatchRuntimeSourceSurfaces()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
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
                "appearanceDescription": "Guardian for journal non-political sourceSurface contract."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await MirrorCurrentGuardiansToPreTurnSnapshotAsync("test_backups/preturn_guardians_journal_non_political_surface.json");
        await EnsureReadableCurrentGuardianProjectTrackerAsync();
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "journal_offering_wrong_surface_runtime",
              "eventId": "evt_journal_offering_wrong_surface_runtime",
              "turn": 12,
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "life_evaluation",
              "sourceId": "journal_offering_wrong_surface_runtime",
              "title": "Offering journal must not reuse life evaluation surface",
              "summary": "Generic journal validator must enforce non-political sourceSurface runtime contract.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_journal_runtime_surface",
                "baseDelta": 2,
                "finalDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            },
            {
              "entryId": "journal_resonance_wrong_surface_runtime",
              "eventId": "evt_journal_resonance_wrong_surface_runtime",
              "turn": 12,
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "journal_resonance_wrong_surface_runtime",
              "title": "Resonance journal must not reuse offering surface",
              "summary": "Resonance journal entries belong only to life evaluation surface.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.PowerJournalOfferings);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_reason_type_source_surface_mismatch", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".entries[0].sourceSurface", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_reason_type_source_surface_mismatch", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".entries[1].sourceSurface", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnAbodeOffering_InvalidPoliticalJournalBeatsBrokenTrackerAuthority()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: ABODE_OFFERING] 100 Чернильных Перьев"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
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
                "appearanceDescription": "Current guardian after accepted-turn offering with malformed political journal entry and broken tracker authority."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();
        await WriteRawAsync(GuardianProjectState.TrackerPath, "{");

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_journal_valid_offering_before_invalid_political",
              "eventId": "offering_evt_valid_before_invalid_political",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_valid_before_invalid_political",
              "title": "Valid offering event before invalid political entry",
              "summary": "Offering proof still depends on whole journal being canonical.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            },
            {
              "entryId": "abode_journal_invalid_political_beats_tracker",
              "eventId": "political_evt_invalid_beats_tracker",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 3,
              "reasonType": "project_completion",
              "sourceSurface": "guardianProjectUpdates",
              "sourceId": "proj_invalid_beats_tracker",
              "title": "Malformed political entry must beat broken tracker authority",
              "summary": "Current journal proof should classify malformed political entries before tracker authority failure.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "projectGuardianId": "guardian_alpha",
                "projectId": "proj_invalid_beats_tracker",
                "projectName": "Некорректный политический журнал",
                "projectType": "abode_expansion",
                "projectTier": "major",
                "finalState": "Completed"
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_invalid_political_beats_tracker.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "ink_feathers",
              "inkFeathersOffered": 100,
              "returnCycleId": "cycle_12",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_accepted_offering_invalid_political_beats_tracker.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_accepted_offering_invalid_political_beats_tracker.json",
            NormalizeGuardianStateJson("""
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
                    "appearanceDescription": "Pre-turn guardian before invalid political journal should beat broken tracker authority."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_accepted_invalid_political_beats_tracker.json",
            """
            {
              "entries": []
            }
            """);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "ABODE_OFFERING",
          "resolutionType": "abodeOffering",
          "summary": "Malformed political journal entries must beat broken tracker authority in accepted-turn offering proof.",
          "resolved": true,
          "costInFeathers": 100,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "powerGain": 2,
            "returnCycleId": "cycle_12",
            "powerEventId": "offering_evt_valid_before_invalid_political",
            "affectedFiles": [
              "game_state/meta/guardians.json",
              "game_state/meta/abode_power_journal.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_current_journal_proof", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_current_tracker_authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnAbodeOffering_UnrelatedInvalidRawPowerEventsDoNotPoisonJournalProofButInvalidateCurrentGuardianAuthorityOutcome()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: ABODE_OFFERING] 100 Чернильных Перьев"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
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
                "appearanceDescription": "Current guardian after offering with unrelated invalid raw political event."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "invalid_raw_political_for_offering",
              "guardianId": "guardian_alpha",
              "delta": 3,
              "reasonType": "project_completion",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "project_invalid_raw_for_offering",
              "title": "Invalid raw political event should not block non-political offering proof",
              "summary": "Raw invalid power-event authority must not poison non-political offering journal proof.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "projectGuardianId": "guardian_alpha",
                "projectName": "Broken political payload",
                "projectType": "offensive_intrigue",
                "projectTier": "minor",
                "finalState": "Completed"
              }
            }
          ]
        }
        """);
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
        }
        """);
        await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_journal_unrelated_invalid_raw_offering_001",
              "eventId": "offering_evt_unrelated_invalid_raw_offering",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_unrelated_invalid_raw_offering",
              "title": "Non-political offering proof must ignore unrelated invalid raw political events",
              "summary": "Current offering journal proof should not map unrelated raw power-event failure to guardian authority failure.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_unrelated_invalid_raw_guardian_event.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "ink_feathers",
              "inkFeathersOffered": 100,
              "returnCycleId": "cycle_12",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_unrelated_invalid_raw_guardian_event_offering.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_unrelated_invalid_raw_guardian_event_offering.json",
            NormalizeGuardianStateJson("""
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
                    "appearanceDescription": "Pre-turn guardian before offering with unrelated invalid raw political event."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_unrelated_invalid_raw_guardian_event_offering.json",
            """
            {
              "entries": []
            }
            """);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "ABODE_OFFERING",
          "resolutionType": "abodeOffering",
          "summary": "Unrelated invalid raw power events must not poison journal proof, but current guardian authority must still fail closed for offering outcome.",
          "resolved": true,
          "costInFeathers": 100,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "powerGain": 2,
            "returnCycleId": "cycle_12",
            "powerEventId": "offering_evt_unrelated_invalid_raw_offering",
            "affectedFiles": [
              "game_state/meta/guardians.json",
              "game_state/meta/abode_power_journal.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_current_guardian_authority", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_current_journal_proof", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_power_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnAbodeOffering_NonArrayGuardianPowerEventsRaiseCurrentGuardianAuthorityError()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: ABODE_OFFERING] 100 Чернильных Перьев"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
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
                "appearanceDescription": "Current guardian after offering with malformed non-array guardianPowerEvents."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": {}
        }
        """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_journal_non_array_guardian_power_events_001",
              "eventId": "offering_evt_non_array_guardian_power_events",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_non_array_guardian_power_events",
              "title": "Offering journal stays canonical while raw guardianPowerEvents surface is malformed",
              "summary": "ABODE_OFFERING must fail on strict current guardian authority before reading sanitized power.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_non_array_guardian_power_events.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "ink_feathers",
              "inkFeathersOffered": 100,
              "returnCycleId": "cycle_12",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_non_array_guardian_power_events_offering.json",
            NormalizeGuardianStateJson("""
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
                    "appearanceDescription": "Pre-turn guardian before offering non-array power-event regression."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_non_array_guardian_power_events_offering.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_non_array_guardian_power_events_offering.json",
            """
            {
              "entries": []
            }
            """);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "ABODE_OFFERING",
          "resolutionType": "abodeOffering",
          "summary": "Malformed non-array guardianPowerEvents must invalidate strict current guardian authority for offering outcome.",
          "resolved": true,
          "costInFeathers": 100,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "powerGain": 2,
            "returnCycleId": "cycle_12",
            "powerEventId": "offering_evt_non_array_guardian_power_events",
            "affectedFiles": [
              "game_state/meta/guardians.json",
              "game_state/meta/abode_power_journal.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_current_guardian_authority", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_power_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnAbodeOffering_SameGuardianSnapshotRawPoliticalEventsAffectGuardianBaseline()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: ABODE_OFFERING] 100 Чернильных Перьев"
        }
        """);

        const string currentGuardiansJson = """
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
                "appearanceDescription": "Current guardian after offering with unrelated snapshot raw political history."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        await WriteRawAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson(currentGuardiansJson));
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_journal_snapshot_raw_political_offering_001",
              "eventId": "offering_evt_snapshot_raw_political_offering",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_snapshot_raw_political_offering",
              "title": "Same-guardian snapshot raw political events must still affect accepted-turn offering baseline",
              "summary": "Offering proof must compare against guardian baseline that already includes canonical same-guardian political power provenance.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_snapshot_raw_political_guardian_baseline.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "ink_feathers",
              "inkFeathersOffered": 100,
              "returnCycleId": "cycle_12",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        var snapshotGuardiansRoot = JsonNode.Parse(NormalizeGuardianStateJson("""
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
                "appearanceDescription": "Validated snapshot guardian before offering with same-guardian political raw power provenance."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guardian_beta", "attitudeScore": 80, "attitudeTier": "trusted", "reason": "Ancient pact", "lastChangedAt": null }
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
                "appearanceDescription": "Target guardian needed to materialize same-guardian political snapshot authority."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 12, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 30, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guardian_alpha", "attitudeScore": 55, "attitudeTier": "ally", "reason": "Mutual accord", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """))!.AsObject();
        snapshotGuardiansRoot["guardianPowerEvents"] = new JsonArray
        {
            new JsonObject
            {
                ["eventId"] = "snapshot_political_evt_for_guardian_offering_baseline",
                ["guardianId"] = "guardian_alpha",
                ["delta"] = GuardianProjectState.GetDefaultTerminalAbodePowerDelta("offensive_intrigue", "Completed", "major"),
                ["reasonType"] = "project_completion",
                ["sourceSurface"] = "completeGuardianProjects",
                ["sourceId"] = "proj_snapshot_political_for_guardian_offering_baseline",
                ["title"] = "Snapshot project completion should raise same-guardian offering baseline",
                ["summary"] = "Same-guardian political raw power provenance must contribute to previousPower before the current offering is compared.",
                ["visibility"] = "player_known",
                ["appliedAt"] = "2026-03-27T00:00:00Z",
                ["audit"] = new JsonObject
                {
                    ["projectGuardianId"] = "guardian_alpha",
                    ["projectId"] = "proj_snapshot_political_for_guardian_offering_baseline",
                    ["projectName"] = "Разрыв древнего пакта перед offering",
                    ["projectType"] = "offensive_intrigue",
                    ["projectTier"] = "major",
                    ["finalState"] = "Completed"
                }
            }
        };
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_snapshot_raw_political_nonpolitical_offering.json",
            snapshotGuardiansRoot.ToJsonString());

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_tracker_snapshot_raw_political_nonpolitical_offering.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [],
              "temporaryProjectModifiers": [],
              "startGuardianProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_snapshot_political_for_guardian_offering_baseline",
                    "projectType": "offensive_intrigue",
                    "projectTier": "major",
                    "projectMode": "offensive",
                    "projectName": "Разрыв древнего пакта перед offering",
                    "targetGuardianId": "guardian_beta",
                    "betrayalReason": "The pact is broken after a deliberate transgression.",
                    "activeState": "Closing the strike",
                    "totalWork": 18,
                    "workDone": 18,
                    "totalStages": 3,
                    "currentStage": 3,
                    "pressure": 8,
                    "stability": 72
                  }
                }
              ],
              "completeGuardianProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "projectId": "proj_snapshot_political_for_guardian_offering_baseline",
                  "finalState": "Completed",
                  "outcome": "Same-guardian political provenance must raise the offering baseline before the current request is applied.",
                  "offensiveImpactAudit": {
                    "targetLoss": 3
                  }
                }
              ]
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_snapshot_raw_political_nonpolitical_offering.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_snapshot_raw_political_nonpolitical_offering.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "ABODE_OFFERING",
          "resolutionType": "abodeOffering",
          "summary": "Same-guardian snapshot raw political events must still affect accepted-turn offering guardian baseline.",
          "resolved": true,
          "costInFeathers": 100,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "powerGain": 2,
            "returnCycleId": "cycle_12",
            "powerEventId": "offering_evt_snapshot_raw_political_offering",
            "affectedFiles": [
              "game_state/meta/guardians.json",
              "game_state/meta/abode_power_journal.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_power_missing", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "abode_offering_expected_power_gain_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AbodeOfferingResolution_MalformedRelevantSnapshotRawOfferingWithoutReasonTypeInvalidatesValidatedSnapshotGuardians()
    {
        const string preTurnGuardiansJson = """
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
                "appearanceDescription": "Pre-turn guardian before pending offering malformed snapshot raw-event regression."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        const string currentGuardiansJson = """
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
                "appearanceDescription": "Current guardian after pending offering malformed snapshot raw-event regression."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson(currentGuardiansJson));
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """);
        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "pending_offering_snapshot_reason_type_missing_current_001",
              "eventId": "offering_evt_pending_snapshot_reason_type_missing_current",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_pending_snapshot_reason_type_missing_current",
              "title": "Pending offering current journal stays canonical",
              "summary": "Malformed validated snapshot raw offering event must invalidate guardian proof before power comparison.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """);

        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_snapshot_reason_type_missing.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "ink_feathers",
              "inkFeathersOffered": 100,
              "returnCycleId": "cycle_12",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        var snapshotGuardiansRoot = JsonNode.Parse(preTurnGuardiansJson)!.AsObject();
        snapshotGuardiansRoot["guardianPowerEvents"] = JsonNode.Parse("""
        [
          {
            "eventId": "snapshot_offering_missing_reason_type",
            "guardianId": "guardian_alpha",
            "delta": 2,
            "sourceSurface": "guardianAbodeOffering",
            "sourceId": "snapshot_offering_missing_reason_type",
            "title": "Malformed snapshot offering raw event must not opt out",
            "summary": "Pending offering power proof must fail closed when a relevant snapshot raw event hides its reasonType.",
            "visibility": "player_known",
            "appliedAt": "2026-03-27T00:00:00Z",
            "audit": {
              "offeringType": "ink_feathers",
              "returnCycleId": "cycle_12",
              "baseDelta": 2,
              "finalDelta": 2,
              "inkFeathersOffered": 100,
              "capRemainingBefore": 150
            }
          }
        ]
        """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_snapshot_reason_type_missing_pending_offering.json",
            snapshotGuardiansRoot.ToJsonString());

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_tracker_snapshot_reason_type_missing_pending_offering.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_snapshot_reason_type_missing_pending_offering.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_snapshot_reason_type_missing_pending_offering.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.PowerJournalOfferings);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_validated_snapshot_guardians", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_expected_power_gain_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianResonanceValidation_MalformedSnapshotRawEntryWithoutReasonTypeInvalidatesValidatedSnapshotGuardians()
    {
        const string currentGuardiansJson = """
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
                "appearanceDescription": "Guardian for malformed snapshot raw resonance relevance regression."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        await WriteRawAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson(currentGuardiansJson));

        var snapshotGuardiansRoot = JsonNode.Parse(NormalizeGuardianStateJson(currentGuardiansJson))!.AsObject();
        snapshotGuardiansRoot["guardianPowerEvents"] = JsonNode.Parse("""
        [
          {
            "eventId": "snapshot_resonance_missing_reason_type",
            "guardianId": "guardian_alpha",
            "delta": 7,
            "sourceSurface": "life_evaluation",
            "sourceId": "life_eval_snapshot_resonance_missing_reason_type",
            "title": "Malformed snapshot resonance raw event must not opt out",
            "summary": "Strict resonance snapshot proof must fail closed when a relevant raw entry omits reasonType.",
            "visibility": "player_known",
            "appliedAt": "2026-03-27T00:00:00Z",
            "audit": {
              "lifeId": "life_snapshot_resonance_missing_reason_type",
              "domainAlignment": 8,
              "worldScale": 7,
              "permanence": 6,
              "sacrifice": 5,
              "publicImpact": 4,
              "resonanceScore": 30,
              "classification": "meaningful resonance",
              "finalDelta": 7
            }
          }
        ]
        """);
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_snapshot_missing_reason_type_resonance.json",
            snapshotGuardiansRoot.ToJsonString());

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_snapshot_missing_reason_type_resonance.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["sourceLabel"] = LifeEvaluationRewardAnalyzer.AutomaticLifeEvaluationSourceLabel;
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "resonance_snapshot_reason_type_missing_current",
              "eventId": "resonance_evt_snapshot_reason_type_missing_current",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 7,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_snapshot_reason_type_missing_current",
              "title": "Current resonance stays canonical",
              "summary": "Malformed snapshot raw entries must invalidate guardians proof before current resonance can rely on it.",
              "category": "other",
              "content": "Malformed snapshot raw entries must invalidate guardians proof before current resonance can rely on it.",
              "discoveredAt": "2026-03-28T00:00:00Z",
              "discoveryContext": "life_evaluation",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "lifeId": "life_snapshot_reason_type_missing_current",
                "domainAlignment": 8,
                "worldScale": 7,
                "permanence": 6,
                "sacrifice": 5,
                "publicImpact": 4,
                "resonanceScore": 30,
                "classification": "meaningful resonance",
                "finalDelta": 7
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.PowerJournalOfferings);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_validated_snapshot_guardians", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_validated_snapshot_journal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnAbodeOffering_UnrelatedOtherGuardianMalformedSnapshotOfferingEventDoesNotInvalidateTargetProof()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: ABODE_OFFERING] 100 Чернильных Перьев"
        }
        """);

        const string currentGuardiansJson = """
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
                "appearanceDescription": "Current guardian after offering with unrelated malformed snapshot offering history for another guardian."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        await WriteRawAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson(currentGuardiansJson));
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_journal_other_guardian_snapshot_offering_001",
              "eventId": "offering_evt_other_guardian_snapshot_offering",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_other_guardian_snapshot_offering",
              "title": "Another guardian's malformed snapshot offering event must stay irrelevant",
              "summary": "Accepted-turn offering proof should scope snapshot raw offering history to the current guardian request.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_other_guardian_snapshot_offering.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "ink_feathers",
              "inkFeathersOffered": 100,
              "returnCycleId": "cycle_12",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        var snapshotGuardiansRoot = JsonNode.Parse(NormalizeGuardianStateJson("""
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
                "appearanceDescription": "Validated snapshot guardian before offering."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """))!.AsObject();
        snapshotGuardiansRoot["guardianPowerEvents"] = JsonNode.Parse("""
        [
          {
            "eventId": "snapshot_other_guardian_offering_missing_reason_type",
            "guardianId": "guardian_beta",
            "delta": 1,
            "sourceSurface": "guardianAbodeOffering",
            "sourceId": "snapshot_other_guardian_offering_missing_reason_type",
            "title": "Malformed other-guardian snapshot offering event",
            "summary": "Guardian-scoped offering proof must ignore malformed snapshot offering history that clearly belongs to another guardian.",
            "visibility": "player_known",
            "appliedAt": "2026-03-27T00:00:00Z",
            "audit": {
              "offeringType": "ink_feathers",
              "returnCycleId": "cycle_12",
              "baseDelta": 1,
              "finalDelta": 1,
              "inkFeathersOffered": 50,
              "capRemainingBefore": 150
            }
          }
        ]
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_other_guardian_snapshot_offering.json",
            snapshotGuardiansRoot.ToJsonString());

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_tracker_other_guardian_snapshot_offering.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_other_guardian_snapshot_offering.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_other_guardian_snapshot_offering.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "ABODE_OFFERING",
          "resolutionType": "abodeOffering",
          "summary": "Another guardian's malformed snapshot offering event must stay out of the current offering proof scope.",
          "resolved": true,
          "costInFeathers": 100,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "powerGain": 2,
            "returnCycleId": "cycle_12",
            "powerEventId": "offering_evt_other_guardian_snapshot_offering",
            "affectedFiles": [
              "game_state/meta/guardians.json",
              "game_state/meta/abode_power_journal.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_action_invalid_validated_snapshot_data", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_validated_snapshot_guardians", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "abode_offering_invalid_validated_snapshot_tracker", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnAbodeOffering_MalformedSnapshotTrackerInvalidatesGuardianBaselineMaterialization()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: ABODE_OFFERING] 100 Чернильных Перьев"
        }
        """);

        const string currentGuardiansJson = """
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
                "appearanceDescription": "Malformed snapshot tracker must invalidate accepted-turn offering guardian baseline materialization."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        await WriteRawAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson(currentGuardiansJson));
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_journal_malformed_snapshot_tracker_001",
              "eventId": "offering_evt_malformed_snapshot_tracker",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_malformed_snapshot_tracker",
              "title": "Malformed snapshot tracker must invalidate guardian baseline materialization",
              "summary": "Accepted-turn offering cannot trust stale guardian baseline when snapshot tracker side effects are unreadable.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_malformed_snapshot_tracker.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "ink_feathers",
              "inkFeathersOffered": 100,
              "returnCycleId": "cycle_12",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_malformed_snapshot_tracker_offering.json",
            NormalizeGuardianStateJson("""
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
                    "appearanceDescription": "Target guardian snapshot before offering with malformed tracker baseline."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """));

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_tracker_malformed_for_offering_baseline.json",
            "{ malformed snapshot tracker");

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_malformed_snapshot_tracker.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_malformed_snapshot_tracker.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "ABODE_OFFERING",
          "resolutionType": "abodeOffering",
          "summary": "Malformed snapshot tracker must invalidate offering guardian baseline materialization.",
          "resolved": true,
          "costInFeathers": 100,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "powerGain": 2,
            "returnCycleId": "cycle_12",
            "powerEventId": "offering_evt_malformed_snapshot_tracker",
            "affectedFiles": [
              "game_state/meta/guardians.json",
              "game_state/meta/abode_power_journal.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_action_invalid_validated_snapshot_data", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "abode_offering_invalid_validated_snapshot_guardians", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnAbodeOffering_SnapshotTrackerUnknownGuardianModifierFailsStrictProof()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: ABODE_OFFERING] 100 Чернильных Перьев"
        }
        """);

        const string currentGuardiansJson = """
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
                "appearanceDescription": "Snapshot proof must reject tracker modifiers that point at unknown guardians."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        await WriteRawAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson(currentGuardiansJson));
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_journal_unknown_modifier_snapshot_001",
              "eventId": "offering_evt_unknown_modifier_snapshot_tracker",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_unknown_modifier_snapshot_tracker",
              "title": "Snapshot tracker modifier authority must stay strict",
              "summary": "Accepted-turn offering must reject validated snapshot tracker modifiers that point at unknown guardians.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_unknown_modifier_snapshot.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "ink_feathers",
              "inkFeathersOffered": 100,
              "returnCycleId": "cycle_12",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_unknown_modifier_snapshot_offering.json",
            NormalizeGuardianStateJson("""
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
                    "appearanceDescription": "Target guardian snapshot before offering with strict tracker proof."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """));

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_tracker_unknown_modifier_snapshot_offering.json",
            """
            {
              "activeProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_snapshot_modifier_scope",
                    "projectType": "abode_expansion",
                    "projectTier": "minor",
                    "projectMode": "internal",
                    "projectName": "Scope snapshot tracker to offering guardian",
                    "activeState": "Tracker baseline touches the offered guardian",
                    "totalWork": 8,
                    "workDone": 1,
                    "totalStages": 2,
                    "currentStage": 0,
                    "pressure": 0,
                    "stability": 10,
                    "startedTurn": 11
                  }
                }
              ],
              "completedProjects": [],
              "temporaryProjectModifiers": [
                {
                  "modifierId": "tmp_unknown_modifier",
                  "guardianId": "guardian_missing",
                  "modifierType": "next_internal_project_starting_pressure",
                  "value": 2,
                  "remainingApplications": 1
                }
              ]
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_unknown_modifier_snapshot.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_unknown_modifier_snapshot.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "ABODE_OFFERING",
          "resolutionType": "abodeOffering",
          "summary": "Unknown-guardian snapshot tracker modifier must invalidate strict offering proof.",
          "resolved": true,
          "costInFeathers": 100,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "powerGain": 2,
            "returnCycleId": "cycle_12",
            "powerEventId": "offering_evt_unknown_modifier_snapshot_tracker",
            "affectedFiles": [
              "game_state/meta/guardians.json",
              "game_state/meta/abode_power_journal.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_action_invalid_validated_snapshot_data", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "abode_offering_invalid_validated_snapshot_guardians", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "abode_offering_invalid_validated_snapshot_tracker", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnAbodeOffering_MissingSnapshotTrackerDoesNotBlockIrrelevantGuardianBaseline()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: ABODE_OFFERING] 100 Чернильных Перьев"
        }
        """);

        const string currentGuardiansJson = """
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
                "appearanceDescription": "Missing snapshot tracker must invalidate accepted-turn offering guardian baseline materialization."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        await WriteRawAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson(currentGuardiansJson));
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "abode_journal_missing_snapshot_tracker_001",
              "eventId": "offering_evt_missing_snapshot_tracker",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_missing_snapshot_tracker",
              "title": "Missing snapshot tracker must invalidate guardian baseline materialization",
              "summary": "Accepted-turn offering cannot trust stale guardian baseline when snapshot tracker provenance is missing.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_missing_snapshot_tracker.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "ink_feathers",
              "inkFeathersOffered": 100,
              "returnCycleId": "cycle_12",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_missing_snapshot_tracker_offering.json",
            NormalizeGuardianStateJson("""
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
                    "appearanceDescription": "Target guardian snapshot before offering with missing tracker baseline."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_missing_snapshot_tracker.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_missing_snapshot_tracker.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await RemoveTrackedSnapshotEntryFromCurrentPendingTurnSnapshotAsync(GuardianProjectState.TrackerPath);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "ABODE_OFFERING",
          "resolutionType": "abodeOffering",
          "summary": "Missing snapshot tracker must invalidate offering guardian baseline materialization.",
          "resolved": true,
          "costInFeathers": 100,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "powerGain": 2,
            "returnCycleId": "cycle_12",
            "powerEventId": "offering_evt_missing_snapshot_tracker",
            "affectedFiles": [
              "game_state/meta/guardians.json",
              "game_state/meta/abode_power_journal.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_action_invalid_validated_snapshot_data", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "abode_offering_invalid_validated_snapshot_guardians", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "abode_offering_invalid_validated_snapshot_tracker", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnDonateToGuardian_MissingSnapshotTrackerInvalidatesGuardianBaselineMaterialization()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: DONATE_TO_GUARDIAN] 30 Чернильных Перьев"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
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
                "appearanceDescription": "Missing snapshot tracker must invalidate accepted-turn donation guardian baseline materialization."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_missing_snapshot_tracker_donation.json",
            NormalizeGuardianStateJson("""
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
                    "appearanceDescription": "Target guardian snapshot before donation with missing tracker provenance."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 20, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_missing_snapshot_tracker_donation.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await RemoveTrackedSnapshotEntryFromCurrentPendingTurnSnapshotAsync(GuardianProjectState.TrackerPath);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "DONATE_TO_GUARDIAN",
          "resolutionType": "guardianReputation",
          "summary": "Missing snapshot tracker must invalidate donation guardian baseline materialization.",
          "resolved": true,
          "costInFeathers": 30,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "reputationChange": 10,
            "affectedFiles": [
              "game_state/meta/guardians.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_action_invalid_validated_snapshot_data", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_reputation_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnGuardianFavor_MissingSnapshotTrackerInvalidatesGuardianBaselineMaterialization()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: GUARDIAN_FAVOR] 30 Чернильных Перьев"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
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
                "appearanceDescription": "Missing snapshot tracker must invalidate accepted-turn guardian favor baseline materialization."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_missing_snapshot_tracker_favor.json",
            NormalizeGuardianStateJson("""
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
                    "appearanceDescription": "Target guardian snapshot before favor with missing tracker provenance."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 20, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_missing_snapshot_tracker_favor.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await RemoveTrackedSnapshotEntryFromCurrentPendingTurnSnapshotAsync(GuardianProjectState.TrackerPath);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "GUARDIAN_FAVOR",
          "resolutionType": "guardianReputation",
          "summary": "Missing snapshot tracker must invalidate guardian favor baseline materialization.",
          "resolved": true,
          "costInFeathers": 30,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "reputationChange": 10,
            "affectedFiles": [
              "game_state/meta/guardians.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_action_invalid_validated_snapshot_data", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_favor_reputation_missing", StringComparison.OrdinalIgnoreCase));
    }

    private static void EnsureGuardianNavigationState(JsonObject root)
    {
        if (root["activeGuardian"] is not JsonObject activeGuardian ||
            activeGuardian["abode"] is not JsonObject abode)
            return;
        var abodeId = abode["abodeId"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(abodeId))
            return;
        if (root["chaosSeaNavigation"] is not JsonObject navigation)
        {
            navigation = new JsonObject();
            root["chaosSeaNavigation"] = navigation;
        }
        navigation["currentAbodeId"] ??= abodeId;
        if (navigation["discoveredAbodes"] is not JsonArray discoveredAbodes)
        {
            discoveredAbodes = new JsonArray();
            navigation["discoveredAbodes"] = discoveredAbodes;
        }
        if (!discoveredAbodes.Any(node => string.Equals(node?.GetValue<string>(), abodeId, StringComparison.OrdinalIgnoreCase)))
            discoveredAbodes.Add(abodeId);
    }

    private JsonObject CreateTestSnapshotManifest()
    {
        var sessionId = "test-session";
        var requestId = "test-request";
        var turnNumber = 12;
        var turnRequestPath = _fs.ResolvePath("input/turn_request.json");
        if (File.Exists(turnRequestPath))
        {
            var turnRequestJson = File.ReadAllText(turnRequestPath);
            if (!string.IsNullOrWhiteSpace(turnRequestJson) && JsonNode.Parse(turnRequestJson) is JsonObject turnRequest)
            {
                sessionId = turnRequest["sessionId"]?.GetValue<string>() ?? sessionId;
                requestId = turnRequest["requestId"]?.GetValue<string>() ?? requestId;
                turnNumber = turnRequest["turnNumber"]?.GetValue<int>() ?? turnNumber;
            }
        }
        return new JsonObject
        {
            ["sessionId"] = sessionId, ["requestId"] = requestId, ["turnNumber"] = turnNumber,
            ["requestTimestamp"] = "2026-03-24T00:00:00Z", ["playerAction"] = "guardian-regression-test",
            ["files"] = new JsonObject(), ["snapshotFileHashes"] = new JsonObject(),
            ["clientOwnedValidationHashes"] = new JsonObject(), ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = new JsonArray(), ["sourceLabel"] = "guardian-system-regression-tests",
            ["manifestPayloadHash"] = string.Empty
        };
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
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(content)));
    }

    private async Task<JsonObject> ReadObjectAsync(string path)
    {
        var raw = await _fs.ReadFileAsync(path);
        Assert.False(string.IsNullOrWhiteSpace(raw));
        var node = JsonNode.Parse(raw!) as JsonObject;
        Assert.NotNull(node);
        return node!;
    }

    private async Task MirrorCurrentGuardiansToPreTurnSnapshotAsync(string backupPath)
    {
        var raw = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        Assert.False(string.IsNullOrWhiteSpace(raw));
        await WritePreTurnTrackedFileAsync("game_state/meta/guardians.json", backupPath,
            BuildValidatedPreTurnGuardiansSnapshotRoot(raw!).ToJsonString());
    }

    private async Task WriteCurrentGuardiansNormalizerBackupAsync(string backupPath)
    {
        var raw = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        Assert.False(string.IsNullOrWhiteSpace(raw));
        var guardiansRoot = JsonNode.Parse(raw!) as JsonObject;
        Assert.NotNull(guardiansRoot);
        guardiansRoot!.Remove("UpdateGuardians");
        guardiansRoot.Remove("guardianPowerEvents");
        guardiansRoot.Remove(GuardianTradeRequestState.UpdateReceiptsProperty);
        await _fs.WriteFileAtomicAsync(backupPath, guardiansRoot.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));
    }

    private Task EnsureReadableCurrentGuardianProjectTrackerAsync(string? currentTrackerJson = null) =>
        _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, currentTrackerJson ?? EmptyGuardianProjectTrackerJson);

    [Fact]
    public async Task AcceptedTurnDonateToGuardian_UsesMaterializedSnapshotUpdateGuardiansForPreTurnReputation()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: DONATE_TO_GUARDIAN] 30 Чернильных Перьев"
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
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
                "appearanceDescription": "Current guardian after donation should still fail if snapshot command-only reputation already raised the baseline."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 40, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_snapshot_update_reputation_authority.json",
            NormalizeGuardianStateJson("""
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
                    "appearanceDescription": "Snapshot guardian before command-only reputation provenance."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 20, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ],
              "UpdateGuardians": [
                {
                  "command": "updateReputation",
                  "guardianId": "guardian_alpha",
                  "reputationChange": 10,
                  "reason": "Snapshot-only reputation provenance must be materialized into pre-turn authority."
                }
              ]
            }
        """));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_snapshot_update_reputation_authority.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "DONATE_TO_GUARDIAN",
          "resolutionType": "guardianReputation",
          "summary": "Snapshot UpdateGuardians reputation provenance must affect accepted-turn donation baseline.",
          "resolved": true,
          "costInFeathers": 30,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "reputationChange": 15,
            "affectedFiles": [
              "game_state/meta/guardians.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_action_invalid_validated_snapshot_data", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_reputation_delta_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnDonateToGuardian_SnapshotCreateGuardianProvidesBaselineTarget()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: DONATE_TO_GUARDIAN] 30 Чернильных Перьев"
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
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
                "appearanceDescription": "Current guardian after donation must still resolve a snapshot-created baseline guardian."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 25, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        var createdGuardianRoot = JsonNode.Parse(NormalizeGuardianStateJson("""
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
                "appearanceDescription": "Guardian exists only through validated snapshot create command."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 18, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """))!.AsObject();
        var snapshotCreateRoot = new JsonObject
        {
            ["guardians"] = new JsonArray(),
            ["UpdateGuardians"] = new JsonArray
            {
                new JsonObject
                {
                    ["command"] = "create",
                    ["data"] = createdGuardianRoot["guardians"]![0]!.DeepClone()
                }
            }
        };
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_snapshot_create_target.json",
            snapshotCreateRoot.ToJsonString());

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_snapshot_create_target.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var currentManifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        currentManifest["sessionId"] = "live-session";
        currentManifest["requestId"] = "live-request";
        currentManifest["manifestPayloadHash"] = ComputeManifestPayloadHash(currentManifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            currentManifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "DONATE_TO_GUARDIAN",
          "resolutionType": "guardianReputation",
          "summary": "Snapshot create commands must materialize into guardian baseline lookup for donation.",
          "resolved": true,
          "costInFeathers": 30,
          "stateEvidence": {
            "guardianId": "guardian_alpha",
            "reputationChange": 15,
            "affectedFiles": [
              "game_state/meta/guardians.json"
            ]
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_action_invalid_validated_snapshot_data", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_reputation_delta_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AbodeOfferingResolution_UsesMaterializedSnapshotGuardianPowerEventsForPreTurnPower()
    {
        const string currentGuardiansJson = """
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
                "appearanceDescription": "Current guardian after pending offering should still fail if snapshot raw offering already raised pre-turn power."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson(currentGuardiansJson));
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """);
        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "pending_offering_snapshot_power_current",
              "eventId": "pending_offering_snapshot_power_current",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "pending_offering_snapshot_power_current",
              "title": "Current offering journal proof still exists",
              "summary": "Only the pre-turn power baseline should be stricter because snapshot raw offering provenance already raised it.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """);

        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WriteRawAsync(GuardianAbodeOfferingState.PendingRequestPath, """
        {
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "offeringType": "ink_feathers",
          "inkFeathersOffered": 100,
          "returnCycleId": "cycle_12",
          "createdAtUtc": "2026-03-28T00:00:00Z"
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_snapshot_power_authority.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "ink_feathers",
              "inkFeathersOffered": 100,
              "returnCycleId": "cycle_12",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_snapshot_power_authority.json",
            NormalizeGuardianStateJson("""
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
                    "appearanceDescription": "Pre-turn guardian before pending offering snapshot raw power-event provenance."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ],
              "guardianPowerEvents": [
                {
                  "eventId": "snapshot_offering_power_authority_evt",
                  "guardianId": "guardian_alpha",
                  "delta": 2,
                  "reasonType": "offering",
                  "sourceSurface": "guardianAbodeOffering",
                  "sourceId": "snapshot_offering_power_authority_evt",
                  "title": "Snapshot raw offering provenance must materialize into pending pre-turn power",
                  "summary": "Pending offering power proof should use snapshot raw offering events when they are the only baseline provenance.",
                  "visibility": "player_known",
                  "appliedAt": "2026-03-27T00:00:00Z",
                  "audit": {
                    "offeringType": "ink_feathers",
                    "returnCycleId": "cycle_12",
                    "baseDelta": 2,
                    "finalDelta": 2,
                    "inkFeathersOffered": 100,
                    "capRemainingBefore": 150
                  }
                }
              ]
            }
            """));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_snapshot_power_authority.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_snapshot_power_authority.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_snapshot_power_authority.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.PowerJournalOfferings);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_expected_power_gain_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AbodeOfferingResolution_UsesSameGuardianSnapshotPowerEventsOutsideRequestMatchForPreTurnPower()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
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
                "appearanceDescription": "Current guardian power should still fail when snapshot already contains a same-guardian offering outside the current request scope."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """);
        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "pending_offering_same_guardian_scope_current",
              "eventId": "pending_offering_same_guardian_scope_current",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "pending_offering_same_guardian_scope_current",
              "title": "Current offering journal proof still exists",
              "summary": "Guardian-scoped snapshot power provenance should still affect the pending offering baseline.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """);

        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WriteRawAsync(GuardianAbodeOfferingState.PendingRequestPath, """
        {
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "offeringType": "ink_feathers",
          "inkFeathersOffered": 100,
          "returnCycleId": "cycle_12",
          "createdAtUtc": "2026-03-28T00:00:00Z"
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_same_guardian_scope.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "ink_feathers",
              "inkFeathersOffered": 100,
              "returnCycleId": "cycle_12",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_snapshot_same_guardian_scope.json",
            NormalizeGuardianStateJson("""
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
                    "appearanceDescription": "Guardian-scoped snapshot power provenance must materialize even when the request matcher would treat it as unrelated."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ],
              "guardianPowerEvents": [
                {
                  "eventId": "snapshot_same_guardian_unrelated_offering_evt",
                  "guardianId": "guardian_alpha",
                  "delta": 1,
                  "reasonType": "offering",
                  "sourceSurface": "guardianAbodeOffering",
                  "sourceId": "snapshot_same_guardian_unrelated_offering_evt",
                  "title": "Same-guardian historical offering still changes baseline power",
                  "summary": "Request-aware proof matching may ignore this event, but pre-turn power materialization must still include it for the same guardian.",
                  "visibility": "player_known",
                  "appliedAt": "2026-03-27T00:00:00Z",
                  "audit": {
                    "offeringType": "ink_feathers",
                    "returnCycleId": "cycle_previous",
                    "baseDelta": 1,
                    "finalDelta": 1,
                    "inkFeathersOffered": 50,
                    "capRemainingBefore": 150
                  }
                }
              ]
            }
            """));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_same_guardian_scope.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_same_guardian_scope.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_same_guardian_scope.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.PowerJournalOfferings);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_expected_power_gain_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AbodeOfferingResolution_UsesSnapshotTrackerCompletionForPreTurnPower()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
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
                "appearanceDescription": "Current guardian power should still fail when snapshot tracker completion already raised the pre-turn baseline."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """);
        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "pending_offering_tracker_power_current",
              "eventId": "pending_offering_tracker_power_current",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 12,
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "pending_offering_tracker_power_current",
              "title": "Current offering journal proof still exists",
              "summary": "Snapshot tracker completion should still affect the pending offering power baseline.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_12",
                "baseDelta": 2,
                "finalDelta": 2,
                "inkFeathersOffered": 100,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """);

        await WriteRawAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);

        await WriteRawAsync(GuardianAbodeOfferingState.PendingRequestPath, """
        {
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "offeringType": "ink_feathers",
          "inkFeathersOffered": 100,
          "returnCycleId": "cycle_12",
          "createdAtUtc": "2026-03-28T00:00:00Z"
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "test_backups/preturn_pending_abode_offering_tracker_power.json",
            """
            {
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "offeringType": "ink_feathers",
              "inkFeathersOffered": 100,
              "returnCycleId": "cycle_12",
              "createdAtUtc": "2026-03-28T00:00:00Z"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_snapshot_tracker_power.json",
            NormalizeGuardianStateJson("""
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
                    "appearanceDescription": "Tracker completion should be the only snapshot provenance for pre-turn power."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_tracker_power.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_tracker_power.json",
            """
            {
              "activeProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_tracker_power_only",
                    "projectType": "abode_expansion",
                    "projectTier": "minor",
                    "projectMode": "internal",
                    "projectName": "Малое расширение Обители",
                    "activeState": "Sealing a new chamber",
                    "totalWork": 12,
                    "workDone": 12,
                    "totalStages": 2,
                    "currentStage": 2,
                    "pressure": 3,
                    "stability": 82
                  }
                }
              ],
              "completedProjects": [],
              "temporaryProjectModifiers": [],
              "completeGuardianProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "projectId": "proj_tracker_power_only",
                  "finalState": "Completed",
                  "outcome": "Snapshot tracker completion should raise pre-turn power before the pending offering is applied.",
                  "abodePowerDelta": 1
                }
              ]
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_tracker_power.json",
            """
            {
              "currentRealm": "Chaos Sea",
              "currentIncarnation": 1
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.PowerJournalOfferings);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_offering_expected_power_gain_missing", StringComparison.OrdinalIgnoreCase));
    }

}

