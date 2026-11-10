using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualGuardianGroupZeroReceiptTests
{
    /// <summary>Creates one cheap LowMarginZeroPair and retains once-parsed original receipt, History and full-source DATA for this case only.</summary>
    /// <returns>Invocation-local immutable parsed facts and separate mutable input copies, never current owner authority.</returns>
    private Inputs CreateInputs()
    {
        var fixture = SpiritualGuardianGroupPressureTestData.Create(GuardianGroupPressureCase.LowMarginZeroPair);
        var fixtureJson = JsonSerializer.Serialize(fixture);
        var profiles = JsonNode.Parse(fixture.OriginalProfilesJson)!.AsObject();
        var conflict = JsonNode.Parse(fixture.OriginalConflictJson)!.AsObject();
        var issues = SpiritualGuardianAttributionDataCheck.CheckGroup(fixture.Retained, profiles, conflict, [3, 4]);
        Log("complete_original_graph", new { fixture, profiles, conflict, issues,
            scope = "Synthetic retained ancestry and detached DATA only; no executed births, signing, source-only C3 or C4." });
        Assert.Empty(issues);
        Assert.Empty(conflict["activeConflict"]!["exchangeLog"]!.AsArray());
        Assert.Equal("strained", Text(conflict["activeConflict"]!["oppositionSideStrain"]));
        Assert.Equal(new[] { 3, 4 }, fixture.Retained.Parents.Select(parent => parent["severity"]!["rank"]!.GetValue<int>()).ToArray());
        var guardianProfile = Assert.Single(profiles["profiles"]!.AsArray().Where(row => Text(row!["actorType"]) == "guardian"))!;
        Assert.Equal(5, guardianProfile["standardArts"]![AfterlifeSpiritualConflictState.SpiritualResilienceArtId]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(guardianProfile["standardArts"], conflict["activeConflict"]!["oppositionSide"]!["leadContestant"]!["actorArtTierSnapshot"]));
        var originalSnapshot = JsonNode.Parse(fixture.OriginalSnapshotJson)!.AsObject();
        var originalRoots = SpiritualGuardianAttributionDataCheck.Roots(fixture.Retained, profiles, conflict);
        var snapshotFingerprint = new CanonicalBeforeImage(true,
            Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(originalSnapshot))).Fingerprint;
        Log("complete_original_snapshot", new { originalSnapshot, originalRoots, snapshotFingerprint,
            ordinaryEvidence = fixture.OrdinarySource["turnEvidence"], actualEvidence = fixture.ActualSource["turnEvidence"] });
        Assert.Equal(originalRoots.Keys.Order(StringComparer.Ordinal), originalSnapshot.Select(field => field.Key).Order(StringComparer.Ordinal));
        foreach (var (path, root) in originalRoots) Assert.True(JsonNode.DeepEquals(root, originalSnapshot[path]), path);
        Assert.Equal(snapshotFingerprint, Text(fixture.OrdinarySource["turnEvidence"]!["originalSnapshotFingerprint"]));
        Assert.Equal(snapshotFingerprint, Text(fixture.ActualSource["turnEvidence"]!["originalSnapshotFingerprint"]));
        ReadComparisonFreeSource(fixture.OrdinarySource, fixture.Ordinary, OrdinaryCalculation(), "comparison_free_ordinary");
        ReadComparisonFreeSource(fixture.ActualSource, fixture.Actual, ActualCalculation(), "comparison_free_actual");

        var source = SpiritualGuardianGroupPressureProofData.CreateSource(fixture, OrdinaryCalculation());
        var sourceJson = source.ToJsonString();
        SpiritualWoundCalculation? calculation = null;
        SpiritualStrainPreimageData? comparison = null;
        var error = Record.Exception(() => calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out comparison));
        Log("full_v7_source", new { source, calculation, retained = comparison is not null, error = error?.ToString(),
            expectedOrdinary = OrdinaryCalculation(), expectedActual = ActualCalculation(), parents = fixture.Retained.Parents });
        Assert.Null(error); Assert.Equal(ActualCalculation(), calculation);
        var data = Assert.IsType<SpiritualStrainPreimageData>(comparison);
        Assert.Equal(7, data.Version); Assert.Equal(2, data.Parents.Count);
        Assert.Equal(OrdinaryCalculation(), data.OrdinaryCalculation); Assert.Equal(ActualCalculation(), data.ActualCalculation);
        Assert.Equal((1, 2, 4, -4L, 0, 6L, 0), (data.OrdinaryCalculation!.PreviousStrainRank,
            data.OrdinaryCalculation.NewStrainRank, data.ActualCalculation!.NewStrainRank,
            data.OrdinaryCalculation.TraumaPressure, data.OrdinaryCalculation.MaximumSeverityRank,
            data.ActualCalculation.TraumaPressure, data.ActualCalculation.MaximumSeverityRank));
        Assert.Equal("existing_guardian_pressure_failed_action_group_v1", Text(source["strainPreimage"]!["inputRoleRevision"]));
        Assert.Equal(new[] { "inputRoleRevision", "ordinaryCalculation", "ordinaryExchangeJsonBase64", "originalInput", "parents", "version" },
            source["strainPreimage"]!.AsObject().Select(field => field.Key).Order(StringComparer.Ordinal).ToArray());
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary, Decode(source["strainPreimage"]!["ordinaryExchangeJsonBase64"])));
        Assert.True(JsonNode.DeepEquals(fixture.Actual, Decode(source["exchangeJsonBase64"])));
        var raw = Convert.FromBase64String(fixture.RawOrdinaryBase64);
        var rawFingerprint = new CanonicalBeforeImage(true, raw).Fingerprint;
        Log("raw_ordinary_identity", new { fixture.RawOrdinaryBase64, rawFingerprint, fixture.OriginalInput,
            decoded = SpiritualWoundStateJson.DecodeUtf8JsonText(raw), fixture.OrdinaryRoot, fixture.ActualRoot,
            data.OriginalInputFingerprint, data.OriginalExchangePointer });
        Assert.True(raw.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.Equal(" \n" + fixture.OrdinaryRoot.ToJsonString() + "\n", SpiritualWoundStateJson.DecodeUtf8JsonText(raw));
        Assert.True(JsonNode.DeepEquals(fixture.OrdinaryRoot, SpiritualWoundStateJson.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(raw))));
        Assert.Equal(rawFingerprint, data.OriginalInputFingerprint); Assert.Equal("/activeConflict/exchangeLog/0", data.OriginalExchangePointer);
        Assert.NotEqual(rawFingerprint, new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(fixture.OrdinaryRoot.ToJsonString())).Fingerprint);
        Assert.Equal((AfterlifeSpiritualConflictState.StatePath, true, rawFingerprint, "/activeConflict/exchangeLog/0"),
            (Text(fixture.OriginalInput["path"]), fixture.OriginalInput["existed"]!.GetValue<bool>(),
                Text(fixture.OriginalInput["contentFingerprint"]), Text(fixture.OriginalInput["exchangePointer"])));
        var expectedOrdinary = conflict.DeepClone();
        expectedOrdinary["activeConflict"]!["exchangeLog"]!.AsArray().Add(fixture.Ordinary.DeepClone());
        expectedOrdinary["activeConflict"]!["oppositionSideStrain"] = "fractured";
        Assert.True(JsonNode.DeepEquals(expectedOrdinary, fixture.OrdinaryRoot));
        var expectedActual = fixture.OrdinaryRoot.DeepClone();
        expectedActual["activeConflict"]!["exchangeLog"]![0] = fixture.Actual.DeepClone();
        expectedActual["activeConflict"]!["oppositionSideStrain"] = "broken";
        Assert.True(JsonNode.DeepEquals(expectedActual, fixture.ActualRoot));
        Assert.Equal(fixture.OriginalProfilesJson, profiles.ToJsonString());
        Assert.Equal(fixture.OriginalConflictJson, conflict.ToJsonString());

        var priorRoot = fixture.Retained.Receipts.DeepClone().AsObject();
        var priorJson = priorRoot.ToJsonString();
        var prior = ReadReceipt(priorRoot, "complete_ancestry_receipt");
        var historyJson = fixture.Retained.History.ToJsonString();
        var parsedHistory = WoundHistoryState.Parse(historyJson, WoundHistoryState.HistoryPath);
        Log("complete_recorded_history", new { historyJson, parsedHistory.IsValid, parsedHistory.Issues });
        Assert.True(parsedHistory.IsValid, JsonSerializer.Serialize(parsedHistory.Issues)); Assert.Empty(parsedHistory.Issues);
        var history = Assert.IsType<WoundHistoryState>(parsedHistory.State);
        for (var index = 0; index < data.Parents.Count; index++)
        {
            var parent = data.Parents[index];
            var wound = parent.ParentWound;
            var found = history.TryResolveExactTransition(wound.LastTransition.TransitionId, out var anchor);
            var creations = history.ResolveWoundTransitions(wound.WoundId).ToArray();
            var creationCovered = creations.Length == 1 && prior.CoversOriginalCreation(wound.WoundId, wound.Origin.OpportunityId!, creations[0]);
            var encodedParent = source["strainPreimage"]!["parents"]![index]!.AsObject();
            Log("independent_parent_anchor", new { index, parent = fixture.Retained.Parents[index], encodedParent,
                fullParent = WoundMaterializationContract.SerializeCanonical(wound), parent.ParentSemanticFingerprint,
                parent.EffectId, parent.ComponentId, parent.ParentSourceId, found, anchor, creations, creationCovered,
                creationSource = priorRoot["sources"]![index], creationDecision = priorRoot["decisions"]![index] });
            Assert.Equal(index == 0 ? 3 : 4, wound.Severity.Rank);
            Assert.Equal(40 + 2 * index, wound.Origin.CreatedAtTurn);
            Assert.True(JsonNode.DeepEquals(fixture.Retained.Parents[index], JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(wound))));
            Assert.True(JsonNode.DeepEquals(fixture.Retained.Parents[index], Decode(encodedParent["parentWoundJsonBase64"])));
            Assert.Equal(new[] { "componentId", "effectId", "parentWoundJsonBase64" }, encodedParent.Select(field => field.Key).Order(StringComparer.Ordinal).ToArray());
            Assert.Equal(new WoundOwnerCoordinate("chaos_sea", "guardian", "guardian_frame", WoundCarrierCatalog.AfterlifeProfilesPath), wound.Owner);
            Assert.Null(parent.ParentSourceId); Assert.Equal("component_001", parent.ComponentId);
            Assert.Equal(Text(fixture.Retained.Parents[index]["consequences"]!["ownedEffectSources"]!["rootBindings"]![0]!["effectId"]), parent.EffectId);
            Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(wound), parent.ParentSemanticFingerprint);
            Assert.True(found); Assert.NotNull(anchor);
            Assert.Equal((wound.WoundId, wound.LastTransition.Kind, wound.LastTransition.Ordinal, wound.LastTransition.Turn, false, parent.ParentSemanticFingerprint),
                (anchor!.WoundId, anchor.Kind, anchor.WoundTransitionOrdinal, anchor.Turn, anchor.Terminal, anchor.AfterFingerprint));
            Assert.Single(creations); Assert.True(creationCovered);
            Assert.Equal(wound.LastTransition.TransitionId, creations[0].TransitionId);
            Assert.Equal(Text(priorRoot["sources"]![index]!["witness"]!["sourceFingerprint"]), creations[0].SourceFingerprint);
        }
        Log("prerequisite_nonmutation", new { fixtureUnchanged = fixtureJson == JsonSerializer.Serialize(fixture),
            sourceUnchanged = sourceJson == source.ToJsonString(), priorUnchanged = priorJson == priorRoot.ToJsonString(),
            historyUnchanged = historyJson == fixture.Retained.History.ToJsonString() });
        Assert.Equal(fixtureJson, JsonSerializer.Serialize(fixture)); Assert.Equal(sourceJson, source.ToJsonString());
        Assert.Equal(priorJson, priorRoot.ToJsonString()); Assert.Equal(historyJson, fixture.Retained.History.ToJsonString());
        return new(fixture, fixtureJson, source, sourceJson, data, raw, priorRoot, priorJson, prior, history);
    }

    /// <summary>Checks real full-source and ordinary rules/dice entry points without a comparison or a live owner.</summary>
    /// <param name="source">Complete independent comparison-free source.</param>
    /// <param name="exchange">Its entire independently authored exchange.</param>
    /// <param name="expected">Literal margin4/resilience5 ordinary or actual calculation.</param>
    /// <param name="phase">Distinct prerequisite phase for complete diagnostics.</param>
    private void ReadComparisonFreeSource(JsonObject source, JsonObject exchange, SpiritualWoundCalculation expected, string phase)
    {
        var sourceBefore = source.ToJsonString(); var exchangeBefore = exchange.ToJsonString();
        var issues = new List<ValidationIssue>();
        ValidationService.ValidateRetainedSpiritualExchangeShape(exchange, "guardianGroupZero.prerequisite", issues);
        ValidationService.ValidateSpiritualWoundSourceActionShape(exchange, "guardianGroupZero.prerequisite", issues);
        ValidationService.ValidateRetainedSpiritualDiceAudit(exchange, source["turnEvidence"]!.AsObject(), issues);
        ValidationService.ValidateRetainedSpiritualExchangeRules(exchange, issues);
        SpiritualWoundCalculation? calculation = null; SpiritualStrainPreimageData? comparison = null;
        var error = Record.Exception(() => calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out comparison));
        Log(phase, new { source, exchange, expected, calculation, comparisonReturned = comparison is not null, issues, error = error?.ToString() });
        Assert.Empty(issues); Assert.Null(error); Assert.Null(comparison); Assert.False(source.ContainsKey("strainPreimage"));
        Assert.Equal(expected, calculation);
        Assert.Equal(("pressure", "success", "strained", expected.Input.NewStrain, 8, 4, 4, "player_success"),
            (Text(exchange["operationType"]), Text(exchange["outcome"]), Text(exchange["before"]!["oppositionSideStrain"]),
                Text(exchange["after"]!["oppositionSideStrain"]), exchange["diceAudit"]!["playerTotal"]!.GetValue<int>(),
                exchange["diceAudit"]!["oppositionTotal"]!.GetValue<int>(), exchange["diceAudit"]!["margin"]!.GetValue<int>(), Text(exchange["diceAudit"]!["outcomeBand"])));
        Assert.Equal("guardian_frame", SpiritualWoundFailedAction.ReadGuardianDeclaration(exchange));
        Assert.Equal(("standard", 0, 5, 4, 0, "opposition", "guardian", "guardian_frame"),
            (Text(source["appliedArtKind"]), source["appliedArtTier"]!.GetValue<int>(), source["targetResilienceTier"]!.GetValue<int>(),
                source["harmfulMargin"]!.GetValue<int>(), source["maximumSeverityRank"]!.GetValue<int>(), Text(source["affectedSide"]),
                Text(source["affectedActor"]!["actorKind"]), Text(source["affectedActor"]!["actorId"])));
        Assert.Equal(new[] { 8, 4 }, source["turnEvidence"]!["acceptedD20Values"]!.AsArray().Select(row => row!.GetValue<int>()).ToArray());
        Assert.Equal(new[] { 0, 1 }, source["selectedDiceIndices"]!.AsArray().Select(row => row!.GetValue<int>()).ToArray());
        Assert.Equal((0, 0, 46), (source["sourceOrdinal"]!.GetValue<int>(), source["exchangeOrdinal"]!.GetValue<int>(), source["turnEvidence"]!["turn"]!.GetValue<int>()));
        Assert.True(JsonNode.DeepEquals(exchange, Decode(source["exchangeJsonBase64"])));
        foreach (var field in new[] { "guaranteedSeverityRank", "retraumaWoundId", "specialArtJsonBase64", "escalationFingerprint" }) Assert.Null(source[field]);
        Assert.Equal(sourceBefore, source.ToJsonString()); Assert.Equal(exchangeBefore, exchange.ToJsonString());
    }

    /// <summary>Logs all strict parser issues and input nonmutation before requiring an independently valid baseline or replay.</summary>
    /// <param name="root">Private complete receipt image.</param>
    /// <param name="phase">Named prerequisite or independent replay boundary.</param>
    /// <returns>Detached immutable parsed DATA, never permission to publish.</returns>
    private SpiritualWoundOpportunityReceiptState ReadReceipt(JsonObject root, string phase)
    {
        var before = root.ToJsonString();
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(before, SpiritualWoundOpportunityReceiptState.StatePath);
        Log(phase, new { receiptJson = before, parsed.IsValid, parsed.Issues, inputUnchanged = before == root.ToJsonString() });
        Assert.Equal(before, root.ToJsonString());
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues)); Assert.Empty(parsed.Issues);
        return Assert.IsType<SpiritualWoundOpportunityReceiptState>(parsed.State);
    }

    /// <summary>Supplies the independent ordinaryII−4/max0 calculation for margin4 and resilience5.</summary>
    /// <returns>Fixed literal arithmetic rather than a result inferred from parent count.</returns>
    private static SpiritualWoundCalculation OrdinaryCalculation() =>
        new(new(4, 0, 5, "strained", "fractured", "hostile", 4), 1, 2, 0, -4, 0, 2, 4, true, 0);

    /// <summary>Supplies the independently authored actualIV6/max0 calculation for the complete III+IV group.</summary>
    /// <returns>Fixed literal actual arithmetic.</returns>
    private static SpiritualWoundCalculation ActualCalculation() =>
        new(new(4, 0, 5, "strained", "broken", "hostile", 4), 1, 4, 2, 6, 0, 4, 4, true, 0);

    /// <summary>Decodes one complete detached object through the existing strict UTF-8 codec.</summary>
    /// <param name="encoded">Exact base64 object value.</param>
    /// <returns>An independent mutable DATA copy.</returns>
    private static JsonObject Decode(JsonNode? encoded) => SpiritualWoundStateJson.Parse(
        SpiritualWoundStateJson.DecodeUtf8JsonText(SpiritualWoundStateJson.DecodeJsonObjectBytes(encoded)));

    /// <summary>Reads exact required fixture text without accepting an alias or guessed default.</summary>
    /// <param name="node">Required string node.</param>
    /// <returns>The supplied text.</returns>
    private static string Text(JsonNode? node) => node!.GetValue<string>();

    /// <summary>Writes full source/result/error observations without a record or byte cap.</summary>
    /// <param name="phase">Distinct reached DATA phase.</param>
    /// <param name="data">Complete observations for that phase.</param>
    private void Log(string phase, object data) => output.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_zero_receipt_" + phase, data }));

    /// <summary>Retains facts within one case only; mutable inputs are separate from immutable parsed receipt, source and History DATA.</summary>
    /// <param name="Fixture">One complete LowMarginZeroPair with synthetic retained ancestry.</param>
    /// <param name="FixtureJson">Original complete fixture nonmutation snapshot.</param>
    /// <param name="Source">Private sealed full-v7 source candidate.</param>
    /// <param name="SourceJson">Original full source nonmutation snapshot.</param>
    /// <param name="Comparison">Once-parsed immutable full-source and all-parent facts.</param>
    /// <param name="RawOrdinaryBytes">Exact BOM/whitespace ordinary bytes used by the resolver.</param>
    /// <param name="PriorRoot">Private mutable complete original receipt prefix.</param>
    /// <param name="PriorJson">Original complete prior receipt nonmutation snapshot.</param>
    /// <param name="PriorState">Once-parsed immutable original receipt.</param>
    /// <param name="History">Once-parsed immutable original chronology.</param>
    private sealed record Inputs(GuardianGroupPressureData Fixture, string FixtureJson, JsonObject Source, string SourceJson,
        SpiritualStrainPreimageData Comparison, byte[] RawOrdinaryBytes, JsonObject PriorRoot, string PriorJson,
        SpiritualWoundOpportunityReceiptState PriorState, WoundHistoryState History);
}
