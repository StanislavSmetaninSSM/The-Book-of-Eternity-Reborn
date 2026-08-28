using System.Text.Json;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task WoundSnapshotAndRollback_TracksEveryFoundationalAuthorityPath()
    {
        var baselineBytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var path in WoundAcceptedTurnSnapshotContract.RequiredPaths)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(
                $"{{\"baselinePath\":{JsonSerializer.Serialize(path)}}}");
            baselineBytes.Add(path, bytes);
            await _fs.WriteFileAtomicBytesAsync(path, bytes);
        }

        var engine = CreateGameEngine();
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "wound-foundational-authority");
        var request = new TurnRequest
        {
            SessionId = "session-wound-foundational-authority",
            RequestId = "request-wound-foundational-authority",
            TurnNumber = 42,
            PlayerAction = "capture exact wound publication authority",
            Timestamp = "2026-08-28T00:00:00Z",
            ProgressionControl = new ProgressionControl
            {
                CurrentRealm = "Mortal World"
            }
        };

        await InvokePrivateTaskResultAsync(
            engine,
            "CreateCanonicalBaselineSnapshotAsync",
            request,
            rollbackSnapshot,
            "wound-foundational-authority");

        var manifestJson = await _fs.ReadFileAsync(
            "game_state/control/pending_turn_snapshot.json");
        var manifest = JsonSerializer.Deserialize<PendingTurnSnapshotManifestPayload>(
            manifestJson!,
            SnapshotHashJsonOpts)!;
        foreach (var path in WoundAcceptedTurnSnapshotContract.RequiredPaths)
        {
            Assert.Contains(path, manifest.RollbackBaselineFiles);
            Assert.True(manifest.Files.ContainsKey(path));
            Assert.True(manifest.SnapshotFileHashes.ContainsKey(path));
            Assert.True(manifest.RollbackBackups.ContainsKey(path));
            await _fs.WriteFileAtomicAsync(path, "{\"mutated\":true}");
        }

        await InvokePrivateTaskAsync(
            engine,
            "RestorePreTurnBackup",
            rollbackSnapshot);

        foreach (var (path, bytes) in baselineBytes)
        {
            Assert.Equal(bytes, await _fs.ReadFileBytesAsync(path));
        }
    }
}
