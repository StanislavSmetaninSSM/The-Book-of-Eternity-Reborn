using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Checks authored guardian-zero DATA through typed readers without issuing signatures, owners or publication authority.</summary>
public sealed class SpiritualGuardianZeroPressureDocumentationTests
{
    private const string Contract = "spiritual_existing_guardian_pressure_attribution_v1";
    private const string Fragment = "guardian_zero_pressure_data_v1";
    private const string ExampleFile = "E_CLI_Spiritual_Guardian_Pressure_Attribution.txt";

    /// <summary>Pins the ordinary-only input, distinct failed guardian action and comparison-only lifecycle in active guidance.</summary>
    /// <param name="path">One independently read current guidance document.</param>
    [Theory]
    [InlineData("specs/1536-complete-wound-materialization/data-model.md")]
    [InlineData("specs/1536-complete-wound-materialization/contracts/wound-effects-and-atomicity.md")]
    [InlineData("OtherGuides/Wound_Materialization_Contract.md")]
    [InlineData("OtherGuides/Afterlife_Contract_Matrix.md")]
    [InlineData("TaskGuides/CLI_Step_Main.txt")]
    [InlineData("CLI_API_Specification.md")]
    [InlineData("Examples/E_CLI_Afterlife_Turns.txt")]
    [InlineData("Rules/Block_CLI_Operations.txt")]
    public void GuardianZeroGuidanceKeepsExactActionAndNoOfferLifecycle(string path)
    {
        var text = Regex.Replace(Read(path), @"\s+", " ");
        foreach (var token in new[] { Contract, Fragment, ExampleFile, "Guardian-v5 maximum0 is comparison-only",
                     "singleton III/IV", "beforeI", "ordinaryII/III", "actual=ordinary+1", "ordinal0",
                     "guardian ordinaryIV/capped", "multi-parent remain refused", "spiritualWoundFailedAction",
                     "/incomingAction", "player pressure/success", "margin inversion", "profile", "resilience4",
                     "ordinary2/max0 -> actual7/max0", "ordinary6/max0 -> actual11/maxI", "C1 no offer", "C2 no_offer",
                     "fresh validator/capture/lease", "source-only C3", "No C2 Open", "fabricated none",
                     "receipt-schema3", "strainComparisons", "no new injury source or decision", "History",
                     "raw/BOM/pointer/request", "AP3->0", "not runtime qualification" })
            Assert.Contains(token, text, StringComparison.Ordinal);
        Assert.DoesNotContain("Guardian-v5 remains positive-only", text, StringComparison.Ordinal);
        if (path == "OtherGuides/Wound_Materialization_Contract.md")
        {
            var start = text.IndexOf("### " + Contract, StringComparison.Ordinal);
            Assert.True(start >= 0);
            var end = text.IndexOf("## spiritual_wound_position_v1", start, StringComparison.Ordinal);
            Assert.True(end > start);
            Assert.DoesNotContain("Не добавляйте вторую рану, другого актёра, обычный I-result или capped/max0 вариант.",
                text[start..end], StringComparison.Ordinal);
        }
    }

    /// <summary>Reads each complete authored original/ancestry and checks v5/schema3 append and replay with literal independent arithmetic.</summary>
    /// <param name="rank">Independent expected parent severity III or IV, never inferred from the example's result.</param>
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void WorkedGuardianZeroPressureKeepsCompleteProvenanceAndNoDecision(int rank)
    {
        var worked = ReadWorkedData();
        Assert.Equal(new[] { 3, 4 }, worked["parentRankVariants"]!.AsArray().Select(row => row!.GetValue<int>()));
        var data = Assert.Single(worked["cases"]!.AsArray().OfType<JsonObject>(), row => row["parentRank"]!.GetValue<int>() == rank);
        var inputBefore = data.ToJsonString();
        var expectedOrdinary = new SpiritualWoundCalculation(new(8, 0, 4, "strained", "fractured", "hostile", 4), 1, 2, 0, 2, 0, 2, 4, true, 0);
        var expectedActual = new SpiritualWoundCalculation(new(8, 0, 4, "strained", "overwhelmed", "hostile", 4), 1, 3, 1, 7, 0, 3, 4, true, 0);
        Assert.Equal(expectedOrdinary, SpiritualWoundOpportunityMath.Calculate(expectedOrdinary.Input));
        Assert.Equal(expectedActual, SpiritualWoundOpportunityMath.Calculate(expectedActual.Input));
        Assert.Equal((2L, 0, 7L, 0), (data["ordinaryCalculation"]!["traumaPressure"]!.GetValue<long>(),
            data["ordinaryCalculation"]!["maximumSeverityRank"]!.GetValue<int>(), data["actualCalculation"]!["traumaPressure"]!.GetValue<long>(),
            data["actualCalculation"]!["maximumSeverityRank"]!.GetValue<int>()));
        var premise = data["preTurnPremise"]!;
        Assert.Equal((46, "strained", 3, 3, 4), (premise["turn"]!.GetValue<int>(), premise["guardianSideStrain"]!.GetValue<string>(),
            premise["playerActionPoints"]!.GetValue<int>(), premise["oppositionActionPoints"]!.GetValue<int>(), premise["guardianSpiritualResilienceTier"]!.GetValue<int>()));
        Assert.Equal(new[] { 12, 4 }, premise["preGeneratedDices1d20"]!.AsArray().Select(row => row!.GetValue<int>()));

        var root = data["ordinaryConflict"]!.AsObject();
        var active = root["activeConflict"]!.AsObject();
        var ordinary = Assert.Single(active["exchangeLog"]!.AsArray())!.AsObject();
        Assert.Equal(("Chaos Sea", "direct_duel", "hostile", "active", "clear", "fractured"),
            (active["realm"]!.GetValue<string>(), active["sideModel"]!.GetValue<string>(), active["dangerMode"]!.GetValue<string>(),
                active["resolutionState"]!.GetValue<string>(), active["playerSideStrain"]!.GetValue<string>(), active["oppositionSideStrain"]!.GetValue<string>()));
        Assert.Equal(("pressure", "success", "strained", "fractured"), (ordinary["operationType"]!.GetValue<string>(), ordinary["outcome"]!.GetValue<string>(),
            ordinary["before"]!["oppositionSideStrain"]!.GetValue<string>(), ordinary["after"]!["oppositionSideStrain"]!.GetValue<string>()));
        Assert.Equal("player_soul", active["playerSide"]!["leadContestant"]!["actorId"]!.GetValue<string>());
        Assert.Equal("guardian_frame", active["oppositionSide"]!["leadContestant"]!["actorId"]!.GetValue<string>());
        Assert.Equal("guardian_frame", SpiritualWoundFailedAction.ReadGuardianDeclaration(ordinary));
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["version"] = 1, ["side"] = "opposition", ["actorType"] = "guardian",
            ["actorId"] = "guardian_frame", ["actionPath"] = "/incomingAction", ["operationType"] = "pressure" }, ordinary[SpiritualWoundFailedAction.Property]));
        foreach (var field in new[] { "actorType", "ownerActorType" })
            Assert.Equal("guardian", ordinary["incomingAction"]![field]!.GetValue<string>());
        foreach (var field in new[] { "actorId", "actorRef", "ownerActorId", "guardianId", "id" })
            Assert.Equal("guardian_frame", ordinary["incomingAction"]![field]!.GetValue<string>());
        Assert.Equal((12, 4, 8, "decisive_player_success"), (ordinary["diceAudit"]!["playerTotal"]!.GetValue<int>(),
            ordinary["diceAudit"]!["oppositionTotal"]!.GetValue<int>(), ordinary["diceAudit"]!["margin"]!.GetValue<int>(), ordinary["diceAudit"]!["outcomeBand"]!.GetValue<string>()));
        foreach (var side in new[] { "player", "opposition" })
            Assert.Equal((3, 3, 0), (ordinary["actionCostAudit"]![side]!["effectiveCost"]!.GetValue<int>(),
                ordinary["actionCostAudit"]![side]!["before"]!.GetValue<int>(), ordinary["actionCostAudit"]![side]!["after"]!.GetValue<int>()));
        var paths = new[] { "/activeConflict/exchangeLog/0/after/oppositionSideStrain", "/activeConflict/oppositionSideStrain" };
        Assert.Equal(paths, data["clientChangedPaths"]!.AsArray().Select(row => row!.GetValue<string>()));
        Assert.Equal("overwhelmed", data["clientActualStrain"]!.GetValue<string>());
        var actualRoot = root.DeepClone().AsObject();
        actualRoot["activeConflict"]!["exchangeLog"]![0]!["after"]!["oppositionSideStrain"] = "overwhelmed";
        actualRoot["activeConflict"]!["oppositionSideStrain"] = "overwhelmed";
        var actual = actualRoot["activeConflict"]!["exchangeLog"]![0]!.AsObject();
        var restored = actualRoot.DeepClone().AsObject();
        restored["activeConflict"]!["exchangeLog"]![0]!["after"]!["oppositionSideStrain"] = "fractured";
        restored["activeConflict"]!["oppositionSideStrain"] = "fractured";
        Assert.True(JsonNode.DeepEquals(root, restored));
        var raw = Encoding.UTF8.GetBytes(data["ordinaryInputUtf8"]!.GetValue<string>());
        Assert.True(raw.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.True(JsonNode.DeepEquals(root, SpiritualWoundStateJson.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(raw))));
        var fingerprint = new CanonicalBeforeImage(true, raw).Fingerprint;
        Assert.NotEqual(fingerprint, new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(root.ToJsonString())).Fingerprint);

        var ancestry = data["retainedState"]!;
        var profiles = ancestry["afterlifeProfiles"]!.AsObject();
        var guardian = Assert.Single(profiles["profiles"]!.AsArray().OfType<JsonObject>(), row => row["actorType"]!.GetValue<string>() == "guardian");
        Assert.Equal(4, guardian["standardArts"]!["spiritual_resilience"]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(guardian["standardArts"], active["oppositionSide"]!["leadContestant"]!["actorArtTierSnapshot"]));
        var parent = Assert.Single(guardian["activeWounds"]!.AsArray())!.AsObject();
        var effect = Assert.Single(guardian["activeEffects"]!.AsArray())!.AsObject();
        var retained = new RetainedPressureGroupData([parent], [effect], ancestry["woundIdentities"]!.AsObject(),
            ancestry["history"]!.AsObject(), ancestry["effectIdentities"]!.AsObject(), ancestry["creationReceipts"]!.AsObject());
        Assert.Empty(SpiritualGuardianAttributionDataCheck.Check(retained, profiles, root, rank));
        AssertLiteralAncestry(retained, rank);

        var next = data["comparisonOnlyReceipt"]!.AsObject();
        var row = Assert.Single(next["strainComparisons"]!.AsArray())!.AsObject();
        var source = row["witness"]!.AsObject();
        var sourceBefore = source.ToJsonString();
        Assert.Equal(("player_soul", "player_soul", "guardian", "guardian_frame"), (source["actingActor"]!["actorKind"]!.GetValue<string>(),
            source["actingActor"]!["actorId"]!.GetValue<string>(), source["affectedActor"]!["actorKind"]!.GetValue<string>(), source["affectedActor"]!["actorId"]!.GetValue<string>()));
        Assert.Equal((0, 4, 8), (source["appliedArtTier"]!.GetValue<int>(), source["targetResilienceTier"]!.GetValue<int>(), source["harmfulMargin"]!.GetValue<int>()));
        Assert.Null(source["guaranteedSeverityRank"]);
        var ordinarySource = SpiritualGuardianZeroPressureTestData.CreateComparisonFreeSource(ordinary, 4, "ordinary_example_session",
            "ordinary_example_request", "ordinary_example_snapshot", active["conflictId"]!.GetValue<string>(), fingerprint);
        Assert.Equal(expectedOrdinary, SpiritualWoundSourceWitness.ValidateAndReadStrainData(ordinarySource, out var absent));
        Assert.Null(absent);
        Assert.Equal(expectedActual, SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var full));
        foreach (var comparison in new[] { Assert.IsType<SpiritualStrainPreimageData>(full), SpiritualStrainPreimageData.ReadIntrinsic(source, actual) })
        {
            Assert.Equal(5, comparison.Version);
            Assert.Single(comparison.Parents);
            Assert.Equal(expectedOrdinary, comparison.OrdinaryCalculation);
            Assert.Equal(expectedActual, comparison.ActualCalculation);
            Assert.Equal(rank, comparison.ParentWound.Severity.Rank);
            Assert.True(JsonNode.DeepEquals(parent, JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(comparison.ParentWound))));
            Assert.Equal(("effect_retained_guardian_data_1", "component_001"), (comparison.Parents[0].EffectId, comparison.Parents[0].ComponentId));
            Assert.Equal(fingerprint, comparison.OriginalInputFingerprint);
            Assert.Equal("/activeConflict/exchangeLog/0", comparison.OriginalExchangePointer);
            Assert.True(JsonNode.DeepEquals(actual, comparison.ReadActualExchange()));
            Assert.True(JsonNode.DeepEquals(ordinary["after"], comparison.ReadOrdinaryAfter(actual)));
            comparison.ReadActualExchange()["after"]!["oppositionSideStrain"] = "clear";
            comparison.ReadOrdinaryAfter(actual)["oppositionSideStrain"] = "clear";
            Assert.True(JsonNode.DeepEquals(actual, comparison.ReadActualExchange()));
            Assert.True(JsonNode.DeepEquals(ordinary["after"], comparison.ReadOrdinaryAfter(actual)));
        }
        Assert.Equal(SpiritualWoundFailedAction.Role, source["strainPreimage"]!["inputRoleRevision"]!.GetValue<string>());
        var issues = new List<ValidationIssue>();
        foreach (var exchange in new[] { ordinary, actual })
        {
            ValidationService.ValidateRetainedSpiritualExchangeShape(exchange, "guardianZero.example", issues);
            ValidationService.ValidateSpiritualWoundSourceActionShape(exchange, "guardianZero.example", issues);
            ValidationService.ValidateRetainedSpiritualDiceAudit(exchange, source["turnEvidence"]!.AsObject(), issues);
            ValidationService.ValidateRetainedSpiritualExchangeRules(exchange, issues);
        }
        Assert.Empty(issues);
        AssertComparisonOnlyAppend(retained, next, raw, actualRoot);
        Assert.Equal(sourceBefore, source.ToJsonString());
        Assert.Equal(inputBefore, data.ToJsonString());
    }

    /// <summary>Extends the existing manifest family without presenting detached DATA as live or wider qualification.</summary>
    [Fact]
    public void GuardianZeroManifestKeepsExistingFamilyAndDataScope()
    {
        var coverage = ExampleValidationManifest.Load().EffectMaterializationCoverage;
        var entry = Assert.Single(coverage, row => row.ContractId == Contract);
        Assert.DoesNotContain(coverage, row => row.ContractId == Fragment);
        Assert.Equal(ExampleFile, entry.File);
        Assert.Equal(new[] { "Chaos Sea" }, entry.Realms);
        Assert.Equal("focused-fragment", entry.ValidationKind);
        Assert.Contains(nameof(WorkedGuardianZeroPressureKeepsCompleteProvenanceAndNoDecision), entry.FocusedFragmentReason, StringComparison.Ordinal);
        foreach (var token in new[] { "not runtime qualification", "native births", "source-only preparation", "C4 writes/signed47", "rollback/retry" })
            Assert.Contains(token, entry.CoverageLimit, StringComparison.OrdinalIgnoreCase);
        var text = Read("Examples/" + ExampleFile);
        Assert.All(entry.RequiredText, token => Assert.Contains(token, text, StringComparison.Ordinal));
        _ = ReadWorkedData();
    }

    /// <summary>Checks independently fixed birth chronology, severity siblings and literal identity/source anchors from the example.</summary>
    /// <param name="retained">Complete parent, effect, identity, first-creation receipt and History DATA read from the example.</param>
    /// <param name="rank">Independent expected III or IV severity.</param>
    private static void AssertLiteralAncestry(RetainedPressureGroupData retained, int rank)
    {
        var parent = retained.Parents.Single();
        var effect = retained.Effects.Single();
        var transition = Assert.Single(retained.History["transitions"]!.AsArray())!;
        var birthSource = Assert.Single(retained.Receipts["sources"]!.AsArray())!["witness"]!.AsObject();
        var expectedSemantic = rank == 3 ? "f87fe0fd1f8bb36c86ba383fdd91114d5080ed6a2f655853bfd769f79d1ef7c9" : "492ea2b535530f4ff6c09229bfe581c6bea6e7fa1a58842e86f5a6eb54b4cf52";
        var expectedSource = rank == 3 ? "cca624778ebe78c0d2a42e5c6e436e71d02490b7daaeb7708fc7ca31d555e0c6" : "20c7442e907eaacb10d053d92d819eb623c7bfa4511e23f656ce3896df4d381c";
        Assert.Equal("sha256:" + expectedSemantic, transition["afterFingerprint"]!.GetValue<string>());
        Assert.Equal("sha256:" + expectedSource, transition["sourceFingerprint"]!.GetValue<string>());
        Assert.Equal((rank, rank, rank, 2 * rank), (parent["severity"]!["rank"]!.GetValue<int>(), parent["consequences"]!["slotsUsed"]!.GetValue<int>(),
            parent["consequences"]!["slotBudget"]!.GetValue<int>(), parent["recovery"]!["currentStepThreshold"]!.GetValue<int>()));
        Assert.Equal(("create", 42, 1, "wound_transition_retained_guardian_data_1"), (transition["kind"]!.GetValue<string>(),
            transition["turn"]!.GetValue<int>(), transition["woundTransitionOrdinal"]!.GetValue<int>(), transition["transitionId"]!.GetValue<string>()));
        Assert.Equal(0, birthSource["targetResilienceTier"]!.GetValue<int>());
        var birth = SpiritualWoundSourceWitness.ValidateAndReadStrainData(birthSource, out var noComparison);
        Assert.Null(noComparison);
        Assert.Equal((0, rank, rank == 4 ? 25L : 20L, rank), (birth.PreviousStrainRank, birth.NewStrainRank, birth.TraumaPressure, birth.MaximumSeverityRank));
        var components = effect["components"]!.AsArray();
        Assert.Equal(rank, components.Count);
        Assert.Equal(("pressure", 1), (components[0]!["payload"]!["operation"]!.GetValue<string>(), components[0]!["payload"]!["magnitude"]!.GetValue<int>()));
        Assert.Equal(("counter", "disadvantage"), (components[1]!["payload"]!["operation"]!.GetValue<string>(), components[1]!["payload"]!["magnitude"]!.GetValue<string>()));
        Assert.Equal(("guard", rank == 4 ? 3 : 2), (components[2]!["payload"]!["operation"]!.GetValue<string>(), components[2]!["payload"]!["magnitude"]!.GetValue<int>()));
        if (rank == 4)
            Assert.Equal(("counter", 3), (components[3]!["payload"]!["operation"]!.GetValue<string>(), components[3]!["payload"]!["magnitude"]!.GetValue<int>()));
    }

    /// <summary>Checks a real resolver/schema1 skeleton, strict schema3 parse, all immutable prefixes and exact replay without an owner key.</summary>
    /// <param name="retained">Exact pre-existing receipt and History prefixes.</param>
    /// <param name="next">Complete authored schema3 candidate from the example.</param>
    /// <param name="raw">Full original ordinary UTF-8 bytes, including BOM and whitespace.</param>
    /// <param name="actualRoot">The ordinary root with only its two guardian strain projections changed.</param>
    private static void AssertComparisonOnlyAppend(RetainedPressureGroupData retained, JsonObject next, byte[] raw, JsonObject actualRoot)
    {
        var before = retained.Receipts;
        var baseline = ReadReceipt(before);
        var row = Assert.Single(next["strainComparisons"]!.AsArray())!;
        var source = row["witness"]!.AsObject();
        var evidence = source["turnEvidence"]!;
        var resolved = SpiritualWoundFirstOfferInstanceResolver.Resolve(new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(before.ToJsonString())),
            new CanonicalBeforeImage(true, raw), "chaos_sea", evidence["sessionId"]!.GetValue<string>(), evidence["requestId"]!.GetValue<string>(), evidence["snapshotToken"]!.GetValue<string>(), 46);
        Assert.Empty(resolved.Issues);
        var proposed = Assert.IsType<JsonObject>(resolved.ProposedRow);
        var skeleton = before.DeepClone().AsObject();
        skeleton["instances"]!.AsArray().Add(proposed.DeepClone());
        skeleton["nextInstanceOrdinal"] = 3;
        Assert.Equal(1, skeleton["schemaVersion"]!.GetValue<int>());
        Assert.Equal("appended", SpiritualWoundOpportunityReceiptState.PlanAppend(baseline, ReadReceipt(skeleton)).Disposition);
        Assert.Equal(3, next["schemaVersion"]!.GetValue<int>());
        foreach (var (field, value) in before)
            if (field is not ("schemaVersion" or "instances" or "nextInstanceOrdinal"))
                Assert.True(JsonNode.DeepEquals(value, next[field]), field);
        Assert.Equal(2, next["instances"]!.AsArray().Count);
        Assert.True(JsonNode.DeepEquals(before["instances"]![0], next["instances"]![0]));
        Assert.True(JsonNode.DeepEquals(proposed, next["instances"]![1]));
        Assert.Equal(3, next["nextInstanceOrdinal"]!.GetValue<int>());
        Assert.Equal((1, 1, 2), (row["ordinal"]!.GetValue<int>(), row["instanceComparisonOrdinal"]!.GetValue<int>(), next["nextStrainComparisonOrdinal"]!.GetValue<int>()));
        Assert.Equal(resolved.InstanceId, row["instanceId"]!.GetValue<string>());
        Assert.Single(next["sources"]!.AsArray());
        Assert.Single(next["decisions"]!.AsArray());
        Assert.Empty(next["healingSources"]!.AsArray());
        Assert.Empty(next["healingDecisions"]!.AsArray());
        Assert.Equal((1, 1), (next["nextHealingSourceOrdinal"]!.GetValue<int>(), next["nextHealingDecisionOrdinal"]!.GetValue<int>()));
        Assert.DoesNotContain(next["sources"]!.AsArray(), item => item!["witness"]!["sourceId"]!.GetValue<string>() == source["sourceId"]!.GetValue<string>());
        Assert.DoesNotContain(next["decisions"]!.AsArray(), item => item!["sourceId"]!.GetValue<string>() == source["sourceId"]!.GetValue<string>());
        var history = WoundHistoryState.Parse(retained.History.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(history.IsValid, JsonSerializer.Serialize(history.Issues));
        var state = ReadReceipt(next);
        Assert.True(state.HasRetainedPressureComparisons);
        Assert.True(state.HasExactStrainHistory(history.State!));
        var active = actualRoot["activeConflict"]!.AsObject();
        Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact, state.MatchRetainedPressureHistory("chaos_sea", active, 46));
        Assert.True(JsonNode.DeepEquals(retained.Parents.Single(), JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(
            SpiritualStrainPreimageData.ReadIntrinsic(source, active["exchangeLog"]![0]!.AsObject()).ParentWound))));
        var append = SpiritualWoundOpportunityReceiptState.PlanAppend(baseline, state);
        Assert.Equal("appended", append.Disposition);
        var canonical = SpiritualWoundOpportunityReceiptState.SerializeCanonical(state);
        Assert.Equal(canonical, SpiritualWoundOpportunityReceiptState.SerializeCanonical(Assert.IsType<SpiritualWoundOpportunityReceiptState>(append.After)));
        Assert.Equal("exact_replay", SpiritualWoundOpportunityReceiptState.PlanAppend(state, ReadReceipt(next)).Disposition);
        var callerCopy = next.DeepClone().AsObject();
        var detached = ReadReceipt(callerCopy);
        callerCopy["strainComparisons"]![0]!["witness"]!["maximumSeverityRank"] = 4;
        Assert.Equal(canonical, SpiritualWoundOpportunityReceiptState.SerializeCanonical(detached));
        Assert.Equal("exact_replay", SpiritualWoundOpportunityReceiptState.PlanAppend(state, detached).Disposition);
    }

    /// <summary>Parses one complete detached receipt candidate through the actual typed parser.</summary>
    /// <param name="root">Complete candidate with no parser bypass or live capability.</param>
    /// <returns>The typed immutable parsed receipt state.</returns>
    private static SpiritualWoundOpportunityReceiptState ReadReceipt(JsonObject root)
    {
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(root.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues));
        return Assert.IsType<SpiritualWoundOpportunityReceiptState>(parsed.State);
    }

    /// <summary>Reads only the separate zero delimiter, keeping the original positive worked JSON outside this reader.</summary>
    /// <returns>The complete zero-fragment wrapper, not a GameResponse or owner.</returns>
    private static JsonObject ReadWorkedData()
    {
        var text = Read("Examples/" + ExampleFile);
        var start = text.IndexOf(Fragment + " — DATA-only", StringComparison.Ordinal);
        Assert.True(start > text.IndexOf("end_" + Contract, StringComparison.Ordinal));
        var end = text.IndexOf("end_" + Fragment, start, StringComparison.Ordinal);
        Assert.True(end > start);
        var block = Assert.Single(Regex.Matches(text[start..end], "```json\\s*([\\s\\S]*?)```", RegexOptions.CultureInvariant).Cast<Match>());
        return JsonNode.Parse(block.Groups[1].Value)!.AsObject();
    }

    /// <summary>Reads one cheap document fresh; no repeated expensive setup, shared mutable state or cache is required.</summary>
    /// <param name="path">Repository-relative document path.</param>
    /// <returns>The complete current document text.</returns>
    private static string Read(string path) => File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, path));
}
