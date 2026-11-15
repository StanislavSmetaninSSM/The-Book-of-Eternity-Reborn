using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalLocationMaterializationValidationTests
{
    [Fact]
    public async Task StorageAccessProjection_BirthPublishesEffectiveDenialAndPreservesPendingCommand()
    {
        const string path = "game_state/misc/storage_access.json";
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        var backups = await ArrangeStorageBirthAccessAsync(context, "Faction", access: false);
        var command = new JsonArray(new JsonObject
        {
            ["storageId"] = "storage_birth", ["targetPlayerId"] = "player_current", ["targetPlayerName"] = "Current player"
        });
        var rawAccess = MortalStorageAccessProjection.CreateEmptyRoot();
        rawAccess["grantStorageAccess"] = command.DeepClone();
        await context.WriteJsonAsync(path, rawAccess);
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(),
            i => i.Severity == IssueSeverity.Error);
        var issues = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateAsync(
            context.FileSystem, context.Normalizer, context.Validator, backups);
        Assert.DoesNotContain(issues, i => i.Severity == IssueSeverity.Error);
        var access = (await context.ReadJsonAsync(path))!;
        Assert.Equal(1, access["schemaVersion"]?.GetValue<int>());
        var entry = Assert.Single(access["entries"]!.AsArray());
        var map = (await context.ReadJsonAsync(MortalLocationMaterializationContract.WorldMapPath))!;
        var location = Assert.Single(map["locations"]!.AsArray())!;
        Assert.Equal(location["locationId"]!.GetValue<string>(), entry!["locationId"]!.GetValue<string>());
        Assert.Equal("storage_birth", entry["storageId"]!.GetValue<string>());
        Assert.False(entry["hasFullAccess"]!.GetValue<bool>());
        Assert.True(JsonNode.DeepEquals(location["locationStorages"]![0]!["owner"], entry["owner"]));
        Assert.True(JsonNode.DeepEquals(command, access["grantStorageAccess"]));
        Assert.Null(entry["contents"]);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StorageAccessProjection_ForgedEntriesRefusedWithOrWithoutLocationCommands(bool noCommands)
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        var backups = await ArrangeStorageBirthAccessAsync(context, "Player", false);
        if (noCommands)
        {
            await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateAsync(context.FileSystem, context.Normalizer, context.Validator, backups);
            await context.CaptureValidatedPendingSnapshotAsync(43);
        }
        var raw = (await context.ReadJsonAsync(MortalStorageAccessProjection.StatePath))!.AsObject();
        raw["entries"] = new JsonArray(new JsonObject { ["locationId"] = "invented", ["storageId"] = "storage_birth", ["hasFullAccess"] = true });
        await context.WriteJsonAsync(MortalStorageAccessProjection.StatePath, raw);
        var before = await context.CaptureBytesAsync(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
        var issues = await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync();
        Assert.Contains(issues, i => i.Code == "mortal_location_storage_access_client_owned_mutation");
        var after = await context.CaptureBytesAsync(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
        foreach (var pair in before) Assert.Equal(pair.Value, after[pair.Key]);
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(issues));
    }

    [Fact]
    public async Task StorageAccessProjection_ReachedSidecarPublicationCutRestoresAllPaths()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        using var owned = new CleanupOwnedFixture(context.RootPath, output.WriteLine);
        var backups = await ArrangeStorageBirthAccessAsync(context, "Faction", false);
        var captured = await context.CaptureBytesAsync(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
        var before = captured.ToDictionary(p => p.Key, p => p.Value == null ? null : Convert.FromBase64String(p.Value), StringComparer.Ordinal);
        var generation = CleanupPublicationCut.ReadOptional(context.FileSystem.SessionGenerationPath);
        var cut = new KnownRollbackPublicationCut(context.FileSystem.ResolvePath(MortalStorageAccessProjection.StatePath));
        cut.Attach(context.FileSystem); context.CurrentPublicationObserver = cut.Hooks.LocalPublicationObserver;
        var failure = await Record.ExceptionAsync(() => AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateAsync(
            context.FileSystem, context.Normalizer, context.Validator, backups));
        cut.AssertRestored(before, generation, failure, output.WriteLine);
        Assert.Equal(MortalStorageAccessProjection.StatePath, Assert.IsType<CanonicalStateWriteException>(failure).RelativePath);
    }

    [Fact]
    public async Task StorageAccessProjection_WebUsesEffectiveEntriesNotPendingRights()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        var backups = await ArrangeStorageBirthAccessAsync(context, "Faction", false);
        await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateAsync(context.FileSystem, context.Normalizer, context.Validator, backups);
        var raw = (await context.ReadJsonAsync(MortalStorageAccessProjection.StatePath))!.AsObject();
        raw["grantStorageAccess"] = new JsonArray(new JsonObject { ["storageId"] = "pending_only", ["targetPlayerId"] = "player_current", ["targetPlayerName"] = "Player" });
        await context.WriteJsonAsync(MortalStorageAccessProjection.StatePath, raw);
        var state = new StateManager(context.FileSystem, new GameSettings(), NullLogger<StateManager>.Instance);
        var result = await BookOfEternityClient.UI.ExplorerMortalWorldCommandResultBuilder.TryBuildAsync("/storage_access", state, context.FileSystem);
        var rendered = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        Assert.Contains("storage_birth", rendered); Assert.Contains("Доступ закрыт", rendered); Assert.DoesNotContain("pending_only", rendered);
        raw["entries"]![0]!["hasFullAccess"] = true;
        await context.WriteJsonAsync(MortalStorageAccessProjection.StatePath, raw);
        result = await BookOfEternityClient.UI.ExplorerMortalWorldCommandResultBuilder.TryBuildAsync("/storage_access", state, context.FileSystem);
        Assert.DoesNotContain("storage_birth", System.Text.Json.JsonSerializer.Serialize(result));
    }

    [Fact]
    public async Task StorageAccessProjection_WebRejectsMutuallyConsistentUnacceptedMap()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        var map = new JsonObject { ["realm"] = "mortal_world", ["locations"] = new JsonArray(new JsonObject
        {
            ["realm"] = "mortal_world", ["locationId"] = "forged_location",
            ["locationStorages"] = new JsonArray(MortalLocationTestFixture.CreateStorageMetadata("forged_storage", "Forged", true))
        }) };
        var access = MortalStorageAccessProjection.Build(map, null);
        Assert.True(access.IsValid);
        await context.WriteJsonAsync(MortalLocationMaterializationContract.WorldMapPath, map);
        await context.WriteJsonAsync(MortalStorageAccessProjection.StatePath, access.Root!);
        var state = new StateManager(context.FileSystem, new GameSettings(), NullLogger<StateManager>.Instance);
        var result = await BookOfEternityClient.UI.ExplorerMortalWorldCommandResultBuilder.TryBuildAsync("/storage_access", state, context.FileSystem);
        Assert.DoesNotContain("forged_storage", System.Text.Json.JsonSerializer.Serialize(result));
    }

    [Theory]
    [InlineData("hidden")]
    [InlineData("rumored")]
    public async Task StorageAccessProjection_WebDoesNotExposeUndiscoveredStorage(string tier)
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        var location = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity("private_location", "Remote", tier);
        location["locationStorages"] = new JsonArray(MortalLocationTestFixture.CreateStorageMetadata("private_storage", "Private", true));
        location["materialization"]!["sections"]!["storageMetadata"] = new JsonObject { ["disposition"] = "populated", ["reason"] = null };
        MortalLocationTestFixture.ResealCanonicalLocation(location);
        using var locationDocument = System.Text.Json.JsonDocument.Parse(location.ToJsonString());
        Assert.Empty(MortalLocationMaterializationContract.ValidateCanonicalLocation(locationDocument.RootElement, "fixture"));
        var map = MortalLocationTestFixture.CreateWorldMap(location);
        var index = MortalLocationTestFixture.CreateIdentityIndex(location);
        Assert.Empty(MortalLocationIdentityState.Parse(index).ValidateCanonicalState(map));
        var access = MortalStorageAccessProjection.Build(map, null);
        Assert.True(access.IsValid);
        await context.WriteJsonAsync(MortalLocationMaterializationContract.WorldMapPath, map);
        await context.WriteJsonAsync(MortalLocationIdentityState.StatePath, index);
        await context.WriteJsonAsync(MortalStorageAccessProjection.StatePath, access.Root!);
        var state = new StateManager(context.FileSystem, new GameSettings(), NullLogger<StateManager>.Instance);
        var result = await BookOfEternityClient.UI.ExplorerMortalWorldCommandResultBuilder.TryBuildAsync("/storage_access", state, context.FileSystem);
        Assert.DoesNotContain("private_storage", System.Text.Json.JsonSerializer.Serialize(result));
    }

    [Fact]
    public async Task StorageAccessProjection_MalformedRealmReturnsRawDiagnostics()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        await context.WritePreTurnCanonicalStateAsync(MortalLocationTestFixture.CreateCanonicalLocation());
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var map = (await context.ReadJsonAsync(MortalLocationMaterializationContract.WorldMapPath))!.AsObject();
        map["realm"] = 17;
        await context.WriteJsonAsync(MortalLocationMaterializationContract.WorldMapPath, map);
        var issues = await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync();
        Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error);
    }

    [Fact]
    public async Task StorageAccessProjection_MalformedRealmNoCommandNormalizerDoesNotThrowOrWrite()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        await context.WritePreTurnCanonicalStateAsync(MortalLocationTestFixture.CreateCanonicalLocation());
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var map = (await context.ReadJsonAsync(MortalLocationMaterializationContract.WorldMapPath))!.AsObject();
        map["realm"] = 17;
        await context.WriteJsonAsync(MortalLocationMaterializationContract.WorldMapPath, map);
        var before = await context.FileSystem.ReadFileAsync(MortalLocationMaterializationContract.WorldMapPath);
        Assert.Null(await context.Normalizer.NormalizeMortalLocationsAsync(null));
        Assert.Equal(before, await context.FileSystem.ReadFileAsync(MortalLocationMaterializationContract.WorldMapPath));
    }

}
