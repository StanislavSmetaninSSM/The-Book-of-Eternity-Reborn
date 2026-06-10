using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class FileSystemExampleFixtureIntegrityTests
{
    [Fact]
    public void GameSessionFixtureJsonFiles_AreNonEmptyAndParseable()
    {
        var jsonFiles = Directory
            .EnumerateFiles(TestRepoPaths.BaseSessionRoot, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(jsonFiles);

        var invalidFiles = new List<string>();
        foreach (var jsonFile in jsonFiles)
        {
            var content = File.ReadAllText(jsonFile);
            if (string.IsNullOrWhiteSpace(content))
            {
                invalidFiles.Add($"{ToFixtureRelativePath(jsonFile)}: empty file");
                continue;
            }

            try
            {
                using var _ = JsonDocument.Parse(content);
            }
            catch (JsonException ex)
            {
                invalidFiles.Add($"{ToFixtureRelativePath(jsonFile)}: {ex.Message}");
            }
        }

        Assert.True(
            invalidFiles.Count == 0,
            "FileSystemExample/game_session must not contain empty or malformed JSON files. Invalid files:" +
            Environment.NewLine + string.Join(Environment.NewLine, invalidFiles));
    }

    [Fact]
    public void GameSessionFixture_DoesNotTrackStalePendingTurnSnapshots()
    {
        var pendingSnapshotArtifacts = Directory
            .EnumerateFileSystemEntries(TestRepoPaths.BaseSessionRoot, "pending_turn_snapshot*", SearchOption.AllDirectories)
            .Select(ToFixtureRelativePath)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            pendingSnapshotArtifacts.Length == 0,
            "FileSystemExample/game_session must remain free of stale pending_turn_snapshot artifacts. Found:" +
            Environment.NewLine + string.Join(Environment.NewLine, pendingSnapshotArtifacts));
    }

    private static string ToFixtureRelativePath(string fullPath)
    {
        return Path.GetRelativePath(TestRepoPaths.BaseSessionRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/');
    }
}
