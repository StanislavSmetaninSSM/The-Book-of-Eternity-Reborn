using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

/// <summary>Detached, independently authored complete comparison inputs; no value carries live or historical execution authority.</summary>
internal sealed record SpiritualPressureMultiParentFixture(RetainedPressureGroupData Retained, JsonObject Profiles,
    JsonObject Conflict, JsonObject Ordinary, JsonObject Actual, JsonObject Source, byte[] RawInput);

/// <summary>Test-only complete group geometry; all parents are authored once before any comparison or signature.</summary>
internal static class SpiritualPressureMultiParentData
{
    /// <summary>Independent signed4/12, standard art0/resilience0 ordinaryI pressure8/maxI.</summary>
    internal static SpiritualWoundCalculation OrdinaryCalculation =>
        new(new(8, 0, 0, "clear", "strained", "hostile", 4), 0, 1, 0, 8, 1, 1, 4, true, 1);

    /// <summary>Independent actualIV pressure23/maxIV after at least three distinct applicable parents saturate strain.</summary>
    internal static SpiritualWoundCalculation ActualCalculation =>
        new(new(8, 0, 0, "clear", "broken", "hostile", 4), 0, 4, 3, 23, 4, 4, 4, true, 4);

    /// <summary>Creates one complete fixture without deriving its inputs from the comparison reader.</summary>
    /// <param name="parentCount">At least three complete parents, all born before the explicit source turn.</param>
    /// <param name="ranks">Optional complete independent III/IV sequence; omission preserves original N3/N4 inputs.</param>
    /// <param name="comparisonTurn">Larger detached lists require an explicitly later lawful source/exchange turn.</param>
    /// <returns>Fresh mutable DATA owned by this invocation alone.</returns>
    internal static SpiritualPressureMultiParentFixture Create(int parentCount, IReadOnlyList<int>? ranks = null,
        int comparisonTurn = 46)
    {
        if (parentCount < 3) throw new ArgumentOutOfRangeException(nameof(parentCount));
        var retained = SpiritualPressureGroupOwnerCaseData.Create(parentCount, completeRanks: ranks,
            comparisonTurn: comparisonTurn);
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
        var ordinary = SpiritualPairLowPressureData.ReadExchange("strained");
        var actual = SpiritualPairLowPressureData.ReadExchange("broken");
        foreach (var exchange in new[] { ordinary, actual })
        {
            exchange["turnNumber"] = comparisonTurn; exchange["exchangeAtTurn"] = comparisonTurn;
            exchange["exchangeId"] = "exchange_pressure_group_" + comparisonTurn;
        }
        var conflict = new JsonObject { ["schemaVersion"] = 1, ["activeConflict"] = new JsonObject
        {
            ["conflictId"] = "multi_parent_data", ["realm"] = "Chaos Sea", ["sideModel"] = "direct_duel",
            ["dangerMode"] = "hostile", ["resolutionState"] = "active", ["playerSideStrain"] = "strained",
            ["oppositionSideStrain"] = "clear", ["conflictPosition"] = "contested", ["controlState"] = null,
            ["tempoAdvantage"] = null, ["exchangeLog"] = new JsonArray(ordinary.DeepClone()), ["combatConditions"] = new JsonArray(),
            ["playerSide"] = new JsonObject { ["leadContestant"] = new JsonObject { ["actorType"] = "player", ["actorId"] = "player_soul" }, ["supporters"] = new JsonArray() },
            ["oppositionSide"] = new JsonObject { ["leadContestant"] = new JsonObject { ["actorType"] = "guardian", ["actorId"] = "guardian_frame" }, ["supporters"] = new JsonArray() }
        }, ["recentConflicts"] = new JsonArray() };
        var raw = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + conflict.ToJsonString() + "\n")).ToArray();
        var source = CreateStandaloneSource(actual, raw);
        source["strainPreimage"] = new JsonObject
        {
            ["version"] = 6, ["inputRoleRevision"] = "existing_pressure_ordinary_group_v1",
            ["ordinaryExchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(ordinary),
            ["parents"] = new JsonArray(retained.Parents.Select((parent, index) => (JsonNode)new JsonObject
            {
                ["parentWoundJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(parent),
                ["effectId"] = retained.Effects[index]["effectId"]!.DeepClone(), ["componentId"] = "component_001"
            }).ToArray()),
            ["originalInput"] = new JsonObject { ["path"] = AfterlifeSpiritualConflictState.StatePath, ["existed"] = true,
                ["contentFingerprint"] = new CanonicalBeforeImage(true, raw).Fingerprint, ["exchangePointer"] = "/activeConflict/exchangeLog/0" },
            ["ordinaryCalculation"] = new JsonObject { ["traumaPressure"] = 8L, ["maximumSeverityRank"] = 1 }
        };
        SpiritualExistingPressureTestData.Seal(source);
        return new(retained, profiles, conflict, ordinary, actual, source, raw);
    }

    /// <summary>Creates a comparison-free source whose complete ordinary or actual exchange is independently checked by the production witness.</summary>
    /// <param name="exchange">Complete literal pressure exchange, never a retained reader result.</param>
    /// <param name="raw">Exact independently authored ordinary full-root bytes.</param>
    /// <returns>A fresh sealed standard source with no comparison proof or live permission.</returns>
    internal static JsonObject CreateStandaloneSource(JsonObject exchange, byte[] raw) =>
        SpiritualRetainedPressureGroupTestData.CreateSource(exchange, "multi_parent_session", "multi_parent_request",
            "multi_parent_snapshot", "multi_parent_data", new CanonicalBeforeImage(true, raw).Fingerprint);

    /// <summary>Captures every independent mutable input for full nonmutation assertions and useful failure diagnostics.</summary>
    /// <param name="fixture">One test's complete detached input.</param>
    /// <returns>All input values, including every retained graph/ledger and exact raw bytes.</returns>
    internal static string Serialize(SpiritualPressureMultiParentFixture fixture) => JsonSerializer.Serialize(new
    {
        parents = fixture.Retained.Parents.Select(row => row.ToJsonString()), effects = fixture.Retained.Effects.Select(row => row.ToJsonString()),
        wounds = fixture.Retained.WoundIdentities.ToJsonString(), history = fixture.Retained.History.ToJsonString(),
        effectIdentities = fixture.Retained.EffectIdentities.ToJsonString(), receipts = fixture.Retained.Receipts.ToJsonString(),
        profiles = fixture.Profiles.ToJsonString(), conflict = fixture.Conflict.ToJsonString(), ordinary = fixture.Ordinary.ToJsonString(),
        actual = fixture.Actual.ToJsonString(), source = fixture.Source.ToJsonString(), raw = Convert.ToBase64String(fixture.RawInput)
    });
}
