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
{    [Fact]
    public async Task GuardianProjectValidation_OffensiveIntrigueAgainstTrustedTarget_RequiresBetrayalReason()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 52, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guardian_alpha", "attitudeScore": 55, "attitudeTier": "ally", "reason": "Mutual accord", "lastChangedAt": null }
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
            "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [
              { "targetGuardianId": "guardian_beta", "attitudeScore": 80, "attitudeTier": "trusted", "reason": "Ancient pact", "lastChangedAt": null }
            ],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "startGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_betrayal_test",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Разрыв древнего пакта",
                "targetGuardianId": "guardian_beta",
                "activeState": "Preparing the strike",
                "totalWork": 18,
                "workDone": 0,
                "totalStages": 3,
                "currentStage": 0,
                "pressure": 0,
                "stability": 100
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "guardian_project_missing_betrayal_reason", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProjectValidation_CompleteOffensiveIntrigueAgainstTrustedTarget_RequiresBetrayalReasonWhenActiveProjectLacksIt()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 52, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guardian_alpha", "attitudeScore": 55, "attitudeTier": "ally", "reason": "Mutual accord", "lastChangedAt": null }
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
            "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [
              { "targetGuardianId": "guardian_beta", "attitudeScore": 80, "attitudeTier": "trusted", "reason": "Ancient pact", "lastChangedAt": null }
            ],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_betrayal_completion",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Разрыв древнего пакта",
                "targetGuardianId": "guardian_beta",
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
              "projectId": "proj_betrayal_completion",
              "finalState": "Completed",
              "outcome": "Удар доведен до конца."
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "guardian_project_missing_betrayal_reason", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProjectValidation_CompleteOffensiveIntrigueAgainstTrustedTarget_AllowsStoredBetrayalReason()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 52, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guardian_alpha", "attitudeScore": 55, "attitudeTier": "ally", "reason": "Mutual accord", "lastChangedAt": null }
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
            "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [
              { "targetGuardianId": "guardian_beta", "attitudeScore": 80, "attitudeTier": "trusted", "reason": "Ancient pact", "lastChangedAt": null }
            ],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        const string preTurnTrackerJson = """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_betrayal_completion",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Разрыв древнего пакта",
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
          ]
        }
        """;

        await WritePreTurnTrackedFileAsync(GuardianProjectState.TrackerPath, "test_backups/preturn_guardian_projects_betrayal.json", preTurnTrackerJson);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_betrayal_completion",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Разрыв древнего пакта",
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
              "projectId": "proj_betrayal_completion",
              "finalState": "Completed",
              "outcome": "Удар доведен до конца.",
              "offensiveImpactAudit": {
                "targetLoss": 3
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "guardian_project_missing_betrayal_reason", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "guardian_project_completion_offensive_audit_missing_target", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProjectValidation_CompletePoliticalProject_CannotRetargetStoredGuardianTarget()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guardian_beta", "attitudeScore": 5, "attitudeTier": "neutral", "reason": "Measured distance", "lastChangedAt": null },
                { "targetGuardianId": "guardian_gamma", "attitudeScore": 80, "attitudeTier": "trusted", "reason": "Sacred accord", "lastChangedAt": null }
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
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 52, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guardian_alpha", "attitudeScore": 0, "attitudeTier": "neutral", "reason": "Measured distance", "lastChangedAt": null }
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
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 60, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guardian_alpha", "attitudeScore": 60, "attitudeTier": "ally", "reason": "Sacred accord", "lastChangedAt": null }
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
            "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [
              { "targetGuardianId": "guardian_beta", "attitudeScore": 5, "attitudeTier": "neutral", "reason": "Measured distance", "lastChangedAt": null },
              { "targetGuardianId": "guardian_gamma", "attitudeScore": 80, "attitudeTier": "trusted", "reason": "Sacred accord", "lastChangedAt": null }
            ],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        const string preTurnTrackerJson = """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_retarget_test",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Смена цели на финише",
                "targetGuardianId": "guardian_beta",
                "activeState": "Closing the strike",
                "totalWork": 18,
                "workDone": 18,
                "totalStages": 3,
                "currentStage": 3,
                "pressure": 8,
                "stability": 72
              }
            }
          ]
        }
        """;

        await WritePreTurnTrackedFileAsync(GuardianProjectState.TrackerPath, "test_backups/preturn_guardian_projects_retarget.json", preTurnTrackerJson);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_retarget_test",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Смена цели на финише",
                "targetGuardianId": "guardian_beta",
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
              "projectId": "proj_retarget_test",
              "finalState": "Completed",
              "outcome": "Удар доведен до конца.",
              "targetGuardianId": "guardian_gamma",
              "offensiveImpactAudit": {
                "targetLoss": 3
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "guardian_project_completion_target_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "guardian_project_neutral_target_low_motivation", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "guardian_project_missing_betrayal_reason", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProjectValidation_SameTurnStartAndComplete_UsesStartedProjectPoliticalFallbacks()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 52, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guardian_alpha", "attitudeScore": 55, "attitudeTier": "ally", "reason": "Mutual accord", "lastChangedAt": null }
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
            "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [
              { "targetGuardianId": "guardian_beta", "attitudeScore": 80, "attitudeTier": "trusted", "reason": "Ancient pact", "lastChangedAt": null }
            ],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "startGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_same_turn_completion",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Разрыв пакта в один ход",
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
              "projectId": "proj_same_turn_completion",
              "finalState": "Completed",
              "outcome": "Удар доведен до конца.",
              "offensiveImpactAudit": {
                "targetLoss": 3
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "guardian_project_missing_betrayal_reason", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "guardian_project_completion_offensive_audit_missing_target", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProjectValidation_DuplicateGuardianStart_DoesNotProvideSameTurnCompletionFallback()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 52, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guardian_alpha", "attitudeScore": 55, "attitudeTier": "ally", "reason": "Mutual accord", "lastChangedAt": null }
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
            "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [
              { "targetGuardianId": "guardian_beta", "attitudeScore": 80, "attitudeTier": "trusted", "reason": "Ancient pact", "lastChangedAt": null }
            ],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "startGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_first_start",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Первый недопустимый offensive start",
                "targetGuardianId": "guardian_beta",
                "betrayalReason": "The pact is broken after a deliberate transgression.",
                "activeState": "Opening the first forbidden strike",
                "totalWork": 18,
                "workDone": 18,
                "totalStages": 3,
                "currentStage": 3,
                "pressure": 8,
                "stability": 72
              }
            },
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_illegal_same_turn",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Недопустимый второй старт",
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
              "projectId": "proj_first_start",
              "finalState": "Completed",
              "outcome": "Попытка завершить первый проект из duplicate start-set.",
              "offensiveImpactAudit": {
                "targetLoss": 2
              }
            },
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_illegal_same_turn",
              "finalState": "Completed",
              "outcome": "Попытка завершить недопустимый второй старт.",
              "offensiveImpactAudit": {
                "targetLoss": 3
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Equal(2, issues.Count(issue => string.Equals(issue.Code, "guardian_project_start_duplicate_guardian", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(2, issues.Count(issue => string.Equals(issue.Code, "guardian_project_completion_unknown_project_id", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(2, issues.Count(issue => string.Equals(issue.Code, "guardian_project_completion_offensive_audit_missing_target", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task GuardianProjectValidation_DuplicateSameTurnProjectKey_DoesNotShadowPreTurnCompletionMetadata()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 52, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guardian_alpha", "attitudeScore": 55, "attitudeTier": "ally", "reason": "Mutual accord", "lastChangedAt": null }
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
            "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [
              { "targetGuardianId": "guardian_beta", "attitudeScore": 80, "attitudeTier": "trusted", "reason": "Ancient pact", "lastChangedAt": null }
            ],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        const string preTurnTrackerJson = """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_duplicate_key",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Канонический удар",
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
          ]
        }
        """;

        await WritePreTurnTrackedFileAsync(GuardianProjectState.TrackerPath, "test_backups/preturn_guardian_projects_duplicate_key.json", preTurnTrackerJson);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_duplicate_key",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Канонический удар",
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
          "startGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_duplicate_key",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Попытка затенить канонический проект",
                "targetGuardianId": "guardian_beta",
                "betrayalReason": "The pact is broken after a deliberate transgression.",
                "activeState": "Improvised shadow start",
                "totalWork": 10,
                "workDone": 0,
                "totalStages": 2,
                "currentStage": 0,
                "pressure": 0,
                "stability": 100
              }
            }
          ],
          "completeGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_duplicate_key",
              "finalState": "Completed",
              "outcome": "Канонический удар завершен.",
              "offensiveImpactAudit": {
                "targetLoss": 3
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "guardian_project_start_duplicate_existing_project_id", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "guardian_project_missing_betrayal_reason", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "guardian_project_completion_offensive_audit_missing_target", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProjectValidation_OffensiveIntrigueAgainstNeutralTarget_WarnsAboutWeakMotivation()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guardian_beta", "attitudeScore": 5, "attitudeTier": "neutral", "reason": "Distant respect", "lastChangedAt": null }
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
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 52, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guardian_alpha", "attitudeScore": 5, "attitudeTier": "neutral", "reason": "Distant respect", "lastChangedAt": null }
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
            "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [
              { "targetGuardianId": "guardian_beta", "attitudeScore": 5, "attitudeTier": "neutral", "reason": "Distant respect", "lastChangedAt": null }
            ],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "startGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_neutral_pressure",
                "projectType": "offensive_intrigue",
                "projectTier": "minor",
                "projectMode": "offensive",
                "projectName": "Проверка нейтрального давления",
                "targetGuardianId": "guardian_beta",
                "activeState": "Probing the boundary",
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

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "guardian_project_neutral_target_low_motivation", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Warning && string.Equals(issue.Code, "guardian_project_neutral_target_low_motivation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProjectValidation_CompletedOffensiveIntrigue_InconsistentPoliticalAuditMetadata_FailsValidation()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 52, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guardian_alpha", "attitudeScore": -60, "attitudeTier": "rival", "reason": "Hostility", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_inconsistent_offensive_audit",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Противоречивый аудит удара",
                "targetGuardianId": "guardian_beta",
                "finalState": "Completed",
                "completionTurn": 12,
                "offensiveImpactAudit": {
                  "attackerCurrentPower": 48,
                  "targetCurrentPower": 52,
                  "baseLoss": 3,
                  "attackerBonus": 1,
                  "baseTargetShield": 1,
                  "fortificationBonus": 0,
                  "counterOperationBonus": 0,
                  "playerDefenseBonus": 0,
                  "targetShield": 1,
                  "targetLoss": 3,
                  "pressureDelta": 3,
                  "stabilityDamage": 2,
                  "targetAttitudeScore": -80,
                  "targetAttitudeTier": "neutral",
                  "hostilityWeight": 0,
                  "preferredHostileTarget": false
                }
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "guardian_project_offensive_target_attitude_tier_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "guardian_project_offensive_hostility_weight_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "guardian_project_offensive_preferred_hostile_target_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProjectValidation_CompleteGuardianProjects_InconsistentPoliticalAuditMetadata_FailsValidation()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 52, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guardian_alpha", "attitudeScore": -60, "attitudeTier": "rival", "reason": "Hostility", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        const string preTurnTrackerJson = """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_command_surface_inconsistent_audit",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Командный аудит с противоречиями",
                "targetGuardianId": "guardian_beta",
                "activeState": "Closing the strike",
                "totalWork": 18,
                "workDone": 18,
                "totalStages": 3,
                "currentStage": 3,
                "pressure": 8,
                "stability": 72
              }
            }
          ]
        }
        """;

        await WritePreTurnTrackedFileAsync(GuardianProjectState.TrackerPath, "test_backups/preturn_guardian_projects_inconsistent_command_audit.json", preTurnTrackerJson);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_command_surface_inconsistent_audit",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Командный аудит с противоречиями",
                "targetGuardianId": "guardian_beta",
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
              "projectId": "proj_command_surface_inconsistent_audit",
              "finalState": "Completed",
              "outcome": "Интрига завершена.",
              "offensiveImpactAudit": {
                "targetLoss": 3,
                "targetAttitudeScore": -80,
                "targetAttitudeTier": "neutral",
                "hostilityWeight": 0,
                "preferredHostileTarget": false
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "guardian_project_offensive_target_attitude_tier_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "guardian_project_offensive_hostility_weight_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "guardian_project_offensive_preferred_hostile_target_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProjectValidation_CompleteGuardianProjects_NonOffensiveAuditPayload_FailsValidation()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guardian_beta", "attitudeScore": -20, "attitudeTier": "competitive", "reason": "Competition", "lastChangedAt": null }
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
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 52, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guardian_alpha", "attitudeScore": -20, "attitudeTier": "competitive", "reason": "Competition", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        const string preTurnTrackerJson = """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_non_offensive_audit",
                "projectType": "abode_expansion",
                "projectTier": "major",
                "projectMode": "internal",
                "projectName": "Расширение без интриги",
                "activeState": "Sealing a new chamber",
                "totalWork": 18,
                "workDone": 18,
                "totalStages": 3,
                "currentStage": 3,
                "pressure": 4,
                "stability": 78
              }
            }
          ]
        }
        """;

        await WritePreTurnTrackedFileAsync(GuardianProjectState.TrackerPath, "test_backups/preturn_guardian_projects_non_offensive_audit.json", preTurnTrackerJson);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_non_offensive_audit",
                "projectType": "abode_expansion",
                "projectTier": "major",
                "projectMode": "internal",
                "projectName": "Расширение без интриги",
                "activeState": "Sealing a new chamber",
                "totalWork": 18,
                "workDone": 18,
                "totalStages": 3,
                "currentStage": 3,
                "pressure": 4,
                "stability": 78
              }
            }
          ],
          "completeGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_non_offensive_audit",
              "finalState": "Completed",
              "outcome": "Обычное строительство завершено.",
              "targetGuardianId": "guardian_beta",
              "offensiveImpactAudit": {
                "targetLoss": 3
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "guardian_project_completion_unexpected_offensive_audit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProjectValidation_CompleteGuardianProjects_AbandonedOffensiveAuditPayload_FailsValidation()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 52, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guardian_alpha", "attitudeScore": -60, "attitudeTier": "rival", "reason": "Hostility", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        const string preTurnTrackerJson = """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_abandoned_offensive_audit",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Брошенная интрига",
                "targetGuardianId": "guardian_beta",
                "activeState": "Breaking off the strike",
                "totalWork": 18,
                "workDone": 14,
                "totalStages": 3,
                "currentStage": 2,
                "pressure": 8,
                "stability": 72
              }
            }
          ]
        }
        """;

        await WritePreTurnTrackedFileAsync(GuardianProjectState.TrackerPath, "test_backups/preturn_guardian_projects_abandoned_offensive_audit.json", preTurnTrackerJson);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_abandoned_offensive_audit",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Брошенная интрига",
                "targetGuardianId": "guardian_beta",
                "activeState": "Breaking off the strike",
                "totalWork": 18,
                "workDone": 14,
                "totalStages": 3,
                "currentStage": 2,
                "pressure": 8,
                "stability": 72
              }
            }
          ],
          "completeGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_abandoned_offensive_audit",
              "finalState": "Abandoned",
              "outcome": "Интрига сорвана.",
              "offensiveImpactAudit": {
                "targetLoss": 2
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "guardian_project_completion_unexpected_offensive_audit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProjectValidation_CompleteGuardianProjects_TypeInvalidPoliticalAuditMetadata_FailsValidation()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 52, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guardian_alpha", "attitudeScore": -60, "attitudeTier": "rival", "reason": "Hostility", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        const string preTurnTrackerJson = """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_command_surface_type_invalid_audit",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Командный аудит с неверными типами",
                "targetGuardianId": "guardian_beta",
                "activeState": "Closing the strike",
                "totalWork": 18,
                "workDone": 18,
                "totalStages": 3,
                "currentStage": 3,
                "pressure": 8,
                "stability": 72
              }
            }
          ]
        }
        """;

        await WritePreTurnTrackedFileAsync(GuardianProjectState.TrackerPath, "test_backups/preturn_guardian_projects_type_invalid_command_audit.json", preTurnTrackerJson);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_command_surface_type_invalid_audit",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Командный аудит с неверными типами",
                "targetGuardianId": "guardian_beta",
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
              "projectId": "proj_command_surface_type_invalid_audit",
              "finalState": "Completed",
              "outcome": "Интрига завершена.",
              "offensiveImpactAudit": {
                "targetLoss": 3,
                "playerDefenseBonus": "shield",
                "targetAttitudeScore": "bad",
                "targetAttitudeTier": 5,
                "hostilityWeight": "wrong",
                "preferredHostileTarget": "yes"
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            issue.FilePath.EndsWith(".completeGuardianProjects[0].offensiveImpactAudit.targetAttitudeScore", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Message, "Поле должно быть целым числом", StringComparison.Ordinal));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "missing_required_string", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".completeGuardianProjects[0].offensiveImpactAudit.targetAttitudeTier", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            issue.FilePath.EndsWith(".completeGuardianProjects[0].offensiveImpactAudit.hostilityWeight", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Message, "Поле должно быть целым числом", StringComparison.Ordinal));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "invalid_boolean_field", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".completeGuardianProjects[0].offensiveImpactAudit.preferredHostileTarget", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            issue.FilePath.EndsWith(".completeGuardianProjects[0].offensiveImpactAudit.playerDefenseBonus", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Message, "Поле должно быть целым числом", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GuardianProjectValidation_StartGuardianProjects_UnknownGuardian_DoesNotEnterFallback()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "UpdateGuardians": [
            {
              "command": "updateReputation",
              "guardianId": "guardian_alpha",
              "reputationChange": 10,
              "reason": "Validated snapshot favor outcome"
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "startGuardianProjects": [
            {
              "guardianId": "guardian_missing",
              "project": {
                "projectId": "proj_missing_guardian",
                "projectType": "abode_fortification",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Фантомный проект",
                "activeState": "Trying to start",
                "totalWork": 8,
                "workDone": 0,
                "totalStages": 2,
                "currentStage": 0,
                "pressure": 0,
                "stability": 100
              }
            }
          ],
          "completeGuardianProjects": [
            {
              "guardianId": "guardian_missing",
              "projectId": "proj_missing_guardian",
              "finalState": "Completed",
              "outcome": "Фантомный проект якобы завершён."
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_unknown_guardian_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".startGuardianProjects[0].guardianId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_completion_unknown_project_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".completeGuardianProjects[0].projectId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProjectValidation_StartGuardianProjects_UnknownPoliticalTarget_FailsValidation()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "startGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_unknown_target",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Удар в пустоту",
                "targetGuardianId": "guardian_missing",
                "activeState": "Targeting the void",
                "totalWork": 18,
                "workDone": 2,
                "totalStages": 3,
                "currentStage": 0,
                "pressure": 4,
                "stability": 70
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_unknown_target_guardian_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".startGuardianProjects[0].project.targetGuardianId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProjectValidation_CompleteGuardianProjects_FallbackUnknownPoliticalTarget_FailsValidation()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        const string preTurnTrackerJson = """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_unknown_completion_target",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Фантомная цель",
                "targetGuardianId": "guardian_missing",
                "activeState": "Closing the strike",
                "totalWork": 18,
                "workDone": 18,
                "totalStages": 3,
                "currentStage": 3,
                "pressure": 8,
                "stability": 72
              }
            }
          ]
        }
        """;

        await WritePreTurnTrackedFileAsync(GuardianProjectState.TrackerPath, "test_backups/preturn_guardian_projects_unknown_completion_target.json", preTurnTrackerJson);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_unknown_completion_target",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Фантомная цель",
                "targetGuardianId": "guardian_missing",
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
              "projectId": "proj_unknown_completion_target",
              "finalState": "Completed",
              "outcome": "Интрига завершена."
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": []
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_unknown_target_guardian_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".activeProjects[0].project.targetGuardianId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_unknown_target_guardian_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".completeGuardianProjects[0].targetGuardianId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProjectValidation_StartGuardianProjects_CannotReplaceExistingActiveProject_AndDoesNotEnterFallback()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        const string preTurnTrackerJson = """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_existing",
                "projectType": "abode_expansion",
                "projectTier": "major",
                "projectMode": "internal",
                "projectName": "Текущий активный проект",
                "activeState": "Still under way",
                "totalWork": 18,
                "workDone": 9,
                "totalStages": 3,
                "currentStage": 1,
                "pressure": 4,
                "stability": 78
              }
            }
          ]
        }
        """;

        await WritePreTurnTrackedFileAsync(GuardianProjectState.TrackerPath, "test_backups/preturn_guardian_projects_existing_active.json", preTurnTrackerJson);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "startGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_replacement_attempt",
                "projectType": "abode_fortification",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Попытка подмены",
                "activeState": "Trying to replace",
                "totalWork": 8,
                "workDone": 0,
                "totalStages": 2,
                "currentStage": 0,
                "pressure": 0,
                "stability": 100
              }
            }
          ],
          "completeGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_replacement_attempt",
              "finalState": "Completed",
              "outcome": "Несуществующий replacement якобы завершён."
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_start_guardian_already_has_active_project", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".startGuardianProjects[0].guardianId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_completion_unknown_project_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".completeGuardianProjects[0].projectId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProjectValidation_StartGuardianProjects_UsesCurrentTrackerWhenPreTurnSnapshotMissing()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_existing_current_only",
                "projectType": "abode_expansion",
                "projectTier": "major",
                "projectMode": "internal",
                "projectName": "Только текущий tracker",
                "activeState": "Still under way",
                "totalWork": 18,
                "workDone": 9,
                "totalStages": 3,
                "currentStage": 1,
                "pressure": 4,
                "stability": 78
              }
            }
          ],
          "startGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_replacement_without_snapshot",
                "projectType": "abode_fortification",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Попытка замены без snapshot",
                "activeState": "Trying to replace",
                "totalWork": 8,
                "workDone": 0,
                "totalStages": 2,
                "currentStage": 0,
                "pressure": 0,
                "stability": 100
              }
            }
          ],
          "completeGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_replacement_without_snapshot",
              "finalState": "Completed",
              "outcome": "Несуществующий replacement якобы завершён."
            }
          ]
        }
        """);

        await RemoveTrackedFileFromCurrentPendingTurnSnapshotAsync(GuardianProjectState.TrackerPath);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_missing_validated_preturn_tracker_snapshot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProjectValidation_CurrentTrackerProjectKnowledge_ResolvesUpdateAndCompletionWithoutPreTurnSnapshot()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 52, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guardian_alpha", "attitudeScore": -60, "attitudeTier": "rival", "reason": "Hostility", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_current_only_completion",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Интрига только в current tracker",
                "targetGuardianId": "guardian_beta",
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
          "guardianProjectUpdates": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_current_only_completion",
              "workDone": 18,
              "currentStage": 3
            }
          ],
          "completeGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_current_only_completion",
              "finalState": "Completed",
              "outcome": "Интрига завершена по current tracker metadata.",
              "offensiveImpactAudit": {
                "targetLoss": 3
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_update_unknown_project_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_completion_unknown_project_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProjectValidation_SameKeyCurrentTrackerStart_DoesNotShadowCanonicalCompletionMetadata()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 52, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guardian_alpha", "attitudeScore": -60, "attitudeTier": "rival", "reason": "Hostility", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_same_key_current",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Каноническая интрига",
                "targetGuardianId": "guardian_beta",
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
          "startGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_same_key_current",
                "projectType": "abode_expansion",
                "projectTier": "major",
                "projectMode": "internal",
                "projectName": "Конфликтный старт тем же key",
                "activeState": "Trying to shadow",
                "totalWork": 18,
                "workDone": 0,
                "totalStages": 3,
                "currentStage": 0,
                "pressure": 0,
                "stability": 100
              }
            }
          ],
          "completeGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_same_key_current",
              "finalState": "Completed",
              "outcome": "Интрига завершена по canonical current metadata.",
              "offensiveImpactAudit": {
                "targetLoss": 2
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_same_key_current_validation.json",
            """
            {
              "activeProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_same_key_current",
                    "projectType": "offensive_intrigue",
                    "projectTier": "major",
                    "projectMode": "offensive",
                    "projectName": "Каноническая интрига",
                    "targetGuardianId": "guardian_beta",
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
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_start_duplicate_existing_project_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".startGuardianProjects[0].project.projectId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProjectValidation_InvalidPoliticalStart_DoesNotEnterSameTurnFallback()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "startGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_self_targeted",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Удар по себе",
                "targetGuardianId": "guardian_alpha",
                "activeState": "Impossible loop",
                "totalWork": 16,
                "workDone": 0,
                "totalStages": 3,
                "currentStage": 0,
                "pressure": 5,
                "stability": 72
              }
            }
          ],
          "completeGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_self_targeted",
              "finalState": "Completed",
              "outcome": "Недопустимая интрига якобы завершена.",
              "offensiveImpactAudit": {
                "targetLoss": 1
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_self_target_guardian", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".startGuardianProjects[0].project.targetGuardianId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_completion_unknown_project_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".completeGuardianProjects[0].projectId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerJournalValidation_RejectsInvalidJournalSemantics()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await MirrorCurrentGuardiansToPreTurnSnapshotAsync("test_backups/preturn_guardians_invalid_journal_semantics.json");
        await EnsureReadableCurrentGuardianProjectTrackerAsync();

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "journal_invalid_reason",
              "eventId": "evt_invalid_reason",
              "turn": 12,
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "delta": -2,
              "reasonType": "void_signal",
              "sourceSurface": "guardianProjectUpdates",
              "sourceId": "proj_alpha",
              "title": "Неверный тип",
              "summary": "reasonType не из canonical списка.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {}
            },
            {
              "entryId": "journal_invalid_visibility",
              "eventId": "evt_invalid_visibility",
              "turn": 12,
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "delta": -2,
              "reasonType": "rival_strike",
              "sourceSurface": "guardianProjectUpdates",
              "sourceId": "proj_alpha",
              "title": "Неверная видимость",
              "summary": "visibility не из canonical списка.",
              "visibility": "secret_public",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {}
            },
            {
              "entryId": "journal_invalid_applied_at",
              "eventId": "evt_invalid_applied_at",
              "turn": 12,
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "delta": -2,
              "reasonType": "rival_strike",
              "sourceSurface": "guardianProjectUpdates",
              "sourceId": "proj_alpha",
              "title": "Неверная дата",
              "summary": "appliedAt не ISO 8601.",
              "visibility": "player_known",
              "appliedAt": "not-a-timestamp",
              "audit": {}
            },
            {
              "entryId": "journal_missing_audit",
              "eventId": "evt_missing_audit",
              "turn": 12,
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "delta": -2,
              "reasonType": "rival_strike",
              "sourceSurface": "guardianProjectUpdates",
              "sourceId": "proj_alpha",
              "title": "Нет audit",
              "summary": "journal entry без machine-readable audit.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z"
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_invalid_reason_type", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".entries[0].reasonType", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_invalid_visibility", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".entries[1].visibility", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_invalid_applied_at", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".entries[2].appliedAt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerEventValidation_PoliticalStrikeAuditMissingProjectIdentity_FailsValidation()
    {
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            },
            {
              "guardianId": "guardian_gamma",
              "canonicalName": "Нерин",
              "nameVariants": { "default": "Нерин", "feminine": null, "masculine": "Нерин", "neutral": null },
              "manifestation": {
                "currentDisplayName": "Нерин",
                "formFlexibility": "fixed",
                "currentPresentationStyle": "masculine",
                "currentPronouns": "он/его",
                "appearanceDescription": "Тестовая форма."
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
              "eventId": "evt_political_strike_missing_identity",
              "guardianId": "guardian_alpha",
              "delta": -3,
              "reasonType": "rival_strike",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_alpha",
              "title": "Политический удар без identity",
              "summary": "offensive power event не содержит canonical project identity.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "attackerCurrentPower": 60,
                "targetCurrentPower": 48,
                "baseLoss": 4,
                "attackerBonus": 1,
                "baseTargetShield": 0,
                "fortificationBonus": 0,
                "counterOperationBonus": 0,
                "playerDefenseBonus": 0,
                "targetShield": 0,
                "targetLoss": 3,
                "pressureDelta": 2,
                "stabilityDamage": 1
              }
            }
          ]
        }
        """));
        await MirrorCurrentGuardiansToPreTurnSnapshotAsync("test_backups/preturn_guardians_political_strike_missing_identity.json");
        await EnsureReadableCurrentGuardianProjectTrackerAsync();

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => issue.FilePath.EndsWith(".guardianPowerEvents[0].audit.projectGuardianId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => issue.FilePath.EndsWith(".guardianPowerEvents[0].audit.projectName", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => issue.FilePath.EndsWith(".guardianPowerEvents[0].audit.projectType", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => issue.FilePath.EndsWith(".guardianPowerEvents[0].audit.projectTier", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => issue.FilePath.EndsWith(".guardianPowerEvents[0].audit.finalState", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerEventValidation_CompletionRivalStrikeRequiresRuntimeTargetLossShapeAndRelatedGuardian()
    {
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 60, "tier": "Могущественная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "evt_positive_delta_strike",
              "guardianId": "guardian_beta",
              "delta": 3,
              "reasonType": "rival_strike",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_strike_shape",
              "title": "Невозможный положительный strike",
              "summary": "completion rival_strike не должен усиливать цель.",
              "visibility": "player_known",
              "relatedGuardianId": "guardian_alpha",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectGuardianId": "guardian_alpha",
                "projectId": "proj_strike_shape",
                "projectName": "Острая интрига",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "finalState": "Completed",
                "attackerCurrentPower": 60,
                "targetCurrentPower": 48,
                "baseLoss": 4,
                "attackerBonus": 1,
                "baseTargetShield": 0,
                "fortificationBonus": 0,
                "counterOperationBonus": 0,
                "playerDefenseBonus": 0,
                "targetShield": 0,
                "targetLoss": 3,
                "pressureDelta": 2,
                "stabilityDamage": 1
              }
            },
            {
              "eventId": "evt_zero_target_loss_strike",
              "guardianId": "guardian_beta",
              "delta": -3,
              "reasonType": "rival_strike",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_strike_shape",
              "title": "Нулевой hostile loss",
              "summary": "completion rival_strike должен существовать только при реальной потере силы.",
              "visibility": "player_known",
              "relatedGuardianId": "guardian_alpha",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectGuardianId": "guardian_alpha",
                "projectId": "proj_strike_shape",
                "projectName": "Острая интрига",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "finalState": "Completed",
                "attackerCurrentPower": 60,
                "targetCurrentPower": 48,
                "baseLoss": 4,
                "attackerBonus": 1,
                "baseTargetShield": 0,
                "fortificationBonus": 0,
                "counterOperationBonus": 0,
                "playerDefenseBonus": 0,
                "targetShield": 0,
                "targetLoss": 0,
                "pressureDelta": 2,
                "stabilityDamage": 1
              }
            },
            {
              "eventId": "evt_missing_related_strike",
              "guardianId": "guardian_beta",
              "delta": -3,
              "reasonType": "rival_strike",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_strike_shape",
              "title": "Strike без source guardian",
              "summary": "target-side strike обязан явно ссылаться на attacking guardian.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectGuardianId": "guardian_alpha",
                "projectId": "proj_strike_shape",
                "projectName": "Острая интрига",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "finalState": "Completed",
                "attackerCurrentPower": 60,
                "targetCurrentPower": 48,
                "baseLoss": 4,
                "attackerBonus": 1,
                "baseTargetShield": 0,
                "fortificationBonus": 0,
                "counterOperationBonus": 0,
                "playerDefenseBonus": 0,
                "targetShield": 0,
                "targetLoss": 3,
                "pressureDelta": 2,
                "stabilityDamage": 1
              }
            },
            {
              "eventId": "evt_delta_target_loss_mismatch",
              "guardianId": "guardian_beta",
              "delta": -4,
              "reasonType": "rival_strike",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_strike_shape",
              "title": "Несогласованный hostile loss",
              "summary": "Applied hostile loss не может превышать targetLoss.",
              "visibility": "player_known",
              "relatedGuardianId": "guardian_alpha",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectGuardianId": "guardian_alpha",
                "projectId": "proj_strike_shape",
                "projectName": "Острая интрига",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "finalState": "Completed",
                "attackerCurrentPower": 60,
                "targetCurrentPower": 48,
                "baseLoss": 4,
                "attackerBonus": 1,
                "baseTargetShield": 0,
                "fortificationBonus": 0,
                "counterOperationBonus": 0,
                "playerDefenseBonus": 0,
                "targetShield": 0,
                "targetLoss": 3,
                "pressureDelta": 2,
                "stabilityDamage": 1
              }
            },
            {
              "eventId": "evt_raw_applied_loss_shape",
              "guardianId": "guardian_beta",
              "delta": -1,
              "reasonType": "rival_strike",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_strike_shape",
              "title": "Raw strike с clamped-подобным delta",
              "summary": "Raw completion strike должен хранить pre-clamp hostile loss, а не уже уменьшенный applied delta.",
              "visibility": "player_known",
              "relatedGuardianId": "guardian_alpha",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectGuardianId": "guardian_alpha",
                "projectId": "proj_strike_shape",
                "projectName": "Острая интрига",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "finalState": "Completed",
                "attackerCurrentPower": 60,
                "targetCurrentPower": 48,
                "baseLoss": 4,
                "attackerBonus": 1,
                "baseTargetShield": 0,
                "fortificationBonus": 0,
                "counterOperationBonus": 0,
                "playerDefenseBonus": 0,
                "targetShield": 0,
                "targetLoss": 3,
                "pressureDelta": 2,
                "stabilityDamage": 1
              }
            }
          ]
        }
        """));
        await MirrorCurrentGuardiansToPreTurnSnapshotAsync("test_backups/preturn_guardians_political_metadata_truth.json");

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_strike_shape",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "hostile",
                "projectName": "Острая интрига",
                "targetGuardianId": "guardian_beta",
                "finalState": "Completed",
                "completionTurn": 12,
                "outcome": "Соперник ослаблен."
              }
            }
          ],
          "temporaryProjectModifiers": []
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_wrong_victim_repair.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_wrong_victim_repair",
                    "projectType": "offensive_intrigue",
                    "projectTier": "major",
                    "projectMode": "offensive",
                    "projectName": "Интрига Азалии",
                    "targetGuardianId": "guardian_beta",
                    "finalState": "Completed",
                    "completionTurn": 12,
                    "outcome": "Цель должна быть только guardian_beta."
                  }
                }
              ],
              "temporaryProjectModifiers": []
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_target_match.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_target_match",
                    "projectType": "offensive_intrigue",
                    "projectTier": "major",
                    "projectMode": "hostile",
                    "projectName": "Острая интрига",
                    "targetGuardianId": "guardian_beta",
                    "finalState": "Completed",
                    "completionTurn": 12,
                    "outcome": "Соперник ослаблен."
                  }
                }
              ],
              "temporaryProjectModifiers": []
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_reused_completed.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_reused_completed",
                    "projectType": "abode_expansion",
                    "projectTier": "major",
                    "projectMode": "internal",
                    "projectName": "Старый завершённый проект",
                    "finalState": "Completed",
                    "completionTurn": 11,
                    "outcome": "Уже завершён."
                  }
                }
              ],
              "temporaryProjectModifiers": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_rival_strike_delta_sign_mismatch", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".guardianPowerEvents[0].delta", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_rival_strike_target_loss_invalid", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".guardianPowerEvents[1].audit.targetLoss", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_rival_strike_missing_related_guardian_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".guardianPowerEvents[2].relatedGuardianId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_rival_strike_delta_target_loss_mismatch", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".guardianPowerEvents[3].audit.targetLoss", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_rival_strike_delta_target_loss_mismatch", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".guardianPowerEvents[4].audit.targetLoss", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerEventValidation_CompletionRivalStrikeRequiresStrictTrackerAuthorityBeforeTargetMatch()
    {
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 60, "tier": "Могущественная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            },
            {
              "guardianId": "guardian_gamma",
              "canonicalName": "Нерин",
              "nameVariants": { "default": "Нерин", "feminine": null, "masculine": "Нерин", "neutral": null },
              "manifestation": {
                "currentDisplayName": "Нерин",
                "formFlexibility": "fixed",
                "currentPresentationStyle": "masculine",
                "currentPronouns": "он/его",
                "appearanceDescription": "Тестовая форма."
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
              "eventId": "evt_wrong_victim_strike",
              "guardianId": "guardian_gamma",
              "delta": -3,
              "reasonType": "rival_strike",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_target_match",
              "title": "Strike по неверной цели",
              "summary": "target-side strike должен совпадать с targetGuardianId исходного offensive_intrigue.",
              "visibility": "player_known",
              "relatedGuardianId": "guardian_alpha",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectGuardianId": "guardian_alpha",
                "projectId": "proj_target_match",
                "projectName": "Острая интрига",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "finalState": "Completed",
                "attackerCurrentPower": 60,
                "targetCurrentPower": 48,
                "baseLoss": 4,
                "attackerBonus": 1,
                "baseTargetShield": 0,
                "fortificationBonus": 0,
                "counterOperationBonus": 0,
                "playerDefenseBonus": 0,
                "targetShield": 0,
                "targetLoss": 3,
                "pressureDelta": 2,
                "stabilityDamage": 1
              }
            }
          ]
        }
        """));
        await MirrorCurrentGuardiansToPreTurnSnapshotAsync("test_backups/preturn_guardians_completion_rival_target_match.json");

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_reused_completed_key.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_reused_completed",
                    "projectType": "abode_expansion",
                    "projectTier": "major",
                    "projectMode": "internal",
                    "projectName": "Старый завершённый проект",
                    "finalState": "Completed",
                    "completionTurn": 11,
                    "outcome": "Уже завершён."
                  }
                }
              ],
              "temporaryProjectModifiers": []
            }
            """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_target_match",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "hostile",
                "projectName": "Острая интрига",
                "targetGuardianId": "guardian_beta",
                "finalState": "Completed",
                "completionTurn": 12,
                "outcome": "Соперник ослаблен."
              }
            }
          ],
          "temporaryProjectModifiers": []
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_target_match_exact.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_target_match",
                    "projectType": "offensive_intrigue",
                    "projectTier": "major",
                    "projectMode": "hostile",
                    "projectName": "Острая интрига",
                    "targetGuardianId": "guardian_beta",
                    "finalState": "Completed",
                    "completionTurn": 12,
                    "outcome": "Соперник ослаблен."
                  }
                }
              ],
              "temporaryProjectModifiers": []
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_completion_rival_target_match.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_target_match",
                    "projectType": "offensive_intrigue",
                    "projectTier": "major",
                    "projectMode": "hostile",
                    "projectName": "Острая интрига",
                    "targetGuardianId": "guardian_beta",
                    "finalState": "Completed",
                    "completionTurn": 12,
                    "outcome": "Соперник ослаблен."
                  }
                }
              ],
              "temporaryProjectModifiers": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_missing_current_tracker_authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerEventValidation_UpdateSourcedRivalStrikeRequiresNegativeDelta()
    {
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 52, "tier": "Могущественная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "evt_update_strike_zero",
              "guardianId": "guardian_alpha",
              "delta": 0,
              "reasonType": "rival_strike",
              "sourceSurface": "guardianProjectUpdates",
              "sourceId": "proj_update_strike",
              "title": "Нулевой sabotage strike",
              "summary": "update-sourced rival_strike должен быть hostile loss.",
              "visibility": "player_known",
              "relatedGuardianId": "guardian_beta",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectGuardianId": "guardian_alpha",
                "projectId": "proj_update_strike",
                "projectName": "Подрыв внешнего кольца",
                "projectType": "abode_expansion",
                "projectTier": "major",
                "HostileReach": 1,
                "ProjectExposure": 1,
                "DamageIntent": 2,
                "DamageAchieved": 1,
                "PlayerComplicity": 1,
                "sabotageSeverityScore": 6,
                "classification": "major sabotage"
              }
            },
            {
              "eventId": "evt_update_strike_positive",
              "guardianId": "guardian_alpha",
              "delta": 2,
              "reasonType": "rival_strike",
              "sourceSurface": "guardianProjectUpdates",
              "sourceId": "proj_update_strike",
              "title": "Положительный sabotage strike",
              "summary": "sabotage strike не может усиливать цель.",
              "visibility": "player_known",
              "relatedGuardianId": "guardian_beta",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectGuardianId": "guardian_alpha",
                "projectId": "proj_update_strike",
                "projectName": "Подрыв внешнего кольца",
                "projectType": "abode_expansion",
                "projectTier": "major",
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
        """));

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_update_strike",
                "projectType": "abode_expansion",
                "projectTier": "major",
                "projectMode": "internal",
                "projectName": "Подрыв внешнего кольца",
                "activeState": "Holding the outer works",
                "totalWork": 18,
                "workDone": 8,
                "totalStages": 3,
                "currentStage": 2,
                "pressure": 5,
                "stability": 72
              }
            }
          ],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_update_rival_strike_delta_sign_mismatch", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".guardianPowerEvents[0].delta", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_update_rival_strike_delta_sign_mismatch", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".guardianPowerEvents[1].delta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerJournalValidation_LegacyCompletionRivalStrikeRequiresStrictTrackerAuthorityBeforeRepair()
    {
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 60, "tier": "Могущественная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """));
        await MirrorCurrentGuardiansToPreTurnSnapshotAsync("test_backups/preturn_guardians_legacy_completion_rival_owner.json");

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_same_key_shadow.json",
            """
            {
              "activeProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_same_key_current",
                    "projectType": "offensive_intrigue",
                    "projectTier": "major",
                    "projectMode": "offensive",
                    "projectName": "Каноническая интрига",
                    "targetGuardianId": "guardian_beta",
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
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_legacy_completion_strike",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Старая завершённая интрига",
                "targetGuardianId": "guardian_beta",
                "finalState": "Completed",
                "completionTurn": 12,
                "outcome": "Наследуемый political strike."
              }
            }
          ],
          "temporaryProjectModifiers": []
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_legacy_completion_strike_exact.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_legacy_completion_strike",
                    "projectType": "offensive_intrigue",
                    "projectTier": "major",
                    "projectMode": "offensive",
                    "projectName": "Старая завершённая интрига",
                    "targetGuardianId": "guardian_beta",
                    "finalState": "Completed",
                    "completionTurn": 12,
                    "outcome": "Наследуемый political strike."
                  }
                }
              ],
              "temporaryProjectModifiers": []
            }
            """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "journal_legacy_completion_strike",
              "eventId": "evt_legacy_completion_strike",
              "turn": 12,
              "guardianId": "guardian_beta",
              "guardianName": "Варак",
              "delta": -2,
              "reasonType": "rival_strike",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_legacy_completion_strike",
              "title": "Legacy target-side strike",
              "summary": "Старый strike без relatedGuardianId должен repair'иться из owner-bound project truth.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectId": "proj_legacy_completion_strike",
                "projectName": "Старая завершённая интрига",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "finalState": "Completed",
                "attackerCurrentPower": 60,
                "targetCurrentPower": 48,
                "baseLoss": 4,
                "attackerBonus": 1,
                "baseTargetShield": 0,
                "fortificationBonus": 0,
                "counterOperationBonus": 0,
                "playerDefenseBonus": 0,
                "targetShield": 0,
                "targetLoss": 3,
                "pressureDelta": 2,
                "stabilityDamage": 1
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_same_key_current.json",
            """
            {
              "activeProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_same_key_current",
                    "projectType": "offensive_intrigue",
                    "projectTier": "major",
                    "projectMode": "offensive",
                    "projectName": "Каноническая интрига",
                    "targetGuardianId": "guardian_beta",
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
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            issue.FilePath.EndsWith("game_state/meta/abode_power_journal.json", StringComparison.OrdinalIgnoreCase) &&
            issue.Message.Contains("readable current guardian project tracker authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerJournalValidation_CompletedPoliticalAuditMissingFinalState_FailsValidation()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await MirrorCurrentGuardiansToPreTurnSnapshotAsync("test_backups/preturn_guardians_completed_political_missing_final_state.json");
        await EnsureReadableCurrentGuardianProjectTrackerAsync();

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "journal_completed_missing_final_state",
              "eventId": "evt_completed_missing_final_state",
              "turn": 12,
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "delta": 2,
              "reasonType": "project_completion",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_alpha",
              "title": "Завершение без finalState",
              "summary": "Power history entry потеряла finalState completion-аудита.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectId": "proj_alpha",
                "projectName": "Контур Азалии",
                "projectType": "abode_expansion",
                "projectTier": "major"
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => issue.FilePath.EndsWith(".entries[0].audit.finalState", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerJournalValidation_UnknownRelatedGuardian_FailsValidation()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await MirrorCurrentGuardiansToPreTurnSnapshotAsync("test_backups/preturn_guardians_unknown_related_journal.json");
        await EnsureReadableCurrentGuardianProjectTrackerAsync();

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "journal_unknown_related",
              "eventId": "evt_unknown_related",
              "turn": 12,
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "delta": -2,
              "reasonType": "rival_strike",
              "sourceSurface": "guardianProjectUpdates",
              "sourceId": "proj_alpha",
              "title": "Фантомная атака",
              "summary": "Ссылка на несуществующего rival.",
              "visibility": "player_known",
              "relatedGuardianId": "guardian_missing",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectId": "proj_alpha",
                "classification": "major sabotage"
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_unknown_related_guardian_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".entries[0].relatedGuardianId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerJournalValidation_UnknownGuardianId_FailsValidation()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await MirrorCurrentGuardiansToPreTurnSnapshotAsync("test_backups/preturn_guardians_unknown_guardian_journal.json");
        await EnsureReadableCurrentGuardianProjectTrackerAsync();

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "journal_phantom_guardian",
              "eventId": "evt_phantom_guardian",
              "guardianId": "guardian_missing",
              "guardianName": "Фантом",
              "turn": 12,
              "delta": -2,
              "reasonType": "rival_strike",
              "sourceSurface": "guardianProjectUpdates",
              "sourceId": "proj_alpha",
              "title": "Фантомный удар",
              "summary": "Событие от несуществующего Хранителя.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectId": "proj_alpha",
                "classification": "major sabotage"
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_unknown_guardian_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".entries[0].guardianId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerJournalValidation_SelfRelatedGuardian_FailsValidation()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await MirrorCurrentGuardiansToPreTurnSnapshotAsync("test_backups/preturn_guardians_self_related_journal.json");
        await EnsureReadableCurrentGuardianProjectTrackerAsync();

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "journal_self_related",
              "eventId": "evt_self_related",
              "turn": 12,
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "delta": -2,
              "reasonType": "rival_strike",
              "sourceSurface": "guardianProjectUpdates",
              "sourceId": "proj_alpha",
              "title": "Самоссылка",
              "summary": "Событие с некорректной self-related ссылкой.",
              "visibility": "player_known",
              "relatedGuardianId": "guardian_alpha",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectId": "proj_alpha",
                "classification": "major sabotage"
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_related_guardian_self_reference", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".entries[0].relatedGuardianId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProjectValidation_CanonicalOffensiveAudit_InvalidPlayerDefenseBonus_ProducesSingleIssue()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 52, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guardian_alpha", "attitudeScore": -60, "attitudeTier": "rival", "reason": "Hostility", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_invalid_canonical_player_defense",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Неверный канонический аудит",
                "targetGuardianId": "guardian_beta",
                "completionTurn": 41,
                "finalState": "Completed",
                "outcome": "Интрига завершена.",
                "offensiveImpactAudit": {
                  "targetLoss": 1,
                  "playerDefenseBonus": "shield"
                }
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_legacy_completion_owner_repair.json",
            """
            {
              "completedProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_completion_owner_repair",
                    "projectType": "offensive_intrigue",
                    "projectTier": "major",
                    "projectMode": "offensive",
                    "projectName": "Каноническая интрига",
                    "targetGuardianId": "guardian_beta",
                    "finalState": "Completed",
                    "completionTurn": 12,
                    "outcome": "Удар завершён."
                  }
                }
              ],
              "activeProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_legacy_repairable_strike.json",
            """
            {
              "completedProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_repairable_strike",
                    "projectType": "offensive_intrigue",
                    "projectTier": "major",
                    "projectMode": "offensive",
                    "projectName": "Каноническая интрига",
                    "targetGuardianId": "guardian_beta",
                    "finalState": "Completed",
                    "completionTurn": 12,
                    "outcome": "Удар завершён."
                  }
                }
              ],
              "activeProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        var playerDefenseIssues = issues.Where(issue =>
            issue.FilePath.EndsWith(".completedProjects[0].project.offensiveImpactAudit.playerDefenseBonus", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Single(playerDefenseIssues);
        Assert.Equal("Поле должно быть целым числом", playerDefenseIssues[0].Message);
    }

    [Fact]
    public async Task GuardianPowerJournalValidation_ProjectCompletionFinalStateMismatch_FailsValidation()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await MirrorCurrentGuardiansToPreTurnSnapshotAsync("test_backups/preturn_guardians_journal_project_completion_mismatch.json");
        await EnsureReadableCurrentGuardianProjectTrackerAsync();

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "journal_project_completion_mismatch",
              "eventId": "evt_project_completion_mismatch",
              "turn": 12,
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "delta": 2,
              "reasonType": "project_completion",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_alpha",
              "title": "Некорректный completion audit",
              "summary": "project_completion не должен нести failed finalState.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectId": "proj_alpha",
                "projectName": "Контур Азалии",
                "projectType": "abode_expansion",
                "projectTier": "major",
                "finalState": "Abandoned"
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_project_completion_final_state_mismatch", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".entries[0].audit.finalState", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerJournalValidation_ProjectFailureFinalStateMismatch_FailsValidation()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await MirrorCurrentGuardiansToPreTurnSnapshotAsync("test_backups/preturn_guardians_journal_project_failure_mismatch.json");
        await EnsureReadableCurrentGuardianProjectTrackerAsync();

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "journal_project_failure_mismatch",
              "eventId": "evt_project_failure_mismatch",
              "turn": 12,
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "delta": -2,
              "reasonType": "project_failure",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_alpha",
              "title": "Некорректный failure audit",
              "summary": "project_failure не должен нести Completed.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectId": "proj_alpha",
                "projectName": "Контур Азалии",
                "projectType": "abode_expansion",
                "projectTier": "major",
                "finalState": "Completed"
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_project_failure_final_state_mismatch", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".entries[0].audit.finalState", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerJournalValidation_PoliticalAuditProjectIdMustMatchSourceId()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await MirrorCurrentGuardiansToPreTurnSnapshotAsync("test_backups/preturn_guardians_journal_project_id_match.json");
        await EnsureReadableCurrentGuardianProjectTrackerAsync();

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "journal_project_id_mismatch",
              "eventId": "evt_project_id_mismatch",
              "turn": 12,
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "delta": -2,
              "reasonType": "project_completion",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_alpha",
              "title": "Несогласованная identity проекта",
              "summary": "audit.projectId расходится с sourceId.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectId": "proj_other",
                "projectName": "Чужой completion",
                "projectType": "abode_expansion",
                "projectTier": "major",
                "finalState": "Completed"
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_project_source_id_mismatch", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".entries[0].audit.projectId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerJournalAppend_BackfillsLegacyPoliticalAuditIdentityFromTracker()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_legacy_completion",
                "projectType": "abode_expansion",
                "projectTier": "major",
                "projectMode": "internal",
                "projectName": "Наследуемый проект",
                "finalState": "Completed",
                "completionTurn": 11,
                "outcome": "Проект завершён."
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_political_truth.json",
            """
            {
              "completedProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_truth",
                    "projectType": "offensive_intrigue",
                    "projectTier": "major",
                    "projectMode": "offensive",
                    "projectName": "Истинное имя проекта",
                    "targetGuardianId": "guardian_beta",
                    "finalState": "Completed",
                    "completionTurn": 12,
                    "outcome": "Удар завершён."
                  }
                }
              ],
              "activeProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "legacy_entry",
              "eventId": "legacy_event",
              "turn": 11,
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "delta": 2,
              "reasonType": "project_completion",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_legacy_completion",
              "title": "Старый журнал",
              "summary": "Запись старого формата без project identity.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
              }
            }
          ]
        }
        """);

        await GuardianPowerEventState.AppendJournalEntriesAsync(_fs, new[]
        {
            GuardianPowerEventState.BuildEvent(
                "evt_append_trigger",
                "guardian_alpha",
                1,
                "offering",
                "offer_guardian",
                "offering_cycle",
                "Тестовое приношение",
                "Триггер для backfill существующего journal.",
                new JsonObject
                {
                    ["offeringType"] = "ink_feathers",
                    ["returnCycleId"] = "cycle_alpha",
                    ["baseDelta"] = 1,
                    ["finalDelta"] = 1,
                    ["inkFeathersOffered"] = 1,
                    ["capRemainingBefore"] = 4
                })
        });

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.FilePath.EndsWith(".entries[0].audit.projectGuardianId", StringComparison.OrdinalIgnoreCase) ||
            issue.FilePath.EndsWith(".entries[0].audit.projectName", StringComparison.OrdinalIgnoreCase) ||
            issue.FilePath.EndsWith(".entries[0].audit.projectType", StringComparison.OrdinalIgnoreCase) ||
            issue.FilePath.EndsWith(".entries[0].audit.projectTier", StringComparison.OrdinalIgnoreCase) ||
            issue.FilePath.EndsWith(".entries[0].audit.finalState", StringComparison.OrdinalIgnoreCase));

        var journal = await ReadObjectAsync(GuardianPowerEventState.JournalPath);
        var legacyEntry = journal["entries"]!.AsArray().OfType<JsonObject>().Single(item =>
            string.Equals(item["eventId"]?.GetValue<string>(), "legacy_event", StringComparison.OrdinalIgnoreCase));
        var audit = legacyEntry["audit"]!.AsObject();

        Assert.Equal("guardian_alpha", audit["projectGuardianId"]?.GetValue<string>());
        Assert.Equal("proj_legacy_completion", audit["projectId"]?.GetValue<string>());
        Assert.Equal("Наследуемый проект", audit["projectName"]?.GetValue<string>());
        Assert.Equal("abode_expansion", audit["projectType"]?.GetValue<string>());
        Assert.Equal("major", audit["projectTier"]?.GetValue<string>());
        Assert.Equal("Completed", audit["finalState"]?.GetValue<string>());
    }

    [Fact]
    public async Task GuardianPowerJournalValidation_LegacyPoliticalAuditBackfillsFromPreTurnTrackerWithoutAppend()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "game_state/control/rollback_backups/game_state_meta_guardian_projects.json",
            """
            {
              "completedProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_preturn_legacy",
                    "projectType": "abode_expansion",
                    "projectTier": "major",
                    "projectMode": "internal",
                    "projectName": "Проект из pre-turn snapshot",
                    "finalState": "Completed",
                    "completionTurn": 10,
                    "outcome": "Завершён раньше."
                  }
                }
              ]
            }
            """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "legacy_preturn_entry",
              "eventId": "legacy_preturn_event",
              "turn": 10,
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "delta": 2,
              "reasonType": "project_completion",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_preturn_legacy",
              "title": "Старый completion из snapshot",
              "summary": "Identity должна добраться из pre-turn tracker без append-trigger.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.FilePath.EndsWith(".entries[0].audit.projectGuardianId", StringComparison.OrdinalIgnoreCase) ||
            issue.FilePath.EndsWith(".entries[0].audit.projectName", StringComparison.OrdinalIgnoreCase) ||
            issue.FilePath.EndsWith(".entries[0].audit.projectType", StringComparison.OrdinalIgnoreCase) ||
            issue.FilePath.EndsWith(".entries[0].audit.projectTier", StringComparison.OrdinalIgnoreCase) ||
            issue.FilePath.EndsWith(".entries[0].audit.finalState", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerJournalValidation_PoliticalReasonTypeMustMatchSourceSurface()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await MirrorCurrentGuardiansToPreTurnSnapshotAsync("test_backups/preturn_guardians_journal_reason_type_surface.json");
        await EnsureReadableCurrentGuardianProjectTrackerAsync();

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "journal_invalid_surface",
              "eventId": "evt_invalid_surface",
              "turn": 12,
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "delta": 2,
              "reasonType": "project_completion",
              "sourceSurface": "guardianProjectUpdates",
              "sourceId": "proj_alpha",
              "title": "Некорректная связка sourceSurface",
              "summary": "project_completion не должен идти из guardianProjectUpdates.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectId": "proj_alpha",
                "projectName": "Контур Азалии",
                "projectType": "abode_expansion",
                "projectTier": "major",
                "finalState": "Completed"
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_reason_type_source_surface_mismatch", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".entries[0].audit.projectId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerJournalValidation_PoliticalMetadataRequiresStrictTrackerAuthorityBeforeCanonicalization()
    {
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """));

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_truth",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Истинное имя проекта",
                "targetGuardianId": "guardian_beta",
                "finalState": "Completed",
                "completionTurn": 12,
                "outcome": "Удар завершён."
              }
            }
          ],
          "activeProjects": [],
          "temporaryProjectModifiers": []
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_political_truth_exact.json",
            """
            {
              "completedProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_truth",
                    "projectType": "offensive_intrigue",
                    "projectTier": "major",
                    "projectMode": "offensive",
                    "projectName": "Истинное имя проекта",
                    "targetGuardianId": "guardian_beta",
                    "finalState": "Completed",
                    "completionTurn": 12,
                    "outcome": "Удар завершён."
                  }
                }
              ],
              "activeProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "journal_metadata_mismatch",
              "eventId": "evt_metadata_mismatch",
              "turn": 12,
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "delta": 2,
              "reasonType": "project_completion",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_truth",
              "title": "Ложная metadata проекта",
              "summary": "repairable legacy metadata должна canonicalize'иться по tracker truth.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectId": "proj_truth",
                "projectName": "Ложное имя проекта",
                "projectType": "counter_rival_operation",
                "projectTier": "minor",
                "finalState": "Completed"
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_projectName_mismatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "guardian_power_event_projectType_mismatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "guardian_power_event_projectTier_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_missing_current_tracker_authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerJournalValidation_RepairableLegacyRivalStrikeRequiresStrictTrackerAuthorityBeforeRepair()
    {
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 52, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 44, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """));

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_repairable_strike",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Каноническая интрига",
                "targetGuardianId": "guardian_beta",
                "finalState": "Completed",
                "completionTurn": 12,
                "outcome": "Удар завершён."
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_repairable_strike_exact.json",
            """
            {
              "completedProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_repairable_strike",
                    "projectType": "offensive_intrigue",
                    "projectTier": "major",
                    "projectMode": "offensive",
                    "projectName": "Каноническая интрига",
                    "targetGuardianId": "guardian_beta",
                    "finalState": "Completed",
                    "completionTurn": 12,
                    "outcome": "Удар завершён."
                  }
                }
              ],
              "activeProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_repairable_invalid_related_guardian.json",
            """
            {
              "completedProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_repairable_strike",
                    "projectType": "offensive_intrigue",
                    "projectTier": "major",
                    "projectMode": "offensive",
                    "projectName": "Каноническая интрига",
                    "targetGuardianId": "guardian_beta",
                    "finalState": "Completed",
                    "completionTurn": 12,
                    "outcome": "Удар завершён."
                  }
                }
              ],
              "activeProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_duplicate_project_id_target_side.json",
            """
            {
              "completedProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_duplicate",
                    "projectType": "offensive_intrigue",
                    "projectTier": "major",
                    "projectMode": "offensive",
                    "projectName": "Дубликат alpha",
                    "targetGuardianId": "guardian_beta",
                    "finalState": "Completed",
                    "completionTurn": 12,
                    "outcome": "Удар alpha."
                  }
                },
                {
                  "guardianId": "guardian_gamma",
                  "project": {
                    "projectId": "proj_duplicate",
                    "projectType": "offensive_intrigue",
                    "projectTier": "major",
                    "projectMode": "offensive",
                    "projectName": "Дубликат gamma",
                    "targetGuardianId": "guardian_beta",
                    "finalState": "Completed",
                    "completionTurn": 12,
                    "outcome": "Удар gamma."
                  }
                }
              ],
              "activeProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "journal_repairable_related_guardian",
              "eventId": "evt_repairable_related_guardian",
              "turn": 12,
              "guardianId": "guardian_beta",
              "guardianName": "Варак",
              "delta": -2,
              "reasonType": "rival_strike",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_repairable_strike",
              "title": "Repairable legacy strike",
              "summary": "Raw relatedGuardianId неверен, но source project owner известен.",
              "visibility": "player_known",
              "relatedGuardianId": "guardian_missing",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectId": "proj_repairable_strike",
                "projectName": "Старое имя",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "finalState": "Completed",
                "targetLoss": 2,
                "pressureDelta": 1,
                "stabilityDamage": 1
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            issue.FilePath.EndsWith("game_state/meta/abode_power_journal.json", StringComparison.OrdinalIgnoreCase) &&
            issue.Message.Contains("validated pre-turn project tracker baseline", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerJournalValidation_TargetSideRivalStrikeRequiresStrictTrackerAuthorityBeforeProjectResolution()
    {
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 55, "tier": "Могущественная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            },
            {
              "guardianId": "guardian_gamma",
              "canonicalName": "Орфел",
              "nameVariants": { "default": "Орфел", "feminine": null, "masculine": "Орфел", "neutral": null },
              "manifestation": {
                "currentDisplayName": "Орфел",
                "formFlexibility": "fixed",
                "currentPresentationStyle": "masculine",
                "currentPronouns": "он/его",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 60, "tier": "Могущественная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """));
        await MirrorCurrentGuardiansToPreTurnSnapshotAsync("test_backups/preturn_guardians_target_side_duplicate_project_id.json");

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_shared",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Интрига Азалии",
                "finalState": "Completed",
                "completionTurn": 11,
                "outcome": "Азалия нанесла удар."
              }
            },
            {
              "guardianId": "guardian_gamma",
              "project": {
                "projectId": "proj_shared",
                "projectType": "abode_expansion",
                "projectTier": "major",
                "projectMode": "internal",
                "projectName": "Расширение Орфела",
                "finalState": "Completed",
                "completionTurn": 11,
                "outcome": "Орфел завершил развитие."
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_duplicate_project_id_exact.json",
            """
            {
              "completedProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_duplicate",
                    "projectType": "offensive_intrigue",
                    "projectTier": "major",
                    "projectMode": "offensive",
                    "projectName": "Дубликат alpha",
                    "targetGuardianId": "guardian_beta",
                    "finalState": "Completed",
                    "completionTurn": 12,
                    "outcome": "Удар alpha."
                  }
                },
                {
                  "guardianId": "guardian_gamma",
                  "project": {
                    "projectId": "proj_duplicate",
                    "projectType": "offensive_intrigue",
                    "projectTier": "major",
                    "projectMode": "offensive",
                    "projectName": "Дубликат gamma",
                    "targetGuardianId": "guardian_beta",
                    "finalState": "Completed",
                    "completionTurn": 12,
                    "outcome": "Удар gamma."
                  }
                }
              ],
              "activeProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_target_side_duplicate_project_id.json",
            """
            {
              "completedProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_duplicate",
                    "projectType": "offensive_intrigue",
                    "projectTier": "major",
                    "projectMode": "offensive",
                    "projectName": "Дубликат alpha",
                    "targetGuardianId": "guardian_beta",
                    "finalState": "Completed",
                    "completionTurn": 12,
                    "outcome": "Удар alpha."
                  }
                },
                {
                  "guardianId": "guardian_gamma",
                  "project": {
                    "projectId": "proj_duplicate",
                    "projectType": "offensive_intrigue",
                    "projectTier": "major",
                    "projectMode": "offensive",
                    "projectName": "Дубликат gamma",
                    "targetGuardianId": "guardian_beta",
                    "finalState": "Completed",
                    "completionTurn": 12,
                    "outcome": "Удар gamma."
                  }
                }
              ],
              "activeProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "journal_owner_bound_rival_strike",
              "eventId": "evt_owner_bound_rival_strike",
              "turn": 12,
              "guardianId": "guardian_beta",
              "guardianName": "Варак",
              "delta": -3,
              "reasonType": "rival_strike",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_shared",
              "title": "Owner-bound удар",
              "summary": "duplicate projectId не должен ломать source resolution.",
              "visibility": "player_known",
              "relatedGuardianId": "guardian_alpha",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectGuardianId": "guardian_alpha",
                "projectId": "proj_shared",
                "projectName": "Интрига Азалии",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "finalState": "Completed",
                "attackerCurrentPower": 60,
                "targetCurrentPower": 48,
                "baseLoss": 4,
                "attackerBonus": 1,
                "baseTargetShield": 0,
                "fortificationBonus": 0,
                "counterOperationBonus": 0,
                "playerDefenseBonus": 0,
                "targetShield": 0,
                "targetLoss": 3,
                "pressureDelta": 2,
                "stabilityDamage": 1
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_missing_current_tracker_authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerEventValidation_PoliticalAuditUnknownSourceProject_FailsValidation()
    {
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 55, "tier": "Могущественная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "evt_unknown_source_project",
              "guardianId": "guardian_beta",
              "delta": -3,
              "reasonType": "rival_strike",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_missing",
              "title": "Несуществующий source project",
              "summary": "Политический удар не должен проходить без canonical project source.",
              "visibility": "player_known",
              "relatedGuardianId": "guardian_alpha",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectGuardianId": "guardian_alpha",
                "projectId": "proj_missing",
                "projectName": "Фантомный проект",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "finalState": "Completed",
                "attackerCurrentPower": 60,
                "targetCurrentPower": 48,
                "baseLoss": 4,
                "attackerBonus": 1,
                "baseTargetShield": 0,
                "fortificationBonus": 0,
                "counterOperationBonus": 0,
                "playerDefenseBonus": 0,
                "targetShield": 0,
                "targetLoss": 3,
                "pressureDelta": 2,
                "stabilityDamage": 1
              }
            }
          ]
        }
        """));
        await MirrorCurrentGuardiansToPreTurnSnapshotAsync("test_backups/preturn_guardians_power_unknown_project.json");
        await EnsureReadableCurrentGuardianProjectTrackerAsync();

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_unknown_source_project", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".guardianPowerEvents[0].audit.projectId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerJournalAppend_StalePreTurnSnapshotWithoutManifest_DoesNotBackfillPoliticalIdentity()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WriteRawAsync("game_state/control/pending_turn_snapshot/game_state/meta/guardian_projects.json", """
        {
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_stale_snapshot",
                "projectType": "abode_expansion",
                "projectTier": "major",
                "projectMode": "internal",
                "projectName": "Старый snapshot проект",
                "finalState": "Completed",
                "completionTurn": 11,
                "outcome": "Завершён."
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
        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_tracker_missing_journal_baseline.json",
            """
            {
              "activeProjects": [],
              "completedProjects": []
            }
            """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "legacy_snapshot_entry",
              "eventId": "legacy_snapshot_event",
              "turn": 11,
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "delta": 2,
              "reasonType": "project_completion",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_stale_snapshot",
              "title": "Сиротский snapshot",
              "summary": "Append не должен backfill'ить из orphaned snapshot без manifest.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
              }
            }
          ]
        }
        """);

        await GuardianPowerEventState.AppendJournalEntriesAsync(_fs, new[]
        {
            GuardianPowerEventState.BuildEvent(
                "evt_append_after_stale_snapshot",
                "guardian_alpha",
                1,
                "offering",
                "offer_guardian",
                "offering_cycle",
                "Тестовое приношение",
                "Append trigger without valid manifest.",
                new JsonObject
                {
                    ["offeringType"] = "ink_feathers",
                    ["returnCycleId"] = "cycle_alpha",
                    ["baseDelta"] = 1,
                    ["finalDelta"] = 1,
                    ["inkFeathersOffered"] = 1,
                    ["capRemainingBefore"] = 4
                })
        });

        var journal = await ReadObjectAsync(GuardianPowerEventState.JournalPath);
        var legacyEntry = journal["entries"]!.AsArray().OfType<JsonObject>().Single(item =>
            string.Equals(item["eventId"]?.GetValue<string>(), "legacy_snapshot_event", StringComparison.OrdinalIgnoreCase));
        var audit = legacyEntry["audit"]!.AsObject();

        Assert.Null(audit["projectGuardianId"]);
        Assert.Null(audit["projectName"]);
        Assert.Null(audit["projectType"]);
        Assert.Null(audit["projectTier"]);
        Assert.Null(audit["finalState"]);
    }

    [Fact]
    public async Task GuardianPowerJournalAppend_ManifestBackedPreTurnSnapshot_BackfillsPoliticalIdentity()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_append_only_snapshot.json",
            """
            {
              "completedProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_manifest_backfill",
                    "projectType": "abode_expansion",
                    "projectTier": "major",
                    "projectMode": "internal",
                    "projectName": "Manifest-backed snapshot проект",
                    "finalState": "Completed",
                    "completionTurn": 11,
                    "outcome": "Завершён."
                  }
                }
              ]
            }
            """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "legacy_manifest_entry",
              "eventId": "legacy_manifest_event",
              "turn": 11,
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "delta": 2,
              "reasonType": "project_completion",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_manifest_backfill",
              "title": "Manifest-backed snapshot",
              "summary": "Append-time backfill должен подтянуть identity из manifest-backed pending snapshot.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
              }
            }
          ]
        }
        """);

        await GuardianPowerEventState.AppendJournalEntriesAsync(_fs, new[]
        {
            GuardianPowerEventState.BuildEvent(
                "evt_append_after_manifest_snapshot",
                "guardian_alpha",
                1,
                "offering",
                "offer_guardian",
                "offering_cycle",
                "Тестовое приношение",
                "Append trigger with manifest-backed snapshot present.",
                new JsonObject
                {
                    ["offeringType"] = "ink_feathers",
                    ["returnCycleId"] = "cycle_alpha",
                    ["baseDelta"] = 1,
                    ["finalDelta"] = 1,
                    ["inkFeathersOffered"] = 1,
                    ["capRemainingBefore"] = 4
                })
        });

        var journal = await ReadObjectAsync(GuardianPowerEventState.JournalPath);
        var legacyEntry = journal["entries"]!.AsArray().OfType<JsonObject>().Single(item =>
            string.Equals(item["eventId"]?.GetValue<string>(), "legacy_manifest_event", StringComparison.OrdinalIgnoreCase));
        var audit = legacyEntry["audit"]!.AsObject();

        Assert.Equal("guardian_alpha", audit["projectGuardianId"]?.GetValue<string>());
        Assert.Equal("Manifest-backed snapshot проект", audit["projectName"]?.GetValue<string>());
        Assert.Equal("abode_expansion", audit["projectType"]?.GetValue<string>());
        Assert.Equal("major", audit["projectTier"]?.GetValue<string>());
        Assert.Equal("Completed", audit["finalState"]?.GetValue<string>());
    }

    [Fact]
    public async Task GuardianPowerJournalAppend_InvalidManifestHash_DoesNotBackfillPoliticalIdentity()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_invalid_manifest_hash.json",
            """
            {
              "completedProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_invalid_manifest_hash",
                    "projectType": "abode_expansion",
                    "projectTier": "major",
                    "projectMode": "internal",
                    "projectName": "Manifest с битым hash",
                    "finalState": "Completed",
                    "completionTurn": 11,
                    "outcome": "Не должен использоваться."
                  }
                }
              ]
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["manifestPayloadHash"] = "BROKEN_MANIFEST_HASH";
        await WriteRawAsync("game_state/control/pending_turn_snapshot.json", manifest.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "legacy_invalid_manifest_hash_entry",
              "eventId": "legacy_invalid_manifest_hash_event",
              "turn": 11,
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "delta": 2,
              "reasonType": "project_completion",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_invalid_manifest_hash",
              "title": "Битый manifest hash",
              "summary": "Append не должен backfill'ить из snapshot с неверным manifestPayloadHash.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {}
            }
          ]
        }
        """);

        await GuardianPowerEventState.AppendJournalEntriesAsync(_fs, new[]
        {
            GuardianPowerEventState.BuildEvent(
                "evt_append_after_invalid_manifest_hash",
                "guardian_alpha",
                1,
                "offering",
                "offer_guardian",
                "offering_cycle",
                "Тестовое приношение",
                "Append trigger with invalid manifest hash.",
                new JsonObject
                {
                    ["offeringType"] = "ink_feathers",
                    ["returnCycleId"] = "cycle_alpha",
                    ["baseDelta"] = 1,
                    ["finalDelta"] = 1,
                    ["inkFeathersOffered"] = 1,
                    ["capRemainingBefore"] = 4
                })
        });

        var journal = await ReadObjectAsync(GuardianPowerEventState.JournalPath);
        var legacyEntry = journal["entries"]!.AsArray().OfType<JsonObject>().Single(item =>
            string.Equals(item["eventId"]?.GetValue<string>(), "legacy_invalid_manifest_hash_event", StringComparison.OrdinalIgnoreCase));
        var audit = legacyEntry["audit"]!.AsObject();

        Assert.Null(audit["projectGuardianId"]);
        Assert.Null(audit["projectName"]);
        Assert.Null(audit["projectType"]);
        Assert.Null(audit["projectTier"]);
        Assert.Null(audit["finalState"]);
    }

    [Fact]
    public async Task GuardianPowerJournalRepair_TargetMismatchDoesNotCanonicalizeCompletionStrikeIdentity()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 60, "tier": "Могущественная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            },
            {
              "guardianId": "guardian_gamma",
              "canonicalName": "Орфел",
              "nameVariants": { "default": "Орфел", "feminine": null, "masculine": "Орфел", "neutral": null },
              "manifestation": {
                "currentDisplayName": "Орфел",
                "formFlexibility": "fixed",
                "currentPresentationStyle": "masculine",
                "currentPronouns": "он/его",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 25, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 44, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_wrong_victim_repair",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Интрига Азалии",
                "targetGuardianId": "guardian_beta",
                "finalState": "Completed",
                "completionTurn": 12,
                "outcome": "Цель должна быть только guardian_beta."
              }
            }
          ],
          "temporaryProjectModifiers": []
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_wrong_victim_exact.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_wrong_victim_repair",
                    "projectType": "offensive_intrigue",
                    "projectTier": "major",
                    "projectMode": "offensive",
                    "projectName": "Интрига Азалии",
                    "targetGuardianId": "guardian_beta",
                    "finalState": "Completed",
                    "completionTurn": 12,
                    "outcome": "Цель должна быть только guardian_beta."
                  }
                }
              ],
              "temporaryProjectModifiers": []
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_wrong_victim_repair_validation.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_wrong_victim_repair",
                    "projectType": "offensive_intrigue",
                    "projectTier": "major",
                    "projectMode": "offensive",
                    "projectName": "Интрига Азалии",
                    "targetGuardianId": "guardian_beta",
                    "finalState": "Completed",
                    "completionTurn": 12,
                    "outcome": "Цель должна быть только guardian_beta."
                  }
                }
              ],
              "temporaryProjectModifiers": []
            }
            """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "journal_wrong_victim_repair",
              "eventId": "evt_wrong_victim_repair",
              "turn": 12,
              "guardianId": "guardian_gamma",
              "guardianName": "Орфел",
              "delta": -2,
              "reasonType": "rival_strike",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_wrong_victim_repair",
              "title": "Legacy strike с неправильной жертвой",
              "summary": "Repair не должен partially canonicalize target-side strike, если victim расходится с source project target.",
              "visibility": "player_known",
              "relatedGuardianId": "guardian_missing",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectId": "proj_wrong_victim_repair",
                "projectGuardianId": "guardian_missing",
                "projectName": "Старое ложное имя",
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

        await GuardianPowerEventState.RepairJournalAsync(_fs);

        var journal = await ReadObjectAsync(GuardianPowerEventState.JournalPath);
        var entry = journal["entries"]!.AsArray().OfType<JsonObject>().Single();

        Assert.Equal("guardian_missing", entry["relatedGuardianId"]?.GetValue<string>());
        Assert.Equal("guardian_missing", entry["audit"]?["projectGuardianId"]?.GetValue<string>());
        Assert.Equal("Старое ложное имя", entry["audit"]?["projectName"]?.GetValue<string>());
        Assert.Equal("counter_rival_operation", entry["audit"]?["projectType"]?.GetValue<string>());
        Assert.Equal("minor", entry["audit"]?["projectTier"]?.GetValue<string>());
    }

    [Fact]
    public async Task GuardianPowerJournalValidation_TargetMismatchDoesNotReportCanonicalRepair()
    {
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 52, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 44, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            },
            {
              "guardianId": "guardian_gamma",
              "canonicalName": "Орфел",
              "nameVariants": { "default": "Орфел", "feminine": null, "masculine": "Орфел", "neutral": null },
              "manifestation": {
                "currentDisplayName": "Орфел",
                "formFlexibility": "fixed",
                "currentPresentationStyle": "masculine",
                "currentPronouns": "он/его",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 41, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """));

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_wrong_victim_validation",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Интрига Азалии",
                "targetGuardianId": "guardian_beta",
                "finalState": "Completed",
                "completionTurn": 12,
                "outcome": "Цель должна быть только guardian_beta."
              }
            }
          ],
          "temporaryProjectModifiers": []
        }
        """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "journal_wrong_victim_validation",
              "eventId": "evt_wrong_victim_validation",
              "turn": 12,
              "guardianId": "guardian_gamma",
              "guardianName": "Орфел",
              "delta": -2,
              "reasonType": "rival_strike",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_wrong_victim_validation",
              "title": "Legacy strike с неправильной жертвой",
              "summary": "Validator не должен помечать irreparable wrong-victim entry как repairable.",
              "visibility": "player_known",
              "relatedGuardianId": "guardian_alpha",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectId": "proj_wrong_victim_validation",
                "projectGuardianId": "guardian_alpha",
                "projectName": "Интрига Азалии",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "finalState": "Completed",
                "targetLoss": 2,
                "pressureDelta": 1,
                "stabilityDamage": 1
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_unknown_source_project", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "guardian_power_event_rival_strike_target_guardian_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_requires_canonical_repair", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".entries[0]", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerJournalValidation_InvalidPreTurnGuardianManifestDoesNotAuthorizeUnknownGuardian()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 52, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "game_state/control/rollback_backups/game_state_meta_guardians.json",
            """
            {
              "guardians": [
                {
                  "guardianId": "guardian_missing",
                  "canonicalName": "Фантом",
                  "nameVariants": { "default": "Фантом", "feminine": null, "masculine": null, "neutral": null },
                  "manifestation": {
                    "currentDisplayName": "Фантом",
                    "formFlexibility": "fixed",
                    "currentPresentationStyle": "neutral",
                    "currentPronouns": "они/их",
                    "appearanceDescription": "Несуществующий guardian."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 0, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["manifestPayloadHash"] = "BROKEN_MANIFEST_HASH";
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
              "entryId": "journal_unknown_guardian_after_invalid_manifest",
              "eventId": "evt_unknown_guardian_after_invalid_manifest",
              "turn": 12,
              "guardianId": "guardian_missing",
              "guardianName": "Фантом",
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_test",
              "title": "Несуществующий guardian не должен проходить через битый pre-turn manifest",
              "summary": "Journal validation должна опираться только на validated pre-turn guardians snapshot.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "offeringType": "soul_relic",
                "returnCycleId": "cycle_1",
                "baseDelta": 2,
                "finalDelta": 2,
                "relicId": "relic_test",
                "relicName": "Тестовый реликт",
                "relicRarity": "Rare"
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_missing_validated_preturn_guardians_snapshot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerJournalRepair_TargetAwareLookupDisambiguatesDuplicateProjectId()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 52, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 44, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            },
            {
              "guardianId": "guardian_gamma",
              "canonicalName": "Орфел",
              "nameVariants": { "default": "Орфел", "feminine": null, "masculine": "Орфел", "neutral": null },
              "manifestation": {
                "currentDisplayName": "Орфел",
                "formFlexibility": "fixed",
                "currentPresentationStyle": "masculine",
                "currentPronouns": "он/его",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 41, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_shared_target_aware",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Интрига Азалии",
                "targetGuardianId": "guardian_beta",
                "finalState": "Completed",
                "completionTurn": 12,
                "outcome": "Варак ослаблен."
              }
            },
            {
              "guardianId": "guardian_gamma",
              "project": {
                "projectId": "proj_shared_target_aware",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Интрига Орфела",
                "targetGuardianId": "guardian_alpha",
                "finalState": "Completed",
                "completionTurn": 12,
                "outcome": "Азалия ослаблена."
              }
            }
          ],
          "temporaryProjectModifiers": []
        }
        """);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "journal_target_aware_disambiguation",
              "eventId": "evt_target_aware_disambiguation",
              "turn": 12,
              "guardianId": "guardian_beta",
              "guardianName": "Варак",
              "delta": -2,
              "reasonType": "rival_strike",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_shared_target_aware",
              "title": "Target-aware repair",
              "summary": "duplicate projectId должен разрешаться по canonical target, даже если attacker metadata потеряна.",
              "visibility": "player_known",
              "relatedGuardianId": "guardian_missing",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectId": "proj_shared_target_aware",
                "projectGuardianId": "guardian_missing",
                "projectName": "Старое имя",
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

        await GuardianPowerEventState.RepairJournalAsync(_fs);

        var journal = await ReadObjectAsync(GuardianPowerEventState.JournalPath);
        var entry = journal["entries"]!.AsArray().OfType<JsonObject>().Single();

        Assert.Equal("guardian_alpha", entry["relatedGuardianId"]?.GetValue<string>());
        Assert.Equal("guardian_alpha", entry["audit"]?["projectGuardianId"]?.GetValue<string>());
        Assert.Equal("Интрига Азалии", entry["audit"]?["projectName"]?.GetValue<string>());
        Assert.Equal("offensive_intrigue", entry["audit"]?["projectType"]?.GetValue<string>());
        Assert.Equal("major", entry["audit"]?["projectTier"]?.GetValue<string>());
    }

    [Fact]
    public async Task GuardianPowerJournalRepair_UsesValidatedSnapshotTrackerWithoutRollbackBackup()
    {
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """);

        const string trackerBackupPath = "test_backups/preturn_guardian_projects_snapshot_only_repair.json";
        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            trackerBackupPath,
            """
            {
              "activeProjects": [],
              "completedProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_snapshot_only_repair",
                    "projectType": "offensive_intrigue",
                    "projectTier": "major",
                    "projectMode": "offensive",
                    "projectName": "Интрига из snapshot",
                    "targetGuardianId": "guardian_beta",
                    "finalState": "Completed",
                    "completionTurn": 12,
                    "outcome": "Validated snapshot tracker должен оставаться достаточным без rollback backup."
                  }
                }
              ],
              "temporaryProjectModifiers": []
            }
            """);
        await RemoveRollbackBackupFromCurrentPendingTurnSnapshotAsync(GuardianProjectState.TrackerPath, trackerBackupPath);

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "journal_snapshot_only_repair",
              "eventId": "evt_snapshot_only_repair",
              "turn": 12,
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "delta": 3,
              "reasonType": "project_completion",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_snapshot_only_repair",
              "title": "Snapshot-only repair",
              "summary": "Repair должен читать validated snapshot tracker даже без rollback backup file.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectId": "proj_snapshot_only_repair",
                "projectGuardianId": "guardian_alpha",
                "projectName": "Старое имя",
                "projectType": "counter_rival_operation",
                "projectTier": "minor",
                "finalState": "Failed"
              }
            }
          ]
        }
        """);

        await GuardianPowerEventState.RepairJournalAsync(_fs);

        var journal = await ReadObjectAsync(GuardianPowerEventState.JournalPath);
        var entry = journal["entries"]!.AsArray().OfType<JsonObject>().Single();

        Assert.Equal("Интрига из snapshot", entry["audit"]?["projectName"]?.GetValue<string>());
        Assert.Equal("offensive_intrigue", entry["audit"]?["projectType"]?.GetValue<string>());
        Assert.Equal("major", entry["audit"]?["projectTier"]?.GetValue<string>());
        Assert.Equal("Completed", entry["audit"]?["finalState"]?.GetValue<string>());
    }

    [Fact]
    public async Task GuardianProjectValidation_InvalidPreTurnTrackerDoesNotAuthorizePhantomUpdate()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_invalid_lifecycle.json",
            """
            {
              "activeProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_invalid_snapshot_only",
                    "projectType": "abode_expansion",
                    "projectTier": "major",
                    "projectMode": "internal",
                    "projectName": "Фантомный pre-turn project",
                    "activeState": "Should not authorize update",
                    "totalWork": 10,
                    "workDone": 4,
                    "totalStages": 2,
                    "currentStage": 1,
                    "pressure": 2,
                    "stability": 90
                  }
                }
              ],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["manifestPayloadHash"] = "BROKEN_MANIFEST_HASH";
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": [],
          "guardianProjectUpdates": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_invalid_snapshot_only",
              "workDone": 6
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_missing_validated_preturn_guardians_snapshot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProjectValidation_ReadableInvalidGuardianSnapshotFailsClosed()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": []
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_readable_but_invalid_for_project_validation.json",
            """
            {
              "guardians": [
                {
                  "guardianId": "guardian_alpha",
                  "canonicalName": "Азалия"
                }
              ]
            }
            """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": [],
          "guardianProjectUpdates": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_invalid_snapshot_guardian",
              "workDone": 3
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_missing_validated_preturn_guardians_snapshot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProjectValidation_StaleButHashValidPreTurnTrackerDoesNotAuthorizePhantomUpdate()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_stale_context.json",
            """
            {
              "activeProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_stale_snapshot_only",
                    "projectType": "abode_expansion",
                    "projectTier": "major",
                    "projectMode": "internal",
                    "projectName": "Hash-valid, but stale snapshot",
                    "activeState": "Should not authorize update",
                    "totalWork": 10,
                    "workDone": 4,
                    "totalStages": 2,
                    "currentStage": 1,
                    "pressure": 2,
                    "stability": 90
                  }
                }
              ],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "sessionId": "live-session", "requestId": "live-request", "turnNumber": 12 }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": [],
          "guardianProjectUpdates": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_stale_snapshot_only",
              "workDone": 6
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_missing_validated_preturn_guardians_snapshot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProjectValidation_StartCannotReuseCompletedProjectKey()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_reused_completed",
                "projectType": "abode_expansion",
                "projectTier": "major",
                "projectMode": "internal",
                "projectName": "Старый завершённый проект",
                "finalState": "Completed",
                "completionTurn": 11,
                "outcome": "Уже завершён."
              }
            }
          ],
          "temporaryProjectModifiers": [],
          "startGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_reused_completed",
                "projectType": "abode_fortification",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Новая попытка с тем же ключом",
                "activeState": "Should fail",
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

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_reused_completed_key_validation.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_reused_completed",
                    "projectType": "abode_expansion",
                    "projectTier": "major",
                    "projectMode": "internal",
                    "projectName": "Старый завершённый проект",
                    "finalState": "Completed",
                    "completionTurn": 11,
                    "outcome": "Уже завершён."
                  }
                }
              ],
              "temporaryProjectModifiers": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_start_duplicate_completed_project_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".startGuardianProjects[0].project.projectId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProjectValidation_DuplicateProjectKeyAcrossActiveAndCompletedIsRejected()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_collision_same_guardian",
                "projectType": "abode_expansion",
                "projectTier": "major",
                "projectMode": "internal",
                "projectName": "Активный проект с конфликтующим ключом",
                "activeState": "В работе",
                "totalWork": 10,
                "workDone": 3,
                "totalStages": 2,
                "currentStage": 1,
                "pressure": 1,
                "stability": 95
              }
            }
          ],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_collision_same_guardian",
                "projectType": "abode_expansion",
                "projectTier": "major",
                "projectMode": "internal",
                "projectName": "Завершённый проект с тем же ключом",
                "finalState": "Completed",
                "completionTurn": 11,
                "outcome": "Исторический collision должен быть запрещён."
              }
            }
          ],
          "temporaryProjectModifiers": []
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_duplicate_project_key", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".completedProjects[0].project.projectId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerEventValidation_CompletionStrikeDoesNotResolveAgainstActiveOffensiveProject()
    {
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 55, "tier": "Могущественная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "evt_active_completion_strike",
              "guardianId": "guardian_beta",
              "delta": -2,
              "reasonType": "rival_strike",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_active_only_strike",
              "title": "Нельзя резолвить completion strike к active project",
              "summary": "Source project ещё не завершён.",
              "visibility": "player_known",
              "relatedGuardianId": "guardian_alpha",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectGuardianId": "guardian_alpha",
                "projectId": "proj_active_only_strike",
                "projectName": "Активная интрига",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "finalState": "Completed",
                "attackerCurrentPower": 60,
                "targetCurrentPower": 48,
                "baseLoss": 3,
                "attackerBonus": 0,
                "baseTargetShield": 0,
                "fortificationBonus": 0,
                "counterOperationBonus": 0,
                "playerDefenseBonus": 0,
                "targetShield": 0,
                "targetLoss": 2,
                "pressureDelta": 1,
                "stabilityDamage": 1
              }
            }
          ]
        }
        """));

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_active_only_strike",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Активная интрига",
                "targetGuardianId": "guardian_beta",
                "activeState": "Still active",
                "totalWork": 16,
                "workDone": 12,
                "totalStages": 3,
                "currentStage": 2,
                "pressure": 5,
                "stability": 74
              }
            }
          ],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_unknown_source_project", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".guardianPowerEvents[0].audit.projectId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerEventValidation_CollidingProjectKeyDoesNotAuthorizeHistoricalSourceResolution()
    {
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 55, "tier": "Могущественная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """));

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_historical_collision",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Активная интрига с конфликтующим ключом",
                "targetGuardianId": "guardian_beta",
                "activeState": "Ещё не завершена",
                "totalWork": 10,
                "workDone": 7,
                "totalStages": 2,
                "currentStage": 1,
                "pressure": 1,
                "stability": 95
              }
            }
          ],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_historical_collision",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Завершённая интрига с тем же ключом",
                "targetGuardianId": "guardian_beta",
                "finalState": "Completed",
                "completionTurn": 11,
                "outcome": "Collision должен делать historical resolution ambiguous."
              }
            }
          ],
          "temporaryProjectModifiers": [],
          "guardianPowerEvents": [
            {
              "eventId": "evt_historical_collision",
              "guardianId": "guardian_beta",
              "delta": -2,
              "reasonType": "rival_strike",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_historical_collision",
              "title": "Collision не должен резолвиться",
              "summary": "Historical power event должен fail-closed при неоднозначном canonical source.",
              "visibility": "player_known",
              "relatedGuardianId": "guardian_alpha",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "projectGuardianId": "guardian_alpha",
                "projectId": "proj_historical_collision",
                "projectName": "Ложный снапшот",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "finalState": "Completed",
                "targetLoss": 2,
                "pressureDelta": 1,
                "stabilityDamage": 1,
                "targetAttitudeTier": "hostile",
                "targetAttitudeScore": -70,
                "preferredHostileTarget": true
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_unknown_source_project", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".guardianPowerEvents[0].audit.projectId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerJournalRepair_StaleButHashValidSnapshotDoesNotBackfillLegacyEntry()
    {
        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_stale_journal.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_stale_journal_source",
                    "projectType": "abode_expansion",
                    "projectTier": "major",
                    "projectMode": "internal",
                    "projectName": "Hash-valid stale journal source",
                    "finalState": "Completed",
                    "completionTurn": 11,
                    "outcome": "Не должен использоваться для backfill."
                  }
                }
              ],
              "temporaryProjectModifiers": []
            }
            """);

        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "sessionId": "live-session", "requestId": "live-request", "turnNumber": 12 }
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
              "entryId": "legacy_stale_context_entry",
              "eventId": "legacy_stale_context_event",
              "turn": 11,
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "delta": 2,
              "reasonType": "project_completion",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "proj_stale_journal_source",
              "title": "Stale snapshot must not backfill",
              "summary": "Append path не должен canonicalize'ить legacy journal через stale-but-hash-valid snapshot context.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {}
            }
          ]
        }
        """);

        await GuardianPowerEventState.AppendJournalEntriesAsync(_fs, new[]
        {
            GuardianPowerEventState.BuildEvent(
                "evt_append_after_stale_snapshot",
                "guardian_alpha",
                1,
                "offering",
                "offer_guardian",
                "offering_cycle",
                "Тестовое приношение",
                "Append trigger with stale-but-hash-valid snapshot context.",
                new JsonObject
                {
                    ["offeringType"] = "ink_feathers",
                    ["returnCycleId"] = "cycle_alpha",
                    ["baseDelta"] = 1,
                    ["finalDelta"] = 1,
                    ["inkFeathersOffered"] = 1,
                    ["capRemainingBefore"] = 4
                })
        });

        var journal = await ReadObjectAsync(GuardianPowerEventState.JournalPath);
        var legacyEntry = journal["entries"]!.AsArray().OfType<JsonObject>().Single(item =>
            string.Equals(item["eventId"]?.GetValue<string>(), "legacy_stale_context_event", StringComparison.OrdinalIgnoreCase));
        var audit = legacyEntry["audit"]!.AsObject();

        Assert.Null(audit["projectGuardianId"]);
        Assert.Null(audit["projectName"]);
        Assert.Null(audit["projectType"]);
        Assert.Null(audit["projectTier"]);
        Assert.Null(audit["finalState"]);
    }

    [Fact]
    public async Task GuardianCommandValidation_StaleButHashValidPreTurnGuardianSnapshotDoesNotAuthorizeUnknownGuardian()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [],
          "UpdateGuardians": [
            {
              "command": "updateReputation",
              "guardianId": "guardian_missing",
              "reputationChange": 5,
              "reason": "Stale snapshot should not authorize this guardian."
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "game_state/control/rollback_backups/game_state_meta_guardians_stale_context.json",
            """
            {
              "guardians": [
                {
                  "guardianId": "guardian_missing",
                  "canonicalName": "Фантом",
                  "nameVariants": { "default": "Фантом", "feminine": null, "masculine": null, "neutral": null },
                  "manifestation": {
                    "currentDisplayName": "Фантом",
                    "formFlexibility": "fixed",
                    "currentPresentationStyle": "neutral",
                    "currentPronouns": "они/их",
                    "appearanceDescription": "Несуществующий guardian."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 0, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "sessionId": "live-session", "requestId": "live-request", "turnNumber": 12 }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_commands_missing_validated_preturn_guardians_snapshot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianCommandValidation_CurrentGuardianStateAuthorizesCompleteQuestWithoutPreTurnSnapshot()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """{ "turnNumber": 17 }""");
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 80, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 35, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
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
            "relationshipData": { "currentReputation": 80, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 35, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
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
          },
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

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_non_create_unknown_guardian", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_complete_quest_unknown_quest_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianCommandValidation_ValidatedPreTurnSnapshotAuthorizesCommandOnlyGuardianViaRepairContext()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [],
          "UpdateGuardians": [
            {
              "command": "updateReputation",
              "guardianId": "guardian_snapshot",
              "reputationChange": 5,
              "reason": "Repair context should authorize validated pre-turn guardian state."
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "game_state/control/rollback_backups/game_state_meta_guardians_repair_context.json",
            """
            {
              "guardians": [
                {
                  "guardianId": "guardian_snapshot",
                  "canonicalName": "Фантом",
                  "nameVariants": { "default": "Фантом", "feminine": null, "masculine": null, "neutral": null },
                  "manifestation": {
                    "currentDisplayName": "Фантом",
                    "formFlexibility": "fixed",
                    "currentPresentationStyle": "neutral",
                    "currentPronouns": "они/их",
                    "appearanceDescription": "Восстановленный guardian."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 0, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "sessionId": "other-session", "requestId": "other-request", "turnNumber": 12 }
        """);
        await WriteRawAsync("game_state/control/validation_repair_request.json", """
        { "sessionId": "test-session", "requestId": "test-request", "turnNumber": 12 }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_non_create_unknown_guardian", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".UpdateGuardians[0].guardianId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProjectValidation_MalformedRepairRequestFallsBackToValidTurnRequestContext()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_repair_request_fallback.json",
            """
            {
              "activeProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_repair_fallback",
                    "projectType": "abode_expansion",
                    "projectTier": "major",
                    "projectMode": "internal",
                    "projectName": "Проект из validated pre-turn snapshot",
                    "activeState": "Ready for update",
                    "totalWork": 12,
                    "workDone": 4,
                    "totalStages": 2,
                    "currentStage": 1,
                    "pressure": 2,
                    "stability": 92
                  }
                }
              ],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await WriteRawAsync("game_state/control/validation_repair_request.json", """
        { "sessionId": "test-session", "requestId": "broken-request"
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": [],
          "guardianProjectUpdates": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_repair_fallback",
              "workDone": 6
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_project_update_unknown_project_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".guardianProjectUpdates[0].projectId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidationRepairReady_DiagnosticOnlyRepairRequestFailsClosedWithDedicatedIssue()
    {
        await WriteRawAsync("game_state/control/validation_repair_request.json", """
        {
          "sessionId": "",
          "requestId": "",
          "turnNumber": 0,
          "metadataDiagnosticOnly": true,
          "source": "repair",
          "detectedAtUtc": "2026-04-15T00:00:00Z",
          "revalidationAttempt": 1,
          "gmInstructions": "diagnostic-only metadata",
          "summaryGroups": [],
          "errors": []
        }
        """);
        await WriteRawAsync("game_state/control/validation_repair_ready.json", """
        {
          "sessionId": "",
          "requestId": "",
          "turnNumber": 0,
          "updatedAtUtc": "2026-04-15T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "repair_ready_against_diagnostic_only_request", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.FilePath, "game_state/control/validation_repair_ready.json", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "mismatched_repair_ready_context", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.FilePath, "game_state/control/validation_repair_ready.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidationRepairReady_LegacyRepairRequestWithoutDiagnosticFlagKeepsExactCopyContract()
    {
        await WriteRawAsync("game_state/control/validation_repair_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12,
          "source": "repair",
          "detectedAtUtc": "2026-04-15T00:00:00Z",
          "revalidationAttempt": 1,
          "gmInstructions": "legacy request without structured degraded flag",
          "summaryGroups": [],
          "errors": []
        }
        """);
        await WriteRawAsync("game_state/control/validation_repair_ready.json", """
        {
          "sessionId": "stale-session",
          "requestId": "stale-request",
          "turnNumber": 99,
          "updatedAtUtc": "2026-04-15T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "mismatched_repair_ready_context", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.FilePath, "game_state/control/validation_repair_ready.json", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "repair_ready_against_diagnostic_only_request", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.FilePath, "game_state/control/validation_repair_ready.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidationRepairReady_InvalidJsonUsesDegradedHintWhenRepairRequestMetadataIsDiagnosticOnly()
    {
        await WriteRawAsync("game_state/control/validation_repair_request.json", """
        {
          "sessionId": "",
          "requestId": "",
          "turnNumber": 0,
          "metadataDiagnosticOnly": true,
          "source": "repair",
          "detectedAtUtc": "2026-04-15T00:00:00Z",
          "revalidationAttempt": 1,
          "gmInstructions": "diagnostic-only metadata",
          "summaryGroups": [],
          "errors": []
        }
        """);
        await WriteRawAsync("game_state/control/validation_repair_ready.json", """
        {
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        var issue = Assert.Single(issues, candidate =>
            string.Equals(candidate.Code, "invalid_repair_ready_json", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.FilePath, "game_state/control/validation_repair_ready.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Не создавай validation_repair_ready.json по sentinel metadata", issue.RepairHint ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("скопируй в него точные sessionId/requestId/turnNumber", issue.RepairHint ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidationRepairReady_NonObjectJsonUsesDegradedHintWhenRepairRequestMetadataIsDiagnosticOnly()
    {
        await WriteRawAsync("game_state/control/validation_repair_request.json", """
        {
          "sessionId": "",
          "requestId": "",
          "turnNumber": 0,
          "metadataDiagnosticOnly": true,
          "source": "repair",
          "detectedAtUtc": "2026-04-15T00:00:00Z",
          "revalidationAttempt": 1,
          "gmInstructions": "diagnostic-only metadata",
          "summaryGroups": [],
          "errors": []
        }
        """);
        await WriteRawAsync("game_state/control/validation_repair_ready.json", """
        []
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        var issue = Assert.Single(issues, candidate =>
            string.Equals(candidate.Code, "invalid_repair_ready_json", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.FilePath, "game_state/control/validation_repair_ready.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Не создавай validation_repair_ready.json по sentinel metadata", issue.RepairHint ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("скопируй в него точные sessionId/requestId/turnNumber", issue.RepairHint ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidationRepairReady_InvalidJsonLegacyRepairRequestKeepsExactCopyHint()
    {
        await WriteRawAsync("game_state/control/validation_repair_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12,
          "source": "repair",
          "detectedAtUtc": "2026-04-15T00:00:00Z",
          "revalidationAttempt": 1,
          "gmInstructions": "legacy request without structured degraded flag",
          "summaryGroups": [],
          "errors": []
        }
        """);
        await WriteRawAsync("game_state/control/validation_repair_ready.json", """
        {
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        var issue = Assert.Single(issues, candidate =>
            string.Equals(candidate.Code, "invalid_repair_ready_json", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.FilePath, "game_state/control/validation_repair_ready.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("скопируй в него точные sessionId/requestId/turnNumber из validation_repair_request.json", issue.RepairHint ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Не создавай validation_repair_ready.json по sentinel metadata", issue.RepairHint ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GuardianProcessGachaValidation_UsesCurrentCompletedRelicForgingWhenPreTurnTrackerUnavailable()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """{ "turnNumber": 21 }""");
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
                "baseRarity": "Rare",
                "abodePowerBonusSteps": 0,
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

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
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
                  "guardianRarityCeilingBonusSteps": 1
                },
                "effectState": {
                  "gachaUsesGranted": 1,
                  "gachaUsesSpent": 0
                }
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_process_gacha_bonus_audit_forge_steps_exceeded", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_non_create_unknown_guardian", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProcessGachaValidation_DoesNotUseSnapshotOnlyCompletedRelicForgingWhenCurrentTrackerLacksSourceProject()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "sessionId": "test-session", "requestId": "gacha-current-tracker", "turnNumber": 21 }
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
                "baseRarity": "Rare",
                "abodePowerBonusSteps": 0,
                "relicForgingBonusSteps": 1,
                "finalRarity": "Epic",
                "sourceProjectId": "forge_removed"
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

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_snapshot_only_forge.json",
            """
            {
              "completedProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "forge_removed",
                    "projectType": "relic_forging",
                    "projectTier": "grand",
                    "projectMode": "supportive",
                    "projectName": "Старая ковка",
                    "finalState": "Completed",
                    "projectOutcomeAudit": {
                      "guardianRarityCeilingBonusSteps": 1
                    },
                    "effectState": {
                      "gachaUsesGranted": 1,
                      "gachaUsesSpent": 0
                    }
                  }
                }
              ],
              "activeProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_non_create_unknown_guardian", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProcessGachaValidation_FailsClosedOnCurrentTrackerAuthorityBeforeForgeExceeded()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "sessionId": "test-session", "requestId": "gacha-current-tracker-authority", "turnNumber": 21 }
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
                "appearanceDescription": "Current tracker authority must fail before forge exceeded."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 60, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "tradeInventory": { "items": [] },
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
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
              "appearanceDescription": "Current tracker authority must fail before forge exceeded."
            },
            "manifestationHistory": [],
            "domain": "Tide",
            "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
            "personalityProfile": {
              "archetype": "Tide Keeper",
              "speechPattern": "Measured and tidal",
              "coreValues": [ "balance", "memory", "patience" ]
            },
            "relationshipData": { "currentReputation": 60, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "tradeInventory": { "items": [] },
            "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
            "gachaSystem": { "chargesPerReturn": 2, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          },
          "UpdateGuardians": [
            {
              "command": "processGacha",
              "guardianId": "guardian_alpha",
              "inkFeathersSpent": 50,
              "gachaBonusAudit": {
                "baseRarity": "Rare",
                "abodePowerBonusSteps": 0,
                "relicForgingBonusSteps": 2,
                "finalRarity": "Epic",
                "sourceProjectId": "forge_invalid"
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

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_gacha_current_tracker_authority_invalid.json",
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
                    "appearanceDescription": "Validated guardian baseline exists."
                  },
                  "manifestationHistory": [],
                  "domain": "Tide",
                  "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
                  "personalityProfile": {
                    "archetype": "Tide Keeper",
                    "speechPattern": "Measured and tidal",
                    "coreValues": [ "balance", "memory", "patience" ]
                  },
                  "relationshipData": { "currentReputation": 60, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "tradeInventory": { "items": [] },
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 2, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_tracker_gacha_current_tracker_authority_invalid.json",
            """
            {
              "activeProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_gacha_invalid_alpha",
                    "projectType": "abode_expansion",
                    "projectTier": "minor",
                    "projectMode": "internal",
                    "projectName": "Первый конфликтующий проект",
                    "activeState": "Tracking invalid authority",
                    "totalWork": 10,
                    "workDone": 1,
                    "totalStages": 2,
                    "currentStage": 1,
                    "pressure": 0,
                    "stability": 10,
                    "startedTurn": 4
                  }
                },
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_gacha_invalid_beta",
                    "projectType": "abode_fortification",
                    "projectTier": "minor",
                    "projectMode": "internal",
                    "projectName": "Второй конфликтующий проект",
                    "activeState": "Duplicate guardian slot must invalidate gacha tracker authority",
                    "totalWork": 8,
                    "workDone": 0,
                    "totalStages": 2,
                    "currentStage": 0,
                    "pressure": 0,
                    "stability": 10,
                    "startedTurn": 4
                  }
                }
              ],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_process_gacha_bonus_audit_missing_current_tracker_authority", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual != null &&
            issue.Actual.Contains("semantically invalid", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_process_gacha_bonus_audit_forge_steps_exceeded", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_CurrentCompletedProjectsCannotResurrectConsumedRelicForgingBonus()
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
                "appearanceDescription": "Current tracker must not resurrect consumed forging bonus."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 60, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 2, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "forge_spent",
                "projectType": "relic_forging",
                "projectTier": "grand",
                "projectMode": "supportive",
                "projectName": "Подменённая ковка",
                "finalState": "Completed",
                "projectOutcomeAudit": {
                  "guardianRarityCeilingBonusSteps": 1
                },
                "effectState": {
                  "gachaUsesGranted": 1,
                  "gachaUsesSpent": 0
                }
              }
            }
          ],
          "temporaryProjectModifiers": []
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_current_completed_projects_cannot_resurrect_bonus.json",
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
                    "appearanceDescription": "Validated guardian baseline exists."
                  },
                  "manifestationHistory": [],
                  "domain": "Tide",
                  "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
                  "personalityProfile": {
                    "archetype": "Tide Keeper",
                    "speechPattern": "Measured and tidal",
                    "coreValues": [ "balance", "memory", "patience" ]
                  },
                  "relationshipData": { "currentReputation": 60, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 2, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_tracker_current_completed_projects_cannot_resurrect_bonus.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "forge_spent",
                    "projectType": "relic_forging",
                    "projectTier": "grand",
                    "projectMode": "supportive",
                    "projectName": "Исходная ковка",
                    "finalState": "Completed",
                    "projectOutcomeAudit": {
                      "guardianRarityCeilingBonusSteps": 1
                    },
                    "effectState": {
                      "gachaUsesGranted": 1,
                      "gachaUsesSpent": 1
                    }
                  }
                }
              ],
              "temporaryProjectModifiers": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".completedProjects", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_CurrentTrackerMaterializedStateFailsClosedWhenCurrentAuthorityIsSemanticallyInvalid()
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
                "appearanceDescription": "Tracker materialized-state validation must fail closed when current authority input is semantically invalid."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 60, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 2, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_for_materialized_current_tracker_authority_failure.json",
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
                    "appearanceDescription": "Validated guardian baseline exists."
                  },
                  "manifestationHistory": [],
                  "domain": "Tide",
                  "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
                  "personalityProfile": {
                    "archetype": "Tide Keeper",
                    "speechPattern": "Measured and tidal",
                    "coreValues": [ "balance", "memory", "patience" ]
                  },
                  "relationshipData": { "currentReputation": 60, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 2, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_tracker_for_materialized_current_tracker_authority_failure.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": [],
          "guardianProjectUpdates": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "missing_project",
              "activeState": "Escalating"
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_materialized_state_missing_current_tracker_authority", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual != null &&
            issue.Actual.Contains("semantically invalid", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_project_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_CurrentTemporaryProjectModifiersCannotMaterializeOutsideKernelAuthority()
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
                "appearanceDescription": "Current tracker modifiers must not become authority."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 60, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 2, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": [
            {
              "modifierId": "temp_reborn_bonus",
              "guardianId": "guardian_alpha",
              "modifierType": "next_internal_project_starting_pressure",
              "value": 2,
              "remainingApplications": 1
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_current_modifiers_cannot_materialize_outside_authority.json",
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
                    "appearanceDescription": "Validated guardian baseline exists."
                  },
                  "manifestationHistory": [],
                  "domain": "Tide",
                  "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
                  "personalityProfile": {
                    "archetype": "Tide Keeper",
                    "speechPattern": "Measured and tidal",
                    "coreValues": [ "balance", "memory", "patience" ]
                  },
                  "relationshipData": { "currentReputation": 60, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 2, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_tracker_current_modifiers_cannot_materialize_outside_authority.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".temporaryProjectModifiers", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_DuplicateCurrentTemporaryProjectModifiersFailClosedBeforeMaterializedStateComparison()
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
                "appearanceDescription": "Duplicate modifiers must invalidate current tracker authority."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 60, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 2, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_duplicate_current_modifier_authority_failure.json",
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
                    "appearanceDescription": "Validated guardian baseline exists."
                  },
                  "manifestationHistory": [],
                  "domain": "Tide",
                  "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
                  "personalityProfile": {
                    "archetype": "Tide Keeper",
                    "speechPattern": "Measured and tidal",
                    "coreValues": [ "balance", "memory", "patience" ]
                  },
                  "relationshipData": { "currentReputation": 60, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 2, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_tracker_duplicate_current_modifier_authority_failure.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": [
            {
              "modifierId": "tmp_guardian_alpha_dup",
              "guardianId": "guardian_alpha",
              "modifierType": "next_internal_project_starting_pressure",
              "value": 2,
              "remainingApplications": 1
            },
            {
              "modifierId": "tmp_guardian_alpha_dup",
              "guardianId": "guardian_alpha",
              "modifierType": "next_internal_project_starting_pressure",
              "value": 3,
              "remainingApplications": 1
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_materialized_state_missing_current_tracker_authority", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual != null &&
            issue.Actual.Contains("semantically invalid", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_project_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".temporaryProjectModifiers", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_DuplicateValidatedPreTurnTemporaryProjectModifiersFailClosedBeforeCurrentTrackerAuthorityBuild()
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
                "appearanceDescription": "Validated pre-turn modifier collisions must break authority."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 60, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 2, "chargesUsedThisReturn": 0, "gachaHistory": [] }
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

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_duplicate_validated_modifier_authority_failure.json",
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
                    "appearanceDescription": "Validated guardian baseline exists."
                  },
                  "manifestationHistory": [],
                  "domain": "Tide",
                  "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
                  "personalityProfile": {
                    "archetype": "Tide Keeper",
                    "speechPattern": "Measured and tidal",
                    "coreValues": [ "balance", "memory", "patience" ]
                  },
                  "relationshipData": { "currentReputation": 60, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 2, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_tracker_duplicate_validated_modifier_authority_failure.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [],
              "temporaryProjectModifiers": [
                {
                  "modifierId": "tmp_guardian_alpha_dup",
                  "guardianId": "guardian_alpha",
                  "modifierType": "next_internal_project_starting_pressure",
                  "value": 2,
                  "remainingApplications": 1
                },
                {
                  "modifierId": "tmp_guardian_alpha_dup",
                  "guardianId": "guardian_alpha",
                  "modifierType": "next_internal_project_starting_pressure",
                  "value": 3,
                  "remainingApplications": 1
                }
              ]
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_invalid_validated_preturn_tracker_snapshot", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual != null &&
            issue.Actual.Contains("validated pre-turn guardian project tracker baseline is semantically invalid", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_project_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".temporaryProjectModifiers", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianCommandValidation_FutureOrInvalidCreateDoesNotAuthorizeCommands()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [],
          "UpdateGuardians": [
            {
              "command": "updateReputation",
              "guardianId": "guardian_invalid",
              "reputationChange": 5,
              "reason": "Команда не должна ссылаться на guardian, который будет создан только позже."
            },
            {
              "command": "create",
              "data": {
                "guardianId": "guardian_invalid"
              }
            },
            {
              "command": "updateReputation",
              "guardianId": "guardian_invalid",
              "reputationChange": 3,
              "reason": "Невалидный create не должен авторизовать последующие команды."
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_non_create_unknown_guardian", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".UpdateGuardians[0].guardianId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_non_create_unknown_guardian", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".UpdateGuardians[2].guardianId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProjectValidation_ValidatedPreTurnGuardiansAuthorizeCommandOnlyPoliticalLifecycleAndPreserveRelationshipRules()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": []
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_snapshot_only_political.json",
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
                    "appearanceDescription": "Политический guardian из validated pre-turn snapshot."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 80, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [
                    {
                      "targetGuardianId": "guardian_beta",
                      "attitudeScore": 72
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
                    "appearanceDescription": "Цель политического проекта из validated pre-turn snapshot."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 15, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 32, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
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
          "temporaryProjectModifiers": [],
          "startGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_snapshot_only",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Интрига из validated pre-turn snapshot",
                "targetGuardianId": "guardian_beta",
                "activeState": "Planning",
                "totalWork": 8,
                "workDone": 0,
                "totalStages": 2,
                "currentStage": 0,
                "pressure": 2,
                "stability": 88
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_project_unknown_guardian_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".startGuardianProjects[0].guardianId", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_project_unknown_guardian_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".startGuardianProjects[0].project.targetGuardianId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_missing_betrayal_reason", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".startGuardianProjects[0].project.betrayalReason", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerValidation_ValidatedPreTurnSnapshotAuthorizesCommandOnlyPowerEvent()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [],
          "guardianPowerEvents": [
            {
              "eventId": "evt_snapshot_power",
              "guardianId": "guardian_snapshot",
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_snapshot",
              "title": "Validated pre-turn guardian должен авторизовать power event",
              "summary": "Power validation должна видеть canonical guardian baseline из validated pre-turn snapshot.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "offeringType": "soul_relic",
                "returnCycleId": "cycle_snapshot",
                "baseDelta": 2,
                "finalDelta": 2,
                "relicId": "relic_snapshot",
                "relicName": "Снимочный реликт",
                "relicRarity": "Rare"
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_snapshot_only_power_event.json",
            """
            {
              "guardians": [
                {
                  "guardianId": "guardian_snapshot",
                  "canonicalName": "Снимок",
                  "nameVariants": { "default": "Снимок", "feminine": null, "masculine": null, "neutral": null },
                  "manifestation": {
                    "currentDisplayName": "Снимок",
                    "formFlexibility": "fixed",
                    "currentPresentationStyle": "neutral",
                    "currentPronouns": "они/их",
                    "appearanceDescription": "Только в validated pre-turn snapshot."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 0, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 20, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_project_unknown_guardian_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".guardianPowerEvents[0].guardianId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerValidation_RawCreateOnlyGuardianDoesNotAuthorizePowerEventOrJournal()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [],
          "UpdateGuardians": [
            {
              "command": "create",
              "data": {
                "guardianId": "guardian_ephemeral",
                "canonicalName": "Эфемер",
                "nameVariants": { "default": "Эфемер", "feminine": null, "masculine": "Эфемер", "neutral": null },
                "manifestation": {
                  "currentDisplayName": "Эфемер",
                  "formFlexibility": "fixed",
                  "currentPresentationStyle": "masculine",
                  "currentPronouns": "он/его",
                  "appearanceDescription": "Существует только в raw create."
                },
                "manifestationHistory": [],
                "domain": "Ash",
                "abode": { "abodeId": "abode_ephemeral", "title": "Обитель Эфемера" },
                "relationshipData": { "currentReputation": 5, "reputationHistory": [], "lastInteraction": null },
                "abodePower": { "currentPower": 18, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                "guardianRelationships": [],
                "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
              }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "evt_ephemeral_power",
              "guardianId": "guardian_ephemeral",
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_ephemeral",
              "title": "Raw create не должен авторизовать power event",
              "summary": "Power event должен ссылаться только на current guardian state.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "offeringType": "soul_relic",
                "returnCycleId": "cycle_ephemeral",
                "baseDelta": 2,
                "finalDelta": 2,
                "relicId": "relic_ephemeral",
                "relicName": "Тестовый реликт",
                "relicRarity": "Rare"
              }
            }
          ]
        }
        """);
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_raw_create_only_power.json",
            """
            {
              "guardians": []
            }
            """);
        await EnsureReadableCurrentGuardianProjectTrackerAsync();

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "journal_ephemeral_guardian",
              "eventId": "evt_journal_ephemeral",
              "turn": 12,
              "guardianId": "guardian_ephemeral",
              "guardianName": "Эфемер",
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_ephemeral_journal",
              "title": "Raw create не должен авторизовать journal entry",
              "summary": "Journal validation тоже должна смотреть только на current guardian state.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "offeringType": "soul_relic",
                "returnCycleId": "cycle_ephemeral",
                "baseDelta": 2,
                "finalDelta": 2,
                "relicId": "relic_ephemeral",
                "relicName": "Тестовый реликт",
                "relicRarity": "Rare"
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_unknown_guardian_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".guardianPowerEvents[0].guardianId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_unknown_guardian_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".entries[0].guardianId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProjectValidation_StaleRepairRequestFallsBackToValidTurnRequestContext()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_stale_repair_context.json",
            """
            {
              "activeProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_stale_repair_fallback",
                    "projectType": "abode_expansion",
                    "projectTier": "major",
                    "projectMode": "internal",
                    "projectName": "Проект из validated pre-turn snapshot",
                    "activeState": "Ready for update",
                    "totalWork": 12,
                    "workDone": 4,
                    "totalStages": 2,
                    "currentStage": 1,
                    "pressure": 2,
                    "stability": 92
                  }
                }
              ],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "sessionId": "test-session", "requestId": "test-request", "turnNumber": 12 }
        """);
        await WriteRawAsync("game_state/control/validation_repair_request.json", """
        { "sessionId": "stale-session", "requestId": "stale-request", "turnNumber": 12 }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": [],
          "guardianProjectUpdates": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_stale_repair_fallback",
              "workDone": 5
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_project_update_unknown_project_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".guardianProjectUpdates[0].projectId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProjectValidation_SameTurnValidCreateAuthorizesProjectStartAndNormalizerMaterializesProject()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """{ "turnNumber": 18 }""");
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [],
          "UpdateGuardians": [
            {
              "command": "create",
              "data": {
                "guardianId": "guardian_new",
                "canonicalName": "Нева",
                "nameVariants": { "default": "Нева", "feminine": "Нева", "masculine": "Нева", "neutral": "Нева" },
                "manifestation": {
                  "currentDisplayName": "Нева",
                  "formFlexibility": "selective",
                  "currentPresentationStyle": "feminine",
                  "currentPronouns": "она/её",
                  "appearanceDescription": "Новая хранительница текущего хода."
                },
                "manifestationHistory": [],
                "domain": "Tide",
                "abode": { "abodeId": "abode_new", "title": "Приливный предел" },
                "personalityProfile": {
                  "archetype": "Tide Keeper",
                  "speechPattern": "Measured and tidal",
                  "coreValues": [ "balance", "memory", "patience" ]
                },
                "relationshipData": { "currentReputation": 25, "reputationHistory": [], "lastInteraction": null },
                "abodePower": { "currentPower": 18, "tier": "Угасающая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                "guardianRelationships": [],
                "mood": {
                  "current": "focused",
                  "intensity": 40,
                  "reason": "A newly formed purpose.",
                  "since": 18
                },
                "loreFragments": [
                  { "fragmentId": "guardian_new_lore_1", "category": "personal_history", "title": "Вход в прилив", "content": null, "requiredReputation": 0 },
                  { "fragmentId": "guardian_new_lore_2", "category": "cosmic_secret", "title": "Тайна глубины", "content": null, "requiredReputation": 50 },
                  { "fragmentId": "guardian_new_lore_3", "category": "domain_mastery", "title": "Узел течений", "content": null, "requiredReputation": 130 },
                  { "fragmentId": "guardian_new_lore_4", "category": "lost_world", "title": "Затонувший берег", "content": null, "requiredReputation": 230 },
                  { "fragmentId": "guardian_new_lore_5", "category": "other_guardians", "title": "Имена в пене", "content": null, "requiredReputation": 0 },
                  { "fragmentId": "guardian_new_lore_6", "category": "soul_mechanics", "title": "Память соли", "content": null, "requiredReputation": 50 },
                  { "fragmentId": "guardian_new_lore_7", "category": "personal_history", "title": "Возвращение волны", "content": null, "requiredReputation": 130 }
                ],
                "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
              }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": [],
          "startGuardianProjects": [
            {
              "guardianId": "guardian_new",
              "project": {
                "projectId": "proj_new",
                "projectType": "abode_expansion",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Разметить новую обитель",
                "activeState": "Planning",
                "totalWork": 6,
                "workDone": 0,
                "totalStages": 2,
                "currentStage": 0,
                "pressure": 1,
                "stability": 90
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_project_unknown_guardian_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".startGuardianProjects[0].guardianId", StringComparison.OrdinalIgnoreCase));

        await _fs.WriteFileAtomicAsync("test_backups/preturn_tracker_same_turn_created_guardian_project.json", """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await WriteCurrentGuardiansNormalizerBackupAsync("test_backups/preturn_guardians_same_turn_created_guardian_project_normalizer.json");
        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>
        {
            [GuardianProjectState.TrackerPath] = "test_backups/preturn_tracker_same_turn_created_guardian_project.json",
            ["game_state/meta/guardians.json"] = "test_backups/preturn_guardians_same_turn_created_guardian_project_normalizer.json"
        });

        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        Assert.NotNull(guardiansJson);
        using var guardiansDoc = JsonDocument.Parse(guardiansJson!);
        Assert.Contains(
            guardiansDoc.RootElement.GetProperty("guardians").EnumerateArray(),
            guardian => string.Equals(guardian.GetProperty("guardianId").GetString(), "guardian_new", StringComparison.OrdinalIgnoreCase));

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        Assert.NotNull(trackerJson);
        using var trackerDoc = JsonDocument.Parse(trackerJson!);
        Assert.Contains(
            trackerDoc.RootElement.GetProperty("activeProjects").EnumerateArray(),
            entry =>
                string.Equals(entry.GetProperty("guardianId").GetString(), "guardian_new", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.GetProperty("project").GetProperty("projectId").GetString(), "proj_new", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianPowerValidation_SameTurnValidCreateAuthorizesPowerEventAndNormalizerAppliesEvent()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """{ "turnNumber": 19 }""");
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [],
          "UpdateGuardians": [
            {
              "command": "create",
              "data": {
                "guardianId": "guardian_new",
                "canonicalName": "Нева",
                "nameVariants": { "default": "Нева", "feminine": "Нева", "masculine": "Нева", "neutral": "Нева" },
                "manifestation": {
                  "currentDisplayName": "Нева",
                  "formFlexibility": "selective",
                  "currentPresentationStyle": "feminine",
                  "currentPronouns": "она/её",
                  "appearanceDescription": "Новая хранительница текущего хода."
                },
                "manifestationHistory": [],
                "domain": "Tide",
                "abode": { "abodeId": "abode_new", "title": "Приливный предел" },
                "personalityProfile": {
                  "archetype": "Tide Keeper",
                  "speechPattern": "Measured and tidal",
                  "coreValues": [ "balance", "memory", "patience" ]
                },
                "relationshipData": { "currentReputation": 25, "reputationHistory": [], "lastInteraction": null },
                "abodePower": { "currentPower": 18, "tier": "Угасающая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                "guardianRelationships": [],
                "mood": {
                  "current": "focused",
                  "intensity": 40,
                  "reason": "A newly formed purpose.",
                  "since": 19
                },
                "loreFragments": [
                  { "fragmentId": "guardian_new_lore_1", "category": "personal_history", "title": "Вход в прилив", "content": null, "requiredReputation": 0 },
                  { "fragmentId": "guardian_new_lore_2", "category": "cosmic_secret", "title": "Тайна глубины", "content": null, "requiredReputation": 50 },
                  { "fragmentId": "guardian_new_lore_3", "category": "domain_mastery", "title": "Узел течений", "content": null, "requiredReputation": 130 },
                  { "fragmentId": "guardian_new_lore_4", "category": "lost_world", "title": "Затонувший берег", "content": null, "requiredReputation": 230 },
                  { "fragmentId": "guardian_new_lore_5", "category": "other_guardians", "title": "Имена в пене", "content": null, "requiredReputation": 0 },
                  { "fragmentId": "guardian_new_lore_6", "category": "soul_mechanics", "title": "Память соли", "content": null, "requiredReputation": 50 },
                  { "fragmentId": "guardian_new_lore_7", "category": "personal_history", "title": "Возвращение волны", "content": null, "requiredReputation": 130 }
                ],
                "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
              }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "evt_same_turn_created",
              "guardianId": "guardian_new",
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_same_turn_created",
              "title": "Новая хранительница принимает дар",
              "summary": "Same-turn create должен разрешать power event.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "offeringType": "soul_relic",
                "returnCycleId": "cycle_same_turn_created",
                "baseDelta": 2,
                "finalDelta": 2,
                "relicId": "relic_same_turn_created",
                "relicName": "Приливный осколок",
                "relicRarity": "Rare"
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_project_unknown_guardian_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".guardianPowerEvents[0].guardianId", StringComparison.OrdinalIgnoreCase));

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync();

        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        Assert.NotNull(guardiansJson);
        using var guardiansDoc = JsonDocument.Parse(guardiansJson!);
        var guardianPower = guardiansDoc.RootElement
            .GetProperty("guardians")
            .EnumerateArray()
            .First(guardian => string.Equals(guardian.GetProperty("guardianId").GetString(), "guardian_new", StringComparison.OrdinalIgnoreCase))
            .GetProperty("abodePower")
            .GetProperty("currentPower")
            .GetInt32();
        Assert.Equal(20, guardianPower);

        var journalJson = await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath);
        Assert.NotNull(journalJson);
        using var journalDoc = JsonDocument.Parse(journalJson!);
        Assert.Contains(
            journalDoc.RootElement.GetProperty("entries").EnumerateArray(),
            entry => string.Equals(entry.GetProperty("eventId").GetString(), "evt_same_turn_created", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianProjectValidation_TurnNumberOnlyRequestContextDoesNotAuthorizePreTurnSnapshot()
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
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 90, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_projects_turn_number_only_context.json",
            """
            {
              "activeProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_turn_number_only",
                    "projectType": "abode_expansion",
                    "projectTier": "major",
                    "projectMode": "internal",
                    "projectName": "Проект только из snapshot",
                    "activeState": "Ready for update",
                    "totalWork": 12,
                    "workDone": 4,
                    "totalStages": 2,
                    "currentStage": 1,
                    "pressure": 2,
                    "stability": 92
                  }
                }
              ],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await _fs.WriteFileAtomicAsync("input/turn_request.json", """{ "turnNumber": 12 }""");

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": [],
          "guardianProjectUpdates": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_turn_number_only",
              "workDone": 6
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_missing_validated_preturn_guardians_snapshot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianIdentityValidation_OrphanActiveGuardianDoesNotAuthorizeCommandsProjectsOrPower()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "sessionId": "test-session", "requestId": "orphan-active-guardian", "turnNumber": 20 }
        """);
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [],
          "activeGuardian": {
            "guardianId": "guardian_orphan",
            "canonicalName": "Эхо",
            "nameVariants": { "default": "Эхо", "feminine": null, "masculine": null, "neutral": "Эхо" },
            "manifestation": {
              "currentDisplayName": "Эхо",
              "formFlexibility": "fixed",
              "currentPresentationStyle": "neutral",
              "currentPronouns": "они/их",
              "appearanceDescription": "Сиротский activeGuardian."
            },
            "manifestationHistory": [],
            "domain": "Mist",
            "abode": { "abodeId": "abode_orphan", "title": "Предел Эха" },
            "relationshipData": { "currentReputation": 20, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 18, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
            "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          },
          "UpdateGuardians": [
            {
              "command": "processGacha",
              "guardianId": "guardian_orphan",
              "inkFeathersSpent": 25,
              "result": {
                "relicId": "relic_orphan",
                "name": "Призрачный осколок",
                "rarity": "Rare"
              }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "evt_orphan",
              "guardianId": "guardian_orphan",
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_orphan",
              "title": "Сиротский guardian не должен авторизовать power event",
              "summary": "Power event должен опираться только на guardians[].",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "offeringType": "soul_relic",
                "returnCycleId": "cycle_orphan",
                "baseDelta": 2,
                "finalDelta": 2,
                "relicId": "relic_orphan",
                "relicName": "Призрачный осколок",
                "relicRarity": "Rare"
              }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": [],
          "startGuardianProjects": [
            {
              "guardianId": "guardian_orphan",
              "project": {
                "projectId": "proj_orphan",
                "projectType": "abode_expansion",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Сиротский проект",
                "activeState": "Planning",
                "totalWork": 4,
                "workDone": 0,
                "totalStages": 1,
                "currentStage": 0,
                "pressure": 1,
                "stability": 95
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "active_guardian_missing_in_guardians_array", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_non_create_unknown_guardian", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".UpdateGuardians[0].guardianId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_unknown_guardian_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".guardianPowerEvents[0].guardianId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_unknown_guardian_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".startGuardianProjects[0].guardianId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianIdentityValidation_NameMatchDoesNotBypassStaleActiveGuardianId()
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
                "appearanceDescription": "Canonical guardian entry."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 75, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_stale",
            "canonicalName": "Азалия",
            "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Имя совпадает, но guardianId больше не каноничен."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": 75, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "active_guardian_missing_in_guardians_array", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianCreateValidation_DuplicateExistingGuardianIdIsRejectedAndIgnoredByNormalizer()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """{ "sessionId": "test-session", "requestId": "test-request", "turnNumber": 21 }""");
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "UpdateGuardians": [
            {
              "command": "create",
              "data": {
                "guardianId": "guardian_alpha",
                "canonicalName": "Нева",
                "nameVariants": { "default": "Нева", "feminine": "Нева", "masculine": "Нева", "neutral": "Нева" },
                "manifestation": {
                  "currentDisplayName": "Нева",
                  "formFlexibility": "selective",
                  "currentPresentationStyle": "feminine",
                  "currentPronouns": "она/её",
                  "appearanceDescription": "Дубликат create не должен overwrite существующего guardian."
                },
                "manifestationHistory": [],
                "domain": "Tide",
                "abode": { "abodeId": "abode_new", "title": "Приливный предел" },
                "personalityProfile": {
                  "archetype": "Tide Keeper",
                  "speechPattern": "Measured and tidal",
                  "coreValues": [ "balance", "memory", "patience" ]
                },
                "relationshipData": { "currentReputation": 25, "reputationHistory": [], "lastInteraction": null },
                "abodePower": { "currentPower": 18, "tier": "Угасающая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                "guardianRelationships": [],
                "mood": {
                  "current": "focused",
                  "intensity": 40,
                  "reason": "A newly formed purpose.",
                  "since": 21
                },
                "loreFragments": [
                  { "fragmentId": "guardian_new_lore_1", "category": "personal_history", "title": "Вход в прилив", "content": null, "requiredReputation": 0 },
                  { "fragmentId": "guardian_new_lore_2", "category": "cosmic_secret", "title": "Тайна глубины", "content": null, "requiredReputation": 50 },
                  { "fragmentId": "guardian_new_lore_3", "category": "domain_mastery", "title": "Узел течений", "content": null, "requiredReputation": 130 },
                  { "fragmentId": "guardian_new_lore_4", "category": "lost_world", "title": "Затонувший берег", "content": null, "requiredReputation": 230 },
                  { "fragmentId": "guardian_new_lore_5", "category": "other_guardians", "title": "Имена в пене", "content": null, "requiredReputation": 0 },
                  { "fragmentId": "guardian_new_lore_6", "category": "soul_mechanics", "title": "Память соли", "content": null, "requiredReputation": 50 },
                  { "fragmentId": "guardian_new_lore_7", "category": "personal_history", "title": "Возвращение волны", "content": null, "requiredReputation": 130 }
                ],
                "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_duplicate_existing_guardian.json",
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
                    "appearanceDescription": "Validated pre-turn guardian baseline."
                  },
                  "manifestationHistory": [],
                  "domain": "Tide",
                  "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
                  "personalityProfile": {
                    "archetype": "Tide Keeper",
                    "speechPattern": "Measured and tidal",
                    "coreValues": [ "balance", "memory", "patience" ]
                  },
                  "relationshipData": { "currentReputation": 75, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 48, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 2, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_create_duplicate_guardian_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianCreateValidation_DuplicateSameTurnCreateIsRejectedAndOnlyFirstGuardianMaterializes()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """{ "turnNumber": 22 }""");
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [],
          "UpdateGuardians": [
            {
              "command": "create",
              "data": {
                "guardianId": "guardian_new",
                "canonicalName": "Первая Нева",
                "nameVariants": { "default": "Первая Нева", "feminine": "Первая Нева", "masculine": "Первая Нева", "neutral": "Первая Нева" },
                "manifestation": {
                  "currentDisplayName": "Первая Нева",
                  "formFlexibility": "selective",
                  "currentPresentationStyle": "feminine",
                  "currentPronouns": "она/её",
                  "appearanceDescription": "Первый create."
                },
                "manifestationHistory": [],
                "domain": "Tide",
                "abode": { "abodeId": "abode_new_1", "title": "Первый прилив" },
                "personalityProfile": {
                  "archetype": "Tide Keeper",
                  "speechPattern": "Measured and tidal",
                  "coreValues": [ "balance", "memory", "patience" ]
                },
                "relationshipData": { "currentReputation": 25, "reputationHistory": [], "lastInteraction": null },
                "abodePower": { "currentPower": 18, "tier": "Угасающая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                "guardianRelationships": [],
                "mood": {
                  "current": "focused",
                  "intensity": 40,
                  "reason": "A newly formed purpose.",
                  "since": 22
                },
                "loreFragments": [
                  { "fragmentId": "guardian_new_lore_1", "category": "personal_history", "title": "Вход в прилив", "content": null, "requiredReputation": 0 },
                  { "fragmentId": "guardian_new_lore_2", "category": "cosmic_secret", "title": "Тайна глубины", "content": null, "requiredReputation": 50 },
                  { "fragmentId": "guardian_new_lore_3", "category": "domain_mastery", "title": "Узел течений", "content": null, "requiredReputation": 130 },
                  { "fragmentId": "guardian_new_lore_4", "category": "lost_world", "title": "Затонувший берег", "content": null, "requiredReputation": 230 },
                  { "fragmentId": "guardian_new_lore_5", "category": "other_guardians", "title": "Имена в пене", "content": null, "requiredReputation": 0 },
                  { "fragmentId": "guardian_new_lore_6", "category": "soul_mechanics", "title": "Память соли", "content": null, "requiredReputation": 50 },
                  { "fragmentId": "guardian_new_lore_7", "category": "personal_history", "title": "Возвращение волны", "content": null, "requiredReputation": 130 }
                ],
                "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
              }
            },
            {
              "command": "create",
              "data": {
                "guardianId": "guardian_new",
                "canonicalName": "Вторая Нева",
                "nameVariants": { "default": "Вторая Нева", "feminine": "Вторая Нева", "masculine": "Вторая Нева", "neutral": "Вторая Нева" },
                "manifestation": {
                  "currentDisplayName": "Вторая Нева",
                  "formFlexibility": "selective",
                  "currentPresentationStyle": "feminine",
                  "currentPronouns": "она/её",
                  "appearanceDescription": "Второй duplicate create."
                },
                "manifestationHistory": [],
                "domain": "Tide",
                "abode": { "abodeId": "abode_new_2", "title": "Второй прилив" },
                "personalityProfile": {
                  "archetype": "Tide Keeper",
                  "speechPattern": "Measured and tidal",
                  "coreValues": [ "balance", "memory", "patience" ]
                },
                "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
                "abodePower": { "currentPower": 18, "tier": "Угасающая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                "guardianRelationships": [],
                "mood": {
                  "current": "focused",
                  "intensity": 45,
                  "reason": "A duplicate purpose.",
                  "since": 22
                },
                "loreFragments": [
                  { "fragmentId": "guardian_dup_lore_1", "category": "personal_history", "title": "Вход в прилив", "content": null, "requiredReputation": 0 },
                  { "fragmentId": "guardian_dup_lore_2", "category": "cosmic_secret", "title": "Тайна глубины", "content": null, "requiredReputation": 50 },
                  { "fragmentId": "guardian_dup_lore_3", "category": "domain_mastery", "title": "Узел течений", "content": null, "requiredReputation": 130 },
                  { "fragmentId": "guardian_dup_lore_4", "category": "lost_world", "title": "Затонувший берег", "content": null, "requiredReputation": 230 },
                  { "fragmentId": "guardian_dup_lore_5", "category": "other_guardians", "title": "Имена в пене", "content": null, "requiredReputation": 0 },
                  { "fragmentId": "guardian_dup_lore_6", "category": "soul_mechanics", "title": "Память соли", "content": null, "requiredReputation": 50 },
                  { "fragmentId": "guardian_dup_lore_7", "category": "personal_history", "title": "Возвращение волны", "content": null, "requiredReputation": 130 }
                ],
                "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_create_duplicate_guardian_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".UpdateGuardians[1].data.guardianId", StringComparison.OrdinalIgnoreCase));

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync();

        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        Assert.NotNull(guardiansJson);
        using var guardiansDoc = JsonDocument.Parse(guardiansJson!);
        var guardians = guardiansDoc.RootElement.GetProperty("guardians").EnumerateArray().ToList();
        Assert.Single(guardians);
        Assert.Equal("Первая Нева", guardians[0].GetProperty("canonicalName").GetString());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_RemovesOrphanActiveGuardianMirror()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """{ "turnNumber": 23 }""");
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [],
          "activeGuardian": {
            "guardianId": "guardian_orphan",
            "canonicalName": "Эхо",
            "manifestation": {
              "currentDisplayName": "Эхо",
              "formFlexibility": "fixed",
              "currentPresentationStyle": "neutral",
              "currentPronouns": "они/их",
              "appearanceDescription": "Сиротский mirror."
            }
          }
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync();

        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        Assert.NotNull(guardiansJson);
        using var guardiansDoc = JsonDocument.Parse(guardiansJson!);
        Assert.False(guardiansDoc.RootElement.TryGetProperty("activeGuardian", out _));
    }

    [Fact]
    public async Task GuardianCrossRefs_InvalidRawCreateOnlyGuardianDoesNotAuthorizeSoulQuest()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """{ "turnNumber": 24 }""");
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [],
          "UpdateGuardians": [
            {
              "command": "create",
              "data": {
                "guardianId": "guardian_ephemeral"
              }
            }
          ]
        }
        """);

        await WriteRawAsync("game_state/quests/soul_quests.json", """
        {
          "UpdateSoulQuests": [
            {
              "questId": "soul_quest_ephemeral",
              "guardianId": "guardian_ephemeral",
              "title": "Эфемерный зов",
              "description": "Невалидный raw create не должен авторизовать soul quest.",
              "objectives": [
                {
                  "description": "Пережить проверку.",
                  "status": "Active"
                }
              ],
              "status": "active",
              "progress": { "completed": 0, "total": 1 },
              "rewards": { "inkFeathers": 0, "enlightenmentExperience": 0 },
              "crossIncarnation": true
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "soul_quest_unknown_guardian_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".UpdateSoulQuests[0].guardianId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianCrossRefs_ValidatedPreTurnSnapshotAuthorizesThoughtJournalGuardian()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": []
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_snapshot_only_thought_journal.json",
            """
            {
              "guardians": [
                {
                  "guardianId": "guardian_snapshot",
                  "canonicalName": "Снимок",
                  "nameVariants": { "default": "Снимок", "feminine": null, "masculine": null, "neutral": null },
                  "manifestation": {
                    "currentDisplayName": "Снимок",
                    "formFlexibility": "fixed",
                    "currentPresentationStyle": "neutral",
                    "currentPronouns": "они/их",
                    "appearanceDescription": "Только в pre-turn snapshot."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 0, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 20, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await WriteRawAsync(GuardianThoughtJournalState.StatePath, """
        {
          "entries": [
            {
              "entryId": "gthought_snapshot_only",
              "guardianId": "guardian_snapshot",
              "turn": 12,
              "timestamp": "2026-03-27T10:00:00Z",
              "title": "Snapshot-only guardian",
              "summary": "Thought journal теперь должен видеть validated pre-turn guardian identity.",
              "intent": "Остаться авторизованным."
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_thought_unknown_guardian_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".entries[0].guardianId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianCrossRefs_CanonicalGuardiansWithoutSnapshotDoNotTriggerUnknownNonCreate()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_named",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "guardian_named",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Canonical guardian state without snapshot."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_named", "title": "Именованная обитель" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 25, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 18, "tier": "Угасающая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "mood": {
                "current": "focused",
                "intensity": 40,
                "reason": "Canonical guardian fixture.",
                "since": 22
              },
              "loreFragments": [
                { "fragmentId": "guardian_named_lore_1", "category": "personal_history", "title": "Именованный след", "content": null, "requiredReputation": 0 },
                { "fragmentId": "guardian_named_lore_2", "category": "cosmic_secret", "title": "Тайна течения", "content": null, "requiredReputation": 50 },
                { "fragmentId": "guardian_named_lore_3", "category": "domain_mastery", "title": "Узел имени", "content": null, "requiredReputation": 130 },
                { "fragmentId": "guardian_named_lore_4", "category": "lost_world", "title": "Имя на берегу", "content": null, "requiredReputation": 230 },
                { "fragmentId": "guardian_named_lore_5", "category": "other_guardians", "title": "Имена в пене", "content": null, "requiredReputation": 0 },
                { "fragmentId": "guardian_named_lore_6", "category": "soul_mechanics", "title": "Память соли", "content": null, "requiredReputation": 50 },
                { "fragmentId": "guardian_named_lore_7", "category": "personal_history", "title": "Возвращение имени", "content": null, "requiredReputation": 130 }
              ],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_unknown_non_create_target", StringComparison.OrdinalIgnoreCase));
    }

}

