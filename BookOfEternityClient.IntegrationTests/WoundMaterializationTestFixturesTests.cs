using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundMaterializationTestFixturesTests
{
    [Fact]
    public void MortalSeeds_ExposeIndependentCurrentAuthorityRootsAndFutureProposals()
    {
        var postApocalyptic = WoundMaterializationTestFixtures.CreatePostApocalypticMortalScenario();
        var magical = WoundMaterializationTestFixtures.CreateMagicalWorldMortalScenario();

        Assert.NotSame(postApocalyptic.WoundProposal, magical.WoundProposal);
        Assert.NotEqual(postApocalyptic.Refs.WoundRef, magical.Refs.WoundRef);
        Assert.Equal("mortal_world", postApocalyptic.Expected.Realm);
        Assert.Equal("mortal_world", magical.Expected.Realm);
        Assert.IsType<JsonArray>(postApocalyptic.AuthorityRoots.ProviderAuthority["NPCsInScene"]);
        Assert.IsType<JsonObject>(postApocalyptic.AuthorityRoots.CurrentLocation);
        Assert.IsType<JsonObject>(postApocalyptic.AuthorityRoots.LocationIdentityIndex);
        Assert.Equal(postApocalyptic.Refs.HiddenRouteId,
            postApocalyptic.WoundProposal["treatment"]!["diagnosisPaths"]![0]!["reveals"]![0]!
                .GetValue<string>()["route:".Length..]);
        Assert.NotEqual(postApocalyptic.Refs.ComplicationRef, magical.Refs.ComplicationRef);
        Assert.NotEqual(postApocalyptic.Refs.LocationAuthorityRef, magical.Refs.LocationAuthorityRef);
    }

    [Fact]
    public void SpiritualConflictSeed_SeparatesCurrentProfilesFromFutureArtAndDangerProposals()
    {
        var fixture = WoundMaterializationTestFixtures.CreateSpiritualConflictScenario("hostile", 2, 3, true);
        var profiles = fixture.AfterlifeProfiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray();
        var player = profiles[0]!.AsObject();

        Assert.Equal(1, fixture.AfterlifeProfiles["schemaVersion"]!.GetValue<int>());
        Assert.Equal("Chaos Sea", player["realm"]!.GetValue<string>());
        Assert.IsAssignableFrom<JsonValue>(player["standardArts"]!["guard"]);
        Assert.Null(player["standardArts"]!["spiritual_resilience"]);
        Assert.NotNull(fixture.FutureProposal);
        Assert.Equal("hostile", fixture.FutureProposal["dangerEnvelope"]!["dangerMode"]!.GetValue<string>());
        Assert.NotNull(fixture.FutureProposal["priorTrainingEscalation"]);
        Assert.Equal("player_soul", fixture.Refs.PlayerProfileId);
    }

    [Fact]
    public void ElyaraSeed_UsesCurrentCanonicalProfileRootAndSeparateBuiltInFutureProposal()
    {
        var fixture = WoundMaterializationTestFixtures.CreateElyaraScenario();
        var profiles = fixture.AfterlifeProfiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray();
        var elyara = profiles[0]!.AsObject();

        Assert.Equal(1, fixture.AfterlifeProfiles["schemaVersion"]!.GetValue<int>());
        Assert.Equal("Chaos Sea", elyara["realm"]!.GetValue<string>());
        Assert.IsAssignableFrom<JsonValue>(elyara["standardArts"]!["guard"]);
        Assert.Null(elyara["healingServiceProfile"]);
        Assert.True(fixture.BuiltInAssets.Manifest["alwaysAvailable"]!.GetValue<bool>());
        Assert.Equal("/alwaysAvailable", fixture.BuiltInAssets.DiscoverabilityPointer);
        Assert.Equal(5, fixture.FutureProposal["spiritualHealing"]!["tier"]!.GetValue<int>());
    }

    [Fact]
    public void ShiningSeed_UsesCurrentRosterAndFutureHealingRoleProposalWithoutPublicServiceByDefault()
    {
        var fixture = WoundMaterializationTestFixtures.CreateShiningFactionScenario(3);
        var resident = fixture.ResidentRoster["entries"]![0]!.AsObject();
        var profile = fixture.AfterlifeProfiles[AfterlifeEntityProfileState.ProfilesProperty]![0]!.AsObject();

        Assert.Equal("attendant_spirit", resident["residentKind"]!.GetValue<string>());
        Assert.IsType<JsonObject>(resident["abodeDisposition"]);
        Assert.Equal("Shining Abode", profile["realm"]!.GetValue<string>());
        Assert.Null(resident["primaryRole"]);
        Assert.Null(profile["healingServiceProfile"]);
        Assert.Equal("healing_support", fixture.FutureProposal["residentRole"]!["key"]!.GetValue<string>());
        Assert.Equal("absent", fixture.Expected.ServiceVisibility);
    }

    [Fact]
    public void DangerModes_UseIndependentConflictEvidence()
    {
        var training = WoundMaterializationTestFixtures.CreateSpiritualConflictScenario("training");
        var annihilation = WoundMaterializationTestFixtures.CreateSpiritualConflictScenario("annihilation");

        Assert.NotEqual(training.Refs.ConflictId, annihilation.Refs.ConflictId);
        Assert.NotEqual(training.Refs.EventRef, annihilation.Refs.EventRef);
        Assert.NotEqual(training.Refs.SealedD20Ref, annihilation.Refs.SealedD20Ref);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WoundMaterializationTestFixtures.CreateShiningFactionScenario(0));
    }
}
