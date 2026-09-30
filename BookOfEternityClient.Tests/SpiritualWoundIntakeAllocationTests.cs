using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks allocation retention and input isolation at the actual upstream owners.
/// </summary>
public sealed class SpiritualWoundIntakeAllocationTests
{
    /// <summary>
    /// Preserves each upstream owner's existing permanent identity format during strict replay.
    /// </summary>
    /// <param name="kind">
    /// Semantic journal family.
    /// </param>
    /// <param name="prefix">
    /// Existing gameplay identity prefix.
    /// </param>
    [Theory]
    [InlineData("item", "itm_")]
    [InlineData("location", "loc_")]
    [InlineData("location_receipt", "mlocrec_")]
    [InlineData("location_link", "lnk_")]
    [InlineData("location_link_receipt", "mlinkrec_")]
    [InlineData("location_transition", "mltrn_")]
    [InlineData("location_threat", "threat_")]
    public void ExistingIdentityFormatsReplay(string kind, string prefix)
    {
        var value = prefix + "0123456789abcdef0123456789abcdef";
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        Assert.Equal(value, journal.Request(kind, "intake", "operation:0", () => value));
        var replay = SpiritualWoundReplayJournal.CreateReplay(journal.Export().ToJsonString());
        Assert.Equal(value, replay.Request(kind, "intake", "operation:0",
            () => throw new InvalidOperationException("Replay allocated.")));
        Assert.Single(replay.Export());
    }

    /// <summary>
    /// Revalidates location lore bindings when companion authority changes on an otherwise equal input.
    /// </summary>
    [Fact]
    public void LocationCacheBindsCompanionAuthority()
    {
        var raw = MortalLocationTestFixture.CreateRawLocation();
        raw["loreBindings"] = new JsonArray(new JsonObject
        {
            ["kind"] = "codex", ["codexEntryId"] = "codex_intake"
        });
        raw["materialization"]!["sections"]!["loreBindings"] = new JsonObject
        {
            ["disposition"] = "populated", ["reason"] = null
        };
        var input = new MortalLocationAcceptedTurnInput(
            MortalLocationTestFixture.CreateWorldMap(),
            null, MortalLocationIdentityState.CreateEmptyRoot(), null,
            new JsonObject { ["newLocations"] = new JsonArray(raw) }, 42,
            CompanionAuthority: MortalLocationCompanionAuthority.FromCanonicalRoots(
                new JsonObject { ["entries"] = new JsonArray(new JsonObject { ["entryId"] = "codex_intake" }) },
                null, null));
        var cache = new MortalLocationAcceptedTurnPlanCache();
        var first = cache.GetOrBuild(input);
        var changed = cache.GetOrBuild(input with { CompanionAuthority = MortalLocationCompanionAuthority.Empty });
        Assert.True(first.Success);
        Assert.NotSame(first, changed);
        Assert.Contains(changed.Issues, issue => issue.Code?.Contains("lore", StringComparison.Ordinal) == true);
    }
}
