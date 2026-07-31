using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "FullValidation")]
public sealed partial class CanonicalStateNormalizerTests
{
    [Fact]
    public async Task NormalizeAccumulatedStateAsync_ProjectsAfterlifeChronicleUpdates()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            AfterlifeChronicleState.StatePath,
            """
            {
              "schemaVersion": 1,
              "chronicles": [
                {
                  "chronicleId": "chaos_black_tide",
                  "scopeType": "chaos_sea_region",
                  "scopeId": "black_tide",
                  "displayName": "Черный Прилив",
                  "eventDescriptions": [
                    "[Turn 10] Игрок впервые увидел черную волну."
                  ],
                  "lastEventsDescription": "[Turn 11] Хранитель Зеркал оставил знак на берегу.",
                  "persistentConsequences": [
                    "На берегу остался зеркальный знак."
                  ],
                  "openThreads": [
                    "Понять, кто оставил знак."
                  ],
                  "lastUpdatedTurn": 11
                }
              ],
              "afterlifeChronicleUpdates": [
                {
                  "chronicleId": "chaos_black_tide",
                  "scopeType": "chaos_sea_region",
                  "scopeId": "black_tide",
                  "displayName": "Черный Прилив",
                  "lastEventsDescription": "[Turn 12] Игрок нашел след Хранителя под черной водой.",
                  "persistentConsequences": [
                    "Зеркальный знак начал светиться."
                  ],
                  "openThreads": [
                    "Проследить путь под черной водой."
                  ],
                  "lastUpdatedTurn": 12
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeChronicleState.StatePath))!)!.AsObject();
        Assert.False(root.ContainsKey(AfterlifeChronicleState.UpdateProperty));
        Assert.Equal(1, root["schemaVersion"]?.GetValue<int>());

        var chronicle = Assert.Single(root[AfterlifeChronicleState.ChroniclesProperty]!.AsArray().OfType<JsonObject>());
        Assert.Equal("chaos_black_tide", chronicle["chronicleId"]?.GetValue<string>());
        Assert.Equal("[Turn 12] Игрок нашел след Хранителя под черной водой.", chronicle["lastEventsDescription"]?.GetValue<string>());
        Assert.Equal(12, chronicle["lastUpdatedTurn"]?.GetValue<int>());

        var events = Assert.IsType<JsonArray>(chronicle["eventDescriptions"]);
        Assert.Contains(events.OfType<JsonValue>(), value =>
            value.GetValue<string>() == "[Turn 10] Игрок впервые увидел черную волну.");
        Assert.Contains(events.OfType<JsonValue>(), value =>
            value.GetValue<string>() == "[Turn 11] Хранитель Зеркал оставил знак на берегу.");
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_AddsMissingAfterlifeChronicleEventArchive()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            AfterlifeChronicleState.StatePath,
            """
            {
              "schemaVersion": 1,
              "chronicles": [
                {
                  "chronicleId": "guardian_first_meeting",
                  "scopeType": "guardian_scene",
                  "scopeId": "guard_system_myriel_001",
                  "displayName": "Первая встреча с Мириэль",
                  "lastEventsDescription": "Элиан впервые встретила Мириэль в Своде Пепельных Созвездий.",
                  "persistentConsequences": [
                    "Обитель Мириэль стала первой безопасной точкой души."
                  ],
                  "openThreads": [
                    "Элиан еще не выбрала первое воплощение."
                  ],
                  "lastUpdatedTurn": 1
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeChronicleState.StatePath))!)!.AsObject();
        var chronicle = Assert.Single(root[AfterlifeChronicleState.ChroniclesProperty]!.AsArray().OfType<JsonObject>());
        var events = Assert.IsType<JsonArray>(chronicle["eventDescriptions"]);
        Assert.Empty(events);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_LocalizesInternalRealmTermsInAfterlifeChronicles()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            AfterlifeChronicleState.StatePath,
            """
            {
              "schemaVersion": 1,
              "chronicles": [
                {
                  "chronicleId": "guardian_first_incarnation",
                  "scopeType": "guardian_scene",
                  "scopeId": "guard_system_azalia_001",
                  "displayName": "Переход к первой жизни",
                  "lastEventsDescription": "Душа прошла из Chaos Sea в Mortal World.",
                  "persistentConsequences": [
                    "Mortal World стал первой целью души."
                  ],
                  "openThreads": [
                    "Проследить, как страх afterlife проявится в ShiningAbode."
                  ],
                  "eventDescriptions": [
                    "Азалия встретила душу в ChaosSea."
                  ],
                  "lastUpdatedTurn": 2
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var json = (await _fs.ReadFileAsync(AfterlifeChronicleState.StatePath))!;
        Assert.DoesNotContain("Mortal World", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ChaosSea", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ShiningAbode", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("afterlife", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("смертный мир", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Море Хаоса", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Сияющая Обитель", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("посмертие", json, StringComparison.OrdinalIgnoreCase);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_chronicle_player_text_internal_term", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_PreservesAfterlifeChronicleArchiveOnDirectReplacement()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        const string backupPath = "game_state/control/test_backups/afterlife_chronicles_previous.json";
        await _fs.WriteFileAtomicAsync(
            backupPath,
            """
            {
              "schemaVersion": 1,
              "chronicles": [
                {
                  "chronicleId": "guardian_first_meeting",
                  "scopeType": "guardian_scene",
                  "scopeId": "guard_system_myriel_001",
                  "displayName": "Первая встреча с Мириэль",
                  "lastEventsDescription": "Элиан впервые встретила Мириэль в Своде Пепельных Созвездий.",
                  "eventDescriptions": [
                    "Элиан пробудилась в Море Хаоса."
                  ],
                  "persistentConsequences": [
                    "Обитель Мириэль стала первой безопасной точкой души."
                  ],
                  "openThreads": [
                    "Элиан еще не выбрала первое воплощение."
                  ],
                  "lastUpdatedTurn": 1
                }
              ]
            }
            """);
        await _fs.WriteFileAtomicAsync(
            AfterlifeChronicleState.StatePath,
            """
            {
              "schemaVersion": 1,
              "chronicles": [
                {
                  "chronicleId": "guardian_first_meeting",
                  "scopeType": "guardian_scene",
                  "scopeId": "guard_system_myriel_001",
                  "displayName": "Первая встреча с Мириэль",
                  "lastEventsDescription": "Мириэль объяснила роль Хранителя до первой смертной жизни.",
                  "persistentConsequences": [
                    "Элиан знает, что Хранитель является свидетелем, якорем и мерой."
                  ],
                  "openThreads": [
                    "Уточнить цену помощи Хранителя."
                  ],
                  "lastUpdatedTurn": 2
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [AfterlifeChronicleState.StatePath] = backupPath
            });

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeChronicleState.StatePath))!)!.AsObject();
        var chronicle = Assert.Single(root[AfterlifeChronicleState.ChroniclesProperty]!.AsArray().OfType<JsonObject>());
        Assert.Equal("Мириэль объяснила роль Хранителя до первой смертной жизни.", chronicle["lastEventsDescription"]?.GetValue<string>());

        var events = Assert.IsType<JsonArray>(chronicle["eventDescriptions"]);
        Assert.Contains(events.OfType<JsonValue>(), value =>
            value.GetValue<string>() == "Элиан пробудилась в Море Хаоса.");
        Assert.Contains(events.OfType<JsonValue>(), value =>
            value.GetValue<string>() == "Элиан впервые встретила Мириэль в Своде Пепельных Созвездий.");
    }
}
