using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class CanonicalStateNormalizerTests
{
    [Fact]
    public async Task NormalizeAccumulatedStateAsync_ProjectsAfterlifeThreatCommandsWithBaseline()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        const string baselinePath = "test_backups/afterlife_threats_baseline.json";
        await _fs.WriteFileAtomicAsync(
            baselinePath,
            """
            {
              "schemaVersion": 1,
              "threats": [
                {
                  "threatId": "chaos_soul_hunter_pack",
                  "realm": "Chaos Sea",
                  "scopeId": "black_tide_shore",
                  "displayName": "Стая охотников за душами",
                  "threatArchetype": { "motivation": "Consumption", "method": "Overt" },
                  "intensity": 4,
                  "currentActivity": {
                    "activityId": "hunt_001",
                    "activityName": "Идут по следу души",
                    "description": "Охотники ищут след игрока у Черного Прилива.",
                    "activeState": "Active"
                  },
                  "impactProfile": {
                    "primaryTargetType": "Location",
                    "primaryTargetId": "black_tide_shore",
                    "primaryTargetName": "Берег Черного Прилива",
                    "primaryImpact": "Covert",
                    "baseImpactValue": 4
                  },
                  "visibleToPlayer": true,
                  "ledger": []
                }
              ]
            }
            """);

        await _fs.WriteFileAtomicAsync(
            AfterlifeActiveThreatState.StatePath,
            """
            {
              "afterlifeThreatsToAdd": [
                {
                  "threatId": "wings_hidden_cell",
                  "realm": "Shining Abode",
                  "scopeId": "choir_district",
                  "displayName": "Тайная ячейка Крыльев Ангелов",
                  "threatArchetype": { "motivation": "Domination", "method": "Covert" },
                  "intensity": 5,
                  "currentActivity": {
                    "activityId": "cell_recruitment",
                    "activityName": "Вербует резидентов хора",
                    "description": "Ячейка ищет тех, кто сомневается в Обители.",
                    "activeState": "Active"
                  },
                  "impactProfile": {
                    "primaryTargetType": "Faction",
                    "primaryTargetId": "faction_radiant_choir",
                    "primaryTargetName": "Лучезарный Хор",
                    "primaryImpact": "Social",
                    "baseImpactValue": 5
                  },
                  "visibleToPlayer": false,
                  "linkedFactionId": "wings_of_angels",
                  "sarefLink": { "role": "hidden_cell", "evidenceLevel": "suspected" },
                  "ledger": []
                }
              ],
              "afterlifeThreatsToUpdate": [
                {
                  "threatId": "chaos_soul_hunter_pack",
                  "intensity": 6,
                  "currentActivity": {
                    "description": "Охотники нашли свежий след и ускорились."
                  },
                  "ledgerEntry": {
                    "turnNumber": 12,
                    "summary": "След игрока стал заметнее."
                  }
                }
              ],
              "completeAfterlifeThreatActivities": [
                {
                  "threatId": "chaos_soul_hunter_pack",
                  "activityId": "hunt_001",
                  "finalState": "completed",
                  "completionSummary": "Охотники дошли до старого следа, но потеряли душу у зеркальной отмели.",
                  "completedAtTurn": 13
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [AfterlifeActiveThreatState.StatePath] = baselinePath
        });

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeActiveThreatState.StatePath))!)!.AsObject();
        Assert.False(root.ContainsKey(AfterlifeActiveThreatState.AddsProperty));
        Assert.False(root.ContainsKey(AfterlifeActiveThreatState.UpdatesProperty));
        Assert.False(root.ContainsKey(AfterlifeActiveThreatState.CompleteActivitiesProperty));

        var threats = Assert.IsType<JsonArray>(root[AfterlifeActiveThreatState.ThreatsProperty]);
        Assert.Equal(2, threats.Count);

        var hunter = threats.OfType<JsonObject>().Single(threat =>
            string.Equals(threat["threatId"]?.GetValue<string>(), "chaos_soul_hunter_pack", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(6, hunter["intensity"]?.GetValue<int>());
        Assert.Null(hunter["currentActivity"]);
        var hunterLedger = Assert.IsType<JsonArray>(hunter["ledger"]);
        Assert.Contains(hunterLedger.OfType<JsonObject>(), entry =>
            string.Equals(entry["finalState"]?.GetValue<string>(), "completed", StringComparison.OrdinalIgnoreCase));

        var wings = threats.OfType<JsonObject>().Single(threat =>
            string.Equals(threat["threatId"]?.GetValue<string>(), "wings_hidden_cell", StringComparison.OrdinalIgnoreCase));
        Assert.False(wings["visibleToPlayer"]?.GetValue<bool>() ?? true);
        Assert.Equal("wings_of_angels", wings["linkedFactionId"]?.GetValue<string>());
    }
}
