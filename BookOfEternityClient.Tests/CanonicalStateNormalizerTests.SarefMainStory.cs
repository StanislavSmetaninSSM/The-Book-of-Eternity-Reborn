using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class CanonicalStateNormalizerTests
{
    [Fact]
    public void NormalizerBackupInputFiles_IncludesSarefMainStoryState()
    {
        Assert.Contains(
            SarefMainStoryState.StatePath,
            CanonicalStateNormalizer.NormalizerBackupInputFiles,
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_SarefMainStoryUpdateWrapper_ProjectsAgainstBackupBaseline()
    {
        var backupPath = "test_backups/pre_saref_story.json";
        await _fs.WriteFileAtomicAsync(backupPath, BuildSarefRouteBaseline());
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, """
        {
          "sarefMainStoryUpdate": {
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
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [SarefMainStoryState.StatePath] = backupPath
        });

        var raw = await _fs.ReadFileAsync(SarefMainStoryState.StatePath);
        var root = JsonNode.Parse(raw!)!.AsObject();

        Assert.False(root.ContainsKey(SarefMainStoryState.ResponseField));
        Assert.Equal("wings_revealed", root["revealStage"]!.GetValue<string>());
        Assert.Equal("revealed", root["wingsInfiltration"]!["status"]!.GetValue<string>());
        Assert.Equal("saref_wings_infiltration:42", root["wingsInfiltration"]!["requestId"]!.GetValue<string>());
        Assert.Equal(43, root["wingsInfiltration"]!["resolvedAtTurn"]!.GetValue<int>());
        Assert.Equal("revealed", root["factionLinks"]!["visibility"]!.GetValue<string>());
        Assert.Equal("wings_of_angels", root["factionLinks"]!["wingsFactionId"]!.GetValue<string>());
        Assert.Equal(4, root["sarefRevelations"]!.AsArray().Count);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_SarefDefeatOutcomeUpdateWrapper_ProjectsAgainstBackupBaseline()
    {
        var backupPath = "test_backups/pre_saref_story_defeat.json";
        await _fs.WriteFileAtomicAsync(backupPath, BuildSarefRouteBaseline());
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, """
        {
          "sarefMainStoryUpdate": {
            "mode": "record_defeat_outcome",
            "resolvedAtTurn": 91,
            "defeatOutcome": {
              "outcomeId": "saref_defeat_forced_oath_001",
              "outcomeType": "forced_oath",
              "sceneType": "saref_confrontation",
              "oathId": "saref_oath_001",
              "summary": "Сареф принудил душу к клятве после поражения.",
              "gmMotivation": "Сареф хочет использовать игрока как связанную фигуру."
            },
            "playerOathState": {
              "state": "oathbound",
              "oathId": "saref_oath_001",
              "summary": "Игрок связан клятвой Сарефа."
            }
          }
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [SarefMainStoryState.StatePath] = backupPath
        });

        var raw = await _fs.ReadFileAsync(SarefMainStoryState.StatePath);
        var root = JsonNode.Parse(raw!)!.AsObject();

        Assert.False(root.ContainsKey(SarefMainStoryState.ResponseField));
        var outcome = Assert.IsType<JsonObject>(Assert.Single(root["defeatOutcomes"]!.AsArray()));
        Assert.Equal("saref_defeat_forced_oath_001", outcome["outcomeId"]!.GetValue<string>());
        Assert.Equal("forced_oath", outcome["outcomeType"]!.GetValue<string>());
        Assert.Equal(91, outcome["resolvedAtTurn"]!.GetValue<int>());
        Assert.Equal("oathbound", root["playerOathState"]!["state"]!.GetValue<string>());
    }

    private static string BuildSarefRouteBaseline() => """
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
        { "revelationId": "rev_identity", "category": "identity", "sourceGuardianId": "azalia", "sourceQuestId": "azalia_saref_q4", "sourceQuestOrdinal": 4, "revealedAtTurn": 50 },
        { "revelationId": "rev_method", "category": "method", "sourceGuardianId": "azalia", "sourceQuestId": "azalia_saref_q4", "sourceQuestOrdinal": 4, "revealedAtTurn": 51 },
        { "revelationId": "rev_faction", "category": "faction", "sourceGuardianId": "azalia", "sourceQuestId": "azalia_saref_q4", "sourceQuestOrdinal": 4, "revealedAtTurn": 52 },
        { "revelationId": "rev_path", "category": "path", "sourceGuardianId": "azalia", "sourceQuestId": "azalia_saref_q4", "sourceQuestOrdinal": 4, "revealedAtTurn": 53 }
      ],
      "sarefAdvantages": [],
      "sarefAdvantageUses": [],
      "wingsInfiltration": null,
      "factionLinks": { "visibility": "hidden", "wingsFactionId": null },
      "finalConfrontation": null,
      "defeatOutcomes": [],
      "endings": [],
      "playerOathState": null,
      "sarefPersonalBond": null
    }
    """;
}
