using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class CanonicalStateNormalizerTests
{
    [Fact]
    public async Task NormalizeAccumulatedStateAsync_ProjectsAfterlifeStoryOutline()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            AfterlifeStoryOutlineState.StatePath,
            """
            {
              "schemaVersion": 1,
              "mainArc": "старый гибкий план",
              "actorSubplots": [],
              "factionOrInstitutionArcs": [],
              "loomingThreatsOrOpportunities": [],
              "pendingRevelations": [],
              "nextLikelySceneBeats": [],
              "playerAgencyNotes": "старое правило agency",
              "lastUpdatedTurn": 8,
              "afterlifeStoryOutline": {
                "mainArc": "Скрытые следы Крыльев Ангелов",
                "realmArc": "Азалия готовит сцену памяти",
                "actorSubplots": [
                  {
                    "actorRef": "guardian:azalia",
                    "summary": "Азалия сомневается, готова ли Душа услышать правду."
                  }
                ],
                "factionOrInstitutionArcs": [],
                "loomingThreatsOrOpportunities": [
                  "Черный прилив может стать входом к старой клятве."
                ],
                "pendingRevelations": [
                  "Не раскрывать имя Сарефа до нужной сцены."
                ],
                "nextLikelySceneBeats": [
                  "Если игрок останется, провести сцену воспоминания."
                ],
                "playerAgencyNotes": "План гибкий; если игрок откажется, не форсировать сцену.",
                "lastUpdatedTurn": 9
              }
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeStoryOutlineState.StatePath))!)!.AsObject();
        Assert.False(root.ContainsKey(AfterlifeStoryOutlineState.ResponseField));
        Assert.Equal(1, root["schemaVersion"]?.GetValue<int>());
        Assert.Equal("Скрытые следы Крыльев Ангелов", root["mainArc"]?.GetValue<string>());
        Assert.Equal("Азалия готовит сцену памяти", root["realmArc"]?.GetValue<string>());
        Assert.Equal(9, root["lastUpdatedTurn"]?.GetValue<int>());
        Assert.Single(root["actorSubplots"]!.AsArray());
        Assert.Contains(root["pendingRevelations"]!.AsArray().OfType<JsonValue>(), value =>
            value.GetValue<string>() == "Не раскрывать имя Сарефа до нужной сцены.");
    }
}
