using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ChaosSeaBootstrapStateBuilderTests
{
    [Fact]
    public void BuildFreshNewGameFiles_ReturnsMeaningfulRequiredChaosSeaBootstrapFiles()
    {
        var files = ChaosSeaBootstrapStateBuilder.BuildFreshNewGameFiles(
            soulName: "Искра после перезагрузки",
            soulFormDescription: "серебристая человеческая фигура",
            guardianName: "Иларион Архивный Свет",
            abodeName: "Архив Лучистых Тишин",
            createdAtUtc: DateTimeOffset.Parse("2026-06-29T00:00:00Z"));

        foreach (var requiredPath in new[]
        {
            "game_state/meta/character_chronicle.json",
            "lore/codex_entries.json",
            "lore/chaos_sea/cosmology.json",
            "lore/chaos_sea/soul_system_lore.json",
            "lore/chaos_sea/guardians_lore.json"
        })
        {
            Assert.True(files.ContainsKey(requiredPath), $"Missing {requiredPath}");
            AssertMeaningful(files[requiredPath]);
        }

        var codex = files["lore/codex_entries.json"];
        var entries = Assert.IsType<JsonArray>(codex["entries"]);
        Assert.True(entries.Count >= 3);
        Assert.Equal(entries.Count, codex["totalEntries"]?.GetValue<int>());
        Assert.Contains(entries, entry =>
            string.Equals(entry?["sourceFile"]?.GetValue<string>(), "lore/chaos_sea/cosmology.json", StringComparison.Ordinal));
        Assert.Contains(entries, entry =>
            string.Equals(entry?["sourceFile"]?.GetValue<string>(), "lore/chaos_sea/guardians_lore.json", StringComparison.Ordinal));
        Assert.Contains(entries, entry =>
            string.Equals(entry?["sourceFile"]?.GetValue<string>(), "lore/chaos_sea/soul_system_lore.json", StringComparison.Ordinal));

        var characterChronicle = files["game_state/meta/character_chronicle.json"];
        var chronicleEntries = Assert.IsType<JsonArray>(characterChronicle["entries"]);
        var firstEntry = Assert.IsType<JsonObject>(Assert.Single(chronicleEntries));
        Assert.Contains("Искра после перезагрузки", firstEntry["summary"]?.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("Иларион Архивный Свет", firstEntry["summary"]?.GetValue<string>(), StringComparison.Ordinal);
    }

    private static void AssertMeaningful(JsonNode? node)
    {
        Assert.True(HasMeaningfulContent(node), "Expected bootstrap JSON to contain at least one non-empty string.");
    }

    private static bool HasMeaningfulContent(JsonNode? node) => node switch
    {
        JsonObject obj => obj.Any(property => !property.Key.StartsWith("_", StringComparison.Ordinal) && HasMeaningfulContent(property.Value)),
        JsonArray arr => arr.Any(HasMeaningfulContent),
        JsonValue value => value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text),
        _ => false
    };
}
