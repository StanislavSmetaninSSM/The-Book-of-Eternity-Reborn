using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
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
