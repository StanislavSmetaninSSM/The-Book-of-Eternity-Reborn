using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalLocationRepairRetryPolicyTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void CorrectedStorageChildrenDischargeExactRetryObligations(int count)
    {
        var (obligations, root) = StorageRequest(count);
        Assert.True(MortalLocationRepairRetryPolicy.HasExactResubmission(obligations, null, root));
    }

    [Theory]
    [InlineData("drop")]
    [InlineData("replace")]
    [InlineData("move")]
    [InlineData("parent")]
    [InlineData("duplicate")]
    public void ChangedOrDroppedStorageChildCannotDischargeRetry(string fault)
    {
        var (obligations, root) = StorageRequest(2);
        var ops = root["worldMapUpdates"]!["storagesToAdd"]!.AsArray();
        if (fault == "drop") ops.RemoveAt(1);
        if (fault == "replace") ops[1]!["storage"]!["storageId"] = "replacement";
        if (fault == "move") { var first = ops[0]!.DeepClone(); ops.RemoveAt(0); ops.Add(first); }
        if (fault == "parent") ops[1]!["targetLocationId"] = "loc_other";
        if (fault == "duplicate") ops.Add(ops[1]!.DeepClone());
        Assert.False(MortalLocationRepairRetryPolicy.HasExactResubmission(obligations, null, root));
    }

    [Fact]
    public void CaptureRetainsTwoChildrenSharingOneParent()
    {
        var packets = Enumerable.Range(0, 2).Select(i => new MortalLocationRepairPacket
        {
            CanonicalActorNames = new[] { "mortal_location:existing:loc_parent" },
            Route = "storage_addition", RawCoordinate = $"worldMapUpdates.storagesToAdd[{i}]",
            ExpectedStorageId = "storage_" + i
        }).ToArray();
        var captured = MortalLocationRepairRetryPolicy.Capture(packets);
        Assert.Equal(2, captured.Count);
        for (var i = 0; i < 2; i++)
        {
            Assert.Equal(packets[i].RawCoordinate, captured[i].RawCoordinate);
            Assert.Equal(packets[i].ExpectedStorageId, captured[i].StorageId);
        }
    }

    [Theory]
    [InlineData("world_map_creation", "newLocations", "mortal_location:new:ref_a", "initialId")]
    [InlineData("world_map_link_creation", "newLinks", "mortal_location_link:new:ref_a", "initialId")]
    [InlineData("narrow_location_update", "locationUpdates", "mortal_location:existing:ref_a", "locationId")]
    [InlineData("location_discovery_transition", "locationDiscoveryTransitions", "mortal_location:existing:ref_a", "locationId")]
    [InlineData("link_update", "linkUpdates", "mortal_location_link:existing:ref_a", "linkId")]
    [InlineData("link_removal", "linkRemovals", "mortal_location_link:existing:ref_a", "linkId")]
    public void ExistingMapRoutesStillRequireOneExactIdentity(string route, string collection, string actor, string idField)
    {
        var candidate = new JsonObject { [idField] = "ref_a", ["materialization"] = new JsonObject { ["materializationId"] = "birth_a" } };
        var rows = new JsonArray(candidate);
        var root = new JsonObject { ["worldMapUpdates"] = new JsonObject { [collection] = rows } };
        var obligations = new[] { new MortalLocationRepairRetryObligation(actor, route, "birth_a") };
        Assert.True(MortalLocationRepairRetryPolicy.HasExactResubmission(obligations, null, root));
        rows.Add(candidate.DeepClone());
        Assert.False(MortalLocationRepairRetryPolicy.HasExactResubmission(obligations, null, root));
        rows.RemoveAt(1); candidate[idField] = "REF_A";
        Assert.False(MortalLocationRepairRetryPolicy.HasExactResubmission(obligations, null, root));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExistingCurrentRoutesRetainIdentityAndCreationEvidence(bool creation)
    {
        var current = new JsonObject
        {
            [creation ? "initialId" : "locationId"] = "current_exact",
            ["materialization"] = new JsonObject { ["materializationId"] = "origin_exact" }
        };
        var root = new JsonObject { ["currentLocationData"] = current };
        var obligations = new[] { new MortalLocationRepairRetryObligation(
            creation ? "mortal_location:new:current_exact" : "mortal_location:existing:current_exact",
            creation ? "current_scene_creation" : "current_selection", "origin_exact") };
        Assert.True(MortalLocationRepairRetryPolicy.HasExactResubmission(obligations, root, null));
        current["materialization"]!["materializationId"] = "changed";
        Assert.Equal(!creation, MortalLocationRepairRetryPolicy.HasExactResubmission(obligations, root, null));
        current[creation ? "initialId" : "locationId"] = "CURRENT_EXACT";
        Assert.False(MortalLocationRepairRetryPolicy.HasExactResubmission(obligations, root, null));
    }

    private static (MortalLocationRepairRetryObligation[], JsonObject) StorageRequest(int count)
    {
        var rows = new JsonArray();
        var obligations = new List<MortalLocationRepairRetryObligation>();
        for (var i = 0; i < count; i++)
        {
            rows.Add(new JsonObject { ["targetLocationId"] = "loc_parent",
                ["storage"] = new JsonObject { ["storageId"] = "storage_" + i, ["name"] = "Corrected" } });
            obligations.Add(new("mortal_location:existing:loc_parent", "storage_addition", null,
                $"worldMapUpdates.storagesToAdd[{i}]", "storage_" + i));
        }
        return (obligations.ToArray(), new JsonObject { ["worldMapUpdates"] = new JsonObject { ["storagesToAdd"] = rows } });
    }
}
