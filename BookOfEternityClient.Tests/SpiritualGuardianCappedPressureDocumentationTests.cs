using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Checks compact capped guidance against independent complete fixture ancestry; DATA never grants live authority.</summary>
/// <param name="output">Complete baseline and named DATA-refusal diagnostics.</param>
public sealed class SpiritualGuardianCappedPressureDocumentationTests(ITestOutputHelper output)
{
    private const string Contract = "spiritual_existing_guardian_pressure_attribution_v1";
    private const string Fragment = "guardian_capped_pressure_data_v1";
    private const string ExampleFile = "E_CLI_Spiritual_Guardian_Pressure_Attribution.txt";
    private const string GuardianId = "guardian_example_sentinel";

    /// <summary>Pins the closed current equality and positive lifecycle at each active GM entrypoint.</summary>
    /// <param name="path">One primary guide or the existing guardian worked-example family.</param>
    [Theory]
    [InlineData("OtherGuides/Wound_Materialization_Contract.md")]
    [InlineData("OtherGuides/Afterlife_Contract_Matrix.md")]
    [InlineData("TaskGuides/CLI_Step_Main.txt")]
    [InlineData("Rules/Block_CLI_Operations.txt")]
    [InlineData("CLI_API_Specification.md")]
    [InlineData("Examples/E_CLI_Spiritual_Guardian_Pressure_Attribution.txt")]
    public void GuardianCappedGuidancePinsClosedEqualityAndPositiveLifecycle(string path)
    {
        var text = Regex.Replace(Read(path), @"\s+", " ");
        foreach (var token in new[] { Contract, Fragment, ExampleFile, "singleton III/IV", "beforeI", "ordinaryII/III/IV",
                     "actual=min(4,ordinary+1)", "ordinaryIV=actualIV", "managed paths remain present", "empty changed paths",
                     "pressure20/maxIII", "fixture-supplied ancestry", "not runtime qualification", "spiritualWoundFailedAction",
                     "/incomingAction", "History", "C4", "schema1" })
            Assert.Contains(token, text, StringComparison.Ordinal);
        Assert.DoesNotContain("Guardian-v5 ordinaryIV remains refused", text, StringComparison.Ordinal);
        Assert.DoesNotContain("guardian ordinaryIV/capped remain refused", text, StringComparison.Ordinal);
    }

    /// <summary>Validates independently authored complete ordinary/actual originals before full/direct v5 and precise raw/sibling DATA checks.</summary>
    /// <param name="rank">One complete fixture-supplied retained III or IV parent alternative, never simultaneous parents.</param>
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void WorkedGuardianCappedFragmentHasIndependentFullV5Data(int rank)
    {
        var data = ReadFragment(); var beforeData = data.ToJsonString();
        Assert.Equal(new[] { "actualCalculation", "appliedArtTier", "clientActualEqualsOrdinary", "clientActualStrain",
            "clientChangedPaths", "clientManagedPaths", "fixtureAncestry", "ordinaryCalculation", "ordinaryConflict",
            "ordinaryFinalEcho", "ordinaryInputUtf8", "parentRankVariants", "targetResilienceTier" },
            data.Select(row => row.Key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(new[] { 3, 4 }, data["parentRankVariants"]!.AsArray().Select(row => row!.GetValue<int>()));
        Assert.Equal((0, 0), (data["appliedArtTier"]!.GetValue<int>(), data["targetResilienceTier"]!.GetValue<int>()));
        Assert.Contains("fixture-supplied ancestry", data["fixtureAncestry"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("not signed/current/live authority", data["fixtureAncestry"]!.GetValue<string>(), StringComparison.Ordinal);
        var root = data["ordinaryConflict"]!.AsObject(); var active = root["activeConflict"]!.AsObject();
        var ordinary = Assert.Single(active["exchangeLog"]!.AsArray())!.AsObject();
        Assert.Equal(("Chaos Sea", "direct_duel", "hostile", "active", "clear", "broken"),
            (active["realm"]!.GetValue<string>(), active["sideModel"]!.GetValue<string>(), active["dangerMode"]!.GetValue<string>(),
                active["resolutionState"]!.GetValue<string>(), active["playerSideStrain"]!.GetValue<string>(), active["oppositionSideStrain"]!.GetValue<string>()));
        Assert.Equal(("pressure", "success", "strained", "broken"), (ordinary["operationType"]!.GetValue<string>(), ordinary["outcome"]!.GetValue<string>(),
            ordinary["before"]!["oppositionSideStrain"]!.GetValue<string>(), ordinary["after"]!["oppositionSideStrain"]!.GetValue<string>()));
        Assert.Equal(GuardianId, SpiritualWoundFailedAction.ReadGuardianDeclaration(ordinary));
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["version"] = 1, ["side"] = "opposition", ["actorType"] = "guardian",
            ["actorId"] = GuardianId, ["actionPath"] = "/incomingAction", ["operationType"] = "pressure" }, ordinary[SpiritualWoundFailedAction.Property]));
        foreach (var field in new[] { "actorType", "ownerActorType" }) Assert.Equal("guardian", ordinary["incomingAction"]![field]!.GetValue<string>());
        foreach (var field in new[] { "actorId", "actorRef", "ownerActorId", "guardianId", "id" }) Assert.Equal(GuardianId, ordinary["incomingAction"]![field]!.GetValue<string>());
        Assert.Equal((12, 4, 8), (ordinary["diceAudit"]!["playerTotal"]!.GetValue<int>(), ordinary["diceAudit"]!["oppositionTotal"]!.GetValue<int>(), ordinary["diceAudit"]!["margin"]!.GetValue<int>()));
        foreach (var side in new[] { "player", "opposition" })
            Assert.Equal((3, 3, 0), (ordinary["actionCostAudit"]![side]!["effectiveCost"]!.GetValue<int>(),
                ordinary["actionCostAudit"]![side]!["before"]!.GetValue<int>(), ordinary["actionCostAudit"]![side]!["after"]!.GetValue<int>()));
        Assert.True(data["clientActualEqualsOrdinary"]!.GetValue<bool>()); Assert.Empty(data["clientChangedPaths"]!.AsArray());
        Assert.Equal(new[] { "/activeConflict/exchangeLog/0/after/oppositionSideStrain", "/activeConflict/oppositionSideStrain" },
            data["clientManagedPaths"]!.AsArray().Select(row => row!.GetValue<string>()));
        Assert.Equal("broken", data["clientActualStrain"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["oppositionSideStrain"] = "broken" }, data["ordinaryFinalEcho"]));
        var actual = ordinary.DeepClone().AsObject(); actual["after"]!["oppositionSideStrain"] = data["clientActualStrain"]!.DeepClone();
        var actualRoot = root.DeepClone().AsObject(); actualRoot["activeConflict"]!["exchangeLog"]![0] = actual.DeepClone();
        actualRoot["activeConflict"]!["oppositionSideStrain"] = data["clientActualStrain"]!.DeepClone();
        Assert.True(JsonNode.DeepEquals(ordinary, actual)); Assert.True(JsonNode.DeepEquals(root, actualRoot));
        var raw = Encoding.UTF8.GetBytes(data["ordinaryInputUtf8"]!.GetValue<string>());
        Assert.True(raw.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.True(JsonNode.DeepEquals(root, SpiritualWoundStateJson.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(raw))));
        var fingerprint = new CanonicalBeforeImage(true, raw).Fingerprint;
        Assert.NotEqual(fingerprint, new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(root.ToJsonString())).Fingerprint);
        var expected = new SpiritualWoundCalculation(new(8, 0, 0, "strained", "broken", "hostile", 4), 1, 4, 2, 20, 3, 4, 4, true, 3);
        Assert.Equal(expected, SpiritualWoundOpportunityMath.Calculate(expected.Input));
        foreach (var name in new[] { "ordinaryCalculation", "actualCalculation" })
        {
            Assert.Equal(new[] { "maximumSeverityRank", "traumaPressure" }, data[name]!.AsObject().Select(row => row.Key).OrderBy(key => key, StringComparer.Ordinal));
            Assert.Equal((20L, 3), (data[name]!["traumaPressure"]!.GetValue<long>(), data[name]!["maximumSeverityRank"]!.GetValue<int>()));
        }

        // Independent complete ancestry/profiles exist before the example's full comparison is sealed.
        // This synthetic retained creation DATA is not an executed birth, signature or current membership.
        var fixture = SpiritualGuardianAttributionTestData.Create(GuardianId, rank, GuardianSingletonPressureContour.CappedBroken);
        var ancestryIssues = SpiritualGuardianAttributionDataCheck.Check(fixture.Retained, fixture.Profiles, fixture.Conflict, rank);
        output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_capped_example_complete_fixture", rank, ancestryIssues, fixture.Retained, fixture.Profiles }));
        Assert.Empty(ancestryIssues); Assert.True(JsonNode.DeepEquals(fixture.Conflict, root));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary, ordinary)); Assert.True(JsonNode.DeepEquals(fixture.Actual, actual));
        var retainedParent = Assert.Single(fixture.Retained.Parents); var retainedEffect = Assert.Single(fixture.Retained.Effects);
        Assert.Equal((rank, rank, rank), (retainedParent["severity"]!["rank"]!.GetValue<int>(), retainedParent["consequences"]!["slotsUsed"]!.GetValue<int>(), retainedParent["consequences"]!["slotBudget"]!.GetValue<int>()));
        Assert.Equal(rank, retainedEffect["components"]!.AsArray().Count);
        Assert.Equal(("pressure", 1, 1), (retainedEffect["components"]![0]!["payload"]!["operation"]!.GetValue<string>(),
            retainedEffect["components"]![0]!["payload"]!["magnitude"]!.GetValue<int>(), retainedEffect["stacking"]!["currentStacks"]!.GetValue<int>()));
        var historical = Assert.Single(fixture.Retained.History["transitions"]!.AsArray())!;
        Assert.Equal(("create", 42, 1), (historical["kind"]!.GetValue<string>(), historical["turn"]!.GetValue<int>(), historical["woundTransitionOrdinal"]!.GetValue<int>()));
        var creation = Assert.Single(fixture.Retained.Receipts["sources"]!.AsArray())!["witness"]!.AsObject();
        var birth = SpiritualWoundSourceWitness.ValidateAndReadStrainData(creation, out var noBirthComparison); Assert.Null(noBirthComparison);
        Assert.Equal((0, rank, rank == 3 ? 20L : 25L, rank), (birth.PreviousStrainRank, birth.NewStrainRank, birth.TraumaPressure, birth.MaximumSeverityRank));
        Assert.Equal(creation["sourceFingerprint"]!.GetValue<string>(), historical["sourceFingerprint"]!.GetValue<string>());
        Assert.Equal("materialize", Assert.Single(fixture.Retained.Receipts["decisions"]!.AsArray())!["decision"]!.GetValue<string>());

        JsonObject ComparisonFree(JsonObject exchange) => SpiritualGuardianAttributionTestData.CreateSource(exchange,
            "guardian_capped_example_session", "guardian_capped_example_request", "guardian_capped_example_snapshot",
            active["conflictId"]!.GetValue<string>(), fingerprint);
        foreach (var exchange in new[] { ordinary, actual })
        {
            var original = ComparisonFree(exchange); Assert.False(original.ContainsKey("strainPreimage"));
            Assert.Equal(expected, SpiritualWoundSourceWitness.ValidateAndReadStrainData(original, out var absent)); Assert.Null(absent);
            var issues = new List<ValidationIssue>();
            ValidationService.ValidateRetainedSpiritualExchangeShape(exchange, "guardianCapped.example", issues);
            ValidationService.ValidateSpiritualWoundSourceActionShape(exchange, "guardianCapped.example", issues);
            ValidationService.ValidateRetainedSpiritualDiceAudit(exchange, original["turnEvidence"]!.AsObject(), issues);
            ValidationService.ValidateRetainedSpiritualExchangeRules(exchange, issues); Assert.Empty(issues);
        }
        var source = ComparisonFree(actual); var proof = fixture.Source["strainPreimage"]!.DeepClone().AsObject();
        proof["ordinaryExchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(ordinary);
        proof["ordinaryCalculation"] = data["ordinaryCalculation"]!.DeepClone(); proof["originalInput"]!["contentFingerprint"] = fingerprint;
        source["strainPreimage"] = proof; SpiritualExistingPressureTestData.Seal(source); var beforeSource = source.ToJsonString();
        Assert.Equal(expected, SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var full));
        byte[] differentBytes = [.. raw, (byte)' ']; var differentRaw = new CanonicalBeforeImage(true, differentBytes);
        Assert.True(JsonNode.DeepEquals(root, SpiritualWoundStateJson.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(differentBytes))));
        foreach (var comparison in new[] { Assert.IsType<SpiritualStrainPreimageData>(full), SpiritualStrainPreimageData.ReadIntrinsic(source, actual) })
        {
            Assert.Equal(5, comparison.Version); Assert.Equal(expected, comparison.OrdinaryCalculation); Assert.Equal(expected, comparison.ActualCalculation);
            Assert.Equal(fingerprint, comparison.OriginalInputFingerprint); Assert.Equal("/activeConflict/exchangeLog/0", comparison.OriginalExchangePointer);
            Assert.Equal(rank, Assert.Single(comparison.Parents).ParentWound.Severity.Rank);
            Assert.True(JsonNode.DeepEquals(retainedParent, JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(comparison.ParentWound))));
            Assert.True(JsonNode.DeepEquals(actual, comparison.ReadActualExchange())); Assert.True(JsonNode.DeepEquals(ordinary["after"], comparison.ReadOrdinaryAfter(actual)));
            comparison.ReadActualExchange()["after"]!["oppositionSideStrain"] = "clear";
            Assert.True(JsonNode.DeepEquals(actual, comparison.ReadActualExchange()));
            Assert.NotEqual(comparison.OriginalInputFingerprint, differentRaw.Fingerprint);
        }
        Assert.Equal((AfterlifeSpiritualConflictState.StatePath, true, fingerprint, "/activeConflict/exchangeLog/0"),
            (proof["originalInput"]!["path"]!.GetValue<string>(), proof["originalInput"]!["existed"]!.GetValue<bool>(),
                proof["originalInput"]!["contentFingerprint"]!.GetValue<string>(), proof["originalInput"]!["exchangePointer"]!.GetValue<string>()));
        Assert.Equal(SpiritualWoundFailedAction.Role, proof["inputRoleRevision"]!.GetValue<string>());
        Assert.Equal(new[] { "componentId", "effectId", "inputRoleRevision", "ordinaryCalculation", "ordinaryExchangeJsonBase64", "originalInput", "parentWoundJsonBase64", "version" }, proof.Select(row => row.Key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(beforeSource, source.ToJsonString()); Assert.Equal(beforeData, data.ToJsonString());
        output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_capped_example_full_direct_baseline", rank, expected, root, rawBase64 = Convert.ToBase64String(raw), source,
            scope = "Complete retained DATA/full comparison remains present at equality; no signature, current source or publication authority." }));

        // A valid complete baseline precedes this exact non-strain sibling cut; resealing cannot authorize it.
        var changedActual = actual.DeepClone().AsObject(); changedActual["incomingAction"]!["summary"] = "Changed authored sibling.";
        var changedSource = source.DeepClone().AsObject(); changedSource["exchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(changedActual);
        SpiritualExistingPressureTestData.Seal(changedSource); var unchangedMutation = changedSource.ToJsonString(); SpiritualStrainPreimageData? escaped = full;
        var refusal = Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.ValidateAndReadStrainData(changedSource, out escaped)); Assert.Null(escaped);
        var direct = Assert.Throws<FormatException>(() => SpiritualStrainPreimageData.ReadIntrinsic(changedSource, changedActual));
        Assert.Equal("Only the selected guardian after-strain may differ.", refusal.Message); Assert.Equal(refusal.Message, direct.Message);
        Assert.Equal(unchangedMutation, changedSource.ToJsonString());
        output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_capped_example_sibling_refusal", rank, fullError = refusal.ToString(), directError = direct.ToString(), sourceJson = unchangedMutation }));
    }

    /// <summary>Keeps one existing family/manifest version and separates compact DATA from actual bounded authority qualification.</summary>
    [Fact]
    public void GuardianCappedManifestKeepsOneFamilyAndQualifiedStages()
    {
        var coverage = ExampleValidationManifest.Load().EffectMaterializationCoverage; Assert.Equal(68, coverage.Count);
        var entry = Assert.Single(coverage, row => row.ContractId == Contract);
        Assert.DoesNotContain(coverage, row => row.ContractId == Fragment); Assert.Equal(ExampleFile, entry.File);
        Assert.Equal("focused-fragment", entry.ValidationKind); Assert.Equal(new[] { "Chaos Sea" }, entry.Realms);
        Assert.Contains(nameof(WorkedGuardianCappedFragmentHasIndependentFullV5Data), entry.FocusedFragmentReason, StringComparison.Ordinal);
        Assert.Contains("fixture", entry.FocusedFragmentReason, StringComparison.OrdinalIgnoreCase);
        foreach (var token in new[] { "not runtime qualification", "Task4A", "C4/signed47", "persist byte-exact", "capped-max0" })
            Assert.Contains(token, entry.CoverageLimit, StringComparison.Ordinal);
        var text = Read("Examples/" + ExampleFile); Assert.All(entry.RequiredText, token => Assert.Contains(token, text, StringComparison.Ordinal));
        Assert.Contains("end_guardian_zero_pressure_data_v1", text, StringComparison.Ordinal);
        Assert.Contains("end_" + Fragment, text, StringComparison.Ordinal); _ = ReadFragment();
        var manifest = JsonNode.Parse(Read("Examples/example_validation_manifest.json"))!.AsObject(); Assert.Equal(1, manifest["version"]!.GetValue<int>());
    }

    /// <summary>Reads only the separately delimited compact capped JSON; prior positive/zero fences remain independent.</summary>
    /// <returns>One authored full ordinary root and independent scalar/raw operands.</returns>
    private static JsonObject ReadFragment()
    {
        var text = Read("Examples/" + ExampleFile); var start = text.IndexOf(Fragment + " —", StringComparison.Ordinal);
        Assert.True(start >= 0, "The separately delimited capped DATA fragment must exist.");
        var end = text.IndexOf("end_" + Fragment, start, StringComparison.Ordinal); Assert.True(end > start);
        var block = Assert.Single(Regex.Matches(text[start..end], "```json\\s*([\\s\\S]*?)```", RegexOptions.CultureInvariant).Cast<Match>());
        return JsonNode.Parse(block.Groups[1].Value)!.AsObject();
    }

    /// <summary>Reads one small resource freshly; no mutable fixture, filesystem lease or cache is shared across cases.</summary>
    /// <param name="path">Repository-relative resource path.</param>
    /// <returns>The full current resource text.</returns>
    private static string Read(string path) => File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, path));
}
