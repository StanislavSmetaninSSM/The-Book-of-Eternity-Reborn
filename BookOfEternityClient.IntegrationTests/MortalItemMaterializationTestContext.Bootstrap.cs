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
}
