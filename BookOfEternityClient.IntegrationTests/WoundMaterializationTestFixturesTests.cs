using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
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
        Assert.Equal("infection", postApocalyptic.WoundProposal["complications"]![0]!["kind"]!.GetValue<string>());
        Assert.Equal("Риск воспаления", postApocalyptic.WoundProposal["complications"]![0]!["displayName"]!.GetValue<string>());
        Assert.Equal("spiritual_instability", magical.WoundProposal["complications"]![0]!["kind"]!.GetValue<string>());
        Assert.Equal("Резонансная нестабильность", magical.WoundProposal["complications"]![0]!["displayName"]!.GetValue<string>());
        Assert.NotEqual(
            postApocalyptic.WoundProposal["complications"]![0]!["displayName"]!.GetValue<string>(),
            magical.WoundProposal["complications"]![0]!["displayName"]!.GetValue<string>());

        AssertVisibleMortalPresentationIsSettingSpecific(postApocalyptic);
        AssertVisibleMortalPresentationIsSettingSpecific(magical);
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
        Assert.Equal(fixture.BuiltInAssets.DiscoverabilityPointer, fixture.Refs.DiscoverabilityPointer);
        Assert.Equal(5, fixture.FutureProposal["spiritualHealing"]!["tier"]!.GetValue<int>());
    }

    [Fact]
    public void ShiningSeed_UsesCurrentRosterAndFutureHealingRoleProposalWithoutPublicServiceByDefault()
    {
        var fixture = WoundMaterializationTestFixtures.CreateShiningFactionScenario(3);
        var resident = fixture.ResidentRoster["entries"]![0]!.AsObject();
        var profile = fixture.AfterlifeProfiles[AfterlifeEntityProfileState.ProfilesProperty]![0]!.AsObject();

        Assert.Equal("attendant_spirit", resident["residentKind"]!.GetValue<string>());
        Assert.Equal("ascended", resident["ascensionState"]!.GetValue<string>());
        Assert.Equal(fixture.Refs.FactionId, resident["shiningFactionId"]!.GetValue<string>());
        Assert.Equal(ShiningAbodeState.ResidentRoleSocialSupport, resident["residentRole"]!.GetValue<string>());
        Assert.Null(resident["visibleRole"]);
        var faction = fixture.ShiningState["factions"]![0]!.AsObject();
        var leadership = faction["leadership"]!.AsObject();
        Assert.Equal(ShiningAbodeState.HeadActorTypeResident, leadership["headActorType"]!.GetValue<string>());
        Assert.Equal(resident["residentId"]!.GetValue<string>(), leadership["headActorId"]!.GetValue<string>());
        Assert.Equal(fixture.Refs.LocationRef, faction["hallId"]!.GetValue<string>());
        Assert.Equal(resident["guardianId"]!.GetValue<string>(), fixture.Refs.HostGuardianId);
        Assert.Equal(resident["abodeId"]!.GetValue<string>(), fixture.Refs.HostAbodeId);

        var guardians = Assert.IsType<JsonArray>(fixture.GuardianRoot["guardians"]);
        var guardian = Assert.Single(guardians.OfType<JsonObject>(), candidate =>
            candidate["guardianId"]!.GetValue<string>() == resident["guardianId"]!.GetValue<string>());
        Assert.Equal(resident["abodeId"]!.GetValue<string>(), guardian["abode"]!["abodeId"]!.GetValue<string>());
        Assert.Equal("azalia", guardian["sourcePreset"]!["presetId"]!.GetValue<string>());
        Assert.Contains("game_state/meta/guardians.json", fixture.CanonicalWritePaths);

        var residentProfiles = profile.Parent!.AsArray().OfType<JsonObject>()
            .Where(candidate => candidate["actorType"]!.GetValue<string>() == "resident" &&
                                candidate["actorId"]!.GetValue<string>() == fixture.Refs.ResidentId)
            .ToArray();
        Assert.Single(residentProfiles);
        Assert.True(faction["materialization"]!["capabilities"]!["hasResidentAffiliations"]!.GetValue<bool>());
        Assert.Equal("populated", faction["materialization"]!["sections"]!["residentAffiliations"]!["state"]!.GetValue<string>());
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

    [Fact]
    public void Builders_ReturnFreshManifestAndNestedAuthorityGraphs()
    {
        var firstPostApocalyptic = WoundMaterializationTestFixtures.CreatePostApocalypticMortalScenario();
        var secondPostApocalyptic = WoundMaterializationTestFixtures.CreatePostApocalypticMortalScenario();
        var firstMagical = WoundMaterializationTestFixtures.CreateMagicalWorldMortalScenario();
        var secondMagical = WoundMaterializationTestFixtures.CreateMagicalWorldMortalScenario();
        var firstConflict = WoundMaterializationTestFixtures.CreateSpiritualConflictScenario();
        var secondConflict = WoundMaterializationTestFixtures.CreateSpiritualConflictScenario();
        var firstElyara = WoundMaterializationTestFixtures.CreateElyaraScenario();
        var secondElyara = WoundMaterializationTestFixtures.CreateElyaraScenario();
        var firstShining = WoundMaterializationTestFixtures.CreateShiningFactionScenario();
        var secondShining = WoundMaterializationTestFixtures.CreateShiningFactionScenario();

        Assert.NotSame(firstPostApocalyptic.WoundProposal, secondPostApocalyptic.WoundProposal);
        Assert.NotSame(firstPostApocalyptic.AuthorityRoots.PlayerInventory, secondPostApocalyptic.AuthorityRoots.PlayerInventory);
        Assert.NotSame(firstMagical.WoundProposal, secondMagical.WoundProposal);
        Assert.NotSame(firstMagical.AuthorityRoots.PlayerInventory, secondMagical.AuthorityRoots.PlayerInventory);
        Assert.NotSame(firstPostApocalyptic.AuthorityRoots.PlayerInventory, firstMagical.AuthorityRoots.PlayerInventory);
        Assert.NotSame(firstConflict.AfterlifeProfiles, secondConflict.AfterlifeProfiles);
        Assert.NotSame(firstConflict.FutureProposal, secondConflict.FutureProposal);
        Assert.NotSame(firstElyara.AfterlifeProfiles, secondElyara.AfterlifeProfiles);
        Assert.NotSame(firstElyara.FutureProposal, secondElyara.FutureProposal);
        Assert.NotSame(firstShining.ShiningState, secondShining.ShiningState);
        Assert.NotSame(firstShining.FutureProposal, secondShining.FutureProposal);

        firstPostApocalyptic.WoundProposal["display"]!["visibleSymptoms"]![0] = "mutated";
        firstMagical.AuthorityRoots.PlayerInventory["items"]![0]!["name"] = "mutated";
        firstConflict.FutureProposal["dangerEnvelope"]!["dangerMode"] = "mutated";
        firstElyara.BuiltInAssets.Manifest["displayName"] = "mutated";
        firstShining.ResidentRoster["entries"]![0]!["displayName"] = "mutated";

        Assert.NotEqual("mutated", secondPostApocalyptic.WoundProposal["display"]!["visibleSymptoms"]![0]!.GetValue<string>());
        Assert.NotEqual("mutated", secondMagical.AuthorityRoots.PlayerInventory["items"]![0]!["name"]!.GetValue<string>());
        Assert.NotEqual("mutated", secondConflict.FutureProposal["dangerEnvelope"]!["dangerMode"]!.GetValue<string>());
        Assert.NotEqual("mutated", secondElyara.BuiltInAssets.Manifest["displayName"]!.GetValue<string>());
        Assert.NotEqual("mutated", secondShining.ResidentRoster["entries"]![0]!["displayName"]!.GetValue<string>());
    }

    [Fact]
    public void CurrentCanonicalRoots_PassTheirOwningValidators()
    {
        var spiritual = WoundMaterializationTestFixtures.CreateSpiritualConflictScenario();
        var elyara = WoundMaterializationTestFixtures.CreateElyaraScenario();
        var shining = WoundMaterializationTestFixtures.CreateShiningFactionScenario();

        AssertCanonicalRootHasNoErrors("ValidateAfterlifeEntityProfileStateFile", spiritual.AfterlifeProfiles, AfterlifeEntityProfileState.StatePath);
        AssertCanonicalRootHasNoErrors("ValidateAfterlifeEntityProfileStateFile", elyara.AfterlifeProfiles, AfterlifeEntityProfileState.StatePath);
        AssertCanonicalRootHasNoErrors("ValidateAfterlifeEntityProfileStateFile", shining.AfterlifeProfiles, AfterlifeEntityProfileState.StatePath);
        AssertCanonicalRootHasNoErrors("ValidateGuardianAbodeResidentsStateFile", shining.ResidentRoster, GuardianAbodeResidentState.StatePath);
        AssertCanonicalRootHasNoErrors("ValidateShiningAbodeStateFile", shining.ShiningState, ShiningAbodeState.StatePath);
        AssertCanonicalRootHasNoErrors("ValidateGuardianStateData", shining.GuardianRoot, "game_state/meta/guardians.json");
    }

    private static void AssertVisibleMortalPresentationIsSettingSpecific(MortalWoundScenarioFixture fixture)
    {
        var item = fixture.AuthorityRoots.PlayerInventory["items"]![0]!.AsObject();
        var provider = fixture.AuthorityRoots.ProviderAuthority["NPCsInScene"]![0]!.AsObject();
        var location = fixture.AuthorityRoots.CurrentLocation;

        Assert.False(string.IsNullOrWhiteSpace(item["description"]!.GetValue<string>()));
        Assert.Equal(location["name"]!.GetValue<string>(), provider["currentLocationName"]!.GetValue<string>());
        foreach (var visible in new[]
                 {
                     item["description"]!.GetValue<string>(),
                     provider["role"]!.GetValue<string>(), provider["summary"]!.GetValue<string>(),
                     provider["worldview"]!.GetValue<string>(), provider["personalityArchetype"]!.GetValue<string>(),
                     provider["currentLocationName"]!.GetValue<string>()
                 })
        {
            Assert.DoesNotContain("test", visible, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("setting-neutral", visible, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void AssertCanonicalRootHasNoErrors(string methodName, JsonObject root, string statePath)
    {
        using var document = JsonDocument.Parse(root.ToJsonString());
        var validator = new ValidationService(
            new FileSystemManager(Path.GetTempPath(), NullLogger<FileSystemManager>.Instance),
            NullLogger<ValidationService>.Instance);
        var method = typeof(ValidationService).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var issues = new List<ValidationIssue>();
        method!.Invoke(validator, new object[] { document.RootElement, statePath, issues });
        var errors = issues.Where(issue => issue.Severity == IssueSeverity.Error).ToArray();
        Assert.True(errors.Length == 0, string.Join(
            Environment.NewLine,
            errors.Select(issue => $"{issue.Code}: {issue.FilePath}; expected={issue.Expected}; actual={issue.Actual}")));
    }
}
