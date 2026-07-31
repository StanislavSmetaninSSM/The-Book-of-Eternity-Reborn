using System.Security.Cryptography;
using System.Reflection;
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

public sealed class GuardianPolicyKernelTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public GuardianPolicyKernelTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-guardian-policy-kernel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task ReadCurrentTurnNumberForProjectAuthority_UsesCanonicalRelativeRequestPath()
    {
        await WriteRawAsync("input/turn_request.json", """
        {
          "sessionId": "guardian-relative-read-session",
          "requestId": "guardian-relative-read-request",
          "turnNumber": 27
        }
        """);

        var validator = new ValidationService(
            _fs,
            NullLogger<ValidationService>.Instance);
        var method = typeof(ValidationService).GetMethod(
            "ReadCurrentTurnNumberForProjectAuthority",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Guardian turn-authority reader was not found.");

        var turnNumber = Assert.IsType<int>(method.Invoke(validator, null));

        Assert.Equal(27, turnNumber);
    }

    [Fact]
    public async Task DebugGuardianPolicyContext_MissingManifestReportsMissingSnapshotStatus()
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
                "appearanceDescription": "Kernel should distinguish missing manifest from file-level failures."
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
              "mood": { "current": "focused", "intensity": 40, "reason": "Calm tide.", "since": 10 },
              "loreFragments": [
                { "fragmentId": "guardian_alpha_lore_1", "category": "personal_history", "title": "Берег памяти", "content": null, "requiredReputation": 0 },
                { "fragmentId": "guardian_alpha_lore_2", "category": "cosmic_secret", "title": "Тайна глубины", "content": null, "requiredReputation": 50 },
                { "fragmentId": "guardian_alpha_lore_3", "category": "domain_mastery", "title": "Узел течений", "content": null, "requiredReputation": 130 },
                { "fragmentId": "guardian_alpha_lore_4", "category": "lost_world", "title": "Затонувший берег", "content": null, "requiredReputation": 230 },
                { "fragmentId": "guardian_alpha_lore_5", "category": "other_guardians", "title": "Имена в пене", "content": null, "requiredReputation": 0 },
                { "fragmentId": "guardian_alpha_lore_6", "category": "soul_mechanics", "title": "Память соли", "content": null, "requiredReputation": 50 },
                { "fragmentId": "guardian_alpha_lore_7", "category": "personal_history", "title": "Возвращение волны", "content": null, "requiredReputation": 130 }
              ],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var snapshot = await validator.DebugResolveGuardianPolicyContextAsync();

        Assert.Equal("Missing", snapshot.ManifestStatus);
        Assert.Equal("MissingManifest", snapshot.PreTurnGuardiansSnapshotFileStatus);
        Assert.Equal(1, snapshot.CurrentGuardianCount);
        Assert.Equal(0, snapshot.PreTurnGuardianCount);
    }

    [Fact]
    public async Task DebugGuardianPolicyContext_EmptyValidatedBaselineRemainsUsableBaseline()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": []
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/kernel_empty_guardians_baseline.json",
            """
            {
              "guardians": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var snapshot = await validator.DebugResolveGuardianPolicyContextAsync();

        Assert.Equal("Usable", snapshot.ManifestStatus);
        Assert.Equal("Usable", snapshot.PreTurnGuardiansSnapshotFileStatus);
        Assert.True(snapshot.HasPreTurnRoot);
        Assert.Equal(0, snapshot.PreTurnGuardianCount);
        Assert.Empty(snapshot.BaselineGuardianIds);
    }

    [Fact]
    public async Task DebugGuardianPolicyContext_ValidSameTurnCreateIsCapturedAsAuthorizedCreate()
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
                  "appearanceDescription": "Kernel should track authorized same-turn creates."
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
                "mood": { "current": "focused", "intensity": 40, "reason": "A newly formed purpose.", "since": 18 },
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
            "test_backups/kernel_empty_guardians_for_create.json",
            """
            {
              "guardians": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var snapshot = await validator.DebugResolveGuardianPolicyContextAsync();

        Assert.Equal(1, snapshot.AuthorizedSameTurnCreateCount);
        Assert.Contains("guardian_new", snapshot.AuthorizedSameTurnCreateGuardianIds);
        Assert.Contains("guardian_new", snapshot.AuthoritativeGuardianIds);
    }

    [Fact]
    public async Task ValidateGameState_FreeformSameTurnCreateAuthorizesMaterializedGuardianMirrors()
    {
        var guardian = BuildCanonicalGuardian(
            "guard_freeform_selena_shadow_001",
            "Хранительница Селена Теневая",
            reputation: 0,
            power: 10,
            appearanceDescription: "Высокая женственная фигура в темной мантии с серебряной окантовкой.");
        guardian["guardianName"] = "Хранительница Селена Теневая";
        guardian["name"] = "Хранительница Селена Теневая";
        guardian["displayName"] = "Хранительница Селена Теневая";
        guardian["originType"] = "freeform";
        guardian["sourceReason"] = "pending_guardian_creation_freeform";
        guardian["sourceRequestId"] = "test-freeform-request";
        guardian["abode"] = new JsonObject
        {
            ["abodeId"] = "abode_selena_infinite_archives_001",
            ["name"] = "Башня Бесконечных Архивов",
            ["displayName"] = "Башня Бесконечных Архивов",
            ["isDiscovered"] = true,
            ["sourceReason"] = "pending_guardian_creation_freeform"
        };

        await WriteRawAsync(
            "game_state/meta/guardians.json",
            SerializeJson(new JsonObject
            {
                ["UpdateGuardians"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["command"] = "create",
                        ["data"] = guardian.DeepClone()
                    }
                },
                ["guardians"] = new JsonArray
                {
                    guardian.DeepClone()
                },
                ["activeGuardian"] = guardian.DeepClone(),
                ["chaosSeaNavigation"] = new JsonObject
                {
                    ["currentAbodeId"] = "abode_selena_infinite_archives_001",
                    ["discoveredAbodes"] = new JsonArray("abode_selena_infinite_archives_001"),
                    ["discoveredAbodeDetails"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["abodeId"] = "abode_selena_infinite_archives_001",
                            ["guardianId"] = "guard_freeform_selena_shadow_001",
                            ["displayName"] = "Башня Бесконечных Архивов",
                            ["sourceReason"] = "pending_guardian_creation_freeform"
                        }
                    }
                }
            }));

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/kernel_empty_guardians_for_freeform_create.json",
            """
            {
              "guardians": [],
              "activeGuardian": null,
              "chaosSeaNavigation": {
                "currentAbodeId": null,
                "discoveredAbodes": []
              }
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_materialized_without_create_surface", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_scope_invalid_active_guardian_identity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_SameTurnCreateAuthorizesFollowUpGuardianCommandWithoutPreTurnBaseline()
    {
        var createdGuardian = BuildCanonicalGuardian("guardian_new", "Лира", reputation: 25, power: 18);
        var root = new JsonObject
        {
            ["UpdateGuardians"] = new JsonArray
            {
                new JsonObject
                {
                    ["command"] = "create",
                    ["data"] = createdGuardian.DeepClone()
                },
                new JsonObject
                {
                    ["command"] = "updateReputation",
                    ["guardianId"] = "guardian_new",
                    ["reputationChange"] = 5,
                    ["reason"] = "Follow-up command for the Guardian created earlier in this response."
                }
            }
        };

        await WriteRawAsync("game_state/meta/guardians.json", SerializeJson(root));

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_commands_missing_validated_preturn_guardians_snapshot", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".UpdateGuardians[1]", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_non_create_unknown_guardian", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.EndsWith(".UpdateGuardians[1].guardianId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_GuardianProcessGachaRequiresCurrentTurnBaseRarity()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "sessionId": "test-session", "requestId": "missing-gacha-base", "turnNumber": 12 }
        """);
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/kernel_guardian_process_gacha_requires_base_rarity.json",
            SerializeJson(BuildGuardiansRoot(BuildCanonicalGuardian("guardian_alpha", "Азалия", reputation: 80, power: 40))));

        var root = new JsonObject
        {
            ["UpdateGuardians"] = new JsonArray
            {
                new JsonObject
                {
                    ["command"] = "processGacha",
                    ["guardianId"] = "guardian_alpha",
                    ["inkFeathersSpent"] = 25,
                    ["result"] = new JsonObject
                    {
                        ["relicId"] = "relic_alpha",
                        ["name"] = "Осколок прилива",
                        ["rarity"] = "rare"
                    }
                }
            },
            ["metaStateUpdates"] = new JsonObject
            {
                ["inkFeatherChanges"] = new JsonObject
                {
                    ["spend"] = 25
                },
                ["soulRelicOperations"] = new JsonObject
                {
                    ["addRelic"] = new JsonObject
                    {
                        ["relicId"] = "relic_alpha",
                        ["name"] = "Осколок прилива",
                        ["rarity"] = "rare"
                    }
                }
            }
        };
        await WriteRawAsync("game_state/meta/guardians.json", SerializeJson(root));

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "guardian_process_gacha_missing_or_invalid_base_rarity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DebugGuardianPolicyContext_RawCurrentGuardiansDoNotOverrideAuthorityRoot()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Подмена",
              "nameVariants": { "default": "Подмена", "feminine": "Подмена", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Подмена",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Raw current guardians should not override authority."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_fake", "title": "Ложная обитель" },
              "personalityProfile": {
                "archetype": "False Tide",
                "speechPattern": "Imitative",
                "coreValues": [ "deception" ]
              },
              "relationshipData": { "currentReputation": 999, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 777, "tier": "Сияющая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "mood": { "current": "distorted", "intensity": 90, "reason": "This should not become authority.", "since": 1 },
              "loreFragments": [
                { "fragmentId": "guardian_alpha_lore_1", "category": "personal_history", "title": "Ложное имя", "content": null, "requiredReputation": 0 },
                { "fragmentId": "guardian_alpha_lore_2", "category": "cosmic_secret", "title": "Ложная тайна", "content": null, "requiredReputation": 50 },
                { "fragmentId": "guardian_alpha_lore_3", "category": "domain_mastery", "title": "Ложный узел", "content": null, "requiredReputation": 130 },
                { "fragmentId": "guardian_alpha_lore_4", "category": "lost_world", "title": "Ложный берег", "content": null, "requiredReputation": 230 },
                { "fragmentId": "guardian_alpha_lore_5", "category": "other_guardians", "title": "Ложные имена", "content": null, "requiredReputation": 0 },
                { "fragmentId": "guardian_alpha_lore_6", "category": "soul_mechanics", "title": "Ложная память", "content": null, "requiredReputation": 50 },
                { "fragmentId": "guardian_alpha_lore_7", "category": "personal_history", "title": "Ложное возвращение", "content": null, "requiredReputation": 130 }
              ],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/kernel_authority_root_preturn_guardians.json",
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
                    "appearanceDescription": "Authority should stay on validated baseline plus authorized mutations only."
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
                  "mood": { "current": "focused", "intensity": 40, "reason": "Baseline authority.", "since": 10 },
                  "loreFragments": [
                    { "fragmentId": "guardian_alpha_lore_1", "category": "personal_history", "title": "Берег памяти", "content": null, "requiredReputation": 0 },
                    { "fragmentId": "guardian_alpha_lore_2", "category": "cosmic_secret", "title": "Тайна глубины", "content": null, "requiredReputation": 50 },
                    { "fragmentId": "guardian_alpha_lore_3", "category": "domain_mastery", "title": "Узел течений", "content": null, "requiredReputation": 130 },
                    { "fragmentId": "guardian_alpha_lore_4", "category": "lost_world", "title": "Затонувший берег", "content": null, "requiredReputation": 230 },
                    { "fragmentId": "guardian_alpha_lore_5", "category": "other_guardians", "title": "Имена в пене", "content": null, "requiredReputation": 0 },
                    { "fragmentId": "guardian_alpha_lore_6", "category": "soul_mechanics", "title": "Память соли", "content": null, "requiredReputation": 50 },
                    { "fragmentId": "guardian_alpha_lore_7", "category": "personal_history", "title": "Возвращение волны", "content": null, "requiredReputation": 130 }
                  ],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var snapshot = await validator.DebugResolveGuardianPolicyContextAsync();

        Assert.True(snapshot.HasCurrentAuthorityRoot);
        Assert.NotNull(snapshot.CurrentAuthorityRootJson);

        var currentAuthorityRoot = JsonNode.Parse(snapshot.CurrentAuthorityRootJson!)!.AsObject();
        var guardian = Assert.Single(Assert.IsType<JsonArray>(currentAuthorityRoot["guardians"]))!.AsObject();
        var relationshipData = Assert.IsType<JsonObject>(guardian["relationshipData"]);
        var abode = Assert.IsType<JsonObject>(guardian["abode"]);
        var abodePower = Assert.IsType<JsonObject>(guardian["abodePower"]);

        Assert.Equal("Азалия", guardian["canonicalName"]!.GetValue<string>());
        Assert.Equal(18, relationshipData["currentReputation"]!.GetValue<int>());
        Assert.Equal("abode_alpha", abode["abodeId"]!.GetValue<string>());
        Assert.Equal(40, abodePower["currentPower"]!.GetValue<int>());
    }

    [Fact]
    public async Task DebugGuardianPolicyContext_RawGuardianPowerEventsFeedCurrentAuthorityRoot()
    {
        await WriteRawAsync("input/turn_request.json", """
        { "sessionId": "test-session", "requestId": "test-request", "turnNumber": 33 }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardianPowerEvents": [
            {
              "eventId": "evt_kernel_authority_power",
              "guardianId": "guardian_alpha",
              "delta": 3,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_kernel_authority",
              "title": "Kernel authority power event",
              "summary": "Current guardian authority should include validated current power events.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_kernel_authority",
                "baseDelta": 3,
                "finalDelta": 3,
                "inkFeathersOffered": 150,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/kernel_authority_power_event_guardians.json",
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
                    "appearanceDescription": "Authority power should include current events."
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
                  "mood": { "current": "focused", "intensity": 40, "reason": "Baseline power.", "since": 10 },
                  "loreFragments": [
                    { "fragmentId": "guardian_alpha_lore_1", "category": "personal_history", "title": "Берег памяти", "content": null, "requiredReputation": 0 },
                    { "fragmentId": "guardian_alpha_lore_2", "category": "cosmic_secret", "title": "Тайна глубины", "content": null, "requiredReputation": 50 },
                    { "fragmentId": "guardian_alpha_lore_3", "category": "domain_mastery", "title": "Узел течений", "content": null, "requiredReputation": 130 },
                    { "fragmentId": "guardian_alpha_lore_4", "category": "lost_world", "title": "Затонувший берег", "content": null, "requiredReputation": 230 },
                    { "fragmentId": "guardian_alpha_lore_5", "category": "other_guardians", "title": "Имена в пене", "content": null, "requiredReputation": 0 },
                    { "fragmentId": "guardian_alpha_lore_6", "category": "soul_mechanics", "title": "Память соли", "content": null, "requiredReputation": 50 },
                    { "fragmentId": "guardian_alpha_lore_7", "category": "personal_history", "title": "Возвращение волны", "content": null, "requiredReputation": 130 }
                  ],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/kernel_authority_power_event_journal.json",
            """
            {
              "entries": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var snapshot = await validator.DebugResolveGuardianPolicyContextAsync();

        Assert.Equal("Resolved", snapshot.CurrentGuardianPowerEventAuthorityStatus);
        Assert.True(snapshot.HasCurrentAuthorityRoot);
        Assert.NotNull(snapshot.CurrentAuthorityRootJson);

        var currentAuthorityRoot = JsonNode.Parse(snapshot.CurrentAuthorityRootJson!)!.AsObject();
        var guardian = Assert.Single(Assert.IsType<JsonArray>(currentAuthorityRoot["guardians"]))!.AsObject();
        var abodePower = Assert.IsType<JsonObject>(guardian["abodePower"]);

        Assert.Equal(43, abodePower["currentPower"]!.GetValue<int>());
    }

    [Fact]
    public async Task DebugGuardianPolicyContext_InvalidRawGuardianPowerEventDoesNotFeedCurrentAuthorityRoot()
    {
        await WriteRawAsync("input/turn_request.json", """
        { "sessionId": "test-session", "requestId": "test-request", "turnNumber": 34 }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardianPowerEvents": [
            {
              "eventId": "evt_kernel_invalid_power",
              "guardianId": "guardian_alpha",
              "delta": 3,
              "reasonType": "invalid_reason",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_kernel_invalid",
              "title": "Invalid power event should not affect authority",
              "summary": "Kernel must not accept invalid reasonType into authority state.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_kernel_invalid",
                "baseDelta": 3,
                "finalDelta": 3,
                "inkFeathersOffered": 150
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/kernel_invalid_power_event_guardians.json",
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
                    "appearanceDescription": "Invalid raw power events must not influence authority."
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
                  "mood": { "current": "focused", "intensity": 40, "reason": "Baseline power.", "since": 10 },
                  "loreFragments": [
                    { "fragmentId": "guardian_alpha_lore_1", "category": "personal_history", "title": "Берег памяти", "content": null, "requiredReputation": 0 },
                    { "fragmentId": "guardian_alpha_lore_2", "category": "cosmic_secret", "title": "Тайна глубины", "content": null, "requiredReputation": 50 },
                    { "fragmentId": "guardian_alpha_lore_3", "category": "domain_mastery", "title": "Узел течений", "content": null, "requiredReputation": 130 },
                    { "fragmentId": "guardian_alpha_lore_4", "category": "lost_world", "title": "Затонувший берег", "content": null, "requiredReputation": 230 },
                    { "fragmentId": "guardian_alpha_lore_5", "category": "other_guardians", "title": "Имена в пене", "content": null, "requiredReputation": 0 },
                    { "fragmentId": "guardian_alpha_lore_6", "category": "soul_mechanics", "title": "Память соли", "content": null, "requiredReputation": 50 },
                    { "fragmentId": "guardian_alpha_lore_7", "category": "personal_history", "title": "Возвращение волны", "content": null, "requiredReputation": 130 }
                  ],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var snapshot = await validator.DebugResolveGuardianPolicyContextAsync();

        Assert.True(snapshot.HasCurrentAuthorityRoot);
        Assert.NotNull(snapshot.CurrentAuthorityRootJson);

        var currentAuthorityRoot = JsonNode.Parse(snapshot.CurrentAuthorityRootJson!)!.AsObject();
        var guardian = Assert.Single(Assert.IsType<JsonArray>(currentAuthorityRoot["guardians"]))!.AsObject();
        var abodePower = Assert.IsType<JsonObject>(guardian["abodePower"]);

        Assert.Equal(40, abodePower["currentPower"]!.GetValue<int>());
    }

    [Fact]
    public async Task DebugGuardianPolicyContext_AuditInvalidRawGuardianPowerEventDoesNotFeedCurrentAuthorityRoot()
    {
        await WriteRawAsync("input/turn_request.json", """
        { "sessionId": "test-session", "requestId": "test-request", "turnNumber": 35 }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardianPowerEvents": [
            {
              "eventId": "evt_kernel_invalid_audit_power",
              "guardianId": "guardian_alpha",
              "delta": 3,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_kernel_invalid_audit",
              "title": "Audit-invalid power event should not affect authority",
              "summary": "Kernel must not accept shape-valid but audit-invalid offering events.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {}
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/kernel_invalid_audit_power_event_guardians.json",
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
                    "appearanceDescription": "Audit-invalid raw power events must not influence authority."
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
                  "mood": { "current": "focused", "intensity": 40, "reason": "Baseline power.", "since": 10 },
                  "loreFragments": [
                    { "fragmentId": "guardian_alpha_lore_1", "category": "personal_history", "title": "Берег памяти", "content": null, "requiredReputation": 0 },
                    { "fragmentId": "guardian_alpha_lore_2", "category": "cosmic_secret", "title": "Тайна глубины", "content": null, "requiredReputation": 50 },
                    { "fragmentId": "guardian_alpha_lore_3", "category": "domain_mastery", "title": "Узел течений", "content": null, "requiredReputation": 130 },
                    { "fragmentId": "guardian_alpha_lore_4", "category": "lost_world", "title": "Затонувший берег", "content": null, "requiredReputation": 230 },
                    { "fragmentId": "guardian_alpha_lore_5", "category": "other_guardians", "title": "Имена в пене", "content": null, "requiredReputation": 0 },
                    { "fragmentId": "guardian_alpha_lore_6", "category": "soul_mechanics", "title": "Память соли", "content": null, "requiredReputation": 50 },
                    { "fragmentId": "guardian_alpha_lore_7", "category": "personal_history", "title": "Возвращение волны", "content": null, "requiredReputation": 130 }
                  ],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var snapshot = await validator.DebugResolveGuardianPolicyContextAsync();

        Assert.True(snapshot.HasCurrentAuthorityRoot);
        Assert.NotNull(snapshot.CurrentAuthorityRootJson);

        var currentAuthorityRoot = JsonNode.Parse(snapshot.CurrentAuthorityRootJson!)!.AsObject();
        var guardian = Assert.Single(Assert.IsType<JsonArray>(currentAuthorityRoot["guardians"]))!.AsObject();
        var abodePower = Assert.IsType<JsonObject>(guardian["abodePower"]);

        Assert.Equal(40, abodePower["currentPower"]!.GetValue<int>());
    }

    [Fact]
    public async Task DebugGuardianPolicyContext_UsableManifestWithoutGuardiansSnapshotEntryReportsMissingSnapshotFile()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": []
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/kernel_only_soul_snapshot.json",
            """
            {
              "currentRealm": "Chaos Sea"
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var snapshot = await validator.DebugResolveGuardianPolicyContextAsync();

        Assert.Equal("Usable", snapshot.ManifestStatus);
        Assert.Equal("MissingSnapshotFile", snapshot.PreTurnGuardiansSnapshotFileStatus);
        Assert.False(snapshot.HasPreTurnRoot);
    }

    [Fact]
    public async Task DebugGuardianProjectTrackerPolicyContext_UnreadableCurrentTrackerDoesNotFallbackToPreTurnAuthority()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": []
        }
        """);
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/kernel_preturn_guardians_empty_for_unreadable_tracker.json",
            """
            {
              "guardians": []
            }
            """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, "{ invalid tracker");
        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/kernel_tracker_baseline.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var snapshot = await validator.DebugResolveGuardianProjectTrackerPolicyContextAsync();

        Assert.True(snapshot.HasPreTurnRoot);
        Assert.Equal("UnreadableCurrentState", snapshot.CurrentStateFailureKind);
        Assert.True(snapshot.HasProjectedAuthorityRoot);
        Assert.False(snapshot.HasCurrentAuthorityRoot);
    }

    [Fact]
    public async Task DebugGuardianProjectTrackerPolicyContext_MissingCurrentTrackerStillKeepsProjectedBaselineAuthorityOnly()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": []
        }
        """);
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/kernel_preturn_guardians_empty_for_missing_tracker.json",
            """
            {
              "guardians": []
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/kernel_tracker_missing_current.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var snapshot = await validator.DebugResolveGuardianProjectTrackerPolicyContextAsync();

        Assert.True(snapshot.HasPreTurnRoot);
        Assert.Equal("MissingCurrentState", snapshot.CurrentStateFailureKind);
        Assert.True(snapshot.HasProjectedAuthorityRoot);
        Assert.False(snapshot.HasCurrentAuthorityRoot);
    }

    [Fact]
    public async Task DebugGuardianProjectTrackerPolicyContext_SemanticallyInvalidPreTurnTrackerDoesNotProjectAuthority()
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
                "appearanceDescription": "Current tracker policy context should reject semantically invalid validated tracker baseline."
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
            "game_state/meta/guardians.json",
            "test_backups/kernel_preturn_guardians_for_semantic_invalid_tracker.json",
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
                  "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
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

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/kernel_semantic_invalid_tracker_baseline.json",
            """
            {
              "activeProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_kernel_semantic_invalid_alpha",
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
                    "projectId": "proj_kernel_semantic_invalid_beta",
                    "projectType": "abode_fortification",
                    "projectTier": "minor",
                    "projectMode": "internal",
                    "projectName": "Second conflicting active project",
                    "activeState": "Duplicate guardian slot must invalidate tracker authority",
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

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var snapshot = await validator.DebugResolveGuardianProjectTrackerPolicyContextAsync();

        Assert.True(snapshot.HasPreTurnRoot);
        Assert.Equal("None", snapshot.CurrentStateFailureKind);
        Assert.False(snapshot.HasProjectedAuthorityRoot);
        Assert.False(snapshot.HasCurrentAuthorityRoot);
    }

    [Fact]
    public async Task DebugGuardianProjectTrackerPolicyContext_SemanticallyInvalidCurrentTrackerDoesNotBuildAuthority()
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
                "appearanceDescription": "Current tracker authority should fail on semantically invalid same-turn commands."
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
            "game_state/meta/guardians.json",
            "test_backups/kernel_preturn_guardians_for_semantic_invalid_current_tracker.json",
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
            "test_backups/kernel_tracker_preturn_for_semantic_invalid_current_tracker.json",
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
          "startGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_kernel_invalid_current_alpha",
                "projectType": "abode_expansion",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Первый конфликтующий старт",
                "activeState": "Preparing",
                "totalWork": 10,
                "workDone": 0,
                "totalStages": 2,
                "currentStage": 0,
                "pressure": 0,
                "stability": 10,
                "startedTurn": 5
              }
            },
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_kernel_invalid_current_beta",
                "projectType": "abode_fortification",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Второй конфликтующий старт",
                "activeState": "Preparing",
                "totalWork": 8,
                "workDone": 0,
                "totalStages": 2,
                "currentStage": 0,
                "pressure": 0,
                "stability": 10,
                "startedTurn": 5
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var snapshot = await validator.DebugResolveGuardianProjectTrackerPolicyContextAsync();

        Assert.True(snapshot.HasPreTurnRoot);
        Assert.Equal("SemanticallyInvalidCurrentState", snapshot.CurrentStateFailureKind);
        Assert.False(snapshot.HasProjectedAuthorityRoot);
        Assert.False(snapshot.HasCurrentAuthorityRoot);
    }

    [Fact]
    public async Task DebugGuardianProjectTrackerPolicyContext_InvalidRawGuardianCreateDoesNotAuthorizeProjectAuthority()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [],
          "UpdateGuardians": [
            {
              "command": "create",
              "data": {
                "guardianId": "guardian_invalid",
                "canonicalName": "Ложный Хранитель",
                "nameVariants": { "default": "Ложный Хранитель" },
                "manifestation": {
                  "currentDisplayName": "Ложный Хранитель"
                }
              }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "startGuardianProjects": [
            {
              "guardianId": "guardian_invalid",
              "project": {
                "projectId": "proj_invalid_create",
                "projectType": "abode_expansion",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Недопустимый старт проекта"
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/kernel_invalid_create_guardians_baseline.json",
            """
            {
              "guardians": []
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/kernel_invalid_create_tracker_baseline.json",
            """
            {
              "activeProjects": [],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var snapshot = await validator.DebugResolveGuardianProjectTrackerPolicyContextAsync();

        Assert.Equal("SemanticallyInvalidCurrentState", snapshot.CurrentStateFailureKind);
        Assert.False(snapshot.HasProjectedAuthorityRoot);
        Assert.False(snapshot.HasCurrentAuthorityRoot);
    }

    [Fact]
    public async Task DebugGuardianProjectTrackerPolicyContext_DuplicateCurrentTemporaryModifiersDoNotAuthorizeProjectAuthority()
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
                "appearanceDescription": "Modifier authority must stay strict."
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
            "test_backups/kernel_duplicate_current_modifier_guardians_baseline.json",
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
            "test_backups/kernel_duplicate_current_modifier_tracker_baseline.json",
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
        var snapshot = await validator.DebugResolveGuardianProjectTrackerPolicyContextAsync();

        Assert.Equal("SemanticallyInvalidCurrentState", snapshot.CurrentStateFailureKind);
        Assert.False(snapshot.HasProjectedAuthorityRoot);
        Assert.False(snapshot.HasCurrentAuthorityRoot);
    }

    [Fact]
    public async Task DebugGuardianProjectTrackerPolicyContext_CommandShapedGuardianMutationDoesNotPromoteCompatibilityTrackerAuthority()
    {
        await WriteRawAsync("input/turn_request.json", """
        { "sessionId": "test-session", "requestId": "test-request", "turnNumber": 52 }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "UpdateGuardians": [
            {
              "command": "completeQuest",
              "guardianId": "guardian_alpha",
              "questId": "quest_power",
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

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "completeGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_power_shift",
              "finalState": "Completed",
              "outcome": "Интрига завершена после усиления хранителя.",
              "targetGuardianId": "guardian_beta"
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/kernel_command_guardian_authority_baseline.json",
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
                    "appearanceDescription": "Kernel should apply command-shaped guardian power changes."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 60, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 50, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [
                    { "targetGuardianId": "guardian_beta", "attitudeScore": -80, "attitudeTier": "enemy", "reason": "Open hostility", "lastChangedAt": null }
                  ],
                  "questManagement": {
                    "availableQuests": [],
                    "activeQuests": [
                      { "questId": "quest_power", "questName": "Усилить нажим", "difficulty": "hard" }
                    ],
                    "completedQuests": []
                  },
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
                    "appearanceDescription": "Target guardian remains in baseline only."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 52, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [
                    { "targetGuardianId": "guardian_alpha", "attitudeScore": -60, "attitudeTier": "rival", "reason": "Hostility", "lastChangedAt": null }
                  ],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/kernel_command_guardian_tracker_baseline.json",
            """
            {
              "activeProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_power_shift",
                    "projectType": "offensive_intrigue",
                    "projectTier": "major",
                    "projectMode": "offensive",
                    "projectName": "Интрига после усиления",
                    "targetGuardianId": "guardian_beta",
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
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var snapshot = await validator.DebugResolveGuardianProjectTrackerPolicyContextAsync();

        Assert.True(snapshot.HasCurrentAuthorityRoot);
        Assert.NotNull(snapshot.CurrentAuthorityRootJson);

        var currentAuthorityRoot = JsonNode.Parse(snapshot.CurrentAuthorityRootJson!)!.AsObject();
        var completedProjects = Assert.IsType<JsonArray>(currentAuthorityRoot["completedProjects"]);
        var completedEntry = Assert.Single(completedProjects)!.AsObject();
        var project = Assert.IsType<JsonObject>(completedEntry["project"]);
        var offensiveImpactAudit = Assert.IsType<JsonObject>(project["offensiveImpactAudit"]);

        Assert.Equal(50, offensiveImpactAudit["attackerCurrentPower"]!.GetValue<int>());
        Assert.Equal(52, offensiveImpactAudit["targetCurrentPower"]!.GetValue<int>());
    }

    [Fact]
    public async Task DebugGuardianPolicyContext_InvalidNonCreateGuardianCommandDoesNotFeedCurrentAuthorityRoot()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "UpdateGuardians": [
            {
              "command": "updateReputation",
              "guardianId": "guardian_alpha",
              "reputationChange": 12
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/kernel_invalid_non_create_guardian_command.json",
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
                    "appearanceDescription": "Invalid non-create commands must not mutate authority."
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
        var snapshot = await validator.DebugResolveGuardianPolicyContextAsync();

        Assert.True(snapshot.HasCurrentAuthorityRoot);
        Assert.NotNull(snapshot.CurrentAuthorityRootJson);

        var currentAuthorityRoot = JsonNode.Parse(snapshot.CurrentAuthorityRootJson!)!.AsObject();
        var guardians = Assert.IsType<JsonArray>(currentAuthorityRoot["guardians"]);
        var guardian = Assert.Single(guardians)!.AsObject();
        var relationshipData = Assert.IsType<JsonObject>(guardian["relationshipData"]);

        Assert.Equal(18, relationshipData["currentReputation"]!.GetValue<int>());
    }

    [Fact]
    public async Task DebugGuardianPolicyContext_ShapeValidButContractInvalidPowerEventDoesNotFeedCurrentAuthorityRoot()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardianPowerEvents": [
            {
              "eventId": "evt_invalid_preauth",
              "guardianId": "guardian_alpha",
              "relatedGuardianId": "guardian_alpha",
              "delta": "3",
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_invalid_preauth",
              "title": "Invalid preauth event",
              "summary": "A shape-valid event with wrong delta shape and self-related guardian must not enter authority.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_invalid_preauth",
                "baseDelta": 3,
                "finalDelta": 3,
                "inkFeathersOffered": 150
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/kernel_invalid_guardian_power_event_preauth.json",
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
                    "appearanceDescription": "Invalid raw power event must not mutate authority."
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
        var snapshot = await validator.DebugResolveGuardianPolicyContextAsync();

        Assert.True(snapshot.HasCurrentAuthorityRoot);
        Assert.NotNull(snapshot.CurrentAuthorityRootJson);

        var currentAuthorityRoot = JsonNode.Parse(snapshot.CurrentAuthorityRootJson!)!.AsObject();
        var guardians = Assert.IsType<JsonArray>(currentAuthorityRoot["guardians"]);
        var guardian = Assert.Single(guardians)!.AsObject();
        var abodePower = Assert.IsType<JsonObject>(guardian["abodePower"]);

        Assert.Equal(40, abodePower["currentPower"]!.GetValue<int>());
    }

    [Fact]
    public async Task DebugGuardianPolicyContext_ImpossibleOfferingGainRawPowerEventDoesNotFeedCurrentAuthorityRoot()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardianPowerEvents": [
            {
              "eventId": "evt_invalid_impossible_offering_gain",
              "guardianId": "guardian_alpha",
              "delta": 1,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_invalid_impossible_gain",
              "title": "Impossible offering gain must not affect authority",
              "summary": "Kernel must reject raw offering events whose authored delta disagrees with canonical offering gain.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_invalid_impossible_gain",
                "baseDelta": 1,
                "finalDelta": 1,
                "inkFeathersOffered": 150,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/kernel_invalid_impossible_offering_guardians.json",
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
                    "appearanceDescription": "Impossible raw offering gain must not mutate authority."
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
        var snapshot = await validator.DebugResolveGuardianPolicyContextAsync();

        Assert.True(snapshot.HasCurrentAuthorityRoot);
        Assert.NotNull(snapshot.CurrentAuthorityRootJson);

        var currentAuthorityRoot = JsonNode.Parse(snapshot.CurrentAuthorityRootJson!)!.AsObject();
        var guardians = Assert.IsType<JsonArray>(currentAuthorityRoot["guardians"]);
        var guardian = Assert.Single(guardians)!.AsObject();
        var abodePower = Assert.IsType<JsonObject>(guardian["abodePower"]);

        Assert.Equal(40, abodePower["currentPower"]!.GetValue<int>());
    }

    [Fact]
    public async Task DebugGuardianPolicyContext_InvalidResonanceRawPowerEventDoesNotFeedCurrentAuthorityRoot()
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardianPowerEvents": [
            {
              "eventId": "evt_invalid_resonance_contract",
              "guardianId": "guardian_alpha",
              "delta": -1,
              "reasonType": "resonance",
              "sourceSurface": "life_evaluation",
              "sourceId": "life_eval_invalid_resonance_contract",
              "title": "Invalid resonance event must not affect authority",
              "summary": "Kernel must reject resonance events without positive canonical delta and life-bound audit.",
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
                "finalDelta": 2
              }
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/kernel_invalid_resonance_power_event_guardians.json",
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
                    "appearanceDescription": "Invalid resonance audit must not mutate authority."
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
        var snapshot = await validator.DebugResolveGuardianPolicyContextAsync();

        Assert.True(snapshot.HasCurrentAuthorityRoot);
        Assert.NotNull(snapshot.CurrentAuthorityRootJson);

        var currentAuthorityRoot = JsonNode.Parse(snapshot.CurrentAuthorityRootJson!)!.AsObject();
        var guardians = Assert.IsType<JsonArray>(currentAuthorityRoot["guardians"]);
        var guardian = Assert.Single(guardians)!.AsObject();
        var abodePower = Assert.IsType<JsonObject>(guardian["abodePower"]);

        Assert.Equal(40, abodePower["currentPower"]!.GetValue<int>());
    }

    [Fact]
    public async Task DebugGuardianProjectTrackerPolicyContext_RawGuardianPowerEventFeedsProjectAuthority()
    {
        await WriteRawAsync("input/turn_request.json", """
        { "sessionId": "test-session", "requestId": "test-request", "turnNumber": 53 }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardianPowerEvents": [
            {
              "eventId": "evt_project_authority_power",
              "guardianId": "guardian_alpha",
              "delta": 3,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "offering_project_authority",
              "title": "Power event should feed project authority",
              "summary": "Tracker authority must see current guardian power events through the guardian kernel.",
              "visibility": "player_known",
              "appliedAt": "2026-03-24T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_project_authority",
                "baseDelta": 3,
                "finalDelta": 3,
                "inkFeathersOffered": 150,
                "capRemainingBefore": 150
              }
            }
          ]
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "completeGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_power_event_shift",
              "finalState": "Completed",
              "outcome": "Интрига завершена после raw guardian power event.",
              "targetGuardianId": "guardian_beta"
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/kernel_power_event_guardian_authority_baseline.json",
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
                    "appearanceDescription": "Raw guardian power events must affect project authority."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 60, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 50, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [
                    { "targetGuardianId": "guardian_beta", "attitudeScore": -80, "attitudeTier": "enemy", "reason": "Open hostility", "lastChangedAt": null }
                  ],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
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
                    "appearanceDescription": "Political target remains in baseline."
                  },
                  "manifestationHistory": [],
                  "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
                  "abodePower": { "currentPower": 52, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
                  "guardianRelationships": [
                    { "targetGuardianId": "guardian_alpha", "attitudeScore": -60, "attitudeTier": "rival", "reason": "Hostility", "lastChangedAt": null }
                  ],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/kernel_power_event_tracker_baseline.json",
            """
            {
              "activeProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_power_event_shift",
                    "projectType": "offensive_intrigue",
                    "projectTier": "major",
                    "projectMode": "offensive",
                    "projectName": "Интрига после power event",
                    "targetGuardianId": "guardian_beta",
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
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/kernel_power_event_tracker_journal.json",
            """
            {
              "entries": []
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var snapshot = await validator.DebugResolveGuardianProjectTrackerPolicyContextAsync();

        Assert.True(snapshot.HasCurrentAuthorityRoot);
        Assert.NotNull(snapshot.CurrentAuthorityRootJson);

        var currentAuthorityRoot = JsonNode.Parse(snapshot.CurrentAuthorityRootJson!)!.AsObject();
        var completedProjects = Assert.IsType<JsonArray>(currentAuthorityRoot["completedProjects"]);
        var completedEntry = Assert.Single(completedProjects)!.AsObject();
        var project = Assert.IsType<JsonObject>(completedEntry["project"]);
        var offensiveImpactAudit = Assert.IsType<JsonObject>(project["offensiveImpactAudit"]);

        Assert.Equal(53, offensiveImpactAudit["attackerCurrentPower"]!.GetValue<int>());
        Assert.Equal(52, offensiveImpactAudit["targetCurrentPower"]!.GetValue<int>());
    }

    [Fact]
    public async Task DebugGuardianPolicyContext_MissingValidatedPreTurnJournalIdentityInvalidatesCurrentPowerEventAuthority()
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
                "appearanceDescription": "Kernel should fail-close guardian power-event authority when the validated pre-turn journal identity baseline is missing."
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
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "mood": { "current": "focused", "intensity": 40, "reason": "Awaiting strict proof.", "since": 10 },
              "loreFragments": [
                { "fragmentId": "guardian_alpha_lore_1", "category": "personal_history", "title": "Берег памяти", "content": null, "requiredReputation": 0 },
                { "fragmentId": "guardian_alpha_lore_2", "category": "cosmic_secret", "title": "Тайна глубины", "content": null, "requiredReputation": 50 },
                { "fragmentId": "guardian_alpha_lore_3", "category": "domain_mastery", "title": "Узел течений", "content": null, "requiredReputation": 130 },
                { "fragmentId": "guardian_alpha_lore_4", "category": "lost_world", "title": "Затонувший берег", "content": null, "requiredReputation": 230 },
                { "fragmentId": "guardian_alpha_lore_5", "category": "other_guardians", "title": "Имена в пене", "content": null, "requiredReputation": 0 },
                { "fragmentId": "guardian_alpha_lore_6", "category": "soul_mechanics", "title": "Память соли", "content": null, "requiredReputation": 50 },
                { "fragmentId": "guardian_alpha_lore_7", "category": "personal_history", "title": "Возвращение волны", "content": null, "requiredReputation": 130 }
              ],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "guardianPowerEvents": [
            {
              "eventId": "offering_evt_kernel_missing_journal_baseline",
              "guardianId": "guardian_alpha",
              "delta": 2,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "kernel_missing_journal_baseline",
              "title": "Kernel raw offering event without validated pre-turn journal identity",
              "summary": "Guardian power-event authority must be unusable instead of sanitized when the validated pre-turn journal baseline is missing.",
              "visibility": "player_known",
              "appliedAt": "2026-03-28T00:00:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "cycle_kernel_missing_journal_baseline",
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
            "game_state/meta/guardians.json",
            "test_backups/kernel_guardians_missing_journal_identity.json",
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
                    "appearanceDescription": "Pre-turn guardian before missing journal identity fail-closed kernel case."
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
                  "mood": { "current": "focused", "intensity": 40, "reason": "Pre-turn calm.", "since": 10 },
                  "loreFragments": [
                    { "fragmentId": "guardian_alpha_lore_1", "category": "personal_history", "title": "Берег памяти", "content": null, "requiredReputation": 0 },
                    { "fragmentId": "guardian_alpha_lore_2", "category": "cosmic_secret", "title": "Тайна глубины", "content": null, "requiredReputation": 50 },
                    { "fragmentId": "guardian_alpha_lore_3", "category": "domain_mastery", "title": "Узел течений", "content": null, "requiredReputation": 130 },
                    { "fragmentId": "guardian_alpha_lore_4", "category": "lost_world", "title": "Затонувший берег", "content": null, "requiredReputation": 230 },
                    { "fragmentId": "guardian_alpha_lore_5", "category": "other_guardians", "title": "Имена в пене", "content": null, "requiredReputation": 0 },
                    { "fragmentId": "guardian_alpha_lore_6", "category": "soul_mechanics", "title": "Память соли", "content": null, "requiredReputation": 50 },
                    { "fragmentId": "guardian_alpha_lore_7", "category": "personal_history", "title": "Возвращение волны", "content": null, "requiredReputation": 130 }
                  ],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var snapshot = await validator.DebugResolveGuardianPolicyContextAsync();

        Assert.Equal("MissingValidatedPreTurnJournalIdentity", snapshot.CurrentGuardianPowerEventAuthorityStatus);
        Assert.Contains("abode_power_journal.json", snapshot.CurrentGuardianPowerEventAuthorityFailureDescription, StringComparison.OrdinalIgnoreCase);
        Assert.True(snapshot.HasCurrentAuthorityRoot);
        Assert.NotNull(snapshot.CurrentAuthorityRootJson);

        var currentAuthorityRoot = JsonNode.Parse(snapshot.CurrentAuthorityRootJson!)!.AsObject();
        var guardian = Assert.Single(Assert.IsType<JsonArray>(currentAuthorityRoot["guardians"]))!.AsObject();
        var abodePower = Assert.IsType<JsonObject>(guardian["abodePower"]);
        Assert.Equal(40, abodePower["currentPower"]!.GetValue<int>());
    }

    [Fact]
    public async Task DebugGuardianPolicyContext_SnapshotTrackerCompletionDoesNotPromotePartialStrictPreTurnAuthorityIntoGenericCurrentAuthority()
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
                "appearanceDescription": "Current authority should build from strict snapshot pre-turn baseline."
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
              "abodePower": { "currentPower": 42, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "mood": { "current": "focused", "intensity": 40, "reason": "Current calm.", "since": 10 },
              "loreFragments": [
                { "fragmentId": "guardian_alpha_lore_1", "category": "personal_history", "title": "Берег памяти", "content": null, "requiredReputation": 0 },
                { "fragmentId": "guardian_alpha_lore_2", "category": "cosmic_secret", "title": "Тайна глубины", "content": null, "requiredReputation": 50 },
                { "fragmentId": "guardian_alpha_lore_3", "category": "domain_mastery", "title": "Узел течений", "content": null, "requiredReputation": 130 },
                { "fragmentId": "guardian_alpha_lore_4", "category": "lost_world", "title": "Затонувший берег", "content": null, "requiredReputation": 230 },
                { "fragmentId": "guardian_alpha_lore_5", "category": "other_guardians", "title": "Имена в пене", "content": null, "requiredReputation": 0 },
                { "fragmentId": "guardian_alpha_lore_6", "category": "soul_mechanics", "title": "Память соли", "content": null, "requiredReputation": 50 },
                { "fragmentId": "guardian_alpha_lore_7", "category": "personal_history", "title": "Возвращение волны", "content": null, "requiredReputation": 130 }
              ],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/kernel_snapshot_tracker_preturn_guardians.json",
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
                    "appearanceDescription": "Pre-turn guardian before tracker-only power provenance."
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
                  "mood": { "current": "focused", "intensity": 40, "reason": "Pre-turn calm.", "since": 10 },
                  "loreFragments": [
                    { "fragmentId": "guardian_alpha_lore_1", "category": "personal_history", "title": "Берег памяти", "content": null, "requiredReputation": 0 },
                    { "fragmentId": "guardian_alpha_lore_2", "category": "cosmic_secret", "title": "Тайна глубины", "content": null, "requiredReputation": 50 },
                    { "fragmentId": "guardian_alpha_lore_3", "category": "domain_mastery", "title": "Узел течений", "content": null, "requiredReputation": 130 },
                    { "fragmentId": "guardian_alpha_lore_4", "category": "lost_world", "title": "Затонувший берег", "content": null, "requiredReputation": 230 },
                    { "fragmentId": "guardian_alpha_lore_5", "category": "other_guardians", "title": "Имена в пене", "content": null, "requiredReputation": 0 },
                    { "fragmentId": "guardian_alpha_lore_6", "category": "soul_mechanics", "title": "Память соли", "content": null, "requiredReputation": 50 },
                    { "fragmentId": "guardian_alpha_lore_7", "category": "personal_history", "title": "Возвращение волны", "content": null, "requiredReputation": 130 }
                  ],
                  "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
                  "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
                }
              ]
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/kernel_snapshot_tracker_preturn_journal.json",
            """
            {
              "entries": []
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/kernel_snapshot_tracker_preturn_tracker.json",
            """
            {
              "activeProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_kernel_tracker_power",
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
                  "projectId": "proj_kernel_tracker_power",
                  "finalState": "Completed",
                  "outcome": "Snapshot tracker completion should feed strict pre-turn authority root.",
                  "abodePowerDelta": 1
                }
              ]
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/kernel_snapshot_tracker_preturn_soul.json",
            """
            {
              "currentRealm": "Chaos Sea",
              "currentIncarnation": 1
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var snapshot = await validator.DebugResolveGuardianPolicyContextAsync();

        Assert.False(snapshot.HasPreTurnAuthorityRoot);
        Assert.Equal("InvalidValidatedSnapshotGuardians", snapshot.StrictPreTurnGuardianAuthorityStatus);
        Assert.Null(snapshot.PreTurnAuthorityRootJson);
        Assert.True(snapshot.HasCurrentAuthorityRoot);
        Assert.NotNull(snapshot.CurrentAuthorityRootJson);

        var currentAuthorityRoot = JsonNode.Parse(snapshot.CurrentAuthorityRootJson!)!.AsObject();
        var guardian = Assert.Single(Assert.IsType<JsonArray>(currentAuthorityRoot["guardians"]))!.AsObject();
        var abodePower = Assert.IsType<JsonObject>(guardian["abodePower"]);
        Assert.Equal(40, abodePower["currentPower"]!.GetValue<int>());
    }

    [Fact]
    public async Task DebugGuardianPolicyContext_GenericSharedStrictPreTurnAuthority_ResolvesSnapshotRelationshipsWithoutRawFallback()
    {
        await WriteRawAsync(
            "game_state/meta/guardians.json",
            SerializeJson(BuildGuardiansRoot(
                BuildCanonicalGuardian(
                    "guardian_alpha",
                    "Азалия",
                    reputation: 18,
                    power: 40,
                    relationships: new JsonArray
                    {
                        new JsonObject
                        {
                            ["targetGuardianId"] = "guardian_beta",
                            ["attitudeScore"] = -80,
                            ["attitudeTier"] = "enemy",
                            ["reason"] = "Open hostility",
                            ["lastChangedAt"] = null
                        }
                    }),
                BuildCanonicalGuardian(
                    "guardian_beta",
                    "Варак",
                    reputation: 10,
                    power: 35,
                    relationships: new JsonArray
                    {
                        new JsonObject
                        {
                            ["targetGuardianId"] = "guardian_alpha",
                            ["attitudeScore"] = -60,
                            ["attitudeTier"] = "rival",
                            ["reason"] = "Mutual hostility",
                            ["lastChangedAt"] = null
                        }
                    },
                    defaultName: "Варак",
                    masculineName: "Варак",
                    presentationStyle: "masculine",
                    pronouns: "он/его"))));

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/kernel_generic_shared_relationship_snapshot.json",
            SerializeJson(BuildGuardiansRoot(
                BuildCanonicalGuardian(
                    "guardian_alpha",
                    "Азалия",
                    reputation: 18,
                    power: 40,
                    relationships: new JsonArray
                    {
                        new JsonObject
                        {
                            ["targetGuardianId"] = "guardian_beta",
                            ["attitudeScore"] = -80,
                            ["attitudeTier"] = "enemy",
                            ["reason"] = "Open hostility",
                            ["lastChangedAt"] = null
                        }
                    }),
                BuildCanonicalGuardian(
                    "guardian_beta",
                    "Варак",
                    reputation: 10,
                    power: 35,
                    relationships: new JsonArray
                    {
                        new JsonObject
                        {
                            ["targetGuardianId"] = "guardian_alpha",
                            ["attitudeScore"] = -60,
                            ["attitudeTier"] = "rival",
                            ["reason"] = "Mutual hostility",
                            ["lastChangedAt"] = null
                        }
                    },
                    defaultName: "Варак",
                    masculineName: "Варак",
                    presentationStyle: "masculine",
                    pronouns: "он/его"))));

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var snapshot = await validator.DebugResolveGuardianPolicyContextAsync();

        Assert.True(
            snapshot.HasGenericSharedStrictPreTurnAuthorityRoot,
            $"{snapshot.GenericSharedStrictPreTurnGuardianAuthorityStatus}: {snapshot.GenericSharedStrictPreTurnGuardianAuthorityFailureDescription}");
        Assert.Equal("Resolved", snapshot.GenericSharedStrictPreTurnGuardianAuthorityStatus);
        Assert.NotNull(snapshot.GenericSharedStrictPreTurnAuthorityRootJson);

        var genericSharedStrictPreTurnAuthorityRoot = JsonNode.Parse(snapshot.GenericSharedStrictPreTurnAuthorityRootJson!)!.AsObject();
        var guardians = Assert.IsType<JsonArray>(genericSharedStrictPreTurnAuthorityRoot["guardians"]);
        Assert.Equal(2, guardians.Count);
        var guardianAlpha = guardians
            .Select(node => Assert.IsType<JsonObject>(node))
            .Single(guardian => string.Equals(guardian["guardianId"]?.GetValue<string>(), "guardian_alpha", StringComparison.OrdinalIgnoreCase));
        var guardianRelationships = Assert.IsType<JsonArray>(guardianAlpha["guardianRelationships"]);
        var relationship = Assert.Single(guardianRelationships)!.AsObject();
        Assert.Equal("guardian_beta", relationship["targetGuardianId"]!.GetValue<string>());
        Assert.Equal(-80, relationship["attitudeScore"]!.GetValue<int>());
    }

    [Fact]
    public async Task DebugGuardianPolicyContext_GenericSharedStrictPreTurnAuthority_MaterializesSnapshotCreateGuardian()
    {
        await WriteRawAsync(
            "game_state/meta/guardians.json",
            """
            {
              "guardians": []
            }
            """);

        var createdGuardian = BuildCanonicalGuardian(
            "guardian_new",
            "Лира",
            reputation: 25,
            power: 18,
            defaultName: "Лира",
            feminineName: "Лира",
            masculineName: "Лира",
            neutralName: "Лира");

        var preTurnSnapshotRoot = BuildGuardiansRoot();
        preTurnSnapshotRoot["UpdateGuardians"] = new JsonArray
        {
            new JsonObject
            {
                ["command"] = "create",
                ["data"] = createdGuardian.DeepClone()
            }
        };

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/kernel_generic_shared_snapshot_create_guardian.json",
            SerializeJson(preTurnSnapshotRoot));

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var snapshot = await validator.DebugResolveGuardianPolicyContextAsync();

        Assert.True(
            snapshot.HasGenericSharedStrictPreTurnAuthorityRoot,
            $"{snapshot.GenericSharedStrictPreTurnGuardianAuthorityStatus}: {snapshot.GenericSharedStrictPreTurnGuardianAuthorityFailureDescription}");
        Assert.Equal("Resolved", snapshot.GenericSharedStrictPreTurnGuardianAuthorityStatus);
        Assert.NotNull(snapshot.GenericSharedStrictPreTurnAuthorityRootJson);

        var genericSharedStrictPreTurnAuthorityRoot = JsonNode.Parse(snapshot.GenericSharedStrictPreTurnAuthorityRootJson!)!.AsObject();
        var guardians = Assert.IsType<JsonArray>(genericSharedStrictPreTurnAuthorityRoot["guardians"]);
        var createdGuardianEntry = Assert.Single(guardians)!.AsObject();
        Assert.Equal("guardian_new", createdGuardianEntry["guardianId"]!.GetValue<string>());
        Assert.Equal("Лира", createdGuardianEntry["canonicalName"]!.GetValue<string>());
    }

    [Fact]
    public async Task DebugGuardianPolicyContext_GenericSharedStrictPreTurnAuthority_IgnoresProofOnlyTrackerRequirement()
    {
        await WriteRawAsync(
            "game_state/meta/guardians.json",
            SerializeJson(BuildGuardiansRoot(
                BuildCanonicalGuardian("guardian_alpha", "Азалия", reputation: 18, power: 40))));

        var preTurnSnapshotRoot = BuildGuardiansRoot(
            BuildCanonicalGuardian("guardian_alpha", "Азалия", reputation: 18, power: 40));
        preTurnSnapshotRoot["guardianPowerEvents"] = new JsonArray
        {
            BuildOfferingGuardianPowerEvent(
                "offering_evt_generic_shared_missing_journal",
                "guardian_alpha",
                "generic_shared_missing_journal",
                2)
        };

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/kernel_generic_shared_missing_journal_guardians.json",
            SerializeJson(preTurnSnapshotRoot));

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var snapshot = await validator.DebugResolveGuardianPolicyContextAsync();

        Assert.False(snapshot.HasPreTurnAuthorityRoot, snapshot.StrictPreTurnGuardianAuthorityFailureDescription);
        Assert.Equal("MissingValidatedSnapshotTracker", snapshot.StrictPreTurnGuardianAuthorityStatus);
        Assert.Null(snapshot.PreTurnAuthorityRootJson);

        Assert.True(
            snapshot.HasGenericSharedStrictPreTurnAuthorityRoot,
            $"{snapshot.GenericSharedStrictPreTurnGuardianAuthorityStatus}: {snapshot.GenericSharedStrictPreTurnGuardianAuthorityFailureDescription}");
        Assert.Equal("Resolved", snapshot.GenericSharedStrictPreTurnGuardianAuthorityStatus);
        Assert.NotNull(snapshot.GenericSharedStrictPreTurnAuthorityRootJson);

        var genericSharedStrictPreTurnAuthorityRoot = JsonNode.Parse(snapshot.GenericSharedStrictPreTurnAuthorityRootJson!)!.AsObject();
        var guardian = Assert.Single(Assert.IsType<JsonArray>(genericSharedStrictPreTurnAuthorityRoot["guardians"]))!.AsObject();
        var abodePower = Assert.IsType<JsonObject>(guardian["abodePower"]);
        Assert.Equal(40, abodePower["currentPower"]!.GetValue<int>());
    }

    [Fact]
    public async Task DebugGuardianProjectTrackerPolicyContext_ReadablePartialCurrentSoulStateMergesValidatedSnapshotSoulContext()
    {
        await WriteRawAsync(
            "game_state/meta/guardians.json",
            SerializeJson(BuildGuardiansRoot(
                BuildCanonicalGuardian(
                    "guardian_alpha",
                    "Азалия",
                    reputation: 18,
                    power: 40,
                    appearanceDescription: "Current guardian authority should use merged soul-state context for tracker materialization."))));

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/kernel_partial_current_soul_guardians_snapshot.json",
            SerializeJson(BuildGuardiansRoot(
                BuildCanonicalGuardian(
                    "guardian_alpha",
                    "Азалия",
                    reputation: 18,
                    power: 40,
                    appearanceDescription: "Validated guardian baseline for merged soul-state tracker authority."))));

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": [],
          "completeGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_kernel_lore_completion",
              "finalState": "Completed",
              "outcome": "Readable partial current soul_state should merge with validated snapshot soul context."
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/kernel_partial_current_soul_tracker_snapshot.json",
            """
            {
              "activeProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_kernel_lore_completion",
                    "projectType": "lore_research",
                    "projectTier": "major",
                    "projectMode": "internal",
                    "projectName": "Исследование прилива памяти",
                    "activeState": "Preparing the final archive hook",
                    "totalWork": 18,
                    "workDone": 18,
                    "totalStages": 3,
                    "currentStage": 3,
                    "pressure": 4,
                    "stability": 84
                  }
                }
              ],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "inkFeathers": {
            "current": 1,
            "total": 1
          }
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/kernel_partial_current_soul_snapshot.json",
            """
            {
              "currentRealm": "Chaos Sea",
              "currentIncarnation": 3
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var snapshot = await validator.DebugResolveGuardianProjectTrackerPolicyContextAsync();

        Assert.Equal("None", snapshot.CurrentStateFailureKind);
        Assert.True(snapshot.HasCurrentAuthorityRoot);
        Assert.NotNull(snapshot.CurrentAuthorityRootJson);

        var currentAuthorityRoot = JsonNode.Parse(snapshot.CurrentAuthorityRootJson!)!.AsObject();
        var completedProjects = Assert.IsType<JsonArray>(currentAuthorityRoot["completedProjects"]);
        var completedEntry = Assert.Single(completedProjects)!.AsObject();
        var project = Assert.IsType<JsonObject>(completedEntry["project"]);
        var effectState = Assert.IsType<JsonObject>(project["effectState"]);

        Assert.Equal(4, effectState["targetIncarnation"]!.GetValue<int>());
    }

    [Fact]
    public async Task DebugGuardianProjectTrackerPolicyContext_MalformedCurrentSoulStateInvalidatesSoulDependentTrackerAuthority()
    {
        await WriteRawAsync(
            "game_state/meta/guardians.json",
            SerializeJson(BuildGuardiansRoot(
                BuildCanonicalGuardian(
                    "guardian_alpha",
                    "Азалия",
                    reputation: 18,
                    power: 40,
                    appearanceDescription: "Malformed current soul_state must invalidate soul-dependent tracker authority."))));

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/kernel_invalid_current_soul_guardians_snapshot.json",
            SerializeJson(BuildGuardiansRoot(
                BuildCanonicalGuardian(
                    "guardian_alpha",
                    "Азалия",
                    reputation: 18,
                    power: 40,
                    appearanceDescription: "Validated guardian baseline for invalid current soul-state test."))));

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": [],
          "completeGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_kernel_invalid_current_soul",
              "finalState": "Completed",
              "outcome": "Malformed current soul_state must fail tracker authority before fallback."
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/kernel_invalid_current_soul_tracker_snapshot.json",
            """
            {
              "activeProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_kernel_invalid_current_soul",
                    "projectType": "lore_research",
                    "projectTier": "major",
                    "projectMode": "internal",
                    "projectName": "Исследование без читаемой души",
                    "activeState": "The completion requires soul-context-aware authority.",
                    "totalWork": 18,
                    "workDone": 18,
                    "totalStages": 3,
                    "currentStage": 3,
                    "pressure": 4,
                    "stability": 84
                  }
                }
              ],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await WriteRawAsync("game_state/meta/soul_state.json", "{ malformed soul");

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/kernel_invalid_current_soul_snapshot.json",
            """
            {
              "currentRealm": "Chaos Sea",
              "currentIncarnation": 3
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var snapshot = await validator.DebugResolveGuardianProjectTrackerPolicyContextAsync();

        Assert.Equal("SemanticallyInvalidCurrentState", snapshot.CurrentStateFailureKind);
        Assert.False(snapshot.HasProjectedAuthorityRoot);
        Assert.False(snapshot.HasCurrentAuthorityRoot);
    }

    [Fact]
    public async Task DebugGuardianProjectTrackerPolicyContext_UnsupportedTopLevelCurrentSoulStateInvalidatesSoulDependentTrackerAuthority()
    {
        await WriteRawAsync(
            "game_state/meta/guardians.json",
            SerializeJson(BuildGuardiansRoot(
                BuildCanonicalGuardian(
                    "guardian_alpha",
                    "Азалия",
                    reputation: 18,
                    power: 40,
                    appearanceDescription: "Unsupported current soul_state top-level key must invalidate soul-dependent tracker authority."))));

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/kernel_invalid_current_soul_unsupported_key_guardians_snapshot.json",
            SerializeJson(BuildGuardiansRoot(
                BuildCanonicalGuardian(
                    "guardian_alpha",
                    "Азалия",
                    reputation: 18,
                    power: 40,
                    appearanceDescription: "Validated guardian baseline for unsupported-key current soul-state test."))));

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": [],
          "completeGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_kernel_invalid_current_soul_unsupported_key",
              "finalState": "Completed",
              "outcome": "Mixed valid and unsupported current soul_state keys must fail tracker authority before fallback."
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/kernel_invalid_current_soul_unsupported_key_tracker_snapshot.json",
            """
            {
              "activeProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_kernel_invalid_current_soul_unsupported_key",
                    "projectType": "lore_research",
                    "projectTier": "major",
                    "projectMode": "internal",
                    "projectName": "Исследование с unsupported current soul_state key",
                    "activeState": "The completion requires strict soul-context-aware authority.",
                    "totalWork": 18,
                    "workDone": 18,
                    "totalStages": 3,
                    "currentStage": 3,
                    "pressure": 4,
                    "stability": 84
                  }
                }
              ],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 3,
          "foo": []
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/kernel_invalid_current_soul_unsupported_key_snapshot.json",
            """
            {
              "currentRealm": "Chaos Sea",
              "currentIncarnation": 3
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var snapshot = await validator.DebugResolveGuardianProjectTrackerPolicyContextAsync();

        Assert.Equal("SemanticallyInvalidCurrentState", snapshot.CurrentStateFailureKind);
        Assert.False(snapshot.HasProjectedAuthorityRoot);
        Assert.False(snapshot.HasCurrentAuthorityRoot);
    }

    [Fact]
    public async Task DebugGuardianProjectTrackerPolicyContext_ArchiveActionResolutionsCurrentSoulStateRemainsValidStrictAuthority()
    {
        await WriteRawAsync(
            "game_state/meta/guardians.json",
            SerializeJson(BuildGuardiansRoot(
                BuildCanonicalGuardian(
                    "guardian_alpha",
                    "Азалия",
                    reputation: 18,
                    power: 40,
                    appearanceDescription: "Canonical archiveActionResolutions key must remain valid strict soul authority."))));

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/kernel_archive_action_resolutions_guardians_snapshot.json",
            SerializeJson(BuildGuardiansRoot(
                BuildCanonicalGuardian(
                    "guardian_alpha",
                    "Азалия",
                    reputation: 18,
                    power: 40,
                    appearanceDescription: "Validated guardian baseline for archiveActionResolutions current soul-state test."))));

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": [],
          "completeGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_kernel_archive_action_resolutions",
              "finalState": "Completed",
              "outcome": "Canonical archiveActionResolutions current soul_state key must not invalidate tracker authority."
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/kernel_archive_action_resolutions_tracker_snapshot.json",
            """
            {
              "activeProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_kernel_archive_action_resolutions",
                    "projectType": "lore_research",
                    "projectTier": "major",
                    "projectMode": "internal",
                    "projectName": "Исследование с archiveActionResolutions в current soul_state",
                    "activeState": "The completion requires strict soul-context-aware authority.",
                    "totalWork": 18,
                    "workDone": 18,
                    "totalStages": 3,
                    "currentStage": 3,
                    "pressure": 4,
                    "stability": 84
                  }
                }
              ],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 3,
          "archiveActionResolutions": []
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/kernel_archive_action_resolutions_soul_snapshot.json",
            """
            {
              "currentRealm": "Chaos Sea",
              "currentIncarnation": 3
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var snapshot = await validator.DebugResolveGuardianProjectTrackerPolicyContextAsync();

        Assert.Equal("None", snapshot.CurrentStateFailureKind);
        Assert.True(snapshot.HasProjectedAuthorityRoot);
        Assert.True(snapshot.HasCurrentAuthorityRoot);
        Assert.NotNull(snapshot.CurrentAuthorityRootJson);
    }

    [Fact]
    public async Task DebugGuardianProjectTrackerPolicyContext_CrossIncarnationDataCurrentSoulStateRemainsReadableOnLifecycleAuthority()
    {
        await WriteRawAsync(
            "game_state/meta/guardians.json",
            SerializeJson(BuildGuardiansRoot(
                BuildCanonicalGuardian(
                    "guardian_alpha",
                    "Азалия",
                    reputation: 18,
                    power: 40,
                    appearanceDescription: "Non-canonical crossIncarnationData root key must not remain strict guardian-policy soul authority."))));

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/kernel_cross_incarnation_data_guardians_snapshot.json",
            SerializeJson(BuildGuardiansRoot(
                BuildCanonicalGuardian(
                    "guardian_alpha",
                    "Азалия",
                    reputation: 18,
                    power: 40,
                    appearanceDescription: "Validated guardian baseline for crossIncarnationData strict soul-state test."))));

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": [],
          "completeGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_kernel_cross_incarnation_data",
              "finalState": "Completed",
              "outcome": "crossIncarnationData root key must fail strict soul authority instead of slipping through as canonical state."
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/kernel_cross_incarnation_data_tracker_snapshot.json",
            """
            {
              "activeProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_kernel_cross_incarnation_data",
                    "projectType": "lore_research",
                    "projectTier": "major",
                    "projectMode": "internal",
                    "projectName": "Исследование с lifecycle-compatible crossIncarnationData в current soul_state",
                    "activeState": "The completion requires lifecycle-aware soul-context authority.",
                    "totalWork": 18,
                    "workDone": 18,
                    "totalStages": 3,
                    "currentStage": 3,
                    "pressure": 4,
                    "stability": 84
                  }
                }
              ],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 3,
          "crossIncarnationData": {
            "legacyThreadId": "thread_alpha"
          }
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/kernel_cross_incarnation_data_soul_snapshot.json",
            """
            {
              "currentRealm": "Chaos Sea",
              "currentIncarnation": 3
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var snapshot = await validator.DebugResolveGuardianProjectTrackerPolicyContextAsync();

        Assert.Equal("None", snapshot.CurrentStateFailureKind);
        Assert.True(snapshot.HasProjectedAuthorityRoot);
        Assert.True(snapshot.HasCurrentAuthorityRoot);
    }

    [Fact]
    public async Task DebugGuardianProjectTrackerPolicyContext_CurrentIncarnationOnlySoulPreparationAuthorityDoesNotRequireCurrentRealm()
    {
        await WriteRawAsync(
            "game_state/meta/guardians.json",
            SerializeJson(BuildGuardiansRoot(
                BuildCanonicalGuardian(
                    "guardian_alpha",
                    "Азалия",
                    reputation: 18,
                    power: 40,
                    appearanceDescription: "Soul preparation tracker authority should accept incarnation-only soul context."))));

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/kernel_soul_preparation_incarnation_only_guardians_snapshot.json",
            SerializeJson(BuildGuardiansRoot(
                BuildCanonicalGuardian(
                    "guardian_alpha",
                    "Азалия",
                    reputation: 18,
                    power: 40,
                    appearanceDescription: "Validated guardian baseline for incarnation-only soul preparation authority."))));

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [],
          "temporaryProjectModifiers": [],
          "completeGuardianProjects": [
            {
              "guardianId": "guardian_alpha",
              "projectId": "proj_kernel_soul_preparation_incarnation_only",
              "finalState": "Completed",
              "outcome": "Incarnation-only soul context should be enough for soul preparation tracker authority."
            }
          ]
        }
        """);

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/kernel_soul_preparation_incarnation_only_tracker_snapshot.json",
            """
            {
              "activeProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_kernel_soul_preparation_incarnation_only",
                    "projectType": "soul_preparation",
                    "projectTier": "major",
                    "projectMode": "internal",
                    "projectName": "Подготовка души без realm",
                    "activeState": "Only the current incarnation should matter for this completion.",
                    "totalWork": 12,
                    "workDone": 12,
                    "totalStages": 2,
                    "currentStage": 2,
                    "pressure": 2,
                    "stability": 91
                  }
                }
              ],
              "completedProjects": [],
              "temporaryProjectModifiers": []
            }
            """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "inkFeathers": {
            "current": 2,
            "total": 2
          }
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var snapshot = await validator.DebugResolveGuardianProjectTrackerPolicyContextAsync();

        Assert.Equal("None", snapshot.CurrentStateFailureKind);
        Assert.True(snapshot.HasCurrentAuthorityRoot);
        Assert.NotNull(snapshot.CurrentAuthorityRootJson);

        var currentAuthorityRoot = JsonNode.Parse(snapshot.CurrentAuthorityRootJson!)!.AsObject();
        var completedProjects = Assert.IsType<JsonArray>(currentAuthorityRoot["completedProjects"]);
        var completedEntry = Assert.Single(completedProjects)!.AsObject();
        var project = Assert.IsType<JsonObject>(completedEntry["project"]);
        var effectState = Assert.IsType<JsonObject>(project["effectState"]);

        Assert.Equal(4, effectState["targetIncarnation"]!.GetValue<int>());
        Assert.Equal(0, effectState["preparationBudgetPointsSpent"]!.GetValue<int>());
    }

    [Fact]
    public async Task DebugGuardianPolicyContext_PartialValidatedSnapshotSoulStateInvalidatesSoulDependentSnapshotTrackerProof()
    {
        await WriteRawAsync(
            "game_state/meta/guardians.json",
            SerializeJson(BuildGuardiansRoot(
                BuildCanonicalGuardian(
                    "guardian_alpha",
                    "Азалия",
                    reputation: 18,
                    power: 40,
                    appearanceDescription: "Strict snapshot proof should fail when validated snapshot soul_state is only partial."))));

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/kernel_partial_snapshot_soul_guardians_snapshot.json",
            SerializeJson(BuildGuardiansRoot(
                BuildCanonicalGuardian(
                    "guardian_alpha",
                    "Азалия",
                    reputation: 18,
                    power: 40,
                    appearanceDescription: "Validated guardian baseline for partial snapshot soul proof failure."))));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/kernel_partial_snapshot_soul_tracker_snapshot.json",
            """
            {
              "activeProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_kernel_partial_snapshot_soul",
                    "projectType": "lore_research",
                    "projectTier": "major",
                    "projectMode": "internal",
                    "projectName": "Исследование неполного snapshot soul",
                    "activeState": "Strict snapshot proof requires soul-derived tracker materialization.",
                    "totalWork": 18,
                    "workDone": 18,
                    "totalStages": 3,
                    "currentStage": 3,
                    "pressure": 4,
                    "stability": 84
                  }
                }
              ],
              "completedProjects": [],
              "temporaryProjectModifiers": [],
              "completeGuardianProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "projectId": "proj_kernel_partial_snapshot_soul",
                  "finalState": "Completed",
                  "outcome": "Readable partial validated snapshot soul_state without trusted baseline must invalidate strict snapshot tracker proof."
                }
              ]
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/kernel_partial_snapshot_soul_snapshot.json",
            """
            {
              "inkFeathers": {
                "current": 1,
                "total": 1
              }
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var snapshot = await validator.DebugResolveGuardianPolicyContextAsync();

        Assert.False(snapshot.HasPreTurnAuthorityRoot);
        Assert.Equal("InvalidValidatedSnapshotTracker", snapshot.StrictPreTurnGuardianAuthorityStatus);
        Assert.Contains(
            "readable current soul_state.json authority surface",
            snapshot.StrictPreTurnGuardianAuthorityFailureDescription,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DebugGuardianPolicyContext_PartialValidatedSnapshotSoulStateWithoutRealmAllowsSoulPreparationSnapshotTrackerProof()
    {
        await WriteRawAsync(
            "game_state/meta/guardians.json",
            SerializeJson(BuildGuardiansRoot(
                BuildCanonicalGuardian(
                    "guardian_alpha",
                    "Азалия",
                    reputation: 18,
                    power: 40,
                    appearanceDescription: "Snapshot soul preparation proof should accept incarnation-only soul context."))));

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1
        }
        """);

        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/kernel_partial_snapshot_soul_preparation_guardians_snapshot.json",
            SerializeJson(BuildGuardiansRoot(
                BuildCanonicalGuardian(
                    "guardian_alpha",
                    "Азалия",
                    reputation: 18,
                    power: 40,
                    appearanceDescription: "Validated guardian baseline for incarnation-only soul preparation proof."))));

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/kernel_partial_snapshot_soul_preparation_tracker_snapshot.json",
            """
            {
              "activeProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "project": {
                    "projectId": "proj_kernel_partial_snapshot_soul_preparation",
                    "projectType": "soul_preparation",
                    "projectTier": "major",
                    "projectMode": "internal",
                    "projectName": "Подготовка души из partial snapshot soul",
                    "activeState": "Strict snapshot proof should only require current incarnation here.",
                    "totalWork": 10,
                    "workDone": 10,
                    "totalStages": 2,
                    "currentStage": 2,
                    "pressure": 2,
                    "stability": 93
                  }
                }
              ],
              "completedProjects": [],
              "temporaryProjectModifiers": [],
              "completeGuardianProjects": [
                {
                  "guardianId": "guardian_alpha",
                  "projectId": "proj_kernel_partial_snapshot_soul_preparation",
                  "finalState": "Completed",
                  "outcome": "Readable partial validated snapshot soul_state without realm should remain valid for soul preparation proof."
                }
              ]
            }
            """);

        await AddTrackedFileToCurrentPendingTurnSnapshotAsync(
            "game_state/meta/soul_state.json",
            "test_backups/kernel_partial_snapshot_soul_preparation_snapshot.json",
            """
            {
              "currentIncarnation": 2,
              "inkFeathers": {
                "current": 1,
                "total": 1
              }
            }
            """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var snapshot = await validator.DebugResolveGuardianPolicyContextAsync();

        Assert.True(
            snapshot.HasPreTurnAuthorityRoot,
            $"{snapshot.StrictPreTurnGuardianAuthorityStatus}: {snapshot.StrictPreTurnGuardianAuthorityFailureDescription}");
        Assert.Equal("Resolved", snapshot.StrictPreTurnGuardianAuthorityStatus);
        Assert.NotNull(snapshot.PreTurnAuthorityRootJson);
    }

    private static string SerializeJson(JsonNode node)
        => node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    private static JsonObject BuildGuardiansRoot(params JsonObject[] guardians)
    {
        var guardiansArray = new JsonArray();
        foreach (var guardian in guardians)
            guardiansArray.Add(guardian.DeepClone());

        return new JsonObject
        {
            ["guardians"] = guardiansArray
        };
    }

    private static JsonObject BuildCanonicalGuardian(
        string guardianId,
        string canonicalName,
        int reputation,
        int power,
        JsonArray? relationships = null,
        string? defaultName = null,
        string? feminineName = null,
        string? masculineName = null,
        string? neutralName = null,
        string? presentationStyle = null,
        string? pronouns = null,
        string? appearanceDescription = null)
    {
        defaultName ??= canonicalName;
        feminineName ??= defaultName;
        masculineName ??= defaultName;
        neutralName ??= defaultName;
        presentationStyle ??= "feminine";
        pronouns ??= "она/её";
        appearanceDescription ??= $"Canonical fixture for {guardianId}.";
        var canonicalPower = AbodePowerRules.ClampCurrentPower(power);
        var gachaChargesPerReturn = GuardianGachaChargeRules.GetChargesPerReturnForReputation(reputation, canonicalPower);
        var nameVariants = new JsonObject
        {
            ["default"] = defaultName,
            ["feminine"] = feminineName,
            ["masculine"] = masculineName,
            ["neutral"] = neutralName
        };

        return new JsonObject
        {
            ["guardianId"] = guardianId,
            ["canonicalName"] = canonicalName,
            ["nameVariants"] = nameVariants,
            ["manifestation"] = new JsonObject
            {
                ["currentDisplayName"] = defaultName,
                ["formFlexibility"] = "selective",
                ["currentPresentationStyle"] = presentationStyle,
                ["currentPronouns"] = pronouns,
                ["appearanceDescription"] = appearanceDescription
            },
            ["manifestationHistory"] = new JsonArray(),
            ["domain"] = "Tide",
            ["abode"] = new JsonObject
            {
                ["abodeId"] = $"abode_{guardianId}",
                ["title"] = $"Обитель {canonicalName}"
            },
            ["personalityProfile"] = new JsonObject
            {
                ["archetype"] = "Tide Keeper",
                ["speechPattern"] = "Measured and tidal",
                ["coreValues"] = new JsonArray("balance", "memory", "patience")
            },
            ["relationshipData"] = new JsonObject
            {
                ["currentReputation"] = reputation,
                ["reputationHistory"] = new JsonArray(),
                ["lastInteraction"] = null
            },
            ["abodePower"] = new JsonObject
            {
                ["currentPower"] = canonicalPower,
                ["tier"] = AbodePowerRules.GetTierLabel(canonicalPower),
                ["lastUpdatedAt"] = "2026-03-24T00:00:00Z",
                ["history"] = new JsonArray()
            },
            ["guardianRelationships"] = relationships?.DeepClone() ?? new JsonArray(),
            ["mood"] = new JsonObject
            {
                ["current"] = "focused",
                ["intensity"] = 40,
                ["reason"] = "Kernel fixture calm.",
                ["since"] = 10
            },
            ["loreFragments"] = new JsonArray
            {
                new JsonObject { ["fragmentId"] = $"{guardianId}_lore_1", ["category"] = "personal_history", ["title"] = "Берег памяти", ["content"] = null, ["requiredReputation"] = 0 },
                new JsonObject { ["fragmentId"] = $"{guardianId}_lore_2", ["category"] = "cosmic_secret", ["title"] = "Тайна глубины", ["content"] = null, ["requiredReputation"] = 50 },
                new JsonObject { ["fragmentId"] = $"{guardianId}_lore_3", ["category"] = "domain_mastery", ["title"] = "Узел течений", ["content"] = null, ["requiredReputation"] = 130 },
                new JsonObject { ["fragmentId"] = $"{guardianId}_lore_4", ["category"] = "lost_world", ["title"] = "Затонувший берег", ["content"] = null, ["requiredReputation"] = 230 },
                new JsonObject { ["fragmentId"] = $"{guardianId}_lore_5", ["category"] = "other_guardians", ["title"] = "Имена в пене", ["content"] = null, ["requiredReputation"] = 0 },
                new JsonObject { ["fragmentId"] = $"{guardianId}_lore_6", ["category"] = "soul_mechanics", ["title"] = "Память соли", ["content"] = null, ["requiredReputation"] = 50 },
                new JsonObject { ["fragmentId"] = $"{guardianId}_lore_7", ["category"] = "personal_history", ["title"] = "Возвращение волны", ["content"] = null, ["requiredReputation"] = 130 }
            },
            ["questManagement"] = new JsonObject
            {
                ["availableQuests"] = new JsonArray(),
                ["activeQuests"] = new JsonArray(),
                ["completedQuests"] = new JsonArray()
            },
            ["gachaSystem"] = new JsonObject
            {
                ["chargesPerReturn"] = gachaChargesPerReturn,
                ["chargesUsedThisReturn"] = 0,
                ["gachaHistory"] = new JsonArray()
            }
        };
    }

    private static JsonObject BuildOfferingGuardianPowerEvent(
        string eventId,
        string guardianId,
        string sourceId,
        int delta)
        => new()
        {
            ["eventId"] = eventId,
            ["guardianId"] = guardianId,
            ["delta"] = delta,
            ["reasonType"] = "offering",
            ["sourceSurface"] = "guardianAbodeOffering",
            ["sourceId"] = sourceId,
            ["title"] = "Snapshot offering event",
            ["summary"] = "Generic shared strict pre-turn authority should ignore proof-only journal requirements.",
            ["visibility"] = "player_known",
            ["appliedAt"] = "2026-03-24T00:00:00Z",
            ["audit"] = new JsonObject
            {
                ["offeringType"] = "ink_feathers",
                ["returnCycleId"] = $"cycle_{sourceId}",
                ["baseDelta"] = delta,
                ["finalDelta"] = delta,
                ["inkFeathersOffered"] = 100,
                ["capRemainingBefore"] = 150
            }
        };

    private async Task WriteRawAsync(string path, string json) =>
        await _fs.WriteFileAtomicAsync(path, json);

    private async Task WritePreTurnTrackedFileAsync(string trackedPath, string backupPath, string json)
    {
        if (File.Exists(_fs.ResolvePath("game_state/control/pending_turn_snapshot.json")))
        {
            await AddTrackedFileToCurrentPendingTurnSnapshotAsync(trackedPath, backupPath, json);
            return;
        }

        await _fs.WriteFileAtomicAsync(backupPath, json);
        var snapshotPath = $"game_state/control/pending_turn_snapshot/{trackedPath}";
        await _fs.WriteFileAtomicAsync(snapshotPath, json);

        if (!File.Exists(_fs.ResolvePath("input/turn_request.json")))
        {
            await _fs.WriteFileAtomicAsync("input/turn_request.json", """
            { "sessionId": "test-session", "requestId": "test-request", "turnNumber": 12 }
            """);
        }

        var sessionId = "test-session";
        var requestId = "test-request";
        var turnNumber = 12;
        var turnRequestPath = _fs.ResolvePath("input/turn_request.json");
        if (File.Exists(turnRequestPath))
        {
            var turnRequestJson = File.ReadAllText(turnRequestPath);
            if (!string.IsNullOrWhiteSpace(turnRequestJson) &&
                JsonNode.Parse(turnRequestJson) is JsonObject turnRequest)
            {
                sessionId = turnRequest["sessionId"]?.GetValue<string>() ?? sessionId;
                requestId = turnRequest["requestId"]?.GetValue<string>() ?? requestId;
                turnNumber = turnRequest["turnNumber"]?.GetValue<int>() ?? turnNumber;
            }
        }

        var snapshotHash = ComputeSha256(json);
        var manifest = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turnNumber,
            ["requestTimestamp"] = "2026-03-24T00:00:00Z",
            ["playerAction"] = "guardian-kernel-test",
            ["files"] = new JsonObject
            {
                [trackedPath] = snapshotPath
            },
            ["snapshotFileHashes"] = new JsonObject
            {
                [trackedPath] = snapshotHash
            },
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject
            {
                [trackedPath] = backupPath
            },
            ["rollbackBaselineFiles"] = new JsonArray(trackedPath),
            ["sourceLabel"] = "guardian-policy-kernel-tests",
            ["manifestPayloadHash"] = string.Empty
        };

        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await _fs.WriteFileAtomicAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private async Task AddTrackedFileToCurrentPendingTurnSnapshotAsync(string trackedPath, string backupPath, string json)
    {
        await _fs.WriteFileAtomicAsync(backupPath, json);
        var snapshotPath = $"game_state/control/pending_turn_snapshot/{trackedPath}";
        await _fs.WriteFileAtomicAsync(snapshotPath, json);

        var manifestJson = await _fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json");
        Assert.False(string.IsNullOrWhiteSpace(manifestJson));
        var manifest = JsonNode.Parse(manifestJson!)!.AsObject();
        var files = manifest["files"] as JsonObject ?? new JsonObject();
        var snapshotHashes = manifest["snapshotFileHashes"] as JsonObject ?? new JsonObject();
        var rollbackBackups = manifest["rollbackBackups"] as JsonObject ?? new JsonObject();
        var rollbackBaselineFiles = manifest["rollbackBaselineFiles"] as JsonArray ?? new JsonArray();

        files[trackedPath] = snapshotPath;
        snapshotHashes[trackedPath] = ComputeSha256(json);
        rollbackBackups[trackedPath] = backupPath;
        if (!rollbackBaselineFiles.Any(node => string.Equals(node?.GetValue<string>(), trackedPath, StringComparison.OrdinalIgnoreCase)))
            rollbackBaselineFiles.Add(trackedPath);

        manifest["files"] = files;
        manifest["snapshotFileHashes"] = snapshotHashes;
        manifest["rollbackBackups"] = rollbackBackups;
        manifest["rollbackBaselineFiles"] = rollbackBaselineFiles;
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);

        await _fs.WriteFileAtomicAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
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
}
