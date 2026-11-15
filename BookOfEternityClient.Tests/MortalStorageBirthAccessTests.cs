using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Storage player identity is exact; explicit access is never derived from ownership.</summary>
public sealed class MortalStorageBirthAccessTests
{
    [Fact]
    public void DeliveredStorageGuidanceContainsCompleteRestrictedOwnerFragment()
    {
        const string fragment = """{"storageId":"storage_locked","name":"Locked chest","description":"A closed chest owned by the current player.","image_prompt":"A closed wooden chest, no text","capacity":8,"volume":40,"owner":{"ownerType":"Player","ownerId":"player_current","ownerName":"Current player"},"authorizedUsers":[],"hasFullAccess":false,"contents":[]}""";
        foreach (var path in new[] { "Rules/Block_2.txt", "BookOfEternityClient/game_master_daemon.ps1" })
        {
            var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, path));
            Assert.Contains(fragment, text, StringComparison.Ordinal);
            Assert.Contains("Explicit hasFullAccess=false remains restricted even for the owner", text, StringComparison.Ordinal);
            Assert.Contains("unknown players are unsupported", text, StringComparison.Ordinal);
        }
        using var document = JsonDocument.Parse(fragment);
        Assert.Empty(MortalLocationStorageMetadataContract.Validate(document.RootElement, "storage"));
    }

    [Fact]
    public void ExistingStorageBirthGuidanceUsesCompleteEmptySourceLocalOperation()
    {
        foreach (var path in new[] { "Rules/Block_2.txt", "Rules/Block_CLI_Operations.txt", "BookOfEternityClient/game_master_daemon.ps1" })
        {
            var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, path));
            Assert.Contains("mortal_existing_storage_birth_v1", text, StringComparison.Ordinal);
            Assert.Contains("same-turn new-location targets are not supported", text, StringComparison.Ordinal);
            Assert.Contains("preserving the original location envelope/receipt", text, StringComparison.Ordinal);
        }
        var example = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples/E_CLI_Storage_Birth.txt"));
        var json = example.Split("```json", StringSplitOptions.None)[1].Split("```", StringSplitOptions.None)[0];
        var root = JsonNode.Parse(json)!;
        var operation = Assert.Single(root["worldMapUpdates"]!["storagesToAdd"]!.AsArray())!;
        Assert.Equal("loc_warehouse", operation["targetLocationId"]!.GetValue<string>());
        var storage = operation["storage"]!;
        Assert.Empty(storage["contents"]!.AsArray());
        Assert.False(storage["hasFullAccess"]!.GetValue<bool>());
        Assert.Equal("player_current", storage["owner"]!["ownerId"]!.GetValue<string>());
        Assert.Empty(MortalLocationStorageMetadataContract.Validate(JsonSerializer.SerializeToElement(storage), "storage"));
    }

    [Fact]
    public void CurrentStorageCargoBirthGuidanceUsesCompleteSourceLocalExample()
    {
        foreach (var path in new[] { "Rules/Block_2.txt", "Rules/Block_CLI_Operations.txt", "BookOfEternityClient/game_master_daemon.ps1" })
            Assert.Contains("mortal_current_storage_cargo_birth_v1", File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, path)), StringComparison.Ordinal);
        var example = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples/E_CLI_Storage_Birth.txt"));
        var json = example.Split("```json", StringSplitOptions.None)[2].Split("```", StringSplitOptions.None)[0];
        var storage = JsonNode.Parse(json)!.AsObject();
        Assert.Empty(MortalLocationStorageMetadataContract.Validate(JsonSerializer.SerializeToElement(storage), "storage"));
        Assert.Equal(1, storage["capacity"]!.GetValue<int>());
        Assert.Equal(0.01d, storage["volume"]!.GetValue<double>());
        Assert.Null(storage["cargoMeasurements"]); Assert.Null(storage["cargoBirthEvidence"]);
        var item = Assert.Single(storage["contents"]!.AsArray())!.AsObject();
        Assert.Empty(MortalItemMaterializationContract.Validate(JsonSerializer.SerializeToElement(item), "storage.contents[0]", MortalItemMaterializationPhase.RawPreSeal));
        Assert.Equal("locref_current:storage_supply", item["materialization"]!["sourceAuthority"]!["authorityId"]!.GetValue<string>());
        Assert.Equal(1, item["count"]!.GetValue<int>());
        Assert.False(item["isContainer"]!.GetValue<bool>());
        Assert.Equal(storage["volume"]!.GetValue<double>(), item["volume"]!.GetValue<double>());
    }

    [Theory]
    [InlineData("Player", false)]
    [InlineData("Shared", false)]
    [InlineData("Player", true)]
    public void UnknownPlayerIdentityIsRejectedWithoutMutation(string ownerType, bool recipient)
    {
        var storage = Storage(ownerType, false);
        var suffix = recipient ? ".authorizedUsers[0].playerId" : ".owner.ownerId";
        if (recipient)
            storage["authorizedUsers"] = new JsonArray(new JsonObject
                { ["playerId"] = "player_unknown", ["playerName"] = "Unknown player" });
        else
            storage["owner"]!["ownerId"] = "player_unknown";
        var before = storage.ToJsonString();

        var issues = MortalLocationStorageMetadataContract.Validate(
            JsonSerializer.SerializeToElement(storage), "location.locationStorages[0]");

        Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error &&
            issue.FilePath == "location.locationStorages[0]" + suffix);
        Assert.Equal(before, storage.ToJsonString());
        Assert.False(storage["hasFullAccess"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData("Player", false)]
    [InlineData("Shared", false)]
    [InlineData("Player", true)]
    [InlineData("Shared", true)]
    public void ExactCurrentPlayerDoesNotOverrideExplicitAccess(string ownerType, bool access)
    {
        var storage = Storage(ownerType, access);
        storage["authorizedUsers"] = new JsonArray(new JsonObject
            { ["playerId"] = "player_current", ["playerName"] = "Current player" });
        var before = storage.ToJsonString();

        var issues = MortalLocationStorageMetadataContract.Validate(
            JsonSerializer.SerializeToElement(storage), "location.locationStorages[0]");

        Assert.Empty(issues);
        Assert.Equal(before, storage.ToJsonString());
        Assert.Equal(access, storage["hasFullAccess"]!.GetValue<bool>());
    }

    [Fact]
    public void FactionShapeRemainsSeparateFromFactionAuthority()
    {
        var storage = Storage("Faction", false);
        storage["owner"]!["ownerId"] = "faction_wardens";
        storage["owner"]!["ownerName"] = "Wardens";
        var before = storage.ToJsonString();

        Assert.Empty(MortalLocationStorageMetadataContract.Validate(
            JsonSerializer.SerializeToElement(storage), "location.locationStorages[0]"));
        Assert.Equal(before, storage.ToJsonString());
        Assert.False(storage["hasFullAccess"]!.GetValue<bool>());
    }

    private static JsonObject Storage(string ownerType, bool access) => new()
    {
        ["storageId"] = "storage_locked_chest",
        ["name"] = "Locked supply chest",
        ["description"] = "A sound wooden chest holding supplies behind a closed lock.",
        ["image_prompt"] = "A sturdy locked wooden supply chest, no text",
        ["capacity"] = 8,
        ["volume"] = 40.0,
        ["owner"] = new JsonObject
        {
            ["ownerType"] = ownerType, ["ownerId"] = "player_current", ["ownerName"] = "Current player"
        },
        ["authorizedUsers"] = new JsonArray(),
        ["hasFullAccess"] = access
    };
}
