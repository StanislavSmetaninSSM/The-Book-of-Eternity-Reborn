using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

internal sealed partial class MortalItemMaterializationTestContext
{
    internal async Task BuildMortalBootstrapAsync()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 1,
            characterDescription: "Тестовый смертный персонаж.",
            worldDescription: "Нейтральный тестовый смертный мир.",
            startingCircumstances: "Начало тестовой смертной жизни.",
            createdAtUtc: DateTimeOffset.Parse("2026-08-11T00:00:00Z"));

        foreach (var (path, root) in files)
            await WriteJsonAsync(path, root);
    }

    internal async Task SeedMortalPlayerResourcesAsync(int turn = 41)
    {
        var resources = ResourceBootstrapStateBuilder.BuildMortalPlayer(
            incarnationNumber: 1,
            turn,
            permanentStrength: 10,
            permanentConstitution: 10,
            permanentIntelligence: 10,
            permanentWisdom: 10,
            permanentFaith: 10);
        if (!resources.IsValid)
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, resources.Issues));
        }

        await WriteJsonAsync(
            ResourceMaterializationContract.DefinitionsPath,
            System.Text.Json.Nodes.JsonNode.Parse(resources.Definitions!.ToCanonicalJson())!);
        await WriteJsonAsync(
            ResourceMaterializationContract.StatePath,
            System.Text.Json.Nodes.JsonNode.Parse(resources.State!.ToCanonicalJson())!);
        await WriteJsonAsync(
            ResourceMaterializationContract.HistoryPath,
            System.Text.Json.Nodes.JsonNode.Parse(resources.History!.ToCanonicalJson())!);
    }
}
