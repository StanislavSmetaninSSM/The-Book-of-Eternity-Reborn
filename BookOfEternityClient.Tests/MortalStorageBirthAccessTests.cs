using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Storage player identity is exact; explicit access is never derived from ownership.</summary>
public sealed class MortalStorageBirthAccessTests
{
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
