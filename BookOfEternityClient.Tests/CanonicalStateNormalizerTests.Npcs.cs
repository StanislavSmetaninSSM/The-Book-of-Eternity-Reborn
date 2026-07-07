using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class CanonicalStateNormalizerTests
{
    [Fact]
    public async Task NormalizeAccumulatedStateAsync_CanonicalizesNpcJournalEntryStrings()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "NPCsInScene": [
            {
              "npcId": "npc_valmont_house_servant_001",
              "name": "Домашний слуга Валмонтов",
              "role": "Слуга дома Вальмонт"
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_journals.json", """
        {
          "NPCJournals": [
            {
              "npcId": "npc_valmont_house_servant_001",
              "npcName": "Домашний слуга Валмонтов",
              "lastJournalNote": "Я видел посыльного у боковой лестницы.",
              "journalEntries": [
                "Я видел посыльного у боковой лестницы.",
                {
                  "entryId": "journal_servant_002",
                  "note": "Порошок на перчатке посыльного совпал с краем конверта.",
                  "timestamp": "2026-07-07T04:17:06.8974555Z"
                }
              ]
            },
            {
              "npcId": "npc_valmont_house_servant_001",
              "npcName": "Домашний слуга Валмонтов",
              "entry": "Слуга вспомнил серебряную окантовку на форме посыльного."
            }
          ]
        }
        """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync("game_state/npcs/npc_journals.json"))!)!.AsObject();
        var journals = root["NPCJournals"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.Equal(2, journals.Count);
        var journal = journals[0];
        var entries = journal["journalEntries"]!.AsArray();
        var first = Assert.IsType<JsonObject>(entries[0]);
        var second = Assert.IsType<JsonObject>(entries[1]);
        Assert.Equal("Я видел посыльного у боковой лестницы.", first["description"]?.GetValue<string>());
        Assert.Equal("Порошок на перчатке посыльного совпал с краем конверта.", second["description"]?.GetValue<string>());
        Assert.Equal("journal_servant_002", second["entryId"]?.GetValue<string>());
        Assert.Equal("Слуга вспомнил серебряную окантовку на форме посыльного.", journals[1]["lastJournalNote"]?.GetValue<string>());
        var legacyEntry = Assert.Single(journals[1]["journalEntries"]!.AsArray().OfType<JsonObject>());
        Assert.Equal("Слуга вспомнил серебряную окантовку на форме посыльного.", legacyEntry["description"]?.GetValue<string>());

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();
        Assert.DoesNotContain(issues, issue =>
            issue.FilePath.Contains("npc_journals", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(issue.Code, "expected_object", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(issue.Code, "missing_required_string", StringComparison.OrdinalIgnoreCase)));
    }
}
