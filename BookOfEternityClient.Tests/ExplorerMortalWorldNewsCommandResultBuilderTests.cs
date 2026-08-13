using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ExplorerMortalWorldNewsCommandResultBuilderTests : IDisposable
{
    private static readonly JsonSerializerOptions PlayerJson = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public ExplorerMortalWorldNewsCommandResultBuilderTests()
    {
        _rootPath = Path.Combine(
            Path.GetTempPath(),
            "boe-mortal-world-news-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task BuildAsync_ThreatSummaryCountsOnlyAcceptedDiscoverySafeLocationsOnce()
    {
        var current = CreateLocationWithThreat(
            "loc_news_current",
            "Текущая площадь",
            "visited",
            "Принятая угроза площади",
            x: 1);
        var discovered = CreateLocationWithThreat(
            "loc_news_discovered",
            "Открытая башня",
            "discovered",
            "Принятая угроза башни",
            x: 2);
        var rumored = CreateLocationWithThreat(
            "loc_news_rumored",
            "Башня из слухов",
            "rumored",
            "PRIVATE_RUMORED_THREAT",
            x: 3);
        var hidden = CreateLocationWithThreat(
            "loc_news_hidden",
            "PRIVATE_HIDDEN_LOCATION",
            "hidden",
            "PRIVATE_HIDDEN_THREAT",
            x: 4);
        var rejected = CreateLocationWithThreat(
            "loc_news_rejected",
            "PRIVATE_REJECTED_LOCATION",
            "visited",
            "PRIVATE_REJECTED_THREAT",
            x: 5);
        rejected.Remove("materializationReceipt");

        await WriteCanonicalStateAsync(
            [current, discovered, rumored, hidden, rejected],
            MortalLocationTestFixture.CreateCurrentProjection(current),
            [current, discovered, rumored, hidden]);

        var result = await ExplorerMortalWorldNewsCommandResultBuilder.BuildAsync(
            "/новости_мира",
            _fs);
        var payload = JsonSerializer.Serialize(result, PlayerJson);

        Assert.Contains("Угрозы локаций", payload, StringComparison.Ordinal);
        Assert.Contains("2 угрозы", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("3 угрозы", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("materializationReceipt", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("location_identity_index", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildAsync_EventDetailResolvesOnlyExactAcceptedLocationReferences()
    {
        var visible = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_news_visible",
            "Каноническая северная площадь",
            "visited",
            x: 10,
            y: 2);
        var hidden = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_news_hidden_event",
            "PRIVATE_HIDDEN_EVENT_LOCATION",
            "hidden",
            x: 11,
            y: 2);
        var rejected = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_news_rejected_event",
            "PRIVATE_REJECTED_EVENT_LOCATION",
            "visited",
            x: 12,
            y: 2);
        rejected.Remove("materializationReceipt");
        await WriteCanonicalStateAsync(
            [visible, hidden, rejected],
            MortalLocationTestFixture.CreateCurrentProjection(visible),
            [visible, hidden]);
        await _fs.WriteFileAtomicAsync(
            "game_state/world/world_events.json",
            new JsonObject
            {
                ["worldEventsLog"] = new JsonArray(new JsonObject
                {
                    ["eventId"] = "event_exact_location",
                    ["title"] = "Следы у площади",
                    ["visibility"] = "public",
                    ["locationId"] = "loc_news_visible",
                    ["location"] = "PRIVATE_SPOOFED_TOP_LEVEL_LOCATION",
                    ["description"] = "Стража нашла следы у северной стены.",
                    ["affectedLocations"] = new JsonArray(
                        new JsonObject
                        {
                            ["locationId"] = "loc_news_visible",
                            ["locationName"] = "PRIVATE_SPOOFED_ACCEPTED_LABEL",
                            ["impactDescription"] = "На площади усилили дозор."
                        },
                        new JsonObject
                        {
                            ["locationId"] = "LOC_NEWS_VISIBLE",
                            ["locationName"] = "PRIVATE_WRONG_CASE_LOCATION"
                        },
                        new JsonObject
                        {
                            ["locationId"] = "loc_news_hidden_event",
                            ["locationName"] = "PRIVATE_HIDDEN_JOIN"
                        },
                        new JsonObject
                        {
                            ["locationId"] = "loc_news_rejected_event",
                            ["locationName"] = "PRIVATE_REJECTED_JOIN"
                        },
                        new JsonObject
                        {
                            ["locationName"] = "PRIVATE_NAME_ONLY_JOIN"
                        })
                })
            }.ToJsonString());

        var result = await ExplorerMortalWorldNewsCommandResultBuilder.BuildAsync(
            "/новости_мира событие event_exact_location",
            _fs);
        var payload = JsonSerializer.Serialize(result, PlayerJson);

        Assert.Contains("Каноническая северная площадь", payload, StringComparison.Ordinal);
        Assert.Contains("На площади усилили дозор", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("loc_news_visible", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("LOC_NEWS_VISIBLE", payload, StringComparison.Ordinal);
    }

    private static JsonObject CreateLocationWithThreat(
        string locationId,
        string locationName,
        string discoveryTier,
        string threatName,
        int x)
    {
        var location = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            locationId,
            locationName,
            discoveryTier,
            x,
            y: 1);
        location["activeThreats"] = new JsonArray(new JsonObject
        {
            ["threatId"] = "threat_" + locationId,
            ["name"] = threatName,
            ["description"] = "Угроза требует внимания героя.",
            ["intensity"] = 3,
            ["longTermGoal"] = "Закрепиться в этой локации.",
            ["currentActivity"] = null,
            ["threatArchetype"] = new JsonObject
            {
                ["motivation"] = "Domination",
                ["method"] = "Overt",
                ["customMotivation"] = null,
                ["customMethod"] = null
            },
            ["impactProfile"] = new JsonObject
            {
                ["primaryTargetType"] = "Location",
                ["primaryTargetId"] = null,
                ["primaryTargetName"] = locationName,
                ["primaryImpact"] = "Stability",
                ["baseImpactValue"] = 2
            }
        });
        location["materialization"]!["sections"]!["activeThreats"] = new JsonObject
        {
            ["disposition"] = "populated",
            ["reason"] = null
        };
        MortalLocationTestFixture.ResealCanonicalLocation(location);
        using (var document = JsonDocument.Parse(location.ToJsonString()))
        {
            Assert.Empty(MortalLocationMaterializationContract.ValidateCanonicalLocation(
                document.RootElement,
                "news projection canonical location"));
        }
        return location;
    }

    private async Task WriteCanonicalStateAsync(
        IReadOnlyCollection<JsonObject> mapLocations,
        JsonObject current,
        IReadOnlyCollection<JsonObject> indexedLocations)
    {
        var index = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["realm"] = "mortal_world",
            ["locationEntries"] = new JsonArray(),
            ["linkEntries"] = new JsonArray()
        };
        foreach (var location in indexedLocations)
        {
            var single = MortalLocationTestFixture.CreateIdentityIndex(location);
            index["locationEntries"]!.AsArray().Add(single["locationEntries"]![0]!.DeepClone());
        }

        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            MortalLocationTestFixture.CreateWorldMap(mapLocations.ToArray()).ToJsonString());
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            current.ToJsonString());
        await _fs.WriteFileAtomicAsync(
            MortalLocationIdentityState.StatePath,
            index.ToJsonString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}
