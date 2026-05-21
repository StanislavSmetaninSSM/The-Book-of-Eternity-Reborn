using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SarefMainStoryStateValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public SarefMainStoryStateValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-saref-main-story-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateGameStateAsync_MissingSarefState_AllowsLegacySave()
    {
        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("saref_main_story_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_DefaultSarefState_PassesContractValidation()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, SarefMainStoryState.SerializeDefaultRoot());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("saref_main_story_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_SarefWingsPendingInChaosSea_ReportsContextIssue()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1
        }
        """);
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefWingsRouteState());
        await _fs.WriteFileAtomicAsync("game_state/control/pending_saref_wings_infiltration.json", BuildValidSarefWingsPendingRequest());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_wings_pending_wrong_realm", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SarefWingsAcceptedTurnWithoutClosure_ReportsMissingClosure()
    {
        await SeedShiningWingsPendingAcceptedTurnAsync(BuildSarefWingsRouteState());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_wings_pending_missing_closure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SarefWingsAcceptedTurnWithRevealClosure_PassesPendingClosureValidation()
    {
        var storyRoot = SarefMainStoryState.ApplyUpdate(
            JsonNode.Parse(BuildSarefWingsRouteState())!.AsObject(),
            JsonNode.Parse("""
            {
              "mode": "reveal_wings",
              "requestId": "saref_wings_infiltration:42",
              "resolvedAtTurn": 43,
              "routeSafety": "safe",
              "entryMode": "safe_infiltration",
              "summary": "Игрок нашел внешний круг Крыльев Ангелов.",
              "factionLinks": {
                "wingsFactionId": "wings_of_angels"
              }
            }
            """)!.AsObject());

        await SeedShiningWingsPendingAcceptedTurnAsync(storyRoot.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "saref_wings_pending_missing_closure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_WingsRevealedWithoutFactionId_ReportsActionableFactionIssue()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefWingsRouteState("""
          "factionLinks": {
            "visibility": "revealed"
          }
        """, revealStage: "wings_revealed"));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_wings_revealed_missing_faction_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_WingsRevealedFactionIdMissingFromShiningFactions_ReportsIssue()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefWingsRouteState("""
          "factionLinks": {
            "visibility": "revealed",
            "wingsFactionId": "wings_of_angels"
          }
        """, revealStage: "wings_revealed"));
        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, """
        {
          "schemaVersion": 1,
          "availability": "active",
          "radiance": {
            "experience": 0,
            "tier": 0
          },
          "preparedIncarnationPackage": null,
          "factions": []
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_wings_faction_missing_shining_actor", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_WingsKnownAgentsSingleArchetype_ReportsIssue()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefWingsRouteState("""
          "factionLinks": {
            "visibility": "hidden",
            "knownAgents": [
              {
                "agentId": "wing_agent_001",
                "supporterArchetype": "fanatic",
                "summary": "Слепо предан Сарефу."
              },
              {
                "agentId": "wing_agent_002",
                "supporterArchetype": "fanatic",
                "summary": "Слепо предан Сарефу."
              }
            ]
          }
        """));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_wings_agents_need_mixed_archetypes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_WingsKnownAgentsMixedArchetypes_PassesAgentValidation()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefWingsRouteState("""
          "factionLinks": {
            "visibility": "hidden",
            "knownAgents": [
              {
                "agentId": "wing_agent_001",
                "supporterArchetype": "deceived",
                "interactionRoutes": [ "persuade", "expose" ],
                "summary": "Верит, что Крылья спасут Обитель."
              },
              {
                "agentId": "wing_agent_002",
                "supporterArchetype": "oathbound",
                "importance": "important",
                "interactionRoutes": [ "free", "defeat" ],
                "summary": "Связан клятвой Сарефа."
              }
            ],
            "shadowTraces": [
              {
                "traceId": "white_dead_feather",
                "stage": "shadow",
                "summary": "Белое мертвое перо в Море Хаоса."
              }
            ]
          }
        """));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("saref_main_story_wings_agent", StringComparison.OrdinalIgnoreCase) == true ||
            issue.Code?.StartsWith("saref_main_story_wings_shadow_trace", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void GetPlayerVisibleShiningFactions_HidesUnrevealedWingsFaction()
    {
        var shiningRoot = JsonNode.Parse("""
        {
          "factions": [
            {
              "factionId": "wings_of_angels",
              "sarefFactionRole": "wings_of_angels",
              "sarefVisibility": "hidden",
              "charter": { "factionName": "Крылья Ангелов" },
              "factionStrength": 99
            },
            {
              "factionId": "radiant_accord",
              "charter": { "factionName": "Сияющий Договор" },
              "factionStrength": 20
            }
          ]
        }
        """)!.AsObject();

        var visibleFactionIds = SarefMainStoryState.GetPlayerVisibleShiningFactions(shiningRoot)
            .Select(faction => SarefMainStoryState.GetNodeString(faction["factionId"]))
            .ToList();

        Assert.Equal(["radiant_accord"], visibleFactionIds);
    }

    [Fact]
    public async Task ValidateGameStateAsync_InvalidSarefState_ReportsShapeAndDuplicateIssues()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, """
        {
          "schemaVersion": 1,
          "revealStage": "wings_known",
          "guardianQuestlines": [
            { "guardianId": "azalia", "questStage": 4 },
            { "guardianId": "azalia", "questStage": 3 }
          ],
          "latentTraces": "broken",
          "sarefRevelations": [
            { "revelationId": "rev_1", "category": "unknown_category", "sourceGuardianId": "azalia", "revealedAtTurn": -1 },
            { "revelationId": "rev_1", "category": "identity", "sourceGuardianId": "myriel", "revealedAtTurn": 7 }
          ],
          "sarefAdvantages": [
            { "advantageId": "adv_1", "state": "ready" },
            { "advantageId": "adv_1", "state": "available" }
          ],
          "factionLinks": { "visibility": "visible" },
          "defeatOutcomes": [],
          "endings": [],
          "playerOathState": null,
          "sarefPersonalBond": { "state": "obsessed" }
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "saref_main_story_invalid_reveal_stage", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "saref_main_story_duplicate_guardian_questline", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "saref_main_story_array_not_array", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "saref_main_story_invalid_revelation_category", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "saref_main_story_duplicate_revelation", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "saref_main_story_invalid_advantage_state", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "saref_main_story_duplicate_advantage", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "saref_main_story_invalid_faction_visibility", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "saref_main_story_invalid_personal_bond_state", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "saref_main_story_negative_turn", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_UnknownStageWithCanonicalRevelation_ReportsSpoilerInvariant()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, """
        {
          "schemaVersion": 1,
          "revealStage": "unknown",
          "guardianQuestlines": [],
          "latentTraces": [],
          "sarefRevelations": [
            { "revelationId": "rev_identity", "category": "identity", "sourceGuardianId": "myriel", "revealedAtTurn": 11 }
          ],
          "sarefAdvantages": [],
          "factionLinks": { "visibility": "hidden" },
          "defeatOutcomes": [],
          "endings": [],
          "playerOathState": null,
          "sarefPersonalBond": null
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_no_spoiler_stage_has_revealed_content", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_WingsStageWithoutUnlockRoute_ReportsEvidenceIssue()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, """
        {
          "schemaVersion": 1,
          "revealStage": "wings_revealed",
          "guardianQuestlines": [],
          "latentTraces": [],
          "sarefRevelations": [
            { "revelationId": "rev_identity", "category": "identity", "sourceGuardianId": "myriel", "revealedAtTurn": 11 }
          ],
          "sarefAdvantages": [],
          "factionLinks": { "visibility": "hidden" },
          "defeatOutcomes": [],
          "endings": [],
          "playerOathState": null,
          "sarefPersonalBond": null
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_wings_stage_without_unlock_route", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_LatentFutureSarefQuestProgressWithoutSpoilers_Passes()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, """
        {
          "schemaVersion": 1,
          "revealStage": "shadow",
          "guardianQuestlines": [
            {
              "guardianId": "azalia",
              "questStates": [
                {
                  "questOrdinal": 1,
                  "status": "latent",
                  "questId": "azalia_saref_q1",
                  "evidence": {
                    "itemEcho": {
                      "summary": "A silk memory image found in a mortal life."
                    }
                  }
                },
                {
                  "questOrdinal": 4,
                  "status": "recognized",
                  "questId": "azalia_saref_q4",
                  "evidence": {
                    "memoryImprint": {
                      "summary": "The soul saw a crown of wing-shadows but does not understand it yet."
                    }
                  }
                }
              ]
            }
          ],
          "latentTraces": [
            {
              "traceId": "latent_azalia_q4_crown",
              "guardianId": "azalia",
              "questOrdinal": 4,
              "status": "latent",
              "evidence": {
                "knowledgeTrace": {
                  "summary": "Unlabeled afterlife trace."
                }
              }
            }
          ],
          "sarefRevelations": [],
          "sarefAdvantages": [],
          "factionLinks": { "visibility": "hidden" },
          "defeatOutcomes": [],
          "endings": [],
          "playerOathState": null,
          "sarefPersonalBond": null
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("saref_main_story_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_QuestFourCompletedBeforeEarlierQuests_ReportsOrderIssue()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, """
        {
          "schemaVersion": 1,
          "revealStage": "name_revealed",
          "guardianQuestlines": [
            {
              "guardianId": "azalia",
              "questStates": [
                { "questOrdinal": 1, "status": "completed", "questId": "azalia_saref_q1" },
                { "questOrdinal": 2, "status": "active", "questId": "azalia_saref_q2" },
                { "questOrdinal": 4, "status": "completed", "questId": "azalia_saref_q4" }
              ]
            }
          ],
          "latentTraces": [],
          "sarefRevelations": [
            {
              "revelationId": "rev_azalia_faction",
              "category": "faction",
              "sourceGuardianId": "azalia",
              "sourceQuestId": "azalia_saref_q4",
              "sourceQuestOrdinal": 4,
              "revealedAtTurn": 44
            }
          ],
          "sarefAdvantages": [
            {
              "advantageId": "adv_azalia_false_loyalty",
              "sourceGuardianId": "azalia",
              "sourceQuestId": "azalia_saref_q4",
              "sourceQuestOrdinal": 4,
              "state": "available",
              "unlockedAtTurn": 44
            }
          ],
          "factionLinks": { "visibility": "hidden" },
          "defeatOutcomes": [],
          "endings": [],
          "playerOathState": null,
          "sarefPersonalBond": null
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_questline_out_of_order", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_QuestFourRevelationWithoutCompletedPrerequisites_ReportsIssue()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, """
        {
          "schemaVersion": 1,
          "revealStage": "name_revealed",
          "guardianQuestlines": [
            {
              "guardianId": "myriel",
              "questStates": [
                { "questOrdinal": 1, "status": "completed", "questId": "myriel_saref_q1" },
                { "questOrdinal": 2, "status": "completed", "questId": "myriel_saref_q2" },
                { "questOrdinal": 3, "status": "ready_to_turn_in", "questId": "myriel_saref_q3" },
                { "questOrdinal": 4, "status": "ready_to_turn_in", "questId": "myriel_saref_q4" }
              ]
            }
          ],
          "latentTraces": [],
          "sarefRevelations": [
            {
              "revelationId": "rev_myriel_identity",
              "category": "identity",
              "sourceGuardianId": "myriel",
              "sourceQuestId": "myriel_saref_q4",
              "sourceQuestOrdinal": 4,
              "revealedAtTurn": 52
            }
          ],
          "sarefAdvantages": [
            {
              "advantageId": "adv_myriel_ash_formula",
              "sourceGuardianId": "myriel",
              "sourceQuestId": "myriel_saref_q4",
              "sourceQuestOrdinal": 4,
              "state": "available",
              "unlockedAtTurn": 52
            }
          ],
          "factionLinks": { "visibility": "hidden" },
          "defeatOutcomes": [],
          "endings": [],
          "playerOathState": null,
          "sarefPersonalBond": null
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_revelation_without_questline_completion", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MalformedMemoryScene_ReportsRequiredShapeIssues()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, """
        {
          "schemaVersion": 1,
          "revealStage": "shadow",
          "guardianQuestlines": [],
          "latentTraces": [],
          "sarefRevelations": [],
          "sarefAdvantages": [],
          "sarefAdvantageUses": [],
          "memoryScene": {
            "sceneId": "memory_scene_azalia_q4",
            "status": "active",
            "guardianId": "azalia",
            "questId": "azalia_saref_q4",
            "questOrdinal": 4
          },
          "factionLinks": { "visibility": "hidden" },
          "defeatOutcomes": [],
          "endings": [],
          "playerOathState": null,
          "sarefPersonalBond": null
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "saref_main_story_memory_scene_missing_role", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "saref_main_story_memory_scene_missing_boundaries", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "saref_main_story_memory_scene_invalid_ability_count", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "saref_main_story_memory_scene_missing_required_nodes", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "saref_main_story_memory_scene_missing_success_condition", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "saref_main_story_memory_scene_missing_closure_target", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_QuestFourCompletedWithoutMemorySceneProof_ReportsIssue()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefQuestFourCompletedState(includeMemoryProof: false));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_quest_four_missing_memory_scene_proof", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_QuestFourCompletedWithMemorySceneProof_PassesMemorySceneGate()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefQuestFourCompletedState(includeMemoryProof: true));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_quest_four_missing_memory_scene_proof", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ApplyUpdate_RecordMemoryScene_MergesSceneQuestRevelationAndAdvantage()
    {
        var baseline = JsonNode.Parse("""
        {
          "schemaVersion": 1,
          "revealStage": "shadow",
          "guardianQuestlines": [
            {
              "guardianId": "azalia",
              "questStates": [
                { "questOrdinal": 1, "status": "completed", "questId": "azalia_saref_q1" },
                { "questOrdinal": 2, "status": "completed", "questId": "azalia_saref_q2" },
                { "questOrdinal": 3, "status": "completed", "questId": "azalia_saref_q3" }
              ]
            }
          ],
          "latentTraces": [],
          "sarefRevelations": [],
          "sarefAdvantages": [],
          "sarefAdvantageUses": [],
          "memoryScene": null,
          "factionLinks": { "visibility": "hidden" },
          "defeatOutcomes": [],
          "endings": [],
          "playerOathState": null,
          "sarefPersonalBond": null
        }
        """)!.AsObject();

        var update = JsonNode.Parse($$"""
        {
          "mode": "record_memory_scene",
          "memoryScene": {{BuildValidMemoryScenePayload()}},
          "guardianQuestline": {
            "guardianId": "azalia",
            "questStates": [
              {
                "questOrdinal": 4,
                "status": "completed",
                "questId": "azalia_saref_q4",
                "completedAtTurn": 44,
                "memorySceneProof": {{BuildValidMemorySceneProofPayload()}}
              }
            ]
          },
          "sarefRevelation": {
            "revelationId": "rev_azalia_faction",
            "category": "faction",
            "sourceGuardianId": "azalia",
            "sourceQuestId": "azalia_saref_q4",
            "sourceQuestOrdinal": 4,
            "revealedAtTurn": 44
          },
          "sarefAdvantage": {
            "advantageId": "adv_azalia_false_loyalty",
            "sourceGuardianId": "azalia",
            "sourceQuestId": "azalia_saref_q4",
            "sourceQuestOrdinal": 4,
            "state": "available",
            "applicableScenes": [ "wings_infiltration" ],
            "summary": "Можно выдать себя за полезного перебежчика.",
            "unlockedAtTurn": 44
          }
        }
        """)!.AsObject();

        var root = SarefMainStoryState.ApplyUpdate(baseline, update);

        Assert.Equal("completed", root["memoryScene"]!["status"]!.GetValue<string>());
        var questline = Assert.IsType<JsonObject>(Assert.Single(root["guardianQuestlines"]!.AsArray()));
        var questFour = questline["questStates"]!.AsArray()
            .OfType<JsonObject>()
            .Single(state => state["questOrdinal"]!.GetValue<int>() == 4);
        Assert.Equal("completed", questFour["status"]!.GetValue<string>());
        Assert.Equal("memory_scene_azalia_q4", questFour["memorySceneProof"]!["sceneId"]!.GetValue<string>());
        Assert.Equal("rev_azalia_faction", Assert.IsType<JsonObject>(Assert.Single(root["sarefRevelations"]!.AsArray()))["revelationId"]!.GetValue<string>());
        Assert.Equal("adv_azalia_false_loyalty", Assert.IsType<JsonObject>(Assert.Single(root["sarefAdvantages"]!.AsArray()))["advantageId"]!.GetValue<string>());
    }

    [Fact]
    public async Task ValidateGameStateAsync_PhysicalMortalItemInSarefQuestEvidence_ReportsIssue()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, """
        {
          "schemaVersion": 1,
          "revealStage": "shadow",
          "guardianQuestlines": [
            {
              "guardianId": "azalia",
              "questStates": [
                {
                  "questOrdinal": 1,
                  "status": "ready_to_turn_in",
                  "questId": "azalia_saref_q1",
                  "evidence": {
                    "itemEcho": {
                      "transferredItemId": "rare_ore_001"
                    }
                  }
                }
              ]
            }
          ],
          "latentTraces": [],
          "sarefRevelations": [],
          "sarefAdvantages": [],
          "factionLinks": { "visibility": "hidden" },
          "defeatOutcomes": [],
          "endings": [],
          "playerOathState": null,
          "sarefPersonalBond": null
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_physical_mortal_item_evidence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SpentSarefAdvantageWithMatchingUseAudit_Passes()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefAdvantageState("""
          "sarefAdvantages": [
            {
              "advantageId": "adv_azalia_false_loyalty",
              "sourceGuardianId": "azalia",
              "sourceQuestId": "azalia_saref_q4",
              "sourceQuestOrdinal": 4,
              "state": "spent",
              "applicableScenes": [ "wings_infiltration" ],
              "summary": "Можно выдать себя за полезного перебежчика перед агентами Крыльев Ангелов.",
              "spentAudit": {
                "usageId": "use_false_loyalty_001",
                "usedAtTurn": 77,
                "sceneType": "wings_infiltration",
                "summary": "Игрок использовал легенду ложной лояльности при входе в круг Крыльев Ангелов."
              }
            }
          ],
          "sarefAdvantageUses": [
            {
              "usageId": "use_false_loyalty_001",
              "advantageId": "adv_azalia_false_loyalty",
              "usedAtTurn": 77,
              "sceneType": "wings_infiltration",
              "consumesAdvantage": true,
              "summary": "Легенда ложной лояльности открыла доступ к закрытому собранию."
            }
          ]
        """));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("saref_main_story_advantage_", StringComparison.OrdinalIgnoreCase) == true ||
            issue.Code?.StartsWith("saref_main_story_spent_advantage", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_UnknownSarefAdvantageUse_ReportsIssue()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefAdvantageState("""
          "sarefAdvantages": [
            {
              "advantageId": "adv_azalia_false_loyalty",
              "sourceGuardianId": "azalia",
              "sourceQuestId": "azalia_saref_q4",
              "sourceQuestOrdinal": 4,
              "state": "available",
              "applicableScenes": [ "wings_infiltration" ]
            }
          ],
          "sarefAdvantageUses": [
            {
              "usageId": "use_unknown_001",
              "advantageId": "adv_missing",
              "usedAtTurn": 77,
              "sceneType": "wings_infiltration",
              "consumesAdvantage": true,
              "summary": "GM claimed an unknown advantage."
            }
          ]
        """));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_unknown_advantage_usage", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SuppressedSarefAdvantageUse_ReportsIssue()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefAdvantageState("""
          "sarefAdvantages": [
            {
              "advantageId": "adv_ilarion_memory_anchor",
              "sourceGuardianId": "azalia",
              "sourceQuestId": "azalia_saref_q4",
              "sourceQuestOrdinal": 4,
              "state": "suppressed",
              "applicableScenes": [ "memory_attack" ],
              "suppressionReason": "Сареф временно заглушил якорь памяти."
            }
          ],
          "sarefAdvantageUses": [
            {
              "usageId": "use_suppressed_001",
              "advantageId": "adv_ilarion_memory_anchor",
              "usedAtTurn": 80,
              "sceneType": "memory_attack",
              "consumesAdvantage": true,
              "summary": "GM attempted to use a suppressed advantage."
            }
          ]
        """));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_advantage_usage_unauthorized_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_InapplicableSarefAdvantageUse_ReportsIssue()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefAdvantageState("""
          "sarefAdvantages": [
            {
              "advantageId": "adv_veyra_wing_mask",
              "sourceGuardianId": "azalia",
              "sourceQuestId": "azalia_saref_q4",
              "sourceQuestOrdinal": 4,
              "state": "available",
              "applicableScenes": [ "wings_infiltration" ]
            }
          ],
          "sarefAdvantageUses": [
            {
              "usageId": "use_wrong_scene_001",
              "advantageId": "adv_veyra_wing_mask",
              "usedAtTurn": 82,
              "sceneType": "saref_confrontation",
              "consumesAdvantage": true,
              "summary": "GM claimed an infiltration mask as a final confrontation advantage."
            }
          ]
        """));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_advantage_usage_inapplicable_scene", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SpentSarefAdvantageWithoutAudit_ReportsIssue()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefAdvantageState("""
          "sarefAdvantages": [
            {
              "advantageId": "adv_myriel_ash_formula",
              "sourceGuardianId": "azalia",
              "sourceQuestId": "azalia_saref_q4",
              "sourceQuestOrdinal": 4,
              "state": "spent",
              "applicableScenes": [ "saref_confrontation" ]
            }
          ],
          "sarefAdvantageUses": []
        """));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_spent_advantage_missing_audit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ApplyUpdate_RecordDefeatOutcome_AppendsOutcomeAndOathState()
    {
        var root = SarefMainStoryState.ApplyUpdate(
            JsonNode.Parse(BuildSarefDefeatState("[]"))!.AsObject(),
            JsonNode.Parse("""
            {
              "mode": "record_defeat_outcome",
              "resolvedAtTurn": 91,
              "defeatOutcome": {
                "outcomeId": "saref_defeat_forced_oath_001",
                "outcomeType": "forced_oath",
                "sceneType": "saref_confrontation",
                "oathId": "saref_oath_001",
                "summary": "Сареф принудил душу к клятве после поражения.",
                "gmMotivation": "Сареф хочет не убить игрока, а связать его волю."
              },
              "playerOathState": {
                "state": "oathbound",
                "oathId": "saref_oath_001",
                "boundAtTurn": 91,
                "summary": "Игрок связан клятвой Сарефа."
              }
            }
            """)!.AsObject());

        var outcomes = Assert.IsType<JsonArray>(root["defeatOutcomes"]);
        var outcome = Assert.IsType<JsonObject>(Assert.Single(outcomes));
        Assert.Equal("saref_defeat_forced_oath_001", SarefMainStoryState.GetNodeString(outcome["outcomeId"]));
        Assert.Equal(91, SarefMainStoryState.GetNodeInt(outcome["resolvedAtTurn"]));
        Assert.Equal("oathbound", SarefMainStoryState.GetNodeString(root["playerOathState"]?["state"]));
    }

    [Fact]
    public async Task ValidateGameStateAsync_InvalidSarefDefeatOutcome_ReportsContractIssues()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefDefeatState("""
        [
          {
            "outcomeId": "saref_defeat_invalid_001",
            "outcomeType": "annihilate_everything",
            "resolvedAtTurn": 91,
            "sceneType": "saref_confrontation",
            "summary": "Invalid outcome.",
            "gmMotivation": ""
          }
        ]
        """));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_defeat_invalid_outcome_type", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_defeat_missing_motivation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ForcedOathDefeatWithoutOathState_ReportsIssue()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefDefeatState("""
        [
          {
            "outcomeId": "saref_defeat_forced_oath_001",
            "outcomeType": "forced_oath",
            "resolvedAtTurn": 91,
            "sceneType": "saref_confrontation",
            "oathId": "saref_oath_001",
            "summary": "Сареф принудил душу к клятве.",
            "gmMotivation": "Сареф хочет использовать игрока как связанную фигуру."
          }
        ]
        """));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_defeat_forced_oath_missing_oath_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SoulDissipationDefeatWithoutProofLink_ReportsIssue()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefDefeatState("""
        [
          {
            "outcomeId": "saref_defeat_soul_dissipation_001",
            "outcomeType": "soul_dissipation",
            "resolvedAtTurn": 91,
            "sceneType": "saref_confrontation",
            "summary": "Сареф пытается окончательно развеять душу игрока.",
            "gmMotivation": "Сареф считает игрока угрозой, которую нельзя оставить в Мироздании."
          }
        ]
        """));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_defeat_soul_dissipation_missing_proof", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PyrrhicEscapeWithUnknownMitigationAdvantage_ReportsIssue()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefDefeatState("""
        [
          {
            "outcomeId": "saref_defeat_pyrrhic_escape_001",
            "outcomeType": "pyrrhic_escape",
            "resolvedAtTurn": 91,
            "sceneType": "saref_confrontation",
            "escapeCost": "Игрок вырывается, но теряет маршрут и часть союзников.",
            "summary": "Преимущество превращает поражение в тяжёлое бегство.",
            "gmMotivation": "Сареф решает не преследовать мгновенно, потому что цена уже нанесена.",
            "mitigation": {
              "mitigatedByAdvantages": [ "use_missing_anchor" ],
              "mitigationSummary": "Память удерживает душу от полного подавления."
            }
          }
        ]
        """));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_defeat_unknown_mitigation_advantage", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PyrrhicEscapeWithKnownMitigationAdvantage_PassesDefeatValidation()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefDefeatState(
            """
            [
              {
                "outcomeId": "saref_defeat_pyrrhic_escape_001",
                "outcomeType": "pyrrhic_escape",
                "resolvedAtTurn": 91,
                "sceneType": "saref_confrontation",
                "escapeCost": "Игрок вырывается, но теряет маршрут и часть союзников.",
                "summary": "Преимущество превращает поражение в тяжёлое бегство.",
                "gmMotivation": "Сареф сохраняет угрозу, но позволяет бегству стать уроком.",
                "mitigation": {
                  "mitigatedByAdvantages": [ "use_memory_anchor_escape" ],
                  "mitigationSummary": "Якорь памяти удержал личность игрока в момент подавления."
                }
              }
            ]
            """,
            """
            "sarefAdvantages": [
              {
                "advantageId": "adv_ilarion_memory_anchor",
                "state": "passive",
                "applicableScenes": [ "saref_confrontation", "memory_attack" ],
                "summary": "Игрок может закрепить одну важную правду против подавления памяти."
              }
            ],
            "sarefAdvantageUses": [
              {
                "usageId": "use_memory_anchor_escape",
                "advantageId": "adv_ilarion_memory_anchor",
                "usedAtTurn": 91,
                "sceneType": "saref_confrontation",
                "consumesAdvantage": false,
                "summary": "Якорь памяти превратил поражение в бегство вместо стирания личности."
              }
            ],
            """));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("saref_main_story_defeat_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void ApplyUpdate_RecordFinalConfrontation_SetsCompletedStateAndEnding()
    {
        var root = SarefMainStoryState.ApplyUpdate(
            JsonNode.Parse(BuildSarefFinalConfrontationState("null"))!.AsObject(),
            JsonNode.Parse("""
            {
              "mode": "record_final_confrontation",
              "resolvedAtTurn": 120,
              "finalConfrontation": {
                "confrontationId": "saref_final_001",
                "status": "resolved",
                "routeType": "combat",
                "victoryTier": "clean",
                "directScene": true,
                "sceneType": "saref_confrontation",
                "conflictId": "saref_conflict_001",
                "sarefOutcome": "defeated",
                "wingsFactionOutcome": "broken",
                "summary": "Игрок победил Сарефа в прямой финальной сцене."
              },
              "ending": {
                "endingId": "saref_ending_clean_001",
                "endingType": "victory",
                "victoryTier": "clean",
                "summary": "Крылья Ангелов сломлены."
              }
            }
            """)!.AsObject());

        Assert.Equal(SarefMainStoryState.RevealStageCompleted, SarefMainStoryState.GetNodeString(root["revealStage"]));
        Assert.Equal("resolved", SarefMainStoryState.GetNodeString(root["finalConfrontation"]?["status"]));
        Assert.Equal(120, SarefMainStoryState.GetNodeInt(root["finalConfrontation"]?["resolvedAtTurn"]));
        var ending = Assert.IsType<JsonObject>(Assert.Single(root["endings"]!.AsArray()));
        Assert.Equal("saref_ending_clean_001", SarefMainStoryState.GetNodeString(ending["endingId"]));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CompletedWithoutFinalConfrontation_ReportsIssue()
    {
        await _fs.WriteFileAtomicAsync(
            SarefMainStoryState.StatePath,
            BuildSarefFinalConfrontationState("null", revealStage: "completed"));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_completed_without_final_confrontation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ResolvedFinalConfrontationWithoutDirectScene_ReportsIssue()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefFinalConfrontationState("""
        {
          "confrontationId": "saref_final_offscreen_001",
          "status": "resolved",
          "routeType": "combat",
          "victoryTier": "clean",
          "directScene": false,
          "sceneType": "saref_confrontation",
          "conflictId": "saref_conflict_001",
          "sarefOutcome": "defeated",
          "wingsFactionOutcome": "broken",
          "summary": "Сареф исчез за кадром."
        }
        """));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_final_confrontation_offscreen", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_DeepVictoryWithoutBroadGuardianPreparation_ReportsIssue()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefFinalConfrontationState("""
        {
          "confrontationId": "saref_final_deep_001",
          "status": "resolved",
          "routeType": "metaphysical",
          "victoryTier": "deep",
          "directScene": true,
          "sceneType": "final_resolution",
          "metaphysicalProofId": "source_truth_001",
          "sarefOutcome": "defeated",
          "wingsFactionOutcome": "dissolved",
          "summary": "Игрок пытается раскрыть чужемирную природу Сарефа полностью."
        }
        """));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_final_deep_victory_insufficient_guardians", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_HybridVictoryWithoutRouteComponents_ReportsIssue()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefFinalConfrontationState("""
        {
          "confrontationId": "saref_final_hybrid_001",
          "status": "resolved",
          "routeType": "hybrid",
          "victoryTier": "clean",
          "directScene": true,
          "sceneType": "final_resolution",
          "sarefOutcome": "defeated",
          "wingsFactionOutcome": "broken",
          "summary": "Игрок заявляет смешанную победу без доказанных маршрутов."
        }
        """));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_final_hybrid_missing_components", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_FinalConfrontationWithUnknownAdvantageUse_ReportsIssue()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefFinalConfrontationState("""
        {
          "confrontationId": "saref_final_advantage_001",
          "status": "resolved",
          "routeType": "oath_law",
          "victoryTier": "clean",
          "directScene": true,
          "sceneType": "final_resolution",
          "oathBreakProofId": "oath_break_001",
          "advantageUseIds": [ "use_missing_advantage" ],
          "sarefOutcome": "defeated",
          "wingsFactionOutcome": "broken",
          "summary": "Игрок ссылается на неизвестное преимущество."
        }
        """));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_final_unknown_advantage_use", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_FinalVictoryWithMismatchedWingsLifecycle_ReportsIssue()
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefFinalConfrontationState("""
        {
          "confrontationId": "saref_final_political_001",
          "status": "resolved",
          "routeType": "political",
          "victoryTier": "clean",
          "directScene": true,
          "sceneType": "final_resolution",
          "factionCampaignId": "campaign_wings_break_001",
          "sarefOutcome": "defeated",
          "wingsFactionOutcome": "broken",
          "summary": "Игрок победил Сарефа через раскол Крыльев."
        }
        """));
        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, BuildShiningWingsFactionState("active"));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_final_wings_lifecycle_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_DeepHybridVictoryWithBroadGuardianPreparation_PassesFinalValidation()
    {
        await _fs.WriteFileAtomicAsync(
            SarefMainStoryState.StatePath,
            BuildSarefFinalConfrontationState(
                """
                {
                  "confrontationId": "saref_final_deep_hybrid_001",
                  "status": "resolved",
                  "routeType": "hybrid",
                  "routeComponents": [ "combat", "oath_law", "metaphysical" ],
                  "victoryTier": "deep",
                  "directScene": true,
                  "sceneType": "final_resolution",
                  "resolvedAtTurn": 120,
                  "conflictId": "saref_conflict_001",
                  "oathBreakProofId": "oath_break_001",
                  "metaphysicalProofId": "source_truth_001",
                  "advantageUseIds": [ "use_azalia_false_loyalty" ],
                  "sarefOutcome": "defeated",
                  "wingsFactionOutcome": "dissolved",
                  "summary": "Игрок одновременно разбил Сарефа, рассек клятву и раскрыл его иномировую природу."
                }
                """,
                guardianQuestlinesPayload: BuildBroadGuardianQuestlinesPayload(),
                advantagePayload: """
                "sarefAdvantages": [
                  { "advantageId": "adv_azalia_false_loyalty", "state": "passive", "applicableScenes": [ "final_resolution", "saref_confrontation" ], "summary": "Азалия учит лгать Крыльям." }
                ],
                "sarefAdvantageUses": [
                  { "usageId": "use_azalia_false_loyalty", "advantageId": "adv_azalia_false_loyalty", "sceneType": "final_resolution", "usedAtTurn": 120, "consumesAdvantage": false, "summary": "Ложная верность открыла путь к ядру Крыльев." }
                ],
                """));
        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, BuildShiningWingsFactionState("dissolved"));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("saref_main_story_final_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void ApplyUpdate_RecordFinalConfrontation_WithDealEndingCopiesOathAndRewards()
    {
        var root = SarefMainStoryState.ApplyUpdate(
            JsonNode.Parse(BuildSarefFinalConfrontationState("null"))!.AsObject(),
            JsonNode.Parse("""
            {
              "mode": "record_final_confrontation",
              "resolvedAtTurn": 130,
              "finalConfrontation": {
                "confrontationId": "saref_deal_final_001",
                "status": "resolved",
                "routeType": "deal",
                "victoryTier": "deal",
                "directScene": true,
                "sceneType": "saref_negotiation",
                "sarefOutcome": "allied",
                "wingsFactionOutcome": "joined",
                "summary": "Игрок принимает сделку Сарефа в прямой сцене."
              },
              "playerOathState": {
                "state": "oathbound",
                "oathId": "saref_oath_001",
                "summary": "Клятва связывает игрока с Крыльями Ангелов."
              },
              "ending": {
                "endingId": "saref_ending_deal_001",
                "endingType": "deal",
                "finalConfrontationId": "saref_deal_final_001",
                "summary": "Сареф щедро награждает игрока и связывает клятвой.",
                "rewardBundle": {
                  "resourceReward": { "scale": "huge", "inkFeathers": 5000, "lightSparks": 250 },
                  "wingsAccess": { "status": "joined", "accessLevel": "inner_circle" },
                  "sarefArt": { "artId": "saref_false_light", "summary": "Особое искусство Сарефа." },
                  "sarefPassive": { "passiveId": "saref_oathfire", "summary": "Пассивная метка Крыльев." },
                  "oathCost": { "oathId": "saref_oath_001", "state": "oathbound", "summary": "Нельзя выйти из Крыльев без разрыва клятвы." }
                }
              }
            }
            """)!.AsObject());

        Assert.Equal(SarefMainStoryState.RevealStageCompleted, SarefMainStoryState.GetNodeString(root["revealStage"]));
        Assert.Equal("oathbound", SarefMainStoryState.GetNodeString(root["playerOathState"]?["state"]));
        var ending = Assert.IsType<JsonObject>(Assert.Single(root["endings"]!.AsArray()));
        Assert.Equal("deal", SarefMainStoryState.GetNodeString(ending["endingType"]));
        Assert.Equal("huge", SarefMainStoryState.GetNodeString(ending["rewardBundle"]?["resourceReward"]?["scale"]));
    }

    [Fact]
    public void ApplyUpdate_RecordFinalConfrontation_WithDealInitializesOathboundPostStoryAgenda()
    {
        var root = SarefMainStoryState.ApplyUpdate(
            JsonNode.Parse(BuildSarefFinalConfrontationState("null"))!.AsObject(),
            JsonNode.Parse("""
            {
              "mode": "record_final_confrontation",
              "resolvedAtTurn": 130,
              "finalConfrontation": {
                "confrontationId": "saref_deal_final_001",
                "status": "resolved",
                "routeType": "deal",
                "victoryTier": "deal",
                "directScene": true,
                "sceneType": "saref_negotiation",
                "sarefOutcome": "allied",
                "wingsFactionOutcome": "joined",
                "summary": "Игрок принимает сделку Сарефа в прямой сцене."
              },
              "playerOathState": {
                "state": "oathbound",
                "oathId": "saref_oath_001",
                "summary": "Клятва связывает игрока с Крыльями Ангелов."
              },
              "ending": {
                "endingId": "saref_ending_deal_001",
                "endingType": "deal",
                "finalConfrontationId": "saref_deal_final_001",
                "summary": "Сареф щедро награждает игрока и связывает клятвой.",
                "rewardBundle": {
                  "resourceReward": { "scale": "huge", "inkFeathers": 5000, "lightSparks": 250 },
                  "wingsAccess": { "status": "joined", "accessLevel": "inner_circle" },
                  "sarefArt": { "artId": "saref_false_light", "summary": "Особое искусство Сарефа." },
                  "sarefPassive": { "passiveId": "saref_oathfire", "summary": "Пассивная метка Крыльев." },
                  "oathCost": { "oathId": "saref_oath_001", "state": "oathbound", "summary": "Нельзя выйти из Крыльев без разрыва клятвы." }
                }
              }
            }
            """)!.AsObject());

        var agenda = Assert.IsType<JsonObject>(root["postStoryAgenda"]);
        Assert.Equal("oathbound_to_saref", SarefMainStoryState.GetNodeString(agenda["state"]));
        Assert.Equal("saref_deal_final_001", SarefMainStoryState.GetNodeString(agenda["sourceFinalConfrontationId"]));
        Assert.IsType<JsonArray>(agenda["assignments"]);
    }

    [Fact]
    public async Task ValidateGameStateAsync_DealEndingWithoutOathCost_ReportsIssue()
    {
        await _fs.WriteFileAtomicAsync(
            SarefMainStoryState.StatePath,
            BuildSarefFinalConfrontationState(
                BuildSarefDealFinalConfrontationPayload(),
                endingsPayload: """
                [
                  {
                    "endingId": "saref_ending_deal_001",
                    "endingType": "deal",
                    "finalConfrontationId": "saref_deal_final_001",
                    "resolvedAtTurn": 130,
                    "summary": "Сделка без цены.",
                    "rewardBundle": {
                      "resourceReward": { "scale": "huge", "inkFeathers": 5000, "lightSparks": 250 },
                      "wingsAccess": { "status": "joined", "accessLevel": "inner_circle" },
                      "sarefArt": { "artId": "saref_false_light", "summary": "Особое искусство Сарефа." },
                      "sarefPassive": { "passiveId": "saref_oathfire", "summary": "Пассивная метка Крыльев." }
                    }
                  }
                ]
                """,
                playerOathStatePayload: """
                {
                  "state": "oathbound",
                  "oathId": "saref_oath_001",
                  "summary": "Клятва связывает игрока с Крыльями Ангелов."
                }
                """));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_ending_deal_missing_oath_cost", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_OathboundDealWithoutPostStoryAgenda_ReportsIssue()
    {
        await _fs.WriteFileAtomicAsync(
            SarefMainStoryState.StatePath,
            BuildSarefFinalConfrontationState(
                BuildSarefDealFinalConfrontationPayload(),
                endingsPayload: BuildSarefDealEndingPayload(),
                playerOathStatePayload: BuildSarefOathboundPayload()));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_oathbound_agenda_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_OathboundDealWithEscapedOath_ReportsIssue()
    {
        await _fs.WriteFileAtomicAsync(
            SarefMainStoryState.StatePath,
            BuildSarefFinalConfrontationState(
                BuildSarefDealFinalConfrontationPayload(),
                endingsPayload: BuildSarefDealEndingPayload(),
                playerOathStatePayload: """
                {
                  "state": "escaped",
                  "oathId": "saref_oath_001",
                  "summary": "Игрок пытается выйти из Крыльев обычным добровольным действием."
                }
                """,
                postStoryAgendaPayload: BuildSarefOathboundAgendaPayload()));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_oathbound_left_without_oath_break", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ApplyUpdate_RecordOathBreak_MergesArcAndUpdatesOathState()
    {
        var root = SarefMainStoryState.ApplyUpdate(
            JsonNode.Parse(BuildSarefFinalConfrontationState(
                BuildSarefDealFinalConfrontationPayload(),
                endingsPayload: BuildSarefDealEndingPayload(),
                playerOathStatePayload: BuildSarefOathboundPayload(),
                postStoryAgendaPayload: BuildSarefOathboundAgendaPayload()))!.AsObject(),
            JsonNode.Parse("""
            {
              "mode": "record_oath_break",
              "oathBreakArc": {
                "arcId": "saref_oath_break_seret_001",
                "state": "broken",
                "route": "seret",
                "leadActorId": "seret",
                "startedAtTurn": 150,
                "resolvedAtTurn": 166,
                "proofSummary": "Серет нашёл лазейку в формуле клятвы.",
                "consequences": [ "renegade_from_wings", "oath_reversed", "second_confrontation_unlocked" ],
                "summary": "Клятва Сарефа разрушена, но игрок стал изменником Крыльев."
              },
              "playerOathState": {
                "state": "broken",
                "oathId": "saref_oath_001",
                "brokenAtTurn": 166,
                "summary": "Клятва Сарефа разорвана через закон Серета."
              }
            }
            """)!.AsObject());

        var agenda = Assert.IsType<JsonObject>(root["postStoryAgenda"]);
        var arc = Assert.IsType<JsonObject>(agenda["oathBreakArc"]);
        Assert.Equal("broken", SarefMainStoryState.GetNodeString(arc["state"]));
        Assert.Equal("seret", SarefMainStoryState.GetNodeString(arc["route"]));
        Assert.Equal("broken", SarefMainStoryState.GetNodeString(root["playerOathState"]?["state"]));
        var consequences = Assert.IsType<JsonArray>(arc["consequences"]);
        Assert.Contains(consequences.OfType<JsonValue>(), value =>
            string.Equals(value.GetValue<string>(), "second_confrontation_unlocked", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_BrokenOathWithoutOathBreakArc_ReportsIssue()
    {
        await _fs.WriteFileAtomicAsync(
            SarefMainStoryState.StatePath,
            BuildSarefFinalConfrontationState(
                BuildSarefDealFinalConfrontationPayload(),
                endingsPayload: BuildSarefDealEndingPayload(),
                playerOathStatePayload: """
                {
                  "state": "broken",
                  "oathId": "saref_oath_001",
                  "brokenAtTurn": 166,
                  "summary": "Клятва якобы разорвана без арки."
                }
                """,
                postStoryAgendaPayload: BuildSarefOathboundAgendaPayload()));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_oath_break_missing_arc", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_BrokenOathBreakArcWithoutProofAndConsequences_ReportsIssues()
    {
        await _fs.WriteFileAtomicAsync(
            SarefMainStoryState.StatePath,
            BuildSarefFinalConfrontationState(
                BuildSarefDealFinalConfrontationPayload(),
                endingsPayload: BuildSarefDealEndingPayload(),
                playerOathStatePayload: """
                {
                  "state": "broken",
                  "oathId": "saref_oath_001",
                  "brokenAtTurn": 166,
                  "summary": "Клятва разорвана без доказанной цены."
                }
                """,
                postStoryAgendaPayload: BuildSarefOathboundAgendaWithOathBreakPayload("""
                {
                  "arcId": "saref_oath_break_invalid_001",
                  "state": "broken",
                  "route": "seret",
                  "leadActorId": "seret",
                  "startedAtTurn": 150,
                  "resolvedAtTurn": 166,
                  "summary": "GM пытается разорвать клятву без доказательства и последствий."
                }
                """)));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_oath_break_missing_proof", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_oath_break_missing_consequence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_OathBreakWithUnknownAdvantageUse_ReportsIssue()
    {
        await _fs.WriteFileAtomicAsync(
            SarefMainStoryState.StatePath,
            BuildSarefFinalConfrontationState(
                BuildSarefDealFinalConfrontationPayload(),
                endingsPayload: BuildSarefDealEndingPayload(),
                playerOathStatePayload: BuildSarefBrokenOathPayload(),
                postStoryAgendaPayload: BuildSarefOathboundAgendaWithOathBreakPayload(BuildValidSarefOathBreakArcPayload(advantageUseId: "missing_use"))));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_oath_break_unknown_advantage_use", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_OathBreakWithKnownAdvantageAndConsequences_Passes()
    {
        await _fs.WriteFileAtomicAsync(
            SarefMainStoryState.StatePath,
            BuildSarefFinalConfrontationState(
                BuildSarefDealFinalConfrontationPayload(),
                advantagePayload: BuildSarefOathBreakAdvantagePayload(),
                endingsPayload: BuildSarefDealEndingPayload(),
                playerOathStatePayload: BuildSarefBrokenOathPayload(),
                postStoryAgendaPayload: BuildSarefOathboundAgendaWithOathBreakPayload(BuildValidSarefOathBreakArcPayload())));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("saref_main_story_oath_break_", StringComparison.OrdinalIgnoreCase) == true ||
            string.Equals(issue.Code, "saref_main_story_oathbound_left_without_oath_break", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RomanticOathBreakWithoutBelovedTraitor_ReportsIssue()
    {
        await _fs.WriteFileAtomicAsync(
            SarefMainStoryState.StatePath,
            BuildSarefFinalConfrontationState(
                BuildSarefDealFinalConfrontationPayload(),
                advantagePayload: BuildSarefOathBreakAdvantagePayload(),
                endingsPayload: BuildSarefDealEndingPayload(),
                playerOathStatePayload: BuildSarefBrokenOathPayload(),
                postStoryAgendaPayload: BuildSarefOathboundAgendaWithOathBreakPayload(BuildValidSarefOathBreakArcPayload(consequencesPayload: """
                [ "renegade_from_wings", "oath_reversed", "second_confrontation_unlocked" ]
                """)),
                personalBondPayload: """
                {
                  "state": "intimate_oath",
                  "summary": "Игрок и Сареф связали сделку с опасной романтической близостью."
                }
                """));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_oath_break_romance_missing_tragedy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_OathboundAssignmentWithoutFactionCampaign_ReportsIssue()
    {
        await _fs.WriteFileAtomicAsync(
            SarefMainStoryState.StatePath,
            BuildSarefFinalConfrontationState(
                BuildSarefDealFinalConfrontationPayload(),
                endingsPayload: BuildSarefDealEndingPayload(),
                playerOathStatePayload: BuildSarefOathboundPayload(),
                postStoryAgendaPayload: BuildSarefOathboundAgendaPayload(assignmentsPayload: """
                [
                  {
                    "assignmentId": "saref_assignment_silver_chorus_001",
                    "status": "active",
                    "targetFactionId": "silver_chorus",
                    "campaignId": "missing_campaign",
                    "objective": "Разрушить союз Серебряного Хора против Крыльев.",
                    "summary": "Сареф требует начать давление на Серебряный Хор.",
                    "createdAtTurn": 131
                  }
                ]
                """)));
        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, BuildShiningSarefAgendaState(includeCampaign: false));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_oathbound_assignment_campaign_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_OathboundDominionWithoutScene_ReportsIssue()
    {
        await _fs.WriteFileAtomicAsync(
            SarefMainStoryState.StatePath,
            BuildSarefFinalConfrontationState(
                BuildSarefDealFinalConfrontationPayload(),
                endingsPayload: BuildSarefDealEndingPayload(),
                playerOathStatePayload: BuildSarefOathboundPayload(),
                postStoryAgendaPayload: BuildSarefOathboundAgendaPayload(dominationScenePayload: "null")));
        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, BuildShiningSarefAgendaState(rivalLifecycleState: "dissolved", includeCampaign: true, campaignStatus: "completed"));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_oathbound_domination_scene_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_OathboundAgendaWithDirectiveCampaignAndDominationScene_PassesPostStoryValidation()
    {
        await _fs.WriteFileAtomicAsync(
            SarefMainStoryState.StatePath,
            BuildSarefFinalConfrontationState(
                BuildSarefDealFinalConfrontationPayload(),
                endingsPayload: BuildSarefDealEndingPayload(),
                playerOathStatePayload: BuildSarefOathboundPayload(),
                postStoryAgendaPayload: BuildSarefOathboundAgendaPayload(dominationScenePayload: """
                {
                  "sceneId": "saref_domination_scene_001",
                  "status": "completed",
                  "resolvedAtTurn": 170,
                  "summary": "Больше не осталось никого, кто мог бы противостоять Сарефу."
                }
                """)));
        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, BuildShiningSarefAgendaState(rivalLifecycleState: "dissolved", includeCampaign: true, campaignStatus: "completed"));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("saref_main_story_oathbound_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_CleanVictoryEndingMissingProtections_ReportsIssue()
    {
        await _fs.WriteFileAtomicAsync(
            SarefMainStoryState.StatePath,
            BuildSarefFinalConfrontationState(
                BuildSarefCleanFinalConfrontationPayload(),
                endingsPayload: """
                [
                  {
                    "endingId": "saref_ending_clean_001",
                    "endingType": "victory",
                    "finalConfrontationId": "saref_final_clean_001",
                    "resolvedAtTurn": 120,
                    "victoryTier": "clean",
                    "summary": "Победа без обязательных защит.",
                    "rewardBundle": {
                      "relic": { "relicId": "saref_broken_crown", "summary": "Реликвия победы." },
                      "guardianRelationshipEffects": [
                        { "guardianId": "azalia", "effect": "respect", "summary": "Азалия признаёт победу." }
                      ]
                    }
                  }
                ]
                """));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_ending_victory_missing_protection", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_EndingWithoutMatchingFinal_ReportsIssue()
    {
        await _fs.WriteFileAtomicAsync(
            SarefMainStoryState.StatePath,
            BuildSarefFinalConfrontationState(
                BuildSarefCleanFinalConfrontationPayload(),
                endingsPayload: """
                [
                  {
                    "endingId": "saref_ending_orphan_001",
                    "endingType": "victory",
                    "finalConfrontationId": "other_final",
                    "resolvedAtTurn": 120,
                    "victoryTier": "clean",
                    "summary": "Награда ссылается на другой финал.",
                    "rewardBundle": {
                      "antiOathProtection": { "protectionId": "anti_oath_clean", "summary": "Защита от клятв." },
                      "antiForeignProtection": { "protectionId": "anti_foreign_clean", "summary": "Защита от чужемирного света." },
                      "relic": { "relicId": "saref_broken_crown", "summary": "Реликвия победы." },
                      "guardianRelationshipEffects": [
                        { "guardianId": "azalia", "effect": "respect", "summary": "Азалия признаёт победу." }
                      ]
                    }
                  }
                ]
                """));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "saref_main_story_ending_final_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_DeepVictoryEndingWithFullRewardBundle_PassesEndingValidation()
    {
        await _fs.WriteFileAtomicAsync(
            SarefMainStoryState.StatePath,
            BuildSarefFinalConfrontationState(
                """
                {
                  "confrontationId": "saref_final_deep_001",
                  "status": "resolved",
                  "routeType": "hybrid",
                  "routeComponents": [ "combat", "oath_law", "metaphysical" ],
                  "victoryTier": "deep",
                  "directScene": true,
                  "sceneType": "final_resolution",
                  "resolvedAtTurn": 140,
                  "conflictId": "saref_conflict_001",
                  "oathBreakProofId": "oath_break_001",
                  "metaphysicalProofId": "source_truth_001",
                  "sarefOutcome": "defeated",
                  "wingsFactionOutcome": "dissolved",
                  "summary": "Игрок раскрывает и уничтожает основу власти Сарефа."
                }
                """,
                guardianQuestlinesPayload: BuildBroadGuardianQuestlinesPayload(),
                endingsPayload: """
                [
                  {
                    "endingId": "saref_ending_deep_001",
                    "endingType": "victory",
                    "finalConfrontationId": "saref_final_deep_001",
                    "resolvedAtTurn": 140,
                    "victoryTier": "deep",
                    "summary": "Глубокая победа меняет устройство Обители.",
                    "rewardBundle": {
                      "antiOathProtection": { "protectionId": "anti_oath_deep", "summary": "Глубокая защита от клятв." },
                      "antiForeignProtection": { "protectionId": "anti_foreign_deep", "summary": "Глубокая защита от чужемирного света." },
                      "relic": { "relicId": "saref_true_crown", "summary": "Реликвия глубокой победы." },
                      "passive": { "passiveId": "old_world_witness", "summary": "Пассив против чужого порядка." },
                      "guardianRelationshipEffects": [
                        { "guardianId": "azalia", "effect": "reverence", "summary": "Азалия видит полную правду." },
                        { "guardianId": "ilarion", "effect": "reverence", "summary": "Иларион признаёт долг." }
                      ],
                      "deepWorldStateEffects": [
                        { "effectId": "wings_dissolved", "summary": "Крылья Ангелов теряют структуру." }
                      ]
                    }
                  }
                ]
                """));
        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, BuildShiningWingsFactionState("dissolved"));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("saref_main_story_ending_", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static string BuildSarefAdvantageState(string advantagePayload) =>
        $$"""
        {
          "schemaVersion": 1,
          "revealStage": "name_revealed",
          "guardianQuestlines": [
            {
              "guardianId": "azalia",
              "questStates": [
                { "questOrdinal": 1, "status": "completed", "questId": "azalia_saref_q1" },
                { "questOrdinal": 2, "status": "completed", "questId": "azalia_saref_q2" },
                { "questOrdinal": 3, "status": "completed", "questId": "azalia_saref_q3" },
                { "questOrdinal": 4, "status": "completed", "questId": "azalia_saref_q4" }
              ]
            }
          ],
          "latentTraces": [],
          "sarefRevelations": [
            {
              "revelationId": "rev_azalia_faction",
              "category": "faction",
              "sourceGuardianId": "azalia",
              "sourceQuestId": "azalia_saref_q4",
              "sourceQuestOrdinal": 4,
              "revealedAtTurn": 44
            }
          ],
          {{advantagePayload}},
          "factionLinks": { "visibility": "hidden" },
          "defeatOutcomes": [],
          "endings": [],
          "playerOathState": null,
          "sarefPersonalBond": null
        }
        """;

    private static string BuildSarefQuestFourCompletedState(bool includeMemoryProof)
    {
        var memoryProof = includeMemoryProof
            ? $"""
              ,
                  "memorySceneProof": {BuildValidMemorySceneProofPayload()}
              """
            : string.Empty;

        return $$"""
        {
          "schemaVersion": 1,
          "revealStage": "name_revealed",
          "guardianQuestlines": [
            {
              "guardianId": "azalia",
              "questStates": [
                { "questOrdinal": 1, "status": "completed", "questId": "azalia_saref_q1" },
                { "questOrdinal": 2, "status": "completed", "questId": "azalia_saref_q2" },
                { "questOrdinal": 3, "status": "completed", "questId": "azalia_saref_q3" },
                {
                  "questOrdinal": 4,
                  "status": "completed",
                  "questId": "azalia_saref_q4",
                  "completedAtTurn": 44{{memoryProof}}
                }
              ]
            }
          ],
          "latentTraces": [],
          "sarefRevelations": [
            {
              "revelationId": "rev_azalia_faction",
              "category": "faction",
              "sourceGuardianId": "azalia",
              "sourceQuestId": "azalia_saref_q4",
              "sourceQuestOrdinal": 4,
              "revealedAtTurn": 44
            }
          ],
          "sarefAdvantages": [
            {
              "advantageId": "adv_azalia_false_loyalty",
              "sourceGuardianId": "azalia",
              "sourceQuestId": "azalia_saref_q4",
              "sourceQuestOrdinal": 4,
              "state": "available",
              "applicableScenes": [ "wings_infiltration" ],
              "summary": "Можно выдать себя за полезного перебежчика.",
              "unlockedAtTurn": 44
            }
          ],
          "sarefAdvantageUses": [],
          "memoryScene": null,
          "factionLinks": { "visibility": "hidden" },
          "defeatOutcomes": [],
          "endings": [],
          "playerOathState": null,
          "sarefPersonalBond": null
        }
        """;
    }

    private static string BuildValidMemoryScenePayload() => """
        {
          "sceneId": "memory_scene_azalia_q4",
          "status": "completed",
          "layer": "Воспоминание",
          "guardianId": "azalia",
          "questId": "azalia_saref_q4",
          "questOrdinal": 4,
          "role": {
            "roleId": "azalia_white_lodge_witness",
            "displayName": "Свидетель ложи",
            "summary": "Игрок действует через роль свидетеля старого предательства Азалии."
          },
          "boundaries": [
            { "boundaryId": "past_is_fixed", "summary": "Сареф уже вошел в ложу; это нельзя отменить." }
          ],
          "abilities": [
            { "abilityId": "read_oath", "name": "Прочитать клятву", "summary": "Увидеть скрытую цену белых перьев." },
            { "abilityId": "hold_memory", "name": "Удержать память", "summary": "Не дать сцене рассыпаться." },
            { "abilityId": "name_traitor", "name": "Назвать предателя", "summary": "Связать образ с будущей правдой о Сарефе." }
          ],
          "requiredStoryNodes": [
            { "nodeId": "enter_lodge", "status": "completed", "summary": "Игрок вошел в ложу белых перьев." },
            { "nodeId": "see_betrayal", "status": "completed", "summary": "Игрок увидел, как Сареф использовал доверие Азалии." }
          ],
          "successCondition": {
            "conditionId": "truth_recognized",
            "summary": "Игрок распознал связь ложи с Крыльями Ангелов.",
            "satisfied": true
          },
          "closureTarget": {
            "guardianId": "azalia",
            "questId": "azalia_saref_q4",
            "questOrdinal": 4,
            "revelationId": "rev_azalia_faction",
            "advantageId": "adv_azalia_false_loyalty"
          },
          "startedAtTurn": 43,
          "resolvedAtTurn": 44,
          "resolutionSummary": "Воспоминание завершено, Азалия получила правду о ложе белых перьев."
        }
        """;

    private static string BuildValidMemorySceneProofPayload() => """
        {
          "sceneId": "memory_scene_azalia_q4",
          "layer": "Воспоминание",
          "roleId": "azalia_white_lodge_witness",
          "guardianId": "azalia",
          "questId": "azalia_saref_q4",
          "questOrdinal": 4,
          "completedAtTurn": 44,
          "successConditionSatisfied": true,
          "summary": "Игрок прошел роль свидетеля и восстановил правду о ложе белых перьев."
        }
        """;

    private static string BuildSarefDefeatState(string defeatOutcomesPayload, string advantagePayload = """
          "sarefAdvantages": [],
          "sarefAdvantageUses": [],
    """) =>
        $$"""
        {
          "schemaVersion": 1,
          "revealStage": "name_revealed",
          "guardianQuestlines": [
            {
              "guardianId": "azalia",
              "questStates": [
                { "questOrdinal": 1, "status": "completed", "questId": "azalia_saref_q1" },
                { "questOrdinal": 2, "status": "completed", "questId": "azalia_saref_q2" },
                { "questOrdinal": 3, "status": "completed", "questId": "azalia_saref_q3" },
                { "questOrdinal": 4, "status": "completed", "questId": "azalia_saref_q4" }
              ]
            }
          ],
          "latentTraces": [],
          "sarefRevelations": [
            {
              "revelationId": "rev_azalia_faction",
              "category": "faction",
              "sourceGuardianId": "azalia",
              "sourceQuestId": "azalia_saref_q4",
              "sourceQuestOrdinal": 4,
              "revealedAtTurn": 44
            }
          ],
          {{advantagePayload}}
          "factionLinks": { "visibility": "hidden" },
          "finalConfrontation": null,
          "defeatOutcomes": {{defeatOutcomesPayload}},
          "endings": [],
          "playerOathState": null,
          "sarefPersonalBond": null
        }
        """;

    private static string BuildSarefFinalConfrontationState(
        string finalConfrontationPayload,
        string revealStage = "confrontation_available",
        string? guardianQuestlinesPayload = null,
        string advantagePayload = """
          "sarefAdvantages": [],
          "sarefAdvantageUses": [],
        """,
        string endingsPayload = "[]",
        string playerOathStatePayload = "null",
        string postStoryAgendaPayload = "null",
        string personalBondPayload = "null") =>
        $$"""
        {
          "schemaVersion": 1,
          "revealStage": "{{revealStage}}",
          "guardianQuestlines": {{guardianQuestlinesPayload ?? BuildSingleGuardianQuestlinePayload()}},
          "latentTraces": [],
          "sarefRevelations": [
            { "revelationId": "rev_identity", "category": "identity", "sourceGuardianId": "azalia", "sourceQuestId": "azalia_saref_q4", "sourceQuestOrdinal": 4, "revealedAtTurn": 50 },
            { "revelationId": "rev_method", "category": "method", "sourceGuardianId": "azalia", "sourceQuestId": "azalia_saref_q4", "sourceQuestOrdinal": 4, "revealedAtTurn": 51 },
            { "revelationId": "rev_faction", "category": "faction", "sourceGuardianId": "azalia", "sourceQuestId": "azalia_saref_q4", "sourceQuestOrdinal": 4, "revealedAtTurn": 52 },
            { "revelationId": "rev_path", "category": "path", "sourceGuardianId": "azalia", "sourceQuestId": "azalia_saref_q4", "sourceQuestOrdinal": 4, "revealedAtTurn": 53 }
          ],
          {{advantagePayload}}
          "wingsInfiltration": { "status": "revealed", "requestId": "saref_wings_infiltration:42", "resolvedAtTurn": 90 },
          "factionLinks": {
            "visibility": "revealed",
            "wingsFactionId": "wings_of_angels",
            "knownAgents": [
              { "agentId": "wing_agent_deceived", "supporterArchetype": "deceived", "interactionRoutes": [ "persuade" ], "summary": "Обманутый агент." },
              { "agentId": "wing_agent_oathbound", "supporterArchetype": "oathbound", "importance": "important", "interactionRoutes": [ "free" ], "summary": "Связанный агент." }
            ]
          },
          "finalConfrontation": {{finalConfrontationPayload}},
          "defeatOutcomes": [],
          "endings": {{endingsPayload}},
          "postStoryAgenda": {{postStoryAgendaPayload}},
          "playerOathState": {{playerOathStatePayload}},
          "sarefPersonalBond": {{personalBondPayload}}
        }
        """;

    private static string BuildSarefDealFinalConfrontationPayload() => """
        {
          "confrontationId": "saref_deal_final_001",
          "status": "resolved",
          "routeType": "deal",
          "victoryTier": "deal",
          "directScene": true,
          "sceneType": "saref_negotiation",
          "resolvedAtTurn": 130,
          "sarefOutcome": "allied",
          "wingsFactionOutcome": "joined",
          "summary": "Игрок принимает сделку Сарефа в прямой сцене."
        }
        """;

    private static string BuildSarefDealEndingPayload() => """
        [
          {
            "endingId": "saref_ending_deal_001",
            "endingType": "deal",
            "finalConfrontationId": "saref_deal_final_001",
            "resolvedAtTurn": 130,
            "summary": "Сареф щедро награждает игрока и связывает клятвой.",
            "rewardBundle": {
              "resourceReward": { "scale": "huge", "inkFeathers": 5000, "lightSparks": 250 },
              "wingsAccess": { "status": "joined", "accessLevel": "inner_circle" },
              "sarefArt": { "artId": "saref_false_light", "summary": "Особое искусство Сарефа." },
              "sarefPassive": { "passiveId": "saref_oathfire", "summary": "Пассивная метка Крыльев." },
              "oathCost": { "oathId": "saref_oath_001", "state": "oathbound", "summary": "Нельзя выйти из Крыльев без разрыва клятвы." }
            }
          }
        ]
        """;

    private static string BuildSarefOathboundPayload() => """
        {
          "state": "oathbound",
          "oathId": "saref_oath_001",
          "summary": "Клятва связывает игрока с Крыльями Ангелов."
        }
        """;

    private static string BuildSarefBrokenOathPayload() => """
        {
          "state": "broken",
          "oathId": "saref_oath_001",
          "brokenAtTurn": 166,
          "summary": "Клятва Сарефа разорвана доказанной аркой."
        }
        """;

    private static string BuildSarefOathboundAgendaPayload(
        string assignmentsPayload = """
        [
          {
            "assignmentId": "saref_assignment_silver_chorus_001",
            "status": "active",
            "targetFactionId": "silver_chorus",
            "campaignId": "campaign_saref_silver_chorus_001",
            "objective": "Разрушить союз Серебряного Хора против Крыльев.",
            "summary": "Сареф требует начать давление на Серебряный Хор.",
            "createdAtTurn": 131
          }
        ]
        """,
        string dominationScenePayload = "null") =>
        $$"""
        {
          "state": "oathbound_to_saref",
          "sourceFinalConfrontationId": "saref_deal_final_001",
          "startedAtTurn": 130,
          "currentObjective": "Выполнять поручения Сарефа против остальных фракций Сияющей Обители.",
          "agendaSummary": "Сделка завершила главную линию, но Сареф продолжает вести игрока к власти Крыльев.",
          "assignments": {{assignmentsPayload}},
          "dominationScene": {{dominationScenePayload}}
        }
        """;

    private static string BuildSarefOathboundAgendaWithOathBreakPayload(string oathBreakArcPayload) =>
        $$"""
        {
          "state": "oathbound_to_saref",
          "sourceFinalConfrontationId": "saref_deal_final_001",
          "startedAtTurn": 130,
          "currentObjective": "Выполнять поручения Сарефа против остальных фракций Сияющей Обители.",
          "agendaSummary": "Сделка завершила главную линию, но Сареф продолжает вести игрока к власти Крыльев.",
          "assignments": [],
          "dominationScene": null,
          "oathBreakArc": {{oathBreakArcPayload}}
        }
        """;

    private static string BuildValidSarefOathBreakArcPayload(
        string advantageUseId = "use_seret_oath_law_001",
        string consequencesPayload = """
        [ "renegade_from_wings", "oath_reversed", "beloved_traitor", "second_confrontation_unlocked" ]
        """) =>
        $$"""
        {
          "arcId": "saref_oath_break_seret_001",
          "state": "broken",
          "route": "seret",
          "leadActorId": "seret",
          "routeProofId": "seret_oath_law_proof_001",
          "startedAtTurn": 150,
          "resolvedAtTurn": 166,
          "proofSummary": "Серет, старая защита от клятв и собственная воля игрока нашли лазейку в формуле Сарефа.",
          "advantageUseIds": [ "{{advantageUseId}}" ],
          "consequences": {{consequencesPayload}},
          "summary": "Игрок разорвал клятву и стал изменником Крыльев, открыв вторую конфронтацию с Сарефом."
        }
        """;

    private static string BuildSarefOathBreakAdvantagePayload() => """
          "sarefAdvantages": [
            {
              "advantageId": "adv_seret_oath_law",
              "state": "passive",
              "applicableScenes": [ "oath_break" ],
              "summary": "Серет оставил игроку законную лазейку против клятв Сарефа."
            }
          ],
          "sarefAdvantageUses": [
            {
              "usageId": "use_seret_oath_law_001",
              "advantageId": "adv_seret_oath_law",
              "sceneType": "oath_break",
              "usedAtTurn": 166,
              "consumesAdvantage": false,
              "summary": "Преимущество Серета стало ключом к разрыву клятвы."
            }
          ],
        """;

    private static string BuildSarefCleanFinalConfrontationPayload() => """
        {
          "confrontationId": "saref_final_clean_001",
          "status": "resolved",
          "routeType": "combat",
          "victoryTier": "clean",
          "directScene": true,
          "sceneType": "saref_confrontation",
          "resolvedAtTurn": 120,
          "conflictId": "saref_conflict_001",
          "sarefOutcome": "defeated",
          "wingsFactionOutcome": "broken",
          "summary": "Игрок побеждает Сарефа в прямой сцене."
        }
        """;

    private static string BuildSingleGuardianQuestlinePayload() => """
        [
          {
            "guardianId": "azalia",
            "questStates": [
              { "questOrdinal": 1, "status": "completed", "questId": "azalia_saref_q1" },
              { "questOrdinal": 2, "status": "completed", "questId": "azalia_saref_q2" },
              { "questOrdinal": 3, "status": "completed", "questId": "azalia_saref_q3" },
              { "questOrdinal": 4, "status": "completed", "questId": "azalia_saref_q4" }
            ]
          }
        ]
        """;

    private static string BuildBroadGuardianQuestlinesPayload() => """
        [
          {
            "guardianId": "azalia",
            "questStates": [
              { "questOrdinal": 1, "status": "completed", "questId": "azalia_saref_q1" },
              { "questOrdinal": 2, "status": "completed", "questId": "azalia_saref_q2" },
              { "questOrdinal": 3, "status": "completed", "questId": "azalia_saref_q3" },
              { "questOrdinal": 4, "status": "completed", "questId": "azalia_saref_q4" }
            ]
          },
          {
            "guardianId": "ilarion",
            "questStates": [
              { "questOrdinal": 1, "status": "completed", "questId": "ilarion_saref_q1" },
              { "questOrdinal": 2, "status": "completed", "questId": "ilarion_saref_q2" },
              { "questOrdinal": 3, "status": "completed", "questId": "ilarion_saref_q3" },
              { "questOrdinal": 4, "status": "completed", "questId": "ilarion_saref_q4" }
            ]
          },
          {
            "guardianId": "veyra",
            "questStates": [
              { "questOrdinal": 1, "status": "completed", "questId": "veyra_saref_q1" },
              { "questOrdinal": 2, "status": "completed", "questId": "veyra_saref_q2" },
              { "questOrdinal": 3, "status": "completed", "questId": "veyra_saref_q3" },
              { "questOrdinal": 4, "status": "completed", "questId": "veyra_saref_q4" }
            ]
          },
          {
            "guardianId": "myriel",
            "questStates": [
              { "questOrdinal": 1, "status": "completed", "questId": "myriel_saref_q1" },
              { "questOrdinal": 2, "status": "completed", "questId": "myriel_saref_q2" },
              { "questOrdinal": 3, "status": "completed", "questId": "myriel_saref_q3" },
              { "questOrdinal": 4, "status": "completed", "questId": "myriel_saref_q4" }
            ]
          }
        ]
        """;

    private static string BuildShiningSarefAgendaState(
        string rivalLifecycleState = "active",
        bool includeCampaign = true,
        string campaignStatus = "active") =>
        $$"""
        {
          "schemaVersion": 1,
          "availability": "active",
          "radiance": {
            "experience": 0,
            "tier": 0
          },
          "preparedIncarnationPackage": null,
          "factions": [
            {
              "factionId": "wings_of_angels",
              "sarefFactionRole": "wings_of_angels",
              "sarefVisibility": "revealed",
              "factionLifecycle": {
                "state": "active"
              }
            },
            {
              "factionId": "silver_chorus",
              "factionName": "Серебряный Хор",
              "factionLifecycle": {
                "state": "{{rivalLifecycleState}}",
                "defeatedAtTurn": 160,
                "defeatReason": "Директивы Сарефа разрушили власть фракции.",
                "remnantsSummary": "Остались только разрозненные голоса."
              },
              "factionStrength": {{(rivalLifecycleState is "broken" or "dissolved" ? "0" : "40")}},
              "leadership": {
                "leadershipState": "{{(rivalLifecycleState is "broken" or "dissolved" or "leaderless" ? "vacant" : "stable")}}",
                "headActorType": null,
                "headActorId": null
              }
            }
          ],
          "factionConflictCampaigns": {{(includeCampaign ? $$"""
          [
            {
              "campaignId": "campaign_saref_silver_chorus_001",
              "targetFactionId": "silver_chorus",
              "goal": "dissolve",
              "status": "{{campaignStatus}}",
              "startedAtTurn": 131,
              "completedAtTurn": 160,
              "playerIntent": "Выполнить приказ Сарефа.",
              "summary": "Кампания Крыльев против Серебряного Хора.",
              "breakthroughLog": [
                {
                  "breakthroughId": "saref_directive_breakthrough_001",
                  "type": "saref_directive",
                  "resolvedAtTurn": 150,
                  "summary": "Директива Сарефа открыла путь к распаду фракции."
                }
              ]
            }
          ]
          """ : "[]")}}
        }
        """;

    private static string BuildShiningWingsFactionState(string lifecycleState) =>
        $$"""
        {
          "schemaVersion": 1,
          "availability": "active",
          "radiance": {
            "experience": 0,
            "tier": 0
          },
          "preparedIncarnationPackage": null,
          "factions": [
            {
              "factionId": "wings_of_angels",
              "sarefFactionRole": "wings_of_angels",
              "sarefVisibility": "revealed",
              "factionLifecycle": {
                "state": "{{lifecycleState}}"
              }
            }
          ]
        }
        """;

    private static string BuildValidSarefWingsPendingRequest() => """
    {
      "requestId": "saref_wings_infiltration:42",
      "createdAtTurn": 42,
      "createdAtUtc": "2026-05-20T00:00:00Z",
      "routeSafety": "safe",
      "entryMode": "safe_infiltration",
      "routeFragments": [
        { "revelationId": "rev_identity", "category": "identity", "summary": "Имя Сарефа." },
        { "revelationId": "rev_method", "category": "method", "summary": "Метод стирания." },
        { "revelationId": "rev_faction", "category": "faction", "summary": "Крылья Ангелов." },
        { "revelationId": "rev_path", "category": "path", "summary": "Путь к внешнему кругу." }
      ],
      "substituteFragments": [],
      "availableAdvantages": [],
      "disadvantages": [],
      "expectedResponseSurface": "sarefMainStoryUpdate",
      "expectedClosure": {
        "mode": "reveal_wings",
        "requestId": "saref_wings_infiltration:42"
      }
    }
    """;

    private async Task SeedShiningWingsPendingAcceptedTurnAsync(string currentStoryRoot)
    {
        await _fs.WriteFileAtomicAsync("ready/turn_complete.json", """{ "accepted": true }""");
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Shining Abode",
          "currentIncarnation": 1
        }
        """);
        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, """
        {
          "schemaVersion": 1,
          "availability": "active",
          "radiance": {
            "experience": 0,
            "tier": 0
          },
          "preparedIncarnationPackage": null
        }
        """);
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, currentStoryRoot);
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.PendingWingsInfiltrationPath, BuildValidSarefWingsPendingRequest());

        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Shining Abode",
          "currentIncarnation": 1
        }
        """);
        await WriteSnapshotFileAsync(ShiningAbodeState.StatePath, """
        {
          "schemaVersion": 1,
          "availability": "active",
          "radiance": {
            "experience": 0,
            "tier": 0
          },
          "preparedIncarnationPackage": null
        }
        """);
        await WriteSnapshotFileAsync(SarefMainStoryState.StatePath, BuildSarefWingsRouteState());
        await WriteSnapshotFileAsync(SarefMainStoryState.PendingWingsInfiltrationPath, BuildValidSarefWingsPendingRequest());
        await WriteValidatedSnapshotManifestAsync(
            ("game_state/meta/soul_state.json", """
            {
              "soulName": "Тестовая Душа",
              "currentRealm": "Shining Abode",
              "currentIncarnation": 1
            }
            """),
            (ShiningAbodeState.StatePath, """
            {
              "schemaVersion": 1,
              "availability": "active",
              "radiance": {
                "experience": 0,
                "tier": 0
              },
              "preparedIncarnationPackage": null
            }
            """),
            (SarefMainStoryState.StatePath, BuildSarefWingsRouteState()),
            (SarefMainStoryState.PendingWingsInfiltrationPath, BuildValidSarefWingsPendingRequest()));
    }

    private Task WriteSnapshotFileAsync(string logicalPath, string json) =>
        _fs.WriteFileAtomicAsync($"game_state/control/pending_turn_snapshot/{logicalPath}", json);

    private async Task WriteValidatedSnapshotManifestAsync(params (string Path, string Json)[] snapshotFiles)
    {
        const string sessionId = "session_saref_wings_tests";
        const string requestId = "request_saref_wings_tests";
        const int turnNumber = 43;
        const string playerAction = "[SAREF_WINGS_INFILTRATION: saref_wings_infiltration:42] Ищу Крылья Ангелов.";

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
            ["requestTimestamp"] = "2026-05-20T00:00:00Z",
            ["playerAction"] = playerAction,
            ["files"] = files,
            ["snapshotFileHashes"] = snapshotFileHashes,
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = rollbackBaselineFiles,
            ["sourceLabel"] = "saref-wings-infiltration-tests",
            ["manifestPayloadHash"] = string.Empty
        };
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);

        await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.json", manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private static string BuildSarefWingsRouteState(string? factionLinksPayload = null, string revealStage = "name_revealed") =>
        $$"""
    {
      "schemaVersion": 1,
      "revealStage": "{{revealStage}}",
      "guardianQuestlines": [
        {
          "guardianId": "azalia",
          "questStates": [
            { "questOrdinal": 1, "status": "completed", "questId": "azalia_saref_q1" },
            { "questOrdinal": 2, "status": "completed", "questId": "azalia_saref_q2" },
            { "questOrdinal": 3, "status": "completed", "questId": "azalia_saref_q3" },
            { "questOrdinal": 4, "status": "completed", "questId": "azalia_saref_q4" }
          ]
        }
      ],
      "latentTraces": [],
      "sarefRevelations": [
        { "revelationId": "rev_identity", "category": "identity", "sourceGuardianId": "azalia", "sourceQuestId": "azalia_saref_q4", "sourceQuestOrdinal": 4, "revealedAtTurn": 50 },
        { "revelationId": "rev_method", "category": "method", "sourceGuardianId": "azalia", "sourceQuestId": "azalia_saref_q4", "sourceQuestOrdinal": 4, "revealedAtTurn": 51 },
        { "revelationId": "rev_faction", "category": "faction", "sourceGuardianId": "azalia", "sourceQuestId": "azalia_saref_q4", "sourceQuestOrdinal": 4, "revealedAtTurn": 52 },
        { "revelationId": "rev_path", "category": "path", "sourceGuardianId": "azalia", "sourceQuestId": "azalia_saref_q4", "sourceQuestOrdinal": 4, "revealedAtTurn": 53 }
      ],
      "sarefAdvantages": [],
      "sarefAdvantageUses": [],
      "wingsInfiltration": null,
      {{factionLinksPayload ?? "\"factionLinks\": { \"visibility\": \"hidden\" }"}},
      "finalConfrontation": null,
      "defeatOutcomes": [],
      "endings": [],
      "playerOathState": null,
      "sarefPersonalBond": null
    }
    """;

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // best effort cleanup
        }
    }
}
