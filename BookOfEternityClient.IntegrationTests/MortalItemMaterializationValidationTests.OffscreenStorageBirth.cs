using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalItemMaterializationValidationTests
{
    [Trait("Category", "FullValidation")]
    public sealed class OffscreenStorageBirth(ITestOutputHelper output)
    {
        private const string MapPath = MortalLocationMaterializationContract.WorldMapPath;
        private const string A = MortalItemMaterializationTestContext.OffscreenBirthCurrentId;
        private const string B = MortalItemMaterializationTestContext.OffscreenBirthTargetId;
        private const string Storage = MortalItemMaterializationTestContext.OffscreenBirthStorageId;

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task OffscreenBirthPublishesPermanentCargoAndSourceBoundHistory(bool measured)
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            await context.ArrangeOffscreenStorageBirthAsync(measured);
            var originalMap = await OriginalMapAsync(context);
            await AdmitAsync(context);
            var first = await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(context.FileSystem);
            Assert.NotNull(first);
            var allocated = await AllocatedRootAsync(context);
            Assert.NotNull(allocated);
            await AdmitAsync(context);
            var repeated = await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(context.FileSystem);
            Assert.NotNull(repeated);
            Assert.Equal(first.Binding.SnapshotToken, repeated.Binding.SnapshotToken);
            Assert.Equal(allocated, await AllocatedRootAsync(context));
            await context.NormalizeAcceptedTurnAsync();
            var items = await PublishedItemsAsync(context);
            Assert.Equal(measured ? 3 : 1, items.Count);
            Assert.Equal(allocated, items[0]!["itemId"]!.GetValue<string>());
            Assert.All(items.OfType<JsonObject>(), item => Assert.NotNull(item[MortalItemMaterializationContract.ReceiptProperty]));
            var itemIndex = (await context.ReadJsonAsync(MortalItemIdentityState.StatePath))!.AsObject();
            Assert.Equal(items.Count, itemIndex["entries"]!.AsArray().Count);
            Assert.All(itemIndex["entries"]!.AsArray().OfType<JsonObject>(), entry =>
            {
                Assert.Equal("location_storage", entry["currentCarrier"]!["kind"]!.GetValue<string>());
                Assert.Equal(B, entry["currentCarrier"]!["ownerId"]!.GetValue<string>());
                Assert.Equal(Storage, entry["currentCarrier"]!["containerId"]!.GetValue<string>());
            });
            var current = (await context.ReadJsonAsync(StorageTransportMoveService.CurrentLocationPath))!.AsObject();
            Assert.Equal(A, current["locationId"]!.GetValue<string>());
            Assert.Empty(current["locationStorages"]!.AsArray());
            var map = (await context.ReadJsonAsync(MapPath))!.AsObject();
            foreach (var original in originalMap["locations"]!.AsArray().OfType<JsonObject>())
            {
                var retained = map["locations"]!.AsArray().OfType<JsonObject>().Single(location => location["locationId"]!.GetValue<string>() == original["locationId"]!.GetValue<string>());
                Assert.True(JsonNode.DeepEquals(original["materialization"], retained["materialization"]));
                Assert.True(JsonNode.DeepEquals(original["materializationReceipt"], retained["materializationReceipt"]));
            }
            var metadata = Assert.Single(map["locations"]!.AsArray().OfType<JsonObject>(), l => l["locationId"]!.GetValue<string>() == B)["locationStorages"]![0]!;
            Assert.Null(metadata["contents"]); Assert.Null(metadata["cargoMeasurements"]); Assert.Null(metadata["cargoBirthEvidence"]);
            var transition = await BirthTransitionAsync(context);
            var evidence = transition["afterState"]!["cargoBirthEvidence"]!.AsObject();
            Assert.Equal("worldMapUpdates.storagesToAdd[0]", transition["operationRef"]!.GetValue<string>());
            Assert.Equal(B, evidence["locationId"]!.GetValue<string>());
            Assert.Equal(Storage, evidence["storageId"]!.GetValue<string>());
            var manifest = (await context.ReadJsonAsync("game_state/control/pending_turn_snapshot.json"))!.AsObject();
            foreach (var field in new[] { "sessionId", "requestId" })
                Assert.Equal(manifest[field]!.GetValue<string>(), evidence["sourceAuthority"]![field]!.GetValue<string>());
            Assert.Equal(first.Binding.SnapshotToken, evidence["sourceAuthority"]!["snapshotToken"]!.GetValue<string>());
            Assert.Equal(42, evidence["sourceTurn"]!.GetValue<int>());
            var row = Assert.Single(evidence["cargoMeasurements"]!.AsArray())!;
            Assert.Equal(allocated, row["itemId"]!.GetValue<string>());
            Assert.Equal(1d, row["externalVolumeDm3"]!.GetValue<double>());
            if (measured)
            {
                Assert.Single(row["measuredConfiguration"]!["contents"]!.AsArray());
                Assert.Single(row["measuredConfiguration"]!["contents"]![0]!["contents"]!.AsArray());
            }
            var access = (await context.ReadJsonAsync(MortalStorageAccessProjection.StatePath))!.AsObject();
            Assert.Empty(MortalStorageAccessProjection.ValidateCanonical(map, access));
            Assert.True(Assert.Single(access["entries"]!.AsArray())!["hasFullAccess"]!.GetValue<bool>());
        }

        [Theory]
        [InlineData(0, 1d, "capacity")]
        [InlineData(1, 0.5d, "volume")]
        public async Task CapacityRefusalIdentifiesRawStorageFieldWithoutMutation(int capacity, double volume, string field)
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            await context.ArrangeOffscreenStorageBirthAsync(capacity: capacity, volume: volume);
            var before = await context.CaptureExactBytesAsync(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
            var issues = await CollectRawIssuesAsync(context);
            await context.AssertExactBytesAsync(before);
            Assert.Contains(issues, issue => issue.Code == "mortal_location_storage_cargo_capacity_invalid" &&
                issue.FilePath.EndsWith("worldMapUpdates.storagesToAdd[0].storage." + field, StringComparison.Ordinal));
            Assert.False(await AcceptedMechanicsAuthorityTestProbe.HasItemsAsync(context.FileSystem));
        }

        [Fact]
        public async Task WrongSourceCoordinateTargetsRawCommandRatherThanProjectedCompanion()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            await context.ArrangeOffscreenStorageBirthAsync();
            var raw = (await context.ReadJsonAsync(MapPath))!.AsObject();
            raw["worldMapUpdates"]!["storagesToAdd"]![0]!["storage"]!["contents"]![0]!["materialization"]!["sourceAuthority"]!["authorityId"] = B + ":wrong_storage";
            await context.WriteJsonAsync(MapPath, raw);
            var before = await context.CaptureExactBytesAsync(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
            var issues = await CollectRawIssuesAsync(context);
            await context.AssertExactBytesAsync(before);
            Assert.Contains(issues, issue => issue.Code == "mortal_item_materialization_route_authority_mismatch" &&
                issue.FilePath.Contains("worldMapUpdates.storagesToAdd[0].storage.contents[0]", StringComparison.Ordinal));
            Assert.DoesNotContain(issues, issue => issue.Code == "mortal_item_materialization_route_authority_mismatch" &&
                issue.FilePath.StartsWith(MortalLocationStorageContentsState.StatePath, StringComparison.Ordinal));
            var packet = Assert.Single(MortalItemRepairPacketBuilder.Build(issues));
            Assert.Contains(MapPath, packet.TargetFiles);
            Assert.DoesNotContain(MortalLocationStorageContentsState.StatePath, packet.TargetFiles);
            var locationPackets = MortalLocationRepairPacketBuilder.Build(issues);
            var obligation = MortalLocationRepairRetryPolicy.Capture(locationPackets);
            Assert.Single(obligation);
            Assert.True(MortalLocationRepairRetryPolicy.HasExactResubmission(obligation, null, raw));
            var changedParent = raw.DeepClone().AsObject();
            changedParent["worldMapUpdates"]!["storagesToAdd"]![0]!["targetLocationId"] = A;
            Assert.False(MortalLocationRepairRetryPolicy.HasExactResubmission(obligation, null, changedParent));
            var changedChild = raw.DeepClone().AsObject();
            changedChild["worldMapUpdates"]!["storagesToAdd"]![0]!["storage"]!["storageId"] = "other_storage";
            Assert.False(MortalLocationRepairRetryPolicy.HasExactResubmission(obligation, null, changedChild));
        }

        [Fact]
        public async Task ReachedOffscreenContentsPublicationCutRestoresEveryTrackedPath()
        {
            KnownRollbackPublicationCut? cut = null;
            await using var context = await MortalItemMaterializationTestContext.CreateAsync(new FileSystemManagerHooks
                { LocalPublicationObserver = (phase, index) => cut?.Hooks.LocalPublicationObserver?.Invoke(phase, index) });
            await context.ArrangeOffscreenStorageBirthAsync();
            await AdmitAsync(context);
            var before = await context.CaptureExactBytesAsync(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
            var generation = CleanupPublicationCut.ReadOptional(context.FileSystem.SessionGenerationPath);
            cut = new KnownRollbackPublicationCut(context.FileSystem.ResolvePath(MortalLocationStorageContentsState.StatePath));
            cut.Attach(context.FileSystem);
            var failure = await Record.ExceptionAsync(context.NormalizeAcceptedTurnAsync);
            cut.AssertRestored(before, generation, failure, output.WriteLine);
            var writeFailure = Assert.IsType<CanonicalStateWriteException>(failure);
            Assert.Equal(MortalLocationStorageContentsState.StatePath, writeFailure.RelativePath);
            Assert.Same(cut.Failure, writeFailure.InnerException);
        }

        [Fact]
        public async Task SourceDriftIsRefusedBeforePublishingOffscreenBirth()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            await context.ArrangeOffscreenStorageBirthAsync();
            await AdmitAsync(context);
            var common = await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(context.FileSystem);
            Assert.NotNull(common);
            var sources = common.Binding.BeforeImages;
            foreach (var path in new[] { MapPath, MortalLocationIdentityState.StatePath,
                         MortalLocationStorageContentsState.StatePath, MortalStorageAccessProjection.StatePath })
                Assert.Contains(path, sources.Keys);
            var raw = (await context.ReadJsonAsync(MapPath))!.AsObject();
            raw["worldMapUpdates"]!["storagesToAdd"]![0]!["storage"]!["description"] = "Changed after admission";
            await context.WriteJsonAsync(MapPath, raw);
            var before = await context.CaptureExactBytesAsync(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
            await Assert.ThrowsAnyAsync<InvalidDataException>(() => context.NormalizeAcceptedTurnWithIssuesAsync());
            await context.AssertExactBytesAsync(before);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SubsequentTravelAndExistingMovePreserveBirthEvidenceAndExplicitAccess(bool access)
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            await context.ArrangeOffscreenStorageBirthAsync(access: access);
            await AdmitAsync(context);
            await context.NormalizeAcceptedTurnAsync();
            var itemId = (await PublishedItemsAsync(context))[0]!["itemId"]!.GetValue<string>();
            var birth = await BirthTransitionAsync(context);
            Assert.DoesNotContain((await StorageTransportMoveService.ReadStorageMoveContextAsync(context.FileSystem)).Storages,
                storage => storage.StorageId == Storage);
            await context.CaptureValidatedPendingSnapshotAsync(43);
            await context.WriteJsonAsync(StorageTransportMoveService.CurrentLocationPath, new JsonObject
            {
                ["currentLocationData"] = new JsonObject { ["locationId"] = B,
                    ["lastEventsDescription"] = "Arrived at the remote storehouse.", ["currentInteractions"] = new JsonArray() }
            });
            await AdmitAsync(context);
            await context.NormalizeAcceptedTurnAsync();
            Assert.True(JsonNode.DeepEquals(birth, await BirthTransitionAsync(context)));
            var current = (await context.ReadJsonAsync(StorageTransportMoveService.CurrentLocationPath))!.AsObject();
            Assert.Equal(B, current["locationId"]!.GetValue<string>());
            Assert.Equal(itemId, current["locationStorages"]![0]!["contents"]![0]!["itemId"]!.GetValue<string>());
            var options = await StorageTransportMoveService.ReadStorageMoveContextAsync(context.FileSystem);
            Assert.True(options.Success, options.Message);
            Assert.Equal(access, options.Storages.Any(storage => storage.StorageId == Storage));
            if (!access) return;
            var offered = Assert.Single(options.Storages);
            var item = Assert.Single(offered.Contents);
            var retrieved = await StorageTransportMoveService.MoveStorageItemAsync(context.FileSystem,
                StorageTransportMoveService.DirectionRetrieve, offered.Key, item.Key);
            Assert.True(retrieved.Success, retrieved.Message);
            Assert.True(JsonNode.DeepEquals(birth, await BirthTransitionAsync(context)));
            var fresh = await StorageTransportMoveService.ReadStorageMoveContextAsync(context.FileSystem);
            var carried = Assert.Single(fresh.InventoryItems, candidate => candidate.Identity == itemId);
            var deposited = await StorageTransportMoveService.MoveStorageItemAsync(context.FileSystem,
                StorageTransportMoveService.DirectionDeposit, offered.Key, carried.Key);
            Assert.True(deposited.Success, deposited.Message);
            Assert.True(JsonNode.DeepEquals(birth, await BirthTransitionAsync(context)));
        }

        private static async Task AdmitAsync(MortalItemMaterializationTestContext context)
        {
            var issues = await CollectRawIssuesAsync(context);
            Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        }

        private static async Task<IReadOnlyList<ValidationIssue>> CollectRawIssuesAsync(MortalItemMaterializationTestContext context)
        {
            var issues = new List<ValidationIssue>();
            issues.AddRange(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
            issues.AddRange(await context.ValidateAcceptedTurnRawMaterializationAsync());
            return issues;
        }

        private static async Task<string?> AllocatedRootAsync(MortalItemMaterializationTestContext context)
        {
            var manifest = (await context.ReadJsonAsync("game_state/control/pending_turn_snapshot.json"))!.AsObject();
            return await AcceptedMechanicsAuthorityTestProbe.GetAllocatedItemIdAsync(context.FileSystem,
                manifest["sessionId"]!.GetValue<string>(), manifest["manifestPayloadHash"]!.GetValue<string>(),
                MortalItemMaterializationTestContext.OffscreenBirthRootRef);
        }

        private static async Task<JsonObject> OriginalMapAsync(MortalItemMaterializationTestContext context)
        {
            var manifest = (await context.ReadJsonAsync("game_state/control/pending_turn_snapshot.json"))!.AsObject();
            return (await context.ReadJsonAsync(manifest["files"]![MapPath]!.GetValue<string>()))!.AsObject();
        }

        private static async Task<JsonArray> PublishedItemsAsync(MortalItemMaterializationTestContext context)
        {
            var root = (await context.ReadJsonAsync(MortalLocationStorageContentsState.StatePath))!.AsObject();
            Assert.Empty(MortalLocationStorageContentsState.Parse(root).Issues);
            return Assert.Single(root["entries"]!.AsArray().OfType<JsonObject>(),
                entry => entry["locationId"]!.GetValue<string>() == B && entry["storageId"]!.GetValue<string>() == Storage)["contents"]!.AsArray();
        }

        private static async Task<JsonObject> BirthTransitionAsync(MortalItemMaterializationTestContext context)
        {
            var index = (await context.ReadJsonAsync(MortalLocationIdentityState.StatePath))!.AsObject();
            Assert.Empty(MortalLocationIdentityState.Parse(index).Issues);
            var entry = Assert.Single(index["locationEntries"]!.AsArray().OfType<JsonObject>(), e => e["locationId"]!.GetValue<string>() == B);
            return Assert.Single(entry["transitions"]!.AsArray().OfType<JsonObject>(), t =>
                t["kind"]!.GetValue<string>() == "storage_addition" && t["childId"]!.GetValue<string>() == Storage).DeepClone().AsObject();
        }
    }
}
