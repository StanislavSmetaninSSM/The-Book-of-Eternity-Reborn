using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Small detached fixtures over the existing plan and completion APIs. No world, I/O or reflection.</summary>
public sealed class MortalOffscreenStorageBirthTests
{
    private const string Target = "loc_offscreen_birth_b";
    private const string Storage = "storage_offscreen_birth";
    private const string ItemId = "itm_offscreen_birth";
    private static readonly MortalStorageBirthSourceBinding Source = new("session", "request", "snapshot", 42, "projection");

    [Fact]
    public void PreparationRetainsExactNoncurrentBirthCoordinateAndStableDraft()
    {
        var input = Input();
        var original = input.RawWorldMapUpdates!.ToJsonString();
        var cache = new MortalLocationAcceptedTurnPlanCache();
        var result = cache.GetOrBuild(input);
        Assert.True(result.Success, Describe(result.Issues));
        var plan = result.Plan!;
        Assert.Same(plan, cache.GetOrBuild(input).Plan);
        Assert.Equal("loc_offscreen_birth_a", plan.FinalCurrentLocation!["locationId"]!.GetValue<string>());
        Assert.Empty(plan.FinalCurrentLocation["locationStorages"]!.AsArray());
        var draft = Assert.Single(plan.StorageBirthDrafts);
        Assert.Equal(Target, draft.LocationId);
        Assert.Equal(Storage, draft.StorageId);
        Assert.Equal(42, draft.SourceTurn);
        Assert.Equal("worldMapUpdates.storagesToAdd[0]", draft.OperationRef);
        var transition = Transition(plan);
        Assert.Equal(draft.TransitionId, transition["transitionId"]!.GetValue<string>());
        Assert.Equal("turn_42", transition["sourceAuthorityId"]!.GetValue<string>());
        Assert.Null(transition["afterState"]!["contents"]);
        Assert.Null(transition["afterState"]!["cargoMeasurements"]);
        var entry = Assert.Single(plan.FinalStorageContents["entries"]!.AsArray())!;
        Assert.Equal(Target, entry["locationId"]!.GetValue<string>());
        Assert.Equal(Storage, entry["storageId"]!.GetValue<string>());
        var item = Assert.Single(entry["contents"]!.AsArray())!;
        Assert.Equal(Target + ":" + Storage, item["materialization"]!["sourceAuthority"]!["authorityId"]!.GetValue<string>());
        Assert.Contains(MortalLocationStorageContentsState.StatePath, plan.TouchedPaths);
        Assert.Equal(original, input.RawWorldMapUpdates.ToJsonString());
    }

    [Fact]
    public void FinalizationUsesOffscreenItemPhaseWithoutMutatingPreparedRootsOrHistory()
    {
        var (plan, phase, catalog) = PreparedPhase();
        var beforeIndex = plan.FinalIdentityIndex.ToJsonString();
        var beforeContents = plan.FinalStorageContents.ToJsonString();
        var result = MortalLocationStorageBirthEvidence.Finalize(plan, phase, catalog,
            new Dictionary<string, string> { [MortalItemTestFixture.CreationRef] = ItemId }, Source);
        Assert.True(result.IsValid, Describe(result.Issues));
        Assert.Equal(beforeIndex, plan.FinalIdentityIndex.ToJsonString());
        Assert.Equal(beforeContents, plan.FinalStorageContents.ToJsonString());
        Assert.Empty(result.Completion!.ValidateForPublication(plan, Source));
        var completed = result.Completion.Plan;
        var evidence = Transition(completed)["afterState"]!["cargoBirthEvidence"]!.AsObject();
        Assert.Equal(Target, evidence["locationId"]!.GetValue<string>());
        Assert.Equal(Storage, evidence["storageId"]!.GetValue<string>());
        Assert.Equal("snapshot", evidence["sourceAuthority"]!["snapshotToken"]!.GetValue<string>());
        Assert.Equal(ItemId, Assert.Single(evidence["cargoMeasurements"]!.AsArray())!["itemId"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(plan.FinalWorldMap, completed.FinalWorldMap));
        Assert.True(JsonNode.DeepEquals(plan.FinalCurrentLocation, completed.FinalCurrentLocation));
        Assert.Empty(MortalLocationIdentityState.Parse(completed.FinalIdentityIndex).Issues);
        Assert.Empty(result.Completion.ValidateReadback(completed.FinalIdentityIndex));
        completed.FinalIdentityIndex["locationEntries"] = new JsonArray();
        Assert.NotEmpty(result.Completion.ValidateReadback(completed.FinalIdentityIndex));
        Assert.Empty(result.Completion.ValidateForPublication(plan, Source));
    }

    [Theory]
    [InlineData("carrier")]
    [InlineData("metadata")]
    [InlineData("turn")]
    public void FinalizationRefusesUnmatchedProjectedCarrierMetadataOrSourceTurn(string mutation)
    {
        var (plan, phase, _) = PreparedPhase();
        var roots = phase.ItemPhaseAfterImages;
        var source = Source;
        if (mutation == "carrier")
            roots[MortalLocationStorageContentsState.StatePath]!["entries"]![0]!["locationId"] = "loc_other";
        if (mutation == "metadata")
            Transition(plan)["afterState"]!["volume"] = 0d;
        if (mutation == "turn") source = source with { Turn = 43 };
        var catalog = Catalog(roots);
        var before = plan.FinalIdentityIndex.ToJsonString();
        var result = MortalLocationStorageBirthEvidence.Finalize(plan, phase, catalog,
            new Dictionary<string, string> { [MortalItemTestFixture.CreationRef] = ItemId }, source);
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Issues);
        Assert.Equal(before, plan.FinalIdentityIndex.ToJsonString());
    }

    private static (MortalLocationAcceptedTurnPlan, MortalItemCanonicalProjectionResult, MortalItemCarrierCatalog) PreparedPhase()
    {
        var planning = MortalLocationAcceptedTurnPlanner.Build(Input());
        Assert.True(planning.Success, Describe(planning.Issues));
        var plan = planning.Plan!;
        var contents = plan.FinalStorageContents.DeepClone().AsObject();
        var item = contents["entries"]![0]!["contents"]![0]!.AsObject();
        item["itemId"] = ItemId;
        item.Remove("creationRef");
        var roots = new Dictionary<string, JsonNode?>
        {
            [StorageTransportMoveService.CurrentLocationPath] = plan.FinalCurrentLocation!.DeepClone(),
            [MortalLocationStorageContentsState.StatePath] = contents
        };
        var phase = new MortalItemCanonicalProjectionResult(roots, MortalItemIdentityState.CreateEmptyRoot(),
            Array.Empty<ValidationIssue>(), "phase");
        return (plan, phase, Catalog(roots));
    }

    private static MortalItemCarrierCatalog Catalog(IReadOnlyDictionary<string, JsonNode?> roots) =>
        MortalItemCarrierCatalog.Build(new(null, null, null,
            roots[StorageTransportMoveService.CurrentLocationPath] as JsonObject, null,
            new Dictionary<string, JsonObject>(), roots[MortalLocationStorageContentsState.StatePath] as JsonObject));

    private static JsonObject Transition(MortalLocationAcceptedTurnPlan plan) =>
        Assert.Single(Assert.Single(plan.FinalIdentityIndex["locationEntries"]!.AsArray().OfType<JsonObject>(),
            e => e["locationId"]!.GetValue<string>() == Target)["transitions"]!.AsArray().OfType<JsonObject>());

    internal static MortalLocationAcceptedTurnInput Input()
    {
        var a = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity("loc_offscreen_birth_a", "Current scene", x: 0);
        var b = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(Target, "Remote storehouse", x: 1);
        var map = MortalLocationTestFixture.CreateWorldMap(a, b);
        var index = MortalLocationTestFixture.CreateIdentityIndex(a);
        index["locationEntries"]!.AsArray().Add(MortalLocationTestFixture.CreateIdentityIndex(b)["locationEntries"]![0]!.DeepClone());
        var item = MortalItemTestFixture.CreateRawRoot("storage_placement", "location_storage", Target + ":" + Storage);
        item["volume"] = 1d;
        var storage = MortalLocationTestFixture.CreateStorageMetadata(Storage, "Remote chest", true, new JsonArray(item));
        storage["capacity"] = 1; storage["volume"] = 1d;
        var commands = new JsonObject { ["storagesToAdd"] = new JsonArray(new JsonObject
            { ["targetLocationId"] = Target, ["storage"] = storage }) };
        return new(map, MortalLocationTestFixture.CreateCurrentProjection(a), index, null, commands, 42);
    }

    private static string Describe(IEnumerable<ValidationIssue> issues) => string.Join(";", issues.Select(i => i.Code + ":" + i.FilePath));
}
