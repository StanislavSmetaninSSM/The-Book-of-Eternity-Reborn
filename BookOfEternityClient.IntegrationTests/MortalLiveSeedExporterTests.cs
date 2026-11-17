using System.Security.Cryptography;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalLiveSeedExporterTests
{
    [Fact]
    public async Task InitialExportIsValidatedCoherentSetupOnlyAndCannotOverwrite()
    {
        var root = Path.Combine(TestDiagnosticPaths.Root, "mortal-live-seed", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var basePath = Path.Combine(root, "play");
            var manifest = Path.Combine(root, "seed-manifest.json");
            await MortalLiveSeedExporter.ExportAsync(basePath, TestRepoPaths.BaseSessionRoot, manifest);
            var fs = new FileSystemManager(basePath, NullLogger<FileSystemManager>.Instance);
            var validator = new ValidationService(fs, NullLogger<ValidationService>.Instance);
            Assert.Empty(await validator.ValidateGameStateAsync());
            var receipt = JsonNode.Parse(await File.ReadAllTextAsync(manifest))!;
            Assert.Equal("seeded-live-mortal", receipt["kind"]!.GetValue<string>());
            Assert.Equal(41, receipt["setupTurn"]!.GetValue<int>());
            Assert.False(receipt["acceptedGmTurn"]!.GetValue<bool>());
            var chat = JsonNode.Parse((await fs.ReadFileAsync("game_state/history/chat_log.json"))!)!;
            Assert.Equal(receipt["sessionId"]!.GetValue<string>(), chat["sessionId"]!.GetValue<string>());
            var story = (await fs.ReadFileAsync(MortalLiveSeedExporter.StoryPath))!;
            var entry = JsonNode.Parse(Assert.Single(story.Split('\n', StringSplitOptions.RemoveEmptyEntries)))!;
            Assert.Equal(41, entry["turn"]!.GetValue<int>());
            Assert.Equal("[TEST SETUP ONLY]", entry["player"]!.GetValue<string>());
            Assert.Null(entry["requestId"]);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(story.TrimEnd('\n')))).ToLowerInvariant(),
                receipt["setupStoryEntrySha256"]!.GetValue<string>());
            var allStories = Directory.GetFiles(Path.Combine(fs.GameSessionPath, "stories"), "*", SearchOption.AllDirectories);
            Assert.Single(allStories);
            var allTurns = allStories.SelectMany(path => File.ReadAllLines(path)).Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => JsonNode.Parse(line)!["turn"]!.GetValue<int>()).ToArray();
            Assert.Equal(41, allTurns.Max());
            Assert.False(Directory.Exists(Path.Combine(basePath, ".boe_runtime/gm-runs")));
            Assert.False(Directory.Exists(Path.Combine(fs.GameSessionPath, "queue")));
            foreach (var directory in new[] { "ready", "input", "output" })
            {
                var path = Path.Combine(fs.GameSessionPath, directory);
                Assert.False(File.Exists(path));
                if (Directory.Exists(path)) Assert.Empty(Directory.EnumerateFileSystemEntries(path));
            }

            var soul = JsonNode.Parse((await fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!;
            Assert.Equal("Mortal World", soul["currentRealm"]!.GetValue<string>());
            Assert.Equal(2, soul["currentIncarnation"]!.GetValue<int>());
            foreach (var path in new[] { "input/turn_request.json", "game_state/control/pending_turn_snapshot.json",
                "game_state/control/pending_turn_snapshot.authority.json", "ready/turn_complete.json",
                "game_state/control/gm_bridge_status.json" }) Assert.False(fs.FileExists(path), path);
            var before = Inventory();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                MortalLiveSeedExporter.ExportAsync(basePath, TestRepoPaths.BaseSessionRoot, manifest));
            Assert.Equal(before, Inventory());
            string[] Inventory() => Directory.GetFiles(root, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal)
                .Select(p => Path.GetRelativePath(root, p) + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)))).ToArray();
        }
        finally { Directory.Delete(root, recursive: true); }
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OccupiedBaseOrManifestRefusesBeforeAnyCopy(bool occupiedBase)
    {
        var root = Path.Combine(TestDiagnosticPaths.Root, "mortal-live-seed-refusal", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var basePath = Path.Combine(root, "play");
            var manifest = Path.Combine(root, "seed-manifest.json");
            if (occupiedBase) Directory.CreateDirectory(basePath);
            var sentinel = occupiedBase ? Path.Combine(basePath, "sentinel.bin") : manifest;
            await File.WriteAllBytesAsync(sentinel, [0, 255, 9, 10]);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                MortalLiveSeedExporter.ExportAsync(basePath, TestRepoPaths.BaseSessionRoot, manifest));
            Assert.Equal(new byte[] { 0, 255, 9, 10 }, await File.ReadAllBytesAsync(sentinel));
            Assert.Single(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
            if (occupiedBase) Assert.False(File.Exists(manifest));
            else Assert.False(Directory.Exists(basePath));
        }
        finally { Directory.Delete(root, true); }
    }
}
