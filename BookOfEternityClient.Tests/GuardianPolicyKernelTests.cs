using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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
        var guardian = Assert.Single(Assert.IsType<JsonArray>(currentAuthorityRoot["guardians"])).AsObject();
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
        var guardian = Assert.Single(Assert.IsType<JsonArray>(currentAuthorityRoot["guardians"])).AsObject();
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
        var guardian = Assert.Single(Assert.IsType<JsonArray>(currentAuthorityRoot["guardians"])).AsObject();
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
        var guardian = Assert.Single(Assert.IsType<JsonArray>(currentAuthorityRoot["guardians"])).AsObject();
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

        Assert.True(snapshot.HasProjectedAuthorityRoot);
        Assert.True(snapshot.HasCurrentAuthorityRoot);
        Assert.Empty(snapshot.ProjectedActiveProjectKeys);
        Assert.Empty(snapshot.CurrentActiveProjectKeys);
    }

    [Fact]
    public async Task DebugGuardianProjectTrackerPolicyContext_CommandShapedGuardianMutationFeedsProjectAuthority()
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
        var completedEntry = Assert.Single(completedProjects).AsObject();
        var project = Assert.IsType<JsonObject>(completedEntry["project"]);
        var offensiveImpactAudit = Assert.IsType<JsonObject>(project["offensiveImpactAudit"]);

        Assert.Equal(57, offensiveImpactAudit["attackerCurrentPower"]!.GetValue<int>());
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
        var guardian = Assert.Single(guardians).AsObject();
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
        var guardian = Assert.Single(guardians).AsObject();
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
        var guardian = Assert.Single(guardians).AsObject();
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
        var guardian = Assert.Single(guardians).AsObject();
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
        var completedEntry = Assert.Single(completedProjects).AsObject();
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
        var guardian = Assert.Single(Assert.IsType<JsonArray>(currentAuthorityRoot["guardians"])).AsObject();
        var abodePower = Assert.IsType<JsonObject>(guardian["abodePower"]);
        Assert.Equal(40, abodePower["currentPower"]!.GetValue<int>());
    }

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
            ["rollbackBaselineFiles"] = new JsonArray(),
            ["sourceLabel"] = "guardian-policy-kernel-tests",
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

        files[trackedPath] = snapshotPath;
        snapshotHashes[trackedPath] = ComputeSha256(json);
        rollbackBackups[trackedPath] = backupPath;

        manifest["files"] = files;
        manifest["snapshotFileHashes"] = snapshotHashes;
        manifest["rollbackBackups"] = rollbackBackups;
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
