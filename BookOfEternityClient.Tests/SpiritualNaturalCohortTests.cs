using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SpiritualNaturalCohortTests
{
    [Fact]
    public void PersistentCurrentRealmOwnersUseTheirOwnTierRegardlessOfVisibility()
    {
        var profiles = new JsonArray();
        foreach (var (kind, actorType, tier, realm) in new[]
        {
            ("guardian", "guardian", 0, "chaos_sea"), ("resident", "resident", 5, "chaos_sea"),
            ("afterlife_actor", "custom_afterlife_actor", 2, "chaos_sea"),
            ("radiant_actor", "radiant_actor", 4, "shining_abode")
        })
        {
            var wound = WoundContractTestData.CreateSpiritualActiveWound("wound_" + kind, realm, kind, "owner_" + kind,
                WoundCarrierCatalog.AfterlifeProfilesPath);
            wound["display"]!["visibility"] = "hidden";
            profiles.Add(new JsonObject
            {
                ["actorId"] = "owner_" + kind, ["actorType"] = actorType,
                ["realm"] = realm == "chaos_sea" ? "Chaos Sea" : "Shining Abode",
                ["standardArts"] = new JsonObject { ["spiritual_healing"] = tier },
                ["activeWounds"] = new JsonArray(wound), ["activeEffects"] = new JsonArray()
            });
        }
        var root = new JsonObject { ["profiles"] = profiles };
        var catalog = WoundCarrierCatalog.Build(new(null, null, null, null, root));
        Assert.Empty(catalog.Issues);
        var members = SpiritualHealingRequestProjection.ReadNaturalMembers(catalog, root, null, "chaos_sea");
        Assert.Equal(new[] { "afterlife_actor", "guardian", "resident" }, members.Select(m => m.Wound.Owner.OwnerKind));
        Assert.Equal(new[] { 2, 0, 5 }, members.Select(m => m.HealingTier));
        var projection = SpiritualHealingRequestProjection.CreateNatural("chaos_sea", 1, "sha256:" + new string('a', 64), members);
        Assert.Equal(3, projection["cohort"]!.AsArray().Count);
        Assert.Null(projection["attemptId"]);
        Assert.Null(projection["roll"]);
        var program = SpiritualHealingRequestProjection.BuildNaturalProgram("chaos_sea", 1, members);
        Assert.DoesNotContain(program.Stages, s => s.AttemptId is not null || s.Kind == "treat");
        Assert.Equal(3, program.Stages.Single(s => s.WoundId == "wound_afterlife_actor").Progress);
        Assert.Equal(1, program.Stages.Single(s => s.WoundId == "wound_guardian").Progress);
        Assert.Equal("heal", program.Stages.Last(s => s.WoundId == "wound_resident").Kind);
        Assert.DoesNotContain(program.Stages, s => s.WoundId == "wound_radiant_actor");

        profiles[1]!["standardArts"]!.AsObject().Remove("spiritual_healing");
        var missing = Assert.Throws<InvalidDataException>(() =>
            SpiritualHealingRequestProjection.ReadNaturalMembers(catalog, root, null, "chaos_sea"));
        Assert.Contains("explicit healing tier", missing.Message);
    }
}
