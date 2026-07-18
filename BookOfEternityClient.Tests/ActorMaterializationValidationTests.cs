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
    public async Task ValidateGameStateAsync_MissingValidatedPreTurnFileAuthority_SkipsOnlyContinuityValidation(
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

    [Fact]
    public void ValidateResponse_AfterlifeMaterializationWithAuthoritativeFalseTradeEvidence_ReportsMismatchThroughProductionCaller()
    {
        var stateRoot = JsonNode.Parse(BuildHistoricalEnvelopeStateJson("afterlife", mutation: null))!.AsObject();
        var profile = stateRoot[AfterlifeEntityProfileState.ProfilesProperty]![0]!.DeepClone().AsObject();
        profile["materialization"]!["capabilities"]!["canTrade"] = true;
        var responseRoot = new JsonObject
        {
            [AfterlifeEntityProfileState.ResponseProfilesProperty] = new JsonArray(profile)
        };
        using var document = JsonDocument.Parse(responseRoot.ToJsonString());

        var issues = _validator.ValidateResponse(document.RootElement);

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_capability_mismatch" &&
            issue.Actor == "radiant_actor:historical_afterlife_actor" &&
            issue.Section == "canTrade" &&
            issue.Expected == bool.FalseString &&
            issue.Actual == bool.TrueString);
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
