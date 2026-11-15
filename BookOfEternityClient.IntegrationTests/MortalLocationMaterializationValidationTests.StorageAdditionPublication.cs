using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalLocationMaterializationValidationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StorageAdditionPublication_PersistsHistoryAccessAndRejectsRepeatedBirth(bool access)
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        var arrangement = await ArrangeStorageAdditionPublicationAsync(context, access);
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);

        var issues = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateAsync(
            context.FileSystem, context.Normalizer, context.Validator, arrangement.Backups);

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var map = (await context.ReadJsonAsync(MortalLocationMaterializationContract.WorldMapPath))!;
        var location = Assert.Single(map["locations"]!.AsArray())!;
        var metadata = Assert.Single(location["locationStorages"]!.AsArray())!;
        Assert.Equal("storage_added", metadata["storageId"]!.GetValue<string>());
        Assert.Equal(access, metadata["hasFullAccess"]!.GetValue<bool>());
        Assert.True(JsonNode.DeepEquals(arrangement.Location["materializationReceipt"], location["materializationReceipt"]));
        Assert.True(JsonNode.DeepEquals(arrangement.Location["materialization"], location["materialization"]));
        Assert.Null(metadata["contents"]);
        Assert.Null(map["worldMapUpdates"]);
        var current = (await context.ReadJsonAsync(MortalLocationMaterializationContract.CurrentLocationPath))!;
        Assert.Equal(access, Assert.Single(current["locationStorages"]!.AsArray())!["hasFullAccess"]!.GetValue<bool>());
        var index = (await context.ReadJsonAsync(MortalLocationIdentityState.StatePath))!;
        var transition = Assert.Single(index["locationEntries"]![0]!["transitions"]!.AsArray())!;
        Assert.Equal("storage_addition", transition["kind"]!.GetValue<string>());
        Assert.Equal(43, transition["turn"]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(metadata, transition["afterState"]));
        var effective = (await context.ReadJsonAsync(MortalStorageAccessProjection.StatePath))!;
        var entry = Assert.Single(effective["entries"]!.AsArray())!;
        Assert.Equal(MortalLocationTestFixture.LocationId, entry["locationId"]!.GetValue<string>());
        Assert.Equal("storage_added", entry["storageId"]!.GetValue<string>());
        Assert.Equal(access, entry["hasFullAccess"]!.GetValue<bool>());
        var moves = await StorageTransportMoveService.ReadStorageMoveContextAsync(context.FileSystem);
        Assert.True(moves.Success, moves.Message);
        Assert.Equal(access, moves.Storages.Any(s => s.StorageId == "storage_added"));

        // The next independent turn cannot replay birth over a retained child identity.
        await context.CaptureValidatedPendingSnapshotAsync(turn: 44);
        await context.WriteRawTurnStateAsync(null, EmptyStorageAddition(MortalLocationTestFixture.LocationId));
        var before = await context.CaptureBytesAsync(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
        var repeated = await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync();
        Assert.Contains(repeated, issue => issue.Code == "mortal_location_storage_addition_identity_reused");
        var after = await context.CaptureBytesAsync(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
        foreach (var pair in before) Assert.Equal(pair.Value, after[pair.Key]);
    }

    [Fact]
    public async Task StorageAdditionPublication_AccessWriteCutRestoresAllTrackedPaths()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        using var owned = new CleanupOwnedFixture(context.RootPath, output.WriteLine);
        var arrangement = await ArrangeStorageAdditionPublicationAsync(context, access: false);
        var captured = await context.CaptureBytesAsync(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
        var before = captured.ToDictionary(p => p.Key,
            p => p.Value == null ? null : Convert.FromBase64String(p.Value), StringComparer.Ordinal);
        var generation = CleanupPublicationCut.ReadOptional(context.FileSystem.SessionGenerationPath);
        var cut = new KnownRollbackPublicationCut(context.FileSystem.ResolvePath(MortalStorageAccessProjection.StatePath));
        cut.Attach(context.FileSystem);
        context.CurrentPublicationObserver = cut.Hooks.LocalPublicationObserver;

        var failure = await Record.ExceptionAsync(() => AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateAsync(
            context.FileSystem, context.Normalizer, context.Validator, arrangement.Backups));

        cut.AssertRestored(before, generation, failure, output.WriteLine);
        var writeFailure = Assert.IsType<CanonicalStateWriteException>(failure);
        Assert.Equal(MortalStorageAccessProjection.StatePath, writeFailure.RelativePath);
        Assert.Same(cut.Failure, writeFailure.InnerException);
    }

    [Fact]
    public async Task StorageAdditionPublication_NonemptyBirthRefusedWithoutWrites()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        await ArrangeStorageAdditionPublicationAsync(context, access: true);
        var raw = (await context.ReadJsonAsync(MortalLocationMaterializationContract.WorldMapPath))!;
        raw["worldMapUpdates"]!["storagesToAdd"]![0]!["storage"]!["contents"] =
            new JsonArray(new JsonObject { ["itemId"] = "not_admitted" });
        await context.WriteJsonAsync(MortalLocationMaterializationContract.WorldMapPath, raw);
        var before = await context.CaptureBytesAsync(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);

        var issues = await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        Assert.Contains(issues, issue => issue.Code == "mortal_location_storage_addition_contents_invalid" &&
            issue.FilePath == "worldMapUpdates.storagesToAdd[0].storage.contents");
        var after = await context.CaptureBytesAsync(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
        foreach (var pair in before) Assert.Equal(pair.Value, after[pair.Key]);
    }

    [Fact]
    public async Task StorageAdditionPublication_InvalidRawNameHasBoundedIndexedRepair()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        await ArrangeStorageAdditionPublicationAsync(context, access: false);
        var raw = (await context.ReadJsonAsync(MortalLocationMaterializationContract.WorldMapPath))!;
        raw["worldMapUpdates"]!["storagesToAdd"]![0]!["storage"]!["name"] = "";
        await context.WriteJsonAsync(MortalLocationMaterializationContract.WorldMapPath, raw);
        var issues = await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync();
        Assert.Contains(issues, issue => issue.Code == "mortal_location_storage_semantic_invalid");
        Assert.False(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(issues));
        var packet = Assert.Single(MortalLocationRepairPacketBuilder.Build(issues));
        Assert.Equal("worldMapUpdates.storagesToAdd[0]", packet.RawCoordinate);
        Assert.Equal(new[] { MortalLocationMaterializationContract.WorldMapPath }, packet.TargetFiles);
    }

    private static async Task<(JsonObject Location, Dictionary<string, string> Backups)> ArrangeStorageAdditionPublicationAsync(
        MortalLocationMaterializationTestContext context, bool access)
    {
        var location = MortalLocationTestFixture.CreateCanonicalLocation();
        await context.WritePreTurnCanonicalStateAsync(location);
        await context.WriteJsonAsync(MortalItemIdentityState.StatePath, MortalItemIdentityState.CreateEmptyRoot());
        await context.WriteJsonAsync(InventoryEquipmentService.ItemsPath,
            new JsonObject { ["items"] = new JsonArray(), ["equipment"] = new JsonObject() });
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var updates = EmptyStorageAddition(MortalLocationTestFixture.LocationId);
        updates["storagesToAdd"]![0]!["storage"]!["hasFullAccess"] = access;
        await context.WriteRawTurnStateAsync(null, updates);
        var backups = new[] { MortalLocationMaterializationContract.WorldMapPath,
            MortalLocationMaterializationContract.CurrentLocationPath, MortalLocationIdentityState.StatePath,
            MortalStorageAccessProjection.StatePath }.ToDictionary(path => path,
                path => "game_state/control/pending_turn_snapshot/" + path, StringComparer.Ordinal);
        return (location, backups);
    }
}
