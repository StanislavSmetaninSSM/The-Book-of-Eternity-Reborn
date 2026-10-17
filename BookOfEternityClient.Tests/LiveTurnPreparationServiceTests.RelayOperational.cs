using System.Text.Json;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class LiveTurnPreparationServiceTests
{
    [Fact]
    public async Task RelayOperationalRecords_ActualLivePreparationExcludesOnlyOperationalSnapshotMembers()
    {
        using var cleanup = new GmRelayLongSessionTests.OwnedRoot(_rootPath, Console.WriteLine);
        await WriteCanonicalAndGeneratedHarnessFilesAsync();
        string[] paths = ["game_state/control/gm_prompt_delivery.json", "game_state/control/gm_relay_apply_receipt.json"];
        foreach (var path in paths) await _fs.WriteFileAtomicAsync(path, "{\"schemaVersion\":1,\"ownedOperationalMarker\":true}");
        const string neighbor = "game_state/control/relay_ordinary_neighbor.json";
        await _fs.WriteFileAtomicAsync(neighbor, "{\"ordinaryGameplayNeighbor\":true}");
        await new LiveTurnPreparationService(_fs).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "owned-relay-session", RequestId = "owned-relay-request", TurnNumber = 2,
            PlayerAction = "Осмотреть спокойную комнату.", PreGeneratedDices1d20 = [14, 8, 17]
        });
        var manifest = JsonSerializer.Deserialize<LiveTurnPendingSnapshotManifest>(
            (await _fs.ReadFileAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!, LiveTurnPreparationService.ManifestJsonOptions)!;
        Assert.Contains(neighbor, manifest.Files.Keys);
        Assert.Contains(neighbor, manifest.RollbackBaselineFiles);
        Assert.Equal("owned-relay-request", manifest.RequestId);
        foreach (var path in paths)
        {
            Assert.True(_fs.FileExists(path));
            Assert.DoesNotContain(path, manifest.Files.Keys);
            Assert.DoesNotContain(path, manifest.RollbackBaselineFiles);
        }
    }
}
