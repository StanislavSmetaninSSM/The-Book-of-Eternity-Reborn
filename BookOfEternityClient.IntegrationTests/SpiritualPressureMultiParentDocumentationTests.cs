using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Checks complete-group GM guidance and literal N3/N4 example DATA without live ownership or runtime qualification.</summary>
public sealed class SpiritualPressureMultiParentDocumentationTests
{
    private const string Contract = "spiritual_existing_pressure_multi_parent_v1";
    private const string ExampleFile = "E_CLI_Spiritual_Multi_Parent_Pressure.txt";

    /// <summary>Pins complete arbitrary group retention, closed private versions and unchanged GM authoring boundaries.</summary>
    /// <param name="path">The active normative or CLI entrypoint, read fresh without mutable shared state.</param>
    [Theory]
    [InlineData("OtherGuides/Wound_Materialization_Contract.md")]
    [InlineData("OtherGuides/Afterlife_Contract_Matrix.md")]
    [InlineData("TaskGuides/CLI_Step_Main.txt")]
    [InlineData("CLI_API_Specification.md")]
    [InlineData("Rules/Block_CLI_Operations.txt")]
    public void MultiParentGuidancePinsCompleteGroupAndBoundaries(string path)
    {
        var text = Regex.Replace(Read(path), @"\s+", " ");
        foreach (var token in new[] { Contract, ExampleFile, "N>=3", "actual=min(4,ordinary+N)",
                     "existing_pressure_ordinary_pair_v1", "existing_pressure_ordinary_group_v1", "ordinal0",
                     "I–III", "pressure8/maxI", "pressure23/maxIV", "all four", "AP3->0", "v1–v4/v6" })
            Assert.Contains(token, text, StringComparison.Ordinal);
        Assert.Contains("spiritual_existing_pressure_capped_group_v1", text, StringComparison.Ordinal);
        Assert.Contains("before < ordinary <= broken", text, StringComparison.Ordinal);
        Assert.Contains("spiritual_existing_pressure_group_zero_v1", text, StringComparison.Ordinal);
        Assert.Contains("receipt-schema3 strainComparisons", text, StringComparison.Ordinal);
        Assert.Contains("Guardian-v5 ordinaryIV remains refused", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Three matching strain parents", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Three parents/other applicable sources", text, StringComparison.Ordinal);
    }

    /// <summary>Runs authored ordinary/actual roots through production v6 readers against independent literal calculation and parent oracles.</summary>
    /// <param name="parents">Independent three/four complete III-parent alternatives, including the fourth parent after saturation.</param>
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void WorkedMultiParentPressureRetainsEveryParentThroughV6Reader(int parents)
    {
        var data = ReadWorkedData();
        Assert.Equal(new[] { 3, 4 }, data["parentVariants"]!.AsArray().Select(row => row!["parentCount"]!.GetValue<int>()));
        var variant = Assert.Single(data["parentVariants"]!.AsArray(), row => row!["parentCount"]!.GetValue<int>() == parents)!;
        var ids = parents == 3
            ? new[] { "wound_retained_pressure_owner_1", "wound_retained_pressure_owner_2", "wound_retained_pressure_owner_3" }
            : new[] { "wound_retained_pressure_owner_1", "wound_retained_pressure_owner_2", "wound_retained_pressure_owner_3", "wound_retained_pressure_owner_4" };
        Assert.Equal(ids, variant["parentWoundIds"]!.AsArray().Select(row => row!.GetValue<string>()));
        var root = data["ordinaryConflict"]!.AsObject();
        var conflict = root["activeConflict"]!.AsObject();
        var ordinary = Assert.Single(conflict["exchangeLog"]!.AsArray())!.AsObject();
        Assert.Equal(("pressure", "setback", 46, "clear", "strained", "strained"),
            (ordinary["operationType"]!.GetValue<string>(), ordinary["outcome"]!.GetValue<string>(),
                ordinary["turnNumber"]!.GetValue<int>(), ordinary["before"]!["playerSideStrain"]!.GetValue<string>(),
                ordinary["after"]!["playerSideStrain"]!.GetValue<string>(), conflict["playerSideStrain"]!.GetValue<string>()));
        Assert.Equal("strained", data["ordinaryFinalEcho"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("broken", data["clientActualStrain"]!.GetValue<string>());
        Assert.Equal(new[] { "/activeConflict/exchangeLog/0/after/playerSideStrain", "/activeConflict/playerSideStrain" },
            data["clientChangedPaths"]!.AsArray().Select(row => row!.GetValue<string>()));
        Assert.Equal((4, 12, -8), (ordinary["diceAudit"]!["playerTotal"]!.GetValue<int>(),
            ordinary["diceAudit"]!["oppositionTotal"]!.GetValue<int>(), ordinary["diceAudit"]!["margin"]!.GetValue<int>()));
        foreach (var side in new[] { "player", "opposition" })
            Assert.Equal((0, 3, 3, 0), (ordinary["actionCostAudit"]![side]!["artTier"]!.GetValue<int>(),
                ordinary["actionCostAudit"]![side]!["effectiveCost"]!.GetValue<int>(),
                ordinary["actionCostAudit"]![side]!["before"]!.GetValue<int>(), ordinary["actionCostAudit"]![side]!["after"]!.GetValue<int>()));
        Assert.Equal((0, 0), (data["appliedArtTier"]!.GetValue<int>(), data["targetResilienceTier"]!.GetValue<int>()));
        Assert.False(ordinary.ContainsKey(SpiritualWoundFailedAction.Property));
        Assert.False(ordinary.ContainsKey("strainPreimage"));
        Assert.False(ordinary.ContainsKey("parentCount"));

        // Complete detached input is prepared once. The expected calculations below are literal, never reader-generated.
        var fixture = SpiritualPressureMultiParentData.Create(parents);
        var before = SpiritualPressureMultiParentData.Serialize(fixture);
        Assert.True(SpiritualPressureRetainedDataAdmissibility.Check(fixture.Retained, fixture.Profiles).IsValid);
        Assert.True(JsonNode.DeepEquals(fixture.Conflict, root));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary, ordinary));
        var expectedOrdinary = new SpiritualWoundCalculation(new(8, 0, 0, "clear", "strained", "hostile", 4),
            0, 1, 0, 8, 1, 1, 4, true, 1);
        var expectedActual = new SpiritualWoundCalculation(new(8, 0, 0, "clear", "broken", "hostile", 4),
            0, 4, 3, 23, 4, 4, 4, true, 4);
        Assert.Equal((8L, 1), (data["ordinaryCalculation"]!["traumaPressure"]!.GetValue<long>(),
            data["ordinaryCalculation"]!["maximumSeverityRank"]!.GetValue<int>()));
        Assert.Equal((23L, 4), (data["actualCalculation"]!["traumaPressure"]!.GetValue<long>(),
            data["actualCalculation"]!["maximumSeverityRank"]!.GetValue<int>()));
        var actualRoot = root.DeepClone().AsObject();
        actualRoot["activeConflict"]!["exchangeLog"]![0]!["after"]!["playerSideStrain"] = "broken";
        actualRoot["activeConflict"]!["playerSideStrain"] = "broken";
        var actual = actualRoot["activeConflict"]!["exchangeLog"]![0]!.AsObject();
        Assert.True(JsonNode.DeepEquals(fixture.Actual, actual));
        var restored = actualRoot.DeepClone().AsObject();
        restored["activeConflict"]!["exchangeLog"]![0]!["after"]!["playerSideStrain"] = "strained";
        restored["activeConflict"]!["playerSideStrain"] = "strained";
        Assert.True(JsonNode.DeepEquals(root, restored));
        var raw = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + root.ToJsonString() + "\n")).ToArray();
        var fingerprint = new CanonicalBeforeImage(true, raw).Fingerprint;
        Assert.Equal(expectedOrdinary, SpiritualWoundSourceWitness.ValidateAndReadStrainData(
            SpiritualPressureMultiParentData.CreateStandaloneSource(ordinary, raw), out var ordinaryComparison));
        Assert.Null(ordinaryComparison);
        var source = SpiritualPressureMultiParentData.CreateStandaloneSource(actual, raw);
        var proof = fixture.Source["strainPreimage"]!.DeepClone().AsObject();
        proof["ordinaryExchangeJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(ordinary.ToJsonString()));
        proof["originalInput"]!["contentFingerprint"] = fingerprint;
        proof["ordinaryCalculation"] = data["ordinaryCalculation"]!.DeepClone();
        source["strainPreimage"] = proof;
        SpiritualExistingPressureTestData.Seal(source);

        Assert.Equal(expectedActual, SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var retained));
        var comparison = Assert.IsType<SpiritualStrainPreimageData>(retained);
        Assert.Equal(6, comparison.Version);
        Assert.Equal("existing_pressure_ordinary_group_v1", proof["inputRoleRevision"]!.GetValue<string>());
        Assert.Equal(expectedOrdinary, comparison.OrdinaryCalculation);
        Assert.Equal(expectedActual, comparison.ActualCalculation);
        Assert.Equal(parents, comparison.Parents.Count);
        Assert.Equal(fingerprint, comparison.OriginalInputFingerprint);
        Assert.Equal("/activeConflict/exchangeLog/0", comparison.OriginalExchangePointer);
        Assert.True(JsonNode.DeepEquals(actual, comparison.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(ordinary["after"], comparison.ReadOrdinaryAfter(actual)));
        for (var index = 0; index < parents; index++)
        {
            var parent = comparison.Parents[index].ParentWound;
            Assert.Equal(ids[index], parent.WoundId);
            Assert.Equal((3, 3, 3), (parent.Severity.Rank, parent.Consequences.SlotsUsed, parent.Consequences.SlotBudget));
            Assert.True(JsonNode.DeepEquals(fixture.Retained.Parents[index],
                JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(parent))));
            Assert.Equal((fixture.Retained.Effects[index]["effectId"]!.GetValue<string>(), "component_001"),
                (comparison.Parents[index].EffectId, comparison.Parents[index].ComponentId));
        }
        var intrinsic = Assert.IsType<SpiritualStrainPreimageData>(SpiritualStrainPreimageData.ReadIntrinsic(source, actual));
        Assert.Equal(parents, intrinsic.Parents.Count);
        Assert.Equal(expectedActual, intrinsic.ActualCalculation);
        comparison.ReadActualExchange()["after"]!["playerSideStrain"] = "clear";
        comparison.ReadOrdinaryAfter(actual)["playerSideStrain"] = "clear";
        Assert.True(JsonNode.DeepEquals(actual, comparison.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(ordinary["after"], comparison.ReadOrdinaryAfter(actual)));
        Assert.Equal(before, SpiritualPressureMultiParentData.Serialize(fixture));
    }

    /// <summary>Registers this exact family as focused DATA coverage without live qualification claims.</summary>
    [Fact]
    public void MultiParentManifestKeepsFocusedUnqualifiedCoverage()
    {
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage, row => row.ContractId == Contract);
        Assert.Equal(ExampleFile, entry.File);
        Assert.Equal(new[] { "Chaos Sea" }, entry.Realms);
        Assert.Equal("focused-fragment", entry.ValidationKind);
        Assert.Contains(nameof(WorkedMultiParentPressureRetainsEveryParentThroughV6Reader), entry.FocusedFragmentReason, StringComparison.Ordinal);
        Assert.Contains("not runtime qualification", entry.CoverageLimit, StringComparison.Ordinal);
        Assert.Contains("native births", entry.CoverageLimit, StringComparison.Ordinal);
        var example = Read("Examples/" + ExampleFile);
        Assert.All(entry.RequiredText, token => Assert.Contains(token, example, StringComparison.Ordinal));
    }

    /// <summary>Checks the unchanged operational-resource wildcard ships the new example without a separate packaging path.</summary>
    [Fact]
    public void MultiParentExampleIsPackagedByExistingOperationalResourceLink()
    {
        var packaged = Path.Combine(AppContext.BaseDirectory, "operational-resources", "Examples", ExampleFile);
        Assert.True(File.Exists(packaged), "The existing Examples/**/*.txt resource Link must ship the complete-group example.");
        Assert.Equal(File.ReadAllBytes(Path.Combine(TestRepoPaths.RepoRoot, "Examples", ExampleFile)), File.ReadAllBytes(packaged));
    }

    /// <summary>Reads the one delimited JSON fence as explanatory DATA, never submitted wrapper fields.</summary>
    /// <returns>A fresh independently mutable authored example root.</returns>
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

    /// <summary>Reads one small document freshly; trivial reads do not justify a cache or live world setup.</summary>
    /// <param name="path">Repository-relative document path.</param>
    /// <returns>Complete document text.</returns>
    private static string Read(string path) => File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, path));
}
