using BookOfEternityClient.Services;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class TrainingServiceTests
{
    private const string SoulStatePath = "game_state/meta/soul_state.json";

    public static IEnumerable<object[]> StandardWoundArtInvalidTrainingAuthorityCases()
    {
        foreach (var artId in AfterlifeSpiritualConflictState.RequiredWoundArtIds)
        {
            foreach (var mutation in new[] { "missing_leaf", "null_leaf", "string_leaf", "object_leaf" })
                yield return [artId, mutation, $"afterlifeCombatProfile.artTiers.{artId}"];

            var sibling = artId == AfterlifeSpiritualConflictState.SpiritualResilienceArtId
                ? AfterlifeSpiritualConflictState.SpiritualHealingArtId
                : AfterlifeSpiritualConflictState.SpiritualResilienceArtId;
            yield return [artId, "invalid_sibling", $"afterlifeCombatProfile.artTiers.{sibling}"];
        }

        yield return [AfterlifeSpiritualConflictState.SpiritualResilienceArtId, "null_profile", "afterlifeCombatProfile"];
        yield return [AfterlifeSpiritualConflictState.SpiritualResilienceArtId, "scalar_profile", "afterlifeCombatProfile"];
        yield return [AfterlifeSpiritualConflictState.SpiritualResilienceArtId, "null_map", "afterlifeCombatProfile.artTiers"];
        yield return [AfterlifeSpiritualConflictState.SpiritualResilienceArtId, "array_map", "afterlifeCombatProfile.artTiers"];
        yield return ["pressure", "invalid_healing_for_old_art", "afterlifeCombatProfile.artTiers.spiritual_healing"];
    }

    [Theory]
    [MemberData(nameof(StandardWoundArtInvalidTrainingAuthorityCases))]
    public async Task StandardWoundArt_InvalidPlayerAuthority_SelfTrainingRejectsQuoteAndPurchaseWithoutMutation(
        string artId,
        string mutation,
        string expectedContext)
    {
        await SeedAfterlifeSoulStateAsync(inkFeathers: 2500);
        await MutateTrainingWoundArtAuthorityAsync(artId, mutation);
        var before = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        var service = CreateService();

        var view = await service.EnsureTrainingAsync(currentTurn: 21);
        var result = await service.BuyTrainingAsync("self", $"self_art_{artId}_tier_1", currentTurn: 22);

        Assert.False(result.Success);
        Assert.False(result.StateChanged);
        Assert.Contains(expectedContext, result.Message, StringComparison.Ordinal);
        Assert.Equal(before, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        Assert.Contains(expectedContext, view.ScopeUnavailableReason, StringComparison.Ordinal);
        Assert.DoesNotContain(view.SelfTrainingOffers, offer => offer.Available);
    }

    [Theory]
    [MemberData(nameof(StandardWoundArtInvalidTrainingAuthorityCases))]
    public async Task StandardWoundArt_InvalidPlayerAuthority_FreshMentorTrainingRejectsQuoteAndPurchaseWithoutMutation(
        string artId,
        string mutation,
        string expectedContext)
    {
        await SeedAfterlifeSoulStateAsync(inkFeathers: 2500);
        var offerId = $"mentor_invalid_authority_{artId}_tier_1";
        await SeedStandardWoundArtMentorAsync(artId, relationshipLevel: 60, mentorTier: 4, offerId);
        await MutateTrainingWoundArtAuthorityAsync(artId, mutation);
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        var beforeProfiles = await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath);
        var profiles = JsonNode.Parse(beforeProfiles!)!.AsObject();
        var mentor = Assert.Single(profiles["profiles"]!.AsArray().OfType<JsonObject>());
        var showcase = Assert.IsType<JsonObject>(mentor["mentorTrainingShowcase"]);
        Assert.Equal(
            TrainingService.ComputeSourceSnapshotHash(mentor),
            showcase["sourceActorSnapshotHash"]?.GetValue<string>());
        var service = CreateService();

        var view = await service.EnsureTrainingAsync(currentTurn: 31);
        var result = await service.BuyTrainingAsync("guardian_wound_mentor", offerId, currentTurn: 32);

        Assert.False(result.Success);
        Assert.False(result.StateChanged);
        Assert.Contains(expectedContext, result.Message, StringComparison.Ordinal);
        Assert.Equal(beforeSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        Assert.Equal(beforeProfiles, await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath));
        Assert.Contains(expectedContext, view.ScopeUnavailableReason, StringComparison.Ordinal);
        Assert.DoesNotContain(view.Teachers.SelectMany(teacher => teacher.Offers), offer => offer.Available);
    }

    [Fact]
    public async Task StandardWoundArt_AbsentWholeProfile_SelfTrainingRetainsIntentionalBootstrap()
    {
        await SeedAfterlifeSoulStateAsync(inkFeathers: 2500);
        var soul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        soul.Remove(AfterlifeSpiritualConflictState.SoulStateProfileProperty);
        soul["enlightenment"] = new JsonObject { ["experience"] = 32, ["level"] = 3 };
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soul.ToJsonString());

        var result = await CreateService().BuyTrainingAsync("self", "self_art_spiritual_resilience_tier_1", currentTurn: 22);

        Assert.True(result.Success, result.Message);
        var updated = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        Assert.Equal(1, updated["afterlifeCombatProfile"]!["artTiers"]!["spiritual_resilience"]!.GetValue<int>());
        Assert.Equal(0, updated["afterlifeCombatProfile"]!["artTiers"]!["spiritual_healing"]!.GetValue<int>());
        Assert.Equal(2000, updated["inkFeathers"]!["current"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("missing", null)]
    [InlineData("malformed", "{ not-json")]
    [InlineData("non_object", "[]")]
    public async Task StandardWoundArt_UnreadableSoulAuthority_TrainingViewRejectsBeforeQuote(
        string mutation,
        string? unreadableSoul)
    {
        await SeedShiningSoulStateAsync(inkFeathers: 2500);
        var validView = await CreateService().EnsureTrainingAsync(currentTurn: 20, createPendingRequests: false);
        Assert.Contains(
            validView.SelfTrainingOffers,
            offer => offer.OfferId == "self_art_spiritual_resilience_tier_1" && offer.Available);

        var guardianLibrary = new SystemGuardianLibraryService(
            _fs,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SystemGuardianLibraryService>.Instance);
        var profiles = guardianLibrary.BuildAfterlifeEntityProfileRootForFreshNewGame(
            CreateSystemGuardianPreset("myriel", "Мириэль Пепельная Звезда", "Magic"),
            "Северная Искра",
            turnNumber: 1,
            createdAtUtc: DateTimeOffset.Parse("2026-07-06T05:00:00Z"));
        var systemGuardian = Assert.Single(profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray())!.AsObject();
        systemGuardian["realm"] = "Shining Abode";
        systemGuardian["locationId"] = "hall_lanterns";
        systemGuardian["locationName"] = "Зал Фонарей";

        var readyMentor = BuildAfterlifeMentor(
            "resident_shining_offer",
            "Наставница ясного света",
            "Shining Abode",
            "hall_lanterns",
            "Зал Фонарей",
            actorType: "resident");
        var pendingMentor = BuildAfterlifeMentor(
            "resident_shining_pending",
            "Наставник без витрины",
            "Shining Abode",
            "hall_lanterns",
            "Зал Фонарей",
            actorType: "resident");
        foreach (var mentor in new[] { readyMentor, pendingMentor })
        {
            mentor["standardArts"]![AfterlifeSpiritualConflictState.SpiritualResilienceArtId] = 0;
            mentor["standardArts"]![AfterlifeSpiritualConflictState.SpiritualHealingArtId] = 0;
        }
        readyMentor["mentorTrainingShowcase"]!["sourceActorSnapshotHash"] =
            TrainingService.ComputeSourceSnapshotHash(readyMentor);
        pendingMentor.Remove("mentorTrainingShowcase");
        profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray().Add(readyMentor);
        profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray().Add(pendingMentor);
        await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());

        var validSoul = (await _fs.ReadFileAsync(SoulStatePath))!;
        var profilesBefore = await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath);
        var shiningBefore = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        var resolvedScope = await new LocalInteractionScopeService(_fs).ResolveAsync();
        Assert.True(resolvedScope.IsResolved);
        Assert.Equal(LocalInteractionRealmKind.ShiningAbode, resolvedScope.RealmKind);

        var resolver = new SequenceLocalInteractionScopeResolver(
            async call =>
            {
                if (call == 1)
                {
                    if (mutation == "missing")
                        _fs.DeleteFile(SoulStatePath);
                    else
                        await _fs.WriteFileAtomicAsync(SoulStatePath, unreadableSoul!);
                }
                else if (call == 2)
                {
                    // The second resolution is reachable only while the product continues past
                    // its authoritative soul read. Restore realm authority so the old fallback's
                    // generated quotes and writes remain externally visible as semantic RED.
                    await _fs.WriteFileAtomicAsync(SoulStatePath, validSoul);
                }
            },
            resolvedScope,
            resolvedScope,
            resolvedScope);
        var service = new TrainingService(
            _fs,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TrainingService>.Instance,
            resolver);

        try
        {
            var view = await service.EnsureTrainingAsync(currentTurn: 21);

            Assert.Equal("afterlife", view.Realm);
            Assert.DoesNotContain(view.SelfTrainingOffers, offer => offer.Available);
            Assert.DoesNotContain(view.Teachers.SelectMany(teacher => teacher.Offers), offer => offer.Available);
            Assert.Contains("состояние души", view.ScopeUnavailableReason, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("недоступ", view.ScopeUnavailableReason, StringComparison.OrdinalIgnoreCase);
            Assert.False(view.RequestPending);
            Assert.False(view.RequestCreatedThisCall);
            Assert.Null(view.PendingGmAction);
            Assert.False(_fs.FileExists(TrainingRequestState.PendingRequestPath));
            Assert.Equal(profilesBefore, await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath));
            Assert.Equal(shiningBefore, await _fs.ReadFileAsync(ShiningAbodeState.StatePath));
            Assert.Equal(unreadableSoul, await _fs.ReadFileAsync(SoulStatePath));
        }
        finally
        {
            await _fs.WriteFileAtomicAsync(SoulStatePath, validSoul);
        }
    }

    private async Task MutateTrainingWoundArtAuthorityAsync(string selectedArtId, string mutation)
    {
        var soul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        if (mutation == "null_profile")
        {
            soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty] = null;
        }
        else if (mutation == "scalar_profile")
        {
            soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty] = 1;
        }
        else
        {
            var profile = Assert.IsType<JsonObject>(soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]);
            if (mutation == "null_map")
            {
                profile["artTiers"] = null;
            }
            else if (mutation == "array_map")
            {
                profile["artTiers"] = new JsonArray();
            }
            else
            {
                var arts = Assert.IsType<JsonObject>(profile["artTiers"]);
                var targetId = mutation switch
                {
                    "invalid_sibling" when selectedArtId == AfterlifeSpiritualConflictState.SpiritualResilienceArtId =>
                        AfterlifeSpiritualConflictState.SpiritualHealingArtId,
                    "invalid_sibling" => AfterlifeSpiritualConflictState.SpiritualResilienceArtId,
                    "invalid_healing_for_old_art" => AfterlifeSpiritualConflictState.SpiritualHealingArtId,
                    _ => selectedArtId
                };
                switch (mutation)
                {
                    case "missing_leaf":
                        arts.Remove(targetId);
                        break;
                    case "null_leaf":
                        arts[targetId] = null;
                        break;
                    case "string_leaf":
                        arts[targetId] = "1";
                        break;
                    case "object_leaf":
                    case "invalid_sibling":
                    case "invalid_healing_for_old_art":
                        arts[targetId] = new JsonObject { ["tier"] = 1, ["experience"] = 0 };
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
                }
            }
        }

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soul.ToJsonString());
    }
}
