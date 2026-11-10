using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

/// <summary>Closed independently authored actual-maximum-zero player group contours.</summary>
internal enum GroupZeroPressureContour { Low, NearCap, Capped, Training }

/// <summary>Literal complete arithmetic and setup inputs, never derived from an issued comparison.</summary>
internal sealed record GroupZeroPressureExpectation(int Parents, int PlayerDie, int Resilience,
    SpiritualWoundCalculation Ordinary, SpiritualWoundCalculation Actual);

/// <summary>One call's complete detached retained graphs, profiles, exchanges, full root and exact ordinary bytes.</summary>
internal sealed record SpiritualGroupZeroPressureFixture(RetainedPressureGroupData Retained, JsonObject Profiles,
    JsonObject Conflict, JsonObject Ordinary, JsonObject Actual, JsonObject Source, byte[] RawInput);

/// <summary>Cheap per-call DATA only; no signing, live owner, cache or execution capability.</summary>
internal static class SpiritualGroupZeroPressureData
{
    /// <summary>Maps only the four finite public Theory arguments before any allocation.</summary>
    /// <param name="value">Only low=0, near-cap=1, capped hostile=2 or training=3.</param>
    /// <returns>A closed contour; unknown values throw.</returns>
    internal static GroupZeroPressureContour Contour(int value) => value switch
    {
        0 => GroupZeroPressureContour.Low, 1 => GroupZeroPressureContour.NearCap,
        2 => GroupZeroPressureContour.Capped, 3 => GroupZeroPressureContour.Training,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    /// <summary>Hand-checked full calculations for hostile low/near-cap/capped and detached training-cap DATA.</summary>
    /// <param name="contour">Closed complete geometry, dice, art, resilience and danger inputs.</param>
    /// <returns>Independent ordinary and actual expectations.</returns>
    internal static GroupZeroPressureExpectation Expected(GroupZeroPressureContour contour) => contour switch
    {
        GroupZeroPressureContour.Low => new(2, 6, 5,
            new(new(6, 0, 5, "clear", "strained", "hostile", 4), 0, 1, 0, -4, 0, 1, 4, true, 0),
            new(new(6, 0, 5, "clear", "overwhelmed", "hostile", 4), 0, 3, 2, 6, 0, 3, 4, true, 0)),
        GroupZeroPressureContour.NearCap => new(3, 4, 5,
            new(new(8, 0, 5, "fractured", "overwhelmed", "hostile", 4), 2, 3, 0, 2, 0, 3, 4, true, 0),
            new(new(8, 0, 5, "fractured", "broken", "hostile", 4), 2, 4, 1, 7, 0, 4, 4, true, 0)),
        GroupZeroPressureContour.Capped => new(4, 4, 4,
            new(new(8, 0, 4, "overwhelmed", "broken", "hostile", 4), 3, 4, 0, 6, 0, 4, 4, true, 0),
            new(new(8, 0, 4, "overwhelmed", "broken", "hostile", 4), 3, 4, 0, 6, 0, 4, 4, true, 0)),
        GroupZeroPressureContour.Training => new(4, 4, 0,
            new(new(8, 0, 0, "overwhelmed", "broken", "training", 4), 3, 4, 0, 14, 2, 4, 0, true, 0),
            new(new(8, 0, 0, "overwhelmed", "broken", "training", 4), 3, 4, 0, 14, 2, 4, 0, true, 0)),
        _ => throw new ArgumentOutOfRangeException(nameof(contour))
    };

    /// <summary>Authors a complete literal exchange before raw-root construction or source sealing.</summary>
    /// <param name="contour">Closed geometry and signed dice.</param>
    /// <param name="actual">Selects the independently declared ordinary or actual destination.</param>
    /// <returns>A fresh complete exchange with its before state authored consistently.</returns>
    internal static JsonObject ReadExchange(GroupZeroPressureContour contour, bool actual = false)
    {
        var expected = Expected(contour);
        var calculation = actual ? expected.Actual : expected.Ordinary;
        var exchange = SpiritualPairLowPressureData.ReadExchange(calculation.Input.NewStrain!, expected.PlayerDie);
        exchange["before"]!["playerSideStrain"] = calculation.Input.PreviousStrain;
        return exchange;
    }

    /// <summary>Reuses existing complete low/training factories and authors new hostile contours before any seal.</summary>
    /// <param name="contour">Closed per-call fixture selection.</param>
    /// <param name="parents">Optional independently selected N2/N3/N4 complete inventory; low remains exact-two.</param>
    /// <param name="ranks">Optional complete III/IV rank sequence, authored before graph/provenance construction.</param>
    /// <returns>Independent mutable inputs, with no live authority.</returns>
    internal static SpiritualGroupZeroPressureFixture Create(GroupZeroPressureContour contour, int? parents = null,
        IReadOnlyList<int>? ranks = null)
    {
        var expected = Expected(contour);
        var count = parents ?? expected.Parents;
        if (count is not (2 or 3 or 4) || contour == GroupZeroPressureContour.Low && count != 2)
            throw new ArgumentOutOfRangeException(nameof(parents));
        if (ranks is not null && (ranks.Count != count || ranks.Any(rank => rank is not (3 or 4))))
            throw new ArgumentOutOfRangeException(nameof(ranks));
        if (contour == GroupZeroPressureContour.Low)
        {
            var fixture = SpiritualPairLowPressureData.Create(PairLowPressureContour.ActualZero, ranks?[0] ?? 3, ranks?[1] ?? 4);
            return new(fixture.Retained, fixture.Profiles, fixture.Conflict, fixture.Ordinary,
                fixture.Actual, fixture.Source, fixture.RawInput);
        }
        if (contour == GroupZeroPressureContour.Training)
        {
            var fixture = SpiritualCappedGroupPressureData.Create(count, ranks: ranks, training: true);
            return new(fixture.Retained, fixture.Profiles, fixture.Conflict, fixture.Ordinary,
                fixture.Actual, fixture.Source, fixture.RawInput);
        }
        var retained = SpiritualPressureGroupOwnerCaseData.Create(count, completeRanks: ranks);
        var profiles = SpiritualPressureRetainedDataAdmissibility.CreateProfiles(retained);
        var guardian = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile("guardian", "guardian_frame", "Chaos Sea", 40);
        guardian.Remove(ActorMaterializationContract.PropertyName);
        guardian["activeWounds"] = new JsonArray(); guardian["activeEffects"] = new JsonArray();
        profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray().Add(guardian);
        foreach (var profile in profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray())
        {
            var arts = new JsonObject();
            foreach (var art in AfterlifeEntityProfileState.StandardArtIds) arts[art] = 0;
            if (profile!["actorType"]!.GetValue<string>() == "player_soul") arts["spiritual_resilience"] = expected.Resilience;
            profile["standardArts"] = arts;
        }
        var ordinary = ReadExchange(contour);
        var actual = ReadExchange(contour, actual: true);
        var root = new JsonObject { ["schemaVersion"] = 1, ["activeConflict"] = new JsonObject
        {
            ["conflictId"] = "group_zero_data", ["realm"] = "Chaos Sea", ["sideModel"] = "direct_duel",
            ["dangerMode"] = "hostile", ["resolutionState"] = "active", ["playerSideStrain"] = expected.Ordinary.Input.NewStrain,
            ["oppositionSideStrain"] = "clear", ["conflictPosition"] = "contested", ["controlState"] = null,
            ["tempoAdvantage"] = null, ["exchangeLog"] = new JsonArray(ordinary.DeepClone()), ["combatConditions"] = new JsonArray(),
            ["playerSide"] = new JsonObject { ["leadContestant"] = new JsonObject { ["actorType"] = "player", ["actorId"] = "player_soul" }, ["supporters"] = new JsonArray() },
            ["oppositionSide"] = new JsonObject { ["leadContestant"] = new JsonObject { ["actorType"] = "guardian", ["actorId"] = "guardian_frame" }, ["supporters"] = new JsonArray() }
        }, ["recentConflicts"] = new JsonArray() };
        var raw = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + root.ToJsonString() + "\n")).ToArray();
        var source = CreateStandaloneSource(actual, expected.Actual, raw, "group_zero_data");
        source["strainPreimage"] = new JsonObject
        {
            ["version"] = count == 2 ? 4 : 6,
            ["inputRoleRevision"] = count == 2 ? "existing_pressure_ordinary_pair_v1" : "existing_pressure_ordinary_group_v1",
            ["ordinaryExchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(ordinary),
            ["parents"] = new JsonArray(retained.Parents.Select((parent, index) => (JsonNode)new JsonObject
            {
                ["parentWoundJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(parent),
                ["effectId"] = retained.Effects[index]["effectId"]!.DeepClone(), ["componentId"] = "component_001"
            }).ToArray()),
            ["originalInput"] = new JsonObject { ["path"] = AfterlifeSpiritualConflictState.StatePath, ["existed"] = true,
                ["contentFingerprint"] = new CanonicalBeforeImage(true, raw).Fingerprint, ["exchangePointer"] = "/activeConflict/exchangeLog/0" },
            ["ordinaryCalculation"] = new JsonObject { ["traumaPressure"] = expected.Ordinary.TraumaPressure,
                ["maximumSeverityRank"] = expected.Ordinary.MaximumSeverityRank }
        };
        SpiritualExistingPressureTestData.Seal(source);
        return new(retained, profiles, root, ordinary, actual, source, raw);
    }

    /// <summary>Seals comparison-free source DATA from complete independently authored arithmetic and exact ordinary bytes.</summary>
    /// <param name="exchange">Ordinary or actual complete exchange.</param>
    /// <param name="calculation">Independent expected calculation, not a production result.</param>
    /// <param name="raw">Exact full ordinary root bytes, including BOM/whitespace.</param>
    /// <param name="conflictId">The independently authored full root identity.</param>
    /// <returns>A fresh sealed source without comparison DATA.</returns>
    internal static JsonObject CreateStandaloneSource(JsonObject exchange, SpiritualWoundCalculation calculation,
        byte[] raw, string conflictId)
    {
        var source = SpiritualRetainedPressureGroupTestData.CreateSource(exchange, "group_zero_session", "group_zero_request",
            "group_zero_snapshot", conflictId, new CanonicalBeforeImage(true, raw).Fingerprint);
        source["targetResilienceTier"] = calculation.Input.TargetResilienceTier;
        source["maximumSeverityRank"] = calculation.MaximumSeverityRank;
        source["dangerMode"] = calculation.Input.DangerMode;
        source["dangerDeclarationFingerprint"] = new CanonicalBeforeImage(true,
            Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(new JsonObject { ["dangerMode"] = calculation.Input.DangerMode }))).Fingerprint;
        SpiritualExistingPressureTestData.Seal(source);
        return source;
    }

    /// <summary>Snapshots every independently mutable input for exact nonmutation and failure diagnostics.</summary>
    /// <param name="fixture">One independently owned complete fixture.</param>
    /// <returns>All graph, ledger, profile, full-root, exchange, source and raw inputs.</returns>
    internal static string Serialize(SpiritualGroupZeroPressureFixture fixture) => JsonSerializer.Serialize(new
    {
        parents = fixture.Retained.Parents.Select(row => row.ToJsonString()), effects = fixture.Retained.Effects.Select(row => row.ToJsonString()),
        wounds = fixture.Retained.WoundIdentities.ToJsonString(), history = fixture.Retained.History.ToJsonString(),
        effectIdentities = fixture.Retained.EffectIdentities.ToJsonString(), receipts = fixture.Retained.Receipts.ToJsonString(),
        profiles = fixture.Profiles.ToJsonString(), conflict = fixture.Conflict.ToJsonString(), ordinary = fixture.Ordinary.ToJsonString(),
        actual = fixture.Actual.ToJsonString(), source = fixture.Source.ToJsonString(), raw = Convert.ToBase64String(fixture.RawInput)
    });
}
