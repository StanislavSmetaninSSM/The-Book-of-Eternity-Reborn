using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Four independent comparison-free prerequisites, not v7 acceptance or genuine signed owner execution.</summary>
/// <param name="output">Complete unbounded prerequisite DATA and diagnostics for this invocation.</param>
public sealed class SpiritualGuardianGroupPressureDataTests(ITestOutputHelper output)
{
    /// <summary>Catches an unlawful mixed N2 original or incorrect independent ordinary10/actual20 source arithmetic.</summary>
    [Fact]
    public void GuardianGroup_N2MixedPrerequisites() => AssertPrerequisites(
        GuardianGroupPressureCase.MixedPair, [3, 4], 12, 0, "decisive_player_success",
        new(new(8, 0, 0, "strained", "fractured", "hostile", 4), 1, 2, 0, 10, 1, 2, 4, true, 1),
        new(new(8, 0, 0, "strained", "broken", "hostile", 4), 1, 4, 2, 20, 3, 4, 4, true, 3));

    /// <summary>Catches omission of the final N3 parent, its later creation, or its severity-specific siblings after saturation.</summary>
    [Fact]
    public void GuardianGroup_N3MixedPrerequisites() => AssertPrerequisites(
        GuardianGroupPressureCase.MixedTriple, [3, 4, 3], 12, 0, "decisive_player_success",
        new(new(8, 0, 0, "strained", "fractured", "hostile", 4), 1, 2, 0, 10, 1, 2, 4, true, 1),
        new(new(8, 0, 0, "strained", "broken", "hostile", 4), 1, 4, 2, 20, 3, 4, 4, true, 3));

    /// <summary>Catches dropped full-source/raw premises when both independently authored capped roots have equal scalar state.</summary>
    [Fact]
    public void GuardianGroup_N2EqualCapPrerequisites() => AssertPrerequisites(
        GuardianGroupPressureCase.EqualCapPair, [3, 4], 12, 0, "decisive_player_success",
        new(new(8, 0, 0, "strained", "broken", "hostile", 4), 1, 4, 2, 20, 3, 4, 4, true, 3),
        new(new(8, 0, 0, "strained", "broken", "hostile", 4), 1, 4, 2, 20, 3, 4, 4, true, 3));

    /// <summary>Separately proves the SOURCE hypothesis margin4/player_success/resilience5 with ordinary−4/max0 and actual6/max0.</summary>
    [Fact]
    public void GuardianGroup_N2LowMarginZeroPrerequisites() => AssertPrerequisites(
        GuardianGroupPressureCase.LowMarginZeroPair, [3, 4], 8, 5, "player_success",
        new(new(4, 0, 5, "strained", "fractured", "hostile", 4), 1, 2, 0, -4, 0, 2, 4, true, 0),
        new(new(4, 0, 5, "strained", "broken", "hostile", 4), 1, 4, 2, 6, 0, 4, 4, true, 0));

    /// <summary>Exercises real typed graph, receipt, rules, dice and full-source readers using literals supplied by each independent row.</summary>
    private void AssertPrerequisites(GuardianGroupPressureCase contour, int[] ranks, int playerDie, int resilience,
        string band, SpiritualWoundCalculation expectedOrdinary, SpiritualWoundCalculation expectedActual)
    {
        var fixture = SpiritualGuardianGroupPressureTestData.Create(contour);
        output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_prerequisite_inputs", contour,
            expectedOrdinary, expectedActual, fixture, historicalBirth = "synthetic retained DATA", signedOwner = "UNRUN in Task2" }));
        var originalProfiles = JsonNode.Parse(fixture.OriginalProfilesJson)!.AsObject();
        var originalConflict = JsonNode.Parse(fixture.OriginalConflictJson)!.AsObject();
        var originalProfilesBefore = originalProfiles.ToJsonString();
        var originalConflictBefore = originalConflict.ToJsonString();
        var issues = SpiritualGuardianAttributionDataCheck.CheckGroup(fixture.Retained, originalProfiles, originalConflict, ranks);
        Assert.True(issues.Count == 0, JsonSerializer.Serialize(issues));
        var original = originalConflict["activeConflict"]!;
        Assert.Empty(original["exchangeLog"]!.AsArray());
        Assert.Equal(("direct_duel", "hostile", "active", "clear", "strained"),
            (Text(original["sideModel"]), Text(original["dangerMode"]), Text(original["resolutionState"]),
                Text(original["playerSideStrain"]), Text(original["oppositionSideStrain"])));
        Assert.Equal(("player", "player_soul", "guardian", "guardian_frame"),
            (Text(original["playerSide"]!["leadContestant"]!["actorType"]), Text(original["playerSide"]!["leadContestant"]!["actorId"]),
                Text(original["oppositionSide"]!["leadContestant"]!["actorType"]), Text(original["oppositionSide"]!["leadContestant"]!["actorId"])));
        foreach (var profile in originalProfiles["profiles"]!.AsArray().OfType<JsonObject>())
        {
            var guardian = Text(profile["actorType"]) == "guardian";
            Assert.Equal(guardian ? "guardian_frame" : "player_soul", Text(profile["actorId"]));
            foreach (var art in profile["standardArts"]!.AsObject())
                Assert.Equal(guardian && art.Key == AfterlifeSpiritualConflictState.SpiritualResilienceArtId ? resilience : 0,
                    art.Value!.GetValue<int>());
            Assert.Equal(guardian ? ranks.Length : 0, profile["activeWounds"]!.AsArray().Count);
            Assert.Equal(guardian ? ranks.Length : 0, profile["activeEffects"]!.AsArray().Count);
            var lead = original[guardian ? "oppositionSide" : "playerSide"]!["leadContestant"]!;
            Assert.True(JsonNode.DeepEquals(profile["standardArts"], lead["actorArtTierSnapshot"]));
            Assert.Equal("afterlife_entity_profiles", Text(lead["artAuthoritySource"]));
        }
        Assert.Equal(ranks, fixture.Retained.Parents.Select(row => row["severity"]!["rank"]!.GetValue<int>()).ToArray());
        Assert.Equal(ranks.Length, fixture.Retained.Parents.Select(row => Text(row["woundId"])).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(ranks.Length, fixture.Retained.Effects.Select(row => Text(row["effectId"])).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(Enumerable.Range(1, ranks.Length).Select(index => "wound_retained_guardian_data_" + index),
            fixture.Retained.Parents.Select(row => Text(row["woundId"])));
        Assert.Equal(ranks.Length, fixture.Retained.Parents.Select(row => MortalLocationIdentityState.BuildConfusableKey(Text(row["woundId"])))
            .Distinct(StringComparer.Ordinal).Count());
        var allRoots = fixture.Retained.Parents.SelectMany(row => row["consequences"]!["ownedEffectSources"]!["rootBindings"]!.AsArray())
            .Select(row => Text(row!["effectId"])).ToArray();
        Assert.Equal(fixture.Retained.Effects.Select(row => Text(row["effectId"])), allRoots);
        Assert.Equal(ranks.Length, allRoots.Select(MortalLocationIdentityState.BuildConfusableKey).Distinct(StringComparer.Ordinal).Count());
        for (var index = 0; index < ranks.Length; index++)
        {
            var parent = fixture.Retained.Parents[index];
            Assert.Equal(40 + 2 * index, parent["origin"]!["createdAtTurn"]!.GetValue<int>());
            Assert.True(parent["lastTransition"]!["turn"]!.GetValue<int>() < 46);
            Assert.Equal(ranks[index], parent["consequences"]!["ownedEffectSources"]!["definitions"]![0]!["components"]!.AsArray().Count);
        }

        AssertSource(fixture.OrdinarySource, fixture.Ordinary, expectedOrdinary, playerDie, resilience, band);
        AssertSource(fixture.ActualSource, fixture.Actual, expectedActual, playerDie, resilience, band);
        Assert.NotSame(fixture.OrdinarySource, fixture.ActualSource);
        Assert.NotSame(fixture.Ordinary, fixture.Actual);
        var originalSnapshot = JsonNode.Parse(fixture.OriginalSnapshotJson)!.AsObject();
        var originalRoots = SpiritualGuardianAttributionDataCheck.Roots(fixture.Retained, originalProfiles, originalConflict);
        Assert.Equal(originalRoots.Keys.Order(StringComparer.Ordinal), originalSnapshot.Select(row => row.Key).Order(StringComparer.Ordinal));
        foreach (var (path, root) in originalRoots) Assert.True(JsonNode.DeepEquals(root, originalSnapshot[path]), path);
        var originalFingerprint = new CanonicalBeforeImage(true,
            Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(originalSnapshot))).Fingerprint;
        foreach (var source in new[] { fixture.OrdinarySource, fixture.ActualSource })
            Assert.Equal(originalFingerprint, Text(source["turnEvidence"]!["originalSnapshotFingerprint"]));
        var expectedExchange = fixture.Ordinary.DeepClone();
        expectedExchange["after"]!["oppositionSideStrain"] = "broken";
        Assert.True(JsonNode.DeepEquals(expectedExchange, fixture.Actual));
        var expectedOrdinaryRoot = originalConflict.DeepClone();
        expectedOrdinaryRoot["activeConflict"]!["exchangeLog"]!.AsArray().Add(fixture.Ordinary.DeepClone());
        expectedOrdinaryRoot["activeConflict"]!["oppositionSideStrain"] = expectedOrdinary.Input.NewStrain;
        Assert.True(JsonNode.DeepEquals(expectedOrdinaryRoot, fixture.OrdinaryRoot));
        var expectedActualRoot = fixture.OrdinaryRoot.DeepClone();
        expectedActualRoot["activeConflict"]!["exchangeLog"]![0] = fixture.Actual.DeepClone();
        expectedActualRoot["activeConflict"]!["oppositionSideStrain"] = "broken";
        Assert.True(JsonNode.DeepEquals(expectedActualRoot, fixture.ActualRoot));
        Assert.Equal(contour == GuardianGroupPressureCase.EqualCapPair, JsonNode.DeepEquals(fixture.OrdinaryRoot, fixture.ActualRoot));
        var raw = Convert.FromBase64String(fixture.RawOrdinaryBase64);
        Assert.True(raw.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.Equal(" \n" + fixture.OrdinaryRoot.ToJsonString() + "\n", SpiritualWoundStateJson.DecodeUtf8JsonText(raw));
        Assert.True(JsonNode.DeepEquals(fixture.OrdinaryRoot, SpiritualWoundStateJson.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(raw))));
        var rawFingerprint = new CanonicalBeforeImage(true, raw).Fingerprint;
        Assert.NotEqual(rawFingerprint, new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(fixture.OrdinaryRoot.ToJsonString())).Fingerprint);
        Assert.Equal((AfterlifeSpiritualConflictState.StatePath, true, rawFingerprint, "/activeConflict/exchangeLog/0"),
            (Text(fixture.OriginalInput["path"]), fixture.OriginalInput["existed"]!.GetValue<bool>(),
                Text(fixture.OriginalInput["contentFingerprint"]), Text(fixture.OriginalInput["exchangePointer"])));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary, fixture.OrdinaryRoot["activeConflict"]!["exchangeLog"]![0]));
        Assert.Equal(originalProfilesBefore, originalProfiles.ToJsonString());
        Assert.Equal(originalConflictBefore, originalConflict.ToJsonString());
        Assert.Equal(fixture.OriginalProfilesJson, originalProfiles.ToJsonString());
        Assert.Equal(fixture.OriginalConflictJson, originalConflict.ToJsonString());
        output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_comparison_free_prerequisites_checked", contour,
            expectedOrdinary, expectedActual, parentCount = ranks.Length }));
    }

    /// <summary>Checks every source premise without reading or authoring v7 comparison DATA or asking for a live capability.</summary>
    private static void AssertSource(JsonObject source, JsonObject exchange, SpiritualWoundCalculation expected,
        int playerDie, int resilience, string band)
    {
        var before = source.ToJsonString();
        var issues = new List<ValidationIssue>();
        ValidationService.ValidateRetainedSpiritualExchangeShape(exchange, "guardianGroup.prerequisite", issues);
        ValidationService.ValidateSpiritualWoundSourceActionShape(exchange, "guardianGroup.prerequisite", issues);
        ValidationService.ValidateRetainedSpiritualDiceAudit(exchange, source["turnEvidence"]!.AsObject(), issues);
        ValidationService.ValidateRetainedSpiritualExchangeRules(exchange, issues);
        Assert.True(issues.Count == 0, JsonSerializer.Serialize(issues));
        Assert.False(source.ContainsKey("strainPreimage"));
        Assert.Equal(expected, SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var comparison));
        Assert.Null(comparison);
        Assert.Equal(("pressure", "success", "strained", expected.Input.NewStrain, playerDie, 4, playerDie - 4, band),
            (Text(exchange["operationType"]), Text(exchange["outcome"]), Text(exchange["before"]!["oppositionSideStrain"]),
                Text(exchange["after"]!["oppositionSideStrain"]), exchange["diceAudit"]!["playerTotal"]!.GetValue<int>(),
                exchange["diceAudit"]!["oppositionTotal"]!.GetValue<int>(), exchange["diceAudit"]!["margin"]!.GetValue<int>(),
                Text(exchange["diceAudit"]!["outcomeBand"])));
        Assert.Equal("guardian_frame", SpiritualWoundFailedAction.ReadGuardianDeclaration(exchange));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""
            {"version":1,"side":"opposition","actorType":"guardian","actorId":"guardian_frame","actionPath":"/incomingAction","operationType":"pressure"}
            """), exchange[SpiritualWoundFailedAction.Property]));
        foreach (var alias in new[] { "actorType", "ownerActorType" }) Assert.Equal("guardian", Text(exchange["incomingAction"]![alias]));
        foreach (var alias in new[] { "actorId", "actorRef", "ownerActorId", "guardianId", "id" })
            Assert.Equal("guardian_frame", Text(exchange["incomingAction"]![alias]));
        foreach (var field in new[] { "playerSideStrain", "conflictPosition", "controlState", "tempoAdvantage" })
            Assert.True(JsonNode.DeepEquals(exchange["before"]![field], exchange["after"]![field]), field);
        Assert.Equal("pressure", Text(exchange["incomingAction"]!["operationType"]));
        foreach (var side in new[] { "player", "opposition" })
        {
            Assert.Equal(("pressure", 3m, 1m, side == "player" ? 10m : 9m),
                (Text(exchange["actionCostAudit"]![side]!["operationType"]), exchange["actionCostAudit"]![side]!["baseCost"]!.GetValue<decimal>(),
                    exchange["actionCostAudit"]![side]!["minCost"]!.GetValue<decimal>(), exchange["actionCostAudit"]![side]!["max"]!.GetValue<decimal>()));
            Assert.Equal((0, 3m, 3m, 0m), (exchange["actionCostAudit"]![side]!["artTier"]!.GetValue<int>(),
                exchange["actionCostAudit"]![side]!["effectiveCost"]!.GetValue<decimal>(),
                exchange["actionCostAudit"]![side]!["before"]!.GetValue<decimal>(), exchange["actionCostAudit"]![side]!["after"]!.GetValue<decimal>()));
        }
        Assert.Equal(("standard", 0, resilience, playerDie - 4, expected.MaximumSeverityRank, "hostile"),
            (Text(source["appliedArtKind"]), source["appliedArtTier"]!.GetValue<int>(), source["targetResilienceTier"]!.GetValue<int>(),
                source["harmfulMargin"]!.GetValue<int>(), source["maximumSeverityRank"]!.GetValue<int>(), Text(source["dangerMode"])));
        Assert.Equal(("player_soul", "player_soul", "guardian", "guardian_frame", "opposition", "pressure", "player_soul:player_soul:pressure"),
            (Text(source["actingActor"]!["actorKind"]), Text(source["actingActor"]!["actorId"]),
                Text(source["affectedActor"]!["actorKind"]), Text(source["affectedActor"]!["actorId"]),
                Text(source["affectedSide"]), Text(source["operation"]), Text(source["appliedArtId"])));
        Assert.Equal((0, 0, 46, "activeConflict.exchangeLog[0].opposition"),
            (source["sourceOrdinal"]!.GetValue<int>(), source["exchangeOrdinal"]!.GetValue<int>(),
                source["turnEvidence"]!["turn"]!.GetValue<int>(), Text(source["coordinate"])));
        Assert.Equal(new[] { playerDie, 4 }, source["turnEvidence"]!["acceptedD20Values"]!.AsArray().Select(row => row!.GetValue<int>()).ToArray());
        Assert.Equal(new[] { 0, 1 }, source["selectedDiceIndices"]!.AsArray().Select(row => row!.GetValue<int>()).ToArray());
        Assert.Equal(2, source["turnEvidence"]!["diceClaims"]!.AsArray().Count);
        for (var index = 0; index < 2; index++)
        {
            var claim = source["turnEvidence"]!["diceClaims"]![index]!.AsObject();
            Assert.Equal((index, index == 0 ? playerDie : 4, 0),
                (claim["sourceIndex"]!.GetValue<int>(), claim["value"]!.GetValue<int>(), claim["exchangeOrdinal"]!.GetValue<int>()));
            Assert.Equal(SpiritualWoundTurnEvidence.ComputeClaimFingerprint(claim), Text(claim["claimFingerprint"]));
        }
        Assert.True(JsonNode.DeepEquals(exchange, SpiritualExistingPressureTestData.ReadActual(source)));
        foreach (var field in new[] { "guaranteedSeverityRank", "retraumaWoundId", "retraumaWoundJsonBase64", "specialArtJsonBase64", "escalationFingerprint" })
            Assert.Null(source[field]);
        Assert.Equal(before, source.ToJsonString());
    }

    private static string Text(JsonNode? node) => node?.GetValue<string>() ?? "";
}
