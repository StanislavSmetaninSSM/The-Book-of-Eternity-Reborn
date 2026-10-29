using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ElyaraCurrentLocationContractTests
{
    private const string Generation = "generation_elyara_visit";
    private static readonly string Fingerprint = "sha256:" + new string('a', 64);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CurrentLocationSelectsItsActualHostNotElyarasHome(bool relocated)
    {
        var input = Inputs(); var actor = Actor(input);
        if (relocated)
        {
            var remote = Guardian("guardian_remote_host", "abode_remote", "Другая открытая Обитель");
            input.Guardians!["guardians"]!.AsArray().Add(remote);
            input.Guardians["chaosSeaNavigation"]!["discoveredAbodes"]!.AsArray().Add("abode_remote");
            actor["locationId"] = "abode_remote"; actor["healingServiceProfile"]!["locationRef"] = "abode_remote";
        }
        var before = Serialize(input);
        var plan = ElyaraVisitService.Project(input, Generation, Fingerprint);
        Assert.Equal(ElyaraVisitKind.ChaosTravel, plan.Kind);
        var intent = Assert.IsType<ElyaraVisitCommandState>(plan.State).Intent;
        Assert.Equal(relocated ? "guardian_remote_host" : AfterlifeWoundTreatmentAuthority.ElyaraId, intent.TargetGuardianId);
        Assert.Equal(relocated ? "abode_remote" : AfterlifeWoundTreatmentAuthority.ElyaraAbodeId, intent.TargetAbodeId);
        Assert.Equal("abode_host", intent.SourceAbodeId);
        Assert.Equal(intent, ElyaraVisitService.ParseAction(ElyaraVisitService.ActionFor(intent)));
        Assert.Equal(before, Serialize(input));
        var json = JsonSerializer.Serialize(ExplorerElyaraCommandResultBuilder.Build(plan));
        foreach (var secret in new[] { "sourceFingerprint", "guard_system_elyara_001", "abode_remote", "sha256:", Generation })
            Assert.DoesNotContain(secret, json);
    }

    [Theory]
    [InlineData("dead")] [InlineData("hidden")] [InlineData("duplicate_realm")]
    [InlineData("identity_alias")] [InlineData("service_location")]
    [InlineData("unknown_destination")] [InlineData("undiscovered")]
    [InlineData("foreign_current_scope")] [InlineData("conflict")] [InlineData("unavailable_service")]
    [InlineData("current_location_alias")]
    public void UnprovedCurrentLifeIdentityLocalityOrReachabilityNeverTravels(string change)
    {
        var input = Inputs(); var actor = Actor(input);
        switch (change)
        {
            case "dead": actor["lifeState"] = "dead"; break;
            case "hidden": actor["isPlayerVisible"] = false; break;
            case "duplicate_realm":
                var copy = actor.DeepClone().AsObject(); copy["realm"] = "Shining Abode";
                copy["healingServiceProfile"]!["realm"] = "shining_abode";
                input.Profiles!["profiles"]!.AsArray().Add(copy); break;
            case "identity_alias": actor["actorRef"] = "foreign_guardian"; break;
            case "service_location": actor["healingServiceProfile"]!["locationRef"] = "foreign_place"; break;
            case "unknown_destination": actor["locationId"] = "unknown_place"; actor["healingServiceProfile"]!["locationRef"] = "unknown_place"; break;
            case "undiscovered": input.Guardians!["chaosSeaNavigation"]!["discoveredAbodes"] = new JsonArray("abode_host"); break;
            case "foreign_current_scope": input.Guardians!["chaosSeaNavigation"]!["currentGuardianId"] = "foreign_guardian"; break;
            case "conflict": input = input with { Conflict = new JsonObject { ["activeConflict"] = new JsonObject() } }; break;
            case "unavailable_service": actor["healingServiceProfile"]!["availability"] = "unavailable"; break;
            case "current_location_alias": actor["currentLocationId"] = "foreign_place"; break;
        }
        var before = Serialize(input);
        var plan = ElyaraVisitService.Project(input, Generation, Fingerprint);
        Assert.Null(plan.State); Assert.DoesNotContain(plan.Kind, new[] { ElyaraVisitKind.ChaosTravel, ElyaraVisitKind.CoLocated });
        if (change is "dead" or "hidden" or "duplicate_realm" or "identity_alias") Assert.False(ElyaraVisitService.CanShowEntry(input.Profiles));
        Assert.Equal(before, Serialize(input));
    }

    [Theory]
    [InlineData("local")] [InlineData("remote")] [InlineData("missing_hall")]
    [InlineData("alias")] [InlineData("duplicate_hall")] [InlineData("package")]
    public void ShiningCompanionUsesCurrentHallAndNeverInventsHallTravel(string change)
    {
        var input = Inputs(); var actor = Actor(input);
        input.Soul!["currentRealm"] = "Shining Abode";
        actor["realm"] = "Shining Abode"; actor["locationId"] = "hall_current";
        actor["healingServiceProfile"]!["realm"] = "shining_abode";
        actor["healingServiceProfile"]!["locationRef"] = "hall_current";
        var hall = new JsonObject { ["hallId"] = "hall_current", ["hallName"] = "Текущий зал" };
        input = input with { Shining = new JsonObject { ["currentHallId"] = "hall_current", ["halls"] = new JsonArray(hall) } };
        if (change == "remote") { actor["locationId"] = "hall_remote"; actor["healingServiceProfile"]!["locationRef"] = "hall_remote"; }
        if (change == "missing_hall") input.Shining!["halls"] = new JsonArray();
        if (change == "alias") input.Shining!["selectedHallId"] = "hall_foreign";
        if (change == "duplicate_hall") input.Shining!["halls"]!.AsArray().Add(hall.DeepClone());
        if (change == "package") input.Shining!["preparedIncarnationPackage"] = new JsonObject();
        var before = Serialize(input);
        var plan = ElyaraVisitService.Project(input, Generation, Fingerprint);
        Assert.Equal(change == "local" ? ElyaraVisitKind.CoLocated : change == "remote" || change == "package"
            ? ElyaraVisitKind.Blocked : ElyaraVisitKind.Unavailable, plan.Kind);
        Assert.Null(plan.State); Assert.Equal(before, Serialize(input));
    }

    [Theory]
    [InlineData("stripped")] [InlineData("duplicate")] [InlineData("target")]
    [InlineData("payload")] [InlineData("suffix")]
    public void AlteredVisitMarkerOrOrdinaryTargetCannotBind(string change)
    {
        var intent = ElyaraVisitService.Project(Inputs(), Generation, Fingerprint).State!.Intent;
        var action = ElyaraVisitService.ActionFor(intent);
        action = change switch
        {
            "stripped" => action[(action.IndexOf('\n') + 1)..],
            "duplicate" => action + "\n" + action,
            "target" => action.Replace("targetGuardianId=" + intent.TargetGuardianId, "targetGuardianId=foreign_guardian", StringComparison.Ordinal),
            "payload" => action.Replace(ElyaraVisitService.Marker + " ", ElyaraVisitService.Marker + " !", StringComparison.Ordinal),
            _ => action + " extra"
        };
        Assert.ThrowsAny<Exception>(() => ElyaraVisitService.ParseAction(action));
    }

    private static JsonObject Actor(ElyaraVisitInput input) => input.Profiles!["profiles"]![0]!.AsObject();
    private static string Serialize(ElyaraVisitInput input) => JsonSerializer.Serialize(input);
    private static JsonObject Guardian(string id, string abode, string name) => new()
    { ["guardianId"] = id, ["abode"] = new JsonObject { ["abodeId"] = abode, ["name"] = name, ["isDiscovered"] = true } };
    private static ElyaraVisitInput Inputs()
    {
        var actor = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile("guardian", AfterlifeWoundTreatmentAuthority.ElyaraId, "Chaos Sea", 42);
        actor["lifeState"] = "alive"; actor["locationId"] = AfterlifeWoundTreatmentAuthority.ElyaraAbodeId;
        actor["standardArts"]!["spiritual_healing"] = 5;
        actor["healingServiceProfile"] = AfterlifeHealingServiceTestData.Actor()["healingServiceProfile"]!.DeepClone();
        var host = Guardian("guardian_host", "abode_host", "Тихая Обитель");
        return new(new() { ["currentRealm"] = "Chaos Sea" }, new() { ["profiles"] = new JsonArray(actor) },
            new() { ["guardians"] = new JsonArray(Guardian(AfterlifeWoundTreatmentAuthority.ElyaraId, AfterlifeWoundTreatmentAuthority.ElyaraAbodeId, "Лазарет"), host),
                ["activeGuardian"] = host.DeepClone(), ["chaosSeaNavigation"] = new JsonObject
                { ["currentAbodeId"] = "abode_host", ["currentGuardianId"] = "guardian_host",
                    ["discoveredAbodes"] = new JsonArray("abode_host", AfterlifeWoundTreatmentAuthority.ElyaraAbodeId) } }, null, null);
    }
}
