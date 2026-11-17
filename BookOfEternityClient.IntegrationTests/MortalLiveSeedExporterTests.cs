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
}
