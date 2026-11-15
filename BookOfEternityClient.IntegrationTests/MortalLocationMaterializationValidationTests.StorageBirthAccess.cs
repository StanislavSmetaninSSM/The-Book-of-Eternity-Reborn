using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalLocationMaterializationValidationTests
{
    [Theory]
    [InlineData("Faction", false)]
    [InlineData("Player", false)]
    [InlineData("Player", true)]
    public async Task StorageBirth_AcceptedMetadataAndMoveConsumerPreserveExplicitAccess(string ownerType, bool access)
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        var backups = await ArrangeStorageBirthAccessAsync(context, ownerType, access);
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);

        var result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateAsync(
            context.FileSystem, context.Normalizer, context.Validator, backups);

        Assert.DoesNotContain(result.Issues, issue => issue.Severity == IssueSeverity.Error);
        var map = (await context.ReadJsonAsync(MortalLocationMaterializationContract.WorldMapPath))!;
        var location = Assert.Single(map["locations"]!.AsArray())!;
        var current = (await context.ReadJsonAsync(MortalLocationMaterializationContract.CurrentLocationPath))!;
        var metadata = Assert.Single(location["locationStorages"]!.AsArray())!;
        var projected = Assert.Single(current["locationStorages"]!.AsArray())!;
        Assert.Equal(location["locationId"]!.GetValue<string>(), current["locationId"]!.GetValue<string>());
        Assert.Equal("storage_birth", metadata["storageId"]!.GetValue<string>());
        Assert.Equal(access, metadata["hasFullAccess"]!.GetValue<bool>());
        Assert.Equal(access, projected["hasFullAccess"]!.GetValue<bool>());
        Assert.True(JsonNode.DeepEquals(metadata["owner"], projected["owner"]));
        Assert.Equal(42, location["materializationReceipt"]!["sourceTurn"]!.GetValue<int>());
        Assert.Null(metadata["contents"]);
        Assert.Empty(projected["contents"]!.AsArray());

        var moves = await StorageTransportMoveService.ReadStorageMoveContextAsync(context.FileSystem);
        Assert.True(moves.Success, moves.Message);
        Assert.Equal(access, moves.Storages.Any(storage => storage.StorageId == "storage_birth"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StorageBirth_UnknownRecipientOrFactionIsRejectedBeforeWrites(bool faction)
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        await ArrangeStorageBirthAccessAsync(context, faction ? "Faction" : "Player", access: false);
        var raw = (await context.ReadJsonAsync(MortalLocationMaterializationContract.CurrentLocationPath))!;
        var storage = raw["currentLocationData"]!["locationStorages"]![0]!;
        if (faction) storage["owner"]!["ownerId"] = "missing_faction";
        else storage["authorizedUsers"] = new JsonArray(new JsonObject
            { ["playerId"] = "missing_player", ["playerName"] = "Unknown" });
        await context.WriteJsonAsync(MortalLocationMaterializationContract.CurrentLocationPath, raw);
        var before = await context.CaptureBytesAsync(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);

        var issues = await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error &&
            issue.FilePath.EndsWith(faction ? ".owner.ownerId" : ".authorizedUsers[0].playerId", StringComparison.Ordinal));
        var after = await context.CaptureBytesAsync(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
        foreach (var pair in before) Assert.Equal(pair.Value, after[pair.Key]);
    }

    [Fact]
    public async Task StorageBirth_ReachedLocationPublicationCutRestoresEveryTrackedPath()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        using var owned = new CleanupOwnedFixture(context.RootPath, output.WriteLine);
        var backups = await ArrangeStorageBirthAccessAsync(context, "Faction", access: false);
        var captured = await context.CaptureBytesAsync(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
        var before = captured.ToDictionary(pair => pair.Key,
            pair => pair.Value == null ? null : Convert.FromBase64String(pair.Value), StringComparer.Ordinal);
        var generation = CleanupPublicationCut.ReadOptional(context.FileSystem.SessionGenerationPath);
        var cut = new KnownRollbackPublicationCut(context.FileSystem.ResolvePath(MortalLocationMaterializationContract.WorldMapPath));
        cut.Attach(context.FileSystem);
        context.CurrentPublicationObserver = cut.Hooks.LocalPublicationObserver;

        var failure = await Record.ExceptionAsync(() => AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateAsync(
            context.FileSystem, context.Normalizer, context.Validator, backups));

        cut.AssertRestored(before, generation, failure, output.WriteLine);
        var writeFailure = Assert.IsType<CanonicalStateWriteException>(failure);
        Assert.Equal(MortalLocationMaterializationContract.WorldMapPath, writeFailure.RelativePath);
        Assert.Same(cut.Failure, writeFailure.InnerException);
    }

    private static async Task<Dictionary<string, string>> ArrangeStorageBirthAccessAsync(
        MortalLocationMaterializationTestContext context, string ownerType, bool access)
    {
        await context.WriteJsonAsync(MortalLocationMaterializationContract.WorldMapPath,
            MortalLocationTestFixture.CreateWorldMap());
        await context.WriteJsonAsync(MortalLocationIdentityState.StatePath, MortalLocationIdentityState.CreateEmptyRoot());
        await context.WriteJsonAsync(MortalItemIdentityState.StatePath, MortalItemIdentityState.CreateEmptyRoot());
        await context.WriteJsonAsync(InventoryEquipmentService.ItemsPath,
            new JsonObject { ["items"] = new JsonArray(), ["equipment"] = new JsonObject() });
        var faction = EffectMaterializationTestFixture.CreateSameTurnMortalFaction("faction_storage");
        faction["factionId"] = "faction_storage";
        faction.Remove("initialId"); faction.Remove("isNewFaction"); faction.Remove("activeEffectDefinitions");
        await context.WriteJsonAsync(FactionCoreChangesContract.FactionCorePath,
            new JsonObject { ["factions"] = new JsonArray(faction) });
        await context.CaptureValidatedPendingSnapshotAsync();
        var raw = MortalLocationTestFixture.CreateRawLocation("current_scene_creation");
        var storage = CreateStorage("storage_birth", "Birth storage");
        storage["owner"] = new JsonObject
        {
            ["ownerType"] = ownerType,
            ["ownerId"] = ownerType == "Faction" ? "faction_storage" : "player_current",
            ["ownerName"] = ownerType == "Faction" ? faction["name"]!.DeepClone() : JsonValue.Create("Current player")
        };
        storage["hasFullAccess"] = access;
        raw["locationStorages"] = new JsonArray(storage);
        SetSectionPopulated(raw, "storageMetadata");
        await context.WriteRawTurnStateAsync(raw, worldMapUpdates: null);
        return new[] { MortalLocationMaterializationContract.WorldMapPath, MortalLocationIdentityState.StatePath }
            .ToDictionary(path => path, path => "game_state/control/pending_turn_snapshot/" + path, StringComparer.Ordinal);
    }
}
