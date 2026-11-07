using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Cheap full singleton DATA prerequisites and desired v3 IV success, without a world or live authority.</summary>
public sealed class SpiritualSingletonIvPlayerDataTests(ITestOutputHelper output)
{
    /// <summary>Validates unchanged III and complete IV synthetic creation, canonical graph, ordinary arithmetic and sibling isolation.</summary>
    /// <param name="rank">Explicit expected rank, independent of the fixture's authored rank fields.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public void PlayerSingletonData_CompleteGraphAndOrdinaryPrerequisites(int rank) => AssertPrerequisites(Create(rank), rank);

    /// <summary>Requires real witness and direct intrinsic v3 APIs to retain a lawful complete IV singleton.</summary>
    [Fact]
    public void PlayerSingletonIv_VersionThreeIntrinsicSucceeds()
    {
        var fixture = Create(4); AssertPrerequisites(fixture, 4);
        var source = fixture.Source; var original = source.ToJsonString();
        SpiritualStrainPreimageData? retained = null; SpiritualWoundCalculation? actual = null;
        var error = Record.Exception(() => actual = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out retained));
        output.WriteLine(JsonSerializer.Serialize(new { phase = "singleton_iv_player_intrinsic_v3", error = error?.ToString(),
            sourceJson = original, actual, comparisonReturned = retained is not null }));
        Assert.Equal(original, source.ToJsonString()); Assert.Null(error);
        Assert.Equal((18L, 3), (actual!.TraumaPressure, actual.MaximumSeverityRank));
        var data = Assert.IsType<SpiritualStrainPreimageData>(retained);
        Assert.Equal(3, data.Version); Assert.Single(data.Parents); Assert.Null(data.ParentSourceId);
        Assert.Equal(4, data.ParentWound.Severity.Rank);
        Assert.Equal((13L, 2), (data.OrdinaryCalculation!.TraumaPressure, data.OrdinaryCalculation.MaximumSeverityRank));
        Assert.Same(actual, data.ActualCalculation);
        Assert.True(JsonNode.DeepEquals(fixture.Retained.Parents.Single(), JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(data.ParentWound))));
        Assert.Equal(fixture.Retained.Effects.Single()["effectId"]!.GetValue<string>(), data.EffectId);
        Assert.Equal("component_001", data.ComponentId);
        Assert.Equal(new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint, data.OriginalInputFingerprint);
        Assert.Equal("/activeConflict/exchangeLog/0", data.OriginalExchangePointer);
        Assert.True(JsonNode.DeepEquals(fixture.Actual, data.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], data.ReadOrdinaryAfter(fixture.Actual)));
        var direct = SpiritualStrainPreimageData.ReadIntrinsic(source, fixture.Actual);
        Assert.Equal(data.Version, direct.Version); Assert.Equal(4, direct.ParentWound.Severity.Rank);
        Assert.Equal(data.ActualCalculation, direct.ActualCalculation);
        data.ReadActualExchange()["after"]!["playerSideStrain"] = "broken";
        data.ReadOrdinaryAfter(fixture.Actual)["playerSideStrain"] = "clear";
        Assert.True(JsonNode.DeepEquals(fixture.Actual, data.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], data.ReadOrdinaryAfter(fixture.Actual)));
        Assert.Equal(original, source.ToJsonString());
    }

    /// <summary>Authors complete independent singleton DATA before any signing, using the established rank-aware player factory.</summary>
    internal static SpiritualGuardianAttributionFixture Create(int rank)
    {
        var retained = SpiritualPressureGroupOwnerCaseData.Create(1, ranks: new(rank, 3));
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
        var ordinary = SpiritualPressureGroupOwnerCaseData.Ordinary();
        var actual = ordinary.DeepClone().AsObject(); actual["after"]!["playerSideStrain"] = "overwhelmed";
        var root = new JsonObject { ["schemaVersion"] = 1, ["activeConflict"] = new JsonObject
        {
            ["conflictId"] = "singleton_iv_player_data", ["realm"] = "Chaos Sea", ["sideModel"] = "direct_duel",
            ["dangerMode"] = "hostile", ["resolutionState"] = "active", ["playerSideStrain"] = "fractured",
            ["oppositionSideStrain"] = "clear", ["conflictPosition"] = "contested", ["controlState"] = null,
            ["tempoAdvantage"] = null, ["exchangeLog"] = new JsonArray(ordinary.DeepClone()), ["combatConditions"] = new JsonArray(),
            ["playerSide"] = new JsonObject { ["leadContestant"] = new JsonObject { ["actorType"] = "player", ["actorId"] = "player_soul" }, ["supporters"] = new JsonArray() },
            ["oppositionSide"] = new JsonObject { ["leadContestant"] = new JsonObject { ["actorType"] = "guardian", ["actorId"] = "guardian_frame" }, ["supporters"] = new JsonArray() }
        }, ["recentConflicts"] = new JsonArray() };
        var raw = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + root.ToJsonString() + "\n")).ToArray();
        var source = SpiritualRetainedPressureGroupTestData.CreateSource(actual, "singleton_iv_player_session",
            "singleton_iv_player_request", "singleton_iv_player_snapshot", "singleton_iv_player_data", new CanonicalBeforeImage(true, raw).Fingerprint);
        source["strainPreimage"] = new JsonObject
        {
            ["version"] = 3, ["inputRoleRevision"] = "existing_pressure_ordinary_v1",
            ["ordinaryExchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(ordinary),
            ["parentWoundJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(retained.Parents.Single()),
            ["effectId"] = retained.Effects.Single()["effectId"]!.DeepClone(), ["componentId"] = "component_001",
            ["originalInput"] = new JsonObject { ["path"] = AfterlifeSpiritualConflictState.StatePath, ["existed"] = true,
                ["contentFingerprint"] = new CanonicalBeforeImage(true, raw).Fingerprint, ["exchangePointer"] = "/activeConflict/exchangeLog/0" },
            ["ordinaryCalculation"] = new JsonObject { ["traumaPressure"] = 13L, ["maximumSeverityRank"] = 2 }
        };
        SpiritualExistingPressureTestData.Seal(source);
        return new(retained, profiles, root, ordinary, actual, source, raw);
    }

    /// <summary>Uses independent full retained validators and literal expectations, never the currently unsupported comparison as a prerequisite.</summary>
    internal static void AssertPrerequisites(SpiritualGuardianAttributionFixture fixture, int rank)
    {
        var checkedData = SpiritualPressureRetainedDataAdmissibility.Check(fixture.Retained, fixture.Profiles);
        Assert.True(checkedData.IsValid, JsonSerializer.Serialize(checkedData));
        var parent = Assert.Single(fixture.Retained.Parents);
        Assert.Equal((rank, rank == 4 ? "IV" : "III", 2 * rank), (parent["severity"]!["rank"]!.GetValue<int>(),
            parent["severity"]!["maximumAtCreation"]!.GetValue<string>(), parent["recovery"]!["currentStepThreshold"]!.GetValue<int>()));
        Assert.Equal(rank, parent["consequences"]!["slotBudget"]!.GetValue<int>());
        Assert.Equal(rank, parent["consequences"]!["slotsUsed"]!.GetValue<int>());
        Assert.Equal(rank, parent["consequences"]!["entries"]!.AsArray().Count);
        var effect = Assert.Single(fixture.Retained.Effects);
        var components = effect["components"]!.AsArray(); Assert.Equal(rank, components.Count);
        Assert.Equal(Enumerable.Range(1, rank).Select(i => "component_00" + i), effect["triggers"]![0]!["componentIds"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Equal(1, components[0]!["payload"]!["magnitude"]!.GetValue<int>());
        Assert.Equal(("counter", "disadvantage"), (components[1]!["payload"]!["operation"]!.GetValue<string>(), components[1]!["payload"]!["magnitude"]!.GetValue<string>()));
        Assert.Equal(("guard", rank == 4 ? 3 : 2), (components[2]!["payload"]!["operation"]!.GetValue<string>(), components[2]!["payload"]!["magnitude"]!.GetValue<int>()));
        if (rank == 4) Assert.Equal(("counter", 3), (components[3]!["payload"]!["operation"]!.GetValue<string>(), components[3]!["payload"]!["magnitude"]!.GetValue<int>()));
        var carriers = new EffectCarrierCatalogInput(null, null, null, null, fixture.Profiles, fixture.Conflict);
        var snapshot = EffectMechanicsSnapshot.Build(new(carriers, fixture.Retained.EffectIdentities));
        Assert.True(snapshot.IsAccepted, JsonSerializer.Serialize(snapshot.Issues));
        var roots = SpiritualPressureRetainedDataAdmissibility.CreateSourceRoots(fixture.Retained, fixture.Profiles)
            .ToDictionary(row => row.Key, row => row.Value, StringComparer.Ordinal);
        roots[AfterlifeSpiritualConflictState.StatePath] = fixture.Conflict;
        var sources = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(roots);
        var targets = EffectAcceptedTurnInputComposer.BuildCanonicalTargetAuthority(carriers, roots);
        var all = SpiritualWoundConflictContributionProjector.Project(snapshot, sources, targets, fixture.Conflict);
        Assert.True(all.IsAccepted, JsonSerializer.Serialize(all.Issues)); Assert.Equal(rank, all.Contributions.Count);
        var selected = Assert.Single(all.Contributions, row => row.Profile == "spiritual_strain_burden" && row.Operation == "pressure");
        Assert.Equal(("player", "player_soul", "player", 1, 1), (selected.Actor.Kind, selected.Actor.TargetId, selected.ResolvedSide, selected.CurrentStacks, selected.Magnitude.GetInt32()));
        var isolated = snapshot with { Components = snapshot.Components.Where(row => row.EffectId != selected.EffectId || row.ComponentId != selected.ComponentId).ToArray() };
        var siblingProjection = SpiritualWoundConflictContributionProjector.Project(isolated, sources, targets, fixture.Conflict);
        Assert.True(siblingProjection.IsAccepted, JsonSerializer.Serialize(siblingProjection.Issues)); Assert.Equal(rank - 1, siblingProjection.Contributions.Count);
        Assert.Equal(all.Contributions.Where(row => row.ComponentId != selected.ComponentId).Select(row => JsonSerializer.Serialize(row)),
            siblingProjection.Contributions.Select(row => JsonSerializer.Serialize(row)));
        var oldSource = fixture.Retained.Receipts["sources"]![0]!["witness"]!.AsObject();
        var birth = SpiritualWoundSourceWitness.ValidateAndReadStrainData(oldSource, out var noBirthComparison); Assert.Null(noBirthComparison);
        Assert.Equal((0, rank, rank == 4 ? 25L : 20L, rank), (birth.PreviousStrainRank, birth.NewStrainRank, birth.TraumaPressure, birth.MaximumSeverityRank));
        var issues = new List<ValidationIssue>();
        ValidationService.ValidateRetainedSpiritualExchangeShape(fixture.Ordinary, "singleton.player.ordinary", issues);
        ValidationService.ValidateSpiritualWoundSourceActionShape(fixture.Ordinary, "singleton.player.ordinary", issues);
        ValidationService.ValidateRetainedSpiritualDiceAudit(fixture.Ordinary, fixture.Source["turnEvidence"]!.AsObject(), issues);
        ValidationService.ValidateRetainedSpiritualExchangeRules(fixture.Ordinary, issues);
        Assert.True(issues.Count == 0, JsonSerializer.Serialize(issues));
        var ordinarySource = SpiritualRetainedPressureGroupTestData.CreateSource(fixture.Ordinary, "ordinary-session", "ordinary-request", "ordinary-snapshot",
            "ordinary-conflict", new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint);
        var ordinary = SpiritualWoundSourceWitness.ValidateAndReadStrainData(ordinarySource, out var noOrdinary); Assert.Null(noOrdinary);
        Assert.Equal((13L, 2), (ordinary.TraumaPressure, ordinary.MaximumSeverityRank));
        var actualSource = fixture.Source.DeepClone().AsObject(); Assert.True(actualSource.Remove("strainPreimage")); SpiritualExistingPressureTestData.Seal(actualSource);
        var actual = SpiritualWoundSourceWitness.ValidateAndReadStrainData(actualSource, out var noActual); Assert.Null(noActual);
        Assert.Equal((18L, 3), (actual.TraumaPressure, actual.MaximumSeverityRank));
        var expected = fixture.Ordinary.DeepClone(); expected["after"]!["playerSideStrain"] = "overwhelmed";
        Assert.True(JsonNode.DeepEquals(expected, fixture.Actual));
        Assert.Equal(("pressure", "setback", -8), (fixture.Ordinary["operationType"]!.GetValue<string>(), fixture.Ordinary["outcome"]!.GetValue<string>(), fixture.Ordinary["diceAudit"]!["margin"]!.GetValue<int>()));
        Assert.True(fixture.RawInput.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.True(JsonNode.DeepEquals(fixture.Conflict, SpiritualWoundStateJson.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(fixture.RawInput))));
        Assert.Equal(new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint, fixture.Source["strainPreimage"]!["originalInput"]!["contentFingerprint"]!.GetValue<string>());
    }
}
