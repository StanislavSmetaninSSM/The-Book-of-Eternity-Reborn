using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
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
        var command = ExplorerCommandCatalog.Require("elyara_visit");
        var metadata = BrowserPlayerCommandMenuBuilder.GetCoverageMetadata(command);
        Assert.Equal("afterlife", metadata.SectionId);
        Assert.Equal(ElyaraVisitService.Label, metadata.Label);
        Assert.Equal("guided-form", metadata.FormMode);
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
    [InlineData("sealed")] [InlineData("unknown_availability")]
    public void ShiningCompanionUsesCurrentHallAndNeverInventsHallTravel(string change)
    {
        var input = Inputs(); var actor = Actor(input);
        input.Soul!["currentRealm"] = "Shining Abode";
        actor["realm"] = "Shining Abode"; actor["locationId"] = "hall_current"; actor["locationName"] = "Текущий зал";
        actor["healingServiceProfile"]!["realm"] = "shining_abode";
        actor["healingServiceProfile"]!["locationRef"] = "hall_current";
        var hall = new JsonObject { ["hallId"] = "hall_current", ["hallName"] = "Текущий зал" };
        input = input with { Shining = new JsonObject { ["availability"] = ShiningAbodeState.AvailabilityActive, ["currentHallId"] = "hall_current", ["halls"] = new JsonArray(hall) } };
        if (change == "remote") { actor["locationId"] = "hall_remote"; actor["healingServiceProfile"]!["locationRef"] = "hall_remote"; }
        if (change == "missing_hall") input.Shining!["halls"] = new JsonArray();
        if (change == "alias") input.Shining!["selectedHallId"] = "hall_foreign";
        if (change == "duplicate_hall") input.Shining!["halls"]!.AsArray().Add(hall.DeepClone());
        if (change == "package") input.Shining!["preparedIncarnationPackage"] = new JsonObject();
        if (change == "sealed") input.Shining!["availability"] = ShiningAbodeState.AvailabilitySealedUntilNextAscension;
        if (change == "unknown_availability") input.Shining!.Remove("availability");
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

    [Theory]
    [InlineData("valid")] [InlineData("forged_original")] [InlineData("dead")]
    [InlineData("moved")] [InlineData("duplicate")] [InlineData("wrong_arrival")]
    [InlineData("realm")]
    public void AcceptanceRequiresOriginalReachabilityAndSameCurrentAliveProvider(string change)
    {
        var original = Inputs();
        var fingerprint = ElyaraVisitService.DataFingerprint(original, Generation);
        var intent = ElyaraVisitService.Project(original, Generation, fingerprint).State!.Intent;
        var current = Inputs();
        var destination = current.Guardians!["guardians"]![0]!.AsObject();
        current.Guardians["activeGuardian"] = destination.DeepClone();
        current.Guardians["chaosSeaNavigation"]!["currentAbodeId"] = intent.TargetAbodeId;
        current.Guardians["chaosSeaNavigation"]!["currentGuardianId"] = intent.TargetGuardianId;
        if (change == "forged_original") original.Guardians!["chaosSeaNavigation"]!["discoveredAbodes"] = new JsonArray("abode_host");
        if (change == "dead") Actor(current)["lifeState"] = "dead";
        if (change == "moved") { Actor(current)["locationId"] = "other_place"; Actor(current)["healingServiceProfile"]!["locationRef"] = "other_place"; }
        if (change == "duplicate") current.Profiles!["profiles"]!.AsArray().Add(Actor(current).DeepClone());
        if (change == "wrong_arrival") current.Guardians["chaosSeaNavigation"]!["currentAbodeId"] = "abode_host";
        if (change == "realm") current.Soul!["currentRealm"] = "Shining Abode";
        var before = Serialize(original) + Serialize(current);
        if (change == "valid") ElyaraVisitService.RequireAccepted(intent, original, current);
        else Assert.Throws<InvalidDataException>(() => ElyaraVisitService.RequireAccepted(intent, original, current));
        Assert.Equal(before, Serialize(original) + Serialize(current));
    }

    [Fact]
    public void ComparisonDataIsEncodingIndependentButEveryRootAndGenerationParticipates()
    {
        var input = Inputs();
        var fingerprint = ElyaraVisitService.DataFingerprint(input, Generation);
        var state = ElyaraVisitService.Project(input, Generation, fingerprint).State!;
        var exported = ElyaraVisitService.ActionFor(state.Intent);
        input.Soul!["unrelatedVisibleData"] = 1;
        Assert.NotEqual(fingerprint, ElyaraVisitService.DataFingerprint(input, Generation));
        Assert.Equal(exported, ElyaraVisitService.ActionFor(state.Intent)); // No mutable root escapes in intent.
        input.Soul.Remove("unrelatedVisibleData");
        var reordered = new JsonObject(input.Guardians!.Reverse().Select(p =>
            new KeyValuePair<string, JsonNode?>(p.Key, p.Value?.DeepClone())));
        Assert.Equal(fingerprint, ElyaraVisitService.DataFingerprint(input with { Guardians = reordered }, Generation));
        Assert.NotEqual(fingerprint, ElyaraVisitService.DataFingerprint(input, Generation + "_replaced"));
        Assert.NotEqual(fingerprint, ElyaraVisitService.DataFingerprint(input with { Conflict = new JsonObject() }, Generation));
        var initialized = AfterlifeSpiritualConflictState.CreateDefaultRoot();
        Assert.Equal(fingerprint, ElyaraVisitService.DataFingerprint(input with { Conflict = initialized }, Generation));
        initialized["recentConflicts"]!.AsArray().Add(new JsonObject { ["conflictId"] = "previous_conflict" });
        Assert.NotEqual(fingerprint, ElyaraVisitService.DataFingerprint(input with { Conflict = initialized }, Generation));
        initialized = AfterlifeSpiritualConflictState.CreateDefaultRoot(); initialized["foreignUnknownField"] = true;
        Assert.NotEqual(fingerprint, ElyaraVisitService.DataFingerprint(input with { Conflict = initialized }, Generation));
    }

    [Theory]
    [InlineData("valid")] [InlineData("foreign_provider")] [InlineData("mixed_realm")]
    public void ShiningQuoteUsesExistingResolverAndOnlyExactSameRealmElyara(string change)
    {
        var input = Inputs(); var actor = Actor(input);
        actor["realm"] = "Shining Abode"; actor["locationId"] = "hall_current";
        actor["healingServiceProfile"]!["realm"] = "shining_abode";
        actor["healingServiceProfile"]!["locationRef"] = "hall_current";
        var id = change == "foreign_provider" ? "guardian_foreign" : AfterlifeWoundTreatmentAuthority.ElyaraId;
        actor["actorId"] = id; actor["healingServiceProfile"]!["providerId"] = id;
        var provider = new WoundOwnerCoordinate("shining_abode", "guardian", id, WoundCarrierCatalog.AfterlifeProfilesPath);
        var profile = Assert.IsType<AfterlifeHealingServiceProfile>(AfterlifeHealingServiceContract.ReadPublicCurrencyProfile(provider, actor));
        var node = WoundContractTestData.CreateSpiritualActiveWound(realm: change == "mixed_realm" ? "chaos_sea" : "shining_abode",
            ownerId: "player_soul", carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath);
        var parsed = WoundMaterializationContract.Parse(node.ToJsonString(), "shining.quote.fixture");
        Assert.True(parsed.IsValid);
        var prepared = SpiritualHealingResolver.PrepareSource(new(parsed.Wound!, provider, actor, null,
            new EffectMechanicsSnapshot(true, [], [], [])));
        if (!prepared.IsValid) { Assert.Equal("mixed_realm", change); return; }
        var quote = AfterlifeHealingServiceContract.CreateQuote(profile, parsed.Wound!, prepared.Source!, "hall_current");
        if (change == "valid")
        {
            Assert.NotNull(quote); Assert.Equal(50, quote.Amount);
            Assert.Equal(quote, AfterlifeHealingServiceContract.ParseQuote(quote.ToJson()));
            var soul = new JsonObject { ["inkFeathers"] = new JsonObject { ["current"] = 500, ["total"] = 900 } };
            var payment = Assert.IsType<AfterlifeHealingServicePayment>(AfterlifeHealingServicePlanner.PreparePayment(quote, "attempt_shining_quote", soul));
            Assert.Equal(payment, AfterlifeHealingServicePlanner.ParsePayment(payment.ToJson(), quote, "attempt_shining_quote"));
            var corrupt = quote.ToJson(); corrupt["provider"]!["extra"] = true;
            Assert.Throws<InvalidDataException>(() => AfterlifeHealingServiceContract.ParseQuote(corrupt));
            corrupt = quote.ToJson(); corrupt["patient"]!["realm"] = "chaos_sea";
            Assert.Throws<InvalidDataException>(() => AfterlifeHealingServiceContract.ParseQuote(corrupt));
            corrupt = payment.ToJson(); corrupt["payer"]!["ownerId"] = "foreign_patient";
            Assert.Throws<InvalidDataException>(() => AfterlifeHealingServicePlanner.ParsePayment(corrupt, quote, "attempt_shining_quote"));
            foreach (var (field, value) in new[] { ("carrierPath", "game_state/foreign.json"), ("ownerKind", "resident"), ("realm", "Shining_Abode"), ("ownerId", "invalid owner") })
            {
                corrupt = quote.ToJson(); corrupt["provider"]![field] = value;
                Assert.Throws<InvalidDataException>(() => AfterlifeHealingServiceContract.ParseQuote(corrupt));
            }
        }
        else Assert.Null(quote);
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
