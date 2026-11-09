using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Checks complete authored maximum-zero group DATA without signatures, live owners or publication authority.</summary>
public sealed class SpiritualGroupZeroPressureDocumentationTests
{
    private const string Contract = "spiritual_existing_pressure_group_zero_v1";
    private const string ExampleFile = "E_CLI_Afterlife_Turns.txt";

    /// <summary>Keeps active normative and GM guidance on ordinary-only input and comparison-only completion.</summary>
    /// <param name="path">One current document; each read has an independent short-lived value.</param>
    [Theory]
    [InlineData("OtherGuides/Wound_Materialization_Contract.md")]
    [InlineData("OtherGuides/Afterlife_Contract_Matrix.md")]
    [InlineData("TaskGuides/CLI_Step_Main.txt")]
    [InlineData("CLI_API_Specification.md")]
    [InlineData("Rules/Block_CLI_Operations.txt")]
    [InlineData("specs/1536-complete-wound-materialization/spec.md")]
    [InlineData("specs/1536-complete-wound-materialization/data-model.md")]
    [InlineData("specs/1536-complete-wound-materialization/contracts/wound-effects-and-atomicity.md")]
    public void GroupZeroGuidanceKeepsOrdinaryInputAndNoOfferLifecycle(string path)
    {
        var text = Regex.Replace(Read(path), @"\s+", " ");
        foreach (var token in new[] { Contract, ExampleFile, "before < ordinary <= broken",
                     "actual=min(4,ordinary+N)", "ordinal0", "no_offer", "source-only C3", "C2 Open",
                     "receipt-schema3", "strainComparisons", "AP3->0", "History", "Guardian-v5", "Guardian-v5 maximum0 is comparison-only" })
            Assert.Contains(token, text, StringComparison.Ordinal);
    }

    /// <summary>Runs both independently authored complete roots and ancestry through real readers and the receipt parser.</summary>
    /// <param name="contour">Only N2 low=0 and N3 near-cap=1, not a count-by-rank product.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void WorkedGroupZeroPressureKeepsCompleteProvenanceAndNoDecision(int contour)
    {
        if (contour is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(contour));
        var worked = ReadWorkedData();
        Assert.Equal(new[] { "N2 low", "N3 near cap" }, worked["cases"]!.AsArray().Select(row => row!["name"]!.GetValue<string>()));
        var data = worked["cases"]![contour]!.AsObject();
        var inputBefore = data.ToJsonString();
        var count = contour == 0 ? 2 : 3;
        var version = contour == 0 ? 4 : 6;
        int[] ranks = contour == 0 ? [3, 4] : [3, 3, 3];
        string[] woundIds = contour == 0
            ? ["wound_retained_pressure_owner_1", "wound_retained_pressure_owner_2"]
            : ["wound_retained_pressure_owner_1", "wound_retained_pressure_owner_2", "wound_retained_pressure_owner_3"];
        var expectedOrdinary = contour == 0
            ? new SpiritualWoundCalculation(new(6, 0, 5, "clear", "strained", "hostile", 4), 0, 1, 0, -4, 0, 1, 4, true, 0)
            : new SpiritualWoundCalculation(new(8, 0, 5, "fractured", "overwhelmed", "hostile", 4), 2, 3, 0, 2, 0, 3, 4, true, 0);
        var expectedActual = contour == 0
            ? new SpiritualWoundCalculation(new(6, 0, 5, "clear", "overwhelmed", "hostile", 4), 0, 3, 2, 6, 0, 3, 4, true, 0)
            : new SpiritualWoundCalculation(new(8, 0, 5, "fractured", "broken", "hostile", 4), 2, 4, 1, 7, 0, 4, 4, true, 0);
        Assert.Equal(count, data["parentCount"]!.GetValue<int>());
        Assert.Equal(woundIds, data["parentWoundIds"]!.AsArray().Select(row => row!.GetValue<string>()));
        Assert.Equal(ranks, data["parentRanks"]!.AsArray().Select(row => row!.GetValue<int>()));
        Assert.Equal((0, 5), (data["appliedArtTier"]!.GetValue<int>(), data["targetResilienceTier"]!.GetValue<int>()));
        Assert.Equal(expectedOrdinary, SpiritualWoundOpportunityMath.Calculate(expectedOrdinary.Input));
        Assert.Equal(expectedActual, SpiritualWoundOpportunityMath.Calculate(expectedActual.Input));
        Assert.Equal((contour == 0 ? -4L : 2L, 0), (data["ordinaryCalculation"]!["traumaPressure"]!.GetValue<long>(), data["ordinaryCalculation"]!["maximumSeverityRank"]!.GetValue<int>()));
        Assert.Equal((contour == 0 ? 6L : 7L, 0), (data["actualCalculation"]!["traumaPressure"]!.GetValue<long>(), data["actualCalculation"]!["maximumSeverityRank"]!.GetValue<int>()));

        var root = data["ordinaryConflict"]!.AsObject();
        Assert.True(JsonNode.DeepEquals(ReadExpectedOrdinaryRoot(contour), root));
        var ordinary = Assert.Single(root["activeConflict"]!["exchangeLog"]!.AsArray())!.AsObject();
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["playerSideStrain"] = expectedOrdinary.Input.NewStrain }, data["ordinaryFinalEcho"]));
        Assert.Equal(expectedActual.Input.NewStrain, data["clientActualStrain"]!.GetValue<string>());
        var paths = new[] { "/activeConflict/exchangeLog/0/after/playerSideStrain", "/activeConflict/playerSideStrain" };
        Assert.Equal(paths, data["clientManagedPaths"]!.AsArray().Select(row => row!.GetValue<string>()));
        Assert.Equal(paths, data["clientChangedPaths"]!.AsArray().Select(row => row!.GetValue<string>()));
        Assert.False(ordinary.ContainsKey("strainPreimage"));
        Assert.False(ordinary.ContainsKey("parentCount"));
        Assert.False(ordinary.ContainsKey(SpiritualWoundFailedAction.Property));
        var premise = data["preTurnPremise"]!;
        Assert.Equal((46, expectedOrdinary.Input.PreviousStrain, 3, 3, 5), (premise["turn"]!.GetValue<int>(),
            premise["playerSideStrain"]!.GetValue<string>(), premise["playerActionPoints"]!.GetValue<int>(),
            premise["oppositionActionPoints"]!.GetValue<int>(), premise["soulSpiritualResilienceTier"]!.GetValue<int>()));
        Assert.Equal(contour == 0 ? new[] { 6, 12 } : new[] { 4, 12 }, premise["preGeneratedDices1d20"]!.AsArray().Select(row => row!.GetValue<int>()));

        var retainedInput = data["retainedState"]!;
        var profiles = retainedInput["afterlifeProfiles"]!.AsObject();
        var player = Assert.Single(profiles["profiles"]!.AsArray(), row => row!["actorType"]!.GetValue<string>() == "player_soul")!;
        Assert.Equal("player_soul", player["actorId"]!.GetValue<string>());
        Assert.Equal(5, player["standardArts"]!["spiritual_resilience"]!.GetValue<int>());
        var parents = player["activeWounds"]!.AsArray().Select(row => row!.DeepClone().AsObject()).ToArray();
        var effects = player["activeEffects"]!.AsArray().Select(row => row!.DeepClone().AsObject()).ToArray();
        var retained = new RetainedPressureGroupData(parents, effects, retainedInput["woundIdentities"]!.AsObject(),
            retainedInput["history"]!.AsObject(), retainedInput["effectIdentities"]!.AsObject(), retainedInput["creationReceipts"]!.AsObject());
        Assert.Equal(count, parents.Length);
        Assert.Equal(count, effects.Length);
        Assert.Equal(woundIds, parents.Select(row => row["woundId"]!.GetValue<string>()));
        var admissible = SpiritualPressureRetainedDataAdmissibility.Check(retained, profiles);
        Assert.True(admissible.IsValid, JsonSerializer.Serialize(admissible));
        AssertLiteralProvenance(retained, ranks, contour);

        var raw = Encoding.UTF8.GetBytes(data["ordinaryInputUtf8"]!.GetValue<string>());
        Assert.True(raw.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.True(JsonNode.DeepEquals(root, SpiritualWoundStateJson.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(raw))));
        var fingerprint = new CanonicalBeforeImage(true, raw).Fingerprint;
        Assert.NotEqual(new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(root.ToJsonString())).Fingerprint, fingerprint);
        var actualRoot = root.DeepClone().AsObject();
        actualRoot["activeConflict"]!["exchangeLog"]![0]!["after"]!["playerSideStrain"] = expectedActual.Input.NewStrain;
        actualRoot["activeConflict"]!["playerSideStrain"] = expectedActual.Input.NewStrain;
        var actual = actualRoot["activeConflict"]!["exchangeLog"]![0]!.AsObject();
        var restored = actualRoot.DeepClone();
        restored["activeConflict"]!["exchangeLog"]![0]!["after"]!["playerSideStrain"] = expectedOrdinary.Input.NewStrain;
        restored["activeConflict"]!["playerSideStrain"] = expectedOrdinary.Input.NewStrain;
        Assert.True(JsonNode.DeepEquals(root, restored));
        var conflictId = root["activeConflict"]!["conflictId"]!.GetValue<string>();
        var ordinarySource = SpiritualGroupZeroPressureData.CreateStandaloneSource(ordinary, expectedOrdinary, raw, conflictId);
        Assert.Equal(expectedOrdinary, SpiritualWoundSourceWitness.ValidateAndReadStrainData(ordinarySource, out var absent));
        Assert.Null(absent);
        var source = SpiritualGroupZeroPressureData.CreateStandaloneSource(actual, expectedActual, raw, conflictId);
        source["strainPreimage"] = new JsonObject
        {
            ["version"] = version,
            ["inputRoleRevision"] = contour == 0 ? "existing_pressure_ordinary_pair_v1" : "existing_pressure_ordinary_group_v1",
            ["ordinaryExchangeJsonBase64"] = Encode(ordinary),
            ["originalInput"] = new JsonObject { ["path"] = AfterlifeSpiritualConflictState.StatePath, ["existed"] = true,
                ["contentFingerprint"] = fingerprint, ["exchangePointer"] = "/activeConflict/exchangeLog/0" },
            ["parents"] = new JsonArray(parents.Select((parent, index) => (JsonNode)new JsonObject
            {
                ["parentWoundJsonBase64"] = Encode(parent), ["effectId"] = effects[index]["effectId"]!.DeepClone(), ["componentId"] = "component_001"
            }).ToArray()),
            ["ordinaryCalculation"] = data["ordinaryCalculation"]!.DeepClone()
        };
        SpiritualExistingPressureTestData.Seal(source);
        var sourceBefore = source.ToJsonString();
        Assert.Equal(expectedActual, SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var full));
        var direct = SpiritualStrainPreimageData.ReadIntrinsic(source, actual);
        foreach (var comparison in new[] { Assert.IsType<SpiritualStrainPreimageData>(full), direct })
        {
            Assert.Equal(version, comparison.Version);
            Assert.Equal(expectedOrdinary, comparison.OrdinaryCalculation);
            Assert.Equal(expectedActual, comparison.ActualCalculation);
            Assert.Equal(count, comparison.Parents.Count);
            Assert.Equal(fingerprint, comparison.OriginalInputFingerprint);
            Assert.Equal("/activeConflict/exchangeLog/0", comparison.OriginalExchangePointer);
            for (var index = 0; index < count; index++)
            {
                Assert.Equal(woundIds[index], comparison.Parents[index].ParentWound.WoundId);
                Assert.Equal(ranks[index], comparison.Parents[index].ParentWound.Severity.Rank);
                Assert.True(JsonNode.DeepEquals(parents[index], JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(comparison.Parents[index].ParentWound))));
                Assert.Equal(("effect_retained_pressure_owner_" + (index + 1), "component_001"),
                    (comparison.Parents[index].EffectId, comparison.Parents[index].ComponentId));
            }
            comparison.ReadActualExchange()["after"]!["playerSideStrain"] = "clear";
            comparison.ReadOrdinaryAfter(actual)["playerSideStrain"] = "clear";
            Assert.True(JsonNode.DeepEquals(actual, comparison.ReadActualExchange()));
            Assert.True(JsonNode.DeepEquals(ordinary["after"], comparison.ReadOrdinaryAfter(actual)));
        }
        AssertComparisonOnlyAppend(retained, source, raw, actualRoot);
        Assert.Equal(sourceBefore, source.ToJsonString());
        Assert.Equal(inputBefore, data.ToJsonString());
    }

    /// <summary>Pins the focused family's exact inventory and explicit unqualified scope.</summary>
    [Fact]
    public void GroupZeroManifestKeepsFocusedDataAndQualificationSeparate()
    {
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage, row => row.ContractId == Contract);
        Assert.Equal(ExampleFile, entry.File);
        Assert.Equal(new[] { "Chaos Sea" }, entry.Realms);
        Assert.Equal("focused-fragment", entry.ValidationKind);
        Assert.Contains(nameof(WorkedGroupZeroPressureKeepsCompleteProvenanceAndNoDecision), entry.FocusedFragmentReason, StringComparison.Ordinal);
        foreach (var token in new[] { "not runtime qualification", "native births", "current authority", "UNRUN" })
            Assert.Contains(token, entry.CoverageLimit, StringComparison.Ordinal);
        var text = Read("Examples/" + ExampleFile);
        Assert.All(entry.RequiredText, token => Assert.Contains(token, text, StringComparison.Ordinal));
    }

    /// <summary>Keeps the selected existing-context-pack path and ordinary operational-resource packaging explicit.</summary>
    [Fact]
    public void GroupZeroExampleUsesExistingContextPackAndResourcePackaging()
    {
        var packaged = Path.Combine(AppContext.BaseDirectory, "operational-resources", "Examples", ExampleFile);
        Assert.True(File.Exists(packaged));
        Assert.Equal(File.ReadAllBytes(Path.Combine(TestRepoPaths.RepoRoot, "Examples", ExampleFile)), File.ReadAllBytes(packaged));
        var daemon = Read("BookOfEternityClient/game_master_daemon.ps1");
        Assert.Contains("@(\"Examples\\E_CLI_Afterlife_Turns.txt\", \"afterlife_turn_examples\")", daemon, StringComparison.Ordinal);
        Assert.Contains("$script:AfterlifeTurnsExamplePath = Join-Path $script:GmContextPackRoot", daemon, StringComparison.Ordinal);
    }

    /// <summary>Checks literal first-creation identity, rank, chronology, hashes and every component sibling.</summary>
    /// <param name="retained">Complete authored ancestry parsed from the example, not generated expectations.</param>
    /// <param name="ranks">Independent literal severities for the selected contour.</param>
    /// <param name="contour">Low pair or near-cap triple.</param>
    private static void AssertLiteralProvenance(RetainedPressureGroupData retained, int[] ranks, int contour)
    {
        string[] semanticHashes = contour == 0
            ? ["a45455b421340383e52efc00ec7a6436b7766e08af5f8cba7016bb81cd736f32", "8068685d9bd2c27ec2faaae0dd9a423cad6f23305817e93510f104a8a49539b2"]
            : ["a45455b421340383e52efc00ec7a6436b7766e08af5f8cba7016bb81cd736f32", "4284230f89b6adbf2f7b8d0fd21d345f6892a2792f3d17add25c0841b4c1794a", "297427ea222cd1b9f054a29ab231f6407ac0800d000dbe545898639e806eeab6"];
        string[] sourceHashes = contour == 0
            ? ["8ebe09ef219d54990a342f520a8fc2dd6e9ae49103dbc4d4a22745a92fd7c9c4", "b125cab0d968d62a7b3ef5998d4759f83e4406d1a6ee5b5499b79425f6ef670d"]
            : ["8ebe09ef219d54990a342f520a8fc2dd6e9ae49103dbc4d4a22745a92fd7c9c4", "04191e8d264becc2527994a57b7f0f7d21b484f1cb6ffa209fadf5169de69dc4", "05c5052347dec48b797d7b1ebd08f08724691e7eda68a88a8a1500e4a22ed8c6"];
        Assert.Equal(ranks.Length, retained.History["transitions"]!.AsArray().Count);
        foreach (var field in new[] { "instances", "closures", "sources", "decisions" })
            Assert.Equal(ranks.Length, retained.Receipts[field]!.AsArray().Count);
        for (var index = 0; index < ranks.Length; index++)
        {
            var rank = ranks[index];
            var wound = retained.Parents[index];
            var effect = retained.Effects[index];
            var history = retained.History["transitions"]![index]!;
            var source = retained.Receipts["sources"]![index]!["witness"]!.AsObject();
            var decision = retained.Receipts["decisions"]![index]!;
            Assert.Equal((rank, rank, rank, 2 * rank), (wound["severity"]!["rank"]!.GetValue<int>(),
                wound["consequences"]!["slotsUsed"]!.GetValue<int>(), wound["consequences"]!["slotBudget"]!.GetValue<int>(), wound["recovery"]!["currentStepThreshold"]!.GetValue<int>()));
            Assert.Equal(("create", 41 + index, 1, "wound_transition_retained_pressure_owner_" + (index + 1), "operation_retained_pressure_owner_" + (index + 1)),
                (history["kind"]!.GetValue<string>(), history["turn"]!.GetValue<int>(), history["woundTransitionOrdinal"]!.GetValue<int>(),
                    history["transitionId"]!.GetValue<string>(), history["operationKey"]!.GetValue<string>()));
            Assert.Equal("sha256:" + semanticHashes[index], history["afterFingerprint"]!.GetValue<string>());
            Assert.Equal("sha256:" + sourceHashes[index], history["sourceFingerprint"]!.GetValue<string>());
            Assert.Equal(41 + index, wound["origin"]!["createdAtTurn"]!.GetValue<int>());
            Assert.Equal(("materialize", rank, "wound_retained_pressure_owner_" + (index + 1)),
                (decision["decision"]!.GetValue<string>(), decision["selectedSeverityRank"]!.GetValue<int>(), decision["woundId"]!.GetValue<string>()));
            Assert.Equal(source["sourceId"]!.GetValue<string>(), decision["sourceId"]!.GetValue<string>());
            Assert.Equal(wound["origin"]!["opportunityId"]!.GetValue<string>(), decision["opportunityRef"]!.GetValue<string>());
            Assert.True(JsonNode.DeepEquals(source, decision["sourceWitness"]));
            var birth = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var birthComparison);
            Assert.Null(birthComparison);
            Assert.Equal((0, rank, rank == 4 ? 25L : 20L, rank), (birth.PreviousStrainRank, birth.NewStrainRank, birth.TraumaPressure, birth.MaximumSeverityRank));
            var components = effect["components"]!.AsArray();
            Assert.Equal(rank, components.Count);
            Assert.Equal(("pressure", 1), (components[0]!["payload"]!["operation"]!.GetValue<string>(), components[0]!["payload"]!["magnitude"]!.GetValue<int>()));
            Assert.Equal(("counter", "disadvantage"), (components[1]!["payload"]!["operation"]!.GetValue<string>(), components[1]!["payload"]!["magnitude"]!.GetValue<string>()));
            Assert.Equal(("guard", rank == 4 ? 3 : 2), (components[2]!["payload"]!["operation"]!.GetValue<string>(), components[2]!["payload"]!["magnitude"]!.GetValue<int>()));
            if (rank == 4)
                Assert.Equal(("counter", 3), (components[3]!["payload"]!["operation"]!.GetValue<string>(), components[3]!["payload"]!["magnitude"]!.GetValue<int>()));
        }
    }

    /// <summary>Appends one real-parser comparison DATA row without creating owner or reducer authority.</summary>
    /// <param name="retained">Exact existing creation receipt and History prefixes.</param>
    /// <param name="source">Reader-accepted actual v4/v6 witness.</param>
    /// <param name="raw">Full authored ordinary bytes.</param>
    /// <param name="actualRoot">Client-only actual projection for signed-log matching DATA.</param>
    private static void AssertComparisonOnlyAppend(RetainedPressureGroupData retained, JsonObject source, byte[] raw, JsonObject actualRoot)
    {
        var before = retained.Receipts.DeepClone().AsObject();
        var baseline = ReadReceipt(before);
        var evidence = source["turnEvidence"]!;
        var resolved = SpiritualWoundFirstOfferInstanceResolver.Resolve(new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(before.ToJsonString())),
            new CanonicalBeforeImage(true, raw), "chaos_sea", evidence["sessionId"]!.GetValue<string>(),
            evidence["requestId"]!.GetValue<string>(), evidence["snapshotToken"]!.GetValue<string>(), 46);
        Assert.Empty(resolved.Issues);
        var instance = Assert.IsType<JsonObject>(resolved.ProposedRow);
        var next = before.DeepClone().AsObject();
        next["instances"]!.AsArray().Add(instance.DeepClone());
        next["nextInstanceOrdinal"] = instance["ordinal"]!.GetValue<int>() + 1;
        next["schemaVersion"] = 3;
        next["healingSources"] = new JsonArray(); next["healingDecisions"] = new JsonArray();
        next["nextHealingSourceOrdinal"] = 1; next["nextHealingDecisionOrdinal"] = 1;
        next["nextStrainComparisonOrdinal"] = 2;
        next["strainComparisons"] = new JsonArray(new JsonObject { ["ordinal"] = 1, ["instanceId"] = resolved.InstanceId,
            ["instanceComparisonOrdinal"] = 1, ["witness"] = source.DeepClone() });
        var state = ReadReceipt(next);
        foreach (var field in new[] { "sources", "decisions", "closures", "nextSourceOrdinal", "nextDecisionOrdinal", "nextClosureOrdinal" })
            Assert.True(JsonNode.DeepEquals(before[field], next[field]), field);
        Assert.Single(next["strainComparisons"]!.AsArray());
        Assert.Equal(retained.Parents.Count, next["sources"]!.AsArray().Count);
        Assert.Equal(retained.Parents.Count, next["decisions"]!.AsArray().Count);
        Assert.DoesNotContain(next["sources"]!.AsArray(), row => row!["witness"]!["sourceId"]!.GetValue<string>() == source["sourceId"]!.GetValue<string>());
        Assert.DoesNotContain(next["decisions"]!.AsArray(), row => row!["sourceId"]!.GetValue<string>() == source["sourceId"]!.GetValue<string>());
        var history = WoundHistoryState.Parse(retained.History.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(history.IsValid, JsonSerializer.Serialize(history.Issues));
        Assert.True(state.HasExactStrainHistory(history.State!));
        Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact,
            state.MatchRetainedPressureHistory("chaos_sea", actualRoot["activeConflict"]!.AsObject(), 46));
        Assert.Equal("appended", SpiritualWoundOpportunityReceiptState.PlanAppend(baseline, state).Disposition);
        Assert.Equal("exact_replay", SpiritualWoundOpportunityReceiptState.PlanAppend(state, ReadReceipt(next)).Disposition);
    }

    /// <summary>Reads the complete strict receipt without resealing or normalizing malformed example DATA.</summary>
    /// <param name="root">One detached complete receipt.</param>
    /// <returns>Immutable parsed comparison DATA.</returns>
    private static SpiritualWoundOpportunityReceiptState ReadReceipt(JsonObject root)
    {
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(root.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues));
        return Assert.IsType<SpiritualWoundOpportunityReceiptState>(parsed.State);
    }

    /// <summary>Independently spells every ordinary-root sibling; only literal low/near-cap inputs differ.</summary>
    /// <param name="contour">The two closed authored alternatives.</param>
    /// <returns>A complete expected root, not calculated or copied from a production fixture.</returns>
    private static JsonObject ReadExpectedOrdinaryRoot(int contour)
    {
        var text = """
            {"schemaVersion":1,"activeConflict":{"conflictId":"CONFLICT","realm":"Chaos Sea","sideModel":"direct_duel",
            "dangerMode":"hostile","resolutionState":"active","playerSideStrain":"ORDINARY","oppositionSideStrain":"clear",
            "conflictPosition":"contested","controlState":null,"tempoAdvantage":null,"exchangeLog":[{
            "exchangeId":"exchange_pressure_group_46","turnNumber":46,"exchangeAtTurn":46,"operationType":"pressure","outcome":"setback",
            "summary":"Душа встречает давление Хранителя.","before":{"playerSideStrain":"BEFORE","oppositionSideStrain":"clear",
            "conflictPosition":"contested","controlState":null,"tempoAdvantage":null},"after":{"playerSideStrain":"ORDINARY",
            "oppositionSideStrain":"clear","conflictPosition":"contested","controlState":null,"tempoAdvantage":null},
            "incomingAction":{"operationType":"pressure","actorType":"guardian","actorId":"guardian_frame","summary":"Хранитель оказывает давление."},
            "matchupAudit":{"playerOperation":"pressure","oppositionOperation":"pressure","primaryResolutionLane":"pressure",
            "riskProfile":"offensive_pressure","matchupRationale":"Встречное давление."},"actionCostAudit":{
            "player":{"operationType":"pressure","baseCost":3,"minCost":1,"artTier":0,"effectiveCost":3,"before":3,"after":0,"max":10},
            "opposition":{"operationType":"pressure","baseCost":3,"minCost":1,"artTier":0,"effectiveCost":3,"before":3,"after":0,"max":9}},
            "diceAudit":{"formulaVersion":"afterlife_spiritual_conflict_v1","diceSource":"input/turn_request.json.preGeneratedDices1d20",
            "diceUsed":[{"side":"player","sourceIndex":0,"sides":20,"value":PLAYER_DIE},{"side":"opposition","sourceIndex":1,"sides":20,"value":12}],
            "playerTotal":PLAYER_DIE,"oppositionTotal":12,"margin":MARGIN,"outcomeBand":"BAND","modifierBreakdown":{"player":[],"opposition":[]}}}],
            "combatConditions":[],"playerSide":{"leadContestant":{"actorType":"player","actorId":"player_soul"},"supporters":[]},
            "oppositionSide":{"leadContestant":{"actorType":"guardian","actorId":"guardian_frame"},"supporters":[]}},"recentConflicts":[]}
            """;
        var fields = contour switch
        {
            0 => ("pair_low_data", "clear", "strained", "6", "-6", "opposition_success"),
            1 => ("group_zero_data", "fractured", "overwhelmed", "4", "-8", "decisive_opposition_success"),
            _ => throw new ArgumentOutOfRangeException(nameof(contour))
        };
        text = text.Replace("CONFLICT", fields.Item1, StringComparison.Ordinal).Replace("BEFORE", fields.Item2, StringComparison.Ordinal)
            .Replace("ORDINARY", fields.Item3, StringComparison.Ordinal).Replace("PLAYER_DIE", fields.Item4, StringComparison.Ordinal)
            .Replace("MARGIN", fields.Item5, StringComparison.Ordinal).Replace("BAND", fields.Item6, StringComparison.Ordinal);
        return JsonNode.Parse(text)!.AsObject();
    }

    /// <summary>Reads one separately delimited family with exactly one explanatory JSON fence.</summary>
    /// <returns>Fresh independent complete example inputs.</returns>
    private static JsonObject ReadWorkedData()
    {
        var text = Read("Examples/" + ExampleFile);
        var start = text.IndexOf(Contract + " — worked", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = text.IndexOf("end_" + Contract, start, StringComparison.Ordinal);
        Assert.True(end > start);
        var fence = Assert.Single(Regex.Matches(text[start..end], "```json\\s*([\\s\\S]*?)```", RegexOptions.CultureInvariant).Cast<Match>());
        return JsonNode.Parse(fence.Groups[1].Value)!.AsObject();
    }

    /// <summary>Encodes complete comparison DATA without providing live authority.</summary>
    /// <param name="value">Detached canonical parent or exchange.</param>
    /// <returns>Base64 UTF-8 canonical JSON.</returns>
    private static string Encode(JsonNode value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(value)));

    /// <summary>Reads documents freshly; cheap bounded DATA requires no cache or game setup.</summary>
    /// <param name="path">Repository-relative path.</param>
    /// <returns>Complete document text.</returns>
    private static string Read(string path) => File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, path));
}
