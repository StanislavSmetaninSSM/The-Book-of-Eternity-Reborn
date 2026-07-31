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
    public async Task ForcedGuardianIncarnation_UsesCanonicalGuardianStateInsteadOfActiveMirror()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12
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
                "appearanceDescription": "Canonical forced-incarnation guardian."
              },
              "manifestationHistory": [],
              "abode": { "abodeId": "abode_alpha", "title": "Обитель Азалии" },
              "relationshipData": { "currentReputation": -10, "reputationHistory": [], "lastInteraction": null },
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
              "appearanceDescription": "Stale mirror with harsher hostility."
            },
            "manifestationHistory": [],
            "abode": { "abodeId": "abode_alpha", "title": "Обитель Азалии" },
            "relationshipData": { "currentReputation": -30, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          },
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_alpha"
          }
        }
        """);

        await WriteRawAsync("game_state/control/incarnation_trigger.json", """
        {
          "worldDescription": "Новый мир как наказание хранителя.",
          "characterDescription": "Душа просыпается в новом теле.",
          "circumstances": "Принудительное воплощение после провокации.",
          "source": "guardian_forced",
          "guardianId": "guardian_alpha",
          "severityBand": "harsh",
          "reason": "Провокация Хранителя",
          "provocationSummary": "Игрок сознательно оскорбил хранителя."
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_forced_incarnation_canonical_guardian.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["sessionId"] = "live-session";
        manifest["requestId"] = "live-request";
        manifest["sourceLabel"] = "обработки хода";
        manifest["playerAction"] = "[GUARDIAN_PROVOCATION: guardian_alpha] Я сознательно вывожу хранителя из себя.";
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

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "forced_incarnation_reputation_too_high", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "active_guardian_mirror_reputation_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_SystemGuardianAttractionUsesSnapshotPresetWhenLiveFileIsRetargeted()
    {
        const string guardiansJson = """
        {
          "guardians": [
            {
              "guardianId": "guardian_varak",
              "canonicalName": "Варак",
              "nameVariants": { "default": "Варак", "feminine": null, "masculine": "Варак", "neutral": null },
              "manifestation": {
                "currentDisplayName": "Варак",
                "formFlexibility": "fixed",
                "currentPresentationStyle": "masculine",
                "currentPronouns": "он/его",
                "appearanceDescription": "Wrong retargeted active guardian."
              },
              "manifestationHistory": [],
              "domain": "Forge",
              "abode": { "abodeId": "abode_varak", "title": "Горн Варака" },
              "personalityProfile": {
                "archetype": "Forge Warden",
                "speechPattern": "Short and iron",
                "coreValues": [ "discipline", "fire", "will" ]
              },
              "relationshipData": { "currentReputation": 5, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] },
              "sourcePreset": { "presetId": "varak", "displayName": "Варак", "version": "1.0", "library": "built_in" }
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_varak",
            "canonicalName": "Варак",
            "nameVariants": { "default": "Варак", "feminine": null, "masculine": "Варак", "neutral": null },
            "manifestation": {
              "currentDisplayName": "Варак",
              "formFlexibility": "fixed",
              "currentPresentationStyle": "masculine",
              "currentPronouns": "он/его",
              "appearanceDescription": "Wrong retargeted active guardian."
            },
            "manifestationHistory": [],
            "domain": "Forge",
            "abode": { "abodeId": "abode_varak", "title": "Горн Варака" },
            "personalityProfile": {
              "archetype": "Forge Warden",
              "speechPattern": "Short and iron",
              "coreValues": [ "discipline", "fire", "will" ]
            },
            "relationshipData": { "currentReputation": 5, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] },
            "sourcePreset": { "presetId": "varak", "displayName": "Варак", "version": "1.0", "library": "built_in" }
          }
        }
        """;
        const string preTurnAttractionJson = """
        {
          "mode": "system_guardian_attraction",
          "targetPresetId": "azalia",
          "targetPresetDisplayName": "Азалия",
          "targetPresetVersion": "1.0",
          "sourceLibrary": "built_in",
          "targetSummary": "Дипломатичная хранительница страсти, власти и преданности.",
          "renderedPromptPackage": "PresetId: azalia"
        }
        """;
        const string retargetedLiveAttractionJson = """
        {
          "mode": "system_guardian_attraction",
          "targetPresetId": "varak",
          "targetPresetDisplayName": "Варак",
          "targetPresetVersion": "1.0",
          "sourceLibrary": "built_in",
          "targetSummary": "Воинственный кузнец.",
          "renderedPromptPackage": "PresetId: varak"
        }
        """;

        await WriteRawAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson(guardiansJson));
        await WriteRawAsync(SystemGuardianLibraryService.AttractionRequestPath, retargetedLiveAttractionJson);
        await WriteRawAsync("ready/turn_complete.json", """{ "accepted": true }""");
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_system_attraction_retargeted.json",
            NormalizeGuardianStateJson(guardiansJson));
        await WritePreTurnTrackedFileAsync(
            SystemGuardianLibraryService.AttractionRequestPath,
            "test_backups/preturn_system_attraction_azalia.json",
            preTurnAttractionJson);
        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["playerAction"] = "[CHAOS_SEA_SYSTEM_GUARDIAN_ATTRACTION: azalia] Игрок зовёт Азалию.";
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            JsonSerializer.Serialize(manifest, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.SystemGuardianAttraction);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "client_owned_system_guardian_attraction_modified", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "system_guardian_attraction_target_mismatch", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Expected, "azalia", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actual, "varak", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_SystemGuardianAttractionWrongRealmSkipsClosureChecks()
    {
        const string attractionJson = """
        {
          "mode": "system_guardian_attraction",
          "targetPresetId": "azalia",
          "targetPresetDisplayName": "Азалия",
          "targetPresetVersion": "1.0",
          "sourceLibrary": "built_in",
          "targetSummary": "Дипломатичная хранительница страсти, власти и преданности.",
          "renderedPromptPackage": "PresetId: azalia"
        }
        """;
        const string shiningSoulJson = """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Shining Abode",
          "currentIncarnation": 1
        }
        """;

        await WriteRawAsync("game_state/meta/soul_state.json", shiningSoulJson);
        await WriteRawAsync(SystemGuardianLibraryService.AttractionRequestPath, attractionJson);
        await WriteRawAsync("ready/turn_complete.json", """{ "accepted": true }""");
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_system_attraction_shining.json",
            shiningSoulJson);
        await WritePreTurnTrackedFileAsync(
            SystemGuardianLibraryService.AttractionRequestPath,
            "test_backups/preturn_system_attraction_shining_wrong_realm.json",
            attractionJson);
        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        manifest["playerAction"] = "[CHAOS_SEA_SYSTEM_GUARDIAN_ATTRACTION: azalia] Игрок зовёт Азалию.";
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await WriteRawAsync(
            "game_state/control/pending_turn_snapshot.json",
            JsonSerializer.Serialize(manifest, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.SystemGuardianAttraction);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "system_guardian_attraction_wrong_realm", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "system_guardian_attraction_missing_active_guardian", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "system_guardian_attraction_target_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_SystemGuardianAttractionUsesAuthorityActiveGuardianInsteadOfRawMirror()
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
                "appearanceDescription": "Authority target for deterministic attraction."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] },
              "sourcePreset": { "presetId": "azalia", "displayName": "Азалия", "version": "1.0", "library": "built_in" }
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
                "appearanceDescription": "Raw active mirror drifts to the wrong guardian."
              },
              "manifestationHistory": [],
              "domain": "Forge",
              "abode": { "abodeId": "abode_beta", "title": "Горн Варака" },
              "personalityProfile": {
                "archetype": "Forge Warden",
                "speechPattern": "Short and iron",
                "coreValues": [ "discipline", "fire", "will" ]
              },
              "relationshipData": { "currentReputation": 5, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] },
              "sourcePreset": { "presetId": "varak", "displayName": "Варак", "version": "1.0", "library": "built_in" }
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_beta",
            "canonicalName": "Варак",
            "nameVariants": { "default": "Варак", "feminine": null, "masculine": "Варак", "neutral": null },
            "manifestation": {
              "currentDisplayName": "Варак",
              "formFlexibility": "fixed",
              "currentPresentationStyle": "masculine",
              "currentPronouns": "он/его",
              "appearanceDescription": "Raw mirror must not drive deterministic attraction target resolution."
            },
            "manifestationHistory": [],
            "domain": "Forge",
            "abode": { "abodeId": "abode_beta", "title": "Горн Варака" },
            "personalityProfile": {
              "archetype": "Forge Warden",
              "speechPattern": "Short and iron",
              "coreValues": [ "discipline", "fire", "will" ]
            },
            "relationshipData": { "currentReputation": 5, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] },
            "sourcePreset": { "presetId": "varak", "displayName": "Варак", "version": "1.0", "library": "built_in" }
          }
        }
        """);

        await WriteRawAsync(SystemGuardianLibraryService.AttractionRequestPath, """
        {
          "mode": "system_guardian_attraction",
          "targetPresetId": "azalia",
          "targetPresetDisplayName": "Азалия",
          "targetPresetVersion": "1.0",
          "sourceLibrary": "built_in",
          "targetSummary": "Дипломатичная хранительница страсти, власти и преданности.",
          "renderedPromptPackage": "PresetId: azalia"
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_system_guardian_attraction_authority_active_guardian.json",
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
                    "appearanceDescription": "Validated authority active guardian."
                  },
                  "manifestationHistory": [],
                  "domain": "Tide",
                  "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
                  "personalityProfile": {
                    "archetype": "Tide Keeper",
                    "speechPattern": "Measured and tidal",
                    "coreValues": [ "balance", "memory", "patience" ]
                  },
                  "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] },
                  "sourcePreset": { "presetId": "azalia", "displayName": "Азалия", "version": "1.0", "library": "built_in" }
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
                    "appearanceDescription": "Secondary guardian remains in authority baseline."
                  },
                  "manifestationHistory": [],
                  "domain": "Forge",
                  "abode": { "abodeId": "abode_beta", "title": "Горн Варака" },
                  "personalityProfile": {
                    "archetype": "Forge Warden",
                    "speechPattern": "Short and iron",
                    "coreValues": [ "discipline", "fire", "will" ]
                  },
                  "relationshipData": { "currentReputation": 5, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] },
                  "sourcePreset": { "presetId": "varak", "displayName": "Варак", "version": "1.0", "library": "built_in" }
                }
              ],
              "activeGuardian": {
                "guardianId": "guardian_alpha",
                "canonicalName": "Азалия",
                "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": "Азалия", "neutral": "Азалия" },
                "manifestation": {
                  "currentDisplayName": "Азалия",
                  "formFlexibility": "selective",
                  "currentPresentationStyle": "feminine",
                  "currentPronouns": "она/её",
                  "appearanceDescription": "Authority active guardian should win over raw mirror drift."
                },
                "manifestationHistory": [],
                "domain": "Tide",
                "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
                "personalityProfile": {
                  "archetype": "Tide Keeper",
                  "speechPattern": "Measured and tidal",
                  "coreValues": [ "balance", "memory", "patience" ]
                },
                "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                "guardianRelationships": [],
                "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] },
                "sourcePreset": { "presetId": "azalia", "displayName": "Азалия", "version": "1.0", "library": "built_in" }
              }
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.SystemGuardianAttraction);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "system_guardian_attraction_target_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_SystemGuardianAttractionDoesNotUseRawMirrorWhenAuthorityActiveGuardianIsMissing()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [],
          "activeGuardian": {
            "guardianId": "guardian_azalia",
            "canonicalName": "Азалия",
            "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Raw mirror must not satisfy deterministic attraction by itself."
            },
            "manifestationHistory": [],
            "domain": "Tide",
            "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
            "personalityProfile": {
              "archetype": "Tide Keeper",
              "speechPattern": "Measured and tidal",
              "coreValues": [ "balance", "memory", "patience" ]
            },
            "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] },
            "sourcePreset": { "presetId": "azalia", "displayName": "Азалия", "version": "1.0", "library": "built_in" }
          }
        }
        """);

        await WriteRawAsync(SystemGuardianLibraryService.AttractionRequestPath, """
        {
          "mode": "system_guardian_attraction",
          "targetPresetId": "azalia",
          "targetPresetDisplayName": "Азалия",
          "targetPresetVersion": "1.0",
          "sourceLibrary": "built_in",
          "targetSummary": "Дипломатичная хранительница страсти, власти и преданности.",
          "renderedPromptPackage": "PresetId: azalia"
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_attraction_without_authority_active_guardian.json",
            """
            {
              "guardians": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.SystemGuardianAttraction);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "system_guardian_attraction_missing_active_guardian", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "system_guardian_attraction_target_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_SystemGuardianAttractionDoesNotUseCurrentMirrorWhenGenericSharedCurrentAuthorityIsUnavailable()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [],
          "activeGuardian": {
            "guardianId": "guardian_azalia",
            "canonicalName": "Азалия",
            "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Raw mirror must not satisfy system attraction when shared current authority cannot be built."
            },
            "manifestationHistory": [],
            "domain": "Tide",
            "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
            "personalityProfile": {
              "archetype": "Tide Keeper",
              "speechPattern": "Measured and tidal",
              "coreValues": [ "balance", "memory", "patience" ]
            },
            "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] },
            "sourcePreset": { "presetId": "azalia", "displayName": "Азалия", "version": "1.0", "library": "built_in" }
          }
        }
        """);

        await WriteRawAsync(SystemGuardianLibraryService.AttractionRequestPath, """
        {
          "mode": "system_guardian_attraction",
          "targetPresetId": "azalia",
          "targetPresetDisplayName": "Азалия",
          "targetPresetVersion": "1.0",
          "sourceLibrary": "built_in",
          "targetSummary": "Дипломатичная хранительница страсти, власти и преданности.",
          "renderedPromptPackage": "PresetId: azalia"
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_attraction_invalid_shared_current_authority.json",
            """
            {
              "guardians": {}
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.SystemGuardianAttraction);

        var missingActiveGuardianIssue = Assert.Single(
            issues,
            issue => string.Equals(issue.Code, "system_guardian_attraction_missing_active_guardian", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("raw mirror without current guardian authority", missingActiveGuardianIssue.Actual ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("strict kernel authority", missingActiveGuardianIssue.Actual ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "system_guardian_attraction_target_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_SystemGuardianAttraction_TargetMismatchUsesAuthorityBackedCreate()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "UpdateGuardians": [
            {
              "command": "create",
              "data": {
                "guardianId": "guardian_azalia",
                "canonicalName": "Азалия",
                "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": "Азалия", "neutral": "Азалия" },
                "manifestation": {
                  "currentDisplayName": "Азалия",
                  "formFlexibility": "selective",
                  "currentPresentationStyle": "feminine",
                  "currentPronouns": "она/её",
                  "appearanceDescription": "Authority-backed create should drive attraction mismatch validation."
                },
                "manifestationHistory": [],
                "domain": "Tide",
                "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
                "personalityProfile": {
                  "archetype": "Tide Keeper",
                  "speechPattern": "Measured and tidal",
                  "coreValues": [ "balance", "memory", "patience" ]
                },
                "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                "guardianRelationships": [],
                "mood": { "current": "focused", "intensity": 40, "reason": "Materializing under system attraction.", "since": 10 },
                "loreFragments": [
                  { "fragmentId": "guardian_azalia_lore_1", "category": "personal_history", "title": "Первая искра", "content": null, "requiredReputation": 0 },
                  { "fragmentId": "guardian_azalia_lore_2", "category": "cosmic_secret", "title": "Тайна пламени", "content": null, "requiredReputation": 50 },
                  { "fragmentId": "guardian_azalia_lore_3", "category": "domain_mastery", "title": "Власть слова", "content": null, "requiredReputation": 130 },
                  { "fragmentId": "guardian_azalia_lore_4", "category": "lost_world", "title": "Следы обители", "content": null, "requiredReputation": 230 },
                  { "fragmentId": "guardian_azalia_lore_5", "category": "other_guardians", "title": "Имена союзников", "content": null, "requiredReputation": 0 },
                  { "fragmentId": "guardian_azalia_lore_6", "category": "soul_mechanics", "title": "Память души", "content": null, "requiredReputation": 50 },
                  { "fragmentId": "guardian_azalia_lore_7", "category": "personal_history", "title": "Возвращение искры", "content": null, "requiredReputation": 130 }
                ],
                "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                "gachaSystem": { "chargesPerReturn": 2, "chargesUsedThisReturn": 0, "gachaHistory": [] },
                "sourcePreset": { "presetId": "wrong_preset", "displayName": "Кто-то другой", "version": "1.0", "library": "built_in" }
              }
            }
          ]
        }
        """);

        await WriteRawAsync(SystemGuardianLibraryService.AttractionRequestPath, """
        {
          "mode": "system_guardian_attraction",
          "targetPresetId": "azalia"
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_attraction_target_mismatch_create.json",
            """
            {
              "guardians": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.SystemGuardianAttraction);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "system_guardian_attraction_target_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "system_guardian_attraction_missing_active_guardian", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task IncarnationTrigger_StaleSnapshotContextRaisesSnapshotErrorInsteadOfWrongRealm()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/control/incarnation_trigger.json", """
        {
          "worldDescription": "Новая смертная жизнь.",
          "characterDescription": "Душа просыпается в новом теле.",
          "circumstances": "Переход между жизнями."
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_incarnation_stale_realm.json",
            """
            {
              "currentRealm": "Mortal World"
            }
            """);

        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "incarnation_trigger_invalid_validated_snapshot_context", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "incarnation_trigger_invalid_realm", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task IncarnationTrigger_MissingSnapshotRaisesSnapshotContextError()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/control/incarnation_trigger.json", """
        {
          "worldDescription": "Новая смертная жизнь.",
          "characterDescription": "Душа просыпается в новом теле.",
          "circumstances": "Переход между жизнями."
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "incarnation_trigger_invalid_validated_snapshot_context", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "incarnation_trigger_invalid_realm", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnGuardianFavor_SnapshotFileBeatsTamperedBackupButStillRequiresAuthorityMutation()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: GUARDIAN_FAVOR] 30 Чернильных Перьев"
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
                "appearanceDescription": "Current guardian after favor."
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

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_tampered_backup_favor.json",
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
                    "appearanceDescription": "Validated snapshot guardian state."
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

        await WriteRawAsync("test_backups/preturn_guardians_tampered_backup_favor.json", """
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
                "appearanceDescription": "Tampered rollback backup must be ignored."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 5, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);
        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_accepted_turn_favor.json",
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
          "actionTag": "GUARDIAN_FAVOR",
          "resolutionType": "guardianReputation",
          "summary": "Validated snapshot should win over tampered rollback backup.",
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

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_action_invalid_validated_snapshot_context", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_action_missing_validated_snapshot_file", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_action_invalid_validated_snapshot_data", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_favor_reputation_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnDonateToGuardian_UsesAuthorityReputationInsteadOfMaterializedDrift()
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
                "appearanceDescription": "Materialized current guardian drift inflates reputation without authority."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 35, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_donate_authority_drift.json",
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
                    "appearanceDescription": "Validated snapshot guardian state."
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
            "test_backups/preturn_soul_state_donate_authority_drift.json",
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
          "summary": "Raw materialized guardian drift must not satisfy donation outcome.",
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

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_reputation_delta_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnDonateToGuardian_InvalidRawPowerEventsRaiseCurrentGuardianAuthorityError()
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
                "appearanceDescription": "Current guardian after donation with invalid raw political event."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 35, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "invalid_raw_political_for_donation",
              "guardianId": "guardian_alpha",
              "delta": 3,
              "reasonType": "project_completion",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "project_invalid_raw_for_donation",
              "title": "Invalid raw political event should invalidate strict current guardian authority",
              "summary": "Donation outcome must not read sanitized guardian authority when raw power events are invalid.",
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

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_donation_invalid_raw_authority.json",
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
                    "appearanceDescription": "Validated snapshot guardian before donation invalid raw authority regression."
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
            "test_backups/preturn_soul_state_donation_invalid_raw_authority.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_donation_invalid_raw_authority.json",
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
          "actionTag": "DONATE_TO_GUARDIAN",
          "resolutionType": "guardianReputation",
          "summary": "Invalid raw power events must invalidate strict current guardian authority for donation outcome.",
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

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_invalid_current_authority", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_reputation_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnDonateToGuardian_NonArrayGuardianPowerEventsRaiseCurrentGuardianAuthorityError()
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
                "appearanceDescription": "Current guardian after donation with malformed non-array guardianPowerEvents."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 35, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": {}
        }
        """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_donation_non_array_power_events.json",
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
                    "appearanceDescription": "Validated snapshot guardian before donation non-array power-event regression."
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
            "test_backups/preturn_soul_state_donation_non_array_power_events.json",
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
          "summary": "Malformed non-array guardianPowerEvents must invalidate strict current guardian authority for donation outcome.",
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

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_invalid_current_authority", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_reputation_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnDonateToGuardian_ParseableButInvalidValidatedGuardiansSnapshotRaisesSnapshotDataError()
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
                "appearanceDescription": "Current guardian after donation with invalid validated guardians baseline."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 35, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_parseable_invalid_accepted_turn_donation.json",
            """
            {
              "guardians": [
                {
                  "guardianId": "guardian_alpha",
                  "relationshipData": { "currentReputation": 20 }
                }
              ]
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_parseable_invalid_accepted_turn_donation.json",
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
          "summary": "Parseable invalid guardian snapshot must not authorize donation baseline.",
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

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_action_invalid_validated_snapshot_data", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_reputation_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnDonateToGuardian_EmptySnapshotGuardianPowerEventsDoNotRequireSnapshotJournal()
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
                "appearanceDescription": "Current guardian after donation with empty snapshot guardianPowerEvents baseline."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 35, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_empty_power_events_donation.json",
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
                    "appearanceDescription": "Validated snapshot guardian state with empty guardianPowerEvents array."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 20, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ],
              "guardianPowerEvents": []
            }
            """));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_empty_power_events_donation.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);
        await RemoveTrackedFileFromCurrentPendingTurnSnapshotAsync(GuardianPowerEventState.JournalPath);

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
          "summary": "Empty snapshot guardianPowerEvents must not require snapshot journal baseline for donation proof.",
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
    public async Task AcceptedTurnDonateToGuardian_DuplicateGuardianIdsInValidatedSnapshotRaiseSnapshotDataError()
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
                "appearanceDescription": "Current guardian after donation with duplicate validated baseline ids."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 35, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_duplicate_ids_accepted_turn_donation.json",
            NormalizeGuardianStateJson("""
            {
              "guardians": [
                {
                  "guardianId": "guardian_alpha",
                  "canonicalName": "Азалия"
                },
                {
                  "guardianId": "guardian_alpha",
                  "canonicalName": "Азалия Дубль"
                }
              ]
            }
            """));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_duplicate_ids_accepted_turn_donation.json",
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
          "summary": "Duplicate guardian ids in validated snapshot must not authorize donation baseline.",
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

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_action_invalid_validated_snapshot_data", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_reputation_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnDonateToGuardian_InvalidRootLevelGuardianSnapshotRaisesSnapshotDataError()
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
                "appearanceDescription": "Current guardian after donation with invalid root-level validated snapshot."
              },
              "manifestationHistory": [],
              "abode": {
                "abodeId": "abode_alpha",
                "name": "Обитель Азалии",
                "theme": "Тихий Шёпот",
                "isDiscovered": true
              },
              "relationshipData": { "currentReputation": 35, "reputationHistory": [], "lastInteraction": null },
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
              "appearanceDescription": "Mirror guardian for current donation state."
            },
            "manifestationHistory": [],
            "abode": {
              "abodeId": "abode_alpha",
              "name": "Обитель Азалии",
              "theme": "Тихий Шёпот",
              "isDiscovered": true
            },
            "relationshipData": { "currentReputation": 35, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          },
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_alpha",
            "discoveredAbodes": [ "abode_alpha" ]
          }
        }
        """;

        await WriteRawAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson(currentGuardiansJson));

        var invalidSnapshotRoot = JsonNode.Parse(NormalizeGuardianStateJson(currentGuardiansJson))!.AsObject();
        invalidSnapshotRoot["chaosSeaNavigation"]!["currentAbodeId"] = "abode_wrong";
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_invalid_root_level_accepted_turn_donation.json",
            invalidSnapshotRoot.ToJsonString());

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_invalid_root_level_accepted_turn_donation.json",
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
          "summary": "Malformed root-level guardian snapshot must not authorize donation baseline.",
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

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_action_invalid_validated_snapshot_data", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_reputation_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnDonateToGuardian_OrphanActiveGuardianInValidatedSnapshotRaisesSnapshotDataError()
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
                "appearanceDescription": "Canonical guardian for orphan activeGuardian snapshot regression."
              },
              "manifestationHistory": [],
              "abode": {
                "abodeId": "abode_alpha",
                "name": "Обитель Азалии",
                "theme": "Тихий Шёпот",
                "isDiscovered": true
              },
              "relationshipData": { "currentReputation": 35, "reputationHistory": [], "lastInteraction": null },
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
              "appearanceDescription": "Current strict mirror guardian."
            },
            "manifestationHistory": [],
            "abode": {
              "abodeId": "abode_alpha",
              "name": "Обитель Азалии",
              "theme": "Тихий Шёпот",
              "isDiscovered": true
            },
            "relationshipData": { "currentReputation": 35, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          },
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_alpha",
            "discoveredAbodes": [ "abode_alpha" ]
          }
        }
        """;

        await WriteRawAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson(currentGuardiansJson));

        var invalidSnapshotRoot = JsonNode.Parse(NormalizeGuardianStateJson(currentGuardiansJson))!.AsObject();
        invalidSnapshotRoot["activeGuardian"]!["guardianId"] = "guardian_orphan";
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_orphan_active_guardian_accepted_turn_donation.json",
            invalidSnapshotRoot.ToJsonString());

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_orphan_active_guardian_accepted_turn_donation.json",
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
          "summary": "Orphan activeGuardian in validated snapshot must not authorize donation baseline.",
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

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_action_invalid_validated_snapshot_data", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_reputation_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnDonateToGuardian_InvalidUpdateGuardiansInValidatedSnapshotRaisesSnapshotDataError()
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
                "appearanceDescription": "Canonical guardian for invalid UpdateGuardians snapshot regression."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 35, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        await WriteRawAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson(currentGuardiansJson));

        var invalidSnapshotRoot = JsonNode.Parse(NormalizeGuardianStateJson(currentGuardiansJson))!.AsObject();
        invalidSnapshotRoot["UpdateGuardians"] = JsonNode.Parse("""
        [
          {
            "command": "updateReputation",
            "guardianId": "guardian_alpha",
            "reputationChange": "invalid",
            "reason": "snapshot drifted command surface"
          }
        ]
        """);
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_invalid_update_guardians_accepted_turn_donation.json",
            invalidSnapshotRoot.ToJsonString());

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_invalid_update_guardians_accepted_turn_donation.json",
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
          "summary": "Validated guardian snapshot must reject invalid UpdateGuardians command surfaces.",
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

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_action_invalid_validated_snapshot_data", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_reputation_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnDonateToGuardian_InvalidGuardianPowerEventsInValidatedSnapshotRaisesSnapshotDataError()
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

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": []
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
                "appearanceDescription": "Canonical guardian for invalid guardianPowerEvents snapshot regression."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 35, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """;

        await WriteRawAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson(currentGuardiansJson));

        var invalidSnapshotRoot = JsonNode.Parse(NormalizeGuardianStateJson(currentGuardiansJson))!.AsObject();
        invalidSnapshotRoot["guardianPowerEvents"] = JsonNode.Parse("""
        [
          {
            "eventId": "snapshot_invalid_power_event",
            "guardianId": "guardian_alpha",
            "delta": "invalid",
            "reasonType": "resonance",
            "sourceSurface": "life_evaluation",
            "sourceId": "life_eval_snapshot_invalid_power_event",
            "title": "Snapshot guardian power event must stay canonical",
            "summary": "Accepted-turn guardian baseline must reject invalid guardianPowerEvents.",
            "visibility": "player_known",
            "appliedAt": "2026-03-28T00:00:00Z",
            "audit": {
              "lifeId": "life_snapshot_invalid_power_event",
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
            "test_backups/preturn_guardians_invalid_power_events_accepted_turn_donation.json",
            invalidSnapshotRoot.ToJsonString());

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_tracker_for_invalid_power_events_accepted_turn_donation.json",
            """
            {
              "activeProjects": [],
              "completedProjects": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_invalid_power_events_accepted_turn_donation.json",
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
          "summary": "Validated guardian snapshot must reject invalid guardianPowerEvents surfaces.",
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

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_action_invalid_validated_snapshot_data", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_reputation_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnGuardianFavor_MalformedValidatedGuardiansSnapshotRaisesExplicitSnapshotDataError()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: GUARDIAN_FAVOR] 30 Чернильных Перьев"
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
                "appearanceDescription": "Current guardian after favor with malformed validated guardian baseline."
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

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_invalid_accepted_turn_favor.json",
            "{");

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_invalid_accepted_turn_favor.json",
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
          "actionTag": "GUARDIAN_FAVOR",
          "resolutionType": "guardianReputation",
          "summary": "Malformed validated guardian baseline must surface snapshot-data error.",
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

    [Fact]
    public async Task AcceptedTurnGuardianFavor_MissingTargetGuardianInValidatedSnapshotRaisesSnapshotDataError()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: GUARDIAN_FAVOR] 30 Чернильных Перьев"
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
                "appearanceDescription": "Current guardian after favor with missing target in validated snapshot."
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

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_missing_target_accepted_turn_favor.json",
            NormalizeGuardianStateJson("""
            {
              "guardians": [
                {
                  "guardianId": "guardian_beta",
                  "canonicalName": "Лиора"
                }
              ]
            }
            """));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_missing_target_accepted_turn_favor.json",
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
          "actionTag": "GUARDIAN_FAVOR",
          "resolutionType": "guardianReputation",
          "summary": "Readable validated snapshot without target guardian must not authorize favor baseline.",
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

    [Fact]
    public async Task AcceptedTurnGuardianFavor_StaleManifestDoesNotReusePreTurnGuardianBackup()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: GUARDIAN_FAVOR] 30 Чернильных Перьев"
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
                "appearanceDescription": "Текущий guardian после GM-side favor."
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

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_stale_accepted_turn_favor.json",
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
                    "appearanceDescription": "Stale pre-turn guardian snapshot."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 20, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await WriteRawAsync("output/ink_feather_action_result.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "actionTag": "GUARDIAN_FAVOR",
          "resolutionType": "guardianReputation",
          "summary": "Stale manifest must not authorize old guardian backup.",
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

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_action_invalid_validated_snapshot_context", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_favor_reputation_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnGuardianFavor_InvalidRawPowerEventsRaiseCurrentGuardianAuthorityError()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: GUARDIAN_FAVOR] 30 Чернильных Перьев"
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
                "appearanceDescription": "Current guardian after favor with invalid raw political event."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "invalid_raw_political_for_favor",
              "guardianId": "guardian_alpha",
              "delta": 3,
              "reasonType": "project_completion",
              "sourceSurface": "completeGuardianProjects",
              "sourceId": "project_invalid_raw_for_favor",
              "title": "Invalid raw political event should invalidate strict current guardian authority",
              "summary": "Guardian favor outcome must not read sanitized guardian authority when raw power events are invalid.",
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

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_favor_invalid_raw_authority.json",
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
                    "appearanceDescription": "Validated snapshot guardian before favor invalid raw authority regression."
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
            "test_backups/preturn_soul_state_favor_invalid_raw_authority.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_favor_invalid_raw_authority.json",
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
          "actionTag": "GUARDIAN_FAVOR",
          "resolutionType": "guardianReputation",
          "summary": "Invalid raw power events must invalidate strict current guardian authority for favor outcome.",
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
            string.Equals(issue.Code, "ink_feather_guardian_invalid_current_authority", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_favor_reputation_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnGuardianFavor_NonArrayGuardianPowerEventsRaiseCurrentGuardianAuthorityError()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12,
          "playerAction": "[INK_FEATHER_ACTION: GUARDIAN_FAVOR] 30 Чернильных Перьев"
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
                "appearanceDescription": "Current guardian after favor with malformed non-array guardianPowerEvents."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 30, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": {}
        }
        """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_favor_non_array_power_events.json",
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
                    "appearanceDescription": "Validated snapshot guardian before favor non-array power-event regression."
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
            "test_backups/preturn_soul_state_favor_non_array_power_events.json",
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
          "actionTag": "GUARDIAN_FAVOR",
          "resolutionType": "guardianReputation",
          "summary": "Malformed non-array guardianPowerEvents must invalidate strict current guardian authority for guardian favor outcome.",
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
            string.Equals(issue.Code, "ink_feather_guardian_invalid_current_authority", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "ink_feather_guardian_favor_reputation_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_GuardianCentricMissingValidatedSnapshotRaisesSnapshotContextError()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12
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
                "appearanceDescription": "Canonical guardian for reasoning snapshot test."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
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
              "appearanceDescription": "Active guardian for reasoning snapshot test."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Guardian-centric\n- Релевантные акторы: Азалия\n- Почему они релевантны: Ход сосредоточен на решении хранителя.\n- Акторы вне охвата: нет\n- Почему они вне охвата: Другие акторы не принимают решений.\n\n## Guardian Thoughts\n### Азалия\n- Ситуация: Хранитель оценивает последствия действия игрока.\n- Мысли: Она взвешивает реакцию на поступок.\n- Действия: Она выбирает мягкий ответ.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_scope_invalid_validated_snapshot_context", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_GuardianCentricUsesCanonicalGuardianNameInsteadOfStaleMirror()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12
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
                "appearanceDescription": "Canonical guardian should drive scope aliasing."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_alpha",
            "canonicalName": "Старое Имя",
            "nameVariants": { "default": "Старое Имя", "feminine": "Старое Имя", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Старое Имя",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Stale mirror name must not drive guardian-centric scope."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_reasoning_scope.json",
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

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Guardian-centric\n- Релевантные акторы: Азалия\n- Почему они релевантны: Ход сосредоточен на canonical guardian.\n- Акторы вне охвата: нет\n- Почему они вне охвата: Другие акторы не принимают решений.\n\n## Guardian Thoughts\n### Азалия\n- Ситуация: Хранитель оценивает исход конфликта.\n- Мысли: Она сверяет последствия с клятвами.\n- Действия: Она готовит ответ.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_scope_invalid_validated_snapshot_context", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "active_guardian_missing_from_scope", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_GuardianCentricOrphanActiveGuardianRaisesIdentityError()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12
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
                "appearanceDescription": "Canonical guardian exists, but active mirror points elsewhere."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_orphan",
            "canonicalName": "Старая Тень",
            "nameVariants": { "default": "Старая Тень", "feminine": "Старая Тень", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Старая Тень",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Orphan activeGuardian mirror should not authorize reasoning."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": 25, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 70, "tier": "Сильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_reasoning_orphan.json",
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

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Guardian-centric\n- Релевантные акторы: Старая Тень\n- Почему они релевантны: Размышления ошибочно опираются на orphan mirror.\n- Акторы вне охвата: нет\n- Почему они вне охвата: Другие акторы не участвуют.\n\n## Guardian Thoughts\n### Старая Тень\n- Ситуация: Хранитель оценивает действие.\n- Мысли: Она рассматривает жёсткий ответ.\n- Действия: Она готовит вмешательство.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_scope_invalid_active_guardian_identity", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_scope_invalid_validated_snapshot_context", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MixedGuardianScopeMissingValidatedSnapshotRaisesSnapshotContextError()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12
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
                "appearanceDescription": "Canonical guardian for mixed reasoning snapshot test."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
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
              "appearanceDescription": "Active guardian for mixed reasoning snapshot test."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Mixed\n- Релевантные акторы: Азалия\n- Почему они релевантны: Ход частично зависит от активного хранителя.\n- Акторы вне охвата: нет\n- Почему они вне охвата: Другие акторы не принимают решения.\n\n## Guardian Thoughts\n### Азалия\n- Ситуация: Хранитель сопоставляет последствия.\n- Мысли: Она анализирует моральную цену решения.\n- Действия: Она готовит осторожный ответ.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_scope_invalid_validated_snapshot_context", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MixedWithoutGuardianScopeDoesNotRaiseSnapshotContextError()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Mixed\n- Релевантные акторы: Наблюдатель\n- Почему они релевантны: Ход касается только внешнего свидетеля.\n- Акторы вне охвата: нет\n- Почему они вне охвата: Дополнительные акторы не влияют.\n\n## Reasoning\n### Наблюдатель\n- Ситуация: Он фиксирует последствия хода.\n- Мысли: Он оценивает реакцию окружающих.\n- Действия: Он остаётся в стороне.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_scope_invalid_validated_snapshot_context", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MixedDirectCanonicalGuardianSurfaceWithoutGuardianMentionsRaisesSnapshotError()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "live-session",
          "requestId": "live-request",
          "turnNumber": 12
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteGuardianRawWithoutValidatedSnapshotAsync("""
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
                "appearanceDescription": "Direct current guardians[] surface must activate strict guardian provenance even without guardian mentions in mixed scope."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
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
              "appearanceDescription": "Mirror should not hide missing validated guardians baseline."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Mixed\n- Релевантные акторы: Наблюдатель\n- Почему они релевантны: Текст reasoning не называет Хранителя напрямую.\n- Акторы вне охвата: нет\n- Почему они вне охвата: Дополнительные акторы не обсуждаются.\n\n## Reasoning\n### Наблюдатель\n- Ситуация: Он фиксирует изменение guardian state.\n- Мысли: Current canonical guardians[] уже materialized, но validated baseline нет.\n- Действия: Нужен provenance error.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_scope_missing_validated_guardians_snapshot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_GuardianCentricMissingActiveGuardianRaisesIdentityError()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12
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
                "appearanceDescription": "Canonical guardian exists but activeGuardian is missing."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_reasoning_missing_active.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Guardian-centric\n- Релевантные акторы: Азалия\n- Почему они релевантны: Ход полностью зависит от решения хранителя.\n- Акторы вне охвата: нет\n- Почему они вне охвата: Другие акторы не влияют.\n\n## Guardian Thoughts\n### Азалия\n- Ситуация: Хранитель анализирует последствия.\n- Мысли: Она сопоставляет риск и долг.\n- Действия: Она готовит ответ.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_scope_missing_active_guardian", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "active_guardian_missing_from_scope", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MixedStaleMirrorAliasRaisesCanonicalGuardianError()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12
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
                "appearanceDescription": "Canonical guardian should define reasoning aliases."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_alpha",
            "canonicalName": "Старая Тень",
            "nameVariants": { "default": "Старая Тень", "feminine": "Старая Тень", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Старая Тень",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Stale mirror alias must not drive Mixed reasoning."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_reasoning_mixed_stale_alias.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Mixed\n- Релевантные акторы: Старая Тень\n- Почему они релевантны: reasoning ошибочно опирается на stale mirror alias.\n- Акторы вне охвата: нет\n- Почему они вне охвата: Другие акторы не влияют.\n\n## Guardian Thoughts\n### Старая Тень\n- Ситуация: Хранитель оценивает исход.\n- Мысли: Она сопоставляет последствия с клятвами.\n- Действия: Она готовит вмешательство.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_scope_stale_active_guardian_alias", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MixedTamperedCurrentGuardianAliasDoesNotCoverGuardianPowerEvent()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12
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
              "canonicalName": "Ложная Тень",
              "nameVariants": { "default": "Ложная Тень", "feminine": "Ложная Тень", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Ложная Тень",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Raw current guardian alias must not soften reasoning scope enforcement."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "power_evt_alias_drift",
              "guardianId": "guardian_alpha",
              "delta": 4,
              "reasonType": "offering",
              "visibility": "public"
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_reasoning_current_alias_drift.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_reasoning_current_alias_drift.json",
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
                    "appearanceDescription": "Validated canonical guardian alias must remain authoritative for reasoning."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Mixed\n- Релевантные акторы: Ложная Тень\n- Почему они релевантны: reasoning ошибочно пытается покрыть guardianPowerEvents через tampered current guardian alias.\n- Акторы вне охвата: Азалия\n- Почему они вне охвата: Каноническое имя хранителя ошибочно исключено.\n\n## Guardian Thoughts\n### Ложная Тень\n- Ситуация: Raw current guardian alias пытается пройти как canonical scope actor.\n- Мысли: Это не должно снимать требование canonical guardian coverage.\n- Действия: Нужен strict out-of-scope error по authority alias.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "structured_guardian_update_out_of_scope", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Азалия", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "structured_guardian_update_out_of_scope", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Ложная Тень", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MixedCommandShapedGuardianUpdateOutOfScopeUsesPreTurnCanonicalAlias()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "UpdateGuardians": [
            {
              "command": "updateReputation",
              "guardianId": "guardian_alpha",
              "reputationChange": 5,
              "reason": "Command-shaped update should still resolve canonical guardian scope."
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_reasoning_command_shape.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_reasoning_command_shape.json",
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
                    "appearanceDescription": "Pre-turn canonical guardian baseline for command-shaped reasoning test."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ],
              "activeGuardian": {
                "guardianId": "guardian_alpha",
                "canonicalName": "Азалия",
                "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": "Азалия", "neutral": "Азалия" },
                "manifestation": {
                  "currentDisplayName": "Азалия",
                  "formFlexibility": "selective",
                  "currentPresentationStyle": "feminine",
                  "currentPronouns": "она/её",
                  "appearanceDescription": "Pre-turn active guardian baseline for command-shaped reasoning test."
                },
                "manifestationHistory": [],
                "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                "guardianRelationships": [],
                "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
              }
            }
            """);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Mixed\n- Релевантные акторы: Наблюдатель\n- Почему они релевантны: reasoning не покрывает хранителя, хотя UpdateGuardians меняет его.\n- Акторы вне охвата: Азалия\n- Почему они вне охвата: Хранитель ошибочно исключён.\n\n## Reasoning\n### Наблюдатель\n- Ситуация: Он отмечает напряжение в мире.\n- Мысли: Он оценивает, кто может отреагировать.\n- Действия: Он остаётся в стороне.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "structured_guardian_update_out_of_scope", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Азалия", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "structured_guardian_update_missing_canonical_identity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MixedCommandShapedGuardianUpdateUnknownGuardianRaisesCanonicalIdentityError()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "UpdateGuardians": [
            {
              "command": "updateReputation",
              "guardianId": "guardian_unknown",
              "reputationChange": 5,
              "reason": "Unknown guardian must not bypass reasoning scope enforcement."
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_reasoning_unknown_guardian.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Mixed\n- Релевантные акторы: Наблюдатель\n- Почему они релевантны: reasoning не может канонически резолвить guardian update.\n- Акторы вне охвата: нет\n- Почему они вне охвата: Другие акторы не влияют.\n\n## Reasoning\n### Наблюдатель\n- Ситуация: Он видит странное смещение состояния.\n- Мысли: Он не понимает, кто именно был изменён.\n- Действия: Он ждёт прояснения.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "structured_guardian_update_missing_canonical_identity", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "guardian_unknown", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MixedUnknownGuardianRawIdInScopeStillRaisesCanonicalIdentityError()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "UpdateGuardians": [
            {
              "command": "updateReputation",
              "guardianId": "guardian_unknown",
              "reputationChange": 5,
              "reason": "Unknown guardian raw id in Relevant actors must not bypass canonical identity enforcement."
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_reasoning_unknown_guardian_raw_id.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Mixed\n- Релевантные акторы: guardian_unknown\n- Почему они релевантны: reasoning ошибочно пытается покрыть unknown guardian raw id.\n- Акторы вне охвата: нет\n- Почему они вне охвата: Другие акторы не влияют.\n\n## Reasoning\n### guardian_unknown\n- Ситуация: Идентичность хранителя не установлена.\n- Мысли: Raw id не должен считаться guardian scope alias.\n- Действия: Нужна canonical identity.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "structured_guardian_update_missing_canonical_identity", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "guardian_unknown", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "structured_guardian_update_out_of_scope", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MixedRawGuardianIdInScopeWithoutGuardianUpdatesRaisesGuardianScopeAliasError()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12
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
                "appearanceDescription": "Mixed reasoning should not silently accept raw guardianId as scope actor."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
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
              "appearanceDescription": "Mirror must not let raw guardianId pass as a canonical scope actor."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_reasoning_mixed_raw_id_only.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Mixed\n- Релевантные акторы: guardian_alpha\n- Почему они релевантны: reasoning ссылается на raw guardianId без guardian update.\n- Акторы вне охвата: нет\n- Почему они вне охвата: Другие акторы не влияют.\n\n## Guardian Thoughts\n### guardian_alpha\n- Ситуация: Хранитель фигурирует в охвате только по transport id.\n- Мысли: Raw id не должен считаться canonical alias.\n- Действия: Нужна canonical guardian alias.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_scope_uses_raw_guardian_id", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "guardian_alpha", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MixedDirectCanonicalGuardianChangeOutOfScopeRaisesGuardianUpdateOutOfScope()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12
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
                "appearanceDescription": "Direct canonical guardian state change must participate in reasoning scope."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 24, "reputationHistory": [], "lastInteraction": null },
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
              "appearanceDescription": "Mirror follows the changed canonical guardian."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": 24, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_reasoning_direct_guardian_diff.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_reasoning_direct_guardian_diff.json",
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
                    "appearanceDescription": "Pre-turn guardian baseline for direct canonical diff reasoning test."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ],
              "activeGuardian": {
                "guardianId": "guardian_alpha",
                "canonicalName": "Азалия",
                "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": "Азалия", "neutral": "Азалия" },
                "manifestation": {
                  "currentDisplayName": "Азалия",
                  "formFlexibility": "selective",
                  "currentPresentationStyle": "feminine",
                  "currentPronouns": "она/её",
                  "appearanceDescription": "Pre-turn active guardian baseline for direct canonical diff reasoning test."
                },
                "manifestationHistory": [],
                "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                "guardianRelationships": [],
                "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
              }
            }
            """);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Mixed\n- Релевантные акторы: Наблюдатель\n- Почему они релевантны: reasoning не покрывает direct canonical guardian change.\n- Акторы вне охвата: Азалия\n- Почему они вне охвата: Хранитель ошибочно исключён.\n\n## Reasoning\n### Наблюдатель\n- Ситуация: Он видит колебание guardian state.\n- Мысли: Он не учитывает, что direct canonical guardian change уже произошёл.\n- Действия: Он остаётся внешним свидетелем.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "structured_guardian_update_out_of_scope", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Азалия", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MixedCanonicalGuardiansSurfaceWithoutValidatedSnapshotRaisesSnapshotError()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteGuardianRawWithoutValidatedSnapshotAsync("""
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
                "appearanceDescription": "Direct canonical guardian state needs a validated pre-turn guardians snapshot."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 24, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_reasoning_missing_guardians_snapshot.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Mixed\n- Релевантные акторы: Азалия\n- Почему они релевантны: reasoning касается direct canonical guardian state.\n- Акторы вне охвата: нет\n- Почему они вне охвата: Другие акторы не влияют.\n\n## Guardian Thoughts\n### Азалия\n- Ситуация: Хранитель уже изменён напрямую в canonical state.\n- Мысли: Без validated pre-turn guardians snapshot reasoning не может безопасно сравнить direct diff.\n- Действия: Нужен snapshot-contract error.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_scope_missing_validated_guardians_snapshot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MixedGuardianTouchWithCanonicalGuardiansArrayAndMissingGuardiansSnapshotRaisesSnapshotError()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteGuardianRawWithoutValidatedSnapshotAsync("""
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
                "appearanceDescription": "Current guardians[] stays present while guardianPowerEvents also touch the same guardian."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 24, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 41, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "guardian_power_1",
              "guardianId": "guardian_alpha",
              "changeType": "resonance",
              "amount": 3,
              "occurredAt": "2026-03-28T00:00:00Z",
              "reason": "Reasoning also sees guardianPowerEvents",
              "source": "guardian_power_journal"
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_reasoning_guardian_touch_missing_guardians_snapshot.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Mixed\n- Релевантные акторы: Азалия\n- Почему они релевантны: reasoning касается guardianPowerEvents и текущего guardian state.\n- Акторы вне охвата: нет\n- Почему они вне охвата: Другие акторы не влияют.\n\n## Guardian Thoughts\n### Азалия\n- Ситуация: Журнал силы и current guardian state оба затронуты в одном ходе.\n- Мысли: Без validated pre-turn guardians snapshot direct canonical surface остаётся непроверяемым.\n- Действия: Нужен snapshot-contract error.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_scope_missing_validated_guardians_snapshot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MixedGuardianPowerEventOutOfScopeUsesCanonicalGuardianAlias()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12
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
                "appearanceDescription": "guardianPowerEvents should map to canonical guardian aliases for reasoning."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "power_evt_1",
              "guardianId": "guardian_alpha",
              "delta": 4,
              "reasonType": "offering",
              "visibility": "public"
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_reasoning_power_event_scope.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_reasoning_power_event_scope.json",
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
                    "appearanceDescription": "Pre-turn guardian baseline for guardianPowerEvents reasoning test."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Mixed\n- Релевантные акторы: Наблюдатель\n- Почему они релевантны: reasoning не покрывает guardianPowerEvents.\n- Акторы вне охвата: Азалия\n- Почему они вне охвата: Хранитель ошибочно исключён.\n\n## Reasoning\n### Наблюдатель\n- Ситуация: Он замечает всплеск силы, но не включает хранителя в reasoning.\n- Мысли: guardianPowerEvents должны требовать canonical guardian coverage.\n- Действия: Он остаётся внешним наблюдателем.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "structured_guardian_update_out_of_scope", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Азалия", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MixedGuardianPowerEventUnknownGuardianRaisesCanonicalIdentityError()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardianPowerEvents": [
            {
              "eventId": "power_evt_unknown",
              "guardianId": "guardian_unknown",
              "delta": 3,
              "reasonType": "offering",
              "visibility": "public"
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_reasoning_unknown_power_event.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Mixed\n- Релевантные акторы: Наблюдатель\n- Почему они релевантны: reasoning не может канонически резолвить guardianPowerEvents guardian.\n- Акторы вне охвата: нет\n- Почему они вне охвата: Другие акторы не влияют.\n\n## Reasoning\n### Наблюдатель\n- Ситуация: Он видит необъяснимый всплеск guardian power.\n- Мысли: Неизвестный guardian из power event не должен исчезать из scope validation.\n- Действия: Он ждёт canonical identity.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "structured_guardian_update_missing_canonical_identity", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "guardian_unknown", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MixedCommandShapedGuardianUpdateWithinScopeUsesPreTurnCanonicalAlias()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "UpdateGuardians": [
            {
              "command": "updateReputation",
              "guardianId": "guardian_alpha",
              "reputationChange": 5,
              "reason": "Known guardian update should reuse canonical alias from pre-turn baseline."
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_reasoning_command_shape_in_scope.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_reasoning_command_shape_in_scope.json",
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
                    "appearanceDescription": "Pre-turn canonical guardian baseline for in-scope command-shaped reasoning test."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
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
                  "appearanceDescription": "Pre-turn active guardian baseline for in-scope command-shaped reasoning test."
                },
                "manifestationHistory": [],
                "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                "guardianRelationships": [],
                "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
              }
            }
            """);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Mixed\n- Релевантные акторы: Азалия\n- Почему они релевантны: reasoning покрывает хранителя, которого меняет UpdateGuardians.\n- Акторы вне охвата: нет\n- Почему они вне охвата: Другие акторы не влияют.\n\n## Guardian Thoughts\n### Азалия\n- Ситуация: Хранитель отмечает рост доверия.\n- Мысли: Она взвешивает последствия перемены.\n- Действия: Она принимает изменение.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "structured_guardian_update_out_of_scope", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "structured_guardian_update_missing_canonical_identity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_GuardianCentricRawIdInScopeDoesNotSatisfyCanonicalActiveGuardianCoverage()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12
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
                "appearanceDescription": "Guardian-centric reasoning should require canonical alias, not raw guardian id."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
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
              "appearanceDescription": "Mirror should not let raw guardian id satisfy scope."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_reasoning_raw_id_scope.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Guardian-centric\n- Релевантные акторы: guardian_alpha\n- Почему они релевантны: reasoning пытается покрыть активного хранителя raw id вместо canonical alias.\n- Акторы вне охвата: нет\n- Почему они вне охвата: Другие акторы не участвуют.\n\n## Guardian Thoughts\n### guardian_alpha\n- Ситуация: Хранитель размышляет о ходе.\n- Мысли: Raw id не должен считаться валидным actor label.\n- Действия: Нужно canonical имя.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "active_guardian_missing_from_scope", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_scope_invalid_active_guardian_identity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_GuardianCentricCommandShapedTurnUsesPreTurnCanonicalActiveGuardian()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "UpdateGuardians": [
            {
              "command": "updateReputation",
              "guardianId": "guardian_alpha",
              "reputationChange": 2,
              "reason": "Command-shaped guardian-centric turn should still resolve active guardian through canonical baseline."
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
              "appearanceDescription": "Mirror should resolve through validated pre-turn guardian baseline."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_reasoning_guardian_centric_command_shape.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_reasoning_guardian_centric_command_shape.json",
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
                    "appearanceDescription": "Canonical guardian baseline for guardian-centric command-shaped reasoning."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Guardian-centric\n- Релевантные акторы: Азалия\n- Почему они релевантны: reasoning описывает решение активного хранителя в command-shaped turn.\n- Акторы вне охвата: нет\n- Почему они вне охвата: Другие акторы не участвуют.\n\n## Guardian Thoughts\n### Азалия\n- Ситуация: Хранитель рассматривает изменение репутации.\n- Мысли: Она взвешивает влияние на долг.\n- Действия: Она принимает решение.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_scope_invalid_active_guardian_identity", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_scope_missing_active_guardian", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "active_guardian_missing_from_scope", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_GuardianCentricCreateWithoutRawMirrorUsesAuthorityActiveGuardian()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "UpdateGuardians": [
            {
              "command": "create",
              "data": {
                "guardianId": "guardian_lira",
                "canonicalName": "Лира",
                "nameVariants": { "default": "Лира", "feminine": "Лира", "masculine": "Лира", "neutral": "Лира" },
                "manifestation": {
                  "currentDisplayName": "Лира",
                  "formFlexibility": "selective",
                  "currentPresentationStyle": "feminine",
                  "currentPronouns": "она/её",
                  "appearanceDescription": "Authority create should resolve active guardian without raw mirror."
                },
                "manifestationHistory": [],
                "domain": "Tide",
                "abode": { "abodeId": "abode_lira", "title": "Обитель Лиры" },
                "personalityProfile": {
                  "archetype": "Tide Keeper",
                  "speechPattern": "Measured and tidal",
                  "coreValues": [ "balance", "memory", "patience" ]
                },
                "relationshipData": { "currentReputation": 14, "reputationHistory": [], "lastInteraction": null },
                "abodePower": { "currentPower": 18, "tier": "Угасающая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                "guardianRelationships": [],
                "mood": {
                  "current": "focused",
                  "intensity": 40,
                  "reason": "A newly formed purpose.",
                  "since": 22
                },
                "loreFragments": [
                  { "fragmentId": "guardian_lira_lore_1", "category": "personal_history", "title": "Вход в прилив", "content": null, "requiredReputation": 0 },
                  { "fragmentId": "guardian_lira_lore_2", "category": "cosmic_secret", "title": "Тайна глубины", "content": null, "requiredReputation": 50 },
                  { "fragmentId": "guardian_lira_lore_3", "category": "domain_mastery", "title": "Узел течений", "content": null, "requiredReputation": 130 },
                  { "fragmentId": "guardian_lira_lore_4", "category": "lost_world", "title": "Затонувший берег", "content": null, "requiredReputation": 230 },
                  { "fragmentId": "guardian_lira_lore_5", "category": "other_guardians", "title": "Имена в пене", "content": null, "requiredReputation": 0 },
                  { "fragmentId": "guardian_lira_lore_6", "category": "soul_mechanics", "title": "Память соли", "content": null, "requiredReputation": 50 },
                  { "fragmentId": "guardian_lira_lore_7", "category": "personal_history", "title": "Возвращение волны", "content": null, "requiredReputation": 130 }
                ],
                "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_reasoning_authority_active_no_mirror.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_reasoning_authority_active_no_mirror.json",
            """
            {
              "guardians": []
            }
            """);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Guardian-centric\n- Релевантные акторы: Лира\n- Почему они релевантны: authority-backed create определяет активного хранителя без raw mirror.\n- Акторы вне охвата: нет\n- Почему они вне охвата: Другие акторы не участвуют.\n\n## Guardian Thoughts\n### Лира\n- Ситуация: Новый хранитель принимает решение.\n- Мысли: Она закрепляет своё появление.\n- Действия: Она подтверждает выбор.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_scope_missing_active_guardian", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_scope_invalid_active_guardian_identity", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "active_guardian_missing_from_scope", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MixedGuardianCreateOutOfScopeUsesCreateDataCanonicalAlias()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "UpdateGuardians": [
            {
              "command": "create",
              "data": {
                "guardianId": "guardian_new",
                "canonicalName": "Лира",
                "nameVariants": { "default": "Лира", "feminine": "Лира", "masculine": "Лира", "neutral": "Лира" },
                "manifestation": {
                  "currentDisplayName": "Лира",
                  "formFlexibility": "selective",
                  "currentPresentationStyle": "feminine",
                  "currentPronouns": "она/её",
                  "appearanceDescription": "Same-turn guardian creation must participate in reasoning scope enforcement."
                },
                "manifestationHistory": [],
                "domain": "Tide",
                "abode": { "abodeId": "abode_new", "title": "Тихий прилив" },
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
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_reasoning_create_scope.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_reasoning_create_scope.json",
            """
            {
              "guardians": []
            }
            """);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Mixed\n- Релевантные акторы: Наблюдатель\n- Почему они релевантны: reasoning игнорирует newly created guardian.\n- Акторы вне охвата: Лира\n- Почему они вне охвата: Хранитель ошибочно не включён.\n\n## Reasoning\n### Наблюдатель\n- Ситуация: Он замечает появление новой силы.\n- Мысли: Он не успевает понять, кто возник.\n- Действия: Он отступает.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "structured_guardian_update_out_of_scope", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Лира", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "structured_guardian_update_missing_canonical_identity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MixedGuardianUpdateIgnoresPayloadSmuggledName()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "UpdateGuardians": [
            {
              "command": "updateReputation",
              "guardianId": "guardian_alpha",
              "canonicalName": "Ложная Тень",
              "nameVariants": { "default": "Ложная Тень", "feminine": "Ложная Тень", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Ложная Тень",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Payload name must not be treated as authoritative guardian alias."
              },
              "reputationChange": 3,
              "reason": "Non-create guardian command tries to smuggle stale display name."
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_reasoning_payload_smuggle.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_reasoning_payload_smuggle.json",
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
                    "appearanceDescription": "Canonical guardian baseline must win over payload-smuggled names."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Mixed\n- Релевантные акторы: Ложная Тень\n- Почему они релевантны: reasoning ошибочно доверяет payload name в guardian command.\n- Акторы вне охвата: Азалия\n- Почему они вне охвата: Каноническое имя хранителя ошибочно исключено.\n\n## Reasoning\n### Ложная Тень\n- Ситуация: Хранитель якобы меняет своё отношение.\n- Мысли: Подложное имя пытается пройти в scope.\n- Действия: Оно не должно авторизоваться.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "structured_guardian_update_out_of_scope", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Азалия", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "structured_guardian_update_out_of_scope", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Ложная Тень", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "structured_guardian_update_missing_canonical_identity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MixedDuplicateCreateAgainstCanonicalGuardianDoesNotAuthorizeScopeCoverage()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12
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
                "appearanceDescription": "Existing canonical guardian baseline."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "UpdateGuardians": [
            {
              "command": "create",
              "data": {
                "guardianId": "guardian_alpha",
                "canonicalName": "Ложная Азалия",
                "nameVariants": { "default": "Ложная Азалия", "feminine": "Ложная Азалия", "masculine": "Ложная Азалия", "neutral": "Ложная Азалия" },
                "manifestation": {
                  "currentDisplayName": "Ложная Азалия",
                  "formFlexibility": "selective",
                  "currentPresentationStyle": "feminine",
                  "currentPronouns": "она/её",
                  "appearanceDescription": "Duplicate create must not authorize reasoning scope."
                },
                "manifestationHistory": [],
                "domain": "Tide",
                "abode": { "abodeId": "abode_new", "title": "Тихий прилив" },
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

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_reasoning_duplicate_create_scope.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Mixed\n- Релевантные акторы: Азалия\n- Почему они релевантны: reasoning не должно принимать duplicate create как покрытый canonical guardian actor.\n- Акторы вне охвата: нет\n- Почему они вне охвата: Другие акторы не участвуют.\n\n## Guardian Thoughts\n### Азалия\n- Ситуация: Существующий хранитель наблюдает ход.\n- Мысли: Duplicate create не должен стать валидным scope update.\n- Действия: Нужна explicit guardian identity error.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "structured_guardian_update_missing_canonical_identity", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "guardian_alpha", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "structured_guardian_update_out_of_scope", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Азалия", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MixedMalformedGuardianUpdateWithoutGuardianIdStillRaisesCanonicalIdentityError()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "UpdateGuardians": [
            {
              "command": "updateReputation",
              "reputationChange": 5,
              "reason": "Malformed guardian command must still surface in reasoning normalization."
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_reasoning_malformed_update.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Mixed\n- Релевантные акторы: Наблюдатель\n- Почему они релевантны: reasoning не должен терять malformed guardian command.\n- Акторы вне охвата: нет\n- Почему они вне охвата: Другие акторы не влияют.\n\n## Reasoning\n### Наблюдатель\n- Ситуация: Он видит некорректное изменение guardian state.\n- Мысли: Команда сломана и должна остаться видимой для guardian scope contract.\n- Действия: Он ждёт исправления.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "structured_guardian_update_missing_canonical_identity", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "UpdateGuardians.updateReputation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MixedMalformedGuardianCreateWithoutGuardianIdStillRaisesCanonicalIdentityError()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "UpdateGuardians": [
            {
              "command": "create",
              "data": {
                "canonicalName": "Лира",
                "nameVariants": { "default": "Лира", "feminine": "Лира", "masculine": "Лира", "neutral": "Лира" },
                "manifestation": {
                  "currentDisplayName": "Лира",
                  "formFlexibility": "selective",
                  "currentPresentationStyle": "feminine",
                  "currentPronouns": "она/её",
                  "appearanceDescription": "Malformed create without guardianId must still surface in reasoning."
                }
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_reasoning_malformed_create.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Mixed\n- Релевантные акторы: Наблюдатель\n- Почему они релевантны: reasoning не должен терять malformed guardian create.\n- Акторы вне охвата: нет\n- Почему они вне охвата: Другие акторы не влияют.\n\n## Reasoning\n### Наблюдатель\n- Ситуация: Он замечает попытку создать хранителя без корректной identity.\n- Мысли: Команда должна остаться видимой для guardian scope contract.\n- Действия: Он фиксирует нарушение.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "structured_guardian_update_missing_canonical_identity", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "UpdateGuardians.create", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_GuardianCanonicalNamesCannotCollapseToGuardianId()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "guardian_alpha",
              "nameVariants": { "default": "guardian_alpha", "feminine": null, "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "guardian_alpha",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Canonical guardian naming must not collapse to raw guardianId."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_canonical_name_collapses_to_guardian_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MixedUsesNameVariantsDefaultAsCanonicalGuardianAlias()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "UpdateGuardians": [
            {
              "command": "updateReputation",
              "guardianId": "guardian_alpha",
              "reputationChange": 3,
              "reason": "Reasoning should accept nameVariants.default as canonical alias."
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_reasoning_namevariant_default.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_reasoning_namevariant_default.json",
            """
            {
              "guardians": [
                {
                  "guardianId": "guardian_alpha",
                  "canonicalName": "guardian_alpha",
                  "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
                  "manifestation": {
                    "currentDisplayName": "guardian_alpha",
                    "formFlexibility": "selective",
                    "currentPresentationStyle": "feminine",
                    "currentPronouns": "она/её",
                    "appearanceDescription": "Reasoning should still resolve guardian through human-readable nameVariants.default."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ],
              "activeGuardian": {
                "guardianId": "guardian_alpha",
                "canonicalName": "guardian_alpha",
                "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
                "manifestation": {
                  "currentDisplayName": "guardian_alpha",
                  "formFlexibility": "selective",
                  "currentPresentationStyle": "feminine",
                  "currentPronouns": "она/её",
                  "appearanceDescription": "Active guardian also uses nameVariants.default as human-readable alias."
                },
                "manifestationHistory": [],
                "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                "guardianRelationships": [],
                "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
              }
            }
            """);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Mixed\n- Релевантные акторы: Азалия\n- Почему они релевантны: reasoning должен разрешать canonical alias из nameVariants.default.\n- Акторы вне охвата: нет\n- Почему они вне охвата: Другие акторы не влияют.\n\n## Guardian Thoughts\n### Азалия\n- Ситуация: Хранитель оценивает изменение репутации.\n- Мысли: Human-readable alias живёт в nameVariants.default.\n- Действия: Она принимает изменение.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "structured_guardian_update_missing_canonical_identity", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "structured_guardian_update_out_of_scope", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_GuardianAliasWithCommaStaysSingleRelevantActor()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "UpdateGuardians": [
            {
              "command": "updateReputation",
              "guardianId": "guardian_selena",
              "reputationChange": 1,
              "reason": "The comma-containing Guardian name must still satisfy reasoning scope."
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_reasoning_comma_alias.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_reasoning_comma_alias.json",
            """
            {
              "guardians": [
                {
                  "guardianId": "guardian_selena",
                  "canonicalName": "Селена, хранительница забытых библиотек",
                  "nameVariants": {
                    "default": "Селена, хранительница забытых библиотек",
                    "feminine": "Селена, хранительница забытых библиотек",
                    "masculine": null,
                    "neutral": null
                  },
                  "manifestation": {
                    "currentDisplayName": "Селена, хранительница забытых библиотек",
                    "formFlexibility": "selective",
                    "currentPresentationStyle": "feminine",
                    "currentPronouns": "она/её",
                    "appearanceDescription": "Хранительница архивов над Серым морем."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 0, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 10, "tier": "Угасающая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ],
              "activeGuardian": {
                "guardianId": "guardian_selena",
                "canonicalName": "Селена, хранительница забытых библиотек",
                "nameVariants": {
                  "default": "Селена, хранительница забытых библиотек",
                  "feminine": "Селена, хранительница забытых библиотек",
                  "masculine": null,
                  "neutral": null
                },
                "manifestation": {
                  "currentDisplayName": "Селена, хранительница забытых библиотек",
                  "formFlexibility": "selective",
                  "currentPresentationStyle": "feminine",
                  "currentPronouns": "она/её",
                  "appearanceDescription": "Хранительница архивов над Серым морем."
                },
                "manifestationHistory": [],
                "relationshipData": { "currentReputation": 0, "reputationHistory": [], "lastInteraction": null },
                "abodePower": { "currentPower": 10, "tier": "Угасающая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                "guardianRelationships": [],
                "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
              }
            }
            """);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Guardian-centric\n- Релевантные акторы: Селена, хранительница забытых библиотек\n- Почему они релевантны: ход меняет репутацию активного Хранителя.\n- Акторы вне охвата: нет\n- Почему они вне охвата: Другие акторы не участвуют.\n\n## Guardian Thoughts\n### Селена, хранительница забытых библиотек\n- Ситуация: Хранительница принимает первую оценку души.\n- Мысли: Она сверяет просьбу с памятью своих архивов.\n- Действия: Она меняет отношение к душе.\n",
          "timestamp": "2026-03-28T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "active_guardian_missing_from_scope", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "structured_guardian_update_out_of_scope", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "missing_actor_block", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_NewGuardianMaterializedInCurrentGuardiansWithoutCreateSurfaceRaisesExplicitError()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_new",
              "canonicalName": "Лира",
              "nameVariants": { "default": "Лира", "feminine": "Лира", "masculine": "Лира", "neutral": "Лира" },
              "manifestation": {
                "currentDisplayName": "Лира",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Current guardians[] must not silently materialize a new guardian over a validated pre-turn baseline."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_new", "title": "Тихий прилив" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 25, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 18, "tier": "Угасающая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_new_materialization_without_create.json",
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
                  "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_materialized_without_create_surface", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actual, "guardian_new", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_FirstGuardianMaterializedInCurrentGuardiansOverEmptyValidatedBaselineRaisesExplicitError()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_new",
              "canonicalName": "Лира",
              "nameVariants": { "default": "Лира", "feminine": "Лира", "masculine": "Лира", "neutral": "Лира" },
              "manifestation": {
                "currentDisplayName": "Лира",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "First guardian still requires explicit create over an empty validated baseline."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_new", "title": "Тихий прилив" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 25, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 18, "tier": "Угасающая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_empty_materialization_without_create.json",
            """
            {
              "guardians": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_materialized_without_create_surface", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actual, "guardian_new", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_ValidSameTurnCreateAuthorizesCurrentGuardianMaterialization()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "UpdateGuardians": [
            {
              "command": "create",
              "data": {
                "guardianId": "guardian_new",
                "canonicalName": "Лира",
                "nameVariants": { "default": "Лира", "feminine": "Лира", "masculine": "Лира", "neutral": "Лира" },
                "manifestation": {
                  "currentDisplayName": "Лира",
                  "formFlexibility": "selective",
                  "currentPresentationStyle": "feminine",
                  "currentPronouns": "она/её",
                  "appearanceDescription": "Valid same-turn create should authorize current guardian materialization."
                },
                "manifestationHistory": [],
                "domain": "Tide",
                "abode": { "abodeId": "abode_new", "title": "Тихий прилив" },
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
          ],
          "guardians": [
            {
              "guardianId": "guardian_new",
              "canonicalName": "Лира",
              "nameVariants": { "default": "Лира", "feminine": "Лира", "masculine": "Лира", "neutral": "Лира" },
              "manifestation": {
                "currentDisplayName": "Лира",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Current canonical state may include the guardian created earlier in the same turn."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_new", "title": "Тихий прилив" },
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
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_empty_valid_create_materialization.json",
            """
            {
              "guardians": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_create_duplicate_guardian_id", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actual, "guardian_new", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_materialized_without_create_surface", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actual, "guardian_new", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_CurrentGuardianMaterializedStateOutsideAuthorityRaisesExplicitError()
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
                "appearanceDescription": "Current materialized guardian drifts from kernel authority."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_alpha_drift", "title": "Ложная обитель" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 99, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 55, "tier": "Сильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
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
              "appearanceDescription": "Mirror drifts with current guardian."
            },
            "manifestationHistory": [],
            "domain": "Tide",
            "abode": { "abodeId": "abode_alpha_drift", "title": "Ложная обитель" },
            "personalityProfile": {
              "archetype": "Tide Keeper",
              "speechPattern": "Measured and tidal",
              "coreValues": [ "balance", "memory", "patience" ]
            },
            "relationshipData": { "currentReputation": 99, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 55, "tier": "Сильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_materialized_drift.json",
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
                    "appearanceDescription": "Validated pre-turn authority."
                  },
                  "manifestationHistory": [],
                  "domain": "Tide",
                  "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
                  "personalityProfile": {
                    "archetype": "Tide Keeper",
                    "speechPattern": "Measured and tidal",
                    "coreValues": [ "balance", "memory", "patience" ]
                  },
                  "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """));

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_GuardianResidentCrossRefsUseAuthorityBackedAbodeMap()
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
                "appearanceDescription": "Raw current guardian abode drift must not relax resident crossrefs."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_raw_drift", "title": "Ложная обитель" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianAbodeResidentState.StatePath, """
        {
          "entries": [
            {
              "residentId": "resident_alpha",
              "guardianId": "guardian_alpha",
              "abodeId": "abode_raw_drift",
              "displayName": "Свидетель дрейфа"
            }
          ],
          "rosterReceipts": [],
          "interactionReceipts": [],
          "historyLog": []
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_resident_authority_abode.json",
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
                    "appearanceDescription": "Validated pre-turn guardian abode must remain authoritative."
                  },
                  "manifestationHistory": [],
                  "domain": "Tide",
                  "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
                  "personalityProfile": {
                    "archetype": "Tide Keeper",
                    "speechPattern": "Measured and tidal",
                    "coreValues": [ "balance", "memory", "patience" ]
                  },
                  "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_abode_resident_abode_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_IdleChaosSeaGuardiansWithoutPendingSnapshot_DoesNotRequirePreTurnGuardiansSnapshot()
    {
        _fs.DeleteFile("input/turn_request.json");
        _fs.DeleteFile("ready/turn_complete.json");
        _fs.DeleteFile("ready/turn_error.json");
        _fs.DeleteFile("game_state/control/pending_turn_snapshot.json");

        await WriteGuardianRawWithoutValidatedSnapshotAsync("""
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
                "appearanceDescription": "Idle Chaos Sea state before the next turn is dispatched."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_missing_validated_preturn_guardians_snapshot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_UsableManifestWithoutGuardiansSnapshotEntryRaisesExplicitSnapshotError()
    {
        await WriteGuardianRawWithoutValidatedSnapshotAsync("""
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
                "appearanceDescription": "Current guardians[] requires a validated snapshot entry when manifest is usable."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
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
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_state_guardian_snapshot_entry_missing.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_missing_validated_preturn_guardians_snapshot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_GuardianProjectsDoNotFallbackToCurrentGuardiansWithoutValidatedBaseline()
    {
        await WriteGuardianRawWithoutValidatedSnapshotAsync("""
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
                "appearanceDescription": "Project validation must not derive guardian provenance from current guardians[]."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "startGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_missing_baseline",
                "projectType": "abode_expansion",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Старт без validated baseline"
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_project_missing_guardians_snapshot.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_missing_validated_preturn_guardians_snapshot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_GuardianPowerEventsDoNotFallbackToCurrentGuardiansWithoutValidatedBaseline()
    {
        await WriteGuardianRawWithoutValidatedSnapshotAsync("""
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
                "appearanceDescription": "guardianPowerEvents must not derive authority from current guardian state when baseline provenance is missing."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "evt_missing_baseline_guardian_power",
              "guardianId": "guardian_alpha",
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_missing_baseline",
              "title": "Power event without validated guardians baseline",
              "summary": "Validator must fail before resolving guardian/project authority from current state.",
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

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_guardian_power_event_missing_guardians_snapshot.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_power_event_missing_validated_preturn_guardians_snapshot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_SystemGuardianPresetDoesNotSkipWhenValidatedGuardiansSnapshotIsMissing()
    {
        await WriteGuardianRawWithoutValidatedSnapshotAsync("""
        {
          "guardians": [],
          "pendingGuardianCreation": {
            "mode": "system_preset",
            "presetId": "azalia"
          },
          "activeGuardian": {
            "guardianId": "guardian_azalia",
            "canonicalName": "Азалия",
            "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Pending system preset must not disappear when guardians snapshot provenance is missing."
            },
            "manifestationHistory": [],
            "domain": "Tide",
            "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
            "personalityProfile": {
              "archetype": "Tide Keeper",
              "speechPattern": "Measured and tidal",
              "coreValues": [ "balance", "memory", "patience" ]
            },
            "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] },
            "sourcePreset": { "presetId": "azalia", "displayName": "Азалия", "version": "1.0", "library": "built_in" }
          }
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_system_guardian_preset_missing_guardians_snapshot.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "system_guardian_pending_preset_missing_validated_guardians_snapshot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_SystemGuardianPresetDoesNotTreatReadableInvalidGuardiansSnapshotAsUsableBaseline()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [],
          "pendingGuardianCreation": {
            "mode": "system_preset",
            "presetId": "azalia"
          },
          "activeGuardian": {
            "guardianId": "guardian_azalia",
            "canonicalName": "Азалия",
            "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Readable but invalid snapshot guardians must not authorize system preset baseline."
            },
            "manifestationHistory": [],
            "domain": "Tide",
            "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
            "personalityProfile": {
              "archetype": "Tide Keeper",
              "speechPattern": "Measured and tidal",
              "coreValues": [ "balance", "memory", "patience" ]
            },
            "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] },
            "sourcePreset": { "presetId": "azalia", "displayName": "Азалия", "version": "1.0", "library": "built_in" }
          }
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_readable_but_invalid_for_system_preset.json",
            """
            {
              "guardians": [
                {
                  "guardianId": "guardian_azalia",
                  "canonicalName": "Азалия"
                }
              ],
              "pendingGuardianCreation": {
                "mode": "system_preset",
                "presetId": "azalia"
              }
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "system_guardian_pending_preset_missing_validated_guardians_snapshot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_SystemGuardianPresetDoesNotUseRawMirrorWhenAuthorityActiveGuardianIsMissing()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [],
          "pendingGuardianCreation": {
            "mode": "system_preset",
            "presetId": "azalia"
          },
          "activeGuardian": {
            "guardianId": "guardian_azalia",
            "canonicalName": "Азалия",
            "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Raw mirror must not materialize pending preset by itself."
            },
            "manifestationHistory": [],
            "domain": "Tide",
            "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
            "personalityProfile": {
              "archetype": "Tide Keeper",
              "speechPattern": "Measured and tidal",
              "coreValues": [ "balance", "memory", "patience" ]
            },
            "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] },
            "sourcePreset": { "presetId": "azalia", "displayName": "Азалия", "version": "1.0", "library": "built_in" }
          }
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_pending_preset_without_authority_active_guardian.json",
            """
            {
              "guardians": [],
              "pendingGuardianCreation": {
                "mode": "system_preset",
                "presetId": "azalia"
              }
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        var missingActiveGuardianIssue = Assert.Single(
            issues,
            issue => string.Equals(issue.Code, "system_guardian_pending_preset_missing_active_guardian", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("raw mirror without current guardian authority", missingActiveGuardianIssue.Actual ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("strict kernel authority", missingActiveGuardianIssue.Actual ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "system_guardian_pending_preset_not_materialized", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_SystemGuardianPreset_NotMaterializedUsesAuthorityBackedCreate()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "UpdateGuardians": [
            {
              "command": "create",
              "data": {
                "guardianId": "guardian_azalia",
                "canonicalName": "Азалия",
                "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": "Азалия", "neutral": "Азалия" },
                "manifestation": {
                  "currentDisplayName": "Азалия",
                  "formFlexibility": "selective",
                  "currentPresentationStyle": "feminine",
                  "currentPronouns": "она/её",
                  "appearanceDescription": "Authority-backed create should drive pending preset mismatch validation."
                },
                "manifestationHistory": [],
                "domain": "Tide",
                "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
                "personalityProfile": {
                  "archetype": "Tide Keeper",
                  "speechPattern": "Measured and tidal",
                  "coreValues": [ "balance", "memory", "patience" ]
                },
                "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                "guardianRelationships": [],
                "mood": { "current": "focused", "intensity": 40, "reason": "Materializing pending preset.", "since": 10 },
                "loreFragments": [
                  { "fragmentId": "guardian_azalia_lore_1", "category": "personal_history", "title": "Первая искра", "content": null, "requiredReputation": 0 },
                  { "fragmentId": "guardian_azalia_lore_2", "category": "cosmic_secret", "title": "Тайна пламени", "content": null, "requiredReputation": 50 },
                  { "fragmentId": "guardian_azalia_lore_3", "category": "domain_mastery", "title": "Власть слова", "content": null, "requiredReputation": 130 },
                  { "fragmentId": "guardian_azalia_lore_4", "category": "lost_world", "title": "Следы обители", "content": null, "requiredReputation": 230 },
                  { "fragmentId": "guardian_azalia_lore_5", "category": "other_guardians", "title": "Имена союзников", "content": null, "requiredReputation": 0 },
                  { "fragmentId": "guardian_azalia_lore_6", "category": "soul_mechanics", "title": "Память души", "content": null, "requiredReputation": 50 },
                  { "fragmentId": "guardian_azalia_lore_7", "category": "personal_history", "title": "Возвращение искры", "content": null, "requiredReputation": 130 }
                ],
                "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                "gachaSystem": { "chargesPerReturn": 2, "chargesUsedThisReturn": 0, "gachaHistory": [] },
                "sourcePreset": { "presetId": "wrong_preset", "displayName": "Кто-то другой", "version": "1.0", "library": "built_in" }
              }
            }
          ],
          "pendingGuardianCreation": {
            "mode": "system_preset",
            "presetId": "azalia"
          }
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_pending_preset_target_mismatch_create.json",
            """
            {
              "guardians": [],
              "pendingGuardianCreation": {
                "mode": "system_preset",
                "presetId": "azalia"
              }
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "system_guardian_pending_preset_not_materialized", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "system_guardian_pending_preset_missing_active_guardian", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_SystemGuardianPresetDoesNotSkipWhenValidatedGuardiansManifestIsUnusable()
    {
        await WriteGuardianRawWithoutValidatedSnapshotAsync("""
        {
          "guardians": [],
          "pendingGuardianCreation": {
            "mode": "system_preset",
            "presetId": "azalia"
          },
          "activeGuardian": {
            "guardianId": "guardian_azalia",
            "canonicalName": "Азалия",
            "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Unusable validated manifest must still fail-close system preset materialization."
            },
            "manifestationHistory": [],
            "domain": "Tide",
            "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
            "personalityProfile": {
              "archetype": "Tide Keeper",
              "speechPattern": "Measured and tidal",
              "coreValues": [ "balance", "memory", "patience" ]
            },
            "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] },
            "sourcePreset": { "presetId": "azalia", "displayName": "Азалия", "version": "1.0", "library": "built_in" }
          }
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_soul_state_system_guardian_preset_unusable_manifest.json",
            """
            {
              "currentRealm": "Chaos Sea"
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

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "system_guardian_pending_preset_missing_validated_guardians_snapshot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_CurrentGuardiansFailClosedWhenValidatedGuardiansManifestIsMissing()
    {
        await WriteGuardianRawWithoutValidatedSnapshotAsync("""
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
                "appearanceDescription": "Current guardians[] must not become authoritative when the validated guardians manifest is missing."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_missing_validated_preturn_guardians_snapshot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_GuardianProjectsDoNotFallbackToCurrentTrackerWithoutValidatedTrackerBaseline()
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
                "appearanceDescription": "Guardian project validation requires a validated pre-turn tracker baseline."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "startGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_tracker_gap",
                "projectType": "abode_expansion",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Трекер без validated baseline"
              }
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
                "projectId": "proj_tracker_gap",
                "projectType": "abode_expansion",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Current-only tracker state must not authorize guardian projects."
              }
            }
          ],
          "completedProjects": []
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_project_tracker_gap.json",
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
                    "appearanceDescription": "Validated guardian baseline exists, but tracker baseline is missing."
                  },
                  "manifestationHistory": [],
                  "domain": "Tide",
                  "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
                  "personalityProfile": {
                    "archetype": "Tide Keeper",
                    "speechPattern": "Measured and tidal",
                    "coreValues": [ "balance", "memory", "patience" ]
                  },
                  "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 36, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await RemoveTrackedFileFromCurrentPendingTurnSnapshotAsync(GuardianProjectState.TrackerPath);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_missing_validated_preturn_tracker_snapshot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_DirectTrackerStateFailsClosedWithoutValidatedTrackerBaseline()
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
                "appearanceDescription": "Direct tracker state must fail closed without validated tracker provenance."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 36, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
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
                "projectId": "proj_direct_tracker_only",
                "projectType": "abode_expansion",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Direct tracker state without validated baseline"
              }
            }
          ],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_direct_tracker_state.json",
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
                    "appearanceDescription": "Validated guardian baseline exists, but tracker baseline is missing."
                  },
                  "manifestationHistory": [],
                  "domain": "Tide",
                  "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
                  "personalityProfile": {
                    "archetype": "Tide Keeper",
                    "speechPattern": "Measured and tidal",
                    "coreValues": [ "balance", "memory", "patience" ]
                  },
                  "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 36, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await RemoveTrackedFileFromCurrentPendingTurnSnapshotAsync(GuardianProjectState.TrackerPath);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_missing_validated_preturn_tracker_snapshot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_CurrentOnlyTrackerStateDoesNotAuthorizeGuardianProjectUpdates()
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
                "appearanceDescription": "Current tracker state must not authorize updates."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 36, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
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
                "projectId": "proj_current_only_update",
                "projectType": "abode_expansion",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Current tracker must not authorize this update"
              }
            }
          ],
          "guardianProjectUpdates": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_current_only_update",
              "workDone": 3
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_current_only_project_update.json",
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
                  "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 36, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_tracker_current_only_project_update.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_update_unknown_project_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_SemanticallyInvalidValidatedTrackerBaselineFailsClosedBeforeUnknownProjectIds()
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
                "appearanceDescription": "Shared tracker identity knowledge must fail closed on semantically invalid validated baseline."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 36, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "guardianProjectUpdates": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_semantic_invalid_snapshot",
              "workDone": 3
            }
          ],
          "completeGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_semantic_invalid_completion",
              "finalState": "Completed",
              "outcome": "Semantically invalid validated tracker baseline must fail before unknown project diagnostics."
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_semantic_invalid_tracker_identity.json",
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
                    "appearanceDescription": "Validated guardian baseline exists for semantic-invalid tracker regression."
                  },
                  "manifestationHistory": [],
                  "domain": "Tide",
                  "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
                  "personalityProfile": {
                    "archetype": "Tide Keeper",
                    "speechPattern": "Measured and tidal",
                    "coreValues": [ "balance", "memory", "patience" ]
                  },
                  "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 36, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_tracker_semantic_invalid_project_identity.json",
            """
            {
              "activeProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_semantic_invalid_snapshot",
                    "projectType": "abode_expansion",
                    "projectTier": "minor",
                    "projectMode": "internal",
                    "projectName": "First conflicting active project",
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
                    "projectId": "proj_conflicting_snapshot_entry",
                    "projectType": "abode_fortification",
                    "projectTier": "minor",
                    "projectMode": "internal",
                    "projectName": "Second conflicting active project",
                    "activeState": "Duplicate guardian slot must invalidate shared tracker authority",
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

        await WriteRawAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": []
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_project_invalid_validated_preturn_tracker_snapshot", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_project_update_unknown_project_id", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_project_completion_unknown_project_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_CurrentUnreadableTrackerFailsClosedForGuardianTradeDerivedState()
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
                "appearanceDescription": "Trade validation must not fallback to stale pre-turn tracker."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 52, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 120, "tier": "Возвышенная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "tradeInventory": {
                "tradeCycleId": "cycle-alpha",
                "generatedAtUtc": "2026-03-24T00:00:00Z",
                "generationReputationTier": "Friendly",
                "pricingReputationTier": "Friendly",
                "items": []
              },
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_trade_tracker_unreadable.json",
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
                  "relationshipData": { "currentReputation": 52, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 120, "tier": "Возвышенная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "tradeInventory": {
                    "tradeCycleId": "cycle-alpha",
                    "generatedAtUtc": "2026-03-24T00:00:00Z",
                    "generationReputationTier": "Friendly",
                    "pricingReputationTier": "Friendly",
                    "items": []
                  },
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_tracker_trade_tracker_unreadable.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, "{ unreadable tracker");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_inventory_missing_current_tracker_authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_SemanticallyInvalidValidatedTrackerBaselineFailsClosedForGuardianTradeDerivedState()
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
                "appearanceDescription": "Trade validation must reject semantically invalid validated tracker baseline."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 52, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 120, "tier": "Возвышенная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "tradeInventory": {
                "tradeCycleId": "cycle-alpha",
                "generatedAtUtc": "2026-03-24T00:00:00Z",
                "generationReputationTier": "Friendly",
                "pricingReputationTier": "Friendly",
                "items": []
              },
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_trade_semantic_invalid_tracker.json",
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
                  "relationshipData": { "currentReputation": 52, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 120, "tier": "Возвышенная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "tradeInventory": {
                    "tradeCycleId": "cycle-alpha",
                    "generatedAtUtc": "2026-03-24T00:00:00Z",
                    "generationReputationTier": "Friendly",
                    "pricingReputationTier": "Friendly",
                    "items": []
                  },
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_tracker_trade_semantic_invalid.json",
            """
            {
              "activeProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_trade_semantic_invalid_alpha",
                    "projectType": "abode_expansion",
                    "projectTier": "minor",
                    "projectMode": "internal",
                    "projectName": "First conflicting active project",
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
                    "projectId": "proj_trade_semantic_invalid_beta",
                    "projectType": "abode_fortification",
                    "projectTier": "minor",
                    "projectMode": "internal",
                    "projectName": "Second conflicting active project",
                    "activeState": "Duplicate guardian slot must invalidate current tracker authority",
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
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_inventory_missing_current_tracker_authority", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual != null &&
            issue.Actual.Contains("semantically invalid", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_GuardianTradeInventoryDoesNotUseCompatibilityTrackerProjectionWhenSharedGuardianBaselineIsInvalid()
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
                "appearanceDescription": "Trade validation must not accept compatibility tracker projection after shared guardian baseline failure."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 52, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 120, "tier": "Возвышенная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "tradeInventory": {
                "tradeCycleId": "cycle-alpha",
                "generatedAtUtc": "2026-03-24T00:00:00Z",
                "generationReputationTier": "Friendly",
                "pricingReputationTier": "Friendly",
                "items": []
              },
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_trade_invalid_shared_baseline.json",
            """
            {
              "guardians": {}
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_tracker_trade_invalid_shared_baseline.json",
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
          "temporaryProjectModifiers": []
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_trade_inventory_missing_current_tracker_authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_GuardianQuestLoreOriginsFailClosedWhenCurrentTrackerAuthorityIsUnavailable()
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
                "appearanceDescription": "Quest lore origin validation must fail closed when tracker authority is unavailable."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 52, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 0, "tier": "Слабая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "tradeInventory": { "items": [] },
              "questManagement": {
                "availableQuests": [
                  {
                    "questId": "quest_archive_hook",
                    "title": "Архивный след",
                    "difficulty": "normal",
                    "questOrigin": "archive_consultation_hook",
                    "sourceProjectId": "proj_archive_hook"
                  }
                ],
                "activeQuests": [],
                "completedQuests": []
              },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_quest_tracker_authority_invalid.json",
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
                  "relationshipData": { "currentReputation": 52, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 0, "tier": "Слабая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "tradeInventory": { "items": [] },
                  "questManagement": {
                    "availableQuests": [
                      {
                        "questId": "quest_archive_hook",
                        "title": "Архивный след",
                        "difficulty": "normal",
                        "questOrigin": "archive_consultation_hook",
                        "sourceProjectId": "proj_archive_hook"
                      }
                    ],
                    "activeQuests": [],
                    "completedQuests": []
                  },
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_tracker_quest_authority_invalid.json",
            """
            {
              "activeProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_quest_invalid_alpha",
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
                    "projectId": "proj_quest_invalid_beta",
                    "projectType": "abode_fortification",
                    "projectTier": "minor",
                    "projectMode": "internal",
                    "projectName": "Второй конфликтующий проект",
                    "activeState": "Duplicate guardian slot must invalidate quest tracker authority",
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
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_quest_management_missing_current_tracker_authority", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_available_quests_limit_exceeded", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_NonCreateGuardianCommandUsesPreTurnOnlySequentialState()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "UpdateGuardians": [
            {
              "command": "completeQuest",
              "guardianId": "guardian_alpha",
              "questId": "quest_missing_from_preturn_state",
              "outcome": "success"
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_preturn_only_sequential_state.json",
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
                    "appearanceDescription": "Sequential command validation must still see pre-turn guardian state even when current guardians[] omits the guardian."
                  },
                  "manifestationHistory": [],
                  "domain": "Tide",
                  "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
                  "personalityProfile": {
                    "archetype": "Tide Keeper",
                    "speechPattern": "Measured and tidal",
                    "coreValues": [ "balance", "memory", "patience" ]
                  },
                  "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 36, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "questManagement": {
                    "availableQuests": [
                      {
                        "questId": "quest_existing",
                        "title": "Существующий квест",
                        "description": "Only this quest exists in pre-turn state.",
                        "difficulty": "minor"
                      }
                    ],
                    "activeQuests": [],
                    "completedQuests": []
                  },
                  "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_complete_quest_unknown_quest_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_InvalidFirstNonCreateGuardianCommandDoesNotMutateSequentialStateForLaterCommand()
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
                "appearanceDescription": "Sequential authorization must ignore invalid earlier command mutations."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 36, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "questManagement": {
                "availableQuests": [],
                "activeQuests": [
                  {
                    "questId": "quest_alpha",
                    "questName": "Удержать контур",
                    "difficulty": "hard"
                  }
                ],
                "completedQuests": []
              },
              "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "UpdateGuardians": [
            {
              "command": "completeQuest",
              "guardianId": "guardian_alpha",
              "questId": "quest_alpha",
              "outcome": "success"
            },
            {
              "command": "completeQuest",
              "guardianId": "guardian_alpha",
              "questId": "quest_alpha",
              "outcome": "success",
              "questPowerAudit": {
                "questDifficultyTier": "hard",
                "outcome": "success",
                "supportsCurrentProject": false,
                "defendsAgainstRivalPressure": false,
                "baseDelta": 5,
                "bonusDelta": 0,
                "finalDelta": 5
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_invalid_first_non_create_sequence.json",
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
                    "appearanceDescription": "Validated pre-turn quest state."
                  },
                  "manifestationHistory": [],
                  "domain": "Tide",
                  "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
                  "personalityProfile": {
                    "archetype": "Tide Keeper",
                    "speechPattern": "Measured and tidal",
                    "coreValues": [ "balance", "memory", "patience" ]
                  },
                  "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 36, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "questManagement": {
                    "availableQuests": [],
                    "activeQuests": [
                      {
                        "questId": "quest_alpha",
                        "questName": "Удержать контур",
                        "difficulty": "hard"
                      }
                    ],
                    "completedQuests": []
                  },
                  "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_complete_quest_missing_power_audit", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_complete_quest_unknown_quest_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_ProcessGachaUsesAuthoritySequentialStateInsteadOfRawCurrentGuardianDrift()
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
                "appearanceDescription": "Raw current gacha state should not soften authorization."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 0, "tier": "Угасающая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "UpdateGuardians": [
            {
              "command": "processGacha",
              "guardianId": "guardian_alpha",
              "inkFeathersSpent": 50,
              "result": {
                "relicId": "relic_alpha",
                "name": "Тестовая реликвия",
                "rarity": "Common",
                "quality": "Common"
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_process_gacha_authority_sequence.json",
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
                    "appearanceDescription": "Validated sequential gacha state."
                  },
                  "manifestationHistory": [],
                  "domain": "Tide",
                  "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
                  "personalityProfile": {
                    "archetype": "Tide Keeper",
                    "speechPattern": "Measured and tidal",
                    "coreValues": [ "balance", "memory", "patience" ]
                  },
                  "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 0, "tier": "Угасающая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 1, "gachaHistory": [] }
                }
              ]
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_process_gacha_no_remaining_charges", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateCrossReferences_GuardianNpcBoundaryFailsClosedWhenGuardianBaselineIsBroken()
    {
        await WriteGuardianRawWithoutValidatedSnapshotAsync("""
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
                "appearanceDescription": "NPC boundary validation must fail closed when guardian provenance is broken."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_alpha", "title": "Тихий прилив" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 18, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WriteRawAsync("game_state/npcs/npc_core.json", """
        {
          "NPCsInScene": [
            {
              "npcId": "guardian_alpha",
              "name": "Азалия"
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_npc_boundary_missing_validated_preturn_guardians_snapshot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateCrossReferences_GuardianNpcBoundaryFailsClosedWhenReadableSnapshotGuardiansAreStrictlyInvalid()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": []
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_readable_but_invalid_for_npc_boundary.json",
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

        await WriteRawAsync("game_state/npcs/npc_core.json", """
        {
          "NPCsInScene": [
            {
              "npcId": "guardian_alpha",
              "name": "Азалия"
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(GuardianValidationProfiles.AcceptedAuthority);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_npc_boundary_missing_validated_preturn_guardians_snapshot", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_leaked_into_npc_surface", StringComparison.OrdinalIgnoreCase));
    }
}

