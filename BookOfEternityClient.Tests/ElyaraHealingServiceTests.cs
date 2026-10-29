using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ElyaraHealingServiceTests
{
    [Fact]
    public async Task FreshSeedMakesElyaraDiscoverableWithoutReplacingTheChosenGuardian()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-elyara-seed-" + Guid.NewGuid().ToString("N"));
        try
        {
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            var library = new SystemGuardianLibraryService(files, NullLogger<SystemGuardianLibraryService>.Instance);
            var chosen = new SystemGuardianLibraryService.SystemGuardianPresetDescriptor
                { PresetId = "chosen", DisplayName = "Выбранный Хранитель", Domain = "Knowledge", AbodeName = "Выбранная Обитель" };
            var elyara = new SystemGuardianLibraryService.SystemGuardianPresetDescriptor
                { PresetId = "elyara", DisplayName = "Элиара Последней Раны", Domain = "Healing", AbodeName = "Лазарет Незаживающего Света" };
            var now = new DateTimeOffset(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);
            var guardians = library.BuildCanonicalGuardianRootForFreshNewGame(chosen, "Душа", 1, now);
            var profiles = library.BuildAfterlifeEntityProfileRootForFreshNewGame(chosen, "Душа", 1, now);
            var active = guardians["activeGuardian"]!.ToJsonString();
            var current = guardians["chaosSeaNavigation"]!["currentGuardianId"]!.GetValue<string>();
            // Already loaded immutable preset DATA avoids directory enumeration or a second manifest read.
            await library.SeedElyaraForFreshNewGameAsync(guardians, profiles, "Душа", 1, now, elyara);
            Assert.Equal(active, guardians["activeGuardian"]!.ToJsonString());
            Assert.Equal(current, guardians["chaosSeaNavigation"]!["currentGuardianId"]!.GetValue<string>());
            Assert.Contains(guardians["chaosSeaNavigation"]!["discoveredAbodes"]!.AsArray(), n =>
                n?.GetValue<string>() == AfterlifeWoundTreatmentAuthority.ElyaraAbodeId);
            var profile = profiles["profiles"]!.AsArray().OfType<JsonObject>().Single(AfterlifeHealingServiceContract.IsElyaraProfile);
            Assert.Equal(5, profile["standardArts"]!["spiritual_healing"]!.GetValue<int>());
            Assert.Equal("alive", profile["lifeState"]!.GetValue<string>());
            Assert.NotNull(AfterlifeHealingServiceContract.ReadPublicCurrencyProfile(AfterlifeHealingServiceTestData.Provider(), profile));
            profile["realm"] = "Shining Abode"; profile["locationId"] = "shining_current"; profile["lifeState"] = "dead";
            profile["healingServiceProfile"]!["realm"] = "shining_abode"; profile["healingServiceProfile"]!["locationRef"] = "shining_current";
            var accepted = profiles.ToJsonString();
            await library.SeedElyaraForFreshNewGameAsync(guardians, profiles, "Душа", 1, now, elyara);
            Assert.Equal(accepted, profiles.ToJsonString()); // Never reseed a present accepted identity.
            Assert.Equal(active, guardians["activeGuardian"]!.ToJsonString());
            Assert.Single(profiles["profiles"]!.AsArray().OfType<JsonObject>().Where(AfterlifeHealingServiceContract.IsElyaraProfile));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("tier")][InlineData("price")][InlineData("life")][InlineData("service")]
    [InlineData("alias")][InlineData("realm")][InlineData("location")]
    public void CanonicalUpsertRefusesElyaraInvariantLoss(string change)
    {
        var previous = AfterlifeHealingServiceTestData.Actor(); var next = previous.DeepClone().AsObject();
        switch (change)
        {
            case "tier": next["standardArts"]!["spiritual_healing"] = 4; break;
            case "price": next["healingServiceProfile"]!["priceMultiplierPercent"] = 99; break;
            case "life": next.Remove("lifeState"); break;
            case "service": next.Remove("healingServiceProfile"); break;
            case "alias": next["actorRef"] = "ELYARA_ALIAS"; break;
            case "realm": next["healingServiceProfile"]!["realm"] = "shining_abode"; break;
            case "location": next["locationId"] = "changed_without_service_location"; break;
        }
        var rows = new JsonArray(previous); var before = rows.ToJsonString();
        Assert.Throws<InvalidDataException>(() => AfterlifeEntityProfileState.UpsertProfile(rows, next));
        Assert.Equal(before, rows.ToJsonString());
    }

    [Fact]
    public void RelocationAndDeathRemainCanonicalAndCannotBeResurrectedByAnUpsert()
    {
        var actor = AfterlifeHealingServiceTestData.Actor(); var rows = new JsonArray(actor);
        var moved = actor.DeepClone().AsObject(); moved["realm"] = "Shining Abode"; moved["locationId"] = "shining_current";
        moved["healingServiceProfile"]!["realm"] = "shining_abode"; moved["healingServiceProfile"]!["locationRef"] = "shining_current";
        AfterlifeEntityProfileState.UpsertProfile(rows, moved);
        var dead = rows[0]!.DeepClone().AsObject(); dead["lifeState"] = "dead";
        AfterlifeEntityProfileState.UpsertProfile(rows, dead);
        var previous = new JsonObject { ["schemaVersion"] = 1, ["profiles"] = rows };
        var projected = AfterlifeEntityProfileState.ProjectCanonicalRoot(previous, previous);
        var final = Assert.Single(projected["profiles"]!.AsArray())!;
        Assert.Equal("dead", final["lifeState"]!.GetValue<string>());
        Assert.Equal("Shining Abode", final["realm"]!.GetValue<string>());
        Assert.Equal("shining_current", final["locationId"]!.GetValue<string>());
        var alive = final.DeepClone().AsObject(); alive["lifeState"] = "alive";
        Assert.Contains("resurrected", Assert.Throws<InvalidDataException>(() =>
            AfterlifeEntityProfileState.UpsertProfile(projected["profiles"]!.AsArray(), alive)).Message);
    }

    [Fact]
    public void ADeadCanonicalHealerCannotUseThePrivateAidResolverAsAFreeBypass()
    {
        var (wound, _) = AfterlifeHealingServiceTestData.WoundAndCheck();
        var actor = AfterlifeHealingServiceTestData.Actor(); actor["lifeState"] = "dead";
        var result = SpiritualHealingResolver.PrepareSource(new(wound, AfterlifeHealingServiceTestData.Provider(), actor, null,
            new EffectMechanicsSnapshot(true, [], [], [])));
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Code == "spiritual_healing_healer_not_alive");
    }
}
