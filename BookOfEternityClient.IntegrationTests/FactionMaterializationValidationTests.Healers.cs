using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class FactionMaterializationValidationTests
{
    public static IEnumerable<object[]> HealerLifecycleCases()
    {
        foreach (var raw in new[] { true, false })
            foreach (var (change, expected) in new (string, string?)[]
            {
                ("valid", null), ("alive", null), ("dead", "missing"), ("null_life", "missing"),
                ("invalid_life", "missing"), ("hidden_profile", "not_visible"), ("hidden_faction", null),
                ("rumored_faction", null), ("hidden_without_healer", "missing"), ("weakened", null),
                ("leaderless", null), ("broken", null), ("dissolved", null),
                ("lifecycle_scalar", "missing"), ("lifecycle_array", "missing"), ("lifecycle_null", "missing")
            }) yield return [raw, change, expected!];
    }

    public static IEnumerable<object[]> HealerIdentityCases()
    {
        foreach (var raw in new[] { true, false })
            foreach (var (change, expected) in new (string, string?)[]
            {
                ("tier_zero", "missing"), ("tier_six", "missing"), ("tier_string", "missing"),
                ("missing_art", "missing"), ("wrong_kind", "missing"), ("wrong_id", "missing"),
                ("wrong_realm", "missing"), ("duplicate_profile", "missing"), ("duplicate_resident", "missing"),
                ("other_faction", "missing"), ("not_ascended", "missing"), ("other_role", "missing"),
                ("missing_envelope", "missing"), ("living_and_dead", null), ("two_living", null),
                ("unrelated_malformed_profile", null)
            }) yield return [raw, change, expected!];
    }

    [Theory]
    [MemberData(nameof(HealerLifecycleCases))]
    public Task CurrentFactionHealerLifecycleAndDisclosureUseBothValidationOwners(bool raw, string change, string? expected) =>
        AssertHealerInvariantAsync(raw, change, expected);

    [Theory]
    [MemberData(nameof(HealerIdentityCases))]
    public Task CurrentFactionHealerUsesExactUniqueCurrentMaterializedCapability(bool raw, string change, string? expected) =>
        AssertHealerInvariantAsync(raw, change, expected);

    [Fact]
    public async Task CurrentMaterializedFactionWithoutHealerIsRejectedWithoutChangingAuthoredState()
    {
        await WriteGenericStoryFactionOutcomeAsync(BuildCompleteGenericStoryShiningFaction(includeHealer: false), includeHealer: false);
        var before = new[] { ShiningPath, ResidentPath, AfterlifeProfilesPath, SarefStoryPath }
            .ToDictionary(path => path, path => File.Exists(_fs.ResolvePath(path))
                ? File.ReadAllBytes(_fs.ResolvePath(path)) : null, StringComparer.Ordinal);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();
        foreach (var (path, bytes) in before)
        {
            var after = File.Exists(_fs.ResolvePath(path)) ? File.ReadAllBytes(_fs.ResolvePath(path)) : null;
            Assert.True(bytes is null ? after is null : after is not null && bytes.AsSpan().SequenceEqual(after), path);
        }
        var factionErrors = issues.Where(i => i.Severity == IssueSeverity.Error &&
            i.Actor == "shining_faction:shine_faction_dawn_archive").ToArray();
        Assert.DoesNotContain(factionErrors, i => i.Code != "faction_materialization_shining_healer_missing");
        Assert.Contains(factionErrors, i => i.Code == "faction_materialization_shining_healer_missing");
    }

    private async Task AssertHealerInvariantAsync(bool raw, string change, string? expected)
    {
        var faction = BuildCompleteGenericStoryShiningFaction();
        var residents = BuildRouteResidentRoot(); var profiles = new List<JsonObject>();
        AuthorFixtureFactionHealer(faction, residents, profiles);
        var resident = residents["entries"]![0]!.AsObject(); var profile = profiles[0];
        switch (change)
        {
            case "valid": break; // Supported generic profile omits lifeState.
            case "alive": profile["lifeState"] = "alive"; break;
            case "dead": profile["lifeState"] = "dead"; break;
            case "null_life": profile["lifeState"] = null; break;
            case "invalid_life": profile["lifeState"] = true; break;
            case "hidden_profile": profile["isPlayerVisible"] = false; break;
            case "hidden_faction": faction["visibility"] = "hidden"; profile["isPlayerVisible"] = false; break;
            case "rumored_faction": faction["visibility"] = "rumored"; profile["visibility"] = "private"; break;
            case "hidden_without_healer": faction["visibility"] = "hidden"; RemoveHealer(); break;
            case "weakened": faction["factionLifecycle"]!["state"] = "weakened"; break;
            case "leaderless":
                faction["factionLifecycle"]!["state"] = "leaderless";
                faction["leadership"] = new JsonObject { ["leadershipState"] = "vacant", ["headActorType"] = null, ["headActorId"] = null };
                break;
            case "broken": case "dissolved":
                faction["factionLifecycle"] = new JsonObject { ["state"] = change, ["defeatedAtTurn"] = 11,
                    ["defeatReason"] = "The historical faction lost its last active defenders.",
                    ["remnantsSummary"] = "Its complete chronicle and old materialization are retained." };
                faction["leadership"] = new JsonObject { ["leadershipState"] = "vacant", ["headActorType"] = null, ["headActorId"] = null };
                RemoveHealer(); break;
            case "lifecycle_scalar": faction["factionLifecycle"] = "broken"; RemoveHealer(); break;
            case "lifecycle_array": faction["factionLifecycle"] = new JsonArray("broken"); RemoveHealer(); break;
            case "lifecycle_null": faction["factionLifecycle"] = null; RemoveHealer(); break;
            case "tier_zero": profile["standardArts"]!["spiritual_healing"] = 0; break;
            case "tier_six": profile["standardArts"]!["spiritual_healing"] = 6; break;
            case "tier_string": profile["standardArts"]!["spiritual_healing"] = "1"; break;
            case "missing_art": profile["standardArts"]!.AsObject().Remove("spiritual_healing"); break;
            case "wrong_kind": profile["actorType"] = "guardian"; break;
            case "wrong_id": profile["actorId"] = "other"; break;
            case "wrong_realm": profile["realm"] = "Chaos Sea"; break;
            case "duplicate_profile": profiles.Add(profile.DeepClone().AsObject()); break;
            case "duplicate_resident": residents["entries"]!.AsArray().Add(resident.DeepClone()); break;
            case "other_faction": resident["shiningFactionId"] = "other"; break;
            case "not_ascended": resident["ascensionState"] = "not_ascended"; break;
            case "other_role": resident["residentRole"] = "archive_support"; break;
            case "missing_envelope": profile.Remove(ActorMaterializationContract.PropertyName); break;
            case "living_and_dead": case "two_living":
                var second = resident.DeepClone().AsObject(); second["residentId"] = "second_healer";
                residents["entries"]!.AsArray().Add(second);
                var secondProfile = BuildRouteAfterlifeProfile("second_healer", includeEnvelope: true);
                secondProfile["standardArts"]!["spiritual_healing"] = 5;
                secondProfile["lifeState"] = change == "living_and_dead" ? "dead" : "alive";
                profiles.Add(secondProfile); break;
            case "unrelated_malformed_profile": profiles.Add(new JsonObject { ["actorType"] = "resident" }); break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        faction["sarefVisibility"] = faction["visibility"]!.DeepClone();
        // Keep authored DATA truthful, including the changed resident bonus and
        // historical/leaderless trade state. This grants no actor trade authority.
        if (faction["factionLifecycle"] is JsonObject)
        {
            var strength = ShiningAbodeState.ComputeFactionStrength(faction, residents, 0);
            faction["factionStrength"] = strength;
            faction["materialization"]!["capabilities"]!["canTrade"] = ShiningAbodeState.FactionHasAvailableTrade(faction, strength);
        }
        var root = ShiningRoot(faction);
        root["halls"] = new JsonArray(BuildShiningHall("hall_dawn_archive", "Dawn Archive"));
        var profileRoot = BuildAfterlifeProfileRoot(profiles);
        await WriteCurrentAndSnapshotAsync((ShiningPath, root.ToJsonString()), (ResidentPath, residents.ToJsonString()),
            (AfterlifeProfilesPath, profileRoot.ToJsonString()),
            (SarefStoryPath, BuildSarefStoryRoot(faction["visibility"]!.GetValue<string>(), "shine_faction_dawn_archive").ToJsonString()),
            (ShiningPath, root.ToJsonString()), (ResidentPath, residents.ToJsonString()), (AfterlifeProfilesPath, profileRoot.ToJsonString()));
        var before = CaptureHealerAuthoredState();
        var issues = raw ? await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync() :
            await _validator.ValidateGameStateAsync(GameStateValidationPhase.AcceptedTurnFactionMaterializationCompleteness);
        AssertHealerAuthoredStateUnchanged(before);
        var healerErrors = issues.Where(i => i.Code?.StartsWith("faction_materialization_shining_healer_", StringComparison.Ordinal) == true).ToArray();
        if (expected is null)
        {
            Assert.Empty(healerErrors);
            Assert.DoesNotContain(issues, i => i.Severity == IssueSeverity.Error);
        }
        else Assert.Contains(healerErrors, i => i.Code == "faction_materialization_shining_healer_" + expected);
        if (change.StartsWith("lifecycle_", StringComparison.Ordinal))
            Assert.Contains(issues, i => i.Code == "faction_materialization_shining_lifecycle_invalid");

        void RemoveHealer()
        {
            residents["entries"]!.AsArray().Clear(); profiles.Clear();
            faction["materialization"]!["capabilities"]!["hasResidentAffiliations"] = false;
            faction["materialization"]!["sections"]!["residentAffiliations"] = EmptyDisposition("No current affiliated residents.");
        }
    }

    [Theory]
    [InlineData(1)] [InlineData(20)]
    public async Task FactionHealerOwnerReadsProfilesOnceForOneOrTwentyFactions(int population)
    {
        var armed = false; var opens = 0;
        using var fixture = new FactionMaterializationValidationTests(new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (armed && path == AfterlifeProfilesPath) opens++;
                return Task.CompletedTask;
            }
        });
        var root = ShiningRoot(); root["halls"] = new JsonArray();
        var residents = BuildRouteResidentRoot(); var profiles = new List<JsonObject>();
        for (var index = 0; index < population; index++)
        {
            var faction = BuildCompleteNativeShiningFaction(); var id = "faction_healer_count_" + index;
            faction["factionId"] = id; faction["hallId"] = "hall_healer_count_" + index;
            faction["materialization"]!["factionId"] = id;
            faction["materialization"]!["materializationId"] = "fmat_healer_count_" + index;
            faction["factionLifecycle"]!["state"] = "leaderless";
            faction["leadership"] = new JsonObject { ["leadershipState"] = "vacant", ["headActorType"] = null, ["headActorId"] = null };
            AuthorFixtureFactionHealer(faction, residents, profiles);
            MarkResidentAffiliationsPopulated(faction);
            var strength = ShiningAbodeState.ComputeFactionStrength(faction, residents, 0);
            faction["factionStrength"] = strength;
            faction["materialization"]!["capabilities"]!["canTrade"] = ShiningAbodeState.FactionHasAvailableTrade(faction, strength);
            root["factions"]!.AsArray().Add(faction);
            root["halls"]!.AsArray().Add(BuildShiningHall("hall_healer_count_" + index, "Counted Hall"));
        }
        var profileRoot = BuildAfterlifeProfileRoot(profiles);
        await fixture.WriteCurrentAndSnapshotAsync((ShiningPath, root.ToJsonString()), (ResidentPath, residents.ToJsonString()),
            (AfterlifeProfilesPath, profileRoot.ToJsonString()), (ShiningPath, root.ToJsonString()),
            (ResidentPath, residents.ToJsonString()), (AfterlifeProfilesPath, profileRoot.ToJsonString()));
        var before = fixture.CaptureHealerAuthoredState(); var issues = new List<ValidationIssue>();
        armed = true;
        await fixture._validator.ValidateAcceptedTurnShiningFactionMaterializationCompletenessAsync(true, issues);
        armed = false;
        Assert.Equal(1, opens);
        Assert.DoesNotContain(issues, i => i.Severity == IssueSeverity.Error);
        fixture.AssertHealerAuthoredStateUnchanged(before);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task HeadAndHealerReuseOneActualProfileMaterializationValidation(bool raw)
    {
        var faction = BuildCompleteGenericStoryShiningFaction();
        var residents = BuildRouteResidentRoot(); var profiles = new List<JsonObject>();
        AuthorFixtureFactionHealer(faction, residents, profiles);
        var id = profiles[0]["actorId"]!.GetValue<string>();
        faction["leadership"] = new JsonObject { ["leadershipState"] = "secure", ["headActorType"] = "resident", ["headActorId"] = id };
        profiles[0]["materialization"]!["capabilities"]!["canTrade"] = true;
        profiles[0].Remove("motivation"); // One deterministic error per actual validation execution.
        var root = ShiningRoot(faction);
        root["halls"] = new JsonArray(BuildShiningHall("hall_dawn_archive", "Dawn Archive"));
        var profileRoot = BuildAfterlifeProfileRoot(profiles);
        await WriteCurrentAndSnapshotAsync((ShiningPath, root.ToJsonString()), (ResidentPath, residents.ToJsonString()),
            (AfterlifeProfilesPath, profileRoot.ToJsonString()),
            (SarefStoryPath, BuildSarefStoryRoot("revealed", "shine_faction_dawn_archive").ToJsonString()),
            (ShiningPath, root.ToJsonString()), (ResidentPath, residents.ToJsonString()), (AfterlifeProfilesPath, profileRoot.ToJsonString()));
        var before = CaptureHealerAuthoredState(); var issues = new List<ValidationIssue>();
        await _validator.ValidateAcceptedTurnShiningFactionMaterializationCompletenessAsync(raw, issues);
        Assert.Single(issues, i => i.Code == "actor_materialization_afterlife_missing_motivation");
        Assert.Contains(issues, i => i.Code == "faction_materialization_shining_healer_missing");
        AssertHealerAuthoredStateUnchanged(before);
    }

    private Dictionary<string, byte[]?> CaptureHealerAuthoredState() =>
        new[] { ShiningPath, ResidentPath, AfterlifeProfilesPath, SarefStoryPath }.ToDictionary(path => path,
            path => File.Exists(_fs.ResolvePath(path)) ? File.ReadAllBytes(_fs.ResolvePath(path)) : null, StringComparer.Ordinal);

    private void AssertHealerAuthoredStateUnchanged(Dictionary<string, byte[]?> before)
    {
        var after = CaptureHealerAuthoredState();
        foreach (var (path, bytes) in before)
            Assert.True(bytes is null ? after[path] is null : after[path] is not null && bytes.AsSpan().SequenceEqual(after[path]), path);
    }

    private static void AuthorFixtureFactionHealer(JsonObject faction, JsonObject residents,
        List<JsonObject> profiles, string? existingResidentId = null)
    {
        if (faction["factionId"] is not JsonValue value || !value.TryGetValue<string>(out var factionId) ||
            factionId is null || !ResourceMaterializationContract.IsExactIdentifier(factionId)) return;
        JsonObject resident;
        JsonObject profile;
        if (existingResidentId is null)
        {
            var id = "healer_" + factionId;
            resident = BuildRouteResident(id, factionId);
            residents["entries"]!.AsArray().Add(resident);
            profile = BuildRouteAfterlifeProfile(id, includeEnvelope: true);
            profiles.Add(profile);
        }
        else
        {
            resident = Assert.Single(residents["entries"]!.AsArray().OfType<JsonObject>(), r =>
                r["residentId"]!.GetValue<string>() == existingResidentId && r["shiningFactionId"]!.GetValue<string>() == factionId);
            profile = Assert.Single(profiles, p => p["actorType"]!.GetValue<string>() == "resident" &&
                p["actorId"]!.GetValue<string>() == existingResidentId);
        }
        resident["residentRole"] = ShiningAbodeState.ResidentRoleHealingSupport;
        profile["standardArts"]!["spiritual_healing"] = 1;
    }
}
