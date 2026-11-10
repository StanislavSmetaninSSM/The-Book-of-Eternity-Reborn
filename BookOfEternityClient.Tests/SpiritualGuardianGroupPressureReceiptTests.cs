using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Separately selectable positive-v7 receipt DATA consumers; none manufactures native ancestry or live authority.</summary>
/// <param name="output">Complete uncapped inputs, results and errors for each reached consumer.</param>
public sealed partial class SpiritualGuardianGroupPressureReceiptTests(ITestOutputHelper output)
{
    /// <summary>Retains every ordered parent and complete ancestry prefix while appending one schema1 source/none and independently parsing exact replay.</summary>
    /// <param name="contour">Literal mixed N2, saturated N3 or equal-cap N2 positive input.</param>
    [Theory]
    [InlineData("n2")]
    [InlineData("n3")]
    [InlineData("equal")]
    public void GuardianGroupReceipt_FullSourceParsesAppendsAndReplays(string contour)
    {
        var inputs = CreateInputs(contour);
        var frame = CreateFrame(inputs, retainAncestry: true);
        var rootBefore = frame.Root.ToJsonString();
        var append = SpiritualWoundOpportunityReceiptState.PlanAppend(inputs.PriorState, frame.State);
        Log("complete_prefix_append", new { contour, append.Disposition, afterReturned = append.After is not null, before = inputs.PriorRoot, after = frame.Root });
        Assert.Equal("appended", append.Disposition);
        var appended = Assert.IsType<SpiritualWoundOpportunityReceiptState>(append.After);
        Assert.Equal(SpiritualWoundOpportunityReceiptState.SerializeCanonical(frame.State),
            SpiritualWoundOpportunityReceiptState.SerializeCanonical(appended));
        Assert.Equal(1, frame.Root["schemaVersion"]!.GetValue<int>());
        foreach (var field in new[] { "instances", "sources", "decisions" })
        {
            Assert.Equal(inputs.PriorRoot[field]!.AsArray().Count + 1, frame.Root[field]!.AsArray().Count);
            for (var index = 0; index < inputs.PriorRoot[field]!.AsArray().Count; index++)
                Assert.Equal(inputs.PriorRoot[field]![index]!.ToJsonString(), frame.Root[field]![index]!.ToJsonString());
        }
        Assert.Equal(inputs.PriorRoot["closures"]!.ToJsonString(), frame.Root["closures"]!.ToJsonString());
        Assert.Equal(inputs.PriorRoot["nextClosureOrdinal"]!.GetValue<int>(), frame.Root["nextClosureOrdinal"]!.GetValue<int>());
        foreach (var cursor in new[] { "nextInstanceOrdinal", "nextSourceOrdinal", "nextDecisionOrdinal" })
            Assert.Equal(inputs.PriorRoot[cursor]!.GetValue<int>() + 1, frame.Root[cursor]!.GetValue<int>());
        var sourceRow = frame.Root["sources"]!.AsArray().Last()!;
        var decision = frame.Root["decisions"]!.AsArray().Last()!;
        Assert.Equal("none", Text(decision["decision"]));
        foreach (var field in new[] { "selectedSeverityRank", "woundId", "transitionId" }) Assert.Null(decision[field]);
        Assert.Equal(inputs.Source.ToJsonString(), sourceRow["witness"]!.ToJsonString());
        Assert.Equal(inputs.Source.ToJsonString(), decision["sourceWitness"]!.ToJsonString());
        Assert.Equal(inputs.PriorRoot["nextSourceOrdinal"]!.GetValue<int>(), sourceRow["ordinal"]!.GetValue<int>());
        Assert.Equal(1, sourceRow["instanceSourceOrdinal"]!.GetValue<int>());
        var replayState = ReadReceipt(frame.Root, "independently_parsed_exact_replay");
        var replay = SpiritualWoundOpportunityReceiptState.PlanAppend(frame.State, replayState);
        Log("complete_prefix_replay", new { contour, replay.Disposition, afterReturned = replay.After is not null, receipt = frame.Root });
        Assert.Equal("exact_replay", replay.Disposition);
        Assert.Equal(rootBefore, frame.Root.ToJsonString());
        Assert.Equal(inputs.Fixture.Retained.Receipts.ToJsonString(), inputs.PriorRoot.ToJsonString());
    }

    /// <summary>Builds one complete independent fixture and proves intrinsic, graph, creation and recorded-History premises without testing later consumers.</summary>
    /// <param name="contour">Closed positive fixture selector with literal severities and arithmetic.</param>
    /// <returns>Invocation-local parsed DATA and immutable source premises, never an owner.</returns>
    private Inputs CreateInputs(string contour = "n2")
    {
        var (kind, ranks, ordinary) = contour switch
        {
            "n2" => (GuardianGroupPressureCase.MixedPair, new[] { 3, 4 }, OrdinaryCalculation()),
            "n3" => (GuardianGroupPressureCase.MixedTriple, new[] { 3, 4, 3 }, OrdinaryCalculation()),
            "equal" => (GuardianGroupPressureCase.EqualCapPair, new[] { 3, 4 }, ActualCalculation()),
            _ => throw new ArgumentOutOfRangeException(nameof(contour))
        };
        var fixture = SpiritualGuardianGroupPressureTestData.Create(kind);
        var source = SpiritualGuardianGroupPressureProofData.CreateSource(fixture, ordinary);
        Log("fixture_inputs", new { contour, ranks, expectedOrdinary = ordinary, expectedActual = ActualCalculation(), fixture, source,
            scope = "Synthetic retained ancestry and detached DATA only; not executed births, signing or live C3/C4." });
        var profiles = JsonNode.Parse(fixture.OriginalProfilesJson)!.AsObject();
        var conflict = JsonNode.Parse(fixture.OriginalConflictJson)!.AsObject();
        var issues = SpiritualGuardianAttributionDataCheck.CheckGroup(fixture.Retained, profiles, conflict, ranks);
        Log("complete_original_graph", new { issues, profiles, conflict });
        Assert.Empty(issues);
        var ordinaryBefore = fixture.OrdinarySource.ToJsonString();
        SpiritualWoundCalculation? ordinaryRead = null;
        SpiritualStrainPreimageData? unexpected = null;
        var ordinaryError = Record.Exception(() => ordinaryRead = SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.OrdinarySource, out unexpected));
        Log("comparison_free_ordinary", new { source = fixture.OrdinarySource, ordinaryRead, error = ordinaryError?.ToString() });
        Assert.Null(ordinaryError); Assert.Null(unexpected); Assert.Equal(ordinary, ordinaryRead);
        Assert.Equal(ordinaryBefore, fixture.OrdinarySource.ToJsonString());
        var comparison = ReadSupportedSource(source, fixture.Retained.Parents, ordinary);
        var raw = Convert.FromBase64String(fixture.RawOrdinaryBase64);
        Assert.True(raw.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.Equal(new CanonicalBeforeImage(true, raw).Fingerprint, comparison.OriginalInputFingerprint);
        Assert.Equal("/activeConflict/exchangeLog/0", comparison.OriginalExchangePointer);
        Assert.True(JsonNode.DeepEquals(fixture.OrdinaryRoot, SpiritualWoundStateJson.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(raw))));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary, Decode(source["strainPreimage"]!["ordinaryExchangeJsonBase64"])));
        Assert.Equal(contour == "equal", JsonNode.DeepEquals(fixture.OrdinaryRoot, fixture.ActualRoot));
        var history = ReadHistory(fixture.Retained.History, "complete_recorded_history");
        var priorRoot = fixture.Retained.Receipts.DeepClone().AsObject();
        var prior = ReadReceipt(priorRoot, "complete_ancestry_receipt");
        foreach (var parent in comparison.Parents)
        {
            var wound = parent.ParentWound;
            var found = history.TryResolveExactTransition(wound.LastTransition.TransitionId, out var anchor);
            Log("independent_recorded_parent", new { wound = WoundMaterializationContract.SerializeCanonical(wound), found, anchor });
            Assert.True(found); Assert.NotNull(anchor);
            Assert.Equal((wound.WoundId, wound.LastTransition.Kind, wound.LastTransition.Ordinal, wound.LastTransition.Turn, false,
                    parent.ParentSemanticFingerprint),
                (anchor!.WoundId, anchor.Kind, anchor.WoundTransitionOrdinal, anchor.Turn, anchor.Terminal, anchor.AfterFingerprint));
            var creation = Assert.Single(history.ResolveWoundTransitions(wound.WoundId));
            Assert.True(prior.CoversOriginalCreation(wound.WoundId, Assert.IsType<string>(wound.Origin.OpportunityId), creation));
        }
        return new(fixture, source, comparison, priorRoot, prior, history);
    }

    /// <summary>Checks both real typed intrinsic entry points and every complete parent using explicit literal arithmetic.</summary>
    /// <param name="source">Complete original or deliberately resealed full v7 witness.</param>
    /// <param name="parents">Independently supplied ordered complete parent envelopes.</param>
    /// <param name="ordinary">Literal ordinary10/maxI or equal-cap20/maxIII calculation.</param>
    /// <returns>Already parsed immutable full-witness DATA for reuse within this invocation.</returns>
    private SpiritualStrainPreimageData ReadSupportedSource(JsonObject source, IReadOnlyList<JsonObject> parents,
        SpiritualWoundCalculation ordinary)
    {
        var before = source.ToJsonString();
        SpiritualWoundCalculation? calculation = null;
        SpiritualStrainPreimageData? retained = null;
        var fullError = Record.Exception(() => calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out retained));
        Log("full_witness", new { source, calculation, returned = retained is not null, error = fullError?.ToString() });
        Assert.Null(fullError); Assert.Equal(ActualCalculation(), calculation);
        var data = Assert.IsType<SpiritualStrainPreimageData>(retained);
        SpiritualStrainPreimageData? direct = null;
        var directError = Record.Exception(() => direct = SpiritualStrainPreimageData.ReadIntrinsic(source, Decode(source["exchangeJsonBase64"])));
        Log("direct_intrinsic", new { source, returned = direct is not null, error = directError?.ToString() });
        Assert.Null(directError); Assert.NotNull(direct);
        foreach (var parsed in new[] { data, direct! })
        {
            Assert.Equal(7, parsed.Version);
            Assert.Equal(ordinary, parsed.OrdinaryCalculation); Assert.Equal(ActualCalculation(), parsed.ActualCalculation);
            Assert.Equal(parents.Count, parsed.Parents.Count);
            for (var index = 0; index < parents.Count; index++)
            {
                var parent = parsed.Parents[index];
                Assert.True(JsonNode.DeepEquals(parents[index], JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(parent.ParentWound))));
                Assert.Equal(new WoundOwnerCoordinate("chaos_sea", "guardian", "guardian_frame", WoundCarrierCatalog.AfterlifeProfilesPath), parent.ParentWound.Owner);
                Assert.Null(parent.ParentSourceId);
                Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(parent.ParentWound), parent.ParentSemanticFingerprint);
                Assert.Equal(Text(parents[index]["consequences"]!["ownedEffectSources"]!["rootBindings"]![0]!["effectId"]), parent.EffectId);
                Assert.Equal("component_001", parent.ComponentId);
            }
        }
        Assert.Equal(before, source.ToJsonString());
        return data;
    }

    /// <summary>Resolves the real DATA instance and appends one complete source/none, stopping before presence, log or authentication predicates.</summary>
    /// <param name="inputs">Already independently validated fixture and immutable DATA.</param>
    /// <param name="retainAncestry">Whether to retain the entire historical materialization prefix or isolate pressure-only publication.</param>
    /// <returns>A containing parsed receipt premise for one separately selected consumer.</returns>
    private Frame CreateFrame(Inputs inputs, bool retainAncestry = false)
    {
        var before = retainAncestry ? inputs.PriorRoot : EmptyLedger();
        var source = inputs.Source;
        var evidence = source["turnEvidence"]!;
        var resolved = SpiritualWoundFirstOfferInstanceResolver.Resolve(
            new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(before.ToJsonString())),
            new CanonicalBeforeImage(true, Convert.FromBase64String(inputs.Fixture.RawOrdinaryBase64)), "chaos_sea",
            Text(evidence["sessionId"]), Text(evidence["requestId"]), Text(evidence["snapshotToken"]), 46);
        Log("first_offer_instance_data", new { retainAncestry, before, resolved.InstanceId, resolved.ProposedRow, resolved.Issues });
        Assert.Empty(resolved.Issues);
        var instanceId = Assert.IsType<string>(resolved.InstanceId);
        var proposed = Assert.IsType<JsonObject>(resolved.ProposedRow);
        var root = before.DeepClone().AsObject();
        root["instances"]!.AsArray().Add(proposed.DeepClone());
        root["nextInstanceOrdinal"] = proposed["ordinal"]!.GetValue<int>() + 1;
        _ = ReadReceipt(root, "instance_skeleton");
        AppendSource(root, source, instanceId);
        var state = ReadReceipt(root, "containing_full_v7_receipt");
        return new(inputs, root, state, instanceId);
    }

    /// <summary>Authors one DATA source and none decision using actual next global/instance ordinals and complete context copies.</summary>
    /// <param name="root">Invocation-local receipt candidate whose existing prefixes remain unchanged.</param>
    /// <param name="source">Independently valid full source to copy without modification.</param>
    /// <param name="instanceId">Exact containing open instance identity.</param>
    private static void AppendSource(JsonObject root, JsonObject source, string instanceId)
    {
        var ordinal = root["nextSourceOrdinal"]!.GetValue<int>();
        Assert.Equal(ordinal, root["nextDecisionOrdinal"]!.GetValue<int>());
        var instanceOrdinal = root["sources"]!.AsArray().Count(row => Text(row!["instanceId"]) == instanceId) + 1;
        root["sources"]!.AsArray().Add(new JsonObject { ["ordinal"] = ordinal, ["instanceId"] = instanceId,
            ["instanceSourceOrdinal"] = instanceOrdinal, ["witness"] = source.DeepClone() });
        var evidence = source["turnEvidence"]!;
        var key = SpiritualWoundSourceWitness.HarmEventKey(Text(evidence["sessionId"]), Text(evidence["requestId"]),
            Text(evidence["snapshotToken"]), Text(source["conflictId"]), Text(source["exchangeId"]), "opposition");
        var decision = new JsonObject
        {
            ["ordinal"] = ordinal, ["decisionId"] = "decision_guardian_group_data_" + ordinal, ["instanceId"] = instanceId,
            ["sourceId"] = source["sourceId"]!.DeepClone(), ["opportunityRef"] = "spiritual_wound_" + key,
            ["sourceWitness"] = source.DeepClone(), ["sourceFingerprint"] = source["sourceFingerprint"]!.DeepClone(),
            ["binding"] = new JsonObject { ["sessionId"] = evidence["sessionId"]!.DeepClone(),
                ["requestId"] = evidence["requestId"]!.DeepClone(), ["snapshotToken"] = evidence["snapshotToken"]!.DeepClone(),
                ["turn"] = evidence["turn"]!.DeepClone(), ["continuationGeneration"] = 1, ["waveOrdinal"] = 0, ["sourceOrdinal"] = 0 },
            ["decision"] = "none", ["selectedSeverityRank"] = null, ["woundId"] = null, ["transitionId"] = null,
            ["decisionFingerprint"] = ""
        };
        decision["decisionFingerprint"] = SpiritualWoundStateJson.Hash(decision, "decision", "decisionFingerprint");
        root["decisions"]!.AsArray().Add(decision);
        root["nextSourceOrdinal"] = ordinal + 1; root["nextDecisionOrdinal"] = ordinal + 1;
    }

    /// <summary>Logs the full strict parser result before requiring unconditional desired success.</summary>
    /// <param name="root">Complete baseline or deliberately mutated receipt DATA.</param>
    /// <param name="phase">Distinct causal parser stage.</param>
    /// <returns>Immutable parsed receipt DATA, not permission to publish.</returns>
    private SpiritualWoundOpportunityReceiptState ReadReceipt(JsonObject root, string phase)
    {
        var before = root.ToJsonString();
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(before, SpiritualWoundOpportunityReceiptState.StatePath);
        Log(phase, new { receiptJson = before, parsed.IsValid, parsed.Issues });
        Assert.Equal(before, root.ToJsonString());
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues)); Assert.Empty(parsed.Issues);
        return Assert.IsType<SpiritualWoundOpportunityReceiptState>(parsed.State);
    }

    /// <summary>Independently parses complete chronology before any exact recorded-parent join.</summary>
    /// <param name="root">Invocation-local complete History candidate.</param>
    /// <param name="phase">Distinct chronology prerequisite or mutation stage.</param>
    /// <returns>Validated immutable History DATA.</returns>
    private WoundHistoryState ReadHistory(JsonObject root, string phase)
    {
        var before = root.ToJsonString();
        var parsed = WoundHistoryState.Parse(before, WoundHistoryState.HistoryPath);
        Log(phase, new { historyJson = before, parsed.IsValid, parsed.Issues });
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues)); Assert.Empty(parsed.Issues);
        Assert.Equal(before, root.ToJsonString());
        return Assert.IsType<WoundHistoryState>(parsed.State);
    }

    /// <summary>Creates an isolated schema1 ledger with no historical materializations to mask pressure presence.</summary>
    /// <returns>One invocation's empty mutable receipt DATA.</returns>
    private static JsonObject EmptyLedger() => new()
    {
        ["schemaVersion"] = 1, ["nextInstanceOrdinal"] = 1, ["nextClosureOrdinal"] = 1,
        ["nextSourceOrdinal"] = 1, ["nextDecisionOrdinal"] = 1,
        ["instances"] = new JsonArray(), ["closures"] = new JsonArray(), ["sources"] = new JsonArray(), ["decisions"] = new JsonArray()
    };

    /// <summary>Decodes complete detached DATA through the existing strict UTF-8/object codec.</summary>
    /// <param name="encoded">The supplied base64 object value.</param>
    /// <returns>A private mutable copy for deliberate candidate changes.</returns>
    private static JsonObject Decode(JsonNode? encoded) => SpiritualWoundStateJson.Parse(
        SpiritualWoundStateJson.DecodeUtf8JsonText(SpiritualWoundStateJson.DecodeJsonObjectBytes(encoded)));

    /// <summary>Reads an exact required fixture string without guessing aliases.</summary>
    /// <param name="node">Required string DATA.</param>
    /// <returns>The exact supplied text.</returns>
    private static string Text(JsonNode? node) => node!.GetValue<string>();

    /// <summary>Writes full DATA and observations with no record or byte cap.</summary>
    /// <param name="phase">Named source or consumer boundary.</param>
    /// <param name="data">Complete input/result/error observation for that boundary.</param>
    private void Log(string phase, object data) => output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_receipt_" + phase, data }));

    /// <summary>Supplies the literal independent ordinaryII10/maxI calculation, never inferred from production parent counts.</summary>
    /// <returns>Fixed positive ordinary arithmetic.</returns>
    private static SpiritualWoundCalculation OrdinaryCalculation() =>
        new(new(8, 0, 0, "strained", "fractured", "hostile", 4), 1, 2, 0, 10, 1, 2, 4, true, 1);

    /// <summary>Supplies the literal independent actualIV20/maxIII calculation, also used for equal-cap ordinary DATA.</summary>
    /// <returns>Fixed positive actual arithmetic.</returns>
    private static SpiritualWoundCalculation ActualCalculation() =>
        new(new(8, 0, 0, "strained", "broken", "hostile", 4), 1, 4, 2, 20, 3, 4, 4, true, 3);

    /// <summary>Reuses only parsed immutable DATA within one case; mutable fixture candidates are never shared between invocations.</summary>
    /// <param name="Fixture">Independent original inputs and complete synthetic retained ancestry.</param>
    /// <param name="Source">Complete checked v7 source DATA.</param>
    /// <param name="Comparison">Already parsed immutable intrinsic DATA.</param>
    /// <param name="PriorRoot">Private complete original receipt prefix.</param>
    /// <param name="PriorState">Once-parsed immutable ancestry receipt.</param>
    /// <param name="History">Once-parsed immutable original chronology.</param>
    private sealed record Inputs(GuardianGroupPressureData Fixture, JsonObject Source, SpiritualStrainPreimageData Comparison,
        JsonObject PriorRoot, SpiritualWoundOpportunityReceiptState PriorState, WoundHistoryState History);

    /// <summary>Containing parser premise only; later presence/log/authentication checks belong to individual leaves.</summary>
    /// <param name="Inputs">This invocation's independently validated source and parent premises.</param>
    /// <param name="Root">Private complete receipt candidate.</param>
    /// <param name="State">Once-parsed immutable containing receipt.</param>
    /// <param name="InstanceId">Exact resolved open-instance DATA identity.</param>
    private sealed record Frame(Inputs Inputs, JsonObject Root, SpiritualWoundOpportunityReceiptState State, string InstanceId);
}
