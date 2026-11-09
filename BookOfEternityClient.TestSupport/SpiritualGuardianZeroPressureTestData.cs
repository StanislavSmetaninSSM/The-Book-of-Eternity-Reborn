using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

/// <summary>Independent closed guardian zero-pressure DATA authoring; no source, signature or publication authority is issued.</summary>
internal static class SpiritualGuardianZeroPressureTestData
{
    /// <summary>Authors the primary hostile resilience4 maximum-zero contour with a complete retained III/IV parent.</summary>
    /// <param name="rank">Independent expected parent rank, restricted to III or IV before allocation.</param>
    /// <returns>Independent mutable originals and sealed detached comparison DATA.</returns>
    internal static GuardianZeroPressureData Create(int rank)
    {
        if (rank is not (3 or 4)) throw new ArgumentOutOfRangeException(nameof(rank));
        return CreateCore(rank, 4);
    }

    /// <summary>Authors the sole rankIII resilience2 ordinary-zero/positive-actual sentinel, not another live qualification contour.</summary>
    internal static GuardianZeroPressureData CreateOrdinaryZeroPositiveActual() => CreateCore(3, 2);

    /// <summary>Returns literal ordinary expectations without using the production calculation to choose an oracle.</summary>
    /// <param name="resilience">Only the primary4 or sentinel2 original tier.</param>
    internal static SpiritualWoundCalculation OrdinaryCalculation(int resilience) => resilience switch
    {
        4 => new(new(8, 0, 4, "strained", "fractured", "hostile", 4), 1, 2, 0, 2, 0, 2, 4, true, 0),
        2 => new(new(8, 0, 2, "strained", "fractured", "hostile", 4), 1, 2, 0, 6, 0, 2, 4, true, 0),
        _ => throw new ArgumentOutOfRangeException(nameof(resilience))
    };

    /// <summary>Returns literal actual expectations after one distinct guardian parent contributes exactly one strain step.</summary>
    /// <param name="resilience">Only the primary4 or sentinel2 original tier.</param>
    internal static SpiritualWoundCalculation ActualCalculation(int resilience) => resilience switch
    {
        4 => new(new(8, 0, 4, "strained", "overwhelmed", "hostile", 4), 1, 3, 1, 7, 0, 3, 4, true, 0),
        2 => new(new(8, 0, 2, "strained", "overwhelmed", "hostile", 4), 1, 3, 1, 11, 1, 3, 4, true, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(resilience))
    };

    /// <summary>Builds a complete comparison-free intrinsic witness from already authored ordinary or actual DATA, then seals it once.</summary>
    /// <param name="exchange">Complete declared I-to-II or I-to-III exchange, never a live source.</param>
    /// <param name="resilience">Closed original guardian tier, authored before this call.</param>
    /// <param name="session">Original request's session label.</param>
    /// <param name="request">Original request label.</param>
    /// <param name="token">Original snapshot label; DATA alone does not authenticate it.</param>
    /// <param name="conflictId">Exact containing conflict identity.</param>
    /// <param name="originalFingerprint">Independent original image fingerprint; raw ordinary comparison identity remains separate.</param>
    /// <returns>Sealed comparison-free witness DATA, never an admitted source or source-only key.</returns>
    internal static JsonObject CreateComparisonFreeSource(JsonObject exchange, int resilience,
        string session, string request, string token, string conflictId, string originalFingerprint)
    {
        var source = CreateUnsealedSource(exchange, resilience, session, request, token, conflictId, originalFingerprint);
        SpiritualExistingPressureTestData.Seal(source);
        return source;
    }

    /// <summary>Authors every source field before its one final seal; a caller may attach the complete comparison before sealing.</summary>
    private static JsonObject CreateUnsealedSource(JsonObject exchange, int resilience,
        string session, string request, string token, string conflictId, string originalFingerprint)
    {
        var expected = exchange["after"]?["oppositionSideStrain"]?.GetValue<string>() switch
        {
            "fractured" => OrdinaryCalculation(resilience),
            "overwhelmed" => ActualCalculation(resilience),
            _ => throw new ArgumentOutOfRangeException(nameof(exchange))
        };
        var claims = new JsonArray();
        foreach (var (index, value) in new[] { (0, 12), (1, 4) })
        {
            var claim = new JsonObject { ["sourceIndex"] = index, ["value"] = value,
                ["exchangeOrdinal"] = 0, ["claimFingerprint"] = "" };
            claim["claimFingerprint"] = SpiritualWoundTurnEvidence.ComputeClaimFingerprint(claim);
            claims.Add(claim);
        }
        var source = new JsonObject
        {
            ["sourceId"] = "", ["coordinate"] = "activeConflict.exchangeLog[0].opposition",
            ["conflictId"] = conflictId, ["exchangeId"] = exchange["exchangeId"]!.DeepClone(),
            ["sourceOrdinal"] = 0, ["exchangeOrdinal"] = 0,
            ["turnEvidence"] = new JsonObject
            {
                ["sessionId"] = session, ["requestId"] = request, ["snapshotToken"] = token,
                ["turn"] = exchange["turnNumber"]!.DeepClone(), ["realm"] = "chaos_sea",
                ["originalSnapshotFingerprint"] = originalFingerprint,
                ["bounds"] = new JsonObject { ["exchangeCount"] = 1, ["sourceSlotCount"] = 2,
                    ["diceCount"] = 2, ["imagePathCount"] = 40 },
                ["acceptedD20Values"] = new JsonArray(12, 4), ["diceClaims"] = claims
            },
            ["affectedSide"] = "opposition",
            ["actingActor"] = new JsonObject { ["actorKind"] = "player_soul", ["actorId"] = "player_soul" },
            ["affectedActor"] = new JsonObject { ["actorKind"] = "guardian",
                ["actorId"] = exchange["incomingAction"]!["actorId"]!.DeepClone() },
            ["operation"] = "pressure", ["appliedArtId"] = "player_soul:player_soul:pressure",
            ["appliedArtKind"] = "standard", ["appliedArtTier"] = 0, ["targetResilienceTier"] = resilience,
            ["priorStrainRank"] = expected.PreviousStrainRank, ["destinationStrainRank"] = expected.NewStrainRank,
            ["extraStrainJumps"] = expected.ExtraJumpSteps, ["harmfulMargin"] = 8,
            ["maximumSeverityRank"] = expected.MaximumSeverityRank, ["guaranteedSeverityRank"] = null,
            ["dangerMode"] = "hostile",
            ["dangerDeclarationFingerprint"] = Fingerprint(new JsonObject { ["dangerMode"] = "hostile" }),
            ["escalationFingerprint"] = null, ["retraumaWoundId"] = null, ["retraumaWoundJsonBase64"] = null,
            ["exchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(exchange),
            ["specialArtJsonBase64"] = null, ["selectedDiceIndices"] = new JsonArray(0, 1),
            ["resourceResultFingerprint"] = Fingerprint(exchange["actionCostAudit"]!),
            ["effectPlanFingerprint"] = Fingerprint(new JsonObject { ["retainedDataEffectPlan"] = conflictId }),
            ["conflictBeforeFingerprint"] = Fingerprint(exchange["before"]!),
            ["conflictAfterFingerprint"] = Fingerprint(exchange["after"]!), ["sourceFingerprint"] = ""
        };
        return source;
    }

    /// <summary>Constructs complete original profiles/root/raw bytes before sealing the comparison, leaving historical birth DATA unchanged.</summary>
    private static GuardianZeroPressureData CreateCore(int rank, int resilience)
    {
        var ordinaryCalculation = OrdinaryCalculation(resilience);
        var retained = SpiritualGuardianAttributionTestData.CreateRetained(rank: rank);
        var profiles = new JsonArray();
        foreach (var (kind, id) in new[] { ("player_soul", "player_soul"), ("guardian", SpiritualGuardianAttributionTestData.GuardianId) })
        {
            var profile = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile(kind, id, "Chaos Sea", 40);
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
        var guardian = profiles.OfType<JsonObject>().Single(row => row["actorType"]!.GetValue<string>() == "guardian");
        var ordinary = SpiritualGuardianAttributionTestData.CreateExchange("exchange_guardian_zero_46", 46,
            "strained", "fractured", declaration: true);
        ordinary["incomingAction"]!["ownerActorType"] = "guardian";
        foreach (var alias in new[] { "actorRef", "ownerActorId", "guardianId", "id" })
            ordinary["incomingAction"]![alias] = SpiritualGuardianAttributionTestData.GuardianId;
        var actual = ordinary.DeepClone().AsObject();
        actual["after"]!["oppositionSideStrain"] = "overwhelmed";
        var root = new JsonObject
        {
            ["schemaVersion"] = 1, ["recentConflicts"] = new JsonArray(),
            ["activeConflict"] = new JsonObject
            {
                ["conflictId"] = "conflict_guardian_zero", ["realm"] = "Chaos Sea", ["sideModel"] = "direct_duel",
                ["dangerMode"] = "hostile", ["resolutionState"] = "active", ["playerSideStrain"] = "clear",
                ["oppositionSideStrain"] = "fractured", ["conflictPosition"] = "contested", ["controlState"] = null,
                ["tempoAdvantage"] = null, ["exchangeLog"] = new JsonArray(ordinary.DeepClone()), ["combatConditions"] = new JsonArray(),
                ["playerSide"] = new JsonObject { ["leadContestant"] = new JsonObject { ["actorType"] = "player", ["actorId"] = "player_soul" },
                    ["supporters"] = new JsonArray() },
                ["oppositionSide"] = new JsonObject { ["leadContestant"] = new JsonObject
                    { ["actorType"] = "guardian", ["actorId"] = SpiritualGuardianAttributionTestData.GuardianId,
                        ["actorArtTierSnapshot"] = guardian["standardArts"]!.DeepClone(), ["artAuthoritySource"] = "afterlife_entity_profiles" },
                    ["supporters"] = new JsonArray() }
            }
        };
        var raw = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + root.ToJsonString() + "\n")).ToArray();
        var fingerprint = new CanonicalBeforeImage(true, raw).Fingerprint;
        var source = CreateUnsealedSource(actual, resilience, "session_guardian_zero", "request_guardian_zero",
            "snapshot_guardian_zero", "conflict_guardian_zero", fingerprint);
        source["strainPreimage"] = new JsonObject
        {
            ["version"] = 5, ["inputRoleRevision"] = SpiritualGuardianAttributionTestData.Role,
            ["ordinaryExchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(ordinary),
            ["parentWoundJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(retained.Parents.Single()),
            ["effectId"] = retained.Effects.Single()["effectId"]!.DeepClone(), ["componentId"] = "component_001",
            ["originalInput"] = new JsonObject { ["path"] = AfterlifeSpiritualConflictState.StatePath,
                ["existed"] = true, ["contentFingerprint"] = fingerprint, ["exchangePointer"] = "/activeConflict/exchangeLog/0" },
            ["ordinaryCalculation"] = new JsonObject { ["traumaPressure"] = ordinaryCalculation.TraumaPressure,
                ["maximumSeverityRank"] = ordinaryCalculation.MaximumSeverityRank }
        };
        SpiritualExistingPressureTestData.Seal(source);
        return new(rank, resilience, retained, profilesRoot, root, ordinary, actual, source, raw);
    }

    /// <summary>Computes intrinsic synthetic DATA identity; it never substitutes for a signed image or current owner.</summary>
    private static string Fingerprint(JsonNode node) => new CanonicalBeforeImage(true,
        Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(node))).Fingerprint;
}

/// <summary>Per-invocation detached originals and comparison; mutable values are never shared between tests.</summary>
internal sealed record GuardianZeroPressureData(int Rank, int Resilience, RetainedPressureGroupData Retained,
    JsonObject Profiles, JsonObject Conflict, JsonObject Ordinary, JsonObject Actual, JsonObject Source, byte[] RawOrdinaryBytes)
{
    /// <summary>Gets the independent literal ordinary arithmetic for this closed input.</summary>
    internal SpiritualWoundCalculation OrdinaryCalculation => SpiritualGuardianZeroPressureTestData.OrdinaryCalculation(Resilience);
    /// <summary>Gets the independent literal actual arithmetic, not the result of an issued source.</summary>
    internal SpiritualWoundCalculation ActualCalculation => SpiritualGuardianZeroPressureTestData.ActualCalculation(Resilience);
}
