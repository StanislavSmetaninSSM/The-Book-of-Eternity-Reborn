using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

/// <summary>Closed independently authored prerequisite inputs; no case selects a live owner or comparison reader.</summary>
internal enum GuardianGroupPressureCase { MixedPair, MixedTriple, EqualCapPair, LowMarginZeroPair }

/// <summary>Cheap per-case guardian group DATA using the existing retained ancestry builder, without GameEngine or signing.</summary>
internal static class SpiritualGuardianGroupPressureTestData
{
    /// <summary>Authors immutable original image strings before separate ordinary/actual candidates and comparison-free source seals.</summary>
    /// <param name="contour">One of the four prerequisite cases with independent literal inputs.</param>
    /// <returns>Independent mutable candidate DATA plus immutable reusable originals; not historical birth or signed capture evidence.</returns>
    internal static GuardianGroupPressureData Create(GuardianGroupPressureCase contour)
    {
        var (ranks, ordinaryStrain, playerDie, resilience, band) = contour switch
        {
            GuardianGroupPressureCase.MixedPair => (new[] { 3, 4 }, "fractured", 12, 0, "decisive_player_success"),
            GuardianGroupPressureCase.MixedTriple => (new[] { 3, 4, 3 }, "fractured", 12, 0, "decisive_player_success"),
            GuardianGroupPressureCase.EqualCapPair => (new[] { 3, 4 }, "broken", 12, 0, "decisive_player_success"),
            GuardianGroupPressureCase.LowMarginZeroPair => (new[] { 3, 4 }, "fractured", 8, 5, "player_success"),
            _ => throw new ArgumentOutOfRangeException(nameof(contour))
        };
        // N3 creates at40/42/44, all strictly before source46. The singleton builder keeps its old turn42.
        var retained = SpiritualGuardianAttributionTestData.CreateRetainedGroup(ranks, firstCreationTurn: 40);
        var profiles = new JsonArray();
        foreach (var (kind, id) in new[] { ("player_soul", "player_soul"), ("guardian", "guardian_frame") })
        {
            var profile = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile(kind, id, "Chaos Sea", 38);
            profile.Remove(ActorMaterializationContract.PropertyName);
            var arts = new JsonObject();
            foreach (var art in AfterlifeEntityProfileState.StandardArtIds) arts[art] = 0;
            if (kind == "guardian") arts[AfterlifeSpiritualConflictState.SpiritualResilienceArtId] = resilience;
            profile["standardArts"] = arts;
            profile["activeWounds"] = kind == "guardian" ? new JsonArray(retained.Parents.Select(row => row.DeepClone()).ToArray()) : new JsonArray();
            profile["activeEffects"] = kind == "guardian" ? new JsonArray(retained.Effects.Select(row => row.DeepClone()).ToArray()) : new JsonArray();
            profiles.Add(profile);
        }
        var profilesRoot = new JsonObject { ["schemaVersion"] = 1, ["profiles"] = profiles };
        var original = new JsonObject
        {
            ["schemaVersion"] = 1, ["recentConflicts"] = new JsonArray(),
            ["activeConflict"] = new JsonObject
            {
                ["conflictId"] = "conflict_guardian_group", ["realm"] = "Chaos Sea", ["sideModel"] = "direct_duel",
                ["dangerMode"] = "hostile", ["resolutionState"] = "active", ["playerSideStrain"] = "clear",
                ["oppositionSideStrain"] = "strained", ["conflictPosition"] = "contested", ["controlState"] = null,
                ["tempoAdvantage"] = null, ["exchangeLog"] = new JsonArray(), ["combatConditions"] = new JsonArray(),
                ["playerSide"] = Side("player", "player_soul", profiles[0]!["standardArts"]!),
                ["oppositionSide"] = Side("guardian", "guardian_frame", profiles[1]!["standardArts"]!)
            }
        };
        // These strings are the original inputs for a later genuine owner fixture. No signing happens here.
        var originalProfilesJson = profilesRoot.ToJsonString();
        var originalConflictJson = original.ToJsonString();
        var originalSnapshot = new JsonObject();
        foreach (var (path, root) in SpiritualGuardianAttributionDataCheck.Roots(retained, profilesRoot, original))
            originalSnapshot[path] = root?.DeepClone();
        var originalSnapshotJson = originalSnapshot.ToJsonString();
        var originalSnapshotFingerprint = Fingerprint(originalSnapshot);
        var ordinary = Exchange(ordinaryStrain, playerDie, band);
        // Independently specify actualIV. Do not derive ordinary by subtracting parents or changing the before-state.
        var actual = Exchange("broken", playerDie, band);
        var ordinaryRoot = Candidate(original, ordinary, ordinaryStrain);
        var actualRoot = Candidate(original, actual, "broken");
        var raw = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + ordinaryRoot.ToJsonString() + "\n")).ToArray();
        var originalInput = new JsonObject
        {
            ["path"] = AfterlifeSpiritualConflictState.StatePath, ["existed"] = true,
            ["contentFingerprint"] = new CanonicalBeforeImage(true, raw).Fingerprint,
            ["exchangePointer"] = "/activeConflict/exchangeLog/0"
        };
        var ordinarySource = SpiritualGuardianAttributionTestData.CreateSource(ordinary, "session_guardian_group",
            "request_guardian_group", "snapshot_guardian_group", "conflict_guardian_group", originalSnapshotFingerprint, resilience);
        var actualSource = SpiritualGuardianAttributionTestData.CreateSource(actual, "session_guardian_group",
            "request_guardian_group", "snapshot_guardian_group", "conflict_guardian_group", originalSnapshotFingerprint, resilience);
        return new(retained, originalProfilesJson, originalConflictJson, originalSnapshotJson, ordinaryRoot, actualRoot, ordinary, actual,
            ordinarySource, actualSource, originalInput, Convert.ToBase64String(raw));
    }

    /// <summary>Sets all incoming aliases and the literal outcome band before a comparison-free source is sealed.</summary>
    private static JsonObject Exchange(string destination, int playerDie, string band)
    {
        var exchange = SpiritualGuardianAttributionTestData.CreateExchange("exchange_guardian_group_46", 46,
            "strained", destination, playerDie, 4, declaration: true);
        exchange["diceAudit"]!["outcomeBand"] = band;
        exchange["incomingAction"]!["ownerActorType"] = "guardian";
        foreach (var alias in new[] { "actorRef", "ownerActorId", "guardianId", "id" }) exchange["incomingAction"]![alias] = "guardian_frame";
        return exchange;
    }

    /// <summary>Copies a whole zero-exchange original before adding exactly one independently authored exchange and its final strain.</summary>
    private static JsonObject Candidate(JsonObject original, JsonObject exchange, string finalStrain)
    {
        var root = original.DeepClone().AsObject();
        root["activeConflict"]!["exchangeLog"]!.AsArray().Add(exchange.DeepClone());
        root["activeConflict"]!["oppositionSideStrain"] = finalStrain;
        return root;
    }

    /// <summary>Creates exact original lead identities and profile art snapshots without a live participant capability.</summary>
    private static JsonObject Side(string kind, string id, JsonNode arts) => new()
    {
        ["leadContestant"] = new JsonObject { ["actorType"] = kind, ["actorId"] = id,
            ["actorArtTierSnapshot"] = arts.DeepClone(), ["artAuthoritySource"] = "afterlife_entity_profiles" },
        ["supporters"] = new JsonArray()
    };

    private static string Fingerprint(JsonNode node) => new CanonicalBeforeImage(true,
        Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(node))).Fingerprint;
}

/// <summary>One invocation's detached group DATA. Original JSON and raw-base64 strings are immutable; candidate objects are never shared or cached.</summary>
internal sealed record GuardianGroupPressureData(RetainedPressureGroupData Retained,
    string OriginalProfilesJson, string OriginalConflictJson, string OriginalSnapshotJson, JsonObject OrdinaryRoot, JsonObject ActualRoot,
    JsonObject Ordinary, JsonObject Actual, JsonObject OrdinarySource, JsonObject ActualSource,
    JsonObject OriginalInput, string RawOrdinaryBase64);
