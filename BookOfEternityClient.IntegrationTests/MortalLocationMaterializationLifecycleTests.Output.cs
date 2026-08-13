using BookOfEternityClient.Services;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task MortalLocationCanonicalRepair_RejectsStaleTravelConfirmationUntilRewritten()
    {
        await WriteJsonAsync("output/narrative_response.json", new
        {
            response = "Вы переходите через Чёрный брод и входите в башню.",
            timestamp = "2026-08-12T10:00:00Z"
        });
        await WriteJsonAsync("output/interface_updates.json", new
        {
            dialogueOptions = new[] { "Осмотреть башню" },
            timestamp = "2026-08-12T10:00:00Z"
        });
        var repairBoundaryUtc = DateTime.UtcNow.AddMinutes(-1);
        var staleWrittenAtUtc = repairBoundaryUtc.AddMinutes(-1);
        File.SetLastWriteTimeUtc(
            _fs.ResolvePath("output/narrative_response.json"),
            staleWrittenAtUtc);
        File.SetLastWriteTimeUtc(
            _fs.ResolvePath("output/interface_updates.json"),
            staleWrittenAtUtc);
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["realm"] = "mortal_world",
                ["locations"] = new JsonArray(),
                ["links"] = new JsonArray()
            });
        await context.WriteJsonAsync(
            MortalLocationIdentityState.StatePath,
            MortalLocationIdentityState.CreateEmptyRoot());
        await context.CaptureValidatedPendingSnapshotAsync();
        var first = MortalLocationTestFixture.CreateRawLocation("world_map_creation");
        first["coordinates"] = new JsonObject { ["x"] = 91, ["y"] = 0, ["z"] = 0 };
        var second = first.DeepClone().AsObject();
        second["initialId"] = "locref_stale_tower";
        second["materialization"]!["initialId"] = "locref_stale_tower";
        second["materialization"]!["materializationId"] = "mlocmat_stale_tower";
        await context.WriteRawTurnStateAsync(
            currentLocationData: null,
            worldMapUpdates: new JsonObject
            {
                ["newLocations"] = new JsonArray(first, second)
            });
        var realIssues = await context.Validator
            .ValidateAcceptedTurnRawMortalLocationMaterializationAsync();
        var locationIssue = Assert.Single(realIssues, issue =>
            issue.Code == "mortal_location_materialization_coordinate_collision" &&
            issue.Actor == "mortal_location:new:locref_stale_tower");
        Assert.StartsWith("game_state/world/world_map.json.", locationIssue.FilePath, StringComparison.Ordinal);

        var engine = CreateGameEngine();
        var staleIssues = InvokePrivateValue<List<ValidationIssue>>(
            engine,
            "CollectPlayerFacingOutputStaleAfterCanonicalRepairIssues",
            new[] { locationIssue },
            repairBoundaryUtc);

        Assert.Equal(2, staleIssues.Count);
        Assert.All(
            staleIssues,
            issue => Assert.Equal(
                "accepted_turn_stale_player_facing_output_after_canonical_repair",
                issue.Code));
        Assert.Contains(staleIssues, issue => issue.FilePath == "output/narrative_response.json");
        Assert.Contains(staleIssues, issue => issue.FilePath == "output/interface_updates.json");
    }

    [Fact]
    public async Task MortalLocationRollback_RemovesUnacceptedTransitionAndPlayerConfirmationByteExact()
    {
        var baselineMap = "{\"schemaVersion\":1,\"realm\":\"mortal_world\",\"locations\":[],\"links\":[]}"u8.ToArray();
        var baselineIndex = "{\"schemaVersion\":1,\"realm\":\"mortal_world\",\"locationEntries\":[],\"linkEntries\":[]}"u8.ToArray();
        await _fs.WriteFileAtomicBytesAsync(MortalLocationMaterializationContract.WorldMapPath, baselineMap);
        await _fs.WriteFileAtomicBytesAsync(MortalLocationIdentityState.StatePath, baselineIndex);
        var engine = CreateGameEngine();
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "mortal_location_output_rollback");

        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            """{"schemaVersion":1,"realm":"mortal_world","locations":[{"locationId":"loc_unaccepted_tower"}],"links":[]}""");
        await _fs.WriteFileAtomicAsync(
            MortalLocationIdentityState.StatePath,
            """{"schemaVersion":1,"realm":"mortal_world","locationEntries":[{"locationId":"loc_unaccepted_tower"}],"linkEntries":[]}""");
        await WriteJsonAsync("output/narrative_response.json", new
        {
            response = "Вы вошли в не принятую башню.",
            timestamp = "2026-08-12T10:00:00Z"
        });
        await WriteJsonAsync("output/interface_updates.json", new
        {
            dialogueOptions = new[] { "Подняться наверх" },
            timestamp = "2026-08-12T10:00:00Z"
        });

        await InvokePrivateTaskAsync(engine, "RestorePreTurnBackup", rollbackSnapshot);

        Assert.Equal(
            baselineMap,
            await _fs.ReadFileBytesAsync(MortalLocationMaterializationContract.WorldMapPath));
        Assert.Equal(
            baselineIndex,
            await _fs.ReadFileBytesAsync(MortalLocationIdentityState.StatePath));
        Assert.False(_fs.FileExists("output/narrative_response.json"));
        Assert.False(_fs.FileExists("output/interface_updates.json"));
    }
}
