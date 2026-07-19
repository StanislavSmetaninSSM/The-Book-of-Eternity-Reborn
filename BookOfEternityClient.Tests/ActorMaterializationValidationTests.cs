using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ActorMaterializationValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public ActorMaterializationValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-actor-materialization-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public void ValidateResponse_NewMortalNpcWithoutEnvelope_ReportsMissingMaterialization()
    {
        using var document = JsonDocument.Parse("""
        {
          "NPCsInScene": [
            {
              "NPCId": null,
              "initialId": "npc_station_medic",
              "name": "Дежурный медик орбитальной станции"
            }
          ]
        }
        """);

        var issues = _validator.ValidateResponse(document.RootElement);

        Assert.Contains(issues, issue => issue.Code == "actor_materialization_missing");
    }

    [Fact]
    public void ValidateResponse_NewAfterlifeProfileWithoutEnvelope_ReportsMissingMaterialization()
    {
        using var document = JsonDocument.Parse("""
        {
          "afterlifeEntityProfiles": [
            {
              "actorType": "radiant_actor",
              "actorId": "radiant_voice_of_north_gallery",
              "displayName": "Голос северной галереи",
              "realm": "Shining Abode"
            }
          ]
        }
        """);

        var issues = _validator.ValidateResponse(document.RootElement);

        Assert.Contains(issues, issue => issue.Code == "actor_materialization_missing");
    }

    [Theory]
    [InlineData("actor_ref_only")]
    [InlineData("same_actor_ref_alias")]
    [InlineData("conflicting_actor_ref_alias")]
    public void ValidateResponse_NewAfterlifeProfileWithoutExclusiveCanonicalActorId_ReportsBindingMismatch(
        string mutation)
    {
        const string actorType = "custom_afterlife_actor";
        const string actorId = "custom_actor_exact_identity";
        var profileState = JsonNode.Parse(BuildCompleteAfterlifeBindingProfileStateJson(
            actorType,
            actorId,
            includeProfile: true))!.AsObject();
        var profile = profileState[AfterlifeEntityProfileState.ProfilesProperty]![0]!.AsObject();
        switch (mutation)
        {
            case "actor_ref_only":
                profile.Remove("actorId");
                profile["actorRef"] = actorId;
                break;
            case "same_actor_ref_alias":
                profile["actorRef"] = actorId;
                break;
            case "conflicting_actor_ref_alias":
                profile["actorRef"] = "different_actor";
                break;
        }

        var response = new JsonObject
        {
            [AfterlifeEntityProfileState.ResponseProfilesProperty] = new JsonArray(profile.DeepClone())
        };
        using var document = JsonDocument.Parse(response.ToJsonString());

        var issues = _validator.ValidateResponse(document.RootElement);

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_actor_binding_mismatch" &&
            issue.Actor == $"{actorType}:{actorId}");
    }

    [Fact]
    public void ValidateResponse_NewAfterlifeProfileWithDuplicateCanonicalActorId_ReportsBindingMismatch()
    {
        const string actorType = "custom_afterlife_actor";
        const string actorId = "custom_actor_exact_identity";
        var profileState = JsonNode.Parse(BuildCompleteAfterlifeBindingProfileStateJson(
            actorType,
            actorId,
            includeProfile: true))!.AsObject();
        var profileJson = profileState[AfterlifeEntityProfileState.ProfilesProperty]![0]!.ToJsonString();
        var actorIdMarker = $"\"actorId\":\"{actorId}\"";
        var actorIdOffset = profileJson.IndexOf(actorIdMarker, StringComparison.Ordinal);
        Assert.True(actorIdOffset >= 0);
        profileJson = profileJson.Insert(actorIdOffset, "\"actorId\":\"shadow_identity\",");
        using var document = JsonDocument.Parse($$"""
        {
          "afterlifeEntityProfiles": [{{profileJson}}]
        }
        """);

        var issues = _validator.ValidateResponse(document.RootElement);

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_actor_binding_mismatch" &&
            issue.Actor == $"{actorType}:{actorId}");
    }

    [Fact]
    public async Task ValidateGameStateAsync_NewCanonicalMortalNpcWithPermanentIdAndValidatedPreTurnAuthority_ReportsMissingMaterialization()
    {
        const string path = "game_state/npcs/npc_core.json";
        const string preTurnJson = """
        {
          "UpdateNPCs": [],
          "NPCsInScene": []
        }
        """;
        const string currentJson = """
        {
          "UpdateNPCs": [
            {
              "NPCId": "npc_0042",
              "name": "Архивист станции"
            }
          ],
          "NPCsInScene": []
        }
        """;
        await WriteCurrentAndValidatedPreTurnAsync(path, currentJson, preTurnJson);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_missing" &&
            issue.Actor == "mortal_npc:npc_0042");
    }

    [Fact]
    public async Task ValidateGameStateAsync_LegacyMortalNpcWithoutStructuredPromotion_DoesNotRequireMaterialization()
    {
        const string path = "game_state/npcs/npc_core.json";
        const string legacyJson = """
        {
          "UpdateNPCs": [
            {
              "NPCId": "teacher_trader_combatant_legacy",
              "name": "Учитель, торговец и ветеран",
              "occupation": "Наставляет, торгует и рассказывает о сражениях",
              "teacherProfile": { "canTeach": false, "skills": [] },
              "tradeState": { "canTrade": false },
              "activeSkills": [],
              "passiveSkills": []
            }
          ],
          "NPCsInScene": []
        }
        """;
        await WriteCurrentAndValidatedPreTurnAsync(path, legacyJson, legacyJson);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code == "actor_materialization_missing" &&
            issue.Actor == "mortal_npc:teacher_trader_combatant_legacy");
    }

    [Theory]
    [InlineData("teacher")]
    [InlineData("trader")]
    [InlineData("combat")]
    [InlineData("actor_brain")]
    public async Task ValidateGameStateAsync_LegacyMortalNpcWithStructuredPromotion_ReportsMissingMaterialization(
        string promotion)
    {
        const string path = "game_state/npcs/npc_core.json";
        var preTurnJson = BuildMortalPromotionStateJson(promotion: null);
        var currentJson = BuildMortalPromotionStateJson(promotion);
        await WriteCurrentAndValidatedPreTurnAsync(path, currentJson, preTurnJson);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_missing" &&
            issue.Actor == "mortal_npc:legacy_structured_actor");
    }

    private static string BuildMortalPromotionStateJson(string? promotion)
    {
        var actor = new JsonObject
        {
            ["NPCId"] = "legacy_structured_actor",
            ["name"] = "Смотритель узла",
            ["teacherProfile"] = new JsonObject
            {
                ["canTeach"] = false,
                ["skills"] = new JsonArray()
            },
            ["tradeState"] = new JsonObject
            {
                ["canTrade"] = false
            },
            ["activeSkills"] = new JsonArray(),
            ["passiveSkills"] = new JsonArray()
        };

        switch (promotion)
        {
            case "teacher":
                actor["teacherProfile"]!["canTeach"] = true;
                actor["teacherProfile"]!["skills"]!.AsArray().Add(new JsonObject
                {
                    ["skillId"] = "skill_signal",
                    ["skillName"] = "Чтение сигнала",
                    ["masteryLevel"] = 2
                });
                break;
            case "trader":
                actor["tradeState"]!["canTrade"] = true;
                actor["tradeState"]!["merchantProfile"] = "GeneralGoods";
                break;
            case "combat":
                actor["activeSkills"]!.AsArray().Add(new JsonObject { ["skillId"] = "skill_signal" });
                break;
            case "actor_brain":
                actor["currentActivity"] = new JsonObject { ["activityId"] = "activity_signal" };
                break;
        }

        return new JsonObject
        {
            ["UpdateNPCs"] = new JsonArray(actor),
            ["NPCsInScene"] = new JsonArray()
        }.ToJsonString();
    }

    [Fact]
    public async Task ValidateGameStateAsync_NewCanonicalNonPlayerAfterlifeProfile_ReportsMissingMaterialization()
    {
        const string path = "game_state/meta/afterlife_entity_profiles.json";
        var preTurnJson = BuildAfterlifeProfileStateJson(includeProfile: false);
        var currentJson = BuildAfterlifeProfileStateJson(includeProfile: true);
        await WriteCurrentAndValidatedPreTurnAsync(path, currentJson, preTurnJson);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_missing" &&
            issue.Actor == "radiant_actor:afterlife_actor_0042");
    }

    [Fact]
    public async Task ValidateGameStateAsync_NewPlayerSoulProfile_DoesNotRequireNonPlayerMaterialization()
    {
        const string path = "game_state/meta/afterlife_entity_profiles.json";
        var preTurnJson = BuildAfterlifeProfileStateJson(includeProfile: false);
        var currentJson = BuildAfterlifeProfileStateJson(
            includeProfile: true,
            actorType: "player_soul",
            actorId: "player_soul");
        await WriteCurrentAndValidatedPreTurnAsync(path, currentJson, preTurnJson);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code == "actor_materialization_missing" &&
            issue.Actor == "player_soul:player_soul");
    }

    [Fact]
    public async Task ValidateGameStateAsync_UntouchedLegacyNonPlayerAfterlifeProfile_DoesNotRequireMaterialization()
    {
        const string path = "game_state/meta/afterlife_entity_profiles.json";
        var legacyJson = BuildAfterlifeProfileStateJson(
            includeProfile: true,
            actorId: "mentor_combatant_legacy_profile");
        await WriteCurrentAndValidatedPreTurnAsync(path, legacyJson, legacyJson);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code == "actor_materialization_missing" &&
            issue.Actor == "radiant_actor:mentor_combatant_legacy_profile");
    }

    [Fact]
    public async Task ValidateGameStateAsync_UntouchedLegacyActorRefProfile_DoesNotRequireMaterialization()
    {
        const string path = "game_state/meta/afterlife_entity_profiles.json";
        const string actorId = "mentor_combatant_legacy_actor_ref";
        var legacyRoot = JsonNode.Parse(BuildAfterlifeProfileStateJson(
            includeProfile: true,
            actorId: actorId))!.AsObject();
        var legacyProfile = legacyRoot[AfterlifeEntityProfileState.ProfilesProperty]![0]!.AsObject();
        legacyProfile.Remove("actorId");
        legacyProfile["actorRef"] = actorId;
        var legacyJson = legacyRoot.ToJsonString();
        await WriteCurrentAndValidatedPreTurnAsync(path, legacyJson, legacyJson);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code is "actor_materialization_missing" or
                "actor_materialization_actor_binding_mismatch" &&
            issue.Actor == $"radiant_actor:{actorId}");
    }

    [Fact]
    public async Task ValidateGameStateAsync_UntouchedLegacyAfterlifeProfileWithCaseVariantActorType_DoesNotApplyStrictMaterializationTokenRule()
    {
        const string path = "game_state/meta/afterlife_entity_profiles.json";
        var legacyJson = BuildAfterlifeProfileStateJson(
            includeProfile: true,
            actorType: "Radiant_Actor",
            actorId: "legacy_case_variant_profile");
        await WriteCurrentAndValidatedPreTurnAsync(path, legacyJson, legacyJson);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code == "actor_materialization_invalid_actor_type" &&
            issue.Actor == "Radiant_Actor:legacy_case_variant_profile");
    }

    [Theory]
    [InlineData("mentor")]
    [InlineData("combat")]
    [InlineData("actor_brain")]
    public async Task ValidateGameStateAsync_LegacyNonPlayerAfterlifeProfileWithStructuredPromotion_ReportsMissingMaterialization(
        string promotion)
    {
        const string path = "game_state/meta/afterlife_entity_profiles.json";
        var preTurnJson = BuildAfterlifeProfileStateJson(includeProfile: true);
        var currentJson = BuildAfterlifeProfileStateJson(includeProfile: true, promotion: promotion);
        await WriteCurrentAndValidatedPreTurnAsync(path, currentJson, preTurnJson);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_missing" &&
            issue.Actor == "radiant_actor:afterlife_actor_0042");
    }

    [Theory]
    [InlineData("guardian", "guardian", "guardian_materialization_binding")]
    [InlineData("resident", "resident", "resident_materialization_binding")]
    [InlineData("radiant", "radiant_actor", "radiant_materialization_binding")]
    [InlineData("saref", "saref_agent", "saref_materialization_binding")]
    public async Task ValidateAcceptedTurnContinuity_NewAfterlifeSourceActorWithoutExactProfile_IsRejected(
        string sourceKind,
        string actorType,
        string actorId)
    {
        var sourcePath = GetAfterlifeBindingSourcePath(sourceKind);
        var preTurnSource = BuildAfterlifeBindingSourceJson(sourceKind, actorId, includeActor: false);
        var currentSource = BuildAfterlifeBindingSourceJson(
            sourceKind,
            actorId,
            includeActor: true,
            includeTypeSpecificMemory: sourceKind is "guardian" or "resident");
        var emptyProfiles = BuildCompleteAfterlifeBindingProfileStateJson(actorType, actorId, includeProfile: false);
        await WriteAfterlifeBindingScenarioAsync(
            sourcePath,
            currentSource,
            preTurnSource,
            emptyProfiles,
            emptyProfiles);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "afterlife_actor_materialization_profile_missing" &&
            issue.Actor == $"{actorType}:{actorId}");
    }

    [Theory]
    [InlineData("guardian", "guardian", "guardian_complete_binding")]
    [InlineData("resident", "resident", "resident_complete_binding")]
    [InlineData("radiant", "radiant_actor", "radiant_complete_binding")]
    [InlineData("saref", "saref_agent", "saref_complete_binding")]
    public async Task ValidateAcceptedTurnContinuity_NewAfterlifeSourceActorWithExactCompleteProfile_PassesBinding(
        string sourceKind,
        string actorType,
        string actorId)
    {
        var sourcePath = GetAfterlifeBindingSourcePath(sourceKind);
        var preTurnSource = BuildAfterlifeBindingSourceJson(sourceKind, actorId, includeActor: false);
        var currentSource = BuildAfterlifeBindingSourceJson(
            sourceKind,
            actorId,
            includeActor: true,
            includeTypeSpecificMemory: sourceKind is "guardian" or "resident");
        var preTurnProfiles = BuildCompleteAfterlifeBindingProfileStateJson(actorType, actorId, includeProfile: false);
        var currentProfiles = BuildCompleteAfterlifeBindingProfileStateJson(actorType, actorId, includeProfile: true);
        await WriteAfterlifeBindingScenarioAsync(
            sourcePath,
            currentSource,
            preTurnSource,
            currentProfiles,
            preTurnProfiles);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.DoesNotContain(issues, issue =>
            (issue.Code is "afterlife_actor_materialization_profile_missing" or
                "afterlife_actor_materialization_profile_ambiguous" or
                "afterlife_actor_materialization_memory_missing") &&
            issue.Actor == $"{actorType}:{actorId}");
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_AfterlifeProfileBinding_IsCaseSensitive()
    {
        const string sourceKind = "guardian";
        const string actorId = "guardian_exact_case";
        var sourcePath = GetAfterlifeBindingSourcePath(sourceKind);
        var preTurnSource = BuildAfterlifeBindingSourceJson(sourceKind, actorId, includeActor: false);
        var currentSource = BuildAfterlifeBindingSourceJson(sourceKind, actorId, includeActor: true);
        var preTurnProfiles = BuildCompleteAfterlifeBindingProfileStateJson("guardian", actorId, includeProfile: false);
        var currentProfiles = BuildCompleteAfterlifeBindingProfileStateJson(
            "guardian",
            "Guardian_Exact_Case",
            includeProfile: true);
        await WriteAfterlifeBindingScenarioAsync(
            sourcePath,
            currentSource,
            preTurnSource,
            currentProfiles,
            preTurnProfiles);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "afterlife_actor_materialization_profile_missing" &&
            issue.Actor == "guardian:guardian_exact_case");
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_DuplicateExactAfterlifeProfiles_AreRejectedAsAmbiguous()
    {
        const string sourceKind = "resident";
        const string actorId = "resident_ambiguous_profile";
        var sourcePath = GetAfterlifeBindingSourcePath(sourceKind);
        var preTurnSource = BuildAfterlifeBindingSourceJson(sourceKind, actorId, includeActor: false);
        var currentSource = BuildAfterlifeBindingSourceJson(sourceKind, actorId, includeActor: true);
        var preTurnProfiles = BuildCompleteAfterlifeBindingProfileStateJson("resident", actorId, includeProfile: false);
        var currentProfiles = BuildCompleteAfterlifeBindingProfileStateJson(
            "resident",
            actorId,
            includeProfile: true,
            duplicateProfile: true);
        await WriteAfterlifeBindingScenarioAsync(
            sourcePath,
            currentSource,
            preTurnSource,
            currentProfiles,
            preTurnProfiles);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "afterlife_actor_materialization_profile_ambiguous" &&
            issue.Actor == "resident:resident_ambiguous_profile");
    }

    [Theory]
    [InlineData("guardian", "guardian", "guardian_legacy_binding")]
    [InlineData("resident", "resident", "resident_legacy_binding")]
    [InlineData("radiant", "radiant_actor", "radiant_legacy_binding")]
    [InlineData("saref", "saref_agent", "saref_legacy_binding")]
    public async Task ValidateAcceptedTurnContinuity_UntouchedLegacyAfterlifeSourceWithoutProfile_RemainsLoadable(
        string sourceKind,
        string actorType,
        string actorId)
    {
        var sourcePath = GetAfterlifeBindingSourcePath(sourceKind);
        var legacySource = BuildAfterlifeBindingSourceJson(sourceKind, actorId, includeActor: true);
        var emptyProfiles = BuildCompleteAfterlifeBindingProfileStateJson(actorType, actorId, includeProfile: false);
        await WriteAfterlifeBindingScenarioAsync(
            sourcePath,
            legacySource,
            legacySource,
            emptyProfiles,
            emptyProfiles);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_NewShiningFactionHead_RequiresCurrentMaterialization()
    {
        const string actorType = "guardian";
        const string actorId = "guardian_promoted_head";
        var preTurnShining = BuildShiningLeadershipBindingStateJson(actorType, actorId, "vacant");
        var currentShining = BuildShiningLeadershipBindingStateJson(actorType, actorId, "secure");
        var legacyProfileState = BuildCompleteAfterlifeBindingProfileStateJson(
            actorType,
            actorId,
            includeProfile: true,
            includeEnvelope: false);
        await WriteAfterlifeBindingScenarioAsync(
            ShiningAbodeState.StatePath,
            currentShining,
            preTurnShining,
            legacyProfileState,
            legacyProfileState);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_missing" &&
            issue.Actor == "guardian:guardian_promoted_head");
    }

    [Theory]
    [InlineData("player_soul", "player_soul")]
    public async Task ValidateAcceptedTurnContinuity_ShiningLeadershipExceptions_DoNotRequireNonPlayerProfile(
        string actorType,
        string actorId)
    {
        var preTurnShining = BuildShiningLeadershipBindingStateJson(actorType, actorId, "vacant");
        var currentShining = BuildShiningLeadershipBindingStateJson(actorType, actorId, "secure");
        var emptyProfiles = BuildCompleteAfterlifeBindingProfileStateJson(actorType, actorId, includeProfile: false);
        await WriteAfterlifeBindingScenarioAsync(
            ShiningAbodeState.StatePath,
            currentShining,
            preTurnShining,
            emptyProfiles,
            emptyProfiles);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code is "afterlife_actor_materialization_profile_missing" or "actor_materialization_missing" &&
            issue.Actor == $"{actorType}:{actorId}");
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_GenuinelyVacantShiningLeadership_DoesNotRequireProfile()
    {
        const string actorType = "guardian";
        const string actorId = "unused_vacant_head";
        var preTurnShining = BuildShiningLeadershipBindingStateJson(actorType, actorId, "vacant");
        var currentShining = BuildShiningLeadershipBindingStateJson(actorType, actorId, "vacant");
        var emptyProfiles = BuildCompleteAfterlifeBindingProfileStateJson(actorType, actorId, includeProfile: false);
        await WriteAfterlifeBindingScenarioAsync(
            ShiningAbodeState.StatePath,
            currentShining,
            preTurnShining,
            emptyProfiles,
            emptyProfiles);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code is "afterlife_actor_materialization_profile_missing" or "actor_materialization_missing");
    }

    [Theory]
    [InlineData("guardian", "guardian", "guardian_memory_missing")]
    [InlineData("resident", "resident", "resident_memory_missing")]
    [InlineData("radiant", "radiant_actor", "radiant_memory_missing")]
    [InlineData("saref", "saref_agent", "saref_memory_missing")]
    public async Task ValidateAcceptedTurnContinuity_NewAfterlifeSourceActorWithoutActorOwnedMemory_IsRejected(
        string sourceKind,
        string actorType,
        string actorId)
    {
        var sourcePath = GetAfterlifeBindingSourcePath(sourceKind);
        var preTurnSource = BuildAfterlifeBindingSourceJson(sourceKind, actorId, includeActor: false);
        var currentSource = BuildAfterlifeBindingSourceJson(sourceKind, actorId, includeActor: true);
        var preTurnProfiles = BuildCompleteAfterlifeBindingProfileStateJson(actorType, actorId, includeProfile: false);
        var currentProfiles = BuildCompleteAfterlifeBindingProfileStateJson(
            actorType,
            actorId,
            includeProfile: true,
            includeMemory: false);
        await WriteAfterlifeBindingScenarioAsync(
            sourcePath,
            currentSource,
            preTurnSource,
            currentProfiles,
            preTurnProfiles);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "afterlife_actor_materialization_memory_missing" &&
            issue.Actor == $"{actorType}:{actorId}");
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_NewProfileOnlyAfterlifeActorWithoutActorOwnedMemory_IsRejected()
    {
        const string actorType = "custom_afterlife_actor";
        const string actorId = "custom_profile_only_memory_missing";
        var preTurnProfiles = BuildCompleteAfterlifeBindingProfileStateJson(
            actorType,
            actorId,
            includeProfile: false);
        var currentProfiles = BuildCompleteAfterlifeBindingProfileStateJson(
            actorType,
            actorId,
            includeProfile: true,
            includeMemory: false);
        await WriteCurrentAndValidatedPreTurnAsync(
            AfterlifeEntityProfileState.StatePath,
            currentProfiles,
            preTurnProfiles);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "afterlife_actor_materialization_memory_missing" &&
            issue.Actor == $"{actorType}:{actorId}");
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_ProgressionHistoryDoesNotSubstituteForNewProfileMemory()
    {
        const string actorType = "custom_afterlife_actor";
        const string actorId = "custom_progression_without_memory";
        var preTurnProfiles = BuildCompleteAfterlifeBindingProfileStateJson(
            actorType,
            actorId,
            includeProfile: false);
        var currentRoot = JsonNode.Parse(BuildCompleteAfterlifeBindingProfileStateJson(
            actorType,
            actorId,
            includeProfile: true,
            includeMemory: false))!.AsObject();
        var profile = currentRoot[AfterlifeEntityProfileState.ProfilesProperty]![0]!.AsObject();
        profile["ledger"]!.AsArray().Add(new JsonObject
        {
            ["entryId"] = "progression_is_not_memory",
            ["summary"] = "Изменился уровень искусства."
        });
        profile["materialization"]!["sections"]!["progressionHistory"] =
            new JsonObject { ["state"] = "populated" };
        await WriteCurrentAndValidatedPreTurnAsync(
            AfterlifeEntityProfileState.StatePath,
            currentRoot.ToJsonString(),
            preTurnProfiles);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "afterlife_actor_materialization_memory_missing" &&
            issue.Actor == $"{actorType}:{actorId}");
    }

    [Theory]
    [InlineData("guardian", "guardian", "guardian_profile_memory_is_not_journal")]
    [InlineData("resident", "resident", "resident_profile_memory_is_not_journal")]
    public async Task ValidateAcceptedTurnContinuity_DedicatedMemorySurfaceCannotBeReplacedByProfileSummary(
        string sourceKind,
        string actorType,
        string actorId)
    {
        var sourcePath = GetAfterlifeBindingSourcePath(sourceKind);
        var preTurnSource = BuildAfterlifeBindingSourceJson(sourceKind, actorId, includeActor: false);
        var currentSource = BuildAfterlifeBindingSourceJson(sourceKind, actorId, includeActor: true);
        var preTurnProfiles = BuildCompleteAfterlifeBindingProfileStateJson(actorType, actorId, includeProfile: false);
        var currentProfiles = BuildCompleteAfterlifeBindingProfileStateJson(actorType, actorId, includeProfile: true);
        await WriteAfterlifeBindingScenarioAsync(
            sourcePath,
            currentSource,
            preTurnSource,
            currentProfiles,
            preTurnProfiles);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "afterlife_actor_materialization_memory_missing" &&
            issue.Actor == $"{actorType}:{actorId}");
    }

    [Theory]
    [InlineData("guardian", "guardian", "guardian_existing_source_new_profile")]
    [InlineData("resident", "resident", "resident_existing_source_new_profile")]
    [InlineData("radiant", "radiant_actor", "radiant_existing_source_new_profile")]
    [InlineData("saref", "saref_agent", "saref_existing_source_new_profile")]
    public async Task ValidateAcceptedTurnContinuity_FirstProfileForExistingSourceStillRequiresMemory(
        string sourceKind,
        string actorType,
        string actorId)
    {
        var sourcePath = GetAfterlifeBindingSourcePath(sourceKind);
        var source = BuildAfterlifeBindingSourceJson(sourceKind, actorId, includeActor: true);
        var preTurnProfiles = BuildCompleteAfterlifeBindingProfileStateJson(actorType, actorId, includeProfile: false);
        var currentProfiles = BuildCompleteAfterlifeBindingProfileStateJson(
            actorType,
            actorId,
            includeProfile: true,
            includeMemory: false);
        await WriteAfterlifeBindingScenarioAsync(
            sourcePath,
            source,
            source,
            currentProfiles,
            preTurnProfiles);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "afterlife_actor_materialization_memory_missing" &&
            issue.Actor == $"{actorType}:{actorId}");
    }

    [Theory]
    [InlineData("guardian", "guardian", "guardian_type_memory")]
    [InlineData("resident", "resident", "resident_type_memory")]
    public async Task ValidateAcceptedTurnContinuity_NewAfterlifeActorWithExactTypeSpecificMemory_PassesMemoryContract(
        string sourceKind,
        string actorType,
        string actorId)
    {
        var sourcePath = GetAfterlifeBindingSourcePath(sourceKind);
        var preTurnSource = BuildAfterlifeBindingSourceJson(sourceKind, actorId, includeActor: false);
        var currentSource = BuildAfterlifeBindingSourceJson(
            sourceKind,
            actorId,
            includeActor: true,
            includeTypeSpecificMemory: true);
        var preTurnProfiles = BuildCompleteAfterlifeBindingProfileStateJson(actorType, actorId, includeProfile: false);
        var currentProfiles = BuildCompleteAfterlifeBindingProfileStateJson(
            actorType,
            actorId,
            includeProfile: true,
            includeMemory: false);
        await WriteAfterlifeBindingScenarioAsync(
            sourcePath,
            currentSource,
            preTurnSource,
            currentProfiles,
            preTurnProfiles);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code == "afterlife_actor_materialization_memory_missing" &&
            issue.Actor == $"{actorType}:{actorId}");
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_DuplicatePreTurnAfterlifeSourceAuthority_IsRejected()
    {
        const string sourceKind = "guardian";
        const string actorId = "guardian_duplicate_baseline";
        var sourcePath = GetAfterlifeBindingSourcePath(sourceKind);
        var preTurnRoot = JsonNode.Parse(BuildAfterlifeBindingSourceJson(sourceKind, actorId, includeActor: true))!.AsObject();
        preTurnRoot["guardians"]!.AsArray().Add(preTurnRoot["guardians"]![0]!.DeepClone());
        var currentSource = BuildAfterlifeBindingSourceJson(sourceKind, actorId, includeActor: true);
        var emptyProfiles = BuildCompleteAfterlifeBindingProfileStateJson("guardian", actorId, includeProfile: false);
        await WriteAfterlifeBindingScenarioAsync(
            sourcePath,
            currentSource,
            preTurnRoot.ToJsonString(),
            emptyProfiles,
            emptyProfiles);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_pre_turn_authority_unusable" &&
            issue.FilePath == sourcePath);
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_AfterlifeSourceSnapshotWithoutBaselineRegistration_IsRejected()
    {
        const string sourceKind = "guardian";
        const string actorId = "guardian_orphan_snapshot";
        var sourcePath = GetAfterlifeBindingSourcePath(sourceKind);
        var legacySource = BuildAfterlifeBindingSourceJson(sourceKind, actorId, includeActor: true);
        var emptyProfiles = BuildCompleteAfterlifeBindingProfileStateJson("guardian", actorId, includeProfile: false);
        await WriteAfterlifeBindingScenarioAsync(
            sourcePath,
            legacySource,
            legacySource,
            emptyProfiles,
            emptyProfiles);
        await RemovePendingTurnBaselineRegistrationAsync(sourcePath);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_pre_turn_authority_unusable" &&
            issue.FilePath == sourcePath);
    }

    [Theory]
    [InlineData("guardian", "missing_id")]
    [InlineData("guardian", "duplicate_id")]
    [InlineData("resident", "missing_id")]
    [InlineData("radiant", "wrong_actor_type")]
    [InlineData("saref", "non_object_root")]
    public async Task ValidateAcceptedTurnContinuity_MalformedCurrentAfterlifeBindingSource_IsRejected(
        string sourceKind,
        string mutation)
    {
        const string actorId = "afterlife_malformed_current_source";
        var sourcePath = GetAfterlifeBindingSourcePath(sourceKind);
        var currentSource = BuildMalformedAfterlifeBindingSourceJson(sourceKind, actorId, mutation);
        var preTurnSource = BuildAfterlifeBindingSourceJson(sourceKind, actorId, includeActor: false);
        var emptyProfiles = BuildCompleteAfterlifeBindingProfileStateJson(
            sourceKind == "radiant" ? "radiant_actor" : sourceKind == "saref" ? "saref_agent" : sourceKind,
            actorId,
            includeProfile: false);
        await WriteAfterlifeBindingScenarioAsync(
            sourcePath,
            currentSource,
            preTurnSource,
            emptyProfiles,
            emptyProfiles);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "afterlife_actor_binding_current_authority_unusable" &&
            issue.FilePath == sourcePath);
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_MalformedCurrentAfterlifeProfileAuthority_IsRejected()
    {
        const string sourceKind = "guardian";
        const string actorId = "guardian_malformed_profile_authority";
        var sourcePath = GetAfterlifeBindingSourcePath(sourceKind);
        var currentProfiles = """{ "schemaVersion": 1 }""";
        var preTurnProfiles = BuildCompleteAfterlifeBindingProfileStateJson("guardian", actorId, includeProfile: false);
        await WriteAfterlifeBindingScenarioAsync(
            sourcePath,
            BuildAfterlifeBindingSourceJson(sourceKind, actorId, includeActor: true),
            BuildAfterlifeBindingSourceJson(sourceKind, actorId, includeActor: false),
            currentProfiles,
            preTurnProfiles);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "afterlife_actor_binding_current_authority_unusable" &&
            issue.FilePath == AfterlifeEntityProfileState.StatePath);
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_TrackedAfterlifeProfilesDeleted_IsRejected()
    {
        const string actorType = "guardian";
        const string actorId = "guardian_deleted_profile_authority";
        var sourcePath = GetAfterlifeBindingSourcePath(actorType);
        var source = BuildAfterlifeBindingSourceJson(actorType, actorId, includeActor: true);
        var profiles = BuildCompleteAfterlifeBindingProfileStateJson(actorType, actorId, includeProfile: true);
        await WriteAfterlifeBindingScenarioAsync(
            sourcePath,
            source,
            source,
            profiles,
            profiles);
        _fs.DeleteFile(AfterlifeEntityProfileState.StatePath);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "afterlife_actor_binding_current_authority_unusable" &&
            issue.FilePath == AfterlifeEntityProfileState.StatePath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{ \"schemaVersion\": 1 }")]
    public async Task ValidateAcceptedTurnContinuity_TrackedAfterlifeSourceActorRemoved_IsRejected(
        string? replacementJson)
    {
        const string sourceKind = "guardian";
        const string actorId = "guardian_deleted_source_authority";
        var sourcePath = GetAfterlifeBindingSourcePath(sourceKind);
        var source = BuildAfterlifeBindingSourceJson(sourceKind, actorId, includeActor: true);
        var profiles = BuildCompleteAfterlifeBindingProfileStateJson("guardian", actorId, includeProfile: true);
        await WriteAfterlifeBindingScenarioAsync(
            sourcePath,
            source,
            source,
            profiles,
            profiles);
        if (replacementJson == null)
            _fs.DeleteFile(sourcePath);
        else
            await _fs.WriteFileAtomicAsync(sourcePath, replacementJson);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "afterlife_actor_binding_current_authority_unusable" &&
            issue.FilePath == sourcePath);
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_ProfileWithCaseVariantIdentityAlias_IsRejectedAsUnusableAuthority()
    {
        const string sourceKind = "guardian";
        const string actorId = "guardian_case_alias";
        var sourcePath = GetAfterlifeBindingSourcePath(sourceKind);
        var currentProfilesRoot = JsonNode.Parse(BuildCompleteAfterlifeBindingProfileStateJson(
            "guardian",
            actorId,
            includeProfile: true))!.AsObject();
        currentProfilesRoot[AfterlifeEntityProfileState.ProfilesProperty]![0]!["ActorId"] = actorId;
        var preTurnProfiles = BuildCompleteAfterlifeBindingProfileStateJson("guardian", actorId, includeProfile: false);
        await WriteAfterlifeBindingScenarioAsync(
            sourcePath,
            BuildAfterlifeBindingSourceJson(sourceKind, actorId, includeActor: true),
            BuildAfterlifeBindingSourceJson(sourceKind, actorId, includeActor: false),
            currentProfilesRoot.ToJsonString(),
            preTurnProfiles);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "afterlife_actor_binding_current_authority_unusable" &&
            issue.FilePath == AfterlifeEntityProfileState.StatePath);
    }

    [Theory]
    [InlineData("actor_id_alias")]
    [InlineData("actor_ref_alias")]
    public async Task ValidateAcceptedTurnContinuity_ProfileWithOneValidAndOneMalformedIdentityAlias_IsRejected(
        string mutation)
    {
        const string sourceKind = "guardian";
        const string actorId = "guardian_conflicting_identity_alias";
        var sourcePath = GetAfterlifeBindingSourcePath(sourceKind);
        var currentProfilesRoot = JsonNode.Parse(BuildCompleteAfterlifeBindingProfileStateJson(
            "guardian",
            actorId,
            includeProfile: true))!.AsObject();
        var profile = currentProfilesRoot[AfterlifeEntityProfileState.ProfilesProperty]![0]!.AsObject();
        if (mutation == "actor_id_alias")
        {
            profile.Remove("actorId");
            profile["actorRef"] = actorId;
            profile["ActorId"] = "shadow_identity";
        }
        else
        {
            profile["ActorRef"] = "shadow_identity";
        }

        await WriteAfterlifeBindingScenarioAsync(
            sourcePath,
            BuildAfterlifeBindingSourceJson(sourceKind, actorId, includeActor: true),
            BuildAfterlifeBindingSourceJson(sourceKind, actorId, includeActor: false),
            currentProfilesRoot.ToJsonString(),
            BuildCompleteAfterlifeBindingProfileStateJson("guardian", actorId, includeProfile: false));

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "afterlife_actor_binding_current_authority_unusable" &&
            issue.FilePath == AfterlifeEntityProfileState.StatePath);
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_ExistingLegacyGuardianGainingTradeAuthority_RequiresMaterialization()
    {
        const string actorId = "guardian_promoted_to_merchant";
        const string abodeId = "abode_promoted_to_merchant";
        var sourcePath = GetAfterlifeBindingSourcePath("guardian");
        var preTurnSource = JsonNode.Parse(BuildAfterlifeBindingSourceJson(
            "guardian",
            actorId,
            includeActor: true))!.AsObject();
        var currentSource = preTurnSource.DeepClone().AsObject();
        var guardian = currentSource["guardians"]![0]!.AsObject();
        guardian["abode"] = new JsonObject { ["abodeId"] = abodeId };
        currentSource["activeGuardian"] = guardian.DeepClone();
        currentSource["chaosSeaNavigation"] = new JsonObject { ["currentAbodeId"] = abodeId };
        var legacyProfilesRoot = JsonNode.Parse(BuildCompleteAfterlifeBindingProfileStateJson(
            "guardian",
            actorId,
            includeProfile: true,
            includeEnvelope: false))!.AsObject();
        legacyProfilesRoot[AfterlifeEntityProfileState.ProfilesProperty]![0]!["realm"] = "Chaos Sea";
        var legacyProfiles = legacyProfilesRoot.ToJsonString();

        await WriteAfterlifeBindingScenarioAsync(
            sourcePath,
            currentSource.ToJsonString(),
            preTurnSource.ToJsonString(),
            legacyProfiles,
            legacyProfiles);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_missing" &&
            issue.Actor == $"guardian:{actorId}");
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_ExistingLegacyShiningHeadReachingTradeTier_RequiresMaterialization()
    {
        const string actorType = "radiant_actor";
        const string actorId = "radiant_head_promoted_to_merchant";
        var preTurnShining = JsonNode.Parse(BuildShiningLeadershipBindingStateJson(
            actorType,
            actorId,
            ShiningAbodeState.LeadershipStateSecure))!.AsObject();
        var currentShining = preTurnShining.DeepClone().AsObject();
        preTurnShining["factions"]![0]!["factionStrength"] = 24;
        currentShining["factions"]![0]!["factionStrength"] = 25;
        preTurnShining["factions"]![0]!["factionLifecycle"] = new JsonObject
        {
            ["state"] = ShiningAbodeState.FactionLifecycleStateActive
        };
        currentShining["factions"]![0]!["factionLifecycle"] = new JsonObject
        {
            ["state"] = ShiningAbodeState.FactionLifecycleStateActive
        };
        var legacyProfiles = BuildCompleteAfterlifeBindingProfileStateJson(
            actorType,
            actorId,
            includeProfile: true,
            includeEnvelope: false);

        await WriteAfterlifeBindingScenarioAsync(
            ShiningAbodeState.StatePath,
            currentShining.ToJsonString(),
            preTurnShining.ToJsonString(),
            legacyProfiles,
            legacyProfiles);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_missing" &&
            issue.Actor == $"{actorType}:{actorId}");
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_DeletingOneBoundLegacyProfile_IsRejected()
    {
        const string actorType = "guardian";
        const string actorId = "guardian_deleted_legacy_profile";
        var sourcePath = GetAfterlifeBindingSourcePath(actorType);
        var source = BuildAfterlifeBindingSourceJson(actorType, actorId, includeActor: true);
        var preTurnProfiles = BuildCompleteAfterlifeBindingProfileStateJson(
            actorType,
            actorId,
            includeProfile: true,
            includeEnvelope: false);
        var currentProfiles = BuildCompleteAfterlifeBindingProfileStateJson(actorType, actorId, includeProfile: false);

        await WriteAfterlifeBindingScenarioAsync(
            sourcePath,
            source,
            source,
            currentProfiles,
            preTurnProfiles);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "afterlife_actor_materialization_profile_missing" &&
            issue.Actor == $"{actorType}:{actorId}");
    }

    [Fact]
    public void ValidateResponse_FullAfterlifeProfileCarrier_DefersPositiveTradeAuthorityToAcceptedTurn()
    {
        var state = JsonNode.Parse(BuildCompleteAfterlifeBindingProfileStateJson(
            "guardian",
            "guardian_full_response_merchant",
            includeProfile: true))!.AsObject();
        var profile = state[AfterlifeEntityProfileState.ProfilesProperty]![0]!.AsObject();
        profile["realm"] = "Chaos Sea";
        profile[ActorMaterializationContract.PropertyName]!["capabilities"]!["canTrade"] = true;
        var response = new JsonObject
        {
            ["response"] = "Хранитель открывает полное досье торговой роли.",
            [AfterlifeEntityProfileState.ResponseProfilesProperty] = new JsonArray(profile.DeepClone())
        };
        using var document = JsonDocument.Parse(response.ToJsonString());

        var issues = _validator.ValidateResponse(document.RootElement);

        Assert.DoesNotContain(issues, issue =>
            issue.Code == "actor_materialization_capability_mismatch" &&
            issue.Section == "canTrade");
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_NewShiningFactionHeadWithExactCompleteProfile_PassesBinding()
    {
        const string actorType = "resident";
        const string actorId = "resident_complete_head";
        var preTurnShining = BuildShiningLeadershipBindingStateJson(actorType, actorId, "vacant");
        var currentShining = BuildShiningLeadershipBindingStateJson(actorType, actorId, "secure");
        var preTurnProfiles = BuildCompleteAfterlifeBindingProfileStateJson(actorType, actorId, includeProfile: false);
        var currentProfiles = BuildCompleteAfterlifeBindingProfileStateJson(actorType, actorId, includeProfile: true);
        await WriteAfterlifeBindingScenarioAsync(
            ShiningAbodeState.StatePath,
            currentShining,
            preTurnShining,
            currentProfiles,
            preTurnProfiles);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.DoesNotContain(issues, issue =>
            (issue.Code is "afterlife_actor_materialization_profile_missing" or
                "afterlife_actor_materialization_profile_ambiguous" or
                "afterlife_actor_materialization_memory_missing" or
                "actor_materialization_missing") &&
            issue.Actor == "resident:resident_complete_head");
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_DuplicatePreTurnAfterlifeProfiles_AreRejectedAsUnusableAuthority()
    {
        const string actorType = "guardian";
        const string actorId = "guardian_duplicate_profile_baseline";
        var currentProfiles = BuildCompleteAfterlifeBindingProfileStateJson(actorType, actorId, includeProfile: true);
        var preTurnProfiles = BuildCompleteAfterlifeBindingProfileStateJson(
            actorType,
            actorId,
            includeProfile: true,
            duplicateProfile: true);
        await WriteCurrentAndValidatedPreTurnAsync(
            AfterlifeEntityProfileState.StatePath,
            currentProfiles,
            preTurnProfiles);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_pre_turn_authority_unusable" &&
            issue.FilePath == AfterlifeEntityProfileState.StatePath);
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_MalformedPreTurnAfterlifeProfileIdentity_IsRejectedAsUnusableAuthority()
    {
        const string malformedPreTurnProfiles = """
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "displayName": "Профиль без actorId"
            }
          ]
        }
        """;
        const string currentProfiles = """{ "schemaVersion": 1, "profiles": [] }""";
        await WriteCurrentAndValidatedPreTurnAsync(
            AfterlifeEntityProfileState.StatePath,
            currentProfiles,
            malformedPreTurnProfiles);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_pre_turn_authority_unusable" &&
            issue.FilePath == AfterlifeEntityProfileState.StatePath);
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_MalformedPreTurnShiningLeadership_IsRejectedAsUnusableAuthority()
    {
        const string actorType = "guardian";
        const string actorId = "guardian_malformed_leadership_baseline";
        var preTurnShiningRoot = JsonNode.Parse(BuildShiningLeadershipBindingStateJson(
            actorType,
            actorId,
            ShiningAbodeState.LeadershipStateVacant))!.AsObject();
        preTurnShiningRoot["factions"] = new JsonObject();
        var currentShining = BuildShiningLeadershipBindingStateJson(actorType, actorId, ShiningAbodeState.LeadershipStateSecure);
        var legacyProfiles = BuildCompleteAfterlifeBindingProfileStateJson(
            actorType,
            actorId,
            includeProfile: true,
            includeEnvelope: false);
        await WriteAfterlifeBindingScenarioAsync(
            ShiningAbodeState.StatePath,
            currentShining,
            preTurnShiningRoot.ToJsonString(),
            legacyProfiles,
            legacyProfiles);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_pre_turn_authority_unusable" &&
            issue.FilePath == ShiningAbodeState.StatePath);
    }

    [Theory]
    [InlineData("missing_head_actor_id")]
    [InlineData("duplicate_head_actor_id")]
    public async Task ValidateAcceptedTurnContinuity_MalformedPreTurnShiningLeadershipIdentity_IsRejectedAsUnusableAuthority(
        string mutation)
    {
        const string actorType = "guardian";
        const string actorId = "guardian_malformed_identity_baseline";
        var preTurnShining = BuildShiningLeadershipBindingStateJson(
            actorType,
            actorId,
            ShiningAbodeState.LeadershipStateSecure);
        if (mutation == "missing_head_actor_id")
        {
            var preTurnRoot = JsonNode.Parse(preTurnShining)!.AsObject();
            preTurnRoot["factions"]![0]!["leadership"]!.AsObject().Remove("headActorId");
            preTurnShining = preTurnRoot.ToJsonString();
        }
        else
        {
            var marker = $"\"headActorId\":\"{actorId}\"";
            var offset = preTurnShining.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(offset >= 0);
            preTurnShining = preTurnShining.Insert(offset, "\"headActorId\":\"shadow_identity\",");
        }

        var currentShining = BuildShiningLeadershipBindingStateJson(
            actorType,
            actorId,
            ShiningAbodeState.LeadershipStateVacant);
        var legacyProfiles = BuildCompleteAfterlifeBindingProfileStateJson(
            actorType,
            actorId,
            includeProfile: true,
            includeEnvelope: false);
        await WriteAfterlifeBindingScenarioAsync(
            ShiningAbodeState.StatePath,
            currentShining,
            preTurnShining,
            legacyProfiles,
            legacyProfiles);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_pre_turn_authority_unusable" &&
            issue.FilePath == ShiningAbodeState.StatePath);
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_DuplicatePreTurnShiningHeadIdentity_IsRejectedAsUnusableAuthority()
    {
        const string actorType = "guardian";
        const string actorId = "guardian_duplicate_head_baseline";
        var preTurnShiningRoot = JsonNode.Parse(BuildShiningLeadershipBindingStateJson(
            actorType,
            actorId,
            ShiningAbodeState.LeadershipStateSecure))!.AsObject();
        var duplicateFaction = preTurnShiningRoot["factions"]![0]!.DeepClone().AsObject();
        duplicateFaction["factionId"] = "faction_materialization_binding_duplicate";
        preTurnShiningRoot["factions"]!.AsArray().Add(duplicateFaction);
        var currentShining = BuildShiningLeadershipBindingStateJson(actorType, actorId, ShiningAbodeState.LeadershipStateSecure);
        var legacyProfiles = BuildCompleteAfterlifeBindingProfileStateJson(
            actorType,
            actorId,
            includeProfile: true,
            includeEnvelope: false);
        await WriteAfterlifeBindingScenarioAsync(
            ShiningAbodeState.StatePath,
            currentShining,
            preTurnShiningRoot.ToJsonString(),
            legacyProfiles,
            legacyProfiles);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_pre_turn_authority_unusable" &&
            issue.FilePath == ShiningAbodeState.StatePath);
    }

    private static string GetAfterlifeBindingSourcePath(string sourceKind) => sourceKind switch
    {
        "guardian" => "game_state/meta/guardians.json",
        "resident" => GuardianAbodeResidentState.StatePath,
        "radiant" => ShiningAbodeState.StatePath,
        "saref" => SarefMainStoryState.StatePath,
        _ => throw new ArgumentOutOfRangeException(nameof(sourceKind), sourceKind, null)
    };

    private static string BuildAfterlifeBindingSourceJson(
        string sourceKind,
        string actorId,
        bool includeActor,
        bool includeTypeSpecificMemory = false)
    {
        var root = new JsonObject { ["schemaVersion"] = 1 };
        switch (sourceKind)
        {
            case "guardian":
                root["guardians"] = includeActor
                    ? new JsonArray(new JsonObject
                    {
                        ["guardianId"] = actorId,
                        ["name"] = "Хранитель точного договора",
                        ["musings"] = includeTypeSpecificMemory
                            ? new JsonArray(new JsonObject
                            {
                                ["turn"] = 42,
                                ["mood"] = "calm",
                                ["thought"] = "Я вижу точную связь между моими обязанностями и памятью."
                            })
                            : new JsonArray()
                    })
                    : new JsonArray();
                break;
            case "resident":
                root[GuardianAbodeResidentState.EntriesProperty] = includeActor
                    ? new JsonArray(new JsonObject
                    {
                        ["residentId"] = actorId,
                        ["guardianId"] = "guardian_binding_host",
                        ["abodeId"] = "abode_binding_host",
                        ["displayName"] = "Резидент точного договора"
                    })
                    : new JsonArray();
                root[GuardianAbodeResidentState.ThoughtJournalProperty] = includeTypeSpecificMemory
                    ? new JsonArray(new JsonObject
                    {
                        ["entryId"] = $"thought_{actorId}",
                        ["residentId"] = actorId,
                        ["thought"] = "Я помню своё решение остаться в этой обители."
                    })
                    : new JsonArray();
                break;
            case "radiant":
                root["factions"] = new JsonArray();
                root["shiningPoliticalActors"] = includeActor
                    ? new JsonArray(new JsonObject
                    {
                        ["actorType"] = "radiant_actor",
                        ["actorId"] = actorId,
                        ["displayName"] = "Светозарный актор точного договора",
                        ["politicalStatus"] = ShiningAbodeState.PoliticalStatusElder
                    })
                    : new JsonArray();
                break;
            case "saref":
                root["factionLinks"] = new JsonObject
                {
                    ["knownAgents"] = includeActor
                        ? new JsonArray(new JsonObject
                        {
                            ["agentId"] = actorId,
                            ["displayName"] = "Посланник точного договора"
                        })
                        : new JsonArray()
                };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(sourceKind), sourceKind, null);
        }

        return root.ToJsonString();
    }

    private static string BuildMalformedAfterlifeBindingSourceJson(
        string sourceKind,
        string actorId,
        string mutation)
    {
        if (mutation == "non_object_root")
            return "[]";

        var root = JsonNode.Parse(BuildAfterlifeBindingSourceJson(sourceKind, actorId, includeActor: true))!.AsObject();
        JsonObject actor;
        switch (sourceKind)
        {
            case "guardian":
                actor = root["guardians"]![0]!.AsObject();
                if (mutation == "duplicate_id")
                {
                    root["guardians"]!.AsArray().Add(actor.DeepClone());
                    break;
                }
                actor.Remove("guardianId");
                break;
            case "resident":
                actor = root[GuardianAbodeResidentState.EntriesProperty]![0]!.AsObject();
                actor.Remove("residentId");
                break;
            case "radiant":
                actor = root["shiningPoliticalActors"]![0]!.AsObject();
                actor["actorType"] = "guardian";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(sourceKind), sourceKind, null);
        }

        return root.ToJsonString();
    }

    private static string BuildShiningLeadershipBindingStateJson(
        string actorType,
        string actorId,
        string leadershipState)
    {
        var isVacant = string.Equals(leadershipState, ShiningAbodeState.LeadershipStateVacant, StringComparison.Ordinal);
        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["factions"] = new JsonArray(new JsonObject
            {
                ["factionId"] = "faction_materialization_binding",
                ["leadership"] = new JsonObject
                {
                    ["leadershipState"] = leadershipState,
                    ["headActorType"] = isVacant ? null : actorType,
                    ["headActorId"] = isVacant ? null : actorId
                }
            }),
            ["shiningPoliticalActors"] = new JsonArray()
        }.ToJsonString();
    }

    private static string BuildCompleteAfterlifeBindingProfileStateJson(
        string actorType,
        string actorId,
        bool includeProfile,
        bool includeMemory = true,
        bool duplicateProfile = false,
        bool includeEnvelope = true)
    {
        var profiles = new JsonArray();
        if (includeProfile)
        {
            profiles.Add(BuildCompleteAfterlifeBindingProfile(
                actorType,
                actorId,
                includeMemory,
                includeEnvelope,
                materializationSuffix: "a"));
            if (duplicateProfile)
            {
                profiles.Add(BuildCompleteAfterlifeBindingProfile(
                    actorType,
                    actorId,
                    includeMemory,
                    includeEnvelope,
                    materializationSuffix: "b"));
            }
        }

        return new JsonObject
        {
            ["schemaVersion"] = 1,
            [AfterlifeEntityProfileState.ProfilesProperty] = profiles
        }.ToJsonString();
    }

    private static JsonObject BuildCompleteAfterlifeBindingProfile(
        string actorType,
        string actorId,
        bool includeMemory,
        bool includeEnvelope,
        string materializationSuffix)
    {
        var profile = new JsonObject
        {
            ["actorType"] = actorType,
            ["actorId"] = actorId,
            ["displayName"] = "Сущность точного договора",
            ["realm"] = "Shining Abode",
            ["standardArts"] = new JsonObject { ["guard"] = 1 },
            ["specialArts"] = new JsonArray(),
            ["customStates"] = new JsonArray(),
            ["fateCards"] = new JsonArray(),
            ["relationships"] = new JsonArray(),
            ["goals"] = new JsonObject
            {
                ["goalId"] = $"goal_{actorId}",
                ["shortTermGoal"] = "Сохранить точную связь между источниками.",
                ["longTermGoal"] = "Поддерживать целостность своей роли.",
                ["plan"] = "Проверять собственные записи и действовать согласно роли.",
                ["gmThoughtsSummary"] = "Я должен сохранить точную связь между источниками.",
                ["updatedAtTurn"] = 42
            },
            ["personalQuests"] = new JsonArray(),
            ["currentActivity"] = null,
            ["completedActivities"] = new JsonArray(),
            ["progressionStrategy"] = new JsonObject
            {
                ["strategyId"] = $"strategy_{actorId}",
                ["summary"] = "Сохраняет точную связь между источниками.",
                ["priorityOrder"] = new JsonArray("guard")
            },
            ["ledger"] = new JsonArray(),
            ["progressionLedger"] = new JsonArray()
        };
        if (includeMemory)
            profile["gmThoughtsSummary"] = "Я помню, зачем принял эту роль.";

        if (includeEnvelope)
        {
            profile[ActorMaterializationContract.PropertyName] = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["materializationId"] = $"mat_{actorType}_{actorId}_{materializationSuffix}",
                ["actorType"] = actorType,
                ["actorId"] = actorId,
                ["materializedAtTurn"] = 42,
                ["state"] = "complete",
                ["capabilities"] = new JsonObject
                {
                    ["canFight"] = true,
                    ["canTeach"] = false,
                    ["canTrade"] = false
                },
                ["sections"] = new JsonObject
                {
                    ["standardArts"] = new JsonObject { ["state"] = "populated" },
                    ["specialArts"] = EmptyByDesign("Личное духовное искусство ещё не сформировано."),
                    ["customStates"] = EmptyByDesign("Особых духовных состояний сейчас нет."),
                    ["fateCards"] = EmptyByDesign("Карта судьбы ещё не открыта."),
                    ["relationships"] = EmptyByDesign("Устойчивые связи ещё не сложились."),
                    ["agency"] = new JsonObject { ["state"] = "populated" },
                    ["progressionHistory"] = EmptyByDesign("История развития ещё не началась.")
                }
            };
        }

        return profile;

        static JsonObject EmptyByDesign(string reason) => new()
        {
            ["state"] = "empty_by_design",
            ["reason"] = reason
        };
    }

    private async Task WriteAfterlifeBindingScenarioAsync(
        string sourcePath,
        string currentSourceJson,
        string preTurnSourceJson,
        string currentProfilesJson,
        string preTurnProfilesJson)
    {
        await _fs.WriteFileAtomicAsync(sourcePath, currentSourceJson);
        await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, currentProfilesJson);
        await _fs.WriteFileAtomicAsync(
            $"game_state/control/pending_turn_snapshot/{sourcePath}",
            preTurnSourceJson);
        await _fs.WriteFileAtomicAsync(
            $"game_state/control/pending_turn_snapshot/{AfterlifeEntityProfileState.StatePath}",
            preTurnProfilesJson);
        await WriteValidatedSnapshotManifestAsync(
            (sourcePath, preTurnSourceJson),
            (AfterlifeEntityProfileState.StatePath, preTurnProfilesJson));
    }

    private static string BuildAfterlifeProfileStateJson(
        bool includeProfile,
        string actorType = "radiant_actor",
        string actorId = "afterlife_actor_0042",
        string? promotion = null)
    {
        var profiles = new JsonArray();
        if (includeProfile)
        {
            var profile = new JsonObject
            {
                ["actorType"] = actorType,
                ["actorId"] = actorId,
                ["displayName"] = "Хранитель сигнального узла",
                ["realm"] = "Shining Abode",
                ["standardArts"] = new JsonObject
                {
                    ["seal_breaking"] = 0
                },
                ["specialArts"] = new JsonArray(),
                ["mentorProfile"] = new JsonObject
                {
                    ["canTeach"] = false
                },
                ["personalQuests"] = new JsonArray(),
                ["completedActivities"] = new JsonArray(),
                ["ledger"] = new JsonArray(),
                ["progressionLedger"] = new JsonArray()
            };

            switch (promotion)
            {
                case "mentor":
                    profile["mentorProfile"]!["canTeach"] = true;
                    profile["standardArts"]!["seal_breaking"] = 1;
                    break;
                case "combat":
                    profile["standardArts"]!["seal_breaking"] = 1;
                    break;
                case "actor_brain":
                    profile["ledger"]!.AsArray().Add(new JsonObject { ["entryId"] = "memory_signal" });
                    break;
            }

            profiles.Add(profile);
        }

        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["profiles"] = profiles
        }.ToJsonString();
    }

    [Theory]
    [InlineData("mortal")]
    [InlineData("afterlife")]
    public async Task ValidateGameStateAsync_MissingValidatedPreTurnFileAuthority_DoesNotBlanketRequireLegacyMaterialization(
        string family)
    {
        var targetPath = family == "mortal"
            ? "game_state/npcs/npc_core.json"
            : "game_state/meta/afterlife_entity_profiles.json";
        var authorityPath = family == "mortal"
            ? "game_state/meta/afterlife_entity_profiles.json"
            : "game_state/npcs/npc_core.json";
        var authorityJson = family == "mortal"
            ? """{ "schemaVersion": 1, "profiles": [] }"""
            : """{ "UpdateNPCs": [], "NPCsInScene": [] }""";
        var currentJson = family == "mortal"
            ? """
              {
                "UpdateNPCs": [
                  { "NPCId": "authority_gap_mortal", "name": "Свидетель разрыва authority" }
                ],
                "NPCsInScene": []
              }
              """
            : BuildAfterlifeProfileStateJson(
                includeProfile: true,
                actorId: "authority_gap_afterlife");
        var expectedActor = family == "mortal"
            ? "mortal_npc:authority_gap_mortal"
            : "radiant_actor:authority_gap_afterlife";
        var expectedShapeIssue = family == "mortal"
            ? "npc_full_object_missing_required_fields"
            : "afterlife_entity_profile_missing_currencies";

        await _fs.WriteFileAtomicAsync(targetPath, currentJson);
        await _fs.WriteFileAtomicAsync(
            $"game_state/control/pending_turn_snapshot/{authorityPath}",
            authorityJson);
        await WriteValidatedSnapshotManifestAsync((authorityPath, authorityJson));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code == "actor_materialization_missing" &&
            issue.Actor == expectedActor);
        Assert.Contains(issues, issue => issue.Code == expectedShapeIssue);
        if (family == "mortal")
        {
            Assert.Contains(issues, issue =>
                issue.Code == "actor_materialization_pre_turn_authority_unusable" &&
                issue.FilePath == targetPath);
        }
    }

    [Theory]
    [InlineData("mortal", "removed")]
    [InlineData("mortal", "changed")]
    [InlineData("afterlife", "removed")]
    [InlineData("afterlife", "changed")]
    public async Task ValidateGameStateAsync_ExistingHistoricalEnvelopeRemovedOrChanged_ReportsFocusedIssue(
        string family,
        string mutation)
    {
        var path = family == "mortal"
            ? "game_state/npcs/npc_core.json"
            : "game_state/meta/afterlife_entity_profiles.json";
        var preTurnJson = BuildHistoricalEnvelopeStateJson(family, mutation: null);
        var currentJson = BuildHistoricalEnvelopeStateJson(family, mutation);
        await WriteCurrentAndValidatedPreTurnAsync(path, currentJson, preTurnJson);

        var issues = await _validator.ValidateGameStateAsync();

        var expectedActor = family == "mortal"
            ? "mortal_npc:historical_mortal_actor"
            : "radiant_actor:historical_afterlife_actor";
        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_historical_envelope_changed" &&
            issue.Actor == expectedActor);
    }

    [Theory]
    [InlineData("mortal")]
    [InlineData("afterlife")]
    public async Task ValidateGameStateAsync_HistoricalEnvelopeWithOnlyPropertyOrderChanged_RemainsEquivalent(
        string family)
    {
        var path = family == "mortal"
            ? "game_state/npcs/npc_core.json"
            : "game_state/meta/afterlife_entity_profiles.json";
        var preTurnJson = BuildHistoricalEnvelopeStateJson(family, mutation: null);
        var currentJson = BuildHistoricalEnvelopeStateJson(family, mutation: "reordered");
        await WriteCurrentAndValidatedPreTurnAsync(path, currentJson, preTurnJson);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code == "actor_materialization_historical_envelope_changed");
    }

    [Theory]
    [InlineData("mortal")]
    [InlineData("afterlife")]
    public async Task ValidateGameStateAsync_HistoricalEnvelopeWithLaterGameplayDelta_DoesNotReinterpretEnvelope(
        string family)
    {
        var path = family == "mortal"
            ? "game_state/npcs/npc_core.json"
            : "game_state/meta/afterlife_entity_profiles.json";
        var preTurnJson = BuildHistoricalEnvelopeStateJson(family, mutation: null);
        var currentState = JsonNode.Parse(preTurnJson)!.AsObject();
        if (family == "mortal")
        {
            currentState["NPCsInScene"]![0]!["activeSkills"]!.AsArray().Add(
                new JsonObject { ["skillId"] = "learned_after_materialization" });
        }
        else
        {
            currentState["profiles"]![0]!["standardArts"]!["seal_breaking"] = 1;
        }

        await WriteCurrentAndValidatedPreTurnAsync(path, currentState.ToJsonString(), preTurnJson);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("actor_materialization_", StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData("mortal")]
    [InlineData("afterlife")]
    public async Task ValidateGameStateAsync_InvalidPreTurnRoot_DoesNotBlanketRequireLegacyMaterialization(
        string family)
    {
        var path = family == "mortal"
            ? "game_state/npcs/npc_core.json"
            : "game_state/meta/afterlife_entity_profiles.json";
        var currentJson = family == "mortal"
            ? BuildMortalPromotionStateJson(promotion: null)
            : BuildAfterlifeProfileStateJson(includeProfile: true);
        var expectedActor = family == "mortal"
            ? "mortal_npc:legacy_structured_actor"
            : "radiant_actor:afterlife_actor_0042";
        await WriteCurrentAndValidatedPreTurnAsync(path, currentJson, "[]");

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code == "actor_materialization_missing" &&
            issue.Actor == expectedActor);
    }

    [Theory]
    [InlineData("mortal")]
    [InlineData("afterlife")]
    public async Task ValidateAcceptedTurnContinuity_InvalidCurrentRoot_SkipsContinuity(
        string family)
    {
        var path = family == "mortal"
            ? "game_state/npcs/npc_core.json"
            : "game_state/meta/afterlife_entity_profiles.json";
        var preTurnJson = family == "mortal"
            ? """{ "UpdateNPCs": [], "NPCsInScene": [] }"""
            : """{ "schemaVersion": 1, "profiles": [] }""";
        await WriteCurrentAndValidatedPreTurnAsync(path, "[]", preTurnJson);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Empty(issues);
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_CurrentMortalActorWithConflictingIdentityAliases_IsRejected()
    {
        const string path = "game_state/npcs/npc_core.json";
        var preTurnJson = BuildMortalPromotionStateJson(promotion: null);
        var currentRoot = JsonNode.Parse(preTurnJson)!.AsObject();
        currentRoot["UpdateNPCs"]![0]!["id"] = "shadow_mortal_identity";
        await WriteCurrentAndValidatedPreTurnAsync(path, currentRoot.ToJsonString(), preTurnJson);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_current_authority_unusable" &&
            issue.FilePath == path);
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_CurrentAfterlifeProfileWithConflictingIdentityAliases_IsRejected()
    {
        const string path = "game_state/meta/afterlife_entity_profiles.json";
        var preTurnJson = BuildAfterlifeProfileStateJson(includeProfile: true);
        var currentRoot = JsonNode.Parse(preTurnJson)!.AsObject();
        currentRoot["profiles"]![0]!["actorRef"] = "shadow_afterlife_identity";
        await WriteCurrentAndValidatedPreTurnAsync(path, currentRoot.ToJsonString(), preTurnJson);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "afterlife_actor_binding_current_authority_unusable" &&
            issue.FilePath == path);
    }

    [Theory]
    [InlineData("mortal")]
    [InlineData("afterlife")]
    public async Task ValidateAcceptedTurnContinuity_NewEnvelopeOnLegacyActor_ValidatesCurrentContract(
        string family)
    {
        var path = family == "mortal"
            ? "game_state/npcs/npc_core.json"
            : "game_state/meta/afterlife_entity_profiles.json";
        var preTurnJson = BuildHistoricalEnvelopeStateJson(family, mutation: "removed");
        var currentState = JsonNode.Parse(BuildHistoricalEnvelopeStateJson(family, mutation: null))!.AsObject();
        var actor = family == "mortal"
            ? currentState["NPCsInScene"]![0]!
            : currentState["profiles"]![0]!;
        actor["materialization"]!["actorId"] = "wrong_actor_binding";
        await WriteCurrentAndValidatedPreTurnAsync(path, currentState.ToJsonString(), preTurnJson);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue => issue.Code == "actor_materialization_actor_binding_mismatch");
    }

    [Theory]
    [InlineData("mortal")]
    [InlineData("afterlife")]
    public async Task ValidateAcceptedTurnContinuity_FirstMaterializationStillChecksCurrentSectionContent(
        string family)
    {
        var path = family == "mortal"
            ? "game_state/npcs/npc_core.json"
            : "game_state/meta/afterlife_entity_profiles.json";
        var preTurnJson = family == "mortal"
            ? """{ "UpdateNPCs": [], "NPCsInScene": [] }"""
            : """{ "schemaVersion": 1, "profiles": [] }""";
        var currentState = JsonNode.Parse(BuildHistoricalEnvelopeStateJson(family, mutation: null))!.AsObject();
        if (family == "mortal")
        {
            currentState["NPCsInScene"]![0]!["activeSkills"]!.AsArray().Add(
                new JsonObject { ["skillId"] = "skill_present_on_first_materialization" });
        }
        else
        {
            currentState["profiles"]![0]!["standardArts"]!["seal_breaking"] = 1;
        }
        await WriteCurrentAndValidatedPreTurnAsync(path, currentState.ToJsonString(), preTurnJson);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        var expectedSection = family == "mortal" ? "skills" : "standardArts";
        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_section_content_mismatch" &&
            issue.Section == expectedSection);
    }

    [Fact]
    public async Task ValidateResponse_UsableSnapshotWithoutActor_NewPermanentIdWithEnvelopeAndInventory_HasNoErrors()
    {
        const string path = "game_state/npcs/npc_core.json";
        var currentJson = BuildMortalActorStateJson(
            "permanent_update_actor",
            sectionName: "UpdateNPCs",
            canTeach: true,
            includeEnvelope: true,
            includeInventory: true);
        const string preTurnJson = """{ "UpdateNPCs": [], "NPCsInScene": [] }""";
        await WriteCurrentAndValidatedPreTurnAsync(path, currentJson, preTurnJson);

        using var currentDocument = JsonDocument.Parse(currentJson);
        var issues = _validator.ValidateResponse(currentDocument.RootElement);

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_NewActiveGuardianWithExactTradeAuthority_PassesCanTradeCapability()
    {
        const string actorId = "guardian_trade_authority";
        const string abodeId = "abode_trade_authority";
        var sourcePath = GetAfterlifeBindingSourcePath("guardian");
        var currentSourceRoot = JsonNode.Parse(BuildAfterlifeBindingSourceJson(
            "guardian",
            actorId,
            includeActor: true))!.AsObject();
        var guardian = currentSourceRoot["guardians"]![0]!.AsObject();
        guardian["abode"] = new JsonObject { ["abodeId"] = abodeId };
        currentSourceRoot["activeGuardian"] = guardian.DeepClone();
        currentSourceRoot["chaosSeaNavigation"] = new JsonObject
        {
            ["currentAbodeId"] = abodeId
        };

        var currentProfilesRoot = JsonNode.Parse(BuildCompleteAfterlifeBindingProfileStateJson(
            "guardian",
            actorId,
            includeProfile: true))!.AsObject();
        var profile = currentProfilesRoot[AfterlifeEntityProfileState.ProfilesProperty]![0]!.AsObject();
        profile["realm"] = "Chaos Sea";
        profile[ActorMaterializationContract.PropertyName]!["capabilities"]!["canTrade"] = true;

        await WriteAfterlifeBindingScenarioAsync(
            sourcePath,
            currentSourceRoot.ToJsonString(),
            BuildAfterlifeBindingSourceJson("guardian", actorId, includeActor: false),
            currentProfilesRoot.ToJsonString(),
            BuildCompleteAfterlifeBindingProfileStateJson("guardian", actorId, includeProfile: false));

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code == "actor_materialization_capability_mismatch" &&
            issue.Section == "canTrade" &&
            issue.Actor == $"guardian:{actorId}");
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_NewTradableShiningFactionHead_PassesCanTradeCapability()
    {
        const string actorType = "resident";
        const string actorId = "resident_shining_trade_authority";
        var currentShiningRoot = JsonNode.Parse(BuildShiningLeadershipBindingStateJson(
            actorType,
            actorId,
            ShiningAbodeState.LeadershipStateSecure))!.AsObject();
        var faction = currentShiningRoot["factions"]![0]!.AsObject();
        faction["factionStrength"] = 50;
        faction["factionLifecycle"] = new JsonObject
        {
            ["state"] = ShiningAbodeState.FactionLifecycleStateActive
        };

        var currentProfilesRoot = JsonNode.Parse(BuildCompleteAfterlifeBindingProfileStateJson(
            actorType,
            actorId,
            includeProfile: true))!.AsObject();
        currentProfilesRoot[AfterlifeEntityProfileState.ProfilesProperty]![0]!
            [ActorMaterializationContract.PropertyName]!["capabilities"]!["canTrade"] = true;

        await WriteAfterlifeBindingScenarioAsync(
            ShiningAbodeState.StatePath,
            currentShiningRoot.ToJsonString(),
            BuildShiningLeadershipBindingStateJson(
                actorType,
                actorId,
                ShiningAbodeState.LeadershipStateVacant),
            currentProfilesRoot.ToJsonString(),
            BuildCompleteAfterlifeBindingProfileStateJson(actorType, actorId, includeProfile: false));

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code == "actor_materialization_capability_mismatch" &&
            issue.Section == "canTrade" &&
            issue.Actor == $"{actorType}:{actorId}");
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_GuardianOutsideCurrentAbode_CannotClaimTradeCapability()
    {
        const string actorId = "guardian_wrong_trade_abode";
        var currentSourceRoot = JsonNode.Parse(BuildAfterlifeBindingSourceJson(
            "guardian",
            actorId,
            includeActor: true,
            includeTypeSpecificMemory: true))!.AsObject();
        var guardian = currentSourceRoot["guardians"]![0]!.AsObject();
        guardian["abode"] = new JsonObject { ["abodeId"] = "abode_guardian_home" };
        currentSourceRoot["activeGuardian"] = guardian.DeepClone();
        currentSourceRoot["chaosSeaNavigation"] = new JsonObject
        {
            ["currentAbodeId"] = "abode_elsewhere"
        };
        var currentProfilesRoot = JsonNode.Parse(BuildCompleteAfterlifeBindingProfileStateJson(
            "guardian",
            actorId,
            includeProfile: true))!.AsObject();
        var profile = currentProfilesRoot[AfterlifeEntityProfileState.ProfilesProperty]![0]!.AsObject();
        profile["realm"] = "Chaos Sea";
        profile[ActorMaterializationContract.PropertyName]!["capabilities"]!["canTrade"] = true;

        await WriteAfterlifeBindingScenarioAsync(
            GetAfterlifeBindingSourcePath("guardian"),
            currentSourceRoot.ToJsonString(),
            BuildAfterlifeBindingSourceJson("guardian", actorId, includeActor: false),
            currentProfilesRoot.ToJsonString(),
            BuildCompleteAfterlifeBindingProfileStateJson("guardian", actorId, includeProfile: false));

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_capability_mismatch" &&
            issue.Section == "canTrade" &&
            issue.Actor == $"guardian:{actorId}");
    }

    [Theory]
    [InlineData(ShiningAbodeState.LeadershipStateContested, 25, ShiningAbodeState.FactionLifecycleStateActive, true)]
    [InlineData(ShiningAbodeState.LeadershipStateSecure, 24, ShiningAbodeState.FactionLifecycleStateActive, false)]
    [InlineData(ShiningAbodeState.LeadershipStateSecure, 50, ShiningAbodeState.FactionLifecycleStateBroken, false)]
    [InlineData(ShiningAbodeState.LeadershipStateSecure, 50, ShiningAbodeState.FactionLifecycleStateLeaderless, false)]
    public async Task ValidateAcceptedTurnContinuity_ShiningTradeAuthority_UsesLeadershipTierAndLifecycle(
        string leadershipState,
        int factionStrength,
        string lifecycleState,
        bool shouldAuthorize)
    {
        const string actorType = "radiant_actor";
        const string actorId = "radiant_trade_matrix_head";
        var currentShiningRoot = JsonNode.Parse(BuildShiningLeadershipBindingStateJson(
            actorType,
            actorId,
            leadershipState))!.AsObject();
        var faction = currentShiningRoot["factions"]![0]!.AsObject();
        faction["factionStrength"] = factionStrength;
        faction["factionLifecycle"] = new JsonObject { ["state"] = lifecycleState };
        var currentProfilesRoot = JsonNode.Parse(BuildCompleteAfterlifeBindingProfileStateJson(
            actorType,
            actorId,
            includeProfile: true))!.AsObject();
        currentProfilesRoot[AfterlifeEntityProfileState.ProfilesProperty]![0]!
            [ActorMaterializationContract.PropertyName]!["capabilities"]!["canTrade"] = true;

        await WriteAfterlifeBindingScenarioAsync(
            ShiningAbodeState.StatePath,
            currentShiningRoot.ToJsonString(),
            BuildShiningLeadershipBindingStateJson(
                actorType,
                actorId,
                ShiningAbodeState.LeadershipStateVacant),
            currentProfilesRoot.ToJsonString(),
            BuildCompleteAfterlifeBindingProfileStateJson(actorType, actorId, includeProfile: false));

        var issues = await InvokeAcceptedTurnContinuityAsync();
        var hasTradeMismatch = issues.Any(issue =>
            issue.Code == "actor_materialization_capability_mismatch" &&
            issue.Section == "canTrade" &&
            issue.Actor == $"{actorType}:{actorId}");

        Assert.Equal(!shouldAuthorize, hasTradeMismatch);
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_DuplicateShiningHeadTradeAuthority_IsRejectedAsAmbiguous()
    {
        const string actorType = "radiant_actor";
        const string actorId = "radiant_duplicate_trade_head";
        var currentShiningRoot = JsonNode.Parse(BuildShiningLeadershipBindingStateJson(
            actorType,
            actorId,
            ShiningAbodeState.LeadershipStateSecure))!.AsObject();
        var firstFaction = currentShiningRoot["factions"]![0]!.AsObject();
        firstFaction["factionStrength"] = 50;
        firstFaction["factionLifecycle"] = new JsonObject
        {
            ["state"] = ShiningAbodeState.FactionLifecycleStateActive
        };
        var duplicateFaction = firstFaction.DeepClone().AsObject();
        duplicateFaction["factionId"] = "faction_materialization_binding_duplicate_trade";
        currentShiningRoot["factions"]!.AsArray().Add(duplicateFaction);
        var currentProfiles = BuildCompleteAfterlifeBindingProfileStateJson(actorType, actorId, includeProfile: true);

        await WriteAfterlifeBindingScenarioAsync(
            ShiningAbodeState.StatePath,
            currentShiningRoot.ToJsonString(),
            BuildShiningLeadershipBindingStateJson(
                actorType,
                actorId,
                ShiningAbodeState.LeadershipStateVacant),
            currentProfiles,
            BuildCompleteAfterlifeBindingProfileStateJson(actorType, actorId, includeProfile: false));

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "afterlife_actor_binding_current_authority_unusable" &&
            issue.FilePath == ShiningAbodeState.StatePath);
    }

    [Fact]
    public async Task ValidateResponse_HashValidSnapshotWithNonObjectNpcEntry_NewPermanentIdInventory_IsBlockedFailClosed()
    {
        const string path = "game_state/npcs/npc_core.json";
        var currentJson = BuildMortalActorStateJson(
            "non_object_snapshot_actor",
            sectionName: "UpdateNPCs",
            canTeach: true,
            includeEnvelope: true,
            includeInventory: true);
        const string preTurnJson = """{ "UpdateNPCs": [null], "NPCsInScene": [] }""";
        await WriteCurrentAndValidatedPreTurnAsync(path, currentJson, preTurnJson);

        using var currentDocument = JsonDocument.Parse(currentJson);
        var issues = _validator.ValidateResponse(currentDocument.RootElement);

        Assert.Contains(issues, issue => issue.Code == "npc_existing_inventory_resend_forbidden");
    }

    [Fact]
    public async Task ValidateResponse_HashValidSnapshotWithUnreadablePermanentIdentity_NewPermanentIdInventory_IsBlockedFailClosed()
    {
        const string path = "game_state/npcs/npc_core.json";
        var currentJson = BuildMortalActorStateJson(
            "unreadable_identity_snapshot_actor",
            sectionName: "UpdateNPCs",
            canTeach: true,
            includeEnvelope: true,
            includeInventory: true);
        const string preTurnJson = """
        {
          "UpdateNPCs": [
            { "NPCId": 42, "npcId": null, "id": " " }
          ],
          "NPCsInScene": []
        }
        """;
        await WriteCurrentAndValidatedPreTurnAsync(path, currentJson, preTurnJson);

        using var currentDocument = JsonDocument.Parse(currentJson);
        var issues = _validator.ValidateResponse(currentDocument.RootElement);

        Assert.Contains(issues, issue => issue.Code == "npc_existing_inventory_resend_forbidden");
    }

    [Theory]
    [InlineData("UpdateNPCs", "UpdateNPCs")]
    [InlineData("NPCsInScene", "NPCsInScene")]
    [InlineData("UpdateNPCs", "updatenpcs")]
    [InlineData("NPCsInScene", "npcsinscene")]
    public async Task ValidateResponse_HashValidSnapshotWithDuplicateCanonicalNpcCarrier_IsBlockedFailClosed(
        string canonicalSectionName,
        string duplicateSectionName)
    {
        const string path = "game_state/npcs/npc_core.json";
        const string actorId = "duplicate_carrier_snapshot_actor";
        var currentJson = BuildMortalActorStateJson(
            actorId,
            sectionName: "UpdateNPCs",
            canTeach: true,
            includeEnvelope: true,
            includeInventory: true);
        var otherSectionName = canonicalSectionName == "UpdateNPCs" ? "NPCsInScene" : "UpdateNPCs";
        var preTurnJson = duplicateSectionName == canonicalSectionName
            ? $$"""
              {
                "{{canonicalSectionName}}": [{ "NPCId": "{{actorId}}" }],
                "{{duplicateSectionName}}": [],
                "{{otherSectionName}}": []
              }
              """
            : $$"""
              {
                "{{canonicalSectionName}}": [],
                "{{duplicateSectionName}}": [{ "NPCId": "{{actorId}}" }],
                "{{otherSectionName}}": []
              }
              """;
        await WriteCurrentAndValidatedPreTurnAsync(path, currentJson, preTurnJson);

        using var currentDocument = JsonDocument.Parse(currentJson);
        var issues = _validator.ValidateResponse(currentDocument.RootElement);

        Assert.Contains(issues, issue => issue.Code == "npc_existing_inventory_resend_forbidden");
    }

    [Theory]
    [InlineData("updatenpcs", "NPCsInScene")]
    [InlineData("npcsinscene", "UpdateNPCs")]
    public async Task ValidateResponse_HashValidSnapshotWithSingleNonCanonicalCaseNpcCarrier_IsBlockedFailClosed(
        string nonCanonicalSectionName,
        string otherCanonicalSectionName)
    {
        const string path = "game_state/npcs/npc_core.json";
        const string actorId = "noncanonical_carrier_snapshot_actor";
        var currentJson = BuildMortalActorStateJson(
            actorId,
            sectionName: "UpdateNPCs",
            canTeach: true,
            includeEnvelope: true,
            includeInventory: true);
        var preTurnJson = $$"""
        {
          "{{nonCanonicalSectionName}}": [{ "NPCId": "{{actorId}}" }],
          "{{otherCanonicalSectionName}}": []
        }
        """;
        await WriteCurrentAndValidatedPreTurnAsync(path, currentJson, preTurnJson);

        using var currentDocument = JsonDocument.Parse(currentJson);
        var issues = _validator.ValidateResponse(currentDocument.RootElement);

        Assert.Contains(issues, issue => issue.Code == "npc_existing_inventory_resend_forbidden");
    }

    [Theory]
    [InlineData("duplicate_npc_id")]
    [InlineData("conflicting_identity_aliases")]
    [InlineData("duplicate_materialization")]
    [InlineData("duplicate_teacher_profile")]
    [InlineData("duplicate_nested_can_teach")]
    [InlineData("case_variant_nested_can_teach")]
    public async Task ValidateResponse_HashValidSnapshotWithLossyActorAuthoritySubtree_IsBlockedFailClosed(
        string lossyMutation)
    {
        const string path = "game_state/npcs/npc_core.json";
        const string actorId = "lossy_actor_authority_snapshot_actor";
        var currentJson = BuildMortalActorStateJson(
            actorId,
            sectionName: "UpdateNPCs",
            canTeach: true,
            includeEnvelope: true,
            includeInventory: true);
        var preTurnJson = BuildLossyMortalActorAuthoritySnapshotJson(actorId, lossyMutation);
        await WriteCurrentAndValidatedPreTurnAsync(path, currentJson, preTurnJson);

        using var currentDocument = JsonDocument.Parse(currentJson);
        var issues = _validator.ValidateResponse(currentDocument.RootElement);

        Assert.Contains(issues, issue => issue.Code == "npc_existing_inventory_resend_forbidden");
    }

    [Fact]
    public void CanonicalMortalActorSnapshotAuthority_RealBootstrapNpcIdPair_IsAccepted()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 8,
            characterDescription: "Архивист новой жизни.",
            worldDescription: "Нейтральный испытательный мир.",
            startingCircumstances: "Наставник готов провести первый урок.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-19T00:00:00Z"));

        var snapshotJson = files["game_state/npcs/npc_core.json"].ToJsonString();

        Assert.True(CanReadCanonicalMortalActorSnapshotAuthority(snapshotJson));
    }

    [Theory]
    [InlineData("conflicting_pair")]
    [InlineData("generic_id_with_pair")]
    public void CanonicalMortalActorSnapshotAuthority_AmbiguousIdentityAliases_AreRejected(
        string mutation)
    {
        var actorJson = mutation switch
        {
            "conflicting_pair" => """{ "NPCId": "actor_alpha", "npcId": "actor_beta" }""",
            "generic_id_with_pair" => """{ "NPCId": "actor_alpha", "npcId": "actor_alpha", "id": "actor_alpha" }""",
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null)
        };
        var snapshotJson = $$"""
        {
          "UpdateNPCs": [{{actorJson}}],
          "NPCsInScene": []
        }
        """;

        Assert.False(CanReadCanonicalMortalActorSnapshotAuthority(snapshotJson));
    }

    [Fact]
    public void CanonicalMortalActorSnapshotAuthority_DuplicateActorInSameCarrier_IsRejected()
    {
        var root = JsonNode.Parse(BuildMortalActorStateJson(
            "same_carrier_duplicate_actor",
            sectionName: "NPCsInScene",
            canTeach: false,
            includeEnvelope: false))!.AsObject();
        root["NPCsInScene"]!.AsArray().Add(root["NPCsInScene"]![0]!.DeepClone());

        Assert.False(CanReadCanonicalMortalActorSnapshotAuthority(root.ToJsonString()));
    }

    [Fact]
    public void CanonicalMortalActorSnapshotAuthority_IdenticalCrossCarrierActors_AreAccepted()
    {
        var root = JsonNode.Parse(BuildMortalActorStateJson(
            "compatible_cross_carrier_actor",
            sectionName: "NPCsInScene",
            canTeach: false,
            includeEnvelope: false))!.AsObject();
        root["UpdateNPCs"]!.AsArray().Add(root["NPCsInScene"]![0]!.DeepClone());

        Assert.True(CanReadCanonicalMortalActorSnapshotAuthority(root.ToJsonString()));
    }

    [Fact]
    public void CanonicalMortalActorSnapshotAuthority_ConflictingCrossCarrierPromotionSignals_AreRejected()
    {
        var root = JsonNode.Parse(BuildMortalActorStateJson(
            "conflicting_cross_carrier_signals_actor",
            sectionName: "NPCsInScene",
            canTeach: false,
            includeEnvelope: false))!.AsObject();
        var conflictingActor = root["NPCsInScene"]![0]!.DeepClone().AsObject();
        conflictingActor["teacherProfile"] = new JsonObject
        {
            ["canTeach"] = true,
            ["skills"] = new JsonArray
            {
                new JsonObject
                {
                    ["skillId"] = "setting_neutral_instruction",
                    ["skillName"] = "Практическое наставничество",
                    ["masteryLevel"] = 1
                }
            }
        };
        root["UpdateNPCs"]!.AsArray().Add(conflictingActor);

        Assert.False(CanReadCanonicalMortalActorSnapshotAuthority(root.ToJsonString()));
    }

    [Fact]
    public void CanonicalMortalActorSnapshotAuthority_ConflictingCrossCarrierEnvelopes_AreRejected()
    {
        var root = JsonNode.Parse(BuildMortalActorStateJson(
            "conflicting_cross_carrier_envelope_actor",
            sectionName: "NPCsInScene",
            canTeach: true,
            includeEnvelope: true))!.AsObject();
        var conflictingActor = root["NPCsInScene"]![0]!.DeepClone().AsObject();
        conflictingActor["materialization"]!["materializationId"] = "mat_conflicting_cross_carrier";
        root["UpdateNPCs"]!.AsArray().Add(conflictingActor);

        Assert.False(CanReadCanonicalMortalActorSnapshotAuthority(root.ToJsonString()));
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_UnusableMortalSnapshot_ReportsAuthorityError()
    {
        const string path = "game_state/npcs/npc_core.json";
        var preTurnRoot = JsonNode.Parse(BuildMortalActorStateJson(
            "ambiguous_snapshot_actor",
            sectionName: "NPCsInScene",
            canTeach: false,
            includeEnvelope: false))!.AsObject();
        preTurnRoot["NPCsInScene"]!.AsArray().Add(
            preTurnRoot["NPCsInScene"]![0]!.DeepClone());
        var currentJson = BuildMortalActorStateJson(
            "ambiguous_snapshot_actor",
            sectionName: "NPCsInScene",
            canTeach: false,
            includeEnvelope: false);
        await WriteCurrentAndValidatedPreTurnAsync(path, currentJson, preTurnRoot.ToJsonString());

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_pre_turn_authority_unusable" &&
            issue.FilePath == path);
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_UnusableMortalSnapshotTransport_ReportsAuthorityError()
    {
        const string path = "game_state/npcs/npc_core.json";
        var currentJson = BuildMortalActorStateJson(
            "transport_guard_actor",
            sectionName: "NPCsInScene",
            canTeach: false,
            includeEnvelope: false);
        await WriteCurrentAndValidatedPreTurnAsync(path, currentJson, currentJson);
        await _fs.WriteFileAtomicAsync(
            PendingTurnSnapshotAuthority.AuthorityPath,
            """{ "schemaVersion": 1, "manifestPayloadHash": "tampered" }""");

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_pre_turn_authority_unusable" &&
            issue.FilePath == path);
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_NoSnapshotManifest_DoesNotReportAuthorityError()
    {
        const string path = "game_state/npcs/npc_core.json";
        var currentJson = BuildMortalActorStateJson(
            "ordinary_load_actor",
            sectionName: "NPCsInScene",
            canTeach: false,
            includeEnvelope: false);
        await _fs.WriteFileAtomicAsync(path, currentJson);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code == "actor_materialization_pre_turn_authority_unusable");
    }

    [Theory]
    [InlineData("partial_combat")]
    [InlineData("partial_teacher")]
    public void CanonicalMortalActorSnapshotAuthority_PartialCompositeDelta_DoesNotContradictCarrier(
        string partialDelta)
    {
        var root = JsonNode.Parse(BuildMortalActorStateJson(
            "partial_composite_delta_actor",
            sectionName: "NPCsInScene",
            canTeach: true,
            includeEnvelope: false))!.AsObject();
        var actor = root["NPCsInScene"]![0]!.AsObject();
        actor["activeSkills"] = new JsonArray
        {
            new JsonObject { ["skillId"] = "setting_neutral_practice" }
        };
        actor["passiveSkills"] = new JsonArray();
        root["UpdateNPCs"] = new JsonArray
        {
            partialDelta switch
            {
                "partial_combat" => new JsonObject
                {
                    ["NPCId"] = "partial_composite_delta_actor",
                    ["activeSkills"] = new JsonArray()
                },
                "partial_teacher" => new JsonObject
                {
                    ["NPCId"] = "partial_composite_delta_actor",
                    ["teacherProfile"] = new JsonObject
                    {
                        ["summary"] = "Уточнено расписание занятий."
                    }
                },
                _ => throw new ArgumentOutOfRangeException(nameof(partialDelta), partialDelta, null)
            }
        };

        Assert.True(CanReadCanonicalMortalActorSnapshotAuthority(root.ToJsonString()));
    }

    [Fact]
    public async Task ValidateResponse_HistoricalCarrierWithEnvelopeFreeDelta_RemainsUsableNextTurn()
    {
        const string path = "game_state/npcs/npc_core.json";
        var preTurnRoot = JsonNode.Parse(BuildMortalActorStateJson(
            "historical_actor_with_delta",
            sectionName: "NPCsInScene",
            canTeach: true,
            includeEnvelope: true))!.AsObject();
        preTurnRoot["UpdateNPCs"] = new JsonArray
        {
            new JsonObject
            {
                ["NPCId"] = "historical_actor_with_delta",
                ["plans"] = "Продолжить работу после принятого хода."
            }
        };
        var currentJson = BuildMortalActorStateJson(
            "new_actor_after_historical_delta",
            sectionName: "UpdateNPCs",
            canTeach: true,
            includeEnvelope: true,
            includeInventory: true);
        await WriteCurrentAndValidatedPreTurnAsync(path, currentJson, preTurnRoot.ToJsonString());

        using var currentDocument = JsonDocument.Parse(currentJson);
        var issues = _validator.ValidateResponse(currentDocument.RootElement);

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
    }

    [Theory]
    [InlineData(63, true)]
    [InlineData(64, false)]
    public void CanonicalMortalActorSnapshotAuthority_DepthBudget_IsEnforcedAtBoundary(
        int nestedObjectCount,
        bool expected)
    {
        JsonNode nested = JsonValue.Create("leaf")!;
        for (var index = 0; index < nestedObjectCount; index++)
            nested = new JsonObject { ["child"] = nested };

        var snapshotJson = BuildMortalActorAuthoritySnapshotWithPayload(nested);

        Assert.Equal(expected, CanReadCanonicalMortalActorSnapshotAuthority(snapshotJson));
    }

    [Theory]
    [InlineData(32765, true)]
    [InlineData(32766, false)]
    public void CanonicalMortalActorSnapshotAuthority_NodeBudget_IsEnforcedAtBoundary(
        int payloadNodeCount,
        bool expected)
    {
        var payload = new JsonArray();
        for (var index = 0; index < payloadNodeCount; index++)
            payload.Add(index);

        var snapshotJson = BuildMortalActorAuthoritySnapshotWithPayload(payload);

        Assert.Equal(expected, CanReadCanonicalMortalActorSnapshotAuthority(snapshotJson));
    }

    [Fact]
    public async Task ValidateResponse_ValidCrossSectionSameActorMerge_TruePromotionHasNoErrors()
    {
        const string path = "game_state/npcs/npc_core.json";
        const string actorId = "cross_section_merge_actor";
        var preTurnRoot = JsonNode.Parse(BuildMortalActorStateJson(
            actorId,
            sectionName: "NPCsInScene",
            canTeach: false,
            includeEnvelope: false))!.AsObject();
        var duplicateActor = preTurnRoot["NPCsInScene"]![0]!.DeepClone();
        preTurnRoot["UpdateNPCs"] = new JsonArray(duplicateActor);
        var currentJson = BuildMortalActorStateJson(
            actorId,
            sectionName: "UpdateNPCs",
            canTeach: true,
            includeEnvelope: true,
            includeInventory: true);
        await WriteCurrentAndValidatedPreTurnAsync(path, currentJson, preTurnRoot.ToJsonString());

        using var currentDocument = JsonDocument.Parse(currentJson);
        var issues = _validator.ValidateResponse(currentDocument.RootElement);

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
    }

    [Fact]
    public async Task ValidateResponse_MissingSnapshot_NewPermanentIdWithEnvelopeAndInventory_IsBlockedFailClosed()
    {
        const string path = "game_state/npcs/npc_core.json";
        var currentJson = BuildMortalActorStateJson(
            "missing_snapshot_actor",
            sectionName: "UpdateNPCs",
            canTeach: true,
            includeEnvelope: true,
            includeInventory: true);
        await _fs.WriteFileAtomicAsync(path, currentJson);

        using var currentDocument = JsonDocument.Parse(currentJson);
        var issues = _validator.ValidateResponse(currentDocument.RootElement);

        Assert.Contains(issues, issue => issue.Code == "npc_existing_inventory_resend_forbidden");
    }

    [Fact]
    public async Task ValidateResponse_MalformedValidatedNpcSnapshot_NewPermanentIdWithEnvelopeAndInventory_IsBlockedFailClosed()
    {
        const string path = "game_state/npcs/npc_core.json";
        var currentJson = BuildMortalActorStateJson(
            "malformed_snapshot_actor",
            sectionName: "UpdateNPCs",
            canTeach: true,
            includeEnvelope: true,
            includeInventory: true);
        await WriteCurrentAndValidatedPreTurnAsync(path, currentJson, "{ malformed validated npc snapshot");

        using var currentDocument = JsonDocument.Parse(currentJson);
        var issues = _validator.ValidateResponse(currentDocument.RootElement);

        Assert.Contains(issues, issue => issue.Code == "npc_existing_inventory_resend_forbidden");
    }

    [Fact]
    public async Task ValidateResponse_UnchangedLegacyActorAddingEnvelopeAndInventory_IsBlockedAsExistingResend()
    {
        const string path = "game_state/npcs/npc_core.json";
        var preTurnJson = BuildMortalActorStateJson(
            "unchanged_legacy_envelope_actor",
            sectionName: "NPCsInScene",
            canTeach: false,
            includeEnvelope: false);
        var currentJson = BuildMortalActorStateJson(
            "unchanged_legacy_envelope_actor",
            sectionName: "UpdateNPCs",
            canTeach: false,
            includeEnvelope: true,
            includeInventory: true);
        await WriteCurrentAndValidatedPreTurnAsync(path, currentJson, preTurnJson);

        using var currentDocument = JsonDocument.Parse(currentJson);
        var issues = _validator.ValidateResponse(currentDocument.RootElement);

        Assert.Contains(issues, issue => issue.Code == "npc_existing_inventory_resend_forbidden");
    }

    [Fact]
    public async Task ValidateResponse_TrueLegacyPromotionWithEnvelopeAndInventory_HasNoErrors()
    {
        const string path = "game_state/npcs/npc_core.json";
        var preTurnJson = BuildMortalActorStateJson(
            "legacy_promoted_actor",
            sectionName: "NPCsInScene",
            canTeach: false,
            includeEnvelope: false);
        var currentJson = BuildMortalActorStateJson(
            "legacy_promoted_actor",
            sectionName: "UpdateNPCs",
            canTeach: true,
            includeEnvelope: true,
            includeInventory: true);
        await WriteCurrentAndValidatedPreTurnAsync(path, currentJson, preTurnJson);

        using var currentDocument = JsonDocument.Parse(currentJson);
        var issues = _validator.ValidateResponse(currentDocument.RootElement);

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
    }

    [Fact]
    public async Task ValidateGameStateAsync_MaterializedExistingActorResendingEnvelopeAndInventory_IsRejectedPreTurnAware()
    {
        const string path = "game_state/npcs/npc_core.json";
        var preTurnJson = BuildMortalActorStateJson(
            "existing_resend_actor",
            sectionName: "NPCsInScene",
            canTeach: true,
            includeEnvelope: true,
            includeInventory: true);
        var currentRoot = JsonNode.Parse(preTurnJson)!.AsObject();
        var retainedActor = currentRoot["NPCsInScene"]![0]!.DeepClone();
        currentRoot["UpdateNPCs"] = new JsonArray(retainedActor);
        await WriteCurrentAndValidatedPreTurnAsync(path, currentRoot.ToJsonString(), preTurnJson);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_existing_resend_forbidden" &&
            issue.Actor == "mortal_npc:existing_resend_actor");
        Assert.Contains(issues, issue => issue.Code == "npc_existing_inventory_resend_forbidden");
    }

    [Fact]
    public async Task ValidateGameStateAsync_HistoricalCanonicalEnvelopeRetention_IsNotClassifiedAsResend()
    {
        const string path = "game_state/npcs/npc_core.json";
        var preTurnJson = BuildMortalActorStateJson(
            "historical_retained_actor",
            sectionName: "NPCsInScene",
            canTeach: true,
            includeEnvelope: true);
        var currentRoot = JsonNode.Parse(preTurnJson)!.AsObject();
        currentRoot["NPCsInScene"]![0]!["plans"] = "Продолжить работу после принятого хода.";
        await WriteCurrentAndValidatedPreTurnAsync(path, currentRoot.ToJsonString(), preTurnJson);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Actor == "mortal_npc:historical_retained_actor" &&
            issue.Code?.StartsWith("actor_materialization_", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_UnchangedHistoricalUpdateCarrier_IsNotClassifiedAsResend()
    {
        const string path = "game_state/npcs/npc_core.json";
        var preTurnJson = BuildMortalActorStateJson(
            "historical_update_carrier",
            sectionName: "UpdateNPCs",
            canTeach: true,
            includeEnvelope: true);
        await WriteCurrentAndValidatedPreTurnAsync(path, preTurnJson, preTurnJson);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Actor == "mortal_npc:historical_update_carrier" &&
            issue.Code == "actor_materialization_existing_resend_forbidden");
    }

    [Fact]
    public async Task ValidateGameStateAsync_ChangedHistoricalUpdateCarrierRetainingEnvelope_IsRejectedAsResend()
    {
        const string path = "game_state/npcs/npc_core.json";
        var preTurnJson = BuildMortalActorStateJson(
            "changed_historical_update_carrier",
            sectionName: "UpdateNPCs",
            canTeach: true,
            includeEnvelope: true);
        var currentRoot = JsonNode.Parse(preTurnJson)!.AsObject();
        currentRoot["UpdateNPCs"]![0]!["plans"] = "Новый план текущего хода.";
        await WriteCurrentAndValidatedPreTurnAsync(path, currentRoot.ToJsonString(), preTurnJson);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            issue.Actor == "mortal_npc:changed_historical_update_carrier" &&
            issue.Code == "actor_materialization_existing_resend_forbidden");
    }

    [Fact]
    public async Task ValidateGameStateAsync_CrossSectionHistoricalUpdateCarrierWithReorderedProperties_IsAllowed()
    {
        const string path = "game_state/npcs/npc_core.json";
        var preTurnRoot = JsonNode.Parse(BuildMortalActorStateJson(
            "cross_section_historical_carrier",
            sectionName: "NPCsInScene",
            canTeach: true,
            includeEnvelope: true))!.AsObject();
        var historicalUpdate = preTurnRoot["NPCsInScene"]![0]!.DeepClone().AsObject();
        preTurnRoot["UpdateNPCs"] = new JsonArray(historicalUpdate);

        var currentRoot = preTurnRoot.DeepClone().AsObject();
        var reorderedUpdate = new JsonObject();
        foreach (var property in historicalUpdate.Reverse())
            reorderedUpdate[property.Key] = property.Value?.DeepClone();
        currentRoot["UpdateNPCs"] = new JsonArray(reorderedUpdate);
        await WriteCurrentAndValidatedPreTurnAsync(
            path,
            currentRoot.ToJsonString(),
            preTurnRoot.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Actor == "mortal_npc:cross_section_historical_carrier" &&
            issue.Code == "actor_materialization_existing_resend_forbidden");
    }

    [Fact]
    public async Task ValidateGameStateAsync_CrossSectionChangedHistoricalUpdateCarrier_IsRejectedAsResend()
    {
        const string path = "game_state/npcs/npc_core.json";
        var preTurnRoot = JsonNode.Parse(BuildMortalActorStateJson(
            "cross_section_changed_carrier",
            sectionName: "NPCsInScene",
            canTeach: true,
            includeEnvelope: true))!.AsObject();
        preTurnRoot["UpdateNPCs"] = new JsonArray(preTurnRoot["NPCsInScene"]![0]!.DeepClone());
        var currentRoot = preTurnRoot.DeepClone().AsObject();
        currentRoot["UpdateNPCs"]![0]!["plans"] = "Изменённый delta с повторно присланным envelope.";
        await WriteCurrentAndValidatedPreTurnAsync(
            path,
            currentRoot.ToJsonString(),
            preTurnRoot.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            issue.Actor == "mortal_npc:cross_section_changed_carrier" &&
            issue.Code == "actor_materialization_existing_resend_forbidden");
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_HistoricalActorDedicatedDeltaWithoutEnvelope_Passes()
    {
        const string path = "game_state/npcs/npc_core.json";
        var preTurnJson = BuildMortalActorStateJson(
            "historical_delta_actor",
            sectionName: "NPCsInScene",
            canTeach: true,
            includeEnvelope: true);
        var currentRoot = JsonNode.Parse(preTurnJson)!.AsObject();
        currentRoot["UpdateNPCs"] = new JsonArray
        {
            new JsonObject
            {
                ["NPCId"] = "historical_delta_actor",
                ["plans"] = "Проверить следующую запись архива."
            }
        };
        await WriteCurrentAndValidatedPreTurnAsync(path, currentRoot.ToJsonString(), preTurnJson);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Actor == "mortal_npc:historical_delta_actor" &&
            issue.Code?.StartsWith("actor_materialization_", StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData("mortal")]
    [InlineData("afterlife")]
    public async Task ValidateAcceptedTurnContinuity_HistoricalActorRemovedFromCanonicalState_IsRejected(
        string family)
    {
        var path = family == "mortal"
            ? "game_state/npcs/npc_core.json"
            : AfterlifeEntityProfileState.StatePath;
        var preTurnJson = BuildHistoricalEnvelopeStateJson(family, mutation: null);
        var currentJson = family == "mortal"
            ? """{ "UpdateNPCs": [], "NPCsInScene": [] }"""
            : """{ "schemaVersion": 1, "profiles": [] }""";
        await WriteCurrentAndValidatedPreTurnAsync(path, currentJson, preTurnJson);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_historical_envelope_changed" &&
            issue.Actor == (family == "mortal"
                ? "mortal_npc:historical_mortal_actor"
                : "radiant_actor:historical_afterlife_actor"));
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_HistoricalActorDeltaWithoutCanonicalCarrier_IsRejected()
    {
        const string path = "game_state/npcs/npc_core.json";
        var preTurnJson = BuildMortalActorStateJson(
            "historical_delta_without_carrier",
            sectionName: "NPCsInScene",
            canTeach: true,
            includeEnvelope: true);
        const string currentJson = """
        {
          "UpdateNPCs": [
            {
              "NPCId": "historical_delta_without_carrier",
              "plans": "Продолжить работу после обновления."
            }
          ],
          "NPCsInScene": []
        }
        """;
        await WriteCurrentAndValidatedPreTurnAsync(path, currentJson, preTurnJson);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_historical_envelope_changed" &&
            issue.Actor == "mortal_npc:historical_delta_without_carrier");
    }

    [Theory]
    [InlineData("mortal", "removed")]
    [InlineData("mortal", "changed")]
    [InlineData("afterlife", "removed")]
    [InlineData("afterlife", "changed")]
    public async Task NormalizeThenValidate_HistoricalEnvelopeIsPreservedOnlyWhenOmitted(
        string family,
        string mutation)
    {
        var path = family == "mortal"
            ? "game_state/npcs/npc_core.json"
            : "game_state/meta/afterlife_entity_profiles.json";
        var preTurnJson = BuildHistoricalEnvelopeStateJson(family, mutation: null);
        var currentJson = BuildHistoricalEnvelopeStateJson(family, mutation);
        await WriteCurrentAndValidatedPreTurnAsync(path, currentJson, preTurnJson);
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [path] = $"game_state/control/pending_turn_snapshot/{path}"
        });

        var normalizedRoot = JsonNode.Parse((await _fs.ReadFileAsync(path))!)!.AsObject();
        var normalizedActor = family == "mortal"
            ? normalizedRoot["NPCsInScene"]![0]!
            : normalizedRoot["profiles"]![0]!;
        var normalizedMaterializationId = normalizedActor["materialization"]?["materializationId"]?.GetValue<string>();
        var issues = await InvokeAcceptedTurnContinuityAsync();

        if (mutation == "removed")
        {
            Assert.Equal(family == "mortal" ? "mat_mortal_historical" : "mat_afterlife_historical", normalizedMaterializationId);
            Assert.DoesNotContain(issues, issue => issue.Code == "actor_materialization_historical_envelope_changed");
        }
        else
        {
            Assert.Equal($"mat_{family}_changed", normalizedMaterializationId);
            Assert.Contains(issues, issue => issue.Code == "actor_materialization_historical_envelope_changed");
        }
    }

    [Fact]
    public void ValidateResponse_CompleteAfterlifeProfileWithMaterialization_PassesFullIntegration()
    {
        using var document = JsonDocument.Parse("""
        {
          "response": "Смотритель светлого архива завершает проверку записи.",
          "afterlifeEntityProfiles": [
            {
              "actorType": "radiant_actor",
              "actorId": "radiant_complete_actor",
              "displayName": "Смотритель светлого архива",
              "realm": "Shining Abode",
              "currencies": { "inkFeathers": 0, "lightSparks": 2 },
              "progression": {
                "enlightenment": { "experience": 0, "tier": 0 },
                "radiance": { "experience": 20, "tier": 1 }
              },
              "standardArts": { "guard": 1 },
              "specialArts": [],
              "customStates": [],
              "fateCards": [],
              "relationships": [],
              "goals": {
                "goalId": "goal_radiant_complete_actor",
                "shortTermGoal": "Сохранить светлый архив.",
                "longTermGoal": "Удержать непрерывность памяти Обители.",
                "plan": "Сначала укрепить защиту архива, затем проверить его записи.",
                "gmThoughtsSummary": "Я должен сохранить архив и его память.",
                "updatedAtTurn": 8
              },
              "personalQuests": [],
              "currentActivity": null,
              "completedActivities": [],
              "soulDissipationTier": 0,
              "progressionStrategy": {
                "strategyId": "strategy_radiant_complete_actor",
                "summary": "Сначала удерживает защиту архива.",
                "priorityOrder": ["guard"]
              },
              "ledger": [],
              "progressionLedger": [],
              "materialization": {
                "schemaVersion": 1,
                "materializationId": "mat_radiant_complete_actor_turn_8",
                "actorType": "radiant_actor",
                "actorId": "radiant_complete_actor",
                "materializedAtTurn": 8,
                "state": "complete",
                "capabilities": {
                  "canFight": true,
                  "canTeach": false,
                  "canTrade": false
                },
                "sections": {
                  "standardArts": { "state": "populated" },
                  "specialArts": { "state": "empty_by_design", "reason": "Личное искусство ещё не создано." },
                  "customStates": { "state": "empty_by_design", "reason": "Особых состояний сейчас нет." },
                  "fateCards": { "state": "empty_by_design", "reason": "Карта судьбы ещё не открыта." },
                  "relationships": { "state": "empty_by_design", "reason": "Устойчивые связи ещё не сложились." },
                  "agency": { "state": "populated" },
                  "progressionHistory": { "state": "empty_by_design", "reason": "История развития ещё не началась." }
                }
              }
            }
          ]
        }
        """);

        var issues = _validator.ValidateResponse(document.RootElement);

        Assert.Empty(issues);
    }

    [Theory]
    [InlineData("mortal")]
    [InlineData("afterlife")]
    public async Task ValidateGameStateAsync_DuplicateMaterializationIdWithinActorState_IsRejected(
        string family)
    {
        var path = family == "mortal"
            ? "game_state/npcs/npc_core.json"
            : AfterlifeEntityProfileState.StatePath;
        var currentRoot = JsonNode.Parse(BuildHistoricalEnvelopeStateJson(family, mutation: null))!.AsObject();
        var actorArray = family == "mortal"
            ? currentRoot["NPCsInScene"]!.AsArray()
            : currentRoot[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray();
        var duplicate = actorArray[0]!.DeepClone().AsObject();
        var duplicateActorId = family == "mortal"
            ? "historical_mortal_actor_duplicate"
            : "historical_afterlife_actor_duplicate";
        if (family == "mortal")
        {
            duplicate["NPCId"] = duplicateActorId;
        }
        else
        {
            duplicate["actorId"] = duplicateActorId;
        }
        duplicate[ActorMaterializationContract.PropertyName]!["actorId"] = duplicateActorId;
        actorArray.Add(duplicate);
        var preTurnJson = family == "mortal"
            ? """{ "UpdateNPCs": [], "NPCsInScene": [] }"""
            : """{ "schemaVersion": 1, "profiles": [] }""";
        await WriteCurrentAndValidatedPreTurnAsync(path, currentRoot.ToJsonString(), preTurnJson);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_duplicate_id" &&
            issue.Actual == (family == "mortal" ? "mat_mortal_historical" : "mat_afterlife_historical"));
    }

    [Theory]
    [InlineData("mortal")]
    [InlineData("afterlife")]
    public async Task ValidateGameStateAsync_DuplicateMaterializationIdWithoutPreTurnAuthority_IsRejected(
        string family)
    {
        var path = family == "mortal"
            ? "game_state/npcs/npc_core.json"
            : AfterlifeEntityProfileState.StatePath;
        var currentRoot = JsonNode.Parse(BuildHistoricalEnvelopeStateJson(family, mutation: null))!.AsObject();
        var actorArray = family == "mortal"
            ? currentRoot["NPCsInScene"]!.AsArray()
            : currentRoot[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray();
        var duplicate = actorArray[0]!.DeepClone().AsObject();
        var duplicateActorId = family == "mortal"
            ? "historical_mortal_actor_without_authority_duplicate"
            : "historical_afterlife_actor_without_authority_duplicate";
        if (family == "mortal")
            duplicate["NPCId"] = duplicateActorId;
        else
            duplicate["actorId"] = duplicateActorId;
        duplicate[ActorMaterializationContract.PropertyName]!["actorId"] = duplicateActorId;
        actorArray.Add(duplicate);
        await _fs.WriteFileAtomicAsync(path, currentRoot.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_duplicate_id" &&
            issue.Actual == (family == "mortal" ? "mat_mortal_historical" : "mat_afterlife_historical"));
    }

    [Fact]
    public async Task ValidateAcceptedTurnContinuity_SameTurnMortalActorsWithDifferentInitialIdsAndDuplicateMaterializationId_IsRejected()
    {
        const string path = "game_state/npcs/npc_core.json";
        var currentRoot = JsonNode.Parse(BuildMortalActorStateJson(
            "same_turn_seed",
            sectionName: "NPCsInScene",
            canTeach: true,
            includeEnvelope: true))!.AsObject();
        var firstActor = currentRoot["NPCsInScene"]![0]!.DeepClone().AsObject();
        ConfigureSameTurnMortalActor(firstActor, "same_turn_actor_alpha", "mat_same_turn_duplicate");
        var secondActor = firstActor.DeepClone().AsObject();
        ConfigureSameTurnMortalActor(secondActor, "same_turn_actor_beta", "mat_same_turn_duplicate");
        currentRoot["NPCsInScene"] = new JsonArray(firstActor, secondActor);
        const string preTurnJson = """{ "UpdateNPCs": [], "NPCsInScene": [] }""";
        await WriteCurrentAndValidatedPreTurnAsync(path, currentRoot.ToJsonString(), preTurnJson);

        var issues = await InvokeAcceptedTurnContinuityAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_duplicate_id" &&
            issue.Actor == "mortal_npc:same_turn_actor_beta" &&
            issue.Actual == "mat_same_turn_duplicate");
    }

    private static string BuildLossyMortalActorAuthoritySnapshotJson(
        string actorId,
        string lossyMutation) => lossyMutation switch
    {
        "duplicate_npc_id" => $$"""
        {
          "UpdateNPCs": [
            { "NPCId": "{{actorId}}", "NPCId": "{{actorId}}" }
          ],
          "NPCsInScene": []
        }
        """,
        "conflicting_identity_aliases" => $$"""
        {
          "UpdateNPCs": [
            { "NPCId": "different_snapshot_actor", "id": "{{actorId}}" }
          ],
          "NPCsInScene": []
        }
        """,
        "duplicate_materialization" => $$"""
        {
          "UpdateNPCs": [
            {
              "NPCId": "{{actorId}}",
              "materialization": { "schemaVersion": 1 },
              "materialization": null
            }
          ],
          "NPCsInScene": []
        }
        """,
        "duplicate_teacher_profile" => $$"""
        {
          "UpdateNPCs": [
            {
              "NPCId": "{{actorId}}",
              "teacherProfile": {
                "canTeach": true,
                "skills": [{ "skillId": "seal_reading", "skillName": "Чтение печатей", "masteryLevel": 1 }]
              },
              "teacherProfile": { "canTeach": false, "skills": [] }
            }
          ],
          "NPCsInScene": []
        }
        """,
        "duplicate_nested_can_teach" => $$"""
        {
          "UpdateNPCs": [
            {
              "NPCId": "{{actorId}}",
              "teacherProfile": {
                "canTeach": true,
                "canTeach": false,
                "skills": [{ "skillId": "seal_reading", "skillName": "Чтение печатей", "masteryLevel": 1 }]
              }
            }
          ],
          "NPCsInScene": []
        }
        """,
        "case_variant_nested_can_teach" => $$"""
        {
          "UpdateNPCs": [
            {
              "NPCId": "{{actorId}}",
              "teacherProfile": {
                "canTeach": false,
                "CanTeach": true,
                "skills": []
              }
            }
          ],
          "NPCsInScene": []
        }
        """,
        _ => throw new ArgumentOutOfRangeException(nameof(lossyMutation), lossyMutation, null)
    };

    private static string BuildMortalActorStateJson(
        string actorId,
        string sectionName,
        bool canTeach,
        bool includeEnvelope,
        bool includeInventory = false)
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 8,
            characterDescription: "Архивист новой жизни.",
            worldDescription: "Городской архив с практическим наставником.",
            startingCircumstances: "Наставник готов обучить чтению печатей.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-19T00:00:00Z"));
        var sourceRoot = files["game_state/npcs/npc_core.json"].AsObject();
        var actor = sourceRoot["NPCsInScene"]![0]!.DeepClone().AsObject();
        actor["NPCId"] = actorId;
        actor.Remove("npcId");
        actor.Remove("id");
        if (!canTeach)
        {
            actor["teacherProfile"] = new JsonObject
            {
                ["canTeach"] = false,
                ["skills"] = new JsonArray()
            };
        }

        if (includeInventory)
        {
            var inventoryItem = files["game_state/inventory/items.json"]["items"]![0]!.DeepClone().AsObject();
            var inventoryItemId = $"item_{actorId}_archive_key";
            inventoryItem["itemId"] = inventoryItemId;
            inventoryItem["existedId"] = inventoryItemId;
            actor["inventory"] = new JsonArray(inventoryItem);
        }

        if (includeEnvelope)
            actor["materialization"] = BuildCompleteMortalEnvelope(actorId, canTeach, includeInventory);
        else
            actor.Remove("materialization");

        var root = new JsonObject
        {
            ["UpdateNPCs"] = new JsonArray(),
            ["NPCsInScene"] = new JsonArray()
        };
        root[sectionName]!.AsArray().Add(actor);
        return root.ToJsonString();
    }

    private static void ConfigureSameTurnMortalActor(
        JsonObject actor,
        string initialId,
        string materializationId)
    {
        actor["NPCId"] = null;
        actor.Remove("npcId");
        actor.Remove("id");
        actor["initialId"] = initialId;
        actor["materialization"]!["actorId"] = initialId;
        actor["materialization"]!["materializationId"] = materializationId;
    }

    private static JsonObject BuildCompleteMortalEnvelope(
        string actorId,
        bool canTeach,
        bool ownsItems = false) =>
        new()
        {
            ["schemaVersion"] = 1,
            ["materializationId"] = $"mat_{actorId}_turn_8",
            ["actorType"] = "mortal_npc",
            ["actorId"] = actorId,
            ["materializedAtTurn"] = 8,
            ["state"] = "complete",
            ["capabilities"] = new JsonObject
            {
                ["canFight"] = false,
                ["canTeach"] = canTeach,
                ["canTrade"] = false,
                ["ownsItems"] = ownsItems
            },
            ["sections"] = new JsonObject
            {
                ["skills"] = EmptyMortalSection("Этот NPC не использует боевые навыки."),
                ["inventory"] = ownsItems
                    ? new JsonObject { ["state"] = "populated" }
                    : EmptyMortalSection("Этот NPC не носит личных предметов."),
                ["fateCards"] = EmptyMortalSection("Карта судьбы ещё не открыта."),
                ["personalQuests"] = EmptyMortalSection("Личная просьба пока не сформировалась."),
                ["relationships"] = new JsonObject { ["state"] = "populated" }
            }
        };

    private static JsonObject EmptyMortalSection(string reason) =>
        new()
        {
            ["state"] = "empty_by_design",
            ["reason"] = reason
        };

    private static string BuildMortalActorAuthoritySnapshotWithPayload(JsonNode payload) =>
        new JsonObject
        {
            ["UpdateNPCs"] = new JsonArray
            {
                new JsonObject
                {
                    ["NPCId"] = "authority_budget_actor",
                    ["payload"] = payload
                }
            },
            ["NPCsInScene"] = new JsonArray()
        }.ToJsonString();

    private static bool CanReadCanonicalMortalActorSnapshotAuthority(string snapshotJson)
    {
        var method = typeof(ValidationService).GetMethod(
            "TryReadCanonicalMortalActorStates",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        using var document = JsonDocument.Parse(
            snapshotJson,
            new JsonDocumentOptions { MaxDepth = 256 });
        var arguments = new object?[] { document.RootElement, null };
        return Assert.IsType<bool>(method.Invoke(null, arguments));
    }

    private static string BuildHistoricalEnvelopeStateJson(string family, string? mutation)
    {
        JsonObject actor;
        JsonObject root;
        if (family == "mortal")
        {
            actor = new JsonObject
            {
                ["NPCId"] = "historical_mortal_actor",
                ["name"] = "Хранитель архива",
                ["currentLocationId"] = "loc_historical_archive",
                ["relationshipLevel"] = 0,
                ["attitude"] = "Нейтралитет",
                ["goals"] = new JsonObject { ["shortTerm"] = "Сохранить архив." },
                ["teacherProfile"] = new JsonObject
                {
                    ["canTeach"] = false,
                    ["skills"] = new JsonArray()
                },
                ["tradeState"] = new JsonObject { ["canTrade"] = false },
                ["activeSkills"] = new JsonArray(),
                ["passiveSkills"] = new JsonArray(),
                ["inventory"] = new JsonArray(),
                ["fateCards"] = new JsonArray(),
                ["personalQuests"] = new JsonArray()
            };
            root = new JsonObject
            {
                ["UpdateNPCs"] = new JsonArray(),
                ["NPCsInScene"] = new JsonArray(actor)
            };
        }
        else
        {
            actor = JsonNode.Parse(BuildAfterlifeProfileStateJson(
                includeProfile: true,
                actorId: "historical_afterlife_actor"))!["profiles"]![0]!.AsObject();
            actor = actor.DeepClone().AsObject();
            root = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["profiles"] = new JsonArray(actor)
            };
        }

        actor["materialization"] = BuildHistoricalEnvelope(family);
        if (mutation == "removed")
        {
            actor.Remove("materialization");
        }
        else if (mutation == "changed")
        {
            actor["materialization"]!["materializationId"] = $"mat_{family}_changed";
        }
        else if (mutation == "reordered")
        {
            var envelope = actor["materialization"]!.AsObject();
            var reordered = new JsonObject();
            foreach (var property in envelope.Reverse())
                reordered[property.Key] = property.Value?.DeepClone();
            actor["materialization"] = reordered;
        }

        return root.ToJsonString();
    }

    private static JsonObject BuildHistoricalEnvelope(string family)
    {
        var isMortal = family == "mortal";
        var capabilities = new JsonObject
        {
            ["canFight"] = false,
            ["canTeach"] = false,
            ["canTrade"] = false
        };
        if (isMortal)
            capabilities["ownsItems"] = false;

        var sectionNames = isMortal
            ? new[] { "skills", "inventory", "fateCards", "personalQuests", "relationships" }
            : new[] { "standardArts", "specialArts", "customStates", "fateCards", "relationships", "agency", "progressionHistory" };
        var sections = new JsonObject();
        foreach (var sectionName in sectionNames)
        {
            sections[sectionName] = new JsonObject
            {
                ["state"] = "empty_by_design",
                ["reason"] = "Раздел пока намеренно пуст."
            };
        }

        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["materializationId"] = isMortal ? "mat_mortal_historical" : "mat_afterlife_historical",
            ["actorType"] = isMortal ? "mortal_npc" : "radiant_actor",
            ["actorId"] = isMortal ? "historical_mortal_actor" : "historical_afterlife_actor",
            ["materializedAtTurn"] = 7,
            ["state"] = "complete",
            ["capabilities"] = capabilities,
            ["sections"] = sections
        };
    }

    private async Task WriteCurrentAndValidatedPreTurnAsync(string path, string currentJson, string preTurnJson)
    {
        await _fs.WriteFileAtomicAsync(path, currentJson);
        await _fs.WriteFileAtomicAsync($"game_state/control/pending_turn_snapshot/{path}", preTurnJson);
        await WriteValidatedSnapshotManifestAsync((path, preTurnJson));
    }

    private async Task WriteValidatedSnapshotManifestAsync(params (string Path, string Json)[] snapshotFiles)
    {
        const string sessionId = "session_actor_materialization_tests";
        const string requestId = "request_actor_materialization_tests";
        const int turnNumber = 42;
        const string playerAction = "Actor materialization continuity validation test.";

        await _fs.WriteFileAtomicAsync("input/turn_request.json", $$"""
        {
          "sessionId": "{{sessionId}}",
          "requestId": "{{requestId}}",
          "turnNumber": {{turnNumber}},
          "playerAction": {{JsonSerializer.Serialize(playerAction)}}
        }
        """);

        var files = new JsonObject();
        var snapshotFileHashes = new JsonObject();
        var rollbackBaselineFiles = new JsonArray();
        foreach (var (path, json) in snapshotFiles)
        {
            files[path] = $"game_state/control/pending_turn_snapshot/{path}";
            snapshotFileHashes[path] = PendingTurnSnapshotAuthority.ComputeSha256(json);
            rollbackBaselineFiles.Add(path);
        }

        var manifest = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turnNumber,
            ["requestTimestamp"] = "2026-07-19T00:00:00Z",
            ["playerAction"] = playerAction,
            ["files"] = files,
            ["snapshotFileHashes"] = snapshotFileHashes,
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = rollbackBaselineFiles,
            ["sourceLabel"] = "actor materialization continuity test",
            ["manifestPayloadHash"] = string.Empty
        };
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);

        await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.json", manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private async Task RemovePendingTurnBaselineRegistrationAsync(string path)
    {
        const string manifestPath = "game_state/control/pending_turn_snapshot.json";
        var manifest = JsonNode.Parse((await _fs.ReadFileAsync(manifestPath))!)!.AsObject();
        var baselineFiles = manifest["rollbackBaselineFiles"]!.AsArray();
        var entry = baselineFiles.FirstOrDefault(node =>
            string.Equals(node?.GetValue<string>(), path, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(entry);
        baselineFiles.Remove(entry);
        manifest["manifestPayloadHash"] = string.Empty;
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
        await _fs.WriteFileAtomicAsync(manifestPath, manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private async Task<List<ValidationIssue>> InvokeAcceptedTurnContinuityAsync()
    {
        var method = typeof(ValidationService).GetMethod(
            "ValidateAcceptedTurnActorMaterializationCompletenessAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var issues = new List<ValidationIssue>();
        await Assert.IsAssignableFrom<Task>(method.Invoke(_validator, new object[] { issues }));
        return issues;
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
            // Best-effort test cleanup.
        }
    }
}
