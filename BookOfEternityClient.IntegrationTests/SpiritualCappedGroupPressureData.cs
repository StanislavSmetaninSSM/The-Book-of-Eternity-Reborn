using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

/// <summary>Fresh complete capped-group DATA; no value grants signing, execution or publication authority.</summary>
internal sealed record SpiritualCappedGroupPressureFixture(RetainedPressureGroupData Retained, JsonObject Profiles,
    JsonObject Conflict, JsonObject Ordinary, JsonObject Actual, JsonObject Source, byte[] RawInput);

/// <summary>Independent increasing before0..3 to ordinaryIV/actualIV DATA with every retained parent authored before source sealing.</summary>
internal static class SpiritualCappedGroupPressureData
{
    /// <summary>Literal signed4/12, art0/resilience0, hostile/sourcecapIV calculation, independent of group size.</summary>
    internal static SpiritualWoundCalculation Calculation =>
        new(new(8, 0, 0, "overwhelmed", "broken", "hostile", 4), 3, 4, 0, 14, 2, 4, 4, true, 2);

    /// <summary>Literal full expectations for the closed before0..3 positive matrix or separately authored training-zero comparison.</summary>
    /// <param name="previous">Only increasing preimages clear through overwhelmed.</param>
    /// <param name="training">Authors consistent zero-cap inputs; a contradictory declared maximum must still refuse.</param>
    /// <returns>Independently declared ordinary and actual arithmetic; no production calculation supplies an expectation.</returns>
    internal static SpiritualWoundCalculation Expected(int previous, bool training = false) => (previous, training) switch
    {
        (0, false) => new(new(8, 0, 0, "clear", "broken", "hostile", 4), 0, 4, 3, 23, 4, 4, 4, true, 4),
        (1, false) => new(new(8, 0, 0, "strained", "broken", "hostile", 4), 1, 4, 2, 20, 3, 4, 4, true, 3),
        (2, false) => new(new(8, 0, 0, "fractured", "broken", "hostile", 4), 2, 4, 1, 17, 2, 4, 4, true, 2),
        (3, false) => Calculation,
        (0, true) => new(new(8, 0, 0, "clear", "broken", "training", 4), 0, 4, 3, 23, 4, 4, 0, true, 0),
        (1, true) => new(new(8, 0, 0, "strained", "broken", "training", 4), 1, 4, 2, 20, 3, 4, 0, true, 0),
        (2, true) => new(new(8, 0, 0, "fractured", "broken", "training", 4), 2, 4, 1, 17, 2, 4, 0, true, 0),
        (3, true) => new(new(8, 0, 0, "overwhelmed", "broken", "training", 4), 3, 4, 0, 14, 2, 4, 0, true, 0),
        _ => throw new ArgumentOutOfRangeException(nameof(previous))
    };

    /// <summary>Authors the complete exchange before it is embedded in the full ordinary root or sealed source.</summary>
    /// <returns>A detached literal before0..3/afterIV pressure/setback exchange with signed4/12 and both AP3→0 costs.</returns>
    /// <param name="previous">Only the closed increasing preimages clear through overwhelmed.</param>
    internal static JsonObject ReadExchange(int previous = 3)
    {
        var exchange = SpiritualPairLowPressureData.ReadExchange("broken");
        exchange["before"]!["playerSideStrain"] = Expected(previous).Input.PreviousStrain;
        return exchange;
    }

    /// <summary>Creates a fresh lawful full-root/raw-bound fixture, never by mutating an already sealed comparison.</summary>
    /// <param name="parentCount">Closed N2/N3/N4 complete retained parent DATA before turn46.</param>
    /// <param name="previous">Only the closed before0..3 increasing geometry; default retains the accepted discriminating beforeIII.</param>
    /// <param name="ranks">Optional complete III/IV sequence authored before profiles, provenance and comparison sealing.</param>
    /// <param name="training">Authors consistent full-root training-zero DATA without granting current owner authority.</param>
    /// <returns>Independent mutable inputs owned only by this call.</returns>
    internal static SpiritualCappedGroupPressureFixture Create(int parentCount, int previous = 3,
        IReadOnlyList<int>? ranks = null, bool training = false)
    {
        if (parentCount is not (2 or 3 or 4)) throw new ArgumentOutOfRangeException(nameof(parentCount));
        var expected = Expected(previous, training);
        var retained = SpiritualPressureGroupOwnerCaseData.Create(parentCount, completeRanks: ranks);
        var profiles = SpiritualPressureRetainedDataAdmissibility.CreateProfiles(retained);
        var guardian = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile("guardian", "guardian_frame", "Chaos Sea", 40);
        guardian.Remove(ActorMaterializationContract.PropertyName);
        guardian["activeWounds"] = new JsonArray(); guardian["activeEffects"] = new JsonArray();
        profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray().Add(guardian);
        foreach (var profile in profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray())
        {
            var arts = new JsonObject();
            foreach (var art in AfterlifeEntityProfileState.StandardArtIds) arts[art] = 0;
            profile!["standardArts"] = arts;
        }
        var ordinary = ReadExchange(previous);
        var actual = ReadExchange(previous);
        var root = new JsonObject { ["schemaVersion"] = 1, ["activeConflict"] = new JsonObject
        {
            ["conflictId"] = "capped_group_data", ["realm"] = "Chaos Sea", ["sideModel"] = "direct_duel",
            ["dangerMode"] = training ? "training" : "hostile", ["resolutionState"] = "active", ["playerSideStrain"] = "broken",
            ["oppositionSideStrain"] = "clear", ["conflictPosition"] = "contested", ["controlState"] = null,
            ["tempoAdvantage"] = null, ["exchangeLog"] = new JsonArray(ordinary.DeepClone()), ["combatConditions"] = new JsonArray(),
            ["playerSide"] = new JsonObject { ["leadContestant"] = new JsonObject { ["actorType"] = "player", ["actorId"] = "player_soul" }, ["supporters"] = new JsonArray() },
            ["oppositionSide"] = new JsonObject { ["leadContestant"] = new JsonObject { ["actorType"] = "guardian", ["actorId"] = "guardian_frame" }, ["supporters"] = new JsonArray() }
        }, ["recentConflicts"] = new JsonArray() };
        var raw = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + root.ToJsonString() + "\n")).ToArray();
        var source = CreateStandaloneSource(actual, raw, training);
        source["strainPreimage"] = new JsonObject
        {
            ["version"] = parentCount == 2 ? 4 : 6,
            ["inputRoleRevision"] = parentCount == 2 ? "existing_pressure_ordinary_pair_v1" : "existing_pressure_ordinary_group_v1",
            ["ordinaryExchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(ordinary),
            ["parents"] = new JsonArray(retained.Parents.Select((parent, index) => (JsonNode)new JsonObject
            {
                ["parentWoundJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(parent),
                ["effectId"] = retained.Effects[index]["effectId"]!.DeepClone(), ["componentId"] = "component_001"
            }).ToArray()),
            ["originalInput"] = new JsonObject { ["path"] = AfterlifeSpiritualConflictState.StatePath, ["existed"] = true,
                ["contentFingerprint"] = new CanonicalBeforeImage(true, raw).Fingerprint, ["exchangePointer"] = "/activeConflict/exchangeLog/0" },
            ["ordinaryCalculation"] = new JsonObject { ["traumaPressure"] = expected.TraumaPressure,
                ["maximumSeverityRank"] = expected.MaximumSeverityRank }
        };
        SpiritualExistingPressureTestData.Seal(source);
        return new(retained, profiles, root, ordinary, actual, source, raw);
    }

    /// <summary>Seals an independently authored comparison-free source for the exact full ordinary root's raw fingerprint.</summary>
    /// <param name="exchange">Complete exchange authored before source construction.</param>
    /// <param name="raw">Exact full ordinary root bytes, including BOM and whitespace.</param>
    /// <param name="training">Uses the full root's explicit training danger declaration and maximum0; never group permission.</param>
    /// <returns>A fresh source without comparison proof or live authority.</returns>
    internal static JsonObject CreateStandaloneSource(JsonObject exchange, byte[] raw, bool training = false)
    {
        var source = SpiritualRetainedPressureGroupTestData.CreateSource(exchange, "capped_group_session", "capped_group_request",
            "capped_group_snapshot", "capped_group_data", new CanonicalBeforeImage(true, raw).Fingerprint);
        if (training)
        {
            source["dangerMode"] = "training";
            source["maximumSeverityRank"] = 0;
            source["dangerDeclarationFingerprint"] = new CanonicalBeforeImage(true,
                Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(new JsonObject { ["dangerMode"] = "training" }))).Fingerprint;
            SpiritualExistingPressureTestData.Seal(source);
        }
        return source;
    }

    /// <summary>Captures every independently mutable fixture input for full nonmutation checks and failure diagnostics.</summary>
    /// <param name="fixture">One call's complete detached DATA.</param>
    /// <returns>Every retained graph, ledger, profile, exchange, source, full root and exact raw bytes.</returns>
    internal static string Serialize(SpiritualCappedGroupPressureFixture fixture) => JsonSerializer.Serialize(new
    {
        parents = fixture.Retained.Parents.Select(row => row.ToJsonString()), effects = fixture.Retained.Effects.Select(row => row.ToJsonString()),
        wounds = fixture.Retained.WoundIdentities.ToJsonString(), history = fixture.Retained.History.ToJsonString(),
        effectIdentities = fixture.Retained.EffectIdentities.ToJsonString(), receipts = fixture.Retained.Receipts.ToJsonString(),
        profiles = fixture.Profiles.ToJsonString(), conflict = fixture.Conflict.ToJsonString(), ordinary = fixture.Ordinary.ToJsonString(),
        actual = fixture.Actual.ToJsonString(), source = fixture.Source.ToJsonString(), raw = Convert.ToBase64String(fixture.RawInput)
    });
}
