using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Checks the narrow GM-authored guardian example through production DATA readers, without live authority.</summary>
public sealed class SpiritualGuardianAttributionDocumentationTests
{
    private const string Contract = "spiritual_existing_guardian_pressure_attribution_v1";
    private const string ExampleFile = "E_CLI_Spiritual_Guardian_Pressure_Attribution.txt";
    private const string GuardianId = "guardian_example_sentinel";

    /// <summary>Pins declaration authoring and unchanged player success at each GM-facing entrypoint.</summary>
    /// <param name="path">Repository-relative GM entrypoint to inspect.</param>
    [Theory]
    [InlineData("OtherGuides/Wound_Materialization_Contract.md")]
    [InlineData("OtherGuides/Afterlife_Contract_Matrix.md")]
    [InlineData("TaskGuides/CLI_Step_Main.txt")]
    public void GuardianGuidancePinsClosedDeclarationAndBoundaries(string path)
    {
        var text = Regex.Replace(Read(path), @"\s+", " ");
        foreach (var token in new[] { Contract, ExampleFile, "spiritualWoundFailedAction", "/incomingAction",
            "actorType", "actorId", "ownerActorType", "actorRef", "ownerActorId", "guardianId",
            "strained", "fractured", "overwhelmed", "after.oppositionSideStrain",
            "activeConflict.oppositionSideStrain", "no_effect", "v1–v4", "schema1" })
            Assert.Contains(token, text, StringComparison.Ordinal);
        Assert.Contains("pressure/success", text, StringComparison.Ordinal);
        Assert.Contains("existing_guardian_pressure_failed_action_v1", text, StringComparison.Ordinal);
        Assert.Contains("singleton III/IV", text, StringComparison.Ordinal);
    }

    /// <summary>Validates complete authored ordinary input, actual-only strain projection and private v5 through the real reader.</summary>
    /// <param name="rank">Independent expected III or IV guardian parent rank.</param>
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void WorkedGuardianPressureRunsThroughRetainedV5Reader(int rank)
    {
        var data = ReadWorkedData();
        Assert.Equal(new[] { 3, 4 }, data["parentRankVariants"]!.AsArray().Select(value => value!.GetValue<int>()));
        var conflictRoot = data["ordinaryConflict"]!.AsObject();
        var conflict = conflictRoot["activeConflict"]!.AsObject();
        var ordinary = Assert.Single(conflict["exchangeLog"]!.AsArray())!.AsObject();
        Assert.Equal(GuardianId, conflict["oppositionSide"]!["leadContestant"]!["actorId"]!.GetValue<string>());
        Assert.Equal("player_soul", conflict["playerSide"]!["leadContestant"]!["actorId"]!.GetValue<string>());
        Assert.Equal("pressure", ordinary["operationType"]!.GetValue<string>());
        Assert.Equal("success", ordinary["outcome"]!.GetValue<string>());
        Assert.Equal("strained", ordinary["before"]!["oppositionSideStrain"]!.GetValue<string>());
        Assert.Equal("fractured", ordinary["after"]!["oppositionSideStrain"]!.GetValue<string>());
        Assert.Equal("fractured", conflict["oppositionSideStrain"]!.GetValue<string>());
        var declaration = ordinary[SpiritualWoundFailedAction.Property]!.AsObject();
        Assert.Equal(new[] { "actionPath", "actorId", "actorType", "operationType", "side", "version" },
            declaration.Select(row => row.Key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(1, declaration["version"]!.GetValue<int>());
        Assert.Equal("opposition", declaration["side"]!.GetValue<string>());
        Assert.Equal("guardian", declaration["actorType"]!.GetValue<string>());
        Assert.Equal("/incomingAction", declaration["actionPath"]!.GetValue<string>());
        Assert.Equal("pressure", declaration["operationType"]!.GetValue<string>());
        Assert.Equal(GuardianId, declaration["actorId"]!.GetValue<string>());
        foreach (var field in new[] { "actorType", "ownerActorType" })
            Assert.Equal("guardian", ordinary["incomingAction"]![field]!.GetValue<string>());
        foreach (var field in new[] { "actorId", "actorRef", "ownerActorId", "guardianId", "id" })
            Assert.Equal(GuardianId, ordinary["incomingAction"]![field]!.GetValue<string>());
        Assert.Equal(GuardianId, SpiritualWoundFailedAction.ReadGuardianDeclaration(ordinary));
        Assert.Equal(new[] { "/activeConflict/exchangeLog/0/after/oppositionSideStrain", "/activeConflict/oppositionSideStrain" },
            data["clientChangedPaths"]!.AsArray().Select(row => row!.GetValue<string>()));
        Assert.Equal("overwhelmed", data["clientActualStrain"]!.GetValue<string>());
        Assert.Equal((12, 4, 8), (ordinary["diceAudit"]!["playerTotal"]!.GetValue<int>(),
            ordinary["diceAudit"]!["oppositionTotal"]!.GetValue<int>(), ordinary["diceAudit"]!["margin"]!.GetValue<int>()));
        foreach (var side in new[] { "player", "opposition" })
            Assert.Equal((3, 3, 0), (ordinary["actionCostAudit"]![side]!["effectiveCost"]!.GetValue<int>(),
                ordinary["actionCostAudit"]![side]!["before"]!.GetValue<int>(),
                ordinary["actionCostAudit"]![side]!["after"]!.GetValue<int>()));

        // This detached synthetic retained graph supplies only parent DATA, never a signature or live owner.
        var fixture = SpiritualGuardianAttributionTestData.Create(GuardianId, rank);
        Assert.Empty(SpiritualGuardianAttributionDataCheck.Check(fixture.Retained, fixture.Profiles, fixture.Conflict, rank));
        var expectedOrdinary = fixture.Ordinary.DeepClone().AsObject();
        expectedOrdinary["incomingAction"]!["ownerActorType"] = "guardian";
        foreach (var field in new[] { "actorRef", "ownerActorId", "guardianId", "id" })
            expectedOrdinary["incomingAction"]![field] = GuardianId;
        Assert.True(JsonNode.DeepEquals(expectedOrdinary, ordinary));
        var expectedRoot = fixture.Conflict.DeepClone().AsObject();
        expectedRoot["activeConflict"]!["exchangeLog"]![0] = expectedOrdinary.DeepClone();
        Assert.True(JsonNode.DeepEquals(expectedRoot, conflictRoot));
        var actual = ordinary.DeepClone().AsObject();
        actual["after"]!["oppositionSideStrain"] = data["clientActualStrain"]!.DeepClone();
        var actualRoot = conflictRoot.DeepClone().AsObject();
        actualRoot["activeConflict"]!["exchangeLog"]![0] = actual.DeepClone();
        actualRoot["activeConflict"]!["oppositionSideStrain"] = data["clientActualStrain"]!.DeepClone();
        var restoredOrdinary = actualRoot.DeepClone().AsObject();
        restoredOrdinary["activeConflict"]!["exchangeLog"]![0]!["after"]!["oppositionSideStrain"] = "fractured";
        restoredOrdinary["activeConflict"]!["oppositionSideStrain"] = "fractured";
        Assert.True(JsonNode.DeepEquals(conflictRoot, restoredOrdinary));
        var raw = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + conflictRoot.ToJsonString() + "\n")).ToArray();
        var fingerprint = new CanonicalBeforeImage(true, raw).Fingerprint;
        var source = SpiritualGuardianAttributionTestData.CreateSource(actual, "session_guardian_example",
            "request_guardian_example", "snapshot_guardian_example", conflict["conflictId"]!.GetValue<string>(), fingerprint);
        var proof = fixture.Source["strainPreimage"]!.DeepClone().AsObject();
        proof["ordinaryExchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(ordinary);
        proof["originalInput"]!["contentFingerprint"] = fingerprint;
        proof["ordinaryCalculation"] = data["ordinaryCalculation"]!.DeepClone();
        source["strainPreimage"] = proof;
        SpiritualExistingPressureTestData.Seal(source);
        Assert.Equal(new[] { "componentId", "effectId", "inputRoleRevision", "ordinaryCalculation", "ordinaryExchangeJsonBase64",
            "originalInput", "parentWoundJsonBase64", "version" }, proof.Select(row => row.Key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(SpiritualWoundFailedAction.Role, proof["inputRoleRevision"]!.GetValue<string>());
        Assert.Equal("player_soul", source["actingActor"]!["actorKind"]!.GetValue<string>());
        Assert.Equal("player_soul", source["actingActor"]!["actorId"]!.GetValue<string>());
        Assert.Equal("guardian", source["affectedActor"]!["actorKind"]!.GetValue<string>());
        Assert.Equal(GuardianId, source["affectedActor"]!["actorId"]!.GetValue<string>());
        Assert.Equal(8, source["harmfulMargin"]!.GetValue<int>());

        var actualCalculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var retained);

        var comparison = Assert.IsType<SpiritualStrainPreimageData>(retained);
        Assert.Equal(5, comparison.Version);
        Assert.Equal((10L, 1), (comparison.OrdinaryCalculation!.TraumaPressure, comparison.OrdinaryCalculation.MaximumSeverityRank));
        Assert.Equal((15L, 2), (actualCalculation.TraumaPressure, actualCalculation.MaximumSeverityRank));
        Assert.Equal((15L, 2), (data["actualCalculation"]!["traumaPressure"]!.GetValue<long>(),
            data["actualCalculation"]!["maximumSeverityRank"]!.GetValue<int>()));
        var parent = Assert.Single(comparison.Parents).ParentWound;
        Assert.Equal(rank, parent.Severity.Rank);
        Assert.Equal(rank, parent.Consequences.SlotsUsed);
        Assert.Equal(rank, parent.Consequences.SlotBudget);
        Assert.Equal(new WoundOwnerCoordinate("chaos_sea", "guardian", GuardianId, WoundCarrierCatalog.AfterlifeProfilesPath), parent.Owner);
        Assert.True(JsonNode.DeepEquals(actual, comparison.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(ordinary["after"], comparison.ReadOrdinaryAfter(actual)));
        comparison.ReadActualExchange()["summary"] = "Detached caller edit.";
        Assert.True(JsonNode.DeepEquals(actual, comparison.ReadActualExchange()));
    }

    /// <summary>Keeps prior player lanes declaration-free and routes only the separate guardian lane to the new example.</summary>
    [Fact]
    public void PlayerLanesKeepGuardianAttributionSeparate()
    {
        var guide = Read("OtherGuides/Wound_Materialization_Contract.md");
        var playerSection = guide[guide.IndexOf("### spiritual_existing_pressure_ordinary_v1", StringComparison.Ordinal)..
            guide.IndexOf("### " + Contract, StringComparison.Ordinal)];
        Assert.Contains("Player v1–v4 forbid `spiritualWoundFailedAction`", playerSection, StringComparison.Ordinal);
        Assert.Contains("outside the new guardian lane", playerSection, StringComparison.Ordinal);
        Assert.Contains("before < ordinary < actual <= broken", playerSection, StringComparison.Ordinal);
        Assert.Contains("ordinary18/maxIII and actual23/maxIV", playerSection, StringComparison.Ordinal);
        var examples = Read("Examples/E_CLI_Afterlife_Turns.txt");
        Assert.Contains("Player v1–v4 forbid spiritualWoundFailedAction", examples, StringComparison.Ordinal);
        Assert.Contains(ExampleFile, examples, StringComparison.Ordinal);
        Assert.Contains("that does not widen this fixed guardian I->II->III/v5 contour", Read("Examples/" + ExampleFile), StringComparison.Ordinal);
        Assert.False(SpiritualExistingPressureTestData.ReadOrdinary(SpiritualExistingPressureTestData.CreateData())
            .ContainsKey(SpiritualWoundFailedAction.Property));
        Assert.Contains("v2-only zero-ceiling", guide, StringComparison.Ordinal);
    }

    /// <summary>Separates the exact family's focused-fragment coverage from independently recorded bounded runtime evidence.</summary>
    [Fact]
    public void GuardianManifestKeepsFocusedUnqualifiedCoverage()
    {
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage, row => row.ContractId == Contract);
        Assert.Equal(ExampleFile, entry.File);
        Assert.Equal(new[] { "Chaos Sea" }, entry.Realms);
        Assert.Equal("focused-fragment", entry.ValidationKind);
        Assert.Contains(nameof(WorkedGuardianPressureRunsThroughRetainedV5Reader), entry.FocusedFragmentReason, StringComparison.Ordinal);
        Assert.Contains("Separate bounded runtime evidence", entry.CoverageLimit, StringComparison.Ordinal);
        Assert.Contains("not runtime qualification", entry.CoverageLimit, StringComparison.Ordinal);
        Assert.Contains("native births", entry.CoverageLimit, StringComparison.Ordinal);
        var text = Read("Examples/" + ExampleFile);
        Assert.All(entry.RequiredText, token => Assert.Contains(token, text, StringComparison.Ordinal));
        _ = ReadWorkedData();
    }

    /// <summary>Checks that the existing operational-resource wildcard ships the separate GM example unchanged.</summary>
    [Fact]
    public void GuardianExampleIsPackagedByExistingOperationalResourceLink()
    {
        var packaged = Path.Combine(AppContext.BaseDirectory, "operational-resources", "Examples", ExampleFile);
        Assert.True(File.Exists(packaged), "The existing Examples/**/*.txt resource Link must ship the guardian worked example.");
        Assert.Equal(File.ReadAllBytes(Path.Combine(TestRepoPaths.RepoRoot, "Examples", ExampleFile)), File.ReadAllBytes(packaged));
    }

    /// <summary>Reads the one named JSON worked fragment without interpreting explanatory wrapper labels as response fields.</summary>
    private static JsonObject ReadWorkedData()
    {
        var text = Read("Examples/" + ExampleFile);
        var start = text.IndexOf(Contract + " — worked", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = text.IndexOf("end_" + Contract, start, StringComparison.Ordinal);
        Assert.True(end > start);
        var block = Assert.Single(Regex.Matches(text[start..end], "```json\\s*([\\s\\S]*?)```", RegexOptions.CultureInvariant).Cast<Match>());
        return JsonNode.Parse(block.Groups[1].Value)!.AsObject();
    }

    /// <summary>Reads one cheap repository document fresh; no shared mutable fixture, live preparation or cache is needed.</summary>
    /// <param name="path">Repository-relative document path.</param>
    /// <returns>The complete current document.</returns>
    private static string Read(string path) => File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, path));
}
