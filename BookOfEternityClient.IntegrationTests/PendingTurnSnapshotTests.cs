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
    public async Task MortalBootstrap_WritesOnlyEmptyCurrentSchemaIdentityIndex()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();

        await context.BuildMortalBootstrapAsync();

        Assert.True(JsonNode.DeepEquals(
            MortalItemIdentityState.CreateEmptyRoot(),
            await context.ReadJsonAsync(MortalItemIdentityState.StatePath)));
    }
}
