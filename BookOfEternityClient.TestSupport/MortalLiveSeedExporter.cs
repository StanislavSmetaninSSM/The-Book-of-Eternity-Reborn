namespace BookOfEternityClient.Tests;

// Test-only initial setup. Never an accepted GM turn or a restoration API.
internal static class MortalLiveSeedExporter
{
    internal const int SetupTurn = 41;
    internal const string StoryPath = "stories/mortal_life_2.jsonl";
    internal static Task ExportAsync(string basePath, string exampleSessionPath, string manifestPath)
        => throw new InvalidOperationException("Seeded live Mortal exporter is not implemented.");
}
