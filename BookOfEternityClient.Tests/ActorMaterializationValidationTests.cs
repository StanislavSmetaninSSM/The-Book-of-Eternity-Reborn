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
                actor["teacherProfile"]!["skills"]!.AsArray().Add(new JsonObject { ["skillId"] = "skill_signal" });
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
