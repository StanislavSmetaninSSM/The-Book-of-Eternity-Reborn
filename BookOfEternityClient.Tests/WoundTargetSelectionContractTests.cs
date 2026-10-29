using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Target navigation uses visible typed facts and never creates treatment permission.</summary>
public sealed class WoundTargetSelectionContractTests
{
    /// <summary>Self comes first; public role context distinguishes equal names without exposing bindings.</summary>
    [Fact]
    public void TargetsUseSelfFirstAndPublicContextWithoutLeakingPrivateCoordinates()
    {
        var (context, guardians, residents) = Inputs();
        var before = context.Input.Carriers.AfterlifeProfiles!.ToJsonString() + guardians.ToJsonString() + residents.ToJsonString();
        var targets = WoundTargetSelectionService.Build(context, guardians, residents);
        Assert.Equal("Себя", targets[0].Name);
        Assert.Equal(new[] { "player_soul", "guardian_choice", "resident_a", "resident_b" }, targets.Select(t => t.Owner.OwnerId));
        var named = targets.Where(t => t.Name == "Одинаковое имя").ToArray();
        Assert.Equal(2, named.Length); Assert.All(named, t => Assert.True(t.Available));
        Assert.Contains("Смотритель", named[0].Context); Assert.Contains("Летописец", named[1].Context);
        Assert.NotEqual(named[0].Value, named[1].Value);
        Assert.Equal("Известная рана", Assert.Single(named[0].Wounds).Name);
        var json = JsonSerializer.Serialize(targets);
        foreach (var token in new[] { "resident_a", "resident_b", "guardian_choice", "wound_resident", "sha256:", "carrierPath", "private_imprint", "private_brain", "Скрытая рана" })
            Assert.DoesNotContain(token, json);
        Assert.Equal(before, context.Input.Carriers.AfterlifeProfiles!.ToJsonString() + guardians.ToJsonString() + residents.ToJsonString());
    }

    /// <summary>A visible actor remains inspectable without consent, while hidden-only wounds remain undisclosed.</summary>
    [Fact]
    public void KnownWoundSummariesDoNotTurnVisibilityIntoConsentOrRevealHiddenWounds()
    {
        var (context, guardians, residents) = Inputs(hiddenResidentWound: true);
        var targets = WoundTargetSelectionService.Build(context, guardians, residents);
        Assert.Empty(targets.Single(t => t.Owner.OwnerId == "resident_a").Wounds);
        Assert.Empty(targets.Single(t => t.Owner.OwnerKind == "guardian").Wounds);
        Assert.DoesNotContain("Скрытая рана", JsonSerializer.Serialize(targets));
        Assert.All(context.Input.Carriers.AfterlifeProfiles!["profiles"]!.AsArray().OfType<JsonObject>(),
            p => Assert.False(p.ContainsKey(SpiritualTreatmentParticipation.RecordProperty)));
        var state = new WoundCommandState("generation_choice", []).WithTargets(targets);
        Assert.True(state.IsTargetSelection);
        var selected = state.ForTarget(targets.Single(t => t.Owner.OwnerId == "resident_a"));
        Assert.Empty(selected.Choices); // Navigating a visible target does not invent an aid action.
    }

    /// <summary>Identical public contexts cannot be rescued by opaque IDs or invented ordinal identities.</summary>
    [Fact]
    public void IndistinguishablePublicTargetsAreUnavailable()
    {
        var (context, guardians, residents) = Inputs();
        residents["entries"]![1]!["roleLabel"] = "Смотритель";
        residents["entries"]![1]!["summary"] = "Наблюдает за входом.";
        var targets = WoundTargetSelectionService.Build(context, guardians, residents);
        var duplicates = targets.Where(t => t.Name == "Одинаковое имя").ToArray();
        Assert.Equal(2, duplicates.Length); Assert.All(duplicates, t => Assert.False(t.Available));
        var state = new WoundCommandState("generation_choice", []).WithTargets(targets);
        Assert.Throws<InvalidDataException>(() => state.ResolveTarget(duplicates[0].Value));
        Assert.All(duplicates, t => Assert.Contains("различить", t.Reason));
    }

    /// <summary>Nearby targets require exact visible typed current profile and roster membership.</summary>
    [Theory]
    [InlineData("hidden")]
    [InlineData("departed")]
    [InlineData("wrong_realm")]
    [InlineData("foreign_abode")]
    [InlineData("duplicate_roster")]
    [InlineData("confusable_profile")]
    [InlineData("alias_conflict")]
    public void InvalidNearbyIdentityCannotBeSelected(string scenario)
    {
        var (context, guardians, residents) = Inputs();
        var profile = context.Input.Carriers.AfterlifeProfiles!["profiles"]!.AsArray().OfType<JsonObject>().Single(p => p["actorId"]!.GetValue<string>() == "resident_a");
        var row = residents["entries"]![0]!.AsObject();
        switch (scenario)
        {
            case "hidden": profile["visibility"] = "gm_only"; break;
            case "departed": row["isPresent"] = false; break;
            case "wrong_realm": profile["realm"] = "Shining Abode"; break;
            case "foreign_abode": row["abodeId"] = "abode_elsewhere"; break;
            case "duplicate_roster": residents["entries"]!.AsArray().Add(row.DeepClone()); break;
            case "confusable_profile": var alias = profile.DeepClone(); alias["actorId"] = "RESIDENT_A"; context.Input.Carriers.AfterlifeProfiles["profiles"]!.AsArray().Add(alias); break;
            default: profile["actorRef"] = "resident_other"; break;
        }
        Assert.DoesNotContain(WoundTargetSelectionService.Build(context, guardians, residents), t => t.Owner.OwnerId == "resident_a");
    }

    /// <summary>An old menu token resolves its original owner even after a new target changes ordering.</summary>
    [Fact]
    public void OldTargetTokenNeverRetargetsAfterOrderingChangesAndChangedWoundRefuses()
    {
        var (context, guardians, residents) = Inputs();
        var targets = WoundTargetSelectionService.Build(context, guardians, residents);
        var old = targets.Single(t => t.Owner.OwnerId == "resident_a");
        var state = new WoundCommandState("generation_choice", []).WithTargets(targets);
        var extra = context.Input.Carriers.AfterlifeProfiles!["profiles"]![2]!.DeepClone().AsObject();
        extra["actorId"] = "resident_first"; extra["displayName"] = "А раньше"; extra["activeWounds"] = new JsonArray();
        context.Input.Carriers.AfterlifeProfiles["profiles"]!.AsArray().Add(extra);
        var row = residents["entries"]![0]!.DeepClone().AsObject(); row["residentId"] = "resident_first";
        residents["entries"]!.AsArray().Add(row);
        var fresh = WoundTargetSelectionService.Build(context, guardians, residents);
        Assert.NotEqual(old.Value, fresh.Single(t => t.Owner == old.Owner).Value);
        Assert.Equal("resident_a", state.ResolveTarget(old.Value).Owner.OwnerId);
        Assert.Equal(old.Owner, WoundTargetSelectionService.RequireCurrent(old, fresh).Owner);
        var (changed, sameGuardians, sameResidents) = Inputs(changedWound: true);
        Assert.Throws<InvalidDataException>(() => WoundTargetSelectionService.RequireCurrent(old,
            WoundTargetSelectionService.Build(changed, sameGuardians, sameResidents)));
    }

    private static (WoundPlayerReadContext Context, JsonObject Guardians, JsonObject Residents) Inputs(bool hiddenResidentWound = false, bool changedWound = false)
    {
        var input = WoundPlayerProjectionTestData.InputInRealm("chaos_sea", WoundContractTestData.CreateSpiritualActiveWound(
            "wound_self_choice", "chaos_sea", "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath));
        var profiles = input.Carriers.AfterlifeProfiles!["profiles"]!.AsArray();
        profiles[0]!["standardArts"] = new JsonObject { ["spiritual_healing"] = 3 };
        foreach (var (kind, id, name) in new[] { ("guardian", "guardian_choice", "Хранитель"), ("resident", "resident_a", "Одинаковое имя"), ("resident", "resident_b", "Одинаковое имя") })
            profiles.Add(new JsonObject { ["actorType"] = kind, ["actorId"] = id, ["realm"] = "Chaos Sea", ["locationId"] = "abode_choice",
                ["displayName"] = name, ["activeWounds"] = new JsonArray(), ["activeEffects"] = new JsonArray(), ["private_brain"] = "private_brain" });
        var node = WoundContractTestData.CreateSpiritualActiveWound("wound_resident_choice", "chaos_sea", "resident", "resident_a", WoundCarrierCatalog.AfterlifeProfilesPath);
        node["display"]!["name"] = hiddenResidentWound ? "Скрытая рана" : "Известная рана";
        node["display"]!["visibility"] = hiddenResidentWound ? "gm_only" : "known_to_player";
        if (changedWound) node["display"]!["description"] = "Изменившаяся известная рана.";
        node["lastTransition"]!["transitionId"] = "create_wound_resident_choice";
        profiles[2]!["activeWounds"] = new JsonArray(node);
        var parsed = WoundMaterializationContract.Parse(node.ToJsonString(), "target.fixture.wound"); Assert.True(parsed.IsValid);
        var wound = parsed.Wound!; var fp = WoundIdentityState.ComputeSemanticFingerprint(wound);
        var identities = JsonNode.Parse(WoundIdentityState.SerializeCanonical(input.Identities))!.AsObject();
        identities["entries"]!.AsArray().Add(WoundContractTestData.CreateIdentityEntry(wound.WoundId, wound.Owner.Realm, wound.Owner.OwnerKind,
            wound.Owner.OwnerId, wound.Owner.CarrierPath, "spiritual", semanticFingerprint: fp));
        var history = JsonNode.Parse(WoundHistoryState.SerializeCanonical(input.History))!.AsObject();
        var transition = WoundContractTestData.CreateTransition(); transition["ordinal"] = 2; transition["woundId"] = wound.WoundId;
        transition["transitionId"] = wound.LastTransition.TransitionId; transition["operationKey"] = "create_wound_resident_choice";
        transition["beforeFingerprint"] = WoundHistoryState.ComputeNonexistentBeforeFingerprint(wound.WoundId); transition["afterFingerprint"] = fp;
        transition["sourceFingerprint"] = "sha256:" + new string('e', 64); transition["attemptId"] = null;
        transition["outputFingerprint"] = WoundHistoryState.ComputeOutputFingerprint("create_wound_resident_choice",
            transition["eventRef"]!.GetValue<string>(), transition["readableSummary"]!.GetValue<string>());
        history["transitions"]!.AsArray().Add(transition); history["nextOrdinal"] = 3;
        var identityState = WoundIdentityState.Parse(identities.ToJsonString(), "target.fixture.identity");
        var historyState = WoundHistoryState.Parse(history.ToJsonString(), "target.fixture.history");
        Assert.True(identityState.IsValid); Assert.True(historyState.IsValid);
        input = input with { Identities = identityState.State!, History = historyState.State! };
        var catalog = WoundCarrierCatalog.Build(input.Carriers); Assert.Empty(catalog.Issues); Assert.Empty(input.History.ValidateAgreement(input.Identities, catalog));
        var guardian = new JsonObject { ["guardianId"] = "guardian_choice", ["canonicalName"] = "Хранитель", ["abode"] = new JsonObject { ["abodeId"] = "abode_choice", ["name"] = "Обитель выбора" } };
        var guardians = new JsonObject { ["guardians"] = new JsonArray(guardian), ["activeGuardian"] = guardian.DeepClone(), ["chaosSeaNavigation"] = new JsonObject { ["currentGuardianId"] = "guardian_choice", ["currentAbodeId"] = "abode_choice" } };
        var residents = new JsonObject { ["entries"] = new JsonArray(
            new JsonObject { ["residentId"] = "resident_a", ["guardianId"] = "guardian_choice", ["abodeId"] = "abode_choice", ["isPresent"] = true, ["roleLabel"] = "Смотритель", ["summary"] = "Наблюдает за входом.", ["mortalWorldImprint"] = "private_imprint" },
            new JsonObject { ["residentId"] = "resident_b", ["guardianId"] = "guardian_choice", ["abodeId"] = "abode_choice", ["isPresent"] = true, ["roleLabel"] = "Летописец", ["summary"] = "Ведёт записи." }) };
        var soul = new JsonObject { ["currentRealm"] = "Chaos Sea", ["afterlifeCombatProfile"] = new JsonObject { ["artTiers"] = new JsonObject { ["spiritual_healing"] = 3 } } };
        return (new(input, catalog, soul, null, WoundPlayerProjection.Build(input, catalog)), guardians, residents);
    }
}
