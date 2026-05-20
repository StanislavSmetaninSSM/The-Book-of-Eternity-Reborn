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
