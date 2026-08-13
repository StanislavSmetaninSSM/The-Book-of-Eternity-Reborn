using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "FullValidation")]
public sealed class PendingTurnSnapshotTests
{
    [Fact]
    public void MortalItemIdentityAndCarrierFiles_AreRegisteredAcrossCanonicalRollbackContours()
    {
        var governedPaths = new[]
        {
            MortalItemIdentityState.StatePath,
            InventoryEquipmentService.ItemsPath,
            NpcCoreChangesContract.NpcCorePath,
            StorageTransportMoveService.CurrentLocationPath,
            StorageTransportMoveService.VehiclesPath
        };

        foreach (var path in governedPaths)
        {
            Assert.Contains(
                path,
                CanonicalStateNormalizer.CanonicalAccumulatedFiles,
                StringComparer.OrdinalIgnoreCase);
            Assert.Contains(
                path,
                CanonicalStateNormalizer.NormalizerBackupInputFiles,
                StringComparer.OrdinalIgnoreCase);
            Assert.Contains(
                path,
                CanonicalStateNormalizer.NormalizerRollbackTrackedFiles,
                StringComparer.OrdinalIgnoreCase);
            Assert.Contains(
                path,
                QteSceneService.BrowserTransactionRollbackPaths,
                StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void MortalLocationBootstrapFiles_AreRegisteredAcrossCanonicalRollbackContours()
    {
        var governedPaths = new[]
        {
            MortalLocationMaterializationContract.WorldMapPath,
            MortalLocationMaterializationContract.CurrentLocationPath,
            MortalLocationIdentityState.StatePath,
            "game_state/control/mortal_bootstrap_scaffold.json"
        };

        foreach (var path in governedPaths)
        {
            Assert.Contains(
                path,
                CanonicalStateNormalizer.CanonicalAccumulatedFiles,
                StringComparer.OrdinalIgnoreCase);
            Assert.Contains(
                path,
                CanonicalStateNormalizer.NormalizerBackupInputFiles,
                StringComparer.OrdinalIgnoreCase);
            Assert.Contains(
                path,
                CanonicalStateNormalizer.NormalizerRollbackTrackedFiles,
                StringComparer.OrdinalIgnoreCase);
            Assert.Contains(
                path,
                QteSceneService.BrowserTransactionRollbackPaths,
                StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task CaptureValidatedPendingSnapshot_IncludesExistingIdentityAndCarrierFiles()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        var governedPaths = new[]
        {
            MortalItemIdentityState.StatePath,
            InventoryEquipmentService.ItemsPath,
            NpcCoreChangesContract.NpcCorePath,
            StorageTransportMoveService.CurrentLocationPath,
            StorageTransportMoveService.VehiclesPath
        };
        foreach (var path in governedPaths)
        {
            await context.WriteJsonAsync(
                path,
                path == MortalItemIdentityState.StatePath
                    ? MortalItemIdentityState.CreateEmptyRoot()
                    : new JsonObject());
        }

        await context.CaptureValidatedPendingSnapshotAsync();

        var manifest = (await context.ReadJsonAsync(
            "game_state/control/pending_turn_snapshot.json"))!.AsObject();
        foreach (var path in governedPaths)
        {
            Assert.Equal(
                $"game_state/control/pending_turn_snapshot/{path}",
                manifest["files"]![path]!.GetValue<string>());
            Assert.Contains(
                manifest["rollbackBaselineFiles"]!.AsArray(),
                node => string.Equals(
                    node?.GetValue<string>(),
                    path,
                    StringComparison.Ordinal));
        }
        Assert.True(context.FileSystem.FileExists(
            "game_state/control/pending_turn_snapshot.authority.json"));
    }

    [Fact]
    public async Task CaptureValidatedPendingSnapshot_IncludesMortalLocationBootstrapAuthorityFiles()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        var governedFiles = new Dictionary<string, JsonObject>(StringComparer.Ordinal)
        {
            [MortalLocationMaterializationContract.WorldMapPath] = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["realm"] = "mortal_world",
                ["locations"] = new JsonArray(),
                ["links"] = new JsonArray()
            },
            [MortalLocationMaterializationContract.CurrentLocationPath] = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["realm"] = "mortal_world",
                ["locationId"] = null,
                ["state"] = "pending_materialization"
            },
            [MortalLocationIdentityState.StatePath] = MortalLocationIdentityState.CreateEmptyRoot(),
            [MortalBootstrapLocationScaffold.StatePath] = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["purpose"] = "fresh_mortal_world_bootstrap",
                ["locationMaterializationRequest"] =
                    MortalBootstrapLocationScaffold.CreatePendingRequest(
                        1,
                        "session_mortal_location_materialization",
                        "request_mortal_location_materialization",
                        42)
            }
        };
        foreach (var pair in governedFiles)
            await context.WriteJsonAsync(pair.Key, pair.Value);

        await context.CaptureValidatedPendingSnapshotAsync();

        var manifest = (await context.ReadJsonAsync(
            "game_state/control/pending_turn_snapshot.json"))!.AsObject();
        foreach (var path in governedFiles.Keys)
        {
            Assert.Equal(
                $"game_state/control/pending_turn_snapshot/{path}",
                manifest["files"]![path]!.GetValue<string>());
            Assert.Contains(
                manifest["rollbackBaselineFiles"]!.AsArray(),
                node => string.Equals(node?.GetValue<string>(), path, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task MortalBootstrap_WritesNeutralCurrentSchemaLocationRoots()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();

        await context.BuildMortalBootstrapAsync();

        Assert.True(JsonNode.DeepEquals(
            MortalItemIdentityState.CreateEmptyRoot(),
            await context.ReadJsonAsync(MortalItemIdentityState.StatePath)));
        Assert.True(JsonNode.DeepEquals(
            MortalLocationIdentityState.CreateEmptyRoot(),
            await context.ReadJsonAsync(MortalLocationIdentityState.StatePath)));
        Assert.True(JsonNode.DeepEquals(
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["realm"] = "mortal_world",
                ["locations"] = new JsonArray(),
                ["links"] = new JsonArray()
            },
            await context.ReadJsonAsync(MortalLocationMaterializationContract.WorldMapPath)));
        Assert.True(JsonNode.DeepEquals(
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["realm"] = "mortal_world",
                ["locationId"] = null,
                ["state"] = "pending_materialization"
            },
            await context.ReadJsonAsync(MortalLocationMaterializationContract.CurrentLocationPath)));
    }
}
