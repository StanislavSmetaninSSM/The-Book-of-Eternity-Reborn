using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

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
}
